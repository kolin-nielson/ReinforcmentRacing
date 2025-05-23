using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// Manages spawning and setup of car prefabs for ML-Agents training,
/// ensuring dependencies like track analysis are ready.
/// Can use manually assigned spawn points or automatically generated ones.
/// </summary>

// Needed for ToArray

public class CarSpawnerManager : MonoBehaviour
{
    // Default layer

    [Header("Track System Dependencies")]
    [Tooltip("Reference to the TrackBoundaryDetector component.")]
    public TrackBoundaryDetector boundaryDetector;

    [Tooltip("The physics layer the spawned cars should be on.")]
    public LayerMask carLayer = 1 << 0;

    [Header("Car Setup")]
    [Tooltip("The car prefab to be spawned. Must have VehicleController and InputManager.")]
    public GameObject carPrefab;

    [Tooltip("Reference to the CheckpointGenerator component (Required for generated spawn points).")]
    public CheckpointGenerator checkpointGenerator;

    [Tooltip("Set up agents for self-play competition (requires BehaviorParameters TeamId configuration).")]
    public bool enableSelfPlay = false;

    [Tooltip("Number of cars (agents) to spawn for parallel training.")]
    [Range(1, 64)]
    public int numberOfCars = 1;

    [Tooltip("If true, disables physics collisions between spawned agent cars.")]
    public bool preventCollisionsBetweenAgents = true;

    [Tooltip("Distribute cars randomly among available spawn points if true, otherwise sequentially.")]
    public bool randomizeSpawnPoints = true;

    [Header("Training Settings")]
    // *** NEW: Option to use generated points ***
    [Tooltip("If true, uses spawn points generated by CheckpointGenerator. If false, uses 'Manual Spawn Points'.")]
    public bool useGeneratedSpawnPoints = true;

    private Transform[] activeSpawnPoints;
    private List<RacingAgent> agents = new List<RacingAgent>();
    private int carLayerIndex = -1;
    private bool isInitialized = false;

    // *** MODIFIED: Now optional if using generated points ***
    [Tooltip("Transforms defining spawn positions and initial rotations (Used if 'Use Generated Spawn Points' is false).")]
    public Transform[]
     manualSpawnPoints;

    // --- Private State ---
    private List<GameObject> spawnedCars = new List<GameObject>();

    // --- Public Getters ---
    public List<RacingAgent> SpawnedAgents => agents;
    public GameObject FirstSpawnedCar => spawnedCars.Count > 0 ? spawnedCars[0] : null;

    public void ResetAllAgents() { Debug.Log("[CarSpawnerManager] Resetting all agents."); foreach (RacingAgent agent in agents) { if (agent != null) { agent.EndEpisode(); } } }

    private IEnumerator InitializeAndSpawn()
    {
        yield return StartCoroutine(WaitForDependencies());
        if (!SelectActiveSpawnPoints()) // *** NEW: Select spawn points AFTER dependencies ready ***
        {
            enabled = false; // Stop if no valid spawn points
            yield break;
        }
        SpawnCars();
        isInitialized = true;
    }

    private void OnDestroy() { foreach (GameObject car in spawnedCars) { if (car != null) { Destroy(car); } } spawnedCars.Clear(); agents.Clear(); }

    // *** NEW: Method to select which spawn points to use ***
    private bool SelectActiveSpawnPoints()
    {
        if (useGeneratedSpawnPoints)
        {
            if (!checkpointGenerator.generateSpawnPoints)
            {
                Debug.LogError("[CarSpawnerManager] 'Use Generated Spawn Points' is true, but 'Generate Spawn Points' is false on CheckpointGenerator!", this);
                return false;
            }
            if (checkpointGenerator.GeneratedSpawnPoints == null || checkpointGenerator.GeneratedSpawnPoints.Count == 0)
            {
                Debug.LogError("[CarSpawnerManager] 'Use Generated Spawn Points' is true, but CheckpointGenerator did not generate any spawn points!", this);
                return false;
            }
            activeSpawnPoints = checkpointGenerator.GeneratedSpawnPoints.ToArray();
            Debug.Log($"[CarSpawnerManager] Using {activeSpawnPoints.Length} spawn points generated by CheckpointGenerator.");
        }
        else
        {
            if (manualSpawnPoints == null || manualSpawnPoints.Length == 0)
            {
                Debug.LogWarning("[CarSpawnerManager] 'Use Generated Spawn Points' is false, but no 'Manual Spawn Points' are assigned. Creating default.", this);
                GameObject defaultSpawn = new GameObject("DefaultSpawnPoint"); defaultSpawn.transform.SetPositionAndRotation(transform.position, transform.rotation); defaultSpawn.transform.SetParent(transform);
                manualSpawnPoints = new Transform[] { defaultSpawn.transform };
            }
            activeSpawnPoints = manualSpawnPoints;
            Debug.Log($"[CarSpawnerManager] Using {activeSpawnPoints.Length} manually assigned spawn points.");
        }

        if (activeSpawnPoints.Length == 0)
        {
            Debug.LogError("[CarSpawnerManager] No active spawn points available!", this);
            return false;
        }

        // Warning if not enough points for sequential spawning
        if (activeSpawnPoints.Length < numberOfCars && !randomizeSpawnPoints)
        {
            Debug.LogWarning($"[CarSpawnerManager] Fewer active spawn points ({activeSpawnPoints.Length}) than cars ({numberOfCars}). Cars will reuse points sequentially.", this);
        }
        return true;
    }

