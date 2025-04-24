using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Visualizes boundary walls, checkpoints, and raycasts from the car in watch mode.
/// This script should be attached to a GameObject in the scene.
/// </summary>
public class WatchModeVisualizer : MonoBehaviour
{
    [Header("References")]
    private TrackBoundaryDetector boundaryDetector;
    private CheckpointGenerator checkpointGenerator;
    private RacingAgent racingAgent;
    private GameObject aiVehicle;
    private CarSpawnerManager carSpawnerManager;

    [Header("Visualization Settings")]
    [SerializeField] private bool showBoundaryWalls = true;
    [SerializeField] private bool showCheckpoints = false;        // Disabled by default
    [SerializeField] private bool showRaycasts = true;
    [SerializeField] private bool showTrackCenterline = false;    // Disabled by default

    [Header("Boundary Wall Visualization")]
    [SerializeField] private Color innerBoundaryColor = new Color(0f, 0.5f, 1f, 0.8f); // Blue
    [SerializeField] private Color outerBoundaryColor = new Color(1f, 0.3f, 0.3f, 0.8f); // Red
    [SerializeField] private float boundaryLineWidth = 0.5f; // Increased thickness for visibility
    [SerializeField] private float boundaryLineHeight = 10.0f; // Increased height for full walls

    [Header("Checkpoint Visualization")]
    [SerializeField] private Color checkpointColor = new Color(0f, 1f, 0f, 0.6f); // Green
    [SerializeField] private Color nextCheckpointColor = new Color(1f, 1f, 0f, 0.8f); // Yellow
    [SerializeField] private float checkpointWidth = 1f;  // Increased width
    [SerializeField] private float checkpointHeight = 10f; // Increased height

    [Header("Raycast Visualization")]
    [SerializeField] private Color trackRayColor = Color.green;
    [SerializeField] private Color boundaryRayColor = Color.magenta;
    [SerializeField] private Color otherRayColor = Color.yellow;
    [SerializeField] private float raycastDuration = 0.05f; // How long the rays stay visible

    [Header("Raycast Line Settings")]
    [Tooltip("Width of the persistent raycast lines")]
    [SerializeField] private float rayLineWidth = 0.05f;
    [Tooltip("Color for raycasts that don't hit anything")]
    [SerializeField] private Color rayMissColor = Color.gray;
    private List<LineRenderer> raycastLines = new List<LineRenderer>();

    [Header("Hitpoint Visualization")]
    [Tooltip("Show spheres at each raycast hit point")]
    [SerializeField] private bool showHitPoints = false;
    [Tooltip("Prefab for hit point marker; if null, a sphere will be created")]
    [SerializeField] private GameObject hitPointPrefab;
    [Tooltip("Size of the hit point marker spheres")]
    [SerializeField] private float hitPointSize = 0.2f;
    private List<GameObject> hitPointObjects = new List<GameObject>();

    [Header("UI Elements")]

    // Private variables
    private LineRenderer innerBoundaryLine;
    private LineRenderer outerBoundaryLine;
    private LineRenderer centerlineLine;
    private List<LineRenderer> checkpointLines = new List<LineRenderer>();
    private int currentCheckpointIndex = 0;
    private bool isWatchMode = false;

    // Add these private fields for bottom and vertical boundary lines:
    private LineRenderer innerBoundaryBottomLine;
    private LineRenderer outerBoundaryBottomLine;
    private List<LineRenderer> innerBoundaryVerticalLines = new List<LineRenderer>();
    private List<LineRenderer> outerBoundaryVerticalLines = new List<LineRenderer>();

    // --- Public Properties for State Access ---
    public bool ShowBoundaryWalls => showBoundaryWalls;
    public bool ShowCheckpoints => showCheckpoints;
    public bool ShowRaycasts => showRaycasts;
    public bool ShowTrackCenterline => showTrackCenterline;
    public bool ShowHitPoints => showHitPoints;

    private void Awake()
    {
        // Check if we're in watch mode
        isWatchMode = PlayerPrefs.GetInt("GameMode", 0) == 1;

        if (!isWatchMode)
        {
            // If not in watch mode, disable this component
            this.enabled = false;
            return;
        }

        // Enforce only boundaries and raycasts for visualization
        showCheckpoints = false;
        showTrackCenterline = false;
        showHitPoints = false;
    }

