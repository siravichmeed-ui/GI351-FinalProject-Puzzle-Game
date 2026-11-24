using UnityEngine;

/// <summary>
/// ฝุ่นเวทมนตร์ลอยในอากาศ — แสงเล็กๆ ลอยขึ้นช้าๆ ระยิบระยับ
///  - โค้ดล้วน ไม่ต้องมี asset / Particle System (ใช้ได้กับทุก Render Pipeline)
///  - มี 2 ชั้นความลึก: ชั้นหลัง (อยู่หลังของ) กับชั้นหน้า (ลอยทับของนิดหน่อย)
///  - สีเปลี่ยนตามเวลาในวัน (ทองตอนเช้า -> ม่วงตอนค่ำ) ถ้ามี ShopDayManager
///  - ถูกเมาส์ปัดหนี และถูกลมจากลูกค้าที่วิ่งผ่านพัดตาม
///  - MagicDust.Burst(...) ไว้ยิงประกายตอนเกิดเหตุการณ์ (กระดิ่งดัง, จ่ายเงิน, ล้างคำสาปเสร็จ)
/// วิธีใช้: สร้าง GameObject ว่างในซีน ใส่สคริปต์นี้ 1 ตัว (ใส่ทั้งซีนร้านและซีนล้างคำสาปได้)
/// </summary>
public class MagicDust : MonoBehaviour
{
    public static MagicDust Instance { get; private set; }

    [Header("Motes (ฝุ่นแสงที่ลอยอยู่)")]
    [Range(0, 200)] public int count = 45;
    public Vector2 sizeRange = new Vector2(0.06f, 0.2f);
    [Tooltip("ความเร็วลอยขึ้น (หน่วย/วินาที)")]
    public Vector2 riseSpeedRange = new Vector2(0.05f, 0.3f);
    [Tooltip("ความแกว่งส่ายไปมา")]
    public float wander = 0.25f;
    [Tooltip("ลมพัดด้านข้างคงที่ (+ ขวา, - ซ้าย)")]
    public float windX = 0.04f;
    [Range(0f, 1f)] public float sparkleChance = 0.3f;
    public Vector2 alphaRange = new Vector2(0.25f, 0.8f);
    public float twinkleSpeed = 1.6f;

    [Header("Colors")]
    [Tooltip("สีตอนเช้า")]
    public Color dayColor = new Color(1f, 0.85f, 0.5f);
    [Tooltip("สีตอนค่ำ (ใช้เมื่อมี ShopDayManager)")]
    public Color duskColor = new Color(0.72f, 0.5f, 1f);
    public Color accentColor = new Color(0.6f, 1f, 0.9f);
    [Range(0f, 1f)] public float accentChance = 0.25f;

    [Header("Area")]
    [Tooltip("ว่าง = Camera.main")]
    public Camera targetCamera;
    [Tooltip("ขอบนอกจอที่ฝุ่นเกิด/หายไป")]
    public float margin = 0.6f;
    public bool useCustomArea;
    public Rect customArea = new Rect(-8f, -5f, 16f, 10f);

    [Header("Sorting")]
    public string sortingLayerName = "Default";
    [Tooltip("ชั้นหลัง: อยู่หลังของ/ลูกค้า")]
    public int backOrder = -5;
    [Tooltip("ชั้นหน้า: ลอยทับของ")]
    public int frontOrder = 30;
    [Range(0f, 1f)] public float frontFraction = 0.25f;

    [Header("Interaction")]
    public bool reactToMouse = true;
    public float mouseRadius = 1.4f;
    public float mouseForce = 2.5f;
    public float pushDamping = 2.5f;

    [Header("Burst Sparks")]
    public int maxSparks = 80;

    // ---------------------------------------------------------------

    private class Mote
    {
        public Transform t;
        public SpriteRenderer sr;
        public Vector2 pos;
        public Vector2 push;
        public float depth;       // 0.4-1 (ไกล -> ใกล้)
        public float rise;
        public float size;
        public float baseAlpha;
        public float phase;
        public float seed;
        public float whiteMix;
        public bool accent;
        public bool star;
        public float spin;
    }

    private struct Spark
    {
        public bool active;
        public Transform t;
        public SpriteRenderer sr;
        public Vector2 pos;
        public Vector2 vel;
        public float life;
        public float maxLife;
        public float size;
        public Color color;
    }

    private static Sprite starSprite;

    private Mote[] motes = new Mote[0];
    private Spark[] sparks = new Spark[0];
    private int nextSpark;

    private Transform root;
    private Camera cam;
    private Sprite glowSprite;
    private int sortingLayerId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => starSprite = null;

    // =====================================
    // STATIC API
    // =====================================

