using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================
//  มินิเกม "เรียงเศษกระจก" (จิ๊กซอว์เศษกระจกแตก)
//  - สุ่มรูปจากลิสต์ แล้วตัดเป็นเศษกระจกรูปทรงเหลี่ยมไม่ซ้ำกันทุกรอบ (ใส่รูปอะไรก็เล่นได้)
//  - ซ้าย = ต้นฉบับสมบูรณ์ / ขวา = พื้นที่ต่อภาพ / ล่าง = ถาดวางเศษ
//  - ลากเศษไปวางบนพื้นที่ต่อภาพ วางใกล้ตำแหน่งที่ถูกจะดูดเข้าที่เอง
//  - ทุกอย่างสร้างจากโค้ด ไม่ต้องใช้ภาพ/Prefab เพิ่ม
//  วิธีติดตั้ง: ดูคำอธิบายท้ายไฟล์
// =====================================================================

[RequireComponent(typeof(RectTransform))]
public class ShatteredMirrorPuzzle : MonoBehaviour, IPuzzle
{
    [Header("หน้าต่าง")]
    [SerializeField] private GameObject windowRoot;

    [Header("รูปภาพ (ใส่กี่ภาพก็ได้ สุ่ม 1 ภาพทุกครั้งที่เปิด)")]
    [SerializeField] private Sprite[] images;
    [Tooltip("ใส่รูปที่ยังไม่ได้ตั้งเป็น Sprite ได้ด้วย (Texture Type: Default ก็ได้)")]
    [SerializeField] private Texture2D[] textures;

    [Header("ความยาก")]
    [SerializeField] private int cols = 4;                 // จำนวนช่องแนวนอน (ยิ่งเยอะ เศษยิ่งเล็กและเยอะ)
    [SerializeField] private int rows = 3;                 // จำนวนช่องแนวตั้ง
    [SerializeField, Range(0f, 1f)] private float splitChance = 0.35f; // โอกาสที่ช่องนั้นแตกเป็น 2 เศษสามเหลี่ยม
    [SerializeField, Range(0f, 0.35f)] private float jitter = 0.24f;   // ความเบี้ยวของรอยแตก (0 = กริดเรียบ)
    [SerializeField] private float snapDistance = 60f;     // วางใกล้ที่ถูกภายในกี่พิกเซลจะดูดเข้าที่ (ยิ่งเยอะยิ่งง่าย)
    [SerializeField] private bool showGuides = true;       // แสดงรอยแตกจาง ๆ บนพื้นที่ต่อภาพเป็นตัวช่วย
    [SerializeField, Range(0f, 1f)] private float guideAlpha = 0.25f;

    [Header("เลย์เอาต์ (หน่วยพิกเซลบน board)")]
    [SerializeField] private float padding = 24f;
    [SerializeField, Range(0.2f, 0.45f)] private float trayHeightRatio = 0.32f; // สัดส่วนความสูงของถาดด้านล่าง

    [Header("หน้าตา")]
    [SerializeField] private Color panelColor = new Color(0.05f, 0.07f, 0.12f, 0.85f);
    [SerializeField] private Color trayColor = new Color(0.1f, 0.12f, 0.2f, 0.9f);
    [SerializeField] private Color frameColor = new Color(0.75f, 0.9f, 1f, 0.8f);
    [SerializeField] private Color outlineColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color liftOutlineColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Color snapFlashColor = new Color(0.75f, 1f, 1f, 1f);
    [SerializeField] private float outlineThickness = 2f;
    [SerializeField, Range(0f, 0.3f)] private float facetStrength = 0.08f; // ความต่างของความสว่างระหว่างเศษ
    [SerializeField] private float liftScale = 1.06f;
    [SerializeField] private Color barBgColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color barColor = new Color(0.65f, 0.9f, 1f, 1f);

    [Header("เอฟเฟกต์ตอนชนะ")]
    [SerializeField] private float seamFadeDuration = 0.7f; // รอยร้าวจางหาย ภาพเนียนเป็นชิ้นเดียว
    [SerializeField] private float winHoldTime = 1.3f;

