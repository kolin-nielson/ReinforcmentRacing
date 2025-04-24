using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// All-in-one manager for watch mode visualization.
/// This script automatically sets up visualization for boundary walls, checkpoints, and raycasts.
/// Just add this script to any GameObject in the scene and everything will be set up automatically.
/// </summary>
public class WatchModeManager : MonoBehaviour
{
    [Header("Visualization Settings")]
    [SerializeField] private bool showBoundaryWalls = true;
    [SerializeField] private bool showCheckpoints = true;
    [SerializeField] private bool showRaycasts = true;
    [SerializeField] private bool showTrackCenterline = true;
    [SerializeField] private bool showUI = true;

    [Header("Boundary Wall Visualization")]
    [SerializeField] private Color innerBoundaryColor = new Color(0f, 0.7f, 1f, 0.8f); // Bright blue
    [SerializeField] private Color outerBoundaryColor = new Color(1f, 0.3f, 0.3f, 0.8f); // Red
    [SerializeField] private float boundaryLineWidth = 0.2f;
    [SerializeField] private float boundaryLineHeight = 3.0f; // Increased height for better visibility
    [SerializeField] private float boundaryEmissionIntensity = 2.5f; // Glow intensity
    [SerializeField] private float boundaryWireSpacing = 2.0f; // Wireframe grid spacing
    [SerializeField] private float boundaryWireThickness = 0.1f; // Wireframe grid thickness

    [Header("Checkpoint Visualization")]
    [SerializeField] private Color checkpointColor = new Color(0f, 1f, 0f, 0.6f); // Green
    [SerializeField] private Color nextCheckpointColor = new Color(1f, 1f, 0f, 0.8f); // Yellow
    [SerializeField] private float checkpointWidth = 0.3f;
    [SerializeField] private float checkpointHeight = 5f;
    [SerializeField] private float checkpointEmissionIntensity = 3.0f; // Glow intensity
    [SerializeField] private float checkpointScanLineSpeed = 2.0f; // Speed of scan line effect
    [SerializeField] private float checkpointGridSize = 2.0f; // Size of grid pattern

    [Header("Raycast Visualization")]
    [SerializeField] private Color trackRayColor = new Color(0f, 1f, 0.5f, 0.8f); // Bright green
    [SerializeField] private Color boundaryRayColor = new Color(1f, 0.3f, 1f, 0.8f); // Bright magenta
    [SerializeField] private Color otherRayColor = new Color(1f, 1f, 0.3f, 0.8f); // Bright yellow
    [SerializeField] private float raycastDuration = 0.2f; // How long the rays stay visible
    [SerializeField] private float raycastWidth = 0.1f; // Width of raycast lines
    [SerializeField] private float raycastEmissionIntensity = 3.0f; // Glow intensity
    [SerializeField] private float raycastFlowSpeed = 3.0f; // Speed of flow effect

    [Header("UI Settings")]
    [SerializeField] private Vector2 panelSize = new Vector2(300, 200);
    [SerializeField] private Vector2 panelPosition = new Vector2(20, 20);
    [SerializeField] private Color panelColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color toggleColor = Color.white;

    // Private references
    private TrackBoundaryDetector boundaryDetector;
    private CheckpointGenerator checkpointGenerator;
    private RacingAgent racingAgent;
    private GameObject aiVehicle;
    private Canvas uiCanvas;

    // Visualization objects
    private LineRenderer innerBoundaryLine;
    private LineRenderer outerBoundaryLine;
    private LineRenderer centerlineLine;
    private List<LineRenderer> checkpointLines = new List<LineRenderer>();
    private int currentCheckpointIndex = 0;

    // UI elements
    private GameObject watchModePanel;
    private GameObject helpPanel;
    private Toggle boundaryToggle;
    private Toggle checkpointToggle;
    private Toggle raycastToggle;
    private Toggle centerlineToggle;

    // State
    private bool isWatchMode = false;
    private bool isPanelVisible = true;
    private bool isHelpVisible = false;

    private void Awake()
    {
        // Check if we're in watch mode
        isWatchMode = PlayerPrefs.GetInt("GameMode", 0) == 1;

        if (!isWatchMode)
        {
            // If not in watch mode, disable this component
            this.enabled = false;
            Debug.Log("[WatchModeManager] Not in watch mode. Disabling visualization.");
            return;
        }

        Debug.Log("[WatchModeManager] Watch mode detected. Setting up visualization...");
    }

    private void Start()
    {
        // Initialize everything
        StartCoroutine(Initialize());
    }

    private IEnumerator Initialize()
    {
        // Find all required components
        yield return StartCoroutine(FindComponents());

        // Create visualization objects
        CreateVisualizationObjects();

        // Create UI
        if (showUI)
        {
            CreateUI();
        }

        // Enable raycast visualization in the racing agent
        if (racingAgent != null)
        {
            racingAgent.showRaycasts = showRaycasts;
            racingAgent.showUpcomingCheckpoints = showCheckpoints;
            racingAgent.showCenterlineGizmo = showTrackCenterline;
            racingAgent.showRecoveryHelpers = true;

            // Add enhanced raycast visualizer
            if (!racingAgent.gameObject.GetComponent<RaycastVisualizer>())
            {
                RaycastVisualizer visualizer = racingAgent.gameObject.AddComponent<RaycastVisualizer>();
                Debug.Log("[WatchModeManager] Added RaycastVisualizer to racing agent.");
            }
        }

        // Initialize visualizations
        yield return StartCoroutine(InitializeVisualizations());

        // Fix checkpoint initialization for the car's spawn position
        yield return StartCoroutine(FixCheckpointInitialization());

        Debug.Log("[WatchModeManager] Watch mode visualization initialized successfully.");
    }

