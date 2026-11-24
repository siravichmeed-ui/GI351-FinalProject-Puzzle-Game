using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public enum ReturnRequestResult
{
    Started,
    NoItem,
    UnknownOwner,
    NotSolved,
    ReturnBusy,
    MissingPoints
}

/// <summary>
/// จัดการลูกค้า 2 ทาง แยกกัน:
///  1) ลูกค้าใหม่ (มาฝากของ) — ทีละคน แต่ออกปุ๊บคนต่อไปเข้าได้ทันที
///  2) เจ้าของสินค้าที่ถูกเรียกกลับมารับของ — ทีละคนเช่นกัน ไม่บล็อกลูกค้าใหม่
/// สินค้าที่ฝากไว้ทุกชิ้นเป็น "ออเดอร์" ผูกด้วย instanceID
/// </summary>
public class CustomerSpawnManager : MonoBehaviour
{
    [Header("Customer Database")]
    public List<CustomerData> customers = new List<CustomerData>();

    [Header("Customer Positions")]
    public Transform spawnPoint;
    public Transform stopPoint;
    public Transform exitPoint;

    [Header("Item Positions")]
    public Transform itemSpawnPoint;
    [Tooltip("ใช้เมื่อไม่มี Waiting Slot ว่าง")]
    public Transform itemTargetPoint;
    [Tooltip("(ไม่บังคับ) ช่องวางสินค้ารอทำ Puzzle — สินค้าใหม่จะลงช่องว่างช่องแรก")]
    public Transform[] waitingSlots;
    public float slotOccupiedRadius = 0.6f;
    public float fallbackSpread = 1.2f;

    [Header("Return Pickup")]
    public Transform returnPickupPoint;
    public float pickupFlyDuration = 0.4f;
    public float happyPause = 0.35f;

    [Header("Work Area")]
    public WorkArea workArea;

    [Header("Spawn Settings")]
    public float minSpawnDelay = 20f;
    public float maxSpawnDelay = 60f;
    [Tooltip("หน่วงก่อนลูกค้าคนถัดไปเข้า หลังคนก่อนหน้าฝากของและเดินออก")]
    public float nextCustomerDelay = 1.5f;
    [Tooltip("จำนวนสินค้าที่รอรับคืนได้สูงสุด (0 = ไม่จำกัด)")]
    public int maxWaitingOrders = 5;

    // ---------------------------------------------------------------

    private class PendingOrder
    {
        public string instanceID;
        public string customerID;
        public string itemID;
        public CustomerData customer;
        public bool rewardPaid;
        public bool itemPickedUp;
    }

    private class CustomerRig
    {
        public GameObject go;
        public CustomerMovement movement;
        public CustomerInteraction interaction;
        public ItemDelivery delivery;
    }

    private readonly Dictionary<string, PendingOrder> orders =
        new Dictionary<string, PendingOrder>();

    // ลูกค้าใหม่
    private GameObject currentCustomer;
    private CustomerData currentCustomerData;
    private ItemData currentSelectedItem;

    // เจ้าของที่มารับของ
    private GameObject returnCustomer;
    private PendingOrder returnOrder;
    private Coroutine pickupRoutine;

    // กดกระดิ่งตอนเจ้าของยังเดินออกไม่พ้นร้าน -> รอไว้ แล้วเรียกกลับทันทีที่ออกไป
    private PendingOrder queuedReturn;

    private bool isInitializing = true;
    private bool spawnQuickly;

    public GameObject CurrentCustomer => currentCustomer;
    public GameObject ReturnCustomer => returnCustomer;
    public bool IsReturnInProgress => returnCustomer != null || queuedReturn != null;
    public int PendingOrderCount => orders.Count;

    /// <summary>ยังมีลูกค้า (ใหม่/มารับของ) อยู่ในร้านหรือรอคิวอยู่</summary>
    public bool HasActiveCustomers =>
        currentCustomer != null || returnCustomer != null || queuedReturn != null;

