using UnityEngine;

public class SkillBarUI : MonoBehaviour
{
    public SkillDataReader dataReader;
    public SkillSlotUI[] skillSlots;

    [Header("Refresh Interval (Seconds)")]
    public float refreshInterval = 0.1f;

    private float _timer;

    void OnEnable()
    {
        _timer = 0f;
    }

    void Update()
    {
        // Refresh on a timer instead of every frame.
        _timer += Time.deltaTime;
        if (_timer < refreshInterval) return;
        _timer = 0f;

        // Guard against missing references and skip safely.
        if (dataReader == null || skillSlots == null || skillSlots.Length == 0)
            return;

        for (int i = 0; i < skillSlots.Length && i < 4; i++)
        {
            if (skillSlots[i] == null) continue;
            skillSlots[i].Refresh(dataReader.GetSkillStatus(i));
        }
    }
}
