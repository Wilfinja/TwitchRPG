using UnityEngine;

public class ParticleEffectManager : MonoBehaviour
{
    [Header("Particle Systems")]
    [SerializeField] private ParticleSystem explosionEffect;
    [SerializeField] private ParticleSystem burstEffect;
    [SerializeField] private ParticleSystem confettiEffect;

    [Header("Spawn Settings")]
    [SerializeField] private bool spawnAtRandomPosition = true;
    [SerializeField] private float screenWidth = 19.2f;
    [SerializeField] private float screenHeight = 10.8f;

    private static ParticleEffectManager _instance;

    public static ParticleEffectManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<ParticleEffectManager>();
            }
            return _instance;
        }
    }

    public void TriggerExplosion()
    {
        if (explosionEffect != null)
        {
            Vector3 position = spawnAtRandomPosition ? GetRandomScreenPosition() : Vector3.zero;
            PlayEffect(explosionEffect, position);
        }
        else
        {
            Debug.LogWarning("Explosion effect not assigned!");
        }
    }

    public void TriggerBurst()
    {
        if (burstEffect != null)
        {
            Vector3 position = spawnAtRandomPosition ? GetRandomScreenPosition() : Vector3.zero;
            PlayEffect(burstEffect, position);
        }
        else
        {
            Debug.LogWarning("Burst effect not assigned!");
        }
    }

    public void TriggerConfetti()
    {
        if (confettiEffect != null)
        {
            Vector3 position = new Vector3(0, screenHeight / 2, 0); // Top center
            PlayEffect(confettiEffect, position);
        }
        else
        {
            Debug.LogWarning("Confetti effect not assigned!");
        }
    }

    public void TriggerEffectAtPosition(ParticleSystem effect, Vector3 position)
    {
        if (effect != null)
        {
            PlayEffect(effect, position);
        }
    }

    private void PlayEffect(ParticleSystem effect, Vector3 position)
    {
        ParticleSystem instance = Instantiate(effect, position, Quaternion.identity);
        instance.Play();

        // Destroy the particle system after it finishes
        Destroy(instance.gameObject, instance.main.duration + instance.main.startLifetime.constantMax);
    }

    private Vector3 GetRandomScreenPosition()
    {
        float randomX = Random.Range(-screenWidth / 2, screenWidth / 2);
        float randomY = Random.Range(-screenHeight / 2, screenHeight / 2);
        return new Vector3(randomX, randomY, 0);
    }

    // Helper method to create a simple particle effect programmatically
    public ParticleSystem CreateSimpleParticleEffect(Color color, int particleCount = 50)
    {
        GameObject particleObj = new GameObject("ParticleEffect");
        ParticleSystem ps = particleObj.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime = 2f;
        main.startSpeed = 5f;
        main.startSize = 0.3f;
        main.startColor = color;
        main.maxParticles = particleCount;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, particleCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));

        return ps;
    }
}
