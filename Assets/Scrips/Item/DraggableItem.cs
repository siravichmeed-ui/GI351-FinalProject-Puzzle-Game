
using System.Collections;
using UnityEngine;

public class DraggableItem : MonoBehaviour
{
    [Header("References")]
    public WorkArea workArea;

    [Header("Item Information")]
    public string itemID;
    public string itemName;

    [Header("Item Appearance")]
    public Sprite cursedSprite;
    public Sprite cleanedSprite;

    [Header("Cursor")]
    public Texture2D openHandCursor;
    public Texture2D grabHandCursor;

    [Header("Drag Settings")]
    public int draggingSortingOrder = 20;
    public float followSmoothness = 25f;

    [Header("Juice - Scale")]
    public float hoverScale = 1.05f;
    public float pickupScale = 1.15f;
    public float scaleLerpSpeed = 14f;

    [Header("Juice - Tilt")]
    public float maxTilt = 15f;
    public float tiltAmount = 2.5f;
    public float tiltSmooth = 12f;

    [Header("Juice - Shadow")]
    public Vector2 shadowOffsetLifted =
        new Vector2(0.2f, -0.25f);

    [Range(0f, 1f)]
    public float shadowAlpha = 0.3f;

    [Header("Return")]
    public float returnSpeed = 4f;

    [Header("Landing Animation")]
    public float squashAmount = 0.15f;
    public float squashDuration = 0.45f;
    public float squashFrequency = 3f;
    public float squashDecay = 7f;

    [Header("Landing FX")]
    public ParticleSystem landParticlesPrefab;
    public float shakeStrength = 0.04f;
    public float shakeDuration = 0.12f;
    [Tooltip("แรงสะเทือนที่ส่งไปให้ของแขวนรอบๆ ตอนสินค้าตกโต๊ะ")]
    public float landBumpForce = 30f;
    public float landBumpRadius = 4f;

    [Header("Cursed Aura")]
    [Tooltip("ของที่ยังไม่ผ่าน Puzzle จะเรืองแสงวูบวาบ")]
    public Color cursedAuraColor = new Color(0.75f, 0.45f, 1f, 1f);
    [Range(0f, 1f)] public float cursedAuraAmount = 0.35f;
    public float cursedAuraSpeed = 2.2f;

    [Header("Audio")]
    public AudioClip pickupSfx;
    public AudioClip dropSfx;
    public AudioClip landSfx;

    // =================================
    // PRIVATE VARIABLES
    // =================================

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

    private bool isDragging;
    private bool isReturning;
    private bool isHovering;

    private Vector2 returnTarget;
    private bool returnToBoard;

    private Vector3 originalScale;
    private float scaleMul = 1f;

    private bool isSquashing;
    private float squashTimer;
    private float currentSquashAmount;
    private float squashValue;

    private bool hasLanded;
    public bool HasLanded => hasLanded;

    public bool IsDragging => isDragging;
    public bool IsLocked => isLocked;
    public float OriginalGravityScale => originalGravityScale;

    /// <summary>สินค้าที่ผู้เล่นกำลังลากอยู่ (ใช้ไฮไลต์จุดคืนของ)</summary>
    public static DraggableItem DraggedItem { get; private set; }

    private bool isLocked;
    private float externalScale = 1f;

    private ItemPersistenceData persistence;
    private bool auraTinted;
    private float auraPhase;

    private float currentAngle;
    private float velocityX;

    private Transform shadow;
    private SpriteRenderer shadowRenderer;
    private float lift;

    private bool isShaking;

    // =================================
    // AWAKE
    // =================================

    void Awake()
    {
        mainCamera = Camera.main;
        auraPhase = Random.value * Mathf.PI * 2f;

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

        if (spriteRenderer == null)
            Debug.LogError("DraggableItem: SpriteRenderer missing!");

        if (itemCollider == null)
            Debug.LogError("DraggableItem: Collider2D missing!");

        if (rb == null)
            Debug.LogError("DraggableItem: Rigidbody2D missing!");

        if (spriteRenderer != null)
            CreateShadow();
    }

