using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Simple start/finish line detector for lap counting.
/// </summary>
public class StartFinishLine : MonoBehaviour
{
    [Tooltip("Event triggered when the player crosses the start/finish line")]
    public UnityEvent OnPlayerCrossed = new UnityEvent();

    [Tooltip("Event triggered when the AI crosses the start/finish line")]
    public UnityEvent OnAICrossed = new UnityEvent();

    [Tooltip("Tag of the player vehicle")]
    public string playerTag = "Player";

    [Tooltip("Tag of the AI vehicle")]
    public string aiTag = "AI";

    [Tooltip("Minimum time between crossings (to prevent multiple triggers)")]
    public float crossingCooldown = 5f; // Increased from 3f to 5f

    private float lastPlayerCrossingTime = -10f;
    private float lastAICrossingTime = -10f;

    private void OnTriggerEnter(Collider other)
    {
        // Check if it's the player
        if (other.CompareTag(playerTag) || other.transform.root.CompareTag(playerTag))
        {
            // Check cooldown to prevent multiple triggers
            if (Time.time - lastPlayerCrossingTime > crossingCooldown)
            {
                Debug.Log("[StartFinishLine] Player crossed the start/finish line");
                lastPlayerCrossingTime = Time.time;
                OnPlayerCrossed.Invoke();
            }
        }
        // Check if it's the AI
        else if (other.CompareTag(aiTag) || other.transform.root.CompareTag(aiTag))
        {
            // Check cooldown to prevent multiple triggers
            if (Time.time - lastAICrossingTime > crossingCooldown)
            {
                Debug.Log("[StartFinishLine] AI crossed the start/finish line");
                lastAICrossingTime = Time.time;
                OnAICrossed.Invoke();
            }
        }
        else
        {
            // Debug what crossed the line
            Debug.Log($"[StartFinishLine] Object crossed: {other.gameObject.name} with tag {other.tag}");

            // Try to find a RacingAgent or InputManager component on the object or its parents
            RacingAgent racingAgent = other.GetComponentInParent<RacingAgent>();
            InputManager inputManager = other.GetComponentInParent<InputManager>();

            if (racingAgent != null)
            {
                Debug.Log("[StartFinishLine] AI agent detected crossing the line");
                if (Time.time - lastAICrossingTime > crossingCooldown)
                {
                    lastAICrossingTime = Time.time;
                    OnAICrossed.Invoke();
                }
            }
            else if (inputManager != null && !inputManager.isMLAgentControlled)
            {
                Debug.Log("[StartFinishLine] Player detected crossing the line");
                if (Time.time - lastPlayerCrossingTime > crossingCooldown)
                {
                    lastPlayerCrossingTime = Time.time;
                    OnPlayerCrossed.Invoke();
                }
            }
        }
    }
}