    private IEnumerator FindComponents()
    {
        // Find boundary detector
        while (boundaryDetector == null)
        {
            boundaryDetector = FindObjectOfType<TrackBoundaryDetector>();
            if (boundaryDetector == null)
            {
                Debug.Log("[WatchModeManager] Waiting for TrackBoundaryDetector...");
                yield return new WaitForSeconds(0.5f);
            }
        }

        // Find checkpoint generator
        while (checkpointGenerator == null)
        {
            checkpointGenerator = FindObjectOfType<CheckpointGenerator>();
            if (checkpointGenerator == null)
            {
                Debug.Log("[WatchModeManager] Waiting for CheckpointGenerator...");
                yield return new WaitForSeconds(0.5f);
            }
        }

        // Find racing agent
        while (racingAgent == null)
        {
            racingAgent = FindObjectOfType<RacingAgent>();
            if (racingAgent == null)
            {
                Debug.Log("[WatchModeManager] Waiting for RacingAgent...");
                yield return new WaitForSeconds(0.5f);
            }
        }

        // Set AI vehicle
        aiVehicle = racingAgent.gameObject;

        // Find or create canvas for UI
        uiCanvas = FindObjectOfType<Canvas>();
        if (uiCanvas == null)
        {
            GameObject canvasObj = new GameObject("WatchModeCanvas");
            uiCanvas = canvasObj.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            Debug.Log("[WatchModeManager] Created new Canvas for UI.");
        }

        Debug.Log("[WatchModeManager] All required components found.");
    }

    private void CreateVisualizationObjects()
    {
        // Create parent object for visualization
        GameObject visualizationParent = new GameObject("WatchModeVisualization");
        visualizationParent.transform.SetParent(transform);

        // Create inner boundary line renderer
        GameObject innerBoundaryObj = new GameObject("InnerBoundaryLine");
        innerBoundaryObj.transform.SetParent(visualizationParent.transform);
        innerBoundaryLine = innerBoundaryObj.AddComponent<LineRenderer>();
        SetupLineRenderer(innerBoundaryLine, innerBoundaryColor, boundaryLineWidth, false); // Set loop to false for vertical walls
        // Additional settings for vertical walls
        innerBoundaryLine.numCornerVertices = 4; // Smooth corners
        innerBoundaryLine.textureMode = LineTextureMode.Tile;

        // Create outer boundary line renderer
        GameObject outerBoundaryObj = new GameObject("OuterBoundaryLine");
        outerBoundaryObj.transform.SetParent(visualizationParent.transform);
        outerBoundaryLine = outerBoundaryObj.AddComponent<LineRenderer>();
        SetupLineRenderer(outerBoundaryLine, outerBoundaryColor, boundaryLineWidth, false); // Set loop to false for vertical walls
        // Additional settings for vertical walls
        outerBoundaryLine.numCornerVertices = 4; // Smooth corners
        outerBoundaryLine.textureMode = LineTextureMode.Tile;

        // Create centerline line renderer
        GameObject centerlineObj = new GameObject("CenterlineLine");
        centerlineObj.transform.SetParent(visualizationParent.transform);
        centerlineLine = centerlineObj.AddComponent<LineRenderer>();
        SetupLineRenderer(centerlineLine, checkpointColor, boundaryLineWidth / 2, true);

        Debug.Log("[WatchModeManager] Visualization objects created.");
    }

