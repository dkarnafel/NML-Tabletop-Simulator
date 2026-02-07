using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Connects a name input field to PlayerNameManager
/// Automatically saves and loads player name
/// Place this on your name input field in the main menu
/// </summary>
public class PlayerNameInput : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button confirmButton; // Optional: button to confirm name change

    [Header("Settings")]
    [SerializeField] private bool saveOnChange = true; // Save automatically when typing
    [SerializeField] private bool saveOnEndEdit = true; // Save when done typing
    [SerializeField] private int maxNameLength = 20;
    [SerializeField] private string defaultName = "Player";

    [Header("Feedback (Optional)")]
    [SerializeField] private TextMeshProUGUI feedbackText; // Shows "Saved!" feedback
    [SerializeField] private float feedbackDuration = 1.5f;

    private void Start()
    {
        // Get reference if not assigned
        if (nameInputField == null)
        {
            nameInputField = GetComponent<TMP_InputField>();
        }

        if (nameInputField != null)
        {
            // Set character limit
            nameInputField.characterLimit = maxNameLength;

            // Load saved name
            LoadSavedName();

            // Subscribe to events
            if (saveOnChange)
            {
                nameInputField.onValueChanged.AddListener(OnNameChanged);
            }

            if (saveOnEndEdit)
            {
                nameInputField.onEndEdit.AddListener(OnNameEndEdit);
            }
        }

        // Setup confirm button if provided
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        // Hide feedback initially
        if (feedbackText != null)
        {
            feedbackText.gameObject.SetActive(false);
        }
    }

    private void LoadSavedName()
    {
        if (PlayerNameManagerFixed.Instance != null)
        {
            string savedName = PlayerNameManagerFixed.Instance.GetSavedPlayerName();

            if (!string.IsNullOrEmpty(savedName))
            {
                nameInputField.text = savedName;
                Debug.Log($"[PlayerNameInput] Loaded saved name: {savedName}");
            }
            else
            {
                // No saved name, use default
                nameInputField.text = defaultName;
                Debug.Log($"[PlayerNameInput] No saved name, using default: {defaultName}");
            }
        }
        else
        {
            Debug.LogWarning("[PlayerNameInput] PlayerNameManagerFixed not found!");
            nameInputField.text = defaultName;
        }
    }

    private void OnNameChanged(string newName)
    {
        if (saveOnChange && !string.IsNullOrEmpty(newName))
        {
            SaveName(newName);
        }
    }

    private void OnNameEndEdit(string finalName)
    {
        if (saveOnEndEdit && !string.IsNullOrEmpty(finalName))
        {
            SaveName(finalName);
        }
    }

    private void OnConfirmClicked()
    {
        string currentName = nameInputField.text;

        if (!string.IsNullOrEmpty(currentName))
        {
            SaveName(currentName);
            ShowFeedback("Name saved!");
        }
    }

    private void SaveName(string playerName)
    {
        // Trim whitespace
        playerName = playerName.Trim();

        if (string.IsNullOrEmpty(playerName))
        {
            Debug.LogWarning("[PlayerNameInput] Cannot save empty name");
            return;
        }

        // Limit length
        if (playerName.Length > maxNameLength)
        {
            playerName = playerName.Substring(0, maxNameLength);
            nameInputField.text = playerName;
        }

        // Save through PlayerNameManager
        if (PlayerNameManagerFixed.Instance != null)
        {
            PlayerNameManagerFixed.Instance.SetLocalPlayerName(playerName);
            Debug.Log($"[PlayerNameInput] Saved name: {playerName}");
        }
        else
        {
            Debug.LogWarning("[PlayerNameInput] PlayerNameManagerFixed not found!");
        }
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText != null)
        {
            StopAllCoroutines();
            StartCoroutine(ShowFeedbackCoroutine(message));
        }
    }

    private System.Collections.IEnumerator ShowFeedbackCoroutine(string message)
    {
        feedbackText.text = message;
        feedbackText.gameObject.SetActive(true);

        yield return new WaitForSeconds(feedbackDuration);

        feedbackText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Manually refresh to load the latest saved name
    /// </summary>
    public void RefreshName()
    {
        LoadSavedName();
    }

    /// <summary>
    /// Get the current name from the input field
    /// </summary>
    public string GetCurrentName()
    {
        return nameInputField != null ? nameInputField.text : defaultName;
    }

    private void OnDestroy()
    {
        if (nameInputField != null)
        {
            nameInputField.onValueChanged.RemoveListener(OnNameChanged);
            nameInputField.onEndEdit.RemoveListener(OnNameEndEdit);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }
    }
}