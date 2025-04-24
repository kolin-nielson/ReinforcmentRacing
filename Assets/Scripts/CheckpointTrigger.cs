using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Handles checkpoint trigger detection for vehicles.
/// </summary>
public class CheckpointTrigger : MonoBehaviour
{
    [SerializeField] private int checkpointIndex;
    [SerializeField] private bool isFinishLine = false;

    // Event that can be used to notify other scripts when a checkpoint is triggered
    public UnityEvent<int> OnCheckpointTriggered;

    private void Awake()
    {
        // Initialize the event if it's null
        if (OnCheckpointTriggered == null)
            OnCheckpointTriggered = new UnityEvent<int>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Debug log to see what's triggering the checkpoint
        Debug.Log($"[CheckpointTrigger] Trigger entered by {other.gameObject.name} with tag {other.tag}");

        // Check if the collider is a vehicle (player or AI)
        if (other.CompareTag("Player") || other.CompareTag("AI"))
        {
            Debug.Log($"[CheckpointTrigger] Player/AI triggered checkpoint {checkpointIndex}");
            // Invoke the event with the checkpoint index
            OnCheckpointTriggered.Invoke(checkpointIndex);
        }
        else
        {
            // If the vehicle doesn't have the correct tag, try checking its parent or root
            Transform rootTransform = other.transform.root;
            if (rootTransform.CompareTag("Player") || rootTransform.CompareTag("AI"))
            {
                Debug.Log($"[CheckpointTrigger] Root object {rootTransform.name} triggered checkpoint {checkpointIndex}");
                // Invoke the event with the checkpoint index
                OnCheckpointTriggered.Invoke(checkpointIndex);
            }
            else
            {
                // Try to find a RacingAgent or InputManager component on the object or its parents
                RacingAgent racingAgent = other.GetComponentInParent<RacingAgent>();
                InputManager inputManager = other.GetComponentInParent<InputManager>();

                if (racingAgent != null || inputManager != null)
                {
                    Debug.Log($"[CheckpointTrigger] Vehicle component found on {other.gameObject.name}, triggered checkpoint {checkpointIndex}");
                    // Invoke the event with the checkpoint index
                    OnCheckpointTriggered.Invoke(checkpointIndex);
                }
            }
        }
    }

    // Set the checkpoint index (used by CheckpointGenerator)
    public void SetCheckpointIndex(int index)
    {
        checkpointIndex = index;
        isFinishLine = (index == 0); // Assuming checkpoint 0 is the start/finish line
    }

    // Get the checkpoint index
    public int GetCheckpointIndex()
    {
        return checkpointIndex;
    }

    // Check if this is the finish line
    public bool IsFinishLine()
    {
        return isFinishLine;
    }
}
