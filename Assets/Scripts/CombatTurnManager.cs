using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages combat turns, action queuing, and execution order
/// </summary>
public class CombatTurnManager : MonoBehaviour
{
    public static CombatTurnManager Instance;

    [Header("Turn State")]
    public bool combatActive;
    public bool playerTurn;
    public float turnTimer;
    public float maxTurnTime = 45f;

    [Header("Combat Visuals")]
    [Tooltip("Where characters zoom to when using abilities")]
    public Vector3 centerPosition = new Vector3(0, -10.5f, 0);

    [Tooltip("Zoom animation duration")]
    public float zoomDuration = 0.3f;

    [Header("Action Queue")]
    private Dictionary<string, QueuedAction> queuedActions = new Dictionary<string, QueuedAction>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Update()
    {
        if (!combatActive || !playerTurn) return;

        // Turn timer countdown
        if (turnTimer > 0)
        {
            turnTimer -= Time.deltaTime;
            CombatUIManager.Instance?.UpdateTurnTimer(turnTimer, maxTurnTime);
        }

        if (turnTimer <= 0)
        {
            // Time's up! Auto-submit default actions for players who haven't acted
            AutoSubmitDefaultActions();
            ExecutePlayerTurn();
        }
    }

    #region Combat Flow

    public void StartCombat()
    {
        combatActive = true;
        queuedActions.Clear();

        CombatUIManager.Instance?.ShowCombatUI();

        OnScreenNotification.Instance?.ShowNotification("⚔️ Combat begins! Players, queue your actions with !queue <ability> [target]");

        StartPlayerTurn();
    }

    void StartPlayerTurn()
    {
        playerTurn = true;
        turnTimer = maxTurnTime;
        queuedActions.Clear();

        // Reset all player turn flags
        List<CombatEntity> players = ExpeditionManager.Instance.GetAllPlayerEntities();
        foreach (CombatEntity player in players)
        {
            player.ResetTurn();
        }

        CombatUIManager.Instance?.ShowTurnIndicator(true);
        OnScreenNotification.Instance?.ShowNotification($"🎯 Player turn! {maxTurnTime} seconds to queue actions. Use !queue <ability>");
    }

    void ExecutePlayerTurn()
    {
        playerTurn = false;
        CombatUIManager.Instance?.ShowTurnIndicator(false);

        StartCoroutine(ExecutePlayerActions());

        Debug.Log("Player turn coming up!");
    }

