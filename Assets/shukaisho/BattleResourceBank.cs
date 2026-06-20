using System;
using UnityEngine;

public class BattleResourceBank : MonoBehaviour
{
    [Header("Health")]
    [Min(0f)] public float maxHealth = 3f;
    [Min(0f)] public float currentHealth = 3f;
    [Min(0f)] public float shield = 0f;
    [Min(0f)] public float maxShield = 0f;

    [Header("Energy")]
    [Min(0f)] public float maxEnergy = 100f;
    [Min(0f)] public float currentEnergy = 0f;
    [Min(0f)] public float energyOverflowDamageBonusPerPoint = 0f;
    [Min(0f)] public float energyOverflowBuffSeconds = 0f;

    [Header("Growth")]
    [Min(0)] public int growthSoul = 0;

    [Header("Function")]
    [Min(0)] public int functionSoul = 0;

    public event Action<SoulType, float> SoulApplied;
    public event Action FunctionSoulTriggered;
    public event Action<float, float> OnShieldChanged;

    private float skillDamageMultiplier = 1f;
    private float skillDamageBuffEndTime = -1f;

    public float SkillDamageMultiplier
    {
        get
        {
            if (skillDamageBuffEndTime >= 0f && Time.time > skillDamageBuffEndTime)
            {
                skillDamageMultiplier = 1f;
                skillDamageBuffEndTime = -1f;
            }

            return skillDamageMultiplier;
        }
    }

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);
    }

    public bool TrySpendEnergy(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (currentEnergy < amount)
        {
            return false;
        }

        currentEnergy -= amount;
        return true;
    }

    public bool TrySpendHealth(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (currentHealth <= amount)
        {
            return false;
        }

        currentHealth -= amount;
        return true;
    }

    public void ApplySoul(SoulType type, float amount)
    {
        amount = Mathf.Max(0f, amount);

        switch (type)
        {
            case SoulType.Life:
                ApplyLifeSoul(amount);
                break;
            case SoulType.Energy:
                ApplyEnergySoul(amount);
                break;
            case SoulType.Growth:
                growthSoul += Mathf.RoundToInt(amount);
                break;
            case SoulType.Function:
                functionSoul += Mathf.RoundToInt(amount);
                FunctionSoulTriggered?.Invoke();
                break;
        }

        SoulApplied?.Invoke(type, amount);
    }

    public void Heal(float amount)
    {
        ApplyLifeSoul(amount);
    }

    public void SetShield(float amount)
    {
        shield = Mathf.Max(0f, amount);
        maxShield = shield;
        OnShieldChanged?.Invoke(shield, maxShield);
    }

    public void SetShieldCurrent(float amount)
    {
        shield = Mathf.Max(0f, amount);
        OnShieldChanged?.Invoke(shield, maxShield);
    }

    public void ClearShield()
    {
        shield = 0f;
        maxShield = 0f;
        OnShieldChanged?.Invoke(shield, maxShield);
    }

    public float CurrentShield => shield;
    public float MaxShield => maxShield;
    public bool HasShield => shield > 0f;

    public float GetCurrentShield()
    {
        return shield;
    }

    public float GetMaxShield()
    {
        return maxShield;
    }

    public bool HasActiveShield()
    {
        return shield > 0f;
    }

    private void ApplyLifeSoul(float amount)
    {
        float before = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        float used = currentHealth - before;
        float overflow = amount - used;

        if (overflow > 0f)
        {
            shield += overflow;
            maxShield = Mathf.Max(maxShield, shield);
            OnShieldChanged?.Invoke(shield, maxShield);
        }
    }

    private void ApplyEnergySoul(float amount)
    {
        float before = currentEnergy;
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
        float used = currentEnergy - before;
        float overflow = amount - used;

        if (overflow > 0f && energyOverflowDamageBonusPerPoint > 0f && energyOverflowBuffSeconds > 0f)
        {
            skillDamageMultiplier = 1f + overflow * energyOverflowDamageBonusPerPoint;
            skillDamageBuffEndTime = Time.time + energyOverflowBuffSeconds;
        }
    }

    public float AbsorbDamage(float amount)
    {
        amount = Mathf.Max(0f, amount);
        float shieldUsed = Mathf.Min(shield, amount);
        shield -= shieldUsed;
        if (shieldUsed > 0f)
        {
            Debug.Log($"[Shield] absorbed={shieldUsed:F2}, remaining={shield:F2}, incoming={amount:F2}", this);
            OnShieldChanged?.Invoke(shield, maxShield);
        }
        return amount - shieldUsed;
    }
}
