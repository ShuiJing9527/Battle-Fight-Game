using UnityEngine;

public class Player1Skill_W_ThreadFlow : Player01SkillBase
{
    private void Reset()
    {
        cooldown = 4.5f;
        duration = 0.75f;
        effectPower = 0.8f;
        animationName = "Idle";
        debugLog = true;
    }

    protected override bool ShouldLoopAnimation()
    {
        return true;
    }

    protected override void OnCastStarted()
    {
        if (debugLog)
        {
            Debug.Log($"[W - 丝缕缠流] Placeholder flow active. effectPower={effectPower:F2}", this);
        }
    }

    protected override string GetSkillLabel()
    {
        return "W - 丝缕缠流";
    }
}
