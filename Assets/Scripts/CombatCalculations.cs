using System.Collections.Generic;
using UnityEngine;
using static AbilityData;

/// <summary>
/// Enhanced combat calculations with dual-stat scaling support
/// FULLY BACKWARDS COMPATIBLE with existing abilities
/// </summary>
public static class CombatCalculations
{
    public static void ExecuteAbility(CombatEntity caster, CombatEntity target, AbilityData ability)
    {
        Debug.Log($"[CombatCalc] ExecuteAbility called: {caster.entityName} → {ability.abilityName} → {target.entityName}");


        ConsumeUpfrontResources(caster, ability);

        int hitCount = CombatCalculations.CalculateHitCount(caster, ability);
        int totalDamage = 0;

        Debug.Log($"[CombatCalc] Hit count: {hitCount}, Category: {ability.category}");

        if (ability.category == AbilityCategory.Damage)
        {
            Debug.Log($"[CombatCalc] Starting damage loop...");

            if (ability.isMultiHit && ability.multiHitTargetMode != MultiHitTargetMode.SameTarget)
            {
                for (int i = 0; i < hitCount; i++)
                {
                    CombatEntity randomTarget = GetRandomTarget(ability);

                    if (randomTarget != null && !randomTarget.isDead)
                    {
                        int damage = CalculateDamage(caster, randomTarget, ability);
                        Debug.Log($"[CombatCalc] Hit {i + 1}/{hitCount}: {damage} damage to {randomTarget.entityName}");

                        randomTarget.TakeDamage(damage, caster);
                        CheckAndApplyLifesteal(caster, damage);
                        ConsumeSneakAfterDamage(caster, ability);
                        totalDamage += damage;
                    }
                }
            }
            else
            {
                for (int i = 0; i < hitCount; i++)
                {
                    int damage = CalculateDamage(caster, target, ability);
                    Debug.Log($"[CombatCalc] Hit {i + 1}/{hitCount}: {damage} damage");

                    target.TakeDamage(damage, caster);
                    CheckAndApplyLifesteal(caster, damage);
                    ConsumeSneakAfterDamage(caster, ability);
                    totalDamage += damage;
                }
            }

            if (ability.consumeResourceAfterHits)
            {
                ConsumeMultiHitResources(caster, ability, hitCount);
            }

            CombatLog.Instance?.AddEntry($"{caster.entityName} hit {hitCount} times for {totalDamage} total damage!");
            Debug.Log($"[CombatCalc] Total damage dealt: {totalDamage}");
        }
        else if (ability.category == AbilityCategory.Heal)
        {
            Debug.Log($"[CombatCalc] Executing heal...");
            int healing = CalculateHealing(caster, ability);
            target.Heal(healing, caster);
        }
        else if (ability.category == AbilityCategory.Buff)
        {
            Debug.Log($"[CombatCalc] Executing buff...");
            ApplyBuff(caster, target, ability);
        }

        if (ability.grantsDefenseBoost)
        {
            ApplyDefenseBoost(caster, target, ability);
        }

        if (ability.grantsStatBoost)
        {
            ApplyStatBoost(caster, target, ability);
        }

        ApplyRiposte(caster, ability);

        if (ability.grantsLifesteal)
        {
            ApplyLifesteal(caster, target, ability);
        }

        GrantResources(caster, ability);

        foreach (StatusEffect effectTemplate in ability.appliesEffects)
        {
            // ── Proc-chance roll ──────────────────────────────────────────────
            if (effectTemplate.applicationChance < 1f)
            {
                float roll = Random.value; // 0.0 – 1.0
                if (roll > effectTemplate.applicationChance)
                {
                    CombatLog.Instance?.AddEntry(
                        $"{ability.abilityName} failed to apply {effectTemplate.effectName} " +
                        $"({effectTemplate.applicationChance * 100:F0}% chance)"
                    );
                    Debug.Log($"[StatusEffect] Proc failed: {effectTemplate.effectName} " +
                              $"roll={roll:F2} needed<={effectTemplate.applicationChance:F2}");
                    continue;
                }
            }

            StatusEffect newEffect = new StatusEffect
            {
                effectName = effectTemplate.effectName,
                duration = effectTemplate.duration,
                applicationChance = effectTemplate.applicationChance,

                isNegativeEffect = effectTemplate.isNegativeEffect,
                statusResistanceBonus = effectTemplate.statusResistanceBonus,

                damageMultiplier = effectTemplate.damageMultiplier,
                defenseMultiplier = effectTemplate.defenseMultiplier,
                damageOverTime = effectTemplate.damageOverTime,
                temporaryDefenseBonus = effectTemplate.temporaryDefenseBonus,

                baseDefenseAmount = effectTemplate.baseDefenseAmount,
                defenseScalingStat = effectTemplate.defenseScalingStat,
                defenseScalingMultiplier = effectTemplate.defenseScalingMultiplier,

                consumedOnHit = effectTemplate.consumedOnHit,
                statBoostType = effectTemplate.statBoostType,
                statBoostAmount = effectTemplate.statBoostAmount,
                lifestealPercent = effectTemplate.lifestealPercent,

                // Riposte
                isRiposte = effectTemplate.isRiposte,
                riposteDamagePercent = effectTemplate.riposteDamagePercent,
                riposteFlatBonus = effectTemplate.riposteFlatBonus,
                riposteScalingStat = effectTemplate.riposteScalingStat,
                riposteScalingMultiplier = effectTemplate.riposteScalingMultiplier,
                riposteConsumedOnUse = effectTemplate.riposteConsumedOnUse,

                // New effect types
                isStun = effectTemplate.isStun,
                isSilence = effectTemplate.isSilence,
                isBleed = effectTemplate.isBleed,
                bleedDamagePerTurn = effectTemplate.bleedDamagePerTurn,
                isBarrier = effectTemplate.isBarrier,
                barrierCurrentAmount = effectTemplate.barrierMaxAmount, // start full
                barrierMaxAmount = effectTemplate.barrierMaxAmount,
                isMark = effectTemplate.isMark,
                markedDamageMultiplier = effectTemplate.markedDamageMultiplier,
                isTaunt = effectTemplate.isTaunt,
                tauntTargetEntityName = effectTemplate.tauntTargetEntityName,
                isCurse = effectTemplate.isCurse,
                healingReductionPercent = effectTemplate.healingReductionPercent,
                isExposed = effectTemplate.isExposed,
                exposedDefenseReduction = effectTemplate.exposedDefenseReduction,
                isEnrage = effectTemplate.isEnrage,
                enrageDamageMultiplier = effectTemplate.enrageDamageMultiplier,
                isHaste = effectTemplate.isHaste,

                // Primed status effect
                isPrimed = effectTemplate.isPrimed,
                primeThresholdType = effectTemplate.primeThresholdType,
                primeThreshold = effectTemplate.primeThreshold,
                primedEffects = effectTemplate.primedEffects,   // reference is fine; TriggerPrimed deep-copies on detonation
                primedConsumedOnTrigger = effectTemplate.primedConsumedOnTrigger,
            };

            target.ApplyStatusEffect(newEffect);

            // Announce guaranteed procs with specific flavour
            if (effectTemplate.applicationChance >= 1f)
            {
                if (effectTemplate.isStun)
                    CombatLog.Instance?.AddEntry($"💫 {target.entityName} is STUNNED for {newEffect.duration} turn(s)!");
                else if (effectTemplate.isSilence)
                    CombatLog.Instance?.AddEntry($"🔇 {target.entityName} is SILENCED for {newEffect.duration} turn(s)!");
                else if (effectTemplate.isBleed)
                    CombatLog.Instance?.AddEntry($"🩸 {target.entityName} is BLEEDING ({newEffect.bleedDamagePerTurn}/turn) for {newEffect.duration} turn(s)!");
                else if (effectTemplate.isBarrier)
                    CombatLog.Instance?.AddEntry($"🛡 {target.entityName} gains a Barrier ({newEffect.barrierCurrentAmount} HP)!");
                else if (effectTemplate.isMark)
                    CombatLog.Instance?.AddEntry($"🎯 {target.entityName} is MARKED – takes {newEffect.markedDamageMultiplier * 100 - 100:F0}% more damage!");
                else if (effectTemplate.isTaunt)
                    CombatLog.Instance?.AddEntry($"😤 {target.entityName} is TAUNTED – forced to target {newEffect.tauntTargetEntityName}!");
                else if (effectTemplate.isCurse)
                    CombatLog.Instance?.AddEntry($"🖤 {target.entityName} is CURSED – healing reduced by {newEffect.healingReductionPercent * 100:F0}%!");
                else if (effectTemplate.isExposed)
                    CombatLog.Instance?.AddEntry($"💥 {target.entityName} is EXPOSED – -{newEffect.exposedDefenseReduction} DEF!");
                else if (effectTemplate.isEnrage)
                    CombatLog.Instance?.AddEntry($"😡 {target.entityName} is ENRAGED – +{(newEffect.enrageDamageMultiplier - 1f) * 100:F0}% damage, forced targeting!");
                else if (effectTemplate.isHaste)
                    CombatLog.Instance?.AddEntry($"⚡ {target.entityName} is HASTED – acts twice this turn!");
            }
            else
            {
                // Successful proc on a chance-based effect
                CombatLog.Instance?.AddEntry(
                    $"{ability.abilityName} applied {newEffect.effectName} " +
                    $"({effectTemplate.applicationChance * 100:F0}% proc)!"
                );
            }
        }

        // Update Ranger combo
        if (caster.characterClass == CharacterClass.Ranger)
        {
            UpdateRangerCombo(caster, ability);
        }

        if (ability.elementType != ElementType.None && caster.characterClass == CharacterClass.Mage)
        {
            MageChargeSystem chargeSystem = caster.GetComponent<MageChargeSystem>();
            if (chargeSystem != null)
            {
                chargeSystem.AddCharge(ability.elementType);
            }
        }

        Debug.Log($"[CombatCalc] ExecuteAbility complete!");
    }