    private void HandleDayStarted()
    {
        spawnQuickly = true;
    }

    private void OnDestroy()
    {
        if (ShopDayManager.Instance != null)
            ShopDayManager.Instance.OnDayStarted -= HandleDayStarted;
    }

    /// <summary>ปิดร้าน: ลูกค้าที่ยังไม่ได้ฝากของให้เดินออกไป (คนที่ฝากแล้วจะออกเองตามปกติ)</summary>
    public void DismissWaitingCustomer()
    {
        if (currentCustomer == null)
            return;

        CustomerInteraction interaction = currentCustomer.GetComponent<CustomerInteraction>();

        if (interaction != null)
            interaction.DismissForClosing();
    }

    // =====================================
    // START
    // =====================================

    private void Start()
    {
        if (workArea == null)
            workArea = FindFirstObjectByType<WorkArea>();

        StartCoroutine(InitializeCustomerSystem());
    }

    private IEnumerator InitializeCustomerSystem()
    {
        yield return null;

        while (ShopSaveManager.Instance != null &&
               ShopSaveManager.Instance.IsRestoring)
            yield return null;

        isInitializing = false;

        ShopDayManager day = ShopDayManager.Instance;

        if (day != null)
        {
            day.OnDayStarted += HandleDayStarted;

            // วันเพิ่งเริ่ม -> ลูกค้าคนแรกเข้าเร็วๆ ไม่ต้องรอสุ่ม 20-60 วินาที
            if (day.ElapsedMinutes < 1f)
                spawnQuickly = true;
        }

        StartCoroutine(SpawnCustomerRoutine());
    }

    // =====================================
    // SPAWN NEW CUSTOMER
    // =====================================

    private bool CanSpawnCustomer()
    {
        return !isInitializing &&
               (ShopDayManager.Instance == null || ShopDayManager.Instance.IsShopOpen) &&
               currentCustomer == null &&
               (maxWaitingOrders <= 0 || orders.Count < maxWaitingOrders);
    }

    private IEnumerator SpawnCustomerRoutine()
    {
        while (true)
        {
            // ไม่รอ WorkArea / ไม่รอรับคืน — รอแค่ "ไม่มีลูกค้าใหม่อยู่ในร้าน"
            yield return new WaitUntil(CanSpawnCustomer);

            float delay = spawnQuickly
                ? nextCustomerDelay
                : Random.Range(minSpawnDelay, maxSpawnDelay);

            spawnQuickly = false;

            yield return new WaitForSeconds(delay);

            if (CanSpawnCustomer())
                SpawnCustomer();
        }
    }

    private void SpawnCustomer()
    {
        if (customers == null || customers.Count == 0 || spawnPoint == null)
        {
            Debug.LogError("CustomerSpawnManager: Customer database or Spawn Point is missing!");
            return;
        }

        List<CustomerData> validCustomers = new List<CustomerData>();

        foreach (CustomerData customer in customers)
        {
            if (customer != null &&
                customer.customerPrefab != null &&
                customer.possibleItems != null &&
                customer.possibleItems.Count > 0)
                validCustomers.Add(customer);
        }

        if (validCustomers.Count == 0)
        {
            Debug.LogError("CustomerSpawnManager: No valid customers!");
            return;
        }

        CustomerData selectedCustomer =
            validCustomers[Random.Range(0, validCustomers.Count)];

        List<ItemData> validItems = new List<ItemData>();

        foreach (ItemData item in selectedCustomer.possibleItems)
        {
            if (item != null && item.itemPrefab != null)
                validItems.Add(item);
        }

        if (validItems.Count == 0)
        {
            Debug.LogError("CustomerSpawnManager: No valid items!");
            return;
        }

        ItemData selectedItem = validItems[Random.Range(0, validItems.Count)];

        if (CreateOrderCustomer(selectedCustomer, selectedItem) &&
            ShopSaveManager.Instance != null)
            ShopSaveManager.Instance.SaveGame();
    }

