// --- START OF FILE TrainingStatsUI.cs ---

using UnityEngine;
using System.Text;

/// <summary>
/// Displays enhanced ML-Agents training statistics using IMGUI.
/// Automatically finds and monitors a RacingAgent in the scene.
/// V1.2 - Simplified layout, explicit positioning, more checks for robustness.
/// </summary>
public class TrainingStatsUI : MonoBehaviour
{
    [Header("Configuration")]
    public bool showUI = true;
    public SpeedUnit speedUnit = SpeedUnit.KPH;
    public float boxWidth = 300f;
    public float boxHeight = 280f; // Keep increased height
    public float agentSearchInterval = 2.0f;
    public bool enableDebugLogs = false;

    public enum SpeedUnit { MPS, KPH, MPH }

    private RacingAgent targetAgent;
    private Rigidbody agentRigidbody;
    private VehicleController agentVehicleController;
    private CheckpointGenerator checkpointGenerator;
    // Removed StringBuilder as we'll construct text directly or use simple concatenation
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;
    private GUIStyle actionBarStyle;
    private bool agentFound = false;
    private float timeSinceLastSearch = 0f;
    private bool stylesInitialized = false;

    // Constants for layout
    private const float PADDING = 5f;
    private const float LINE_HEIGHT = 20f; // Estimated height per line including spacing

    void Start()
    {
        FindAndSetupAgent();
        if (!agentFound && enableDebugLogs) Debug.Log("[TrainingStatsUI] Initial search failed. Will retry...");
    }

