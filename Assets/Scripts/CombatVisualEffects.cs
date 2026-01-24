using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Handles visual effects: damage numbers, evade text, hit particles
/// </summary>
public class CombatVisualEffects : MonoBehaviour
{
    public static CombatVisualEffects Instance;

    [Header("Prefabs")]
    public GameObject damageNumberPrefab;
    public GameObject healNumberPrefab;
    public GameObject evadeTextPrefab;
    public GameObject blockTextPrefab;

    [Header("Particle Effects")]
    public GameObject hitParticle;
    public GameObject criticalHitParticle;
    public GameObject healParticle;
    public GameObject buffParticle;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    #region Damage Numbers

    public void ShowDamageNumber(Vector3 position, int damage)
    {
        if (damageNumberPrefab == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(position);
        GameObject numberObj = Instantiate(damageNumberPrefab, transform);
        numberObj.transform.position = screenPos;

        TextMeshProUGUI text = numberObj.GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = $"-{damage}";
            text.color = Color.red;
        }

        StartCoroutine(AnimateAndDestroy(numberObj, 1f));

        // Spawn hit particle
        if (hitParticle != null)
        {
            GameObject particle = Instantiate(hitParticle, position, Quaternion.identity);
            Destroy(particle, 2f);
        }
    }

    public void ShowHealNumber(Vector3 position, int healing)
    {
        if (healNumberPrefab == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(position);
        GameObject numberObj = Instantiate(healNumberPrefab, transform);
        numberObj.transform.position = screenPos;

        TextMeshProUGUI text = numberObj.GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = $"+{healing}";
            text.color = Color.green;
        }

        StartCoroutine(AnimateAndDestroy(numberObj, 1f));

        // Spawn heal particle
        if (healParticle != null)
        {
            GameObject particle = Instantiate(healParticle, position, Quaternion.identity);
            Destroy(particle, 2f);
        }
    }

    public void ShowBlockedDamage(Vector3 position, int blocked)
    {
        if (blockTextPrefab == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(position);
        GameObject textObj = Instantiate(blockTextPrefab, transform);
        textObj.transform.position = screenPos + new Vector3(30, 0, 0); // Offset to the side

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = $"🛡 {blocked}";
            text.color = Color.cyan;
        }

        StartCoroutine(AnimateAndDestroy(textObj, 1f));
    }

    public void ShowEvadeText(Vector3 position)
    {
        if (evadeTextPrefab == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(position);
        GameObject textObj = Instantiate(evadeTextPrefab, transform);
        textObj.transform.position = screenPos;

        TextMeshProUGUI text = textObj.GetComponent<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = "EVADE!";
            text.color = Color.yellow;
        }

        StartCoroutine(AnimateAndDestroy(textObj, 1f));
    }

    #endregion

    #region Animations

    IEnumerator AnimateAndDestroy(GameObject obj, float duration)
    {
        RectTransform rect = obj.GetComponent<RectTransform>();
        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();

        Vector3 startPos = rect.position;
        Vector3 endPos = startPos + new Vector3(0, 100, 0); // Float upward

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Move upward
            rect.position = Vector3.Lerp(startPos, endPos, t);

            // Fade out
            if (text != null)
            {
                Color color = text.color;
                color.a = 1f - t;
                text.color = color;
            }

            yield return null;
        }

        Destroy(obj);
    }

    #endregion

    #region Particle Effects

    public void PlayHitEffect(Vector3 position)
    {
        if (hitParticle != null)
        {
            GameObject particle = Instantiate(hitParticle, position, Quaternion.identity);
            Destroy(particle, 2f);
        }
    }

    public void PlayCriticalEffect(Vector3 position)
    {
        if (criticalHitParticle != null)
        {
            GameObject particle = Instantiate(criticalHitParticle, position, Quaternion.identity);
            Destroy(particle, 2f);
        }
    }

    public void PlayBuffEffect(Vector3 position)
    {
        if (buffParticle != null)
        {
            GameObject particle = Instantiate(buffParticle, position, Quaternion.identity);
            Destroy(particle, 2f);
        }
    }

    #endregion
}
