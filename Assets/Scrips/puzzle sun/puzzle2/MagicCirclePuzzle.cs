using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// มินิเกมหมุนวงล้อวงเวทย์ 3 วง ให้ลายตรงกัน (เวอร์ชันอัปเกรด 2 + รองรับ PuzzleManager)
/// - มี N รูปแบบ (Pattern) สุ่มทุกครั้งที่เปิดเกม
/// - สคริปต์นี้วางบน "ringArea" (Image โปร่งใส ที่ติ๊ก Raycast Target ไว้ ขนาดครอบวงเวทย์ทั้งหมด)
/// - วงล้อทั้ง 3 เป็น Image ลูกของ ringArea ตั้ง Anchor/Pivot ไว้กลาง และต้องปิด Raycast Target
/// - ผู้เล่นลากเมาส์/นิ้วรอบจุดศูนย์กลาง ระบบเลือกวงที่หมุนจาก "ระยะห่างจากศูนย์กลาง"
/// - ภาพทุกชั้นตัดมาจากภาพเต็มภาพเดียวกัน ดังนั้น "ถูกต้อง" = ทุกวงมีมุมสัมพัทธ์เท่ากัน (หมุน 0 องศา)
///
/// ใหม่ในเวอร์ชันนี้
///  - ไฮไลต์ม่วงกระพริบเบาๆ "ตามแนวแกน Y" (ด้านบนของวง) เมื่อวงตรงกับวงใหญ่
///  - ตอนชนะ: วงเก่าหมุนออก+จางหายก่อน -> ภาพเต็ม fade เข้า -> เส้นดำกลายเป็นแสงนีออนเรืองๆ
///  - implement IPuzzle ให้ PuzzleManager เรียกใช้ได้
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MagicCirclePuzzle : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IPuzzle
{
    [Serializable]
    public class Pattern
    {
        public string name;
        public Sprite full;   // ภาพเต็ม (ใช้โชว์ตอนชนะ) ไม่ใส่ก็ได้
        public Sprite large;  // วงใหญ่
        public Sprite mid;    // วงกลาง
        public Sprite small;  // วงเล็ก
    }

    [Header("หน้าต่าง")]
    [SerializeField] private GameObject windowRoot;

    [Header("วงล้อ (Image)")]
    [SerializeField] private Image largeRing;
    [SerializeField] private Image midRing;
    [SerializeField] private Image smallRing;
    [SerializeField] private Image fullImage; // ภาพเต็ม จะเข้ามาหลังวงเก่าหายไป แล้วกลายเป็นนีออน

    [Header("รูปแบบ (ใส่ 2 อัน)")]
    [SerializeField] private Pattern[] patterns;

    [Header("รัศมีเลือกวง (หน่วย local ของ ringArea)")]
    [SerializeField] private float smallRadius = 150f;  // ในรัศมีนี้ = วงเล็ก
    [SerializeField] private float midRadius = 320f;    // ถัดออกมา = วงกลาง
    [SerializeField] private float largeRadius = 520f;  // ถัดออกมา = วงใหญ่

    [Header("ทดสอบ")]
    [SerializeField] private bool openOnStart = false; // ติ๊กไว้ตอนทดสอบ กด Play แล้วเริ่มเกมเลย

    [Header("กติกา")]
    [SerializeField] private bool canRotateLarge = false; // วงใหญ่เป็นฐานตายตัว
    [SerializeField] private bool canRotateMid = true;
    [SerializeField] private bool canRotateSmall = true;
    [SerializeField] private float tolerance = 3f;        // คลาดเคลื่อนที่ยอมรับ (องศา)
    [SerializeField] private float snapStep = 5f;         // ล็อกมุมทีละกี่องศา (0 = ไม่ล็อก)

    [Header("Highlight วงที่จับ")]
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color highlightColor = new Color(1f, 0.93f, 0.7f, 1f);
    [SerializeField] private Color dimColor = new Color(0.6f, 0.6f, 0.65f, 1f);
    [SerializeField] private float highlightScale = 1.03f;
    [SerializeField] private float visualSmooth = 12f;     // ยิ่งมากยิ่งเปลี่ยนสี/ขนาดเร็ว
    [SerializeField] private float lockPulse = 0.06f;      // ขนาดที่เด้งตอนล็อกตรงกับวงใหญ่

    [Header("ไฮไลต์ม่วงตอนวงตรงกัน (แสงเรียงตามแกน Y ด้านบนของวง)")]
    [SerializeField] private bool showAlignHint = true;
    [SerializeField] private Color hintColor = new Color(0.72f, 0.35f, 1f, 1f);
    [SerializeField] private float hintWidth = 46f;
    [SerializeField, Range(0f, 1f)] private float hintMaxAlpha = 0.55f;
    [SerializeField] private float hintBlinkSpeed = 3f;

    [Header("เสียง + Inertia")]
    [SerializeField] private AudioSource audioSource;      // ไม่ใส่ = สร้างให้เอง
    [SerializeField] private AudioClip tickClip;           // คลิกทุกขั้น snap
    [SerializeField] private AudioClip lockClip;           // ตอนวงตรงกับวงใหญ่
    [SerializeField] private AudioClip winClip;            // ตอนชนะ
    [SerializeField, Range(0f, 1f)] private float tickVolume = 0.5f;
    [SerializeField] private float tickPitchRandom = 0.06f;
    [SerializeField] private bool useInertia = true;
    [SerializeField] private float inertiaFriction = 4f;   // ยิ่งมากยิ่งหยุดเร็ว
    [SerializeField] private float minInertiaSpeed = 25f;  // องศา/วินาที ต่ำกว่านี้ถือว่าหยุด
    [SerializeField] private float maxInertiaSpeed = 900f;

    [Header("ลำดับเอฟเฟกต์ตอนชนะ")]
    [SerializeField] private float ringsClearDuration = 0.5f; // 1) วงเก่าหมุนออก + จางหาย
    [SerializeField] private float fullFadeDuration = 0.6f;   // 2) ภาพเต็มค่อยๆ เข้ามา
    [SerializeField] private float neonDuration = 1.2f;       // 3) เส้นดำ -> แสงนีออน
    [SerializeField] private float winHoldTime = 1.6f;        // ค้างให้ดูนีออนก่อนปิดหน้าต่าง
    [SerializeField] private Color winGlowColor = new Color(0.9f, 0.75f, 1f, 1f); // แสงวาบตอนวงเก่าหาย
    [SerializeField] private Image rippleImage;               // ไม่ใส่ก็ได้ รูปวงแหวน/แสง ขยายตอนนีออนติด
    [SerializeField] private ParticleSystem winParticles;     // ไม่ใส่ก็ได้

    [Header("Neon")]
    [SerializeField] private Shader neonShader;               // ลากไฟล์ NeonMagicUI.shader มาใส่
    [SerializeField] private Color neonColorA = new Color(0.1f, 1f, 1f, 1f);     // ฟ้าไซยาน
    [SerializeField] private Color neonColorB = new Color(0.65f, 0.3f, 1f, 1f);  // ม่วง
    [SerializeField] private Color neonColorC = new Color(1f, 0.25f, 0.85f, 1f); // ชมพูแมเจนต้า
    [SerializeField] private float neonIntensity = 1.6f;      // ความสว่างแกนเส้น
    [SerializeField] private float neonGlowStrength = 1.3f;   // ความแรงของแสงรอบเส้น
    [SerializeField] private float neonGlowRadius = 6f;       // รัศมีแสง (หน่วยพิกเซลของ texture)
    [SerializeField, Range(0f, 1f)] private float neonPulse = 0.25f;
    [SerializeField] private bool neonAlphaOnly = false;      // ติ๊กถ้าเส้นของภาพไม่ได้เป็นสีดำ ให้ใช้ alpha เป็นตัวกำหนดเส้นแทน

    private static readonly int idNeon = Shader.PropertyToID("_Neon");
    private static readonly int idUTime = Shader.PropertyToID("_UTime");

    private RectTransform area;
    private Image[] rings;
    private bool[] rotatable;
    private float[] angles = new float[3]; // 0=large, 1=mid, 2=small

    // state
    private int activeRing = -1;
    private float lastPointerAngle;
    private bool inputLocked = true;
    private bool isOpen;
    private bool winPlaying;
    private bool isCoasting;
    private int coastingRing = -1;
    private Coroutine spinRoutine;
    private Action<bool> onFinished;
    private float savedTimeScale = 1f;

    // drag velocity / tick
    private float dragVelocity;
    private float lastDragTime;
    private int lastTickIndex;
    private float lastTickTime;

    // visual state
    private float[] baseScale = new float[3];
    private float[] scaleCur = new float[3];
    private Color[] colorCur = new Color[3];
    private float[] pulse = new float[3];
    private float winGlow;
    private float winBoost;
    private float ringsAlpha = 1f;
    private float fullBaseScale = 1f;

    // ไฮไลต์ม่วง
    private Image[] beams;
    private float[] beamAmt = new float[3];
    private float[] beamTarget = new float[3];
    private Sprite beamSprite;

    // neon
    private readonly List<Material> neonMats = new List<Material>();

    private void Awake() => Init();

    // เตรียมตัวครั้งเดียว (เรียกซ้ำได้ ปลอดภัย) — Open() เรียกด้วย กันกรณี Awake ยังไม่ทำงานเพราะหน้าต่างปิดอยู่
    private void Init()
    {
        if (area != null) return;

        area = (RectTransform)transform;
        rings = new[] { largeRing, midRing, smallRing };
        rotatable = new[] { canRotateLarge, canRotateMid, canRotateSmall };

        for (int i = 0; i < 3; i++)
        {
            baseScale[i] = rings[i].rectTransform.localScale.x;
            scaleCur[i] = 1f;
            colorCur[i] = idleColor;
        }
        if (fullImage != null) fullBaseScale = fullImage.rectTransform.localScale.x;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;

        BuildBeams();
    }

    private void Start()
    {
        if (openOnStart) Open(null);
    }

    private void OnDisable()
    {
        // กันกรณีหน้าต่างถูกปิดจากข้างนอกขณะเกมเปิดอยู่ แล้ว timeScale ค้างที่ 0
        if (isOpen)
        {
            isOpen = false;
            Time.timeScale = savedTimeScale;
        }
    }

    private void OnDestroy()
    {
        foreach (var m in neonMats) if (m != null) Destroy(m);
        neonMats.Clear();
        if (beamSprite != null)
        {
            if (beamSprite.texture != null) Destroy(beamSprite.texture);
            Destroy(beamSprite);
        }
    }

    // วาดวงรัศมีในหน้า Scene (เห็นตอนเลือก ringarea) แดง=เล็ก เขียว=กลาง น้ำเงิน=ใหญ่
    private void OnDrawGizmosSelected()
    {
        float scale = transform.lossyScale.x;
        DrawCircleGizmo(smallRadius * scale, Color.red);
        DrawCircleGizmo(midRadius * scale, Color.green);
        DrawCircleGizmo(largeRadius * scale, Color.blue);
    }

    private void DrawCircleGizmo(float radius, Color color)
    {
        Gizmos.color = color;
        Vector3 center = transform.position;
        Vector3 prev = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= 64; i++)
        {
            float a = i / 64f * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
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

        if (isOpen) return;
        isOpen = true;

        onFinished = callback;
        savedTimeScale = Time.timeScale; // เก็บค่าเดิมไว้คืน
        windowRoot.SetActive(true);
        Time.timeScale = 0f;
        StartPuzzle();
    }

    // ปุ่มปิด/ยอมแพ้ (ระหว่างเล่นเอฟเฟกต์ชนะจะไม่ทำงาน)
    public void GiveUp()
    {
        if (winPlaying) return;
        Finish(false);
    }

    // ================= เริ่มด่าน =================
    private void StartPuzzle()
    {
        if (patterns == null || patterns.Length == 0)
        {
            Debug.LogError("MagicCirclePuzzle: ยังไม่ได้ใส่ patterns");
            Finish(false);
            return;
        }

        rotatable = new[] { canRotateLarge, canRotateMid, canRotateSmall };

        // รีเซ็ตทุกอย่างจากรอบก่อน
        StopAllCoroutines();
        spinRoutine = null;
        ClearNeon();

        // สุ่ม 1 ใน N รูปแบบ
        Pattern p = patterns[UnityEngine.Random.Range(0, patterns.Length)];
        largeRing.sprite = p.large;
        midRing.sprite = p.mid;
        smallRing.sprite = p.small;

        if (fullImage != null)
        {
            fullImage.sprite = p.full;
            fullImage.color = new Color(1, 1, 1, 0);
            fullImage.rectTransform.localScale = new Vector3(fullBaseScale, fullBaseScale, 1f);
        }

        activeRing = -1;
        coastingRing = -1;
        isCoasting = false;
        winPlaying = false;
        winGlow = 0f;
        winBoost = 0f;
        ringsAlpha = 1f;
        dragVelocity = 0f;
        for (int i = 0; i < 3; i++)
        {
            pulse[i] = 0f;
            scaleCur[i] = 1f;
            colorCur[i] = idleColor;
            beamAmt[i] = 0f;
        }
        if (rippleImage != null) SetAlpha(rippleImage, 0f);
        if (winParticles != null) winParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // สุ่มมุมเริ่มต้น และต้องไม่เริ่มมาแล้วตรงกันเลย (มีตัวกันลูปไม่จบ)
        bool anyRotatable = rotatable[0] || rotatable[1] || rotatable[2];
        if (!anyRotatable) Debug.LogWarning("MagicCirclePuzzle: ไม่มีวงไหนหมุนได้เลย");

        int guard = 0;
        do
        {
            for (int i = 0; i < 3; i++)
                angles[i] = rotatable[i] ? RandomAngle() : 0f;
        }
        while (anyRotatable && IsSolved() && ++guard < 100);

        ApplyAngles();
        ApplyVisuals();
        inputLocked = false;
    }

    private float RandomAngle()
    {
        float a = UnityEngine.Random.Range(40f, 320f);
        return snapStep > 0 ? Mathf.Round(a / snapStep) * snapStep : a;
    }

    // ================= อัปเดตภาพทุกเฟรม =================
    private void Update()
    {
        if (!isOpen) return;

        // timeScale = 0 อยู่ ต้องส่งเวลาจริงให้ shader เอง
        float ut = Time.unscaledTime;
        for (int i = 0; i < neonMats.Count; i++)
            if (neonMats[i] != null) neonMats[i].SetFloat(idUTime, ut);

        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        float dt = Time.unscaledDeltaTime;
        float k = 1f - Mathf.Exp(-visualSmooth * dt);
        int focus = activeRing >= 0 ? activeRing : coastingRing;

        for (int i = 0; i < 3; i++)
        {
            Color targetColor = idleColor;
            float targetScale = 1f;

            if (!winPlaying && focus >= 0)
            {
                if (i == focus) { targetColor = highlightColor; targetScale = highlightScale; }
                else targetColor = dimColor;
            }

            scaleCur[i] = Mathf.Lerp(scaleCur[i], targetScale, k);
            colorCur[i] = Color.Lerp(colorCur[i], targetColor, k);
            pulse[i] = Mathf.MoveTowards(pulse[i], 0f, dt * 3f);

            float s = baseScale[i] * scaleCur[i] * (1f + pulse[i] * lockPulse + winBoost);
            rings[i].rectTransform.localScale = new Vector3(s, s, 1f);

            Color c = Color.Lerp(colorCur[i], winGlowColor, winGlow);
            c.a = ringsAlpha;
            rings[i].color = c;
        }

        UpdateBeams(k);
    }

    // ================= ไฮไลต์ม่วงตามแกน Y =================
    // วงกลาง/วงเล็ก ตรงกับวงใหญ่แล้ว -> แถบแสงม่วงของวงนั้นจะติดกระพริบเบาๆ ที่ด้านบน (แกน Y)
    // วงใหญ่จะติดเมื่อมีอย่างน้อย 1 วงตรงแล้ว แถบจึงต่อกันเป็นลำแสงตั้งตรงจากศูนย์กลางขึ้นไปเมื่อตรงมากขึ้น
    private void UpdateBeams(float k)
    {
        if (beams == null) return;

        bool show = showAlignHint && !winPlaying;
        bool m1 = show && Mathf.Abs(Mathf.DeltaAngle(angles[1], angles[0])) <= tolerance;
        bool m2 = show && Mathf.Abs(Mathf.DeltaAngle(angles[2], angles[0])) <= tolerance;

        beamTarget[0] = (m1 || m2) ? 1f : 0f;
        beamTarget[1] = m1 ? 1f : 0f;
        beamTarget[2] = m2 ? 1f : 0f;

        float blink = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * hintBlinkSpeed);

        for (int i = 0; i < 3; i++)
        {
            beamAmt[i] = Mathf.Lerp(beamAmt[i], beamTarget[i], k);
            Color c = hintColor;
            c.a = hintMaxAlpha * beamAmt[i] * blink;
            beams[i].color = c;
        }
    }

    private void BuildBeams()
    {
        beamSprite = MakeBeamSprite();
        beams = new Image[3];

        // index ตามวง: 0=ใหญ่ 1=กลาง 2=เล็ก (แต่ละแถบกินระยะตามแนวรัศมีของวงนั้น)
        float[] inner = { midRadius, smallRadius, 0f };
        float[] outer = { largeRadius, midRadius, smallRadius };

        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject("AlignBeam_" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(area, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); // กลาง rect เดียวกับที่ใช้คำนวณศูนย์กลางวง
            rt.pivot = new Vector2(0.5f, 0f);                      // ฐานอยู่ที่รัศมีด้านใน ยืดขึ้นไปตามแกน +Y
            rt.anchoredPosition = new Vector2(0f, inner[i]);
            rt.sizeDelta = new Vector2(hintWidth, Mathf.Max(1f, outer[i] - inner[i]));

            var img = go.GetComponent<Image>();
            img.sprite = beamSprite;
            img.raycastTarget = false;
            img.color = new Color(hintColor.r, hintColor.g, hintColor.b, 0f);
            beams[i] = img;
        }
    }

    // สร้างสไปรต์แถบแสงนุ่มๆ (ขอบจาง แกนกลางสว่าง) ด้วยโค้ด ไม่ต้องมีไฟล์รูป
    private static Sprite MakeBeamSprite()
    {
        const int w = 32, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            float fadeY = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(v, 1f - v) / 0.12f));
            for (int x = 0; x < w; x++)
            {
                float u = ((x + 0.5f) / w) * 2f - 1f;
                float a = Mathf.Exp(-u * u * 5f) * 0.7f + Mathf.Exp(-u * u * 40f) * 0.5f;
                a = Mathf.Clamp01(a) * fadeY;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    // ================= รับ input =================
    public void OnPointerDown(PointerEventData e)
    {
        if (inputLocked) return;

        // ถ้าวงกำลังหมุนตามแรงเฉื่อยอยู่ ให้หยุดและ snap ทันที แล้วค่อยจับวงใหม่
        if (isCoasting)
        {
            CancelCoast();
            if (inputLocked) return; // กรณี snap แล้วชนะพอดี
        }

        Vector2 local = ToLocal(e);
        float dist = local.magnitude;

        if (dist <= smallRadius) activeRing = 2;
        else if (dist <= midRadius) activeRing = 1;
        else if (dist <= largeRadius) activeRing = 0;
        else activeRing = -1;

        if (activeRing >= 0 && !rotatable[activeRing]) activeRing = -1;
        lastPointerAngle = PointerAngle(local);

        dragVelocity = 0f;
        lastDragTime = Time.unscaledTime;
        if (activeRing >= 0) lastTickIndex = TickIndex(angles[activeRing]);
    }

    public void OnDrag(PointerEventData e)
    {
        if (inputLocked || activeRing < 0) return;

        float now = PointerAngle(ToLocal(e));
        float delta = Mathf.DeltaAngle(lastPointerAngle, now);
        angles[activeRing] += delta;
        lastPointerAngle = now;

        // เก็บความเร็วเชิงมุม (องศา/วินาที) ไว้ใช้ทำ inertia
        float dt = Time.unscaledDeltaTime;
        if (dt > 0.0001f) dragVelocity = Mathf.Lerp(dragVelocity, delta / dt, 0.4f);
        lastDragTime = Time.unscaledTime;

        ApplyAngles();
        TickCheck(activeRing);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (inputLocked || activeRing < 0) return;
        int ring = activeRing;
        activeRing = -1;

        // ถ้าค้างนิ้วนิ่งๆ ก่อนปล่อย = ไม่ต้องมีแรงเฉื่อย
        float v = (Time.unscaledTime - lastDragTime > 0.08f) ? 0f : dragVelocity;
        spinRoutine = StartCoroutine(SpinAndSnap(ring, v));
    }

    private Vector2 ToLocal(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out var local);
        // local อิงจาก pivot ของ ringArea, ลบจุดกลาง rect เพื่อให้ได้ศูนย์กลางจริง (ใช้ได้ทุก pivot)
        return local - (Vector2)area.rect.center;
    }

    private float PointerAngle(Vector2 local) => Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;

    // ================= เสียงคลิก =================
    private int TickIndex(float angle)
    {
        float step = snapStep > 0 ? snapStep : 10f;
        return Mathf.FloorToInt(angle / step);
    }

    private void TickCheck(int ring)
    {
        int idx = TickIndex(angles[ring]);
        if (idx == lastTickIndex) return;
        lastTickIndex = idx;

        // กันเสียงรัวเกินไปตอนหมุนเร็วๆ
        if (Time.unscaledTime - lastTickTime < 0.025f) return;
        lastTickTime = Time.unscaledTime;

        PlaySfx(tickClip, tickVolume, 1f + UnityEngine.Random.Range(-tickPitchRandom, tickPitchRandom));
    }

    private void PlaySfx(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null || audioSource == null) return;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, volume);
    }

    // ================= หมุน / inertia / snap / ตรวจ =================
    private void ApplyAngles()
    {
        for (int i = 0; i < 3; i++)
            rings[i].rectTransform.localRotation = Quaternion.Euler(0, 0, angles[i]);
    }

    private IEnumerator SpinAndSnap(int ring, float velocity)
    {
        // ช่วง inertia: หมุนต่อแล้วค่อยหน่วงลง (ผู้เล่นจับวงอื่นแทรกได้)
        if (useInertia && Mathf.Abs(velocity) > minInertiaSpeed)
        {
            float v = Mathf.Clamp(velocity, -maxInertiaSpeed, maxInertiaSpeed);
            isCoasting = true;
            coastingRing = ring;
            lastTickIndex = TickIndex(angles[ring]);

            while (Mathf.Abs(v) > minInertiaSpeed)
            {
                float dt = Time.unscaledDeltaTime;
                angles[ring] += v * dt;
                v *= Mathf.Exp(-inertiaFriction * dt);
                ApplyAngles();
                TickCheck(ring);
                yield return null;
            }

            isCoasting = false;
            coastingRing = -1;
        }

        yield return SnapAndCheck(ring);
    }

    private IEnumerator SnapAndCheck(int ring)
    {
        inputLocked = true;

        if (snapStep > 0)
        {
            float from = angles[ring];
            float to = Mathf.Round(from / snapStep) * snapStep;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.1f; // ใช้ unscaled เพราะ timeScale = 0
                float c = Mathf.Clamp01(t);
                angles[ring] = Mathf.Lerp(from, to, c * c * (3f - 2f * c)); // smoothstep
                ApplyAngles();
                yield return null;
            }
        }

        spinRoutine = null;
        AfterSnap(ring);
    }

    // จัดการหลัง snap เสร็จ (ใช้ร่วมกันทั้งตอน snap ปกติและตอนถูกจับแทรกระหว่าง inertia)
    private void AfterSnap(int ring)
    {
        if (IsSolved())
        {
            StartCoroutine(WinSequence());
            return;
        }

        if (RingMatchesBase(ring))
        {
            PlaySfx(lockClip, 0.8f);
            pulse[ring] = 1f;
        }
        inputLocked = false;
    }

    private void CancelCoast()
    {
        if (spinRoutine != null) StopCoroutine(spinRoutine);
        spinRoutine = null;

        int ring = coastingRing;
        isCoasting = false;
        coastingRing = -1;
        if (ring < 0) return;

        if (snapStep > 0) angles[ring] = Mathf.Round(angles[ring] / snapStep) * snapStep;
        ApplyAngles();
        inputLocked = true;
        AfterSnap(ring);
    }

    // ตรงกัน = มุมของวงกลางและวงเล็ก เทียบวงใหญ่ ต่างกันไม่เกิน tolerance
    private bool IsSolved()
    {
        return Mathf.Abs(Mathf.DeltaAngle(angles[1], angles[0])) <= tolerance
            && Mathf.Abs(Mathf.DeltaAngle(angles[2], angles[0])) <= tolerance;
    }

    // วงที่เพิ่งหมุนตรงกับวงใหญ่แล้วหรือยัง (ใช้เล่นเสียง/เอฟเฟกต์ล็อก)
    private bool RingMatchesBase(int ring)
    {
        if (ring != 0)
            return Mathf.Abs(Mathf.DeltaAngle(angles[ring], angles[0])) <= tolerance;

        return Mathf.Abs(Mathf.DeltaAngle(angles[1], angles[0])) <= tolerance
            || Mathf.Abs(Mathf.DeltaAngle(angles[2], angles[0])) <= tolerance;
    }

    // ================= เอฟเฟกต์ตอนชนะ =================
    // 1) วงเก่าหมุนออก + จางหาย  2) ภาพเต็มค่อยๆ เข้ามา  3) เส้นดำกลายเป็นแสงนีออน
    private IEnumerator WinSequence()
    {
        inputLocked = true;
        winPlaying = true;

        // ดึงให้ตรงเป๊ะ
        angles[1] = angles[2] = angles[0];
        ApplyAngles();

        PlaySfx(winClip, 1f);

        bool hasFull = fullImage != null && fullImage.sprite != null;
        var targets = new List<Image>();

        if (hasFull)
        {
            // ---------- 1) เอาของเก่า (วงทั้ง 3) ออกก่อน ----------
            float midStart = angles[1];
            float smallStart = angles[2];
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, ringsClearDuration);
                float c = Mathf.Clamp01(t);
                float ease = 1f - Mathf.Pow(1f - c, 3f);

                angles[1] = midStart + 180f * ease;   // หมุนสวนกันขณะหายไป
                angles[2] = smallStart - 180f * ease;
                ApplyAngles();

                ringsAlpha = 1f - Mathf.SmoothStep(0f, 1f, c);
                winGlow = Mathf.Sin(c * Mathf.PI);     // วาบแสงม่วงอ่อนตอนกำลังหาย
                winBoost = ease * 0.12f;               // ขยายออกเล็กน้อย
                yield return null;
            }
            ringsAlpha = 0f;
            winGlow = 0f;
            winBoost = 0f;

            // ---------- 2) ภาพเต็มค่อยๆ เข้ามา ----------
            t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fullFadeDuration);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
                fullImage.color = new Color(1, 1, 1, e);
                float s = fullBaseScale * Mathf.Lerp(0.92f, 1f, e);
                fullImage.rectTransform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            fullImage.color = Color.white;
            fullImage.rectTransform.localScale = new Vector3(fullBaseScale, fullBaseScale, 1f);

            targets.Add(fullImage);
        }
        else
        {
            // ไม่มีภาพเต็ม -> ใช้วงทั้ง 3 เป็นตัวเปลี่ยนเป็นนีออนแทน
            targets.AddRange(rings);
        }

        // ---------- 3) เส้นดำ -> แสงวงเวทย์นีออน ----------
        foreach (var img in targets) MakeNeonMaterial(img);
        if (neonMats.Count == 0)
            Debug.LogWarning("MagicCirclePuzzle: ไม่พบ Neon Shader (ลาก NeonMagicUI.shader ใส่ช่อง Neon Shader) จึงข้ามเอฟเฟกต์นีออน");

        if (winParticles != null)
        {
            var main = winParticles.main;
            main.useUnscaledTime = true; // timeScale = 0 อยู่ ต้องใช้เวลาจริง
            winParticles.Play();
        }

        float t3 = 0f;
        while (t3 < 1f)
        {
            t3 += Time.unscaledDeltaTime / Mathf.Max(0.01f, neonDuration);
            float c = Mathf.Clamp01(t3);
            float e = Mathf.SmoothStep(0f, 1f, c);

            for (int i = 0; i < neonMats.Count; i++)
                if (neonMats[i] != null) neonMats[i].SetFloat(idNeon, e);

            if (rippleImage != null)
            {
                float ease = 1f - Mathf.Pow(1f - c, 3f);
                float rs = Mathf.Lerp(0.6f, 1.8f, ease);
                rippleImage.rectTransform.localScale = new Vector3(rs, rs, 1f);
                Color rc = neonColorA;
                rc.a = (1f - c) * 0.8f;
                rippleImage.color = rc;
            }
            yield return null;
        }

        for (int i = 0; i < neonMats.Count; i++)
            if (neonMats[i] != null) neonMats[i].SetFloat(idNeon, 1f);
        if (rippleImage != null) SetAlpha(rippleImage, 0f);

        // ค้างให้ชมนีออนเรืองๆ (shader ไล่สี + เต้นเบาๆ ต่อเอง)
        yield return new WaitForSecondsRealtime(winHoldTime);
        Finish(true);
    }

    // ================= Neon material =================
    private void MakeNeonMaterial(Image img)
    {
        if (img == null) return;
        if (neonShader == null) neonShader = Shader.Find("UI/NeonMagic");
        if (neonShader == null) return;

        var m = new Material(neonShader);
        m.SetColor("_ColA", neonColorA);
        m.SetColor("_ColB", neonColorB);
        m.SetColor("_ColC", neonColorC);
        m.SetFloat("_Intensity", neonIntensity);
        m.SetFloat("_GlowStrength", neonGlowStrength);
        m.SetFloat("_GlowRadius", neonGlowRadius);
        m.SetFloat("_Pulse", neonPulse);
        m.SetFloat("_AlphaOnly", neonAlphaOnly ? 1f : 0f);
        m.SetFloat(idNeon, 0f);
        m.SetFloat(idUTime, Time.unscaledTime);

        // UI ไม่ได้ส่งขนาด texture ให้ shader เอง จึงตั้งให้
        if (img.sprite != null && img.sprite.texture != null)
        {
            Texture tex = img.sprite.texture;
            m.SetVector("_Texel", new Vector4(1f / tex.width, 1f / tex.height, 0f, 0f));
        }

        img.material = m;
        neonMats.Add(m);
    }

    private void ClearNeon()
    {
        foreach (var m in neonMats) if (m != null) Destroy(m);
        neonMats.Clear();

        if (fullImage != null) fullImage.material = null; // กลับไปใช้ material ปกติของ UI
        if (rings != null)
            foreach (var r in rings) if (r != null) r.material = null;
    }

    private static void SetAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    private void Finish(bool success)
    {
        if (!isOpen) return;
        isOpen = false;

        StopAllCoroutines(); // กัน coroutine (snap/inertia/win) ค้างหลังปิดหน้าต่าง
        spinRoutine = null;
        isCoasting = false;
        coastingRing = -1;
        activeRing = -1;
        winPlaying = false;
        inputLocked = true;

        windowRoot.SetActive(false);
        Time.timeScale = savedTimeScale; // คืนค่าเดิม ไม่ใช่ 1 ตายตัว
        onFinished?.Invoke(success);
    }
}