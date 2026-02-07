using UnityEngine;
using UnityEngine.EventSystems;

public class CameraZoomBlocker2D : MonoBehaviour
{
    [Header("What counts as 'over a pile/card'")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private LayerMask cardMask;     // set to your Card layer(s)
    [SerializeField] private float rayDistance = 100f;

    private float _lastOrthoSize;
    private float _lastFov;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera != null)
        {
            _lastOrthoSize = targetCamera.orthographicSize;
            _lastFov = targetCamera.fieldOfView;
        }
    }

    private void Update()
    {
        if (targetCamera == null) return;

        // Record "allowed" zoom each frame when NOT hovering a card/pile.
        // So when zoom tries to happen while hovering, we can snap back.
        if (!IsPointerOverCard())
        {
            if (targetCamera.orthographic)
                _lastOrthoSize = targetCamera.orthographicSize;
            else
                _lastFov = targetCamera.fieldOfView;
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;

        // If scrolling and hovering a card/pile, cancel zoom changes
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        // Ignore scroll over UI (optional: remove if you want UI scroll blocked too)
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (!IsPointerOverCard())
            return;

        // Revert zoom to last allowed value
        if (targetCamera.orthographic)
            targetCamera.orthographicSize = _lastOrthoSize;
        else
            targetCamera.fieldOfView = _lastFov;
    }

    private bool IsPointerOverCard()
    {
        if (targetCamera == null) return false;

        Vector3 world = targetCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 point = new Vector2(world.x, world.y);

        // This detects ANY collider in cardMask under mouse (pile cards are included).
        return Physics2D.OverlapPoint(point, cardMask) != null;
    }
}
