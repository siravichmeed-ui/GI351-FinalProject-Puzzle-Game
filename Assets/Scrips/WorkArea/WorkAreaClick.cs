
using UnityEngine;
using UnityEngine.SceneManagement;

public class WorkAreaClick : MonoBehaviour
{
    [Header("References")]
    public WorkArea workArea;

    [Header("Settings")]
    public string sceneName = "CleansingScene";
    public float stopSpeed = 0.1f;

    private GameObject currentItem;
    private DraggableItem currentDraggable;
    private Rigidbody2D currentRb;
    private Collider2D currentCollider;
    private SpriteRenderer currentSprite;

    private bool canEnterPuzzle = false;

    void Start()
    {
        if (workArea == null)
            workArea = GetComponent<WorkArea>();
    }

    void Update()
    {
        canEnterPuzzle = false;

        if (workArea == null)
            return;

        // อ่านไอเทมที่อยู่บนกระดานในขณะนี้
        GameObject boardItem = workArea.CurrentItem;

        if (boardItem == null)
        {
            ClearCurrentItem();
            return;
        }

        // ถ้าเป็นไอเทมชิ้นใหม่ ให้ดึง Component ใหม่
        if (currentItem != boardItem)
        {
            SetCurrentItem(boardItem);
        }

        if (currentDraggable == null ||
            currentRb == null ||
            currentCollider == null ||
            currentSprite == null)
            return;

        // ตรวจสอบข้อมูลไอเทม
        if (string.IsNullOrEmpty(currentDraggable.itemID))
        {
            Debug.LogError("Item ID is missing!");
            return;
        }

        // ตรวจสอบว่าไอเทมอยู่ในพื้นที่กระดานทั้งหมด
        bool insideBoard =
            workArea.IsInsideArea(currentCollider.bounds);

        // ตรวจสอบว่าไอเทมตกถึงโต๊ะแล้ว
        bool hasLanded = currentDraggable.HasLanded;

        // ตรวจสอบว่าไอเทมหยุดนิ่งแล้ว
        bool isStopped =
            currentRb.bodyType == RigidbodyType2D.Dynamic &&
            currentRb.linearVelocity.magnitude <= stopSpeed &&
            Mathf.Abs(currentRb.angularVelocity) <= stopSpeed;

        // ถ้า Puzzle ของไอเทมนี้แก้แล้ว ให้เปลี่ยนเป็นภาพสะอาด
        bool isPuzzleSolved =
            ItemSessionData.IsPuzzleSolvedFor(currentDraggable.itemID);

        if (isPuzzleSolved)
        {
            if (currentDraggable.cleanedSprite != null)
            {
                currentSprite.sprite = currentDraggable.cleanedSprite;
            }

            return;
        }

        // อนุญาตให้คลิกกระดานเมื่อไอเทมพร้อมเท่านั้น
        canEnterPuzzle =
            insideBoard &&
            hasLanded &&
            isStopped;
    }

    // =====================================
    // SET CURRENT ITEM
    // =====================================

    void SetCurrentItem(GameObject item)
    {
        currentItem = item;

        currentDraggable =
            item.GetComponent<DraggableItem>();

        currentRb =
            item.GetComponent<Rigidbody2D>();

        currentCollider =
            item.GetComponent<Collider2D>();

        currentSprite =
            item.GetComponent<SpriteRenderer>();

        if (currentDraggable == null)
            Debug.LogError("Item has no DraggableItem!");

        if (currentRb == null)
            Debug.LogError("Item has no Rigidbody2D!");

        if (currentCollider == null)
            Debug.LogError("Item has no Collider2D!");

        if (currentSprite == null)
            Debug.LogError("Item has no SpriteRenderer!");

        if (currentDraggable != null)
        {
            Debug.Log(
                "WorkArea Item: " +
                currentDraggable.itemName
            );
        }
    }

    // =====================================
    // CLEAR CURRENT ITEM
    // =====================================

    void ClearCurrentItem()
    {
        currentItem = null;
        currentDraggable = null;
        currentRb = null;
        currentCollider = null;
        currentSprite = null;
    }

    // =====================================
    // CLICK BOARD
    // =====================================

    void OnMouseDown()
    {
        if (workArea == null)
            return;

        // ต้องมีไอเทมอยู่บนกระดาน
        GameObject boardItem = workArea.CurrentItem;

        if (boardItem == null)
        {
            Debug.Log("No item on WorkArea!");
            return;
        }

        // ตรวจสอบว่าข้อมูลที่ Cache ไว้ตรงกับไอเทมปัจจุบัน
        if (currentItem != boardItem)
        {
            SetCurrentItem(boardItem);
        }

        if (!canEnterPuzzle)
        {
            Debug.Log("Item is not ready for Puzzle!");
            return;
        }

        if (currentDraggable == null ||
            currentSprite == null)
            return;

        string id = currentDraggable.itemID;
        string itemName = currentDraggable.itemName;

        // ป้องกันการเข้า Puzzle ซ้ำ
        if (ItemSessionData.IsPuzzleSolvedFor(id))
        {
            Debug.Log(itemName + " has already been cleansed!");
            return;
        }

        // บันทึกข้อมูลไอเทมที่เลือก
        ItemSessionData.SetCurrentItem(
            id,
            itemName,
            currentSprite.sprite
        );

        Debug.Log("Entering Puzzle: " + itemName);

        SceneManager.LoadScene(sceneName);
    }
}
