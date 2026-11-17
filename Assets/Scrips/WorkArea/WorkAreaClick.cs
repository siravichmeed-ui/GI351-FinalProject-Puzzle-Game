
using UnityEngine;
using UnityEngine.SceneManagement;

public class WorkAreaClick : MonoBehaviour
{
    [Header("References")]
    public WorkArea workArea;
    public GameObject cursedItem;

    [Header("Settings")]
    public string sceneName = "CleansingScene";
    public float stopSpeed = 0.1f;

    [Header("Item Information")]
    public string itemID = "TeddyBear_001";
    public string itemName = "ตุ๊กตาหมีเย็บปะ";

    [Header("Item Appearance")]
    public Sprite cursedSprite;
    public Sprite cleanedSprite;

    private DraggableItem draggableItem;
    private Rigidbody2D itemRb;
    private Collider2D itemCollider;
    private SpriteRenderer itemSpriteRenderer;

    private bool canEnterPuzzle = false;
    private bool isPuzzleSolved = false;

    void Start()
    {
        if (workArea == null)
            workArea = GetComponent<WorkArea>();

        if (cursedItem != null)
        {
            draggableItem = cursedItem.GetComponent<DraggableItem>();
            itemRb = cursedItem.GetComponent<Rigidbody2D>();
            itemCollider = cursedItem.GetComponent<Collider2D>();
            itemSpriteRenderer = cursedItem.GetComponent<SpriteRenderer>();
        }

        // ตรวจสอบสถานะ Puzzle ของไอเทมชิ้นนี้
        CheckItemProgress();
    }

    // =====================================
    // CHECK ITEM PROGRESS
    // =====================================

    void CheckItemProgress()
    {
        isPuzzleSolved =
            ItemSessionData.IsPuzzleSolvedFor(itemID);

        if (itemSpriteRenderer == null)
            return;

        if (isPuzzleSolved)
        {
            if (cleanedSprite != null)
            {
                itemSpriteRenderer.sprite = cleanedSprite;
            }

            Debug.Log(itemName + " has been cleansed!");
        }
        else
        {
            if (cursedSprite != null)
            {
                itemSpriteRenderer.sprite = cursedSprite;
            }

            Debug.Log(itemName + " is still cursed.");
        }
    }

    // =====================================
    // CHECK ITEM POSITION
    // =====================================

    void Update()
    {
        canEnterPuzzle = false;

        // ถ้าแก้ Puzzle แล้ว ห้ามเข้าอีก
        if (isPuzzleSolved)
            return;

        if (workArea == null ||
            cursedItem == null ||
            draggableItem == null ||
            itemRb == null ||
            itemCollider == null)
            return;

        // ตรวจว่าของอยู่บนกระดานสีเขียว
        Bounds area = workArea.AreaBounds;
        Bounds item = itemCollider.bounds;

        bool insideBoard = area.Contains(item.center);

        // ตรวจว่าของตกถึงโต๊ะแล้ว
        bool hasLanded = draggableItem.HasLanded;

        // ตรวจว่าของหยุดนิ่งแล้ว
        bool isStopped =
            itemRb.bodyType == RigidbodyType2D.Dynamic &&
            itemRb.linearVelocity.magnitude <= stopSpeed &&
            Mathf.Abs(itemRb.angularVelocity) <= stopSpeed;

        canEnterPuzzle =
            insideBoard &&
            hasLanded &&
            isStopped;
    }

    // =====================================
    // CLICK GREEN BOARD
    // =====================================

    void OnMouseDown()
    {
        // ตรวจสอบสถานะอีกครั้งก่อนเข้า Puzzle
        if (ItemSessionData.IsPuzzleSolvedFor(itemID))
        {
            Debug.Log(itemName + " has already been cleansed!");
            return;
        }

        if (!canEnterPuzzle)
        {
            Debug.Log("Item is not ready!");
            return;
        }

        if (cursedItem == null)
            return;

        SpriteRenderer itemSprite =
            cursedItem.GetComponent<SpriteRenderer>();

        if (itemSprite == null)
        {
            Debug.LogError("CursedItem has no SpriteRenderer!");
            return;
        }

        // เก็บข้อมูลก่อนเปลี่ยน Scene
        ItemSessionData.SetCurrentItem(
            itemID,
            itemName,
            itemSprite.sprite
        );

        // เข้าสู่ Scene ตรวจสอบคำสาป
        SceneManager.LoadScene(sceneName);
    }
}