    private Transform SelectSpawnPoint(int agentIndex)
    {
        // Now uses activeSpawnPoints determined by SelectActiveSpawnPoints()
        if (activeSpawnPoints == null || activeSpawnPoints.Length == 0) return null;
        if (randomizeSpawnPoints) { return activeSpawnPoints[Random.Range(0, activeSpawnPoints.Length)]; }
        else { return activeSpawnPoints[agentIndex % activeSpawnPoints.Length]; }
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    /*
        private void ConfigureBehaviorSpaces(BehaviorParameters behaviorParams, RacingAgent agent)
        {
            // ... (previous reflection code) ...
            // Recommendation: Do this manually on the prefab instead!
            Debug.LogWarning("[CarSpawnerManager] ConfigureBehaviorSpaces called, but manual setup on prefab is recommended due to potential reflection issues.");
        }
        */
    // Commented out ConfigureBehaviorSpaces as manual setup on prefab is recommended
    private void SetupAgentCollisionPrevention() { Debug.Log("[CarSpawnerManager] Setting up collision prevention."); for (int i = 0; i < spawnedCars.Count; i++) { if (spawnedCars[i] == null) continue; Collider[] colsA = spawnedCars[i].GetComponentsInChildren<Collider>(); for (int j = i + 1; j < spawnedCars.Count; j++) { if (spawnedCars[j] == null) continue; Collider[] colsB = spawnedCars[j].GetComponentsInChildren<Collider>(); foreach (Collider ca in colsA) { foreach (Collider cb in colsB) { if (ca != null && cb != null && ca.enabled && cb.enabled && !ca.isTrigger && !cb.isTrigger) { if (!Physics.GetIgnoreLayerCollision(ca.gameObject.layer, cb.gameObject.layer)) { Physics.IgnoreCollision(ca, cb, true); } } } } } } }

    private RacingAgent SetupAgentComponents(GameObject carInstance, int agentIndex)
    {
        RacingAgent agent = carInstance.GetComponent<RacingAgent>(); if (agent == null) agent = carInstance.AddComponent<RacingAgent>();
        agent.vehicleController = carInstance.GetComponent<VehicleController>(); agent.inputManager = carInstance.GetComponent<InputManager>();
        agent.inputManager.EnableMLAgentControl(true); // Ensure AI control is enabled
        agent.boundaryDetector = this.boundaryDetector; agent.checkpointGenerator = this.checkpointGenerator;
        agent.trackLayer = this.boundaryDetector.trackLayer; agent.boundaryWallLayer = this.boundaryDetector.boundaryWallLayer;
        BehaviorParameters bp = carInstance.GetComponent<BehaviorParameters>(); if (bp == null) bp = carInstance.AddComponent<BehaviorParameters>();
        bp.BehaviorName = "RacingBehavior";
        // *** Recommendation: Configure BehaviorParameters Manually on Prefab ***
        // ConfigureBehaviorSpaces(bp, agent); // Keep commented out if manual setup is preferred
        if (enableSelfPlay) { bp.TeamId = agentIndex % 2; }
        DecisionRequester dr = carInstance.GetComponent<DecisionRequester>(); if (dr == null) dr = carInstance.AddComponent<DecisionRequester>();
        dr.DecisionPeriod = 5; dr.TakeActionsBetweenDecisions = true;
        Debug.Log($"[CarSpawnerManager] Configured agent components for {carInstance.name}.");
        return agent;
    }

    private void SpawnCars()
    {
        if (carPrefab == null || activeSpawnPoints == null || activeSpawnPoints.Length == 0)
        {
            Debug.LogError("[CarSpawnerManager] Cannot spawn cars - prefab or active spawn points missing.", this); return;
        }
        if (carPrefab.GetComponent<VehicleController>() == null || carPrefab.GetComponent<InputManager>() == null) { Debug.LogError($"[CarSpawnerManager] Car Prefab missing VehicleController or InputManager!", this); return; }

        Debug.Log($"[CarSpawnerManager] Spawning {numberOfCars} agents...");
        for (int i = 0; i < numberOfCars; i++)
        {
            Transform spawnPoint = SelectSpawnPoint(i); // Uses activeSpawnPoints
            if (spawnPoint == null)
            { // Safety check
                Debug.LogError($"[CarSpawnerManager] Failed to select a valid spawn point for agent {i}. Skipping."); continue;
            }

            GameObject carInstance = Instantiate(carPrefab, spawnPoint.position, spawnPoint.rotation);
            carInstance.name = $"{carPrefab.name}_Agent_{i}"; carInstance.transform.SetParent(this.transform);
            SetLayerRecursively(carInstance, carLayerIndex); // Set layer for car and children

            RacingAgent agent = SetupAgentComponents(carInstance, i);
            if (agent != null) { agents.Add(agent); spawnedCars.Add(carInstance); }
            else { Debug.LogError($"[CarSpawnerManager] Failed setup for {carInstance.name}. Destroying.", carInstance); Destroy(carInstance); }
        }
        if (preventCollisionsBetweenAgents && agents.Count > 1) { SetupAgentCollisionPrevention(); }
        Debug.Log($"[CarSpawnerManager] Successfully spawned {agents.Count} agents.");
    }

    // Holds the actual spawn points being used

    void Start()
    {
        carLayerIndex = LayerMaskHelper.GetLayerIndexFromMask(carLayer);
        if (carLayerIndex < 0) { Debug.LogError($"[CarSpawnerManager] Invalid Car Layer.", this); enabled = false; return; }
        Debug.Log($"[CarSpawnerManager] Using layer '{LayerMask.LayerToName(carLayerIndex)}' for cars.");
        StartCoroutine(InitializeAndSpawn());
    }

    private IEnumerator WaitForDependencies()
    {
        Debug.Log("[CarSpawnerManager] Waiting for dependencies...");
        if (boundaryDetector == null) boundaryDetector = FindObjectOfType<TrackBoundaryDetector>();
        if (checkpointGenerator == null) checkpointGenerator = FindObjectOfType<CheckpointGenerator>();
        if (boundaryDetector == null || checkpointGenerator == null) { Debug.LogError("[CarSpawnerManager] Missing TrackBoundaryDetector or CheckpointGenerator!", this); enabled = false; yield break; }

        // Wait for Boundary Detector Scan
        while (!boundaryDetector.HasScanned) { Debug.Log("[CarSpawnerManager] Waiting for TrackBoundaryDetector..."); yield return new WaitForSeconds(0.5f); }
        Debug.Log("[CarSpawnerManager] TrackBoundaryDetector ready.");

        // Wait for Checkpoint Generator Initialization (which includes spawn point generation if enabled)
        // *** MODIFIED: Use IsFullyGenerated if using generated spawns ***
        if (useGeneratedSpawnPoints && checkpointGenerator.generateSpawnPoints)
        {
            while (!checkpointGenerator.IsFullyGenerated) { Debug.Log("[CarSpawnerManager] Waiting for CheckpointGenerator (including Spawns)..."); yield return new WaitForSeconds(0.5f); }
        }
        else
        {
            while (!checkpointGenerator.IsInitialized) { Debug.Log("[CarSpawnerManager] Waiting for CheckpointGenerator (Checkpoints only)..."); yield return new WaitForSeconds(0.5f); }
        }
        Debug.Log("[CarSpawnerManager] CheckpointGenerator ready.");
        Debug.Log("[CarSpawnerManager] All dependencies satisfied.");
    }

    private static class LayerMaskHelper
    {
        public static int GetLayerIndexFromMask(LayerMask lm) { int v = lm.value, i = -1, c = 0; while (v > 0 && c < 32) { if ((v & 1) == 1) { if (i != -1) return -2; i = c; } v >>= 1; c++; } return i; }
    }
}