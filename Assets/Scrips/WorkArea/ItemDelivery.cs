using System;
using System.Collections;
using UnityEngine;

public class ItemDelivery : MonoBehaviour
{
    [Header("Delivery Points")]
    public Transform spawnPoint;
    [Tooltip("ใช้สำรองเมื่อ CustomerSpawnManager ไม่ได้ส่งตำแหน่งมาให้")]
    public Transform targetPoint;

    [Header("Work Area")]
    public WorkArea workArea;

    [Header("Animation")]
    public float deliveryDuration = 0.8f;
    public float arcHeight = 0.6f;
    [Tooltip("องศาที่สินค้าหมุนส่ายระหว่างลอยมา")]
    public float spinDegrees = 12f;

    [Header("Physics")]
    public float itemGravity = 1f;

    public Action OnDeliveryCompleted;

    /// <summary>เรียกทันทีที่สินค้าถูกสร้าง (ส่ง instanceID)</summary>
    public Action<string> OnItemSpawned;

    private ItemData selectedItem;
    private string ownerCustomerID;
    private Func<Vector3> targetResolver;

    private GameObject spawnedItem;
    private string spawnedItemInstanceID;
    private Vector3 deliveryTarget;

    private bool hasDelivered;
    private Collider2D[] customerColliders;

    // =====================================
    // SAVE / RESTORE
    // =====================================

    public bool HasDelivered() => hasDelivered;

    public string GetDeliveredItemInstanceID() => spawnedItemInstanceID;

    public void RestoreDeliveryState(bool delivered)
    {
        hasDelivered = delivered;
    }

    public void RestoreDeliveredItem(GameObject restoredItem, string instanceID)
    {
        spawnedItem = restoredItem;
        spawnedItemInstanceID = instanceID;

        if (restoredItem == null)
        {
            Debug.LogWarning("Restore Delivered Item: item object is missing.");
            return;
        }

        IgnoreCustomerCollision();
    }

    // =====================================
    // CONFIGURE
    // =====================================

    public void Configure(
        CustomerData customer,
        ItemData item,
        Transform spawn,
        Func<Vector3> targetPositionResolver)
    {
        selectedItem = item;
        ownerCustomerID = customer != null ? customer.customerID : string.Empty;
        spawnPoint = spawn;
        targetResolver = targetPositionResolver;

        hasDelivered = false;

        customerColliders = GetComponentsInChildren<Collider2D>(true);

        if (workArea == null)
            workArea = FindFirstObjectByType<WorkArea>();
    }

    // =====================================
    // DELIVER
    // =====================================

    public void DeliverItem()
    {
        if (hasDelivered)
            return;

        if (selectedItem == null ||
            selectedItem.itemPrefab == null ||
            spawnPoint == null)
        {
            Debug.LogError("ItemDelivery: Missing Item or Spawn Point!");
            return;
        }

        deliveryTarget = ResolveTarget();

        hasDelivered = true;

        spawnedItem = Instantiate(
            selectedItem.itemPrefab,
            spawnPoint.position,
            Quaternion.identity
        );

        SetupSpawnedItem();
        IgnoreCustomerCollision();

        OnItemSpawned?.Invoke(spawnedItemInstanceID);

        StartCoroutine(DeliveryAnimation());
    }

    private Vector3 ResolveTarget()
    {
        if (targetResolver != null)
            return targetResolver();

        if (targetPoint != null)
            return targetPoint.position;

        return spawnPoint.position + Vector3.down;
    }

    // =====================================
    // SETUP SPAWNED ITEM
    // =====================================

    private void SetupSpawnedItem()
    {
        ItemPersistenceData persistence =
            spawnedItem.GetComponent<ItemPersistenceData>();

        if (persistence == null)
            persistence = spawnedItem.AddComponent<ItemPersistenceData>();

        // สินค้าทุกชิ้นได้ instanceID ใหม่ + จำเจ้าของ (customerID) ไว้กับตัว
        persistence.Initialize(ownerCustomerID, selectedItem);
        spawnedItemInstanceID = persistence.instanceID;

        ItemSessionData.RegisterNewItem(persistence.instanceID);

        SpriteRenderer sr = spawnedItem.GetComponent<SpriteRenderer>();

        if (sr == null)
            sr = spawnedItem.GetComponentInChildren<SpriteRenderer>();

        if (sr != null)
            sr.sprite = selectedItem.cursedSprite;

        DraggableItem draggable = spawnedItem.GetComponent<DraggableItem>();

        if (draggable == null)
        {
            Debug.LogError("Item Prefab needs DraggableItem!");
            return;
        }

        draggable.workArea = workArea;
        draggable.itemID = selectedItem.itemID;
        draggable.itemName = selectedItem.itemName;
        draggable.cursedSprite = selectedItem.cursedSprite;
        draggable.cleanedSprite = selectedItem.cleanedSprite;

        Debug.Log(
            "New Item: " + selectedItem.itemName +
            " | Owner: " + ownerCustomerID +
            " | Type: " + selectedItem.itemID +
            " | Instance: " + persistence.instanceID
        );
    }

    // =====================================
    // IGNORE CUSTOMER COLLISION
    // =====================================

    private void IgnoreCustomerCollision()
    {
        if (spawnedItem == null || customerColliders == null)
            return;

        Collider2D[] itemColliders =
            spawnedItem.GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D customerCollider in customerColliders)
        {
            if (customerCollider == null || !customerCollider.enabled)
                continue;

            foreach (Collider2D itemCollider in itemColliders)
            {
                if (itemCollider == null || !itemCollider.enabled)
                    continue;

                Physics2D.IgnoreCollision(customerCollider, itemCollider, true);
            }
        }
    }

    // =====================================
    // DELIVERY ANIMATION
    // =====================================

    private IEnumerator DeliveryAnimation()
    {
        if (spawnedItem == null)
            yield break;

        Rigidbody2D rb = spawnedItem.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        Vector3 startPosition = spawnPoint.position;
        Vector3 endPosition = deliveryTarget;

        float timer = 0f;

        while (timer < deliveryDuration)
        {
            if (spawnedItem == null)
                yield break;

            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / deliveryDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 position = Vector3.Lerp(startPosition, endPosition, smoothT);
            position.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

            spawnedItem.transform.position = position;

            spawnedItem.transform.rotation = Quaternion.Euler(
                0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * spinDegrees
            );

            yield return null;
        }

        if (spawnedItem == null)
            yield break;

        spawnedItem.transform.position = endPosition;
        spawnedItem.transform.rotation = Quaternion.identity;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = itemGravity;
        }

        // รอให้สินค้าตกถึงโต๊ะ
        // (การลงทะเบียน WorkArea ทำโดย DraggableItem ตอนลงจอด —
        //  ถ้า WorkArea ว่างก็ลงทะเบียน ถ้าไม่ว่างสินค้าจะถูกดันออกมารอ)
        float waitTimer = 0f;

        while (spawnedItem != null && waitTimer < 3f)
        {
            DraggableItem draggable = spawnedItem.GetComponent<DraggableItem>();

            if (draggable != null && draggable.HasLanded)
                break;

            waitTimer += Time.deltaTime;

            yield return null;
        }

        OnDeliveryCompleted?.Invoke();
    }
}
