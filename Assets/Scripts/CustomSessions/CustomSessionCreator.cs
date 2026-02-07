using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using TMPro;
using System.Threading.Tasks;

/// <summary>
/// Custom Session Creator - Simplified version
/// Always 4 players, no dropdown
/// Shows join code in lobby instead of main menu
/// </summary>
public class CustomSessionCreator : MonoBehaviour
{
    [Header("UI References - Main Menu")]
    [SerializeField] private Button createButton;
    [SerializeField] private TMP_InputField sessionNameInput;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Session Settings")]
    [SerializeField] private string defaultSessionName = "New Game";
    [SerializeField] private int maxPlayers = 4; // Fixed at 4
    [SerializeField] private bool useRelay = true;

    [Header("Manager References")]
    [SerializeField] private SessionManagerComplete sessionManager;
    [SerializeField] private LobbyManagerCustomProfiles lobbyManager;

    // Store join code to be accessed by lobby
    private static string currentJoinCode = "";
    private bool isCreating = false;

    private void Start()
    {
        if (createButton != null)
        {
            createButton.onClick.AddListener(OnCreateSessionClicked);
        }

        // Auto-find managers if not assigned
        if (sessionManager == null)
        {
            sessionManager = FindObjectOfType<SessionManagerComplete>();
        }

        if (lobbyManager == null)
        {
            lobbyManager = FindObjectOfType<LobbyManagerCustomProfiles>();
        }
    }

    private async void OnCreateSessionClicked()
    {
        if (isCreating)
        {
            Debug.LogWarning("[SessionCreator] Already creating a session...");
            return;
        }

        isCreating = true;

        // Disable button during creation
        if (createButton != null)
        {
            createButton.interactable = false;
        }

        UpdateStatus("Creating session...");

        try
        {
            // Get session name from input
            string sessionName = GetSessionName();

            Debug.Log($"[SessionCreator] Creating session: {sessionName} (Max: {maxPlayers})");

            if (useRelay)
            {
                // Create with Relay (for internet play)
                await CreateSessionWithRelay();
            }
            else
            {
                // Create without Relay (local network only)
                CreateSessionLocal();
            }

            // Set session name in lobby
            if (lobbyManager != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                lobbyManager.SetSessionName(sessionName);
            }

            Debug.Log("[SessionCreator] Session created successfully!");
            UpdateStatus("Created!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SessionCreator] Failed to create session: {e.Message}");
            UpdateStatus($"Failed: {e.Message}");

            // Re-enable button on failure
            if (createButton != null)
            {
                createButton.interactable = true;
            }

            isCreating = false;
        }
    }

    /// <summary>
    /// Create session with Unity Relay (for internet play)
    /// </summary>
    private async Task CreateSessionWithRelay()
    {
        UpdateStatus("Allocating Relay...");

        // Create Relay allocation (maxPlayers - 1 because host counts as one)
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);

        Debug.Log($"[SessionCreator] Relay allocation created. Region: {allocation.Region}");

        // Get join code for others to join
        currentJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        Debug.Log($"[SessionCreator] Join Code: {currentJoinCode}");

        UpdateStatus("Starting server...");

        // Configure Unity Transport with Relay
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData
        );

        // Start as host
        bool started = NetworkManager.Singleton.StartHost();

        if (started)
        {
            UpdateStatus($"Created!");
            Debug.Log("[SessionCreator] Started as Host with Relay");
            // Join code will be displayed in lobby
        }
        else
        {
            throw new System.Exception("Failed to start host");
        }
    }

    /// <summary>
    /// Create session without Relay (local network only)
    /// </summary>
    private void CreateSessionLocal()
    {
        UpdateStatus("Starting local server...");

        // Generate a simple code for local sessions too
        currentJoinCode = GenerateSimpleCode();

        // Configure Unity Transport for local
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.ConnectionData.Address = "127.0.0.1";
        transport.ConnectionData.Port = 7777;

        // Start as host
        bool started = NetworkManager.Singleton.StartHost();

        if (started)
        {
            UpdateStatus("Created!");
            Debug.Log("[SessionCreator] Started as Host (Local)");
        }
        else
        {
            throw new System.Exception("Failed to start host");
        }
    }

    private string GenerateSimpleCode()
    {
        // Simple code for local sessions
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] code = new char[6];

        for (int i = 0; i < 6; i++)
        {
            code[i] = chars[Random.Range(0, chars.Length)];
        }

        return new string(code);
    }

    private string GetSessionName()
    {
        if (sessionNameInput != null && !string.IsNullOrEmpty(sessionNameInput.text))
        {
            return sessionNameInput.text;
        }

        // Generate default name
        string playerName = "Player";
        if (PlayerNameManagerFixed.Instance != null)
        {
            playerName = PlayerNameManagerFixed.Instance.GetLocalPlayerName();
        }

        return $"{playerName}'s Game";
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        Debug.Log($"[SessionCreator] Status: {message}");
    }

    /// <summary>
    /// Get the current join code (called by lobby to display it)
    /// </summary>
    public static string GetJoinCode()
    {
        return currentJoinCode;
    }

    /// <summary>
    /// Check if there's an active join code
    /// </summary>
    public static bool HasJoinCode()
    {
        return !string.IsNullOrEmpty(currentJoinCode);
    }

    /// <summary>
    /// Clear the join code (when leaving session)
    /// </summary>
    public static void ClearJoinCode()
    {
        currentJoinCode = "";
        Debug.Log("[SessionCreator] Cleared join code");
    }

    private void OnDestroy()
    {
        if (createButton != null)
        {
            createButton.onClick.RemoveListener(OnCreateSessionClicked);
        }
    }
}