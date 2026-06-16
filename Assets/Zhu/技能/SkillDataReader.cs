using UnityEngine;

public struct SkillStatus
{
    public float currentCD;
    public float maxCD;
    public float manaCost;
    public bool isReady;
}

public class SkillDataReader : MonoBehaviour
{
    [Header("对接原有技能脚本")]
    public MonoBehaviour playerSkillScript;

    [Header("测试模式开关")]
    [Tooltip("开启后用模拟数据，不用对接原有代码也能测UI")]
    public bool useMockData = true;

    // 4个技能配置：0=Q 1=W 2=E 3=R
    // 严格遵循规则：蓝耗越低CD越短，蓝耗越高CD越长
    private readonly float[] _mockMaxCD = { 3f, 8f, 5f, 20f };
    private readonly float[] _mockManaCost = { 10f, 30f, 20f, 50f };
    private float[] _mockCurrentCD;
    private readonly KeyCode[] _skillKeys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R };

    void Awake()
    {
        // 初始化每个技能独立的冷却计时器
        _mockCurrentCD = new float[4];
        for (int i = 0; i < 4; i++) _mockCurrentCD[i] = 0f;
    }

    void Update()
    {
        if (!useMockData) return;

        // 每个技能独立倒计时
        for (int i = 0; i < 4; i++)
        {
            if (_mockCurrentCD[i] > 0f)
                _mockCurrentCD[i] -= Time.deltaTime;
        }

        // 检测按键，触发对应技能进入冷却
        for (int i = 0; i < 4; i++)
        {
            if (Input.GetKeyDown(_skillKeys[i]) && _mockCurrentCD[i] <= 0.01f)
            {
                _mockCurrentCD[i] = _mockMaxCD[i];
            }
        }
    }

    // ========== 对接区域：仅修改这里，其他代码不动 ==========
    private float GetRealCurrentCD(int index)
    {
        // 示例：打开注释，替换成你们项目真实的冷却字段名
        // if (playerSkillScript == null) return 0;
        // PlayerSkill skill = playerSkillScript as PlayerSkill;
        // if (skill == null) return 0;
        // switch(index)
        // {
        //     case 0: return skill.skillQ.cdTimer;
        //     case 1: return skill.skillW.cdTimer;
        //     case 2: return skill.skillE.cdTimer;
        //     case 3: return skill.skillR.cdTimer;
        //     default: return 0;
        // }
        return 0;
    }

    private float GetRealMaxCD(int index)
    {
        // 替换成你们每个技能的总冷却时长
        return 0;
    }

    private float GetRealManaCost(int index)
    {
        // 替换成你们每个技能的蓝耗
        return 0;
    }

    private float GetCurrentPlayerMana()
    {
        // 替换成角色当前蓝量
        return 100f;
    }
    // ======================================================

    // UI唯一调用接口，全安全防护
    public SkillStatus GetSkillStatus(int index)
    {
        SkillStatus status = new SkillStatus();

        // 模拟模式：返回独立计时数据
        if (useMockData)
        {
            if (index < 0 || index >= 4) return status;
            status.maxCD = _mockMaxCD[index];
            status.manaCost = _mockManaCost[index];
            status.currentCD = Mathf.Max(0, _mockCurrentCD[index]);
            status.isReady = status.currentCD <= 0.01f;
            return status;
        }

        // 真实模式：异常捕获，绝不报错卡游戏
        try
        {
            status.maxCD = GetRealMaxCD(index);
            status.manaCost = GetRealManaCost(index);
            status.currentCD = Mathf.Max(0, GetRealCurrentCD(index));
            float playerMana = GetCurrentPlayerMana();
            status.isReady = status.currentCD <= 0.01f && playerMana >= status.manaCost;
        }
        catch
        {
            status.isReady = true;
        }

        return status;
    }
}