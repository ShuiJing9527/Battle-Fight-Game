using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AHD2TimeOfDay;

public class EnemySpawner : MonoBehaviour
{
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
    }

    [Header("Enemy")]
    public GameObject[] enemyPrefabs;
    public GameObject[] normalEnemyPrefabs;
    public GameObject[] eliteEnemyPrefabs;
    public GameObject[] bossEnemyPrefabs;
    public bool useRuntimeRankOverride = true;

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
    public float monsterStatGrowthIntervalMin = 30f;
    public float monsterStatGrowthIntervalMax = 60f;
    [Range(0.01f, 0.5f)] public float monsterStatGrowthPercentMin = 0.01f;
    [Range(0.01f, 0.5f)] public float monsterStatGrowthPercentMax = 0.05f;
    [Min(1f)] public float currentMonsterStatMultiplier = 1f;

    [Header("Rank Multipliers - Normal")]
    public float normalHealthMultiplier = 1f;
    public float normalAttackMultiplier = 1f;
    public float normalDefenseMultiplier = 1f;
    public float normalMagicMultiplier = 1f;
    public float normalResistanceMultiplier = 1f;
    public float normalSpeedMultiplier = 1f;

    [Header("Rank Multipliers - Elite")]
    public float eliteHealthMultiplier = 3f;
    public float eliteAttackMultiplier = 2f;
    public float eliteDefenseMultiplier = 1.5f;
    public float eliteMagicMultiplier = 2f;
    public float eliteResistanceMultiplier = 1.5f;
    public float eliteSpeedMultiplier = 1.1f;
    public float eliteAttackIntervalMultiplier = 1.1f;
    public float eliteOutgoingDamageMultiplier = 1f;

    [Header("Rank Multipliers - Boss")]
    public float bossHealthMultiplier = 10f;
    public float bossAttackMultiplier = 5f;
    public float bossDefenseMultiplier = 3f;
    public float bossMagicMultiplier = 5f;
    public float bossResistanceMultiplier = 3f;
    public float bossSpeedMultiplier = 1f;
    public float bossAttackIntervalMultiplier = 1.8f;
    public float bossOutgoingDamageMultiplier = 1.5f;

    [Header("Elite")]
    public float eliteSpawnIntervalMin = 20f;
    public float eliteSpawnIntervalMax = 40f;
    public int maxAliveEliteCount = 1;

    [Header("Boss")]
    public float bossCheckIntervalGameHours = 6f;
    [Range(0f, 1f)] public float bossSpawnChancePerCheck = 0.25f;
    public int maxAliveBossCount = 1;

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

    [Header("Timed Difficulty")]
    [SerializeField] private EnemyDifficultyDirector difficultyDirector;
    [SerializeField] private bool debugDifficultySpawnLogs = false;

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
    private int resolvedEnemyLayer = -1;
    private bool enemyLayerCollisionConfigured;

    private void Start()
    {
        ResolveDifficultyDirector();
        CachePrefabPools();
        ResolveEnemyLayer();
        ConfigureEnemyLayerCollision();
        ResolvePlayerTarget();
        InitializeTodTracking();
        StartCoroutine(InitialNormalSpawnRoutine());
        StartCoroutine(NormalBaseMaintenanceRoutine());
        StartCoroutine(NormalReinforcementRoutine());
        StartCoroutine(EliteSpawnRoutine());
        StartCoroutine(MonsterGrowthRoutine());
    }

    private void Update()
    {
        ResolvePlayerTarget();
        CheckBossSpawnByGameHours();
    }

    private IEnumerator InitialNormalSpawnRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, startDelay));
        SpawnNormalEnemiesUpTo(baseNormalMonsterCount);
    }

    private IEnumerator NormalBaseMaintenanceRoutine()
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

    private IEnumerator NormalReinforcementRoutine()
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

    private IEnumerator EliteSpawnRoutine()
    {
        while (true)
        {
            float baseWaitSeconds = Random.Range(
                Mathf.Max(0.1f, Mathf.Min(eliteSpawnIntervalMin, eliteSpawnIntervalMax)),
                Mathf.Max(Mathf.Min(eliteSpawnIntervalMin, eliteSpawnIntervalMax) + 0.1f, Mathf.Max(eliteSpawnIntervalMin, eliteSpawnIntervalMax)));
            float waitSeconds = ResolveDifficultyAdjustedInterval(baseWaitSeconds, 0.1f);

            yield return new WaitForSeconds(waitSeconds);

            CleanupTrackedEnemies();
            ResolvePlayerTarget();

            if (CountAliveEnemies(MonsterRank.Elite) < Mathf.Max(0, maxAliveEliteCount))
            {
                SpawnEliteEnemy();
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

    private void SpawnNormalEnemy()
    {
        SpawnFromPool(ResolvePool(normalEnemyPrefabs, fallbackNormalEnemyPrefabs), MonsterRank.Normal);
    }

    private void SpawnEliteEnemy()
    {
        SpawnFromPool(ResolvePool(eliteEnemyPrefabs, fallbackEliteEnemyPrefabs), MonsterRank.Elite);
    }

    private void SpawnBossEnemy()
    {
        SpawnFromPool(ResolvePool(bossEnemyPrefabs, fallbackBossEnemyPrefabs), MonsterRank.Boss);
    }

    private void SpawnFromPool(List<GameObject> sourcePool, MonsterRank forcedRank)
    {
        if (!CanSpawnByDifficulty(forcedRank.ToString()))
        {
            return;
        }

        if (sourcePool == null || sourcePool.Count == 0)
        {
            return;
        }

        int randomIndex = Random.Range(0, sourcePool.Count);
        GameObject selectedEnemy = sourcePool[randomIndex];
        if (selectedEnemy == null)
        {
            return;
        }

        MonsterIdentity prefabIdentity = selectedEnemy.GetComponent<MonsterIdentity>();
        MonsterSpecies? runtimeSpecies = prefabIdentity != null ? prefabIdentity.species : (MonsterSpecies?)null;
        MonsterRank runtimeRank = forcedRank;

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

        cloneIdentity.rank = runtimeRank;

        MonsterCombatAutoSetup.Configure(spawnedEnemy, runtimeSpecies, runtimeRank);
        ResolveDifficultyDirector()?.ApplyDifficultyToEnemy(spawnedEnemy);

        RegisterSpawnedEnemy(spawnedEnemy);

        EnemyDeathNotifier notifier = spawnedEnemy.GetComponent<EnemyDeathNotifier>();
        if (notifier == null)
        {
            notifier = spawnedEnemy.AddComponent<EnemyDeathNotifier>();
        }
        notifier.Initialize(this);

        EnemyController enemyController = spawnedEnemy.GetComponent<EnemyController>();
        if (enemyController != null)
        {
            enemyController.SetTarget(ResolveActivePlayerTarget(), "Spawner");
        }
    }

    public void SpawnSplitNormalsFromElite(GameObject eliteSource, int count, float scatterRadius)
    {
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

        for (int i = 0; i < count; i++)
        {
            GameObject selectedEnemy = ResolveNormalSplitPrefab(sourcePool, sourceIdentity.species);
            if (selectedEnemy == null)
            {
                continue;
            }

            Vector3 spawnPosition = eliteSource.transform.position + ResolveSplitOffset(scatterRadius, i, count);
            spawnPosition.y = selectedEnemy.transform.position.y;
            GameObject spawnedEnemy = Instantiate(selectedEnemy, spawnPosition, Quaternion.identity);

            MonsterIdentity cloneIdentity = spawnedEnemy.GetComponent<MonsterIdentity>();
            if (cloneIdentity == null)
            {
                cloneIdentity = spawnedEnemy.AddComponent<MonsterIdentity>();
            }

            cloneIdentity.species = sourceIdentity.species;
            cloneIdentity.rank = MonsterRank.Normal;
            cloneIdentity.suppressRuneDrop = true;

            MonsterCombatAutoSetup.Configure(spawnedEnemy, sourceIdentity.species, MonsterRank.Normal);
            ResolveDifficultyDirector()?.ApplyDifficultyToEnemy(spawnedEnemy);
            RegisterSpawnedEnemy(spawnedEnemy);

            EnemyDeathNotifier notifier = spawnedEnemy.GetComponent<EnemyDeathNotifier>();
            if (notifier == null)
            {
                notifier = spawnedEnemy.AddComponent<EnemyDeathNotifier>();
            }
            notifier.Initialize(this);

            EnemyController enemyController = spawnedEnemy.GetComponent<EnemyController>();
            if (enemyController != null)
            {
                enemyController.SetTarget(ResolveActivePlayerTarget(), "Spawner");
            }
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

        monsterBaseSnapshots[enemy.GetInstanceID()] = new MonsterBaseSnapshot
        {
            initialized = true,
            maxHealth = Mathf.Max(1f, stats.maxHealth),
            physicalAttack = Mathf.Max(0f, stats.physicalAttack),
            physicalDefense = Mathf.Max(0f, stats.physicalDefense),
            specialAttack = Mathf.Max(0f, stats.specialAttack),
            specialDefense = Mathf.Max(0f, stats.specialDefense),
            speed = Mathf.Max(0.1f, stats.speed),
            luck = Mathf.Max(0f, stats.luck)
        };
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
        float healthMultiplier = timeMultiplier * ResolveRankHealthMultiplier(rank);
        float attackMultiplier = timeMultiplier * ResolveRankAttackMultiplier(rank);
        float defenseMultiplier = timeMultiplier * ResolveRankDefenseMultiplier(rank);
        float magicMultiplier = timeMultiplier * ResolveRankMagicMultiplier(rank);
        float resistanceMultiplier = timeMultiplier * ResolveRankResistanceMultiplier(rank);
        float speedMultiplier = timeMultiplier * ResolveRankSpeedMultiplier(rank);

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
        if (identity.species == MonsterSpecies.BlueSlime)
        {
            range = 1.1f;
            hitRange = 1.15f;
            cooldown = 1.45f;
        }
        if (identity.rank == MonsterRank.Elite)
        {
            range = 5f;
            hitRange = 6f;
            cooldown = 1.6f;
        }
        else if (identity.rank == MonsterRank.Boss)
        {
            range = 8f;
            hitRange = 8f;
            cooldown = 2.2f;
        }

        float moveSpeed = controller.BaseMoveSpeed > 0f ? controller.BaseMoveSpeed : ResolveMoveSpeed(identity, stats.speed);
        BattleDamageType damageType = identity.attackStyle == MonsterAttackStyle.Melee ? BattleDamageType.Physical : BattleDamageType.Special;
        float attackPower = damageType == BattleDamageType.Physical ? stats.physicalAttack : stats.specialAttack;
        controller.ConfigureRuntime(
            moveSpeed,
            0.8f,
            range,
            hitRange,
            cooldown,
            attackPower,
            identity.attackStyle,
            ResolveRankAttackIntervalMultiplier(identity.rank),
            ResolveRankOutgoingDamageMultiplier(identity.rank));
        controller.SetTarget(ResolveActivePlayerTarget(), "Spawner");
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
            if (selectedEnemyPrefab != null)
            {
                spawnPosition.y = selectedEnemyPrefab.transform.position.y;
            }
            return spawnPosition;
        }

        Vector3 fallbackPosition = transform.position;
        float randomX = Random.Range(-fallbackSpawnRadiusX, fallbackSpawnRadiusX);
        float randomZ = Random.Range(-fallbackSpawnRadiusZ, fallbackSpawnRadiusZ);
        fallbackPosition += new Vector3(randomX, 0f, randomZ);
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
            constraints &= ~RigidbodyConstraints.FreezePositionY;

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

    private void TrySpawnBossFromTimedCheck()
    {
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
        }
    }

    public void OnEnemyDestroyed(GameObject destroyedEnemy)
    {
        NotifyDifficultyDirectorOfEnemyDeath(destroyedEnemy);

        if (destroyedEnemy != null)
        {
            aliveEnemies.Remove(destroyedEnemy);
            monsterBaseSnapshots.Remove(destroyedEnemy.GetInstanceID());
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

        Vector3 previewPosition = ResolveSpawnPosition(bossPool[0]);
        return previewPosition.ToString("F2");
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
