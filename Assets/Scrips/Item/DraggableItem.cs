using System.Collections;
using UnityEngine;

public class DraggableItem : MonoBehaviour
{
    [Header("References")]
    public WorkArea workArea;

    [Header("Cursor")]
    public Texture2D openHandCursor;
    public Texture2D grabHandCursor;

    [Header("Drag Settings")]
    public int draggingSortingOrder = 20;
    [Tooltip("ยิ่งสูงยิ่งตามเมาส์ติด (ต่ำ = นุ่ม หน่วงๆ)")]
    public float followSmoothness = 25f;

    [Header("Juice - Scale")]
    public float hoverScale = 1.05f;
    public float pickupScale = 1.15f;
    public float scaleLerpSpeed = 14f;

    [Header("Juice - Tilt (เอียงตามความเร็ว)")]
    public float maxTilt = 15f;
    public float tiltAmount = 2.5f;
    public float tiltSmooth = 12f;

    [Header("Juice - Shadow")]
    public Vector2 shadowOffsetLifted = new Vector2(0.2f, -0.25f);
    [Range(0f, 1f)] public float shadowAlpha = 0.3f;

    [Header("Return")]
    public float returnSpeed = 4f;

    [Header("Landing Animation")]
    public float squashAmount = 0.15f;
    public float squashDuration = 0.45f;
    public float squashFrequency = 3f;   // จำนวนรอบการเด้ง
    public float squashDecay = 7f;       // ยิ่งสูงยิ่งหยุดไว

    [Header("Landing FX (optional)")]
    public ParticleSystem landParticlesPrefab;
    public float shakeStrength = 0.04f;
    public float shakeDuration = 0.12f;

    [Header("Audio (optional)")]
    public AudioClip pickupSfx;
    public AudioClip dropSfx;
    public AudioClip landSfx;

    // ---------- private ----------
    private Camera mainCamera;
    private SpriteRenderer spriteRenderer;
    private Collider2D itemCollider;
    private Rigidbody2D rb;
    private AudioSource audioSource;

    private Vector3 originalPosition;
    private Vector3 offset;
    private Vector2 dragTarget;

    private int originalSortingOrder;
    private float originalGravityScale;

    private bool isDragging = false;
    private bool isReturning = false;
    private bool isHovering = false;
    private Vector2 returnTarget;

    // scale
    private Vector3 originalScale;
    private float scaleMul = 1f;

    // squash
    private bool isSquashing = false;
    private float squashTimer = 0f;
    private float currentSquashAmount;
    private bool hasLanded = false;
    public bool HasLanded => hasLanded;
    private float squashValue = 0f;

    // tilt
    private float currentAngle;
    private float velocityX;

    // shadow
    private Transform shadow;
    private SpriteRenderer shadowRenderer;
    private float lift = 0f; // 0 = วางพื้น, 1 = ลอย

    private bool isShaking = false;

    void Awake()
    {
        mainCamera = Camera.main;

        spriteRenderer = GetComponent<SpriteRenderer>();
        itemCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();

        if (rb != null)
            originalGravityScale = rb.gravityScale;

        originalScale = transform.localScale;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        CreateShadow();
    }

    void OnDestroy()
    {
        if (shadow != null) Destroy(shadow.gameObject);
    }

    // =================================
    // SHADOW
    // =================================

    void CreateShadow()
    {
        GameObject go = new GameObject(name + "_Shadow");
        shadow = go.transform;

        shadowRenderer = go.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = spriteRenderer.sprite;
        shadowRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        shadowRenderer.color = new Color(0f, 0f, 0f, 0f);
    }

