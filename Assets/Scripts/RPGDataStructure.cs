using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    Unique
}

[Serializable]
public enum ItemType
{
    Weapon,
    Shield,      // Offhand-only for Fighters/Clerics
    Trinket,     // Offhand-only for Mages
    Helmet,
    ChestArmor,
    LegArmor,
    ArmArmor,
    Boots,
    Consumable
}

[Serializable]
public enum WeaponCategory
{
    None,
    Sword,       // One-handed, mainhand-only
    Axe,         // One-handed, mainhand-only
    Mace,        // One-handed, mainhand-only
    Dagger,      // One-handed, can dual wield (Rogue/Ranger)
    Bow,         // Two-handed
    Staff,       // Two-handed
    Spear,       // Two-handed
    Greatsword,  // Two-handed
    Warhammer    // Two-handed
}

[Serializable]
public enum CharacterClass
{
    None,
    Rogue,
    Cleric,
    Fighter,
    Ranger,
    Mage
}





// ===== ITEM ABILITY SYSTEM =====
[Serializable]
public class ItemAbility
{
    public string abilityName;
    public string abilityDescription;
    public string abilityCommand;
    public int manaCost;
    public int cooldownTurns;
}

[Serializable]
public class CombatAbility
{
    public string abilityName;
    public string commandName; // What players type
    [TextArea(3, 5)]
    public string description;
    public CharacterClass requiredClass;

    // Ability type
    public AbilityCategory category; // Buff, Heal, Damage
    public AbilityTargetType targetType;

    // Damage/Healing
    public DamageStat scalingStat;
    public float statMultiplier = 1f;
    public int baseDamage;

    // Resource costs (reuse existing ClassResources structure)
    public int sneakCost;
    public int sneakGain;
    public int manaCost;
    public int wrathCost;
    public int wrathGain;
    public int balanceCost;
    public int balanceGain;
    public int balanceRequirement;
    public BalanceRequirementType balanceRequirementType;

    // Targeting
    public bool canTargetAllies;
    public bool canTargetEnemies = true;
    public int maxTargetPosition = 1;
    public bool isAOE;
    public int aoeTargets = 1;

    public int cooldown;
}

[Serializable]
public enum FighterStance
{
    None,
    Defensive,
    Aggressive,
    Balanced,
    Reflective
}

[Serializable]
public enum BoostableStat
{
    None,
    Strength,
    Constitution,
    Dexterity,
    Willpower,
    Charisma,
    Intelligence
}

[Serializable]
public class StatusEffect
{
    // ── Core ─────────────────────────────────────────────────────────────────
    public string effectName;
    public int duration;

    // ── Proc Chance ──────────────────────────────────────────────────────────
    [Tooltip("Probability this effect is applied when triggered. 1.0 = always, 0.5 = 50% chance.")]
    [Range(0f, 1f)]
    public float applicationChance = 1f;

    [Tooltip("Mark TRUE for any effect that harms or restricts the target: " +
             "Stun, Silence, Bleed, Curse, Exposed, Marked, Enrage, Taunt, or any DoT. " +
             "Leave FALSE for beneficial effects: Barrier, Haste, stat buffs, Riposte, Lifesteal. " +
             "\n\nWhen true, the target's Status Resistance (derived from Willpower) is " +
             "rolled against AFTER the applicationChance roll. Both must pass for the effect to land.")]
    public bool isNegativeEffect = false;

    [Tooltip("Flat bonus added to the bearer's Status Resistance while this effect is active. " +
             "Use on buff abilities like 'Iron Will' or 'Fortify' to grant extra resist chance. " +
             "0.1 = +10% resistance. Stacks additively with other bonuses before the 75% cap.")]
    public float statusResistanceBonus = 0f;

    // ── Original Multipliers ─────────────────────────────────────────────────
    public float damageMultiplier = 1f;
    public float defenseMultiplier = 1f;
    public int damageOverTime;

    [Tooltip("Flat defense bonus added while active")]
    public int temporaryDefenseBonus;

