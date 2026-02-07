using System;
using System.Collections.Generic;
using UnityEngine;

public class CardArtLibrary : MonoBehaviour
{
    public static CardArtLibrary Instance { get; private set; }

    [SerializeField] private CardArtDatabase database;

    private Dictionary<string, Sprite> _lookup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildLookup();
        DontDestroyOnLoad(gameObject); // optional but handy
    }

    private void OnEnable()
    {
        // in case object gets disabled/enabled across scenes
        if (_lookup == null || _lookup.Count == 0)
            BuildLookup();
    }

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        if (database == null)
        {
            Debug.LogError("[CardArtLibrary] Database not assigned.");
            return;
        }

        int added = 0;
        foreach (var e in database.entries)
        {
            var key = Sanitize(e.cardName);
            if (string.IsNullOrEmpty(key) || e.sprite == null)
                continue;

            if (!_lookup.ContainsKey(key))
            {
                _lookup.Add(key, e.sprite);
                added++;
            }
        }

        Debug.Log($"[CardArtLibrary] Built lookup. Entries={database.entries.Count}, Added={added}");
    }

    public Sprite GetSprite(string cardName)
    {
        if (_lookup == null || _lookup.Count == 0)
            BuildLookup();

        var key = Sanitize(cardName);
        if (string.IsNullOrEmpty(key))
            return null;

        if (_lookup.TryGetValue(key, out var sprite))
            return sprite;

        Debug.LogWarning($"[CardArtLibrary] Missing sprite for '{cardName}' (sanitized='{key}')");
        return null;
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;

        // Trim + remove common hidden characters
        return s.Trim()
                .Replace("\u200B", "")   // zero-width space
                .Replace("\r", "")
                .Replace("\n", "");
    }
}
