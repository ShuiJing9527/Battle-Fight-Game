using UnityEngine;

public class EliteSlimeSplitOnDeath : MonoBehaviour
{
    [Header("Elite Split")]
    [Tooltip("How many normal slime children an elite slime spawns on death. 0 disables elite splitting.")]
    [SerializeField, Min(0)] private int splitCount = 2;
    [Tooltip("How far elite split children scatter from the parent death position.")]
    [SerializeField, Min(0f)] private float splitScatterRadius = 1.2f;

    [Header("Boss Split")]
    [Tooltip("Whether slime bosses are allowed to reuse the elite split flow on death.")]
    [SerializeField] private bool bossCanSplit = true;
    [Tooltip("How many children a slime boss spawns when boss splitting is enabled.")]
    [SerializeField, Min(0)] private int bossSplitCount = 2;
    [Tooltip("How far boss split children scatter from the boss death position.")]
    [SerializeField, Min(0f)] private float bossSplitScatterRadius = 1.5f;
    [Tooltip("Target rank used for boss split children. Boss is automatically downgraded to Elite to prevent recursive boss chains.")]
    [SerializeField] private MonsterRank bossSplitChildRank = MonsterRank.Elite;
    [Tooltip("Multiplier applied to child HP after normal spawn-time scaling has been applied.")]
    [SerializeField, Min(0f)] private float bossSplitHealthRatio = 0.65f;
    [Tooltip("Multiplier applied to child physical and special attack after normal spawn-time scaling has been applied.")]
    [SerializeField, Min(0f)] private float bossSplitAttackRatio = 0.75f;
    [Tooltip("Multiplier applied to child physical and special defense after normal spawn-time scaling has been applied.")]
    [SerializeField, Min(0f)] private float bossSplitDefenseRatio = 0.7f;
    [Tooltip("Multiplier applied to child speed after normal spawn-time scaling has been applied.")]
    [SerializeField, Min(0f)] private float bossSplitSpeedRatio = 1f;
    [Tooltip("Multiplier applied to the spawned child visual scale.")]
    [SerializeField, Min(0f)] private float bossSplitScaleRatio = 0.9f;
    [Tooltip("If false, spawned boss children have their split component disabled to prevent infinite recursion.")]
    [SerializeField] private bool bossChildrenCanSplit = false;
    [Tooltip("Legacy compatibility field. Cleanup bosses no longer use death split and instead use CleanupBoss phase split on EnemySpawner.")]
    [SerializeField] private bool finalBossCanSplit = true;

    [Header("Debug")]
    [Tooltip("Print a one-shot [BossSplit] log when boss split is evaluated or triggered.")]
    [SerializeField] private bool debugSplitLogs = false;

    private CombatHealth combatHealth;
    private bool deathBound;
    private bool splitTriggered;

    private void OnEnable()
    {
        splitTriggered = false;
        TryBindDeathEvent();
    }

    private void Start()
    {
        TryBindDeathEvent();
    }

    private void OnDisable()
    {
        if (deathBound && combatHealth != null)
        {
            combatHealth.Died -= Split;
        }

        deathBound = false;
    }

    private void TryBindDeathEvent()
    {
        if (deathBound)
        {
            return;
        }

        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
        }

        if (combatHealth == null)
        {
            return;
        }

        combatHealth.Died += Split;
        deathBound = true;
    }

    private void Split(GameObject killer)
    {
        if (splitTriggered)
        {
            return;
        }

        MonsterIdentity identity = GetComponent<MonsterIdentity>();
        if (identity == null || !IsSlime(identity.species))
        {
            return;
        }

        EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
        if (spawner == null)
        {
            Debug.LogWarning($"[EliteSlimeSplitOnDeath] No EnemySpawner found. Elite slime '{name}' could not split.", this);
            return;
        }

        if (identity.rank == MonsterRank.Elite)
        {
            if (splitCount <= 0)
            {
                return;
            }

            splitTriggered = true;
            spawner.SpawnSplitNormalsFromElite(gameObject, splitCount, splitScatterRadius);
            return;
        }

        if (identity.rank != MonsterRank.Boss || !bossCanSplit || bossSplitCount <= 0)
        {
            return;
        }

        EnemyDifficultyDirector director = EnemyDifficultyDirector.Instance;
        bool isCleanupBoss = director != null && director.CleanupBossInstance == gameObject;
        if (isCleanupBoss)
        {
            if (debugSplitLogs)
            {
                Debug.Log($"[BossSplit] boss={name} trigger=Death skipped=true reason=CleanupBossUsesPhaseSplit", this);
            }
            return;
        }

        splitTriggered = true;
        spawner.SpawnSplitChildren(
            gameObject,
            bossSplitCount,
            bossSplitScatterRadius,
            bossSplitChildRank,
            bossSplitHealthRatio,
            bossSplitAttackRatio,
            bossSplitDefenseRatio,
            bossSplitSpeedRatio,
            bossSplitScaleRatio,
            bossChildrenCanSplit,
            isCleanupBoss,
            debugSplitLogs);
    }

    private static bool IsSlime(MonsterSpecies species)
    {
        return species == MonsterSpecies.BlueSlime ||
               species == MonsterSpecies.GreenSlime ||
               species == MonsterSpecies.LavaSlime ||
               species == MonsterSpecies.PoisonSlime ||
               species == MonsterSpecies.RainbowSlime;
    }
}
