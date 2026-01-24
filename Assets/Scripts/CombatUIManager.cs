using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages all combat UI elements: health bars, turn timer, wave indicator
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

    [Header("Turn Indicator")]
    public Color playerTurnColor = Color.green;
    public Color enemyTurnColor = Color.red;
    public Image turnIndicatorBackground;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    #region Health Bars

    public void CreateHealthBar(CombatEntity entity)
    {
        if (healthBarPrefab == null)
        {
            Debug.LogWarning("Health bar prefab not assigned!");
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
        if (turnTimerText == null) return;

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
        if (turnIndicatorPanel == null) return;

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
        if (waveIndicatorText == null) return;

        waveIndicatorText.text = $"Wave {currentWave}/{totalWaves}";
    }

    #endregion
}
