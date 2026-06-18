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

    protected float nextCastTime;
    protected Coroutine castRoutine;
    protected bool castFinished;

    public virtual void Initialize(Player01SkillController controller)
    {
        Controller = controller;
    }

    public bool CanCastNow()
    {
        return Time.time >= nextCastTime;
    }

    public virtual void Cast()
    {
        if (!TryReserveCast())
        {
            return;
        }

        if (debugLog)
        {
            Debug.Log($"[{GetSkillLabel()}] Cast started. duration={duration:F2}, power={effectPower:F2}, animation={ResolveAnimationName()}", this);
        }

        OnCastStarted();

        StartManagedCast(CastRoutine());
    }

    protected virtual void OnCastStarted()
    {
    }

    protected virtual void OnCastFinished()
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

        nextCastTime = Time.time + Mathf.Max(0f, cooldown);
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

        CompleteCast();
    }
}
