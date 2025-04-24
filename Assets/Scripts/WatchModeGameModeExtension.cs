using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Extends the GameModeManager to add watch mode visualization features.
/// This script should be attached to the same GameObject as the GameModeManager.
/// </summary>
public class WatchModeGameModeExtension : MonoBehaviour
{
    [Header("Watch Mode Visualization")]
    [SerializeField] private GameObject watchModeVisualizerPrefab;
    [SerializeField] private GameObject watchModeUIPrefab;

    private GameModeManager gameModeManager;
    private bool isWatchMode = false;

    private void Awake()
    {
        // Get reference to GameModeManager
        gameModeManager = GetComponent<GameModeManager>();

        // Check if we're in watch mode
        isWatchMode = PlayerPrefs.GetInt("GameMode", 0) == 1;

        if (!isWatchMode)
        {
            // If not in watch mode, disable this component
            this.enabled = false;
            return;
        }
    }

    private void Start()
    {
        // Wait a frame to ensure GameModeManager has initialized
        StartCoroutine(InitializeWatchModeVisualization());
    }

    private IEnumerator InitializeWatchModeVisualization()
    {
        // Wait for GameModeManager to initialize
        yield return new WaitForSeconds(0.5f);

        // Create watch mode visualizer
        GameObject visualizerObj;
        if (watchModeVisualizerPrefab != null)
        {
            visualizerObj = Instantiate(watchModeVisualizerPrefab);
        }
        else
        {
            // Create from scratch
            visualizerObj = new GameObject("WatchModeVisualizer");
            visualizerObj.AddComponent<WatchModeVisualizer>();

            Debug.Log("[WatchModeGameModeExtension] Created WatchModeVisualizer. Please assign references in the inspector.");
        }

        // Create watch mode UI
        if (watchModeUIPrefab != null)
        {
            Instantiate(watchModeUIPrefab);
            Debug.Log("[WatchModeGameModeExtension] Instantiated WatchModeUI prefab.");
        }

        // Add raycast enabler to all racing agents
        RacingAgent[] racingAgents = FindObjectsOfType<RacingAgent>();
        foreach (RacingAgent agent in racingAgents)
        {
            if (!agent.GetComponent<WatchModeRaycastEnabler>())
            {
                agent.gameObject.AddComponent<WatchModeRaycastEnabler>();
            }
        }

        Debug.Log("[WatchModeGameModeExtension] Watch mode visualization system initialized.");
    }
}