    [Tooltip("Base defense amount before scaling")]
    public int baseDefenseAmount = 0;

    [Tooltip("Stat to scale defense bonus with")]
    public DamageStat defenseScalingStat = DamageStat.None;

    [Tooltip("Multiplier for defense scaling (e.g., 0.5 = +50% of stat)")]
    public float defenseScalingMultiplier = 0f;

    [Tooltip("If true, removed after 1 hit instead of after duration expires")]
    public bool consumedOnHit;

    [Tooltip("Which stat is being boosted")]
    public BoostableStat statBoostType;

    [Tooltip("Amount the stat is boosted")]
    public int statBoostAmount;

    [Tooltip("Percentage of damage dealt that heals the attacker (0.0-1.0)")]
    [Range(0f, 1f)]
    public float lifestealPercent = 0f;

    // ── Riposte ──────────────────────────────────────
    [Tooltip("Marks this effect as a Riposte. When the bearer takes damage, a counter-attack fires.")]
    public bool isRiposte = false;

    [Tooltip("Percent of incoming final damage (post-defense) reflected as counter damage. 1.0 = 100%.")]
    public float riposteDamagePercent = 0f;

    [Tooltip("Flat bonus damage added to the riposte counter.")]
    public int riposteFlatBonus = 0;

    [Tooltip("Stat used for additional riposte scaling. Looked up on the entity when the counter fires.")]
    public DamageStat riposteScalingStat = DamageStat.None;

    [Tooltip("Multiplier applied to the riposteScalingStat value.")]
    public float riposteScalingMultiplier = 0f;

    [Tooltip("If true, this Riposte effect is removed after the first successful counter-attack.")]
    public bool riposteConsumedOnUse = true;

    // ── Stun ─────────────────────────────────────────────────────────────────
    [Header("Stun")]
    [Tooltip("Entity skips their entire next turn. Action is wasted; cooldowns still tick.")]
    public bool isStun = false;

    // ── Silence ───────────────────────────────────────────────────────────────
    [Header("Silence")]
    [Tooltip("Entity cannot use class abilities. Auto-submit forces their default basic attack.")]
    public bool isSilence = false;

    // ── Bleed ────────────────────────────────────────────────────────────────
    [Header("Bleed")]
    [Tooltip("Damage-over-time that is paused (does not tick) on any turn the entity is healed.")]
    public bool isBleed = false;

    [Tooltip("Damage dealt per turn by the bleed. Separate from damageOverTime so both can coexist.")]
    public int bleedDamagePerTurn = 0;

    // ── Barrier ───────────────────────────────────────────────────────────────
    [Header("Barrier")]
    [Tooltip("Absorbs this much damage before any HP is lost. Depletes as it absorbs, then expires.")]
    public bool isBarrier = false;

    [Tooltip("Current remaining barrier HP. Set this equal to barrierMaxAmount when the effect is created.")]
    public int barrierCurrentAmount = 0;

    [Tooltip("Maximum barrier HP (used for display / reference).")]
    public int barrierMaxAmount = 0;

    // ── Marked ───────────────────────────────────────────────────────────────
    [Header("Marked")]
    [Tooltip("Target receives increased damage from ALL sources while Marked.")]
    public bool isMark = false;

    [Tooltip("Multiplier applied to ALL incoming damage (e.g., 1.25 = 25% more damage taken).")]
    [Range(1f, 3f)]
    public float markedDamageMultiplier = 1.25f;

    // ── Taunt ────────────────────────────────────────────────────────────────
    [Header("Taunt")]
    [Tooltip("Forces THIS entity to direct all attacks at the taunter for the duration.")]
    public bool isTaunt = false;

    [Tooltip("The entityName of the entity that must be targeted while this Taunt is active.")]
    public string tauntTargetEntityName = "";

    // ── Curse (Healing Reduction) ─────────────────────────────────────────────
    [Header("Curse")]
    [Tooltip("Reduces all healing received by this entity.")]
    public bool isCurse = false;

