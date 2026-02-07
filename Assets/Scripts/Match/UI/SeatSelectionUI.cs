using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SeatSelectionUI : MonoBehaviour
{
    [Header("Seat Buttons")]
    [SerializeField] private Button player1Button;
    [SerializeField] private Button player2Button;
    [SerializeField] private Button spectatorButton;
    [SerializeField] private Button resetViewButton;

    private PlayerSeat _localSeat;

    private void Awake()
    {
        if (player1Button != null)
            player1Button.onClick.AddListener(() => OnSeatButtonClicked(PlayerSeat.SeatType.Player1));

        if (player2Button != null)
            player2Button.onClick.AddListener(() => OnSeatButtonClicked(PlayerSeat.SeatType.Player2));

        if (spectatorButton != null)
            spectatorButton.onClick.AddListener(() => OnSeatButtonClicked(PlayerSeat.SeatType.Spectator));

        if (resetViewButton != null)
            resetViewButton.onClick.AddListener(OnResetViewClicked);
    }

    private void OnEnable()
    {
        StartCoroutine(FindLocalSeat());

        // Subscribe to seat change notifications on this client
        PlayerSeat.SeatsChanged += UpdateButtonInteractable;
    }

    private void OnDisable()
    {
        PlayerSeat.SeatsChanged -= UpdateButtonInteractable;
    }

    private IEnumerator FindLocalSeat()
    {
        Debug.Log("[SeatSelectionUI] Looking for local PlayerSeat...");

        while (_localSeat == null)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.SpawnManager != null)
            {
                var localObj = nm.SpawnManager.GetLocalPlayerObject();
                if (localObj != null)
                {
                    _localSeat = localObj.GetComponent<PlayerSeat>();
                    if (_localSeat == null)
                        _localSeat = localObj.GetComponentInChildren<PlayerSeat>();

                    if (_localSeat != null)
                        Debug.Log("[SeatSelectionUI] Found local PlayerSeat on object: " + localObj.name);
                }
            }

            yield return null;
        }

        // Initial state refresh
        UpdateButtonInteractable();
    }

    private void OnSeatButtonClicked(PlayerSeat.SeatType seat)
    {
        Debug.Log("[SeatSelectionUI] Seat button clicked: " + seat);

        if (_localSeat == null)
        {
            Debug.LogWarning("[SeatSelectionUI] Local PlayerSeat not ready yet. Click ignored.");
            return;
        }

        _localSeat.RequestSeatServerRpc(seat);
    }

    private void UpdateButtonInteractable()
    {
        if (player1Button == null || player2Button == null || spectatorButton == null)
            return;

        bool p1Taken = false;
        bool p2Taken = false;

        foreach (var ps in FindObjectsOfType<PlayerSeat>())
        {
            if (ps.CurrentSeat.Value == PlayerSeat.SeatType.Player1)
                p1Taken = true;

            if (ps.CurrentSeat.Value == PlayerSeat.SeatType.Player2)
                p2Taken = true;
        }

        player1Button.interactable = !p1Taken;
        player2Button.interactable = !p2Taken;
        spectatorButton.interactable = true;
    }

    private void OnResetViewClicked()
    {
        if (_localSeat == null)
        {
            Debug.LogWarning("[SeatSelectionUI] ResetView clicked but local PlayerSeat not ready.");
            return;
        }

        _localSeat.ResetCameraRotation();
    }
}
