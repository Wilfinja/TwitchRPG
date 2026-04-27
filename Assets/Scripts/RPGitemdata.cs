using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject wrapper for a named RPGItem.
/// Create via: Right-click → Create → RPG → Item
/// Supports all rarities — Common through Unique.
/// </summary>
[CreateAssetMenu(fileName = "New RPG Item", menuName = "RPG/Item")]
public class RPGItemData : ScriptableObject
{
    [Header("Identity")]
    public string itemName;
    [TextArea(2, 4)]
    public string description;
    public ItemType itemType;
    public ItemRarity rarity;
    public WeaponCategory weaponCategory;
    public int requiredLevel = 1;
    public int price;

    [Header("Weapon Properties")]
    public bool isTwoHanded;

    [Header("Stat Bonuses (Percentage-Based)")]
    [Range(0f, 1f)] public float strengthBonusPercent;
    [Range(0f, 1f)] public float constitutionBonusPercent;
    [Range(0f, 1f)] public float dexterityBonusPercent;
    [Range(0f, 1f)] public float willpowerBonusPercent;
    [Range(0f, 1f)] public float charismaBonusPercent;
    [Range(0f, 1f)] public float intelligenceBonusPercent;

    [Header("Stat Bonuses (Flat)")]
    public int strengthBonus;
    public int constitutionBonus;
    public int dexterityBonus;
    public int willpowerBonus;
    public int charismaBonus;
    public int intelligenceBonus;

    [Header("Flat Combat Bonuses")]
    public int damageBonus;
    public int defenseBonus;
    public int healAmount;

    [Header("Mana / Spellcasting")]
    public int maxManaBonus;
    public int manaRegenBonus;
    [Range(0f, 0.75f)] public float manaCostReduction;

    [Header("Passive Effects")]
    [Tooltip("Create an RPG asset and place here for the passive effect")]
    public List<ItemPassiveEffect> passives = new List<ItemPassiveEffect> ();
    public AbilityData grantAbility;

    [Header("Class Restrictions")]
    [Tooltip("Leave empty to allow all classes")]
    public List<CharacterClass> allowedClasses = new List<CharacterClass>();

    [Header("Item Abilities")]
    public List<ItemAbilityDefinition> abilities = new List<ItemAbilityDefinition>();

    [Header("Special Properties (Key-Value)")]
    public List<ItemProperty> properties = new List<ItemProperty>();

    // ─────────────────────────────────────────────────────────────────────────
    // Runtime conversion
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts this ScriptableObject into a runtime <see cref="RPGItem"/> instance
    /// with a fresh GUID so each drop/purchase is independent.
    /// </summary>
    public RPGItem ToRPGItem()
    {
        var item = new RPGItem
        {
            itemName = itemName,
            description = description,
            itemType = itemType,
            rarity = rarity,
            weaponCategory = weaponCategory,
            requiredLevel = requiredLevel,
            price = price,
            isTwoHanded = isTwoHanded,

            strengthBonusPercent = strengthBonusPercent,
            constitutionBonusPercent = constitutionBonusPercent,
            dexterityBonusPercent = dexterityBonusPercent,
            willpowerBonusPercent = willpowerBonusPercent,
            charismaBonusPercent = charismaBonusPercent,
            intelligenceBonusPercent = intelligenceBonusPercent,

            damageBonus = damageBonus,
            defenseBonus = defenseBonus,
            healAmount = healAmount,

            maxManaBonus = maxManaBonus,
            manaRegenBonus = manaRegenBonus,
            manaCostReduction = manaCostReduction,

            allowedClasses = new List<CharacterClass>(allowedClasses),

            abilities = new List<ItemAbility>(),
            properties = new Dictionary<string, string>(),
        };

        // Convert abilities
        foreach (var def in abilities)
        {
            item.abilities.Add(new ItemAbility
            {
                abilityName = def.abilityName,
                abilityDescription = def.abilityDescription,
                abilityCommand = def.abilityCommand,
                manaCost = def.manaCost,
                cooldownTurns = def.cooldownTurns,
            });
        }

        // Convert key-value properties
        foreach (var prop in properties)
        {
            if (!string.IsNullOrEmpty(prop.key))
                item.properties[prop.key] = prop.value;
        }

        return item;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Auto-set price if left at 0 using the standard rarity pricing
        if (price == 0)
        {
            price = DefaultPriceForRarity(rarity);
        }
    }

    private static int DefaultPriceForRarity(ItemRarity r)
    {
        switch (r)
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
#endif
}

// ─────────────────────────────────────────────────────────────────────────────
// Supporting serializable types for the Inspector
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Inspector-friendly version of <see cref="ItemAbility"/>.
/// </summary>
[System.Serializable]
public class ItemAbilityDefinition
{
    public string abilityName;
    [TextArea(2, 3)]
    public string abilityDescription;
    public string abilityCommand;
    public int manaCost;
    public int cooldownTurns;
}

/// <summary>
/// A simple string key-value pair for special item properties.
/// </summary>
[System.Serializable]
public class ItemProperty
{
    public string key;
    public string value;
}
