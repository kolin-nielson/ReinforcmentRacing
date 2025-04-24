using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enhances raycast visualization in watch mode.
/// This script should be attached to the same GameObject as the RacingAgent.
/// </summary>
public class RaycastVisualizer : MonoBehaviour
{
    [Header("Raycast Visualization")]
    [SerializeField] private Color trackRayColor = new Color(0f, 1f, 0.5f, 0.8f); // Bright green
    [SerializeField] private Color boundaryRayColor = new Color(1f, 0.3f, 1f, 0.8f); // Bright magenta
    [SerializeField] private Color otherRayColor = new Color(1f, 1f, 0.3f, 0.8f); // Bright yellow
    [SerializeField] private Color warningRayColor = new Color(1f, 0.3f, 0.3f, 0.8f); // Bright red
    [SerializeField] private float raycastWidth = 0.3f; // Increased width for better visibility
    [SerializeField] private float raycastDuration = 0.5f; // Increased duration
    [SerializeField] private bool showRaycasts = true;
    [SerializeField] private float rayHeightOffset = 0.5f; // Offset raycasts above ground for visibility

    // References
    private RacingAgent racingAgent;
    private Dictionary<int, LineRenderer> activeRaycasts = new Dictionary<int, LineRenderer>();
    private int raycastCounter = 0;

    // Shader references
    private Shader raycastShader;

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

        // Get references
        racingAgent = GetComponent<RacingAgent>();

        if (racingAgent == null)
        {
            Debug.LogError("[RaycastVisualizer] No RacingAgent found on this GameObject.");
            this.enabled = false;
            return;
        }

        // Find raycast shader
        raycastShader = Shader.Find("Custom/RaycastGlow");

        // Create parent object for raycasts
        GameObject raycastParent = new GameObject("RaycastVisualizations");
        raycastParent.transform.SetParent(transform);

        // Enable raycast visualization in the racing agent
        racingAgent.showRaycasts = showRaycasts;

        // Hook into the raycast events
        HookRaycastEvents();

