using System.Collections.Generic;
using UnityEngine;

public class EnemyDebuffReceiver : MonoBehaviour
{
    private readonly Dictionary<string, float> moveSpeedMultipliers = new Dictionary<string, float>();
    private readonly Dictionary<string, float> attackMultipliers = new Dictionary<string, float>();

    public void ApplyMoveSpeedMultiplier(string key, float multiplier)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        moveSpeedMultipliers[key] = Mathf.Max(0f, multiplier);
    }

    public void RemoveMoveSpeedMultiplier(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        moveSpeedMultipliers.Remove(key);
    }

    public void ApplyAttackMultiplier(string key, float multiplier)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        attackMultipliers[key] = Mathf.Max(0f, multiplier);
    }

    public void RemoveAttackMultiplier(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        attackMultipliers.Remove(key);
    }

    public float GetMoveSpeedMultiplier()
    {
        return GetProduct(moveSpeedMultipliers);
    }

    public float GetAttackMultiplier()
    {
        return GetProduct(attackMultipliers);
    }

    private void OnDisable()
    {
        moveSpeedMultipliers.Clear();
        attackMultipliers.Clear();
    }

    private static float GetProduct(Dictionary<string, float> multipliers)
    {
        float result = 1f;
        foreach (float value in multipliers.Values)
        {
            result *= Mathf.Max(0f, value);
        }

        return result;
    }
}
