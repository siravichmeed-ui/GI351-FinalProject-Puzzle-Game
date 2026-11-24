
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuTransition : MonoBehaviour
{
    [Header("Menu UI")]
    public CanvasGroup menuUI;

    [Header("Shop Background")]
    public RectTransform background;

    [Header("Camera Zoom")]
    public float zoomScale = 1.3f;
    public float zoomDuration = 1.5f;

    [Header("Menu Fade")]
    public float fadeDuration = 0.5f;

    [Header("Smoke Animation")]
    public CanvasGroup smoke;
    public Animator smokeAnimator;
    public float smokeDuration = 1.5f;

    [Header("Scene")]
    public string nextSceneName = "ShopScene";

    private Vector3 originalBackgroundScale;
    private bool isTransitioning = false;

    void Start()
    {
        // จำขนาดพื้นหลังเริ่มต้น
        originalBackgroundScale = background.localScale;

        // ตั้งค่าเมนู
        menuUI.alpha = 1f;
        menuUI.interactable = true;
        menuUI.blocksRaycasts = true;

        // ซ่อนควันตอนเริ่มเกม
        smoke.alpha = 0f;
        smoke.interactable = false;
        smoke.blocksRaycasts = false;
    }

    public void OnPlayButton()
    {
        if (isTransitioning) return;

        isTransitioning = true;
        StartCoroutine(PlayTransition());
    }

    IEnumerator PlayTransition()
    {
        // ปิดการกดปุ่มเมนู
        menuUI.interactable = false;
        menuUI.blocksRaycasts = false;

        // เริ่มแสดงควันทันที
        smoke.alpha = 1f;

        // เริ่ม Animation ควัน
        smokeAnimator.Rebind();
        smokeAnimator.Update(0f);
        smokeAnimator.Play("Smoke_Expand", 0, 0f);

        // เริ่ม Zoom และ Fade พร้อมกัน
        float timer = 0f;

        while (timer < zoomDuration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / zoomDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            // Fade เมนู
            float fadeT = Mathf.Clamp01(timer / fadeDuration);
            menuUI.alpha = 1f - fadeT;

            // Zoom พื้นหลัง
            float currentScale = Mathf.Lerp(1f, zoomScale, smooth);
            background.localScale = originalBackgroundScale * currentScale;

            yield return null;
        }

        menuUI.alpha = 0f;

        // รอให้ Animation ควันเล่นครบ
        float remainingTime = Mathf.Max(0f, smokeDuration - zoomDuration);

        yield return new WaitForSeconds(remainingTime);

        // เปลี่ยน Scene
        SceneManager.LoadScene(nextSceneName);
    }
}