    private void Start()
    {
        // Find required scene components
        boundaryDetector = FindObjectOfType<TrackBoundaryDetector>();
        if (boundaryDetector == null)
        {
            Debug.LogError("[WatchModeVisualizer] TrackBoundaryDetector not found in scene! Disabling visualizer.", this);
            enabled = false;
            return;
        }

        checkpointGenerator = FindObjectOfType<CheckpointGenerator>();
        if (checkpointGenerator == null)
        {
            Debug.LogError("[WatchModeVisualizer] CheckpointGenerator not found in scene! Disabling visualizer.", this);
            enabled = false;
            return;
        }

        carSpawnerManager = FindObjectOfType<CarSpawnerManager>();
        if (carSpawnerManager == null)
        {
            Debug.LogError("[WatchModeVisualizer] CarSpawnerManager not found in scene! Cannot find AI vehicle. Disabling visualizer.", this);
            enabled = false;
            return;
        }

        // Create line renderers
        CreateLineRenderers();

        // Start initialization coroutine
        StartCoroutine(InitializeVisualizations());
    }

    private void Update()
    {
        // Debug: Confirm script is running
        // Debug.Log("[WatchModeVisualizer] Update Running"); 

        if (!isWatchMode) return;

        // Update current checkpoint index from racing agent
        if (racingAgent != null)
        {
            currentCheckpointIndex = racingAgent.CurrentCheckpoint;
        }

        // Update checkpoint visualization colors
        UpdateCheckpointColors();

        // After toggles, update persistent raycast visuals
        UpdateRaycastLines();
    }

    private void CreateLineRenderers()
    {
        // Create inner boundary top line renderer
        GameObject innerBoundaryObj = new GameObject("InnerBoundaryLineTop");
        innerBoundaryObj.transform.SetParent(transform);
        innerBoundaryLine = innerBoundaryObj.AddComponent<LineRenderer>();
        SetupLineRenderer(innerBoundaryLine, innerBoundaryColor, boundaryLineWidth, true);

        // Create inner boundary bottom line renderer
        GameObject innerBottomObj = new GameObject("InnerBoundaryLineBottom");
        innerBottomObj.transform.SetParent(transform);
        innerBoundaryBottomLine = innerBottomObj.AddComponent<LineRenderer>();
        SetupLineRenderer(innerBoundaryBottomLine, innerBoundaryColor, boundaryLineWidth, true);

        // Create outer boundary top line renderer
        GameObject outerBoundaryObj = new GameObject("OuterBoundaryLineTop");
        outerBoundaryObj.transform.SetParent(transform);
        outerBoundaryLine = outerBoundaryObj.AddComponent<LineRenderer>();
        SetupLineRenderer(outerBoundaryLine, outerBoundaryColor, boundaryLineWidth, true);

        // Create outer boundary bottom line renderer
        GameObject outerBottomObj = new GameObject("OuterBoundaryLineBottom");
        outerBottomObj.transform.SetParent(transform);
        outerBoundaryBottomLine = outerBottomObj.AddComponent<LineRenderer>();
        SetupLineRenderer(outerBoundaryBottomLine, outerBoundaryColor, boundaryLineWidth, true);

        // Create centerline line renderer
        GameObject centerlineObj = new GameObject("CenterlineLine");
        centerlineObj.transform.SetParent(transform);
        centerlineLine = centerlineObj.AddComponent<LineRenderer>();
        SetupLineRenderer(centerlineLine, checkpointColor, boundaryLineWidth / 2, true);

        // Update hit points visibility
        foreach (var hp in hitPointObjects)
        {
            if (hp != null) hp.SetActive(showHitPoints);
        }
        // Actual positioning happens in UpdateRaycastLines
    }

    private void SetupLineRenderer(LineRenderer lineRenderer, Color color, float width, bool loop = false)
    {
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;

        // Use a simple Unlit shader for reliable color rendering
        Shader unlitShader = Shader.Find("Unlit/Color");
        if (unlitShader != null)
        {
            lineRenderer.material = new Material(unlitShader);
            lineRenderer.material.color = color; // Set color on the material
        }
        else
        {
            Debug.LogError("[WatchModeVisualizer] Could not find 'Unlit/Color' shader. Lines may not render correctly.");
            lineRenderer.material = new Material(Shader.Find("Sprites/Default")); // Fallback
        }

        // Colors are now set via material tint
        lineRenderer.startColor = Color.white; // Use white so material color shows through
        lineRenderer.endColor = Color.white;

        lineRenderer.loop = loop;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 0;
    }

