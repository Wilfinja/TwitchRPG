using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Bridges Unity game state to the Railway EBS, and receives button commands back.
///
/// SETUP:
///   1. Deploy ebs/server.js to Railway and note its URL (e.g. https://your-app.up.railway.app)
///   2. Set ebsUrl and sharedSecret in the Inspector to match your EBS environment variables
///   3. Add this component to the same GameObject as RPGManager
///
/// FLOW:
///   Unity ──pushState──► EBS ──PubSub──► Twitch Panel
///   Panel button ──POST──► EBS ──POST──► Unity (port 7433)
/// </summary>
public class PanelSyncServer : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("EBS Connection")]
    [Tooltip("Full URL of your Railway EBS, e.g. https://your-app.up.railway.app")]
    [SerializeField] private string ebsUrl = "https://your-app.up.railway.app";

    [Tooltip("Must match EBS environment variable UNITY_SECRET")]
    [SerializeField] private string sharedSecret = "CHANGE_ME";

    [Header("Push Settings")]
    [Tooltip("How often (seconds) to push full game state to EBS")]
    [SerializeField] private float pushIntervalSeconds = 3f;

    [Tooltip("Port this server listens on for incoming commands from EBS")]
    [SerializeField] private int inboundPort = 7433;

    [Header("References")]
    [SerializeField] private RPGChatCommands rpgCommands;

    // ── Private ───────────────────────────────────────────────────────────────

    private HttpListener _listener;
    private Thread _listenerThread;
    private bool _running;
    private float _pushTimer;

    // Cache last pushed state per viewer to avoid redundant pushes
    private Dictionary<string, string> _lastPushedState = new Dictionary<string, string>();

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        if (string.IsNullOrEmpty(ebsUrl) || ebsUrl == "https://your-app.up.railway.app")
        {
            Debug.LogError("[PanelSync] ebsUrl is not configured! Set it in the Inspector.");
            return;
        }

        if (sharedSecret == "CHANGE_ME")
        {
            Debug.LogError("[PanelSync] sharedSecret is not configured!");
            return;
        }

        StartInboundServer();
        _pushTimer = 0f;
        Debug.Log($"[PanelSync] Started. Pushing to {ebsUrl} every {pushIntervalSeconds}s");
    }

    private void Update()
    {
        _pushTimer += Time.deltaTime;
        if (_pushTimer >= pushIntervalSeconds)
        {
            _pushTimer = 0f;
            StartCoroutine(PushActiveViewerStates());
        }
    }

    private void OnApplicationQuit()
    {
        _running = false;
        try { _listener?.Stop(); } catch { }
        try { _listenerThread?.Join(300); } catch { }
    }

    private void OnDestroy()
    {
        _running = false;
        try { _listener?.Stop(); } catch { }
    }

    // ── State Pushing ─────────────────────────────────────────────────────────

    /// <summary>
    /// Push the current state of every on-screen character to the EBS.
    /// The EBS will broadcast this to the relevant viewer's panel via PubSub.
    /// </summary>
    private IEnumerator PushActiveViewerStates()
    {
        if (CharacterSpawner.Instance == null) yield break;

        List<OnScreenCharacter> characters = CharacterSpawner.Instance.GetAllCharacters();
        if (characters == null || characters.Count == 0) yield break;

        foreach (OnScreenCharacter character in characters)
        {
            string userId = character.GetUserId();
            ViewerData viewer = RPGManager.Instance?.GetViewer(userId);
            if (viewer == null) continue;

            string stateJson = BuildViewerStateJson(viewer, character);

            // Skip if state hasn't changed
            if (_lastPushedState.TryGetValue(userId, out string last) && last == stateJson)
                continue;

            _lastPushedState[userId] = stateJson;

            // Fire and forget — we don't block the game loop
            StartCoroutine(PostToPush(userId, stateJson));
        }

        // Also push global state (shop refresh timer, expedition status, etc.)
        StartCoroutine(PostGlobalState());
    }

    private string BuildViewerStateJson(ViewerData viewer, OnScreenCharacter character)
    {
        CharacterStats totalStats = viewer.GetTotalStats();
        CharacterStats baseStats = viewer.baseStats;

        // Build inventory JSON (first 20 items to stay within PubSub 5KB limit)
        var inventoryItems = new System.Text.StringBuilder();
        int invCount = Mathf.Min(viewer.inventory.Count, 20);
        for (int i = 0; i < invCount; i++)
        {
            RPGItem item = viewer.inventory[i];
            if (i > 0) inventoryItems.Append(",");
            inventoryItems.Append("{");
            inventoryItems.Append($"\"id\":\"{EscapeJson(item.itemId)}\",");
            inventoryItems.Append($"\"name\":\"{EscapeJson(item.itemName)}\",");
            inventoryItems.Append($"\"rarity\":\"{item.rarity}\",");
            inventoryItems.Append($"\"type\":\"{item.itemType}\",");
            inventoryItems.Append($"\"price\":{item.price}");
            inventoryItems.Append("}");
        }

        // Build equipped abilities loadout
        var abilities = new System.Text.StringBuilder();
        for (int i = 0; i < viewer.equippedAbilities.Count; i++)
        {
            if (i > 0) abilities.Append(",");
            string cmd = viewer.equippedAbilities[i];
            AbilityData abilityData = AbilityDatabase.Instance?.GetAbility(cmd);
            string abilityName = abilityData != null ? abilityData.abilityName : cmd;
            abilities.Append("{");
            abilities.Append($"\"cmd\":\"{EscapeJson(cmd)}\",");
            abilities.Append($"\"name\":\"{EscapeJson(abilityName)}\"");
            abilities.Append("}");
        }

        // Build equipped gear summary
        string mainHandName = viewer.equipped.mainHand?.itemName ?? "";
        string offHandName = viewer.equipped.offHand?.itemName ?? "";
        string headName = viewer.equipped.head?.itemName ?? "";
        string chestName = viewer.equipped.chest?.itemName ?? "";

        // Combat state
        bool inCombat = CombatTurnManager.Instance != null && CombatTurnManager.Instance.combatActive;
        bool isPlayerTurn = inCombat && CombatTurnManager.Instance.playerTurn;
        string queuedAction = "";

        if (inCombat)
        {
            OnScreenCharacter charObj = CharacterSpawner.Instance?.GetCharacter(viewer.twitchUserId);
            if (charObj != null)
            {
                CombatEntity entity = charObj.GetComponent<CombatEntity>();
                if (entity != null) queuedAction = entity.queuedAction ?? "";
            }
        }

        // XP progress
        int xpNeeded = 150;
        float xpProgress = 0f;
        if (ExperienceManager.Instance != null)
        {
            xpNeeded = ExperienceManager.Instance.GetXPForNextLevel(baseStats.level);
            xpProgress = ExperienceManager.Instance.GetLevelProgress(viewer);
        }

        int totalDamage = viewer.equipped.GetTotalDamageBonus();
        int totalDefense = viewer.equipped.GetTotalDefenseBonus();

        StringBuilder json = new StringBuilder();
        json.Append("{");
        json.Append($"\"type\":\"viewer_state\",");
        json.Append($"\"userId\":\"{EscapeJson(viewer.twitchUserId)}\",");
        json.Append($"\"username\":\"{EscapeJson(viewer.username)}\",");
        json.Append($"\"class\":\"{viewer.characterClass}\",");
        json.Append($"\"coins\":{viewer.coins},");
        json.Append($"\"level\":{totalStats.level},");
        json.Append($"\"xp\":{baseStats.experience},");
        json.Append($"\"xpNeeded\":{xpNeeded},");
        json.Append($"\"xpProgress\":{xpProgress:F2},");
        json.Append($"\"unallocatedPoints\":{baseStats.unallocatedStatPoints},");
        json.Append($"\"hp\":{totalStats.currentHealth},");
        json.Append($"\"maxHp\":{totalStats.maxHealth},");
        json.Append("\"stats\":{");
        json.Append($"\"str\":{totalStats.strength},");
        json.Append($"\"con\":{totalStats.constitution},");
        json.Append($"\"dex\":{totalStats.dexterity},");
        json.Append($"\"wil\":{totalStats.willpower},");
        json.Append($"\"cha\":{totalStats.charisma},");
        json.Append($"\"int\":{totalStats.intelligence}");
        json.Append("},");
        json.Append("\"baseStats\":{");
        json.Append($"\"str\":{baseStats.strength},");
        json.Append($"\"con\":{baseStats.constitution},");
        json.Append($"\"dex\":{baseStats.dexterity},");
        json.Append($"\"wil\":{baseStats.willpower},");
        json.Append($"\"cha\":{baseStats.charisma},");
        json.Append($"\"int\":{baseStats.intelligence}");
        json.Append("},");
        json.Append($"\"damageBonus\":{totalDamage},");
        json.Append($"\"defenseBonus\":{totalDefense},");
        json.Append("\"equipped\":{");
        json.Append($"\"mainHand\":\"{EscapeJson(mainHandName)}\",");
        json.Append($"\"offHand\":\"{EscapeJson(offHandName)}\",");
        json.Append($"\"head\":\"{EscapeJson(headName)}\",");
        json.Append($"\"chest\":\"{EscapeJson(chestName)}\"");
        json.Append("},");
        json.Append($"\"inventory\":[{inventoryItems}],");
        json.Append($"\"inventoryCount\":{viewer.inventory.Count},");
        json.Append($"\"abilities\":[{abilities}],");
        json.Append($"\"pvpWins\":{viewer.pvpWins},");
        json.Append($"\"pvpLosses\":{viewer.pvpLosses},");
        json.Append($"\"isDead\":{viewer.isDead.ToString().ToLower()},");
        json.Append($"\"inCombat\":{inCombat.ToString().ToLower()},");
        json.Append($"\"isPlayerTurn\":{isPlayerTurn.ToString().ToLower()},");
        json.Append($"\"queuedAction\":\"{EscapeJson(queuedAction)}\"");
        json.Append("}");

        return json.ToString();
    }

    private IEnumerator PostGlobalState()
    {
        bool expeditionActive = ExpeditionManager.Instance != null &&
                                ExpeditionManager.Instance.currentExpedition.isActive;
        bool pvpActive = PvPManager.Instance != null && PvPManager.Instance.pvpActive;
        bool combatActive = CombatTurnManager.Instance != null && CombatTurnManager.Instance.combatActive;
        bool isPlayerTurn = combatActive && CombatTurnManager.Instance.playerTurn;
        float turnTimer = combatActive ? CombatTurnManager.Instance.turnTimer : 0f;

        // Shop refresh time
        string shopRefresh = "";
        if (ShopManager.Instance != null)
        {
            TimeSpan t = ShopManager.Instance.GetTimeUntilRefresh();
            shopRefresh = $"{t.Hours}h {t.Minutes}m";
        }

        int wave = 0;
        int totalWaves = 0;
        if (expeditionActive)
        {
            wave = ExpeditionManager.Instance.currentExpedition.currentWave;
            totalWaves = ExpeditionManager.Instance.currentExpedition.totalWaves;
        }

        string json = "{" +
            "\"type\":\"global_state\"," +
            $"\"expeditionActive\":{expeditionActive.ToString().ToLower()}," +
            $"\"pvpActive\":{pvpActive.ToString().ToLower()}," +
            $"\"combatActive\":{combatActive.ToString().ToLower()}," +
            $"\"isPlayerTurn\":{isPlayerTurn.ToString().ToLower()}," +
            $"\"turnTimer\":{turnTimer:F0}," +
            $"\"wave\":{wave}," +
            $"\"totalWaves\":{totalWaves}," +
            $"\"shopRefresh\":\"{EscapeJson(shopRefresh)}\"" +
            "}";

        yield return PostToEbs("/unity/broadcast", json);
    }

    private IEnumerator PostToPush(string userId, string stateJson)
    {
        string payload = $"{{\"userId\":\"{EscapeJson(userId)}\",\"state\":{stateJson}}}";
        yield return PostToEbs("/unity/push", payload);
    }

    // ── HTTP Client (Unity WebRequest) ────────────────────────────────────────

    private IEnumerator PostToEbs(string path, string jsonBody)
    {
        string url = ebsUrl.TrimEnd('/') + path;
        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);

        using (var req = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyBytes);
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("X-Unity-Secret", sharedSecret);
            req.timeout = 5;

            yield return req.SendWebRequest();

            if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[PanelSync] POST {path} failed: {req.responseCode} {req.error}");
            }
        }
    }

    // ── Inbound Server (commands FROM the panel, via EBS) ────────────────────

    private void StartInboundServer()
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{inboundPort}/");
            _listener.Start();
            _running = true;

            _listenerThread = new Thread(InboundListenLoop)
            {
                IsBackground = true,
                Name = "PanelSyncInbound"
            };
            _listenerThread.Start();

            Debug.Log($"[PanelSync] Inbound server listening on port {inboundPort}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PanelSync] Failed to start inbound server: {ex.Message}");
        }
    }

    private void InboundListenLoop()
    {
        while (_running)
        {
            try
            {
                HttpListenerContext ctx = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleInbound(ctx));
            }
            catch (HttpListenerException) { break; }
            catch (Exception ex)
            {
                if (_running) Debug.LogWarning($"[PanelSync] Inbound error: {ex.Message}");
            }
        }
    }

    private void HandleInbound(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var resp = ctx.Response;

        try
        {
            // Auth check
            if (req.Headers["X-EBS-Secret"] != sharedSecret)
            {
                SendInboundResponse(resp, 401, "{\"error\":\"Unauthorized\"}");
                return;
            }

            string body;
            using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                body = reader.ReadToEnd();

            // Parse the minimal fields we need without a JSON library
            string userId = ExtractJsonString(body, "userId");
            string username = ExtractJsonString(body, "username");
            string command = ExtractJsonString(body, "command");
            string[] args = ExtractJsonStringArray(body, "args");

            if (string.IsNullOrEmpty(command))
            {
                SendInboundResponse(resp, 400, "{\"error\":\"Missing command\"}");
                return;
            }

            // Allowed panel commands (subset of full chat commands)
            HashSet<string> allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "queue", "q", "confirm", "stats", "inventory", "inv",
                "abilities", "loadout", "equip", "unequip",
                "equipability", "unequipability", "levelup",
                "sell", "buy", "shop", "coins", "balance",
                "stance", "stances", "pvpstats"
            };

            if (!allowed.Contains(command))
            {
                SendInboundResponse(resp, 400, $"{{\"error\":\"Command '{command}' not available via panel\"}}");
                return;
            }

            // Dispatch to Unity main thread
            var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();

            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                try
                {
                    string result = rpgCommands.HandleRPGCommand(command, userId, username, args ?? new string[0]);
                    tcs.SetResult(result ?? $"Command '{command}' processed.");
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            if (!tcs.Task.Wait(TimeSpan.FromSeconds(8)))
            {
                SendInboundResponse(resp, 504, "{\"error\":\"Unity timed out\"}");
                return;
            }

            if (tcs.Task.IsFaulted)
            {
                Debug.LogError($"[PanelSync] Command error: {tcs.Task.Exception?.InnerException?.Message}");
                SendInboundResponse(resp, 500, "{\"error\":\"Internal error\"}");
                return;
            }

            string responseJson = $"{{\"success\":true,\"message\":{JsonStringLiteral(tcs.Task.Result)}}}";
            SendInboundResponse(resp, 200, responseJson);

            // After a command, immediately push the updated state for this viewer
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                _lastPushedState.Remove(userId); // Force a push next tick
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PanelSync] Inbound request error: {ex.Message}");
            SendInboundResponse(resp, 500, "{\"error\":\"Internal server error\"}");
        }
        finally
        {
            resp.Close();
        }
    }

    private void SendInboundResponse(HttpListenerResponse resp, int status, string json)
    {
        try
        {
            resp.StatusCode = status;
            resp.ContentType = "application/json; charset=utf-8";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            resp.ContentLength64 = bytes.Length;
            resp.OutputStream.Write(bytes, 0, bytes.Length);
        }
        catch { }
    }

    // ── JSON Helpers (no external library dependency) ─────────────────────────

    private static string EscapeJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }

    private static string JsonStringLiteral(string s)
    {
        return "\"" + EscapeJson(s ?? "") + "\"";
    }

    /// <summary>Very simple field extractor — works for flat string fields in known JSON.</summary>
    private static string ExtractJsonString(string json, string key)
    {
        string search = $"\"{key}\":\"";
        int start = json.IndexOf(search, StringComparison.Ordinal);
        if (start < 0) return null;
        start += search.Length;
        int end = json.IndexOf('"', start);
        if (end < 0) return null;
        return json.Substring(start, end - start);
    }

    private static string[] ExtractJsonStringArray(string json, string key)
    {
        string search = $"\"{key}\":[";
        int start = json.IndexOf(search, StringComparison.Ordinal);
        if (start < 0) return new string[0];
        start += search.Length;
        int end = json.IndexOf(']', start);
        if (end < 0) return new string[0];

        string inner = json.Substring(start, end - start).Trim();
        if (string.IsNullOrEmpty(inner)) return new string[0];

        var results = new List<string>();
        int i = 0;
        while (i < inner.Length)
        {
            if (inner[i] == '"')
            {
                int strEnd = inner.IndexOf('"', i + 1);
                if (strEnd < 0) break;
                results.Add(inner.Substring(i + 1, strEnd - i - 1));
                i = strEnd + 1;
            }
            else i++;
        }
        return results.ToArray();
    }

    // ── Public API (call from other scripts to force an immediate push) ───────

    /// <summary>
    /// Force an immediate state push for a specific viewer.
    /// Call this after significant events (item purchase, level up, etc.)
    /// </summary>
    public void ForcePushViewer(string userId)
    {
        _lastPushedState.Remove(userId);
    }

    /// <summary>
    /// Force an immediate global state broadcast.
    /// Call this when expedition/PvP state changes.
    /// </summary>
    public void ForcePushGlobal()
    {
        StartCoroutine(PostGlobalState());
    }
}