    static int CalculateDamage(CombatEntity caster, CombatEntity target, AbilityData ability)
    {
        float totalDamage = 0f;

        int primaryStatValue = GetStatValue(caster, ability.scalingStat);
        float primaryScaling = primaryStatValue * ability.statMultiplier;
        totalDamage += primaryScaling;

        if (ability.HasSecondaryScaling())
        {
            int secondaryStatValue = GetStatValue(caster, ability.secondaryScalingStat);
            float secondaryScaling = secondaryStatValue * ability.secondaryStatMultiplier;
            totalDamage += secondaryScaling;

            // Log dual-stat calculation
            Debug.Log($"[Dual-Stat] {ability.abilityName}: " +
                     $"{primaryStatValue} {ability.scalingStat} × {ability.statMultiplier} = {primaryScaling:F1} + " +
                     $"{secondaryStatValue} {ability.secondaryScalingStat} × {ability.secondaryStatMultiplier} = {secondaryScaling:F1} " +
                     $"= {totalDamage:F1} total");
        }

        // Add base damage BEFORE sneak multiplier
        totalDamage += ability.baseDamage;

        totalDamage += caster.damageBonus;

        bool didCrit = false;
        if (ability.canCrit)
        {
            float critChance = 0.1f; // 10% base crit
            if (Random.value < critChance)
            {
                totalDamage *= 1.5f; // 150% damage
                didCrit = true;


                Debug.Log($"[Damage] CRITICAL HIT! {totalDamage} damage");

                // ✅ NEW: Show crit particle
                if (target != null && CombatVisualEffects.Instance != null)
                {
                    CombatVisualEffects.Instance.PlayCriticalEffect(target.transform.position);
                }
            }
        }

        if (ability.HasSneakScaling() && caster.characterClass == CharacterClass.Rogue)
        {
            int currentSneak = caster.sneakPoints;
            float sneakBonus = currentSneak * ability.sneakDamageMultiplier;
            float sneakMultiplier = 1f + sneakBonus;

            float damageBeforeSneak = totalDamage;
            totalDamage *= sneakMultiplier;

            CombatLog.Instance?.AddEntry(
                $"{caster.entityName} uses {currentSneak} sneak! " +
                $"Damage: {damageBeforeSneak:F0} × {sneakMultiplier:F2} = {totalDamage:F0}"
            );

            Debug.Log($"[Sneak Scaling] {ability.abilityName}: " +
                     $"{currentSneak} sneak × {ability.sneakDamageMultiplier:F2} = +{sneakBonus:F2} multiplier " +
                     $"({damageBeforeSneak:F0} → {totalDamage:F0})");
        }

        // Apply Ranger combo multiplier
        if (caster.characterClass == CharacterClass.Ranger && caster.comboCounter > 0)
        {
            float comboBonus = 1f + (caster.comboCounter * 0.2f); // +20% per combo
            totalDamage *= comboBonus;
        }

        // Apply status effect multipliers
        foreach (StatusEffect effect in caster.activeEffects)
        {
            totalDamage *= effect.damageMultiplier;

            if (effect.isEnrage)
            {
                totalDamage *= effect.enrageDamageMultiplier;
                Debug.Log($"[Enrage] {caster.entityName} enrage bonus: ×{effect.enrageDamageMultiplier}");
                break; // Only apply one enrage multiplier
            }
        }

        // TODO: Critical hits (if ability.canCrit)

        return Mathf.RoundToInt(totalDamage);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ✅ ENHANCED: HEALING CALCULATION WITH DUAL-STAT SCALING
    // ═══════════════════════════════════════════════════════════════════════
    static int CalculateHealing(CombatEntity caster, AbilityData ability)
    {
        float totalHealing = 0f;

        // PRIMARY STAT SCALING
        int primaryStatValue = GetStatValue(caster, ability.scalingStat);
        float primaryScaling = primaryStatValue * ability.statMultiplier;
        totalHealing += primaryScaling;

        // ✅ NEW: SECONDARY STAT SCALING (optional)
        if (ability.HasSecondaryScaling())
        {
            int secondaryStatValue = GetStatValue(caster, ability.secondaryScalingStat);
            float secondaryScaling = secondaryStatValue * ability.secondaryStatMultiplier;
            totalHealing += secondaryScaling;
        }

        // Add base healing (stored in baseDamage field)
        totalHealing += ability.baseDamage;

        return Mathf.RoundToInt(totalHealing);
    }

    static void ApplyBuff(CombatEntity caster, CombatEntity target, AbilityData ability)
    {
        if (target != null && CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayBuffEffect(target.transform.position);
        }

        Debug.Log($"[Buff] {caster.entityName} buffed {target.entityName}");

        // Buffs are handled through status effects in ability.appliesEffects
        CombatLog.Instance?.AddEntry($"{caster.entityName} buffed {target.entityName} with {ability.abilityName}!");
    }

    static int GetStatValue(CombatEntity entity, DamageStat stat)
    {
        switch (stat)
        {
            case DamageStat.None:
                return 0;
            case DamageStat.Strength:
                return entity.strength;
            case DamageStat.Dexterity:
                return entity.dexterity;
            case DamageStat.Intelligence:
                return entity.intelligence;
            case DamageStat.Constitution:
                return entity.constitution;
            case DamageStat.Willpower:
                return entity.GetBoostedWillpower(); // ✅ Use boosted version
            case DamageStat.Charisma:
                return entity.charisma;
            default:
                return entity.strength;
        }
    }

    static void ConsumeSneakAfterDamage(CombatEntity caster, AbilityData ability)
    {
        if (caster.characterClass != CharacterClass.Rogue) return;
        if (!ability.HasSneakScaling()) return;

        int sneakBefore = caster.sneakPoints;

        if (ability.consumesAllSneak)
        {
            // Consume ALL sneak points
            caster.sneakPoints = 0;
            caster.UpdateClassResourceBar();

            CombatLog.Instance?.AddEntry(
                $"{caster.entityName} consumed all {sneakBefore} sneak points!"
            );

            Debug.Log($"[Sneak Consumed] {ability.abilityName} consumed ALL sneak: {sneakBefore} → 0");
        }
        else if (ability.consumeSneakAmount > 0)
        {
            // Consume specific amount
            int consumed = Mathf.Min(ability.consumeSneakAmount, caster.sneakPoints);
            caster.sneakPoints -= consumed;
            caster.UpdateClassResourceBar();

            CombatLog.Instance?.AddEntry(
                $"{caster.entityName} consumed {consumed} sneak points!"
            );

            Debug.Log($"[Sneak Consumed] {ability.abilityName} consumed {consumed} sneak: " +
                     $"{sneakBefore} → {caster.sneakPoints}");
        }

        // Clamp to valid range
        caster.sneakPoints = Mathf.Clamp(caster.sneakPoints, 0, 6);
        caster.UpdateClassResourceBar();
    }

    static void ConsumeUpfrontResources(CombatEntity caster, AbilityData ability)
    {
        switch (caster.characterClass)
        {
            case CharacterClass.Rogue:
                // Only consume sneakCost upfront (NOT for sneak scaling)
                // Sneak scaling consumption happens AFTER damage
                if (ability.sneakCost > 0)
                {
                    caster.sneakPoints -= ability.sneakCost;
                    caster.sneakPoints = Mathf.Clamp(caster.sneakPoints, 0, 6);
                    caster.UpdateClassResourceBar();
                }
                break;

            case CharacterClass.Fighter:
                // Fighter abilities may have cooldowns but no direct resource cost
                // Stance changes are handled separately
                break;

            case CharacterClass.Mage:
                int manaCost = ability.manaCost;

                if (caster.viewerData != null)
                {
                    float reduction = caster.viewerData.equipped.GetTotalManaCostReduction();
                    if (reduction > 0)
                    {
                        int reducedCost = Mathf.RoundToInt(manaCost * (1f - reduction));
                        int savedMana = manaCost - reducedCost;

                        CombatLog.Instance?.AddEntry(
                            $"{caster.entityName} saved {savedMana} mana ({reduction * 100:F0}% reduction)"
                        );

                        manaCost = reducedCost;
                    }
                }

                caster.mana -= manaCost;
                caster.mana = Mathf.Clamp(caster.mana, 0, 100);
                caster.UpdateClassResourceBar();
                break;

            case CharacterClass.Cleric:
                caster.wrath -= ability.wrathCost;
                caster.wrath = Mathf.Clamp(caster.wrath, 0, 100);
                caster.UpdateClassResourceBar();
                break;

            case CharacterClass.Ranger:
                caster.balance -= ability.balanceCost;
                caster.balance = Mathf.Clamp(caster.balance, -10, 10);
                caster.UpdateClassResourceBar();
                break;
        }
    }

    static void GrantResources(CombatEntity caster, AbilityData ability)
    {
        switch (caster.characterClass)
        {
            case CharacterClass.Rogue:
                caster.sneakPoints += ability.sneakGain;
                caster.sneakPoints = Mathf.Clamp(caster.sneakPoints, 0, 6);
                caster.UpdateClassResourceBar();
                break;

            case CharacterClass.Mage:
                // Mage gains mana per turn, not per ability
                break;

            case CharacterClass.Cleric:
                caster.wrath += ability.wrathGain;
                caster.wrath = Mathf.Clamp(caster.wrath, 0, 100);
                caster.UpdateClassResourceBar();
                break;

            case CharacterClass.Ranger:
                caster.balance += ability.balanceGain;
                caster.balance = Mathf.Clamp(caster.balance, -10, 10);
                caster.UpdateClassResourceBar();
                break;
        }
    }

    public static int CalculateHitCount(CombatEntity actor, AbilityData ability)
    {
        if (!ability.isMultiHit)
            return 1; // Single hit

        int hits = ability.baseHitCount;

        switch (ability.multiHitType)
        {
            case MultiHitType.None:
                // Just use base count
                break;

            case MultiHitType.PerSneakPoint:
                if (actor.GetCharacterClass() == CharacterClass.Rogue)
                {
                    hits = actor.sneakPoints;
                }
                break;

            case MultiHitType.PerBalancePoint:
                if (actor.GetCharacterClass() == CharacterClass.Ranger)
                {
                    int absBalance = Mathf.Abs(actor.balance);
                    hits = absBalance / ability.resourcePerHit;
                }
                break;

            case MultiHitType.IfAggressive:
                if (actor.GetCharacterClass() == CharacterClass.Fighter)
                {
                    if (actor.currentStance == FighterStance.Aggressive)
                    {
                        hits++; // Add 1 extra hit
                    }
                }
                break;

            case MultiHitType.PerWrathTier:
                if (actor.GetCharacterClass() == CharacterClass.Cleric)
                {
                    // Example: 1 hit per 25 wrath
                    hits = actor.wrath / 25;
                }
                break;
        }

        // Apply limits
        hits = Mathf.Max(hits, ability.baseHitCount); // Never below base
        hits = Mathf.Min(hits, ability.maxHitCount);   // Never above max

        return hits;
    }

    static void ConsumeMultiHitResources(CombatEntity caster, AbilityData ability, int hitCount)
    {
        switch (ability.multiHitType)
        {
            case MultiHitType.PerSneakPoint:
                caster.sneakPoints = 0; // Consume all sneak
                caster.UpdateClassResourceBar();
                break;

            case MultiHitType.PerBalancePoint:
                // Shift balance toward neutral
                int consumed = hitCount * ability.resourcePerHit;
                if (caster.balance > 0)
                {
                    caster.balance = Mathf.Max(0, caster.balance - consumed);
                    caster.UpdateClassResourceBar();
                }
                else if (caster.balance < 0)
                {
                    caster.balance = Mathf.Min(0, caster.balance + consumed);
                    caster.UpdateClassResourceBar();
                }
                break;

                // Others don't consume resources
        }
    }

    /// <summary>
    /// Check if caster has lifesteal and heal them based on damage dealt
    /// </summary>
    static void CheckAndApplyLifesteal(CombatEntity caster, int damageDealt)
    {
        if (caster == null || caster.isDead) return;

        float totalLifestealPercent = 0f;

        // Check all active effects for lifesteal
        foreach (StatusEffect effect in caster.activeEffects)
        {
            if (effect.lifestealPercent > 0f)
            {
                totalLifestealPercent += effect.lifestealPercent;
            }
        }

        if (totalLifestealPercent > 0f)
        {
            int healAmount = Mathf.RoundToInt(damageDealt * totalLifestealPercent);
            healAmount = Mathf.Max(1, healAmount); // Minimum 1 HP heal

            // Don't overheal
            int actualHeal = Mathf.Min(healAmount, caster.maxHealth - caster.currentHealth);

            if (actualHeal > 0)
            {
                caster.Heal(actualHeal, caster);
                CombatLog.Instance?.AddEntry($"{caster.entityName} drained {actualHeal} HP! ({totalLifestealPercent * 100:F0}% lifesteal)");

                Debug.Log($"[Lifesteal] {caster.entityName} healed {actualHeal} HP from {damageDealt} damage ({totalLifestealPercent * 100:F0}%)");
            }
        }
    }

    /// <summary>
    /// Apply lifesteal buff to target
    /// </summary>
    static void ApplyLifesteal(CombatEntity caster, CombatEntity target, AbilityData ability)
    {
        if (!ability.grantsLifesteal || ability.lifestealPercent <= 0f) return;

        StatusEffect lifestealBuff = new StatusEffect
        {
            effectName = "Vampiric",
            duration = ability.lifestealDuration,
            lifestealPercent = ability.lifestealPercent
        };

        target.ApplyStatusEffect(lifestealBuff);

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayBuffEffect(target.transform.position);
        }

        string durationText = ability.lifestealDuration == 1 ? "for 1 turn" : $"for {ability.lifestealDuration} turns";
        CombatLog.Instance?.AddEntry($"{target.entityName} gained {ability.lifestealPercent * 100:F0}% lifesteal {durationText}!");
    }

