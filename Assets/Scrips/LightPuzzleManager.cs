
using UnityEngine;

public class LightPuzzleManager : MonoBehaviour
{
    public enum Difficulty
    {
        Random,
        Easy,
        Normal,
        Hard
    }
    [Header("Obstacle Walls")]
    public ObstacleWallGenerator obstacleWallGenerator;

    [Header("Difficulty")]
    public Difficulty difficulty = Difficulty.Random;

    [Header("References")]
    public Transform lightSource;
    public LightBeam lightBeam;
    public Transform target;
    public SpriteRenderer targetRenderer;
    public MirrorController[] mirrors;

    [Header("Random Area")]
    public float minX = -7.5f;
    public float maxX = 7.5f;
    public float minY = -3.5f;
    public float maxY = 3.5f;

    [Header("Position Settings")]
    public float sourceEdgePadding = 0.5f;
    public float targetEdgePadding = 0.5f;
    public float mirrorEdgePadding = 1.5f;
    public float mirrorXJitter = 0.5f;
    public float minMirrorGap = 0.25f;

    [Header("Random Rotation")]
    public float minScrambleAngle = 25f;
    public float maxScrambleAngle = 75f;

    [Header("Puzzle Validation")]
    public int maxGenerationAttempts = 100;

    [Header("Colors")]
    public Color normalTargetColor = Color.white;
    public Color solvedColor = Color.green;
    public Color normalBeamColor = Color.cyan;

    private int activeMirrorCount;
    private float[] solutionAngles;
    private bool isCompleted;

    public bool IsCompleted => isCompleted;

    // =====================================
    // START
    // =====================================

    private void Start()
    {
        if (lightSource == null &&
            lightBeam != null &&
            lightBeam.beamOrigin != null)
        {
            lightSource = lightBeam.beamOrigin.parent;
        }

        if (targetRenderer == null && target != null)
        {
            targetRenderer = target.GetComponent<SpriteRenderer>();
        }

        StartPuzzle(difficulty);
    }

    // =====================================
    // START PUZZLE
    // =====================================

    public void StartPuzzle(Difficulty selectedDifficulty)
    {
        isCompleted = false;

        if (selectedDifficulty == Difficulty.Random)
        {
            selectedDifficulty =
                (Difficulty)Random.Range(1, 4);
        }

        switch (selectedDifficulty)
        {
            case Difficulty.Easy:
                activeMirrorCount = 2;
                break;

            case Difficulty.Normal:
                activeMirrorCount = 4;
                break;

            case Difficulty.Hard:
                activeMirrorCount = 6;
                break;
        }

        if (lightSource == null ||
            lightBeam == null ||
            lightBeam.beamOrigin == null ||
            target == null)
        {
            Debug.LogError("Missing Light Puzzle References!");
            return;
        }

        if (mirrors == null ||
            mirrors.Length < activeMirrorCount)
        {
            Debug.LogError("Not enough Mirrors!");
            return;
        }

        for (int i = 0; i < activeMirrorCount; i++)
        {
            if (mirrors[i] == null ||
                mirrors[i].GetComponent<Collider2D>() == null)
            {
                Debug.LogError(
                    "Mirror " + i + " is missing MirrorController or Collider2D!"
                );
                return;
            }
        }

        ActivateMirrors();

        solutionAngles = new float[activeMirrorCount];

        lightBeam.requiredMirrorCount = activeMirrorCount;

        ResetPuzzleVisuals();
        lightBeam.ResetBeamState();

        bool validPuzzle = false;

        for (int attempt = 0;
             attempt < maxGenerationAttempts;
             attempt++)
        {
            RandomizeSourceAndTarget();
            RandomizeMirrorPositions();

            Physics2D.SyncTransforms();

            // Generate obstacle walls.
            if (obstacleWallGenerator != null)
            {
                if (!obstacleWallGenerator.Generate())
                {
                    continue;
                }
            }

            Physics2D.SyncTransforms();

            // Check mirror spacing before calculating the route.
            if (!AreMirrorsSeparated())
            {
                continue;
            }

            CalculateMirrorRotations();

            Physics2D.SyncTransforms();

            // The solution must hit every active mirror once
            // and then reach the target.
            if (lightBeam.ValidateSolution())
            {
                validPuzzle = true;

                Debug.Log(
                    "Puzzle generated successfully! Attempt: " +
                    (attempt + 1)
                );

                break;
            }
        }

        if (!validPuzzle)
        {
            Debug.LogError(
                "Cannot generate a valid puzzle. " +
                "Try changing Random Area or Mirror settings."
            );

            return;
        }

        ScrambleMirrors();

        Physics2D.SyncTransforms();

        lightBeam.ResetBeamState();

        Debug.Log("Difficulty: " + selectedDifficulty);
        Debug.Log("Active Mirrors: " + activeMirrorCount);
    }

    // =====================================
    // ACTIVATE MIRRORS
    // =====================================

    private void ActivateMirrors()
    {
        for (int i = 0; i < mirrors.Length; i++)
        {
            if (mirrors[i] == null)
                continue;

            bool shouldBeActive = i < activeMirrorCount;

            mirrors[i].gameObject.SetActive(shouldBeActive);

            if (shouldBeActive)
            {
                mirrors[i].UnlockRotation();
            }
        }
    }

