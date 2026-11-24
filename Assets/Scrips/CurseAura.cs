using UnityEngine;

/// <summary>
/// ออร่าคำสาปรอบสินค้าที่ "ยังไม่ได้ล้าง" ให้เด่นสะดุดตา — ทำด้วยโค้ดล้วน ไม่ต้องใช้ shader/asset:
///   1) แสงม่วงเรืองด้านหลัง เต้นเป็นจังหวะ
///   2) ขอบม่วงเรืองรอบตัวภาพ (ก็อปสไปรต์ 8 ทิศ ทำเส้นขอบแบบไม่ต้องใช้ shader)
///   3) ควันดำม่วงลอยขึ้นจากตัวสินค้า
///   4) คลื่นวงแหวนแผ่ออกเป็นจังหวะเหมือนหัวใจเต้น
///   5) กลิตช์แวบๆ เป็นครั้งคราว (เงาแดง/ฟ้าสั่น)
/// เมื่อสินค้าล้างคำสาปเสร็จ ออร่าจะสลายไปพร้อมประกายแล้วลบตัวเอง
/// DraggableItem (และ ItemDisplay ในซีนล้างคำสาป) เพิ่มสคริปต์นี้ให้อัตโนมัติ
/// </summary>
[DefaultExecutionOrder(100)]
public class CurseAura : MonoBehaviour
{
    [Header("Overall")]
    public Color auraColor = new Color(0.72f, 0.25f, 1f, 1f);
    public Color smokeColor = new Color(0.32f, 0.08f, 0.5f, 1f);
    [Range(0f, 2f)] public float intensity = 1f;
    public float pulseSpeed = 1.6f;

    [Header("Glow (แสงด้านหลัง)")]
    [Tooltip("ขนาดแสงเทียบกับขนาดสินค้า")]
    public float glowScale = 1.8f;
    public Vector2 glowAlpha = new Vector2(0.25f, 0.6f);

    [Header("Outline (ขอบเรือง)")]
    [Tooltip("ความหนาขอบ (หน่วย world)")]
    public float outlineWidth = 0.05f;
    [Range(0f, 1f)] public float outlineAlpha = 0.8f;

    [Header("Smoke (ควันลอยขึ้น)")]
    [Range(0, 16)] public int wispCount = 7;
    public float wispRise = 0.5f;
    public Vector2 wispLifetime = new Vector2(1.4f, 2.4f);
    public Vector2 wispSize = new Vector2(0.3f, 0.55f);

    [Header("Heartbeat (คลื่นวงแหวน)")]
    [Tooltip("เปิดคลื่นวงแหวนที่แผ่ออกเป็นจังหวะ (ปิดไว้เป็นค่าเริ่มต้น)")]
    public bool showHeartbeatRing = false;
    public Vector2 pulseInterval = new Vector2(2.5f, 5f);
    public float pulseDuration = 0.9f;

    [Header("Glitch (สั่นแวบๆ)")]
    public Vector2 glitchInterval = new Vector2(2.5f, 6f);
    public float glitchDuration = 0.2f;
    public float glitchOffset = 0.08f;

    [Header("Dissolve")]
    public float fadeDuration = 0.7f;

    // ---------------------------------------------------------------

    private class Wisp
    {
        public SpriteRenderer sr;
        public bool active;
        public Vector2 pos;
        public float life, maxLife, size, phase, swayAmp;
        public float respawn;
    }

    private SpriteRenderer itemSr;
    private ItemPersistenceData persistence;

    private Transform root;
    private SpriteRenderer glow;
    private SpriteRenderer ring;
    private readonly SpriteRenderer[] outline = new SpriteRenderer[8];
    private readonly SpriteRenderer[] ghosts = new SpriteRenderer[2];
    private Wisp[] wisps;

    private float phase;
    private float checkTimer;
    private float fade;
    private bool fading;
    private bool seenCursed;

    private float pulseTimer;
    private float ringT = -1f;
    private float glitchTimer;
    private float glitchLeft;

    // =====================================
    // SETUP
    // =====================================

    private void Awake()
    {
        itemSr = GetComponent<SpriteRenderer>();

        if (itemSr == null)
            itemSr = GetComponentInChildren<SpriteRenderer>();

        if (itemSr == null)
        {
            Destroy(this);
            return;
        }

        persistence = GetComponent<ItemPersistenceData>();

        phase = Random.value * Mathf.PI * 2f;

        pulseTimer = Random.Range(0.5f, pulseInterval.y);
        glitchTimer = Random.Range(glitchInterval.x, glitchInterval.y);

        BuildVisuals();
    }

