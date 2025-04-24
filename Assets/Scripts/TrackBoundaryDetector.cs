using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Automatically detects track boundaries and generates invisible trigger walls
/// for off-track detection by ML-Agents raycasts and triggers.
/// Requires a dedicated physics layer 'TrackBoundaryWall'.
/// </summary>

// Required for OrderBy, Select

public class TrackBoundaryDetector : MonoBehaviour
{
    // Should be different from trackLayer
    [Tooltip("The dedicated physics layer for the invisible boundary walls.")]
    public LayerMask boundaryWallLayer;

    // Default layer often
    [Tooltip("Layer mask for the off-track/grass area.")]
    public LayerMask grassLayer = 1 << 0;

    // Assign the 'TrackBoundaryWall' layer index here in inspector!

    [Header("Scanning Parameters")]
    [Tooltip("Resolution of the grid used for initial boundary sampling (lower = more detailed but slower).")]
    public float gridResolution = 1.5f;

    // RDP tolerance
    [Tooltip("Minimum length for a wall segment after simplification.")]
    public float minWallSegmentLength = 1.0f;

    [Tooltip("Height above potential ground to start raycasts.")]
    public float raycastHeightOffset = 10.0f;

    [Tooltip("Maximum distance downwards for raycasts.")]
    public float raycastMaxDistance = 20.0f;

    [Tooltip("How far out from the GameObject's position to scan for track.")]
    public float scanRadius = 200.0f;

    public bool showBoundaryWalls = true;
    public bool showDetectedEdges = true;

    [Header("Debug Visualization")]
    public bool showScanGrid = false;

    [Tooltip("Simplification tolerance. Higher values create fewer, longer wall segments.")]
    public float simplificationTolerance = 0.5f;

    public Color trackEdgeColor = Color.yellow;

    [Header("Layer Settings")]
    [Tooltip("Layer mask for the drivable track surface.")]
    public LayerMask trackLayer = 1 << 0;

    public Color wallColor = new Color(1f, 0f, 1f, 0.5f);

    [Header("Boundary Wall Generation")]
    [Tooltip("Height of the invisible boundary walls.")]
    public float wallHeight = 10.0f;

    [Tooltip("Thickness of the boundary wall colliders.")]
    public float wallThickness = 0.2f;

    private int boundaryWallLayerIndex = -1;
    private bool isScanning = false;
    private Coroutine scanCoroutine;

    // Points just outside track edge

    // --- Private ---
    private List<GameObject> wallObjects = new List<GameObject>();

    // Magenta


    // --- Public Access ---
    // *** CORRECTION: Ensure correct property name and accessibility ***
    public bool HasScanned { get; private set; } = false;

    public List<Vector3> InnerBoundaryPoints { get; private set; } = new List<Vector3>();

    // Points just inside track edge
    public List<Vector3> OuterBoundaryPoints { get; private set; } = new List<Vector3>();

    /// <summary>
    /// Destroys all generated wall GameObjects.
    /// </summary>
    public void ClearBoundaryWalls()
    {
        // Iterate backwards to safely remove from list while destroying
        for (int i = wallObjects.Count - 1; i >= 0; i--)
        {
            if (wallObjects[i] != null)
            {
                // Use DestroyImmediate in editor, Destroy in play mode
                if (Application.isPlaying) Destroy(wallObjects[i]);
                else DestroyImmediate(wallObjects[i]);
            }
        }
        wallObjects.Clear();
    }

    /// <summary>
    /// Gets the closest boundary points (inner and outer) to a given position.
    /// Note: This relies on the simplified points stored in Inner/OuterBoundaryPoints.
    /// The classification into Inner/Outer is heuristic and might be swapped.
    /// </summary>
    public bool GetClosestBoundaryPoints(Vector3 position, out Vector3 closestInnerPoint, out Vector3 closestOuterPoint, float maxDistance = 50f)
    {
        closestInnerPoint = FindClosestPointOnLine(InnerBoundaryPoints, position, maxDistance);
        closestOuterPoint = FindClosestPointOnLine(OuterBoundaryPoints, position, maxDistance);

        // Check if valid points were found within maxDistance (Vector3.positiveInfinity indicates not found)
        bool innerFound = (closestInnerPoint.x != float.PositiveInfinity);
        bool outerFound = (closestOuterPoint.x != float.PositiveInfinity);

        if (!innerFound) closestInnerPoint = Vector3.zero; // Return zero if not found
        if (!outerFound) closestOuterPoint = Vector3.zero;

        // Return true only if BOTH inner and outer points were found within the range
        return innerFound && outerFound;
    }