    private IEnumerator InitializeVisualizations()
    {
        Debug.Log("[WatchModeVisualizer] Starting InitializeVisualizations Coroutine...");
        // Wait for boundary detector to finish scanning
        if (boundaryDetector != null)
        {
            while (!boundaryDetector.HasScanned)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }

        // Wait for checkpoint generator to initialize
        if (checkpointGenerator != null)
        {
            while (checkpointGenerator.Checkpoints.Count == 0)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }

        // Wait for RacingAgent to spawn via CarSpawnerManager
        while (carSpawnerManager.SpawnedAgents == null || carSpawnerManager.SpawnedAgents.Count == 0)
        {
            Debug.Log("[WatchModeVisualizer] Waiting for Racing Agent to spawn...");
            yield return new WaitForSeconds(0.5f);
        }
        racingAgent = carSpawnerManager.SpawnedAgents[0];
        aiVehicle = racingAgent.gameObject;
        Debug.Log($"[WatchModeVisualizer] Assigned RacingAgent: {racingAgent.name}");

        // Now that we have the agent, create raycast lines and hit point objects
        CreateRaycastLines();
        if (showHitPoints)
            CreateHitPointObjects();

        // Initialize boundary walls visualization
        if (boundaryDetector != null && boundaryDetector.HasScanned)
        {
            InitializeBoundaryWalls();
        }

        // Initialize checkpoints visualization if enabled
        if (showCheckpoints && checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > 0)
        {
            InitializeCheckpoints();
        }

        // Initialize centerline visualization if enabled
        if (showTrackCenterline && checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > 0)
        {
            InitializeCenterline();
        }

        // Enable raycast visualization in the racing agent
        if (racingAgent != null)
        {
            racingAgent.showRaycasts = showRaycasts;
            racingAgent.showUpcomingCheckpoints = showCheckpoints;  // no effect if disabled
        }

        // Update visibility based on toggle settings
        UpdateVisualizationVisibility();
        Debug.Log("[WatchModeVisualizer] Initial visualizations setup complete.");
    }

