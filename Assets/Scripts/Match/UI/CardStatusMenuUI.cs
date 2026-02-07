using UnityEngine;
using UnityEngine.UI;

public class CardStatusMenuUI : MonoBehaviour
{
    public static CardStatusMenuUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button addExhaustButton;
    [SerializeField] private Button removeExhaustButton;

    [SerializeField] private Button add11Button;
    [SerializeField] private Button addAtkButton;
    [SerializeField] private Button addHpButton;

    [SerializeField] private Button sub11Button;
    [SerializeField] private Button subAtkButton;
    [SerializeField] private Button subHpButton;

    [SerializeField] private Button closeButton;

    private NetworkCard _current;

    private void Awake()
    {
        // Setup Buttons and Listeners for Menu
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (panelRoot == null) panelRoot = gameObject;
        panelRoot.SetActive(false);

        if (closeButton != null) closeButton.onClick.AddListener(Hide);

        // ...
        if (addExhaustButton != null) addExhaustButton.onClick.AddListener(() =>
        {
            _current?.AddExhaustFromUI();
            Hide();
        });

        if (removeExhaustButton != null) removeExhaustButton.onClick.AddListener(() =>
        {
            _current?.RemoveExhaustFromUI();
            Hide();
        });

        if (add11Button != null) add11Button.onClick.AddListener(() => { _current?.AddBuffFromUI(+1, +1); Hide(); });
        if (addAtkButton != null) addAtkButton.onClick.AddListener(() => { _current?.AddBuffFromUI(+1, 0); Hide(); });
        if (addHpButton != null) addHpButton.onClick.AddListener(() => { _current?.AddBuffFromUI(0, +1); Hide(); });

        if (sub11Button != null) sub11Button.onClick.AddListener(() => { _current?.AddBuffFromUI(-1, -1); Hide(); });
        if (subAtkButton != null) subAtkButton.onClick.AddListener(() => { _current?.AddBuffFromUI(-1, 0); Hide(); });
        if (subHpButton != null) subHpButton.onClick.AddListener(() => { _current?.AddBuffFromUI(0, -1); Hide(); });
    }

    public void ShowForCard(NetworkCard card, Vector3 worldPos)
    {
        if (panelRoot == null || card == null) return;

        _current = card;

        panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        _current = null;
    }

    public bool IsShowingFor(NetworkCard c) => panelRoot != null && panelRoot.activeInHierarchy && _current == c;
}
