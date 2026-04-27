using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages all named items in the game via ScriptableObject assets.
/// Hand-crafted items of any rarity are dragged into the appropriate list
/// in the Inspector. Common/Uncommon shop slots are filled procedurally
/// when no SO assets are available for that rarity.
///
/// Workflow:
///   1. Right-click in Project → Create → RPG → Item
///   2. Fill in the fields on the asset
///   3. Drag the asset into the matching rarity list below
/// </summary>
public class HybridItemSystem : MonoBehaviour
{
    // ── Named items by rarity (assign SO assets in the Inspector) ────────────

    [Header("Named Items — Unique")]
    [SerializeField] private List<RPGItemData> namedUniques = new List<RPGItemData>();

    [Header("Named Items — Legendary")]
    [SerializeField] private List<RPGItemData> namedLegendaries = new List<RPGItemData>();

    [Header("Named Items — Epic")]
    [SerializeField] private List<RPGItemData> namedEpics = new List<RPGItemData>();

    [Header("Named Items — Rare")]
    [SerializeField] private List<RPGItemData> namedRares = new List<RPGItemData>();

    [Header("Named Items — Uncommon")]
    [SerializeField] private List<RPGItemData> namedUncommons = new List<RPGItemData>();

    [Header("Named Items — Common")]
    [SerializeField] private List<RPGItemData> namedCommons = new List<RPGItemData>();



    // ── Singleton ─────────────────────────────────────────────────────────────

    private static HybridItemSystem _instance;
    public static HybridItemSystem Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<HybridItemSystem>();
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    // ── Shop inventory generation ─────────────────────────────────────────────

    /// <summary>
    /// Builds the daily shop inventory from named SO items only.
    /// Slot allocation (out of <paramref name="shopSize"/>):
    ///   • 1 Unique    — 2 % chance
    ///   • 1 Legendary — 5 % chance
    ///   • 1 Epic      — 10 % chance
    ///   • 2–3 Rare
    ///   • Remainder   — Uncommon / Common (40 / 60 split)
    /// Slots are skipped silently if the matching rarity list is empty.
    /// </summary>
    public List<RPGItem> GenerateShopInventory(int shopSize = 10)
    {
        var shopItems = new List<RPGItem>();

        // Unique (2 %)
        if (Random.value < 0.02f)
            TryAddNamedItem(shopItems, namedUniques);

        // Legendary (5 %)
        if (Random.value < 0.05f)
            TryAddNamedItem(shopItems, namedLegendaries);

        // Epic (10 %)
        if (Random.value < 0.10f)
            TryAddNamedItem(shopItems, namedEpics);

        // 2–3 Rares
        int rareCount = Random.Range(2, 4);
        for (int i = 0; i < rareCount; i++)
            TryAddNamedItem(shopItems, namedRares);

        // Fill remainder with Uncommon / Common
        int attempts = 0; // safety valve — stops if all lists are exhausted
        while (shopItems.Count < shopSize && attempts < shopSize * 4)
        {
            attempts++;
            bool useUncommon = Random.value < 0.4f;
            TryAddNamedItem(shopItems, useUncommon ? namedUncommons : namedCommons);
        }

        if (shopItems.Count == 0)
            Debug.LogWarning("[HybridItemSystem] Shop is empty — add RPGItemData assets to the rarity lists in the Inspector.");

        return shopItems;
    }

    // ── Named item helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Picks a random item from <paramref name="source"/>, converts it to a
    /// runtime RPGItem, and adds it if it isn't already in the shop.
    /// Does nothing if the list is null or empty.
    /// </summary>
    private void TryAddNamedItem(List<RPGItem> shopItems, List<RPGItemData> source)
    {
        if (source == null || source.Count == 0) return;

        RPGItemData data = source[Random.Range(0, source.Count)];
        if (data == null) return;

        RPGItem item = data.ToRPGItem();

        // Avoid duplicate named items in the same shop rotation
        if (!shopItems.Exists(i => i.itemName == item.itemName))
            shopItems.Add(item);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the first named item whose name matches (case-insensitive),
    /// searching all rarity lists. Returns null if not found.
    /// </summary>
    public RPGItem GetNamedItem(string itemName)
    {
        foreach (var list in AllNamedLists())
        {
            var data = list.Find(d => d != null && d.itemName.ToLower() == itemName.ToLower());
            if (data != null) return data.ToRPGItem();
        }
        return null;
    }

    /// <summary>
    /// Returns a runtime RPGItem for every named item across all rarities.
    /// </summary>
    public List<RPGItem> GetAllNamedItems()
    {
        var result = new List<RPGItem>();
        foreach (var list in AllNamedLists())
            foreach (var data in list)
                if (data != null) result.Add(data.ToRPGItem());
        return result;
    }

    /// <summary>
    /// Returns all named items of a specific rarity as runtime RPGItems.
    /// </summary>
    public List<RPGItem> GetNamedItemsByRarity(ItemRarity rarity)
    {
        var result = new List<RPGItem>();
        foreach (var data in GetListForRarity(rarity))
            if (data != null) result.Add(data.ToRPGItem());
        return result;
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private IEnumerable<List<RPGItemData>> AllNamedLists()
    {
        yield return namedUniques;
        yield return namedLegendaries;
        yield return namedEpics;
        yield return namedRares;
        yield return namedUncommons;
        yield return namedCommons;
    }

    private List<RPGItemData> GetListForRarity(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Unique: return namedUniques;
            case ItemRarity.Legendary: return namedLegendaries;
            case ItemRarity.Epic: return namedEpics;
            case ItemRarity.Rare: return namedRares;
            case ItemRarity.Uncommon: return namedUncommons;
            case ItemRarity.Common: return namedCommons;
            default: return namedCommons;
        }
    }
}
