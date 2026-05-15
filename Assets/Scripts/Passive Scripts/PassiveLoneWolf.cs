using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Passive_LoneWolf", menuName = "RPG/Passives/LoneWolf")]
public class PassiveLoneWolf : ItemPassiveEffect
{
    [Header("Lone Wolf Settings")]
    [Tooltip("Stat boosts applied when solo.")]
    public List<LoneWolfStatPair> statBoosts = new List<LoneWolfStatPair>();

    private const string ActiveKey = "LoneWolf_Active";

    public PassiveLoneWolf()
    {
        trigger = PassiveTriggerType.OnTurnStart;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value)
    {
        if (owner == null || ExpeditionManager.Instance == null) return;

        bool isSolo = ExpeditionManager.Instance.GetAllPlayerEntities().Count <= 1;
        bool wasActive = owner.passiveState.ContainsKey(ActiveKey) && (bool)owner.passiveState[ActiveKey];

        if (isSolo && !wasActive)
        {
            owner.passiveState[ActiveKey] = true;
            ApplyBoosts(owner, true);
            CombatLog.Instance?.AddEntry($"🐺 {owner.entityName}'s Lone Wolf activates!");
            OnScreenNotification.Instance?.ShowInfo($"🐺 {owner.entityName} is a Lone Wolf!");
        }
        else if (!isSolo && wasActive)
        {
            owner.passiveState[ActiveKey] = false;
            ApplyBoosts(owner, false);
            CombatLog.Instance?.AddEntry($"🐺 {owner.entityName}'s Lone Wolf deactivates.");
        }
    }

    private void ApplyBoosts(CombatEntity entity, bool apply)
    {
        int sign = apply ? 1 : -1;
        foreach (var pair in statBoosts)
        {
            int amount = Mathf.RoundToInt(PassiveEffectProcessor.GetBaseStatValue(entity, pair.stat) * pair.bonusPercent);
            amount = Mathf.Max(1, amount) * sign;
            ApplyStatDelta(entity, pair.stat, amount);
        }
    }

    private void ApplyStatDelta(CombatEntity entity, BoostableStat stat, int delta)
    {
        switch (stat)
        {
            case BoostableStat.Strength:     entity.strength     += delta; break;
            case BoostableStat.Constitution: entity.constitution += delta; break;
            case BoostableStat.Dexterity:    entity.dexterity    += delta; break;
            case BoostableStat.Intelligence: entity.intelligence += delta; break;
            case BoostableStat.Willpower:    entity.willpower    += delta; break;
            case BoostableStat.Charisma:     entity.charisma     += delta; break;
        }
    }
}
