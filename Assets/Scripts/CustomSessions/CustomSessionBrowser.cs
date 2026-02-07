using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using TMPro;
using System.Threading.Tasks;

/// <summary>
/// Custom Session Browser - Simplified version
/// Always visible, no show/hide panel logic
/// </summary>
public class CustomSessionBrowser : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button joinButton;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Settings")]
    [SerializeField] private bool useRelay = true; // Must match creator setting

    [Header("Manager References")]
    [SerializeField] private SessionManagerComplete sessionManager;
    [SerializeField] private LobbyManagerCustomProfiles lobbyManager;

    private bool isJoining = false;

    private void Start()
    {
        if (joinButton != null)
        {
            joinButton.onClick.AddListener(OnJoinButtonClicked);
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

    private async void OnJoinButtonClicked()
    {
        if (isJoining)
        {
            Debug.LogWarning("[SessionBrowser] Already joining a session...");
            return;
        }

        // Get join code from input
        string joinCode = joinCodeInput != null ? joinCodeInput.text.Trim().ToUpper() : "";

        if (string.IsNullOrEmpty(joinCode))
        {
            UpdateStatus("Please enter a join code!");
            return;
        }

        isJoining = true;

        // Disable button during join
        if (joinButton != null)
        {
            joinButton.interactable = false;
        }

        UpdateStatus("Joining...");
        Debug.Log($"[SessionBrowser] Attempting to join with code: {joinCode}");

        try
        {
            if (useRelay)
            {
                // Join with Relay
                await JoinSessionWithRelay(joinCode);
            }
            else
            {
                // Join local session
                JoinSessionLocal();
            }

            Debug.Log("[SessionBrowser] Joined session successfully!");
            UpdateStatus("Joined!");

            // Clear input after successful join
            if (joinCodeInput != null)
            {
                joinCodeInput.text = "";
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SessionBrowser] Failed to join session: {e.Message}");
            UpdateStatus($"Failed: Invalid code");

            // Re-enable button on failure
            if (joinButton != null)
            {
                joinButton.interactable = true;
            }

            isJoining = false;
        }
    }

    /// <summary>
    /// Join session using Unity Relay
    /// </summary>
    private async Task JoinSessionWithRelay(string joinCode)
    {
        UpdateStatus("Connecting to Relay...");

        // Join Relay allocation using join code
        JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

        Debug.Log($"[SessionBrowser] Joined Relay allocation. Region: {allocation.Region}");

        UpdateStatus("Connecting to host...");

        // Configure Unity Transport with Relay
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData,
            allocation.HostConnectionData
        );

        // Start as client
        bool started = NetworkManager.Singleton.StartClient();

        if (started)
        {
            UpdateStatus("Connected!");
            Debug.Log("[SessionBrowser] Started as Client with Relay");
            // SessionManager will detect connection and show lobby
        }
        else
        {
            throw new System.Exception("Failed to start client");
        }
    }

    /// <summary>
    /// Join local session (without Relay)
    /// </summary>
    private void JoinSessionLocal()
    {
        UpdateStatus("Connecting...");

        // Configure Unity Transport for local
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.ConnectionData.Address = "127.0.0.1";
        transport.ConnectionData.Port = 7777;

        // Start as client
        bool started = NetworkManager.Singleton.StartClient();

        if (started)
        {
            UpdateStatus("Connected!");
            Debug.Log("[SessionBrowser] Started as Client (Local)");
            // SessionManager will detect connection and show lobby
        }
        else
        {
            throw new System.Exception("Failed to start client");
        }
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        if (!string.IsNullOrEmpty(message))
        {
            Debug.Log($"[SessionBrowser] Status: {message}");
        }
    }

    private void OnDestroy()
    {
        if (joinButton != null)
        {
            joinButton.onClick.RemoveListener(OnJoinButtonClicked);
        }
    }
}