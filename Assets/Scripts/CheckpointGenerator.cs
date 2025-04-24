// --- START OF FILE CheckpointGenerator.cs ---

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq; // Added for OrderBy, Select

/// <summary>
/// Automatically generates checkpoints AND spawn points along the track centerline.
/// V2: Uses TrackBoundaryDetector for robust centerline calculation.
/// </summary>
public class CheckpointGenerator : MonoBehaviour
{
[Header("Dependencies")]
[Tooltip("REQUIRED: Link to TrackBoundaryDetector for accurate centerline.")]
public TrackBoundaryDetector boundaryDetector; // Now essential
[Tooltip("Layer mask for the drivable track surface (used for final placement raycast).")]
public LayerMask trackLayer;

[Header("Checkpoint Settings")]
[Tooltip("Approximate number of checkpoints to generate around the track.")]
public int targetCheckpointCount = 50;
[Tooltip("Minimum distance between generated checkpoints.")]
public float minCheckpointSpacing = 5.0f;
[Tooltip("Maximum distance between generated checkpoints.")]
public float maxCheckpointSpacing = 25.0f;
[Tooltip("Default width for checkpoint trigger zone (can be refined by boundary data).")]
public float defaultCheckpointWidth = 10.0f;
[Tooltip("Height of the checkpoint trigger zone.")]
public float checkpointTriggerHeight = 5.0f;
[Tooltip("If enabled, checkpoints are shifted towards the inside/outside for a basic racing line.")]
public bool optimizeForRacingLine = true;
[Range(0.1f, 0.5f)]
[Tooltip("How much to shift towards apex (0.1 = slight shift, 0.5 = sharp apex).")]
public float apexFactor = 0.35f;
[Tooltip("Vertical offset when raycasting down to find final checkpoint position.")]
public float checkpointPlacementRaycastHeightOffset = 1.5f; // Renamed for clarity

[Header("Spawn Point Generation")]
[Tooltip("Enable automatic generation of spawn points based on checkpoints.")]
public bool generateSpawnPoints = true;
[Tooltip("How many spawn points to generate, distributed around the track.")]
public int numberOfSpawnPointsToGenerate = 8;
[Tooltip("Vertical offset above the track for generated spawn points.")]
public float spawnPointHeightOffset = 0.2f;
[Tooltip("Minimum distance between automatically generated spawn points.")]
public float minSpawnPointSeparation = 20.0f;

[Header("Debug Visualization")]
public bool showCheckpointsGizmos = true;
public Color checkpointColor = Color.green;
public Color directionColor = Color.yellow;
public bool showCenterlinePoints = true; // Renamed from showCenterlineTrace
public Color centerlineColor = Color.cyan;
[Tooltip("Enable verbose logging during generation.")]
public bool enableDebugLogs = false;
// REMOVED: showInitialScanGrid, showInitialScanPoints (related to old method)
[Tooltip("Show the automatically generated spawn points.")]
public bool showSpawnPointsGizmos = true;


[Header("Manual Control")]
[Tooltip("Click this in the Inspector during Play mode to regenerate checkpoints.")]
public bool regenerateCheckpoints = false;

// --- Public Access ---
public bool IsInitialized { get; private set; } = false;
public List<Checkpoint> Checkpoints { get; private set; } = new List<Checkpoint>();
public List<Transform> GeneratedSpawnPoints { get; private set; } = new List<Transform>();
// IsFullyGenerated checks if Initialization is done AND spawn points are generated (if requested)
public bool IsFullyGenerated => IsInitialized && (!generateSpawnPoints || GeneratedSpawnPoints.Count >= Mathf.Min(1, numberOfSpawnPointsToGenerate)); // Updated check


// --- Private ---
private bool isGenerating = false;
private Coroutine generationCoroutine;
private GameObject checkpointContainer;
private GameObject spawnPointContainer;
// Store calculated centerline for gizmos/debugging
private List<Vector3> generatedCenterlinePoints = new List<Vector3>();

[System.Serializable]
public class Checkpoint
{
    public int index;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 forward;
    public Vector3 right;
    public float width;
    public GameObject visualObject;
    public BoxCollider triggerCollider;
}

// REMOVED: TrackPointInfo class (related to old method)

void Start()
{
    // Basic validation
    if (boundaryDetector == null)
    {
        Debug.LogWarning("[CPG] TrackBoundaryDetector not assigned, attempting to find one...");
        boundaryDetector = FindObjectOfType<TrackBoundaryDetector>();
        if (boundaryDetector == null)
        {
            Debug.LogError("[CPG] REQUIRED TrackBoundaryDetector component not found in scene! Aborting generation.", this);
            enabled = false;
            return;
        }
    }
    if (trackLayer == 0)
    {
        Debug.LogError("[CPG] Track Layer mask is not set! Cannot place checkpoints. Aborting.", this);
         enabled = false;
         return;
    }

    generationCoroutine = StartCoroutine(GenerateCheckpointsAndSpawnsWithDelay());
}

void Update()
{
    if (regenerateCheckpoints && !isGenerating && Application.isPlaying)
    {
        regenerateCheckpoints = false;
        if (generationCoroutine != null) StopCoroutine(generationCoroutine);
        generationCoroutine = StartCoroutine(GenerateCheckpointsAndSpawnsWithDelay());
    }
}

private IEnumerator GenerateCheckpointsAndSpawnsWithDelay()
{
    if (isGenerating) yield break;
    isGenerating = true;
    IsInitialized = false;
    GeneratedSpawnPoints.Clear();
    generatedCenterlinePoints.Clear(); // Clear debug line
    if (enableDebugLogs) Debug.Log("[CPG] Starting generation...");
    yield return null; // Wait a frame

    // --- Wait for Boundary Detector ---
    if (!boundaryDetector.HasScanned)
    {
         Debug.Log("[CPG] Waiting for TrackBoundaryDetector scan to complete...");
         // REMOVED check for boundaryDetector.scanCoroutine - Was inaccessible and unnecessary
    }
    float waitStartTime = Time.time;
    while (!boundaryDetector.HasScanned)
    {
        if (Time.time - waitStartTime > 300f) // Timeout after 5 minutes
        {
             Debug.LogError("[CPG] Timed out waiting for TrackBoundaryDetector scan! Aborting.", this);
             isGenerating = false;
             yield break;
        }
        yield return new WaitForSeconds(0.5f); // Check every half second
    }
    if (enableDebugLogs) Debug.Log("[CPG] TrackBoundaryDetector scan complete.");


    ClearGeneratedObjects();

    // --- Generate Centerline from Boundaries ---
    List<Vector3> centerlinePoints = new List<Vector3>();
    List<Quaternion> centerlineRotations = new List<Quaternion>();
    if (!GenerateCenterlineFromBoundaries(centerlinePoints, centerlineRotations))
    {
        Debug.LogError("[CPG] Failed to generate centerline from boundaries! Aborting.");
        isGenerating = false;
        yield break;
    }
    if (enableDebugLogs) Debug.Log($"[CPG] Generated centerline using boundaries: {centerlinePoints.Count} points.");
    generatedCenterlinePoints = new List<Vector3>(centerlinePoints); // Store for gizmos

    // --- Distribute Checkpoints along the new centerline ---
    DistributeCheckpoints(centerlinePoints, centerlineRotations);
    if (enableDebugLogs) Debug.Log($"[CPG] Distributed checkpoints: {Checkpoints.Count}");

    if (Checkpoints.Count < 2)
    {
         Debug.LogError($"[CPG] Failed to distribute sufficient checkpoints ({Checkpoints.Count})! Check boundary data and checkpoint settings. Aborting.", this);
         isGenerating = false;
         yield break;
    }

    // Optional: Apply racing line optimization (uses checkpoint data)
    if (optimizeForRacingLine && Checkpoints.Count > 5)
    {
        OptimizeForRacingLine(); // This function might need tweaks if width calculation changes
        if (enableDebugLogs) Debug.Log("[CPG] Applied racing line optimization.");
    }

    // Create the physical checkpoint trigger objects
    CreateCheckpointObjects();
    if (enableDebugLogs) Debug.Log("[CPG] Created checkpoint GameObjects.");

    // --- Generate Spawn Points (if enabled) ---
    if (generateSpawnPoints && Checkpoints.Count >= 1) // Need at least one CP for spawn
    {
        // Ensure we don't request more spawns than checkpoints exist
        int actualSpawnsToGen = Mathf.Min(numberOfSpawnPointsToGenerate, Checkpoints.Count);
         if (actualSpawnsToGen < numberOfSpawnPointsToGenerate)
         {
             Debug.LogWarning($"[CPG] Requested {numberOfSpawnPointsToGenerate} spawn points, but only {Checkpoints.Count} checkpoints exist. Generating {actualSpawnsToGen} instead.");
         }

         if (actualSpawnsToGen > 0)
         {
            GenerateSpawnPointsFromCheckpoints(actualSpawnsToGen); // Pass the actual count
            if (enableDebugLogs) Debug.Log($"[CPG] Generated {GeneratedSpawnPoints.Count} spawn points.");
         }
         else {
             if (enableDebugLogs) Debug.Log("[CPG] Not enough checkpoints to generate any spawn points.");
         }

    }
    else if (generateSpawnPoints)
    {
        Debug.LogWarning($"[CPG] Cannot generate spawn points as no checkpoints were created. Skipping spawn point generation.");
    }

    // --- Finish ---
    IsInitialized = true; // Mark as initialized after CPs are done
    isGenerating = false;
    Debug.Log($"[CPG] Generation complete. {Checkpoints.Count} checkpoints ready. {(generateSpawnPoints ? GeneratedSpawnPoints.Count : 0)} spawn points generated.");
    generationCoroutine = null;
}

/// <summary>
/// Generates centerline points and rotations using the inner/outer boundaries
/// provided by the TrackBoundaryDetector.
/// </summary>
private bool GenerateCenterlineFromBoundaries(List<Vector3> outCenterlinePoints, List<Quaternion> outCenterlineRotations)
{
    outCenterlinePoints.Clear();
    outCenterlineRotations.Clear();

    List<Vector3> inner = boundaryDetector.InnerBoundaryPoints;
    List<Vector3> outer = boundaryDetector.OuterBoundaryPoints;

    if (inner == null || outer == null || inner.Count < 3 || outer.Count < 3)
    {
        Debug.LogError($"[CPG] Invalid boundary data from TrackBoundaryDetector. Inner: {(inner?.Count ?? -1)}, Outer: {(outer?.Count ?? -1)}. Cannot generate centerline.");
        return false;
    }

    // Use the longer boundary as the "driver" for iteration
    List<Vector3> driverPoints = (inner.Count >= outer.Count) ? inner : outer;
    List<Vector3> otherPoints = (inner.Count < outer.Count) ? inner : outer;

    if (enableDebugLogs) Debug.Log($"[CPG] Using {(driverPoints == inner ? "Inner" : "Outer")} boundary ({driverPoints.Count} points) as driver.");

    List<Vector3> calculatedPoints = new List<Vector3>();
    List<Vector3> calculatedNormals = new List<Vector3>();

    // --- Pass 1: Calculate Midpoints and Find Track Surface ---
    for (int i = 0; i < driverPoints.Count; i++)
    {
        Vector3 driverPoint = driverPoints[i];
        Vector3 closestOtherPoint = FindClosestVertex(driverPoint, otherPoints); // Find closest VERTEX on the other boundary

        Vector3 midPointEstimate = (driverPoint + closestOtherPoint) / 2.0f;

        // Raycast down to find the actual track surface point and normal
        Vector3 rayStart = midPointEstimate + Vector3.up * checkpointPlacementRaycastHeightOffset;
        float rayDist = checkpointPlacementRaycastHeightOffset * 2.0f; // Search down
        RaycastHit hit;

        if (Physics.Raycast(rayStart, Vector3.down, out hit, rayDist, trackLayer))
        {
            calculatedPoints.Add(hit.point);
            calculatedNormals.Add(hit.normal);
             // Debug Draw (Optional)
            // if (showCenterlinePoints) Debug.DrawRay(rayStart, Vector3.down * hit.distance, Color.green, 15f);
        }
        else
        {
            // Fallback: Use midpoint estimate and assume flat up normal
            // This might happen over gaps or if raycast settings are wrong
            calculatedPoints.Add(midPointEstimate);
            calculatedNormals.Add(Vector3.up);
            if (enableDebugLogs) Debug.LogWarning($"[CPG] Centerline raycast MISS at index {i}. Using estimated midpoint/normal.");
             // Debug Draw (Optional)
            // if (showCenterlinePoints) Debug.DrawRay(rayStart, Vector3.down * rayDist, Color.red, 15f);
        }
    }

    if (calculatedPoints.Count < 2)
    {
         Debug.LogError("[CPG] Less than 2 centerline points generated from boundaries. Cannot proceed.");
         return false;
    }


    // --- Pass 2: Calculate Rotations ---
    for (int i = 0; i < calculatedPoints.Count; i++)
    {
        Vector3 currentPoint = calculatedPoints[i];
        Vector3 nextPoint = calculatedPoints[(i + 1) % calculatedPoints.Count]; // Loop back for the last point
        Vector3 normal = calculatedNormals[i];

        Vector3 forwardDir = (nextPoint - currentPoint).normalized;

        // Handle cases where points might be identical (though unlikely with boundary method)
        if (forwardDir.sqrMagnitude < 0.001f)
        {
            if (i > 0) // Use previous forward direction if available
            {
                 forwardDir = (calculatedPoints[i] - calculatedPoints[i-1]).normalized;
            }
             else // Fallback for the very first point if it coincides with the second
             {
                 forwardDir = Vector3.forward; // Arbitrary fallback
                 if(enableDebugLogs) Debug.LogWarning($"[CPG] Centerline points {i} and {(i + 1) % calculatedPoints.Count} are too close. Using arbitrary forward.");
             }

             // Still need to handle zero vector case again
             if (forwardDir.sqrMagnitude < 0.001f) forwardDir = Vector3.forward;
        }

        // Ensure forward is orthogonal to normal (important for LookRotation)
        Vector3 rightDir = Vector3.Cross(normal, forwardDir).normalized;
        forwardDir = Vector3.Cross(rightDir, normal).normalized; // Recompute forward based on normal and right

        Quaternion rotation = Quaternion.LookRotation(forwardDir, normal);
        outCenterlinePoints.Add(currentPoint);
        outCenterlineRotations.Add(rotation);
    }

    return outCenterlinePoints.Count >= 2;
}

/// <summary>
/// Finds the vertex in the 'vertices' list that is closest to the 'point'.
/// </summary>
private Vector3 FindClosestVertex(Vector3 point, List<Vector3> vertices)
{
    Vector3 closest = Vector3.positiveInfinity;
    float minDistSq = float.MaxValue;

    foreach(Vector3 vertex in vertices)
    {
        float distSq = (vertex - point).sqrMagnitude;
        if (distSq < minDistSq)
        {
            minDistSq = distSq;
            closest = vertex;
        }
    }
    return closest; // Returns positiveInfinity if vertices list is empty
}


// REMOVED: FindInitialTrackPointRobust()
// REMOVED: TraceTrackCenterline()
// REMOVED: FindNextCenterlinePoint()


private float MeasureTrackWidth(Vector3 point, Quaternion rotation)
{
    float measuredWidth = 0f; // Use a different name initially to avoid scope conflict with the return value if needed, though direct assignment is fine
    bool widthFromBoundaries = false;

    // Use Boundary Detector for width if available and seems reasonable
    if (boundaryDetector != null && boundaryDetector.HasScanned)
    {
        Vector3 i, o;
        // Search slightly further than default width just in case
        if (boundaryDetector.GetClosestBoundaryPoints(point, out i, out o, defaultCheckpointWidth * 1.5f))
        {
            // *** FIXED: Assign to existing variable, don't redeclare ***
            measuredWidth = Vector3.Distance(i, o);
            if (measuredWidth > 1.0f) // Basic sanity check for width
            {
                widthFromBoundaries = true; // Mark that we got a good value
                // return measuredWidth; // Can return early if preferred
            }
            else
            {
                if (enableDebugLogs) Debug.LogWarning($"[CPG] Measured width near {point} from boundaries was too small ({measuredWidth:F1}m). Falling back to raycast.");
                // Reset measuredWidth if it was invalid from boundaries, so fallback below uses 0 start
                measuredWidth = 0f;
            }
        }
         else
         {
             // Optional: Log when boundary method fails
             // if (enableDebugLogs) Debug.Log($"[CPG] BoundaryDetector failed GetClosestBoundaryPoints near {point}. Falling back to raycast width.");
             measuredWidth = 0f; // Ensure it's 0 if boundaries fail
         }
    }

    // Fallback or if boundary width wasn't used: Simple sideways raycast
    if (!widthFromBoundaries)
    {
        Vector3 right = rotation * Vector3.right;
        // We reset measuredWidth above if boundary failed, so hits can add to it
        int hits = 0;
        RaycastHit h;
        Vector3 rayOrigin = point + Vector3.up * 0.3f;
        LayerMask edgeMask = boundaryDetector?.boundaryWallLayer ?? 0;
        if (edgeMask == 0) edgeMask = ~trackLayer;
        else edgeMask |= ~trackLayer;

        float rayLength = defaultCheckpointWidth * 1.5f;

        if (Physics.Raycast(rayOrigin, -right, out h, rayLength, edgeMask))
        {
            measuredWidth += h.distance;
            hits++;
        }
        if (Physics.Raycast(rayOrigin, right, out h, rayLength, edgeMask))
        {
            measuredWidth += h.distance;
            hits++;
        }

        if (hits == 2) return Mathf.Max(measuredWidth, 1.0f); // Found both sides
        if (hits == 1) return Mathf.Max(measuredWidth * 1.8f, defaultCheckpointWidth * 0.75f); // Found one side, estimate other
        return defaultCheckpointWidth; // Found neither, use default
    }
    else
    {
         // If we got here, widthFromBoundaries was true and valid
         return Mathf.Max(measuredWidth, 1.0f); // Ensure minimum width of 1
    }
}


// --- Distribute Checkpoints, AddCheckpointToList, RecalculateCheckpointOrientationsAndWidths ---
// These methods should work correctly with the generated centerlinePoints and centerlineRotations
// Minor Tweak: Ensure Recalculate uses the potentially more accurate width from MeasureTrackWidth
// which now prefers boundary data.

private void DistributeCheckpoints(List<Vector3> centerlinePoints, List<Quaternion> centerlineRotations)
{
    Checkpoints.Clear();
    if (centerlinePoints.Count < 2) return;

    float totalLength = 0;
    for (int i = 0; i < centerlinePoints.Count - 1; i++)
    {
        totalLength += Vector3.Distance(centerlinePoints[i], centerlinePoints[i + 1]);
    }
    // Add distance from last to first for loop closure
    totalLength += Vector3.Distance(centerlinePoints[centerlinePoints.Count - 1], centerlinePoints[0]);


    if (totalLength < minCheckpointSpacing * 2)
    {
         Debug.LogWarning($"[CPG] Calculated centerline length ({totalLength:F1}m) is very short. Clamping checkpoint count.");
         // Avoid dividing by zero or tiny numbers if track is minuscule
         if (targetCheckpointCount <= 0) targetCheckpointCount = 10; // Ensure positive target
         if (minCheckpointSpacing <= 0) minCheckpointSpacing = 1f;
         if (maxCheckpointSpacing <= minCheckpointSpacing) maxCheckpointSpacing = minCheckpointSpacing * 2f;
    }

    float desiredSpacing = Mathf.Clamp(totalLength / Mathf.Max(1, targetCheckpointCount), minCheckpointSpacing, maxCheckpointSpacing);
    int actualCheckpointCount = Mathf.Max(2, Mathf.FloorToInt(totalLength / desiredSpacing));
    if (enableDebugLogs) Debug.Log($"[CPG] Centerline Length: {totalLength:F1}m, Desired Spacing: {desiredSpacing:F1}m, Target/Actual CPs: {targetCheckpointCount}/{actualCheckpointCount}");

    float distanceAccumulatedOnCenterline = 0f;
    float nextCheckpointTargetDist = 0f; // Start placement relative to the beginning
    int centerlineIndex = 0;

    // Add the first checkpoint manually at the start of the centerline
    AddCheckpointToList(0, centerlinePoints[0], centerlineRotations[0]);
    nextCheckpointTargetDist += desiredSpacing;

    // Iterate through centerline segments to place remaining checkpoints
    while (Checkpoints.Count < actualCheckpointCount && centerlineIndex < centerlinePoints.Count)
    {
        // Handle loop segment (last point connecting back to first)
        int nextCenterlineIndex = (centerlineIndex + 1) % centerlinePoints.Count;
        Vector3 start = centerlinePoints[centerlineIndex];
        Vector3 end = centerlinePoints[nextCenterlineIndex];
        float segmentLength = Vector3.Distance(start, end);

        if (segmentLength < 0.01f) // Skip zero-length segments
        {
            centerlineIndex = nextCenterlineIndex; // Move to next index
             if(centerlineIndex == 0) break; // Completed loop potentially stuck on zero segment
            continue;
        }

        // Place checkpoints that fall within this segment
        while (distanceAccumulatedOnCenterline + segmentLength >= nextCheckpointTargetDist)
        {
            float distanceNeeded = nextCheckpointTargetDist - distanceAccumulatedOnCenterline;
            float interpolationFactor = Mathf.Clamp01(distanceNeeded / segmentLength);

            Vector3 cpPos = Vector3.Lerp(start, end, interpolationFactor);
            // Interpolate rotation using Slerp for smoother rotation changes
            Quaternion cpRot = Quaternion.Slerp(centerlineRotations[centerlineIndex], centerlineRotations[nextCenterlineIndex], interpolationFactor);

            AddCheckpointToList(Checkpoints.Count, cpPos, cpRot);
            nextCheckpointTargetDist += desiredSpacing;

            if (Checkpoints.Count >= actualCheckpointCount) break; // Exit if we've placed enough
        }

        distanceAccumulatedOnCenterline += segmentLength;
        centerlineIndex = nextCenterlineIndex; // Move to the next segment start point

        // Break if we somehow looped back to start without finishing
        // (shouldn't happen with while condition, but safety check)
        if (centerlineIndex == 0 && Checkpoints.Count < actualCheckpointCount)
        {
            if(enableDebugLogs) Debug.LogWarning($"[CPG] Completed centerline loop but placed only {Checkpoints.Count}/{actualCheckpointCount} CPs. Stopping.");
            break;
        }

    } // End while loop through centerline

    if (Checkpoints.Count < actualCheckpointCount && enableDebugLogs)
    {
        Debug.LogWarning($"[CPG] Placed {Checkpoints.Count}/{actualCheckpointCount} checkpoints. Centerline might be shorter than expected or spacing parameters restrictive.");
    }

    // Final pass to refine orientations and measure widths
    RecalculateCheckpointOrientationsAndWidths();
}

private void AddCheckpointToList(int index, Vector3 position, Quaternion rotation)
{
    // No change needed here
    Checkpoints.Add(new Checkpoint { index = index, position = position, rotation = rotation });
}

private void RecalculateCheckpointOrientationsAndWidths()
{
    // This function primarily recalculates forward/right vectors and measures width.
    // It should still work, but the width measurement part now uses the improved MeasureTrackWidth.
    if (Checkpoints.Count < 1) return;

    for (int i = 0; i < Checkpoints.Count; i++)
    {
        Checkpoint current = Checkpoints[i];
        Checkpoint next = Checkpoints[(i + 1) % Checkpoints.Count];
        Checkpoint prev = Checkpoints[(i - 1 + Checkpoints.Count) % Checkpoints.Count]; // Wraps around correctly

        // Calculate forward direction based on neighbours
        Vector3 dirToNext = (next.position - current.position).normalized;
        Vector3 dirFromPrev = (current.position - prev.position).normalized;

        bool dirToNextValid = dirToNext.sqrMagnitude > 0.001f;
        bool dirFromPrevValid = dirFromPrev.sqrMagnitude > 0.001f;

        if (dirToNextValid && dirFromPrevValid)
        {
            // Average directions for smoother orientation at turns
            current.forward = (dirToNext + dirFromPrev).normalized;
            // If vectors nearly cancel out (sharp 180 turn?), default to direction to next
            if (current.forward.sqrMagnitude < 0.001f)
            {
                current.forward = dirToNext;
            }
        }
        else if (dirToNextValid) // Use next if prev is invalid (e.g., first point)
        {
            current.forward = dirToNext;
        }
        else if (dirFromPrevValid) // Use prev if next is invalid (e.g., last point if not looped)
        {
            current.forward = dirFromPrev;
        }
        else // Fallback if both neighbours are colocated (should be rare)
        {
             // Use original rotation's forward if available, else global forward
            current.forward = (current.rotation * Vector3.forward).normalized;
             if (current.forward.sqrMagnitude < 0.001f) current.forward = Vector3.forward;

            if (enableDebugLogs) Debug.LogWarning($"[CPG] Could not determine valid forward direction for CP {i} from neighbors. Using fallback.");
        }

        // Recalculate rotation based on the refined forward direction (assuming world up)
        // We could potentially use the stored surface normal here if needed, but world up is often fine for checkpoints
        current.rotation = Quaternion.LookRotation(current.forward, Vector3.up);
        current.right = current.rotation * Vector3.right; // Recalculate right vector

        // Measure width using the potentially boundary-assisted method
        current.width = MeasureTrackWidth(current.position, current.rotation);
    }
}

// --- OptimizeForRacingLine ---
// This function should still work as it uses the checkpoint positions and widths.
// Make sure the raycast down uses the trackLayer and appropriate height offset.
 private void OptimizeForRacingLine()
 {
     if (Checkpoints.Count < 3) return;
     List<Vector3> originalPositions = Checkpoints.Select(cp => cp.position).ToList();

     for (int i = 0; i < Checkpoints.Count; i++)
     {
         // Get original positions of previous, current, and next checkpoints
         Vector3 pP = originalPositions[(i - 1 + Checkpoints.Count) % Checkpoints.Count];
         Vector3 cP = originalPositions[i];
         Vector3 nP = originalPositions[(i + 1) % Checkpoints.Count];

         Vector3 pD = (cP - pP).normalized; // Direction from previous to current
         Vector3 nD = (nP - cP).normalized; // Direction from current to next

         // Check for valid directions
         if (pD.sqrMagnitude < 0.001f || nD.sqrMagnitude < 0.001f) continue;

         // Calculate the turn angle (unsigned) and sign (which way it turns)
         float angle = Vector3.Angle(pD, nD);
         // Use SignedAngle around World Up axis to determine left/right turn
         float turnSign = Mathf.Sign(Vector3.SignedAngle(pD, nD, Vector3.up));

         // Only shift significantly for actual turns (e.g., > 15 degrees)
         if (angle > 15.0f)
         {
             Checkpoint currentCP = Checkpoints[i];
             float width = currentCP.width; // Use the measured width

             // Calculate shift magnitude based on angle and width (more angle/width = more shift)
             // apexFactor controls how much of the available half-width is used
             float shiftMagnitude = Mathf.Sin(angle * Mathf.Deg2Rad * 0.5f) * (width * 0.5f) * apexFactor;

             // Calculate the direction perpendicular to the average direction, pointing towards the inside of the turn
             Vector3 averageDirection = (pD + nD).normalized;
             // Rotate average direction 90 degrees inwards (using -turnSign)
             Vector3 shiftDirection = Quaternion.AngleAxis(90f * -turnSign, Vector3.up) * averageDirection;

             // Calculate the potential new position
             Vector3 shiftedPosAttempt = currentCP.position + shiftDirection.normalized * shiftMagnitude;

             // Raycast down from the shifted position to find the actual track surface
             RaycastHit hit;
             Vector3 rayStart = shiftedPosAttempt + Vector3.up * checkpointPlacementRaycastHeightOffset;
             float rayDist = checkpointPlacementRaycastHeightOffset * 2f;

             if (Physics.Raycast(rayStart, Vector3.down, out hit, rayDist, trackLayer))
             {
                 // Successfully found track surface at shifted position
                 currentCP.position = hit.point;
             }
             else
             {
                 // Fallback: Try shifting half the distance if full shift misses track
                 shiftedPosAttempt = currentCP.position + shiftDirection.normalized * shiftMagnitude * 0.5f;
                 rayStart = shiftedPosAttempt + Vector3.up * checkpointPlacementRaycastHeightOffset;
                 if (Physics.Raycast(rayStart, Vector3.down, out hit, rayDist, trackLayer))
                 {
                     currentCP.position = hit.point;
                 }
                 // Else: If even half shift fails, leave the checkpoint at its original centerline position
             }
         }
     }
     // After shifting positions, recalculate orientations and potentially widths again
     RecalculateCheckpointOrientationsAndWidths();
 }

// --- CreateCheckpointObjects ---
// No change needed here, uses the final Checkpoints list data.
private void CreateCheckpointObjects() {
    if (checkpointContainer != null) Destroy(checkpointContainer);
    checkpointContainer = new GameObject("CheckpointTriggers");
    checkpointContainer.transform.SetParent(this.transform);

    for (int i = 0; i < Checkpoints.Count; i++)
    {
        Checkpoint cp = Checkpoints[i];
        GameObject cpObj = new GameObject($"Checkpoint_{i}");
        // Optional: Set layer for easier physics queries if needed, though agent uses index primarily
         // cpObj.layer = LayerMask.NameToLayer("Checkpoints"); // Example layer
        cpObj.transform.SetParent(checkpointContainer.transform);
        cpObj.transform.position = cp.position;
        cpObj.transform.rotation = cp.rotation; // Use calculated rotation

        BoxCollider trig = cpObj.AddComponent<BoxCollider>();
        trig.isTrigger = true;
        // Size relative to checkpoint's forward/right
        trig.size = new Vector3(cp.width, checkpointTriggerHeight, 1.0f); // X = width, Y = height, Z = depth
        // Center the collider vertically
        trig.center = Vector3.up * (checkpointTriggerHeight / 2f);

        cp.triggerCollider = trig;
        cp.visualObject = cpObj;
    }
}

// --- GenerateSpawnPointsFromCheckpoints ---
// Minor update to accept the number of points to actually generate
private void GenerateSpawnPointsFromCheckpoints(int count) // Accept count
{
    ClearGeneratedSpawnPoints(); // Clear previous spawns if any
    GeneratedSpawnPoints.Clear();

    if (Checkpoints.Count < 1 || count <= 0) return; // Need at least 1 CP

    spawnPointContainer = new GameObject("GeneratedSpawnPoints");
    spawnPointContainer.transform.SetParent(this.transform);

    float interval = (Checkpoints.Count > 1 && count > 1) ? (float)Checkpoints.Count / count : 0; // Avoid division by zero if count is 1

    int addedSpawnCount = 0;
    float lastSpawnPointDistance = -minSpawnPointSeparation * 2f; // Ensure first point is always added

    for (int i = 0; i < count; i++)
    {
        int checkpointIndex = Mathf.FloorToInt(i * interval) % Checkpoints.Count; // Wrap around if needed
        Checkpoint cp = Checkpoints[checkpointIndex];

        // Calculate distance along the track from the start (approximate)
        // This is complex to do accurately. A simpler check is distance to LAST added spawn point.
        bool skip = false;
        if (GeneratedSpawnPoints.Count > 0)
        {
            float distToLast = Vector3.Distance(cp.position, GeneratedSpawnPoints.Last().position);
            if (distToLast < minSpawnPointSeparation)
            {
                if (enableDebugLogs) Debug.Log($"[CPG] Skipping potential spawn point near CP {checkpointIndex} due to proximity ({distToLast:F1}m) to previous spawn.");
                skip = true;
                // Try the next checkpoint index to see if it's further away
                int nextCpIndex = (checkpointIndex + 1) % Checkpoints.Count;
                Checkpoint nextCp = Checkpoints[nextCpIndex];
                float distToNextPotential = Vector3.Distance(nextCp.position, GeneratedSpawnPoints.Last().position);
                if(distToNextPotential >= minSpawnPointSeparation)
                {
                     if (enableDebugLogs) Debug.Log($"[CPG] Trying next CP {nextCpIndex} instead.");
                     cp = nextCp; // Use the next checkpoint instead
                     skip = false; // Don't skip this one
                     // We might technically place spawn points out of perfect interval order, but separation is more important.
                }
            }
        }

        if (skip) continue; // Skip if too close and couldn't find alternative

        GameObject spObj = new GameObject($"SpawnPoint_Generated_{addedSpawnCount}");
        spObj.transform.SetParent(spawnPointContainer.transform);

        // Position slightly above track using checkpoint position and rotation
        spObj.transform.position = cp.position + cp.rotation * Vector3.up * spawnPointHeightOffset; // Use checkpoint rotation for UP direction relative to track
        spObj.transform.rotation = cp.rotation; // Align with checkpoint forward direction

        GeneratedSpawnPoints.Add(spObj.transform);
        addedSpawnCount++;
        lastSpawnPointDistance = 0f; // Reset distance tracking (simplification)

         if(addedSpawnCount >= count) break; // Stop if we've added enough
    }

    // Log if constraints prevented generating the target number
    if (addedSpawnCount < count && enableDebugLogs)
    {
        Debug.LogWarning($"[CPG] Generated only {addedSpawnCount}/{count} spawn points due to minimum separation constraints ({minSpawnPointSeparation}m).");
    }
}


// --- Helper Methods (Clearing, Getting Checkpoints) ---

private void ClearGeneratedSpawnPoints()
{
    if (spawnPointContainer != null)
    {
        if (Application.isPlaying) Destroy(spawnPointContainer); else DestroyImmediate(spawnPointContainer);
    }
    GeneratedSpawnPoints.Clear(); // Clear the list too
}

private void ClearGeneratedObjects()
{
    ClearCheckpoints(); // Clears CP container and list
    ClearGeneratedSpawnPoints(); // Clears SP container and list
}

public void ClearCheckpoints()
{
    if (checkpointContainer != null)
    {
        if (Application.isPlaying) Destroy(checkpointContainer); else DestroyImmediate(checkpointContainer);
    }
    Checkpoints.Clear();
    IsInitialized = false; // Mark as uninitialized when cleared
}

// GetCheckpoint, GetNextCheckpoint, GetPreviousCheckpoint, GetNearestCheckpoint, GetUpcomingCheckpoints, GetAllCheckpointsList
// These methods do not need changes. They rely on the final Checkpoints list.
 public Checkpoint GetCheckpoint(int index) { if (!IsInitialized || index < 0 || index >= Checkpoints.Count) return null; return Checkpoints[index]; }
 public Checkpoint GetNextCheckpoint(int currentIndex) { if (!IsInitialized || Checkpoints.Count == 0) return null; return Checkpoints[(currentIndex + 1) % Checkpoints.Count]; }
 public Checkpoint GetPreviousCheckpoint(int currentIndex) { if (!IsInitialized || Checkpoints.Count == 0) return null; return Checkpoints[(currentIndex - 1 + Checkpoints.Count) % Checkpoints.Count]; }
 public Checkpoint GetNearestCheckpoint(Vector3 position, out int index) { index = -1; if (!IsInitialized || Checkpoints.Count == 0) return null; float minSqrDist = float.MaxValue; Checkpoint nearest = null; for (int i = 0; i < Checkpoints.Count; i++) { float sqrDist = (Checkpoints[i].position - position).sqrMagnitude; if (sqrDist < minSqrDist) { minSqrDist = sqrDist; nearest = Checkpoints[i]; index = i; } } return nearest; }
 public List<Checkpoint> GetUpcomingCheckpoints(int currentIndex, int count) { List<Checkpoint> upcoming = new List<Checkpoint>(); if (!IsInitialized || Checkpoints.Count == 0) return upcoming; for (int i = 1; i <= count; i++) { int idx = (currentIndex + i) % Checkpoints.Count; if (idx >= 0 && idx < Checkpoints.Count) upcoming.Add(Checkpoints[idx]); } return upcoming; }
 public List<Checkpoint> GetAllCheckpointsList() { return new List<Checkpoint>(Checkpoints); }
 // REMOVED: GetSingleLayerFromMask (not used anymore here)

// --- Gizmos ---
private void OnDrawGizmosSelected() // Keep as Selected to avoid clutter unless object is selected
{
    // Draw Generated Centerline Points (if enabled)
    if (showCenterlinePoints && generatedCenterlinePoints != null && generatedCenterlinePoints.Count > 1)
    {
        Gizmos.color = centerlineColor;
        for (int i = 0; i < generatedCenterlinePoints.Count - 1; i++)
        {
            Gizmos.DrawLine(generatedCenterlinePoints[i] + Vector3.up * 0.1f, generatedCenterlinePoints[i + 1] + Vector3.up * 0.1f);
        }
        // Draw closing segment
        Gizmos.DrawLine(generatedCenterlinePoints[generatedCenterlinePoints.Count - 1] + Vector3.up * 0.1f, generatedCenterlinePoints[0] + Vector3.up * 0.1f);
    }


    // Draw Checkpoints (No changes needed)
    if (showCheckpointsGizmos && Checkpoints != null && Checkpoints.Count > 0)
    {
        for (int i = 0; i < Checkpoints.Count; i++)
        {
            Checkpoint cp = Checkpoints[i]; if (cp == null) continue;
            Gizmos.color = checkpointColor; Gizmos.DrawWireSphere(cp.position, 0.5f);
            Gizmos.color = directionColor; Gizmos.DrawLine(cp.position, cp.position + cp.forward * 3f);
            Gizmos.color = Color.white; Vector3 r = cp.position + cp.right * (cp.width / 2f), l = cp.position - cp.right * (cp.width / 2f); Gizmos.DrawLine(l, r);
            Gizmos.color = Color.gray; Checkpoint nextCp = Checkpoints[(i + 1) % Checkpoints.Count]; if (nextCp != null) Gizmos.DrawLine(cp.position, nextCp.position);
            if (cp.triggerCollider != null && cp.visualObject != null) { Gizmos.color = new Color(checkpointColor.r, checkpointColor.g, checkpointColor.b, 0.3f); Gizmos.matrix = cp.visualObject.transform.localToWorldMatrix; Gizmos.DrawWireCube(cp.triggerCollider.center, cp.triggerCollider.size); Gizmos.matrix = Matrix4x4.identity; }
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(cp.position + Vector3.up * 1.5f, $"CP {cp.index} W:{cp.width:F1}");
            #endif
        }
    }

    // Draw Spawn Points (No changes needed)
    if (showSpawnPointsGizmos && GeneratedSpawnPoints != null && GeneratedSpawnPoints.Count > 0)
    {
        Gizmos.color = Color.blue;
        foreach (Transform sp in GeneratedSpawnPoints)
        {
            if (sp == null) continue;
            Gizmos.DrawWireSphere(sp.position, 0.75f);
            Gizmos.DrawLine(sp.position, sp.position + sp.forward * 2f);
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(sp.position + Vector3.up * 1.0f, sp.name);
            #endif
        }
    }
}}