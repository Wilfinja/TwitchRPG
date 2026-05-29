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

    //[Header("Enemy Prefabs")]
    //public GameObject enemyCombatPrefab;

    [Header("References")]
    private List<GameObject> activeEnemies = new List<GameObject>();

    [Header("Spectator Positions")]
    [SerializeField] private float spectatorLeftX = -8f;
    [SerializeField] private float spectatorRightX = 8f;

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

    public void QueueExpedition(ExpeditionDifficulty difficulty, string theme = null)
    {
        if (expeditionQueued || currentExpedition.isActive)
        {
            OnScreenNotification.Instance?.ShowNotification("An expedition is already in progress!");
            return;
        }

        // If no theme specified, pick a random one from config
        if (string.IsNullOrEmpty(theme))
        {
            if (config.themedPools != null && config.themedPools.Count > 0)
            {
                theme = config.themedPools[Random.Range(0, config.themedPools.Count)].themeName;
            }
        }

        currentExpedition = new ExpeditionState
        {
            difficulty = difficulty,
            theme = theme, // NEW
            isActive = false,
            currentWave = 0,
            joinTimer = config.joinTimerDuration
        };

        DifficultyConfig diffConfig = config.GetDifficulty(difficulty);
        currentExpedition.totalWaves = diffConfig.waves.Count;

        expeditionQueued = true;
        acceptingJoins = true;

        // Show theme in notification
        string themeText = !string.IsNullOrEmpty(theme) ? $" [{theme}]" : "";
        OnScreenNotification.Instance?.ShowNotification(
            $"🗡️ {diffConfig.displayName} expedition{themeText} is now open! " +
            $"Type !enterexpedition <position 1-4> to join! Timer: {config.joinTimerDuration}s"
        );
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
        List<OnScreenCharacter> allCharacters = CharacterSpawner.Instance?.GetAllCharacters();
        if (allCharacters == null) yield break;

        // Separate participants from spectators
        List<OnScreenCharacter> spectators = new List<OnScreenCharacter>();
        foreach (var character in allCharacters)
        {
            if (!currentExpedition.participantUserIds.Contains(character.GetUserId()))
                spectators.Add(character);
        }

        // Move spectators to the edges in two groups: left and right
        int leftCount = 0;
        int rightCount = 0;
        float yPos = -10.5f; // match your spawnYPosition

        for (int i = 0; i < spectators.Count; i++)
        {
            OnScreenCharacter spec = spectators[i];

            // Alternate left / right
            float targetX;
            if (i % 2 == 0)
            {
                // Stack slightly inward from the edge so multiple spectators don't overlap
                targetX = spectatorLeftX - (leftCount * 1.2f);
                leftCount++;
            }
            else
            {
                targetX = spectatorRightX + (rightCount * 1.2f);
                rightCount++;
            }

            Vector3 spectatorPos = new Vector3(targetX, yPos, 0f);
            spec.EnterCombatMode(spectatorPos);
        }

        yield return new WaitForSeconds(0.5f);

        // Move participants to their combat positions and add CombatEntity
        foreach (string username in currentExpedition.participantUsernames)
        {
            int position = currentExpedition.participantPositions[username];

            OnScreenCharacter character = CharacterSpawner.Instance?.GetCharacter(
                currentExpedition.participantUserIds[currentExpedition.participantUsernames.IndexOf(username)]
            );

            if (character != null)
            {
                character.EnterCombatMode(playerCombatPositions[position - 1]);

                CombatEntity combatEntity = character.gameObject.GetComponent<CombatEntity>();
                if (combatEntity == null)
                    combatEntity = character.gameObject.AddComponent<CombatEntity>();

                string userId = character.GetUserId();
                combatEntity.InitializePlayer(userId, username, position);

                CombatUIManager.Instance?.CreateHealthBar(combatEntity);
                CombatUIManager.Instance?.CreateClassResourceBar(combatEntity);
            }
        }

        yield return new WaitForSeconds(2f);

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

        // Get the themed pool if available
        ThemedEnemyPool themedPool = null;
        if (!string.IsNullOrEmpty(currentExpedition.theme))
        {
            themedPool = config.GetThemedPool(currentExpedition.theme);

            if (themedPool != null)
            {
                Debug.Log($"[Expedition] Using themed pool: {currentExpedition.theme}");
            }
            else
            {
                Debug.LogWarning($"[Expedition] Theme '{currentExpedition.theme}' not found! Using default EnemyDatabase.");
            }
        }

        // Spawn regular enemies
        for (int i = 0; i < enemyCount; i++)
        {
            EnemyData enemyData = null;

            // Try themed pool first
            if (themedPool != null)
            {
                enemyData = themedPool.GetRandomEnemy(currentExpedition.difficulty, false);
            }

            // Fallback to global EnemyDatabase if themed pool failed
            if (enemyData == null && EnemyDatabase.Instance != null)
            {
                enemyData = EnemyDatabase.Instance.GetRandomEnemy(currentExpedition.difficulty, false);
            }

            if (enemyData != null)
            {
                SpawnEnemy(enemyData, i + 1);
            }
            else
            {
                Debug.LogError($"[Expedition] Failed to get enemy data for wave!");
            }
        }

        // Spawn boss if needed
        if (wave.hasBoss)
        {
            for (int i = 0; i < wave.bossCount; i++)
            {
                EnemyData bossData = null;

                // Try themed pool first
                if (themedPool != null)
                {
                    bossData = themedPool.GetRandomEnemy(currentExpedition.difficulty, true);
                }

                // Fallback to global EnemyDatabase
                if (bossData == null && EnemyDatabase.Instance != null)
                {
                    bossData = EnemyDatabase.Instance.GetRandomEnemy(currentExpedition.difficulty, true);
                }

                if (bossData != null)
                {
                    SpawnEnemy(bossData, enemyCount + i + 1);
                }
            }
        }
    }

    void SpawnEnemy(EnemyData data, int position)
    {
        if (data == null)
        {
            Debug.LogError("[Expedition] Cannot spawn null EnemyData!");
            return;
        }

        if (data.enemyPrefab == null)
        {
            Debug.LogError($"[Expedition] {data.enemyName} has no enemyPrefab assigned! Assign a prefab in the EnemyData ScriptableObject.");
            return;
        }

        if (position < 1 || position > enemyPositions.Length)
        {
            Debug.LogError($"[Expedition] Invalid enemy position {position}! Must be 1-{enemyPositions.Length}");
            return;
        }

        // Instantiate the SPECIFIC prefab for this enemy type
        GameObject enemyObj = Instantiate(data.enemyPrefab, enemyPositions[position - 1], Quaternion.identity);

        // Add or get CombatEntity component
        CombatEntity entity = enemyObj.GetComponent<CombatEntity>();
        if (entity == null)
        {
            entity = enemyObj.AddComponent<CombatEntity>();
        }

        // Initialize combat stats from EnemyData
        entity.InitializeEnemy(
            data.enemyName,
            position,
            data.baseHealth,
            data.baseStrength,
            data.baseDexterity,
            data.baseConstitution,
            data.baseIntelligence,
            0, // willpower - can add to EnemyData later if needed
            0, // charisma - can add to EnemyData later if needed
            data.baseDefense
        );

        // Setup AI controller
        EnemyCombatController controller = enemyObj.GetComponent<EnemyCombatController>();
        if (controller == null)
        {
            controller = enemyObj.AddComponent<EnemyCombatController>();
        }
        controller.Initialize(data);

        // Add to active enemies list
        activeEnemies.Add(enemyObj);

        // Create health bar
        CombatUIManager.Instance?.CreateHealthBar(entity);

        Debug.Log($"[Expedition] Spawned {data.enemyName} at position {position} using prefab: {data.enemyPrefab.name}");
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

            player.TickDownCooldowns();
            player.TickDownCooldowns();
            player.TickDownCooldowns();

        }

        // Start next wave
        StartCoroutine(StartWaveWithDelay(currentExpedition.currentWave, 0f));
    }

    #endregion

    #region Expedition End

    public void CompleteExpedition(bool victory)
    {
        // Stop combat state immediately so the panel clears the combat UI
        // regardless of victory or defeat. CleanupExpedition handles the rest.
        CombatTurnManager.Instance?.EndCombat();

        if (victory)
        {
            OnScreenNotification.Instance?.ShowNotification("🎉 Victory! The expedition is complete!");

            // ✅ NEW: Trigger burst effect
            if (ParticleEffectManager.Instance != null)
            {
                ParticleEffectManager.Instance.TriggerBurst();
            }

            GrantRewards();
        }
        else
        {
            OnScreenNotification.Instance?.ShowNotification("💀 The party has been defeated...");
            GrantPartialRewards();
        }

        CombatUIManager.Instance?.HideCombatUI();

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
        List<OnScreenCharacter> allChars = CharacterSpawner.Instance?.GetAllCharacters();
        if (allChars != null)
        {
            foreach (var character in allChars)
            {
                if (!currentExpedition.participantUserIds.Contains(character.GetUserId()))
                {
                    // Return spectators to normal exploration mode
                    character.ExitCombatMode();
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

        if (CoinSpawner.Instance != null)
        {
            CoinSpawner.Instance.SpawnQueuedCoins();
            Debug.Log("[Expedition] Spawning queued coins after combat");
        }

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
        if (currentExpedition == null)
        {
            OnScreenNotification.Instance?.ShowNotification("No active expedition to cancel.");
            return;
        }

        OnScreenNotification.Instance?.ShowNotification("Expedition has been cancelled.");

        CombatUIManager.Instance?.HideCombatUI(); // ← ADD THIS LINE

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

    /// <summary>
    /// Get all living player entities
    /// </summary>
    public List<CombatEntity> GetAllPlayerEntities()
    {
        List<CombatEntity> players = new List<CombatEntity>();

        foreach (string username in currentExpedition.participantUsernames)
        {
            string userId = currentExpedition.participantUserIds[
                currentExpedition.participantUsernames.IndexOf(username)
            ];

            OnScreenCharacter character = CharacterSpawner.Instance?.GetCharacter(userId);
            if (character != null)
            {
                CombatEntity entity = character.GetComponent<CombatEntity>();
                if (entity != null && !entity.isDead)
                {
                    players.Add(entity);
                }
            }
        }

        return players;
    }

    /// <summary>
    /// Get all living enemy entities
    /// </summary>
    public List<CombatEntity> GetAllEnemyEntities()
    {
        List<CombatEntity> enemies = new List<CombatEntity>();

        foreach (GameObject enemyObj in activeEnemies)
        {
            if (enemyObj == null) continue;

            CombatEntity entity = enemyObj.GetComponent<CombatEntity>();
            if (entity != null && !entity.isDead)
            {
                enemies.Add(entity);
            }
        }

        return enemies;
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

        ShiftEnemyPositionsForward();
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

    /// <summary>
    /// Shift enemies forward when front enemy dies
    /// </summary>
    public void ShiftEnemyPositionsForward()
    {
        List<CombatEntity> aliveEnemies = GetAllEnemyEntities();

        if (aliveEnemies.Count == 0)
        {
            Debug.Log("[Expedition] No enemies left to shift");
            return;
        }

        Debug.Log($"[Expedition] Shifting {aliveEnemies.Count} enemies forward");

        // Sort by current position
        aliveEnemies = aliveEnemies.OrderBy(e => e.position).ToList();

        // Reassign positions starting from 1
        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            int oldPosition = aliveEnemies[i].position;
            int newPosition = i + 1;

            aliveEnemies[i].position = newPosition;

            // Move enemy GameObject to new position
            Transform enemyTransform = aliveEnemies[i].transform;
            if (enemyTransform != null && newPosition <= enemyPositions.Length)
            {
                // Smoothly move to new position
                StartCoroutine(SmoothMoveEnemy(enemyTransform, enemyPositions[newPosition - 1]));
            }

            Debug.Log($"[Expedition] {aliveEnemies[i].entityName}: Position {oldPosition} → {newPosition}");
        }
    }

    /// <summary>
    /// Smoothly move enemy to new position
    /// </summary>
    IEnumerator SmoothMoveEnemy(Transform enemy, Vector3 targetPosition)
    {
        float duration = 0.5f;
        float elapsed = 0f;
        Vector3 startPosition = enemy.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Ease-out interpolation
            t = 1f - Mathf.Pow(1f - t, 3f);

            enemy.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        enemy.position = targetPosition;
    }

    #endregion

    /// <summary>
    /// Get the frontmost (lowest position) living enemy
    /// </summary>
    public CombatEntity GetFrontmostEnemy()
    {
        CombatEntity frontEnemy = null;
        int lowestPosition = int.MaxValue;

        foreach (GameObject enemyObj in activeEnemies)
        {
            if (enemyObj == null) continue;

            CombatEntity entity = enemyObj.GetComponent<CombatEntity>();
            if (entity != null && !entity.isDead && entity.position < lowestPosition)
            {
                frontEnemy = entity;
                lowestPosition = entity.position;
            }
        }

        return frontEnemy;
    }
}
