
using UnityEngine;

public class WorkArea : MonoBehaviour
{
    [Header("Juice (optional)")]
    public SpriteRenderer areaVisual;

    [Range(0f, 1f)]
    public float idleAlpha = 0f;

    [Range(0f, 1f)]
    public float highlightAlpha = 0.25f;

    public float breathSpeed = 3f;

    [Range(0f, 0.5f)]
    public float breathAmount = 0.08f;

    public float fadeSpeed = 10f;

    [Header("Pulse")]
    [Range(0f, 1f)]
    public float pulseAlpha = 0.35f;

    public float pulseDecay = 6f;

    private BoxCollider2D areaCollider;

    private Color baseColor;
    private float currentAlpha;
    private float pulse;
    private bool isHighlighted;

    // Item ปัจจุบันบนโต๊ะ
    private GameObject currentItem;

    public GameObject CurrentItem => currentItem;
    public bool IsOccupied => currentItem != null;

    public Bounds AreaBounds
    {
        get
        {
            if (areaCollider == null)
                return new Bounds();

            return areaCollider.bounds;
        }
    }

    // =================================
    // AWAKE
    // =================================

    void Awake()
    {
        areaCollider = GetComponent<BoxCollider2D>();

        if (areaCollider == null)
        {
            Debug.LogError(
                "WorkArea needs BoxCollider2D!"
            );
        }

        if (areaVisual != null)
        {
            baseColor = areaVisual.color;
            currentAlpha = idleAlpha;

            ApplyAlpha(currentAlpha);
        }
    }

    // =================================
    // UPDATE
    // =================================

    void Update()
    {
        if (areaVisual == null)
            return;

        float dt = Time.deltaTime;

        float target = idleAlpha;

        if (isHighlighted)
        {
            target = highlightAlpha +
                     Mathf.Sin(Time.time * breathSpeed)
                     * breathAmount;
        }

        currentAlpha = Mathf.Lerp(
            currentAlpha,
            target,
            1f - Mathf.Exp(-fadeSpeed * dt)
        );

        pulse = Mathf.Lerp(
            pulse,
            0f,
            1f - Mathf.Exp(-pulseDecay * dt)
        );

        ApplyAlpha(currentAlpha + pulse);
    }

    // =================================
    // APPLY ALPHA
    // =================================

    void ApplyAlpha(float alpha)
    {
        Color c = baseColor;

        c.a = Mathf.Clamp01(alpha);

        areaVisual.color = c;
    }

    // =================================
    // REGISTER ITEM
    // =================================

    public bool TryPlaceItem(GameObject item)
    {
        if (item == null)
            return false;

        // ถ้ามี Item อื่นอยู่แล้ว ห้ามวางทับ
        if (currentItem != null && currentItem != item)
        {
            Debug.Log(
                "WorkArea is occupied by: " +
                currentItem.name
            );

            return false;
        }

        currentItem = item;

        Debug.Log(
            "Item registered: " + item.name
        );

        return true;
    }

    // =================================
    // UNREGISTER ITEM
    // =================================

    public void ReleaseItem(GameObject item)
    {
        if (item == null)
            return;

        // ปลดล็อกเฉพาะ Item ที่ลงทะเบียนอยู่
        if (currentItem != item)
            return;

        currentItem = null;

        Debug.Log(
            "Item removed from WorkArea: " + item.name
        );
    }

    // =================================
    // CHECK CURRENT ITEM
    // =================================

    public bool IsCurrentItem(GameObject item)
    {
        return currentItem == item;
    }

    // =================================
    // JUICE
    // =================================

    public void SetHighlight(bool value)
    {
        isHighlighted = value;
    }

    public void Pulse(float strength = 1f)
    {
        pulse = pulseAlpha * strength;
    }

    // =================================
    // AREA CHECK
    // =================================

    public bool IsInsideArea(Bounds itemBounds)
    {
        if (areaCollider == null)
            return false;

        Vector2 itemCenter = itemBounds.center;

        return areaCollider.OverlapPoint(itemCenter);
    }

    // =================================
    // NEAREST POINT OUTSIDE (ดันสินค้าที่ตกทับออกไปรอข้างโต๊ะ)
    // =================================

    public Vector3 GetNearestOutsidePoint(Bounds itemBounds, float margin = 0.15f)
    {
        Bounds area = AreaBounds;
        Vector3 center = itemBounds.center;

        bool right = center.x >= area.center.x;

        center.x = right
            ? area.max.x + itemBounds.extents.x + margin
            : area.min.x - itemBounds.extents.x - margin;

        return center;
    }

    // =================================
    // CLAMP POSITION
    // =================================

    public Vector3 ClampPosition(
        Vector3 position,
        Bounds itemBounds)
    {
        if (areaCollider == null)
            return position;

        Bounds area = AreaBounds;

        float minX = area.min.x + itemBounds.extents.x;
        float maxX = area.max.x - itemBounds.extents.x;

        float minY = area.min.y + itemBounds.extents.y;
        float maxY = area.max.y - itemBounds.extents.y;

        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);

        return position;
    }

    // =================================
    // DEBUG GIZMOS
    // =================================

    void OnDrawGizmos()
    {
        BoxCollider2D col =
            GetComponent<BoxCollider2D>();

        if (col == null)
            return;

        Bounds b = col.bounds;

        Gizmos.color = new Color(
            0.2f, 1f, 0.4f, 0.12f
        );

        Gizmos.DrawCube(
            b.center,
            b.size
        );

        Gizmos.color = new Color(
            0.2f, 1f, 0.4f, 0.9f
        );

        Gizmos.DrawWireCube(
            b.center,
            b.size
        );
    }
}
