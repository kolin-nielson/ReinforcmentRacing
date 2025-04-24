using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages track selection UI and loading for the Reinforced Racing project.
/// </summary>
public class TrackSelectionManager : MonoBehaviour
{
    [Header("Track Selection UI")]
    [SerializeField] private GameObject trackSelectionPanel;
    [SerializeField] private Button backButton;
    [SerializeField] private Transform trackButtonsContainer;
    [SerializeField] private GameObject trackButtonPrefab;

    [Header("Track Preview")]
    [SerializeField] private Image trackPreviewImage;
    [SerializeField] private TextMeshProUGUI trackNameText;
    [SerializeField] private TextMeshProUGUI trackDescriptionText;
    [SerializeField] private Button selectButton;

    // Using shared TrackData class

    [Header("Track Data")]
    [SerializeField] private List<TrackData> availableTracks = new List<TrackData>();

    private TrackData selectedTrack;
    private MainMenuManager mainMenuManager;

    private void Start()
    {
        mainMenuManager = FindObjectOfType<MainMenuManager>();

        // Add button listeners
        if (backButton != null) backButton.onClick.AddListener(() => OnBackButtonClicked());
        if (selectButton != null) selectButton.onClick.AddListener(() => OnSelectButtonClicked());

        // Initialize UI
        PopulateTrackButtons();
        UpdateTrackPreview(null);
    }

    private void PopulateTrackButtons()
    {
        // Check if trackButtonPrefab is assigned
        if (trackButtonPrefab == null)
        {
            Debug.LogError("[TrackSelectionManager] trackButtonPrefab is not assigned! Please assign it in the inspector.");

            // Try to find a prefab in the Resources folder
            trackButtonPrefab = Resources.Load<GameObject>("TrackButtonPrefab");

            if (trackButtonPrefab == null)
            {
                // Create a default button if no prefab is found
                trackButtonPrefab = CreateDefaultTrackButton();

                if (trackButtonPrefab == null)
                {
                    Debug.LogError("[TrackSelectionManager] Failed to create a default track button prefab!");
                    return;
                }
            }
        }

        // Clear existing track buttons
        foreach (Transform child in trackButtonsContainer)
        {
            Destroy(child.gameObject);
        }

        // Create track buttons
        foreach (TrackData track in availableTracks)
        {
            GameObject buttonObj = Instantiate(trackButtonPrefab, trackButtonsContainer);
            Button button = buttonObj.GetComponent<Button>();
            TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            Image buttonImage = buttonObj.GetComponent<Image>();

            if (buttonText != null) buttonText.text = track.trackName;
            if (buttonImage != null && track.trackPreview != null) buttonImage.sprite = track.trackPreview;

            // Add click listener
            button.onClick.AddListener(() => OnTrackButtonClicked(track));
        }
    }

    /// <summary>
    /// Creates a default track button prefab if none is assigned.
    /// </summary>
    private GameObject CreateDefaultTrackButton()
    {
        try
        {
            // Create a new GameObject for the button
            GameObject buttonObj = new GameObject("DefaultTrackButton");

            // Add RectTransform component
            RectTransform rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 50);

            // Add Image component
            Image image = buttonObj.AddComponent<Image>();
            image.color = Color.white;

            // Add Button component
            buttonObj.AddComponent<Button>();

            // Create child GameObject for text
            GameObject textObj = new GameObject("Text (TMP)");
            textObj.transform.SetParent(buttonObj.transform, false);

            // Add RectTransform component to text
            RectTransform textRectTransform = textObj.AddComponent<RectTransform>();
            textRectTransform.anchorMin = Vector2.zero;
            textRectTransform.anchorMax = Vector2.one;
            textRectTransform.sizeDelta = Vector2.zero;

            // Add TextMeshProUGUI component
            TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "Track";
            text.color = Color.black;
            text.fontSize = 24;
            text.alignment = TextAlignmentOptions.Center;

            return buttonObj;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TrackSelectionManager] Error creating default track button: {e.Message}");
            return null;
        }
    }

    public void OnTrackButtonClicked(TrackData track)
    {
        selectedTrack = track;
        UpdateTrackPreview(track);
    }

    private void UpdateTrackPreview(TrackData track)
    {
        if (track == null)
        {
            // No track selected
            if (trackPreviewImage != null) trackPreviewImage.gameObject.SetActive(false);
            if (trackNameText != null) trackNameText.text = "Select a Track";
            if (trackDescriptionText != null) trackDescriptionText.text = "Please select a track from the list.";
            if (selectButton != null) selectButton.interactable = false;
        }
        else
        {
            // Track selected
            if (trackPreviewImage != null)
            {
                trackPreviewImage.gameObject.SetActive(true);
                trackPreviewImage.sprite = track.trackPreview;
            }
            if (trackNameText != null) trackNameText.text = track.trackName;
            if (trackDescriptionText != null) trackDescriptionText.text = track.trackDescription;
            if (selectButton != null) selectButton.interactable = true;
        }
    }

    private void OnBackButtonClicked()
    {
        // Return to main menu
        if (mainMenuManager != null)
        {
            mainMenuManager.ShowMainMenu();
        }
    }

    private void OnSelectButtonClicked()
    {
        if (selectedTrack != null)
        {
            // Load selected track
            if (mainMenuManager != null)
            {
                mainMenuManager.LoadTrack(selectedTrack.sceneName);
            }
        }
    }
}
