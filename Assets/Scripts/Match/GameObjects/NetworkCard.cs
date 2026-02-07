using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.UI;   // (or TMPro if you prefer TMP)
using WebSocketSharp;
using System.Linq;
using UnityEngine.EventSystems;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(ClientNetworkTransform))]   // owner-authoritative
public class NetworkCard : NetworkBehaviour
{
    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Cards";
    [SerializeField] private int sortingOrder = 30;

    [Header("Pile UI")]
    [SerializeField] private TextMeshProUGUI pileCountText;   // assign in inspector
    [SerializeField] private float pileCounterRadius = 0.6f;

    [Header("Size (world units)")]
    [SerializeField] private Vector2 cardWorldSize = new Vector2(1.75f, 2.5f);

    [Header("Controls")]
    [SerializeField] private float rotationStepDegrees = 30f;

    [SerializeField] private float altZoomScale = 8.0f;
    private bool _isHoveringForZoom;

    [Header("Grouping")]
    [SerializeField] private float groupRadius = 1.0f;   // how close cards must be to move together
    private readonly List<NetworkCard> _dragGroup = new List<NetworkCard>();

    [Header("Pile Counter Badge")]
    [SerializeField] private GameObject pileCountBadgePrefab;
    [SerializeField] private float pileCountYOffset = 0.25f;
    [SerializeField] private Vector2 pileCountOffset = new Vector2(0.25f, 0.25f); // x/y nudge from corner
     
    private GameObject _pileBadgeInstance;
    private SpriteRenderer _pileBadgeBG;
    private TextMeshPro _pileBadgeText;

    [Header("Pile Cycle Controls")]
    [SerializeField] private PileCycleControls pileCycleControlsPrefab;
    [SerializeField] private Vector2 pileCycleControlsOffset = new Vector2(0.15f, 0f);

    private PileCycleControls _pileCycleControlsInstance;


    [Header("Physics Masks")]
    [SerializeField] private LayerMask cardInteractMask = ~0; // set in Inspector to exclude Deck layer
    [SerializeField] private LayerMask deckMask;              // set to Deck only

    [Header("Status Overlays")]
    [SerializeField] private GameObject exhaustIcon;     // child object
    [SerializeField] private TMP_Text buffText;          // child TMP
    [SerializeField] private TMPro.TMP_Text exhaustCountText; // NEW

    private readonly NetworkVariable<int> _exhaustCount =
    new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> _buffPower =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> _buffHealth =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private static readonly List<NetworkCard> s_allCards = new List<NetworkCard>();
    public FixedString128Bytes CardName => _cardName.Value;

    private bool _isAltZooming = false;
    private Vector3 _baseScale;

    private SpriteRenderer _renderer;
    private Camera _ownerCamera;
    private bool _isDragging;
    private bool _isShowingPileMenu;
    public virtual bool CanGroup => !TryGetComponent<NoPileGrouping>(out _);


