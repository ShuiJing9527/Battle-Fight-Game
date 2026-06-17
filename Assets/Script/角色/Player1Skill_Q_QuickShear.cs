using UnityEngine;

public class Player1Skill_Q_QuickShear : Player01SkillBase
{
    private void Reset()
    {
        cooldown = 0.7f;
        duration = 0.42f;
        effectPower = 1.2f;
        animationName = "ATK2";
        debugLog = true;
    }

    protected override void OnCastStarted()
    {
        if (debugLog)
        {
            Debug.Log($"[Q - 快刀剪乱] QuickShear executed. effectPower={effectPower:F2}", this);
        }
    }

    protected override string GetSkillLabel()
    {
        return "Q - 快刀剪乱";
    }
}
