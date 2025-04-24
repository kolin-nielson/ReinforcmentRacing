using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using UnityEngine;

/// <summary>
/// Adds extra body friction to help the vehicle with turning when using physics
/// </summary>
public class BodyFriction : MonoBehaviour
{
    [Header("Friction Settings")]
    [SerializeField]
    private float lateralFrictionMultiplier = 3.5f; // Increased from 2.0f for more grip
    [SerializeField]
    private float lowerBodyFriction = 0.2f; // Increased from 0.1f
    [SerializeField]
    private float upperBodyFriction = 0.8f; // Increased from 0.5f

    [Header("Advanced Controls")]
    [SerializeField]
    private float highSpeedThreshold = 15f; // When to apply extra high-speed grip
    [SerializeField]
    private float extraGripAtHighSpeed = 1.5f; // Extra grip multiplier at high speeds
    [SerializeField]
    private float counterSteeringFactor = 0.3f; // How much to help when counter-steering
    [SerializeField]
    private float downforceMultiplier = 0.25f; // Downforce based on lateral velocity
    [SerializeField]
    private float minVelocityThreshold = 0.5f; // Increased from 0.6f

    [Header("Collision-Based Friction")]
    [SerializeField]
    private bool useCollisionFriction = true;
    [SerializeField]
    private Collider bodyCollider;
    // No longer serialized, managed internally
    private PhysicsMaterial bodyFrictionMaterial;
    // *** NEW: Flag to track if we created the material instance ***
    private bool isMaterialOwner = false;

    [Header("Debug")]
    [SerializeField]
    private bool showDebug = false;
    // Removed redundant friction fields, use lower/upperBodyFriction
    // [SerializeField] private float colliderDynamicFriction = 0.5f; // Not used directly
    // [SerializeField] private float colliderStaticFriction = 0.7f;  // Not used directly
    [SerializeField] private bool logOnlyMLAgentIssues = true; // Only log when ML-Agent has issues

    // Components
    private Rigidbody rb;
    private InputManager inputManager;
    private VehicleController vehicleController;

    // Last frame values
    private Vector3 lastVelocity; // Note: This isn't used in the provided FixedUpdate logic. Maybe intended for something else?
    private Vector3 lastLateralVelocity;
    private float lastSteeringInput;
    private float debugLogTimer; // Note: This isn't used either.


    void Start()
    {
        rb = GetComponent<Rigidbody>();
        inputManager = GetComponent<InputManager>();
        vehicleController = GetComponent<VehicleController>();

        if (rb == null)
        {
            Debug.LogError("[BodyFriction] No Rigidbody found on this GameObject. Disabling component.");
            enabled = false;
            return;
        }

        if (inputManager == null)
        {
            Debug.LogWarning("[BodyFriction] No InputManager found on this GameObject. Steering detection may not work correctly.");
        }

        // Setup physics material for the body collider if enabled
        if (useCollisionFriction && bodyCollider != null)
        {
            // *** MEMORY FIX: Create and manage material lifecycle ***
            // Create a new material instance specifically for this object
            // This prevents modifying shared assets and allows safe destruction.
            bodyFrictionMaterial = new PhysicsMaterial(gameObject.name + "_BodyFrictionMat"); // Give it a unique name for profiling
            bodyFrictionMaterial.dynamicFriction = lowerBodyFriction;
            bodyFrictionMaterial.staticFriction = lowerBodyFriction;
            bodyFrictionMaterial.bounciness = 0;
            // Changed from Minimum to Maximum for potentially more predictable friction combination
            bodyFrictionMaterial.frictionCombine = PhysicsMaterialCombine.Maximum;
            bodyFrictionMaterial.bounceCombine = PhysicsMaterialCombine.Minimum;

            bodyCollider.material = bodyFrictionMaterial;
            isMaterialOwner = true; // Mark that we created this instance

            // *** MEMORY FIX: Subscribe to event (unsubscribe handled in OnDestroy) ***
            if (vehicleController != null && vehicleController.VehicleEvents != null)
            {
                vehicleController.VehicleEvents.OnGrounded.AddListener(ResetFrictionMatProperties);
            }
            else if (vehicleController == null)
            {
                 Debug.LogWarning("[BodyFriction] No VehicleController found, cannot subscribe to OnGrounded event.");
            }


            Debug.Log("[BodyFriction] Collision-based friction initialized with unique PhysicsMaterial.");
        }
        else if (!useCollisionFriction)
        {
             Debug.Log("[BodyFriction] Collision-based friction is disabled.");
        }
        else if (bodyCollider == null)
        {
             Debug.LogWarning("[BodyFriction] Collision-based friction enabled, but no Body Collider assigned.");
        }


        Debug.Log("[BodyFriction] Component initialized with lateral friction multiplier: " + lateralFrictionMultiplier);
    }

