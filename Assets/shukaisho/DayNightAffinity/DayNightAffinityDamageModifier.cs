using UnityEngine;

public enum TwinStateRuntimeType
{
    Normal,
    Buff,
    Neutral,
    Debuff
}

public enum TwinChildRuntimeType
{
    None,
    DayChild,
    NightChild
}

[System.Serializable]
public struct TwinStateRuntimeBonus
{
    public TwinStateRuntimeType statusType;
    public TwinChildRuntimeType childType;
    public bool hasCurrentPhase;
    public DayNightPhase currentPhase;
    public bool isInDayChildState;
    public bool isInNightChildState;
    public float radiance;
    public float twilight;
    public float attackStatMultiplier;
    public float magicStatMultiplier;
    public float defenseStatMultiplier;
    public float resistanceStatMultiplier;
    public float outgoingDamageMultiplier;
    public float incomingDamageMultiplier;
    public float evasionMultiplier;
    public float moveSpeedMultiplier;
}

public static class DayNightAffinityDamageModifier
{
    private const float TwinChildOutgoingMultiplier = 2f;
    private const float TwinChildIncomingMultiplier = 0.5f;
    private const float NightChildAttackStatMultiplier = 1.5f;
    private const float NightChildMagicStatMultiplier = 1.5f;
    private const float NightChildDefenseStatMultiplier = 2f;
    private const float NightChildResistanceStatMultiplier = 2f;
    private const float DayChildAttackStatMultiplier = 2f;
    private const float DayChildMagicStatMultiplier = 2f;
    private const float DayChildDefenseStatMultiplier = 1.5f;
    private const float DayChildResistanceStatMultiplier = 1.5f;
    private const float WrongTimeEvasionMultiplier = 0.5f;
    private const float WrongTimeMoveSpeedMultiplier = 0.5f;
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

        bool hasCurrentPhase = TryResolveCurrentPhase(out DayNightPhase currentPhase);

        bool hasGauge = DayNightGaugeRuntimeState.TryGetExistingInstance(out DayNightGaugeRuntimeState gauge);
        bool attackerHasDayChildState = HasDayChildState(attacker);
        bool attackerHasNightChildState = HasNightChildState(attacker);
        bool defenderHasDayChildState = HasDayChildState(defender);
        bool defenderHasNightChildState = HasNightChildState(defender);
        TwinStateRuntimeBonus attackerTwinBonus = GetTwinStateRuntimeBonus(attacker);
        TwinStateRuntimeBonus defenderTwinBonus = GetTwinStateRuntimeBonus(defender);
        bool attackerDayChildBuffActive = attackerTwinBonus.isInDayChildState && attackerTwinBonus.statusType == TwinStateRuntimeType.Buff;
        bool attackerNightChildBuffActive = attackerTwinBonus.isInNightChildState && attackerTwinBonus.statusType == TwinStateRuntimeType.Buff;
        bool defenderDayChildBuffActive = defenderTwinBonus.isInDayChildState && defenderTwinBonus.statusType == TwinStateRuntimeType.Buff;
        bool defenderNightChildBuffActive = defenderTwinBonus.isInNightChildState && defenderTwinBonus.statusType == TwinStateRuntimeType.Buff;
        bool defenderDayChildDebuffActive = defenderTwinBonus.isInDayChildState && defenderTwinBonus.statusType == TwinStateRuntimeType.Debuff;
        bool defenderNightChildDebuffActive = defenderTwinBonus.isInNightChildState && defenderTwinBonus.statusType == TwinStateRuntimeType.Debuff;
        bool debugEnabled = hasGauge && (gauge.DebugAffinityDamageEnabled || gauge.DebugDayNightPhaseDiagnosticsEnabled);

        PlayerDayNightAffinity attackerAffinity = ResolveAffinity(attacker);
        PlayerDayNightAffinity defenderAffinity = ResolveAffinity(defender);
        string reason = "no-applicable-rule";