    [Tooltip("Fraction of healing that is negated. 0.5 = 50% of healing is lost.")]
    [Range(0f, 1f)]
    public float healingReductionPercent = 0.5f;

    // ── Exposed ───────────────────────────────────────────────────────────────
    [Header("Exposed")]
    [Tooltip("Flat defense reduction while this effect is active. Stacks with other reductions.")]
    public bool isExposed = false;

    [Tooltip("How many defense points are subtracted from the entity's total defense.")]
    public int exposedDefenseReduction = 0;

    // ── Enrage ────────────────────────────────────────────────────────────────
    [Header("Enrage")]
    [Tooltip("Entity deals more damage but is FORCED to attack the nearest/front target.")]
    public bool isEnrage = false;

    [Tooltip("Damage multiplier applied to all outgoing damage while enraged (e.g., 1.3 = +30% dmg).")]
    [Range(1f, 3f)]
    public float enrageDamageMultiplier = 1.3f;

    // ── Haste ─────────────────────────────────────────────────────────────────
    [Header("Haste")]
    [Tooltip("Entity acts twice in the same turn. Second action is a copy of the first.")]
    public bool isHaste = false;

    [Header("Primed Condition")]

    [Tooltip("Marks this StatusEffect as a Primed condition. " +
             "When the bearer receives a hit that meets the threshold, " +
             "all effects in primedEffects are applied to the bearer.")]
    public bool isPrimed = false;

    [Tooltip("How the detonation threshold is measured:\n" +
             "• FlatDamage        – hit must deal ≥ N final HP damage\n" +
             "• PercentMaxHealth  – hit must deal ≥ N% of the target's max HP\n" +
             "• PercentCurrentHealth – hit must deal ≥ N% of current HP at time of hit")]
    public PrimeThresholdType primeThresholdType = PrimeThresholdType.FlatDamage;

    [Tooltip("The numeric threshold value.\n" +
             "• FlatDamage:            e.g. 20  = must take 20+ damage\n" +
             "• PercentMaxHealth:      e.g. 15  = must take ≥ 15% of max HP\n" +
             "• PercentCurrentHealth:  e.g. 25  = must take ≥ 25% of current HP")]
    public float primeThreshold = 20f;

    [Tooltip("The effects that are applied to the target when the Primed condition detonates. " +
             "These go through full ApplyStatusEffect() logic, meaning:\n" +
             "• isNegativeEffect effects are subject to resistance rolls\n" +
             "• applicationChance rolls apply per-effect\n" +
             "Any StatusEffect can be listed here: Bleed, Enrage, Stun, Silence, Curse, etc.")]
    [System.NonSerialized]
    public List<StatusEffect> primedEffects = new List<StatusEffect>();

    [Tooltip("If true, the Primed effect itself is removed after it detonates. " +
             "If false, it can keep detonating every qualifying hit for its full duration.")]
    public bool primedConsumedOnTrigger = true;

    [Header("Condition Indicator (Visual Only)")]
    [Tooltip("Short key used to pick an icon/emoji in the health bar and panel. " +
         "Built-in keys: dot, bleed, stun, silence, barrier, mark, taunt, " +
         "curse, exposed, enrage, haste, riposte, primed, defense, statboost, lifesteal, buff. " +
         "Leave empty to fall back to auto-detection from the effect's bool flags.")]
    public string iconKey = "";

    [Tooltip("Hex colour for this condition's icon background (e.g. \"#FF4444\" for red DoT). " +
             "Leave empty to use the default colour for the iconKey.")]
    public string colorHex = "";
}

public enum AbilityCategory
{
    Damage,
    Heal,
    Buff,
    Debuff
}

public enum AbilityTargetType
{
    SingleEnemy,
    SingleAlly,
    Self,
    AllEnemies,
    AllAllies,
    FrontEnemy,
    AOEEnemies
}

public enum DamageStat
{
    None,
    Strength,
    Dexterity,
    Intelligence,
    Willpower,
    Charisma,
    Constitution
}

