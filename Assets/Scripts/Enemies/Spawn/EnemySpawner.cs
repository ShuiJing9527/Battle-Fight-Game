using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AHD2TimeOfDay;

public class EnemySpawner : MonoBehaviour
{
    private const string EliteSplitFromTestSuffix = "[EliteSplit_FromTest]";
    private const string GroundSnapVersion = "BodyRendererDirect_20260713_01";

    private struct MonsterBaseSnapshot
    {
        public bool initialized;
        public float maxHealth;
        public float physicalAttack;
        public float physicalDefense;
        public float specialAttack;
        public float specialDefense;
        public float speed;
        public float luck;
        public bool hasScaleTarget;
        public Vector3 scaleTargetLocalScale;
    }

    [Header("Enemy")]
    public GameObject[] enemyPrefabs;
    public GameObject[] normalEnemyPrefabs;
    public GameObject[] eliteEnemyPrefabs;
    public GameObject[] bossEnemyPrefabs;
    public bool useRuntimeRankOverride = true;

    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private Transform[] enemySpawnPoints;
    [SerializeField] private bool debugMonsterSpawnState = true;

    [Header("Legacy Spawn")]
    [Tooltip("Base refill check interval used when alive normal monsters fall below the base amount.")]
    public float spawnInterval = 3f;
    public float startDelay = 2f;
    [Tooltip("Legacy field kept for compatibility. Elite now uses the random interval range below.")]
    [HideInInspector] public float eliteSpawnInterval = 30f;

    [Header("Normal Monsters")]
    public int baseNormalMonsterCount = 5;
    public int maxNormalMonsterCount = 50;
    public float normalReinforceIntervalMin = 5f;
    public float normalReinforceIntervalMax = 10f;
    public int normalReinforceCountMin = 1;
    public int normalReinforceCountMax = 5;

    [Header("Monster Stat Growth")]
    [Tooltip("Base HP multiplier applied before time growth, rank multipliers, and special boss phase multipliers.")]
    [Min(0.01f)] public float baseHealthMultiplier = 1f;
    [Tooltip("Base physical attack multiplier applied before time growth, rank multipliers, and special boss phase multipliers.")]
    [Min(0.01f)] public float baseAttackMultiplier = 1f;
    [Tooltip("Base physical defense multiplier applied before time growth, rank multipliers, and special boss phase multipliers.")]
    [Min(0.01f)] public float baseDefenseMultiplier = 1f;
    [Tooltip("Base special attack multiplier applied before time growth, rank multipliers, and special boss phase multipliers.")]
    [Min(0.01f)] public float baseSpecialAttackMultiplier = 1f;
    [Tooltip("Base special defense multiplier applied before time growth, rank multipliers, and special boss phase multipliers.")]
    [Min(0.01f)] public float baseSpecialDefenseMultiplier = 1f;
    [Tooltip("Base speed multiplier applied before time growth, rank multipliers, and special boss phase multipliers.")]
    [Min(0.01f)] public float baseSpeedMultiplier = 1f;
    [Tooltip("Minimum interval between extra monster growth rolls.")]
    public float monsterStatGrowthIntervalMin = 30f;
    [Tooltip("Maximum interval between extra monster growth rolls.")]
    public float monsterStatGrowthIntervalMax = 60f;
    [Tooltip("Additional global growth applied per roll to every living monster at the low end.")]
    [Range(0.01f, 0.5f)] public float monsterStatGrowthPercentMin = 0.01f;
    [Tooltip("Additional global growth applied per roll to every living monster at the high end.")]
    [Range(0.01f, 0.5f)] public float monsterStatGrowthPercentMax = 0.05f;
    [Tooltip("Current extra global monster multiplier accumulated by growth rolls. 1 means no extra growth.")]
    [Min(1f)] public float currentMonsterStatMultiplier = 1f;

    [Header("Rank Multipliers - Normal")]
    [Tooltip("Normal monster HP multiplier after base and time growth. 1 means unchanged.")]
    public float normalHealthMultiplier = 1f;
    [Tooltip("Normal monster physical attack multiplier after base and time growth. 1 means unchanged.")]
    public float normalAttackMultiplier = 1f;
    [Tooltip("Normal monster physical defense multiplier after base and time growth. 1 means unchanged.")]
    public float normalDefenseMultiplier = 1f;
    [Tooltip("Normal monster special attack multiplier after base and time growth. 1 means unchanged.")]
    public float normalMagicMultiplier = 1f;
    [Tooltip("Normal monster special defense multiplier after base and time growth. 1 means unchanged.")]
    public float normalResistanceMultiplier = 1f;
    [Tooltip("Normal monster speed multiplier after base and time growth. 1 means unchanged.")]
    public float normalSpeedMultiplier = 1f;
    [Header("Normal Visual Config")]
    [SerializeField] private bool enableNormalVisualGroundOffset = true;
    [SerializeField] private float normalVisualGroundOffsetY = -0.05f;
    [SerializeField] private float normalHealthBarOffsetY = 0.25f;

    [Header("Rank Multipliers - Elite")]
    [Tooltip("Elite monster HP multiplier after base and time growth. 1 means unchanged.")]
    public float eliteHealthMultiplier = 3f;
    [Tooltip("Elite monster physical attack multiplier after base and time growth. 1 means unchanged.")]
    public float eliteAttackMultiplier = 2f;
    [Tooltip("Elite monster physical defense multiplier after base and time growth. 1 means unchanged.")]
    public float eliteDefenseMultiplier = 1.5f;
    [Tooltip("Elite monster special attack multiplier after base and time growth. 1 means unchanged.")]
    public float eliteMagicMultiplier = 2f;
    [Tooltip("Elite monster special defense multiplier after base and time growth. 1 means unchanged.")]
    public float eliteResistanceMultiplier = 1.5f;
    [Tooltip("Elite monster speed multiplier after base and time growth. 1 means unchanged.")]
    public float eliteSpeedMultiplier = 1.1f;
    [Tooltip("Elite attack interval multiplier passed to EnemyController. Values above 1 make attacks slower in the current formula.")]
    public float eliteAttackIntervalMultiplier = 1.1f;
    [Tooltip("Elite outgoing damage multiplier passed to EnemyController. 1 means unchanged.")]
    public float eliteOutgoingDamageMultiplier = 1f;
    [SerializeField] private bool enableEliteVisualScale = true;
    [SerializeField] private float eliteVisualScaleMultiplier = 1.5f;
    [SerializeField] private bool enableEliteVisualGroundOffset = true;
    [SerializeField] private float eliteVisualGroundOffsetY = 0.25f;
    [SerializeField] private float eliteHealthBarOffsetY = 0.3f;
    [SerializeField] private bool debugEliteVisualConfig = true;

    [Header("Rank Multipliers - Boss")]
    [Tooltip("Boss HP multiplier after base and time growth. 1 means unchanged.")]
    public float bossHealthMultiplier = 10f;
    [Tooltip("Boss physical attack multiplier after base and time growth. 1 means unchanged.")]
    public float bossAttackMultiplier = 5f;
    [Tooltip("Boss physical defense multiplier after base and time growth. 1 means unchanged.")]
    public float bossDefenseMultiplier = 3f;
    [Tooltip("Boss special attack multiplier after base and time growth. 1 means unchanged.")]
    public float bossMagicMultiplier = 5f;
    [Tooltip("Boss special defense multiplier after base and time growth. 1 means unchanged.")]
    public float bossResistanceMultiplier = 3f;
    [Tooltip("Boss speed multiplier after base and time growth. 1 means unchanged.")]
    public float bossSpeedMultiplier = 1f;
    [Tooltip("Boss attack interval multiplier passed to EnemyController. Values above 1 make attacks slower in the current formula.")]
    public float bossAttackIntervalMultiplier = 1.8f;
    [Tooltip("Boss outgoing damage multiplier passed to EnemyController. 1 means unchanged.")]
    public float bossOutgoingDamageMultiplier = 1.5f;
    [SerializeField] private bool enableBossVisualScale = true;
    [SerializeField] private float bossVisualScaleMultiplier = 4.0f;
    [SerializeField] private bool enableBossVisualGroundOffset = true;
    [SerializeField] private float bossVisualGroundOffsetY = 0.85f;
    [SerializeField] private float bossVisibleGroundContactLocalYOffset = 1.0625f;
    [SerializeField] private float bossHealthBarOffsetY = 0.45f;
    [SerializeField] private bool debugBossVisualGroundOffset = true;
    [SerializeField] private bool enableBossHurtboxScale = true;
    [SerializeField] private bool useBossVisualBoundsHurtbox = true;
    [SerializeField] private float bossHurtboxRadiusMultiplier = 3.0f;
    [SerializeField] private float bossHurtboxCenterYOffset = 0f;
    [SerializeField, Min(0.1f)] private float bossHurtboxSizeMultiplier = 1f;
    [SerializeField] private Vector3 bossHurtboxExtraPadding = Vector3.zero;
    [SerializeField] private Vector3 bossHurtboxCenterOffset = Vector3.zero;
    [SerializeField, Min(0.01f)] private float bossHurtboxMinimumDepth = 0.2f;
    [SerializeField, Min(0.05f)] private float bossHurtboxRuntimeRefreshInterval = 0.25f;
    [SerializeField] private bool debugBossHurtbox = true;

    [Header("Elite")]
    public float eliteSpawnIntervalMin = 10f;
    public float eliteSpawnIntervalMax = 30f;
    [SerializeField, Min(0f)] private float eliteGuaranteedSpawnTime = 30f;
    public int maxAliveEliteCount = 1;

    [Header("Boss")]
    public float bossCheckIntervalGameHours = 6f;
    [Range(0f, 1f)] public float bossSpawnChancePerCheck = 0.25f;
    public int maxAliveBossCount = 1;

    [Header("Final Moment Boss")]
    [Tooltip("FinalRush boss HP multiplier applied on top of base, time, and rank multipliers.")]
    [SerializeField, Min(0.01f)] private float finalMomentBossHpMultiplier = 1.5f;
    [Tooltip("FinalRush boss physical and special attack multiplier applied on top of base, time, and rank multipliers.")]
    [SerializeField, Min(0.01f)] private float finalMomentBossAttackMultiplier = 1.25f;
    [Tooltip("FinalRush boss physical defense multiplier applied on top of base, time, and rank multipliers.")]
    [SerializeField, Min(0.01f)] private float finalMomentBossDefenseMultiplier = 1f;
    [Tooltip("FinalRush boss special attack multiplier applied on top of base, time, and rank multipliers.")]
    [SerializeField, Min(0.01f)] private float finalMomentBossSpecialAttackMultiplier = 1.25f;
    [Tooltip("FinalRush boss special defense multiplier applied on top of base, time, and rank multipliers.")]
    [SerializeField, Min(0.01f)] private float finalMomentBossSpecialDefenseMultiplier = 1f;
    [Tooltip("FinalRush boss speed multiplier applied on top of base, time, and rank multipliers.")]
    [SerializeField, Min(0.01f)] private float finalMomentBossSpeedMultiplier = 1f;

    [Header("Ultimate Countdown Boss")]
    [Tooltip("Extra cleanup boss HP gained per remaining non-boss enemy when spawn-stopped cleanup begins.")]
    [SerializeField, Min(0f)] private float ultimateBossHpPerRemainingEnemy = 0.05f;
    [Tooltip("Extra cleanup boss attack gained per remaining non-boss enemy when spawn-stopped cleanup begins.")]
    [SerializeField, Min(0f)] private float ultimateBossAttackPerRemainingEnemy = 0.03f;
    [Tooltip("Maximum HP multiplier reachable from the dynamic cleanup boss reinforcement.")]
    [SerializeField, Min(1f)] private float ultimateBossMaxHpMultiplier = 5f;
    [Tooltip("Maximum attack multiplier reachable from the dynamic cleanup boss reinforcement.")]
    [SerializeField, Min(1f)] private float ultimateBossMaxAttackMultiplier = 3f;
    [Tooltip("Cleanup boss speed multiplier from the dynamic spawn-stopped boss reinforcement.")]
    [SerializeField, Min(0.01f)] private float ultimateBossSpeedMultiplier = 1f;
    [Tooltip("Additional cleanup boss HP multiplier applied only to the post-FinalRush cleanup boss.")]
    [SerializeField, Min(0.01f)] private float cleanupBossHealthMultiplier = 1f;
    [Tooltip("Additional cleanup boss physical attack multiplier applied only to the post-FinalRush cleanup boss.")]
    [SerializeField, Min(0.01f)] private float cleanupBossAttackMultiplier = 1f;
    [Tooltip("Additional cleanup boss physical defense multiplier applied only to the post-FinalRush cleanup boss.")]
    [SerializeField, Min(0.01f)] private float cleanupBossDefenseMultiplier = 1f;
    [Tooltip("Additional cleanup boss special attack multiplier applied only to the post-FinalRush cleanup boss.")]
    [SerializeField, Min(0.01f)] private float cleanupBossSpecialAttackMultiplier = 1f;
    [Tooltip("Additional cleanup boss special defense multiplier applied only to the post-FinalRush cleanup boss.")]
    [SerializeField, Min(0.01f)] private float cleanupBossSpecialDefenseMultiplier = 1f;
    [Tooltip("Additional cleanup boss speed multiplier applied only to the post-FinalRush cleanup boss.")]
    [SerializeField, Min(0.01f)] private float cleanupBossSpeedMultiplier = 1f;
    [Tooltip("Additional cleanup boss visual scale multiplier applied only to the post-FinalRush cleanup boss. 1 means unchanged.")]
    [SerializeField, Min(0.01f)] private float cleanupBossScaleMultiplier = 1f;
    [Tooltip("Additional cleanup boss outgoing damage multiplier applied only to the post-FinalRush cleanup boss. 1 means unchanged.")]
    [SerializeField, Min(0.01f)] private float cleanupBossOutgoingDamageMultiplier = 1f;
    [Tooltip("Additional cleanup boss attack interval multiplier applied only to the post-FinalRush cleanup boss. Values below 1 make attacks faster.")]
    [SerializeField, Min(0.01f)] private float cleanupBossAttackIntervalMultiplier = 1f;
    [Tooltip("Additional cleanup boss reward multiplier applied only to the post-FinalRush cleanup boss. 1 means unchanged.")]
    [SerializeField, Min(0.01f)] private float cleanupBossRewardMultiplier = 1f;

    [Header("Cleanup Boss Phase Split")]
    [Tooltip("Whether the post-FinalRush cleanup boss uses HP threshold phase split instead of death split.")]
    [SerializeField] private bool cleanupBossPhaseSplitEnabled = true;
    [Tooltip("Cleanup boss HP ratio thresholds that each trigger one split wave. 0.7 means 70% HP.")]
    [SerializeField] private float[] cleanupBossSplitHealthThresholds = { 0.7f, 0.5f, 0.3f };
    [Tooltip("How many children the cleanup boss spawns at each HP threshold.")]
    [SerializeField, Min(0)] private int cleanupBossSplitCountPerThreshold = 2;
    [Tooltip("How far cleanup boss split children scatter from the boss position.")]
    [SerializeField, Min(0f)] private float cleanupBossSplitScatterRadius = 1.5f;
    [Tooltip("Target rank used for cleanup boss split children. Boss is automatically downgraded to Elite.")]
    [SerializeField] private MonsterRank cleanupBossSplitChildRank = MonsterRank.Elite;
    [Tooltip("Cleanup boss split child HP ratio applied after normal spawn-time scaling.")]
    [SerializeField, Min(0f)] private float cleanupBossSplitChildHealthRatio = 0.35f;
    [Tooltip("Cleanup boss split child physical and special attack ratio applied after normal spawn-time scaling.")]
    [SerializeField, Min(0f)] private float cleanupBossSplitChildAttackRatio = 0.55f;
    [Tooltip("Cleanup boss split child physical and special defense ratio applied after normal spawn-time scaling.")]
    [SerializeField, Min(0f)] private float cleanupBossSplitChildDefenseRatio = 0.5f;
    [Tooltip("Cleanup boss split child speed ratio applied after normal spawn-time scaling.")]
    [SerializeField, Min(0f)] private float cleanupBossSplitChildSpeedRatio = 1f;
    [Tooltip("Cleanup boss split child visual scale ratio applied after normal spawn-time scaling.")]
    [SerializeField, Min(0f)] private float cleanupBossSplitChildScaleRatio = 0.75f;
    [Tooltip("If false, cleanup boss split children cannot split again.")]
    [SerializeField] private bool cleanupBossSplitChildrenCanSplit = false;
    [Tooltip("Print one-shot cleanup boss phase split logs on initialize, threshold trigger, and body death.")]
    [SerializeField] private bool debugCleanupBossPhaseSplit = false;
    [Tooltip("Print cleanup boss scaling breakdown whenever cleanup boss stats are recalculated.")]
    [SerializeField] private bool debugCleanupBossScaling = false;

    [Header("Spawn Around Player")]
    public bool spawnAroundPlayer = true;
    public float spawnMinDistance = 6f;
    public float spawnMaxDistance = 12f;
    public float fallbackSpawnRadiusX = 10f;
    public float fallbackSpawnRadiusZ = 10f;

    [Header("Target")]
    public Transform playerTarget;
    public string playerTag = "Player";

    [Header("Enemy Collision")]
    [SerializeField] private string enemyLayerName = "Enemy";
    [SerializeField] private bool ignoreEnemySelfCollision = true;
    [SerializeField] private bool freezeEnemyVerticalPosition = false;

