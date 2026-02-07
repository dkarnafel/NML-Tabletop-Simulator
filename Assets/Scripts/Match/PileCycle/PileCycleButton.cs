using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PileCycleButton : MonoBehaviour
{
    public NetworkCard BoundCard { get; private set; }
    public int Direction { get; private set; } // +1 = up, -1 = down

    public void Bind(NetworkCard card, int direction)
    {
        BoundCard = card;
        Direction = direction;
    }

    private void OnMouseDown()
    {
        if (BoundCard == null)
            return;

        // Only allow cycling from the owner (your current gameplay rule)
        if (!BoundCard.IsOwner)
            return;

        BoundCard.RequestCyclePile(Direction > 0);
    }

    private void Awake()
    {
        // These UI arrows are world-space helpers. We want them clickable,
        // but we DO NOT want them to collide/push other cards or affect pile detection.
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            // Trigger = no physical pushes.
            col.isTrigger = true;
        }

        // If you created a dedicated layer (recommended), keep arrows there.
        // This makes it easy to exclude them from physics queries.
        int pileUiLayer = LayerMask.NameToLayer("PileUI");
        if (pileUiLayer >= 0)
            gameObject.layer = pileUiLayer;
    }

}
