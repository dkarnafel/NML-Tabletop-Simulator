// NetworkDeck.cs
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public class NetworkDeck : NetworkBehaviour
{
    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 20;

    [Header("Card Display Size (world units)")]
    [SerializeField] private Vector2 cardWorldSize = new Vector2(1.75f, 2.5f);

    [Header("Controls")]
    [SerializeField] private float rotationSpeed = 120f;

    [Header("Visuals")]
    [SerializeField] private Sprite cardBackSprite;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shuffleClip;

    private NetworkVariable<bool> _isFaceUp = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkList<int> _cardOrder;
    private NetworkList<FixedString128Bytes> _cardNames;

    [SerializeField] private NetworkObject cardPrefab;
    [SerializeField] private CardArtLibrary cardArtLibrary;
    [SerializeField] private Transform defaultSpawnPoint;

    private static readonly List<NetworkDeck> s_allDecks = new List<NetworkDeck>();

    private List<Sprite> _localCardFaces;
    private Camera _ownerCamera;
    private SpriteRenderer _renderer;
    private bool _isDragging;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        if (!string.IsNullOrEmpty(sortingLayerName))
            _renderer.sortingLayerName = sortingLayerName;
        _renderer.sortingOrder = sortingOrder;

        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = false;

        _cardOrder = new NetworkList<int>();
        _cardNames = new NetworkList<FixedString128Bytes>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!s_allDecks.Contains(this))
            s_allDecks.Add(this);

        _isFaceUp.OnValueChanged += OnFaceUpChanged;
        _cardOrder.OnListChanged += OnCardOrderChanged;
        _cardNames.OnListChanged += OnCardNamesChanged;

        RebuildLocalFacesFromNames();
        UpdateVisual();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        s_allDecks.Remove(this);

        _isFaceUp.OnValueChanged -= OnFaceUpChanged;
        _cardOrder.OnListChanged -= OnCardOrderChanged;
        _cardNames.OnListChanged -= OnCardNamesChanged;
    }

    public void InitializeDeckFromNamesClient(string deckName,
                                              FixedString128Bytes[] cardNames,
                                              Sprite backSprite,
                                              Camera ownerCamera)
    {
        _ownerCamera = ownerCamera;
        if (backSprite != null)
            cardBackSprite = backSprite;

        if (IsOwner)
            InitializeDeckFromNamesServerRpc(cardNames);
    }

    [ServerRpc(RequireOwnership = false)]
    private void InitializeDeckFromNamesServerRpc(FixedString128Bytes[] cardNames)
    {
        InitializeFromCardNames(cardNames);
    }

    public void InitializeFromCardNames(FixedString128Bytes[] cardNames)
    {
        _cardNames.Clear();
        foreach (var n in cardNames)
            _cardNames.Add(n);

        _cardOrder.Clear();
        for (int i = 0; i < _cardNames.Count; i++)
            _cardOrder.Add(i);

        _isFaceUp.Value = false;
        RebuildLocalFacesFromNames();
    }

    private void OnFaceUpChanged(bool oldValue, bool newValue)
    {
        UpdateVisual();
        DeckInspectorUI.Instance?.RefreshIfShowingDeck(this);
    }

    private void OnCardOrderChanged(NetworkListEvent<int> changeEvent)
    {
        UpdateVisual();
        DeckInspectorUI.Instance?.RefreshIfShowingDeck(this);
    }

    private void OnCardNamesChanged(NetworkListEvent<FixedString128Bytes> changeEvent)
    {
        RebuildLocalFacesFromNames();
        DeckInspectorUI.Instance?.RefreshIfShowingDeck(this);
    }

    private void RebuildLocalFacesFromNames()
    {
        if (CardArtLibrary.Instance == null)
        {
            Debug.LogWarning("[NetworkDeck] CardArtLibrary not found in scene.");
            return;
        }

        _localCardFaces = new List<Sprite>(_cardNames.Count);
        foreach (var name in _cardNames)
            _localCardFaces.Add(CardArtLibrary.Instance.GetSprite(name.ToString()));

        UpdateVisual();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (_ownerCamera == null)
            _ownerCamera = Camera.main;

        ForceSpriteToCardSize();

        HandleDragInput();
        HandleRotateInput();

        if (Input.GetKeyDown(KeyCode.V))
            ToggleFaceUpServerRpc();

        if (Input.GetKeyDown(KeyCode.C))
            ShuffleServerRpc(UnityEngine.Random.Range(int.MinValue, int.MaxValue));

        if (Input.GetKeyDown(KeyCode.R))
            RequestDrawCardServerRpc();

        if (IsMouseOverDeck() && Input.GetMouseButtonDown(1))
            OpenDeckInspectorForLocalClient();
    }

    private void HandleDragInput()
    {
        if (_ownerCamera == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorld = _ownerCamera.ScreenToWorldPoint(
                new Vector3(Input.mousePosition.x, Input.mousePosition.y, -_ownerCamera.transform.position.z));

            RaycastHit2D hit = Physics2D.Raycast((Vector2)mouseWorld, Vector2.zero);
            if (hit.collider != null && hit.collider.gameObject == gameObject)
                _isDragging = true;
        }

        if (Input.GetMouseButtonUp(0))
        {
            _isDragging = false;
            BoardZone.TrySnapToClosestZone(transform);
        }

        if (_isDragging)
        {
            Vector3 mouseWorld = _ownerCamera.ScreenToWorldPoint(
                new Vector3(Input.mousePosition.x, Input.mousePosition.y, -_ownerCamera.transform.position.z));

            mouseWorld.z = transform.position.z;
            transform.position = mouseWorld;
        }
    }

    private bool IsMouseOverDeck()
    {
        if (_ownerCamera == null) _ownerCamera = Camera.main;
        if (_ownerCamera == null) return false;

        Vector3 mouseWorld = _ownerCamera.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, -_ownerCamera.transform.position.z));

        RaycastHit2D hit = Physics2D.Raycast((Vector2)mouseWorld, Vector2.zero);
        return hit.collider != null && hit.collider.gameObject == gameObject;
    }

    private void HandleRotateInput()
    {
        if (!IsOwner) return;
        if (!IsMouseOverDeck()) return;

        float step = 30f;
        if (Input.GetKeyDown(KeyCode.Q)) RotateLocally(step);
        if (Input.GetKeyDown(KeyCode.E)) RotateLocally(-step);
    }

    private void RotateLocally(float degrees)
    {
        transform.Rotate(0f, 0f, degrees);
        float z = transform.eulerAngles.z;
        float snapped = Mathf.Round(z / 30f) * 30f;
        transform.rotation = Quaternion.Euler(0f, 0f, snapped);
    }

    [ServerRpc(RequireOwnership = true)]
    private void RequestDrawCardServerRpc(ServerRpcParams serverRpcParams = default)
    {
        if (_cardOrder == null || _cardOrder.Count == 0)
            return;

        int drawnCardIndex = _cardOrder[0];
        _cardOrder.RemoveAt(0);

        ulong ownerClientId = serverRpcParams.Receive.SenderClientId;

        var clientParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new List<ulong> { ownerClientId }
            }
        };

        FixedString128Bytes name = _cardNames[drawnCardIndex];
        GiveCardToHandClientRpc(drawnCardIndex, name, clientParams);
    }

    [ServerRpc(RequireOwnership = true)]
    private void ToggleFaceUpServerRpc()
    {
        _isFaceUp.Value = !_isFaceUp.Value;
    }

    [ServerRpc(RequireOwnership = true)]
    private void ShuffleServerRpc(int seed)
    {
        if (_cardOrder.Count <= 1)
            return;

        var rng = new System.Random(seed);
        for (int i = _cardOrder.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (_cardOrder[i], _cardOrder[j]) = (_cardOrder[j], _cardOrder[i]);
        }

        PlayShuffleSoundClientRpc();
    }

    private void UpdateVisual()
    {
        if (_renderer == null)
            return;

        if (!string.IsNullOrEmpty(sortingLayerName))
            _renderer.sortingLayerName = sortingLayerName;
        _renderer.sortingOrder = sortingOrder;

        bool faceUp = _isFaceUp.Value;

        if (!faceUp || _localCardFaces == null || _localCardFaces.Count == 0 || _cardOrder.Count == 0)
        {
            _renderer.sprite = cardBackSprite;
        }
        else
        {
            int topCardIndex = _cardOrder[0];
            if (topCardIndex >= 0 && topCardIndex < _localCardFaces.Count)
                _renderer.sprite = _localCardFaces[topCardIndex];
            else
                _renderer.sprite = cardBackSprite;
        }

        ForceSpriteToCardSize();
    }

    private void ForceSpriteToCardSize()
    {
        if (_renderer == null || _renderer.sprite == null) return;

        Vector2 spriteSize = _renderer.sprite.bounds.size;
        if (spriteSize.x <= 0 || spriteSize.y <= 0) return;

        float scaleX = cardWorldSize.x / spriteSize.x;
        float scaleY = cardWorldSize.y / spriteSize.y;

        transform.localScale = new Vector3(scaleX, scaleY, 1f);

        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            float localSizeX = cardWorldSize.x / scaleX;
            float localSizeY = cardWorldSize.y / scaleY;
            col.size = new Vector2(localSizeX, localSizeY);
            col.offset = Vector2.zero;
        }
    }

    [ClientRpc]
    private void GiveCardToHandClientRpc(int cardIndex, FixedString128Bytes cardName, ClientRpcParams clientRpcParams = default)
    {
        if (PlayerHandUI.Instance == null)
        {
            Debug.LogWarning("[NetworkDeck] PlayerHandUI.Instance not found.");
            return;
        }

        if (_localCardFaces == null || cardIndex < 0 || cardIndex >= _localCardFaces.Count)
        {
            Debug.LogWarning($"[NetworkDeck] Invalid cardIndex {cardIndex} for local card faces.");
            return;
        }

        Sprite cardSprite = _localCardFaces[cardIndex];
        PlayerHandUI.Instance.AddCardToHand(
            cardSprite,
            cardName.ToString(),
            cardIndex,
            NetworkManager.Singleton.LocalClientId);
    }

    [ClientRpc]
    private void PlayShuffleSoundClientRpc()
    {
        if (audioSource != null && shuffleClip != null)
            audioSource.PlayOneShot(shuffleClip);
    }

    // ✅ Names in CURRENT shuffled order
    public List<string> GetOrderedCardNames()
    {
        var list = new List<string>(_cardOrder.Count);
        for (int i = 0; i < _cardOrder.Count; i++)
        {
            int cardIndex = _cardOrder[i];
            if (cardIndex >= 0 && cardIndex < _cardNames.Count)
                list.Add(_cardNames[cardIndex].ToString());
        }
        return list;
    }

    public void OpenDeckInspectorForLocalClient()
    {
        if (!IsClient) return;
        if (DeckInspectorUI.Instance == null) return;

        Vector3 worldPos = transform.position;
        var col = GetComponent<Collider2D>();
        if (col != null)
            worldPos = new Vector3(col.bounds.center.x, col.bounds.max.y, transform.position.z);

        DeckInspectorUI.Instance.ShowForDeck(this, worldPos);
    }

    // ✅ UI click → remove from deck and add to hand (order-position based)
    public void RequestTakeCardToHandFromOrderPosition(int orderPos)
    {
        if (!IsClient) return;
        TakeCardToHandFromOrderServerRpc(orderPos);
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeCardToHandFromOrderServerRpc(int orderPos, ServerRpcParams rpcParams = default)
    {
        if (_cardOrder == null || _cardOrder.Count == 0)
            return;

        if (orderPos < 0 || orderPos >= _cardOrder.Count)
            return;

        int cardIndex = _cardOrder[orderPos];
        _cardOrder.RemoveAt(orderPos);

        ulong requester = rpcParams.Receive.SenderClientId;

        var clientParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new List<ulong> { requester }
            }
        };

        FixedString128Bytes name = _cardNames[cardIndex];
        GiveCardToHandClientRpc(cardIndex, name, clientParams);
    }

    public static NetworkDeck FindNearestDeck(Vector3 position, float maxRadius)
    {
        NetworkDeck best = null;
        float bestDistSq = maxRadius * maxRadius;

        for (int i = 0; i < s_allDecks.Count; i++)
        {
            var d = s_allDecks[i];
            if (d == null || !d.IsSpawned)
                continue;

            float distSq = (d.transform.position - position).sqrMagnitude;
            if (distSq <= bestDistSq)
            {
                bestDistSq = distSq;
                best = d;
            }
        }


        return best;
    }
    public void AddCardByName(Unity.Collections.FixedString128Bytes cardName, bool onTop)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[NetworkDeck] AddCardByName called on non-server.");
            return;
        }

        // Append to _cardNames
        _cardNames.Add(cardName);
        int newIndex = _cardNames.Count - 1;

        // Insert index into _cardOrder (top or bottom)
        if (onTop)
            _cardOrder.Insert(0, newIndex);
        else
            _cardOrder.Add(newIndex);

        // If your deck doesn't auto-refresh on list change, you can force:
        // RebuildLocalFacesFromNames();
        // UpdateVisual();
    }

    public void RequestDelete()
    {
        if (!IsSpawned) return;
        RequestDeleteServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDeleteServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong requester = rpcParams.Receive.SenderClientId;

        bool requesterIsHost = NetworkManager.Singleton != null &&
                               requester == NetworkManager.ServerClientId;

        bool requesterIsOwner = OwnerClientId == requester;

        if (!requesterIsHost && !requesterIsOwner)
            return;

        // Close inspector for everyone (optional but nice)
        DeckInspectorUI.Instance?.Hide();

        var no = GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned)
            no.Despawn(true);
    }
}