    /// <summary>
    /// If the ability grants a Riposte, build a StatusEffect and apply it to the
    /// caster so that CombatEntity.TakeDamage can fire the counter automatically.
    /// Call this from ExecuteAbility() alongside ApplyDefenseBoost / ApplyStatBoost.
    /// </summary>
    static void ApplyRiposte(CombatEntity caster, AbilityData ability)
    {
        if (!ability.HasRiposte()) return;

        StatusEffect riposte = new StatusEffect
        {
            effectName = $"Riposte ({ability.abilityName})",
            duration = ability.riposteDuration,

            // Counter-attack payload – read by CombatEntity.TakeDamage
            isRiposte = true,
            riposteDamagePercent = ability.riposteDamagePercent,
            riposteFlatBonus = ability.riposteFlatBonus,
            riposteScalingStat = ability.riposteScalingStat,
            riposteScalingMultiplier = ability.riposteScalingMultiplier,
            riposteConsumedOnUse = ability.riposteConsumedOnUse,

            // Leave regular effect fields at neutral defaults
            damageMultiplier = 1f,
            defenseMultiplier = 1f,
        };

        caster.ApplyStatusEffect(riposte);
        CombatLog.Instance?.AddEntry($"⚔️ {caster.entityName} is ready to Riposte!");
        Debug.Log($"[CombatCalc] Riposte applied to {caster.entityName} " +
                  $"({ability.riposteDamagePercent * 100:F0}% reflect, {ability.riposteFlatBonus} flat, " +
                  $"{ability.riposteDuration} turn(s))");
    }

