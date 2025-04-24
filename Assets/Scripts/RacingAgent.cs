// --- START OF FILE RacingAgent.cs ---

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Linq;

/// <summary>
/// ML-Agents agent tuned for ELITE, clean, and fast racing performance.
/// V17.1 - Optimized Robust Recovery & Performance (Formatting Fix)
/// Focus: Combines State-Based Recovery Prioritization with aggressive Anti-Reward Hacking penalties.
/// Off-track behavior is dominated by penalties and clear guidance back to track, ignoring checkpoints.
/// On-track behavior focuses on speed, smooth driving, and correct checkpoint order.
/// </summary>
[RequireComponent(typeof(VehicleController))]
[RequireComponent(typeof(InputManager))]
[RequireComponent(typeof(Rigidbody))]
public class RacingAgent : Agent
{
    // --- Inspector Parameters ---
    [Header("Agent Core Components")]
    public InputManager inputManager;
    public VehicleController vehicleController;
    private Rigidbody rb;

    [Header("Track & Environment References")]
    public TrackBoundaryDetector boundaryDetector;
    public CheckpointGenerator checkpointGenerator;
    public LayerMask trackLayer; // Essential for detecting when back on track
    public LayerMask boundaryWallLayer;

    [Header("Observation Settings")]
    [Tooltip("Number of rays used for environment sensing.")]
    public int numRaycasts = 40;
    [Tooltip("Maximum distance the rays can detect.")]
    public float raycastDistance = 120f;
    [Tooltip("Total angle covered by the raycasts.")]
    public float raycastAngle = 240f;
    [Tooltip("How many future checkpoints to observe (when on track).")]
    public int upcomingCheckpointsToTrack = 8;
    [Tooltip("Vertical offset of raycast origin from agent center.")]
    public float raycastVerticalOffset = 0.4f;
    [Tooltip("Layers the raycasts should interact with.")]
    public LayerMask raycastMask;
    [Tooltip("Observe the forward/right direction of upcoming checkpoints (on track).")]
    public bool observeCheckpointOrientation = true;
    [Tooltip("Observe the angle of the upcoming track segment relative to the current one (on track).")]
    public bool observeUpcomingTurnAngle = true;
    [Tooltip("Observe the agent's angle relative to the nearest track centerline segment.")]
    public bool observeAngleToTrack = true;
    [Tooltip("Observe the track curvature ahead of the agent (using N points). 0 = No.")]
    public int observeTrackCurvatureAhead = 3;
    [Tooltip("Observe the track curvature behind the agent (using N points). 0 = No.")]
    public int observeTrackCurvatureBehind = 2;
    [Tooltip("Observe how long the agent has been 'stuck' (off-track + low speed).")]
    public bool observeTimeStuck = true;
    [Tooltip("Observe direction and distance to the *nearest point on the track centerline*.")]
    public bool observeNearestTrackPoint = true; // *** KEEP TRUE ***

    [Header("Action Settings")]
    public float steerMultiplier = 1.0f;
    public float accelMultiplier = 1.0f;
    public float brakeMultiplier = 1.0f;

    // --- REWARDS ---
    [Header("Reward Settings (Optimized - Anti-Hacking / Robust Recovery)")]
    // --- On-Track Rewards (Anti-Circle Behavior) ---
    [Header("On-Track Rewards (Anti-Circle Behavior)")]
    public float checkpointReward = 5.0f; // Significantly increased to make checkpoints the primary reward source
    public float lapCompletionReward = 20.0f; // Significantly increased to make lap completion the ultimate goal
    public float speedRewardFactor = 0.01f; // Drastically reduced to prevent speed-only exploitation
    public float velocityProgressRewardFactor = 2.0f; // Doubled to strongly reward progress toward next checkpoint
    public float timePenalty = -0.01f; // Increased time pressure to discourage stalling
    public float centerlineRewardFactor = 0.02f; // Further reduced to minimize centerline exploitation
    public float centerlineRewardZoneWidth = 3.0f;
    public float beatBestLapBonus = 100.0f; // Doubled to strongly encourage faster laps
    public float steeringSmoothnessPenaltyFactor = -0.01f; // Increased to discourage erratic steering
    public float spinPenaltyFactor = -0.05f; // Significantly increased to strongly discourage spinning
    public float wrongCheckpointPenalty = -10.0f; // Doubled to strongly discourage wrong checkpoint order
    public float circlingPenaltyFactor = -0.5f; // New penalty for circling behavior
    public float boundaryHitPenalty = -5.0f; // Always apply
    public float resetPenalty = -2.0f; // On reset

    // --- Off-Track / Recovery (Recovery Focused) ---
    [Header("Off-Track / Recovery (Recovery Focused)")]
    [Tooltip("Continuous penalty per second while off-track.")]
    public float continuousOffTrackPenalty = -2.0f; // Reduced to prevent agent from freezing
    [Tooltip("One-time bonus for successfully returning to the track.")]
    public float recoveryReward = 10.0f; // Significantly increased to strongly reward getting back on track
    [Tooltip("Reward for reducing distance to the nearest track boundary *while off-track*.")]
    public float distanceReductionRewardFactor = 1.0f; // Doubled to strongly encourage moving toward track
    [Tooltip("Penalty multiplier for increasing distance to the nearest track boundary *while off-track*.")]
    public float distanceIncreasePenaltyFactor = -0.3f; // Reduced to prevent agent from freezing
    [Tooltip("Reward multiplier for aligning with the track direction *while off-track*.")]
    public float offTrackOrientationRewardFactor = 0.5f; // Increased to strongly encourage proper alignment
    [Tooltip("Time (seconds) before 'stuck' penalty applies (Off-track AND slow).")]
    public float timeUntilStuckThreshold = 1.0f; // Further reduced to trigger stuck penalty faster
    [Tooltip("Speed threshold (m/s) below which the agent is considered 'stuck' while off-track.")]
    public float stuckSpeedThreshold = 0.5f; // Reduced to detect being stuck earlier
    [Tooltip("SEVERE penalty applied per second while the agent is considered 'stuck' off-track.")]
    public float stuckOffTrackPenalty = -10.0f; // Doubled to strongly discourage being stuck
    [Tooltip("Strong guidance reward for velocity towards the NEAREST TRACK POINT *while off-track*.")]
    public float recoveryTargetRewardFactor = 2.0f; // Significantly increased to strongly encourage moving toward track
    [Tooltip("Penalty factor for velocity towards the TARGET CHECKPOINT *while off-track*. (Negative value)")]
    public float offTrackCheckpointAvoidancePenaltyFactor = 0.0f; // Set to zero to not penalize checkpoint seeking while off-track

    // --- Proactive Boundary Avoidance (On-Track) ---
    [Header("Proactive Boundary Avoidance (On-Track)")]
    public float proactiveBoundaryPenaltyDistance = 5.0f; // Increased to detect boundaries earlier
    public float proactiveBoundaryMaxPenaltyBase = -1.0f; // Increased to make boundary avoidance more important
    public float proactivePenaltySpeedFactor = 1.5f; // Increased to make high-speed boundary approaches more dangerous
    public float proactivePenaltyMaxSpeed = 50.0f;
    public float proactiveBoundaryCheckAngle = 60.0f;

    [Header("Episode Control & Reset")]
    public int maxEpisodeSteps = 30000;
    public float checkpointReachedDistance = 4.0f; // Trigger is primary mechanism
    public float progressTimeout = 10.0f; // Reduced from 15.0f to reset sooner when stuck
    public float minimumSpeedForProgress = 0.5f; // Increased from 0.1f to require more meaningful progress
    public bool resetOnBoundaryHit = false; // MUST BE FALSE FOR RECOVERY
    public bool resetWhenUpsideDown = true;
    public float upsideDownTimeThreshold = 2.0f; // Reduced from 3.0f to reset sooner when upside down
    [Range(0.8f, 1.0f)]
    public float minCheckpointFractionForLap = 0.9f;
    public bool enableExtendedStuckReset = true; // Changed to true to enable extended stuck reset
    public float extendedStuckResetTime = 8.0f; // Reduced from 10.0f to reset sooner when stuck off-track

    [Header("Debug Visualization")]
    public bool showRaycasts = false;
    public Color trackRayColor = Color.green;
    public Color boundaryRayColor = Color.magenta;
    public Color otherRayColor = Color.yellow;
    public Color proactiveWarnRayColor = Color.red;
    public bool showUpcomingCheckpoints = false; // Only shows when on track
    public Color nextCheckpointColor = Color.blue;
    public bool showCenterlineGizmo = true; // Only shows when on track
    public bool showRecoveryHelpers = true;

