using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Manages race state, UI, and visualization elements like racing lines and ghost cars.
/// </summary>
public class RaceManager : MonoBehaviour
{
    [Header("Race Settings")]
    [SerializeField] private int totalLaps = 3;
    [SerializeField] private float offTrackRecoveryTime = 3f;
    [SerializeField] private float offTrackDetectionDelay = 5f; // Delay before off-track detection starts working (increased from 3f to 5f)
    [SerializeField] private float spawnProtectionTime = 10f; // Time after spawn during which respawning is disabled (increased from 5f to 10f)

    [Tooltip("Reference to the StartFinishLine component.")]
    public StartFinishLine startFinishLine;

    [Header("Environment Setup")]
    [Tooltip("Reference to the AI vehicle's transform.")]
    public Transform aiVehicle;
    [Tooltip("Reference to the Player vehicle's transform.")]
    public Transform playerVehicle;

    [Header("Track Components Dependencies")]
    [Tooltip("Reference to the TrackBoundaryDetector component.")]
    public TrackBoundaryDetector boundaryDetector;
    [Tooltip("Reference to the CheckpointGenerator component.")]
    public CheckpointGenerator checkpointGenerator;

    [Header("UI References")]
    [SerializeField] private RaceUI raceUI;
    [SerializeField] private RaceStartManager raceStartManager;
    public TextMeshProUGUI bestLapText;

    // Optional material for the line

    [Header("Ghost Car")]
    public bool enableGhostCar = true;

    [Tooltip("Prefab for the ghost car visualization.")]
    public GameObject ghostCarPrefab;

    [Range(0.1f, 0.8f)]
    public float ghostTransparency = 0.4f;

    // Optional UI Panel
    public TextMeshProUGUI lapCounterText;

    public TextMeshProUGUI lapTimeText;

    // This is now defined in the Environment Setup section

    public TextMeshProUGUI positionText;

    [Header("Race UI")]
    public GameObject raceInfoPanel;

    public Color racingLineColor = new Color(0, 1, 0, 0.5f);
    public Material racingLineMaterial;
    public float racingLineWidth = 0.3f;

    // For player vs AI position

    [Header("Visualization")]
    public bool showRacingLine = true;

    // --- References ---
    private RacingAgent aiAgent;

    // --- Ghost Car ---
    private List<GhostCarFrame> bestLapGhostData = new List<GhostCarFrame>();

    // --- Race State ---
    private float bestLapTimeAI = float.MaxValue;
    private float bestLapTimePlayer = float.MaxValue;
    private int currentCheckpointAI = 0;
    private int currentCheckpointPlayer = 0;
    private int currentLapAI = 1;
    private int currentLapPlayer = 1;
    private List<GhostCarFrame> currentLapRecording = new List<GhostCarFrame>();
    private float currentLapTimeAI = 0f;
    private float currentLapTimePlayer = 0f;
    private float playerTotalTime = 0f;
    private float aiTotalTime = 0f;
    private List<float> playerLapTimes = new List<float>();
    private List<float> aiLapTimes = new List<float>();
    private bool playerIsOffTrack = false;
    private float playerOffTrackTime = 0f;

    // Public properties
    public bool IsRaceActive => raceActive;
    public int TotalLaps => totalLaps;
    public int CurrentLapPlayer => currentLapPlayer;
    public float CurrentLapTimePlayer => currentLapTimePlayer;
    public float BestLapTimePlayer => bestLapTimePlayer;
    public int CurrentLapAI => currentLapAI;
    public float CurrentLapTimeAI => currentLapTimeAI;
    public float BestLapTimeAI => bestLapTimeAI;
    public float PlayerTotalTime => playerTotalTime;
    public float AITotalTime => aiTotalTime;
    public List<float> PlayerLapTimes => playerLapTimes;
    public List<float> AILapTimes => aiLapTimes;
    public bool IsPlayerAhead => DetermineRacePosition();

    private bool dependenciesReady = false;
    private GameObject ghostCarInstance;
    private int ghostPlaybackIndex = 0;
    private float ghostRecordInterval = 0.1f;
    private bool isRecordingGhost = false;

    // Record position X times per second
    private float lastGhostRecordTime = 0f;

    // Track AI's progress

    private bool raceActive = false;
    private float raceActiveTime = 0f; // Time since race started

    // --- Visualization ---
    private LineRenderer racingLineRenderer;

    // Assuming one primary AI for now
    // Add player controller reference if needed


    [System.Serializable]
    public class GhostCarFrame
    {
        public Vector3 position;
        public Quaternion rotation;
        public float timeStamp;
    }

