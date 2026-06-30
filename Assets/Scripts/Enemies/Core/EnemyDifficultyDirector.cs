using System;
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

    [Header("Timeline")]
    [SerializeField, Min(1f)] private float normalLevelInterval = 10f;
    [SerializeField, Min(0f)] private float finalRushStartTime = 600f;
    [SerializeField, Min(0f)] private float finalRushDuration = 180f;

    [Header("Per-Level Growth")]
    [SerializeField, Min(0f)] private float hpGrowthPerLevel = 0.10f;
    [SerializeField, Min(0f)] private float attackGrowthPerLevel = 0.12f;
    [SerializeField, Min(0f)] private float defenseGrowthPerLevel = 0.10f;
    [SerializeField, Min(0f)] private float speedGrowthPerLevel = 0.06f;

    [Header("Final Rush Multipliers")]
    [SerializeField, Min(0.01f)] private float finalRushHpMultiplier = 2.5f;
    [SerializeField, Min(0.01f)] private float finalRushAttackMultiplier = 2.2f;
    [SerializeField, Min(0.01f)] private float finalRushDefenseMultiplier = 1.8f;
    [SerializeField, Min(0.01f)] private float finalRushSpeedMultiplier = 1.4f;

    [Header("Spawn Pressure")]
    [SerializeField, Min(0f)] private float spawnRateGrowthPerLevel = 0.08f;
    [SerializeField, Min(0)] private int extraMaxAlivePerLevel = 2;
    [SerializeField, Min(0.01f)] private float finalRushSpawnIntervalMultiplier = 0.25f;
    [SerializeField, Min(0)] private int finalRushExtraMaxAlive = 40;

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
    private bool bossDefeated;
    private int totalEnemyKills;
    private int spawnedBossCountByKills;
    private const float FinalRushBonusLevelInterval = 5f;

    public static EnemyDifficultyDirector Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<EnemyDifficultyDirector>();
            }

            return instance;
        }
    }

    public static EnemyDifficultyDirector GetOrCreateInstance()
    {
        if (Instance != null)
        {
            return instance;
        }

        GameObject directorObject = new GameObject("EnemyDifficultyDirector");
        instance = directorObject.AddComponent<EnemyDifficultyDirector>();
        return instance;
    }

    public event Action OnVictory;

    public DifficultyPhase CurrentPhase => currentPhase;
    public float ElapsedTime => elapsedTime;
    public int CurrentNormalDifficultyLevel => Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(Mathf.Max(0f, elapsedTime), Mathf.Max(0f, finalRushStartTime)) / Mathf.Max(1f, normalLevelInterval)));
    public int CurrentFinalRushDifficultyLevel => ResolveCurrentFinalRushDifficultyLevel();
    public int CurrentDifficultyLevel => Mathf.Max(0, CurrentNormalDifficultyLevel + CurrentFinalRushDifficultyLevel);
    public float FinalRushEndTime => finalRushStartTime + Mathf.Max(0f, finalRushDuration);
    public bool IsFinalRushActive => currentPhase == DifficultyPhase.FinalRush;
    public bool CanSpawnEnemies => currentPhase == DifficultyPhase.Normal || currentPhase == DifficultyPhase.FinalRush;
    public bool ShouldAllowSpawning => CanSpawnEnemies;

    public float CurrentHpMultiplier => ResolvePerSpawnMultiplier(1f + CurrentDifficultyLevel * hpGrowthPerLevel, finalRushHpMultiplier);
    public float CurrentAttackMultiplier => ResolvePerSpawnMultiplier(1f + CurrentDifficultyLevel * attackGrowthPerLevel, finalRushAttackMultiplier);
    public float CurrentDefenseMultiplier => ResolvePerSpawnMultiplier(1f + CurrentDifficultyLevel * defenseGrowthPerLevel, finalRushDefenseMultiplier);
    public float CurrentSpeedMultiplier => ResolvePerSpawnMultiplier(1f + CurrentDifficultyLevel * speedGrowthPerLevel, finalRushSpeedMultiplier);
    public float CurrentSpawnIntervalMultiplier => ResolveSpawnIntervalMultiplier();
    public int CurrentExtraMaxAlive => ResolveExtraMaxAlive();
    public int CurrentSpawnBatchCount => ResolveSpawnBatchCount();

    private void Awake()
    {
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
    }

    private void Update()
    {
        UpdateTimeline();
        CheckVictoryFromRemainingEnemies();
    }

    public void ApplyDifficultyToEnemy(GameObject enemy)
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

        float hpMultiplier = CurrentHpMultiplier;
        float attackMultiplier = CurrentAttackMultiplier;
        float defenseMultiplier = CurrentDefenseMultiplier;
        float speedMultiplier = CurrentSpeedMultiplier;

        stats.maxHealth = Mathf.Max(1f, Mathf.Round(stats.maxHealth * hpMultiplier));
        stats.physicalAttack = Mathf.Max(0f, Mathf.Round(stats.physicalAttack * attackMultiplier));
        stats.specialAttack = Mathf.Max(0f, Mathf.Round(stats.specialAttack * attackMultiplier));
        stats.physicalDefense = Mathf.Max(0f, Mathf.Round(stats.physicalDefense * defenseMultiplier));
        stats.specialDefense = Mathf.Max(0f, Mathf.Round(stats.specialDefense * defenseMultiplier));
        stats.speed = Mathf.Max(0.1f, RoundToDecimals(stats.speed * speedMultiplier, 2));

        BattleResourceBank resourceBank = enemy.GetComponent<BattleResourceBank>();
        CombatHealth combatHealth = enemy.GetComponent<CombatHealth>();
        if (resourceBank != null)
        {
            resourceBank.maxHealth = stats.maxHealth;
            resourceBank.currentHealth = stats.maxHealth;
        }

        if (combatHealth != null)
        {
            combatHealth.stats = stats;
            combatHealth.resourceBank = resourceBank;
            combatHealth.currentHealth = stats.maxHealth;
        }

        EnemyDifficultyTrackedEnemy trackedEnemy = enemy.GetComponent<EnemyDifficultyTrackedEnemy>();
        if (trackedEnemy == null)
        {
            trackedEnemy = enemy.AddComponent<EnemyDifficultyTrackedEnemy>();
        }

        MonsterIdentity identity = enemy.GetComponent<MonsterIdentity>();
        trackedEnemy.Initialize(this, identity != null && identity.rank == MonsterRank.Boss);

        if (debugScaleLogs)
        {
            Debug.Log(
                "[EnemyDifficultyScale] " +
                $"enemy={enemy.name} phase={currentPhase} level={CurrentDifficultyLevel} " +
                $"hpMultiplier={hpMultiplier:F2} attackMultiplier={attackMultiplier:F2} " +
                $"defenseMultiplier={defenseMultiplier:F2} speedMultiplier={speedMultiplier:F2}",
                enemy);
        }
    }

    public void NotifyBossDefeated()
    {
        if (bossDefeated || currentPhase == DifficultyPhase.Victory)
        {
            return;
        }

        bossDefeated = true;
        SetVictory("BossDefeated");
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

        elapsedTime += Time.deltaTime;

        if (currentPhase == DifficultyPhase.Normal && elapsedTime >= finalRushStartTime)
        {
            currentPhase = DifficultyPhase.FinalRush;
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
            if (!spawnStoppedLogged)
            {
                spawnStoppedLogged = true;
                Log("[SpawnStopped] final rush ended, stop spawning");
            }
        }
    }

    private void CheckVictoryFromRemainingEnemies()
    {
        if (currentPhase != DifficultyPhase.SpawnStopped)
        {
            return;
        }

        if (Time.time < lastRemainingEnemyCheckTime + Mathf.Max(0.1f, remainingEnemyCheckInterval))
        {
            return;
        }

        lastRemainingEnemyCheckTime = Time.time;
        int aliveEnemies = CountAliveEnemiesForVictory();
        if (debugLogs)
        {
            Debug.Log($"[VictoryCheck] aliveEnemies={aliveEnemies} phase={currentPhase}", this);
        }

        if (aliveEnemies <= 0)
        {
            SetVictory("AllEnemiesClearedAfterFinalRush");
        }
    }

    private void SetVictory(string reason)
    {
        if (currentPhase == DifficultyPhase.Victory)
        {
            return;
        }

        currentPhase = DifficultyPhase.Victory;
        Log("[GameVictory] reason=" + reason);
        OnVictory?.Invoke();
    }

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

    private float ResolveSpawnIntervalMultiplier()
    {
        float multiplier = 1f / (1f + CurrentDifficultyLevel * Mathf.Max(0f, spawnRateGrowthPerLevel));
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
            $"elapsed={elapsedTime:F1} phase={currentPhase} level={CurrentDifficultyLevel} " +
            $"hpMul={CurrentHpMultiplier:F2} atkMul={CurrentAttackMultiplier:F2} " +
            $"defMul={CurrentDefenseMultiplier:F2} spdMul={CurrentSpeedMultiplier:F2} " +
            $"spawnIntervalMul={CurrentSpawnIntervalMultiplier:F2} extraMaxAlive={CurrentExtraMaxAlive} " +
            $"spawnStopped={!CanSpawnEnemies}");
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
}

public sealed class EnemyDifficultyTrackedEnemy : MonoBehaviour
{
    private EnemyDifficultyDirector director;
    private CombatHealth combatHealth;
    private bool isBoss;
    private bool subscribed;
    private bool deathNotified;

    public void Initialize(EnemyDifficultyDirector owner, bool boss)
    {
        director = owner;
        isBoss = boss;
        combatHealth = GetComponent<CombatHealth>();

        if (combatHealth == null || subscribed)
        {
            return;
        }

        combatHealth.Died += HandleDied;
        subscribed = true;
    }

    private void OnDestroy()
    {
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
            director.NotifyBossDefeated();
        }
    }
}
