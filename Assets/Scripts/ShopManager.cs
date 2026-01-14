using System;
using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [Header("Shop Settings")]
    [SerializeField] private int shopSize = 10;
    [SerializeField] private float refreshIntervalHours = 24f;

    [Header("Current Shop")]
    private List<RPGItem> currentShopItems = new List<RPGItem>();
    private DateTime lastRefreshTime;
    private DateTime nextRefreshTime;

    private static ShopManager _instance;
    public static ShopManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ShopManager>();
            }
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

    private void Start()
    {
        lastRefreshTime = DateTime.Now;
        nextRefreshTime = lastRefreshTime.AddHours(refreshIntervalHours);
        RefreshShop();
    }

    private void Update()
    {
        // Auto-refresh if time has elapsed
        if (DateTime.Now >= nextRefreshTime)
        {
            RefreshShop();
            OnScreenNotification.Instance?.ShowSuccess(
                "The shop has been refreshed!\nType !shop to see new items!"
            );
        }
    }

    public void RefreshShop()
    {
        if (HybridItemSystem.Instance == null)
        {
            Debug.LogError("[Shop] HybridItemSystem not found!");
            return;
        }

        currentShopItems = HybridItemSystem.Instance.GenerateShopInventory(shopSize);
        lastRefreshTime = DateTime.Now;
        nextRefreshTime = lastRefreshTime.AddHours(refreshIntervalHours);

        Debug.Log($"[Shop] Refreshed with {currentShopItems.Count} items. Next refresh: {nextRefreshTime}");
    }

    // ============================================
    // PAGE SYSTEM
    // ============================================

    public string GetShopPage(ViewerData viewer, int page = 1)
    {
        if (currentShopItems.Count == 0)
        {
            return "Shop is currently empty. Check back later!";
        }

        switch (page)
        {
            case 1:
                return GetPage1_Overview(viewer);
            case 2:
                return GetPage2_Commons(viewer);
            case 3:
                return GetPage3_Uncommons(viewer);
            case 4:
                return GetPage4_RaresAndEpics(viewer);
            default:
                return GetPage1_Overview(viewer);
        }
    }

    // ============================================
    // PAGE 1: OVERVIEW (Just Names)
    // ============================================

    private string GetPage1_Overview(ViewerData viewer)
    {
        TimeSpan timeUntilRefresh = nextRefreshTime - DateTime.Now;
        string refreshTimer = $"{timeUntilRefresh.Hours}h {timeUntilRefresh.Minutes}m";

        string display = "════════════════════════════════════\n";
        display += "           🏪 DAILY SHOP 🏪\n";
        display += $"   (Refreshes in {refreshTimer})\n";
        display += "════════════════════════════════════\n";
        display += $"Your coins: {viewer.coins}\n\n";

        // Group by rarity
        var epics = currentShopItems.FindAll(i => i.rarity == ItemRarity.Epic);
        var rares = currentShopItems.FindAll(i => i.rarity == ItemRarity.Rare);
        var uncommons = currentShopItems.FindAll(i => i.rarity == ItemRarity.Uncommon);
        var commons = currentShopItems.FindAll(i => i.rarity == ItemRarity.Common);

        // Show Epics
        if (epics.Count > 0)
        {
            display += "[EPIC] 🔥\n";
            foreach (var item in epics)
            {
                display += $"  • {item.itemName} ({item.price}c)\n";
            }
            display += "\n";
        }

        // Show Rares
        if (rares.Count > 0)
        {
            display += "[RARE] 💎\n";
            foreach (var item in rares)
            {
                display += $"  • {item.itemName} ({item.price}c)\n";
            }
            display += "\n";
        }

        // Show Uncommons count
        if (uncommons.Count > 0)
        {
            display += $"[UNCOMMON] ⭐ - {uncommons.Count} items\n";
            display += "  Type !shop 3 to view\n\n";
        }

        // Show Commons count
        if (commons.Count > 0)
        {
            display += $"[COMMON] ◆ - {commons.Count} items\n";
            display += "  Type !shop 2 to view\n\n";
        }

        display += "════════════════════════════════════\n";
        display += "!shop 2 = Commons | !shop 3 = Uncommons\n";
        display += "!shop 4 = Rares/Epics\n";
        display += "!buy <item name> to purchase\n";
        display += "════════════════════════════════════";

        return display;
    }

    // ============================================
    // PAGE 2: COMMONS (Detailed)
    // ============================================

    private string GetPage2_Commons(ViewerData viewer)
    {
        var commons = currentShopItems.FindAll(i => i.rarity == ItemRarity.Common);

        if (commons.Count == 0)
        {
            return "════════════════════════════════════\n" +
                   "  SHOP - COMMONS\n" +
                   "════════════════════════════════════\n" +
                   "No common items in stock!\n\n" +
                   "Type !shop to return to overview\n" +
                   "════════════════════════════════════";
        }

        string display = "════════════════════════════════════\n";
        display += "        SHOP - COMMONS ◆\n";
        display += "════════════════════════════════════\n";
        display += $"Your coins: 💰 {viewer.coins}\n\n";

        int num = 1;
        foreach (var item in commons)
        {
            display += FormatDetailedItem(num++, item, viewer);
            display += "\n";
        }

        display += "════════════════════════════════════\n";
        display += "!buy <item name> to purchase\n";
        display += "!shop = Return to overview\n";
        display += "════════════════════════════════════";

        return display;
    }

    // ============================================
    // PAGE 3: UNCOMMONS (Detailed)
    // ============================================

    private string GetPage3_Uncommons(ViewerData viewer)
    {
        var uncommons = currentShopItems.FindAll(i => i.rarity == ItemRarity.Uncommon);

        if (uncommons.Count == 0)
        {
            return "════════════════════════════════════\n" +
                   "  SHOP - UNCOMMONS\n" +
                   "════════════════════════════════════\n" +
                   "No uncommon items in stock!\n\n" +
                   "Type !shop to return to overview\n" +
                   "════════════════════════════════════";
        }

        string display = "════════════════════════════════════\n";
        display += "        SHOP - UNCOMMONS ⭐\n";
        display += "════════════════════════════════════\n";
        display += $"Your coins: {viewer.coins}\n\n";

        int num = 1;
        foreach (var item in uncommons)
        {
            display += FormatDetailedItem(num++, item, viewer);
            display += "\n";
        }

        display += "════════════════════════════════════\n";
        display += "!buy <item name> to purchase\n";
        display += "!shop = Return to overview\n";
        display += "════════════════════════════════════";

        return display;
    }

    // ============================================
    // PAGE 4: RARES & EPICS (Detailed)
    // ============================================

    private string GetPage4_RaresAndEpics(ViewerData viewer)
    {
        var rares = currentShopItems.FindAll(i => i.rarity == ItemRarity.Rare);
        var epics = currentShopItems.FindAll(i => i.rarity == ItemRarity.Epic);
        var combined = new List<RPGItem>();
        combined.AddRange(epics);
        combined.AddRange(rares);

        if (combined.Count == 0)
        {
            return "════════════════════════════════════\n" +
                   "  SHOP - RARES & EPICS\n" +
                   "════════════════════════════════════\n" +
                   "No rare or epic items in stock!\n\n" +
                   "Type !shop to return to overview\n" +
                   "════════════════════════════════════";
        }

        string display = "════════════════════════════════════\n";
        display += "      SHOP - RARES & EPICS\n";
        display += "════════════════════════════════════\n";
        display += $"Your coins: 💰 {viewer.coins}\n\n";

        int num = 1;
        foreach (var item in combined)
        {
            display += FormatDetailedItem(num++, item, viewer);
            display += "\n";
        }

        display += "════════════════════════════════════\n";
        display += "!buy <item name> to purchase\n";
        display += "!shop = Return to overview\n";
        display += "════════════════════════════════════";

        return display;
    }

    // ============================================
    // FORMAT ITEM WITH DETAILS
    // ============================================

    private string FormatDetailedItem(int number, RPGItem item, ViewerData viewer)
    {
        string line = $"{number}. {item.itemName} - {item.price} coins\n";

        // Show percentage bonuses
        List<string> bonuses = new List<string>();

        if (item.strengthBonusPercent > 0)
            bonuses.Add($"+{item.strengthBonusPercent * 100:F0}% STR");
        if (item.constitutionBonusPercent > 0)
            bonuses.Add($"+{item.constitutionBonusPercent * 100:F0}% CON");
        if (item.dexterityBonusPercent > 0)
            bonuses.Add($"+{item.dexterityBonusPercent * 100:F0}% DEX");
        if (item.willpowerBonusPercent > 0)
            bonuses.Add($"+{item.willpowerBonusPercent * 100:F0}% WIL");
        if (item.charismaBonusPercent > 0)
            bonuses.Add($"+{item.charismaBonusPercent * 100:F0}% CHA");
        if (item.intelligenceBonusPercent > 0)
            bonuses.Add($"+{item.intelligenceBonusPercent * 100:F0}% INT");

        if (item.damageBonus > 0)
            bonuses.Add($"+{item.damageBonus} Damage");
        if (item.defenseBonus > 0)
            bonuses.Add($"+{item.defenseBonus} Defense");

        if (bonuses.Count > 0)
        {
            line += "   " + string.Join(", ", bonuses) + "\n";
        }

        // Show calculated bonus for THIS viewer
        CharacterStats viewerStats = viewer.baseStats;
        List<string> yourBonuses = new List<string>();

        if (item.strengthBonusPercent > 0)
        {
            int bonus = Mathf.Max(1, Mathf.RoundToInt(viewerStats.strength * item.strengthBonusPercent));
            yourBonuses.Add($"+{bonus} STR");
        }
        if (item.constitutionBonusPercent > 0)
        {
            int bonus = Mathf.Max(1, Mathf.RoundToInt(viewerStats.constitution * item.constitutionBonusPercent));
            yourBonuses.Add($"+{bonus} CON");
        }
        if (item.dexterityBonusPercent > 0)
        {
            int bonus = Mathf.Max(1, Mathf.RoundToInt(viewerStats.dexterity * item.dexterityBonusPercent));
            yourBonuses.Add($"+{bonus} DEX");
        }
        if (item.willpowerBonusPercent > 0)
        {
            int bonus = Mathf.Max(1, Mathf.RoundToInt(viewerStats.willpower * item.willpowerBonusPercent));
            yourBonuses.Add($"+{bonus} WIL");
        }
        if (item.charismaBonusPercent > 0)
        {
            int bonus = Mathf.Max(1, Mathf.RoundToInt(viewerStats.charisma * item.charismaBonusPercent));
            yourBonuses.Add($"+{bonus} CHA");
        }
        if (item.intelligenceBonusPercent > 0)
        {
            int bonus = Mathf.Max(1, Mathf.RoundToInt(viewerStats.intelligence * item.intelligenceBonusPercent));
            yourBonuses.Add($"+{bonus} INT");
        }

        if (yourBonuses.Count > 0)
        {
            line += "   Your bonus: " + string.Join(", ", yourBonuses) + "\n";
        }

        // Show class restriction
        if (item.allowedClasses.Count > 0)
        {
            string classes = string.Join(", ", item.allowedClasses);
            line += $"   Class: {classes} only\n";
        }

        return line;
    }

    // ============================================
    // PURCHASE SYSTEM (Unchanged)
    // ============================================

    public bool PurchaseItem(string userId, string itemName)
    {
        ViewerData viewer = RPGManager.Instance.GetViewer(userId);
        if (viewer == null)
        {
            Debug.LogError("[Shop] Viewer not found!");
            return false;
        }

        // Find item in shop (case-insensitive)
        RPGItem item = currentShopItems.Find(i => i.itemName.ToLower() == itemName.ToLower());

        if (item == null)
        {
            return false;
        }

        if (!viewer.CanAfford(item.price))
        {
            return false;
        }

        if (viewer.inventory.Count >= 50)
        {
            return false;
        }

        // Purchase successful
        viewer.coins -= item.price;

        // Create a copy
        RPGItem purchasedItem = new RPGItem
        {
            itemName = item.itemName,
            description = item.description,
            itemType = item.itemType,
            rarity = item.rarity,
            requiredLevel = item.requiredLevel,
            price = item.price,

            strengthBonusPercent = item.strengthBonusPercent,
            constitutionBonusPercent = item.constitutionBonusPercent,
            dexterityBonusPercent = item.dexterityBonusPercent,
            willpowerBonusPercent = item.willpowerBonusPercent,
            charismaBonusPercent = item.charismaBonusPercent,
            intelligenceBonusPercent = item.intelligenceBonusPercent,

            damageBonus = item.damageBonus,
            defenseBonus = item.defenseBonus,
            healAmount = item.healAmount,

            allowedClasses = new List<CharacterClass>(item.allowedClasses),
            properties = new Dictionary<string, string>(item.properties)
        };

        viewer.AddItem(purchasedItem);
        RPGManager.Instance.SaveGameData();

        Debug.Log($"[Shop] {viewer.username} purchased {item.itemName} for {item.price} coins");
        return true;
    }

    public List<RPGItem> GetCurrentShopItems()
    {
        return new List<RPGItem>(currentShopItems);
    }

    public TimeSpan GetTimeUntilRefresh()
    {
        return nextRefreshTime - DateTime.Now;
    }
}
