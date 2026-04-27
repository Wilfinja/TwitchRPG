using System.Collections.Generic;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
// PASSIVE: LIFESTEAL (Always-on)
// ═══════════════════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "Passive_Lifesteal", menuName = "RPG/Passives/Lifesteal")]
public class PassiveLifesteal : ItemPassiveEffect
{
    [Header("Lifesteal Settings")]
    [Range(0f, 1f)]
    [Tooltip("Fraction of damage dealt restored as HP. 0.1 = 10%.")]
    public float lifestealPercent = 0.1f;

    public override void Proc(CombatEntity owner, CombatEntity target, int value)
    {
        // Handled directly in CombatCalculations via PassiveEffectProcessor.GetItemLifestealPercent()
        // This Proc is intentionally empty — the value is queried inline during damage calc.
    }

    public override bool IsCalculationPassive() => true;
}

// ═══════════════════════════════════════════════════════════════════════════════
// PASSIVE: PINPOINT (Always-on — highest wins)
// ═══════════════════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "Passive_Pinpoint", menuName = "RPG/Passives/Pinpoint")]
public class PassivePinpoint : ItemPassiveEffect
{
    [Header("Pinpoint Settings")]
    [Range(0f, 1f)]
    [Tooltip("Fraction of enemy defense ignored. 0.3 = ignore 30% of their defense.")]
    public float defenseIgnorePercent = 0.3f;

    public override void Proc(CombatEntity owner, CombatEntity target, int value) { }
    public override bool IsCalculationPassive() => true;
}

// ═══════════════════════════════════════════════════════════════════════════════
// PASSIVE: FOCUS FIRE / IMBUED (OnDealDamage)
// ═══════════════════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "Passive_Imbued", menuName = "RPG/Passives/Imbued")]
public class PassiveImbued : ItemPassiveEffect
{
    [Header("Imbued Settings")]
    [Range(0f, 1f)]
    [Tooltip("Chance per hit to apply the status effect.")]
    public float procChance = 0.2f;

    [Tooltip("The status effect to apply on proc. Respects target's status resistance.")]
    public StatusEffect effectToApply;

    public PassiveImbued()
    {
        trigger = PassiveTriggerType.OnDealDamage;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value)
    {
        if (target == null || target.isDead) return;
        if (effectToApply == null) return;
        if (Random.value > procChance) return;

        // Deep copy so the template is never mutated
        StatusEffect copy = DeepCopyEffect(effectToApply);
        target.ApplyStatusEffect(copy);

        CombatLog.Instance?.AddEntry(
            $"✨ {owner.entityName}'s item procced {copy.effectName} on {target.entityName}!"
        );
    }

