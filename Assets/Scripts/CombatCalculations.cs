using UnityEngine;

/// <summary>
/// Handles all combat damage, healing, and resource calculations
/// </summary>
public static class CombatCalculations
{
    public static void ExecuteAbility(CombatEntity caster, CombatEntity target, AbilityData ability)
    {
        // Consume resources
        ConsumeResources(caster, ability);

        // Apply damage or healing
        if (ability.category == AbilityCategory.Damage)
        {
            int damage = CalculateDamage(caster, target, ability);
            target.TakeDamage(damage, caster);
        }
        else if (ability.category == AbilityCategory.Heal)
        {
            int healing = CalculateHealing(caster, ability);
            target.Heal(healing, caster);
        }
        else if (ability.category == AbilityCategory.Buff)
        {
            ApplyBuff(caster, target, ability);
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
    }

    static int CalculateDamage(CombatEntity caster, CombatEntity target, AbilityData ability)
    {
        // Get base stat value
        int statValue = GetStatValue(caster, ability.scalingStat);

        // Calculate base damage
        float damage = (statValue * ability.statMultiplier) + ability.baseDamage;

        // Apply Ranger combo multiplier
        if (caster.characterClass == CharacterClass.Ranger && caster.comboCounter > 0)
        {
            float comboBonus = 1f + (caster.comboCounter * 0.2f); // +20% per combo
            damage *= comboBonus;
        }

        // Apply status effect multipliers
        foreach (StatusEffect effect in caster.activeEffects)
        {
            damage *= effect.damageMultiplier;
        }

        // TODO: Critical hits (if ability.canCrit)

        return Mathf.RoundToInt(damage);
    }

    static int CalculateHealing(CombatEntity caster, AbilityData ability)
    {
        int statValue = GetStatValue(caster, ability.scalingStat);
        float healing = (statValue * ability.statMultiplier) + ability.baseDamage; // Using baseDamage field for healing amount

        return Mathf.RoundToInt(healing);
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
            case DamageStat.Strength:
                return entity.strength;
            case DamageStat.Dexterity:
                return entity.dexterity;
            case DamageStat.Intelligence:
                return entity.intelligence;
            case DamageStat.Constitution:
                return entity.constitution;
            default:
                return entity.strength;
        }
    }

    static void ConsumeResources(CombatEntity caster, AbilityData ability)
    {
        switch (caster.characterClass)
        {
            case CharacterClass.Rogue:
                caster.sneakPoints -= ability.sneakCost;
                caster.sneakPoints = Mathf.Clamp(caster.sneakPoints, 0, 6);
                break;

            case CharacterClass.Fighter:
                // Fighter abilities may have cooldowns but no direct resource cost
                // Stance changes are handled separately
                break;

            case CharacterClass.Mage:
                caster.mana -= ability.manaCost;
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
}
