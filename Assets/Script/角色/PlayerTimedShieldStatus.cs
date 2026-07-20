using UnityEngine;

[DisallowMultipleComponent]
public class PlayerTimedShieldStatus : MonoBehaviour
{
    [SerializeField] private bool debugLog;
    [SerializeField] private string shieldSourceId;

    private CombatHealth combatHealth;
    private float currentShield;
    private float maxShield;
    private float expireAtTime = -1f;
    private bool persistentShield;

    public float CurrentShield => IsActive ? currentShield : 0f;
    public float MaxShield => IsActive ? maxShield : 0f;
    public bool IsActive => currentShield > 0f && (persistentShield || Time.time < expireAtTime);
    public string ShieldSourceId => shieldSourceId;

    private void Awake()
    {
        combatHealth = GetComponent<CombatHealth>();
    }

    private void Update()
    {
        if (!persistentShield && !IsActive && currentShield > 0f)
        {
            ClearShield(notify: true, reason: "Expired");
        }
    }

    public void ConfigureSource(string sourceId)
    {
        shieldSourceId = string.IsNullOrWhiteSpace(sourceId) ? string.Empty : sourceId.Trim();
    }

    public static PlayerTimedShieldStatus GetOrAdd(GameObject owner, string sourceId)
    {
        if (owner == null)
        {
            return null;
        }

        string resolvedSourceId = string.IsNullOrWhiteSpace(sourceId) ? string.Empty : sourceId.Trim();
        PlayerTimedShieldStatus[] statuses = owner.GetComponents<PlayerTimedShieldStatus>();
        for (int i = 0; i < statuses.Length; i++)
        {
            PlayerTimedShieldStatus status = statuses[i];
            if (status != null && string.Equals(status.ShieldSourceId, resolvedSourceId, System.StringComparison.Ordinal))
            {
                return status;
            }
        }

        PlayerTimedShieldStatus created = owner.AddComponent<PlayerTimedShieldStatus>();
        created.ConfigureSource(resolvedSourceId);
        return created;
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
        persistentShield = false;
        currentShield = Mathf.Max(previousCurrent, amount);
        maxShield = currentShield;
        expireAtTime = Time.time + duration;

        DebugShield($"Apply amount={amount:F2} duration={duration:F2} previous={previousCurrent:F2} current={currentShield:F2} expireAt={expireAtTime:F2}");
        NotifyShieldChanged();
    }

    public void ApplyPersistentShield(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
        {
            return;
        }

        persistentShield = true;
        currentShield = amount;
        maxShield = amount;
        expireAtTime = -1f;

        DebugShield($"ApplyPersistent amount={amount:F2} sourceId={shieldSourceId}");
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
        persistentShield = false;

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
