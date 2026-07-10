using System.Collections;
using UnityEngine;

public abstract class Player01SkillBase : MonoBehaviour
{
    [Header("Common")]
    [InspectorName("Cooldown")]
    [SerializeField, Min(0f)] protected float cooldown = 1f;
    [InspectorName("Duration")]
    [SerializeField, Min(0f)] protected float duration = 0.35f;
    [InspectorName("Damage / Effect Power")]
    [SerializeField, Min(0f)] protected float effectPower = 1f;
    [InspectorName("Animation Name")]
    [SerializeField] protected string animationName = "";
    [InspectorName("Debug Log")]
    [SerializeField] protected bool debugLog = true;

    protected Player01SkillController Controller { get; private set; }
    protected PlayerSkillCooldownManager SkillResource { get; private set; }

    protected float nextCastTime;
    protected Coroutine castRoutine;
    protected bool castFinished;
    protected int CurrentRuneCastId { get; private set; } = -1;
    protected float CurrentManaRuneEffectStrength { get; private set; }
    private RuneRuntimeState cachedRuneRuntimeState;

    public virtual void Initialize(Player01SkillController controller)
    {
        Controller = controller;
        SkillResource = GetComponent<PlayerSkillCooldownManager>();
    }

    public bool CanCastNow()
    {
        PrepareCastValidation();

        if (SkillResource != null && SkillIndex >= 0)
        {
            return SkillResource.IsSkillCastable(SkillIndex);
        }

        return Time.time >= nextCastTime;
    }

    public virtual bool Cast()
    {
        if (!TryReserveCast())
        {
            return false;
        }

        if (debugLog)
        {
            Debug.Log($"[{GetSkillLabel()}] Cast started. duration={duration:F2}, power={effectPower:F2}, animation={ResolveAnimationName()}", this);
        }

        OnCastStarted();

        StartManagedCast(CastRoutine());
        return true;
    }

    protected virtual void OnCastStarted()
    {
    }

    protected virtual void OnCastFinished()
    {
    }

    protected virtual void PrepareCastValidation()
    {
    }

    protected virtual bool ShouldLoopAnimation()
    {
        return false;
    }

    public virtual bool LocksLocomotionAnimation()
    {
        return true;
    }

    protected virtual string ResolveAnimationName()
    {
        return animationName;
    }

    protected virtual string GetSkillLabel()
    {
        return GetType().Name;
    }

    protected virtual int SkillIndex => -1;

    protected virtual IEnumerator CastRoutine()
    {
        string resolvedAnimation = ResolveAnimationName();
        if (Controller != null)
        {
            Controller.TryPlaySkillAnimation(resolvedAnimation, ShouldLoopAnimation());
        }

        float waitTime = Mathf.Max(0f, duration);
        if (waitTime > 0f)
        {
            yield return new WaitForSeconds(waitTime);
        }
        else
        {
            yield return null;
        }

        CompleteCast();

        if (debugLog)
        {
            Debug.Log($"[{GetSkillLabel()}] Cast finished.", this);
        }

        castRoutine = null;
    }

    protected virtual void OnDisable()
    {
        AbortCast();
    }

    protected virtual void OnDestroy()
    {
        AbortCast();
    }

    protected bool TryReserveCast()
    {
        PrepareCastValidation();

        if (!CanCastNow())
        {
            if (debugLog)
            {
                Debug.Log($"[{GetSkillLabel()}] Skill is on cooldown.", this);
            }

            return false;
        }

        if (Controller != null && !Controller.TryBeginSkill(this))
        {
            if (debugLog)
            {
                Debug.Log($"[{GetSkillLabel()}] Controller is busy, cast ignored.", this);
            }

            return false;
        }

        if (SkillResource != null && SkillIndex >= 0)
        {
            if (!SkillResource.TryConsumeSkillResource(SkillIndex))
            {
                Controller?.FinishSkill(this);
                return false;
            }

            nextCastTime = Time.time + SkillResource.GetSkillMaxCD(SkillIndex);
        }
        else
        {
            nextCastTime = Time.time + Mathf.Max(0f, cooldown);
        }

        PrepareRuneCastContext();
        NotifyDayNightGaugeSkillCast();
        castFinished = false;
        return true;
    }