    // *** NEW: OnDestroy method to clean up resources ***
    private void OnDestroy()
    {
        // *** MEMORY FIX: Unsubscribe from the event ***
        if (vehicleController != null && vehicleController.VehicleEvents != null)
        {
            vehicleController.VehicleEvents.OnGrounded.RemoveListener(ResetFrictionMatProperties);
             Debug.Log("[BodyFriction] Unsubscribed from OnGrounded event.");
        }

        // *** MEMORY FIX: Destroy the material ONLY if this script created it ***
        if (isMaterialOwner && bodyFrictionMaterial != null)
        {
            // Optional: Remove reference from collider first
            if (bodyCollider != null && bodyCollider.sharedMaterial == bodyFrictionMaterial)
            {
                bodyCollider.material = null; // Use .material to assign null, .sharedMaterial is read-only for assets
            }

            Destroy(bodyFrictionMaterial);
            bodyFrictionMaterial = null;
            isMaterialOwner = false; // Just to be clean
            Debug.Log("[BodyFriction] Destroyed owned PhysicsMaterial instance.");
        }
        else if (!isMaterialOwner && bodyFrictionMaterial != null)
        {
             // This case shouldn't happen with the current Start() logic, but good for safety
             Debug.LogWarning("[BodyFriction] OnDestroy called, but not the owner of the PhysicsMaterial. Not destroying.");
        }
    }


    // --- Core Friction Logic (Unchanged) ---

