using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy", menuName = "RPG/Enemy")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName;
    public Sprite enemySprite;
    public EnemyRole role;

    [Header("Stats")]
    public int baseHealth = 50;
    public int baseStrength = 5;
    public int baseDexterity = 5;
    public int baseConstitution = 5;
    public int baseIntelligence = 5;
    public int baseDefense = 0;

    [Header("Scaling")]
    public float healthScaling = 1.2f; // Multiply by difficulty level
    public float damageScaling = 1.1f;

    [Header("Abilities")]
    public List<AbilityData> abilities = new List<AbilityData>();
    public int basicAttackCooldown = 0; // 0 = can use every turn

    [Header("AI Behavior")]
    public bool prioritizeLowHP; // Assassin behavior
    public bool prioritizeHighDefense; // Smart targeting
    public bool prioritizeBackline; // Mastermind behavior
    public bool useAOEWhenPossible; // Boss behavior
    public bool buffAllies; // Controller behavior

    [Header("Loot")]
    public int coinDropMin = 10;
    public int coinDropMax = 50;
    public List<ItemDropChance> possibleDrops = new List<ItemDropChance>();

    [Header("XP")]
    public int xpReward = 25;
}

[System.Serializable]
public class ItemDropChance
{
    public RPGItem item;
    public float dropChance; // 0-1
}

public enum EnemyRole
{
    Minion,      // Attack frontline, simple
    Assassin,    // Target low HP
    Ranged,      // Target high defense/random
    Controller,  // Debuff and buff
    Mastermind,  // Target backline
    Boss         // Use AOE attacks
}

/// <summary>
/// Database of all enemy types
/// </summary>
public class EnemyDatabase : MonoBehaviour
{
    public static EnemyDatabase Instance;

    [Header("Enemy Templates")]
    public List<EnemyData> allEnemies = new List<EnemyData>();

    [Header("Difficulty Pools")]
    public List<EnemyData> easyEnemies = new List<EnemyData>();
    public List<EnemyData> mediumEnemies = new List<EnemyData>();
    public List<EnemyData> hardEnemies = new List<EnemyData>();
    public List<EnemyData> deadlyEnemies = new List<EnemyData>();
    public List<EnemyData> bossEnemies = new List<EnemyData>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public EnemyData GetRandomEnemy(ExpeditionDifficulty difficulty, bool isBoss = false)
    {
        List<EnemyData> pool;

        if (isBoss)
        {
            pool = bossEnemies;
        }
        else
        {
            switch (difficulty)
            {
                case ExpeditionDifficulty.Easy:
                    pool = easyEnemies;
                    break;
                case ExpeditionDifficulty.Medium:
                    pool = mediumEnemies;
                    break;
                case ExpeditionDifficulty.Hard:
                    pool = hardEnemies;
                    break;
                case ExpeditionDifficulty.Deadly:
                    pool = deadlyEnemies;
                    break;
                default:
                    pool = easyEnemies;
                    break;
            }
        }

        if (pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    public EnemyData GetEnemyByName(string name)
    {
        return allEnemies.Find(e => e.enemyName.ToLower() == name.ToLower());
    }
}
