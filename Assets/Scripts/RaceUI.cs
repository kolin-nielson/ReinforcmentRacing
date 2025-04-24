using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the race UI, including lap times, position, and race information.
/// </summary>
public class RaceUI : MonoBehaviour
{
    [Header("Race Info Panel")]
    [SerializeField] private GameObject raceInfoPanel;
    [SerializeField] private TextMeshProUGUI lapCounterText;
    [SerializeField] private TextMeshProUGUI currentTimeText;
    [SerializeField] private TextMeshProUGUI bestLapTimeText;
    [SerializeField] private TextMeshProUGUI positionText;
    [SerializeField] private TextMeshProUGUI speedText;

    [Header("Race Results Panel")]
    [SerializeField] private GameObject raceResultsPanel;
    [SerializeField] private TextMeshProUGUI winnerText;
    [SerializeField] private TextMeshProUGUI playerTotalTimeText;
    [SerializeField] private TextMeshProUGUI aiTotalTimeText;
    [SerializeField] private TextMeshProUGUI playerBestLapText;
    [SerializeField] private TextMeshProUGUI aiBestLapText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    private RaceManager raceManager;
    private VehicleController playerVehicle;

    private void Start()
    {
        // Find race manager if not assigned
        if (raceManager == null)
            raceManager = FindObjectOfType<RaceManager>();

        Debug.Log("[RaceUI] RaceManager found: " + (raceManager != null));

        // Find player vehicle if available
        if (raceManager != null && raceManager.playerVehicle != null)
        {
            playerVehicle = raceManager.playerVehicle.GetComponent<VehicleController>();
            Debug.Log("[RaceUI] Found player vehicle through RaceManager");
        }
        else
        {
            // Try to find player vehicle directly
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerVehicle = playerObj.GetComponent<VehicleController>();
                Debug.Log("[RaceUI] Found player vehicle through Player tag");
            }
            else
            {
                // Try to find any VehicleController in the scene
                VehicleController[] vehicles = FindObjectsOfType<VehicleController>();
                if (vehicles.Length > 0)
                {
                    playerVehicle = vehicles[0]; // Use the first one found
                    Debug.Log("[RaceUI] Found player vehicle by searching all VehicleControllers");
                }
                else
                {
                    Debug.LogWarning("[RaceUI] Could not find any player vehicle!");
                }
            }
        }

        // Check if speedText is assigned
        if (speedText == null)
        {
            Debug.LogWarning("[RaceUI] Speed Text is not assigned in the inspector!");
        }
        else
        {
            Debug.Log("[RaceUI] Speed Text is assigned correctly");
        }

        // Initialize UI
        if (raceInfoPanel != null)
            raceInfoPanel.SetActive(true);

        if (raceResultsPanel != null)
            raceResultsPanel.SetActive(false);