public enum BalanceRequirementType
{
    None,
    Above,
    Below
}

public enum PrimeThresholdType
{
    FlatDamage,
    PercentMaxHealth,
    PercentCurrentHealth,
}

[Serializable]
public class CharacterStats
{
    public int strength;
    public int constitution;
    public int dexterity;
    public int willpower;
    public int charisma;
    public int intelligence;

    public int maxHealth;
    public int currentHealth;
    public int level;
    public int experience;
    public int unallocatedStatPoints;

    public CharacterStats()
    {
        strength = 1;
        constitution = 1;
        dexterity = 1;
        willpower = 1;
        charisma = 1;
        intelligence = 1;
        level = 1;
        experience = 0;
        maxHealth = 100;
        currentHealth = 100;
        unallocatedStatPoints = 0;
    }

    public void RecalculateHealth()
    {
        int oldMax = maxHealth;
        maxHealth = 50 + (constitution * 10) + (level * 5);

        if (maxHealth > oldMax)
        {
            currentHealth += (maxHealth - oldMax);
        }

        currentHealth = Mathf.Min(currentHealth, maxHealth);
    }


}

[Serializable]
public class ClassResources
{
    // Rogue
    public int sneak;
    public int maxSneak = 6;

    // Fighter
    public Dictionary<string, int> maneuverCooldowns = new Dictionary<string, int>();
    public string currentStance = "None";

    // Mage
    public int mana;
    public int maxMana = 100;

    // Cleric
    public int wrath;
    public int maxWrath = 100;

    // Ranger
    public int balance;
    public int maxBalance = 10;
    public int minBalance = -10;

    public void ResetForClass(CharacterClass charClass)
    {
        sneak = 0;
        maneuverCooldowns.Clear();
        currentStance = "None";
        mana = maxMana;
        wrath = 0;
        balance = 0;
    }
}

[Serializable]
public class RPGItem
{
    public string itemId;
    public string itemName;
    public string description;
    public ItemType itemType;
    public ItemRarity rarity;
    public int requiredLevel;
    public int price;

    public int maxManaBonus = 0;
    public int manaRegenBonus = 0;
    public float manaCostReduction = 0f;

    // WEAPON PROPERTIES
    public bool isTwoHanded = false;
    public WeaponCategory weaponCategory = WeaponCategory.None;

    // PERCENTAGE-BASED STAT BONUSES
    [Range(0f, 1f)] public float strengthBonusPercent;
    [Range(0f, 1f)] public float constitutionBonusPercent;
    [Range(0f, 1f)] public float dexterityBonusPercent;
    [Range(0f, 1f)] public float willpowerBonusPercent;
    [Range(0f, 1f)] public float charismaBonusPercent;
    [Range(0f, 1f)] public float intelligenceBonusPercent;

    // FLAT-BASED STAT BONUSES
    public int strengthBonus;
    public int constitutionBonus;
    public int dexterityBonus;
    public int willpowerBonus;
    public int charismaBonus;
    public int intelligenceBonus;

    public List<ItemPassiveEffect> passives = new List<ItemPassiveEffect>();
    public AbilityData grantAbility;

    // FLAT COMBAT BONUSES
    public int damageBonus;
    public int defenseBonus;
    public int healAmount;

    // Class restrictions
    public List<CharacterClass> allowedClasses = new List<CharacterClass>();

    // Special properties
    public Dictionary<string, string> properties = new Dictionary<string, string>();

    // Abilities
    public List<ItemAbility> abilities = new List<ItemAbility>();

    public RPGItem()
    {
        itemId = Guid.NewGuid().ToString();
        allowedClasses = new List<CharacterClass>();
        properties = new Dictionary<string, string>();
        abilities = new List<ItemAbility>();
    }

    // Check if item has abilities
    public bool HasAbilities()
    {
        return abilities != null && abilities.Count > 0;
    }