    // *** CORRECTION: Ensure correct method name (was already correct, just confirming) ***
    public IEnumerator ScanAndGenerateBoundaries()
    {
        if (isScanning)
        { // Prevent multiple scans running concurrently
            Debug.LogWarning("[TrackBoundaryDetector] Scan already in progress.");
            yield break;
        }
        isScanning = true;
        HasScanned = false; // Mark as not scanned until complete
        Debug.Log("[TrackBoundaryDetector] Starting boundary scan...");

        ClearBoundaryWalls();
        InnerBoundaryPoints.Clear();
        OuterBoundaryPoints.Clear();
        var edgePoints = new List<EdgePoint>(); // Store potential edge points

        int gridSize = Mathf.CeilToInt(scanRadius * 2f / gridResolution);
        Vector3 scanOrigin = transform.position; // Scan around the detector's position
        Vector3 gridStart = scanOrigin - new Vector3(scanRadius, 0, scanRadius);

        // --- Step 1: Grid Scan ---
        Debug.Log($"[TrackBoundaryDetector] Scanning grid ({gridSize}x{gridSize})...");
        int pointsChecked = 0;
        for (int x = 0; x <= gridSize; x++)
        {
            for (int z = 0; z <= gridSize; z++)
            {
                pointsChecked++;
                Vector3 currentGridWorldPos = gridStart + new Vector3(x * gridResolution, 0, z * gridResolution);
                Vector3 rayStart = currentGridWorldPos + Vector3.up * raycastHeightOffset;

                RaycastHit hit;
                bool isTrack = Physics.Raycast(rayStart, Vector3.down, out hit, raycastMaxDistance, trackLayer);
                bool isGrass = !isTrack && Physics.Raycast(rayStart, Vector3.down, out hit, raycastMaxDistance, grassLayer); // Only check grass if not track

                PointType currentPointType = isTrack ? PointType.Track : (isGrass ? PointType.Grass : PointType.None);
                Vector3 currentHitPoint = (currentPointType != PointType.None) ? hit.point : rayStart + Vector3.down * raycastMaxDistance; // Store hit point or default

                if (currentPointType == PointType.None && showScanGrid)
                {
                    Debug.DrawLine(rayStart, currentHitPoint, Color.grey, 10f);
                }
                if (showScanGrid && currentPointType != PointType.None)
                {
                    Debug.DrawLine(rayStart, currentHitPoint, isTrack ? Color.green : Color.red, 10f);
                }


                // Check neighbors (Right and Forward) to find transitions
                Vector3[] neighborsOffset = { Vector3.right, Vector3.forward }; // Relative offsets

                for (int n = 0; n < 2; n++) // Check Right (n=0) and Forward (n=1)
                {
                    Vector3 neighborRayStart = rayStart + neighborsOffset[n] * gridResolution;
                    RaycastHit neighborHit;
                    bool neighborIsTrack = Physics.Raycast(neighborRayStart, Vector3.down, out neighborHit, raycastMaxDistance, trackLayer);
                    bool neighborIsGrass = !neighborIsTrack && Physics.Raycast(neighborRayStart, Vector3.down, out neighborHit, raycastMaxDistance, grassLayer);
                    PointType neighborType = neighborIsTrack ? PointType.Track : (neighborIsGrass ? PointType.Grass : PointType.None);
                    Vector3 neighborHitPoint = (neighborType != PointType.None) ? neighborHit.point : neighborRayStart + Vector3.down * raycastMaxDistance;

                    // --- Detect Transition ---
                    if (currentPointType != PointType.None && neighborType != PointType.None && currentPointType != neighborType)
                    {
                        // Transition detected between current point and neighbor 'n'
                        Vector3 edgePos = (currentHitPoint + neighborHitPoint) / 2.0f; // Midpoint as edge estimate

                        // Add to edge list, store which side is track (relative to the edge segment direction)
                        EdgeDirection trackSide;
                        if (currentPointType == PointType.Track)
                        {
                            // Current is Track, Neighbor is Grass/None
                            trackSide = (n == 0) ? EdgeDirection.Left : EdgeDirection.Down; // Edge is to the Right or Forward of Track point
                        }
                        else
                        {
                            // Current is Grass/None, Neighbor is Track
                            trackSide = (n == 0) ? EdgeDirection.Right : EdgeDirection.Up; // Edge is to the Left or Below the Track point
                        }

                        edgePoints.Add(new EdgePoint { Position = edgePos, TrackSide = trackSide });
                    }
                }
            }
            // Yield occasionally during grid scan
            if (x % (gridSize / 10 + 1) == 0) // Yield ~10 times during scan
            {
                Debug.Log($"[TrackBoundaryDetector] Scanning grid... {((float)x / gridSize) * 100f:F0}% complete ({pointsChecked} points)");
                yield return null;
            }
        }
        Debug.Log($"[TrackBoundaryDetector] Grid scan complete. Found {edgePoints.Count} potential edge points.");

        if (edgePoints.Count < 10)
        {
            Debug.LogError("[TrackBoundaryDetector] Found too few edge points (<10). Check layer masks, scan radius, and grid resolution. Ensure track/grass contrast exists.", this);
            isScanning = false;
            yield break;
        }

        // --- Step 2: Connect and Order Edge Points ---
        Debug.Log("[TrackBoundaryDetector] Ordering edge points into boundary lines...");
        List<List<Vector3>> boundaryLines = OrderEdgePoints(edgePoints);
        Debug.Log($"[TrackBoundaryDetector] Formed {boundaryLines.Count} boundary line(s).");

        if (boundaryLines.Count == 0)
        {
            Debug.LogError("[TrackBoundaryDetector] Failed to order edge points into lines.", this);
            isScanning = false;
            yield break;
        }

        // Classify lines as Inner/Outer based on heuristics (e.g., longest two are likely inner/outer)
        // A robust classification is hard. Let's just simplify and generate walls for all significant lines found.
        // Clear previous points before adding new ones
        InnerBoundaryPoints.Clear();
        OuterBoundaryPoints.Clear();

        List<List<Vector3>> simplifiedLines = new List<List<Vector3>>();
        Debug.Log("[TrackBoundaryDetector] Simplifying boundary lines...");
        for (int i = 0; i < boundaryLines.Count; i++)
        {
            if (boundaryLines[i].Count < 2) continue;
            List<Vector3> simplified = SimplifyLine(boundaryLines[i], simplificationTolerance);
            // Filter out very short lines after simplification
            if (CalculateLineLength(simplified) > minWallSegmentLength * 2)
            {
                simplifiedLines.Add(simplified);
                Debug.Log($"[TrackBoundaryDetector] Simplified line {i} to {simplified.Count} points (Length: {CalculateLineLength(simplified):F1}m).");
                // Heuristic: Add longest lines to Inner/Outer for GetClosestBoundaryPoints compatibility
                // This is NOT guaranteed to be correct classification.
                if (i == 0) OuterBoundaryPoints = simplified; // Assume longest is outer
                else if (i == 1) InnerBoundaryPoints = simplified; // Assume second longest is inner
            }
            else
            {
                Debug.Log($"[TrackBoundaryDetector] Discarded simplified line {i} (too short).");
            }
        }


        // --- Step 4: Generate Wall Colliders ---
        Debug.Log("[TrackBoundaryDetector] Generating boundary wall colliders...");
        int wallContainerIndex = 0;
        foreach (var line in simplifiedLines)
        {
            GenerateWallsForLine(line, $"BoundaryWalls_{wallContainerIndex++}");
        }
        Debug.Log($"[TrackBoundaryDetector] Generated {wallObjects.Count} wall segments in total.");

        HasScanned = true; // Mark scan as complete
        isScanning = false;
        Debug.Log("[TrackBoundaryDetector] Boundary scan and wall generation complete.");
        scanCoroutine = null;
    }