    [Header("เสียง (ไม่ใส่ก็ได้)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pickClip;
    [SerializeField] private AudioClip dropClip;
    [SerializeField] private AudioClip snapClip;
    [SerializeField] private AudioClip winClip;

    [Header("ทดสอบ")]
    [SerializeField] private bool openOnStart = false;

    // ---------- สถานะ ----------
    private RectTransform area, content, guidesRoot, placedRoot, looseRoot;
    private readonly List<MirrorShardPiece> pieces = new List<MirrorShardPiece>();
    private Image flash, progressFill;
    private float barFullWidth, progressShown;
    private MirrorShardPiece dragging;
    private Vector2 dragOffset;
    private int placedCount;
    private bool isOpen, inputLocked, winPlaying, opened;
    private float savedTimeScale = 1f;
    private Action<bool> onFinished;
    private int lastImageIndex = -1;
    private Sprite runtimeSprite;

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
    }

    private void Start()
    {
        if (openOnStart && !opened) Open(null);
    }

    private void OnDisable()
    {
        // กันกรณีหน้าต่างถูกปิดจากข้างนอก แล้ว timeScale ค้างที่ 0
        if (isOpen)
        {
            isOpen = false;
            Time.timeScale = savedTimeScale;
        }
    }

    private void OnDestroy()
    {
        if (runtimeSprite != null) Destroy(runtimeSprite);
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
        if (winPlaying) return;
        Finish(false);
    }

    public void Reroll()
    {
        if (isOpen && !winPlaying) StartPuzzle(); // ปุ่ม "สุ่มภาพใหม่" (ไม่บังคับ)
    }

    private void Finish(bool success)
    {
        if (!isOpen) return;
        isOpen = false;
        StopAllCoroutines();
        dragging = null;
        inputLocked = true;
        winPlaying = false;

        windowRoot.SetActive(false);
        Time.timeScale = savedTimeScale;
        onFinished?.Invoke(success);
    }

    // ================= เริ่มด่าน =================
    private void StartPuzzle()
    {
        StopAllCoroutines();
        dragging = null;
        winPlaying = false;
        inputLocked = true;

        Sprite sp = PickSprite();
        if (sp == null)
        {
            Debug.LogError("ShatteredMirrorPuzzle: ยังไม่ได้ใส่รูป (ช่อง Images หรือ Textures)");
            Finish(false);
            return;
        }

        if (!BuildPuzzle(sp))
        {
            Debug.LogError("ShatteredMirrorPuzzle: board เล็กเกินไป ลองขยายขนาดเป็น 1400 x 800 ขึ้นไป");
            Finish(false);
            return;
        }
        inputLocked = false;
    }

    private Sprite PickSprite()
    {
        int nImg = images != null ? images.Length : 0;
        int nTex = textures != null ? textures.Length : 0;
        int total = nImg + nTex;
        if (total == 0) return null;

        int idx = UnityEngine.Random.Range(0, total);
        if (total > 1 && idx == lastImageIndex) idx = (idx + 1 + UnityEngine.Random.Range(0, total - 1)) % total; // ไม่ซ้ำภาพเดิมติดกัน
        lastImageIndex = idx;

        if (runtimeSprite != null) { Destroy(runtimeSprite); runtimeSprite = null; }

        if (idx < nImg) return images[idx];

        Texture2D t = textures[idx - nImg];
        if (t == null) return null;
        runtimeSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        return runtimeSprite;
    }

    // ================= สร้างหน้าจอ + เศษกระจก =================
    private bool BuildPuzzle(Sprite sp)
    {
        if (content != null) Destroy(content.gameObject);
        pieces.Clear();
        placedCount = 0;
        progressShown = 0f;

        Vector2 size = area.rect.size;
        float pad = padding;
        float trayH = Mathf.Clamp(size.y * trayHeightRatio, 120f, size.y * 0.5f);
        const float barH = 10f;

        float topY0 = -size.y * 0.5f + trayH + pad;
        float topY1 = size.y * 0.5f - pad - barH - 6f;
        float x0 = -size.x * 0.5f + pad, x1 = size.x * 0.5f - pad;
        float mid = (x0 + x1) * 0.5f;

        Rect leftR = Rect.MinMaxRect(x0, topY0, mid - pad * 0.5f, topY1);
        Rect rightR = Rect.MinMaxRect(mid + pad * 0.5f, topY0, x1, topY1);
        Rect trayR = Rect.MinMaxRect(x0, -size.y * 0.5f + pad, x1, -size.y * 0.5f + trayH);

        if (leftR.width < 200f || leftR.height < 150f) return false;

        // ราก
        content = NewStretch("Content", area);
        NewImage("RefPanel", content, null, panelColor, leftR.center, leftR.size);
        NewImage("BoardPanel", content, null, panelColor, rightR.center, rightR.size);
        NewImage("TrayPanel", content, null, trayColor, trayR.center, trayR.size);

        // ขนาดรูปที่แสดง (เท่ากันทั้งต้นฉบับและพื้นที่ต่อภาพ)
        Rect tr = sp.textureRect;
        float aspect = tr.width / Mathf.Max(1f, tr.height);
        Vector2 inner = leftR.size - Vector2.one * 32f;
        float w = inner.x, h = w / aspect;
        if (h > inner.y) { h = inner.y; w = h * aspect; }
        Vector2 imgSize = new Vector2(w, h);

        // ต้นฉบับ (ซ้าย)
        NewImage("RefFrame", content, null, frameColor, leftR.center, imgSize + Vector2.one * 8f);
        Image refImg = NewImage("Reference", content, sp, Color.white, leftR.center, imgSize);
        refImg.preserveAspect = false;

        // พื้นที่ต่อภาพ (ขวา)
        NewImage("BoardFrame", content, null, new Color(frameColor.r, frameColor.g, frameColor.b, 0.35f), rightR.center, imgSize + Vector2.one * 8f);
        NewImage("BoardBack", content, null, new Color(0f, 0f, 0f, 0.45f), rightR.center, imgSize);
        Vector2 boardOrigin = rightR.center - imgSize * 0.5f; // มุมซ้ายล่างของภาพที่ต่อ

        guidesRoot = NewStretch("Guides", content);
        placedRoot = NewStretch("Placed", content);
        looseRoot = NewStretch("Loose", content);

        // เศษกระจก
        List<Vector2[]> shards = GenerateShards();
        int n = shards.Count;

        // ตำแหน่งเริ่มต้นในถาด (เรียงเป็นช่อง ๆ แล้วสุ่มสลับ + สั่นตำแหน่งเล็กน้อย)
        Rect ti = Rect.MinMaxRect(trayR.xMin + 50f, trayR.yMin + 40f, trayR.xMax - 50f, trayR.yMax - 40f);
        int tRows = n > 8 ? 2 : 1;
        int tCols = Mathf.CeilToInt(n / (float)tRows);
        var slots = new List<Vector2>();
        float slotW = ti.width / tCols, slotH = ti.height / tRows;
        for (int r = 0; r < tRows; r++)
            for (int c = 0; c < tCols; c++)
            {
                Vector2 p = new Vector2(
                    Mathf.Lerp(ti.xMin, ti.xMax, (c + 0.5f) / tCols) + UnityEngine.Random.Range(-0.2f, 0.2f) * slotW,
                    Mathf.Lerp(ti.yMin, ti.yMax, (r + 0.5f) / tRows) + UnityEngine.Random.Range(-0.2f, 0.2f) * slotH);
                slots.Add(p);
            }
        for (int i = slots.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            Vector2 tmp = slots[i]; slots[i] = slots[j]; slots[j] = tmp;
        }

        for (int i = 0; i < n; i++)
            pieces.Add(MakePiece(shards[i], i, sp, imgSize, boardOrigin, slots[i % slots.Count]));

        // แฟลชตอนชนะ (ทับพื้นที่ต่อภาพ)
        flash = NewImage("Flash", content, null, new Color(1f, 1f, 1f, 0f), rightR.center, imgSize);

        // แถบความคืบหน้าด้านบน
        barFullWidth = size.x - pad * 2f;
        float barY = size.y * 0.5f - pad * 0.5f - barH * 0.5f;
        NewImage("BarBg", content, null, barBgColor, new Vector2(0f, barY), new Vector2(barFullWidth, barH));
        progressFill = NewImage("BarFill", content, null, barColor, Vector2.zero, new Vector2(0f, barH));
        progressFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        progressFill.rectTransform.anchoredPosition = new Vector2(-barFullWidth * 0.5f, barY);

        return true;
    }

    // ตัดภาพเป็นเศษ: กริดที่ขยับจุดให้เบี้ยว แล้วบางช่องแตกเป็น 2 เศษสามเหลี่ยม (พิกัดเป็นสัดส่วน 0..1, แกน Y ขึ้น)
    private List<Vector2[]> GenerateShards()
    {
        int cx = Mathf.Max(2, cols), cy = Mathf.Max(2, rows);
        float cw = 1f / cx, ch = 1f / cy;

        var g = new Vector2[cx + 1, cy + 1];
        for (int i = 0; i <= cx; i++)
            for (int j = 0; j <= cy; j++)
            {
                float x = i * cw, y = j * ch;
                if (i > 0 && i < cx) x += UnityEngine.Random.Range(-jitter, jitter) * cw;  // ขอบรูปไม่ขยับ จะได้เป็นสี่เหลี่ยมพอดี
                if (j > 0 && j < cy) y += UnityEngine.Random.Range(-jitter, jitter) * ch;
                g[i, j] = new Vector2(x, y);
            }

        var list = new List<Vector2[]>();
        for (int j = 0; j < cy; j++)
            for (int i = 0; i < cx; i++)
            {
                Vector2 a = g[i, j], b = g[i + 1, j], c = g[i + 1, j + 1], d = g[i, j + 1];
                bool convex = Cross(b - a, c - b) > 0f && Cross(c - b, d - c) > 0f && Cross(d - c, a - d) > 0f && Cross(a - d, b - a) > 0f;

                if (convex && UnityEngine.Random.value >= splitChance)
                {
                    list.Add(new[] { a, b, c, d });
                    continue;
                }

                // แตกเป็น 2 สามเหลี่ยม เลือกเส้นทแยงที่ใช้ได้
                bool acOk = Cross(b - a, c - a) > 0f && Cross(c - a, d - a) > 0f;
                bool bdOk = Cross(b - a, d - a) > 0f && Cross(c - b, d - b) > 0f;
                bool useAC = acOk && (!bdOk || UnityEngine.Random.value < 0.5f);
                if (useAC)
                {
                    list.Add(new[] { a, b, c });
                    list.Add(new[] { a, c, d });
                }
                else
                {
                    list.Add(new[] { a, b, d });
                    list.Add(new[] { b, c, d });
                }
            }
        return list;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private MirrorShardPiece MakePiece(Vector2[] npts, int index, Sprite sp, Vector2 imgSize, Vector2 boardOrigin, Vector2 startPos)
    {
        int n = npts.Length;
        var disp = new Vector2[n];
        Vector2 centroid = Vector2.zero;
        for (int i = 0; i < n; i++)
        {
            disp[i] = new Vector2(npts[i].x * imgSize.x, npts[i].y * imgSize.y);
            centroid += disp[i];
        }
        centroid /= n;

        var local = new Vector2[n];
        var uvs = new Vector2[n];
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        Rect tr = sp.textureRect;
        Texture tex = sp.texture;
        for (int i = 0; i < n; i++)
        {
            local[i] = disp[i] - centroid;
            min = Vector2.Min(min, local[i]);
            max = Vector2.Max(max, local[i]);
            uvs[i] = new Vector2((tr.x + npts[i].x * tr.width) / tex.width, (tr.y + npts[i].y * tr.height) / tex.height);
        }
        Vector2 sz = Vector2.Max(max - min, Vector2.one * 4f);
        Vector2 pivot = new Vector2(-min.x / sz.x, -min.y / sz.y); // ให้จุดกึ่งกลางเศษอยู่ที่ pivot

        float shade = 1f - UnityEngine.Random.Range(0f, facetStrength);

        // ตัวเศษ (อยู่ในถาด)
        var go = new GameObject("Shard_" + index, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(looseRoot, false);
        var rt = (RectTransform)go.transform;
        SetupRect(rt, startPos, sz, pivot);

        var fill = go.AddComponent<MirrorShardGraphic>();
        fill.poly = local; fill.uv = uvs; fill.tex = tex; fill.outline = false;
        fill.shade = shade; fill.raycastTarget = true;
        fill.SetAllDirty();

        // รอยร้าวรอบเศษ
        var oGo = new GameObject("Outline", typeof(RectTransform), typeof(CanvasRenderer));
        oGo.transform.SetParent(go.transform, false);
        var ort = (RectTransform)oGo.transform;
        ort.pivot = pivot; // ตั้ง pivot ก่อน แล้วค่อยยืดให้เท่าตัวเศษ เพื่อให้จุดหมุนตรงกันเป๊ะ
        ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
        ort.offsetMin = ort.offsetMax = Vector2.zero;
        var outl = oGo.AddComponent<MirrorShardGraphic>();
        outl.poly = local; outl.outline = true; outl.thickness = outlineThickness;
        outl.color = outlineColor; outl.raycastTarget = false;
        outl.SetAllDirty();

        // รอยแตกจางๆ บนพื้นที่ต่อภาพ (ตัวช่วย)
        var gGo = new GameObject("Guide_" + index, typeof(RectTransform), typeof(CanvasRenderer));
        gGo.transform.SetParent(guidesRoot, false);
        var grt = (RectTransform)gGo.transform;
        Vector2 home = boardOrigin + centroid;
        SetupRect(grt, home, sz, pivot);
        var guide = gGo.AddComponent<MirrorShardGraphic>();
        guide.poly = local; guide.outline = true; guide.thickness = outlineThickness;
        guide.color = new Color(1f, 1f, 1f, showGuides ? guideAlpha : 0f);
        guide.raycastTarget = false;
        guide.SetAllDirty();

        var piece = go.AddComponent<MirrorShardPiece>();
        piece.owner = this;
        piece.rt = rt; piece.fill = fill; piece.outline = outl; piece.guide = guide;
        piece.home = home; piece.baseShade = shade;
        return piece;
    }

    // ================= รับ input จากเศษ =================
    public void PieceDown(MirrorShardPiece p, PointerEventData e)
    {
        if (inputLocked || p.placed || dragging != null) return;
        dragging = p;
        dragOffset = p.rt.anchoredPosition - ToLocal(e);
        p.transform.SetAsLastSibling();
        p.rt.localScale = Vector3.one * liftScale;
        p.outline.color = liftOutlineColor;
        PlaySfx(pickClip, 0.6f);
    }

    public void PieceDrag(MirrorShardPiece p, PointerEventData e)
    {
        if (dragging != p) return;
        Vector2 pos = ClampToArea(ToLocal(e) + dragOffset);
        p.rt.anchoredPosition = pos;

        // ใกล้ตำแหน่งที่ถูก -> รอยแตกบนพื้นที่ต่อภาพสว่างขึ้นบอกใบ้
        if (showGuides && p.guide != null)
        {
            float d = (pos - p.home).magnitude;
            float a = d <= snapDistance * 1.6f ? Mathf.Max(guideAlpha, 0.7f) : guideAlpha;
            p.guide.color = new Color(1f, 1f, 1f, a);
        }
    }

    public void PieceUp(MirrorShardPiece p, PointerEventData e)
    {
        if (dragging != p) return;
        dragging = null;
        p.rt.localScale = Vector3.one;
        p.outline.color = outlineColor;
        if (p.guide != null) p.guide.color = new Color(1f, 1f, 1f, showGuides ? guideAlpha : 0f);

        if ((p.rt.anchoredPosition - p.home).magnitude <= snapDistance)
            StartCoroutine(SnapPiece(p));
        else
            PlaySfx(dropClip, 0.5f);
    }

    private IEnumerator SnapPiece(MirrorShardPiece p)
    {
        p.placed = true;
        p.fill.raycastTarget = false;

        Vector2 from = p.rt.anchoredPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.1f;
            float c = Mathf.Clamp01(t);
            p.rt.anchoredPosition = Vector2.Lerp(from, p.home, c * c * (3f - 2f * c));
            yield return null;
        }

        p.transform.SetParent(placedRoot, false);
        p.rt.anchoredPosition = p.home;
        if (p.guide != null) p.guide.gameObject.SetActive(false);

        placedCount++;
        PlaySfx(snapClip, 0.9f);
        StartCoroutine(FlashPiece(p));

        if (placedCount >= pieces.Count) StartCoroutine(WinSequence());
    }

    private IEnumerator FlashPiece(MirrorShardPiece p)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / 0.35f;
            float c = Mathf.Clamp01(t);
            float s = 1f + 0.12f * Mathf.Sin(c * Mathf.PI);
            p.rt.localScale = new Vector3(s, s, 1f);
            p.outline.color = Color.Lerp(snapFlashColor, outlineColor, c);
            yield return null;
        }
        p.rt.localScale = Vector3.one;
        p.outline.color = outlineColor;
    }