    private void InitializeBoundaryWalls()
    {
        // Ensure checkpoints are hidden
        if (checkpointLines != null)
        {
            foreach (var line in checkpointLines)
                if (line != null) Destroy(line.gameObject);
            checkpointLines.Clear();
        }
        if (innerBoundaryLine == null || outerBoundaryLine == null)
        {
            Debug.LogError("[WatchModeVisualizer] Boundary line renderers are null!");
            return;
        }
        // Get boundary points
        List<Vector3> innerPoints = boundaryDetector.InnerBoundaryPoints;
        List<Vector3> outerPoints = boundaryDetector.OuterBoundaryPoints;

        // Top loops
        if (innerPoints != null && innerPoints.Count > 0)
        {
            innerBoundaryLine.positionCount = innerPoints.Count;
            for (int i = 0; i < innerPoints.Count; i++)
            {
                innerBoundaryLine.SetPosition(i, innerPoints[i] + Vector3.up * boundaryLineHeight);
            }
        }
        if (outerPoints != null && outerPoints.Count > 0)
        {
            outerBoundaryLine.positionCount = outerPoints.Count;
            for (int i = 0; i < outerPoints.Count; i++)
            {
                outerBoundaryLine.SetPosition(i, outerPoints[i] + Vector3.up * boundaryLineHeight);
            }
        }

        // Bottom loops
        if (innerBoundaryBottomLine != null && innerPoints != null && innerPoints.Count > 0)
        {
            innerBoundaryBottomLine.positionCount = innerPoints.Count;
            for (int i = 0; i < innerPoints.Count; i++)
            {
                innerBoundaryBottomLine.SetPosition(i, innerPoints[i]);
            }
        }
        if (outerBoundaryBottomLine != null && outerPoints != null && outerPoints.Count > 0)
        {
            outerBoundaryBottomLine.positionCount = outerPoints.Count;
            for (int i = 0; i < outerPoints.Count; i++)
            {
                outerBoundaryBottomLine.SetPosition(i, outerPoints[i]);
            }
        }

        // Vertical lines: clear old then recreate
        foreach(var lr in innerBoundaryVerticalLines) Destroy(lr.gameObject);
        innerBoundaryVerticalLines.Clear();
        if (innerPoints != null)
        {
            for (int i = 0; i < innerPoints.Count; i++)
            {
                GameObject vertObj = new GameObject($"InnerBoundary_Vertical_{i}");
                vertObj.transform.SetParent(transform);
                LineRenderer lr = vertObj.AddComponent<LineRenderer>();
                SetupLineRenderer(lr, innerBoundaryColor, boundaryLineWidth, false);
                lr.positionCount = 2;
                lr.SetPosition(0, innerPoints[i]);
                lr.SetPosition(1, innerPoints[i] + Vector3.up * boundaryLineHeight);
                innerBoundaryVerticalLines.Add(lr);
            }
        }
        foreach(var lr in outerBoundaryVerticalLines) Destroy(lr.gameObject);
        outerBoundaryVerticalLines.Clear();
        if (outerPoints != null)
        {
            for (int i = 0; i < outerPoints.Count; i++)
            {
                GameObject vertObj = new GameObject($"OuterBoundary_Vertical_{i}");
                vertObj.transform.SetParent(transform);
                LineRenderer lr = vertObj.AddComponent<LineRenderer>();
                SetupLineRenderer(lr, outerBoundaryColor, boundaryLineWidth, false);
                lr.positionCount = 2;
                lr.SetPosition(0, outerPoints[i]);
                lr.SetPosition(1, outerPoints[i] + Vector3.up * boundaryLineHeight);
                outerBoundaryVerticalLines.Add(lr);
            }
        }
    }

    private void InitializeCheckpoints()
    {
        if (checkpointGenerator == null) {
            Debug.LogError("[WatchModeVisualizer] CheckpointGenerator is null, cannot initialize checkpoints.");
            return;
        }

        // Clear existing checkpoint lines
        foreach (var line in checkpointLines)
        {
            if (line != null)
                Destroy(line.gameObject);
        }
        checkpointLines.Clear();

        // Create new checkpoint lines
        var checkpoints = checkpointGenerator.Checkpoints;
        for (int i = 0; i < checkpoints.Count; i++)
        {
            var checkpoint = checkpoints[i];

            // Create checkpoint line renderer
            GameObject checkpointObj = new GameObject($"Checkpoint_{i}");
            checkpointObj.transform.SetParent(transform);
            LineRenderer checkpointLine = checkpointObj.AddComponent<LineRenderer>();

            // Set up line renderer
            SetupLineRenderer(checkpointLine, i == currentCheckpointIndex ? nextCheckpointColor : checkpointColor, checkpointWidth, false);

            // Calculate checkpoint line positions
            Vector3 center = checkpoint.position;
            Vector3 forward = checkpoint.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float width = checkpoint.width;
            if (width <= 0) width = 10f; // Default width if not set

            Vector3 start = center - right * (width / 2);
            Vector3 end = center + right * (width / 2);

            // Set line positions
            checkpointLine.positionCount = 2;
            checkpointLine.SetPosition(0, start + Vector3.up * (checkpointHeight / 2));
            checkpointLine.SetPosition(1, end + Vector3.up * (checkpointHeight / 2));

            // Add to list
            checkpointLines.Add(checkpointLine);
        }
        Debug.Log($"[WatchModeVisualizer] Initialized Checkpoints: {checkpointLines.Count} lines.");
    }

