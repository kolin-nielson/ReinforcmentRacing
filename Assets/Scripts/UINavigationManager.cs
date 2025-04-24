using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Manages UI navigation for controller input across all screens.
/// Ensures proper selection of UI elements and handles navigation between UI panels.
/// </summary>
public class UINavigationManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject raceUIPanel;
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject winScreenPanel;
    [SerializeField] private GameObject settingsPanel;
    
    [Header("Default Selected Elements")]
    [SerializeField] private GameObject mainMenuFirstSelected;
    [SerializeField] private GameObject pauseMenuFirstSelected;
    [SerializeField] private GameObject winScreenFirstSelected;
    [SerializeField] private GameObject settingsFirstSelected;
    
    [Header("Settings")]
    [SerializeField] private float inputDelay = 0.2f;
    [SerializeField] private bool debugMode = false;
    
    // Internal state tracking
    private GameObject currentPanel;
    private GameObject lastSelectedObject;
    private float lastInputTime;
    private bool isPaused = false;
    
    // Singleton instance
    public static UINavigationManager Instance { get; private set; }
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Ensure we have an EventSystem
        if (FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
            Debug.Log("[UINavigationManager] Created EventSystem as none was found");
        }
    }
    
    private void Start()
    {
        // Set initial UI state
        SetupInitialNavigation();
    }
    
    private void Update()
    {
        // Check for pause menu input
        CheckForPauseInput();
        
        // Ensure something is always selected when using controller
        EnsureSelection();

        // Added: handle controller UI navigation and confirm/cancel
        if (Gamepad.current != null)
        {
            // Read navigation input from left stick or D-pad
            Vector2 navigation = Gamepad.current.leftStick.ReadValue();
            if (navigation == Vector2.zero)
                navigation = Gamepad.current.dpad.ReadValue();

            // Only process if past input delay
            if (Time.unscaledTime - lastInputTime > inputDelay)
            {
                // Get currently selected UI element
                Selectable sel = EventSystem.current.currentSelectedGameObject?.GetComponent<Selectable>();
                // Navigate Up
                if (navigation.y > 0.5f && sel != null)
                {
                    var next = sel.FindSelectableOnUp();
                    if (next != null)
                    {
                        EventSystem.current.SetSelectedGameObject(next.gameObject);
                        lastInputTime = Time.unscaledTime;
                    }
                }
                // Navigate Down
                else if (navigation.y < -0.5f && sel != null)
                {
                    var next = sel.FindSelectableOnDown();
                    if (next != null)
                    {
                        EventSystem.current.SetSelectedGameObject(next.gameObject);
                        lastInputTime = Time.unscaledTime;
                    }
                }
                // Navigate Left
                else if (navigation.x < -0.5f && sel != null)
                {
                    var next = sel.FindSelectableOnLeft();
                    if (next != null)
                    {
                        EventSystem.current.SetSelectedGameObject(next.gameObject);
                        lastInputTime = Time.unscaledTime;
                    }
                }
                // Navigate Right
                else if (navigation.x > 0.5f && sel != null)
                {
                    var next = sel.FindSelectableOnRight();
                    if (next != null)
                    {
                        EventSystem.current.SetSelectedGameObject(next.gameObject);
                        lastInputTime = Time.unscaledTime;
                    }
                }
                // Confirm (A button)
                else if (Gamepad.current.buttonSouth.wasPressedThisFrame)
                {
                    lastInputTime = Time.unscaledTime;
                    var selected = EventSystem.current.currentSelectedGameObject;
                    if (selected != null)
                        ExecuteEvents.Execute(selected, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                }
                // Cancel/Back (B button)
                else if (Gamepad.current.buttonEast.wasPressedThisFrame)
                {
                    lastInputTime = Time.unscaledTime;
                    if (currentPanel == pauseMenuPanel)
                        ResumeGame();
                    else if (currentPanel == winScreenPanel || currentPanel == settingsPanel)
                        ShowMainMenu();
                    else if (currentPanel == raceUIPanel)
                        PauseGame();
                }
            }
        }
        
        // Debug info
        if (debugMode && Time.frameCount % 60 == 0)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            Debug.Log($"[UINavigationManager] Current selected: {(selected ? selected.name : "None")}   Panel: {currentPanel?.name}");
        }
    }
    
    /// <summary>
    /// Sets up initial navigation state when the game starts
    /// </summary>
    private void SetupInitialNavigation()
    {
        // Start with main menu
        if (mainMenuPanel != null)
        {
            SetActivePanel(mainMenuPanel);
            SelectUIElement(mainMenuFirstSelected);
        }
    }
    
    /// <summary>
    /// Ensures a UI element is always selected when using controller
    /// </summary>
    private void EnsureSelection()
    {
        // If nothing is selected but we have a last selected object, reselect it
        if (EventSystem.current.currentSelectedGameObject == null && lastSelectedObject != null)
        {
            if (lastSelectedObject.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(lastSelectedObject);
                if (debugMode)
                {
                    Debug.Log($"[UINavigationManager] Reselected: {lastSelectedObject.name}");
                }
            }
            else if (currentPanel != null)
            {
                // Find a selectable element in the current panel
                Selectable[] selectables = currentPanel.GetComponentsInChildren<Selectable>(false);
                if (selectables.Length > 0)
                {
                    EventSystem.current.SetSelectedGameObject(selectables[0].gameObject);
                    if (debugMode)
                    {
                        Debug.Log($"[UINavigationManager] Selected first available: {selectables[0].name}");
                    }
                }
            }
        }
        
        // Store the current selection for future reference
        if (EventSystem.current.currentSelectedGameObject != null)
        {
            lastSelectedObject = EventSystem.current.currentSelectedGameObject;
        }
    }
    
    /// <summary>
    /// Checks for pause menu input
    /// </summary>
    private void CheckForPauseInput()
    {
        // Check for pause button (Start button on controller or Escape key)
        bool pausePressed = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame || 
                           Input.GetKeyDown(KeyCode.Escape);
        
        if (pausePressed && Time.unscaledTime - lastInputTime > inputDelay)
        {
            lastInputTime = Time.unscaledTime;
            
            // Toggle pause state
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }
    
    /// <summary>
    /// Pauses the game and shows the pause menu
    /// </summary>
    public void PauseGame()
    {
        if (isPaused) return;
        
        isPaused = true;
        Time.timeScale = 0f;
        
        if (pauseMenuPanel != null)
        {
            SetActivePanel(pauseMenuPanel);
            SelectUIElement(pauseMenuFirstSelected);
        }
        
        Debug.Log("[UINavigationManager] Game paused");
    }
    
    /// <summary>
    /// Resumes the game and hides the pause menu
    /// </summary>
    public void ResumeGame()
    {
        if (!isPaused) return;
        
        isPaused = false;
        Time.timeScale = 1f;
        
        if (raceUIPanel != null)
        {
            SetActivePanel(raceUIPanel);
        }
        
        Debug.Log("[UINavigationManager] Game resumed");
    }
    
    /// <summary>
    /// Shows the main menu
    /// </summary>
    public void ShowMainMenu()
    {
        Time.timeScale = 1f;
        isPaused = false;
        
        SetActivePanel(mainMenuPanel);
        SelectUIElement(mainMenuFirstSelected);
    }
    
    /// <summary>
    /// Shows the race UI
    /// </summary>
    public void ShowRaceUI()
    {
        Time.timeScale = 1f;
        isPaused = false;
        
        SetActivePanel(raceUIPanel);
    }
    
    /// <summary>
    /// Shows the win screen
    /// </summary>
    public void ShowWinScreen()
    {
        Time.timeScale = 0f;
        isPaused = true;
        
        SetActivePanel(winScreenPanel);
        SelectUIElement(winScreenFirstSelected);
    }
    
    /// <summary>
    /// Shows the settings panel
    /// </summary>
    public void ShowSettings()
    {
        SetActivePanel(settingsPanel);
        SelectUIElement(settingsFirstSelected);
    }
    
    /// <summary>
    /// Sets the active panel and deactivates others
    /// </summary>
    private void SetActivePanel(GameObject panel)
    {
        if (panel == null) return;
        
        currentPanel = panel;
        
        // Deactivate all panels
        if (mainMenuPanel != null) mainMenuPanel.SetActive(mainMenuPanel == panel);
        if (raceUIPanel != null) raceUIPanel.SetActive(raceUIPanel == panel);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(pauseMenuPanel == panel);
        if (winScreenPanel != null) winScreenPanel.SetActive(winScreenPanel == panel);
        if (settingsPanel != null) settingsPanel.SetActive(settingsPanel == panel);
        
        // Activate the specified panel
        panel.SetActive(true);
    }
    
    /// <summary>
    /// Selects a UI element if it exists
    /// </summary>
    private void SelectUIElement(GameObject element)
    {
        if (element != null && element.activeInHierarchy)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(element);
            lastSelectedObject = element;
            
            if (debugMode)
            {
                Debug.Log($"[UINavigationManager] Selected UI element: {element.name}");
            }
        }
    }
    
    /// <summary>
    /// Manually select a UI element by name within the current panel
    /// </summary>
    public void SelectElementByName(string elementName)
    {
        if (currentPanel == null) return;
        
        Transform element = currentPanel.transform.Find(elementName);
        if (element != null)
        {
            SelectUIElement(element.gameObject);
        }
        else
        {
            Debug.LogWarning($"[UINavigationManager] Could not find UI element: {elementName}");
        }
    }
}
