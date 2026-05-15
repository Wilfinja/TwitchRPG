using UnityEngine;

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
