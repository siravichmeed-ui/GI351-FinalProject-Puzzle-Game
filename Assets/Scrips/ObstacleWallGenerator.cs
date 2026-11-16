
using UnityEngine;
using System.Collections.Generic;

public class ObstacleWallGenerator : MonoBehaviour
{
    [Header("Obstacle References")]
    public Transform[] obstacleRoots;

    [Header("Random Area")]
    public float minX = -6.5f;
    public float maxX = 6.5f;
    public float minY = -2.5f;
    public float maxY = 2.5f;

    [Header("Corridor Settings")]
    public float minPathDistance = 1.0f;
    public float maxPathDistance = 3.0f;
    public float minObjectDistance = 1.2f;
    public float minWallGap = 0.3f;

    [Header("Wall Settings")]
    public float minWallLength = 1.5f;
    public float maxWallLength = 3f;
    public float wallThickness = 0.5f;

    [Header("Generation Settings")]
    public int attemptsPerObstacle = 80;
    public float minObstacleCenterDistance = 1.5f;

    [Header("References")]
    public MirrorController[] mirrors;
    public Transform lightSource;
    public Transform target;

    private List<Vector2> solutionPath =
        new List<Vector2>();

    private List<Vector2> placedPositions =
        new List<Vector2>();

    // =====================================
    // GENERATE
    // =====================================

    public bool Generate()
    {
        if (obstacleRoots == null ||
            obstacleRoots.Length == 0)
            return true;

        if (lightSource == null || target == null)
        {
            Debug.LogError(
                "Obstacle Generator: Source or Target missing!"
            );

            return false;
        }

        BuildSolutionPath();

        if (solutionPath.Count < 2)
            return false;

        HideAllObstacles();

        placedPositions.Clear();

        for (int i = 0; i < obstacleRoots.Length; i++)
        {
            Transform root = obstacleRoots[i];

            if (root == null)
                continue;

            bool placed = false;

            // Distribute obstacles across path segments.
            int preferredSegment =
                i % (solutionPath.Count - 1);

            for (int attempt = 0;
                 attempt < attemptsPerObstacle;
                 attempt++)
            {
                int segmentIndex;

                // Try the preferred segment first.
                if (attempt < attemptsPerObstacle / 2)
                {
                    segmentIndex = preferredSegment;
                }
                else
                {
                    segmentIndex = Random.Range(
                        0,
                        solutionPath.Count - 1
                    );
                }

                Vector2 start = solutionPath[segmentIndex];
                Vector2 end = solutionPath[segmentIndex + 1];

                Vector2 pathDirection =
                    (end - start).normalized;

                if (pathDirection.sqrMagnitude < 0.001f)
                    continue;

                Vector2 perpendicular = new Vector2(
                    -pathDirection.y,
                    pathDirection.x
                );

                float t = Random.Range(0.25f, 0.75f);

                Vector2 pathPoint =
                    Vector2.Lerp(start, end, t);

                float side = Random.value < 0.5f ? 1f : -1f;

                float offset = Random.Range(
                    minPathDistance,
                    maxPathDistance
                );

                Vector2 candidate =
                    pathPoint + perpendicular * offset * side;

                // Reject clustered obstacle centers.
                if (!IsFarFromPlacedWalls(candidate))
                    continue;

                SetupShape(root, pathDirection);

                root.position = new Vector3(
                    candidate.x,
                    candidate.y,
                    root.position.z
                );

                root.gameObject.SetActive(true);

                Physics2D.SyncTransforms();

                if (IsPlacementValid(i))
                {
                    placedPositions.Add(candidate);
                    placed = true;
                    break;
                }

                root.gameObject.SetActive(false);
            }

            if (!placed)
            {
                HideAllObstacles();

                Debug.LogWarning(
                    "Layout rejected: Could not place " +
                    root.name
                );

                return false;
            }
        }

        Physics2D.SyncTransforms();

        Debug.Log(
            "Puzzle Layout Generated. Walls: " +
            placedPositions.Count
        );

        return true;
    }

    // =====================================
    // HIDE ALL OBSTACLES
    // =====================================

    private void HideAllObstacles()
    {
        foreach (Transform root in obstacleRoots)
        {
            if (root != null)
                root.gameObject.SetActive(false);
        }
    }

    // =====================================
    // BUILD SOLUTION PATH
    // =====================================

    private void BuildSolutionPath()
    {
        solutionPath.Clear();

        solutionPath.Add(lightSource.position);

        if (mirrors != null)
        {
            foreach (MirrorController mirror in mirrors)
            {
                if (mirror == null ||
                    !mirror.gameObject.activeInHierarchy)
                    continue;

                solutionPath.Add(
                    mirror.transform.position
                );
            }
        }

        solutionPath.Add(target.position);
    }

    // =====================================
    // CHECK WALL CENTER SPACING
    // =====================================

    private bool IsFarFromPlacedWalls(Vector2 candidate)
    {
        foreach (Vector2 placed in placedPositions)
        {
            float distance = Vector2.Distance(
                candidate,
                placed
            );

            if (distance < minObstacleCenterDistance)
                return false;
        }

        return true;
    }

    // =====================================
    // SETUP WALL SHAPE
    // =====================================

