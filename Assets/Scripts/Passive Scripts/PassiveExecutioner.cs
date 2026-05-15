using UnityEngine;

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