    IEnumerator ExecutePlayerActions()
    {
        Debug.Log("[Combat] ═══ STARTING PLAYER TURN EXECUTION ═══");

        List<CombatEntity> players = ExpeditionManager.Instance.GetAllPlayerEntities();
        Debug.Log($"[Combat] Found {players.Count} alive players");

        // Organize actions by category: Buffs -> Heals -> Damage
        List<QueuedAction> buffActions = new List<QueuedAction>();
        List<QueuedAction> healActions = new List<QueuedAction>();
        List<QueuedAction> damageActions = new List<QueuedAction>();

        Debug.Log($"[Combat] Total queued actions: {queuedActions.Count}");

        foreach (var kvp in queuedActions)
        {
            QueuedAction action = kvp.Value;
            Debug.Log($"[Combat] Action: {action.caster.entityName} → {action.ability.abilityName} → {action.target.entityName}");

            if (action.ability.category == AbilityCategory.Buff)
                buffActions.Add(action);
            else if (action.ability.category == AbilityCategory.Heal)
                healActions.Add(action);
            else
                damageActions.Add(action);
        }

        Debug.Log($"[Combat] Organized: {buffActions.Count} buffs, {healActions.Count} heals, {damageActions.Count} damage");

        // Sort each category by position (1 -> 4)
        buffActions = buffActions.OrderBy(a => a.caster.position).ToList();
        healActions = healActions.OrderBy(a => a.caster.position).ToList();
        damageActions = damageActions.OrderBy(a => a.caster.position).ToList();

        // Execute buffs
        Debug.Log("[Combat] Executing BUFF actions...");
        foreach (QueuedAction action in buffActions)
        {
            yield return StartCoroutine(ExecuteActionWithHaste(action));
        }

        // Execute heals
        Debug.Log("[Combat] Executing HEAL actions...");
        foreach (QueuedAction action in healActions)
        {
            yield return StartCoroutine(ExecuteActionWithHaste(action));
        }

        // Execute damage
        Debug.Log("[Combat] Executing DAMAGE actions...");
        foreach (QueuedAction action in damageActions)
        {
            yield return StartCoroutine(ExecuteActionWithHaste(action));
        }

        Debug.Log("[Combat] All actions executed!");

        // Process status effects for all players
        foreach (CombatEntity player in players)
        {
            player.ProcessStatusEffects();
        }

        // ✅ FIX v2: Check for PvP FIRST, use correct method to get fighters
        if (PvPManager.Instance != null && PvPManager.Instance.pvpActive)
        {
            Debug.Log("[Combat] PvP mode - checking for match end");

            // ✅ CORRECTED: Use GetAllLivingCombatants() instead of GetAllPlayerEntities()
            List<CombatEntity> aliveFighters = GetAllLivingCombatants();

            Debug.Log($"[Combat] PvP fighters alive: {aliveFighters.Count}");

            if (aliveFighters.Count <= 1)
            {
                Debug.Log($"[Combat] PvP match over! {aliveFighters.Count} fighter(s) remaining");

                if (aliveFighters.Count == 1)
                {
                    // Winner!
                    PvPManager.Instance.OnPvPMatchEnd(aliveFighters[0].userId);
                }
                else
                {
                    // Draw (both died somehow)
                    Debug.LogWarning("[Combat] PvP ended in a draw!");
                    PvPManager.Instance.OnPvPMatchEnd(null);
                }

                combatActive = false;
                yield break;
            }

            // Match continues - skip enemy turn (no enemies in PvP)
            Debug.Log("[Combat] PvP continuing - both fighters alive, starting next player turn");
            yield return new WaitForSeconds(0.5f);
            StartPlayerTurn();
            yield break;
        }

        // ✅ EXISTING CODE: PvE expedition logic (only runs if NOT PvP)
        // Check if wave is cleared
        if (CheckWaveCleared())
        {
            Debug.Log("[Combat] WAVE CLEARED!");
            // Expedition wave cleared
            ExpeditionManager.Instance.OnWaveCleared();
            yield break;
        }

        Debug.Log("[Combat] Wave not cleared, starting enemy turn...");

        // Enemy turn (PvE only)
        yield return StartCoroutine(ExecuteEnemyTurn());

        // Check for player wipe (PvE only)
        if (CheckPlayerWipe())
        {
            Debug.Log("[Combat] PLAYER WIPE!");
            // Expedition failure
            ExpeditionManager.Instance.CompleteExpedition(false);
            yield break;
        }

        Debug.Log("[Combat] Starting next player turn...");

        // Start next player turn
        StartPlayerTurn();
    }

    /// <summary>
    /// Executes an action, then executes it a second time if the caster is Hasted.
    /// </summary>
    IEnumerator ExecuteActionWithHaste(QueuedAction action)
    {
        yield return StartCoroutine(ExecuteAction(action));
        yield return new WaitForSeconds(0.5f);

        // Haste check: if caster is still alive and hasted, act again
        if (!action.caster.isDead && action.caster.IsHasted())
        {
            CombatLog.Instance?.AddEntry($"⚡ {action.caster.entityName} acts again from HASTE!");
            OnScreenNotification.Instance?.ShowNotification($"⚡ {action.caster.entityName} acts twice!");

            // Re-determine target in case original is dead
            if (action.target.isDead)
            {
                List<CombatEntity> enemies = ExpeditionManager.Instance?.GetAllEnemyEntities();
                if (enemies != null && enemies.Count > 0)
                    action.target = enemies[0];
            }

            yield return StartCoroutine(ExecuteAction(action));
            yield return new WaitForSeconds(0.5f);
        }
    }

