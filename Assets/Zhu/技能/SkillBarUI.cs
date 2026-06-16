using UnityEngine;

public class SkillBarUI : MonoBehaviour
{
    public SkillDataReader dataReader;
    public SkillSlotUI[] skillSlots;

    [Header("刷新间隔（秒）")]
    public float refreshInterval = 0.1f;

    private float _timer;

    void OnEnable()
    {
        _timer = 0f;
    }

    void Update()
    {
        // 定时刷新，不是每帧都跑
        _timer += Time.deltaTime;
        if (_timer < refreshInterval) return;
        _timer = 0f;

        // 全判空，缺任何东西都直接跳过，绝不报错
        if (dataReader == null || skillSlots == null || skillSlots.Length == 0)
            return;

        for (int i = 0; i < skillSlots.Length && i < 4; i++)
        {
            if (skillSlots[i] == null) continue;
            skillSlots[i].Refresh(dataReader.GetSkillStatus(i));
        }
    }
}