    private CustomerRig InstantiateCustomer(CustomerData data, Transform startPoint)
    {
        if (data == null || data.customerPrefab == null || startPoint == null)
        {
            Debug.LogError("CustomerSpawnManager: Customer data/prefab/start point is missing!");
            return null;
        }

        GameObject go = Instantiate(
            data.customerPrefab,
            startPoint.position,
            Quaternion.identity
        );

        CustomerRig rig = new CustomerRig
        {
            go = go,
            movement = go.GetComponent<CustomerMovement>(),
            interaction = go.GetComponent<CustomerInteraction>(),
            delivery = go.GetComponent<ItemDelivery>()
        };

        if (rig.movement == null || rig.interaction == null || rig.delivery == null)
        {
            Debug.LogError("Customer Prefab needs CustomerMovement, CustomerInteraction and ItemDelivery!");
            Destroy(go);
            return null;
        }

        rig.movement.startPoint = startPoint;
        rig.movement.exitPoint = exitPoint;

        return rig;
    }

    private bool CreateOrderCustomer(CustomerData customer, ItemData item)
    {
        if (item == null)
            return false;

        CustomerRig rig = InstantiateCustomer(customer, spawnPoint);

        if (rig == null)
            return false;

        currentCustomer = rig.go;
        currentCustomerData = customer;
        currentSelectedItem = item;

        rig.movement.stopPoint = stopPoint;
        rig.delivery.workArea = workArea;

        rig.interaction.ConfigureCustomer(
            customer,
            item,
            itemSpawnPoint,
            ResolveItemTarget
        );

        rig.movement.OnExited += HandleCustomerExited;
        rig.delivery.OnDeliveryCompleted += rig.movement.LeaveShop;

        // ลงทะเบียนออเดอร์ตั้งแต่สินค้าถูกส่งมา (ไม่ต้องรอลูกค้าเดินออก)
        rig.delivery.OnItemSpawned += instanceID =>
        {
            RegisterOrder(customer, item, instanceID);

            if (ShopSaveManager.Instance != null)
                ShopSaveManager.Instance.SaveGame();
        };

        return true;
    }

    // =====================================
    // ITEM TARGET (WAITING SLOTS)
    // =====================================

    private Vector3 ResolveItemTarget()
    {
        if (waitingSlots != null)
        {
            foreach (Transform slot in waitingSlots)
            {
                if (slot != null && !IsSlotTaken(slot.position))
                    return slot.position;
            }
        }

        Vector3 basePos = itemTargetPoint != null
            ? itemTargetPoint.position
            : (itemSpawnPoint != null ? itemSpawnPoint.position : Vector3.zero);

        basePos.x += Random.Range(-fallbackSpread, fallbackSpread);

        return basePos;
    }

    private bool IsSlotTaken(Vector3 position)
    {
        foreach (ItemPersistenceData item in ItemPersistenceData.All)
        {
            if (item == null)
                continue;

            if (Vector2.Distance(item.transform.position, position) < slotOccupiedRadius)
                return true;
        }

        return false;
    }

    // =====================================
    // CUSTOMER EXIT -> REGISTER ORDER
    // =====================================

    private void HandleCustomerExited()
    {
        if (currentCustomer == null)
            return;

        GameObject exited = currentCustomer;

        ItemDelivery delivery = exited.GetComponent<ItemDelivery>();

        if (delivery != null && delivery.HasDelivered())
        {
            RegisterOrder(
                currentCustomerData,
                currentSelectedItem,
                delivery.GetDeliveredItemInstanceID()
            );
        }

        // ตั้งก่อนเคลียร์ currentCustomer เพื่อให้ routine เห็นค่านี้ตอนปล่อยผ่าน
        spawnQuickly = true;

        currentCustomer = null;
        currentCustomerData = null;
        currentSelectedItem = null;

        Destroy(exited);

        // ถ้ามีคนกดกระดิ่งรอไว้ ให้เจ้าของเดินกลับมารับเลย
        if (queuedReturn != null)
        {
            PendingOrder queued = queuedReturn;
            queuedReturn = null;

            if (!StartReturnCustomer(queued, null))
                SetOrderItemLocked(queued, false);
        }

        if (ShopSaveManager.Instance != null)
            ShopSaveManager.Instance.SaveGame();
    }

