using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent Camera Manager
/// Ensures a Main Camera always exists across scenes and network events
/// Prevents camera from being destroyed when players join/leave or during scene transitions
/// </summary>
public class PersistentCameraManager : MonoBehaviour
{
    public static PersistentCameraManager Instance { get; private set; }

    [Header("Camera Settings")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private AudioListener audioListener;
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Scene-Specific Cameras")]
    [SerializeField] private string[] scenesWithOwnCamera = { "Match" }; // Scenes that have their own camera

    private GameObject cameraObject;
    private bool isInSceneWithOwnCamera = false;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Debug.Log("[CameraManager] Another instance exists, destroying this one");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
            Debug.Log("[CameraManager] Camera set to DontDestroyOnLoad");
        }

        // Get references
        if (mainCamera == null)
        {
            mainCamera = GetComponentInChildren<Camera>();
        }

        if (audioListener == null)
        {
            audioListener = GetComponentInChildren<AudioListener>();
        }

        cameraObject = mainCamera != null ? mainCamera.gameObject : gameObject;

        // Subscribe to scene changes
        SceneManager.sceneLoaded += OnSceneLoaded;

        Debug.Log("[CameraManager] Initialized");
    }

    private void Start()
    {
        // Check current scene
        CheckCurrentScene();

        // Subscribe to network events
        SubscribeToNetworkEvents();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeFromNetworkEvents();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void SubscribeToNetworkEvents()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void UnsubscribeFromNetworkEvents()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[CameraManager] Scene loaded: {scene.name}");
        CheckCurrentScene();
        EnsureCameraExists();
    }

    private void CheckCurrentScene()
    {
        string currentScene = SceneManager.GetActiveScene().name;

        // Check if this scene should have its own camera
        isInSceneWithOwnCamera = false;
        foreach (string sceneName in scenesWithOwnCamera)
        {
            if (currentScene.Equals(sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                isInSceneWithOwnCamera = true;
                break;
            }
        }

        if (isInSceneWithOwnCamera)
        {
            Debug.Log($"[CameraManager] In scene with own camera: {currentScene}");
            DisablePersistentCamera();
        }
        else
        {
            Debug.Log($"[CameraManager] In scene using persistent camera: {currentScene}");
            EnablePersistentCamera();
        }
    }

    private void EnablePersistentCamera()
    {
        if (mainCamera != null)
        {
            mainCamera.enabled = true;
            mainCamera.gameObject.SetActive(true);
            Debug.Log("[CameraManager] Enabled persistent camera");
        }

        if (audioListener != null)
        {
            audioListener.enabled = true;
        }

        // Disable any other cameras in the scene
        DisableOtherCameras();
    }

    private void DisablePersistentCamera()
    {
        if (mainCamera != null)
        {
            mainCamera.enabled = false;
            // Don't deactivate the GameObject, just disable the camera component
            Debug.Log("[CameraManager] Disabled persistent camera (scene has its own)");
        }

        if (audioListener != null)
        {
            audioListener.enabled = false;
        }
    }

    private void DisableOtherCameras()
    {
        Camera[] allCameras = FindObjectsOfType<Camera>();

        foreach (Camera cam in allCameras)
        {
            // Don't disable our persistent camera
            if (cam == mainCamera)
                continue;

            // Disable other cameras
            if (cam.enabled)
            {
                Debug.Log($"[CameraManager] Disabling extra camera: {cam.gameObject.name}");
                cam.enabled = false;
            }
        }

        // Also check for extra audio listeners
        AudioListener[] allListeners = FindObjectsOfType<AudioListener>();

        foreach (AudioListener listener in allListeners)
        {
            if (listener == audioListener)
                continue;

            if (listener.enabled)
            {
                Debug.LogWarning($"[CameraManager] Disabling extra AudioListener on: {listener.gameObject.name}");
                listener.enabled = false;
            }
        }
    }

    private void EnsureCameraExists()
    {
        // If we're in a scene with its own camera, don't interfere
        if (isInSceneWithOwnCamera)
            return;

        // Make sure our camera is active
        if (mainCamera == null || !mainCamera.enabled)
        {
            Debug.LogWarning("[CameraManager] Main camera missing or disabled, re-enabling");
            EnablePersistentCamera();
        }

        // Check if there's ANY active camera in the scene
        Camera activeCamera = Camera.main;
        if (activeCamera == null)
        {
            Debug.LogError("[CameraManager] No active camera found! Re-enabling persistent camera");
            EnablePersistentCamera();
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[CameraManager] Client connected: {clientId}");

        // Ensure camera still exists after connection
        EnsureCameraExists();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[CameraManager] Client disconnected: {clientId}");

        // Ensure camera still exists after disconnection
        EnsureCameraExists();
    }

    /// <summary>
    /// Manually ensure camera exists (call this if you suspect it's missing)
    /// </summary>
    public void ForceEnsureCamera()
    {
        Debug.Log("[CameraManager] Forcing camera check");
        EnsureCameraExists();
    }

    /// <summary>
    /// Get the persistent camera
    /// </summary>
    public Camera GetCamera()
    {
        return mainCamera;
    }

    /// <summary>
    /// Check if camera is currently active
    /// </summary>
    public bool IsCameraActive()
    {
        return mainCamera != null && mainCamera.enabled;
    }

    [ContextMenu("Check Camera Status")]
    private void CheckCameraStatus()
    {
        Debug.Log("=== CAMERA STATUS ===");
        Debug.Log($"Persistent Camera exists: {mainCamera != null}");
        Debug.Log($"Persistent Camera enabled: {mainCamera != null && mainCamera.enabled}");
        Debug.Log($"Persistent Camera active: {mainCamera != null && mainCamera.gameObject.activeSelf}");
        Debug.Log($"Current scene: {SceneManager.GetActiveScene().name}");
        Debug.Log($"In scene with own camera: {isInSceneWithOwnCamera}");

        Camera mainCam = Camera.main;
        Debug.Log($"Camera.main exists: {mainCam != null}");
        if (mainCam != null)
        {
            Debug.Log($"Camera.main is on: {mainCam.gameObject.name}");
        }

        Camera[] allCameras = FindObjectsOfType<Camera>();
        Debug.Log($"Total cameras in scene: {allCameras.Length}");
        foreach (var cam in allCameras)
        {
            Debug.Log($"  - {cam.gameObject.name}: enabled={cam.enabled}, active={cam.gameObject.activeSelf}");
        }

        Debug.Log("====================");
    }
}