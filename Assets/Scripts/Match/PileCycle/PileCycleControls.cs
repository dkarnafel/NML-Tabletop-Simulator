using UnityEngine;

public class PileCycleControls : MonoBehaviour
{
    [SerializeField] private PileCycleButton upButton;
    [SerializeField] private PileCycleButton downButton;

    public void Bind(NetworkCard card)
    {
        if (upButton != null) upButton.Bind(card, +1);
        if (downButton != null) downButton.Bind(card, -1);
    }

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
}
