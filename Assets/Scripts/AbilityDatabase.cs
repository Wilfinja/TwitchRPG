using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages all abilities in the game
/// Singleton that provides lookup for abilities by command name
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

        Debug.Log($"[AbilityDatabase] Built lookup with {abilityLookup.Count} abilities");
    }

    void AddAbilitiesToLookup(List<AbilityData> abilities)
    {
        foreach (var ability in abilities)
        {
            if (ability == null) continue;

            if (!abilityLookup.ContainsKey(ability.commandName.ToLower()))
            {
                abilityLookup.Add(ability.commandName.ToLower(), ability);
            }
            else
            {
                Debug.LogWarning($"[AbilityDatabase] Duplicate command name: {ability.commandName}");
            }
        }
    }

    /// <summary>
    /// Get an ability by its command name (what players type)
    /// </summary>
    public AbilityData GetAbility(string commandName)
    {
        if (string.IsNullOrEmpty(commandName)) return null;

        if (abilityLookup.TryGetValue(commandName.ToLower(), out AbilityData ability))
            return ability;

        return null;
    }

    /// <summary>
    /// Get all abilities for a specific class
    /// </summary>
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

    /// <summary>
    /// Get all available abilities for a combat entity
    /// (Used for AI or UI display)
    /// </summary>
    public List<AbilityData> GetAvailableAbilities(CombatEntity entity)
    {
        List<AbilityData> available = new List<AbilityData>();

        CharacterClass charClass = entity.GetCharacterClass();
        List<AbilityData> classAbilities = GetAbilitiesForClass(charClass);

        available.AddRange(classAbilities);
        return available;
    }
}
