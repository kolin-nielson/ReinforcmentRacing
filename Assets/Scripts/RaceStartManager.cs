using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages the race start sequence, including spawning vehicles and countdown.
/// </summary>
public class RaceStartManager : MonoBehaviour
{
    [Header("Race Settings")]
    [SerializeField] private int totalLaps = 3;
    [SerializeField] private int countdownFrom = 3;

    [Header("Vehicle References")]
    [SerializeField] private GameObject playerVehiclePrefab;
    [SerializeField] private GameObject aiVehiclePrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private Transform aiSpawnPoint;
    [SerializeField] private bool useRandomSpawnPoints = true;

    [Header("Dependencies")]
    [SerializeField] private CheckpointGenerator checkpointGenerator;
    [SerializeField] private TrackBoundaryDetector boundaryDetector;
    [SerializeField] private CountdownUI countdownUI;
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private CarSpawnerManager carSpawnerManager;
    [SerializeField] private TrainingStatsUI trainingStatsUI;

    [Header("Camera Settings")]
    [SerializeField] private bool lockCameraToPlayer = true;
    [SerializeField] private string playerCameraTag = "PlayerCamera";
    [SerializeField] private string aiCameraTag = "AICamera";

    [Header("Events")]
    public UnityEvent onRaceStarted;
    public UnityEvent onRaceFinished;

    // Internal state
    private GameObject playerVehicle;
    private GameObject aiVehicle;
    private bool isRaceActive = false;
    private bool isDependenciesReady = false;
    private bool isPlayerReady = false;
    private bool isAIReady = false;

    private void Start()
    {
        // Find dependencies if not assigned
        if (checkpointGenerator == null)
            checkpointGenerator = FindObjectOfType<CheckpointGenerator>();

        if (boundaryDetector == null)
            boundaryDetector = FindObjectOfType<TrackBoundaryDetector>();

        if (countdownUI == null)
            countdownUI = FindObjectOfType<CountdownUI>();

        if (raceManager == null)
            raceManager = FindObjectOfType<RaceManager>();

        // Start initialization
        StartCoroutine(InitializeRace());
    }

    private IEnumerator InitializeRace()
    {
        Debug.Log("[RaceStartManager] Initializing race...");

        // Check game mode and set camera lock accordingly
        int gameMode = PlayerPrefs.GetInt("GameMode", 0);
        bool isRaceMode = (gameMode == 0);
        bool isWatchMode = (gameMode == 1);

        lockCameraToPlayer = isRaceMode; // Lock to player in Race AI mode, follow AI in Watch AI mode
        Debug.Log($"[RaceStartManager] Game mode: {(isRaceMode ? "Race AI" : "Watch AI")}. Camera locked to player: {lockCameraToPlayer}");

        // Handle CarSpawnerManager based on game mode
        if (carSpawnerManager == null)
        {
            carSpawnerManager = FindObjectOfType<CarSpawnerManager>();
        }

        if (carSpawnerManager != null)
        {
            if (isRaceMode) // Race AI mode
            {
                // Disable CarSpawnerManager in Race AI mode to prevent random AI cars from spawning
                carSpawnerManager.enabled = false;
                Debug.Log("[RaceStartManager] Disabled CarSpawnerManager in Race AI mode.");
            }
            else // Watch AI mode
            {
                // Keep CarSpawnerManager enabled in Watch AI mode to allow AI car to spawn
                carSpawnerManager.enabled = true;
                Debug.Log("[RaceStartManager] Enabled CarSpawnerManager in Watch AI mode.");
            }
        }

        // Handle TrainingStatsUI based on game mode
        if (trainingStatsUI == null)
        {
            trainingStatsUI = FindObjectOfType<TrainingStatsUI>();
        }

        if (trainingStatsUI != null)
        {
            if (isRaceMode) // Race AI mode
            {
                // Disable TrainingStatsUI in Race AI mode
                trainingStatsUI.showUI = false;
                Debug.Log("[RaceStartManager] Disabled TrainingStatsUI in Race AI mode.");
            }
            else // Watch AI mode
            {
                // Keep TrainingStatsUI enabled in Watch AI mode
                trainingStatsUI.showUI = true;
                Debug.Log("[RaceStartManager] Enabled TrainingStatsUI in Watch AI mode.");
            }
        }

        // Handle UI elements based on game mode
        if (countdownUI != null)
        {
            countdownUI.gameObject.SetActive(isRaceMode);
            Debug.Log($"[RaceStartManager] {(isRaceMode ? "Enabled" : "Disabled")} CountdownUI.");
        }

        // Find and handle RaceUI if not directly assigned
        Canvas raceUI = FindObjectOfType<RaceUI>()?.GetComponent<Canvas>();
        if (raceUI != null)
        {
            raceUI.gameObject.SetActive(isRaceMode);
            Debug.Log($"[RaceStartManager] {(isRaceMode ? "Enabled" : "Disabled")} RaceUI.");
        }

        // Wait for track dependencies to be ready
        yield return StartCoroutine(WaitForDependencies());

        // Get spawn points
        if (useRandomSpawnPoints && checkpointGenerator != null && checkpointGenerator.GeneratedSpawnPoints.Count >= 2)
        {
            // Use random spawn points from checkpoint generator
            List<Transform> spawnPoints = new List<Transform>(checkpointGenerator.GeneratedSpawnPoints);

            // Shuffle spawn points
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                int randomIndex = Random.Range(i, spawnPoints.Count);
                Transform temp = spawnPoints[i];
                spawnPoints[i] = spawnPoints[randomIndex];
                spawnPoints[randomIndex] = temp;
            }

            // Assign spawn points
            playerSpawnPoint = spawnPoints[0];
            aiSpawnPoint = spawnPoints[1];

            Debug.Log($"[RaceStartManager] Using random spawn points: Player={playerSpawnPoint.name}, AI={aiSpawnPoint.name}");
        }
        else if (playerSpawnPoint == null || aiSpawnPoint == null)
        {
            Debug.LogError("[RaceStartManager] Missing spawn points and no generated spawn points available!");
            yield break;
        }

