using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Individual health bar component that follows a combat entity
/// </summary>
public class CombatHealthBar : MonoBehaviour
{
    [Header("References")]
    public Image fillImage;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI nameText;
    public Canvas canvas;

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

        UpdateHealth(entity.currentHealth, entity.maxHealth);
    }

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