    [Header("Ground Snap")]
    [SerializeField] private LayerMask enemyGroundSnapLayerMask = 1;
    [SerializeField, Min(1f)] private float enemyGroundSnapRayStartHeight = 20f;
    [SerializeField, Min(1f)] private float enemyGroundSnapRayDistance = 80f;
    [SerializeField, Min(0f)] private float enemyGroundSnapTolerance = 0.01f;
    [SerializeField] private bool debugBossGroundSnap = true;

    [Header("Timed Difficulty")]
    [SerializeField] private EnemyDifficultyDirector difficultyDirector;
    [SerializeField] private bool debugDifficultySpawnLogs = false;
    [SerializeField] private bool debugScalingBreakdown = false;

    private Player2Bootstrap playerBootstrap;
    private TODController todController;
    private float previousTodTime;
    private bool todTimeInitialized;
    private float elapsedTrackedGameHours;
    private float nextBossCheckElapsedGameHours;

    private readonly List<GameObject> fallbackNormalEnemyPrefabs = new List<GameObject>();
    private readonly List<GameObject> fallbackEliteEnemyPrefabs = new List<GameObject>();
    private readonly List<GameObject> fallbackBossEnemyPrefabs = new List<GameObject>();
    private readonly List<GameObject> aliveEnemies = new List<GameObject>();
    private readonly Dictionary<int, MonsterBaseSnapshot> monsterBaseSnapshots = new Dictionary<int, MonsterBaseSnapshot>();
    private readonly HashSet<int> finalMomentBossEnemyIds = new HashSet<int>();
    private readonly Dictionary<int, UltimateBossModifiers> ultimateBossModifiersByEnemyId = new Dictionary<int, UltimateBossModifiers>();
    private int resolvedEnemyLayer = -1;
    private bool enemyLayerCollisionConfigured;
    private bool finalMomentBossTriggered;
    private bool spawnStoppedResolutionTriggered;
    private DifficultyPhase lastObservedDifficultyPhase = DifficultyPhase.Normal;
    private GameObject cleanupBossInstance;
    private Coroutine initialNormalSpawnCoroutine;
    private Coroutine normalBaseMaintenanceCoroutine;
    private Coroutine normalReinforcementCoroutine;
    private Coroutine eliteSpawnCoroutine;
    private Coroutine monsterGrowthCoroutine;
    private bool externalTestPauseActive;
    private float nextBossHurtboxRuntimeRefreshTime;
    private int lastBossHurtboxRuntimeConfigHash;
    public bool IsExternalTestPauseActive => externalTestPauseActive;

    private struct UltimateBossModifiers
    {
        public float hpMultiplier;
        public float attackMultiplier;
        public float defenseMultiplier;
        public float specialAttackMultiplier;
        public float specialDefenseMultiplier;
        public float speedMultiplier;
        public int remainingEnemyCount;
    }

    private void Start()
    {
        ResolveDifficultyDirector();
        CachePrefabPools();
        ResolveEnemyLayer();
        ConfigureEnemyLayerCollision();
        ResolvePlayerTarget();
        InitializeTodTracking();
        EnsureSpawnerCoroutinesRunning();
    }

    public void PauseSpawningForExternalTest()
    {
        if (externalTestPauseActive)
        {
            Debug.Log("[EnemySpawner] PauseSpawningForExternalTest ignored because spawner is already paused.", this);
            return;
        }

        externalTestPauseActive = true;
        StopSpawnerCoroutinesForExternalPause();
        CancelInvoke();
        Debug.Log("[EnemySpawner] PauseSpawningForExternalTest applied. Formal spawn loops paused for external testing.", this);
    }

    public void ResumeSpawningAfterExternalTest()
    {
        if (!externalTestPauseActive)
        {
            Debug.Log("[EnemySpawner] ResumeSpawningAfterExternalTest ignored because spawner is not paused.", this);
            return;
        }

        externalTestPauseActive = false;
        EnsureSpawnerCoroutinesRunning();
        Debug.Log("[EnemySpawner] ResumeSpawningAfterExternalTest applied. Formal spawn loops resumed.", this);
    }

    private void Update()
    {
        if (externalTestPauseActive)
        {
            return;
        }

        ResolvePlayerTarget();
        RefreshAliveBossHurtboxesIfConfigChanged();
        CheckBossSpawnByGameHours();
        CheckFinalMomentBossTrigger();
        CheckSpawnStoppedUltimateBossResolution();
    }

    private void EnsureSpawnerCoroutinesRunning()
    {
        if (externalTestPauseActive || !enabled)
        {
            return;
        }

        EnsureNormalSpawnCoroutinesRunning();

        if (eliteSpawnCoroutine == null)
        {
            eliteSpawnCoroutine = StartCoroutine(EliteSpawnRoutine());
        }

        if (monsterGrowthCoroutine == null)
        {
            monsterGrowthCoroutine = StartCoroutine(MonsterGrowthRoutine());
        }
    }

    private void EnsureNormalSpawnCoroutinesRunning()
    {
        if (!Application.isPlaying || externalTestPauseActive || !enabled)
        {
            return;
        }

        if (initialNormalSpawnCoroutine == null)
        {
            initialNormalSpawnCoroutine = StartCoroutine(InitialNormalSpawnRoutine());
        }

        if (normalBaseMaintenanceCoroutine == null)
        {
            normalBaseMaintenanceCoroutine = StartCoroutine(NormalBaseMaintenanceRoutine());
        }

        if (normalReinforcementCoroutine == null)
        {
            normalReinforcementCoroutine = StartCoroutine(NormalReinforcementRoutine());
        }
    }

    private void StopSpawnerCoroutinesForExternalPause()
    {
        if (initialNormalSpawnCoroutine != null)
        {
            StopCoroutine(initialNormalSpawnCoroutine);
            initialNormalSpawnCoroutine = null;
        }

        if (normalBaseMaintenanceCoroutine != null)
        {
            StopCoroutine(normalBaseMaintenanceCoroutine);
            normalBaseMaintenanceCoroutine = null;
        }

        if (normalReinforcementCoroutine != null)
        {
            StopCoroutine(normalReinforcementCoroutine);
            normalReinforcementCoroutine = null;
        }

        if (eliteSpawnCoroutine != null)
        {
            StopCoroutine(eliteSpawnCoroutine);
            eliteSpawnCoroutine = null;
        }

        if (monsterGrowthCoroutine != null)
        {
            StopCoroutine(monsterGrowthCoroutine);
            monsterGrowthCoroutine = null;
        }
    }

    private IEnumerator InitialNormalSpawnRoutine()
    {
        try
        {
            yield return new WaitForSeconds(Mathf.Max(0f, startDelay));
            SpawnNormalEnemiesUpTo(baseNormalMonsterCount);
        }
        finally
        {
            initialNormalSpawnCoroutine = null;
        }
    }

    private IEnumerator NormalBaseMaintenanceRoutine()
    {
        try
        {
            yield return new WaitForSeconds(Mathf.Max(0f, startDelay));

            while (true)
            {
                CleanupTrackedEnemies();
                ResolvePlayerTarget();
                SpawnNormalEnemiesUpTo(ResolveDifficultyTargetNormalCount());
                yield return new WaitForSeconds(ResolveDifficultyAdjustedInterval(spawnInterval, 0.25f));
            }
        }
        finally
        {
            normalBaseMaintenanceCoroutine = null;
        }
    }

    private IEnumerator NormalReinforcementRoutine()
    {
        try
        {
            yield return new WaitForSeconds(Mathf.Max(0f, startDelay));

            while (true)
            {
                float baseWaitSeconds = Random.Range(
                    Mathf.Max(0.1f, Mathf.Min(normalReinforceIntervalMin, normalReinforceIntervalMax)),
                    Mathf.Max(Mathf.Min(normalReinforceIntervalMin, normalReinforceIntervalMax) + 0.1f, Mathf.Max(normalReinforceIntervalMin, normalReinforceIntervalMax)));
                float waitSeconds = ResolveDifficultyAdjustedInterval(baseWaitSeconds, 0.1f);

                yield return new WaitForSeconds(waitSeconds);

                CleanupTrackedEnemies();
                ResolvePlayerTarget();
                int aliveNormal = CountAliveEnemies(MonsterRank.Normal);
                int capacity = Mathf.Max(0, ResolveDifficultyAdjustedMaxNormalMonsterCount() - aliveNormal);
                if (capacity <= 0)
                {
                    continue;
                }

                int reinforceMin = Mathf.Max(1, Mathf.Min(normalReinforceCountMin, normalReinforceCountMax));
                int reinforceMax = Mathf.Max(reinforceMin, Mathf.Max(normalReinforceCountMin, normalReinforceCountMax));
                int reinforceCount = Mathf.Min(capacity, Mathf.Max(Random.Range(reinforceMin, reinforceMax + 1), ResolveDifficultySpawnBatchCount()));
                SpawnMultipleNormals(reinforceCount);
            }
        }
        finally
        {
            normalReinforcementCoroutine = null;
        }
    }

    private IEnumerator EliteSpawnRoutine()
    {
        while (true)
        {
            float earliestAttemptTime = Mathf.Max(10f, Mathf.Min(eliteSpawnIntervalMin, eliteSpawnIntervalMax));
            float guaranteedSpawnTime = Mathf.Max(
                earliestAttemptTime,
                eliteGuaranteedSpawnTime,
                Mathf.Max(eliteSpawnIntervalMin, eliteSpawnIntervalMax));
            float randomAttemptTime = Random.Range(
                earliestAttemptTime,
                Mathf.Max(earliestAttemptTime + 0.01f, guaranteedSpawnTime - 0.01f));

            float cycleElapsed = 0f;
            bool randomAttemptConsumed = false;
            bool spawnedThisCycle = false;

            while (!spawnedThisCycle)
            {
                yield return null;

                cycleElapsed += Time.deltaTime;
                CleanupTrackedEnemies();
                ResolvePlayerTarget();

                if (!randomAttemptConsumed && cycleElapsed >= randomAttemptTime)
                {
                    randomAttemptConsumed = true;
                    spawnedThisCycle = TrySpawnEliteIfAvailable();
                    if (spawnedThisCycle)
                    {
                        break;
                    }
                }

                if (cycleElapsed >= guaranteedSpawnTime)
                {
                    spawnedThisCycle = TrySpawnEliteIfAvailable();
                    if (spawnedThisCycle)
                    {
                        break;
                    }

                    if (CountAliveEnemies(MonsterRank.Elite) >= Mathf.Max(0, maxAliveEliteCount))
                    {
                        cycleElapsed = guaranteedSpawnTime;
                    }
                }
            }
        }
    }

    private IEnumerator MonsterGrowthRoutine()
    {
        while (true)
        {
            float waitSeconds = Random.Range(
                Mathf.Max(0.1f, Mathf.Min(monsterStatGrowthIntervalMin, monsterStatGrowthIntervalMax)),
                Mathf.Max(Mathf.Min(monsterStatGrowthIntervalMin, monsterStatGrowthIntervalMax) + 0.1f, Mathf.Max(monsterStatGrowthIntervalMin, monsterStatGrowthIntervalMax)));

            yield return new WaitForSeconds(waitSeconds);
            ApplyMonsterGrowthRoll();
        }
    }

    private void SpawnNormalEnemiesUpTo(int targetCount)
    {
        if (externalTestPauseActive)
        {
            return;
        }

        if (!CanSpawnByDifficulty("NormalBase"))
        {
            return;
        }

        CleanupTrackedEnemies();
        int safeTarget = Mathf.Clamp(targetCount, 0, ResolveDifficultyAdjustedMaxNormalMonsterCount());
        int aliveNormal = CountAliveEnemies(MonsterRank.Normal);
        int missingCount = Mathf.Max(0, safeTarget - aliveNormal);
        SpawnMultipleNormals(Mathf.Min(missingCount, ResolveDifficultySpawnBatchCount()));
    }

    private void SpawnMultipleNormals(int count)
    {
        if (externalTestPauseActive)
        {
            return;
        }

        count = Mathf.Max(0, count);
        for (int i = 0; i < count; i++)
        {
            if (!CanSpawnByDifficulty("NormalReinforcement"))
            {
                break;
            }

            if (CountAliveEnemies(MonsterRank.Normal) >= ResolveDifficultyAdjustedMaxNormalMonsterCount())
            {
                break;
            }

            SpawnNormalEnemy();
        }
    }

    private GameObject SpawnNormalEnemy()
    {
        if (externalTestPauseActive)
        {
            return null;
        }

        return SpawnFromPool(ResolvePool(normalEnemyPrefabs, fallbackNormalEnemyPrefabs), MonsterRank.Normal);
    }

    private GameObject SpawnEliteEnemy()
    {
        if (externalTestPauseActive)
        {
            return null;
        }

        return SpawnFromPool(ResolvePool(eliteEnemyPrefabs, fallbackEliteEnemyPrefabs), MonsterRank.Elite);
    }

    private GameObject SpawnBossEnemy()
    {
        if (externalTestPauseActive)
        {
            return null;
        }

        return SpawnFromPool(ResolvePool(bossEnemyPrefabs, fallbackBossEnemyPrefabs), MonsterRank.Boss);
    }

    private GameObject SpawnFromPool(List<GameObject> sourcePool, MonsterRank forcedRank)
    {
        if (externalTestPauseActive)
        {
            return null;
        }

        if (!CanSpawnByDifficulty(forcedRank.ToString()))
        {
            return null;
        }

        if (sourcePool == null || sourcePool.Count == 0)
        {
            return null;
        }

        int randomIndex = Random.Range(0, sourcePool.Count);
        GameObject selectedEnemy = sourcePool[randomIndex];
        if (selectedEnemy == null)
        {
            return null;
        }

        MonsterIdentity prefabIdentity = selectedEnemy.GetComponent<MonsterIdentity>();
        MonsterSpecies? runtimeSpecies = prefabIdentity != null ? prefabIdentity.species : (MonsterSpecies?)null;
        MonsterRank runtimeRank = forcedRank;

        Vector3 spawnPosition = ResolveSpawnPosition(selectedEnemy);
        if (runtimeRank == MonsterRank.Boss)
        {
            LogBossSpawnYDiagnostics(selectedEnemy, spawnPosition, runtimeRank, prefabIdentity);
        }

        GameObject spawnedEnemy = Instantiate(selectedEnemy, spawnPosition, Quaternion.identity);
        if (runtimeRank == MonsterRank.Boss)
        {
            Debug.Log(
                $"[BossSpawnY] prefab={selectedEnemy.name} root position after Instantiate={spawnedEnemy.transform.position} rank={runtimeRank} attackStyle={(prefabIdentity != null ? prefabIdentity.attackStyle.ToString() : "Unknown")}",
                spawnedEnemy);
        }
        MonsterIdentity cloneIdentity = spawnedEnemy.GetComponent<MonsterIdentity>();
        if (cloneIdentity == null)
        {
            cloneIdentity = spawnedEnemy.AddComponent<MonsterIdentity>();
        }

        if (runtimeSpecies.HasValue)
        {
            cloneIdentity.species = runtimeSpecies.Value;
        }

        cloneIdentity.rank = runtimeRank;

        ApplyOfficialMonsterRuntimeSetup(
            spawnedEnemy,
            runtimeSpecies,
            runtimeRank,
            ResolveActivePlayerTarget(),
            trackAsAlive: true,
            initializeDeathNotifier: true,
            source: "EnemySpawner");

        return spawnedEnemy;
    }

    public void SpawnSplitNormalsFromElite(GameObject eliteSource, int count, float scatterRadius, bool allowDuringExternalTest = false)
    {
        if (externalTestPauseActive && !allowDuringExternalTest)
        {
            Debug.Log("[EliteSplitDebug] split blocked by external test pause.", eliteSource != null ? eliteSource : this);
            return;
        }

        if (eliteSource == null || count <= 0 || !CanSpawnByDifficulty("EliteSplit"))
        {
            return;
        }

        MonsterIdentity sourceIdentity = eliteSource.GetComponent<MonsterIdentity>();
        if (sourceIdentity == null || sourceIdentity.rank != MonsterRank.Elite || !IsSlimeSpecies(sourceIdentity.species))
        {
            return;
        }

        List<GameObject> sourcePool = ResolvePool(normalEnemyPrefabs, fallbackNormalEnemyPrefabs);
        if (sourcePool == null || sourcePool.Count == 0)
        {
            Debug.LogWarning("[EnemySpawner] Elite slime split failed: normal enemy prefab pool is empty.", this);
            return;
        }

        SpawnSplitChildrenInternal(
            eliteSource,
            sourceIdentity.species,
            MonsterRank.Normal,
            count,
            scatterRadius,
            1f,
            1f,
            1f,
            1f,
            1f,
            false,
            true,
            false,
            allowDuringExternalTest,
            "EliteDeathSplit");
    }

    public void SpawnSplitChildren(
        GameObject bossSource,
        int count,
        float scatterRadius,
        MonsterRank childRank,
        float healthRatio,
        float attackRatio,
        float defenseRatio,
        float speedRatio,
        float scaleRatio,
        bool childrenCanSplit,
        bool isCleanupBoss,
        bool debugLog,
        bool allowDuringExternalTest = false)
    {
        SpawnSplitChildrenAndCollect(
            bossSource,
            count,
            scatterRadius,
            childRank,
            healthRatio,
            attackRatio,
            defenseRatio,
            speedRatio,
            scaleRatio,
            childrenCanSplit,
            isCleanupBoss,
            debugLog,
            "Death",
            allowDuringExternalTest);
    }

