using System;
using System.Collections.Generic;
using UnityEngine;

public enum DifficultyPhase
{
    Normal,
    FinalRush,
    SpawnStopped,
    Victory
}

public class EnemyDifficultyDirector : MonoBehaviour
{
    private static EnemyDifficultyDirector instance;
    private static bool isShuttingDown;

    [Header("Timeline")]
    [Tooltip("Seconds required to gain one normal difficulty level before FinalRush starts.")]
    [SerializeField, Min(1f)] private float normalLevelInterval = 10f;
    [Tooltip("Elapsed battle time in seconds when FinalRush begins.")]
    [SerializeField, Min(0f)] private float finalRushStartTime = 600f;
    [Tooltip("How long FinalRush lasts before the scene enters the cleanup phase.")]
    [SerializeField, Min(0f)] private float finalRushDuration = 180f;

    [Header("Initial Grace")]
    [Tooltip("How long monster combat stat growth stays buffered at the start of the run.")]
    [SerializeField, Min(0f)] private float initialGraceDuration = 30f;
    [Tooltip("Combat stat multiplier applied to monsters during the initial grace period.")]
    [SerializeField, Range(0.1f, 1f)] private float initialMonsterStrengthMultiplier = 0.8f;

    [Header("Base Multipliers")]
    [Tooltip("Base HP multiplier for the first difficulty layer. 1 means unchanged.")]
    [SerializeField, Min(0.01f)] private float baseHealthMultiplier = 1f;
    [Tooltip("Base physical attack multiplier for the first difficulty layer. 1 means unchanged.")]
    [SerializeField, Min(0.01f)] private float baseAttackMultiplier = 1f;
    [Tooltip("Base physical defense multiplier for the first difficulty layer. 1 means unchanged.")]
    [SerializeField, Min(0.01f)] private float baseDefenseMultiplier = 1f;
    [Tooltip("Base special attack multiplier for the first difficulty layer. 1 means unchanged.")]
    [SerializeField, Min(0.01f)] private float baseSpecialAttackMultiplier = 1f;
    [Tooltip("Base special defense multiplier for the first difficulty layer. 1 means unchanged.")]
    [SerializeField, Min(0.01f)] private float baseSpecialDefenseMultiplier = 1f;
    [Tooltip("Base speed multiplier for the first difficulty layer. 1 means unchanged.")]
    [SerializeField, Min(0.01f)] private float baseSpeedMultiplier = 1f;

    [Header("Per-Level Growth")]
    [Tooltip("Additive HP growth per difficulty level. 0.10 means +10% per level before FinalRush overrides.")]
    [SerializeField, Min(0f)] private float hpGrowthPerLevel = 0.10f;
    [Tooltip("Additive physical attack growth per difficulty level. 0.12 means +12% per level before FinalRush overrides.")]
    [SerializeField, Min(0f)] private float attackGrowthPerLevel = 0.12f;
    [Tooltip("Additive physical defense growth per difficulty level. 0.10 means +10% per level before FinalRush overrides.")]
    [SerializeField, Min(0f)] private float defenseGrowthPerLevel = 0.10f;
    [Tooltip("Additive special attack growth per difficulty level. 0.12 means +12% per level before FinalRush overrides.")]
    [SerializeField, Min(0f)] private float specialAttackGrowthPerLevel = 0.12f;
    [Tooltip("Additive special defense growth per difficulty level. 0.10 means +10% per level before FinalRush overrides.")]
    [SerializeField, Min(0f)] private float specialDefenseGrowthPerLevel = 0.10f;
    [Tooltip("Additive speed growth per difficulty level. 0.06 means +6% per level before FinalRush overrides.")]
    [SerializeField, Min(0f)] private float speedGrowthPerLevel = 0.06f;

    [Header("Final Rush Multipliers")]
    [Tooltip("Extra HP multiplier applied when FinalRush is active.")]
    [SerializeField, Min(0.01f)] private float finalRushHpMultiplier = 2.5f;
    [Tooltip("Extra physical attack multiplier applied when FinalRush is active.")]
    [SerializeField, Min(0.01f)] private float finalRushAttackMultiplier = 2.2f;
    [Tooltip("Extra physical defense multiplier applied when FinalRush is active.")]
    [SerializeField, Min(0.01f)] private float finalRushDefenseMultiplier = 1.8f;
    [Tooltip("Extra special attack multiplier applied when FinalRush is active.")]
    [SerializeField, Min(0.01f)] private float finalRushSpecialAttackMultiplier = 2.2f;
    [Tooltip("Extra special defense multiplier applied when FinalRush is active.")]
    [SerializeField, Min(0.01f)] private float finalRushSpecialDefenseMultiplier = 1.8f;
    [Tooltip("Extra speed multiplier applied when FinalRush is active.")]
    [SerializeField, Min(0.01f)] private float finalRushSpeedMultiplier = 1.4f;

