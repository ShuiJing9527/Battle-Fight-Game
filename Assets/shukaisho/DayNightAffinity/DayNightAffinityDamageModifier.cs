using UnityEngine;

public static class DayNightAffinityDamageModifier
{
    private const float SkillGaugeGainManaWeight = 0.5f;
    private const float SkillGaugeGainCooldownWeight = 0.5f;
    private const float SkillGaugeGainMin = 5f;
    private const float SkillGaugeGainMax = 30f;

    public static float ApplyModifier(GameObject attacker, GameObject defender, float damage, out float multiplier, bool includeAmbientAffinity = true)
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
        bool attackerHasDayChildState = HasDayChildState(attacker);
        bool attackerHasNightChildState = HasNightChildState(attacker);
        bool defenderHasDayChildState = HasDayChildState(defender);
        bool defenderHasNightChildState = HasNightChildState(defender);
        bool attackerDayChildFavorable = attackerHasDayChildState && isDay;
        bool attackerNightChildFavorable = attackerHasNightChildState && isNight;
        bool defenderDayChildFavorable = defenderHasDayChildState && isDay;
        bool defenderNightChildFavorable = defenderHasNightChildState && isNight;
        bool debugEnabled = hasGauge && gauge.DebugAffinityDamageEnabled;

        PlayerDayNightAffinity attackerAffinity = ResolveAffinity(attacker);
        PlayerDayNightAffinity defenderAffinity = ResolveAffinity(defender);
        string reason = "no-applicable-rule";

        if (attackerIsPlayer && defenderIsMonster && attackerAffinity != null)
        {
            if (includeAmbientAffinity && attackerAffinity.IsDayChild && AmbientDayNightAffinityBonus.IsDaytime())
            {
                multiplier *= AmbientDayNightAffinityBonus.GetDayChildAmbientOutgoingMultiplier(attacker, defender, "PlayerVsMonster");
                reason = "day-child-ambient-player-vs-monster";
            }

            if (attackerAffinity.IsNightChild && attackerNightChildFavorable)
            {
                multiplier *= 1.5f;
                reason = AppendReason(reason, "night-child-favorable-player-vs-monster");
            }
            else if (attackerAffinity.IsDayChild && attackerDayChildFavorable)
            {
                multiplier *= 1.5f;
                reason = AppendReason(reason, "day-child-favorable-player-vs-monster");
            }
            else if (reason == "no-applicable-rule" || reason == "day-child-ambient-player-vs-monster")
            {
                reason = ResolvePlayerAttackMissReason(
                    attackerAffinity,
                    isDay,
                    isNight,
                    attackerHasDayChildState,
                    attackerHasNightChildState);
            }
        }
        else if (attackerIsMonster && defenderIsPlayer && defenderAffinity != null)
        {
            if (includeAmbientAffinity && defenderAffinity.IsNightChild && AmbientDayNightAffinityBonus.IsNighttime())
            {
                multiplier *= AmbientDayNightAffinityBonus.GetNightChildAmbientIncomingMultiplier(defender, defender, "MonsterVsPlayer");
                reason = "night-child-ambient-monster-vs-player";
            }

            if (defenderAffinity.IsNightChild)
            {
                if (defenderHasNightChildState)
                {
                    multiplier *= defenderNightChildFavorable ? 0.5f : 2f;
                    reason = AppendReason(
                        reason,
                        defenderNightChildFavorable
                        ? "night-child-favorable-monster-vs-player-resist"
                        : "night-child-unfavorable-monster-vs-player-penalty");
                }
                else if (reason == "no-applicable-rule" || reason == "night-child-ambient-monster-vs-player")
                {
                    reason = "night-child-state-inactive";
                }
            }
            else if (defenderAffinity.IsDayChild)
            {
                if (defenderHasDayChildState)
                {
                    multiplier *= defenderDayChildFavorable ? 0.5f : 2f;
                    reason = AppendReason(
                        reason,
                        defenderDayChildFavorable
                        ? "day-child-favorable-monster-vs-player-resist"
                        : "day-child-unfavorable-monster-vs-player-penalty");
                }
                else if (reason == "no-applicable-rule")
                {
                    reason = "day-child-state-inactive";
                }
            }
            else if (reason == "no-applicable-rule")
            {
                reason = "defender-affinity-none";
            }
        }
        else
        {
            reason = ResolveUnhandledReason(attackerIsPlayer, defenderIsMonster, attackerIsMonster, defenderIsPlayer);
            LogAffinityDecision(debugEnabled, attacker, defender, attackerAffinity, defenderAffinity, gauge, isDay, isNight, attackerHasDayChildState, attackerHasNightChildState, defenderHasDayChildState, defenderHasNightChildState, damage, multiplier, reason);
            return damage;
        }

