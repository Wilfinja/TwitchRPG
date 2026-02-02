using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Enhanced AbilityData with dual-stat scaling support
/// BACKWARDS COMPATIBLE - old abilities will work without changes
/// </summary>
[CreateAssetMenu(fileName = "New Ability", menuName = "RPG/Ability")]
public class AbilityData : ScriptableObject
{
    [Header("Basic Info")]
    public string abilityName;
    public string commandName; // What players type: "quickcut", "strike", etc.
    [TextArea(3, 5)]
    public string description;
    public CharacterClass requiredClass;
    public int levelRequired = 1;

    [Header("Ability Type")]
    public AbilityCategory category; // Buff, Heal, Damage
    public AbilityTargetType targetType;

    [Header("Primary Damage/Healing Scaling")]
    public DamageStat scalingStat; // Primary stat (DEX, STR, INT, etc.)
    public float statMultiplier = 1f; // Primary stat multiplier

    // ═══════════════════════════════════════════════════════════
    // ✅ NEW: SECONDARY STAT SCALING
    // ═══════════════════════════════════════════════════════════
    [Header("Secondary Scaling (Optional)")]
    [Tooltip("Enable dual-stat scaling (e.g., Flame Dagger = DEX + INT)")]
    public bool useSecondaryScaling = false;

    [Tooltip("Secondary stat to scale with")]
    public DamageStat secondaryScalingStat = DamageStat.None;

    [Tooltip("Secondary stat multiplier")]
    public float secondaryStatMultiplier = 0f;

    // ═══════════════════════════════════════════════════════════

    // ═══════════════════════════════════════════════════════════
    // ✅ NEW: SNEAK-BASED DAMAGE SCALING (ROGUE)
    // ═══════════════════════════════════════════════════════════
    [Header("Sneak Damage Scaling (Rogue Only)")]
    [Tooltip("Damage scales based on CURRENT sneak points")]
    public bool scalesWithSneak = false;

    [Tooltip("Damage multiplier per sneak point (e.g., 0.5 = +50% per sneak)")]
    public float sneakDamageMultiplier = 0f;

    [Tooltip("Consume ALL sneak points after using this ability")]
    public bool consumesAllSneak = false;

    [Tooltip("Consume specific amount of sneak (0 = don't consume based on count)")]
    public int consumeSneakAmount = 0;
    // ═══════════════════════════════════════════════════════════

    [Header("Base Damage/Healing")]
    public int baseDamage; // Flat damage/healing added
    public bool canCrit;

    [Header("Resource Cost")]
    public int sneakCost; // Rogue
    public int sneakGain; // Rogue
    public bool requiresStance; // Fighter
    public FighterStance requiredStance; // Fighter
    public int manaCost; // Mage
    public int wrathCost; // Cleric
    public int wrathGain; // Cleric (from offensive abilities)
    public int balanceCost; // Ranger
    public int balanceGain; // Ranger
    public int balanceRequirement; // Ranger (must be above/below this)
    public BalanceRequirementType balanceRequirementType;

    [Header("Targeting")]
    public bool canTargetAllies;
    public bool canTargetEnemies = true;
    public int maxTargetPosition = 1; // Can hit positions 1-X
    public int minTargetPosition = 1;
    public bool isAOE;
    public int aoETargets = 1; // How many targets for AOE

    [Header("Cooldown")]
    public int cooldown; // Turns

    [Header("Special Effects")]
    public List<StatusEffect> appliesEffects = new List<StatusEffect>();
    public bool shiftPosition; // Move target forward/back
    public int positionShift;

    [Header("Animation")]
    public string animationTrigger = "Attack";
    public GameObject particleEffect;

    // ═══════════════════════════════════════════════════════════
    // ✅ HELPER METHODS FOR DUAL-STAT SCALING
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns true if this ability uses dual-stat scaling
    /// </summary>
    public bool HasSecondaryScaling()
    {
        return useSecondaryScaling &&
               secondaryScalingStat != DamageStat.None &&
               secondaryStatMultiplier > 0f;
    }

    /// <summary>
    /// Returns true if this ability scales damage with sneak points
    /// </summary>
    public bool HasSneakScaling()
    {
        return scalesWithSneak && sneakDamageMultiplier > 0f;
    }

    /// <summary>
    /// Get all scaling stats for this ability (for UI display)
    /// </summary>
    public string GetScalingDescription()
    {
        if (!HasSecondaryScaling() && !HasSneakScaling())
        {
            // Single stat scaling (backwards compatible)
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

    /// <summary>
    /// Validate ability data on save (Unity Editor only)
    /// </summary>
    private void OnValidate()
    {
        // Prevent invalid secondary scaling configurations
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
    }
}

/// <summary>
/// Manages all abilities in the game
/// </summary>
public class AbilityDatabase : MonoBehaviour
{
    public static AbilityDatabase Instance;

    [Header("Ability Lists")]
    public List<AbilityData> rogueAbilities = new List<AbilityData>();
    public List<AbilityData> fighterAbilities = new List<AbilityData>();
    public List<AbilityData> mageAbilities = new List<AbilityData>();
    public List<AbilityData> clericAbilities = new List<AbilityData>();
    public List<AbilityData> rangerAbilities = new List<AbilityData>();
    public List<AbilityData> enemyAbilities = new List<AbilityData>();

    private Dictionary<string, AbilityData> abilityLookup = new Dictionary<string, AbilityData>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        BuildAbilityLookup();
    }

    void BuildAbilityLookup()
    {
        abilityLookup.Clear();

        AddAbilitiesToLookup(rogueAbilities);
        AddAbilitiesToLookup(fighterAbilities);
        AddAbilitiesToLookup(mageAbilities);
        AddAbilitiesToLookup(clericAbilities);
        AddAbilitiesToLookup(rangerAbilities);
        AddAbilitiesToLookup(enemyAbilities);
    }

    void AddAbilitiesToLookup(List<AbilityData> abilities)
    {
        foreach (var ability in abilities)
        {
            if (!abilityLookup.ContainsKey(ability.commandName.ToLower()))
            {
                abilityLookup.Add(ability.commandName.ToLower(), ability);
            }
        }
    }

    public AbilityData GetAbility(string commandName)
    {
        if (abilityLookup.TryGetValue(commandName.ToLower(), out AbilityData ability))
            return ability;
        return null;
    }

    public List<AbilityData> GetAbilitiesForClass(CharacterClass charClass)
    {
        switch (charClass)
        {
            case CharacterClass.Rogue: return rogueAbilities;
            case CharacterClass.Fighter: return fighterAbilities;
            case CharacterClass.Mage: return mageAbilities;
            case CharacterClass.Cleric: return clericAbilities;
            case CharacterClass.Ranger: return rangerAbilities;
            default: return new List<AbilityData>();
        }
    }

    public List<AbilityData> GetAvailableAbilities(CombatEntity entity)
    {
        List<AbilityData> available = new List<AbilityData>();

        CharacterClass charClass = entity.GetCharacterClass();
        List<AbilityData> classAbilities = GetAbilitiesForClass(charClass);

        available.AddRange(classAbilities);
        return available;
    }
}
