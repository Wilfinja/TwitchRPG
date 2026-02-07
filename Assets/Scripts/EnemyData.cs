using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that defines an enemy type
/// Create via: Right-click → Create → RPG → Enemy
/// </summary>
[CreateAssetMenu(fileName = "New Enemy", menuName = "RPG/Enemy")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyName;
    [Tooltip("The visual prefab for this enemy (with animations, sprite, etc.)")]
    public GameObject enemyPrefab;
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

/// <summary>
/// Defines a possible item drop with a chance
/// </summary>
[System.Serializable]
public class ItemDropChance
{
    public RPGItem item;
    [Range(0f, 1f)]
    public float dropChance; // 0-1
}

/// <summary>
/// Enemy role determines AI behavior
/// </summary>
public enum EnemyRole
{
    Minion,      // Attack frontline, simple
    Assassin,    // Target low HP
    Ranged,      // Target high defense/random
    Controller,  // Debuff and buff
    Mastermind,  // Target backline
    Boss         // Use AOE attacks
}
