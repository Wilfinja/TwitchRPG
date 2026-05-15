using UnityEngine;

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
        if (owner == null || owner.isDead) return;

        foreach (var effect in owner.activeEffects)
        {
            if (effect.effectName == "Censer Defense")
            {
                effect.duration = duration;
                CombatLog.Instance?.AddEntry(
                    $"🛡 {owner.entityName}'s Censer defense refreshed! ({duration} turns)"
                );
                return;
            }
        }

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
