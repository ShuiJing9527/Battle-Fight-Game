using UnityEngine;

public static class MonsterCombatAutoSetup
{
    public static bool enableStatVariance = true;
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

        identity.attackStyle = ResolveAttackStyle(identity.rank);

        ApplyStats(monster, identity);
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

    private static MonsterAttackStyle ResolveAttackStyle(MonsterRank rank)
    {
        if (rank == MonsterRank.Boss)
        {
            return MonsterAttackStyle.ElementalBoss;
        }

        return rank == MonsterRank.Elite ? MonsterAttackStyle.Ranged : MonsterAttackStyle.Melee;
    }

    private static void ApplyStats(GameObject monster, MonsterIdentity identity)
    {
        CombatStats stats = monster.GetComponent<CombatStats>();
        if (stats == null)
        {
            stats = monster.AddComponent<CombatStats>();
        }

        float maxHealth = 55f;
        float physicalAttack = 8f;
        float specialAttack = 8f;
        float physicalDefense = 0f;
        float specialDefense = 0f;
        float speed = 2.5f;
        float range = 1.35f;
        float hitRange = 1.6f;
        float cooldown = 1.1f;

        switch (identity.species)
        {
            case MonsterSpecies.GreenSlime:
                maxHealth = 60f;
                physicalAttack = 7f;
                specialAttack = 7f;
                speed = 2.9f;
                break;
            case MonsterSpecies.LavaSlime:
                maxHealth = 75f;
                physicalAttack = 13f;
                specialAttack = 13f;
                physicalDefense = 1f;
                specialDefense = 1f;
                speed = 2.1f;
                break;
            case MonsterSpecies.PoisonSlime:
                maxHealth = 50f;
                physicalAttack = 9f;
                specialAttack = 12f;
                speed = 2.7f;
                break;
            case MonsterSpecies.RainbowSlime:
                maxHealth = 105f;
                physicalAttack = 15f;
                specialAttack = 15f;
                physicalDefense = 2f;
                specialDefense = 2f;
                speed = 2.3f;
                break;
            case MonsterSpecies.Flying:
                maxHealth = 28f;
                physicalAttack = 7f;
                specialAttack = 7f;
                speed = 3.6f;
                range = 5.5f;
                hitRange = 5.5f;
                break;
            case MonsterSpecies.Ranged:
                maxHealth = 32f;
                physicalAttack = 9f;
                specialAttack = 9f;
                speed = 2f;
                range = 7f;
                hitRange = 7f;
                cooldown = 1.7f;
                break;
            case MonsterSpecies.Tank:
                maxHealth = 95f;
                physicalAttack = 10f;
                specialAttack = 10f;
                physicalDefense = 4f;
                specialDefense = 4f;
                speed = 1.25f;
                cooldown = 1.4f;
                break;
            case MonsterSpecies.Assassin:
                maxHealth = 26f;
                physicalAttack = 14f;
                specialAttack = 14f;
                speed = 4.4f;
                cooldown = 0.75f;
                break;
        }

        if (identity.rank == MonsterRank.Elite)
        {
            maxHealth *= 2.5f;
            physicalAttack *= 2f;
            specialAttack *= 2f;
            physicalDefense += 2f;
            specialDefense += 2f;
            speed *= 1.05f;
            range = 5f;
            hitRange = 6f;
            cooldown = 1.6f;
        }
        else if (identity.rank == MonsterRank.Boss)
        {
            maxHealth *= 9f;
            physicalAttack *= 4f;
            specialAttack *= 4f;
            physicalDefense += 5f;
            specialDefense += 5f;
            speed *= 1.1f;
            range = 8f;
            hitRange = 8f;
            cooldown = 2.2f;
        }

        ApplyStatVariance(monster, ref maxHealth, ref physicalAttack, ref specialAttack, ref physicalDefense, ref specialDefense, ref speed);

        stats.maxHealth = maxHealth;
        stats.physicalAttack = physicalAttack;
        stats.specialAttack = specialAttack;
        stats.physicalDefense = physicalDefense;
        stats.specialDefense = specialDefense;
        stats.speed = speed;

        CombatHealth health = monster.GetComponent<CombatHealth>();
        if (health == null)
        {
            health = monster.AddComponent<CombatHealth>();
        }

        health.stats = stats;
        health.currentHealth = stats.maxHealth;

        EnemyController controller = monster.GetComponent<EnemyController>();
        if (controller != null)
        {
            controller.ConfigureRuntime(speed, 0.8f, range, hitRange, cooldown, physicalAttack, identity.attackStyle);
        }
    }

    private static void EnsureRuntimeComponents(GameObject monster)
    {
        if (monster.GetComponent<WorldHealthBar>() == null)
        {
            monster.AddComponent<WorldHealthBar>();
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
