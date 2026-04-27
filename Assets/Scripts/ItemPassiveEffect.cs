using UnityEngine;
using System.Collections.Generic;

public enum PassiveTriggerType
{
    OnCombatStart,
    OnTurnStart,
    OnDealDamage,
    OnTakeDamage,
    OnKill,
    OnHealthThreshold,
    OnHeal,
    OnStatusEffectApplied,
    OnWaveStart,
    OnWaveEnd,
    OnDeath,            // For Phoenix
    OnCombatEnd,
    Passive             // Always-on effects evaluated during calculation
}

/// <summary>
/// Base class for all item passive effects.
/// Subclass this and create a ScriptableObject for each passive type.
/// </summary>
public abstract class ItemPassiveEffect : ScriptableObject
{
    [Header("Identity")]
    public string passiveName;
    [TextArea(2, 3)]
    public string description;
    public PassiveTriggerType trigger;

    /// <summary>
    /// Called by PassiveEffectProcessor when the matching trigger fires.
    /// owner = the entity that has this item equipped
    /// target = the relevant other entity (attacker, heal target, etc.) — may be null
    /// value = context-specific number (damage dealt, healing done, etc.)
    /// </summary>
    public abstract void Proc(CombatEntity owner, CombatEntity target, int value = 0);

    /// <summary>
    /// Override to return true if this passive modifies damage calculation inline.
    /// Used by CombatCalculations to query always-on stat modifiers.
    /// </summary>
    public virtual bool IsCalculationPassive() => false;
}
