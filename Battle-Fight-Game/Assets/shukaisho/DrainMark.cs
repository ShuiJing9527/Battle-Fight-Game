using UnityEngine;

public class DrainMark : MonoBehaviour
{
    private GameObject owner;
    private float healAmount;
    private CombatHealth health;

    public void Set(GameObject owner, float healAmount)
    {
        this.owner = owner;
        this.healAmount = healAmount;

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
        if (owner == null || killer != owner)
        {
            return;
        }

        BattleResourceBank bank = owner.GetComponent<BattleResourceBank>();
        if (bank != null)
        {
            bank.Heal(healAmount);
        }
    }
}
