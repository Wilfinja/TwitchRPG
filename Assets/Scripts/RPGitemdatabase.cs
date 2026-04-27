using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject database that holds all hand-crafted named items,
/// organized by rarity.  Assign RPGItemData assets via the Inspector.
///
/// Create via: Assets > Create > RPG > Item Database
/// </summary>
[CreateAssetMenu(fileName = "RPGItemDatabase", menuName = "RPG/Item Database")]
public class RPGItemDatabase : ScriptableObject
{
    [Header("Named Items by Rarity")]
    [Tooltip("Unique items — one-of-a-kind, story-specific")]
    public List<RPGItemData> namedUniques = new List<RPGItemData>();

    [Tooltip("Legendary items")]
    public List<RPGItemData> namedLegendaries = new List<RPGItemData>();

    [Tooltip("Epic items")]
    public List<RPGItemData> namedEpics = new List<RPGItemData>();

    [Tooltip("Rare items")]
    public List<RPGItemData> namedRares = new List<RPGItemData>();

    // ─────────────────────────────────────────────
    // Lookup
    // ─────────────────────────────────────────────

    /// <summary>
    /// Find a named item by exact name (case-insensitive).
    /// Returns null if not found.
    /// </summary>
    public RPGItemData GetNamedItemData(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return null;

        string lower = itemName.ToLower();

        foreach (var item in namedUniques)
            if (item != null && item.itemName.ToLower() == lower) return item;

        foreach (var item in namedLegendaries)
            if (item != null && item.itemName.ToLower() == lower) return item;

        foreach (var item in namedEpics)
            if (item != null && item.itemName.ToLower() == lower) return item;

        foreach (var item in namedRares)
            if (item != null && item.itemName.ToLower() == lower) return item;

        return null;
    }

    /// <summary>
    /// Returns all RPGItemData assets across every rarity tier.
    /// </summary>
    public List<RPGItemData> GetAllNamedItemData()
    {
        var all = new List<RPGItemData>();
        all.AddRange(namedUniques);
        all.AddRange(namedLegendaries);
        all.AddRange(namedEpics);
        all.AddRange(namedRares);
        return all;
    }

    /// <summary>
    /// Returns all named items converted to runtime RPGItem instances.
    /// </summary>
    public List<RPGItem> GetAllNamedItems()
    {
        var all = new List<RPGItem>();
        foreach (var data in GetAllNamedItemData())
            if (data != null) all.Add(data.ToRPGItem());
        return all;
    }

    // ─────────────────────────────────────────────
    // Editor validation
    // ─────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        CheckForDuplicates(namedUniques, "Uniques");
        CheckForDuplicates(namedLegendaries, "Legendaries");
        CheckForDuplicates(namedEpics, "Epics");
        CheckForDuplicates(namedRares, "Rares");
    }

    private void CheckForDuplicates(List<RPGItemData> list, string label)
    {
        var seen = new HashSet<string>();
        foreach (var item in list)
        {
            if (item == null) continue;
            string lower = item.itemName.ToLower();
            if (!seen.Add(lower))
                Debug.LogWarning($"[RPGItemDatabase] Duplicate item name in {label}: '{item.itemName}'");
        }
    }
#endif
}
