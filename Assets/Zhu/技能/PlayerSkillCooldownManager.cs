using UnityEngine;

[System.Serializable]
public struct SkillCostCDData
{
    [Tooltip("Q/W/E/R max cooldown in seconds")]
    public float maxCooldown;
    [Tooltip("Mana cost")]
    public float manaCost;
}

public class PlayerSkillCooldownManager : MonoBehaviour
{
    private const int SkillCount = 4;
    private const float ReadyCooldownThreshold = 0.01f;
    private const float DefaultQCooldown = 2f;
    private const float DefaultWCooldown = 6f;
    private const float DefaultECooldown = 4f;
    private const float DefaultRCooldown = 12f;
    private const float DefaultQManaCost = 10f;
    private const float DefaultWManaCost = 30f;
    private const float DefaultEManaCost = 20f;
    private const float DefaultRManaCost = 60f;

    [Header("Skill config: Q(0) W(1) E(2) R(3)")]
    public SkillCostCDData[] skillDatas = new SkillCostCDData[SkillCount]
    {
        new SkillCostCDData { maxCooldown = DefaultQCooldown, manaCost = DefaultQManaCost },
        new SkillCostCDData { maxCooldown = DefaultWCooldown, manaCost = DefaultWManaCost },
        new SkillCostCDData { maxCooldown = DefaultECooldown, manaCost = DefaultEManaCost },
        new SkillCostCDData { maxCooldown = DefaultRCooldown, manaCost = DefaultRManaCost }
    };

    [Header("Mana")]
    public float maxMana = 100f;
    public float manaRecoverPerSecond = 5f;
    public BattleResourceBank resourceBank;

    [Header("Debug")]
    [SerializeField] private bool debugManaRegen = false;
    [SerializeField, Min(0.1f)] private float debugManaRegenInterval = 1f;

    private float[] runtimeCurrentCD;
    private float runtimeCurrentMana;
    private float nextDebugManaLogTime;
    private CombatStats combatStats;

    private void Awake()
    {
        if (resourceBank == null)
        {
            resourceBank = GetComponent<BattleResourceBank>();
        }

        combatStats = GetComponent<CombatStats>();
        runtimeCurrentCD = new float[SkillCount];

        if (resourceBank != null)
        {
            resourceBank.maxEnergy = Mathf.Max(resourceBank.maxEnergy, maxMana);
            if (resourceBank.currentEnergy <= 0f)
            {
                resourceBank.currentEnergy = resourceBank.maxEnergy;
            }

            runtimeCurrentMana = resourceBank.currentEnergy;
        }
        else
        {
            runtimeCurrentMana = maxMana;
        }
    }

    private void Update()
    {
        TickCooldownAndMana(Time.deltaTime);
    }

    public void TickCooldownAndMana(float deltaTime)
    {
        EnsureRuntimeArrays();

        // Cooldown and mana are advanced together so the HUD can read a consistent state.
        for (int i = 0; i < runtimeCurrentCD.Length; i++)
        {
            runtimeCurrentCD[i] = Mathf.Max(0f, runtimeCurrentCD[i] - deltaTime);
        }

        if (resourceBank != null)
        {
            resourceBank.maxEnergy = Mathf.Max(resourceBank.maxEnergy, maxMana);
            resourceBank.currentEnergy = Mathf.Min(resourceBank.maxEnergy, resourceBank.currentEnergy + manaRecoverPerSecond * deltaTime);
            runtimeCurrentMana = resourceBank.currentEnergy;
        }
        else
        {
            runtimeCurrentMana = Mathf.Min(maxMana, runtimeCurrentMana + manaRecoverPerSecond * deltaTime);
        }

        if (debugManaRegen && Time.time >= nextDebugManaLogTime)
        {
            nextDebugManaLogTime = Time.time + Mathf.Max(0.1f, debugManaRegenInterval);
            float currentMana = GetCurrentMana();
            float maxCurrentMana = GetMaxMana();
            Debug.Log($"[Player MP Regen] regenPerSecond={manaRecoverPerSecond:F2}", this);
            Debug.Log($"[Player MP Regen] currentMP={currentMana:F2} / maxMP={maxCurrentMana:F2}", this);
        }
    }

    public bool IsSkillCastable(int skillIndex)
    {
        if (!IsValidSkillIndex(skillIndex))
        {
            return false;
        }

        SkillCostCDData data = skillDatas[skillIndex];
        float currentMana = resourceBank != null ? resourceBank.currentEnergy : runtimeCurrentMana;
        return runtimeCurrentCD[skillIndex] <= ReadyCooldownThreshold && currentMana >= data.manaCost;
    }

    public void ConsumeSkillResource(int skillIndex)
    {
        TryConsumeSkillResource(skillIndex);
    }

    public bool TryConsumeSkillResource(int skillIndex)
    {
        if (!IsValidSkillIndex(skillIndex) || !IsSkillCastable(skillIndex))
        {
            return false;
        }

        SkillCostCDData data = skillDatas[skillIndex];
        if (resourceBank != null)
        {
            if (!resourceBank.TrySpendEnergy(data.manaCost))
            {
                return false;
            }

            runtimeCurrentMana = resourceBank.currentEnergy;
        }
        else
        {
            runtimeCurrentMana -= data.manaCost;
        }

        runtimeCurrentCD[skillIndex] = Mathf.Max(0f, data.maxCooldown * ResolveCooldownMultiplier());
        return true;
    }

    public float GetCurrentSkillCD(int idx)
    {
        if (!IsValidSkillIndex(idx))
        {
            return 0f;
        }

        return Mathf.Max(0f, runtimeCurrentCD[idx]);
    }

    public float GetSkillMaxCD(int idx)
    {
        if (idx < 0 || skillDatas == null || idx >= skillDatas.Length)
        {
            return 0f;
        }

        return skillDatas[idx].maxCooldown * ResolveCooldownMultiplier();
    }

    public float GetSkillManaCost(int idx)
    {
        if (idx < 0 || skillDatas == null || idx >= skillDatas.Length)
        {
            return 0f;
        }

        return skillDatas[idx].manaCost;
    }

    public float GetCurrentMana()
    {
        return resourceBank != null ? resourceBank.currentEnergy : runtimeCurrentMana;
    }

    public float GetMaxMana()
    {
        return resourceBank != null ? resourceBank.maxEnergy : maxMana;
    }

    public bool IsSkillReady(int idx)
    {
        return IsSkillCastable(idx);
    }

    private bool IsValidSkillIndex(int idx)
    {
        EnsureRuntimeArrays();
        return idx >= 0 && skillDatas != null && idx < skillDatas.Length && idx < runtimeCurrentCD.Length;
    }

    private void EnsureRuntimeArrays()
    {
        if (runtimeCurrentCD == null || runtimeCurrentCD.Length != SkillCount)
        {
            runtimeCurrentCD = new float[SkillCount];
        }

        if (skillDatas == null || skillDatas.Length != SkillCount)
        {
            skillDatas = new SkillCostCDData[SkillCount]
            {
                new SkillCostCDData { maxCooldown = DefaultQCooldown, manaCost = DefaultQManaCost },
                new SkillCostCDData { maxCooldown = DefaultWCooldown, manaCost = DefaultWManaCost },
                new SkillCostCDData { maxCooldown = DefaultECooldown, manaCost = DefaultEManaCost },
                new SkillCostCDData { maxCooldown = DefaultRCooldown, manaCost = DefaultRManaCost }
            };
        }
    }

    private float ResolveCooldownMultiplier()
    {
        return BattleStatUtility.GetCooldownMultiplier(combatStats);
    }
}