        // Step 1: Spawn player vehicle first (in Race AI mode only)
        if (isRaceMode) // Race AI mode
        {
            SpawnPlayerVehicle();
            Debug.Log("[RaceStartManager] Player vehicle spawned. Waiting for AI setup...");
        }
        else
        {
            Debug.Log("[RaceStartManager] Skipping player vehicle spawn in Watch AI mode.");
        }

        // Step 2: Show "Get Ready" UI while waiting for AI (in Race AI mode only)
        if (countdownUI != null && isRaceMode)
        {
            countdownUI.ShowGetReady();
            Debug.Log("[RaceStartManager] Showing 'Get Ready' UI.");
        }

        // Step 3: Spawn AI vehicle
        SpawnAIVehicle();

        // Step 4: Handle player vehicle in Watch AI mode
        if (isWatchMode) // Watch AI mode
        {
            // In Watch AI mode, we don't spawn the player vehicle
            isPlayerReady = true; // Mark as ready anyway
            Debug.Log("[RaceStartManager] Watch AI mode - no player vehicle spawned.");

            // Disable any existing player vehicle if it was spawned somehow
            if (playerVehicle != null)
            {
                Debug.Log("[RaceStartManager] Disabling existing player vehicle in Watch AI mode.");
                playerVehicle.SetActive(false);
            }
        }

        // Step 5: Wait for vehicles to be ready
        yield return StartCoroutine(WaitForVehiclesToBeReady());
        Debug.Log("[RaceStartManager] All vehicles ready. Starting countdown...");

        // Step 6: Start countdown
        yield return StartCoroutine(StartCountdown());

