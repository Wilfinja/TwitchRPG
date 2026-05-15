using UnityEngine;

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
