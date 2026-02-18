using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Enhanced AbilityData with dual-stat scaling, defense boosts, and stat boosts
/// Defense and stat boosts can now SCALE with caster stats!
/// </summary>
[CreateAssetMenu(fileName = "New Ability", menuName = "RPG/Ability")]
public class AbilityData : ScriptableObject
{
    [Header("Basic Info")]
    public string abilityName;
    public string commandName;
    [TextArea(3, 5)]
    public string description;
    public CharacterClass requiredClass;
    public int levelRequired = 1;

    [Header("Ability Type")]
    public AbilityCategory category;
    public AbilityTargetType targetType;

    [Header("Primary Damage/Healing Scaling")]
    public DamageStat scalingStat;
    public float statMultiplier = 1f;

    [Header("Secondary Scaling (Optional)")]
    [Tooltip("Enable dual-stat scaling (e.g., Flame Dagger = DEX + INT)")]
    public bool useSecondaryScaling = false;

    [Tooltip("Secondary stat to scale with")]
    public DamageStat secondaryScalingStat = DamageStat.None;

    [Tooltip("Secondary stat multiplier")]
    public float secondaryStatMultiplier = 0f;

    [Header("Sneak Damage Scaling (Rogue Only)")]
    [Tooltip("Damage scales based on CURRENT sneak points")]
    public bool scalesWithSneak = false;

    [Tooltip("Damage multiplier per sneak point (e.g., 0.5 = +50% per sneak)")]
    public float sneakDamageMultiplier = 0f;

    [Tooltip("Consume ALL sneak points after using this ability")]
    public bool consumesAllSneak = false;

    [Tooltip("Consume specific amount of sneak (0 = don't consume based on count)")]
    public int consumeSneakAmount = 0;

    [Header("Base Damage/Healing")]
    public int baseDamage;
    public bool canCrit;

    [Header("Multi-Hit (Multiple Strikes Per Use)")]
    [Tooltip("Enable multiple hits in one attack")]
    public bool isMultiHit = false;

    [Tooltip("Base number of hits (minimum)")]
    public int baseHitCount = 1;

    [Tooltip("Additional hits based on class resource")]
    public MultiHitType multiHitType = MultiHitType.None;

    [Tooltip("Resource-to-hit conversion (e.g., 1 sneak = 1 hit)")]
    public int resourcePerHit = 1;

    [Tooltip("Maximum total hits allowed")]
    public int maxHitCount = 5;

    [Tooltip("Consume the resource after hitting?")]
    public bool consumeResourceAfterHits = false;

    [Header("Mage Elemental Charge")]
    [Tooltip("What element charge does this ability grant? (Mage only)")]
    public ElementType elementType = ElementType.None;

    // ═══════════════════════════════════════════════════════════
    // ✅ UPDATED: Defense Boost with Stat Scaling
    // ═══════════════════════════════════════════════════════════
    [Header("Defense Boost")]
    [Tooltip("Grants temporary defense boost")]
    public bool grantsDefenseBoost;

    [Tooltip("Base defense amount (before scaling)")]
    public int baseDefenseBoost;

    [Tooltip("Which stat to scale defense boost with (None = no scaling, just use base)")]
    public DamageStat defenseScalingStat = DamageStat.None;

    [Tooltip("Defense scaling multiplier (e.g., 1.5 = defense = base + 1.5x CON)")]
    public float defenseScalingMultiplier = 0f;

    [Tooltip("Defense consumed after 1 hit (true) or lasts 1 turn (false)")]
    public bool defenseConsumedOnHit;

    // ═══════════════════════════════════════════════════════════
    // ✅ UPDATED: Stat Boost with Stat Scaling
    // ═══════════════════════════════════════════════════════════
    [Header("Stat Boost")]
    [Tooltip("Grants temporary stat boost")]
    public bool grantsStatBoost;

    [Tooltip("Which stat to boost")]
    public BoostableStat statToBoost;

    [Tooltip("Base stat boost amount (before scaling)")]
    public int baseStatBoost;

    [Tooltip("Which stat to scale the boost with (None = no scaling, just use base)")]
    public DamageStat statBoostScalingStat = DamageStat.None;

    [Tooltip("Stat boost scaling multiplier (e.g., 0.5 = boost = base + 0.5x CHA)")]
    public float statBoostScalingMultiplier = 0f;

    [Tooltip("How many turns the stat boost lasts")]
    public int statBoostDuration = 1;

    [Header("Resource Cost")]
    public int sneakCost;
    public int sneakGain;
    public bool requiresStance;
    public FighterStance requiredStance;
    public int manaCost;
    public int wrathCost;
    public int wrathGain;
    public int balanceCost;
    public int balanceGain;
    public int balanceRequirement;
    public BalanceRequirementType balanceRequirementType;

