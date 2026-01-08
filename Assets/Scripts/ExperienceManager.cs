using UnityEngine;

public class ExperienceManager : MonoBehaviour
{
    private static ExperienceManager _instance;
    public static ExperienceManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ExperienceManager>();
            }
            return _instance;
        }
    }

    [Header("XP Settings")]
    [SerializeField] private int xpPerLevel = 150;
    [SerializeField] private int maxLevel = 50;
    [SerializeField] private int statPointsPerLevel = 2;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    public void AddExperience(string userId, int amount)
    {
        ViewerData viewer = RPGManager.Instance.GetViewer(userId);
        if (viewer == null) return;

        viewer.baseStats.experience += amount;

        // Check for level up
        while (CanLevelUp(viewer))
        {
            LevelUp(viewer);
        }
    }

    private bool CanLevelUp(ViewerData viewer)
    {
        if (viewer.baseStats.level >= maxLevel)
            return false;

        int xpNeeded = GetXPForNextLevel(viewer.baseStats.level);
        return viewer.baseStats.experience >= xpNeeded;
    }

    private void LevelUp(ViewerData viewer)
    {
        // Deduct XP cost
        int xpCost = GetXPForNextLevel(viewer.baseStats.level);
        viewer.baseStats.experience -= xpCost;

        // Increase level
        viewer.baseStats.level++;

        // Grant stat points
        viewer.baseStats.unallocatedStatPoints += statPointsPerLevel;

        // Fully restore health
        viewer.baseStats.RecalculateHealth();
        viewer.baseStats.currentHealth = viewer.baseStats.maxHealth;

        // Notify player
        string notification = $"🎉 {viewer.username} reached Level {viewer.baseStats.level}!\n" +
                            $"You have {statPointsPerLevel} stat points to allocate.\n" +
                            $"Use: !levelup <stat> <points> (e.g., !levelup str 2)";

        OnScreenNotification.Instance?.ShowSuccess(notification);

        // Check for ability unlock
        CheckAbilityUnlock(viewer);

        // Refresh on-screen character
        CharacterSpawner.Instance?.RefreshCharacter(viewer.twitchUserId);

        // Save progress
        RPGManager.Instance.SaveGameData();

        Debug.Log($"[XP] {viewer.username} leveled up to {viewer.baseStats.level}!");
    }

    private void CheckAbilityUnlock(ViewerData viewer)
    {
        int level = viewer.baseStats.level;

        // Check if this level unlocks an ability (every 5 levels)
        if (level % 5 == 0)
        {
            int abilitiesUnlocked = viewer.characterClass == CharacterClass.Mage ? 2 : 1;

            string abilityNotification = $"✨ {viewer.username} unlocked {abilitiesUnlocked} new " +
                                       $"{(abilitiesUnlocked > 1 ? "abilities" : "ability")} at Level {level}!";

            OnScreenNotification.Instance?.ShowInfo(abilityNotification);
        }
    }

    public int GetXPForNextLevel(int currentLevel)
    {
        // Linear: 150 XP per level
        return xpPerLevel;
    }

    public float GetLevelProgress(ViewerData viewer)
    {
        if (viewer.baseStats.level >= maxLevel)
            return 1f;

        int xpNeeded = GetXPForNextLevel(viewer.baseStats.level);
        return (float)viewer.baseStats.experience / xpNeeded;
    }

    public bool AllocateStatPoints(string userId, string statName, int points)
    {
        ViewerData viewer = RPGManager.Instance.GetViewer(userId);
        if (viewer == null) return false;

        // Check if they have enough unallocated points
        if (viewer.baseStats.unallocatedStatPoints < points)
        {
            Debug.LogWarning($"[XP] {viewer.username} doesn't have {points} unallocated stat points");
            return false;
        }

        statName = statName.ToLower();

        switch (statName)
        {
            case "str":
            case "strength":
                viewer.baseStats.strength += points;
                break;
            case "con":
            case "constitution":
                viewer.baseStats.constitution += points;
                break;
            case "dex":
            case "dexterity":
                viewer.baseStats.dexterity += points;
                break;
            case "wil":
            case "willpower":
                viewer.baseStats.willpower += points;
                break;
            case "cha":
            case "charisma":
                viewer.baseStats.charisma += points;
                break;
            case "int":
            case "intelligence":
                viewer.baseStats.intelligence += points;
                break;
            default:
                return false;
        }

        // Deduct stat points
        viewer.baseStats.unallocatedStatPoints -= points;

        viewer.baseStats.RecalculateHealth();
        RPGManager.Instance.SaveGameData();

        Debug.Log($"[XP] {viewer.username} allocated {points} points to {statName}");
        return true;
    }

    // Called by combat system (Phase 6+)
    public void AwardCombatExperience(string[] userIds, int xpAmount)
    {
        foreach (string userId in userIds)
        {
            AddExperience(userId, xpAmount);
        }
    }
}