    // --- Internal State ---
    private int currentCheckpointIndex = 0;
    private int lapsCompleted = 0;
    private float timeSinceLastCheckpoint = 0f;
    private float previousDistanceToNextCheckpoint = float.MaxValue;
    private float timeSinceLastProgress = 0f;
    private float timeUpsideDown = 0f;
    private bool isCurrentlyOffTrack = false;
    private int stepsSinceEpisodeStart = 0;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool dependenciesReady = false;
    private int boundaryWallLayerIndex = -1;
    private float currentLapTimer = 0f;
    private float bestLapTime = float.MaxValue;
    private Vector3 closestPointOnCenterline; // For on-track centerline reward
    private int boundaryTriggersTouching = 0; // Counter for boundary trigger zones
    private HashSet<int> visitedCheckpointsThisLap = new HashSet<int>();
    private bool wasOffTrackLastFrame = false;
    private float previousDistanceToTrack = -1f; // Distance to boundary when off-track
    private float lastSteerAction = 0f; // Applied action (after multiplier)
    private float lastAccelAction = 0f; // Applied action
    private float lastBrakeAction = 0f; // Applied action
    private float previousSteerActionRaw = 0f; // Raw action (before multiplier) for smoothness calc
    private string lastResetReason = "N/A"; // For debugging/UI
    private float timeStuckOffTrack = 0f; // Timer for stuck detection
    private Vector3 nearestTrackSegmentStart = Vector3.zero; // For debug viz
    private Vector3 nearestTrackSegmentEnd = Vector3.zero;   // For debug viz
    private Vector3 nearestTrackPointTarget = Vector3.zero; // Target point for recovery

    // Anti-circling variables
    private Vector3 lastPosition = Vector3.zero; // Position from previous frame
    private float circlingDetectionRadius = 5.0f; // Radius to detect circling within
    private float circlingTime = 0f; // Time spent circling in the same area
    private float circlingDetectionTime = 3.0f; // Time threshold to detect circling
    private Dictionary<int, float> checkpointCooldowns = new Dictionary<int, float>(); // Cooldown timers for checkpoints
    private float checkpointCooldownTime = 10.0f; // Cooldown time before a checkpoint can be triggered again

    // --- Observation Size Calculation ---
    private int calculatedObservationSize = -1;
    private const int BASE_OBS_SIZE = 9; private const int OFF_TRACK_HISTORY_SIZE = 1; private const int BOUNDARY_OBS_SIZE = 8;
    private const int CHECKPOINT_BASE_OBS_SIZE_PER_CP = 3; private const int CHECKPOINT_ORIENT_OBS_SIZE_PER_CP = 4; private const int CHECKPOINT_TURN_ANGLE_OBS_SIZE = 1;
    private const int RAYCAST_OBS_SIZE_PER_RAY = 3; private const int ANGLE_TO_TRACK_OBS_SIZE = 1; private const int CURVATURE_OBS_SIZE = 1;
    private const int TIME_STUCK_OBS_SIZE = 1; private const int NEAREST_TRACK_POINT_OBS_SIZE = 4;
    private const int ENHANCED_RECOVERY_OBS_SIZE = 5; // 1 for distance + 2 for direction to track + 2 for track segment direction

    // --- Cached Raycast Results ---
    private float[] rayHitDistances;
    private int[] rayHitTypes;

    // --- Public Getters ---
    public int CurrentCheckpoint => currentCheckpointIndex; // Exposed for watch mode visualization
    public float CurrentLapTime => currentLapTimer; public float BestLapTimeSoFar => bestLapTime;
    public int StepCount => stepsSinceEpisodeStart; public float LastSteer => lastSteerAction; public float LastAccel => lastAccelAction;
    public float LastBrake => lastBrakeAction; public bool IsOffTrack => isCurrentlyOffTrack; public bool IsGrounded => vehicleController != null ? vehicleController.vehicleIsGrounded : false;
    public string LastResetReason => lastResetReason;
    public bool IsDependenciesReady => dependenciesReady;
    public int BoundaryWallLayerIndex => boundaryWallLayerIndex;

    void Awake()
    {
        rb = GetComponent<Rigidbody>(); vehicleController = GetComponent<VehicleController>(); inputManager = GetComponent<InputManager>();
        if (inputManager == null || vehicleController == null || rb == null) { Debug.LogError($"[{gameObject.name}] Missing core components. Disabling agent.", this); enabled = false; return; }
        CalculateObservationSize();
        if (numRaycasts > 0) { rayHitDistances = new float[numRaycasts]; rayHitTypes = new int[numRaycasts]; } else { Debug.LogWarning($"[{gameObject.name}] numRaycasts <= 0.", this); rayHitDistances = null; rayHitTypes = null; }
    }

    void OnValidate()
    {
         CalculateObservationSize(); if (numRaycasts > 0) { if (rayHitDistances == null || rayHitDistances.Length != numRaycasts) rayHitDistances = new float[numRaycasts]; if (rayHitTypes == null || rayHitTypes.Length != numRaycasts) rayHitTypes = new int[numRaycasts]; } else { rayHitDistances = null; rayHitTypes = null; }
         observeTrackCurvatureAhead = Mathf.Max(0, observeTrackCurvatureAhead); observeTrackCurvatureBehind = Mathf.Max(0, observeTrackCurvatureBehind);
    }

    private void CalculateObservationSize()
    {
        int cpObsSizePerCp = CHECKPOINT_BASE_OBS_SIZE_PER_CP + (observeCheckpointOrientation ? CHECKPOINT_ORIENT_OBS_SIZE_PER_CP : 0) + (observeUpcomingTurnAngle ? CHECKPOINT_TURN_ANGLE_OBS_SIZE : 0);
        int totalCheckpointObsSize = upcomingCheckpointsToTrack * cpObsSizePerCp; int totalRaycastObsSize = Mathf.Max(0, numRaycasts) * RAYCAST_OBS_SIZE_PER_RAY;
        int angleToTrackSize = observeAngleToTrack ? ANGLE_TO_TRACK_OBS_SIZE : 0; int curvatureSize = (observeTrackCurvatureAhead + observeTrackCurvatureBehind) * CURVATURE_OBS_SIZE;
        int timeStuckSize = observeTimeStuck ? TIME_STUCK_OBS_SIZE : 0; int nearestTrackPointSize = observeNearestTrackPoint ? NEAREST_TRACK_POINT_OBS_SIZE : 0;
        // Include enhanced recovery observations
        calculatedObservationSize = BASE_OBS_SIZE + OFF_TRACK_HISTORY_SIZE + BOUNDARY_OBS_SIZE + totalCheckpointObsSize + totalRaycastObsSize + angleToTrackSize + curvatureSize + timeStuckSize + nearestTrackPointSize + ENHANCED_RECOVERY_OBS_SIZE;
    }

    public override void Initialize()
    {
        Debug.Log($"[{gameObject.name}] Initializing RacingAgent...");
        boundaryWallLayerIndex = LayerMaskHelper.GetLayerIndexFromMask(boundaryWallLayer); if (boundaryWallLayerIndex < 0) { Debug.LogError($"[{gameObject.name}] Invalid Boundary Wall Layer mask! Disabling agent.", this); enabled = false; return; }
        if (raycastMask == 0) { raycastMask = LayerMask.GetMask("Default", "Track", "Grass", "TrackBoundaryWall", "Obstacle"); Debug.LogWarning($"[{gameObject.name}] Raycast Mask not set, using default.", this); }
        else if ((raycastMask & boundaryWallLayer) == 0) { Debug.LogWarning($"[{gameObject.name}] Raycast Mask missing Boundary Wall Layer.", this); }
        startPosition = transform.position; startRotation = transform.rotation;
        Debug.Log($"[{gameObject.name}] Starting position: {startPosition}, rotation: {startRotation.eulerAngles}");
        StartCoroutine(CheckDependencies());
    }

