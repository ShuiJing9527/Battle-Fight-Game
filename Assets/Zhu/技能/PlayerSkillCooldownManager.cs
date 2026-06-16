using UnityEngine;

[System.Serializable]
public struct SkillCostCDData
{
    [Tooltip("技能最大冷却 Q/W/E/R 顺序 0~3")]
    public float maxCooldown;
    [Tooltip("技能蓝耗")]
    public float manaCost;
}

public class PlayerSkillCooldownManager : MonoBehaviour
{
    [Header("技能配置 Q(0) W(1) E(2) R(3)")]
    public SkillCostCDData[] skillDatas = new SkillCostCDData[4]
    {
        new SkillCostCDData(){maxCooldown = 3f, manaCost = 10f},
        new SkillCostCDData(){maxCooldown = 8f, manaCost = 30f},
        new SkillCostCDData(){maxCooldown = 5f, manaCost = 20f},
        new SkillCostCDData(){maxCooldown = 20f, manaCost = 50f}
    };

    [Header("全局蓝量设置")]
    public float maxMana = 100f;
    public float manaRecoverPerSecond = 2f;

    // 运行时私有状态（不会在面板暴露干扰修改）
    private float[] _runtimeCurrentCD;
    private float _runtimeCurrentMana;

    private void Awake()
    {
        // 初始化冷却数组，长度固定4个技能
        _runtimeCurrentCD = new float[4];
        _runtimeCurrentMana = maxMana;
    }

    /// <summary>每帧更新冷却倒计时与自动回蓝，在控制器Update调用</summary>
    public void TickCooldownAndMana(float deltaTime)
    {
        // 冷却倒计时
        for (int i = 0; i < 4; i++)
        {
            if (_runtimeCurrentCD[i] > 0f)
                _runtimeCurrentCD[i] -= deltaTime;
        }

        // 自动回蓝，实时读取面板maxMana，改面板立刻生效
        if (_runtimeCurrentMana < maxMana)
        {
            _runtimeCurrentMana = Mathf.Min(maxMana, _runtimeCurrentMana + manaRecoverPerSecond * deltaTime);
        }
    }

    /// <summary>判断技能是否可释放</summary>
    public bool IsSkillCastable(int skillIndex)
    {
        if (skillIndex < 0 || skillIndex >= 4) return false;
        SkillCostCDData data = skillDatas[skillIndex];
        // 冷却完成 + 蓝量足够
        return _runtimeCurrentCD[skillIndex] <= 0.01f && _runtimeCurrentMana >= data.manaCost;
    }

    /// <summary>释放技能：扣除蓝量、刷新冷却</summary>
    public void ConsumeSkillResource(int skillIndex)
    {
        if (skillIndex < 0 || skillIndex >= 4) return;
        SkillCostCDData data = skillDatas[skillIndex];
        _runtimeCurrentMana -= data.manaCost;
        // 实时读取面板当前maxCD，修改面板数值下一次释放直接生效
        _runtimeCurrentCD[skillIndex] = data.maxCooldown;
    }

    #region 外部只读接口（给UI/控制器读取）
    public float GetCurrentSkillCD(int idx)
    {
        if (idx < 0 || idx >= 4) return 0f;
        return Mathf.Max(0f, _runtimeCurrentCD[idx]);
    }

    public float GetSkillMaxCD(int idx)
    {
        if (idx < 0 || idx >= 4) return 0f;
        return skillDatas[idx].maxCooldown;
    }

    public float GetSkillManaCost(int idx)
    {
        if (idx < 0 || idx >= 4) return 0f;
        return skillDatas[idx].manaCost;
    }

    public float GetCurrentMana() => _runtimeCurrentMana;
    public float GetMaxMana() => maxMana;
    public bool IsSkillReady(int idx) => IsSkillCastable(idx);
    #endregion
}