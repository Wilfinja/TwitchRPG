using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Controls enemy AI behavior with role-based targeting and ability selection
/// Includes zoom-to-center support and projectile spawning
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

        // ✅ Choose ability and target using role-based AI
        AbilityData chosenAbility = ChooseAbility();

        if (chosenAbility != null)
        {
            CombatEntity target = ChooseTarget(chosenAbility);

            if (target != null)
            {
                // ✅ NEW: Zoom to center if ability requires it
                Vector3 originalPosition = entity.transform.position;
                bool didZoom = false;

                if (chosenAbility.zoomToCenter)
                {
                    yield return StartCoroutine(ZoomToPosition(entity.transform, CombatTurnManager.Instance.centerPosition, CombatTurnManager.Instance.zoomDuration));
                    didZoom = true;
                }

                // Trigger animation
                entity.animator?.SetTrigger(chosenAbility.animationTrigger);

                // ✅ Spawn projectile from current position (center if zoomed)
                if (chosenAbility.projectilePrefab != null)
                {
                    SpawnProjectile(entity, target, chosenAbility);
                    yield return new WaitForSeconds(chosenAbility.projectileSpeed);
                }
                else
                {
                    yield return new WaitForSeconds(0.3f);
                }

                // Execute ability
                CombatCalculations.ExecuteAbility(entity, target, chosenAbility);

                yield return new WaitForSeconds(0.5f);

                // ✅ NEW: Zoom back if we zoomed
                if (didZoom && !entity.isDead)
                {
                    yield return StartCoroutine(ZoomToPosition(entity.transform, originalPosition, CombatTurnManager.Instance.zoomDuration));
                }

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

    // ═══════════════════════════════════════════════════════════════
    // ✅ ROLE-BASED ABILITY SELECTION
    // ═══════════════════════════════════════════════════════════════
    AbilityData ChooseAbility()
    {
        if (enemyData == null || enemyData.abilities.Count == 0)
            return null;

        List<CombatEntity> players = ExpeditionManager.Instance.GetAllPlayerEntities();
        if (players.Count == 0) return null;

        // Get available abilities (off cooldown)
        List<AbilityData> availableAbilities = new List<AbilityData>();
        foreach (var ability in enemyData.abilities)
        {
            if (!abilityCooldowns.ContainsKey(ability) || abilityCooldowns[ability] <= 0)
            {
                availableAbilities.Add(ability);
            }
        }

        if (availableAbilities.Count == 0)
        {
            // All on cooldown, use basic attack
            return enemyData.abilities.Count > 0 ? enemyData.abilities[0] : null;
        }

        // ✅ ROLE-BASED SELECTION
        switch (enemyData.role)
        {
            case EnemyRole.Boss:
                // Bosses prefer AOE when multiple targets available
                if (enemyData.useAOEWhenPossible && players.Count >= 2)
                {
                    var aoeAbility = availableAbilities.Find(a => a.isAOE);
                    if (aoeAbility != null) return aoeAbility;
                }
                // Otherwise use highest damage ability
                return availableAbilities.OrderByDescending(a => a.baseDamage).First();

            case EnemyRole.Controller:
                // Controllers prioritize buffs/debuffs
                if (enemyData.buffAllies)
                {
                    var buffAbility = availableAbilities.Find(a => a.category == AbilityCategory.Buff);
                    if (buffAbility != null) return buffAbility;
                }
                // Then debuffs
                var debuffAbility = availableAbilities.Find(a => a.appliesEffects.Count > 0);
                if (debuffAbility != null) return debuffAbility;
                break;

            case EnemyRole.Assassin:
                // Assassins use highest damage ability when targeting low HP
                return availableAbilities.OrderByDescending(a => a.baseDamage).First();

            case EnemyRole.Ranged:
            case EnemyRole.Mastermind:
                // Prefer ranged/spell abilities (higher intelligence scaling)
                var rangedAbility = availableAbilities.Find(a => a.scalingStat == DamageStat.Intelligence || a.scalingStat == DamageStat.Dexterity);
                if (rangedAbility != null) return rangedAbility;
                break;

            case EnemyRole.Minion:
            default:
                // Minions just use first available
                break;
        }

        // Fallback: first available ability
        return availableAbilities[0];
    }

    // ═══════════════════════════════════════════════════════════════
    // ✅ ROLE-BASED TARGETING
    // ═══════════════════════════════════════════════════════════════
    CombatEntity ChooseTarget(AbilityData ability)
    {
        List<CombatEntity> players = ExpeditionManager.Instance.GetAllPlayerEntities();

        if (players.Count == 0) return null;

        // Filter out dead players
        players = players.Where(p => !p.isDead).ToList();

        if (players.Count == 0) return null;

        // ✅ ROLE-BASED TARGETING
        switch (enemyData.role)
        {
            case EnemyRole.Assassin:
                // Target lowest HP player
                if (enemyData.prioritizeLowHP)
                {
                    return players.OrderBy(p => p.currentHealth).First();
                }
                break;

            case EnemyRole.Mastermind:
                // Target backline (highest position number)
                if (enemyData.prioritizeBackline)
                {
                    return players.OrderByDescending(p => p.position).First();
                }
                break;

            case EnemyRole.Ranged:
                // Target high defense targets (smart targeting to bypass tanks)
                if (enemyData.prioritizeHighDefense)
                {
                    return players.OrderByDescending(p => p.defense).First();
                }
                // Or target random
                return players[Random.Range(0, players.Count)];

            case EnemyRole.Controller:
                // Target whoever doesn't have debuffs yet
                var unbuffedTarget = players.Find(p => p.activeEffects.Count == 0);
                if (unbuffedTarget != null) return unbuffedTarget;
                break;

            case EnemyRole.Boss:
                // Bosses target highest threat (highest level or most HP)
                return players.OrderByDescending(p => p.maxHealth).First();

            case EnemyRole.Minion:
            default:
                // Minions target front-most player
                return players.OrderBy(p => p.position).First();
        }

        // Fallback: front-most player
        return players.OrderBy(p => p.position).First();
    }

    // ═══════════════════════════════════════════════════════════════
    // ✅ ZOOM ANIMATION
    // ═══════════════════════════════════════════════════════════════
    IEnumerator ZoomToPosition(Transform target, Vector3 destination, float duration)
    {
        Vector3 startPosition = target.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Ease-in-out curve (smooth start and stop)
            float easedT = t < 0.5f
                ? 2f * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            target.position = Vector3.Lerp(startPosition, destination, easedT);
            yield return null;
        }

        target.position = destination;
    }

    // ═══════════════════════════════════════════════════════════════
    // ✅ PROJECTILE SPAWNING
    // ═══════════════════════════════════════════════════════════════
    void SpawnProjectile(CombatEntity caster, CombatEntity target, AbilityData ability)
    {
        Vector3 startPos = caster.transform.position;
        Vector3 endPos = target.transform.position;

        GameObject projectileObj = Instantiate(ability.projectilePrefab, startPos, Quaternion.identity);

        Projectile projectile = projectileObj.GetComponent<Projectile>();
        if (projectile == null)
        {
            projectile = projectileObj.AddComponent<Projectile>();
        }

        projectile.Launch(startPos, endPos, ability.projectileSpeed);

        Debug.Log($"[Enemy AI] {caster.entityName} fired projectile at {target.entityName}");
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
