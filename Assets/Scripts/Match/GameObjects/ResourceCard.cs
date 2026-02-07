using Unity.Netcode;
using UnityEngine;

public class ResourceCard : NetworkCard
{
    public enum ResourceType : byte
    {
        None = 0,
        Primal = 1,
        Arcane = 2,
        Refined = 3,
        Tech = 4
    }

    [Header("Sprites")]
    [SerializeField] private Sprite backSprite;
    [SerializeField] private Sprite primalSprite;
    [SerializeField] private Sprite arcaneSprite;
    [SerializeField] private Sprite refinedSprite;
    [SerializeField] private Sprite techSprite;

    [Header("Menu Position")]
    [SerializeField] private float menuWorldYOffset = 0.1f; // how far above card in world units  

    [Header("Size")]
    [SerializeField] private Vector2 targetWorldSize = new Vector2(5f, 6.5f); // tweak to match your normal cards

    private SpriteRenderer _renderer;
    private Camera _cam;
    private BoxCollider2D _collider;
    public override bool CanGroup => false;

    private bool _isShowingMenu;

    // Networked resource type
    private NetworkVariable<ResourceType> _resourceType =
        new NetworkVariable<ResourceType>(
            ResourceType.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _renderer = GetComponent<SpriteRenderer>();
        _cam = Camera.main;
        _collider = GetComponent<BoxCollider2D>();

        // One subscription only
        _resourceType.OnValueChanged += OnResourceTypeChanged;

        // Apply current sprite and size once at spawn
        OnResourceTypeChanged(ResourceType.None, _resourceType.Value);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        _resourceType.OnValueChanged -= OnResourceTypeChanged;
    }

    protected override void ClientTick()
    {
        // Keep menu working for everyone (not just owner)
        if (_cam == null)
            _cam = Camera.main;

        HandleRightClickMenu();
    }

    /// <summary>Called by the spawner on the server after creation.</summary>
    public void InitializeAsBackServer()
    {
        if (!IsServer)
            return;

        _resourceType.Value = ResourceType.None;
    }

    private void OnResourceTypeChanged(ResourceType previous, ResourceType current)
    {
        if (_renderer == null)
            _renderer = GetComponent<SpriteRenderer>();
        if (_renderer == null)
            return;

        switch (current)
        {
            case ResourceType.Primal:
                _renderer.sprite = primalSprite;
                break;
            case ResourceType.Arcane:
                _renderer.sprite = arcaneSprite;
                break;
            case ResourceType.Refined:
                _renderer.sprite = refinedSprite;
                break;
            case ResourceType.Tech:
                _renderer.sprite = techSprite;
                break;
            default:
                _renderer.sprite = backSprite;
                break;
        }

        // IMPORTANT: make this sprite match the same target size as all others
        TryAutoScaleToTargetSize();
    }

