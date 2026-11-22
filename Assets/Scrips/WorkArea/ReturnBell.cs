using System.Collections;
using TMPro;
using UnityEngine;

public class ReturnBell : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ReturnArea returnArea;
    [SerializeField] private CustomerSpawnManager customerManager;

    [Header("Input")]
    [Tooltip("คลิกที่กระดิ่ง (ต้องมี Collider2D) เพื่อกด — ถ้าใช้ UI Button ให้ปิด")]
    [SerializeField] private bool clickToRing = true;
    [SerializeField] private float cooldown = 0.35f;

    [Header("Juice")]
    [Tooltip("ถ้าว่างจะใช้ Transform ของตัวเอง")]
    [SerializeField] private Transform visual;
    [SerializeField] private float swingAngle = 18f;
    [SerializeField] private float swingDuration = 0.7f;
    [SerializeField] private float punchScale = 0.15f;

    [Header("Feedback Text (optional)")]
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private float feedbackDuration = 1.6f;

    [Header("Audio (optional)")]
    [SerializeField] private AudioClip ringSfx;
    [SerializeField] private AudioClip rejectSfx;

    private AudioSource audioSource;
    private Quaternion baseRotation;
    private Vector3 baseScale;
    private float nextRingTime;

    private Coroutine swingRoutine;
    private Coroutine feedbackRoutine;

    private void Awake()
    {
        if (visual == null)
            visual = transform;

        baseRotation = visual.localRotation;
        baseScale = visual.localScale;

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        if (feedbackText != null)
            feedbackText.alpha = 0f;
    }

    private void OnMouseDown()
    {
        if (clickToRing)
            RingBell();
    }

    public void RingBell()
    {
        if (Time.unscaledTime < nextRingTime)
            return;

        nextRingTime = Time.unscaledTime + cooldown;

        if (returnArea == null || customerManager == null)
        {
            Debug.LogWarning("ReturnBell: Missing references!");
            return;
        }

        ReturnRequestResult result =
            customerManager.BeginReturn(returnArea.GetCurrentItem());

        if (result == ReturnRequestResult.Started)
        {
            returnArea.Reserve();
            returnArea.Pulse();

            Swing(1f);
            PlaySfx(ringSfx);
        }
        else
        {
            Swing(0.3f);
            PlaySfx(rejectSfx);
        }

        ShowMessage(MessageFor(result));
    }

    private static string MessageFor(ReturnRequestResult result)
    {
        switch (result)
        {
            case ReturnRequestResult.Started:
                return "เรียกเจ้าของสินค้ากลับมาแล้ว!";
            case ReturnRequestResult.NoItem:
                return "วางสินค้าที่จุดคืนของก่อน";
            case ReturnRequestResult.NotSolved:
                return "สินค้ายังไม่ได้ผ่าน Puzzle!";
            case ReturnRequestResult.ReturnBusy:
                return "มีลูกค้ากำลังมารับของอยู่";
            case ReturnRequestResult.MissingPoints:
                return "ตั้งค่าจุดรับของยังไม่ครบ";
            default:
                return "ไม่พบเจ้าของสินค้าชิ้นนี้";
        }
    }

    // =====================================
    // JUICE
    // =====================================

    private void Swing(float strength)
    {
        if (swingRoutine != null)
            StopCoroutine(swingRoutine);

        swingRoutine = StartCoroutine(SwingRoutine(strength));
    }

    private IEnumerator SwingRoutine(float strength)
    {
        float t = 0f;
        float duration = Mathf.Max(swingDuration, 0.05f);

        while (t < duration)
        {
            t += Time.deltaTime;

            float n = t / duration;
            float decay = Mathf.Exp(-5f * n);

            float angle = swingAngle * strength * decay *
                          Mathf.Sin(n * Mathf.PI * 6f);

            float punch = 1f + punchScale * strength * Mathf.Exp(-10f * n);

            visual.localRotation = baseRotation * Quaternion.Euler(0f, 0f, angle);
            visual.localScale = baseScale * punch;

            yield return null;
        }

        visual.localRotation = baseRotation;
        visual.localScale = baseScale;
        swingRoutine = null;
    }

    private void ShowMessage(string message)
    {
        Debug.Log(message);

        if (feedbackText == null)
            return;

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);

        feedbackRoutine = StartCoroutine(FeedbackRoutine(message));
    }

    private IEnumerator FeedbackRoutine(string message)
    {
        feedbackText.text = message;
        feedbackText.alpha = 1f;

        yield return new WaitForSeconds(feedbackDuration);

        float t = 0f;

        while (t < 0.3f)
        {
            t += Time.deltaTime;
            feedbackText.alpha = 1f - Mathf.Clamp01(t / 0.3f);
            yield return null;
        }

        feedbackText.alpha = 0f;
        feedbackRoutine = null;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.pitch = Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(clip);
    }
}