    IEnumerator ExecuteEnemyTurn()
    {
        OnScreenNotification.Instance?.ShowNotification("👹 Enemy turn!");
        CombatUIManager.Instance?.ShowTurnIndicator(false);

        yield return new WaitForSeconds(1f);

        List<CombatEntity> enemies = ExpeditionManager.Instance.GetAllEnemyEntities();

        foreach (CombatEntity enemy in enemies)
        {
            if (enemy.isDead) continue;

            // Enemy AI chooses action
            EnemyCombatController controller = enemy.GetComponent<EnemyCombatController>();
            if (controller != null)
            {
                yield return StartCoroutine(controller.ExecuteAIAction());
            }

            yield return new WaitForSeconds(0.8f);
        }

        // Process status effects for all enemies
        foreach (CombatEntity enemy in enemies)
        {
            if (!enemy.isDead)
            {
                enemy.ProcessStatusEffects();
            }
        }

        yield return new WaitForSeconds(0.5f);
    }

    /// <summary>
    /// Get all living combatants (works for both PvE and PvP)
    /// </summary>
    List<CombatEntity> GetAllLivingCombatants()
    {
        List<CombatEntity> entities = new List<CombatEntity>();

        // Get all characters on screen
        List<OnScreenCharacter> allCharacters = CharacterSpawner.Instance?.GetAllCharacters();
        if (allCharacters == null) return entities;

        foreach (var character in allCharacters)
        {
            CombatEntity entity = character.GetComponent<CombatEntity>();
            if (entity != null && !entity.isDead && entity.isPlayer)
            {
                entities.Add(entity);
            }
        }

        return entities;
    }

    #endregion

    #region Action Management

    public bool QueueAction(string userId, string username, string abilityName, string targetName = null)
    {
        CombatEntity caster = ExpeditionManager.Instance.GetPlayerEntity(userId, username);

        if (caster == null)
        {
            OnScreenNotification.Instance?.ShowNotification($"@{username} You're not in this expedition!");
            return false;
        }

        if (caster.isDead)
        {
            OnScreenNotification.Instance?.ShowNotification($"@{username} You're dead! You cannot act.");
            return false;
        }

        if (!playerTurn)
        {
            OnScreenNotification.Instance?.ShowNotification($"@{username} It's not the player turn right now!");
            return false;
        }

        // Get ability
        AbilityData ability = AbilityDatabase.Instance?.GetAbility(abilityName);

        if (ability == null)
        {
            OnScreenNotification.Instance?.ShowNotification($"@{username} Unknown ability: {abilityName}");
            return false;
        }

        // Check if player can use this ability (CLASS)
        if (ability.requiredClass != caster.characterClass)
        {
            OnScreenNotification.Instance?.ShowNotification($"@{username} You can't use that ability!");
            return false;
        }

        // ✅ NEW: Check LEVEL requirement
        if (!CheckLevelRequirement(caster, ability, username))
        {
            return false;
        }

        // Check resource costs
        if (!CanAffordAbility(caster, ability))
        {
            OnScreenNotification.Instance?.ShowNotification($"@{username} Not enough resources to use {ability.abilityName}!");
            return false;
        }

        // Find target
        CombatEntity target = DetermineTarget(ability, targetName, caster);

        if (target == null)
        {
            OnScreenNotification.Instance?.ShowNotification($"@{username} Invalid or no target found!");
            return false;
        }

        if (caster.IsSilenced())
        {
            bool isBasicAttack = ability.manaCost == 0 &&
                                 ability.wrathCost == 0 &&
                                 ability.sneakCost == 0 &&
                                 ability.balanceCost == 0;

            if (!isBasicAttack)
            {
                OnScreenNotification.Instance?.ShowNotification(
                    $"@{username} You are silenced! You can only use basic attacks. " +
                    $"Try !queue {GetDefaultAbility(caster.characterClass)}"
                );
                return false;
            }
        }

        // Queue the action
        QueuedAction action = new QueuedAction
        {
            caster = caster,
            ability = ability,
            target = target,
            confirmed = false
        };

        if (queuedActions.ContainsKey(username))
            queuedActions[username] = action;
        else
            queuedActions.Add(username, action);

        caster.queuedAction = abilityName;

        OnScreenNotification.Instance?.ShowNotification($"@{username} queued {ability.abilityName} → {target.entityName}. Type !confirm to lock it in or !queue <ability> to change.");

        return true;
    }

