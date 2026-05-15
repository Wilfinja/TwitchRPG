using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Pushes viewer + global state to Railway EBS so the Twitch panel can display it.
///
/// SETUP:
///   1. Add this script to a persistent GameObject in your scene.
///   2. Wire the rpgCommands reference in the Inspector.
///   3. Set ebsUrl      = your Railway URL  (e.g. https://xyz.up.railway.app)
///   4. Set unitySecret = UNITY_SECRET env var in Railway
///   5. Set ebsSecret   = EBS_SECRET   env var in Railway
///   6. In Railway env vars: UNITY_INBOUND_URL = http://<tailscale-ip>:7433
///   7. Allow port 7433 inbound in Windows Firewall (or run cmd as admin):
///      netsh http add urlacl url=http://+:7433/ user=Everyone
///
/// FIX: All floats now use InvariantCulture so "0.123" never becomes "0,123"
///      on European/non-English Windows locales, which was causing Bad Request.
/// </summary>
public class PanelSyncServer : MonoBehaviour
{
    [Header("EBS Connection")]
    [Tooltip("Your Railway EBS URL, e.g. https://twitchrpgebs-production-e7e6.up.railway.app")]
    [SerializeField] private string ebsUrl = "https://twitchrpgebs-production-e7e6.up.railway.app";

    [Tooltip("Must match the UNITY_SECRET environment variable in Railway")]
    [SerializeField] private string unitySecret = "fAquxh3jWudjqPtc7DlilLEEA0Wy9zwR";

    [Tooltip("Must match the EBS_SECRET environment variable in Railway (used for inbound commands)")]
    [SerializeField] private string ebsSecret = "W2rSwaK6hY7a9lMTEgtnlcyNzcKKSoOB";

    [Header("Sync Settings")]
    [Tooltip("How often (seconds) to push all active viewer states")]
    [SerializeField] private float pushIntervalSeconds = 5f;

    [Tooltip("How often (seconds) to push the global game state")]
    [SerializeField] private float globalPushIntervalSeconds = 2f;

    [Tooltip("Max viewers to push per batch (avoids flooding on large streams)")]
    [SerializeField] private int maxViewersPerBatch = 50;

    [Header("Inbound Command Server")]
    [Tooltip("Port the EBS will POST commands to. Must match UNITY_INBOUND_URL in Railway.")]
    [SerializeField] private int inboundPort = 7433;

    [Header("References")]
    [SerializeField] private RPGChatCommands rpgCommands;

    [Header("Debug")]
    [Tooltip("Log every push attempt to the Unity console")]
    [SerializeField] private bool verboseLogging = false;

    // ── Singleton ────────────────────────────────────────────────────────────

