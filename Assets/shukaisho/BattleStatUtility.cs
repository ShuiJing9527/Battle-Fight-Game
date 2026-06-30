using UnityEngine;

public static class BattleStatUtility
{
    private const float BaseEvasionChance = 0.05f;
    private const float SpeedMoveBonusPerPoint = 0.0075f;
    private const float SpeedCooldownBonusPerPoint = 0.015f;
    private const float EvasionSpeedBonusPerPoint = 0.005f;
    private const float EvasionLuckBonusPerPoint = 0.04f;
    public const float EnemyAttackSpeedBaseMultiplier = 1.0f;
    public const float EnemyAttackSpeedExtraMax = 1.0f;
    public const float EnemyAttackSpeedSoftCap = 10f;
    private const float BaseCritRate = 0.10f;
    private const float CritRatePerLuck = 0.055f;
    private const float MaxCritRate = 0.75f;
    private const float BaseCritDamageMultiplier = 1.5f;
    private const float CritDamagePerLuck = 0.04f;
    private const float MaxCritDamageMultiplier = 2.1f;

    public static CombatStats GetCombatStats(GameObject owner)
    {
        if (owner == null)
        {
            return null;
        }

        CombatStats stats = owner.GetComponent<CombatStats>();
        if (stats == null)
        {
            stats = owner.GetComponentInParent<CombatStats>();
        }

        return stats;
    }

    public static float ResolveBaseMoveSpeed(CombatStats stats, float fallbackBaseMoveSpeed)
    {
        if (stats == null)
        {
            return Mathf.Max(0f, fallbackBaseMoveSpeed);
        }

        return stats.speed > 0f ? stats.speed : Mathf.Max(0f, fallbackBaseMoveSpeed);
    }

    public static float GetSpeedMoveMultiplier(CombatStats stats)
    {
        if (stats == null)
        {
            return 1f;
        }

        float speed = stats != null ? Mathf.Max(0f, stats.speed) : 0f;
        return 1f + (speed - 1f) * SpeedMoveBonusPerPoint;
    }

    public static float GetMoveSpeedMultiplier(CombatStats stats)
    {
        return GetSpeedMoveMultiplier(stats);
    }

    public static float GetCooldownMultiplier(CombatStats stats)
    {
        if (stats == null)
        {
            return 1f;
        }

        float speed = stats != null ? Mathf.Max(0f, stats.speed) : 0f;
        return 1f / (1f + (speed - 1f) * SpeedCooldownBonusPerPoint);
    }

    public static float GetEnemyAttackSpeedMultiplier(CombatStats stats)
    {
        if (stats == null)
        {
            return EnemyAttackSpeedBaseMultiplier;
        }

        float speed = Mathf.Max(0f, stats.speed);
        float speedBonus = Mathf.Max(0f, speed - 1f);
        float bonusPortion = speedBonus / (speedBonus + EnemyAttackSpeedSoftCap);
        return Mathf.Max(0.1f, EnemyAttackSpeedBaseMultiplier + EnemyAttackSpeedExtraMax * bonusPortion);
    }

    public static float ResolveMoveSpeed(CombatStats stats, float baseMoveSpeed, float externalMoveMultiplier = 1f)
    {
        float externalMultiplier = Mathf.Max(0f, externalMoveMultiplier);
        return Mathf.Max(0f, baseMoveSpeed) * GetSpeedMoveMultiplier(stats) * externalMultiplier;
    }

    public static float ResolveCooldown(CombatStats stats, float baseCooldown, float externalCooldownMultiplier = 1f)
    {
        float resolvedBaseCooldown = Mathf.Max(0f, baseCooldown);
        float externalMultiplier = Mathf.Max(0f, externalCooldownMultiplier);
        return resolvedBaseCooldown * GetCooldownMultiplier(stats) * externalMultiplier;
    }

    public static float GetEvasionMultiplier(CombatStats stats)
    {
        if (stats == null)
        {
            return 1f;
        }

        float speed = stats != null ? Mathf.Max(0f, stats.speed) : 0f;
        float luck = stats != null ? Mathf.Max(0f, stats.luck) : 0f;
        return 1f + (speed - 1f) * EvasionSpeedBonusPerPoint + (luck - 1f) * EvasionLuckBonusPerPoint;
    }

    public static float GetEvasionChance(CombatStats stats)
    {
        if (stats == null)
        {
            return 0f;
        }

        return Mathf.Clamp01(BaseEvasionChance * GetEvasionMultiplier(stats));
    }

    public static float GetAccuracyMultiplier(CombatStats attackerStats)
    {
        if (attackerStats == null)
        {
            return 1f;
        }

        float attackerSpeed = Mathf.Max(0f, attackerStats.speed);
        return Mathf.Max(0.1f, 1f + Mathf.Max(0f, attackerSpeed - 1f) * 0.04f);
    }

    public static float GetFinalEvasionChance(CombatStats defenderStats, CombatStats attackerStats)
    {
        float rawEvasionChance = GetEvasionChance(defenderStats);
        float accuracyMultiplier = GetAccuracyMultiplier(attackerStats);
        if (accuracyMultiplier <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(rawEvasionChance / accuracyMultiplier);
    }

    public static bool TryRollEvasion(GameObject target, out float evasionChance)
    {
        CombatStats stats = GetCombatStats(target);
        evasionChance = GetEvasionChance(stats);
        if (evasionChance <= 0f)
        {
            return false;
        }

        return Random.value < evasionChance;
    }

    public static bool TryRollEvasion(GameObject defender, GameObject attacker, out float finalEvasionChance)
    {
        CombatStats defenderStats = GetCombatStats(defender);
        CombatStats attackerStats = GetCombatStats(attacker);
        finalEvasionChance = GetFinalEvasionChance(defenderStats, attackerStats);
        if (finalEvasionChance <= 0f)
        {
            return false;
        }

        return Random.value < finalEvasionChance;
    }

    public static float GetCritRate(CombatStats stats)
    {
        float luck = stats != null ? Mathf.Max(0f, stats.luck) : 0f;
        return Mathf.Min(MaxCritRate, BaseCritRate + luck * CritRatePerLuck);
    }

    public static float GetCritDamageMultiplier(CombatStats stats)
    {
        float luck = stats != null ? Mathf.Max(0f, stats.luck) : 0f;
        return Mathf.Min(MaxCritDamageMultiplier, BaseCritDamageMultiplier + luck * CritDamagePerLuck);
    }

    public static bool TryRollCritical(GameObject attacker, out float critDamageMultiplier)
    {
        CombatStats stats = GetCombatStats(attacker);
        if (stats == null)
        {
            critDamageMultiplier = 1f;
            return false;
        }

        if (Random.value > GetCritRate(stats))
        {
            critDamageMultiplier = 1f;
            return false;
        }

        critDamageMultiplier = GetCritDamageMultiplier(stats);
        return critDamageMultiplier > 1f;
    }

    public static float ApplyCriticalDamage(GameObject attacker, float damage, out bool isCritical)
    {
        damage = Mathf.Max(0f, damage);
        isCritical = TryRollCritical(attacker, out float critDamageMultiplier);
        return isCritical ? damage * critDamageMultiplier : damage;
    }

    public static float ResolveAttackPower(GameObject owner, BattleDamageType damageType, float fallbackValue)
    {
        CombatStats stats = GetCombatStats(owner);
        if (stats == null)
        {
            return Mathf.Max(0f, fallbackValue);
        }

        return damageType == BattleDamageType.Physical
            ? Mathf.Max(0f, stats.physicalAttack)
            : Mathf.Max(0f, stats.specialAttack);
    }
}
