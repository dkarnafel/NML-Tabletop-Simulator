using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro;
using System.Collections;

/// <summary>
/// Displays join code at the top of the lobby
/// Allows copying the code to clipboard
/// Only visible for the host
/// UPDATED: Better debugging and event-based updates
/// </summary>
public class LobbyJoinCodeDisplay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject joinCodePanel; // The entire panel to show/hide
    [SerializeField] private TextMeshProUGUI joinCodeText;
    [SerializeField] private Button copyCodeButton;
    [SerializeField] private TextMeshProUGUI copyFeedbackText; // Shows "Copied!" feedback

    [Header("Settings")]
    [SerializeField] private bool onlyShowForHost = true; // Only host sees the join code
    [SerializeField] private float checkInterval = 0.5f; // How often to check for code (seconds)

    private string currentJoinCode = "";
    private bool hasFoundCode = false;

    private void Start()
    {
        Debug.Log("[LobbyJoinCode] Start called");

        if (copyCodeButton != null)
        {
            copyCodeButton.onClick.AddListener(OnCopyCodeClicked);
        }

        if (copyFeedbackText != null)
        {
            copyFeedbackText.gameObject.SetActive(false);
        }

        // Start checking for join code
        StartCoroutine(CheckForJoinCodeRoutine());
    }

    private IEnumerator CheckForJoinCodeRoutine()
    {
        Debug.Log("[LobbyJoinCode] Starting join code check routine");

        // Keep checking until we find the code
        while (!hasFoundCode)
        {
            yield return new WaitForSeconds(checkInterval);

            // Check if we're the host
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            Debug.Log($"[LobbyJoinCode] Checking... IsHost: {isHost}");

            if (onlyShowForHost && !isHost)
            {
                Debug.Log("[LobbyJoinCode] Not host, hiding panel");
                if (joinCodePanel != null)
                {
                    joinCodePanel.SetActive(false);
                }
                yield break; // Stop checking if not host
            }

            // Try to get join code
            string code = CustomSessionCreator.GetJoinCode();
            Debug.Log($"[LobbyJoinCode] Got code from CustomSessionCreator: '{code}'");

            if (!string.IsNullOrEmpty(code))
            {
                Debug.Log($"[LobbyJoinCode] Found join code: {code}");
                currentJoinCode = code;
                hasFoundCode = true;
                UpdateDisplay();
                yield break; // Stop checking once we found it
            }
            else
            {
                Debug.LogWarning("[LobbyJoinCode] Join code is empty, will retry...");
            }
        }
    }

    private void UpdateDisplay()
    {
        if (string.IsNullOrEmpty(currentJoinCode))
        {
            Debug.LogWarning("[LobbyJoinCode] Cannot update display - join code is empty");
            if (joinCodePanel != null)
            {
                joinCodePanel.SetActive(false);
            }
            return;
        }

        // Show panel
        if (joinCodePanel != null)
        {
            joinCodePanel.SetActive(true);
            Debug.Log("[LobbyJoinCode] Activated join code panel");
        }
        else
        {
            Debug.LogError("[LobbyJoinCode] joinCodePanel is NULL!");
        }

        // Update text
        if (joinCodeText != null)
        {
            joinCodeText.text = $"{currentJoinCode}";
            Debug.Log($"[LobbyJoinCode] Set join code text to: {joinCodeText.text}");
        }
        else
        {
            Debug.LogError("[LobbyJoinCode] joinCodeText is NULL!");
        }
    }

    private void OnCopyCodeClicked()
    {
        if (!string.IsNullOrEmpty(currentJoinCode))
        {
            // Copy to clipboard
            GUIUtility.systemCopyBuffer = currentJoinCode;

            Debug.Log($"[LobbyJoinCode] Copied join code to clipboard: {currentJoinCode}");

            // Show feedback
            StartCoroutine(ShowCopyFeedback());
        }
        else
        {
            Debug.LogWarning("[LobbyJoinCode] Cannot copy - join code is empty");
        }
    }

    private IEnumerator ShowCopyFeedback()
    {
        if (copyFeedbackText != null)
        {
            copyFeedbackText.gameObject.SetActive(true);
            copyFeedbackText.text = "Copied!";

            yield return new WaitForSeconds(1.5f);

            copyFeedbackText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Manually refresh the join code display
    /// Call this if the join code updates during the session
    /// </summary>
    public void RefreshDisplay()
    {
        Debug.Log("[LobbyJoinCode] Manual refresh requested");
        hasFoundCode = false;
        StartCoroutine(CheckForJoinCodeRoutine());
    }

    /// <summary>
    /// Force set the join code (for debugging or manual setting)
    /// </summary>
    public void SetJoinCode(string code)
    {
        Debug.Log($"[LobbyJoinCode] Manually setting join code: {code}");
        currentJoinCode = code;
        hasFoundCode = true;
        UpdateDisplay();
    }

    private void OnDestroy()
    {
        if (copyCodeButton != null)
        {
            copyCodeButton.onClick.RemoveListener(OnCopyCodeClicked);
        }
    }

    // DEBUG: Show current state in inspector
    [ContextMenu("Debug - Check Current State")]
    private void DebugCheckState()
    {
        Debug.Log("=== LobbyJoinCodeDisplay Debug ===");
        Debug.Log($"Has Found Code: {hasFoundCode}");
        Debug.Log($"Current Join Code: '{currentJoinCode}'");
        Debug.Log($"Is Host: {(NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)}");
        Debug.Log($"CustomSessionCreator.GetJoinCode(): '{CustomSessionCreator.GetJoinCode()}'");
        Debug.Log($"Join Code Panel Active: {(joinCodePanel != null ? joinCodePanel.activeSelf.ToString() : "NULL")}");
        Debug.Log("================================");
    }
}