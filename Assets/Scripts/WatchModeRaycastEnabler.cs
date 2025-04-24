using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enables the RacingAgent's built-in raycast visualization in watch mode.
/// This script should be attached to the same GameObject as the RacingAgent.
/// </summary>
public class WatchModeRaycastEnabler : MonoBehaviour
{
    [SerializeField] private RacingAgent racingAgent;
    
    private void Start()
    {
        // Check if we're in watch mode
        bool isWatchMode = PlayerPrefs.GetInt("GameMode", 0) == 1;
        
        if (!isWatchMode)
        {
            // If not in watch mode, disable this component
            this.enabled = false;
            return;
        }
        
        // Find racing agent if not assigned
        if (racingAgent == null)
            racingAgent = GetComponent<RacingAgent>();
            
        if (racingAgent == null)
            racingAgent = FindObjectOfType<RacingAgent>();
            
        // Enable raycast visualization
        if (racingAgent != null)
        {
            racingAgent.showRaycasts = true;
            racingAgent.showUpcomingCheckpoints = true;
            racingAgent.showCenterlineGizmo = true;
            racingAgent.showRecoveryHelpers = true;
        }
    }
}
