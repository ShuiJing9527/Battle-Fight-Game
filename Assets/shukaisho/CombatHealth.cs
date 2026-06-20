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
    public event Action<float, float> OnShieldChanged;

    private bool dead;
    private float localShield;
    private float localMaxShield;

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

        if (resourceBank != null)
        {
            resourceBank.OnShieldChanged += HandleResourceBankOnShieldChanged;
        }

        currentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);
        localShield = Mathf.Max(0f, localShield);
        localMaxShield = Mathf.Max(0f, localMaxShield);
    }

    private void OnDestroy()
    {
        if (resourceBank != null)
        {
            resourceBank.OnShieldChanged -= HandleResourceBankOnShieldChanged;
        }
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
        finalDamage = AbsorbShieldDamage(finalDamage);
        Player2PrototypeController player2 = GetComponent<Player2PrototypeController>();
        if (player2 != null)
        {
            finalDamage = player2.ProcessIncomingDamageWithWGuard(finalDamage, damage);
        }

        if (resourceBank != null)
        {
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

    public void SetShield(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (resourceBank != null)
        {
            resourceBank.SetShield(amount);
            return;
        }

        localShield = amount;
        localMaxShield = amount;
        OnShieldChanged?.Invoke(localShield, localMaxShield);
    }

    public void ClearShield()
    {
        if (resourceBank != null)
        {
            resourceBank.ClearShield();
            return;
        }

        localShield = 0f;
        localMaxShield = 0f;
        OnShieldChanged?.Invoke(localShield, localMaxShield);
    }

    public float GetShield()
    {
        return resourceBank != null ? resourceBank.CurrentShield : localShield;
    }

    public float GetMaxShield()
    {
        if (resourceBank != null)
        {
            return resourceBank.MaxShield;
        }

        return localMaxShield;
    }

    public bool HasActiveShield()
    {
        return GetShield() > 0f;
    }

    public float CurrentShield => GetShield();
    public float MaxShield => GetMaxShield();
    public bool HasShield => HasActiveShield();

    public float GetCurrentShield()
    {
        return GetShield();
    }

    private float AbsorbShieldDamage(float amount)
    {
        amount = Mathf.Max(0f, amount);
        float shieldUsed = Mathf.Min(GetShield(), amount);
        if (shieldUsed <= 0f)
        {
            return amount;
        }

        float remainingShield = GetShield() - shieldUsed;
        if (resourceBank != null)
        {
            resourceBank.SetShieldCurrent(remainingShield);
        }
        else
        {
            localShield = remainingShield;
            localMaxShield = Mathf.Max(localMaxShield, localShield);
            OnShieldChanged?.Invoke(localShield, localMaxShield);
        }

        Debug.Log($"[Shield] absorbed={shieldUsed:F2}, remaining={remainingShield:F2}, incoming={amount:F2}", this);
        return amount - shieldUsed;
    }

    private void HandleResourceBankOnShieldChanged(float currentShield, float maxShield)
    {
        OnShieldChanged?.Invoke(currentShield, maxShield);
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
