using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// กระดิ่งเรียกเจ้าของสินค้า — ผู้เล่น "ดึงพู่" ลงมา
///   พู่ถูกยึดไว้ที่ "จุดยึด" ด้านบน (ไม่ขยับ) ตอนดึงพู่จะยืดลงและเอียงตามเมาส์รอบจุดยึด
///   ดึงถึงระยะที่กำหนด -> กระดิ่งดัง + กระดิ่งแกว่ง
///   ปล่อยมือ -> พู่หดกลับแบบสปริง แล้วแกว่งต่อรอบจุดยึด
///   คลิกเฉยๆ -> พู่ดึงตัวเองลงมาให้ แล้วดังเหมือนกัน
/// วิธีตั้งค่าอยู่ท้ายไฟล์ (คอมเมนต์)
/// </summary>
public class ReturnBell : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ReturnArea returnArea;
    [SerializeField] private CustomerSpawnManager customerManager;

    [Header("Cord / Tassel (พู่ที่ต้องดึง)")]
    [Tooltip("พู่ที่ห้อยอยู่ — ถ้าไม่มี Collider2D ระบบจะเพิ่มให้เอง")]
    [SerializeField] private Transform tassel;
    [Tooltip("จุดยึดด้านบนของพู่ (จุดที่ผูกกับกระดิ่ง) — สร้าง Empty ไปวางตรงนั้นแล้วลากใส่ ถ้าว่างจะใช้ขอบบนกึ่งกลางของภาพพู่")]
    [SerializeField] private Transform anchorPoint;
    [Tooltip("ความยาวพู่ตอนห้อยปกติ (หน่วย world) — 0 = วัดจากภาพให้เอง")]
    [SerializeField] private float tasselLength = 0f;
    [Tooltip("ยืดพู่ลงแล้วให้แคบลงนิดหน่อย (รักษาปริมาตร) ดูนุ่มขึ้น")]
    [SerializeField] private bool squashWhenStretched = true;
    [Tooltip("(ไม่บังคับ) เชือกที่จะยืดตามตอนดึง — pivot ควรอยู่ด้านบน")]
    [SerializeField] private Transform cordToStretch;
    [SerializeField] private float cordLength = 1f;
    [Tooltip("ระยะดึงลงสูงสุด (หน่วย world)")]
    [SerializeField] private float maxPull = 0.6f;
    [Tooltip("ดึงลงถึงสัดส่วนนี้ของระยะสูงสุด = กระดิ่งดัง")]
    [Range(0.2f, 0.95f)]
    [SerializeField] private float triggerRatio = 0.7f;
    [Tooltip("เพิ่มขนาดพื้นที่คลิกรอบพู่ให้จับง่ายขึ้น")]
    [SerializeField] private float grabPadding = 0.25f;

    [Header("Sway รอบจุดยึด")]
    [Tooltip("พู่เอียงตามเมาส์ได้สูงสุดกี่องศา")]
    [SerializeField] private float maxTiltAngle = 30f;
    [SerializeField] private float swayHoldStiffness = 220f;
    [SerializeField] private float swayHoldDamping = 22f;
    [Tooltip("ตอนปล่อย — ค่าหน่วงน้อยจะแกว่งหลายที")]
    [SerializeField] private float swayReleaseStiffness = 70f;
    [SerializeField] private float swayReleaseDamping = 3.2f;
    [Tooltip("แกว่งเบาๆ ตอนไม่มีใครแตะ (องศา)")]
    [SerializeField] private float idleSwayDegrees = 2f;

    [Header("Spring (ความรู้สึกของสปริง)")]
    [Tooltip("ตอนจับค้างอยู่ — ยิ่งมากยิ่งตามเมาส์ติด")]
    [SerializeField] private float holdStiffness = 700f;
    [SerializeField] private float holdDamping = 48f;
    [Tooltip("ตอนปล่อยมือ — ค่าน้อย/หน่วงน้อย = เด้งกลับแล้วเด้งต่อหลายที")]
    [SerializeField] private float releaseStiffness = 110f;
    [SerializeField] private float releaseDamping = 6f;

    [Header("Click (คลิกเฉยๆ)")]
    [SerializeField] private bool clickToPull = true;
    [Tooltip("ลากน้อยกว่านี้ = นับเป็นคลิก")]
    [SerializeField] private float clickThreshold = 0.08f;
    [SerializeField] private float autoPullSpeed = 6f;
    [SerializeField] private float cooldown = 0.5f;

    [Header("Bell Body")]
    [Tooltip("ตัวกระดิ่ง ถ้าว่างจะใช้ Transform ของตัวเอง")]
    [SerializeField] private Transform visual;
    [Tooltip("กระดิ่งเอียงตามแรงดึงกี่องศา")]
    [SerializeField] private float pullTiltDegrees = 5f;
    [SerializeField] private float swingAngle = 18f;
    [SerializeField] private float swingDuration = 0.9f;
    [SerializeField] private float punchScale = 0.12f;

    [Header("Feedback Text (optional)")]
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private float feedbackDuration = 1.6f;

    [Header("Audio (optional)")]
    [Tooltip("เสียงตอนจับพู่/เริ่มดึง")]
    [SerializeField] private AudioClip tugSfx;
    [Tooltip("เสียงกระดิ่งตอนดึงถึงระยะ")]
    [SerializeField] private AudioClip ringSfx;
    [SerializeField] private AudioClip rejectSfx;
    [Tooltip("เสียงตอนปล่อยพู่เด้งกลับ")]
    [SerializeField] private AudioClip releaseSfx;

    // ---------------------------------------------------------------

    private AudioSource audioSource;

    private Quaternion baseRotation;
    private Vector3 baseScale;
    private Vector3 cordBaseScale;

    // พู่ (ค่าตอนห้อยปกติ ในพิกัด local ของ parent)
    private Vector3 tasselBasePos;
    private Quaternion tasselBaseRot;
    private Vector3 tasselBaseScale;
    private Vector3 anchorLocal;      // จุดยึดในพิกัด local ของตัวพู่เอง
    private float restLength = 1f;
    private float seed;

    private float angle;
    private float angleVel;
    private float angleTarget;
    private float startMouseX;

    private bool held;
    private bool autoPulling;
    private bool ringedThisPull;
    private float autoHoldTimer;
    private float startMouseY;
    private float maxRaw;

    private float pullTarget;
    private float pullOffset;
    private float pullVel;

    private float ringTimer = -1f;
    private float ringStrength;
    private float nextRingTime;

    private Camera cam;
    private Coroutine feedbackRoutine;

    private float TriggerPull => maxPull * triggerRatio;

    // =====================================
    // SETUP
    // =====================================

    private void Awake()
    {
        if (visual == null)
            visual = transform;

        baseRotation = visual.localRotation;
        baseScale = visual.localScale;

        cam = Camera.main;

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        if (feedbackText != null)
            feedbackText.alpha = 0f;

        if (cordToStretch != null)
            cordBaseScale = cordToStretch.localScale;

        SetupTassel();
    }

    private void SetupTassel()
    {
        if (tassel == null)
        {
            Debug.LogWarning("ReturnBell: ยังไม่ได้ลาก Tassel (พู่) ใส่ช่อง — กดดึงไม่ได้", this);
            return;
        }

        seed = Random.value * 100f;

        // ReturnBell คุม transform ของพู่เองทั้งหมด (ยึดจุดบน) — ปิด HangingProp บนพู่กันตีกัน
        HangingProp conflicting = tassel.GetComponent<HangingProp>();

        if (conflicting != null)
        {
            conflicting.enabled = false;
            Debug.Log("ReturnBell: disabled HangingProp on the tassel (ReturnBell drives the sway itself)", this);
        }

        tasselBasePos = tassel.localPosition;
        tasselBaseRot = tassel.localRotation;
        tasselBaseScale = tassel.localScale;

        // หาจุดยึด + ความยาวพู่
        SpriteRenderer[] renderers = tassel.GetComponentsInChildren<SpriteRenderer>();

        Bounds bounds = new Bounds(tassel.position, Vector3.zero);

        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 anchorWorld = anchorPoint != null
            ? anchorPoint.position
            : new Vector3(bounds.center.x, bounds.max.y, tassel.position.z);

        anchorLocal = tassel.InverseTransformPoint(anchorWorld);

        restLength = tasselLength > 0.01f
            ? tasselLength
            : Mathf.Max(anchorWorld.y - bounds.min.y, 0.1f);

        Collider2D col = tassel.GetComponent<Collider2D>();

        if (col == null)
        {
            BoxCollider2D box = tassel.gameObject.AddComponent<BoxCollider2D>();

            SpriteRenderer sr = tassel.GetComponentInChildren<SpriteRenderer>();

            if (sr != null)
            {
                Vector3 lossy = tassel.lossyScale;
                Bounds b = sr.bounds;

                box.offset = tassel.InverseTransformPoint(b.center);
                box.size = new Vector2(
                    b.size.x / Mathf.Max(Mathf.Abs(lossy.x), 0.0001f) + grabPadding,
                    b.size.y / Mathf.Max(Mathf.Abs(lossy.y), 0.0001f) + grabPadding
                );
            }
        }

        BellCordHandle handle = tassel.GetComponent<BellCordHandle>();

        if (handle == null)
            handle = tassel.gameObject.AddComponent<BellCordHandle>();

        handle.owner = this;
    }

    // =====================================
    // INPUT (เรียกจาก BellCordHandle)
    // =====================================

    public void OnCordHover()
    {
        if (!held && !autoPulling)
            angleVel += Random.Range(-1f, 1f) * 40f;
    }

    public void BeginPull()
    {
        if (held || Time.timeScale <= 0f || tassel == null)
            return;

        held = true;
        autoPulling = false;
        ringedThisPull = false;
        maxRaw = 0f;

        Vector2 m = MouseWorld();
        startMouseY = m.y;
        startMouseX = m.x;

        PlaySfx(tugSfx);
    }

    public void EndPull()
    {
        if (!held)
            return;

        held = false;

        if (clickToPull && maxRaw < clickThreshold)
        {
            StartAutoPull();
            return;
        }

        if (pullOffset > maxPull * 0.2f)
            PlaySfx(releaseSfx);

        // ปล่อยแล้วพู่แกว่งต่อรอบจุดยึด
        angleVel += Random.Range(-1f, 1f) * 60f;
    }

    /// <summary>ผูกกับ UI Button ได้ — ให้พู่ดึงตัวเองลงมาแล้วดัง</summary>
    public void RingBell()
    {
        StartAutoPull();
    }

    private void StartAutoPull()
    {
        if (autoPulling || held || Time.timeScale <= 0f)
            return;

        autoPulling = true;
        ringedThisPull = false;
        autoHoldTimer = 0f;

        PlaySfx(tugSfx);
    }

    private Vector2 MouseWorld()
    {
        if (cam == null)
            cam = Camera.main;

        if (cam == null || tassel == null)
            return Vector2.zero;

        Vector3 p = Input.mousePosition;
        p.z = Mathf.Abs(cam.transform.position.z - tassel.position.z);

        Vector3 w = cam.ScreenToWorldPoint(p);

        return new Vector2(w.x, w.y);
    }

    // =====================================
    // UPDATE
    // =====================================

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        if (dt <= 0f)
            return;

        UpdateInput(dt);
        UpdateSpring(dt);
        ApplyVisuals();
    }

    private void UpdateInput(float dt)
    {
        if (held)
        {
            if (!Input.GetMouseButton(0))
            {
                EndPull();
            }
            else
            {
                Vector2 m = MouseWorld();

                float raw = Mathf.Max(0f, startMouseY - m.y);
                maxRaw = Mathf.Max(maxRaw, raw);

                // ยิ่งดึงยิ่งฝืด (ยาง)
                float x = raw / Mathf.Max(maxPull, 0.01f);
                pullTarget = maxPull * (1f - Mathf.Exp(-1.8f * x));

                // พู่เอียงรอบจุดยึดตามทิศที่ลากไปซ้าย/ขวา (ยิ่งดึงลงยิ่งเอียงตามชัด)
                float dx = m.x - startMouseX;

                angleTarget = Mathf.Clamp(
                    Mathf.Atan2(dx, restLength + pullTarget) * Mathf.Rad2Deg,
                    -maxTiltAngle, maxTiltAngle
                ) * Mathf.Clamp01(raw / 0.15f);


                if (!ringedThisPull && pullOffset >= TriggerPull)
                {
                    ringedThisPull = true;
                    Trigger();
                }
            }
        }
        else if (autoPulling)
        {
            float autoTarget = maxPull * 0.85f;

            pullTarget = Mathf.MoveTowards(pullTarget, autoTarget, autoPullSpeed * dt);

            if (!ringedThisPull && pullOffset >= TriggerPull)
            {
                ringedThisPull = true;
                Trigger();
            }

            // ค้างแป๊บหนึ่งหลังดัง แล้วปล่อย
            if (ringedThisPull)
                autoHoldTimer += dt;

            angleTarget = 0f;

            if (autoHoldTimer > 0.07f ||
                (Mathf.Approximately(pullTarget, autoTarget) && pullOffset >= autoTarget * 0.97f))
            {
                autoPulling = false;
                PlaySfx(releaseSfx);

                angleVel += Random.Range(-1f, 1f) * 60f;
            }
        }
        else
        {
            pullTarget = 0f;

            // ไม่มีใครแตะ -> แกว่งเบาๆ ตามลม
            angleTarget = (Mathf.PerlinNoise(Time.time * 0.4f, seed) - 0.5f) * 2f * idleSwayDegrees;
        }
    }

    private void UpdateSpring(float dt)
    {
        bool holding = held || autoPulling;

        float k = holding ? holdStiffness : releaseStiffness;
        float c = holding ? holdDamping : releaseDamping;

        const int steps = 3;
        float h = dt / steps;

        for (int i = 0; i < steps; i++)
        {
            float acc = -k * (pullOffset - pullTarget) - c * pullVel;

            pullVel += acc * h;
            pullOffset += pullVel * h;
        }

        // เด้งขึ้นเลยจุดเดิมได้นิดหน่อย แต่ไม่เกินไป
        pullOffset = Mathf.Clamp(pullOffset, -maxPull * 0.35f, maxPull * 1.3f);

        // สปริงของมุมเอียงรอบจุดยึด
        float kA = holding ? swayHoldStiffness : swayReleaseStiffness;
        float cA = holding ? swayHoldDamping : swayReleaseDamping;

        for (int i = 0; i < steps; i++)
        {
            float accA = -kA * (angle - angleTarget) - cA * angleVel;

            angleVel += accA * h;
            angle += angleVel * h;
        }

        angle = Mathf.Clamp(angle, -maxTiltAngle * 1.5f, maxTiltAngle * 1.5f);
    }

    // =====================================
    // APPLY
    // =====================================

    private void ApplyVisuals()
    {
        ApplyTassel();

        if (cordToStretch != null)
        {
            Vector3 s = cordBaseScale;
            s.y = cordBaseScale.y * (1f + Mathf.Max(pullOffset, 0f) / Mathf.Max(cordLength, 0.01f));
            cordToStretch.localScale = s;
        }

        // ตัวกระดิ่ง: เอียงตามแรงดึง + แกว่งหลังดัง
        float bellAngle = pullOffset / Mathf.Max(maxPull, 0.01f) * pullTiltDegrees;
        float punch = 1f;

        if (ringTimer >= 0f)
        {
            ringTimer += Time.deltaTime;

            float duration = Mathf.Max(swingDuration, 0.05f);
            float n = ringTimer / duration;

            if (n >= 1f)
            {
                ringTimer = -1f;
            }
            else
            {
                bellAngle += swingAngle * ringStrength *
                         Mathf.Exp(-5f * n) * Mathf.Sin(n * Mathf.PI * 6f);

                punch = 1f + punchScale * ringStrength * Mathf.Exp(-10f * n);
            }
        }

        visual.localRotation = baseRotation * Quaternion.Euler(0f, 0f, bellAngle);
        visual.localScale = baseScale * punch;
    }

    // พู่ถูกยึดที่จุดบน: ยืดลง + หมุนรอบจุดยึด (จุดยึดไม่ขยับเลย)
    private void ApplyTassel()
    {
        if (tassel == null)
            return;

        float stretch = Mathf.Max(1f + pullOffset / Mathf.Max(restLength, 0.01f), 0.6f);

        Vector3 scale = tasselBaseScale;
        scale.y *= stretch;

        if (squashWhenStretched)
            scale.x *= 1f / Mathf.Sqrt(stretch);

        Quaternion rot = Quaternion.Euler(0f, 0f, angle) * tasselBaseRot;

        // ตำแหน่งจุดยึดในพิกัด local ของ parent (คงที่)
        Vector3 anchorInParent =
            tasselBasePos + tasselBaseRot * Vector3.Scale(anchorLocal, tasselBaseScale);

        tassel.localRotation = rot;
        tassel.localScale = scale;
        tassel.localPosition = anchorInParent - rot * Vector3.Scale(anchorLocal, scale);
    }

    // =====================================
    // RING
    // =====================================

    private void Trigger()
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

            StartSwing(1f);
            PlaySfx(ringSfx);
        }
        else
        {
            StartSwing(0.3f);
            PlaySfx(rejectSfx);
        }

        ShowMessage(MessageFor(result));
    }

    private void StartSwing(float strength)
    {
        ringTimer = 0f;
        ringStrength = strength;

        // กระดิ่งดัง -> พู่สะดุ้งแกว่ง
        angleVel += Random.Range(-1f, 1f) * 90f * strength;

        // กระดิ่งดัง -> ของแขวนรอบๆ แกว่งตาม
        HangingProp.Bump(transform.position, 40f * strength, 5f);
    }

    private static string MessageFor(ReturnRequestResult result)
    {
        switch (result)
        {
            case ReturnRequestResult.Started:
                return "The owner has been called back!";
            case ReturnRequestResult.NoItem:
                return "Place an item on the return spot first";
            case ReturnRequestResult.NotSolved:
                return "This item hasn't been cleansed yet!";
            case ReturnRequestResult.ReturnBusy:
                return "A customer is already on the way";
            case ReturnRequestResult.MissingPoints:
                return "Pickup points are not set up";
            default:
                return "Can't find the owner of this item";
        }
    }

    // =====================================
    // FEEDBACK
    // =====================================

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

    private void OnDrawGizmosSelected()
    {
        Vector3 p;

        if (anchorPoint != null)
            p = anchorPoint.position;
        else if (tassel != null)
            p = tassel.position;
        else
            return;

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 1f);
        Gizmos.DrawWireSphere(p, 0.07f);

        float len = tasselLength > 0.01f ? tasselLength : 1f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(p, p + Vector3.down * (len + maxPull));

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(p + Vector3.down * (len + TriggerPull), 0.05f);
    }
}

