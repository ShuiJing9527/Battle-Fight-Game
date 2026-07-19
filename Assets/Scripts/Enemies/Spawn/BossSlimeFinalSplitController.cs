using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossSlimeFinalSplitController : MonoBehaviour
{
    private struct RuntimeCombatSnapshot
    {
        public float maxHealth;
        public float currentHealth;
        public float healthRatio;
        public float physicalAttack;
        public float physicalDefense;
        public float specialAttack;
        public float specialDefense;
        public float speed;
    }

    [Header("Final Boss Phase Split")]
    [SerializeField] private bool enableFinalBossPhaseSplit = true;
    [SerializeField, Range(0.01f, 1f)] private float firstSplitHealthThreshold = 0.60f;
    [SerializeField, Range(0.01f, 1f)] private float secondSplitHealthThreshold = 0.30f;
    [SerializeField, Min(1)] private int minimumSplitBossCount = 1;
    [SerializeField, Min(1)] private int maximumSplitBossCount = 4;
    [SerializeField, Range(0.01f, 1f)] private float splitBossCombatRatio = 0.50f;
    [SerializeField, Min(0f)] private float splitDurationSeconds = 10f;
    [SerializeField, Min(1f)] private float mergeCombatMultiplier = 2f;
    [SerializeField, Min(0f)] private float splitSpawnRadius = 3f;
    [SerializeField] private GameObject splitBossPrefab;
    [SerializeField] private bool suppressSplitBodyRuneDrop = true;

    [Header("Split Boss Elite Summon")]
    [SerializeField] private GameObject[] eliteEnemyPrefabs;
    [SerializeField, Range(0.01f, 1f)] private float splitBossFirstEliteThreshold = 0.60f;
    [SerializeField, Range(0.01f, 1f)] private float splitBossSecondEliteThreshold = 0.30f;
    [SerializeField, Min(1)] private int minimumEliteSummonCount = 1;
    [SerializeField, Min(1)] private int maximumEliteSummonCount = 4;
    [SerializeField, Min(0f)] private float eliteSummonRadius = 2.5f;
    [SerializeField] private bool suppressEliteSummonRuneDrop = true;
    [SerializeField] private bool cleanupSummonedElitesOnMerge = true;

    [Header("Debug")]
    [SerializeField] private bool debugFinalBossSplit = true;

    private EnemySpawner spawner;
    private CombatHealth combatHealth;
    private CombatStats combatStats;
    private BattleResourceBank resourceBank;
    private MonsterIdentity monsterIdentity;
    private EnemyController enemyController;
    private Rigidbody body;
    private WorldHealthBar worldHealthBar;

    private bool initialized;
    private bool subscribed;
    private bool firstSplitTriggered;
    private bool secondSplitTriggered;
    private bool splitSequenceActive;
    private float accumulatedMergeCombatMultiplier = 1f;
    private int activeSplitBatchId;
    private Coroutine mergeRoutine;

    private Renderer[] hiddenRenderers;
    private bool[] hiddenRendererEnabledStates;
    private Collider[] hiddenColliders;
    private bool[] hiddenColliderEnabledStates;
    private bool worldHealthBarWasEnabled;
    private Transform worldHealthBarRoot;
    private bool worldHealthBarRootWasActive;
    private bool bodyUseGravityBeforeHide;
    private bool bodyIsKinematicBeforeHide;
    private RigidbodyConstraints bodyConstraintsBeforeHide;

    private readonly List<GameObject> activeSplitBosses = new List<GameObject>();
    private readonly List<GameObject> activeSummonedElites = new List<GameObject>();

    public bool IsSplitSequenceActive => splitSequenceActive;
    public float AccumulatedMergeCombatMultiplier => accumulatedMergeCombatMultiplier;

    public void Initialize(EnemySpawner owner)
    {
        spawner = owner;
        CacheComponents();

        if (monsterIdentity == null || combatHealth == null)
        {
            enabled = false;
            return;
        }

        monsterIdentity.bossRole = MonsterBossRole.FinalBoss;
        monsterIdentity.splitPhaseIndex = 0;
        monsterIdentity.splitBatchId = 0;
        monsterIdentity.sourceFinalBossInstanceId = gameObject.GetInstanceID();

        if (enemyController != null)
        {
            enemyController.SetBossTimedSplitEnabled(false);
        }

        SplitBossMinionController normalBossEliteSummon = GetComponent<SplitBossMinionController>();
        if (normalBossEliteSummon != null)
        {
            normalBossEliteSummon.DisableForFinalBoss("FinalBossExcluded");
        }

        EliteSlimeSplitOnDeath legacyEliteSplit = GetComponent<EliteSlimeSplitOnDeath>();
        if (legacyEliteSplit != null)
        {
            legacyEliteSplit.enabled = false;

            if (debugFinalBossSplit)
            {
                Debug.Log(
                    "[EnemySplitConflictTrace] " +
                    "event=ConflictDetected" +
                    " object=" + name +
                    " conflictType=FinalBossWithLegacyEliteSplit" +
                    " resolvedBy=DisableEliteSlimeSplitOnDeath",
                    this);
            }
        }

        spawner?.ClearPhaseSplitBossModifiers(gameObject, refreshRuntimeScaling: true, refillCurrentHealth: false);

        firstSplitTriggered = false;
        secondSplitTriggered = false;
        splitSequenceActive = false;
        accumulatedMergeCombatMultiplier = 1f;
        activeSplitBatchId = 0;
        initialized = true;

        UnbindEvents();
        BindEvents();

        if (debugFinalBossSplit)
        {
            Debug.Log(
                "[FinalBossSplitDebug] " +
                "event=Initialize" +
                " boss=" + name +
                " enableFinalBossPhaseSplit=" + enableFinalBossPhaseSplit +
                " threshold60=" + firstSplitHealthThreshold.ToString("F2") +
                " threshold30=" + secondSplitHealthThreshold.ToString("F2") +
                " splitBossCountRange=" + minimumSplitBossCount + "-" + maximumSplitBossCount +
                " splitBossCombatRatio=" + splitBossCombatRatio.ToString("F2") +
                " splitDurationSeconds=" + splitDurationSeconds.ToString("F2") +
                " mergeCombatMultiplier=" + mergeCombatMultiplier.ToString("F2"),
                this);
        }
    }

    public void RegisterSummonedElite(GameObject elite)
    {
        if (elite == null || activeSummonedElites.Contains(elite))
        {
            return;
        }

        activeSummonedElites.Add(elite);
    }

    public void NotifySplitBossDestroyed(GameObject splitBoss)
    {
        if (splitBoss == null)
        {
            return;
        }

        activeSplitBosses.Remove(splitBoss);
    }

    private void Awake()
    {
        CacheComponents();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void OnDestroy()
    {
        UnbindEvents();
        DestroyTrackedSplitBosses();
        DestroyTrackedSummonedElites();
    }

    private void HandleDamaged(float damage, GameObject attacker)
    {
        EvaluateSplitThresholds("Damaged");
    }

    private void HandleDied(GameObject killer)
    {
        if (mergeRoutine != null)
        {
            StopCoroutine(mergeRoutine);
            mergeRoutine = null;
        }

        DestroyTrackedSplitBosses();
        DestroyTrackedSummonedElites();
    }

    private void CacheComponents()
    {
        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
        }

        if (combatStats == null)
        {
            combatStats = GetComponent<CombatStats>();
        }

        if (resourceBank == null)
        {
            resourceBank = GetComponent<BattleResourceBank>();
        }

        if (monsterIdentity == null)
        {
            monsterIdentity = GetComponent<MonsterIdentity>();
        }

        if (enemyController == null)
        {
            enemyController = GetComponent<EnemyController>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        if (worldHealthBar == null)
        {
            worldHealthBar = GetComponent<WorldHealthBar>();
        }
    }

    private void EvaluateSplitThresholds(string reason)
    {
        if (!initialized || !enableFinalBossPhaseSplit || splitSequenceActive || spawner == null || combatHealth == null || combatHealth.IsDead)
        {
            return;
        }

        float currentMaxHealth = ResolveCurrentMaxHealth();
        if (currentMaxHealth <= 0f)
        {
            return;
        }

        float currentHealthRatio = Mathf.Clamp01(ResolveCurrentHealth() / currentMaxHealth);

        if (!firstSplitTriggered && currentHealthRatio <= Mathf.Clamp01(firstSplitHealthThreshold))
        {
            if (TryBeginSplitPhase(1, currentHealthRatio, reason))
            {
                firstSplitTriggered = true;
            }

            return;
        }

        if (firstSplitTriggered && !secondSplitTriggered && currentHealthRatio <= Mathf.Clamp01(secondSplitHealthThreshold))
        {
            if (TryBeginSplitPhase(2, currentHealthRatio, reason))
            {
                secondSplitTriggered = true;
            }
        }
    }

    private bool TryBeginSplitPhase(int phaseIndex, float currentHealthRatio, string reason)
    {
        RuntimeCombatSnapshot sourceSnapshot = CaptureCurrentCombatSnapshot();
        int splitCount = Random.Range(
            Mathf.Max(1, minimumSplitBossCount),
            Mathf.Max(Mathf.Max(1, minimumSplitBossCount), maximumSplitBossCount) + 1);

        if (splitCount <= 0)
        {
            return false;
        }

        GameObject resolvedSplitBossPrefab = ResolveSplitBossPrefab();
        if (resolvedSplitBossPrefab == null)
        {
            Debug.LogWarning(
                "[FinalBossSplitDebug] event=SplitAborted reason=MissingSplitBossPrefab boss=" + name +
                " phaseIndex=" + phaseIndex,
                this);
            return false;
        }

        splitSequenceActive = true;
        activeSplitBatchId++;
        Vector3 splitOrigin = transform.position;
        Transform activeTarget = spawner.ResolveActivePlayerTargetForExternalSystems();

        HideFinalBossBody("FinalBossSplitPhase" + phaseIndex);
        SpawnSplitBosses(
            resolvedSplitBossPrefab,
            sourceSnapshot,
            splitCount,
            phaseIndex,
            splitOrigin,
            activeTarget);

        if (mergeRoutine != null)
        {
            StopCoroutine(mergeRoutine);
        }

        mergeRoutine = StartCoroutine(MergeAfterDurationRoutine(phaseIndex, splitOrigin));

        if (debugFinalBossSplit)
        {
            Debug.Log(
                "[FinalBossSplitDebug] " +
                "event=SplitStarted" +
                " boss=" + name +
                " phaseIndex=" + phaseIndex +
                " reason=" + reason +
                " sourceCurrentHealth=" + sourceSnapshot.currentHealth.ToString("F1") +
                " sourceMaxHealth=" + sourceSnapshot.maxHealth.ToString("F1") +
                " sourceHealthRatio=" + currentHealthRatio.ToString("F3") +
                " splitCount=" + splitCount +
                " splitDurationSeconds=" + splitDurationSeconds.ToString("F2") +
                " mergePowerBefore=" + accumulatedMergeCombatMultiplier.ToString("F2"),
                this);
        }

        return true;
    }

    private RuntimeCombatSnapshot CaptureCurrentCombatSnapshot()
    {
        RuntimeCombatSnapshot snapshot = new RuntimeCombatSnapshot
        {
            maxHealth = ResolveCurrentMaxHealth(),
            currentHealth = ResolveCurrentHealth(),
            physicalAttack = combatStats != null ? Mathf.Max(0f, combatStats.physicalAttack) : 0f,
            physicalDefense = combatStats != null ? Mathf.Max(0f, combatStats.physicalDefense) : 0f,
            specialAttack = combatStats != null ? Mathf.Max(0f, combatStats.specialAttack) : 0f,
            specialDefense = combatStats != null ? Mathf.Max(0f, combatStats.specialDefense) : 0f,
            speed = combatStats != null ? Mathf.Max(0.01f, combatStats.speed) : 0.01f
        };

        snapshot.healthRatio = snapshot.maxHealth > 0f
            ? Mathf.Clamp01(snapshot.currentHealth / snapshot.maxHealth)
            : 1f;

        return snapshot;
    }

    private float ResolveCurrentMaxHealth()
    {
        if (resourceBank != null)
        {
            return Mathf.Max(1f, resourceBank.maxHealth);
        }

        if (combatHealth != null)
        {
            return Mathf.Max(1f, combatHealth.MaxHealthValue);
        }

        return combatStats != null ? Mathf.Max(1f, combatStats.maxHealth) : 1f;
    }

    private float ResolveCurrentHealth()
    {
        if (resourceBank != null)
        {
            return Mathf.Max(0f, resourceBank.currentHealth);
        }

        return combatHealth != null ? Mathf.Max(0f, combatHealth.currentHealth) : 0f;
    }

    private void HideFinalBossBody(string reason)
    {
        CacheComponents();

        if (enemyController != null)
        {
            enemyController.AbortCombatForExternalPhase(reason);
            enemyController.enabled = false;
        }

        if (body != null)
        {
            bodyUseGravityBeforeHide = body.useGravity;
            bodyIsKinematicBeforeHide = body.isKinematic;
            bodyConstraintsBeforeHide = body.constraints;
            body.linearVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
        }

        hiddenRenderers = GetComponentsInChildren<Renderer>(true);
        hiddenRendererEnabledStates = new bool[hiddenRenderers.Length];
        for (int i = 0; i < hiddenRenderers.Length; i++)
        {
            Renderer renderer = hiddenRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            hiddenRendererEnabledStates[i] = renderer.enabled;
            renderer.enabled = false;
        }

        hiddenColliders = GetComponentsInChildren<Collider>(true);
        hiddenColliderEnabledStates = new bool[hiddenColliders.Length];
        for (int i = 0; i < hiddenColliders.Length; i++)
        {
            Collider collider = hiddenColliders[i];
            if (collider == null)
            {
                continue;
            }

            hiddenColliderEnabledStates[i] = collider.enabled;
            collider.enabled = false;
        }

        worldHealthBarRoot = FindNamedChild(transform, "WorldHealthBar");
        worldHealthBarRootWasActive = worldHealthBarRoot != null && worldHealthBarRoot.gameObject.activeSelf;
        if (worldHealthBarRoot != null)
        {
            worldHealthBarRoot.gameObject.SetActive(false);
        }

        if (worldHealthBar != null)
        {
            worldHealthBarWasEnabled = worldHealthBar.enabled;
            worldHealthBar.enabled = false;
        }
    }

    private void RestoreFinalBossBody(Vector3 mergePosition, string source)
    {
        transform.position = mergePosition;

        if (hiddenColliders != null)
        {
            for (int i = 0; i < hiddenColliders.Length; i++)
            {
                Collider collider = hiddenColliders[i];
                if (collider == null)
                {
                    continue;
                }

                bool shouldEnable = hiddenColliderEnabledStates != null && i < hiddenColliderEnabledStates.Length && hiddenColliderEnabledStates[i];
                collider.enabled = shouldEnable;
            }
        }

        if (hiddenRenderers != null)
        {
            for (int i = 0; i < hiddenRenderers.Length; i++)
            {
                Renderer renderer = hiddenRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                bool shouldEnable = hiddenRendererEnabledStates != null && i < hiddenRendererEnabledStates.Length && hiddenRendererEnabledStates[i];
                renderer.enabled = shouldEnable;
            }
        }

        if (worldHealthBarRoot != null)
        {
            worldHealthBarRoot.gameObject.SetActive(worldHealthBarRootWasActive);
        }

        if (worldHealthBar != null)
        {
            worldHealthBar.enabled = worldHealthBarWasEnabled;
            worldHealthBar.RefreshWorldPositionForDebug();
        }

        if (body != null)
        {
            body.isKinematic = bodyIsKinematicBeforeHide;
            body.useGravity = bodyUseGravityBeforeHide;
            body.constraints = bodyConstraintsBeforeHide;
            body.linearVelocity = Vector3.zero;
        }

        if (enemyController != null)
        {
            enemyController.EnsureBossSkillComponentsForRuntime();
            enemyController.enabled = true;
            enemyController.SetBossTimedSplitEnabled(false);
            enemyController.SetTarget(spawner != null ? spawner.ResolveActivePlayerTargetForExternalSystems() : null, source);
        }
    }

    private void SpawnSplitBosses(
        GameObject resolvedSplitBossPrefab,
        RuntimeCombatSnapshot sourceSnapshot,
        int splitCount,
        int phaseIndex,
        Vector3 splitOrigin,
        Transform targetOverride)
    {
        activeSplitBosses.Clear();

        MonsterSpecies species = monsterIdentity != null ? monsterIdentity.species : MonsterSpecies.BlueSlime;
        for (int i = 0; i < splitCount; i++)
        {
            Vector3 spawnOffset = ResolveScatterOffset(splitSpawnRadius, i, splitCount);
            Vector3 spawnPosition = splitOrigin + spawnOffset;
            GameObject spawnedBoss = Instantiate(resolvedSplitBossPrefab, spawnPosition, Quaternion.identity);
            spawnedBoss.name += "[FinalBossSplit_Phase" + phaseIndex + "_" + (i + 1) + "]";

            MonsterIdentity childIdentity = spawnedBoss.GetComponent<MonsterIdentity>();
            if (childIdentity == null)
            {
                childIdentity = spawnedBoss.AddComponent<MonsterIdentity>();
            }

            childIdentity.species = species;
            childIdentity.rank = MonsterRank.Boss;
            childIdentity.suppressRuneDrop = suppressSplitBodyRuneDrop;
            childIdentity.bossRole = MonsterBossRole.SplitBoss;
            childIdentity.splitPhaseIndex = phaseIndex;
            childIdentity.splitBatchId = activeSplitBatchId;
            childIdentity.sourceFinalBossInstanceId = gameObject.GetInstanceID();

            spawner.ApplyOfficialMonsterRuntimeSetup(
                spawnedBoss,
                species,
                MonsterRank.Boss,
                targetOverride,
                trackAsAlive: true,
                initializeDeathNotifier: true,
                source: "FinalBossSplit");

            ApplySnapshotScaledCombatToSplitBoss(spawnedBoss, sourceSnapshot, splitBossCombatRatio);

            EnemyController childController = spawnedBoss.GetComponent<EnemyController>();
            if (childController != null)
            {
                childController.SetBossTimedSplitEnabled(false);
            }

            EliteSlimeSplitOnDeath legacySplitComponent = spawnedBoss.GetComponent<EliteSlimeSplitOnDeath>();
            if (legacySplitComponent != null)
            {
                legacySplitComponent.enabled = false;

                if (debugFinalBossSplit)
                {
                    Debug.Log(
                        "[EnemySplitConflictTrace] " +
                        "event=ConflictDetected" +
                        " object=" + spawnedBoss.name +
                        " conflictType=SplitBossWithLegacyEliteSplit" +
                        " resolvedBy=DisableEliteSlimeSplitOnDeath",
                        spawnedBoss);
                }
            }

            SplitBossMinionController minionController = spawnedBoss.GetComponent<SplitBossMinionController>();
            if (minionController == null)
            {
                minionController = spawnedBoss.AddComponent<SplitBossMinionController>();
            }

            minionController.Initialize(
                owner: this,
                ownerSpawner: spawner,
                preferredSpecies: species,
                elitePrefabOverrides: eliteEnemyPrefabs,
                firstEliteThreshold: splitBossFirstEliteThreshold,
                secondEliteThreshold: splitBossSecondEliteThreshold,
                minEliteCount: minimumEliteSummonCount,
                maxEliteCount: maximumEliteSummonCount,
                summonRadius: eliteSummonRadius,
                suppressRuneDrop: suppressEliteSummonRuneDrop,
                debugLogs: debugFinalBossSplit);

            activeSplitBosses.Add(spawnedBoss);

            if (debugFinalBossSplit)
            {
                Debug.Log(
                    "[FinalBossSplitDebug] " +
                    "event=SplitBossSpawned" +
                    " owner=" + name +
                    " splitBoss=" + spawnedBoss.name +
                    " phaseIndex=" + phaseIndex +
                    " splitBatchId=" + activeSplitBatchId +
                    " spawnPosition=" + spawnedBoss.transform.position +
                    " sourceMaxHealth=" + sourceSnapshot.maxHealth.ToString("F1") +
                    " sourceAttack=" + sourceSnapshot.physicalAttack.ToString("F1") +
                    " splitRatio=" + splitBossCombatRatio.ToString("F2"),
                    spawnedBoss);
            }
        }
    }

    private void ApplySnapshotScaledCombatToSplitBoss(GameObject splitBoss, RuntimeCombatSnapshot sourceSnapshot, float ratio)
    {
        if (splitBoss == null || spawner == null)
        {
            return;
        }

        CombatStats splitStats = splitBoss.GetComponent<CombatStats>();
        if (splitStats == null)
        {
            return;
        }

        float desiredMaxHealth = Mathf.Max(1f, sourceSnapshot.maxHealth * Mathf.Max(0.01f, ratio));
        float desiredPhysicalAttack = Mathf.Max(0f, sourceSnapshot.physicalAttack * Mathf.Max(0.01f, ratio));
        float desiredPhysicalDefense = Mathf.Max(0f, sourceSnapshot.physicalDefense * Mathf.Max(0.01f, ratio));
        float desiredSpecialAttack = Mathf.Max(0f, sourceSnapshot.specialAttack * Mathf.Max(0.01f, ratio));
        float desiredSpecialDefense = Mathf.Max(0f, sourceSnapshot.specialDefense * Mathf.Max(0.01f, ratio));
        float desiredSpeed = Mathf.Max(0.01f, sourceSnapshot.speed * Mathf.Max(0.01f, ratio));

        float hpMultiplier = SafeDivide(desiredMaxHealth, splitStats.maxHealth);
        float attackMultiplier = SafeDivide(desiredPhysicalAttack, splitStats.physicalAttack);
        float defenseMultiplier = SafeDivide(desiredPhysicalDefense, splitStats.physicalDefense);
        float specialAttackMultiplier = SafeDivide(desiredSpecialAttack, splitStats.specialAttack);
        float specialDefenseMultiplier = SafeDivide(desiredSpecialDefense, splitStats.specialDefense);
        float speedMultiplier = SafeDivide(desiredSpeed, splitStats.speed);

        spawner.SetPhaseSplitBossModifiers(
            splitBoss,
            hpMultiplier,
            attackMultiplier,
            defenseMultiplier,
            specialAttackMultiplier,
            specialDefenseMultiplier,
            speedMultiplier,
            refreshRuntimeScaling: true,
            refillCurrentHealth: true);

        CombatHealth splitHealth = splitBoss.GetComponent<CombatHealth>();
        if (splitHealth != null)
        {
            splitHealth.SyncHealthFromStats(refillCurrentHealth: true);
        }

        WorldHealthBar splitHealthBar = splitBoss.GetComponent<WorldHealthBar>();
        if (splitHealthBar != null)
        {
            splitHealthBar.RefreshWorldPositionForDebug();
        }
    }

    private IEnumerator MergeAfterDurationRoutine(int phaseIndex, Vector3 splitOrigin)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, splitDurationSeconds));

        mergeRoutine = null;

        if (combatHealth == null || combatHealth.IsDead)
        {
            DestroyTrackedSplitBosses();
            DestroyTrackedSummonedElites();
            yield break;
        }

        Vector3 mergePosition = ResolveMergePosition(splitOrigin);

        if (cleanupSummonedElitesOnMerge)
        {
            DestroyTrackedSummonedElites();
        }

        DestroyTrackedSplitBosses();

        accumulatedMergeCombatMultiplier *= Mathf.Max(1f, mergeCombatMultiplier);
        spawner.SetPhaseSplitBossModifiers(
            gameObject,
            hpMultiplier: 1f,
            attackMultiplier: accumulatedMergeCombatMultiplier,
            defenseMultiplier: accumulatedMergeCombatMultiplier,
            specialAttackMultiplier: accumulatedMergeCombatMultiplier,
            specialDefenseMultiplier: accumulatedMergeCombatMultiplier,
            speedMultiplier: accumulatedMergeCombatMultiplier,
            refreshRuntimeScaling: true,
            refillCurrentHealth: false);

        RestoreFinalBossBody(mergePosition, "FinalBossSplitMerge");
        splitSequenceActive = false;

        if (debugFinalBossSplit)
        {
            Debug.Log(
                "[FinalBossSplitDebug] " +
                "event=MergeCompleted" +
                " boss=" + name +
                " phaseIndex=" + phaseIndex +
                " mergePosition=" + mergePosition +
                " newMergeCombatMultiplier=" + accumulatedMergeCombatMultiplier.ToString("F2") +
                " healthRatioAfterMerge=" + (ResolveCurrentHealth() / Mathf.Max(1f, ResolveCurrentMaxHealth())).ToString("F3"),
                this);
        }

        yield return null;
        EvaluateSplitThresholds("PostMergeRecheck");
    }

    private Vector3 ResolveMergePosition(Vector3 fallbackPosition)
    {
        Vector3 accumulatedPosition = Vector3.zero;
        int aliveCount = 0;
        for (int i = 0; i < activeSplitBosses.Count; i++)
        {
            GameObject splitBoss = activeSplitBosses[i];
            if (splitBoss == null)
            {
                continue;
            }

            accumulatedPosition += splitBoss.transform.position;
            aliveCount++;
        }

        return aliveCount > 0 ? accumulatedPosition / aliveCount : fallbackPosition;
    }

    private void DestroyTrackedSplitBosses()
    {
        for (int i = 0; i < activeSplitBosses.Count; i++)
        {
            GameObject splitBoss = activeSplitBosses[i];
            if (splitBoss == null)
            {
                continue;
            }

            EnemyController childController = splitBoss.GetComponent<EnemyController>();
            if (childController != null)
            {
                childController.AbortCombatForExternalPhase("FinalBossMergeCleanup");
            }

            Destroy(splitBoss);
        }

        activeSplitBosses.Clear();
    }

    private void DestroyTrackedSummonedElites()
    {
        for (int i = 0; i < activeSummonedElites.Count; i++)
        {
            GameObject elite = activeSummonedElites[i];
            if (elite == null)
            {
                continue;
            }

            Destroy(elite);
        }

        activeSummonedElites.Clear();
    }

    private GameObject ResolveSplitBossPrefab()
    {
        if (splitBossPrefab != null)
        {
            return splitBossPrefab;
        }

        return spawner != null && monsterIdentity != null
            ? spawner.ResolveBossSplitPrefabForSpecies(monsterIdentity.species)
            : null;
    }

    private static float SafeDivide(float desiredValue, float currentValue)
    {
        return Mathf.Abs(currentValue) > 0.0001f
            ? Mathf.Max(0.01f, desiredValue / currentValue)
            : 1f;
    }

    private static Vector3 ResolveScatterOffset(float radius, int index, int count)
    {
        if (count <= 0)
        {
            return Vector3.zero;
        }

        float safeRadius = Mathf.Max(0f, radius);
        float angle = (360f / Mathf.Max(1, count)) * index + Random.Range(-15f, 15f);
        Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
        Vector3 baseDirection = rotation * Vector3.forward;
        float distance = safeRadius > 0f ? Random.Range(safeRadius * 0.55f, safeRadius) : 0f;
        return baseDirection.normalized * distance;
    }

    private static Transform FindNamedChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private void BindEvents()
    {
        if (subscribed || combatHealth == null)
        {
            return;
        }

        combatHealth.Damaged += HandleDamaged;
        combatHealth.Died += HandleDied;
        subscribed = true;
    }

    private void UnbindEvents()
    {
        if (!subscribed || combatHealth == null)
        {
            return;
        }

        combatHealth.Damaged -= HandleDamaged;
        combatHealth.Died -= HandleDied;
        subscribed = false;
    }
}
