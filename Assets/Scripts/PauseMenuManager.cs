using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple pause menu functionality.
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button mainMenuButton;
    [Header("Player Car Settings")]
    [SerializeField] private InputField maxSpeedField;
    [SerializeField] private InputField accelerationField;
    [SerializeField] private Button applySettingsButton;

    private bool isPaused = false;
    private bool settingsUICreated = false;

    private void Start()
    {
        // Initialize pause menu
        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        // Add button listeners
        if (resumeButton != null)
        {
            resumeButton.onClick.AddListener(ResumeGame);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }
        // Add listener for applying player settings
        if (applySettingsButton != null)
        {
            applySettingsButton.onClick.AddListener(ApplyPlayerSettings);
        }
    }

    private void Update()
    {
        // Toggle pause menu with Escape key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(isPaused);
            if (isPaused) // Pausing
            {
                if (!settingsUICreated)
                {
                    Debug.Log("[PauseMenuManager] Creating settings UI programmatically");
                    CreateSettingsUI();
                    settingsUICreated = true;
                }
                // Populate fields with current values before pausing time
                InitializeSettingsFields();
                // Set time scale to 0 AFTER activating the panel and initializing fields
                Time.timeScale = 0f;
            }
            else // Resuming via Toggle
            {
                // Set time scale back to 1 when unpausing
                Time.timeScale = 1f;
            }
        }

        // Removed the general Time.timeScale setting from here
        // Time.timeScale = isPaused ? 0f : 1f;
    }

    public void ResumeGame()
    {
        isPaused = false;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }

        // Resume time
        Time.timeScale = 1f;
    }

    public void ReturnToMainMenu()
    {
        // Ensure time scale is reset before loading the main menu
        Time.timeScale = 1f;
        isPaused = false; // Ensure pause state is reset

        // Load main menu scene
        SceneManager.LoadScene("MainMenu");
    }

    // Populate input fields with current stats from the player's VehicleController
    private void InitializeSettingsFields()
    {
        var vc = FindObjectOfType<VehicleController>();
        if (vc != null)
        {
            if (maxSpeedField != null)
                maxSpeedField.text = vc.MaxSpeed.ToString("F0");
            if (accelerationField != null)
                accelerationField.text = vc.Acceleration.ToString("F1");
        }
    }

    // Apply settings entered in the pause menu to the player's VehicleController
    private void ApplyPlayerSettings()
    {
        var vc = FindObjectOfType<VehicleController>();
        if (vc == null) return;
        float ms, acc;
        if (maxSpeedField != null && float.TryParse(maxSpeedField.text, out ms) &&
            accelerationField != null && float.TryParse(accelerationField.text, out acc))
        {
            vc.OverridePlayerStats(ms, acc);
        }
    }

    // Dynamically builds the input fields and apply button
    private void CreateSettingsUI()
    {
        Debug.Log("[PauseMenuManager] CreateSettingsUI() called");
        // Max Speed InputField
        if (maxSpeedField == null)
        {
            GameObject msGO = new GameObject("MaxSpeedField", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            msGO.transform.SetParent(pauseMenuPanel.transform, false);
            msGO.transform.SetAsLastSibling();
            Debug.Log("[PauseMenuManager] Created MaxSpeedField");
            var rt = msGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(10, -10);
            rt.sizeDelta = new Vector2(140, 30);
            var img = msGO.GetComponent<Image>(); img.color = new Color(1f,1f,1f,0.9f);
            var input = msGO.GetComponent<InputField>(); maxSpeedField = input;
            // Configure InputField
            input.targetGraphic = img;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.lineType = InputField.LineType.SingleLine;
            // Add layout element for sizing in panel
            var le1 = msGO.AddComponent<LayoutElement>();
            le1.preferredWidth = 140;
            le1.preferredHeight = 30;
            // Text component
            var textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGO.transform.SetParent(msGO.transform, false);
            var txtRT = textGO.GetComponent<RectTransform>(); txtRT.anchorMin=Vector2.zero; txtRT.anchorMax=Vector2.one; txtRT.offsetMin=Vector2.zero; txtRT.offsetMax=Vector2.zero;
            var txt = textGO.GetComponent<Text>(); txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); txt.text = ""; txt.alignment = TextAnchor.MiddleLeft; txt.color = Color.black;
            input.textComponent = txt;
            // Placeholder
            var phGO = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            phGO.transform.SetParent(msGO.transform, false);
            var phRT = phGO.GetComponent<RectTransform>(); phRT.anchorMin=Vector2.zero; phRT.anchorMax=Vector2.one; phRT.offsetMin=Vector2.zero; phRT.offsetMax=Vector2.zero;
            var ph = phGO.GetComponent<Text>(); ph.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); ph.text = "Max Speed"; ph.alignment = TextAnchor.MiddleLeft; ph.color = Color.gray;
            input.placeholder = ph;
        }
        // Acceleration InputField
        if (accelerationField == null)
        {
            GameObject accGO = new GameObject("AccelerationField", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            accGO.transform.SetParent(pauseMenuPanel.transform, false);
            accGO.transform.SetAsLastSibling();
            Debug.Log("[PauseMenuManager] Created AccelerationField");
            var rt = accGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(10, -50);
            rt.sizeDelta = new Vector2(140, 30);
            var img = accGO.GetComponent<Image>(); img.color = new Color(1f,1f,1f,0.9f);
            var input = accGO.GetComponent<InputField>(); accelerationField = input;
            // Configure InputField
            input.targetGraphic = img;
            input.contentType = InputField.ContentType.DecimalNumber;
            input.lineType = InputField.LineType.SingleLine;
            // Add layout element for sizing
            var le2 = accGO.AddComponent<LayoutElement>();
            le2.preferredWidth = 140;
            le2.preferredHeight = 30;
            // Text component
            var textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGO.transform.SetParent(accGO.transform, false);
            var txtRT = textGO.GetComponent<RectTransform>(); txtRT.anchorMin=Vector2.zero; txtRT.anchorMax=Vector2.one; txtRT.offsetMin=Vector2.zero; txtRT.offsetMax=Vector2.zero;
            var txt = textGO.GetComponent<Text>(); txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); txt.text = ""; txt.alignment = TextAnchor.MiddleLeft; txt.color = Color.black;
            input.textComponent = txt;
            // Placeholder
            var phGO = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            phGO.transform.SetParent(accGO.transform, false);
            var phRT = phGO.GetComponent<RectTransform>(); phRT.anchorMin=Vector2.zero; phRT.anchorMax=Vector2.one; phRT.offsetMin=Vector2.zero; phRT.offsetMax=Vector2.zero;
            var ph = phGO.GetComponent<Text>(); ph.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); ph.text = "Acceleration"; ph.alignment = TextAnchor.MiddleLeft; ph.color = Color.gray;
            input.placeholder = ph;
        }
        // Apply Button
        if (applySettingsButton == null)
        {
            GameObject btnGO = new GameObject("ApplySettingsButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(pauseMenuPanel.transform, false);
            btnGO.transform.SetAsLastSibling();
            Debug.Log("[PauseMenuManager] Created ApplySettingsButton");
            var rt = btnGO.GetComponent<RectTransform>(); rt.anchorMin=new Vector2(0,1); rt.anchorMax=new Vector2(0,1); rt.pivot=new Vector2(0,1); rt.anchoredPosition=new Vector2(10,-90); rt.sizeDelta=new Vector2(100,30);
            var img = btnGO.GetComponent<Image>(); img.color=new Color(1f,1f,1f,0.9f);
            var btn = btnGO.GetComponent<Button>(); applySettingsButton = btn;
            btn.onClick.AddListener(ApplyPlayerSettings);
            // Configure Button graphic and size
            btn.targetGraphic = img;
            var le3 = btnGO.AddComponent<LayoutElement>();
            le3.preferredWidth = 100;
            le3.preferredHeight = 30;
            // Button Text
            var textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGO.transform.SetParent(btnGO.transform, false);
            var txtRT = textGO.GetComponent<RectTransform>(); txtRT.anchorMin=Vector2.zero; txtRT.anchorMax=Vector2.one; txtRT.offsetMin=Vector2.zero; txtRT.offsetMax=Vector2.zero;
            var txt = textGO.GetComponent<Text>(); txt.font=Resources.GetBuiltinResource<Font>("Arial.ttf"); txt.text="Apply"; txt.alignment=TextAnchor.MiddleCenter; txt.color=Color.black;
        }
    }
}
