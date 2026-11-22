
using System.Collections;
using UnityEngine;

public class ShopItemRestore : MonoBehaviour
{
    [Header("Work Area")]
    public WorkArea workArea;

    void Start()
    {
        if (ShopSaveManager.Instance != null &&
            ShopSaveManager.Instance.HasSaveFile)
        {
            ItemSessionData.ClearPendingWorkAreaItem();
            return;
        }

        StartCoroutine(RestoreRoutine());
    }

    IEnumerator RestoreRoutine()
    {
        yield return null;

        RestoreItem();
    }

    // =====================================
    // RESTORE
    // =====================================

    void RestoreItem()
    {
        if (!ItemSessionData.HasPendingWorkAreaItem)
            return;

        if (workArea == null)
            workArea = FindObjectOfType<WorkArea>();

        if (workArea == null)
        {
            Debug.LogError("WorkArea not found!");
            return;
        }

        if (workArea.IsOccupied)
        {
            Debug.LogError("WorkArea is occupied!");
            return;
        }

        ItemData data = ItemSessionData.PendingItemData;

        string instanceID =
            ItemSessionData.PendingInstanceID;

        if (data == null ||
            data.itemPrefab == null ||
            string.IsNullOrEmpty(instanceID))
        {
            Debug.LogError("Missing Restore Data!");
            return;
        }

        GameObject restoredItem = Instantiate(
            data.itemPrefab,
            ItemSessionData.PendingItemPosition,
            ItemSessionData.PendingItemRotation
        );

        // =====================================
        // IDENTITY
        // =====================================

        ItemPersistenceData persistence =
            restoredItem.GetComponent<ItemPersistenceData>();

        if (persistence == null)
        {
            persistence =
                restoredItem.AddComponent<ItemPersistenceData>();
        }

        persistence.itemData = data;
        persistence.instanceID = instanceID;
        persistence.restoredOnBoard = true;

        // =====================================
        // DRAGGABLE
        // =====================================

        DraggableItem draggable =
            restoredItem.GetComponent<DraggableItem>();

        if (draggable == null)
        {
            Debug.LogError("DraggableItem missing!");

            Destroy(restoredItem);
            return;
        }

        draggable.workArea = workArea;

        // =====================================
        // SPRITE
        // =====================================

        bool solved =
            ItemSessionData.IsPuzzleSolvedFor(instanceID);

        SpriteRenderer sr =
            restoredItem.GetComponent<SpriteRenderer>();

        if (sr == null)
            sr = restoredItem.GetComponentInChildren<SpriteRenderer>();

        if (sr != null)
        {
            sr.sprite = solved
                ? data.cleanedSprite
                : data.cursedSprite;
        }

        // =====================================
        // PHYSICS
        // =====================================

        Rigidbody2D rb =
            restoredItem.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Dynamic;
        }

        // =====================================
        // REGISTER
        // =====================================

        bool placed = workArea.TryPlaceItem(restoredItem);

        if (!placed)
        {
            Debug.LogError("Failed to register restored Item!");

            Destroy(restoredItem);
            return;
        }

        ItemSessionData.ClearPendingWorkAreaItem();

        Debug.Log(
            "Restored Item: " + data.itemName +
            " | Instance: " + instanceID +
            " | Solved: " + solved
        );
    }
}