    public List<GameObject> SpawnSplitChildrenAndCollect(
        GameObject bossSource,
        int count,
        float scatterRadius,
        MonsterRank childRank,
        float healthRatio,
        float attackRatio,
        float defenseRatio,
        float speedRatio,
        float scaleRatio,
        bool childrenCanSplit,
        bool isCleanupBoss,
        bool debugLog,
        string splitTriggerLabel = "Death",
        bool allowDuringExternalTest = false)
    {
        if (bossSource == null || count <= 0 || !CanSpawnByDifficulty("BossSplit"))
        {
            return new List<GameObject>();
        }

        MonsterIdentity sourceIdentity = bossSource.GetComponent<MonsterIdentity>();
        if (sourceIdentity == null || sourceIdentity.rank != MonsterRank.Boss || !IsSlimeSpecies(sourceIdentity.species))
        {
            return new List<GameObject>();
        }

        MonsterRank resolvedChildRank = childRank == MonsterRank.Boss ? MonsterRank.Elite : childRank;
        List<GameObject> sourcePool = ResolveSplitPoolForRank(resolvedChildRank);
        if (sourcePool == null || sourcePool.Count == 0)
        {
            Debug.LogWarning("[EnemySpawner] Boss split failed: child prefab pool is empty.", this);
            return new List<GameObject>();
        }

        if (debugLog)
        {
            Debug.Log(
                $"[BossSplit] boss name={bossSource.name} split trigger={splitTriggerLabel} child count={count} child rank={resolvedChildRank} child health ratio={healthRatio:F2} child attack ratio={attackRatio:F2} child defense ratio={defenseRatio:F2} child speed ratio={speedRatio:F2} childrenCanSplit={childrenCanSplit} cleanupBoss={isCleanupBoss}",
                bossSource);
        }

        return SpawnSplitChildrenInternal(
            bossSource,
            sourceIdentity.species,
            resolvedChildRank,
            count,
            scatterRadius,
            healthRatio,
            attackRatio,
            defenseRatio,
            speedRatio,
            scaleRatio,
            childrenCanSplit,
            true,
            false,
            allowDuringExternalTest,
            isCleanupBoss ? $"CleanupBoss{splitTriggerLabel}Split" : $"Boss{splitTriggerLabel}Split");
    }

    private List<GameObject> SpawnSplitChildrenInternal(
        GameObject sourceEnemy,
        MonsterSpecies species,
        MonsterRank childRank,
        int count,
        float scatterRadius,
        float healthRatio,
        float attackRatio,
        float defenseRatio,
        float speedRatio,
        float scaleRatio,
        bool childrenCanSplit,
        bool suppressRuneDrop,
        bool keepAsCleanupBoss,
        bool allowDuringExternalTest,
        string splitSource)
    {
        if (externalTestPauseActive && !allowDuringExternalTest)
        {
            Debug.Log("[EliteSplitDebug] split blocked by external test pause.", sourceEnemy != null ? sourceEnemy : this);
            return new List<GameObject>();
        }

        List<GameObject> sourcePool = ResolveSplitPoolForRank(childRank);
        if (sourceEnemy == null || count <= 0 || sourcePool == null || sourcePool.Count == 0)
        {
            return new List<GameObject>();
        }

        List<GameObject> spawnedChildren = new List<GameObject>(count);
        for (int i = 0; i < count; i++)
        {
            GameObject selectedEnemy = ResolveNormalSplitPrefab(sourcePool, species);
            if (selectedEnemy == null)
            {
                continue;
            }

            Vector3 spawnPosition = sourceEnemy.transform.position + ResolveSplitOffset(scatterRadius, i, count);
            GameObject spawnedEnemy = Instantiate(selectedEnemy, spawnPosition, Quaternion.identity);

            MonsterIdentity cloneIdentity = spawnedEnemy.GetComponent<MonsterIdentity>();
            if (cloneIdentity == null)
            {
                cloneIdentity = spawnedEnemy.AddComponent<MonsterIdentity>();
            }

            cloneIdentity.species = species;
            cloneIdentity.rank = childRank;
            cloneIdentity.suppressRuneDrop = suppressRuneDrop;

            MonsterCombatAutoSetup.Configure(spawnedEnemy, species, childRank);
            ApplyBossVisualExternalConfig(spawnedEnemy, childRank, "EnemySpawner");
            ApplyHealthBarExternalConfig(spawnedEnemy, childRank, "EnemySpawner");
            ResolveDifficultyDirector()?.ApplyDifficultyToEnemy(spawnedEnemy);
            ApplySplitChildModifiers(spawnedEnemy, healthRatio, attackRatio, defenseRatio, speedRatio, scaleRatio);
            SnapSplitChildToGround(spawnedEnemy, splitSource);
            RegisterSpawnedEnemy(spawnedEnemy);

            EnemyDeathNotifier notifier = spawnedEnemy.GetComponent<EnemyDeathNotifier>();
            if (notifier == null)
            {
                notifier = spawnedEnemy.AddComponent<EnemyDeathNotifier>();
            }
            notifier.Initialize(this);

            EliteSlimeSplitOnDeath splitComponentOnChild = spawnedEnemy.GetComponent<EliteSlimeSplitOnDeath>();
            if (splitComponentOnChild != null)
            {
                splitComponentOnChild.Initialize(this);
            }

            EnemyController enemyController = spawnedEnemy.GetComponent<EnemyController>();
            if (enemyController != null)
            {
                enemyController.SetTarget(ResolveActivePlayerTarget(), "Spawner");
            }

            if (!childrenCanSplit)
            {
                if (splitComponentOnChild != null)
                {
                    splitComponentOnChild.enabled = false;
                }
            }

            if (sourceEnemy.name.Contains("[MonsterTest_") || sourceEnemy.name.Contains(EliteSplitFromTestSuffix))
            {
                spawnedEnemy.name = AppendSplitTestSuffix(spawnedEnemy.name);
            }

            if (!keepAsCleanupBoss && spawnedEnemy == cleanupBossInstance)
            {
                cleanupBossInstance = null;
            }

            spawnedChildren.Add(spawnedEnemy);
        }

        return spawnedChildren;
    }

    private static string AppendSplitTestSuffix(string baseName)
    {
        return baseName.Contains(EliteSplitFromTestSuffix) ? baseName : baseName + EliteSplitFromTestSuffix;
    }