        if (attackerIsPlayer && defenderIsMonster && attackerAffinity != null)
        {
            if (attackerAffinity.IsNightChild && attackerNightChildBuffActive)
            {
                multiplier *= attackerTwinBonus.outgoingDamageMultiplier;
                reason = AppendReason(reason, "night-child-favorable-player-vs-monster");
            }
            else if (attackerAffinity.IsDayChild && attackerDayChildBuffActive)
            {
                multiplier *= attackerTwinBonus.outgoingDamageMultiplier;
                reason = AppendReason(reason, "day-child-favorable-player-vs-monster");
            }
            else if (reason == "no-applicable-rule")
            {
                reason = ResolvePlayerAttackMissReason(
                    attackerAffinity,
                    hasCurrentPhase,
                    currentPhase,
                    attackerHasDayChildState,
                    attackerHasNightChildState);
            }
        }
        else if (attackerIsMonster && defenderIsPlayer && defenderAffinity != null)
        {
            if (defenderAffinity.IsNightChild)
            {
                if (defenderNightChildBuffActive)
                {
                    multiplier *= defenderTwinBonus.incomingDamageMultiplier;
                    reason = AppendReason(reason, "night-child-favorable-monster-vs-player-resist");
                }
                else if (defenderNightChildDebuffActive)
                {
                    multiplier *= defenderTwinBonus.incomingDamageMultiplier;
                    reason = AppendReason(reason, "night-child-unfavorable-monster-vs-player-penalty");
                }
                else if (reason == "no-applicable-rule")
                {
                    reason = "night-child-state-inactive";
                }
            }
            else if (defenderAffinity.IsDayChild)
            {
                if (defenderDayChildBuffActive)
                {
                    multiplier *= defenderTwinBonus.incomingDamageMultiplier;
                    reason = AppendReason(reason, "day-child-favorable-monster-vs-player-resist");
                }
                else if (defenderDayChildDebuffActive)
                {
                    multiplier *= defenderTwinBonus.incomingDamageMultiplier;
                    reason = AppendReason(reason, "day-child-unfavorable-monster-vs-player-penalty");
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
            LogAffinityDecision(debugEnabled, attacker, defender, attackerAffinity, defenderAffinity, gauge, hasCurrentPhase, currentPhase, attackerHasDayChildState, attackerHasNightChildState, defenderHasDayChildState, defenderHasNightChildState, damage, multiplier, reason);
            return damage;
        }

        float finalDamage = damage * multiplier;
        LogAffinityDecision(debugEnabled, attacker, defender, attackerAffinity, defenderAffinity, gauge, hasCurrentPhase, currentPhase, attackerHasDayChildState, attackerHasNightChildState, defenderHasDayChildState, defenderHasNightChildState, damage, multiplier, reason);

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

        if (gauge == null)
        {
            LogHitFlow(debugHitFlow, $"skipped reason=gauge-null originalAttacker={GetObjectName(attacker)} resolvedAttacker={GetObjectName(resolvedAttacker)} target={GetObjectName(resolvedDefender)}", resolvedDefender);
            return false;
        }

        float gain = Mathf.Max(0f, gauge.GaugeGainPerHit);
        float previousRadiance = gauge.RadianceValue;
        float previousTwilight = gauge.TwilightValue;
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
            $"success target={GetObjectName(resolvedDefender)} originalAttacker={GetObjectName(attacker)} resolvedAttacker={GetObjectName(resolvedAttacker)} affinity={GetAffinityName(affinity)} gain={gain:F2} action={action} oldRadiance={previousRadiance:F2} oldTwilight={previousTwilight:F2} newRadiance={gauge.RadianceValue:F2} newTwilight={gauge.TwilightValue:F2}",
            resolvedDefender);
        return true;
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
        return TryResolveCurrentPhase(out DayNightPhase phase) && IsNightChildPositiveState(target, phase);
    }

    public static bool IsDayChildFavorableTime(GameObject target)
    {
        return TryResolveCurrentPhase(out DayNightPhase phase) && IsDayChildPositiveState(target, phase);
    }

    public static bool IsNightChildUnfavorableTime(GameObject target)
    {
        return TryResolveCurrentPhase(out DayNightPhase phase) && IsNightChildNegativeState(target, phase);
    }

    public static bool IsDayChildUnfavorableTime(GameObject target)
    {
        return TryResolveCurrentPhase(out DayNightPhase phase) && IsDayChildNegativeState(target, phase);
    }

    public static bool IsNightChildBuffActive(GameObject target)
    {
        return TryResolveCurrentPhase(out DayNightPhase phase) && IsNightChildPositiveState(target, phase);
    }

    public static bool IsDayChildBuffActive(GameObject target)
    {
        return TryResolveCurrentPhase(out DayNightPhase phase) && IsDayChildPositiveState(target, phase);
    }

