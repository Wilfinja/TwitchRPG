using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Combat component added to OnScreenCharacter during expeditions.
/// Handles combat stats, health, damage, and turn-based actions.
/// Stats are CACHED from ViewerData at combat start for consistency.
/// </summary>
public class CombatEntity : MonoBehaviour
{
    [Header("Identity")]
    public string userId; // Twitch user ID for players
    public string entityName;
    public bool isPlayer;
    public int position; // 1-4 for players, 1-6 for enemies

    [Header("Cached Combat Stats - DO NOT MODIFY DIRECTLY")]
    public int maxHealth;
    public int currentHealth;
    public int strength;
    public int dexterity;
    public int constitution;
    public int intelligence;
    public int willpower;
    public int charisma;
    public int defense;
    public int damageBonus; // From equipment

    [Header("Combat State")]
    public bool isDead;
    public bool hasActedThisTurn;
    public string queuedAction;
    public bool actionConfirmed;

    [Header("Class & Resources")]
    public CharacterClass characterClass;
    public int sneakPoints; // Rogue: 0-6
    public FighterStance currentStance; // Fighter
    public int stanceCooldown; // Fighter
    public int mana; // Mage: 0-100
    public int wrath; // Cleric: 0-100
    public int balance; // Ranger: -10 to +10
    public int comboCounter; // Ranger
    public bool lastAttackWasMelee; // Ranger combo tracking

    [Header("Status Effects")]
    public List<StatusEffect> activeEffects = new List<StatusEffect>();

    [Header("Visual References")]
    public GameObject healthBarObject;
    public Animator animator;

    // Reference to the ViewerData (for syncing back after combat)
    private ViewerData viewerData;

    // Reference to OnScreenCharacter component
    private OnScreenCharacter onScreenChar;

    // Calculated Properties
    public float EvasionChance => Mathf.Floor(dexterity / 5f) * 0.01f; // 1% per 5 DEX

    #region Initialization

    /// <summary>
    /// Initialize as a player combatant - pulls stats from ViewerData and CACHES them
    /// </summary>
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
        onScreenChar = GetComponent<OnScreenCharacter>();

        if (viewerData != null)
        {
            // CACHE stats from ViewerData - these are locked for this combat
            CharacterStats totalStats = viewerData.GetTotalStats();
            maxHealth = totalStats.maxHealth;
            currentHealth = totalStats.currentHealth;
            strength = totalStats.strength;
            dexterity = totalStats.dexterity;
            constitution = totalStats.constitution;
            intelligence = totalStats.intelligence;
            willpower = totalStats.willpower;
            charisma = totalStats.charisma;
            defense = viewerData.equipped.GetTotalDefenseBonus();
            damageBonus = viewerData.equipped.GetTotalDamageBonus();
            characterClass = viewerData.characterClass;

            // Initialize class resources
            InitializeClassResources();

            Debug.Log($"[CombatEntity] Initialized player {entityName} - HP: {currentHealth}/{maxHealth}, DEF: {defense}, DMG: +{damageBonus}");
        }
        else
        {
            Debug.LogError($"[CombatEntity] Could not find ViewerData for {uid}!");
        }

        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Initialize as an enemy - stats are set directly
    /// </summary>
    public void InitializeEnemy(string name, int pos, int hp, int str, int dex, int con, int intel, int wil, int cha, int def)
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
        willpower = wil;
        charisma = cha;
        defense = def;
        damageBonus = 0;

        animator = GetComponent<Animator>();