    // Networked card name
    private NetworkVariable<FixedString128Bytes> _cardName =
        new NetworkVariable<FixedString128Bytes>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Server);

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        if (_renderer == null)
            _renderer = gameObject.AddComponent<SpriteRenderer>();
        _baseScale = transform.localScale;
        if (!string.IsNullOrEmpty(sortingLayerName))
            _renderer.sortingLayerName = sortingLayerName;
        _renderer.sortingOrder = sortingOrder;

        var col = GetComponent<BoxCollider2D>();
        if (col == null)
            col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!s_allCards.Contains(this))
            s_allCards.Add(this);

        _cardName.OnValueChanged += OnCardNameChanged;
        _exhaustCount.OnValueChanged += (_, __) => RefreshStatusOverlays();
        _buffPower.OnValueChanged += (_, __) => RefreshStatusOverlays();
        _buffHealth.OnValueChanged += (_, __) => RefreshStatusOverlays();

        RefreshStatusOverlays();
        UpdateVisual();
    }

    public override void OnNetworkDespawn() 
    {
        base.OnNetworkDespawn();
        s_allCards.Remove(this);
        _exhaustCount.OnValueChanged -= (_, __) => RefreshStatusOverlays(); // (optional: if you use lambdas, don't unsubscribe)
        _buffPower.OnValueChanged -= (_, __) => RefreshStatusOverlays();
        _buffHealth.OnValueChanged -= (_, __) => RefreshStatusOverlays();
        _cardName.OnValueChanged -= OnCardNameChanged;
    }

    private void OnCardNameChanged(FixedString128Bytes oldName, FixedString128Bytes newName)
    {
        UpdateVisual();
    }

    /// <summary>
    /// Called on the server after spawn to set the card's logical name.
    /// </summary>
    public void SetCardName(FixedString128Bytes name)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[NetworkCard] SetCardName called on non-server.");
            return;
        }

        _cardName.Value = name;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (_renderer == null)
            return;

        if (!string.IsNullOrEmpty(sortingLayerName))
            _renderer.sortingLayerName = sortingLayerName;
        _renderer.sortingOrder = sortingOrder;

        // Look up sprite via CardArtLibrary using the cardName
        if (CardArtLibrary.Instance != null && _cardName.Value.Length > 0)
        {
            Sprite s = CardArtLibrary.Instance.GetSprite(_cardName.Value.ToString());
            _renderer.sprite = s;
        }

        ForceSpriteToCardSize();
    }

    private void ForceSpriteToCardSize()
    {
        if (_renderer == null || _renderer.sprite == null)
            return;

        Vector2 spriteSize = _renderer.sprite.bounds.size;
        if (spriteSize.x <= 0 || spriteSize.y <= 0)
            return;

        float scaleX = cardWorldSize.x / spriteSize.x;
        float scaleY = cardWorldSize.y / spriteSize.y;

        transform.localScale = new Vector3(scaleX, scaleY, 1f);

        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            // Collider size is in local units, so world size = size * localScale.
            // We want world size = cardWorldSize, so:
            float localSizeX = cardWorldSize.x / scaleX;
            float localSizeY = cardWorldSize.y / scaleY;

            col.size = new Vector2(localSizeX, localSizeY);
            col.offset = Vector2.zero;
        }
    }

    private void Update()
    {
        if (!IsClient)
            return;

        if (_ownerCamera == null)
            _ownerCamera = Camera.main;

        // Runs for everyone (host + all clients). ResourceCard uses this.
        ClientTick();

        HandleAltZoom();
        HandlePileMenuRightClick();

        // Only the owner can drag/rotate/return/etc.
        if (!IsOwner)
            return;

        if (IsMouseOverCard() && Input.GetKeyDown(KeyCode.T))
        {
            if (PileInspectorUI.Instance != null) PileInspectorUI.Instance.Hide();
            ReturnGroupToDeckServerRpc();
        }

        if (IsMouseOverCard() && Input.GetKeyDown(KeyCode.G))
        {
            if (PileInspectorUI.Instance != null) PileInspectorUI.Instance.Hide();
            RequestReturnToHandServerRpc();
        }

       
        HandleDragInput();
        HandleRotateInput();
        HandleStatusMenuRightClick();
        UpdatePileCountBadge();
        OwnerTick();
    }

    // --- Input ---

    private void HandleDragInput()
    {
        if (_ownerCamera == null)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            if (IsMouseOverPileCycleControls())
                return;

            // Only the TOPMOST card under the cursor can start a drag.
            // This prevents piles + resource cards from both reacting to the same click.
            var top = GetTopmostCardUnderMouse(_ownerCamera, cardInteractMask);
            if (top != this)
                return;

            _isDragging = true;

            // ✅ If clicking on/over a deck OR anything marked NoPileGrouping, never form a drag group
            if (IsMouseOverDeck() || IsMouseOverNoPileObject())
            {
                _dragGroup.Clear();
                return;
            }

            if (CanGroup)
                BuildDragGroup();
            else
                _dragGroup.Clear();
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (_isDragging)
            {
                _isDragging = false;

                // ✅ Use overlap check (card vs deck), not mouse
                bool overDeck = IsOverDeckByOverlap();
                bool overNoPile = IsOverNoPileByOverlap();

                if (overDeck)
                {
                    _dragGroup.Clear();
                    PushOutOfDeckOverlap();
                    return;
                }

                if (overNoPile)
                {
                    _dragGroup.Clear();
                    PushOutOfNoPileOverlap();
                    return;
                }


                // Normal behavior when not over a deck
                BoardZone.TrySnapToClosestZone(transform);
                foreach (var c in _dragGroup)
                    BoardZone.TrySnapToClosestZone(c.transform);

                RequestSetAsTopOfPileServerRpc();

                _dragGroup.Clear();
            }
        }

        if (_isDragging)
        {
            Vector3 oldPos = transform.position;

            Vector3 mouseWorld = _ownerCamera.ScreenToWorldPoint(
                new Vector3(Input.mousePosition.x,
                            Input.mousePosition.y,
                            -_ownerCamera.transform.position.z));

            mouseWorld.z = transform.position.z;
            Vector3 delta = mouseWorld - oldPos;

            // Move this card
            transform.position = mouseWorld;

            // Move grouped cards by same delta
            foreach (var c in _dragGroup)
            {
                c.transform.position += delta;
            }
        }
    }

    private void BuildDragGroup()
    {
        _dragGroup.Clear();

        if (!CanGroup)
            return;

        foreach (var c in s_allCards)
        {
            if (c == null || c == this)
                continue;

            // Only group cards owned by the same player as THIS card
            if (c.OwnerClientId != OwnerClientId)
                continue;

            // Never group cards that opt out (ResourceCards)
            if (!c.CanGroup)
                continue;

            if (c is ResourceCard || c.GetComponent<ResourceCard>() != null)
                continue;

            float dist = Vector2.Distance(transform.position, c.transform.position);
            if (dist <= groupRadius)
                _dragGroup.Add(c);
        }
    }

    private void HandleRotateInput()
    {
        if (!IsMouseOverCard())
            return;

        if (Input.GetKeyDown(KeyCode.Q))
        {
            RotateLocally(rotationStepDegrees);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            RotateLocally(-rotationStepDegrees);
        }
    }
    private void HandleAltZoom()
    {
        if (CardZoomUI.Instance == null || CardArtLibrary.Instance == null)
            return;

        if (_ownerCamera == null)
            _ownerCamera = Camera.main;

        if (_ownerCamera == null)
            return;

        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (!alt)
            return; // CardZoomUI will auto-close when ALT is released

        if (PileInspectorUI.Instance != null && PileInspectorUI.Instance.IsPointerOverPanel)
        {
            // Mouse is over UI - don't respond
            return;
        }

        // ✅ Always pick the visually topmost card under the mouse.
        // In a pile, this guarantees we zoom the "top card" (after cycling).
        var top = GetTopmostCardUnderMouse(_ownerCamera, cardInteractMask);
        if (top == null || top != this)
            return;

        // Show THIS (topmost) card
        var sprite = CardArtLibrary.Instance.GetSprite(_cardName.Value.ToString());
        if (sprite != null)
            CardZoomUI.Instance.ShowZoom(sprite);
    }

    private void RotateLocally(float degrees)
    {
        transform.Rotate(0f, 0f, degrees);

        // Snap to grid of rotationStepDegrees
        float z = transform.eulerAngles.z;
        float snapped = Mathf.Round(z / rotationStepDegrees) * rotationStepDegrees;
        transform.rotation = Quaternion.Euler(0f, 0f, snapped);
    }

    private bool IsMouseOverCard()
    {
        if (_ownerCamera == null)
            _ownerCamera = Camera.main;
        if (_ownerCamera == null)
            return false;

        var col = GetComponent<Collider2D>();
        if (col == null)
            return false;

        Vector3 mouseWorld = _ownerCamera.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x,
                        Input.mousePosition.y,
                        -_ownerCamera.transform.position.z));

        // OverlapPoint checks ONLY this collider, ignoring others
        return col.OverlapPoint(mouseWorld);
    }

    [ServerRpc(RequireOwnership = true)]
    private void RequestReturnToHandServerRpc(ServerRpcParams serverRpcParams = default)
    {
        ulong ownerClientId = serverRpcParams.Receive.SenderClientId;

        // store name before despawn
        FixedString128Bytes name = _cardName.Value;

        // tell that owner to add this card back to their hand
        var clientParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new System.Collections.Generic.List<ulong> { ownerClientId }
            }
        };

        ReturnToHandClientRpc(name, clientParams);

        // despawn board card for everyone
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn(true);
    }

    [ClientRpc]
    private void ReturnToHandClientRpc(FixedString128Bytes cardName, ClientRpcParams clientRpcParams = default)
    {
        if (PlayerHandUI.Instance == null)
        {
            Debug.LogWarning("[NetworkCard] PlayerHandUI.Instance not found on this client.");
            return;
        }

        PlayerHandUI.Instance.AddCardToHandFromName(cardName.ToString());
        PileInspectorUI.Instance?.CloseIfShowingCard(this);
    }

    [ServerRpc(RequireOwnership = true)]
    private void ReturnGroupToDeckServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        float deckSearchRadius = 5f;
        NetworkDeck deck = NetworkDeck.FindNearestDeck(transform.position, deckSearchRadius);
        if (deck == null)
        {
            Debug.LogWarning("[NetworkCard] No deck nearby to return cards to.");
            return;
        }

        float groupRadiusForReturn = groupRadius;
        List<NetworkCard> group = FindGroupAround(transform.position, groupRadiusForReturn, sender);

        if (!group.Contains(this))
            group.Add(this);

        foreach (var card in group)
        {
            if (card == null || !card.IsSpawned)
                continue;

            var netObj = card.GetComponent<NetworkObject>();
            if (netObj == null || !netObj.IsSpawned)
                continue;

            // card name stored as FixedString128Bytes in NetworkVariable
            var name = card._cardName.Value;

            // server adds it back
            deck.AddCardByName(name, onTop: false);

            // remove from board
            netObj.Despawn(true);
            PileInspectorUI.Instance?.CloseIfShowingCard(this);
        }
    }

    private static List<NetworkCard> FindGroupAround(Vector3 center, float radius, ulong ownerClientId)
    {
        var result = new List<NetworkCard>();
        float rSq = radius * radius;

        foreach (var c in s_allCards)
        {
            if (c == null || !c.IsSpawned)
                continue;

            // Only include cards owned by that client
            if (c.OwnerClientId != ownerClientId)
                continue;

            if (!c.CanGroup) continue;
            if (c is ResourceCard || c.GetComponent<ResourceCard>() != null) continue;

            float distSq = (c.transform.position - center).sqrMagnitude;
            if (distSq <= rSq)
            {
                result.Add(c);
            }
        }

        return result;
    }

    private static Vector3 ComputePileCenter(List<NetworkCard> pile)
    {
        if(pile == null || pile.Count == 0)
        return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (var c in pile)
        {
            if (c != null)
                sum += c.transform.position;
        }

        Vector3 center = sum / pile.Count;
        // Keep same Z plane as the board/cards
        center.z = pile[0].transform.position.z;
        return center;
    }

    private static List<NetworkCard> FindPileForOwnerNonResource(Vector3 center, float radius, ulong ownerClientId)
    {
        var result = new List<NetworkCard>();
        float rSq = radius * radius;

        foreach (var c in s_allCards)
        {
            if (c == null || !c.IsSpawned)
                continue;

            // Only include cards owned by the same player
            if (c.OwnerClientId != ownerClientId)
                continue;

            if (!c.CanGroup) // 🔒 HARD BLOCK
                continue;

            // Safety: never include resource cards (in case you ever add shared components later)
            if (c is ResourceCard || c.GetComponent<ResourceCard>() != null)
                continue;

            float distSq = (c.transform.position - center).sqrMagnitude;
            if (distSq <= rSq)
                result.Add(c);
        }

        return result;
    }

    private void HandlePileMenuRightClick()
    {
        var pileUI = PileInspectorUI.Instance;
        if (pileUI == null)
            return;

        // make sure camera exists
        if (_ownerCamera == null)
            _ownerCamera = Camera.main;

        if (_ownerCamera == null)
            return;

        // Only react on the frame the right mouse is pressed
        if (!Input.GetMouseButtonDown(1))
            return;

        // ✅ Only the visually topmost card under the mouse handles the click
        var top = GetTopmostCardUnderMouse(_ownerCamera, cardInteractMask);
        if (top == null || top != this)
            return;

        // Must actually be over THIS card
        if (!IsMouseOverCard())
            return;

        float pileRadius = pileCounterRadius;

        var pile = FindPileForOwnerNonResource(transform.position, pileRadius, OwnerClientId);

        if (pile.Count > 1)
        {
            Vector3 center = ComputePileCenter(pile);
            pileUI.ShowForPile(pile, center);
        }
        else
        {
            pileUI.Hide();
        }
    }

    private void HandleStatusMenuRightClick()
    {
        var menu = CardStatusMenuUI.Instance;
        if (menu == null) return;

        if (_ownerCamera == null)
            _ownerCamera = Camera.main;
        if (_ownerCamera == null) return;

        // right-click only
        if (!Input.GetMouseButtonDown(1))
            return;

        // must be over THIS card, and must be the topmost card under cursor
        var top = GetTopmostCardUnderMouse(_ownerCamera, cardInteractMask);
        if (top == null || top != this) return;
        if (!IsMouseOverCard()) return;

        // ✅ Do NOT show for Resource cards
        if (this is ResourceCard || GetComponent<ResourceCard>() != null)
            return;

        // ✅ Do NOT show for piles (only single cards)
        // (Your pile rule is count > 1 in pile around this card)
        var pile = FindPileForOwnerNonResource(transform.position, pileCounterRadius, OwnerClientId);
        if (pile != null && pile.Count > 1)
            return;

        // ✅ Do NOT show for decks (block if overlapping deck)
        if (IsMouseOverDeck())
            return;

        menu.ShowForCard(this, transform.position);
    }

    private static NetworkCard GetTopmostCardUnderMouse(Camera cam, LayerMask mask)
    {
        if (cam == null)
            return null;

        Vector3 mouseWorld3 = cam.ScreenToWorldPoint(new Vector3(
            Input.mousePosition.x,
            Input.mousePosition.y,
            -cam.transform.position.z));

        Vector2 mouseWorld = new Vector2(mouseWorld3.x, mouseWorld3.y);

        // Grab everything at the mouse point
        Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorld, mask);

        NetworkCard best = null;
        int bestOrder = int.MinValue;
        float bestY = float.MinValue;

        foreach (var h in hits)
        {
            if (h == null)
                continue;

            var card = h.GetComponentInParent<NetworkCard>();
            if (card == null || !card.IsSpawned)
                continue;

            // Choose the visually topmost card by sortingOrder, then Y
            var sr = card.GetComponent<SpriteRenderer>();
            int order = sr != null ? sr.sortingOrder : 0;
            float y = card.transform.position.y;

            if (best == null || order > bestOrder || (order == bestOrder && y > bestY))
            {
                best = card;
                bestOrder = order;
                bestY = y;
            }
        }

        return best;
    }

    private void UpdatePileCountBadge()
    {
        // purely visual, client-side
        if (!IsClient)
            return;

        // If you have a "CanGroup" flag, non-groupables never show pile counts
        if (!CanGroup)
        {
            HidePileBadge();
            HidePileCycleControls();
            return;
        }

        // Find pile around THIS card (owner-restricted pile)
        var pile = FindPileForOwnerNonResource(transform.position, pileCounterRadius, OwnerClientId);
        int count = (pile != null) ? pile.Count : 0;

        // No pile / single card => nothing to show
        if (count <= 1)
        {
            HidePileBadge();
            HidePileCycleControls();
            return;
        }

        // Only the TOPMOST card in the pile shows the badge + arrows
        var top = GetTopmostCardInList(pile);
        if (top != this)
        {
            HidePileBadge();
            HidePileCycleControls();
            return;
        }

        // We need a collider to position UI near the card
        var col = GetComponent<Collider2D>();
        if (col == null)
        {
            HidePileBadge();
            HidePileCycleControls();
            return;
        }

        // Ensure visuals exist
        EnsurePileBadge();
        EnsurePileCycleControls();

        // Update badge text
        if (_pileBadgeText != null)
            _pileBadgeText.text = count.ToString();

        // Determine viewer orientation (seat flip)
        bool isLocalSeatPlayer1 = false;
        if (NetworkManager.Singleton != null)
        {
            var localSeat = PlayerSeat.GetSeatForClient(NetworkManager.Singleton.LocalClientId);
            isLocalSeatPlayer1 = (localSeat == PlayerSeat.SeatType.Player1);
        }

        float edgeY = isLocalSeatPlayer1 ? col.bounds.min.y : col.bounds.max.y;
        float signY = isLocalSeatPlayer1 ? -1f : 1f;

        // Place badge near "top-right" from the viewer perspective
        float badgeX = col.bounds.max.x + pileCountOffset.x;
        float badgeY = edgeY + (pileCountYOffset * signY);

        if (_pileBadgeInstance != null)
        {
            _pileBadgeInstance.transform.position = new Vector3(badgeX, badgeY, transform.position.z);
            OrientPileBadgeToCamera();
            _pileBadgeInstance.SetActive(true);
        }

        // Sorting so badge/arrows render above the card/board
        var cardSR = GetComponent<SpriteRenderer>();
        int baseOrder = (cardSR != null) ? cardSR.sortingOrder : 0;

        // Badge text render order (MeshRenderer)
        if (_pileBadgeText != null && cardSR != null)
        {
            var mr = _pileBadgeText.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingLayerID = cardSR.sortingLayerID;
                mr.sortingOrder = baseOrder + 101;
            }
        }

        // Place + sort + rotate the cycle controls (SpriteRenderer children)
        if (_pileCycleControlsInstance != null)
        {
            // Position cycle controls to the right of the card
            // how “wide” the card is in world units
            float cx = col.bounds.max.x + pileCycleControlsOffset.x;
            float cy = col.bounds.center.y + pileCycleControlsOffset.y;
            _pileCycleControlsInstance.transform.position = new Vector3(cx, cy, transform.position.z);

            // Sorting: push all arrow sprites above the card
            if (cardSR != null)
            {
                var srs = _pileCycleControlsInstance.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var sr in srs)
                {
                    if (sr == null) continue;
                    sr.sortingLayerID = cardSR.sortingLayerID;
                    sr.sortingOrder = baseOrder + 120;
                }
            }

            // Rotate to match camera (important for Player1 flipped view)
            OrientPileCycleControlsToCamera();

            // Bind to this top card and show
            _pileCycleControlsInstance.Bind(this);
            _pileCycleControlsInstance.gameObject.SetActive(true);
        }
        else
        {
            HidePileCycleControls();
        }
    }


    private void EnsurePileBadge()
    {
        if (_pileBadgeInstance != null)
            return;

        if (pileCountBadgePrefab == null)
        {
            Debug.LogWarning("[NetworkCard] pileCountBadgePrefab not assigned.");
            return;
        }

        _pileBadgeInstance = Instantiate(pileCountBadgePrefab);
        _pileBadgeInstance.transform.SetParent(transform, true); // ✅ parent to card so it dies with it
        _pileBadgeInstance.name = "PileCountBadge_Instance";

        _pileBadgeBG = _pileBadgeInstance.transform.Find("BadgeBG")?.GetComponent<SpriteRenderer>();
        _pileBadgeText = _pileBadgeInstance.transform.Find("BadgeText")?.GetComponent<TextMeshPro>();

        _pileBadgeInstance.SetActive(false);
    }

    private void HidePileBadge()
    {
        if (_pileBadgeInstance != null)
            _pileBadgeInstance.SetActive(false);
    }

    private NetworkCard GetTopmostCardInList(System.Collections.Generic.List<NetworkCard> pile)
    {
        NetworkCard best = null;
        int bestOrder = int.MinValue;
        float bestY = float.MinValue;

        foreach (var c in pile)
        {
            if (c == null) continue;
            var sr = c.GetComponent<SpriteRenderer>();
            int order = sr != null ? sr.sortingOrder : 0;
            float y = c.transform.position.y;

            if (best == null || order > bestOrder || (order == bestOrder && y > bestY))
            {
                best = c;
                bestOrder = order;
                bestY = y;
            }
        }

        return best;
    }

    private void OrientPileBadgeToCamera()
    {
        if (_pileBadgeInstance == null || _ownerCamera == null)
            return;

        // Keep badge flat in 2D but match camera rotation
        _pileBadgeInstance.transform.rotation =
            Quaternion.Euler(0f, 0f, _ownerCamera.transform.eulerAngles.z);
    }

    private bool IsMouseOverDeck()
    {
        if (_ownerCamera == null) _ownerCamera = Camera.main;
        if (_ownerCamera == null) return false;

        Vector3 mouseWorld = _ownerCamera.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, -_ownerCamera.transform.position.z));

        RaycastHit2D hit = Physics2D.Raycast((Vector2)mouseWorld, Vector2.zero, 0f, deckMask);
        if (hit.collider == null) return false;

        return hit.collider.GetComponentInParent<NetworkDeck>() != null;
    }


    private bool IsOverDeckByOverlap()
    {
        var myCol = GetComponent<Collider2D>();
        if (myCol == null) return false;

        Bounds b = myCol.bounds;

        // Only check against the Deck layer
        Collider2D[] hits = Physics2D.OverlapBoxAll(b.center, b.size, 0f, deckMask);
        return hits != null && hits.Length > 0;
    }


    private void PushOutOfDeckOverlap()
    {
        var myCol = GetComponent<Collider2D>();
        if (myCol == null) return;

        Bounds b = myCol.bounds;
        Collider2D[] hits = Physics2D.OverlapBoxAll(b.center, b.size, 0f, deckMask);
        if (hits == null || hits.Length == 0) return;

        // Push away from the first deck collider we overlap
        var deckCol = hits[0];
        Vector3 dir = (transform.position - deckCol.bounds.center);
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.up;

        dir.Normalize();
        transform.position += dir * 0.35f;
    }

    private bool IsMouseOverNoPileObject()
    {
        if (_ownerCamera == null) _ownerCamera = Camera.main;
        if (_ownerCamera == null) return false;

        Vector3 mouseWorld = _ownerCamera.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x, Input.mousePosition.y, -_ownerCamera.transform.position.z));

        Collider2D hit = Physics2D.OverlapPoint(mouseWorld, cardInteractMask);
        if (hit == null) return false;

        // If the thing under the mouse has NoPileGrouping anywhere up its hierarchy → block grouping
        return hit.GetComponentInParent<NoPileGrouping>() != null;
    }

    private bool IsOverNoPileByOverlap()
    {
        var myCol = GetComponent<Collider2D>();
        if (myCol == null) return false;

        Bounds b = myCol.bounds;
        Collider2D[] hits = Physics2D.OverlapBoxAll(b.center, b.size, 0f, cardInteractMask);

        foreach (var h in hits)
        {
            if (h == null || h == myCol) continue;
            if (h.GetComponentInParent<NoPileGrouping>() != null)
                return true;
        }

        return false;
    }

    private void PushOutOfNoPileOverlap()
    {
        var myCol = GetComponent<Collider2D>();
        if (myCol == null) return;

        Bounds b = myCol.bounds;
        Collider2D[] hits = Physics2D.OverlapBoxAll(b.center, b.size, 0f);

        foreach (var h in hits)
        {
            if (h == null || h == myCol) continue;

            var blocker = h.GetComponentInParent<NoPileGrouping>();
            if (blocker == null) continue;

            var blockerCol = blocker.GetComponent<Collider2D>();
            if (blockerCol == null) blockerCol = h; // fall back to the collider we hit

            Vector3 dir = (transform.position - blockerCol.bounds.center);
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.up;

            dir.Normalize();

            float pushDist = 0.35f; // tune
            transform.position += dir * pushDist;
            return;
        }
    }


    public static bool IsMouseOverPile(Camera cam, LayerMask mask, out NetworkCard topCard)
    {
        topCard = null;
        if (cam == null) return false;

        // Reuse your existing logic
        var card = GetTopmostCardUnderMouse(cam, mask);
        if (card == null) return false;

        topCard = card;

        // If your “pile” is identified by having more than 1 card in its pile list
        // (or a flag you already have), use that condition here:
        return card.IsInPile(); // <-- implement or replace with your pile condition
    }

    public bool IsInPile()
    {
        var pile = FindPileForOwnerNonResource(transform.position, groupRadius, OwnerClientId);
        return pile != null && pile.Count > 1;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestCyclePileServerRpc(bool up, ServerRpcParams rpcParams = default)
    {
        var pile = FindPileForOwnerNonResource(transform.position, pileCounterRadius, OwnerClientId);
        if (pile == null || pile.Count < 2)
            return;

        // Sort by current order (lowest -> highest), top is last
        pile.Sort((a, b) =>
        {
            int ao = a._renderer != null ? a._renderer.sortingOrder : 0;
            int bo = b._renderer != null ? b._renderer.sortingOrder : 0;
            return ao.CompareTo(bo);
        });

        if (up)
        {
            // bring bottom to top
            var bottom = pile[0];
            pile.RemoveAt(0);
            pile.Add(bottom);
        }
        else
        {
            // bring top to bottom
            var top = pile[pile.Count - 1];
            pile.RemoveAt(pile.Count - 1);
            pile.Insert(0, top);
        }

        // Re-assign sequential sorting orders starting from current min
        int baseOrder = pile[0]._renderer != null ? pile[0]._renderer.sortingOrder : 30;

        ulong[] ids = new ulong[pile.Count];
        int[] orders = new int[pile.Count];

        for (int i = 0; i < pile.Count; i++)
        {
            int order = baseOrder + i;
            ids[i] = pile[i].NetworkObjectId;
            orders[i] = order;

            if (pile[i]._renderer != null) pile[i]._renderer.sortingOrder = order;
            pile[i].sortingOrder = order;
        }

        ApplyPileOrderClientRpc(ids, orders);
    }

    [ClientRpc]
    private void ApplyPileOrderClientRpc(ulong[] ids, int[] orders)
    {
        if (NetworkManager.Singleton == null)
            return;

        for (int i = 0; i < ids.Length && i < orders.Length; i++)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(ids[i], out var no))
            {
                var card = no.GetComponent<NetworkCard>();
                if (card != null)
                {
                    card.sortingOrder = orders[i];
                    if (card._renderer != null)
                        card._renderer.sortingOrder = orders[i];

                    // Keep badge render above the top card
                    card.UpdatePileCountBadge();
                }
            }
        }
    }

    public void RequestCyclePile(bool up)
    {
        // You can add any local validation here (pile count > 1, is top card, etc.)
        RequestCyclePileServerRpc(up);
    }

    private void EnsurePileCycleControls()
    {
        if (_pileCycleControlsInstance != null) return;
        if (pileCycleControlsPrefab == null) return;

        _pileCycleControlsInstance = Instantiate(pileCycleControlsPrefab);
        _pileCycleControlsInstance.transform.SetParent(transform, true);  // ✅ important
        _pileCycleControlsInstance.name = "PileCycleControls_Instance";
        // Not parented to the card -> avoids compound collider issues
        _pileCycleControlsInstance.gameObject.SetActive(false);
    }

    private void HidePileCycleControls()
    {
        if (_pileCycleControlsInstance != null)
            _pileCycleControlsInstance.gameObject.SetActive(false);
    }

    // Add this method near your pile/cycle code:

    // When a card is dropped onto an existing pile, ensure this card becomes the visually topmost.
    // This preserves the intuitive "latest added card is on top" rule.
    [ServerRpc(RequireOwnership = true)]
    private void RequestSetAsTopOfPileServerRpc(ServerRpcParams rpcParams = default)
    {
        var pile = FindPileForOwnerNonResource(transform.position, pileCounterRadius, OwnerClientId);
        if (pile == null || pile.Count < 2)
            return;

        // De-dupe (defensive)
        var unique = new List<NetworkCard>(pile.Count);
        var seen = new HashSet<ulong>();
        foreach (var c in pile)
        {
            if (c == null || !c.IsSpawned) continue;
            if (seen.Add(c.NetworkObjectId))
                unique.Add(c);
        }

        // Find this card in the pile
        NetworkCard me = null;
        for (int i = 0; i < unique.Count; i++)
        {
            if (unique[i] != null && unique[i].NetworkObjectId == NetworkObjectId)
            {
                me = unique[i];
                unique.RemoveAt(i);
                break;
            }
        }

        if (me == null)
            return;

        // Sort by current visual order (bottom -> top)
        unique.Sort((a, b) =>
        {
            int ao = a._renderer != null ? a._renderer.sortingOrder : 0;
            int bo = b._renderer != null ? b._renderer.sortingOrder : 0;
            int cmp = ao.CompareTo(bo);
            if (cmp != 0) return cmp;
            return a.transform.position.y.CompareTo(b.transform.position.y);
        });

        // Put this card last => topmost
        unique.Add(me);

        // Choose a stable base order for the pile
        int baseOrder = 30;
        for (int i = 0; i < unique.Count; i++)
        {
            if (unique[i]?._renderer != null)
                baseOrder = Mathf.Min(baseOrder, unique[i]._renderer.sortingOrder);
        }

        ulong[] ids = new ulong[unique.Count];
        int[] orders = new int[unique.Count];

        for (int i = 0; i < unique.Count; i++)
        {
            int order = baseOrder + i;
            ids[i] = unique[i].NetworkObjectId;
            orders[i] = order;

            if (unique[i]._renderer != null) unique[i]._renderer.sortingOrder = order;
            unique[i].sortingOrder = order;
        }

        ApplyPileOrderClientRpc(ids, orders);
    }

    private void OrientPileCycleControlsToCamera()
    {
        if (_pileCycleControlsInstance == null || _ownerCamera == null)
            return;

        _pileCycleControlsInstance.transform.rotation =
            Quaternion.Euler(0f, 0f, _ownerCamera.transform.eulerAngles.z);
    }

    private bool IsMouseOverPileCycleControls()
    {
        if (_pileCycleControlsInstance == null) return false;

        var cam = _ownerCamera != null ? _ownerCamera : Camera.main;
        if (cam == null) return false;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 p = new Vector2(mouseWorld.x, mouseWorld.y);

        // If the controls have any Collider2D on them (or children), detect overlap
        var cols = _pileCycleControlsInstance.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols)
        {
            if (c != null && c.OverlapPoint(p))
                return true;
        }

        return false;
    }

    public void RequestReturnToHandFromUI()
    {
        if (!IsOwner) return;      // only the owner can request
        RequestReturnToHandServerRpc();
    }

    private void RefreshStatusOverlays()
    {
        int ex = _exhaustCount.Value;

        if (exhaustIcon != null)
            exhaustIcon.SetActive(ex > 0);

        if (exhaustCountText != null)
        {
            // Only show a number for 2+
            bool showNum = ex >= 2;
            exhaustCountText.gameObject.SetActive(showNum);
            if (showNum) exhaustCountText.text = ex.ToString();
        }

        if (buffText != null)
        {
            int p = _buffPower.Value;
            int h = _buffHealth.Value;

            bool show = (p != 0 || h != 0);
            buffText.gameObject.SetActive(show);

            if (show)
            {
                // +X/+X formatting
                string ps = p >= 0 ? $"+{p}" : p.ToString();
                string hs = h >= 0 ? $"+{h}" : h.ToString();
                buffText.text = $"{ps}/{hs}";
            }
        }
    }

    public void AddExhaustFromUI()
    {
        if (!IsOwner) return;
        AddExhaustServerRpc(1);
    }

    public void RemoveExhaustFromUI()
    {
        if (!IsOwner) return;
        AddExhaustServerRpc(-1);
    }

    [ServerRpc(RequireOwnership = true)]
    private void AddExhaustServerRpc(int delta)
    {
        int next = _exhaustCount.Value + delta;
        _exhaustCount.Value = Mathf.Clamp(next, 0, 99);
    }

    // Set buff by delta
    public void AddBuffFromUI(int deltaPower, int deltaHealth)
    {
        if (!IsOwner) return;
        AddBuffServerRpc(deltaPower, deltaHealth);
    }

    [ServerRpc(RequireOwnership = true)]
    private void AddBuffServerRpc(int deltaPower, int deltaHealth)
    {
        _buffPower.Value += deltaPower;
        _buffHealth.Value += deltaHealth;

        // Optional clamp so it doesn't go crazy
        _buffPower.Value = Mathf.Clamp(_buffPower.Value, -99, 99);
        _buffHealth.Value = Mathf.Clamp(_buffHealth.Value, -99, 99);
    }

    // Optional: clear
    public void ClearBuffFromUI()
    {
        if (!IsOwner) return;
        ClearBuffServerRpc();
    }

    [ServerRpc(RequireOwnership = true)]
    private void ClearBuffServerRpc()
    {
        _buffPower.Value = 0;
        _buffHealth.Value = 0;
    }

    // Overridable hooks
    protected virtual void ClientTick() { }
    protected virtual void OwnerTick() { }
}