    private IEnumerator CheckDependencies()
    {
        dependenciesReady = false;
        Debug.Log($"[{gameObject.name}] Checking dependencies...");

        // Wait for CheckpointGenerator
        while (checkpointGenerator == null || !checkpointGenerator.IsInitialized) {
            if (checkpointGenerator == null) {
                Debug.Log($"[{gameObject.name}] Looking for CheckpointGenerator...");
                checkpointGenerator = FindObjectOfType<CheckpointGenerator>();
            }
            if (checkpointGenerator == null) {
                Debug.LogError($"[{gameObject.name}] CheckpointGenerator not found! Disabling.", this);
                enabled = false;
                yield break;
            }
            if (!checkpointGenerator.IsInitialized) {
                Debug.Log($"[{gameObject.name}] Waiting for CheckpointGenerator to initialize...");
            }
            yield return new WaitForSeconds(0.5f);
        }
        Debug.Log($"[{gameObject.name}] CheckpointGenerator ready with {checkpointGenerator.Checkpoints.Count} checkpoints.");

        // Wait for TrackBoundaryDetector
        while (boundaryDetector == null || !boundaryDetector.HasScanned) {
            if (boundaryDetector == null) {
                Debug.Log($"[{gameObject.name}] Looking for TrackBoundaryDetector...");
                boundaryDetector = FindObjectOfType<TrackBoundaryDetector>();
            }
            if (boundaryDetector == null) {
                Debug.LogError($"[{gameObject.name}] TrackBoundaryDetector not found! Disabling.", this);
                enabled = false;
                yield break;
            }
            if (!boundaryDetector.HasScanned) {
                Debug.Log($"[{gameObject.name}] Waiting for TrackBoundaryDetector to scan...");
            }
            yield return new WaitForSeconds(0.5f);
        }

        dependenciesReady = true;
        Debug.Log($"[{gameObject.name}] All dependencies ready.");
        FindClosestCheckpoint();
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log($"[{gameObject.name}] OnEpisodeBegin called");

        if (!dependenciesReady) {
            Debug.Log($"[{gameObject.name}] Dependencies not ready, starting dependency check");
            StartCoroutine(CheckDependencies());
            return;
        }

        Debug.Log($"[{gameObject.name}] Resetting vehicle and state variables");
        ResetVehicle();
        lapsCompleted = 0;
        timeSinceLastCheckpoint = 0f;
        timeSinceLastProgress = 0f;
        timeUpsideDown = 0f;
        isCurrentlyOffTrack = false;
        wasOffTrackLastFrame = false;
        previousDistanceToTrack = -1f;
        stepsSinceEpisodeStart = 0;
        currentLapTimer = 0f;
        boundaryTriggersTouching = 0;
        visitedCheckpointsThisLap.Clear();
        lastResetReason = "Episode Start";
        previousSteerActionRaw = 0f;
        timeStuckOffTrack = 0f;
        nearestTrackSegmentStart = Vector3.zero;
        nearestTrackSegmentEnd = Vector3.zero;
        nearestTrackPointTarget = Vector3.zero;

        // Reset anti-circling variables
        lastPosition = transform.position;
        circlingTime = 0f;
        checkpointCooldowns.Clear();

        Debug.Log($"[{gameObject.name}] Finding closest checkpoint");
        FindClosestCheckpoint();

        Debug.Log($"[{gameObject.name}] OnEpisodeBegin complete");
    }

    /// <summary>
    /// Public method to force checkpoint initialization - useful for external systems
    /// </summary>
    public void ForceCheckpointInitialization()
    {
        Debug.Log($"[{gameObject.name}] Force checkpoint initialization requested");

        // Make sure dependencies are ready
        if (!dependenciesReady)
        {
            Debug.Log($"[{gameObject.name}] Dependencies not ready, starting dependency check");
            StartCoroutine(CheckDependencies());
            return;
        }

        // Find the closest checkpoint
        FindClosestCheckpoint();
    }

    /// <summary>
    /// Public method to force a complete agent reset - useful for external systems
    /// </summary>
    public void ForceAgentReset()
    {
        Debug.Log($"[{gameObject.name}] Force agent reset requested");

        // Make sure dependencies are ready
        if (!dependenciesReady)
        {
            Debug.Log($"[{gameObject.name}] Dependencies not ready, starting dependency check");
            StartCoroutine(CheckDependencies());
            return;
        }

        // Call OnEpisodeBegin to reset everything
        OnEpisodeBegin();

        // Force a new decision from the agent
        RequestDecision();

        Debug.Log($"[{gameObject.name}] Force agent reset complete");
    }

    private void FindClosestCheckpoint()
    {
        Debug.Log($"[{gameObject.name}] Finding closest checkpoint...");
        int checkCount = (dependenciesReady && checkpointGenerator != null) ? checkpointGenerator.Checkpoints.Count : 0;

        if (checkCount < 2) {
            currentCheckpointIndex = 0;
            previousDistanceToNextCheckpoint = float.MaxValue;
            if(dependenciesReady) {
                Debug.LogWarning($"[{gameObject.name}] Insufficient checkpoints ({checkCount}).", this);
            }
            return;
        }

        // Log all checkpoints for debugging
        Debug.Log($"[{gameObject.name}] Track has {checkCount} checkpoints total.");
        for (int i = 0; i < Math.Min(checkCount, 10); i++) {
            CheckpointGenerator.Checkpoint cp = checkpointGenerator.GetCheckpoint(i);
            if (cp != null) {
                Debug.Log($"[{gameObject.name}] Checkpoint {i}: Position={cp.position}, Forward={cp.forward}");
            }
        }
        if (checkCount > 10) {
            Debug.Log($"[{gameObject.name}] ... and {checkCount - 10} more checkpoints.");
        }

        int closestIndex;
        CheckpointGenerator.Checkpoint nearestCp = checkpointGenerator.GetNearestCheckpoint(transform.position, out closestIndex);

        if (nearestCp != null) {
            Debug.Log($"[{gameObject.name}] Found nearest checkpoint: {closestIndex} at position {nearestCp.position}");
            Debug.Log($"[{gameObject.name}] Agent position: {transform.position}, distance to nearest: {Vector3.Distance(transform.position, nearestCp.position):F2}m");

            CheckpointGenerator.Checkpoint prevCp = checkpointGenerator.GetPreviousCheckpoint(closestIndex);
            Vector3 toNearest = (nearestCp.position - transform.position).normalized;
            Vector3 prevToNearest = (prevCp != null) ? (nearestCp.position - prevCp.position).normalized : Vector3.zero;
            float alignmentDot = Vector3.Dot(toNearest, prevToNearest);
            bool isAligned = prevToNearest != Vector3.zero && toNearest.sqrMagnitude > 0.01f && alignmentDot > 0.3f;

            Debug.Log($"[{gameObject.name}] Alignment check: toNearest={toNearest}, prevToNearest={prevToNearest}, dot={alignmentDot:F2}, isAligned={isAligned}");

            // IMPORTANT FIX: Always use the nearest checkpoint as the current checkpoint
            // This is more reliable than the alignment check, especially with custom spawn points
            currentCheckpointIndex = closestIndex;

            // Old logic (commented out):
            // currentCheckpointIndex = isAligned ? closestIndex : (closestIndex + 1) % checkCount;

            CheckpointGenerator.Checkpoint nextCp = checkpointGenerator.GetCheckpoint(currentCheckpointIndex);
            previousDistanceToNextCheckpoint = (nextCp != null) ? Vector3.Distance(transform.position, nextCp.position) : float.MaxValue;

            Debug.Log($"[{gameObject.name}] Set current checkpoint to {currentCheckpointIndex}, distance: {previousDistanceToNextCheckpoint:F2}m");
        }
        else {
            currentCheckpointIndex = 0;
            previousDistanceToNextCheckpoint = float.MaxValue;
            Debug.LogWarning($"[{gameObject.name}] GetNearestCheckpoint returned null.", this);
        }

        visitedCheckpointsThisLap.Clear();
        Debug.Log($"[{gameObject.name}] Checkpoint initialization complete.");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (calculatedObservationSize < 0) CalculateObservationSize(); if (!dependenciesReady || vehicleController == null || rb == null || boundaryDetector == null) { for (int i = 0; i < calculatedObservationSize; ++i) sensor.AddObservation(0f); return; }
        // Base Vehicle State
        Vector3 lv = transform.InverseTransformDirection(rb.linearVelocity); float maxSpd = Mathf.Max(1f, vehicleController.MaxSpeed); sensor.AddObservation(Mathf.Clamp(lv.x / maxSpd, -1f, 1f)); sensor.AddObservation(Mathf.Clamp(lv.z / maxSpd, -1f, 1f)); sensor.AddObservation(Mathf.Clamp(lv.y / maxSpd, -1f, 1f)); sensor.AddObservation(Mathf.Clamp(rb.angularVelocity.y / 10f, -1f, 1f)); sensor.AddObservation(Mathf.Clamp(rb.angularVelocity.x / 10f, -1f, 1f)); sensor.AddObservation(Mathf.Clamp(rb.angularVelocity.z / 10f, -1f, 1f)); sensor.AddObservation(vehicleController.vehicleIsGrounded ? 1f : 0f); sensor.AddObservation(isCurrentlyOffTrack ? 1f : 0f); sensor.AddObservation(Vector3.Dot(transform.up, Vector3.up));
        // History & Boundary
        sensor.AddObservation(wasOffTrackLastFrame ? 1f : 0f); AddBoundaryObservations(sensor);
        // Checkpoints (Conditional)
        if (!isCurrentlyOffTrack) { AddCheckpointObservations(sensor); } else { int cpObsSizePerCp = CHECKPOINT_BASE_OBS_SIZE_PER_CP + (observeCheckpointOrientation ? CHECKPOINT_ORIENT_OBS_SIZE_PER_CP : 0) + (observeUpcomingTurnAngle ? CHECKPOINT_TURN_ANGLE_OBS_SIZE : 0); int totalCpObs = upcomingCheckpointsToTrack * cpObsSizePerCp; for(int i = 0; i < totalCpObs; ++i) sensor.AddObservation(0f); }
        // Raycasts & Recovery Sensors
        AddRaycastObservationsAndCache(sensor); AddRecoveryObservations(sensor);
    }