    // =================================
    // LANDING
    // =================================

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.name != "TableSurface")
            return;

        if (isDragging || hasLanded)
            return;

        hasLanded = true;
        if (workArea != null &&
    workArea.AreaBounds.Contains(transform.position))
        {
            workArea.Pulse();
        }

        // ยิ่งตกแรง ยิ่งยุบเยอะ
        float strength = Mathf.Clamp01(collision.relativeVelocity.magnitude / 8f);
        currentSquashAmount = squashAmount * Mathf.Lerp(0.4f, 1f, strength);

        squashTimer = 0f;
        isSquashing = true;

        PlaySfx(landSfx);

        // ฝุ่น / พาร์ติเคิล
        if (landParticlesPrefab != null && collision.contactCount > 0)
        {
            ParticleSystem ps = Instantiate(
                landParticlesPrefab,
                collision.GetContact(0).point,
                Quaternion.identity
            );
            Destroy(ps.gameObject, 2f);
        }

        // กล้องสั่นเบาๆ
        if (shakeStrength > 0f && !isShaking && mainCamera != null)
            StartCoroutine(CameraShake(shakeStrength * Mathf.Lerp(0.5f, 1f, strength)));
    }

    IEnumerator CameraShake(float strength)
    {
        isShaking = true;
        Transform cam = mainCamera.transform;
        Vector3 origin = cam.localPosition;
        float t = 0f;

        while (t < shakeDuration)
        {
            t += Time.deltaTime;
            float falloff = 1f - (t / shakeDuration);
            Vector2 rand = Random.insideUnitCircle * strength * falloff;
            cam.localPosition = origin + new Vector3(rand.x, rand.y, 0f);
            yield return null;
        }

        cam.localPosition = origin;
        isShaking = false;
    }

    // =================================
    // PHYSICS LOOP
    // =================================

    void FixedUpdate()
    {
        if (rb == null) return;

        // ---------- ลาก (ตามเมาส์แบบนุ่มๆ) ----------
        if (isDragging)
        {
            float k = 1f - Mathf.Exp(-followSmoothness * Time.fixedDeltaTime);
            Vector2 next = Vector2.Lerp(rb.position, dragTarget, k);

            velocityX = (next.x - rb.position.x) / Time.fixedDeltaTime;
            rb.MovePosition(next);

            UpdateTilt();
            return;
        }

        // ---------- เคลื่อนกลับ (ไกลเร็ว ใกล้ช้า) ----------
        if (isReturning)
        {
            Vector2 current = rb.position;
            float dist = Vector2.Distance(current, returnTarget);
            float speed = returnSpeed * (1f + dist);

            Vector2 next = Vector2.MoveTowards(
                current,
                returnTarget,
                speed * Time.fixedDeltaTime
            );

            velocityX = (next.x - current.x) / Time.fixedDeltaTime;
            rb.MovePosition(next);

            UpdateTilt();

            if (Vector2.Distance(next, returnTarget) < 0.05f)
            {
                isReturning = false;

                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = originalGravityScale;
            }
        }
    }

    void UpdateTilt()
    {
        float targetAngle = Mathf.Clamp(-velocityX * tiltAmount, -maxTilt, maxTilt);
        float k = 1f - Mathf.Exp(-tiltSmooth * Time.fixedDeltaTime);
        currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, k);
        rb.MoveRotation(currentAngle);
    }

    // =================================
    // VISUAL LOOP
    // =================================

    void Update()
    {
        // ---------- Squash แบบสปริง (ยุบ -> เด้งเกิน -> นิ่ง) ----------
        if (isSquashing)
        {
            squashTimer += Time.deltaTime;
            float t = squashTimer;

            squashValue = currentSquashAmount
                          * Mathf.Exp(-squashDecay * t)
                          * Mathf.Cos(2f * Mathf.PI * squashFrequency * t);

            if (t >= squashDuration)
            {
                isSquashing = false;
                squashValue = 0f;
            }
        }

        // ---------- Scale ตามสถานะ ----------
        float targetMul = 1f;
        if (isDragging || isReturning) targetMul = pickupScale;
        else if (isHovering) targetMul = hoverScale;

        scaleMul = Mathf.Lerp(scaleMul, targetMul, 1f - Mathf.Exp(-scaleLerpSpeed * Time.deltaTime));

        // ---------- Lift (ใช้คุมเงา) ----------
        float liftTarget = (isDragging || isReturning) ? 1f : 0f;
        lift = Mathf.MoveTowards(lift, liftTarget, Time.deltaTime * 8f);
    }

    void LateUpdate()
    {
        // รวม scale ทั้งหมด
        transform.localScale = new Vector3(
            originalScale.x * scaleMul * (1f + squashValue),
            originalScale.y * scaleMul * (1f - squashValue),
            originalScale.z
        );

        // อัปเดตเงา
        if (shadow != null)
        {
            shadow.position = transform.position + (Vector3)(shadowOffsetLifted * lift);
            shadow.rotation = transform.rotation;
            shadow.localScale = transform.localScale;

            shadowRenderer.sprite = spriteRenderer.sprite;
            shadowRenderer.flipX = spriteRenderer.flipX;
            shadowRenderer.flipY = spriteRenderer.flipY;
            shadowRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
            shadowRenderer.color = new Color(0f, 0f, 0f, shadowAlpha * lift);
        }
    }

    // =================================
    // HOVER
    // =================================

    void OnMouseEnter()
    {
        isHovering = true;

        if (!isDragging)
            SetCursor(openHandCursor);
    }

    void OnMouseExit()
    {
        isHovering = false;

        if (!isDragging)
            SetCursor(null);
    }

    // =================================
    // PICK UP ITEM
    // =================================


    void OnMouseDown()
    {
        if (workArea == null || rb == null) return;

        hasLanded = false;

        isDragging = true;
        isReturning = false;
        isSquashing = false;
        squashValue = 0f;

        originalPosition = transform.position;
        originalSortingOrder = spriteRenderer.sortingOrder;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        Vector3 mouseWorld = GetMouseWorldPosition();
        offset = transform.position - mouseWorld;
        dragTarget = rb.position;
        currentAngle = rb.rotation;

        spriteRenderer.sortingOrder = draggingSortingOrder;

        scaleMul = 0.92f;

        workArea.SetHighlight(true);

        PlaySfx(pickupSfx);
        SetCursor(grabHandCursor);
    }


    // =================================
    // DRAG ITEM
    // =================================

    void OnMouseDrag()
    {
        if (!isDragging) return;

        Vector3 mouseWorld = GetMouseWorldPosition();
        Vector3 targetPosition = mouseWorld + offset;

        // แค่ตั้งเป้าหมาย แล้วให้ FixedUpdate ค่อยๆ ตามไปแบบนุ่มๆ
        dragTarget = new Vector2(targetPosition.x, targetPosition.y);
    }

    // =================================
    // DROP ITEM
    // =================================


    void OnMouseUp()
    {
        if (!isDragging) return;

        isDragging = false;
        isReturning = false;

        spriteRenderer.sortingOrder = originalSortingOrder;

        // หยุดการเคลื่อนที่เดิม
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        // เปิดฟิสิกส์ ณ จุดที่ผู้เล่นปล่อยของ
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = originalGravityScale;

        // ปิด Highlight กระดาน
        if (workArea != null)
            workArea.SetHighlight(false);

        PlaySfx(dropSfx);
        SetCursor(isHovering ? openHandCursor : null);
    }


    // =================================
    // HELPERS
    // =================================

    void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;

        audioSource.pitch = Random.Range(0.95f, 1.05f);
        audioSource.PlayOneShot(clip);
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;

        mousePosition.z = Mathf.Abs(
            mainCamera.transform.position.z - transform.position.z
        );

        return mainCamera.ScreenToWorldPoint(mousePosition);
    }

    void SetCursor(Texture2D cursorTexture)
    {
        if (cursorTexture == null)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }

        Vector2 hotspot = new Vector2(
            cursorTexture.width / 2f,
            cursorTexture.height / 2f
        );

        Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);
    }
}