    public bool ConfirmAction(string userId, string username)
    {
        if (!queuedActions.ContainsKey(username))
        {
            OnScreenNotification.Instance?.ShowNotification($"@{username} You haven't queued an action yet!");
            return false;
        }

        queuedActions[username].confirmed = true;

        CombatEntity caster = queuedActions[username].caster;
        caster.actionConfirmed = true;

        OnScreenNotification.Instance?.ShowNotification($"@{username} ✅ Action confirmed!");

        // Check if all alive players have confirmed
        CheckAllPlayersReady();

        return true;
    }

    void CheckAllPlayersReady()
    {
        if (PvPManager.Instance != null && PvPManager.Instance.pvpActive)
        {

            int confirmedCount = queuedActions.Values.Count(a => a.confirmed);

            if (confirmedCount >= 2)
            {
                OnScreenNotification.Instance?.ShowNotification("All players ready! Executing actions...");

                ExecutePlayerTurn();
            }
        }
        else
        {
            List<CombatEntity> alivePlayers = ExpeditionManager.Instance.GetAllPlayerEntities();

            int confirmedCount = queuedActions.Values.Count(a => a.confirmed);

            if (confirmedCount >= alivePlayers.Count)
            {
                OnScreenNotification.Instance?.ShowNotification("All players ready! Executing actions...");

                ExecutePlayerTurn();
            }
        }
        
    }

    /// <summary>
    /// Check if the caster meets the level requirement for an ability
    /// </summary>
    bool CheckLevelRequirement(CombatEntity caster, AbilityData ability, string username)
    {
        // Get player's ViewerData to check level
        ViewerData viewer = RPGManager.Instance?.GetViewer(caster.userId);

        if (viewer == null)
        {
            Debug.LogWarning($"[Combat] Could not find ViewerData for {username}");
            return true; // Fallback: allow if we can't check
        }

        int playerLevel = viewer.baseStats.level;
        int requiredLevel = ability.levelRequired;

        // Check level requirement
        if (playerLevel < requiredLevel)
        {
            OnScreenNotification.Instance?.ShowNotification(
                $"@{username} {ability.abilityName} requires Level {requiredLevel}! " +
                $"(You are Level {playerLevel})"
            );
            return false;
        }

        return true;
    }

    void AutoSubmitDefaultActions()
    {
        List<CombatEntity> players = ExpeditionManager.Instance.GetAllPlayerEntities();

        foreach (CombatEntity player in players)
        {
            if (!queuedActions.ContainsKey(player.entityName) || !queuedActions[player.entityName].confirmed)
            {
                // Use default ability
                string defaultAbility = GetDefaultAbility(player.characterClass);
                QueueAction(player.userId, player.entityName, defaultAbility);

                if (queuedActions.ContainsKey(player.entityName))
                {
                    queuedActions[player.entityName].confirmed = true;
                }

                OnScreenNotification.Instance?.ShowNotification($"@{player.entityName} auto-used {defaultAbility} (time expired)");
            }
        }
    }

    string GetDefaultAbility(CharacterClass charClass)
    {
        switch (charClass)
        {
            case CharacterClass.Rogue: return "quickstrike";
            case CharacterClass.Fighter: return "strike";
            case CharacterClass.Mage: return "arcaneblast";
            case CharacterClass.Cleric: return "slam";
            case CharacterClass.Ranger: return "quickslice";
            default: return "strike";
        }
    }

    #endregion

    #region Action Execution