    private void AddBoundaryObservations(VectorSensor sensor)
    {
        Vector3 cI = Vector3.zero, cO = Vector3.zero; float maxD = 50f; bool found = false;
        if (boundaryDetector != null && boundaryDetector.HasScanned) found = boundaryDetector.GetClosestBoundaryPoints(transform.position, out cI, out cO, maxD);
        if (found) { Vector3 tiL = transform.InverseTransformDirection(cI - transform.position); float iD = tiL.magnitude; Vector3 toL = transform.InverseTransformDirection(cO - transform.position); float oD = toL.magnitude; sensor.AddObservation(tiL.normalized); sensor.AddObservation(Mathf.Clamp01(iD / maxD)); sensor.AddObservation(toL.normalized); sensor.AddObservation(Mathf.Clamp01(oD / maxD)); }
        else { sensor.AddObservation(Vector3.zero); sensor.AddObservation(1f); sensor.AddObservation(Vector3.zero); sensor.AddObservation(1f); }
    }

    private void AddCheckpointObservations(VectorSensor sensor) // Only called when On Track
    {
        List<CheckpointGenerator.Checkpoint> up = checkpointGenerator.GetUpcomingCheckpoints(currentCheckpointIndex, upcomingCheckpointsToTrack + 1);
        for (int i = 0; i < upcomingCheckpointsToTrack; i++)
        {
            CheckpointGenerator.Checkpoint cp = (i < up.Count) ? up[i] : null;
            CheckpointGenerator.Checkpoint cpN = ((i + 1) < up.Count) ? up[i+1] : null;
            if (cp != null)
            {
                Vector3 dW = cp.position - transform.position; Vector3 dL = transform.InverseTransformDirection(dW); float d = dW.magnitude; float nD = Mathf.Clamp01(d / raycastDistance);
                sensor.AddObservation(Mathf.Clamp(dL.x/(d+1e-5f),-1f,1f)); sensor.AddObservation(Mathf.Clamp(dL.z/(d+1e-5f),-1f,1f)); sensor.AddObservation(nD);
                if (observeCheckpointOrientation){Vector3 fL=transform.InverseTransformDirection(cp.forward); Vector3 rL=transform.InverseTransformDirection(cp.right); sensor.AddObservation(Mathf.Clamp(fL.x,-1f,1f)); sensor.AddObservation(Mathf.Clamp(fL.z,-1f,1f)); sensor.AddObservation(Mathf.Clamp(rL.x,-1f,1f)); sensor.AddObservation(Mathf.Clamp(rL.z,-1f,1f));}
                if(observeUpcomingTurnAngle){float tAngN=0f; if(cpN!=null){Vector3 s1=(cp.position-transform.position).normalized; Vector3 s2=(cpN.position-cp.position).normalized; if(s1.sqrMagnitude>0.01f&&s2.sqrMagnitude>0.01f){tAngN=Mathf.Clamp(Vector3.SignedAngle(s1,s2,Vector3.up)/180f,-1f,1f);}} sensor.AddObservation(tAngN);}
            }
            else
            {
                sensor.AddObservation(0f);sensor.AddObservation(0f);sensor.AddObservation(1f);
                if(observeCheckpointOrientation){sensor.AddObservation(0f);sensor.AddObservation(1f);sensor.AddObservation(1f);sensor.AddObservation(0f);}
                if(observeUpcomingTurnAngle){sensor.AddObservation(0f);}
            }
        }
        if (showUpcomingCheckpoints && up.Count > 0 && up[0] != null) Debug.DrawLine(transform.position + transform.up*raycastVerticalOffset, up[0].position, nextCheckpointColor, 0f);
    }

    private void AddRaycastObservationsAndCache(VectorSensor sensor)
    {
        if (numRaycasts <= 0 || rayHitDistances == null || rayHitTypes == null) { int exp = Mathf.Max(0, numRaycasts) * RAYCAST_OBS_SIZE_PER_RAY; for (int i = 0; i < exp; ++i) sensor.AddObservation(0f); return; }
        Vector3 o = transform.position + transform.up*raycastVerticalOffset; float step=(numRaycasts>1)?raycastAngle/(numRaycasts-1):0f; float startA=(numRaycasts>1)?-raycastAngle/2f:0f;
        for(int i=0; i<numRaycasts; ++i)
        {
            float ang=startA+i*step; Quaternion rot=Quaternion.Euler(0,ang,0); Vector3 dir=rot*transform.forward; RaycastHit h;
            float hDistN=1f; float hTypeN=0f; float hNormYN=0.5f; int hTypeI=0; float actHitD=raycastDistance;
            if(Physics.Raycast(o,dir,out h,raycastDistance,raycastMask))
            {
                actHitD=h.distance; hDistN=actHitD/raycastDistance; hNormYN=(Mathf.Clamp(h.normal.y,-1f,1f)+1f)/2f;
                int lay=h.collider.gameObject.layer; Color drwC=otherRayColor;
                if(lay==boundaryWallLayerIndex){hTypeN=1.0f;hTypeI=2;drwC=boundaryRayColor;} // Boundary = 1.0
                else if(((1<<lay)&trackLayer)!=0){hTypeN=0.5f;hTypeI=1;drwC=trackRayColor;} // Track = 0.5
                else{hTypeN=0.0f;hTypeI=3;} // Other/Miss = 0.0
                bool proZone=Mathf.Abs(ang)<=proactiveBoundaryCheckAngle;
                if(showRaycasts&&proZone&&hTypeI==2&&actHitD<proactiveBoundaryPenaltyDistance)drwC=proactiveWarnRayColor;
                if(showRaycasts)Debug.DrawLine(o,h.point,drwC,0f);
            }
            else if(showRaycasts)
            {
                Debug.DrawRay(o,dir*raycastDistance,Color.grey,0f);
            }
            sensor.AddObservation(hDistN);sensor.AddObservation(hTypeN);sensor.AddObservation(hNormYN);
            if(i<rayHitDistances.Length)rayHitDistances[i]=actHitD;
            if(i<rayHitTypes.Length)rayHitTypes[i]=hTypeI;
        }
    }