        // Add button listeners
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartRace);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
    }

    private void Update()
    {
        if (raceManager == null || !raceManager.IsRaceActive)
            return;

        // Try to find player vehicle if it's not set yet
        if (playerVehicle == null)
        {
            // Try to find through RaceManager first
            if (raceManager != null && raceManager.playerVehicle != null)
            {
                playerVehicle = raceManager.playerVehicle.GetComponent<VehicleController>();
                Debug.Log("[RaceUI] Found player vehicle through RaceManager in Update");
            }
            else
            {
                // Try to find player vehicle directly
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    playerVehicle = playerObj.GetComponent<VehicleController>();
                    Debug.Log("[RaceUI] Found player vehicle through Player tag in Update");
                }
                else
                {
                    // Try to find any VehicleController in the scene
                    VehicleController[] vehicles = FindObjectsOfType<VehicleController>();
                    if (vehicles.Length > 0)
                    {
                        playerVehicle = vehicles[0]; // Use the first one found
                        Debug.Log("[RaceUI] Found player vehicle by searching all VehicleControllers in Update");
                    }
                }
            }
        }

        UpdateRaceInfo();
    }

    /// <summary>
    /// Updates the race information UI.
    /// </summary>
    private void UpdateRaceInfo()
    {
        // Update lap counter
        if (lapCounterText != null)
            lapCounterText.text = $"Lap: {raceManager.CurrentLapPlayer}/{raceManager.TotalLaps}";

        // Update current time
        if (currentTimeText != null)
            currentTimeText.text = $"Time: {FormatTime(raceManager.CurrentLapTimePlayer)}";

        // Update best lap time
        if (bestLapTimeText != null)
        {
            string bestLapString = raceManager.BestLapTimePlayer < float.MaxValue ?
                FormatTime(raceManager.BestLapTimePlayer) : "--:--.---";
            bestLapTimeText.text = $"Best: {bestLapString}";
        }

        // Update position
        if (positionText != null)
        {
            string position = raceManager.IsPlayerAhead ? "1st" : "2nd";
            positionText.text = $"Position: {position}";
        }

        // Update speed display
        if (speedText != null && playerVehicle != null)
        {
            // Get speed directly from the vehicle's localVehicleVelocity
            // Use the Z component (forward) for more accurate speed reading
            float speedMS = Mathf.Abs(playerVehicle.localVehicleVelocity.z);

            // Log speed for debugging
            if (Time.frameCount % 60 == 0) // Log every second
            {
                Debug.Log($"[RaceUI] Vehicle speed: {speedMS:F2} m/s, {speedMS * 2.237f:F1} mph");
                Debug.Log($"[RaceUI] Vehicle localVelocity: {playerVehicle.localVehicleVelocity}");
            }

            // Convert to mph (multiply by 2.237)
            float speedMPH = speedMS * 2.237f;

            // Display speed rounded to nearest integer
            speedText.text = $"Speed: {Mathf.Round(speedMPH)} mph";

            if (Time.frameCount % 60 == 0) // Log every second
            {
                Debug.Log($"[RaceUI] Updated speed text to: {speedText.text}");
            }
        }
        else
        {
            if (Time.frameCount % 300 == 0) // Log every 5 seconds
            {
                Debug.LogWarning($"[RaceUI] Cannot update speed: speedText={speedText != null}, playerVehicle={playerVehicle != null}");
            }
        }
    }



    /// <summary>
    /// Shows the race results panel.
    /// </summary>
    /// <param name="playerWon">True if the player won, false if the AI won.</param>
    public void ShowRaceResults(bool playerWon)
    {
        if (raceResultsPanel != null)
            raceResultsPanel.SetActive(true);

        if (winnerText != null)
            winnerText.text = playerWon ? "You Win!" : "AI Wins!";

        if (playerTotalTimeText != null)
            playerTotalTimeText.text = $"Player Total Time: {FormatTime(raceManager.PlayerTotalTime)}";

        if (aiTotalTimeText != null)
            aiTotalTimeText.text = $"AI Total Time: {FormatTime(raceManager.AITotalTime)}";

        // Add best lap times
        if (playerBestLapText != null)
        {
            string bestLapString = raceManager.BestLapTimePlayer < float.MaxValue ?
                FormatTime(raceManager.BestLapTimePlayer) : "--:--.---";
            playerBestLapText.text = $"Player Best Lap: {bestLapString}";
        }

        if (aiBestLapText != null)
        {
            string bestLapString = raceManager.BestLapTimeAI < float.MaxValue ?
                FormatTime(raceManager.BestLapTimeAI) : "--:--.---";
            aiBestLapText.text = $"AI Best Lap: {bestLapString}";
        }

        // Hide race info panel
        if (raceInfoPanel != null)
            raceInfoPanel.SetActive(false);
    }

    /// <summary>
    /// Restarts the current race.
    /// </summary>
    private void RestartRace()
    {
        // Reload the current scene
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Returns to the main menu.
    /// </summary>
    private void ReturnToMainMenu()
    {
        // Load the main menu scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    /// <summary>
    /// Formats time in seconds to mm:ss.fff format.
    /// </summary>
    private string FormatTime(float timeInSeconds)
    {
        if (timeInSeconds >= float.MaxValue || timeInSeconds < 0)
            return "--:--.---";

        int minutes = (int)(timeInSeconds / 60);
        float seconds = timeInSeconds % 60;

        return $"{minutes:00}:{seconds:00.000}";
    }
}