        Debug.Log("[RaycastVisualizer] Initialized successfully.");
    }

    private void HookRaycastEvents()
    {
        // Use reflection to access the private raycast methods
        System.Type type = racingAgent.GetType();

        // Try to find the DrawRay method
        System.Reflection.MethodInfo drawRayMethod = type.GetMethod("DrawRay",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (drawRayMethod != null)
        {
            Debug.Log("[RaycastVisualizer] Found DrawRay method, but can't hook into it directly.");
            // We can't directly hook into the method, but we can override it with our own implementation
            // This is done by the RacingAgent's showRaycasts flag
        }

        // Since we can't hook directly, we'll use Update to check for new raycasts
        StartCoroutine(MonitorRaycasts());
    }

    private IEnumerator MonitorRaycasts()
    {
        // Wait a moment for everything to initialize
        yield return new WaitForSeconds(1.0f);

        // Log that we're starting to monitor raycasts
        Debug.Log("[RaycastVisualizer] Starting to monitor raycasts...");

        // Create some test raycasts to verify the system is working
        CreateTestRaycasts();

        while (true)
        {
            // Wait for the next frame
            yield return null;

            // Check if raycasts are enabled
            if (!showRaycasts || racingAgent == null)
                continue;

            // Use reflection to access the private raycast debug data
            System.Type type = racingAgent.GetType();

            // Try to access the raycast origins and directions
            System.Reflection.FieldInfo originsField = type.GetField("raycastOrigins",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            System.Reflection.FieldInfo directionsField = type.GetField("raycastDirections",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            System.Reflection.FieldInfo hitsField = type.GetField("raycastHits",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (originsField != null && directionsField != null && hitsField != null)
            {
                Vector3[] origins = (Vector3[])originsField.GetValue(racingAgent);
                Vector3[] directions = (Vector3[])directionsField.GetValue(racingAgent);
                RaycastHit[] hits = (RaycastHit[])hitsField.GetValue(racingAgent);

                if (origins != null && directions != null && hits != null &&
                    origins.Length == directions.Length && origins.Length == hits.Length)
                {
                    // Log the number of raycasts (occasionally)
                    if (Time.frameCount % 300 == 0) // Every 5 seconds at 60fps
                    {
                        Debug.Log($"[RaycastVisualizer] Processing {origins.Length} raycasts");
                    }

                    // Visualize each raycast
                    for (int i = 0; i < origins.Length; i++)
                    {
                        Vector3 origin = origins[i];
                        Vector3 direction = directions[i];
                        RaycastHit hit = hits[i];

                        // Visualize both hit and non-hit raycasts
                        if (hit.collider != null)
                        {
                            // Determine color based on what was hit
                            Color rayColor = otherRayColor;

                            if (hit.collider.CompareTag("Track"))
                            {
                                rayColor = trackRayColor;
                            }
                            else if (hit.collider.CompareTag("Boundary"))
                            {
                                rayColor = boundaryRayColor;

                                // If the hit is very close, use warning color
                                if (hit.distance < 2.0f)
                                {
                                    rayColor = warningRayColor;
                                }
                            }

                            // Create enhanced raycast visualization
                            CreateEnhancedRaycast(origin, hit.point, rayColor);
                        }
                        else
                        {
                            // For non-hit raycasts, show them going in their direction for a fixed distance
                            Vector3 endpoint = origin + direction.normalized * 20f; // 20 meter max distance
                            CreateEnhancedRaycast(origin, endpoint, otherRayColor);
                        }
                    }
                }
                else
                {
                    // If we can't get the raycast data, create some test raycasts occasionally
                    if (Time.frameCount % 60 == 0) // Every second at 60fps
                    {
                        CreateTestRaycasts();
                    }
                }
            }
            else
            {
                // If we can't access the fields, create some test raycasts occasionally
                if (Time.frameCount % 60 == 0) // Every second at 60fps
                {
                    CreateTestRaycasts();
                }
            }
        }
    }

    private void CreateTestRaycasts()
    {
        // Create some test raycasts to verify the system is working
        if (racingAgent != null)
        {
            Vector3 carPosition = racingAgent.transform.position;
            Vector3 carForward = racingAgent.transform.forward;
            Vector3 carRight = racingAgent.transform.right;

            // Create raycasts in different directions
            CreateEnhancedRaycast(carPosition, carPosition + carForward * 10f, trackRayColor);
            CreateEnhancedRaycast(carPosition, carPosition + carForward * 5f + carRight * 5f, boundaryRayColor);
            CreateEnhancedRaycast(carPosition, carPosition + carForward * 5f - carRight * 5f, otherRayColor);
            CreateEnhancedRaycast(carPosition, carPosition - carForward * 5f, warningRayColor);

            Debug.Log("[RaycastVisualizer] Created test raycasts");
        }
    }

    private void CreateEnhancedRaycast(Vector3 start, Vector3 end, Color color)
    {
        // Create a unique ID for this raycast
        int raycastId = raycastCounter++;

        // Create a new GameObject for the raycast
        GameObject raycastObj = new GameObject($"Raycast_{raycastId}");
        raycastObj.transform.SetParent(transform);

        // Add a LineRenderer component
        LineRenderer lineRenderer = raycastObj.AddComponent<LineRenderer>();

        // Set up the LineRenderer
        lineRenderer.startWidth = raycastWidth;
        lineRenderer.endWidth = raycastWidth * 0.5f; // Taper the end

        // Raise the raycast above the ground for better visibility
        start.y += rayHeightOffset;
        end.y += rayHeightOffset;

        // Use the raycast shader if available
        if (raycastShader != null)
        {
            Material rayMaterial = new Material(raycastShader);
            rayMaterial.SetColor("_Color", color);
            rayMaterial.SetColor("_EmissionColor", color);
            rayMaterial.SetFloat("_EmissionIntensity", 5.0f); // Increased emission
            rayMaterial.SetFloat("_FlowSpeed", 5.0f); // Faster flow
            rayMaterial.SetFloat("_FlowIntensity", 0.7f); // More intense flow
            rayMaterial.SetFloat("_PulseSpeed", 8.0f); // Faster pulse
            lineRenderer.material = rayMaterial;
        }
        else
        {
            // Fallback to default material
            Material defaultMaterial = new Material(Shader.Find("Sprites/Default"));
            defaultMaterial.color = color;
            lineRenderer.material = defaultMaterial;
        }

        // Set the positions
        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);

        // Set other properties
        lineRenderer.startColor = color;
        lineRenderer.endColor = new Color(color.r, color.g, color.b, 0.0f); // Fade out at the end
        lineRenderer.useWorldSpace = true;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.numCapVertices = 4;
        lineRenderer.alignment = LineAlignment.View;

        // Store the raycast
        activeRaycasts[raycastId] = lineRenderer;

        // Destroy the raycast after a delay
        StartCoroutine(DestroyRaycastAfterDelay(raycastId, raycastDuration));

        // Log for debugging
        if (raycastCounter % 20 == 0) // Don't spam the log
        {
            Debug.Log($"[RaycastVisualizer] Created raycast from {start} to {end} with color {color}");
        }
    }

    private IEnumerator DestroyRaycastAfterDelay(int raycastId, float delay)
    {
        // Wait for the specified delay
        yield return new WaitForSeconds(delay);

        // Get the LineRenderer
        if (activeRaycasts.TryGetValue(raycastId, out LineRenderer lineRenderer))
        {
            // Remove from dictionary
            activeRaycasts.Remove(raycastId);

            // Destroy the GameObject
            if (lineRenderer != null && lineRenderer.gameObject != null)
            {
                Destroy(lineRenderer.gameObject);
            }
        }
    }

    public void SetShowRaycasts(bool show)
    {
        showRaycasts = show;

        if (racingAgent != null)
        {
            racingAgent.showRaycasts = show;
        }
    }
}
