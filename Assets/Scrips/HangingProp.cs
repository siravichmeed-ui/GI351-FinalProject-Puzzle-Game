using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ของแขวนที่แกว่งได้เหมือนจริง (โคมไฟ ป้าย โมบาย พวงมาลัย สมุนไพรแขวน ฯลฯ)
/// - แกว่งแบบลูกตุ้ม (สปริง + หน่วง) รอบ "จุดแขวน" มีลมพัดเบาๆ ตลอดเวลา
/// - ตอบสนองต่อเหตุการณ์ในเกม: ลูกค้าวิ่งผ่าน, ของตกโต๊ะ, สั่นกระดิ่ง, เอาเมาส์ปัดผ่าน
/// - ซ้อนกันได้ (เช่น โคมไฟห้อยจากป้าย) ตัวลูกจะแกว่งตามตัวแม่แบบหน่วงๆ เอง
/// - ใส่เอฟเฟกต์ไฟกะพริบให้โคมไฟได้ (flickerTarget)
/// วิธีใช้: เลือก object ของแขวน (เลือกหลายตัวพร้อมกันได้) แล้ว Add Component ตัวนี้
///          จุดแขวนจะถูกตั้งไว้ที่ขอบบนของภาพให้อัตโนมัติ ปรับได้ที่ Pivot Offset
/// </summary>
public class HangingProp : MonoBehaviour
{
    [Header("Pivot (จุดที่แขวน)")]
    [Tooltip("จุดแขวนในพิกัด local ของ object นี้ — ตั้งอัตโนมัติที่ขอบบนของภาพตอนเพิ่ม component")]
    public Vector2 pivotOffset;

    [Header("Swing")]
    [Tooltip("ความถี่แกว่งธรรมชาติ (รอบ/วินาที) ของยาวจะแกว่งช้า ~0.4 ของสั้นแกว่งเร็ว ~1.0")]
    public float swingFrequency = 0.6f;
    [Range(0f, 1f)] public float frequencyVariance = 0.15f;
    [Tooltip("ยิ่งน้อยยิ่งแกว่งนาน")]
    [Range(0.01f, 1f)] public float damping = 0.12f;
    public float maxAngle = 35f;

    [Header("Idle Wind (ลมเบาๆ)")]
    [Tooltip("มุมที่ถูกลมพัดเอียงไปมา (องศา) 0 = นิ่งสนิท")]
    public float windAngle = 3f;
    public float windSpeed = 0.35f;
    [Tooltip("แรงลมกระโชกสุ่ม (0 = ไม่มี)")]
    public float gustStrength = 6f;
    public Vector2 gustInterval = new Vector2(3f, 9f);

    [Header("Reactions")]
    [Tooltip("ความไวต่อแรงกระแทก/ลมจากลูกค้า (ของเบา = มาก ของหนัก = น้อย)")]
    public float sensitivity = 1f;
    public bool reactToMouse = true;
    public float mouseRadius = 0.8f;
    public float mouseForce = 60f;

    [Header("External (ให้สคริปต์อื่นขยับเพิ่ม เช่น ดึงพู่กระดิ่ง)")]
    [Tooltip("ระยะที่ถูกดึงเพิ่ม (พิกัด local ของ parent) — ReturnBell ใช้ตัวนี้")]
    public Vector2 externalOffset;

    [Header("Flicker (ไม่บังคับ — สำหรับโคมไฟ/เทียน)")]
    public SpriteRenderer flickerTarget;
    [Range(0f, 1f)] public float flickerAmount = 0.2f;
    public float flickerSpeed = 6f;


    [Header("Touch Sound")]
    public List<string> touchSoundIDs = new List<string>();



    [Range(0f, 1f)]
    public float touchVolume = 0.7f;

    [Tooltip("ระยะเวลาขั้นต่ำก่อนเล่นเสียงครั้งถัดไป")]
    public float soundCooldown = 0.3f;

    private AudioSource touchAudioSource;
    private bool mouseWasTouching;
    private float nextSoundTime;

    // ---------------------------------------------------------------

    private static readonly List<HangingProp> registry = new List<HangingProp>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => registry.Clear();

    private Vector3 baseLocalPos;
    private Quaternion baseLocalRot;
    private Vector3 baseLocalScale;

    private float angle;
    private float angVel;
    private float freq;
    private float seed;
    private float gustTimer;
    private Color flickerBase;

    private Camera cam;
    private Vector2 lastMouse;
    private bool hasLastMouse;

