using UnityEngine;
using UnityEngine.UI;

public class HideEmptySessions : MonoBehaviour
{
    private void OnEnable()
    {
        // Wait a moment for list to populate
        Invoke("FilterList", 0.5f);
    }

    private void FilterList()
    {
        // Hide session items with 0 players
        var sessionItems = GetComponentsInChildren<Transform>(true);

        foreach (var item in sessionItems)
        {
            var text = item.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (text != null && text.text.Contains("0/"))
            {
                item.gameObject.SetActive(false);
            }
        }
    }
}