    // Check if this weapon can be dual wielded by this class
    public bool CanDualWield(CharacterClass charClass)
    {
        if (itemType != ItemType.Weapon) return false;
        if (isTwoHanded) return false;
        if (weaponCategory != WeaponCategory.Dagger) return false;

        // Only Rogues and Rangers can dual wield daggers
        return charClass == CharacterClass.Rogue || charClass == CharacterClass.Ranger;
    }

    // Check if this item can go in offhand for this class
    public bool CanEquipInOffhand(CharacterClass charClass)
    {
        // Shields: Fighters and Clerics only
        if (itemType == ItemType.Shield)
        {
            return charClass == CharacterClass.Fighter || charClass == CharacterClass.Cleric;
        }

        // Trinkets: Mages only
        if (itemType == ItemType.Trinket)
        {
            return charClass == CharacterClass.Mage;
        }

        // Daggers: Rogues and Rangers can dual wield
        if (itemType == ItemType.Weapon && weaponCategory == WeaponCategory.Dagger)
        {
            return charClass == CharacterClass.Rogue || charClass == CharacterClass.Ranger;
        }

        return false;
    }

    // Check if this weapon MUST go in mainhand only
    public bool IsMainhandOnly()
    {
        if (itemType != ItemType.Weapon) return false;
        if (isTwoHanded) return true;

        // All weapons except daggers are mainhand-only
        return weaponCategory != WeaponCategory.Dagger;
    }

    // Helper method to get rarity multiplier
    public static float GetRarityPercentageBonus(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return 0.10f;
            case ItemRarity.Uncommon: return 0.20f;
            case ItemRarity.Rare: return 0.30f;
            case ItemRarity.Epic: return 0.40f;
            case ItemRarity.Legendary: return 0.50f;
            case ItemRarity.Unique: return 0.60f;
            default: return 0.10f;
        }
    }

    public Color GetRarityColor()
    {
        switch (rarity)
        {
            case ItemRarity.Common: return new Color(0.6f, 0.6f, 0.6f);
            case ItemRarity.Uncommon: return new Color(0.2f, 0.8f, 0.2f);
            case ItemRarity.Rare: return new Color(0.2f, 0.4f, 1f);
            case ItemRarity.Epic: return new Color(0.64f, 0.21f, 0.93f);
            case ItemRarity.Legendary: return new Color(1f, 0.5f, 0f);
            case ItemRarity.Unique: return new Color(0f, 1f, 1f);
            default: return Color.white;
        }
    }
}

[Serializable]
public class EquippedItems
{
    public RPGItem head;
    public RPGItem chest;
    public RPGItem legs;
    public RPGItem arms;
    public RPGItem mainHand;
    public RPGItem offHand;
    public RPGItem feet;

    public bool HasItem(string itemId)
    {
        return (head?.itemId == itemId) ||
               (chest?.itemId == itemId) ||
               (legs?.itemId == itemId) ||
               (arms?.itemId == itemId) ||
               (mainHand?.itemId == itemId) ||
               (offHand?.itemId == itemId) ||
               (feet?.itemId == itemId);
    }

    public RPGItem GetEquippedItem(ItemType slot)
    {
        switch (slot)
        {
            case ItemType.Helmet: return head;
            case ItemType.ChestArmor: return chest;
            case ItemType.LegArmor: return legs;
            case ItemType.ArmArmor: return arms;
            case ItemType.Boots: return feet;
            case ItemType.Weapon:
                return mainHand;
            case ItemType.Shield:
            case ItemType.Trinket:
                return offHand;
            default: return null;
        }
    }

    public void SetEquippedItem(ItemType slot, RPGItem item)
    {
        switch (slot)
        {
            case ItemType.Helmet: head = item; break;
            case ItemType.ChestArmor: chest = item; break;
            case ItemType.LegArmor: legs = item; break;
            case ItemType.ArmArmor: arms = item; break;
            case ItemType.Boots: feet = item; break;
            case ItemType.Weapon: mainHand = item; break; // FIXED - direct assignment
            case ItemType.Shield: offHand = item; break;
            case ItemType.Trinket: offHand = item; break;
            default:
                Debug.LogWarning($"[RPGData] Unknown item type: {slot}");
                break;
        }
    }

