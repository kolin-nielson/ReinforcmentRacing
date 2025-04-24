using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [Tooltip("AI's acceleration input (-1 to 1)")]
    public float aiAccelerationInput = 0f;

    [Tooltip("AI's handbrake input (0 to 1)")]
    public float aiHandbrakeInput = 0f;

    [Tooltip("AI's steering input (-1 to 1)")]
    public float aiSteerInput = 0f;

    public KeyboardInput keyboardInput = new KeyboardInput();
    public ControllerInput controllerInput = new ControllerInput();
    private float _accelerationInput = 0f;
    private float _handbrakeInput = 0f;
    private float _steerInput = 0f;
    private float lastLogTime = 0f;

    // Track the last frame when ML-Agent inputs were received
    private int lastMLAgentInputFrame = -1;

    [SerializeField]
    private float logFrequency = 1.0f;

    [Header("Controller Settings")]
    [SerializeField, Range(0.01f, 1.0f)]
    private float controllerDeadzone = 0.1f;

    [SerializeField, Range(0.1f, 1.0f)]
    private float triggerDeadzone = 0.25f; // Higher deadzone specifically for triggers to prevent auto-movement

    [SerializeField, Range(0.1f, 2.0f)]
    private float controllerSteeringSensitivity = 1.0f;

    [SerializeField, Range(0.1f, 2.0f)]
    private float controllerAccelerationSensitivity = 1.0f;

    [SerializeField]
    private bool invertSteeringAxis = false;

    [Header("Debug Settings")]
    [SerializeField]
    private bool logInputs = true;

    [SerializeField]
    private bool showControllerDebug = true;

    [Header("Car Components")]
    [SerializeField]
    private VehicleController vehicleController;

    [Header("Debug")]
    [SerializeField]
    private bool verboseLogging = true;

    [Header("New Input System Settings")]
    [Tooltip("Enable support for Unity InputSystem Gamepad (Xbox One)")]
    [SerializeField]
    private bool enableNewInputSystem = true;

    [Serializable]
    public class KeyboardInput
    {
        public KeyCode accelerate = KeyCode.W;
        public KeyCode decelerate = KeyCode.S;
        public KeyCode handBrake = KeyCode.Space;
        public KeyCode steerLeft = KeyCode.A;
        public KeyCode steerRight = KeyCode.D;
    }

    [Serializable]
    public class ControllerInput
    {
        [Tooltip("Joystick axis for steering (left stick horizontal)")]
        public string steeringAxis = "Horizontal";

        [Tooltip("Right trigger for acceleration (joystick axis)")]
        [SerializeField]
        public string accelerationAxis = "RT";

        [Tooltip("Left trigger for braking/reverse (joystick axis)")]
        [SerializeField]
        public string brakeAxis = "LT";

        [Tooltip("X button on Xbox controller for handbrake")]
        public string handbrakeButton = "Fire3";

        // Debug info for controller axes
        [HideInInspector]
        public string lastActiveRightTriggerAxis = "";

        [HideInInspector]
        public string lastActiveLeftTriggerAxis = "";

        // Alternative axis names for different controllers
        [HideInInspector]
        public string[] alternativeAccelerationAxes = new string[] {
            "RT", "Right Trigger", "joystick 1 axis 5", "joystick 1 axis 9", "joystick 1 axis 3", "joystick 1 axis 4",
            "3rd axis", "Trigger", "Axis 3", "Axis 9", "Axis 5", "Axis 4",
            // Xbox controller specific
            "XboxRightTriggerAxis", "JoystickAxis3", "Joy0Axis3", "Joy0Axis9", "Joy0Axis5", "Joy0Axis4",
            // Additional numbered axes to try
            "Axis 0", "Axis 1", "Axis 2", "Axis 3", "Axis 4", "Axis 5", "Axis 6", "Axis 7", "Axis 8", "Axis 9",
            // Direct numbered axes
            "X axis", "Y axis", "3rd axis", "4th axis", "5th axis", "6th axis", "7th axis", "8th axis", "9th axis", "10th axis"
        };

        [HideInInspector]
        public string[] alternativeBrakeAxes = new string[] {
            "LT", "Left Trigger", "joystick 1 axis 2", "joystick 1 axis 8", "joystick 1 axis 3", "joystick 1 axis 5",
            "3rd axis (inverted)", "Trigger", "Axis 3", "Axis 8", "Axis 2", "Axis 5",
            // Xbox controller specific
            "XboxLeftTriggerAxis", "JoystickAxis2", "Joy0Axis2", "Joy0Axis8", "Joy0Axis5",
            // Additional numbered axes to try
            "Axis 0", "Axis 1", "Axis 2", "Axis 3", "Axis 4", "Axis 5", "Axis 6", "Axis 7", "Axis 8", "Axis 9",
            // Direct numbered axes
            "X axis", "Y axis", "3rd axis", "4th axis", "5th axis", "6th axis", "7th axis", "8th axis", "9th axis", "10th axis"
        };
    }

    public float AccelerationInput { get; set; }
    public float HandbrakeInput { get; set; }
    public bool isMLAgentControlled { get; set; } = false;

    // AI Control
    [Header("AI Control")]
    [Tooltip("Enable this to let AI control the vehicle")]
    public bool overrideInputs { get; set; } = false;

    // Input properties - changing from private setters to public to allow direct manipulation from Heuristic
    public float SteerInput { get; set; }

    /// <summary>
    /// Toggle ML-Agent control on or off.
    /// </summary>
    /// <param name="enable">Whether to enable ML-Agent control</param>
    public void EnableMLAgentControl(bool enable)
    {
        isMLAgentControlled = enable;
        overrideInputs = enable;

        // Reset inputs when disabling AI control
        if (!enable)
        {
            aiSteerInput = 0f;
            aiAccelerationInput = 0f;
            aiHandbrakeInput = 0f;
        }

        // Reset all inputs when switching control modes
        if (enable)
        {
            // When enabling ML-Agent control, zero out both sets of inputs
            aiSteerInput = 0f;
            aiAccelerationInput = 0f;
            aiHandbrakeInput = 0f;
        }

        SteerInput = 0f;
        AccelerationInput = 0f;
        HandbrakeInput = 0f;
    }

    /// <summary>
    /// LEGACY METHOD: Only used for backward compatibility.
    /// For pure ML-Agent training, use SetMLAgentInputs instead.
    /// </summary>
    public void ForceApplyInputs(float steer, float accel, float handbrake)
    {
        // Just delegate to SetMLAgentInputs for proper handling
        SetMLAgentInputs(steer, accel, handbrake);

        if (verboseLogging && Time.frameCount % 300 == 0) // Once per 5 seconds
        {
            Debug.LogWarning("[InputManager] ForceApplyInputs is deprecated - use SetMLAgentInputs for natural ML-Agent training");
        }
    }

    /// <summary>
    /// Set ML-Agent acceleration input directly - useful for Heuristic mode or direct control
    /// </summary>
    public void SetAccelerationInput(float acceleration)
    {
        aiAccelerationInput = Mathf.Clamp(acceleration, -1f, 1f);

        // If in heuristic mode and running ML-Agent, this will bypass the normal Input.GetAxis checks
        if (isMLAgentControlled && overrideInputs)
        {
            AccelerationInput = aiAccelerationInput;
        }
    }

    /// <summary>
    /// Set ML-Agent handbrake input directly - useful for Heuristic mode or direct control
    /// </summary>
    public void SetHandbrakeInput(float handbrake)
    {
        aiHandbrakeInput = Mathf.Clamp01(handbrake);

        // If in heuristic mode and running ML-Agent, this will bypass the normal Input.GetAxis checks
        if (isMLAgentControlled && overrideInputs)
        {
            HandbrakeInput = aiHandbrakeInput;
        }
    }

    /// <summary>
    /// Sets ML-Agent inputs that will be used during processing
    /// </summary>
    public void SetMLAgentInputs(float steer, float accel, float handbrake)
    {
        // Store the AI inputs
        aiSteerInput = steer;
        aiAccelerationInput = accel;
        aiHandbrakeInput = handbrake;

        // Mark that we received ML-Agent inputs this frame
        lastMLAgentInputFrame = Time.frameCount;

        // If we are in ML-Agent mode, apply the inputs directly for immediate effect
        if (isMLAgentControlled && overrideInputs)
        {
            // Apply inputs directly to the properties
            SteerInput = steer;
            AccelerationInput = accel;
            HandbrakeInput = handbrake;

            // Apply inputs to vehicle controller directly
            ApplyInputsToVehicle();

        }
    }

    /// <summary>
    /// Set override inputs flag - allows external classes to control whether AI inputs override player inputs
    /// </summary>
    /// <param name="value">True to use AI inputs, false to use player inputs</param>
    public void SetOverrideInputs(bool value)
    {
        Debug.Log($"[InputManager] SetOverrideInputs called with value: {value}");
        overrideInputs = value;

        if (logInputs)
        {
            Debug.Log($"Input override {(value ? "enabled" : "disabled")}");
        }
    }

    /// <summary>
    /// Set ML-Agent inputs directly - useful for Heuristic mode or direct control
    /// </summary>
    public void SetSteeringInput(float steer)
    {
        aiSteerInput = Mathf.Clamp(steer, -1f, 1f);

        // If in heuristic mode and running ML-Agent, this will bypass the normal Input.GetAxis checks
        if (isMLAgentControlled && overrideInputs)
        {
            SteerInput = aiSteerInput;
        }
    }

    /// <summary>
    /// Set the axis name for the right trigger (acceleration)
    /// </summary>
    public void SetAccelerationAxis(string axisName)
    {
        if (!string.IsNullOrEmpty(axisName))
        {
            controllerInput.accelerationAxis = axisName;
            controllerInput.lastActiveRightTriggerAxis = axisName;
            Debug.Log($"[InputManager] Set acceleration axis to {axisName}");
        }
    }

    /// <summary>
    /// Set the axis name for the left trigger (brake)
    /// </summary>
    public void SetBrakeAxis(string axisName)
    {
        if (!string.IsNullOrEmpty(axisName))
        {
            controllerInput.brakeAxis = axisName;
            controllerInput.lastActiveLeftTriggerAxis = axisName;
            Debug.Log($"[InputManager] Set brake axis to {axisName}");
        }
    }

    // Directly apply inputs to the vehicle controller
    public void ApplyInputsToVehicle()
    {
        if (vehicleController == null) return;

        try
        {
            vehicleController.steerInput = SteerInput;
            vehicleController.accelerationInput = AccelerationInput;
            vehicleController.handbrakeInput = HandbrakeInput;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[InputManager] Error applying inputs to vehicle controller: {e.Message}");
        }
    }

    private void Awake()
    {
        // Get the vehicle controller if not set
        if (vehicleController == null)
        {
            vehicleController = GetComponent<VehicleController>();
            if (vehicleController == null)
            {
                vehicleController = GetComponentInParent<VehicleController>();
            }

            if (vehicleController == null)
            {
                Debug.LogError("[InputManager] No VehicleController found! Input will not be applied.");
            }
        }

        // Force debug log on initialization
        Debug.Log($"[InputManager] Initialized on {gameObject.name}: ML-Agent={isMLAgentControlled}, Override={overrideInputs}, VehicleController={vehicleController != null}");

        // Log initialization
        if (logInputs)
        {
            Debug.Log($"InputManager initialized: ML-Agent={isMLAgentControlled}, Override={overrideInputs}");
        }

        // Check for controller at startup
        bool controllerConnected = IsControllerConnected();
        if (controllerConnected)
        {
            Debug.Log("[InputManager] Controller detected. Controller input enabled.");
        }
        else
        {
            Debug.Log("[InputManager] No controller detected. Using keyboard input.");
        }
    }

    // No cleanup needed for controller input

    private float GetKeyboardAccelerationInput()
    {
        float accelInput = 0f;
        if (Input.GetKey(keyboardInput.accelerate))
            accelInput += 1f;
        if (Input.GetKey(keyboardInput.decelerate))
            accelInput -= 1f;
        return accelInput;
    }

    private float GetKeyboardHandbrakeInput()
    {
        return Input.GetKey(keyboardInput.handBrake) ? 1f : 0f;
    }

    private float GetKeyboardSteerInput()
    {
        float steerInput = 0f;
        if (Input.GetKey(keyboardInput.steerLeft))
            steerInput -= 1f;
        if (Input.GetKey(keyboardInput.steerRight))
            steerInput += 1f;
        return steerInput;
    }

    private float GetControllerSteerInput()
    {
        // Added support for Unity InputSystem Gamepad (Xbox One)
        if (enableNewInputSystem && Gamepad.current != null)
        {
            float rawSteer = Gamepad.current.leftStick.x.ReadValue();
            if (Mathf.Abs(rawSteer) < controllerDeadzone)
                return 0f;
            float processedSteer = Mathf.Sign(rawSteer) * ((Mathf.Abs(rawSteer) - controllerDeadzone) / (1 - controllerDeadzone));
            processedSteer *= controllerSteeringSensitivity;
            if (invertSteeringAxis)
                processedSteer = -processedSteer;
            return Mathf.Clamp(processedSteer, -1f, 1f);
        }
        else // Fallback to old input system
    {
        // Get raw steering input from controller (left stick)
        float rawSteer = Input.GetAxis(controllerInput.steeringAxis);

        // Apply deadzone
        if (Mathf.Abs(rawSteer) < controllerDeadzone)
            return 0f;

        // Apply sensitivity and normalize
        float processedSteer = Mathf.Sign(rawSteer) * ((Mathf.Abs(rawSteer) - controllerDeadzone) / (1 - controllerDeadzone));
        processedSteer *= controllerSteeringSensitivity;

        // Apply inversion if needed
        if (invertSteeringAxis)
            processedSteer = -processedSteer;

        // Clamp to valid range
        return Mathf.Clamp(processedSteer, -1f, 1f);
        }
    }

    private float GetControllerAccelerationInput()
    {
        // Added support for Unity InputSystem Gamepad (Xbox One)
        if (enableNewInputSystem && Gamepad.current != null)
        {
            float throttle = Gamepad.current.rightTrigger.ReadValue();
            float brake = Gamepad.current.leftTrigger.ReadValue();
            // Apply trigger deadzone correctly for new system
            float t = Mathf.Max(0f, (throttle - triggerDeadzone) / (1f - triggerDeadzone));
            float b = Mathf.Max(0f, (brake - triggerDeadzone) / (1f - triggerDeadzone));
            float finalInput = b > 0.1f ? -b : t; // Prioritize brake
            finalInput *= controllerAccelerationSensitivity;
            return Mathf.Clamp(finalInput, -1f, 1f);
        }
        else // Fallback to old input system
    {
        // Get acceleration from right trigger (RT)
        float accelerationValue = 0f;
        bool foundAccelAxis = false;

        // Special case for Xbox Wireless Controller
        string[] joystickNames = Input.GetJoystickNames();
        bool isXboxController = false;
        for (int i = 0; i < joystickNames.Length; i++)
        {
            if (!string.IsNullOrEmpty(joystickNames[i]) && joystickNames[i].ToLower().Contains("xbox"))
            {
                isXboxController = true;
                break;
            }
        }

        // Try our special Xbox trigger handling first - this is the preferred method
        float rightTrigger, leftTrigger;
        if (TryGetXboxTriggerValues(out rightTrigger, out leftTrigger))
        {
            accelerationValue = rightTrigger;
            if (accelerationValue > 0.01f)
            {
                foundAccelAxis = true;
                Debug.Log($"[Controller] Using Xbox right trigger value: {accelerationValue:F2}");
                return accelerationValue; // Return immediately if we found a working trigger
            }
        }

        // SPECIAL CASE: Check for direct button input for acceleration
        // For Xbox controllers, check if A button or Y button is pressed for acceleration
        if (Input.GetKey(KeyCode.JoystickButton0) || Input.GetKey(KeyCode.JoystickButton3)) // A button or Y button
        {
            Debug.Log("[Controller] Using A or Y button for acceleration");
            return 1.0f;
        }

        // Xbox-specific handling
        if (isXboxController)
        {
            // If trigger detection didn't work, try common Xbox controller mappings
            if (!foundAccelAxis)
            {
                string[] xboxTriggerAxes = new string[] {
                    "XboxRightTriggerAxis", "Joy0Axis9", "Joy0Axis3", "joystick 1 axis 9", "joystick 1 axis 5",
                    "5th axis", "9th axis", "3rd axis", "Axis 5", "Axis 9", "Axis 3"
                };

                foreach (string axisName in xboxTriggerAxes)
                {
                    try
                    {
                        float value = Input.GetAxis(axisName);
                        if (Mathf.Abs(value) > 0.01f)
                        {
                            accelerationValue = value;
                            foundAccelAxis = true;
                            Debug.Log($"[Controller] Found Xbox acceleration axis: {axisName} with value {value:F2}");
                            controllerInput.accelerationAxis = axisName;
                            break;
                        }
                    }
                    catch (System.Exception) { }
                }
            }

            // If still not found, try direct button mapping for Xbox controllers
            if (!foundAccelAxis)
            {
                // Check if right trigger button is pressed (sometimes mapped as a button)
                if (Input.GetButton("Fire1") || Input.GetButton("Fire2") ||
                    Input.GetButton("Fire3") || Input.GetButton("Submit"))
                {
                    accelerationValue = 1.0f;
                    foundAccelAxis = true;
                    Debug.Log("[Controller] Using Xbox button mapping for acceleration");
                }
            }
        }

        // If Xbox-specific approach didn't work, try the standard approach
        if (!foundAccelAxis)
        {
            // Try the primary acceleration axis first
            try {
                // Get the right trigger value
                accelerationValue = Input.GetAxis(controllerInput.accelerationAxis);

                // If we got a non-zero value or no error, consider it found
                if (Mathf.Abs(accelerationValue) > 0.01f) {
                    foundAccelAxis = true;
                    Debug.Log($"[Controller] Using primary acceleration axis: {controllerInput.accelerationAxis}");
                }
            }
            catch (System.Exception) {
                // Axis doesn't exist, will try alternatives
            }

            // If primary axis didn't work, try alternatives
            if (!foundAccelAxis) {
                foreach (string axisName in controllerInput.alternativeAccelerationAxes) {
                    try {
                        float value = Input.GetAxis(axisName);
                        if (Mathf.Abs(value) > 0.01f) {
                            accelerationValue = value;
                            foundAccelAxis = true;
                            Debug.Log($"[Controller] Found working acceleration axis: {axisName} with value {value:F2}");

                            // Update the primary axis name for future use
                            controllerInput.accelerationAxis = axisName;
                            break;
                        }
                    }
                    catch (System.Exception) {
                        // Try next axis
                    }
                }
            }

            // Try special case for Xbox controllers where both triggers use the same axis
            if (!foundAccelAxis) {
                try {
                    // On some controllers, both triggers use the same axis but in opposite directions
                    float triggerValue = Input.GetAxis("3rd axis");
                    if (triggerValue > 0.01f) { // Right trigger is positive
                        accelerationValue = triggerValue;
                        foundAccelAxis = true;
                        Debug.Log($"[Controller] Using combined trigger axis (positive) for acceleration: {triggerValue:F2}");
                        controllerInput.accelerationAxis = "3rd axis";
                    }
                }
                catch (System.Exception) {
                    // Axis doesn't exist
                }
            }
        }

        // Normalize to 0 to 1 range (RT is usually 0 to 1 already on most platforms)
        if (accelerationValue < 0 && !controllerInput.accelerationAxis.Contains("inverted")) // If using -1 to 1 range
            accelerationValue = (accelerationValue + 1f) / 2f;

        // Apply deadzone
        if (accelerationValue < controllerDeadzone)
            accelerationValue = 0f;
        else
            accelerationValue = (accelerationValue - controllerDeadzone) / (1f - controllerDeadzone);

        // Get braking/reverse from left trigger (LT)
        float brakeValue = 0f;
        bool foundBrakeAxis = false;

        // Try our special Xbox trigger handling first - this is the preferred method
        float rt2, lt2;
        if (TryGetXboxTriggerValues(out rt2, out lt2))
        {
            brakeValue = lt2;
            if (brakeValue > 0.01f)
            {
                foundBrakeAxis = true;
                Debug.Log($"[Controller] Using Xbox left trigger value: {brakeValue:F2}");
                // Convert to negative for reverse when used as acceleration input
                return -brakeValue;
            }
        }

        // SPECIAL CASE: Check for direct button input for braking/reverse
        // For Xbox controllers, check if B button is pressed for braking/reverse
        if (Input.GetKey(KeyCode.JoystickButton1)) // B button
        {
            Debug.Log("[Controller] Using B button for braking/reverse");
            return -1.0f; // Negative value for reverse
        }

        // Xbox-specific handling for brake
        if (isXboxController)
        {

            // If that didn't work, try common Xbox controller mappings
            if (!foundBrakeAxis)
            {
                string[] xboxBrakeAxes = new string[] {
                    "XboxLeftTriggerAxis", "Joy0Axis8", "Joy0Axis2", "joystick 1 axis 8", "joystick 1 axis 4",
                    "4th axis", "8th axis", "2nd axis", "Axis 4", "Axis 8", "Axis 2"
                };

                foreach (string axisName in xboxBrakeAxes)
                {
                    try
                    {
                        float value = Input.GetAxis(axisName);
                        if (Mathf.Abs(value) > 0.01f)
                        {
                            brakeValue = value;
                            foundBrakeAxis = true;
                            Debug.Log($"[Controller] Found Xbox brake axis: {axisName} with value {value:F2}");
                            controllerInput.brakeAxis = axisName;
                            break;
                        }
                    }
                    catch (System.Exception) { }
                }
            }

            // If still not found, try direct button mapping for Xbox controllers
            if (!foundBrakeAxis)
            {
                // Check if left trigger button is pressed (sometimes mapped as a button)
                if (Input.GetButton("Cancel") || Input.GetButton("Fire2") ||
                    Input.GetButton("Fire3") || Input.GetButton("Submit"))
                {
                    brakeValue = 1.0f;
                    foundBrakeAxis = true;
                    Debug.Log("[Controller] Using Xbox button mapping for braking");
                }
            }
        }

        // If Xbox-specific approach didn't work, try the standard approach
        if (!foundBrakeAxis)
        {
            // Try the primary brake axis first
            try {
                // Get the left trigger value
                brakeValue = Input.GetAxis(controllerInput.brakeAxis);

                // If we got a non-zero value or no error, consider it found
                if (Mathf.Abs(brakeValue) > 0.01f) {
                    foundBrakeAxis = true;
                    Debug.Log($"[Controller] Using primary brake axis: {controllerInput.brakeAxis}");
                }
            }
            catch (System.Exception) {
                // Axis doesn't exist, will try alternatives
            }

            // If primary axis didn't work, try alternatives
            if (!foundBrakeAxis) {
                foreach (string axisName in controllerInput.alternativeBrakeAxes) {
                    try {
                        float value = Input.GetAxis(axisName);
                        if (Mathf.Abs(value) > 0.01f) {
                            brakeValue = value;
                            foundBrakeAxis = true;
                            Debug.Log($"[Controller] Found working brake axis: {axisName} with value {value:F2}");

                            // Update the primary axis name for future use
                            controllerInput.brakeAxis = axisName;
                            break;
                        }
                    }
                    catch (System.Exception) {
                        // Try next axis
                    }
                }
            }

            // Try special case for Xbox controllers where both triggers use the same axis
            if (!foundBrakeAxis) {
                try {
                    // On some controllers, both triggers use the same axis but in opposite directions
                    float triggerValue = Input.GetAxis("3rd axis");
                    if (triggerValue < -0.01f) { // Left trigger is negative
                        brakeValue = -triggerValue; // Convert to positive
                        foundBrakeAxis = true;
                        Debug.Log($"[Controller] Using combined trigger axis (negative) for braking: {triggerValue:F2}");
                        controllerInput.brakeAxis = "3rd axis (inverted)";
                    }
                }
                catch (System.Exception) {
                    // Axis doesn't exist
                }
            }
        }

        // Normalize to 0 to 1 range (LT is usually 0 to 1 already on most platforms)
        if (brakeValue < 0 && !controllerInput.brakeAxis.Contains("inverted")) // If using -1 to 1 range
            brakeValue = (brakeValue + 1f) / 2f;

        // Special case for inverted axes
        if (controllerInput.brakeAxis.Contains("inverted"))
            brakeValue = Mathf.Abs(brakeValue);

        // Apply deadzone
        if (brakeValue < controllerDeadzone)
            brakeValue = 0f;
        else
            brakeValue = (brakeValue - controllerDeadzone) / (1f - controllerDeadzone);

        // Forza-style controls: RT accelerates, LT brakes/reverses
        // If both are pressed, prioritize braking
        float finalInput = 0f;

        if (brakeValue > 0.1f) {
            // Braking/reversing takes priority
            finalInput = -brakeValue;
        } else {
            // Otherwise use acceleration
            finalInput = accelerationValue;
        }

        // Apply sensitivity
        finalInput *= controllerAccelerationSensitivity;

        // Clamp to valid range
        return Mathf.Clamp(finalInput, -1f, 1f);
        }
    }

    private float GetControllerHandbrakeInput()
    {
        // Added support for Unity InputSystem Gamepad (Xbox One)
        if (enableNewInputSystem && Gamepad.current != null)
        {
            // Using right shoulder as handbrake for new input system
            return Gamepad.current.rightShoulder.isPressed ? 1f : 0f;
        }
        else // Fallback to old input system
    {
        // Check if we're on macOS
        bool isMacOS = SystemInfo.operatingSystem.Contains("Mac");

        if (isMacOS)
        {
            // On macOS, use a different approach for Xbox controllers
            return GetMacXboxHandbrakeInput();
        }
        else
        {
            // On other platforms, use the standard approach
            return GetStandardHandbrakeInput();
            }
        }
    }

    // Special implementation for Xbox controller handbrake on macOS
    private float GetMacXboxHandbrakeInput()
    {
        // On macOS, we'll use the X button (button 2) for handbrake
        if (Input.GetKey(KeyCode.JoystickButton2)) // X button on Xbox controller
        {
            Debug.Log("[Xbox macOS] Using X button for handbrake");
            return 1f;
        }

        // Fallback to other buttons if X isn't working
        if (Input.GetKey(KeyCode.JoystickButton3)) // Y button
        {
            Debug.Log("[Xbox macOS] Using Y button for handbrake (fallback)");
            return 1f;
        }

        if (Input.GetKey(KeyCode.JoystickButton4) || Input.GetKey(KeyCode.JoystickButton5)) // LB or RB
        {
            Debug.Log("[Xbox macOS] Using bumper button for handbrake (fallback)");
            return 1f;
        }

        return 0f;
    }

    // Standard implementation for handbrake on other platforms
    private float GetStandardHandbrakeInput()
    {
        // Direct check for X button (most reliable method)
        if (Input.GetKey(KeyCode.JoystickButton2)) // X button on Xbox controller
        {
            Debug.Log("[Xbox Controller] Using X button for handbrake");
            return 1f;
        }

        // Try Fire3 which is often mapped to X button on Xbox controllers
        if (Input.GetButton("Fire3"))
        {
            Debug.Log("[Xbox Controller] Using Fire3 (X button) for handbrake");
            return 1f;
        }

        // Try the primary handbrake button
        if (Input.GetButton(controllerInput.handbrakeButton))
        {
            Debug.Log($"[Xbox Controller] Using primary handbrake button: {controllerInput.handbrakeButton}");
            return 1f;
        }

        // Fallback to other common buttons if X isn't working
        if (Input.GetButton("Jump") || Input.GetButton("Submit") || Input.GetKey(KeyCode.JoystickButton0))
        {
            Debug.Log("[Xbox Controller] Using A button for handbrake (fallback)");
            return 1f;
        }

        // Try shoulder buttons as another fallback
        if (Input.GetKey(KeyCode.JoystickButton4) || Input.GetKey(KeyCode.JoystickButton5)) // LB or RB
        {
            Debug.Log("[Xbox Controller] Using shoulder button for handbrake (fallback)");
            return 1f;
        }

        return 0f;
    }

    private bool IsControllerConnected()
    {
        try {
            string[] joystickNames = Input.GetJoystickNames();
            return joystickNames.Length > 0 && !string.IsNullOrEmpty(joystickNames[0]);
        }
        catch (System.Exception) {
            return false;
        }
    }

    // Track previous trigger states to detect when they're released
    private float previousLeftTrigger = 0f;
    private float previousRightTrigger = 0f;
    private float leftTriggerStuckTime = 0f;
    private float rightTriggerStuckTime = 0f;
    private const float TRIGGER_STUCK_TIMEOUT = 0.5f; // Time in seconds before considering a trigger stuck

    // Direct implementation of Forza-style controls for Xbox controller
    private float GetForzaStyleInput()
    {
        // This method implements Forza-style controls:
        // - Right trigger for acceleration
        // - Left trigger for braking/reverse
        // - If both are pressed, brake takes priority

        float rightTrigger = 0f;
        float leftTrigger = 0f;
        bool foundRT = false;
        bool foundLT = false;



        // TRIGGER-BASED IMPLEMENTATION FOR XBOX CONTROLLER
        // First try the RT and LT axes that should be set up by InputAxisSetup
        try {
            float rtValue = Input.GetAxis("RT");
            // Check if RT is negative (which is unusual but happens with some controllers)
            if (rtValue < -0.05f) {
                // FIXED: Use negative RT for LEFT trigger (braking)
                leftTrigger = -rtValue; // Convert to positive
                foundLT = true;
                Debug.Log($"[Controller] Using RT axis (negative) for LT: {leftTrigger:F2}");
            } else if (rtValue > 0.05f) {
                // FIXED: Use positive RT for RIGHT trigger (acceleration)
                rightTrigger = rtValue;
                foundRT = true;
                Debug.Log($"[Controller] Using RT axis for RT: {rightTrigger:F2}");
            }
        } catch {}

        try {
            float ltValue = Input.GetAxis("LT");
            // Check if LT is negative (which is unusual but happens with some controllers)
            if (ltValue < -0.05f) {
                // FIXED: Use negative LT for RIGHT trigger (acceleration)
                rightTrigger = -ltValue; // Convert to positive
                foundRT = true;
                Debug.Log($"[Controller] Using LT axis (negative) for RT: {rightTrigger:F2}");
            } else if (ltValue > 0.05f) {
                // FIXED: Use positive LT for LEFT trigger (braking)
                leftTrigger = ltValue;
                foundLT = true;
                Debug.Log($"[Controller] Using LT axis for LT: {leftTrigger:F2}");
            }
        } catch {}

        // If that didn't work, try alternative axes
        if (!foundRT) {
            try {
                float rtAltValue = Input.GetAxis("RT_Alt");
                // Check if RT_Alt is negative (which is unusual but happens with some controllers)
                if (rtAltValue < -0.05f) {
                    // FIXED: Use negative RT_Alt for LEFT trigger (braking)
                    if (!foundLT) {
                        leftTrigger = -rtAltValue; // Convert to positive
                        foundLT = true;
                        Debug.Log($"[Controller] Using RT_Alt axis (negative) for LT: {leftTrigger:F2}");
                    }
                } else if (rtAltValue > 0.05f) {
                    // FIXED: Use positive RT_Alt for RIGHT trigger (acceleration)
                    if (!foundRT) {
                        rightTrigger = rtAltValue;
                        foundRT = true;
                        Debug.Log($"[Controller] Using RT_Alt axis for RT: {rightTrigger:F2}");
                    }
                }
            } catch {}
        }

        if (!foundLT) {
            try {
                float ltAltValue = Input.GetAxis("LT_Alt");
                // Check if LT_Alt is negative (which is unusual but happens with some controllers)
                if (ltAltValue < -0.05f) {
                    // FIXED: Use negative LT_Alt for RIGHT trigger (acceleration)
                    if (!foundRT) {
                        rightTrigger = -ltAltValue; // Convert to positive
                        foundRT = true;
                        Debug.Log($"[Controller] Using LT_Alt axis (negative) for RT: {rightTrigger:F2}");
                    }
                } else if (ltAltValue > 0.05f) {
                    // FIXED: Use positive LT_Alt for LEFT trigger (braking)
                    if (!foundLT) {
                        leftTrigger = ltAltValue;
                        foundLT = true;
                        Debug.Log($"[Controller] Using LT_Alt axis for LT: {leftTrigger:F2}");
                    }
                }
            } catch {}
        }

        // Try direct joystick axes
        if (!foundRT || !foundLT) {
            // Check all axes from 0 to 10 to find the triggers
            for (int axis = 0; axis <= 10; axis++) {
                try {
                    float value = Input.GetAxis($"joystick 1 axis {axis}");

                    // FIXED: If the value is negative, it might be a left trigger (braking)
                    if (value < -0.05f && !foundLT) {
                        leftTrigger = -value; // Convert to positive
                        foundLT = true;
                        Debug.Log($"[Controller] Using direct axis {axis} (negative) for LT: {leftTrigger:F2}");
                    }
                    // FIXED: If the value is positive, it might be a right trigger (acceleration)
                    else if (value > 0.05f && !foundRT) {
                        rightTrigger = value;
                        foundRT = true;
                        Debug.Log($"[Controller] Using direct axis {axis} for RT: {rightTrigger:F2}");
                    }

                    // If we found both triggers, we can stop searching
                    if (foundRT && foundLT) {
                        break;
                    }
                } catch {}
            }
        }

        // Try specific axes known to work with Xbox controllers
        if (!foundRT) {
            int[] rtAxes = new int[] { 5, 9, 3, 4 };
            foreach (int axis in rtAxes) {
                try {
                    float value = Input.GetAxis($"joystick 1 axis {axis}");
                    // FIXED: Use positive value for right trigger (acceleration)
                    if (value > 0.05f && !foundRT) {
                        rightTrigger = value;
                        foundRT = true;
                        Debug.Log($"[Controller] Using direct RT axis {axis} for RT: {rightTrigger:F2}");
                    }
                    // FIXED: Use negative value for left trigger (braking)
                    else if (value < -0.05f && !foundLT) {
                        leftTrigger = -value; // Convert to positive
                        foundLT = true;
                        Debug.Log($"[Controller] Using direct RT axis {axis} (negative) for LT: {leftTrigger:F2}");
                    }
                } catch {}
            }
        }

        if (!foundLT) {
            int[] ltAxes = new int[] { 2, 8, 3, 4 };
            foreach (int axis in ltAxes) {
                try {
                    float value = Input.GetAxis($"joystick 1 axis {axis}");
                    // FIXED: Use positive value for left trigger (braking)
                    if (value > 0.05f && !foundLT) {
                        leftTrigger = value;
                        foundLT = true;
                        Debug.Log($"[Controller] Using direct LT axis {axis} for LT: {leftTrigger:F2}");
                    }
                    // FIXED: Use negative value for right trigger (acceleration)
                    else if (value < -0.05f && !foundRT) {
                        rightTrigger = -value; // Convert to positive
                        foundRT = true;
                        Debug.Log($"[Controller] Using direct LT axis {axis} (negative) for RT: {rightTrigger:F2}");
                    }
                } catch {}
            }
        }

        // If we still haven't found triggers, check for button input as fallback
        if (!foundRT && Input.GetKey(KeyCode.JoystickButton0)) { // A button
            rightTrigger = 1.0f;
            foundRT = true;
            Debug.Log("[Controller] Using A button for acceleration");
        }

        if (!foundLT && Input.GetKey(KeyCode.JoystickButton1)) { // B button
            leftTrigger = 1.0f;
            foundLT = true;
            Debug.Log("[Controller] Using B button for braking");
        }

        // Check for stuck triggers
        // If a trigger has been at the same value for too long, it might be stuck
        if (leftTrigger > 0.9f && Mathf.Approximately(leftTrigger, previousLeftTrigger)) {
            leftTriggerStuckTime += Time.deltaTime;
            if (leftTriggerStuckTime > TRIGGER_STUCK_TIMEOUT) {
                // Check for button input to confirm the user is actually pressing the trigger
                bool ltButtonPressed = Input.GetKey(KeyCode.JoystickButton6) || // LT is sometimes mapped to button 6
                                      Input.GetKey(KeyCode.JoystickButton8) || // Or button 8
                                      Input.GetKey(KeyCode.JoystickButton4) || // Or LB (button 4)
                                      Input.GetKey(KeyCode.JoystickButton1);   // Or B button

                // If no button is pressed, the trigger is probably stuck
                if (!ltButtonPressed) {
                    Debug.Log("[Controller] LT appears to be stuck, resetting value");
                    leftTrigger = 0f;
                    foundLT = false;
                }
            }
        } else {
            // Reset the stuck timer if the value changes
            leftTriggerStuckTime = 0f;
        }

        // Same for right trigger
        if (rightTrigger > 0.9f && Mathf.Approximately(rightTrigger, previousRightTrigger)) {
            rightTriggerStuckTime += Time.deltaTime;
            if (rightTriggerStuckTime > TRIGGER_STUCK_TIMEOUT) {
                // Check for button input to confirm the user is actually pressing the trigger
                bool rtButtonPressed = Input.GetKey(KeyCode.JoystickButton7) || // RT is sometimes mapped to button 7
                                      Input.GetKey(KeyCode.JoystickButton9) || // Or button 9
                                      Input.GetKey(KeyCode.JoystickButton5) || // Or RB (button 5)
                                      Input.GetKey(KeyCode.JoystickButton0);   // Or A button

                // If no button is pressed, the trigger is probably stuck
                if (!rtButtonPressed) {
                    Debug.Log("[Controller] RT appears to be stuck, resetting value");
                    rightTrigger = 0f;
                    foundRT = false;
                }
            }
        } else {
            // Reset the stuck timer if the value changes
            rightTriggerStuckTime = 0f;
        }



        // IMPORTANT: Check for phantom left trigger activation when right trigger is released
        // This is a common issue with some controllers where releasing RT causes LT to activate
        if (foundRT && rightTrigger > 0.1f && foundLT && leftTrigger > 0.1f)
        {
            // If both triggers are active but RT was active in the previous frame and LT wasn't,
            // this might be a phantom activation of LT when releasing RT
            if (previousRightTrigger > 0.1f && previousLeftTrigger < 0.1f)
            {
                Debug.Log("[Controller Debug] Detected possible phantom LT activation when releasing RT. Ignoring LT input.");
                leftTrigger = 0f;
                foundLT = false;
            }
        }

        // Store current values for next frame (raw values before processing)
        previousLeftTrigger = leftTrigger;
        previousRightTrigger = rightTrigger;

        // Apply deadzone with a larger threshold to prevent auto-movement
        // Use the dedicated trigger deadzone value
        float originalRightTrigger = rightTrigger;
        float originalLeftTrigger = leftTrigger;

        if (Mathf.Abs(rightTrigger) < triggerDeadzone)
            rightTrigger = 0f;
        else
            rightTrigger = Mathf.Sign(rightTrigger) * (Mathf.Abs(rightTrigger) - triggerDeadzone) / (1f - triggerDeadzone);

        if (Mathf.Abs(leftTrigger) < triggerDeadzone)
            leftTrigger = 0f;
        else
            leftTrigger = Mathf.Sign(leftTrigger) * (Mathf.Abs(leftTrigger) - triggerDeadzone) / (1f - triggerDeadzone);

        // Apply sensitivity
        rightTrigger *= controllerAccelerationSensitivity;
        leftTrigger *= controllerAccelerationSensitivity;

        // Debug log processed trigger values
        if (Time.frameCount % 60 == 0) // Log every second at 60fps
        {
            Debug.Log($"[Controller Debug] Processed trigger values: RT={rightTrigger:F2} (from {originalRightTrigger:F2}), LT={leftTrigger:F2} (from {originalLeftTrigger:F2})");
        }

        // Forza-style: If brake is pressed, it takes priority
        float result = 0f;

        // IMPORTANT: Special handling for the case where RT was just released
        // This prevents the car from automatically braking when RT is released
        bool rtJustReleased = previousRightTrigger > 0.1f && rightTrigger <= 0.1f;

        if (rtJustReleased) {
            // If RT was just released, ignore LT input for a short time to prevent auto-braking
            result = 0f;
            Debug.Log("[Controller Debug] RT just released - ignoring LT input to prevent auto-braking");
        }
        else if (leftTrigger > 0.1f) {
            // Return negative value for braking/reverse
            result = -leftTrigger;
            if (Time.frameCount % 60 == 0) // Log every second at 60fps
            {
                Debug.Log($"[Controller Debug] Using left trigger for braking: {result:F2}");
            }
        } else if (rightTrigger > 0.1f) {
            // Return positive value for acceleration
            result = rightTrigger;
            if (Time.frameCount % 60 == 0) // Log every second at 60fps
            {
                Debug.Log($"[Controller Debug] Using right trigger for acceleration: {result:F2}");
            }
        } else {
            // If both inputs are very small, return exactly zero to prevent auto-movement
            result = 0f;
            if (Time.frameCount % 60 == 0) // Log every second at 60fps
            {
                Debug.Log("[Controller Debug] Both triggers below threshold, returning zero");
            }
        }

        return result;
    }

    // Special implementation for Xbox controllers on macOS
    private float GetMacXboxControllerInput()
    {
        // macOS has different axis mappings for Xbox controllers
        float rightTrigger = 0f;
        float leftTrigger = 0f;
        bool foundRT = false;
        bool foundLT = false;

        // On macOS, the Xbox controller triggers are often mapped to these axes:
        // - Right Trigger (RT): axis 5 (ranges from -1 to 1, with 0 being unpressed)
        // - Left Trigger (LT): axis 4 (ranges from -1 to 1, with 0 being unpressed)
        try {
            rightTrigger = (Input.GetAxis("joystick 1 axis 5") + 1f) / 2f; // Convert from -1,1 to 0,1
            if (Mathf.Abs(rightTrigger - 0.5f) > 0.01f) { // If not centered at 0.5
                foundRT = true;
                Debug.Log($"[Controller] macOS: Using RT on axis 5: {rightTrigger:F2}");
            }
        } catch {}

        try {
            leftTrigger = (Input.GetAxis("joystick 1 axis 4") + 1f) / 2f; // Convert from -1,1 to 0,1
            if (Mathf.Abs(leftTrigger - 0.5f) > 0.01f) { // If not centered at 0.5
                foundLT = true;
                Debug.Log($"[Controller] macOS: Using LT on axis 4: {leftTrigger:F2}");
            }
        } catch {}

        // Try alternative axes if the standard ones didn't work
        if (!foundRT) {
            try {
                rightTrigger = (Input.GetAxis("joystick 1 axis 3") + 1f) / 2f;
                if (Mathf.Abs(rightTrigger - 0.5f) > 0.01f) {
                    foundRT = true;
                    Debug.Log($"[Controller] macOS: Using RT on axis 3: {rightTrigger:F2}");
                }
            } catch {}
        }

        if (!foundLT) {
            try {
                leftTrigger = (Input.GetAxis("joystick 1 axis 2") + 1f) / 2f;
                if (Mathf.Abs(leftTrigger - 0.5f) > 0.01f) {
                    foundLT = true;
                    Debug.Log($"[Controller] macOS: Using LT on axis 2: {leftTrigger:F2}");
                }
            } catch {}
        }

        // If we still haven't found triggers, check for button input as fallback
        if (!foundRT && Input.GetKey(KeyCode.JoystickButton0)) { // A button
            rightTrigger = 1.0f;
            foundRT = true;
            Debug.Log("[Controller] macOS: Using A button for acceleration");
        }

        if (!foundLT && Input.GetKey(KeyCode.JoystickButton1)) { // B button
            leftTrigger = 1.0f;
            foundLT = true;
            Debug.Log("[Controller] macOS: Using B button for braking");
        }

        // Check for stuck triggers
        // If a trigger has been at the same value for too long, it might be stuck
        if (leftTrigger > 0.9f && Mathf.Approximately(leftTrigger, previousLeftTrigger)) {
            leftTriggerStuckTime += Time.deltaTime;
            if (leftTriggerStuckTime > TRIGGER_STUCK_TIMEOUT) {
                // Check for button input to confirm the user is actually pressing the trigger
                bool ltButtonPressed = Input.GetKey(KeyCode.JoystickButton6) || // LT is sometimes mapped to button 6
                                      Input.GetKey(KeyCode.JoystickButton8) || // Or button 8
                                      Input.GetKey(KeyCode.JoystickButton4) || // Or LB (button 4)
                                      Input.GetKey(KeyCode.JoystickButton1);   // Or B button

                // If no button is pressed, the trigger is probably stuck
                if (!ltButtonPressed) {
                    Debug.Log("[Controller] macOS: LT appears to be stuck, resetting value");
                    leftTrigger = 0f;
                    foundLT = false;
                }
            }
        } else {
            // Reset the stuck timer if the value changes
            leftTriggerStuckTime = 0f;
        }

        // Same for right trigger
        if (rightTrigger > 0.9f && Mathf.Approximately(rightTrigger, previousRightTrigger)) {
            rightTriggerStuckTime += Time.deltaTime;
            if (rightTriggerStuckTime > TRIGGER_STUCK_TIMEOUT) {
                // Check for button input to confirm the user is actually pressing the trigger
                bool rtButtonPressed = Input.GetKey(KeyCode.JoystickButton7) || // RT is sometimes mapped to button 7
                                      Input.GetKey(KeyCode.JoystickButton9) || // Or button 9
                                      Input.GetKey(KeyCode.JoystickButton5) || // Or RB (button 5)
                                      Input.GetKey(KeyCode.JoystickButton0);   // Or A button

                // If no button is pressed, the trigger is probably stuck
                if (!rtButtonPressed) {
                    Debug.Log("[Controller] macOS: RT appears to be stuck, resetting value");
                    rightTrigger = 0f;
                    foundRT = false;
                }
            }
        } else {
            // Reset the stuck timer if the value changes
            rightTriggerStuckTime = 0f;
        }

        // Debug log raw trigger values before processing
        if (Time.frameCount % 60 == 0) // Log every second at 60fps
        {
            Debug.Log($"[Controller Debug] macOS: Raw trigger values before processing: RT={rightTrigger:F2}, LT={leftTrigger:F2}, foundRT={foundRT}, foundLT={foundLT}");
        }

        // IMPORTANT: Check for phantom left trigger activation when right trigger is released
        // This is a common issue with some controllers where releasing RT causes LT to activate
        if (foundRT && rightTrigger > 0.1f && foundLT && leftTrigger > 0.1f)
        {
            // If both triggers are active but RT was active in the previous frame and LT wasn't,
            // this might be a phantom activation of LT when releasing RT
            if (previousRightTrigger > 0.1f && previousLeftTrigger < 0.1f)
            {
                Debug.Log("[Controller Debug] macOS: Detected possible phantom LT activation when releasing RT. Ignoring LT input.");
                leftTrigger = 0f;
                foundLT = false;
            }
        }

        // Store current values for next frame (raw values before processing)
        previousLeftTrigger = leftTrigger;
        previousRightTrigger = rightTrigger;

        // Apply deadzone with a larger threshold to prevent auto-movement
        // Use the dedicated trigger deadzone value
        float originalRightTrigger = rightTrigger;
        float originalLeftTrigger = leftTrigger;

        if (Mathf.Abs(rightTrigger) < triggerDeadzone)
            rightTrigger = 0f;
        else
            rightTrigger = Mathf.Sign(rightTrigger) * (Mathf.Abs(rightTrigger) - triggerDeadzone) / (1f - triggerDeadzone);

        if (Mathf.Abs(leftTrigger) < triggerDeadzone)
            leftTrigger = 0f;
        else
            leftTrigger = Mathf.Sign(leftTrigger) * (Mathf.Abs(leftTrigger) - triggerDeadzone) / (1f - triggerDeadzone);

        // Apply sensitivity
        rightTrigger *= controllerAccelerationSensitivity;
        leftTrigger *= controllerAccelerationSensitivity;

        // Debug log processed trigger values
        if (Time.frameCount % 60 == 0) // Log every second at 60fps
        {
            Debug.Log($"[Controller Debug] macOS: Processed trigger values: RT={rightTrigger:F2} (from {originalRightTrigger:F2}), LT={leftTrigger:F2} (from {originalLeftTrigger:F2})");
        }

        // Forza-style: If brake is pressed, it takes priority
        float result = 0f;

        // IMPORTANT: Special handling for the case where RT was just released
        // This prevents the car from automatically braking when RT is released
        bool rtJustReleased = previousRightTrigger > 0.1f && rightTrigger <= 0.1f;

        if (rtJustReleased) {
            // If RT was just released, ignore LT input for a short time to prevent auto-braking
            result = 0f;
            Debug.Log("[Controller Debug] macOS: RT just released - ignoring LT input to prevent auto-braking");
        }
        else if (leftTrigger > 0.1f) {
            // Return negative value for braking/reverse
            result = -leftTrigger;
            if (Time.frameCount % 60 == 0) // Log every second at 60fps
            {
                Debug.Log($"[Controller Debug] macOS: Using left trigger for braking: {result:F2}");
            }
        } else if (rightTrigger > 0.1f) {
            // Return positive value for acceleration
            result = rightTrigger;
            if (Time.frameCount % 60 == 0) // Log every second at 60fps
            {
                Debug.Log($"[Controller Debug] macOS: Using right trigger for acceleration: {result:F2}");
            }
        } else {
            // If both inputs are very small, return exactly zero to prevent auto-movement
            result = 0f;
            if (Time.frameCount % 60 == 0) // Log every second at 60fps
            {
                Debug.Log("[Controller Debug] macOS: Both triggers below threshold, returning zero");
            }
        }

        return result;

    }

    // Generic implementation for Xbox controllers on other platforms
    private float GetGenericXboxControllerInput()
    {
        float rightTriggerValue = 0f;
        float leftTriggerValue = 0f;
        bool foundTriggers = false;

        // Try the most common Xbox trigger mappings first
        // These are the most common axes for Xbox controllers across different platforms
        int[][] commonXboxTriggerMappings = new int[][] {
            new int[] { 3, 3 },   // Windows combined axis (positive/negative)
            new int[] { 5, 2 },   // Windows common mapping
            new int[] { 9, 8 },   // Windows alternate mapping
            new int[] { 2, 5 },   // Reversed mapping
            new int[] { 4, 5 }    // Another common mapping
        };

        // Try each mapping pair
        foreach (int[] mapping in commonXboxTriggerMappings)
        {
            int rtAxis = mapping[0];
            int ltAxis = mapping[1];

            try
            {
                // Check if this is a combined axis (both triggers on same axis)
                if (rtAxis == ltAxis)
                {
                    string axisName = $"joystick 1 axis {rtAxis}";
                    float value = Input.GetAxis(axisName);

                    if (Mathf.Abs(value) > 0.05f)
                    {
                        if (value > 0)
                        {
                            rightTriggerValue = value;
                            Debug.Log($"[Xbox Controller] Found right trigger on combined axis {rtAxis}: {value:F2}");
                        }
                        else
                        {
                            leftTriggerValue = -value; // Convert negative to positive
                            Debug.Log($"[Xbox Controller] Found left trigger on combined axis {ltAxis}: {-value:F2}");
                        }
                        foundTriggers = true;
                    }
                }
                else
                {
                    // Check separate axes
                    string rtAxisName = $"joystick 1 axis {rtAxis}";
                    string ltAxisName = $"joystick 1 axis {ltAxis}";

                    float rtValue = Input.GetAxis(rtAxisName);
                    float ltValue = Input.GetAxis(ltAxisName);

                    if (Mathf.Abs(rtValue) > 0.05f)
                    {
                        rightTriggerValue = Mathf.Abs(rtValue); // Use absolute value in case it's negative
                        Debug.Log($"[Xbox Controller] Found right trigger on axis {rtAxis}: {rightTriggerValue:F2}");
                        foundTriggers = true;

                        // Save this axis for future use
                        controllerInput.accelerationAxis = rtAxisName;
                    }

                    if (Mathf.Abs(ltValue) > 0.05f)
                    {
                        leftTriggerValue = Mathf.Abs(ltValue); // Use absolute value in case it's negative
                        Debug.Log($"[Xbox Controller] Found left trigger on axis {ltAxis}: {leftTriggerValue:F2}");
                        foundTriggers = true;

                        // Save this axis for future use
                        controllerInput.brakeAxis = ltAxisName;
                    }
                }

                // If we found both triggers, we can stop searching
                if (rightTriggerValue > 0 && leftTriggerValue > 0)
                {
                    break;
                }
            }
            catch (System.Exception) { }
        }

        // If we still haven't found the triggers, try all axes as a last resort
        if (!foundTriggers)
        {
            // Try all possible joystick axes
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    string axisName = $"joystick 1 axis {i}";
                    float value = Input.GetAxis(axisName);

                    // If we found an active axis, log it and use it
                    if (Mathf.Abs(value) > 0.05f)
                    {
                        Debug.Log($"[Xbox Controller] Found active axis {i}: {value:F2}");

                        // For simplicity, use the first active axis as right trigger (acceleration)
                        // and the second as left trigger (brake)
                        if (rightTriggerValue == 0)
                        {
                            rightTriggerValue = Mathf.Abs(value);
                            controllerInput.accelerationAxis = axisName;
                            Debug.Log($"[Xbox Controller] Using axis {i} for right trigger");
                            foundTriggers = true;
                        }
                        else if (leftTriggerValue == 0)
                        {
                            leftTriggerValue = Mathf.Abs(value);
                            controllerInput.brakeAxis = axisName;
                            Debug.Log($"[Xbox Controller] Using axis {i} for left trigger");
                            foundTriggers = true;
                            break; // Once we have both triggers, we can stop
                        }
                    }
                }
                catch (System.Exception) { }
            }
        }

        // Apply deadzone and sensitivity
        if (foundTriggers)
        {
            // Debug log raw trigger values before processing
            if (Time.frameCount % 60 == 0) // Log every second at 60fps
            {
                Debug.Log($"[Controller Debug] Generic: Raw trigger values before processing: RT={rightTriggerValue:F2}, LT={leftTriggerValue:F2}, foundTriggers={foundTriggers}");
            }

            // Store original values for debugging
            float originalRightTrigger = rightTriggerValue;
            float originalLeftTrigger = leftTriggerValue;

            // Apply deadzone with a larger threshold to prevent auto-movement
            // Use the dedicated trigger deadzone value
            if (Mathf.Abs(rightTriggerValue) < triggerDeadzone)
                rightTriggerValue = 0f;
            else
                rightTriggerValue = Mathf.Sign(rightTriggerValue) * (Mathf.Abs(rightTriggerValue) - triggerDeadzone) / (1f - triggerDeadzone);

            if (Mathf.Abs(leftTriggerValue) < triggerDeadzone)
                leftTriggerValue = 0f;
            else
                leftTriggerValue = Mathf.Sign(leftTriggerValue) * (Mathf.Abs(leftTriggerValue) - triggerDeadzone) / (1f - triggerDeadzone);

            // Apply sensitivity
            rightTriggerValue *= controllerAccelerationSensitivity;
            leftTriggerValue *= controllerAccelerationSensitivity;

            // Debug log processed trigger values
            if (Time.frameCount % 60 == 0) // Log every second at 60fps
            {
                Debug.Log($"[Controller Debug] Generic: Processed trigger values: RT={rightTriggerValue:F2} (from {originalRightTrigger:F2}), LT={leftTriggerValue:F2} (from {originalLeftTrigger:F2})");
            }

            // Forza-style: If brake is pressed, it takes priority
            float result = 0f;

            // IMPORTANT: Special handling for the case where RT was just released
            // This prevents the car from automatically braking when RT is released
            bool rtJustReleased = previousRightTrigger > 0.1f && rightTriggerValue <= 0.1f;

            if (rtJustReleased) {
                // If RT was just released, ignore LT input for a short time to prevent auto-braking
                result = 0f;
                Debug.Log("[Controller Debug] Generic: RT just released - ignoring LT input to prevent auto-braking");
            }
            else if (leftTriggerValue > 0.1f) {
                // Return negative value for braking/reverse
                result = -leftTriggerValue;
                if (Time.frameCount % 60 == 0) // Log every second at 60fps
                {
                    Debug.Log($"[Controller Debug] Generic: Using left trigger for braking: {result:F2}");
                }
            } else if (rightTriggerValue > 0.1f) {
                // Return positive value for acceleration
                result = rightTriggerValue;
                if (Time.frameCount % 60 == 0) // Log every second at 60fps
                {
                    Debug.Log($"[Controller Debug] Generic: Using right trigger for acceleration: {result:F2}");
                }
            } else {
                // If both inputs are very small, return exactly zero to prevent auto-movement
                result = 0f;
                if (Time.frameCount % 60 == 0) // Log every second at 60fps
                {
                    Debug.Log("[Controller Debug] Generic: Both triggers below threshold, returning zero");
                }
            }

            // Store current values for next frame (processed values)
            previousLeftTrigger = leftTriggerValue;
            previousRightTrigger = rightTriggerValue;

            return result;
        }

        // If we get here, we couldn't find any trigger input
        // Fallback to button input
        if (Input.GetKey(KeyCode.JoystickButton0)) // A button
        {
            Debug.Log("[Xbox Controller] Using A button for acceleration");
            return 1.0f; // Accelerate
        }
        else if (Input.GetKey(KeyCode.JoystickButton1)) // B button
        {
            Debug.Log("[Xbox Controller] Using B button for braking/reverse");
            return -1.0f; // Brake/reverse
        }

        return 0f; // No input
    }

    // Special handling for Xbox controller triggers
    private bool TryGetXboxTriggerValues(out float rightTrigger, out float leftTrigger)
    {
        rightTrigger = 0f;
        leftTrigger = 0f;
        bool foundTriggers = false;

        // Try to get Xbox controller trigger values using various methods

        // Method 1: Try standard axis names
        try
        {
            rightTrigger = Input.GetAxis("XboxRightTriggerAxis");
            leftTrigger = Input.GetAxis("XboxLeftTriggerAxis");
            if (Mathf.Abs(rightTrigger) > 0.01f || Mathf.Abs(leftTrigger) > 0.01f)
            {
                foundTriggers = true;
                Debug.Log($"[Controller] Found Xbox triggers using standard names: RT={rightTrigger:F2}, LT={leftTrigger:F2}");
                return true;
            }
        }
        catch (System.Exception) { }

        // Skip trying RT and LT directly as they're not defined in the Input Manager

        // Method 3: Try combined axis (common on Xbox controllers)
        try
        {
            // On many Xbox controllers, triggers share a single axis
            // Right trigger is positive, left trigger is negative
            float combinedValue = Input.GetAxis("3rd axis");

            if (combinedValue > 0.01f)
            {
                rightTrigger = combinedValue;
                leftTrigger = 0f;
                foundTriggers = true;
                Debug.Log($"[Controller] Found right trigger on combined 3rd axis: {rightTrigger:F2}");
            }
            else if (combinedValue < -0.01f)
            {
                rightTrigger = 0f;
                leftTrigger = -combinedValue; // Convert to positive
                foundTriggers = true;
                Debug.Log($"[Controller] Found left trigger on combined 3rd axis: {leftTrigger:F2}");
            }

            if (foundTriggers)
            {
                return true;
            }
        }
        catch (System.Exception) { }

        // Method 4: Try Xbox Wireless Controller specific axes
        // These are the most common mappings for Xbox Wireless Controller
        try
        {
            // Try axis 2 and 5 (common for Xbox Wireless Controller)
            float rt = Input.GetAxis("joystick 1 axis 5");
            float lt = Input.GetAxis("joystick 1 axis 2");

            // Handle negative values (some controllers use negative values)
            if (rt < -0.01f) {
                // Negative RT might actually be LT
                leftTrigger = -rt;
                rightTrigger = 0f;
                foundTriggers = true;
                Debug.Log($"[Controller] Found Xbox Wireless Controller LT on axis 5 (negative): {leftTrigger:F2}");
            } else if (rt > 0.01f) {
                rightTrigger = rt;
                foundTriggers = true;
                Debug.Log($"[Controller] Found Xbox Wireless Controller RT on axis 5: {rightTrigger:F2}");
            }

            if (lt < -0.01f) {
                // Negative LT might actually be RT
                if (!foundTriggers) {
                    rightTrigger = -lt;
                    foundTriggers = true;
                    Debug.Log($"[Controller] Found Xbox Wireless Controller RT on axis 2 (negative): {rightTrigger:F2}");
                }
            } else if (lt > 0.01f) {
                leftTrigger = lt;
                foundTriggers = true;
                Debug.Log($"[Controller] Found Xbox Wireless Controller LT on axis 2: {leftTrigger:F2}");
            }

            if (foundTriggers) {
                return true;
            }
        }
        catch (System.Exception) { }

        // Method 5: Try more Xbox Wireless Controller specific axes
        try
        {
            // Try other common axis combinations
            rightTrigger = Input.GetAxis("joystick 1 axis 9");
            leftTrigger = Input.GetAxis("joystick 1 axis 8");

            if (Mathf.Abs(rightTrigger) > 0.01f || Mathf.Abs(leftTrigger) > 0.01f)
            {
                foundTriggers = true;
                Debug.Log($"[Controller] Found Xbox Wireless Controller triggers (alt): RT={rightTrigger:F2}, LT={leftTrigger:F2}");
                return true;
            }
        }
        catch (System.Exception) { }

        // Method 6: Try even more Xbox Wireless Controller specific axes
        try
        {
            // Try axis 3 (sometimes used as combined triggers)
            float combinedValue = Input.GetAxis("joystick 1 axis 3");

            if (combinedValue > 0.01f)
            {
                rightTrigger = combinedValue;
                leftTrigger = 0f;
                foundTriggers = true;
                Debug.Log($"[Controller] Found Xbox right trigger on axis 3: {rightTrigger:F2}");
                return true;
            }
            else if (combinedValue < -0.01f)
            {
                rightTrigger = 0f;
                leftTrigger = -combinedValue;
                foundTriggers = true;
                Debug.Log($"[Controller] Found Xbox left trigger on axis 3: {leftTrigger:F2}");
                return true;
            }
        }
        catch (System.Exception) { }

        // Method 7: Try macOS specific mappings
        try
        {
            // On macOS, Xbox controller triggers are often on these axes
            float rt = Input.GetAxis("joystick 1 axis 4");
            float lt = Input.GetAxis("joystick 1 axis 5");

            // Handle negative values (some controllers use negative values)
            if (rt < -0.01f) {
                // Negative RT might actually be LT
                if (!foundTriggers) {
                    leftTrigger = -rt;
                    foundTriggers = true;
                    Debug.Log($"[Controller] Found Xbox LT on macOS axis 4 (negative): {leftTrigger:F2}");
                }
            } else if (rt > 0.01f) {
                rightTrigger = rt;
                foundTriggers = true;
                Debug.Log($"[Controller] Found Xbox RT on macOS axis 4: {rightTrigger:F2}");
            }

            if (lt < -0.01f) {
                // Negative LT might actually be RT
                if (!foundTriggers) {
                    rightTrigger = -lt;
                    foundTriggers = true;
                    Debug.Log($"[Controller] Found Xbox RT on macOS axis 5 (negative): {rightTrigger:F2}");
                }
            } else if (lt > 0.01f) {
                leftTrigger = lt;
                foundTriggers = true;
                Debug.Log($"[Controller] Found Xbox LT on macOS axis 5: {leftTrigger:F2}");
            }

            if (foundTriggers) {
                return true;
            }
        }
        catch (System.Exception) { }

        // Method 8: Try direct access to all possible joystick axes
        for (int i = 0; i < 20; i++)
        {
            try
            {
                string axisName = $"joystick 1 axis {i}";
                float value = Input.GetAxis(axisName);

                if (Mathf.Abs(value) > 0.01f)
                {
                    // We found an active axis, now determine if it's right or left trigger
                    // For Xbox controllers, typically:
                    // - Right trigger is axis 5 or 9 or 4
                    // - Left trigger is axis 2 or 8 or 5
                    if (i == 5 || i == 9 || i == 4 || i == 3)
                    {
                        rightTrigger = value;
                        Debug.Log($"[Controller] Found right trigger on axis {i}: {value:F2}");
                        foundTriggers = true;
                    }
                    else if (i == 2 || i == 8 || i == 5 || i == 3)
                    {
                        // Note: axis 3 could be either trigger depending on the controller
                        // We'll check the sign to determine which one it is
                        if (i == 3 && value < 0)
                        {
                            leftTrigger = -value; // Convert negative to positive
                        }
                        else
                        {
                            leftTrigger = value;
                        }
                        Debug.Log($"[Controller] Found left trigger on axis {i}: {leftTrigger:F2}");
                        foundTriggers = true;
                    }
                }
            }
            catch (System.Exception) { }
        }

        // Method 9: Try direct axis access with standard names
        string[] rightTriggerAxes = new string[] { "5th axis", "9th axis", "4th axis", "Axis 5", "Axis 9", "Axis 4", "Axis 3", "Right Trigger" };
        string[] leftTriggerAxes = new string[] { "2nd axis", "8th axis", "5th axis", "Axis 2", "Axis 8", "Axis 5", "Left Trigger" };

        foreach (string axis in rightTriggerAxes)
        {
            try
            {
                float value = Input.GetAxis(axis);
                if (Mathf.Abs(value) > 0.01f)
                {
                    rightTrigger = value;
                    Debug.Log($"[Controller] Found right trigger on {axis}: {value:F2}");
                    foundTriggers = true;
                    break;
                }
            }
            catch (System.Exception) { }
        }

        foreach (string axis in leftTriggerAxes)
        {
            try
            {
                float value = Input.GetAxis(axis);
                if (Mathf.Abs(value) > 0.01f)
                {
                    leftTrigger = value;
                    Debug.Log($"[Controller] Found left trigger on {axis}: {value:F2}");
                    foundTriggers = true;
                    break;
                }
            }
            catch (System.Exception) { }
        }

        return foundTriggers;
    }

    private void Update()
    {
        // We don't want to reset the previous trigger values here as it can cause issues
        // Instead, we'll handle this in the individual input methods

        var behaviorParams = GetComponentInParent<Unity.MLAgents.Policies.BehaviorParameters>();
        bool isHeuristicMode = behaviorParams != null &&
                              behaviorParams.BehaviorType == Unity.MLAgents.Policies.BehaviorType.HeuristicOnly;

        // Call our debug method to check active axes
        if (Time.frameCount % 120 == 0) // Every 2 seconds at 60fps
        {
            DebugActiveAxes();

            // Try to detect Xbox controller triggers using a special method
            TryDetectXboxTriggers();
        }

        // Enhanced debug logging for controller troubleshooting
        if (showControllerDebug && Time.frameCount % 30 == 0) // Every half second at 60fps
        {
            Debug.Log("[Controller Debug] ===== CONTROLLER DEBUG START =====\n");

            // Check for joystick names first
            string[] joystickNames = Input.GetJoystickNames();
            bool isXboxController = false;

            for (int i = 0; i < joystickNames.Length; i++)
            {
                if (!string.IsNullOrEmpty(joystickNames[i]))
                {
                    Debug.Log($"[Controller Debug] Joystick {i}: {joystickNames[i]}");
                    if (joystickNames[i].ToLower().Contains("xbox"))
                    {
                        isXboxController = true;
                        Debug.Log("[Controller Debug] Xbox controller detected");
                    }
                }
            }

            // Check ALL possible joystick axes
            Debug.Log("[Controller Debug] Active joystick axes:");
            for (int i = 0; i < 30; i++)
            {
                try
                {
                    string axisName = $"joystick 1 axis {i}";
                    float value = Input.GetAxis(axisName);
                    if (Mathf.Abs(value) > 0.01f)
                    {
                        Debug.Log($"[Controller Debug] {axisName} value: {value:F2}");
                    }
                }
                catch (System.Exception) { }
            }

            // Check common controller axis names
            Debug.Log("[Controller Debug] Common controller axes:");
            string[] axisNames = new string[] {
                "Trigger", "Right Trigger", "Left Trigger", "Triggers",
                "3rd axis", "4th axis", "5th axis", "6th axis", "7th axis", "8th axis", "9th axis", "10th axis",
                "Horizontal", "Vertical", "XboxRightTriggerAxis", "XboxLeftTriggerAxis"
            };

            foreach (string axis in axisNames)
            {
                try
                {
                    float value = Input.GetAxis(axis);
                    if (Mathf.Abs(value) > 0.01f)
                    {
                        Debug.Log($"[Controller Debug] Axis '{axis}' value: {value:F2}");
                    }
                }
                catch (System.Exception) { }
            }

            // Debug which controller buttons are being pressed
            Debug.Log("[Controller Debug] Controller buttons:");
            string[] buttonNames = new string[] { "Jump", "Fire1", "Fire2", "Fire3", "Submit", "Cancel" };
            foreach (string buttonName in buttonNames)
            {
                try
                {
                    if (Input.GetButton(buttonName))
                    {
                        Debug.Log($"[Controller Debug] Button '{buttonName}' is pressed");
                    }
                }
                catch (System.Exception) { }
            }

            for (int i = 0; i < 20; i++)
            {
                if (Input.GetKey((KeyCode)(KeyCode.JoystickButton0 + i)))
                {
                    Debug.Log($"[Controller Debug] Joystick button {i} is pressed");

                    // Map Xbox controller buttons for Forza-style controls
                    if (i == 0) // A button
                    {
                        Debug.Log("[Controller Debug] A button pressed - can be used for acceleration");
                    }
                    else if (i == 1) // B button
                    {
                        Debug.Log("[Controller Debug] B button pressed - can be used for braking/reverse");
                    }
                    else if (i == 2) // X button
                    {
                        Debug.Log("[Controller Debug] X button pressed - can be used for handbrake");
                    }
                    else if (i == 3) // Y button
                    {
                        Debug.Log("[Controller Debug] Y button pressed - can be used for alternative acceleration");
                    }
                }
            }

            // Special case for Xbox controllers - check for combined trigger axis
            if (isXboxController)
            {
                Debug.Log("[Controller Debug] Xbox controller specific tests:");

                try
                {
                    float combinedTriggers = Input.GetAxis("3rd axis");
                    if (Mathf.Abs(combinedTriggers) > 0.01f)
                    {
                        Debug.Log($"[Controller Debug] Combined triggers axis value: {combinedTriggers:F2}");

                        // If we detect a combined trigger axis, update our controller settings
                        if (combinedTriggers > 0)
                        {
                            Debug.Log("[Controller Debug] Right trigger detected on 3rd axis (positive values)");
                        }
                        else
                        {
                            Debug.Log("[Controller Debug] Left trigger detected on 3rd axis (negative values)");
                        }
                    }
                }
                catch (System.Exception) { }

                // Try special Xbox trigger handling
                float rightTrigger, leftTrigger;
                if (TryGetXboxTriggerValues(out rightTrigger, out leftTrigger))
                {
                    Debug.Log($"[Controller Debug] Xbox triggers detected: Right={rightTrigger:F2}, Left={leftTrigger:F2}");
                }
                else
                {
                    Debug.Log("[Controller Debug] Xbox triggers not detected with TryGetXboxTriggerValues");
                }

                // Test Forza-style input
                float forzaInput = GetForzaStyleInput();
                Debug.Log($"[Controller Debug] Forza-style input value: {forzaInput:F2}");
            }

            Debug.Log("\n[Controller Debug] ===== CONTROLLER DEBUG END =====");
        }

        // When in ML-Agent control mode, prioritize ML-Agent inputs
        if (isMLAgentControlled && overrideInputs && !isHeuristicMode)
        {
            // In ML-Agent training mode, trust the inputs from OnActionReceived
            // They are set via SetMLAgentInputs

            // Apply inputs to vehicle controller for natural training flow
            ApplyInputsToVehicle();
            return;
        }

        // Only get player input in heuristic mode or when not in ML-Agent control
        if (isHeuristicMode || !isMLAgentControlled)
        {
            // Check if a controller is connected
            bool controllerConnected = IsControllerConnected();

            float rawSteer = 0f;
            float rawAccel = 0f;
            float rawHandbrake = 0f;

            // Get controller inputs if a controller is connected
            if (controllerConnected)
            {
                // REMOVED: Special handling for Xbox controller that forced GetForzaStyleInput
                // ALWAYS call the standard GetController... methods now
                // These methods handle the new Input System check internally
                    rawSteer = GetControllerSteerInput();
                    rawAccel = GetControllerAccelerationInput();
                    rawHandbrake = GetControllerHandbrakeInput();

                // Debug log controller inputs at low frequency
                if (Time.frameCount % 60 == 0) // Every second at 60fps for debugging
                {
                    Debug.Log($"[InputManager] Using controller inputs: Steer={rawSteer:F2}, Accel={rawAccel:F2}, Brake={rawHandbrake:F2}");

                    // Debug raw axis values - only try to access if they're valid joystick axes
                    if (controllerInput.accelerationAxis.StartsWith("joystick"))
                    {
                        try { Debug.Log($"[Controller Debug] Raw RT axis: {Input.GetAxis(controllerInput.accelerationAxis):F2}"); } catch { Debug.Log("[Controller Debug] RT axis error"); }
                    }

                    if (controllerInput.brakeAxis.StartsWith("joystick"))
                    {
                        try { Debug.Log($"[Controller Debug] Raw LT axis: {Input.GetAxis(controllerInput.brakeAxis):F2}"); } catch { Debug.Log("[Controller Debug] LT axis error"); }
                    }
                }
            }
            // Fall back to keyboard inputs if no controller is connected
            else
            {
                // Get keyboard inputs
                rawSteer = GetKeyboardSteerInput();
                rawAccel = GetKeyboardAccelerationInput();
                rawHandbrake = GetKeyboardHandbrakeInput();

                // Debug log keyboard inputs at low frequency
                if (verboseLogging && Time.frameCount % 300 == 0) // Every ~5 seconds
                {
                    Debug.Log($"[InputManager] Using keyboard inputs: Steer={rawSteer:F2}, Accel={rawAccel:F2}, Brake={rawHandbrake:F2}");
                }
            }

            // Apply inputs
            SteerInput = rawSteer;
            AccelerationInput = rawAccel;
            HandbrakeInput = rawHandbrake;
        }

        // Apply inputs to vehicle controller
        ApplyInputsToVehicle();
    }

    private void DebugActiveAxes()
    {
        // Check for active axes
        Debug.Log("[Controller] Checking active axes...");

        // Check standard Unity input axes that are likely to be defined
        Debug.Log("[Controller] Standard Unity input axes:");
        string[] standardAxes = new string[] {
            "Horizontal", "Vertical", "Mouse X", "Mouse Y",
            "Fire1", "Fire2", "Fire3", "Jump", "Submit", "Cancel"
        };

        foreach (string axisName in standardAxes)
        {
            try
            {
                float value = Input.GetAxis(axisName);
                Debug.Log($"[Controller] Standard axis '{axisName}': {value:F2}");
            }
            catch (Exception e)
            {
                Debug.Log($"[Controller] Standard axis '{axisName}' error: {e.Message}");
            }
        }

        // Check for active buttons
        Debug.Log("[Controller] Checking joystick buttons:");
        for (int i = 0; i < 20; i++)
        {
            if (Input.GetKey((KeyCode)(KeyCode.JoystickButton0 + i)))
            {
                Debug.Log($"[Controller] Button {i} is pressed");

                // Map specific buttons to functions
                if (i == 0) // A button
                {
                    Debug.Log("[Controller] A button can be used for acceleration");
                }
                else if (i == 1) // B button
                {
                    Debug.Log("[Controller] B button can be used for braking");
                }
                else if (i == 2) // X button
                {
                    Debug.Log("[Controller] X button can be used for handbrake");
                }
                else if (i == 3) // Y button
                {
                    Debug.Log("[Controller] Y button can be used as alternative acceleration");
                }
            }
        }

        // Check for controller presence
        string[] joystickNames = Input.GetJoystickNames();
        for (int i = 0; i < joystickNames.Length; i++)
        {
            if (!string.IsNullOrEmpty(joystickNames[i]))
            {
                Debug.Log($"[Controller] Joystick {i}: {joystickNames[i]}");
            }
        }
    }

    // Special method to detect Xbox controller triggers using a different approach
    private void TryDetectXboxTriggers()
    {
        Debug.Log("[Controller] Trying to detect Xbox controller triggers...");

        // Try to detect Xbox controller triggers using Input.GetJoystickNames
        string[] joystickNames = Input.GetJoystickNames();
        bool isXboxController = false;

        for (int i = 0; i < joystickNames.Length; i++)
        {
            if (!string.IsNullOrEmpty(joystickNames[i]) && joystickNames[i].ToLower().Contains("xbox"))
            {
                isXboxController = true;
                Debug.Log($"[Controller] Xbox controller detected: {joystickNames[i]}");
                break;
            }
        }

        if (isXboxController)
        {
            // Since the Input Manager axes aren't set up, we'll focus on buttons
            Debug.Log("[Controller] Checking Xbox controller buttons for trigger alternatives...");

            // Check all joystick buttons
            for (int i = 0; i < 20; i++)
            {
                if (Input.GetKey((KeyCode)(KeyCode.JoystickButton0 + i)))
                {
                    Debug.Log($"[Controller] Button {i} is pressed");

                    // Map specific buttons to functions
                    if (i == 0) // A button
                    {
                        Debug.Log("[Controller] A button can be used for acceleration");
                    }
                    else if (i == 1) // B button
                    {
                        Debug.Log("[Controller] B button can be used for braking");
                    }
                    else if (i == 2) // X button
                    {
                        Debug.Log("[Controller] X button can be used for handbrake");
                    }
                    else if (i == 3) // Y button
                    {
                        Debug.Log("[Controller] Y button can be used as alternative acceleration");
                    }
                }
            }

            // Check standard Unity input axes
            string[] standardAxes = new string[] {
                "Horizontal", "Vertical", "Fire1", "Fire2", "Fire3", "Jump", "Submit", "Cancel"
            };

            foreach (string axisName in standardAxes)
            {
                try
                {
                    float value = Input.GetAxis(axisName);
                    if (Mathf.Abs(value) > 0.1f)
                    {
                        Debug.Log($"[Controller] Standard axis '{axisName}' active: {value:F2}");
                    }
                }
                catch (Exception e)
                {
                    // Ignore errors
                }
            }

            // Check if we can use the right stick for acceleration/braking
            try
            {
                float rightStickY = Input.GetAxis("Vertical");
                if (Mathf.Abs(rightStickY) > 0.1f)
                {
                    Debug.Log($"[Controller] Right stick vertical axis active: {rightStickY:F2}");
                    Debug.Log("[Controller] Right stick can be used for acceleration/braking");
                }
            }
            catch (Exception e)
            {
                // Ignore errors
            }

            // Provide a summary of available controls
            Debug.Log("[Controller] Available controls for Xbox controller:");
            Debug.Log("[Controller] - A button: Acceleration");
            Debug.Log("[Controller] - B button: Braking/Reverse");
            Debug.Log("[Controller] - X button: Handbrake");
            Debug.Log("[Controller] - Y button: Alternative Acceleration");
            Debug.Log("[Controller] - Left stick: Steering");
        }
    }
}