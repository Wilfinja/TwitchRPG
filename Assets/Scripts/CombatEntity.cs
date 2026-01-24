using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Integrated combat entity that uses ViewerData for players
/// and stores stats directly for enemies
/// </summary>
public class CombatEntity : MonoBehaviour
{
    [Header("Identity")]
    public string userId; // Twitch user ID for players
    public string entityName;
    public bool isPlayer;
    public int position; // 1-4 for players, 1-6 for enemies

    [Header("Combat State")]
    public bool isDead;
    public bool hasActedThisTurn;
    public string queuedAction;
    public bool actionConfirmed;

    [Header("Class Resources - Players Only")]
    public CharacterClass characterClass;
    public int sneakPoints; // Rogue: 0-6
    public FighterStance currentStance; // Fighter
    public int stanceCooldown; // Fighter
    public int mana; // Mage: 0-100
    public int wrath; // Cleric: 0-100
    public int balance; // Ranger: -10 to +10
    public int comboCounter; // Ranger
    public bool lastAttackWasMelee; // Ranger combo tracking

    [Header("Enemy-Only Stats (Players use ViewerData)")]
    public int maxHealth;
    public int currentHealth;
    public int strength;
    public int dexterity;
    public int constitution;
    public int intelligence;
    public int defense;

    [Header("Status Effects")]
    public List<StatusEffect> activeEffects = new List<StatusEffect>();

    [Header("Visual References")]
    public GameObject healthBarObject;
    public SpriteRenderer spriteRenderer;
    public Animator animator;

    // For players: Reference to existing ViewerData
    private ViewerData viewerData;

    // Calculated Properties
    public float EvasionChance => Mathf.Floor(dexterity / 5f) * 0.01f; // 1% per 5 DEX

    #region Initialization

    public void InitializePlayer(string uid, string uname, int pos)
    {
        userId = uid;
        entityName = uname;
        isPlayer = true;
        position = pos;
        isDead = false;
        hasActedThisTurn = false;
        queuedAction = null;
        actionConfirmed = false;

        // Get viewer data from existing system
        viewerData = RPGManager.Instance.GetViewer(userId);

        if (viewerData != null)
        {
            // Pull stats from ViewerData
            CharacterStats stats = viewerData.GetTotalStats();
            maxHealth = stats.maxHealth;
            currentHealth = stats.currentHealth;
            strength = stats.strength;
            dexterity = stats.dexterity;
            constitution = stats.constitution;
            intelligence = stats.intelligence;
            defense = viewerData.equipped.GetTotalDefenseBonus();

            Debug.Log($"[Combat] Initialized player {entityName} - HP: {currentHealth}/{maxHealth}, DEF: {defense}");
        }
        else
        {
            Debug.LogError($"[Combat] Could not find ViewerData for {uid}!");
        }
    }

    public void InitializeEnemy(string name, int pos, int hp, int str, int dex, int con, int intel, int def)
    {
        entityName = name;
        isPlayer = false;
        position = pos;
        isDead = false;
        hasActedThisTurn = false;

        maxHealth = hp;
        currentHealth = hp;
        strength = str;
        dexterity = dex;
        constitution = con;
        intelligence = intel;
        defense = def;

        Debug.Log($"[Combat] Initialized enemy {entityName} - HP: {currentHealth}/{maxHealth}");
    }

    #endregion

    #region Combat Actions

    public void TakeDamage(int damage, CombatEntity attacker)
    {
        if (isDead) return;

        // Check evasion
        if (Random.value < EvasionChance)
        {
            CombatVisualEffects.Instance?.ShowEvadeText(transform.position);
            CombatLog.Instance?.AddEntry($"{entityName} evaded the attack!");
            return;
        }

        // Apply defense
        int finalDamage = Mathf.Max(0, damage - defense);
        currentHealth -= finalDamage;

        // Show damage
        CombatVisualEffects.Instance?.ShowDamageNumber(transform.position, finalDamage);

        if (defense > 0 && damage > finalDamage)
        {
            int blocked = damage - finalDamage;
            CombatVisualEffects.Instance?.ShowBlockedDamage(transform.position, blocked);
        }

        CombatLog.Instance?.AddEntry($"{attacker.entityName} hit {entityName} for {finalDamage} damage!");

        // Grant wrath to cleric allies when player is hit
        if (isPlayer && finalDamage > 0)
        {
            GrantWrathToClericAllies(finalDamage);
        }

        // Trigger hit animation
        animator?.SetTrigger("Hit");

        // Check for death
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
        else
        {
            UpdateHealthBar();
            SyncToViewerData();
        }
    }

    [System.Serializable]
    public class StatusEffect
    {
        public string effectName;
        public int duration; // Turns remaining
        public float damageMultiplier = 1f;
        public float defenseMultiplier = 1f;
        public int damageOverTime;
    }

