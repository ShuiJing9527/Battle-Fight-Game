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

    private static SoulPickup cachedDefaultPrefab;

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
                SoulPickup prefab = entry.prefab != null ? entry.prefab : GetDefaultSoulPrefab();
                if (prefab == null)
                {
                    continue;
                }

                SoulPickup soul = Instantiate(prefab, transform.position, Quaternion.identity);
                soul.Configure(entry.soulType, soul.amount);
                Debug.Log($"[SoulOrb] spawned prefab={soul.name}", soul);
            }
        }
    }

    private static SoulPickup GetDefaultSoulPrefab()
    {
        if (cachedDefaultPrefab != null)
        {
            return cachedDefaultPrefab;
        }

        cachedDefaultPrefab = Resources.Load<SoulPickup>("Prefabs/Drop/SoulOrb")
                            ?? Resources.Load<SoulPickup>("Prefabs/Drop/SoulOrbPreview");
        return cachedDefaultPrefab;
    }
}
