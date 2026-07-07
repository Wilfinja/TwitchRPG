using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// ============================================================
//  DiscordBridgeServer.cs
//  Attach to a persistent GameObject in your scene (e.g. the
//  same one that holds RPGManager or TwitchOverlayManager).
//
//  SECURITY MODEL:
//    • Listener is bound to 127.0.0.1 ONLY — never reachable
//      from outside your PC.
//    • Every request must include a shared secret token in the
//      X-Bridge-Token header. Without it the server returns 401.
//    • Only explicitly whitelisted commands are accepted.
//      Anything else returns 400 before touching game logic.
//    • All writes go through RPGManager / RPGChatCommands,
//      the same path Twitch commands use — no direct file access.
//    • The save file (game_data.json) is NEVER touched by this
//      script directly.
// ============================================================

public class DiscordBridgeServer : MonoBehaviour
{
    // ── Inspector Fields ─────────────────────────────────────

    [Header("Server Settings")]
    [Tooltip("Port the Discord bot will connect to. Default: 7432")]
    [SerializeField] private int port = 7432;

    [Tooltip("Shared secret between Unity and the Discord bot. " +
             "Set the same string in both places. Treat like a password.")]
    [SerializeField] private string sharedSecret = "CHANGE_ME_BEFORE_USE";

    [Header("References")]
    [SerializeField] private RPGChatCommands rpgCommands;

    // ── Private State ────────────────────────────────────────

    private HttpListener _listener;
    private Thread _listenerThread;
    private bool _running;

    // These are set ONCE on the main thread in Start() and then safely
    // read from background threads. Never compute them on-the-fly because
    // RPGSaveSystem.GetSavePath() calls Application.persistentDataPath,
    // which Unity only allows on the main thread.
    private static string _mappingFilePath;
    private static string _pendingFilePath;

