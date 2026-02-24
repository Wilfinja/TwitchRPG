using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Component attached to mage CombatEntity to track elemental charges and trigger combos
/// Automatically added to mages during combat initialization
/// </summary>
public class MageChargeSystem : MonoBehaviour
{
    [Header("Charge State")]
    public ElementalCharge[] charges = new ElementalCharge[4];
    public int currentChargeCount = 0;
    public const int MAX_CHARGES = 4;

    [Header("References")]
    private CombatEntity combatEntity;
    private MageChargeUI chargeUI;

    [Header("Combo Tracking")]
    public bool comboReady = false;
    private MageComboData pendingCombo;

    void Awake()
    {
        combatEntity = GetComponent<CombatEntity>();

        // Initialize empty charge array
        for (int i = 0; i < MAX_CHARGES; i++)
        {
            charges[i] = null;
        }
    }

    void Start()
    {
        // Try to find or create the charge UI
        chargeUI = GetComponent<MageChargeUI>();
        if (chargeUI == null)
        {
            chargeUI = gameObject.AddComponent<MageChargeUI>();
        }

        chargeUI.Initialize(this);
    }

    /// <summary>
    /// Add a new elemental charge when an ability is used
    /// Automatically triggers combo if 4 charges are reached
    /// </summary>
    public void AddCharge(ElementType element)
    {
        if (element == ElementType.None)
        {
            Debug.LogWarning($"[MageCharges] Attempted to add None element charge");
            return;
        }

        if (currentChargeCount >= MAX_CHARGES)
        {
            Debug.LogWarning($"[MageCharges] {combatEntity.entityName} already at max charges!");
            return;
        }

        // Add the charge
        charges[currentChargeCount] = new ElementalCharge(element);
        currentChargeCount++;

        Debug.Log($"[MageCharges] {combatEntity.entityName} gained {element} charge ({currentChargeCount}/{MAX_CHARGES})");

        // Update UI
        chargeUI?.UpdateDisplay();

        // Show notification
        OnScreenNotification.Instance?.ShowNotification(
            $"{combatEntity.entityName} gained {element} charge! ({currentChargeCount}/4)"
        );

        // Check if we hit 4 charges
        if (currentChargeCount == MAX_CHARGES)
        {
            CheckForCombo();
        }
    }

    /// <summary>
    /// Check if the current 4 charges form a valid combo and trigger it
    /// </summary>
    void CheckForCombo()
    {
        if (MageComboDatabase.Instance == null)
        {
            Debug.LogWarning("[MageCharges] MageComboDatabase not found! Cannot trigger combo.");
            ClearCharges();
            return;
        }

        // Look up combo
        pendingCombo = MageComboDatabase.Instance.GetCombo(charges);

        if (pendingCombo != null)
        {
            // Valid combo found!
            string comboName = ChargeComboHelper.GetComboDisplayName(charges);
            Debug.Log($"[MageCharges] COMBO TRIGGERED: {pendingCombo.comboName} ({comboName})");

            OnScreenNotification.Instance?.ShowNotification(
                $"🔥 {combatEntity.entityName} unleashed {pendingCombo.comboName}! 🔥"
            );

            // Trigger the combo effect immediately
            StartCoroutine(ExecuteCombo());
        }
        else
        {
            // No combo for this combination
            string comboName = ChargeComboHelper.GetComboDisplayName(charges);
            Debug.Log($"[MageCharges] No combo found for: {comboName}");

            OnScreenNotification.Instance?.ShowNotification(
                $"{combatEntity.entityName}'s charges fizzled... (No combo for {comboName})"
            );

            // Clear charges without effect
            ClearCharges();
        }
    }

    /// <summary>
    /// Execute the combo effect based on the MageComboData
    /// </summary>
    IEnumerator ExecuteCombo()
    {
        if (pendingCombo == null)
        {
            ClearCharges();
            yield break;
        }

        // Add to combat log
        CombatLog.Instance?.AddEntry($"⚡ {combatEntity.entityName} unleashed {pendingCombo.comboName}!");

        // Spawn particle effect at mage's position
        if (pendingCombo.particleEffect != null)
        {
            GameObject particles = Instantiate(
                pendingCombo.particleEffect,
                combatEntity.transform.position,
                Quaternion.identity
            );
            Destroy(particles, 3f);
        }

        yield return new WaitForSeconds(0.3f);

        // Determine targets based on combo target type
        List<CombatEntity> targets = GetComboTargets(pendingCombo);

        // Apply effects to each target
        foreach (CombatEntity target in targets)
        {
            if (target == null || target.isDead) continue;

            switch (pendingCombo.effectType)
            {
                case ComboEffectType.Damage:
                    ApplyComboDamage(target, pendingCombo);
                    break;

                case ComboEffectType.Healing:
                    ApplyComboHealing(target, pendingCombo);
                    break;

                case ComboEffectType.Buff:
                case ComboEffectType.Debuff:
                    ApplyComboStatusEffects(target, pendingCombo);
                    break;

                case ComboEffectType.Mixed:
                    // Apply both damage and status effects
                    ApplyComboDamage(target, pendingCombo);
                    ApplyComboStatusEffects(target, pendingCombo);
                    break;
            }

            // Grant defense boost if combo specifies it
            if (pendingCombo.grantsDefenseBoost)
            {
                ApplyDefenseBoost(target, pendingCombo);
            }

            yield return new WaitForSeconds(0.1f);
        }

        // Clear charges after combo executes
        ClearCharges();
    }

