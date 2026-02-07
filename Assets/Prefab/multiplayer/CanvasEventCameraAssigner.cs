using UnityEngine;
using Unity.Netcode; // For NetworkManager and NetworkObject
using UnityEngine.UI; // For Canvas
using System.Collections; // For Coroutines
using System.Collections.Generic; // For Dictionary if using ConnectedClients (which we are)

[RequireComponent(typeof(Canvas))]
public class CanvasEventCameraAssigner : MonoBehaviour
{
    private Canvas targetCanvas;

    void Awake()
    {
        targetCanvas = GetComponent<Canvas>();
        if (targetCanvas == null)
        {
            Debug.LogError("CanvasEventCameraAssigner: No Canvas component found on this GameObject!", this);
            enabled = false;
            return;
        }

        if (targetCanvas.renderMode != RenderMode.WorldSpace && targetCanvas.renderMode != RenderMode.ScreenSpaceCamera)
        {
            Debug.LogWarning("CanvasEventCameraAssigner: Canvas Render Mode is not World Space or Screen Space - Camera. Event Camera assignment may not be necessary for this render mode.", this);
            // We'll keep it enabled even if not WorldSpace/ScreenSpaceCamera, as it will just not assign anything.
            // You could disable it here if you want, but for robust error checking, we'll keep it active.
        }
    }

    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            // --- FIX: Subscribe to OnClientConnectedCallback instead of OnPlayerPrefabSpawned ---
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            // Also check for already connected clients (e.g., for the Host when this script starts)
            // This will trigger the WaitForLocalPlayerObject for the host's client.
            if (NetworkManager.Singleton.IsConnectedClient && NetworkManager.Singleton.LocalClient != null) // Check if local client is connected
            {
                StartCoroutine(WaitForPlayerObject(NetworkManager.Singleton.LocalClient.ClientId));
            }
        }
        else
        {
            Debug.LogError("CanvasEventCameraAssigner: NetworkManager.Singleton is null. Is NetworkManager in scene?", this);
            enabled = false;
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
        {
            // --- FIX: Unsubscribe from OnClientConnectedCallback ---
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    // --- NEW METHOD: Handles when a client connects ---
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"CanvasEventCameraAssigner: Client {clientId} connected. Checking for PlayerObject...");
        // Start coroutine to wait for the player object to be spawned for this client.
        StartCoroutine(WaitForPlayerObject(clientId));
    }

    // --- MODIFIED COROUTINE: Waits for the player object to be spawned ---
    private IEnumerator WaitForPlayerObject(ulong clientId)
    {
        float timeout = 10f; // Max wait time
        float timer = 0f;

        // Wait until the client is in ConnectedClients and its PlayerObject is not null
        // Check NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId) before accessing
        while ((NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId) || NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject == null) && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null; // Wait for next frame
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId) && NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject != null)
        {
            NetworkObject playerNetworkObject = NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject;
            if (playerNetworkObject.IsOwner) // Only assign for the local client's player object
            {
                Camera playerCamera = playerNetworkObject.GetComponentInChildren<Camera>(true);
                if (playerCamera != null)
                {
                    if (targetCanvas.renderMode == RenderMode.WorldSpace || targetCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                    {
                        targetCanvas.worldCamera = playerCamera;
                        Debug.Log($"CanvasEventCameraAssigner: Assigned local player's camera '{playerCamera.name}' for Client ID {clientId} as Event Camera.", this);
                    }
                    else
                    {
                         Debug.Log($"CanvasEventCameraAssigner: Local player's camera '{playerCamera.name}' spawned for Client ID {clientId}, but Canvas Render Mode is not World Space or Screen Space-Camera, so no Event Camera assigned.", this);
                    }
                }
                else
                {
                    Debug.LogWarning($"CanvasEventCameraAssigner: Local player's camera not found on spawned NetworkPlayer for Client ID {clientId}! Canvas will not receive input.", this);
                }
            }
            else
            {
                Debug.Log($"CanvasEventCameraAssigner: PlayerObject for Client ID {clientId} spawned, but it's not the local owner. No Event Camera assigned.", this);
            }
        }
        else
        {
            Debug.LogWarning($"CanvasEventCameraAssigner: Timed out waiting for PlayerObject for Client ID {clientId} to assign as Event Camera. PlayerObject was null or client not found.", this);
        }
    }
}