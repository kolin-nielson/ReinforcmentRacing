using UnityEngine;

/// <summary>
/// This script ensures that the InputAxisSetup component is added to a GameObject in the scene.
/// It should be attached to a GameObject that persists throughout the game, like a GameManager.
/// </summary>
[DefaultExecutionOrder(-100)] // Execute before other scripts
public class InputAxisSetupInitializer : MonoBehaviour
{
    [Tooltip("Whether to create the InputAxisSetup component if it doesn't exist")]
    public bool createIfMissing = true;
    
    [Tooltip("Whether to log debug information")]
    public bool debugLog = true;
    
    private void Awake()
    {
        // Check if InputAxisSetup already exists
        InputAxisSetup existingSetup = FindObjectOfType<InputAxisSetup>();
        
        if (existingSetup == null && createIfMissing)
        {
            // Create a new GameObject for the InputAxisSetup
            GameObject setupObject = new GameObject("InputAxisSetup");
            setupObject.AddComponent<InputAxisSetup>();
            
            // Make it persist across scenes
            DontDestroyOnLoad(setupObject);
            
            if (debugLog)
            {
                Debug.Log("[InputAxisSetupInitializer] Created InputAxisSetup GameObject");
            }
        }
        else if (existingSetup != null && debugLog)
        {
            Debug.Log("[InputAxisSetupInitializer] InputAxisSetup already exists");
        }
    }
}