    [Header("Spawn Pressure")]
    [Tooltip("Additive spawn-rate growth per difficulty level. Higher values make spawn intervals shorter.")]
    [SerializeField, Min(0f)] private float spawnRateGrowthPerLevel = 0.08f;
    [Tooltip("Extra alive-enemy cap granted per difficulty level.")]
    [SerializeField, Min(0)] private int extraMaxAlivePerLevel = 2;
    [Tooltip("FinalRush multiplier applied to the resolved spawn interval. Values below 1 spawn faster.")]
    [SerializeField, Min(0.01f)] private float finalRushSpawnIntervalMultiplier = 0.25f;
    [Tooltip("Extra alive-enemy cap granted while FinalRush is active.")]
    [SerializeField, Min(0)] private int finalRushExtraMaxAlive = 40;

    [Header("Demo Balance")]
    [Tooltip("Exhibition balance: final outgoing damage multiplier for all non-Boss monsters.")]
    [SerializeField, Range(0.01f, 1f)] private float normalEnemyDamageMultiplier = 0.8f;
    [Tooltip("Exhibition balance: final outgoing damage multiplier for Boss attacks.")]
    [SerializeField, Range(0.01f, 1f)] private float bossDamageMultiplier = 0.85f;
    [Tooltip("Exhibition balance: wrong day/night character incoming damage multiplier. 1.5 means +50% damage.")]
    [SerializeField, Min(1f)] private float wrongTimeDamageMultiplier = 1.5f;
    [Tooltip("Exhibition balance: player monster-hit invincibility duration in seconds.")]
    [SerializeField, Min(0f)] private float playerHitInvincibleDuration = 0.8f;
    [Tooltip("Exhibition balance: extra cooldown after Boss main attacks.")]
    [SerializeField, Min(0f)] private float bossAttackRecoveryBonus = 0.7f;
    [Tooltip("Exhibition balance: early-game spawn pressure multiplier. 0.8 means spawn intervals are 25% longer during initial grace.")]
    [SerializeField, Range(0.1f, 1f)] private float earlyGameSpawnMultiplier = 0.8f;

    [Header("Boss Spawn By Kills")]
    [SerializeField, Min(1)] private int killsPerBossSpawn = 100;

    [Header("Victory Check")]
    [SerializeField, Min(0.1f)] private float remainingEnemyCheckInterval = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool debugScaleLogs = false;

    private float elapsedTime;
    private float lastRemainingEnemyCheckTime = -1f;
    private DifficultyPhase currentPhase = DifficultyPhase.Normal;
    private bool finalRushLogged;
    private bool spawnStoppedLogged;
    private bool finalRushStarted;
    private bool finalRushEnded;
    private bool bossDefeated;
    private bool victoryTriggered;
    private bool finalRushVictoryArmed;
    private bool spawnStoppedBossVictoryArmed;
    private GameObject cleanupBossInstance;
    private int totalEnemyKills;
    private int spawnedBossCountByKills;
    private const float FinalRushBonusLevelInterval = 5f;
    private readonly HashSet<EnemyDifficultyTrackedEnemy> trackedEnemies = new HashSet<EnemyDifficultyTrackedEnemy>();
    private bool initialGraceStartLogged;
    private bool initialGraceEndHandled;

    public static EnemyDifficultyDirector Instance
    {
        get
        {
            if (isShuttingDown)
            {
                return null;
            }

            if (instance == null)
            {
                instance = FindObjectOfType<EnemyDifficultyDirector>();
            }

            return instance;
        }
    }

    public static EnemyDifficultyDirector GetOrCreateInstance()
    {
        if (isShuttingDown)
        {
            return null;
        }

        if (Instance != null)
        {
            return instance;
        }

        GameObject directorObject = new GameObject("EnemyDifficultyDirector");
        instance = directorObject.AddComponent<EnemyDifficultyDirector>();
        return instance;
    }

    public event Action OnVictory;
    public event Action OnInitialGraceEnded;

