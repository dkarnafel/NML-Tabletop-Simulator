using UnityEngine;

public class ResetGameButton : MonoBehaviour
{
    // Hook this to the UI Button OnClick
    public void OnResetClicked()
    {
        if (GameManager.Singleton == null)
        {
            Debug.LogError("[ResetGameButton] GameManager.Singleton is null. Is GameManager spawned?");
            return;
        }

        GameManager.Singleton.RequestResetFromUI();
    }
}
