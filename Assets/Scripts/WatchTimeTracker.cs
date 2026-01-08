using System.Collections.Generic;
using UnityEngine;

public class WatchTimeTracker : MonoBehaviour
{
    private static WatchTimeTracker _instance;
    public static WatchTimeTracker Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<WatchTimeTracker>();
            }
            return _instance;
        }
    }

    [Header("Watch Time Settings")]
    [SerializeField] private float xpIntervalMinutes = 30f; // 30 minutes
    [SerializeField] private int xpPerInterval = 5;

    private Dictionary<string, float> watchTimers = new Dictionary<string, float>();

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
        // Update all active character watch timers
        List<OnScreenCharacter> characters = CharacterSpawner.Instance?.GetAllCharacters();

        if (characters == null) return;

        foreach (OnScreenCharacter character in characters)
        {
            string userId = character.GetUserId();

            // Initialize timer if needed
            if (!watchTimers.ContainsKey(userId))
            {
                watchTimers[userId] = 0f;
            }

            // Increment watch time
            watchTimers[userId] += Time.deltaTime;

            // Check if interval reached
            float intervalSeconds = xpIntervalMinutes * 60f;
            if (watchTimers[userId] >= intervalSeconds)
            {
                // Award XP
                ExperienceManager.Instance?.AddExperience(userId, xpPerInterval);

                // Update viewer's total watch time
                ViewerData viewer = RPGManager.Instance.GetViewer(userId);
                if (viewer != null)
                {
                    viewer.totalWatchTimeMinutes += xpIntervalMinutes;
                }

                // Reset timer
                watchTimers[userId] -= intervalSeconds;

                Debug.Log($"[WatchTime] {character.GetUsername()} earned {xpPerInterval} XP for watching {xpIntervalMinutes} minutes");
            }
        }

        // Clean up timers for despawned characters
        CleanupInactiveTimers();
    }

    private void CleanupInactiveTimers()
    {
        List<OnScreenCharacter> activeCharacters = CharacterSpawner.Instance?.GetAllCharacters();
        if (activeCharacters == null) return;

        HashSet<string> activeIds = new HashSet<string>();
        foreach (var character in activeCharacters)
        {
            activeIds.Add(character.GetUserId());
        }

        // Remove timers for despawned characters
        List<string> toRemove = new List<string>();
        foreach (var userId in watchTimers.Keys)
        {
            if (!activeIds.Contains(userId))
            {
                toRemove.Add(userId);
            }
        }

        foreach (var userId in toRemove)
        {
            watchTimers.Remove(userId);
        }
    }

    public float GetWatchTime(string userId)
    {
        return watchTimers.ContainsKey(userId) ? watchTimers[userId] : 0f;
    }

    public void ResetWatchTime(string userId)
    {
        if (watchTimers.ContainsKey(userId))
        {
            watchTimers[userId] = 0f;
        }
    }
}