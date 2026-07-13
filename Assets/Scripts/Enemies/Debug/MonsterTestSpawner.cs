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

    private const string NormalSuffix = "[MonsterTest_Normal]";
    private const string EliteSuffix = "[MonsterTest_Elite]";
    private const string BossSuffix = "[MonsterTest_Boss]";
    private const bool FallbackEnableNormalVisualGroundOffset = true;
    private const float FallbackNormalVisualGroundOffsetY = -0.05f;
    private const float FallbackNormalHealthBarOffsetY = 0.25f;
    private const bool FallbackEnableEliteVisualScale = true;
    private const float FallbackEliteVisualScaleMultiplier = 1.5f;
    private const bool FallbackEnableEliteVisualGroundOffset = true;
    private const float FallbackEliteVisualGroundOffsetY = 0.25f;
    private const float FallbackEliteHealthBarOffsetY = 0.3f;
    private const bool FallbackDebugEliteVisualConfig = true;
    private const bool FallbackEnableBossVisualScale = true;
    private const float FallbackBossVisualScaleMultiplier = 4.0f;
    private const bool FallbackEnableBossVisualGroundOffset = true;
    private const float FallbackBossVisualGroundOffsetY = 0.85f;
    private const float FallbackBossHealthBarOffsetY = 0.45f;
    private const bool FallbackDebugBossVisualGroundOffset = true;
    private const bool FallbackEnableBossHurtboxScale = true;
    private const bool FallbackUseBossVisualBoundsHurtbox = true;
    private const float FallbackBossHurtboxRadiusMultiplier = 3.0f;
    private const float FallbackBossHurtboxCenterYOffset = 0f;
    private const float FallbackBossHurtboxSizeMultiplier = 1f;
    private static readonly Vector3 FallbackBossHurtboxExtraPadding = Vector3.zero;
    private static readonly Vector3 FallbackBossHurtboxCenterOffset = Vector3.zero;
    private const float FallbackBossHurtboxMinimumDepth = 0.2f;
    private const bool FallbackDebugBossHurtbox = true;
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

        GameObject prefab = ResolvePrefabForRank(rank);
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
        }
        else
        {
            if (runtimeSpecies.HasValue)
            {
                identity.species = runtimeSpecies.Value;
            }

            identity.rank = rank;
            MonsterCombatAutoSetup.Configure(spawnedMonster, runtimeSpecies, rank);
            ApplyFallbackOfficialConfig(spawnedMonster, rank);
        }

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
            "spawn position=" + spawnPosition +
            " visual config source=" + visualConfigSource +
            " visual scale=" + configuredVisualScale.ToString("F2") +
            " visual offset=" + configuredVisualOffset.ToString("F2") +
            " note=visual config does not control spawn position",
            spawnedMonster);

        Debug.Log(
            "[MonsterTestSpawner] Spawned " +
            "spawn type=" + ResolveSpawnTypeLabel(rank) +
            " rank=" + rank +
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

        Debug.Log(
            "[MonsterTestSpawner] using fallback local visual config because shared EnemySpawner is null.",
            monster);

        MonsterRankVisual rankVisual = monster.GetComponent<MonsterRankVisual>();
        if (rankVisual != null)
        {
            switch (rank)
            {
                case MonsterRank.Boss:
                    rankVisual.ApplyBossVisualConfig(
                        FallbackEnableBossVisualScale,
                        FallbackBossVisualScaleMultiplier,
                        FallbackEnableBossVisualGroundOffset,
                        FallbackBossVisualGroundOffsetY,
                        FallbackDebugBossVisualGroundOffset,
                        "MonsterTestSpawnerFallback");
                    break;
                case MonsterRank.Elite:
                    rankVisual.ApplyEliteVisualConfig(
                        FallbackEnableEliteVisualScale,
                        FallbackEliteVisualScaleMultiplier,
                        FallbackEnableEliteVisualGroundOffset,
                        FallbackEliteVisualGroundOffsetY,
                        FallbackDebugEliteVisualConfig,
                        "MonsterTestSpawnerFallback");
                    break;
                default:
                    rankVisual.ApplyNormalVisualConfig(
                        FallbackEnableNormalVisualGroundOffset,
                        FallbackNormalVisualGroundOffsetY,
                        "MonsterTestSpawnerFallback");
                    break;
            }
        }

        WorldHealthBar healthBar = monster.GetComponent<WorldHealthBar>();
        if (healthBar != null)
        {
            healthBar.ApplyHealthBarConfig(
                FallbackNormalHealthBarOffsetY,
                FallbackEliteHealthBarOffsetY,
                FallbackBossHealthBarOffsetY,
                true,
                "MonsterTestSpawnerFallback");
        }

        if (rank == MonsterRank.Boss)
        {
            ApplyBossHurtboxFallbackConfig(monster);
        }
    }

    private void ApplyBossHurtboxFallbackConfig(GameObject monster)
    {
        if (monster == null || !FallbackEnableBossHurtboxScale)
        {
            return;
        }

        Collider mainCollider = ResolveBossPrimaryBodyCollider(monster);
        if (mainCollider == null)
        {
            Debug.LogWarning("[BossHurtboxDebug] missing main body collider for test boss hurtbox scaling.", monster);
            return;
        }

        Collider hurtboxCollider = EnsureBossScaledHurtbox(
            monster,
            mainCollider,
            FallbackBossHurtboxRadiusMultiplier,
            FallbackBossHurtboxCenterYOffset,
            FallbackUseBossVisualBoundsHurtbox,
            FallbackBossHurtboxSizeMultiplier,
            FallbackBossHurtboxExtraPadding,
            FallbackBossHurtboxCenterOffset,
            FallbackBossHurtboxMinimumDepth);

        if (hurtboxCollider == null || !FallbackDebugBossHurtbox)
        {
            return;
        }

        MonsterIdentity identity = monster.GetComponent<MonsterIdentity>();
        Transform visualTransform = ResolveBossVisualTransform(monster);
        Renderer visualRenderer = ResolveBossVisualRenderer(visualTransform);

        Debug.Log(
            "[BossHurtboxDebug] " +
            "object=" + monster.name +
            " rank=" + (identity != null ? identity.rank.ToString() : "Unknown") +
            " visualScale=" + (visualTransform != null ? visualTransform.localScale.ToString() : "null") +
            " visualBounds=" + (visualRenderer != null ? visualRenderer.bounds.ToString() : "null") +
            " mainCollider=" + mainCollider.name +
            " mainColliderBounds=" + mainCollider.bounds +
            " hurtboxCollider=" + hurtboxCollider.name +
            " hurtboxBounds=" + hurtboxCollider.bounds +
            " useVisualBoundsHurtbox=" + FallbackUseBossVisualBoundsHurtbox +
            " sizeMultiplier=" + FallbackBossHurtboxSizeMultiplier +
            " extraPadding=" + FallbackBossHurtboxExtraPadding +
            " centerOffset=" + FallbackBossHurtboxCenterOffset +
            " minimumDepth=" + FallbackBossHurtboxMinimumDepth +
            " source=MonsterTestSpawnerFallback",
            monster);
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

    private Collider EnsureBossScaledHurtbox(
        GameObject monster,
        Collider sourceCollider,
        float radiusMultiplierValue,
        float centerYOffset,
        bool useVisualBounds,
        float sizeMultiplierValue,
        Vector3 extraPadding,
        Vector3 centerOffset,
        float minimumDepth)
    {
        Transform hurtboxRoot = monster.transform.Find("BossScaledHurtbox");
        if (hurtboxRoot == null)
        {
            GameObject hurtboxObject = new GameObject("BossScaledHurtbox");
            hurtboxRoot = hurtboxObject.transform;
            hurtboxRoot.SetParent(monster.transform, false);
        }

        hurtboxRoot.gameObject.layer = monster.layer;
        hurtboxRoot.gameObject.tag = monster.tag;
        hurtboxRoot.localPosition = Vector3.zero;
        hurtboxRoot.localRotation = Quaternion.identity;
        hurtboxRoot.localScale = Vector3.one;

        Renderer visualRenderer = useVisualBounds
            ? ResolveBossVisualRenderer(ResolveBossVisualTransform(monster))
            : null;
        if (visualRenderer != null)
        {
            RemoveColliderComponentsExcept<BoxCollider>(hurtboxRoot);
            BoxCollider visualBoundsHurtbox = hurtboxRoot.GetComponent<BoxCollider>();
            if (visualBoundsHurtbox == null)
            {
                visualBoundsHurtbox = hurtboxRoot.gameObject.AddComponent<BoxCollider>();
            }

            ConfigureBossVisualBoundsHurtbox(
                monster.transform,
                visualRenderer.bounds,
                visualBoundsHurtbox,
                radiusMultiplierValue,
                centerYOffset,
                sizeMultiplierValue,
                extraPadding,
                centerOffset,
                minimumDepth);
            return visualBoundsHurtbox;
        }

        float radiusMultiplier = Mathf.Max(1f, radiusMultiplierValue) * Mathf.Max(0.1f, sizeMultiplierValue);
        Vector3 colliderCenterOffset = centerOffset + Vector3.up * centerYOffset;
        Vector3 colliderPadding = AbsVector3(extraPadding);

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

    private static void ConfigureBossVisualBoundsHurtbox(
        Transform monsterRoot,
        Bounds visualBounds,
        BoxCollider hurtbox,
        float radiusMultiplierValue,
        float centerYOffset,
        float sizeMultiplierValue,
        Vector3 extraPadding,
        Vector3 centerOffset,
        float minimumDepth)
    {
        if (monsterRoot == null || hurtbox == null)
        {
            return;
        }

        Vector3 localMin = monsterRoot.InverseTransformPoint(visualBounds.min);
        Vector3 localMax = monsterRoot.InverseTransformPoint(visualBounds.max);
        Vector3 localCenter = (localMin + localMax) * 0.5f;
        Vector3 localSize = new Vector3(
            Mathf.Abs(localMax.x - localMin.x),
            Mathf.Abs(localMax.y - localMin.y),
            Mathf.Abs(localMax.z - localMin.z));

        float minDepth = Mathf.Max(minimumDepth, Mathf.Max(localSize.x, localSize.y) * 0.35f);
        if (localSize.z < minDepth)
        {
            localSize.z = minDepth;
        }

        float sizeMultiplier = Mathf.Max(0.1f, sizeMultiplierValue);
        Vector3 legacyPadding = localSize * Mathf.Max(0f, radiusMultiplierValue - 1f) * 0.05f;
        Vector3 customPadding = AbsVector3(extraPadding);
        localCenter += centerOffset + Vector3.up * centerYOffset;
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

        return rank switch
        {
            MonsterRank.Boss => FallbackBossVisualScaleMultiplier,
            MonsterRank.Elite => FallbackEliteVisualScaleMultiplier,
            _ => 1f
        };
    }

    private float ResolveConfiguredVisualGroundOffsetY(MonsterRank rank)
    {
        if (enemySpawner != null)
        {
            return enemySpawner.GetConfiguredVisualGroundOffsetY(rank);
        }

        return rank switch
        {
            MonsterRank.Boss => FallbackBossVisualGroundOffsetY,
            MonsterRank.Elite => FallbackEliteVisualGroundOffsetY,
            _ => FallbackNormalVisualGroundOffsetY
        };
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
}
