
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomerSpawnManager : MonoBehaviour
{
    [Header("Customer Database")]
    public List<CustomerData> customers =
        new List<CustomerData>();

    [Header("Customer Positions")]
    public Transform spawnPoint;
    public Transform stopPoint;
    public Transform exitPoint;

    [Header("Item Positions")]
    public Transform itemSpawnPoint;
    public Transform itemTargetPoint;

    [Header("Work Area")]
    public WorkArea workArea;

    [Header("Spawn Settings")]
    public float minSpawnDelay = 20f;
    public float maxSpawnDelay = 60f;

    private GameObject currentCustomer;

    // ข้อมูลลูกค้าคนปัจจุบัน
    private CustomerData currentCustomerData;
    private ItemData currentSelectedItem;

    // ป้องกันการเริ่มสุ่มลูกค้าก่อน Load เสร็จ
    private bool isInitializing = true;

    public GameObject CurrentCustomer => currentCustomer;

    // =====================================
    // START
    // =====================================

    void Start()
    {
        if (workArea == null)
            workArea = FindObjectOfType<WorkArea>();

        StartCoroutine(InitializeCustomerSystem());
    }

    IEnumerator InitializeCustomerSystem()
    {
        // รอให้ ShopSaveManager เริ่ม Load ก่อน
        yield return null;

        // รอจนกว่าการ Restore Item และ Customer จะเสร็จ
        while (ShopSaveManager.Instance != null &&
               ShopSaveManager.Instance.IsRestoring)
        {
            yield return null;
        }

        isInitializing = false;

        // ถ้าไม่มีลูกค้าที่ Restore กลับมา
        // จึงเริ่มระบบสุ่มลูกค้าตามปกติ
        if (currentCustomer == null)
        {
            StartCoroutine(SpawnCustomerRoutine());
        }
    }

    // =====================================
    // SPAWN ROUTINE
    // =====================================

    IEnumerator SpawnCustomerRoutine()
    {
        while (true)
        {
            // รอจนกว่าลูกค้าเก่าจะออก
            // และ WorkArea จะว่าง
            yield return new WaitUntil(() =>
                !isInitializing &&
                currentCustomer == null &&
                workArea != null &&
                !workArea.IsOccupied
            );

            float delay = Random.Range(
                minSpawnDelay,
                maxSpawnDelay
            );

            Debug.Log(
                "Next customer in " + delay + " seconds."
            );

            yield return new WaitForSeconds(delay);

            // ตรวจสอบอีกครั้งก่อน Spawn
            if (!isInitializing &&
                currentCustomer == null &&
                workArea != null &&
                !workArea.IsOccupied)
            {
                SpawnCustomer();
            }
        }
    }

    // =====================================
    // SPAWN CUSTOMER
    // =====================================

    void SpawnCustomer()
    {
        if (customers == null || customers.Count == 0)
        {
            Debug.LogError("Customer Database is empty!");
            return;
        }

        if (spawnPoint == null)
        {
            Debug.LogError("Spawn Point is missing!");
            return;
        }

        List<CustomerData> validCustomers =
            new List<CustomerData>();

        foreach (CustomerData customer in customers)
        {
            if (customer == null ||
                customer.customerPrefab == null ||
                customer.possibleItems == null ||
                customer.possibleItems.Count == 0)
                continue;

            validCustomers.Add(customer);
        }

        if (validCustomers.Count == 0)
        {
            Debug.LogError("No valid customers!");
            return;
        }

        CustomerData selectedCustomer =
            validCustomers[
                Random.Range(0, validCustomers.Count)
            ];

        List<ItemData> validItems =
            new List<ItemData>();

        foreach (ItemData item in selectedCustomer.possibleItems)
        {
            if (item != null && item.itemPrefab != null)
                validItems.Add(item);
        }

        if (validItems.Count == 0)
        {
            Debug.LogError("No valid Items!");
            return;
        }

        ItemData selectedItem =
            validItems[
                Random.Range(0, validItems.Count)
            ];

        CreateCustomer(selectedCustomer, selectedItem);

        if (currentCustomer != null)
        {
            Debug.Log(
                "Spawned: " +
                selectedCustomer.customerName +
                " | Item: " +
                selectedItem.itemName
            );

            if (ShopSaveManager.Instance != null)
                ShopSaveManager.Instance.SaveGame();
        }
    }

    // =====================================
    // CREATE CUSTOMER
    // =====================================

    void CreateCustomer(
        CustomerData selectedCustomer,
        ItemData selectedItem)
    {
        if (selectedCustomer == null ||
            selectedItem == null)
            return;

        if (selectedCustomer.customerPrefab == null ||
            spawnPoint == null)
            return;

        currentCustomerData = selectedCustomer;
        currentSelectedItem = selectedItem;

        currentCustomer = Instantiate(
            selectedCustomer.customerPrefab,
            spawnPoint.position,
            Quaternion.identity
        );

        CustomerMovement movement =
            currentCustomer.GetComponent<CustomerMovement>();

        CustomerInteraction interaction =
            currentCustomer.GetComponent<CustomerInteraction>();

        ItemDelivery delivery =
            currentCustomer.GetComponent<ItemDelivery>();

        if (movement == null ||
            interaction == null ||
            delivery == null)
        {
            Debug.LogError(
                "Customer Prefab needs CustomerMovement, " +
                "CustomerInteraction and ItemDelivery!"
            );

            Destroy(currentCustomer);
            currentCustomer = null;
            currentCustomerData = null;
            currentSelectedItem = null;

            return;
        }

        // ตั้งค่าจุดเดิน
        movement.startPoint = spawnPoint;
        movement.stopPoint = stopPoint;
        movement.exitPoint = exitPoint;

        // เชื่อม WorkArea
        delivery.workArea = workArea;

        // เมื่อลูกค้าส่งของเสร็จ ให้เดินออก
        delivery.OnDeliveryCompleted -= movement.LeaveShop;
        delivery.OnDeliveryCompleted += movement.LeaveShop;

        // เมื่อลูกค้าเดินออกจนสุดทาง
        movement.OnExited -= HandleCustomerExited;
        movement.OnExited += HandleCustomerExited;

        // ตั้งค่าข้อมูลลูกค้าและ Item
        interaction.ConfigureCustomer(
            selectedCustomer,
            selectedItem,
            itemSpawnPoint,
            itemTargetPoint
        );
    }

    // =====================================
    // CAPTURE CUSTOMER SAVE
    // =====================================

    public CustomerSaveData CaptureCustomerState()
    {
        if (currentCustomer == null ||
            currentCustomerData == null ||
            currentSelectedItem == null)
        {
            return null;
        }

        CustomerInteraction interaction =
    currentCustomer.GetComponent<CustomerInteraction>();

        ItemDelivery delivery =
    currentCustomer.GetComponent<ItemDelivery>();

        return new CustomerSaveData
        {
            customerID = currentCustomerData.customerID,
            itemID = currentSelectedItem.itemID,

            orderAccepted = interaction != null &&
                    interaction.GetOrderAcceptedState(),

            hasDelivered = delivery != null &&
                   delivery.HasDelivered(),

            deliveredItemInstanceID = delivery != null
        ? delivery.GetDeliveredItemInstanceID()
        : ""
        };
    }

    // =====================================
    // RESTORE CUSTOMER
    // =====================================

    public void RestoreSavedCustomer(
        CustomerSaveData savedCustomer,
        CustomerMovementSaveData movementData)
    {
        if (savedCustomer == null)
            return;

        if (currentCustomer != null)
        {
            Debug.LogWarning(
                "A customer already exists. Restore cancelled."
            );
            return;
        }

        // ค้นหาลูกค้าจาก CustomerData เดิม
        CustomerData customerData = customers.Find(
            c => c != null &&
                 c.customerID == savedCustomer.customerID
        );

        if (customerData == null)
        {
            Debug.LogError(
                "Cannot find saved Customer: " +
                savedCustomer.customerID
            );
            return;
        }

        // ค้นหา Item ที่ลูกค้าคนนี้สั่ง
        ItemData selectedItem = customerData.possibleItems.Find(
            item => item != null &&
                    item.itemID == savedCustomer.itemID
        );

        if (selectedItem == null)
        {
            Debug.LogError(
                "Cannot find saved Item: " +
                savedCustomer.itemID
            );
            return;
        }

        Debug.Log(
            "Restoring Customer: " +
            customerData.customerName
        );

        // สร้าง Customer และตั้งค่า Component
        CreateCustomer(customerData, selectedItem);

        if (currentCustomer == null)
        {
            Debug.LogError("Failed to restore Customer!");
            return;
        }

        CustomerInteraction interaction =
    currentCustomer.GetComponent<CustomerInteraction>();

        if (interaction != null)
        {
            interaction.RestoreInteractionState(
                savedCustomer.orderAccepted
            );
        }

        ItemDelivery delivery =
            currentCustomer.GetComponent<ItemDelivery>();

        if (delivery != null)
        {
            delivery.RestoreDeliveryState(
                savedCustomer.hasDelivered
            );
        }
        // ค้นหา Item ที่ถูก Restore กลับมา
        if (delivery != null &&
            savedCustomer.hasDelivered &&
            !string.IsNullOrEmpty(
                savedCustomer.deliveredItemInstanceID))
        {
            ItemPersistenceData[] restoredItems =
                FindObjectsOfType<ItemPersistenceData>(true);

            foreach (ItemPersistenceData persistence in restoredItems)
            {
                if (persistence == null)
                    continue;

                if (persistence.instanceID ==
                    savedCustomer.deliveredItemInstanceID)
                {
                    delivery.RestoreDeliveredItem(
                        persistence.gameObject,
                        persistence.instanceID
                    );

                    break;
                }
            }
        }

        CustomerMovement movement =
            currentCustomer.GetComponent<CustomerMovement>();

        // คืนสถานะการเดิน
        if (movement != null && movementData != null)
        {
            movement.RestoreMovementState(movementData);
        }

        Debug.Log(
            "Customer Restored: " +
            customerData.customerName +
            " | Item: " +
            selectedItem.itemName
        );
    }

    // =====================================
    // CUSTOMER EXIT
    // =====================================

    void HandleCustomerExited()
    {
        if (currentCustomer == null)
            return;

        Debug.Log("Customer has left the shop.");

        GameObject exitedCustomer = currentCustomer;

        currentCustomer = null;
        currentCustomerData = null;
        currentSelectedItem = null;

        Destroy(exitedCustomer);

        if (ShopSaveManager.Instance != null)
        {
            ShopSaveManager.Instance.SaveGame();
        }
    }
}
