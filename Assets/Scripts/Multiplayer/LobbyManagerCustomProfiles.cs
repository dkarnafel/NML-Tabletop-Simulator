using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// COMPLETE VERSION - Includes PlayerReadyInfo struct
/// Lobby Manager for Custom PlayerProfile Prefabs
/// Manages player list in a scroll view with vertical layout
/// </summary>
public class LobbyManagerCustomProfiles : NetworkBehaviour
{
    [Header("Panel References")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Lobby UI Elements")]
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private TextMeshProUGUI readyButtonText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI readyCountText;

    [Header("Player Profile List")]
    [SerializeField] private Transform playerListContainer;  // The Content object of your ScrollView
    [SerializeField] private GameObject playerProfilePrefab; // Your PlayerProfile prefab

    [Header("Scene Settings")]
    [SerializeField] private string matchSceneName = "Match";

    [Header("Button Colors")]
    [SerializeField] private Color readyColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color notReadyColor = new Color(0.8f, 0.2f, 0.2f);

    [Header("Session Info Display")]
    [SerializeField] private TextMeshProUGUI sessionNameText;
    [SerializeField] private string defaultSessionName = "Game Lobby";

    private NetworkVariable<FixedString64Bytes> sessionName =
     new NetworkVariable<FixedString64Bytes>(
         new FixedString64Bytes("Game Lobby"),
         NetworkVariableReadPermission.Everyone,
         NetworkVariableWritePermission.Server
     );

    // Network synchronized ready states
    private NetworkList<PlayerReadyInfo> playersReadyState;

    // Local player state
    private bool isLocalPlayerReady = false;

    // Dictionary to track spawned player profile items
    private Dictionary<ulong, PlayerProfileItem> playerProfiles = new Dictionary<ulong, PlayerProfileItem>();

    private void Awake()
    {
        playersReadyState = new NetworkList<PlayerReadyInfo>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Subscribe to network list changes
        playersReadyState.OnListChanged += OnPlayersReadyStateChanged;

        // Subscribe to player name changes
        if (PlayerNameManagerFixed.Instance != null)
        {
            PlayerNameManagerFixed.Instance.OnPlayerNamesChanged += UpdateAllPlayerProfiles;
        }

        // Setup UI button listeners
        SetupButtons();

        // If this is the server, set up client management
        if (IsServer)
        {
            // Add all connected clients
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                AddPlayerToReadyList(clientId);
            }

            // Listen for new clients connecting
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        sessionName.OnValueChanged += (o, n) => UpdateSessionInfo();

        UpdateUI();
        RebuildPlayerList();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (playersReadyState != null)
        {
            playersReadyState.OnListChanged -= OnPlayersReadyStateChanged;
        }

        if (PlayerNameManagerFixed.Instance != null)
        {
            PlayerNameManagerFixed.Instance.OnPlayerNamesChanged -= UpdateAllPlayerProfiles;
        }

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        CleanupButtons();
    }

    private void SetupButtons()
    {
        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyButtonClicked);
            UpdateReadyButton(false);
        }

        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnStartGameButtonClicked);
            startGameButton.gameObject.SetActive(false);
        }
    }

    private void CleanupButtons()
    {
        if (readyButton != null)
        {
            readyButton.onClick.RemoveListener(OnReadyButtonClicked);
        }

        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveListener(OnStartGameButtonClicked);
        }
    }

    public void SetSessionName(string name)
    {
        if (IsServer)
        {
            sessionName.Value = new FixedString64Bytes(name);
        }
    }

    private void UpdateSessionInfo()
    {
        if (sessionNameText != null)
        {
            sessionNameText.text = sessionName.Value.ToString();
        }
    }

    public void OnSessionJoined()
    {
        Debug.Log("Session joined - showing lobby panel");

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (lobbyPanel != null)
            lobbyPanel.SetActive(true);

        UpdateSessionInfo();
        UpdateUI();
        RebuildPlayerList();
    }

    public void OnSessionLeft()
    {
        Debug.Log("Session left - showing main menu");

        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        // Reset local ready state
        isLocalPlayerReady = false;
        if (readyButton != null)
        {
            UpdateReadyButton(false);
        }

        // Clear player list
        ClearPlayerList();
        // IMPORTANT: Clear the NetworkList on server
        if (IsServer && playersReadyState != null)
        {
            playersReadyState.Clear();
            Debug.Log("Cleared players ready state");
        }
    }

    private void OnReadyButtonClicked()
    {
        ToggleReady();
    }

    private void ToggleReady()
    {
        isLocalPlayerReady = !isLocalPlayerReady;

        Debug.Log($"Local player ready state: {isLocalPlayerReady}");

        // Update button immediately for responsiveness
        UpdateReadyButton(isLocalPlayerReady);

        // Send to server
        SetPlayerReadyServerRpc(NetworkManager.Singleton.LocalClientId, isLocalPlayerReady);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerReadyServerRpc(ulong clientId, bool ready)
    {
        Debug.Log($"Server: Setting player {clientId} ready state to {ready}");

        // Find and update the player's ready state
        for (int i = 0; i < playersReadyState.Count; i++)
        {
            if (playersReadyState[i].clientId == clientId)
            {
                var info = playersReadyState[i];
                info.isReady = ready;
                playersReadyState[i] = info;
                return;
            }
        }

        Debug.LogWarning($"Player {clientId} not found in ready list");
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Server: Client {clientId} connected");
        AddPlayerToReadyList(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Server: Client {clientId} disconnected");
        RemovePlayerFromReadyList(clientId);
    }

    private void AddPlayerToReadyList(ulong clientId)
    {
        if (!IsServer) return;

        // Check if player already exists
        foreach (var player in playersReadyState)
        {
            if (player.clientId == clientId)
                return;
        }

        // Add new player
        playersReadyState.Add(new PlayerReadyInfo
        {
            clientId = clientId,
            isReady = false
        });

        Debug.Log($"Added player {clientId} to ready list. Total players: {playersReadyState.Count}");
    }

    private void RemovePlayerFromReadyList(ulong clientId)
    {
        if (!IsServer) return;

        for (int i = playersReadyState.Count - 1; i >= 0; i--)
        {
            if (playersReadyState[i].clientId == clientId)
            {
                playersReadyState.RemoveAt(i);
                Debug.Log($"Removed player {clientId} from ready list");
                break;
            }
        }
    }

    private void OnPlayersReadyStateChanged(NetworkListEvent<PlayerReadyInfo> changeEvent)
    {
        Debug.Log($"Ready state changed. Event type: {changeEvent.Type}");
        UpdateUI();
        RebuildPlayerList();
        CheckAllPlayersReady();
    }

    private void CheckAllPlayersReady()
    {
        if (!IsServer) return;

        int totalPlayers = playersReadyState.Count;
        int readyCount = 0;

        foreach (var player in playersReadyState)
        {
            if (player.isReady)
                readyCount++;
        }

        Debug.Log($"Ready check: {readyCount}/{totalPlayers} players ready");

        // All players must be ready and there must be at least 1 player
        bool allReady = totalPlayers > 0 && readyCount == totalPlayers;

        // Show/hide start button
        UpdateStartButtonClientRpc(allReady);
    }

    [ClientRpc]
    private void UpdateStartButtonClientRpc(bool show)
    {
        if (startGameButton != null && IsHost)
        {
            startGameButton.gameObject.SetActive(show);
            Debug.Log($"Start button visibility: {show}");
        }
    }

    private void UpdateUI()
    {
        int totalPlayers = playersReadyState.Count;
        int readyCount = 0;

        foreach (var player in playersReadyState)
        {
            if (player.isReady)
                readyCount++;
        }

        if (playerCountText != null)
        {
            playerCountText.text = $"Players: {totalPlayers}";
        }

        if (readyCountText != null)
        {
            readyCountText.text = $"Ready: {readyCount}/{totalPlayers}";
        }
    }

    private void UpdateReadyButton(bool isReady)
    {
        if (readyButton == null) return;

        if (readyButtonText != null)
        {
            readyButtonText.text = isReady ? "Not Ready" : "Ready";
        }

        // Update button color
        var colors = readyButton.colors;
        colors.normalColor = isReady ? notReadyColor : readyColor;
        colors.highlightedColor = isReady ? notReadyColor * 1.2f : readyColor * 1.2f;
        readyButton.colors = colors;
    }

    /// <summary>
    /// Rebuild the entire player list - creates PlayerProfile for each player
    /// </summary>
    private void RebuildPlayerList()
    {
        if (playerListContainer == null || playerProfilePrefab == null)
        {
            Debug.LogWarning("Player list container or prefab not assigned!");
            return;
        }

        // Clear existing profiles
        ClearPlayerList();

        // Create a profile for each player
        foreach (var playerInfo in playersReadyState)
        {
            CreatePlayerProfile(playerInfo);
        }
    }

    /// <summary>
    /// Create a single player profile UI element
    /// </summary>
    private void CreatePlayerProfile(PlayerReadyInfo playerInfo)
    {
        if (playerListContainer == null || playerProfilePrefab == null) return;

        // Instantiate the profile
        GameObject profileObj = Instantiate(playerProfilePrefab, playerListContainer);
        PlayerProfileItem profile = profileObj.GetComponent<PlayerProfileItem>();

        if (profile != null)
        {
            // Initialize with client ID
            profile.Initialize(playerInfo.clientId);
            profile.SetReady(playerInfo.isReady);

            // Store reference
            playerProfiles[playerInfo.clientId] = profile;

            Debug.Log($"Created profile for player {playerInfo.clientId}");
        }
        else
        {
            Debug.LogError("PlayerProfile prefab is missing PlayerProfileItem component!");
            Destroy(profileObj);
        }
    }

    /// <summary>
    /// Update all existing player profiles (called when names change)
    /// </summary>
    private void UpdateAllPlayerProfiles()
    {
        // Just rebuild the list to ensure everything is in sync
        RebuildPlayerList();
    }

    /// <summary>
    /// Clear all player profiles from the list
    /// </summary>
    private void ClearPlayerList()
    {
        foreach (var profile in playerProfiles.Values)
        {
            if (profile != null && profile.gameObject != null)
            {
                Destroy(profile.gameObject);
            }
        }

        playerProfiles.Clear();
    }

    private void OnStartGameButtonClicked()
    {
        if (!IsHost)
        {
            Debug.LogWarning("Only the host can start the game!");
            return;
        }

        Debug.Log("Starting game...");
        StartGameServerRpc();
    }

    [ServerRpc]
    private void StartGameServerRpc()
    {
        Debug.Log($"Loading match scene: {matchSceneName}");

        // Use Netcode's scene manager to load the match scene for all clients
        NetworkManager.Singleton.SceneManager.LoadScene(matchSceneName,
            UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}

/// <summary>
/// Data structure for storing player ready information
/// IMPORTANT: This struct must be defined for NetworkList to work!
/// </summary>
public struct PlayerReadyInfo : INetworkSerializable, System.IEquatable<PlayerReadyInfo>
{
    public ulong clientId;
    public bool isReady;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref isReady);
    }

    public bool Equals(PlayerReadyInfo other)
    {
        return clientId == other.clientId && isReady == other.isReady;
    }

}