    /// <summary>ยิงประกายกระจายออกจากจุดหนึ่ง (ไม่มี MagicDust ในซีนก็ไม่ทำอะไร)</summary>
    public static void Burst(Vector2 position, int amount, Color color)
    {
        if (Instance != null)
            Instance.SpawnBurst(position, amount, color);
    }

    /// <summary>พัดฝุ่นรอบๆ ไปทางซ้าย/ขวา (impulse + = ไปทาง +X) — ให้คูณ dt มาด้วย</summary>
    public static void Disturb(Vector2 position, float impulse, float radius)
    {
        if (Instance == null || Mathf.Approximately(impulse, 0f))
            return;

        foreach (Mote m in Instance.motes)
        {
            float d = Vector2.Distance(m.pos, position);

            if (d >= radius)
                continue;

            float f = 1f - d / radius;

            m.push.x += impulse * f * m.depth;
            m.push.y += Mathf.Abs(impulse) * 0.15f * f;
        }
    }

    // =====================================
    // SETUP
    // =====================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        cam = targetCamera != null ? targetCamera : Camera.main;

        glowSprite = CurseVisuals.Glow;

        if (starSprite == null)
            starSprite = BuildStar();

        sortingLayerId = SortingLayer.NameToID(sortingLayerName);

        root = new GameObject("MagicDustRoot").transform;
        root.SetParent(transform, false);

        BuildMotes();
        BuildSparks();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void BuildMotes()
    {
        Rect view = GetView();

        motes = new Mote[count];

        for (int i = 0; i < count; i++)
        {
            Mote m = new Mote();

            m.depth = Random.Range(0.4f, 1f);
            m.rise = Random.Range(riseSpeedRange.x, riseSpeedRange.y);
            m.size = Random.Range(sizeRange.x, sizeRange.y) * Mathf.Lerp(0.6f, 1f, m.depth);
            m.baseAlpha = Random.Range(alphaRange.x, alphaRange.y) * Mathf.Lerp(0.6f, 1f, m.depth);
            m.phase = Random.value * Mathf.PI * 2f;
            m.seed = Random.value * 100f;
            m.whiteMix = Random.Range(0f, 0.5f);
            m.accent = Random.value < accentChance;
            m.star = Random.value < sparkleChance;
            m.spin = Random.Range(-30f, 30f);

            m.pos = new Vector2(
                Random.Range(view.xMin, view.xMax),
                Random.Range(view.yMin, view.yMax)
            );

            bool front = Random.value < frontFraction;

            m.t = NewRenderer(
                "Mote_" + i,
                m.star ? starSprite : glowSprite,
                front ? frontOrder : backOrder,
                out m.sr
            );

            motes[i] = m;
        }
    }

    private void BuildSparks()
    {
        sparks = new Spark[Mathf.Max(maxSparks, 0)];

        for (int i = 0; i < sparks.Length; i++)
        {
            sparks[i].t = NewRenderer("Spark_" + i, starSprite, frontOrder + 5, out sparks[i].sr);
            sparks[i].t.gameObject.SetActive(false);
        }
    }

    private Transform NewRenderer(string objName, Sprite sprite, int order, out SpriteRenderer sr)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(root, false);

        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerID = sortingLayerId;
        sr.sortingOrder = order;

