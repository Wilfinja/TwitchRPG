using System;
using System.IO;
using UnityEngine;

public class RPGSaveSystem
{
    private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "RPGData");
    private static string GameDataPath => Path.Combine(SaveDirectory, "game_data.json");
    private static string BackupPath => Path.Combine(SaveDirectory, "game_data_backup.json");

    public static void SaveGameData(GameDatabase database)
    {
        try
        {
            // Create directory if it doesn't exist
            if (!Directory.Exists(SaveDirectory))
            {
                Directory.CreateDirectory(SaveDirectory);
            }

            // Create backup of existing save
            if (File.Exists(GameDataPath))
            {
                File.Copy(GameDataPath, BackupPath, true);
            }

            database.lastSaveTime = DateTime.Now;

            string json = JsonUtility.ToJson(database, true);
            File.WriteAllText(GameDataPath, json);

            Debug.Log($"[RPG] Game data saved successfully to: {GameDataPath}");
            Debug.Log($"[RPG] Total viewers: {database.allViewers.Count}, Total items in database: {database.itemDatabase.Count}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RPG] Failed to save game data: {ex.Message}");
        }
    }

    public static GameDatabase LoadGameData()
    {
        try
        {
            if (File.Exists(GameDataPath))
            {
                string json = File.ReadAllText(GameDataPath);
                GameDatabase database = JsonUtility.FromJson<GameDatabase>(json);

                if (database != null)
                {
                    Debug.Log($"[RPG] Game data loaded successfully!");
                    Debug.Log($"[RPG] Loaded {database.allViewers.Count} viewers and {database.itemDatabase.Count} items");
                    return database;
                }
            }
            else
            {
                Debug.Log("[RPG] No save file found, creating new database");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RPG] Failed to load game data: {ex.Message}");
            Debug.Log("[RPG] Attempting to load backup...");

            // Try to load backup
            try
            {
                if (File.Exists(BackupPath))
                {
                    string json = File.ReadAllText(BackupPath);
                    GameDatabase database = JsonUtility.FromJson<GameDatabase>(json);
                    Debug.Log("[RPG] Backup loaded successfully!");
                    return database;
                }
            }
            catch (Exception backupEx)
            {
                Debug.LogError($"[RPG] Failed to load backup: {backupEx.Message}");
            }
        }

        // Return new database if all else fails
        return new GameDatabase();
    }

    public static void ExportViewerData(string userId)
    {
        try
        {
            GameDatabase database = LoadGameData();
            ViewerData viewer = database.allViewers.Find(v => v.twitchUserId == userId);

            if (viewer != null)
            {
                string exportPath = Path.Combine(SaveDirectory, $"viewer_{viewer.username}_{userId}.json");
                string json = JsonUtility.ToJson(viewer, true);
                File.WriteAllText(exportPath, json);
                Debug.Log($"[RPG] Exported viewer data to: {exportPath}");
            }
            else
            {
                Debug.LogWarning($"[RPG] Viewer {userId} not found for export");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RPG] Failed to export viewer data: {ex.Message}");
        }
    }

    public static string GetSavePath()
    {
        return GameDataPath;
    }

    public static bool SaveExists()
    {
        return File.Exists(GameDataPath);
    }

    public static void DeleteSave()
    {
        try
        {
            if (File.Exists(GameDataPath))
            {
                File.Delete(GameDataPath);
                Debug.Log("[RPG] Save file deleted");
            }
            if (File.Exists(BackupPath))
            {
                File.Delete(BackupPath);
                Debug.Log("[RPG] Backup file deleted");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RPG] Failed to delete save: {ex.Message}");
        }
    }
}