    public CharacterStats CalculateTotalStats(CharacterStats baseStats)
    {
        CharacterStats total = new CharacterStats
        {
            strength = baseStats.strength,
            constitution = baseStats.constitution,
            dexterity = baseStats.dexterity,
            willpower = baseStats.willpower,
            charisma = baseStats.charisma,
            intelligence = baseStats.intelligence,
            level = baseStats.level,
            experience = baseStats.experience,
            unallocatedStatPoints = baseStats.unallocatedStatPoints
        };

        float totalStrBonus = 0f;
        float totalConBonus = 0f;
        float totalDexBonus = 0f;
        float totalWilBonus = 0f;
        float totalChaBonus = 0f;
        float totalIntBonus = 0f;

        RPGItem[] allItems = { head, chest, legs, arms, mainHand, offHand, feet };
        foreach (var item in allItems)
        {
            if (item != null)
            {
                totalStrBonus += item.strengthBonusPercent;
                totalConBonus += item.constitutionBonusPercent;
                totalDexBonus += item.dexterityBonusPercent;
                totalWilBonus += item.willpowerBonusPercent;
                totalChaBonus += item.charismaBonusPercent;
                totalIntBonus += item.intelligenceBonusPercent;
            }
        }

        if (totalStrBonus > 0)
            total.strength += Mathf.Max(1, Mathf.RoundToInt(baseStats.strength * totalStrBonus));

        if (totalConBonus > 0)
            total.constitution += Mathf.Max(1, Mathf.RoundToInt(baseStats.constitution * totalConBonus));

        if (totalDexBonus > 0)
            total.dexterity += Mathf.Max(1, Mathf.RoundToInt(baseStats.dexterity * totalDexBonus));

        if (totalWilBonus > 0)
            total.willpower += Mathf.Max(1, Mathf.RoundToInt(baseStats.willpower * totalWilBonus));

        if (totalChaBonus > 0)
            total.charisma += Mathf.Max(1, Mathf.RoundToInt(baseStats.charisma * totalChaBonus));

        if (totalIntBonus > 0)
            total.intelligence += Mathf.Max(1, Mathf.RoundToInt(baseStats.intelligence * totalIntBonus));

        total.RecalculateHealth();

        return total;
    }

    public int GetTotalMaxManaBonus()
    {
        int total = 0;
        RPGItem[] allItems = { head, chest, legs, arms, mainHand, offHand, feet };
        foreach (var item in allItems)
        {
            if (item != null)
                total += item.maxManaBonus;
        }
        return total;
    }

    public int GetTotalManaRegenBonus()
    {
        int total = 0;
        RPGItem[] allItems = { head, chest, legs, arms, mainHand, offHand, feet };
        foreach (var item in allItems)
        {
            if (item != null)
                total += item.manaRegenBonus;
        }
        return total;
    }

    public float GetTotalManaCostReduction()
    {
        float total = 0f;
        RPGItem[] allItems = { head, chest, legs, arms, mainHand, offHand, feet };
        foreach (var item in allItems)
        {
            if (item != null)
                total += item.manaCostReduction;
        }
        return Mathf.Clamp(total, 0f, 0.75f); // Max 75% reduction
    }

    public int GetTotalDamageBonus()
    {
        int total = 0;
        RPGItem[] allItems = { head, chest, legs, arms, mainHand, offHand, feet };
        foreach (var item in allItems)
        {
            if (item != null)
                total += item.damageBonus;
        }
        return total;
    }

