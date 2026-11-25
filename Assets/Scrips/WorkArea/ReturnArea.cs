using UnityEngine;

/// <summary>
/// จุดคืนของ: วางสินค้าที่ทำ Puzzle เสร็จได้ครั้งละ 1 ชิ้น
/// (ต้องมี Collider2D แบบ Is Trigger)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ReturnArea : MonoBehaviour
{
    [Header("Juice (optional)")]
    public SpriteRenderer areaVisual;

    [Tooltip("สีแสงที่เรืองขึ้นมา (ทองอ่อน)")]
    public Color glowColor = new Color(1f, 0.82f, 0.35f, 1f);

    [Range(0f, 1f)] public float idleAlpha = 0f;
    [Range(0f, 1f)] public float highlightAlpha = 0.3f;
    public float breathSpeed = 3f;
    [Range(0f, 0.5f)] public float breathAmount = 0.08f;
    public float fadeSpeed = 10f;

    [Header("Pulse")]
    [Range(0f, 1f)] public float pulseAlpha = 0.35f;
    public float pulseDecay = 6f;

    private GameObject currentItem;
    private bool reserved;

    private Color baseColor;
    private float currentAlpha;
    private float pulse;

    public bool IsReserved => reserved;

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (!col.isTrigger)
            Debug.LogWarning("ReturnArea: Collider2D should be 'Is Trigger'.", this);

        if (areaVisual != null)
        {
            baseColor = glowColor;
            currentAlpha = idleAlpha;
            ApplyAlpha(currentAlpha);
        }
    }

    private void Update()
    {
        // สินค้าถูกทำลาย (ลูกค้ารับไปแล้ว) -> ปลดจอง
        if (currentItem == null)
        {
            currentItem = null;
            reserved = false;
        }

        if (areaVisual == null)
            return;

        float dt = Time.deltaTime;

        // ไฮไลต์เมื่อผู้เล่นกำลังลากสินค้าที่ทำเสร็จแล้ว
        bool highlight = false;
        DraggableItem dragged = DraggableItem.DraggedItem;

        if (dragged != null && !reserved)
        {
            ItemPersistenceData p = dragged.GetComponent<ItemPersistenceData>();
            highlight = p != null && ItemSessionData.IsPuzzleSolvedFor(p.instanceID);
        }

        float target = idleAlpha;

        if (highlight)
            target = highlightAlpha + Mathf.Sin(Time.time * breathSpeed) * breathAmount;

        currentAlpha = Mathf.Lerp(currentAlpha, target, 1f - Mathf.Exp(-fadeSpeed * dt));
        pulse = Mathf.Lerp(pulse, 0f, 1f - Mathf.Exp(-pulseDecay * dt));

        ApplyAlpha(currentAlpha + pulse);
    }

    private void ApplyAlpha(float alpha)
    {
        Color c = baseColor;
        c.a = Mathf.Clamp01(alpha);
        areaVisual.color = c;
    }

    public void Pulse(float strength = 1f)
    {
        pulse = pulseAlpha * strength;
    }

    // =====================================
    // TRIGGERS
    // =====================================

    private void OnTriggerEnter2D(Collider2D other) => TryAccept(other);

    private void OnTriggerStay2D(Collider2D other) => TryAccept(other);

    private void TryAccept(Collider2D other)
    {
        if (currentItem != null)
            return;

        DraggableItem draggable = other.GetComponent<DraggableItem>();

        if (draggable == null || draggable.IsDragging)
            return;

        currentItem = draggable.gameObject;
        Pulse();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // ระหว่างลูกค้ากำลังมารับ สินค้าถูกล็อกอยู่ จึงไม่ปลด
        if (currentItem == other.gameObject && !reserved)
            currentItem = null;
    }

    // =====================================
    // QUERY
    // =====================================

    public GameObject GetCurrentItem() => currentItem;

    public bool HasItem() => currentItem != null;

    public bool HasSolvedItem()
    {
        if (currentItem == null)
            return false;

        ItemPersistenceData data = currentItem.GetComponent<ItemPersistenceData>();

        return data != null &&
               !string.IsNullOrEmpty(data.instanceID) &&
               ItemSessionData.IsPuzzleSolvedFor(data.instanceID);
    }

    /// <summary>กันไม่ให้วางชิ้นอื่นทับ ขณะเจ้าของกำลังมารับ</summary>
    public void Reserve()
    {
        if (currentItem != null)
            reserved = true;
    }

    public void ClearItem(GameObject item)
    {
        if (currentItem == item)
        {
            currentItem = null;
            reserved = false;
        }
    }
}