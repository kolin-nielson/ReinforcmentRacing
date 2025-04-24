using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Manages the main menu UI and scene transitions for the Reinforced Racing project.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // Helper component to store track data on buttons
    public class TrackButtonData : MonoBehaviour
    {
        public TrackData trackData;
    }
    [Header("Main Menu UI")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private Button raceAIButton;
    [SerializeField] private Button watchAIButton;
    [SerializeField] private Button quitButton;

    [Header("Track Selection UI")]
    [SerializeField] private GameObject trackSelectionPanel;
    [SerializeField] private Button backButton;
    [SerializeField] private Transform trackButtonsContainer;
    [SerializeField] private GameObject trackButtonPrefab;

    [Header("Loading UI")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Slider loadingProgressBar;
    [SerializeField] private TextMeshProUGUI loadingText;

    // Using shared TrackData class

    [Header("Track Data")]
    [SerializeField] private List<TrackData> availableTracks = new List<TrackData>();

    // Mode selection
    private enum GameMode { RaceAI, WatchAI }
    private GameMode selectedMode;

    // Public methods for MainMenuSetup
    public void SetMainMenuPanel(GameObject panel)
    {
        mainMenuPanel = panel;

        // Find buttons
        if (mainMenuPanel != null)
        {
            raceAIButton = mainMenuPanel.transform.Find("RaceAIButton")?.GetComponent<Button>();
            watchAIButton = mainMenuPanel.transform.Find("WatchAIButton")?.GetComponent<Button>();
            quitButton = mainMenuPanel.transform.Find("QuitButton")?.GetComponent<Button>();
        }
    }

    public void SetTrackSelectionPanel(GameObject panel)
    {
        trackSelectionPanel = panel;

        // Find components
        if (trackSelectionPanel != null)
        {
            backButton = trackSelectionPanel.transform.Find("BackButton")?.GetComponent<Button>();
            trackButtonsContainer = trackSelectionPanel.transform.Find("TracksContainer")?.transform;
        }
    }

    public void SetLoadingPanel(GameObject panel)
    {
        loadingPanel = panel;

        // Find components
        if (loadingPanel != null)
        {
            loadingProgressBar = loadingPanel.transform.Find("ProgressBar")?.GetComponent<Slider>();
            loadingText = loadingPanel.transform.Find("LoadingText")?.GetComponent<TextMeshProUGUI>();
        }
    }

    public void SetTrackButtonPrefab(GameObject prefab)
    {
        trackButtonPrefab = prefab;
    }

    public void SetTrackData(List<TrackData> tracks)
    {
        availableTracks = tracks;
    }

    public void LoadTrack(string sceneName)
    {
        StartCoroutine(LoadTrackAsync(sceneName));
    }

    private void Start()
    {
        // Validate required references
        ValidateReferences();

        // Initialize UI
        ShowMainMenu();

        // Add button listeners
        if (raceAIButton != null) raceAIButton.onClick.AddListener(() => OnRaceAIClicked());
        if (watchAIButton != null) watchAIButton.onClick.AddListener(() => OnWatchAIClicked());
        if (quitButton != null) quitButton.onClick.AddListener(() => OnQuitClicked());
        if (backButton != null) backButton.onClick.AddListener(() => ShowMainMenu());

        // Hide loading panel initially
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    private void ValidateReferences()
    {
        bool hasErrors = false;

        // Check main menu panel and buttons
        if (mainMenuPanel == null)
        {
            Debug.LogError("Main Menu Panel is not assigned!");
            hasErrors = true;
        }

        if (raceAIButton == null || watchAIButton == null || quitButton == null)
        {
            Debug.LogError("One or more main menu buttons are not assigned!");
            hasErrors = true;
        }

        // Check track selection panel
        if (trackSelectionPanel == null)
        {
            Debug.LogError("Track Selection Panel is not assigned!");
            hasErrors = true;
        }

        if (trackButtonsContainer == null)
        {
            Debug.LogError("Track Buttons Container is not assigned!");
            hasErrors = true;
        }

        if (trackButtonPrefab == null)
        {
            Debug.LogError("Track Button Prefab is not assigned!");
            hasErrors = true;
        }

        // Check loading panel
        if (loadingPanel == null)
        {
            Debug.LogError("Loading Panel is not assigned!");
            hasErrors = true;
        }

        // Check track data
        if (availableTracks == null || availableTracks.Count == 0)
        {
            Debug.LogWarning("No tracks available! Please add track data in the inspector.");
        }

        if (hasErrors)
        {
            Debug.LogWarning("Please fix the above errors in the MainMenuManager component!");
        }
    }

    private void OnRaceAIClicked()
    {
        selectedMode = GameMode.RaceAI;
        ShowTrackSelection();
    }

    private void OnWatchAIClicked()
    {
        selectedMode = GameMode.WatchAI;
        ShowTrackSelection();
    }

    private void OnQuitClicked()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    public void ShowMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (trackSelectionPanel != null) trackSelectionPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    private void ShowTrackSelection()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (trackSelectionPanel != null) trackSelectionPanel.SetActive(true);

        // Check if track buttons container exists
        if (trackButtonsContainer == null)
        {
            Debug.LogError("Track buttons container is null! Cannot create track buttons.");
            return;
        }

        // Clear existing track buttons
        foreach (Transform child in trackButtonsContainer)
        {
            Destroy(child.gameObject);
        }

        // Create track buttons
        if (trackButtonPrefab == null)
        {
            Debug.LogWarning("Track button prefab is null! Creating a default button prefab.");
            trackButtonPrefab = CreateDefaultTrackButton();

            if (trackButtonPrefab == null)
            {
                Debug.LogError("Failed to create default track button prefab!");
                return;
            }
        }

        // Check if there are any tracks available
        if (availableTracks == null || availableTracks.Count == 0)
        {
            Debug.LogWarning("No tracks available! Please add track data in the inspector.");

            // Create a message in the track selection panel
            GameObject messageObj = new GameObject("NoTracksMessage");
            messageObj.transform.SetParent(trackButtonsContainer, false);
            TextMeshProUGUI messageText = messageObj.AddComponent<TextMeshProUGUI>();
            messageText.text = "No tracks available. Please add track data in the inspector.";
            messageText.fontSize = 24;
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.color = Color.white;

            RectTransform messageRect = messageObj.GetComponent<RectTransform>();
            messageRect.anchorMin = new Vector2(0, 0);
            messageRect.anchorMax = new Vector2(1, 1);
            messageRect.sizeDelta = Vector2.zero;

            return;
        }

        foreach (TrackData track in availableTracks)
        {
            // Skip if track data is invalid
            if (track == null || string.IsNullOrEmpty(track.sceneName))
            {
                Debug.LogWarning("Skipping invalid track data");
                continue;
            }

            try
            {
                GameObject buttonObj = Instantiate(trackButtonPrefab, trackButtonsContainer);
                Button button = buttonObj.GetComponent<Button>();
                TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                Image buttonImage = buttonObj.GetComponent<Image>();

                if (buttonText != null) buttonText.text = track.trackName;
                if (buttonImage != null && track.trackPreview != null) buttonImage.sprite = track.trackPreview;

                // Store the track data for the button
                TrackButtonData buttonData = buttonObj.AddComponent<TrackButtonData>();
                buttonData.trackData = track;

                // Add click listener to select the track (not load it directly)
                button.onClick.AddListener(() => OnTrackButtonClicked(buttonData));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error creating track button: {e.Message}");
            }
        }
    }

    // LoadTrack method is already defined as public above

    private void OnTrackButtonClicked(TrackButtonData buttonData)
    {
        if (buttonData == null || buttonData.trackData == null)
        {
            Debug.LogError("Track button data is null!");
            return;
        }

        // Find the TrackSelectionManager and call its method
        TrackSelectionManager trackSelectionManager = FindObjectOfType<TrackSelectionManager>();
        if (trackSelectionManager != null)
        {
            trackSelectionManager.OnTrackButtonClicked(buttonData.trackData);
        }
        else
        {
            Debug.LogError("TrackSelectionManager not found!");
        }
    }

    private GameObject CreateDefaultTrackButton()
    {
        try
        {
            // Create a new button GameObject
            GameObject buttonObj = new GameObject("DefaultTrackButton");

            // Add a RectTransform component
            RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 50);

            // Add a Button component
            Button button = buttonObj.AddComponent<Button>();

            // Add an Image component for the button background
            Image image = buttonObj.AddComponent<Image>();
            image.color = Color.white;

            // Create a child GameObject for the text
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            // Add a RectTransform component to the text
            RectTransform textRectTransform = textObj.AddComponent<RectTransform>();
            textRectTransform.anchorMin = Vector2.zero;
            textRectTransform.anchorMax = Vector2.one;
            textRectTransform.sizeDelta = Vector2.zero;

            // Add a TextMeshProUGUI component
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "Track";
            text.color = Color.black;
            text.fontSize = 24;
            text.alignment = TextAlignmentOptions.Center;

            // Set the button's navigation to None
            Navigation navigation = new Navigation();
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            // Set the button's colors
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.9f, 0.9f, 0.9f);
            colors.highlightedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            colors.selectedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;

            // Don't destroy the button when loading a new scene
            DontDestroyOnLoad(buttonObj);

            return buttonObj;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error creating default track button: {e.Message}");
            return null;
        }
    }

    private IEnumerator LoadTrackAsync(string sceneName)
    {
        // Show loading panel
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (trackSelectionPanel != null) trackSelectionPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(true);

        // Set game mode in PlayerPrefs for the scene to read
        PlayerPrefs.SetInt("GameMode", (int)selectedMode);
        PlayerPrefs.Save();

        // Start loading the scene
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        // Update progress bar
        while (asyncLoad.progress < 0.9f)
        {
            if (loadingProgressBar != null) loadingProgressBar.value = asyncLoad.progress;
            if (loadingText != null) loadingText.text = $"Loading... {asyncLoad.progress * 100:F0}%";
            yield return null;
        }

        // Scene is almost ready
        if (loadingProgressBar != null) loadingProgressBar.value = 1f;
        if (loadingText != null) loadingText.text = "Press any key to continue...";

        // Wait for user input
        yield return new WaitUntil(() => Input.anyKeyDown);

        // Activate the scene
        asyncLoad.allowSceneActivation = true;
    }
}
