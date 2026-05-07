using System;
using UnityEngine;

public class CombatHealth : MonoBehaviour
{
    [Header("生命")]
    public CombatStats stats;
    public BattleResourceBank resourceBank;
    [Min(0f)] public float currentHealth = 3f;
    public bool destroyOnDeath = true;

    public event Action<GameObject> Died;

    private bool dead;

    private float MaxHealth => stats != null ? stats.maxHealth : (resourceBank != null ? resourceBank.maxHealth : currentHealth);

    private void Awake()
    {
        if (stats == null)
        {
            stats = GetComponent<CombatStats>();
        }

        if (resourceBank == null)
        {
            resourceBank = GetComponent<BattleResourceBank>();
        }

        currentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(new BattleDamage(amount, BattleDamageType.Physical, null));
    }

    public void TakeDamage(BattleDamage damage)
    {
        if (dead)
        {
            return;
        }

        float finalDamage = stats != null ? stats.ReduceDamage(damage) : Mathf.Max(0f, damage.amount);
        if (resourceBank != null)
        {
            finalDamage = resourceBank.AbsorbDamage(finalDamage);
            resourceBank.currentHealth = Mathf.Max(0f, resourceBank.currentHealth - finalDamage);
            currentHealth = resourceBank.currentHealth;
        }
        else
        {
            currentHealth = Mathf.Max(0f, currentHealth - finalDamage);
        }

        if (currentHealth <= 0f)
        {
            Die(damage.source);
        }
    }

    public void Heal(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (resourceBank != null)
        {
            resourceBank.Heal(amount);
            currentHealth = resourceBank.currentHealth;
        }
        else
        {
            currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
        }
    }

    private void Die(GameObject killer)
    {
        if (dead)
        {
            return;
        }

        dead = true;
        Died?.Invoke(killer);

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }
}