    private float CalculateLineLength(List<Vector3> line)
    {
        float length = 0f;
        if (line == null || line.Count < 2) return 0f;
        for (int i = 0; i < line.Count - 1; i++)
        {
            length += Vector3.Distance(line[i], line[i + 1]);
        }
        // Add loop connection distance if it seems closed
        bool isClosedLoop = line.Count > 2 && (line.Last() - line.First()).magnitude < gridResolution * 3f;
        if (isClosedLoop)
        {
            length += Vector3.Distance(line.Last(), line.First());
        }
        return length;
    }

    private void DrawLineGizmo(List<Vector3> linePoints, Color color)
    {
        if (linePoints == null || linePoints.Count < 2) return;
        Gizmos.color = color;
        for (int i = 0; i < linePoints.Count - 1; i++)
        {
            Gizmos.DrawLine(linePoints[i] + Vector3.up * 0.1f, linePoints[i + 1] + Vector3.up * 0.1f); // Draw slightly above ground
        }
        // Draw loop connection if applicable
        bool isClosedLoop = linePoints.Count > 2 && (linePoints.Last() - linePoints.First()).magnitude < gridResolution * 3f;
        if (isClosedLoop)
        {
            Gizmos.DrawLine(linePoints.Last() + Vector3.up * 0.1f, linePoints.First() + Vector3.up * 0.1f);
        }
    }