    private void SetupLineRenderer(LineRenderer lineRenderer, Color color, float width, bool loop = false)
    {
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;

        // Create a texture generator if needed
        if (!GameObject.Find("TextureGenerator"))
        {
            GameObject textureGenObj = new GameObject("TextureGenerator");
            textureGenObj.AddComponent<TextureGenerator>();
        }

        // Choose the appropriate shader based on the object type
        Material material = null;

        // For boundary walls
        if (lineRenderer.gameObject.name.Contains("Boundary"))
        {
            Shader wireframeShader = Shader.Find("Custom/WireframeGlow");
            if (wireframeShader != null)
            {
                material = new Material(wireframeShader);
                material.SetColor("_Color", color);
                material.SetColor("_EmissionColor", color);
                material.SetFloat("_EmissionIntensity", boundaryEmissionIntensity);
                material.SetFloat("_WireSpacing", boundaryWireSpacing);
                material.SetFloat("_WireThickness", boundaryWireThickness);
                material.SetFloat("_PatternScale", 0.5f);
                material.SetFloat("_PatternSpeed", 0.2f);

                // Try to find the noise texture
                Texture2D noiseTexture = Resources.Load<Texture2D>("NoiseTexture");
                if (noiseTexture == null)
                {
                    // Create a simple noise texture if not found
                    noiseTexture = new Texture2D(256, 256);
                    for (int y = 0; y < 256; y++)
                    {
                        for (int x = 0; x < 256; x++)
                        {
                            float noise = Mathf.PerlinNoise(x / 256f * 4f, y / 256f * 4f);
                            noiseTexture.SetPixel(x, y, new Color(noise, noise, noise, 1f));
                        }
                    }
                    noiseTexture.Apply();
                }
                material.SetTexture("_PatternTex", noiseTexture);
            }
        }
        // For checkpoints
        else if (lineRenderer.gameObject.name.Contains("Checkpoint"))
        {
            Shader checkpointShader = Shader.Find("Custom/CheckpointGlow");
            if (checkpointShader != null)
            {
                material = new Material(checkpointShader);
                material.SetColor("_Color", color);
                material.SetColor("_EmissionColor", color);
                material.SetFloat("_EmissionIntensity", checkpointEmissionIntensity);
                material.SetFloat("_ScanLineSpeed", checkpointScanLineSpeed);
                material.SetFloat("_ScanLineWidth", 0.1f);
                material.SetFloat("_ScanLineIntensity", 1.5f);
                material.SetFloat("_GridSize", checkpointGridSize);
                material.SetFloat("_GridThickness", 0.05f);
            }
        }
        // For direction indicators or centerline
        else if (lineRenderer.gameObject.name.Contains("Direction") || lineRenderer.gameObject.name.Contains("Centerline"))
        {
            Shader glowingShader = Shader.Find("Custom/GlowingLine");
            if (glowingShader != null)
            {
                material = new Material(glowingShader);
                material.SetColor("_Color", color);
                material.SetColor("_EmissionColor", color);
                material.SetFloat("_EmissionIntensity", 2.5f);
            }
        }
        // Default fallback
        if (material == null)
        {
            Shader glowingShader = Shader.Find("Custom/GlowingLine");
            if (glowingShader != null)
            {
                material = new Material(glowingShader);
                material.SetColor("_Color", color);
                material.SetColor("_EmissionColor", color);
                material.SetFloat("_EmissionIntensity", 2.0f);
            }
            else
            {
                // Ultimate fallback
                material = new Material(Shader.Find("Sprites/Default"));
                material.color = color;
            }
        }

        // Apply the material
        lineRenderer.material = material;

        // Set other line renderer properties
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.loop = loop;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = 0;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.allowOcclusionWhenDynamic = false;
        lineRenderer.generateLightingData = false;

        // Make lines look smoother
        lineRenderer.numCapVertices = 6; // Increased for smoother caps
        lineRenderer.numCornerVertices = 6; // Increased for smoother corners
        lineRenderer.alignment = LineAlignment.View; // Makes lines face the camera

        // Set texture mode for better quality
        lineRenderer.textureMode = LineTextureMode.Tile;
    }

    private void CreateUI()
    {
        // Create main panel
        watchModePanel = CreatePanel("WatchModePanel", panelPosition, panelSize, panelColor);

        // Create title
        TextMeshProUGUI titleText = CreateText(watchModePanel, "TitleText", "Watch Mode Visualization",
            new Vector2(0, panelSize.y - 30), new Vector2(panelSize.x, 30));
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 18;

        // Create toggles
        float toggleY = panelSize.y - 70;
        float toggleHeight = 30;
        float toggleSpacing = 10;

        boundaryToggle = CreateToggle(watchModePanel, "BoundaryToggle", "Show Boundary Walls",
            new Vector2(20, toggleY), new Vector2(panelSize.x - 40, toggleHeight));
        boundaryToggle.isOn = showBoundaryWalls;
        boundaryToggle.onValueChanged.AddListener((value) => {
            showBoundaryWalls = value;
            UpdateVisualizationVisibility();
        });

        toggleY -= (toggleHeight + toggleSpacing);
        checkpointToggle = CreateToggle(watchModePanel, "CheckpointToggle", "Show Checkpoints",
            new Vector2(20, toggleY), new Vector2(panelSize.x - 40, toggleHeight));
        checkpointToggle.isOn = showCheckpoints;
        checkpointToggle.onValueChanged.AddListener((value) => {
            showCheckpoints = value;
            UpdateVisualizationVisibility();
            if (racingAgent != null)
                racingAgent.showUpcomingCheckpoints = value;
        });

        toggleY -= (toggleHeight + toggleSpacing);
        raycastToggle = CreateToggle(watchModePanel, "RaycastToggle", "Show Raycasts",
            new Vector2(20, toggleY), new Vector2(panelSize.x - 40, toggleHeight));
        raycastToggle.isOn = showRaycasts;
        raycastToggle.onValueChanged.AddListener((value) => {
            showRaycasts = value;
            if (racingAgent != null)
                racingAgent.showRaycasts = value;
        });

        toggleY -= (toggleHeight + toggleSpacing);
        centerlineToggle = CreateToggle(watchModePanel, "CenterlineToggle", "Show Track Centerline",
            new Vector2(20, toggleY), new Vector2(panelSize.x - 40, toggleHeight));
        centerlineToggle.isOn = showTrackCenterline;
        centerlineToggle.onValueChanged.AddListener((value) => {
            showTrackCenterline = value;
            UpdateVisualizationVisibility();
            if (racingAgent != null)
                racingAgent.showCenterlineGizmo = value;
        });

        // Create help text
        toggleY -= (toggleHeight + toggleSpacing);
        TextMeshProUGUI helpInfo = CreateText(watchModePanel, "HelpInfo", "Press H for help, Tab to hide panel",
            new Vector2(20, toggleY), new Vector2(panelSize.x - 40, toggleHeight));
        helpInfo.fontSize = 14;

        // Create help panel
        helpPanel = CreatePanel("HelpPanel", new Vector2(panelPosition.x + panelSize.x + 20, panelPosition.y),
            new Vector2(300, 200), panelColor);

        TextMeshProUGUI helpText = CreateText(helpPanel, "HelpText",
            "Watch Mode Controls:\n" +
            "B - Toggle Boundary Walls\n" +
            "C - Toggle Checkpoints\n" +
            "R - Toggle Raycasts\n" +
            "T - Toggle Track Centerline\n" +
            "H - Toggle Help\n" +
            "Tab - Toggle UI Panel",
            new Vector2(20, 20), new Vector2(260, 160));
        helpText.fontSize = 16;

        // Hide help panel initially
        helpPanel.SetActive(false);

        Debug.Log("[WatchModeManager] UI created.");
    }

