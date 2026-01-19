using System.Collections;
using UnityEngine;
using TMPro;

public class ItemDropVisual : MonoBehaviour
{
    [Header("Visual Settings")]
    private SpriteRenderer spriteRenderer;
    private ItemRarity itemRarity;
    private string itemName;

    [Header("Animation Settings")]
    [SerializeField] private float fallDuration = 0.75f;
    [SerializeField] private AnimationCurve fallCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Flash Effect")]
    [SerializeField] private float flashDuration = 0.3f;
    [SerializeField] private int flashCount = 3;

    [Header("Floating Text")]
    [SerializeField] private GameObject floatingTextPrefab;
    [SerializeField] private float textFloatHeight = 2f;
    [SerializeField] private float textDuration = 1.5f;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private bool isAnimating = false;

    public void Initialize(Vector3 startPos, Vector3 targetPos, ItemRarity rarity, string name)
    {
        startPosition = startPos;
        targetPosition = targetPos;
        itemRarity = rarity;
        itemName = name;

        transform.position = startPosition;

        // Get or add sprite renderer
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        // Set initial color based on rarity
        spriteRenderer.color = GetRarityColor(rarity);

        StartCoroutine(AnimateDrop());
    }

    private IEnumerator AnimateDrop()
    {
        isAnimating = true;
        float elapsed = 0f;

        // Animate fall
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fallDuration;
            float curveValue = fallCurve.Evaluate(progress);

            transform.position = Vector3.Lerp(startPosition, targetPosition, curveValue);

            yield return null;
        }

        transform.position = targetPosition;

        // Land effects
        yield return StartCoroutine(PlayLandingEffects());

        isAnimating = false;

        // Cleanup
        Destroy(gameObject);
    }

    private IEnumerator PlayLandingEffects()
    {
        // 1. Flash effect
        yield return StartCoroutine(FlashEffect());

        // 2. Particle burst
        TriggerParticleBurst();

        // 3. Floating text
        ShowFloatingText();

        // Brief delay before cleanup
        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator FlashEffect()
    {
        Color originalColor = spriteRenderer.color;
        Color flashColor = Color.white;

        float flashInterval = flashDuration / (flashCount * 2);

        for (int i = 0; i < flashCount; i++)
        {
            // Flash to white
            spriteRenderer.color = flashColor;
            yield return new WaitForSeconds(flashInterval);

            // Flash back to original
            spriteRenderer.color = originalColor;
            yield return new WaitForSeconds(flashInterval);
        }
    }

    private void TriggerParticleBurst()
    {
        if (ParticleEffectManager.Instance == null) return;

        // Create a simple colored particle burst
        Color particleColor = GetRarityColor(itemRarity);
        int particleCount = GetParticleCountByRarity(itemRarity);

        ParticleSystem burstEffect = ParticleEffectManager.Instance.CreateSimpleParticleEffect(
            particleColor,
            particleCount
        );

        if (burstEffect != null)
        {
            burstEffect.transform.position = targetPosition;
            burstEffect.Play();
            Destroy(burstEffect.gameObject, 2f);
        }
    }

    private void ShowFloatingText()
    {
        if (floatingTextPrefab == null)
        {
            // Fallback: Create simple floating text programmatically
            CreateSimpleFloatingText();
            return;
        }

        GameObject textObj = Instantiate(floatingTextPrefab, targetPosition, Quaternion.identity);

        TextMeshPro tmp = textObj.GetComponent<TextMeshPro>();
        if (tmp != null)
        {
            tmp.text = itemName;
            tmp.color = GetRarityColor(itemRarity);
            tmp.fontSize = 3;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        StartCoroutine(AnimateFloatingText(textObj));
    }

    private void CreateSimpleFloatingText()
    {
        GameObject textObj = new GameObject("FloatingItemText");
        textObj.transform.position = targetPosition;

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = itemName;
        tmp.color = GetRarityColor(itemRarity);
        tmp.fontSize = 3;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.sortingOrder = 100;

        // Add outline
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = Color.black;

        StartCoroutine(AnimateFloatingText(textObj));
    }

    private IEnumerator AnimateFloatingText(GameObject textObj)
    {
        Vector3 startPos = textObj.transform.position;
        Vector3 endPos = startPos + Vector3.up * textFloatHeight;

        TextMeshPro tmp = textObj.GetComponent<TextMeshPro>();
        float elapsed = 0f;

        while (elapsed < textDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / textDuration;

            // Move upward
            textObj.transform.position = Vector3.Lerp(startPos, endPos, progress);

            // Fade out
            if (tmp != null)
            {
                Color color = tmp.color;
                color.a = Mathf.Lerp(1f, 0f, progress);
                tmp.color = color;
            }

            yield return null;
        }

        Destroy(textObj);
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return new Color(0.7f, 0.7f, 0.7f); // Gray
            case ItemRarity.Uncommon: return new Color(0.2f, 0.8f, 0.2f); // Green
            case ItemRarity.Rare: return new Color(0.2f, 0.4f, 1f); // Blue
            case ItemRarity.Epic: return new Color(0.64f, 0.21f, 0.93f); // Purple
            case ItemRarity.Legendary: return new Color(1f, 0.5f, 0f); // Orange
            case ItemRarity.Unique: return new Color(0f, 1f, 1f); // Cyan
            default: return Color.white;
        }
    }

    private int GetParticleCountByRarity(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return 20;
            case ItemRarity.Uncommon: return 30;
            case ItemRarity.Rare: return 50;
            case ItemRarity.Epic: return 75;
            case ItemRarity.Legendary: return 100;
            case ItemRarity.Unique: return 150;
            default: return 30;
        }
    }
}
