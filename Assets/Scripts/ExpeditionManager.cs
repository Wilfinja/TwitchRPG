using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages expedition flow: queue, timer, wave progression, character transitions
/// </summary>
public class ExpeditionManager : MonoBehaviour
{
    public static ExpeditionManager Instance;

    [Header("Configuration")]
    public ExpeditionConfig config;

    [Header("Current State")]
    public ExpeditionState currentExpedition;
    public bool expeditionQueued;
    public bool acceptingJoins;

    [Header("Combat Positions")]
    public Vector3[] playerCombatPositions = new Vector3[4]; // Positions 1-4
    public Vector3[] enemyPositions = new Vector3[6]; // Positions 1-6

    [Header("Enemy Prefabs")]
    public GameObject enemyCombatPrefab;

    [Header("References")]
    private List<GameObject> activeEnemies = new List<GameObject>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        currentExpedition = new ExpeditionState();
    }

    void Update()
    {
        // Handle join timer countdown
        if (acceptingJoins && currentExpedition.joinTimer > 0)
        {
            currentExpedition.joinTimer -= Time.deltaTime;

            if (currentExpedition.joinTimer <= 0)
            {
                StartExpedition();
            }
        }
    }

    #region Expedition Setup

    public void QueueExpedition(ExpeditionDifficulty difficulty)
    {
        if (expeditionQueued || currentExpedition.isActive)
        {
            OnScreenNotification.Instance?.ShowNotification("An expedition is already in progress!");
            return;
        }

        currentExpedition = new ExpeditionState
        {
            difficulty = difficulty,
            isActive = false,
            currentWave = 0,
            joinTimer = config.joinTimerDuration
        };

        DifficultyConfig diffConfig = config.GetDifficulty(difficulty);
        currentExpedition.totalWaves = diffConfig.waves.Count;

        expeditionQueued = true;
        acceptingJoins = true;

        OnScreenNotification.Instance?.ShowNotification($"🗡️ {diffConfig.displayName} expedition is now open! Type !enterexpedition <position 1-4> to join! Timer: {config.joinTimerDuration}s");
    }

    public bool AddParticipant(string userId, string username, int requestedPosition)
    {
        if (!acceptingJoins)
        {
            OnScreenNotification.Instance?.ShowError($"{username}: Expedition is not accepting joins right now.");
            return false;
        }

        // Get viewer data to validate
        ViewerData viewer = RPGManager.Instance.GetViewer(userId);
        if (viewer == null || viewer.characterClass == CharacterClass.None)
        {
            OnScreenNotification.Instance?.ShowError($"{username}: Choose a class first with !class");
            return false;
        }

        if (currentExpedition.participantUserIds.Contains(userId))
        {
            OnScreenNotification.Instance?.ShowError($"{username}: You're already in the expedition!");
            return false;
        }

        if (currentExpedition.participantUsernames.Count >= config.maxPartySize)
        {
            OnScreenNotification.Instance?.ShowNotification($"@{username} Expedition is full!");
            return false;
        }

        // Handle position selection
        if (requestedPosition < 1 || requestedPosition > config.maxPartySize)
        {
            OnScreenNotification.Instance?.ShowNotification($"@{username} Invalid position! Choose 1-{config.maxPartySize}");
            return false;
        }

        // Check if position is taken
        if (currentExpedition.participantPositions.ContainsValue(requestedPosition))
        {
            // Find who has this position and swap
            string occupant = currentExpedition.participantPositions.FirstOrDefault(x => x.Value == requestedPosition).Key;
            if (!string.IsNullOrEmpty(occupant))
            {
                int oldPosition = currentExpedition.participantUsernames.Count + 1;
                currentExpedition.participantPositions[occupant] = oldPosition;
                OnScreenNotification.Instance?.ShowNotification($"@{occupant} has been moved to position {oldPosition} to make room for @{username}");
            }
        }

        // Add participant
        currentExpedition.participantUsernames.Add(username);
        currentExpedition.participantUserIds.Add(userId);
        currentExpedition.participantPositions[username] = requestedPosition;
        currentExpedition.actionsPerformed[username] = 0;

        // Start timer on first join
        if (currentExpedition.participantUsernames.Count == 1)
        {
            currentExpedition.joinTimer = config.joinTimerDuration;
        }

        OnScreenNotification.Instance?.ShowNotification($"@{username} joined the expedition in position {requestedPosition}! ({currentExpedition.participantUsernames.Count}/{config.maxPartySize})");

        // Auto-start if full
        if (currentExpedition.participantUsernames.Count >= config.maxPartySize)
        {
            acceptingJoins = false;
            StartExpedition();
        }

        return true;
    }

    public void StartExpedition()
    {
        if (currentExpedition.participantUsernames.Count == 0)
        {
            OnScreenNotification.Instance?.ShowNotification("No one joined the expedition. It has been cancelled.");
            ResetExpedition();
            return;
        }

        acceptingJoins = false;
        currentExpedition.isActive = true;

        OnScreenNotification.Instance?.ShowNotification($"🔥 The expedition begins! {currentExpedition.participantUsernames.Count} brave adventurers embark into danger!");

        // Transition characters to combat mode
        StartCoroutine(TransitionToCombat());
    }

    private IEnumerator TransitionToCombat()
    {
        // 1. FADE OUT non-participants
        List<OnScreenCharacter> allCharacters = CharacterSpawner.Instance?.GetAllCharacters();
        if (allCharacters != null)
        {
            foreach (var character in allCharacters)
            {
                string charUserId = character.GetUserId();

                if (!currentExpedition.participantUserIds.Contains(charUserId))
                {
                    // Fade out non-participants
                    StartCoroutine(FadeCharacter(character, 0.3f));
                }
            }
        }

        yield return new WaitForSeconds(0.5f);

        // 2. MOVE participants to combat positions and add CombatEntity
        foreach (string username in currentExpedition.participantUsernames)
        {
            int position = currentExpedition.participantPositions[username];

            // Find the OnScreenCharacter
            OnScreenCharacter character = CharacterSpawner.Instance?.GetCharacter(
                currentExpedition.participantUserIds[currentExpedition.participantUsernames.IndexOf(username)]
            );

            if (character != null)
            {
                // Enter combat mode - character will walk to position
                character.EnterCombatMode(playerCombatPositions[position - 1]);

                // Add CombatEntity component
                CombatEntity combatEntity = character.gameObject.GetComponent<CombatEntity>();
                if (combatEntity == null)
                {
                    combatEntity = character.gameObject.AddComponent<CombatEntity>();
                }

                string userId = character.GetUserId();
                combatEntity.InitializePlayer(userId, username, position);

                // Create health bar
                CombatUIManager.Instance?.CreateHealthBar(combatEntity);
            }
        }

        // 3. Wait for characters to arrive at positions
        yield return new WaitForSeconds(2f);

        // 4. Start first wave
        StartCoroutine(StartWaveWithDelay(0, 1f));
    }

    private IEnumerator FadeCharacter(OnScreenCharacter character, float targetAlpha)
    {
        SpriteRenderer sr = character.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        Color startColor = sr.color;
        Color targetColor = new Color(startColor.r, startColor.g, startColor.b, targetAlpha);

        float duration = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            sr.color = Color.Lerp(startColor, targetColor, elapsed / duration);
            yield return null;
        }

        sr.color = targetColor;
    }

    #endregion

    #region Wave Management

    IEnumerator StartWaveWithDelay(int waveIndex, float delay)
    {
        yield return new WaitForSeconds(delay);

        currentExpedition.currentWave = waveIndex + 1;
        DifficultyConfig diffConfig = config.GetDifficulty(currentExpedition.difficulty);

        if (waveIndex >= diffConfig.waves.Count)
        {
            // Expedition complete!
            CompleteExpedition(true);
            yield break;
        }

        WaveConfig wave = diffConfig.waves[waveIndex];

        OnScreenNotification.Instance?.ShowNotification($"⚔️ Wave {currentExpedition.currentWave}/{currentExpedition.totalWaves} begins!");

        CombatUIManager.Instance?.UpdateWaveIndicator(currentExpedition.currentWave, currentExpedition.totalWaves);

        // Spawn enemies
        SpawnWaveEnemies(wave);

        yield return new WaitForSeconds(0.5f);

        // Start combat
        CombatTurnManager.Instance?.StartCombat();
    }

    void SpawnWaveEnemies(WaveConfig wave)
    {
        activeEnemies.Clear();

        int enemyCount = Random.Range(wave.minEnemyCount, wave.maxEnemyCount + 1);

        // Spawn regular enemies
        for (int i = 0; i < enemyCount; i++)
        {
            EnemyData enemyData = EnemyDatabase.Instance?.GetRandomEnemy(currentExpedition.difficulty, false);
            if (enemyData != null)
            {
                SpawnEnemy(enemyData, i + 1);
            }
        }

        // Spawn boss if needed
        if (wave.hasBoss)
        {
            for (int i = 0; i < wave.bossCount; i++)
            {
                EnemyData bossData = EnemyDatabase.Instance?.GetRandomEnemy(currentExpedition.difficulty, true);
                if (bossData != null)
                {
                    SpawnEnemy(bossData, enemyCount + i + 1);
                }
            }
        }
    }

    void SpawnEnemy(EnemyData data, int position)
    {
        if (enemyCombatPrefab == null)
        {
            Debug.LogError("[Expedition] Enemy prefab not assigned!");
            return;
        }

        GameObject enemyObj = Instantiate(enemyCombatPrefab, enemyPositions[position - 1], Quaternion.identity);

        // Add CombatEntity component
        CombatEntity entity = enemyObj.GetComponent<CombatEntity>();
        if (entity == null)
        {
            entity = enemyObj.AddComponent<CombatEntity>();
        }

        entity.InitializeEnemy(
            data.enemyName,
            position,
            data.baseHealth,
            data.baseStrength,
            data.baseDexterity,
            data.baseConstitution,
            data.baseIntelligence,
            0, // willpower - not in EnemyData yet
            0, // charisma - not in EnemyData yet
            data.baseDefense
        );

        // Setup visuals
        SpriteRenderer sr = enemyObj.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sprite = data.enemySprite;

        // Setup AI controller
        EnemyCombatController controller = enemyObj.GetComponent<EnemyCombatController>();
        if (controller == null)
        {
            controller = enemyObj.AddComponent<EnemyCombatController>();
        }
        controller.Initialize(data);

        activeEnemies.Add(enemyObj);

        // Create health bar
        CombatUIManager.Instance?.CreateHealthBar(entity);
    }

    public void OnWaveCleared()
    {
        OnScreenNotification.Instance?.ShowNotification($"✅ Wave {currentExpedition.currentWave} cleared!");

        currentExpedition.totalEnemiesDefeated += activeEnemies.Count;

        // Clear enemy objects
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        activeEnemies.Clear();

        // Brief rest period
        StartCoroutine(WaveRestPeriod());
    }

    IEnumerator WaveRestPeriod()
    {
        OnScreenNotification.Instance?.ShowNotification($"Take a breath! Next wave in {config.waveClearDelay} seconds.");

        yield return new WaitForSeconds(config.waveClearDelay);

        // Reset resources for players
        var allPlayers = GetAllPlayerEntities();
        foreach (var player in allPlayers)
        {
            player.RegenerateManaIfMage();
            // TODO: Reset ability cooldowns when implemented
        }

        // Start next wave
        StartCoroutine(StartWaveWithDelay(currentExpedition.currentWave, 0f));
    }

    #endregion

    #region Expedition End

    public void CompleteExpedition(bool victory)
    {
        if (victory)
        {
            OnScreenNotification.Instance?.ShowNotification("🎉 Victory! The expedition is complete!");
            GrantRewards();
        }
        else
        {
            OnScreenNotification.Instance?.ShowNotification("💀 The party has been defeated...");
            GrantPartialRewards();
        }

        StartCoroutine(CleanupExpedition());
    }

    private void GrantRewards()
    {
        DifficultyConfig diffConfig = config.GetDifficulty(currentExpedition.difficulty);

        foreach (string userId in currentExpedition.participantUserIds)
        {
            ViewerData viewer = RPGManager.Instance.GetViewer(userId);
            if (viewer == null) continue;

            // Grant coins
            int coinReward = Random.Range(diffConfig.coinRewardMin, diffConfig.coinRewardMax + 1);
            RPGManager.Instance.AddCoins(userId, coinReward);

            // Grant XP
            int xpReward = 50 * diffConfig.xpMultiplier;
            ExperienceManager.Instance?.AddExperience(userId, xpReward);

            OnScreenNotification.Instance?.ShowSuccess($"{viewer.username} earned {coinReward} coins and {xpReward} XP!");
        }

        Debug.Log("[Expedition] Rewards granted to all participants");
    }

    private void GrantPartialRewards()
    {
        DifficultyConfig diffConfig = config.GetDifficulty(currentExpedition.difficulty);

        foreach (string userId in currentExpedition.participantUserIds)
        {
            ViewerData viewer = RPGManager.Instance.GetViewer(userId);
            if (viewer == null) continue;

            // Partial rewards (50%)
            int coinReward = Random.Range(diffConfig.coinRewardMin / 2, diffConfig.coinRewardMax / 2);
            RPGManager.Instance.AddCoins(userId, coinReward);

            OnScreenNotification.Instance?.ShowInfo($"{viewer.username} earned {coinReward} coins for participating.");
        }
    }

    IEnumerator CleanupExpedition()
    {
        yield return new WaitForSeconds(3f);

        // Remove CombatEntity components and exit combat mode
        foreach (string userId in currentExpedition.participantUserIds)
        {
            OnScreenCharacter character = CharacterSpawner.Instance?.GetCharacter(userId);
            if (character != null)
            {
                // Sync combat data back to ViewerData
                CombatEntity combatEntity = character.GetComponent<CombatEntity>();
                if (combatEntity != null)
                {
                    combatEntity.SyncAllToViewerData();
                    Destroy(combatEntity);
                }

                // Exit combat mode
                character.ExitCombatMode();

                // Fade back in
                StartCoroutine(FadeCharacter(character, 1f));
            }
        }

        // Fade in all non-participants
        List<OnScreenCharacter> allCharacters = CharacterSpawner.Instance?.GetAllCharacters();
        if (allCharacters != null)
        {
            foreach (var character in allCharacters)
            {
                string charUserId = character.GetUserId();

                if (!currentExpedition.participantUserIds.Contains(charUserId))
                {
                    StartCoroutine(FadeCharacter(character, 1f));
                }
            }
        }

        // Despawn all enemies
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        activeEnemies.Clear();

        yield return new WaitForSeconds(1f);

        ResetExpedition();
    }

    void ResetExpedition()
    {
        currentExpedition = new ExpeditionState();
        expeditionQueued = false;
        acceptingJoins = false;
    }

    public void CancelExpedition()
    {
        if (!expeditionQueued && !currentExpedition.isActive)
        {
            OnScreenNotification.Instance?.ShowError("No active expedition to cancel.");
            return;
        }

        OnScreenNotification.Instance?.ShowNotification("Expedition has been cancelled.");

        if (currentExpedition.isActive)
        {
            StartCoroutine(CleanupExpedition());
        }
        else
        {
            ResetExpedition();
        }
    }

    #endregion

    #region Helper Methods

    public CombatEntity GetPlayerEntity(string userId, string username)
    {
        OnScreenCharacter character = CharacterSpawner.Instance?.GetCharacter(userId);
        if (character != null)
        {
            return character.GetComponent<CombatEntity>();
        }
        return null;
    }

    public List<CombatEntity> GetAllPlayerEntities()
    {
        List<CombatEntity> entities = new List<CombatEntity>();

        foreach (string userId in currentExpedition.participantUserIds)
        {
            OnScreenCharacter character = CharacterSpawner.Instance?.GetCharacter(userId);
            if (character != null)
            {
                CombatEntity entity = character.GetComponent<CombatEntity>();
                if (entity != null && !entity.isDead)
                {
                    entities.Add(entity);
                }
            }
        }

        return entities.OrderBy(e => e.position).ToList();
    }

    public List<CombatEntity> GetAllEnemyEntities()
    {
        List<CombatEntity> entities = new List<CombatEntity>();

        foreach (var enemy in activeEnemies)
        {
            if (enemy != null)
            {
                CombatEntity entity = enemy.GetComponent<CombatEntity>();
                if (entity != null && !entity.isDead)
                {
                    entities.Add(entity);
                }
            }
        }

        return entities.OrderBy(e => e.position).ToList();
    }

    public void OnPlayerDeath(string userId)
    {
        ViewerData viewer = RPGManager.Instance.GetViewer(userId);
        if (viewer != null && !currentExpedition.deadParticipants.Contains(viewer.username))
        {
            currentExpedition.deadParticipants.Add(viewer.username);
        }

        // Check for TPK (Total Party Kill)
        if (currentExpedition.deadParticipants.Count >= currentExpedition.participantUsernames.Count)
        {
            CompleteExpedition(false);
        }
        else
        {
            // Shift positions forward
            ShiftPositionsForward();
        }
    }

    public void OnEnemyDeath(CombatEntity enemy)
    {
        Debug.Log($"[Expedition] Enemy {enemy.entityName} defeated");
        // Enemy death is handled by wave completion check
    }

    void ShiftPositionsForward()
    {
        List<CombatEntity> alivePlayers = GetAllPlayerEntities();

        for (int i = 0; i < alivePlayers.Count; i++)
        {
            alivePlayers[i].position = i + 1;

            OnScreenCharacter character = alivePlayers[i].GetComponent<OnScreenCharacter>();
            if (character != null)
            {
                character.EnterCombatMode(playerCombatPositions[i]);
            }
        }
    }

    #endregion
}
