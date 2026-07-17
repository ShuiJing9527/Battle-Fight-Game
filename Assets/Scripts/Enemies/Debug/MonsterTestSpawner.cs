using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum MonsterTestSpawnType
{
    Normal,
    Elite,
    Boss
}

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public class MonsterTestSpawner : MonoBehaviour
{
    private const string EliteSplitFromTestSuffix = "[EliteSplit_FromTest]";

    [Header("Optional Shared References")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private Player2Bootstrap playerBootstrap;
    [SerializeField] private Transform playerTarget;

    [Header("Spawn Sources")]
    [SerializeField] private GameObject[] normalEnemyPrefabs;
    [SerializeField] private GameObject[] eliteEnemyPrefabs;
    [SerializeField] private GameObject[] bossEnemyPrefabs;

    [Header("Monster Prefab")]
    [SerializeField] private GameObject testMonsterPrefab;

    [Header("Spawn Placement")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0f)] private float playerFrontSpawnDistance = 10f;
    [SerializeField] private bool useCameraVisibleSpawnPosition = true;
    [SerializeField] private float cameraVisibleSpawnHorizontalOffset = 4f;
    [SerializeField] private float cameraVisibleSpawnDepthOffset = 1.5f;
    [SerializeField] private bool preferSpawnOnPlayerRight = true;
    [SerializeField] private bool autoRepositionIfOutsideCamera = true;
    [SerializeField] private bool allowFallbackToSelfPosition = false;
    [SerializeField] private bool clearPreviousSpawnedByThisSpawner = false;
    [SerializeField] private LayerMask spawnGroundLayerMask = ~0;
    [SerializeField, Min(0f)] private float spawnGroundRaycastStartHeight = 20f;
    [SerializeField, Min(1f)] private float spawnGroundRaycastDistance = 80f;
    [SerializeField] private float suspiciousBelowGroundY = -5f;

    [Header("Test Spawn Mode")]
    [SerializeField] private MonsterTestSpawnType testSpawnType = MonsterTestSpawnType.Normal;
    [SerializeField] private bool spawnSelectedTypeOnStart = false;
    [SerializeField, Min(0f)] private float autoSpawnDelay = 0.2f;
    [SerializeField, Min(0f)] private float playerResolveTimeout = 5f;
    [SerializeField, Min(0.01f)] private float playerResolveRetryInterval = 0.1f;
    [SerializeField] private bool isolateTestSpawn = false;
    [SerializeField] private bool disableSharedEnemySpawnerDuringTest = true;
    [SerializeField] private bool resumeSharedEnemySpawnerOnDisable = false;

    [Header("Runtime")]
    [SerializeField] private List<GameObject> spawnedTestMonsters = new List<GameObject>();

    [Header("Fallback Player Rune Monster Scaling")]
    [SerializeField] private bool enablePlayerRuneStrengthScaling = true;
    [SerializeField, Min(0f)] private float strengthIncreasePerEquippedRune = 0.05f;
    [SerializeField, Min(1f)] private float maximumRuneMovementSpeedMultiplier = 1.5f;
    [SerializeField] private bool debugPlayerRuneMonsterScaling = false;

    private const string NormalSuffix = "[MonsterTest_Normal]";
    private const string EliteSuffix = "[MonsterTest_Elite]";
    private const string BossSuffix = "[MonsterTest_Boss]";
    private static readonly MonsterRankGeometrySettings FallbackNormalGeometry = new MonsterRankGeometrySettings
    {
        visualScale = Vector3.one,
        visualLocalPosition = new Vector3(0f, -0.05f, 0f),
        groundContactLocalPosition = Vector3.zero,
        physicalColliderCenter = new Vector3(0f, 0.52f, 0f),
        physicalColliderRadius = 0.5f,
        hurtboxCenter = new Vector3(0f, 0.5f, 0f),
        hurtboxSize = Vector3.one,
        healthBarOffsetY = 0f
    };
    private static readonly MonsterRankGeometrySettings FallbackEliteGeometry = new MonsterRankGeometrySettings
    {
        visualScale = new Vector3(2f, 2f, 2f),
        // Keep MonsterTestSpawner aligned with the official elite geometry.
        visualLocalPosition = new Vector3(0f, 0.10f, 0f),
        groundContactLocalPosition = Vector3.zero,
        physicalColliderCenter = new Vector3(0f, 0.77f, 0f),
        physicalColliderRadius = 0.75f,
        hurtboxCenter = new Vector3(0f, 0.85f, 0f),
        hurtboxSize = new Vector3(1.5f, 1.5f, 1.5f),
        healthBarOffsetY = 0.3f
    };
    private static readonly MonsterRankGeometrySettings FallbackBossGeometry = new MonsterRankGeometrySettings
    {
        visualScale = new Vector3(4f, 4f, 4f),
        visualLocalPosition = new Vector3(0f, 0.85f, 0f),
        groundContactLocalPosition = Vector3.zero,
        physicalColliderCenter = new Vector3(0f, 1.22f, 0f),
        physicalColliderRadius = 1.2f,
        hurtboxCenter = new Vector3(0f, 1.75f, 0f),
        hurtboxSize = new Vector3(3.68f, 3.68f, 1.288f),
        healthBarOffsetY = 0.45f
    };
    private bool sharedEnemySpawnerPausedByTest;
    private string lastPlayerResolveFailureReason = "NotResolved";
    private Coroutine autoSpawnRoutine;
    private float nextBossHurtboxRuntimeRefreshTime;
    private int lastSharedBossHurtboxConfigHash;

    private void Awake()
    {
        PrepareSharedEnemySpawnerForTest();
    }

    private void OnEnable()
    {
        PrepareSharedEnemySpawnerForTest();
    }

    private void Start()
    {
        Debug.Log(
            "[MonsterTestSpawner] " +
            "selected testSpawnType=" + testSpawnType +
            " spawnSelectedTypeOnStart=" + spawnSelectedTypeOnStart +
            " autoSpawnDelay=" + autoSpawnDelay.ToString("F2") +
            " playerResolveTimeout=" + playerResolveTimeout.ToString("F2") +
            " playerResolveRetryInterval=" + playerResolveRetryInterval.ToString("F2") +
            " isolateTestSpawn=" + isolateTestSpawn +
            " disableSharedEnemySpawnerDuringTest=" + disableSharedEnemySpawnerDuringTest,
            this);

        if (!spawnSelectedTypeOnStart)
        {
            return;
        }

        autoSpawnRoutine = StartCoroutine(SpawnSelectedTypeOnStartRoutine());
    }

    private void Update()
    {
        RefreshSpawnedBossHurtboxesIfSharedConfigChanged();
    }

    [ContextMenu("TEST/Spawn Selected Monster Type")]
    public void SpawnSelectedMonsterType()
    {
        Debug.Log(
            "[MonsterTestSpawner] " +
            "selected testSpawnType=" + testSpawnType +
            " spawnSelectedTypeOnStart=" + spawnSelectedTypeOnStart +
            " isolateTestSpawn=" + isolateTestSpawn +
            " disableSharedEnemySpawnerDuringTest=" + disableSharedEnemySpawnerDuringTest,
            this);

        PrepareSharedEnemySpawnerForTest();

        switch (testSpawnType)
        {
            case MonsterTestSpawnType.Elite:
                SpawnEliteSlime();
                break;
            case MonsterTestSpawnType.Boss:
                SpawnBossSlime();
                break;
            default:
                SpawnNormalSlime();
                break;
        }
    }

    private void OnDisable()
    {
        StopAutoSpawnRoutineIfRunning();

        if (resumeSharedEnemySpawnerOnDisable)
        {
            ResumeSharedEnemySpawnerAfterTestIfNeeded();
        }
    }

    private void OnDestroy()
    {
        StopAutoSpawnRoutineIfRunning();

        if (resumeSharedEnemySpawnerOnDisable)
        {
            ResumeSharedEnemySpawnerAfterTestIfNeeded();
        }
    }

    [ContextMenu("TEST/Spawn Normal Slime")]
    public void SpawnNormalSlime()
    {
        PrepareSharedEnemySpawnerForTest();
        SpawnMonsterForTest(MonsterRank.Normal);
    }

    [ContextMenu("TEST/Spawn Elite Slime")]
    public void SpawnEliteSlime()
    {
        PrepareSharedEnemySpawnerForTest();
        SpawnMonsterForTest(MonsterRank.Elite);
    }

    [ContextMenu("TEST/Spawn Boss Slime")]
    public void SpawnBossSlime()
    {
        PrepareSharedEnemySpawnerForTest();
        SpawnMonsterForTest(MonsterRank.Boss);
    }

    [ContextMenu("TEST/Clear Test Monsters")]
    public void ClearTestMonsters()
    {
        CleanupTrackedMonsters();

        int clearedCount = 0;
        for (int i = spawnedTestMonsters.Count - 1; i >= 0; i--)
        {
            GameObject monster = spawnedTestMonsters[i];
            if (monster == null)
            {
                spawnedTestMonsters.RemoveAt(i);
                continue;
            }

            spawnedTestMonsters.RemoveAt(i);
            clearedCount++;

            if (Application.isPlaying)
            {
                Destroy(monster);
            }
            else
            {
                DestroyImmediate(monster);
            }
        }

        GameObject[] runtimeObjects = FindObjectsOfType<GameObject>(true);
        for (int i = 0; i < runtimeObjects.Length; i++)
        {
            GameObject runtimeObject = runtimeObjects[i];
            if (runtimeObject == null || !runtimeObject.name.Contains(EliteSplitFromTestSuffix))
            {
                continue;
            }

            clearedCount++;
            if (Application.isPlaying)
            {
                Destroy(runtimeObject);
            }
            else
            {
                DestroyImmediate(runtimeObject);
            }
        }

        Debug.Log("[MonsterTestSpawner] Clear Test Monsters count=" + clearedCount, this);
    }

    [ContextMenu("TEST/Print Test Monster State")]
    public void PrintTestMonsterState()
    {
        CleanupTrackedMonsters();

        Transform target = ResolvePlayerTarget();
        Debug.Log(
            "[MonsterTestSpawner] Print Test Monster State count=" + spawnedTestMonsters.Count +
            " player=" + (target != null ? target.name : "null"),
            this);

        for (int i = 0; i < spawnedTestMonsters.Count; i++)
        {
            GameObject monster = spawnedTestMonsters[i];
            if (monster == null)
            {
                continue;
            }

            Debug.Log(BuildMonsterStateSummary(monster, target), monster);
        }
    }

    [ContextMenu("TEST/Select Last Spawned Monster")]
    public void SelectLastSpawnedMonster()
    {
        CleanupTrackedMonsters();
        GameObject lastSpawnedMonster = GetLastSpawnedMonster();
        if (lastSpawnedMonster == null)
        {
            Debug.LogWarning("[MonsterTestSpawner] SelectLastSpawnedMonster failed: no tracked spawned monster.", this);
            return;
        }

#if UNITY_EDITOR
        Selection.activeGameObject = lastSpawnedMonster;
        EditorGUIUtility.PingObject(lastSpawnedMonster);
        Debug.Log("[MonsterTestSpawner] Selected last spawned monster: " + lastSpawnedMonster.name, lastSpawnedMonster);
#else
        Debug.LogWarning("[MonsterTestSpawner] SelectLastSpawnedMonster is only available in the Unity Editor.", this);
#endif
    }

    [ContextMenu("TEST/Teleport Last Spawned Monster In Front Of Player")]
    public void TeleportLastSpawnedMonsterInFrontOfPlayer()
    {
        CleanupTrackedMonsters();
        GameObject lastSpawnedMonster = GetLastSpawnedMonster();
        if (lastSpawnedMonster == null)
        {
            Debug.LogWarning("[MonsterTestSpawner] Teleport failed: no tracked spawned monster.", this);
            return;
        }

        Transform target = ResolvePlayerTarget();
        if (target == null)
        {
            Debug.LogWarning("[MonsterTestSpawner] Teleport failed: player target not found.", this);
            return;
        }

        Vector3 forward = target.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.right;
        }

        Vector3 candidatePosition = target.position + forward.normalized * 3f;
        Vector3 resolvedPosition = FinalizeSpawnPosition(candidatePosition, "teleportFrontOfPlayer");
        if (resolvedPosition.y < suspiciousBelowGroundY)
        {
            Debug.LogWarning("[MonsterTestSpawner] Teleport aborted: resolved position below ground. position=" + resolvedPosition, this);
            return;
        }

        lastSpawnedMonster.transform.position = resolvedPosition;
        Debug.Log("[MonsterTestSpawner] Teleported last spawned monster to " + resolvedPosition, lastSpawnedMonster);
        LogSpawnedMonsterVisibility(lastSpawnedMonster, target);
    }

    private void SpawnMonsterForTest(MonsterRank rank)
    {
        CleanupTrackedMonsters();

        if (clearPreviousSpawnedByThisSpawner)
        {
            ClearTestMonsters();
        }

        string prefabSelectionSource;
        GameObject prefab = ResolveSelectedPrefab(rank, out prefabSelectionSource);
        if (prefab == null)
        {
            Debug.LogWarning("[MonsterTestSpawner] Missing prefab for rank=" + rank, this);
            return;
        }

        Transform target = ResolvePlayerTarget();
        if (!TryResolveSpawnPosition(target, out Vector3 spawnPosition, out string spawnFailureReason))
        {
            Debug.LogWarning("[MonsterTestSpawner] " + spawnFailureReason, this);
            return;
        }

        GameObject spawnedMonster = Instantiate(prefab, spawnPosition, Quaternion.identity);
        if (spawnedMonster == null)
        {
            Debug.LogWarning("[MonsterTestSpawner] Instantiate failed prefab=" + prefab.name, this);
            return;
        }
        LogNormalPrefabGeometry(spawnedMonster, "MonsterTestSpawner", "AfterInstantiate", rankGeometryExecuted: false, groundContactExecuted: false, visualTransformWriteExecuted: false);

        MonsterIdentity prefabIdentity = prefab.GetComponent<MonsterIdentity>();
        MonsterSpecies? runtimeSpecies = prefabIdentity != null ? prefabIdentity.species : (MonsterSpecies?)null;
        string prefabSource = enemySpawner != null && IsSpawnerPrefabSource(prefab, rank) ? "Shared EnemySpawner" : "Local";

        MonsterIdentity identity = spawnedMonster.GetComponent<MonsterIdentity>();
        if (identity == null)
        {
            identity = spawnedMonster.AddComponent<MonsterIdentity>();
        }

        if (enemySpawner != null)
        {
            enemySpawner.ApplyOfficialMonsterRuntimeSetup(
                spawnedMonster,
                runtimeSpecies,
                rank,
                target,
                trackAsAlive: false,
                initializeDeathNotifier: true,
                source: "MonsterTestSpawner");
            LogNormalPrefabGeometry(spawnedMonster, "MonsterTestSpawner", "ApplyOfficialEnd", rankGeometryExecuted: false, groundContactExecuted: false, visualTransformWriteExecuted: false);
        }
        else
        {
            if (runtimeSpecies.HasValue)
            {
                identity.species = runtimeSpecies.Value;
            }

            identity.rank = rank;
            MonsterCombatAutoSetup.Configure(spawnedMonster, runtimeSpecies, rank);
            LogNormalPrefabGeometry(spawnedMonster, "MonsterTestSpawnerFallback", "AfterConfigure", rankGeometryExecuted: false, groundContactExecuted: false, visualTransformWriteExecuted: false);
            ApplyFallbackOfficialConfig(spawnedMonster, rank);
            ApplyFallbackPlayerRuneMonsterScaling(spawnedMonster, target);
            LogNormalPrefabGeometry(spawnedMonster, "MonsterTestSpawnerFallback", "ApplyOfficialEnd", rankGeometryExecuted: false, groundContactExecuted: false, visualTransformWriteExecuted: false);
        }
        StartCoroutine(LogNormalPrefabGeometryAfterFirstFrame(spawnedMonster, "MonsterTestSpawner"));

        EnemyController enemyController = spawnedMonster.GetComponent<EnemyController>();
        MonsterIdentity configuredIdentity = spawnedMonster.GetComponent<MonsterIdentity>();
        if (enemyController != null)
        {
            enemyController.enabled = true;
            enemyController.SetTarget(target, "MonsterTestSpawner");
        }
        else
        {
            Debug.LogWarning("[MonsterTestSpawner] Spawned monster has no EnemyController. object=" + spawnedMonster.name, spawnedMonster);
        }

        spawnedMonster.name = AppendTestSuffix(spawnedMonster.name, rank);
        spawnedTestMonsters.Add(spawnedMonster);

        MonsterRankVisual rankVisual = spawnedMonster.GetComponent<MonsterRankVisual>();
        Transform runtimeVisualRoot = rankVisual != null ? rankVisual.RuntimeVisualRoot : null;
        string visualConfigSource = enemySpawner != null ? "EnemySpawner" : "MonsterTestSpawnerFallback";
        float configuredVisualScale = ResolveConfiguredVisualScaleMultiplier(rank);
        float configuredVisualOffset = ResolveConfiguredVisualGroundOffsetY(rank);
        bool eliteVisualApplied = rank == MonsterRank.Elite
            && rankVisual != null
            && rankVisual.LastAppliedRank == MonsterRank.Elite;

        Debug.Log(
            "[MonsterTestSpawner] " +
            "requested prefab=" + (testMonsterPrefab != null ? testMonsterPrefab.name : "null") +
            " actual spawned prefab=" + prefab.name +
            " prefab selection source=" + prefabSelectionSource +
            " rank=" + rank +
            " spawn position=" + spawnPosition +
            " visual config source=" + visualConfigSource +
            " visual scale=" + configuredVisualScale.ToString("F2") +
            " visual offset=" + configuredVisualOffset.ToString("F2") +
            " note=visual config does not control spawn position",
            spawnedMonster);

        Debug.Log(
            "[MonsterTestSpawner] Spawned " +
            "spawn type=" + ResolveSpawnTypeLabel(rank) +
            " rank=" + rank +
            " requested prefab=" + (testMonsterPrefab != null ? testMonsterPrefab.name : "null") +
            " actual spawned prefab=" + prefab.name +
            " selection source=" + prefabSelectionSource +
            " prefab source=" + prefabSource +
            " prefab=" + prefab.name +
            " object=" + spawnedMonster.name +
            " position=" + spawnedMonster.transform.position +
            " expected rank=" + rank +
            " runtime rank=" + (configuredIdentity != null ? configuredIdentity.rank.ToString() : "Unknown") +
            " species=" + (configuredIdentity != null ? configuredIdentity.species.ToString() : "Unknown") +
            " attackStyle=" + (configuredIdentity != null ? configuredIdentity.attackStyle.ToString() : "Unknown") +
            " elite visual config applied=" + eliteVisualApplied +
            " elite visual scale multiplier=" + (rank == MonsterRank.Elite ? configuredVisualScale.ToString("F2") : "n/a") +
            " final visual scale=" + (runtimeVisualRoot != null ? runtimeVisualRoot.localScale.ToString() : "null") +
            " target assigned=" + (target != null) +
            " target=" + (target != null ? target.name : "null") +
            (target == null ? " targetFailureReason=" + lastPlayerResolveFailureReason : string.Empty) +
            " hasEnemyController=" + (enemyController != null),
            spawnedMonster);

        WorldHealthBar healthBar = spawnedMonster.GetComponent<WorldHealthBar>();
        if (healthBar != null)
        {
            healthBar.RefreshWorldPositionForDebug();
        }

        LogSpawnedMonsterVisibility(spawnedMonster, target);
        TryAutoRepositionSpawnedMonsterIntoCameraView(spawnedMonster, target);
    }

    private static string ResolveSpawnTypeLabel(MonsterRank rank)
    {
        return rank switch
        {
            MonsterRank.Elite => MonsterTestSpawnType.Elite.ToString(),
            MonsterRank.Boss => MonsterTestSpawnType.Boss.ToString(),
            _ => MonsterTestSpawnType.Normal.ToString()
        };
    }

    private GameObject GetLastSpawnedMonster()
    {
        for (int i = spawnedTestMonsters.Count - 1; i >= 0; i--)
        {
            GameObject monster = spawnedTestMonsters[i];
            if (monster != null)
            {
                return monster;
            }
        }

        return null;
    }

    private void LogSpawnedMonsterVisibility(GameObject monster, Transform target)
    {
        if (monster == null)
        {
            return;
        }

        Camera activeCamera = Camera.main;
        Vector3 viewportPosition = activeCamera != null ? activeCamera.WorldToViewportPoint(monster.transform.position) : Vector3.zero;
        bool isInCameraView = activeCamera != null
            && viewportPosition.z > 0f
            && viewportPosition.x >= 0f && viewportPosition.x <= 1f
            && viewportPosition.y >= 0f && viewportPosition.y <= 1f;

        MonsterRankVisual rankVisual = monster.GetComponent<MonsterRankVisual>();
        Transform visualRoot = rankVisual != null ? rankVisual.RuntimeVisualRoot : monster.transform.Find("Visual_Slime");
        Renderer renderer = visualRoot != null ? visualRoot.GetComponentInChildren<Renderer>(true) : monster.GetComponentInChildren<Renderer>(true);
        WorldHealthBar healthBar = monster.GetComponent<WorldHealthBar>();
        MonsterIdentity identity = monster.GetComponent<MonsterIdentity>();
        float distanceToPlayer = target != null ? Vector3.Distance(monster.transform.position, target.position) : -1f;

        if (renderer == null)
        {
            Debug.LogWarning("[MonsterTestVisibleDebug] Renderer not found.", monster);
        }

        string sortingLayer = "n/a";
        int sortingOrder = 0;
        string spriteOrMaterial = "n/a";
        if (renderer is SpriteRenderer spriteRenderer)
        {
            sortingLayer = spriteRenderer.sortingLayerName;
            sortingOrder = spriteRenderer.sortingOrder;
            spriteOrMaterial = spriteRenderer.sprite != null ? spriteRenderer.sprite.name : "null-sprite";
        }
        else if (renderer != null)
        {
            Material material = renderer.sharedMaterial;
            spriteOrMaterial = material != null ? material.name : "null-material";
        }

        Debug.Log(
            "[MonsterTestVisibleDebug] " +
            "object=" + monster.name +
            " root activeSelf=" + monster.activeSelf +
            " root activeInHierarchy=" + monster.activeInHierarchy +
            " root position=" + monster.transform.position +
            " distanceToPlayer=" + (distanceToPlayer >= 0f ? distanceToPlayer.ToString("F2") : "n/a") +
            " camera=" + (activeCamera != null ? activeCamera.name : "null") +
            " viewport position=" + viewportPosition +
            " isInCameraView=" + isInCameraView +
            " visualRoot=" + (visualRoot != null ? visualRoot.name : "null") +
            " visualRoot activeSelf=" + (visualRoot != null && visualRoot.gameObject.activeSelf) +
            " visualRoot activeInHierarchy=" + (visualRoot != null && visualRoot.gameObject.activeInHierarchy) +
            " visual localPosition=" + (visualRoot != null ? visualRoot.localPosition.ToString() : "null") +
            " visual worldPosition=" + (visualRoot != null ? visualRoot.position.ToString() : "null") +
            " visual localScale=" + (visualRoot != null ? visualRoot.localScale.ToString() : "null") +
            " renderer=" + (renderer != null ? renderer.GetType().Name : "null") +
            " renderer enabled=" + (renderer != null && renderer.enabled) +
            " renderer bounds=" + (renderer != null ? renderer.bounds.ToString() : "null") +
            " sortingLayer=" + sortingLayer +
            " sortingOrder=" + sortingOrder +
            " sprite/material=" + spriteOrMaterial +
            " healthBar active=" + (healthBar != null && healthBar.gameObject.activeInHierarchy) +
            " rank=" + (identity != null ? identity.rank.ToString() : "Unknown") +
            " species=" + (identity != null ? identity.species.ToString() : "Unknown") +
            " attackStyle=" + (identity != null ? identity.attackStyle.ToString() : "Unknown"),
            monster);

        if (visualRoot != null && visualRoot.localScale.sqrMagnitude <= 0.0001f)
        {
            Debug.LogWarning("[MonsterTestVisibleDebug] visualRoot localScale is near zero: " + visualRoot.localScale, monster);
        }

        if (visualRoot != null && visualRoot.position.y < suspiciousBelowGroundY)
        {
            Debug.LogWarning("[MonsterTestVisibleDebug] visualRoot worldPosition seems below ground: " + visualRoot.position, monster);
        }

        if (activeCamera != null && !isInCameraView)
        {
            Debug.LogWarning("[MonsterTestVisibleDebug] viewport position outside camera view: " + viewportPosition, monster);
        }
    }

    private bool IsSpawnerPrefabSource(GameObject prefab, MonsterRank rank)
    {
        if (enemySpawner == null || prefab == null)
        {
            return false;
        }

        GameObject[] source = rank switch
        {
            MonsterRank.Elite => enemySpawner.eliteEnemyPrefabs,
            MonsterRank.Boss => enemySpawner.bossEnemyPrefabs,
            _ => enemySpawner.normalEnemyPrefabs
        };

        if (source == null)
        {
            return false;
        }

        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == prefab)
            {
                return true;
            }
        }

        return false;
    }

    private void PrepareSharedEnemySpawnerForTest()
    {
        ResolveSharedEnemySpawner();

        if (!isolateTestSpawn || !disableSharedEnemySpawnerDuringTest)
        {
            return;
        }

        if (enemySpawner == null)
        {
            Debug.LogWarning("[MonsterTestSpawner] isolateTestSpawn requested but EnemySpawner reference is missing.", this);
            return;
        }

        enemySpawner.PauseSpawningForExternalTest();
        sharedEnemySpawnerPausedByTest = true;
        enemySpawner.enabled = false;

        Debug.Log(
            "[MonsterTestSpawner] Shared EnemySpawner paused for isolated test spawning. " +
            "used PauseSpawningForExternalTest=true enabled=false",
            this);
    }

    private EnemySpawner ResolveSharedEnemySpawner()
    {
        if (enemySpawner != null)
        {
            return enemySpawner;
        }

        EnemySpawner[] spawners = FindObjectsOfType<EnemySpawner>(true);
        for (int i = 0; i < spawners.Length; i++)
        {
            EnemySpawner candidate = spawners[i];
            if (candidate == null)
            {
                continue;
            }

            enemySpawner = candidate;
            Debug.Log("[MonsterTestSpawner] Resolved scene EnemySpawner for shared Rank Geometry: " + candidate.name, candidate);
            return enemySpawner;
        }

        return null;
    }

    private void ResumeSharedEnemySpawnerAfterTestIfNeeded()
    {
        if (!sharedEnemySpawnerPausedByTest || enemySpawner == null)
        {
            return;
        }

        enemySpawner.enabled = true;
        enemySpawner.ResumeSpawningAfterExternalTest();
        sharedEnemySpawnerPausedByTest = false;

        Debug.Log("[MonsterTestSpawner] Shared EnemySpawner resumed after isolated test spawning.", this);
    }

    private GameObject ResolvePrefabForRank(MonsterRank rank)
    {
        GameObject[] source = rank switch
        {
            MonsterRank.Elite => ResolvePrefabArray(eliteEnemyPrefabs, enemySpawner != null ? enemySpawner.eliteEnemyPrefabs : null),
            MonsterRank.Boss => ResolvePrefabArray(bossEnemyPrefabs, enemySpawner != null ? enemySpawner.bossEnemyPrefabs : null),
            _ => ResolvePrefabArray(normalEnemyPrefabs, enemySpawner != null ? enemySpawner.normalEnemyPrefabs : null)
        };

        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] != null)
            {
                return source[i];
            }
        }

        return null;
    }

    private GameObject ResolveSelectedPrefab(MonsterRank rank, out string selectionSource)
    {
        if (testMonsterPrefab != null)
        {
            selectionSource = "InspectorOverride";
            return testMonsterPrefab;
        }

        selectionSource = "RankFallback";
        return ResolvePrefabForRank(rank);
    }

    private static GameObject[] ResolvePrefabArray(GameObject[] local, GameObject[] fallback)
    {
        if (ContainsUsablePrefab(local))
        {
            return local;
        }

        return fallback ?? System.Array.Empty<GameObject>();
    }

    private static bool ContainsUsablePrefab(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator SpawnSelectedTypeOnStartRoutine()
    {
        if (autoSpawnDelay > 0f)
        {
            Debug.Log("[MonsterTestSpawner] Auto spawn waiting for player...", this);
            yield return new WaitForSeconds(autoSpawnDelay);
        }
        else
        {
            yield return null;
        }

        float elapsed = 0f;
        Transform resolvedPlayer = ResolvePlayerTarget();
        while (resolvedPlayer == null && elapsed < playerResolveTimeout)
        {
            yield return new WaitForSeconds(playerResolveRetryInterval);
            elapsed += playerResolveRetryInterval;
            resolvedPlayer = ResolvePlayerTarget();
        }

        if (resolvedPlayer != null)
        {
            Debug.Log(
                "[MonsterTestSpawner] Player resolved after " + elapsed.ToString("F2") + " seconds: " + resolvedPlayer.name,
                this);
        }
        else if (spawnPoint == null)
        {
            Debug.LogWarning("[MonsterTestSpawner] Auto spawn aborted: player not found and spawnPoint is null.", this);
            autoSpawnRoutine = null;
            yield break;
        }

        autoSpawnRoutine = null;
        SpawnSelectedMonsterType();
    }

    private void StopAutoSpawnRoutineIfRunning()
    {
        if (autoSpawnRoutine == null)
        {
            return;
        }

        StopCoroutine(autoSpawnRoutine);
        autoSpawnRoutine = null;
    }

    private bool TryResolveSpawnPosition(Transform resolvedPlayerTarget, out Vector3 spawnPosition, out string failureReason)
    {
        Vector3 candidatePosition;
        string source;

        if (spawnPoint != null)
        {
            candidatePosition = spawnPoint.position;
            source = "spawnPoint";
            Debug.Log("[MonsterTestSpawner] using spawnPoint = " + candidatePosition, this);
        }
        else if (resolvedPlayerTarget != null && useCameraVisibleSpawnPosition && TryResolveCameraVisibleSpawnPosition(resolvedPlayerTarget, out candidatePosition))
        {
            source = "cameraVisible";
        }
        else if (resolvedPlayerTarget != null)
        {
            Vector3 forward = resolvedPlayerTarget.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.right;
            }

            candidatePosition = resolvedPlayerTarget.position + forward.normalized * Mathf.Max(0f, playerFrontSpawnDistance);
            source = "playerForward";
        }
        else if (allowFallbackToSelfPosition)
        {
            Debug.LogWarning("[MonsterTestSpawner] Player target not found. Spawn position fallback may be invalid.", this);
            candidatePosition = transform.position;
            source = "selfTransform";
        }
        else
        {
            spawnPosition = Vector3.zero;
            failureReason = "Cannot spawn test monster because player target is not ready and spawnPoint is null.";
            return false;
        }

        spawnPosition = FinalizeSpawnPosition(candidatePosition, source);
        if (spawnPosition.y < suspiciousBelowGroundY)
        {
            failureReason = "spawn position below ground, abort spawn. position=" + spawnPosition + " source=" + source;
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private Transform ResolvePlayerTarget()
    {
        if (playerTarget != null)
        {
            lastPlayerResolveFailureReason = "ManualPlayerTarget";
            return playerTarget;
        }

        if (playerBootstrap != null && playerBootstrap.CurrentPlayerTransform != null)
        {
            lastPlayerResolveFailureReason = "PlayerBootstrapCurrentPlayer";
            return playerBootstrap.CurrentPlayerTransform;
        }

        if (enemySpawner != null && enemySpawner.playerTarget != null)
        {
            lastPlayerResolveFailureReason = "EnemySpawnerPlayerTarget";
            return enemySpawner.playerTarget;
        }

        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            try
            {
                GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
                if (taggedPlayer != null && taggedPlayer.activeInHierarchy)
                {
                    lastPlayerResolveFailureReason = "FindGameObjectWithTag";
                    return taggedPlayer.transform;
                }
            }
            catch (UnityException)
            {
                lastPlayerResolveFailureReason = "PlayerTagMissing";
            }
        }

        CombatHealth[] combatHealths = FindObjectsOfType<CombatHealth>(true);
        for (int i = 0; i < combatHealths.Length; i++)
        {
            CombatHealth combatHealth = combatHealths[i];
            if (combatHealth == null || !combatHealth.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(playerTag) && combatHealth.CompareTag(playerTag))
            {
                lastPlayerResolveFailureReason = "CombatHealthWithPlayerTag";
                return combatHealth.transform;
            }
        }

        Player2PrototypeController[] player2Controllers = FindObjectsOfType<Player2PrototypeController>(true);
        for (int i = 0; i < player2Controllers.Length; i++)
        {
            Player2PrototypeController controller = player2Controllers[i];
            if (controller == null || !controller.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(playerTag) || controller.CompareTag(playerTag))
            {
                lastPlayerResolveFailureReason = "Player2PrototypeController";
                return controller.transform;
            }
        }

        Player01SkillController[] player1Controllers = FindObjectsOfType<Player01SkillController>(true);
        for (int i = 0; i < player1Controllers.Length; i++)
        {
            Player01SkillController controller = player1Controllers[i];
            if (controller == null || !controller.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(playerTag) || controller.CompareTag(playerTag))
            {
                lastPlayerResolveFailureReason = "Player01SkillController";
                return controller.transform;
            }
        }

        lastPlayerResolveFailureReason = "NoActivePlayerFound";
        return null;
    }

    private Vector3 FinalizeSpawnPosition(Vector3 candidatePosition, string source)
    {
        Vector3 resolvedPosition = ProjectSpawnPositionToGround(candidatePosition);
        if (resolvedPosition.y < suspiciousBelowGroundY)
        {
            Debug.LogWarning(
                "[MonsterTestSpawner] spawn position seems below ground: " + resolvedPosition + " source=" + source,
                this);
        }

        return resolvedPosition;
    }

    private bool TryResolveCameraVisibleSpawnPosition(Transform resolvedPlayerTarget, out Vector3 candidatePosition)
    {
        Camera activeCamera = Camera.main;
        if (resolvedPlayerTarget == null || activeCamera == null)
        {
            candidatePosition = Vector3.zero;
            return false;
        }

        Vector3 cameraRight = activeCamera.transform.right;
        cameraRight.y = 0f;
        if (cameraRight.sqrMagnitude <= 0.0001f)
        {
            candidatePosition = Vector3.zero;
            return false;
        }

        cameraRight.Normalize();
        Vector3 cameraForward = Vector3.Cross(Vector3.up, cameraRight).normalized;
        float horizontalSign = preferSpawnOnPlayerRight ? 1f : -1f;
        candidatePosition = resolvedPlayerTarget.position
            + cameraRight * cameraVisibleSpawnHorizontalOffset * horizontalSign
            + cameraForward * cameraVisibleSpawnDepthOffset;

        return true;
    }

    private void TryAutoRepositionSpawnedMonsterIntoCameraView(GameObject monster, Transform target)
    {
        if (!autoRepositionIfOutsideCamera || monster == null || target == null)
        {
            return;
        }

        Camera activeCamera = Camera.main;
        if (activeCamera == null)
        {
            return;
        }

        Vector3 viewportPosition = activeCamera.WorldToViewportPoint(monster.transform.position);
        bool isInCameraView =
            viewportPosition.z > 0f &&
            viewportPosition.x >= 0f && viewportPosition.x <= 1f &&
            viewportPosition.y >= 0f && viewportPosition.y <= 1f;

        if (isInCameraView)
        {
            return;
        }

        if (!TryResolveSpawnPosition(target, out Vector3 repositionedSpawnPosition, out string failureReason))
        {
            Debug.LogWarning("[MonsterTestSpawner] auto reposition failed: " + failureReason, monster);
            return;
        }

        monster.transform.position = repositionedSpawnPosition;
        WorldHealthBar healthBar = monster.GetComponent<WorldHealthBar>();
        if (healthBar != null)
        {
            healthBar.RefreshWorldPositionForDebug();
        }
        LogSpawnedMonsterVisibility(monster, target);

        viewportPosition = activeCamera.WorldToViewportPoint(monster.transform.position);
        isInCameraView =
            viewportPosition.z > 0f &&
            viewportPosition.x >= 0f && viewportPosition.x <= 1f &&
            viewportPosition.y >= 0f && viewportPosition.y <= 1f;

        if (!isInCameraView)
        {
            Debug.LogWarning("[MonsterTestSpawner] spawned monster is still outside camera view.", monster);
        }
    }

    private Vector3 ProjectSpawnPositionToGround(Vector3 candidatePosition)
    {
        Vector3 rayOrigin = candidatePosition + Vector3.up * Mathf.Max(0f, spawnGroundRaycastStartHeight);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, Mathf.Max(1f, spawnGroundRaycastDistance), spawnGroundLayerMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return candidatePosition;
    }

    private void ApplyFallbackOfficialConfig(GameObject monster, MonsterRank rank)
    {
        if (monster == null)
        {
            return;
        }

        if (ShouldPreserveNormalPrefabGeometry(monster, rank))
        {
            LogNormalPrefabGeometry(monster, "MonsterTestSpawnerFallback", "FallbackRankGeometrySkipped", rankGeometryExecuted: false, groundContactExecuted: false, visualTransformWriteExecuted: false);
            return;
        }

        Debug.Log(
            "[MonsterTestSpawner] using fallback rank geometry because shared EnemySpawner is null.",
            monster);

        ApplyFallbackRankGeometry(monster, rank);

        WorldHealthBar healthBar = monster.GetComponent<WorldHealthBar>();
        if (healthBar != null)
        {
            MonsterRankGeometrySettings normalGeometry = ResolveFallbackRankGeometry(MonsterRank.Normal);
            MonsterRankGeometrySettings eliteGeometry = ResolveFallbackRankGeometry(MonsterRank.Elite);
            MonsterRankGeometrySettings bossGeometry = ResolveFallbackRankGeometry(MonsterRank.Boss);
            healthBar.ApplyHealthBarConfig(
                normalGeometry.healthBarOffsetY,
                eliteGeometry.healthBarOffsetY,
                bossGeometry.healthBarOffsetY,
                true,
                "MonsterTestSpawnerFallback");
        }
    }

    private void ApplyFallbackPlayerRuneMonsterScaling(GameObject monster, Transform target)
    {
        if (!enablePlayerRuneStrengthScaling || monster == null)
        {
            return;
        }

        CombatStats stats = monster.GetComponent<CombatStats>();
        if (stats == null)
        {
            return;
        }

        int runeCount = EnemySpawner.ResolveEquippedRuneCountForMonsterScaling(target, out string playerName, out string countSource);
        float strengthMultiplier = EnemySpawner.CalculateRuneStrengthMultiplier(runeCount, strengthIncreasePerEquippedRune);
        float movementMultiplier = Mathf.Min(strengthMultiplier, Mathf.Max(1f, maximumRuneMovementSpeedMultiplier));

        float baseMaxHealth = stats.maxHealth;
        float basePhysicalAttack = stats.physicalAttack;
        float baseSpecialAttack = stats.specialAttack;
        float basePhysicalDefense = stats.physicalDefense;
        float baseSpecialDefense = stats.specialDefense;
        float baseMovementSpeed = stats.speed;

        stats.maxHealth = Mathf.Max(1f, Mathf.Round(baseMaxHealth * strengthMultiplier));
        stats.physicalAttack = Mathf.Max(0f, Mathf.Round(basePhysicalAttack * strengthMultiplier));
        stats.specialAttack = Mathf.Max(0f, Mathf.Round(baseSpecialAttack * strengthMultiplier));
        stats.physicalDefense = Mathf.Max(0f, Mathf.Round(basePhysicalDefense * strengthMultiplier));
        stats.specialDefense = Mathf.Max(0f, Mathf.Round(baseSpecialDefense * strengthMultiplier));
        stats.speed = Mathf.Max(0.1f, RoundToDecimals(baseMovementSpeed * movementMultiplier, 2));

        BattleResourceBank resourceBank = monster.GetComponent<BattleResourceBank>();
        if (resourceBank != null)
        {
            resourceBank.maxHealth = stats.maxHealth;
            resourceBank.currentHealth = stats.maxHealth;
        }

        CombatHealth combatHealth = monster.GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            combatHealth.stats = stats;
            combatHealth.resourceBank = resourceBank;
            combatHealth.currentHealth = stats.maxHealth;
        }

        ConfigureFallbackEnemyController(monster, stats);

        if (debugPlayerRuneMonsterScaling)
        {
            Debug.Log(
                "[MonsterRuneScalingTrace] " +
                "event=ScalingApplied " +
                $"enemy={monster.name} " +
                $"enemyInstanceId={monster.GetInstanceID()} " +
                $"player={playerName} " +
                $"countSource={countSource} " +
                $"equippedRuneCount={runeCount} " +
                $"strengthPerRune={Mathf.Max(0f, strengthIncreasePerEquippedRune):F2} " +
                $"strengthMultiplier={strengthMultiplier:F2} " +
                $"movementMultiplier={movementMultiplier:F2} " +
                $"baseMaxHealth={baseMaxHealth:F1} " +
                $"scaledMaxHealth={stats.maxHealth:F1} " +
                $"baseDamage={Mathf.Max(basePhysicalAttack, baseSpecialAttack):F1} " +
                $"scaledDamage={Mathf.Max(stats.physicalAttack, stats.specialAttack):F1} " +
                $"basePhysicalDefense={basePhysicalDefense:F1} " +
                $"scaledPhysicalDefense={stats.physicalDefense:F1} " +
                $"baseSpecialDefense={baseSpecialDefense:F1} " +
                $"scaledSpecialDefense={stats.specialDefense:F1} " +
                $"baseMovementSpeed={baseMovementSpeed:F2} " +
                $"scaledMovementSpeed={stats.speed:F2}",
                monster);
        }
    }

    private static void ConfigureFallbackEnemyController(GameObject monster, CombatStats stats)
    {
        if (monster == null || stats == null)
        {
            return;
        }

        EnemyController controller = monster.GetComponent<EnemyController>();
        MonsterIdentity identity = monster.GetComponent<MonsterIdentity>();
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

        BattleDamageType damageType = identity.attackStyle == MonsterAttackStyle.Melee ? BattleDamageType.Physical : BattleDamageType.Special;
        float attackPower = damageType == BattleDamageType.Physical ? stats.physicalAttack : stats.specialAttack;
        controller.ConfigureRuntime(
            Mathf.Max(0.1f, stats.speed),
            0.8f,
            range,
            hitRange,
            cooldown,
            attackPower,
            identity.attackStyle);
    }

    private static float RoundToDecimals(float value, int decimals)
    {
        float multiplier = Mathf.Pow(10f, Mathf.Max(0, decimals));
        return Mathf.Round(value * multiplier) / multiplier;
    }

    private static void ApplyFallbackRankGeometry(GameObject monster, MonsterRank rank)
    {
        if (monster == null)
        {
            return;
        }

        if (ShouldPreserveNormalPrefabGeometry(monster, rank))
        {
            LogNormalPrefabGeometry(monster, "MonsterTestSpawnerFallback", "ApplyFallbackRankGeometrySkipped", rankGeometryExecuted: false, groundContactExecuted: false, visualTransformWriteExecuted: false);
            return;
        }

        MonsterRankGeometrySettings geometry = ResolveFallbackRankGeometry(rank);
        monster.transform.localScale = Vector3.one;

        Transform visualRoot = ResolveFallbackRankVisualRoot(monster);
        if (visualRoot != null)
        {
            Vector3 prefabBaseVisualScale = visualRoot.localScale;
            Vector3 expectedFinalVisualScale = ResolveFallbackFinalVisualScale(rank, prefabBaseVisualScale, geometry.visualScale);
            LogEliteScaleTrace(monster, "MonsterTestSpawnerFallback", "ApplyFallbackRankGeometryBefore", prefabBaseVisualScale, geometry.visualScale, expectedFinalVisualScale, visualRoot, null);

            visualRoot.localScale = expectedFinalVisualScale;
            visualRoot.localPosition = geometry.visualLocalPosition;

            SlimeAnimationController slimeAnimationController = monster.GetComponent<SlimeAnimationController>();
            if (slimeAnimationController != null)
            {
                slimeAnimationController.SetVisualBaseScale(visualRoot.localScale);
                slimeAnimationController.SetVisualBasePosition(visualRoot.localPosition);
            }

            LogEliteScaleTrace(monster, "MonsterTestSpawnerFallback", "ApplyFallbackRankGeometryAfter", prefabBaseVisualScale, geometry.visualScale, expectedFinalVisualScale, visualRoot, slimeAnimationController);
        }

        Transform groundContact = EnsureFallbackGroundContact(monster);
        if (groundContact != null)
        {
            groundContact.localPosition = geometry.groundContactLocalPosition;
            groundContact.localRotation = Quaternion.identity;
            groundContact.localScale = Vector3.one;
        }

        SphereCollider physicalCollider = EnsureFallbackPhysicalSphereCollider(monster);
        if (physicalCollider != null)
        {
            physicalCollider.isTrigger = false;
            physicalCollider.center = geometry.physicalColliderCenter;
            physicalCollider.radius = Mathf.Max(0.01f, geometry.physicalColliderRadius);
        }

        BoxCollider hurtbox = EnsureFallbackRankHurtbox(monster, rank);
        if (hurtbox != null)
        {
            hurtbox.isTrigger = true;
            hurtbox.center = geometry.hurtboxCenter;
            hurtbox.size = AbsVector3(geometry.hurtboxSize);
            hurtbox.gameObject.layer = monster.layer;
            hurtbox.gameObject.tag = monster.tag;
        }

        Debug.Log(
            "[RankGeometry] " +
            "sourceEnemySpawner=None" +
            " fallbackUsed=true " +
            "rank=" + rank +
            " source=MonsterTestSpawnerFallback" +
            " rootScale=" + monster.transform.localScale +
            " visualScale=" + (visualRoot != null ? visualRoot.localScale.ToString() : "null") +
            " visualLocalPosition=" + (visualRoot != null ? visualRoot.localPosition.ToString() : "null") +
            " visualLocalPositionConfigured=" + geometry.visualLocalPosition +
            " visualLocalPositionApplied=" + (visualRoot != null ? visualRoot.localPosition.ToString() : "null") +
            " finalVisualLocalPosition=" + (visualRoot != null ? visualRoot.localPosition.ToString() : "null") +
            " groundContactLocalPosition=" + (groundContact != null ? groundContact.localPosition.ToString() : "null") +
            " physicalColliderCenter=" + (physicalCollider != null ? physicalCollider.center.ToString() : "null") +
            " physicalColliderRadius=" + (physicalCollider != null ? physicalCollider.radius.ToString("F3") : "n/a") +
            " hurtboxCenter=" + (hurtbox != null ? hurtbox.center.ToString() : "null") +
            " hurtboxSize=" + (hurtbox != null ? hurtbox.size.ToString() : "null") +
            " applyCount=1",
            monster);
    }

    private static MonsterRankGeometrySettings ResolveFallbackRankGeometry(MonsterRank rank)
    {
        return rank switch
        {
            MonsterRank.Boss => FallbackBossGeometry,
            MonsterRank.Elite => FallbackEliteGeometry,
            _ => FallbackNormalGeometry
        };
    }

    private static Vector3 ResolveFallbackFinalVisualScale(MonsterRank rank, Vector3 prefabBaseVisualScale, Vector3 configuredVisualScale)
    {
        if (rank == MonsterRank.Elite)
        {
            return Vector3.Scale(prefabBaseVisualScale, SanitizeScale(configuredVisualScale));
        }

        return configuredVisualScale;
    }

    private static Vector3 SanitizeScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
    }

    private static void LogEliteScaleTrace(
        GameObject monster,
        string source,
        string phase,
        Vector3 prefabBaseVisualScale,
        Vector3 eliteMultiplier,
        Vector3 expectedFinalScale,
        Transform visualRoot,
        SlimeAnimationController slimeAnimationController)
    {
        if (monster == null)
        {
            return;
        }

        MonsterIdentity identity = monster.GetComponent<MonsterIdentity>();
        if (identity == null || identity.rank != MonsterRank.Elite)
        {
            return;
        }

        Debug.Log(
            "[EliteScaleTrace] " +
            "object=" + monster.name +
            " spawnSource=" + source +
            " phase=" + phase +
            " prefabBaseVisualScale=" + prefabBaseVisualScale +
            " eliteMultiplier=" + eliteMultiplier +
            " expectedFinalScale=" + expectedFinalScale +
            " actualFinalScale=" + (visualRoot != null ? visualRoot.localScale.ToString() : "null") +
            " visualPosition=" + (visualRoot != null ? visualRoot.localPosition.ToString() : "null") +
            " animationBaseScale=" + (slimeAnimationController != null ? slimeAnimationController.BaseVisualLocalScale.ToString() : "null"),
            monster);
    }

    private static Transform ResolveFallbackRankVisualRoot(GameObject monster)
    {
        Transform visualRoot = monster.transform.Find("Visual_Slime");
        if (visualRoot != null)
        {
            return visualRoot;
        }

        SlimeAnimationController slimeAnimation = monster.GetComponent<SlimeAnimationController>();
        if (slimeAnimation != null && slimeAnimation.VisualRoot != null && slimeAnimation.VisualRoot != monster.transform)
        {
            return slimeAnimation.VisualRoot;
        }

        MonsterRankVisual rankVisual = monster.GetComponent<MonsterRankVisual>();
        return rankVisual != null ? rankVisual.RuntimeVisualRoot : null;
    }

    private static Transform EnsureFallbackGroundContact(GameObject monster)
    {
        Transform root = monster.transform;
        Transform groundContact = root.Find("GroundContact");
        if (groundContact == null)
        {
            Transform legacyVisualGroundContact = root.Find("Visual_Slime/GroundContact");
            if (legacyVisualGroundContact != null)
            {
                groundContact = legacyVisualGroundContact;
                groundContact.SetParent(root, false);
            }
        }

        if (groundContact == null)
        {
            GameObject contactObject = new GameObject("GroundContact");
            groundContact = contactObject.transform;
            groundContact.SetParent(root, false);
        }

        return groundContact;
    }

    private static SphereCollider EnsureFallbackPhysicalSphereCollider(GameObject monster)
    {
        SphereCollider[] spheres = monster.GetComponents<SphereCollider>();
        for (int i = 0; i < spheres.Length; i++)
        {
            SphereCollider sphere = spheres[i];
            if (sphere != null && !sphere.isTrigger)
            {
                return sphere;
            }
        }

        SphereCollider created = monster.AddComponent<SphereCollider>();
        created.isTrigger = false;
        return created;
    }

    private static BoxCollider EnsureFallbackRankHurtbox(GameObject monster, MonsterRank rank)
    {
        string hurtboxName = rank == MonsterRank.Boss ? "BossScaledHurtbox" : "RankHurtbox";
        Transform hurtboxRoot = monster.transform.Find(hurtboxName);
        if (hurtboxRoot == null)
        {
            GameObject hurtboxObject = new GameObject(hurtboxName);
            hurtboxRoot = hurtboxObject.transform;
            hurtboxRoot.SetParent(monster.transform, false);
        }

        hurtboxRoot.localPosition = Vector3.zero;
        hurtboxRoot.localRotation = Quaternion.identity;
        hurtboxRoot.localScale = Vector3.one;
        hurtboxRoot.gameObject.SetActive(true);

        RemoveColliderComponentsExcept<BoxCollider>(hurtboxRoot);
        BoxCollider hurtbox = hurtboxRoot.GetComponent<BoxCollider>();
        if (hurtbox == null)
        {
            hurtbox = hurtboxRoot.gameObject.AddComponent<BoxCollider>();
        }

        return hurtbox;
    }

    private Collider ResolveBossPrimaryBodyCollider(GameObject monster)
    {
        Collider[] colliders = monster.GetComponentsInChildren<Collider>(true);
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
            if (collider.transform == monster.transform)
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

    private static Vector3 AbsVector3(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
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

    private static Transform ResolveBossVisualTransform(GameObject monster)
    {
        SlimeAnimationController slimeAnimation = monster.GetComponent<SlimeAnimationController>();
        if (slimeAnimation != null && slimeAnimation.VisualRoot != null)
        {
            return slimeAnimation.VisualRoot;
        }

        Transform namedVisual = monster.transform.Find("Visual_Slime");
        if (namedVisual != null)
        {
            return namedVisual;
        }

        SpriteRenderer spriteRenderer = monster.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null)
        {
            return spriteRenderer.transform;
        }

        Renderer renderer = monster.GetComponentInChildren<Renderer>(true);
        return renderer != null ? renderer.transform : monster.transform;
    }

    private static Renderer ResolveBossVisualRenderer(Transform visualTransform)
    {
        if (visualTransform == null)
        {
            return null;
        }

        Renderer directRenderer = visualTransform.GetComponent<Renderer>();
        if (directRenderer != null)
        {
            return directRenderer;
        }

        return visualTransform.GetComponentInChildren<Renderer>(true);
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

    private void CleanupTrackedMonsters()
    {
        for (int i = spawnedTestMonsters.Count - 1; i >= 0; i--)
        {
            GameObject monster = spawnedTestMonsters[i];
            if (monster == null)
            {
                spawnedTestMonsters.RemoveAt(i);
            }
        }
    }

    private void RefreshSpawnedBossHurtboxesIfSharedConfigChanged()
    {
        if (enemySpawner == null || Time.unscaledTime < nextBossHurtboxRuntimeRefreshTime)
        {
            return;
        }

        nextBossHurtboxRuntimeRefreshTime = Time.unscaledTime + 0.25f;

        int configHash = enemySpawner.GetConfiguredBossHurtboxConfigHash();
        if (configHash == lastSharedBossHurtboxConfigHash)
        {
            return;
        }

        lastSharedBossHurtboxConfigHash = configHash;
        CleanupTrackedMonsters();

        for (int i = 0; i < spawnedTestMonsters.Count; i++)
        {
            GameObject monster = spawnedTestMonsters[i];
            MonsterIdentity identity = monster != null ? monster.GetComponent<MonsterIdentity>() : null;
            if (identity == null || identity.rank != MonsterRank.Boss)
            {
                continue;
            }

            enemySpawner.RefreshBossHurtboxForRuntime(monster, "MonsterTestSpawnerRuntimeRefresh");
        }
    }

    private float ResolveConfiguredVisualScaleMultiplier(MonsterRank rank)
    {
        if (enemySpawner != null)
        {
            return enemySpawner.GetConfiguredVisualScaleMultiplier(rank);
        }

        Vector3 visualScale = ResolveFallbackRankGeometry(rank).visualScale;
        return Mathf.Max(Mathf.Abs(visualScale.x), Mathf.Max(Mathf.Abs(visualScale.y), Mathf.Abs(visualScale.z)));
    }

    private float ResolveConfiguredVisualGroundOffsetY(MonsterRank rank)
    {
        if (enemySpawner != null)
        {
            return enemySpawner.GetConfiguredVisualGroundOffsetY(rank);
        }

        return ResolveFallbackRankGeometry(rank).visualLocalPosition.y;
    }

    private string BuildMonsterStateSummary(GameObject monster, Transform target)
    {
        MonsterIdentity identity = monster != null ? monster.GetComponent<MonsterIdentity>() : null;
        EnemyController controller = monster != null ? monster.GetComponent<EnemyController>() : null;
        CombatHealth health = monster != null ? monster.GetComponent<CombatHealth>() : null;
        Collider bodyCollider = monster != null ? ResolveBossPrimaryBodyCollider(monster) : null;

        string runtimeSummary = controller != null
            ? controller.BuildRuntimeDebugSummary(target)
            : "[MonsterDebug] enemyControllerExists=false";

        return
            "[MonsterTestState] " +
            "name=" + (monster != null ? monster.name : "null") +
            " rootPosition=" + (monster != null ? monster.transform.position.ToString() : "null") +
            " rank=" + (identity != null ? identity.rank.ToString() : "Unknown") +
            " species=" + (identity != null ? identity.species.ToString() : "Unknown") +
            " attackStyle=" + (identity != null ? identity.attackStyle.ToString() : "Unknown") +
            " health=" + (health != null ? health.currentHealth.ToString("F1") + "/" + health.MaxHealthValue.ToString("F1") : "n/a") +
            " collider=" + (bodyCollider != null ? bodyCollider.GetType().Name : "None") +
            " colliderBounds=" + (bodyCollider != null ? bodyCollider.bounds.ToString() : "None") +
            " " + runtimeSummary;
    }

    private static string AppendTestSuffix(string baseName, MonsterRank rank)
    {
        string suffix = rank switch
        {
            MonsterRank.Elite => EliteSuffix,
            MonsterRank.Boss => BossSuffix,
            _ => NormalSuffix
        };

        return baseName.Contains(suffix) ? baseName : baseName + suffix;
    }

    private static bool ShouldPreserveNormalPrefabGeometry(GameObject monster, MonsterRank rank)
    {
        if (monster == null || rank != MonsterRank.Normal)
        {
            return false;
        }

        MonsterIdentity identity = monster.GetComponent<MonsterIdentity>();
        if (identity != null)
        {
            return IsSlimeSpecies(identity.species);
        }

        return monster.name.StartsWith("Enemy_Slime");
    }

    private static bool IsSlimeSpecies(MonsterSpecies species)
    {
        return species == MonsterSpecies.BlueSlime ||
               species == MonsterSpecies.GreenSlime ||
               species == MonsterSpecies.LavaSlime ||
               species == MonsterSpecies.PoisonSlime ||
               species == MonsterSpecies.RainbowSlime;
    }

    private static void LogNormalPrefabGeometry(
        GameObject monster,
        string source,
        string phase,
        bool rankGeometryExecuted,
        bool groundContactExecuted,
        bool visualTransformWriteExecuted)
    {
        if (monster == null)
        {
            return;
        }

        MonsterIdentity identity = monster.GetComponent<MonsterIdentity>();
        MonsterRank rank = identity != null ? identity.rank : MonsterRank.Normal;
        if (!ShouldPreserveNormalPrefabGeometry(monster, rank))
        {
            return;
        }

        Transform visual = ResolveNormalVisualTransform(monster);
        Debug.Log(
            "[NormalPrefabGeometry] " +
            "object=" + monster.name +
            " spawnSource=" + source +
            " phase=" + phase +
            " rank=" + rank +
            " species=" + (identity != null ? identity.species.ToString() : "Unknown") +
            " visualTransform=" + (visual != null ? visual.name : "null") +
            " rootPosition=" + monster.transform.position +
            " rootRotation=" + monster.transform.localRotation.eulerAngles +
            " rootScale=" + monster.transform.localScale +
            " visualLocalPosition=" + (visual != null ? visual.localPosition.ToString() : "null") +
            " visualLocalRotation=" + (visual != null ? visual.localRotation.eulerAngles.ToString() : "null") +
            " visualLocalScale=" + (visual != null ? visual.localScale.ToString() : "null") +
            " rankGeometryExecuted=" + rankGeometryExecuted +
            " groundContactExecuted=" + groundContactExecuted +
            " visualTransformWriteExecuted=" + visualTransformWriteExecuted,
            monster);
    }

    private IEnumerator LogNormalPrefabGeometryAfterFirstFrame(GameObject monster, string source)
    {
        if (monster == null)
        {
            yield break;
        }

        MonsterIdentity identity = monster.GetComponent<MonsterIdentity>();
        MonsterRank rank = identity != null ? identity.rank : MonsterRank.Normal;
        if (!ShouldPreserveNormalPrefabGeometry(monster, rank))
        {
            yield break;
        }

        yield return null;
        LogNormalPrefabGeometry(monster, source, "AfterFirstFrame", rankGeometryExecuted: false, groundContactExecuted: false, visualTransformWriteExecuted: false);
    }

    private static Transform ResolveNormalVisualTransform(GameObject monster)
    {
        if (monster == null)
        {
            return null;
        }

        Transform visual = monster.transform.Find("Visual_Slime");
        if (visual != null)
        {
            return visual;
        }

        SlimeAnimationController slimeAnimation = monster.GetComponent<SlimeAnimationController>();
        if (slimeAnimation != null && slimeAnimation.VisualRoot != null && slimeAnimation.VisualRoot != monster.transform)
        {
            return slimeAnimation.VisualRoot;
        }

        Renderer renderer = monster.GetComponentInChildren<Renderer>(true);
        return renderer != null && renderer.transform != monster.transform ? renderer.transform : null;
    }
}
