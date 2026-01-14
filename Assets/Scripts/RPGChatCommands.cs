using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RPGChatCommands : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TwitchOverlayManager twitchManager;

    public string HandleRPGCommand(string command, string userId, string username, string[] args)
    {
        ViewerData viewer = RPGManager.Instance.GetOrCreateViewer(userId, username);

        if (!viewer.CanTakeAction())
        {
            if (viewer.isBanned)
            {
                return $"{username}: You are banned from the RPG system.";
            }
            if (viewer.isDead)
            {
                return $"{username}: You are recovering from death. Please wait.";
            }
        }

        switch (command)
        {
            case "class":
                return HandleClassCommand(viewer, args);

            case "inventory":
            case "inv":
                return HandleInventoryCommand(viewer);

            case "equip":
                return HandleEquipCommand(viewer, args);

            case "unequip":
                return HandleUnequipCommand(viewer, args);

            case "stats":
                return HandleStatsCommand(viewer);

            case "coins":
            case "balance":
                return HandleCoinsCommand(viewer);

            case "shop":
                return HandleShopCommand(viewer, args);

            case "buy":
                return HandleBuyCommand(viewer, args);

            case "trade":
                return HandleTradeCommand(viewer, args);

            case "join":
                return HandleJoinCommand(viewer);

            case "leave":
                return HandleLeaveCommand(viewer);

            case "levelup":
                return HandleLevelUpCommand(viewer, args);

            case "help":
            case "rpghelp":
                return HandleHelpCommand(viewer);

            default:
                return null;
        }
    }

    private string HandleClassCommand(ViewerData viewer, string[] args)
    {
        if (viewer.characterClass != CharacterClass.None)
        {
            return $"{viewer.username}: You are a Level {viewer.baseStats.level} {viewer.characterClass}.";
        }

        if (args.Length == 0)
        {
            return $"{viewer.username}: Choose your class!\n" +
                   "!class rogue - Sneaky damage dealer\n" +
                   "!class fighter - Tank with stances\n" +
                   "!class mage - Powerful spellcaster\n" +
                   "!class cleric - Healer and support\n" +
                   "!class ranger - Balanced attacker";
        }

        string className = args[0].ToLower();
        CharacterClass newClass = CharacterClass.None;

        switch (className)
        {
            case "rogue": newClass = CharacterClass.Rogue; break;
            case "fighter": newClass = CharacterClass.Fighter; break;
            case "mage": newClass = CharacterClass.Mage; break;
            case "cleric": newClass = CharacterClass.Cleric; break;
            case "ranger": newClass = CharacterClass.Ranger; break;
            default:
                return $"{viewer.username}: Invalid class. Choose: rogue, fighter, mage, cleric, or ranger";
        }

        if (RPGManager.Instance.SetViewerClass(viewer.twitchUserId, newClass))
        {
            return $"🎉 {viewer.username}: Welcome, {newClass}! You start at Level 1.\n" +
                   "Use !join to appear on screen and start collecting coins!\n" +
                   "Use !help to see all commands.";
        }

        return $"{viewer.username}: Failed to set class.";
    }

    private string HandleJoinCommand(ViewerData viewer)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first!\n" +
                   "!class rogue/fighter/mage/cleric/ranger";
        }

        // Spawn character on screen
        if (CharacterSpawner.Instance.SpawnCharacter(viewer.twitchUserId, viewer.username))
        {
            return $"⚔️ {viewer.username} has appeared on screen!\n" +
                   $"Level {viewer.baseStats.level} {viewer.characterClass}\n" +
                   $"Collect falling coins to gain XP and gold!";
        }

        return $"{viewer.username}: You're already on screen!";
    }

    private string HandleLeaveCommand(ViewerData viewer)
    {
        CharacterSpawner.Instance.DespawnCharacter(viewer.twitchUserId);
        return $"{viewer.username} has left the adventure. Use !join to return!";
    }

    private string HandleLevelUpCommand(ViewerData viewer, string[] args)
    {
        if (args.Length < 2)
        {
            return $"{viewer.username}: Usage: !levelup <stat> <points>\n" +
                   "Example: !levelup str 2\n" +
                   "Stats: str, con, dex, wil, cha, int\n" +
                   $"Unallocated points: {viewer.baseStats.unallocatedStatPoints}";
        }

        string statName = args[0];

        if (!int.TryParse(args[1], out int points))
        {
            return $"{viewer.username}: Invalid point amount. Use a number.";
        }

        if (points <= 0)
        {
            return $"{viewer.username}: Points must be positive!";
        }

        if (viewer.baseStats.unallocatedStatPoints < points)
        {
            return $"{viewer.username}: You only have {viewer.baseStats.unallocatedStatPoints} unallocated points!";
        }

        if (ExperienceManager.Instance.AllocateStatPoints(viewer.twitchUserId, statName, points))
        {
            return $"✓ {viewer.username}: Allocated {points} points to {statName.ToUpper()}!\n" +
                   $"New total: {GetStatValue(viewer, statName)}\n" +
                   $"Remaining points: {viewer.baseStats.unallocatedStatPoints}";
        }

        return $"{viewer.username}: Invalid stat name. Use: str, con, dex, wil, cha, int";
    }

    private int GetStatValue(ViewerData viewer, string statName)
    {
        statName = statName.ToLower();
        switch (statName)
        {
            case "str":
            case "strength":
                return viewer.baseStats.strength;
            case "con":
            case "constitution":
                return viewer.baseStats.constitution;
            case "dex":
            case "dexterity":
                return viewer.baseStats.dexterity;
            case "wil":
            case "willpower":
                return viewer.baseStats.willpower;
            case "cha":
            case "charisma":
                return viewer.baseStats.charisma;
            case "int":
            case "intelligence":
                return viewer.baseStats.intelligence;
            default:
                return 0;
        }
    }

    private string HandleInventoryCommand(ViewerData viewer)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        string result = $"═══ {viewer.username}'s Equipment ═══\n";
        result += $"Head: {viewer.equipped.head?.itemName ?? "Empty"}\n";
        result += $"Chest: {viewer.equipped.chest?.itemName ?? "Empty"}\n";
        result += $"Arms: {viewer.equipped.arms?.itemName ?? "Empty"}\n";
        result += $"Legs: {viewer.equipped.legs?.itemName ?? "Empty"}\n";
        result += $"L.Hand: {viewer.equipped.leftHand?.itemName ?? "Empty"}\n";
        result += $"R.Hand: {viewer.equipped.rightHand?.itemName ?? "Empty"}\n";
        result += $"Feet: {viewer.equipped.feet?.itemName ?? "Empty"}\n";

        if (viewer.inventory.Count > 0)
        {
            result += $"\n📦 Inventory ({viewer.inventory.Count}/50):\n";
            var itemGroups = viewer.inventory.GroupBy(i => i.itemName).Take(5);
            foreach (var group in itemGroups)
            {
                int count = group.Count();
                string countStr = count > 1 ? $" x{count}" : "";
                result += $"  • {group.First().itemName}{countStr}\n";
            }
            if (viewer.inventory.Count > 5)
            {
                result += $"  ... and {viewer.inventory.Count - 5} more items\n";
            }
        }
        else
        {
            result += "\n📦 Inventory: Empty";
        }

        result += $"\n💰 Coins: {viewer.coins}";

        return result;
    }

    private string HandleEquipCommand(ViewerData viewer, string[] args)
    {
        if (args.Length == 0)
        {
            return $"{viewer.username}: Usage: !equip <item name>";
        }

        string itemName = string.Join(" ", args).ToLower();
        RPGItem item = viewer.inventory.Find(i => i.itemName.ToLower() == itemName);

        if (item == null)
        {
            return $"{viewer.username}: Item '{itemName}' not found in inventory.";
        }

        if (RPGManager.Instance.EquipItem(viewer.twitchUserId, item.itemId))
        {
            return $"✓ {viewer.username}: Equipped {item.itemName}!";
        }

        return $"{viewer.username}: Failed to equip {item.itemName}. Check level requirement or class restrictions.";
    }

    private string HandleUnequipCommand(ViewerData viewer, string[] args)
    {
        if (args.Length == 0)
        {
            return $"{viewer.username}: Usage: !unequip <slot>\n" +
                   "Slots: head, chest, legs, arms, lhand, rhand, feet";
        }

        ItemType slot;
        switch (args[0].ToLower())
        {
            case "head": slot = ItemType.Helmet; break;
            case "chest": slot = ItemType.ChestArmor; break;
            case "legs": slot = ItemType.LegArmor; break;
            case "arms": slot = ItemType.ArmArmor; break;
            case "lhand": slot = ItemType.Weapon; break;
            case "rhand": slot = ItemType.Weapon; break;
            case "feet": slot = ItemType.Boots; break;
            default:
                return $"{viewer.username}: Invalid slot. Use: head, chest, legs, arms, lhand, rhand, feet";
        }

        if (RPGManager.Instance.UnequipItem(viewer.twitchUserId, slot))
        {
            return $"✓ {viewer.username}: Unequipped item from {args[0]}!";
        }

        return $"{viewer.username}: No item equipped in that slot.";
    }

    private string HandleStatsCommand(ViewerData viewer)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        CharacterStats total = viewer.GetTotalStats();
        int xpNeeded = ExperienceManager.Instance.GetXPForNextLevel(viewer.baseStats.level);
        float progress = ExperienceManager.Instance.GetLevelProgress(viewer);

        string result = $"═══ {viewer.username} [{viewer.characterClass}] ═══\n";
        result += $"Level {total.level} | XP: {viewer.baseStats.experience}/{xpNeeded} ({progress * 100:F1}%)\n";
        result += $"HP: {total.currentHealth}/{total.maxHealth}\n";
        result += $"STR: {total.strength} | CON: {total.constitution} | DEX: {total.dexterity}\n";
        result += $"WIL: {total.willpower} | CHA: {total.charisma} | INT: {total.intelligence}\n";
        result += $"💰 Coins: {viewer.coins} | Items: {viewer.inventory.Count}/50";

        return result;
    }

    private string HandleCoinsCommand(ViewerData viewer)
    {
        return $"{viewer.username}: You have 💰 {viewer.coins} coins.";
    }

    private string HandleShopCommand(ViewerData viewer, string[] args)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        if (ShopManager.Instance == null)
        {
            return "Shop system is not available right now.";
        }

        // Check if page number provided
        int page = 1;
        if (args.Length > 0)
        {
            if (int.TryParse(args[0], out int requestedPage))
            {
                page = Mathf.Clamp(requestedPage, 1, 4); // Pages 1-4 only
            }
        }

        // Get the requested page
        return ShopManager.Instance.GetShopPage(viewer, page);
    }

    private string HandleBuyCommand(ViewerData viewer, string[] args)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        if (args.Length == 0)
        {
            return $"{viewer.username}: Usage: !buy <item name>\nExample: !buy iron sword";
        }

        if (ShopManager.Instance == null)
        {
            return "Shop system is not available right now.";
        }

        string itemName = string.Join(" ", args);

        bool success = ShopManager.Instance.PurchaseItem(viewer.twitchUserId, itemName);

        if (!success)
        {
            // Check why it failed
            var shopItems = ShopManager.Instance.GetCurrentShopItems();
            var item = shopItems.Find(i => i.itemName.ToLower() == itemName.ToLower());

            if (item == null)
            {
                return $"{viewer.username}: '{itemName}' not found in shop. Use !shop to see available items.";
            }
            else if (!viewer.CanAfford(item.price))
            {
                return $"{viewer.username}: Not enough coins! {item.itemName} costs {item.price} coins. You have {viewer.coins}.";
            }
            else if (viewer.inventory.Count >= 50)
            {
                return $"{viewer.username}: Your inventory is full! (50/50)";
            }
            else
            {
                return $"{viewer.username}: Purchase failed. Please try again.";
            }
        }

        // Success! Calculate what bonus they got
        var purchasedItem = viewer.inventory[viewer.inventory.Count - 1]; // Last added item
        string bonusText = "";

        if (purchasedItem.strengthBonusPercent > 0)
        {
            int bonus = Mathf.Max(1, Mathf.RoundToInt(viewer.baseStats.strength * purchasedItem.strengthBonusPercent));
            bonusText = $"+{bonus} STR";
        }
        else if (purchasedItem.dexterityBonusPercent > 0)
        {
            int bonus = Mathf.Max(1, Mathf.RoundToInt(viewer.baseStats.dexterity * purchasedItem.dexterityBonusPercent));
            bonusText = $"+{bonus} DEX";
        }
        else if (purchasedItem.constitutionBonusPercent > 0)
        {
            int bonus = Mathf.Max(1, Mathf.RoundToInt(viewer.baseStats.constitution * purchasedItem.constitutionBonusPercent));
            bonusText = $"+{bonus} CON";
        }
        else if (purchasedItem.intelligenceBonusPercent > 0)
        {
            int bonus = Mathf.Max(1, Mathf.RoundToInt(viewer.baseStats.intelligence * purchasedItem.intelligenceBonusPercent));
            bonusText = $"+{bonus} INT";
        }

        return $"✅ {viewer.username} bought {purchasedItem.itemName}! ({bonusText})\nRemaining coins: {viewer.coins}";
    }

    private string HandleTradeCommand(ViewerData viewer, string[] args)
    {
        if (args.Length < 2)
        {
            return $"{viewer.username}: Usage:\n" +
                   "!trade @username <item name> - Trade an item\n" +
                   "!trade @username coins <amount> - Trade coins";
        }

        return $"{viewer.username}: Trading system coming soon!";
    }

    private string HandleHelpCommand(ViewerData viewer)
    {
        string help = "═══ RPG COMMANDS ═══\n";
        help += "!class <type> - Choose your class\n";
        help += "!join - Spawn on screen to collect coins\n";
        help += "!leave - Remove your character from screen\n";
        help += "!stats - View your character stats\n";
        help += "!inventory (or !inv) - View equipment\n";
        help += "!coins - Check your balance\n";
        help += "!levelup <stat> <points> - Allocate stats (e.g., !levelup str 2)\n";
        help += "!equip <item> - Equip an item\n";
        help += "!unequip <slot> - Unequip an item\n";
        help += "!shop - View the shop (coming soon)\n";
        help += "\nEarn XP by collecting coins and watching!\n";
        help += "Level up every 150 XP to get stronger!";

        return help;
    }

    public string HandleAdminCommand(string command, string[] args, bool isBroadcaster)
    {
        if (!isBroadcaster)
        {
            return null;
        }

        switch (command)
        {
            case "rpggive":
                return HandleAdminGive(args);
            case "rpgban":
                return HandleAdminBan(args);
            case "rpgunban":
                return HandleAdminUnban(args);
            case "rpgreset":
                return HandleAdminReset(args);
            case "rpgkill":
                return HandleAdminKill(args);
            case "rpgsave":
                RPGManager.Instance.SaveGameData();
                return "✓ RPG data saved!";
            case "rpghelpadmin":
                return HandleAdminHelp();
            case "rpgrefreshop":
            case "rpgrefreshshop":
                return HandleAdminRefreshShop();

            case "rpggiveitem":
                return HandleAdminGiveItem(args);

            default:
                return null;
        }
    }

    private string HandleAdminGive(string[] args)
    {
        // Usage: !rpggive @username coins 100
        // Usage: !rpggive @username xp 50
        if (args.Length < 3)
        {
            return "Usage:\n" +
                   "!rpggive @username coins <amount>\n" +
                   "!rpggive @username xp <amount>\n" +
                   "Example: !rpggive @alice coins 100";
        }

        string targetUsername = args[0].TrimStart('@').ToLower();
        string giveType = args[1].ToLower();

        if (!int.TryParse(args[2], out int amount))
        {
            return "Error: Amount must be a number!";
        }

        if (amount <= 0)
        {
            return "Error: Amount must be positive!";
        }

        // Find viewer by username
        ViewerData targetViewer = FindViewerByUsername(targetUsername);

        if (targetViewer == null)
        {
            return $"Error: User '{targetUsername}' not found in database.\n" +
                   "They need to have used at least one RPG command first.";
        }

        switch (giveType)
        {
            case "coins":
            case "coin":
                RPGManager.Instance.AddCoins(targetViewer.twitchUserId, amount);
                RPGManager.Instance.SaveGameData();
                return $"✓ Gave {amount} coins to {targetViewer.username}!\n" +
                       $"New balance: {targetViewer.coins} coins";

            case "xp":
            case "exp":
                if (ExperienceManager.Instance != null)
                {
                    ExperienceManager.Instance.AddExperience(targetViewer.twitchUserId, amount);
                    RPGManager.Instance.SaveGameData();
                    return $"✓ Gave {amount} XP to {targetViewer.username}!\n" +
                           $"Current XP: {targetViewer.baseStats.experience}";
                }
                else
                {
                    return "Error: ExperienceManager not found in scene!";
                }

            default:
                return $"Error: Unknown type '{giveType}'. Use 'coins' or 'xp'";
        }
    }

    private string HandleAdminBan(string[] args)
    {
        if (args.Length < 1)
        {
            return "Usage: !rpgban @username\n" +
                   "Example: !rpgban @baduser";
        }

        string targetUsername = args[0].TrimStart('@').ToLower();
        ViewerData targetViewer = FindViewerByUsername(targetUsername);

        if (targetViewer == null)
        {
            return $"Error: User '{targetUsername}' not found in database.";
        }

        if (targetViewer.isBanned)
        {
            return $"{targetViewer.username} is already banned.";
        }

        RPGManager.Instance.AdminBanViewer(targetViewer.twitchUserId, true);

        // Remove from screen if they're active
        if (CharacterSpawner.Instance != null)
        {
            CharacterSpawner.Instance.DespawnCharacter(targetViewer.twitchUserId);
        }

        return $"✓ {targetViewer.username} has been banned from the RPG system.";
    }

    private string HandleAdminUnban(string[] args)
    {
        if (args.Length < 1)
        {
            return "Usage: !rpgunban @username\n" +
                   "Example: !rpgunban @gooduser";
        }

        string targetUsername = args[0].TrimStart('@').ToLower();
        ViewerData targetViewer = FindViewerByUsername(targetUsername);

        if (targetViewer == null)
        {
            return $"Error: User '{targetUsername}' not found in database.";
        }

        if (!targetViewer.isBanned)
        {
            return $"{targetViewer.username} is not banned.";
        }

        RPGManager.Instance.AdminBanViewer(targetViewer.twitchUserId, false);
        return $"✓ {targetViewer.username} has been unbanned!";
    }

    private string HandleAdminReset(string[] args)
    {
        if (args.Length < 1)
        {
            return "Usage: !rpgreset @username\n" +
                   "WARNING: This deletes all their progress!\n" +
                   "Example: !rpgreset @username";
        }

        string targetUsername = args[0].TrimStart('@').ToLower();
        ViewerData targetViewer = FindViewerByUsername(targetUsername);

        if (targetViewer == null)
        {
            return $"Error: User '{targetUsername}' not found in database.";
        }

        string userId = targetViewer.twitchUserId;
        string username = targetViewer.username;

        // Despawn character if on screen
        if (CharacterSpawner.Instance != null)
        {
            CharacterSpawner.Instance.DespawnCharacter(userId);
        }

        RPGManager.Instance.AdminResetViewer(userId);
        return $"✓ Reset {username}'s character!\n" +
               "They can choose a new class with !class";
    }

    private string HandleAdminKill(string[] args)
    {
        if (args.Length < 1)
        {
            return "Usage: !rpgkill @username\n" +
                   "Kills the player (30 min death lockout)\n" +
                   "Example: !rpgkill @unluckyviewer";
        }

        string targetUsername = args[0].TrimStart('@').ToLower();
        ViewerData targetViewer = FindViewerByUsername(targetUsername);

        if (targetViewer == null)
        {
            return $"Error: User '{targetUsername}' not found in database.";
        }

        if (targetViewer.isDead)
        {
            return $"{targetViewer.username} is already dead!";
        }

        // Kill the player
        targetViewer.isDead = true;
        targetViewer.deathLockoutUntil = System.DateTime.Now.AddMinutes(30);
        targetViewer.baseStats.currentHealth = 0;
        targetViewer.baseStats.experience = 0; // Reset XP progress to 0

        // Despawn character if on screen
        if (CharacterSpawner.Instance != null)
        {
            CharacterSpawner.Instance.DespawnCharacter(targetViewer.twitchUserId);
        }

        RPGManager.Instance.SaveGameData();

        return $"💀 {targetViewer.username} has been slain!\n" +
               "Death lockout: 30 minutes\n" +
               "XP progress reset to 0";
    }

    private string HandleAdminHelp()
    {
        return "═══ ADMIN COMMANDS ═══\n" +
               "!rpgsave - Save game data\n" +
               "!rpggive @user coins <amount> - Give coins\n" +
               "!rpggive @user xp <amount> - Give XP\n" +
               "!rpgban @user - Ban from RPG\n" +
               "!rpgunban @user - Unban user\n" +
               "!rpgkill @user - Kill player (30 min lockout)\n" +
               "!rpgreset @user - Reset character (DELETES PROGRESS!)";
    }

    private string HandleAdminRefreshShop()
    {
        if (ShopManager.Instance == null)
        {
            return "Shop system not available!";
        }

        ShopManager.Instance.RefreshShop();

        TimeSpan timeUntilNext = ShopManager.Instance.GetTimeUntilRefresh();
        return $"✓ Shop refreshed!\nNext refresh in {timeUntilNext.Hours}h {timeUntilNext.Minutes}m";
    }

    private string HandleAdminGiveItem(string[] args)
    {
        if (args.Length < 2)
        {
            return "Usage: !rpggiveitem @username <item name>\n" +
                   "Example: !rpggiveitem @alice shadowfang";
        }

        string targetUsername = args[0].TrimStart('@').ToLower();
        string itemName = string.Join(" ", args.Skip(1));

        // Find viewer
        ViewerData targetViewer = FindViewerByUsername(targetUsername);
        if (targetViewer == null)
        {
            return $"Error: User '{targetUsername}' not found.";
        }

        // Find item in named items
        if (HybridItemSystem.Instance == null)
        {
            return "Item system not available!";
        }

        RPGItem item = HybridItemSystem.Instance.GetNamedItem(itemName);
        if (item == null)
        {
            return $"Error: Item '{itemName}' not found.\nTip: This only works with hand-crafted named items.";
        }

        // Check inventory space
        if (targetViewer.inventory.Count >= 50)
        {
            return $"Error: {targetViewer.username}'s inventory is full!";
        }

        // Give item (create copy)
        RPGItem giftedItem = new RPGItem
        {
            itemName = item.itemName,
            description = item.description,
            itemType = item.itemType,
            rarity = item.rarity,
            requiredLevel = item.requiredLevel,
            price = item.price,

            strengthBonusPercent = item.strengthBonusPercent,
            constitutionBonusPercent = item.constitutionBonusPercent,
            dexterityBonusPercent = item.dexterityBonusPercent,
            willpowerBonusPercent = item.willpowerBonusPercent,
            charismaBonusPercent = item.charismaBonusPercent,
            intelligenceBonusPercent = item.intelligenceBonusPercent,

            damageBonus = item.damageBonus,
            defenseBonus = item.defenseBonus,
            healAmount = item.healAmount,

            allowedClasses = new List<CharacterClass>(item.allowedClasses),
            properties = new Dictionary<string, string>(item.properties)
        };

        targetViewer.AddItem(giftedItem);
        RPGManager.Instance.SaveGameData();

        return $"Wilfinja Gave {item.itemName} [{item.rarity}] to {targetViewer.username}!";
    }

    // HELPER METHOD: Find viewer by username (case-insensitive)
    private ViewerData FindViewerByUsername(string username)
    {
        username = username.ToLower();

        var allViewers = RPGManager.Instance.GetAllViewers();
        if (allViewers == null) return null;

        foreach (var viewer in allViewers)
        {
            if (viewer.username.ToLower() == username)
            {
                return viewer;
            }
        }

        return null;
    }
}