    // Commands the Discord bot is allowed to trigger.
    // READ-ONLY commands are safe any time.
    // WRITE commands still go through RPGManager and save normally.
    private static readonly HashSet<string> AllowedCommands = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        // ── Read-only ──
        "stats",
        "inventory", "inv",
        "abilities",
        "ability",
        "loadout",
        "coins", "balance",
        "shop",
        "pvpleaderboard",
        "help",
        // ── Write (safe via RPGManager) ──
        "buy",
        "equip",
        "unequip",
        "equipability",
        "unequipability",
        "levelup",
        "sell",
        // ── Account linking ──
        "linkdiscord",
        "whoami"
    };

    // ── Unity Lifecycle ──────────────────────────────────────

    private void Start()
    {
        if (string.IsNullOrEmpty(sharedSecret) || sharedSecret == "CHANGE_ME_BEFORE_USE")
        {
            Debug.LogError("[DiscordBridge] ⚠ sharedSecret is not set! " +
                           "Set it in the Inspector before running. Server will NOT start.");
            return;
        }

        // Cache file paths HERE on the main thread — Application.persistentDataPath
        // is only available on the main thread, so we resolve it once during Start()
        // and store the result in plain strings that background threads can read safely.
        string saveDir = Path.GetDirectoryName(RPGSaveSystem.GetSavePath());
        _mappingFilePath = Path.Combine(saveDir, "discord_mapping.json");
        _pendingFilePath = Path.Combine(saveDir, "pending_discord_commands.json");

        Debug.Log($"[DiscordBridge] Mapping file: {_mappingFilePath}");

        StartServer();
        ProcessPendingCommands(); // Apply any commands queued while Unity was offline
    }

    private void OnApplicationQuit()
    {
        StopServer();
    }

    private void OnDestroy()
    {
        StopServer();
    }

    // ── Server Start / Stop ──────────────────────────────────

    private void StartServer()
    {
        try
        {
            _listener = new HttpListener();

            // SECURITY: Bind to 127.0.0.1 (loopback) ONLY.
            // This prefix is invisible to any external network interface.
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");

            _listener.Start();
            _running = true;

            _listenerThread = new Thread(ListenLoop)
            {
                IsBackground = true,    // Thread dies automatically when Unity exits
                Name = "DiscordBridgeListener"
            };
            _listenerThread.Start();

            Debug.Log($"[DiscordBridge] ✅ Server started on http://127.0.0.1:{port}/ (localhost only)");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DiscordBridge] Failed to start server: {ex.Message}\n" +
                           "If the port is already in use, change it in the Inspector.");
        }
    }

    private void StopServer()
    {
        _running = false;

        try { _listener?.Stop(); }
        catch { /* Ignore errors during shutdown */ }

        try { _listenerThread?.Join(500); }
        catch { }

        Debug.Log("[DiscordBridge] Server stopped.");
    }

    // ── Listener Loop (Background Thread) ───────────────────

    // This runs entirely on a background thread.
    // It MUST NOT call any Unity API directly.
    // All Unity work is dispatched to the main thread via
    // UnityMainThreadDispatcher + TaskCompletionSource.

    private void ListenLoop()
    {
        while (_running)
        {
            try
            {
                HttpListenerContext ctx = _listener.GetContext(); // Blocks until a request arrives
                // Handle each request on a thread-pool thread so the listener
                // can immediately accept the next connection.
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
            }
            catch (HttpListenerException)
            {
                // Normal — thrown when _listener.Stop() is called.
                break;
            }
            catch (Exception ex)
            {
                if (_running)
                    Debug.LogWarning($"[DiscordBridge] Listener error: {ex.Message}");
            }
        }
    }

    // ── Request Handler (Thread Pool Thread) ─────────────────

    private void HandleRequest(HttpListenerContext ctx)
    {
        HttpListenerRequest req = ctx.Request;
        HttpListenerResponse resp = ctx.Response;

        try
        {
            // ── 1. Method gate: only POST ──────────────────
            if (req.HttpMethod != "POST")
            {
                SendResponse(resp, 405, false, null, "Method Not Allowed — use POST");
                return;
            }

            // ── 2. Auth gate: shared secret header ────────
            string token = req.Headers["X-Bridge-Token"];
            if (token != sharedSecret)
            {
                // Intentionally vague — don't hint that the header even exists
                SendResponse(resp, 401, false, null, "Unauthorized");
                return;
            }

            // ── 3. Parse request body ──────────────────────
            string body;
            using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                body = reader.ReadToEnd();

            BridgeRequest bridgeReq = JsonUtility.FromJson<BridgeRequest>(body);
            if (bridgeReq == null || string.IsNullOrEmpty(bridgeReq.command))
            {
                SendResponse(resp, 400, false, null, "Invalid request body");
                return;
            }

            bridgeReq.command = bridgeReq.command.Trim().ToLower().TrimStart('!');
            if (bridgeReq.args == null)
                bridgeReq.args = new string[0];

            // ── 4. Command whitelist gate ──────────────────
            if (!AllowedCommands.Contains(bridgeReq.command))
            {
                SendResponse(resp, 400, false, null,
                    $"Command '{bridgeReq.command}' is not available via Discord.");
                return;
            }

            // ── 5. Resolve Discord → Twitch identity ──────
            // Special case: linking command doesn't need an existing mapping yet
            if (bridgeReq.command == "linkdiscord")
            {
                string linkResult = HandleLinkCommand(bridgeReq);
                SendResponse(resp, 200, true, linkResult, null);
                return;
            }

            DiscordMapping mapping = FindMappingByDiscordId(bridgeReq.discordUserId);
            if (mapping == null)
            {
                SendResponse(resp, 404, false, null,
                    "Your Discord account isn't linked yet.\n" +
                    "In Twitch chat, type: **!linkdiscord <your_discord_id>**\n" +
                    $"Your Discord ID is: {bridgeReq.discordUserId}");
                return;
            }

            // ── 6. Dispatch to Unity main thread ──────────
            // We use a TaskCompletionSource to bridge the background
            // thread (here) with the Unity main thread, then block
            // this thread until the result comes back.
            var tcs = new TaskCompletionSource<string>();

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                try
                {
                    string result = rpgCommands.HandleRPGCommand(
                        bridgeReq.command,
                        mapping.twitchUserId,
                        mapping.twitchUsername,
                        bridgeReq.args);

                    tcs.SetResult(result ?? $"Command '{bridgeReq.command}' processed.");
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            // Wait up to 10 seconds for the main thread to respond
            if (!tcs.Task.Wait(TimeSpan.FromSeconds(10)))
            {
                SendResponse(resp, 504, false, null, "Unity timed out processing your command.");
                return;
            }

            if (tcs.Task.IsFaulted)
            {
                Debug.LogError($"[DiscordBridge] Command error: {tcs.Task.Exception?.InnerException?.Message}");
                SendResponse(resp, 500, false, null, "An error occurred processing your command.");
                return;
            }

            SendResponse(resp, 200, true, tcs.Task.Result, null);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DiscordBridge] Unhandled request error: {ex.Message}");
            SendResponse(resp, 500, false, null, "Internal server error.");
        }
        finally
        {
            resp.Close();
        }
    }

    // ── Response Helper ──────────────────────────────────────

    private void SendResponse(HttpListenerResponse resp, int statusCode,
                               bool success, string message, string error)
    {
        try
        {
            resp.StatusCode = statusCode;
            resp.ContentType = "application/json; charset=utf-8";

            // Build a minimal JSON response without requiring Newtonsoft
            string safeMessage = (message ?? "").Replace("\"", "\\\"").Replace("\n", "\\n");
            string safeError = (error ?? "").Replace("\"", "\\\"").Replace("\n", "\\n");

            string json = success
                ? $"{{\"success\":true,\"message\":\"{safeMessage}\"}}"
                : $"{{\"success\":false,\"error\":\"{safeError}\"}}";

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DiscordBridge] Failed to send response: {ex.Message}");
        }
    }

    // ── Discord ↔ Twitch Account Linking ────────────────────

    // Viewers link their accounts by typing the following in Twitch chat:
    //   !linkdiscord <their_discord_user_id>
    // That Twitch-side command calls RegisterTwitchLink() below, which is
    // verified because we already know their Twitch identity from the chat event.

    /// <summary>
    /// Called by RPGChatCommands when a viewer types !linkdiscord in Twitch chat.
    /// This is the verified path — we know the Twitch identity is genuine.
    /// </summary>
    public static string RegisterTwitchLink(string twitchUserId, string twitchUsername, string discordId)
    {
        discordId = discordId.Trim();

        if (string.IsNullOrEmpty(discordId) || discordId.Length < 16)
            return $"{twitchUsername}: Please provide your Discord User ID (18-digit number).\n" +
                   "Enable Developer Mode in Discord → Settings → Advanced,\n" +
                   "then right-click your username → Copy User ID.";

        // PRIMARY: Save directly onto ViewerData so it persists in game_data.json.
        // This is the authoritative store — no separate file required.
        ViewerData viewer = RPGManager.Instance?.GetViewer(twitchUserId);
        if (viewer != null)
        {
            viewer.discordUserId = discordId;
            RPGManager.Instance.SaveGameData();
            Debug.Log($"[DiscordBridge] Saved discordUserId '{discordId}' onto ViewerData for {twitchUsername}");
        }
        else
        {
            Debug.LogWarning($"[DiscordBridge] Could not find ViewerData for {twitchUserId} — mapping file only.");
        }

        // SECONDARY: Also write to discord_mapping.json as a fallback for the
        // Discord bot's offline reader (which can't access RPGManager directly).
        if (!string.IsNullOrEmpty(_mappingFilePath))
        {
            DiscordMappingDatabase db = LoadMappingDatabase();
            db.mappings.RemoveAll(m =>
                m.twitchUserId == twitchUserId || m.discordUserId == discordId);
            db.mappings.Add(new DiscordMapping
            {
                twitchUserId = twitchUserId,
                twitchUsername = twitchUsername,
                discordUserId = discordId,
                linkedAt = DateTime.Now.ToString("o")
            });
            SaveMappingDatabase(db);
        }
        else
        {
            Debug.LogWarning("[DiscordBridge] _mappingFilePath not yet set — mapping file skipped. ViewerData was still updated.");
        }

        Debug.Log($"[DiscordBridge] Linked Discord {discordId} → Twitch {twitchUsername} ({twitchUserId})");
        return $"✅ {twitchUsername}: Discord account linked! " +
               "You can now use RPG commands in the Discord server.";
    }

    // Called when the Discord bot sends a "linkdiscord" command — this is the
    // bot confirming the OTHER side of a pending link (future flow).
    // For now it just returns a helpful prompt.
    private string HandleLinkCommand(BridgeRequest req)
    {
        return "To link your Discord account, type the following in Twitch chat:\n" +
               $"**!linkdiscord {req.discordUserId}**";
    }

    // ── Mapping File I/O ─────────────────────────────────────

    private static DiscordMapping FindMappingByDiscordId(string discordId)
    {
        // PRIMARY: Search ViewerData directly — this is always up to date
        // because RegisterTwitchLink now saves discordUserId onto ViewerData.
        if (RPGManager.Instance != null)
        {
            var allViewers = RPGManager.Instance.GetAllViewers();
            if (allViewers != null)
            {
                ViewerData match = allViewers.Find(v =>
                    !string.IsNullOrEmpty(v.discordUserId) &&
                    v.discordUserId == discordId);

                if (match != null)
                {
                    return new DiscordMapping
                    {
                        twitchUserId = match.twitchUserId,
                        twitchUsername = match.username,
                        discordUserId = discordId,
                        linkedAt = ""
                    };
                }
            }
        }

        // FALLBACK: Check the mapping file (covers edge cases where ViewerData
        // wasn't available when the link was first registered).
        DiscordMappingDatabase db = LoadMappingDatabase();
        return db.mappings.Find(m => m.discordUserId == discordId);
    }

    private static DiscordMappingDatabase LoadMappingDatabase()
    {
        try
        {
            if (string.IsNullOrEmpty(_mappingFilePath))
            {
                Debug.LogWarning("[DiscordBridge] Mapping path not yet initialized — Start() may not have run.");
                return new DiscordMappingDatabase();
            }

            if (File.Exists(_mappingFilePath))
            {
                string json = File.ReadAllText(_mappingFilePath);
                var db = JsonUtility.FromJson<DiscordMappingDatabase>(json);
                if (db != null) return db;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DiscordBridge] Could not load mapping file: {ex.Message}");
        }
        return new DiscordMappingDatabase();
    }

    private static void SaveMappingDatabase(DiscordMappingDatabase db)
    {
        try
        {
            if (string.IsNullOrEmpty(_mappingFilePath))
            {
                Debug.LogError("[DiscordBridge] Cannot save mapping — path not initialized.");
                return;
            }

            string dir = Path.GetDirectoryName(_mappingFilePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_mappingFilePath, JsonUtility.ToJson(db, true));
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DiscordBridge] Could not save mapping file: {ex.Message}");
        }
    }

    // ── Pending Command Queue (Offline Support) ──────────────

    // When Unity is NOT running, the Discord bot can queue certain safe
    // write commands (equip, equipability, etc.) to a pending file.
    // On next Unity startup, ProcessPendingCommands() replays them all.

    /// <summary>
    /// Called on Start() — replays any commands the Discord bot queued
    /// while Unity was offline.
    /// </summary>
    private void ProcessPendingCommands()
    {
        if (string.IsNullOrEmpty(_pendingFilePath)) return;
        if (!File.Exists(_pendingFilePath)) return;

        PendingCommandDatabase pending;
        try
        {
            string json = File.ReadAllText(_pendingFilePath);
            pending = JsonUtility.FromJson<PendingCommandDatabase>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DiscordBridge] Could not read pending commands: {ex.Message}");
            return;
        }

        if (pending == null || pending.commands == null || pending.commands.Count == 0)
        {
            File.Delete(_pendingFilePath);
            return;
        }

        Debug.Log($"[DiscordBridge] Processing {pending.commands.Count} offline-queued command(s)...");

        // Dispatch each to the main thread in order.
        // We give a short stagger so RPGManager has fully initialised.
        StartCoroutine(ReplayPendingCoroutine(pending));
    }

    private System.Collections.IEnumerator ReplayPendingCoroutine(PendingCommandDatabase pending)
    {
        // Wait two frames to ensure all other Awake/Start calls have completed
        yield return null;
        yield return null;

        int processed = 0;
        foreach (var cmd in pending.commands)
        {
            DiscordMapping mapping = FindMappingByDiscordId(cmd.discordUserId);
            if (mapping == null)
            {
                Debug.LogWarning($"[DiscordBridge] Skipping pending command from unmapped Discord user {cmd.discordUserId}");
                continue;
            }

            if (!AllowedCommands.Contains(cmd.command))
            {
                Debug.LogWarning($"[DiscordBridge] Skipping disallowed pending command: {cmd.command}");
                continue;
            }

            try
            {
                string result = rpgCommands.HandleRPGCommand(
                    cmd.command,
                    mapping.twitchUserId,
                    mapping.twitchUsername,
                    cmd.args ?? new string[0]);

                Debug.Log($"[DiscordBridge] Replayed offline command '{cmd.command}' for {mapping.twitchUsername}: {result}");
                processed++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DiscordBridge] Error replaying command '{cmd.command}': {ex.Message}");
            }

            yield return null; // Let Unity breathe between commands
        }

        // Delete the file once everything has been processed
        try { File.Delete(_pendingFilePath); }
        catch { }

        Debug.Log($"[DiscordBridge] ✅ Replayed {processed}/{pending.commands.Count} offline commands.");
    }

    // ── Serializable Data Structures ─────────────────────────

    [Serializable]
    private class BridgeRequest
    {
        public string discordUserId;
        public string command;
        public string[] args;
    }

    [Serializable]
    public class DiscordMapping
    {
        public string twitchUserId;
        public string twitchUsername;
        public string discordUserId;
        public string linkedAt;
    }

    [Serializable]
    private class DiscordMappingDatabase
    {
        public List<DiscordMapping> mappings = new List<DiscordMapping>();
    }

    [Serializable]
    public class PendingCommand
    {
        public string discordUserId;
        public string command;
        public string[] args;
        public string queuedAt;
    }

    [Serializable]
    public class PendingCommandDatabase
    {
        public List<PendingCommand> commands = new List<PendingCommand>();
    }
}
