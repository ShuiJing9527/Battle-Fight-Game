using UnityEngine;

public class PlayerTimedSkillDamageBoostStatus : MonoBehaviour
{
    [SerializeField, Min(1f)] private float damageMultiplier = 1.2f;
    [SerializeField, Min(0f)] private float remainingDuration = 5f;

    public float Multiplier => IsActive ? Mathf.Max(1f, damageMultiplier) : 1f;
    public bool IsActive => remainingDuration > 0f;

    public void ApplyOrRefresh(float multiplier, float duration)
    {
        damageMultiplier = Mathf.Max(1f, multiplier);
        remainingDuration = Mathf.Max(0f, duration);
        enabled = remainingDuration > 0f;
    }

    public static void ApplyOrRefresh(GameObject target, float multiplier, float duration)
    {
        if (target == null)
        {
            return;
        }

        PlayerTimedSkillDamageBoostStatus status = Resolve(target);
        if (status == null)
        {
            status = target.AddComponent<PlayerTimedSkillDamageBoostStatus>();
        }

        status.ApplyOrRefresh(multiplier, duration);
    }

    public static PlayerTimedSkillDamageBoostStatus Resolve(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        PlayerTimedSkillDamageBoostStatus status = target.GetComponent<PlayerTimedSkillDamageBoostStatus>();
        if (status != null)
        {
            return status;
        }

        status = target.GetComponentInParent<PlayerTimedSkillDamageBoostStatus>();
        if (status != null)
        {
            return status;
        }

        return target.GetComponentInChildren<PlayerTimedSkillDamageBoostStatus>();
    }

    private void Update()
    {
        remainingDuration = Mathf.Max(0f, remainingDuration - Time.deltaTime);
        if (remainingDuration <= 0f)
        {
            Destroy(this);
        }
    }
}