    private void BuildVisuals()
    {
        root = new GameObject("CurseAura_" + name).transform;

        glow = NewRenderer("Glow", CurseVisuals.Glow);
        ring = NewRenderer("Ring", CurseVisuals.Ring);
        ring.enabled = false;

        for (int i = 0; i < outline.Length; i++)
            outline[i] = NewRenderer("Outline_" + i, itemSr.sprite);

        for (int i = 0; i < ghosts.Length; i++)
        {
            ghosts[i] = NewRenderer("Ghost_" + i, itemSr.sprite);
            ghosts[i].enabled = false;
        }

        wisps = new Wisp[wispCount];

        for (int i = 0; i < wispCount; i++)
        {
            wisps[i] = new Wisp
            {
                sr = NewRenderer("Wisp_" + i, CurseVisuals.Glow),
                respawn = Random.Range(0f, wispLifetime.y)
            };

            wisps[i].sr.enabled = false;
        }
    }

    private SpriteRenderer NewRenderer(string objName, Sprite sprite)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(root, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;

        return sr;
    }

    private void OnDestroy()
    {
        if (root != null)
            Destroy(root.gameObject);
    }

    // =====================================
    // UPDATE
    // =====================================

    private void LateUpdate()
    {
        if (itemSr == null)
            return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        float time = Time.unscaledTime;

        checkTimer -= dt;

        if (checkTimer <= 0f)
        {
            checkTimer = 0.25f;
            CheckSolved();

            if (this == null)
                return;
        }

        fade = fading
            ? Mathf.MoveTowards(fade, 0f, dt / Mathf.Max(fadeDuration, 0.01f))
            : Mathf.MoveTowards(fade, 1f, dt * 2f);

        if (fading && fade <= 0f)
        {
            Destroy(this);
            return;
        }

        float k = intensity * fade;

        Bounds b = itemSr.bounds;
        Vector3 center = b.center;
        float maxDim = Mathf.Max(b.size.x, b.size.y);
        float z = itemSr.transform.position.z;

        int layer = itemSr.sortingLayerID;
        int order = itemSr.sortingOrder;

        float beat = 0.5f + 0.5f * Mathf.Sin(time * pulseSpeed + phase);

        UpdateGlow(center, maxDim, z, layer, order, beat, k);
        UpdateOutline(z, layer, order, time, k);
        UpdateRing(center, maxDim, z, layer, order, dt, k);
        UpdateGlitch(z, layer, order, dt);
        UpdateWisps(b, z, layer, order, dt, k);
    }

    private void UpdateGlow(Vector3 center, float maxDim, float z, int layer, int order, float beat, float k)
    {
        glow.sortingLayerID = layer;
        glow.sortingOrder = order - 3;

        glow.transform.position = new Vector3(center.x, center.y, z);
        glow.transform.localScale = Vector3.one * maxDim * glowScale * (0.95f + 0.1f * beat);

        Color c = auraColor;
        c.a = Mathf.Lerp(glowAlpha.x, glowAlpha.y, beat) * k;
        glow.color = c;
    }

    private void UpdateOutline(float z, int layer, int order, float time, float k)
    {
        Transform it = itemSr.transform;

        float pulse = 0.75f + 0.25f * Mathf.Sin(time * pulseSpeed * 1.4f + phase);

        for (int i = 0; i < outline.Length; i++)
        {
            SpriteRenderer sr = outline[i];

            float ang = i * Mathf.PI * 2f / outline.Length;
            Vector3 offset = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * outlineWidth * pulse;

            sr.sprite = itemSr.sprite;
            sr.flipX = itemSr.flipX;
            sr.flipY = itemSr.flipY;
            sr.sortingLayerID = layer;
            sr.sortingOrder = order - 1;

            sr.transform.position = it.position + offset;
            sr.transform.rotation = it.rotation;
            sr.transform.localScale = it.lossyScale;

            Color c = auraColor;
            c.a = outlineAlpha * pulse * k;
            sr.color = c;
        }
    }

    private void UpdateRing(Vector3 center, float maxDim, float z, int layer, int order, float dt, float k)
    {
        if (!showHeartbeatRing)
        {
            ring.enabled = false;
            ringT = -1f;
            return;
        }

        pulseTimer -= dt;

        if (pulseTimer <= 0f && ringT < 0f && !fading)
        {
            ringT = 0f;
            pulseTimer = Random.Range(pulseInterval.x, pulseInterval.y);
        }

        if (ringT < 0f)
        {
            ring.enabled = false;
            return;
        }

        ringT += dt / Mathf.Max(pulseDuration, 0.05f);

        if (ringT >= 1f)
        {
            ringT = -1f;
            ring.enabled = false;
            return;
        }

        float eased = 1f - Mathf.Pow(1f - ringT, 3f);

        ring.enabled = true;
        ring.sortingLayerID = layer;
        ring.sortingOrder = order - 2;

        ring.transform.position = new Vector3(center.x, center.y, z);
        ring.transform.localScale = Vector3.one * Mathf.Lerp(maxDim * 0.8f, maxDim * 2.4f, eased);

        Color c = auraColor;
        c.a = (1f - ringT) * 0.7f * k;
        ring.color = c;
    }