    /// <summary>
    /// Get the list of targets based on combo target type
    /// </summary>
    List<CombatEntity> GetComboTargets(MageComboData combo)
    {
        List<CombatEntity> targets = new List<CombatEntity>();

        switch (combo.targetType)
        {
            case ComboTargetType.AllEnemies:
                targets = ExpeditionManager.Instance.GetAllEnemyEntities();
                break;

            case ComboTargetType.AllAllies:
                targets = ExpeditionManager.Instance.GetAllPlayerEntities();
                break;

            case ComboTargetType.SingleEnemy:
                // Target the front-most enemy
                CombatEntity frontEnemy = ExpeditionManager.Instance.GetFrontmostEnemy();
                if (frontEnemy != null) targets.Add(frontEnemy);
                break;

            case ComboTargetType.FrontEnemies:
                // Get front X enemies based on targetCount
                List<CombatEntity> allEnemies = ExpeditionManager.Instance.GetAllEnemyEntities();
                for (int i = 0; i < Mathf.Min(combo.targetCount, allEnemies.Count); i++)
                {
                    targets.Add(allEnemies[i]);
                }
                break;

            case ComboTargetType.Self:
                targets.Add(combatEntity);
                break;
        }

        return targets;
    }

    /// <summary>
    /// Apply damage from a combo
    /// </summary>
    void ApplyComboDamage(CombatEntity target, MageComboData combo)
    {
        int damage = combo.basePower;

        // Add stat scaling
        if (combo.scalingStat != DamageStat.None)
        {
            int statValue = GetStatValue(combatEntity, combo.scalingStat);
            damage += Mathf.RoundToInt(statValue * combo.statMultiplier);
        }

        // Apply damage
        target.TakeDamage(damage, combatEntity);

        Debug.Log($"[MageCombo] {combo.comboName} dealt {damage} damage to {target.entityName}");
    }

    /// <summary>
    /// Apply healing from a combo
    /// </summary>
    void ApplyComboHealing(CombatEntity target, MageComboData combo)
    {
        int healing = combo.basePower;

        // Add stat scaling
        if (combo.scalingStat != DamageStat.None)
        {
            int statValue = GetStatValue(combatEntity, combo.scalingStat);
            healing += Mathf.RoundToInt(statValue * combo.statMultiplier);
        }

        // Apply healing
        target.Heal(healing, combatEntity);

        Debug.Log($"[MageCombo] {combo.comboName} healed {target.entityName} for {healing}");
    }

    /// <summary>
    /// Apply status effects from a combo
    /// </summary>
    void ApplyComboStatusEffects(CombatEntity target, MageComboData combo)
    {
        foreach (StatusEffect effectTemplate in combo.appliedEffects)
        {
            StatusEffect newEffect = new StatusEffect
            {
                effectName = effectTemplate.effectName,
                duration = effectTemplate.duration,
                damageMultiplier = effectTemplate.damageMultiplier,
                defenseMultiplier = effectTemplate.defenseMultiplier,
                damageOverTime = effectTemplate.damageOverTime,
                temporaryDefenseBonus = effectTemplate.temporaryDefenseBonus,
                consumedOnHit = effectTemplate.consumedOnHit,
                statBoostType = effectTemplate.statBoostType,
                statBoostAmount = effectTemplate.statBoostAmount,

                isNegativeEffect = effectTemplate.isNegativeEffect,
                statusResistanceBonus = effectTemplate.statusResistanceBonus,
            };

            target.ApplyStatusEffect(newEffect);
            Debug.Log($"[MageCombo] Applied {newEffect.effectName} to {target.entityName}");
        }
    }

    /// <summary>
    /// Apply defense boost from a combo
    /// </summary>
    void ApplyDefenseBoost(CombatEntity target, MageComboData combo)
    {
        StatusEffect defenseEffect = new StatusEffect
        {
            effectName = $"{combo.comboName} Shield",
            duration = combo.defenseBoostDuration,
            temporaryDefenseBonus = combo.defenseBoostAmount,
            damageMultiplier = 1f,
            defenseMultiplier = 1f
        };

        target.ApplyStatusEffect(defenseEffect);
        Debug.Log($"[MageCombo] Granted {combo.defenseBoostAmount} defense to {target.entityName} for {combo.defenseBoostDuration} turns");
    }

    /// <summary>
    /// Get stat value from combat entity
    /// </summary>
    int GetStatValue(CombatEntity entity, DamageStat stat)
    {
        switch (stat)
        {
            case DamageStat.Strength: return entity.strength;
            case DamageStat.Dexterity: return entity.dexterity;
            case DamageStat.Intelligence: return entity.intelligence;
            case DamageStat.Constitution: return entity.constitution;
            case DamageStat.Willpower: return entity.willpower;
            case DamageStat.Charisma: return entity.charisma;
            default: return 0;
        }
    }

    /// <summary>
    /// Clear all charges
    /// </summary>
    public void ClearCharges()
    {
        for (int i = 0; i < MAX_CHARGES; i++)
        {
            charges[i] = null;
        }
        currentChargeCount = 0;
        comboReady = false;
        pendingCombo = null;

        chargeUI?.UpdateDisplay();

        Debug.Log($"[MageCharges] {combatEntity.entityName}'s charges cleared");
    }

    /// <summary>
    /// Get a specific charge (for UI display)
    /// </summary>
    public ElementalCharge GetCharge(int index)
    {
        if (index < 0 || index >= MAX_CHARGES)
            return null;

        return charges[index];
    }
}
