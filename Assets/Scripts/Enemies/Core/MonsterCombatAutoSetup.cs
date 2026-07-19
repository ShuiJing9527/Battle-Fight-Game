using UnityEngine;
using System.Collections;

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
        else
        {
            if (forcedSpecies.HasValue)
            {
                identity.species = forcedSpecies.Value;
            }

            if (forcedRank.HasValue)
            {
                identity.rank = forcedRank.Value;
            }
        }

        identity.attackStyle = ResolveAttackStyle(identity);

        SyncExistingStats(monster, identity);
        EnsureRuntimeComponents(monster, identity);
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
        if (identity == null)
        {
            return MonsterAttackStyle.Melee;
        }

        if (identity.rank == MonsterRank.Boss && IsSlimeSpecies(identity.species))
        {
            return MonsterAttackStyle.ElementalBoss;
        }

        if (IsSlimeSpecies(identity.species))
        {
            return MonsterAttackStyle.Melee;
        }

        MonsterRank rank = identity.rank;
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

    private static void EnsureRuntimeComponents(GameObject monster, MonsterIdentity identity)
    {
        if (monster.GetComponent<WorldHealthBar>() == null)
        {
            monster.AddComponent<WorldHealthBar>();
        }

        if (monster.GetComponent<EliteSlimeSplitOnDeath>() == null)
        {
            monster.AddComponent<EliteSlimeSplitOnDeath>();
        }

        if (identity != null &&
            identity.rank == MonsterRank.Boss &&
            monster.GetComponent<SplitBossMinionController>() == null)
        {
            monster.AddComponent<SplitBossMinionController>();
        }

        if (identity != null && BattleTargetUtility.IsMonster(monster))
        {
            MonsterDayNightAffinity affinity = monster.GetComponent<MonsterDayNightAffinity>();
            if (affinity == null)
            {
                affinity = monster.AddComponent<MonsterDayNightAffinity>();
            }

            affinity.NotifyStatsInitialized();
        }
    }

    private static void EnsureRankVisual(GameObject monster, MonsterIdentity identity)
    {
        MonsterRankVisual rankVisual = monster.GetComponent<MonsterRankVisual>();
        if (rankVisual == null)
        {
            rankVisual = monster.AddComponent<MonsterRankVisual>();
        }

        if (rankVisual.effectRoot == null)
        {
            rankVisual.effectRoot = monster.transform;
        }

        rankVisual.Apply(identity);

        if (identity != null && identity.rank == MonsterRank.Boss)
        {
            Transform runtimeVisualRoot = rankVisual.RuntimeVisualRoot;
            Transform visualSlime = monster.transform.Find("Visual_Slime");
            Debug.Log(
                "[BossRankCheck] " +
                "object=" + monster.name +
                " MonsterIdentity.rank=" + identity.rank +
                " runtime rank=" + identity.rank +
                " configured rank=" + identity.rank +
                " attackStyle=" + identity.attackStyle +
                " isBoss=" + (identity.rank == MonsterRank.Boss) +
                " MonsterRankVisual enabled=" + rankVisual.enabled +
                " MonsterRankVisual applied rank=" + rankVisual.LastAppliedRank +
                " Visual_Slime=" + (visualSlime != null ? visualSlime.name : "null") +
                " runtimeVisualRoot=" + (runtimeVisualRoot != null ? runtimeVisualRoot.name : "null"),
                monster);
        }
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

[DisallowMultipleComponent]
public sealed class MonsterDayNightAffinity : MonoBehaviour
{
    private enum MonsterAffinityState
    {
        Unknown,
        Day,
        Night
    }

    [Header("Polling")]
    [Tooltip("How often the boss checks the existing TOD day/night state when no change event is available.")]
    [SerializeField, Min(0.05f)] private float stateCheckInterval = 0.25f;
    [Tooltip("How long to wait for the existing TOD day/night state to become available before logging an error.")]
    [SerializeField, Min(0.1f)] private float initializationTimeout = 2f;

    private CombatStats stats;
    private MonsterIdentity identity;
    private Coroutine runtimeCoroutine;
    private MonsterAffinityState currentState = MonsterAffinityState.Unknown;
    private bool baseStatsCaptured;
    private bool missingStateLogged;
    private float basePhysicalAttack;
    private float baseSpecialAttack;
    private float basePhysicalDefense;
    private float baseSpecialDefense;

    public void NotifyStatsInitialized()
    {
        CacheReferences();
        baseStatsCaptured = false;
        missingStateLogged = false;
        currentState = MonsterAffinityState.Unknown;
    }

    private void OnEnable()
    {
        NotifyStatsInitialized();
        if (runtimeCoroutine == null)
        {
            runtimeCoroutine = StartCoroutine(RuntimeLoop());
        }
    }

    private void OnDisable()
    {
        if (runtimeCoroutine != null)
        {
            StopCoroutine(runtimeCoroutine);
            runtimeCoroutine = null;
        }
    }

    public static float ResolveAttackScale(GameObject owner, BattleDamageType damageType)
    {
        if (owner == null)
        {
            return 1f;
        }

        MonsterDayNightAffinity affinity = owner.GetComponent<MonsterDayNightAffinity>();
        if (affinity == null)
        {
            affinity = owner.GetComponentInParent<MonsterDayNightAffinity>();
        }

        return affinity != null ? affinity.ResolveCurrentScale(damageType) : 1f;
    }

    private IEnumerator RuntimeLoop()
    {
        yield return null;

        CacheReferences();
        if (!IsEligibleMonster())
        {
            runtimeCoroutine = null;
            yield break;
        }

        float startTime = Time.unscaledTime;
        while (enabled)
        {
            if (!baseStatsCaptured)
            {
                CaptureBaseStats();
            }

            if (TryResolveState(out MonsterAffinityState resolvedState))
            {
                ApplyStateIfNeeded(resolvedState);
                break;
            }

            if (!missingStateLogged && Time.unscaledTime - startTime >= Mathf.Max(0.1f, initializationTimeout))
            {
                missingStateLogged = true;
                Debug.LogError(
                    "[MonsterDayNightAffinity] Failed to resolve day/night state from TODDayNightAdapter within timeout. Monster affinity was not applied yet. object=" + name,
                    this);
            }

            yield return null;
        }

        while (enabled)
        {
            if (!baseStatsCaptured)
            {
                CaptureBaseStats();
            }

            if (TryResolveState(out MonsterAffinityState resolvedState))
            {
                ApplyStateIfNeeded(resolvedState);
            }

            yield return new WaitForSeconds(Mathf.Max(0.05f, stateCheckInterval));
        }

        runtimeCoroutine = null;
    }

    private void CacheReferences()
    {
        if (stats == null)
        {
            stats = GetComponent<CombatStats>();
        }

        if (identity == null)
        {
            identity = GetComponent<MonsterIdentity>();
        }
    }

    private bool IsEligibleMonster()
    {
        if (stats == null)
        {
            Debug.LogError("[MonsterDayNightAffinity] Missing CombatStats. Monster affinity cannot be applied. object=" + name, this);
            return false;
        }

        if (identity == null)
        {
            Debug.LogError("[MonsterDayNightAffinity] Missing MonsterIdentity. Monster affinity cannot be applied. object=" + name, this);
            return false;
        }

        return BattleTargetUtility.IsMonster(gameObject);
    }

    private void CaptureBaseStats()
    {
        if (baseStatsCaptured || stats == null)
        {
            return;
        }

        basePhysicalAttack = Mathf.Max(0f, stats.physicalAttack);
        baseSpecialAttack = Mathf.Max(0f, stats.specialAttack);
        basePhysicalDefense = Mathf.Max(0f, stats.physicalDefense);
        baseSpecialDefense = Mathf.Max(0f, stats.specialDefense);
        baseStatsCaptured = true;
    }

    private bool TryResolveState(out MonsterAffinityState resolvedState)
    {
        resolvedState = MonsterAffinityState.Unknown;
        if (!TODDayNightAdapter.TryGetIsDay(out bool isDay) || !TODDayNightAdapter.TryGetIsNight(out bool isNight))
        {
            return false;
        }

        if (isDay == isNight)
        {
            return false;
        }

        resolvedState = isDay ? MonsterAffinityState.Day : MonsterAffinityState.Night;
        return true;
    }

    private void ApplyStateIfNeeded(MonsterAffinityState resolvedState)
    {
        if (!baseStatsCaptured || stats == null || resolvedState == MonsterAffinityState.Unknown || currentState == resolvedState)
        {
            return;
        }

        switch (resolvedState)
        {
            case MonsterAffinityState.Day:
                stats.physicalAttack = RoundCombatStat(basePhysicalAttack, 1f);
                stats.specialAttack = RoundCombatStat(baseSpecialAttack, 1f);
                stats.physicalDefense = RoundCombatStat(basePhysicalDefense, 2f);
                stats.specialDefense = RoundCombatStat(baseSpecialDefense, 2f);
                Debug.Log("[MonsterDayNightAffinity] " + name + " Day applied: DEF x2, SP.DEF x2.", this);
                break;
            case MonsterAffinityState.Night:
                stats.physicalAttack = RoundCombatStat(basePhysicalAttack, 2f);
                stats.specialAttack = RoundCombatStat(baseSpecialAttack, 2f);
                stats.physicalDefense = RoundCombatStat(basePhysicalDefense, 1f);
                stats.specialDefense = RoundCombatStat(baseSpecialDefense, 1f);
                Debug.Log("[MonsterDayNightAffinity] " + name + " Night applied: ATK x2, SP.ATK x2.", this);
                break;
        }

        currentState = resolvedState;
    }

    private float ResolveCurrentScale(BattleDamageType damageType)
    {
        if (!baseStatsCaptured || stats == null)
        {
            return 1f;
        }

        float baseValue = damageType == BattleDamageType.Physical ? basePhysicalAttack : baseSpecialAttack;
        if (baseValue <= 0f)
        {
            return 1f;
        }

        float currentValue = damageType == BattleDamageType.Physical ? stats.physicalAttack : stats.specialAttack;
        return Mathf.Max(0f, currentValue) / baseValue;
    }

    private static float RoundCombatStat(float baseValue, float multiplier)
    {
        return Mathf.Max(0f, Mathf.Round(Mathf.Max(0f, baseValue) * Mathf.Max(0f, multiplier)));
    }
}
