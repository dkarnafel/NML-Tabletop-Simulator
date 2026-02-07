#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardArtDatabase))]
public class CardArtDatabaseEditor : Editor
{
    // Change this to the folder where your card sprites live
    private const string DefaultSpritesFolder = "Assets/Cards/CardArtLibrary";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Auto Build", EditorStyles.boldLabel);

        if (GUILayout.Button("Rebuild Entries From Folder"))
        {
            RebuildFromFolder((CardArtDatabase)target, DefaultSpritesFolder);
        }

        if (GUILayout.Button("Rebuild Entries From Selected Assets"))
        {
            RebuildFromSelection((CardArtDatabase)target);
        }
    }

    private static void RebuildFromFolder(CardArtDatabase db, string folder)
    {
        Undo.RecordObject(db, "Rebuild CardArtDatabase Entries");

        db.entries.Clear();

        // Finds all Sprite assets inside folder
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;

            // Default card name = sprite name (can be customized below)
            string cardName = sprite.name.Replace("_", " ").TrimEnd(' ', '0', '1', '2');

            db.entries.Add(new CardArtDatabase.Entry
            {
                cardName = cardName,
                sprite = sprite
            });
        }

        // Sort by name
        db.entries = db.entries.OrderBy(e => e.cardName, StringComparer.OrdinalIgnoreCase).ToList();

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        Debug.Log($"[CardArtDatabase] Rebuilt {db.entries.Count} entries from: {folder}");
    }

    private static void RebuildFromSelection(CardArtDatabase db)
    {
        Undo.RecordObject(db, "Rebuild CardArtDatabase Entries");

        db.entries.Clear();

        var sprites = Selection.objects
            .Select(o => o as Sprite)
            .Where(s => s != null)
            .ToList();

        foreach (var sprite in sprites)
        {
            db.entries.Add(new CardArtDatabase.Entry
            {
                cardName = sprite.name,
                sprite = sprite
            });
        }

        db.entries = db.entries.OrderBy(e => e.cardName, StringComparer.OrdinalIgnoreCase).ToList();

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        Debug.Log($"[CardArtDatabase] Rebuilt {db.entries.Count} entries from Selection.");
    }
}
#endif
