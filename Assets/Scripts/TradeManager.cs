using System;
using System.Collections.Generic;
using UnityEngine;

public class TradeManager : MonoBehaviour
{
    private static TradeManager _instance;
    public static TradeManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<TradeManager>();
            }
            return _instance;
        }
    }

    [Header("Trade Settings")]
    [SerializeField] private float tradeOfferDurationSeconds = 60f;
    [SerializeField] private int maxTradeHistoryPerPlayer = 50;

    private Dictionary<string, PendingTrade> pendingTrades = new Dictionary<string, PendingTrade>();

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
        // Check for expired trades
        List<string> expiredTrades = new List<string>();

        foreach (var kvp in pendingTrades)
        {
            if (DateTime.Now >= kvp.Value.expiresAt)
            {
                expiredTrades.Add(kvp.Key);
            }
        }

        // Remove expired trades
        foreach (string userId in expiredTrades)
        {
            PendingTrade trade = pendingTrades[userId];
            ViewerData initiator = RPGManager.Instance.GetViewer(userId);

            if (initiator != null)
            {
                OnScreenNotification.Instance?.ShowError(
                    $"{initiator.username}'s trade offer to {trade.targetUsername} expired."
                );
            }

            pendingTrades.Remove(userId);
            Debug.Log($"[Trade] Trade from {userId} expired");
        }
    }

    // ==================== DIRECT GIVE (NO CONFIRMATION) ====================

    public string GiveCoins(string fromUserId, string fromUsername, string targetUsername, int amount)
    {
        // Validate sender
        ViewerData sender = RPGManager.Instance.GetViewer(fromUserId);
        if (sender == null || !sender.CanTakeAction())
        {
            return GetCannotTradeReason(sender, fromUsername);
        }

        // Validate amount
        if (amount <= 0)
        {
            return $"{fromUsername}: Amount must be positive!";
        }

        if (!sender.CanAfford(amount))
        {
            return $"{fromUsername}: You only have {sender.coins} coins!";
        }

        // Find target
        ViewerData target = FindViewerByUsername(targetUsername);
        if (target == null)
        {
            return $"{fromUsername}: User '{targetUsername}' not found.";
        }

        if (!target.CanTakeAction())
        {
            return $"{fromUsername}: {target.username} cannot receive trades right now.";
        }

        // Execute transfer
        sender.coins -= amount;
        target.coins += amount;

        // Log trade
        LogTrade(sender, target, null, null, amount, 0);

        RPGManager.Instance.SaveGameData();

        return $"✅ {fromUsername} gave {amount} coins to {target.username}!";
    }

    public string GiveItem(string fromUserId, string fromUsername, string targetUsername, string itemName)
    {
        // Validate sender
        ViewerData sender = RPGManager.Instance.GetViewer(fromUserId);
        if (sender == null || !sender.CanTakeAction())
        {
            return GetCannotTradeReason(sender, fromUsername);
        }

        // Find item
        RPGItem item = sender.inventory.Find(i => i.itemName.ToLower() == itemName.ToLower());
        if (item == null)
        {
            return $"{fromUsername}: You don't have '{itemName}' in your inventory.";
        }

        // Find target
        ViewerData target = FindViewerByUsername(targetUsername);
        if (target == null)
        {
            return $"{fromUsername}: User '{targetUsername}' not found.";
        }

        if (!target.CanTakeAction())
        {
            return $"{fromUsername}: {target.username} cannot receive trades right now.";
        }

        // Check target inventory space
        if (target.inventory.Count >= 50)
        {
            return $"{fromUsername}: {target.username}'s inventory is full!";
        }

        // Execute transfer
        sender.RemoveItem(item.itemId);
        target.AddItem(item);

        // Log trade
        LogTrade(sender, target, item, null, 0, 0);

        RPGManager.Instance.SaveGameData();

        // Spawn item drop visual
        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.SpawnItemDrop(target.twitchUserId, item);
        }

        return $"✅ {fromUsername} gave {item.itemName} to {target.username}!";
    }

    // ==================== TRADE OFFERS (REQUIRES CONFIRMATION) ====================

    public string CreateTradeOffer(string fromUserId, string fromUsername, string targetUsername,
                                   string offerItemName, string wantItemName, int offerCoins, int wantCoins)
    {
        // Validate sender
        ViewerData sender = RPGManager.Instance.GetViewer(fromUserId);
        if (sender == null || !sender.CanTakeAction())
        {
            return GetCannotTradeReason(sender, fromUsername);
        }

        // Check if already has pending trade
        if (pendingTrades.ContainsKey(fromUserId))
        {
            return $"{fromUsername}: You already have a pending trade. Cancel it first with !canceltrade";
        }

        // Find target
        ViewerData target = FindViewerByUsername(targetUsername);
        if (target == null)
        {
            return $"{fromUsername}: User '{targetUsername}' not found.";
        }

        if (!target.CanTakeAction())
        {
            return $"{fromUsername}: {target.username} cannot trade right now.";
        }

        // Validate what we're offering
        RPGItem offerItem = null;
        if (!string.IsNullOrEmpty(offerItemName))
        {
            offerItem = sender.inventory.Find(i => i.itemName.ToLower() == offerItemName.ToLower());
            if (offerItem == null)
            {
                return $"{fromUsername}: You don't have '{offerItemName}' in your inventory.";
            }
        }

        if (offerCoins > 0 && !sender.CanAfford(offerCoins))
        {
            return $"{fromUsername}: You only have {sender.coins} coins!";
        }

        // Validate what we're requesting
        RPGItem wantItem = null;
        if (!string.IsNullOrEmpty(wantItemName))
        {
            wantItem = target.inventory.Find(i => i.itemName.ToLower() == wantItemName.ToLower());
            if (wantItem == null)
            {
                return $"{fromUsername}: {target.username} doesn't have '{wantItemName}'.";
            }
        }

        if (wantCoins > 0 && !target.CanAfford(wantCoins))
        {
            return $"{fromUsername}: {target.username} only has {target.coins} coins.";
        }

        // Check inventory space
        if (wantItem != null && sender.inventory.Count >= 50)
        {
            return $"{fromUsername}: Your inventory is full!";
        }

        if (offerItem != null && target.inventory.Count >= 50)
        {
            return $"{fromUsername}: {target.username}'s inventory is full!";
        }

        // Create pending trade
        PendingTrade trade = new PendingTrade
        {
            initiatorId = fromUserId,
            initiatorUsername = fromUsername,
            targetId = target.twitchUserId,
            targetUsername = target.username,
            offerItem = offerItem,
            wantItem = wantItem,
            offerCoins = offerCoins,
            wantCoins = wantCoins,
            createdAt = DateTime.Now,
            expiresAt = DateTime.Now.AddSeconds(tradeOfferDurationSeconds)
        };

        pendingTrades[fromUserId] = trade;

        // Build offer description
        string offerDesc = BuildTradeDescription(offerItem, offerCoins, "offering");
        string wantDesc = BuildTradeDescription(wantItem, wantCoins, "wants");

        string notification = $"📦 TRADE OFFER\n" +
                            $"{fromUsername} → {target.username}\n" +
                            $"Offering: {offerDesc}\n" +
                            $"Wants: {wantDesc}\n" +
                            $"{target.username}, use !accepttrade @{fromUsername} to accept!\n" +
                            $"Expires in {tradeOfferDurationSeconds} seconds";

        OnScreenNotification.Instance?.ShowInfo(notification);

        return $"✅ Trade offer sent to {target.username}!";
    }

    public string AcceptTrade(string acceptorUserId, string acceptorUsername, string initiatorUsername)
    {
        // Find the initiator
        ViewerData initiator = FindViewerByUsername(initiatorUsername);
        if (initiator == null)
        {
            return $"{acceptorUsername}: User '{initiatorUsername}' not found.";
        }

        // Check if trade exists
        if (!pendingTrades.ContainsKey(initiator.twitchUserId))
        {
            return $"{acceptorUsername}: No pending trade from {initiator.username}.";
        }

        PendingTrade trade = pendingTrades[initiator.twitchUserId];

        // Verify this person is the target
        if (trade.targetId != acceptorUserId)
        {
            return $"{acceptorUsername}: That trade isn't for you!";
        }

        // Validate both parties can still trade
        ViewerData acceptor = RPGManager.Instance.GetViewer(acceptorUserId);
        ViewerData sender = RPGManager.Instance.GetViewer(initiator.twitchUserId);

        if (sender == null || !sender.CanTakeAction())
        {
            pendingTrades.Remove(initiator.twitchUserId);
            return $"{acceptorUsername}: {initiator.username} cannot trade anymore.";
        }

        if (acceptor == null || !acceptor.CanTakeAction())
        {
            return GetCannotTradeReason(acceptor, acceptorUsername);
        }

        // Re-validate items and coins still exist
        if (trade.offerItem != null && !sender.inventory.Contains(trade.offerItem))
        {
            pendingTrades.Remove(initiator.twitchUserId);
            return $"{acceptorUsername}: {initiator.username} no longer has {trade.offerItem.itemName}!";
        }

        if (trade.wantItem != null && !acceptor.inventory.Contains(trade.wantItem))
        {
            pendingTrades.Remove(initiator.twitchUserId);
            return $"{acceptorUsername}: You no longer have {trade.wantItem.itemName}!";
        }

        if (trade.offerCoins > 0 && !sender.CanAfford(trade.offerCoins))
        {
            pendingTrades.Remove(initiator.twitchUserId);
            return $"{acceptorUsername}: {initiator.username} no longer has {trade.offerCoins} coins!";
        }

        if (trade.wantCoins > 0 && !acceptor.CanAfford(trade.wantCoins))
        {
            pendingTrades.Remove(initiator.twitchUserId);
            return $"{acceptorUsername}: You no longer have {trade.wantCoins} coins!";
        }

        // Check inventory space
        if (trade.wantItem != null && sender.inventory.Count >= 50)
        {
            pendingTrades.Remove(initiator.twitchUserId);
            return $"{acceptorUsername}: {initiator.username}'s inventory is full!";
        }

        if (trade.offerItem != null && acceptor.inventory.Count >= 50)
        {
            pendingTrades.Remove(initiator.twitchUserId);
            return $"{acceptorUsername}: Your inventory is full!";
        }

        // EXECUTE TRADE
        // Transfer items
        if (trade.offerItem != null)
        {
            sender.RemoveItem(trade.offerItem.itemId);
            acceptor.AddItem(trade.offerItem);

            if (ItemDropManager.Instance != null)
            {
                ItemDropManager.Instance.SpawnItemDrop(acceptorUserId, trade.offerItem);
            }
        }

        if (trade.wantItem != null)
        {
            acceptor.RemoveItem(trade.wantItem.itemId);
            sender.AddItem(trade.wantItem);

            if (ItemDropManager.Instance != null)
            {
                ItemDropManager.Instance.SpawnItemDrop(initiator.twitchUserId, trade.wantItem);
            }
        }

        // Transfer coins
        if (trade.offerCoins > 0)
        {
            sender.coins -= trade.offerCoins;
            acceptor.coins += trade.offerCoins;
        }

        if (trade.wantCoins > 0)
        {
            acceptor.coins -= trade.wantCoins;
            sender.coins += trade.wantCoins;
        }

        // Log trade
        LogTrade(sender, acceptor, trade.offerItem, trade.wantItem, trade.offerCoins, trade.wantCoins);

        // Remove pending trade
        pendingTrades.Remove(initiator.twitchUserId);

        RPGManager.Instance.SaveGameData();

        string offerDesc = BuildTradeDescription(trade.offerItem, trade.offerCoins, "");
        string wantDesc = BuildTradeDescription(trade.wantItem, trade.wantCoins, "");

        return $"✅ TRADE COMPLETE!\n" +
               $"{initiator.username} gave: {offerDesc}\n" +
               $"{acceptor.username} gave: {wantDesc}";
    }

    public string CancelTrade(string userId, string username)
    {
        if (!pendingTrades.ContainsKey(userId))
        {
            return $"{username}: You don't have any pending trades.";
        }

        PendingTrade trade = pendingTrades[userId];
        pendingTrades.Remove(userId);

        return $"✅ {username}: Trade offer to {trade.targetUsername} cancelled.";
    }

    // ==================== TRADE HISTORY ====================

    private void LogTrade(ViewerData initiator, ViewerData target, RPGItem initiatorItem,
                         RPGItem targetItem, int initiatorCoins, int targetCoins)
    {
        TradeRecord record = new TradeRecord
        {
            timestamp = DateTime.Now,
            initiatorId = initiator.twitchUserId,
            initiatorUsername = initiator.username,
            targetId = target.twitchUserId,
            targetUsername = target.username,
            initiatorItemName = initiatorItem?.itemName,
            targetItemName = targetItem?.itemName,
            initiatorCoins = initiatorCoins,
            targetCoins = targetCoins
        };

        // Add to both players' history
        initiator.tradeHistory.Add(record);
        target.tradeHistory.Add(record);

        // Trim if too long
        if (initiator.tradeHistory.Count > maxTradeHistoryPerPlayer)
        {
            initiator.tradeHistory.RemoveAt(0);
        }

        if (target.tradeHistory.Count > maxTradeHistoryPerPlayer)
        {
            target.tradeHistory.RemoveAt(0);
        }

        Debug.Log($"[Trade] Logged trade between {initiator.username} and {target.username}");
    }

    // ==================== HELPER METHODS ====================

    private ViewerData FindViewerByUsername(string username)
    {
        username = username.TrimStart('@').ToLower();
        var allViewers = RPGManager.Instance.GetAllViewers();

        foreach (var viewer in allViewers)
        {
            if (viewer.username.ToLower() == username)
            {
                return viewer;
            }
        }

        return null;
    }

    private string GetCannotTradeReason(ViewerData viewer, string username)
    {
        if (viewer == null)
        {
            return $"{username}: You need to choose a class first with !class";
        }

        if (viewer.isBanned)
        {
            return $"{username}: You are banned from trading.";
        }

        if (viewer.isDead)
        {
            return $"{username}: You cannot trade while dead. Wait for revival.";
        }

        return $"{username}: You cannot trade right now.";
    }

    private string BuildTradeDescription(RPGItem item, int coins, string prefix)
    {
        List<string> parts = new List<string>();

        if (item != null)
        {
            parts.Add($"{item.itemName} [{item.rarity}]");
        }

        if (coins > 0)
        {
            parts.Add($"{coins} coins");
        }

        if (parts.Count == 0)
        {
            return "nothing";
        }

        string desc = string.Join(" + ", parts);
        return string.IsNullOrEmpty(prefix) ? desc : $"{prefix} {desc}";
    }

    public string GetTradeHistory(string userId, int count = 10)
    {
        ViewerData viewer = RPGManager.Instance.GetViewer(userId);
        if (viewer == null || viewer.tradeHistory.Count == 0)
        {
            return "No trade history.";
        }

        int displayCount = Mathf.Min(count, viewer.tradeHistory.Count);
        string result = $"═══ TRADE HISTORY (Last {displayCount}) ═══\n";

        for (int i = viewer.tradeHistory.Count - 1; i >= viewer.tradeHistory.Count - displayCount; i--)
        {
            TradeRecord record = viewer.tradeHistory[i];
            string otherPerson = record.initiatorId == userId ? record.targetUsername : record.initiatorUsername;

            string gave = "";
            string received = "";

            if (record.initiatorId == userId)
            {
                // We were the initiator
                if (!string.IsNullOrEmpty(record.initiatorItemName))
                    gave = record.initiatorItemName;
                if (record.initiatorCoins > 0)
                    gave += (string.IsNullOrEmpty(gave) ? "" : " + ") + $"{record.initiatorCoins}c";

                if (!string.IsNullOrEmpty(record.targetItemName))
                    received = record.targetItemName;
                if (record.targetCoins > 0)
                    received += (string.IsNullOrEmpty(received) ? "" : " + ") + $"{record.targetCoins}c";
            }
            else
            {
                // We were the target
                if (!string.IsNullOrEmpty(record.targetItemName))
                    gave = record.targetItemName;
                if (record.targetCoins > 0)
                    gave += (string.IsNullOrEmpty(gave) ? "" : " + ") + $"{record.targetCoins}c";

                if (!string.IsNullOrEmpty(record.initiatorItemName))
                    received = record.initiatorItemName;
                if (record.initiatorCoins > 0)
                    received += (string.IsNullOrEmpty(received) ? "" : " + ") + $"{record.initiatorCoins}c";
            }

            if (string.IsNullOrEmpty(gave)) gave = "nothing";
            if (string.IsNullOrEmpty(received)) received = "nothing";

            result += $"{record.timestamp:MM/dd HH:mm} - {otherPerson}\n";
            result += $"  Gave: {gave} | Got: {received}\n";
        }

        return result;
    }
}

// ==================== DATA STRUCTURES ====================

[Serializable]
public class PendingTrade
{
    public string initiatorId;
    public string initiatorUsername;
    public string targetId;
    public string targetUsername;
    public RPGItem offerItem;
    public RPGItem wantItem;
    public int offerCoins;
    public int wantCoins;
    public DateTime createdAt;
    public DateTime expiresAt;
}

[Serializable]
public class TradeRecord
{
    public DateTime timestamp;
    public string initiatorId;
    public string initiatorUsername;
    public string targetId;
    public string targetUsername;
    public string initiatorItemName;
    public string targetItemName;
    public int initiatorCoins;
    public int targetCoins;
}
