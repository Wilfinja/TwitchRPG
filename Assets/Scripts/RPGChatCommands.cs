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

            case "sell":
                return HandleSellCommand(viewer, args);

            case "trade":
                return HandleTradeCommand(viewer, args);

            case "join":
                return HandleJoinCommand(viewer);

            case "leave":
                return HandleLeaveCommand(viewer);

            case "levelup":
                return HandleLevelUpCommand(viewer, args);

            case "abilities":
                return HandleAbilitiesCommand(viewer);

            case "ability":
                return HandleAbilityDetailsCommand(viewer, args);

            case "loadout":
                return HandleLoadoutCommand(viewer);

            case "equipability":
            case "equipa":
                return HandleEquipAbilityCommand(viewer, args);

            case "unequipability":
            case "unequipa":
                return HandleUnequipAbilityCommand(viewer, args);

            case "help":
            case "rpghelp":
                return HandleHelpCommand(viewer);

            case "linkdiscord":
                return HandleLinkDiscordCommand(viewer, args);

            case "give":
                return HandleGiveCommand(viewer, args);

            case "accepttrade":
                return HandleAcceptTradeCommand(viewer, args);

            case "canceltrade":
                return HandleCancelTradeCommand(viewer);

            case "tradehistory":
                return HandleTradeHistoryCommand(viewer, args);

            case "enterexpedition":
                return HandleEnterExpedition(viewer, args);

            case "startexpedition":
                return HandleStartExpedition(args);

            case "q":
            case "queue":
                return HandleQueueAction(viewer, args);

            case "confirm":
                return HandleConfirmAction(viewer);

            case "challenge":
                return HandleChallengeCommand(viewer, args);

            case "accept":
                return HandleAcceptCommand(viewer);

            case "decline":
                return HandleDeclineCommand(viewer);

            case "bet":
                return HandleBetCommand(viewer, args);

            case "pvpstats":
                return HandlePvPStatsCommand(args);

            case "pvpleaderboard":
                return HandlePvPLeaderboardCommand();

            case "stance":
                return HandleStanceCommand(userId, username, args);

            case "stances":
                return HandleStancesCommand(userId);

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

        // Store old value
        int oldValue = GetStatValue(viewer, statName);

        if (ExperienceManager.Instance != null &&
            ExperienceManager.Instance.AllocateStatPoints(viewer.twitchUserId, statName, points))
        {
            int newValue = GetStatValue(viewer, statName);
            return $"✅ {viewer.username}: Allocated {points} points to {statName.ToUpper()}!\n" +
                   $"{statName.ToUpper()}: {oldValue} → {newValue}\n" +
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

    // ===== UPDATED INVENTORY COMMAND =====
    private string HandleInventoryCommand(ViewerData viewer)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        CharacterStats totalStats = viewer.GetTotalStats();
        int totalDamage = viewer.equipped.GetTotalDamageBonus();
        int totalDefense = viewer.equipped.GetTotalDefenseBonus();

        string result = $"=== {viewer.username}'s Equipment ===\n";

        // Show equipped items
        result += $"[HEAD] {GetEquippedItemDisplay(viewer.equipped.head)}\n";
        result += $"[CHEST] {GetEquippedItemDisplay(viewer.equipped.chest)}\n";
        result += $"[ARMS] {GetEquippedItemDisplay(viewer.equipped.arms)}\n";
        result += $"[LEGS] {GetEquippedItemDisplay(viewer.equipped.legs)}\n";
        result += $"[MAINHAND] {GetEquippedItemDisplay(viewer.equipped.mainHand)}\n";
        result += $"[OFFHAND] {GetEquippedItemDisplay(viewer.equipped.offHand)}\n";
        result += $"[FEET] {GetEquippedItemDisplay(viewer.equipped.feet)}\n";

        // Show equipment stats summary
        result += $"\n--- Equipment Stats ---\n";
        result += $"Total Damage: +{totalDamage}\n";
        result += $"Total Defense: +{totalDefense}\n";
        result += $"Total HP: {totalStats.maxHealth}\n";

        // Show inventory items
        if (viewer.inventory.Count > 0)
        {
            result += $"\n--- Inventory ({viewer.inventory.Count}/50) ---\n";

            int displayCount = Mathf.Min(10, viewer.inventory.Count);
            for (int i = 0; i < displayCount; i++)
            {
                RPGItem item = viewer.inventory[i];

                // ✅ FIX: Check for empty/null item name
                string displayName = string.IsNullOrEmpty(item.itemName) ? $"[{item.itemType}]" : item.itemName;
                string abilityTag = item.HasAbilities() ? " [ABILITY]" : "";

                // ✅ FIX: Show price for selling
                result += $"{i + 1}. {displayName} [{item.rarity}] ({item.price}c){abilityTag}\n";
            }

            if (viewer.inventory.Count > 10)
            {
                result += $"... and {viewer.inventory.Count - 10} more items\n";
            }
        }
        else
        {
            result += "\n--- Inventory: Empty ---\n";
        }

        result += $"\n💰 Coins: {viewer.coins}";
        result += $"\nUse !equip <number or name> to equip items";
        result += $"\nUse !sell <number> to sell items for 50% value"; // ✅ NEW

        return result;
    }

    private string GetEquippedItemDisplay(RPGItem item)
    {
        if (item == null) return "Empty";

        List<string> bonuses = new List<string>();
        if (item.strengthBonusPercent > 0) bonuses.Add($"+{item.strengthBonusPercent * 100:F0}% STR");
        if (item.constitutionBonusPercent > 0) bonuses.Add($"+{item.constitutionBonusPercent * 100:F0}% CON");
        if (item.dexterityBonusPercent > 0) bonuses.Add($"+{item.dexterityBonusPercent * 100:F0}% DEX");
        if (item.willpowerBonusPercent > 0) bonuses.Add($"+{item.willpowerBonusPercent * 100:F0}% WIL");
        if (item.charismaBonusPercent > 0) bonuses.Add($"+{item.charismaBonusPercent * 100:F0}% CHA");
        if (item.intelligenceBonusPercent > 0) bonuses.Add($"+{item.intelligenceBonusPercent * 100:F0}% INT");
        if (item.damageBonus > 0) bonuses.Add($"+{item.damageBonus} DMG");
        if (item.defenseBonus > 0) bonuses.Add($"+{item.defenseBonus} DEF");

        string abilityTag = item.HasAbilities() ? $" [{item.abilities[0].abilityName}]" : "";
        string bonusText = bonuses.Count > 0 ? $" ({string.Join(", ", bonuses)})" : "";

        return $"{item.itemName}{bonusText}{abilityTag}";
    }

    // ===== UPDATED EQUIP COMMAND =====
    private string HandleEquipCommand(ViewerData viewer, string[] args)
    {
        if (args.Length == 0)
        {
            return $"{viewer.username}: Usage: !equip <number or name>\nExample: !equip 1  OR  !equip iron sword";
        }

        RPGItem item = null;

        // Try to parse as number first
        if (int.TryParse(args[0], out int itemNumber))
        {
            if (itemNumber < 1 || itemNumber > viewer.inventory.Count)
            {
                return $"{viewer.username}: Invalid item number. Use !inventory to see your items.";
            }
            item = viewer.inventory[itemNumber - 1];
        }
        else
        {
            string itemName = string.Join(" ", args).ToLower();
            item = viewer.inventory.Find(i => i.itemName.ToLower() == itemName);

            if (item == null)
            {
                return $"{viewer.username}: Item '{itemName}' not found in inventory.";
            }
        }

        // Get stats BEFORE equipping
        CharacterStats statsBefore = viewer.GetTotalStats();

        // Equip the item
        if (RPGManager.Instance.EquipItem(viewer.twitchUserId, item.itemId))
        {
            // Get stats AFTER equipping
            CharacterStats statsAfter = viewer.GetTotalStats();

            // Calculate changes
            int strChange = statsAfter.strength - statsBefore.strength;
            int conChange = statsAfter.constitution - statsBefore.constitution;
            int dexChange = statsAfter.dexterity - statsBefore.dexterity;
            int wilChange = statsAfter.willpower - statsBefore.willpower;
            int chaChange = statsAfter.charisma - statsBefore.charisma;
            int intChange = statsAfter.intelligence - statsBefore.intelligence;
            int hpChange = statsAfter.maxHealth - statsBefore.maxHealth;

            string response = $"✅ {viewer.username}: Equipped {item.itemName}!\n";

            // Show stat changes
            List<string> changes = new List<string>();
            if (strChange != 0) changes.Add($"STR {FormatStatChange(strChange)}");
            if (conChange != 0) changes.Add($"CON {FormatStatChange(conChange)}");
            if (dexChange != 0) changes.Add($"DEX {FormatStatChange(dexChange)}");
            if (wilChange != 0) changes.Add($"WIL {FormatStatChange(wilChange)}");
            if (chaChange != 0) changes.Add($"CHA {FormatStatChange(chaChange)}");
            if (intChange != 0) changes.Add($"INT {FormatStatChange(intChange)}");
            if (hpChange != 0) changes.Add($"HP {FormatStatChange(hpChange)}");

            if (changes.Count > 0)
            {
                response += string.Join(" | ", changes);
            }

            return response;
        }

        return $"{viewer.username}: Failed to equip {item.itemName}. Check level requirement or class restrictions.";
    }

    /// <summary>
    /// Sell an item from inventory for 50% of its price
    /// </summary>
    private string HandleSellCommand(ViewerData viewer, string[] args)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        if (args.Length == 0)
        {
            return $"{viewer.username}: Usage: !sell <item number>\n" +
                   "Example: !sell 3\n" +
                   "Use !inventory to see your items";
        }

        // Parse item number
        if (!int.TryParse(args[0], out int itemNumber))
        {
            return $"{viewer.username}: Please provide an item number. Use !inventory to see your items.";
        }

        if (itemNumber < 1 || itemNumber > viewer.inventory.Count)
        {
            return $"{viewer.username}: Invalid item number. You have {viewer.inventory.Count} items in inventory.";
        }

        // Get the item
        RPGItem item = viewer.inventory[itemNumber - 1];

        // Check if item is currently equipped
        if (IsItemEquipped(viewer, item))
        {
            return $"{viewer.username}: You must unequip {item.itemName} before selling it!";
        }

        // Calculate sell price (50% of original)
        int sellPrice = Mathf.RoundToInt(item.price * 0.5f);
        sellPrice = Mathf.Max(1, sellPrice); // Minimum 1 coin

        // Remove item from inventory
        viewer.inventory.RemoveAt(itemNumber - 1);

        // Add coins
        viewer.coins += sellPrice;

        // Save
        RPGManager.Instance.SaveGameData();

        // Build response
        string itemName = string.IsNullOrEmpty(item.itemName) ? $"[{item.itemType}]" : item.itemName;

        return $"✅ {viewer.username}: Sold {itemName} for {sellPrice} coins (50% of {item.price}c)\n" +
               $"New balance: {viewer.coins} coins";
    }

    /// <summary>
    /// Check if an item is currently equipped
    /// </summary>
    private bool IsItemEquipped(ViewerData viewer, RPGItem item)
    {
        if (item == null) return false;

        // Check all equipment slots
        if (viewer.equipped.head != null && viewer.equipped.head.itemId == item.itemId) return true;
        if (viewer.equipped.chest != null && viewer.equipped.chest.itemId == item.itemId) return true;
        if (viewer.equipped.arms != null && viewer.equipped.arms.itemId == item.itemId) return true;
        if (viewer.equipped.legs != null && viewer.equipped.legs.itemId == item.itemId) return true;
        if (viewer.equipped.mainHand != null && viewer.equipped.mainHand.itemId == item.itemId) return true;
        if (viewer.equipped.offHand != null && viewer.equipped.offHand.itemId == item.itemId) return true;
        if (viewer.equipped.feet != null && viewer.equipped.feet.itemId == item.itemId) return true;

        return false;
    }

    private string FormatStatChange(int change)
    {
        if (change > 0) return $"+{change}";
        return change.ToString();
    }

    private string HandleUnequipCommand(ViewerData viewer, string[] args)
    {
        if (args.Length == 0)
        {
            return $"{viewer.username}: Usage: !unequip <slot>\n" +
                   "Slots: head, chest, legs, arms, mainhand, offhand, feet";
        }

        string slotName = args[0].ToLower();
        ItemType? slot = null;
        bool isMainHand = false;
        bool isOffHand = false;

        switch (slotName)
        {
            case "head":
            case "helmet":
                slot = ItemType.Helmet;
                break;
            case "chest":
            case "body":
                slot = ItemType.ChestArmor;
                break;
            case "legs":
            case "pants":
                slot = ItemType.LegArmor;
                break;
            case "arms":
            case "gloves":
            case "gauntlets":
                slot = ItemType.ArmArmor;
                break;
            case "mainhand":
            case "main":
            case "lhand":
            case "left":
            case "weapon":
                isMainHand = true;
                break;
            case "offhand":
            case "off":
            case "rhand":
            case "right":
            case "shield":
                isOffHand = true;
                break;
            case "feet":
            case "boots":
                slot = ItemType.Boots;
                break;
            default:
                return $"{viewer.username}: Invalid slot. Use: head, chest, legs, arms, mainhand, offhand, feet";
        }

        // Handle mainhand/offhand separately
        if (isMainHand)
        {
            if (viewer.equipped.mainHand == null)
            {
                return $"{viewer.username}: Nothing equipped in main hand.";
            }

            if (!viewer.AddItem(viewer.equipped.mainHand))
            {
                return $"{viewer.username}: Inventory full!";
            }

            string itemName = viewer.equipped.mainHand.itemName;
            viewer.equipped.mainHand = null;
            RPGManager.Instance.SaveGameData();
            return $"✅ {viewer.username}: Unequipped {itemName} from main hand!";
        }

        if (isOffHand)
        {
            if (viewer.equipped.offHand == null)
            {
                return $"{viewer.username}: Nothing equipped in off hand.";
            }

            if (!viewer.AddItem(viewer.equipped.offHand))
            {
                return $"{viewer.username}: Inventory full!";
            }

            string itemName = viewer.equipped.offHand.itemName;
            viewer.equipped.offHand = null;
            RPGManager.Instance.SaveGameData();
            return $"✅ {viewer.username}: Unequipped {itemName} from off hand!";
        }

        // Handle other slots
        if (slot.HasValue && RPGManager.Instance.UnequipItem(viewer.twitchUserId, slot.Value))
        {
            return $"✅ {viewer.username}: Unequipped item from {args[0]}!";
        }

        return $"{viewer.username}: No item equipped in that slot.";
    }

    // ===== UPDATED STATS COMMAND (Option 2 Format) =====
    private string HandleStatsCommand(ViewerData viewer)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        CharacterStats baseStats = viewer.baseStats;
        CharacterStats totalStats = viewer.GetTotalStats();

        int xpNeeded = 150;
        float progress = 0f;

        if (ExperienceManager.Instance != null)
        {
            xpNeeded = ExperienceManager.Instance.GetXPForNextLevel(baseStats.level);
            progress = ExperienceManager.Instance.GetLevelProgress(viewer);
        }

        string result = $"═══ {viewer.username} [{viewer.characterClass}] ═══\n";
        result += $"Level {totalStats.level} | XP: {baseStats.experience}/{xpNeeded} ({progress * 100:F1}%)\n";
        result += $"HP: {totalStats.currentHealth}/{totalStats.maxHealth}\n";

        if (baseStats.unallocatedStatPoints > 0)
        {
            result += $"⭐ Unallocated Points: {baseStats.unallocatedStatPoints}\n";
        }
        result += "\n";

        // Calculate equipment bonuses
        int strBonus = totalStats.strength - baseStats.strength;
        int conBonus = totalStats.constitution - baseStats.constitution;
        int dexBonus = totalStats.dexterity - baseStats.dexterity;
        int wilBonus = totalStats.willpower - baseStats.willpower;
        int chaBonus = totalStats.charisma - baseStats.charisma;
        int intBonus = totalStats.intelligence - baseStats.intelligence;

        // Option 2: Inline format
        result += "═══ STATS ═══\n";
        result += FormatStatLine("STR", baseStats.strength, strBonus, totalStats.strength);
        result += FormatStatLine("CON", baseStats.constitution, conBonus, totalStats.constitution);
        result += FormatStatLine("DEX", baseStats.dexterity, dexBonus, totalStats.dexterity);
        result += FormatStatLine("WIL", baseStats.willpower, wilBonus, totalStats.willpower);
        result += FormatStatLine("CHA", baseStats.charisma, chaBonus, totalStats.charisma);
        result += FormatStatLine("INT", baseStats.intelligence, intBonus, totalStats.intelligence);

        // Combat stats
        int totalDamage = viewer.equipped.GetTotalDamageBonus();
        int totalDefense = viewer.equipped.GetTotalDefenseBonus();

        if (totalDamage > 0 || totalDefense > 0)
        {
            result += "\n═══ COMBAT ═══\n";
            if (totalDamage > 0) result += $"Damage: +{totalDamage}\n";
            if (totalDefense > 0) result += $"Defense: +{totalDefense}\n";
        }

        result += $"\n💰 Coins: {viewer.coins} | Items: {viewer.inventory.Count}/50";

        return result;
    }

    private string FormatStatLine(string statName, int baseValue, int bonus, int totalValue)
    {
        if (bonus > 0)
        {
            return $"{statName}: {baseValue} (+{bonus}) = {totalValue}\n";
        }
        else if (bonus < 0)
        {
            return $"{statName}: {baseValue} ({bonus}) = {totalValue}\n";
        }
        else
        {
            return $"{statName}: {baseValue}\n";
        }
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

        int page = 1;
        if (args.Length > 0)
        {
            if (int.TryParse(args[0], out int requestedPage))
            {
                page = Mathf.Clamp(requestedPage, 1, 4);
            }
        }

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

        var purchasedItem = viewer.inventory[viewer.inventory.Count - 1];

        // ===== TRIGGER ITEM DROP VISUAL =====
        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.SpawnItemDrop(viewer.twitchUserId, purchasedItem);
        }

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

    private string HandleEnterExpedition(ViewerData viewer, string[] args)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        if (args.Length < 1)
        {
            return $"{viewer.username}: Usage: !enterexpedition <position 1-4>";
        }

        if (!int.TryParse(args[0], out int position))
        {
            return $"{viewer.username}: Position must be a number 1-4";
        }

        if (ExpeditionManager.Instance != null)
        {
            ExpeditionManager.Instance.AddParticipant(viewer.twitchUserId, viewer.username, position);
            return null; // ExpeditionManager sends its own messages
        }

        return "No expedition is currently accepting joins!";
    }

    private string HandleQueueAction(ViewerData viewer, string[] args)
    {
        if (args.Length < 1)
        {
            return $"{viewer.username}: Usage: !queue <ability> [target]\nExample: !queue quickcut";
        }

        string abilityName = args[0].ToLower();
        string targetName = args.Length >= 2 ? args[1] : null;

        if (CombatTurnManager.Instance != null)
        {
            CombatTurnManager.Instance.QueueAction(viewer.twitchUserId, viewer.username, abilityName, targetName);
            return null; // CombatTurnManager sends its own messages
        }

        return $"{viewer.username}: Not currently in combat!";
    }

    private string HandleConfirmAction(ViewerData viewer)
    {
        if (CombatTurnManager.Instance != null)
        {
            CombatTurnManager.Instance.ConfirmAction(viewer.twitchUserId, viewer.username);
            return null; // CombatTurnManager sends its own messages
        }

        return $"{viewer.username}: Not currently in combat!";
    }

    // ===== NEW ABILITIES COMMANDS =====
    private string HandleAbilitiesCommand(ViewerData viewer)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        int playerLevel = viewer.baseStats.level;
        string result = $"═══ {viewer.username}'s Abilities (Lv {playerLevel}) ═══\n";

        // Get class combat abilities
        List<AbilityData> classAbilities = new List<AbilityData>();

        if (AbilityDatabase.Instance != null)
        {
            classAbilities = AbilityDatabase.Instance.GetAbilitiesForClass(viewer.characterClass);

            if (classAbilities.Count > 0)
            {
                // Separate available and locked abilities
                List<AbilityData> availableAbilities = new List<AbilityData>();
                List<AbilityData> lockedAbilities = new List<AbilityData>();

                foreach (var ability in classAbilities)
                {
                    if (playerLevel >= ability.levelRequired)
                    {
                        availableAbilities.Add(ability);
                    }
                    else
                    {
                        lockedAbilities.Add(ability);
                    }
                }

                // Show available abilities
                if (availableAbilities.Count > 0)
                {
                    result += "\n[UNLOCKED ABILITIES]\n";
                    foreach (var ability in availableAbilities)
                    {
                        string levelTag = ability.levelRequired > 1 ? $" [Lv{ability.levelRequired}]" : "";
                        result += $"✓ {ability.abilityName}{levelTag} (!queue {ability.commandName})\n";
                    }
                }

                // Show locked abilities
                if (lockedAbilities.Count > 0)
                {
                    result += "\n[LOCKED ABILITIES]\n";
                    foreach (var ability in lockedAbilities.OrderBy(a => a.levelRequired))
                    {
                        result += $"🔒 {ability.abilityName} [Requires Lv{ability.levelRequired}]\n";
                    }
                }
            }
        }

        // Get abilities from equipped items
        List<ItemAbility> equippedAbilities = new List<ItemAbility>();
        RPGItem[] allEquipped = {
        viewer.equipped.head, viewer.equipped.chest, viewer.equipped.arms,
        viewer.equipped.legs, viewer.equipped.mainHand, viewer.equipped.offHand,
        viewer.equipped.feet
    };

        foreach (var item in allEquipped)
        {
            if (item != null && item.HasAbilities())
            {
                equippedAbilities.AddRange(item.abilities);
            }
        }

        if (equippedAbilities.Count > 0)
        {
            result += "\n[ITEM ABILITIES]\n";
            foreach (var ability in equippedAbilities)
            {
                result += $"⚡ {ability.abilityName}\n";
            }
        }

        if (classAbilities.Count == 0 && equippedAbilities.Count == 0)
        {
            result += "\nNo abilities available.\n";
        }

        result += "\nUse !ability <name> for details";

        return result;
    }

    /// <summary>
    /// Show current ability loadout
    /// </summary>
    private string HandleLoadoutCommand(ViewerData viewer)
    {
        if (viewer.characterClass == CharacterClass.None)
            return $"{viewer.username}: Choose a class first with !class";

        string result = $"═══ {viewer.username}'s Loadout [{viewer.characterClass}] ═══\n";

        if (viewer.equippedAbilities.Count == 0)
        {
            result += "No abilities equipped!\n";
            result += "Use !equipability <name> to add up to 4.\n";
            result += "Use !abilities to see what's available.";
            return result;
        }

        result += $"Slots {viewer.equippedAbilities.Count}/4  |  !unequipa <#> to remove\n";
        result += "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";

        for (int i = 0; i < viewer.equippedAbilities.Count; i++)
        {
            string abilityCmd = viewer.equippedAbilities[i];
            AbilityData ability = AbilityDatabase.Instance?.GetAbility(abilityCmd);

            if (ability != null)
            {
                string cost = BuildAbilityCostString(ability, viewer.characterClass);
                string cooldownText = ability.cooldown > 0 ? $"  CD:{ability.cooldown}" : "";
                string icon = GetCategoryIcon(ability.category);

                result += $"[{i + 1}] {icon} {ability.abilityName}\n";
                result += $"     !q {abilityCmd}{(cost.Length > 0 ? $"  {cost}" : "")}{cooldownText}\n";
            }
            else
            {
                result += $"[{i + 1}] ??? {abilityCmd}  (not found)\n";
            }
        }

        if (!string.IsNullOrEmpty(viewer.equippedItemAbility))
        {
            AbilityData itemAbility = AbilityDatabase.Instance?.GetAbility(viewer.equippedItemAbility);
            string name = itemAbility != null ? itemAbility.abilityName : viewer.equippedItemAbility;
            result += "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";
            result += $"[⚡] {name}  (!q {viewer.equippedItemAbility})\n";
        }

        result += "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";
        result += "!equipability <name>  !abilities";
        return result;
    }

    private string BuildAbilityCostString(AbilityData ability, CharacterClass charClass)
    {
        var parts = new List<string>();
        switch (charClass)
        {
            case CharacterClass.Mage:
                if (ability.manaCost > 0) parts.Add($"{ability.manaCost}MP"); break;
            case CharacterClass.Cleric:
                if (ability.wrathCost > 0) parts.Add($"{ability.wrathCost}WR");
                if (ability.wrathGain > 0) parts.Add($"+{ability.wrathGain}WR"); break;
            case CharacterClass.Rogue:
                if (ability.sneakCost > 0) parts.Add($"{ability.sneakCost}SP");
                if (ability.sneakGain > 0) parts.Add($"+{ability.sneakGain}SP"); break;
            case CharacterClass.Ranger:
                if (ability.balanceCost > 0) parts.Add($"-{ability.balanceCost}BAL");
                if (ability.balanceGain > 0) parts.Add($"+{ability.balanceGain}BAL"); break;
        }
        return string.Join(" ", parts);
    }

    private string GetCategoryIcon(AbilityCategory category)
    {
        switch (category)
        {
            case AbilityCategory.Damage: return "⚔";
            case AbilityCategory.Heal: return "💚";
            case AbilityCategory.Buff: return "✨";
            case AbilityCategory.Debuff: return "💀";
            default: return "◆";
        }
    }

    /// <summary>
    /// Equip an ability to loadout (max 4)
    /// </summary>
    private string HandleEquipAbilityCommand(ViewerData viewer, string[] args)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        if (args.Length == 0)
        {
            return $"{viewer.username}: Usage: !equipability <ability name>\n" +
                   "Example: !equipability strike\n" +
                   "Use !abilities to see available abilities";
        }

        // Check if at max capacity
        if (viewer.equippedAbilities.Count >= 4)
        {
            return $"{viewer.username}: Loadout is full! (4/4)\n" +
                   "Use !unequipability <number> to remove an ability first";
        }

        string abilityName = args[0].ToLower();

        // Find ability by command name
        AbilityData ability = AbilityDatabase.Instance?.GetAbility(abilityName);

        if (ability == null)
        {
            return $"{viewer.username}: Ability '{abilityName}' not found.\n" +
                   "Use !abilities to see available abilities";
        }

        // Check class requirement
        if (ability.requiredClass != viewer.characterClass)
        {
            return $"{viewer.username}: You can't use {ability.abilityName}! " +
                   $"(Requires {ability.requiredClass})";
        }

        // Check level requirement
        if (viewer.baseStats.level < ability.levelRequired)
        {
            return $"{viewer.username}: {ability.abilityName} requires level {ability.levelRequired}.\n" +
                   $"You are level {viewer.baseStats.level}.";
        }

        // Check if already equipped
        if (viewer.equippedAbilities.Contains(ability.commandName))
        {
            return $"{viewer.username}: {ability.abilityName} is already equipped!";
        }

        // Equip it
        viewer.equippedAbilities.Add(ability.commandName);
        RPGManager.Instance.SaveGameData();

        return $"✅ {viewer.username}: Equipped {ability.abilityName} to loadout!\n" +
               $"Slots: {viewer.equippedAbilities.Count}/4";
    }

    /// <summary>
    /// Unequip an ability from loadout by slot number
    /// </summary>
    private string HandleUnequipAbilityCommand(ViewerData viewer, string[] args)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        if (args.Length == 0)
        {
            return $"{viewer.username}: Usage: !unequipability <slot number>\n" +
                   "Example: !unequipability 2\n" +
                   "Use !loadout to see equipped abilities";
        }

        if (!int.TryParse(args[0], out int slotNumber))
        {
            return $"{viewer.username}: Please provide a valid slot number (1-4)";
        }

        if (slotNumber < 1 || slotNumber > viewer.equippedAbilities.Count)
        {
            return $"{viewer.username}: Invalid slot number.\n" +
                   $"You have {viewer.equippedAbilities.Count} abilities equipped.\n" +
                   "Use !loadout to see them.";
        }

        // Get the ability name before removing
        string removedCmd = viewer.equippedAbilities[slotNumber - 1];
        AbilityData ability = AbilityDatabase.Instance?.GetAbility(removedCmd);
        string abilityName = ability != null ? ability.abilityName : removedCmd;

        // Remove it
        viewer.equippedAbilities.RemoveAt(slotNumber - 1);
        RPGManager.Instance.SaveGameData();

        return $"{viewer.username}: Removed {abilityName} from loadout\n" +
               $"Slots: {viewer.equippedAbilities.Count}/4";
    }

    private string HandleAbilityDetailsCommand(ViewerData viewer, string[] args)
    {
        if (args.Length == 0)
        {
            return $"{viewer.username}: Usage: !ability <name>\nExample: !ability backstab";
        }

        string abilityName = string.Join(" ", args).ToLower();

        // Search equipped items for the ability
        RPGItem[] allEquipped = {
            viewer.equipped.head, viewer.equipped.chest, viewer.equipped.arms,
            viewer.equipped.legs, viewer.equipped.mainHand, viewer.equipped.offHand,
            viewer.equipped.feet
        };

        foreach (var item in allEquipped)
        {
            if (item != null && item.HasAbilities())
            {
                foreach (var ability in item.abilities)
                {
                    if (ability.abilityName.ToLower() == abilityName)
                    {
                        string result = $"═══ {ability.abilityName} ═══\n";
                        result += $"{ability.abilityDescription}\n\n";

                        if (ability.manaCost > 0)
                            result += $"Cost: {ability.manaCost} {GetResourceName(viewer.characterClass)}\n";

                        if (ability.cooldownTurns > 0)
                            result += $"Cooldown: {ability.cooldownTurns} turns\n";

                        result += $"\nSource: {item.itemName}";
                        return result;
                    }
                }
            }
        }

        return $"{viewer.username}: Ability '{abilityName}' not found. Use !abilities to see available abilities.";
    }



    private string GetResourceName(CharacterClass charClass)
    {
        switch (charClass)
        {
            case CharacterClass.Rogue: return "Sneak";
            case CharacterClass.Mage: return "Mana";
            case CharacterClass.Cleric: return "Wrath";
            case CharacterClass.Ranger: return "Balance";
            case CharacterClass.Fighter: return "Stance";
            default: return "Resource";
        }
    }

    private string HandleStanceCommand(string userId, string username, string[] args)
    {
        // Check if in combat
        if (!CombatTurnManager.Instance.combatActive)
        {
            return "You can only change stance during combat!";
        }

        // Get player's combat entity
        OnScreenCharacter character = CharacterSpawner.Instance?.GetCharacter(userId);
        if (character == null)
            return "You're not in the expedition!";

        CombatEntity entity = character.GetComponent<CombatEntity>();
        if (entity == null)
            return "You're not in combat!";

        // Check if player is a Fighter
        if (entity.characterClass != CharacterClass.Fighter)
        {
            return "Only Fighters can use stances!";
        }

        // Check if it's player's turn
        if (!CombatTurnManager.Instance.playerTurn)
        {
            return "You can only change stance during the player turn phase!";
        }

        // Show current stance if no args
        if (args.Length < 1)
        {
            return $"Current: {GetStanceDescription(entity.currentStance)}\\n" +
                   "!stance aggressive - +10% STR\\n" +
                   "!stance defensive - +10% CON\\n" +
                   "!stance reflective - +10 DEF";
        }

        // Parse stance
        FighterStance newStance;
        switch (args[0].ToLower())
        {
            case "aggressive":
            case "agg":
                newStance = FighterStance.Aggressive;
                break;
            case "defensive":
            case "def":
                newStance = FighterStance.Defensive;
                break;
            case "reflective":
            case "ref":
                newStance = FighterStance.Reflective;
                break;
            default:
                return $"Unknown stance: {args[0]}";
        }

        // Check if already in this stance
        if (entity.currentStance == newStance)
        {
            return $"You're already in {GetStanceName(newStance)} stance!";
        }

        // Change stance (FREE ACTION)
        bool success = entity.ChangeStance(newStance);

        if (success)
        {
            return $"✓ {GetStanceName(newStance)} Stance! {entity.GetCurrentStanceBonusText()}";
        }

        return "Failed to change stance!";
    }

    private string GetStanceDescription(FighterStance stance)
    {
        switch (stance)
        {
            case FighterStance.Aggressive: return "Aggressive (+10% STR)";
            case FighterStance.Defensive: return "Defensive (+10% CON)";
            case FighterStance.Reflective: return "Reflective (+10 DEF)";
            default: return "None";
        }
    }

    private string GetStanceName(FighterStance stance)
    {
        switch (stance)
        {
            case FighterStance.Aggressive: return "Aggressive";
            case FighterStance.Defensive: return "Defensive";
            case FighterStance.Reflective: return "Reflective";
            default: return "None";
        }
    }

    private string HandleStancesCommand(string userId)
    {
        ViewerData viewer = RPGManager.Instance?.GetViewer(userId);
        if (viewer == null)
            return "Viewer data not found!";

        if (viewer.characterClass != CharacterClass.Fighter)
        {
            return "Only Fighters can use stances!";
        }

        return "═══ FIGHTER STANCES ═══\\n" +
               "⚔️ Aggressive: +10% Strength\\n" +
               "🛡️ Defensive: +10% Constitution\\n" +
               "✨ Reflective: +10 Defense\\n\\n" +
               "Change with: !stance <type>";
    }

    private string HandleHelpCommand(ViewerData viewer)
    {
        return "=== RPG Commands ===\n" +
               "!class <rogue/fighter/mage/cleric/ranger> - Choose class\n" +
               "!stats - View character stats\n" +
               "!inventory - View equipment & items\n" +
               "!equip <number or name> - Equip item\n" +
               "!unequip <slot> - Unequip item\n" +
               "!shop - Browse shop items\n" +
               "!shop <page> - View specific shop page\n" +
               "!buy <item name> - Purchase item\n" +
               "!sell <number> - Sell item for 50% value\n" + // ✅ NEW
               "!abilities - List your class abilities\n" +
               "!ability <n> - View ability details\n" +
               "!levelup <stat> - Spend stat points\n" +
               "!coins - Check coin balance\n" +
               "!trade - Trade with other players";
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
            case "rpgtestlevelup":
                return HandleAdminTestLevelUp(args);

            case "cancelexpedition":
                return HandleCancelExpedition();
            default:
                return null;
        }
    }

    private string HandleStartExpedition(string[] args)
    {
        if (args.Length < 1)
        {
            return "Usage: !startexpedition <easy/medium/hard/deadly> [theme]\n" +
                   "Example: !startexpedition easy forest";
        }

        string difficultyStr = args[0].ToLower();
        ExpeditionDifficulty difficulty;

        switch (difficultyStr)
        {
            case "easy":
                difficulty = ExpeditionDifficulty.Easy;
                break;
            case "medium":
                difficulty = ExpeditionDifficulty.Medium;
                break;
            case "hard":
                difficulty = ExpeditionDifficulty.Hard;
                break;
            case "deadly":
                difficulty = ExpeditionDifficulty.Deadly;
                break;
            default:
                return "Invalid difficulty! Use: easy, medium, hard, or deadly";
        }

        // Check for optional theme parameter
        string theme = args.Length >= 2 ? args[1] : null;

        if (ExpeditionManager.Instance != null)
        {
            ExpeditionManager.Instance.QueueExpedition(difficulty, theme);
            return null; // ExpeditionManager sends its own message
        }

        return "Expedition system not available!";
    }

    private string HandleCancelExpedition()
    {
        if (ExpeditionManager.Instance != null)
        {
            ExpeditionManager.Instance.CancelExpedition();
            return "Expedition cancelled.";
        }
        return "No active expedition to cancel.";
    }

    // ===== NEW ADMIN COMMAND FOR TESTING LEVELUP =====
    private string HandleAdminTestLevelUp(string[] args)
    {
        if (args.Length < 1)
        {
            return "Usage: !rpgtestlevelup @username\n" +
                   "Gives user enough XP to level up for testing";
        }

        string targetUsername = args[0].TrimStart('@').ToLower();
        ViewerData targetViewer = FindViewerByUsername(targetUsername);

        if (targetViewer == null)
        {
            return $"Error: User '{targetUsername}' not found in database.";
        }

        if (ExperienceManager.Instance == null)
        {
            return "Error: ExperienceManager not found!";
        }

        // Give enough XP to level up
        int xpNeeded = ExperienceManager.Instance.GetXPForNextLevel(targetViewer.baseStats.level);
        int xpToGive = xpNeeded - targetViewer.baseStats.experience + 1;

        ExperienceManager.Instance.AddExperience(targetViewer.twitchUserId, xpToGive);

        return $"✓ Gave {xpToGive} XP to {targetViewer.username}\n" +
               $"They should now be Level {targetViewer.baseStats.level}\n" +
               $"Unallocated points: {targetViewer.baseStats.unallocatedStatPoints}";
    }

    private string HandleAdminGive(string[] args)
    {
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
                           $"Current Level: {targetViewer.baseStats.level}\n" +
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

        targetViewer.isDead = true;
        targetViewer.deathLockoutUntil = System.DateTime.Now.AddMinutes(30);
        targetViewer.baseStats.currentHealth = 0;
        targetViewer.baseStats.experience = 0;

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
               "!rpgtestlevelup @user - Give XP to level up\n" +
               "!rpgban @user - Ban from RPG\n" +
               "!rpgunban @user - Unban user\n" +
               "!rpgkill @user - Kill player (30 min lockout)\n" +
               "!rpgreset @user - Reset character (DELETES PROGRESS!)\n" +
               "!rpggiveitem @user <item name> - Give named item";
    }

    private string HandleLinkDiscordCommand(ViewerData viewer, string[] args)
    {
        // Usage: !linkdiscord <discord_user_id>
        // The Discord User ID is an 18-digit number the viewer gets by right-clicking
        // their Discord username with Developer Mode enabled.

        if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
        {
            return $"{viewer.username}: Usage: !linkdiscord <your_discord_id>\n" +
                   "To find your Discord ID:\n" +
                   "  1. Discord Settings → Advanced → Enable Developer Mode\n" +
                   "  2. Right-click your username → Copy User ID\n" +
                   "Then type: !linkdiscord 123456789012345678";
        }

        // Delegate to DiscordBridgeServer which owns the mapping file
        return DiscordBridgeServer.RegisterTwitchLink(
            viewer.twitchUserId,
            viewer.username,
            args[0]);
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

        ViewerData targetViewer = FindViewerByUsername(targetUsername);
        if (targetViewer == null)
        {
            return $"Error: User '{targetUsername}' not found.";
        }

        if (HybridItemSystem.Instance == null)
        {
            return "Item system not available!";
        }

        RPGItem item = HybridItemSystem.Instance.GetNamedItem(itemName);
        if (item == null)
        {
            return $"Error: Item '{itemName}' not found.\nTip: This only works with hand-crafted named items.";
        }

        if (targetViewer.inventory.Count >= 50)
        {
            return $"Error: {targetViewer.username}'s inventory is full!";
        }

        RPGItem giftedItem = new RPGItem
        {
            itemName = item.itemName,
            description = item.description,
            itemType = item.itemType,
            rarity = item.rarity,
            requiredLevel = item.requiredLevel,
            price = item.price,
            isTwoHanded = item.isTwoHanded,

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
            properties = new Dictionary<string, string>(item.properties),
            abilities = new List<ItemAbility>()
        };

        // Copy abilities if they exist
        if (item.HasAbilities())
        {
            foreach (var ability in item.abilities)
            {
                giftedItem.abilities.Add(new ItemAbility
                {
                    abilityName = ability.abilityName,
                    abilityDescription = ability.abilityDescription,
                    abilityCommand = ability.abilityCommand,
                    manaCost = ability.manaCost,
                    cooldownTurns = ability.cooldownTurns
                });
            }
        }

        targetViewer.AddItem(giftedItem);
        RPGManager.Instance.SaveGameData();

        // ===== TRIGGER ITEM DROP VISUAL =====
        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.SpawnItemDrop(targetViewer.twitchUserId, giftedItem);
        }

        return $"✓ Gave {item.itemName} [{item.rarity}] to {targetViewer.username}!";
    }

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

    private string HandleTradeCommand(ViewerData viewer, string[] args)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        if (args.Length < 2)
        {
            return $"{viewer.username}: Usage:\n" +
                   "!trade @username coins <amount> - Give coins\n" +
                   "!trade @username item <name> - Give item\n" +
                   "!trade @username offer:<item/coins> want:<item/coins> - Trade offer\n" +
                   "Examples:\n" +
                   "  !trade @alice coins 100\n" +
                   "  !trade @bob item iron sword\n" +
                   "  !trade @carol offer:steel axe want:100";
        }

        if (TradeManager.Instance == null)
        {
            return "Trade system not available!";
        }

        string targetUsername = args[0].TrimStart('@');

        // SIMPLE COMMANDS: coins or item
        if (args.Length >= 2 && args[1].ToLower() == "coins")
        {
            if (args.Length < 3)
            {
                return $"{viewer.username}: !trade @{targetUsername} coins <amount>";
            }

            if (!int.TryParse(args[2], out int amount))
            {
                return $"{viewer.username}: Invalid coin amount!";
            }

            return TradeManager.Instance.GiveCoins(viewer.twitchUserId, viewer.username, targetUsername, amount);
        }

        if (args.Length >= 2 && args[1].ToLower() == "item")
        {
            if (args.Length < 3)
            {
                return $"{viewer.username}: !trade @{targetUsername} item <name>";
            }

            string itemName = string.Join(" ", args.Skip(2));
            return TradeManager.Instance.GiveItem(viewer.twitchUserId, viewer.username, targetUsername, itemName);
        }

        // TRADE OFFER SYSTEM: offer:X want:Y
        string offerItemName = null;
        string wantItemName = null;
        int offerCoins = 0;
        int wantCoins = 0;

        foreach (string arg in args.Skip(1))
        {
            if (arg.StartsWith("offer:", StringComparison.OrdinalIgnoreCase))
            {
                string value = arg.Substring(6);
                if (int.TryParse(value, out int coins))
                {
                    offerCoins = coins;
                }
                else
                {
                    offerItemName = value;
                }
            }
            else if (arg.StartsWith("want:", StringComparison.OrdinalIgnoreCase))
            {
                string value = arg.Substring(5);
                if (int.TryParse(value, out int coins))
                {
                    wantCoins = coins;
                }
                else
                {
                    wantItemName = value;
                }
            }
        }

        // Validate we have something to offer and want
        if (string.IsNullOrEmpty(offerItemName) && offerCoins == 0)
        {
            return $"{viewer.username}: You must offer something! Use offer:<item or coins>";
        }

        if (string.IsNullOrEmpty(wantItemName) && wantCoins == 0)
        {
            return $"{viewer.username}: You must want something! Use want:<item or coins>";
        }

        return TradeManager.Instance.CreateTradeOffer(
            viewer.twitchUserId,
            viewer.username,
            targetUsername,
            offerItemName,
            wantItemName,
            offerCoins,
            wantCoins
        );
    }

    private string HandleGiveCommand(ViewerData viewer, string[] args)
    {
        // Alternative simpler syntax: !give @user coins 100  OR  !give @user iron sword
        if (args.Length < 2)
        {
            return $"{viewer.username}: Usage:\n" +
                   "!give @username coins <amount>\n" +
                   "!give @username <item name>";
        }

        if (TradeManager.Instance == null)
        {
            return "Trade system not available!";
        }

        string targetUsername = args[0].TrimStart('@');

        // Check if second arg is "coins"
        if (args[1].ToLower() == "coins")
        {
            if (args.Length < 3)
            {
                return $"{viewer.username}: !give @{targetUsername} coins <amount>";
            }

            if (!int.TryParse(args[2], out int amount))
            {
                return $"{viewer.username}: Invalid coin amount!";
            }

            return TradeManager.Instance.GiveCoins(viewer.twitchUserId, viewer.username, targetUsername, amount);
        }

        // Otherwise it's an item name
        string itemName = string.Join(" ", args.Skip(1));
        return TradeManager.Instance.GiveItem(viewer.twitchUserId, viewer.username, targetUsername, itemName);
    }

    private string HandleAcceptTradeCommand(ViewerData viewer, string[] args)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        if (args.Length < 1)
        {
            return $"{viewer.username}: Usage: !accepttrade @username";
        }

        if (TradeManager.Instance == null)
        {
            return "Trade system not available!";
        }

        string initiatorUsername = args[0].TrimStart('@');
        return TradeManager.Instance.AcceptTrade(viewer.twitchUserId, viewer.username, initiatorUsername);
    }

    private string HandleCancelTradeCommand(ViewerData viewer)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        if (TradeManager.Instance == null)
        {
            return "Trade system not available!";
        }

        return TradeManager.Instance.CancelTrade(viewer.twitchUserId, viewer.username);
    }

    private string HandleTradeHistoryCommand(ViewerData viewer, string[] args)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        if (TradeManager.Instance == null)
        {
            return "Trade system not available!";
        }

        int count = 10;
        if (args.Length > 0 && int.TryParse(args[0], out int requestedCount))
        {
            count = Mathf.Clamp(requestedCount, 1, 20);
        }

        return TradeManager.Instance.GetTradeHistory(viewer.twitchUserId, count);
    }

    // ==================== PVP COMMANDS ====================

    private string HandleChallengeCommand(ViewerData viewer, string[] args)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        if (args.Length < 2)
        {
            return $"{viewer.username}: Usage: !challenge @username <coins>\n" +
                   "Example: !challenge @alice 100";
        }

        string targetUsername = args[0].TrimStart('@');

        if (!int.TryParse(args[1], out int wager))
        {
            return $"{viewer.username}: Wager must be a number!";
        }

        if (PvPManager.Instance == null)
        {
            return "PvP system not available!";
        }

        string result = PvPManager.Instance.CreateChallenge(viewer.twitchUserId, viewer.username, targetUsername, wager);
        return result; // Can be null if notification already sent
    }

    private string HandleAcceptCommand(ViewerData viewer)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        if (PvPManager.Instance == null)
        {
            return "PvP system not available!";
        }

        return PvPManager.Instance.AcceptChallenge(viewer.twitchUserId, viewer.username);
    }

    private string HandleDeclineCommand(ViewerData viewer)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        if (PvPManager.Instance == null)
        {
            return "PvP system not available!";
        }

        return PvPManager.Instance.DeclineChallenge(viewer.twitchUserId, viewer.username);
    }

    private string HandleBetCommand(ViewerData viewer, string[] args)
    {
        if (viewer.characterClass == CharacterClass.None)
        {
            return $"{viewer.username}: Choose a class first with !class";
        }

        if (args.Length < 1)
        {
            return $"{viewer.username}: Usage: !bet @username\n" +
                   "Example: !bet @alice";
        }

        string fighterUsername = args[0].TrimStart('@');

        if (PvPManager.Instance == null)
        {
            return "PvP system not available!";
        }

        return PvPManager.Instance.PlaceBet(viewer.twitchUserId, viewer.username, fighterUsername);
    }

    private string HandlePvPStatsCommand(string[] args)
    {
        if (PvPManager.Instance == null)
        {
            return "PvP system not available!";
        }

        string targetUsername = args.Length > 0 ? args[0].TrimStart('@') : null;

        // If no username provided, must be used by someone with a class
        if (targetUsername == null)
        {
            return "Usage: !pvpstats @username\nExample: !pvpstats @alice";
        }

        return PvPManager.Instance.GetPvPStats(targetUsername);
    }

    private string HandlePvPLeaderboardCommand()
    {
        if (PvPManager.Instance == null)
        {
            return "PvP system not available!";
        }

        return PvPManager.Instance.GetPvPLeaderboard();
    }
}

