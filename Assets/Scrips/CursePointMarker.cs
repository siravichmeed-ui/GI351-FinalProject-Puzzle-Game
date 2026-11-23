using UnityEngine;

/// <summary>สร้างสไปรต์วงกลมเรืองแสงด้วยโค้ด (ไม่ต้องมี asset)</summary>
public static class CurseVisuals
{
    private static Sprite core, glow, ring;

    public static Sprite Core => core != null ? core
        : (core = Make(64, r => Mathf.Clamp01((0.62f - r) / 0.08f)));

    public static Sprite Glow => glow != null ? glow
        : (glow = Make(64, r => r >= 1f ? 0f : Mathf.Pow(1f - r, 2.2f)));

    public static Sprite Ring => ring != null ? ring
        : (ring = Make(128, r => Mathf.Clamp01(Mathf.Min((r - 0.86f) / 0.04f, (1f - r) / 0.04f))));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        core = glow = ring = null;
    }

    private static Sprite Make(int size, System.Func<float, float> alphaByRadius)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny);

                float a = r <= 1f ? Mathf.Clamp01(alphaByRadius(r)) : 0f;

                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        // pixelsPerUnit = size -> สไปรต์กว้าง 1 unit พอดี
        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
    }
}

/// <summary>
/// จุดคำสาป 1 จุดบนสินค้า — เรืองแสงเต้นตุบๆ, โผล่แบบเด้ง, ขยายเมื่อเอาเมาส์ชี้
/// ใช้เวลาแบบ unscaled เพราะ Puzzle ตั้ง Time.timeScale = 0
/// </summary>
public class CursePointMarker : MonoBehaviour
{
    public int Index { get; private set; }
    public bool IsSolved { get; private set; }

    private CurseCleansingController owner;
    private Transform follow;
    private Vector3 localOffset;

    private SpriteRenderer coreSr, glowSr, ringSr;
    private CircleCollider2D col;

    private float size;
    private float vis;
    private float showDelay;
    private bool wantShow;
    private float hover, hoverTarget;
    private float phase;
    private float shakeTimer;

    public static CursePointMarker Create(
        CurseCleansingController owner,
        int index,
        Transform follow,
        Vector3 localOffset,
        Color color,
        float size,
        int sortingLayerId,
        int sortingOrder,
        Sprite customCore)
    {
        GameObject go = new GameObject("CursePoint_" + index);
        CursePointMarker m = go.AddComponent<CursePointMarker>();

        m.owner = owner;
        m.Index = index;
        m.follow = follow;
        m.localOffset = localOffset;
        m.size = size;
        m.phase = index * 1.7f;

        m.glowSr = m.MakeRenderer("Glow", CurseVisuals.Glow, color, 2.6f, sortingLayerId, sortingOrder);
        m.ringSr = m.MakeRenderer("Ring", CurseVisuals.Ring, color, 1.5f, sortingLayerId, sortingOrder + 1);

        Color coreColor = Color.Lerp(color, Color.white, 0.55f);
        m.coreSr = m.MakeRenderer(
            "Core",
            customCore != null ? customCore : CurseVisuals.Core,
            coreColor, 1f, sortingLayerId, sortingOrder + 2);

        m.col = go.AddComponent<CircleCollider2D>();
        m.col.radius = 0.7f; // กว้างกว่าภาพเล็กน้อย คลิกง่าย
        m.col.enabled = false;

        go.transform.localScale = Vector3.zero;

        return m;
    }

    private SpriteRenderer MakeRenderer(
        string childName, Sprite sprite, Color color, float scale,
        int sortingLayerId, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        child.transform.localScale = Vector3.one * scale;

        SpriteRenderer sr = child.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingLayerID = sortingLayerId;
        sr.sortingOrder = sortingOrder;
        sr.enabled = false;

        return sr;
    }

    // =====================================
    // CONTROL
    // =====================================

    public void Show(float delay)
    {
        if (IsSolved)
            return;

        wantShow = true;
        showDelay = delay;
    }

    public void Hide()
    {
        wantShow = false;
        hoverTarget = 0f;
    }

    public void MarkSolved()
    {
        IsSolved = true;
        wantShow = false;
        hoverTarget = 0f;
    }

    public void Shake()
    {
        shakeTimer = 0.3f;
    }

    // =====================================
    // UPDATE
    // =====================================

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float time = Time.unscaledTime;

        Vector3 pos = follow != null
            ? follow.TransformPoint(localOffset)
            : transform.position;

        if (shakeTimer > 0f)
        {
            shakeTimer -= dt;
            pos += (Vector3)(Random.insideUnitCircle * 0.08f * Mathf.Clamp01(shakeTimer / 0.3f));
        }

        transform.position = pos;

        if (showDelay > 0f)
            showDelay -= dt;

        float target = (wantShow && showDelay <= 0f) ? 1f : 0f;

        vis = Mathf.MoveTowards(vis, target, dt * (target > vis ? 3.2f : 6f));

        hover = Mathf.Lerp(hover, hoverTarget, 1f - Mathf.Exp(-14f * dt));

        bool visible = vis > 0.001f;

        coreSr.enabled = visible;
        glowSr.enabled = visible;
        ringSr.enabled = visible;

        col.enabled = wantShow && !IsSolved && vis > 0.7f;

        if (!visible)
            return;

        float pulse = 1f + Mathf.Sin(time * 4f + phase) * 0.07f;
        float s = size * EaseOutBack(vis) * pulse * (1f + 0.25f * hover);

        transform.localScale = Vector3.one * Mathf.Max(s, 0f);

        float glowPulse = (Mathf.Sin(time * 3f + phase) + 1f) * 0.5f;

        Color g = glowSr.color;
        g.a = Mathf.Lerp(0.35f, 0.75f, glowPulse) * vis;
        glowSr.color = g;

        Color r = ringSr.color;
        r.a = (0.5f + 0.5f * hover) * vis;
        ringSr.color = r;

        ringSr.transform.localRotation = Quaternion.Euler(0f, 0f, time * 45f);
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;

        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // =====================================
    // MOUSE (legacy physics raycast เหมือน DraggableItem)
    // =====================================

    private void OnMouseEnter()
    {
        if (owner != null && owner.CanInteract)
            hoverTarget = 1f;
    }

    private void OnMouseExit()
    {
        hoverTarget = 0f;
    }

    private void OnMouseDown()
    {
        if (owner != null)
            owner.OnPointClicked(this);
    }
}
