using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Initializes the watch mode visualization system.
/// This script should be attached to a GameObject in the scene.
/// </summary>
public class WatchModeInitializer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject watchModeVisualizerPrefab;
    [SerializeField] private GameObject watchModeUIPrefab;

    private void Awake()
    {
        // Check if we're in watch mode
        bool isWatchMode = PlayerPrefs.GetInt("GameMode", 0) == 1;

        if (!isWatchMode)
        {
            // If not in watch mode, disable this component
            this.enabled = false;
            return;
        }
    }

    private void Start()
    {
        // Initialize watch mode visualization
        StartCoroutine(InitializeWatchMode());
    }

    private IEnumerator InitializeWatchMode()
    {
        // Wait a moment for other components to initialize
        yield return new WaitForSeconds(0.5f);

        // Create visualizer
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

            Debug.Log("[WatchModeInitializer] Created WatchModeVisualizer. Please assign references in the inspector.");
        }

        // Create UI
        if (watchModeUIPrefab != null)
        {
            Instantiate(watchModeUIPrefab);
            Debug.Log("[WatchModeInitializer] Instantiated WatchModeUI prefab.");
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

        Debug.Log("[WatchModeInitializer] Watch mode visualization system initialized.");
    }
}
