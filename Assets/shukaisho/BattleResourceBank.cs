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
    [Min(0f)] public float growthAttackBonusPerSoul = 0.03f;

    [Header("Function")]
    [Min(0)] public int functionSoul = 0;
    [Range(0.1f, 1f)] public float functionCooldownMultiplierPerSoul = 0.97f;

    public event Action<SoulType, float> SoulApplied;
    public event Action FunctionSoulTriggered;
    public event Action<float, float> OnShieldChanged;

    private float skillDamageMultiplier = 1f;
    private float skillDamageBuffEndTime = -1f;

    public float ResolveConfiguredMaxHealth(float fallback = 100f)
    {
        CombatStats stats = GetComponent<CombatStats>();
        if (stats != null && stats.maxHealth > 0f)
        {
            return stats.maxHealth;
        }

        return maxHealth > 0f ? maxHealth : fallback;
    }

    public void SyncHealthFromCombatStats(bool refillCurrentHealth)
    {
        float previousMaxHealth = maxHealth;
        bool hadMatchingSerializedHealth = Mathf.Approximately(currentHealth, previousMaxHealth);
        float resolvedMaxHealth = ResolveConfiguredMaxHealth(previousMaxHealth > 0f ? previousMaxHealth : 100f);

        maxHealth = Mathf.Max(0f, resolvedMaxHealth);

        if (refillCurrentHealth || hadMatchingSerializedHealth || currentHealth <= 0f)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }
    }

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

    // Legacy soul multiplier hooks are intentionally neutralized.
    public float AttributeDamageMultiplier => 1f;
    public float SkillCooldownMultiplier => 1f;

    private void Awake()
    {
        SyncHealthFromCombatStats(refillCurrentHealth: false);
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
        ApplySoulWithFeedback(type, Mathf.RoundToInt(amount));
    }

    public string ApplySoulWithFeedback(SoulType type, int soulPoint)
    {
        soulPoint = Mathf.Clamp(soulPoint, 1, 5);
        float resolvedValue = ResolveSoulValue(type, soulPoint);
        string feedback;

        switch (type)
        {
            case SoulType.Life:
                ApplyLifeSoul(resolvedValue);
                feedback = $"Heal +{Mathf.CeilToInt(resolvedValue)}";
                break;
            case SoulType.Energy:
                ApplyEnergySoul(resolvedValue);
                feedback = $"MP +{Mathf.CeilToInt(resolvedValue)}";
                break;
            case SoulType.Growth:
                feedback = ApplyGrowthSoul(soulPoint);
                break;
            case SoulType.Function:
                ApplyFunctionSoul(resolvedValue);
                feedback = $"Shield +{Mathf.CeilToInt(resolvedValue)}";
                break;
            default:
                feedback = string.Empty;
                break;
        }

        SoulApplied?.Invoke(type, soulPoint);
        return feedback;
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

    public void AddShield(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
        {
            return;
        }

        shield += amount;
        maxShield = Mathf.Max(maxShield, shield);
        OnShieldChanged?.Invoke(shield, maxShield);
    }

    private void ApplyLifeSoul(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    private void ApplyEnergySoul(float amount)
    {
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
    }

    private void ApplyFunctionSoul(float amount)
    {
        AddShield(amount);
    }

    private float ResolveSoulValue(SoulType type, int soulPoint)
    {
        soulPoint = Mathf.Clamp(soulPoint, 1, 5);
        return type == SoulType.Growth ? soulPoint : soulPoint * 10f;
    }

    private string ApplyGrowthSoul(int soulPoint)
    {
        CombatStats stats = GetComponent<CombatStats>();
        CombatHealth combatHealth = GetComponent<CombatHealth>();
        int growthChoice = UnityEngine.Random.Range(0, 5);
        int growthAmount = Mathf.Clamp(soulPoint, 1, 5);
        float healthGrowth = growthAmount * 10f;

        if (stats == null)
        {
            if (growthChoice == 0)
            {
                maxHealth += healthGrowth;
                currentHealth = Mathf.Min(maxHealth, currentHealth + healthGrowth);
                if (combatHealth != null)
                {
                    combatHealth.currentHealth = currentHealth;
                }

                return $"HP +{Mathf.CeilToInt(healthGrowth)}";
            }

            return string.Empty;
        }

        switch (growthChoice)
        {
            case 0:
            {
                stats.maxHealth += healthGrowth;
                maxHealth = Mathf.Max(0f, stats.maxHealth);
                currentHealth = Mathf.Min(maxHealth, currentHealth + healthGrowth);
                if (combatHealth != null)
                {
                    combatHealth.stats = stats;
                    combatHealth.resourceBank = this;
                    combatHealth.currentHealth = currentHealth;
                }

                return $"HP +{Mathf.CeilToInt(healthGrowth)}";
            }
            case 1:
                stats.physicalAttack += growthAmount;
                return $"ATK +{growthAmount}";
            case 2:
                stats.physicalDefense += growthAmount;
                return $"DEF +{growthAmount}";
            case 3:
                stats.specialAttack += growthAmount;
                return $"MAG +{growthAmount}";
            default:
                stats.specialDefense += growthAmount;
                return $"RES +{growthAmount}";
        }
    }

    public float AbsorbDamage(float amount)
    {
        amount = Mathf.Max(0f, amount);
        float shieldUsed = Mathf.Min(shield, amount);
        shield -= shieldUsed;
        if (shieldUsed > 0f)
        {
            OnShieldChanged?.Invoke(shield, maxShield);
        }
        return amount - shieldUsed;
    }
}