    private void InitializeCenterline()
    {
        if (checkpointGenerator == null) {
             Debug.LogError("[WatchModeVisualizer] CheckpointGenerator is null, cannot initialize centerline.");
             return;
        }
         if (centerlineLine == null) {
             Debug.LogError("[WatchModeVisualizer] Centerline LineRenderer is null!");
             return;
         }

        // Set centerline points from checkpoints
        var checkpoints = checkpointGenerator.Checkpoints;
        if (checkpoints != null && checkpoints.Count > 0)
        {
            Debug.Log("[WatchModeVisualizer] Attempting to initialize Centerline...");
            Debug.Log($"[WatchModeVisualizer] Found {checkpoints.Count} checkpoints for centerline. First CP pos: {checkpoints[0].position}");
            centerlineLine.positionCount = checkpoints.Count;
            for (int i = 0; i < checkpoints.Count; i++)
            {
                centerlineLine.SetPosition(i, checkpoints[i].position + Vector3.up * boundaryLineHeight);
            }
            Debug.Log($"[WatchModeVisualizer] Initialized Centerline: {checkpoints.Count} points.");
        }
         else
        {
             Debug.LogWarning("[WatchModeVisualizer] No checkpoints found for centerline.");
             centerlineLine.positionCount = 0;
        }
    }

    private void UpdateCheckpointColors()
    {
        // Update checkpoint colors based on current checkpoint
        for (int i = 0; i < checkpointLines.Count; i++)
        {
            if (checkpointLines[i] != null)
            {
                checkpointLines[i].startColor = i == currentCheckpointIndex ? nextCheckpointColor : checkpointColor;
                checkpointLines[i].endColor = i == currentCheckpointIndex ? nextCheckpointColor : checkpointColor;
            }
        }
    }

    private void UpdateVisualizationVisibility()
    {
        Debug.Log($"[WatchModeVisualizer] Updating Visibility: Boundaries({ShowBoundaryWalls}), Checkpoints({ShowCheckpoints}), Rays({ShowRaycasts}), Centerline({ShowTrackCenterline}), HitPoints({ShowHitPoints})");
        // Toggle line renderers
        if (innerBoundaryLine != null) innerBoundaryLine.enabled = ShowBoundaryWalls;
        if (outerBoundaryLine != null) outerBoundaryLine.enabled = ShowBoundaryWalls;
        if (innerBoundaryBottomLine != null) innerBoundaryBottomLine.enabled = ShowBoundaryWalls;
        if (outerBoundaryBottomLine != null) outerBoundaryBottomLine.enabled = ShowBoundaryWalls;
        foreach(var lr in innerBoundaryVerticalLines) if(lr!=null) lr.enabled = ShowBoundaryWalls;
        foreach(var lr in outerBoundaryVerticalLines) if(lr!=null) lr.enabled = ShowBoundaryWalls;

        // Update checkpoints visibility
        foreach (var line in checkpointLines)
        {
            if (line != null)
                line.enabled = ShowCheckpoints;
        }

        // Update centerline visibility
        if (centerlineLine != null)
            centerlineLine.enabled = ShowTrackCenterline;

        // Update hit points visibility (only if raycasts are also shown)
        bool showHPs = ShowHitPoints && ShowRaycasts;
        foreach (var hp in hitPointObjects)
        {
            if (hp != null)
            {
                // Ensure hit point is only active if BOTH hitpoints AND raycasts are enabled
                hp.SetActive(showHPs);
                // Debug.Log($"Setting HP {hp.name} active state to {showHPs}");
            }
        }
        // Actual hit point positioning happens in UpdateRaycastLines

        // Update persistent raycast line visibility
        foreach(var line in raycastLines)
        {
            if (line != null) line.enabled = ShowRaycasts;
        }
    }

    // Public methods for UI buttons
    public void ToggleBoundaryWalls()
    {
        showBoundaryWalls = !showBoundaryWalls;
        UpdateVisualizationVisibility();
        Debug.Log($"[WatchModeVisualizer] Toggled Boundary Walls to: {showBoundaryWalls}");
    }

    public void ToggleCheckpoints()
    {
        showCheckpoints = !showCheckpoints;
        UpdateVisualizationVisibility();
        Debug.Log($"[WatchModeVisualizer] Toggled Checkpoints to: {showCheckpoints}");

        if (racingAgent != null)
            racingAgent.showUpcomingCheckpoints = showCheckpoints;
    }

    public void ToggleRaycasts()
    {
        showRaycasts = !showRaycasts;
        UpdateVisualizationVisibility(); // Update all visuals based on new state
        Debug.Log($"[WatchModeVisualizer] Toggled Raycasts to: {showRaycasts}");

        if (racingAgent != null)
            racingAgent.showRaycasts = showRaycasts;
    }

