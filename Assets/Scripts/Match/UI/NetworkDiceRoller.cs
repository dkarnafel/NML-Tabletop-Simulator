using Unity.Netcode;
using UnityEngine;

public class NetworkDiceRoller : NetworkBehaviour
{
    public static NetworkDiceRoller Instance { get; private set; }

    // Replicated final dice values (1..6)
    public NetworkVariable<int> Die1 = new NetworkVariable<int>(1);
    public NetworkVariable<int> Die2 = new NetworkVariable<int>(1);

    // Replicated "roll id" so UI can detect a new roll even if values repeat
    public NetworkVariable<int> RollId = new NetworkVariable<int>(0);

    private void Awake()
    {
        Instance = this;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestRollServerRpc(ServerRpcParams rpcParams = default)
    {
        // Server decides final results so all clients match
        int d1 = Random.Range(1, 7);
        int d2 = Random.Range(1, 7);

        Die1.Value = d1;
        Die2.Value = d2;

        RollId.Value++; // triggers all clients to animate
    }
}
