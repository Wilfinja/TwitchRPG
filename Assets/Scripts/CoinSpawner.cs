using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CoinSpawner : MonoBehaviour
{
    [Header("Coin Prefabs")]
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private GameObject goldenCoinPrefab;
    [SerializeField] private GameObject redGemPrefab;
    [SerializeField] private GameObject greenGemprefab;
    [SerializeField] private GameObject blueGemPrefab;
    [SerializeField] private GameObject purpleGemPrefab;

    [Header("Spawn Area")]
    [SerializeField] private float spawnWidth = 19.2f;
    [SerializeField] private float spawnHeight = 12f;
    [SerializeField] private Transform spawnParent;

    [Header("Coin Behavior")]
    [SerializeField] private float coinLifetime = 5f;
    [SerializeField] private float dissolveTime = 2f;
    [SerializeField] private float spawnDelay = 0.02f;

    [Header("Physics")]
    [SerializeField] private float minHorizontalForce = -2f;
    [SerializeField] private float maxHorizontalForce = 2f;
    [SerializeField] private float minVerticalForce = 4f;
    [SerializeField] private float maxVerticalForce = 6f;

    [Header("Combat Queuing")]
    private Queue<int> queuedCoinSpawns = new Queue<int>();

    private int totalCoinsSpawned = 0;

    public void TestSpawn()
    {
        SpawnCoins(5);
    }

    public void SpawnCoins(int count)
    {
        // Check if expedition or PvP is active
        bool combatActive = (ExpeditionManager.Instance != null && ExpeditionManager.Instance.currentExpedition.isActive) ||
                           (PvPManager.Instance != null && PvPManager.Instance.pvpActive);

        if (combatActive)
        {
            // Queue coins to spawn later
            queuedCoinSpawns.Enqueue(count);
            Debug.Log($"[CoinSpawner] Queued {count} coins to spawn after combat ends");
        }
        else
        {
            // Spawn immediately
            StartCoroutine(SpawnCoinsRoutine(count, 0));
        }
    }

    /// <summary>
    /// Called by ExpeditionManager or PvPManager when combat ends
    /// </summary>
    public void SpawnQueuedCoins()
    {
        if (queuedCoinSpawns.Count == 0) return;

        Debug.Log($"[CoinSpawner] Spawning {queuedCoinSpawns.Count} queued coin batches");

        while (queuedCoinSpawns.Count > 0)
        {
            int count = queuedCoinSpawns.Dequeue();
            StartCoroutine(SpawnCoinsRoutine(count, 0));
        }
    }

    public void SpawnGoldenCoin()
    {
        StartCoroutine(SpawnCoinsRoutine(1, 1));
    }

    private IEnumerator SpawnCoinsRoutine(int count, int type)
    {
        for (int i = 0; i < count; i++)
        {
            totalCoinsSpawned++;

            // Default
            type = 0;

            // Determine coin type based on TOTAL coins spawned
            if (totalCoinsSpawned % 1000 == 0)
                type = 5;                   // Purple
            else if (totalCoinsSpawned % 500 == 0)
                type = 4;                   // Red
            else if (totalCoinsSpawned % 100 == 0)
                type = 3;                   // Green
            else if (totalCoinsSpawned % 50 == 0)
                type = 2;                   // Blue
            else if (totalCoinsSpawned % 10 == 0)
                type = 1;                   // Golden

            SpawnSingleCoin(type);
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    private void SpawnSingleCoin(int coinType)
    {
        float randomX = Random.Range(-spawnWidth / 2f, spawnWidth / 2f);
        Vector3 spawnPos = new Vector3(randomX, spawnHeight, 0f);

        GameObject prefab;
        Coin.CoinType type;

        switch (coinType)
        {
            case 1:
                prefab = goldenCoinPrefab;
                type = Coin.CoinType.Golden;
                break;

            case 2:
                prefab = blueGemPrefab;
                type = Coin.CoinType.BlueGem;
                break;

            case 3:
                prefab = greenGemprefab;
                type = Coin.CoinType.GreenGem;
                break;

            case 4:
                prefab = redGemPrefab;
                type = Coin.CoinType.RedGem;
                break;

            case 5:
                prefab = purpleGemPrefab;
                type = Coin.CoinType.PurpleGem;
                break;

            default:
                prefab = coinPrefab;
                type = Coin.CoinType.Normal;
                break;
        }

        GameObject coinGO = Instantiate(prefab, spawnPos, Quaternion.identity, spawnParent);

        Coin coin = coinGO.GetComponent<Coin>();
        if (coin == null)
            coin = coinGO.AddComponent<Coin>();

        // Set coin type
        coin.SetCoinType(type);

        // Random initial force
        float forceX = Random.Range(minHorizontalForce, maxHorizontalForce);
        float forceY = Random.Range(minVerticalForce, maxVerticalForce);
        coin.Initialize(coinLifetime, dissolveTime, new Vector2(forceX, forceY));
    }

    public void StartCoinRain(int count)
    {
        SpawnCoins(count);
    }
}