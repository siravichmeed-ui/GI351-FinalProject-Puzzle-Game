using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// แผงสรุปวัน — สร้าง UI เองด้วยโค้ดตอนเริ่ม (ไม่ต้องตั้งค่าใน Scene)
/// ShopDayManager จะเพิ่มสคริปต์นี้ให้เองถ้ายังไม่มี
/// ใช้เวลาแบบ unscaled เพราะตอนแสดงเกมหยุด (Time.timeScale = 0)
/// </summary>
public class DaySummaryPanel : MonoBehaviour
{
    private const string Green = "#7CFFB0";
    private const string Red = "#FF6B6B";
    private const string Yellow = "#FFD866";

    private GameObject canvasRoot;
    private CanvasGroup group;
    private RectTransform card;
    private TextMeshProUGUI titleText, bodyText, buttonText;
    private Button button;

    private DaySummary current;
    private bool waitingInput;
    private Coroutine routine;

    // =====================================
    // LIFETIME
    // =====================================

    private void Start()
    {
        Build();

        if (ShopDayManager.Instance != null)
            ShopDayManager.Instance.OnDaySummaryReady += Show;
    }

    private void OnDestroy()
    {
        if (ShopDayManager.Instance != null)
            ShopDayManager.Instance.OnDaySummaryReady -= Show;

        if (canvasRoot != null)
            Destroy(canvasRoot);
    }

    private void Update()
    {
        if (waitingInput &&
            (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            OnContinue();
    }

    // =====================================
    // SHOW / HIDE
    // =====================================

    private void Show(DaySummary summary)
    {
        if (canvasRoot == null)
            return;

        current = summary;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        List<string> lines = BuildLines(current);

        titleText.text = current.bankrupt
            ? "<color=" + Red + ">BANKRUPT</color>"
            : "DAY " + current.day + " COMPLETE";

        bodyText.text = "";
        button.gameObject.SetActive(false);
        buttonText.text = current.bankrupt
            ? "GAME OVER"
            : "START DAY " + (current.day + 1);

        group.blocksRaycasts = true;
        group.interactable = false;

        // เฟดเข้า + การ์ดเด้ง
        float t = 0f;

        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;

            float n = Mathf.Clamp01(t / 0.3f);

            group.alpha = n;
            card.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(n));

            yield return null;
        }

        group.alpha = 1f;
        card.localScale = Vector3.one;

        // ทยอยโชว์ทีละบรรทัด
        List<string> shown = new List<string>();

        foreach (string line in lines)
        {
            shown.Add(line);
            bodyText.text = string.Join("\n", shown);

            yield return new WaitForSecondsRealtime(0.35f);
        }

        // ปุ่มเด้งขึ้นมา
        button.gameObject.SetActive(true);
        group.interactable = true;
        waitingInput = true;

        RectTransform br = (RectTransform)button.transform;
        t = 0f;

        while (t < 0.25f)
        {
            t += Time.unscaledDeltaTime;
            br.localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, 1f, EaseOutBack(Mathf.Clamp01(t / 0.25f)));
            yield return null;
        }

        br.localScale = Vector3.one;

        routine = null;
    }

    private void OnContinue()
    {
        if (!waitingInput)
            return;

        waitingInput = false;
        group.interactable = false;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(HideRoutine());
    }

    private IEnumerator HideRoutine()
    {
        float t = 0f;

        while (t < 0.25f)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = 1f - Mathf.Clamp01(t / 0.25f);
            yield return null;
        }

        group.alpha = 0f;
        group.blocksRaycasts = false;

        routine = null;

        ShopDayManager day = ShopDayManager.Instance;

        if (day != null)
        {
            if (current != null && current.bankrupt)
                day.ConfirmGameOver();
            else
                day.ConfirmNextDay();
        }
    }

    // =====================================
    // CONTENT
    // =====================================

    private static List<string> BuildLines(DaySummary s)
    {
        List<string> lines = new List<string>
        {
            Row("Earned today", "$ " + s.earnings.ToString("N0"), Green),
            Row("Rent due", "$ " + s.rentDue.ToString("N0"), null),
            Row("Rent status", s.rentPaid ? "PAID" : "UNPAID", s.rentPaid ? Green : Red),
            Row("Balance", "$ " + s.moneyAfter.ToString("N0"), Yellow)
        };

        lines.Add("");

        if (s.bankrupt)
        {
            lines.Add("<color=" + Red + ">You couldn't pay the rent in time.\nThe shop has been shut down.</color>");
        }
        else if (!s.rentPaid)
        {
            lines.Add("<color=" + Red + ">You owe $ " + s.unpaidRent.ToString("N0") + ".\nPay it within " +
                      s.graceDaysLeft + (s.graceDaysLeft == 1 ? " day" : " days") +
                      " or you will go bankrupt!</color>");
        }
        else
        {
            lines.Add("A new day awaits...");
        }

        return lines;
    }

    private static string Row(string label, string value, string colorHex)
    {
        string v = colorHex != null
            ? "<color=" + colorHex + ">" + value + "</color>"
            : value;

        return label + "<pos=62%>" + v;
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;

        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // =====================================
    // BUILD UI (procedural)
    // =====================================

    private void Build()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        canvasRoot = new GameObject("DaySummaryCanvas");

        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasRoot.AddComponent<GraphicRaycaster>();

        group = canvasRoot.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        // พื้นหลังมืดเต็มจอ
        Image dim = NewImage("Dim", canvasRoot.transform, new Color(0f, 0f, 0f, 0.75f));
        Stretch(dim.rectTransform);

        // การ์ด
        Image cardImage = NewImage("Card", canvasRoot.transform, new Color(0.09f, 0.06f, 0.13f, 0.98f));
        card = cardImage.rectTransform;
        Center(card, new Vector2(820f, 620f), Vector2.zero);

        // เส้นสีม่วงใต้หัวข้อ
        Image accent = NewImage("Accent", card, new Color(0.65f, 0.3f, 1f, 1f));
        Center(accent.rectTransform, new Vector2(660f, 4f), new Vector2(0f, 200f));

        titleText = NewText("Title", card, 56, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        Center(titleText.rectTransform, new Vector2(760f, 90f), new Vector2(0f, 245f));

        bodyText = NewText("Body", card, 34, FontStyles.Normal, new Color(0.92f, 0.9f, 0.95f), TextAlignmentOptions.TopLeft);
        bodyText.richText = true;
        Center(bodyText.rectTransform, new Vector2(700f, 330f), new Vector2(0f, 20f));

        // ปุ่ม
        Image buttonImage = NewImage("Button", card, new Color(0.55f, 0.25f, 0.85f, 1f));
        buttonImage.raycastTarget = true;
        Center(buttonImage.rectTransform, new Vector2(440f, 84f), new Vector2(0f, -250f));

        button = buttonImage.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(OnContinue);

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.85f, 1f, 1f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.9f, 1f);
        button.colors = colors;

        buttonText = NewText("Label", buttonImage.transform, 36, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        Stretch(buttonText.rectTransform);

        button.gameObject.SetActive(false);
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        return img;
    }

    private static TextMeshProUGUI NewText(
        string name, Transform parent, float size, FontStyles style,
        Color color, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;

        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rt, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }
}
