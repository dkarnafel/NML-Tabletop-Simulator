using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Complete MatchDisconnectUI with all callback methods
/// Handles disconnecting from match scene and returning to main menu
/// </summary>
public class MatchDisconnectUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button hostDisconnectButton;
    [SerializeField] private Button clientDisconnectButton;

    [Header("Scenes")]
    [SerializeField] private string mainMenuSceneName = "Main menu";

    private bool isReturningToMenu = false;

    private void Start()
    {
        SetupButtons();
        SubscribeToNetworkEvents();
    }

    private void SetupButtons()
    {
        var nm = NetworkManager.Singleton;
        bool isHost = nm != null && (nm.IsHost || nm.IsServer);
        bool isClient = nm != null && nm.IsClient && !nm.IsHost;

        // Setup host disconnect button
        if (hostDisconnectButton != null)
        {
            hostDisconnectButton.gameObject.SetActive(isHost);
            if (isHost)
            {
                hostDisconnectButton.onClick.AddListener(OnDisconnectClicked);
            }
        }

        // Setup client disconnect button
        if (clientDisconnectButton != null)
        {
            clientDisconnectButton.gameObject.SetActive(isClient);
            if (isClient)
            {
                clientDisconnectButton.onClick.AddListener(OnDisconnectClicked);
            }
        }
    }

    private void SubscribeToNetworkEvents()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            // Subscribe to network callbacks
            nm.OnClientStopped += OnClientStopped;
            nm.OnServerStopped += OnServerStopped;

            Debug.Log("[MatchDisconnectUI] Subscribed to network events");
        }
        else
        {
            Debug.LogWarning("[MatchDisconnectUI] NetworkManager not found!");
        }
    }

    private void OnDestroy()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            // Unsubscribe from network callbacks
            nm.OnClientStopped -= OnClientStopped;
            nm.OnServerStopped -= OnServerStopped;
        }

        // Cleanup button listeners
        if (hostDisconnectButton != null)
        {
            hostDisconnectButton.onClick.RemoveListener(OnDisconnectClicked);
        }
        if (clientDisconnectButton != null)
        {
            clientDisconnectButton.onClick.RemoveListener(OnDisconnectClicked);
        }
    }

    /// <summary>
    /// Called when user clicks disconnect button
    /// </summary>
    private void OnDisconnectClicked()
    {
        Debug.Log("[MatchDisconnectUI] Disconnect button clicked");

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            Debug.Log("[MatchDisconnectUI] Shutting down NetworkManager");
            nm.Shutdown();
        }
        else
        {
            // If already disconnected, just go to menu
            ReturnToMainMenu();
        }
    }

    /// <summary>
    /// ★ CALLBACK: Called when server stops (host side)
    /// This method MUST exist for the subscription to work!
    /// </summary>
    private void OnServerStopped(bool wasCleanShutdown)
    {
        Debug.Log($"[MatchDisconnectUI] Server stopped. Clean={wasCleanShutdown}");
        ReturnToMainMenu();
    }

    /// <summary>
    /// ★ CALLBACK: Called when client stops (client side)
    /// This method MUST exist for the subscription to work!
    /// </summary>
    private void OnClientStopped(bool wasCleanShutdown)
    {
        Debug.Log($"[MatchDisconnectUI] Client stopped. Clean={wasCleanShutdown}");
        ReturnToMainMenu();
    }

    /// <summary>
    /// Return to main menu - only runs once
    /// </summary>
    private void ReturnToMainMenu()
    {
        if (isReturningToMenu)
        {
            Debug.Log("[MatchDisconnectUI] Already returning to menu, skipping");
            return;
        }

        isReturningToMenu = true;
        Debug.Log("[MatchDisconnectUI] Starting return to main menu");

        StartCoroutine(ReturnToMainMenuCoroutine());
    }

    private IEnumerator ReturnToMainMenuCoroutine()
    {
        var nm = NetworkManager.Singleton;

        // Ensure camera persists
        EnsureCameraPersists();

        // Ensure NetworkManager is shut down
        if (nm != null && nm.IsListening)
        {
            Debug.Log("[MatchDisconnectUI] Forcing NetworkManager shutdown");
            nm.Shutdown();
            yield return new WaitForSeconds(0.3f);
        }

        // Clear join code from previous session
        if (CustomSessionCreator.HasJoinCode())
        {
            CustomSessionCreator.ClearJoinCode();
            Debug.Log("[MatchDisconnectUI] Cleared old join code");
        }

        // Notify SessionManager we've left (if it exists)
        var sessionManager = FindObjectOfType<SessionManagerComplete>();
        if (sessionManager != null)
        {
            Debug.Log("[MatchDisconnectUI] Notifying SessionManager of disconnect");
            sessionManager.OnLeaveSessionButtonClicked();
        }

        // Small delay to ensure everything is cleaned up
        yield return new WaitForSeconds(0.2f);

        // Ensure camera persists before loading
        EnsureCameraPersists();

        // Load main menu scene
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogWarning("[MatchDisconnectUI] Main menu scene name not set!");
            SceneManager.LoadScene(0);
        }
        else
        {
            Debug.Log($"[MatchDisconnectUI] Loading main menu: {mainMenuSceneName}");
            SceneManager.LoadScene(mainMenuSceneName);
        }

        // Ensure camera persists after loading
        yield return new WaitForSeconds(0.5f);
        EnsureCameraPersists();
    }

    /// <summary>
    /// Ensure camera persists through disconnects
    /// </summary>
    private void EnsureCameraPersists()
    {
        // Use PersistentCameraManager if available
        if (PersistentCameraManager.Instance != null)
        {
            Debug.Log("[MatchDisconnectUI] Using PersistentCameraManager");
            PersistentCameraManager.Instance.ForceEnsureCamera();
            return;
        }

        // Fallback: manually protect main camera
        Debug.LogWarning("[MatchDisconnectUI] PersistentCameraManager not found, using fallback");

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Debug.Log($"[MatchDisconnectUI] Protecting camera: {mainCam.gameObject.name}");
            DontDestroyOnLoad(mainCam.gameObject);
        }
        else
        {
            Debug.LogError("[MatchDisconnectUI] No main camera found!");
        }
    }

    /// <summary>
    /// Cleanup on application quit
    /// </summary>
    private void OnApplicationQuit()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            Debug.Log("[MatchDisconnectUI] Shutting down on application quit");
            nm.Shutdown();
        }
    }

    /// <summary>
    /// Debug method to test disconnect
    /// </summary>
    [ContextMenu("Test Disconnect")]
    private void TestDisconnect()
    {
        OnDisconnectClicked();
    }
}