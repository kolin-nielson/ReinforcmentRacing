using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Sets up the main menu scene with UI elements and track data.
/// </summary>
public class MainMenuSetup : MonoBehaviour
{
    [Header("UI Prefabs")]
    [SerializeField] private GameObject canvasPrefab;
    [SerializeField] private GameObject mainMenuPanelPrefab;
    [SerializeField] private GameObject trackSelectionPanelPrefab;
    [SerializeField] private GameObject loadingPanelPrefab;
    [SerializeField] private GameObject trackButtonPrefab;

    [Header("Track Data")]
    [SerializeField] private List<TrackData> trackData = new List<TrackData>();

    private void Awake()
    {
        // Create UI if it doesn't exist
        if (FindObjectOfType<Canvas>() == null)
        {
            CreateMainMenuUI();
        }
    }

    private void CreateMainMenuUI()
    {
        // Create canvas
        GameObject canvasObj = Instantiate(canvasPrefab);
        canvasObj.name = "MainMenuCanvas";

        // Create main menu panel
        GameObject mainMenuPanel = Instantiate(mainMenuPanelPrefab, canvasObj.transform);
        mainMenuPanel.name = "MainMenuPanel";

        // Create track selection panel
        GameObject trackSelectionPanel = Instantiate(trackSelectionPanelPrefab, canvasObj.transform);
        trackSelectionPanel.name = "TrackSelectionPanel";
        trackSelectionPanel.SetActive(false);

        // Create loading panel
        GameObject loadingPanel = Instantiate(loadingPanelPrefab, canvasObj.transform);
        loadingPanel.name = "LoadingPanel";
        loadingPanel.SetActive(false);

        // Add MainMenuManager component
        MainMenuManager menuManager = canvasObj.AddComponent<MainMenuManager>();

        // Set references
        menuManager.SetMainMenuPanel(mainMenuPanel);
        menuManager.SetTrackSelectionPanel(trackSelectionPanel);
        menuManager.SetLoadingPanel(loadingPanel);
        menuManager.SetTrackButtonPrefab(trackButtonPrefab);

        // Set track data
        menuManager.SetTrackData(trackData);
    }
}