    private GameObject CreatePanel(string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(uiCanvas.transform, false);

        RectTransform rectTransform = panel.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.pivot = new Vector2(0, 0);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;

        Image image = panel.AddComponent<Image>();
        image.color = color;

        return panel;
    }

    private TextMeshProUGUI CreateText(GameObject parent, string name, string text, Vector2 position, Vector2 size)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent.transform, false);

        RectTransform rectTransform = textObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.pivot = new Vector2(0, 0);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;

        TextMeshProUGUI tmpText = textObj.AddComponent<TextMeshProUGUI>();
        tmpText.text = text;
        tmpText.color = textColor;
        tmpText.fontSize = 16;
        tmpText.alignment = TextAlignmentOptions.Left;

        return tmpText;
    }

    private Toggle CreateToggle(GameObject parent, string name, string label, Vector2 position, Vector2 size)
    {
        GameObject toggleObj = new GameObject(name);
        toggleObj.transform.SetParent(parent.transform, false);

        RectTransform rectTransform = toggleObj.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.pivot = new Vector2(0, 0);
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = size;

        Toggle toggle = toggleObj.AddComponent<Toggle>();

        // Create background
        GameObject background = new GameObject("Background");
        background.transform.SetParent(toggleObj.transform, false);

        RectTransform bgRectTransform = background.AddComponent<RectTransform>();
        bgRectTransform.anchorMin = new Vector2(0, 0.5f);
        bgRectTransform.anchorMax = new Vector2(0, 0.5f);
        bgRectTransform.pivot = new Vector2(0.5f, 0.5f);
        bgRectTransform.anchoredPosition = new Vector2(10, 0);
        bgRectTransform.sizeDelta = new Vector2(20, 20);

        Image bgImage = background.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1);

        // Create checkmark
        GameObject checkmark = new GameObject("Checkmark");
        checkmark.transform.SetParent(background.transform, false);

        RectTransform checkRectTransform = checkmark.AddComponent<RectTransform>();
        checkRectTransform.anchorMin = new Vector2(0, 0);
        checkRectTransform.anchorMax = new Vector2(1, 1);
        checkRectTransform.pivot = new Vector2(0.5f, 0.5f);
        checkRectTransform.anchoredPosition = Vector2.zero;
        checkRectTransform.sizeDelta = Vector2.zero;

        Image checkImage = checkmark.AddComponent<Image>();
        checkImage.color = toggleColor;

        // Create label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(toggleObj.transform, false);

        RectTransform labelRectTransform = labelObj.AddComponent<RectTransform>();
        labelRectTransform.anchorMin = new Vector2(0, 0);
        labelRectTransform.anchorMax = new Vector2(1, 1);
        labelRectTransform.pivot = new Vector2(0.5f, 0.5f);
        labelRectTransform.anchoredPosition = new Vector2(10, 0);
        labelRectTransform.sizeDelta = new Vector2(-20, 0);

        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.color = textColor;
        labelText.fontSize = 16;
        labelText.alignment = TextAlignmentOptions.Left;

        // Set up toggle component
        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;

        return toggle;
    }

    private IEnumerator InitializeVisualizations()
    {
        // Wait for boundary detector to finish scanning
        if (boundaryDetector != null)
        {
            while (!boundaryDetector.HasScanned)
            {
                Debug.Log("[WatchModeManager] Waiting for boundary detector to finish scanning...");
                yield return new WaitForSeconds(0.5f);
            }
        }

        // Wait for checkpoint generator to initialize
        if (checkpointGenerator != null)
        {
            while (checkpointGenerator.Checkpoints.Count == 0)
            {
                Debug.Log("[WatchModeManager] Waiting for checkpoint generator to initialize...");
                yield return new WaitForSeconds(0.5f);
            }
        }

        // Initialize boundary walls visualization
        if (boundaryDetector != null && boundaryDetector.HasScanned)
        {
            InitializeBoundaryWalls();
        }

        // Initialize checkpoints visualization
        if (checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > 0)
        {
            InitializeCheckpoints();
        }

        // Initialize centerline visualization
        if (checkpointGenerator != null && checkpointGenerator.Checkpoints.Count > 0)
        {
            InitializeCenterline();
        }

        // Update visibility based on toggle settings
        UpdateVisualizationVisibility();

        Debug.Log("[WatchModeManager] Visualizations initialized.");
    }

    private void InitializeBoundaryWalls()
    {
        // Set inner boundary line points
        List<Vector3> innerPoints = boundaryDetector.InnerBoundaryPoints;
        if (innerPoints != null && innerPoints.Count > 0)
        {
            // Smooth the inner boundary points for a better visual
            List<Vector3> smoothedInnerPoints = SmoothPoints(innerPoints, 3);

            // Create vertical walls by duplicating points at different heights
            int pointsPerWall = 2; // Top and bottom points
            innerBoundaryLine.positionCount = smoothedInnerPoints.Count * pointsPerWall;

            for (int i = 0; i < smoothedInnerPoints.Count; i++)
            {
                // Bottom point (at ground level)
                innerBoundaryLine.SetPosition(i * pointsPerWall, smoothedInnerPoints[i] + Vector3.up * 0.1f);

                // Top point (at specified height)
                innerBoundaryLine.SetPosition(i * pointsPerWall + 1, smoothedInnerPoints[i] + Vector3.up * boundaryLineHeight);
            }
        }

        // Set outer boundary line points
        List<Vector3> outerPoints = boundaryDetector.OuterBoundaryPoints;
        if (outerPoints != null && outerPoints.Count > 0)
        {
            // Smooth the outer boundary points for a better visual
            List<Vector3> smoothedOuterPoints = SmoothPoints(outerPoints, 3);

            // Create vertical walls by duplicating points at different heights
            int pointsPerWall = 2; // Top and bottom points
            outerBoundaryLine.positionCount = smoothedOuterPoints.Count * pointsPerWall;

            for (int i = 0; i < smoothedOuterPoints.Count; i++)
            {
                // Bottom point (at ground level)
                outerBoundaryLine.SetPosition(i * pointsPerWall, smoothedOuterPoints[i] + Vector3.up * 0.1f);

                // Top point (at specified height)
                outerBoundaryLine.SetPosition(i * pointsPerWall + 1, smoothedOuterPoints[i] + Vector3.up * boundaryLineHeight);
            }
        }

        Debug.Log("[WatchModeManager] Boundary walls visualization initialized.");
    }

    // Helper method to smooth a list of points using a moving average
    private List<Vector3> SmoothPoints(List<Vector3> points, int windowSize)
    {
        if (points == null || points.Count <= windowSize || windowSize <= 1)
            return points;

        List<Vector3> smoothedPoints = new List<Vector3>(points.Count);

        // Add first points unchanged
        for (int i = 0; i < windowSize / 2; i++)
            smoothedPoints.Add(points[i]);

        // Smooth middle points
        for (int i = windowSize / 2; i < points.Count - windowSize / 2; i++)
        {
            Vector3 sum = Vector3.zero;
            for (int j = -windowSize / 2; j <= windowSize / 2; j++)
                sum += points[i + j];

            smoothedPoints.Add(sum / windowSize);
        }

        // Add last points unchanged
        for (int i = points.Count - windowSize / 2; i < points.Count; i++)
            smoothedPoints.Add(points[i]);

        return smoothedPoints;
    }

    private void InitializeCheckpoints()
    {
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

            // Create checkpoint parent object
            GameObject checkpointObj = new GameObject($"Checkpoint_{i}");
            checkpointObj.transform.SetParent(transform);

            // Create horizontal line renderer (main gate)
            LineRenderer horizontalLine = checkpointObj.AddComponent<LineRenderer>();

            // Set up line renderer
            Color checkpointColorToUse = i == currentCheckpointIndex ? nextCheckpointColor : checkpointColor;
            SetupLineRenderer(horizontalLine, checkpointColorToUse, checkpointWidth, false);

            // Calculate checkpoint line positions
            Vector3 center = checkpoint.position;
            Vector3 forward = checkpoint.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            float width = checkpoint.width;
            if (width <= 0) width = 10f; // Default width if not set

            Vector3 start = center - right * (width / 2);
            Vector3 end = center + right * (width / 2);

            // Set horizontal line positions
            horizontalLine.positionCount = 2;
            horizontalLine.SetPosition(0, start + Vector3.up * (checkpointHeight / 2));
            horizontalLine.SetPosition(1, end + Vector3.up * (checkpointHeight / 2));

            // Create vertical lines (gate posts)
            GameObject verticalLinesObj = new GameObject("VerticalLines");
            verticalLinesObj.transform.SetParent(checkpointObj.transform);
            LineRenderer verticalLines = verticalLinesObj.AddComponent<LineRenderer>();

            // Set up vertical line renderer
            SetupLineRenderer(verticalLines, checkpointColorToUse, checkpointWidth * 0.8f, false);

            // Set vertical line positions (two posts)
            verticalLines.positionCount = 4;
            verticalLines.SetPosition(0, start);
            verticalLines.SetPosition(1, start + Vector3.up * checkpointHeight);
            verticalLines.SetPosition(2, end);
            verticalLines.SetPosition(3, end + Vector3.up * checkpointHeight);

            // Create direction indicator
            GameObject directionObj = new GameObject("Direction");
            directionObj.transform.SetParent(checkpointObj.transform);
            LineRenderer directionLine = directionObj.AddComponent<LineRenderer>();

            // Set up direction line renderer
            SetupLineRenderer(directionLine, checkpointColorToUse, checkpointWidth * 0.5f, false);

            // Set direction line positions (arrow pointing in checkpoint forward direction)
            float arrowLength = width * 0.3f;
            float arrowWidth = width * 0.15f;
            Vector3 arrowStart = center + Vector3.up * (checkpointHeight / 2);
            Vector3 arrowEnd = arrowStart + forward * arrowLength;
            Vector3 arrowLeft = arrowEnd - forward * (arrowLength * 0.3f) - right * arrowWidth;
            Vector3 arrowRight = arrowEnd - forward * (arrowLength * 0.3f) + right * arrowWidth;

            directionLine.positionCount = 5;
            directionLine.SetPosition(0, arrowStart);
            directionLine.SetPosition(1, arrowEnd);
            directionLine.SetPosition(2, arrowLeft);
            directionLine.SetPosition(3, arrowEnd);
            directionLine.SetPosition(4, arrowRight);

            // Add to list (we'll use the horizontal line for color updates)
            checkpointLines.Add(horizontalLine);
            checkpointLines.Add(verticalLines);
            checkpointLines.Add(directionLine);
        }

        Debug.Log("[WatchModeManager] Checkpoints visualization initialized.");
    }

    private void InitializeCenterline()
    {
        // Set centerline points from checkpoints
        var checkpoints = checkpointGenerator.Checkpoints;
        if (checkpoints != null && checkpoints.Count > 0)
        {
            // Create a denser centerline by interpolating between checkpoints
            List<Vector3> centerlinePoints = new List<Vector3>();

            // Add first checkpoint
            centerlinePoints.Add(checkpoints[0].position + Vector3.up * boundaryLineHeight);

            // Interpolate between checkpoints
            for (int i = 0; i < checkpoints.Count - 1; i++)
            {
                Vector3 start = checkpoints[i].position;
                Vector3 end = checkpoints[i + 1].position;
                float distance = Vector3.Distance(start, end);

                // Add more points for longer segments
                int pointCount = Mathf.Max(2, Mathf.FloorToInt(distance / 5f));

                for (int j = 1; j < pointCount; j++)
                {
                    float t = j / (float)pointCount;
                    Vector3 point = Vector3.Lerp(start, end, t) + Vector3.up * boundaryLineHeight;
                    centerlinePoints.Add(point);
                }

                // Add the end point
                centerlinePoints.Add(end + Vector3.up * boundaryLineHeight);
            }

            // Set the centerline points
            centerlineLine.positionCount = centerlinePoints.Count;
            for (int i = 0; i < centerlinePoints.Count; i++)
            {
                centerlineLine.SetPosition(i, centerlinePoints[i]);
            }
        }

        Debug.Log("[WatchModeManager] Centerline visualization initialized.");
    }

    private void Update()
    {
        if (!isWatchMode) return;

        // Update current checkpoint index from racing agent
        if (racingAgent != null)
        {
            int newCheckpointIndex = racingAgent.CurrentCheckpoint;
            if (newCheckpointIndex != currentCheckpointIndex)
            {
                currentCheckpointIndex = newCheckpointIndex;
                UpdateCheckpointColors();
            }

            // Monitor for car resets
            MonitorCarReset();
        }

        // Toggle visualizations with keyboard shortcuts
        if (Input.GetKeyDown(KeyCode.B))
            ToggleBoundaryWalls();

        if (Input.GetKeyDown(KeyCode.C))
            ToggleCheckpoints();

        if (Input.GetKeyDown(KeyCode.R))
            ToggleRaycasts();

        if (Input.GetKeyDown(KeyCode.T))
            ToggleTrackCenterline();

        // Toggle UI panel with Tab key
        if (Input.GetKeyDown(KeyCode.Tab))
            ToggleUIPanel();

        // Toggle help panel with H key
        if (Input.GetKeyDown(KeyCode.H))
            ToggleHelpPanel();
    }

    // Variables to track car position for reset detection
    private Vector3 lastCarPosition = Vector3.zero;
    private float lastPositionChangeTime = 0f;
    private bool isMonitoringReset = false;

    private void MonitorCarReset()
    {
        if (racingAgent == null) return;

        // Get current car position
        Vector3 currentPosition = racingAgent.transform.position;

        // If this is the first time, just store the position
        if (lastCarPosition == Vector3.zero)
        {
            lastCarPosition = currentPosition;
            lastPositionChangeTime = Time.time;
            return;
        }

        // Check if the car has moved significantly (possible reset)
        float distanceMoved = Vector3.Distance(currentPosition, lastCarPosition);

        // If the car has moved a significant distance suddenly (more than 5 units)
        if (distanceMoved > 5f && !isMonitoringReset)
        {
            Debug.Log($"[WatchModeManager] Detected possible car reset. Distance moved: {distanceMoved:F2}");
            isMonitoringReset = true;
            StartCoroutine(HandleCarReset());
        }

        // Update last position if the car is moving normally
        if (distanceMoved > 0.1f)
        {
            lastCarPosition = currentPosition;
            lastPositionChangeTime = Time.time;
        }
        // If car hasn't moved for 3 seconds, it might be stuck
        else if (Time.time - lastPositionChangeTime > 3f && !isMonitoringReset)
        {
            Debug.Log("[WatchModeManager] Car appears to be stuck. Checking if reset is needed.");
            isMonitoringReset = true;
            StartCoroutine(HandleCarReset());
        }
    }

    private IEnumerator HandleCarReset()
    {
        // Wait a moment for the car to settle after reset
        yield return new WaitForSeconds(0.5f);

        // Force a complete agent reset to ensure proper initialization
        Debug.Log("[WatchModeManager] Forcing complete agent reset after car reset detected...");
        racingAgent.ForceAgentReset();

        // Wait for the reset to complete
        yield return new WaitForSeconds(0.5f);

        // Force the racing agent to find the closest checkpoint
        ForceRacingAgentCheckpointUpdate();

        // Wait a bit more and update our tracking
        yield return new WaitForSeconds(0.5f);

        if (racingAgent != null)
        {
            // Update our local tracking
            currentCheckpointIndex = racingAgent.CurrentCheckpoint;
            UpdateCheckpointColors();

            // Update last position
            lastCarPosition = racingAgent.transform.position;
            lastPositionChangeTime = Time.time;

            Debug.Log($"[WatchModeManager] After reset: Current checkpoint is {currentCheckpointIndex}");

            // Force another decision to make sure the agent starts moving
            racingAgent.RequestDecision();
            Debug.Log("[WatchModeManager] Requested new decision from racing agent after reset.");
        }

        isMonitoringReset = false;
    }

    private void UpdateCheckpointColors()
    {
        // Update checkpoint colors based on current checkpoint
        var checkpoints = checkpointGenerator.Checkpoints;
        if (checkpoints == null || checkpoints.Count == 0) return;

        // We have 3 line renderers per checkpoint (horizontal, vertical, direction)
        for (int i = 0; i < checkpointLines.Count; i += 3)
        {
            int checkpointIndex = i / 3;

            // Determine color based on whether this is the current checkpoint
            Color color = checkpointIndex == currentCheckpointIndex ? nextCheckpointColor : checkpointColor;

            // Update all line renderers for this checkpoint
            for (int j = 0; j < 3; j++)
            {
                int lineIndex = i + j;
                if (lineIndex < checkpointLines.Count && checkpointLines[lineIndex] != null)
                {
                    // Update line renderer colors
                    checkpointLines[lineIndex].startColor = color;
                    checkpointLines[lineIndex].endColor = color;

                    // Update material colors for the glow effect
                    Material mat = checkpointLines[lineIndex].material;
                    if (mat != null)
                    {
                        if (mat.HasProperty("_Color"))
                            mat.SetColor("_Color", color);

                        if (mat.HasProperty("_EmissionColor"))
                            mat.SetColor("_EmissionColor", color);
                    }
                }
            }
        }
    }

    private IEnumerator FixCheckpointInitialization()
    {
        // Wait until the racing agent and checkpoint generator are ready
        if (racingAgent == null || checkpointGenerator == null)
        {
            Debug.LogWarning("[WatchModeManager] Cannot fix checkpoint initialization: Racing agent or checkpoint generator is null.");
            yield break;
        }

        // Wait a moment to ensure everything is initialized
        yield return new WaitForSeconds(1.0f);

        // First, force a complete agent reset to ensure proper initialization
        Debug.Log("[WatchModeManager] Forcing complete agent reset...");
        racingAgent.ForceAgentReset();

        // Wait for the reset to complete
        yield return new WaitForSeconds(1.0f);

        // Now try to force the RacingAgent to find the closest checkpoint itself
        bool success = ForceRacingAgentCheckpointUpdate();

        if (!success)
        {
            // If that didn't work, try our own implementation
            Debug.Log("[WatchModeManager] Falling back to custom checkpoint initialization.");

            // Get the car's current position
            Vector3 carPosition = racingAgent.transform.position;

            // Find the closest checkpoint to the car's position
            int closestCheckpointIndex = FindClosestCheckpointToPosition(carPosition);

            if (closestCheckpointIndex >= 0)
            {
                // Set the current checkpoint in the racing agent
                Debug.Log($"[WatchModeManager] Setting initial checkpoint to {closestCheckpointIndex} based on car spawn position.");

                // Force the racing agent to use this checkpoint
                SetInitialCheckpoint(closestCheckpointIndex);

                // Update our local tracking of the current checkpoint
                currentCheckpointIndex = closestCheckpointIndex;

                // Update the checkpoint colors
                UpdateCheckpointColors();
            }
            else
            {
                Debug.LogWarning("[WatchModeManager] Could not find a suitable checkpoint for the car's spawn position.");
            }
        }

        // Wait a bit more and update our local tracking of the current checkpoint
        yield return new WaitForSeconds(0.5f);

        // Force another decision to make sure the agent starts moving
        racingAgent.RequestDecision();
        Debug.Log("[WatchModeManager] Requested new decision from racing agent.");

        // Update our tracking
        if (racingAgent != null)
        {
            currentCheckpointIndex = racingAgent.CurrentCheckpoint;
            UpdateCheckpointColors();
            Debug.Log($"[WatchModeManager] Final checkpoint index: {currentCheckpointIndex}");
        }

        // Wait a bit more and force one more decision to ensure the agent is moving
        yield return new WaitForSeconds(1.0f);
        racingAgent.RequestDecision();
        Debug.Log("[WatchModeManager] Requested final decision from racing agent.");
    }

    private bool ForceRacingAgentCheckpointUpdate()
    {
        if (racingAgent == null)
            return false;

        try
        {
            // Try to call the FindClosestCheckpoint method directly
            System.Type type = racingAgent.GetType();
            System.Reflection.MethodInfo method = type.GetMethod("FindClosestCheckpoint",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                method.Invoke(racingAgent, null);
                Debug.Log("[WatchModeManager] Successfully called FindClosestCheckpoint method.");
                return true;
            }

            // If that didn't work, try to call CheckDependencies which might call FindClosestCheckpoint
            method = type.GetMethod("CheckDependencies",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                method.Invoke(racingAgent, null);
                Debug.Log("[WatchModeManager] Successfully called CheckDependencies method.");
                return true;
            }

            // If we couldn't find either method, return false
            Debug.LogWarning("[WatchModeManager] Could not find FindClosestCheckpoint or CheckDependencies methods.");
            return false;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WatchModeManager] Error forcing checkpoint update: {e.Message}");
            return false;
        }
    }

    private int FindClosestCheckpointToPosition(Vector3 position)
    {
        if (checkpointGenerator == null || checkpointGenerator.Checkpoints == null || checkpointGenerator.Checkpoints.Count == 0)
            return -1;

        var checkpoints = checkpointGenerator.Checkpoints;
        int closestIndex = -1;
        float closestDistance = float.MaxValue;

        // Find the closest checkpoint by distance
        for (int i = 0; i < checkpoints.Count; i++)
        {
            float distance = Vector3.Distance(position, checkpoints[i].position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        // Check if we're facing the right direction for this checkpoint
        if (closestIndex >= 0 && racingAgent != null)
        {
            Vector3 carForward = racingAgent.transform.forward;
            Vector3 checkpointForward = checkpoints[closestIndex].forward;

            // Calculate dot product to check alignment
            float dot = Vector3.Dot(carForward, checkpointForward);

            // If we're facing away from the checkpoint direction, find a better one
            if (dot < 0.5f) // Not aligned well enough
            {
                // Try to find a checkpoint that's better aligned with our direction
                int betterAlignedIndex = -1;
                float bestAlignment = 0.5f; // Minimum threshold for good alignment

                for (int i = 0; i < checkpoints.Count; i++)
                {
                    float alignment = Vector3.Dot(carForward, checkpoints[i].forward);
                    float dist = Vector3.Distance(position, checkpoints[i].position);

                    // Consider both alignment and distance (must be reasonably close)
                    if (alignment > bestAlignment && dist < closestDistance * 2f)
                    {
                        bestAlignment = alignment;
                        betterAlignedIndex = i;
                    }
                }

                // If we found a better aligned checkpoint, use that instead
                if (betterAlignedIndex >= 0)
                {
                    Debug.Log($"[WatchModeManager] Choosing better aligned checkpoint {betterAlignedIndex} instead of closest {closestIndex}");
                    closestIndex = betterAlignedIndex;
                }
            }
        }

        return closestIndex;
    }

    private void SetInitialCheckpoint(int checkpointIndex)
    {
        if (racingAgent == null || checkpointGenerator == null)
            return;

        // Try to set the checkpoint directly if the property is accessible
        try
        {
            // Use reflection to access the private field if needed
            System.Type type = racingAgent.GetType();
            System.Reflection.FieldInfo field = type.GetField("currentCheckpointIndex",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(racingAgent, checkpointIndex);
                Debug.Log($"[WatchModeManager] Successfully set currentCheckpointIndex to {checkpointIndex} via reflection.");
            }
            else
            {
                // Try to call FindClosestCheckpoint method to force checkpoint update
                System.Reflection.MethodInfo method = type.GetMethod("FindClosestCheckpoint",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (method != null)
                {
                    method.Invoke(racingAgent, null);
                    Debug.Log("[WatchModeManager] Called FindClosestCheckpoint to update checkpoint.");
                }
                else
                {
                    Debug.LogWarning("[WatchModeManager] Could not find method to update checkpoint.");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WatchModeManager] Error setting initial checkpoint: {e.Message}");
        }
    }

    private void UpdateVisualizationVisibility()
    {
        // Update boundary walls visibility
        if (innerBoundaryLine != null)
            innerBoundaryLine.enabled = showBoundaryWalls;

        if (outerBoundaryLine != null)
            outerBoundaryLine.enabled = showBoundaryWalls;

        // Update checkpoints visibility
        foreach (var line in checkpointLines)
        {
            if (line != null)
                line.enabled = showCheckpoints;
        }

        // Update centerline visibility
        if (centerlineLine != null)
            centerlineLine.enabled = showTrackCenterline;
    }

    // Public methods for UI buttons
    public void ToggleBoundaryWalls()
    {
        showBoundaryWalls = !showBoundaryWalls;
        if (boundaryToggle != null)
            boundaryToggle.isOn = showBoundaryWalls;
        UpdateVisualizationVisibility();
    }

    public void ToggleCheckpoints()
    {
        showCheckpoints = !showCheckpoints;
        if (checkpointToggle != null)
            checkpointToggle.isOn = showCheckpoints;
        UpdateVisualizationVisibility();

        if (racingAgent != null)
            racingAgent.showUpcomingCheckpoints = showCheckpoints;
    }

    public void ToggleRaycasts()
    {
        showRaycasts = !showRaycasts;
        if (raycastToggle != null)
            raycastToggle.isOn = showRaycasts;

        if (racingAgent != null)
        {
            racingAgent.showRaycasts = showRaycasts;

            // Update the RaycastVisualizer if it exists
            RaycastVisualizer visualizer = racingAgent.gameObject.GetComponent<RaycastVisualizer>();
            if (visualizer != null)
            {
                visualizer.SetShowRaycasts(showRaycasts);
            }
        }
    }

    public void ToggleTrackCenterline()
    {
        showTrackCenterline = !showTrackCenterline;
        if (centerlineToggle != null)
            centerlineToggle.isOn = showTrackCenterline;
        UpdateVisualizationVisibility();

        if (racingAgent != null)
            racingAgent.showCenterlineGizmo = showTrackCenterline;
    }

    public void ToggleUIPanel()
    {
        isPanelVisible = !isPanelVisible;
        if (watchModePanel != null)
            watchModePanel.SetActive(isPanelVisible);
    }

    public void ToggleHelpPanel()
    {
        isHelpVisible = !isHelpVisible;
        if (helpPanel != null)
            helpPanel.SetActive(isHelpVisible);
    }
}