    void InitializeStyles()
    {
        if (stylesInitialized || GUI.skin == null) return;
        try
        {
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white }, fontSize = 14, wordWrap = true }; // Use MiddleLeft alignment
            headerStyle = new GUIStyle(labelStyle) { fontStyle = FontStyle.Bold, fontSize = 15, alignment = TextAnchor.UpperLeft }; // Header can be UpperLeft
            actionBarStyle = new GUIStyle(GUI.skin.box);
            stylesInitialized = true;
            if (enableDebugLogs) Debug.Log("[TrainingStatsUI] GUI Styles Initialized.");
        } catch (System.Exception ex) { Debug.LogError($"[TrainingStatsUI] Error initializing GUI styles: {ex.Message}"); }
    }

    void FindAndSetupAgent()
    {
        targetAgent = FindObjectOfType<RacingAgent>();
        if (targetAgent != null) {
            agentFound = true;
            if (enableDebugLogs) Debug.Log($"[TrainingStatsUI] Found RacingAgent: {targetAgent.gameObject.name}");
            agentRigidbody = targetAgent.GetComponent<Rigidbody>();
            agentVehicleController = targetAgent.vehicleController;
            if (agentRigidbody == null) Debug.LogWarning("[TrainingStatsUI] Target RacingAgent missing Rigidbody.", targetAgent);
            if (agentVehicleController == null) Debug.LogWarning("[TrainingStatsUI] Target RacingAgent missing VehicleController.", targetAgent);
            if (checkpointGenerator == null) { checkpointGenerator = FindObjectOfType<CheckpointGenerator>(); if (checkpointGenerator == null) Debug.LogWarning("[TrainingStatsUI] No CheckpointGenerator found."); }
        } else { agentFound = false; timeSinceLastSearch = 0f; }
    }

    void Update()
    {
        if (!agentFound && showUI) {
            timeSinceLastSearch += Time.deltaTime;
            if (timeSinceLastSearch >= agentSearchInterval) { if (enableDebugLogs) Debug.Log("[TrainingStatsUI] Retrying agent search..."); FindAndSetupAgent(); }
        }
    }

    void OnGUI()
    {
        InitializeStyles(); // Ensure styles are ready

        if (!showUI || !stylesInitialized) return;

        Rect boxRect = new Rect(10, 10, boxWidth, boxHeight);

        // --- Waiting Message ---
        if (!agentFound || targetAgent == null) {
            Rect waitBoxRect = new Rect(10, 10, boxWidth, 40);
            DrawDarkBox(waitBoxRect);
            GUIStyle safeStyle = labelStyle ?? GUI.skin.label; // Fallback style
            GUI.Label(new Rect(waitBoxRect.x + PADDING, waitBoxRect.y + PADDING, waitBoxRect.width - 2 * PADDING, waitBoxRect.height - 2 * PADDING),
                      "Waiting for Racing Agent...", safeStyle);
            return;
        }

        // --- Draw Background Box ---
        DrawDarkBox(boxRect);

        // --- Draw Stats using Explicit Rects ---
        float currentY = boxRect.y + PADDING;
        float currentX = boxRect.x + PADDING;
        float contentWidth = boxRect.width - 2 * PADDING;

        try { // Wrap all drawing in a try-catch

            // Header
            GUI.Label(new Rect(currentX, currentY, contentWidth, LINE_HEIGHT), $"--- Agent: {targetAgent.gameObject.name} ---", headerStyle);
            currentY += LINE_HEIGHT * 1.2f; // Add extra space after header

            // Core Stats
            GUI.Label(new Rect(currentX, currentY, contentWidth, LINE_HEIGHT), $"Agent Steps: {targetAgent.StepCount}", labelStyle); currentY += LINE_HEIGHT;
            GUI.Label(new Rect(currentX, currentY, contentWidth, LINE_HEIGHT), $"Cumulative Reward: {targetAgent.GetCumulativeReward():F3}", labelStyle); currentY += LINE_HEIGHT;
            GUI.Label(new Rect(currentX, currentY, contentWidth, LINE_HEIGHT), $"Last Reset Reason: {targetAgent.LastResetReason}", labelStyle); currentY += LINE_HEIGHT;

            // Lap Info Header
            GUI.Label(new Rect(currentX, currentY, contentWidth, LINE_HEIGHT), "--- Lap Info ---", headerStyle); currentY += LINE_HEIGHT * 1.2f;

            // Lap Info Details - Add null check for generator
            int currentCP = targetAgent.CurrentCheckpoint; // Updated to use the new property name
            int totalCP = (checkpointGenerator != null && checkpointGenerator.IsInitialized) ? checkpointGenerator.Checkpoints.Count : 0;
            GUI.Label(new Rect(currentX, currentY, contentWidth, LINE_HEIGHT), $"Target Checkpoint: {currentCP} / {(totalCP > 0 ? totalCP.ToString() : "??")}", labelStyle); currentY += LINE_HEIGHT;
            GUI.Label(new Rect(currentX, currentY, contentWidth, LINE_HEIGHT), $"Current Lap Time: {FormatTime(targetAgent.CurrentLapTime)}", labelStyle); currentY += LINE_HEIGHT;
            string bestLapText = targetAgent.BestLapTimeSoFar < float.MaxValue ? FormatTime(targetAgent.BestLapTimeSoFar) : "(No lap completed yet)";
            GUI.Label(new Rect(currentX, currentY, contentWidth, LINE_HEIGHT), $"Best Lap Time: {bestLapText}", labelStyle); currentY += LINE_HEIGHT;

            // Vehicle State Header
            GUI.Label(new Rect(currentX, currentY, contentWidth, LINE_HEIGHT), "--- Vehicle State ---", headerStyle); currentY += LINE_HEIGHT * 1.2f;

            // Vehicle State Details - Add null check for rigidbody
            if (agentRigidbody != null) {
                float speed = agentRigidbody.linearVelocity.magnitude; string speedStr = "Speed: ";
                switch (speedUnit) { case SpeedUnit.KPH: speedStr += $"{(speed * 3.6f):F1} KPH"; break; case SpeedUnit.MPH: speedStr += $"{(speed * 2.237f):F1} MPH"; break; default: speedStr += $"{speed:F1} m/s"; break; }
                GUI.Label(new Rect(currentX, currentY, contentWidth, LINE_HEIGHT), speedStr, labelStyle); currentY += LINE_HEIGHT;
            } else { GUI.Label(new Rect(currentX, currentY, contentWidth, LINE_HEIGHT), "Speed: N/A (No Rigidbody!)", labelStyle); currentY += LINE_HEIGHT; }
            string stateText = $"State: {(targetAgent.IsOffTrack ? "OFF TRACK" : "On Track")} | {(targetAgent.IsGrounded ? "Grounded" : "AIRBORNE")}";
            GUI.Label(new Rect(currentX, currentY, contentWidth, LINE_HEIGHT), stateText, labelStyle); currentY += LINE_HEIGHT;

            // Actions Header
            GUI.Label(new Rect(currentX, currentY, contentWidth, LINE_HEIGHT), "--- Current Actions ---", headerStyle); currentY += LINE_HEIGHT * 1.2f;

            // Actions Sliders
            currentY = DrawActionSliderExplicit(currentX, currentY, contentWidth, "Steer:", targetAgent.LastSteer, -1f, 1f);
            currentY = DrawActionSliderExplicit(currentX, currentY, contentWidth, "Accel:", targetAgent.LastAccel, 0f, 1f);
            currentY = DrawActionSliderExplicit(currentX, currentY, contentWidth, "Brake:", targetAgent.LastBrake, 0f, 1f);


        } catch (System.Exception ex) {
             // Draw error message if something goes wrong
             Rect errorRect = new Rect(currentX, currentY, contentWidth, boxRect.height - (currentY - boxRect.y) - PADDING); // Remaining space
             GUI.Label(errorRect, $"<color=red>GUI Error:\n{ex.Message}</color>", labelStyle); // Show only message for brevity
             if(enableDebugLogs) Debug.LogError($"[TrainingStatsUI] Error during OnGUI drawing: {ex}"); // Log full error if enabled
        }
    }

    // Helper to draw a dark semi-transparent box
    void DrawDarkBox(Rect rect) {
         if (GUI.skin == null) return;
         Color originalBg = GUI.backgroundColor;
         GUI.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.75f);
         GUI.Box(rect, GUIContent.none, GUI.skin.box);
         GUI.backgroundColor = originalBg;
    }

    // Explicit Rect version of slider drawing
    float DrawActionSliderExplicit(float x, float y, float width, string label, float value, float min, float max)
    {
        if (labelStyle == null || actionBarStyle == null) return y; // Styles not ready

        float labelWidth = width * 0.25f;
        float barWidth = width - labelWidth - PADDING; // Assign remaining width to bar
        float barHeight = 18f; // Fixed height

        // Draw Label
        GUI.Label(new Rect(x, y, labelWidth, barHeight), label, labelStyle);

        // Bar Position
        Rect barRect = new Rect(x + labelWidth + PADDING, y, barWidth, barHeight);

        // Draw Bar Background
        GUI.Box(barRect, GUIContent.none, actionBarStyle);

        // Calculate Filled Portion
        float range = max - min; if (range <= 0) range = 1f; // Prevent division by zero
        float normalizedValue = Mathf.Clamp01((value - min) / range);
        Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * normalizedValue, barRect.height);

        // Adjust for centered bars like Steering
        Color fillColor = Color.Lerp(Color.gray, Color.green, normalizedValue);
        if (label.Contains("Steer")) {
             float zeroPointNorm = (-min / range); // 0.5
             fillRect.width = barRect.width * Mathf.Abs(normalizedValue - zeroPointNorm);
             fillRect.x = barRect.x + barRect.width * Mathf.Min(normalizedValue, zeroPointNorm);
             fillColor = (normalizedValue < zeroPointNorm) ? Color.Lerp(Color.yellow, Color.white, normalizedValue / zeroPointNorm) : Color.Lerp(Color.white, Color.cyan, (normalizedValue - zeroPointNorm) / (1f - zeroPointNorm));
        }
        else if (label.Contains("Brake")) { fillColor = Color.Lerp(Color.gray, Color.red, normalizedValue); }

        // Draw Filled Portion
        Color originalColor = GUI.color;
        GUI.color = fillColor;
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = originalColor;

        return y + barHeight + (PADDING * 0.5f); // Return Y position for the next element
    }


    private string FormatTime(float timeInSeconds) {
        if (timeInSeconds >= float.MaxValue || timeInSeconds < 0) return "--:--.---";
        int minutes = (int)(timeInSeconds / 60); float seconds = timeInSeconds % 60;
        return $"{minutes:00}:{seconds:00.000}";
    }
}
// --- END OF FILE TrainingStatsUI.cs ---