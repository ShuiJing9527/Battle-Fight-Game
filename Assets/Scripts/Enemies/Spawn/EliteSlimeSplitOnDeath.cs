using UnityEngine;

public class EliteSlimeSplitOnDeath : MonoBehaviour
{
    [Header("Split")]
    [SerializeField, Min(0)] private int splitCount = 2;
    [SerializeField, Min(0f)] private float splitScatterRadius = 1.2f;

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
        if (splitTriggered || splitCount <= 0)
        {
            return;
        }

        MonsterIdentity identity = GetComponent<MonsterIdentity>();
        if (identity == null || identity.rank != MonsterRank.Elite || !IsSlime(identity.species))
        {
            return;
        }

        splitTriggered = true;
        EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
        if (spawner == null)
        {
            Debug.LogWarning($"[EliteSlimeSplitOnDeath] No EnemySpawner found. Elite slime '{name}' could not split.", this);
            return;
        }

        spawner.SpawnSplitNormalsFromElite(gameObject, splitCount, splitScatterRadius);
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