    IEnumerator ExecuteAction(QueuedAction action)
    {
        CombatEntity caster = action.caster;
        CombatEntity target = action.target;
        AbilityData ability = action.ability;

        if (caster.isDead) yield break;

        Vector3 originalPosition = caster.transform.position;
        bool didZoom = false;

        if (ability.zoomToCenter)
        {
            yield return StartCoroutine(ZoomToPosition(caster.transform, centerPosition, zoomDuration));
            didZoom = true;
        }

        // ── Stun: entity loses their turn entirely ────────────────────────────────
        if (caster.IsStunned())
        {
            CombatLog.Instance?.AddEntry($"💫 {caster.entityName} is STUNNED and cannot act!");
            OnScreenNotification.Instance?.ShowNotification($"{caster.entityName} is stunned and loses their turn!");
            caster.animator?.SetTrigger("Hit"); // Stagger animation
            yield break;
        }

        // ── Silence: force basic attack if they tried to use an ability ───────────
        if (caster.IsSilenced() && ability.manaCost + ability.wrathCost + ability.sneakCost + ability.balanceCost > 0)
        {
            CombatLog.Instance?.AddEntry($"🔇 {caster.entityName} is SILENCED! Forced to basic attack.");
            OnScreenNotification.Instance?.ShowNotification($"{caster.entityName} is silenced – using basic attack instead!");

            // Swap to the class default basic ability
            string defaultAbilityName = GetDefaultAbility(caster.characterClass);
            AbilityData defaultAbility = AbilityDatabase.Instance?.GetAbility(defaultAbilityName);
            if (defaultAbility != null)
            {
                ability = defaultAbility;
                action = new QueuedAction { caster = caster, ability = ability, target = target, confirmed = true };
            }
        }

        if (target.isDead) yield break;

        // ── Enrage: override target to front enemy ────────────────────────────────
        if (caster.IsEnraged() && ability.canTargetEnemies)
        {
            List<CombatEntity> enemies = ExpeditionManager.Instance?.GetAllEnemyEntities();
            if (enemies != null && enemies.Count > 0)
            {
                target = enemies[0]; // Front-most enemy
                Debug.Log($"[Enrage] {caster.entityName} forced to target {target.entityName}");
            }
        }

        // ── Trigger animation ─────────────────────────────────────────────────────
        caster.animator?.SetTrigger(ability.animationTrigger);

        // ── Projectile ────────────────────────────────────────────────────────────
        if (ability.projectilePrefab != null)
        {
            SpawnProjectile(caster, target, ability);
            yield return new WaitForSeconds(ability.projectileSpeed);
        }
        else
        {
            yield return new WaitForSeconds(0.3f);
        }

        // Spawn ability particle effect at target
        if (ability.particleEffect != null)
        {
            Vector3 particlePosition = target.transform.position;
            GameObject particle = Instantiate(ability.particleEffect, particlePosition, Quaternion.identity);

            ParticleSystem ps = particle.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
                float lifetime = ps.main.duration + ps.main.startLifetime.constantMax;
                Destroy(particle, lifetime);
            }
            else
            {
                Destroy(particle, 2f);
            }
        }

        // ── Execute ability ───────────────────────────────────────────────────────
        if (ability.isAOE)
        {
            List<CombatEntity> targets = GetAOETargets(ability, target);
            foreach (CombatEntity aoeTarget in targets)
            {
                if (!aoeTarget.isDead)
                {
                    CombatCalculations.ExecuteAbility(caster, aoeTarget, ability);
                }
            }
        }
        else
        {
            CombatCalculations.ExecuteAbility(caster, target, ability);
        }

        yield return new WaitForSeconds(0.5f);

        // Zoom back to original position if we zoomed
        if (didZoom && !caster.isDead)
        {
            yield return StartCoroutine(ZoomToPosition(caster.transform, originalPosition, zoomDuration));
        }