    void OnDestroy()
    {
        if (DraggedItem == this)
            DraggedItem = null;

        if (isDragging || isHovering)
            SetCursor(null);

        if (workArea != null)
            workArea.ReleaseItem(gameObject);

        if (shadow != null)
            Destroy(shadow.gameObject);
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
        shadowRenderer.sortingLayerID =
            spriteRenderer.sortingLayerID;

        shadowRenderer.color =
            new Color(0f, 0f, 0f, 0f);
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

        if (workArea != null && itemCollider != null)
        {
            if (workArea.IsInsideArea(itemCollider.bounds))
            {
                bool placed =
                    workArea.TryPlaceItem(gameObject);

                if (placed)
                {
                    workArea.Pulse();

                    Debug.Log(
                        "Item registered: " + itemName
                    );
                }
                else
                {
                    // โต๊ะ Puzzle มีสินค้าอยู่แล้ว -> ดันชิ้นนี้ออกไปรอข้างโต๊ะ
                    NudgeOutOfWorkArea();
                }
            }
        }

        float strength = Mathf.Clamp01(
            collision.relativeVelocity.magnitude / 8f
        );

        currentSquashAmount =
            squashAmount * Mathf.Lerp(0.4f, 1f, strength);

        squashTimer = 0f;
        isSquashing = true;

        PlaySfx(landSfx);

        HangingProp.Bump(transform.position, landBumpForce * strength, landBumpRadius);

        if (landParticlesPrefab != null &&
            collision.contactCount > 0)
        {
            ParticleSystem ps = Instantiate(
                landParticlesPrefab,
                collision.GetContact(0).point,
                Quaternion.identity
            );

            Destroy(ps.gameObject, 2f);
        }

        if (shakeStrength > 0f &&
            !isShaking &&
            mainCamera != null)
        {
            StartCoroutine(
                CameraShake(
                    shakeStrength *
                    Mathf.Lerp(0.5f, 1f, strength)
                )
            );
        }
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

            Vector2 rand =
                Random.insideUnitCircle * strength * falloff;

            cam.localPosition =
                origin + new Vector3(rand.x, rand.y, 0f);

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
        if (rb == null)
            return;

        // DRAG
        if (isDragging)
        {
            float k = 1f - Mathf.Exp(
                -followSmoothness * Time.fixedDeltaTime
            );

            Vector2 next = Vector2.Lerp(
                rb.position,
                dragTarget,
                k
            );

            velocityX =
                (next.x - rb.position.x) /
                Time.fixedDeltaTime;

            rb.MovePosition(next);

            UpdateTilt();

            return;
        }

        // RETURN
        if (isReturning)
        {
            Vector2 current = rb.position;

            float dist = Vector2.Distance(
                current,
                returnTarget
            );

            float speed = returnSpeed * (1f + dist);

            Vector2 next = Vector2.MoveTowards(
                current,
                returnTarget,
                speed * Time.fixedDeltaTime
            );

            velocityX =
                (next.x - current.x) /
                Time.fixedDeltaTime;

            rb.MovePosition(next);

            UpdateTilt();

            if (Vector2.Distance(next, returnTarget) < 0.05f)
            {
                isReturning = false;

                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;

                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = originalGravityScale;

                if (returnToBoard && workArea != null)
                    workArea.TryPlaceItem(gameObject);

                returnToBoard = false;
            }
        }
    }

    void UpdateTilt()
    {
        float targetAngle = Mathf.Clamp(
            -velocityX * tiltAmount,
            -maxTilt,
            maxTilt
        );

        float k = 1f - Mathf.Exp(
            -tiltSmooth * Time.fixedDeltaTime
        );

        currentAngle = Mathf.LerpAngle(
            currentAngle,
            targetAngle,
            k
        );

        rb.MoveRotation(currentAngle);
    }

    // =================================
    // VISUAL LOOP
    // =================================

    void Update()
    {
        if (isSquashing)
        {
            squashTimer += Time.deltaTime;

            float t = squashTimer;

            squashValue =
                currentSquashAmount *
                Mathf.Exp(-squashDecay * t) *
                Mathf.Cos(
                    2f * Mathf.PI * squashFrequency * t
                );

            if (t >= squashDuration)
            {
                isSquashing = false;
                squashValue = 0f;
            }
        }

        float targetMul = 1f;

        if (isDragging || isReturning)
            targetMul = pickupScale;
        else if (isHovering)
            targetMul = hoverScale;

        scaleMul = Mathf.Lerp(
            scaleMul,
            targetMul,
            1f - Mathf.Exp(
                -scaleLerpSpeed * Time.deltaTime
            )
        );

        float liftTarget =
            (isDragging || isReturning) ? 1f : 0f;

        lift = Mathf.MoveTowards(
            lift,
            liftTarget,
            Time.deltaTime * 8f
        );
    }