    [Header("Targeting")]
    public bool canTargetAllies;
    public bool canTargetEnemies = true;
    public int maxTargetPosition = 1;
    public int minTargetPosition = 1;
    public bool isAOE;
    public int aoETargets = 1;

    [Header("Cooldown")]
    public int cooldown;

    [Header("Special Effects")]
    public List<StatusEffect> appliesEffects = new List<StatusEffect>();
    public bool shiftPosition;
    public int positionShift;

    [Header("Animation")]
    public string animationTrigger = "Attack";
    public GameObject particleEffect;

    // ═══════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════

    public enum MultiHitType
    {
        None,              // No resource scaling
        PerSneakPoint,     // Rogue: 1 hit per sneak point
        PerBalancePoint,   // Ranger: 1 hit per +2 balance (until neutral)
        IfAggressive,      // Fighter: +1 hit if in Aggressive stance
        PerWrathTier       // Cleric: Hits based on wrath level
    }

    public bool HasSecondaryScaling()
    {
        return useSecondaryScaling &&
               secondaryScalingStat != DamageStat.None &&
               secondaryStatMultiplier > 0f;
    }

    public bool HasSneakScaling()
    {
        return scalesWithSneak && sneakDamageMultiplier > 0f;
    }

    /// <summary>
    /// Check if defense boost scales with a stat
    /// </summary>
    public bool DefenseBoostScales()
    {
        return grantsDefenseBoost &&
               defenseScalingStat != DamageStat.None &&
               defenseScalingMultiplier > 0f;
    }

    /// <summary>
    /// Check if stat boost scales with a stat
    /// </summary>
    public bool StatBoostScales()
    {
        return grantsStatBoost &&
               statBoostScalingStat != DamageStat.None &&
               statBoostScalingMultiplier > 0f;
    }

    public string GetScalingDescription()
    {
        if (!HasSecondaryScaling() && !HasSneakScaling())
        {
            return $"{statMultiplier:F2}x {scalingStat}";
        }

        List<string> parts = new List<string>();
        parts.Add($"{statMultiplier:F2}x {scalingStat}");

        if (HasSecondaryScaling())
        {
            parts.Add($"{secondaryStatMultiplier:F2}x {secondaryScalingStat}");
        }

        if (HasSneakScaling())
        {
            float percentPerSneak = sneakDamageMultiplier * 100f;
            parts.Add($"+{percentPerSneak:F0}% per Sneak");
        }

        return string.Join(" + ", parts);
    }

    private void OnValidate()
    {
        // Validate secondary scaling
        if (useSecondaryScaling)
        {
            if (secondaryScalingStat == DamageStat.None)
            {
                Debug.LogWarning($"[{abilityName}] Secondary scaling enabled but no stat selected!");
            }

            if (secondaryScalingStat == scalingStat)
            {
                Debug.LogWarning($"[{abilityName}] Secondary stat should be different from primary stat!");
            }

            if (secondaryStatMultiplier <= 0f)
            {
                Debug.LogWarning($"[{abilityName}] Secondary multiplier should be greater than 0!");
            }
        }

        // Validate sneak scaling
        if (scalesWithSneak)
        {
            if (requiredClass != CharacterClass.Rogue)
            {
                Debug.LogWarning($"[{abilityName}] Sneak scaling should only be used for Rogue abilities!");
            }

            if (sneakDamageMultiplier <= 0f)
            {
                Debug.LogWarning($"[{abilityName}] Sneak damage multiplier should be greater than 0!");
            }

            if (consumesAllSneak && consumeSneakAmount > 0)
            {
                Debug.LogWarning($"[{abilityName}] Can't use both consumesAllSneak AND consumeSneakAmount!");
            }
        }

        // Validate defense boost
        if (grantsDefenseBoost)
        {
            if (baseDefenseBoost <= 0 && !DefenseBoostScales())
            {
                Debug.LogWarning($"[{abilityName}] Defense boost enabled but base amount is 0 and no scaling set!");
            }

            if (defenseScalingStat != DamageStat.None && defenseScalingMultiplier <= 0f)
            {
                Debug.LogWarning($"[{abilityName}] Defense scaling stat selected but multiplier is 0!");
            }
        }

        // Validate stat boost
        if (grantsStatBoost)
        {
            if (statToBoost == BoostableStat.None)
            {
                Debug.LogWarning($"[{abilityName}] Stat boost enabled but no stat selected!");
            }

            if (baseStatBoost <= 0 && !StatBoostScales())
            {
                Debug.LogWarning($"[{abilityName}] Stat boost enabled but base amount is 0 and no scaling set!");
            }

            if (statBoostScalingStat != DamageStat.None && statBoostScalingMultiplier <= 0f)
            {
                Debug.LogWarning($"[{abilityName}] Stat boost scaling stat selected but multiplier is 0!");
            }
        }
    }
}