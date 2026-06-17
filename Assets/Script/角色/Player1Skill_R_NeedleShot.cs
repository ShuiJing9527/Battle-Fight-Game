using UnityEngine;

public class Player1Skill_R_NeedleShot : Player01SkillBase
{
    private void Reset()
    {
        cooldown = 1.15f;
        duration = 0.45f;
        effectPower = 1.35f;
        animationName = "ATK1";
        debugLog = true;
    }

    protected override void OnCastStarted()
    {
        if (debugLog)
        {
            Debug.Log($"[R - 弓针镂射] NeedleShot executed. effectPower={effectPower:F2}", this);
        }
    }

    protected override string GetSkillLabel()
    {
        return "R - 弓针镂射";
    }
}
