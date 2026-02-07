using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic; 

public class GameManager : NetworkBehaviour
{
    public static GameManager Singleton { get; private set; }

    [Header("Reset / Refresh")]
    [SerializeField] private List<NetworkObject> keepNetworkObjects = new(); // Board root, UI anchor net objects, etc.
    [SerializeField] private bool hostOnlyReset = true; // set false if you want anyone to reset


    // Store references to all active PlayerIdentity components
    private Dictionary<ulong, PlayerIdentity> _playerIdentities = new Dictionary<ulong, PlayerIdentity>();

    public override void OnNetworkSpawn()
    {
        if (Singleton != null && Singleton != this)
        {
            Destroy(gameObject);
            return;
        }
        Singleton = this;

        Debug.Log("GameManager OnNetworkSpawn. IsServer: " + IsServer + ", IsClient: " + IsClient);

        // Subscribe to client connection/disconnection events
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

        // Handle existing clients if GameManager spawns after they connect 
        // This is for the server/host when it starts or if this GameManager spawns later
        // and there are already connected clients whose players have spawned.
        if (IsServer)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                // Check if the player object already exists for this client
                if (client.PlayerObject != null)
                {
                    PlayerIdentity playerIdentity = client.PlayerObject.GetComponent<PlayerIdentity>();
                    if (playerIdentity != null && !_playerIdentities.ContainsKey(client.ClientId))
                    {
                        _playerIdentities[client.ClientId] = playerIdentity;
                        Debug.Log($"[GameManager] Server found existing player {client.ClientId} during spawn. Registered.");
                    }
                }
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        //Handle network objects and players if someone disconnects
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
        if (Singleton == this)
        {
            Singleton = null;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"[GameManager] Client connected: {clientId}");
        // Coroutine to wait for the player's NetworkObject to be spawned
        StartCoroutine(WaitForPlayerObject(clientId));
    }

    private System.Collections.IEnumerator WaitForPlayerObject(ulong clientId)
    {
        // Wait for a short period, or until the player object exists
        float timeout = 5f; // Max wait time
        float timer = 0f; // initial timer

        while (timer < timeout && !NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId) || NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject == null)
        {
            timer += Time.deltaTime;
            yield return null; // Wait for next frame
        }

        if (NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId) && NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject != null)
        {
            PlayerIdentity playerIdentity = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject.GetComponent<PlayerIdentity>();
            if (playerIdentity != null && !_playerIdentities.ContainsKey(clientId))
            {
                // Set player indentiy after delay
                _playerIdentities[clientId] = playerIdentity;
                Debug.Log($"[GameManager] Registered PlayerIdentity for client {clientId} after spawn.");
            }
            else if (playerIdentity == null)
            {
                // Setup error to see if playeridenty is not set right
                Debug.LogError($"[GameManager] PlayerObject for client {clientId} found, but no PlayerIdentity component!");
            }
        }
        else
        {
            //Setup warning if we go past the timeout
            Debug.LogWarning($"[GameManager] Timed out waiting for PlayerObject for client {clientId}.");
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        Debug.Log($"[GameManager] Client disconnected: {clientId}");
        // Remove reference if player disconnects
        _playerIdentities.Remove(clientId); 
    }

    public PlayerIdentity GetPlayerIdentity(ulong clientId)
    {
        _playerIdentities.TryGetValue(clientId, out PlayerIdentity player);
        return player;
    }

    public void RequestResetFromUI()
    {
        if (NetworkManager.Singleton == null) return;
        RequestResetGameStateServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestResetGameStateServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        if (hostOnlyReset)
        {
            // (Host is both client+server; LocalClientId on host is the host client)
            if (rpcParams.Receive.SenderClientId != NetworkManager.Singleton.LocalClientId)
                return;
        }

        ResetGameStateServer();
    }
    private void ResetGameStateServer()
    {
        if (!IsServer) return;

        int uiLayer = LayerMask.NameToLayer("UI");

        var keepIds = new HashSet<ulong>();
        keepIds.Add(NetworkObjectId);

        foreach (var k in keepNetworkObjects)
        {
            if (k != null && k.IsSpawned)
                keepIds.Add(k.NetworkObjectId);
        }

        var spawned = new List<NetworkObject>(NetworkManager.Singleton.SpawnManager.SpawnedObjectsList);

        foreach (var no in spawned)
        {
            if (no == null) continue;

            // Never destroy player objects
            if (no.IsPlayerObject) continue;

            // Never destroy GameManager / whitelisted objects
            if (keepIds.Contains(no.NetworkObjectId)) continue;

            // Never destroy anything on the UI layer
            if (uiLayer != -1 && no.gameObject.layer == uiLayer) continue;

            // Despawn ONLY gameplay pieces (cards/decks/resources)
            bool isNetworkCard = no.GetComponent<NetworkCard>() != null;
            bool isTaggedDespawn = no.CompareTag("DespawnOnReset");

            if (!isNetworkCard && !isTaggedDespawn)
                continue; // keep board/zones/managers/colliders/etc.

            // Never despawn the NetworkManager object or core infrastructure
            if (no.GetComponent<Unity.Netcode.NetworkManager>() != null) continue;
            if (no.GetComponent<ResourceCardSpawner>() != null) continue;
            if (no.GetComponent<GameManager>() != null) continue;

            no.Despawn(true);
        }

        ResetClientUiClientRpc();
    }


    [ClientRpc]
    private void ResetClientUiClientRpc()
    {
        // 1) Destroy all hand-card UI objects (client-only)
#if UNITY_2023_1_OR_NEWER
        var handCards = FindObjectsByType<HandCardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
    var handCards = FindObjectsOfType<HandCardUI>(true);
#endif

        foreach (var hc in handCards)
        {
            if (hc != null)
                Destroy(hc.gameObject);
        }

        // 2) Hide zoom if any card was showing it
        if (CardZoomUI.Instance != null)
            CardZoomUI.Instance.HideZoom();

        // 3) Destroy all pile badge instances (client-only visuals)
        var badges = GameObject.FindObjectsOfType<GameObject>(true);
        foreach (var go in badges)
        {
            if (go != null && go.name == "PileCountBadge_Instance")
                Destroy(go);
        }
        var roots = GameObject.FindObjectsOfType<GameObject>(true);
        foreach (var go in roots)
        {
            if (go != null && go.name == "PileCycleControls_Instance")
                Destroy(go);
        }
    }

}