/// <summary>
/// ตัวรับเมาส์ที่ติดบนพู่ (ReturnBell เพิ่มให้เองอัตโนมัติ) ส่งต่อให้ ReturnBell
/// </summary>
public class BellCordHandle : MonoBehaviour
{
    [HideInInspector] public ReturnBell owner;

    private void OnMouseEnter()
    {
        if (owner != null)
            owner.OnCordHover();
    }

    private void OnMouseDown()
    {
        if (owner != null)
            owner.BeginPull();
    }

    private void OnMouseUp()
    {
        if (owner != null)
            owner.EndPull();
    }
}

/*
 * ===== วิธีตั้งค่า =====
 * 1) ReturnBell อยู่บน object ตัวกระดิ่งเหมือนเดิม (ลาก Return Area / Customer Manager ใส่เหมือนเดิม)
 * 2) ลาก object "พู่" ที่ห้อยอยู่ใส่ช่อง Tassel
 *      - ไม่ต้องใส่ Collider เอง ระบบเพิ่มให้ตามขนาดภาพ (+ Grab Padding)
 *      - ถ้า sprite ของพู่ pivot อยู่ด้านบน จะดูเป็นธรรมชาติที่สุด
 * 3) สร้าง Empty ไปวางที่ "จุดที่พู่ผูกกับกระดิ่ง" แล้วลากใส่ช่อง Anchor Point (ไม่ใส่ = ใช้ขอบบนของภาพ)
 *      จุดนี้จะไม่ขยับเลย พู่จะยืดและแกว่งรอบจุดนี้ — ไม่ต้องใส่ HangingProp บนพู่ (ถ้ามีจะถูกปิดให้)
 * 4) ใส่เสียง: Tug (จับพู่) / Ring (กระดิ่งดัง) / Reject / Release (พู่เด้งกลับ)
 * 5) ถ้ามีเชือกแยกเป็นอีก sprite ลากใส่ Cord To Stretch ให้ยืดตามตอนดึง
 */
