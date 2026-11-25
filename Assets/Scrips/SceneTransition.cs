using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ควันคลุมจอก่อนเปลี่ยนซีน (เหมือนตอนกด Play ในเมนู) และควันจางออกเมื่อซีนใหม่เริ่ม
/// วางไว้ในทุกซีนที่ต้องการ (ซีนร้าน + ซีนล้างคำสาป) แล้วลาก Canvas ควันมาใส่
///   ใช้: SceneTransition.Load("CleansingScene");   (ไม่มี SceneTransition ในซีน = โหลดเลย)
/// ตั้ง Smoke ให้เหมือนใน MainMenuTransition: CanvasGroup + Animator ที่มี state "Smoke_Expand"
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    /// <summary>กำลังเปลี่ยนซีน — ใช้บล็อกการคลิกของในฉาก</summary>
    public static bool IsTransitioning { get; private set; }

    private static bool pendingIntro;

    [Header("Smoke")]
    [Tooltip("CanvasGroup ของควัน (เต็มจอ อยู่หน้าสุดของ Canvas)")]
    public CanvasGroup smoke;
    public Animator smokeAnimator;
    [Tooltip("ชื่อ state ของ Animation ควันที่ขยายคลุมจอ")]
    public string animationState = "Smoke_Expand";
    [Tooltip("เวลารอให้ควันคลุมจอก่อนโหลดซีน (วินาที)")]
    public float duration = 1.5f;

    [Header("Intro (ควันจางออกตอนซีนใหม่เริ่ม)")]
    public bool playIntro = true;
    public float introHold = 0.15f;
    public float introFadeDuration = 0.7f;

    [Header("Audio (optional)")]
    public AudioClip transitionSfx;

    private AudioSource audioSource;

    // =====================================
    // STATIC API
    // =====================================

    public static void Load(string sceneName)
    {
        if (Instance != null)
            Instance.StartLoad(sceneName, -1);
        else
            SceneManager.LoadScene(sceneName);
    }

    public static void Load(int buildIndex)
    {
        if (Instance != null)
            Instance.StartLoad(null, buildIndex);
        else
            SceneManager.LoadScene(buildIndex);
    }

    /// <summary>
    /// เรียกก่อนโหลดซีนด้วยวิธีอื่น (เช่นจากเมนู) เพื่อให้ซีนใหม่เล่นควันจางออกตอนเริ่ม
    /// </summary>
    public static void RequestIntro()
    {
        pendingIntro = true;
    }

    // =====================================
    // LIFETIME
    // =====================================

    private void Awake()
    {
        Instance = this;
        IsTransitioning = false;

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        if (smoke != null)
        {
            smoke.alpha = 0f;
            smoke.interactable = false;
            smoke.blocksRaycasts = false;
        }
    }

    private void Start()
    {
        if (pendingIntro && playIntro && smoke != null)
            StartCoroutine(IntroRoutine());

        pendingIntro = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =====================================
    // LOAD
    // =====================================

    private void StartLoad(string sceneName, int buildIndex)
    {
        if (IsTransitioning)
            return;

        StartCoroutine(LoadRoutine(sceneName, buildIndex));
    }

    private IEnumerator LoadRoutine(string sceneName, int buildIndex)
    {
        IsTransitioning = true;

        if (smoke != null)
        {
            smoke.alpha = 1f;
            smoke.blocksRaycasts = true;

            PlayExpand(0f);
        }

        if (transitionSfx != null)
            audioSource.PlayOneShot(transitionSfx);

        float t = 0f;

        // เวลาจริง: ไม่ค้างถ้า timeScale เป็น 0
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        pendingIntro = true;

        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
        else
            SceneManager.LoadScene(buildIndex);
    }

    private IEnumerator IntroRoutine()
    {
        IsTransitioning = true;

        smoke.alpha = 1f;
        smoke.blocksRaycasts = true;

        // แสดงควันในท่าที่คลุมจอเต็มที่ แล้วค่อยจางออก
        PlayExpand(1f);

        float t = 0f;

        while (t < introHold)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;

        while (t < introFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            smoke.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(introFadeDuration, 0.01f)));
            yield return null;
        }

        smoke.alpha = 0f;
        smoke.blocksRaycasts = false;

        IsTransitioning = false;
    }

    private void PlayExpand(float normalizedTime)
    {
        if (smokeAnimator == null || string.IsNullOrEmpty(animationState))
            return;

        smokeAnimator.Rebind();
        smokeAnimator.Update(0f);
        smokeAnimator.Play(animationState, 0, normalizedTime);
        smokeAnimator.Update(0f);
    }
}
