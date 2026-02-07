using System.Collections.Generic;
using UnityEngine;

public class BoardZone : MonoBehaviour
{
    public static readonly List<BoardZone> AllZones = new List<BoardZone>();

    [Header("Snap Settings")]
    [Tooltip("Point where cards/decks will snap. If null, uses this object's transform.")]
    public Transform snapPoint;

    [Tooltip("Maximum distance (world units) at which a piece can snap to this zone.")]
    public float snapRadius = 1.0f;

    [Header("Zone Type")]
    [Tooltip("If true, this zone belongs to a Deck and should not accept cards.")]
    public bool isDeckZone = false;

    private void OnEnable()
    {
        if (!AllZones.Contains(this))
            AllZones.Add(this);
    }

    private void OnDisable()
    {
        AllZones.Remove(this);
    }

    /// <summary>
    /// Try to snap a piece to the closest zone within radius.
    /// Returns true if snapped to any zone.
    /// </summary>
    public static bool TrySnapToClosestZone(Transform pieceTransform, float overrideSnapRadius = -1f)
    {
        if (AllZones.Count == 0 || pieceTransform == null)
            return false;

        BoardZone bestZone = null;
        float bestDistSq = float.MaxValue;

        Vector3 piecePos = pieceTransform.position;

        bool isCard = pieceTransform.GetComponent<NetworkCard>() != null;
        bool isDeck = pieceTransform.GetComponent<NetworkDeck>() != null;

        foreach (var zone in AllZones)
        {
            if (zone == null)
                continue;

            // 🚫 Cards are NOT allowed to snap to deck zones
            if (isCard && zone.isDeckZone)
                continue;

            float radius = overrideSnapRadius > 0f ? overrideSnapRadius : zone.snapRadius;
            float rSq = radius * radius;

            Vector3 zonePos = zone.snapPoint != null
                ? zone.snapPoint.position
                : zone.transform.position;

            float distSq = (zonePos - piecePos).sqrMagnitude;

            if (distSq <= rSq && distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestZone = zone;
            }
        }

        if (bestZone != null)
        {
            Vector3 targetPos = bestZone.snapPoint != null
                ? bestZone.snapPoint.position
                : bestZone.transform.position;

            // Preserve piece's current Z (board plane)
            targetPos.z = pieceTransform.position.z;
            pieceTransform.position = targetPos;
            return true;
        }

        return false;
    }
}
