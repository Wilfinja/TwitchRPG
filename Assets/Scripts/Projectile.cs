using UnityEngine;
using System.Collections;

/// <summary>
/// Simple linear projectile that travels from caster to target
/// </summary>
public class Projectile : MonoBehaviour
{
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float duration = 0.3f;
    private float elapsed = 0f;
    private bool isMoving = false;

    private SpriteRenderer spriteRenderer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
    }

    /// <summary>
    /// Initialize and launch the projectile
    /// </summary>
    public void Launch(Vector3 start, Vector3 target, float travelTime = 0.3f)
    {
        startPosition = start;
        targetPosition = target;
        duration = travelTime;
        elapsed = 0f;
        isMoving = true;

        transform.position = startPosition;

        // Face the target
        Vector3 direction = (targetPosition - startPosition).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        Debug.Log($"[Projectile] Launched from {startPosition} to {targetPosition} (duration: {duration}s)");
    }

    void Update()
    {
        if (!isMoving) return;

        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        if (t >= 1f)
        {
            // Arrived
            transform.position = targetPosition;
            isMoving = false;
            Destroy(gameObject);
            return;
        }

        // ✅ LINEAR movement (no easing)
        transform.position = Vector3.Lerp(startPosition, targetPosition, t);
    }
}
