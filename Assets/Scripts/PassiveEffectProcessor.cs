using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton that fires passive effect hooks at the right moments in combat.
/// Call the appropriate static method from CombatCalculations, CombatEntity, etc.
/// </summary>
public static class PassiveEffectProcessor
{
    // ── Helper: collect all passive effects from a CombatEntity's equipped items ──

    public static List<ItemPassiveEffect> GetPassives(CombatEntity entity)
    {
        var passives = new List<ItemPassiveEffect>();
        if (entity == null || !entity.isPlayer || entity.viewerData == null) return passives;

        var equipped = entity.viewerData.equipped;
        RPGItem[] slots = {
            equipped.head, equipped.chest, equipped.arms, equipped.legs,
            equipped.mainHand, equipped.offHand, equipped.feet
        };

        foreach (var item in slots)
        {
            // RPGItem uses the field name "passives" — match it exactly
            if (item == null || item.passives == null) continue;
            passives.AddRange(item.passives);
        }

        return passives;
    }

    // ── Trigger methods called from the combat pipeline ─────────────────────────

    public static void OnCombatStart(CombatEntity entity)
        => Fire(entity, PassiveTriggerType.OnCombatStart, null, 0);

    public static void OnTurnStart(CombatEntity entity)
        => Fire(entity, PassiveTriggerType.OnTurnStart, null, 0);

    /// <summary>Called after damage is fully resolved and applied.</summary>
    public static void OnDealDamage(CombatEntity attacker, CombatEntity target, int damageFinal)
        => Fire(attacker, PassiveTriggerType.OnDealDamage, target, damageFinal);

    /// <summary>Called on the entity that received damage.</summary>
    public static void OnTakeDamage(CombatEntity victim, CombatEntity attacker, int damageFinal)
        => Fire(victim, PassiveTriggerType.OnTakeDamage, attacker, damageFinal);

    /// <summary>Called on the attacker when they kill a target.</summary>
    public static void OnKill(CombatEntity killer, CombatEntity victim)
        => Fire(killer, PassiveTriggerType.OnKill, victim, 0);

    /// <summary>
    /// Called after a heal resolves. Fires on both the healed entity (for Overcharge)
    /// and the healer (for Censer), passing the other as the target argument.
    /// </summary>
    public static void OnHeal(CombatEntity healed, CombatEntity healer, int rawHealAmount)
    {
        // Overcharge fires on the healed entity — checks if they have the item
        Fire(healed, PassiveTriggerType.OnHeal, healer, rawHealAmount);

        // Censer fires on the healer — healer's item grants defense to healed entity
        // We pass healed as "target" so Censer knows who to buff
        if (healer != null && healer != healed)
            Fire(healer, PassiveTriggerType.OnHeal, healed, rawHealAmount);
    }

    /// <summary>Called when a status effect is applied to target.</summary>
    public static void OnStatusEffectApplied(CombatEntity target, CombatEntity applier, StatusEffect effect)
        => Fire(target, PassiveTriggerType.OnStatusEffectApplied, applier, 0);

    public static void OnWaveStart(CombatEntity entity)
        => Fire(entity, PassiveTriggerType.OnWaveStart, null, 0);

    public static void OnWaveEnd(CombatEntity entity)
        => Fire(entity, PassiveTriggerType.OnWaveEnd, null, 0);

    public static void OnHealthThreshold(CombatEntity entity, int healthPercent)
    => Fire(entity, PassiveTriggerType.OnHealthThreshold, null, healthPercent);

    /// <summary>
    /// Called when an entity would die. Returns true if a passive (Phoenix)
    /// prevented the death. Must be called BEFORE Die() commits.
    /// </summary>
    public static bool OnDeath(CombatEntity entity)
    {
        var passives = GetPassives(entity);
        bool prevented = false;

        foreach (var passive in passives)
        {
            if (passive == null || passive.trigger != PassiveTriggerType.OnDeath) continue;

            passive.Proc(entity, null, 0);

            // Phoenix sets currentHealth > 0 and isDead = false if it saved them
            if (!entity.isDead && entity.currentHealth > 0)
            {
                prevented = true;
            }
        }

        return prevented;
    }

