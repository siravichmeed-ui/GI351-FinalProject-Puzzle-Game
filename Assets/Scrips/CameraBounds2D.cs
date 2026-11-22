using UnityEngine;

/// <summary>
/// สร้างกำแพงล่องหน 4 ด้านตามขอบกล้อง (Orthographic) ไม่ให้สินค้าตกหรือกระเด็นออกนอกจอ
/// - ปรับตามขนาดจอ/ซูมกล้องอัตโนมัติ
/// - จำกัดตำแหน่งตอนลากสินค้าให้อยู่ในจอ
/// - ถ้าสินค้า (Dynamic) หลุดออกไปได้ จะถูกดึงกลับเข้ามา
/// วางไว้บน GameObject ว่างๆ ใน Shop Scene 1 ตัว
/// </summary>
[DefaultExecutionOrder(-500)]
public class CameraBounds2D : MonoBehaviour
{
    public static CameraBounds2D Instance { get; private set; }

    [Header("Camera")]
    [Tooltip("ถ้าว่างจะใช้ Camera.main")]
    public Camera targetCamera;

    [Header("Walls")]
    public bool blockLeft = true;
    public bool blockRight = true;
    public bool blockBottom = true;
    public bool blockTop = true;

    [Tooltip("ร่นกำแพงเข้าจากขอบจอ (หน่วย world)")]
    public float padding = 0f;
    public float wallThickness = 2f;
    public PhysicsMaterial2D wallMaterial;

    [Header("Rescue")]
    [Tooltip("ดึงสินค้าที่หลุดออกนอกกรอบกลับเข้ามา")]
    public bool rescueItems = true;
    public float rescueMargin = 0.3f;

    [Header("Debug")]
    public bool drawGizmo = true;

    private BoxCollider2D left, right, bottom, top;

    private float lastSize;
    private float lastAspect;
    private Vector3 lastCamPos;

    /// <summary>พื้นที่ด้านในกรอบ (world space)</summary>
    public Rect View { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (targetCamera == null)
            targetCamera = Camera.main;

        gameObject.layer = 2; // Ignore Raycast: ไม่รบกวนการคลิก

        left = CreateWall("Wall_Left");
        right = CreateWall("Wall_Right");
        bottom = CreateWall("Wall_Bottom");
        top = CreateWall("Wall_Top");

        Rebuild();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private BoxCollider2D CreateWall(string wallName)
    {
        GameObject go = new GameObject(wallName);
        go.layer = 2;
        go.transform.SetParent(transform, false);

        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.sharedMaterial = wallMaterial;

        return box;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            return;
        }

        // ไม่ติดตามตำแหน่งกล้องทุกเฟรม (กันกำแพงสั่นตอนกล้อง shake)
        // สร้างใหม่เมื่อขนาดจอ/ซูมเปลี่ยน หรือกล้องถูกย้ายจริงๆ
        bool moved = (targetCamera.transform.position - lastCamPos).sqrMagnitude > 0.25f;

        if (!Mathf.Approximately(targetCamera.orthographicSize, lastSize) ||
            !Mathf.Approximately(targetCamera.aspect, lastAspect) ||
            moved)
            Rebuild();

        if (rescueItems)
            RescueItems();
    }

    // =====================================
    // BUILD
    // =====================================

    public void Rebuild()
    {
        if (targetCamera == null || !targetCamera.orthographic)
        {
            Debug.LogWarning("CameraBounds2D: ต้องใช้ Orthographic Camera", this);
            return;
        }

        lastSize = targetCamera.orthographicSize;
        lastAspect = targetCamera.aspect;
        lastCamPos = targetCamera.transform.position;

        float halfH = lastSize - padding;
        float halfW = lastSize * lastAspect - padding;

        Vector2 c = lastCamPos;

        View = new Rect(c.x - halfW, c.y - halfH, halfW * 2f, halfH * 2f);

        float t = Mathf.Max(wallThickness, 0.1f);

        SetWall(left, blockLeft,
            new Vector2(View.xMin - t * 0.5f, c.y),
            new Vector2(t, View.height + t * 2f));

        SetWall(right, blockRight,
            new Vector2(View.xMax + t * 0.5f, c.y),
            new Vector2(t, View.height + t * 2f));

        SetWall(bottom, blockBottom,
            new Vector2(c.x, View.yMin - t * 0.5f),
            new Vector2(View.width + t * 2f, t));

        SetWall(top, blockTop,
            new Vector2(c.x, View.yMax + t * 0.5f),
            new Vector2(View.width + t * 2f, t));
    }

    private static void SetWall(BoxCollider2D wall, bool enabled, Vector2 pos, Vector2 size)
    {
        wall.enabled = enabled;
        wall.transform.position = pos;
        wall.size = size;
    }

    // =====================================
    // API
    // =====================================

    /// <summary>จำกัดตำแหน่งให้อยู่ในกรอบ โดยเผื่อขนาดวัตถุ (extents)</summary>
    public Vector2 ClampToView(Vector2 position, Vector2 extents)
    {
        float minX = View.xMin + extents.x;
        float maxX = View.xMax - extents.x;
        float minY = View.yMin + extents.y;
        float maxY = View.yMax - extents.y;

        // ถ้าวัตถุใหญ่กว่ากรอบ ให้ล็อกที่กึ่งกลาง
        position.x = minX > maxX ? View.center.x : Mathf.Clamp(position.x, minX, maxX);
        position.y = minY > maxY ? View.center.y : Mathf.Clamp(position.y, minY, maxY);

        return position;
    }

    // =====================================
    // RESCUE
    // =====================================

    private void RescueItems()
    {
        Rect limit = new Rect(
            View.xMin - rescueMargin,
            View.yMin - rescueMargin,
            View.width + rescueMargin * 2f,
            View.height + rescueMargin * 2f
        );

        foreach (ItemPersistenceData item in ItemPersistenceData.All)
        {
            if (item == null)
                continue;

            Rigidbody2D rb = item.GetComponent<Rigidbody2D>();

            // Kinematic = กำลังลาก / กำลังส่งของ ปล่อยให้ระบบนั้นจัดการเอง
            if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
                continue;

            if (limit.Contains(rb.position))
                continue;

            Collider2D col = item.GetComponent<Collider2D>();
            Vector2 extents = col != null ? (Vector2)col.bounds.extents : Vector2.zero;

            rb.position = ClampToView(rb.position, extents);
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            Debug.Log("CameraBounds2D: rescued item " + item.instanceID);
        }
    }

    // =====================================
    // GIZMO
    // =====================================

    private void OnDrawGizmos()
    {
        if (!drawGizmo)
            return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;

        if (cam == null || !cam.orthographic)
            return;

        float halfH = cam.orthographicSize - padding;
        float halfW = cam.orthographicSize * cam.aspect - padding;

        Vector3 c = cam.transform.position;

        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(
            new Vector3(c.x, c.y, 0f),
            new Vector3(halfW * 2f, halfH * 2f, 0f)
        );
    }
}
