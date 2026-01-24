using UnityEngine;
using System.Collections.Generic;

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

    [Header("Damage/Healing")]
    public DamageStat scalingStat; // Which stat scales the ability
    public float statMultiplier = 1f; // Damage = stat * multiplier
    public int baseDamage; // Flat damage added
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

        CharacterClass charClass = entity.GetCharacterClass(); // CORRECT - use method
        List<AbilityData> classAbilities = GetAbilitiesForClass(charClass);

        available.AddRange(classAbilities);
        return available;
    }
}
