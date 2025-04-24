using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class InputAxisSetup : MonoBehaviour
{
    [Header("Xbox Controller Trigger Axes")]
    [SerializeField] private bool setupOnStart = true;
    [SerializeField] private bool showDebugInfo = true;

    private void Start()
    {
        if (setupOnStart)
        {
            SetupTriggerAxes();
            SetupDirectJoystickAxes();
        }
    }

    public void SetupTriggerAxes()
    {
#if UNITY_EDITOR
        // This will only work in the editor
        // Add the main trigger axes
        AddAxisToInputManager("RT", "Right Trigger", 5, 0.19f);
        AddAxisToInputManager("LT", "Left Trigger", 2, 0.19f);

        // Add alternative trigger axes
        AddAxisToInputManager("RT_Alt", "Right Trigger Alternative", 9, 0.19f);
        AddAxisToInputManager("LT_Alt", "Left Trigger Alternative", 8, 0.19f);

        // Add combined triggers axis
        AddAxisToInputManager("Triggers", "Combined Triggers", 3, 0.19f);

        Debug.Log("[InputAxisSetup] Added trigger axes to Input Manager");
#else
        Debug.LogWarning("[InputAxisSetup] Input axes can only be added in the editor. Please add them manually in Project Settings > Input Manager.");

        // In builds, we'll try to set up direct joystick axes
        SetupRuntimeDirectJoystickAxes();
#endif
    }

    public void SetupDirectJoystickAxes()
    {
#if UNITY_EDITOR
        // Add direct joystick axes
        for (int i = 0; i <= 10; i++)
        {
            AddAxisToInputManager($"joystick 1 axis {i}", $"Joystick 1 Axis {i}", i, 0.19f, 1);
        }

        Debug.Log("[InputAxisSetup] Added direct joystick axes to Input Manager");
#else
        SetupRuntimeDirectJoystickAxes();
#endif
    }

    private void SetupRuntimeDirectJoystickAxes()
    {
        // In builds, we can't modify the Input Manager, but we can try to detect which axes are available
        StartCoroutine(DetectJoystickAxes());
    }

    private IEnumerator DetectJoystickAxes()
    {
        yield return new WaitForSeconds(1f); // Wait a bit for input system to initialize

        Debug.Log("[InputAxisSetup] Detecting available joystick axes at runtime...");

        // Try to detect which axes are available
        for (int i = 0; i <= 20; i++)
        {
            try
            {
                string axisName = $"joystick 1 axis {i}";
                float value = Input.GetAxis(axisName);
                Debug.Log($"[InputAxisSetup] Axis {axisName} is available, current value: {value:F2}");
            }
            catch (System.Exception)
            {
                // Axis not available
            }
        }

        // Try to detect standard axes
        string[] standardAxes = new string[] { "RT", "LT", "RT_Alt", "LT_Alt", "Triggers" };
        foreach (string axisName in standardAxes)
        {
            try
            {
                float value = Input.GetAxis(axisName);
                Debug.Log($"[InputAxisSetup] Standard axis {axisName} is available, current value: {value:F2}");
            }
            catch (System.Exception)
            {
                // Axis not available
            }
        }
    }

#if UNITY_EDITOR
    private void AddAxisToInputManager(string name, string descriptiveName, int axisNum, float deadZone, int joyNum = 0)
    {
        // Get the SerializedObject of the InputManager
        SerializedObject inputManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);

        // Get the axes array
        SerializedProperty axesProperty = inputManager.FindProperty("m_Axes");

        // Check if the axis already exists
        bool axisExists = false;
        for (int i = 0; i < axesProperty.arraySize; i++)
        {
            SerializedProperty axis = axesProperty.GetArrayElementAtIndex(i);
            if (axis.FindPropertyRelative("m_Name").stringValue == name)
            {
                axisExists = true;
                break;
            }
        }

        // If the axis doesn't exist, add it
        if (!axisExists)
        {
            // Add a new element to the array
            axesProperty.arraySize++;
            inputManager.ApplyModifiedProperties();

            // Get the new element
            SerializedProperty axis = axesProperty.GetArrayElementAtIndex(axesProperty.arraySize - 1);

            // Set the properties
            axis.FindPropertyRelative("m_Name").stringValue = name;
            axis.FindPropertyRelative("descriptiveName").stringValue = descriptiveName;
            axis.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
            axis.FindPropertyRelative("negativeButton").stringValue = "";
            axis.FindPropertyRelative("positiveButton").stringValue = "";
            axis.FindPropertyRelative("altNegativeButton").stringValue = "";
            axis.FindPropertyRelative("altPositiveButton").stringValue = "";
            axis.FindPropertyRelative("gravity").floatValue = 0;
            axis.FindPropertyRelative("dead").floatValue = deadZone;
            axis.FindPropertyRelative("sensitivity").floatValue = 1;
            axis.FindPropertyRelative("snap").boolValue = false;
            axis.FindPropertyRelative("invert").boolValue = false;
            axis.FindPropertyRelative("type").intValue = 2; // Joystick axis
            axis.FindPropertyRelative("axis").intValue = axisNum; // The axis number
            axis.FindPropertyRelative("joyNum").intValue = joyNum; // 0 = any joystick, 1 = first joystick

            // Apply the changes
            inputManager.ApplyModifiedProperties();

            if (showDebugInfo)
            {
                Debug.Log($"[InputAxisSetup] Added axis '{name}' to Input Manager");
            }
        }
        else if (showDebugInfo)
        {
            Debug.Log($"[InputAxisSetup] Axis '{name}' already exists in Input Manager");
        }
    }
#endif

    public void LogAllInputAxes()
    {
#if UNITY_EDITOR
        // Get the SerializedObject of the InputManager
        SerializedObject inputManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0]);

        // Get the axes array
        SerializedProperty axesProperty = inputManager.FindProperty("m_Axes");

        Debug.Log($"[InputAxisSetup] Input Manager has {axesProperty.arraySize} axes:");

        // Log all axes
        for (int i = 0; i < axesProperty.arraySize; i++)
        {
            SerializedProperty axis = axesProperty.GetArrayElementAtIndex(i);
            string name = axis.FindPropertyRelative("m_Name").stringValue;
            int axisNum = axis.FindPropertyRelative("axis").intValue;
            float deadZone = axis.FindPropertyRelative("dead").floatValue;
            int joyNum = axis.FindPropertyRelative("joyNum").intValue;

            Debug.Log($"[InputAxisSetup] Axis {i}: {name}, Axis Number: {axisNum}, Joy Num: {joyNum}, Dead Zone: {deadZone}");
        }
#else
        Debug.LogWarning("[InputAxisSetup] Input axes can only be inspected in the editor.");

        // In builds, we'll try to detect which axes are available
        StartCoroutine(DetectJoystickAxes());
#endif
    }
}
