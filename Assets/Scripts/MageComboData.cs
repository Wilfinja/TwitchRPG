using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines what happens when a specific 4-charge combo is triggered
/// Create via: Right-click → Create → RPG → Mage Combo
/// </summary>
[CreateAssetMenu(fileName = "New Mage Combo", menuName = "RPG/Mage Combo")]
public class MageComboData : ScriptableObject
{
    [Header("Combo Identity")]
    [Tooltip("Display name for this combo (e.g., 'Inferno Burst')")]
    public string comboName;

    [Tooltip("Description of what this combo does")]
    [TextArea(3, 5)]
    public string description;

    [Header("Combo Requirements")]
    [Tooltip("The exact combination of charges needed (must be 4)")]
    public List<ElementType> requiredCharges = new List<ElementType>(4);

    [Header("Effect Type")]
    public ComboEffectType effectType;

    [Header("Damage/Healing")]
    [Tooltip("Base damage/healing amount")]
    public int basePower;

    [Tooltip("Which stat scales this combo (if any)")]
    public DamageStat scalingStat = DamageStat.Intelligence;

    [Tooltip("Stat multiplier (e.g., 1.5 = +150% of INT)")]
    public float statMultiplier = 1f;

    [Header("Targeting")]
    [Tooltip("Does this affect all enemies, all allies, or specific targets?")]
    public ComboTargetType targetType;

    [Tooltip("Number of targets affected (for multi-target combos)")]
    public int targetCount = 1;

    [Header("Status Effects")]
    [Tooltip("Status effects applied by this combo")]
    public List<StatusEffect> appliedEffects = new List<StatusEffect>();

    [Header("Special Effects")]
    [Tooltip("Grants temporary defense boost to allies?")]
    public bool grantsDefenseBoost;

    [Tooltip("Amount of defense granted")]
    public int defenseBoostAmount;

    [Tooltip("Duration of defense boost (turns)")]
    public int defenseBoostDuration = 1;

    [Header("Visual")]
    [Tooltip("Particle effect prefab to spawn")]
    public GameObject particleEffect;

    [Tooltip("Color of the combo effect (used for UI and particles)")]
    public Color comboColor = Color.white;

    /// <summary>
    /// Get the combo signature for lookup
    /// This is the alphabetically sorted combination of elements
    /// </summary>
    public string GetComboSignature()
    {
        if (requiredCharges.Count != 4)
        {
            Debug.LogWarning($"[MageCombo] {comboName} doesn't have exactly 4 charges!");
            return "";
        }

        string[] elements = new string[4];
        for (int i = 0; i < 4; i++)
        {
            elements[i] = requiredCharges[i].ToString();
        }

        System.Array.Sort(elements);
        return string.Join("+", elements);
    }
}

/// <summary>
/// Type of effect this combo produces
/// </summary>
public enum ComboEffectType
{
    Damage,         // Deals damage
    Healing,        // Heals targets
    Buff,           // Applies buffs
    Debuff,         // Applies debuffs
    Mixed           // Multiple effects
}

/// <summary>
/// Who this combo targets
/// </summary>
public enum ComboTargetType
{
    AllEnemies,     // Hits all enemies
    AllAllies,      // Affects all allies (including caster)
    SingleEnemy,    // Hits strongest/weakest enemy
    FrontEnemies,   // Hits front X enemies
    Self            // Only affects the mage
}
