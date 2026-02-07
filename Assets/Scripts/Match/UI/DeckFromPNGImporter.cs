using System.IO;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using SFB;


#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(NetworkObject))]
public class DeckFromPngImporter : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private Button importDeckButton;

    [Header("Deck Prefab (NetworkDeck)")]
    [SerializeField] private NetworkDeck deckPrefab;

    [Header("Card Settings")]
    [SerializeField] private Sprite cardBackSprite;
    [SerializeField] private float spawnDistanceFromCamera = 5f;

    [Header("Seat Filter")]
    [SerializeField] private bool restrictToPlayerSeats = true;

    private PlayerSeat _localSeat;

    [System.Serializable]
    private class DeckDefinition
    {
        public string deckName;
        public string[] cardNames;
    }

    private void Awake()
    {
        if (importDeckButton != null)
            importDeckButton.onClick.AddListener(OnImportDeckClicked);
    }

    private void OnDestroy()
    {
        if (importDeckButton != null)
            importDeckButton.onClick.RemoveListener(OnImportDeckClicked);
    }

    private void Update()
    {
        if (restrictToPlayerSeats)
            UpdateButtonVisibilityBasedOnSeat();
    }

    private void UpdateButtonVisibilityBasedOnSeat()
    {
        if (importDeckButton == null)
            return;

        if (_localSeat == null)
        {
            foreach (var seat in FindObjectsOfType<PlayerSeat>())
            {
                if (seat.IsOwner)
                {
                    _localSeat = seat;
                    break;
                }
            }
            if (_localSeat == null)
                return;
        }

        bool isPlayerSeat =
            _localSeat.CurrentSeat.Value == PlayerSeat.SeatType.Player1 ||
            _localSeat.CurrentSeat.Value == PlayerSeat.SeatType.Player2;

        importDeckButton.gameObject.SetActive(isPlayerSeat);
    }

    private void OnImportDeckClicked()
    {
        if (!NetworkManager.Singleton.IsConnectedClient)
        {
            Debug.LogWarning("[DeckImporter] Not connected.");
            return;
        }

#if UNITY_STANDALONE   // Windows, Mac, Linux builds
        var extensions = new[]
        {
        new ExtensionFilter("JSON Files", "json"),
        new ExtensionFilter("All Files", "*" )
    };

        string[] paths = StandaloneFileBrowser.OpenFilePanel(
            "Select deck JSON",
            "",
            extensions,
            false);

        if (paths != null && paths.Length > 0)
        {
            Debug.Log("[DeckImporter] Selected: " + paths[0]);
            TryImportDeck(paths[0]);
        }
        else
        {
            Debug.Log("[DeckImporter] No file selected.");
        }

#elif UNITY_EDITOR     // Still allow editor picker while testing
    string path = UnityEditor.EditorUtility.OpenFilePanel("Select deck JSON", "", "json");
    if (!string.IsNullOrEmpty(path))
        TryImportDeck(path);

#else
    Debug.LogWarning("[DeckImporter] File import not supported on this platform.");
#endif
    }

    private void TryImportDeck(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            Debug.LogError("[DeckImporter] File does not exist: " + jsonPath);
            return;
        }

        string json = File.ReadAllText(jsonPath);
        DeckDefinition def = JsonUtility.FromJson<DeckDefinition>(json);

        if (def == null || def.cardNames == null || def.cardNames.Length == 0)
        {
            Debug.LogError("[DeckImporter] Invalid JSON: " + jsonPath);
            return;
        }

        // Convert to FixedString128
        FixedString128Bytes[] arr = new FixedString128Bytes[def.cardNames.Length];
        for (int i = 0; i < def.cardNames.Length; i++)
            arr[i] = def.cardNames[i];

        // Compute spawn position relative to this client's camera
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[DeckImporter] No MainCamera on client.");
            return;
        }

        Vector3 spawnPos = cam.transform.position + cam.transform.forward * spawnDistanceFromCamera;
        spawnPos.z = -1f;

        // Client REQUESTS server to spawn at this position
        RequestSpawnDeckServerRpc(arr, spawnPos);
    }

    // ------------------- SERVER RPC -------------------
    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnDeckServerRpc(FixedString128Bytes[] cardNames,
                                       Vector3 spawnPos,
                                       ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        NetworkDeck newDeck = Instantiate(deckPrefab, spawnPos, Quaternion.identity);
        var netObj = newDeck.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Destroy(newDeck.gameObject);
            return;
        }

        // Server owns the spawn -> assign ownership to client
        netObj.SpawnWithOwnership(clientId);

        // Initialize deck data entirely on the server
        newDeck.InitializeFromCardNames(cardNames);
    }
}
