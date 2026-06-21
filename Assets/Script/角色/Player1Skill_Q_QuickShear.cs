using System.Collections;
using UnityEngine;
using Spine;
using Spine.Unity;

public class Player1Skill_Q_QuickShear : Player01SkillBase
{
    [Header("Q")]
    [SerializeField, Min(1)] private int slashCount = 3;
    [SerializeField, Min(0f)] private float slashInterval = 0.15f;
    [SerializeField, Min(0f)] private float qDamage = 1.2f;
    [SerializeField, Min(0f)] private float qRange = 1.5f;

    private void Reset()
    {
        cooldown = 0.7f;
        duration = 0.42f;
        effectPower = 1.2f;
        animationName = "AKT2";
        debugLog = true;
        slashCount = 3;
        slashInterval = 0.15f;
        qDamage = 1.2f;
        qRange = 1.5f;
    }

    private void OnValidate()
    {
        if (animationName == "ATK2")
        {
            animationName = "AKT2";
        }
    }

    private void Awake()
    {
        if (animationName == "ATK2")
        {
            animationName = "AKT2";
        }
    }

    protected override void OnCastStarted()
    {
        if (debugLog)
        {
            Debug.Log($"[Q - QuickShear] Start. animation={animationName}, slashes={slashCount}, interval={slashInterval:F2}", this);
        }

        if (Controller != null && Controller.IsVeilBarrierActive())
        {
            Debug.Log("[Player01 Q] cast while W active", this);
        }
    }

    protected override IEnumerator CastRoutine()
    {
        int count = Mathf.Max(1, slashCount);
        float interval = Mathf.Max(0f, slashInterval);
        float totalDuration = Mathf.Max(0f, duration);
        float lockDuration = Mathf.Max(totalDuration, interval * Mathf.Max(0, count - 1), 0.55f);

        for (int i = 0; i < count; i++)
        {
            PlaySlash(i + 1, count, lockDuration);

            if (i < count - 1)
            {
                if (interval > 0f)
                {
                    yield return new WaitForSeconds(interval);
                }
                else
                {
                    yield return null;
                }
            }
        }

        float remaining = totalDuration - Mathf.Max(0f, interval * (count - 1));
        if (remaining > 0f)
        {
            yield return new WaitForSeconds(remaining);
        }

        CompleteCast();
    }

    private void PlaySlash(int slashIndex, int slashTotal, float lockDuration)
    {
        if (Controller == null)
        {
            if (debugLog)
            {
                Debug.LogWarning($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} skipped because Controller is null.", this);
            }

            return;
        }

        SkeletonAnimation spine = Controller.GetComponentInChildren<SkeletonAnimation>(true);
        if (spine == null)
        {
            if (debugLog)
            {
                Debug.LogWarning($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} skipped because SkeletonAnimation is missing.", this);
            }

            return;
        }

        if (debugLog)
        {
            Debug.Log($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} try play {animationName} on {spine.name}.", this);
            Debug.Log($"[Q - QuickShear] Target: {Controller.GetSkeletonAnimationDebugSummary()}", this);
        }

        Debug.Log($"[Q - QuickShear] TryPlayLockedSkillAnimation -> {animationName}.", this);
        bool played = Controller.TryPlayLockedSkillAnimation(animationName, false, lockDuration, true, "Q");
        if (!played)
        {
            if (debugLog)
            {
                Debug.LogWarning($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} failed to play '{animationName}'.", this);
            }

            return;
        }

        if (debugLog)
        {
            Debug.Log($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} requested '{animationName}' via shared controller entry.", this);
            Debug.Log($"[Q - QuickShear] damage={qDamage:F2}, range={qRange:F2}", this);
        }

        Controller.StartCoroutine(LogTrackNextFrame(slashIndex, slashTotal));
    }

    private IEnumerator LogTrackNextFrame(int slashIndex, int slashTotal)
    {
        yield return null;

        if (Controller == null)
        {
            yield break;
        }

        SkeletonAnimation spine = Controller.GetComponentInChildren<SkeletonAnimation>(true);
        if (spine == null || spine.AnimationState == null)
        {
            Debug.LogWarning($"[Q - QuickShear] Next frame track check failed after slash {slashIndex}/{slashTotal}: SkeletonAnimation missing.", this);
            yield break;
        }

        TrackEntry current = spine.AnimationState.GetCurrent(0);
        string currentName = current != null && current.Animation != null ? current.Animation.Name : "<none>";
        if (currentName == animationName)
        {
            Debug.Log($"[Q - QuickShear] Next frame Track0 is still {currentName}.", this);
        }
        else
        {
            Debug.LogWarning($"[Q - QuickShear] Next frame Track0 changed to {currentName}. currentLocomotion={Controller.GetCurrentLocomotionAnimationName()}, currentSkill={Controller.GetCurrentSkillName()}.", this);
        }
    }

    protected override string GetSkillLabel()
    {
        return "Q - QuickShear";
    }
}
