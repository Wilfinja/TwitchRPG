using System.Collections.Generic;
using UnityEngine;

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
