using UnityEngine;

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
            case DamageStat.Strength:     return entity.strength;
            case DamageStat.Constitution: return entity.constitution;
            case DamageStat.Dexterity:    return entity.dexterity;
            case DamageStat.Intelligence: return entity.intelligence;
            case DamageStat.Willpower:    return entity.willpower;
            case DamageStat.Charisma:     return entity.charisma;
            default: return 0;
        }
    }
}