    private List<GameObject> ResolveSplitPoolForRank(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Elite:
                return ResolvePool(eliteEnemyPrefabs, fallbackEliteEnemyPrefabs);
            case MonsterRank.Boss:
                return ResolvePool(bossEnemyPrefabs, fallbackBossEnemyPrefabs);
            default:
                return ResolvePool(normalEnemyPrefabs, fallbackNormalEnemyPrefabs);
        }
    }

    private void ApplySplitChildModifiers(
        GameObject spawnedEnemy,
        float healthRatio,
        float attackRatio,
        float defenseRatio,
        float speedRatio,
        float scaleRatio)
    {
        if (spawnedEnemy == null)
        {
            return;
        }

        CombatStats stats = spawnedEnemy.GetComponent<CombatStats>();
        BattleResourceBank resourceBank = spawnedEnemy.GetComponent<BattleResourceBank>();
        CombatHealth combatHealth = spawnedEnemy.GetComponent<CombatHealth>();
        if (stats != null)
        {
            stats.maxHealth = Mathf.Max(1f, Mathf.Round(stats.maxHealth * Mathf.Max(0f, healthRatio)));
            stats.physicalAttack = Mathf.Max(0f, Mathf.Round(stats.physicalAttack * Mathf.Max(0f, attackRatio)));
            stats.specialAttack = Mathf.Max(0f, Mathf.Round(stats.specialAttack * Mathf.Max(0f, attackRatio)));
            stats.physicalDefense = Mathf.Max(0f, Mathf.Round(stats.physicalDefense * Mathf.Max(0f, defenseRatio)));
            stats.specialDefense = Mathf.Max(0f, Mathf.Round(stats.specialDefense * Mathf.Max(0f, defenseRatio)));
            stats.speed = Mathf.Max(0.1f, RoundToDecimals(stats.speed * Mathf.Max(0f, speedRatio), 2));
        }

        if (resourceBank != null)
        {
            resourceBank.maxHealth = stats != null ? stats.maxHealth : resourceBank.maxHealth;
            resourceBank.currentHealth = resourceBank.maxHealth;
        }

        if (combatHealth != null)
        {
            combatHealth.stats = stats;
            combatHealth.resourceBank = resourceBank;
            combatHealth.currentHealth = stats != null ? stats.maxHealth : combatHealth.currentHealth;
        }

        if (Mathf.Abs(scaleRatio - 1f) > 0.0001f)
        {
            Transform scaleTarget = spawnedEnemy.transform;
            MonsterRankVisual rankVisual = spawnedEnemy.GetComponent<MonsterRankVisual>();
            if (rankVisual != null && rankVisual.visualRoot != null)
            {
                scaleTarget = rankVisual.visualRoot;
            }

            scaleTarget.localScale *= Mathf.Max(0.01f, scaleRatio);
        }
    }

    private GameObject ResolveNormalSplitPrefab(List<GameObject> sourcePool, MonsterSpecies preferredSpecies)
    {
        if (sourcePool == null || sourcePool.Count == 0)
        {
            return null;
        }

        List<GameObject> matchingSpecies = new List<GameObject>();
        for (int i = 0; i < sourcePool.Count; i++)
        {
            GameObject prefab = sourcePool[i];
            if (prefab == null)
            {
                continue;
            }

            MonsterIdentity identity = prefab.GetComponent<MonsterIdentity>();
            MonsterSpecies species = identity != null ? identity.species : MonsterSpecies.BlueSlime;
            if (species == preferredSpecies)
            {
                matchingSpecies.Add(prefab);
            }
        }

        if (matchingSpecies.Count > 0)
        {
            return matchingSpecies[Random.Range(0, matchingSpecies.Count)];
        }

        return sourcePool[Random.Range(0, sourcePool.Count)];
    }

    private static Vector3 ResolveSplitOffset(float scatterRadius, int index, int count)
    {
        float radius = Mathf.Max(0f, scatterRadius);
        if (radius <= 0f)
        {
            return Vector3.zero;
        }

        float angle = count > 0 ? (Mathf.PI * 2f * index / count) : Random.Range(0f, Mathf.PI * 2f);
        Vector2 ringOffset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        Vector2 randomOffset = Random.insideUnitCircle * (radius * 0.25f);
        Vector2 finalOffset = ringOffset + randomOffset;
        return new Vector3(finalOffset.x, 0f, finalOffset.y);
    }

    private static bool IsSlimeSpecies(MonsterSpecies species)
    {
        return species == MonsterSpecies.BlueSlime ||
               species == MonsterSpecies.GreenSlime ||
               species == MonsterSpecies.LavaSlime ||
               species == MonsterSpecies.PoisonSlime ||
               species == MonsterSpecies.RainbowSlime;
    }

    private void RegisterSpawnedEnemy(GameObject enemy)
    {
        if (enemy == null)
        {
            return;
        }

        aliveEnemies.Add(enemy);
        CacheMonsterBaseSnapshot(enemy);
        ConfigureSpawnedEnemyPhysics(enemy);
        ApplyCurrentMultiplierToMonster(enemy, refillCurrentHealth: true);
        if (debugMonsterSpawnState)
        {
            Transform activePlayer = ResolveActivePlayerTarget();
            if (activePlayer == null)
            {
                ResolvePlayerTarget();
                activePlayer = ResolveActivePlayerTarget();
            }

            Debug.Log(BuildMonsterPrefabAndRuntimeSummary(enemy, activePlayer), enemy);
        }
    }

    public void ApplyOfficialMonsterRuntimeSetup(
        GameObject enemy,
        MonsterSpecies? runtimeSpecies,
        MonsterRank runtimeRank,
        Transform targetOverride,
        bool trackAsAlive,
        bool initializeDeathNotifier,
        string source)
    {
        if (enemy == null)
        {
            return;
        }

        MonsterIdentity cloneIdentity = enemy.GetComponent<MonsterIdentity>();
        if (cloneIdentity == null)
        {
            cloneIdentity = enemy.AddComponent<MonsterIdentity>();
        }

        if (runtimeSpecies.HasValue)
        {
            cloneIdentity.species = runtimeSpecies.Value;
        }

        cloneIdentity.rank = runtimeRank;

        MonsterCombatAutoSetup.Configure(enemy, runtimeSpecies, runtimeRank);
        ApplyRankVisualExternalConfig(enemy, runtimeRank, source);
        ApplyHealthBarExternalConfig(enemy, runtimeRank, source);
        ResolveDifficultyDirector()?.ApplyDifficultyToEnemy(enemy);

        if (trackAsAlive)
        {
            RegisterSpawnedEnemy(enemy);
        }
        else
        {
            CacheMonsterBaseSnapshot(enemy);
            ConfigureSpawnedEnemyPhysics(enemy);
            ApplyCurrentMultiplierToMonster(enemy, refillCurrentHealth: true);
        }

        if (runtimeRank == MonsterRank.Boss)
        {
            SnapEnemyToGround(enemy, enemyGroundSnapLayerMask, source);
        }

        if (initializeDeathNotifier)
        {
            EnemyDeathNotifier notifier = enemy.GetComponent<EnemyDeathNotifier>();
            if (notifier == null)
            {
                notifier = enemy.AddComponent<EnemyDeathNotifier>();
            }

            notifier.Initialize(this);
        }

        EliteSlimeSplitOnDeath splitOnDeath = enemy.GetComponent<EliteSlimeSplitOnDeath>();
        if (splitOnDeath != null)
        {
            splitOnDeath.Initialize(this);
        }

        EnemyController enemyController = enemy.GetComponent<EnemyController>();
        if (enemyController != null)
        {
            enemyController.enabled = true;
            enemyController.SetTarget(targetOverride != null ? targetOverride : ResolveActivePlayerTarget(), source);
        }

        if (debugMonsterSpawnState && !trackAsAlive)
        {
            Debug.Log(BuildMonsterPrefabAndRuntimeSummary(enemy, targetOverride != null ? targetOverride : ResolveActivePlayerTarget()), enemy);
        }
    }

    private void ApplyRankVisualExternalConfig(GameObject enemy, MonsterRank rank, string source)
    {
        if (enemy == null)
        {
            return;
        }

        if (rank == MonsterRank.Normal)
        {
            ApplyNormalVisualExternalConfig(enemy, source);
            return;
        }

        if (rank == MonsterRank.Elite)
        {
            ApplyEliteVisualExternalConfig(enemy, source);
            return;
        }

        ApplyBossVisualExternalConfig(enemy, rank, source);
    }

    private void ApplyNormalVisualExternalConfig(GameObject enemy, string source)
    {
        MonsterRankVisual rankVisual = enemy.GetComponent<MonsterRankVisual>();
        if (rankVisual == null)
        {
            return;
        }

        rankVisual.ApplyNormalVisualConfig(
            enableNormalVisualGroundOffset,
            normalVisualGroundOffsetY,
            source);
    }

    private void ApplyEliteVisualExternalConfig(GameObject enemy, string source)
    {
        MonsterRankVisual rankVisual = enemy.GetComponent<MonsterRankVisual>();
        if (rankVisual == null)
        {
            return;
        }

        rankVisual.ApplyEliteVisualConfig(
            enableEliteVisualScale,
            eliteVisualScaleMultiplier,
            enableEliteVisualGroundOffset,
            eliteVisualGroundOffsetY,
            debugEliteVisualConfig,
            source);

        if (debugEliteVisualConfig)
        {
            Debug.Log(
                "[EliteSpawnDebug] " +
                "source=" + source +
                " prefab=" + enemy.name +
                " rank=Elite" +
                " visual scale applied=" + enableEliteVisualScale +
                " final visual scale=" + rankVisual.RuntimeVisualRoot.localScale +
                " elite visual scale multiplier=" + eliteVisualScaleMultiplier.ToString("F2"),
                enemy);
        }
    }

    private void ApplyBossVisualExternalConfig(GameObject enemy, MonsterRank rank, string source)
    {
        if (enemy == null || rank != MonsterRank.Boss)
        {
            return;
        }

        MonsterRankVisual rankVisual = enemy.GetComponent<MonsterRankVisual>();
        if (rankVisual != null)
        {
            rankVisual.ApplyBossVisualConfig(
                enableBossVisualScale,
                bossVisualScaleMultiplier,
                enableBossVisualGroundOffset,
                bossVisualGroundOffsetY,
                debugBossVisualGroundOffset,
                source);

            Debug.Log(
                $"[BossVisualExternalConfig] scaleMultiplier={bossVisualScaleMultiplier:F2} groundOffsetY={bossVisualGroundOffsetY:F2} source={source}",
                enemy);
        }

        ApplyBossHurtboxExternalConfig(enemy, rank, source);
    }

    private void ApplyHealthBarExternalConfig(GameObject enemy, MonsterRank rank, string source)
    {
        if (enemy == null)
        {
            return;
        }

        WorldHealthBar healthBar = enemy.GetComponent<WorldHealthBar>();
        if (healthBar == null)
        {
            return;
        }

        bool debug = debugMonsterSpawnState
            || (rank == MonsterRank.Elite && debugEliteVisualConfig)
            || (rank == MonsterRank.Boss && debugBossVisualGroundOffset);

        healthBar.ApplyHealthBarConfig(
            normalHealthBarOffsetY,
            eliteHealthBarOffsetY,
            bossHealthBarOffsetY,
            debug,
            source);
    }

    private void ApplyBossHurtboxExternalConfig(GameObject enemy, MonsterRank rank, string source)
    {
        if (enemy == null || rank != MonsterRank.Boss || !enableBossHurtboxScale)
        {
            return;
        }

        Collider mainCollider = ResolveBossPrimaryBodyCollider(enemy);
        if (mainCollider == null)
        {
            Debug.LogWarning("[BossHurtboxDebug] missing main body collider for boss hurtbox scaling.", enemy);
            return;
        }

        Collider hurtboxCollider = EnsureBossScaledHurtbox(enemy, mainCollider);
        if (hurtboxCollider == null)
        {
            Debug.LogWarning("[BossHurtboxDebug] failed to create scaled hurtbox collider.", enemy);
            return;
        }

        if (!debugBossHurtbox)
        {
            return;
        }

        MonsterIdentity identity = enemy.GetComponent<MonsterIdentity>();
        Transform visualTransform = ResolveBossVisualTransform(enemy);
        Renderer visualRenderer = ResolveBossVisualRenderer(visualTransform);

        Debug.Log(
            "[BossHurtboxDebug] " +
            "object=" + enemy.name +
            " rank=" + (identity != null ? identity.rank.ToString() : "Unknown") +
            " visualScale=" + (visualTransform != null ? visualTransform.localScale.ToString() : "null") +
            " visualBounds=" + (visualRenderer != null ? visualRenderer.bounds.ToString() : "null") +
            " mainCollider=" + mainCollider.name +
            " mainColliderIsTrigger=" + mainCollider.isTrigger +
            " mainColliderBounds=" + mainCollider.bounds +
            " hurtboxCollider=" + hurtboxCollider.name +
            " hurtboxIsTrigger=" + hurtboxCollider.isTrigger +
            " hurtboxBounds=" + hurtboxCollider.bounds +
            " useVisualBoundsHurtbox=" + useBossVisualBoundsHurtbox +
            " sizeMultiplier=" + bossHurtboxSizeMultiplier +
            " extraPadding=" + bossHurtboxExtraPadding +
            " centerOffset=" + bossHurtboxCenterOffset +
            " minimumDepth=" + bossHurtboxMinimumDepth +
            " layer=" + LayerMask.LayerToName(hurtboxCollider.gameObject.layer) +
            " tag=" + hurtboxCollider.gameObject.tag +
            " source=" + source,
            enemy);

        LogBossHurtboxShapeDetails(enemy, mainCollider, hurtboxCollider);
    }

    public void RefreshBossHurtboxForRuntime(GameObject enemy, string source)
    {
        MonsterIdentity identity = enemy != null ? enemy.GetComponent<MonsterIdentity>() : null;
        if (enemy == null || identity == null || identity.rank != MonsterRank.Boss)
        {
            return;
        }

        if (!enableBossHurtboxScale)
        {
            SetBossScaledHurtboxEnabled(enemy, false);
            return;
        }

        SetBossScaledHurtboxEnabled(enemy, true);
        ApplyBossHurtboxExternalConfig(enemy, identity.rank, source);
    }

    private void RefreshAliveBossHurtboxesIfConfigChanged()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextBossHurtboxRuntimeRefreshTime)
        {
            return;
        }

        nextBossHurtboxRuntimeRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, bossHurtboxRuntimeRefreshInterval);

        int configHash = GetConfiguredBossHurtboxConfigHash();
        if (configHash == lastBossHurtboxRuntimeConfigHash)
        {
            return;
        }

        lastBossHurtboxRuntimeConfigHash = configHash;
        CleanupTrackedEnemies();

        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            RefreshBossHurtboxForRuntime(aliveEnemies[i], "EnemySpawnerRuntimeRefresh");
        }
    }

    private static void SetBossScaledHurtboxEnabled(GameObject enemy, bool enabledState)
    {
        Transform hurtboxRoot = enemy != null ? enemy.transform.Find("BossScaledHurtbox") : null;
        if (hurtboxRoot == null)
        {
            return;
        }

        hurtboxRoot.gameObject.SetActive(enabledState);
        Collider[] colliders = hurtboxRoot.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = enabledState;
            }
        }
    }

    private void CacheMonsterBaseSnapshot(GameObject enemy)
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

        bool hasScaleTarget = TryGetEnemyScaleTarget(enemy, out Transform scaleTarget);
        monsterBaseSnapshots[enemy.GetInstanceID()] = new MonsterBaseSnapshot
        {
            initialized = true,
            maxHealth = Mathf.Max(1f, stats.maxHealth),
            physicalAttack = Mathf.Max(0f, stats.physicalAttack),
            physicalDefense = Mathf.Max(0f, stats.physicalDefense),
            specialAttack = Mathf.Max(0f, stats.specialAttack),
            specialDefense = Mathf.Max(0f, stats.specialDefense),
            speed = Mathf.Max(0.1f, stats.speed),
            luck = Mathf.Max(0f, stats.luck),
            hasScaleTarget = hasScaleTarget,
            scaleTargetLocalScale = scaleTarget != null ? scaleTarget.localScale : Vector3.one
        };
    }

    private string BuildMonsterPrefabAndRuntimeSummary(GameObject enemy, Transform activePlayer)
    {
        MonsterIdentity identity = enemy != null ? enemy.GetComponent<MonsterIdentity>() : null;
        EnemyController controller = enemy != null ? enemy.GetComponent<EnemyController>() : null;
        Rigidbody body = enemy != null ? enemy.GetComponent<Rigidbody>() : null;
        Collider[] colliders = enemy != null ? enemy.GetComponentsInChildren<Collider>(true) : null;
        Collider primaryCollider = null;
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider candidate = colliders[i];
                if (candidate != null && candidate.enabled && !candidate.isTrigger)
                {
                    primaryCollider = candidate;
                    break;
                }
            }
        }

        CombatHealth health = enemy != null ? enemy.GetComponent<CombatHealth>() : null;
        Transform visualSlime = enemy != null ? enemy.transform.Find("Visual_Slime") : null;
        WorldHealthBar healthBar = enemy != null ? enemy.GetComponent<WorldHealthBar>() : null;
        string prefabSource = enemy != null ? enemy.name.Replace("(Clone)", string.Empty).Trim() : "null";
        string runtimeSummary = controller != null
            ? controller.BuildRuntimeDebugSummary(activePlayer)
            : "[MonsterDebug] enemyControllerExists=false";

        return
            "[MonsterPrefabDebug] " +
            $"prefabSource={prefabSource} rootName={(enemy != null ? enemy.name : "null")} rootPosition={(enemy != null ? enemy.transform.position.ToString() : "null")} " +
            $"hasEnemyController={(controller != null)} hasMonsterIdentity={(identity != null)} " +
            $"rank={(identity != null ? identity.rank.ToString() : "Unknown")} species={(identity != null ? identity.species.ToString() : "Unknown")} attackStyle={(identity != null ? identity.attackStyle.ToString() : "Unknown")} " +
            $"hasRigidbody={(body != null)} rigidbodyConstraints={(body != null ? body.constraints.ToString() : "None")} " +
            $"hasCollider={(primaryCollider != null)} colliderType={(primaryCollider != null ? primaryCollider.GetType().Name : "None")} colliderIsTrigger={(primaryCollider != null && primaryCollider.isTrigger)} " +
            $"hasVisualSlime={(visualSlime != null)} visualLocalPosition={(visualSlime != null ? visualSlime.localPosition.ToString() : "null")} visualLocalScale={(visualSlime != null ? visualSlime.localScale.ToString() : "null")} " +
            $"hasShadow=false hasHealthBar={(healthBar != null)} health={(health != null ? health.currentHealth.ToString("F1") : "n/a")}/{(health != null ? health.MaxHealthValue.ToString("F1") : "n/a")} " +
            $"{runtimeSummary}";
    }

    private void ApplyMonsterGrowthRoll()
    {
        float growthPercent = Random.Range(
            Mathf.Max(0f, Mathf.Min(monsterStatGrowthPercentMin, monsterStatGrowthPercentMax)),
            Mathf.Max(Mathf.Min(monsterStatGrowthPercentMin, monsterStatGrowthPercentMax), Mathf.Max(monsterStatGrowthPercentMin, monsterStatGrowthPercentMax)));

        currentMonsterStatMultiplier *= 1f + growthPercent;

        CleanupTrackedEnemies();
        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            ApplyCurrentMultiplierToMonster(aliveEnemies[i], refillCurrentHealth: false);
        }
    }

    private void ApplyCurrentMultiplierToMonster(GameObject enemy, bool refillCurrentHealth)
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

        MonsterBaseSnapshot snapshot;
        if (!monsterBaseSnapshots.TryGetValue(enemy.GetInstanceID(), out snapshot) || !snapshot.initialized)
        {
            CacheMonsterBaseSnapshot(enemy);
            if (!monsterBaseSnapshots.TryGetValue(enemy.GetInstanceID(), out snapshot) || !snapshot.initialized)
            {
                return;
            }
        }

        BattleResourceBank resourceBank = enemy.GetComponent<BattleResourceBank>();
        CombatHealth combatHealth = enemy.GetComponent<CombatHealth>();
        float previousMaxHealth = resourceBank != null
            ? Mathf.Max(1f, resourceBank.maxHealth)
            : (combatHealth != null ? Mathf.Max(1f, combatHealth.MaxHealthValue) : Mathf.Max(1f, stats.maxHealth));
        float previousCurrentHealth = resourceBank != null
            ? Mathf.Max(0f, resourceBank.currentHealth)
            : (combatHealth != null ? Mathf.Max(0f, combatHealth.currentHealth) : previousMaxHealth);
        float healthRatio = refillCurrentHealth || previousMaxHealth <= 0f
            ? 1f
            : Mathf.Clamp01(previousCurrentHealth / previousMaxHealth);

        MonsterIdentity identity = enemy.GetComponent<MonsterIdentity>();
        MonsterRank rank = identity != null ? identity.rank : MonsterRank.Normal;
        float timeMultiplier = Mathf.Max(1f, currentMonsterStatMultiplier);
        float rankHealthMultiplier = ResolveRankHealthMultiplier(rank);
        float rankAttackMultiplier = ResolveRankAttackMultiplier(rank);
        float rankDefenseMultiplier = ResolveRankDefenseMultiplier(rank);
        float rankMagicMultiplier = ResolveRankMagicMultiplier(rank);
        float rankResistanceMultiplier = ResolveRankResistanceMultiplier(rank);
        float rankSpeedMultiplier = ResolveRankSpeedMultiplier(rank);

        float specialBossHpMultiplier = 1f;
        float specialBossAttackMultiplier = 1f;
        float specialBossDefenseMultiplier = 1f;
        float specialBossSpecialAttackMultiplier = 1f;
        float specialBossSpecialDefenseMultiplier = 1f;
        float specialBossSpeedMultiplier = 1f;
        int enemyId = enemy.GetInstanceID();

        bool isCleanupBoss = rank == MonsterRank.Boss && enemy == cleanupBossInstance;
        if (rank == MonsterRank.Boss && !isCleanupBoss && finalMomentBossEnemyIds.Contains(enemyId))
        {
            specialBossHpMultiplier = Mathf.Max(specialBossHpMultiplier, Mathf.Max(0.01f, finalMomentBossHpMultiplier));
            specialBossAttackMultiplier = Mathf.Max(specialBossAttackMultiplier, Mathf.Max(0.01f, finalMomentBossAttackMultiplier));
            specialBossDefenseMultiplier = Mathf.Max(specialBossDefenseMultiplier, Mathf.Max(0.01f, finalMomentBossDefenseMultiplier));
            specialBossSpecialAttackMultiplier = Mathf.Max(specialBossSpecialAttackMultiplier, Mathf.Max(0.01f, finalMomentBossSpecialAttackMultiplier));
            specialBossSpecialDefenseMultiplier = Mathf.Max(specialBossSpecialDefenseMultiplier, Mathf.Max(0.01f, finalMomentBossSpecialDefenseMultiplier));
            specialBossSpeedMultiplier = Mathf.Max(specialBossSpeedMultiplier, Mathf.Max(0.01f, finalMomentBossSpeedMultiplier));
        }

        if (rank == MonsterRank.Boss && ultimateBossModifiersByEnemyId.TryGetValue(enemyId, out UltimateBossModifiers ultimateModifiers))
        {
            specialBossHpMultiplier = Mathf.Max(specialBossHpMultiplier, Mathf.Max(0.01f, ultimateModifiers.hpMultiplier));
            specialBossAttackMultiplier = Mathf.Max(specialBossAttackMultiplier, Mathf.Max(0.01f, ultimateModifiers.attackMultiplier));
            specialBossDefenseMultiplier = Mathf.Max(specialBossDefenseMultiplier, Mathf.Max(0.01f, ultimateModifiers.defenseMultiplier));
            specialBossSpecialAttackMultiplier = Mathf.Max(specialBossSpecialAttackMultiplier, Mathf.Max(0.01f, ultimateModifiers.specialAttackMultiplier));
            specialBossSpecialDefenseMultiplier = Mathf.Max(specialBossSpecialDefenseMultiplier, Mathf.Max(0.01f, ultimateModifiers.specialDefenseMultiplier));
            specialBossSpeedMultiplier = Mathf.Max(specialBossSpeedMultiplier, Mathf.Max(0.01f, ultimateModifiers.speedMultiplier));
        }

        if (isCleanupBoss)
        {
            specialBossHpMultiplier *= Mathf.Max(0.01f, cleanupBossHealthMultiplier);
            specialBossAttackMultiplier *= Mathf.Max(0.01f, cleanupBossAttackMultiplier);
            specialBossDefenseMultiplier *= Mathf.Max(0.01f, cleanupBossDefenseMultiplier);
            specialBossSpecialAttackMultiplier *= Mathf.Max(0.01f, cleanupBossSpecialAttackMultiplier);
            specialBossSpecialDefenseMultiplier *= Mathf.Max(0.01f, cleanupBossSpecialDefenseMultiplier);
            specialBossSpeedMultiplier *= Mathf.Max(0.01f, cleanupBossSpeedMultiplier);
        }

        float healthMultiplier = Mathf.Max(0.01f, baseHealthMultiplier) * timeMultiplier * rankHealthMultiplier * specialBossHpMultiplier;
        float attackMultiplier = Mathf.Max(0.01f, baseAttackMultiplier) * timeMultiplier * rankAttackMultiplier * specialBossAttackMultiplier;
        float defenseMultiplier = Mathf.Max(0.01f, baseDefenseMultiplier) * timeMultiplier * rankDefenseMultiplier * specialBossDefenseMultiplier;
        float magicMultiplier = Mathf.Max(0.01f, baseSpecialAttackMultiplier) * timeMultiplier * rankMagicMultiplier * specialBossSpecialAttackMultiplier;
        float resistanceMultiplier = Mathf.Max(0.01f, baseSpecialDefenseMultiplier) * timeMultiplier * rankResistanceMultiplier * specialBossSpecialDefenseMultiplier;
        float speedMultiplier = Mathf.Max(0.01f, baseSpeedMultiplier) * timeMultiplier * rankSpeedMultiplier * specialBossSpeedMultiplier;

        stats.maxHealth = Mathf.Max(1f, Mathf.Round(snapshot.maxHealth * healthMultiplier));
        stats.physicalAttack = Mathf.Max(0f, Mathf.Round(snapshot.physicalAttack * attackMultiplier));
        stats.physicalDefense = Mathf.Max(0f, Mathf.Round(snapshot.physicalDefense * defenseMultiplier));
        stats.specialAttack = Mathf.Max(0f, Mathf.Round(snapshot.specialAttack * magicMultiplier));
        stats.specialDefense = Mathf.Max(0f, Mathf.Round(snapshot.specialDefense * resistanceMultiplier));
        stats.speed = Mathf.Max(0.1f, RoundToDecimals(snapshot.speed * speedMultiplier, 2));
        stats.luck = Mathf.Max(0f, snapshot.luck);

        float resolvedCurrentHealth = refillCurrentHealth
            ? stats.maxHealth
            : Mathf.Clamp(stats.maxHealth * healthRatio, 0f, stats.maxHealth);

        if (resourceBank != null)
        {
            resourceBank.maxHealth = stats.maxHealth;
            resourceBank.currentHealth = resolvedCurrentHealth;
        }

        if (combatHealth != null)
        {
            combatHealth.stats = stats;
            combatHealth.resourceBank = resourceBank;
            combatHealth.currentHealth = resolvedCurrentHealth;
        }

        ConfigureEnemyController(enemy, stats);
        ApplyCleanupBossVisualScale(enemy, snapshot, isCleanupBoss);

        if (debugScalingBreakdown)
        {
            EnemyDifficultyDirector director = ResolveDifficultyDirector();
            Debug.Log(
                "[EnemyScaling] " +
                $"name={enemy.name} species={(identity != null ? identity.species.ToString() : "Unknown")} rank={rank} phase={(director != null ? director.CurrentPhase.ToString() : "None")} " +
                $"baseHP={snapshot.maxHealth:F1} baseATK={snapshot.physicalAttack:F1} baseDEF={snapshot.physicalDefense:F1} baseSATK={snapshot.specialAttack:F1} baseSDEF={snapshot.specialDefense:F1} baseSPD={snapshot.speed:F2} " +
                $"baseMulHP={baseHealthMultiplier:F2} baseMulATK={baseAttackMultiplier:F2} baseMulDEF={baseDefenseMultiplier:F2} baseMulSATK={baseSpecialAttackMultiplier:F2} baseMulSDEF={baseSpecialDefenseMultiplier:F2} baseMulSPD={baseSpeedMultiplier:F2} " +
                $"timeMul={timeMultiplier:F2} rankHP={rankHealthMultiplier:F2} rankATK={rankAttackMultiplier:F2} rankDEF={rankDefenseMultiplier:F2} rankSATK={rankMagicMultiplier:F2} rankSDEF={rankResistanceMultiplier:F2} rankSPD={rankSpeedMultiplier:F2} " +
                $"specialBossHP={specialBossHpMultiplier:F2} specialBossATK={specialBossAttackMultiplier:F2} specialBossDEF={specialBossDefenseMultiplier:F2} specialBossSATK={specialBossSpecialAttackMultiplier:F2} specialBossSDEF={specialBossSpecialDefenseMultiplier:F2} specialBossSPD={specialBossSpeedMultiplier:F2} " +
                $"finalHP={stats.maxHealth:F1} finalATK={stats.physicalAttack:F1} finalDEF={stats.physicalDefense:F1} finalSATK={stats.specialAttack:F1} finalSDEF={stats.specialDefense:F1} finalSPD={stats.speed:F2}",
                enemy);
        }

        if (isCleanupBoss && debugCleanupBossScaling)
        {
            EnemyDifficultyDirector director = ResolveDifficultyDirector();
            float cleanupDynamicHpMultiplier = 1f;
            float cleanupDynamicAttackMultiplier = 1f;
            float cleanupDynamicDefenseMultiplier = 1f;
            float cleanupDynamicSpecialAttackMultiplier = 1f;
            float cleanupDynamicSpecialDefenseMultiplier = 1f;
            float cleanupDynamicSpeedMultiplier = 1f;
            if (ultimateBossModifiersByEnemyId.TryGetValue(enemyId, out UltimateBossModifiers cleanupModifiers))
            {
                cleanupDynamicHpMultiplier = cleanupModifiers.hpMultiplier;
                cleanupDynamicAttackMultiplier = cleanupModifiers.attackMultiplier;
                cleanupDynamicDefenseMultiplier = cleanupModifiers.defenseMultiplier;
                cleanupDynamicSpecialAttackMultiplier = cleanupModifiers.specialAttackMultiplier;
                cleanupDynamicSpecialDefenseMultiplier = cleanupModifiers.specialDefenseMultiplier;
                cleanupDynamicSpeedMultiplier = cleanupModifiers.speedMultiplier;
            }

            Debug.Log(
                "[CleanupBossScaling] " +
                $"name={enemy.name} phase={(director != null ? director.CurrentPhase.ToString() : "None")} " +
                $"baseHP={snapshot.maxHealth:F1} baseATK={snapshot.physicalAttack:F1} baseDEF={snapshot.physicalDefense:F1} baseSATK={snapshot.specialAttack:F1} baseSDEF={snapshot.specialDefense:F1} baseSPD={snapshot.speed:F2} " +
                $"rankHP={rankHealthMultiplier:F2} rankATK={rankAttackMultiplier:F2} rankDEF={rankDefenseMultiplier:F2} rankSATK={rankMagicMultiplier:F2} rankSDEF={rankResistanceMultiplier:F2} rankSPD={rankSpeedMultiplier:F2} " +
                $"cleanupDynamicHP={cleanupDynamicHpMultiplier:F2} cleanupDynamicATK={cleanupDynamicAttackMultiplier:F2} cleanupDynamicDEF={cleanupDynamicDefenseMultiplier:F2} cleanupDynamicSATK={cleanupDynamicSpecialAttackMultiplier:F2} cleanupDynamicSDEF={cleanupDynamicSpecialDefenseMultiplier:F2} cleanupDynamicSPD={cleanupDynamicSpeedMultiplier:F2} " +
                $"cleanupHP={cleanupBossHealthMultiplier:F2} cleanupATK={cleanupBossAttackMultiplier:F2} cleanupDEF={cleanupBossDefenseMultiplier:F2} cleanupSATK={cleanupBossSpecialAttackMultiplier:F2} cleanupSDEF={cleanupBossSpecialDefenseMultiplier:F2} cleanupSPD={cleanupBossSpeedMultiplier:F2} cleanupScale={cleanupBossScaleMultiplier:F2} cleanupAttackInterval={cleanupBossAttackIntervalMultiplier:F2} cleanupOutgoingDamage={cleanupBossOutgoingDamageMultiplier:F2} cleanupReward={cleanupBossRewardMultiplier:F2} " +
                $"finalHP={stats.maxHealth:F1} finalATK={stats.physicalAttack:F1} finalDEF={stats.physicalDefense:F1} finalSATK={stats.specialAttack:F1} finalSDEF={stats.specialDefense:F1} finalSPD={stats.speed:F2}",
                enemy);
        }
    }

    private void ConfigureEnemyController(GameObject enemy, CombatStats stats)
    {
        if (enemy == null || stats == null)
        {
            return;
        }

        EnemyController controller = enemy.GetComponent<EnemyController>();
        MonsterIdentity identity = enemy.GetComponent<MonsterIdentity>();
        if (controller == null || identity == null)
        {
            return;
        }

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
                identity.attackStyle = MonsterAttackStyle.Melee;
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
                identity.attackStyle = MonsterAttackStyle.ElementalBoss;
            }
            else
            {
                range = 8f;
                hitRange = 8f;
                cooldown = 2.2f;
            }
        }

        float moveSpeed = controller.BaseMoveSpeed > 0f ? controller.BaseMoveSpeed : ResolveMoveSpeed(identity, stats.speed);
        BattleDamageType damageType = identity.attackStyle == MonsterAttackStyle.Melee ? BattleDamageType.Physical : BattleDamageType.Special;
        float attackPower = damageType == BattleDamageType.Physical ? stats.physicalAttack : stats.specialAttack;
        float attackIntervalMultiplier = ResolveRankAttackIntervalMultiplier(identity.rank);
        float outgoingDamageMultiplier = ResolveRankOutgoingDamageMultiplier(identity.rank);
        if (enemy == cleanupBossInstance)
        {
            attackIntervalMultiplier *= Mathf.Max(0.01f, cleanupBossAttackIntervalMultiplier);
            outgoingDamageMultiplier *= Mathf.Max(0.01f, cleanupBossOutgoingDamageMultiplier);
        }

        controller.ConfigureRuntime(
            moveSpeed,
            0.8f,
            range,
            hitRange,
            cooldown,
            attackPower,
            identity.attackStyle,
            attackIntervalMultiplier,
            outgoingDamageMultiplier);
        controller.SetTarget(ResolveActivePlayerTarget(), "Spawner");
    }

    private void ApplyCleanupBossVisualScale(GameObject enemy, MonsterBaseSnapshot snapshot, bool isCleanupBoss)
    {
        if (!isCleanupBoss || !snapshot.hasScaleTarget || !TryGetEnemyScaleTarget(enemy, out Transform scaleTarget) || scaleTarget == null)
        {
            return;
        }

        scaleTarget.localScale = Vector3.Scale(snapshot.scaleTargetLocalScale, Vector3.one * Mathf.Max(0.01f, cleanupBossScaleMultiplier));
        AlignBossVisualToGround(enemy);
    }

    private static bool TryGetEnemyScaleTarget(GameObject enemy, out Transform scaleTarget)
    {
        scaleTarget = null;
        if (enemy == null)
        {
            return false;
        }

        MonsterRankVisual rankVisual = enemy.GetComponent<MonsterRankVisual>();
        if (rankVisual != null && rankVisual.visualRoot != null)
        {
            scaleTarget = rankVisual.visualRoot;
            return true;
        }

        scaleTarget = enemy.transform;
        return scaleTarget != null;
    }

    private static float ResolveMoveSpeed(MonsterIdentity identity, float statSpeed)
    {
        if (statSpeed > 0f)
        {
            return Mathf.Max(0.1f, statSpeed);
        }

        if (identity == null)
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

    private float ResolveRankHealthMultiplier(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Boss:
                return Mathf.Max(0.01f, bossHealthMultiplier);
            case MonsterRank.Elite:
                return Mathf.Max(0.01f, eliteHealthMultiplier);
            default:
                return Mathf.Max(0.01f, normalHealthMultiplier);
        }
    }

    private float ResolveRankAttackMultiplier(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Boss:
                return Mathf.Max(0.01f, bossAttackMultiplier);
            case MonsterRank.Elite:
                return Mathf.Max(0.01f, eliteAttackMultiplier);
            default:
                return Mathf.Max(0.01f, normalAttackMultiplier);
        }
    }

    private float ResolveRankDefenseMultiplier(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Boss:
                return Mathf.Max(0.01f, bossDefenseMultiplier);
            case MonsterRank.Elite:
                return Mathf.Max(0.01f, eliteDefenseMultiplier);
            default:
                return Mathf.Max(0.01f, normalDefenseMultiplier);
        }
    }

    private float ResolveRankMagicMultiplier(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Boss:
                return Mathf.Max(0.01f, bossMagicMultiplier);
            case MonsterRank.Elite:
                return Mathf.Max(0.01f, eliteMagicMultiplier);
            default:
                return Mathf.Max(0.01f, normalMagicMultiplier);
        }
    }

    private float ResolveRankResistanceMultiplier(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Boss:
                return Mathf.Max(0.01f, bossResistanceMultiplier);
            case MonsterRank.Elite:
                return Mathf.Max(0.01f, eliteResistanceMultiplier);
            default:
                return Mathf.Max(0.01f, normalResistanceMultiplier);
        }
    }

    private float ResolveRankSpeedMultiplier(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Boss:
                return Mathf.Max(0.01f, bossSpeedMultiplier);
            case MonsterRank.Elite:
                return Mathf.Max(0.01f, eliteSpeedMultiplier);
            default:
                return Mathf.Max(0.01f, normalSpeedMultiplier);
        }
    }

    private float ResolveRankAttackIntervalMultiplier(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Boss:
                return Mathf.Max(0.1f, bossAttackIntervalMultiplier);
            case MonsterRank.Elite:
                return Mathf.Max(0.1f, eliteAttackIntervalMultiplier);
            default:
                return 1f;
        }
    }

    private float ResolveRankOutgoingDamageMultiplier(MonsterRank rank)
    {
        switch (rank)
        {
            case MonsterRank.Boss:
                return Mathf.Max(0.01f, bossOutgoingDamageMultiplier);
            case MonsterRank.Elite:
                return Mathf.Max(0.01f, eliteOutgoingDamageMultiplier);
            default:
                return 1f;
        }
    }

    private Vector3 ResolveSpawnPosition(GameObject selectedEnemyPrefab)
    {
        Transform activePlayer = ResolveActivePlayerTarget();
        if (spawnAroundPlayer && activePlayer != null)
        {
            float minDistance = Mathf.Max(0f, Mathf.Min(spawnMinDistance, spawnMaxDistance));
            float maxDistance = Mathf.Max(minDistance + 0.1f, Mathf.Max(spawnMinDistance, spawnMaxDistance));
            Vector2 offset2D = Random.insideUnitCircle.normalized * Random.Range(minDistance, maxDistance);
            if (offset2D.sqrMagnitude < 0.0001f)
            {
                offset2D = Vector2.right * minDistance;
            }

            Vector3 spawnPosition = activePlayer.position + new Vector3(offset2D.x, 0f, offset2D.y);
            spawnPosition.y = activePlayer.position.y;

            return spawnPosition;
        }

        Vector3 fallbackPosition = transform.position;
        float randomX = Random.Range(-fallbackSpawnRadiusX, fallbackSpawnRadiusX);
        float randomZ = Random.Range(-fallbackSpawnRadiusZ, fallbackSpawnRadiusZ);
        fallbackPosition += new Vector3(randomX, 0f, randomZ);
        fallbackPosition.y = transform.position.y;

        return fallbackPosition;
    }

    private void ResolvePlayerTarget()
    {
        if (playerBootstrap == null)
        {
            playerBootstrap = FindObjectOfType<Player2Bootstrap>();
            if (playerBootstrap != null)
            {
                playerBootstrap.EnsureInitializedForSpawn();
            }
        }

        Transform activePlayer = ResolveActivePlayerTarget();
        if (activePlayer != null)
        {
            playerTarget = activePlayer;
            return;
        }

        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject playerObject = GameObject.FindWithTag(playerTag);
            if (playerObject != null)
            {
                playerTarget = playerObject.transform;
            }
        }
    }

    private void ResolveEnemyLayer()
    {
        resolvedEnemyLayer = LayerMask.NameToLayer(enemyLayerName);
    }

    private void ConfigureEnemyLayerCollision()
    {
        if (!ignoreEnemySelfCollision)
        {
            return;
        }

        if (resolvedEnemyLayer < 0)
        {
            return;
        }

        if (enemyLayerCollisionConfigured)
        {
            return;
        }

        Physics.IgnoreLayerCollision(resolvedEnemyLayer, resolvedEnemyLayer, true);
        enemyLayerCollisionConfigured = true;
    }

    private void ConfigureSpawnedEnemyPhysics(GameObject enemy)
    {
        if (enemy == null)
        {
            return;
        }

        if (resolvedEnemyLayer >= 0)
        {
            SetLayerRecursively(enemy, resolvedEnemyLayer);
            ConfigureEnemyLayerCollision();
        }

        Rigidbody body = enemy.GetComponent<Rigidbody>();
        if (body != null)
        {
            RigidbodyConstraints constraints = body.constraints;
            constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            MonsterIdentity identity = enemy.GetComponent<MonsterIdentity>();
            bool shouldFreezeVerticalPosition = freezeEnemyVerticalPosition ||
                                                (identity != null && identity.rank == MonsterRank.Boss);
            if (shouldFreezeVerticalPosition)
            {
                if (identity != null && identity.rank == MonsterRank.Boss)
                {
                    AlignBossVisualToGround(enemy);
                }

                constraints |= RigidbodyConstraints.FreezePositionY;
                Vector3 velocity = body.linearVelocity;
                velocity.y = 0f;
                body.linearVelocity = velocity;
            }
            else
            {
                constraints &= ~RigidbodyConstraints.FreezePositionY;
            }

            body.constraints = constraints;
        }

        if (!ignoreEnemySelfCollision || resolvedEnemyLayer >= 0)
        {
            return;
        }

        Collider[] newEnemyColliders = enemy.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            GameObject otherEnemy = aliveEnemies[i];
            if (otherEnemy == null || otherEnemy == enemy)
            {
                continue;
            }

            Collider[] otherColliders = otherEnemy.GetComponentsInChildren<Collider>(true);
            IgnoreColliderPairs(newEnemyColliders, otherColliders);
        }
    }

    private static void AlignBossVisualToGround(GameObject enemy)
    {
        // Boss visual grounding is now handled by MonsterRankVisual using a stable local offset.
        // Keep root / rigidbody / collider anchored to gameplay ground.
    }

    public void SnapEnemyToGround(GameObject enemy, LayerMask groundMask, string spawnSource)
    {
        if (enemy == null)
        {
            return;
        }

        Rigidbody body = enemy.GetComponent<Rigidbody>();
        MonsterRankVisual rankVisual = enemy.GetComponent<MonsterRankVisual>();
        Transform visualRoot = rankVisual != null ? rankVisual.RuntimeVisualRoot : enemy.transform.Find("Visual_Slime");
        Renderer visualRenderer = ResolveBossVisualRenderer(visualRoot);
        Collider primaryCollider = ResolveBossPrimaryBodyCollider(enemy);
        Transform groundContact = EnsureBossGroundContactTransform(visualRoot);
        EnemyController enemyController = enemy.GetComponent<EnemyController>();
        Transform target = ResolveActivePlayerTarget();

        Vector3 positionBeforeSnap = enemy.transform.position;
        Vector3 finalScale = visualRoot != null ? visualRoot.localScale : enemy.transform.localScale;
        Vector3 visualRootLocalPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;

        Physics.SyncTransforms();

        float colliderMinY = primaryCollider != null ? primaryCollider.bounds.min.y : float.PositiveInfinity;
        float rendererMinY = visualRenderer != null ? visualRenderer.bounds.min.y : float.PositiveInfinity;
        float groundContactY = groundContact != null ? groundContact.position.y : float.PositiveInfinity;
        float chosenBottomY = groundContactY;
        string chosenGroundingSource = groundContact != null ? "GroundContact" : "RootFallback";
        float chosenBottomYImmediatelyAfterAssignment = chosenBottomY;
        string scenePath = BuildScenePath(enemy.transform);
        int scriptInstanceId = GetInstanceID();
        int visualRendererInstanceId = visualRenderer != null ? visualRenderer.GetInstanceID() : 0;

        if (debugBossGroundSnap)
        {
            Debug.Log(
                "[BossGroundTrace-A] " +
                "version=" + GroundSnapVersion +
                " scriptInstanceId=" + scriptInstanceId +
                " gameObjectScenePath=" + scenePath +
                " rendererMinY=" + rendererMinY.ToString("F3") +
                " groundContactY=" + groundContactY.ToString("F3") +
                " chosenBottomY=" + chosenBottomY.ToString("F3") +
                " chosenGroundingSource=" + chosenGroundingSource +
                " chosenBottomYImmediatelyAfterAssignment=" + chosenBottomYImmediatelyAfterAssignment.ToString("F3") +
                " visualRendererInstanceId=" + visualRendererInstanceId,
                enemy);
        }

        if (float.IsInfinity(chosenBottomY))
        {
            chosenBottomY = enemy.transform.position.y;
            chosenGroundingSource = "RootFallback";
        }

        if (debugBossGroundSnap)
        {
            Debug.Log(
                "[BossGroundTrace-B] " +
                "version=" + GroundSnapVersion +
                " scriptInstanceId=" + scriptInstanceId +
                " gameObjectScenePath=" + scenePath +
                " rendererMinY=" + rendererMinY.ToString("F3") +
                " groundContactY=" + groundContactY.ToString("F3") +
                " chosenBottomY=" + chosenBottomY.ToString("F3") +
                " chosenGroundingSource=" + chosenGroundingSource +
                " chosenBottomYImmediatelyAfterAssignment=" + chosenBottomYImmediatelyAfterAssignment.ToString("F3") +
                " visualRendererInstanceId=" + visualRendererInstanceId,
                enemy);
        }

        float boundsTopY = Mathf.Max(
            primaryCollider != null ? primaryCollider.bounds.max.y : enemy.transform.position.y,
            visualRenderer != null ? visualRenderer.bounds.max.y : enemy.transform.position.y);

        Vector3 rayOrigin = new Vector3(
            enemy.transform.position.x,
            Mathf.Max(enemy.transform.position.y, boundsTopY) + Mathf.Max(1f, enemyGroundSnapRayStartHeight),
            enemy.transform.position.z);

        if (!TryRaycastGroundBelow(enemy, rayOrigin, Mathf.Max(1f, enemyGroundSnapRayDistance + enemyGroundSnapRayStartHeight), groundMask, out RaycastHit groundHit))
        {
            if (debugBossGroundSnap)
            {
                Debug.LogWarning(
                    "[BossGroundDebug] " +
                    "version=" + GroundSnapVersion +
                    " script instance id=" + scriptInstanceId +
                    " gameObject scene path=" + scenePath +
                    "boss name=" + enemy.name +
                    " spawn source=" + spawnSource +
                    " position before snap=" + positionBeforeSnap +
                    " position after snap=" + enemy.transform.position +
                    " final scale=" + finalScale +
                    " visual root local position=" + visualRootLocalPosition +
                    " ground contact object=" + (groundContact != null ? groundContact.name : "None") +
                    " ground contact world y=" + groundContactY.ToString("F3") +
                    " visible ground offset configuration=" + bossVisibleGroundContactLocalYOffset.ToString("F3") +
                    " body renderer name=" + (visualRenderer != null ? visualRenderer.name : "None") +
                    " rendererMinY raw=" + rendererMinY.ToString("F3") +
                    " visual bottom y=" + (visualRenderer != null ? visualRenderer.bounds.min.y.ToString("F3") : "n/a") +
                    " collider bottom y=" + (primaryCollider != null ? primaryCollider.bounds.min.y.ToString("F3") : "n/a") +
                    " chosenBottomY immediately after assignment=" + chosenBottomYImmediatelyAfterAssignment.ToString("F3") +
                    " chosen bottom y=" + chosenBottomY.ToString("F3") +
                    " chosen grounding source=" + chosenGroundingSource +
                    " visualRenderer instance id=" + visualRendererInstanceId +
                    " collider type=" + (primaryCollider != null ? primaryCollider.GetType().Name : "None") +
                    " collider center=" + ResolveColliderCenter(primaryCollider) +
                    " collider radius/size=" + ResolveColliderSize(primaryCollider) +
                    " collider bounds min y=" + (primaryCollider != null ? primaryCollider.bounds.min.y.ToString("F3") : "n/a") +
                    " renderer bounds min y=" + (visualRenderer != null ? visualRenderer.bounds.min.y.ToString("F3") : "n/a") +
                    " ground hit object=None ground hit layer=None ground hit y=n/a calculated correction y=0.000" +
                    " vertical distance to target=" + (target != null ? Mathf.Abs(target.position.y - enemy.transform.position.y).ToString("F3") : "n/a") +
                    " isGrounded=" + (enemyController != null ? "see EnemyChaseDiag" : "n/a") +
                    " canAttack=" + (enemyController != null ? "see EnemyChaseDiag" : "n/a") +
                    " rigidbody useGravity=" + (body != null && body.useGravity) +
                    " rigidbody isKinematic=" + (body != null && body.isKinematic),
                    enemy);
            }

            return;
        }

        float correctionY = groundHit.point.y - chosenBottomY;
        if (Mathf.Abs(correctionY) > Mathf.Max(0f, enemyGroundSnapTolerance))
        {
            Vector3 snappedPosition = enemy.transform.position + Vector3.up * correctionY;
            enemy.transform.position = snappedPosition;
            if (body != null)
            {
                body.position = snappedPosition;
                Vector3 velocity = body.linearVelocity;
                velocity.y = 0f;
                body.linearVelocity = velocity;
            }
        }

        Physics.SyncTransforms();

        if (debugBossGroundSnap)
        {
            Debug.Log(
                "[BossGroundDebug] " +
                "version=" + GroundSnapVersion +
                " script instance id=" + scriptInstanceId +
                " gameObject scene path=" + scenePath +
                "boss name=" + enemy.name +
                " spawn source=" + spawnSource +
                " position before snap=" + positionBeforeSnap +
                " position after snap=" + enemy.transform.position +
                " final scale=" + finalScale +
                " visual root local position=" + visualRootLocalPosition +
                " ground contact object=" + (groundContact != null ? groundContact.name : "None") +
                " ground contact world y=" + groundContactY.ToString("F3") +
                " visible ground offset configuration=" + bossVisibleGroundContactLocalYOffset.ToString("F3") +
                " body renderer name=" + (visualRenderer != null ? visualRenderer.name : "None") +
                " rendererMinY raw=" + rendererMinY.ToString("F3") +
                " visual bottom y=" + (visualRenderer != null ? visualRenderer.bounds.min.y.ToString("F3") : "n/a") +
                " collider bottom y=" + (primaryCollider != null ? primaryCollider.bounds.min.y.ToString("F3") : "n/a") +
                " chosenBottomY immediately after assignment=" + chosenBottomYImmediatelyAfterAssignment.ToString("F3") +
                " chosen bottom y=" + chosenBottomY.ToString("F3") +
                " chosen grounding source=" + chosenGroundingSource +
                " chosenBottomY before correction=" + chosenBottomY.ToString("F3") +
                " visualRenderer instance id=" + visualRendererInstanceId +
                " collider type=" + (primaryCollider != null ? primaryCollider.GetType().Name : "None") +
                " collider center=" + ResolveColliderCenter(primaryCollider) +
                " collider radius/size=" + ResolveColliderSize(primaryCollider) +
                " collider bounds min y=" + (primaryCollider != null ? primaryCollider.bounds.min.y.ToString("F3") : "n/a") +
                " renderer bounds min y=" + (visualRenderer != null ? visualRenderer.bounds.min.y.ToString("F3") : "n/a") +
                " ground hit object=" + groundHit.collider.name +
                " ground hit layer=" + LayerMask.LayerToName(groundHit.collider.gameObject.layer) +
                " ground hit y=" + groundHit.point.y.ToString("F3") +
                " calculated correction y=" + correctionY.ToString("F3") +
                " vertical distance to target=" + (target != null ? Mathf.Abs(target.position.y - enemy.transform.position.y).ToString("F3") : "n/a") +
                " isGrounded=" + (enemyController != null ? "see EnemyChaseDiag" : "n/a") +
                " canAttack=" + (enemyController != null ? "see EnemyChaseDiag" : "n/a") +
                " rigidbody useGravity=" + (body != null && body.useGravity) +
                " rigidbody isKinematic=" + (body != null && body.isKinematic),
                enemy);
        }
    }

    private void SnapSplitChildToGround(GameObject enemy, string spawnSource)
    {
        if (enemy == null)
        {
            return;
        }

        MonsterIdentity identity = enemy.GetComponent<MonsterIdentity>();
        if (identity != null && identity.rank == MonsterRank.Boss)
        {
            SnapEnemyToGround(enemy, enemyGroundSnapLayerMask, spawnSource);
            return;
        }

        Rigidbody body = enemy.GetComponent<Rigidbody>();
        MonsterRankVisual rankVisual = enemy.GetComponent<MonsterRankVisual>();
        Transform visualRoot = rankVisual != null ? rankVisual.RuntimeVisualRoot : enemy.transform.Find("Visual_Slime");
        Renderer visualRenderer = ResolveBossVisualRenderer(visualRoot != null ? visualRoot : enemy.transform);
        Collider primaryCollider = ResolveBossPrimaryBodyCollider(enemy);

        Physics.SyncTransforms();

        float chosenBottomY;
        string chosenSource;
        if (visualRenderer != null)
        {
            chosenBottomY = visualRenderer.bounds.min.y;
            chosenSource = "RendererBottom";
        }
        else if (primaryCollider != null)
        {
            chosenBottomY = primaryCollider.bounds.min.y;
            chosenSource = "ColliderBottom";
        }
        else
        {
            chosenBottomY = enemy.transform.position.y;
            chosenSource = "Root";
        }

        float boundsTopY = Mathf.Max(
            primaryCollider != null ? primaryCollider.bounds.max.y : enemy.transform.position.y,
            visualRenderer != null ? visualRenderer.bounds.max.y : enemy.transform.position.y);
        Vector3 rayOrigin = new Vector3(
            enemy.transform.position.x,
            Mathf.Max(enemy.transform.position.y, boundsTopY) + Mathf.Max(1f, enemyGroundSnapRayStartHeight),
            enemy.transform.position.z);

        if (!TryRaycastGroundBelow(enemy, rayOrigin, Mathf.Max(1f, enemyGroundSnapRayDistance + enemyGroundSnapRayStartHeight), enemyGroundSnapLayerMask, out RaycastHit groundHit))
        {
            if (debugMonsterSpawnState)
            {
                Debug.LogWarning(
                    "[SplitGroundSnap] " +
                    "enemy=" + enemy.name +
                    " source=" + spawnSource +
                    " result=no-ground-hit" +
                    " position=" + enemy.transform.position +
                    " chosenSource=" + chosenSource,
                    enemy);
            }

            return;
        }

        float correctionY = groundHit.point.y - chosenBottomY;
        if (Mathf.Abs(correctionY) <= Mathf.Max(0f, enemyGroundSnapTolerance))
        {
            return;
        }

        Vector3 snappedPosition = enemy.transform.position + Vector3.up * correctionY;
        enemy.transform.position = snappedPosition;
        if (body != null)
        {
            body.position = snappedPosition;
            Vector3 velocity = body.linearVelocity;
            velocity.y = 0f;
            body.linearVelocity = velocity;
        }

        Physics.SyncTransforms();

        if (debugMonsterSpawnState)
        {
            Debug.Log(
                "[SplitGroundSnap] " +
                "enemy=" + enemy.name +
                " source=" + spawnSource +
                " chosenSource=" + chosenSource +
                " groundY=" + groundHit.point.y.ToString("F3") +
                " correctionY=" + correctionY.ToString("F3") +
                " finalPosition=" + enemy.transform.position,
                enemy);
        }
    }

    private Transform EnsureBossGroundContactTransform(Transform visualRoot)
    {
        if (visualRoot == null)
        {
            return null;
        }

        Transform groundContact = visualRoot.Find("GroundContact");
        if (groundContact == null)
        {
            GameObject groundContactObject = new GameObject("GroundContact");
            groundContact = groundContactObject.transform;
            groundContact.SetParent(visualRoot, false);
        }

        Vector3 localPosition = groundContact.localPosition;
        localPosition.x = 0f;
        localPosition.y = bossVisibleGroundContactLocalYOffset;
        localPosition.z = 0f;
        groundContact.localPosition = localPosition;
        groundContact.localRotation = Quaternion.identity;
        groundContact.localScale = Vector3.one;
        return groundContact;
    }

    private static string BuildScenePath(Transform target)
    {
        if (target == null)
        {
            return "null";
        }

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private void LogBossSpawnYDiagnostics(GameObject prefab, Vector3 spawnPosition, MonsterRank rank, MonsterIdentity prefabIdentity)
    {
        Transform activePlayer = ResolveActivePlayerTarget();
        Debug.Log(
            $"[BossSpawnY] prefab={(prefab != null ? prefab.name : "null")} prefab.transform.position.y={(prefab != null ? prefab.transform.position.y.ToString("F2") : "n/a")} " +
            $"requested spawnPosition.y={spawnPosition.y:F2} final spawnPosition.y before Instantiate={spawnPosition.y:F2} player.position.y={(activePlayer != null ? activePlayer.position.y.ToString("F2") : "n/a")} " +
            $"spawner.position.y={transform.position.y:F2} rank={rank} attackStyle={(prefabIdentity != null ? prefabIdentity.attackStyle.ToString() : "Unknown")}",
            this);
    }

    private static bool TryRaycastGroundBelow(GameObject enemy, Vector3 rayOrigin, float rayDistance, LayerMask groundMask, out RaycastHit selectedHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            Mathf.Max(1f, rayDistance),
            groundMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
        {
            selectedHit = default;
            return false;
        }

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null || hitCollider.isTrigger)
            {
                continue;
            }

            if (enemy != null && (hitCollider.transform == enemy.transform || hitCollider.transform.IsChildOf(enemy.transform)))
            {
                continue;
            }

            selectedHit = hit;
            return true;
        }

        selectedHit = default;
        return false;
    }

    private static string ResolveColliderCenter(Collider collider)
    {
        if (collider is SphereCollider sphere)
        {
            return sphere.center.ToString();
        }

        if (collider is CapsuleCollider capsule)
        {
            return capsule.center.ToString();
        }

        if (collider is BoxCollider box)
        {
            return box.center.ToString();
        }

        return "n/a";
    }

    private static string ResolveColliderSize(Collider collider)
    {
        if (collider is SphereCollider sphere)
        {
            return "radius=" + sphere.radius.ToString("F3");
        }

        if (collider is CapsuleCollider capsule)
        {
            return "radius=" + capsule.radius.ToString("F3") + " height=" + capsule.height.ToString("F3");
        }

        if (collider is BoxCollider box)
        {
            return "size=" + box.size;
        }

        return "n/a";
    }

    private static void IgnoreColliderPairs(Collider[] first, Collider[] second)
    {
        if (first == null || second == null)
        {
            return;
        }

        for (int i = 0; i < first.Length; i++)
        {
            Collider left = first[i];
            if (left == null)
            {
                continue;
            }

            for (int j = 0; j < second.Length; j++)
            {
                Collider right = second[j];
                if (right == null || left == right)
                {
                    continue;
                }

                Physics.IgnoreCollision(left, right, true);
            }
        }
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            if (child != null)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }

    private Transform ResolveActivePlayerTarget()
    {
        if (playerBootstrap != null && playerBootstrap.CurrentPlayerTransform != null)
        {
            return playerBootstrap.CurrentPlayerTransform;
        }

        return playerTarget;
    }

    private void CachePrefabPools()
    {
        fallbackNormalEnemyPrefabs.Clear();
        fallbackEliteEnemyPrefabs.Clear();
        fallbackBossEnemyPrefabs.Clear();

        if (enemyPrefabs == null)
        {
            return;
        }

        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            GameObject prefab = enemyPrefabs[i];
            if (prefab == null)
            {
                continue;
            }

            MonsterIdentity identity = prefab.GetComponent<MonsterIdentity>();
            MonsterRank rank = identity != null ? identity.rank : MonsterRank.Normal;
            switch (rank)
            {
                case MonsterRank.Boss:
                    fallbackBossEnemyPrefabs.Add(prefab);
                    break;
                case MonsterRank.Elite:
                    fallbackEliteEnemyPrefabs.Add(prefab);
                    break;
                default:
                    fallbackNormalEnemyPrefabs.Add(prefab);
                    break;
            }
        }
    }

    private static List<GameObject> ResolvePool(GameObject[] primaryPool, List<GameObject> fallbackPool)
    {
        List<GameObject> resolved = new List<GameObject>();
        if (primaryPool != null)
        {
            for (int i = 0; i < primaryPool.Length; i++)
            {
                if (primaryPool[i] != null)
                {
                    resolved.Add(primaryPool[i]);
                }
            }
        }

        return resolved.Count > 0 ? resolved : fallbackPool;
    }

    private void InitializeTodTracking()
    {
        todController = FindObjectOfType<TODController>();
        elapsedTrackedGameHours = 0f;
        nextBossCheckElapsedGameHours = Mathf.Max(0.1f, bossCheckIntervalGameHours);

        if (todController != null && todController.todGlobalParameters != null)
        {
            previousTodTime = todController.todGlobalParameters.CurrentTime;
            todTimeInitialized = true;
        }
    }

    private void CheckBossSpawnByGameHours()
    {
        if (externalTestPauseActive)
        {
            return;
        }

        if (todController == null)
        {
            todController = FindObjectOfType<TODController>();
        }

        if (todController == null || todController.todGlobalParameters == null)
        {
            return;
        }

        float currentTodTime = todController.todGlobalParameters.CurrentTime;
        if (!todTimeInitialized)
        {
            previousTodTime = currentTodTime;
            todTimeInitialized = true;
            elapsedTrackedGameHours = 0f;
            nextBossCheckElapsedGameHours = Mathf.Max(0.1f, bossCheckIntervalGameHours);
            return;
        }

        float deltaHours = currentTodTime - previousTodTime;
        if (deltaHours < 0f)
        {
            deltaHours += 24f;
        }

        previousTodTime = currentTodTime;
        elapsedTrackedGameHours += Mathf.Max(0f, deltaHours);

        float intervalHours = Mathf.Max(0.1f, bossCheckIntervalGameHours);
        while (elapsedTrackedGameHours >= nextBossCheckElapsedGameHours)
        {
            TrySpawnBossFromTimedCheck();
            nextBossCheckElapsedGameHours += intervalHours;
        }
    }

    private void CheckFinalMomentBossTrigger()
    {
        if (externalTestPauseActive)
        {
            return;
        }

        EnemyDifficultyDirector director = ResolveDifficultyDirector();
        DifficultyPhase currentPhase = director != null ? director.CurrentPhase : DifficultyPhase.Normal;
        bool enteredFinalRush = currentPhase == DifficultyPhase.FinalRush &&
                                lastObservedDifficultyPhase != DifficultyPhase.FinalRush;
        lastObservedDifficultyPhase = currentPhase;

        if (!enteredFinalRush || finalMomentBossTriggered)
        {
            return;
        }

        finalMomentBossTriggered = true;
        EnsureFinalMomentBoss();
    }

    private void CheckSpawnStoppedUltimateBossResolution()
    {
        if (externalTestPauseActive)
        {
            return;
        }

        EnemyDifficultyDirector director = ResolveDifficultyDirector();
        if (director == null || director.CurrentPhase != DifficultyPhase.SpawnStopped || spawnStoppedResolutionTriggered)
        {
            return;
        }

        spawnStoppedResolutionTriggered = true;
        ResolveUltimateBossAfterCountdown();
    }

    private void ResolveUltimateBossAfterCountdown()
    {
        if (externalTestPauseActive)
        {
            return;
        }

        CleanupTrackedEnemies();

        int remainingNonBossEnemyCount = CountAliveNonBossEnemies();
        ClearAliveNonBossEnemies();

        GameObject existingBoss = FindAliveBossEnemy();
        GameObject ultimateBoss = existingBoss;
        if (ultimateBoss == null)
        {
            ultimateBoss = SpawnBossIgnoringDifficulty();
        }

        if (ultimateBoss == null)
        {
            return;
        }

        PromoteBossToFinalMoment(ultimateBoss);
        MarkBossAsUltimate(ultimateBoss, remainingNonBossEnemyCount);
        cleanupBossInstance = ultimateBoss;
        ApplyCurrentMultiplierToMonster(ultimateBoss, refillCurrentHealth: true);
        ConfigureCleanupBossPhaseSplit(ultimateBoss);
        ResolveDifficultyDirector()?.ArmSpawnStoppedBossVictory(ultimateBoss);
    }

    private void EnsureFinalMomentBoss()
    {
        if (externalTestPauseActive)
        {
            return;
        }

        CleanupTrackedEnemies();

        GameObject existingBoss = FindAliveBossEnemy();
        if (existingBoss != null)
        {
            PromoteBossToFinalMoment(existingBoss);
            return;
        }

        if (CountAliveEnemies(MonsterRank.Boss) >= Mathf.Max(0, maxAliveBossCount))
        {
            return;
        }

        GameObject spawnedBoss = SpawnBossEnemy();
        PromoteBossToFinalMoment(spawnedBoss);
    }

    private GameObject FindAliveBossEnemy()
    {
        CleanupTrackedEnemies();
        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            GameObject enemy = aliveEnemies[i];
            if (!IsEnemyAliveForTracking(enemy))
            {
                continue;
            }

            MonsterIdentity identity = enemy.GetComponent<MonsterIdentity>();
            if (identity != null && identity.rank == MonsterRank.Boss)
            {
                return enemy;
            }
        }

        return null;
    }

    private void PromoteBossToFinalMoment(GameObject boss)
    {
        if (boss == null)
        {
            return;
        }

        MonsterIdentity identity = boss.GetComponent<MonsterIdentity>();
        if (identity == null || identity.rank != MonsterRank.Boss)
        {
            return;
        }

        int bossId = boss.GetInstanceID();
        if (!finalMomentBossEnemyIds.Add(bossId))
        {
            return;
        }

        ApplyCurrentMultiplierToMonster(boss, refillCurrentHealth: true);
    }

    private void MarkBossAsUltimate(GameObject boss, int remainingNonBossEnemyCount)
    {
        if (boss == null)
        {
            return;
        }

        UltimateBossModifiers modifiers = new UltimateBossModifiers
        {
            remainingEnemyCount = Mathf.Max(0, remainingNonBossEnemyCount),
            hpMultiplier = Mathf.Max(
                Mathf.Max(0.01f, finalMomentBossHpMultiplier),
                Mathf.Min(Mathf.Max(1f, 1f + Mathf.Max(0, remainingNonBossEnemyCount) * Mathf.Max(0f, ultimateBossHpPerRemainingEnemy)), Mathf.Max(1f, ultimateBossMaxHpMultiplier))),
            attackMultiplier = Mathf.Max(
                Mathf.Max(0.01f, finalMomentBossAttackMultiplier),
                Mathf.Min(Mathf.Max(1f, 1f + Mathf.Max(0, remainingNonBossEnemyCount) * Mathf.Max(0f, ultimateBossAttackPerRemainingEnemy)), Mathf.Max(1f, ultimateBossMaxAttackMultiplier))),
            defenseMultiplier = Mathf.Max(0.01f, finalMomentBossDefenseMultiplier),
            specialAttackMultiplier = Mathf.Max(0.01f, finalMomentBossSpecialAttackMultiplier),
            specialDefenseMultiplier = Mathf.Max(0.01f, finalMomentBossSpecialDefenseMultiplier),
            speedMultiplier = Mathf.Max(0.01f, Mathf.Min(Mathf.Max(0.01f, ultimateBossSpeedMultiplier), 1.1f))
        };

        ultimateBossModifiersByEnemyId[boss.GetInstanceID()] = modifiers;
        ApplyCurrentMultiplierToMonster(boss, refillCurrentHealth: true);
    }

    private void ConfigureCleanupBossPhaseSplit(GameObject cleanupBoss)
    {
        if (cleanupBoss == null)
        {
            return;
        }

        CleanupBossPhaseSplit phaseSplit = cleanupBoss.GetComponent<CleanupBossPhaseSplit>();
        if (phaseSplit == null)
        {
            phaseSplit = cleanupBoss.AddComponent<CleanupBossPhaseSplit>();
        }

        phaseSplit.Initialize(
            this,
            cleanupBoss,
            cleanupBossPhaseSplitEnabled,
            cleanupBossSplitHealthThresholds,
            cleanupBossSplitCountPerThreshold,
            cleanupBossSplitScatterRadius,
            cleanupBossSplitChildRank,
            cleanupBossSplitChildHealthRatio,
            cleanupBossSplitChildAttackRatio,
            cleanupBossSplitChildDefenseRatio,
            cleanupBossSplitChildSpeedRatio,
            cleanupBossSplitChildScaleRatio,
            cleanupBossSplitChildrenCanSplit,
            cleanupBossRewardMultiplier,
            debugCleanupBossPhaseSplit);
    }

    private int CountAliveNonBossEnemies()
    {
        CleanupTrackedEnemies();
        int count = 0;
        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            GameObject enemy = aliveEnemies[i];
            if (!IsEnemyAliveForTracking(enemy))
            {
                continue;
            }

            MonsterIdentity identity = enemy.GetComponent<MonsterIdentity>();
            MonsterRank enemyRank = identity != null ? identity.rank : MonsterRank.Normal;
            if (enemyRank != MonsterRank.Boss)
            {
                count++;
            }
        }

        return count;
    }

    private void ClearAliveNonBossEnemies()
    {
        CleanupTrackedEnemies();
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemy = aliveEnemies[i];
            if (!IsEnemyAliveForTracking(enemy))
            {
                continue;
            }

            MonsterIdentity identity = enemy.GetComponent<MonsterIdentity>();
            MonsterRank enemyRank = identity != null ? identity.rank : MonsterRank.Normal;
            if (enemyRank == MonsterRank.Boss)
            {
                continue;
            }

            aliveEnemies.RemoveAt(i);
            monsterBaseSnapshots.Remove(enemy.GetInstanceID());
            finalMomentBossEnemyIds.Remove(enemy.GetInstanceID());
            ultimateBossModifiersByEnemyId.Remove(enemy.GetInstanceID());
            Destroy(enemy);
        }
    }

    private GameObject SpawnBossIgnoringDifficulty()
    {
        List<GameObject> sourcePool = ResolvePool(bossEnemyPrefabs, fallbackBossEnemyPrefabs);
        if (sourcePool == null || sourcePool.Count == 0)
        {
            return null;
        }

        int randomIndex = Random.Range(0, sourcePool.Count);
        GameObject selectedEnemy = sourcePool[randomIndex];
        if (selectedEnemy == null)
        {
            return null;
        }

        MonsterIdentity prefabIdentity = selectedEnemy.GetComponent<MonsterIdentity>();
        MonsterSpecies? runtimeSpecies = prefabIdentity != null ? prefabIdentity.species : (MonsterSpecies?)null;

        Vector3 spawnPosition = ResolveSpawnPosition(selectedEnemy);

        GameObject spawnedEnemy = Instantiate(selectedEnemy, spawnPosition, Quaternion.identity);
        MonsterIdentity cloneIdentity = spawnedEnemy.GetComponent<MonsterIdentity>();
        if (cloneIdentity == null)
        {
            cloneIdentity = spawnedEnemy.AddComponent<MonsterIdentity>();
        }

        if (runtimeSpecies.HasValue)
        {
            cloneIdentity.species = runtimeSpecies.Value;
        }

        cloneIdentity.rank = MonsterRank.Boss;
        ApplyOfficialMonsterRuntimeSetup(
            spawnedEnemy,
            runtimeSpecies,
            MonsterRank.Boss,
            ResolveActivePlayerTarget(),
            trackAsAlive: true,
            initializeDeathNotifier: true,
            source: "EnemySpawner");

        return spawnedEnemy;
    }

    private void TrySpawnBossFromTimedCheck()
    {
        if (externalTestPauseActive)
        {
            return;
        }

        if (!CanSpawnByDifficulty("BossTimed"))
        {
            return;
        }

        CleanupTrackedEnemies();
        if (CountAliveEnemies(MonsterRank.Boss) >= Mathf.Max(0, maxAliveBossCount))
        {
            return;
        }

        if (Random.value <= Mathf.Clamp01(bossSpawnChancePerCheck))
        {
            SpawnBossEnemy();
        }
    }

    private int CountAliveEnemies(MonsterRank rank)
    {
        CleanupTrackedEnemies();
        int count = 0;
        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            GameObject enemy = aliveEnemies[i];
            if (enemy == null)
            {
                continue;
            }

            MonsterIdentity identity = enemy.GetComponent<MonsterIdentity>();
            MonsterRank enemyRank = identity != null ? identity.rank : MonsterRank.Normal;
            if (enemyRank == rank)
            {
                count++;
            }
        }

        return count;
    }

    private bool TrySpawnEliteIfAvailable()
    {
        if (externalTestPauseActive)
        {
            return false;
        }

        if (!CanSpawnByDifficulty("Elite"))
        {
            return false;
        }

        if (CountAliveEnemies(MonsterRank.Elite) >= Mathf.Max(0, maxAliveEliteCount))
        {
            return false;
        }

        return SpawnEliteEnemy() != null;
    }

    public int CountAliveEnemiesForVictory()
    {
        CleanupTrackedEnemies();
        int count = 0;
        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            if (IsEnemyAliveForTracking(aliveEnemies[i]))
            {
                count++;
            }
        }

        return count;
    }

    private void CleanupTrackedEnemies()
    {
        for (int i = aliveEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemy = aliveEnemies[i];
            if (IsEnemyAliveForTracking(enemy))
            {
                continue;
            }

            aliveEnemies.RemoveAt(i);
            if (enemy != null)
            {
                finalMomentBossEnemyIds.Remove(enemy.GetInstanceID());
                ultimateBossModifiersByEnemyId.Remove(enemy.GetInstanceID());
            }
        }

        List<int> staleKeys = null;
        foreach (KeyValuePair<int, MonsterBaseSnapshot> pair in monsterBaseSnapshots)
        {
            bool exists = false;
            for (int i = 0; i < aliveEnemies.Count; i++)
            {
                if (aliveEnemies[i] != null && aliveEnemies[i].GetInstanceID() == pair.Key)
                {
                    exists = true;
                    break;
                }
            }

            if (exists)
            {
                continue;
            }

            if (staleKeys == null)
            {
                staleKeys = new List<int>();
            }

            staleKeys.Add(pair.Key);
        }

        if (staleKeys == null)
        {
            return;
        }

        for (int i = 0; i < staleKeys.Count; i++)
        {
            monsterBaseSnapshots.Remove(staleKeys[i]);
            finalMomentBossEnemyIds.Remove(staleKeys[i]);
            ultimateBossModifiersByEnemyId.Remove(staleKeys[i]);
        }
    }

    public void OnEnemyDestroyed(GameObject destroyedEnemy)
    {
        NotifyDifficultyDirectorOfEnemyDeath(destroyedEnemy);

        if (destroyedEnemy != null)
        {
            if (destroyedEnemy == cleanupBossInstance)
            {
                cleanupBossInstance = null;
            }

            aliveEnemies.Remove(destroyedEnemy);
            monsterBaseSnapshots.Remove(destroyedEnemy.GetInstanceID());
            finalMomentBossEnemyIds.Remove(destroyedEnemy.GetInstanceID());
            ultimateBossModifiersByEnemyId.Remove(destroyedEnemy.GetInstanceID());
        }

        CleanupTrackedEnemies();
    }

    public void OnEnemyDestroyed()
    {
        CleanupTrackedEnemies();
    }

    private EnemyDifficultyDirector ResolveDifficultyDirector()
    {
        if (difficultyDirector == null)
        {
            difficultyDirector = EnemyDifficultyDirector.GetOrCreateInstance();
        }

        return difficultyDirector;
    }

    private bool CanSpawnByDifficulty(string source)
    {
        EnemyDifficultyDirector director = ResolveDifficultyDirector();
        bool canSpawn = director == null || director.ShouldAllowSpawning;
        if (!canSpawn && debugDifficultySpawnLogs)
        {
            Debug.Log(
                "[SpawnBlocked] " +
                $"reason={(director != null ? director.CurrentPhase.ToString() : "DirectorMissing")} source={source}",
                this);
        }

        return canSpawn;
    }

    private float ResolveDifficultyAdjustedInterval(float baseInterval, float minimumInterval)
    {
        float resolvedInterval = Mathf.Max(minimumInterval, baseInterval);
        EnemyDifficultyDirector director = ResolveDifficultyDirector();
        if (director == null)
        {
            return resolvedInterval;
        }

        return Mathf.Max(minimumInterval, resolvedInterval * director.CurrentSpawnIntervalMultiplier);
    }

    private int ResolveDifficultyAdjustedMaxNormalMonsterCount()
    {
        int resolvedCount = Mathf.Max(0, maxNormalMonsterCount);
        EnemyDifficultyDirector director = ResolveDifficultyDirector();
        if (director == null)
        {
            return resolvedCount;
        }

        return Mathf.Max(0, resolvedCount + director.CurrentExtraMaxAlive);
    }

    private int ResolveDifficultyTargetNormalCount()
    {
        EnemyDifficultyDirector director = ResolveDifficultyDirector();
        if (director == null)
        {
            return Mathf.Max(0, baseNormalMonsterCount);
        }

        int desiredCount = Mathf.Max(0, baseNormalMonsterCount) + director.CurrentDifficultyLevel + director.CurrentSpawnBatchCount;
        return Mathf.Clamp(desiredCount, 0, ResolveDifficultyAdjustedMaxNormalMonsterCount());
    }

    private int ResolveDifficultySpawnBatchCount()
    {
        EnemyDifficultyDirector director = ResolveDifficultyDirector();
        return director != null ? Mathf.Max(1, director.CurrentSpawnBatchCount) : 1;
    }

    private static bool IsEnemyAliveForTracking(GameObject enemy)
    {
        if (enemy == null || !enemy.activeInHierarchy)
        {
            return false;
        }

        CombatHealth combatHealth = enemy.GetComponent<CombatHealth>();
        return combatHealth == null || !combatHealth.IsDead;
    }

    private void NotifyDifficultyDirectorOfEnemyDeath(GameObject destroyedEnemy)
    {
        if (destroyedEnemy == null)
        {
            return;
        }

        CombatHealth combatHealth = destroyedEnemy.GetComponent<CombatHealth>();
        if (combatHealth == null || !combatHealth.IsDead)
        {
            return;
        }

        MonsterIdentity identity = destroyedEnemy.GetComponent<MonsterIdentity>();
        bool wasBoss = identity != null && identity.rank == MonsterRank.Boss;
        EnemyDifficultyDirector director = ResolveDifficultyDirector();
        if (director == null)
        {
            return;
        }

        bool shouldSpawnBoss = director.NotifyEnemyKilled(wasBoss);
        if (!shouldSpawnBoss)
        {
            return;
        }

        TrySpawnBossFromKillCount();
    }

    private void TrySpawnBossFromKillCount()
    {
        if (externalTestPauseActive)
        {
            return;
        }

        if (!CanSpawnByDifficulty("BossByKills"))
        {
            return;
        }

        CleanupTrackedEnemies();
        if (CountAliveEnemies(MonsterRank.Boss) >= Mathf.Max(0, maxAliveBossCount))
        {
            return;
        }

        if (debugDifficultySpawnLogs)
        {
            string bossPrefabName = ResolveBossPrefabName();
            Debug.Log(
                "[BossSpawnByKills] " +
                $"bossPrefab={bossPrefabName} position={ResolveSpawnPositionPreview()}",
                this);
        }

        SpawnBossEnemy();
    }

    private string ResolveBossPrefabName()
    {
        List<GameObject> bossPool = ResolvePool(bossEnemyPrefabs, fallbackBossEnemyPrefabs);
        if (bossPool == null || bossPool.Count == 0 || bossPool[0] == null)
        {
            return "None";
        }

        return bossPool[0].name;
    }

    private string ResolveSpawnPositionPreview()
    {
        List<GameObject> bossPool = ResolvePool(bossEnemyPrefabs, fallbackBossEnemyPrefabs);
        if (bossPool == null || bossPool.Count == 0 || bossPool[0] == null)
        {
            return "Unavailable";
        }

        return ResolveSpawnPosition(bossPool[0]).ToString("F2");
    }

    private Transform ResolveBossVisualTransform(GameObject boss)
    {
        if (boss == null)
        {
            return null;
        }

        SlimeAnimationController slimeAnimation = boss.GetComponent<SlimeAnimationController>();
        if (slimeAnimation != null && slimeAnimation.VisualRoot != null)
        {
            return slimeAnimation.VisualRoot;
        }

        Transform namedVisual = boss.transform.Find("Visual_Slime");
        if (namedVisual != null)
        {
            return namedVisual;
        }

        SpriteRenderer spriteRenderer = boss.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null)
        {
            return spriteRenderer.transform;
        }

        Renderer renderer = boss.GetComponentInChildren<Renderer>(true);
        return renderer != null ? renderer.transform : boss.transform;
    }

    private Renderer ResolveBossVisualRenderer(Transform visualTransform)
    {
        if (visualTransform == null)
        {
            return null;
        }

        Renderer directRenderer = visualTransform.GetComponent<Renderer>();
        if (IsValidBossBodyRenderer(directRenderer))
        {
            return directRenderer;
        }

        MeshRenderer[] meshRenderers = visualTransform.GetComponentsInChildren<MeshRenderer>(true);
        Renderer bestRenderer = SelectBestBossBodyRenderer(meshRenderers);
        if (bestRenderer != null)
        {
            return bestRenderer;
        }

        SpriteRenderer[] spriteRenderers = visualTransform.GetComponentsInChildren<SpriteRenderer>(true);
        bestRenderer = SelectBestBossBodyRenderer(spriteRenderers);
        if (bestRenderer != null)
        {
            return bestRenderer;
        }

        Renderer[] renderers = visualTransform.GetComponentsInChildren<Renderer>(true);
        return SelectBestBossBodyRenderer(renderers);
    }

    private Collider ResolveBossPrimaryBodyCollider(GameObject enemy)
    {
        if (enemy == null)
        {
            return null;
        }

        Collider[] colliders = enemy.GetComponentsInChildren<Collider>(true);
        Collider bestCollider = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            if (ShouldIgnoreBossBodyColliderTransform(collider.transform))
            {
                continue;
            }

            float volume = collider.bounds.size.x * collider.bounds.size.y * collider.bounds.size.z;
            float score = volume;
            if (collider.transform == enemy.transform)
            {
                score += 1000f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestCollider = collider;
            }
        }

        return bestCollider;
    }

    private static Renderer SelectBestBossBodyRenderer(Renderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0)
        {
            return null;
        }

        Renderer bestRenderer = null;
        float bestArea = float.MinValue;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsValidBossBodyRenderer(renderer))
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            float areaScore = bounds.size.x * bounds.size.y;
            if (bestRenderer == null || areaScore > bestArea)
            {
                bestRenderer = renderer;
                bestArea = areaScore;
            }
        }

        return bestRenderer;
    }

    private static bool IsValidBossBodyRenderer(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled)
        {
            return false;
        }

        if (renderer is TrailRenderer || renderer is ParticleSystemRenderer || renderer is LineRenderer)
        {
            return false;
        }

        Transform target = renderer.transform;
        string name = target.name.ToLowerInvariant();
        if (name.Contains("shadow") ||
            name.Contains("healthbar") ||
            name.Contains("hpbar") ||
            name.Contains("trail") ||
            name.Contains("particle") ||
            name.Contains("effect") ||
            name.Contains("aura") ||
            name.Contains("warning") ||
            name.Contains("indicator"))
        {
            return false;
        }

        if (target.GetComponentInParent<Canvas>(true) != null)
        {
            return false;
        }

        return true;
    }

    private Collider EnsureBossScaledHurtbox(GameObject enemy, Collider sourceCollider)
    {
        Transform hurtboxRoot = enemy.transform.Find("BossScaledHurtbox");
        if (hurtboxRoot == null)
        {
            GameObject hurtboxObject = new GameObject("BossScaledHurtbox");
            hurtboxRoot = hurtboxObject.transform;
            hurtboxRoot.SetParent(enemy.transform, false);
        }

        hurtboxRoot.gameObject.SetActive(true);
        hurtboxRoot.gameObject.layer = enemy.layer;
        hurtboxRoot.gameObject.tag = enemy.tag;
        hurtboxRoot.localPosition = Vector3.zero;
        hurtboxRoot.localRotation = Quaternion.identity;
        hurtboxRoot.localScale = Vector3.one;

        Renderer visualRenderer = useBossVisualBoundsHurtbox
            ? ResolveBossVisualRenderer(ResolveBossVisualTransform(enemy))
            : null;
        if (visualRenderer != null)
        {
            RemoveColliderComponentsExcept<BoxCollider>(hurtboxRoot);
            BoxCollider visualBoundsHurtbox = hurtboxRoot.GetComponent<BoxCollider>();
            if (visualBoundsHurtbox == null)
            {
                visualBoundsHurtbox = hurtboxRoot.gameObject.AddComponent<BoxCollider>();
            }

            ConfigureBossVisualBoundsHurtbox(enemy.transform, visualRenderer.bounds, visualBoundsHurtbox);
            return visualBoundsHurtbox;
        }

        float radiusMultiplier = Mathf.Max(1f, bossHurtboxRadiusMultiplier) * Mathf.Max(0.1f, bossHurtboxSizeMultiplier);
        Vector3 colliderCenterOffset = bossHurtboxCenterOffset + Vector3.up * bossHurtboxCenterYOffset;
        Vector3 colliderPadding = AbsVector3(bossHurtboxExtraPadding);

        if (sourceCollider is SphereCollider sourceSphere)
        {
            RemoveColliderComponentsExcept<SphereCollider>(hurtboxRoot);
            SphereCollider hurtbox = hurtboxRoot.GetComponent<SphereCollider>();
            if (hurtbox == null)
            {
                hurtbox = hurtboxRoot.gameObject.AddComponent<SphereCollider>();
            }

            hurtbox.isTrigger = true;
            hurtbox.center = sourceSphere.center + colliderCenterOffset;
            hurtbox.radius = Mathf.Max(0.05f, sourceSphere.radius * radiusMultiplier + MaxComponent(colliderPadding) * 0.5f);
            return hurtbox;
        }

        if (sourceCollider is CapsuleCollider sourceCapsule)
        {
            RemoveColliderComponentsExcept<CapsuleCollider>(hurtboxRoot);
            CapsuleCollider hurtbox = hurtboxRoot.GetComponent<CapsuleCollider>();
            if (hurtbox == null)
            {
                hurtbox = hurtboxRoot.gameObject.AddComponent<CapsuleCollider>();
            }

            hurtbox.isTrigger = true;
            hurtbox.direction = sourceCapsule.direction;
            hurtbox.center = sourceCapsule.center + colliderCenterOffset;
            hurtbox.radius = Mathf.Max(0.05f, sourceCapsule.radius * radiusMultiplier + Mathf.Max(colliderPadding.x, colliderPadding.z) * 0.5f);
            hurtbox.height = Mathf.Max(hurtbox.radius * 2f, sourceCapsule.height * radiusMultiplier + colliderPadding.y);
            return hurtbox;
        }

        if (sourceCollider is BoxCollider sourceBox)
        {
            RemoveColliderComponentsExcept<BoxCollider>(hurtboxRoot);
            BoxCollider hurtbox = hurtboxRoot.GetComponent<BoxCollider>();
            if (hurtbox == null)
            {
                hurtbox = hurtboxRoot.gameObject.AddComponent<BoxCollider>();
            }

            hurtbox.isTrigger = true;
            hurtbox.center = sourceBox.center + colliderCenterOffset;
            hurtbox.size = sourceBox.size * radiusMultiplier + colliderPadding;
            return hurtbox;
        }

        return null;
    }

    private void ConfigureBossVisualBoundsHurtbox(Transform enemyRoot, Bounds visualBounds, BoxCollider hurtbox)
    {
        if (enemyRoot == null || hurtbox == null)
        {
            return;
        }

        Vector3 worldMin = visualBounds.min;
        Vector3 worldMax = visualBounds.max;
        Vector3 localMin = enemyRoot.InverseTransformPoint(worldMin);
        Vector3 localMax = enemyRoot.InverseTransformPoint(worldMax);
        Vector3 localCenter = (localMin + localMax) * 0.5f;
        Vector3 localSize = new Vector3(
            Mathf.Abs(localMax.x - localMin.x),
            Mathf.Abs(localMax.y - localMin.y),
            Mathf.Abs(localMax.z - localMin.z));

        float minDepth = Mathf.Max(bossHurtboxMinimumDepth, Mathf.Max(localSize.x, localSize.y) * 0.35f);
        if (localSize.z < minDepth)
        {
            localSize.z = minDepth;
        }

        float sizeMultiplier = Mathf.Max(0.1f, bossHurtboxSizeMultiplier);
        Vector3 legacyPadding = localSize * Mathf.Max(0f, bossHurtboxRadiusMultiplier - 1f) * 0.05f;
        Vector3 customPadding = AbsVector3(bossHurtboxExtraPadding);
        localCenter += bossHurtboxCenterOffset + Vector3.up * bossHurtboxCenterYOffset;
        hurtbox.isTrigger = true;
        hurtbox.center = localCenter;
        hurtbox.size = new Vector3(
            Mathf.Max(0.1f, localSize.x * sizeMultiplier + legacyPadding.x + customPadding.x),
            Mathf.Max(0.1f, localSize.y * sizeMultiplier + legacyPadding.y + customPadding.y),
            Mathf.Max(0.1f, localSize.z * sizeMultiplier + legacyPadding.z + customPadding.z));
    }

    private static Vector3 AbsVector3(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static float MaxComponent(Vector3 value)
    {
        return Mathf.Max(value.x, Mathf.Max(value.y, value.z));
    }

    private static void RemoveColliderComponentsExcept<T>(Transform root) where T : Collider
    {
        Collider[] colliders = root.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider is T)
            {
                continue;
            }

            Object.Destroy(collider);
        }
    }

    private void LogBossHurtboxShapeDetails(GameObject enemy, Collider mainCollider, Collider hurtboxCollider)
    {
        Debug.Log(
            "[BossHurtboxDebug] " +
            "object=" + enemy.name +
            " mainDetails=" + ResolveColliderShapeDetails(mainCollider) +
            " hurtboxDetails=" + ResolveColliderShapeDetails(hurtboxCollider),
            enemy);
    }

    private static string ResolveColliderShapeDetails(Collider collider)
    {
        if (collider == null)
        {
            return "null";
        }

        if (collider is SphereCollider sphere)
        {
            return "center=" + sphere.center + " radius=" + sphere.radius.ToString("F2");
        }

        if (collider is CapsuleCollider capsule)
        {
            return "center=" + capsule.center + " radius=" + capsule.radius.ToString("F2") + " height=" + capsule.height.ToString("F2");
        }

        if (collider is BoxCollider box)
        {
            return "center=" + box.center + " size=" + box.size;
        }

        return collider.GetType().Name;
    }

    private static bool ShouldIgnoreBossBodyColliderTransform(Transform target)
    {
        if (target == null)
        {
            return true;
        }

        string path = target.name.ToLowerInvariant();
        Transform current = target.parent;
        while (current != null)
        {
            path += "/" + current.name.ToLowerInvariant();
            current = current.parent;
        }

        string[] ignoredKeywords =
        {
            "hitbox",
            "attack",
            "detect",
            "range",
            "sensor",
            "skill",
            "canvas",
            "healthbar",
            "ui",
            "bossscaledhurtbox"
        };

        for (int i = 0; i < ignoredKeywords.Length; i++)
        {
            if (path.Contains(ignoredKeywords[i]))
            {
                return true;
            }
        }

        return false;
    }

    public float GetConfiguredVisualScaleMultiplier(MonsterRank rank)
    {
        return rank switch
        {
            MonsterRank.Boss => enableBossVisualScale ? bossVisualScaleMultiplier : 1f,
            MonsterRank.Elite => enableEliteVisualScale ? eliteVisualScaleMultiplier : 1f,
            _ => 1f
        };
    }

    public float GetConfiguredVisualGroundOffsetY(MonsterRank rank)
    {
        return rank switch
        {
            MonsterRank.Boss => enableBossVisualGroundOffset ? bossVisualGroundOffsetY : 0f,
            MonsterRank.Elite => enableEliteVisualGroundOffset ? eliteVisualGroundOffsetY : 0f,
            _ => enableNormalVisualGroundOffset ? normalVisualGroundOffsetY : 0f
        };
    }

    public float GetConfiguredHealthBarOffsetY(MonsterRank rank)
    {
        return rank switch
        {
            MonsterRank.Boss => bossHealthBarOffsetY,
            MonsterRank.Elite => eliteHealthBarOffsetY,
            _ => normalHealthBarOffsetY
        };
    }

    public float GetConfiguredBossHurtboxRadiusMultiplier()
    {
        return Mathf.Max(1f, bossHurtboxRadiusMultiplier);
    }

    public float GetConfiguredBossHurtboxCenterYOffset()
    {
        return bossHurtboxCenterYOffset;
    }

    public bool GetConfiguredUseBossVisualBoundsHurtbox()
    {
        return useBossVisualBoundsHurtbox;
    }

    public float GetConfiguredBossHurtboxSizeMultiplier()
    {
        return Mathf.Max(0.1f, bossHurtboxSizeMultiplier);
    }

    public Vector3 GetConfiguredBossHurtboxExtraPadding()
    {
        return bossHurtboxExtraPadding;
    }

    public Vector3 GetConfiguredBossHurtboxCenterOffset()
    {
        return bossHurtboxCenterOffset;
    }

    public float GetConfiguredBossHurtboxMinimumDepth()
    {
        return Mathf.Max(0.01f, bossHurtboxMinimumDepth);
    }

    public int GetConfiguredBossHurtboxConfigHash()
    {
        int hash = 17;
        AddBoolToHash(ref hash, enableBossHurtboxScale);
        AddBoolToHash(ref hash, useBossVisualBoundsHurtbox);
        AddFloatToHash(ref hash, bossHurtboxRadiusMultiplier);
        AddFloatToHash(ref hash, bossHurtboxCenterYOffset);
        AddFloatToHash(ref hash, bossHurtboxSizeMultiplier);
        AddVector3ToHash(ref hash, bossHurtboxExtraPadding);
        AddVector3ToHash(ref hash, bossHurtboxCenterOffset);
        AddFloatToHash(ref hash, bossHurtboxMinimumDepth);
        return hash;
    }

    private static void AddBoolToHash(ref int hash, bool value)
    {
        hash = unchecked(hash * 31 + (value ? 1 : 0));
    }

    private static void AddFloatToHash(ref int hash, float value)
    {
        hash = unchecked(hash * 31 + Mathf.RoundToInt(value * 1000f));
    }

    private static void AddVector3ToHash(ref int hash, Vector3 value)
    {
        AddFloatToHash(ref hash, value.x);
        AddFloatToHash(ref hash, value.y);
        AddFloatToHash(ref hash, value.z);
    }

    private static float RoundToDecimals(float value, int decimals)
    {
        float multiplier = Mathf.Pow(10f, Mathf.Max(0, decimals));
        return Mathf.Round(value * multiplier) / multiplier;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        if (spawnAroundPlayer && playerTarget != null)
        {
            Gizmos.DrawWireSphere(playerTarget.position, spawnMinDistance);
            Gizmos.DrawWireSphere(playerTarget.position, spawnMaxDistance);
        }
        else
        {
            Gizmos.DrawWireCube(transform.position, new Vector3(fallbackSpawnRadiusX * 2f, 1f, fallbackSpawnRadiusZ * 2f));
        }
    }
}
