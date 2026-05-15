using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Attach this to a prefab that has a TextMeshPro (NOT TextMeshProUGUI) component.
/// The prefab needs NO Canvas — it lives entirely in world space.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class WorldDamageNumber : MonoBehaviour
{
    [Header("Float Settings")]
    public float lifetime = 1.2f;
    public float floatSpeed = 2.5f;   // units/sec upward
    public float spreadAngle = 35f;    // max degrees left/right from straight up
    public float riseHeight = 1.5f;   // total world-units to rise over lifetime

    [Header("Scale Punch")]
    public float punchScale = 1.4f;   // briefly scales up on spawn
    public float punchTime = 0.1f;

    private TextMeshPro _tmp;
    private Vector3 _dir;
    private float _elapsed;
    private Vector3 _baseScale;

    void Awake()
    {
        _tmp = GetComponent<TextMeshPro>();

        // Always face the camera
        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation;
    }

    /// <summary>
    /// Called immediately after Instantiate to configure the number.
    /// </summary>
    public void Init(string text, Color color, float sizeMultiplier = 1f)
    {
        _tmp.text = text;
        _tmp.color = color;

        // Random upward direction with slight horizontal spread
        float angle = Random.Range(-spreadAngle, spreadAngle);
        _dir = Quaternion.Euler(0, 0, angle) * Vector3.up;

        _baseScale = transform.localScale * sizeMultiplier;
        transform.localScale = _baseScale;

        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        // ── Scale punch ───────────────────────────────────────────────────────
        float punchElapsed = 0f;
        while (punchElapsed < punchTime)
        {
            punchElapsed += Time.deltaTime;
            float t = punchElapsed / punchTime;
            transform.localScale = Vector3.Lerp(_baseScale * punchScale, _baseScale, t);
            yield return null;
        }
        transform.localScale = _baseScale;

        // ── Float & fade ──────────────────────────────────────────────────────
        Color startColor = _tmp.color;
        Vector3 startPos = transform.position;

        while (_elapsed < lifetime)
        {
            _elapsed += Time.deltaTime;
            float t = _elapsed / lifetime;

            // Move in chosen direction
            transform.position = startPos + _dir * (riseHeight * t);

            // Ease-out so it slows near top
            float speedT = 1f - (t * t);
            transform.position += _dir * (floatSpeed * speedT * Time.deltaTime);

            // Fade out in the second half
            float alpha = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);
            _tmp.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

            // Keep facing camera each frame (in case of camera movement)
            if (Camera.main != null)
                transform.rotation = Camera.main.transform.rotation;

            yield return null;
        }

        Destroy(gameObject);
    }
}