    private void RegisterOrder(CustomerData customer, ItemData item, string instanceID)
    {
        if (customer == null || item == null || string.IsNullOrEmpty(instanceID))
            return;

        // ลงทะเบียนแล้วไม่ทับซ้ำ (กันธง rewardPaid ถูกรีเซ็ต)
        if (orders.ContainsKey(instanceID))
            return;

        orders[instanceID] = new PendingOrder
        {
            instanceID = instanceID,
            customerID = customer.customerID,
            itemID = item.itemID,
            customer = customer
        };

        ItemPersistenceData persistence = ItemPersistenceData.Find(instanceID);

        if (persistence != null && string.IsNullOrEmpty(persistence.customerID))
            persistence.customerID = customer.customerID;

        Debug.Log(
            "Order registered | Customer: " + customer.customerID +
            " | Item: " + item.itemID +
            " | Instance: " + instanceID
        );
    }

    // =====================================
    // RETURN (กดกระดิ่ง)
    // =====================================

    public ReturnRequestResult BeginReturn(GameObject item)
    {
        if (item == null)
            return ReturnRequestResult.NoItem;

        ItemPersistenceData persistence = item.GetComponent<ItemPersistenceData>();

        if (persistence == null ||
            string.IsNullOrEmpty(persistence.instanceID) ||
            !orders.TryGetValue(persistence.instanceID, out PendingOrder order))
            return ReturnRequestResult.UnknownOwner;

        // เช็คว่าสินค้ากับออเดอร์เป็นเจ้าของคนเดียวกันจริง
        if (!string.IsNullOrEmpty(persistence.customerID) &&
            persistence.customerID != order.customerID)
        {
            Debug.LogWarning("Item owner does not match order owner!");
            return ReturnRequestResult.UnknownOwner;
        }

        if (returnCustomer != null || queuedReturn != null)
            return ReturnRequestResult.ReturnBusy;

        if (!ItemSessionData.IsPuzzleSolvedFor(order.instanceID))
            return ReturnRequestResult.NotSolved;

        if (order.customer == null)
            return ReturnRequestResult.UnknownOwner;

        if (exitPoint == null || returnPickupPoint == null)
        {
            Debug.LogError("กรุณากำหนด Exit Point และ Return Pickup Point ใน Inspector");
            return ReturnRequestResult.MissingPoints;
        }

        // เจ้าของยังอยู่ในร้าน (กำลังเดินออก) -> เข้าคิวไว้ ไม่สร้างลูกค้าซ้อนตัว
        if (IsOwnerStillInShop(order))
        {
            queuedReturn = order;
            SetOrderItemLocked(order, true);
            return ReturnRequestResult.Started;
        }

        if (!StartReturnCustomer(order, null))
            return ReturnRequestResult.MissingPoints;

        if (ShopSaveManager.Instance != null)
            ShopSaveManager.Instance.SaveGame();

        return ReturnRequestResult.Started;
    }

    private bool IsOwnerStillInShop(PendingOrder order)
    {
        if (currentCustomer == null)
            return false;

        ItemDelivery delivery = currentCustomer.GetComponent<ItemDelivery>();

        return delivery != null &&
               delivery.GetDeliveredItemInstanceID() == order.instanceID;
    }