    private void FixedUpdate()
    {
        // Only apply extra lateral friction if moving beyond a minimum speed
        if (rb.linearVelocity.magnitude < minVelocityThreshold)
            return;

        // Get local velocity
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        // Get steering input
        float steerInput = inputManager != null ? inputManager.SteerInput : 0f;

        // Calculate lateral velocity (sideways movement relative to the car's orientation)
        Vector3 lateralVelocity = transform.right * localVelocity.x;
        float lateralSpeed = lateralVelocity.magnitude;

        // Check if we're counter-steering (turning against the direction of the drift)
        bool isCounterSteering = false;
        if (Mathf.Abs(steerInput) > 0.1f && lateralSpeed > 2f)
        {
            // Determine the direction of lateral movement (sign of local x velocity)
            float lateralDir = Mathf.Sign(localVelocity.x);
            // Determine the direction of steering input
            float steerDir = Mathf.Sign(steerInput);

            // If steering input is opposite to lateral velocity direction, we're counter-steering
            isCounterSteering = (lateralDir * steerDir < 0);
        }

        // Calculate the friction multiplier to apply
        float appliedMultiplier = lateralFrictionMultiplier;

        // Apply more friction when counter-steering to help regain control
        if (isCounterSteering)
        {
            appliedMultiplier *= (1f + counterSteeringFactor);

            // Log counter-steering assist for ML-Agent if debugging is enabled
            if (showDebug && inputManager != null && inputManager.isMLAgentControlled && Time.frameCount % 300 == 0)
            {
                Debug.Log($"[MLDiag] BodyFriction: ML-Agent counter-steering, extra grip applied. Multiplier: {appliedMultiplier:F1}");
            }
        }

        // Apply more friction at higher speeds to simulate aerodynamic effects or tire behavior
        if (rb.linearVelocity.magnitude > highSpeedThreshold)
        {
            // Lerp towards the extra grip factor based on how much speed exceeds the threshold
            float speedBonus = Mathf.Lerp(1f, extraGripAtHighSpeed,
                Mathf.Clamp01((rb.linearVelocity.magnitude - highSpeedThreshold) / 10f)); // Scale over a 10 m/s range
            appliedMultiplier *= speedBonus;
        }

        // Only apply the lateral friction force when needed:
        // 1. If there's significant sideways velocity (drifting/sliding) OR
        // 2. If the agent/player is actively steering
        if (lateralSpeed > 1.0f || Mathf.Abs(steerInput) > 0.1f)
        {
            // Calculate the force required to oppose the lateral velocity
            // ForceMode.Acceleration makes the force mass-independent, applying an acceleration directly
            Vector3 lateralFrictionForce = -lateralVelocity * appliedMultiplier;
            rb.AddForce(lateralFrictionForce, ForceMode.Acceleration);

            // Apply additional downward force (stabilizing force) during turns or drifts
            // This helps prevent the car from tipping over and simulates downforce effects
            // Make it proportional to lateral speed to increase effect during faster slides/turns
            if (Mathf.Abs(steerInput) > 0.2f || lateralSpeed > 3f)
            {
                // ForceMode.Force includes mass, representing a physical force
                Vector3 stabilizingForce = -transform.up * lateralSpeed * downforceMultiplier * rb.mass;
                rb.AddForce(stabilizingForce, ForceMode.Force);

                // Log potential issues for ML-Agent: high lateral force but low forward speed
                if (showDebug && inputManager != null && inputManager.isMLAgentControlled &&
                    lateralSpeed > 5f && rb.linearVelocity.magnitude < 2.0f && Time.frameCount % 180 == 0)
                {
                    Debug.Log($"[MLDiag] ISSUE: ML-Agent high lateral force ({lateralSpeed:F1}m/s) but low forward velocity ({rb.linearVelocity.magnitude:F1}m/s)");
                }
            }

            // Apply extra grip (impulse) when quickly changing steering direction (e.g., flicking the wheel)
            // This helps make quick direction changes more effective
            if (Mathf.Sign(steerInput) != Mathf.Sign(lastSteeringInput) && Mathf.Abs(steerInput) > 0.3f)
            {
                // ForceMode.Impulse applies an instantaneous change in momentum
                Vector3 directionChangeForce = -lateralVelocity.normalized *
                    lateralSpeed * 1.5f * rb.mass; // Apply a stronger counter-force
                rb.AddForce(directionChangeForce, ForceMode.Impulse);

                // Log steering direction changes for ML-Agent
                if (showDebug && inputManager != null && inputManager.isMLAgentControlled && Time.frameCount % 180 == 0)
                {
                    Debug.Log($"[MLDiag] ML-Agent steering direction change from {lastSteeringInput:F2} to {steerInput:F2}");
                }
            }

            // Log detailed friction info specifically for ML-Agent when steering but not moving
            if (showDebug && inputManager != null && inputManager.isMLAgentControlled &&
                Mathf.Abs(steerInput) > 0.2f && rb.linearVelocity.magnitude < 1.0f && Time.frameCount % 180 == 0)
            {
                Debug.Log($"[MLDiag] ISSUE: ML-Agent steering ({steerInput:F2}) with friction force: {lateralFrictionForce.magnitude:F1}N but not moving ({rb.linearVelocity.magnitude:F1}m/s)");
            }
        }

        // Store current values for the next frame's comparison
        lastLateralVelocity = lateralVelocity;
        lastSteeringInput = steerInput;
    }