    private void SetupShape(
        Transform root,
        Vector2 pathDirection)
    {
        Transform segmentA = root.Find("Segment_A");
        Transform segmentB = root.Find("Segment_B");

        if (segmentA == null || segmentB == null)
        {
            Debug.LogError(
                root.name + " needs Segment_A and Segment_B"
            );

            return;
        }

        float pathAngle = Mathf.Atan2(
            pathDirection.y,
            pathDirection.x
        ) * Mathf.Rad2Deg;

        float length = Random.Range(
            minWallLength,
            maxWallLength
        );

        int shape = Random.Range(0, 4);

        segmentA.gameObject.SetActive(true);
        segmentA.localPosition = Vector3.zero;
        segmentA.localRotation = Quaternion.identity;

        segmentA.localScale = new Vector3(
            wallThickness,
            length,
            1f
        );

        if (shape == 0)
        {
            // Straight wall parallel to path.
            segmentB.gameObject.SetActive(false);

            root.rotation = Quaternion.Euler(
                0f, 0f, pathAngle - 90f
            );
        }
        else if (shape == 1)
        {
            // Straight wall perpendicular to path.
            segmentB.gameObject.SetActive(false);

            root.rotation = Quaternion.Euler(
                0f, 0f, pathAngle
            );
        }
        else
        {
            // L-shaped wall.
            segmentB.gameObject.SetActive(true);

            segmentB.localRotation = Quaternion.identity;

            segmentB.localScale = new Vector3(
                length * 0.75f,
                wallThickness,
                1f
            );

            float side = Random.value < 0.5f ? 1f : -1f;

            segmentB.localPosition = new Vector3(
                length * 0.375f,
                side * length * 0.5f,
                0f
            );

            root.rotation = Quaternion.Euler(
                0f, 0f, pathAngle - 90f
            );
        }
    }

    // =====================================
    // VALIDATE PLACEMENT
    // =====================================

    private bool IsPlacementValid(int currentIndex)
    {
        Transform root = obstacleRoots[currentIndex];

        Collider2D[] colliders =
            root.GetComponentsInChildren<Collider2D>();

        foreach (Collider2D current in colliders)
        {
            if (!current.enabled ||
                !current.gameObject.activeInHierarchy)
                continue;

            Bounds bounds = current.bounds;

            // Keep inside the puzzle area.
            if (bounds.min.x < minX ||
                bounds.max.x > maxX ||
                bounds.min.y < minY ||
                bounds.max.y > maxY)
                return false;

            // Keep near the route, but not on it.
            float pathDistance = GetPathDistance(bounds);

            if (pathDistance < minPathDistance ||
                pathDistance > maxPathDistance)
                return false;

            // Avoid active mirrors.
            if (mirrors != null)
            {
                foreach (MirrorController mirror in mirrors)
                {
                    if (mirror == null ||
                        !mirror.gameObject.activeInHierarchy)
                        continue;

                    Collider2D mirrorCollider =
                        mirror.GetComponent<Collider2D>();

                    if (mirrorCollider == null)
                        continue;

                    if (BoundsOverlap(
                        bounds,
                        mirrorCollider.bounds,
                        minWallGap))
                        return false;
                }
            }

            // Avoid previously placed walls.
            for (int j = 0; j < currentIndex; j++)
            {
                Transform previousRoot = obstacleRoots[j];

                if (previousRoot == null ||
                    !previousRoot.gameObject.activeInHierarchy)
                    continue;

                Collider2D[] previousColliders =
                    previousRoot.GetComponentsInChildren<Collider2D>();

                foreach (Collider2D previous in previousColliders)
                {
                    if (!previous.enabled ||
                        !previous.gameObject.activeInHierarchy)
                        continue;

                    if (BoundsOverlap(
                        bounds,
                        previous.bounds,
                        minWallGap))
                        return false;
                }
            }

            // Avoid source and target.
            if (PointTooClose(bounds, lightSource))
                return false;

            if (PointTooClose(bounds, target))
                return false;
        }

        return true;
    }

    // =====================================
    // BOUNDS OVERLAP
    // =====================================

    private bool BoundsOverlap(
        Bounds a,
        Bounds b,
        float gap)
    {
        Bounds expanded = a;
        expanded.Expand(gap * 2f);

        return expanded.Intersects(b);
    }

    // =====================================
    // PATH DISTANCE
    // =====================================

    private float GetPathDistance(Bounds bounds)
    {
        float shortestDistance = float.MaxValue;

        for (int i = 0; i < solutionPath.Count - 1; i++)
        {
            Vector2 start = solutionPath[i];
            Vector2 end = solutionPath[i + 1];

            const int samples = 30;

            for (int j = 0; j <= samples; j++)
            {
                float t = j / (float)samples;

                Vector2 point = Vector2.Lerp(
                    start,
                    end,
                    t
                );

                Vector2 closest = bounds.ClosestPoint(point);

                float distance = Vector2.Distance(
                    point,
                    closest
                );

                shortestDistance = Mathf.Min(
                    shortestDistance,
                    distance
                );
            }
        }

        return shortestDistance;
    }

    // =====================================
    // SOURCE / TARGET DISTANCE
    // =====================================

    private bool PointTooClose(
        Bounds bounds,
        Transform point)
    {
        if (point == null)
            return false;

        Vector2 closest = bounds.ClosestPoint(
            point.position
        );

        return Vector2.Distance(
            closest,
            point.position
        ) < minObjectDistance;
    }
}
