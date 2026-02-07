// DeckInspectorUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Updated DeckInspectorUI with raycast blocking
/// Prevents clicking through to cards beneath the UI
/// </summary>
public class DeckInspectorUI : MonoBehaviour
{
    public static DeckInspectorUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private DeckCardUI deckCardPrefab;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private CardArtLibrary artLibrary;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Controls")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button deleteDeckButton;

    [Header("Raycast Blocking")]
    [SerializeField] private Image raycastBlocker; // Assign in Inspector or auto-creates

    private NetworkDeck _currentDeck;

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

        if (panelRoot == null)
            panelRoot = gameObject;

        if (deleteDeckButton != null)
            deleteDeckButton.onClick.AddListener(OnDeleteDeckClicked);

        panelRoot.SetActive(false);

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
            Debug.Log("[DeckInspectorUI] Raycast blocker configured");
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
                Debug.Log("[DeckInspectorUI] Created raycast blocker automatically");
            }

            // Ensure raycast target is enabled
            raycastBlocker.raycastTarget = true;
        }
    }

    public void ShowForDeck(NetworkDeck deck, Vector3 deckWorldPos)
    {
        if (deck == null || panelRoot == null)
            return;

        _currentDeck = deck;
        panelRoot.SetActive(true);

        RebuildListFromDeck();

        // Scroll to top whenever opened
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;

        // Ensure raycast blocker is enabled
        if (raycastBlocker != null)
        {
            raycastBlocker.enabled = true;
        }
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        _currentDeck = null;

        // Hide any zoom that might be showing
        if (CardZoomUI.Instance != null)
        {
            CardZoomUI.Instance.HideZoom();
        }
    }

    public void RefreshIfShowingDeck(NetworkDeck deck)
    {
        if (panelRoot == null || !panelRoot.activeInHierarchy)
            return;

        if (_currentDeck == null || deck == null)
            return;

        if (_currentDeck != deck)
            return;

        RebuildListFromDeck(keepScrollPosition: true);
    }

    private void RebuildListFromDeck(bool keepScrollPosition = false)
    {
        if (_currentDeck == null || cardContainer == null || deckCardPrefab == null)
            return;

        float scrollPos = 1f;
        if (keepScrollPosition && scrollRect != null)
            scrollPos = scrollRect.verticalNormalizedPosition;

        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);

        List<string> orderedNames = _currentDeck.GetOrderedCardNames();

        var lib = artLibrary != null ? artLibrary : CardArtLibrary.Instance;

        for (int orderPos = 0; orderPos < orderedNames.Count; orderPos++)
        {
            string cardName = orderedNames[orderPos];
            Sprite art = lib != null ? lib.GetSprite(cardName) : null;

            var item = Instantiate(deckCardPrefab, cardContainer);
            item.Initialize(this, orderPos, cardName, art);
        }

        // Force layout rebuild so Content grows and ScrollRect can scroll
        if (cardContainer is RectTransform rt)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        if (keepScrollPosition && scrollRect != null)
            scrollRect.verticalNormalizedPosition = scrollPos;
    }

    public void OnDeckCardClicked(int orderPos)
    {
        if (_currentDeck == null)
            return;

        _currentDeck.RequestTakeCardToHandFromOrderPosition(orderPos);

        // Keep open so player can take multiple cards if they want
        RefreshIfShowingDeck(_currentDeck);
    }

    private void OnDeleteDeckClicked()
    {
        if (_currentDeck == null)
            return;

        _currentDeck.RequestDelete();
        Hide();
    }

    public void OnCloseClicked() => Hide();
}