using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CleanupBossPhaseSplit : MonoBehaviour
{
    private EnemySpawner spawner;
    private GameObject cleanupBoss;
    private CombatHealth combatHealth;
    private bool enabledForCleanupBoss;
    private bool debugLogs;
    private float[] healthThresholds = { 0.7f, 0.5f, 0.3f };
    private int splitCountPerThreshold = 2;
    private float splitScatterRadius = 1.5f;
    private MonsterRank splitChildRank = MonsterRank.Elite;
    private float childHealthRatio = 0.35f;
    private float childAttackRatio = 0.55f;
    private float childDefenseRatio = 0.5f;
    private float childSpeedRatio = 1f;
    private float childScaleRatio = 0.75f;
    private bool splitChildrenCanSplit;
    private float rewardMultiplier = 1f;
    private bool initialized;
    private bool subscribed;
    private bool deathLogged;
    private readonly HashSet<int> triggeredThresholdIndices = new HashSet<int>();
    private readonly List<GameObject> spawnedPhaseChildren = new List<GameObject>();

    public float CleanupBossRewardMultiplier => Mathf.Max(0.01f, rewardMultiplier);

    public void Initialize(
        EnemySpawner owner,
        GameObject cleanupBossObject,
        bool phaseSplitEnabled,
        float[] thresholds,
        int splitCount,
        float scatterRadius,
        MonsterRank childRank,
        float healthRatio,
        float attackRatio,
        float defenseRatio,
        float speedRatio,
        float scaleRatio,
        bool childrenCanSplit,
        float cleanupRewardMultiplier,
        bool debug)
    {
        spawner = owner;
        cleanupBoss = cleanupBossObject;
        enabledForCleanupBoss = phaseSplitEnabled;
        debugLogs = debug;
        splitCountPerThreshold = Mathf.Max(0, splitCount);
        splitScatterRadius = Mathf.Max(0f, scatterRadius);
        splitChildRank = childRank;
        childHealthRatio = Mathf.Max(0f, healthRatio);
        childAttackRatio = Mathf.Max(0f, attackRatio);
        childDefenseRatio = Mathf.Max(0f, defenseRatio);
        childSpeedRatio = Mathf.Max(0f, speedRatio);
        childScaleRatio = Mathf.Max(0f, scaleRatio);
        splitChildrenCanSplit = childrenCanSplit;
        rewardMultiplier = Mathf.Max(0.01f, cleanupRewardMultiplier);
        healthThresholds = thresholds != null && thresholds.Length > 0
            ? (float[])thresholds.Clone()
            : new[] { 0.7f, 0.5f, 0.3f };

        triggeredThresholdIndices.Clear();
        spawnedPhaseChildren.Clear();
        deathLogged = false;
        initialized = true;
        combatHealth = cleanupBoss != null ? cleanupBoss.GetComponent<CombatHealth>() : null;

        UnbindEvents();
        BindEvents();

        if (debugLogs)
        {
            Debug.Log(
                $"[CleanupBossPhaseSplit] boss={name} event=Initialize enabled={enabledForCleanupBoss} thresholds={string.Join(",", healthThresholds)} childCount={splitCountPerThreshold} childRank={splitChildRank} childHP={childHealthRatio:F2} childATK={childAttackRatio:F2} childDEF={childDefenseRatio:F2} childSPD={childSpeedRatio:F2} childScale={childScaleRatio:F2} rewardMultiplier={rewardMultiplier:F2}",
                this);
        }

        EvaluateThresholds("Initialize");
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    private void HandleDamaged(float damage, GameObject attacker)
    {
        EvaluateThresholds("Damaged");
    }

    private void HandleDied(GameObject killer)
    {
        if (deathLogged)
        {
            return;
        }

        deathLogged = true;
        if (debugLogs)
        {
            EnemyDifficultyDirector director = EnemyDifficultyDirector.Instance;
            Debug.Log(
                $"[CleanupBossVictory] cleanup boss body died boss={name} cleanupBossInstanceMatched={(director != null && director.CleanupBossInstance == gameObject)} remainingSplitChildren={CountAliveTrackedChildren()} victoryTriggeredPending=true",
                this);
        }
    }

    private void EvaluateThresholds(string reason)
    {
        if (!initialized || !enabledForCleanupBoss || spawner == null || cleanupBoss != gameObject || combatHealth == null || combatHealth.IsDead)
        {
            return;
        }

        float maxHealth = Mathf.Max(1f, combatHealth.MaxHealthValue);
        float healthRatio = Mathf.Clamp01(combatHealth.currentHealth / maxHealth);
        for (int i = 0; i < healthThresholds.Length; i++)
        {
            float threshold = Mathf.Clamp01(healthThresholds[i]);
            if (triggeredThresholdIndices.Contains(i) || healthRatio > threshold)
            {
                continue;
            }

            triggeredThresholdIndices.Add(i);
            List<GameObject> spawnedChildren = spawner.SpawnSplitChildrenAndCollect(
                gameObject,
                splitCountPerThreshold,
                splitScatterRadius,
                splitChildRank,
                childHealthRatio,
                childAttackRatio,
                childDefenseRatio,
                childSpeedRatio,
                childScaleRatio,
                splitChildrenCanSplit,
                true,
                debugLogs,
                "PhaseThreshold");

            if (spawnedChildren != null && spawnedChildren.Count > 0)
            {
                spawnedPhaseChildren.AddRange(spawnedChildren);
            }

            if (debugLogs)
            {
                Debug.Log(
                    $"[CleanupBossPhaseSplit] boss={name} reason={reason} currentHP={combatHealth.currentHealth:F1} maxHP={maxHealth:F1} healthRatio={healthRatio:F3} threshold={threshold:F2} thresholdIndex={i} childCount={splitCountPerThreshold} childRank={splitChildRank} childHP={childHealthRatio:F2} childATK={childAttackRatio:F2} childDEF={childDefenseRatio:F2} childSPD={childSpeedRatio:F2} childScale={childScaleRatio:F2} alreadyTriggered={triggeredThresholdIndices.Count}",
                    this);
            }
        }
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

    private int CountAliveTrackedChildren()
    {
        int count = 0;
        for (int i = 0; i < spawnedPhaseChildren.Count; i++)
        {
            GameObject child = spawnedPhaseChildren[i];
            if (child == null)
            {
                continue;
            }

            CombatHealth childHealth = child.GetComponent<CombatHealth>();
            if (childHealth == null || !childHealth.IsDead)
            {
                count++;
            }
        }

        return count;
    }
}
