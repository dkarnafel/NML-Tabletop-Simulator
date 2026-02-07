using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerSeat : NetworkBehaviour
{
    public enum SeatType
    {
        None,
        Player1,
        Player2,
        Spectator
    }

    [Header("Spectator Free Cam Settings")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minOrthoSize = 3f;
    [SerializeField] private float maxOrthoSize = 20f;

    [Header("Camera Zoom Settings")]
    [SerializeField] private float overviewOrthoSize = 10f;
    [SerializeField] private float playerSeatOrthoSize = 6f;
    [SerializeField] private float spectatorOrthoSize = 7f;

    [Header("Camera Rotation")]
    [SerializeField] private float rotationStepDegreesPerSecond = 90f; // Q/E rotate speed
    private bool _allowRotation = true;

    [Header("Seat Reset")]
    private Vector3 _seatStartPos;
    private Quaternion _seatStartRot;
    private float _seatStartOrthoSize;
    private bool _hasSeatStart;

    private Transform _overviewCamTransform;
    private Transform _player1CamTransform;
    private Transform _player2CamTransform;
    private Transform _spectatorCamStartTransform;

    public NetworkVariable<SeatType> CurrentSeat =
        new NetworkVariable<SeatType>(
            SeatType.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private Camera _cam;
    private bool _isSpectatorFreeCam;
    private bool _cameraPointsInitialized = false;

    private static readonly List<PlayerSeat> AllSeats = new List<PlayerSeat>();

    // Event that UIs can use to refresh button states.
    // IMPORTANT: This is invoked in OnSeatChanged, which runs on EVERY client
    // whenever the NetworkVariable value changes.
    public static event System.Action SeatsChanged;

    // ?????????????????????????????????????????????????????????????
    //  Lazy init of camera points (works across scenes)
    // ?????????????????????????????????????????????????????????????
    private void TryInitCameraPoints()
    {
        if (_cameraPointsInitialized)
            return;

        // Try singleton first, then search scene
        var reg = CameraPointRegistry.Instance ?? FindObjectOfType<CameraPointRegistry>();
        if (reg == null)
        {
            // Probably in lobby scene – no camera points yet
            return;
        }

        _overviewCamTransform = reg.overview;
        _player1CamTransform = reg.player1;
        _player2CamTransform = reg.player2;
        _spectatorCamStartTransform = reg.spectator;

        _cameraPointsInitialized = true;

        if (IsOwner)
        {
            if (_cam == null)
                _cam = Camera.main;

            ApplySeatCamera(CurrentSeat.Value);
        }
    }

    // ?????????????????????????????????????????????????????????????
    //  Netcode lifecycle
    // ?????????????????????????????????????????????????????????????
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (!AllSeats.Contains(this))
                AllSeats.Add(this);

            CurrentSeat.Value = SeatType.None; // start at overview
        }

        if (IsOwner)
        {
            _cam = Camera.main;
        }

        // Subscribe to NetworkVariable changes on ALL clients
        CurrentSeat.OnValueChanged += OnSeatChanged;

        // Try to hook up camera points (no-op in lobby)
        TryInitCameraPoints();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && AllSeats.Contains(this))
            AllSeats.Remove(this);

        CurrentSeat.OnValueChanged -= OnSeatChanged;
    }

    // ?????????????????????????????????????????????????????????????
    //  When seat changes (runs on every client)
    // ?????????????????????????????????????????????????????????????
    private void OnSeatChanged(SeatType previous, SeatType current)
    {
        // Make sure we have camera points in this scene
        TryInitCameraPoints();

        // Move the camera ONLY for the local owner of this player object
        if (IsOwner && _cameraPointsInitialized)
        {
            ApplySeatCamera(current);
        }

        // Notify all UIs on this client that something changed
        SeatsChanged?.Invoke();
    }

    private Quaternion GetBaseRotationForSeat(SeatType seat)
    {
        switch (seat)
        {
            case SeatType.Player1:
                // Player 1 looks “up” from the bottom – 180° around Z
                return Quaternion.Euler(0f, 0f, 180f);

            case SeatType.Player2:
                // Player 2 looks “down” from the top – default
                return Quaternion.identity;

            case SeatType.Spectator:
                return Quaternion.identity;

            case SeatType.None:
            default:
                // Overview default orientation
                return Quaternion.identity;
        }
    }


    private void ApplySeatCamera(SeatType seat)
    {
        if (!_cameraPointsInitialized)
        {
            Debug.Log($"[PlayerSeat] ({OwnerClientId}) ApplySeatCamera called but camera points not initialized.");
            return;
        }

        if (_cam == null)
            _cam = Camera.main;

        if (_cam == null)
        {
            Debug.LogWarning($"[PlayerSeat] ({OwnerClientId}) ApplySeatCamera: Camera.main is NULL");
            return;
        }

        Transform target = null;
        float targetOrthoSize = overviewOrthoSize;   // default
        Quaternion targetRotation = GetBaseRotationForSeat(seat);

        switch (seat)
        {
            case SeatType.Player1:
                target = _player1CamTransform;
                targetOrthoSize = playerSeatOrthoSize;
                // rotation already set by GetBaseRotationForSeat
                break;

            case SeatType.Player2:
                target = _player2CamTransform;
                targetOrthoSize = playerSeatOrthoSize;
                break;

            case SeatType.Spectator:
                target = _spectatorCamStartTransform != null
                    ? _spectatorCamStartTransform
                    : _overviewCamTransform;
                targetOrthoSize = spectatorOrthoSize;
                break;

            case SeatType.None:
            default:
                target = _overviewCamTransform;
                targetOrthoSize = overviewOrthoSize;
                break;
        }

        Debug.Log($"[PlayerSeat] ({OwnerClientId}) ApplySeatCamera seat={seat}, target={target}");

        if (target == null)
            return;

        // Keep camera's Z so it stays in front of the board
        Vector3 newPos = target.position;
        newPos.z = _cam.transform.position.z;    // usually -10
        _cam.transform.position = newPos;

        _cam.transform.rotation = targetRotation;

        if (_cam.orthographic)
            _cam.orthographicSize = targetOrthoSize;

        // Cache the "seat start" so Space can reset back here later
        _seatStartPos = _cam.transform.position;
        _seatStartRot = _cam.transform.rotation;
        _seatStartOrthoSize = _cam.orthographic ? _cam.orthographicSize : 0f;
        _hasSeatStart = true;

        // Everyone can move the camera (pan/zoom),
        // but rotation is controlled per-seat:
        // Only Overview (None) and Spectator can rotate.
        _isSpectatorFreeCam = true;
        _allowRotation = (seat == SeatType.Spectator || seat == SeatType.None);
    }

    private void Update()
    {
        // When we transition lobby ? match, this will eventually succeed
        if (!_cameraPointsInitialized)
            TryInitCameraPoints();

        if (!IsOwner || !_isSpectatorFreeCam || !_cameraPointsInitialized)
            return;

        // Space = reset back to seat-start camera, size 8
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ResetCameraToSeatStart(8f);
            return; // don't also pan/zoom in the same frame
        }
        HandleSpectatorFreeCam();
    }

    private void HandleSpectatorFreeCam()
    {
        if (_cam == null)
            _cam = Camera.main;
        if (_cam == null)
            return;

        // Get input
        float h = Input.GetAxisRaw("Horizontal"); // A/D or arrows
        float v = Input.GetAxisRaw("Vertical");   // W/S or arrows

        // Move relative to camera's local axes so controls
        // are not reversed when rotated.
        Vector3 right = _cam.transform.right;
        Vector3 up = _cam.transform.up;

        // Zero out any Z movement; keep it 2D
        right.z = 0f;
        up.z = 0f;

        Vector3 move = (right * h + up * v).normalized * moveSpeed * Time.deltaTime;
        _cam.transform.position += move;

        // Zoom with mouse wheel
        if (_cam.orthographic)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                _cam.orthographicSize -= scroll * zoomSpeed;
                _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize, minOrthoSize, maxOrthoSize);
            }
        }

        // Rotate with Q/E ONLY if this seat allows rotation
        if (_allowRotation)
        {
            float rotateDir = 0f;
            if (Input.GetKey(KeyCode.Q))
                rotateDir += 1f;
            if (Input.GetKey(KeyCode.E))
                rotateDir -= 1f;

            if (Mathf.Abs(rotateDir) > 0.01f)
            {
                float deltaAngle = rotateDir * rotationStepDegreesPerSecond * Time.deltaTime;
                _cam.transform.Rotate(0f, 0f, deltaAngle);
            }
        }
    }

    public void ResetCameraRotation()
    {
        if (!IsOwner)
            return;

        if (_cam == null)
            _cam = Camera.main;
        if (_cam == null)
            return;

        Quaternion baseRot = GetBaseRotationForSeat(CurrentSeat.Value);
        _cam.transform.rotation = baseRot;
    }

    // ?????????????????????????????????????????????????????????????
    //  Seat requests (called from UI)
    // ?????????????????????????????????????????????????????????????
    [ServerRpc(RequireOwnership = false)]
    public void RequestSeatServerRpc(SeatType requestedSeat, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"[PlayerSeat] RequestSeatServerRpc from client {rpcParams.Receive.SenderClientId} for seat {requestedSeat}");

        if (!IsServer)
            return;

        // Spectator / None always allowed
        if (requestedSeat == SeatType.Spectator || requestedSeat == SeatType.None)
        {
            CurrentSeat.Value = requestedSeat;
            return; // OnSeatChanged will fire on all clients
        }

        // Enforce one player for Player1 / Player2
        foreach (var ps in AllSeats)
        {
            if (ps != null && ps != this && ps.CurrentSeat.Value == requestedSeat)
            {
                Debug.Log($"[PlayerSeat] Seat {requestedSeat} already taken. Ignoring.");
                return;
            }
        }

        CurrentSeat.Value = requestedSeat;
        // No need to manually invoke event here – OnSeatChanged will run everywhere
    }

    // Returns the current seat for a given clientId.
    // Used by things like ResourceCardSpawner to know which side to spawn on.
    public static SeatType GetSeatForClient(ulong clientId)
    {
        foreach (var ps in AllSeats)
        {
            if (ps == null || !ps.IsSpawned)
                continue;

            if (ps.OwnerClientId == clientId)
                return ps.CurrentSeat.Value;
        }

        return SeatType.None;
    }

    public void ResetCameraToSeatStart(float forcedOrthoSize = 8f)
    {
        if (!IsOwner)
            return;

        if (_cam == null)
            _cam = Camera.main;
        if (_cam == null)
            return;

        if (!_hasSeatStart)
        {
            // Fallback: if we haven't cached yet, just re-apply seat camera
            ApplySeatCamera(CurrentSeat.Value);
        }

        // Reset position/rotation to the seat-start snapshot
        _cam.transform.position = _seatStartPos;
        _cam.transform.rotation = _seatStartRot;

        // Force size to 8 (your request)
        if (_cam.orthographic)
            _cam.orthographicSize = forcedOrthoSize;
    }
}