    /// <summary>
    /// Checks if the player is off track and handles recovery.
    /// </summary>
    private void CheckPlayerOffTrack()
    {
        if (playerVehicle == null || boundaryDetector == null)
            return;

        // Don't check for off-track during spawn protection period
        if (raceActiveTime < spawnProtectionTime)
        {
            if (playerIsOffTrack)
            {
                // Reset off-track state if we were off-track
                playerIsOffTrack = false;
                playerOffTrackTime = 0f;
                Debug.Log("[RaceManager] Spawn protection active - ignoring off-track state.");
            }
            return;
        }

        // Only check every few frames to reduce performance impact and log spam
        if (Time.frameCount % 5 != 0)
            return;

        // Check if player is off track
        bool isOffTrack = IsVehicleOffTrack(playerVehicle);

        if (isOffTrack)
        {
            if (!playerIsOffTrack)
            {
                // Player just went off track
                playerIsOffTrack = true;
                playerOffTrackTime = 0f;

                Debug.Log("[RaceManager] Player went off track.");
            }
            else
            {
                // Player is still off track
                playerOffTrackTime += Time.deltaTime;

                // Debug log to show off-track timer
                if (Mathf.FloorToInt(playerOffTrackTime) != Mathf.FloorToInt(playerOffTrackTime - Time.deltaTime))
                {
                    Debug.Log($"[RaceManager] Player off track for {playerOffTrackTime:F1} seconds. Will reset at {offTrackRecoveryTime} seconds.");
                }

                // Check if player should be reset
                if (playerOffTrackTime >= offTrackRecoveryTime)
                {
                    Debug.Log("[RaceManager] Player off track for too long. Resetting to last checkpoint.");
                    ResetPlayerToTrack();
                }
            }
        }
        else
        {
            // Player is on track
            playerIsOffTrack = false;
            playerOffTrackTime = 0f;
        }
    }

