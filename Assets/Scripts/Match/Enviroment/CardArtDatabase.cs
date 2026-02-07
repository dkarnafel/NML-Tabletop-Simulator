using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CardArtDatabase",
    menuName = "NoMansLand/Card Art Database")]
public class CardArtDatabase : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string cardName;
        public Sprite sprite;
    }

    public List<Entry> entries = new List<Entry>();
}