    public DifficultyPhase CurrentPhase => currentPhase;
    public float ElapsedTime => elapsedTime;
    public float CombatProgressionElapsedTime => Mathf.Max(0f, elapsedTime - Mathf.Max(0f, initialGraceDuration));
    public float RemainingInitialGraceTime => Mathf.Max(0f, Mathf.Max(0f, initialGraceDuration) - elapsedTime);
    public int CurrentNormalDifficultyLevel => Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(Mathf.Max(0f, elapsedTime), Mathf.Max(0f, finalRushStartTime)) / Mathf.Max(1f, normalLevelInterval)));
    public int CurrentFinalRushDifficultyLevel => ResolveCurrentFinalRushDifficultyLevel();
    public int CurrentDifficultyLevel => Mathf.Max(0, CurrentNormalDifficultyLevel + CurrentFinalRushDifficultyLevel);
    public float FinalRushEndTime => finalRushStartTime + Mathf.Max(0f, finalRushDuration);
    public bool IsFinalRushActive => currentPhase == DifficultyPhase.FinalRush;
    public bool CanSpawnEnemies => currentPhase == DifficultyPhase.Normal || currentPhase == DifficultyPhase.FinalRush;
    public bool ShouldAllowSpawning => CanSpawnEnemies;
    public bool IsInitialGraceActive => elapsedTime < Mathf.Max(0f, initialGraceDuration);

    /// <summary>
    /// Reconfigures the battle clock before gameplay starts. This is used by
    /// lightweight entry scenes such as the two-minute exhibition demo.
    /// </summary>
    public void ConfigureTimeline(float levelIntervalSeconds, float finalRushStartSeconds, float finalRushDurationSeconds)
    {
        normalLevelInterval = Mathf.Max(1f, levelIntervalSeconds);
        finalRushStartTime = Mathf.Max(0f, finalRushStartSeconds);
        finalRushDuration = Mathf.Max(0f, finalRushDurationSeconds);
    }

    public float CurrentHpMultiplier => ResolveCombatStatMultiplier(baseHealthMultiplier, hpGrowthPerLevel, finalRushHpMultiplier, applyInitialGraceMultiplier: true);
    public float CurrentAttackMultiplier => ResolveCombatStatMultiplier(baseAttackMultiplier, attackGrowthPerLevel, finalRushAttackMultiplier, applyInitialGraceMultiplier: true);
    public float CurrentDefenseMultiplier => ResolveCombatStatMultiplier(baseDefenseMultiplier, defenseGrowthPerLevel, finalRushDefenseMultiplier, applyInitialGraceMultiplier: true);
    public float CurrentSpecialAttackMultiplier => ResolveCombatStatMultiplier(baseSpecialAttackMultiplier, specialAttackGrowthPerLevel, finalRushSpecialAttackMultiplier, applyInitialGraceMultiplier: true);
    public float CurrentSpecialDefenseMultiplier => ResolveCombatStatMultiplier(baseSpecialDefenseMultiplier, specialDefenseGrowthPerLevel, finalRushSpecialDefenseMultiplier, applyInitialGraceMultiplier: true);
    public float CurrentSpeedMultiplier => ResolveCombatStatMultiplier(baseSpeedMultiplier, speedGrowthPerLevel, finalRushSpeedMultiplier, applyInitialGraceMultiplier: false);
    public float CurrentSpawnIntervalMultiplier => ResolveSpawnIntervalMultiplier();
    public int CurrentExtraMaxAlive => ResolveExtraMaxAlive();
    public int CurrentSpawnBatchCount => ResolveSpawnBatchCount();
    public float NormalEnemyDamageMultiplier => Mathf.Clamp(normalEnemyDamageMultiplier, 0.01f, 1f);
    public float BossDamageMultiplier => Mathf.Clamp(bossDamageMultiplier, 0.01f, 1f);
    public float WrongTimeDamageMultiplier => Mathf.Max(1f, wrongTimeDamageMultiplier);
    public float PlayerHitInvincibleDuration => Mathf.Max(0f, playerHitInvincibleDuration);
    public float BossAttackRecoveryBonus => Mathf.Max(0f, bossAttackRecoveryBonus);
    public float EarlyGameSpawnMultiplier => Mathf.Clamp(earlyGameSpawnMultiplier, 0.1f, 1f);

    public static float ResolveEnemyOutgoingDamageMultiplier(GameObject enemy)
    {
        MonsterIdentity identity = enemy != null ? enemy.GetComponentInParent<MonsterIdentity>() : null;
        if (identity == null)
        {
            return 1f;
        }

        EnemyDifficultyDirector director = Instance;
        if (identity.rank == MonsterRank.Boss)
        {
            return director != null ? director.BossDamageMultiplier : 0.85f;
        }

        return director != null ? director.NormalEnemyDamageMultiplier : 0.8f;
    }

    public static float ResolveWrongTimeDamageMultiplier()
    {
        EnemyDifficultyDirector director = Instance;
        return director != null ? director.WrongTimeDamageMultiplier : 1.5f;
    }

    public static float ResolvePlayerHitInvincibleDuration()
    {
        EnemyDifficultyDirector director = Instance;
        return director != null ? director.PlayerHitInvincibleDuration : 0.8f;
    }

    public static float ResolveBossAttackRecoveryBonus(GameObject enemy)
    {
        MonsterIdentity identity = enemy != null ? enemy.GetComponentInParent<MonsterIdentity>() : null;
        if (identity == null || identity.rank != MonsterRank.Boss)
        {
            return 0f;
        }

        EnemyDifficultyDirector director = Instance;
        return director != null ? director.BossAttackRecoveryBonus : 0.7f;
    }

    private void Awake()
    {
        isShuttingDown = false;

        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        BattleTimerUI timerUi = BattleTimerUI.EnsureInstance();
        if (timerUi == null && debugLogs)
        {
            Debug.LogWarning("[EnemyDifficultyDirector] BattleTimerUI was not found in scene. Use Tools/YY/Battle/Create Battle Timer And Difficulty Director to create it.", this);
        }

        LogDifficultySnapshot();
        LogInitialGraceStartIfNeeded();
    }

    private void Update()
    {
        UpdateTimeline();
        CheckVictoryFromRemainingEnemies();
    }

    public void ApplyDifficultyToEnemy(GameObject enemy, bool recaptureBaseStats = true, bool preserveCurrentHealth = false)
    {
        if (enemy == null)
        {
            return;
        }

        CombatStats stats = enemy.GetComponent<CombatStats>();
        if (stats == null)
        {
            return;
        }

        EnemyDifficultyTrackedEnemy trackedEnemy = enemy.GetComponent<EnemyDifficultyTrackedEnemy>();
        if (trackedEnemy == null)
        {
            trackedEnemy = enemy.AddComponent<EnemyDifficultyTrackedEnemy>();
        }

        MonsterIdentity identity = enemy.GetComponent<MonsterIdentity>();
        trackedEnemy.Initialize(this, identity != null && identity.rank == MonsterRank.Boss);

        if (recaptureBaseStats || !trackedEnemy.HasBaseStats)
        {
            trackedEnemy.CaptureBaseStats(stats);
        }

        float baseHp = trackedEnemy.BaseHealth;
        float baseAttack = trackedEnemy.BaseAttack;
        float baseDefense = trackedEnemy.BaseDefense;
        float baseSpecialAttack = trackedEnemy.BaseSpecialAttack;
        float baseSpecialDefense = trackedEnemy.BaseSpecialDefense;
        float baseSpeed = trackedEnemy.BaseSpeed;
        float hpMultiplier = CurrentHpMultiplier;
        float attackMultiplier = CurrentAttackMultiplier;
        float defenseMultiplier = CurrentDefenseMultiplier;
        float specialAttackMultiplier = CurrentSpecialAttackMultiplier;
        float specialDefenseMultiplier = CurrentSpecialDefenseMultiplier;
        float speedMultiplier = CurrentSpeedMultiplier;
        float previousCurrentHealth = ResolveCurrentHealth(enemy, stats);

        stats.maxHealth = Mathf.Max(1f, Mathf.Round(baseHp * hpMultiplier));
        stats.physicalAttack = Mathf.Max(0f, Mathf.Round(baseAttack * attackMultiplier));
        stats.specialAttack = Mathf.Max(0f, Mathf.Round(baseSpecialAttack * specialAttackMultiplier));
        stats.physicalDefense = Mathf.Max(0f, Mathf.Round(baseDefense * defenseMultiplier));
        stats.specialDefense = Mathf.Max(0f, Mathf.Round(baseSpecialDefense * specialDefenseMultiplier));
        stats.speed = Mathf.Max(0.1f, RoundToDecimals(baseSpeed * speedMultiplier, 2));

        BattleResourceBank resourceBank = enemy.GetComponent<BattleResourceBank>();
        CombatHealth combatHealth = enemy.GetComponent<CombatHealth>();
        if (resourceBank != null)
        {
            resourceBank.maxHealth = stats.maxHealth;
            resourceBank.currentHealth = preserveCurrentHealth
                ? Mathf.Min(previousCurrentHealth, stats.maxHealth)
                : stats.maxHealth;
        }

        if (combatHealth != null)
        {
            combatHealth.stats = stats;
            combatHealth.resourceBank = resourceBank;
            combatHealth.currentHealth = preserveCurrentHealth
                ? Mathf.Min(previousCurrentHealth, stats.maxHealth)
                : stats.maxHealth;
        }

        if (debugScaleLogs)
        {
            Debug.Log(
                "[EnemyScaling] " +
                $"name={enemy.name} species={(identity != null ? identity.species.ToString() : "Unknown")} rank={(identity != null ? identity.rank.ToString() : "Unknown")} phase={currentPhase} " +
                $"baseHP={baseHp:F1} difficultyHP={hpMultiplier:F2} finalHP={stats.maxHealth:F1} " +
                $"baseATK={baseAttack:F1} difficultyATK={attackMultiplier:F2} finalATK={stats.physicalAttack:F1} " +
                $"baseDEF={baseDefense:F1} difficultyDEF={defenseMultiplier:F2} finalDEF={stats.physicalDefense:F1} " +
                $"baseSATK={baseSpecialAttack:F1} difficultySATK={specialAttackMultiplier:F2} finalSATK={stats.specialAttack:F1} " +
                $"baseSDEF={baseSpecialDefense:F1} difficultySDEF={specialDefenseMultiplier:F2} finalSDEF={stats.specialDefense:F1} " +
                $"baseSPD={baseSpeed:F2} difficultySPD={speedMultiplier:F2} finalSPD={stats.speed:F2}",
                enemy);
        }
    }

    public void NotifyBossDefeated(GameObject defeatedBoss)
    {
        if (currentPhase == DifficultyPhase.Victory)
        {
            return;
        }

        if (!spawnStoppedBossVictoryArmed || !finalRushStarted || !finalRushEnded)
        {
            return;
        }

        if (bossDefeated)
        {
            return;
        }

        if (cleanupBossInstance == null || defeatedBoss != cleanupBossInstance)
        {
            return;
        }

        Debug.Log(
            $"[CleanupBossVictory] cleanup boss body died boss={(defeatedBoss != null ? defeatedBoss.name : "null")} cleanupBossInstanceMatched={defeatedBoss == cleanupBossInstance} remainingSplitChildrenIgnored=true victoryTriggered=true",
            this);
        bossDefeated = true;
        spawnStoppedBossVictoryArmed = false;
        cleanupBossInstance = null;
        SetVictory("CleanupBossDefeatedAfterFinalRush");
    }

    public bool NotifyEnemyKilled(bool wasBoss)
    {
        if (wasBoss)
        {
            return false;
        }

        totalEnemyKills++;
        int requiredBossSpawnCount = totalEnemyKills / Mathf.Max(1, killsPerBossSpawn);
        bool shouldSpawnBoss = requiredBossSpawnCount > spawnedBossCountByKills;
        if (shouldSpawnBoss)
        {
            spawnedBossCountByKills++;
        }

        if (debugLogs && (shouldSpawnBoss || totalEnemyKills % Mathf.Max(1, killsPerBossSpawn) == 0))
        {
            Debug.Log(
                "[EnemyKillCount] " +
                $"kills={totalEnemyKills} spawnBoss={shouldSpawnBoss} spawnedBossCountByKills={spawnedBossCountByKills}",
                this);
        }

        return shouldSpawnBoss;
    }

    public string BuildTimerText()
    {
        switch (currentPhase)
        {
            case DifficultyPhase.Normal:
                return FormatSeconds(Mathf.Max(0f, finalRushStartTime - elapsedTime));
            case DifficultyPhase.FinalRush:
                return "FINAL RUSH " + FormatSeconds(Mathf.Max(0f, FinalRushEndTime - elapsedTime));
            case DifficultyPhase.SpawnStopped:
                return "CLEAR REMAINING ENEMIES";
            case DifficultyPhase.Victory:
                return "VICTORY";
            default:
                return FormatSeconds(Mathf.Max(0f, finalRushStartTime - elapsedTime));
        }
    }

    public string BuildPhaseSummaryText()
    {
        switch (currentPhase)
        {
            case DifficultyPhase.FinalRush:
                return "FINAL RUSH";
            case DifficultyPhase.SpawnStopped:
                return "SPAWN STOPPED";
            case DifficultyPhase.Victory:
                return "VICTORY";
            default:
                return "NORMAL";
        }
    }

    private void UpdateTimeline()
    {
        if (currentPhase == DifficultyPhase.SpawnStopped || currentPhase == DifficultyPhase.Victory)
        {
            return;
        }

        float previousElapsedTime = elapsedTime;
        elapsedTime += Time.deltaTime;

        if (!initialGraceEndHandled &&
            previousElapsedTime < Mathf.Max(0f, initialGraceDuration) &&
            elapsedTime >= Mathf.Max(0f, initialGraceDuration))
        {
            HandleInitialGraceEnd();
        }

        if (currentPhase == DifficultyPhase.Normal && elapsedTime >= finalRushStartTime)
        {
            currentPhase = DifficultyPhase.FinalRush;
            finalRushVictoryArmed = false;
            finalRushStarted = true;
            if (!finalRushLogged)
            {
                finalRushLogged = true;
                Log("[FinalRush] started at elapsed=" + elapsedTime.ToString("F1"));
            }
        }

        if (currentPhase == DifficultyPhase.FinalRush && elapsedTime >= FinalRushEndTime)
        {
            currentPhase = DifficultyPhase.SpawnStopped;
            elapsedTime = FinalRushEndTime;
            finalRushVictoryArmed = false;
            finalRushEnded = true;
            if (!spawnStoppedLogged)
            {
                spawnStoppedLogged = true;
                Log("[SpawnStopped] final rush ended, stop spawning");
            }
        }
    }

    private void CheckVictoryFromRemainingEnemies()
    {
        if (!debugLogs || currentPhase != DifficultyPhase.SpawnStopped || victoryTriggered)
        {
            return;
        }

        if (Time.time < lastRemainingEnemyCheckTime + Mathf.Max(0.1f, remainingEnemyCheckInterval))
        {
            return;
        }

        lastRemainingEnemyCheckTime = Time.time;
        int aliveEnemies = CountAliveEnemiesForVictory();
        Debug.Log(
            $"[VictoryCheck] aliveEnemies={aliveEnemies} phase={currentPhase} cleanupBossArmed={spawnStoppedBossVictoryArmed} cleanupBoss={(cleanupBossInstance != null ? cleanupBossInstance.name : "null")}",
            this);
    }

    private void SetVictory(string reason)
    {
        if (victoryTriggered || currentPhase == DifficultyPhase.Victory)
        {
            return;
        }

        victoryTriggered = true;
        currentPhase = DifficultyPhase.Victory;
        Log("[GameVictory] reason=" + reason);
        OnVictory?.Invoke();
    }

    public void ArmFinalRushVictory()
    {
        finalRushVictoryArmed = false;
    }

    public void ArmSpawnStoppedBossVictory(GameObject cleanupBoss)
    {
        if (currentPhase != DifficultyPhase.SpawnStopped || currentPhase == DifficultyPhase.Victory)
        {
            return;
        }

        if (!finalRushStarted || !finalRushEnded || cleanupBoss == null)
        {
            return;
        }

        cleanupBossInstance = cleanupBoss;
        spawnStoppedBossVictoryArmed = true;
        bossDefeated = false;
        if (debugLogs)
        {
            Debug.Log($"[CleanupBossVictory] cleanup boss armed boss={cleanupBoss.name}", this);
        }
    }

    public bool HasFinalRushStarted => finalRushStarted;
    public bool HasFinalRushEnded => finalRushEnded;
    public GameObject CleanupBossInstance => cleanupBossInstance;

    private int CountAliveEnemiesForVictory()
    {
        EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
        if (spawner != null)
        {
            return spawner.CountAliveEnemiesForVictory();
        }

        EnemyController[] enemies = FindObjectsOfType<EnemyController>();
        int count = 0;
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyController enemy = enemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                continue;
            }

            CombatHealth combatHealth = enemy.GetComponent<CombatHealth>();
            if (combatHealth != null && combatHealth.IsDead)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private float ResolvePerSpawnMultiplier(float normalMultiplier, float finalRushMultiplier)
    {
        if (currentPhase == DifficultyPhase.FinalRush)
        {
            return Mathf.Max(0.01f, normalMultiplier) * Mathf.Max(0.01f, finalRushMultiplier);
        }

        return Mathf.Max(0.01f, normalMultiplier);
    }

    private float ResolveCombatStatMultiplier(float baseMultiplier, float growthPerLevel, float finalRushMultiplier, bool applyInitialGraceMultiplier)
    {
        float resolvedBaseMultiplier = Mathf.Max(0.01f, baseMultiplier);
        float growthMultiplier = 1f + CurrentCombatDifficultyLevel * Mathf.Max(0f, growthPerLevel);
        float combinedMultiplier = ResolvePerSpawnMultiplier(resolvedBaseMultiplier * growthMultiplier, finalRushMultiplier);
        if (applyInitialGraceMultiplier && IsInitialGraceActive)
        {
            combinedMultiplier *= Mathf.Clamp(initialMonsterStrengthMultiplier, 0.1f, 1f);
        }

        return Mathf.Max(0.01f, combinedMultiplier);
    }

    private float ResolveSpawnIntervalMultiplier()
    {
        float multiplier = 1f / (1f + CurrentDifficultyLevel * Mathf.Max(0f, spawnRateGrowthPerLevel));
        if (IsInitialGraceActive)
        {
            multiplier /= EarlyGameSpawnMultiplier;
        }

        if (currentPhase == DifficultyPhase.FinalRush)
        {
            multiplier *= Mathf.Max(0.01f, finalRushSpawnIntervalMultiplier);
        }

        return Mathf.Max(0.05f, multiplier);
    }

    private int ResolveExtraMaxAlive()
    {
        int extra = Mathf.Max(0, CurrentDifficultyLevel * Mathf.Max(0, extraMaxAlivePerLevel));
        if (currentPhase == DifficultyPhase.FinalRush)
        {
            extra += Mathf.Max(0, finalRushExtraMaxAlive);
        }

        return extra;
    }

    private int ResolveSpawnBatchCount()
    {
        int batchCount = 2 + Mathf.Max(0, CurrentDifficultyLevel) / 2;
        if (currentPhase == DifficultyPhase.FinalRush)
        {
            batchCount += 5;
        }

        int maxBatchCount = currentPhase == DifficultyPhase.FinalRush ? 20 : 12;
        return Mathf.Clamp(batchCount, 2, maxBatchCount);
    }

    private int ResolveCurrentFinalRushDifficultyLevel()
    {
        if (elapsedTime < finalRushStartTime)
        {
            return 0;
        }

        float clampedFinalRushElapsed = Mathf.Max(0f, Mathf.Min(elapsedTime, FinalRushEndTime) - finalRushStartTime);
        return Mathf.Max(0, Mathf.FloorToInt(clampedFinalRushElapsed / Mathf.Max(0.1f, FinalRushBonusLevelInterval)));
    }

    private static string FormatSeconds(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainSeconds = totalSeconds % 60;
        return minutes.ToString("00") + ":" + remainSeconds.ToString("00");
    }

    private void LogDifficultySnapshot()
    {
        Log(
            "[EnemyDifficulty] " +
            $"elapsed={elapsedTime:F1} combatElapsed={CombatProgressionElapsedTime:F1} phase={currentPhase} level={CurrentDifficultyLevel} combatLevel={CurrentCombatDifficultyLevel} " +
            $"hpMul={CurrentHpMultiplier:F2} atkMul={CurrentAttackMultiplier:F2} defMul={CurrentDefenseMultiplier:F2} " +
            $"sAtkMul={CurrentSpecialAttackMultiplier:F2} sDefMul={CurrentSpecialDefenseMultiplier:F2} spdMul={CurrentSpeedMultiplier:F2} " +
            $"spawnIntervalMul={CurrentSpawnIntervalMultiplier:F2} extraMaxAlive={CurrentExtraMaxAlive} " +
            $"spawnStopped={!CanSpawnEnemies}");
    }

    private void LogInitialGraceStartIfNeeded()
    {
        if (initialGraceStartLogged || initialGraceDuration <= 0f || initialMonsterStrengthMultiplier >= 1f)
        {
            return;
        }

        initialGraceStartLogged = true;
        if (debugLogs)
        {
            Debug.Log(
                $"[MonsterStrength] Initial grace period started: multiplier={initialMonsterStrengthMultiplier:F2}, duration={initialGraceDuration:F1}s.",
                this);
        }
    }

    private void HandleInitialGraceEnd()
    {
        if (initialGraceEndHandled)
        {
            return;
        }

        initialGraceEndHandled = true;
        RefreshTrackedEnemiesAfterGraceEnd();
        OnInitialGraceEnded?.Invoke();
        if (debugLogs)
        {
            Debug.Log("[MonsterStrength] Grace period ended. Normal strength restored; progression timer started.", this);
        }
    }

    private void RefreshTrackedEnemiesAfterGraceEnd()
    {
        if (trackedEnemies.Count == 0)
        {
            return;
        }

        List<EnemyDifficultyTrackedEnemy> refreshBuffer = new List<EnemyDifficultyTrackedEnemy>(trackedEnemies);
        for (int i = 0; i < refreshBuffer.Count; i++)
        {
            EnemyDifficultyTrackedEnemy trackedEnemy = refreshBuffer[i];
            if (trackedEnemy == null || !trackedEnemy.ShouldRefreshDifficulty)
            {
                continue;
            }

            ApplyDifficultyToEnemy(trackedEnemy.gameObject, recaptureBaseStats: false, preserveCurrentHealth: true);
        }
    }

    internal void RegisterTrackedEnemy(EnemyDifficultyTrackedEnemy trackedEnemy)
    {
        if (trackedEnemy == null)
        {
            return;
        }

        trackedEnemies.Add(trackedEnemy);
    }

    internal void UnregisterTrackedEnemy(EnemyDifficultyTrackedEnemy trackedEnemy)
    {
        if (trackedEnemy == null)
        {
            return;
        }

        trackedEnemies.Remove(trackedEnemy);
    }

    private int CurrentCombatDifficultyLevel => Mathf.Max(0, CurrentCombatNormalDifficultyLevel + CurrentCombatFinalRushDifficultyLevel);

    private int CurrentCombatNormalDifficultyLevel =>
        Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(Mathf.Max(0f, CombatProgressionElapsedTime), Mathf.Max(0f, finalRushStartTime)) / Mathf.Max(1f, normalLevelInterval)));

    private int CurrentCombatFinalRushDifficultyLevel => ResolveCurrentCombatFinalRushDifficultyLevel();

    private int ResolveCurrentCombatFinalRushDifficultyLevel()
    {
        if (CombatProgressionElapsedTime < finalRushStartTime)
        {
            return 0;
        }

        float clampedFinalRushElapsed = Mathf.Max(0f, Mathf.Min(CombatProgressionElapsedTime, FinalRushEndTime) - finalRushStartTime);
        return Mathf.Max(0, Mathf.FloorToInt(clampedFinalRushElapsed / Mathf.Max(0.1f, FinalRushBonusLevelInterval)));
    }

    private static float ResolveCurrentHealth(GameObject enemy, CombatStats stats)
    {
        if (enemy == null)
        {
            return stats != null ? stats.maxHealth : 0f;
        }

        CombatHealth combatHealth = enemy.GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            return Mathf.Max(0f, combatHealth.currentHealth);
        }

        BattleResourceBank resourceBank = enemy.GetComponent<BattleResourceBank>();
        if (resourceBank != null)
        {
            return Mathf.Max(0f, resourceBank.currentHealth);
        }

        return stats != null ? Mathf.Max(0f, stats.maxHealth) : 0f;
    }

    private void Log(string message)
    {
        if (!debugLogs)
        {
            return;
        }

        Debug.Log(message, this);
    }

    private static float RoundToDecimals(float value, int decimals)
    {
        float multiplier = Mathf.Pow(10f, Mathf.Max(0, decimals));
        return Mathf.Round(value * multiplier) / multiplier;
    }

    private void OnApplicationQuit()
    {
        isShuttingDown = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            isShuttingDown = true;
            instance = null;
        }
    }

}

