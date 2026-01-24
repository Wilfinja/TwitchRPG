using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Controls enemy AI behavior and ability usage
/// NOTE: This is a basic implementation for Part 1-3. Full AI comes in Part 4.
/// </summary>
public class EnemyCombatController : MonoBehaviour
{
    [Header("Enemy Data")]
    public EnemyData enemyData;

    private CombatEntity entity;
    private Dictionary<AbilityData, int> abilityCooldowns = new Dictionary<AbilityData, int>();

    void Awake()
    {
        entity = GetComponent<CombatEntity>();
    }

    public void Initialize(EnemyData data)
    {
        enemyData = data;

        // Initialize cooldowns
        foreach (var ability in data.abilities)
        {
            abilityCooldowns[ability] = 0;
        }
    }

    public IEnumerator ExecuteAIAction()
    {
        if (entity.isDead) yield break;

        // Simple AI for testing: Use first available ability or basic attack
        AbilityData chosenAbility = ChooseAbility();

        if (chosenAbility != null)
        {
            CombatEntity target = ChooseTarget(chosenAbility);

            if (target != null)
            {
                // Trigger animation
                entity.animator?.SetTrigger(chosenAbility.animationTrigger);

                yield return new WaitForSeconds(0.3f);

                // Execute ability
                CombatCalculations.ExecuteAbility(entity, target, chosenAbility);

                // Set cooldown
                if (abilityCooldowns.ContainsKey(chosenAbility))
                {
                    abilityCooldowns[chosenAbility] = chosenAbility.cooldown;
                }
            }
        }

        // Reduce cooldowns
        ReduceCooldowns();
    }

    AbilityData ChooseAbility()
    {
        // TODO (Phase 4): Implement role-based AI behavior
        // For now: Choose first ability off cooldown

        if (enemyData == null || enemyData.abilities.Count == 0)
            return null;

        foreach (var ability in enemyData.abilities)
        {
            if (!abilityCooldowns.ContainsKey(ability) || abilityCooldowns[ability] <= 0)
            {
                return ability;
            }
        }

        // All on cooldown, use basic attack if available
        return enemyData.abilities.Count > 0 ? enemyData.abilities[0] : null;
    }

    CombatEntity ChooseTarget(AbilityData ability)
    {
        // TODO (Phase 4): Implement role-based targeting
        // For now: Target front-most player

        List<CombatEntity> players = ExpeditionManager.Instance.GetAllPlayerEntities();

        if (players.Count == 0) return null;

        // Simple targeting: front-most player
        return players[0];
    }

    void ReduceCooldowns()
    {
        List<AbilityData> abilities = new List<AbilityData>(abilityCooldowns.Keys);

        foreach (var ability in abilities)
        {
            if (abilityCooldowns[ability] > 0)
            {
                abilityCooldowns[ability]--;
            }
        }
    }
}
