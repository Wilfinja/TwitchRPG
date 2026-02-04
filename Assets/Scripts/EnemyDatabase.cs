using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Database of all enemy types with difficulty-based pools
/// Singleton that provides enemy lookup and random selection
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

        Debug.Log($"[EnemyDatabase] Initialized with {allEnemies.Count} total enemies");
    }

    /// <summary>
    /// Get a random enemy from the appropriate difficulty pool
    /// </summary>
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

        if (pool.Count == 0)
        {
            Debug.LogWarning($"[EnemyDatabase] No enemies in pool for {difficulty} (isBoss: {isBoss})");
            return null;
        }

        return pool[Random.Range(0, pool.Count)];
    }

    /// <summary>
    /// Get a specific enemy by name
    /// </summary>
    public EnemyData GetEnemyByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        return allEnemies.Find(e => e.enemyName.ToLower() == name.ToLower());
    }

    /// <summary>
    /// Get all enemies of a specific role
    /// </summary>
    public List<EnemyData> GetEnemiesByRole(EnemyRole role)
    {
        return allEnemies.FindAll(e => e.role == role);
    }
}
