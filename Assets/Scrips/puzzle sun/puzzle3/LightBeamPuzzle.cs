using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// มินิเกมส่องแสงผ่านกระจก: ส่องจากจุดเริ่ม (Source) ให้ไปถึงเป้าหมาย (Target)
/// - กระจกทุกบาน "ขยับไม่ได้" แต่หมุนได้ 360° (ลากหมุน / คลิกหมุนทีละขั้น / เลื่อนล้อเมาส์)
/// - ทุกรอบสร้างด่านใหม่แบบสุ่ม และ "รับประกันว่าผ่านได้" (สร้างเส้นทางเฉลยก่อน แล้วจำลองยิงแสงตรวจซ้ำ)
/// - มีกำแพงล้อมเป้าหมายให้เข้าได้ทางเดียว + กำแพงสุ่มกันทางลัด + กระจกหลอก
///
/// วิธีติดตั้ง: วางสคริปต์นี้บน "board" (Image โปร่งใส ติ๊ก Raycast Target ขนาดแนะนำ 1200x680)
/// ที่เป็นลูกของ window แล้วเรียก Open(callback) เหมือนมินิเกมวงเวทย์
/// ทุกอย่าง (กระจก กำแพง แสง) สร้างจากโค้ด ไม่ต้องใช้ภาพเพิ่ม
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class LightBeamPuzzle : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler, IPuzzle
{
    // ---------------- ข้อมูลภายใน ----------------
    private class MirrorData
    {
        public Vector2 pos;
        public float solve;      // มุมเฉลย (องศา, mod 180) ใช้เฉพาะกระจกบนเส้นทางเฉลย
        public bool decoy;       // กระจกหลอก
        public float angle;      // มุมที่ตั้งไว้ (logic)
        public float visual;     // มุมที่แสดง (ไหลเข้าหา angle)
        public RectTransform root;
        public Image line;
        public Image glow;
    }

    private struct Seg
    {
        public Vector2 a, b;
        public Seg(Vector2 a, Vector2 b) { this.a = a; this.b = b; }
    }

    // ---------------- ตั้งค่าใน Inspector ----------------
    [Header("หน้าต่าง")]
    [SerializeField] private GameObject windowRoot;

    [Header("ทดสอบ")]
    [SerializeField] private bool openOnStart = false; // ติ๊กไว้ตอนทดสอบ (window ต้องเปิดอยู่ตอนกด Play)

    [Header("ความยาก / ความยาวเกม")]
    [SerializeField] private int minMirrors = 3;   // กระจกบนเส้นทางเฉลย (ยิ่งเยอะยิ่งนาน)
    [SerializeField] private int maxMirrors = 5;
    [SerializeField] private int decoyMin = 2;     // กระจกหลอก
    [SerializeField] private int decoyMax = 4;
    [SerializeField] private int wallMin = 2;      // กำแพงสุ่ม (นอกจากกำแพงล้อมเป้าหมาย)
    [SerializeField] private int wallMax = 3;

    [Header("กติกา")]
    [SerializeField] private float snapStep = 7.5f;   // ล็อกมุมกระจก (ต้องหาร 90 ลงตัวเมื่อคูณ 2 เช่น 7.5, 5, 3.75)
    [SerializeField] private float tapStep = 15f;     // คลิกเฉย ๆ หมุนทีละกี่องศา
    [SerializeField] private float holdTime = 0.5f;   // แสงต้องถึงเป้านานกี่วินาทีถึงชนะ
    [SerializeField] private float pickRadius = 60f;  // คลิกใกล้กระจกแค่ไหนถึงเลือกได้

    [Header("ขนาด (หน่วยพิกเซลบน board)")]
    [SerializeField] private float edgeMargin = 70f;
    [SerializeField] private float mirrorHalf = 45f;   // ครึ่งความยาวกระจก
    [SerializeField] private float targetRadius = 26f;
    [SerializeField] private float minSegment = 150f;  // ระยะระหว่างกระจกบนเส้นทางเฉลย
    [SerializeField] private float maxSegment = 340f;

    [Header("การหมุนกระจก")]
    [SerializeField] private bool snapOnRelease = true;   // ลากหมุนอิสระ แล้วปล่อยมือค่อยดูดเข้ามุมกริด (ปิด = หมุนอิสระล้วน)
    [SerializeField] private float knobRadius = 35f;      // ห่างจากกลางกระจกเกินนี้ = ลากรอบกระจก / ใกล้กว่านี้ = ลากซ้าย-ขวาหมุนได้
    [SerializeField] private float linearDegPerPixel = 0.6f; // ลากซ้าย-ขวา 1 พิกเซล หมุนกี่องศา

    [Header("ภาพ (ใส่ได้ ไม่ใส่ = ใช้ภาพที่สร้างจากโค้ด)")]
    [SerializeField] private Sprite sourceSprite;               // จุดกำเนิดแสง
    [SerializeField] private Vector2 sourceSpriteSize = new Vector2(70f, 70f);
    [SerializeField] private float sourceSpriteAngleOffset = 0f; // ภาพหันไปทางขวา = 0, หันขึ้น = -90
    [SerializeField] private Sprite targetSprite;               // จุดรับแสง
    [SerializeField] private Vector2 targetSpriteSize = new Vector2(70f, 70f);
    [SerializeField] private Sprite mirrorSprite;               // กระจก
    [SerializeField] private Vector2 mirrorSpriteSize = new Vector2(90f, 90f);
    [SerializeField] private float mirrorSpriteAngleOffset = 0f; // ภาพที่ผิวกระจกเป็นเส้นแนวนอน = 0, เส้นทแยงขึ้น ↗ = -45

    [Header("สี")]
    [SerializeField] private Color beamColor = new Color(1f, 0.9f, 0.35f, 1f);
    [SerializeField] private Color litColor = new Color(1f, 0.85f, 0.4f, 1f);       // กระจกที่แสงตกกระทบ
    [SerializeField] private Color mirrorColor = new Color(0.65f, 0.8f, 1f, 1f);
    [SerializeField] private Color frameColor = new Color(0f, 0f, 0f, 0.35f);
    [SerializeField] private Color wallColor = new Color(0.45f, 0.45f, 0.5f, 1f);
    [SerializeField] private Color auraColor = new Color(0.65f, 0.25f, 1f, 1f);     // เป้าหมายเรือง

    // ---------------- สถานะ ----------------
    private RectTransform area;
    private RectTransform content;
    private RectTransform beamRoot;
    private readonly List<MirrorData> mirrors = new List<MirrorData>();
    private readonly List<Seg> walls = new List<Seg>();
    private readonly List<Seg> borders = new List<Seg>();
    private int bracketWalls = 3;

    private Vector2 sourcePos, targetPos;
    private float sourceAngle;

    private readonly List<Vector2> tracePts = new List<Vector2>();
    private readonly HashSet<int> litIdx = new HashSet<int>();
    private bool traceHit;

    private readonly List<Image> beamGlow = new List<Image>();
    private readonly List<Image> beamCore = new List<Image>();
    private Image spark, targetFill, targetGlow;

    private Sprite discSprite, ringSprite, glowSprite;
    private Texture2D[] texs = new Texture2D[3];

    private MirrorData active;
    private Vector2 downPos;
    private bool dragged;
    private float dragRaw, lastPointerAngle;
    private bool knobEngaged;
    private Vector2 lastLocal;

    private bool running, inputLocked, won;
    private float hold;
    private Action<bool> onFinished;
    private bool opened; // กัน Open On Start ทำงานซ้ำ ถ้ามีตัวอื่นสั่ง Open ไปแล้ว

    // ================= ชีวิตของ component =================
    private void Awake() => Init();

    private void Init()
    {
        if (area != null) return;
        area = (RectTransform)transform;
        discSprite = MakeSprite(0, r => Mathf.Clamp01((1f - r) / 0.04f));
        ringSprite = MakeSprite(1, r => Mathf.Clamp01(Mathf.Min((r - 0.80f) / 0.04f, (1f - r) / 0.04f)));
        glowSprite = MakeSprite(2, r => r >= 1f ? 0f : Mathf.Pow(1f - r, 2.2f));
    }

    private void Start()
    {
        if (openOnStart && !opened) Open(null);
    }

    private void OnDestroy()
    {
        if (discSprite != null) Destroy(discSprite);
        if (ringSprite != null) Destroy(ringSprite);
        if (glowSprite != null) Destroy(glowSprite);
        for (int i = 0; i < texs.Length; i++) if (texs[i] != null) Destroy(texs[i]);
    }

    private void OnValidate()
    {
        if (snapStep > 0f && Mathf.Abs(Mathf.Repeat(90f, snapStep * 2f)) > 0.001f &&
            Mathf.Abs(Mathf.Repeat(90f, snapStep * 2f) - snapStep * 2f) > 0.001f)
            Debug.LogWarning("LightBeamPuzzle: snapStep x 2 ต้องหาร 90 ลงตัว (เช่น 7.5, 5, 3.75) ไม่งั้นด่านอาจแก้ไม่ได้");
    }

    // ================= เรียกจากเกมหลัก =================
    public void Open(Action<bool> callback)
    {
        if (!gameObject.scene.IsValid())
        {
            Debug.LogError("ถูกเรียก Open บน Prefab ใน Project (ไม่ใช่ตัวในซีน) ให้ลาก window จาก Hierarchy มาใช้ หรือสร้างสำเนา (Instantiate) ก่อน");
            callback?.Invoke(false);
            return;
        }
        Init(); // กันกรณี Awake ยังไม่ทำงาน (เช่น window ปิดอยู่)
        onFinished = callback;
        opened = true;
        windowRoot.SetActive(true);
        Time.timeScale = 0f;
        StartPuzzle();
    }

    public void GiveUp() => Finish(false);
    public void Reroll() => StartPuzzle(); // ปุ่ม "สุ่มด่านใหม่" (ไม่บังคับ)

    private void StartPuzzle()
    {
        StopAllCoroutines();
        won = false; hold = 0f; active = null; inputLocked = false; running = false;

        if (content != null) Destroy(content.gameObject);
        beamGlow.Clear(); beamCore.Clear();

        if (!BuildLevel())
        {
            Debug.LogError("LightBeamPuzzle: สร้างด่านไม่สำเร็จ ลองขยายขนาด board (แนะนำ 1200x680) หรือลดจำนวนกระจก");
            Finish(false);
            return;
        }
        BuildVisuals();
        running = true;
    }

    private void Finish(bool success)
    {
        running = false;
        inputLocked = true;
        StopAllCoroutines();
        windowRoot.SetActive(false);
        Time.timeScale = 1f;
        onFinished?.Invoke(success);
    }

    // ================= สร้างด่าน (รับประกันผ่านได้) =================
    private bool BuildLevel()
    {
        int k = UnityEngine.Random.Range(minMirrors, maxMirrors + 1);
        for (int attempt = 0; attempt < 600; attempt++)
        {
            if (attempt > 0 && attempt % 150 == 0 && k > 1) k--; // ผ่อนเงื่อนไขถ้าสุ่มไม่ผ่านนาน ๆ
            if (TryBuild(k)) return true;
        }
        return false;
    }

    private bool TryBuild(int k)
    {
        Vector2 size = area.rect.size;
        Vector2 half = size * 0.5f - Vector2.one * edgeMargin;
        if (half.x < 150f || half.y < 100f) return false;

        float dirStep = snapStep > 0f ? snapStep * 2f : 15f;
        int minTurn = Mathf.Max(1, Mathf.CeilToInt(30f / dirStep));
        int maxTurn = Mathf.Max(minTurn, Mathf.FloorToInt(150f / dirStep));

        // เป้าหมาย + ทิศที่ "เปิด" ของกำแพงล้อม (ต้องส่องเข้าทางนี้เท่านั้น)
        Vector2 T = new Vector2(
            UnityEngine.Random.Range(-half.x + 30f, half.x - 30f),
            UnityEngine.Random.Range(-half.y + 30f, half.y - 30f));
        float ao = 90f * UnityEngine.Random.Range(0, 4);
        Vector2 u = Dir(ao);
        Vector2 v = new Vector2(-u.y, u.x);

        // สร้างเส้นทางเฉลยย้อนจากเป้าหมายกลับไปจุดเริ่ม  P[0]=Source, P[1..k]=กระจก, P[k+1]=Target
        Vector2[] P = new Vector2[k + 2];
        float[] A = new float[k + 1]; // A[i] = ทิศของช่วง P[i] -> P[i+1]
        P[k + 1] = T;
        A[k] = ao + 180f;
        P[k] = T + u * UnityEngine.Random.Range(150f, 280f);
        for (int i = k - 1; i >= 0; i--)
        {
            float turn = dirStep * UnityEngine.Random.Range(minTurn, maxTurn + 1);
            if (UnityEngine.Random.value < 0.5f) turn = -turn;
            A[i] = A[i + 1] + turn;
            P[i] = P[i + 1] - Dir(A[i]) * UnityEngine.Random.Range(minSegment, maxSegment);
        }

        // ทุกจุดต้องอยู่ในกระดาน
        for (int i = 0; i <= k; i++)
            if (Mathf.Abs(P[i].x) > half.x || Mathf.Abs(P[i].y) > half.y) return false;

        // กระจกต้องห่างจากช่วงแสงที่ไม่เกี่ยวข้อง
        for (int j = 1; j <= k; j++)
            for (int i = 0; i <= k; i++)
            {
                if (i == j - 1 || i == j) continue;
                if (PointSegDist(P[j], P[i], P[i + 1]) < 95f) return false;
            }
        for (int i = 1; i <= k; i++) if (PointSegDist(P[0], P[i], P[i + 1]) < 60f) return false;
        for (int i = 0; i < k; i++) if (PointSegDist(T, P[i], P[i + 1]) < 110f) return false;
        for (int a = 0; a <= k; a++)
            for (int b = a + 1; b <= k + 1; b++)
                if ((P[a] - P[b]).magnitude < 110f) return false;

        // เก็บข้อมูล
        mirrors.Clear(); walls.Clear(); borders.Clear();
        sourcePos = P[0]; sourceAngle = A[0]; targetPos = T;

        for (int j = 1; j <= k; j++)
        {
            // มุมกระจก = เส้นแบ่งครึ่งของทิศขาเข้า/ขาออก
            float th = Mathf.Repeat((A[j - 1] + A[j]) * 0.5f, 180f);
            mirrors.Add(new MirrorData { pos = P[j], solve = th, decoy = false });
        }

        // กำแพงล้อมเป้าหมาย (รูปตัว ⊓ เปิดทางด้านที่ทิศ u ชี้)
        const float B = 46f, W = 42f, E = 76f;
        walls.Add(new Seg(T - u * B - v * W, T - u * B + v * W));
        walls.Add(new Seg(T - u * B + v * W, T + u * E + v * W));
        walls.Add(new Seg(T - u * B - v * W, T + u * E - v * W));
        bracketWalls = 3;

        // ขอบกระดาน
        Vector2 hs = size * 0.5f;
        borders.Add(new Seg(new Vector2(-hs.x, -hs.y), new Vector2(hs.x, -hs.y)));
        borders.Add(new Seg(new Vector2(hs.x, -hs.y), new Vector2(hs.x, hs.y)));
        borders.Add(new Seg(new Vector2(hs.x, hs.y), new Vector2(-hs.x, hs.y)));
        borders.Add(new Seg(new Vector2(-hs.x, hs.y), new Vector2(-hs.x, -hs.y)));

        // กระจกหลอก
        int decoys = UnityEngine.Random.Range(decoyMin, decoyMax + 1);
        for (int d = 0; d < decoys; d++)
        {
            for (int tries = 0; tries < 80; tries++)
            {
                Vector2 pos = new Vector2(
                    UnityEngine.Random.Range(-half.x, half.x),
                    UnityEngine.Random.Range(-half.y, half.y));
                bool ok = true;
                for (int i = 0; i <= k && ok; i++) if (PointSegDist(pos, P[i], P[i + 1]) < 90f) ok = false;
                for (int m = 0; m < mirrors.Count && ok; m++) if ((mirrors[m].pos - pos).magnitude < 115f) ok = false;
                for (int w = 0; w < bracketWalls && ok; w++) if (PointSegDist(pos, walls[w].a, walls[w].b) < 80f) ok = false;
                if ((pos - P[0]).magnitude < 100f || (pos - T).magnitude < 100f) ok = false;
                if (!ok) continue;
                mirrors.Add(new MirrorData { pos = pos, decoy = true });
                break;
            }
        }

        // กำแพงสุ่ม (ไม่ตัดเส้นทางเฉลย)
        int nWalls = UnityEngine.Random.Range(wallMin, wallMax + 1);
        for (int n = 0; n < nWalls; n++)
        {
            for (int tries = 0; tries < 120; tries++)
            {
                Vector2 c = new Vector2(
                    UnityEngine.Random.Range(-half.x, half.x),
                    UnityEngine.Random.Range(-half.y, half.y));
                float ang = dirStep * UnityEngine.Random.Range(0, Mathf.RoundToInt(180f / dirStep));
                float len = UnityEngine.Random.Range(90f, 170f);
                Vector2 a = c - Dir(ang) * len * 0.5f, b = c + Dir(ang) * len * 0.5f;
                if (Mathf.Abs(a.x) > half.x || Mathf.Abs(a.y) > half.y || Mathf.Abs(b.x) > half.x || Mathf.Abs(b.y) > half.y) continue;
                bool ok = true;
                for (int i = 0; i <= k && ok; i++) if (SegSegDist(a, b, P[i], P[i + 1]) < 45f) ok = false;
                for (int m = 0; m < mirrors.Count && ok; m++) if (PointSegDist(mirrors[m].pos, a, b) < 95f) ok = false;
                for (int w = 0; w < walls.Count && ok; w++) if (SegSegDist(a, b, walls[w].a, walls[w].b) < 45f) ok = false;
                if (PointSegDist(P[0], a, b) < 60f || PointSegDist(T, a, b) < 110f) ok = false;
                if (!ok) continue;
                walls.Add(new Seg(a, b));
                break;
            }
        }

        // ตรวจซ้ำ: ตั้งกระจกเป็นมุมเฉลยแล้วยิงจริง ต้องถึงเป้าหมายและกระทบกระจกเฉลยครบ
        foreach (var m in mirrors)
        {
            m.angle = m.visual = m.decoy ? 0f : m.solve;
        }
        Trace();
        if (!traceHit) return false;
        int litSolution = 0;
        foreach (int idx in litIdx) if (!mirrors[idx].decoy) litSolution++;
        if (litSolution != k || litIdx.Count != k) return false;

        // สลับมุมเริ่มต้น: ทุกกระจกเฉลยต้องผิดมุม และแสงต้องยังไม่ถึงเป้า
        for (int g = 0; g < 300; g++)
        {
            bool allWrong = true;
            foreach (var m in mirrors)
            {
                m.angle = Snap(UnityEngine.Random.Range(0f, 180f));
                m.visual = m.angle;
                if (!m.decoy && Mathf.Abs(Mathf.DeltaAngle(m.angle * 2f, m.solve * 2f)) < 1f) allWrong = false;
            }
            Trace();
            if (allWrong && !traceHit) break;
        }
        return true;
    }

    // ================= ยิงแสง =================
    private void Trace()
    {
        tracePts.Clear();
        litIdx.Clear();
        traceHit = false;

        Vector2 p = sourcePos;
        Vector2 d = Dir(sourceAngle);
        int last = -1;
        tracePts.Add(p);

        for (int bounce = 0; bounce < 24; bounce++)
        {
            float bestT = float.MaxValue;
            int kind = 0, idx = -1; // 1=กระจก 2=กำแพง 3=เป้า

            for (int i = 0; i < mirrors.Count; i++)
            {
                if (i == last) continue;
                Vector2 dir = Dir(mirrors[i].visual);
                Vector2 a = mirrors[i].pos - dir * mirrorHalf;
                Vector2 b = mirrors[i].pos + dir * mirrorHalf;
                if (RaySeg(p, d, a, b, out float t) && t < bestT) { bestT = t; kind = 1; idx = i; }
            }
            for (int i = 0; i < walls.Count; i++)
                if (RaySeg(p, d, walls[i].a, walls[i].b, out float t) && t < bestT) { bestT = t; kind = 2; }
            for (int i = 0; i < borders.Count; i++)
                if (RaySeg(p, d, borders[i].a, borders[i].b, out float t) && t < bestT) { bestT = t; kind = 2; }
            if (RayCircle(p, d, targetPos, targetRadius, out float tc) && tc < bestT) { bestT = tc; kind = 3; }

            if (kind == 0) break;

            Vector2 hit = p + d * bestT;
            tracePts.Add(hit);

            if (kind == 1)
            {
                Vector2 dir = Dir(mirrors[idx].visual);
                Vector2 n = new Vector2(-dir.y, dir.x);
                d = d - 2f * Vector2.Dot(d, n) * n;
                p = hit;
                last = idx;
                litIdx.Add(idx);
            }
            else
            {
                if (kind == 3) traceHit = true;
                break;
            }
        }
    }

    // ================= Update =================
    private void Update()
    {
        if (!running) return;
        float dt = Time.unscaledDeltaTime;
        float k = 1f - Mathf.Exp(-28f * dt);

        for (int i = 0; i < mirrors.Count; i++)
        {
            var m = mirrors[i];
            float diff = Mathf.DeltaAngle(m.visual, m.angle);
            float kk = (m == active) ? 1f - Mathf.Exp(-70f * dt) : k; // กระจกที่กำลังลากตามเมาส์แทบทันที
            m.visual = Mathf.Abs(diff) < 0.05f ? m.angle : m.visual + diff * kk;
            m.root.localRotation = Quaternion.Euler(0f, 0f, m.visual);
        }

        Trace();
        DrawBeam();

        for (int i = 0; i < mirrors.Count; i++)
        {
            var m = mirrors[i];
            bool lit = litIdx.Contains(i);
            if (mirrorSprite != null) m.line.color = lit ? Color.Lerp(Color.white, litColor, 0.45f) : Color.white;
            else m.line.color = lit ? litColor : mirrorColor;
            float ga = Mathf.MoveTowards(m.glow.color.a, lit ? 0.55f : 0f, dt * 4f);
            m.glow.color = new Color(litColor.r, litColor.g, litColor.b, ga);
        }

        if (!won)
        {
            if (traceHit) hold += dt; else hold = Mathf.Max(0f, hold - dt * 2f);
        }
        float prog = Mathf.Clamp01(hold / Mathf.Max(holdTime, 0.01f));
        targetFill.rectTransform.localScale = Vector3.one * prog;
        Color gc = auraColor; gc.a = prog;
        targetGlow.color = gc;
        targetGlow.rectTransform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 8f) * prog);

        if (!won && hold >= holdTime) StartCoroutine(WinSequence());
    }

    private void DrawBeam()
    {
        int count = tracePts.Count - 1;
        float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 14f);

        for (int i = 0; i < count; i++)
        {
            while (beamCore.Count <= i)
            {
                beamGlow.Add(NewImage("BeamGlow", beamRoot, null, new Color(beamColor.r, beamColor.g, beamColor.b, 0.28f), Vector2.one));
                beamCore.Add(NewImage("BeamCore", beamRoot, null, beamColor, Vector2.one));
            }
            SetSegment(beamGlow[i], tracePts[i], tracePts[i + 1], 18f * pulse);
            SetSegment(beamCore[i], tracePts[i], tracePts[i + 1], 5f);
        }
        for (int i = count; i < beamCore.Count; i++)
        {
            beamGlow[i].gameObject.SetActive(false);
            beamCore[i].gameObject.SetActive(false);
        }

        Vector2 end = tracePts[tracePts.Count - 1];
        spark.rectTransform.anchoredPosition = end;
        float s = (traceHit ? 70f : 42f) * pulse;
        spark.rectTransform.sizeDelta = new Vector2(s, s);
    }

    private IEnumerator WinSequence()
    {
        won = true;
        inputLocked = true;

        var burst = NewImage("Burst", content, ringSprite, litColor, Vector2.one * targetRadius * 2f);
        burst.rectTransform.anchoredPosition = targetPos;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.7f;
            float e = Mathf.Clamp01(t);
            burst.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 7f, e);
            Color c = litColor; c.a = 1f - e;
            burst.color = c;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.5f);
        Finish(true);
    }

    // ================= input =================
    public void OnPointerDown(PointerEventData e)
    {
        if (inputLocked || !running) return;
        Vector2 local = ToLocal(e.position, e.pressEventCamera);
        active = FindMirror(local);
        dragged = false;
        downPos = local;
        lastLocal = local;
        if (active == null) return;

        Vector2 rel = local - active.pos;
        lastPointerAngle = Mathf.Atan2(rel.y, rel.x) * Mathf.Rad2Deg;
        knobEngaged = rel.magnitude >= knobRadius;
        dragRaw = active.angle;
    }

    public void OnDrag(PointerEventData e)
    {
        if (inputLocked || active == null) return;
        Vector2 local = ToLocal(e.position, e.pressEventCamera);
        if ((local - downPos).magnitude > 10f) dragged = true;

        Vector2 rel = local - active.pos;
        if (rel.magnitude >= knobRadius)
        {
            // ไกลจากกลางกระจก: หมุนตามมุมของเมาส์รอบกระจก (เหมือนบิดปุ่ม)
            float cur = Mathf.Atan2(rel.y, rel.x) * Mathf.Rad2Deg;
            if (!knobEngaged) { lastPointerAngle = cur; knobEngaged = true; } // กันมุมกระโดดตอนเปลี่ยนโหมด
            dragRaw += Mathf.DeltaAngle(lastPointerAngle, cur);
            lastPointerAngle = cur;
        }
        else
        {
            // ใกล้กลางกระจก: ลากซ้าย-ขวาก็หมุนได้
            knobEngaged = false;
            dragRaw += (local.x - lastLocal.x) * linearDegPerPixel;
        }
        lastLocal = local;

        active.angle = dragRaw; // หมุนอิสระระหว่างลาก ไม่ล็อกมุม
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!inputLocked && active != null)
        {
            if (!dragged) active.angle = Snap(active.angle + tapStep);        // คลิกเฉย ๆ = หมุนทีละขั้น
            else if (snapOnRelease) active.angle = Snap(dragRaw);              // ปล่อยมือแล้วดูดเข้ามุมกริด
        }
        active = null;
    }

    public void OnScroll(PointerEventData e)
    {
        if (inputLocked || !running) return;
        MirrorData m = FindMirror(ToLocal(e.position, e.enterEventCamera));
        if (m == null) return;
        m.angle += Mathf.Sign(e.scrollDelta.y) * (snapStep > 0f ? snapStep : 5f);
    }

    private MirrorData FindMirror(Vector2 p)
    {
        MirrorData best = null;
        float bestD = pickRadius;
        foreach (var m in mirrors)
        {
            float d = (m.pos - p).magnitude;
            if (d <= bestD) { bestD = d; best = m; }
        }
        return best;
    }

    private Vector2 ToLocal(Vector2 screen, Camera cam)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, cam, out var local);
        return local - area.rect.center; // ศูนย์กลางกระดาน = (0,0)
    }

    private float Snap(float a) => snapStep > 0f ? Mathf.Round(a / snapStep) * snapStep : a;

    // ================= สร้างภาพทั้งหมดด้วยโค้ด =================
    private void BuildVisuals()
    {
        var go = new GameObject("Content", typeof(RectTransform));
        go.transform.SetParent(area, false);
        content = (RectTransform)go.transform;
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
        content.offsetMin = content.offsetMax = Vector2.zero;

        // กำแพง
        for (int i = 0; i < walls.Count; i++)
        {
            var img = NewImage("Wall", content, null, wallColor, Vector2.one);
            SetSegment(img, walls[i].a, walls[i].b, 12f);
        }

        // แสง (อยู่ใต้กระจก)
        var br = new GameObject("Beam", typeof(RectTransform));
        br.transform.SetParent(content, false);
        beamRoot = (RectTransform)br.transform;
        beamRoot.anchorMin = Vector2.zero; beamRoot.anchorMax = Vector2.one;
        beamRoot.offsetMin = beamRoot.offsetMax = Vector2.zero;

        // กระจก (บานหลอกหน้าตาเหมือนบานจริงทุกอย่าง)
        foreach (var m in mirrors)
        {
            var rgo = new GameObject("Mirror", typeof(RectTransform));
            rgo.transform.SetParent(content, false);
            m.root = (RectTransform)rgo.transform;
            m.root.anchorMin = m.root.anchorMax = new Vector2(0.5f, 0.5f);
            m.root.pivot = new Vector2(0.5f, 0.5f);
            m.root.sizeDelta = Vector2.zero;
            m.root.anchoredPosition = m.pos;

            // แสงเรืองด้านหลัง เปิดตอนแสงตกกระทบ
            m.glow = NewImage("Glow", m.root, glowSprite, new Color(litColor.r, litColor.g, litColor.b, 0f), Vector2.one * (mirrorHalf * 3.2f));

            if (mirrorSprite != null)
            {
                m.line = NewImage("MirrorArt", m.root, mirrorSprite, Color.white, mirrorSpriteSize);
                m.line.preserveAspect = true;
                m.line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, mirrorSpriteAngleOffset);
            }
            else
            {
                float side = mirrorHalf * 2f / 1.4142f; // เส้นกระจกเป็นเส้นทแยงมุมของกรอบสี่เหลี่ยม
                var bg = NewImage("Frame", m.root, null, frameColor, new Vector2(side, side));
                bg.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
                m.line = NewImage("Glass", m.root, null, mirrorColor, new Vector2(mirrorHalf * 2f, 8f));
            }
            m.root.localRotation = Quaternion.Euler(0, 0, m.visual);
        }

        // จุดกำเนิดแสง
        if (sourceSprite != null)
        {
            var srcArt = NewImage("Source", content, sourceSprite, Color.white, sourceSpriteSize);
            srcArt.preserveAspect = true;
            srcArt.rectTransform.anchoredPosition = sourcePos;
            srcArt.rectTransform.localRotation = Quaternion.Euler(0f, 0f, sourceAngle + sourceSpriteAngleOffset);
        }
        else
        {
            var src = NewImage("Source", content, discSprite, beamColor, Vector2.one * 34f);
            src.rectTransform.anchoredPosition = sourcePos;
            var barrel = NewImage("Barrel", content, null, beamColor, new Vector2(34f, 12f));
            barrel.rectTransform.anchoredPosition = sourcePos + Dir(sourceAngle) * 18f;
            barrel.rectTransform.localRotation = Quaternion.Euler(0, 0, sourceAngle);
        }

        // จุดรับแสง (เป้าหมาย) ลำดับ: เรือง -> สีที่เติมตามเวลา -> ภาพ/วงแหวน
        targetGlow = NewImage("TargetGlow", content, glowSprite, new Color(auraColor.r, auraColor.g, auraColor.b, 0f), Vector2.one * targetRadius * 6f);
        targetGlow.rectTransform.anchoredPosition = targetPos;
        targetFill = NewImage("TargetFill", content, discSprite, auraColor, Vector2.one * (targetRadius * 2f - 8f));
        targetFill.rectTransform.anchoredPosition = targetPos;
        targetFill.rectTransform.localScale = Vector3.zero;
        if (targetSprite != null)
        {
            var tArt = NewImage("TargetArt", content, targetSprite, Color.white, targetSpriteSize);
            tArt.preserveAspect = true;
            tArt.rectTransform.anchoredPosition = targetPos;
        }
        else
        {
            var ring = NewImage("TargetRing", content, ringSprite, beamColor, Vector2.one * targetRadius * 2f);
            ring.rectTransform.anchoredPosition = targetPos;
        }

        // ประกายปลายลำแสง
        spark = NewImage("Spark", content, glowSprite, new Color(beamColor.r, beamColor.g, beamColor.b, 0.9f), Vector2.one * 42f);
    }

    private Image NewImage(string name, Transform parent, Sprite sprite, Color color, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private void SetSegment(Image img, Vector2 a, Vector2 b, float thickness)
    {
        img.gameObject.SetActive(true);
        var rt = img.rectTransform;
        Vector2 d = b - a;
        rt.anchoredPosition = (a + b) * 0.5f;
        rt.sizeDelta = new Vector2(d.magnitude, thickness);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    // สร้างสไปรต์วงกลม/วงแหวน/แสงเรืองด้วยโค้ด  f(r) -> alpha  โดย r = 0..1 จากกลางไปขอบ
    private Sprite MakeSprite(int slot, Func<float, float> f)
    {
        const int res = 128;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[res * res];
        float c = (res - 1) / 2f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float r = new Vector2(x - c, y - c).magnitude / c;
                px[y * res + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(f(r)) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        texs[slot] = tex;
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
    }

    // ================= คณิตศาสตร์ช่วย =================
    private static Vector2 Dir(float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static bool RaySeg(Vector2 p, Vector2 d, Vector2 a, Vector2 b, out float t)
    {
        t = 0f;
        Vector2 e = b - a;
        float den = Cross(d, e);
        if (Mathf.Abs(den) < 1e-6f) return false;
        Vector2 ap = a - p;
        t = Cross(ap, e) / den;
        float s = Cross(ap, d) / den;
        return t > 0.001f && s >= 0f && s <= 1f;
    }

    private static bool RayCircle(Vector2 p, Vector2 d, Vector2 c, float r, out float t)
    {
        t = 0f;
        Vector2 oc = p - c;
        float b = Vector2.Dot(oc, d);
        float cc = Vector2.Dot(oc, oc) - r * r;
        float disc = b * b - cc;
        if (disc < 0f) return false;
        t = -b - Mathf.Sqrt(disc);
        return t > 0.001f;
    }

    private static float PointSegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-6f) return (p - a).magnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return (p - (a + ab * t)).magnitude;
    }

    private static bool SegIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        Vector2 r = b - a, s = d - c;
        float den = Cross(r, s);
        if (Mathf.Abs(den) < 1e-6f) return false;
        Vector2 ca = c - a;
        float t = Cross(ca, s) / den;
        float u = Cross(ca, r) / den;
        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
    }

    private static float SegSegDist(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (SegIntersect(a, b, c, d)) return 0f;
        return Mathf.Min(Mathf.Min(PointSegDist(a, c, d), PointSegDist(b, c, d)),
                         Mathf.Min(PointSegDist(c, a, b), PointSegDist(d, a, b)));
    }
}