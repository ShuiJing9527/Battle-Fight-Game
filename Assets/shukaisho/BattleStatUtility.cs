using UnityEngine;

public static class BattleStatUtility
{
    public const float ActualMoveSpeedCap = 30f;
    public const float PlayerExcessMoveSpeedDamageBonusPerPoint = 0.01f;
    public const float MaxFinalEvasionChance = 0.50f;
    public const float MinNormalMonsterHitChanceAgainstPlayer = 0.65f;
    public const float MinEliteMonsterHitChanceAgainstPlayer = 0.75f;
    public const float MinBossMonsterHitChanceAgainstPlayer = 0.85f;
    public const float MinFinalBossHitChanceAgainstPlayer = 0.90f;
    public const float MinPlayerHitChanceAgainstMonster = 0.90f;
    public const float MaxFinalHitChance = 0.95f;
    private const float BaseEvasionChance = 0.05f;
    private const float SpeedMoveBonusPerPoint = 0.0075f;
    private const float SpeedCooldownBonusPerPoint = 0.015f;
    private const float EvasionSpeedBonusPerPoint = 0f;
    private const float EvasionLuckBonusPerPoint = 0.04f;
    public const float EnemyAttackSpeedBaseMultiplier = 1.0f;
    public const float EnemyAttackSpeedExtraMax = 1.0f;
    public const float EnemyAttackSpeedSoftCap = 10f;
    private const float BaseCritRate = 0.10f;
    private const float CritRatePerLuck = 0.055f;
    private const float MaxCritRate = 0.75f;
    private const float BaseCritDamageMultiplier = 1.5f;
    private const float PlayerCritDamagePerSpeed = 0.003f;
    private const float MaxPlayerCritDamageMultiplier = 2.5f;
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

    public static float ApplyEquippedRuneBaseDamageBonus(GameObject owner, float baseDamage)
    {
        return ApplyEquippedRuneBaseDamageBonus(owner, baseDamage, out _, out _, out _, out _);
    }

    public static float ApplyEquippedRuneBaseDamageBonus(
        GameObject owner,
        float baseDamage,
        out int equippedRuneCount,
        out float damageBonusPerRune,
        out float runeBaseDamageBonus,
        out string runeCountSource)
    {
        float damage = Mathf.Max(0f, baseDamage);
        equippedRuneCount = 0;
        damageBonusPerRune = 0f;
        runeBaseDamageBonus = 0f;
        runeCountSource = "NoPlayerSource";

        GameObject playerSource = BattleTargetUtility.ResolvePlayerSource(owner);
        if (playerSource == null)
        {
            return damage;
        }

        RuneRuntimeState runeRuntimeState = ResolveRuneRuntimeState(playerSource);

        if (runeRuntimeState != null)
        {
            runeRuntimeState.RebuildFromEquippedRunes();
            equippedRuneCount = Mathf.Max(0, runeRuntimeState.GetTotalEquippedRuneCount());
            damageBonusPerRune = runeRuntimeState.GetDemoBaseDamageBonusPerEquippedRune();
            runeCountSource = "RuneRuntimeState.GetTotalEquippedRuneCount";
        }
        else
        {
            equippedRuneCount = CountEquippedRunesFromSkillCaster(playerSource);
            damageBonusPerRune = 5f;
            runeCountSource = "CombatSkillCaster.equippedRunes";
        }

        if (equippedRuneCount <= 0)
        {
            return damage;
        }

        runeBaseDamageBonus = equippedRuneCount * Mathf.Max(0f, damageBonusPerRune);
        return damage + runeBaseDamageBonus;
    }

    public static RuneRuntimeState ResolveRuneRuntimeState(GameObject owner)
    {
        GameObject playerSource = BattleTargetUtility.ResolvePlayerSource(owner) ?? owner;
        if (playerSource == null)
        {
            return null;
        }

        RuneRuntimeState runeRuntimeState = playerSource.GetComponent<RuneRuntimeState>();
        if (runeRuntimeState != null)
        {
            return runeRuntimeState;
        }

        runeRuntimeState = playerSource.GetComponentInParent<RuneRuntimeState>();
        if (runeRuntimeState != null)
        {
            return runeRuntimeState;
        }

        return playerSource.GetComponentInChildren<RuneRuntimeState>(true);
    }