    public static bool IsWrongTimeDebuffActive(GameObject target)
    {
        return GetTwinStateRuntimeBonus(target).statusType == TwinStateRuntimeType.Debuff;
    }

    public static float GetWrongTimeEvasionMultiplier(GameObject target)
    {
        return GetTwinStateRuntimeBonus(target).evasionMultiplier;
    }

    public static float GetWrongTimeMoveSpeedMultiplier(GameObject target)
    {
        return GetTwinStateRuntimeBonus(target).moveSpeedMultiplier;
    }

    public static bool IsCorrectDayNightState(GameObject target)
    {
        return IsTwinChildPositivePhase(target);
    }

    public static bool IsWrongDayNightState(GameObject target)
    {
        return IsTwinChildNegativePhase(target);
    }

    public static float GetTwinOutgoingDamageMultiplier(GameObject target)
    {
        return GetTwinStateRuntimeBonus(target).outgoingDamageMultiplier;
    }

    public static float GetTwinIncomingDamageMultiplier(GameObject target)
    {
        return GetTwinStateRuntimeBonus(target).incomingDamageMultiplier;
    }

    public static TwinStateRuntimeBonus GetTwinStateRuntimeBonus(GameObject target)
    {
        bool hasCurrentPhase = TryResolveCurrentPhase(out DayNightPhase currentPhase);
        TwinStateRuntimeBonus bonus = new TwinStateRuntimeBonus
        {
            statusType = TwinStateRuntimeType.Normal,
            childType = TwinChildRuntimeType.None,
            hasCurrentPhase = hasCurrentPhase,
            currentPhase = currentPhase,
            radiance = -1f,
            twilight = -1f,
            attackStatMultiplier = 1f,
            magicStatMultiplier = 1f,
            defenseStatMultiplier = 1f,
            resistanceStatMultiplier = 1f,
            outgoingDamageMultiplier = 1f,
            incomingDamageMultiplier = 1f,
            evasionMultiplier = 1f,
            moveSpeedMultiplier = 1f
        };

        if (DayNightGaugeRuntimeState.TryGetExistingInstance(out DayNightGaugeRuntimeState gauge) && gauge != null)
        {
            bonus.radiance = gauge.RadianceValue;
            bonus.twilight = gauge.TwilightValue;
        }

        GameObject resolvedTarget = ResolvePlayerSource(target) ?? target;
        PlayerDayNightAffinity affinity = ResolveAffinity(resolvedTarget);
        if (affinity == null)
        {
            return bonus;
        }

        if (affinity.IsDayChild)
        {
            bonus.childType = TwinChildRuntimeType.DayChild;
        }
        else if (affinity.IsNightChild)
        {
            bonus.childType = TwinChildRuntimeType.NightChild;
        }

        bonus.isInDayChildState = affinity.IsDayChild && HasDayChildState(resolvedTarget);
        bonus.isInNightChildState = affinity.IsNightChild && HasNightChildState(resolvedTarget);
        if (!bonus.isInDayChildState && !bonus.isInNightChildState)
        {
            return bonus;
        }

        if (!bonus.hasCurrentPhase)
        {
            bonus.statusType = TwinStateRuntimeType.Neutral;
            return bonus;
        }

        bool positive = bonus.isInDayChildState
            ? IsDayChildPositivePhase(bonus.currentPhase)
            : IsNightChildPositivePhase(bonus.currentPhase);
        bool negative = bonus.isInDayChildState
            ? bonus.currentPhase == DayNightPhase.Night
            : bonus.currentPhase == DayNightPhase.Day;

        if (positive)
        {
            bonus.statusType = TwinStateRuntimeType.Buff;
            bonus.outgoingDamageMultiplier = TwinChildOutgoingMultiplier;
            bonus.incomingDamageMultiplier = TwinChildIncomingMultiplier;

            if (bonus.isInDayChildState)
            {
                bonus.attackStatMultiplier = DayChildAttackStatMultiplier;
                bonus.magicStatMultiplier = DayChildMagicStatMultiplier;
                bonus.defenseStatMultiplier = DayChildDefenseStatMultiplier;
                bonus.resistanceStatMultiplier = DayChildResistanceStatMultiplier;
            }
            else
            {
                bonus.attackStatMultiplier = NightChildAttackStatMultiplier;
                bonus.magicStatMultiplier = NightChildMagicStatMultiplier;
                bonus.defenseStatMultiplier = NightChildDefenseStatMultiplier;
                bonus.resistanceStatMultiplier = NightChildResistanceStatMultiplier;
            }

            return bonus;
        }

        if (negative)
        {
            bonus.statusType = TwinStateRuntimeType.Debuff;
            bonus.incomingDamageMultiplier = EnemyDifficultyDirector.ResolveWrongTimeDamageMultiplier();
            bonus.evasionMultiplier = WrongTimeEvasionMultiplier;
            bonus.moveSpeedMultiplier = WrongTimeMoveSpeedMultiplier;
            return bonus;
        }

        bonus.statusType = TwinStateRuntimeType.Neutral;
        return bonus;
    }

