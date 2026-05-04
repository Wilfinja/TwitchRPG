using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.VirtualTexturing;

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
    public bool wasHealedThisTurn = false;
    public string queuedAction;
    public bool actionConfirmed;

    [Header("Class & Resources")]
    public CharacterClass characterClass;
    public int sneakPoints; // Rogue: 0-6
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
    public GameObject classResourceBarObject;

    [Header("Fighter Stance System")]
    public FighterStance currentStance = FighterStance.None;

    // Base stats (before stance modifiers)
    private int baseStrength;
    private int baseConstitution;
    private int baseDefense;
    private int baseMaxHealth;

    public Dictionary<string, object> passiveState = new Dictionary<string, object>();

    // Reference to the ViewerData (for syncing back after combat)
    public ViewerData viewerData;

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
        wasHealedThisTurn = false;
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
            currentHealth = maxHealth;
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

        // Copy stats from ViewerData
        //strength = viewerData.baseStats.strength;
        //constitution = viewerData.baseStats.constitution;
        //defense = viewerData.equipped.GetTotalDefenseBonus();

        // Save base stats before any modifiers
        InitializeBaseStats();

        // Fighters start in no stance (could also start in Aggressive)
        if (characterClass == CharacterClass.Fighter)
        {
            currentStance = FighterStance.Aggressive;
        }

        if (characterClass == CharacterClass.Mage)
        {
            MageChargeSystem chargeSystem = gameObject.GetComponent<MageChargeSystem>();
            if (chargeSystem == null)
            {
                chargeSystem = gameObject.AddComponent<MageChargeSystem>();
            }
            Debug.Log($"[CombatEntity] Added MageChargeSystem to {entityName}");
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
        wasHealedThisTurn = false;

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

        // ── Evasion ───────────────────────────────────────────────────────────────
        if (Random.value < EvasionChance)
        {
            CombatVisualEffects.Instance?.ShowEvadeText(transform.position);
            CombatLog.Instance?.AddEntry($"{entityName} evaded the attack!");
            return;
        }

        // ── Rogue sneak damage reduction ──────────────────────────────────────────
        if (characterClass == CharacterClass.Rogue && sneakPoints > 0)
        {
            int reductionPercent = GetSneakDamageReduction();
            int reducedAmount = Mathf.RoundToInt(damage * (reductionPercent / 100f));
            damage -= reducedAmount;
            CombatLog.Instance?.AddEntry(
                $"{entityName}'s sneak reduced damage by {reducedAmount} ({reductionPercent}%)!"
            );
        }

        // ── Marked: increase incoming damage ─────────────────────────────────────
        float markedMultiplier = GetMarkedDamageMultiplier();
        if (markedMultiplier > 1f)
        {
            damage = Mathf.RoundToInt(damage * markedMultiplier);
            Debug.Log($"[Marked] {entityName} takes {markedMultiplier:F2}x damage → {damage}");
        }

        // ── Defense (with Exposed reduction) ─────────────────────────────────────
        int currentHealthBeforeHit = currentHealth;
        int totalDefense = defense + GetTemporaryDefenseBonus() - GetExposedDefenseReduction();
        totalDefense = Mathf.Max(0, totalDefense); // Defense can never go below 0

        // ── Barrier absorbs first ─────────────────────────────────────────────────
        int remainingDamage = AbsorbWithBarrier(damage);
        int barrierAbsorbed = damage - remainingDamage;
        if (barrierAbsorbed > 0)
        {
            CombatVisualEffects.Instance?.ShowBlockedDamage(transform.position, barrierAbsorbed);
            CombatLog.Instance?.AddEntry($"🛡 {entityName}'s barrier absorbed {barrierAbsorbed} damage!");
        }

        // ── Apply defense to remaining damage ─────────────────────────────────────
        int finalDamage = Mathf.Max(0, remainingDamage - totalDefense);
        currentHealth -= finalDamage;

        // ── Visuals ───────────────────────────────────────────────────────────────
        CombatVisualEffects.Instance?.ShowDamageNumber(transform.position, finalDamage);

        // Show blocked damage visually
        if (totalDefense > 0 && remainingDamage > finalDamage)
        {
            int blocked = remainingDamage - finalDamage;
            CombatVisualEffects.Instance?.ShowBlockedDamage(transform.position, blocked);
        }

        if (finalDamage > 0)
        {
            if (totalDefense > 0 && remainingDamage > finalDamage)
            {
                int blocked = remainingDamage - finalDamage;
                CombatLog.Instance?.AddEntry(
                    $"{attacker.entityName} hit {entityName} for {finalDamage} damage " +
                    $"({blocked} blocked by {totalDefense} defense)!"
                );
            }
            else
            {
                CombatLog.Instance?.AddEntry($"{attacker.entityName} hit {entityName} for {finalDamage} damage!");
            }
        }
        else if (remainingDamage > 0)
        {
            // All damage blocked
            CombatLog.Instance?.AddEntry(
                $"{attacker.entityName} attacked {entityName} but {totalDefense} defense blocked all {remainingDamage} damage!"
            );
        }

        // ── Consume one-hit defense boosts ────────────────────────────────────────
        ConsumeOneHitDefenseBoosts();

        // ── Grant wrath to cleric allies ─────────────────────────────────────────
        if (isPlayer && finalDamage > 0)
            GrantWrathToClericAllies(finalDamage);

        // ── Hit animation ─────────────────────────────────────────────────────────
        animator?.SetTrigger("Hit");

        // ── Riposte counter-attack ────────────────────────────────────────────────
        if (finalDamage > 0 && attacker != null && !attacker.isDead)
            TriggerRiposte(finalDamage, attacker);

        // ── Primed condition detonation ────────────────────────────────-──────-
        // Pass currentHealthBeforeHit so PercentCurrentHealth thresholds are
        // calculated against HP BEFORE this hit landed, which is the intuitive
        // design expectation ("was hit for 25% of their health").
        if (finalDamage > 0)
            TriggerPrimed(finalDamage, currentHealthBeforeHit);

        // Calculate current health percentage (0-100)
        int currentHpPercent = Mathf.RoundToInt((float)currentHealth / maxHealth * 100f);

        // Pass the percentage as the second argument
        PassiveEffectProcessor.OnHealthThreshold(this, currentHpPercent);

        // ── Death check ───────────────────────────────────────────────────────────
        if (currentHealth <= 0)
        {
            currentHealth = 0;

            if (PassiveEffectProcessor.OnDeath(this))
            {
                // Death was prevented (Phoenix triggered)!
                return;
            }

            Die();
            PassiveEffectProcessor.OnKill(attacker, this);
        }
        else
        {
            UpdateHealthBar();
            SyncToViewerData();
        }

        //Fire passive hooks after all damage is resolved
        PassiveEffectProcessor.OnTakeDamage(this, attacker, finalDamage);
        if (attacker != null && !attacker.isDead)
        PassiveEffectProcessor.OnDealDamage(attacker, this, finalDamage);
    }

    public void Heal(int amount, CombatEntity healer)
    {
        // Store pre-heal HP so Overcharge can calculate excess
        passiveState["preHealHP"] = currentHealth;

        if (isDead) return;

        // Curse reduces healing
        float curseMod = GetHealingReductionMultiplier();
        if (curseMod < 1f)
        {
            int reducedBy = Mathf.RoundToInt(amount * (1f - curseMod));
            amount = Mathf.RoundToInt(amount * curseMod);
            if (reducedBy > 0)
                CombatLog.Instance?.AddEntry($"🖤 {entityName}'s curse reduced healing by {reducedBy}!");
        }

        int healAmount = Mathf.Min(amount, maxHealth - currentHealth);
        currentHealth += healAmount;

        wasHealedThisTurn = true;

        CombatVisualEffects.Instance?.ShowHealNumber(transform.position, healAmount);
        CombatLog.Instance?.AddEntry($"{healer.entityName} healed {entityName} for {healAmount} HP!");

        // Fire OnHeal passives — passes raw amount so Overcharge can find excess
        PassiveEffectProcessor.OnHeal(this, healer, amount);

        UpdateHealthBar();
        SyncToViewerData();
    }

    public void Die()
    {
        if (isDead) return; // Prevent double-death

        // Give Phoenix (and any future OnDeath passives) a chance to prevent death
        // before we commit the death state
        if (isPlayer && PassiveEffectProcessor.OnDeath(this))
        {
            // A passive saved this entity — abort the death sequence
            Debug.Log($"[CombatEntity] {entityName} death prevented by passive!");
            UpdateHealthBar();
            return;
        }

        isDead = true;
        animator?.SetTrigger("Death");
        CombatLog.Instance?.AddEntry($"💀 {entityName} has been defeated!");

        if (isPlayer)
        {
            if (viewerData != null)
            {
                viewerData.isDead = true;
                viewerData.deathLockoutUntil = System.DateTime.Now.AddMinutes(30);
                viewerData.baseStats.currentHealth = 0;
                RPGManager.Instance.SaveGameData();

                Debug.Log($"[CombatEntity] {entityName} died - 30min lockout applied");
            }

            ExpeditionManager.Instance?.OnPlayerDeath(userId);
        }
        else
        {
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
        wasHealedThisTurn = false;
        queuedAction = null;
        actionConfirmed = false;

        animator.Play("Idle");
    }

    public void RegenerateManaIfMage()
    {
        if (!isPlayer) return;

        if (characterClass == CharacterClass.Mage)
        {
            // Base regen: 10% of INT
            int baseRegen = Mathf.FloorToInt(intelligence * 0.1f);

            // ✅ NEW: Add equipment bonus
            int equipmentBonus = 0;
            if (viewerData != null)
            {
                equipmentBonus = viewerData.equipped.GetTotalManaRegenBonus();
            }

            int totalRegen = baseRegen + equipmentBonus;

            // Apply max mana cap
            int maxMana = 100;
            if (viewerData != null)
            {
                maxMana += viewerData.equipped.GetTotalMaxManaBonus();
            }

            mana += totalRegen;
            mana = Mathf.Clamp(mana, 0, maxMana);

            if (totalRegen > 0)
            {
                CombatLog.Instance?.AddEntry(
                    $"{entityName} regenerated {totalRegen} mana ({baseRegen} base + {equipmentBonus} bonus)"
                );
            }
        }
    }

    public void ProcessStatusEffects()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            StatusEffect effect = activeEffects[i];

            // ── Original: Flat damage-over-time ───────────────────────────────────
            if (effect.damageOverTime > 0)
            {
                TakeDamage(effect.damageOverTime, this);
                CombatLog.Instance?.AddEntry(
                    $"{entityName} takes {effect.damageOverTime} damage from {effect.effectName}"
                );
            }

            // ── Bleed DoT (paused when healed this turn) ──────────────────────────
            if (effect.isBleed && effect.bleedDamagePerTurn > 0)
            {
                if (wasHealedThisTurn)
                {
                    CombatLog.Instance?.AddEntry(
                        $"🩸 {entityName}'s bleed is suppressed this turn (was healed)."
                    );
                    // Duration does NOT tick down – bleed is paused, not expired
                    continue;
                }
                else
                {
                    TakeDamage(effect.bleedDamagePerTurn, this);
                    CombatLog.Instance?.AddEntry(
                        $"🩸 {entityName} bleeds for {effect.bleedDamagePerTurn} damage!"
                    );
                }
            }

            // ── Barrier: remove if fully depleted ─────────────────────────────────
            if (effect.isBarrier && effect.barrierCurrentAmount <= 0)
            {
                CombatLog.Instance?.AddEntry($"{entityName}'s barrier was broken!");
                activeEffects.RemoveAt(i);
                continue;
            }

            // ── Tick duration ─────────────────────────────────────────────────────
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

    /// <summary>
    /// Attempts to apply a status effect. Negative effects are first checked
    /// against the target's Status Resistance before being added.
    /// </summary>
    public void ApplyStatusEffect(StatusEffect effect)
    {
        // ── Resistance check (negative effects only) ───────────────────────────
        if (effect.isNegativeEffect)
        {
            float resistance = GetStatusResistance();

            if (resistance > 0f)
            {
                float resistRoll = Random.value; // 0.0 – 1.0

                if (resistRoll < resistance)
                {
                    // Effect was resisted – log and bail out
                    CombatLog.Instance?.AddEntry(
                        $"🛡 {entityName} resisted {effect.effectName}! " +
                        $"({resistance * 100:F0}% resistance)"
                    );
                    Debug.Log($"[Resistance] {entityName} resisted {effect.effectName} " +
                              $"(roll {resistRoll:F2} < threshold {resistance:F2})");
                    return;
                }
                else
                {
                    Debug.Log($"[Resistance] {entityName} failed to resist {effect.effectName} " +
                              $"(roll {resistRoll:F2} >= threshold {resistance:F2})");
                }
            }
        }



        // ── Effect applied ─────────────────────────────────────────────────────
        activeEffects.Add(effect);
        CombatLog.Instance?.AddEntry($"{entityName} is now affected by {effect.effectName}!");
    }

    /// <summary>
    /// Calculates this entity's chance to fully resist a negative status effect.
    ///
    /// Formula: base = Willpower × 0.5%  (so 10 WIL = 5%, 50 WIL = 25%)
    ///          bonus from active effects (e.g. a Cleric Fortify buff)
    ///          hard cap at 75% to keep debuffs relevant
    ///
    /// Returns a value in [0, 0.75] where 0.75 = 75% resist chance.
    ///
    /// Design notes:
    ///   - Willpower is the primary driver, giving WIL a meaningful combat role.
    ///   - Clerics benefit most due to their GetBoostedWillpower() bonus at high wrath.
    ///   - Status effects that already have a low applicationChance compound with
    ///     resistance: both rolls must pass independently.
    /// </summary>
    public float GetStatusResistance()
    {
        // Base resistance from Willpower
        // Use GetBoostedWillpower() so Cleric's wrath bonus counts
        int effectiveWillpower = GetBoostedWillpower();
        float baseResistance = effectiveWillpower * 0.005f; // 0.5% per WIL point

        // Bonus resistance from active status effects
        // (allows abilities like "Fortify" or "Iron Will" to grant extra resist)
        float bonusResistance = 0f;
        foreach (StatusEffect effect in activeEffects)
        {
            bonusResistance += effect.statusResistanceBonus;
        }

        float totalResistance = baseResistance + bonusResistance;

        // Hard cap at 75%
        return Mathf.Clamp(totalResistance, 0f, 0.75f);
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
    public void InitializeBaseStats()
    {
        baseStrength = strength;
        baseConstitution = constitution;
        baseDefense = defense;
        baseMaxHealth = maxHealth;

        Debug.Log($"[CombatEntity] Base stats saved - STR: {baseStrength}, CON: {baseConstitution}, DEF: {baseDefense}, MaxHP: {baseMaxHealth}");
    }

    /// <summary>
    /// Absorbs incoming damage through any active Barrier effects.
    /// Returns the damage that was NOT absorbed (passes through to HP).
    /// </summary>
    public int AbsorbWithBarrier(int incomingDamage)
    {
        int remaining = incomingDamage;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            StatusEffect effect = activeEffects[i];
            if (!effect.isBarrier || effect.barrierCurrentAmount <= 0) continue;

            int absorbed = Mathf.Min(remaining, effect.barrierCurrentAmount);
            effect.barrierCurrentAmount -= absorbed;
            remaining -= absorbed;

            Debug.Log($"[Barrier] {entityName}: absorbed {absorbed}, barrier remaining: {effect.barrierCurrentAmount}");

            if (effect.barrierCurrentAmount <= 0)
            {
                CombatLog.Instance?.AddEntry($"{entityName}'s {effect.effectName} barrier was shattered!");
                activeEffects.RemoveAt(i);
            }

            if (remaining <= 0) break;
        }

        return remaining;
    }

    /// <summary>
    /// Returns the combined damage multiplier from all Mark effects (multiplicative).
    /// </summary>
    public float GetMarkedDamageMultiplier()
    {
        float multiplier = 1f;
        foreach (StatusEffect effect in activeEffects)
        {
            if (effect.isMark)
                multiplier *= effect.markedDamageMultiplier;
        }
        return multiplier;
    }

    /// <summary>
    /// Returns the total flat defense reduction from all Exposed effects.
    /// </summary>
    public int GetExposedDefenseReduction()
    {
        int total = 0;
        foreach (StatusEffect effect in activeEffects)
        {
            if (effect.isExposed)
                total += effect.exposedDefenseReduction;
        }
        return total;
    }

    /// <summary>
    /// Returns the healing multiplier after applying all Curse effects (multiplicative).
    /// 1.0 = full healing, 0.5 = 50% healing, 0.0 = no healing.
    /// </summary>
    public float GetHealingReductionMultiplier()
    {
        float multiplier = 1f;
        foreach (StatusEffect effect in activeEffects)
        {
            if (effect.isCurse)
                multiplier *= (1f - effect.healingReductionPercent);
        }
        return Mathf.Clamp(multiplier, 0f, 1f);
    }

    /// <summary>
    /// Returns true if this entity is currently stunned.
    /// </summary>
    public bool IsStunned()
    {
        foreach (StatusEffect effect in activeEffects)
            if (effect.isStun) return true;
        return false;
    }

    /// <summary>
    /// Returns true if this entity is currently silenced (cannot use abilities).
    /// </summary>
    public bool IsSilenced()
    {
        foreach (StatusEffect effect in activeEffects)
            if (effect.isSilence) return true;
        return false;
    }

    /// <summary>
    /// Returns true if this entity has a Haste buff (acts twice this turn).
    /// </summary>
    public bool IsHasted()
    {
        foreach (StatusEffect effect in activeEffects)
            if (effect.isHaste) return true;
        return false;
    }

    /// <summary>
    /// Returns true if this entity is enraged (forced targeting applies).
    /// </summary>
    public bool IsEnraged()
    {
        foreach (StatusEffect effect in activeEffects)
            if (effect.isEnrage) return true;
        return false;
    }

    /// <summary>
    /// Returns the entityName of the taunt target, or null if not taunted.
    /// </summary>
    public string GetTauntTargetName()
    {
        foreach (StatusEffect effect in activeEffects)
            if (effect.isTaunt && !string.IsNullOrEmpty(effect.tauntTargetEntityName))
                return effect.tauntTargetEntityName;
        return null;
    }

    public void RecalculateStatsWithStance()
    {
        Debug.Log($"[Stance] Recalculating stats. Old - STR: {strength}, CON: {constitution}, DEF: {defense}, MaxHP: {maxHealth}");

        // Reset to base stats
        strength = baseStrength;
        constitution = baseConstitution;
        defense = baseDefense;
        maxHealth = baseMaxHealth;

        // Store old health for percentage calculation
        float healthPercent = maxHealth > 0 ? (float)currentHealth / maxHealth : 1f;

        // Apply stance bonuses
        switch (currentStance)
        {
            case FighterStance.Aggressive:
                strength = Mathf.RoundToInt(baseStrength * 1.1f); // +10% STR
                Debug.Log($"[Stance] Aggressive: STR {baseStrength} → {strength}");
                break;

            case FighterStance.Defensive:
                constitution = Mathf.RoundToInt(baseConstitution * 1.1f); // +10% CON

                // ✅ FIX: Calculate maxHealth change based on CON difference
                int conDifference = constitution - baseConstitution;
                maxHealth = baseMaxHealth + (conDifference * 10);

                // Maintain health percentage (so current health scales with new max)
                currentHealth = Mathf.RoundToInt(maxHealth * healthPercent);

                Debug.Log($"[Stance] Defensive: CON {baseConstitution} → {constitution}, MaxHP {baseMaxHealth} → {maxHealth}, CurrentHP: {currentHealth}");
                break;

            case FighterStance.Reflective:
                defense = baseDefense + 10; // +10 flat DEF
                Debug.Log($"[Stance] Reflective: DEF {baseDefense} → {defense}");
                break;

            case FighterStance.None:
                Debug.Log($"[Stance] No stance - using base stats");
                break;
        }

        Debug.Log($"[Stance] Final - STR: {strength}, CON: {constitution}, DEF: {defense}, MaxHP: {maxHealth}, CurrentHP: {currentHealth}");

        UpdateHealthBar();
    }

    public bool ChangeStance(FighterStance newStance)
    {
        if (characterClass != CharacterClass.Fighter)
            return false;

        FighterStance oldStance = currentStance;
        currentStance = newStance;
        RecalculateStatsWithStance();

        UpdateClassResourceBar();

        CombatLog.Instance?.AddEntry(
            $"{entityName} shifts to {GetStanceName(newStance)} Stance!"
        );

        return true;
    }

    private string GetStanceName(FighterStance stance)
    {
        switch (stance)
        {
            case FighterStance.Aggressive: return "Aggressive";
            case FighterStance.Defensive: return "Defensive";
            case FighterStance.Reflective: return "Reflective";
            default: return "None";
        }
    }

    public int GetBoostedWillpower()
    {
        int baseWillpower = willpower;

        // Cleric: Willpower bonus at high wrath
        if (characterClass == CharacterClass.Cleric)
        {
            if (wrath >= 100)
            {
                return Mathf.RoundToInt(baseWillpower * 1.5f); // +50%
            }
            else if (wrath >= 75)
            {
                return Mathf.RoundToInt(baseWillpower * 1.25f); // +25%
            }
        }

        return baseWillpower;
    }

    public string GetCurrentStanceBonusText()
    {
        if (characterClass != CharacterClass.Fighter)
            return "";

        switch (currentStance)
        {
            case FighterStance.Aggressive:
                return $"+{strength - baseStrength} STR";
            case FighterStance.Defensive:
                return $"+{constitution - baseConstitution} CON";
            case FighterStance.Reflective:
                return $"+{defense - baseDefense} DEF";
            default:
                return "No Stance";
        }
    }

    public void UpdateClassResourceBar()
    {
        if (classResourceBarObject == null) return;

        ClassResourceBar resourceBar = classResourceBarObject.GetComponent<ClassResourceBar>();
        if (resourceBar == null) return;

        Debug.Log($"Refreshing UI for {entityName}");

        switch (characterClass)
        {
            case CharacterClass.Mage:
                resourceBar.UpdateMana(mana, 100);
                break;

            case CharacterClass.Rogue:
                resourceBar.UpdateSneak(sneakPoints, 6);
                break;

            case CharacterClass.Cleric:
                resourceBar.UpdateWrath(wrath, 100);
                break;

            case CharacterClass.Fighter:
                resourceBar.UpdateStance(currentStance);
                break;

            case CharacterClass.Ranger:
                resourceBar.UpdateBalance(balance, -10, 10);
                break;
        }
    }

    /// <summary>
    /// Get total temporary defense from all active buffs
    /// </summary>
    /// <summary>
    /// Get total temporary defense from all active buffs (with dynamic scaling)
    /// </summary>
    int GetTemporaryDefenseBonus()
    {
        int bonus = 0;

        foreach (StatusEffect effect in activeEffects)
        {
            // ✅ Check if effect has dynamic scaling
            if (effect.defenseScalingStat != DamageStat.None && effect.defenseScalingMultiplier > 0f)
            {
                // Calculate scaled defense
                int statValue = GetStatValueForDefense(effect.defenseScalingStat);
                int scaledBonus = Mathf.RoundToInt(statValue * effect.defenseScalingMultiplier);
                int totalDefense = effect.baseDefenseAmount + scaledBonus;

                bonus += totalDefense;

                Debug.Log($"[Defense] {entityName}'s {effect.effectName}: {effect.baseDefenseAmount} base + " +
                         $"({statValue} {effect.defenseScalingStat} × {effect.defenseScalingMultiplier}) = {totalDefense}");
            }
            else
            {
                // Flat defense bonus (backward compatible)
                bonus += effect.temporaryDefenseBonus;
            }
        }

        return bonus;
    }

    /// <summary>
    /// Get stat value for defense scaling calculation
    /// </summary>
    int GetStatValueForDefense(DamageStat stat)
    {
        switch (stat)
        {
            case DamageStat.Strength: return strength;
            case DamageStat.Dexterity: return dexterity;
            case DamageStat.Constitution: return constitution;
            case DamageStat.Intelligence: return intelligence;
            case DamageStat.Willpower: return willpower;
            case DamageStat.Charisma: return charisma;
            default: return 0;
        }
    }

    /// <summary>
    /// Remove defense buffs that are consumed on hit
    /// </summary>
    void ConsumeOneHitDefenseBoosts()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            StatusEffect effect = activeEffects[i];
            if (effect.consumedOnHit && effect.temporaryDefenseBonus > 0)
            {
                CombatLog.Instance?.AddEntry($"{entityName}'s {effect.effectName} was consumed!");
                activeEffects.RemoveAt(i);
            }
        }
    }

    public int GetSneakDamageReduction()
    {
        if (characterClass != CharacterClass.Rogue) return 0;

        // 10% reduction per sneak point
        int reductionPercent = sneakPoints * 10;
        return Mathf.Clamp(reductionPercent, 0, 60); // Max 60%
    }

    /// <summary>
    /// Get a stat value with temporary boosts applied
    /// </summary>
    public int GetBoostedStat(BoostableStat stat)
    {
        int baseValue = 0;

        // Get base stat value
        switch (stat)
        {
            case BoostableStat.Strength:
                baseValue = strength;
                break;
            case BoostableStat.Constitution:
                baseValue = constitution;
                break;
            case BoostableStat.Dexterity:
                baseValue = dexterity;
                break;
            case BoostableStat.Intelligence:
                baseValue = intelligence;
                break;
            case BoostableStat.Willpower:
                baseValue = willpower;
                break;
            case BoostableStat.Charisma:
                baseValue = charisma;
                break;
            case BoostableStat.None:
            default:
                return 0;
        }

        // Add temporary boosts from status effects
        int bonus = 0;
        foreach (StatusEffect effect in activeEffects)
        {
            if (effect.statBoostType == stat)
            {
                bonus += effect.statBoostAmount;
            }
        }

        int totalValue = baseValue + bonus;

        // Debug log if boosted
        if (bonus > 0)
        {
            Debug.Log($"[CombatEntity] {entityName} {stat}: {baseValue} + {bonus} = {totalValue}");
        }

        return totalValue;
    }

    /// <summary>
    /// Scans active effects for any Riposte buff. When found, calculates and
    /// deals a counter-attack to <paramref name="attacker"/>.
    /// </summary>
    /// <param name="finalDamageReceived">Damage that actually landed (post-defense).</param>
    /// <param name="attacker">The entity that struck us.</param>
    private void TriggerRiposte(int finalDamageReceived, CombatEntity attacker)
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            StatusEffect effect = activeEffects[i];
            if (!effect.isRiposte) continue;

            // ── Calculate counter damage ──────────────────────────────────────────
            float counterDamage = 0f;

            // 1) Reflect portion of incoming damage
            counterDamage += finalDamageReceived * effect.riposteDamagePercent;

            // 2) Flat bonus
            counterDamage += effect.riposteFlatBonus;

            // 3) Stat scaling (e.g. DEX for a fencer-style Fighter)
            if (effect.riposteScalingStat != DamageStat.None && effect.riposteScalingMultiplier > 0f)
            {
                int statValue = GetStatValueForRiposte(effect.riposteScalingStat);
                counterDamage += statValue * effect.riposteScalingMultiplier;
            }

            int finalCounter = Mathf.Max(1, Mathf.RoundToInt(counterDamage));

            // ── Apply counter to the attacker ────────────────────────────────────
            // Use a raw health deduction so we don't re-trigger Riposte chains.
            int counterAfterDefense = Mathf.Max(0, finalCounter - attacker.defense);
            attacker.currentHealth -= counterAfterDefense;

            CombatVisualEffects.Instance?.ShowDamageNumber(attacker.transform.position, counterAfterDefense);
            CombatLog.Instance?.AddEntry(
                $"⚔️ {entityName} RIPOSTES {attacker.entityName} for {counterAfterDefense} damage!"
            );
            Debug.Log($"[Riposte] {entityName} countered {attacker.entityName} " +
                      $"(raw: {finalCounter}, after def: {counterAfterDefense})");

            attacker.animator?.SetTrigger("Hit");

            if (attacker.currentHealth <= 0)
            {
                attacker.currentHealth = 0;
                attacker.Die();
            }
            else
            {
                attacker.UpdateHealthBar();
            }

            // ── Consume if needed ─────────────────────────────────────────────────
            if (effect.riposteConsumedOnUse)
            {
                CombatLog.Instance?.AddEntry($"{entityName}'s {effect.effectName} was consumed!");
                activeEffects.RemoveAt(i);
            }

            // Only one Riposte triggers per hit (the first one found).
            // Remove this break if you want stacked Ripostes to all fire.
            break;
        }
    }

    /// <summary>
    /// Returns the raw stat value from this entity for Riposte scaling.
    /// Mirrors CombatCalculations.GetStatValue but accessible on the entity.
    /// </summary>
    private int GetStatValueForRiposte(DamageStat stat)
    {
        switch (stat)
        {
            case DamageStat.Strength: return strength;
            case DamageStat.Dexterity: return dexterity;
            case DamageStat.Intelligence: return intelligence;
            case DamageStat.Willpower: return willpower;
            case DamageStat.Charisma: return charisma;
            case DamageStat.Constitution: return constitution;
            default: return 0;
        }
    }

    private void TriggerPrimed(int finalDamage, int healthBeforeHit)
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            StatusEffect effect = activeEffects[i];
            if (!effect.isPrimed) continue;
            if (effect.primedEffects == null || effect.primedEffects.Count == 0) continue;

            // ── Check if the threshold is met ─────────────────────────────────
            if (!IsPrimeThresholdMet(effect, finalDamage, healthBeforeHit))
                continue;

            // ── Threshold met: detonate ────────────────────────────────────────
            CombatLog.Instance?.AddEntry(
                $"💥 {entityName}'s {effect.effectName} detonates! " +
                $"(took {finalDamage} damage, threshold: {FormatPrimeThreshold(effect)})"
            );
            Debug.Log($"[Primed] {entityName} detonated '{effect.effectName}' " +
                      $"finalDamage={finalDamage}, threshold={effect.primeThreshold} ({effect.primeThresholdType})");

            // Apply each primed effect individually through the full pipeline
            foreach (StatusEffect primedEffectTemplate in effect.primedEffects)
            {
                // Deep copy so we never mutate the template
                StatusEffect newEffect = new StatusEffect
                {
                    effectName = primedEffectTemplate.effectName,
                    duration = primedEffectTemplate.duration,
                    applicationChance = primedEffectTemplate.applicationChance,
                    isNegativeEffect = primedEffectTemplate.isNegativeEffect,
                    statusResistanceBonus = primedEffectTemplate.statusResistanceBonus,

                    damageMultiplier = primedEffectTemplate.damageMultiplier,
                    defenseMultiplier = primedEffectTemplate.defenseMultiplier,
                    damageOverTime = primedEffectTemplate.damageOverTime,
                    temporaryDefenseBonus = primedEffectTemplate.temporaryDefenseBonus,
                    consumedOnHit = primedEffectTemplate.consumedOnHit,
                    statBoostType = primedEffectTemplate.statBoostType,
                    statBoostAmount = primedEffectTemplate.statBoostAmount,
                    lifestealPercent = primedEffectTemplate.lifestealPercent,

                    isRiposte = primedEffectTemplate.isRiposte,
                    riposteDamagePercent = primedEffectTemplate.riposteDamagePercent,
                    riposteFlatBonus = primedEffectTemplate.riposteFlatBonus,
                    riposteScalingStat = primedEffectTemplate.riposteScalingStat,
                    riposteScalingMultiplier = primedEffectTemplate.riposteScalingMultiplier,
                    riposteConsumedOnUse = primedEffectTemplate.riposteConsumedOnUse,

                    isStun = primedEffectTemplate.isStun,
                    isSilence = primedEffectTemplate.isSilence,
                    isBleed = primedEffectTemplate.isBleed,
                    bleedDamagePerTurn = primedEffectTemplate.bleedDamagePerTurn,
                    isBarrier = primedEffectTemplate.isBarrier,
                    barrierCurrentAmount = primedEffectTemplate.barrierMaxAmount,
                    barrierMaxAmount = primedEffectTemplate.barrierMaxAmount,
                    isMark = primedEffectTemplate.isMark,
                    markedDamageMultiplier = primedEffectTemplate.markedDamageMultiplier,
                    isTaunt = primedEffectTemplate.isTaunt,
                    tauntTargetEntityName = primedEffectTemplate.tauntTargetEntityName,
                    isCurse = primedEffectTemplate.isCurse,
                    healingReductionPercent = primedEffectTemplate.healingReductionPercent,
                    isExposed = primedEffectTemplate.isExposed,
                    exposedDefenseReduction = primedEffectTemplate.exposedDefenseReduction,
                    isEnrage = primedEffectTemplate.isEnrage,
                    enrageDamageMultiplier = primedEffectTemplate.enrageDamageMultiplier,
                    isHaste = primedEffectTemplate.isHaste,

                    // Nested primes are allowed but proc-chance and resist still apply
                    isPrimed = primedEffectTemplate.isPrimed,
                    primeThresholdType = primedEffectTemplate.primeThresholdType,
                    primeThreshold = primedEffectTemplate.primeThreshold,
                    primedEffects = primedEffectTemplate.primedEffects,            //new List<StatusEffect>(),
                    primedConsumedOnTrigger = primedEffectTemplate.primedConsumedOnTrigger,
                };

                // applicationChance roll for each payload effect
                if (newEffect.applicationChance < 1f && Random.value > newEffect.applicationChance)
                {
                    CombatLog.Instance?.AddEntry(
                        $"  ↳ {newEffect.effectName} failed to apply ({newEffect.applicationChance * 100:F0}% chance)"
                    );
                    continue;
                }

                // Goes through ApplyStatusEffect which handles resistance for negative effects
                ApplyStatusEffect(newEffect);

                CombatLog.Instance?.AddEntry($"  ↳ {entityName} is now {newEffect.effectName}!");
                Debug.Log($"[Primed] Applied payload '{newEffect.effectName}' to {entityName}");
            }

            // ── Consume the Primed effect if configured ────────────────────────
            if (effect.primedConsumedOnTrigger)
            {
                CombatLog.Instance?.AddEntry($"  ↳ {effect.effectName} was consumed.");
                activeEffects.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Returns true if the given Primed effect's threshold is met by
    /// <paramref name="finalDamage"/>.
    /// </summary>
    private bool IsPrimeThresholdMet(StatusEffect effect, int finalDamage, int healthBeforeHit)
    {
        switch (effect.primeThresholdType)
        {
            case PrimeThresholdType.FlatDamage:
                return finalDamage >= effect.primeThreshold;

            case PrimeThresholdType.PercentMaxHealth:
                // threshold is expressed as a whole-number percentage (e.g. 15 = 15%)
                float percentOfMax = (float)finalDamage / maxHealth * 100f;
                return percentOfMax >= effect.primeThreshold;

            case PrimeThresholdType.PercentCurrentHealth:
                // Guard against division by zero on near-dead entities
                if (healthBeforeHit <= 0) return false;
                float percentOfCurrent = (float)finalDamage / healthBeforeHit * 100f;
                return percentOfCurrent >= effect.primeThreshold;

            default:
                return false;
        }
    }

    /// <summary>
    /// Formats the Prime threshold for human-readable log output.
    /// </summary>
    private string FormatPrimeThreshold(StatusEffect effect)
    {
        switch (effect.primeThresholdType)
        {
            case PrimeThresholdType.FlatDamage:
                return $"{effect.primeThreshold} flat dmg";
            case PrimeThresholdType.PercentMaxHealth:
                return $"{effect.primeThreshold}% max HP";
            case PrimeThresholdType.PercentCurrentHealth:
                return $"{effect.primeThreshold}% current HP";
            default:
                return effect.primeThreshold.ToString();
        }
    }
}
