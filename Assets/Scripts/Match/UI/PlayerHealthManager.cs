using Unity.Netcode;
using UnityEngine;

public class PlayerHealthManager : NetworkBehaviour
{
    public static PlayerHealthManager Instance { get; private set; }

    [Header("Health Settings")]
    [SerializeField] private int startingHealth = 30;
    [SerializeField] private int minHealth = 0;
    [SerializeField] private int maxHealth = 99;

    // Everyone can read, only the server writes
    public NetworkVariable<int> Player1Health =
        new NetworkVariable<int>(
            30,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public NetworkVariable<int> Player2Health =
        new NetworkVariable<int>(
            30,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            Player1Health.Value = startingHealth;
            Player2Health.Value = startingHealth;
        }
    }

    // Called from UI on any client
    public void RequestAdjustHealth(PlayerSeat.SeatType seat, int delta)
    {
        if (!IsSpawned)
            return;

        AdjustHealthServerRpc(seat, delta);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AdjustHealthServerRpc(PlayerSeat.SeatType seat, int delta)
    {
        switch (seat)
        {
            case PlayerSeat.SeatType.Player1:
                Player1Health.Value = Mathf.Clamp(Player1Health.Value + delta, minHealth, maxHealth);
                break;

            case PlayerSeat.SeatType.Player2:
                Player2Health.Value = Mathf.Clamp(Player2Health.Value + delta, minHealth, maxHealth);
                break;

            default:
                // Spectator / None don't have their own health – ignore
                break;
        }
    }
}