        // Step 7: Start race
        StartRace();
    }

    private IEnumerator WaitForDependencies()
    {
        Debug.Log("[RaceStartManager] Waiting for dependencies...");

        isDependenciesReady = false;

        // Wait for checkpoint generator
        while (checkpointGenerator == null || !checkpointGenerator.IsInitialized)
        {
            if (checkpointGenerator == null)
                checkpointGenerator = FindObjectOfType<CheckpointGenerator>();

            yield return new WaitForSeconds(0.5f);
        }

        // Wait for boundary detector
        while (boundaryDetector == null || !boundaryDetector.HasScanned)
        {
            if (boundaryDetector == null)
                boundaryDetector = FindObjectOfType<TrackBoundaryDetector>();

            yield return new WaitForSeconds(0.5f);
        }

        isDependenciesReady = true;
        Debug.Log("[RaceStartManager] Dependencies ready.");
    }

    private void SpawnPlayerVehicle()
    {
        if (playerVehiclePrefab == null || playerSpawnPoint == null)
        {
            Debug.LogError("[RaceStartManager] Cannot spawn player vehicle - missing prefab or spawn point!");
            return;
        }

        Debug.Log("[RaceStartManager] Spawning player vehicle...");

        // Spawn player vehicle with height offset to prevent ground collision
        Vector3 spawnPosition = playerSpawnPoint.position + Vector3.up * 0.5f;
        playerVehicle = Instantiate(playerVehiclePrefab, spawnPosition, playerSpawnPoint.rotation);
        playerVehicle.name = "PlayerVehicle";
        Debug.Log($"[RaceStartManager] Player vehicle spawned at position: {spawnPosition}, rotation: {playerSpawnPoint.rotation.eulerAngles}");

        // Make sure the player vehicle has the "Player" tag
        playerVehicle.tag = "Player";
        Debug.Log("[RaceStartManager] Set player vehicle tag to 'Player'");

        // Get input manager
        InputManager inputManager = playerVehicle.GetComponent<InputManager>();
        if (inputManager != null)
        {
            // Disable ML-Agent control
            inputManager.EnableMLAgentControl(false);
            inputManager.SetOverrideInputs(false);
        }

        // Disable vehicle movement until race starts
        Rigidbody rb = playerVehicle.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // Find the nearest checkpoint to the player and align rotation with track direction
        if (checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > 1)
        {
            // Find the nearest checkpoint to the player's spawn position
            int nearestCheckpointIndex = 0;
            CheckpointGenerator.Checkpoint nearestCp = checkpointGenerator.GetNearestCheckpoint(playerVehicle.transform.position, out nearestCheckpointIndex);

            if (nearestCp != null)
            {
                Debug.Log($"[RaceStartManager] Nearest checkpoint to player spawn is {nearestCheckpointIndex} at position {nearestCp.position}");

                // Get the next checkpoint to determine the intended direction
                CheckpointGenerator.Checkpoint nextCheckpoint = checkpointGenerator.GetNextCheckpoint(nearestCheckpointIndex);

                if (nextCheckpoint != null)
                {
                    // Calculate the intended direction vector
                    Vector3 intendedDirection = (nextCheckpoint.position - nearestCp.position).normalized;

                    // Set rotation to face the intended direction
                    Quaternion targetRotation = Quaternion.LookRotation(intendedDirection, Vector3.up);
                    playerVehicle.transform.rotation = targetRotation;
                    Debug.Log($"[RaceStartManager] Set player vehicle rotation to match track direction: {targetRotation.eulerAngles}");
                }
                else
                {
                    Debug.LogWarning("[RaceStartManager] Could not find next checkpoint for player rotation alignment.");
                }
            }
            else
            {
                Debug.LogWarning("[RaceStartManager] Could not find nearest checkpoint for player rotation alignment.");
            }
        }

        // Set up camera for player vehicle
        SetupPlayerCamera();

        isPlayerReady = true;
        Debug.Log("[RaceStartManager] Player vehicle ready.");
    }

    private void SpawnAIVehicle()
    {
        int gameMode = PlayerPrefs.GetInt("GameMode", 0);

        // In Watch AI mode, let the CarSpawnerManager handle AI vehicle spawning
        if (gameMode == 1 && carSpawnerManager != null && carSpawnerManager.enabled)
        {
            Debug.Log("[RaceStartManager] In Watch AI mode, waiting for CarSpawnerManager to spawn AI vehicle...");

            // Start a coroutine to wait for the AI vehicle to be spawned by CarSpawnerManager
            StartCoroutine(WaitForAIVehicleFromSpawner());
            return;
        }

        // In Race AI mode, we spawn the AI vehicle ourselves
        if (aiVehiclePrefab == null || aiSpawnPoint == null)
        {
            Debug.LogError("[RaceStartManager] Cannot spawn AI vehicle - missing prefab or spawn point!");
            return;
        }

        Debug.Log("[RaceStartManager] Spawning AI vehicle...");

        // Spawn AI vehicle with increased height offset to prevent ground collision
        Vector3 spawnPosition = aiSpawnPoint.position + Vector3.up * 1.0f; // Increased from 0.5f to 1.0f

        // Ensure the spawn point is valid by raycasting to find the track surface
        RaycastHit hit;
        if (Physics.Raycast(spawnPosition + Vector3.up * 5.0f, Vector3.down, out hit, 10.0f, boundaryDetector.trackLayer))
        {
            // Use the raycast hit point with a height offset for more accurate placement
            spawnPosition = hit.point + Vector3.up * 1.0f;
            Debug.Log($"[RaceStartManager] Adjusted AI spawn position to track surface: {spawnPosition}");
        }

        aiVehicle = Instantiate(aiVehiclePrefab, spawnPosition, aiSpawnPoint.rotation);
        aiVehicle.name = "AIVehicle";
        Debug.Log($"[RaceStartManager] AI vehicle spawned at position: {spawnPosition}, rotation: {aiSpawnPoint.rotation.eulerAngles}");

        // Make sure the AI vehicle has the "AI" tag
        aiVehicle.tag = "AI";
        Debug.Log("[RaceStartManager] Set AI vehicle tag to 'AI'");

        // Get racing agent
        RacingAgent racingAgent = aiVehicle.GetComponent<RacingAgent>();
        if (racingAgent != null)
        {
            // Set up racing agent
            racingAgent.boundaryDetector = boundaryDetector;
            racingAgent.checkpointGenerator = checkpointGenerator;
            racingAgent.trackLayer = boundaryDetector.trackLayer;
            racingAgent.boundaryWallLayer = boundaryDetector.boundaryWallLayer;

            // Disable episode auto-reset for race mode
            racingAgent.maxEpisodeSteps = int.MaxValue;
            racingAgent.resetOnBoundaryHit = false;
            racingAgent.resetWhenUpsideDown = false;

            // Check if we need to adjust the AI's rotation to match checkpoint direction
            if (checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > 1)
            {
                // Get the first two checkpoints to determine the intended direction
                CheckpointGenerator.Checkpoint firstCheckpoint = checkpointGenerator.GetCheckpoint(0);
                CheckpointGenerator.Checkpoint secondCheckpoint = checkpointGenerator.GetCheckpoint(1);

                if (firstCheckpoint != null && secondCheckpoint != null)
                {
                    // Calculate the intended direction vector
                    Vector3 intendedDirection = (secondCheckpoint.position - firstCheckpoint.position).normalized;

                    // Calculate the dot product to see if the AI is facing roughly the right direction
                    float dotProduct = Vector3.Dot(aiVehicle.transform.forward, intendedDirection);

                    // If the dot product is negative, the AI is facing the wrong way
                    if (dotProduct < 0)
                    {
                        // Rotate the AI 180 degrees to face the correct direction
                        aiVehicle.transform.rotation = Quaternion.LookRotation(intendedDirection, Vector3.up);
                        Debug.Log("[RaceStartManager] Adjusted AI vehicle rotation to match checkpoint direction.");
                    }
                }
            }
        }

        // Get input manager and ensure ML-Agent control is properly set up
        InputManager inputManager = aiVehicle.GetComponent<InputManager>();
        if (inputManager != null)
        {
            // Enable ML-Agent control
            inputManager.EnableMLAgentControl(true);
            inputManager.SetOverrideInputs(true);
            Debug.Log("[RaceStartManager] Enabled ML-Agent control for AI vehicle.");

            // We already have a reference to the RacingAgent from earlier in this method
            // Make sure the agent has references to required components
            if (racingAgent.inputManager == null)
            {
                racingAgent.inputManager = inputManager;
                Debug.Log("[RaceStartManager] Set inputManager reference on RacingAgent.");
            }

            if (racingAgent.vehicleController == null)
            {
                racingAgent.vehicleController = aiVehicle.GetComponent<VehicleController>();
                Debug.Log("[RaceStartManager] Set vehicleController reference on RacingAgent.");
            }

            if (racingAgent.boundaryDetector == null)
            {
                racingAgent.boundaryDetector = boundaryDetector;
                Debug.Log("[RaceStartManager] Set boundaryDetector reference on RacingAgent.");
            }

            if (racingAgent.checkpointGenerator == null)
            {
                racingAgent.checkpointGenerator = checkpointGenerator;
                Debug.Log("[RaceStartManager] Set checkpointGenerator reference on RacingAgent.");
            }

            // Ensure the agent is enabled
            if (!racingAgent.enabled)
            {
                racingAgent.enabled = true;
                Debug.Log("[RaceStartManager] Enabled RacingAgent component.");
            }
            else
            {
                Debug.LogWarning("[RaceStartManager] AI vehicle does not have a RacingAgent component!");
            }
        }
        else
        {
            Debug.LogWarning("[RaceStartManager] AI vehicle does not have an InputManager component!");
        }

        // Disable vehicle movement until race starts
        Rigidbody rb = aiVehicle.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        // Set up camera for AI vehicle
        SetupAICamera();

        isAIReady = true;
        Debug.Log("[RaceStartManager] AI vehicle ready.");
    }

    /// <summary>
    /// Waits for the CarSpawnerManager to spawn the AI vehicle in Watch AI mode.
    /// </summary>
    private IEnumerator WaitForAIVehicleFromSpawner()
    {
        Debug.Log("[RaceStartManager] Waiting for CarSpawnerManager to spawn AI vehicle...");

        // Wait for CarSpawnerManager to initialize and spawn vehicles
        yield return new WaitForSeconds(1.0f);

        // Try to find the AI vehicle spawned by CarSpawnerManager
        RacingAgent[] agents = FindObjectsOfType<RacingAgent>();

        // Wait until at least one agent is found
        float timeout = 10.0f;
        float elapsed = 0f;

        while (agents.Length == 0 && elapsed < timeout)
        {
            Debug.Log("[RaceStartManager] No AI agents found yet, waiting...");
            yield return new WaitForSeconds(0.5f);
            agents = FindObjectsOfType<RacingAgent>();
            elapsed += 0.5f;
        }

        if (agents.Length > 0)
        {
            // Use the first agent as our AI vehicle
            aiVehicle = agents[0].gameObject;
            Debug.Log($"[RaceStartManager] Found AI vehicle spawned by CarSpawnerManager: {aiVehicle.name}");

            // Make sure the AI vehicle has the "AI" tag
            aiVehicle.tag = "AI";
            Debug.Log("[RaceStartManager] Set AI vehicle tag to 'AI'");

            // Check if we need to adjust the AI's rotation to match checkpoint direction
            if (checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > 1)
            {
                // Get the first two checkpoints to determine the intended direction
                CheckpointGenerator.Checkpoint firstCheckpoint = checkpointGenerator.GetCheckpoint(0);
                CheckpointGenerator.Checkpoint secondCheckpoint = checkpointGenerator.GetCheckpoint(1);

                if (firstCheckpoint != null && secondCheckpoint != null)
                {
                    // Calculate the intended direction vector
                    Vector3 intendedDirection = (secondCheckpoint.position - firstCheckpoint.position).normalized;

                    // Calculate the dot product to see if the AI is facing roughly the right direction
                    float dotProduct = Vector3.Dot(aiVehicle.transform.forward, intendedDirection);

                    // If the dot product is negative, the AI is facing the wrong way
                    if (dotProduct < 0)
                    {
                        // Rotate the AI 180 degrees to face the correct direction
                        aiVehicle.transform.rotation = Quaternion.LookRotation(intendedDirection, Vector3.up);
                        Debug.Log("[RaceStartManager] Adjusted AI vehicle rotation to match checkpoint direction.");
                    }
                }
            }

            // Set up camera for AI vehicle
            SetupAICamera();

            // Disable vehicle movement until race starts
            Rigidbody rb = aiVehicle.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            isAIReady = true;
            Debug.Log("[RaceStartManager] AI vehicle from CarSpawnerManager ready.");
        }
        else
        {
            Debug.LogError("[RaceStartManager] Failed to find AI vehicle spawned by CarSpawnerManager!");

            // Mark as ready anyway to avoid hanging
            isAIReady = true;
        }
    }

    /// <summary>
    /// Sets up the camera for the player vehicle.
    /// </summary>
    private void SetupPlayerCamera()
    {
        if (playerVehicle == null)
            return;

        // Find player camera by tag
        GameObject playerCameraObj = GameObject.FindWithTag(playerCameraTag);
        if (playerCameraObj == null)
        {
            // Try to find camera as a child of the player vehicle
            Transform cameraTransform = playerVehicle.transform.Find("Camera");
            if (cameraTransform != null)
            {
                playerCameraObj = cameraTransform.gameObject;
            }
            else
            {
                Debug.LogWarning("[RaceStartManager] Could not find player camera by tag or as a child.");
                return;
            }
        }

        // Check if the camera has a Cinemachine component
        var virtualCamera = playerCameraObj.GetComponent<Cinemachine.CinemachineVirtualCamera>();
        if (virtualCamera != null)
        {
            // Set the follow target to the player vehicle
            virtualCamera.Follow = playerVehicle.transform;
            virtualCamera.LookAt = playerVehicle.transform;

            // Activate the camera if we're locking to player
            if (lockCameraToPlayer)
            {
                virtualCamera.Priority = 20; // Higher priority to ensure it's active
                Debug.Log("[RaceStartManager] Player camera activated with priority 20.");
            }
            else
            {
                virtualCamera.Priority = 10; // Default priority
            }
        }
        else
        {
            Debug.LogWarning("[RaceStartManager] Player camera does not have a CinemachineVirtualCamera component.");
        }
    }

    /// <summary>
    /// Sets up the camera for the AI vehicle.
    /// </summary>
    private void SetupAICamera()
    {
        if (aiVehicle == null)
            return;

        // Find AI camera by tag
        GameObject aiCameraObj = GameObject.FindWithTag(aiCameraTag);
        if (aiCameraObj == null)
        {
            // Try to find camera as a child of the AI vehicle
            Transform cameraTransform = aiVehicle.transform.Find("Camera");
            if (cameraTransform != null)
            {
                aiCameraObj = cameraTransform.gameObject;
            }
            else
            {
                Debug.LogWarning("[RaceStartManager] Could not find AI camera by tag or as a child.");
                return;
            }
        }

        // Check if the camera has a Cinemachine component
        var virtualCamera = aiCameraObj.GetComponent<Cinemachine.CinemachineVirtualCamera>();
        if (virtualCamera != null)
        {
            // Set the follow target to the AI vehicle
            virtualCamera.Follow = aiVehicle.transform;
            virtualCamera.LookAt = aiVehicle.transform;

            // Set priority based on game mode
            if (lockCameraToPlayer)
            {
                virtualCamera.Priority = 10; // Lower priority than player camera
                Debug.Log("[RaceStartManager] AI camera set to lower priority 10.");
            }
            else
            {
                virtualCamera.Priority = 20; // Higher priority for watch mode
                Debug.Log("[RaceStartManager] AI camera activated with priority 20.");
            }
        }
        else
        {
            Debug.LogWarning("[RaceStartManager] AI camera does not have a CinemachineVirtualCamera component.");
        }
    }

    private IEnumerator WaitForVehiclesToBeReady()
    {
        Debug.Log("[RaceStartManager] Waiting for AI to be ready...");

        // Player should already be ready at this point
        if (!isPlayerReady)
        {
            Debug.LogWarning("[RaceStartManager] Player vehicle not ready yet. This shouldn't happen.");

            // Wait for player to be ready (just in case)
            while (!isPlayerReady)
            {
                yield return null;
            }
        }

        // Wait for AI to be ready
        while (!isAIReady)
        {
            yield return null;
        }

        Debug.Log("[RaceStartManager] All vehicles ready.");
    }

    private IEnumerator StartCountdown()
    {
        // Check game mode
        int gameMode = PlayerPrefs.GetInt("GameMode", 0);
        bool isRaceMode = (gameMode == 0);
        bool isWatchMode = (gameMode == 1);

        // Skip countdown in Watch AI mode
        if (isWatchMode)
        {
            Debug.Log("[RaceStartManager] Skipping countdown in Watch AI mode.");
            yield break;
        }

        Debug.Log("[RaceStartManager] Starting countdown...");

        if (countdownUI != null && countdownUI.gameObject.activeInHierarchy)
        {
            yield return StartCoroutine(countdownUI.StartCountdown(countdownFrom));
        }
        else
        {
            // Simple countdown if no UI
            for (int i = countdownFrom; i > 0; i--)
            {
                Debug.Log($"[RaceStartManager] {i}...");
                yield return new WaitForSeconds(1f);
            }

            Debug.Log("[RaceStartManager] GO!");
            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>
    /// Connects checkpoint triggers to the race manager.
    /// </summary>
    private void ConnectCheckpointTriggers()
    {
        if (raceManager == null || checkpointGenerator == null)
            return;

        Debug.Log("[RaceStartManager] Connecting checkpoint triggers to race manager...");

        // Find all checkpoint triggers in the scene
        CheckpointTrigger[] checkpointTriggers = FindObjectsOfType<CheckpointTrigger>();

        // Connect each checkpoint trigger to the race manager
        foreach (CheckpointTrigger trigger in checkpointTriggers)
        {
            // Remove any existing listeners to avoid duplicates
            trigger.OnCheckpointTriggered.RemoveAllListeners();

            // Add the race manager's OnPlayerCheckpointTriggered method as a listener
            trigger.OnCheckpointTriggered.AddListener(raceManager.OnPlayerCheckpointTriggered);

            Debug.Log($"[RaceStartManager] Connected checkpoint {trigger.GetCheckpointIndex()} to race manager.");
        }

        Debug.Log($"[RaceStartManager] Connected {checkpointTriggers.Length} checkpoint triggers to race manager.");
    }

    private void StartRace()
    {
        // Check game mode
        int gameMode = PlayerPrefs.GetInt("GameMode", 0);
        bool isRaceMode = (gameMode == 0);
        bool isWatchMode = (gameMode == 1);

        if (isRaceMode)
        {
            Debug.Log("[RaceStartManager] Starting race in Race AI mode!");

            // Set up race manager first (to initialize race state)
            if (raceManager != null)
            {
                if (playerVehicle != null)
                    raceManager.playerVehicle = playerVehicle.transform;

                if (aiVehicle != null)
                    raceManager.aiVehicle = aiVehicle.transform;

                // Connect checkpoint triggers to race manager
                ConnectCheckpointTriggers();

                raceManager.StartRace();
            }

            // Enable vehicle movement for both vehicles simultaneously
            Rigidbody playerRb = playerVehicle?.GetComponent<Rigidbody>();
            Rigidbody aiRb = aiVehicle?.GetComponent<Rigidbody>();

            if (playerRb != null)
            {
                // First, ensure the vehicle is positioned correctly
                Vector3 playerPosition = playerSpawnPoint.position + Vector3.up * 0.5f; // Add height to prevent ground collision

                // Ensure the position is on the track by raycasting
                RaycastHit hit;
                if (Physics.Raycast(playerPosition + Vector3.up * 5.0f, Vector3.down, out hit, 10.0f, boundaryDetector.trackLayer))
                {
                    // Use the raycast hit point with a height offset for more accurate placement
                    playerPosition = hit.point + Vector3.up * 0.5f;
                    Debug.Log($"[RaceStartManager] Adjusted player position to track surface: {playerPosition}");
                }

                // Set the player vehicle position
                playerVehicle.transform.position = playerPosition;
                Debug.Log($"[RaceStartManager] Set player vehicle position to {playerPosition}");

                // Find the nearest checkpoint to the player
                int nearestCheckpointIndex = 0;
                if (checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > 0)
                {
                    // Get the nearest checkpoint index directly for our own use
                    CheckpointGenerator.Checkpoint nearestCp = checkpointGenerator.GetNearestCheckpoint(playerPosition, out nearestCheckpointIndex);
                    if (nearestCp != null)
                    {
                        Debug.Log($"[RaceStartManager] Nearest checkpoint to player is {nearestCheckpointIndex} at position {nearestCp.position}");
                    }
                }

                // Ensure the rotation is aligned with the track direction
                if (checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > 1)
                {
                    // Get the current and next checkpoints to determine the intended direction
                    CheckpointGenerator.Checkpoint currentCheckpoint = checkpointGenerator.GetCheckpoint(nearestCheckpointIndex);
                    CheckpointGenerator.Checkpoint nextCheckpoint = checkpointGenerator.GetNextCheckpoint(nearestCheckpointIndex);

                    if (currentCheckpoint != null && nextCheckpoint != null)
                    {
                        // Calculate the intended direction vector
                        Vector3 intendedDirection = (nextCheckpoint.position - currentCheckpoint.position).normalized;

                        // Set rotation to face the intended direction
                        Quaternion targetRotation = Quaternion.LookRotation(intendedDirection, Vector3.up);
                        playerVehicle.transform.rotation = targetRotation;
                        Debug.Log($"[RaceStartManager] Set player vehicle rotation to match track direction: {targetRotation.eulerAngles}");
                    }
                    else
                    {
                        playerVehicle.transform.rotation = playerSpawnPoint.rotation;
                        Debug.Log($"[RaceStartManager] Using spawn point rotation: {playerSpawnPoint.rotation.eulerAngles}");
                    }
                }
                else
                {
                    playerVehicle.transform.rotation = playerSpawnPoint.rotation;
                    Debug.Log($"[RaceStartManager] Using spawn point rotation (no checkpoints): {playerSpawnPoint.rotation.eulerAngles}");
                }

                // Reset velocity and angular velocity
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;

                // Make sure the player vehicle is not kinematic
                playerRb.isKinematic = false;
                Debug.Log("[RaceStartManager] Player vehicle physics enabled.");

                // Make sure the player vehicle has gravity enabled
                playerRb.useGravity = true;

                // Ensure the vehicle is awake
                playerRb.WakeUp();

                Debug.Log($"[RaceStartManager] Player vehicle position: {playerVehicle.transform.position}, rotation: {playerVehicle.transform.rotation.eulerAngles}");
            }

            if (aiRb != null)
            {
                // First, ensure the vehicle is positioned correctly with increased height
                Vector3 aiPosition = aiSpawnPoint.position + Vector3.up * 1.5f; // Increased from 1.0f to 1.5f for better clearance

                // Ensure the position is on the track by raycasting
                RaycastHit hit;
                if (Physics.Raycast(aiPosition + Vector3.up * 5.0f, Vector3.down, out hit, 10.0f, boundaryDetector.trackLayer))
                {
                    // Use the raycast hit point with a height offset for more accurate placement
                    aiPosition = hit.point + Vector3.up * 1.5f; // Increased height offset
                    Debug.Log($"[RaceStartManager] Adjusted AI position to track surface: {aiPosition}");
                }

                // Get the RacingAgent component to find the nearest checkpoint
                RacingAgent aiAgent = aiVehicle.GetComponent<RacingAgent>();
                int nearestCheckpointIndex = 0;

                if (aiAgent != null && checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > 0)
                {
                    // Force the agent to completely reset using the new public method
                    Debug.Log("[RaceStartManager] Forcing RacingAgent to reset...");
                    aiAgent.ForceAgentReset();

                    // Also get the nearest checkpoint index directly for our own use
                    CheckpointGenerator.Checkpoint nearestCp = checkpointGenerator.GetNearestCheckpoint(aiPosition, out nearestCheckpointIndex);
                    if (nearestCp != null)
                    {
                        Debug.Log($"[RaceStartManager] Nearest checkpoint to AI is {nearestCheckpointIndex} at position {nearestCp.position}");
                    }
                }

                // Set the AI vehicle position
                aiVehicle.transform.position = aiPosition;
                Debug.Log($"[RaceStartManager] Set AI vehicle position to {aiPosition}");

                // Ensure the rotation is aligned with the track direction
                if (checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > 1)
                {
                    // Get the current and next checkpoints to determine the intended direction
                    CheckpointGenerator.Checkpoint currentCheckpoint = checkpointGenerator.GetCheckpoint(nearestCheckpointIndex);
                    CheckpointGenerator.Checkpoint nextCheckpoint = checkpointGenerator.GetNextCheckpoint(nearestCheckpointIndex);

                    if (currentCheckpoint != null && nextCheckpoint != null)
                    {
                        // Calculate the intended direction vector
                        Vector3 intendedDirection = (nextCheckpoint.position - currentCheckpoint.position).normalized;

                        // Set rotation to face the intended direction
                        Quaternion targetRotation = Quaternion.LookRotation(intendedDirection, Vector3.up);
                        aiVehicle.transform.rotation = targetRotation;
                        Debug.Log($"[RaceStartManager] Set AI vehicle rotation to match track direction: {targetRotation.eulerAngles}");
                    }
                    else
                    {
                        aiVehicle.transform.rotation = aiSpawnPoint.rotation;
                        Debug.Log($"[RaceStartManager] Using spawn point rotation: {aiSpawnPoint.rotation.eulerAngles}");
                    }
                }
                else
                {
                    aiVehicle.transform.rotation = aiSpawnPoint.rotation;
                    Debug.Log($"[RaceStartManager] Using spawn point rotation (no checkpoints): {aiSpawnPoint.rotation.eulerAngles}");
                }

                // Reset velocity and angular velocity
                aiRb.linearVelocity = Vector3.zero;
                aiRb.angularVelocity = Vector3.zero;

                // Make sure the AI vehicle is not kinematic
                aiRb.isKinematic = false;
                Debug.Log("[RaceStartManager] AI vehicle physics enabled.");

                // Make sure the AI vehicle has gravity enabled
                aiRb.useGravity = true;

                // Ensure the vehicle is awake
                aiRb.WakeUp();

                Debug.Log($"[RaceStartManager] AI vehicle position: {aiVehicle.transform.position}, rotation: {aiVehicle.transform.rotation.eulerAngles}");
            }

            Debug.Log("[RaceStartManager] Vehicles released. Race is on!");

            // Trigger race started event
            onRaceStarted?.Invoke();
        }
        else // Watch AI mode
        {
            Debug.Log("[RaceStartManager] Starting AI demonstration in Watch AI mode!");

            // In Watch AI mode, we only need to release the AI vehicle
            Rigidbody aiRb = aiVehicle?.GetComponent<Rigidbody>();
            if (aiRb != null)
            {
                // First, ensure the vehicle is positioned correctly with increased height
                Vector3 aiPosition = aiSpawnPoint.position + Vector3.up * 1.5f; // Increased from 1.0f to 1.5f for better clearance

                // Ensure the position is on the track by raycasting
                RaycastHit hit;
                if (Physics.Raycast(aiPosition + Vector3.up * 5.0f, Vector3.down, out hit, 10.0f, boundaryDetector.trackLayer))
                {
                    // Use the raycast hit point with a height offset for more accurate placement
                    aiPosition = hit.point + Vector3.up * 1.5f; // Increased height offset
                    Debug.Log($"[RaceStartManager] Adjusted AI position to track surface: {aiPosition}");
                }

                // Get the RacingAgent component to find the nearest checkpoint
                RacingAgent aiAgent = aiVehicle.GetComponent<RacingAgent>();
                int nearestCheckpointIndex = 0;

                if (aiAgent != null && checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > 0)
                {
                    // Force the agent to completely reset using the new public method
                    Debug.Log("[RaceStartManager] Forcing RacingAgent to reset...");
                    aiAgent.ForceAgentReset();

                    // Also get the nearest checkpoint index directly for our own use
                    CheckpointGenerator.Checkpoint nearestCp = checkpointGenerator.GetNearestCheckpoint(aiPosition, out nearestCheckpointIndex);
                    if (nearestCp != null)
                    {
                        Debug.Log($"[RaceStartManager] Nearest checkpoint to AI is {nearestCheckpointIndex} at position {nearestCp.position}");
                    }
                }

                // Set the AI vehicle position
                aiVehicle.transform.position = aiPosition;
                Debug.Log($"[RaceStartManager] Set AI vehicle position to {aiPosition}");

                // Ensure the rotation is aligned with the track direction
                if (checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > 1)
                {
                    // Get the current and next checkpoints to determine the intended direction
                    CheckpointGenerator.Checkpoint currentCheckpoint = checkpointGenerator.GetCheckpoint(nearestCheckpointIndex);
                    CheckpointGenerator.Checkpoint nextCheckpoint = checkpointGenerator.GetNextCheckpoint(nearestCheckpointIndex);

                    if (currentCheckpoint != null && nextCheckpoint != null)
                    {
                        // Calculate the intended direction vector
                        Vector3 intendedDirection = (nextCheckpoint.position - currentCheckpoint.position).normalized;

                        // Set rotation to face the intended direction
                        Quaternion targetRotation = Quaternion.LookRotation(intendedDirection, Vector3.up);
                        aiVehicle.transform.rotation = targetRotation;
                        Debug.Log($"[RaceStartManager] Set AI vehicle rotation to match track direction: {targetRotation.eulerAngles}");
                    }
                    else
                    {
                        aiVehicle.transform.rotation = aiSpawnPoint.rotation;
                        Debug.Log($"[RaceStartManager] Using spawn point rotation: {aiSpawnPoint.rotation.eulerAngles}");
                    }
                }
                else
                {
                    aiVehicle.transform.rotation = aiSpawnPoint.rotation;
                    Debug.Log($"[RaceStartManager] Using spawn point rotation (no checkpoints): {aiSpawnPoint.rotation.eulerAngles}");
                }

                // Reset velocity and angular velocity
                aiRb.linearVelocity = Vector3.zero;
                aiRb.angularVelocity = Vector3.zero;

                // Make sure the AI vehicle is not kinematic
                aiRb.isKinematic = false;
                Debug.Log("[RaceStartManager] AI vehicle physics enabled.");

                // Make sure the AI vehicle has gravity enabled
                aiRb.useGravity = true;

                // Ensure the vehicle is awake
                aiRb.WakeUp();

                Debug.Log($"[RaceStartManager] AI vehicle position: {aiVehicle.transform.position}, rotation: {aiVehicle.transform.rotation.eulerAngles}");
            }

            // No need to set up race manager or connect checkpoint triggers
            // Just trigger the race started event
            onRaceStarted?.Invoke();
        }

        isRaceActive = true;
    }

    /// <summary>
    /// Finishes the race and declares a winner.
    /// </summary>
    /// <param name="playerWon">True if the player won, false if the AI won.</param>
    public void FinishRace(bool playerWon)
    {
        if (!isRaceActive)
            return;

        Debug.Log($"[RaceStartManager] Race finished! {(playerWon ? "Player" : "AI")} won!");

        isRaceActive = false;

        // Trigger race finished event
        onRaceFinished?.Invoke();
    }
}
