using UnityEngine;

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
    }

    public override bool IsCalculationPassive() => true;
}
