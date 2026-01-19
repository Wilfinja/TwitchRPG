using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemDropManager : MonoBehaviour
{
    [Header("Item Drop Prefabs")]
    [Tooltip("Assign a GameObject with a sprite renderer. Can be a simple square placeholder.")]
    [SerializeField] private GameObject commonItemPrefab;

    [Tooltip("Optional: Different prefabs per rarity. If null, uses commonItemPrefab.")]
    [SerializeField] private GameObject uncommonItemPrefab;
    [SerializeField] private GameObject rareItemPrefab;
    [SerializeField] private GameObject epicItemPrefab;
    [SerializeField] private GameObject legendaryItemPrefab;
    [SerializeField] private GameObject uniqueItemPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnHeight = 12f;
    [SerializeField] private Transform dropParent;

    [Header("Queue Settings")]
    [SerializeField] private float queueDelay = 0.3f;
    [SerializeField] private bool queueMultipleDrops = true;

    private Queue<ItemDropRequest> dropQueue = new Queue<ItemDropRequest>();
    private bool isProcessingQueue = false;

    private static ItemDropManager _instance;
    public static ItemDropManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ItemDropManager>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Update()
    {
        if (!isProcessingQueue && dropQueue.Count > 0)
        {
            StartCoroutine(ProcessDropQueue());
        }
    }

    /// <summary>
    /// Spawn an item drop visual for a specific user
    /// </summary>
    public void SpawnItemDrop(string userId, RPGItem item)
    {
        if (item == null)
        {
            Debug.LogWarning("[ItemDrop] Cannot spawn null item!");
            return;
        }

        // Check if character is on screen
        OnScreenCharacter character = CharacterSpawner.Instance?.GetCharacter(userId);

        if (character == null)
        {
            Debug.Log($"[ItemDrop] {userId} is not on screen, skipping visual drop");
            return;
        }

        Vector3 targetPosition = character.transform.position;

        // Create drop request
        ItemDropRequest request = new ItemDropRequest
        {
            targetPosition = targetPosition,
            itemRarity = item.rarity,
            itemName = item.itemName
        };

        if (queueMultipleDrops)
        {
            dropQueue.Enqueue(request);
        }
        else
        {
            // Immediate spawn (no queue)
            SpawnDropVisual(request);
        }
    }

    /// <summary>
    /// Spawn item drop at a specific world position
    /// </summary>
    public void SpawnItemDropAtPosition(Vector3 targetPosition, RPGItem item)
    {
        if (item == null)
        {
            Debug.LogWarning("[ItemDrop] Cannot spawn null item!");
            return;
        }

        ItemDropRequest request = new ItemDropRequest
        {
            targetPosition = targetPosition,
            itemRarity = item.rarity,
            itemName = item.itemName
        };

        if (queueMultipleDrops)
        {
            dropQueue.Enqueue(request);
        }
        else
        {
            SpawnDropVisual(request);
        }
    }

    private IEnumerator ProcessDropQueue()
    {
        isProcessingQueue = true;

        while (dropQueue.Count > 0)
        {
            ItemDropRequest request = dropQueue.Dequeue();
            SpawnDropVisual(request);

            // Wait before processing next item
            yield return new WaitForSeconds(queueDelay);
        }

        isProcessingQueue = false;
    }

    private void SpawnDropVisual(ItemDropRequest request)
    {
        // Get the appropriate prefab based on rarity
        GameObject prefabToUse = GetPrefabForRarity(request.itemRarity);

        if (prefabToUse == null)
        {
            Debug.LogError($"[ItemDrop] No prefab assigned for rarity {request.itemRarity}!");
            return;
        }

        // Calculate spawn position (above target)
        Vector3 spawnPosition = new Vector3(
            request.targetPosition.x,
            spawnHeight,
            request.targetPosition.z
        );

        // Instantiate the drop visual
        GameObject dropObj = Instantiate(prefabToUse, spawnPosition, Quaternion.identity, dropParent);

        // Add ItemDropVisual component if not present
        ItemDropVisual dropVisual = dropObj.GetComponent<ItemDropVisual>();
        if (dropVisual == null)
        {
            dropVisual = dropObj.AddComponent<ItemDropVisual>();
        }

        // Initialize the animation
        dropVisual.Initialize(spawnPosition, request.targetPosition, request.itemRarity, request.itemName);

        Debug.Log($"[ItemDrop] Spawned {request.itemName} [{request.itemRarity}] drop visual");
    }

    private GameObject GetPrefabForRarity(ItemRarity rarity)
    {
        // Return rarity-specific prefab if assigned, otherwise fall back to common
        switch (rarity)
        {
            case ItemRarity.Common:
                return commonItemPrefab;
            case ItemRarity.Uncommon:
                return uncommonItemPrefab ?? commonItemPrefab;
            case ItemRarity.Rare:
                return rareItemPrefab ?? commonItemPrefab;
            case ItemRarity.Epic:
                return epicItemPrefab ?? commonItemPrefab;
            case ItemRarity.Legendary:
                return legendaryItemPrefab ?? commonItemPrefab;
            case ItemRarity.Unique:
                return uniqueItemPrefab ?? commonItemPrefab;
            default:
                return commonItemPrefab;
        }
    }

    // Helper class for queueing drop requests
    private class ItemDropRequest
    {
        public Vector3 targetPosition;
        public ItemRarity itemRarity;
        public string itemName;
    }
}
