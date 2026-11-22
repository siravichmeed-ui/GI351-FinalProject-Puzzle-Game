using System;
using UnityEngine;

public class CustomerMovement : MonoBehaviour
{
    [Header("Movement")]
    public Transform startPoint;
    public Transform stopPoint;
    public Transform exitPoint;
    public float moveSpeed = 2f;
    [Tooltip("เวลาที่ใช้เร่งความเร็วตอนออกตัว")]
    public float accelTime = 0.5f;
    [Tooltip("เริ่มชะลอเมื่อเหลือระยะเท่านี้ก่อนถึงจุดหยุด")]
    public float slowDownDistance = 1.2f;
    [Range(0.1f, 1f)] public float minSpeedRatio = 0.3f;

    [Header("Walking Animation")]
    public float bobAmount = 0.04f;
    public float bobSpeed = 10f;
    public float swayAmount = 3f;
    public float swaySpeed = 8f;
    [Tooltip("ยืด/ยุบตัวตามจังหวะก้าวเท้า")]
    public float stepSquash = 0.05f;

    [Header("Footsteps (optional)")]
    public ParticleSystem stepDust;
    public int dustPerStep = 2;
    public AudioClip stepSfx;
    [Range(0f, 1f)] public float stepVolume = 0.4f;

    [Header("Arrival / Hop")]
    public float arrivalSquash = 0.12f;
    public float hopHeight = 0.35f;
    public float hopDuration = 0.35f;
    public float settleDecay = 8f;
    public float settleFrequency = 2.5f;
    public float settleDuration = 0.6f;

    // เรียกเมื่อลูกค้าเดินถึงจุดหยุด
    public event Action OnArrived;
    public event Action OnExited;

    public bool HasArrived
    {
        get { return hasArrived; }
    }

    private Vector3 currentPosition;
    private Quaternion startRotation;
    private Vector3 originalScale;
    private AudioSource audioSource;

    private bool isMoving = true;
    private bool hasArrived = false;
    private bool isLeaving = false;
    private float walkTimer = 0f;
    private float currentSpeed = 0f;
    private int lastStepIndex = 0;

    private float settleTimer = -1f;
    private float settleAmount = 0f;
    private float hopTimer = -1f;
    private float talkSquash = 0f;
    private bool hasRestoredState = false;

    void Awake()
    {
        originalScale = transform.localScale;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }
    void Start()
    {
        if (hasRestoredState)
            return;

        currentPosition = startPoint.position;
        transform.position = currentPosition;
        startRotation = transform.rotation;
    }


    void Update()
    {
        float dt = Time.deltaTime;

        float squash = 0f;   // + = ยืดแนวตั้ง, - = ยุบแบนกว้าง
        float yOffset = 0f;
        float angle = 0f;

        // ---------- เดิน ----------
        if (isMoving)
        {
            Vector3 targetPosition = isLeaving
    ? exitPoint.position
    : stopPoint.position;

            float dist = Vector3.Distance(currentPosition, targetPosition);

            // ชะลอก่อนถึงจุดหมาย
            float slow = Mathf.SmoothStep(
                minSpeedRatio, 1f,
                Mathf.Clamp01(dist / Mathf.Max(slowDownDistance, 0.01f))
            );

            float targetSpeed = moveSpeed * slow;
            float accel = moveSpeed / Mathf.Max(accelTime, 0.01f);
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * dt);

            currentPosition = Vector3.MoveTowards(
                currentPosition,
                targetPosition,
currentSpeed * dt
            );

            float ratio = Mathf.Clamp01(currentSpeed / Mathf.Max(moveSpeed, 0.01f));
            walkTimer += dt * ratio;

            float bobNorm = Mathf.Abs(Mathf.Sin(walkTimer * bobSpeed));

            yOffset = bobNorm * bobAmount * ratio;
            angle = Mathf.Sin(walkTimer * swaySpeed) * swayAmount * ratio;

            // เท้าแตะพื้น = ยุบ, ลอยสูงสุด = ยืด
            squash = (bobNorm - 0.5f) * 2f * stepSquash * ratio;

            // ตรวจจังหวะก้าว (ทุกครั้งที่ sin ข้ามศูนย์)
            int stepIndex = Mathf.FloorToInt(walkTimer * bobSpeed / Mathf.PI);
            if (stepIndex != lastStepIndex)
            {
                lastStepIndex = stepIndex;
                OnStep();
            }

            if (Vector3.Distance(currentPosition, targetPosition) < 0.01f)
            {
                if (isLeaving)
                    FinishExit();
                else
                    Arrive();

                yOffset = 0f;
                angle = 0f;
                squash = 0f;
            }
        }

        // ---------- กระโดด (Hop) ----------
        if (hopTimer >= 0f)
        {
            hopTimer += dt;
            float t = hopTimer / Mathf.Max(hopDuration, 0.01f);

            if (t >= 1f)
            {
                hopTimer = -1f;
                StartSettle(arrivalSquash * 1.3f);
            }
            else
            {
                yOffset += hopHeight * 4f * t * (1f - t);
                squash += Mathf.Sin(t * Mathf.PI) * 0.08f; // ยืดตัวตอนลอย
            }
        }