    public static void OnCombatEnd(CombatEntity entity)
        => Fire(entity, PassiveTriggerType.OnCombatEnd, null, 0);

    // ── Internal fire helper ──────────────────────────────────────────────────

    private static void Fire(CombatEntity owner, PassiveTriggerType trigger,
                              CombatEntity target, int value)
    {
        if (owner == null) return;

        var passives = GetPassives(owner);
        foreach (var passive in passives)
        {
            if (passive == null || passive.trigger != trigger) continue;

            passive.Proc(owner, target, value);
        }
    }

    // ── Calculation helpers (always-on stat queries) ──────────────────────────

    /// <summary>
    /// Returns the highest Pinpoint defense-ignore fraction across all equipped items.
    /// 0 = no penetration, 1 = full penetration.
    /// </summary>
    public static float GetPinpointPenetration(CombatEntity entity)
    {
        float highest = 0f;
        foreach (var passive in GetPassives(entity))
        {
            if (passive is PassivePinpoint p)
                highest = Mathf.Max(highest, p.defenseIgnorePercent);
        }
        return highest;
    }

    /// <summary>
    /// Returns the highest Executioner bonus damage multiplier for the current
    /// target HP situation. Returns 1.0 if condition not met.
    /// </summary>
    public static float GetExecutionerMultiplier(CombatEntity attacker, CombatEntity target)
    {
        float highest = 1f;
        foreach (var passive in GetPassives(attacker))
        {
            if (passive is PassiveExecutioner e)
            {
                float hpPercent = (float)target.currentHealth / target.maxHealth;
                if (hpPercent <= e.healthThresholdPercent)
                {
                    float mult = 1f + e.bonusDamagePercent;
                    highest = Mathf.Max(highest, mult);
                }
            }
        }
        return highest;
    }

    /// <summary>
    /// Returns current Berserker stat bonuses for the entity.
    /// Returns a dictionary of BoostableStat → bonus amount (flat).
    /// </summary>
    public static Dictionary<BoostableStat, int> GetBerserkerBonuses(CombatEntity entity)
    {
        var result = new Dictionary<BoostableStat, int>();
        if (entity.maxHealth <= 0) return result;

        float hpMissingPercent = 1f - ((float)entity.currentHealth / entity.maxHealth);

        foreach (var passive in GetPassives(entity))
        {
            if (passive is PassiveBerserker b)
            {
                foreach (var pair in b.statPairs)
                {
                    // bonus = baseStat * (hpMissing% * bonusPercentPerMissingPercent / 100)
                    // e.g. 0.5 per missing% at 40% missing = 20% bonus of base stat
                    int baseStat = GetBaseStatValue(entity, pair.stat);
                    int bonus = Mathf.FloorToInt(baseStat * hpMissingPercent * pair.bonusPercentPerMissingPercent / 100f);
                    if (bonus > 0)
                    {
                        if (!result.ContainsKey(pair.stat)) result[pair.stat] = 0;
                        result[pair.stat] += bonus;
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the always-on lifesteal percent from items (highest wins).
    /// </summary>
    public static float GetItemLifestealPercent(CombatEntity entity)
    {
        float highest = 0f;
        foreach (var passive in GetPassives(entity))
        {
            if (passive is PassiveLifesteal l)
                highest = Mathf.Max(highest, l.lifestealPercent);
        }
        return highest;
    }

    // ── Stat lookup helper ────────────────────────────────────────────────────

    public static int GetBaseStatValue(CombatEntity entity, BoostableStat stat)
    {
        switch (stat)
        {
            case BoostableStat.Strength: return entity.strength;
            case BoostableStat.Constitution: return entity.constitution;
            case BoostableStat.Dexterity: return entity.dexterity;
            case BoostableStat.Intelligence: return entity.intelligence;
            case BoostableStat.Willpower: return entity.willpower;
            case BoostableStat.Charisma: return entity.charisma;
            default: return 0;
        }
    }
}
