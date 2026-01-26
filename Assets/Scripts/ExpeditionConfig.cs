using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Expedition Config", menuName = "RPG/Expedition Config")]
public class ExpeditionConfig : ScriptableObject
{
    [Header("Timing")]
    public float joinTimerDuration = 60f; // Seconds to wait after first join
    public float turnTimerDuration = 45f; // Seconds for all players to input actions
    public float waveClearDelay = 5f; // Seconds between waves

    [Header("Party Settings")]
    public int maxPartySize = 4;
    public int maxEnemyPositions = 6;

    [Header("Difficulty Configurations")]
    public List<DifficultyConfig> difficulties = new List<DifficultyConfig>();

    public DifficultyConfig GetDifficulty(ExpeditionDifficulty difficulty)
    {
        return difficulties.Find(d => d.difficulty == difficulty);
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

public enum ExpeditionDifficulty
{
    Easy,
    Medium,
    Hard,
    Deadly
}

/// <summary>
/// Represents the current state of an active expedition
/// </summary>
[System.Serializable]
public class ExpeditionState
{
    public ExpeditionDifficulty difficulty;
    public List<string> participantUsernames = new List<string>();
    public List<string> participantUserIds = new List<string>(); // ADDED - was missing
    public Dictionary<string, int> participantPositions = new Dictionary<string, int>();
    public int currentWave;
    public bool isActive;
    public bool isInCombat;
    public float joinTimer;
    public int totalWaves;

    // Combat tracking
    public Dictionary<string, int> actionsPerformed = new Dictionary<string, int>();
    public int totalEnemiesDefeated;
    public List<string> deadParticipants = new List<string>();
}
