using Unity.Netcode; // Essential for networked behavior
using UnityEngine;
using System.Collections.Generic; // For List<ulong> in RpcParams
using Unity.Collections; // For NetworkVariable<T>
using Unity.Netcode.Components; // <--- ADD THIS LINE
using System; // NEW: for Action

public class PlayerIdentity : NetworkBehaviour
{
    // NEW: fired whenever a player's lobby-relevant state changes (deck or ready)
    public static event Action LobbyStateChanged;

    // Static reference to the local player's instance for easy access from other local scripts
    public static PlayerIdentity LocalPlayerInstance { get; private set; }

    public NetworkVariable<int> PlayerId = new NetworkVariable<int>(0);

    // --- NetworkVariables for Lobby State ---
    public NetworkVariable<bool> IsReady = new NetworkVariable<bool>(false);

    [SerializeField] private GameObject playerVisualPrefab; // Assign your CubeVisual Prefab here
    private GameObject _spawnedPlayerVisual;

    //// Camera settings for this player's view
    //[SerializeField] private Camera playerCamera; // Assign the Camera component from the PlayerCamera child here
    //[SerializeField] private Vector3 player1CameraPosition = new Vector3(0, 10, -5);
    //[SerializeField] private Vector3 player1CameraRotation = new Vector3(60, 0, 0);
    //[SerializeField] private Vector3 player2CameraPosition = new Vector3(0, 10, 5);
    //[SerializeField] private Vector3 player2CameraRotation = new Vector3(120, 180, 0);

    public override void OnNetworkSpawn()
    {
        Debug.Log($"[PlayerIdentity {gameObject.name}] OnNetworkSpawn started. IsOwner: {IsOwner}. IsServer: {IsServer}. " +
                $"Initial PlayerId.Value: {PlayerId.Value}. NetworkObject.OwnerClientId: {NetworkObject.OwnerClientId}.");

        if (IsOwner) // This code runs only for the player instance owned by the local client
        {
            LocalPlayerInstance = this;
            if (IsServer) // This code runs on the server for its local player (Host's player)
            {
                PlayerId.Value = (int)NetworkObject.OwnerClientId; // Assign ID 0 for host
                Debug.Log($"[PlayerIdentity {gameObject.name}] Server (IsOwner true): Set PlayerId.Value to {PlayerId.Value} using OwnerClientId {NetworkObject.OwnerClientId}.");
            }
        }
        else // This code runs for player instances owned by remote clients
        {
            if (IsServer) // This means it's the server's view of a REMOTE player
            {
                PlayerId.Value = (int)NetworkObject.OwnerClientId; // Assign ID 1 (etc) for remote players on the server
                Debug.Log($"[PlayerIdentity {gameObject.name}] Server (IsOwner false, but IsServer true): Set PlayerId.Value to {PlayerId.Value} using OwnerClientId {NetworkObject.OwnerClientId}.");
            }
        }

        //// Initialize camera and audio listener (enabled only for the owner)
        //if (playerCamera != null)
        //{
        //    playerCamera.enabled = IsOwner;
        //    AudioListener audioListener = playerCamera.GetComponent<AudioListener>();
        //    if (audioListener != null)
        //    {
        //        audioListener.enabled = IsOwner;
        //    }

        //    // --- THIS IS THE CRUCIAL PART ---
        //    if (IsOwner) // Only tag the local player's camera
        //    {
        //        playerCamera.tag = "MainCamera"; // Assign the "MainCamera" tag
        //        Debug.Log($"[PlayerIdentity {NetworkObject.OwnerClientId}] Assigned 'MainCamera' tag to local player's camera.");
        //    }
        //    else // For remote player cameras, ensure they don't have the tag
        //    {
        //        playerCamera.tag = "Untagged";
        //    }
        //}

        //// Spawn visual representation for the player
        //if (playerVisualPrefab != null)
        //{
        //    _spawnedPlayerVisual = Instantiate(playerVisualPrefab, transform);
        //    _spawnedPlayerVisual.transform.localPosition = Vector3.zero;

        //    Renderer renderer = _spawnedPlayerVisual.GetComponent<Renderer>();
        //    if (renderer != null)
        //    {
        //        renderer.material = new Material(renderer.material);
        //    }
        //}

        // Force initial update
        OnPlayerIdChanged(0, PlayerId.Value);

        // Subscribe to NetworkVariable changes (for all clients to update UI)
        IsReady.OnValueChanged += OnIsReadyChanged;
    }