    // =====================================
    // RANDOMIZE SOURCE AND TARGET
    // =====================================

    private void RandomizeSourceAndTarget()
    {
        float sourceY = Random.Range(minY, maxY);
        float targetY = Random.Range(minY, maxY);

        lightSource.position = new Vector3(
            minX + sourceEdgePadding,
            sourceY,
            lightSource.position.z
        );

        target.position = new Vector3(
            maxX - targetEdgePadding,
            targetY,
            target.position.z
        );
    }

    // =====================================
    // RANDOMIZE MIRROR POSITIONS
    // =====================================

    private void RandomizeMirrorPositions()
    {
        float left = minX + mirrorEdgePadding;
        float right = maxX - mirrorEdgePadding;

        float spacing =
            (right - left) / (activeMirrorCount + 1);

        float jitter = Mathf.Min(
            mirrorXJitter,
            spacing * 0.25f
        );

        for (int i = 0; i < activeMirrorCount; i++)
        {
            float baseX = left + spacing * (i + 1);

            float randomX = baseX +
                Random.Range(-jitter, jitter);

            float randomY = Random.Range(minY, maxY);

            mirrors[i].transform.position = new Vector3(
                randomX,
                randomY,
                mirrors[i].transform.position.z
            );
        }
    }

    // =====================================
    // CHECK MIRROR OVERLAP / SPACING
    // =====================================

    private bool AreMirrorsSeparated()
    {
        for (int i = 0; i < activeMirrorCount; i++)
        {
            Collider2D colliderA =
                mirrors[i].GetComponent<Collider2D>();

            if (colliderA == null)
                return false;

            for (int j = i + 1; j < activeMirrorCount; j++)
            {
                Collider2D colliderB =
                    mirrors[j].GetComponent<Collider2D>();

                if (colliderB == null)
                    return false;

                ColliderDistance2D distance =
                    colliderA.Distance(colliderB);

                // Reject overlapping or too-close mirrors.
                if (distance.distance < minMirrorGap)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // =====================================
    // CALCULATE SOLUTION ROTATIONS
    // =====================================

    private void CalculateMirrorRotations()
    {
        Vector2 sourceDirection =
            (Vector2)mirrors[0].transform.position -
            (Vector2)lightSource.position;

        float sourceAngle = Mathf.Atan2(
            sourceDirection.y,
            sourceDirection.x
        ) * Mathf.Rad2Deg;

        lightSource.rotation =
            Quaternion.Euler(0f, 0f, sourceAngle);

        Vector2 previousPosition =
            lightBeam.beamOrigin.position;

        for (int i = 0; i < activeMirrorCount; i++)
        {
            Vector2 mirrorPosition =
                mirrors[i].transform.position;

            Vector2 nextPosition;

            if (i == activeMirrorCount - 1)
            {
                nextPosition = target.position;
            }
            else
            {
                nextPosition =
                    mirrors[i + 1].transform.position;
            }

            Vector2 incoming =
                (mirrorPosition - previousPosition).normalized;

            Vector2 outgoing =
                (nextPosition - mirrorPosition).normalized;

            Vector2 normal =
                (incoming - outgoing).normalized;

            if (normal.sqrMagnitude < 0.0001f)
            {
                normal = new Vector2(
                    -incoming.y,
                    incoming.x
                );
            }

            float correctAngle = Mathf.Atan2(
                normal.y,
                normal.x
            ) * Mathf.Rad2Deg;

            solutionAngles[i] = correctAngle;

            mirrors[i].transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    correctAngle
                );

            previousPosition = mirrorPosition;
        }
    }

    // =====================================
    // SCRAMBLE MIRRORS
    // =====================================

    private void ScrambleMirrors()
    {
        for (int i = 0; i < activeMirrorCount; i++)
        {
            float scramble = Random.Range(
                minScrambleAngle,
                maxScrambleAngle
            );

            if (Random.value < 0.5f)
            {
                scramble *= -1f;
            }

            mirrors[i].transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    solutionAngles[i] + scramble
                );
        }
    }

    // =====================================
    // RESET VISUALS
    // =====================================

    private void ResetPuzzleVisuals()
    {
        if (targetRenderer != null)
        {
            targetRenderer.color = normalTargetColor;
        }

        if (lightBeam != null)
        {
            LineRenderer line =
                lightBeam.GetComponent<LineRenderer>();

            if (line != null)
            {
                line.startColor = normalBeamColor;
                line.endColor = normalBeamColor;
            }
        }
    }

    // =====================================
    // COMPLETE PUZZLE
    // =====================================

    public void CompletePuzzle()
    {
        if (isCompleted)
            return;

        isCompleted = true;

        if (targetRenderer != null)
        {
            targetRenderer.color = solvedColor;
        }

        foreach (MirrorController mirror in mirrors)
        {
            if (mirror != null)
            {
                mirror.LockRotation();
            }
        }

        if (lightBeam != null)
        {
            LineRenderer line =
                lightBeam.GetComponent<LineRenderer>();

            if (line != null)
            {
                line.startColor = solvedColor;
                line.endColor = solvedColor;
            }
        }

        Debug.Log("PUZZLE COMPLETE!");
    }
}
