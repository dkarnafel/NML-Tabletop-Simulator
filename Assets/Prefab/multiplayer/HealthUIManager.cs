using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections.Generic;
using System.Collections; // For coroutines
using UnityEngine.UI; // <--- ADD THIS LINE (if not already there)

public class HealthUIManager : MonoBehaviour
{
    [Header("Global Health Displays (in Scene)")]
    [SerializeField] private TextMeshProUGUI player0HealthText; // Assign Player 0's TextMeshProUGUI
    [SerializeField] private TextMeshProUGUI player1HealthText; // Assign Player 1's TextMeshProUGUI
    
    // --- NEW: Button references (will appear as slots in Inspector) ---
    [SerializeField] private Button p0HealthMinusButton;
    [SerializeField] private Button p0HealthPlusButton;
    [SerializeField] private Button p1HealthMinusButton;
    [SerializeField] private Button p1HealthPlusButton;

    // Store references to PlayerHealthData scripts by ClientId
    private Dictionary<ulong, PlayerHealthData> _playerHealthData = new Dictionary<ulong, PlayerHealthData>();

    void Awake() // <--- NEW: Awake method for button listeners
    {
        // Link buttons to their respective methods, passing the TARGET ClientId
        if (p0HealthMinusButton != null)
        {
            p0HealthMinusButton.onClick.AddListener(() => DecreasePlayerHealth(0, 1)); // Target Player 0 (ClientId 0)
        }
        if (p0HealthPlusButton != null)
        {
            p0HealthPlusButton.onClick.AddListener(() => IncreasePlayerHealth(0, 1)); // Target Player 0 (ClientId 0)
        }
        if (p1HealthMinusButton != null)
        {
            p1HealthMinusButton.onClick.AddListener(() => DecreasePlayerHealth(1, 1)); // Target Player 1 (ClientId 1)
        }
        if (p1HealthPlusButton != null)
        {
            p1HealthPlusButton.onClick.AddListener(() => IncreasePlayerHealth(1, 1)); // Target Player 1 (ClientId 1)
        }
    }
    
    void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // Register any players already connected (e.g., Host's own player on Start)
        if (NetworkManager.Singleton.IsConnectedClient)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClients)
            {
                StartCoroutine(WaitForPlayerObjectAndRegisterHealthData(client.Key));
            }
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        foreach (var kvp in _playerHealthData)
        {
            if (kvp.Value != null)
            {
                kvp.Value.CurrentHealth.OnValueChanged -= OnPlayerHealthChanged;
            }
        }
        _playerHealthData.Clear();
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[HealthUIManager] Client {clientId} connected. Waiting for PlayerObject to get HealthData.");
        StartCoroutine(WaitForPlayerObjectAndRegisterHealthData(clientId));
    }

    private IEnumerator WaitForPlayerObjectAndRegisterHealthData(ulong clientId)
    {
        float timeout = 10f;
        float timer = 0f;

        while ((NetworkManager.Singleton == null ||
                !NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId) ||
                NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject == null) && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId) && NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject != null)
        {
            NetworkObject playerObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
            PlayerHealthData healthData = playerObject.GetComponent<PlayerHealthData>();

            if (healthData != null && !_playerHealthData.ContainsKey(clientId))
            {
                _playerHealthData.Add(clientId, healthData);
                healthData.CurrentHealth.OnValueChanged += OnPlayerHealthChanged;
                OnPlayerHealthChanged(healthData.CurrentHealth.Value, healthData.CurrentHealth.Value); // Initial UI update
                Debug.Log($"[HealthUIManager] Registered health data for Player {clientId}.");
            }
            else if (healthData == null)
            {
                Debug.LogError($"[HealthUIManager] PlayerObject for {clientId} spawned, but missing PlayerHealthData!", playerObject);
            }
            else if (_playerHealthData.ContainsKey(clientId))
            {
                 Debug.LogWarning($"[HealthUIManager] WARNING: Player {clientId} HealthData already registered. Skipping duplicate.");
            }
        }
        else
        {
            Debug.LogWarning($"[HealthUIManager] Timed out or failed to get PlayerObject for {clientId}.");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[HealthUIManager] Client {clientId} disconnected.");
        if (_playerHealthData.TryGetValue(clientId, out PlayerHealthData data))
        {
            data.CurrentHealth.OnValueChanged -= OnPlayerHealthChanged;
            _playerHealthData.Remove(clientId);
        }
        if (clientId == 0 && player0HealthText != null) player0HealthText.text = "Player 0: Disconnected";
        else if (clientId == 1 && player1HealthText != null) player1HealthText.text = "Player 1: Disconnected";
    }

    private void OnPlayerHealthChanged(int oldHealth, int newHealth)
    {
        // Find which player's health was updated and update their specific UI Text
        foreach (var kvp in _playerHealthData)
        {
            if (kvp.Value.CurrentHealth.Value == newHealth) // Simple check, relies on uniqueness of newHealth
            {
                if (kvp.Key == 0 && player0HealthText != null)
                {
                    player0HealthText.text = $"Health: {newHealth}";
                    Debug.Log($"[HealthUIManager] Updated Player 0 Health UI to: {newHealth}");
                    return;
                }
                else if (kvp.Key == 1 && player1HealthText != null)
                {
                    player1HealthText.text = $"Health: {newHealth}";
                    Debug.Log($"[HealthUIManager] Updated Player 1 Health UI to: {newHealth}");
                    return;
                }
            }
        }
        Debug.LogWarning($"[HealthUIManager] Could not find matching PlayerHealthData for updated health {newHealth}. This can happen if health values are identical for multiple players.");
    }

    // --- NEW: Universal methods to change health for a TARGETED player ---
    public void DecreasePlayerHealth(ulong targetClientId, int amount)
    {
        Debug.Log($"[HealthUIManager] Button Clicked: Decrease health for Client ID {targetClientId}. Amount: {amount}.");
        if (_playerHealthData.TryGetValue(targetClientId, out PlayerHealthData targetHealthData))
        {
            if (targetHealthData != null)
            {
                targetHealthData.RequestChangeHealthServerRpc(amount, false); // Send RPC to server
                Debug.Log($"[HealthUIManager] RPC sent to server for Client {targetClientId} to decrease health.");
            }
            else
            {
                Debug.LogError($"[HealthUIManager] ERROR: Target PlayerHealthData for Client {targetClientId} is null after retrieval from dictionary.");
            }
        }
        else
        {
            Debug.LogWarning($"[HealthUIManager] WARNING: No PlayerHealthData registered in dictionary for Client {targetClientId}. Cannot change health.");
        }
    }

    public void IncreasePlayerHealth(ulong targetClientId, int amount)
    {
        Debug.Log($"[HealthUIManager] Button Clicked: Increase health for Client ID {targetClientId}. Amount: {amount}.");
        if (_playerHealthData.TryGetValue(targetClientId, out PlayerHealthData targetHealthData))
        {
            if (targetHealthData != null)
            {
                targetHealthData.RequestChangeHealthServerRpc(amount, true); // Send RPC to server
                Debug.Log($"[HealthUIManager] RPC sent to server for Client {targetClientId} to increase health.");
            }
            else
            {
                Debug.LogError($"[HealthUIManager] ERROR: Target PlayerHealthData for Client {targetClientId} is null after retrieval from dictionary.");
            }
        }
        else
        {
            Debug.LogWarning($"[HealthUIManager] WARNING: No PlayerHealthData registered in dictionary for Client {targetClientId}. Cannot change health.");
        }
    }
}