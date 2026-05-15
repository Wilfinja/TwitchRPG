using System.Collections.Generic;
using UnityEngine;

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