    private bool StartReturnCustomer(PendingOrder order, CustomerMovementSaveData savedMovement)
    {
        CustomerRig rig = InstantiateCustomer(order.customer, exitPoint);

        if (rig == null)
            return false;

        returnCustomer = rig.go;
        returnOrder = order;

        rig.movement.stopPoint = returnPickupPoint;

        rig.interaction.SetReturnCustomer();

        rig.movement.OnReturnArrived += HandleReturnArrived;
        rig.movement.OnExited += HandleReturnCustomerExited;

        rig.movement.ReturnToShop(returnPickupPoint);

        if (savedMovement != null)
            rig.movement.RestoreMovementState(savedMovement);

        // ล็อกสินค้า ไม่ให้ผู้เล่นลากหนีระหว่างลูกค้ากำลังเดินมา
        SetOrderItemLocked(order, true);

        // กรณีโหลดเซฟตอนลูกค้ายืนอยู่ที่จุดรับแล้ว
        if (savedMovement != null &&
            rig.movement.HasArrived &&
            !rig.movement.IsLeaving)
            HandleReturnArrived();

        return true;
    }

    private void SetOrderItemLocked(PendingOrder order, bool locked)
    {
        ItemPersistenceData persistence = ItemPersistenceData.Find(order.instanceID);

        if (persistence == null)
            return;

        DraggableItem draggable = persistence.GetComponent<DraggableItem>();

        if (draggable != null)
            draggable.SetLocked(locked);
    }

    private void HandleReturnArrived()
    {
        if (returnCustomer == null || returnOrder == null || pickupRoutine != null)
            return;

        pickupRoutine = StartCoroutine(PickupRoutine());
    }

    private IEnumerator PickupRoutine()
    {
        PendingOrder order = returnOrder;
        GameObject customerObject = returnCustomer;

        CustomerMovement movement = customerObject.GetComponent<CustomerMovement>();

        // 1) รับของ (สินค้าบินเข้าหาลูกค้า)
        if (!order.itemPickedUp)
        {
            ItemPersistenceData persistence = ItemPersistenceData.Find(order.instanceID);

            if (persistence != null)
            {
                DraggableItem draggable = persistence.GetComponent<DraggableItem>();

                if (draggable != null)
                    yield return StartCoroutine(
                        draggable.PlayCollectAnimation(
                            customerObject.transform,
                            pickupFlyDuration
                        )
                    );

                Destroy(persistence.gameObject);
            }
            else
            {
                Debug.LogWarning("ไม่พบสินค้าของออเดอร์: " + order.instanceID);
            }

            order.itemPickedUp = true;

            // สินค้าชิ้นนี้จบหน้าที่แล้ว ลบความคืบหน้าออกจาก PlayerPrefs
            ItemSessionData.RemoveProgress(order.instanceID);
        }

        // 2) จ่ายเงิน — ครั้งเดียวต่อออเดอร์ (บันทึกธงก่อนจ่าย กันจ่ายซ้ำถ้าเซฟ/โหลด)
        if (!order.rewardPaid)
        {
            order.rewardPaid = true;

            if (ShopSaveManager.Instance != null)
                ShopSaveManager.Instance.SaveGame();

            if (ShopMoneyManager.Instance != null && order.customer != null)
                ShopMoneyManager.Instance.AddMoney(order.customer.returnReward);

            if (order.customer != null)
            {
                MagicDust.Burst(
                    customerObject.transform.position + Vector3.up * 1.2f,
                    18,
                    new Color(1f, 0.85f, 0.25f)
                );

                FloatingText.Spawn(
                    "+" + order.customer.returnReward.ToString("N0"),
                    customerObject.transform.position + Vector3.up * 1.6f,
                    new Color(1f, 0.85f, 0.25f),
                    6f
                );
            }
        }

        // 3) ดีใจแล้วเดินออก
        if (movement != null)
        {
            movement.Hop();

            yield return new WaitForSeconds(happyPause);

            movement.LeaveShop();
        }

        pickupRoutine = null;
    }

