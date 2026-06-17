using UnityEngine;

public class Player1Skill_E_BrokenDash : Player01SkillBase
{
    private void Reset()
    {
        cooldown = 2.2f;
        duration = 0.35f;
        effectPower = 4f;
        animationName = "Run";
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
            Debug.Log($"[E - 断续疾走] Dash framework placeholder. effectPower={effectPower:F2}", this);
        }
    }

    protected override string GetSkillLabel()
    {
        return "E - 断续疾走";
    }
}
