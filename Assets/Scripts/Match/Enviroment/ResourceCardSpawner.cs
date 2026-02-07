using Unity.Netcode;
using UnityEngine;

public class ResourceCardSpawner : NetworkBehaviour
{
    [Header("Prefab / Spawn")]
    [SerializeField] private NetworkObject resourceCardPrefab;

    // Spawn zones for each player side
    [SerializeField] private Transform player1ResourceSpawnPoint;
    [SerializeField] private Transform player2ResourceSpawnPoint;

    [Header("Audio")]
    [SerializeField] private GameObject oneShotAudioPrefab;
    [SerializeField] private AudioClip resourceSpawnClip;

    private void Update()
    {
        //// Only the local owner should listen for input
        //if (!IsClient || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
        //    return;

        //if (Input.GetKeyDown(KeyCode.F))
        //{
        //    RequestSpawnResourceServerRpc();
        //}
        // Any connected client can request a spawn
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
            return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            Debug.Log($"[ResourceCardSpawner] F pressed. IsClient={IsClient} IsServer={IsServer} IsSpawned={IsSpawned}");
            RequestSpawnResourceServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnResourceServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        EnsureSpawnPoints();

        ulong senderId = rpcParams.Receive.SenderClientId;

        // Which seat is this client currently in?
        var seat = PlayerSeat.GetSeatForClient(senderId);

        Transform spawnPoint = null;
        switch (seat)
        {
            case PlayerSeat.SeatType.Player1:
                spawnPoint = player1ResourceSpawnPoint;
                break;

            case PlayerSeat.SeatType.Player2:
                spawnPoint = player2ResourceSpawnPoint;
                break;

            default:
                spawnPoint = player1ResourceSpawnPoint != null
                    ? player1ResourceSpawnPoint
                    : player2ResourceSpawnPoint;
                break;
        }

        if (resourceCardPrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("[ResourceCardSpawner] Missing prefab or spawn point.");
            return;
        }

        // ✅ Rotation logic
        Quaternion sidewaysRot = Quaternion.Euler(0f, 0f, -90f);
        Quaternion seatRot = seat == PlayerSeat.SeatType.Player1
            ? Quaternion.Euler(0f, 0f, 180f)
            : Quaternion.identity;

        Quaternion rotation = spawnPoint.rotation * seatRot * sidewaysRot;

        NetworkObject instance = Instantiate(
            resourceCardPrefab,
            spawnPoint.position,
            rotation);

        instance.SpawnWithOwnership(senderId);
        PlaySpawnSoundClientRpc(spawnPoint.position);
    }

    private void EnsureSpawnPoints()
    {
        if (player1ResourceSpawnPoint == null)
        {
            var go = GameObject.FindGameObjectWithTag("P1ResourceSpawn");
            if (go != null) player1ResourceSpawnPoint = go.transform;
        }

        if (player2ResourceSpawnPoint == null)
        {
            var go = GameObject.FindGameObjectWithTag("P2ResourceSpawn");
            if (go != null) player2ResourceSpawnPoint = go.transform;
        }
    }

    [ClientRpc]
    private void PlaySpawnSoundClientRpc(Vector3 worldPos)
    {
        if (oneShotAudioPrefab == null || resourceSpawnClip == null)
            return;

        var audioObj = Instantiate(oneShotAudioPrefab, worldPos, Quaternion.identity);
        audioObj.GetComponent<CardLandAudio>().Play(resourceSpawnClip);
    }


    public override void OnNetworkSpawn()
    {
        Debug.Log($"[ResourceCardSpawner] OnNetworkSpawn IsClient={IsClient} IsServer={IsServer} IsSpawned={IsSpawned} active={gameObject.activeInHierarchy} enabled={enabled}");
    }

    private void OnEnable()
    {
        Debug.Log($"[ResourceCardSpawner] OnEnable active={gameObject.activeInHierarchy} enabled={enabled}");
    }

    private void OnDisable()
    {
        Debug.Log($"[ResourceCardSpawner] OnDisable active={gameObject.activeInHierarchy} enabled={enabled}");
    }

    private void OnDestroy()
    {
        Debug.Log("[ResourceCardSpawner] OnDestroy");
    }

}
