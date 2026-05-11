using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class PlayerExtensionData
{
    public string name;
    public int hp;
    public int maxHp;
    public string className;
    public bool hasActed;
}

[System.Serializable]
public class ExtensionPayload
{
    public bool isCombat;
    public float turnTimer;
    public bool isJoining;
    public List<PlayerExtensionData> party = new List<PlayerExtensionData>();
}

public class ExtensionSync : MonoBehaviour
{
    private string gistId = "12880e28993d2b53813310138dd724e3";
    private string githubToken = "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";

    private void Start()
    {
        // Sync every 3 seconds to be safe
        InvokeRepeating("SyncToExtension", 6f, 6f);
    }

    private void SyncToExtension()
    {
        if (string.IsNullOrEmpty(gistId) || gistId == "YOUR_GIST_ID") return;
        StartCoroutine(UpdateGist());
    }

    IEnumerator UpdateGist()
    {
        // Create the data payload from our current game state
        ExtensionPayload payload = new ExtensionPayload();
        payload.isCombat = CombatTurnManager.Instance != null && CombatTurnManager.Instance.combatActive;
        payload.turnTimer = CombatTurnManager.Instance != null ? CombatTurnManager.Instance.turnTimer : 0f;
        payload.isJoining = ExpeditionManager.Instance != null && ExpeditionManager.Instance.acceptingJoins;

        // Add party members
        if (ExpeditionManager.Instance != null)
        {
            var players = ExpeditionManager.Instance.GetAllPlayerEntities();
            foreach (var p in players)
            {
                payload.party.Add(new PlayerExtensionData
                {
                    name = p.entityName,
                    hp = p.currentHealth,
                    maxHp = p.maxHealth,
                    className = p.characterClass.ToString(),
                    hasActed = p.hasActedThisTurn
                });
            }
        }

        // 1. Convert to JSON
        string jsonContent = JsonUtility.ToJson(payload);

        // 2. Wrap for GitHub Gist API (Escaping quotes)
        string escapedJson = jsonContent.Replace("\"", "\\\"");
        string wrappedJson = "{\"files\": {\"rpg_extension_data.json\": {\"content\": \"" + escapedJson + "\"}}}";

        // 3. Setup the Request
        // We use .Put and then change the method to PATCH to bypass a Unity bug
        using (UnityWebRequest request = UnityWebRequest.Put($"https://api.github.com/gists/{gistId}", wrappedJson))
        {
            request.method = "PATCH"; // Correct verb for updating a Gist

            // HEADERS - These are critical for GitHub
            request.SetRequestHeader("Authorization", $"Bearer {githubToken}");
            request.SetRequestHeader("Content-Type", "application/json");

            // This is the #1 fix for 403 errors in Unity:
            request.SetRequestHeader("User-Agent", "Unity-Twitch-RPG");

            // Disable chunked transfer (Some APIs, including GitHub, occasionally reject it)
            //request.chunkedTransfer = false;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Extension] Gist Sync Failed: {request.responseCode}");
                Debug.LogError($"[Extension] Error: {request.error}");

                // This line is the most important: it shows GitHub's actual reason
                Debug.LogError($"[Extension] GitHub Response: {request.downloadHandler.text}");

                // If we get a 403, stop the repeating sync so we don't get blocked
                if (request.responseCode == 403)
                {
                    Debug.LogError("[Extension] Forbidden! Check your token scopes or User-Agent. Stopping auto-sync.");
                    CancelInvoke("SyncToExtension");
                }
            }
            else
            {
                Debug.Log("[Extension] Stats synced to GitHub successfully!");
            }
        }
    }
}
