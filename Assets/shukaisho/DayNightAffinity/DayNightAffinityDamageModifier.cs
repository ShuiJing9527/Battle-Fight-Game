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

    public static bool NotifySuccessfulPlayerHit(GameObject attacker, GameObject defender)
    {
        DayNightGaugeRuntimeState gauge = DayNightGaugeRuntimeState.Instance;
        bool debugHitFlow = gauge != null && gauge.DebugHitFlowEnabled;

        if (attacker == null || defender == null)
        {
            LogHitFlow(debugHitFlow, $"skipped reason={(attacker == null ? "attacker-null" : "defender-null")} originalAttacker={GetObjectName(attacker)} target={GetObjectName(defender)}", defender != null ? defender : attacker);
            return false;
        }

        GameObject resolvedAttacker = ResolvePlayerSource(attacker);
        GameObject resolvedDefender = ResolveMonsterTarget(defender);
        PlayerDayNightAffinity affinity = ResolveAffinity(resolvedAttacker != null ? resolvedAttacker : attacker);

        if (resolvedAttacker == null)
        {
            LogHitFlow(debugHitFlow, $"skipped reason=attacker-not-player originalAttacker={GetObjectName(attacker)} target={GetObjectName(defender)}", attacker);
            return false;
        }

        if (affinity == null)
        {
            LogHitFlow(debugHitFlow, $"skipped reason=affinity-not-found originalAttacker={GetObjectName(attacker)} resolvedAttacker={GetObjectName(resolvedAttacker)} target={GetObjectName(defender)}", resolvedAttacker);
            return false;
        }

        if (resolvedDefender == null)
        {
            LogHitFlow(debugHitFlow, $"skipped reason=target-not-monster originalAttacker={GetObjectName(attacker)} resolvedAttacker={GetObjectName(resolvedAttacker)} target={GetObjectName(defender)}", defender);
            return false;
        }

        float amount = gauge.GaugeGainPerHit;
        float previousBalance = gauge.BalanceValue;
        string action = "none";
        if (affinity.IsNightChild)
        {
            gauge.AddTwilight(amount);
            action = "AddTwilight";
        }
        else if (affinity.IsDayChild)
        {
            gauge.AddRadiance(amount);
            action = "AddRadiance";
        }
        else
        {
            LogHitFlow(debugHitFlow, $"skipped reason=affinity-type-none originalAttacker={GetObjectName(attacker)} resolvedAttacker={GetObjectName(resolvedAttacker)} target={GetObjectName(resolvedDefender)}", resolvedAttacker);
            return false;
        }

        LogHitFlow(
            debugHitFlow,
            $"success target={GetObjectName(resolvedDefender)} originalAttacker={GetObjectName(attacker)} resolvedAttacker={GetObjectName(resolvedAttacker)} affinity={GetAffinityName(affinity)} attackerIsPlayer={BattleTargetUtility.IsPlayer(resolvedAttacker)} targetIsMonster={BattleTargetUtility.IsMonster(resolvedDefender)} action={action} oldBalance={previousBalance:F2} newBalance={gauge.BalanceValue:F2}",
            resolvedDefender);
        return true;
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

    private static GameObject ResolvePlayerSource(GameObject source)
    {
        if (source == null)
        {
            return null;
        }

        if (BattleTargetUtility.IsPlayer(source))
        {
            return source;
        }

        CombatHealth combatHealth = source.GetComponentInParent<CombatHealth>();
        if (combatHealth != null && BattleTargetUtility.IsPlayer(combatHealth.gameObject))
        {
            return combatHealth.gameObject;
        }

        PlayerMovement movement = source.GetComponentInParent<PlayerMovement>(true);
        if (movement != null)
        {
            return movement.gameObject;
        }

        Player01SkillController player01 = source.GetComponentInParent<Player01SkillController>(true);
        if (player01 != null)
        {
            return player01.gameObject;
        }

        Player2PrototypeController player02 = source.GetComponentInParent<Player2PrototypeController>(true);
        if (player02 != null)
        {
            return player02.gameObject;
        }

        PlayerDayNightAffinity affinity = ResolveAffinity(source);
        return affinity != null ? affinity.gameObject : null;
    }

    private static GameObject ResolveMonsterTarget(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        if (BattleTargetUtility.IsMonster(target))
        {
            CombatHealth combatHealth = target.GetComponentInParent<CombatHealth>();
            return combatHealth != null ? combatHealth.gameObject : target;
        }

        CombatHealth parentHealth = target.GetComponentInParent<CombatHealth>();
        if (parentHealth != null && BattleTargetUtility.IsMonster(parentHealth.gameObject))
        {
            return parentHealth.gameObject;
        }

        EnemyController enemyController = target.GetComponentInParent<EnemyController>(true);
        if (enemyController != null)
        {
            return enemyController.gameObject;
        }

        MonsterIdentity identity = target.GetComponentInParent<MonsterIdentity>(true);
        return identity != null ? identity.gameObject : null;
    }

    private static string GetAffinityName(PlayerDayNightAffinity affinity)
    {
        return affinity != null ? affinity.AffinityType.ToString() : "None";
    }

    private static void LogHitFlow(bool enabled, string message, Object context)
    {
        if (!enabled)
        {
            return;
        }

        Debug.Log($"[DayNightHitFlow] {message}", context);
    }

    private static string GetObjectName(GameObject target)
    {
        if (target == null)
        {
            return "<null>";
        }

        return $"{target.name} ({GetHierarchyPath(target)})";
    }

    private static string GetHierarchyPath(GameObject target)
    {
        if (target == null)
        {
            return "<null>";
        }

        Transform current = target.transform;
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        return path;
    }
}
