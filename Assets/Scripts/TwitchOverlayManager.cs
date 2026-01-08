using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.EventSub;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using UnityEngine;

public class TwitchOverlayManager : MonoBehaviour
{
    [Header("Twitch Credentials")]
    [SerializeField] private string channelName;
    [SerializeField] private string botUsername;
    [SerializeField] private string oAuthToken; // oauth:xxxx for chat
    [SerializeField] private string accessToken; // Access token (without oauth: prefix) for EventSub
    [SerializeField] private string clientId = "gp762nuuoqcoxypju8c569th9wz7q5"; // From TwitchTokenGenerator or your app
    [SerializeField] private string channelId;  // numeric broadcaster ID

    [Header("RPG System")]
    [SerializeField] private RPGChatCommands rpgCommands;

    [Header("References")]
    [SerializeField] private CoinSpawner coinSpawner;
    [SerializeField] private ParticleEffectManager particleManager;

    private TwitchClient client;
    private EventSubWebsocketClient eventSubClient;
    private TwitchAPI twitchApi;
    private string sessionId;

    private async void Start()
    {
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

        // Initialize Twitch API first
        InitializeTwitchAPI();

        await InitializeTwitchClient();
        await InitializeEventSub();
    }

    private void InitializeTwitchAPI()
    {
        Debug.Log("[TwitchOverlay] Initializing Twitch API...");
        twitchApi = new TwitchAPI();
        twitchApi.Settings.ClientId = clientId;
        twitchApi.Settings.AccessToken = accessToken;
        Debug.Log("[TwitchOverlay] Twitch API initialized");
    }

