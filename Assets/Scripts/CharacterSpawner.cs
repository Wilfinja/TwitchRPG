using System.Collections.Generic;
using UnityEngine;

public class CharacterSpawner : MonoBehaviour
{
    [Header("Class Prefabs")]
    [SerializeField] private GameObject roguePrefab;
    [SerializeField] private GameObject fighterPrefab;
    [SerializeField] private GameObject magePrefab;
    [SerializeField] private GameObject clericPrefab;
    [SerializeField] private GameObject rangerPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnYPosition = -3f;
    [SerializeField] private float spawnSpacing = 1.5f;
    [SerializeField] private float idleRangeX = 2f;
    [SerializeField] private Transform characterParent;

    [Header("Screen Bounds")]
    [SerializeField] private float screenLeftBound = -9.5f;
    [SerializeField] private float screenRightBound = 9.5f;

    private Dictionary<string, OnScreenCharacter> activeCharacters = new Dictionary<string, OnScreenCharacter>();
    private List<float> occupiedXPositions = new List<float>();

    private static CharacterSpawner _instance;
    public static CharacterSpawner Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CharacterSpawner>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    public bool SpawnCharacter(string userId, string username)
    {
        // Check if already spawned
        if (activeCharacters.ContainsKey(userId))
        {
            Debug.LogWarning($"[CharacterSpawner] {username} is already spawned!");
            return false;
        }

        // Get viewer data
        ViewerData viewer = RPGManager.Instance.GetViewer(userId);
        if (viewer == null || viewer.characterClass == CharacterClass.None)
        {
            Debug.LogWarning($"[CharacterSpawner] {username} has no class set!");
            return false;
        }

        // Find next available spawn position
        float spawnX = GetNextSpawnPosition();
        Vector3 spawnPos = new Vector3(spawnX, spawnYPosition, 0f);

        // Get the correct prefab for this class
        GameObject prefabToSpawn = GetPrefabForClass(viewer.characterClass);

        if (prefabToSpawn == null)
        {
            Debug.LogError($"[CharacterSpawner] No prefab assigned for class {viewer.characterClass}!");
            return false;
        }

        // Instantiate the class-specific prefab
        GameObject charGO = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity, characterParent);
        OnScreenCharacter character = charGO.GetComponent<OnScreenCharacter>();

        if (character == null)
        {
            Debug.LogError($"[CharacterSpawner] Prefab for {viewer.characterClass} is missing OnScreenCharacter script!");
            Destroy(charGO);
            return false;
        }

        // Initialize character
        character.Initialize(userId, username, viewer.characterClass, viewer.baseStats.level, spawnX, idleRangeX);

        // Track character
        activeCharacters.Add(userId, character);
        occupiedXPositions.Add(spawnX);

        Debug.Log($"[CharacterSpawner] Spawned {username} ({viewer.characterClass}) at position {spawnPos}");
        return true;
    }

    private GameObject GetPrefabForClass(CharacterClass charClass)
    {
        switch (charClass)
        {
            case CharacterClass.Rogue:
                return roguePrefab;
            case CharacterClass.Fighter:
                return fighterPrefab;
            case CharacterClass.Mage:
                return magePrefab;
            case CharacterClass.Cleric:
                return clericPrefab;
            case CharacterClass.Ranger:
                return rangerPrefab;
            default:
                return null;
        }
    }

    public void DespawnCharacter(string userId)
    {
        if (activeCharacters.TryGetValue(userId, out OnScreenCharacter character))
        {
            // Remove occupied position
            occupiedXPositions.Remove(character.GetHomePosition());

            // Destroy character
            Destroy(character.gameObject);
            activeCharacters.Remove(userId);

            Debug.Log($"[CharacterSpawner] Despawned character {userId}");
        }
    }

    private float GetNextSpawnPosition()
    {
        // Start from left and find first available slot
        float currentX = screenLeftBound + spawnSpacing;

        while (currentX < screenRightBound)
        {
            // Check if position is occupied
            bool occupied = false;
            foreach (float pos in occupiedXPositions)
            {
                if (Mathf.Abs(pos - currentX) < spawnSpacing * 0.5f)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied)
            {
                return currentX;
            }

            currentX += spawnSpacing;
        }

        // If screen is full, wrap around and place on top of existing
        Debug.LogWarning("[CharacterSpawner] Screen full, overlapping characters");
        return Random.Range(screenLeftBound, screenRightBound);
    }

    public OnScreenCharacter GetCharacter(string userId)
    {
        if (activeCharacters.TryGetValue(userId, out OnScreenCharacter character))
        {
            return character;
        }
        return null;
    }

    public List<OnScreenCharacter> GetAllCharacters()
    {
        return new List<OnScreenCharacter>(activeCharacters.Values);
    }

    public int GetActiveCharacterCount()
    {
        return activeCharacters.Count;
    }

    public void RefreshCharacter(string userId)
    {
        if (activeCharacters.TryGetValue(userId, out OnScreenCharacter character))
        {
            ViewerData viewer = RPGManager.Instance.GetViewer(userId);
            if (viewer != null)
            {
                character.RefreshStats(viewer.baseStats.level);
            }
        }
    }
}
