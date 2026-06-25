using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Individual health bar component that follows a combat entity.
/// Also displays the entity's position number during combat,
/// so viewers know which button to press on the panel.
///
/// CHANGED: Added ConditionIconRow reference and RefreshConditions() wiring.
/// </summary>
public class CombatHealthBar : MonoBehaviour
{
    [Header("References")]
    public Image fillImage;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI nameText;
    public Canvas canvas;

    [Header("Position Display")]
    [Tooltip("TextMeshPro element that shows the entity's combat position number (1-6 for enemies, 1-4 for allies). Assign in the prefab.")]
    public TextMeshProUGUI positionText;

    // ADDED — assign ConditionIconRow child component in prefab.
    [Header("Condition Icons")]
    [Tooltip("ConditionIconRow component that renders active buff/debuff icons. Assign the child component here.")]
    public ConditionIconRow conditionIconRow;

    [Header("Colors")]
    public Color fullHealthColor = Color.green;
    public Color midHealthColor = Color.yellow;
    public Color lowHealthColor = Color.red;

    private CombatEntity trackedEntity;
    private RectTransform rectTransform;

    [Header("Positioning")]
    public Vector3 offset = new Vector3(0, 1.5f, 0);

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

        if (nameText != null)
            nameText.text = entity.entityName;

        // Show the position number so viewers know which panel button to press.
        // CombatEntity.position is 1-6 for enemies, 1-4 for allies.
        if (positionText != null)
        {
            positionText.text = entity.position.ToString();
            positionText.gameObject.SetActive(true);
        }

        UpdateHealth(entity.currentHealth, entity.maxHealth);

        // ADDED — seed the icon row with whatever effects are already active
        // (covers edge cases like entities initialized mid-combat).
        RefreshConditions(entity.activeEffects);
    }

    // ── Condition icons ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by CombatEntity whenever activeEffects changes.
    /// Safe to call with a null list — treated as empty.
    /// </summary>
    public void RefreshConditions(List<StatusEffect> activeEffects)
    {
        if (conditionIconRow == null) return;
        conditionIconRow.RefreshConditions(activeEffects ?? new List<StatusEffect>());
    }

    // ── Position text helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Call this when combat ends to hide the position number.
    /// Avoids confusing viewers outside of combat.
    /// </summary>
    public void HidePositionText()
    {
        if (positionText != null)
            positionText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Call this when combat starts (or if position changes) to refresh the number.
    /// </summary>
    public void ShowPositionText()
    {
        if (positionText != null && trackedEntity != null)
        {
            positionText.text = trackedEntity.position.ToString();
            positionText.gameObject.SetActive(true);
        }
    }

    // ── Health bar ────────────────────────────────────────────────────────────

    public void UpdateHealth(int current, int max)
    {
        if (fillImage == null) return;

        float fillAmount = (float)current / max;
        fillImage.fillAmount = fillAmount;

        // Update color based on health percentage
        if (fillAmount > 0.6f)
            fillImage.color = fullHealthColor;
        else if (fillAmount > 0.3f)
            fillImage.color = midHealthColor;
        else
            fillImage.color = lowHealthColor;

        // Update text
        if (healthText != null)
            healthText.text = $"{current}/{max}";
    }
}
