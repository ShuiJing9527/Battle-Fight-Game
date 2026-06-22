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
    [Header("Skill config: Q(0) W(1) E(2) R(3)")]
    public SkillCostCDData[] skillDatas = new SkillCostCDData[4]
    {
        new SkillCostCDData { maxCooldown = 2f, manaCost = 10f },
        new SkillCostCDData { maxCooldown = 6f, manaCost = 30f },
        new SkillCostCDData { maxCooldown = 4f, manaCost = 20f },
        new SkillCostCDData { maxCooldown = 12f, manaCost = 60f }
    };

    [Header("Mana")]
    public float maxMana = 100f;
    public float manaRecoverPerSecond = 2f;
    public BattleResourceBank resourceBank;

    private float[] runtimeCurrentCD;
    private float runtimeCurrentMana;

    private void Awake()
    {
        if (resourceBank == null)
        {
            resourceBank = GetComponent<BattleResourceBank>();
        }

        runtimeCurrentCD = new float[4];

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
    }

    public bool IsSkillCastable(int skillIndex)
    {
        if (!IsValidSkillIndex(skillIndex))
        {
            return false;
        }

        SkillCostCDData data = skillDatas[skillIndex];
        float currentMana = resourceBank != null ? resourceBank.currentEnergy : runtimeCurrentMana;
        return runtimeCurrentCD[skillIndex] <= 0.01f && currentMana >= data.manaCost;
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
        if (runtimeCurrentCD == null || runtimeCurrentCD.Length != 4)
        {
            runtimeCurrentCD = new float[4];
        }

        if (skillDatas == null || skillDatas.Length != 4)
        {
            skillDatas = new SkillCostCDData[4]
            {
                new SkillCostCDData { maxCooldown = 2f, manaCost = 10f },
                new SkillCostCDData { maxCooldown = 6f, manaCost = 30f },
                new SkillCostCDData { maxCooldown = 4f, manaCost = 20f },
                new SkillCostCDData { maxCooldown = 12f, manaCost = 60f }
            };
        }
    }

    private float ResolveCooldownMultiplier()
    {
        return resourceBank != null ? resourceBank.SkillCooldownMultiplier : 1f;
    }
}
