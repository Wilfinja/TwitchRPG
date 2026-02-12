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

        // Consume upfront resources
        ConsumeUpfrontResources(caster, ability);

        int hitCount = CombatCalculations.CalculateHitCount(caster, ability);
        int totalDamage = 0;

        Debug.Log($"[CombatCalc] Hit count: {hitCount}, Category: {ability.category}");

        // Apply damage or healing
        if (ability.category == AbilityCategory.Damage)
        {
            Debug.Log($"[CombatCalc] Starting damage loop...");

            for (int i = 0; i < hitCount; i++)
            {
                int damage = CalculateDamage(caster, target, ability);
                Debug.Log($"[CombatCalc] Hit {i + 1}/{hitCount}: {damage} damage");

                target.TakeDamage(damage, caster);
                ConsumeSneakAfterDamage(caster, ability);
                totalDamage += damage;
            }

            if (ability.consumeResourceAfterHits)
            {
                ConsumeMultiHitResources(caster, ability, hitCount);
            }

            // ✅ FIX: Add null-safe operator
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

        // ✅ UPDATED: Pass caster to defense boost
        if (ability.grantsDefenseBoost)
        {
            ApplyDefenseBoost(caster, target, ability);
        }

        // ✅ UPDATED: Pass caster to stat boost
        if (ability.grantsStatBoost)
        {
            ApplyStatBoost(caster, target, ability);
        }

        // Grant resources
        GrantResources(caster, ability);

        // Apply status effects
        foreach (StatusEffect effectTemplate in ability.appliesEffects)
        {
            StatusEffect newEffect = new StatusEffect
            {
                effectName = effectTemplate.effectName,
                duration = effectTemplate.duration,
                damageMultiplier = effectTemplate.damageMultiplier,
                defenseMultiplier = effectTemplate.defenseMultiplier,
                damageOverTime = effectTemplate.damageOverTime
            };
            target.ApplyStatusEffect(newEffect);
        }

        // Update Ranger combo
        if (caster.characterClass == CharacterClass.Ranger)
        {
            UpdateRangerCombo(caster, ability);
        }

        Debug.Log($"[CombatCalc] ExecuteAbility complete!");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ✅ ENHANCED: DAMAGE CALCULATION WITH DUAL-STAT AND SNEAK SCALING
    // ═══════════════════════════════════════════════════════════════════════
    static int CalculateDamage(CombatEntity caster, CombatEntity target, AbilityData ability)
    {
        float totalDamage = 0f;

        // PRIMARY STAT SCALING (always present - backwards compatible)
        int primaryStatValue = GetStatValue(caster, ability.scalingStat);
        float primaryScaling = primaryStatValue * ability.statMultiplier;
        totalDamage += primaryScaling;

        // ✅ SECONDARY STAT SCALING (optional)
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

    // ═══════════════════════════════════════════════════════════════════════
    // ✅ NEW: CONSUME SNEAK AFTER DAMAGE (separate from upfront costs)
    // ═══════════════════════════════════════════════════════════════════════
    static void ConsumeSneakAfterDamage(CombatEntity caster, AbilityData ability)
    {
        if (caster.characterClass != CharacterClass.Rogue) return;
        if (!ability.HasSneakScaling()) return;

        int sneakBefore = caster.sneakPoints;

        if (ability.consumesAllSneak)
        {
            // Consume ALL sneak points
            caster.sneakPoints = 0;

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

            CombatLog.Instance?.AddEntry(
                $"{caster.entityName} consumed {consumed} sneak points!"
            );

            Debug.Log($"[Sneak Consumed] {ability.abilityName} consumed {consumed} sneak: " +
                     $"{sneakBefore} → {caster.sneakPoints}");
        }

        // Clamp to valid range
        caster.sneakPoints = Mathf.Clamp(caster.sneakPoints, 0, 6);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ✅ RENAMED: UPFRONT RESOURCE CONSUMPTION (checked before damage)
    // ═══════════════════════════════════════════════════════════════════════
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
                }
                break;

            case CharacterClass.Fighter:
                // Fighter abilities may have cooldowns but no direct resource cost
                // Stance changes are handled separately
                break;

            case CharacterClass.Mage:
                int manaCost = ability.manaCost;

                // ✅ NEW: Apply cost reduction from equipment
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
                break;

            case CharacterClass.Cleric:
                caster.wrath -= ability.wrathCost;
                caster.wrath = Mathf.Clamp(caster.wrath, 0, 100);
                break;

            case CharacterClass.Ranger:
                caster.balance -= ability.balanceCost;
                caster.balance = Mathf.Clamp(caster.balance, -10, 10);
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
                break;

            case CharacterClass.Mage:
                // Mage gains mana per turn, not per ability
                break;

            case CharacterClass.Cleric:
                caster.wrath += ability.wrathGain;
                caster.wrath = Mathf.Clamp(caster.wrath, 0, 100);
                break;

            case CharacterClass.Ranger:
                caster.balance += ability.balanceGain;
                caster.balance = Mathf.Clamp(caster.balance, -10, 10);
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
                break;

            case MultiHitType.PerBalancePoint:
                // Shift balance toward neutral
                int consumed = hitCount * ability.resourcePerHit;
                if (caster.balance > 0)
                {
                    caster.balance = Mathf.Max(0, caster.balance - consumed);
                }
                else if (caster.balance < 0)
                {
                    caster.balance = Mathf.Min(0, caster.balance + consumed);
                }
                break;

                // Others don't consume resources
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