public sealed class EnemyDifficultyTrackedEnemy : MonoBehaviour
{
    private EnemyDifficultyDirector director;
    private CombatHealth combatHealth;
    private bool isBoss;
    private bool subscribed;
    private bool deathNotified;
    private bool hasBaseStats;
    private float baseHealth;
    private float baseAttack;
    private float baseDefense;
    private float baseSpecialAttack;
    private float baseSpecialDefense;
    private float baseSpeed;

    public void Initialize(EnemyDifficultyDirector owner, bool boss)
    {
        director = owner;
        isBoss = boss;
        combatHealth = GetComponent<CombatHealth>();
        deathNotified = false;
        director?.RegisterTrackedEnemy(this);

        if (combatHealth == null || subscribed)
        {
            return;
        }

        combatHealth.Died += HandleDied;
        subscribed = true;
    }

    public bool HasBaseStats => hasBaseStats;
    public float BaseHealth => baseHealth;
    public float BaseAttack => baseAttack;
    public float BaseDefense => baseDefense;
    public float BaseSpecialAttack => baseSpecialAttack;
    public float BaseSpecialDefense => baseSpecialDefense;
    public float BaseSpeed => baseSpeed;
    public bool ShouldRefreshDifficulty => isActiveAndEnabled && gameObject.activeInHierarchy && (combatHealth == null || !combatHealth.IsDead);

    public void CaptureBaseStats(CombatStats stats)
    {
        if (stats == null)
        {
            return;
        }

        hasBaseStats = true;
        baseHealth = stats.maxHealth;
        baseAttack = stats.physicalAttack;
        baseDefense = stats.physicalDefense;
        baseSpecialAttack = stats.specialAttack;
        baseSpecialDefense = stats.specialDefense;
        baseSpeed = stats.speed;
    }

    private void OnDisable()
    {
        director?.UnregisterTrackedEnemy(this);
    }

    private void OnDestroy()
    {
        director?.UnregisterTrackedEnemy(this);
        if (combatHealth != null && subscribed)
        {
            combatHealth.Died -= HandleDied;
        }
    }

    private void HandleDied(GameObject killer)
    {
        if (deathNotified)
        {
            return;
        }

        deathNotified = true;
        if (isBoss && director != null)
        {
            director.NotifyBossDefeated(gameObject);
        }
    }
}