    /// <summary>
    /// Checks if a vehicle is off track.
    /// </summary>
    private bool IsVehicleOffTrack(Transform vehicle)
    {
        if (vehicle == null || boundaryDetector == null)
            return false;

        // Use the TrackBoundaryDetector to check if the vehicle is off track
        if (boundaryDetector.HasScanned)
        {
            // Get the vehicle's position
            Vector3 vehiclePosition = vehicle.position;

            // Method 1: Check if the vehicle is on the track layer using multiple raycasts
            bool onTrackSurface = false;

            // Cast multiple rays from different points on the vehicle
            Vector3[] raycastOrigins = new Vector3[5];
            raycastOrigins[0] = vehiclePosition + Vector3.up * 0.5f; // Center
            raycastOrigins[1] = vehiclePosition + Vector3.up * 0.5f + vehicle.forward * 1.0f; // Front
            raycastOrigins[2] = vehiclePosition + Vector3.up * 0.5f - vehicle.forward * 1.0f; // Back
            raycastOrigins[3] = vehiclePosition + Vector3.up * 0.5f + vehicle.right * 0.5f; // Right
            raycastOrigins[4] = vehiclePosition + Vector3.up * 0.5f - vehicle.right * 0.5f; // Left

            // Check if any of the rays hit the track
            foreach (Vector3 origin in raycastOrigins)
            {
                Ray downRay = new Ray(origin, Vector3.down);
                RaycastHit hit;
                if (Physics.Raycast(downRay, out hit, 2f, boundaryDetector.trackLayer))
                {
                    onTrackSurface = true;
                    break;
                }
            }

            // If at least one wheel is on the track, consider the vehicle on track
            if (onTrackSurface)
            {
                return false; // Vehicle is on track
            }

            // Method 2: Check if the vehicle is inside the track boundaries
            // Get the closest boundary points
            Vector3 closestInnerPoint, closestOuterPoint;
            bool foundBoundaryPoints = boundaryDetector.GetClosestBoundaryPoints(vehiclePosition, out closestInnerPoint, out closestOuterPoint, 50f);

            // If we found boundary points, check if the vehicle is inside the track
            if (foundBoundaryPoints)
            {
                // Calculate the distance to the closest boundary point
                float distanceToInner = Vector3.Distance(vehiclePosition, closestInnerPoint);
                float distanceToOuter = Vector3.Distance(vehiclePosition, closestOuterPoint);

                // If the vehicle is closer to the outer boundary than the inner boundary,
                // it might be outside the track, but we need to be more lenient

                // Calculate the ratio of distances to determine how far outside the track we are
                float boundaryRatio = distanceToInner / (distanceToInner + distanceToOuter);

                // Only consider off-track if the ratio is very skewed (vehicle is clearly outside)
                // A value of 0.25 means the vehicle is much closer to the outer boundary (more lenient than before)
                bool outsideBoundaries = boundaryRatio < 0.25f; // Reduced from 0.3f to 0.25f to be more lenient

                // If we're outside boundaries, double-check with a more lenient raycast
                if (outsideBoundaries)
                {
                    // Cast a wider net of rays to be sure
                    for (int i = 0; i < 12; i++) // Increased from 8 to 12 rays for better coverage
                    {
                        float angle = i * 30f * Mathf.Deg2Rad; // 30 degrees instead of 45
                        Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                        Vector3 origin = vehiclePosition + Vector3.up * 0.5f + dir * 2.0f; // Increased radius

                        Ray downRay = new Ray(origin, Vector3.down);
                        RaycastHit hit;
                        if (Physics.Raycast(downRay, out hit, 2f, boundaryDetector.trackLayer))
                        {
                            // Found track surface with extended check
                            return false;
                        }
                    }

                    // One more check - if we're moving fast, be more lenient
                    Rigidbody rb = vehicle.GetComponent<Rigidbody>();
                    if (rb != null && rb.linearVelocity.magnitude > 8f) // If moving fast (reduced from 10f to 8f)
                    {
                        // Give a bit more leeway for high-speed vehicles
                        return boundaryRatio < 0.15f; // Even more lenient threshold (reduced from 0.2f to 0.15f)
                    }

                    Debug.Log($"[RaceManager] Vehicle is off track! BoundaryRatio: {boundaryRatio:F2}");
                    return true;
                }

                return false; // Inside boundaries
            }

            // If we couldn't find boundary points, fall back to just the raycast method
            Debug.Log("[RaceManager] Vehicle is off track! (Raycast method only)");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resets the player vehicle to the last checkpoint or starting position.
    /// </summary>
    private void ResetPlayerToTrack()
    {
        if (playerVehicle == null || checkpointGenerator == null)
            return;

        Debug.Log("[RaceManager] Resetting player to track.");

        // Check if the player has passed any checkpoints yet
        if (currentCheckpointPlayer == 0) // If at the start of the race or haven't passed any checkpoints
        {
            Debug.Log("[RaceManager] Player hasn't passed any checkpoints yet. Finding closest spawn point.");

            // Find the closest spawn point
            Transform closestSpawn = null;
            float closestDistance = float.MaxValue;

            // Try to find spawn points from the checkpoint generator
            if (checkpointGenerator.GeneratedSpawnPoints != null && checkpointGenerator.GeneratedSpawnPoints.Count > 0)
            {
                foreach (Transform spawnPoint in checkpointGenerator.GeneratedSpawnPoints)
                {
                    if (spawnPoint == null) continue;

                    float distance = Vector3.Distance(playerVehicle.position, spawnPoint.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestSpawn = spawnPoint;
                    }
                }

                if (closestSpawn != null)
                {
                    // Reset player to the closest spawn point with increased height
                    playerVehicle.position = closestSpawn.position + Vector3.up * 1.0f;
                    playerVehicle.rotation = closestSpawn.rotation;

                    Debug.Log($"[RaceManager] Reset player to spawn point: {playerVehicle.position}, rotation: {playerVehicle.rotation.eulerAngles}");

                    // Reset physics
                    Rigidbody rb = playerVehicle.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                        rb.isKinematic = true; // Temporarily make kinematic
                        StartCoroutine(ReenablePhysics(rb)); // Re-enable physics after a short delay
                    }

                    return;
                }
            }

            // If we couldn't find a spawn point, try to use checkpoint 0 as a fallback
            CheckpointGenerator.Checkpoint startCheckpoint = checkpointGenerator.GetCheckpoint(0);
            if (startCheckpoint != null)
            {
                // Reset player to the start checkpoint
                playerVehicle.position = startCheckpoint.position + Vector3.up * 1.0f;
                playerVehicle.rotation = startCheckpoint.rotation;

                Debug.Log($"[RaceManager] Reset player to start checkpoint: {playerVehicle.position}, rotation: {playerVehicle.rotation.eulerAngles}");

                // Reset physics
                Rigidbody rb = playerVehicle.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true; // Temporarily make kinematic
                    StartCoroutine(ReenablePhysics(rb)); // Re-enable physics after a short delay
                }

                return;
            }

            // If all else fails, just reset physics without changing position
            Debug.Log("[RaceManager] Couldn't find a suitable respawn point. Just resetting physics.");
            Rigidbody rb2 = playerVehicle.GetComponent<Rigidbody>();
            if (rb2 != null)
            {
                rb2.linearVelocity = Vector3.zero;
                rb2.angularVelocity = Vector3.zero;
                rb2.isKinematic = true; // Temporarily make kinematic
                StartCoroutine(ReenablePhysics(rb2)); // Re-enable physics after a short delay
            }

            return;
        }

        // Find the last checkpoint the player passed
        int lastCheckpointIndex = currentCheckpointPlayer > 0 ?
            currentCheckpointPlayer - 1 :
            checkpointGenerator.Checkpoints.Count - 1;

        // Get the checkpoint
        CheckpointGenerator.Checkpoint lastCheckpoint = checkpointGenerator.GetCheckpoint(lastCheckpointIndex);

        if (lastCheckpoint != null)
        {
            // Reset player to the last checkpoint with increased height offset
            playerVehicle.position = lastCheckpoint.position + Vector3.up * 1.0f; // Increased height offset to prevent ground collision issues
            playerVehicle.rotation = lastCheckpoint.rotation;

            Debug.Log($"[RaceManager] Reset player to position: {playerVehicle.position}, rotation: {playerVehicle.rotation.eulerAngles}");

            // Reset physics
            Rigidbody rb = playerVehicle.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Temporarily make kinematic to ensure position is set correctly
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // Re-enable physics after a short delay
                StartCoroutine(ReenablePhysics(rb));
            }

            Debug.Log($"[RaceManager] Player reset to checkpoint {lastCheckpointIndex}");
        }
    }

    /// <summary>
    /// Re-enables physics on a rigidbody after a short delay.
    /// </summary>
    private IEnumerator ReenablePhysics(Rigidbody rb)
    {
        yield return new WaitForSeconds(0.5f);
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
            Debug.Log("[RaceManager] Re-enabled physics on player vehicle.");
        }
    }

    /// <summary>
    /// Handles logic when the AI completes a lap.
    /// </summary>
    public void HandleAILapCompletion() // Called from StartFinishLine or AI agent logic
    {
        if (!raceActive) return;

        // If this is the first crossing of the start line, don't count it as a lap
        if (currentLapAI == 1 && currentLapTimeAI < 10f)
        {
            Debug.Log("[RaceManager] Ignoring initial AI start line crossing");
            return;
        }

        Debug.Log($"[RaceManager] AI completed Lap {currentLapAI} in {FormatTime(currentLapTimeAI)}");

        // Record lap time
        aiLapTimes.Add(currentLapTimeAI);

        // Check for best lap
        if (currentLapTimeAI < bestLapTimeAI)
        {
            bestLapTimeAI = currentLapTimeAI;
            Debug.Log($"[RaceManager] New AI Best Lap: {FormatTime(bestLapTimeAI)}");
        }

        // Reset for next lap
        currentLapAI++;
        currentLapTimeAI = 0f;

        // No need to reset checkpoint index with the new system
        // currentCheckpointAI = 0;

        // Update UI immediately
        UpdateUI();

        // Check if race is complete
        if (currentLapAI > totalLaps)
        {
            Debug.Log($"[RaceManager] AI completed all {totalLaps} laps! Finishing race.");
            FinishRace(false); // AI won
        }
    }

    /// <summary>
    /// Handles player checkpoint triggers.
    /// </summary>
    public void OnPlayerCheckpointTriggered(int checkpointIndex)
    {
        if (!raceActive || checkpointGenerator == null)
            return;

        Debug.Log($"[RaceManager] Player triggered checkpoint {checkpointIndex}. Current checkpoint: {currentCheckpointPlayer}");

        // Get total number of checkpoints
        int checkpointCount = checkpointGenerator.Checkpoints.Count;
        if (checkpointCount == 0) return; // Avoid division by zero if no checkpoints

        // Calculate the expected next checkpoint
        int expectedNext = (currentCheckpointPlayer + 1) % checkpointCount;

        // Special case for the first checkpoint trigger of the race
        if (currentLapPlayer == 1 && currentCheckpointPlayer == 0 && checkpointIndex > 0 && checkpointIndex < 3)
        {
            // Player just started and hit one of the first few checkpoints - accept it
            Debug.Log($"[RaceManager] Player hit first checkpoint {checkpointIndex} of the race.");
            currentCheckpointPlayer = checkpointIndex;
            return;
        }

        // Check for lap completion (checkpoint 0 is the start/finish line)
        if (checkpointIndex == 0)
        {
            // Only count as lap completion if player has passed at least 50% of checkpoints
            // Reduced from 75% to 50% to make it more lenient
            int minCheckpointsForLap = Mathf.Max(1, Mathf.FloorToInt(checkpointCount * 0.5f));

            // If player is at the last checkpoint or has passed enough checkpoints
            if (currentCheckpointPlayer >= minCheckpointsForLap || currentCheckpointPlayer == checkpointCount - 1)
            {
                Debug.Log($"[RaceManager] Player completed lap by reaching checkpoint 0 from checkpoint {currentCheckpointPlayer}");
                HandlePlayerLapCompletion();
                currentCheckpointPlayer = 0; // Reset player checkpoint index
                return;
            }
            else
            {
                // If player hasn't passed enough checkpoints, but this is the first lap
                // and they've been racing for a while, let's count it anyway
                if (currentLapPlayer == 1 && raceActiveTime > 30f)
                {
                    Debug.Log($"[RaceManager] First lap special case: Player completed lap by reaching checkpoint 0 despite only passing {currentCheckpointPlayer} checkpoints");
                    HandlePlayerLapCompletion();
                    currentCheckpointPlayer = 0; // Reset player checkpoint index
                    return;
                }

                Debug.Log($"[RaceManager] Player reached checkpoint 0 but has only passed {currentCheckpointPlayer}/{minCheckpointsForLap} required checkpoints. Not counting as lap completion.");
            }
        }

        // Normal checkpoint progression
        if (checkpointIndex == expectedNext)
        {
            // Player passed the correct next checkpoint
            currentCheckpointPlayer = checkpointIndex;
            Debug.Log($"[RaceManager] Player advanced to checkpoint {currentCheckpointPlayer}");
        }
        else if (Mathf.Abs(checkpointIndex - expectedNext) <= 1 ||
                 (expectedNext == 0 && checkpointIndex == checkpointCount - 1) ||
                 (checkpointIndex == 0 && expectedNext == checkpointCount - 1))
        {
            // Allow for some flexibility - accept checkpoints that are off by 1
            // or when wrapping around the track (last to first or first to last)
            Debug.Log($"[RaceManager] Player hit nearby checkpoint {checkpointIndex} instead of {expectedNext}. Accepting it.");
            currentCheckpointPlayer = checkpointIndex;
        }
        else
        {
            // Player triggered an unexpected checkpoint
            Debug.LogWarning($"[RaceManager] Player triggered unexpected checkpoint {checkpointIndex}. Expected {expectedNext}.");
        }
    }

    /// <summary>
    /// Handles logic when the player completes a lap.
    /// </summary>
    public void HandlePlayerLapCompletion() // Called from StartFinishLine or checkpoint logic
    {
        if (!raceActive)
        {
            Debug.LogWarning("[RaceManager] HandlePlayerLapCompletion called but race is not active!");
            return;
        }

        // If this is the first crossing of the start line, don't count it as a lap
        if (currentLapPlayer == 1 && currentLapTimePlayer < 10f)
        {
            Debug.Log("[RaceManager] Ignoring initial start line crossing");
            return;
        }

        Debug.Log($"[RaceManager] Player completed Lap {currentLapPlayer} in {FormatTime(currentLapTimePlayer)}");

        // Record lap time
        playerLapTimes.Add(currentLapTimePlayer);
        Debug.Log($"[RaceManager] Player lap times: {string.Join(", ", playerLapTimes.Select(t => FormatTime(t)))}");

        // Check for best lap
        if (currentLapTimePlayer < bestLapTimePlayer)
        {
            bestLapTimePlayer = currentLapTimePlayer;
            Debug.Log($"[RaceManager] New Player Best Lap: {FormatTime(bestLapTimePlayer)}");

            // Save ghost data
            if (isRecordingGhost && currentLapRecording.Count > 10) // Need minimum frames
            {
                bestLapGhostData = new List<GhostCarFrame>(currentLapRecording);
                Debug.Log($"[RaceManager] Saved best lap ghost data with {bestLapGhostData.Count} frames.");
                // Activate ghost car if it wasn't already
                if (ghostCarInstance != null && !ghostCarInstance.activeSelf) ghostCarInstance.SetActive(true);
            }
        }

        // Reset for next lap
        currentLapPlayer++;
        Debug.Log($"[RaceManager] Player advancing to lap {currentLapPlayer}/{totalLaps}");
        currentLapTimePlayer = 0f;
        currentLapRecording.Clear();
        ghostPlaybackIndex = 0; // Reset ghost playback for new lap
        lastGhostRecordTime = -ghostRecordInterval; // Ensure recording starts immediately

        // No need to reset checkpoint index with the new system
        // currentCheckpointPlayer = 0;

        // Update UI immediately to show new lap
        UpdateUI();

        // Check if race is complete
        if (currentLapPlayer > totalLaps)
        {
            Debug.Log($"[RaceManager] Player completed all {totalLaps} laps! Finishing race.");
            FinishRace(true); // Player won
        }
    }

    public void StartRace()
    {
        if (!dependenciesReady)
        {
            Debug.LogWarning("[RaceManager] Cannot start race, dependencies not ready.");
            return;
        }
        if (raceActive)
        {
            Debug.LogWarning("[RaceManager] Race already active.");
            return;
        }

        Debug.Log("[RaceManager] Starting Race!");
        raceActive = true;
        raceActiveTime = 0f; // Reset race active time

        // Reset race state
        currentLapTimePlayer = 0f;
        currentLapPlayer = 1; // Start on lap 1
        currentCheckpointPlayer = 0;
        playerLapTimes = new List<float>(); // Initialize lap times list

        currentLapTimeAI = 0f;
        currentLapAI = 1;
        // Assume AI also starts near checkpoint 0
        currentCheckpointAI = (aiAgent != null) ? GetAgentCurrentCheckpointIndex(aiAgent) : 0; // Try to get actual start
        aiLapTimes = new List<float>(); // Initialize lap times list

        // Start Ghost Recording for player
        if (playerVehicle != null && enableGhostCar)
        {
            isRecordingGhost = true;
            currentLapRecording.Clear();
            lastGhostRecordTime = -ghostRecordInterval; // Ensure first frame records immediately
        }

        // Reset Ghost Playback
        ghostPlaybackIndex = 0;
        if (ghostCarInstance != null) ghostCarInstance.SetActive(bestLapGhostData.Count > 0);

        if (raceInfoPanel != null) raceInfoPanel.SetActive(true);
        UpdateUI();
    }

    /// <summary>
    /// Formats time in seconds to mm:ss.fff format.
    /// </summary>
    private string FormatTime(float timeInSeconds)
    {
        if (timeInSeconds >= float.MaxValue || timeInSeconds < 0) return "--:--.---";
        int minutes = (int)(timeInSeconds / 60);
        float seconds = timeInSeconds % 60;
        // Use "F3" for milliseconds format
        return $"{minutes:00}:{seconds:00.000}";
    }

    // *** CORRECTION: Add public getter in RacingAgent or use event ***
    // Placeholder - Replace with proper access method/event later
    private int GetAgentCurrentCheckpointIndex(RacingAgent agent)
    {
        // Option 1: Add public property to RacingAgent (simplest for now)
        // public int CurrentCheckpointIndex => currentCheckpointIndex;
        // return agent.CurrentCheckpointIndex;

        // Option 2: Use reflection (slower, less ideal)
        try
        {
            var field = typeof(RacingAgent).GetField("currentCheckpointIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) return (int)field.GetValue(agent);
        }
        catch { }
        return 0; // Fallback
    }

    /// <summary>
    /// Creates and configures the ghost car instance.
    /// </summary>
    private void InitializeGhostCarVisual()
    {
        if (ghostCarInstance != null) Destroy(ghostCarInstance);

        ghostCarInstance = Instantiate(ghostCarPrefab, Vector3.zero, Quaternion.identity);
        ghostCarInstance.name = "GhostCarVisual";
        ghostCarInstance.transform.SetParent(this.transform);
        ghostCarInstance.SetActive(false); // Hide until best lap data exists and race starts

        // Make ghost transparent
        var renderers = ghostCarInstance.GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            foreach (var mat in rend.materials) // Iterate through all materials
            {
                // Attempt to change material rendering mode to Transparent
                // This works for Standard shader and URP/Lit. May need adjustment for other shaders.
                try
                {
                    mat.SetFloat("_Mode", 3); // Standard Shader: 3=Transparent
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    Color col = mat.color;
                    col.a = ghostTransparency;
                    mat.color = col;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[RaceManager] Could not set transparency on ghost material '{mat.name}': {e.Message}");
                }
            }
        }
        Debug.Log("[RaceManager] Ghost car visual initialized.");
    }

    private IEnumerator InitializeRaceManager()
    {
        Debug.Log("[RaceManager] Initializing...");
        dependenciesReady = false;

        // Wait for track systems
        if (checkpointGenerator == null || boundaryDetector == null)
        {
            Debug.LogError("[RaceManager] Missing CheckpointGenerator or TrackBoundaryDetector reference!", this);
            yield break;
        }
        // *** CORRECTION: Use correct property names ***
        while (!checkpointGenerator.IsInitialized || !boundaryDetector.HasScanned)
        {
            Debug.Log("[RaceManager] Waiting for track dependencies...");
            yield return new WaitForSeconds(0.5f);
        }
        Debug.Log("[RaceManager] Track dependencies ready.");

        // Setup visualizations
        // *** CORRECTION: Check Checkpoints list count ***
        if (showRacingLine && checkpointGenerator.Checkpoints.Count > 0)
        {
            SetupRacingLineVisual();
        }
        if (enableGhostCar && ghostCarPrefab != null)
        {
            InitializeGhostCarVisual();
        }

        // Subscribe to agent events (if agent provides them)
        // e.g., aiAgent.OnLapCompleted += HandleAILapCompletion;
        // e.g., playerController.OnLapCompleted += HandlePlayerLapCompletion;

        dependenciesReady = true;
        Debug.Log("[RaceManager] Initialization complete. Ready to start race.");

        // Optionally auto-start the race after setup
        yield return new WaitForSeconds(1f); // Short delay before starting
        StartRace();
    }

    /// <summary>
    /// Records the player vehicle's current transform for ghost playback.
    /// </summary>
    private void RecordGhostFrame()
    {
        if (playerVehicle == null) return;
        currentLapRecording.Add(new GhostCarFrame
        {
            timeStamp = currentLapTimePlayer,
            position = playerVehicle.position,
            rotation = playerVehicle.rotation
        });
    }

    /// <summary>
    /// Sets up the LineRenderer for the racing line visualization.
    /// </summary>
    private void SetupRacingLineVisual()
    {
        // *** CORRECTION: Use Checkpoints property ***
        if (checkpointGenerator == null || checkpointGenerator.Checkpoints.Count < 2) return;

        if (racingLineRenderer == null)
        {
            GameObject lineObj = new GameObject("RacingLineVisual");
            lineObj.transform.SetParent(this.transform);
            racingLineRenderer = lineObj.AddComponent<LineRenderer>();

            racingLineRenderer.startWidth = racingLineWidth;
            racingLineRenderer.endWidth = racingLineWidth;
            racingLineRenderer.useWorldSpace = true;
            racingLineRenderer.loop = true; // Connects last point to first
            // *** CORRECTION: Use Checkpoints property ***
            racingLineRenderer.positionCount = checkpointGenerator.Checkpoints.Count;
            racingLineRenderer.material = racingLineMaterial != null ? racingLineMaterial : new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply")); // Simple transparent material
            racingLineRenderer.startColor = racingLineColor;
            racingLineRenderer.endColor = racingLineColor;
            racingLineRenderer.sortingOrder = -1; // Render behind cars
        }
        else
        {
            // Update existing renderer if checkpoint count changed (e.g., regeneration)
            racingLineRenderer.positionCount = checkpointGenerator.Checkpoints.Count;
        }

        // Set positions based on checkpoints
        // *** CORRECTION: Use Checkpoints property ***
        for (int i = 0; i < checkpointGenerator.Checkpoints.Count; i++)
        {
            // Add slight vertical offset to prevent z-fighting with track
            racingLineRenderer.SetPosition(i, checkpointGenerator.Checkpoints[i].position + Vector3.up * 0.1f);
        }
        Debug.Log("[RaceManager] Racing line visual updated.");
    }

    void Start()
    {
        // Find components if not assigned
        if (boundaryDetector == null) boundaryDetector = FindObjectOfType<TrackBoundaryDetector>();
        if (checkpointGenerator == null) checkpointGenerator = FindObjectOfType<CheckpointGenerator>();
        if (startFinishLine == null) startFinishLine = FindObjectOfType<StartFinishLine>();

        if (aiVehicle != null) aiAgent = aiVehicle.GetComponent<RacingAgent>();
        // Find player components if needed

        if (raceInfoPanel != null) raceInfoPanel.SetActive(false); // Hide UI initially

        // Connect to StartFinishLine events if available
        if (startFinishLine != null)
        {
            Debug.Log("[RaceManager] Connecting to StartFinishLine events.");
            startFinishLine.OnPlayerCrossed.AddListener(HandlePlayerLapCompletion);
            startFinishLine.OnAICrossed.AddListener(HandleAILapCompletion);
        }
        else
        {
            Debug.LogWarning("[RaceManager] StartFinishLine reference is missing! Lap counting will use checkpoint system instead.");
        }

        StartCoroutine(InitializeRaceManager());
    }

    void Update()
    {
        if (!raceActive || !dependenciesReady) return;

        // Update Timers
        currentLapTimePlayer += Time.deltaTime;
        currentLapTimeAI += Time.deltaTime;
        playerTotalTime += Time.deltaTime;
        aiTotalTime += Time.deltaTime;
        raceActiveTime += Time.deltaTime;

        // Check if player is off track (only after delay)
        if (raceActiveTime >= offTrackDetectionDelay)
        {
            CheckPlayerOffTrack();
        }

        // Update Ghost Car Recording (Player)
        if (isRecordingGhost && playerVehicle != null && Time.time >= lastGhostRecordTime + ghostRecordInterval)
        {
            RecordGhostFrame();
            lastGhostRecordTime = Time.time;
        }

        // Update Ghost Car Playback
        if (ghostCarInstance != null && ghostCarInstance.activeSelf && bestLapGhostData.Count > 1)
        {
            UpdateGhostPlayback();
        }

        // Update Checkpoint Progress (Example for AI - needs better integration)
        if (aiAgent != null)
        {
            int agentReportedIndex = GetAgentCurrentCheckpointIndex(aiAgent); // Get current index (needs proper method)

            if (agentReportedIndex != currentCheckpointAI)
            { // If index changed since last check
              // Check if it's the expected next checkpoint OR a lap completion
              // *** CORRECTION: Use Checkpoints property ***
                int checkpointCount = checkpointGenerator.Checkpoints.Count;
                if (checkpointCount == 0) return; // Avoid division by zero if no checkpoints

                int expectedNext = (currentCheckpointAI + 1) % checkpointCount;

                if (agentReportedIndex == expectedNext)
                {
                    // Correct checkpoint passed by AI
                    // Debug.Log($"[RaceManager] AI passed CP {currentCheckpointAI}, now at {agentReportedIndex}");
                    currentCheckpointAI = agentReportedIndex;
                    // Add split time logic here if needed
                }
                else if (agentReportedIndex == 0 && currentCheckpointAI == checkpointCount - 1)
                {
                    // AI completed a lap (went from last CP to CP 0)
                    HandleAILapCompletion();
                    currentCheckpointAI = 0; // Reset AI checkpoint index
                }
                else
                {
                    // AI index changed unexpectedly (e.g., reset, wrong order).
                    // May need more robust state tracking or just update to agent's current state.
                    // For now, let's just sync if it seems like a reset (index < current)
                    if (agentReportedIndex < currentCheckpointAI && currentCheckpointAI != 0)
                    {
                        // Debug.Log($"[RaceManager] AI index jumped back ({currentCheckpointAI} -> {agentReportedIndex}), likely reset. Syncing.");
                        currentCheckpointAI = agentReportedIndex; // Sync to agent's state
                                                                  // Reset AI lap timer if needed? Might be complex.
                    }
                    else
                    {
                        // Debug.LogWarning($"[RaceManager] AI index mismatch. Current: {currentCheckpointAI}, Agent reports: {agentReportedIndex}, Expected next: {expectedNext}");
                        // Optionally force sync: currentCheckpointAI = agentReportedIndex;
                    }
                }
            }
        }
        // Add similar logic for player if applicable

        // Update UI periodically
        UpdateUI();
    }

    /// <summary>
    /// Updates the ghost car's position based on the recorded best lap data.
    /// </summary>
    private void UpdateGhostPlayback()
    {
        // Find the frame corresponding to the current player lap time
        while (ghostPlaybackIndex < bestLapGhostData.Count - 1 &&
               bestLapGhostData[ghostPlaybackIndex + 1].timeStamp <= currentLapTimePlayer)
        {
            ghostPlaybackIndex++;
        }

        // If time is before the first frame or after the last, place at start/end
        if (currentLapTimePlayer <= bestLapGhostData[0].timeStamp)
        {
            ghostCarInstance.transform.SetPositionAndRotation(bestLapGhostData[0].position, bestLapGhostData[0].rotation);
            ghostPlaybackIndex = 0;
            return;
        }
        if (ghostPlaybackIndex >= bestLapGhostData.Count - 1)
        {
            ghostCarInstance.transform.SetPositionAndRotation(bestLapGhostData.Last().position, bestLapGhostData.Last().rotation);
            // Optionally loop ghost playback by resetting time/index?
            return;
        }


        // Interpolate between the current and next frame
        GhostCarFrame currentFrame = bestLapGhostData[ghostPlaybackIndex];
        GhostCarFrame nextFrame = bestLapGhostData[ghostPlaybackIndex + 1];

        float segmentDuration = nextFrame.timeStamp - currentFrame.timeStamp;
        float timeIntoSegment = currentLapTimePlayer - currentFrame.timeStamp;

        // Avoid division by zero or tiny segment durations
        float interpolationFactor = (segmentDuration > 0.001f) ? Mathf.Clamp01(timeIntoSegment / segmentDuration) : 1f;


        ghostCarInstance.transform.position = Vector3.LerpUnclamped(currentFrame.position, nextFrame.position, interpolationFactor); // Use LerpUnclamped for smoother extrapolation if needed, but Clamp01 on factor prevents this mostly
        ghostCarInstance.transform.rotation = Quaternion.SlerpUnclamped(currentFrame.rotation, nextFrame.rotation, interpolationFactor); // Use SlerpUnclamped
    }

    /// <summary>
    /// Finishes the race and declares a winner.
    /// </summary>
    private void FinishRace(bool playerWon)
    {
        if (!raceActive)
            return;

        Debug.Log($"[RaceManager] Race finished! {(playerWon ? "Player" : "AI")} won!");

        raceActive = false;

        // Show race results
        if (raceUI != null)
        {
            raceUI.ShowRaceResults(playerWon);
        }

        // Notify race start manager
        if (raceStartManager != null)
        {
            raceStartManager.FinishRace(playerWon);
        }
    }

    /// <summary>
    /// Determines the race position (player ahead or behind).
    /// </summary>
    private bool DetermineRacePosition()
    {
        // Debug log to help diagnose position issues - only log occasionally
        if (Time.frameCount % 60 == 0) // Log once per second at 60 FPS
        {
            Debug.Log($"[RaceManager] Position check - Player Lap: {currentLapPlayer}, AI Lap: {currentLapAI}, Player CP: {currentCheckpointPlayer}, AI CP: {currentCheckpointAI}");
        }

        // Compare player and AI progress
        if (currentLapPlayer > currentLapAI)
        {
            Debug.Log("[RaceManager] Player ahead (higher lap)");
            return true; // Player is ahead
        }
        else if (currentLapAI > currentLapPlayer)
        {
            Debug.Log("[RaceManager] AI ahead (higher lap)");
            return false; // AI is ahead
        }
        else
        {
            // Same lap, compare checkpoints
            if (currentCheckpointPlayer > currentCheckpointAI)
            {
                Debug.Log("[RaceManager] Player ahead (higher checkpoint)");
                return true; // Player is ahead
            }
            else if (currentCheckpointAI > currentCheckpointPlayer)
            {
                Debug.Log("[RaceManager] AI ahead (higher checkpoint)");
                return false; // AI is ahead
            }
            else
            {
                // Same checkpoint, compare distance to next checkpoint
                if (checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > 0)
                {
                    // Get the current checkpoint and the next checkpoint
                    int nextCheckpoint = (currentCheckpointPlayer + 1) % checkpointGenerator.Checkpoints.Count;

                    // Get the positions of the current and next checkpoints
                    Vector3 currentCheckpointPos = checkpointGenerator.GetCheckpoint(currentCheckpointPlayer).position;
                    Vector3 nextCheckpointPos = checkpointGenerator.GetCheckpoint(nextCheckpoint).position;

                    if (playerVehicle != null && aiVehicle != null)
                    {
                        // Project the player and AI positions onto the line between current and next checkpoint
                        Vector3 checkpointDirection = (nextCheckpointPos - currentCheckpointPos).normalized;

                        // Calculate the progress along the track segment
                        float playerProgress = Vector3.Dot(playerVehicle.position - currentCheckpointPos, checkpointDirection);
                        float aiProgress = Vector3.Dot(aiVehicle.position - currentCheckpointPos, checkpointDirection);

                        // Normalize the progress values (0 = at current checkpoint, 1 = at next checkpoint)
                        float segmentLength = Vector3.Distance(currentCheckpointPos, nextCheckpointPos);
                        playerProgress = playerProgress / segmentLength;
                        aiProgress = aiProgress / segmentLength;

                        // Only log occasionally to reduce spam
                        if (Time.frameCount % 60 == 0) // Log once per second at 60 FPS
                        {
                            Debug.Log($"[RaceManager] Same checkpoint, comparing progress - Player: {playerProgress:F2}, AI: {aiProgress:F2}");
                        }

                        return playerProgress > aiProgress; // Player is ahead if further along the track segment
                    }
                }

                // Default if we can't determine by distance
                return true;
            }
        }
    }

    /// <summary>
    /// Updates the race UI elements.
    /// </summary>
    private void UpdateUI()
    {
        if (raceInfoPanel == null || !dependenciesReady) return; // Also check dependencies here

        // Activate panel only if race is active
        if (raceInfoPanel.activeSelf != raceActive)
        {
            raceInfoPanel.SetActive(raceActive);
        }
        if (!raceActive) return; // Don't update text if race not active

        if (lapCounterText != null) lapCounterText.text = $"Lap: {currentLapPlayer}/{totalLaps}"; // Show player lap
        if (lapTimeText != null) lapTimeText.text = $"Time: {FormatTime(currentLapTimePlayer)}";
        if (bestLapText != null) bestLapText.text = $"Best: {(bestLapTimePlayer < float.MaxValue ? FormatTime(bestLapTimePlayer) : "--:--.---")}";
        if (positionText != null) positionText.text = $"Position: {(IsPlayerAhead ? "1st" : "2nd")}";

        // Update RaceUI if available
        if (raceUI != null)
        {
            // RaceUI will handle its own updates in its Update method
        }

        // Use the DetermineRacePosition method for position calculation
        if (positionText != null)
        {
            string pos = "N/A";
            if (playerVehicle != null && aiVehicle != null)
            {
                // Update AI checkpoint index from the agent if available
                if (aiAgent != null)
                {
                    currentCheckpointAI = GetAgentCurrentCheckpointIndex(aiAgent);
                }

                // Use our improved position determination method
                bool playerAhead = DetermineRacePosition();
                pos = playerAhead ? "1st" : "2nd";
            }
            else if (playerVehicle != null)
            {
                pos = "1st"; // Assume 1st if no AI
            }
            positionText.text = $"Position: {pos}";
        }
    }
}