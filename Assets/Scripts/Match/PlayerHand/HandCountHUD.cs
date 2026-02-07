using TMPro;
using Unity.Netcode;
using UnityEngine;

public class HandCountHUD : MonoBehaviour
{
    public static HandCountHUD Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI player1Text;
    [SerializeField] private TextMeshProUGUI player2Text;

    private void OnEnable() => InvokeRepeating(nameof(Refresh), 0.2f, 1f);
    private void OnDisable() => CancelInvoke(nameof(Refresh));
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Refresh()
    {
        int p1 = 0;
        int p2 = 0;

        // Find all trackers that are currently spawned
        var trackers = FindObjectsOfType<HandCountTracker>(true);
        foreach (var t in trackers)
        {
            if (t == null || !t.IsSpawned) continue;

            // ✅ Seat is on the same NetworkPlayer object
            var seatComp = t.GetComponent<PlayerSeat>();
            if (seatComp == null) continue;

            var seat = seatComp.CurrentSeat.Value;

            if (seat == PlayerSeat.SeatType.Player1) p1 = t.HandCount.Value;
            else if (seat == PlayerSeat.SeatType.Player2) p2 = t.HandCount.Value;
        }

        if (player1Text != null) player1Text.text = $"Hand: {p1}";
        if (player2Text != null) player2Text.text = $"Hand: {p2}";
    }
}
