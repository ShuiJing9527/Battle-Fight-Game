using UnityEngine;

public class BloodExplosionDrop : MonoBehaviour
{
    private SoulPickup lifeSoulPrefab;
    private CombatHealth health;

    public void Set(SoulPickup lifeSoulPrefab)
    {
        this.lifeSoulPrefab = lifeSoulPrefab;

        if (health == null)
        {
            health = GetComponent<CombatHealth>();
            if (health != null)
            {
                health.Died += OnTargetDied;
            }
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.Died -= OnTargetDied;
        }
    }

    private void OnTargetDied(GameObject killer)
    {
        if (lifeSoulPrefab != null)
        {
            Instantiate(lifeSoulPrefab, transform.position, Quaternion.identity);
        }
    }
}