    /// <summary>
    /// Get a random target based on ability's target mode
    /// </summary>
    static CombatEntity GetRandomTarget(AbilityData ability)
    {
        List<CombatEntity> enemies = ExpeditionManager.Instance.GetAllEnemyEntities();

        if (enemies.Count == 0) return null;

        if (ability.multiHitTargetMode == MultiHitTargetMode.RandomInRange)
        {
            // Filter by position range
            List<CombatEntity> validTargets = enemies.FindAll(e =>
                e.position >= ability.minTargetPosition &&
                e.position <= ability.maxTargetPosition
            );

            if (validTargets.Count == 0) return null;

            return validTargets[Random.Range(0, validTargets.Count)];
        }
        else // TrulyRandom
        {
            // Any alive enemy
            return enemies[Random.Range(0, enemies.Count)];
        }
    }

    static void UpdateRangerCombo(CombatEntity caster, AbilityData ability)
    {
        // Check if this is a melee or ranged ability
        bool isMelee = ability.balanceCost > 0; // Melee abilities use balance
        bool isRanged = ability.balanceGain > 0; // Ranged abilities gain balance

        if (isMelee && !caster.lastAttackWasMelee)
        {
            // Correctly alternated: ranged -> melee
            caster.comboCounter++;
            caster.lastAttackWasMelee = true;
        }
        else if (isRanged && caster.lastAttackWasMelee)
        {
            // Correctly alternated: melee -> ranged
            caster.comboCounter++;
            caster.lastAttackWasMelee = false;
        }
        else
        {
            // Didn't alternate, reset combo
            caster.comboCounter = 0;
            caster.lastAttackWasMelee = isMelee;
        }

        if (caster.comboCounter > 0)
        {
            CombatLog.Instance?.AddEntry($"{caster.entityName} combo: {caster.comboCounter}x!");
        }
    }

