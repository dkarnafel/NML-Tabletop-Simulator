using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class BoardCardSpawner : NetworkBehaviour
{
    public static BoardCardSpawner Instance { get; private set; }

    [SerializeField] private NetworkCard cardPrefab;
    [SerializeField] private float boardZ = 0f;   // z height for cards on the board
    [SerializeField] private float spawnYOffset = 0.5f; // tweak this in Inspector

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Called on the LOCAL client that dragged the card from the UI.
    /// Sends a request to the server to spawn a board card.
    /// </summary>
    public void RequestPlayCardFromHand(string cardName, Vector3 worldPos, ulong ownerClientId)
    {
        if (!IsClient || NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[BoardCardSpawner] Not a connected client.");
            return;
        }

        FixedString128Bytes fsName = cardName;
        PlayCardServerRpc(fsName, worldPos, ownerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlayCardServerRpc(FixedString128Bytes cardName,
                                   Vector3 worldPos,
                                   ulong ownerClientId)
    {
        if (cardPrefab == null)
        {
            Debug.LogError("[BoardCardSpawner] cardPrefab not assigned.");
            return;
        }

        // Lock Z to board plane
        worldPos.z = boardZ;
        worldPos.y += spawnYOffset;

        NetworkCard newCard = Instantiate(cardPrefab, worldPos, Quaternion.identity);
        NetworkObject netObj = newCard.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[BoardCardSpawner] cardPrefab missing NetworkObject.");
            Destroy(newCard.gameObject);
            return;
        }

        netObj.SpawnWithOwnership(ownerClientId);

        // Initialize card name on server (replicates to all clients)
        newCard.SetCardName(cardName.ToString());
    }
}