        float finalDamage = damage * multiplier;
        LogAffinityDecision(debugEnabled, attacker, defender, attackerAffinity, defenderAffinity, gauge, isDay, isNight, attackerHasDayChildState, attackerHasNightChildState, defenderHasDayChildState, defenderHasNightChildState, damage, multiplier, reason);

        return finalDamage;
    }

    private static string AppendReason(string currentReason, string nextReason)
    {
        if (string.IsNullOrEmpty(nextReason))
        {
            return currentReason;
        }

        if (string.IsNullOrEmpty(currentReason) || currentReason == "no-applicable-rule")
        {
            return nextReason;
        }

        return currentReason + "+" + nextReason;
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

        if (!affinity.IsNightChild && !affinity.IsDayChild)
        {
            LogHitFlow(debugHitFlow, $"skipped reason=affinity-type-none originalAttacker={GetObjectName(attacker)} resolvedAttacker={GetObjectName(resolvedAttacker)} target={GetObjectName(resolvedDefender)}", resolvedAttacker);
            return false;
        }

        LogHitFlow(
            debugHitFlow,
            $"skipped reason=hit-gain-disabled target={GetObjectName(resolvedDefender)} originalAttacker={GetObjectName(attacker)} resolvedAttacker={GetObjectName(resolvedAttacker)} affinity={GetAffinityName(affinity)} attackerIsPlayer={BattleTargetUtility.IsPlayer(resolvedAttacker)} targetIsMonster={BattleTargetUtility.IsMonster(resolvedDefender)} balanceUnchanged={gauge.BalanceValue:F2}",
            resolvedDefender);
        return false;
    }

    public static bool NotifySuccessfulSkillCast(GameObject caster, float manaCost, float cooldownSeconds, string skillLabel = null)
    {
        if (caster == null)
        {
            return false;
        }

        DayNightGaugeRuntimeState gauge = DayNightGaugeRuntimeState.Instance;
        bool debugHitFlow = gauge != null && gauge.DebugHitFlowEnabled;
        PlayerDayNightAffinity affinity = ResolveAffinity(caster);
        GameObject resolvedCaster = ResolvePlayerSource(caster) ?? caster;

        if (gauge == null)
        {
            LogHitFlow(debugHitFlow, $"skill-cast skipped reason=gauge-null caster={GetObjectName(resolvedCaster)} skill={skillLabel ?? "<unknown>"}", resolvedCaster);
            return false;
        }

        if (affinity == null)
        {
            LogHitFlow(debugHitFlow, $"skill-cast skipped reason=affinity-not-found caster={GetObjectName(resolvedCaster)} skill={skillLabel ?? "<unknown>"}", resolvedCaster);
            return false;
        }

        if (!affinity.IsDayChild && !affinity.IsNightChild)
        {
            LogHitFlow(debugHitFlow, $"skill-cast skipped reason=affinity-type-none caster={GetObjectName(resolvedCaster)} skill={skillLabel ?? "<unknown>"}", resolvedCaster);
            return false;
        }

        float gain = Mathf.Clamp(
            Mathf.Max(0f, manaCost) * SkillGaugeGainManaWeight
            + Mathf.Max(0f, cooldownSeconds) * SkillGaugeGainCooldownWeight,
            SkillGaugeGainMin,
            SkillGaugeGainMax);
        float previousBalance = gauge.BalanceValue;
        string action;

        if (affinity.IsDayChild)
        {
            gauge.AddRadiance(gain);
            action = "AddRadiance";
        }
        else
        {
            gauge.AddTwilight(gain);
            action = "AddTwilight";
        }

        LogHitFlow(
            debugHitFlow,
            $"skill-cast success caster={GetObjectName(resolvedCaster)} skill={skillLabel ?? "<unknown>"} affinity={GetAffinityName(affinity)} manaCost={manaCost:F2} cooldown={cooldownSeconds:F2} gain={gain:F2} action={action} oldBalance={previousBalance:F2} newBalance={gauge.BalanceValue:F2}",
            resolvedCaster);
        return true;
    }

    public static bool HasNightChildState(GameObject target)
    {
        GameObject resolvedTarget = ResolvePlayerSource(target) ?? target;
        PlayerDayNightAffinity affinity = ResolveAffinity(resolvedTarget);
        return affinity != null
               && affinity.IsNightChild
               && DayNightGaugeRuntimeState.TryGetExistingInstance(out DayNightGaugeRuntimeState gauge)
               && gauge != null
               && gauge.HasTwilightState();
    }

    public static bool HasDayChildState(GameObject target)
    {
        GameObject resolvedTarget = ResolvePlayerSource(target) ?? target;
        PlayerDayNightAffinity affinity = ResolveAffinity(resolvedTarget);
        return affinity != null
               && affinity.IsDayChild
               && DayNightGaugeRuntimeState.TryGetExistingInstance(out DayNightGaugeRuntimeState gauge)
               && gauge != null
               && gauge.HasRadianceState();
    }

    public static bool IsNightChildFavorableTime(GameObject target)
    {
        return HasNightChildState(target) && TryResolveDayState(out _, out bool isNight) && isNight;
    }

    public static bool IsDayChildFavorableTime(GameObject target)
    {
        return HasDayChildState(target) && TryResolveDayState(out bool isDay, out _) && isDay;
    }

    public static bool IsNightChildUnfavorableTime(GameObject target)
    {
        return HasNightChildState(target) && TryResolveDayState(out bool isDay, out _) && isDay;
    }

    public static bool IsDayChildUnfavorableTime(GameObject target)
    {
        return HasDayChildState(target) && TryResolveDayState(out _, out bool isNight) && isNight;
    }

    public static bool IsNightChildBuffActive(GameObject target)
    {
        return HasNightChildState(target);
    }

    public static bool IsDayChildBuffActive(GameObject target)
    {
        return HasDayChildState(target);
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
        bool hasDayChildState,
        bool hasNightChildState)
    {
        if (attackerAffinity == null)
        {
            return "attacker-affinity-none";
        }

        if (attackerAffinity.IsDayChild)
        {
            if (!hasDayChildState)
            {
                return "day-child-state-inactive";
            }

            return !isDay ? "phase-mismatch-not-day" : "no-applicable-rule";
        }

        if (attackerAffinity.IsNightChild)
        {
            if (!hasNightChildState)
            {
                return "night-child-state-inactive";
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
        bool attackerHasDayChildState,
        bool attackerHasNightChildState,
        bool defenderHasDayChildState,
        bool defenderHasNightChildState,
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
            $"balance={balance:F2} radiance={radiance:F2} twilight={twilight:F2} attackerHasDayChildState={attackerHasDayChildState} attackerHasNightChildState={attackerHasNightChildState} defenderHasDayChildState={defenderHasDayChildState} defenderHasNightChildState={defenderHasNightChildState} " +
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
