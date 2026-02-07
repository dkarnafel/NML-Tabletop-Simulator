using Unity.Netcode; // <--- ADD THIS
using UnityEngine;
using UnityEngine.EventSystems; // For IDragHandler, IBeginDragHandler, IEndDragHandler
using UnityEngine.UI; // For RectTransform, Canvas, CanvasGroup

public class UIDrag : NetworkBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler // <--- CHANGE MonoBehaviour TO NetworkBehaviour
{
    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup _canvasGroup; // <--- Renamed to avoid confusion with type name

    [Tooltip("Zone")]
    public string snapZoneTag = "Zone";

    // Header and colors commented out in original, so keeping them commented/removed.
    // public Color selectedColor = Color.yellow;
    // public Color defaultColor = Color.white;


    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>(); // Canvas is parent of UI elements

        // --- NEW: Assign CanvasGroup ---
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            Debug.LogWarning($"[UIDrag {gameObject.name}] No CanvasGroup component found. Adding one dynamically.", this);
            _canvasGroup = gameObject.AddComponent<CanvasGroup>(); // Add CanvasGroup if missing
        }
    }

    // OnNetworkSpawn ensures this script runs only for the owner
    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false; // Disable dragging script for remote clients
            Debug.Log($"[UIDrag {gameObject.name}] Not owner, disabling drag script.");
        }
        else
        {
            Debug.Log($"[UIDrag {gameObject.name}] Is owner, drag script enabled.");
        }
    }

    // OnBeginDrag is called when dragging starts
    public void OnBeginDrag(PointerEventData eventData)
    {
        // Check if this card's owner can begin drag (e.g., during their turn)
        // This is a client-side check, server will ultimately validate.
        if (!IsOwner) return;

        // Make card non-raycastable during drag so raycasts hit elements beneath it (e.g., drop zones)
        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = false;
        }
        // Optional: Bring the dragged card to the front of the rendering order
        transform.SetAsLastSibling();
        Debug.Log($"[UIDrag {gameObject.name}] Begin Drag. Blocks Raycasts: false");
    }

    // OnDrag is called continuously while dragging
    public void OnDrag(PointerEventData eventData)
    {
        if (!IsOwner) return; // Only owner can drag

        // Convert screen position to local position on the Canvas RectTransform
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, // Correctly use worldCamera for World Space
                out localPoint))
        {
            rectTransform.anchoredPosition = localPoint; // Client-side visual update
        }
        // Debug.Log($"[UIDrag {gameObject.name}] Dragging to {rectTransform.anchoredPosition}");
    }

    // OnEndDrag is called when dragging ends (mouse button released)
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsOwner) return; // Only owner can end drag

        // Re-enable raycasting for the card
        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = true;
        }
        Debug.Log($"[UIDrag {gameObject.name}] End Drag. Blocks Raycasts: true");

        // --- Server-Authoritative Drop Logic ---

        // Find all potential snap zones locally
        GameObject[] snapZones = GameObject.FindGameObjectsWithTag(snapZoneTag);
        string targetZoneName = ""; // Name of the zone we snapped to
        Vector3 targetWorldPosition = transform.position; // Default to current position if no snap

        // Check if dropped onto a snap zone
        foreach (var zone in snapZones)
        {
            RectTransform zoneRect = zone.GetComponent<RectTransform>();
            if (zoneRect != null && RectTransformUtility.RectangleContainsScreenPoint(zoneRect, eventData.position, canvas.worldCamera))
            {
                // Snap locally for immediate feedback (client-side prediction)
                // The server will later confirm this position via NetworkTransform sync
                rectTransform.position = zoneRect.position;
                targetWorldPosition = zoneRect.position;
                targetZoneName = zone.name; // Use zone.name as identifier
                Debug.Log($"[UIDrag {gameObject.name}] Snapped locally to zone: {zone.name}");
                break; // Snapped to first valid zone
            }
        }

        // --- Send ServerRpc to confirm drop ---
        // Request the server to update this card's position and (optionally) parent.
        RequestCardDropServerRpc(targetWorldPosition, targetZoneName);

        // The NetworkTransform component on the Card prefab will automatically reconcile
        // the client's position with the server's confirmed position after this RPC.
        Debug.Log($"[UIDrag {gameObject.name}] Sending drop request to server for position {targetWorldPosition} in zone {targetZoneName}.");
    }


    // --- ServerRpc: Client requests the server to update the card's position/parent ---
    [ServerRpc(RequireOwnership = true)] // RequireOwnership true: only the card's owner can send this RPC
    public void RequestCardDropServerRpc(Vector3 newPosition, string snappedZoneName, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return; // This RPC only executes on the server

        ulong senderClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[UIDrag Server] Client {senderClientId} requested to drop Card {gameObject.name} (ID: {NetworkObject.NetworkObjectId}) at {newPosition} in zone: {snappedZoneName}.");

        // --- SERVER-SIDE VALIDATION ---
        // Here, the server would:
        // 1. Validate if the move is allowed (e.g., is it senderClientId's turn? Is the zone valid for this card?)
        //    (e.g., you might have a GameManager that validates `snappedZoneName`)
        // 2. Update the authoritative game state (e.g., Card is now in 'BoardZone_Player1')

        // If validation passes:
        // Update the NetworkObject's Transform (this will automatically sync to all clients)
        transform.position = newPosition; // Use world position for NetworkTransform
        // Optional: Change parent if moving to a new zone GameObject that represents a parent (e.g., HandArea, BoardZone)
        // NetworkManager.Singleton.SpawnManager.GetNetworkObject(NetworkObject.NetworkObjectId).transform.SetParent(targetParentNetworkObject.transform, true);
        // This requires targetParentNetworkObject to also be a NetworkObject if its transform is synced.

        Debug.Log($"[UIDrag Server] Card {gameObject.name} confirmed drop to {newPosition}. Syncing to all clients.");

        // If validation fails:
        // Do NOT set transform.position. The NetworkTransform will automatically snap the client's card
        // back to its last known server-authoritative position (its previous position before the drag).
        // You might send a ClientRpc back to the sender to explain why the move failed.
    }
}