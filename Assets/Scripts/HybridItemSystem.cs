using System.Collections.Generic;
using UnityEngine;

public class HybridItemSystem : MonoBehaviour
{
    [Header("Hand-Crafted Named Items")]
    [SerializeField] private List<RPGItem> namedUniques = new List<RPGItem>();
    [SerializeField] private List<RPGItem> namedLegendaries = new List<RPGItem>();
    [SerializeField] private List<RPGItem> namedEpics = new List<RPGItem>();
    [SerializeField] private List<RPGItem> namedRares = new List<RPGItem>();

    [Header("Procedural Generation")]
    [SerializeField] private bool enableProceduralGeneration = true;

    private static HybridItemSystem _instance;
    public static HybridItemSystem Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<HybridItemSystem>();
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

        InitializeNamedItems();
    }

    private void InitializeNamedItems()
    {
        // Hand-crafted items
        CustomShadowfang();
        CustomStaffOfTheArchmage();
        CustomDragonscalePlate();
        // Add more as you create them...
    }

    // ============================================
    // HAND-CRAFTED ITEMS (FIXED PERCENTAGES)
    // ============================================

    private void CustomShadowfang()
    {
        RPGItem shadowfang = new RPGItem
        {
            itemName = "Shadowfang",
            description = "A legendary dagger that strikes from the shadows.",
            itemType = ItemType.Weapon,
            rarity = ItemRarity.Legendary,
            requiredLevel = 0,
            price = 10000,
            isTwoHanded = false,  // One-handed dagger

            strengthBonusPercent = 0.35f,
            dexterityBonusPercent = 0.25f,
            intelligenceBonusPercent = 0.10f,
            damageBonus = 40,

            allowedClasses = new List<CharacterClass> { CharacterClass.Rogue },

            properties = new Dictionary<string, string>
        {
            { "CritChance", "+15%" },
            { "Backstab", "+50% damage from stealth" },
            { "Passive", "Gain 1 sneak on kill" }
        }
        };

        namedLegendaries.Add(shadowfang);
    }

    private void CustomStaffOfTheArchmage()
    {
        RPGItem staff = new RPGItem
        {
            itemName = "Staff of the Archmage",
            description = "Crackling with ancient magical power.",
            itemType = ItemType.Weapon,
            rarity = ItemRarity.Epic,
            requiredLevel = 0,
            price = 3500,
            isTwoHanded = true,  // Two-handed staff!

            intelligenceBonusPercent = 0.30f,
            willpowerBonusPercent = 0.20f,
            damageBonus = 35,

            allowedClasses = new List<CharacterClass> { CharacterClass.Mage },

            properties = new Dictionary<string, string>
        {
            { "ManaRegen", "+5 per turn" },
            { "SpellPower", "+20%" },
            { "Passive", "Spells cost 10% less mana" }
        }
        };

        namedEpics.Add(staff);
    }

    private void CustomDragonscalePlate()
    {
        RPGItem plate = new RPGItem
        {
            itemName = "Dragonscale Plate",
            description = "Forged from the scales of an ancient dragon.",
            itemType = ItemType.ChestArmor,
            rarity = ItemRarity.Legendary,
            requiredLevel = 0,
            price = 6000,

            strengthBonusPercent = 0.10f,
            constitutionBonusPercent = 0.35f,
            defenseBonus = 50,

            allowedClasses = new List<CharacterClass> { CharacterClass.Fighter },

            properties = new Dictionary<string, string>
            {
                { "FireResist", "+50%" },
                { "Thorns", "Reflect 15% damage" },
                { "Passive", "Immune to burn effects" }
            }
        };

        namedLegendaries.Add(plate);
    }

    // ============================================
    // GENERATE SHOP INVENTORY (FIXED FOR YOUR SPECS)
    // ============================================

    public List<RPGItem> GenerateShopInventory(int shopSize = 10)
    {
        List<RPGItem> shopItems = new List<RPGItem>();

        // NO LEGENDARIES OR UNIQUES IN SHOP!
        // They come from admin/expeditions only

        // 1. Maybe add 1 Epic (5% chance) if we have any
        if (Random.value < 0.05f && namedEpics.Count > 0)
        {
            RPGItem epic = GetRandomFromList(namedEpics);
            shopItems.Add(epic);
        }

        // 2. Add 2-3 Rares (20-30% of shop)
        int rareCount = Random.Range(2, 4);
        for (int i = 0; i < rareCount; i++)
        {
            if (namedRares.Count > 0)
            {
                RPGItem rare = GetRandomFromList(namedRares);
                if (!shopItems.Contains(rare))
                {
                    shopItems.Add(rare);
                }
            }
            else if (enableProceduralGeneration)
            {
                // Generate procedural rare if no hand-crafted ones
                shopItems.Add(GenerateProceduralItem(ItemRarity.Rare));
            }
        }

        // 3. Fill rest with procedural Common/Uncommon items
        if (enableProceduralGeneration)
        {
            while (shopItems.Count < shopSize)
            {
                // 60% Common, 40% Uncommon
                ItemRarity rarity = Random.value < 0.6f ? ItemRarity.Common : ItemRarity.Uncommon;
                RPGItem procedural = GenerateProceduralItem(rarity);
                shopItems.Add(procedural);
            }
        }

        return shopItems;
    }

    // ============================================
    // PROCEDURAL GENERATION (FIXED PERCENTAGES)
    // ============================================

    private RPGItem GenerateProceduralItem(ItemRarity rarity)
    {
        ItemType type = GetRandomItemType();

        switch (type)
        {
            case ItemType.Weapon:
                return GenerateProceduralWeapon(rarity);
            case ItemType.Helmet:
            case ItemType.ChestArmor:
            case ItemType.LegArmor:
            case ItemType.ArmArmor:
            case ItemType.Boots:
                return GenerateProceduralArmor(type, rarity);
            default:
                return GenerateProceduralWeapon(rarity);
        }
    }

    private RPGItem GenerateProceduralWeapon(ItemRarity rarity)
    {
        RPGItem weapon = new RPGItem();

        string material = GetRandomMaterial(rarity);
        string weaponType = GetRandomWeaponType();
        weapon.itemName = $"{material} {weaponType}";
        weapon.description = $"A {rarity.ToString().ToLower()} quality {weaponType.ToLower()}.";
        weapon.itemType = ItemType.Weapon;
        weapon.rarity = rarity;
        weapon.requiredLevel = 0;
        weapon.price = CalculatePrice(rarity);

        // Mark certain weapon types as two-handed
        weapon.isTwoHanded = IsTwoHandedWeaponType(weaponType);

        // Use standard rarity percentages
        float basePercent = RPGItem.GetRarityPercentageBonus(rarity);

        // Weapons focus on STR or DEX
        if (Random.value < 0.5f)
        {
            weapon.strengthBonusPercent = basePercent;
        }
        else
        {
            weapon.dexterityBonusPercent = basePercent;
        }

        // Two-handed weapons get bonus damage
        int damageMultiplier = weapon.isTwoHanded ? 2 : 1;
        weapon.damageBonus = (rarity == ItemRarity.Common ? Random.Range(3, 6) : Random.Range(8, 15)) * damageMultiplier;

        return weapon;
    }

    private bool IsTwoHandedWeaponType(string weaponType)
    {
        // Two-handed weapon types
        string[] twoHandedTypes = { "Bow", "Staff", "Spear", "Greatsword", "Warhammer" };

        foreach (string type in twoHandedTypes)
        {
            if (weaponType.Contains(type))
                return true;
        }

        return false;
    }

    private RPGItem GenerateProceduralArmor(ItemType armorType, ItemRarity rarity)
    {
        RPGItem armor = new RPGItem();

        string material = GetRandomMaterial(rarity);
        string armorName = GetArmorTypeName(armorType);
        armor.itemName = $"{material} {armorName}";
        armor.description = $"A {rarity.ToString().ToLower()} quality {armorName.ToLower()}.";
        armor.itemType = armorType;
        armor.rarity = rarity;
        armor.requiredLevel = 0;
        armor.price = CalculatePrice(rarity);

        // Use standard rarity percentages
        float basePercent = RPGItem.GetRarityPercentageBonus(rarity);

        // Armor focuses on CON
        armor.constitutionBonusPercent = basePercent;

        // Flat defense based on rarity
        armor.defenseBonus = rarity == ItemRarity.Common ? Random.Range(3, 8) : Random.Range(10, 20);

        return armor;
    }

    // ============================================
    // HELPER METHODS
    // ============================================

    private string GetRandomMaterial(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common:
                return Random.value < 0.5f ? "Iron" : "Wooden";
            case ItemRarity.Uncommon:
                return Random.value < 0.5f ? "Steel" : "Bronze";
            case ItemRarity.Rare:
                return Random.value < 0.5f ? "Mithril" : "Silver";
            default:
                return "Iron";
        }
    }

    private string GetRandomWeaponType()
    {
        // Mix of one-handed and two-handed weapons
        string[] weapons = {
        "Sword", "Axe", "Dagger", "Mace",           // One-handed
        "Bow", "Staff", "Spear", "Greatsword"       // Two-handed
    };
        return weapons[Random.Range(0, weapons.Length)];
    }

    private string GetArmorTypeName(ItemType type)
    {
        switch (type)
        {
            case ItemType.Helmet: return "Helm";
            case ItemType.ChestArmor: return "Chestplate";
            case ItemType.LegArmor: return "Leggings";
            case ItemType.ArmArmor: return "Gauntlets";
            case ItemType.Boots: return "Boots";
            default: return "Armor";
        }
    }

    private ItemType GetRandomItemType()
    {
        ItemType[] types = {
            ItemType.Weapon, ItemType.Helmet, ItemType.ChestArmor,
            ItemType.LegArmor, ItemType.ArmArmor, ItemType.Boots
        };
        return types[Random.Range(0, types.Length)];
    }

    private int CalculatePrice(ItemRarity rarity)
    {
        // Fixed prices per your specs
        switch (rarity)
        {
            case ItemRarity.Common: return 50;
            case ItemRarity.Uncommon: return 200;
            case ItemRarity.Rare: return 800;
            case ItemRarity.Epic: return 3000;
            case ItemRarity.Legendary: return 10000;
            case ItemRarity.Unique: return 15000;
            default: return 50;
        }
    }

    private T GetRandomFromList<T>(List<T> list)
    {
        if (list.Count == 0) return default(T);
        return list[Random.Range(0, list.Count)];
    }

    // ============================================
    // PUBLIC API
    // ============================================

    public RPGItem GetNamedItem(string itemName)
    {
        // Search all lists
        foreach (var item in namedUniques)
            if (item.itemName.ToLower() == itemName.ToLower()) return item;

        foreach (var item in namedLegendaries)
            if (item.itemName.ToLower() == itemName.ToLower()) return item;

        foreach (var item in namedEpics)
            if (item.itemName.ToLower() == itemName.ToLower()) return item;

        foreach (var item in namedRares)
            if (item.itemName.ToLower() == itemName.ToLower()) return item;

        return null;
    }

    public List<RPGItem> GetAllNamedItems()
    {
        List<RPGItem> all = new List<RPGItem>();
        all.AddRange(namedUniques);
        all.AddRange(namedLegendaries);
        all.AddRange(namedEpics);
        all.AddRange(namedRares);
        return all;
    }
}
