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
            Debug.Log($"[Combat] → Executing buff: {action.ability.abilityName}");
            yield return StartCoroutine(ExecuteAction(action));
            yield return new WaitForSeconds(0.5f);
        }

        // Execute heals
        Debug.Log("[Combat] Executing HEAL actions...");
        foreach (QueuedAction action in healActions)
        {
            Debug.Log($"[Combat] → Executing heal: {action.ability.abilityName}");
            yield return StartCoroutine(ExecuteAction(action));
            yield return new WaitForSeconds(0.5f);
        }

        // Execute damage
        Debug.Log("[Combat] Executing DAMAGE actions...");
        foreach (QueuedAction action in damageActions)
        {
            Debug.Log($"[Combat] → Executing damage: {action.ability.abilityName}");
            yield return StartCoroutine(ExecuteAction(action));
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("[Combat] All actions executed!");

        // Process status effects for all players
        foreach (CombatEntity player in players)
        {
            player.ProcessStatusEffects();
        }

        // Check if wave is cleared
        if (CheckWaveCleared())
        {
            Debug.Log("[Combat] WAVE CLEARED!");

            // Check if this is PvP
            if (PvPManager.Instance != null && PvPManager.Instance.pvpActive)
            {
                // PvP match end - determine winner
                List<CombatEntity> pvpplayers = ExpeditionManager.Instance.GetAllPlayerEntities();
                if (pvpplayers.Count > 0)
                {
                    PvPManager.Instance.OnPvPMatchEnd(pvpplayers[0].userId);
                }
                combatActive = false;
            }
            else
            {
                // Expedition wave cleared
                ExpeditionManager.Instance.OnWaveCleared();
            }
            yield break;
        }

        Debug.Log("[Combat] Wave not cleared, starting enemy turn...");

        // Enemy turn
        yield return StartCoroutine(ExecuteEnemyTurn());

        // Check for player wipe
        if (CheckPlayerWipe())
        {
            Debug.Log("[Combat] PLAYER WIPE!");

            // Check if this is PvP
            if (PvPManager.Instance != null && PvPManager.Instance.pvpActive)
            {
                // PvP match end - determine winner (the one still alive)
                List<CombatEntity> allPlayers = ExpeditionManager.Instance.GetAllPlayerEntities();
                List<CombatEntity> enemies = ExpeditionManager.Instance.GetAllEnemyEntities();

                // In PvP, "enemies" are actually the other player
                if (allPlayers.Count > 0)
                {
                    PvPManager.Instance.OnPvPMatchEnd(allPlayers[0].userId);
                }
                else if (enemies.Count > 0)
                {
                    // The "enemy" player won
                    PvPManager.Instance.OnPvPMatchEnd(enemies[0].userId);
                }

                combatActive = false;
            }
            else
            {
                // Expedition failure
                ExpeditionManager.Instance.CompleteExpedition(false);
            }
            yield break;
        }

        Debug.Log("[Combat] Starting next player turn...");

        // Start next player turn
        StartPlayerTurn();
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
        List<CombatEntity> alivePlayers = ExpeditionManager.Instance.GetAllPlayerEntities();

        int confirmedCount = queuedActions.Values.Count(a => a.confirmed);

        if (confirmedCount >= alivePlayers.Count)
        {
            OnScreenNotification.Instance?.ShowNotification("All players ready! Executing actions...");

            ExecutePlayerTurn();
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
            case CharacterClass.Rogue: return "quickcut";
            case CharacterClass.Fighter: return "strike";
            case CharacterClass.Mage: return "bolt";
            case CharacterClass.Cleric: return "crush";
            case CharacterClass.Ranger: return "shot";
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

        if (caster.isDead || target.isDead)
            yield break;

        // Trigger animation
        caster.animator?.SetTrigger(ability.animationTrigger);

        yield return new WaitForSeconds(0.3f);

        // ✅ NEW: Check if AOE ability
        if (ability.isAOE)
        {
            // Get multiple targets
            List<CombatEntity> targets = GetAOETargets(ability, target);

            // Execute ability on each target
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
            // Single target (existing code)
            CombatCalculations.ExecuteAbility(caster, target, ability);
        }

        // Track action for XP
        if (ExpeditionManager.Instance.currentExpedition.actionsPerformed.ContainsKey(caster.entityName))
        {
            ExpeditionManager.Instance.currentExpedition.actionsPerformed[caster.entityName]++;
        }
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
        // Self-target
        if (ability.targetType == AbilityTargetType.Self)
            return caster;

        // Specific target name provided
        if (!string.IsNullOrEmpty(targetName))
        {
            if (ability.canTargetAllies)
            {
                List<CombatEntity> allies = ExpeditionManager.Instance.GetAllPlayerEntities();
                CombatEntity ally = allies.Find(a => a.entityName.ToLower() == targetName.ToLower());
                if (ally != null && !ally.isDead)
                    return ally;
            }

            if (ability.canTargetEnemies)
            {
                List<CombatEntity> enemies = ExpeditionManager.Instance.GetAllEnemyEntities();
                CombatEntity enemy = enemies.Find(e => e.entityName.ToLower() == targetName.ToLower());
                if (enemy != null && !enemy.isDead)
                    return enemy;
            }
        }

        // Default targeting: front-most enemy
        if (ability.canTargetEnemies)
        {
            List<CombatEntity> enemies = ExpeditionManager.Instance.GetAllEnemyEntities();
            if (enemies.Count > 0)
                return enemies[0]; // Front-most
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