    public static bool IsTwinChildPositivePhase(GameObject target)
    {
        if (!TryResolveCurrentPhase(out DayNightPhase phase))
        {
            return false;
        }

        return IsDayChildPositiveState(target, phase) || IsNightChildPositiveState(target, phase);
    }

    public static bool IsNightChildPositivePhase(GameObject target)
    {
        return TryResolveCurrentPhase(out DayNightPhase phase) && IsNightChildPositiveState(target, phase);
    }

    public static bool IsNightChildNeutralPhase(GameObject target)
    {
        return TryResolveCurrentPhase(out DayNightPhase phase) && IsNightChildNeutralState(target, phase);
    }

    public static bool IsNightChildNegativePhase(GameObject target)
    {
        return TryResolveCurrentPhase(out DayNightPhase phase) && IsNightChildNegativeState(target, phase);
    }

    public static bool IsDayChildPositivePhase(GameObject target)
    {
        return TryResolveCurrentPhase(out DayNightPhase phase) && IsDayChildPositiveState(target, phase);
    }

    public static bool IsDayChildNeutralPhase(GameObject target)
    {
        return TryResolveCurrentPhase(out DayNightPhase phase) && IsDayChildNeutralState(target, phase);
    }

    public static bool IsDayChildNegativePhase(GameObject target)
    {
        return TryResolveCurrentPhase(out DayNightPhase phase) && IsDayChildNegativeState(target, phase);
    }

    public static bool IsTwinChildNeutralPhase(GameObject target)
    {
        if (!TryResolveCurrentPhase(out DayNightPhase phase))
        {
            return false;
        }

        return IsDayChildNeutralState(target, phase) || IsNightChildNeutralState(target, phase);
    }

    public static bool IsTwinChildNegativePhase(GameObject target)
    {
        if (!TryResolveCurrentPhase(out DayNightPhase phase))
        {
            return false;
        }

        return IsDayChildNegativeState(target, phase) || IsNightChildNegativeState(target, phase);
    }

    public static bool TryGetCurrentPhase(out DayNightPhase phase)
    {
        return TryResolveCurrentPhase(out phase);
    }

    private static bool IsNightChildPositiveState(GameObject target, DayNightPhase phase)
    {
        return HasNightChildState(target) && IsNightChildPositivePhase(phase);
    }

    private static bool IsDayChildPositiveState(GameObject target, DayNightPhase phase)
    {
        return HasDayChildState(target) && IsDayChildPositivePhase(phase);
    }

    private static bool IsNightChildNeutralState(GameObject target, DayNightPhase phase)
    {
        return HasNightChildState(target) && phase == DayNightPhase.Dawn;
    }

    private static bool IsDayChildNeutralState(GameObject target, DayNightPhase phase)
    {
        return HasDayChildState(target) && phase == DayNightPhase.Dusk;
    }

    private static bool IsNightChildNegativeState(GameObject target, DayNightPhase phase)
    {
        return HasNightChildState(target) && phase == DayNightPhase.Day;
    }

    private static bool IsDayChildNegativeState(GameObject target, DayNightPhase phase)
    {
        return HasDayChildState(target) && phase == DayNightPhase.Night;
    }

    private static bool IsNightChildPositivePhase(DayNightPhase phase)
    {
        return phase == DayNightPhase.Dusk || phase == DayNightPhase.Night;
    }

    private static bool IsDayChildPositivePhase(DayNightPhase phase)
    {
        return phase == DayNightPhase.Dawn || phase == DayNightPhase.Day;
    }

    private static bool IsNightChild(GameObject target)
    {
        GameObject resolvedTarget = ResolvePlayerSource(target) ?? target;
        PlayerDayNightAffinity affinity = ResolveAffinity(resolvedTarget);
        return affinity != null && affinity.IsNightChild;
    }