    private void HandleReturnCustomerExited()
    {
        if (returnCustomer == null)
            return;

        GameObject exited = returnCustomer;

        if (returnOrder != null)
            orders.Remove(returnOrder.instanceID);

        returnCustomer = null;
        returnOrder = null;
        pickupRoutine = null;

        Destroy(exited);

        if (ShopSaveManager.Instance != null)
            ShopSaveManager.Instance.SaveGame();
    }

    // =====================================
    // SAVE
    // =====================================

    public bool ShouldExcludeItemFromSave(string instanceID)
    {
        return !string.IsNullOrEmpty(instanceID) &&
               orders.TryGetValue(instanceID, out PendingOrder order) &&
               order.itemPickedUp;
    }

    public void CaptureState(ShopSaveData data)
    {
        if (data == null)
            return;

        data.orders.Clear();

        foreach (PendingOrder order in orders.Values)
        {
            data.orders.Add(new OrderSaveData
            {
                instanceID = order.instanceID,
                customerID = order.customerID,
                itemID = order.itemID,
                rewardPaid = order.rewardPaid,
                itemPickedUp = order.itemPickedUp
            });
        }

        if (currentCustomer != null &&
            currentCustomerData != null &&
            currentSelectedItem != null)
        {
            CustomerInteraction interaction = currentCustomer.GetComponent<CustomerInteraction>();
            ItemDelivery delivery = currentCustomer.GetComponent<ItemDelivery>();
            CustomerMovement movement = currentCustomer.GetComponent<CustomerMovement>();

            data.customer = new CustomerSaveData
            {
                customerID = currentCustomerData.customerID,
                itemID = currentSelectedItem.itemID,
                orderAccepted = interaction != null && interaction.GetOrderAcceptedState(),
                hasDelivered = delivery != null && delivery.HasDelivered(),
                deliveredItemInstanceID = delivery != null ? delivery.GetDeliveredItemInstanceID() : ""
            };

            if (movement != null)
                data.customerMovement = movement.CaptureMovementState();
        }

        if (returnCustomer != null && returnOrder != null)
        {
            CustomerMovement movement = returnCustomer.GetComponent<CustomerMovement>();

            data.returningOrderInstanceID = returnOrder.instanceID;

            if (movement != null)
                data.returnCustomerMovement = movement.CaptureMovementState();
        }
    }

    // =====================================
    // RESTORE
    // =====================================

    public void RestoreState(ShopSaveData data)
    {
        if (data == null)
            return;

        MigrateLegacy(data);

        orders.Clear();

        if (data.orders != null)
        {
            foreach (OrderSaveData saved in data.orders)
            {
                if (saved == null || string.IsNullOrEmpty(saved.instanceID))
                    continue;

                CustomerData customer = FindCustomer(saved.customerID);

                if (customer == null)
                {
                    Debug.LogWarning("Cannot find customer for order: " + saved.customerID);
                    continue;
                }

                orders[saved.instanceID] = new PendingOrder
                {
                    instanceID = saved.instanceID,
                    customerID = saved.customerID,
                    itemID = saved.itemID,
                    customer = customer,
                    rewardPaid = saved.rewardPaid,
                    itemPickedUp = saved.itemPickedUp
                };
            }
        }

        // เติม customerID ให้สินค้าที่เซฟจากเวอร์ชันเก่า และลบออเดอร์ที่สินค้าหายไป
        List<string> orphaned = new List<string>();

        foreach (PendingOrder order in orders.Values)
        {
            ItemPersistenceData item = ItemPersistenceData.Find(order.instanceID);

            if (item != null)
            {
                if (string.IsNullOrEmpty(item.customerID))
                    item.customerID = order.customerID;
            }
            else if (!order.itemPickedUp)
            {
                orphaned.Add(order.instanceID);
            }
        }

        foreach (string id in orphaned)
        {
            Debug.LogWarning("Order item missing, removing order: " + id);
            orders.Remove(id);
            ItemSessionData.RemoveProgress(id);
        }

        // เจ้าของที่กำลังเดินมารับของ
        if (!string.IsNullOrEmpty(data.returningOrderInstanceID) &&
            orders.TryGetValue(data.returningOrderInstanceID, out PendingOrder returning))
        {
            StartReturnCustomer(returning, data.returnCustomerMovement);
        }

        // ลูกค้าใหม่ที่กำลังฝากของ
        RestoreActiveCustomer(data.customer, data.customerMovement);
    }

