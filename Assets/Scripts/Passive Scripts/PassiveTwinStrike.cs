using UnityEngine;

[CreateAssetMenu(fileName = "Passive_TwinStrike", menuName = "RPG/Passives/TwinStrike")]
public class PassiveTwinStrike : ItemPassiveEffect
{
    [Header("Twin Strike Settings")]
    [Range(0f, 1f)]
    [Tooltip("Chance per attack to trigger a second hit.")]
    public float procChance = 0.2f;

    private const string ProcGuard = "TwinStrike_Proccing";

    public PassiveTwinStrike()
    {
        trigger = PassiveTriggerType.OnDealDamage;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value)
    {
        if (owner == null || target == null || target.isDead) return;

        // Anti-cascade guard
        if (owner.passiveState.ContainsKey(ProcGuard)) return;

        if (Random.value > procChance) return;

        string abilityCmd = owner.queuedAction;
        if (string.IsNullOrEmpty(abilityCmd)) return;

        AbilityData ability = AbilityDatabase.Instance?.GetAbility(abilityCmd);
        if (ability == null || ability.category != AbilityCategory.Damage) return;

        owner.passiveState[ProcGuard] = true;

        CombatLog.Instance?.AddEntry(
            $"⚡ {owner.entityName}'s Twin Strike procs! Attacking again!"
        );
        OnScreenNotification.Instance?.ShowInfo(
            $"⚡ TWIN STRIKE! {owner.entityName} attacks twice!"
        );

        CombatCalculations.ExecuteAbility(owner, target, ability);

        owner.passiveState.Remove(ProcGuard);
    }
}
