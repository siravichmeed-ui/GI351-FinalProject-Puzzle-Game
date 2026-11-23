using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// มินิเกม "ลากตามยันต์"
/// - พื้นหลังเป็นภาพเดียว (กระดาษโบราณ) ใช้ทุกยันต์ / ยันต์สุ่ม 1 อันต่อรอบ
/// - เส้นยันต์แสดงเป็นเส้นบางๆ จางๆ ผู้เล่นกดเมาส์ค้างแล้วลากตามเส้น ตรงที่ลากผ่านจะเรืองแสง
/// - ลากครอบคลุมเส้นได้ตามเปอร์เซ็นต์ที่กำหนด (ค่าเริ่มต้น 90%) = ชนะ
/// - ลากออกนอกเส้นสะสมเกินกำหนด = ความคืบหน้าถูกล้าง เริ่มวาดใหม่ (ยันต์เดิม)
/// - ใส่รูปอะไรก็ได้ ไม่ต้องเปิด Read/Write ไม่ต้องทำ mask ไว้ล่วงหน้า (ระบบอ่านเส้นจากรูปเอง
///   รองรับทั้งรูปพื้นโปร่งใสและรูปพื้นขาว)
/// - ทุกอย่างสร้างจากโค้ด ไม่ต้องใช้ shader/Prefab เพิ่ม
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class YantraTracePuzzle : MonoBehaviour, IPuzzle, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private struct Src
    {
        public Texture tex;
        public Rect rect; // พื้นที่ใน texture (หน่วยพิกเซล)
    }

    [Header("หน้าต่าง")]
    [SerializeField] private GameObject windowRoot;

    [Header("พื้นหลัง (ภาพเดียวใช้ทุกยันต์)")]
    [SerializeField] private Sprite backgroundSprite;
    [Tooltip("ใช้แทน Sprite ได้ ถ้ารูปยังไม่ได้ตั้งเป็น Sprite")]
    [SerializeField] private Texture2D backgroundTexture;

    [Header("ยันต์ (สุ่ม 1 อันต่อรอบ)")]
    [SerializeField] private Sprite[] yantraSprites;
    [Tooltip("ใส่รูปธรรมดาได้ (Texture Type: Default ก็ได้)")]
    [SerializeField] private Texture2D[] yantraTextures;

    [Header("การลากตามเส้น")]
    [SerializeField] private float brushRadius = 14f;                  // ลากห่างเส้นได้ไกลแค่ไหนยังนับ (พิกเซลบนหน้าจอ) ยิ่งมากยิ่งง่าย
    [SerializeField, Range(0.5f, 1f)] private float winCoverage = 0.9f; // ต้องลากครอบคลุมเส้นกี่ % ถึงชนะ

    [Header("ลากผิด = เริ่มใหม่")]
    [SerializeField, Range(0.3f, 1f)] private float offLineTolerance = 0.8f; // ต้องอยู่ใกล้เส้นกี่ % ของ brushRadius ถึงนับว่า "ถูก"
    [SerializeField] private float maxErrorDistance = 350f;  // ลากนอกเส้นสะสมได้กี่พิกเซลก่อนเริ่มใหม่ (ยิ่งน้อยยิ่งโหด)
    [SerializeField, Range(0f, 2f)] private float errorRecover = 0.5f; // ลากถูกเส้น ค่าผิดลดลงกี่เท่าของระยะที่ลาก
    [SerializeField] private Color errorColor = new Color(0.9f, 0.15f, 0.1f, 1f);
    [SerializeField] private float failFlashTime = 0.6f;

    [Header("หน้าตาเส้น")]
    [SerializeField, Range(0, 3)] private int thinIterations = 1;      // ทำเส้นให้บางลง (0 = ตามต้นฉบับ, ยิ่งมากยิ่งบาง)
    [SerializeField] private Color guideColor = new Color(0.30f, 0.17f, 0.10f, 1f); // สีเส้นจาง (ก่อนลาก)
    [SerializeField, Range(0f, 1f)] private float guideAlpha = 0.5f;
    [SerializeField] private Color glowColor = new Color(1f, 0.72f, 0.25f, 1f);     // สีแสงรอบเส้น
    [SerializeField] private Color coreColor = new Color(1f, 0.97f, 0.8f, 1f);      // สีแกนเส้นที่สว่าง
    [SerializeField] private int glowRadius = 6;                       // ความกว้างของแสงเรือง (พิกเซลของ texture)
    [SerializeField] private float glowGain = 3f;                      // ความเข้มของแสงเรือง
    [SerializeField, Range(0f, 2f)] private float glowStrength = 1f;
    [SerializeField] private Color backdropColor = new Color(0.07f, 0.06f, 0.09f, 0.92f);

    [Header("เลย์เอาต์")]
    [SerializeField] private float padding = 24f;
    [SerializeField, Range(0.4f, 1f)] private float yantraFill = 0.78f; // ยันต์กินพื้นที่กี่ % ของกระดาษ
    [SerializeField] private float maxUpscale = 1.25f;                  // ขยายยันต์เล็กได้สูงสุดกี่เท่า
    [SerializeField] private int maxReadSize = 2048;                    // ขนาดสูงสุดที่ใช้อ่านรูป (พิกเซล)

    [Header("เอฟเฟกต์ตอนชนะ")]
    [SerializeField] private float finalGlowFade = 0.7f;  // เส้นที่เหลือเรืองพรึ่บทั้งภาพ
    [SerializeField] private float winHoldTime = 1.5f;
    [SerializeField] private Color barBgColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private Color barColor = new Color(1f, 0.78f, 0.3f, 1f);

    [Header("เสียง (ไม่ใส่ก็ได้)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip tickClip;  // ทุก 25% ของความคืบหน้า
    [SerializeField] private AudioClip winClip;

    [Header("ทดสอบ")]
    [SerializeField] private bool openOnStart = false;

    // ---------- สถานะ ----------
    private RectTransform area, content, yantraRoot;
    private Image cursorImg, barFill, errFill;
    private RawImage guideImg, glowImg, finalImg;
    private Texture2D guideTex, glowTex, finalTex;
    private Sprite cursorSprite;
    private Texture2D cursorTex;

    private int w, h;
    private float[] thinA, halo, reveal;
    private bool[] cover, traced;
    private Color32[] glowBuf;
    private Color32 baseGlow32;
    private int inkTotal, tracedCount;
    private bool dirty;
    private float errorAmount;

    private float dispScale = 1f;
    private Vector2 yCenter;
    private float barFullWidth, progressShown;
    private int lastMilestone;

    private bool isOpen, running, inputLocked, drawing, won, opened;
    private Vector2 lastMask;
    private float savedTimeScale = 1f;
    private Action<bool> onFinished;
    private int lastIndex = -1;

    // ================= เตรียมตัว =================
    private void Awake() => Init();

    private void Init()
    {
        if (area != null) return;
        area = (RectTransform)transform;
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        BuildCursorSprite();
    }

    private void Start()
    {
        if (openOnStart && !opened) Open(null);
    }

    private void OnDisable()
    {
        if (isOpen)
        {
            isOpen = false;
            running = false;
            Time.timeScale = savedTimeScale;
        }
    }

    private void OnDestroy()
    {
        DestroyTextures();
        if (cursorSprite != null) Destroy(cursorSprite);
        if (cursorTex != null) Destroy(cursorTex);
    }

    // ================= เรียกจากเกมหลัก / PuzzleManager =================
    public void Open(Action<bool> callback)
    {
        if (!gameObject.scene.IsValid())
        {
            Debug.LogError("ถูกเรียก Open บน Prefab ใน Project (ไม่ใช่ตัวในซีน) ให้ลาก window จาก Hierarchy มาใช้ หรือสร้างสำเนา (Instantiate) ก่อน");
            callback?.Invoke(false);
            return;
        }
        Init();

        if (isOpen) return;
        isOpen = true;
        opened = true;

        onFinished = callback;
        savedTimeScale = Time.timeScale;
        windowRoot.SetActive(true);
        Time.timeScale = 0f;
        StartPuzzle();
    }

    public void GiveUp()
    {
        if (won) return;
        Finish(false);
    }

    public void Reroll()
    {
        if (isOpen && !won) StartPuzzle(); // ปุ่ม "สุ่มยันต์ใหม่" (ไม่บังคับ)
    }

    private void Finish(bool success)
    {
        if (!isOpen) return;
        isOpen = false;
        running = false;
        drawing = false;
        inputLocked = true;
        StopAllCoroutines();

        windowRoot.SetActive(false);
        Time.timeScale = savedTimeScale;
        onFinished?.Invoke(success);
    }

    // ================= เริ่มด่าน =================
    private void StartPuzzle()
    {
        StopAllCoroutines();
        running = false;
        won = false;
        drawing = false;
        inputLocked = true;
        tracedCount = 0;
        progressShown = 0f;
        lastMilestone = 0;
        errorAmount = 0f;

        if (content != null) Destroy(content.gameObject);
        DestroyTextures();

        if (!BuildLevel())
        {
            Finish(false);
            return;
        }
        inputLocked = false;
        running = true;
    }

    private void DestroyTextures()
    {
        if (guideTex != null) Destroy(guideTex);
        if (glowTex != null) Destroy(glowTex);
        if (finalTex != null) Destroy(finalTex);
        guideTex = glowTex = finalTex = null;
    }

    // ================= สร้างด่านจากรูป =================
    private bool BuildLevel()
    {
        // ---- 1) พื้นหลัง: หาขอบที่มีภาพจริง ครอปด้วย uvRect ----
        Src bg = default;
        bool hasBg = GetBackground(out bg);
        Rect bgUv = new Rect(0, 0, 1, 1);
        Vector2 bgPix = Vector2.one;
        if (hasBg)
        {
            bgUv = new Rect(bg.rect.x / bg.tex.width, bg.rect.y / bg.tex.height, bg.rect.width / bg.tex.width, bg.rect.height / bg.tex.height);
            bgPix = bg.rect.size;
            if (ReadRegion(bg.tex, bg.rect, 512, out Color32[] bpx, out int bw, out int bh))
            {
                float[] bink = ComputeInk(bpx, bw, bh);
                if (FindBounds(bink, bw, bh, 0.1f, 0, out RectInt br))
                {
                    float fx = br.x / (float)bw, fy = br.y / (float)bh;
                    float fw = br.width / (float)bw, fh = br.height / (float)bh;
                    bgUv = new Rect((bg.rect.x + fx * bg.rect.width) / bg.tex.width,
                                    (bg.rect.y + fy * bg.rect.height) / bg.tex.height,
                                    fw * bg.rect.width / bg.tex.width,
                                    fh * bg.rect.height / bg.tex.height);
                    bgPix = new Vector2(fw * bg.rect.width, fh * bg.rect.height);
                }
            }
        }

        // ---- 2) ยันต์: อ่านเส้นจากรูป ----
        Src ys = PickYantra();
        if (ys.tex == null)
        {
            Debug.LogError("YantraTracePuzzle: ยังไม่ได้ใส่ยันต์ (ช่อง Yantra Sprites หรือ Yantra Textures)");
            return false;
        }
        if (!ReadRegion(ys.tex, ys.rect, maxReadSize, out Color32[] px, out int fw0, out int fh0))
        {
            Debug.LogError("YantraTracePuzzle: อ่านรูปยันต์ไม่ได้");
            return false;
        }

        float[] inkFull = ComputeInk(px, fw0, fh0);
        int pad = glowRadius * 3 + 4;
        if (!FindBounds(inkFull, fw0, fh0, 0.2f, pad, out RectInt rr))
        {
            Debug.LogError("YantraTracePuzzle: หาเส้นในรูปยันต์ไม่เจอ (รูปว่างเปล่าหรือสีเส้นกลืนกับพื้น)");
            return false;
        }
        w = rr.width;
        h = rr.height;
        float[] ink = Crop(inkFull, fw0, rr);

        // เส้นบางลง + ขอบเนียน
        float[] thin = ink;
        for (int k = 0; k < thinIterations; k++) thin = Erode(thin, w, h);
        thinA = BoxBlur(thin, w, h, 1);

        // แสงเรือง
        float[] b1 = BoxBlur(thinA, w, h, Mathf.Max(1, glowRadius));
        float[] b2 = BoxBlur(b1, w, h, Mathf.Max(1, glowRadius));
        halo = new float[w * h];
        for (int i = 0; i < halo.Length; i++) halo[i] = Mathf.Clamp01(b2[i] * glowGain);

        // ส่วนของเส้นที่ผู้เล่นต้องลากผ่าน (เฉพาะที่เห็นจริง)
        cover = new bool[w * h];
        traced = new bool[w * h];
        reveal = new float[w * h];
        inkTotal = 0;
        for (int i = 0; i < cover.Length; i++)
        {
            if (thinA[i] > 0.3f) { cover[i] = true; inkTotal++; }
        }
        if (inkTotal < 30)
        {
            Debug.LogError("YantraTracePuzzle: เส้นในรูปน้อยเกินไป ลองลด Thin Iterations");
            return false;
        }

        // ---- 3) texture ----
        Color32 baseGlow = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        baseGlow32 = baseGlow;
        glowBuf = new Color32[w * h];
        var finalBuf = new Color32[w * h];
        var guideBuf = new Color32[w * h];
        for (int i = 0; i < glowBuf.Length; i++)
        {
            glowBuf[i] = baseGlow;
            finalBuf[i] = PixelColor(i, 1f);
            Color32 g = new Color(guideColor.r, guideColor.g, guideColor.b, thinA[i] * guideAlpha);
            guideBuf[i] = g;
        }
        guideTex = MakeTexture(guideBuf);
        glowTex = MakeTexture(glowBuf);
        finalTex = MakeTexture(finalBuf);
        dirty = false;

        // ---- 4) เลย์เอาต์ ----
        Vector2 size = area.rect.size;
        const float barH = 10f;
        float availW = size.x - padding * 2f;
        float availH = size.y - padding * 2f - barH - 8f;
        if (availW < 200f || availH < 200f)
        {
            Debug.LogError("YantraTracePuzzle: board เล็กเกินไป ลองขยายเป็น 1400 x 800");
            return false;
        }
        Vector2 center = new Vector2(0f, -(barH + 8f) * 0.5f);

        float pw, ph;
        if (hasBg)
        {
            float bgAspect = bgPix.x / Mathf.Max(1f, bgPix.y);
            pw = availW; ph = pw / bgAspect;
            if (ph > availH) { ph = availH; pw = ph * bgAspect; }
        }
        else { pw = availW; ph = availH; }

        dispScale = Mathf.Min((pw * yantraFill) / w, (ph * yantraFill) / h);
        dispScale = Mathf.Min(dispScale, maxUpscale);
        yCenter = center;
        Vector2 yantraSize = new Vector2(w, h) * dispScale;

        // ---- 5) สร้างหน้าจอ ----
        content = NewStretch("Content", area);
        NewImage("Backdrop", content, backdropColor, Vector2.zero, size);
        if (hasBg)
        {
            RawImage parch = NewRaw("Parchment", content, bg.tex, Color.white, center, new Vector2(pw, ph));
            parch.uvRect = bgUv;
        }

        var yGo = new GameObject("Yantra", typeof(RectTransform));
        yGo.transform.SetParent(content, false);
        yantraRoot = (RectTransform)yGo.transform;
        SetupRect(yantraRoot, center, yantraSize, new Vector2(0.5f, 0.5f));

        guideImg = NewRaw("Guide", yantraRoot, guideTex, Color.white, Vector2.zero, yantraSize);
        glowImg = NewRaw("Glow", yantraRoot, glowTex, Color.white, Vector2.zero, yantraSize);
        finalImg = NewRaw("FinalGlow", yantraRoot, finalTex, new Color(1f, 1f, 1f, 0f), Vector2.zero, yantraSize);

        // แสงตามเมาส์
        cursorImg = NewImage("Cursor", content, new Color(glowColor.r, glowColor.g, glowColor.b, 0f), Vector2.zero, Vector2.one * brushRadius * 3f);
        cursorImg.sprite = cursorSprite;

        // แถบความคืบหน้า
        barFullWidth = size.x - padding * 2f;
        float barY = size.y * 0.5f - padding * 0.5f - barH * 0.5f;
        NewImage("BarBg", content, barBgColor, new Vector2(0f, barY), new Vector2(barFullWidth, barH));
        barFill = NewImage("BarFill", content, barColor, Vector2.zero, new Vector2(0f, barH));
        barFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        barFill.rectTransform.anchoredPosition = new Vector2(-barFullWidth * 0.5f, barY);

        // แถบค่าผิดพลาด (สีแดง ใต้แถบความคืบหน้า)
        errFill = NewImage("ErrFill", content, errorColor, Vector2.zero, new Vector2(0f, 4f));
        errFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        errFill.rectTransform.anchoredPosition = new Vector2(-barFullWidth * 0.5f, barY - barH * 0.5f - 5f);

        // ตัวรับเมาส์ (โปร่งใส ครอบทั้งกระดาน)
        Image catcher = NewImage("InputCatcher", content, new Color(0f, 0f, 0f, 0f), Vector2.zero, size);
        catcher.raycastTarget = true;
        catcher.canvasRenderer.cullTransparentMesh = false; // ไม่งั้นภาพโปร่งใสสนิทจะถูกตัดทิ้ง รับเมาส์ไม่ได้

        return true;
    }

    private Texture2D MakeTexture(Color32[] pixels)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        t.SetPixels32(pixels);
        t.Apply(false);
        return t;
    }

    private Color32 PixelColor(int i, float rev)
    {
        float core = thinA[i];
        float glow = halo[i] * glowStrength;
        float a = Mathf.Clamp01(Mathf.Max(core, glow * 0.85f)) * rev;
        Color c = Color.Lerp(glowColor, coreColor, Mathf.Clamp01(core * 1.2f));
        return new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(a * 255f));
    }

    // ================= เลือกรูป =================
    private bool GetBackground(out Src s)
    {
        s = default;
        if (backgroundSprite != null)
        {
            s.tex = backgroundSprite.texture;
            s.rect = backgroundSprite.textureRect;
            return true;
        }
        if (backgroundTexture != null)
        {
            s.tex = backgroundTexture;
            s.rect = new Rect(0, 0, backgroundTexture.width, backgroundTexture.height);
            return true;
        }
        return false;
    }

    private Src PickYantra()
    {
        var list = new List<Src>();
        if (yantraSprites != null)
            foreach (var sp in yantraSprites)
                if (sp != null) list.Add(new Src { tex = sp.texture, rect = sp.textureRect });
        if (yantraTextures != null)
            foreach (var t in yantraTextures)
                if (t != null) list.Add(new Src { tex = t, rect = new Rect(0, 0, t.width, t.height) });

        if (list.Count == 0) return default;

        int idx = UnityEngine.Random.Range(0, list.Count);
        if (list.Count > 1 && idx == lastIndex) idx = (idx + 1 + UnityEngine.Random.Range(0, list.Count - 1)) % list.Count; // ไม่ซ้ำอันเดิมติดกัน
        lastIndex = idx;
        return list[idx];
    }

    // ================= อ่านพิกเซลจากรูป (ไม่ต้องเปิด Read/Write) =================
    private static bool ReadRegion(Texture tex, Rect texRect, int maxSize, out Color32[] px, out int rw, out int rh)
    {
        px = null; rw = rh = 0;
        if (tex == null) return false;

        float s = Mathf.Min(1f, maxSize / (float)Mathf.Max(tex.width, tex.height));
        int rtW = Mathf.Max(1, Mathf.RoundToInt(tex.width * s));
        int rtH = Mathf.Max(1, Mathf.RoundToInt(tex.height * s));

        RenderTexture rt = RenderTexture.GetTemporary(rtW, rtH, 0, RenderTextureFormat.ARGB32);
        RenderTexture prev = RenderTexture.active;
        try
        {
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;

            int x = Mathf.Clamp(Mathf.RoundToInt(texRect.x * s), 0, rtW - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(texRect.y * s), 0, rtH - 1);
            rw = Mathf.Clamp(Mathf.RoundToInt(texRect.width * s), 1, rtW - x);
            rh = Mathf.Clamp(Mathf.RoundToInt(texRect.height * s), 1, rtH - y);

            var tmp = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
            tmp.ReadPixels(new Rect(x, y, rw, rh), 0, 0);
            px = tmp.GetPixels32();
            Destroy(tmp);
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
        return px != null;
    }

    // ความเข้มของ "เส้น" ในแต่ละพิกเซล 0..1 (พื้นโปร่งใส = ใช้ alpha / พื้นทึบ = ใช้ความต่างจากสีพื้นมุมภาพ)
    private static float[] ComputeInk(Color32[] px, int w, int h)
    {
        Color32 c0 = px[0], c1 = px[w - 1], c2 = px[(h - 1) * w], c3 = px[h * w - 1];
        float cornerAlpha = (c0.a + c1.a + c2.a + c3.a) / (4f * 255f);
        bool alphaMode = cornerAlpha < 0.5f;

        float br = (c0.r + c1.r + c2.r + c3.r) / (4f * 255f);
        float bg = (c0.g + c1.g + c2.g + c3.g) / (4f * 255f);
        float bb = (c0.b + c1.b + c2.b + c3.b) / (4f * 255f);

        var ink = new float[w * h];
        for (int i = 0; i < ink.Length; i++)
        {
            Color32 c = px[i];
            if (alphaMode) ink[i] = c.a / 255f;
            else
            {
                float dr = c.r / 255f - br, dg = c.g / 255f - bg, db = c.b / 255f - bb;
                ink[i] = Mathf.Clamp01(Mathf.Sqrt(dr * dr + dg * dg + db * db) * 1.6f);
            }
        }
        return ink;
    }

    private static bool FindBounds(float[] a, int w, int h, float thr, int pad, out RectInt r)
    {
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (a[y * w + x] > thr)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
        if (maxX < 0) { r = default; return false; }
        minX = Mathf.Max(0, minX - pad); minY = Mathf.Max(0, minY - pad);
        maxX = Mathf.Min(w - 1, maxX + pad); maxY = Mathf.Min(h - 1, maxY + pad);
        r = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return true;
    }

    private static float[] Crop(float[] src, int srcW, RectInt r)
    {
        var dst = new float[r.width * r.height];
        for (int y = 0; y < r.height; y++)
            Array.Copy(src, (r.y + y) * srcW + r.x, dst, y * r.width, r.width);
        return dst;
    }

    private static float[] Erode(float[] src, int w, int h)
    {
        var dst = new float[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float m = 1f;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int yy = Mathf.Clamp(y + dy, 0, h - 1);
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = Mathf.Clamp(x + dx, 0, w - 1);
                        float v = src[yy * w + xx];
                        if (v < m) m = v;
                    }
                }
                dst[y * w + x] = m;
            }
        return dst;
    }

    private static float[] BoxBlur(float[] src, int w, int h, int r)
    {
        if (r <= 0) return (float[])src.Clone();
        var tmp = new float[w * h];
        var dst = new float[w * h];
        float inv = 1f / (2 * r + 1);

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            float sum = 0f;
            for (int k = 0; k <= r && k < w; k++) sum += src[row + k];
            for (int x = 0; x < w; x++)
            {
                tmp[row + x] = sum * inv;
                int add = x + r + 1, rem = x - r;
                if (add < w) sum += src[row + add];
                if (rem >= 0) sum -= src[row + rem];
            }
        }
        for (int x = 0; x < w; x++)
        {
            float sum = 0f;
            for (int k = 0; k <= r && k < h; k++) sum += tmp[k * w + x];
            for (int y = 0; y < h; y++)
            {
                dst[y * w + x] = sum * inv;
                int add = y + r + 1, rem = y - r;
                if (add < h) sum += tmp[add * w + x];
                if (rem >= 0) sum -= tmp[rem * w + x];
            }
        }
        return dst;
    }

    // ================= รับ input =================
    public void OnPointerDown(PointerEventData e)
    {
        if (!running || inputLocked) return;
        Vector2 local = ToLocal(e);
        drawing = true;
        lastMask = ToMask(local);
        Paint(lastMask);
        SetCursor(local, true);
        CheckWin();
    }

    public void OnDrag(PointerEventData e)
    {
        if (!running || inputLocked || !drawing) return;
        Vector2 local = ToLocal(e);
        Vector2 m = ToMask(local);
        PaintLine(lastMask, m);
        lastMask = m;
        if (inputLocked) return; // ถูกรีเซ็ตระหว่างลากเมื่อกี้
        SetCursor(local, true);
        CheckWin();
    }

    public void OnPointerUp(PointerEventData e)
    {
        drawing = false;
        if (cursorImg != null) SetCursor(Vector2.zero, false);
    }

    private Vector2 ToLocal(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out var local);
        return local - (Vector2)area.rect.center; // ศูนย์กลางกระดาน = (0,0)
    }

    private Vector2 ToMask(Vector2 local)
    {
        return (local - yCenter) / dispScale + new Vector2(w * 0.5f, h * 0.5f);
    }

    private void SetCursor(Vector2 local, bool show)
    {
        if (cursorImg == null) return;
        cursorImg.rectTransform.anchoredPosition = local;
        Color c = glowColor;
        c.a = show ? 0.75f : 0f;
        cursorImg.color = c;
    }

    // ================= ลงสี/เรืองแสงตามที่ลาก =================
    private void PaintLine(Vector2 a, Vector2 b)
    {
        float rMask = brushRadius / dispScale;
        float step = Mathf.Max(1f, rMask * 0.5f);
        float dist = (b - a).magnitude;
        int n = Mathf.Max(1, Mathf.CeilToInt(dist / step));
        float seg = (dist / n) * dispScale; // ระยะต่อช่วง (พิกเซลหน้าจอ)

        for (int i = 1; i <= n; i++)
        {
            bool onLine = Paint(Vector2.Lerp(a, b, i / (float)n));
            if (onLine) errorAmount = Mathf.Max(0f, errorAmount - seg * errorRecover);
            else errorAmount += seg;

            if (errorAmount >= maxErrorDistance)
            {
                StartCoroutine(FailReset());
                return;
            }
        }
    }

    // คืนค่า true ถ้าแปรงอยู่ใกล้เส้นยันต์
    private bool Paint(Vector2 p)
    {
        float rMask = brushRadius / dispScale;     // รัศมีที่นับว่าลากโดนเส้น
        float rOuter = rMask * 1.5f;               // แสงค่อย ๆ จางออกไปถึงรัศมีนี้
        float rLine = rMask * offLineTolerance;    // รัศมีที่นับว่า "อยู่บนเส้น"
        int x0 = Mathf.Max(0, Mathf.FloorToInt(p.x - rOuter));
        int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(p.x + rOuter));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(p.y - rOuter));
        int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(p.y + rOuter));
        if (x0 > x1 || y0 > y1) return false;

        bool onLine = false;
        float fade = Mathf.Max(0.01f, rOuter - rMask);
        for (int y = y0; y <= y1; y++)
        {
            float dy = y + 0.5f - p.y;
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - p.x;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > rOuter) continue;

                int i = y * w + x;
                if (d <= rLine && cover[i]) onLine = true;

                float rev = d <= rMask ? 1f : Mathf.Clamp01((rOuter - d) / fade);
                if (rev > reveal[i])
                {
                    reveal[i] = rev;
                    glowBuf[i] = PixelColor(i, rev);
                    dirty = true;
                }
                if (d <= rMask && cover[i] && !traced[i])
                {
                    traced[i] = true;
                    tracedCount++;
                }
            }
        }
        return onLine;
    }

    private float Progress => inkTotal > 0 ? tracedCount / (float)inkTotal : 0f;

    private void CheckWin()
    {
        if (won || inputLocked) return;
        if (Progress >= winCoverage) StartCoroutine(WinSequence());
    }

    // ================= ลากผิดเยอะ = เริ่มใหม่ =================
    private IEnumerator FailReset()
    {
        inputLocked = true;
        drawing = false;
        SetCursor(Vector2.zero, false);

        Vector2 basePos = yantraRoot.anchoredPosition;
        float t = 0f;
        while (t < failFlashTime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / failFlashTime);
            Color tint = Color.Lerp(errorColor, Color.white, k);
            guideImg.color = tint;
            glowImg.color = tint;
            yantraRoot.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 60f) * 8f * (1f - k), 0f);
            yield return null;
        }
        yantraRoot.anchoredPosition = basePos;
        guideImg.color = Color.white;
        glowImg.color = Color.white;

        ResetProgress();
        inputLocked = false;
    }

    private void ResetProgress()
    {
        Array.Clear(traced, 0, traced.Length);
        Array.Clear(reveal, 0, reveal.Length);
        for (int i = 0; i < glowBuf.Length; i++) glowBuf[i] = baseGlow32;
        tracedCount = 0;
        errorAmount = 0f;
        lastMilestone = 0;
        progressShown = 0f;
        dirty = true;
    }

    // ================= ชนะ =================
    private IEnumerator WinSequence()
    {
        won = true;
        inputLocked = true;
        drawing = false;
        SetCursor(Vector2.zero, false);
        PlaySfx(winClip, 1f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, finalGlowFade);
            float c = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            finalImg.color = new Color(1f, 1f, 1f, c);
            float s = 1f + 0.03f * Mathf.Sin(c * Mathf.PI);
            yantraRoot.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        finalImg.color = Color.white;
        yantraRoot.localScale = Vector3.one;

        progressShown = 1f;
        yield return new WaitForSecondsRealtime(winHoldTime);
        Finish(true);
    }

    private void Update()
    {
        if (!running) return;

        if (dirty)
        {
            glowTex.SetPixels32(glowBuf);
            glowTex.Apply(false);
            dirty = false;
        }

        float target = won ? 1f : Mathf.Clamp01(Progress / Mathf.Max(0.01f, winCoverage));
        progressShown = Mathf.Lerp(progressShown, target, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        if (barFill != null)
            barFill.rectTransform.sizeDelta = new Vector2(barFullWidth * progressShown, barFill.rectTransform.sizeDelta.y);

        if (errFill != null)
            errFill.rectTransform.sizeDelta = new Vector2(
                barFullWidth * Mathf.Clamp01(errorAmount / maxErrorDistance), errFill.rectTransform.sizeDelta.y);

        int milestone = Mathf.FloorToInt(Mathf.Clamp01(Progress / Mathf.Max(0.01f, winCoverage)) * 4f);
        if (milestone > lastMilestone && milestone < 4)
        {
            lastMilestone = milestone;
            PlaySfx(tickClip, 0.8f);
        }

        // แสงตามเมาส์เต้นเบาๆ
        if (cursorImg != null && drawing)
        {
            float s = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 14f);
            cursorImg.rectTransform.localScale = new Vector3(s, s, 1f);
        }
    }

    // ================= ตัวช่วย =================
    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, volume);
    }

    private void BuildCursorSprite()
    {
        const int res = 64;
        cursorTex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color32[res * res];
        float c = (res - 1) * 0.5f;
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float r = new Vector2(x - c, y - c).magnitude / c;
                float a = r >= 1f ? 0f : Mathf.Pow(1f - r, 2.4f);
                px[y * res + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        cursorTex.SetPixels32(px);
        cursorTex.Apply(false);
        cursorSprite = Sprite.Create(cursorTex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
    }

    private static RectTransform NewStretch(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static void SetupRect(RectTransform rt, Vector2 pos, Vector2 size, Vector2 pivot)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    // สร้างด้วย CanvasRenderer ตั้งแต่แรก (Unity 6 ไม่เติมให้ในบางกรณี)
    private static Image NewImage(string name, Transform parent, Color color, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        SetupRect((RectTransform)go.transform, pos, size, new Vector2(0.5f, 0.5f));
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static RawImage NewRaw(string name, Transform parent, Texture tex, Color color, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        SetupRect((RectTransform)go.transform, pos, size, new Vector2(0.5f, 0.5f));
        var ri = go.AddComponent<RawImage>();
        ri.texture = tex;
        ri.color = color;
        ri.raycastTarget = false;
        return ri;
    }
}

/*
 วิธีติดตั้ง
 1) Canvas > window (Panel เต็มจอ) > board (UI > Image)
    - board: Anchor/Pivot กลาง ขนาดแนะนำ 1400 x 800 / จะตั้ง Alpha ของสีเป็น 0 ก็ได้ (ระบบวาดพื้นหลังเองให้)
 2) วางสคริปต์ YantraTracePuzzle บน board แล้วลาก window ใส่ช่อง Window Root
 3) ช่อง Background Sprite (หรือ Background Texture) = รูปกระดาษพื้นหลัง (ใช้ทุกยันต์)
 4) ช่อง Yantra Sprites (หรือ Yantra Textures) = รูปยันต์ กี่รูปก็ได้ สุ่ม 1 รูปต่อรอบ
 5) เรียกใช้: puzzle.Open(success => {...});  หรือลาก window ใส่ PuzzleManager
*/