using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Updated PileInspectorUI with raycast blocking
/// Prevents clicking through to cards beneath the UI
/// </summary>
public class PileInspectorUI : MonoBehaviour
{
    public static PileInspectorUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform gridRoot;
    [SerializeField] private PileCardUI pileCardPrefab;

    [Header("Positioning")]
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private float screenOffsetRight = 40f;

    [Header("Offsets")]
    public float uiHorizontalOffset = 275f;

    [Header("Controls")]
    [SerializeField] private Button closeButton;

    [Header("Raycast Blocking")]
    [SerializeField] private Image raycastBlocker; // Assign in Inspector or auto-creates

    private readonly HashSet<NetworkCard> _inspectedCards = new HashSet<NetworkCard>();
    private readonly List<PileCardUI> _entries = new List<PileCardUI>();

    private Canvas _canvas;
    private bool _isPointerOverPanel;

    public bool IsPointerOverPanel => _isPointerOverPanel;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _canvas = GetComponentInParent<Canvas>();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (panelRect == null && panelRoot != null)
            panelRect = panelRoot.GetComponent<RectTransform>();

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingLayerName = "UI";
            canvas.sortingOrder = 100;
        }

        // Setup raycast blocker to prevent clicking through
        SetupRaycastBlocker();
    }

    /// <summary>
    /// Ensures raycast blocker exists to prevent clicking through to cards beneath
    /// </summary>
    private void SetupRaycastBlocker()
    {
        if (raycastBlocker != null)
        {
            // Already assigned, just ensure it's configured
            raycastBlocker.raycastTarget = true;
            Debug.Log("[PileInspectorUI] Raycast blocker configured");
            return;
        }

        // Try to find or create on panelRoot
        if (panelRoot != null)
        {
            // Look for existing Image component
            raycastBlocker = panelRoot.GetComponent<Image>();

            if (raycastBlocker == null)
            {
                // Create one if it doesn't exist
                raycastBlocker = panelRoot.AddComponent<Image>();
                raycastBlocker.color = new Color(0, 0, 0, 0.01f); // Nearly transparent but blocks raycasts
                Debug.Log("[PileInspectorUI] Created raycast blocker automatically");
            }

            // Ensure raycast target is enabled
            raycastBlocker.raycastTarget = true;
        }
    }

    private void Update()
    {
        if (panelRoot == null || panelRect == null || _canvas == null)
        {
            _isPointerOverPanel = false;
            return;
        }

        if (!panelRoot.activeInHierarchy)
        {
            _isPointerOverPanel = false;
            return;
        }

        _isPointerOverPanel = RectTransformUtility.RectangleContainsScreenPoint(
           panelRect,
           Input.mousePosition,
           _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera);

        // Auto-close if the pile we are showing no longer exists
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e == null || e.BoundCard == null || !e.BoundCard.IsSpawned)
            {
                Hide();
                break;
            }
        }
    }

    public void ShowForPile(List<NetworkCard> pile, Vector3 pileWorldPos)
    {
        if (panelRoot == null || gridRoot == null || pileCardPrefab == null)
            return;

        // Clear old entries
        foreach (var e in _entries)
        {
            if (e != null)
                Destroy(e.gameObject);
        }
        _entries.Clear();

        // De-dupe pile
        var unique = new List<NetworkCard>(pile.Count);
        var seen = new HashSet<ulong>();

        for (int i = 0; i < pile.Count; i++)
        {
            var c = pile[i];
            if (c == null || !c.IsSpawned) continue;

            ulong id = c.NetworkObjectId;

            if (seen.Add(id))
                unique.Add(c);
        }

        // Sort for consistent order
        List<NetworkCard> ordered = unique;
        ordered.Sort((a, b) =>
        {
            var ra = a.GetComponent<SpriteRenderer>();
            var rb = b.GetComponent<SpriteRenderer>();
            int oa = ra != null ? ra.sortingOrder : 0;
            int ob = rb != null ? rb.sortingOrder : 0;

            int cmp = oa.CompareTo(ob);
            if (cmp != 0) return cmp;

            return b.transform.position.y.CompareTo(a.transform.position.y);
        });

        // Create entries
        ClearEntriesImmediateVisual();

        _inspectedCards.Clear();
        foreach (var card in ordered)
        {
            var entry = Instantiate(pileCardPrefab, gridRoot);
            entry.Bind(card);
            _entries.Add(entry);

            _inspectedCards.Add(card);
        }

        ClampPanelToCanvas();
        panelRoot.SetActive(true);

        // Ensure raycast blocker is enabled
        if (raycastBlocker != null)
        {
            raycastBlocker.enabled = true;
        }
    }

    public void Hide()
    {
        if (panelRoot == null)
            return;

        panelRoot.SetActive(false);

        ClearEntriesImmediateVisual();
        _entries.Clear();

        _inspectedCards.Clear();

        // Hide any zoom that might be showing
        if (CardZoomUI.Instance != null)
        {
            CardZoomUI.Instance.HideZoom();
        }
    }

    public void HandleEntryClicked(PileCardUI entry, PointerEventData eventData)
    {
        if (entry == null) return;

        NetworkCard card = entry.BoundCard;
        if (card == null || !card.IsSpawned) return;

        if (!card.IsOwner)
        {
            return;
        }

        // Return to hand
        card.RequestReturnToHandFromUI();

        // Close the pile UI
        Hide();
    }

    private void ClampPanelToCanvas()
    {
        if (panelRect == null || _canvas == null)
            return;

        RectTransform canvasRect = _canvas.transform as RectTransform;

        Vector3[] panelCorners = new Vector3[4];
        Vector3[] canvasCorners = new Vector3[4];

        panelRect.GetWorldCorners(panelCorners);
        canvasRect.GetWorldCorners(canvasCorners);

        float leftOverflow = canvasCorners[0].x - panelCorners[0].x;
        float rightOverflow = panelCorners[2].x - canvasCorners[2].x;
        float bottomOverflow = canvasCorners[0].y - panelCorners[0].y;
        float topOverflow = panelCorners[2].y - canvasCorners[2].y;

        Vector3 shift = Vector3.zero;

        if (leftOverflow > 0f)
            shift.x += leftOverflow;
        if (rightOverflow > 0f)
            shift.x -= rightOverflow;

        if (bottomOverflow > 0f)
            shift.y += bottomOverflow;
        if (topOverflow > 0f)
            shift.y -= topOverflow;

        panelRect.position += shift;
    }

    public void CloseIfShowingCard(NetworkCard card)
    {
        if (card == null) return;
        if (!_inspectedCards.Contains(card)) return;
        Hide();
    }

    private void ClearEntriesImmediateVisual()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry == null) continue;

            entry.gameObject.SetActive(false);
            entry.transform.SetParent(null, false);

            Destroy(entry.gameObject);
        }

        _entries.Clear();
    }
}