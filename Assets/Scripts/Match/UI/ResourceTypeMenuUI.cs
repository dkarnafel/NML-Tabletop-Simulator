using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ResourceTypeMenuUI : MonoBehaviour
{
    public static ResourceTypeMenuUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Button primalButton;
    [SerializeField] private Button arcaneButton;
    [SerializeField] private Button refinedButton;
    [SerializeField] private Button techButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button unconvertButton;

    [Header("Offsets")]
    [SerializeField] private float uiHorizontalOffset = 20f;

    [SerializeField] private Button deleteButton; // drag in inspector
    private ResourceCard _bound;

    private Canvas _canvas;
    private ResourceCard _currentCard;
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

        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        _canvas = GetComponentInParent<Canvas>();

        if (panelRoot == null)
            panelRoot = gameObject;
        if (panelRect == null)
            panelRect = panelRoot.GetComponent<RectTransform>();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        // Button hooks
        if (primalButton != null)
            primalButton.onClick.AddListener(() => OnClicked(ResourceCard.ResourceType.Primal));
        if (arcaneButton != null)
            arcaneButton.onClick.AddListener(() => OnClicked(ResourceCard.ResourceType.Arcane));
        if (refinedButton != null)
            refinedButton.onClick.AddListener(() => OnClicked(ResourceCard.ResourceType.Refined));
        if (techButton != null)
            techButton.onClick.AddListener(() => OnClicked(ResourceCard.ResourceType.Tech));
        if (unconvertButton != null)
            unconvertButton.onClick.AddListener(() => OnClicked(ResourceCard.ResourceType.None));
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
    }

    public void ShowForCard(ResourceCard card, Vector3 worldPos)
    {
        if (panelRoot == null || _canvas == null || panelRect == null || card == null)
            return;

        _currentCard = card;

        RectTransform canvasRect = _canvas.transform as RectTransform;
        Camera worldCam = Camera.main;
        Camera uiCam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                          ? null
                          : _canvas.worldCamera;

        //if (worldCam != null && canvasRect != null)
        //{
        //    // World → Screen
        //    Vector3 screenPos = worldCam.WorldToScreenPoint(worldPos);
        //    // Slight horizontal offset to the right
        //    screenPos.x += uiHorizontalOffset;

        //    // Screen → UI world
        //    Vector3 uiWorldPos;
        //    if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
        //            canvasRect,
        //            screenPos,
        //            uiCam,
        //            out uiWorldPos))
        //    {
        //        panelRect.position = uiWorldPos;
        //    }
        //}

        //// Optional: clamp to canvas (reuse ClampPanelToCanvas from Pile menu if you like)
        //ClampToCanvas();
        deleteButton.onClick.RemoveAllListeners();
        deleteButton.onClick.AddListener(() =>
        {
            if (_currentCard != null)
                _currentCard.RequestDelete();

            Hide();
        });

        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        _currentCard = null;
    }

    private void OnClicked(ResourceCard.ResourceType type)
    {
        if (_currentCard != null)
        {
            _currentCard.RequestSetResourceType(type);
        }
        Hide();
    }

    private void ClampToCanvas()
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
        if (leftOverflow > 0f) shift.x += leftOverflow;
        if (rightOverflow > 0f) shift.x -= rightOverflow;
        if (bottomOverflow > 0f) shift.y += bottomOverflow;
        if (topOverflow > 0f) shift.y -= topOverflow;

        panelRect.position += shift;
    }
}
