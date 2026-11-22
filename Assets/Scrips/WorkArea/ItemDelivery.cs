
using System;
using System.Collections;
using UnityEngine;

public class ItemDelivery : MonoBehaviour
{
    [Header("Delivery Points")]
    public Transform spawnPoint;
    public Transform targetPoint;

    [Header("Work Area")]
    public WorkArea workArea;

    [Header("Animation")]
    public float deliveryDuration = 0.8f;
    public float arcHeight = 0.6f;

    [Header("Physics")]
    public float itemGravity = 1f;

    private ItemData selectedItem;
    private GameObject spawnedItem;
    private string spawnedItemInstanceID;

    private bool hasDelivered;
    // =====================================
    // SAVE / RESTORE
    // =====================================

    public bool HasDelivered()
    {
        return hasDelivered;
    }
    public string GetDeliveredItemInstanceID()
    {
        return spawnedItemInstanceID;
    }
    public void RestoreDeliveredItem(
    GameObject restoredItem,
    string instanceID)
    {
        if (restoredItem == null)
        {
            Debug.LogWarning("Restore Delivered Item: Item is null.");
            return;
        }

        spawnedItem = restoredItem;
        spawnedItemInstanceID = instanceID;

        // ตั้งค่าไม่ให้ Item ชนกับ Collider ของลูกค้า
        IgnoreCustomerCollision();

        Debug.Log(
            "Delivered Item Reconnected: " + instanceID
        );
    }

    public void RestoreDeliveryState(bool delivered)
    {
        hasDelivered = delivered;
    }

    private Collider2D[] customerColliders;

    public Action OnDeliveryCompleted;

    // =====================================
    // CONFIGURE
    // =====================================

    public void Configure(
        ItemData item,
        Transform spawn,
        Transform target)
    {
        selectedItem = item;
        spawnPoint = spawn;
        targetPoint = target;

        hasDelivered = false;

        // เก็บ Collider ของ Customer และลูกทั้งหมด
        customerColliders =
            GetComponentsInChildren<Collider2D>(true);

        if (workArea == null)
            workArea = FindObjectOfType<WorkArea>();
    }

    // =====================================
    // DELIVER ITEM
    // =====================================

    public void DeliverItem()
    {
        if (hasDelivered)
            return;

        if (selectedItem == null ||
            selectedItem.itemPrefab == null ||
            spawnPoint == null ||
            targetPoint == null)
        {
            Debug.LogError(
                "ItemDelivery: Missing Item or Delivery Points!"
            );
            return;
        }

        hasDelivered = true;

        spawnedItem = Instantiate(
            selectedItem.itemPrefab,
            spawnPoint.position,
            Quaternion.identity
        );

        SetupSpawnedItem();

        IgnoreCustomerCollision();

        StartCoroutine(DeliveryAnimation());
    }

    // =====================================
    // SETUP SPAWNED ITEM
    // =====================================

    void SetupSpawnedItem()
    {
        if (spawnedItem == null)
            return;

        ItemPersistenceData persistence =
            spawnedItem.GetComponent<ItemPersistenceData>();


        if (persistence == null)
        {
            persistence =
                spawnedItem.AddComponent<ItemPersistenceData>();
        }

        // สร้าง Instance ID ใหม่ทุกครั้ง
        persistence.Initialize(selectedItem);
        spawnedItemInstanceID = persistence.instanceID;

        // ลงทะเบียนสถานะใหม่
        ItemSessionData.RegisterNewItem(
            persistence.instanceID
        );

        // ตั้ง Sprite เริ่มต้นเป็น Cursed
        SpriteRenderer sr =
            spawnedItem.GetComponent<SpriteRenderer>();

        if (sr == null)
            sr = spawnedItem.GetComponentInChildren<SpriteRenderer>();

        if (sr != null)
        {
            sr.sprite = selectedItem.cursedSprite;
        }

        DraggableItem draggable =
            spawnedItem.GetComponent<DraggableItem>();

        if (draggable == null)
        {
            Debug.LogError(
                "Item Prefab needs DraggableItem!"
            );
            return;
        }

        draggable.workArea = workArea;

        Debug.Log(
            "New Item: " + selectedItem.itemName +
            " | Type: " + selectedItem.itemID +
            " | Instance: " + persistence.instanceID
        );
    }

    // =====================================
    // IGNORE CUSTOMER COLLISION
    // =====================================

    void IgnoreCustomerCollision()
    {
        if (spawnedItem == null)
            return;

        Collider2D[] itemColliders =
            spawnedItem.GetComponentsInChildren<Collider2D>(true);

        if (customerColliders == null)
            return;

        foreach (Collider2D customerCollider in customerColliders)
        {
            if (customerCollider == null ||
                !customerCollider.enabled)
                continue;

            foreach (Collider2D itemCollider in itemColliders)
            {
                if (itemCollider == null ||
                    !itemCollider.enabled)
                    continue;

                Physics2D.IgnoreCollision(
                    customerCollider,
                    itemCollider,
                    true
                );
            }
        }

        Debug.Log(
            "Customer and delivered Item collision ignored."
        );
    }

    // =====================================
    // DELIVERY ANIMATION
    // =====================================

    IEnumerator DeliveryAnimation()
    {
        if (spawnedItem == null)
            yield break;

        Rigidbody2D rb =
            spawnedItem.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        Vector3 startPosition = spawnPoint.position;
        Vector3 endPosition = targetPoint.position;

        float timer = 0f;

        while (timer < deliveryDuration)
        {
            if (spawnedItem == null)
                yield break;

            timer += Time.deltaTime;

            float t = Mathf.Clamp01(
                timer / deliveryDuration
            );

            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 position = Vector3.Lerp(
                startPosition,
                endPosition,
                smoothT
            );

            position.y +=
                Mathf.Sin(t * Mathf.PI) * arcHeight;

            spawnedItem.transform.position = position;

            yield return null;
        }

        if (spawnedItem == null)
            yield break;

        spawnedItem.transform.position = endPosition;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = itemGravity;
        }

        // รอให้ Item ตกถึงโต๊ะ
        float waitTimer = 0f;

        while (spawnedItem != null &&
               waitTimer < 3f)
        {
            DraggableItem draggable =
                spawnedItem.GetComponent<DraggableItem>();

            if (draggable != null && draggable.HasLanded)
                break;

            waitTimer += Time.deltaTime;

            yield return null;
        }

        // ลงทะเบียน WorkArea ถ้า Item อยู่ในพื้นที่
        if (spawnedItem != null && workArea != null)
        {
            Collider2D itemCollider =
                spawnedItem.GetComponent<Collider2D>();

            if (itemCollider != null &&
                workArea.IsInsideArea(itemCollider.bounds))
            {
                workArea.TryPlaceItem(spawnedItem);
            }
        }

        Debug.Log("Delivery Completed!");

        OnDeliveryCompleted?.Invoke();
    }
}
