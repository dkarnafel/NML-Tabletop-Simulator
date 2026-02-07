using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// FIXED VERSION - No NetworkBehaviour (it's just UI!)
/// Custom Player Profile Script for your PlayerProfile prefab
/// This version toggles Ready badge and Crown on/off
/// Attach this to your PlayerProfile prefab root
/// </summary>
public class PlayerProfileItem : MonoBehaviour
{
    [Header("UI References - Drag from your prefab")]
    [SerializeField] private Image characterImage;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private GameObject readyBadge;      // The "READY" badge GameObject
    [SerializeField] private GameObject hostCrown;        // The crown GameObject

    [Header("Optional - Character Image")]
    [SerializeField] private Sprite defaultCharacterSprite;

    // Local variables (no network sync needed - this is just UI!)
    private ulong clientId;
    private bool isReady;

    private void Start()
    {
        // Subscribe to player name changes
        if (PlayerNameManagerFixed.Instance != null)
        {
            PlayerNameManagerFixed.Instance.OnPlayerNamesChanged += UpdateDisplay;
        }

        // Initial update
        UpdateDisplay();

        Debug.Log($"PlayerProfileItem started for client {clientId}");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (PlayerNameManagerFixed.Instance != null)
        {
            PlayerNameManagerFixed.Instance.OnPlayerNamesChanged -= UpdateDisplay;
        }
    }

    /// <summary>
    /// Initialize this player profile with a client ID
    /// Called by the lobby manager when creating the profile
    /// </summary>
    public void Initialize(ulong clientId)
    {
        this.clientId = clientId;
        Debug.Log($"PlayerProfile initialized for client {clientId}");
        UpdateDisplay();
    }

    /// <summary>
    /// Set the ready state for this player
    /// This gets called from the lobby manager when ready state changes
    /// </summary>
    public void SetReady(bool ready)
    {
        this.isReady = ready;
        UpdateReadyBadge(ready);
        Debug.Log($"PlayerProfile {clientId} ready state: {ready}");
    }

    /// <summary>
    /// Update the entire display - name, ready, host
    /// </summary>
    private void UpdateDisplay()
    {
        UpdatePlayerName();
        UpdateReadyBadge(isReady);
        UpdateHostCrown();
    }

    /// <summary>
    /// Update the player name text
    /// </summary>
    private void UpdatePlayerName()
    {
        if (playerNameText == null)
        {
            Debug.LogError("PlayerNameText is NULL! Please assign it in the prefab.");
            return;
        }

        string playerName = "Loading...";

        // Get name from PlayerNameManager
        if (PlayerNameManagerFixed.Instance != null)
        {
            playerName = PlayerNameManagerFixed.Instance.GetPlayerName(clientId);
            Debug.Log($"Got name for client {clientId}: {playerName}");
        }
        else
        {
            Debug.LogError("PlayerNameManagerFixed.Instance is NULL!");
            playerName = $"Player {clientId}";
        }

        playerNameText.text = playerName;
        Debug.Log($"Set playerNameText to: {playerName}");
    }

    /// <summary>
    /// Show/hide the READY badge
    /// </summary>
    private void UpdateReadyBadge(bool ready)
    {
        if (readyBadge != null)
        {
            readyBadge.SetActive(ready);
        }
        else
        {
            Debug.LogWarning("Ready badge is null!");
        }
    }

    /// <summary>
    /// Show/hide the crown for host
    /// </summary>
    private void UpdateHostCrown()
    {
        if (hostCrown == null)
        {
            Debug.LogWarning("Host crown is null!");
            return;
        }

        bool isHost = IsPlayerHost(clientId);
        hostCrown.SetActive(isHost);
        Debug.Log($"Client {clientId} is host: {isHost}");
    }

    /// <summary>
    /// Check if this player is the host
    /// </summary>
    private bool IsPlayerHost(ulong clientId)
    {
        if (Unity.Netcode.NetworkManager.Singleton == null) return false;

        // The host is always client ID 0 (server)
        return clientId == 0;
    }

    /// <summary>
    /// Get the client ID this profile represents
    /// </summary>
    public ulong GetClientId()
    {
        return clientId;
    }

    /// <summary>
    /// Get the ready state
    /// </summary>
    public bool IsReady()
    {
        return isReady;
    }

    /// <summary>
    /// Optional: Set character image
    /// You can expand this to use different character sprites per player
    /// </summary>
    public void SetCharacterImage(Sprite sprite)
    {
        if (characterImage != null)
        {
            characterImage.sprite = sprite;
        }
    }
}