using System.Collections.Generic;
using UnityEngine;

public static class PlayerSkillDamageUtility
{
    private static readonly HashSet<string> MissingStatsWarnings = new HashSet<string>();

    public static float CalculateHybridSkillDamage(
        Component context,
        GameObject ownerObject,
        float baseDamage,
        float physicalScaling,
        float specialScaling,
        string skillLabel)
    {
        float damage = Mathf.Max(0f, baseDamage);
        if (ownerObject == null)
        {
            WarnMissingStatsOnce(context, skillLabel, "<null owner>");
            return damage;
        }

        CombatStats stats = ownerObject.GetComponent<CombatStats>();
        if (stats == null)
        {
            stats = ownerObject.GetComponentInParent<CombatStats>();
        }

        if (stats != null)
        {
            damage += Mathf.Max(0f, stats.physicalAttack) * Mathf.Max(0f, physicalScaling);
            damage += Mathf.Max(0f, stats.specialAttack) * Mathf.Max(0f, specialScaling);
        }
        else
        {
            WarnMissingStatsOnce(context, skillLabel, ownerObject.name);
        }

        BattleResourceBank bank = ownerObject.GetComponent<BattleResourceBank>();
        if (bank == null)
        {
            bank = ownerObject.GetComponentInParent<BattleResourceBank>();
        }

        if (bank != null)
        {
            damage *= bank.SkillDamageMultiplier;
        }

        return BattleStatUtility.ApplyCriticalDamage(ownerObject, damage, out _);
    }

    private static void WarnMissingStatsOnce(Component context, string skillLabel, string ownerName)
    {
        string warningKey = $"{skillLabel}:{ownerName}";
        if (!MissingStatsWarnings.Add(warningKey))
        {
            return;
        }

        Debug.LogWarning($"[{skillLabel}] CombatStats not found on '{ownerName}', using baseDamage fallback.", context);
    }
}
