using UnityEngine;
using TMPro;

public class OnScreenCharacter : MonoBehaviour
{
    [Header("Character Data")]
    private string userId;
    private string username;
    private CharacterClass characterClass;
    private int level;

    [Header("Movement - X AXIS ONLY")]
    [SerializeField] private float walkSpeed = 2f;
    [SerializeField] private float runSpeed = 5f;
    private float homePositionX;
    private float idleRangeX;
    private float targetXPosition;
    private bool isMovingToCoin = false;
    private bool isCollecting = false;

    [Header("Idle Behavior")]
    [SerializeField] private float idleWanderInterval = 3f;
    private float idleTimer;

    [Header("Coin Tracking")]
    private GameObject targetCoin;
    private float coinDetectionRange = 15f;

    [Header("Animation")]
    [SerializeField] private string idleAnimationName = "Idle";
    [SerializeField] private string walkAnimationName = "Walk";
    [SerializeField] private string runAnimationName = "Run";
    [SerializeField] private string collectAnimationName = "Collect";
    [SerializeField] private float collectAnimationDuration = 0.5f;

    private Animator animator;
    private bool facingRight = true;
    private string currentAnimation = "";

    [Header("UI References")]
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text levelLabel;
    [SerializeField] private GameObject nameContainer;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void Initialize(string uid, string uname, CharacterClass charClass, int lvl, float homeX, float wanderRange)
    {
        userId = uid;
        username = uname;
        characterClass = charClass;
        level = lvl;
        homePositionX = homeX;
        idleRangeX = wanderRange;

        targetXPosition = transform.position.x;
        idleTimer = idleWanderInterval;

        UpdateUI();
        PlayAnimation(idleAnimationName);
    }

    private void Update()
    {
        // Don't move while collecting
        if (isCollecting) return;

        // Look for coins to collect
        if (!isMovingToCoin || targetCoin == null)
        {
            FindNearestCoin();
        }

        // Move toward target coin or idle wander
        if (isMovingToCoin && targetCoin != null)
        {
            MoveTowardCoin();
        }
        else
        {
            IdleWander();
        }
    }

    private void FindNearestCoin()
    {
        GameObject[] allCoins = GameObject.FindGameObjectsWithTag("Coin");

        if (allCoins.Length == 0)
        {
            targetCoin = null;
            isMovingToCoin = false;
            return;
        }

        GameObject nearest = null;
        float nearestDist = coinDetectionRange;

        foreach (GameObject coin in allCoins)
        {
            if (coin == null) continue;

            float dist = Mathf.Abs(coin.transform.position.x - transform.position.x);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = coin;
            }
        }

        if (nearest != null)
        {
            targetCoin = nearest;
            isMovingToCoin = true;
        }
    }

    private void MoveTowardCoin()
    {
        if (targetCoin == null)
        {
            isMovingToCoin = false;
            PlayAnimation(idleAnimationName);
            return;
        }

        float coinX = targetCoin.transform.position.x;
        float currentX = transform.position.x;
        float direction = Mathf.Sign(coinX - currentX);

        // Move only in X direction
        float newX = currentX + direction * runSpeed * Time.deltaTime;
        transform.position = new Vector3(newX, transform.position.y, transform.position.z);

        // Face direction of movement
        if (direction > 0 && !facingRight)
            Flip();
        else if (direction < 0 && facingRight)
            Flip();

        // Play run animation
        PlayAnimation(runAnimationName);

        // Check if close enough to coin
        float distToCoin = Mathf.Abs(coinX - currentX);
        if (distToCoin < 0.3f)
        {
            // Stop and wait for trigger to collect
            targetCoin = null;
            isMovingToCoin = false;
        }
    }

    private void IdleWander()
    {
        idleTimer -= Time.deltaTime;

        if (idleTimer <= 0f)
        {
            // Pick new random X position within idle range
            float randomX = homePositionX + Random.Range(-idleRangeX, idleRangeX);
            targetXPosition = randomX;
            idleTimer = idleWanderInterval;
        }

        float currentX = transform.position.x;
        float distToTarget = Mathf.Abs(targetXPosition - currentX);

        if (distToTarget > 0.1f)
        {
            // Move toward target X
            float direction = Mathf.Sign(targetXPosition - currentX);
            float newX = currentX + direction * walkSpeed * Time.deltaTime;
            transform.position = new Vector3(newX, transform.position.y, transform.position.z);

            // Face direction
            if (direction > 0 && !facingRight)
                Flip();
            else if (direction < 0 && facingRight)
                Flip();

            // Play walk animation
            PlayAnimation(walkAnimationName);
        }
        else
        {
            // Standing still
            PlayAnimation(idleAnimationName);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Coin") && !isCollecting)
        {
            CollectCoin(collision.gameObject);
        }
    }

    private void CollectCoin(GameObject coin)
    {
        if (coin == null || isCollecting) return;

        Coin coinScript = coin.GetComponent<Coin>();
        if (coinScript == null) return;

        // Check if already collected
        if (coinScript.IsCollected()) return;

        // Mark as collected
        coinScript.MarkAsCollected();

        // Start collection sequence
        StartCoroutine(CollectCoinSequence(coin, coinScript));
    }

    private System.Collections.IEnumerator CollectCoinSequence(GameObject coin, Coin coinScript)
    {
        isCollecting = true;

        // Play collect animation
        PlayAnimation(collectAnimationName);

        // Wait for animation to play
        yield return new WaitForSeconds(collectAnimationDuration);

        // Award coins and XP
        int coinValue = coinScript.GetCoinValue();
        int xpGain = coinScript.GetXPValue();

        RPGManager.Instance.AddCoins(userId, coinValue);

        if (ExperienceManager.Instance != null)
        {
            ExperienceManager.Instance.AddExperience(userId, xpGain);
        }

        Debug.Log($"[Character] {username} collected {coinScript.GetCoinType()} worth {coinValue} coins and {xpGain} XP");

        // Destroy coin
        Destroy(coin);

        // Resume normal behavior
        isCollecting = false;
        PlayAnimation(idleAnimationName);
    }

    private void PlayAnimation(string animationName)
    {
        if (animator == null) return;
        if (currentAnimation == animationName) return; // Don't replay same animation

        if (animator.runtimeAnimatorController != null)
        {
            animator.Play(animationName);
            currentAnimation = animationName;
        }
    }

    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;

        Vector3 nameScale = nameContainer.transform.localScale;

        nameScale.x *= -1;

        nameContainer.transform.localScale = nameScale;
    }

    private void UpdateUI()
    {
        if (nameLabel != null)
        {
            nameLabel.text = username;
        }

        if (levelLabel != null)
        {
            levelLabel.text = $"Lv.{level}";
        }
    }

    public void RefreshStats(int newLevel)
    {
        level = newLevel;
        UpdateUI();
    }

    public float GetHomePosition()
    {
        return homePositionX;
    }

    public string GetUserId()
    {
        return userId;
    }

    public string GetUsername()
    {
        return username;
    }
}