    public override void OnNetworkDespawn()
    {
        PlayerId.OnValueChanged -= OnPlayerIdChanged;
        if (_spawnedPlayerVisual != null)
        {
            Destroy(_spawnedPlayerVisual);
        }
        if (LocalPlayerInstance == this) // Clear static reference if this is the local player despawning
        {
            LocalPlayerInstance = null;
        }

        IsReady.OnValueChanged -= OnIsReadyChanged;
        Debug.Log($"[PlayerIdentity {NetworkObject.OwnerClientId}] OnNetworkDespawn called.");
    }

    // --- Callbacks for NetworkVariable changes (updates UI for all clients) ---

    private void OnIsReadyChanged(bool oldReady, bool newReady)
    {
        Debug.Log($"[PlayerIdentity {NetworkObject.OwnerClientId}] Ready state changed from {oldReady} to {newReady}.");
        LobbyStateChanged?.Invoke(); // NEW: notify UI
    }

    // --- ServerRpc to update player's ready state ---
    [ServerRpc]
    public void SetIsReadyServerRpc(bool readyState, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        IsReady.Value = readyState; // Update NetworkVariable (syncs to all clients)
        Debug.Log($"[PlayerIdentity Server] Client {rpcParams.Receive.SenderClientId} set ready state to: {readyState}");
    }

    // Called on all clients when PlayerId.Value changes (e.g., when the server assigns an ID)
    private void OnPlayerIdChanged(int oldVal, int newVal)
    {
        Debug.Log($"[PlayerIdentity {NetworkObject.OwnerClientId}] OnPlayerIdChanged: ID changed from {oldVal} to {newVal}. IsOwner: {IsOwner}");

        // Update player visual color based on ID
        //if (_spawnedPlayerVisual != null)
        //{
        //    Renderer renderer = _spawnedPlayerVisual.GetComponent<Renderer>();
        //    if (renderer != null && renderer.material != null)
        //    {
        //        switch (newVal)
        //        {
        //            case 0: renderer.material.color = Color.red; break;
        //            case 1: renderer.material.color = Color.blue; break;
        //            case 2: renderer.material.color = Color.green; break;
        //            case 3: renderer.material.color = Color.yellow; break;
        //            default: renderer.material.color = Color.gray; break;
        //        }
        //    }
        //}

        //// Position camera for the owner based on PlayerId
        //if (IsOwner && playerCamera != null)
        //{
        //    Debug.Log($"[PlayerIdentity {NetworkObject.OwnerClientId}] IsOwner true. Positioning camera for PlayerId: {newVal}");
        //    if (newVal == 0) // Player 0 (Host)
        //    {
        //        playerCamera.transform.localPosition = player1CameraPosition;
        //        playerCamera.transform.localRotation = Quaternion.Euler(player1CameraRotation);
        //        Debug.Log($"[PlayerIdentity {NetworkObject.OwnerClientId}] Camera moved to Player 0 position: {player1CameraPosition}");
        //    }
        //    else if (newVal == 1) // Player 1 (First Client)
        //    {
        //        playerCamera.transform.localPosition = player2CameraPosition;
        //        playerCamera.transform.localRotation = Quaternion.Euler(player2CameraRotation);
        //        Debug.Log($"[PlayerIdentity {NetworkObject.OwnerClientId}] Camera moved to Player 1 position: {player2CameraPosition}");
        //    }
        //}
        //else if (!IsOwner)
        //{
        //    Debug.Log($"[PlayerIdentity {NetworkObject.OwnerClientId}] Not owner. Camera for this player will remain disabled.");
        //}
        //else if (playerCamera == null)
        //{
        //    Debug.LogError($"[PlayerIdentity {NetworkObject.OwnerClientId}] playerCamera is NULL in OnPlayerIdChanged!", this);
        //}
    }

    void Update()
    {
        if (!IsOwner) return;
    }

}