    private void RestoreActiveCustomer(CustomerSaveData saved, CustomerMovementSaveData movementData)
    {
        if (saved == null || string.IsNullOrEmpty(saved.customerID))
            return;

        CustomerData customer = FindCustomer(saved.customerID);
        ItemData item = FindItem(customer, saved.itemID);

        if (customer == null || item == null)
        {
            Debug.LogError("Cannot restore active customer: " + saved.customerID);
            return;
        }

        if (!CreateOrderCustomer(customer, item))
            return;

        CustomerInteraction interaction = currentCustomer.GetComponent<CustomerInteraction>();
        ItemDelivery delivery = currentCustomer.GetComponent<ItemDelivery>();
        CustomerMovement movement = currentCustomer.GetComponent<CustomerMovement>();

        if (interaction != null)
            interaction.RestoreInteractionState(saved.orderAccepted);

        if (delivery != null)
        {
            delivery.RestoreDeliveryState(saved.hasDelivered);

            if (saved.hasDelivered &&
                !string.IsNullOrEmpty(saved.deliveredItemInstanceID))
            {
                // เซฟเก่าที่ยังไม่ลงทะเบียนออเดอร์ตอนส่งของ
                RegisterOrder(customer, item, saved.deliveredItemInstanceID);

                ItemPersistenceData persistence =
                    ItemPersistenceData.Find(saved.deliveredItemInstanceID);

                delivery.RestoreDeliveredItem(
                    persistence != null ? persistence.gameObject : null,
                    saved.deliveredItemInstanceID
                );
            }
        }

        if (movement != null)
        {
            if (movementData != null)
                movement.RestoreMovementState(movementData);

            // เซฟตอนแอนิเมชันส่งของยังไม่จบ -> ไม่มี event ให้เดินออก ต้องสั่งเอง
            if (saved.hasDelivered && !movement.IsLeaving)
                movement.LeaveShop();
        }
    }

    private void MigrateLegacy(ShopSaveData data)
    {
        if (data.version >= 2 ||
            data.customer == null ||
            !data.customer.hasPendingReturn)
            return;

        CustomerSaveData old = data.customer;

        string instanceID = !string.IsNullOrEmpty(old.pendingItemInstanceID)
            ? old.pendingItemInstanceID
            : old.deliveredItemInstanceID;

        if (data.orders == null)
            data.orders = new List<OrderSaveData>();

        if (!string.IsNullOrEmpty(instanceID))
        {
            data.orders.Add(new OrderSaveData
            {
                instanceID = instanceID,
                customerID = !string.IsNullOrEmpty(old.pendingCustomerID)
                    ? old.pendingCustomerID : old.customerID,
                itemID = !string.IsNullOrEmpty(old.pendingItemID)
                    ? old.pendingItemID : old.itemID,
                rewardPaid = old.returnRewardPaid,
                itemPickedUp = old.returnItemPickedUp
            });

            if (old.isReturning)
            {
                data.returningOrderInstanceID = instanceID;
                data.returnCustomerMovement = data.customerMovement;
            }
        }

        data.customer = null;
    }

    // =====================================
    // LOOKUP
    // =====================================

    private CustomerData FindCustomer(string customerID)
    {
        if (string.IsNullOrEmpty(customerID) || customers == null)
            return null;

        return customers.Find(c => c != null && c.customerID == customerID);
    }

    private ItemData FindItem(CustomerData customer, string itemID)
    {
        if (customer == null || customer.possibleItems == null)
            return null;

        return customer.possibleItems.Find(i => i != null && i.itemID == itemID);
    }
}