    // ================= ชนะ =================
    private IEnumerator WinSequence()
    {
        inputLocked = true;
        winPlaying = true;
        yield return new WaitForSecondsRealtime(0.35f);
        PlaySfx(winClip, 1f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seamFadeDuration);
            float c = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

            // รอยร้าวจาง + ความต่างความสว่างหาย = ภาพเนียนเป็นชิ้นเดียว
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                Color oc = outlineColor;
                oc.a = Mathf.Lerp(outlineColor.a, 0f, c);
                p.outline.color = oc;
                p.fill.shade = Mathf.Lerp(p.baseShade, 1f, c);
                p.fill.SetVerticesDirty();
            }
            flash.color = new Color(1f, 1f, 1f, Mathf.Sin(c * Mathf.PI) * 0.45f);
            yield return null;
        }
        flash.color = new Color(1f, 1f, 1f, 0f);

        yield return new WaitForSecondsRealtime(winHoldTime);
        Finish(true);
    }

    private void Update()
    {
        if (!isOpen || progressFill == null) return;
        float target = pieces.Count > 0 ? (float)placedCount / pieces.Count : 0f;
        progressShown = Mathf.Lerp(progressShown, target, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        progressFill.rectTransform.sizeDelta = new Vector2(barFullWidth * progressShown, progressFill.rectTransform.sizeDelta.y);
    }

    // ================= ตัวช่วย =================
    private Vector2 ToLocal(PointerEventData e)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, e.position, e.pressEventCamera, out var local);
        return local - (Vector2)area.rect.center; // ศูนย์กลางกระดาน = (0,0)
    }

    private Vector2 ClampToArea(Vector2 p)
    {
        Vector2 half = area.rect.size * 0.5f - Vector2.one * 30f;
        return new Vector2(Mathf.Clamp(p.x, -half.x, half.x), Mathf.Clamp(p.y, -half.y, half.y));
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, volume);
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

    private static Image NewImage(string name, Transform parent, Sprite sprite, Color color, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        SetupRect(rt, pos, size, new Vector2(0.5f, 0.5f));
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}

/*
 วิธีติดตั้ง
 1) ใน Canvas สร้าง  window (Panel เต็มจอ)  >  board (UI > Image)
    - board: Anchor/Pivot กลาง ขนาดแนะนำ 1400 x 800 (เล็กกว่า ~1000 x 600 เศษจะเล็กเกินไป)
    - Alpha ของสี board = 0 หรือใส่สีพื้นหลังก็ได้ / ไม่ต้องติ๊ก Raycast Target ก็ได้
 2) วางสคริปต์ ShatteredMirrorPuzzle บน board แล้วลาก window ใส่ช่อง Window Root
 3) ใส่รูปในช่อง Images (Sprite) หรือ Textures (รูปธรรมดาก็ได้) กี่ภาพก็ได้ ระบบสุ่ม 1 ภาพต่อรอบ
 4) เรียกใช้เหมือนมินิเกมอื่น:  puzzle.Open(success => { ... });  หรือลาก window ใส่ PuzzleManager
*/