    public void Heal(int amount, CombatEntity healer)
    {
        if (isDead) return;

        int healAmount = Mathf.Min(amount, maxHealth - currentHealth);
        currentHealth += healAmount;

        CombatVisualEffects.Instance?.ShowHealNumber(transform.position, healAmount);
        CombatLog.Instance?.AddEntry($"{healer.entityName} healed {entityName} for {healAmount} HP!");

        UpdateHealthBar();
        SyncToViewerData();
    }

    public void Die()
    {
        if (isDead) return; // Prevent double-death

        isDead = true;
        animator?.SetTrigger("Death");
        CombatLog.Instance?.AddEntry($"💀 {entityName} has been defeated!");

        if (isPlayer)
        {
            // Apply death lockout to existing ViewerData system
            if (viewerData != null)
            {
                viewerData.isDead = true;
                viewerData.deathLockoutUntil = System.DateTime.Now.AddMinutes(30);
                viewerData.baseStats.currentHealth = 0;
                RPGManager.Instance.SaveGameData();

                Debug.Log($"[Combat] {entityName} died - 30min lockout applied");
            }

            // Notify expedition manager
            ExpeditionManager.Instance?.OnPlayerDeath(userId);
        }
        else
        {
            // Enemy death
            ExpeditionManager.Instance?.OnEnemyDeath(this);
        }

        StartCoroutine(DespawnAfterAnimation());
    }

    private System.Collections.IEnumerator DespawnAfterAnimation()
    {
        yield return new WaitForSeconds(1.5f);

        if (healthBarObject != null)
            Destroy(healthBarObject);

        gameObject.SetActive(false);
    }

    #endregion

    #region Turn Management

    public void ResetTurn()
    {
        hasActedThisTurn = false;
        queuedAction = null;
        actionConfirmed = false;
    }

    public void RegenerateManaIfMage()
    {
        if (!isPlayer || viewerData == null) return;

        if (viewerData.characterClass == CharacterClass.Mage)
        {
            // Mage regenerates mana based on INT
            int manaGain = Mathf.FloorToInt(intelligence * 0.1f); // 10% of INT per turn
            viewerData.classResources.mana += manaGain;
            viewerData.classResources.mana = Mathf.Clamp(viewerData.classResources.mana, 0, 100);

            if (manaGain > 0)
            {
                CombatLog.Instance?.AddEntry($"{entityName} regenerated {manaGain} mana");
            }
        }
    }

    #endregion

    #region Helper Methods

    public void UpdateHealthBar()
    {
        if (healthBarObject != null)
        {
            CombatHealthBar healthBar = healthBarObject.GetComponent<CombatHealthBar>();
            healthBar?.UpdateHealth(currentHealth, maxHealth);
        }
    }

    // Sync combat stats back to ViewerData
    private void SyncToViewerData()
    {
        if (isPlayer && viewerData != null)
        {
            viewerData.baseStats.currentHealth = currentHealth;
        }
    }

    public void ApplyStatusEffect(StatusEffect effect)
    {
        activeEffects.Add(effect);
        CombatLog.Instance?.AddEntry($"{entityName} is now {effect.effectName}!");
    }

    public void ProcessStatusEffects()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            StatusEffect effect = activeEffects[i];
            effect.duration--;

            if (effect.duration <= 0)
            {
                activeEffects.RemoveAt(i);
                CombatLog.Instance?.AddEntry($"{entityName}'s {effect.effectName} wore off.");
            }
        }
    }

    // Get class resources from ViewerData
    public ClassResources GetClassResources()
    {
        if (isPlayer && viewerData != null)
            return viewerData.classResources;
        return null;
    }

    public CharacterClass GetCharacterClass()
    {
        if (isPlayer && viewerData != null)
            return viewerData.characterClass;
        return CharacterClass.None;
    }

    // Grant wrath to all cleric allies when this player takes damage
    private void GrantWrathToClericAllies(int damageReceived)
    {
        var allPlayers = ExpeditionManager.Instance?.GetAllPlayerEntities();
        if (allPlayers == null) return;

        foreach (var player in allPlayers)
        {
            if (player.GetCharacterClass() == CharacterClass.Cleric)
            {
                var resources = player.GetClassResources();
                if (resources != null)
                {
                    int wrathGain = Mathf.FloorToInt(damageReceived * 0.5f); // 50% of damage taken
                    resources.wrath += wrathGain;
                    resources.wrath = Mathf.Clamp(resources.wrath, 0, 100);

                    if (wrathGain > 0)
                    {
                        CombatLog.Instance?.AddEntry($"{player.entityName} gained {wrathGain} wrath");
                    }
                }
            }
        }
    }

    #endregion
}