    private static PanelSyncServer _instance;
    public static PanelSyncServer Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<PanelSyncServer>();
            return _instance;
        }
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

    // Thread-safe queue for work that must run on Unity's main thread.
    // Background threads (inbound HTTP listener) enqueue actions here;
    // Update() drains it. No third-party package required.
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

    private void EnqueueMainThread(Action action) => _mainThreadQueue.Enqueue(action);

    // Use invariant culture for ALL float-to-string conversions so JSON never
    // contains commas as decimal separators on non-English Windows locales.
    private static readonly IFormatProvider IC = CultureInfo.InvariantCulture;

    private float pushTimer;
    private float globalPushTimer;

    private HttpListener inboundListener;
    private Thread listenerThread;
    private bool listenerRunning;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        pushTimer = 1f;
        globalPushTimer = 0.5f;
        StartInboundListener();
        Debug.Log($"[PanelSync] Started. EBS: {ebsUrl}  Inbound port: {inboundPort}");

        // If using ngrok, auto-detect the public URL and register it with the EBS
        // so you don't have to manually update UNITY_INBOUND_URL after every restart.
        StartCoroutine(RegisterTunnelUrl());
    }

    /// <summary>
    /// Checks the local ngrok agent API for the current public URL and
    /// sends it to the EBS /unity/register-inbound endpoint.
    /// Safe to call even if ngrok isn't running — it silently does nothing.
    /// </summary>
    private IEnumerator RegisterTunnelUrl()
    {
        // Small delay to let ngrok fully start if it was just launched
        yield return new WaitForSeconds(2f);

        // ngrok exposes its current tunnels at localhost:4040/api/tunnels
        string ngrokApi = "http://localhost:4040/api/tunnels";
        bool done = false;
        string tunnelUrl = null;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var req = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Get, ngrokApi);
                var task = http.SendAsync(req);
                task.Wait(3000);
                if (task.Result.IsSuccessStatusCode)
                {
                    string json = task.Result.Content.ReadAsStringAsync().Result;
                    // Parse: {"tunnels":[{"public_url":"https://abc.ngrok-free.app",...}]}
                    int idx = json.IndexOf("\"public_url\":\"https://");
                    if (idx >= 0)
                    {
                        int start = idx + 14;
                        int end = json.IndexOf('"', start);
                        if (end > start)
                            tunnelUrl = json.Substring(start, end - start);
                    }
                }
            }
            catch { /* ngrok not running — normal */ }
            finally { done = true; }
        });

        while (!done) yield return null;

        if (string.IsNullOrEmpty(tunnelUrl)) yield break;

        Debug.Log($"[PanelSync] Detected ngrok tunnel: {tunnelUrl}");

        // Register with EBS so it updates UNITY_INBOUND_URL in memory
        string body = "{\"inboundUrl\":\"" + tunnelUrl + "\"}";
        yield return StartCoroutine(PostToEbs("/unity/register-inbound", body,
            success =>
            {
                if (success)
                    Debug.Log($"[PanelSync] Registered tunnel URL with EBS: {tunnelUrl}");
                else
                    Debug.LogWarning("[PanelSync] Failed to register tunnel URL — update UNITY_INBOUND_URL in Railway manually.");
            }));
    }

    private void Update()
    {
        // Drain the main-thread action queue (filled by inbound HTTP listener)
        while (_mainThreadQueue.TryDequeue(out Action action))
        {
            try { action(); }
            catch (Exception ex) { Debug.LogError($"[PanelSync] Main thread action error: {ex.Message}"); }
        }

        pushTimer -= Time.deltaTime;
        if (pushTimer <= 0f)
        {
            pushTimer = pushIntervalSeconds;
            StartCoroutine(PushAllViewers());
        }

        globalPushTimer -= Time.deltaTime;
        if (globalPushTimer <= 0f)
        {
            globalPushTimer = globalPushIntervalSeconds;
            StartCoroutine(PushGlobalState());
        }
    }

    private void OnApplicationQuit()
    {
        listenerRunning = false;
        try { inboundListener?.Stop(); } catch { }
    }

    private void OnDestroy()
    {
        listenerRunning = false;
        try { inboundListener?.Stop(); } catch { }
    }

    // ── Push ALL viewers in database ─────────────────────────────────────────

    private IEnumerator PushAllViewers()
    {
        if (RPGManager.Instance == null) yield break;

        List<ViewerData> allViewers = RPGManager.Instance.GetAllViewers();
        if (allViewers == null || allViewers.Count == 0) yield break;

        int count = 0;
        foreach (ViewerData viewer in allViewers)
        {
            yield return StartCoroutine(PushSingleViewer(viewer));
            count++;
            if (count >= maxViewersPerBatch) break;
            yield return new WaitForSeconds(0.05f);
        }

        if (verboseLogging)
            Debug.Log($"[PanelSync] Pushed {count} viewer(s)");
    }

    /// <summary>Call after any data change to update the panel immediately.</summary>
    public void PushViewerImmediate(string userId)
    {
        ViewerData viewer = RPGManager.Instance?.GetViewer(userId);
        if (viewer != null)
            StartCoroutine(PushSingleViewer(viewer));
    }

    private IEnumerator PushSingleViewer(ViewerData viewer)
    {
        string stateJson = BuildViewerPayload(viewer);
        string body = "{\"userId\":\"" + EscJson(viewer.twitchUserId) + "\",\"state\":" + stateJson + "}";

        if (verboseLogging)
            Debug.Log($"[PanelSync] Pushing {viewer.username} | JSON length: {body.Length}");

        yield return StartCoroutine(PostToEbs("/unity/push", body,
            success =>
            {
                if (!success && verboseLogging)
                    Debug.LogWarning($"[PanelSync] Failed push for {viewer.username}");
            }));
    }

    // ── Build viewer state payload ───────────────────────────────────────────

    private string BuildViewerPayload(ViewerData viewer)
    {
        CharacterStats total = viewer.GetTotalStats();
        CharacterStats base_ = viewer.baseStats;

        int xpNeeded = 150;
        float xpProgress = 0f;
        if (ExperienceManager.Instance != null)
        {
            xpNeeded = ExperienceManager.Instance.GetXPForNextLevel(base_.level);
            xpProgress = ExperienceManager.Instance.GetLevelProgress(viewer);
        }

        // Use IC.ToString() for every float so decimal separator is always "."
        string xpProgressStr = xpProgress.ToString("F3", IC);

        string head = EscJson(viewer.equipped.head?.itemName ?? "");
        string chest = EscJson(viewer.equipped.chest?.itemName ?? "");
        string mainHand = EscJson(viewer.equipped.mainHand?.itemName ?? "");
        string offHand = EscJson(viewer.equipped.offHand?.itemName ?? "");
        string arms = EscJson(viewer.equipped.arms?.itemName ?? "");
        string legs = EscJson(viewer.equipped.legs?.itemName ?? "");

        // Up to 12 inventory items
        var invBuilder = new StringBuilder("[");
        int shown = Mathf.Min(12, viewer.inventory.Count);
        for (int i = 0; i < shown; i++)
        {
            RPGItem item = viewer.inventory[i];
            if (i > 0) invBuilder.Append(",");
            invBuilder.Append("{");
            invBuilder.Append("\"id\":\"").Append(EscJson(item.itemId)).Append("\",");
            invBuilder.Append("\"name\":\"").Append(EscJson(item.itemName)).Append("\",");
            invBuilder.Append("\"rarity\":\"").Append(item.rarity).Append("\",");
            invBuilder.Append("\"type\":\"").Append(item.itemType).Append("\",");
            invBuilder.Append("\"price\":").Append(item.price);
            invBuilder.Append("}");
        }
        invBuilder.Append("]");

        // Equipped abilities (in loadout) — viewer.equippedAbilities is List<string> of commandNames
        var abilBuilder = new StringBuilder("[");
        for (int i = 0; i < viewer.equippedAbilities.Count; i++)
        {
            string cmd = viewer.equippedAbilities[i];
            AbilityData ab = AbilityDatabase.Instance?.GetAbility(cmd);
            string name = ab != null ? EscJson(ab.abilityName) : EscJson(cmd);
            if (i > 0) abilBuilder.Append(",");
            abilBuilder.Append("{\"cmd\":\"").Append(EscJson(cmd)).Append("\",\"name\":\"").Append(name).Append("\"}");
        }
        abilBuilder.Append("]");

        // Available and locked abilities — mirrors HandleAbilitiesCommand exactly:
        //   AbilityDatabase.GetAbilitiesForClass() returns all abilities for this class.
        //   Split on levelRequired vs viewer.baseStats.level.
        //   "available" = unlocked (level met), shown with EQUIP button.
        //   "locked"    = not yet unlocked, shown greyed out with level requirement.
        var availAbilBuilder = new StringBuilder("[");
        var lockedAbilBuilder = new StringBuilder("[");

        int availCount = 0;
        int lockedCount = 0;

        if (AbilityDatabase.Instance != null)
        {
            List<AbilityData> classAbilities = AbilityDatabase.Instance.GetAbilitiesForClass(viewer.characterClass);
            int playerLevel = viewer.baseStats.level;

            foreach (AbilityData ab in classAbilities)
            {
                if (playerLevel >= ab.levelRequired)
                {
                    // Available (unlocked) — can be equipped
                    if (availCount > 0) availAbilBuilder.Append(",");
                    availAbilBuilder.Append("{")
                        .Append("\"cmd\":\"").Append(EscJson(ab.commandName)).Append("\",")
                        .Append("\"name\":\"").Append(EscJson(ab.abilityName)).Append("\",")
                        .Append("\"levelRequired\":").Append(ab.levelRequired)
                        .Append("}");
                    availCount++;
                }
                else
                {
                    // Locked — show with level requirement
                    if (lockedCount > 0) lockedAbilBuilder.Append(",");
                    lockedAbilBuilder.Append("{")
                        .Append("\"cmd\":\"").Append(EscJson(ab.commandName)).Append("\",")
                        .Append("\"name\":\"").Append(EscJson(ab.abilityName)).Append("\",")
                        .Append("\"levelRequired\":").Append(ab.levelRequired)
                        .Append("}");
                    lockedCount++;
                }
            }
        }

        availAbilBuilder.Append("]");
        lockedAbilBuilder.Append("]");

        // Max loadout slots — hardcoded at 4 to match HandleEquipAbilityCommand
        int maxSlots = 4;

        // Queued combat action
        string queuedAction = "";
        OnScreenCharacter onScreen = CharacterSpawner.Instance?.GetCharacter(viewer.twitchUserId);
        if (onScreen != null)
        {
            CombatEntity entity = onScreen.GetComponent<CombatEntity>();
            if (entity != null && !string.IsNullOrEmpty(entity.queuedAction))
                queuedAction = EscJson(entity.queuedAction);
        }

        // Build final JSON using StringBuilder to avoid any interpolation ambiguity
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append("\"userId\":\"").Append(EscJson(viewer.twitchUserId)).Append("\",");
        sb.Append("\"username\":\"").Append(EscJson(viewer.username)).Append("\",");
        sb.Append("\"class\":\"").Append(viewer.characterClass).Append("\",");
        sb.Append("\"level\":").Append(base_.level).Append(",");
        sb.Append("\"xp\":").Append(base_.experience).Append(",");
        sb.Append("\"xpNeeded\":").Append(xpNeeded).Append(",");
        sb.Append("\"xpProgress\":").Append(xpProgressStr).Append(",");
        sb.Append("\"hp\":").Append(base_.currentHealth).Append(",");
        sb.Append("\"maxHp\":").Append(total.maxHealth).Append(",");
        sb.Append("\"coins\":").Append(viewer.coins).Append(",");
        sb.Append("\"unallocatedPoints\":").Append(base_.unallocatedStatPoints).Append(",");

        // stats object (all ints — no float risk)
        sb.Append("\"stats\":{");
        sb.Append("\"str\":").Append(total.strength).Append(",");
        sb.Append("\"con\":").Append(total.constitution).Append(",");
        sb.Append("\"dex\":").Append(total.dexterity).Append(",");
        sb.Append("\"wil\":").Append(total.willpower).Append(",");
        sb.Append("\"cha\":").Append(total.charisma).Append(",");
        sb.Append("\"int\":").Append(total.intelligence);
        sb.Append("},");

        // baseStats object
        sb.Append("\"baseStats\":{");
        sb.Append("\"str\":").Append(base_.strength).Append(",");
        sb.Append("\"con\":").Append(base_.constitution).Append(",");
        sb.Append("\"dex\":").Append(base_.dexterity).Append(",");
        sb.Append("\"wil\":").Append(base_.willpower).Append(",");
        sb.Append("\"cha\":").Append(base_.charisma).Append(",");
        sb.Append("\"int\":").Append(base_.intelligence);
        sb.Append("},");

        sb.Append("\"damageBonus\":").Append(viewer.equipped.GetTotalDamageBonus()).Append(",");
        sb.Append("\"defenseBonus\":").Append(viewer.equipped.GetTotalDefenseBonus()).Append(",");

        // equipped object
        sb.Append("\"equipped\":{");
        sb.Append("\"head\":\"").Append(head).Append("\",");
        sb.Append("\"chest\":\"").Append(chest).Append("\",");
        sb.Append("\"mainHand\":\"").Append(mainHand).Append("\",");
        sb.Append("\"offHand\":\"").Append(offHand).Append("\",");
        sb.Append("\"arms\":\"").Append(arms).Append("\",");
        sb.Append("\"legs\":\"").Append(legs).Append("\"");
        sb.Append("},");

        sb.Append("\"inventory\":").Append(invBuilder).Append(",");
        sb.Append("\"inventoryCount\":").Append(viewer.inventory.Count).Append(",");
        sb.Append("\"abilities\":").Append(abilBuilder).Append(",");
        sb.Append("\"availableAbilities\":").Append(availAbilBuilder).Append(",");
        sb.Append("\"lockedAbilities\":").Append(lockedAbilBuilder).Append(",");
        sb.Append("\"maxAbilitySlots\":").Append(maxSlots).Append(",");
        sb.Append("\"pvpWins\":").Append(viewer.pvpWins).Append(",");
        sb.Append("\"pvpLosses\":").Append(viewer.pvpLosses).Append(",");
        sb.Append("\"isDead\":").Append(viewer.isDead ? "true" : "false").Append(",");
        sb.Append("\"queuedAction\":\"").Append(queuedAction).Append("\"");
        sb.Append("}");

        return sb.ToString();
    }

    // ── Global state ─────────────────────────────────────────────────────────

    private IEnumerator PushGlobalState()
    {
        bool expeditionActive = ExpeditionManager.Instance != null &&
                                ExpeditionManager.Instance.currentExpedition.isActive;
        bool combatActive = CombatTurnManager.Instance != null &&
                                CombatTurnManager.Instance.combatActive;
        bool pvpActive = PvPManager.Instance != null &&
                                PvPManager.Instance.pvpActive;
        bool isPlayerTurn = combatActive && CombatTurnManager.Instance.playerTurn;

        float turnTimer = isPlayerTurn ? CombatTurnManager.Instance.turnTimer : 0f;
        float maxTurnTime = CombatTurnManager.Instance != null
                                ? CombatTurnManager.Instance.maxTurnTime : 45f;

        int wave = 0, totalWaves = 0;
        if (expeditionActive)
        {
            wave = ExpeditionManager.Instance.currentExpedition.currentWave;
            totalWaves = ExpeditionManager.Instance.currentExpedition.totalWaves;
        }

        string shopRefresh = "";
        if (ShopManager.Instance != null)
        {
            TimeSpan ts = ShopManager.Instance.GetTimeUntilRefresh();
            shopRefresh = ts.Hours + "h " + ts.Minutes + "m";
        }

        // Floats use IC so decimal is always "."
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append("\"expeditionActive\":").Append(expeditionActive ? "true" : "false").Append(",");
        sb.Append("\"combatActive\":").Append(combatActive ? "true" : "false").Append(",");
        sb.Append("\"pvpActive\":").Append(pvpActive ? "true" : "false").Append(",");
        sb.Append("\"isPlayerTurn\":").Append(isPlayerTurn ? "true" : "false").Append(",");
        sb.Append("\"turnTimer\":").Append(turnTimer.ToString("F1", IC)).Append(",");
        sb.Append("\"maxTurnTime\":").Append(maxTurnTime.ToString("F1", IC)).Append(",");
        sb.Append("\"wave\":").Append(wave).Append(",");
        sb.Append("\"totalWaves\":").Append(totalWaves).Append(",");
        sb.Append("\"shopRefresh\":\"").Append(EscJson(shopRefresh)).Append("\"");
        sb.Append("}");

        yield return StartCoroutine(PostToEbs("/unity/broadcast", sb.ToString(), null));
    }

    // ── HTTP POST ────────────────────────────────────────────────────────────

    private IEnumerator PostToEbs(string endpoint, string jsonBody, Action<bool> callback)
    {
        string url = ebsUrl.TrimEnd('/') + endpoint;
        bool done = false;
        bool success = false;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };
                req.Headers.Add("X-Unity-Secret", unitySecret);

                var task = http.SendAsync(req);
                task.Wait();
                success = task.Result.IsSuccessStatusCode;

                if (!success)
                {
                    string resp = task.Result.Content.ReadAsStringAsync().Result;
                    // Trim HTML error pages to a readable length
                    if (resp.Length > 300) resp = resp.Substring(0, 300) + "…";
                    Debug.LogWarning($"[PanelSync] EBS {task.Result.StatusCode} on {endpoint}: {resp}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PanelSync] POST {endpoint} failed: {ex.Message}");
            }
            finally { done = true; }
        });

        while (!done) yield return null;
        callback?.Invoke(success);
    }

    // ── Inbound listener (EBS → Unity) ───────────────────────────────────────

    private void StartInboundListener()
    {
        try
        {
            inboundListener = new HttpListener();
            inboundListener.Prefixes.Add($"http://+:{inboundPort}/");
            inboundListener.Start();
            listenerRunning = true;

            listenerThread = new Thread(InboundListenLoop)
            {
                IsBackground = true,
                Name = "PanelSyncInbound"
            };
            listenerThread.Start();

            Debug.Log($"[PanelSync] Inbound listener on port {inboundPort}");
        }
        catch (Exception ex)
        {
            Debug.LogError(
                $"[PanelSync] Could not start listener on port {inboundPort}: {ex.Message}\n" +
                "Fix: run Unity as Administrator, or run this in an admin cmd prompt:\n" +
                $"  netsh http add urlacl url=http://+:{inboundPort}/ user=Everyone");
        }
    }

    private void InboundListenLoop()
    {
        while (listenerRunning)
        {
            try
            {
                HttpListenerContext ctx = inboundListener.GetContext();
                ThreadPool.QueueUserWorkItem(_ => HandleInboundRequest(ctx));
            }
            catch (HttpListenerException) { break; }
            catch (Exception ex)
            {
                if (listenerRunning)
                    Debug.LogWarning($"[PanelSync] Inbound error: {ex.Message}");
            }
        }
    }

    private void HandleInboundRequest(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var resp = ctx.Response;

        try
        {
            if (req.Headers["X-EBS-Secret"] != ebsSecret)
            {
                SendInboundResponse(resp, 401, "{\"error\":\"Unauthorized\"}");
                return;
            }

            if (req.HttpMethod != "POST")
            {
                SendInboundResponse(resp, 405, "{\"error\":\"Method not allowed\"}");
                return;
            }

            string body;
            using (var reader = new System.IO.StreamReader(req.InputStream, req.ContentEncoding))
                body = reader.ReadToEnd();

            string userId = ExtractJsonString(body, "userId");
            string username = ExtractJsonString(body, "username");
            string command = ExtractJsonString(body, "command");
            string[] args = ExtractJsonStringArray(body, "args");

            if (string.IsNullOrEmpty(command))
            {
                SendInboundResponse(resp, 400, "{\"error\":\"Missing command\"}");
                return;
            }

            // Health ping from the EBS debug route — respond immediately, no main thread needed
            if (command == "ping")
            {
                SendInboundResponse(resp, 200, "{\"success\":true,\"message\":\"Unity inbound listener is reachable\"}");
                return;
            }

            // ── Translate unequipability cmd-name → slot number ───────────────
            // HandleUnequipAbilityCommand in RPGChatCommands only accepts a 1-based
            // slot number. The EBS forwards by command name (e.g. "backstab") so
            // we translate it here on the Unity side before dispatching.
            if ((command == "unequipability" || command == "unequipa") &&
                args.Length > 0 && !int.TryParse(args[0], out _))
            {
                string cmdName = args[0].ToLower();
                ViewerData tempViewer = RPGManager.Instance?.GetViewer(userId);
                if (tempViewer != null)
                {
                    int slot = tempViewer.equippedAbilities.IndexOf(cmdName);
                    if (slot >= 0)
                        args = new string[] { (slot + 1).ToString() };
                    // If not found, pass through as-is and let RPGChatCommands error
                }
            }

            // Enqueue on our own thread-safe queue — no third-party package needed.
            // Update() drains this queue on Unity's main thread every frame.
            // We use a simple int flag (0=pending, 1=done) with Interlocked
            // so the background thread can spin-wait without any lock primitives.
            string result = null;
            int completed = 0;  // 0 = pending, 1 = done

            EnqueueMainThread(() =>
            {
                try
                {
                    result = rpgCommands.HandleRPGCommand(command, userId, username, args)
                             ?? $"Command '{command}' processed.";
                    PushViewerImmediate(userId);
                }
                catch (Exception ex)
                {
                    result = $"Error: {ex.Message}";
                    Debug.LogError($"[PanelSync] Command error '{command}': {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref completed, 1);
                }
            });

            // Spin-wait up to 10s for Unity's main thread to process it.
            // Thread.Sleep(20) keeps CPU usage negligible while waiting.
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (Interlocked.CompareExchange(ref completed, 0, 0) == 0)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    SendInboundResponse(resp, 504, "{\"error\":\"Unity main thread timeout — is RPGManager running?\"}");
                    return;
                }
                Thread.Sleep(20);
            }

            string safe = (result ?? "")
                .Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\n", "\\n").Replace("\r", "");

            // Detect error responses from RPGChatCommands
            bool isError = result != null && !result.StartsWith("✅") &&
                           (result.Contains("Invalid") ||
                            result.Contains("not found") ||
                            result.Contains("full") ||
                            result.Contains("Failed") ||
                            result.Contains("can't") ||
                            result.Contains("Cannot") ||
                            result.Contains("already") ||
                            result.Contains("requires level") ||
                            result.Contains("Requires") ||
                            result.Contains("must ") ||
                            result.Contains("banned") ||
                            result.Contains("not available"));

            string successVal = isError ? "false" : "true";
            SendInboundResponse(resp, 200,
                "{\"success\":" + successVal +
                ",\"message\":\"" + safe + "\"" +
                ",\"error\":\"" + (isError ? safe : "") + "\"}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PanelSync] Inbound unhandled: {ex.Message}");
            SendInboundResponse(resp, 500, "{\"error\":\"Internal error\"}");
        }
        finally
        {
            // Close() is safe to call even if SendInboundResponse already closed the stream
            try { resp.Close(); } catch { }
        }
    }

    private static void SendInboundResponse(HttpListenerResponse resp, int code, string json)
    {
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            resp.StatusCode = code;
            resp.ContentType = "application/json; charset=utf-8";
            resp.ContentLength64 = bytes.Length;
            resp.SendChunked = false;
            resp.KeepAlive = false;
            resp.Headers.Set("Connection", "close");
            resp.OutputStream.Write(bytes, 0, bytes.Length);
            resp.OutputStream.Flush();
            resp.OutputStream.Close();
        }
        catch { }
    }

    // ── JSON helpers ─────────────────────────────────────────────────────────

    private static string EscJson(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "")
                .Replace("\t", "\\t");
    }

    private static string ExtractJsonString(string json, string key)
    {
        string search = "\"" + key + "\":\"";
        int start = json.IndexOf(search, StringComparison.Ordinal);
        if (start < 0) return "";
        start += search.Length;
        int end = json.IndexOf('"', start);
        return end < 0 ? "" : json.Substring(start, end - start);
    }

    private static string[] ExtractJsonStringArray(string json, string key)
    {
        string search = "\"" + key + "\":[";
        int start = json.IndexOf(search, StringComparison.Ordinal);
        if (start < 0) return Array.Empty<string>();
        start += search.Length;
        int end = json.IndexOf(']', start);
        if (end < 0) return Array.Empty<string>();

        string inner = json.Substring(start, end - start).Trim();
        if (string.IsNullOrEmpty(inner)) return Array.Empty<string>();

        var result = new List<string>();
        int i = 0;
        while (i < inner.Length)
        {
            if (inner[i] == '"')
            {
                int vStart = i + 1;
                int vEnd = inner.IndexOf('"', vStart);
                if (vEnd < 0) break;
                result.Add(inner.Substring(vStart, vEnd - vStart));
                i = vEnd + 1;
            }
            else i++;
        }
        return result.ToArray();
    }
}
