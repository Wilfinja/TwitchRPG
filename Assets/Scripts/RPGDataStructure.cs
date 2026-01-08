using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public enum ItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    Unique
}

[Serializable]
public enum ItemType
{
    Weapon,
    Helmet,
    ChestArmor,
    LegArmor,
    ArmArmor,
    Boots,
    Consumable
}

[Serializable]
public enum CharacterClass
{
    None,
    Rogue,
    Cleric,
    Fighter,
    Ranger,
    Mage
}

[Serializable]
public class CharacterStats
{
    public int strength;
    public int constitution;
    public int dexterity;
    public int willpower;
    public int charisma;
    public int intelligence;

    public int maxHealth;
    public int currentHealth;
    public int level;
    public int experience;
    public int unallocatedStatPoints; // NEW: Track stat points from leveling

    public CharacterStats()
    {
        strength = 1;
        constitution = 1;
        dexterity = 1;
        willpower = 1;
        charisma = 1;
        intelligence = 1;
        level = 1;
        experience = 0;
        maxHealth = 100;
        currentHealth = 100;
        unallocatedStatPoints = 0;
    }

    public void RecalculateHealth()
    {
        maxHealth = 50 + (constitution * 10) + (level * 5);
        currentHealth = Mathf.Min(currentHealth, maxHealth);
    }
}

[Serializable]
public class ClassResources
{
    // Rogue
    public int sneak;
    public int maxSneak = 6;

    // Fighter
    public Dictionary<string, int> maneuverCooldowns = new Dictionary<string, int>();
    public string currentStance = "None";

    // Mage
    public int mana;
    public int maxMana = 100;

    // Cleric
    public int wrath;
    public int maxWrath = 100;

    // Ranger
    public int balance;
    public int maxBalance = 10;
    public int minBalance = -10;

    public void ResetForClass(CharacterClass charClass)
    {
        sneak = 0;
        maneuverCooldowns.Clear();
        currentStance = "None";
        mana = maxMana;
        wrath = 0;
        balance = 0;
    }
}

[Serializable]
public class RPGItem
{
    public string itemId;
    public string itemName;
    public string description;
    public ItemType itemType;
    public ItemRarity rarity;
    public int requiredLevel;
    public int price;

    // Stat bonuses
    public int strengthBonus;
    public int constitutionBonus;
    public int dexterityBonus;
    public int willpowerBonus;
    public int charismaBonus;
    public int intelligenceBonus;

    // Combat properties
    public int damageBonus;
    public int defenseBonus;
    public int healAmount;

    // Class restrictions
    public List<CharacterClass> allowedClasses = new List<CharacterClass>();

    // Special properties
    public Dictionary<string, string> properties = new Dictionary<string, string>();

    public RPGItem()
    {
        itemId = Guid.NewGuid().ToString();
        allowedClasses = new List<CharacterClass>();
        properties = new Dictionary<string, string>();
    }

    public Color GetRarityColor()
    {
        switch (rarity)
        {
            case ItemRarity.Common: return new Color(0.6f, 0.6f, 0.6f);
            case ItemRarity.Uncommon: return new Color(0.2f, 0.8f, 0.2f);
            case ItemRarity.Rare: return new Color(0.2f, 0.4f, 1f);
            case ItemRarity.Epic: return new Color(0.64f, 0.21f, 0.93f);
            case ItemRarity.Legendary: return new Color(1f, 0.5f, 0f);
            case ItemRarity.Unique: return new Color(0f, 1f, 1f);
            default: return Color.white;
        }
    }
}

[Serializable]
public class EquippedItems
{
    public RPGItem head;
    public RPGItem chest;
    public RPGItem legs;
    public RPGItem arms;
    public RPGItem leftHand;
    public RPGItem rightHand;
    public RPGItem feet;

    public bool HasItem(string itemId)
    {
        return (head?.itemId == itemId) ||
               (chest?.itemId == itemId) ||
               (legs?.itemId == itemId) ||
               (arms?.itemId == itemId) ||
               (leftHand?.itemId == itemId) ||
               (rightHand?.itemId == itemId) ||
               (feet?.itemId == itemId);
    }

    public RPGItem GetEquippedItem(ItemType slot)
    {
        switch (slot)
        {
            case ItemType.Helmet: return head;
            case ItemType.ChestArmor: return chest;
            case ItemType.LegArmor: return legs;
            case ItemType.ArmArmor: return arms;
            case ItemType.Boots: return feet;
            case ItemType.Weapon:
                return rightHand ?? leftHand;
            default: return null;
        }
    }

    public void SetEquippedItem(ItemType slot, RPGItem item)
    {
        switch (slot)
        {
            case ItemType.Helmet: head = item; break;
            case ItemType.ChestArmor: chest = item; break;
            case ItemType.LegArmor: legs = item; break;
            case ItemType.ArmArmor: arms = item; break;
            case ItemType.Boots: feet = item; break;
            case ItemType.Weapon:
                if (leftHand == null) leftHand = item;
                else rightHand = item;
                break;
        }
    }