        // ---------- ลงพื้นแล้วสปริง ----------
        if (settleTimer >= 0f)
        {
            settleTimer += dt;

            if (settleTimer >= settleDuration)
            {
                settleTimer = -1f;
            }
            else
            {
                squash += -settleAmount
                          * Mathf.Exp(-settleDecay * settleTimer)
                          * Mathf.Cos(2f * Mathf.PI * settleFrequency * settleTimer);
            }
        }

        // ---------- เด้งตอนพูด ----------
        squash += talkSquash;
        talkSquash = Mathf.Lerp(talkSquash, 0f, 1f - Mathf.Exp(-12f * dt));

        // ---------- Apply ----------
        Vector3 basePos = isMoving
    ? currentPosition
    : (isLeaving ? exitPoint.position : stopPoint.position);

        transform.position = basePos + Vector3.up * yOffset;
        transform.rotation = startRotation * Quaternion.Euler(0f, 0f, angle);
        transform.localScale = new Vector3(
            originalScale.x * (1f - squash),
            originalScale.y * (1f + squash),
            originalScale.z
        );
    }

    // =================================
    // EVENTS
    // =================================

    void OnStep()
    {
        if (stepDust != null && dustPerStep > 0)
            stepDust.Emit(dustPerStep);

        if (stepSfx != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(stepSfx, stepVolume);
        }
    }

    void Arrive()
    {
        isMoving = false;
        hasArrived = true;
        currentPosition = stopPoint.position;

        StartSettle(arrivalSquash);

        if (stepDust != null)
            stepDust.Emit(dustPerStep * 3);

        if (OnArrived != null)
            OnArrived();
    }

    void StartSettle(float amount)
    {
        settleAmount = amount;
        settleTimer = 0f;
    }
    public void LeaveShop()
    {
        Debug.Log("LEAVE SHOP CALLED: " + gameObject.name, this);

        if (isLeaving)
        {
            Debug.Log("Customer is already leaving!", this);
            return;
        }

        if (exitPoint == null)
        {
            Debug.LogError(
                "CustomerMovement: Exit Point is not assigned!",
                this
            );
            return;
        }

        isLeaving = true;
        isMoving = true;
        currentSpeed = 0f;
        currentPosition = transform.position;

        Debug.Log(
            "Customer started leaving. Exit: " + exitPoint.position,
            this
        );
    }

    void FinishExit()
    {
        if (!isLeaving)
            return;

        isMoving = false;
        currentPosition = exitPoint.position;

        Debug.Log("CUSTOMER EXIT FINISHED: " + gameObject.name, this);

        if (OnExited != null)
            OnExited();
    }

    // =================================
    // PUBLIC (เรียกจากสคริปต์อื่น)
    // =================================

    // กระโดดดีใจ เช่น ตอนรับออเดอร์

    public void Hop()
    {
        hopTimer = 0f;
    }

    // ตัวเด้งเล็กน้อย เช่น ตอนพูด / เอาเมาส์ชี้
    public void TalkBounce(float amount)
    {
        talkSquash = amount;
    }

    public CustomerMovementSaveData CaptureMovementState()
    {
        CustomerMovementSaveData data =
            new CustomerMovementSaveData();

        data.posX = transform.position.x;
        data.posY = transform.position.y;
        data.posZ = transform.position.z;

        data.currentPosX = currentPosition.x;
        data.currentPosY = currentPosition.y;
        data.currentPosZ = currentPosition.z;

        data.rotationX = startRotation.x;
        data.rotationY = startRotation.y;
        data.rotationZ = startRotation.z;
        data.rotationW = startRotation.w;

        data.scaleX = originalScale.x;
        data.scaleY = originalScale.y;
        data.scaleZ = originalScale.z;

        data.isMoving = isMoving;
        data.hasArrived = hasArrived;
        data.isLeaving = isLeaving;

        data.walkTimer = walkTimer;
        data.currentSpeed = currentSpeed;
        data.lastStepIndex = lastStepIndex;

        data.settleTimer = settleTimer;
        data.settleAmount = settleAmount;
        data.hopTimer = hopTimer;
        data.talkSquash = talkSquash;

        return data;
    }

    public void RestoreMovementState(CustomerMovementSaveData data)
    {
        if (data == null)
            return;

        hasRestoredState = true;

        transform.position = new Vector3(
            data.posX,
            data.posY,
            data.posZ
        );

        currentPosition = new Vector3(
            data.currentPosX,
            data.currentPosY,
            data.currentPosZ
        );

        startRotation = new Quaternion(
            data.rotationX,
            data.rotationY,
            data.rotationZ,
            data.rotationW
        );

        originalScale = new Vector3(
            data.scaleX,
            data.scaleY,
            data.scaleZ
        );

        isMoving = data.isMoving;
        hasArrived = data.hasArrived;
        isLeaving = data.isLeaving;

        walkTimer = data.walkTimer;
        currentSpeed = data.currentSpeed;
        lastStepIndex = data.lastStepIndex;

        settleTimer = data.settleTimer;
        settleAmount = data.settleAmount;
        hopTimer = data.hopTimer;
        talkSquash = data.talkSquash;

        transform.rotation = startRotation;
        transform.localScale = originalScale;
    }

}