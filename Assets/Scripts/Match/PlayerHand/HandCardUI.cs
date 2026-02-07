using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HandCardUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [HideInInspector] public string CardName;      // logical identity
    [HideInInspector] public int CardIndex = -1;   // optional index from the deck
    [HideInInspector] public ulong OwnerClientId;  // who owns this card

    private RectTransform _rect;
    private Canvas _rootCanvas;
    private Vector2 _originalAnchoredPos;
    private Transform _originalParent;

    private Vector2 _dragOffset;

    [Header("Hover Scaling")]
    [SerializeField] private float hoverScale = 1.2f;   // how big on hover
    private Vector3 _originalScale;

    [Header("Audio")]
    [SerializeField] private AudioClip dropCardClip;
    [SerializeField] private GameObject oneShotAudioPrefab;

    private bool _isHovering = false;
    private bool _playedToBoard = false;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        if (_rect == null)
        {
            Debug.LogError($"[HandCardUI] Missing RectTransform on '{gameObject.name}'. " +
                           "HandCardUI must be on a UI object under a Canvas (RectTransform). Disabling component.");
            enabled = false;
            return;
        }

        _rootCanvas = GetComponentInParent<Canvas>();
        _originalScale = _rect.localScale;

        if (_rootCanvas == null)
            Debug.LogWarning("[HandCardUI] No root canvas found.");
    }

    private void Update()
    {
        // --- Hover scale ---
        ApplyHoverScale();

        // --- Alt zoom panel ---
        if (!_isHovering)
            return;

        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        if (alt)
        {
            if (!string.IsNullOrEmpty(CardName) &&
                CardArtLibrary.Instance != null &&
                CardZoomUI.Instance != null)
            {
                var sprite = CardArtLibrary.Instance.GetSprite(CardName);
                if (sprite != null)
                    CardZoomUI.Instance.ShowZoom(sprite);
            }
        }
        else
        {
            // Hovering but Alt released → hide
            if (CardZoomUI.Instance != null)
                CardZoomUI.Instance.HideZoom();
        }
    }

    private void ApplyHoverScale()
    {
        if (_rect == null) return;
        _rect.localScale = _isHovering ? _originalScale * hoverScale : _originalScale;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_rect == null)
            _rect = GetComponent<RectTransform>();
        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();

        _originalParent = _rect.parent;
        _originalAnchoredPos = _rect.anchoredPosition;

        // Bring to front
        _rect.SetAsLastSibling();

        // Compute offset between mouse position and current anchoredPosition
        RectTransform parentRect = _rect.parent as RectTransform;
        if (parentRect != null)
        {
            Vector2 localMousePos;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera,
                    out localMousePos))
            {
                _dragOffset = _rect.anchoredPosition - localMousePos;
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_rect == null || _rootCanvas == null)
            return;

        RectTransform parentRect = _rect.parent as RectTransform;
        if (parentRect == null)
            return;

        Vector2 localMousePos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                eventData.position,
                _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera,
                out localMousePos))
        {
            // Keep the initial offset so the card doesn't "jump" under the mouse
            _rect.anchoredPosition = localMousePos + _dragOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_rect == null)
            return;

        // Convert drop position to world
        Camera cam = Camera.main;
        if (cam == null)
        {
            // Snap back if no camera
            _rect.anchoredPosition = _originalAnchoredPos;
            return;
        }

        Vector3 worldPos = cam.ScreenToWorldPoint(
            new Vector3(eventData.position.x, eventData.position.y, -cam.transform.position.z));

        // Ask the board spawner to play this card
        if (BoardCardSpawner.Instance != null &&
            !string.IsNullOrEmpty(CardName))
        {
            BoardCardSpawner.Instance.RequestPlayCardFromHand(CardName, worldPos, OwnerClientId);
            // 🔊 Play drop sound (local only)
            if (dropCardClip != null && oneShotAudioPrefab != null)
            {
                var audioObj = Instantiate(oneShotAudioPrefab);
                audioObj.GetComponent<CardLandAudio>().Play(dropCardClip);
            }
            _playedToBoard = true;
            // ✅ run the delayed recount on the persistent tracker (not this card)
            if (HandCountTracker.Local != null)
                HandCountTracker.Local.ReportLocalHandCountNextFrame();
            // Remove from hand UI
            Destroy(gameObject);
        }
        else
        {
            // If we can't play it, return it to hand
            _rect.anchoredPosition = _originalAnchoredPos;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovering = true;
        ApplyHoverScale();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
        ApplyHoverScale();

        // Only the card that just lost hover hides zoom
        if (CardZoomUI.Instance != null)
            CardZoomUI.Instance.HideZoom();
    }

    private void OnEnable()
    {
        ReportHandCount();
    }

    private void OnDisable()
    {
        // covers object pooling / disable-on-play
        if (_playedToBoard) return;
        ReportHandCount();
    }

    private void OnDestroy()
    {
        if (_playedToBoard) return;
        ReportHandCount();
    }

    private void OnTransformParentChanged()
    {
        // covers drag reparenting (hand -> drag layer -> field or back)
        ReportHandCount();
    }

    private void ReportHandCount()
    {
        // Prefer an owned tracker (works on host + clients even if Local isn't set)
        var trackers = FindObjectsOfType<HandCountTracker>(true);
        foreach (var t in trackers)
        {
            if (t != null && t.IsSpawned && t.IsOwner)
            {
                t.ReportLocalHandCount();
                return;
            }
        }

        // Fallback to Local if needed
        if (HandCountTracker.Local != null)
            HandCountTracker.Local.ReportLocalHandCount();
    }
}
