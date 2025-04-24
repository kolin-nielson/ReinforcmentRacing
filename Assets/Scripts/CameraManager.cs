using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple camera manager for switching between camera views.
/// </summary>
public class CameraManager : MonoBehaviour
{
    [System.Serializable]
    public class RaceCamera
    {
        public string cameraName;
        public Camera camera;
        public bool isActive;
    }

    [Header("Camera Setup")]
    [SerializeField] private List<RaceCamera> cameras = new List<RaceCamera>();
    [SerializeField] private int defaultCameraIndex = 0;

    [Header("Camera UI")]
    [SerializeField] private Button nextCameraButton;
    [SerializeField] private Button previousCameraButton;

    private int currentCameraIndex;

    private void Start()
    {
        // Initialize cameras
        for (int i = 0; i < cameras.Count; i++)
        {
            if (cameras[i].camera != null)
            {
                cameras[i].camera.gameObject.SetActive(i == defaultCameraIndex);
                cameras[i].isActive = (i == defaultCameraIndex);
            }
        }

        currentCameraIndex = defaultCameraIndex;

        // Add button listeners
        if (nextCameraButton != null) nextCameraButton.onClick.AddListener(SwitchToNextCamera);
        if (previousCameraButton != null) previousCameraButton.onClick.AddListener(SwitchToPreviousCamera);
    }

    private void Update()
    {
        // Handle keyboard input for camera switching
        if (Input.GetKeyDown(KeyCode.C))
        {
            SwitchToNextCamera();
        }
    }

    public void SwitchToNextCamera()
    {
        int nextIndex = (currentCameraIndex + 1) % cameras.Count;
        SwitchCamera(nextIndex);
    }

    public void SwitchToPreviousCamera()
    {
        int prevIndex = (currentCameraIndex - 1 + cameras.Count) % cameras.Count;
        SwitchCamera(prevIndex);
    }

    public void SwitchCamera(int index)
    {
        if (index < 0 || index >= cameras.Count)
            return;

        // Deactivate current camera
        if (currentCameraIndex >= 0 && currentCameraIndex < cameras.Count)
        {
            cameras[currentCameraIndex].camera.gameObject.SetActive(false);
            cameras[currentCameraIndex].isActive = false;
        }

        // Activate new camera
        cameras[index].camera.gameObject.SetActive(true);
        cameras[index].isActive = true;
        currentCameraIndex = index;
    }

    // Add a new camera to the list at runtime
    public void AddCamera(string name, Camera camera)
    {
        RaceCamera newCamera = new RaceCamera
        {
            cameraName = name,
            camera = camera,
            isActive = false
        };

        cameras.Add(newCamera);
    }
}