        Debug.Log($"[CombatEntity] Initialized enemy {entityName} - HP: {currentHealth}/{maxHealth}");
    }

    private void InitializeClassResources()
    {
        switch (characterClass)
        {
            case CharacterClass.Rogue:
                sneakPoints = 0;
                break;
            case CharacterClass.Fighter:
                currentStance = FighterStance.None;
                stanceCooldown = 0;
                break;
            case CharacterClass.Mage:
                mana = 100;
                break;
            case CharacterClass.Cleric:
                wrath = 0;
                break;
            case CharacterClass.Ranger:
                balance = 0;
                comboCounter = 0;
                lastAttackWasMelee = false;
                break;
        }
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
            // Apply death lockout to ViewerData
            if (viewerData != null)
            {
                viewerData.isDead = true;
                viewerData.deathLockoutUntil = System.DateTime.Now.AddMinutes(30);
                viewerData.baseStats.currentHealth = 0;
                RPGManager.Instance.SaveGameData();

                Debug.Log($"[CombatEntity] {entityName} died - 30min lockout applied");
            }

            // Notify expedition manager
            ExpeditionManager.Instance?.OnPlayerDeath(userId);
        }
        else
        {
            // Enemy death
            ExpeditionManager.Instance?.OnEnemyDeath(this);
        }

        StartCoroutine(FadeOutAfterDeath());
    }

    private System.Collections.IEnumerator FadeOutAfterDeath()
    {
        yield return new WaitForSeconds(1.5f);

        if (healthBarObject != null)
            Destroy(healthBarObject);

        // For enemies, deactivate
        if (!isPlayer)
        {
            gameObject.SetActive(false);
        }
        // For players, keep them visible but faded/disabled
        else
        {
            // Optionally fade out the sprite
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = 0.3f;
                sr.color = c;
            }
        }
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
        if (!isPlayer) return;

        if (characterClass == CharacterClass.Mage)
        {
            int manaGain = Mathf.FloorToInt(intelligence * 0.1f); // 10% of INT per turn
            mana += manaGain;
            mana = Mathf.Clamp(mana, 0, 100);

            if (manaGain > 0)
            {
                CombatLog.Instance?.AddEntry($"{entityName} regenerated {manaGain} mana");
            }
        }
    }

    public void ProcessStatusEffects()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            StatusEffect effect = activeEffects[i];

            // Apply damage over time
            if (effect.damageOverTime > 0)
            {
                TakeDamage(effect.damageOverTime, this);
                CombatLog.Instance?.AddEntry($"{entityName} takes {effect.damageOverTime} damage from {effect.effectName}");
            }

            effect.duration--;

            if (effect.duration <= 0)
            {
                activeEffects.RemoveAt(i);
                CombatLog.Instance?.AddEntry($"{entityName}'s {effect.effectName} wore off.");
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

    /// <summary>
    /// Sync combat health back to ViewerData (called after each action)
    /// </summary>
    private void SyncToViewerData()
    {
        if (isPlayer && viewerData != null)
        {
            viewerData.baseStats.currentHealth = currentHealth;
        }
    }

    /// <summary>
    /// Sync all combat data back to ViewerData (called at end of expedition)
    /// </summary>
    public void SyncAllToViewerData()
    {
        if (isPlayer && viewerData != null)
        {
            viewerData.baseStats.currentHealth = currentHealth;
            viewerData.classResources.sneak = sneakPoints;
            viewerData.classResources.mana = mana;
            viewerData.classResources.wrath = wrath;
            viewerData.classResources.balance = balance;
            viewerData.classResources.currentStance = currentStance.ToString();

            RPGManager.Instance.SaveGameData();
            Debug.Log($"[CombatEntity] Synced all combat data for {entityName} back to ViewerData");
        }
    }

    public void ApplyStatusEffect(StatusEffect effect)
    {
        activeEffects.Add(effect);
        CombatLog.Instance?.AddEntry($"{entityName} is now {effect.effectName}!");
    }

    public CharacterClass GetCharacterClass()
    {
        return characterClass;
    }

    /// <summary>
    /// Grant wrath to all cleric allies when this player takes damage
    /// </summary>
    private void GrantWrathToClericAllies(int damageReceived)
    {
        var allPlayers = ExpeditionManager.Instance?.GetAllPlayerEntities();
        if (allPlayers == null) return;

        foreach (var player in allPlayers)
        {
            if (player.GetCharacterClass() == CharacterClass.Cleric && !player.isDead)
            {
                int wrathGain = Mathf.FloorToInt(damageReceived * 0.5f); // 50% of damage taken
                player.wrath += wrathGain;
                player.wrath = Mathf.Clamp(player.wrath, 0, 100);

                if (wrathGain > 0)
                {
                    CombatLog.Instance?.AddEntry($"{player.entityName} gained {wrathGain} wrath");
                }
            }
        }
    }

    #endregion

}
