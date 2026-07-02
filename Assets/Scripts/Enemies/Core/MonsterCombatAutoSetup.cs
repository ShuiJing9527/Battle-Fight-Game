using UnityEngine;

public static class MonsterCombatAutoSetup
{
    public static bool enableStatVariance = false;
    public static float minStatVariance = 0.10f;
    public static float maxStatVariance = 0.20f;

    public static void Configure(GameObject monster, MonsterSpecies? forcedSpecies = null, MonsterRank? forcedRank = null)
    {
        if (monster == null)
        {
            return;
        }

        MonsterIdentity identity = monster.GetComponent<MonsterIdentity>();
        bool createdIdentity = false;
        if (identity == null)
        {
            identity = monster.AddComponent<MonsterIdentity>();
            createdIdentity = true;
        }

        if (createdIdentity)
        {
            identity.species = forcedSpecies ?? ResolveSpecies(monster.name);
            identity.rank = forcedRank ?? ResolveRank(identity.species);
        }

        identity.attackStyle = ResolveAttackStyle(identity);

        SyncExistingStats(monster, identity);
        EnsureRuntimeComponents(monster);
        EnsureRankVisual(monster, identity);
    }

    private static MonsterSpecies ResolveSpecies(string monsterName)
    {
        string lower = monsterName.ToLowerInvariant();
        if (lower.Contains("green")) return MonsterSpecies.GreenSlime;
        if (lower.Contains("lava")) return MonsterSpecies.LavaSlime;
        if (lower.Contains("poison")) return MonsterSpecies.PoisonSlime;
        if (lower.Contains("rainbow")) return MonsterSpecies.RainbowSlime;
        return MonsterSpecies.BlueSlime;
    }

    private static MonsterRank ResolveRank(MonsterSpecies species)
    {
        return species == MonsterSpecies.RainbowSlime ? MonsterRank.Boss : MonsterRank.Normal;
    }

    private static MonsterAttackStyle ResolveAttackStyle(MonsterIdentity identity)
    {
        if (identity != null && IsSlimeSpecies(identity.species))
        {
            return MonsterAttackStyle.Melee;
        }

        MonsterRank rank = identity != null ? identity.rank : MonsterRank.Normal;
        if (rank == MonsterRank.Boss)
        {
            return MonsterAttackStyle.ElementalBoss;
        }

        return rank == MonsterRank.Elite ? MonsterAttackStyle.Ranged : MonsterAttackStyle.Melee;
    }

