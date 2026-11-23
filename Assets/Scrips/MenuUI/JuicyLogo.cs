
using UnityEngine;
using System.Collections;

public class JuicyLogo : MonoBehaviour
{
    [Header("Juicy Settings")]
    public float bounceAmount = 0.08f;
    public float floatAmount = 8f;
    public float rotateAmount = 2f;
    public float speed = 2f;

    private RectTransform rect;
    private Vector3 originalScale;
    private Vector2 originalPosition;
    private Quaternion originalRotation;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();

        originalScale = rect.localScale;
        originalPosition = rect.anchoredPosition;
        originalRotation = rect.localRotation;
    }

    private void Start()
    {
        StartCoroutine(IntroAnimation());
    }

    private void Update()
    {
        float t = Time.unscaledTime * speed;

        // ลอยขึ้นลง
        float floatY = Mathf.Sin(t) * floatAmount;

        // เอียงซ้ายขวา
        float rotation = Mathf.Sin(t * 0.7f) * rotateAmount;

        // ขยายและหดเล็กน้อย
        float scale = 1f + Mathf.Sin(t * 1.5f) * bounceAmount;

        rect.anchoredPosition =
            originalPosition + new Vector2(0, floatY);

        rect.localRotation =
            originalRotation * Quaternion.Euler(0, 0, rotation);

        rect.localScale = originalScale * scale;
    }

    IEnumerator IntroAnimation()
    {
        float duration = 0.5f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / duration);

            // เด้งเข้ามาแบบ Overshoot
            float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.25f;

            rect.localScale = originalScale * scale;

            yield return null;
        }

        rect.localScale = originalScale;
    }
}
