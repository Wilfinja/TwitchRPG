using UnityEngine;

[CreateAssetMenu(fileName = "Passive_Overcharge", menuName = "RPG/Passives/Overcharge")]
public class PassiveOvercharge : ItemPassiveEffect
{
    public PassiveOvercharge()
    {
        trigger = PassiveTriggerType.OnHeal;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value)
    {
        if (owner == null) return;

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
