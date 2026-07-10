using UnityEngine;

public class PlayerNextSkillDamageBoostStatus : MonoBehaviour
{
    [SerializeField, Min(1f)] private float damageMultiplier = 1.2f;
    [SerializeField, Min(0f)] private float remainingDuration = 2f;

    private bool consumed;

    public float Multiplier => !consumed && remainingDuration > 0f ? Mathf.Max(1f, damageMultiplier) : 1f;
    public bool IsActive => !consumed && remainingDuration > 0f;

    public void ApplyOrRefresh(float multiplier, float duration)
    {
        damageMultiplier = Mathf.Max(1f, multiplier);
        remainingDuration = Mathf.Max(remainingDuration, Mathf.Max(0f, duration));
        consumed = false;
        enabled = remainingDuration > 0f;
    }

    public bool TryConsume(out float multiplier)
    {
        if (!IsActive)
        {
            multiplier = 1f;
            return false;
        }

        consumed = true;
        multiplier = Mathf.Max(1f, damageMultiplier);
        remainingDuration = 0f;
        enabled = false;
        Destroy(this);
        return true;
    }

    public static void ApplyOrRefresh(GameObject target, float multiplier, float duration)
    {
        if (target == null)
        {
            return;
        }

        PlayerNextSkillDamageBoostStatus status = Resolve(target);
        if (status == null)
        {
            status = target.AddComponent<PlayerNextSkillDamageBoostStatus>();
        }

        status.ApplyOrRefresh(multiplier, duration);
    }

    public static PlayerNextSkillDamageBoostStatus Resolve(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        PlayerNextSkillDamageBoostStatus status = target.GetComponent<PlayerNextSkillDamageBoostStatus>();
        if (status != null)
        {
            return status;
        }

        status = target.GetComponentInParent<PlayerNextSkillDamageBoostStatus>();
        if (status != null)
        {
            return status;
        }

        return target.GetComponentInChildren<PlayerNextSkillDamageBoostStatus>();
    }

    private void Update()
    {
        if (!IsActive)
        {
            Destroy(this);
            return;
        }

        remainingDuration = Mathf.Max(0f, remainingDuration - Time.deltaTime);
        if (remainingDuration <= 0f)
        {
            Destroy(this);
        }
    }
}
