using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// ข้อความลอยใน World Space (เช่น +100, สะอาดแล้ว!) เด้งออกมา ลอยขึ้น แล้วจางหาย
/// เรียกใช้: FloatingText.Spawn("+100", position, Color.yellow);
/// </summary>
public class FloatingText : MonoBehaviour
{
    private float rise;
    private float duration;
    private TextMeshPro tmp;
    private Vector3 startPos;
    private Color baseColor;

    public static FloatingText Spawn(
        string text,
        Vector3 worldPosition,
        Color color,
        float fontSize = 5f,
        float rise = 1.2f,
        float duration = 1.1f)
    {
        GameObject go = new GameObject("FloatingText");
        go.transform.position = worldPosition;

        TextMeshPro t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.sortingOrder = 100;
        t.rectTransform.sizeDelta = new Vector2(12f, 3f);
        t.outlineColor = new Color32(0, 0, 0, 255);
        t.outlineWidth = 0.25f;

        FloatingText ft = go.AddComponent<FloatingText>();
        ft.tmp = t;
        ft.rise = rise;
        ft.duration = Mathf.Max(duration, 0.2f);
        ft.startPos = worldPosition;
        ft.baseColor = color;

        ft.StartCoroutine(ft.Animate());

        return ft;
    }

    private IEnumerator Animate()
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;

            float n = Mathf.Clamp01(t / duration);

            // เด้งใหญ่ตอนออกมา (ease-out-back) แล้วนิ่ง
            float popT = Mathf.Clamp01(t / 0.22f);
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            float pop = 1f + c3 * Mathf.Pow(popT - 1f, 3) + c1 * Mathf.Pow(popT - 1f, 2);

            transform.localScale = Vector3.one * pop;

            // ลอยขึ้นแบบ ease-out
            float eased = 1f - Mathf.Pow(1f - n, 3f);
            transform.position = startPos + Vector3.up * (rise * eased);

            // จางหายช่วง 40% สุดท้าย
            Color c = baseColor;
            c.a = baseColor.a * (1f - Mathf.Clamp01((n - 0.6f) / 0.4f));
            tmp.color = c;

            yield return null;
        }

        Destroy(gameObject);
    }
}
