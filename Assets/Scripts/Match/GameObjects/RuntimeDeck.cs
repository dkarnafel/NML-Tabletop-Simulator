using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple 2D deck object that shows the top card, can be dragged with the mouse
/// and flipped with the F key to show a card-back sprite.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class RuntimeDeck : MonoBehaviour
{
    private List<Sprite> _cardFaces;
    private Sprite _cardBack;
    private Camera _ownerCamera;

    private SpriteRenderer _renderer;
    private bool _faceUp = true;
    private bool _isDragging;

    // Optional: which card index is on top (you can extend this later)
    private int _topCardIndex = 0;

    public void Initialize(List<Sprite> cardFaces, Sprite cardBack, Camera ownerCamera)
    {
        _cardFaces = cardFaces;
        _cardBack = cardBack;
        _ownerCamera = ownerCamera;

        _renderer = GetComponent<SpriteRenderer>();
        if (_renderer == null)
            _renderer = gameObject.AddComponent<SpriteRenderer>();

        _renderer.sprite = _cardFaces != null && _cardFaces.Count > 0
            ? _cardFaces[_topCardIndex]
            : _cardBack;

        // Collider for clicking/dragging
        var col = GetComponent<BoxCollider2D>();
        if (col == null)
            col = gameObject.AddComponent<BoxCollider2D>();

        col.size = _renderer.bounds.size;
        col.isTrigger = false;
    }

    private void Update()
    {
        if (_ownerCamera == null) return;

        HandleDrag();
        HandleFlip();
    }

    private void HandleDrag()
    {
        // Start drag on left mouse down if we hit this deck
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mouseWorld =
                _ownerCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x,
                                                            Input.mousePosition.y,
                                                            -_ownerCamera.transform.position.z));
            Vector2 mouseWorld2D = mouseWorld;
            RaycastHit2D hit = Physics2D.Raycast(mouseWorld2D, Vector2.zero);

            if (hit.collider != null && hit.collider.gameObject == gameObject)
            {
                _isDragging = true;
            }
        }

        // Stop dragging when mouse released
        if (Input.GetMouseButtonUp(0))
        {
            _isDragging = false;
        }

        // While dragging, follow mouse
        if (_isDragging)
        {
            Vector3 mouseWorld =
                _ownerCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x,
                                                            Input.mousePosition.y,
                                                            -_ownerCamera.transform.position.z));
            mouseWorld.z = 0f; // keep deck on board plane
            transform.position = mouseWorld;
        }
    }

    private void HandleFlip()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            _faceUp = !_faceUp;

            if (_faceUp)
            {
                if (_cardFaces != null && _cardFaces.Count > 0)
                    _renderer.sprite = _cardFaces[_topCardIndex];
            }
            else
            {
                if (_cardBack != null)
                    _renderer.sprite = _cardBack;
            }
        }
    }
}
