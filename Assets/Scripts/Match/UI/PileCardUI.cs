using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Updated PileCardUI with Alt-key zoom support
/// Works with your existing CardZoomUI system
/// </summary>
public class PileCardUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image cardImage;

    [Header("Visual Feedback (Optional)")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color hoverColor = new Color(1f, 1f, 0.8f, 1f);

    public NetworkCard BoundCard { get; private set; }

    private bool _isHovering = false;
    private Sprite _cachedSprite;

    public void Bind(NetworkCard card)
    {
        BoundCard = card;

        if (cardImage == null)
            cardImage = GetComponent<Image>();

        if (CardArtLibrary.Instance != null && card.CardName.Length > 0)
        {
            var sprite = CardArtLibrary.Instance.GetSprite(card.CardName.ToString());
            cardImage.sprite = sprite;
            cardImage.preserveAspect = true;

            // Cache the sprite for zoom
            _cachedSprite = sprite;

            cardImage.raycastTarget = true;
        }
    }

    private void Update()
    {
        // Alt-key zoom functionality
        if (_isHovering && _cachedSprite != null)
        {
            bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (altHeld)
            {
                ShowZoom();
            }
            else
            {
                HideZoom();
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovering = true;

        // Visual feedback on hover
        if (cardImage != null)
        {
            cardImage.color = hoverColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;

        // Hide zoom when mouse leaves
        HideZoom();

        // Reset visual feedback
        if (cardImage != null)
        {
            cardImage.color = normalColor;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (PileInspectorUI.Instance == null)
            return;

        // Let the PileInspectorUI handle moving the real card
        PileInspectorUI.Instance.HandleEntryClicked(this, eventData);
    }

    private void ShowZoom()
    {
        if (CardZoomUI.Instance != null && _cachedSprite != null)
        {
            CardZoomUI.Instance.ShowZoom(_cachedSprite);
        }
    }

    private void HideZoom()
    {
        if (CardZoomUI.Instance != null)
        {
            CardZoomUI.Instance.HideZoom();
        }
    }

    private void OnDestroy()
    {
        // Ensure zoom is hidden when this card is destroyed
        HideZoom();
    }

    private void OnDisable()
    {
        // Also hide when disabled
        _isHovering = false;
        HideZoom();
    }
}