    private static bool IsDayChild(GameObject target)
    {
        GameObject resolvedTarget = ResolvePlayerSource(target) ?? target;
        PlayerDayNightAffinity affinity = ResolveAffinity(resolvedTarget);
        return affinity != null && affinity.IsDayChild;
    }

    private static bool TryResolveDayState(out bool isDay, out bool isNight)
    {
        isDay = false;
        isNight = false;

        if (TryResolveCurrentPhase(out DayNightPhase phase))
        {
            isDay = phase == DayNightPhase.Dawn || phase == DayNightPhase.Day;
            isNight = phase == DayNightPhase.Dusk || phase == DayNightPhase.Night;
            return true;
        }

        return false;
    }

    private static bool TryResolveCurrentPhase(out DayNightPhase phase)
    {
        return TODDayNightAdapter.TryGetCurrentPhase(out phase);
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
        bool hasCurrentPhase,
        DayNightPhase currentPhase,
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

            if (!hasCurrentPhase)
            {
                return "phase-unavailable";
            }

            return !IsDayChildPositivePhase(currentPhase) ? $"day-child-not-positive-phase-{(hasCurrentPhase ? currentPhase.ToString() : "Unavailable")}" : "no-applicable-rule";
        }

        if (attackerAffinity.IsNightChild)
        {
            if (!hasNightChildState)
            {
                return "night-child-state-inactive";
            }

            if (!hasCurrentPhase)
            {
                return "phase-unavailable";
            }

            return !IsNightChildPositivePhase(currentPhase) ? $"night-child-not-positive-phase-{(hasCurrentPhase ? currentPhase.ToString() : "Unavailable")}" : "no-applicable-rule";
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
        bool hasCurrentPhase,
        DayNightPhase currentPhase,
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
        bool isDay = hasCurrentPhase && (currentPhase == DayNightPhase.Dawn || currentPhase == DayNightPhase.Day);
        bool isNight = hasCurrentPhase && (currentPhase == DayNightPhase.Dusk || currentPhase == DayNightPhase.Night);
        string phase = hasCurrentPhase ? currentPhase.ToString() : "Unknown";
        bool hasCurrentTime = TODDayNightAdapter.TryGetCurrentTimeHours(out float currentTimeHours);
        bool attackerCorrectDayNightState = IsCorrectDayNightState(attacker);
        bool attackerWrongDayNightState = IsWrongDayNightState(attacker);
        bool attackerNeutralDayNightState = IsTwinChildNeutralPhase(attacker);
        bool attackerNightChildPositivePhase = hasCurrentPhase && IsNightChildPositiveState(attacker, currentPhase);
        bool attackerNightChildNeutralPhase = hasCurrentPhase && IsNightChildNeutralState(attacker, currentPhase);
        bool attackerNightChildNegativePhase = hasCurrentPhase && IsNightChildNegativeState(attacker, currentPhase);
        bool attackerDayChildPositivePhase = hasCurrentPhase && IsDayChildPositiveState(attacker, currentPhase);
        bool attackerDayChildNeutralPhase = hasCurrentPhase && IsDayChildNeutralState(attacker, currentPhase);
        bool attackerDayChildNegativePhase = hasCurrentPhase && IsDayChildNegativeState(attacker, currentPhase);
        bool defenderCorrectDayNightState = IsCorrectDayNightState(defender);
        bool defenderWrongDayNightState = IsWrongDayNightState(defender);
        bool defenderNeutralDayNightState = IsTwinChildNeutralPhase(defender);
        bool defenderNightChildPositivePhase = hasCurrentPhase && IsNightChildPositiveState(defender, currentPhase);
        bool defenderNightChildNeutralPhase = hasCurrentPhase && IsNightChildNeutralState(defender, currentPhase);
        bool defenderNightChildNegativePhase = hasCurrentPhase && IsNightChildNegativeState(defender, currentPhase);
        bool defenderDayChildPositivePhase = hasCurrentPhase && IsDayChildPositiveState(defender, currentPhase);
        bool defenderDayChildNeutralPhase = hasCurrentPhase && IsDayChildNeutralState(defender, currentPhase);
        bool defenderDayChildNegativePhase = hasCurrentPhase && IsDayChildNegativeState(defender, currentPhase);
        float attackerOutgoingMultiplier = GetTwinOutgoingDamageMultiplier(attacker);
        float defenderIncomingMultiplier = GetTwinIncomingDamageMultiplier(defender);
        float defenderEvasionMultiplier = GetWrongTimeEvasionMultiplier(defender);
        float defenderMoveSpeedMultiplier = GetWrongTimeMoveSpeedMultiplier(defender);
        TwinStateRuntimeBonus attackerRuntimeBonus = GetTwinStateRuntimeBonus(attacker);
        TwinStateRuntimeBonus defenderRuntimeBonus = GetTwinStateRuntimeBonus(defender);
        TODDayNightAdapter.GetPhaseBoundaries(out float dawnStart, out float dayStart, out float duskStart, out float nightStart);

        Debug.Log(
            $"[DayNightAffinity] phase={phase} attackerIsPlayer={BattleTargetUtility.IsPlayer(attacker)} defenderIsPlayer={BattleTargetUtility.IsPlayer(defender)} " +
            $"attackerIsMonster={BattleTargetUtility.IsMonster(attacker)} defenderIsMonster={BattleTargetUtility.IsMonster(defender)} " +
            $"attacker={GetObjectName(attacker)} defender={GetObjectName(defender)} attackerAffinity={GetAffinityName(attackerAffinity)} defenderAffinity={GetAffinityName(defenderAffinity)} " +
            $"currentTime={(hasCurrentTime ? currentTimeHours.ToString("F2") : "Unavailable")} dawnStart={dawnStart:F2} dayStart={dayStart:F2} duskStart={duskStart:F2} nightStart={nightStart:F2} isDayTime={isDay} isNightTime={isNight} " +
            $"balance={balance:F2} radiance={radiance:F2} twilight={twilight:F2} attackerHasDayChildState={attackerHasDayChildState} attackerHasNightChildState={attackerHasNightChildState} defenderHasDayChildState={defenderHasDayChildState} defenderHasNightChildState={defenderHasNightChildState} " +
            $"attackerCorrectDayNightState={attackerCorrectDayNightState} attackerNeutralDayNightState={attackerNeutralDayNightState} attackerWrongDayNightState={attackerWrongDayNightState} defenderCorrectDayNightState={defenderCorrectDayNightState} defenderNeutralDayNightState={defenderNeutralDayNightState} defenderWrongDayNightState={defenderWrongDayNightState} " +
            $"attackerNightChildPositivePhase={attackerNightChildPositivePhase} attackerNightChildNeutralPhase={attackerNightChildNeutralPhase} attackerNightChildNegativePhase={attackerNightChildNegativePhase} attackerDayChildPositivePhase={attackerDayChildPositivePhase} attackerDayChildNeutralPhase={attackerDayChildNeutralPhase} attackerDayChildNegativePhase={attackerDayChildNegativePhase} " +
            $"defenderNightChildPositivePhase={defenderNightChildPositivePhase} defenderNightChildNeutralPhase={defenderNightChildNeutralPhase} defenderNightChildNegativePhase={defenderNightChildNegativePhase} defenderDayChildPositivePhase={defenderDayChildPositivePhase} defenderDayChildNeutralPhase={defenderDayChildNeutralPhase} defenderDayChildNegativePhase={defenderDayChildNegativePhase} " +
            $"attackerStatusType={attackerRuntimeBonus.statusType} attackerAttackStatMultiplier={attackerRuntimeBonus.attackStatMultiplier:F2} attackerMagicStatMultiplier={attackerRuntimeBonus.magicStatMultiplier:F2} attackerDefenseStatMultiplier={attackerRuntimeBonus.defenseStatMultiplier:F2} attackerResistanceStatMultiplier={attackerRuntimeBonus.resistanceStatMultiplier:F2} " +
            $"defenderStatusType={defenderRuntimeBonus.statusType} defenderAttackStatMultiplier={defenderRuntimeBonus.attackStatMultiplier:F2} defenderMagicStatMultiplier={defenderRuntimeBonus.magicStatMultiplier:F2} defenderDefenseStatMultiplier={defenderRuntimeBonus.defenseStatMultiplier:F2} defenderResistanceStatMultiplier={defenderRuntimeBonus.resistanceStatMultiplier:F2} " +
            $"attackerOutgoingDamageMultiplier={attackerOutgoingMultiplier:F2} defenderIncomingDamageMultiplier={defenderIncomingMultiplier:F2} defenderEvasionMultiplier={defenderEvasionMultiplier:F2} defenderMoveSpeedMultiplier={defenderMoveSpeedMultiplier:F2} " +
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