    // =====================================
    // STATIC API (เรียกจากสคริปต์อื่น)
    // =====================================

    /// <summary>
    /// ดันของแขวนรอบๆ ไปทางซ้าย/ขวาตามเครื่องหมายของ impulse (+ = ไปทาง +X)
    /// ใช้กับสิ่งที่เคลื่อนที่ผ่าน เช่นลูกค้าวิ่ง — ให้คูณ dt มาด้วย
    /// </summary>
    public static void Disturb(Vector2 worldPos, float impulse, float radius)
    {
        if (Mathf.Approximately(impulse, 0f))
            return;

        foreach (HangingProp p in registry)
        {
            if (p == null)
                continue;

            float d = Vector2.Distance(p.transform.position, worldPos);

            if (d >= radius)
                continue;

            float f = 1f - d / radius;

            p.angVel += impulse * f * f * p.sensitivity;
        }
    }

    /// <summary>กระแทกแล้วของแขวนรอบๆ แกว่งหนีออกจากจุดนั้น (ของตกโต๊ะ, สั่นกระดิ่ง)</summary>
    public static void Bump(Vector2 worldPos, float strength, float radius)
    {
        foreach (HangingProp p in registry)
        {
            if (p == null)
                continue;

            float d = Vector2.Distance(p.transform.position, worldPos);

            if (d >= radius)
                continue;

            float f = 1f - d / radius;
            float dir = p.transform.position.x >= worldPos.x ? 1f : -1f;

            p.angVel += dir * strength * f * f * p.sensitivity;
        }
    }

    /// <summary>ผลักตัวนี้ตัวเดียว</summary>
    public void Push(float impulse)
    {
        angVel += impulse * sensitivity;
    }

    // =====================================
    // LIFETIME
    // =====================================

    private void OnEnable()
    {
        if (!registry.Contains(this))
            registry.Add(this);
    }

    private void OnDisable()
    {
        registry.Remove(this);
    }

    private void Awake()
    {
        baseLocalPos = transform.localPosition;
        baseLocalRot = transform.localRotation;
        baseLocalScale = transform.localScale;

        seed = Random.value * 100f;
        freq = swingFrequency * (1f + Random.Range(-frequencyVariance, frequencyVariance));
        gustTimer = Random.Range(gustInterval.x, Mathf.Max(gustInterval.x, gustInterval.y));

        // เริ่มต้นแต่ละชิ้นไม่พร้อมกัน
        angVel = Random.Range(-1f, 1f) * Mathf.Max(gustStrength, 1f);

        cam = Camera.main;

        if (flickerTarget != null)
            flickerBase = flickerTarget.color;

        touchAudioSource = GetComponent<AudioSource>();

        if (touchAudioSource == null)
            touchAudioSource = gameObject.AddComponent<AudioSource>();

        touchAudioSource.playOnAwake = false;
        touchAudioSource.spatialBlend = 0f;
    }

    // =====================================
    // UPDATE
    // =====================================

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        if (dt <= 0f)
            return;

        // ลมกระโชก
        if (gustStrength > 0f)
        {
            gustTimer -= dt;

            if (gustTimer <= 0f)
            {
                angVel += (Random.value * 2f - 1f) * gustStrength;
                gustTimer = Random.Range(gustInterval.x, Mathf.Max(gustInterval.x, gustInterval.y));
            }
        }

        if (reactToMouse)
            ReactToMouse();

        // ลมเบาๆ = ขยับจุดสมดุลไปมาช้าๆ
        float wind = (Mathf.PerlinNoise(Time.time * windSpeed, seed) - 0.5f) * 2f * windAngle;

        // สปริง + หน่วง (แบ่ง 2 ก้าวให้นิ่ง)
        float w = 2f * Mathf.PI * Mathf.Max(freq, 0.05f);
        float k = w * w;
        float c = 2f * damping * w;

        const int steps = 2;
        float h = dt / steps;

        for (int i = 0; i < steps; i++)
        {
            float acc = -k * (angle - wind) - c * angVel;

            angVel += acc * h;
            angle += angVel * h;
        }

        if (angle > maxAngle)
        {
            angle = maxAngle;
            angVel = Mathf.Min(angVel, 0f);
        }
        else if (angle < -maxAngle)
        {
            angle = -maxAngle;
            angVel = Mathf.Max(angVel, 0f);
        }

