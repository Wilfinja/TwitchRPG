using UnityEngine;

public class Coin : MonoBehaviour
{
    [Header("Coin Properties")]
    [SerializeField] private int coinValue = 1;
    [SerializeField] private int xpValue = 1;
    [SerializeField] private CoinType coinType = CoinType.Normal;

    [Header("Physics")]
    private Rigidbody2D rb;
    private float lifetime;
    private float dissolveTime;
    private float existTime;
    private bool isDissolving = false;

    [Header("Visual")]
    private SpriteRenderer spriteRenderer;

    public enum CoinType
    {
        Normal,      // 1 coin, 1 XP
        Golden,      // 10 coins, 1 XP
        RedGem,      // 20 coins, 1 XP
        GreenGem,    // 30 coins, 1 XP
        BlueGem,     // 40 coins, 1 XP
        PurpleGem    // 50 coins, 1 XP
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        spriteRenderer = GetComponent<SpriteRenderer>();

        // Set tag for detection
        gameObject.tag = "Coin";

        // Add circle collider if missing
        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col == null)
        {
            col = gameObject.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.3f;
        }
    }

    public void Initialize(float life, float dissolve, Vector2 initialForce)
    {
        lifetime = life;
        dissolveTime = dissolve;
        existTime = 0f;

        // Determine coin value based on type
        SetCoinValueByType(coinType);

        // Apply initial force
        if (rb != null)
        {
            rb.AddForce(initialForce, ForceMode2D.Impulse);
        }
    }

    public void SetCoinType(CoinType type)
    {
        coinType = type;
        SetCoinValueByType(type);
    }

    private void SetCoinValueByType(CoinType type)
    {
        switch (type)
        {
            case CoinType.Normal:
                coinValue = 1;
                xpValue = 1;
                break;
            case CoinType.Golden:
                coinValue = 10;
                xpValue = 1;
                break;
            case CoinType.RedGem:
                coinValue = 20;
                xpValue = 1;
                break;
            case CoinType.GreenGem:
                coinValue = 30;
                xpValue = 1;
                break;
            case CoinType.BlueGem:
                coinValue = 40;
                xpValue = 1;
                break;
            case CoinType.PurpleGem:
                coinValue = 50;
                xpValue = 1;
                break;
        }
    }

    private void Update()
    {
        existTime += Time.deltaTime;

        // Start dissolving when lifetime is reached
        if (existTime >= lifetime && !isDissolving)
        {
            StartDissolve();
        }

        // Update dissolve effect
        if (isDissolving)
        {
            float dissolveProgress = (existTime - lifetime) / dissolveTime;

            if (spriteRenderer != null)
            {
                Color color = spriteRenderer.color;
                color.a = Mathf.Lerp(1f, 0f, dissolveProgress);
                spriteRenderer.color = color;
            }

            if (dissolveProgress >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }

    private void StartDissolve()
    {
        isDissolving = true;

        // Disable physics
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }
    }

    public int GetCoinValue()
    {
        return coinValue;
    }

    public int GetXPValue()
    {
        return xpValue;
    }

    public CoinType GetCoinType()
    {
        return coinType;
    }

    // Handle simultaneous collection by multiple characters
    private bool isCollected = false;

    public bool IsCollected()
    {
        return isCollected;
    }

    public void MarkAsCollected()
    {
        isCollected = true;
    }
}
