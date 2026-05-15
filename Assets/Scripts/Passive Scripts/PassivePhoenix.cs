using UnityEngine;

[CreateAssetMenu(fileName = "Passive_Phoenix", menuName = "RPG/Passives/Phoenix")]
public class PassivePhoenix : ItemPassiveEffect
{
    [Header("Phoenix Settings")]
    [Range(0f, 1f)]
    [Tooltip("Fraction of max HP restored on revival.")]
    public float reviveHealPercent = 0.3f;

    [Tooltip("Particle effect to play on revival (optional).")]
    public GameObject reviveParticle;

    private const string UsedKey = "Phoenix_Used";

    public PassivePhoenix()
    {
        trigger = PassiveTriggerType.OnDeath;
    }

    public override void Proc(CombatEntity owner, CombatEntity target, int value)
    {
        if (owner == null) return;

        if (owner.passiveState.ContainsKey(UsedKey)) return;

        owner.passiveState[UsedKey] = true;

        int healAmount = Mathf.Max(1, Mathf.RoundToInt(owner.maxHealth * reviveHealPercent));
        owner.currentHealth = healAmount;
        owner.isDead = false;

        if (owner.viewerData != null)
        {
            owner.viewerData.isDead = false;
            owner.viewerData.deathLockoutUntil = System.DateTime.MinValue;
            owner.viewerData.baseStats.currentHealth = healAmount;
        }

        owner.UpdateHealthBar();

        if (reviveParticle != null)
        {
            GameObject fx = Object.Instantiate(reviveParticle, owner.transform.position, Quaternion.identity);
            Object.Destroy(fx, 3f);
        }

        if (ParticleEffectManager.Instance != null)
            ParticleEffectManager.Instance.TriggerConfetti();

        OnScreenNotification.Instance?.ShowSuccess(
            $"🔥 PHOENIX! {owner.entityName} cheats death! ({Mathf.RoundToInt(reviveHealPercent * 100)}% HP restored)"
        );

        CombatLog.Instance?.AddEntry(
            $"🔥 {owner.entityName}'s Phoenix passive triggers! Revived with {healAmount} HP!"
        );

        Debug.Log($"[Phoenix] {owner.entityName} survived death via Phoenix passive.");
    }
}