    protected void StartManagedCast(IEnumerator routine)
    {
        if (castRoutine != null)
        {
            StopCoroutine(castRoutine);
        }

        castRoutine = routine != null ? StartCoroutine(routine) : null;
    }

    protected void CompleteCast()
    {
        if (castFinished)
        {
            return;
        }

        castFinished = true;
        castRoutine = null;

        OnCastFinished();
        ResetRuneCastContext();

        if (Controller != null)
        {
            Controller.FinishSkill(this);
        }
    }

    private void AbortCast()
    {
        if (castRoutine != null)
        {
            StopCoroutine(castRoutine);
        }

        castRoutine = null;

        CompleteCast();
    }

    private void PrepareRuneCastContext()
    {
        CurrentRuneCastId = -1;
        CurrentManaRuneEffectStrength = 0f;

        if (SkillIndex < 0)
        {
            return;
        }

        RuneRuntimeState runtimeState = ResolvePlayerRuneRuntimeState();
        if (runtimeState == null)
        {
            LogManaRuneCastFlow("Skipped. reason=rune-runtime-state-null");
            return;
        }

        LogManaRuneCastFlow($"PrepareRuneCastContext entered. skill={GetSkillLabel()} index={SkillIndex}");
        CurrentRuneCastId = runtimeState.NotifySkillCastStarted(SkillIndex);
        CurrentManaRuneEffectStrength = runtimeState.TriggerManaRuneCastEffect(SkillIndex);
        LogManaRuneCastFlow($"PrepareRuneCastContext result. skill={GetSkillLabel()} castId={CurrentRuneCastId} strength={CurrentManaRuneEffectStrength:F2}");
    }

    private void ResetRuneCastContext()
    {
        CurrentRuneCastId = -1;
        CurrentManaRuneEffectStrength = 0f;
    }

    protected RuneRuntimeState ResolvePlayerRuneRuntimeState()
    {
        if (cachedRuneRuntimeState != null)
        {
            return cachedRuneRuntimeState;
        }

        cachedRuneRuntimeState = GetComponent<RuneRuntimeState>();
        if (cachedRuneRuntimeState != null)
        {
            return cachedRuneRuntimeState;
        }

        if (Controller != null)
        {
            cachedRuneRuntimeState = Controller.GetComponent<RuneRuntimeState>();
            if (cachedRuneRuntimeState != null)
            {
                return cachedRuneRuntimeState;
            }
        }

        cachedRuneRuntimeState = GetComponentInParent<RuneRuntimeState>();
        return cachedRuneRuntimeState;
    }

    protected float ResolveManaRuneScaledMultiplier(float maxBonusRatio)
    {
        return 1f + Mathf.Clamp01(maxBonusRatio) * Mathf.Clamp01(CurrentManaRuneEffectStrength);
    }

    protected float ResolveRuneOutgoingDamageMultiplier()
    {
        RuneRuntimeState runtimeState = ResolvePlayerRuneRuntimeState();
        if (runtimeState == null || SkillIndex < 0)
        {
            return 1f;
        }

        return Mathf.Max(0f, runtimeState.GetOutgoingDamageMultiplier(SkillIndex));
    }

    protected virtual float ResolveSkillManaCostForGauge()
    {
        if (SkillResource != null && SkillIndex >= 0)
        {
            return SkillResource.GetSkillManaCost(SkillIndex);
        }

        return 0f;
    }

    protected virtual float ResolveSkillCooldownForGauge()
    {
        if (SkillResource != null && SkillIndex >= 0)
        {
            return SkillResource.GetSkillMaxCD(SkillIndex);
        }

        return Mathf.Max(0f, cooldown);
    }

    protected virtual void NotifyDayNightGaugeSkillCast()
    {
        DayNightAffinityDamageModifier.NotifySuccessfulSkillCast(
            Controller != null ? Controller.gameObject : gameObject,
            ResolveSkillManaCostForGauge(),
            ResolveSkillCooldownForGauge(),
            GetSkillLabel());
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    protected void LogManaRuneApplied(string skillLabel, string propertyName, float beforeValue, float afterValue)
    {
        Debug.Log(
            $"[ManaRune] Applied to skill={skillLabel}, property={propertyName}, before={beforeValue:F2}, after={afterValue:F2}",
            this);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogManaRuneCastFlow(string message)
    {
        Debug.Log($"[ManaRune] {message}", this);
    }
}
