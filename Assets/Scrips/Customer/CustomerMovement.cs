
using System;
using UnityEngine;

public class CustomerMovement : MonoBehaviour
{
    [Header("Movement")]
    public Transform startPoint;
    public Transform stopPoint;
    public Transform exitPoint;
    public float moveSpeed = 2f;
    public float accelTime = 0.5f;
    public float slowDownDistance = 1.2f;
    [Range(0.1f, 1f)] public float minSpeedRatio = 0.3f;

    [Header("Warp Feel (ความเร็วแบบพุ่ง)")]
    [Tooltip("คูณความเร็วตอนเดินเข้าไปที่ Stop Point")]
    public float enterSpeedMultiplier = 3f;
    [Tooltip("คูณความเร็วตอนเดินออกไปที่ Exit Point")]
    public float exitSpeedMultiplier = 4f;
    [Tooltip("คูณความเร็วตอนเดินมารับของคืน")]
    public float returnSpeedMultiplier = 3f;
    [Tooltip("เวลาเร่งความเร็ว (วินาที) เมื่อใช้ตัวคูณ > 1 — ยิ่งน้อยยิ่งพุ่งทันที")]
    public float warpAccelTime = 0.12f;
    [Tooltip("ตัวยืดตามแนวนอนตอนพุ่ง")]
    [Range(0f, 0.5f)] public float dashStretch = 0.18f;
    [Tooltip("ฝุ่นที่พ่นต่อวินาทีตอนพุ่ง")]
    public float dashDustPerSecond = 40f;

    [Header("Wind (ดันของแขวนที่เดินผ่าน)")]
    [Tooltip("แรงลมที่ลูกค้าพัดใส่ HangingProp ขณะเดิน (0 = ปิด)")]
    public float windPush = 5f;
    public float windRadius = 2.5f;

    [Header("Walking Animation")]
    public float bobAmount = 0.04f;
    public float bobSpeed = 10f;
    public float swayAmount = 3f;
    public float swaySpeed = 8f;
    public float stepSquash = 0.05f;

    [Header("Idle (ยืนรอ)")]
    [Tooltip("ลูกค้าหายใจเบาๆ ตอนยืนรอ ไม่นิ่งเป็นรูปปั้น")]
    public float idleBreathAmount = 0.015f;
    public float idleBreathSpeed = 2.2f;

    [Header("Footsteps")]
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

    public event Action OnArrived;
    public event Action OnExited;
    public event Action OnReturnArrived;


    public bool HasArrived => hasArrived;
    public bool IsLeaving => isLeaving;

    private Vector3 currentPosition;
    private Quaternion startRotation;
    private Vector3 originalScale;
    private AudioSource audioSource;

    private bool isMoving = true;
    private bool hasArrived;
    private bool isLeaving;
    private bool isReturning;
    private bool hasRestoredState;

    private Transform customTarget;

    private float walkTimer;
    private float currentSpeed;
    private int lastStepIndex;
    private float settleTimer = -1f;
    private float settleAmount;
    private float hopTimer = -1f;
    private float talkSquash;

    private void Awake()
    {
        originalScale = transform.localScale;
        startRotation = transform.rotation;
        idlePhase = UnityEngine.Random.value * Mathf.PI * 2f;

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }

    private void Start()
    {
        if (hasRestoredState)
            return;

        if (startPoint != null)
        {
            currentPosition = startPoint.position;
            transform.position = currentPosition;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        float squash = 0f;
        float stretch = 0f;
        float yOffset = 0f;
        float angle = 0f;

        if (isMoving)
        {
            Transform target = GetMovementTarget();

            if (target != null)
            {
                Vector3 targetPosition = target.position;
                float dist = Vector3.Distance(currentPosition, targetPosition);

                float slow = Mathf.SmoothStep(
                    minSpeedRatio,
                    1f,
                    Mathf.Clamp01(dist / Mathf.Max(slowDownDistance, 0.01f))
                );

                float speedMul = GetSpeedMultiplier();
                float topSpeed = moveSpeed * speedMul;
                float accelT = speedMul > 1.01f ? warpAccelTime : accelTime;

                float targetSpeed = topSpeed * slow;
                float accel = topSpeed / Mathf.Max(accelT, 0.01f);

                currentSpeed = Mathf.MoveTowards(
                    currentSpeed,
                    targetSpeed,
                    accel * dt
                );

                currentPosition = Vector3.MoveTowards(
                    currentPosition,
                    targetPosition,
                    currentSpeed * dt
                );

                float ratio = Mathf.Clamp01(
                    currentSpeed / Mathf.Max(topSpeed, 0.01f)
                );

                // ก้าวถี่ขึ้นตามความเร็ว แต่ไม่ถี่เท่าตัวคูณเต็มๆ
                walkTimer += dt * ratio * Mathf.Sqrt(speedMul);

                // เดิน/พุ่งผ่านของแขวน -> ลมพัดให้แกว่ง
                if (windPush > 0f)
                {
                    float dirX = targetPosition.x - currentPosition.x;
                    dirX = Mathf.Abs(dirX) > 0.01f ? Mathf.Sign(dirX) : 0f;

                    HangingProp.Disturb(
                        currentPosition,
                        dirX * currentSpeed * windPush * dt,
                        windRadius
                    );
                }

                // ยืดตัว + พ่นฝุ่นตอนพุ่ง
                if (speedMul > 1.01f)
                {
                    float dash = ratio * Mathf.Clamp01(speedMul - 1f);

                    stretch = dash * dashStretch;

                    if (stepDust != null && dash > 0.5f)
                    {
                        dustAccum += dashDustPerSecond * dt;
                        int emit = Mathf.FloorToInt(dustAccum);

                        if (emit > 0)
                        {
                            stepDust.Emit(emit);
                            dustAccum -= emit;
                        }
                    }
                }

                float bobNorm = Mathf.Abs(
                    Mathf.Sin(walkTimer * bobSpeed)
                );

                yOffset = bobNorm * bobAmount * ratio;
                angle = Mathf.Sin(walkTimer * swaySpeed) * swayAmount * ratio;
                squash = (bobNorm - 0.5f) * 2f * stepSquash * ratio;

                int stepIndex = Mathf.FloorToInt(
                    walkTimer * bobSpeed / Mathf.PI
                );

                if (stepIndex != lastStepIndex)
                {
                    lastStepIndex = stepIndex;
                    OnStep();
                }

                if (Vector3.Distance(currentPosition, targetPosition) < 0.01f)
                {
                    currentPosition = targetPosition;
                    isMoving = false;
                    currentSpeed = 0f;

                    if (isLeaving)
                    {
                        FinishExit();
                    }
                    else if (isReturning)
                    {
                        isReturning = false;
                        customTarget = null;
                        hasArrived = true;
                        StartSettle(arrivalSquash);

                        if (stepDust != null)
                            stepDust.Emit(dustPerStep * 3);

                        OnReturnArrived?.Invoke();
                    }
                    else
                    {
                        hasArrived = true;
                        StartSettle(arrivalSquash);

                        if (stepDust != null)
                            stepDust.Emit(dustPerStep * 3);

                        OnArrived?.Invoke();
                    }
                }
            }
        }

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
                squash += Mathf.Sin(t * Mathf.PI) * 0.08f;
            }
        }

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

