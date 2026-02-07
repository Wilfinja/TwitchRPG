using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Expedition Config", menuName = "RPG/Expedition Config")]
public class ExpeditionConfig : ScriptableObject
{
    [Header("Timing")]
    public float joinTimerDuration = 60f;
    public float turnTimerDuration = 45f;
    public float waveClearDelay = 5f;

    [Header("Party Settings")]
    public int maxPartySize = 4;
    public int maxEnemyPositions = 6;

    [Header("Difficulty Configurations")]
    public List<DifficultyConfig> difficulties = new List<DifficultyConfig>();

    [Header("Themed Enemy Pools")]
    [Tooltip("Define themed enemy pools (Forest, Graveyard, Dungeon, etc.)")]
    public List<ThemedEnemyPool> themedPools = new List<ThemedEnemyPool>();

    public DifficultyConfig GetDifficulty(ExpeditionDifficulty difficulty)
    {
        return difficulties.Find(d => d.difficulty == difficulty);
    }

    public ThemedEnemyPool GetThemedPool(string themeName)
    {
        return themedPools.Find(p => p.themeName.ToLower() == themeName.ToLower());
    }
}

[System.Serializable]
public class DifficultyConfig
{
    [Header("Identity")]
    public ExpeditionDifficulty difficulty;
    public string displayName;

    [Header("Wave Structure")]
    public List<WaveConfig> waves = new List<WaveConfig>();

    [Header("Rewards")]
    public int coinRewardMin = 100;
    public int coinRewardMax = 300;
    public int xpMultiplier = 1;

    [Header("Guaranteed Loot")]
    public int guaranteedCommonItems = 0;
    public int guaranteedUncommonItems = 1;
    public int guaranteedRareItems = 0;
    public int guaranteedEpicItems = 0;
    public int guaranteedLegendaryItems = 0;

    [Header("Bonus Loot Chances")]
    public float bonusUncommonChance = 0.2f;
    public float bonusRareChance = 0.1f;
    public float bonusEpicChance = 0.05f;
    public float bonusLegendaryChance = 0.01f;
}

[System.Serializable]
public class WaveConfig
{
    public int waveNumber;
    public int minEnemyCount = 3;
    public int maxEnemyCount = 3;
    public bool hasBoss;
    public int bossCount = 0;
}

/// <summary>
/// Themed enemy pool - defines enemies for a specific location/theme
/// Examples: Forest, Graveyard, Dungeon, Desert, Ice Cave
/// </summary>
[System.Serializable]
public class ThemedEnemyPool
{
    [Header("Theme Identity")]
    public string themeName; // "Forest", "Graveyard", "Dungeon"
    [TextArea(2, 4)]
    public string themeDescription; // Flavor text

    [Header("Enemy Pools by Difficulty")]
    public List<EnemyData> easyEnemies = new List<EnemyData>();
    public List<EnemyData> mediumEnemies = new List<EnemyData>();
    public List<EnemyData> hardEnemies = new List<EnemyData>();
    public List<EnemyData> deadlyEnemies = new List<EnemyData>();
    public List<EnemyData> bossEnemies = new List<EnemyData>();

    /// <summary>
    /// Get enemies from this theme for a specific difficulty
    /// </summary>
    public List<EnemyData> GetEnemiesForDifficulty(ExpeditionDifficulty difficulty)
    {
        switch (difficulty)
        {
            case ExpeditionDifficulty.Easy: return easyEnemies;
            case ExpeditionDifficulty.Medium: return mediumEnemies;
            case ExpeditionDifficulty.Hard: return hardEnemies;
            case ExpeditionDifficulty.Deadly: return deadlyEnemies;
            default: return easyEnemies;
        }
    }

    /// <summary>
    /// Get a random enemy from this themed pool
    /// </summary>
    public EnemyData GetRandomEnemy(ExpeditionDifficulty difficulty, bool isBoss = false)
    {
        List<EnemyData> pool = isBoss ? bossEnemies : GetEnemiesForDifficulty(difficulty);

        if (pool.Count == 0)
        {
            Debug.LogWarning($"[Theme:{themeName}] No enemies for {difficulty} (boss:{isBoss})");
            return null;
        }

        return pool[Random.Range(0, pool.Count)];
    }
}

public enum ExpeditionDifficulty
{
    Easy,
    Medium,
    Hard,
    Deadly
}

[System.Serializable]
public class ExpeditionState
{
    public ExpeditionDifficulty difficulty;
    public string theme; // NEW: "Forest", "Graveyard", etc.
    public List<string> participantUsernames = new List<string>();
    public List<string> participantUserIds = new List<string>();
    public Dictionary<string, int> participantPositions = new Dictionary<string, int>();
    public int currentWave;
    public bool isActive;
    public bool isInCombat;
    public float joinTimer;
    public int totalWaves;
    public Dictionary<string, int> actionsPerformed = new Dictionary<string, int>();
    public int totalEnemiesDefeated;
    public List<string> deadParticipants = new List<string>();
}