    // Called by the UI when a button is pressed
    public void RequestSetResourceType(ResourceType type)
    {
        if (!IsSpawned)
            return;

        SetResourceTypeServerRpc(type);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetResourceTypeServerRpc(ResourceType type, ServerRpcParams rpcParams = default)
    {
        _resourceType.Value = type;
    }

    //private void HandleHoverMenu()
    //{
    //    var menu = ResourceTypeMenuUI.Instance;
    //    if (menu == null || _cam == null)
    //        return;

    //    bool hoverCard = IsMouseOverCard();
    //    bool hoverPanel = menu.IsPointerOverPanel;

    //    if (hoverCard)
    //    {
    //        // Determine local viewer's seat (not the card owner)
    //        bool isLocalSeatPlayer1 = false;
    //        if (NetworkManager.Singleton != null)
    //        {
    //            var localSeat = PlayerSeat.GetSeatForClient(
    //                NetworkManager.Singleton.LocalClientId);
    //            isLocalSeatPlayer1 = (localSeat == PlayerSeat.SeatType.Player1);
    //        }

    //        // Use collider top or bottom depending on viewer
    //        float edgeY;
    //        float offsetSign;

    //        if (_collider != null)
    //        {
    //            if (isLocalSeatPlayer1)
    //            {
    //                // Seat 1 camera is flipped: "top" on screen is world bottom
    //                edgeY = _collider.bounds.min.y;
    //                offsetSign = -0.7f;
    //            }
    //            else
    //            {
    //                // Normal: top on screen is world top
    //                edgeY = _collider.bounds.max.y;
    //                offsetSign = 0.7f;
    //            }

    //            Vector3 edgeWorld = new Vector3(
    //                _collider.bounds.center.x,
    //                edgeY,
    //                transform.position.z);

    //            Vector3 worldPos = edgeWorld + new Vector3(0f, menuWorldYOffset * offsetSign, 0f);

    //            menu.ShowForCard(this, worldPos);
    //        }
    //        else
    //        {
    //            // Fallback if no collider
    //            menu.ShowForCard(this, transform.position);
    //        }

    //        _isShowingMenu = true;
    //        return;
    //    }

    //    // Keep menu open while mouse is over panel
    //    if (!hoverCard && hoverPanel)
    //        return;

    //    // Close if this card opened it and mouse is off both
    //    if (_isShowingMenu && !hoverCard && !hoverPanel)
    //    {
    //        menu.Hide();
    //        _isShowingMenu = false;
    //    }
    //}

    private bool IsMouseOverCard()
    {
        var col = GetComponent<Collider2D>();
        if (col == null)
            return false;

        Vector3 mouseWorld = _cam.ScreenToWorldPoint(
            new Vector3(Input.mousePosition.x,
                        Input.mousePosition.y,
                        -_cam.transform.position.z));

        return col.OverlapPoint(mouseWorld);
    }

    private void TryAutoScaleToTargetSize()
    {
        if (_renderer == null || _renderer.sprite == null)
            return;

        // Sprite's natural world size BEFORE scaling
        Vector2 rawSpriteSize = _renderer.sprite.bounds.size;

        if (rawSpriteSize.x <= 0 || rawSpriteSize.y <= 0)
            return;

        // Calculate the final world size we want
        float targetX = targetWorldSize.x;
        float targetY = targetWorldSize.y;

        // Compute scale needed along each axis
        float scaleX = targetX / rawSpriteSize.x;
        float scaleY = targetY / rawSpriteSize.y;

        // --- KEEP CARD PROPORTIONS ---
        // Use the smaller value so sprite fits inside target
        float finalScale = Mathf.Min(scaleX, scaleY);

        // Apply uniform scale so sprite isn't squished
        transform.localScale = new Vector3(finalScale, finalScale, 1f);

        // Resize collider to match target world size
        var col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            // Collider size is in local space:
            // worldSize = collider.size * localScale
            col.size = new Vector2(targetX / finalScale, targetY / finalScale);
            col.offset = Vector2.zero;
        }
    }
    private void HandleRightClickMenu()
    {
        var menu = ResourceTypeMenuUI.Instance;
        if (menu == null || _cam == null)
            return;

        // Open on right-click (single click) when mouse is over THIS resource card
        if (Input.GetMouseButtonDown(1) && IsMouseOverCard())
        {
            //// Determine local viewer's seat (menu placement is based on viewer)
            //bool isLocalSeatPlayer1 = false;
            //if (NetworkManager.Singleton != null)
            //{
            //    var localSeat = PlayerSeat.GetSeatForClient(NetworkManager.Singleton.LocalClientId);
            //    isLocalSeatPlayer1 = (localSeat == PlayerSeat.SeatType.Player1);
            //}

            //// Use collider edge so menu appears above the card visually
            //float edgeY;
            //float offsetSign;

            //if (_collider != null)
            //{
            //    if (isLocalSeatPlayer1)
            //    {
            //        edgeY = _collider.bounds.min.y;
            //        offsetSign = -0.7f;
            //    }
            //    else
            //    {
            //        edgeY = _collider.bounds.max.y;
            //        offsetSign = 0.7f;
            //    }

            //    Vector3 edgeWorld = new Vector3(_collider.bounds.center.x, edgeY, transform.position.z);
            //    Vector3 worldPos = edgeWorld + new Vector3(0f, menuWorldYOffset * offsetSign, 0f);

            //    menu.ShowForCard(this, worldPos); // stays open until closed/X/another right-click
            //}
            //else
            //{
            //    menu.ShowForCard(this, transform.position);
            //}

            //_isShowingMenu = true;
            menu.ShowForCard(this, transform.position); // worldPos ignored now
            _isShowingMenu = true;
        }
    }
    public void RequestDelete()
    {
        if (!IsSpawned) return;
        RequestDeleteServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDeleteServerRpc(ServerRpcParams rpcParams = default)
    {
        // Allow: owner deletes their own resource card OR host can delete anything
        ulong requester = rpcParams.Receive.SenderClientId;

        bool requesterIsHost = NetworkManager.Singleton != null && requester == NetworkManager.ServerClientId;
        bool requesterIsOwner = OwnerClientId == requester;

        if (!requesterIsHost && !requesterIsOwner)
            return;

        // Close menu for everyone (optional)
        ResourceTypeMenuUI.Instance?.Hide();

        // Despawn network object
        var no = GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned)
            no.Despawn(true);
    }

}
