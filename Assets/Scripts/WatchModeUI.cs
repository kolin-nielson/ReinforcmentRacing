using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Manages the UI panel specifically for Watch Mode, displaying AI agent information.
/// Creates its own UI elements programmatically.
/// </summary>
public class WatchModeUI : MonoBehaviour
{
    // UI Elements (references will be assigned programmatically)
    private GameObject watchModePanel;
    private TextMeshProUGUI stateText;
    private TextMeshProUGUI speedText;
    private TextMeshProUGUI checkpointText;
    private TextMeshProUGUI lapTimeText;
    private TextMeshProUGUI bestLapTimeText;
    private TextMeshProUGUI actionsText;

    // Visualization Toggles (references will be assigned programmatically)
    private Toggle boundaryToggle;
    private Toggle checkpointToggle;
    private Toggle raycastToggle;
    private Toggle centerlineToggle;
    private Toggle hitPointToggle;

    // References to other components
    private RacingAgent racingAgent;
    private CarSpawnerManager carSpawnerManager;
    private WatchModeVisualizer watchModeVisualizer;

    private bool isWatchMode = false;
    private TMP_FontAsset defaultFont;

    // --- Styling constants --- Adjust these for desired look
    private Color panelBackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.75f); // Slightly more transparent
    private Color textColor = Color.white;
    private int defaultFontSize = 14; // Smaller font
    private float panelWidth = 280f; // Narrower panel
    private float panelPadding = 8f;   // Reduced padding
    private float elementSpacing = 4f; // Reduced spacing
    private float textHeightMultiplier = 1.3f; // Multiplier for text/toggle height based on font size

    void Awake()
    {
        isWatchMode = PlayerPrefs.GetInt("GameMode", 0) == 1;
        if (!isWatchMode)
        {
            // If not in watch mode, this component isn't needed
            Destroy(gameObject);
            return;
        }

        // Find visualizer FIRST, before creating UI that might need it
        watchModeVisualizer = FindObjectOfType<WatchModeVisualizer>();
        if (watchModeVisualizer == null)
        {
            Debug.LogWarning("[WatchModeUI] WatchModeVisualizer not found during Awake! Toggles may not function correctly initially.");
            // Proceed with UI creation, but toggles might not link correctly until Start
        }

        // Load default resources
        defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (defaultFont == null)
        {
            defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont == null)
            {
                Debug.LogError("[WatchModeUI] Default TMP Font Asset not found! UI cannot be created.");
                Destroy(gameObject);
                return;
            }
            Debug.LogWarning("[WatchModeUI] LiberationSans SDF not found, using TMP default font.");
        }

        CreateUI();
    }

    void Start()
    {
        // References are found after UI creation
        FindSceneReferences(); // Finds CarSpawnerManager and confirms WatchModeVisualizer
        // Setup toggles IF the visualizer was found (either in Awake or Start)
        if (watchModeVisualizer != null)
        {
            SetupToggles(); // Setup toggles after they are created and visualizer is found
        }
    }

    void FindSceneReferences()
    {
        carSpawnerManager = FindObjectOfType<CarSpawnerManager>();
        if (carSpawnerManager == null)
        {
            Debug.LogError("[WatchModeUI] CarSpawnerManager not found! Cannot link to agent.", this);
        }

        // Re-check for visualizer if not found in Awake
        if (watchModeVisualizer == null)
        {
            watchModeVisualizer = FindObjectOfType<WatchModeVisualizer>();
        }

        if (watchModeVisualizer == null)
        {
            Debug.LogWarning("[WatchModeUI] WatchModeVisualizer not found! Toggles will not function.", this);
        }
        // Agent reference is fetched in Update or later
    }

    void CreateUI()
    {
        // 1. Ensure Canvas and EventSystem exist
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("WatchModeCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            Debug.Log("[WatchModeUI] Created WatchModeCanvas.");
        }
        if (FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Debug.Log("[WatchModeUI] Created EventSystem.");
        }

        // 2. Create the main Panel
        watchModePanel = new GameObject("WatchModePanel");
        watchModePanel.transform.SetParent(canvas.transform, false);

        Image panelImage = watchModePanel.AddComponent<Image>();
        panelImage.color = panelBackgroundColor;

        RectTransform panelRect = watchModePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0); // Bottom-left corner
        panelRect.anchorMax = new Vector2(0, 0);
        panelRect.pivot = new Vector2(0, 0);
        panelRect.anchoredPosition = new Vector2(panelPadding, panelPadding);
        panelRect.sizeDelta = new Vector2(panelWidth, 0); // Width is fixed, height determined by content

        // 3. Add Layout Components to Panel
        VerticalLayoutGroup layoutGroup = watchModePanel.AddComponent<VerticalLayoutGroup>();
        layoutGroup.padding = new RectOffset((int)panelPadding, (int)panelPadding, (int)panelPadding, (int)panelPadding);
        layoutGroup.spacing = elementSpacing;
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        ContentSizeFitter sizeFitter = watchModePanel.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 4. Create Text Elements
        stateText = CreateTextElement("StateText", "State: Initializing...");
        speedText = CreateTextElement("SpeedText", "Speed: -- mph");
        checkpointText = CreateTextElement("CheckpointText", "Checkpoint: -/-");
        lapTimeText = CreateTextElement("LapTimeText", "Lap: --:--.---");
        bestLapTimeText = CreateTextElement("BestLapText", "Best: --:--.---");
        actionsText = CreateTextElement("ActionsText", "Actions: Steer - | Accel - | Brake -");

        // 5. Create Toggle Elements
        // Check if visualizer is available before assigning actions
        UnityEngine.Events.UnityAction boundaryAction = (watchModeVisualizer != null) ? watchModeVisualizer.ToggleBoundaryWalls : null;
        UnityEngine.Events.UnityAction checkpointAction = (watchModeVisualizer != null) ? watchModeVisualizer.ToggleCheckpoints : null;
        UnityEngine.Events.UnityAction raycastAction = (watchModeVisualizer != null) ? watchModeVisualizer.ToggleRaycasts : null;
        UnityEngine.Events.UnityAction centerlineAction = (watchModeVisualizer != null) ? watchModeVisualizer.ToggleTrackCenterline : null;
        UnityEngine.Events.UnityAction hitPointAction = (watchModeVisualizer != null) ? watchModeVisualizer.ToggleHitPoints : null;

        boundaryToggle = CreateToggleElement("BoundaryToggle", "Show Boundaries", boundaryAction, watchModeVisualizer?.ShowBoundaryWalls ?? true);
        checkpointToggle = CreateToggleElement("CheckpointToggle", "Show Checkpoints", checkpointAction, watchModeVisualizer?.ShowCheckpoints ?? true);
        raycastToggle = CreateToggleElement("RaycastToggle", "Show Raycasts", raycastAction, watchModeVisualizer?.ShowRaycasts ?? true);
        centerlineToggle = CreateToggleElement("CenterlineToggle", "Show Centerline", centerlineAction, watchModeVisualizer?.ShowTrackCenterline ?? true);
        hitPointToggle = CreateToggleElement("HitPointToggle", "Show Hit Points", hitPointAction, watchModeVisualizer?.ShowHitPoints ?? true);
    }

    TextMeshProUGUI CreateTextElement(string name, string initialText)
    {
        GameObject textGO = new GameObject(name);
        textGO.transform.SetParent(watchModePanel.transform, false);

        TextMeshProUGUI textComponent = textGO.AddComponent<TextMeshProUGUI>();
        textComponent.font = defaultFont;
        textComponent.fontSize = defaultFontSize;
        textComponent.color = textColor;
        textComponent.text = initialText;
        textComponent.alignment = TextAlignmentOptions.Left;

        // Add layout element to control height
        LayoutElement layoutElement = textGO.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = defaultFontSize * textHeightMultiplier;

        return textComponent;
    }

    Toggle CreateToggleElement(string name, string labelText, UnityEngine.Events.UnityAction toggleAction, bool initialState)
    {
        // Load standard UI sprites (Ensure 'UISprite' is in a Resources folder or accessible)
        Sprite backgroundSprite = Resources.Load<Sprite>("UISprite");
        Sprite checkmarkSprite = Resources.Load<Sprite>("UIMask"); // Often used as checkmark

        // Create Toggle GameObject
        GameObject toggleGO = new GameObject(name);
        toggleGO.transform.SetParent(watchModePanel.transform, false);
        toggleGO.AddComponent<RectTransform>(); // Needed for layout

        Toggle toggleComponent = toggleGO.AddComponent<Toggle>();

        // Create Background Image
        GameObject backgroundGO = new GameObject("Background");
        backgroundGO.transform.SetParent(toggleGO.transform, false);
        Image backgroundImage = backgroundGO.AddComponent<Image>();
        backgroundImage.sprite = backgroundSprite;
        backgroundImage.color = new Color(0.8f, 0.8f, 0.8f, 1f); // Light grey background
        RectTransform bgRect = backgroundGO.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0, 0.5f);
        bgRect.anchorMax = new Vector2(0, 0.5f);
        bgRect.pivot = new Vector2(0, 0.5f);
        bgRect.sizeDelta = new Vector2(defaultFontSize, defaultFontSize); // Square size
        bgRect.anchoredPosition = new Vector2(0, 0);

        // Create Checkmark Image
        GameObject checkmarkGO = new GameObject("Checkmark");
        checkmarkGO.transform.SetParent(backgroundGO.transform, false); // Child of background
        Image checkmarkImage = checkmarkGO.AddComponent<Image>();
        checkmarkImage.sprite = checkmarkSprite;
        checkmarkImage.color = new Color(0.2f, 0.2f, 0.2f, 1f); // Dark checkmark
        RectTransform checkRect = checkmarkGO.GetComponent<RectTransform>();
        // Stretch to fit background (with a small margin)
        checkRect.anchorMin = Vector2.zero;
        checkRect.anchorMax = Vector2.one;
        checkRect.offsetMin = new Vector2(3, 3);
        checkRect.offsetMax = new Vector2(-3, -3);

        // Create Label Text
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(toggleGO.transform, false);
        TextMeshProUGUI labelComponent = labelGO.AddComponent<TextMeshProUGUI>();
        labelComponent.font = defaultFont;
        labelComponent.fontSize = defaultFontSize;
        labelComponent.color = textColor;
        labelComponent.text = labelText;
        labelComponent.alignment = TextAlignmentOptions.Left;
        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0, 0.5f);
        labelRect.anchorMax = new Vector2(0, 0.5f);
        labelRect.pivot = new Vector2(0, 0.5f);
        // Position label next to the toggle graphic
        labelRect.anchoredPosition = new Vector2(defaultFontSize + 10, 0);
        labelRect.sizeDelta = new Vector2(panelWidth - (defaultFontSize + panelPadding*2), defaultFontSize * 1.5f);

        // Configure Toggle component
        toggleComponent.targetGraphic = backgroundImage;
        toggleComponent.graphic = checkmarkImage;
        toggleComponent.isOn = initialState;

        // Add listener (only if action is provided)
        if (toggleAction != null)
        {
            toggleComponent.onValueChanged.AddListener((_) => toggleAction());
        }
        else
        {
             Debug.LogWarning($"[WatchModeUI] No action provided for toggle: {name}");
        }

        // Add LayoutElement to control height in VerticalLayoutGroup
        LayoutElement layoutElement = toggleGO.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = defaultFontSize * textHeightMultiplier; // Ensure consistent height

        return toggleComponent;
    }

    void Update()
    {
        if (!isWatchMode) return;

        // Try to get agent if not found yet
        if (racingAgent == null)
        {
            if (carSpawnerManager != null && carSpawnerManager.SpawnedAgents != null && carSpawnerManager.SpawnedAgents.Count > 0)
            {
                racingAgent = carSpawnerManager.SpawnedAgents[0];
                Debug.Log("[WatchModeUI] Found agent in Update.", this);
            }
            else
            {
                // Still no agent, maybe display a waiting message?
                if (stateText != null) stateText.text = "State: Waiting for Agent...";
                return; // Cannot update UI without agent
            }
        }

        // Update UI elements with data from RacingAgent
        UpdateUI();
    }

    void SetupToggles()
    {
        // This is now called after UI creation in Start()
        // and after FindSceneReferences, so visualizer should exist if found
        if (watchModeVisualizer == null)
        {
             Debug.LogWarning("[WatchModeUI] WatchModeVisualizer not found, cannot set up toggles.");
             return;
        }

        // Link toggles to the visualizer's methods
        // Note: The listeners are already added in CreateToggleElement if an action was provided
        // We just need to ensure the initial state is correct if the visualizer state changed between UI creation and now
        if (boundaryToggle != null) boundaryToggle.isOn = watchModeVisualizer.ShowBoundaryWalls;
        if (checkpointToggle != null) checkpointToggle.isOn = watchModeVisualizer.ShowCheckpoints;
        if (raycastToggle != null) raycastToggle.isOn = watchModeVisualizer.ShowRaycasts;
        if (centerlineToggle != null) centerlineToggle.isOn = watchModeVisualizer.ShowTrackCenterline;
        if (hitPointToggle != null) hitPointToggle.isOn = watchModeVisualizer.ShowHitPoints;
    }

    void UpdateUI()
    {
        if (racingAgent == null || !racingAgent.IsDependenciesReady)
        {
             if (stateText != null) stateText.text = "State: Agent Initializing...";
            return; // Don't update if agent isn't fully ready
        }

        // State
        if (stateText != null)
        {
            stateText.text = $"State: {(racingAgent.IsOffTrack ? "Off Track" : "On Track")}";
            stateText.color = racingAgent.IsOffTrack ? Color.yellow : textColor;
        }

        // Speed
        if (speedText != null && racingAgent.vehicleController != null)
        {
            float speedMPS = racingAgent.vehicleController.carVelocity.magnitude;
            float speedMPH = speedMPS * 2.23694f; // Convert m/s to mph
            speedText.text = $"Speed: {speedMPH:F0} mph";
        }

        // Checkpoint Info
        if (checkpointText != null && racingAgent.checkpointGenerator != null && racingAgent.checkpointGenerator.IsInitialized)
        {
             int currentTarget = racingAgent.CurrentCheckpoint;
             int totalCheckpoints = racingAgent.checkpointGenerator.Checkpoints.Count;
             checkpointText.text = $"Checkpoint: {currentTarget}/{totalCheckpoints}";
        }
        else if (checkpointText != null)
        {
            checkpointText.text = "Checkpoint: N/A";
        }

        // Lap Times
        if (lapTimeText != null)
        {
            lapTimeText.text = $"Lap: {FormatTime(racingAgent.CurrentLapTime)}";
        }
        if (bestLapTimeText != null)
        {
            bestLapTimeText.text = $"Best: {FormatTime(racingAgent.BestLapTimeSoFar)}";
        }

        // Actions
        if (actionsText != null)
        {
            float steer = racingAgent.LastSteer;
            float accel = racingAgent.LastAccel;
            float brake = racingAgent.LastBrake;
            // Simple text representation
            actionsText.text = $"Steer: {steer:F2} | Accel: {accel:F2} | Brake: {brake:F2}";

            // --- Optional: Bar visualization (More complex setup) ---
            // This would require creating additional Image elements for bars
            // Example concept:
            // UpdateBar(steerBarImage, steer, Color.blue);
            // UpdateBar(accelBarImage, accel, Color.green);
            // UpdateBar(brakeBarImage, brake, Color.red);
        }
    }

     private string FormatTime(float t)
    {
        if (t >= float.MaxValue || t < 0) return "--:--.---";
        int minutes = (int)(t / 60);
        float seconds = t % 60;
        return $"{minutes:00}:{seconds:00.000}";
    }
}
