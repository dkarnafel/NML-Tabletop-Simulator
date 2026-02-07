using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT
using TMPro;
#endif

public class DiceRollUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button rollButton;
    [SerializeField] private Image die1Image;
    [SerializeField] private Image die2Image;

    // Use TMP if you have it; otherwise you can swap to UnityEngine.UI.Text
    [SerializeField] private TMPro.TMP_Text resultText;

    [Header("Dice Sprites (Index 0 = face 1, ... Index 5 = face 6)")]
    [SerializeField] private Sprite[] diceFaces = new Sprite[6];

    [Header("Roll Animation")]
    [SerializeField] private float rollDuration = 1.0f;
    [SerializeField] private float faceSwapInterval = 0.05f;

    private Coroutine _rollingRoutine;
    private int _lastRollId = -1;

    private void OnEnable()
    {
        if (rollButton != null)
            rollButton.onClick.AddListener(OnRollClicked);

        StartCoroutine(HookWhenReady());
    }

    private void OnDisable()
    {
        if (rollButton != null)
            rollButton.onClick.RemoveListener(OnRollClicked);

        if (NetworkDiceRoller.Instance != null)
            NetworkDiceRoller.Instance.RollId.OnValueChanged -= OnRollIdChanged;
    }

    private void Start()
    {
        // Initialize UI to current values
        TrySetFinalFaces();
    }

    private void OnRollClicked()
    {
        var roller = NetworkDiceRoller.Instance;

        if (roller == null)
        {
            Debug.LogError("[DiceRollUI] NetworkDiceRoller.Instance is null (roller not in scene?)");
            rollButton.interactable = true;
            return;
        }

        if (!roller.IsSpawned)
        {
            Debug.LogError("[DiceRollUI] Roller exists but is NOT spawned. ServerRpc will not run. " +
                           "Make sure NetworkDiceRoller is a spawned NetworkObject.");
            rollButton.interactable = true;
            return;
        }

        roller.RequestRollServerRpc();
        rollButton.interactable = false;
    }

    private void OnRollIdChanged(int oldValue, int newValue)
    {
        // Prevent double-run if something re-hooks
        if (newValue == _lastRollId) return;
        _lastRollId = newValue;

        // Start roll animation on every client
        if (_rollingRoutine != null)
            StopCoroutine(_rollingRoutine);

        _rollingRoutine = StartCoroutine(RollAnimationThenSettle());
    }

    private IEnumerator RollAnimationThenSettle()
    {
        // disable button during roll
        if (rollButton != null) rollButton.interactable = false;

        float t = 0f;
        while (t < rollDuration)
        {
            // random faces while rolling
            SetFace(die1Image, Random.Range(1, 7));
            SetFace(die2Image, Random.Range(1, 7));

            yield return new WaitForSeconds(faceSwapInterval);
            t += faceSwapInterval;
        }

        // settle on replicated final values
        TrySetFinalFaces();

        if (rollButton != null) rollButton.interactable = true;
        _rollingRoutine = null;
    }

    private void TrySetFinalFaces()
    {
        var roller = NetworkDiceRoller.Instance;
        if (roller == null) return;

        int d1 = roller.Die1.Value;
        int d2 = roller.Die2.Value;

        SetFace(die1Image, d1);
        SetFace(die2Image, d2);

        if (resultText != null)
            resultText.text = $"{d1 + d2}";
    }

    private void SetFace(Image img, int value1to6)
    {
        if (img == null) return;
        int idx = Mathf.Clamp(value1to6, 1, 6) - 1;

        if (diceFaces == null || diceFaces.Length < 6 || diceFaces[idx] == null)
            return;

        img.sprite = diceFaces[idx];
        img.preserveAspect = true;
    }

    private IEnumerator HookWhenReady()
    {
        // Wait until Netcode is running
        while (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
            yield return null;

        // Wait until the roller exists and is spawned
        while (NetworkDiceRoller.Instance == null || !NetworkDiceRoller.Instance.IsSpawned)
            yield return null;

        // Now subscribe safely
        NetworkDiceRoller.Instance.RollId.OnValueChanged += OnRollIdChanged;

        // Initialize UI
        TrySetFinalFaces();
    }

}
