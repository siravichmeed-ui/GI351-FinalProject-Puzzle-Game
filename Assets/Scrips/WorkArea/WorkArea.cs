using UnityEngine;

public class WorkArea : MonoBehaviour
{
    [Header("Juice (optional)")]
    [Tooltip("Sprite ที่ใช้เรืองแสงบอกพื้นที่ทำงาน (ถ้าไม่ใส่จะข้ามไป)")]
    public SpriteRenderer areaVisual;

    [Range(0f, 1f)] public float idleAlpha = 0f;
    [Range(0f, 1f)] public float highlightAlpha = 0.25f;

    [Tooltip("ความเร็วและความแรงของการ 'หายใจ' ตอนไฮไลต์")]
    public float breathSpeed = 3f;
    [Range(0f, 0.5f)] public float breathAmount = 0.08f;

    public float fadeSpeed = 10f;

    [Header("Pulse (กระพริบตอนของตกลงมา)")]
    [Range(0f, 1f)] public float pulseAlpha = 0.35f;
    public float pulseDecay = 6f;

    private BoxCollider2D areaCollider;

    private Color baseColor;
    private float currentAlpha;
    private float pulse;
    private bool isHighlighted;

    public Bounds AreaBounds
    {
        get { return areaCollider.bounds; }
    }

    void Awake()
    {
        areaCollider = GetComponent<BoxCollider2D>();

        if (areaCollider == null)
        {
            Debug.LogError("WorkArea needs BoxCollider2D!");
        }

        if (areaVisual != null)
        {
            baseColor = areaVisual.color;
            currentAlpha = idleAlpha;
            ApplyAlpha(currentAlpha);
        }
    }

    void Update()
    {
        if (areaVisual == null) return;

        float dt = Time.deltaTime;

        float target = idleAlpha;

        if (isHighlighted)
        {
            target = highlightAlpha +
                     Mathf.Sin(Time.time * breathSpeed) * breathAmount;
        }

        currentAlpha = Mathf.Lerp(
            currentAlpha, target,
            1f - Mathf.Exp(-fadeSpeed * dt)
        );

        pulse = Mathf.Lerp(pulse, 0f, 1f - Mathf.Exp(-pulseDecay * dt));

        ApplyAlpha(currentAlpha + pulse);
    }

    void ApplyAlpha(float alpha)
    {
        Color c = baseColor;
        c.a = Mathf.Clamp01(alpha);
        areaVisual.color = c;
    }

    // =================================
    // PUBLIC JUICE API
    // =================================

    // เรียกตอนหยิบ = true, ตอนปล่อย = false
    public void SetHighlight(bool value)
    {
        isHighlighted = value;
    }

    // เรียกตอนของตกกระทบพื้นที่
    public void Pulse(float strength = 1f)
    {
        pulse = pulseAlpha * strength;
    }

    // =================================
    // ORIGINAL API
    // =================================

    // ตรวจสอบว่าวัตถุอยู่ในพื้นที่หรือไม่
    public bool IsInsideArea(Bounds itemBounds)
    {
        Bounds area = AreaBounds;

        return itemBounds.min.x >= area.min.x &&
               itemBounds.max.x <= area.max.x &&
               itemBounds.min.y >= area.min.y &&
               itemBounds.max.y <= area.max.y;
    }

    // จำกัดตำแหน่งไม่ให้วัตถุออกนอกพื้นที่
    public Vector3 ClampPosition(
        Vector3 position,
        Bounds itemBounds)
    {
        Bounds area = AreaBounds;

        float minX = area.min.x + itemBounds.extents.x;
        float maxX = area.max.x - itemBounds.extents.x;

        float minY = area.min.y + itemBounds.extents.y;
        float maxY = area.max.y - itemBounds.extents.y;

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);

        return position;
    }

    // วาดกรอบพื้นที่ใน Scene view ให้เห็นง่ายๆ
    void OnDrawGizmos()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Bounds b = col.bounds;

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.12f);
        Gizmos.DrawCube(b.center, b.size);

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
}