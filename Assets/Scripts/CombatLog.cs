using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Manages combat log display on screen
/// </summary>
public class CombatLog : MonoBehaviour
{
    public static CombatLog Instance;

    [Header("UI References")]
    public TextMeshProUGUI logText;
    public int maxLogEntries = 10;

    private Queue<string> logEntries = new Queue<string>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void AddEntry(string entry)
    {
        logEntries.Enqueue(entry);

        // Keep only recent entries
        while (logEntries.Count > maxLogEntries)
        {
            logEntries.Dequeue();
        }

        UpdateLogDisplay();

        // Also send to Twitch chat (optional - might be spammy)
        // TwitchChatClient.Instance?.SendChatMessage(entry);
    }

    void UpdateLogDisplay()
    {
        if (logText == null) return;

        logText.text = string.Join("\n", logEntries);
    }

    public void Clear()
    {
        logEntries.Clear();
        UpdateLogDisplay();
    }
}
