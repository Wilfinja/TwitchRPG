using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RPGManager : MonoBehaviour
{
    private static RPGManager _instance;
    public static RPGManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<RPGManager>();
                if (_instance == null)
                {
                    Debug.LogError("[RPG] RPGManager not found in scene!");
                }
            }
            return _instance;
        }
    }

    [Header("Settings")]
    [SerializeField] private bool autoSave = true;
    [SerializeField] private float autoSaveIntervalMinutes = 5f;

    [Header("Active Viewers (On Screen)")]
    [SerializeField] private int maxActiveViewers = 10;

    private GameDatabase gameDatabase;
    private List<string> activeViewerIds = new List<string>();
    private float autoSaveTimer;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        LoadGameData();
    }

    private void Start()
    {
        autoSaveTimer = autoSaveIntervalMinutes * 60f;
    }

    private void Update()
    {
        if (autoSave)
        {
            autoSaveTimer -= Time.deltaTime;
            if (autoSaveTimer <= 0)
            {
                SaveGameData();
                autoSaveTimer = autoSaveIntervalMinutes * 60f;
            }
        }
    }

    private void OnApplicationQuit()
    {
        SaveGameData();
    }

    // ==================== DATA MANAGEMENT ====================

    public void LoadGameData()
    {
        gameDatabase = RPGSaveSystem.LoadGameData();
        Debug.Log($"[RPG] Database loaded with {gameDatabase.allViewers.Count} viewers");
    }

    public void SaveGameData()
    {
        if (gameDatabase != null)
        {
            RPGSaveSystem.SaveGameData(gameDatabase);
        }
    }

    // ==================== VIEWER MANAGEMENT ====================

    public ViewerData GetOrCreateViewer(string userId, string username)
    {
        return gameDatabase.GetOrCreateViewer(userId, username);
    }

    public ViewerData GetViewer(string userId)
    {
        return gameDatabase.allViewers.Find(v => v.twitchUserId == userId);
    }

    public bool IsViewerActive(string userId)
    {
        return activeViewerIds.Contains(userId);
    }
    public List<ViewerData> GetAllViewers()
    {
        return gameDatabase.allViewers;
    }

    public bool TryAddActiveViewer(string userId)
    {
        if (activeViewerIds.Count >= maxActiveViewers)
        {
            return false;
        }

        if (!activeViewerIds.Contains(userId))
        {
            activeViewerIds.Add(userId);
            return true;
        }

        return false;
    }

    public void RemoveActiveViewer(string userId)
    {
        activeViewerIds.Remove(userId);
    }

    public List<ViewerData> GetActiveViewers()
    {
        List<ViewerData> viewers = new List<ViewerData>();
        foreach (string id in activeViewerIds)
        {
            ViewerData viewer = GetViewer(id);
            if (viewer != null)
            {
                viewers.Add(viewer);
            }
        }
        return viewers;
    }

    // ==================== COIN MANAGEMENT ====================

    public void AddCoins(string userId, int amount)
    {
        ViewerData viewer = GetViewer(userId);
        if (viewer != null)
        {
            viewer.coins += amount;
            Debug.Log($"[RPG] {viewer.username} gained {amount} coins (Total: {viewer.coins})");
        }
    }

    public bool SpendCoins(string userId, int amount)
    {
        ViewerData viewer = GetViewer(userId);
        if (viewer != null && viewer.CanAfford(amount))
        {
            viewer.coins -= amount;
            Debug.Log($"[RPG] {viewer.username} spent {amount} coins (Remaining: {viewer.coins})");
            return true;
        }
        return false;
    }

    // ==================== INVENTORY MANAGEMENT ====================

    public bool GiveItemToViewer(string userId, RPGItem item)
    {
        ViewerData viewer = GetViewer(userId);
        if (viewer != null)
        {
            if (viewer.AddItem(item))
            {
                Debug.Log($"[RPG] {viewer.username} received {item.itemName}");
                return true;
            }
            else
            {
                Debug.LogWarning($"[RPG] {viewer.username}'s inventory is full!");
                return false;
            }
        }
        return false;
    }

    public bool EquipItem(string userId, string itemId)
    {
        ViewerData viewer = GetViewer(userId);
        if (viewer == null) return false;

        RPGItem item = viewer.FindItemInInventory(itemId);
        if (item == null)
        {
            Debug.LogWarning($"[RPG] Item {itemId} not found in {viewer.username}'s inventory");
            return false;
        }

        // Check class restrictions
        if (item.allowedClasses.Count > 0 && !item.allowedClasses.Contains(viewer.characterClass))
        {
            Debug.LogWarning($"[RPG] {viewer.username} cannot equip {item.itemName} - class restriction");
            return false;
        }

        // Check level requirement
        if (viewer.baseStats.level < item.requiredLevel)
        {
            Debug.LogWarning($"[RPG] {viewer.username} cannot equip {item.itemName} - requires level {item.requiredLevel}");
            return false;
        }

        // Unequip current item in that slot if it exists
        RPGItem currentItem = viewer.equipped.GetEquippedItem(item.itemType);
        if (currentItem != null)
        {
            viewer.AddItem(currentItem); // Put back in inventory
        }

        // Equip new item
        viewer.equipped.SetEquippedItem(item.itemType, item);
        viewer.RemoveItem(itemId); // Remove from inventory

        Debug.Log($"[RPG] {viewer.username} equipped {item.itemName}");
        return true;
    }

    public bool UnequipItem(string userId, ItemType slot)
    {
        ViewerData viewer = GetViewer(userId);
        if (viewer == null) return false;

        RPGItem item = viewer.equipped.GetEquippedItem(slot);
        if (item == null)
        {
            Debug.LogWarning($"[RPG] No item equipped in {slot} slot");
            return false;
        }

        if (!viewer.AddItem(item))
        {
            Debug.LogWarning($"[RPG] Cannot unequip - inventory full!");
            return false;
        }

        viewer.equipped.SetEquippedItem(slot, null);
        Debug.Log($"[RPG] {viewer.username} unequipped {item.itemName}");
        return true;
    }

    // ==================== CLASS MANAGEMENT ====================

    public bool SetViewerClass(string userId, CharacterClass newClass)
    {
        ViewerData viewer = GetViewer(userId);
        if (viewer == null) return false;

        if (viewer.characterClass != CharacterClass.None)
        {
            Debug.LogWarning($"[RPG] {viewer.username} already has a class. Use ResetViewer to change.");
            return false;
        }

        viewer.SetClass(newClass);
        Debug.Log($"[RPG] {viewer.username} is now a {newClass}!");
        return true;
    }

    // ==================== TRADING ====================

    public bool TradeItem(string fromUserId, string toUserId, string itemId)
    {
        ViewerData fromViewer = GetViewer(fromUserId);
        ViewerData toViewer = GetViewer(toUserId);

        if (fromViewer == null || toViewer == null) return false;

        RPGItem item = fromViewer.FindItemInInventory(itemId);
        if (item == null)
        {
            Debug.LogWarning($"[RPG] {fromViewer.username} doesn't have item {itemId}");
            return false;
        }

        if (!toViewer.AddItem(item))
        {
            Debug.LogWarning($"[RPG] {toViewer.username}'s inventory is full!");
            return false;
        }

        fromViewer.RemoveItem(itemId);
        Debug.Log($"[RPG] {fromViewer.username} traded {item.itemName} to {toViewer.username}");
        return true;
    }

    public bool TradeCoins(string fromUserId, string toUserId, int amount)
    {
        ViewerData fromViewer = GetViewer(fromUserId);
        ViewerData toViewer = GetViewer(toUserId);

        if (fromViewer == null || toViewer == null) return false;

        if (!fromViewer.CanAfford(amount))
        {
            Debug.LogWarning($"[RPG] {fromViewer.username} doesn't have {amount} coins");
            return false;
        }

        fromViewer.coins -= amount;
        toViewer.coins += amount;
        Debug.Log($"[RPG] {fromViewer.username} gave {amount} coins to {toViewer.username}");
        return true;
    }

    // ==================== ADMIN COMMANDS ====================

    public void AdminGiveCoins(string userId, int amount)
    {
        ViewerData viewer = GetViewer(userId);
        if (viewer != null)
        {
            viewer.coins += amount;
            Debug.Log($"[RPG ADMIN] Gave {amount} coins to {viewer.username}");
            SaveGameData();
        }
    }

    public void AdminGiveItem(string userId, RPGItem item)
    {
        GiveItemToViewer(userId, item);
        SaveGameData();
    }

    public void AdminBanViewer(string userId, bool banned)
    {
        ViewerData viewer = GetViewer(userId);
        if (viewer != null)
        {
            viewer.isBanned = banned;
            Debug.Log($"[RPG ADMIN] {viewer.username} ban status: {banned}");
            SaveGameData();
        }
    }

    public void AdminResetViewer(string userId)
    {
        ViewerData viewer = GetViewer(userId);
        if (viewer != null)
        {
            string username = viewer.username;
            gameDatabase.allViewers.Remove(viewer);
            ViewerData newViewer = new ViewerData(userId, username);
            gameDatabase.allViewers.Add(newViewer);
            Debug.Log($"[RPG ADMIN] Reset {username}");
            SaveGameData();
        }
    }

    // ==================== ITEM DATABASE ====================

    public void AddItemToDatabase(RPGItem item)
    {
        gameDatabase.itemDatabase.Add(item);
        Debug.Log($"[RPG] Added {item.itemName} to item database");
    }

    public RPGItem GetItemFromDatabase(string itemId)
    {
        return gameDatabase.GetItemById(itemId);
    }

    public List<RPGItem> GetAllItemsOfRarity(ItemRarity rarity)
    {
        return gameDatabase.itemDatabase.Where(i => i.rarity == rarity).ToList();
    }

    // ==================== LEADERBOARDS ====================

    public List<ViewerData> GetTopViewersByCoins(int count)
    {
        return gameDatabase.allViewers
            .OrderByDescending(v => v.coins)
            .Take(count)
            .ToList();
    }

    public List<ViewerData> GetTopViewersByLevel(int count)
    {
        return gameDatabase.allViewers
            .OrderByDescending(v => v.baseStats.level)
            .Take(count)
            .ToList();
    }

    public List<ViewerData> GetTopViewersByWatchTime(int count)
    {
        return gameDatabase.allViewers
            .OrderByDescending(v => v.totalWatchTimeMinutes)
            .Take(count)
            .ToList();
    }
}
