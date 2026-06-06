using System;
using UnityEngine;

public class BattleResourceBank : MonoBehaviour
{
    [Header("生命魂")]
    [Min(0f)] public float maxHealth = 3f;
    [Min(0f)] public float currentHealth = 3f;
    [Min(0f)] public float shield = 0f;

    [Header("能量魂")]
    [Min(0f)] public float maxEnergy = 100f;
    [Min(0f)] public float currentEnergy = 0f;
    [Min(0f)] public float energyOverflowDamageBonusPerPoint = 0f;
    [Min(0f)] public float energyOverflowBuffSeconds = 0f;

    [Header("成长魂")]
    [Min(0)] public int growthSoul = 0;

    [Header("功能魂")]
    [Min(0)] public int functionSoul = 0;

    public event Action<SoulType, float> SoulApplied;
    public event Action FunctionSoulTriggered;

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

    private void ApplyLifeSoul(float amount)
    {
        float before = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        float used = currentHealth - before;
        float overflow = amount - used;

        if (overflow > 0f)
        {
            shield += overflow;
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
        return amount - shieldUsed;
    }
}
