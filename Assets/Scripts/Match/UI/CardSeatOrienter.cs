using Unity.Netcode;
using UnityEngine;

public class CardSeatOrienter : NetworkBehaviour
{
    [Tooltip("Rotation applied for Player 1's view (in degrees around Z).")]
    [SerializeField] private float player1RotationZ = 180f;

    private bool _applied;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        ApplySeatRotation();
    }

    public override void OnGainedOwnership()
    {
        base.OnGainedOwnership();
        ApplySeatRotation();
    }

    private void ApplySeatRotation()
    {
        if (_applied || !IsSpawned)
            return;

        // Which seat does this object's owner occupy?
        var seat = PlayerSeat.GetSeatForClient(OwnerClientId);

        if (seat == PlayerSeat.SeatType.Player1)
        {
            // Rotate 180° around Z relative to current rotation
            transform.rotation = Quaternion.Euler(0f, 0f, player1RotationZ) * transform.rotation;
        }

        _applied = true;
    }
}
