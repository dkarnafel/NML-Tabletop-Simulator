using Unity.Collections;
using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Enhanced Player Name Manager with persistent storage
/// Saves player names between sessions using PlayerPrefs
/// Automatically loads saved name on start
/// </summary>
public class PlayerNameManagerFixed : NetworkBehaviour
{
    public static PlayerNameManagerFixed Instance { get; private set; }

    [Header("Name Settings")]
    [SerializeField] private string defaultPlayerName = "Player";
    [SerializeField] private int maxNameLength = 20;

    [Header("UI References (Optional - for auto-fill)")]
    [SerializeField] private TMP_InputField playerNameInput;

    // Network synced dictionary of player names
    private NetworkList<PlayerNameEntry> playerNames;

    // Local cache for quick lookups
    private Dictionary<ulong, string> nameCache = new Dictionary<ulong, string>();

    // Events
    public event System.Action OnPlayerNamesChanged;

    // PlayerPrefs key for saving name
    private const string SAVED_NAME_KEY = "SavedPlayerName";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        playerNames = new NetworkList<PlayerNameEntry>();

        Debug.Log("[PlayerNameManager] Initialized");
    }

    private void Start()
    {
        // Load saved player name
        LoadSavedPlayerName();

        // If we have a name input field, pre-fill it with saved name
        if (playerNameInput != null)
        {
            string savedName = GetSavedPlayerName();
            if (!string.IsNullOrEmpty(savedName))
            {
                playerNameInput.text = savedName;
                Debug.Log($"[PlayerNameManager] Pre-filled input with saved name: {savedName}");
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        playerNames.OnListChanged += OnPlayerNamesListChanged;

        // If this is the local client, submit their name
        if (IsClient)
        {
            string playerName = GetLocalPlayerName();
            Debug.Log($"[PlayerNameManager] Submitting name on spawn: {playerName}");
            SubmitPlayerNameServerRpc(NetworkManager.Singleton.LocalClientId, playerName);
        }

        Debug.Log("[PlayerNameManager] Network spawned");
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (playerNames != null)
        {
            playerNames.OnListChanged -= OnPlayerNamesListChanged;
        }
    }

    /// <summary>
    /// Get the local player's name (from saved data or input field)
    /// </summary>
    public string GetLocalPlayerName()
    {
        // First, check if we have a name input field with text
        if (playerNameInput != null && !string.IsNullOrEmpty(playerNameInput.text))
        {
            string inputName = playerNameInput.text.Trim();
            if (!string.IsNullOrEmpty(inputName))
            {
                // Save this name for future use
                SavePlayerName(inputName);
                return inputName;
            }
        }

        // Try to load saved name
        string savedName = GetSavedPlayerName();
        if (!string.IsNullOrEmpty(savedName))
        {
            return savedName;
        }

        // Fall back to default
        return defaultPlayerName;
    }

    /// <summary>
    /// Get a player's name by their client ID
    /// </summary>
    public string GetPlayerName(ulong clientId)
    {
        // Check cache first
        if (nameCache.TryGetValue(clientId, out string cachedName))
        {
            return cachedName;
        }

        // Search network list
        foreach (var entry in playerNames)
        {
            if (entry.clientId == clientId)
            {
                nameCache[clientId] = entry.playerName.ToString();
                return entry.playerName.ToString();
            }
        }

        return $"Player {clientId}";
    }

    /// <summary>
    /// Set the local player's name (saves it and updates network)
    /// </summary>
    public void SetLocalPlayerName(string newName)
    {
        if (string.IsNullOrEmpty(newName))
        {
            Debug.LogWarning("[PlayerNameManager] Cannot set empty name");
            return;
        }

        // Trim and limit length
        newName = newName.Trim();
        if (newName.Length > maxNameLength)
        {
            newName = newName.Substring(0, maxNameLength);
        }

        Debug.Log($"[PlayerNameManager] Setting local player name to: {newName}");

        // Save locally
        SavePlayerName(newName);

        // Update input field if we have one
        if (playerNameInput != null)
        {
            playerNameInput.text = newName;
        }

        // If we're in a session, update on network
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            SubmitPlayerNameServerRpc(NetworkManager.Singleton.LocalClientId, newName);
        }
    }

    /// <summary>
    /// Save player name to PlayerPrefs
    /// </summary>
    private void SavePlayerName(string playerName)
    {
        PlayerPrefs.SetString(SAVED_NAME_KEY, playerName);
        PlayerPrefs.Save();
        Debug.Log($"[PlayerNameManager] Saved player name: {playerName}");
    }

    /// <summary>
    /// Load player name from PlayerPrefs
    /// </summary>
    private void LoadSavedPlayerName()
    {
        if (PlayerPrefs.HasKey(SAVED_NAME_KEY))
        {
            string savedName = PlayerPrefs.GetString(SAVED_NAME_KEY);
            Debug.Log($"[PlayerNameManager] Loaded saved name: {savedName}");
        }
        else
        {
            Debug.Log("[PlayerNameManager] No saved name found");
        }
    }

    /// <summary>
    /// Get saved player name from PlayerPrefs
    /// </summary>
    public string GetSavedPlayerName()
    {
        return PlayerPrefs.GetString(SAVED_NAME_KEY, "");
    }

    /// <summary>
    /// Check if a player name is saved
    /// </summary>
    public bool HasSavedName()
    {
        return PlayerPrefs.HasKey(SAVED_NAME_KEY) &&
               !string.IsNullOrEmpty(PlayerPrefs.GetString(SAVED_NAME_KEY));
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitPlayerNameServerRpc(ulong clientId, string playerName)
    {
        Debug.Log($"[PlayerNameManager] Server received name from {clientId}: {playerName}");

        // Check if player already exists
        for (int i = 0; i < playerNames.Count; i++)
        {
            if (playerNames[i].clientId == clientId)
            {
                // Update existing entry
                var entry = playerNames[i];
                entry.playerName = new FixedString64Bytes(playerName);
                playerNames[i] = entry;
                Debug.Log($"[PlayerNameManager] Updated existing name for {clientId}");
                return;
            }
        }

        // Add new entry
        playerNames.Add(new PlayerNameEntry
        {
            clientId = clientId,
            playerName = new FixedString64Bytes(playerName)
        });

        Debug.Log($"[PlayerNameManager] Added new name for {clientId}");
    }

    private void OnPlayerNamesListChanged(NetworkListEvent<PlayerNameEntry> changeEvent)
    {
        Debug.Log($"[PlayerNameManager] Names list changed: {changeEvent.Type}");

        // Clear cache to force refresh
        nameCache.Clear();

        // Notify listeners
        OnPlayerNamesChanged?.Invoke();
    }

    /// <summary>
    /// Get all player names (useful for displaying lists)
    /// </summary>
    public Dictionary<ulong, string> GetAllPlayerNames()
    {
        Dictionary<ulong, string> names = new Dictionary<ulong, string>();

        foreach (var entry in playerNames)
        {
            names[entry.clientId] = entry.playerName.ToString();
        }

        return names;
    }

    /// <summary>
    /// Clear saved player name (for testing or reset)
    /// </summary>
    [ContextMenu("Clear Saved Name")]
    public void ClearSavedName()
    {
        PlayerPrefs.DeleteKey(SAVED_NAME_KEY);
        PlayerPrefs.Save();
        Debug.Log("[PlayerNameManager] Cleared saved name");
    }

    /// <summary>
    /// Debug: Show current saved name
    /// </summary>
    [ContextMenu("Show Saved Name")]
    public void ShowSavedName()
    {
        if (HasSavedName())
        {
            Debug.Log($"[PlayerNameManager] Saved name: {GetSavedPlayerName()}");
        }
        else
        {
            Debug.Log("[PlayerNameManager] No saved name");
        }
    }
}

public struct PlayerNameEntry : INetworkSerializable, System.IEquatable<PlayerNameEntry>
{
    public ulong clientId;
    public FixedString64Bytes playerName;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref playerName);
    }

    public bool Equals(PlayerNameEntry other)
    {
        return clientId == other.clientId && playerName.Equals(other.playerName);
    }
}