using UnityEngine;
using UnityEngine.UI;

/// <summary>กราฟิกรูปหลายเหลี่ยม 1 เศษ: โหมดเติมสี (ใช้ texture ของรูป) หรือโหมดเส้นขอบ (รอยร้าว)</summary>
public class MirrorShardGraphic : MaskableGraphic, ICanvasRaycastFilter
{
    public Vector2[] poly;      // จุดของรูปหลายเหลี่ยม (เทียบกับจุด pivot ของ RectTransform)
    public Vector2[] uv;        // uv ของแต่ละจุด (ใช้เฉพาะโหมดเติมสี)
    public Texture tex;
    public bool outline;
    public float thickness = 2f;
    public float shade = 1f;    // ความสว่างของเศษ (ทำให้เห็นเป็นเหลี่ยมกระจก)
    public float grabSlop = 8f; // คลิกเลยขอบออกไปได้เล็กน้อย จับเศษบางๆ ได้ง่ายขึ้น

    public override Texture mainTexture => (!outline && tex != null) ? tex : base.mainTexture;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (poly == null || poly.Length < 3) return;

        if (!outline)
        {
            Color32 c = new Color(color.r * shade, color.g * shade, color.b * shade, color.a);
            for (int i = 0; i < poly.Length; i++)
                vh.AddVert(poly[i], c, (uv != null && i < uv.Length) ? uv[i] : Vector2.zero);
            for (int i = 1; i < poly.Length - 1; i++)
                vh.AddTriangle(0, i, i + 1);
        }
        else
        {
            Color32 c = color;
            int n = poly.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n];
                Vector2 d = b - a;
                float len = d.magnitude;
                if (len < 0.01f) continue;
                Vector2 nrm = new Vector2(-d.y, d.x) / len * (thickness * 0.5f);
                int s = vh.currentVertCount;
                vh.AddVert(a - nrm, c, Vector2.zero);
                vh.AddVert(a + nrm, c, Vector2.zero);
                vh.AddVert(b + nrm, c, Vector2.zero);
                vh.AddVert(b - nrm, c, Vector2.zero);
                vh.AddTriangle(s, s + 1, s + 2);
                vh.AddTriangle(s, s + 2, s + 3);
            }
        }
    }

    // ให้คลิกโดนเฉพาะ "ตัวเศษ" จริง ๆ ไม่ใช่สี่เหลี่ยมที่ครอบ
    public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
    {
        if (outline || poly == null || poly.Length < 3) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, sp, eventCamera, out Vector2 lp))
            return false;
        if (PointInPoly(lp)) return true;
        for (int i = 0; i < poly.Length; i++)
            if (DistToSeg(lp, poly[i], poly[(i + 1) % poly.Length]) <= grabSlop) return true;
        return false;
    }

    private bool PointInPoly(Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                inside = !inside;
        }
        return inside;
    }

    private static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float l2 = ab.sqrMagnitude;
        if (l2 < 1e-6f) return (p - a).magnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
        return (p - (a + ab * t)).magnitude;
    }
}
