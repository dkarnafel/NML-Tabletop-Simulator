using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHandUI : MonoBehaviour
{
    public static PlayerHandUI Instance { get; private set; }

    [Header("Hand UI")]
    [SerializeField] private RectTransform handContainer; // parent with Horizontal/VerticalLayoutGroup
    [SerializeField] private Image cardUIPrefab;          // UI prefab with an Image component

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Adds a card image to the local player's hand area.
    /// </summary>
    public void AddCardToHand(Sprite cardSprite, string cardName, int cardIndex, ulong ownerClientId)
    {
        if (cardSprite == null || cardUIPrefab == null || handContainer == null)
        {
            Debug.LogWarning("[PlayerHandUI] Missing references.");
            return;
        }

        Image cardUI = Instantiate(cardUIPrefab, handContainer);
        cardUI.sprite = cardSprite;
        cardUI.preserveAspect = true;

        HandCardUI handCard = cardUI.GetComponent<HandCardUI>();
        if (handCard == null)
            handCard = cardUI.gameObject.AddComponent<HandCardUI>();

        handCard.CardName = cardName;
        handCard.CardIndex = cardIndex;
        handCard.OwnerClientId = ownerClientId;
    }
    public void AddCardToHandFromName(string cardName)
    {
        if (CardArtLibrary.Instance == null)
        {
            Debug.LogWarning("[PlayerHandUI] CardArtLibrary not found.");
            return;
        }

        Sprite sprite = CardArtLibrary.Instance.GetSprite(cardName);
        if (sprite == null)
        {
            Debug.LogWarning("[PlayerHandUI] No sprite found for card: " + cardName);
            return;
        }

        AddCardToHand(sprite, cardName, -1, NetworkManager.Singleton.LocalClientId);
    }
}
