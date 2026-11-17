
using UnityEngine;

public class ItemDisplay : MonoBehaviour
{
    [Header("Floating Animation")]
    public float floatHeight = 0.2f;
    public float floatSpeed = 1.5f;

    [Header("Rotation Animation")]
    public float rotationAmount = 3f;
    public float rotationSpeed = 1f;

    private SpriteRenderer spriteRenderer;

    private Vector3 startPosition;
    private Quaternion startRotation;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    void Start()
    {
        // ดึงข้อมูลจาก Shop Scene
        if (!ItemSessionData.HasCurrentItem())
        {
            Debug.LogError("No Item Data Found!");
            return;
        }

        spriteRenderer.sprite = ItemSessionData.CurrentItemSprite;

        Debug.Log(
            "Displaying Item: " +
            ItemSessionData.CurrentItemName
        );
    }

    void Update()
    {
        if (spriteRenderer == null ||
            spriteRenderer.sprite == null)
            return;

        // ลอยขึ้นลง
        float floatOffset =
            Mathf.Sin(Time.time * floatSpeed) * floatHeight;

        transform.position = startPosition +
            Vector3.up * floatOffset;

        // เอียงซ้ายขวาเบา ๆ
        float rotationOffset =
            Mathf.Sin(Time.time * rotationSpeed) * rotationAmount;

        transform.rotation =
            startRotation * Quaternion.Euler(0, 0, rotationOffset);
    }
}