    public int GetTotalDefenseBonus()
    {
        int total = 0;
        RPGItem[] allItems = { head, chest, legs, arms, mainHand, offHand, feet };
        foreach (var item in allItems)
        {
            if (item != null)
                total += item.defenseBonus;
        }
        return total;
    }
}

    [Serializable]
    public class ViewerData
    {
        public string twitchUserId;
        public string username;
        public string discordUserId = "";
        public int coins;
        public CharacterClass characterClass;

        public CharacterStats baseStats;
        public ClassResources classResources;
        public EquippedItems equipped;
        public List<RPGItem> inventory;
    public List<string> equippedAbilities = new List<string>(); //Max 4
    public string equippedItemAbility = null;

        public DateTime lastSeen;
        public float totalWatchTimeMinutes;
        public bool isInCombat;
        public bool isDead;
        public DateTime deathLockoutUntil;
        public bool isBanned;

    public bool isInExpedition;
    public int expeditionActionsPerformed;

    // PvP Stats
    public int pvpWins;
    public int pvpLosses;

    public List<TradeRecord> tradeHistory;

        public ViewerData(string userId, string name)
        {
            twitchUserId = userId;
            username = name;
            coins = 0;
            characterClass = CharacterClass.None;

            baseStats = new CharacterStats();
            classResources = new ClassResources();
            equipped = new EquippedItems();
            inventory = new List<RPGItem>();

            // ===== ADD THIS LINE =====
            tradeHistory = new List<TradeRecord>();

            equippedAbilities = new List<string>();
            equippedItemAbility = "";

            lastSeen = DateTime.Now;
            totalWatchTimeMinutes = 0;
            isInCombat = false;
            isDead = false;
            deathLockoutUntil = DateTime.MinValue;
            isBanned = false;
        }

        public bool CanTakeAction()
    {
        if (isBanned) return false;
        if (isDead && DateTime.Now < deathLockoutUntil) return false;
        return true;
    }

    public void SetClass(CharacterClass newClass)
    {
        characterClass = newClass;
        classResources.ResetForClass(newClass);

        switch (newClass)
        {
            case CharacterClass.Rogue:
                baseStats.dexterity += 2;
                baseStats.intelligence += 1;
                break;
            case CharacterClass.Fighter:
                baseStats.strength += 2;
                baseStats.constitution += 2;
                break;
            case CharacterClass.Mage:
                baseStats.intelligence += 8;
                baseStats.willpower += 2;
                break;
            case CharacterClass.Cleric:
                baseStats.willpower += 5;
                baseStats.charisma += 3;
                baseStats.constitution += 2;
                break;
            case CharacterClass.Ranger:
                baseStats.dexterity += 4;
                baseStats.constitution += 3;
                baseStats.willpower += 3;
                break;
        }

        baseStats.RecalculateHealth();
    }

    public CharacterStats GetTotalStats()
    {
        return equipped.CalculateTotalStats(baseStats);
    }

    public bool CanAfford(int cost)
    {
        return coins >= cost;
    }

    public bool AddItem(RPGItem item)
    {
        if (inventory.Count >= 50)
        {
            return false;
        }
        inventory.Add(item);
        return true;
    }

    public bool RemoveItem(string itemId)
    {
        RPGItem item = inventory.Find(i => i.itemId == itemId);
        if (item != null)
        {
            inventory.Remove(item);
            return true;
        }
        return false;
    }

    public RPGItem FindItemInInventory(string itemId)
    {
        return inventory.Find(i => i.itemId == itemId);
    }
}

[Serializable]
public class GameDatabase
{
    public List<ViewerData> allViewers = new List<ViewerData>();
    public List<RPGItem> itemDatabase = new List<RPGItem>();
    public DateTime lastSaveTime;

    public GameDatabase()
    {
        allViewers = new List<ViewerData>();
        itemDatabase = new List<RPGItem>();
        lastSaveTime = DateTime.Now;
    }

    public ViewerData GetOrCreateViewer(string userId, string username)
    {
        ViewerData viewer = allViewers.Find(v => v.twitchUserId == userId);
        if (viewer == null)
        {
            viewer = new ViewerData(userId, username);
            allViewers.Add(viewer);
        }
        else
        {
            viewer.username = username;
            viewer.lastSeen = DateTime.Now;
        }
        return viewer;
    }

    public RPGItem GetItemById(string itemId)
    {
        return itemDatabase.Find(i => i.itemId == itemId);
    }
}
