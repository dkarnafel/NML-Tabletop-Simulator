// DeckCardUI.cs
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DeckCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image cardImage;

    private DeckInspectorUI _owner;
    private int _orderPos;
    private string _cardName;
    private Sprite _art;

    private bool _hovered;

    public void Initialize(DeckInspectorUI owner, int orderPos, string cardName, Sprite art)
    {
        _owner = owner;
        _orderPos = orderPos;
        _cardName = cardName;
        _art = art;

        if (cardImage != null)
        {
            cardImage.sprite = art;
            cardImage.enabled = (art != null);
            cardImage.raycastTarget = true; // IMPORTANT
        }
    }

    private void Update()
    {
        if (!_hovered) return;

        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (alt && _art != null)
        {
            CardZoomUI.Instance?.ShowZoom(_art);
        }
        else
        {
            // If alt released while still hovering, hide zoom
            CardZoomUI.Instance?.HideZoom();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        CardZoomUI.Instance?.HideZoom();
    }

    public void OnClick()
    {
        _owner?.OnDeckCardClicked(_orderPos);
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        // Left-click only
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        _owner?.OnDeckCardClicked(_orderPos);
    }
}
