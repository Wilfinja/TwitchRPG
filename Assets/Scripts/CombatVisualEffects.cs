using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Handles visual effects: damage numbers, evade text, hit particles.
/// Damage/heal numbers are world-space (no Canvas required).
/// </summary>
public class CombatVisualEffects : MonoBehaviour
{
    public static CombatVisualEffects Instance;

    [Header("World-Space Number Prefabs")]
    [Tooltip("Prefab with TextMeshPro (NOT UGUI) + WorldDamageNumber script")]
    public GameObject damageNumberPrefab;
    public GameObject healNumberPrefab;
    public GameObject evadeTextPrefab;
    public GameObject blockTextPrefab;

    [Header("Number Offsets")]
    [Tooltip("Spawn numbers this far above the entity's pivot")]
    public float spawnHeightOffset = 1.5f;

    [Header("Particle Offsets")]
    [Tooltip("Offsets hit particle spawn position relative to the entity's pivot. Adjust Y to match your sprite height.")]
    public Vector3 hitParticleOffset = new Vector3(0f, 0.5f, 0f);

    [Header("Particle Effects")]
    public GameObject hitParticle;
    public GameObject criticalHitParticle;
    public GameObject healParticle;
    public GameObject buffParticle;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SpawnNumber(GameObject prefab, Vector3 worldPos, string text, Color color, float sizeMultiplier = 1f)
    {
        if (prefab == null) return;

        Vector3 spawnPos = worldPos + Vector3.up * spawnHeightOffset;
        GameObject obj = Instantiate(prefab, spawnPos, Quaternion.identity);

        WorldDamageNumber num = obj.GetComponent<WorldDamageNumber>();
        num?.Init(text, color, sizeMultiplier);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ShowDamageNumber(Vector3 position, int damage)
    {
        SpawnNumber(damageNumberPrefab, position, $"-{damage}", Color.red);

        if (hitParticle != null)
        {
            GameObject p = Instantiate(hitParticle, position + hitParticleOffset, Quaternion.identity);
            Destroy(p, 2f);
        }
    }

    public void ShowHealNumber(Vector3 position, int healing)
    {
        SpawnNumber(healNumberPrefab, position, $"+{healing}", Color.green);

        if (healParticle != null)
        {
            GameObject p = Instantiate(healParticle, position, Quaternion.identity);
            Destroy(p, 2f);
        }
    }

    public void ShowBlockedDamage(Vector3 position, int blocked)
    {
        // Slightly smaller than main damage so it doesn't compete visually
        SpawnNumber(blockTextPrefab, position, $"🛡 {blocked}", Color.cyan, 0.75f);
    }

    public void ShowEvadeText(Vector3 position)
    {
        SpawnNumber(evadeTextPrefab, position, "EVADE!", Color.yellow, 1.1f);
    }

    // ── Particle helpers ──────────────────────────────────────────────────────

    public void PlayHitEffect(Vector3 position)
    {
        if (hitParticle == null) return;
        Destroy(Instantiate(hitParticle, position + hitParticleOffset, Quaternion.identity), 2f);
    }

    public void PlayCriticalEffect(Vector3 position)
    {
        if (criticalHitParticle == null) return;
        Destroy(Instantiate(criticalHitParticle, position, Quaternion.identity), 2f);
    }

    public void PlayBuffEffect(Vector3 position)
    {
        if (buffParticle == null) return;
        Destroy(Instantiate(buffParticle, position, Quaternion.identity), 2f);
    }
}
