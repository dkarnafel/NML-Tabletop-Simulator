using UnityEngine;
using UnityEngine.UI;

public class CardZoomUI : MonoBehaviour
{
    public static CardZoomUI Instance { get; private set; }

    [SerializeField] private Image zoomImage;
    [SerializeField] private GameObject panelRoot;

    private bool _altHeld;
    private bool _hasSprite;

    private void Awake()
    {
        Instance = this;
        panelRoot.SetActive(false);
    }

    private void Update()
    {
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        // When ALT is released, always close
        if (_altHeld && !alt)
        {
            _altHeld = false;
            _hasSprite = false;
            panelRoot.SetActive(false);
            return;
        }

        _altHeld = alt;

        // If ALT is held and we already have a sprite, keep showing
        if (_altHeld && _hasSprite)
        {
            if (!panelRoot.activeSelf)
                panelRoot.SetActive(true);
        }
    }

    public void ShowZoom(Sprite sprite)
    {
        if (sprite == null) return;

        zoomImage.sprite = sprite;
        _hasSprite = true;

        // Only show when ALT is currently held
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        _altHeld = alt;

        if (_altHeld)
            panelRoot.SetActive(true);
    }

    public void HideZoom()
    {
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        _altHeld = alt;

        if (_altHeld)
            return; // keep showing until ALT is released

        _hasSprite = false;
        panelRoot.SetActive(false);
    }
}
