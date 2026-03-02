using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays class-specific resources: Mana, Sneak, Wrath, Stance, Balance
/// Follows a combat entity like health bars
/// </summary>
public class ClassResourceBar : MonoBehaviour
{
    [Header("References")]
    public Image fillImage;
    public TextMeshProUGUI resourceText;
    public Canvas canvas;

    [Header("Sneak Pips (Rogue)")]
    public GameObject[] sneakPips; // Array of 6 pip images
    public Color activePipColor = Color.yellow;
    public Color inactivePipColor = Color.gray;

    [Header("Balance Bar (Ranger)")]
    public Image balanceFillImage;
    public Image balanceCenterMarker; // Visual indicator at 0
    public Color positiveBalanceColor = Color.green;
    public Color negativeBalanceColor = Color.red;
    public Color neutralBalanceColor = Color.white;

    [Header("Stance Display (Fighter)")]
    public TextMeshProUGUI stanceText;
    public Color noneStanceColor = Color.white;
    public Color aggressiveColor = Color.red;
    public Color defensiveColor = Color.blue;
    public Color reflectiveColor = Color.cyan;

    [Header("Mana/Wrath Colors")]
    public Color manaColor = Color.cyan;
    public Color wrathColor = new Color(1f, 0.5f, 0f); // Orange

    private CombatEntity trackedEntity;
    private RectTransform rectTransform;
    private CharacterClass currentClass;

    [Header("Positioning")]
    public Vector3 offset = new Vector3(0, 1.0f, 0); // Below health bar

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (trackedEntity != null && !trackedEntity.isDead)
        {
            // Follow entity position
            Vector3 worldPos = trackedEntity.transform.position + offset;
            transform.position = Camera.main.WorldToScreenPoint(worldPos);
        }
    }

    public void Initialize(CombatEntity entity)
    {
        trackedEntity = entity;
        currentClass = entity.characterClass;

        // Hide all displays initially
        HideAllDisplays();

        // Show appropriate display for class
        switch (currentClass)
        {
            case CharacterClass.Mage:
                SetupManaDisplay();
                UpdateMana(entity.mana, 100);
                break;

            case CharacterClass.Rogue:
                SetupSneakDisplay();
                UpdateSneak(entity.sneakPoints, 6);
                break;

            case CharacterClass.Cleric:
                SetupWrathDisplay();
                UpdateWrath(entity.wrath, 100);
                break;

            case CharacterClass.Fighter:
                SetupStanceDisplay();
                UpdateStance(entity.currentStance);
                break;

            case CharacterClass.Ranger:
                SetupBalanceDisplay();
                UpdateBalance(entity.balance, -10, 10);
                break;
        }
    }

    void HideAllDisplays()
    {
        if (fillImage != null) fillImage.gameObject.SetActive(false);
        if (resourceText != null) resourceText.gameObject.SetActive(false);
        if (balanceFillImage != null) balanceFillImage.gameObject.SetActive(false);
        if (balanceCenterMarker != null) balanceCenterMarker.gameObject.SetActive(false);
        if (stanceText != null) stanceText.gameObject.SetActive(false);

        if (sneakPips != null)
        {
            foreach (var pip in sneakPips)
            {
                if (pip != null) pip.SetActive(false);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // MAGE - MANA BAR
    // ═══════════════════════════════════════════════════════════════
    void SetupManaDisplay()
    {
        if (fillImage != null)
        {
            fillImage.gameObject.SetActive(true);
            fillImage.color = manaColor;
        }
        if (resourceText != null) resourceText.gameObject.SetActive(true);
    }

    public void UpdateMana(int current, int max)
    {
        if (fillImage == null) return;

        float fillAmount = (float)current / max;
        fillImage.fillAmount = fillAmount;

        if (resourceText != null)
            resourceText.text = $"Mana: {current}/{max}";
    }

    // ═══════════════════════════════════════════════════════════════
    // ROGUE - SNEAK PIPS
    // ═══════════════════════════════════════════════════════════════
    void SetupSneakDisplay()
    {
        if (sneakPips == null || sneakPips.Length == 0)
        {
            Debug.LogWarning("[ClassResourceBar] No sneak pips assigned for Rogue!");
            return;
        }

        // Show all 6 pips
        foreach (var pip in sneakPips)
        {
            if (pip != null) pip.SetActive(true);
        }
    }

    public void UpdateSneak(int current, int max)
    {
        if (sneakPips == null) return;

        for (int i = 0; i < sneakPips.Length; i++)
        {
            if (sneakPips[i] == null) continue;

            Image pipImage = sneakPips[i].GetComponent<Image>();
            if (pipImage != null)
            {
                // Active if within current sneak points
                pipImage.color = (i < current) ? activePipColor : inactivePipColor;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // CLERIC - WRATH BAR
    // ═══════════════════════════════════════════════════════════════
    void SetupWrathDisplay()
    {
        if (fillImage != null)
        {
            fillImage.gameObject.SetActive(true);
            fillImage.color = wrathColor;
        }
        if (resourceText != null) resourceText.gameObject.SetActive(true);
    }

    public void UpdateWrath(int current, int max)
    {
        if (fillImage == null) return;

        float fillAmount = (float)current / max;
        fillImage.fillAmount = fillAmount;

        if (resourceText != null)
            resourceText.text = $"Wrath: {current}/{max}";
    }

    // ═══════════════════════════════════════════════════════════════
    // FIGHTER - STANCE INDICATOR
    // ═══════════════════════════════════════════════════════════════
    void SetupStanceDisplay()
    {
        if (stanceText != null)
        {
            stanceText.gameObject.SetActive(true);
        }
    }

    public void UpdateStance(FighterStance stance)
    {
        if (stanceText == null) return;

        switch (stance)
        {
            case FighterStance.None:
                stanceText.text = "No Stance";
                stanceText.color = noneStanceColor;
                break;

            case FighterStance.Aggressive:
                stanceText.text = "⚔️ Aggressive";
                stanceText.color = aggressiveColor;
                break;

            case FighterStance.Defensive:
                stanceText.text = "🛡️ Defensive";
                stanceText.color = defensiveColor;
                break;

            case FighterStance.Reflective:
                stanceText.text = "✨ Reflective";
                stanceText.color = reflectiveColor;
                break;

            case FighterStance.Balanced:
                stanceText.text = "⚖️ Balanced";
                stanceText.color = Color.yellow;
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // RANGER - BALANCE BAR
    // ═══════════════════════════════════════════════════════════════
    void SetupBalanceDisplay()
    {
        if (balanceFillImage != null) balanceFillImage.gameObject.SetActive(true);
        if (balanceCenterMarker != null) balanceCenterMarker.gameObject.SetActive(true);
        if (resourceText != null) resourceText.gameObject.SetActive(true);
    }

    public void UpdateBalance(int current, int min, int max)
    {
        if (balanceFillImage == null) return;

        // Normalize balance from -10 to +10 into 0 to 1
        float normalized = (float)(current - min) / (max - min);
        balanceFillImage.fillAmount = normalized;

        // Color based on balance
        if (current > 3)
            balanceFillImage.color = positiveBalanceColor;
        else if (current < -3)
            balanceFillImage.color = negativeBalanceColor;
        else
            balanceFillImage.color = neutralBalanceColor;

        if (resourceText != null)
            resourceText.text = $"Balance: {current}";
    }
}
