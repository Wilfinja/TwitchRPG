using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Visual display for mage elemental charges
/// Shows 4 colored pips below the health bar
/// </summary>
public class MageChargeUI : MonoBehaviour
{
    [Header("References")]
    private MageChargeSystem chargeSystem;
    private CombatEntity combatEntity;

    [Header("UI Elements")]
    private GameObject chargeContainer;
    private List<Image> chargePips = new List<Image>();

    [Header("Positioning")]
    public Vector3 offset = new Vector3(0, -2f, 0); // Below health bar
    public float pipSpacing = 0.4f;
    public float pipSize = 0.3f;

    [Header("Colors by Element")]
    private Dictionary<ElementType, Color> elementColors = new Dictionary<ElementType, Color>();

    void Awake()
    {
        combatEntity = GetComponent<CombatEntity>();
        InitializeElementColors();
    }

    public void Initialize(MageChargeSystem system)
    {
        chargeSystem = system;
        CreateChargeUI();
    }

    /// <summary>
    /// Initialize the color mapping for each element type
    /// </summary>
    void InitializeElementColors()
    {
        elementColors[ElementType.Fire] = new Color(1f, 0.3f, 0f);        // Orange-red
        elementColors[ElementType.Frost] = new Color(0.3f, 0.7f, 1f);     // Light blue
        elementColors[ElementType.Arcane] = new Color(0.7f, 0.3f, 1f);    // Purple
        elementColors[ElementType.Lightning] = new Color(1f, 1f, 0.3f);   // Yellow
        elementColors[ElementType.Acid] = new Color(0.5f, 1f, 0.3f);      // Lime green
        elementColors[ElementType.Shadow] = new Color(0.3f, 0.3f, 0.5f);  // Dark purple
        elementColors[ElementType.Holy] = new Color(1f, 1f, 0.9f);        // Bright white-gold
        elementColors[ElementType.None] = new Color(0.3f, 0.3f, 0.3f);    // Gray (empty)
    }

    /// <summary>
    /// Create the charge pip UI in world space
    /// </summary>
    void CreateChargeUI()
    {
        // Create container GameObject
        chargeContainer = new GameObject($"{combatEntity.entityName}_ChargeUI");
        chargeContainer.transform.SetParent(transform);

        // Create 4 charge pips
        float totalWidth = (MageChargeSystem.MAX_CHARGES - 1) * pipSpacing;
        float startX = -totalWidth / 2f;

        for (int i = 0; i < MageChargeSystem.MAX_CHARGES; i++)
        {
            GameObject pipObj = CreateChargePip(i, startX + (i * pipSpacing));
            chargePips.Add(pipObj.GetComponent<Image>());
        }

        UpdateDisplay();
    }

    /// <summary>
    /// Create a single charge pip sprite
    /// </summary>
    GameObject CreateChargePip(int index, float xOffset)
    {
        // Create pip GameObject
        GameObject pipObj = new GameObject($"ChargePip_{index}");
        pipObj.transform.SetParent(chargeContainer.transform);
        pipObj.transform.localPosition = new Vector3(xOffset, 0, 0);
        pipObj.transform.localScale = Vector3.one * pipSize;

        // Add SpriteRenderer to draw the pip
        SpriteRenderer spriteRenderer = pipObj.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = CreateCircleSprite();
        spriteRenderer.color = elementColors[ElementType.None]; // Start empty
        spriteRenderer.sortingOrder = 10; // Above characters

        // Alternative: Use UI Image if you want canvas-based UI
        // This would require creating a Canvas component
        // For now, using SpriteRenderer for simplicity

        return pipObj;
    }

    /// <summary>
    /// Create a simple circle sprite for the charge pip
    /// </summary>
    Sprite CreateCircleSprite()
    {
        // Create a simple circle texture
        int resolution = 32;
        Texture2D texture = new Texture2D(resolution, resolution);
        Color[] pixels = new Color[resolution * resolution];

        float center = resolution / 2f;
        float radius = resolution / 2f - 1;

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));

                if (distance <= radius)
                {
                    // Inside circle - white
                    pixels[y * resolution + x] = Color.white;
                }
                else
                {
                    // Outside circle - transparent
                    pixels[y * resolution + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
    }

    /// <summary>
    /// Update the visual display based on current charges
    /// </summary>
    public void UpdateDisplay()
    {
        if (chargeSystem == null || chargePips.Count == 0) return;

        for (int i = 0; i < MageChargeSystem.MAX_CHARGES; i++)
        {
            ElementalCharge charge = chargeSystem.GetCharge(i);

            if (charge != null)
            {
                // Filled pip - show element color
                ElementType element = charge.element;
                if (elementColors.ContainsKey(element))
                {
                    // Get the SpriteRenderer from the pip GameObject
                    SpriteRenderer spriteRenderer = chargePips[i].transform.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.color = elementColors[element];
                    }
                }
            }
            else
            {
                // Empty pip - show gray
                SpriteRenderer spriteRenderer = chargePips[i].transform.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = elementColors[ElementType.None];
                }
            }
        }
    }

    /// <summary>
    /// Update position to follow the mage character
    /// </summary>
    void LateUpdate()
    {
        if (chargeContainer != null && combatEntity != null)
        {
            // Position the charge UI relative to the mage
            Vector3 worldPosition = combatEntity.transform.position + offset;
            chargeContainer.transform.position = worldPosition;
        }
    }

    void OnDestroy()
    {
        // Clean up UI when component is destroyed
        if (chargeContainer != null)
        {
            Destroy(chargeContainer);
        }
    }
}
