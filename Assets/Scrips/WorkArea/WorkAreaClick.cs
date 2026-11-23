
using UnityEngine;
using UnityEngine.SceneManagement;

public class WorkAreaClick : MonoBehaviour
{
    [Header("References")]
    public WorkArea workArea;

    [Header("Scene")]
    public string sceneName = "CleansingScene";

    private GameObject currentItem;

    private DraggableItem draggable;
    private ItemPersistenceData persistence;
    private SpriteRenderer itemSprite;
    private Collider2D itemCollider;
    private Rigidbody2D itemRb;

    private bool canEnterPuzzle;

    void Start()
    {
        if (workArea == null)
            workArea = GetComponent<WorkArea>();
    }

    // =====================================
    // UPDATE
    // =====================================

    void Update()
    {
        canEnterPuzzle = false;

        if (workArea == null)
            return;

        GameObject boardItem = workArea.CurrentItem;

        if (boardItem == null)
        {
            ClearCurrentItem();
            return;
        }

        if (currentItem != boardItem)
        {
            SetCurrentItem(boardItem);
        }

        if (draggable == null ||
            persistence == null ||
            itemSprite == null ||
            itemCollider == null ||
            itemRb == null)
            return;

        bool insideBoard =
            workArea.IsInsideArea(itemCollider.bounds);

        bool solved =
            ItemSessionData.IsPuzzleSolvedFor(
                persistence.instanceID
            );

        if (solved)
        {
            if (draggable.cleanedSprite != null)
                itemSprite.sprite = draggable.cleanedSprite;

            return;
        }

        bool stopped =
            itemRb.linearVelocity.magnitude < 0.1f &&
            Mathf.Abs(itemRb.angularVelocity) < 0.1f;

        canEnterPuzzle =
            insideBoard &&
            draggable.HasLanded &&
            stopped;
    }

    // =====================================
    // SET CURRENT ITEM
    // =====================================

    void SetCurrentItem(GameObject item)
    {
        currentItem = item;

        draggable = item.GetComponent<DraggableItem>();

        persistence =
            item.GetComponent<ItemPersistenceData>();

        itemSprite =
            item.GetComponent<SpriteRenderer>();

        itemCollider =
            item.GetComponent<Collider2D>();

        itemRb =
            item.GetComponent<Rigidbody2D>();

        if (draggable == null)
            Debug.LogError("DraggableItem missing!");

        if (persistence == null)
            Debug.LogError("ItemPersistenceData missing!");

        if (itemSprite == null)
            itemSprite = item.GetComponentInChildren<SpriteRenderer>();

        if (itemCollider == null)
            itemCollider = item.GetComponentInChildren<Collider2D>();

        if (itemRb == null)
            itemRb = item.GetComponent<Rigidbody2D>();
    }

    // =====================================
    // CLEAR
    // =====================================

    void ClearCurrentItem()
    {
        currentItem = null;

        draggable = null;
        persistence = null;

        itemSprite = null;
        itemCollider = null;
        itemRb = null;
    }

    // =====================================
    // CLICK
    // =====================================

    void OnMouseDown()
    {
        if (workArea == null || Time.timeScale <= 0f)
            return;

        GameObject boardItem = workArea.CurrentItem;

        if (boardItem == null)
        {
            Debug.Log("No Item on WorkArea!");
            return;
        }

        if (currentItem != boardItem)
            SetCurrentItem(boardItem);

        if (!canEnterPuzzle)
        {
            Debug.Log("Item is not ready for Puzzle!");
            return;
        }

        if (draggable == null ||
            persistence == null ||
            itemSprite == null)
            return;

        if (persistence.itemData == null ||
            string.IsNullOrEmpty(persistence.instanceID))
        {
            Debug.LogError("Item data is incomplete!");
            return;
        }

        if (ItemSessionData.IsPuzzleSolvedFor(
            persistence.instanceID))
        {
            Debug.Log("This Item is already clean!");
            return;
        }

        ItemSessionData.SavePendingWorkAreaItem(
            persistence.itemData,
            persistence.instanceID,
            currentItem.transform.position,
            currentItem.transform.rotation,
            persistence.customerID
        );

        ItemSessionData.SetCurrentItem(
            persistence.instanceID,
            persistence.itemData.itemID,
            persistence.itemData.itemName,
            itemSprite.sprite
        );

        Debug.Log(
            "Entering Puzzle: " +
            persistence.itemData.itemName +
            " | " + persistence.instanceID
        );

        if (ShopSaveManager.Instance != null)
        {
            ShopSaveManager.Instance.SaveGame();
        }

        SceneManager.LoadScene(sceneName);
    }
}