    public CharacterStats CalculateTotalStats(CharacterStats baseStats)
    {
        CharacterStats total = new CharacterStats
        {
            strength = baseStats.strength,
            constitution = baseStats.constitution,
            dexterity = baseStats.dexterity,
            willpower = baseStats.willpower,
            charisma = baseStats.charisma,
            intelligence = baseStats.intelligence,
            level = baseStats.level,
            experience = baseStats.experience,
            unallocatedStatPoints = baseStats.unallocatedStatPoints
        };

        RPGItem[] allItems = { head, chest, legs, arms, leftHand, rightHand, feet };
        foreach (var item in allItems)
        {
            if (item != null)
            {
                total.strength += item.strengthBonus;
                total.constitution += item.constitutionBonus;
                total.dexterity += item.dexterityBonus;
                total.willpower += item.willpowerBonus;
                total.charisma += item.charismaBonus;
                total.intelligence += item.intelligenceBonus;
            }
        }

        total.RecalculateHealth();
        return total;
    }
}

[Serializable]
public class ViewerData
{
    public string twitchUserId;
    public string username;
    public int coins;
    public CharacterClass characterClass;

    public CharacterStats baseStats;
    public ClassResources classResources;
    public EquippedItems equipped;
    public List<RPGItem> inventory;

    public DateTime lastSeen;
    public float totalWatchTimeMinutes;
    public bool isInCombat;
    public bool isDead;
    public DateTime deathLockoutUntil;
    public bool isBanned;

    public ViewerData(string userId, string name)
    {
        twitchUserId = userId;
        username = name;
        coins = 0;
        characterClass = CharacterClass.None;

        baseStats = new CharacterStats();
        classResources = new ClassResources();
        equipped = new EquippedItems();
        inventory = new List<RPGItem>();

        lastSeen = DateTime.Now;
        totalWatchTimeMinutes = 0;
        isInCombat = false;
        isDead = false;
        deathLockoutUntil = DateTime.MinValue;
        isBanned = false;
    }

    public bool CanTakeAction()
    {
        if (isBanned) return false;
        if (isDead && DateTime.Now < deathLockoutUntil) return false;
        return true;
    }

    public void SetClass(CharacterClass newClass)
    {
        characterClass = newClass;
        classResources.ResetForClass(newClass);

        switch (newClass)
        {
            case CharacterClass.Rogue:
                baseStats.dexterity += 2;
                baseStats.intelligence += 1;
                break;
            case CharacterClass.Fighter:
                baseStats.strength += 2;
                baseStats.constitution += 2;
                break;
            case CharacterClass.Mage:
                baseStats.intelligence += 8;
                baseStats.willpower += 2;
                break;
            case CharacterClass.Cleric:
                baseStats.willpower += 5;
                baseStats.charisma += 3;
                baseStats.constitution += 2;
                break;
            case CharacterClass.Ranger:
                baseStats.dexterity += 4;
                baseStats.constitution += 3;
                baseStats.willpower += 3;
                break;
        }

        baseStats.RecalculateHealth();
    }

    public CharacterStats GetTotalStats()
    {
        return equipped.CalculateTotalStats(baseStats);
    }

    public bool CanAfford(int cost)
    {
        return coins >= cost;
    }

    public bool AddItem(RPGItem item)
    {
        if (inventory.Count >= 50)
        {
            return false;
        }
        inventory.Add(item);
        return true;
    }

    public bool RemoveItem(string itemId)
    {
        RPGItem item = inventory.Find(i => i.itemId == itemId);
        if (item != null)
        {
            inventory.Remove(item);
            return true;
        }
        return false;
    }

    public RPGItem FindItemInInventory(string itemId)
    {
        return inventory.Find(i => i.itemId == itemId);
    }
}

[Serializable]
public class GameDatabase
{
    public List<ViewerData> allViewers = new List<ViewerData>();
    public List<RPGItem> itemDatabase = new List<RPGItem>();
    public DateTime lastSaveTime;

    public GameDatabase()
    {
        allViewers = new List<ViewerData>();
        itemDatabase = new List<RPGItem>();
        lastSaveTime = DateTime.Now;
    }

    public ViewerData GetOrCreateViewer(string userId, string username)
    {
        ViewerData viewer = allViewers.Find(v => v.twitchUserId == userId);
        if (viewer == null)
        {
            viewer = new ViewerData(userId, username);
            allViewers.Add(viewer);
        }
        else
        {
            viewer.username = username;
            viewer.lastSeen = DateTime.Now;
        }
        return viewer;
    }

    public RPGItem GetItemById(string itemId)
    {
        return itemDatabase.Find(i => i.itemId == itemId);
    }
}
