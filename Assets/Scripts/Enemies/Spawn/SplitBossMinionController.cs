using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SplitBossMinionController : MonoBehaviour
{
    [SerializeField] private bool enableEliteSummons = true;
    [SerializeField, Range(0.01f, 1f)] private float firstEliteThreshold = 0.60f;
    [SerializeField, Range(0.01f, 1f)] private float secondEliteThreshold = 0.30f;
    [SerializeField, Min(1)] private int minimumEliteCount = 1;
    [SerializeField, Min(1)] private int maximumEliteCount = 4;
    [SerializeField, Min(0f)] private float summonRadius = 2.5f;
    [SerializeField] private GameObject[] elitePrefabOverrides;
    [SerializeField] private bool suppressRuneDrop = true;
    [SerializeField] private bool debugSplitBossSummon = true;

    private BossSlimeFinalSplitController owner;
    private EnemySpawner spawner;
    private CombatHealth combatHealth;
    private MonsterIdentity monsterIdentity;
    private bool initialized;
    private bool subscribed;
    private bool firstEliteSummonTriggered;
    private bool secondEliteSummonTriggered;
    private int deferredSecondThresholdEligibleFrame = -1;
    private MonsterSpecies preferredSpecies = MonsterSpecies.BlueSlime;
    private string spawnSource = "Unknown";
    private bool autoInitializeAttempted;

    public bool IsInitialized => initialized;

    public void Initialize(
        BossSlimeFinalSplitController owner,
        EnemySpawner ownerSpawner,
        MonsterSpecies preferredSpecies,
        GameObject[] elitePrefabOverrides,
        float firstEliteThreshold,
        float secondEliteThreshold,
        int minEliteCount,
        int maxEliteCount,
        float summonRadius,
        bool suppressRuneDrop,
        bool debugLogs)
    {
        InitializeInternal(
            owner,
            ownerSpawner,
            preferredSpecies,
            elitePrefabOverrides,
            firstEliteThreshold,
            secondEliteThreshold,
            minEliteCount,
            maxEliteCount,
            summonRadius,
            suppressRuneDrop,
            debugLogs,
            owner != null ? "FinalBossSplitTracked" : "StandaloneNormalBoss");
    }

    public void TryAutoInitializeFromScene(string source = "AutoSceneResolve")
    {
        if (initialized || autoInitializeAttempted)
        {
            return;
        }

        autoInitializeAttempted = true;
        CacheComponents();

        if (!QualifiesForNormalBossEliteSummon(out string rejectReason))
        {
            if (debugSplitBossSummon)
            {
                Debug.Log(
                    "[NormalBossEliteSummonTrace] " +
                    "event=SummonRejected" +
                    " object=" + name +
                    " reason=" + rejectReason,
                    this);
            }

            return;
        }

        EnemySpawner resolvedSpawner = spawner != null ? spawner : FindFirstObjectByType<EnemySpawner>();
        if (resolvedSpawner == null)
        {
            Debug.LogWarning(
                "[NormalBossEliteSummonTrace] " +
                "event=SummonRejected" +
                " object=" + name +
                " reason=MissingSpawner",
                this);
            return;
        }

        InitializeInternal(
            null,
            resolvedSpawner,
            monsterIdentity != null ? monsterIdentity.species : preferredSpecies,
            null,
            firstEliteThreshold,
            secondEliteThreshold,
            minimumEliteCount,
            maximumEliteCount,
            summonRadius,
            suppressRuneDrop,
            debugSplitBossSummon,
            source);
    }

    public void DisableForFinalBoss(string reason)
    {
        UnbindEvents();
        initialized = false;
        owner = null;
        spawnSource = "DisabledForFinalBoss";

        if (debugSplitBossSummon)
        {
            Debug.Log(
                "[NormalBossEliteSummonTrace] " +
                "event=SummonRejected" +
                " object=" + name +
                " reason=" + reason +
                " bossRole=" + (monsterIdentity != null ? monsterIdentity.bossRole.ToString() : "Unknown"),
                this);
        }

        enabled = false;
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void Start()
    {
        TryAutoInitializeFromScene("ComponentStart");
    }

    private void OnEnable()
    {
        if (initialized)
        {
            BindEvents();
            return;
        }

        TryAutoInitializeFromScene("ComponentEnable");
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void OnDestroy()
    {
        UnbindEvents();
        if (owner != null)
        {
            owner.NotifySplitBossDestroyed(gameObject);
        }
    }

    private void HandleDamaged(float damage, GameObject attacker)
    {
        EvaluateSummonThresholds("Damaged");
    }

    private void EvaluateSummonThresholds(string reason)
    {
        if (!initialized || !enableEliteSummons || combatHealth == null || combatHealth.IsDead || spawner == null)
        {
            return;
        }

        if (!QualifiesForNormalBossEliteSummon(out string rejectReason))
        {
            if (debugSplitBossSummon)
            {
                Debug.Log(
                    "[NormalBossEliteSummonTrace] " +
                    "event=SummonRejected" +
                    " object=" + name +
                    " reason=" + rejectReason,
                    this);
            }

            return;
        }

        float maxHealth = Mathf.Max(1f, combatHealth.MaxHealthValue);
        float healthRatio = Mathf.Clamp01(combatHealth.currentHealth / maxHealth);

        if (!firstEliteSummonTriggered && healthRatio <= firstEliteThreshold)
        {
            firstEliteSummonTriggered = true;
            if (debugSplitBossSummon)
            {
                Debug.Log(
                    "[NormalBossEliteSummonTrace] " +
                    "event=ThresholdReached" +
                    " object=" + name +
                    " threshold=0.6" +
                    " spawnCountPending=true" +
                    " elitePrefabCount=" + CountUsableElitePrefabs() +
                    " healthRatio=" + healthRatio.ToString("F3"),
                    this);
            }
            SpawnEliteWave(1, healthRatio, reason);

            if (!secondEliteSummonTriggered && healthRatio <= secondEliteThreshold)
            {
                deferredSecondThresholdEligibleFrame = Time.frameCount + 1;
                StartCoroutine(ReevaluateNextFrame());
            }

            return;
        }

        if (deferredSecondThresholdEligibleFrame >= 0 && Time.frameCount < deferredSecondThresholdEligibleFrame)
        {
            return;
        }

        if (firstEliteSummonTriggered && !secondEliteSummonTriggered && healthRatio <= secondEliteThreshold)
        {
            secondEliteSummonTriggered = true;
            deferredSecondThresholdEligibleFrame = -1;
            if (debugSplitBossSummon)
            {
                Debug.Log(
                    "[NormalBossEliteSummonTrace] " +
                    "event=ThresholdReached" +
                    " object=" + name +
                    " threshold=0.3" +
                    " spawnCountPending=true" +
                    " elitePrefabCount=" + CountUsableElitePrefabs() +
                    " healthRatio=" + healthRatio.ToString("F3"),
                    this);
            }
            SpawnEliteWave(2, healthRatio, reason);
        }
    }

    private IEnumerator ReevaluateNextFrame()
    {
        yield return null;
        EvaluateSummonThresholds("DeferredRecheck");
    }

    private void SpawnEliteWave(int waveIndex, float healthRatio, string reason)
    {
        int summonCount = Random.Range(minimumEliteCount, maximumEliteCount + 1);
        Transform activeTarget = spawner.ResolveActivePlayerTargetForExternalSystems();
        int usableElitePrefabCount = CountUsableElitePrefabs();

        for (int i = 0; i < summonCount; i++)
        {
            GameObject elitePrefab = ResolveElitePrefab();
            if (elitePrefab == null)
            {
                Debug.LogWarning(
                    "[NormalBossEliteSummonTrace] " +
                    "event=SummonRejected" +
                    " object=" + name +
                    " reason=MissingElitePrefabs" +
                    " thresholdWave=" + waveIndex +
                    " elitePrefabCount=" + usableElitePrefabCount,
                    this);
                return;
            }

            Vector3 spawnPosition = transform.position + ResolveScatterOffset(summonRadius, i, summonCount);
            GameObject summonedElite = Instantiate(elitePrefab, spawnPosition, Quaternion.identity);
            summonedElite.name += owner != null
                ? "[TrackedBossElite_Wave" + waveIndex + "_" + (i + 1) + "]"
                : "[StandaloneBossElite_Wave" + waveIndex + "_" + (i + 1) + "]";

            MonsterIdentity identity = summonedElite.GetComponent<MonsterIdentity>();
            if (identity == null)
            {
                identity = summonedElite.AddComponent<MonsterIdentity>();
            }

            identity.species = preferredSpecies;
            identity.rank = MonsterRank.Elite;
            identity.suppressRuneDrop = suppressRuneDrop;
            identity.bossRole = MonsterBossRole.None;
            identity.splitPhaseIndex = monsterIdentity != null ? monsterIdentity.splitPhaseIndex : 0;
            identity.splitBatchId = monsterIdentity != null ? monsterIdentity.splitBatchId : 0;
            identity.sourceFinalBossInstanceId = monsterIdentity != null ? monsterIdentity.sourceFinalBossInstanceId : 0;

            spawner.ApplyOfficialMonsterRuntimeSetup(
                summonedElite,
                preferredSpecies,
                MonsterRank.Elite,
                activeTarget,
                trackAsAlive: true,
                initializeDeathNotifier: true,
                source: "SplitBossEliteSummon");

            owner?.RegisterSummonedElite(summonedElite);

            if (debugSplitBossSummon)
            {
                Debug.Log(
                    "[NormalBossEliteSummonTrace] " +
                    "event=EliteSummoned" +
                    " object=" + name +
                    " summonedElite=" + summonedElite.name +
                    " waveIndex=" + waveIndex +
                    " summonCount=" + summonCount +
                    " healthRatio=" + healthRatio.ToString("F3") +
                    " reason=" + reason +
                    " trackedByFinalBoss=" + (owner != null) +
                    " spawnPosition=" + summonedElite.transform.position,
                    summonedElite);
            }
        }
    }

    private GameObject ResolveElitePrefab()
    {
        if (elitePrefabOverrides != null && elitePrefabOverrides.Length > 0)
        {
            List<GameObject> candidates = new List<GameObject>();
            for (int i = 0; i < elitePrefabOverrides.Length; i++)
            {
                if (elitePrefabOverrides[i] != null)
                {
                    candidates.Add(elitePrefabOverrides[i]);
                }
            }

            if (candidates.Count > 0)
            {
                return candidates[Random.Range(0, candidates.Count)];
            }
        }

        return spawner != null ? spawner.ResolveEliteSummonPrefabForSpecies(preferredSpecies) : null;
    }

    private static Vector3 ResolveScatterOffset(float radius, int index, int count)
    {
        if (count <= 0)
        {
            return Vector3.zero;
        }

        float safeRadius = Mathf.Max(0f, radius);
        float angle = (360f / Mathf.Max(1, count)) * index + Random.Range(-22.5f, 22.5f);
        Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
        Vector3 baseDirection = rotation * Vector3.forward;
        float distance = safeRadius > 0f ? Random.Range(safeRadius * 0.45f, safeRadius) : 0f;
        return baseDirection.normalized * distance;
    }

    private void BindEvents()
    {
        if (subscribed || combatHealth == null)
        {
            return;
        }

        combatHealth.Damaged += HandleDamaged;
        subscribed = true;
    }

    private void UnbindEvents()
    {
        if (!subscribed || combatHealth == null)
        {
            return;
        }

        combatHealth.Damaged -= HandleDamaged;
        subscribed = false;
    }

    private void InitializeInternal(
        BossSlimeFinalSplitController trackedOwner,
        EnemySpawner ownerSpawner,
        MonsterSpecies species,
        GameObject[] prefabOverrides,
        float firstThreshold,
        float secondThreshold,
        int minEliteCount,
        int maxEliteCount,
        float radius,
        bool suppressDrops,
        bool debugLogs,
        string source)
    {
        owner = trackedOwner;
        spawner = ownerSpawner;
        preferredSpecies = species;
        elitePrefabOverrides = prefabOverrides;
        firstEliteThreshold = Mathf.Clamp01(firstThreshold);
        secondEliteThreshold = Mathf.Clamp01(secondThreshold);
        minimumEliteCount = Mathf.Max(1, minEliteCount);
        maximumEliteCount = Mathf.Max(minimumEliteCount, maxEliteCount);
        summonRadius = Mathf.Max(0f, radius);
        suppressRuneDrop = suppressDrops;
        debugSplitBossSummon = debugLogs;
        spawnSource = source;

        CacheComponents();
        initialized = true;
        firstEliteSummonTriggered = false;
        secondEliteSummonTriggered = false;
        deferredSecondThresholdEligibleFrame = -1;

        UnbindEvents();
        BindEvents();

        float maxHealth = combatHealth != null ? Mathf.Max(1f, combatHealth.MaxHealthValue) : 1f;
        float healthRatio = combatHealth != null ? Mathf.Clamp01(combatHealth.currentHealth / maxHealth) : 0f;
        int elitePrefabCount = CountUsableElitePrefabs();

        Debug.Log(
            "[NormalBossEliteSummonTrace] " +
            "event=Initialized" +
            " object=" + name +
            " instanceId=" + GetInstanceID() +
            " spawnSource=" + spawnSource +
            " bossRole=" + (monsterIdentity != null ? monsterIdentity.bossRole.ToString() : "Unknown") +
            " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
            " controllerPresent=" + (this != null) +
            " controllerEnabled=" + enabled +
            " currentHp=" + (combatHealth != null ? combatHealth.currentHealth.ToString("F2") : "0") +
            " maxHp=" + maxHealth.ToString("F2") +
            " healthRatio=" + healthRatio.ToString("F3") +
            " firstThreshold=0.6" +
            " secondThreshold=0.3" +
            " firstTriggered=" + firstEliteSummonTriggered +
            " secondTriggered=" + secondEliteSummonTriggered +
            " elitePrefabCount=" + elitePrefabCount,
            this);

        Debug.Log(
            "[NormalBossEliteSummonTrace] " +
            "event=HealthBinding" +
            " object=" + name +
            " controllerHealthObject=" + (combatHealth != null ? combatHealth.gameObject.name : "null") +
            " controllerHealthInstanceId=" + (combatHealth != null ? combatHealth.GetInstanceID().ToString() : "0") +
            " currentHp=" + (combatHealth != null ? combatHealth.currentHealth.ToString("F2") : "0") +
            " maxHp=" + maxHealth.ToString("F2") +
            " healthRatio=" + healthRatio.ToString("F3"),
            this);
    }

    private void CacheComponents()
    {
        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
        }

        if (monsterIdentity == null)
        {
            monsterIdentity = GetComponent<MonsterIdentity>();
        }
    }

    private bool QualifiesForNormalBossEliteSummon(out string rejectReason)
    {
        CacheComponents();

        if (!enabled)
        {
            rejectReason = "Disabled";
            return false;
        }

        if (monsterIdentity == null)
        {
            rejectReason = "MissingIdentity";
            return false;
        }

        if (combatHealth == null)
        {
            rejectReason = "MissingHealth";
            return false;
        }

        if (combatHealth.IsDead || combatHealth.currentHealth <= 0f)
        {
            rejectReason = "Dead";
            return false;
        }

        if (monsterIdentity.rank != MonsterRank.Boss)
        {
            rejectReason = "InvalidIdentity";
            return false;
        }

        if (monsterIdentity.bossRole == MonsterBossRole.FinalBoss)
        {
            rejectReason = "FinalBossExcluded";
            return false;
        }

        rejectReason = string.Empty;
        return true;
    }

    private int CountUsableElitePrefabs()
    {
        int count = 0;
        if (elitePrefabOverrides != null)
        {
            for (int i = 0; i < elitePrefabOverrides.Length; i++)
            {
                if (elitePrefabOverrides[i] != null)
                {
                    count++;
                }
            }
        }

        if (count > 0)
        {
            return count;
        }

        return spawner != null && spawner.eliteEnemyPrefabs != null
            ? CountNonNullPrefabs(spawner.eliteEnemyPrefabs)
            : 0;
    }

    private static int CountNonNullPrefabs(GameObject[] prefabs)
    {
        if (prefabs == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
            {
                count++;
            }
        }

        return count;
    }
}
