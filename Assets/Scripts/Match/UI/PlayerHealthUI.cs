using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthUI : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI player1HealthText;
    [SerializeField] private TextMeshProUGUI player2HealthText;

    private PlayerHealthManager _manager;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _manager = PlayerHealthManager.Instance;
        if (_manager == null)
        {
            Debug.LogError("[PlayerHealthUI] PlayerHealthManager.Instance is null. Make sure it's in the scene.");
            return;
        }

        // Subscribe to health changes
        _manager.Player1Health.OnValueChanged += OnPlayer1HealthChanged;
        _manager.Player2Health.OnValueChanged += OnPlayer2HealthChanged;

        // Initial refresh
        OnPlayer1HealthChanged(0, _manager.Player1Health.Value);
        OnPlayer2HealthChanged(0, _manager.Player2Health.Value);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (_manager != null)
        {
            _manager.Player1Health.OnValueChanged -= OnPlayer1HealthChanged;
            _manager.Player2Health.OnValueChanged -= OnPlayer2HealthChanged;
        }
    }

    private void OnPlayer1HealthChanged(int previous, int current)
    {
        if (player1HealthText != null)
            player1HealthText.text = current.ToString();
    }

    private void OnPlayer2HealthChanged(int previous, int current)
    {
        if (player2HealthText != null)
            player2HealthText.text = current.ToString();
    }

    // Button hooks – can be clicked by any client

    public void OnPlayer1PlusClicked()
    {
        if (_manager != null)
            _manager.RequestAdjustHealth(PlayerSeat.SeatType.Player1, +1);
    }

    public void OnPlayer1MinusClicked()
    {
        if (_manager != null)
            _manager.RequestAdjustHealth(PlayerSeat.SeatType.Player1, -1);
    }

    public void OnPlayer2PlusClicked()
    {
        if (_manager != null)
            _manager.RequestAdjustHealth(PlayerSeat.SeatType.Player2, +1);
    }

    public void OnPlayer2MinusClicked()
    {
        if (_manager != null)
            _manager.RequestAdjustHealth(PlayerSeat.SeatType.Player2, -1);
    }
}