    public static int GetEquippedRuneCount(GameObject owner, out string runeCountSource)
    {
        GameObject playerSource = BattleTargetUtility.ResolvePlayerSource(owner) ?? owner;
        if (playerSource == null)
        {
            runeCountSource = "NoPlayerSource";
            return 0;
        }

        RuneRuntimeState runeRuntimeState = ResolveRuneRuntimeState(playerSource);
        if (runeRuntimeState != null)
        {
            runeCountSource = "RuneRuntimeState.GetTotalEquippedRuneCount";
            return Mathf.Max(0, runeRuntimeState.GetTotalEquippedRuneCount());
        }

        runeCountSource = "CombatSkillCaster.equippedRunes";
        return CountEquippedRunesFromSkillCaster(playerSource);
    }

    private static int CountEquippedRunesFromSkillCaster(GameObject owner)
    {
        CombatSkillCaster caster = owner != null ? owner.GetComponent<CombatSkillCaster>() : null;
        if (caster == null && owner != null)
        {
            caster = owner.GetComponentInParent<CombatSkillCaster>();
        }

        if (caster == null && owner != null)
        {
            caster = owner.GetComponentInChildren<CombatSkillCaster>(true);
        }

        if (caster == null)
        {
            return 0;
        }

        int count = 0;
        for (int skillIndex = 0; skillIndex < 4; skillIndex++)
        {
            BattleSkill skill = caster.TryGetSkillRaw(skillIndex);
            if (skill == null || skill.equippedRunes == null)
            {
                continue;
            }

            int slotLimit = Mathf.Min(Mathf.Max(0, skill.runeSlotCount), skill.equippedRunes.Length);
            for (int slotIndex = 0; slotIndex < slotLimit; slotIndex++)
            {
                RuneDefinition rune = skill.equippedRunes[slotIndex];
                if (rune != null && rune.IsConfigured() && rune.runeType != RuneType.None)
                {
                    count++;
                }
            }
        }

        return count;
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

    public static float ClampActualMoveSpeed(float rawMoveSpeed, out float excessMoveSpeed)
    {
        float safeRawMoveSpeed = Mathf.Max(0f, rawMoveSpeed);
        float cap = Mathf.Max(0f, ActualMoveSpeedCap);
        excessMoveSpeed = Mathf.Max(0f, safeRawMoveSpeed - cap);
        return Mathf.Min(safeRawMoveSpeed, cap);
    }

    public static float GetPlayerExcessMoveSpeedDamageMultiplier(GameObject owner)
    {
        PlayerMovement movement = ResolvePlayerMovement(owner);
        if (movement == null)
        {
            return 1f;
        }

        return 1f + Mathf.Max(0f, movement.ExcessMoveSpeedDamageBonus);
    }

    public static float ApplyPlayerMoveSpeedDamageBonus(GameObject owner, float damage)
    {
        return Mathf.Max(0f, damage) * GetPlayerExcessMoveSpeedDamageMultiplier(owner);
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

        return Mathf.Clamp(BaseEvasionChance * GetEvasionMultiplier(stats), 0f, MaxFinalEvasionChance);
    }

    public static float GetRawEvasionChance(CombatStats stats)
    {
        if (stats == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, BaseEvasionChance * GetEvasionMultiplier(stats));
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

        return Mathf.Clamp(rawEvasionChance / accuracyMultiplier, 0f, MaxFinalEvasionChance);
    }

    public static float GetFinalEvasionChance(GameObject defender, GameObject attacker)
    {
        ResolveFinalEvasionAndHitChance(defender, attacker, out _, out _, out float finalEvasionChance, out _);
        return finalEvasionChance;
    }

    public static void ResolveFinalEvasionAndHitChance(
        GameObject defender,
        GameObject attacker,
        out float rawEvasionChance,
        out float clampedEvasionChance,
        out float finalEvasionChance,
        out float finalHitChance)
    {
        CombatStats defenderStats = GetCombatStats(defender);
        CombatStats attackerStats = GetCombatStats(attacker);
        rawEvasionChance = GetRawEvasionChance(defenderStats);
        clampedEvasionChance = GetEvasionChance(defenderStats);
        float accuracyAdjustedEvasion = GetFinalEvasionChance(defenderStats, attackerStats);
        accuracyAdjustedEvasion *= DayNightAffinityDamageModifier.GetWrongTimeEvasionMultiplier(defender);
        float minHitChance = ResolveMinHitChance(defender, attacker);
        finalHitChance = Mathf.Clamp(1f - accuracyAdjustedEvasion, minHitChance, MaxFinalHitChance);
        finalEvasionChance = Mathf.Clamp01(1f - finalHitChance);
    }

    public static bool TryRollEvasion(GameObject target, out float evasionChance)
    {
        CombatStats stats = GetCombatStats(target);
        evasionChance = GetEvasionChance(stats) * DayNightAffinityDamageModifier.GetWrongTimeEvasionMultiplier(target);
        if (evasionChance <= 0f)
        {
            return false;
        }

        return Random.value < evasionChance;
    }

    public static bool TryRollEvasion(GameObject defender, GameObject attacker, out float finalEvasionChance)
    {
        finalEvasionChance = GetFinalEvasionChance(defender, attacker);
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

    public static float GetCritDamageMultiplier(GameObject attacker)
    {
        CombatStats stats = GetCombatStats(attacker);
        if (BattleTargetUtility.IsPlayer(attacker))
        {
            float finalSpeed = stats != null ? Mathf.Max(0f, stats.speed) : 0f;
            return Mathf.Clamp(
                BaseCritDamageMultiplier + finalSpeed * PlayerCritDamagePerSpeed,
                BaseCritDamageMultiplier,
                MaxPlayerCritDamageMultiplier);
        }

        return GetCritDamageMultiplier(stats);
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

        critDamageMultiplier = GetCritDamageMultiplier(attacker);
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

    private static PlayerMovement ResolvePlayerMovement(GameObject owner)
    {
        if (owner == null)
        {
            return null;
        }

        PlayerMovement movement = owner.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            movement = owner.GetComponentInParent<PlayerMovement>();
        }

        return movement;
    }

    private static float ResolveMinHitChance(GameObject defender, GameObject attacker)
    {
        if (BattleTargetUtility.IsMonster(defender) && BattleTargetUtility.IsPlayer(attacker))
        {
            return MinPlayerHitChanceAgainstMonster;
        }

        if (!BattleTargetUtility.IsPlayer(defender))
        {
            return 0f;
        }

        if (attacker == null)
        {
            return MinNormalMonsterHitChanceAgainstPlayer;
        }

        if (!BattleTargetUtility.IsMonster(attacker))
        {
            return 0f;
        }

        return ResolveMonsterMinimumHitChanceAgainstPlayer(attacker);
    }

    private static float ResolveMonsterMinimumHitChanceAgainstPlayer(GameObject attacker)
    {
        if (attacker == null)
        {
            return MinNormalMonsterHitChanceAgainstPlayer;
        }

        MonsterIdentity identity = attacker.GetComponent<MonsterIdentity>();
        if (identity == null)
        {
            identity = attacker.GetComponentInParent<MonsterIdentity>();
        }

        if (IsFinalBossMonster(attacker))
        {
            return MinFinalBossHitChanceAgainstPlayer;
        }

        if (identity == null)
        {
            return MinNormalMonsterHitChanceAgainstPlayer;
        }

        switch (identity.rank)
        {
            case MonsterRank.Boss:
                return MinBossMonsterHitChanceAgainstPlayer;
            case MonsterRank.Elite:
                return MinEliteMonsterHitChanceAgainstPlayer;
            default:
                return MinNormalMonsterHitChanceAgainstPlayer;
        }
    }

    private static bool IsFinalBossMonster(GameObject owner)
    {
        if (owner == null)
        {
            return false;
        }

        return owner.GetComponent<CleanupBossPhaseSplit>() != null
               || owner.GetComponentInParent<CleanupBossPhaseSplit>() != null;
    }

    public static bool IsBossLikeMonster(GameObject owner)
    {
        if (owner == null)
        {
            return false;
        }

        MonsterIdentity identity = owner.GetComponent<MonsterIdentity>();
        if (identity == null)
        {
            identity = owner.GetComponentInParent<MonsterIdentity>();
        }

        return identity != null && identity.rank == MonsterRank.Boss;
    }

    public static string GetAttackerRankLabel(GameObject owner)
    {
        if (owner == null)
        {
            return "Unknown";
        }

        MonsterIdentity identity = owner.GetComponent<MonsterIdentity>();
        if (identity == null)
        {
            identity = owner.GetComponentInParent<MonsterIdentity>();
        }

        if (identity != null)
        {
            return identity.rank.ToString();
        }

        return BattleTargetUtility.IsPlayer(owner) ? "Player" : "Unknown";
    }
}
