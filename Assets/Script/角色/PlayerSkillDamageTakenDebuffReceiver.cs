using System.Collections.Generic;
using UnityEngine;

public class PlayerSkillDamageTakenDebuffReceiver : MonoBehaviour
{
    private readonly Dictionary<string, float> player01SkillDamageMultipliers = new Dictionary<string, float>();

    public void ApplyPlayer01SkillDamageMultiplier(string key, float multiplier)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        player01SkillDamageMultipliers[key] = Mathf.Max(0f, multiplier);
    }

    public void RemovePlayer01SkillDamageMultiplier(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        player01SkillDamageMultipliers.Remove(key);
    }

    public float GetPlayer01SkillDamageMultiplier()
    {
        float result = 1f;
        foreach (float value in player01SkillDamageMultipliers.Values)
        {
            result *= Mathf.Max(0f, value);
        }

        return result;
    }

    public static float ResolvePlayer01SkillDamageMultiplier(GameObject target)
    {
        PlayerSkillDamageTakenDebuffReceiver receiver = Resolve(target);
        return receiver != null ? receiver.GetPlayer01SkillDamageMultiplier() : 1f;
    }

    public static PlayerSkillDamageTakenDebuffReceiver Resolve(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        PlayerSkillDamageTakenDebuffReceiver receiver = target.GetComponent<PlayerSkillDamageTakenDebuffReceiver>();
        if (receiver != null)
        {
            return receiver;
        }

        receiver = target.GetComponentInParent<PlayerSkillDamageTakenDebuffReceiver>(true);
        if (receiver != null)
        {
            return receiver;
        }

        return target.GetComponentInChildren<PlayerSkillDamageTakenDebuffReceiver>(true);
    }

    private void OnDisable()
    {
        player01SkillDamageMultipliers.Clear();
    }
}