    public static void RegenerateManaPerTurn(CombatEntity caster)
    {
        if (caster.characterClass != CharacterClass.Mage) return;

        // Mage regenerates mana based on INT
        int manaGain = Mathf.FloorToInt(caster.intelligence * 0.1f); // 10% of INT per turn
        caster.mana += manaGain;
        caster.mana = Mathf.Clamp(caster.mana, 0, 100);
        caster.UpdateClassResourceBar();

        if (manaGain > 0)
        {
            CombatLog.Instance?.AddEntry($"{caster.entityName} regenerated {manaGain} mana.");
        }
    }

    public static void GrantClericWrathFromDamage(CombatEntity cleric, int damageReceived)
    {
        if (cleric.characterClass != CharacterClass.Cleric) return;

        // Cleric gains wrath when allies are hit
        int wrathGain = Mathf.FloorToInt(damageReceived * 0.5f); // 50% of damage taken
        cleric.wrath += wrathGain;
        cleric.wrath = Mathf.Clamp(cleric.wrath, 0, 100);

        if (wrathGain > 0)
        {
            CombatLog.Instance?.AddEntry($"{cleric.entityName} gained {wrathGain} wrath.");
        }
    }

    /// <summary>
    /// Apply temporary defense boost (can scale with caster's stats)
    /// Example: Brace gives 0 base + 1.5x CON defense
    /// </summary>
    static void ApplyDefenseBoost(CombatEntity caster, CombatEntity target, AbilityData ability)
    {
        int defenseAmount = ability.baseDefenseBoost;

        if (ability.DefenseBoostScales())
        {
            int statValue = GetStatValue(caster, ability.defenseScalingStat);
            int scaledBonus = Mathf.RoundToInt(statValue * ability.defenseScalingMultiplier);
            defenseAmount += scaledBonus;

            Debug.Log($"[CombatCalc] Defense scaling: {ability.baseDefenseBoost} base + " +
                      $"({statValue} {ability.defenseScalingStat} × {ability.defenseScalingMultiplier}) = {defenseAmount}");
        }

        // Ensure minimum of 1 if boost is granted
        defenseAmount = Mathf.Max(1, defenseAmount);

        StatusEffect defenseBoost = new StatusEffect
        {
            effectName = ability.defenseConsumedOnHit ? "Brace" : "Defense Up",
            duration = ability.defenseConsumedOnHit ? 999 : 1,
            temporaryDefenseBonus = defenseAmount,
            consumedOnHit = ability.defenseConsumedOnHit
        };

        target.ApplyStatusEffect(defenseBoost);

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayBuffEffect(target.transform.position);
        }