    public void ToggleTrackCenterline()
    {
        showTrackCenterline = !showTrackCenterline;
        UpdateVisualizationVisibility();
        Debug.Log($"[WatchModeVisualizer] Toggled Centerline to: {showTrackCenterline}");
    }

    // New public method for hitpoint toggle
    public void ToggleHitPoints()
    {
        showHitPoints = !showHitPoints;
        UpdateVisualizationVisibility();
        Debug.Log($"[WatchModeVisualizer] Toggled HitPoints to: {showHitPoints}");
    }

    /// <summary>
    /// Pre-create line renderers for each ray to visualize in game view.
    /// </summary>
    private void CreateRaycastLines()
    {
        if (racingAgent == null || aiVehicle == null) return;
        int count = racingAgent.numRaycasts;
        for (int i = 0; i < count; i++)
        {
            GameObject obj = new GameObject($"RayLine_{i}");
            obj.transform.SetParent(transform);
            LineRenderer lr = obj.AddComponent<LineRenderer>();
            SetupLineRenderer(lr, rayMissColor, rayLineWidth, false);
            lr.positionCount = 2;
            raycastLines.Add(lr);
        }
    }

    /// <summary>
    /// Update the positions and colors of persistent raycast lines.
    /// </summary>
    private void UpdateRaycastLines()
    {
        // Optimization: If raycasts and hitpoints are both disabled, skip raycasting logic
        if (!ShowRaycasts && !ShowHitPoints)
        {
            // Ensure lines and points are hidden if toggled off mid-frame
             if(raycastLines.Count > 0 && raycastLines[0].enabled) // Quick check if they might be on
             {
                 UpdateVisualizationVisibility();
             }
             return;
        }

        // Ensure dependencies are ready
        if (racingAgent == null || aiVehicle == null || !racingAgent.IsDependenciesReady)
        {
            // Hide lines if not ready or not showing raycasts
            foreach (var line in raycastLines)
            {
                if (line != null) line.enabled = false;
            }
            return;
        }

        // Access raycast parameters from RacingAgent
        float verticalOffset = racingAgent.raycastVerticalOffset;
        float angleRange = racingAgent.raycastAngle;
        float distance = racingAgent.raycastDistance;

        Vector3 origin = aiVehicle.transform.position + Vector3.up * verticalOffset;
        int count = racingAgent.numRaycasts;
        float step = (count > 1) ? angleRange / (count - 1) : 0f;
        float startAngle = (count > 1) ? -angleRange / 2f : 0f;

        // Check if raycastLines list matches numRaycasts
        if (raycastLines.Count != count)
        {
            // If mismatch, clear and recreate lines (should ideally not happen often)
            foreach (var line in raycastLines)
            {
                if (line != null) Destroy(line.gameObject);
            }
            raycastLines.Clear();
            CreateRaycastLines();

            // Check again, if still mismatch, log error and return
            if (raycastLines.Count != count)
            {
                 Debug.LogError("[WatchModeVisualizer] Raycast line count mismatch after recreating!");
                 return;
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (i >= raycastLines.Count) {
                Debug.LogError($"[WatchModeVisualizer] Index out of bounds: {i} >= {raycastLines.Count}");
                continue;
            }
            float angle = startAngle + i * step;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * aiVehicle.transform.forward;
            RaycastHit hit;
            bool didHit = Physics.Raycast(origin, dir, out hit, distance, racingAgent.raycastMask);
            LineRenderer lr = raycastLines[i];

            // Determine color based on hit type
            Color c;
            if (didHit)
            {
                int layer = hit.collider.gameObject.layer;
                if (((1 << layer) & racingAgent.trackLayer) != 0)
                    c = trackRayColor;
                else if (layer == racingAgent.BoundaryWallLayerIndex)
                    c = boundaryRayColor;
                else
                    c = otherRayColor;
            }
            else
            {
                c = rayMissColor;
            }

            // Update line renderer only if raycasts are shown
            if (ShowRaycasts)
            {
                lr.startColor = c;
                lr.endColor = c;
                lr.SetPosition(0, origin);
                lr.SetPosition(1, didHit ? hit.point : origin + dir * distance);
                lr.enabled = true;
            }
            else
            {
                 lr.enabled = false; // Ensure line is hidden if raycasts turned off
            }

            // Show or hide hit point markers
            if (hitPointObjects.Count > i)
            {
                GameObject hp = hitPointObjects[i];
                if (showHitPoints && didHit)
                {
                    hp.SetActive(true);
                    hp.transform.position = hit.point;
                }
                else
                {
                    hp.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Create marker objects to indicate raycast hit points.
    /// </summary>
    private void CreateHitPointObjects()
    {
        int count = racingAgent != null ? racingAgent.numRaycasts : 0;
        for (int i = 0; i < count; i++)
        {
            GameObject hp;
            if (hitPointPrefab != null)
            {
                hp = Instantiate(hitPointPrefab, transform);
            }
            else
            {
                hp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hp.transform.SetParent(transform);
                Destroy(hp.GetComponent<Collider>());
            }
            hp.transform.localScale = Vector3.one * hitPointSize;
            hp.SetActive(false);
            hitPointObjects.Add(hp);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !isWatchMode) return;

        // Draw boundary walls
        if (boundaryDetector != null && ShowBoundaryWalls)
        {
            Gizmos.color = innerBoundaryColor;
            var inner = boundaryDetector.InnerBoundaryPoints;
            for (int i = 0; i < inner.Count; i++)
            {
                Gizmos.DrawLine(inner[i], inner[(i + 1) % inner.Count]);
                Gizmos.DrawLine(inner[i], inner[i] + Vector3.up * boundaryLineHeight);
            }
            Gizmos.color = outerBoundaryColor;
            var outer = boundaryDetector.OuterBoundaryPoints;
            for (int i = 0; i < outer.Count; i++)
            {
                Gizmos.DrawLine(outer[i], outer[(i + 1) % outer.Count]);
                Gizmos.DrawLine(outer[i], outer[i] + Vector3.up * boundaryLineHeight);
            }
        }

        // Draw checkpoints
        if (checkpointGenerator != null && ShowCheckpoints)
        {
            Gizmos.color = checkpointColor;
            foreach (var cp in checkpointGenerator.Checkpoints)
            {
                Vector3 center = cp.position + Vector3.up * (checkpointHeight / 2f);
                Vector3 right = Vector3.Cross(Vector3.up, cp.forward).normalized;
                float width = cp.width > 0 ? cp.width : 10f;
                Vector3 a = center - right * (width / 2f);
                Vector3 b = center + right * (width / 2f);
                Gizmos.DrawLine(a, b);
            }
        }

        // Draw centerline
        if (checkpointGenerator != null && ShowTrackCenterline)
        {
            Gizmos.color = checkpointColor;
            var cps = checkpointGenerator.Checkpoints;
            for (int i = 0; i < cps.Count - 1; i++)
            {
                Gizmos.DrawLine(cps[i].position, cps[i + 1].position);
            }
        }

        // Draw raycasts and hit points
        if (racingAgent != null && ShowRaycasts)
        {
            Vector3 origin = aiVehicle.transform.position + Vector3.up * racingAgent.raycastVerticalOffset;
            int count = racingAgent.numRaycasts;
            float angleRange = racingAgent.raycastAngle;
            float step = count > 1 ? angleRange / (count - 1) : 0f;
            float startAng = count > 1 ? -angleRange / 2f : 0f;
            for (int i = 0; i < count; i++)
            {
                float ang = startAng + i * step;
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * aiVehicle.transform.forward;
                if (Physics.Raycast(origin, dir, out var hit, racingAgent.raycastDistance, racingAgent.raycastMask))
                {
                    int layer = hit.collider.gameObject.layer;
                    Gizmos.color = ((1 << layer) & racingAgent.trackLayer) != 0 ? trackRayColor :
                                  (layer == racingAgent.BoundaryWallLayerIndex ? boundaryRayColor : otherRayColor);
                    Gizmos.DrawLine(origin, hit.point);
                    if (ShowHitPoints) Gizmos.DrawSphere(hit.point, hitPointSize);
                }
                else
                {
                    Gizmos.color = rayMissColor;
                    Gizmos.DrawLine(origin, origin + dir * racingAgent.raycastDistance);
                }
            }
        }
    }
#endif
}