    private void DrawWireCubeGizmo(Transform targetTransform, Vector3 center, Vector3 size)
    {
        Matrix4x4 originalMatrix = Gizmos.matrix; // Store original matrix
        Gizmos.matrix = targetTransform.localToWorldMatrix;
        Gizmos.DrawWireCube(center, size);
        Gizmos.matrix = originalMatrix; // Restore original matrix
    }

    /// <summary>
    /// Finds the closest point on a polyline (list of connected points) to a given position, within maxDistance.
    /// Returns Vector3.positiveInfinity if no point/segment is within maxDistance.
    /// </summary>
    private Vector3 FindClosestPointOnLine(List<Vector3> linePoints, Vector3 position, float maxDistance)
    {
        if (linePoints == null || linePoints.Count < 1) return Vector3.positiveInfinity; // Indicate not found

        Vector3 closestPointFound = Vector3.positiveInfinity;
        float minDistanceSq = maxDistance * maxDistance;

        // Check individual points first
        foreach (var p in linePoints)
        {
            float distSq = (p - position).sqrMagnitude;
            if (distSq < minDistanceSq)
            {
                minDistanceSq = distSq;
                closestPointFound = p;
            }
        }

        // Check line segments
        for (int i = 0; i < linePoints.Count - 1; i++)
        {
            Vector3 segmentStart = linePoints[i];
            Vector3 segmentEnd = linePoints[i + 1];
            float distSq = PerpendicularDistanceSqToSegment(position, segmentStart, segmentEnd);

            if (distSq < minDistanceSq)
            {
                minDistanceSq = distSq;
                // Calculate the actual closest point on the segment
                closestPointFound = GetClosestPointOnSegment(position, segmentStart, segmentEnd);
            }
        }

        // Check segment connecting last to first if it's likely a closed loop
        bool isClosedLoop = linePoints.Count > 2 && (linePoints.Last() - linePoints.First()).magnitude < gridResolution * 3f;
        if (isClosedLoop)
        {
            Vector3 segmentStart = linePoints.Last();
            Vector3 segmentEnd = linePoints.First();
            float distSq = PerpendicularDistanceSqToSegment(position, segmentStart, segmentEnd);
            if (distSq < minDistanceSq)
            {
                minDistanceSq = distSq;
                closestPointFound = GetClosestPointOnSegment(position, segmentStart, segmentEnd);
            }
        }

        return closestPointFound; // Returns positiveInfinity if no point/segment was within maxDistance
    }