    private static void SyncExistingStats(GameObject monster, MonsterIdentity identity)
    {
        CombatStats stats = monster.GetComponent<CombatStats>();
        bool createdFallbackStats = false;
        if (stats == null)
        {
            stats = monster.AddComponent<CombatStats>();
            createdFallbackStats = true;
        }

        if (createdFallbackStats)
        {
            ApplyFallbackStats(stats);
            Debug.LogWarning($"[MonsterCombatAutoSetup] Missing CombatStats on '{monster.name}'. Added fallback CombatStats at runtime.", monster);
        }

        float maxHealth = Mathf.Max(1f, stats.maxHealth);
        float physicalAttack = Mathf.Max(0f, stats.physicalAttack);
        float specialAttack = Mathf.Max(0f, stats.specialAttack);
        float physicalDefense = Mathf.Max(0f, stats.physicalDefense);
        float specialDefense = Mathf.Max(0f, stats.specialDefense);
        float speed = Mathf.Max(0.1f, stats.speed);
        float luck = Mathf.Max(0f, stats.luck);
        float range = 1.2f;
        float hitRange = 1.25f;
        float cooldown = 1.35f;

        if (identity.rank == MonsterRank.Elite)
        {
            if (IsSlimeSpecies(identity.species))
            {
                range = 1.35f;
                hitRange = 1.45f;
                cooldown = 1.45f;
            }
            else
            {
                range = 5f;
                hitRange = 6f;
                cooldown = 1.6f;
            }
        }
        else if (identity.rank == MonsterRank.Boss)
        {
            if (IsSlimeSpecies(identity.species))
            {
                range = 1.6f;
                hitRange = 1.8f;
                cooldown = 1.5f;
            }
            else
            {
                range = 8f;
                hitRange = 8f;
                cooldown = 2.2f;
            }
        }

        ApplyStatVariance(monster, ref maxHealth, ref physicalAttack, ref specialAttack, ref physicalDefense, ref specialDefense, ref speed);

        stats.maxHealth = maxHealth;
        stats.physicalAttack = physicalAttack;
        stats.specialAttack = specialAttack;
        stats.physicalDefense = physicalDefense;
        stats.specialDefense = specialDefense;
        stats.speed = speed;
        stats.luck = luck;

        CombatHealth health = monster.GetComponent<CombatHealth>();
        if (health == null)
        {
            health = monster.AddComponent<CombatHealth>();
        }

        BattleResourceBank resourceBank = monster.GetComponent<BattleResourceBank>();
        if (resourceBank == null)
        {
            resourceBank = monster.AddComponent<BattleResourceBank>();
        }

        health.stats = stats;
        health.resourceBank = resourceBank;
        health.SyncHealthFromStats(refillCurrentHealth: true);

        DissolveOnDeath dissolveOnDeath = monster.GetComponent<DissolveOnDeath>();
        if (dissolveOnDeath != null)
        {
            dissolveOnDeath.EnsureHealthBindings();
        }

        EnemyController controller = monster.GetComponent<EnemyController>();
        if (controller != null)
        {
            float baseMoveSpeed = controller.BaseMoveSpeed > 0f ? controller.BaseMoveSpeed : ResolveMoveSpeed(identity, speed);
            BattleDamageType damageType = identity.attackStyle == MonsterAttackStyle.Melee ? BattleDamageType.Physical : BattleDamageType.Special;
            float attackPower = damageType == BattleDamageType.Physical ? physicalAttack : specialAttack;
            controller.ConfigureRuntime(baseMoveSpeed, 0.8f, range, hitRange, cooldown, attackPower, identity.attackStyle);
        }
    }

    private static void ApplyFallbackStats(CombatStats stats)
    {
        if (stats == null)
        {
            return;
        }

        stats.maxHealth = 100f;
        stats.physicalAttack = 8f;
        stats.physicalDefense = 0f;
        stats.specialAttack = 4f;
        stats.specialDefense = 0f;
        stats.speed = 4f;
        stats.luck = 2f;
    }

    private static float ResolveMoveSpeed(MonsterIdentity identity, float statSpeed)
    {
        if (statSpeed > 0f)
        {
            return Mathf.Max(0.1f, statSpeed);
        }

        switch (identity.species)
        {
            case MonsterSpecies.BlueSlime:
                return 2.2f;
            case MonsterSpecies.GreenSlime:
                return 2.9f;
            case MonsterSpecies.LavaSlime:
                return 2.1f;
            case MonsterSpecies.PoisonSlime:
                return 2.7f;
            case MonsterSpecies.RainbowSlime:
                return 2.3f;
            case MonsterSpecies.Flying:
                return 3.6f;
            case MonsterSpecies.Ranged:
                return 2f;
            case MonsterSpecies.Tank:
                return 1.25f;
            case MonsterSpecies.Assassin:
                return 4.4f;
            default:
                return Mathf.Max(0.1f, statSpeed > 0f ? statSpeed : 2.5f);
        }
    }

    private static bool IsSlimeSpecies(MonsterSpecies species)
    {
        return species == MonsterSpecies.BlueSlime ||
               species == MonsterSpecies.GreenSlime ||
               species == MonsterSpecies.LavaSlime ||
               species == MonsterSpecies.PoisonSlime ||
               species == MonsterSpecies.RainbowSlime;
    }

    private static void EnsureRuntimeComponents(GameObject monster)
    {
        if (monster.GetComponent<WorldHealthBar>() == null)
        {
            monster.AddComponent<WorldHealthBar>();
        }

        if (monster.GetComponent<EliteSlimeSplitOnDeath>() == null)
        {
            monster.AddComponent<EliteSlimeSplitOnDeath>();
        }
    }

