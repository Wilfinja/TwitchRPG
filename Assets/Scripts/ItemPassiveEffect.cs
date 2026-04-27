using UnityEngine;

public enum PassiveTriggerType
{
    OnCombatStart,
    OnTurnStart,
    OnDealDamage,
    OnTakeDamage,
    OnKill,
    OnHealthThreshold // e.g., below 30% HP
}

public abstract class ItemPassiveEffect : ScriptableObject
{
    public string passiveName;
    [TextArea(2, 3)]
    public string description;
    public PassiveTriggerType trigger;

    // The logic that happens when the hook is called
    public abstract void Proc(CombatEntity owner, CombatEntity target, int value = 0);
}