        return go.transform;
    }

    // =====================================
    // UPDATE
    // =====================================

    private void Update()
    {
        // ใช้เวลาจริง: ฝุ่นยังลอยอยู่แม้เกมหยุด (แผงสรุปวัน / Puzzle)
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        float time = Time.unscaledTime;

        if (dt <= 0f)
            return;

        Rect view = GetView();
        Color tint = GetTint();

        bool hasMouse = false;
        Vector2 mouse = Vector2.zero;

        if (reactToMouse && cam != null)
        {
            Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
            mouse = new Vector2(w.x, w.y);
            hasMouse = true;
        }

        float pushDecay = 1f - Mathf.Exp(-pushDamping * dt);

        foreach (Mote m in motes)
        {
            // ลอยขึ้น + ส่ายไปมาด้วย noise
            float nx = Mathf.PerlinNoise(time * 0.15f + m.seed, 0.37f) - 0.5f;
            float ny = Mathf.PerlinNoise(0.91f, time * 0.15f + m.seed) - 0.5f;

            Vector2 vel = new Vector2(
                nx * 2f * wander + windX,
                m.rise + ny * wander * 0.5f
            ) * m.depth;

            vel += m.push;
            m.push = Vector2.Lerp(m.push, Vector2.zero, pushDecay);

            // เมาส์ปัดฝุ่นหนี
            if (hasMouse)
            {
                Vector2 away = m.pos - mouse;
                float dist = away.magnitude;

                if (dist < mouseRadius && dist > 0.001f)
                {
                    float f = 1f - dist / mouseRadius;

                    m.push += away / dist * (mouseForce * f * f * dt * 4f);
                }
            }

            m.pos += vel * dt;

            // หลุดขอบ -> วนกลับมาอีกฝั่ง
            if (m.pos.y > view.yMax + margin)
            {
                m.pos.y = view.yMin - margin;
                m.pos.x = Random.Range(view.xMin, view.xMax);
            }
            else if (m.pos.y < view.yMin - margin)
            {
                m.pos.y = view.yMax + margin;
            }

            if (m.pos.x > view.xMax + margin)
                m.pos.x = view.xMin - margin;
            else if (m.pos.x < view.xMin - margin)
                m.pos.x = view.xMax + margin;

            // ระยิบระยับ
            float tw = 0.55f + 0.45f * Mathf.Sin(time * twinkleSpeed * (0.7f + m.depth * 0.6f) + m.phase);

            // จางตอนเข้า/ออกขอบบนล่าง
            float edge = Mathf.Clamp01(Mathf.Min(
                (m.pos.y - (view.yMin - margin)) / margin,
                ((view.yMax + margin) - m.pos.y) / margin
            ));

            Color c = Color.Lerp(m.accent ? accentColor : tint, Color.white, m.whiteMix);
            c.a = m.baseAlpha * tw * edge;

            m.sr.color = c;

            float s = m.size * (0.85f + 0.15f * Mathf.Sin(time * twinkleSpeed * 1.3f + m.phase));

            m.t.position = new Vector3(m.pos.x, m.pos.y, 0f);
            m.t.localScale = Vector3.one * s;

            if (m.star)
                m.t.Rotate(0f, 0f, m.spin * dt);
        }

        UpdateSparks(dt);
    }

    private void UpdateSparks(float dt)
    {
        for (int i = 0; i < sparks.Length; i++)
        {
            if (!sparks[i].active)
                continue;

            sparks[i].life += dt;

            float n = sparks[i].life / sparks[i].maxLife;

            if (n >= 1f)
            {
                sparks[i].active = false;
                sparks[i].t.gameObject.SetActive(false);
                continue;
            }

            sparks[i].vel *= Mathf.Exp(-2.5f * dt);
            sparks[i].vel.y += 0.5f * dt;
            sparks[i].pos += sparks[i].vel * dt;

            Color c = sparks[i].color;
            c.a = Mathf.Pow(1f - n, 1.5f);

            sparks[i].sr.color = c;
            sparks[i].t.position = new Vector3(sparks[i].pos.x, sparks[i].pos.y, 0f);
            sparks[i].t.localScale = Vector3.one * sparks[i].size * (1f - 0.5f * n);
            sparks[i].t.Rotate(0f, 0f, 180f * dt);
        }
    }

    // =====================================
    // BURST
    // =====================================

    private void SpawnBurst(Vector2 position, int amount, Color color)
    {
        if (sparks.Length == 0)
            return;

        for (int i = 0; i < amount; i++)
        {
            int index = nextSpark;
            nextSpark = (nextSpark + 1) % sparks.Length;

            float ang = Random.value * Mathf.PI * 2f;
            float speed = Random.Range(0.8f, 2.8f);

            sparks[index].active = true;
            sparks[index].pos = position;
            sparks[index].vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed;
            sparks[index].life = 0f;
            sparks[index].maxLife = Random.Range(0.6f, 1.3f);
            sparks[index].size = Random.Range(0.15f, 0.35f);
            sparks[index].color = Color.Lerp(color, Color.white, Random.Range(0f, 0.5f));

            sparks[index].t.position = position;
            sparks[index].t.gameObject.SetActive(true);
        }
    }

    // =====================================
    // HELPERS
    // =====================================

    private Rect GetView()
    {
        if (!useCustomArea)
        {
            if (cam == null)
                cam = targetCamera != null ? targetCamera : Camera.main;

            if (cam != null && cam.orthographic)
            {
                float h = cam.orthographicSize;
                float w = h * cam.aspect;
                Vector3 c = cam.transform.position;

                return new Rect(c.x - w, c.y - h, w * 2f, h * 2f);
            }
        }

        return customArea;
    }

    private Color GetTint()
    {
        float t = ShopDayManager.Instance != null
            ? ShopDayManager.Instance.Progress01
            : 0.3f;

        return Color.Lerp(dayColor, duskColor, t);
    }

    // ประกายรูปดาว 4 แฉก (สร้างด้วยโค้ด)
    private static Sprite BuildStar()
    {
        const int size = 64;

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

                float ax = Mathf.Abs(nx);
                float ay = Mathf.Abs(ny);
                float r = Mathf.Sqrt(nx * nx + ny * ny);

                float a = 0f;

                if (r <= 1f)
                {
                    float arms = Mathf.Exp(-Mathf.Min(ax, ay) * 16f) * (1f - Mathf.Max(ax, ay));
                    float core = Mathf.Clamp01(1f - r * 3f);

                    a = Mathf.Clamp01(arms + core);
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
