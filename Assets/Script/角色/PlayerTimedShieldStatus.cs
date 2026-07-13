using UnityEngine;

[DisallowMultipleComponent]
public class PlayerTimedShieldStatus : MonoBehaviour
{
    [SerializeField] private bool debugLog;

    private CombatHealth combatHealth;
    private float currentShield;
    private float maxShield;
    private float expireAtTime = -1f;

    public float CurrentShield => IsActive ? currentShield : 0f;
    public float MaxShield => IsActive ? maxShield : 0f;
    public bool IsActive => currentShield > 0f && Time.time < expireAtTime;

    private void Awake()
    {
        combatHealth = GetComponent<CombatHealth>();
    }

    private void Update()
    {
        if (!IsActive && currentShield > 0f)
        {
            ClearShield(notify: true, reason: "Expired");
        }
    }

    public void ApplyShield(float amount, float duration)
    {
        amount = Mathf.Max(0f, amount);
        duration = Mathf.Max(0f, duration);

        if (amount <= 0f || duration <= 0f)
        {
            return;
        }

        float previousCurrent = CurrentShield;
        currentShield = Mathf.Max(previousCurrent, amount);
        maxShield = currentShield;
        expireAtTime = Time.time + duration;

        DebugShield($"Apply amount={amount:F2} duration={duration:F2} previous={previousCurrent:F2} current={currentShield:F2} expireAt={expireAtTime:F2}");
        NotifyShieldChanged();
    }

    public float AbsorbDamage(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f || !IsActive)
        {
            return amount;
        }

        float absorbed = Mathf.Min(currentShield, amount);
        currentShield -= absorbed;
        DebugShield($"Absorb incoming={amount:F2} absorbed={absorbed:F2} remaining={currentShield:F2}");

        if (currentShield <= 0f)
        {
            ClearShield(notify: true, reason: "Consumed");
        }
        else
        {
            NotifyShieldChanged();
        }

        return amount - absorbed;
    }

    public void ClearShield()
    {
        ClearShield(notify: true, reason: "Clear");
    }

    private void ClearShield(bool notify, string reason)
    {
        bool hadShield = currentShield > 0f || maxShield > 0f;
        currentShield = 0f;
        maxShield = 0f;
        expireAtTime = -1f;

        if (hadShield)
        {
            DebugShield($"Clear reason={reason}");
        }

        if (notify && hadShield)
        {
            NotifyShieldChanged();
        }
    }

    private void NotifyShieldChanged()
    {
        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
        }

        combatHealth?.NotifyShieldStateChanged();
    }

    private void DebugShield(string message)
    {
        if (!debugLog)
        {
            return;
        }

        Debug.Log($"[TimedShield] owner={name} {message}", this);
    }
}
