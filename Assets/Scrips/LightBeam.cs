
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class LightBeam : MonoBehaviour
{
    [Header("References")]
    public Transform beamOrigin;
    public LightPuzzleManager puzzleManager;

    [Header("Layer Settings")]
    public LayerMask mirrorLayer;
    public LayerMask targetLayer;
    public LayerMask wallLayer;

    [Header("Beam Settings")]
    public float beamDistance = 20f;
    public int maxReflections = 10;
    public float startOffset = 0.01f;

    private LineRenderer lineRenderer;

    private bool targetReached;
    private bool wasTargetReached;

    public bool TargetReached => targetReached;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        CalculateBeam();
    }

    // =====================================
    // CALCULATE AND DRAW BEAM
    // =====================================

    private void CalculateBeam()
    {
        targetReached = TraceBeam(true);

        if (targetReached && !wasTargetReached)
        {
            if (puzzleManager != null)
            {
                puzzleManager.CompletePuzzle();
            }
        }

        wasTargetReached = targetReached;
    }

    // =====================================
    // TRACE BEAM
    // =====================================

    private bool TraceBeam(bool drawBeam)
    {
        if (beamOrigin == null)
            return false;

        List<Vector3> beamPositions = new List<Vector3>();

        Vector2 origin = beamOrigin.position;
        Vector2 direction = beamOrigin.right.normalized;

        bool reachedTarget = false;

        int combinedLayer =
            mirrorLayer.value |
            targetLayer.value |
            wallLayer.value;

        beamPositions.Add(origin);

        for (int i = 0; i <= maxReflections; i++)
        {
            RaycastHit2D hit = Physics2D.Raycast(
                origin,
                direction,
                beamDistance,
                combinedLayer
            );

            // Nothing was hit.
            if (hit.collider == null)
            {
                Vector2 endPosition =
                    origin + direction * beamDistance;

                beamPositions.Add(endPosition);
                break;
            }

            beamPositions.Add(hit.point);

            int hitLayer =
                1 << hit.collider.gameObject.layer;

            // Target reached.
            if ((targetLayer.value & hitLayer) != 0)
            {
                reachedTarget = true;
                break;
            }

            // Wall blocks the beam.
            if ((wallLayer.value & hitLayer) != 0)
            {
                break;
            }

            // Mirror reflection.
            if ((mirrorLayer.value & hitLayer) != 0)
            {
                direction = Vector2.Reflect(
                    direction,
                    hit.normal
                ).normalized;

                origin = hit.point + direction * startOffset;
            }
            else
            {
                break;
            }
        }

        // Draw beam only when requested.
        if (drawBeam && lineRenderer != null)
        {
            lineRenderer.positionCount = beamPositions.Count;
            lineRenderer.SetPositions(beamPositions.ToArray());
        }

        return reachedTarget;
    }

    // =====================================
    // VALIDATE SOLUTION
    // =====================================

    public bool ValidateSolution()
    {
        return TraceBeam(false);
    }

    // =====================================
    // RESET BEAM STATE
    // =====================================

    public void ResetBeamState()
    {
        targetReached = false;
        wasTargetReached = false;
    }
}