        ApplyTransform();
        ApplyFlicker();
    }

    private void ReactToMouse()
    {
        if (Time.timeScale <= 0f)
            return;

        if (cam == null)
        {
            cam = Camera.main;

            if (cam == null)
                return;
        }

        // อ่านตำแหน่งเมาส์
        Vector3 mousePosition = Input.mousePosition;

        // ป้องกันค่า Infinity และ NaN
        if (float.IsNaN(mousePosition.x) ||
            float.IsNaN(mousePosition.y) ||
            float.IsInfinity(mousePosition.x) ||
            float.IsInfinity(mousePosition.y))
        {
            mouseWasTouching = false;
            hasLastMouse = false;
            return;
        }

        // ตรวจสอบว่าเมาส์อยู่ในพื้นที่กล้องหรือไม่
        if (!cam.pixelRect.Contains(
            new Vector2(mousePosition.x, mousePosition.y)))
        {
            mouseWasTouching = false;
            hasLastMouse = false;
            return;
        }

        // ระยะจากกล้องถึงระนาบของวัตถุ
        float cameraDistance = Vector3.Dot(
            transform.position - cam.transform.position,
            cam.transform.forward
        );

        if (cameraDistance <= 0f)
            return;

        // แปลง Screen Position เป็น World Position
        mousePosition.z = cameraDistance;

        Vector3 m = cam.ScreenToWorldPoint(mousePosition);

        if (float.IsNaN(m.x) || float.IsNaN(m.y) ||
            float.IsInfinity(m.x) || float.IsInfinity(m.y))
        {
            mouseWasTouching = false;
            hasLastMouse = false;
            return;
        }

        Vector2 mouse = new Vector2(m.x, m.y);

        float distance = Vector2.Distance(mouse, transform.position);
        bool isTouching = distance < mouseRadius;

        // เล่นเสียงเมื่อเมาส์สัมผัสครั้งแรก
        if (isTouching && !mouseWasTouching)
        {
            PlayTouchSound();
        }

        // ปัดเมาส์แล้วทำให้ของแกว่ง
        if (hasLastMouse && isTouching)
        {
            float dx = Mathf.Clamp(
                mouse.x - lastMouse.x,
                -0.5f,
                0.5f
            );

            angVel += dx * mouseForce * sensitivity;
        }

        mouseWasTouching = isTouching;
        lastMouse = mouse;
        hasLastMouse = true;
    }

    // หมุนรอบจุดแขวน โดยทำใน local space ของ parent (ซ้อนกันได้)
    private void ApplyTransform()
    {
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);

        Vector3 pivot = baseLocalPos +
            baseLocalRot * Vector3.Scale(new Vector3(pivotOffset.x, pivotOffset.y, 0f), baseLocalScale);

        transform.localPosition = pivot + rot * (baseLocalPos - pivot) + (Vector3)externalOffset;
        transform.localRotation = rot * baseLocalRot;
    }

    private void ApplyFlicker()
    {
        if (flickerTarget == null)
            return;

        float n = Mathf.PerlinNoise(Time.time * flickerSpeed, seed + 10f);
        float brightness = 1f - flickerAmount * n;

        Color c = flickerBase;
        c.r *= brightness;
        c.g *= brightness;
        c.b *= brightness;

        flickerTarget.color = c;
    }

    // =====================================
    // EDITOR HELPERS
    // =====================================

    // ถูกเรียกอัตโนมัติตอน Add Component ใน Editor
    private void Reset()
    {
        AutoPivotTop();
    }

    [ContextMenu("Set Pivot To Top Of Sprite")]
    public void AutoPivotTop()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();

        if (renderers.Length == 0)
            return;

        Bounds b = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        Vector3 local = transform.InverseTransformPoint(
            new Vector3(b.center.x, b.max.y, transform.position.z)
        );

        pivotOffset = new Vector2(local.x, local.y);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 world = transform.TransformPoint(new Vector3(pivotOffset.x, pivotOffset.y, 0f));

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 1f);
        Gizmos.DrawWireSphere(world, 0.08f);
        Gizmos.DrawLine(world, transform.position);
    }
    private void PlayTouchSound()
    {
        if (touchSoundIDs == null || touchSoundIDs.Count == 0)
            return;

        string soundID = touchSoundIDs[
            Random.Range(0, touchSoundIDs.Count)
        ];

        if (!string.IsNullOrEmpty(soundID))
        {
            AudioManager.Instance.PlaySFX(soundID);
        }
    }
}
