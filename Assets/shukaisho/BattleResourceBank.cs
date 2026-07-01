using System;
using UnityEngine;

public class BattleResourceBank : MonoBehaviour
{
    private const float SpeedGrowthSoulRedirectChance = 0.5f;
    private const float SpeedGrowthSoulConsumableRedirectChance = 0.5f;

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

    [Header("Debug")]
    [SerializeField] private bool debugGrowthSoulRollLog = false;

    public event Action<SoulType, float> SoulApplied;
    public event Action FunctionSoulTriggered;
    public event Action<float, float> OnShieldChanged;

    private float skillDamageMultiplier = 1f;
    private float skillDamageBuffEndTime = -1f;
    private RuneRuntimeState runeRuntimeState;

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
        runeRuntimeState = GetComponent<RuneRuntimeState>();
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
        return ApplySoulWithFeedbackInternal(type, soulPoint, allowLuckyCopy: true);
    }

    private string ApplySoulWithFeedbackInternal(SoulType type, int soulPoint, bool allowLuckyCopy)
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
        runeRuntimeState?.NotifySoulApplied(type, soulPoint);

        if (allowLuckyCopy)
        {
            RuneRuntimeState runtimeState = ResolveRuneRuntimeState();
            int copyCount = runtimeState != null ? runtimeState.GetSoulPickupCopyCount() : 0;
            if (copyCount > 0)
            {
                int copyPoint = runtimeState.GetSoulPickupCopyPoint();
                for (int i = 0; i < copyCount; i++)
                {
                    ApplySoulWithFeedbackInternal(type, copyPoint, allowLuckyCopy: false);
                }
            }
        }

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

        RuneRuntimeState runtimeState = ResolveRuneRuntimeState();
        float multiplier = runtimeState != null ? runtimeState.GetShieldGainMultiplier() : 1f;
        shield += amount * Mathf.Max(0f, multiplier);
        maxShield = Mathf.Max(maxShield, shield);
        OnShieldChanged?.Invoke(shield, maxShield);
    }

    private void ApplyLifeSoul(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    private void ApplyEnergySoul(float amount)
    {
        float previousEnergy = currentEnergy;
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
        float overflow = Mathf.Max(0f, amount - (currentEnergy - previousEnergy));
        if (overflow > 0f)
        {
            ResolveRuneRuntimeState()?.AddManaOverflow(overflow);
        }
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
        int growthChoice = UnityEngine.Random.Range(0, 6);
        int finalChoice = growthChoice;
        bool redirected = false;
        bool redirectedToConsumable = false;
        if (growthChoice == 5 && UnityEngine.Random.value < SpeedGrowthSoulRedirectChance)
        {
            finalChoice = UnityEngine.Random.Range(0, 5);
            redirected = true;
        }

        int growthAmount = Mathf.Clamp(soulPoint, 1, 5);
        float healthGrowth = growthAmount * 10f;

        if (stats == null)
        {
            if (finalChoice != 0)
            {
                finalChoice = 0;
            }

            if (finalChoice == 0)
            {
                maxHealth += healthGrowth;
                currentHealth = Mathf.Min(maxHealth, currentHealth + healthGrowth);
                if (combatHealth != null)
                {
                    combatHealth.currentHealth = currentHealth;
                }

                LogGrowthSoulRoll(growthChoice, finalChoice, redirected, redirectedToConsumable, null);
                return $"HP +{Mathf.CeilToInt(healthGrowth)}";
            }

            LogGrowthSoulRoll(growthChoice, finalChoice, redirected, redirectedToConsumable, null);
            return string.Empty;
        }

        if (growthChoice == 5 && !redirected && UnityEngine.Random.value < SpeedGrowthSoulConsumableRedirectChance)
        {
            redirectedToConsumable = true;
            SoulType consumableType = ResolveRandomConsumableSoulType();
            float consumableValue = ResolveSoulValue(consumableType, soulPoint);
            string consumableFeedback = ApplyConsumableSoul(consumableType, consumableValue);
            LogGrowthSoulRoll(growthChoice, finalChoice, redirected, redirectedToConsumable, consumableType);
            return consumableFeedback;
        }

        string result = ApplyGrowthSoulResult(stats, combatHealth, finalChoice, growthAmount, healthGrowth);
        LogGrowthSoulRoll(growthChoice, finalChoice, redirected, redirectedToConsumable, null);
        return result;
    }

    private string ApplyGrowthSoulResult(CombatStats stats, CombatHealth combatHealth, int growthChoice, int growthAmount, float healthGrowth)
    {
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
            case 4:
                stats.specialDefense += growthAmount;
                return $"RES +{growthAmount}";
            default:
                stats.speed += growthAmount;
                return $"SPD +{growthAmount}";
        }
    }

    private string ApplyConsumableSoul(SoulType soulType, float amount)
    {
        switch (soulType)
        {
            case SoulType.Life:
                ApplyLifeSoul(amount);
                return $"HP +{Mathf.CeilToInt(amount)}";
            case SoulType.Function:
                ApplyFunctionSoul(amount);
                return $"Shield +{Mathf.CeilToInt(amount)}";
            case SoulType.Energy:
            default:
                ApplyEnergySoul(amount);
                return $"MP +{Mathf.CeilToInt(amount)}";
        }
    }

    private static SoulType ResolveRandomConsumableSoulType()
    {
        int roll = UnityEngine.Random.Range(0, 3);
        return roll switch
        {
            0 => SoulType.Life,
            1 => SoulType.Function,
            _ => SoulType.Energy
        };
    }

    private void LogGrowthSoulRoll(int originalChoice, int finalChoice, bool redirected, bool redirectedToConsumable, SoulType? consumableSoulType)
    {
        if (!debugGrowthSoulRollLog)
        {
            return;
        }

        Debug.Log(
            $"[GrowthSoulRoll] original={GetGrowthSoulLabel(originalChoice)} redirected={redirected} redirectedToConsumable={redirectedToConsumable} final={GetGrowthSoulLabel(finalChoice)} consumable={(consumableSoulType.HasValue ? consumableSoulType.Value.ToString() : "None")} redirectChance={SpeedGrowthSoulRedirectChance:F1} consumableRedirectChance={SpeedGrowthSoulConsumableRedirectChance:F1}",
            this);
    }

    private static string GetGrowthSoulLabel(int growthChoice)
    {
        return growthChoice switch
        {
            0 => "HP",
            1 => "PhysicalAttack",
            2 => "PhysicalDefense",
            3 => "SpecialAttack",
            4 => "SpecialDefense",
            5 => "SPD",
            _ => $"Unknown({growthChoice})"
        };
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

    private RuneRuntimeState ResolveRuneRuntimeState()
    {
        if (runeRuntimeState == null)
        {
            runeRuntimeState = GetComponent<RuneRuntimeState>();
        }

        return runeRuntimeState;
    }
}
