using NUnit.Framework.Interfaces;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages expedition flow: queue, timer, wave progression
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

    [Header("Spawn Positions")]
    public Transform playerCombatParent;
    public Transform enemyCombatParent;
    public Vector3[] playerPositions = new Vector3[4]; // Positions 1-4
    public Vector3[] enemyPositions = new Vector3[6]; // Positions 1-6

    [Header("Prefabs")]
    public GameObject playerCombatPrefab;
    public GameObject enemyCombatPrefab;

    [Header("References")]
    private Dictionary<string, GameObject> activePlayers = new Dictionary<string, GameObject>();
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

        // Spawn player characters
        SpawnPlayerCharacters();

        // Start first wave
        StartCoroutine(StartWaveWithDelay(0, 2f));
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

        // Spawn enemies
        SpawnWaveEnemies(wave);

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
            EnemyData enemyData = EnemyDatabase.Instance.GetRandomEnemy(currentExpedition.difficulty, false);
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
                EnemyData bossData = EnemyDatabase.Instance.GetRandomEnemy(currentExpedition.difficulty, true);
                if (bossData != null)
                {
                    SpawnEnemy(bossData, enemyCount + i + 1);
                }
            }
        }
    }

    void SpawnEnemy(EnemyData data, int position)
    {
        GameObject enemyObj = Instantiate(enemyCombatPrefab, enemyCombatParent);
        enemyObj.transform.position = enemyPositions[position - 1];

        CombatEntity entity = enemyObj.GetComponent<CombatEntity>();
        entity.isPlayer = false;
        entity.entityName = data.enemyName;
        entity.position = position;
        entity.maxHealth = data.baseHealth;
        entity.currentHealth = data.baseHealth;
        entity.strength = data.baseStrength;
        entity.dexterity = data.baseDexterity;
        entity.constitution = data.baseConstitution;
        entity.intelligence = data.baseIntelligence;
        entity.defense = data.baseDefense;

        // Setup visuals
        SpriteRenderer sr = enemyObj.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sprite = data.enemySprite;

        activeEnemies.Add(enemyObj);

        // Create health bar
        CombatUIManager.Instance?.CreateHealthBar(entity);
    }

    public void OnWaveCleared()
    {
        OnScreenNotification.Instance?.ShowNotification($"✅ Wave {currentExpedition.currentWave} cleared!");

        // Brief rest period
        StartCoroutine(WaveRestPeriod());
    }

    IEnumerator WaveRestPeriod()
    {
        OnScreenNotification.Instance?.ShowNotification($"Take a breath! Next wave in {config.waveClearDelay} seconds. You can use items with !useitem <name>");

        yield return new WaitForSeconds(config.waveClearDelay);

        // Reset cooldowns for players
        foreach (var player in activePlayers.Values)
        {
            CombatEntity entity = player.GetComponent<CombatEntity>();
            // TODO: Reset cooldowns when ability cooldown system is implemented
        }

        // Start next wave
        StartCoroutine(StartWaveWithDelay(currentExpedition.currentWave, 0f));
    }

    #endregion

    #region Player Spawning

    void SpawnPlayerCharacters()
    {
        activePlayers.Clear();

        foreach (string username in currentExpedition.participantUsernames)
        {
            ViewerData viewer = ViewerDataManager.Instance.GetOrCreateViewer(username);
            int position = currentExpedition.participantPositions[username];

            GameObject playerObj = Instantiate(playerCombatPrefab, playerCombatParent);
            playerObj.transform.position = playerPositions[position - 1];

            CombatEntity entity = playerObj.GetComponent<CombatEntity>();
            entity.isPlayer = true;
            entity.entityName = username;
            entity.position = position;
            entity.characterClass = viewer.characterClass;

            // Set stats from viewer data
            entity.maxHealth = viewer.maxHealth;
            entity.currentHealth = viewer.currentHealth;
            entity.strength = viewer.strength;
            entity.dexterity = viewer.dexterity;
            entity.constitution = viewer.constitution;
            entity.intelligence = viewer.intelligence;
            entity.defense = CalculateDefense(viewer);

            // Initialize class resources
            InitializeClassResources(entity);

            // Setup visuals
            OnScreenCharacter osc = playerObj.GetComponent<OnScreenCharacter>();
            if (osc != null)
            {
                osc.characterClass = viewer.characterClass;
                osc.UpdateVisuals();
            }

            activePlayers[username] = playerObj;

            // Create health bar
            CombatUIManager.Instance?.CreateHealthBar(entity);
        }
    }

    int CalculateDefense(ViewerData viewer)
    {
        int defense = 0;

        // Add defense from equipment
        foreach (ItemData item in viewer.equippedItems)
        {
            if (item != null)
                defense += item.defense;
        }

        return defense;
    }

    void InitializeClassResources(CombatEntity entity)
    {
        switch (entity.characterClass)
        {
            case CharacterClass.Rogue:
                entity.sneakPoints = 0;
                break;
            case CharacterClass.Fighter:
                entity.currentStance = FighterStance.None;
                entity.stanceCooldown = 0;
                break;
            case CharacterClass.Mage:
                entity.mana = 100;
                break;
            case CharacterClass.Cleric:
                entity.wrath = 0;
                break;
            case CharacterClass.Ranger:
                entity.balance = 0;
                entity.comboCounter = 0;
                entity.lastAttackWasMelee = false;
                break;
        }
    }

    #endregion

    #region Expedition End

    public void CompleteExpedition(bool victory)
    {
        if (victory)
        {
            OnScreenNotification.Instance?.ShowNotification("🎉 Victory! The expedition is complete!");
            // TODO: Grant rewards
        }
        else
        {
            OnScreenNotification.Instance?.ShowNotification("💀 The party has been defeated...");
            // TODO: Grant partial rewards based on progress
        }

        StartCoroutine(CleanupExpedition());
    }

    IEnumerator CleanupExpedition()
    {
        yield return new WaitForSeconds(3f);

        // Despawn all combat entities
        foreach (var player in activePlayers.Values)
        {
            if (player != null) Destroy(player);
        }

        foreach (var enemy in activeEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }

        activePlayers.Clear();
        activeEnemies.Clear();

        ResetExpedition();
    }

    void ResetExpedition()
    {
        currentExpedition = new ExpeditionState();
        expeditionQueued = false;
        acceptingJoins = false;
    }

    #endregion

    #region Helper Methods

    public CombatEntity GetPlayerEntity(string username)
    {
        if (activePlayers.TryGetValue(username, out GameObject playerObj))
        {
            return playerObj.GetComponent<CombatEntity>();
        }
        return null;
    }

    public List<CombatEntity> GetAllPlayerEntities()
    {
        List<CombatEntity> entities = new List<CombatEntity>();
        foreach (var player in activePlayers.Values)
        {
            if (player != null)
            {
                CombatEntity entity = player.GetComponent<CombatEntity>();
                if (entity != null && !entity.isDead)
                    entities.Add(entity);
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
                    entities.Add(entity);
            }
        }
        return entities.OrderBy(e => e.position).ToList();
    }

    public void OnPlayerDeath(string username)
    {
        if (!currentExpedition.deadParticipants.Contains(username))
        {
            currentExpedition.deadParticipants.Add(username);
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

    void ShiftPositionsForward()
    {
        List<CombatEntity> alivePlayers = GetAllPlayerEntities();

        for (int i = 0; i < alivePlayers.Count; i++)
        {
            alivePlayers[i].position = i + 1;
            alivePlayers[i].transform.position = playerPositions[i];
        }
    }

    #endregion
}
