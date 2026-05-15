using UnityEngine;

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