    private void AddRecoveryObservations(VectorSensor sensor)
    {
        Vector3 segDir=Vector3.forward; bool segFound=false; nearestTrackPointTarget=Vector3.zero; float distToTrackPt=raycastDistance;
        if(checkpointGenerator!=null&&checkpointGenerator.IsInitialized&&checkpointGenerator.Checkpoints.Count>=2)
        {
            int nearIdx; CheckpointGenerator.Checkpoint nearCP=checkpointGenerator.GetNearestCheckpoint(transform.position, out nearIdx);
            if(nearCP!=null)
            {
                 CheckpointGenerator.Checkpoint prevCP=checkpointGenerator.GetPreviousCheckpoint(nearIdx); CheckpointGenerator.Checkpoint nextCP=checkpointGenerator.GetNextCheckpoint(nearIdx);
                 if(prevCP!=null&&nextCP!=null)
                 {
                    Vector3 pPrev=GetClosestPointOnLineSegment(prevCP.position,nearCP.position,transform.position); Vector3 pNext=GetClosestPointOnLineSegment(nearCP.position,nextCP.position,transform.position);
                    float dPrev=(pPrev-transform.position).sqrMagnitude; float dNext=(pNext-transform.position).sqrMagnitude;
                    if(dPrev<dNext){
                        nearestTrackPointTarget=pPrev;
                        segDir=(nearCP.position-prevCP.position).normalized;
                        nearestTrackSegmentStart=prevCP.position;
                        nearestTrackSegmentEnd=nearCP.position;
                        distToTrackPt=Mathf.Sqrt(dPrev);
                    }
                    else{
                        nearestTrackPointTarget=pNext;
                        segDir=(nextCP.position-nearCP.position).normalized;
                        nearestTrackSegmentStart=nearCP.position;
                        nearestTrackSegmentEnd=nextCP.position;
                        distToTrackPt=Mathf.Sqrt(dNext);
                    }
                    segFound=true;
                 }
                 else if(prevCP!=null){
                    nearestTrackPointTarget=GetClosestPointOnLineSegment(prevCP.position,nearCP.position,transform.position);
                    segDir=(nearCP.position-prevCP.position).normalized;
                    nearestTrackSegmentStart=prevCP.position;
                    nearestTrackSegmentEnd=nearCP.position;
                    distToTrackPt=Vector3.Distance(transform.position,nearestTrackPointTarget);
                    segFound=true;
                 }
                 else if(nextCP!=null){
                    nearestTrackPointTarget=GetClosestPointOnLineSegment(nearCP.position,nextCP.position,transform.position);
                    segDir=(nextCP.position-nearCP.position).normalized;
                    nearestTrackSegmentStart=nearCP.position;
                    nearestTrackSegmentEnd=nextCP.position;
                    distToTrackPt=Vector3.Distance(transform.position,nearestTrackPointTarget);
                    segFound=true;
                 }
            }
        }

        // Enhanced recovery observations
        if(observeAngleToTrack){
            float angCos=segFound?Vector3.Dot(transform.forward,segDir):0f;
            sensor.AddObservation(angCos);
        }

        // Add normalized distance to nearest track point
        sensor.AddObservation(Mathf.Clamp01(distToTrackPt / raycastDistance));

        // Add direction to nearest track point in local space
        if (nearestTrackPointTarget != Vector3.zero) {
            Vector3 dirToTrackLocal = transform.InverseTransformDirection(nearestTrackPointTarget - transform.position).normalized;
            sensor.AddObservation(dirToTrackLocal.x);
            sensor.AddObservation(dirToTrackLocal.z);
        } else {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // Add track segment direction in local space
        if (segFound) {
            Vector3 trackDirLocal = transform.InverseTransformDirection(segDir);
            sensor.AddObservation(trackDirLocal.x);
            sensor.AddObservation(trackDirLocal.z);
        } else {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
        AddCurvatureObservations(sensor,observeTrackCurvatureAhead,observeTrackCurvatureBehind);
        if(observeTimeStuck){float normT=Mathf.Clamp01(timeStuckOffTrack/timeUntilStuckThreshold); sensor.AddObservation(normT);}
        if(observeNearestTrackPoint){if(segFound){Vector3 dirL=transform.InverseTransformDirection(nearestTrackPointTarget-transform.position); float distN=Mathf.Clamp01(distToTrackPt/raycastDistance); sensor.AddObservation(dirL.normalized); sensor.AddObservation(distN);} else{sensor.AddObservation(Vector3.zero); sensor.AddObservation(1f);}}
    }

    private void AddCurvatureObservations(VectorSensor sensor, int ahead, int behind)
    {
        if (checkpointGenerator==null||!checkpointGenerator.IsInitialized||checkpointGenerator.Checkpoints.Count<2){for(int i=0;i<ahead+behind;++i)sensor.AddObservation(0f);return;}
        int nearIdx; checkpointGenerator.GetNearestCheckpoint(transform.position, out nearIdx);
        Vector3 curPosA=transform.position;
        for(int i=0;i<ahead;++i)
        {
            int nIdx=(nearIdx+i+1)%checkpointGenerator.Checkpoints.Count; int nnIdx=(nearIdx+i+2)%checkpointGenerator.Checkpoints.Count;
            CheckpointGenerator.Checkpoint cpN=checkpointGenerator.GetCheckpoint(nIdx); CheckpointGenerator.Checkpoint cpNN=checkpointGenerator.GetCheckpoint(nnIdx);
            float angN=0f;
            if(cpN!=null&&cpNN!=null){Vector3 s1=(cpN.position-curPosA).normalized; Vector3 s2=(cpNN.position-cpN.position).normalized; if(s1.sqrMagnitude>0.01f&&s2.sqrMagnitude>0.01f){angN=Mathf.Clamp(Vector3.SignedAngle(s1,s2,Vector3.up)/180f,-1f,1f);} curPosA=cpN.position;}
            sensor.AddObservation(angN);
        }
        Vector3 curPosB=transform.position;
        for(int i=0;i<behind;++i)
        {
            int pIdx=(nearIdx-i-1+checkpointGenerator.Checkpoints.Count)%checkpointGenerator.Checkpoints.Count; int ppIdx=(nearIdx-i-2+checkpointGenerator.Checkpoints.Count)%checkpointGenerator.Checkpoints.Count;
            CheckpointGenerator.Checkpoint cpP=checkpointGenerator.GetCheckpoint(pIdx); CheckpointGenerator.Checkpoint cpPP=checkpointGenerator.GetCheckpoint(ppIdx);
            float angN=0f;
            if(cpP!=null&&cpPP!=null){Vector3 s1=(cpP.position-cpPP.position).normalized; Vector3 s2=(curPosB-cpP.position).normalized; if(s1.sqrMagnitude>0.01f&&s2.sqrMagnitude>0.01f){angN=Mathf.Clamp(Vector3.SignedAngle(s1,s2,Vector3.up)/180f,-1f,1f);} curPosB=cpP.position;}
            sensor.AddObservation(angN);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!dependenciesReady || inputManager == null || vehicleController == null) {
            Debug.LogWarning($"[{gameObject.name}] OnActionReceived called but dependencies not ready!");
            return;
        }
        stepsSinceEpisodeStart++; currentLapTimer += Time.fixedDeltaTime;

        // 1. Apply Actions
        float steerRaw = actions.ContinuousActions[0]; float accelRaw = actions.ContinuousActions[1]; float brakeRaw = actions.ContinuousActions[2];
        float steerApplied = Mathf.Clamp(steerRaw, -1f, 1f) * steerMultiplier; float accelApplied = Mathf.Clamp01(accelRaw) * accelMultiplier; float brakeApplied = Mathf.Clamp01(brakeRaw) * brakeMultiplier;

        // Log the actions occasionally
        if (Time.frameCount % 60 == 0) {
            Debug.Log($"[{gameObject.name}] Actions - Raw: Steer={steerRaw:F2}, Accel={accelRaw:F2}, Brake={brakeRaw:F2} | Applied: Steer={steerApplied:F2}, Accel={accelApplied:F2}, Brake={brakeApplied:F2}");
        }

        inputManager.SetMLAgentInputs(steerApplied, accelApplied, brakeApplied);
        lastSteerAction = steerApplied; lastAccelAction = accelApplied; lastBrakeAction = brakeApplied;

        // 2. State Tracking
        wasOffTrackLastFrame = isCurrentlyOffTrack; isCurrentlyOffTrack = (boundaryTriggersTouching > 0);
        if (isCurrentlyOffTrack && rb.linearVelocity.magnitude < stuckSpeedThreshold) { timeStuckOffTrack += Time.fixedDeltaTime; } else { timeStuckOffTrack = 0f; }

        // Update checkpoint cooldowns - using a safe approach to avoid collection modification during enumeration
        List<int> keysToProcess = new List<int>(checkpointCooldowns.Keys);
        List<int> expiredCooldowns = new List<int>();

        foreach (int key in keysToProcess) {
            checkpointCooldowns[key] -= Time.fixedDeltaTime;
            if (checkpointCooldowns[key] <= 0) {
                expiredCooldowns.Add(key);
            }
        }

        foreach (int cpIndex in expiredCooldowns) {
            checkpointCooldowns.Remove(cpIndex);
        }

        // Detect circling behavior - improved to be more robust
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        float speed = rb.linearVelocity.magnitude;
        float angularSpeed = Mathf.Abs(rb.angularVelocity.y);

        // Detect circling: low linear displacement + high speed + high angular velocity
        if (!isCurrentlyOffTrack && distanceMoved < 1.0f && speed > 3.0f && angularSpeed > 1.0f) {
            // Agent is moving fast with high rotation but not changing position much - likely circling
            circlingTime += Time.fixedDeltaTime;
            if (circlingTime > circlingDetectionTime) {
                // Apply circling penalty that scales with time spent circling
                float penaltyMultiplier = Mathf.Min(3.0f, 1.0f + (circlingTime - circlingDetectionTime) / 5.0f);
                AddReward(circlingPenaltyFactor * penaltyMultiplier * Time.fixedDeltaTime);

                if (showRaycasts && Time.frameCount % 30 == 0) { // Log less frequently to avoid spam
                    Debug.Log($"[{gameObject.name}] Circling behavior detected for {circlingTime:F1}s! Applying penalty x{penaltyMultiplier:F1}.");
                }
            }
        } else {
            // Gradually reduce circling timer if agent is moving normally
            circlingTime = Mathf.Max(0f, circlingTime - Time.fixedDeltaTime * 0.5f);
        }
        lastPosition = transform.position;

        // 3. Rewards
        AddReward(timePenalty * Time.fixedDeltaTime); // Base time penalty

        // === OFF-TRACK ===
        if (isCurrentlyOffTrack)
        {
            AddReward(continuousOffTrackPenalty * Time.fixedDeltaTime); // Heavy penalty

            // Boundary Distance R/P
            float currentDist = GetDistanceToNearestBoundary(); if (currentDist >= 0 && previousDistanceToTrack >= 0) { float delta = previousDistanceToTrack - currentDist; if (delta > 0.01f) AddReward(delta * distanceReductionRewardFactor); else if (delta < -0.01f) AddReward(delta * Mathf.Abs(distanceIncreasePenaltyFactor)); } previousDistanceToTrack = currentDist;
            // Orientation Nudge
            if (offTrackOrientationRewardFactor != 0 && nearestTrackSegmentEnd != Vector3.zero) { Vector3 trackDir = (nearestTrackSegmentEnd - nearestTrackSegmentStart).normalized; float alignCos = Vector3.Dot(transform.forward, trackDir); AddReward(Mathf.Clamp01(alignCos) * offTrackOrientationRewardFactor * Time.fixedDeltaTime); }
            // Recovery Target Nudge (Towards Track Point) - Enhanced to always provide guidance
            if (recoveryTargetRewardFactor != 0 && nearestTrackPointTarget != Vector3.zero) {
                Vector3 dirToTarget = (nearestTrackPointTarget - transform.position).normalized;
                float velTowardsTarget = Vector3.Dot(rb.linearVelocity, dirToTarget);
                float alignToTarget = Vector3.Dot(transform.forward, dirToTarget);

                // Always provide some reward for being aligned with the recovery target
                AddReward(Mathf.Clamp01(alignToTarget) * recoveryTargetRewardFactor * 0.5f * Time.fixedDeltaTime);

                // Reward for moving toward the track (even small movements)
                if (velTowardsTarget > 0.01f) { // Reduced threshold to reward even small progress
                    AddReward(velTowardsTarget * recoveryTargetRewardFactor * Time.fixedDeltaTime);
                }

                // Penalty for moving away from the track
                else if (velTowardsTarget < -0.1f) {
                    AddReward(velTowardsTarget * recoveryTargetRewardFactor * 0.5f * Time.fixedDeltaTime); // Half penalty for moving away
                }

                // Debug visualization
                if (showRaycasts) {
                    Debug.DrawLine(transform.position, nearestTrackPointTarget, Color.magenta, 0f);
                }
            }
            // Checkpoint Guidance (Modified to help with recovery)
            if (checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > currentCheckpointIndex) {
                CheckpointGenerator.Checkpoint targetCP = checkpointGenerator.GetCheckpoint(currentCheckpointIndex);
                if (targetCP != null) {
                    Vector3 dirToCP = (targetCP.position - transform.position).normalized;
                    float velToCP = Vector3.Dot(rb.linearVelocity, dirToCP);

                    // If we're set to avoid checkpoints while off-track
                    if (offTrackCheckpointAvoidancePenaltyFactor < 0 && velToCP > 0.1f) {
                        AddReward(velToCP * offTrackCheckpointAvoidancePenaltyFactor * Time.fixedDeltaTime);
                    }
                    // If we're allowing checkpoint seeking (factor = 0), check if it helps recovery
                    else if (offTrackCheckpointAvoidancePenaltyFactor == 0) {
                        // Check if moving toward checkpoint also moves us toward the track
                        if (nearestTrackPointTarget != Vector3.zero) {
                            Vector3 dirToTrack = (nearestTrackPointTarget - transform.position).normalized;
                            float dotProduct = Vector3.Dot(dirToCP, dirToTrack);

                            // If checkpoint direction is somewhat aligned with track direction, reward it
                            if (dotProduct > 0.5f && velToCP > 0.1f) {
                                AddReward(velToCP * dotProduct * 0.5f * Time.fixedDeltaTime);
                            }
                        }
                    }
                }
            }
            // Stuck Penalty
            if (timeStuckOffTrack > timeUntilStuckThreshold) { AddReward(stuckOffTrackPenalty * Time.fixedDeltaTime); }
        }
        // === ON-TRACK ===
        else
        {
            if (wasOffTrackLastFrame) AddReward(recoveryReward); // Recovery bonus
            previousDistanceToTrack = -1f; // Reset state

            // Standard On-Track Rewards
            CheckpointGenerator.Checkpoint targetCP = checkpointGenerator.GetCheckpoint(currentCheckpointIndex); CheckpointGenerator.Checkpoint prevCP = checkpointGenerator.GetPreviousCheckpoint(currentCheckpointIndex);
            // Speed
            float fwdSpeed = Mathf.Max(0, Vector3.Dot(rb.linearVelocity, transform.forward)); float spdAlign=1.0f; if(targetCP!=null&&prevCP!=null)spdAlign=Mathf.Clamp01(Vector3.Dot(transform.forward,(targetCP.position-prevCP.position).normalized)); AddReward(spdAlign*fwdSpeed*speedRewardFactor*Time.fixedDeltaTime);
            // CP Progress - Enhanced to strongly reward progress toward next checkpoint
            if(targetCP!=null){
                // Direction to checkpoint
                Vector3 dirCP=(targetCP.position-transform.position).normalized;
                // Velocity toward checkpoint
                float velCP=Vector3.Dot(rb.linearVelocity,dirCP);
                // Alignment of car forward with direction to checkpoint
                float alignCP=Mathf.Clamp01(Vector3.Dot(transform.forward,dirCP));
                // Alignment with track direction
                float alignTr=1.0f;
                if(prevCP!=null)alignTr=Mathf.Clamp01(Vector3.Dot(transform.forward,(targetCP.position-prevCP.position).normalized));

                // Calculate distance improvement to checkpoint
                float currentDistance = Vector3.Distance(transform.position, targetCP.position);
                float distanceImprovement = previousDistanceToNextCheckpoint - currentDistance;

                // Reward for moving toward checkpoint
                if(velCP>0.1f){
                    // Base progress reward
                    AddReward(alignTr*alignCP*velCP*velocityProgressRewardFactor*Time.fixedDeltaTime);

                    // Additional reward for distance improvement
                    if(distanceImprovement > 0.01f) {
                        AddReward(distanceImprovement * 0.1f);
                    }

                    timeSinceLastProgress=0f;
                } else {
                    // Penalty for not making progress
                    if(rb.linearVelocity.magnitude<minimumSpeedForProgress*1.5f) {
                        timeSinceLastProgress+=Time.fixedDeltaTime;
                    } else {
                        timeSinceLastProgress=0f;
                    }
                }

                previousDistanceToNextCheckpoint=currentDistance;
            } else {
                previousDistanceToNextCheckpoint=float.MaxValue;
                timeSinceLastProgress=0f;
            }
            // Centerline
            if(targetCP!=null&&prevCP!=null&&centerlineRewardFactor>0){closestPointOnCenterline=GetClosestPointOnLineSegment(prevCP.position,targetCP.position,transform.position); float d=Vector3.Distance(transform.position,closestPointOnCenterline); float z=centerlineRewardZoneWidth/2.0f; float m=Mathf.Clamp01(1.0f-Mathf.InverseLerp(0,z,d)); AddReward(m*centerlineRewardFactor*Time.fixedDeltaTime);}else{closestPointOnCenterline=Vector3.zero;}
            // Spin
            if(spinPenaltyFactor<0)AddReward(Mathf.Abs(rb.angularVelocity.y)*spinPenaltyFactor*Time.fixedDeltaTime);
            // Proactive Boundary
            if(proactiveBoundaryMaxPenaltyBase<0){float pPen=CalculateProactiveBoundaryPenalty(fwdSpeed); if(pPen<0)AddReward(pPen*Time.fixedDeltaTime);}
        }

        // Always Apply
        // Steering Smoothness
        if (steeringSmoothnessPenaltyFactor < 0) { float steerDelta = Mathf.Abs(steerRaw - previousSteerActionRaw); AddReward(steerDelta * steerDelta * steeringSmoothnessPenaltyFactor * Time.fixedDeltaTime); }
        previousSteerActionRaw = steerRaw;

        // 4. Check Reset
        CheckResetConditions();
    }

    private float CalculateProactiveBoundaryPenalty(float currentForwardSpeed)
    {
        if (numRaycasts <= 0 || rayHitDistances == null || rayHitTypes == null) return 0f;
        float tPen = 0f; int cRay = 0; float aStep = (numRaycasts > 1) ? raycastAngle / (numRaycasts - 1) : 0f; float sAng = (numRaycasts > 1) ? -raycastAngle / 2f : 0f;
        for (int i = 0; i < numRaycasts; i++) { float cAng = sAng + i * aStep; if (Mathf.Abs(cAng) <= proactiveBoundaryCheckAngle && rayHitTypes[i] == 2 && rayHitDistances[i] < proactiveBoundaryPenaltyDistance) { cRay++; tPen += (1.0f - (rayHitDistances[i] / proactiveBoundaryPenaltyDistance)); } }
        if (cRay > 0) { float avgP = tPen / cRay; float spdScl = Mathf.Clamp01(currentForwardSpeed / proactivePenaltyMaxSpeed); float maxP = Mathf.Lerp(proactiveBoundaryMaxPenaltyBase * 0.1f, proactiveBoundaryMaxPenaltyBase, spdScl * proactivePenaltySpeedFactor); float pen = Mathf.Lerp(0, maxP, avgP); if (cRay > 3) pen *= 1.2f; return Mathf.Min(0f, pen); } return 0f;
    }

    private float GetDistanceToNearestBoundary()
    {
        if (boundaryDetector == null || !boundaryDetector.HasScanned) return -1f; Vector3 cI, cO; if (boundaryDetector.GetClosestBoundaryPoints(transform.position, out cI, out cO, 50f)) { return Mathf.Min(Vector3.Distance(transform.position, cI), Vector3.Distance(transform.position, cO)); } return -1f;
    }

    private void CheckResetConditions()
    {
        bool rst = false; string rsn = "";
        if (!isCurrentlyOffTrack && timeSinceLastProgress > progressTimeout) { if (rb.linearVelocity.magnitude < minimumSpeedForProgress * 2.0f) { rst = true; rsn = "Timeout/Stuck On-Track"; } else { timeSinceLastProgress = 0f; } }
        if (resetWhenUpsideDown && Vector3.Dot(transform.up, Vector3.up) < 0.1f) { timeUpsideDown += Time.fixedDeltaTime; if (timeUpsideDown > upsideDownTimeThreshold) { rst = true; rsn = "Upside Down"; } } else { timeUpsideDown = 0f; }
        if (stepsSinceEpisodeStart >= maxEpisodeSteps) { rst = true; rsn = "Max Steps"; }
        if(transform.position.y < -20f) { rst = true; rsn = "Fell Off World"; }
        if (!rst && enableExtendedStuckReset && isCurrentlyOffTrack && timeStuckOffTrack > extendedStuckResetTime) { rst = true; rsn = "Stuck Off-Track (Extended)"; }
        if (rst) { lastResetReason = rsn; SetReward(resetPenalty); EndEpisode(); }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions; ca.Clear(); float s = Input.GetAxis("Horizontal"); float a = Mathf.Clamp01(Input.GetAxis("Vertical")); float b = Mathf.Clamp01(-Input.GetAxis("Vertical")); if (Input.GetKey(KeyCode.Space)) { b = 1f; a = 0f; } ca[0] = s; ca[1] = a; ca[2] = b;
    }

    private void ResetVehicle()
    {
        Debug.Log($"[{gameObject.name}] ResetVehicle called");

        if(rb == null) {
            Debug.LogWarning($"[{gameObject.name}] Cannot reset vehicle - Rigidbody is null");
            return;
        }

        // Reset physics
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        Debug.Log($"[{gameObject.name}] Reset vehicle physics");

        // Set position and rotation
        if (startRotation == default(Quaternion)) {
            startRotation = transform.rotation;
            Debug.Log($"[{gameObject.name}] Using current rotation as start rotation: {startRotation.eulerAngles}");
        }

        Debug.Log($"[{gameObject.name}] Setting position to {startPosition} and rotation to {startRotation.eulerAngles}");
        transform.SetPositionAndRotation(startPosition, startRotation);

        // Reset inputs
        if(inputManager != null) {
            inputManager.SetMLAgentInputs(0, 0, 0);
            inputManager.EnableMLAgentControl(true);
            Debug.Log($"[{gameObject.name}] Reset inputs and enabled ML-Agent control");
        } else {
            Debug.LogWarning($"[{gameObject.name}] InputManager is null, cannot reset inputs");
        }

        previousSteerActionRaw = 0f;
        lastSteerAction = 0f;
        lastAccelAction = 0f;
        lastBrakeAction = 0f;

        Debug.Log($"[{gameObject.name}] Vehicle reset complete");
    }

    // Collision/Trigger Handling
    void OnTriggerEnter(Collider other) { if (!dependenciesReady || !enabled) return; if (other.gameObject.layer == boundaryWallLayerIndex) { boundaryTriggersTouching++; if (boundaryTriggersTouching == 1) HandleBoundaryHit(); } else { CheckpointGenerator.Checkpoint hCP = GetCheckpointFromCollider(other); if (hCP != null) HandleCheckpointHit(hCP); } }
    void OnTriggerExit(Collider other) { if (!dependenciesReady || !enabled) return; if (other.gameObject.layer == boundaryWallLayerIndex) { boundaryTriggersTouching--; if (boundaryTriggersTouching < 0) boundaryTriggersTouching = 0; } }
    private CheckpointGenerator.Checkpoint GetCheckpointFromCollider(Collider col) {
        if (checkpointGenerator == null) {
            Debug.LogWarning($"[{gameObject.name}] GetCheckpointFromCollider: checkpointGenerator is null");
            return null;
        }

        // Try to get checkpoint directly from the collider's name
        if (col.gameObject.name.StartsWith("Checkpoint_") && int.TryParse(col.gameObject.name.Substring(11), out int idx)) {
            Debug.Log($"[{gameObject.name}] Found checkpoint {idx} from collider name: {col.gameObject.name}");
            return checkpointGenerator.GetCheckpoint(idx);
        }

        // Try to get checkpoint from parent hierarchy
        if (col.transform.parent != null) {
            Transform parent = col.transform.parent;
            if (parent.name.StartsWith("Checkpoint_") && int.TryParse(parent.name.Substring(11), out int parentIdx)) {
                Debug.Log($"[{gameObject.name}] Found checkpoint {parentIdx} from parent name: {parent.name}");
                return checkpointGenerator.GetCheckpoint(parentIdx);
            }

            // Try one more level up
            if (parent.parent != null) {
                Transform grandparent = parent.parent;
                if (grandparent == checkpointGenerator.transform) {
                    // This is a checkpoint under the checkpoint generator
                    if (parent.name.StartsWith("Checkpoint_") && int.TryParse(parent.name.Substring(11), out int gpIdx)) {
                        Debug.Log($"[{gameObject.name}] Found checkpoint {gpIdx} from grandparent hierarchy");
                        return checkpointGenerator.GetCheckpoint(gpIdx);
                    }
                }
            }
        }

        // Try to get checkpoint from CheckpointTrigger component
        CheckpointTrigger trigger = col.GetComponent<CheckpointTrigger>();
        if (trigger != null) {
            int triggerIdx = trigger.GetCheckpointIndex();
            Debug.Log($"[{gameObject.name}] Found checkpoint {triggerIdx} from CheckpointTrigger component");
            return checkpointGenerator.GetCheckpoint(triggerIdx);
        }

        Debug.LogWarning($"[{gameObject.name}] Could not determine checkpoint from collider: {col.gameObject.name}");
        return null;
    }

    // Checkpoint Hit Logic
    private void HandleCheckpointHit(CheckpointGenerator.Checkpoint hitCheckpoint)
    {
        int totalCPs = (checkpointGenerator != null) ? checkpointGenerator.Checkpoints.Count : 0;
        if (totalCPs < 2) {
            Debug.LogWarning($"[{gameObject.name}] HandleCheckpointHit: Not enough checkpoints ({totalCPs})");
            return;
        }

        Debug.Log($"[{gameObject.name}] Hit checkpoint {hitCheckpoint.index}, current target is {currentCheckpointIndex}");

        // Check if this checkpoint is on cooldown (to prevent rapid re-triggering)
        if (checkpointCooldowns.ContainsKey(hitCheckpoint.index)) {
            Debug.Log($"[{gameObject.name}] Checkpoint {hitCheckpoint.index} is on cooldown, ignoring hit");
            return;
        }

        // Be more lenient with checkpoint order - accept checkpoints that are close to the expected one
        bool isCorrectCheckpoint = false;

        // Exact match
        if (hitCheckpoint.index == currentCheckpointIndex) {
            isCorrectCheckpoint = true;
            Debug.Log($"[{gameObject.name}] Hit exact target checkpoint {hitCheckpoint.index}");
        }
        // Next checkpoint (skipping current)
        else if (hitCheckpoint.index == (currentCheckpointIndex + 1) % totalCPs) {
            isCorrectCheckpoint = true;
            Debug.Log($"[{gameObject.name}] Hit next checkpoint {hitCheckpoint.index} (skipping {currentCheckpointIndex})");
        }
        // Previous checkpoint (going backwards)
        else if (hitCheckpoint.index == (currentCheckpointIndex - 1 + totalCPs) % totalCPs) {
            isCorrectCheckpoint = true;
            Debug.Log($"[{gameObject.name}] Hit previous checkpoint {hitCheckpoint.index}");
        }
        // Special case: Wrap around from last to first
        else if (currentCheckpointIndex == totalCPs - 1 && hitCheckpoint.index == 0) {
            isCorrectCheckpoint = true;
            Debug.Log($"[{gameObject.name}] Hit first checkpoint after last one (lap completion)");
        }
        // Special case: Wrap around from first to last
        else if (currentCheckpointIndex == 0 && hitCheckpoint.index == totalCPs - 1) {
            isCorrectCheckpoint = true;
            Debug.Log($"[{gameObject.name}] Hit last checkpoint before first one (going backwards)");
        }
        // Allow for a bit more flexibility - accept checkpoints within a small range
        else if (Mathf.Abs(hitCheckpoint.index - currentCheckpointIndex) <= 2) {
            isCorrectCheckpoint = true;
            Debug.Log($"[{gameObject.name}] Hit nearby checkpoint {hitCheckpoint.index} instead of {currentCheckpointIndex}");
        }

        if (isCorrectCheckpoint)
        {
            // Correct checkpoint hit - give reward and update to next checkpoint
            if (!visitedCheckpointsThisLap.Contains(hitCheckpoint.index)) {
                // Only reward first hit of each checkpoint per lap
                visitedCheckpointsThisLap.Add(hitCheckpoint.index);
                AddReward(checkpointReward);
                Debug.Log($"[{gameObject.name}] Rewarded for checkpoint {hitCheckpoint.index}");
            } else {
                // Checkpoint already hit this lap - reduced reward to prevent exploitation
                Debug.Log($"[{gameObject.name}] Re-hit checkpoint {hitCheckpoint.index} - no additional reward.");
            }

            // Add this checkpoint to cooldown list to prevent rapid re-triggering
            checkpointCooldowns[hitCheckpoint.index] = checkpointCooldownTime;

            // Update current checkpoint to the next one after the hit checkpoint
            currentCheckpointIndex = (hitCheckpoint.index + 1) % totalCPs;
            Debug.Log($"[{gameObject.name}] Updated current checkpoint to {currentCheckpointIndex}");

            // Check if lap is completed - must visit required percentage of checkpoints in order
            int reqCPs = Mathf.Max(1, Mathf.FloorToInt(totalCPs * minCheckpointFractionForLap));
            bool lapDone = (currentCheckpointIndex == 0 && visitedCheckpointsThisLap.Count >= reqCPs);

            if (lapDone) {
                lapsCompleted++;
                float lapT = currentLapTimer;
                if (lapT > 5.0f) {
                    // Give significant reward for completing a lap
                    AddReward(lapCompletionReward);
                    Debug.Log($"[{gameObject.name}] Completed lap {lapsCompleted} in {FormatTime(lapT)}!");

                    // Additional reward for beating best time
                    if (lapT < bestLapTime) {
                        float imp = bestLapTime<float.MaxValue?bestLapTime-lapT:lapT;
                        Debug.Log($"[{gameObject.name}] New Best! {FormatTime(lapT)} (-{FormatTime(imp)})");
                        AddReward(beatBestLapBonus);
                        bestLapTime=lapT;
                    } else if (bestLapTime==float.MaxValue){
                        Debug.Log($"[{gameObject.name}] First Lap! {FormatTime(lapT)}");
                        bestLapTime=lapT;
                    }
                } else {
                    Debug.LogWarning($"[{gameObject.name}] Ignored fast lap ({FormatTime(lapT)}).");
                }
                currentLapTimer = 0f;
                visitedCheckpointsThisLap.Clear();

                // Clear all checkpoint cooldowns when starting a new lap
                checkpointCooldowns.Clear();
            }

            // Update tracking variables for next checkpoint
            CheckpointGenerator.Checkpoint nextCp = checkpointGenerator.GetCheckpoint(currentCheckpointIndex);
            previousDistanceToNextCheckpoint = (nextCp != null) ? Vector3.Distance(transform.position, nextCp.position) : float.MaxValue;
            timeSinceLastCheckpoint = 0f;
            timeSinceLastProgress = 0f;

            // Reset circling detection when making checkpoint progress
            circlingTime = 0f;
        }
        else
        {
            // Wrong checkpoint hit - apply penalty if not off-track
            if (!isCurrentlyOffTrack) {
                int prevIdx = (currentCheckpointIndex - 1 + totalCPs) % totalCPs;
                if (hitCheckpoint.index != prevIdx) {
                    Debug.Log($"[{gameObject.name}] Hit wrong checkpoint {hitCheckpoint.index}, should be {currentCheckpointIndex}");
                    AddReward(wrongCheckpointPenalty);

                    // Add wrong checkpoint to cooldown to prevent repeated penalties
                    checkpointCooldowns[hitCheckpoint.index] = checkpointCooldownTime / 2.0f; // Shorter cooldown for wrong checkpoints
                }
            }
        }
    }
    private void HandleBoundaryHit() { AddReward(boundaryHitPenalty); }

    // Utilities & Gizmos
    private Vector3 GetClosestPointOnLineSegment(Vector3 s, Vector3 e, Vector3 p) { Vector3 sd = e - s; float lSqr = sd.sqrMagnitude; if (lSqr < 1e-8f) return s; float t = Mathf.Clamp01(Vector3.Dot(p - s, sd) / lSqr); return s + t * sd; }
    private string FormatTime(float t) { if (t >= float.MaxValue || t < 0) return "--:--.---"; int m = (int)(t / 60); float s = t % 60; return $"{m:00}:{s:00.000}"; }
    void OnDrawGizmos() { if (showCenterlineGizmo && Application.isPlaying && dependenciesReady && !isCurrentlyOffTrack && closestPointOnCenterline != Vector3.zero) { float d = Vector3.Distance(transform.position, closestPointOnCenterline); float z = centerlineRewardZoneWidth / 2.0f; float l = Mathf.Clamp01(Mathf.InverseLerp(0, z * 1.5f, d)); Gizmos.color = Color.Lerp(Color.green, Color.red, l); Gizmos.DrawLine(transform.position, closestPointOnCenterline); } if (showRecoveryHelpers && Application.isPlaying && isCurrentlyOffTrack) { if (nearestTrackSegmentEnd != Vector3.zero) { Gizmos.color = Color.yellow; Gizmos.DrawLine(nearestTrackSegmentStart, nearestTrackSegmentEnd); Gizmos.DrawSphere(nearestTrackSegmentStart, 0.5f); Gizmos.DrawSphere(nearestTrackSegmentEnd, 0.5f); Gizmos.color = Color.cyan; Vector3 segMid = (nearestTrackSegmentStart + nearestTrackSegmentEnd) / 2f; Vector3 segDir = (nearestTrackSegmentEnd - nearestTrackSegmentStart).normalized; Gizmos.DrawLine(segMid, segMid + segDir * 5f); } Gizmos.color = Color.blue; Gizmos.DrawLine(transform.position, transform.position + transform.forward * 5f); if (nearestTrackPointTarget != Vector3.zero) { Gizmos.color = Color.red; Gizmos.DrawSphere(nearestTrackPointTarget, 0.75f); Gizmos.DrawLine(transform.position, nearestTrackPointTarget); } } }
    private static class LayerMaskHelper { public static int GetLayerIndexFromMask(LayerMask lm) { int v = lm.value, i = -1, c = 0; while (v > 0 && c < 32) { if ((v & 1) == 1) { if (i != -1) return -2; i = c; } v >>= 1; c++; } return i; } }
}
// --- END OF FILE RacingAgent.cs ---