    /// <summary>
    /// Generates BoxCollider walls along a given line of points.
    /// </summary>
    private void GenerateWallsForLine(List<Vector3> linePoints, string containerName)
    {
        if (linePoints.Count < 2) return;

        GameObject lineContainer = new GameObject(containerName);
        lineContainer.transform.SetParent(this.transform);
        // Setting layer on container might not be necessary if segments have it, but can't hurt.
        // lineContainer.layer = boundaryWallLayerIndex;
        wallObjects.Add(lineContainer); // Add container to list for cleanup

        for (int i = 0; i < linePoints.Count - 1; i++)
        {
            Vector3 p1 = linePoints[i];
            Vector3 p2 = linePoints[i + 1];
            Vector3 direction = (p2 - p1);
            float segmentLength = direction.magnitude;

            // Skip very short segments resulting from simplification
            if (segmentLength < minWallSegmentLength) continue;

            GameObject wallSegment = new GameObject($"Wall_{i}-{i + 1}");
            wallSegment.transform.SetParent(lineContainer.transform);
            wallSegment.layer = boundaryWallLayerIndex; // Ensure segment is on the correct layer

            // Position the segment at the midpoint, centered vertically
            wallSegment.transform.position = (p1 + p2) / 2.0f + Vector3.up * (wallHeight / 2.0f);

            // Rotate the segment to align with the line direction (LookRotation needs non-zero vector)
            if (direction != Vector3.zero)
            {
                wallSegment.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }


            // Add and configure the BoxCollider
            BoxCollider collider = wallSegment.AddComponent<BoxCollider>();
            collider.isTrigger = true; // Make it a trigger
            // Size collider in local space: X=thickness, Y=height, Z=length
            collider.size = new Vector3(wallThickness, wallHeight, segmentLength);

            wallObjects.Add(wallSegment); // Add segment itself to list for easier cleanup
        }

        // Check if the line is likely a closed loop and add closing segment if needed
        bool isClosedLoop = linePoints.Count > 2 && (linePoints.Last() - linePoints.First()).magnitude < gridResolution * 3f; // Heuristic check
        if (isClosedLoop)
        {
            Vector3 p1 = linePoints.Last();
            Vector3 p2 = linePoints.First();
            Vector3 direction = (p2 - p1);
            float segmentLength = direction.magnitude;

            if (segmentLength >= minWallSegmentLength)
            {
                GameObject wallSegment = new GameObject($"Wall_{linePoints.Count - 1}-0");
                wallSegment.transform.SetParent(lineContainer.transform);
                wallSegment.layer = boundaryWallLayerIndex;
                wallSegment.transform.position = (p1 + p2) / 2.0f + Vector3.up * (wallHeight / 2.0f);
                if (direction != Vector3.zero) wallSegment.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                BoxCollider collider = wallSegment.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new Vector3(wallThickness, wallHeight, segmentLength);
                wallObjects.Add(wallSegment);
            }
        }
    }

