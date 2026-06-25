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

    private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

    // Guards to prevent push pileup — if a push is already in flight, skip the next tick
    private bool _viewerPushInFlight = false;
    private bool _globalPushInFlight = false;

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
    /// Registers the current Tailscale Funnel URL (or ngrok URL as fallback) with
    /// the EBS so Railway always knows where to forward panel commands, without
    /// needing to update UNITY_INBOUND_URL in Railway env vars after every reboot.
    ///
    /// Tailscale Funnel URL is stable (based on machine name) so we construct it
    /// directly from the Tailscale status API. ngrok is checked as a fallback.
    /// </summary>
    private IEnumerator RegisterTunnelUrl()
    {
        yield return new WaitForSeconds(2f);

        string tunnelUrl = null;
        bool done = false;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                // ── Strategy 1: Read from environment variable ────────────────
                // If UNITY_INBOUND_URL is already set correctly in Railway, we
                // just confirm it by reading it from the Inspector field — no
                // detection needed. This is the most reliable approach.
                // We try detection anyway so the URL stays fresh after reboots.

                // ── Strategy 2: Tailscale local API (named pipe on Windows) ──
                // Tailscale on Windows exposes a local HTTP API via a named pipe.
                // We can reach it through a fixed loopback address it also binds.
                // Try several known ports/addresses in order.
                string[] tailscaleApis = new[]
                {
                    "http://100.100.100.100/localapi/v0/status",  // Tailscale's magic DNS loopback
                    "http://127.0.0.1:41112/localapi/v0/status",  // older Tailscale versions
                };

                foreach (string api in tailscaleApis)
                {
                    try
                    {
                        var req = new System.Net.Http.HttpRequestMessage(
                            System.Net.Http.HttpMethod.Get, api);
                        var task = http.SendAsync(req);
                        if (!task.Wait(2000) || !task.Result.IsSuccessStatusCode) continue;

                        string json = task.Result.Content.ReadAsStringAsync().Result;
                        int idx = json.IndexOf("\"DNSName\":\"", StringComparison.Ordinal);
                        if (idx < 0) continue;
                        int start = idx + 11;
                        int end = json.IndexOf('"', start);
                        if (end <= start) continue;
                        string dnsName = json.Substring(start, end - start).TrimEnd('.');
                        if (!string.IsNullOrEmpty(dnsName))
                        {
                            tunnelUrl = $"https://{dnsName}";
                            UnityEngine.Debug.Log($"[PanelSync] Tailscale API → {tunnelUrl}");
                            break;
                        }
                    }
                    catch { /* try next */ }
                }

                // ── Strategy 3: Run tailscale.exe CLI ────────────────────────
                if (string.IsNullOrEmpty(tunnelUrl))
                {
                    string[] exePaths = new[]
                    {
                        @"C:\Program Files\Tailscale\tailscale.exe",
                        @"C:\Program Files (x86)\Tailscale\tailscale.exe",
                        "tailscale",  // PATH fallback
                    };

                    foreach (string exe in exePaths)
                    {
                        try
                        {
                            if (exe != "tailscale" && !System.IO.File.Exists(exe)) continue;

                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = exe,
                                Arguments = "status --json",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                            };
                            using var proc = System.Diagnostics.Process.Start(psi);
                            string json = proc.StandardOutput.ReadToEnd();
                            proc.WaitForExit(3000);

                            int idx = json.IndexOf("\"DNSName\":\"", StringComparison.Ordinal);
                            if (idx < 0) continue;
                            int start = idx + 11;
                            int end = json.IndexOf('"', start);
                            if (end <= start) continue;
                            string dnsName = json.Substring(start, end - start).TrimEnd('.');
                            if (!string.IsNullOrEmpty(dnsName))
                            {
                                tunnelUrl = $"https://{dnsName}";
                                UnityEngine.Debug.Log($"[PanelSync] tailscale CLI → {tunnelUrl}");
                                break;
                            }
                        }
                        catch { /* try next */ }
                    }
                }

                // ── Strategy 4: ngrok fallback ────────────────────────────────
                if (string.IsNullOrEmpty(tunnelUrl))
                {
                    try
                    {
                        var req = new System.Net.Http.HttpRequestMessage(
                            System.Net.Http.HttpMethod.Get, "http://localhost:4040/api/tunnels");
                        var task = http.SendAsync(req);
                        if (task.Wait(2000) && task.Result.IsSuccessStatusCode)
                        {
                            string json = task.Result.Content.ReadAsStringAsync().Result;
                            int idx = json.IndexOf("\"public_url\":\"https://", StringComparison.Ordinal);
                            if (idx >= 0)
                            {
                                int start = idx + 14;
                                int end = json.IndexOf('"', start);
                                if (end > start)
                                {
                                    tunnelUrl = json.Substring(start, end - start);
                                    UnityEngine.Debug.Log($"[PanelSync] ngrok → {tunnelUrl}");
                                }
                            }
                        }
                    }
                    catch { /* ngrok not running */ }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[PanelSync] RegisterTunnelUrl error: {ex.Message}");
            }
            finally { done = true; }
        });

        while (!done) yield return null;

        if (string.IsNullOrEmpty(tunnelUrl))
        {
            // Detection failed — but if the Railway env var is already correct
            // (as shown by UNITY_INBOUND_URL in Railway), this is harmless.
            // The EBS already knows the right URL from the env var.
            Debug.Log("[PanelSync] Could not auto-detect tunnel URL — " +
                      "UNITY_INBOUND_URL in Railway will be used as-is. " +
                      "This is fine if the env var is already correct.");
            yield break;
        }

        string body = "{\"url\":\"" + tunnelUrl + "\"}";
        yield return StartCoroutine(PostToEbs("/unity/register-inbound", body,
            success =>
            {
                if (success)
                    Debug.Log($"[PanelSync] EBS inbound URL updated to: {tunnelUrl}");
                else
                    Debug.LogWarning("[PanelSync] Failed to register tunnel URL with EBS — " +
                                     "update UNITY_INBOUND_URL in Railway manually.");
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
            if (!_viewerPushInFlight)
                StartCoroutine(PushAllViewers());
        }

        globalPushTimer -= Time.deltaTime;
        if (globalPushTimer <= 0f)
        {
            globalPushTimer = globalPushIntervalSeconds;
            if (!_globalPushInFlight)
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
        _viewerPushInFlight = true;
        try
        {
            if (RPGManager.Instance == null) yield break;

            List<ViewerData> allViewers = RPGManager.Instance.GetAllViewers();
            if (allViewers == null || allViewers.Count == 0) yield break;

            // Build a single batch payload instead of one request per viewer.
            // This reduces 27 HTTP round-trips to 1, preventing timeout pile-up.
            var sb = new StringBuilder("[");
            int count = 0;
            foreach (ViewerData viewer in allViewers)
            {
                if (count >= maxViewersPerBatch) break;
                if (count > 0) sb.Append(",");
                sb.Append("{\"userId\":\"").Append(EscJson(viewer.twitchUserId))
                  .Append("\",\"state\":").Append(BuildViewerPayload(viewer)).Append("}");
                count++;
            }
            sb.Append("]");

            string body = "{\"viewers\":" + sb.ToString() + "}";

            yield return StartCoroutine(PostToEbs("/unity/push-batch", body,
                success =>
                {
                    if (verboseLogging)
                        Debug.Log($"[PanelSync] Batch pushed {count} viewer(s) — success: {success}");
                    else if (!success)
                        Debug.LogWarning($"[PanelSync] Batch push failed for {count} viewers");
                }));
        }
        finally
        {
            _viewerPushInFlight = false;
        }
    }

    /// <summary>Call after any data change to update the panel immediately.</summary>
    public void PushViewerImmediate(string userId)
    {
        ViewerData viewer = RPGManager.Instance?.GetViewer(userId);
        if (viewer != null)
            StartCoroutine(PushSingleViewer(viewer));
    }

    // Single-viewer push — only used for immediate updates after commands.
    // Regular interval pushes use the batch endpoint above.
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
        // Also read per-ability cooldown remaining turns from CombatEntity if in combat.
        var abilBuilder = new StringBuilder("[");

        // Get OnScreenCharacter and CombatEntity — used for cooldowns and queuedAction below
        OnScreenCharacter onScreen = CharacterSpawner.Instance?.GetCharacter(viewer.twitchUserId);
        CombatEntity combatEntity = null;
        if (onScreen != null)
            combatEntity = onScreen.GetComponent<CombatEntity>();

        for (int i = 0; i < viewer.equippedAbilities.Count; i++)
        {
            string cmd = viewer.equippedAbilities[i];
            AbilityData ab = AbilityDatabase.Instance?.GetAbility(cmd);
            string name = ab != null ? EscJson(ab.abilityName) : EscJson(cmd);
            int cooldownMax = ab != null ? ab.cooldown : 0;

            // Read remaining cooldown turns from CombatEntity if available
            int cooldownRemaining = 0;
            if (combatEntity != null && combatEntity.abilityCooldowns != null
                && combatEntity.abilityCooldowns.TryGetValue(cmd, out int cd))
                cooldownRemaining = Mathf.Max(0, cd);

            if (i > 0) abilBuilder.Append(",");
            abilBuilder.Append("{")
                .Append("\"cmd\":\"").Append(EscJson(cmd)).Append("\",")
                .Append("\"name\":\"").Append(name).Append("\",")
                .Append("\"cooldownMax\":").Append(cooldownMax).Append(",")
                .Append("\"cooldownRemaining\":").Append(cooldownRemaining).Append(",")
                .Append("\"canTargetEnemies\":").Append(ab != null && ab.canTargetEnemies ? "true" : "false").Append(",")
                .Append("\"canTargetAllies\":").Append(ab != null && ab.canTargetAllies ? "true" : "false").Append(",")
                .Append("\"targetType\":\"").Append(ab != null ? ab.targetType.ToString() : "SingleEnemy").Append("\",")
                .Append("\"minTargetPosition\":").Append(ab != null ? ab.minTargetPosition : 1).Append(",")
                .Append("\"maxTargetPosition\":").Append(ab != null ? ab.maxTargetPosition : 1)
                .Append("}");
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

        // Item ability slot — single ability granted by an equipped item.
        // Stored as a command name string on ViewerData; look up display name
        // and cooldown the same way class abilities are handled above.
        string itemAbilityJson = "null";
        if (!string.IsNullOrEmpty(viewer.equippedItemAbility))
        {
            string iaCmd = viewer.equippedItemAbility;
            AbilityData iaData = AbilityDatabase.Instance?.GetAbility(iaCmd);
            string iaName = iaData != null ? EscJson(iaData.abilityName) : EscJson(iaCmd);
            int iaCdMax = iaData != null ? iaData.cooldown : 0;
            int iaCdLeft = 0;
            if (combatEntity != null && combatEntity.abilityCooldowns != null
                && combatEntity.abilityCooldowns.TryGetValue(iaCmd, out int iacd))
                iaCdLeft = Mathf.Max(0, iacd);

            itemAbilityJson = "{" +
                "\"cmd\":\"" + EscJson(iaCmd) + "\"," +
                "\"name\":\"" + iaName + "\"," +
                "\"cooldownMax\":" + iaCdMax + "," +
                "\"cooldownRemaining\":" + iaCdLeft + "," +
                "\"canTargetEnemies\":" + (iaData != null && iaData.canTargetEnemies ? "true" : "false") + "," +
                "\"canTargetAllies\":" + (iaData != null && iaData.canTargetAllies ? "true" : "false") + "," +
                "\"targetType\":\"" + (iaData != null ? iaData.targetType.ToString() : "SingleEnemy") + "\"," +
                "\"minTargetPosition\":" + (iaData != null ? iaData.minTargetPosition : 1) + "," +
                "\"maxTargetPosition\":" + (iaData != null ? iaData.maxTargetPosition : 1) +
                "}";
        }

        // Queued combat action
        string queuedAction = "";
        if (onScreen != null)
        {
            if (combatEntity != null && !string.IsNullOrEmpty(combatEntity.queuedAction))
                queuedAction = EscJson(combatEntity.queuedAction);
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
        sb.Append("\"itemAbility\":").Append(itemAbilityJson).Append(",");
        sb.Append("\"queuedAction\":\"").Append(queuedAction).Append("\"");
        sb.Append("}");

        return sb.ToString();
    }

    // ── Global state ─────────────────────────────────────────────────────────

    private IEnumerator PushGlobalState()
    {
        _globalPushInFlight = true;
        try
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

            // Build shop items array for the panel
            // Sent in global state so all viewers see the same shop without
            // duplicating the data in every per-viewer payload.
            var shopBuilder = new StringBuilder("[");
            if (ShopManager.Instance != null)
            {
                List<RPGItem> shopItems = ShopManager.Instance.GetCurrentShopItems();
                for (int i = 0; i < shopItems.Count; i++)
                {
                    RPGItem item = shopItems[i];
                    if (i > 0) shopBuilder.Append(",");
                    shopBuilder.Append("{");
                    shopBuilder.Append("\"name\":\"").Append(EscJson(item.itemName)).Append("\",");
                    shopBuilder.Append("\"rarity\":\"").Append(item.rarity).Append("\",");
                    shopBuilder.Append("\"type\":\"").Append(item.itemType).Append("\",");
                    shopBuilder.Append("\"price\":").Append(item.price).Append(",");
                    shopBuilder.Append("\"dmg\":").Append(item.damageBonus).Append(",");
                    shopBuilder.Append("\"def\":").Append(item.defenseBonus);
                    shopBuilder.Append("}");
                }
            }
            shopBuilder.Append("]");

            // Build enemy positions array — sent in global state since all
            // viewers see the same enemies. Each slot: {pos, name, alive}.
            // Positions 1-4 always emitted so the panel can show empty slots.
            var enemyBuilder = new StringBuilder("[");
            var allyBuilder = new StringBuilder("[");

            if (expeditionActive && ExpeditionManager.Instance != null)
            {
                List<CombatEntity> liveEnemies = ExpeditionManager.Instance.GetAllEnemyEntities();
                List<CombatEntity> livePlayers = ExpeditionManager.Instance.GetAllPlayerEntities();

                // Build a position→entity lookup for enemies (positions 1-6)
                for (int pos = 1; pos <= 6; pos++)
                {
                    CombatEntity e = liveEnemies.Find(en => en.position == pos);
                    if (pos > 1) enemyBuilder.Append(",");
                    enemyBuilder.Append("{")
                        .Append("\"pos\":").Append(pos).Append(",")
                        .Append("\"alive\":").Append(e != null ? "true" : "false")
                        .Append("}");
                }

                // Build a position→entity lookup for allies (positions 1-4)
                for (int pos = 1; pos <= 4; pos++)
                {
                    CombatEntity p = livePlayers.Find(pl => pl.position == pos);
                    if (pos > 1) allyBuilder.Append(",");
                    allyBuilder.Append("{")
                        .Append("\"pos\":").Append(pos).Append(",")
                        .Append("\"alive\":").Append(p != null ? "true" : "false")
                        .Append("}");
                }
            }
            else
            {
                // Not in expedition — emit empty slots so panel always has arrays
                for (int pos = 1; pos <= 6; pos++)
                {
                    if (pos > 1) enemyBuilder.Append(",");
                    enemyBuilder.Append("{\"pos\":").Append(pos).Append(",\"alive\":false}");
                }
                for (int pos = 1; pos <= 4; pos++)
                {
                    if (pos > 1) allyBuilder.Append(",");
                    allyBuilder.Append("{\"pos\":").Append(pos).Append(",\"alive\":false}");
                }
            }

            enemyBuilder.Append("]");
            allyBuilder.Append("]");

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
            sb.Append("\"shopRefresh\":\"").Append(EscJson(shopRefresh)).Append("\",");
            sb.Append("\"shopItems\":").Append(shopBuilder).Append(",");
            sb.Append("\"enemyPositions\":").Append(enemyBuilder).Append(",");
            sb.Append("\"allyPositions\":").Append(allyBuilder);
            sb.Append("}");

            yield return StartCoroutine(PostToEbs("/unity/broadcast", sb.ToString(), null));
        }
        finally
        {
            _globalPushInFlight = false;
        }
    }

    // ── HTTP POST ────────────────────────────────────────────────────────────

    private IEnumerator PostToEbs(string endpoint, string jsonBody, Action<bool> callback)
    {
        string url = ebsUrl.TrimEnd('/') + endpoint;
        bool done = false;
        bool success = false;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            // Per-request CancellationToken — prevents TaskCanceledException from
            // the global HttpClient timeout firing while other requests are in flight.
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };
                req.Headers.Add("X-Unity-Secret", unitySecret);

                var task = http.SendAsync(req, cts.Token);
                task.Wait(cts.Token);
                success = task.Result.IsSuccessStatusCode;

                if (!success)
                {
                    string resp = task.Result.Content.ReadAsStringAsync().Result;
                    if (resp.Length > 300) resp = resp.Substring(0, 300) + "…";
                    Debug.LogWarning($"[PanelSync] EBS {task.Result.StatusCode} on {endpoint}: {resp}");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"[PanelSync] POST {endpoint} timed out after 15s — Railway may be slow or batch is too large");
            }
            catch (AggregateException ae) when (ae.InnerException is OperationCanceledException)
            {
                Debug.LogWarning($"[PanelSync] POST {endpoint} timed out (aggregate) — consider reducing maxViewersPerBatch");
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
                    Debug.Log($"[PanelSync] Executing inbound command '{command}' for {username} ({userId}) args: [{string.Join(", ", args)}]");
                    result = rpgCommands.HandleRPGCommand(command, userId, username, args)
                             ?? $"Command '{command}' processed.";
                    Debug.Log($"[PanelSync] Command '{command}' result: {result}");
                    PushViewerImmediate(userId);
                }
                catch (Exception ex)
                {
                    result = $"Error: {ex.Message}";
                    Debug.LogError($"[PanelSync] Command error '{command}': {ex.Message}\n{ex.StackTrace}");
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