    private void UpdateGlitch(float z, int layer, int order, float dt)
    {
        glitchTimer -= dt;

        if (glitchTimer <= 0f && glitchLeft <= 0f && !fading)
        {
            glitchLeft = glitchDuration;
            glitchTimer = Random.Range(glitchInterval.x, glitchInterval.y);
        }

        bool on = glitchLeft > 0f;

        if (on)
            glitchLeft -= dt;

        Transform it = itemSr.transform;

        for (int i = 0; i < ghosts.Length; i++)
        {
            SpriteRenderer sr = ghosts[i];

            sr.enabled = on;

            if (!on)
                continue;

            float dir = i == 0 ? -1f : 1f;

            Vector3 offset = new Vector3(
                dir * glitchOffset * Random.Range(0.5f, 1.5f),
                Random.Range(-0.3f, 0.3f) * glitchOffset,
                0f
            );

            sr.sprite = itemSr.sprite;
            sr.flipX = itemSr.flipX;
            sr.flipY = itemSr.flipY;
            sr.sortingLayerID = layer;
            sr.sortingOrder = order + 1;

            sr.transform.position = it.position + offset;
            sr.transform.rotation = it.rotation;
            sr.transform.localScale = it.lossyScale;

            sr.color = i == 0
                ? new Color(1f, 0.2f, 0.5f, 0.45f)
                : new Color(0.2f, 1f, 1f, 0.45f);
        }
    }

    private void UpdateWisps(Bounds b, float z, int layer, int order, float dt, float k)
    {
        for (int i = 0; i < wisps.Length; i++)
        {
            Wisp w = wisps[i];

            if (!w.active)
            {
                w.sr.enabled = false;

                if (fading)
                    continue;

                w.respawn -= dt;

                if (w.respawn > 0f)
                    continue;

                w.active = true;
                w.life = 0f;
                w.maxLife = Random.Range(wispLifetime.x, wispLifetime.y);
                w.size = Random.Range(wispSize.x, wispSize.y);
                w.phase = Random.value * Mathf.PI * 2f;
                w.swayAmp = Random.Range(0.15f, 0.35f);

                w.pos = new Vector2(
                    Random.Range(b.min.x, b.max.x),
                    Random.Range(b.min.y, b.min.y + b.size.y * 0.6f)
                );
            }

            w.life += dt;

            float n = w.life / w.maxLife;

            if (n >= 1f)
            {
                w.active = false;
                w.respawn = Random.Range(0f, 0.6f);
                w.sr.enabled = false;
                continue;
            }

            w.pos.y += wispRise * dt * (1f + n * 0.5f);
            w.pos.x += Mathf.Cos(Time.unscaledTime * 1.5f + w.phase) * w.swayAmp * dt;

            w.sr.enabled = true;
            w.sr.sortingLayerID = layer;
            // ครึ่งหนึ่งอยู่หลังตัวสินค้า ครึ่งหนึ่งลอยทับ
            w.sr.sortingOrder = (i % 2 == 0) ? order - 1 : order + 1;

            w.sr.transform.position = new Vector3(w.pos.x, w.pos.y, z);
            w.sr.transform.localScale = Vector3.one * w.size * (0.6f + 0.9f * n);

            Color c = Color.Lerp(smokeColor, auraColor, (1f - n) * 0.35f);
            c.a = Mathf.Sin(n * Mathf.PI) * 0.55f * k;
            w.sr.color = c;
        }
    }

    // =====================================
    // STATE
    // =====================================

    private void CheckSolved()
    {
        bool solved = IsSolved();

        if (!solved)
        {
            seenCursed = true;
            return;
        }

        // เริ่มซีนมาก็สะอาดอยู่แล้ว -> ไม่ต้องมีออร่า (เงียบๆ)
        if (!seenCursed)
        {
            Destroy(this);
            return;
        }

        if (!fading)
        {
            fading = true;

            Vector3 center = itemSr != null ? itemSr.bounds.center : transform.position;
            MagicDust.Burst(center, 22, auraColor);
        }
    }

    private bool IsSolved()
    {
        if (persistence != null)
        {
            return !string.IsNullOrEmpty(persistence.instanceID) &&
                   ItemSessionData.IsPuzzleSolvedFor(persistence.instanceID);
        }

        // ซีนล้างคำสาป (ItemDisplay ไม่มี ItemPersistenceData)
        return ItemSessionData.HasCurrentItem() &&
               ItemSessionData.IsPuzzleSolvedFor(ItemSessionData.CurrentItemInstanceID);
    }
}