    // --- Collision Handling (Unchanged) ---

    private void OnCollisionEnter(Collision collision)
    {
        if (useCollisionFriction && bodyFrictionMaterial != null)
        {
            CustomBodyFrictionLogic(collision);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        // Also update friction during ongoing collisions
        if (useCollisionFriction && bodyFrictionMaterial != null)
        {
            CustomBodyFrictionLogic(collision);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        // Reset friction properties when collision ends
        if (useCollisionFriction && bodyFrictionMaterial != null)
        {
            // Only reset if we are the owner, otherwise we might mess up a shared material
            if (isMaterialOwner)
            {
                ResetFrictionMatProperties();
            }
        }
    }

    /// <summary>
    /// Logic applied during collision contacts to adjust friction material properties.
    /// </summary>
    private void CustomBodyFrictionLogic(Collision collision)
    {
        // Need material owner check if we modify the material properties directly
        if (!isMaterialOwner || bodyFrictionMaterial == null) return;

        // Find the first contact point
        ContactPoint contact = collision.GetContact(0); // Use GetContact for potentially better info
        Vector3 contactPoint = contact.point;

        // Transform the contact point to the vehicle's local space to determine height
        Vector3 localContactPoint = transform.InverseTransformPoint(contactPoint);
        // Check if the contact point is on the lower half of the vehicle body (local y < 0)
        bool isLowerHalf = localContactPoint.y < 0;

        // Determine the base coefficient of friction based on the contact point's location
        float coefficientOfFriction = isLowerHalf ? lowerBodyFriction : upperBodyFriction;

        // Increase friction at higher speeds to simulate downforce or tire behavior during collisions
        if (rb.linearVelocity.magnitude > highSpeedThreshold)
        {
            // Apply extra friction at high speeds to prevent sliding along walls
            float speedRatio = Mathf.Clamp01((rb.linearVelocity.magnitude - highSpeedThreshold) / 10f);
            coefficientOfFriction *= (1f + speedRatio * (extraGripAtHighSpeed - 1f));
        }

        // Modify friction if grounded and steering
        if (vehicleController != null && vehicleController.vehicleIsGrounded)
        {
            // Prevent friction from being set too low when grounded
            coefficientOfFriction = Mathf.Max(coefficientOfFriction, lowerBodyFriction);

            // If actively steering, slightly increase friction to help grip during turns against walls
            if (inputManager != null && Mathf.Abs(inputManager.SteerInput) > 0.2f)
            {
                coefficientOfFriction *= 1.2f;
            }
        }

        // Apply the calculated friction values to the material
        // Only modify the material if we own it
        bodyFrictionMaterial.staticFriction = coefficientOfFriction;
        bodyFrictionMaterial.dynamicFriction = coefficientOfFriction * 0.8f; // Dynamic slightly lower than static

        // Log friction changes for ML-Agent if debugging is enabled and conditions met
        if (showDebug && inputManager != null && inputManager.isMLAgentControlled &&
            rb.linearVelocity.magnitude < 1.0f && Time.frameCount % 300 == 0)
        {
            Debug.Log($"[MLDiag] BodyCollision: ML-Agent Friction updated to={coefficientOfFriction:F2}, Speed={rb.linearVelocity.magnitude:F1}");
        }
    }

    /// <summary>
    /// Resets the friction material properties, typically when grounded or exiting collision.
    /// </summary>
    private void ResetFrictionMatProperties()
    {
        // Need material owner check if we modify the material properties directly
        if (isMaterialOwner && bodyFrictionMaterial != null)
        {
            // Reset to base lower friction values
            bodyFrictionMaterial.staticFriction = lowerBodyFriction;
            bodyFrictionMaterial.dynamicFriction = lowerBodyFriction;
            // Optional: Log the reset if needed for debugging
            // if (showDebug) Debug.Log("[BodyFriction] Resetting friction material properties.");
        }
    }
}