    void LateUpdate()
    {
        if (spriteRenderer == null)
            return;

        UpdateAura();

        transform.localScale = new Vector3(
            originalScale.x * scaleMul * externalScale * (1f + squashValue),
            originalScale.y * scaleMul * externalScale * (1f - squashValue),
            originalScale.z
        );

        if (shadow != null && shadowRenderer != null)
        {
            shadow.position =
                transform.position +
                (Vector3)(shadowOffsetLifted * lift);

            shadow.rotation = transform.rotation;
            shadow.localScale = transform.localScale;

            shadowRenderer.sprite = spriteRenderer.sprite;
            shadowRenderer.flipX = spriteRenderer.flipX;
            shadowRenderer.flipY = spriteRenderer.flipY;

            shadowRenderer.sortingOrder =
                spriteRenderer.sortingOrder - 1;

            shadowRenderer.color = new Color(
                0f, 0f, 0f, shadowAlpha * lift
            );
        }
    }

    // =================================
    // HOVER
    // =================================

    void OnMouseEnter()
    {
        isHovering = true;

        if (!isDragging && !isLocked)
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
        // ล็อกอยู่ (เจ้าของกำลังมารับ) -> หยิบไม่ได้ แต่ตอบสนองนิดหน่อย
        if (Time.timeScale <= 0f)
            return;

        if (isLocked)
        {
            Wobble(1f);
            return;
        }

        if (workArea == null)
        {
            Debug.LogError(
                "DraggableItem: WorkArea is NULL! " +
                gameObject.name
            );
            return;
        }

        if (rb == null ||
            spriteRenderer == null ||
            itemCollider == null)
            return;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                Debug.LogError("Main Camera not found!");
                return;
            }
        }

        // ปลด Item จาก WorkArea ทันที
        workArea.ReleaseItem(gameObject);

        hasLanded = false;

        isDragging = true;
        isReturning = false;
        DraggedItem = this;

        isSquashing = false;
        squashValue = 0f;

        originalPosition = transform.position;

        originalSortingOrder =
            spriteRenderer.sortingOrder;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        Vector3 mouseWorld = GetMouseWorldPosition();

        offset = transform.position - mouseWorld;

        dragTarget = rb.position;

        currentAngle = rb.rotation;

        spriteRenderer.sortingOrder =
            draggingSortingOrder;

        scaleMul = 0.92f;

        workArea.SetHighlight(true);

        PlaySfx(pickupSfx);

        SetCursor(grabHandCursor);

        Debug.Log("Picking up: " + itemName);
    }

    // =================================
    // DRAG ITEM
    // =================================

    void OnMouseDrag()
    {
        if (!isDragging)
            return;

        Vector3 mouseWorld = GetMouseWorldPosition();

        Vector3 targetPosition = mouseWorld + offset;

        dragTarget = new Vector2(
            targetPosition.x,
            targetPosition.y
        );

        // ลากได้ไม่เกินขอบกล้อง
        if (CameraBounds2D.Instance != null && itemCollider != null)
        {
            dragTarget = CameraBounds2D.Instance.ClampToView(
                dragTarget,
                itemCollider.bounds.extents
            );
        }
    }

    // =================================
    // DROP ITEM
    // =================================

    void OnMouseUp()
    {
        if (!isDragging)
            return;

        isDragging = false;

        if (DraggedItem == this)
            DraggedItem = null;

        spriteRenderer.sortingOrder =
            originalSortingOrder;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (workArea != null)
        {
            workArea.SetHighlight(false);

            bool insideBoard =
                workArea.IsInsideArea(itemCollider.bounds);

            if (insideBoard)
            {
                bool placed =
                    workArea.TryPlaceItem(gameObject);

                if (!placed)
                {
                    ReturnToOriginalPosition();
                    return;
                }

                workArea.Pulse();

                Debug.Log(
                    "Item placed: " + itemName
                );
            }
            else
            {
                workArea.ReleaseItem(gameObject);
            }
        }

        isReturning = false;

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = originalGravityScale;

        PlaySfx(dropSfx);

        SetCursor(
            isHovering ? openHandCursor : null
        );
    }

    // =================================
    // HELPERS
    // =================================

    void PlaySfx(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.pitch =
            Random.Range(0.95f, 1.05f);

        audioSource.PlayOneShot(clip);
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Input.mousePosition;

        mousePosition.z = Mathf.Abs(
            mainCamera.transform.position.z -
            transform.position.z
        );

        return mainCamera.ScreenToWorldPoint(
            mousePosition
        );
    }

    void SetCursor(Texture2D cursorTexture)
    {
        if (cursorTexture == null)
        {
            Cursor.SetCursor(
                null,
                Vector2.zero,
                CursorMode.Auto
            );

            return;
        }

        Vector2 hotspot = new Vector2(
            cursorTexture.width / 2f,
            cursorTexture.height / 2f
        );

        Cursor.SetCursor(
            cursorTexture,
            hotspot,
            CursorMode.Auto
        );
    }

    // =================================
    // RETURN TO ORIGINAL POSITION
    // =================================

    void ReturnToOriginalPosition()
    {
        BeginReturnMotion(originalPosition);
    }

    void NudgeOutOfWorkArea()
    {
        if (rb == null || workArea == null || itemCollider == null)
            return;

        Vector3 outside =
            workArea.GetNearestOutsidePoint(itemCollider.bounds);

        Vector2 delta = (Vector2)(outside - itemCollider.bounds.center);

        BeginReturnMotion(rb.position + delta);
    }

    void BeginReturnMotion(Vector2 target)
    {
        isReturning = true;
        returnToBoard = false;

        returnTarget = target;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;

        PlaySfx(dropSfx);
    }

    // =================================
    // CURSED AURA / CELEBRATE
    // =================================

    void UpdateAura()
    {
        if (persistence == null)
        {
            persistence = GetComponent<ItemPersistenceData>();

            if (persistence == null)
                return;
        }

        if (string.IsNullOrEmpty(persistence.instanceID))
            return;

        // เพิ่งทำ Puzzle เสร็จ -> ฉลอง
        if (ItemSessionData.ConsumeJustSolved(persistence.instanceID))
            StartCoroutine(CelebrateRoutine());

        bool solved = ItemSessionData.IsPuzzleSolvedFor(persistence.instanceID);

        if (solved)
        {
            if (auraTinted)
            {
                spriteRenderer.color = Color.white;
                auraTinted = false;
            }

            return;
        }

        float pulse = (Mathf.Sin(Time.time * cursedAuraSpeed + auraPhase) + 1f) * 0.5f;

        spriteRenderer.color = Color.Lerp(
            Color.white,
            cursedAuraColor,
            pulse * cursedAuraAmount
        );

        auraTinted = true;
    }

    IEnumerator CelebrateRoutine()
    {
        yield return new WaitForSeconds(0.35f);

        Wobble(1f);

        FloatingText.Spawn(
            "Cleansed!",
            transform.position + Vector3.up * 0.9f,
            new Color(0.6f, 1f, 0.8f),
            5f
        );
    }

    // =================================
    // LOCK / FEEDBACK / COLLECT
    // =================================

    public void SetLocked(bool locked)
    {
        isLocked = locked;

        if (locked && isDragging)
        {
            isDragging = false;

            if (DraggedItem == this)
                DraggedItem = null;
        }
    }

    /// <summary>เด้งยุบนิดหน่อยเป็น feedback (เช่นตอนหยิบไม่ได้)</summary>
    public void Wobble(float strength)
    {
        currentSquashAmount = squashAmount * Mathf.Clamp01(strength);
        squashTimer = 0f;
        isSquashing = true;
    }

    /// <summary>สินค้าลอยเข้าหาเป้าหมายพร้อมหดเล็กลง (ใช้ตอนลูกค้ารับของ)</summary>
    public IEnumerator PlayCollectAnimation(Transform target, float duration)
    {
        isLocked = true;
        isDragging = false;
        isReturning = false;

        if (DraggedItem == this)
            DraggedItem = null;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        foreach (Collider2D c in GetComponentsInChildren<Collider2D>())
            c.enabled = false;

        if (workArea != null)
            workArea.ReleaseItem(gameObject);

        Vector3 start = transform.position;
        float t = 0f;
        duration = Mathf.Max(duration, 0.05f);

        while (t < 1f)
        {
            t += Time.deltaTime / duration;

            float n = Mathf.Clamp01(t);
            float eased = n * n;

            Vector3 end = target != null
                ? target.position + Vector3.up * 0.4f
                : start;

            Vector3 pos = Vector3.Lerp(start, end, eased);
            pos.y += Mathf.Sin(n * Mathf.PI) * 0.4f;

            transform.position = pos;
            externalScale = Mathf.Lerp(1f, 0.15f, eased);

            yield return null;
        }
    }

    public void RestoreAfterLoad(bool landed)
    {
        isDragging = false;
        isReturning = false;
        isHovering = false;

        isSquashing = false;
        squashValue = 0f;
        scaleMul = 1f;
        lift = 0f;

        hasLanded = landed;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

}
