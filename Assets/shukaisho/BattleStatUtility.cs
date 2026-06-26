using UnityEngine;

public static class BattleStatUtility
{
    private const float MoveSpeedPerPoint = 0.02f;
    private const float CooldownScalePerPoint = 0.035f;
    private const float MinimumCooldownMultiplier = 0.45f;
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

    public static float GetMoveSpeedMultiplier(CombatStats stats)
    {
        float speed = stats != null ? Mathf.Max(0f, stats.speed) : 0f;
        return 1f + speed * MoveSpeedPerPoint;
    }

    public static float GetCooldownMultiplier(CombatStats stats)
    {
        float speed = stats != null ? Mathf.Max(0f, stats.speed) : 0f;
        float multiplier = 1f / (1f + speed * CooldownScalePerPoint);
        return Mathf.Max(MinimumCooldownMultiplier, multiplier);
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
