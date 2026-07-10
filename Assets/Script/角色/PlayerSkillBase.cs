using UnityEngine;

public abstract class PlayerSkillBase : MonoBehaviour
{
    public Player2PrototypeController Owner { get; private set; }
    public virtual float CooldownSeconds => 0f;
    public virtual float ManaCost => 0f;
    protected virtual int SkillIndex => -1;

    protected int CurrentRuneCastId { get; private set; } = -1;
    protected float CurrentManaRuneEffectStrength { get; private set; }
    private RuneRuntimeState cachedRuneRuntimeState;

    public virtual void Initialize(Player2PrototypeController owner)
    {
        Owner = owner;
    }

    public abstract bool Cast();

    public virtual void Cleanup()
    {
        ResetRuneCastContext();
    }

    public virtual float ProcessIncomingDamageWithWGuard(float rawDamage, BattleDamage incomingDamage)
    {
        return Mathf.Max(0f, rawDamage);
    }

    protected void PrepareRuneCastContext()
    {
        ResetRuneCastContext();
        if (SkillIndex < 0)
        {
            return;
        }

        RuneRuntimeState runtimeState = ResolveRuneRuntimeState();
        if (runtimeState == null)
        {
            LogManaRune($"Skipped. reason=rune-runtime-state-null skill={SkillIndex}");
            return;
        }

        CurrentRuneCastId = runtimeState.NotifySkillCastStarted(SkillIndex);
        CurrentManaRuneEffectStrength = runtimeState.TriggerManaRuneCastEffect(SkillIndex);
        LogManaRune($"Prepared. skill={SkillIndex} castId={CurrentRuneCastId} strength={CurrentManaRuneEffectStrength:F2}");
    }

    protected void ResetRuneCastContext()
    {
        CurrentRuneCastId = -1;
        CurrentManaRuneEffectStrength = 0f;
    }

    protected float ResolveManaRuneScaledMultiplier(float maxBonusRatio)
    {
        return 1f + Mathf.Clamp01(maxBonusRatio) * Mathf.Clamp01(CurrentManaRuneEffectStrength);
    }

    protected float ResolveRuneOutgoingDamageMultiplier()
    {
        RuneRuntimeState runtimeState = ResolveRuneRuntimeState();
        if (runtimeState == null || SkillIndex < 0)
        {
            return 1f;
        }

        return Mathf.Max(0f, runtimeState.GetOutgoingDamageMultiplier(SkillIndex));
    }

    protected RuneRuntimeState ResolveRuneRuntimeState()
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

        if (Owner != null)
        {
            cachedRuneRuntimeState = Owner.GetComponent<RuneRuntimeState>() ?? Owner.GetComponentInParent<RuneRuntimeState>();
            if (cachedRuneRuntimeState != null)
            {
                return cachedRuneRuntimeState;
            }
        }

        cachedRuneRuntimeState = GetComponentInParent<RuneRuntimeState>();
        return cachedRuneRuntimeState;
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
    protected void LogManaRune(string message)
    {
        Debug.Log($"[ManaRune] {message}", this);
    }
}
