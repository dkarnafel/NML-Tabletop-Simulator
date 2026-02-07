using UnityEngine;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;
using System;
using System.Collections;

/// <summary>
/// SessionManager with proper Multiplayer Widget cleanup
/// Prevents ghost sessions by leaving widget session before NetworkManager shutdown
/// </summary>
public class SessionManagerComplete : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LobbyManagerCustomProfiles lobbyManager;

    [Header("Leave Session Widget")]
    [SerializeField] private GameObject leaveSessionWidget;

    private bool isInitialized = false;
    private bool hasJoinedSession = false;
    private bool isCleaningUp = false;

    private void Awake()
    {
        // Auto-find lobby manager if reference is lost
        if (lobbyManager == null)
        {
            lobbyManager = FindObjectOfType<LobbyManagerCustomProfiles>();
            if (lobbyManager != null)
            {
                Debug.Log($"Auto-found LobbyManager on: {lobbyManager.gameObject.name}");
            }
        }
    }

    private async void Start()
    {
        // Safety check - ensure NetworkManager is clean
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsConnectedClient ||
                NetworkManager.Singleton.ShutdownInProgress)
            {
                Debug.LogWarning("NetworkManager in bad state on start, forcing cleanup...");
                NetworkManager.Singleton.Shutdown();
                await Task.Delay(500);
            }
        }

        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                Debug.Log("Unity Services already initialized");
                isInitialized = true;
                SetupNetworkCallbacks();
                return;
            }

            Debug.Log("Initializing Unity Services...");
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("Signing in anonymously...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"Signed in as: {AuthenticationService.Instance.PlayerId}");
            }

            isInitialized = true;
            SetupNetworkCallbacks();
            Debug.Log("Unity Services initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
        }
    }

    private void SetupNetworkCallbacks()
    {
        if (NetworkManager.Singleton != null)
        {
            // Remove old callbacks first (prevents duplicates)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnNetworkClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnNetworkClientDisconnected;

            // Add callbacks
            NetworkManager.Singleton.OnClientConnectedCallback += OnNetworkClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnNetworkClientDisconnected;

            Debug.Log("Network callbacks registered");
        }
        else
        {
            Debug.LogWarning("NetworkManager.Singleton is null in SetupNetworkCallbacks");
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnNetworkClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnNetworkClientDisconnected;
        }
    }

    private void OnNetworkClientConnected(ulong clientId)
    {
        Debug.Log($"Network client connected: {clientId}");

        // If this is the local client connecting
        if (NetworkManager.Singleton != null &&
            clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Local client connected to session - preparing to show lobby");

            // Make sure player name is submitted FIRST
            if (PlayerNameManagerFixed.Instance != null)
            {
                string playerName = PlayerNameManagerFixed.Instance.GetLocalPlayerName();
                Debug.Log($"Local player name before joining lobby: {playerName}");
            }

            hasJoinedSession = true;
            StartCoroutine(ShowLobbyAfterDelay());
        }
    }

    private IEnumerator ShowLobbyAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsConnectedClient &&
            !isCleaningUp)
        {
            Debug.Log("Triggering lobby display");

            if (lobbyManager != null)
            {
                // Set session name if server
                if (NetworkManager.Singleton.IsServer)
                {
                    string playerName = "Player";
                    if (PlayerNameManagerFixed.Instance != null)
                    {
                        playerName = PlayerNameManagerFixed.Instance.GetLocalPlayerName();
                    }
                    lobbyManager.SetSessionName($"{playerName}'s Game");
                }

                lobbyManager.OnSessionJoined();
                Debug.Log("Lobby OnSessionJoined called successfully");
            }
            else
            {
                Debug.LogError("LobbyManager reference is null!");
            }
        }
    }

    private void OnNetworkClientDisconnected(ulong clientId)
    {
        Debug.Log($"Network client disconnected: {clientId}");

        // If the local client disconnected
        if (NetworkManager.Singleton != null &&
            clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.Log("Local client disconnected from session");
            if (!isCleaningUp)
            {
                CleanupSession();
            }
        }
    }

    /// <summary>
    /// Call this when user clicks Leave Session button
    /// </summary>
    public void OnLeaveSessionButtonClicked()
    {
        Debug.Log("Leave session button clicked");
        CleanupSession();
    }

    private void CleanupSession()
    {
        if (isCleaningUp)
        {
            Debug.Log("Already cleaning up, skipping...");
            return;
        }

        isCleaningUp = true;
        Debug.Log("=== Starting Session Cleanup ===");

        StartCoroutine(CleanupSessionCoroutine());
    }

    /// <summary>
    /// CRITICAL: Leave widget session BEFORE NetworkManager shutdown
    /// This prevents ghost sessions from appearing in the session list
    /// </summary>
    private IEnumerator CleanupSessionCoroutine()
    {
        // STEP 1: Leave Multiplayer Widget session FIRST!
        yield return StartCoroutine(LeaveMultiplayerWidgetSession());

        // STEP 2: Shutdown NetworkManager
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsListening)
            {
                Debug.Log("Shutting down NetworkManager...");
                NetworkManager.Singleton.Shutdown();
            }
            else
            {
                Debug.Log("NetworkManager already shut down");
            }
        }

        // STEP 3: Reset session state
        hasJoinedSession = false;

        // STEP 4: Show main menu
        OnSessionLeft();

        // STEP 5: Wait for cleanup to complete
        yield return StartCoroutine(CompleteCleanup());
    }

    /// <summary>
    /// Leave the Multiplayer Widget session properly
    /// MUST be called BEFORE NetworkManager.Shutdown()
    /// </summary>
    private IEnumerator LeaveMultiplayerWidgetSession()
    {
        Debug.Log("Looking for Multiplayer Widget SessionManager...");

        // Find the widget's SessionManager
        // NOTE: This assumes the widget uses the default namespace/class name
        // Adjust if your widget setup is different
        var widgetSessionManagers = FindObjectsOfType<MonoBehaviour>();

        foreach (var component in widgetSessionManagers)
        {
            // Check if this is the SessionManager from Multiplayer Widgets
            if (component.GetType().FullName == "Unity.Multiplayer.Widgets.SessionManager")
            {
                Debug.Log("Found Multiplayer Widget SessionManager, leaving session...");

                // Call LeaveSession method via reflection (since we don't have direct reference)
                var method = component.GetType().GetMethod("LeaveSession");
                if (method != null)
                {
                    method.Invoke(component, null);
                    Debug.Log("Called LeaveSession on widget");

                    // Wait for widget to process the leave
                    yield return new WaitForSeconds(0.5f);
                    break;
                }
            }
        }

        Debug.Log("Widget session leave complete");
    }

    private IEnumerator CompleteCleanup()
    {
        // Wait for NetworkManager to fully shutdown
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.5f);

        // Re-setup callbacks for next session
        if (NetworkManager.Singleton != null)
        {
            SetupNetworkCallbacks();
        }

        isCleaningUp = false;

        Debug.Log("=== Session Cleanup Complete ===");
        Debug.Log("✅ Ready to create/join new session");
    }

    private void OnSessionLeft()
    {
        if (lobbyManager != null)
        {
            lobbyManager.OnSessionLeft();
            Debug.Log("Returned to main menu after leaving session");
        }
    }

    /// <summary>
    /// IMPORTANT: Cleanup on application quit to prevent ghost sessions
    /// </summary>
    private void OnApplicationQuit()
    {
        Debug.Log("Application closing - cleaning up session to prevent ghost sessions");

        // Synchronously leave widget session
        var widgetSessionManagers = FindObjectsOfType<MonoBehaviour>();
        foreach (var component in widgetSessionManagers)
        {
            if (component.GetType().FullName == "Unity.Multiplayer.Widgets.SessionManager")
            {
                var method = component.GetType().GetMethod("LeaveSession");
                if (method != null)
                {
                    method.Invoke(component, null);
                    Debug.Log("Left widget session on quit");
                }
                break;
            }
        }

        // Shutdown NetworkManager
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
    }

    /// <summary>
    /// Manual trigger for showing lobby (for debugging)
    /// </summary>
    public void ForceShowLobby()
    {
        if (lobbyManager != null)
        {
            lobbyManager.OnSessionJoined();
        }
    }

    /// <summary>
    /// Debug method to force cleanup
    /// </summary>
    [ContextMenu("Force Cleanup")]
    public void ForceCleanup()
    {
        Debug.Log("Force cleanup triggered manually");
        CleanupSession();
    }

    /// <summary>
    /// Check current state
    /// </summary>
    [ContextMenu("Check State")]
    private void CheckState()
    {
        Debug.Log("=== Session Manager State ===");
        Debug.Log($"Is Initialized: {isInitialized}");
        Debug.Log($"Has Joined Session: {hasJoinedSession}");
        Debug.Log($"Is Cleaning Up: {isCleaningUp}");

        if (NetworkManager.Singleton != null)
        {
            Debug.Log($"NetworkManager.IsConnectedClient: {NetworkManager.Singleton.IsConnectedClient}");
            Debug.Log($"NetworkManager.IsListening: {NetworkManager.Singleton.IsListening}");
            Debug.Log($"NetworkManager.IsHost: {NetworkManager.Singleton.IsHost}");
            Debug.Log($"NetworkManager.IsServer: {NetworkManager.Singleton.IsServer}");
        }
        else
        {
            Debug.Log("NetworkManager.Singleton is NULL");
        }

        Debug.Log("============================");
    }
}