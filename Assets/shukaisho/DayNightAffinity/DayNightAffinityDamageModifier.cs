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

        bool hasGauge = DayNightGaugeRuntimeState.TryGetExistingInstance(out DayNightGaugeRuntimeState gauge);
        bool radianceBuffActive = hasGauge && gauge.IsRadianceBuffActive();
        bool twilightBuffActive = hasGauge && gauge.IsTwilightBuffActive();
        bool debugEnabled = hasGauge && gauge.DebugAffinityDamageEnabled;

        PlayerDayNightAffinity attackerAffinity = ResolveAffinity(attacker);
        PlayerDayNightAffinity defenderAffinity = ResolveAffinity(defender);
        string reason = "no-applicable-rule";

        if (attackerIsPlayer && defenderIsMonster && attackerAffinity != null)
        {
            if (attackerAffinity.IsNightChild && isNight && twilightBuffActive)
            {
                multiplier *= 1.5f;
                reason = "night-child-night-buff-active-player-vs-monster";
            }
            else if (attackerAffinity.IsDayChild && isDay && radianceBuffActive)
            {
                multiplier *= 1.5f;
                reason = "day-child-day-buff-active-player-vs-monster";
            }
            else
            {
                reason = ResolvePlayerAttackMissReason(attackerAffinity, isDay, isNight, radianceBuffActive, twilightBuffActive, hasGauge);
            }
        }
        else if (attackerIsMonster && defenderIsPlayer && defenderAffinity != null)
        {
            if (defenderAffinity.IsNightChild)
            {
                if (twilightBuffActive)
                {
                    multiplier *= isNight ? 0.5f : 2f;
                    reason = isNight
                        ? "night-child-night-buff-active-monster-vs-player-resist"
                        : "night-child-day-buff-active-monster-vs-player-penalty";
                }
                else
                {
                    reason = hasGauge ? "twilight-buff-inactive" : "gauge-missing";
                }
            }
            else if (defenderAffinity.IsDayChild)
            {
                if (radianceBuffActive)
                {
                    multiplier *= isDay ? 0.5f : 2f;
                    reason = isDay
                        ? "day-child-day-buff-active-monster-vs-player-resist"
                        : "day-child-night-buff-active-monster-vs-player-penalty";
                }
                else
                {
                    reason = hasGauge ? "radiance-buff-inactive" : "gauge-missing";
                }
            }
            else
            {
                reason = "defender-affinity-none";
            }
        }
        else
        {
            reason = ResolveUnhandledReason(attackerIsPlayer, defenderIsMonster, attackerIsMonster, defenderIsPlayer);
            LogAffinityDecision(debugEnabled, attacker, defender, attackerAffinity, defenderAffinity, gauge, isDay, isNight, radianceBuffActive, twilightBuffActive, damage, multiplier, reason);
            return damage;
        }

        float finalDamage = damage * multiplier;
        LogAffinityDecision(debugEnabled, attacker, defender, attackerAffinity, defenderAffinity, gauge, isDay, isNight, radianceBuffActive, twilightBuffActive, damage, multiplier, reason);

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

    private static string ResolvePlayerAttackMissReason(
        PlayerDayNightAffinity attackerAffinity,
        bool isDay,
        bool isNight,
        bool radianceBuffActive,
        bool twilightBuffActive,
        bool hasGauge)
    {
        if (attackerAffinity == null)
        {
            return "attacker-affinity-none";
        }

        if (attackerAffinity.IsDayChild)
        {
            if (!hasGauge)
            {
                return "gauge-missing";
            }

            if (!radianceBuffActive)
            {
                return "radiance-buff-inactive";
            }

            return !isDay ? "phase-mismatch-not-day" : "no-applicable-rule";
        }

        if (attackerAffinity.IsNightChild)
        {
            if (!hasGauge)
            {
                return "gauge-missing";
            }

            if (!twilightBuffActive)
            {
                return "twilight-buff-inactive";
            }

            return !isNight ? "phase-mismatch-not-night" : "no-applicable-rule";
        }

        return "attacker-affinity-none";
    }

    private static string ResolveUnhandledReason(bool attackerIsPlayer, bool defenderIsMonster, bool attackerIsMonster, bool defenderIsPlayer)
    {
        if (!attackerIsPlayer && !attackerIsMonster)
        {
            return "attacker-not-player-or-monster";
        }

        if (!defenderIsPlayer && !defenderIsMonster)
        {
            return "defender-not-player-or-monster";
        }

        if (attackerIsPlayer && !defenderIsMonster)
        {
            return "not-player-vs-monster";
        }

        if (attackerIsMonster && !defenderIsPlayer)
        {
            return "not-monster-vs-player";
        }

        return "unsupported-target-pair";
    }

    private static void LogAffinityDecision(
        bool enabled,
        GameObject attacker,
        GameObject defender,
        PlayerDayNightAffinity attackerAffinity,
        PlayerDayNightAffinity defenderAffinity,
        DayNightGaugeRuntimeState gauge,
        bool isDay,
        bool isNight,
        bool radianceBuffActive,
        bool twilightBuffActive,
        float originalDamage,
        float multiplier,
        string reason)
    {
        if (!enabled)
        {
            return;
        }

        float balance = gauge != null ? gauge.BalanceValue : -1f;
        float radiance = gauge != null ? gauge.RadianceValue : -1f;
        float twilight = gauge != null ? gauge.TwilightValue : -1f;
        string phase = isDay ? "Day" : isNight ? "Night" : "Unknown";

        Debug.Log(
            $"[DayNightAffinity] phase={phase} attackerIsPlayer={BattleTargetUtility.IsPlayer(attacker)} defenderIsPlayer={BattleTargetUtility.IsPlayer(defender)} " +
            $"attackerIsMonster={BattleTargetUtility.IsMonster(attacker)} defenderIsMonster={BattleTargetUtility.IsMonster(defender)} " +
            $"attacker={GetObjectName(attacker)} defender={GetObjectName(defender)} attackerAffinity={GetAffinityName(attackerAffinity)} defenderAffinity={GetAffinityName(defenderAffinity)} " +
            $"balance={balance:F2} radiance={radiance:F2} twilight={twilight:F2} radianceBuffActive={radianceBuffActive} twilightBuffActive={twilightBuffActive} " +
            $"originalDamage={originalDamage:F2} multiplier={multiplier:F2} finalDamage={originalDamage * multiplier:F2} reason={reason}",
            defender != null ? defender : attacker);
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
