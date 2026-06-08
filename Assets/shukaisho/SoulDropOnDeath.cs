using UnityEngine;

public class SoulDropOnDeath : MonoBehaviour
{
    [System.Serializable]
    public struct SoulDropEntry
    {
        public SoulType soulType;
        public SoulPickup prefab;
        [Min(0)] public int count;
    }

    [Header("击杀掉落")]
    public SoulDropEntry[] drops;

    private CombatHealth combatHealth;
    private EnemyHealth enemyHealth;

    private void OnEnable()
    {
        combatHealth = GetComponent<CombatHealth>();
        enemyHealth = GetComponent<EnemyHealth>();

        if (combatHealth != null)
        {
            combatHealth.Died += DropSouls;
        }

        if (enemyHealth != null)
        {
            enemyHealth.Died += DropSouls;
        }
    }

    private void OnDisable()
    {
        if (combatHealth != null)
        {
            combatHealth.Died -= DropSouls;
        }

        if (enemyHealth != null)
        {
            enemyHealth.Died -= DropSouls;
        }
    }

    private void DropSouls(GameObject killer)
    {
        foreach (SoulDropEntry entry in drops)
        {
            if (entry.prefab == null || entry.count <= 0)
            {
                continue;
            }

            for (int i = 0; i < entry.count; i++)
            {
                SoulPickup soul = Instantiate(entry.prefab, transform.position, Quaternion.identity);
                soul.soulType = entry.soulType;
            }
        }
    }
}
