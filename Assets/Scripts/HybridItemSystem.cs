using System.Collections.Generic;
using UnityEngine;

public class HybridItemSystem : MonoBehaviour
{
    // ============================================
    // HYBRID APPROACH: Hand-Crafted + Procedural
    // ============================================

    [Header("Hand-Crafted Named Items")]
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

    // ============================================
    // INITIALIZE HAND-CRAFTED ITEMS
    // ============================================
    private void InitializeNamedItems()
    {
        // You'll add your hand-crafted items here
        // Can also load from JSON file or ScriptableObjects

        // Example: Create a legendary item
        CustomShadowfang();
        CustomStaffOfTheArchmage();
        CustomDragonscalePlate();
        // Add more as you create them...
    }

    // ============================================
    // HAND-CRAFTED ITEM EXAMPLES
    // ============================================

    private void CustomShadowfang()
    {
        RPGItem shadowfang = new RPGItem
        {
            itemName = "Shadowfang",
            description = "A legendary dagger that strikes from the shadows.",
            itemType = ItemType.Weapon,
            rarity = ItemRarity.Legendary,
            requiredLevel = 15,
            price = 5000,

            // Stats
            strengthBonus = 5,
            dexterityBonus = 25,
            intelligenceBonus = 10,
            damageBonus = 40,

            // Class restriction
            allowedClasses = new List<CharacterClass> { CharacterClass.Rogue },

            // Special properties
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
            requiredLevel = 12,
            price = 3500,

            intelligenceBonus = 30,
            willpowerBonus = 20,
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
            requiredLevel = 18,
            price = 6000,

            strengthBonus = 10,
            constitutionBonus = 35,
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
    // GET ITEMS FOR SHOP
    // ============================================

    public List<RPGItem> GenerateShopInventory(int shopSize = 10, int shopLevel = 1)
    {
        List<RPGItem> shopItems = new List<RPGItem>();

        // 1. Maybe add 1 legendary (10% chance)
        if (Random.value < 0.10f && namedLegendaries.Count > 0)
        {
            RPGItem legendary = GetRandomFromList(namedLegendaries);
            shopItems.Add(legendary);
        }

        // 2. Maybe add 1-2 epics (30% chance each)
        int epicCount = Random.value < 0.30f ? 1 : 0;
        if (Random.value < 0.30f) epicCount++;

        for (int i = 0; i < epicCount && namedEpics.Count > 0; i++)
        {
            RPGItem epic = GetRandomFromList(namedEpics);
            if (!shopItems.Contains(epic))
            {
                shopItems.Add(epic);
            }
        }

        // 3. Add 2-3 named rares
        int rareCount = Random.Range(2, 4);
        for (int i = 0; i < rareCount && namedRares.Count > 0; i++)
        {
            RPGItem rare = GetRandomFromList(namedRares);
            if (!shopItems.Contains(rare))
            {
                shopItems.Add(rare);
            }
        }

        // 4. Fill rest with procedural common/uncommon items
        if (enableProceduralGeneration)
        {
            while (shopItems.Count < shopSize)
            {
                RPGItem procedural = GenerateProceduralItem(shopLevel);
                shopItems.Add(procedural);
            }
        }

        return shopItems;
    }

    // ============================================
    // PROCEDURAL GENERATION
    // ============================================

    private RPGItem GenerateProceduralItem(int level)
    {
        // Randomly choose item type
        ItemType type = GetRandomItemType();

        // Common/Uncommon only for procedural
        ItemRarity rarity = Random.value < 0.7f ? ItemRarity.Common : ItemRarity.Uncommon;

        // Generate based on type
        switch (type)
        {
            case ItemType.Weapon:
                return GenerateProceduralWeapon(level, rarity);
            case ItemType.Helmet:
            case ItemType.ChestArmor:
            case ItemType.LegArmor:
            case ItemType.ArmArmor:
            case ItemType.Boots:
                return GenerateProceduralArmor(type, level, rarity);
            default:
                return GenerateProceduralWeapon(level, rarity);
        }
    }

    private RPGItem GenerateProceduralWeapon(int level, ItemRarity rarity)
    {
        RPGItem weapon = new RPGItem();

        // Template name: Material + Weapon Type
        string material = GetRandomMaterial(rarity);
        string weaponType = GetRandomWeaponType();
        weapon.itemName = $"{material} {weaponType}";

        weapon.description = $"A {rarity.ToString().ToLower()} quality {weaponType.ToLower()}.";
        weapon.itemType = ItemType.Weapon;
        weapon.rarity = rarity;
        weapon.requiredLevel = level;
        weapon.price = CalculatePrice(rarity, level);

        // Randomized stats based on rarity
        float rarityMult = GetRarityMultiplier(rarity);
        weapon.strengthBonus = Random.Range(2, 6) * (int)rarityMult;
        weapon.dexterityBonus = Random.Range(1, 4) * (int)rarityMult;
        weapon.damageBonus = Random.Range(5, 12) * (int)rarityMult;

        return weapon;
    }

    private RPGItem GenerateProceduralArmor(ItemType armorType, int level, ItemRarity rarity)
    {
        RPGItem armor = new RPGItem();

        string material = GetRandomMaterial(rarity);
        string armorName = GetArmorTypeName(armorType);
        armor.itemName = $"{material} {armorName}";

        armor.description = $"A {rarity.ToString().ToLower()} quality {armorName.ToLower()}.";
        armor.itemType = armorType;
        armor.rarity = rarity;
        armor.requiredLevel = level;
        armor.price = CalculatePrice(rarity, level);

        float rarityMult = GetRarityMultiplier(rarity);
        armor.constitutionBonus = Random.Range(3, 8) * (int)rarityMult;
        armor.defenseBonus = Random.Range(5, 12) * (int)rarityMult;

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
        string[] weapons = { "Sword", "Axe", "Dagger", "Mace", "Spear", "Bow", "Staff" };
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

    private float GetRarityMultiplier(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return 1.0f;
            case ItemRarity.Uncommon: return 1.5f;
            case ItemRarity.Rare: return 2.0f;
            case ItemRarity.Epic: return 3.0f;
            case ItemRarity.Legendary: return 4.0f;
            default: return 1.0f;
        }
    }

    private int CalculatePrice(ItemRarity rarity, int level)
    {
        int basePrice = 0;
        switch (rarity)
        {
            case ItemRarity.Common: basePrice = 25; break;
            case ItemRarity.Uncommon: basePrice = 100; break;
            case ItemRarity.Rare: basePrice = 400; break;
            case ItemRarity.Epic: basePrice = 1500; break;
            case ItemRarity.Legendary: basePrice = 4000; break;
        }

        return (int)(basePrice * (1 + level * 0.3f));
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
        // Search all named item lists
        foreach (var item in namedLegendaries)
        {
            if (item.itemName.ToLower() == itemName.ToLower())
                return item;
        }
        foreach (var item in namedEpics)
        {
            if (item.itemName.ToLower() == itemName.ToLower())
                return item;
        }
        foreach (var item in namedRares)
        {
            if (item.itemName.ToLower() == itemName.ToLower())
                return item;
        }
        return null;
    }

    public List<RPGItem> GetAllNamedItems()
    {
        List<RPGItem> all = new List<RPGItem>();
        all.AddRange(namedLegendaries);
        all.AddRange(namedEpics);
        all.AddRange(namedRares);
        return all;
    }
}
