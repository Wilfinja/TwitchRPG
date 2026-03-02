using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages all combat UI elements: health bars, turn timer, wave indicator
/// ✅ NOW WITH: Show/Hide combat UI when combat starts/ends
/// </summary>
public class CombatUIManager : MonoBehaviour
{
    public static CombatUIManager Instance;

    [Header("Prefabs")]
    public GameObject healthBarPrefab;
    public GameObject damageNumberPrefab;

    [Header("UI Elements")]
    public GameObject turnIndicatorPanel;
    public TextMeshProUGUI turnTimerText;
    public TextMeshProUGUI waveIndicatorText;
    public Transform healthBarContainer;

    [Header("Combat UI Containers")]
    [Tooltip("Parent object containing all combat UI (optional - for easy show/hide)")]
    public GameObject combatUIContainer;

    [Tooltip("Show combat log during combat")]
    public GameObject combatLogPanel;

    [Header("Turn Indicator")]
    public Color playerTurnColor = Color.green;
    public Color enemyTurnColor = Color.red;
    public Image turnIndicatorBackground;

    [Header("Class Resource Bars")]
    public GameObject classResourceBarPrefab;
    public Transform classResourceBarContainer;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        // Hide all combat UI on startup
        HideCombatUI();
    }

    #region Combat UI Visibility

    /// <summary>
    /// Show all combat UI elements when combat starts
    /// </summary>
    /// 

    public void ShowCombatUI()
    {
        // Show main combat UI container if assigned
        if (combatUIContainer != null)
        {
            combatUIContainer.SetActive(true);
        }
        else
        {
            // Fallback: show individual elements
            if (turnIndicatorPanel != null)
                turnIndicatorPanel.SetActive(false); // Don't show until turn starts

            if (waveIndicatorText != null)
                waveIndicatorText.gameObject.SetActive(true);
        }

        // Show combat log
        if (combatLogPanel != null)
        {
            combatLogPanel.SetActive(true);
        }
        else if (CombatLog.Instance != null)
        {
            // Fallback: show the log's GameObject
            CombatLog.Instance.gameObject.SetActive(true);
        }

        Debug.Log("[CombatUI] Combat UI shown");
    }

    /// <summary>
    /// Hide all combat UI elements when combat ends
    /// </summary>
    public void HideCombatUI()
    {
        // Hide main combat UI container if assigned
        if (combatUIContainer != null)
        {
            combatUIContainer.SetActive(false);
        }
        else
        {
            // Fallback: hide individual elements
            if (turnIndicatorPanel != null)
                turnIndicatorPanel.SetActive(false);

            if (waveIndicatorText != null)
                waveIndicatorText.gameObject.SetActive(false);
        }

        // Hide combat log
        if (combatLogPanel != null)
        {
            combatLogPanel.SetActive(false);
        }
        else if (CombatLog.Instance != null)
        {
            // Fallback: hide the log's GameObject
            CombatLog.Instance.gameObject.SetActive(false);
        }

        // Clear all health bars
        ClearAllHealthBars();

        Debug.Log("[CombatUI] Combat UI hidden");
    }

    /// <summary>
    /// Clear all spawned health bars from the container
    /// </summary>
    void ClearAllHealthBars()
    {
        if (healthBarContainer != null)
        {
            foreach (Transform child in healthBarContainer)
            {
                Destroy(child.gameObject);
            }
        }

        // ✅ NEW: Also clear resource bars
        ClearAllClassResourceBars();
    }

    #endregion

    #region Health Bars

    public void CreateHealthBar(CombatEntity entity)
    {
        if (healthBarPrefab == null)
        {
            Debug.LogWarning("[CombatUI] Health bar prefab not assigned - skipping health bar creation");
            return;
        }

        if (healthBarContainer == null)
        {
            Debug.LogWarning("[CombatUI] Health bar container not assigned - skipping health bar creation");
            return;
        }

        GameObject healthBarObj = Instantiate(healthBarPrefab, healthBarContainer);
        CombatHealthBar healthBar = healthBarObj.GetComponent<CombatHealthBar>();

        if (healthBar != null)
        {
            healthBar.Initialize(entity);
            entity.healthBarObject = healthBarObj;
        }
    }

    #endregion

    #region Turn Timer

    public void UpdateTurnTimer(float currentTime, float maxTime)
    {
        if (turnTimerText == null)
        {
            // Don't spam warnings - this is optional UI
            return;
        }

        int seconds = Mathf.CeilToInt(currentTime);
        turnTimerText.text = $"Turn Timer: {seconds}s";

        // Change color based on urgency
        if (currentTime < 10f)
            turnTimerText.color = Color.red;
        else if (currentTime < 20f)
            turnTimerText.color = Color.yellow;
        else
            turnTimerText.color = Color.white;
    }

    public void ShowTurnIndicator(bool isPlayerTurn)
    {
        // ✅ NULL-SAFE: Don't crash if panel isn't assigned
        if (turnIndicatorPanel == null)
        {
            // Debug.LogWarning("[CombatUI] Turn indicator panel not assigned - combat will work but no turn indicator shown");
            return;
        }

        turnIndicatorPanel.SetActive(true);

        if (turnIndicatorBackground != null)
        {
            turnIndicatorBackground.color = isPlayerTurn ? playerTurnColor : enemyTurnColor;
        }

        if (turnTimerText != null)
        {
            turnTimerText.text = isPlayerTurn ? "PLAYER TURN" : "ENEMY TURN";
        }
    }

    public void HideTurnIndicator()
    {
        if (turnIndicatorPanel != null)
            turnIndicatorPanel.SetActive(false);
    }

    #endregion

    #region Wave Indicator

    public void UpdateWaveIndicator(int currentWave, int totalWaves)
    {
        if (waveIndicatorText == null)
        {
            // Don't spam warnings - this is optional UI
            return;
        }

        waveIndicatorText.text = $"Wave {currentWave}/{totalWaves}";
    }

    #endregion

    /// <summary>
    /// Create class resource bar for player entities
    /// </summary>
    public void CreateClassResourceBar(CombatEntity entity)
    {
        // Only create for players with classes
        if (!entity.isPlayer) return;
        if (entity.characterClass == CharacterClass.None) return;

        if (classResourceBarPrefab == null)
        {
            Debug.LogWarning("[CombatUI] Class resource bar prefab not assigned");
            return;
        }

        if (classResourceBarContainer == null)
        {
            Debug.LogWarning("[CombatUI] Class resource bar container not assigned");
            return;
        }

        GameObject resourceBarObj = Instantiate(classResourceBarPrefab, classResourceBarContainer);
        ClassResourceBar resourceBar = resourceBarObj.GetComponent<ClassResourceBar>();

        if (resourceBar != null)
        {
            resourceBar.Initialize(entity);

            // Store reference in entity (add new field to CombatEntity)
            entity.classResourceBarObject = resourceBarObj;
        }
    }

    /// <summary>
    /// Clear class resource bars when combat ends
    /// </summary>
    void ClearAllClassResourceBars()
    {
        if (classResourceBarContainer == null) return;

        foreach (Transform child in classResourceBarContainer)
        {
            Destroy(child.gameObject);
        }
    }
}
