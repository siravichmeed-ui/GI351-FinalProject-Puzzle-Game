using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

/// <summary>
/// ควบคุมซีนล้างคำสาป:
///   กด Q  -> เปิด/ปิด โหมดสแกน แสดง "จุดคำสาป" บนสินค้า
///   คลิกจุด -> เปิด Panel Puzzle (IPuzzle) แบบสุ่มจากลิสต์
///   ชนะ    -> ถ้าล้างครบทุกจุด MarkPuzzleSolved + เปลี่ยนเป็นสปรต์สะอาด + กลับร้าน
/// วางไว้บน GameObject ว่างๆ ใน CleansingScene 1 ตัว
/// </summary>
public class CurseCleansingController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("ถ้าว่างจะหา ItemDisplay ในซีนให้เอง")]
    public ItemDisplay itemDisplay;

    [Header("Puzzle Windows")]
    [Tooltip("window ของมินิเกม (ลากจาก Hierarchy หรือ Prefab) — สุ่มเปิดทีละตัวไม่ซ้ำตัวล่าสุด")]
    public List<GameObject> puzzleWindows = new List<GameObject>();
    [Tooltip("ใช้เมื่อลาก Prefab ใส่ลิสต์: Canvas ที่จะสร้างสำเนา (ว่าง = Canvas แรกในซีน)")]
    public Transform canvas;

    [Header("Scan Mode")]
    public KeyCode scanKey = KeyCode.Q;
    [Min(1)] public int cursePointCount = 1;
    [Tooltip("สัดส่วนพื้นที่ภายในสินค้าที่จุดคำสาปจะเกิด (0-1)")]
    [Range(0.1f, 0.9f)] public float placementArea = 0.55f;
    public float revealStagger = 0.12f;

    [Header("Curse Point Look")]
    public Color curseColor = new Color(0.85f, 0.25f, 1f, 1f);
    public float pointSize = 0.5f;
    [Tooltip("ไม่ใส่ = ใช้วงกลมที่สร้างจากโค้ด")]
    public Sprite customPointSprite;

    [Header("Scan Juice")]
    [Tooltip("(ไม่บังคับ) CanvasGroup เต็มจอสีมืดๆ ไว้หรี่ฉากตอนสแกน")]
    public CanvasGroup scanOverlay;
    [Range(0f, 1f)] public float overlayAlpha = 0.45f;
    [Tooltip("สีของสินค้าตอนสแกน (ทำให้จุดคำสาปเด่นขึ้น)")]
    public Color scanDimColor = new Color(0.6f, 0.6f, 0.7f, 1f);

    [Header("Hint Text (optional)")]
    public TMP_Text hintText;

    [Header("After Cleansed")]
    public float returnDelay = 1.8f;
    [Tooltip("ชื่อซีนร้าน — ถ้าว่างจะใช้ Build Index ด้านล่าง")]
    public string shopSceneName = "";
    public int shopSceneBuildIndex = 1;

    [Header("Audio (optional)")]
    public AudioClip scanOnSfx;
    public AudioClip scanOffSfx;
    public AudioClip pointClickSfx;
    public AudioClip failSfx;
    public AudioClip successSfx;

    // ---------------------------------------------------------------

    private readonly List<CursePointMarker> markers = new List<CursePointMarker>();

    private SpriteRenderer itemRenderer;
    private AudioSource audioSource;

    private bool initialized;
    private bool scanning;
    private bool puzzleOpen;
    private bool cleansed;
    private bool leaving;

    private int solvedCount;
    private int lastWindowIndex = -1;
    private string statusMessage;

    public bool CanInteract =>
        initialized && scanning && !puzzleOpen && !cleansed && !leaving &&
        !IsPointerOverUI();

    // =====================================
    // START
    // =====================================

    private IEnumerator Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        // ปิด window ของมินิเกมทุกตัวก่อน (เปิดเฉพาะตอนคลิกจุดคำสาป)
        foreach (GameObject w in puzzleWindows)
        {
            if (w != null && w.scene.IsValid())
                w.SetActive(false);
        }

        // รอให้ ItemDisplay.Start() ใส่สปรต์ให้เสร็จก่อน
        yield return null;

        if (itemDisplay == null)
            itemDisplay = FindFirstObjectByType<ItemDisplay>();

        if (itemDisplay == null || itemDisplay.spriteRenderer == null)
        {
            Debug.LogError("CurseCleansingController: ไม่พบ ItemDisplay/SpriteRenderer");
            yield break;
        }

        if (!ItemSessionData.HasCurrentItem())
        {
            Debug.LogError("CurseCleansingController: ไม่มี Current Item (เข้าซีนนี้โดยไม่ผ่านโต๊ะ Puzzle?)");
            yield break;
        }

        itemRenderer = itemDisplay.spriteRenderer;

        cleansed = ItemSessionData.IsPuzzleSolvedFor(
            ItemSessionData.CurrentItemInstanceID
        );

        if (cleansed)
        {
            ShowCleanSprite();
        }
        else
        {
            BuildMarkers();
        }

        initialized = true;

        RefreshHint();
    }

    // =====================================
    // BUILD CURSE POINTS (ตำแหน่งคงที่ต่อสินค้า 1 ชิ้น)
    // =====================================

    private void BuildMarkers()
    {
        Sprite sprite = itemRenderer.sprite;

        if (sprite == null)
        {
            Debug.LogError("CurseCleansingController: สินค้าไม่มีสปรต์");
            return;
        }

        Bounds b = sprite.bounds;

        List<Vector3> offsets = GenerateOffsets(
            ItemSessionData.CurrentItemInstanceID,
            b,
            cursePointCount
        );

        for (int i = 0; i < offsets.Count; i++)
        {
            markers.Add(CursePointMarker.Create(
                this,
                i,
                itemRenderer.transform,
                offsets[i],
                curseColor,
                pointSize,
                itemRenderer.sortingLayerID,
                itemRenderer.sortingOrder + 3,
                customPointSprite
            ));
        }
    }

    private List<Vector3> GenerateOffsets(string seedSource, Bounds b, int count)
    {
        System.Random rng = new System.Random(StableHash(seedSource));

        List<Vector3> result = new List<Vector3>();

        float minSep = Mathf.Min(b.extents.x, b.extents.y) * 0.7f;

        for (int i = 0; i < count; i++)
        {
            Vector3 best = b.center;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                double ang = rng.NextDouble() * Math.PI * 2.0;
                double rad = Math.Sqrt(rng.NextDouble()) * placementArea;

                Vector3 p = new Vector3(
                    b.center.x + (float)(Math.Cos(ang) * rad) * b.extents.x,
                    b.center.y + (float)(Math.Sin(ang) * rad) * b.extents.y,
                    0f
                );

                best = p;

                bool farEnough = true;

                foreach (Vector3 other in result)
                {
                    if (Vector3.Distance(p, other) < minSep)
                    {
                        farEnough = false;
                        break;
                    }
                }

                if (farEnough)
                    break;
            }

            result.Add(best);
        }

        return result;
    }

    // hash คงที่ (string.GetHashCode ไม่รับประกันว่าเท่ากันทุกครั้ง)
    private static int StableHash(string s)
    {
        unchecked
        {
            uint h = 2166136261;

            if (!string.IsNullOrEmpty(s))
            {
                foreach (char c in s)
                {
                    h ^= c;
                    h *= 16777619;
                }
            }

            return (int)h;
        }
    }

    // =====================================
    // UPDATE
    // =====================================

    private void Update()
    {
        UpdateVisuals();

        if (!initialized || leaving || puzzleOpen || cleansed)
            return;

        if (Input.GetKeyDown(scanKey))
            SetScanning(!scanning);
    }

    private void UpdateVisuals()
    {
        float dt = Time.unscaledDeltaTime;

        if (scanOverlay != null)
        {
            scanOverlay.alpha = Mathf.MoveTowards(
                scanOverlay.alpha,
                scanning ? overlayAlpha : 0f,
                dt * 4f
            );
        }

        if (itemRenderer != null)
        {
            Color target = scanning ? scanDimColor : Color.white;

            itemRenderer.color = Color.Lerp(
                itemRenderer.color,
                target,
                1f - Mathf.Exp(-8f * dt)
            );
        }
    }

    // =====================================
    // SCAN MODE
    // =====================================

    private void SetScanning(bool on)
    {
        if (scanning == on)
            return;

        scanning = on;

        for (int i = 0; i < markers.Count; i++)
        {
            if (markers[i].IsSolved)
                continue;

            if (on)
                markers[i].Show(0.25f + i * revealStagger);
            else
                markers[i].Hide();
        }

        if (on)
            StartCoroutine(ScanPulseRoutine());

        PlaySfx(on ? scanOnSfx : scanOffSfx);

        statusMessage = null;
        RefreshHint();
    }

    private IEnumerator ScanPulseRoutine()
    {
        if (itemRenderer == null)
            yield break;

        GameObject go = new GameObject("ScanPulse");
        go.transform.position = itemRenderer.bounds.center;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CurseVisuals.Ring;
        sr.sortingLayerID = itemRenderer.sortingLayerID;
        sr.sortingOrder = itemRenderer.sortingOrder + 2;

        float maxSize = Mathf.Max(itemRenderer.bounds.size.x, itemRenderer.bounds.size.y) * 2.2f;
        float duration = 0.6f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;

            float n = Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - n, 3f);

            go.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, maxSize, eased);

            Color c = curseColor;
            c.a = (1f - n) * 0.8f;
            sr.color = c;

            yield return null;
        }

        Destroy(go);
    }

    // =====================================
    // CLICK -> PUZZLE
    // =====================================

    public void OnPointClicked(CursePointMarker marker)
    {
        if (!CanInteract || marker == null || marker.IsSolved)
            return;

        // เจอคำสาปแล้ว (ใช้ระบบเดิมของ ItemSessionData)
        ItemSessionData.MarkCurseFound();

        PlaySfx(pointClickSfx);

        OpenPuzzle(marker);
    }

    private void OpenPuzzle(CursePointMarker marker)
    {
        int index = PickWindowIndex();

        if (index < 0)
        {
            Debug.LogError("CurseCleansingController: ยังไม่ได้ใส่ window ของมินิเกมในลิสต์ Puzzle Windows");
            return;
        }

        lastWindowIndex = index;

        GameObject target = puzzleWindows[index];
        GameObject spawned = null;

        if (!target.scene.IsValid())
        {
            // เป็น Prefab ใน Project -> สร้างสำเนาใต้ Canvas
            Transform parent = canvas;

            if (parent == null)
            {
                Canvas c = FindFirstObjectByType<Canvas>();
                if (c != null) parent = c.transform;
            }

            if (parent == null)
            {
                Debug.LogError("CurseCleansingController: ใส่ Prefab ไว้แต่ไม่พบ Canvas ให้ลาก Canvas ใส่ช่อง Canvas");
                return;
            }

            spawned = Instantiate(target, parent, false);
            target = spawned;
        }

        IPuzzle puzzle = FindPuzzle(target);

        if (puzzle == null)
        {
            Debug.LogError("CurseCleansingController: ไม่พบสคริปต์มินิเกม (IPuzzle) ใต้ '" +
                           puzzleWindows[index].name + "'");

            if (spawned != null)
                Destroy(spawned);

            return;
        }

        puzzleOpen = true;

        puzzle.Open(success => OnPuzzleClosed(marker, success, spawned));
    }

    private int PickWindowIndex()
    {
        List<int> valid = new List<int>();

        for (int i = 0; i < puzzleWindows.Count; i++)
        {
            if (puzzleWindows[i] != null)
                valid.Add(i);
        }

        if (valid.Count == 0)
            return -1;

        if (valid.Count > 1)
            valid.Remove(lastWindowIndex);

        return valid[Random.Range(0, valid.Count)];
    }

    private static IPuzzle FindPuzzle(GameObject go)
    {
        foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb is IPuzzle p)
                return p;
        }

        return null;
    }

    private void OnPuzzleClosed(CursePointMarker marker, bool success, GameObject spawned)
    {
        puzzleOpen = false;

        if (spawned != null)
            Destroy(spawned);

        // กัน timeScale ค้างที่ 0 ถ้ามินิเกมตัวไหนไม่คืนค่า
        if (Time.timeScale == 0f)
            Time.timeScale = 1f;

        if (success)
        {
            marker.MarkSolved();
            solvedCount++;

            PlaySfx(successSfx);

            if (solvedCount >= markers.Count)
            {
                StartCoroutine(CleanseRoutine());
                return;
            }

            int remaining = markers.Count - solvedCount;

            statusMessage = "Curse point cleansed! " + remaining +
                            (remaining == 1 ? " point left" : " points left");
        }
        else
        {
            marker.Shake();
            PlaySfx(failSfx);

            statusMessage = "Failed... try again";
        }

        RefreshHint();
    }

    // =====================================
    // CLEANSED
    // =====================================

    private IEnumerator CleanseRoutine()
    {
        cleansed = true;

        // บันทึกผล (ระบบเดิม) — ตอนกลับร้านจะมีเอฟเฟกต์ฉลองจากแฟลก JustSolved
        ItemSessionData.MarkPuzzleSolved();

        SetScanning(false);
        statusMessage = "Curse removed!";
        RefreshHint();

        yield return new WaitForSecondsRealtime(0.25f);

        Transform t = itemDisplay.transform;
        Vector3 baseScale = t.localScale;

        float duration = 0.55f;
        float timer = 0f;
        bool swapped = false;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float n = Mathf.Clamp01(timer / duration);

            // พองแล้วคืนตัว + แฟลชสว่างตอนเปลี่ยนสปรต์
            t.localScale = baseScale * (1f + Mathf.Sin(n * Mathf.PI) * 0.3f);

            if (!swapped && n >= 0.4f)
            {
                swapped = true;
                ShowCleanSprite();

                MagicDust.Burst(t.position, 30, new Color(0.7f, 0.5f, 1f));
            }

            yield return null;
        }

        t.localScale = baseScale;

        FloatingText.Spawn(
            "Cleansed!",
            t.position + Vector3.up * 1.2f,
            new Color(0.6f, 1f, 0.8f),
            6f
        );

        yield return new WaitForSecondsRealtime(returnDelay);

        ReturnToShop();
    }

    private void ShowCleanSprite()
    {
        if (itemRenderer == null)
            return;

        ItemData data = ItemSessionData.PendingItemData;

        if (data != null && data.cleanedSprite != null)
            itemRenderer.sprite = data.cleanedSprite;
    }

    // =====================================
    // LEAVE
    // =====================================

    /// <summary>ผูกกับปุ่ม "กลับร้าน" ได้</summary>
    public void ReturnToShop()
    {
        if (leaving)
            return;

        leaving = true;
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(shopSceneName))
            SceneManager.LoadScene(shopSceneName);
        else
            SceneManager.LoadScene(shopSceneBuildIndex);
    }

    // =====================================
    // HELPERS
    // =====================================

    private void RefreshHint()
    {
        if (hintText == null)
            return;

        if (!string.IsNullOrEmpty(statusMessage))
        {
            hintText.text = statusMessage;
            return;
        }

        if (cleansed)
            hintText.text = "This item is already cleansed";
        else if (!scanning)
            hintText.text = "Press [" + scanKey + "] to scan for curses";
        else
            hintText.text = "Click a curse point (" + (markers.Count - solvedCount) +
                            " left) - press [" + scanKey + "] to stop scanning";
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.pitch = Random.Range(0.97f, 1.04f);
        audioSource.PlayOneShot(clip);
    }
}