    private static void EnsureRankVisual(GameObject monster, MonsterIdentity identity)
    {
        MonsterRankVisual rankVisual = monster.GetComponent<MonsterRankVisual>();
        if (rankVisual == null)
        {
            rankVisual = monster.AddComponent<MonsterRankVisual>();
        }

        if (rankVisual.visualRoot == null)
        {
            rankVisual.visualRoot = monster.transform;
        }

        if (rankVisual.effectRoot == null)
        {
            rankVisual.effectRoot = monster.transform;
        }

        rankVisual.Apply(identity);
    }

    private static void ApplyStatVariance(
        GameObject monster,
        ref float maxHealth,
        ref float physicalAttack,
        ref float specialAttack,
        ref float physicalDefense,
        ref float specialDefense,
        ref float speed)
    {
        if (monster == null || !enableStatVariance)
        {
            maxHealth = Mathf.Max(1f, Mathf.Round(maxHealth));
            physicalAttack = Mathf.Max(1f, Mathf.Round(physicalAttack));
            specialAttack = Mathf.Max(1f, Mathf.Round(specialAttack));
            physicalDefense = Mathf.Max(0f, Mathf.Round(physicalDefense));
            specialDefense = Mathf.Max(0f, Mathf.Round(specialDefense));
            speed = Mathf.Max(0.1f, RoundToDecimals(speed, 2));
            return;
        }

        MonsterStatVarianceState varianceState = monster.GetComponent<MonsterStatVarianceState>();
        if (varianceState == null)
        {
            varianceState = monster.AddComponent<MonsterStatVarianceState>();
        }

        if (!varianceState.initialized)
        {
            varianceState.healthMultiplier = RollVarianceMultiplier();
            varianceState.physicalAttackMultiplier = RollVarianceMultiplier();
            varianceState.specialAttackMultiplier = RollVarianceMultiplier();
            varianceState.physicalDefenseMultiplier = RollVarianceMultiplier();
            varianceState.specialDefenseMultiplier = RollVarianceMultiplier();
            varianceState.speedMultiplier = RollVarianceMultiplier();
            varianceState.initialized = true;
        }

        maxHealth = Mathf.Max(1f, Mathf.Round(maxHealth * varianceState.healthMultiplier));
        physicalAttack = Mathf.Max(1f, Mathf.Round(physicalAttack * varianceState.physicalAttackMultiplier));
        specialAttack = Mathf.Max(1f, Mathf.Round(specialAttack * varianceState.specialAttackMultiplier));
        physicalDefense = Mathf.Max(0f, Mathf.Round(physicalDefense * varianceState.physicalDefenseMultiplier));
        specialDefense = Mathf.Max(0f, Mathf.Round(specialDefense * varianceState.specialDefenseMultiplier));
        speed = Mathf.Max(0.1f, RoundToDecimals(speed * varianceState.speedMultiplier, 2));
    }

    private static float RollVarianceMultiplier()
    {
        float minVariance = Mathf.Clamp01(Mathf.Min(minStatVariance, maxStatVariance));
        float maxVariance = Mathf.Clamp01(Mathf.Max(minStatVariance, maxStatVariance));
        float variance = Random.Range(minVariance, maxVariance);
        return Random.Range(1f - variance, 1f + variance);
    }

    private static float RoundToDecimals(float value, int decimals)
    {
        float factor = Mathf.Pow(10f, Mathf.Max(0, decimals));
        return Mathf.Round(value * factor) / factor;
    }
}

public sealed class MonsterStatVarianceState : MonoBehaviour
{
    public bool initialized;
    public float healthMultiplier = 1f;
    public float physicalAttackMultiplier = 1f;
    public float specialAttackMultiplier = 1f;
    public float physicalDefenseMultiplier = 1f;
    public float specialDefenseMultiplier = 1f;
    public float speedMultiplier = 1f;
}
