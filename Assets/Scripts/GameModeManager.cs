using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple manager to handle game mode selection (Race AI or Watch AI).
/// </summary>
public class GameModeManager : MonoBehaviour
{
    public enum GameMode
    {
        RaceAI,
        WatchAI
    }

    [SerializeField] private GameMode defaultGameMode = GameMode.RaceAI;

    [Header("Vehicle References")]
    [SerializeField] private GameObject playerVehicle;
    [SerializeField] private GameObject aiVehicle;

    [Header("UI References")]
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject raceUI; // Optional UI for race mode
    [SerializeField] private GameObject watchUI; // Optional UI for watch mode

    [Header("Input Settings")]
    [SerializeField] private InputManager playerInputManager; // Reference to your InputManager script

    private GameMode currentGameMode;

    private void Start()
    {
        // Get game mode from PlayerPrefs (set by MainMenuManager)
        int gameModeIndex = PlayerPrefs.GetInt("GameMode", (int)defaultGameMode);
        currentGameMode = (GameMode)gameModeIndex;

        Debug.Log($"[GameModeManager] Starting game mode: {currentGameMode}");

        // Set up the appropriate mode
        SetupGameMode();
    }

    private void SetupGameMode()
    {
        if (currentGameMode == GameMode.RaceAI)
        {
            // Race mode - enable player vehicle and controls
            SetupRaceMode();
        }
        else
        {
            // Watch mode - disable player vehicle
            SetupWatchMode();
        }

        // Make sure the AI vehicle is always active
        if (aiVehicle != null)
        {
            aiVehicle.SetActive(true);
        }

        // Set up pause menu
        if (pauseMenu != null)
        {
            pauseMenu.SetActive(true);
        }
    }

    private void SetupRaceMode()
    {
        Debug.Log("Setting up Race AI mode");

        // Let the RaceStartManager handle vehicle spawning and race setup
        RaceStartManager raceStartManager = FindObjectOfType<RaceStartManager>();
        if (raceStartManager != null)
        {
            // RaceStartManager will handle everything
            Debug.Log("Using RaceStartManager for race setup");
            return;
        }

        // Fallback if no RaceStartManager is found
        Debug.Log("No RaceStartManager found, using fallback setup");

        // Enable player vehicle
        if (playerVehicle != null)
        {
            playerVehicle.SetActive(true);

            // Set up player input
            SetupPlayerInput(true);
        }

        // Enable race UI if available
        if (raceUI != null)
        {
            raceUI.SetActive(true);
        }

        // Disable watch UI if available
        if (watchUI != null)
        {
            watchUI.SetActive(false);
        }
    }

    private void SetupWatchMode()
    {
        Debug.Log("Setting up Watch AI mode");

        // Let the RaceStartManager handle vehicle spawning and race setup
        RaceStartManager raceStartManager = FindObjectOfType<RaceStartManager>();
        if (raceStartManager != null)
        {
            // RaceStartManager will handle everything
            Debug.Log("Using RaceStartManager for watch mode setup");

            // Just need to set the game mode in PlayerPrefs
            PlayerPrefs.SetInt("GameMode", (int)GameMode.WatchAI);
            return;
        }

        // Fallback if no RaceStartManager is found
        Debug.Log("No RaceStartManager found, using fallback setup");

        // Disable player vehicle
        if (playerVehicle != null)
        {
            playerVehicle.SetActive(false);
        }

        // Set up AI input
        SetupPlayerInput(false);

        // Enable watch UI if available
        if (watchUI != null)
        {
            watchUI.SetActive(true);
        }

        // Disable race UI if available
        if (raceUI != null)
        {
            raceUI.SetActive(false);
        }
    }

    private void SetupPlayerInput(bool enablePlayerControl)
    {
        // Find InputManager on player vehicle if not assigned
        if (playerInputManager == null && playerVehicle != null)
        {
            playerInputManager = playerVehicle.GetComponent<InputManager>();
            if (playerInputManager == null)
            {
                playerInputManager = playerVehicle.GetComponentInChildren<InputManager>();
            }
        }

        // Find InputManager on AI vehicle if still not found
        if (playerInputManager == null && aiVehicle != null)
        {
            playerInputManager = aiVehicle.GetComponent<InputManager>();
            if (playerInputManager == null)
            {
                playerInputManager = aiVehicle.GetComponentInChildren<InputManager>();
            }
        }

        // Configure InputManager
        if (playerInputManager != null)
        {
            if (enablePlayerControl)
            {
                // Race mode - disable ML-Agent control
                playerInputManager.EnableMLAgentControl(false);
                playerInputManager.SetOverrideInputs(false);
                Debug.Log("Enabled player control (disabled ML-Agent control)");
            }
            else
            {
                // Watch mode - enable ML-Agent control
                playerInputManager.EnableMLAgentControl(true);
                playerInputManager.SetOverrideInputs(true);
                Debug.Log("Enabled ML-Agent control (disabled player control)");
            }
        }
        else
        {
            Debug.LogWarning("InputManager not found! Cannot configure input control.");

            // Try to find any InputManager in the scene
            InputManager[] inputManagers = FindObjectsOfType<InputManager>();
            if (inputManagers.Length > 0)
            {
                Debug.Log($"Found {inputManagers.Length} InputManager(s) in the scene:");
                foreach (InputManager im in inputManagers)
                {
                    Debug.Log($"- {im.gameObject.name}");
                }
            }
            else
            {
                Debug.LogError("No InputManager found in the scene!");
            }
        }
    }
}