        // Track action for XP
        if (ExpeditionManager.Instance.currentExpedition.actionsPerformed.ContainsKey(caster.entityName))
        {
            ExpeditionManager.Instance.currentExpedition.actionsPerformed[caster.entityName]++;
        }
        else
        {
            ExpeditionManager.Instance.currentExpedition.actionsPerformed[caster.entityName] = 1;
        }
    }

    /// <summary>
    /// Smoothly move a transform to a target position with ease-in-out
    /// </summary>
    IEnumerator ZoomToPosition(Transform target, Vector3 destination, float duration)
    {
        Vector3 startPosition = target.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // ✅ Ease-in-out curve (smooth start and stop)
            float easedT = t < 0.5f
                ? 2f * t * t  // Ease in (first half)
                : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f; // Ease out (second half)

            target.position = Vector3.Lerp(startPosition, destination, easedT);
            yield return null;
        }

        // Ensure exact final position
        target.position = destination;
    }

    /// <summary>
    /// Get multiple targets for AOE abilities
    /// </summary>
    List<CombatEntity> GetAOETargets(AbilityData ability, CombatEntity primaryTarget)
    {
        List<CombatEntity> targets = new List<CombatEntity>();

        if (ability.canTargetEnemies)
        {
            // Get all alive enemies
            List<CombatEntity> allEnemies = ExpeditionManager.Instance.GetAllEnemyEntities();

            // Get enemies in position range
            for (int i = 0; i < Mathf.Min(ability.aoETargets, allEnemies.Count); i++)
            {
                CombatEntity enemy = allEnemies[i];

                // Check if enemy is in valid position range
                if (enemy.position >= ability.minTargetPosition &&
                    enemy.position <= ability.maxTargetPosition)
                {
                    targets.Add(enemy);
                }
            }
        }
        else if (ability.canTargetAllies)
        {
            // Get all alive players (for AOE heals/buffs)
            List<CombatEntity> allPlayers = ExpeditionManager.Instance.GetAllPlayerEntities();

            for (int i = 0; i < Mathf.Min(ability.aoETargets, allPlayers.Count); i++)
            {
                targets.Add(allPlayers[i]);
            }
        }

        Debug.Log($"[Combat] AOE targeting {targets.Count} targets for {ability.abilityName}");

        return targets;
    }

    /// <summary>
    /// Spawn and launch a projectile from caster to target
    /// </summary>
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

        Debug.Log($"[Projectile] {caster.entityName} fired projectile at {target.entityName}");
    }

    #endregion

    #region Helper Methods

    bool CanAffordAbility(CombatEntity caster, AbilityData ability)
    {
        switch (caster.characterClass)
        {
            case CharacterClass.Rogue:
                return caster.sneakPoints >= ability.sneakCost;

            case CharacterClass.Fighter:
                if (ability.requiresStance && caster.currentStance != ability.requiredStance)
                    return false;
                return true;

            case CharacterClass.Mage:
                return caster.mana >= ability.manaCost;

            case CharacterClass.Cleric:
                return caster.wrath >= ability.wrathCost;

            case CharacterClass.Ranger:
                bool hasBalance = caster.balance >= ability.balanceCost;
                bool meetsRequirement = CheckBalanceRequirement(caster.balance, ability);
                return hasBalance && meetsRequirement;

            default:
                return true;
        }
    }

    bool CheckBalanceRequirement(int currentBalance, AbilityData ability)
    {
        if (ability.balanceRequirementType == BalanceRequirementType.None)
            return true;

        if (ability.balanceRequirementType == BalanceRequirementType.Above)
            return currentBalance > ability.balanceRequirement;

        if (ability.balanceRequirementType == BalanceRequirementType.Below)
            return currentBalance < ability.balanceRequirement;

        return true;
    }

    CombatEntity DetermineTarget(AbilityData ability, string targetName, CombatEntity caster)
    {
        if (ability.canTargetEnemies)
        {
            string tauntTarget = caster.GetTauntTargetName();
            if (!string.IsNullOrEmpty(tauntTarget))
            {
                // Find the taunter in the player or enemy lists
                List<CombatEntity> allEntities = new List<CombatEntity>();
                allEntities.AddRange(ExpeditionManager.Instance?.GetAllPlayerEntities() ?? new List<CombatEntity>());
                allEntities.AddRange(ExpeditionManager.Instance?.GetAllEnemyEntities() ?? new List<CombatEntity>());

                CombatEntity taunter = allEntities.Find(e => e.entityName == tauntTarget && !e.isDead);
                if (taunter != null)
                {
                    Debug.Log($"[Taunt] {caster.entityName} is taunted → forced to target {taunter.entityName}");
                    return taunter;
                }
            }
        }

        // Self-target
        if (ability.targetType == AbilityTargetType.Self)
            return caster;

        // Specific target name provided
        if (!string.IsNullOrEmpty(targetName))
        {
            if (ability.canTargetAllies)
            {
                // ✅ NEW: Check PvP first for ally targeting
                if (PvPManager.Instance != null && PvPManager.Instance.pvpActive)
                {
                    // In PvP, only valid ally is yourself
                    if (targetName.ToLower() == caster.entityName.ToLower())
                    {
                        return caster;
                    }
                }
                else
                {
                    // Normal expedition ally targeting
                    List<CombatEntity> allies = ExpeditionManager.Instance.GetAllPlayerEntities();
                    CombatEntity ally = allies.Find(a => a.entityName.ToLower() == targetName.ToLower());
                    if (ally != null && !ally.isDead)
                        return ally;
                }
            }

            if (ability.canTargetEnemies)
            {
                // ✅ NEW: Check PvP first for enemy targeting
                if (PvPManager.Instance != null && PvPManager.Instance.pvpActive)
                {
                    // In PvP, get the opponent
                    CombatEntity opponent = GetPvPOpponent(caster);
                    if (opponent != null && opponent.entityName.ToLower() == targetName.ToLower())
                    {
                        return opponent;
                    }
                }
                else
                {
                    // Normal expedition enemy targeting
                    List<CombatEntity> enemies = ExpeditionManager.Instance.GetAllEnemyEntities();
                    CombatEntity enemy = enemies.Find(e => e.entityName.ToLower() == targetName.ToLower());
                    if (enemy != null && !enemy.isDead)
                        return enemy;
                }
            }
        }

        // Default targeting - check PvP mode
        if (ability.canTargetEnemies)
        {
            if (PvPManager.Instance != null && PvPManager.Instance.pvpActive)
            {
                // PvP: target the opponent
                return GetPvPOpponent(caster);
            }
            else
            {
                // PvE: target front-most enemy
                List<CombatEntity> enemies = ExpeditionManager.Instance.GetAllEnemyEntities();
                if (enemies.Count > 0)
                    return enemies[0];
            }
        }

        /// <summary>
        /// Get the opponent in a PvP match
        /// </summary>
        CombatEntity GetPvPOpponent(CombatEntity caster)
        {
            if (PvPManager.Instance == null || !PvPManager.Instance.pvpActive)
                return null;

            PvPMatch match = PvPManager.Instance.currentMatch;
            if (match == null) return null;

            // Determine which fighter the caster is, return the other one
            string opponentUserId;

            if (caster.userId == match.fighter1UserId)
            {
                opponentUserId = match.fighter2UserId;
            }
            else if (caster.userId == match.fighter2UserId)
            {
                opponentUserId = match.fighter1UserId;
            }
            else
            {
                Debug.LogError($"[PvP] {caster.entityName} is not in the current PvP match!");
                return null;
            }

            // Get the opponent's character
            OnScreenCharacter opponentChar = CharacterSpawner.Instance?.GetCharacter(opponentUserId);
            if (opponentChar == null)
            {
                Debug.LogError($"[PvP] Opponent character not found for userId: {opponentUserId}");
                return null;
            }

            CombatEntity opponent = opponentChar.GetComponent<CombatEntity>();
            if (opponent == null)
            {
                Debug.LogError($"[PvP] Opponent has no CombatEntity component!");
                return null;
            }

            return opponent;
        }

        // Default targeting: self for ally abilities
        if (ability.canTargetAllies)
        {
            return caster;
        }

        return null;
    }

    bool CheckWaveCleared()
    {
        List<CombatEntity> enemies = ExpeditionManager.Instance.GetAllEnemyEntities();
        return enemies.Count == 0;
    }

    bool CheckPlayerWipe()
    {
        List<CombatEntity> players = ExpeditionManager.Instance.GetAllPlayerEntities();
        return players.Count == 0;
    }

    #endregion
}

[System.Serializable]
public class QueuedAction
{
    public CombatEntity caster;
    public AbilityData ability;
    public CombatEntity target;
    public bool confirmed;
}