    private async Task InitializeTwitchClient()
    {
        Debug.Log("[TwitchOverlay] Initializing Twitch Chat Client...");

        try
        {
            var credentials = new ConnectionCredentials(botUsername, oAuthToken);
            client = new TwitchClient();

            client.Initialize(credentials, channelName);

            client.OnConnected += OnChatConnected;
            client.OnMessageReceived += OnChatMessage;
            client.OnChatCommandReceived += OnChatCommand;
            client.OnJoinedChannel += OnJoinedChannel;

            await client.ConnectAsync();
            Debug.Log($"[TwitchOverlay] Twitch Chat: ConnectAsync completed");

            // Wait longer for connection to stabilize
            await Task.Delay(2000);

            // Manually join the channel
            Debug.Log($"[TwitchOverlay] Attempting to join channel: {channelName}");
            try
            {
                await client.JoinChannelAsync(channelName);
                Debug.Log($"[TwitchOverlay] JoinChannelAsync completed");
            }
            catch (Exception joinEx)
            {
                Debug.LogError($"[TwitchOverlay] JoinChannelAsync failed: {joinEx.Message}");
            }

            // Wait a bit more
            await Task.Delay(1000);

            Debug.Log($"[TwitchOverlay] Final state - Connected: {client.IsConnected}, Channels: {client.JoinedChannels?.Count ?? 0}");

            // MANUALLY SET THE FLAG since the event doesn't fire
            if (client.JoinedChannels != null && client.JoinedChannels.Count > 0)
            {
                isJoinedToChannel = true;
                Debug.Log("[TwitchOverlay] ✓ Manually confirmed channel join - ready to send messages!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TwitchOverlay] Twitch Chat failed: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private Task OnJoinedChannel(object sender, TwitchLib.Client.Events.OnJoinedChannelArgs e)
    {
        Debug.Log($"[TwitchOverlay] ✓✓✓ OnJoinedChannel EVENT FIRED! Channel: {e.Channel} ✓✓✓");
        isJoinedToChannel = true;
        return Task.CompletedTask;
    }

    private async Task InitializeEventSub()
    {
        Debug.Log("[TwitchOverlay] Initializing EventSub Websocket...");

        try
        {
            eventSubClient = new EventSubWebsocketClient();

            // Subscribe to websocket events
            eventSubClient.WebsocketConnected += OnEventSubConnected;
            eventSubClient.WebsocketDisconnected += OnEventSubDisconnected;
            eventSubClient.WebsocketReconnected += OnEventSubReconnected;
            eventSubClient.ErrorOccurred += OnEventSubError;

            // Subscribe to the events we want
            eventSubClient.ChannelCheer += OnChannelCheer;
            eventSubClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointsRedeemed;

            await eventSubClient.ConnectAsync();
            Debug.Log("[TwitchOverlay] EventSub WebSocket: ConnectAsync called.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TwitchOverlay] EventSub failed: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // CHAT EVENT HANDLERS
    private bool isJoinedToChannel = false;

    private Task OnChatConnected(object sender, OnConnectedEventArgs e)
    {
        Debug.Log($"[TwitchOverlay] Twitch Chat Connected as {e.BotUsername}");
        return Task.CompletedTask;
    }

    private Task OnChatMessage(object sender, OnMessageReceivedArgs e)
    {
        Debug.Log($"[TwitchOverlay] {e.ChatMessage.Username}: {e.ChatMessage.Message}");
        return Task.CompletedTask;
    }

    private Task OnChatCommand(object sender, OnChatCommandReceivedArgs e)
    {
        string command = "";
        try
        {
            string message = e.ChatMessage.Message;
            if (message.StartsWith("!"))
            {
                string[] parts = message.TrimStart('!').Split(' ');
                command = parts[0].ToLower();
                string[] args = parts.Length > 1 ? parts.Skip(1).ToArray() : new string[0];
                string userId = e.ChatMessage.UserId;
                string username = e.ChatMessage.Username;
                bool isBroadcaster = e.ChatMessage.IsBroadcaster;

                // Try RPG commands first
                string rpgResponse = rpgCommands.HandleRPGCommand(command, userId, username, args);
                if (rpgResponse != null)
                {
                    Debug.Log($"[TwitchOverlay] RPG Response: {rpgResponse}");

                    // USE MAIN THREAD DISPATCHER!
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        OnScreenNotification.Instance.ShowInfo(rpgResponse);
                    });

                    return Task.CompletedTask;
                }

                // Try admin commands  
                if (isBroadcaster)
                {
                    string adminResponse = rpgCommands.HandleAdminCommand(command, args, true);
                    if (adminResponse != null)
                    {
                        // USE MAIN THREAD DISPATCHER!
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                            OnScreenNotification.Instance.ShowSuccess(adminResponse);
                        });

                        return Task.CompletedTask;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[TwitchOverlay] Could not parse command: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    // EVENTSUB HANDLERS
    private async Task OnEventSubConnected(object sender, WebsocketConnectedArgs e)
    {
        Debug.Log("[TwitchOverlay] EventSub WebSocket Connected!");

        if (!e.IsRequestedReconnect)
        {
            // The session ID is provided in the WebsocketConnectedArgs
            sessionId = eventSubClient.SessionId;
            Debug.Log($"[TwitchOverlay] Session ID: {sessionId}");

            if (string.IsNullOrEmpty(sessionId))
            {
                Debug.LogError("[TwitchOverlay] Session ID is null or empty!");
                return;
            }

            // Now we use the Twitch API to create EventSub subscriptions
            await CreateEventSubSubscriptions();
        }
    }

    private async Task CreateEventSubSubscriptions()
    {
        Debug.Log("[TwitchOverlay] Creating EventSub subscriptions via Helix API...");

        try
        {
            // Subscribe to Bits/Cheers
            var cheerResponse = await twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                type: "channel.cheer",
                version: "1",
                condition: new Dictionary<string, string>
                {
                    { "broadcaster_user_id", channelId }
                },
                method: EventSubTransportMethod.Websocket,
                websocketSessionId: sessionId
            // Don't pass accessToken here - it's already set on twitchApi.Settings
            );

            if (cheerResponse.Subscriptions != null && cheerResponse.Subscriptions.Length > 0)
            {
                Debug.Log($"[TwitchOverlay] ✓ Subscribed to channel.cheer (Status: {cheerResponse.Subscriptions[0].Status})");
            }

            // Subscribe to Channel Points
            var pointsResponse = await twitchApi.Helix.EventSub.CreateEventSubSubscriptionAsync(
                type: "channel.channel_points_custom_reward_redemption.add",
                version: "1",
                condition: new Dictionary<string, string>
                {
                    { "broadcaster_user_id", channelId }
                },
                method: EventSubTransportMethod.Websocket,
                websocketSessionId: sessionId
            // Don't pass accessToken here - it's already set on twitchApi.Settings
            );

            if (pointsResponse.Subscriptions != null && pointsResponse.Subscriptions.Length > 0)
            {
                Debug.Log($"[TwitchOverlay] ✓ Subscribed to channel points (Status: {pointsResponse.Subscriptions[0].Status})");
            }

            Debug.Log("[TwitchOverlay] All EventSub subscriptions created successfully!");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TwitchOverlay] Failed to create subscriptions: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private Task OnEventSubDisconnected(object sender, EventArgs e)
    {
        Debug.LogWarning("[TwitchOverlay] EventSub WebSocket Disconnected");
        return Task.CompletedTask;
    }

    private Task OnEventSubReconnected(object sender, EventArgs e)
    {
        Debug.Log("[TwitchOverlay] EventSub WebSocket Reconnected!");
        return Task.CompletedTask;
    }

    private Task OnEventSubError(object sender, ErrorOccuredArgs e)
    {
        Debug.LogError($"[TwitchOverlay] EventSub Error: {e.Exception?.Message}");
        return Task.CompletedTask;
    }

    public void SimulateCheer()
    {

    }

    private Task OnChannelCheer(object sender, ChannelCheerArgs e)
    {
        if (e == null || e.Payload == null)
        {
            Debug.LogError("[TwitchOverlay] Cheer: args or Payload is NULL");
            return Task.CompletedTask;
        }

        var eventData = e.Payload.Event;
        if (eventData == null)
        {
            Debug.LogError("[TwitchOverlay] Cheer: Payload.Event is NULL");
            return Task.CompletedTask;
        }

        int bits = eventData.Bits;
        string username = eventData.UserName ?? (eventData.IsAnonymous ? "Anonymous" : "Unknown");
        string userId = eventData.UserId;

        Debug.Log($"[TwitchOverlay] Bits Event → User:{username} Bits:{bits}");

        if (bits > 0)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                // Spawn coins for all characters to collect
                coinSpawner.SpawnCoins(bits);

                // Give the cheerer half their bits as direct coins
                if (!eventData.IsAnonymous && !string.IsNullOrEmpty(userId))
                {
                    int coinsToGive = bits / 2;

                    // Get or create viewer data
                    ViewerData viewer = RPGManager.Instance.GetOrCreateViewer(userId, username);

                    // Give coins
                    RPGManager.Instance.AddCoins(userId, coinsToGive);

                    // Show notification
                    OnScreenNotification.Instance.ShowSuccess(
                        $"💎 {username} cheered {bits} bits!\n" +
                        $"Earned {coinsToGive} coins + {bits} coins to collect!"
                    );

                    Debug.Log($"[Twitch] {username} earned {coinsToGive} coins from cheering {bits} bits");
                }
            });
        }

        return Task.CompletedTask;
    }

    private Task OnChannelPointsRedeemed(
    object sender,
    ChannelPointsCustomRewardRedemptionArgs args)
    {
        if (args.Payload == null)
        {
            Debug.LogError("[TwitchOverlay] Channel points: Payload is NULL");
            return Task.CompletedTask;
        }

        var eventData = args.Payload.Event;
        if (eventData == null)
        {
            Debug.LogError("[TwitchOverlay] Channel points: Payload.Event is NULL");
            return Task.CompletedTask;
        }

        string user = eventData.UserName ?? "UNKNOWN";
        string reward = eventData.Reward?.Title ?? "UNKNOWN";
        string input = eventData.UserInput ?? "";

        Debug.Log($"[TwitchOverlay] Channel Points → User:{user} Reward:{reward} Input:{input}");

        // Dispatch safely to Unity
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            HandleChannelPointReward(reward);
        });

        return Task.CompletedTask;
    }

    private void HandleChannelPointReward(string reward)
    {
        if (string.IsNullOrEmpty(reward)) return;

        switch (reward.ToLower())
        {
            case "coin explosion":
                coinSpawner.SpawnCoins(50);
                particleManager.TriggerExplosion();
                break;
            case "particle burst":
                particleManager.TriggerBurst();
                break;
            case "screen shake":
                Debug.Log("[TwitchOverlay] Screen shake triggered!");
                break;
            case "golden coin":
                coinSpawner.SpawnGoldenCoin();
                break;
            case "coins":
                coinSpawner.SpawnCoins(20);
                break;
            case "rain":
                coinSpawner.SpawnCoins(100);
                break;
            default:
                Debug.Log($"[TwitchOverlay] Unknown reward: {reward}");
                break;
        }
    }

    private async void OnApplicationQuit()
    {
        if (client != null)
        {
            try { await client.DisconnectAsync(); }
            catch { }
        }

        if (eventSubClient != null)
        {
            try { await eventSubClient.DisconnectAsync(); }
            catch { }
        }
    }
}