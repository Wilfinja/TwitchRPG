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
        // Load last refresh time from save or default to now
        lastRefreshTime = DateTime.Now;
        nextRefreshTime = lastRefreshTime.AddHours(refreshIntervalHours);

        // Generate initial shop
        RefreshShop();
    }

    private void Update()
    {
        // Auto-refresh if time has elapsed
        if (DateTime.Now >= nextRefreshTime)
        {
            RefreshShop();

            // Notify everyone
            OnScreenNotification.Instance?.ShowSuccess(
                "🏪 The shop has been refreshed!\nType !shop to see new items!"
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

        // Generate new shop inventory
        currentShopItems = HybridItemSystem.Instance.GenerateShopInventory(shopSize);

        // Update refresh times
        lastRefreshTime = DateTime.Now;
        nextRefreshTime = lastRefreshTime.AddHours(refreshIntervalHours);

        Debug.Log($"[Shop] Refreshed with {currentShopItems.Count} items. Next refresh: {nextRefreshTime}");
    }

    public string GetShopDisplay(ViewerData viewer)
    {
        if (currentShopItems.Count == 0)
        {
            return "🏪 Shop is currently empty. Check back later!";
        }

        TimeSpan timeUntilRefresh = nextRefreshTime - DateTime.Now;
        string refreshTimer = $"{timeUntilRefresh.Hours}h {timeUntilRefresh.Minutes}m";

        string display = "════════════════════════════════════\n";
        display += "           🏪 DAILY SHOP 🏪\n";
        display += $"   (Refreshes in {refreshTimer})\n";
        display += "════════════════════════════════════\n";
        display += $"Your coins: 💰 {viewer.coins}\n\n";

        // Group by rarity
        var epics = currentShopItems.FindAll(i => i.rarity == ItemRarity.Epic);
        var rares = currentShopItems.FindAll(i => i.rarity == ItemRarity.Rare);
        var uncommons = currentShopItems.FindAll(i => i.rarity == ItemRarity.Uncommon);
        var commons = currentShopItems.FindAll(i => i.rarity == ItemRarity.Common);

        int itemNumber = 1;

        // Display Epics
        if (epics.Count > 0)
        {
            display += "[EPIC] 🔥\n";
            foreach (var item in epics)
            {
                display += FormatShopItem(itemNumber++, item, viewer);
            }
            display += "\n";
        }

        // Display Rares
        if (rares.Count > 0)
        {
            display += "[RARE] 💎\n";
            foreach (var item in rares)
            {
                display += FormatShopItem(itemNumber++, item, viewer);
            }
            display += "\n";
        }

        // Display Uncommons
        if (uncommons.Count > 0)
        {
            display += "[UNCOMMON] ⭐\n";
            foreach (var item in uncommons)
            {
                display += FormatShopItem(itemNumber++, item, viewer);
            }
            display += "\n";
        }

        // Display Commons
        if (commons.Count > 0)
        {
            display += "[COMMON] ◆\n";
            foreach (var item in commons)
            {
                display += FormatShopItem(itemNumber++, item, viewer);
            }
        }

        display += "\n════════════════════════════════════\n";
        display += "Type: !buy <item name>\n";
        display += "Example: !buy iron sword\n";
        display += "════════════════════════════════════";

        return display;
    }

    private string FormatShopItem(int number, RPGItem item, ViewerData viewer)
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

        // Show what that means for THIS viewer
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
            return false; // Item not in shop
        }

        // Check if can afford
        if (!viewer.CanAfford(item.price))
        {
            return false; // Not enough coins
        }

        // Check inventory space
        if (viewer.inventory.Count >= 50)
        {
            return false; // Inventory full
        }

        // Purchase successful!
        viewer.coins -= item.price;

        // Create a copy of the item (new GUID)
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
