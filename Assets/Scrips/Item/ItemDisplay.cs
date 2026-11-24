
using UnityEngine;

public class ItemDisplay : MonoBehaviour
{
    [Header("Item Reference")]
    public SpriteRenderer spriteRenderer;

    [Header("Floating Animation")]
    public float floatHeight = 0.2f;
    public float floatSpeed = 1.5f;

    [Header("Rotation Animation")]
    public float rotationAmount = 3f;
    public float rotationSpeed = 1f;

    private Vector3 startPosition;
    private Quaternion startRotation;

    void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer =
                GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer =
                GetComponentInChildren<SpriteRenderer>();
        }

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Start()
    {
        LoadItem();
    }

    // =====================================
    // LOAD ITEM FROM SHOP SCENE
    // =====================================

    public void LoadItem()
    {
        if (!ItemSessionData.HasCurrentItem())
        {
            Debug.LogError(
                "ItemDisplay: No Item Data Found!"
            );
            return;
        }

        if (spriteRenderer == null)
        {
            Debug.LogError(
                "ItemDisplay: SpriteRenderer Missing!"
            );
            return;
        }

        Sprite receivedSprite =
            ItemSessionData.CurrentItemSprite;

        if (receivedSprite == null)
        {
            Debug.LogError(
                "ItemDisplay: Current Item Sprite is NULL!"
            );
            return;
        }

        // เปลี่ยนเป็น Sprite ของ Item ที่เลือก
        spriteRenderer.sprite = receivedSprite;

        // ยังไม่สะอาด -> มีออร่าคำสาปรอบสินค้า (สลายเองตอนล้างเสร็จ)
        if (!ItemSessionData.IsPuzzleSolvedFor(ItemSessionData.CurrentItemInstanceID) &&
            GetComponent<CurseAura>() == null)
        {
            gameObject.AddComponent<CurseAura>();
        }

        Debug.Log(
            "Displaying Item: " +
            ItemSessionData.CurrentItemName +
            " | ID: " +
            ItemSessionData.CurrentItemID
        );
    }

    // =====================================
    // ANIMATION
    // =====================================

    void Update()
    {
        if (spriteRenderer == null ||
            spriteRenderer.sprite == null)
            return;

        float floatOffset =
            Mathf.Sin(Time.time * floatSpeed) *
            floatHeight;

        transform.position =
            startPosition + Vector3.up * floatOffset;

        float rotationOffset =
            Mathf.Sin(Time.time * rotationSpeed) *
            rotationAmount;

        transform.rotation =
            startRotation *
            Quaternion.Euler(0, 0, rotationOffset);
    }
}
