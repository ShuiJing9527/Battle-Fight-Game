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
    private bool dropped;

    private void OnEnable()
    {
        dropped = false;
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
        if (dropped)
        {
            return;
        }

        dropped = true;

        foreach (SoulDropEntry entry in drops)
        {
            if (entry.count <= 0)
            {
                continue;
            }

            for (int i = 0; i < entry.count; i++)
            {
                GameObject soulObject = new GameObject($"{entry.soulType} Soul");
                soulObject.transform.position = transform.position;
                soulObject.transform.localScale = Vector3.one * 0.35f;

                SphereCollider collider = soulObject.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.radius = 0.55f;

                Rigidbody rb = soulObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                SoulPickup soul = soulObject.AddComponent<SoulPickup>();
                soul.Configure(entry.soulType, 1);
                soulObject.SetActive(true);
            }
        }
    }
}
