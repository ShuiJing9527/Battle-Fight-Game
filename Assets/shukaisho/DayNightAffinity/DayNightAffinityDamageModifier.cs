using UnityEngine;

public static class DayNightAffinityDamageModifier
{
    public static float ApplyModifier(GameObject attacker, GameObject defender, float damage, out float multiplier)
    {
        multiplier = 1f;
        if (damage <= 0f || attacker == null || defender == null)
        {
            return damage;
        }

        bool attackerIsPlayer = BattleTargetUtility.IsPlayer(attacker);
        bool defenderIsPlayer = BattleTargetUtility.IsPlayer(defender);
        bool attackerIsMonster = BattleTargetUtility.IsMonster(attacker);
        bool defenderIsMonster = BattleTargetUtility.IsMonster(defender);

        if (!TryResolveDayState(out bool isDay, out bool isNight))
        {
            return damage;
        }

        PlayerDayNightAffinity attackerAffinity = ResolveAffinity(attacker);
        PlayerDayNightAffinity defenderAffinity = ResolveAffinity(defender);

        if (attackerIsPlayer && defenderIsMonster && attackerAffinity != null)
        {
            if (attackerAffinity.IsNightChild && isNight)
            {
                multiplier *= 1.5f;
            }
            else if (attackerAffinity.IsDayChild && isDay)
            {
                multiplier *= 1.5f;
            }
        }
        else if (attackerIsMonster && defenderIsPlayer && defenderAffinity != null)
        {
            if (defenderAffinity.IsNightChild)
            {
                multiplier *= isNight ? 0.5f : 2f;
            }
            else if (defenderAffinity.IsDayChild)
            {
                multiplier *= isDay ? 0.5f : 2f;
            }
        }
        else
        {
            return damage;
        }

        float finalDamage = damage * multiplier;
        DayNightGaugeRuntimeState gauge = DayNightGaugeRuntimeState.Instance;
        if (gauge != null && gauge.DebugLogEnabled)
        {
            Debug.Log(
                $"[DayNightAffinity] phase={TODDayNightAdapter.GetDebugPhaseName()} attacker={(attacker != null ? attacker.name : "null")} defender={(defender != null ? defender.name : "null")} attackerAffinity={GetAffinityName(attackerAffinity)} defenderAffinity={GetAffinityName(defenderAffinity)} originalDamage={damage:F2} multiplier={multiplier:F2} finalDamage={finalDamage:F2}",
                defender != null ? defender : attacker);
        }

        return finalDamage;
    }

    public static void NotifySuccessfulPlayerHit(GameObject attacker, GameObject defender)
    {
        if (attacker == null || defender == null)
        {
            return;
        }

        if (!BattleTargetUtility.IsPlayer(attacker) || !BattleTargetUtility.IsMonster(defender))
        {
            return;
        }

        PlayerDayNightAffinity affinity = ResolveAffinity(attacker);
        if (affinity == null)
        {
            return;
        }

        DayNightGaugeRuntimeState gauge = DayNightGaugeRuntimeState.Instance;
        if (gauge == null)
        {
            return;
        }

        float amount = gauge.GaugeGainPerHit;
        if (affinity.IsNightChild)
        {
            gauge.AddTwilight(amount);
        }
        else if (affinity.IsDayChild)
        {
            gauge.AddRadiance(amount);
        }
    }

    private static bool TryResolveDayState(out bool isDay, out bool isNight)
    {
        isDay = false;
        isNight = false;

        if (TODDayNightAdapter.TryGetIsDay(out isDay) && TODDayNightAdapter.TryGetIsNight(out isNight))
        {
            return true;
        }

        return false;
    }

    private static PlayerDayNightAffinity ResolveAffinity(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        PlayerDayNightAffinity affinity = target.GetComponent<PlayerDayNightAffinity>();
        if (affinity != null)
        {
            return affinity;
        }

        affinity = target.GetComponentInParent<PlayerDayNightAffinity>(true);
        if (affinity != null)
        {
            return affinity;
        }

        return target.GetComponentInChildren<PlayerDayNightAffinity>(true);
    }

    private static string GetAffinityName(PlayerDayNightAffinity affinity)
    {
        return affinity != null ? affinity.AffinityType.ToString() : "None";
    }
}