    private StatusEffect DeepCopyEffect(StatusEffect src)
    {
        return new StatusEffect
        {
            effectName = src.effectName,
            duration = src.duration,
            applicationChance = src.applicationChance,
            isNegativeEffect = src.isNegativeEffect,
            statusResistanceBonus = src.statusResistanceBonus,
            damageMultiplier = src.damageMultiplier,
            defenseMultiplier = src.defenseMultiplier,
            damageOverTime = src.damageOverTime,
            temporaryDefenseBonus = src.temporaryDefenseBonus,
            consumedOnHit = src.consumedOnHit,
            statBoostType = src.statBoostType,
            statBoostAmount = src.statBoostAmount,
            isStun = src.isStun,
            isSilence = src.isSilence,
            isBleed = src.isBleed,
            bleedDamagePerTurn = src.bleedDamagePerTurn,
            isBarrier = src.isBarrier,
            barrierCurrentAmount = src.barrierMaxAmount,
            barrierMaxAmount = src.barrierMaxAmount,
            isMark = src.isMark,
            markedDamageMultiplier = src.markedDamageMultiplier,
            isTaunt = src.isTaunt,
            tauntTargetEntityName = src.tauntTargetEntityName,
            isCurse = src.isCurse,
            healingReductionPercent = src.healingReductionPercent,
            isExposed = src.isExposed,
            exposedDefenseReduction = src.exposedDefenseReduction,
            isEnrage = src.isEnrage,
            enrageDamageMultiplier = src.enrageDamageMultiplier,
            isHaste = src.isHaste,
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// PASSIVE: OVERCHARGE (OnHeal — self only)
// Excess healing on self converts to Mana (Mage) or Wrath (Cleric)
// ═══════════════════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "Passive_Overcharge", menuName = "RPG/Passives/Overcharge")]
public class PassiveOvercharge : ItemPassiveEffect
{
    // value passed into Proc = amount healed BEFORE the min(amount, maxHP-currentHP) cap
    // We need to compare against what was actually absorbed to find excess.
    // The Proc is called with the RAW heal amount; excess = rawHeal - actualHeal.
    // CombatEntity.Heal() must store actualHealApplied before calling the hook.

    public PassiveOvercharge()
    {
        trigger = PassiveTriggerType.OnHeal;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value)
    {
        // value = raw heal amount requested
        // owner = the entity that was healed (self-only check enforced by caller)
        if (owner == null) return;

        // Calculate how much was actually applied vs the raw amount
        // currentHealth is already updated at this point, so:
        // actual = min(value, maxHealth - (currentHealth - value))  — but simpler:
        // We stored the pre-heal HP in passiveState["preHealHP"]
        int preHealHP = 0;
        if (owner.passiveState.TryGetValue("preHealHP", out object stored))
            preHealHP = (int)stored;

        int actualHeal = owner.currentHealth - preHealHP;
        int excess = value - actualHeal;

        if (excess <= 0) return;

        switch (owner.characterClass)
        {
            case CharacterClass.Mage:
                int oldMana = owner.mana;
                owner.mana = Mathf.Clamp(owner.mana + excess, 0, 100);
                int manaGained = owner.mana - oldMana;
                if (manaGained > 0)
                {
                    owner.UpdateClassResourceBar();
                    CombatLog.Instance?.AddEntry(
                        $"⚡ {owner.entityName} Overcharge! {manaGained} excess healing → Mana!"
                    );
                    OnScreenNotification.Instance?.ShowInfo(
                        $"{owner.entityName} Overcharge! +{manaGained} Mana"
                    );
                }
                break;

            case CharacterClass.Cleric:
                int oldWrath = owner.wrath;
                owner.wrath = Mathf.Clamp(owner.wrath + excess, 0, 100);
                int wrathGained = owner.wrath - oldWrath;
                if (wrathGained > 0)
                {
                    owner.UpdateClassResourceBar();
                    CombatLog.Instance?.AddEntry(
                        $"🔥 {owner.entityName} Overcharge! {wrathGained} excess healing → Wrath!"
                    );
                    OnScreenNotification.Instance?.ShowInfo(
                        $"{owner.entityName} Overcharge! +{wrathGained} Wrath"
                    );
                }
                break;
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// PASSIVE: MASOCHISM (OnTakeDamage — Cleric only)
// Gain 5 Wrath per 20 HP lost on a single hit
// ═══════════════════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "Passive_Masochism", menuName = "RPG/Passives/Masochism")]
public class PassiveMasochism : ItemPassiveEffect
{
    [Header("Masochism Settings")]
    [Tooltip("HP lost per Wrath tick.")]
    public int hpPerWrathTick = 20;

    [Tooltip("Wrath gained per tick.")]
    public int wrathPerTick = 5;

    public PassiveMasochism()
    {
        trigger = PassiveTriggerType.OnTakeDamage;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value)
    {
        if (owner == null || owner.characterClass != CharacterClass.Cleric) return;
        if (value <= 0) return;

        int ticks = value / hpPerWrathTick;
        if (ticks <= 0) return;

        int wrathGained = ticks * wrathPerTick;
        int oldWrath = owner.wrath;
        owner.wrath = Mathf.Clamp(owner.wrath + wrathGained, 0, 100);
        int actual = owner.wrath - oldWrath;

        if (actual > 0)
        {
            owner.UpdateClassResourceBar();
            CombatLog.Instance?.AddEntry(
                $"😈 {owner.entityName} Masochism! Pain fuels wrath! +{actual} Wrath"
            );
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// PASSIVE: PHOENIX (OnDeath — once per expedition)
// ═══════════════════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "Passive_Phoenix", menuName = "RPG/Passives/Phoenix")]
public class PassivePhoenix : ItemPassiveEffect
{
    [Header("Phoenix Settings")]
    [Range(0f, 1f)]
    [Tooltip("Fraction of max HP restored on revival.")]
    public float reviveHealPercent = 0.3f;

    [Tooltip("Particle effect to play on revival (optional).")]
    public GameObject reviveParticle;

    private const string UsedKey = "Phoenix_Used";

    public PassivePhoenix()
    {
        trigger = PassiveTriggerType.OnDeath;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value)
    {
        if (owner == null) return;

        // Check if already used this expedition
        if (owner.passiveState.ContainsKey(UsedKey)) return;

        owner.passiveState[UsedKey] = true;

        int healAmount = Mathf.Max(1, Mathf.RoundToInt(owner.maxHealth * reviveHealPercent));
        owner.currentHealth = healAmount;
        owner.isDead = false;

        // Revert death-lockout on ViewerData since we prevented death
        if (owner.viewerData != null)
        {
            owner.viewerData.isDead = false;
            owner.viewerData.deathLockoutUntil = System.DateTime.MinValue;
            owner.viewerData.baseStats.currentHealth = healAmount;
        }

        owner.UpdateHealthBar();

        // Visual
        if (reviveParticle != null)
        {
            GameObject fx = Object.Instantiate(reviveParticle, owner.transform.position, Quaternion.identity);
            Object.Destroy(fx, 3f);
        }

        if (ParticleEffectManager.Instance != null)
            ParticleEffectManager.Instance.TriggerConfetti();

        OnScreenNotification.Instance?.ShowSuccess(
            $"🔥 PHOENIX! {owner.entityName} cheats death! ({Mathf.RoundToInt(reviveHealPercent * 100)}% HP restored)"
        );

        CombatLog.Instance?.AddEntry(
            $"🔥 {owner.entityName}'s Phoenix passive triggers! Revived with {healAmount} HP!"
        );

        Debug.Log($"[Phoenix] {owner.entityName} survived death via Phoenix passive.");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// PASSIVE: CENSER (OnHeal — grants defense to heal target)
// ═══════════════════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "Passive_Censer", menuName = "RPG/Passives/Censer")]
public class PassiveCenser : ItemPassiveEffect
{
    [Header("Censer Settings")]
    [Tooltip("Flat defense bonus granted.")]
    public int defenseBonus = 5;

    [Tooltip("Duration in turns.")]
    public int duration = 2;

    public PassiveCenser()
    {
        trigger = PassiveTriggerType.OnHeal;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value)
    {
        // owner = healer (has the item), target = entity being healed
        // We need to apply the buff to whoever was healed.
        // The PassiveEffectProcessor.OnHeal passes (healed, healer, amount).
        // So owner = healed entity, target = healer.
        // We apply the buff to owner.

        if (owner == null || owner.isDead) return;

        // Look for existing Censer effect and refresh duration
        foreach (var effect in owner.activeEffects)
        {
            if (effect.effectName == "Censer Defense")
            {
                effect.duration = duration; // refresh
                CombatLog.Instance?.AddEntry(
                    $"🛡 {owner.entityName}'s Censer defense refreshed! ({duration} turns)"
                );
                return;
            }
        }

        // Apply new buff
        StatusEffect buff = new StatusEffect
        {
            effectName = "Censer Defense",
            duration = duration,
            temporaryDefenseBonus = defenseBonus,
            damageMultiplier = 1f,
            defenseMultiplier = 1f,
        };

        owner.activeEffects.Add(buff);

        CombatLog.Instance?.AddEntry(
            $"🕯 {owner.entityName} gains +{defenseBonus} Defense from Censer! ({duration} turns)"
        );
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// PASSIVE: THORNS (OnTakeDamage)
// Reflect X% of incoming final damage back to attacker
// ═══════════════════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "Passive_Thorns", menuName = "RPG/Passives/Thorns")]
public class PassiveThorns : ItemPassiveEffect
{
    [Header("Thorns Settings")]
    [Range(0f, 1f)]
    [Tooltip("Fraction of final damage reflected back to attacker.")]
    public float reflectPercent = 0.15f;

    public PassiveThorns()
    {
        trigger = PassiveTriggerType.OnTakeDamage;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value)
    {
        // owner = victim (has the item), target = attacker, value = final damage received
        if (target == null || target.isDead || value <= 0) return;

        int reflected = Mathf.Max(1, Mathf.RoundToInt(value * reflectPercent));

        // Bypass normal TakeDamage to avoid infinite reflect loops
        int afterDef = Mathf.Max(0, reflected - target.defense);
        target.currentHealth = Mathf.Max(0, target.currentHealth - afterDef);

        CombatVisualEffects.Instance?.ShowDamageNumber(target.transform.position, afterDef);
        CombatLog.Instance?.AddEntry(
            $"🌵 {owner.entityName}'s Thorns reflect {afterDef} damage to {target.entityName}!"
        );

        target.animator?.SetTrigger("Hit");

        if (target.currentHealth <= 0)
        {
            target.currentHealth = 0;
            target.Die();
        }
        else
        {
            target.UpdateHealthBar();
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// PASSIVE: LONE WOLF (OnCombatStart / evaluated inline)
// Bonus stats when solo or last survivor
// ═══════════════════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "Passive_LoneWolf", menuName = "RPG/Passives/LoneWolf")]
public class PassiveLoneWolf : ItemPassiveEffect
{
    [Header("Lone Wolf Settings")]
    [Tooltip("Stat boosts applied when solo.")]
    public List<LoneWolfStatPair> statBoosts = new List<LoneWolfStatPair>();

    private const string ActiveKey = "LoneWolf_Active";

    public PassiveLoneWolf()
    {
        trigger = PassiveTriggerType.OnTurnStart;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value)
    {
        if (owner == null || ExpeditionManager.Instance == null) return;

        bool isSolo = ExpeditionManager.Instance.GetAllPlayerEntities().Count <= 1;

        bool wasActive = owner.passiveState.ContainsKey(ActiveKey) && (bool)owner.passiveState[ActiveKey];

        if (isSolo && !wasActive)
        {
            owner.passiveState[ActiveKey] = true;
            ApplyBoosts(owner, true);
            CombatLog.Instance?.AddEntry($"🐺 {owner.entityName}'s Lone Wolf activates!");
            OnScreenNotification.Instance?.ShowInfo($"🐺 {owner.entityName} is a Lone Wolf!");
        }
        else if (!isSolo && wasActive)
        {
            owner.passiveState[ActiveKey] = false;
            ApplyBoosts(owner, false);
            CombatLog.Instance?.AddEntry($"🐺 {owner.entityName}'s Lone Wolf deactivates.");
        }
    }

    private void ApplyBoosts(CombatEntity entity, bool apply)
    {
        int sign = apply ? 1 : -1;

        foreach (var pair in statBoosts)
        {
            int amount = Mathf.RoundToInt(PassiveEffectProcessor.GetBaseStatValue(entity, pair.stat) * pair.bonusPercent);
            amount = Mathf.Max(1, amount) * sign;
            ApplyStatDelta(entity, pair.stat, amount);
        }
    }

    private void ApplyStatDelta(CombatEntity entity, BoostableStat stat, int delta)
    {
        switch (stat)
        {
            case BoostableStat.Strength: entity.strength += delta; break;
            case BoostableStat.Constitution: entity.constitution += delta; break;
            case BoostableStat.Dexterity: entity.dexterity += delta; break;
            case BoostableStat.Intelligence: entity.intelligence += delta; break;
            case BoostableStat.Willpower: entity.willpower += delta; break;
            case BoostableStat.Charisma: entity.charisma += delta; break;
        }
    }
}

[System.Serializable]
public class LoneWolfStatPair
{
    public BoostableStat stat;
    [Range(0f, 1f)]
    [Tooltip("Bonus as a fraction of base stat. 0.2 = +20%.")]
    public float bonusPercent = 0.2f;
}

// ═══════════════════════════════════════════════════════════════════════════════
// PASSIVE: BERSERKER (Calculation passive — queried inline)
// Gain stat bonuses scaling with % HP missing
// ═══════════════════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "Passive_Berserker", menuName = "RPG/Passives/Berserker")]
public class PassiveBerserker : ItemPassiveEffect
{
    [Header("Berserker Settings")]
    [Tooltip("One entry per stat you want to scale. Each stat can have its own rate.")]
    public List<BerserkerStatPair> statPairs = new List<BerserkerStatPair>();

    public PassiveBerserker()
    {
        trigger = PassiveTriggerType.Passive;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value) { }
    public override bool IsCalculationPassive() => true;
}

[System.Serializable]
public class BerserkerStatPair
{
    public BoostableStat stat;
    [Tooltip("Bonus percent of base stat gained per 1% of max HP missing.\n" +
             "e.g. 0.5 = +0.5% STR per 1% HP missing → at 50% HP = +25% STR")]
    public float bonusPercentPerMissingPercent = 0.5f;
}

// ═══════════════════════════════════════════════════════════════════════════════
// PASSIVE: GUARDIAN (OnTakeDamage — chance to gain barrier)
// ═══════════════════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "Passive_Guardian", menuName = "RPG/Passives/Guardian")]
public class PassiveGuardian : ItemPassiveEffect
{
    [Header("Guardian Settings")]
    [Range(0f, 1f)]
    public float procChance = 0.25f;

    [Header("Barrier Amount")]
    [Tooltip("Flat base barrier HP.")]
    public int baseBarrierAmount = 20;

    [Tooltip("Stat to scale the barrier with. Use None for flat only.")]
    public DamageStat scalingStat = DamageStat.None;

    [Range(0f, 3f)]
    [Tooltip("Multiplier applied to the scaling stat. e.g. 0.5 = barrier += 0.5 × CON")]
    public float scalingMultiplier = 0f;

    public PassiveGuardian()
    {
        trigger = PassiveTriggerType.OnTakeDamage;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value)
    {
        if (owner == null || owner.isDead) return;
        if (Random.value > procChance) return;

        int barrierAmount = baseBarrierAmount;

        if (scalingStat != DamageStat.None && scalingMultiplier > 0f)
        {
            int statValue = GetEntityStatValue(owner, scalingStat);
            barrierAmount += Mathf.RoundToInt(statValue * scalingMultiplier);
        }

        barrierAmount = Mathf.Max(1, barrierAmount);

        StatusEffect barrier = new StatusEffect
        {
            effectName = "Guardian Barrier",
            duration = 999,
            isBarrier = true,
            barrierCurrentAmount = barrierAmount,
            barrierMaxAmount = barrierAmount,
            damageMultiplier = 1f,
            defenseMultiplier = 1f,
        };

        owner.activeEffects.Add(barrier);

        CombatVisualEffects.Instance?.PlayBuffEffect(owner.transform.position);
        CombatLog.Instance?.AddEntry(
            $"🛡 {owner.entityName}'s Guardian proc! Barrier of {barrierAmount} HP!"
        );
    }

    private int GetEntityStatValue(CombatEntity entity, DamageStat stat)
    {
        switch (stat)
        {
            case DamageStat.Strength: return entity.strength;
            case DamageStat.Constitution: return entity.constitution;
            case DamageStat.Dexterity: return entity.dexterity;
            case DamageStat.Intelligence: return entity.intelligence;
            case DamageStat.Willpower: return entity.willpower;
            case DamageStat.Charisma: return entity.charisma;
            default: return 0;
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// PASSIVE: EXECUTIONER (Calculation passive — queried inline)
// Deal bonus damage when target is below X% HP
// ═══════════════════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "Passive_Executioner", menuName = "RPG/Passives/Executioner")]
public class PassiveExecutioner : ItemPassiveEffect
{
    [Header("Executioner Settings")]
    [Range(0f, 1f)]
    [Tooltip("Target must be at or below this HP fraction. 0.3 = below 30% HP.")]
    public float healthThresholdPercent = 0.3f;

    [Range(0f, 2f)]
    [Tooltip("Bonus damage multiplier added on top of 1.0. 0.5 = +50% damage.")]
    public float bonusDamagePercent = 0.5f;

    public PassiveExecutioner()
    {
        trigger = PassiveTriggerType.Passive;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value) { }
    public override bool IsCalculationPassive() => true;
}

// ═══════════════════════════════════════════════════════════════════════════════
// PASSIVE: TWIN STRIKE (OnDealDamage)
// X% chance to attack again with the same ability (no cascade)
// ═══════════════════════════════════════════════════════════════════════════════

[CreateAssetMenu(fileName = "Passive_TwinStrike", menuName = "RPG/Passives/TwinStrike")]
public class PassiveTwinStrike : ItemPassiveEffect
{
    [Header("Twin Strike Settings")]
    [Range(0f, 1f)]
    [Tooltip("Chance per attack to trigger a second hit.")]
    public float procChance = 0.2f;

    private const string ProcGuard = "TwinStrike_Proccing";

    public PassiveTwinStrike()
    {
        trigger = PassiveTriggerType.OnDealDamage;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value)
    {
        if (owner == null || target == null || target.isDead) return;

        // Anti-cascade guard — prevent the second hit from triggering Twin Strike again
        if (owner.passiveState.ContainsKey(ProcGuard)) return;

        if (Random.value > procChance) return;

        // Find the ability that was last used — stored as queuedAction on the entity
        // We need to re-execute via CombatCalculations with the same queued ability.
        // We look it up from the AbilityDatabase using queuedAction command string.
        string abilityCmd = owner.queuedAction;
        if (string.IsNullOrEmpty(abilityCmd)) return;

        AbilityData ability = AbilityDatabase.Instance?.GetAbility(abilityCmd);
        if (ability == null || ability.category != AbilityCategory.Damage) return;

        owner.passiveState[ProcGuard] = true;

        CombatLog.Instance?.AddEntry(
            $"⚡ {owner.entityName}'s Twin Strike procs! Attacking again!"
        );
        OnScreenNotification.Instance?.ShowInfo(
            $"⚡ TWIN STRIKE! {owner.entityName} attacks twice!"
        );

        // Execute the bonus hit
        CombatCalculations.ExecuteAbility(owner, target, ability);

        owner.passiveState.Remove(ProcGuard);
    }
}