        // หายใจตอนยืนรอที่จุดหยุด
        if (!isMoving && hasArrived)
        {
            squash += Mathf.Sin(Time.time * idleBreathSpeed + idlePhase) *
                      idleBreathAmount;
        }

        squash += talkSquash;
        talkSquash = Mathf.Lerp(
            talkSquash,
            0f,
            1f - Mathf.Exp(-12f * dt)
        );

        transform.position = currentPosition + Vector3.up * yOffset;
        transform.rotation = startRotation * Quaternion.Euler(0f, 0f, angle);

        transform.localScale = new Vector3(
            originalScale.x * (1f - squash) * (1f + stretch),
            originalScale.y * (1f + squash) * (1f - stretch * 0.5f),
            originalScale.z
        );
    }

    private float dustAccum;
    private float idlePhase;

    private float GetSpeedMultiplier()
    {
        if (isLeaving)
            return Mathf.Max(exitSpeedMultiplier, 0.1f);

        if (isReturning)
            return Mathf.Max(returnSpeedMultiplier, 0.1f);

        return Mathf.Max(enterSpeedMultiplier, 0.1f);
    }

    private Transform GetMovementTarget()
    {
        if (customTarget != null)
            return customTarget;

        if (isLeaving)
            return exitPoint;

        if (isReturning)
            return returnTarget;

        return stopPoint;
    }

    private Transform returnTarget;

    private void OnStep()
    {
        if (stepDust != null && dustPerStep > 0)
            stepDust.Emit(dustPerStep);

        if (stepSfx != null && audioSource != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(stepSfx, stepVolume);
        }
    }

    private void StartSettle(float amount)
    {
        settleAmount = amount;
        settleTimer = 0f;
    }

    public void LeaveShop()
    {
        if (isLeaving || exitPoint == null)
            return;

        isLeaving = true;
        isReturning = false;
        isMoving = true;
        hasArrived = false;
        customTarget = null;
        currentSpeed = 0f;
    }

    public void ReturnToShop(Transform destination)
    {
        if (destination == null)
        {
            Debug.LogError("Return destination is missing!", this);
            return;
        }

        isLeaving = false;
        isReturning = true;
        hasArrived = false;

        returnTarget = destination;
        customTarget = destination;

        isMoving = true;
        currentSpeed = 0f;
        currentPosition = transform.position;
    }

    public void WalkToPickupPoint(Transform destination)
    {
        ReturnToShop(destination);
    }

    private void FinishExit()
    {
        isMoving = false;
        isLeaving = false;
        currentPosition = exitPoint.position;

        OnExited?.Invoke();
    }

    public void Hop()
    {
        hopTimer = 0f;
    }

    public void TalkBounce(float amount)
    {
        talkSquash = amount;
    }

    public CustomerMovementSaveData CaptureMovementState()
    {
        CustomerMovementSaveData data = new CustomerMovementSaveData();

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
        data.isReturning = isReturning;

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
            data.posX, data.posY, data.posZ
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
            data.scaleX, data.scaleY, data.scaleZ
        );

        isMoving = data.isMoving;
        hasArrived = data.hasArrived;
        isLeaving = data.isLeaving;
        isReturning = data.isReturning;

        // The manager configures returnTarget before restoring this state.
        // Clear the custom target when the customer is already walking out.
        customTarget = isReturning ? returnTarget : null;

        walkTimer = data.walkTimer;
        currentSpeed = data.currentSpeed;
        lastStepIndex = data.lastStepIndex;

        settleTimer = data.settleTimer;
        settleAmount = data.settleAmount;
        hopTimer = data.hopTimer;
        talkSquash = data.talkSquash;
    }
}