        string durationText = ability.defenseConsumedOnHit ? "vs next attack" : "for 1 turn";
        CombatLog.Instance?.AddEntry($"{target.entityName} gained +{defenseAmount} defense {durationText}!");
    }

    /// <summary>
    /// Apply temporary stat boost (can scale with caster's stats)
    /// Example: Battle Cry gives 5 base + 0.5x CHA strength boost
    /// </summary>
    static void ApplyStatBoost(CombatEntity caster, CombatEntity target, AbilityData ability)
    {
        if (ability.statToBoost == BoostableStat.None) return;

        int boostAmount = ability.baseStatBoost;

        if (ability.StatBoostScales())
        {
            int statValue = GetStatValue(caster, ability.statBoostScalingStat);
            int scaledBonus = Mathf.RoundToInt(statValue * ability.statBoostScalingMultiplier);
            boostAmount += scaledBonus;

            Debug.Log($"[CombatCalc] Stat boost scaling: {ability.baseStatBoost} base + " +
                      $"({statValue} {ability.statBoostScalingStat} × {ability.statBoostScalingMultiplier}) = {boostAmount}");
        }

        // Ensure minimum of 1 if boost is granted
        boostAmount = Mathf.Max(1, boostAmount);

        StatusEffect statBoost = new StatusEffect
        {
            effectName = $"{ability.statToBoost} Boost",
            duration = ability.statBoostDuration,
            statBoostType = ability.statToBoost,
            statBoostAmount = boostAmount
        };

        target.ApplyStatusEffect(statBoost);

        string durationText = ability.statBoostDuration == 1 ? "for 1 turn" : $"for {ability.statBoostDuration} turns";
        CombatLog.Instance?.AddEntry($"{target.entityName} gained +{boostAmount} {ability.statToBoost} {durationText}!");
    }
}