    /// <summary>
    /// Gets the closest point on a line segment to a given point.
    /// </summary>
    private Vector3 GetClosestPointOnSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 lineDir = lineEnd - lineStart;
        float lineLengthSq = lineDir.sqrMagnitude;
        if (lineLengthSq < 0.000001f) return lineStart;
        float t = Vector3.Dot(point - lineStart, lineDir) / lineLengthSq;
        t = Mathf.Clamp01(t);
        return lineStart + t * lineDir;
    }

    void OnDestroy()
    {
        ClearBoundaryWalls();
        if (scanCoroutine != null) StopCoroutine(scanCoroutine);
    }

    // --- Gizmos ---
    private void OnDrawGizmos() // Changed to OnDrawGizmos to always show if enabled
    {
        // Draw Simplified Boundary Points if scan is complete
        if (HasScanned && showDetectedEdges)
        {
            // Draw Inner line (using simplified points)
            DrawLineGizmo(InnerBoundaryPoints, Color.blue); // Blue for Inner
                                                            // Draw Outer line (using simplified points)
            DrawLineGizmo(OuterBoundaryPoints, Color.red);   // Red for Outer

            // Draw spheres at simplified points
            Gizmos.color = Color.blue;
            foreach (var p in InnerBoundaryPoints) Gizmos.DrawSphere(p, 0.3f);
            Gizmos.color = Color.red;
            foreach (var p in OuterBoundaryPoints) Gizmos.DrawSphere(p, 0.3f);
        }

        // Draw Wall Colliders if generated
        if (showBoundaryWalls && wallObjects.Count > 0)
        {
            Gizmos.color = wallColor;
            foreach (GameObject wallObject in wallObjects)
            {
                if (wallObject == null) continue;

                // Check if it's a container or a segment
                BoxCollider col = wallObject.GetComponent<BoxCollider>();
                if (col != null)
                { // It's a segment
                    DrawWireCubeGizmo(wallObject.transform, col.center, col.size);
                }
                else
                { // It's likely a container
                    foreach (Transform segmentTransform in wallObject.transform)
                    {
                        if (segmentTransform == null) continue;
                        BoxCollider segmentCol = segmentTransform.GetComponent<BoxCollider>();
                        if (segmentCol != null)
                        {
                            DrawWireCubeGizmo(segmentTransform, segmentCol.center, segmentCol.size);
                        }
                    }
                }
            }
            Gizmos.matrix = Matrix4x4.identity; // Reset gizmo matrix just in case
        }
    }

    /// <summary>
    /// Tries em
    /// <summary>
    /// Tries to connect nearby edge points into continuous lines.
    /// This is a complex problem, using a simple nearest-neighbor approach here.
    /// </summary>

    // Prevent concurrent scans


    private List<List<Vector3>> OrderEdgePoints(List<EdgePoint> points)
    {
        List<List<Vector3>> lines = new List<List<Vector3>>();
        HashSet<int> usedPointIndices = new HashSet<int>();
        float connectThresholdSq = Mathf.Pow(gridResolution * 2.5f, 2); // Max distance SQUARED to connect points

        for (int startIndex = 0; startIndex < points.Count; startIndex++)
        {
            if (usedPointIndices.Contains(startIndex)) continue; // Skip if already part of a line

            // Start a new line
            List<Vector3> currentLine = new List<Vector3>();
            lines.Add(currentLine);

            int currentIndex = startIndex;

            while (currentIndex != -1) // While we can find a next point
            {
                if (usedPointIndices.Contains(currentIndex)) break; // Already added this point (loop closed or error)

                currentLine.Add(points[currentIndex].Position);
                usedPointIndices.Add(currentIndex);

                int bestNeighborIndex = -1;
                float minDistSq = connectThresholdSq;

                // Find the closest *unused* neighbor
                for (int neighborIndex = 0; neighborIndex < points.Count; neighborIndex++)
                {
                    if (!usedPointIndices.Contains(neighborIndex) && neighborIndex != currentIndex)
                    {
                        float distSq = (points[neighborIndex].Position - points[currentIndex].Position).sqrMagnitude;
                        if (distSq < minDistSq)
                        {
                            minDistSq = distSq;
                            bestNeighborIndex = neighborIndex;
                        }
                    }
                }
                currentIndex = bestNeighborIndex; // Move to the closest neighbor index (-1 if none found)
            }
        }

        // Filter out very short lines
        lines.RemoveAll(line => line.Count < 3);

        return lines;
    }

    /// <summary>
    /// Calculates the squared perpendicular distance from a point to a line segment defined by lineStart and lineEnd.
    /// </summary>
    private float PerpendicularDistanceSq(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 lineDir = lineEnd - lineStart;
        float lineLengthSq = lineDir.sqrMagnitude;
        if (lineLengthSq < 0.000001f) return (point - lineStart).sqrMagnitude; // Line is effectively a point

        // Project point onto the line equation parameter t = Dot(point - lineStart, lineDir) / lineLengthSq
        float t = Vector3.Dot(point - lineStart, lineDir) / lineLengthSq;
        t = Mathf.Clamp01(t); // Clamp parameter to the segment [0, 1]

        Vector3 projection = lineStart + t * lineDir; // Calculate the closest point on the segment
        return (point - projection).sqrMagnitude; // Return squared distance
    }

    /// <summary>
    /// Calculates the squared distance from a point to the closest point on a line segment.
    /// </summary>
    private float PerpendicularDistanceSqToSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 lineDir = lineEnd - lineStart;
        float lineLengthSq = lineDir.sqrMagnitude;
        if (lineLengthSq < 0.000001f) return (point - lineStart).sqrMagnitude;
        float t = Vector3.Dot(point - lineStart, lineDir) / lineLengthSq;
        t = Mathf.Clamp01(t);
        Vector3 projection = lineStart + t * lineDir;
        return (point - projection).sqrMagnitude;
    }

    /// <summary>
    /// Simplifies a line using the Ramer-Douglas-Peucker algorithm.
    /// </summary>
    private List<Vector3> SimplifyLine(List<Vector3> points, float tolerance)
    {
        if (points == null || points.Count < 3)
            return points ?? new List<Vector3>();

        float toleranceSq = tolerance * tolerance;
        int firstPoint = 0;
        int lastPoint = points.Count - 1;
        List<int> pointIndicesToKeep = new List<int>();

        // Add the first and last index
        pointIndicesToKeep.Add(firstPoint);
        pointIndicesToKeep.Add(lastPoint);

        // The stack handles recursive calls iteratively
        Stack<KeyValuePair<int, int>> stack = new Stack<KeyValuePair<int, int>>();
        stack.Push(new KeyValuePair<int, int>(firstPoint, lastPoint));

        while (stack.Count > 0)
        {
            KeyValuePair<int, int> current = stack.Pop();
            int first = current.Key;
            int last = current.Value;

            // Find the point with the maximum distance from the segment (first, last)
            float maxDistanceSq = 0;
            int index = -1;
            for (int i = first + 1; i < last; i++)
            {
                float distanceSq = PerpendicularDistanceSq(points[i], points[first], points[last]);
                if (distanceSq > maxDistanceSq)
                {
                    index = i;
                    maxDistanceSq = distanceSq;
                }
            }

            // If max distance is greater than tolerance, recursively simplify
            if (index != -1 && maxDistanceSq > toleranceSq)
            {
                // Add the largest deviation point to the list of points to keep
                pointIndicesToKeep.Add(index);
                // Push the two sub-segments onto the stack to be processed
                stack.Push(new KeyValuePair<int, int>(first, index));
                stack.Push(new KeyValuePair<int, int>(index, last));
            }
        }

        // Sort the kept indices and build the resulting simplified line
        pointIndicesToKeep.Sort();
        List<Vector3> result = new List<Vector3>();
        foreach (int idx in pointIndicesToKeep)
        {
            result.Add(points[idx]);
        }

        return result;
    }

    void Start()
    {
        // Validate layer setup
        boundaryWallLayerIndex = LayerMaskHelper.GetLayerIndexFromMask(boundaryWallLayer);
        if (boundaryWallLayerIndex == -1 || boundaryWallLayer == 0)
        {
            Debug.LogError($"[TrackBoundaryDetector] 'Boundary Wall Layer' is not assigned or invalid! Please assign the 'TrackBoundaryWall' layer in the inspector.", this);
            enabled = false;
            return;
        }
        if (trackLayer == 0 || grassLayer == 0)
        {
            Debug.LogWarning($"[TrackBoundaryDetector] 'Track Layer' or 'Grass Layer' is not assigned. Detection might be inaccurate.", this);
        }
        if ((trackLayer & grassLayer) != 0)
        {
            Debug.LogWarning($"[TrackBoundaryDetector] 'Track Layer' and 'Grass Layer' should not overlap for best results.", this);
        }


        Debug.Log($"[TrackBoundaryDetector] Using layer '{LayerMask.LayerToName(boundaryWallLayerIndex)}' ({boundaryWallLayerIndex}) for boundary walls.");
        // Start scanning coroutine
        if (scanCoroutine != null) StopCoroutine(scanCoroutine); // Stop previous if any
        scanCoroutine = StartCoroutine(ScanAndGenerateBoundaries());
    }

    // Relative direction FROM the non-track point TO the track point

    private class EdgePoint
    {
        public Vector3 Position;

        public EdgeDirection TrackSide;
        // Indicates direction from this edge point towards the track center
    }

    private static class LayerMaskHelper
    {
        public static int GetLayerIndexFromMask(LayerMask layerMask)
        {
            int layerNumber = layerMask.value;
            int layerIndex = -1;
            int currentLayer = 0;
            while (layerNumber > 0 && currentLayer < 32)
            {
                if ((layerNumber & 1) == 1)
                {
                    if (layerIndex != -1) return -2; // Multiple layers in mask, ambiguous
                    layerIndex = currentLayer;
                }
                layerNumber >>= 1;
                currentLayer++;
            }
            return layerIndex; // -1 if mask is empty or None
        }
    }

    private enum EdgeDirection { Up, Down, Left, Right }

    // --- Helper Enums and Structs ---
    private enum PointType { None, Track, Grass }
}