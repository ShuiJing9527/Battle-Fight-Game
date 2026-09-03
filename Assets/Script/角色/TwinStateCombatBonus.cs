using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TwinFormalStateStatus : MonoBehaviour
{
    [SerializeField] private bool debugTwinStateBonuses;

    private CombatStats combatStats;
    private CombatHealth combatHealth;
    private PlayerDayNightAffinity affinity;
    private float appliedPhysicalAttackBonus;
    private float appliedSpecialAttackBonus;
    private float appliedPhysicalDefenseBonus;
    private float appliedSpecialDefenseBonus;
    private bool deathSubscribed;
    private bool hasLoggedRuntimeState;
    private TwinStateRuntimeType lastLoggedStatusType;
    private DayNightPhase lastLoggedPhase;
    private TwinStateRuntimeBonus currentRuntimeBonus;

    public bool DebugTwinStateBonuses
    {
        get
        {
            if (debugTwinStateBonuses)
            {
                return true;
            }

            return DayNightGaugeRuntimeState.TryGetExistingInstance(out DayNightGaugeRuntimeState gauge)
                   && gauge != null
                   && (gauge.DebugAffinityDamageEnabled || gauge.DebugDayNightPhaseDiagnosticsEnabled);
        }
    }

    public TwinStateRuntimeBonus CurrentRuntimeBonus => currentRuntimeBonus;
    public float AppliedPhysicalAttackBonus => appliedPhysicalAttackBonus;
    public float AppliedSpecialAttackBonus => appliedSpecialAttackBonus;
    public float AppliedPhysicalDefenseBonus => appliedPhysicalDefenseBonus;
    public float AppliedSpecialDefenseBonus => appliedSpecialDefenseBonus;

    private void Awake()
    {
        ResolveReferences();
        SubscribeDeathEventIfNeeded();
        RefreshState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeDeathEventIfNeeded();
        RefreshState();
    }

    private void Update()
    {
        RefreshState();
    }

    public void RefreshNow()
    {
        RefreshState();
    }

    private void OnDisable()
    {
        UnsubscribeDeathEventIfNeeded();
        ClearAppliedBonuses("Disable");
    }

    private void OnDestroy()
    {
        UnsubscribeDeathEventIfNeeded();
        ClearAppliedBonuses("Destroy");
    }

    public void DebugLog(string message, Object context = null)
    {
        if (!DebugTwinStateBonuses)
        {
            return;
        }

        Debug.Log($"[TwinStateBonus] owner={name} {message}", context != null ? context : this);
    }

    private void RefreshState()
    {
        ResolveReferences();
        if (combatStats == null)
        {
            return;
        }

        if (combatHealth != null && combatHealth.IsDead)
        {
            ClearAppliedBonuses("Death");
            return;
        }

        currentRuntimeBonus = DayNightAffinityDamageModifier.GetTwinStateRuntimeBonus(gameObject);
        bool changed = false;
        changed |= SyncStatMultiplier(
            ref combatStats.physicalAttack,
            ref appliedPhysicalAttackBonus,
            currentRuntimeBonus.attackStatMultiplier,
            "physicalAttack");
        changed |= SyncStatMultiplier(
            ref combatStats.specialAttack,
            ref appliedSpecialAttackBonus,
            currentRuntimeBonus.magicStatMultiplier,
            "specialAttack");
        changed |= SyncStatMultiplier(
            ref combatStats.physicalDefense,
            ref appliedPhysicalDefenseBonus,
            currentRuntimeBonus.defenseStatMultiplier,
            "physicalDefense");
        changed |= SyncStatMultiplier(
            ref combatStats.specialDefense,
            ref appliedSpecialDefenseBonus,
            currentRuntimeBonus.resistanceStatMultiplier,
            "specialDefense");

        bool stateChanged = !hasLoggedRuntimeState
                            || lastLoggedStatusType != currentRuntimeBonus.statusType
                            || lastLoggedPhase != currentRuntimeBonus.currentPhase;
        if (changed || stateChanged)
        {
            hasLoggedRuntimeState = true;
            lastLoggedStatusType = currentRuntimeBonus.statusType;
            lastLoggedPhase = currentRuntimeBonus.currentPhase;
            DebugLog(
                $"currentCharacter={currentRuntimeBonus.childType} CurrentPhase={(currentRuntimeBonus.hasCurrentPhase ? currentRuntimeBonus.currentPhase.ToString() : "Unavailable")} " +
                $"Radiance={currentRuntimeBonus.radiance:F2} Twilight={currentRuntimeBonus.twilight:F2} " +
                $"IsInDayChildState={currentRuntimeBonus.isInDayChildState} IsInNightChildState={currentRuntimeBonus.isInNightChildState} statusType={currentRuntimeBonus.statusType} " +
                $"attackStatMultiplier={currentRuntimeBonus.attackStatMultiplier:F2} magicStatMultiplier={currentRuntimeBonus.magicStatMultiplier:F2} " +
                $"defenseStatMultiplier={currentRuntimeBonus.defenseStatMultiplier:F2} resistanceStatMultiplier={currentRuntimeBonus.resistanceStatMultiplier:F2} " +
                $"outgoingDamageMultiplier={currentRuntimeBonus.outgoingDamageMultiplier:F2} incomingDamageMultiplier={currentRuntimeBonus.incomingDamageMultiplier:F2} " +
                $"evasionMultiplier={currentRuntimeBonus.evasionMultiplier:F2} moveSpeedMultiplier={currentRuntimeBonus.moveSpeedMultiplier:F2} " +
                $"panelBonusATK={appliedPhysicalAttackBonus:F2} panelBonusMAG={appliedSpecialAttackBonus:F2} " +
                $"panelBonusDEF={appliedPhysicalDefenseBonus:F2} panelBonusRES={appliedSpecialDefenseBonus:F2}");
        }
    }

    private void ResolveReferences()
    {
        if (combatStats == null)
        {
            combatStats = GetComponent<CombatStats>();
        }

        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
        }

        if (affinity == null)
        {
            affinity = GetComponent<PlayerDayNightAffinity>();
            if (affinity == null)
            {
                affinity = GetComponentInChildren<PlayerDayNightAffinity>(true);
            }
        }
    }

    private void SubscribeDeathEventIfNeeded()
    {
        if (deathSubscribed || combatHealth == null)
        {
            return;
        }

        combatHealth.Died += HandleOwnerDied;
        deathSubscribed = true;
    }

    private void UnsubscribeDeathEventIfNeeded()
    {
        if (!deathSubscribed || combatHealth == null)
        {
            return;
        }

        combatHealth.Died -= HandleOwnerDied;
        deathSubscribed = false;
    }

    private void HandleOwnerDied(GameObject _)
    {
        ClearAppliedBonuses("CombatHealth.Died");
    }

    private bool SyncStatMultiplier(ref float statValue, ref float appliedBonus, float multiplier, string statLabel)
    {
        float rawWithoutBonus = Mathf.Max(0f, statValue - appliedBonus);
        float desiredBonus = Mathf.Max(0f, rawWithoutBonus * Mathf.Max(0f, multiplier - 1f));
        bool changed = !Mathf.Approximately(appliedBonus, desiredBonus);
        statValue = rawWithoutBonus + desiredBonus;
        appliedBonus = desiredBonus;

        if (changed)
        {
            DebugLog($"RuntimeStatBonus stat={statLabel} raw={rawWithoutBonus:F2} multiplier={multiplier:F2} appliedBonus={desiredBonus:F2} final={statValue:F2}");
        }

        return changed;
    }

    private void ClearAppliedBonuses(string reason)
    {
        if (combatStats == null)
        {
            appliedPhysicalAttackBonus = 0f;
            appliedSpecialAttackBonus = 0f;
            appliedPhysicalDefenseBonus = 0f;
            appliedSpecialDefenseBonus = 0f;
            return;
        }

        bool removedAny = false;
        removedAny |= RemoveAppliedBonus(ref combatStats.physicalAttack, ref appliedPhysicalAttackBonus);
        removedAny |= RemoveAppliedBonus(ref combatStats.specialAttack, ref appliedSpecialAttackBonus);
        removedAny |= RemoveAppliedBonus(ref combatStats.physicalDefense, ref appliedPhysicalDefenseBonus);
        removedAny |= RemoveAppliedBonus(ref combatStats.specialDefense, ref appliedSpecialDefenseBonus);

        if (removedAny)
        {
            DebugLog($"RuntimeStatBonus removed reason={reason}");
        }
    }

    private static bool RemoveAppliedBonus(ref float statValue, ref float appliedBonus)
    {
        if (appliedBonus <= 0f)
        {
            return false;
        }

        statValue = Mathf.Max(0f, statValue - appliedBonus);
        appliedBonus = 0f;
        return true;
    }
}

public static class TwinStateCombatBonus
{
    private const float NightChildAttackMultiplier = 2f;
    private const int NightChildFixedDamageBase = 0;
    private const int NightChildFixedDamagePerRune = 0;
    private const int NightChildFixedDamageCap = 0;
    private const float NightChildLowHealthStep = 0.10f;
    private const float NightChildLowHealthReductionPerStep = 0.03f;
    private const float NightChildCriticalHealthThreshold = 0.10f;
    private const float NightChildCriticalHealthReduction = 0.30f;
    private const float NightChildFavorableTimeReduction = 0.50f;
    private const float NightChildTotalReductionCap = 0.80f;
    private const float DayChildQDamageMultiplier = 1f;
    private const float DayChildRBaseDamageBonus = 0f;
    private const float DayChildRMarkDamageBonusPerEnemy = 0f;
    private const float DayChildRMarkDamageBonusCap = 0f;
    private const string NightChildFixedDamageTag = "NightChildFixedBonus";

    private static readonly Dictionary<string, HashSet<int>> NightChildHitTargetsByCast = new Dictionary<string, HashSet<int>>();

    public struct DayChildRSnapshot
    {
        public bool stateActive;
        public int markedEnemyCount;
        public float markBonus;
        public float multiplier;
    }

    public static TwinFormalStateStatus EnsureFormalStateStatus(GameObject owner)
    {
        if (owner == null)
        {
            return null;
        }

        PlayerDayNightAffinity affinity = owner.GetComponent<PlayerDayNightAffinity>();
        if (affinity == null)
        {
            affinity = owner.GetComponentInChildren<PlayerDayNightAffinity>(true);
        }

        if (affinity == null)
        {
            return null;
        }

        TwinFormalStateStatus status = owner.GetComponent<TwinFormalStateStatus>();
        if (status == null)
        {
            status = owner.AddComponent<TwinFormalStateStatus>();
        }

        return status;
    }

    public static bool IsNightChildStateActive(GameObject owner)
    {
        EnsureFormalStateStatus(owner);
        return owner != null && DayNightAffinityDamageModifier.HasNightChildState(owner);
    }

    public static bool IsDayChildStateActive(GameObject owner)
    {
        EnsureFormalStateStatus(owner);
        return owner != null && DayNightAffinityDamageModifier.HasDayChildState(owner);
    }

    public static int CountEquippedRunesForSkill(GameObject owner, int skillIndex)
    {
        CombatSkillCaster caster = ResolveSkillCaster(owner);
        if (caster == null)
        {
            return 0;
        }

        BattleSkill skill = caster.GetSkill(skillIndex);
        if (skill == null || skill.equippedRunes == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < skill.equippedRunes.Length; i++)
        {
            if (skill.equippedRunes[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    public static float GetNightChildAttackMultiplier()
    {
        return NightChildAttackMultiplier;
    }

    public static int GetNightChildFixedSkillDamage(GameObject owner, int skillIndex)
    {
        int runeCount = CountEquippedRunesForSkill(owner, skillIndex);
        return Mathf.Min(NightChildFixedDamageCap, NightChildFixedDamageBase + runeCount * NightChildFixedDamagePerRune);
    }

    public static int CountMarkedLivingEnemies()
    {
        RadianceMarkStatus.CleanupInvalidActiveMarks();
        return RadianceMarkStatus.ActiveMarkCount;
    }

    public static float GetNightChildLowHealthReduction(GameObject target)
    {
        if (!IsNightChildStateActive(target))
        {
            return 0f;
        }

        CombatHealth combatHealth = ResolveCombatHealth(target);
        float maxHealth = ResolveMaxHealth(combatHealth);
        if (combatHealth == null || maxHealth <= 0f)
        {
            return 0f;
        }

        float currentHealth = ResolveCurrentHealth(combatHealth);
        if (currentHealth <= maxHealth * NightChildCriticalHealthThreshold)
        {
            return NightChildCriticalHealthReduction;
        }

        float lostRatio = 1f - Mathf.Clamp01(currentHealth / maxHealth);
        int lostTier = Mathf.Max(0, Mathf.FloorToInt(lostRatio / NightChildLowHealthStep));
        return Mathf.Clamp(lostTier * NightChildLowHealthReductionPerStep, 0f, NightChildCriticalHealthReduction);
    }

    public static float GetNightChildTotalDamageReduction(GameObject target, bool isNight)
    {
        if (!IsNightChildStateActive(target))
        {
            return 0f;
        }

        float nightReduction = isNight ? NightChildFavorableTimeReduction : 0f;
        float lowHealthReduction = GetNightChildLowHealthReduction(target);
        return Mathf.Clamp(nightReduction + lowHealthReduction, 0f, NightChildTotalReductionCap);
    }

    public static float ApplyNightChildIncomingDamageReduction(
        GameObject target,
        GameObject resolvedMonsterSource,
        float damageBeforeTwinReduction,
        Object context = null,
        string damageTag = null)
    {
        float clampedDamage = Mathf.Max(0f, damageBeforeTwinReduction);
        if (clampedDamage <= 0f || target == null || resolvedMonsterSource == null || !IsNightChildStateActive(target))
        {
            return clampedDamage;
        }

        TwinFormalStateStatus status = GetStatus(target);
        if (status != null && status.DebugTwinStateBonuses)
        {
            status.DebugLog(
                $"NightChildIncomingReduction skipped damageTag={damageTag ?? "<none>"} source={resolvedMonsterSource.name} reason=handled-by-day-night-affinity-damage-modifier damage={clampedDamage:F2}",
                context);
        }

        return clampedDamage;
    }

    public static float GetDayChildQDamageMultiplier(GameObject owner, Object context = null)
    {
        float multiplier = IsDayChildStateActive(owner) ? DayChildQDamageMultiplier : 1f;
        GetStatus(owner)?.DebugLog(
            $"DayChildQMultiplier active={IsDayChildStateActive(owner)} multiplier={multiplier:F2}",
            context);
        return multiplier;
    }

    public static DayChildRSnapshot CreateDayChildRSnapshot(GameObject owner, Object context = null)
    {
        DayChildRSnapshot snapshot = new DayChildRSnapshot
        {
            stateActive = IsDayChildStateActive(owner),
            markedEnemyCount = 0,
            markBonus = 0f,
            multiplier = 1f
        };

        if (!snapshot.stateActive)
        {
            GetStatus(owner)?.DebugLog("DayChildRSnapshot active=false multiplier=1.00", context);
            return snapshot;
        }

        snapshot.markedEnemyCount = CountMarkedLivingEnemies();
        snapshot.markBonus = Mathf.Min(
            DayChildRMarkDamageBonusCap,
            snapshot.markedEnemyCount * DayChildRMarkDamageBonusPerEnemy);
        snapshot.multiplier = 1f + DayChildRBaseDamageBonus + snapshot.markBonus;
        GetStatus(owner)?.DebugLog(
            $"DayChildRSnapshot active=true markedEnemyCount={snapshot.markedEnemyCount} markBonus={snapshot.markBonus:F2} multiplier={snapshot.multiplier:F2}",
            context);
        return snapshot;
    }

    public static bool TryApplyNightChildFixedSkillDamage(
        GameObject owner,
        CombatHealth target,
        BattleDamageType damageType,
        int skillIndex,
        int castId,
        Object context = null,
        string skillLabel = null)
    {
        if (owner == null || target == null || castId < 0 || !IsNightChildStateActive(owner))
        {
            return false;
        }

        if (target.IsDead)
        {
            return false;
        }

        int targetId = target.GetInstanceID();
        string castKey = BuildNightChildCastKey(owner, skillIndex, castId);
        HashSet<int> hitTargets = GetOrCreateNightChildTargetSet(castKey);
        if (!hitTargets.Add(targetId))
        {
            GetStatus(owner)?.DebugLog(
                $"NightChildFixedDamageSkipped skill={skillLabel ?? skillIndex.ToString()} castId={castId} target={target.name} reason=already-hit",
                context);
            return false;
        }

        int currentSkillRuneCount = CountEquippedRunesForSkill(owner, skillIndex);
        int extraDamage = Mathf.Min(NightChildFixedDamageCap, NightChildFixedDamageBase + currentSkillRuneCount * NightChildFixedDamagePerRune);
        if (extraDamage <= 0)
        {
            return false;
        }

        BattleDamage bonusDamage = new BattleDamage(extraDamage, damageType, owner)
        {
            bypassAttackerMultipliers = true,
            bypassAffinityModifier = true,
            suppressGaugeNotification = true,
            debugTag = NightChildFixedDamageTag
        };

        target.ApplyDirectDamage(bonusDamage, DamagePopupType.Normal);
        GetStatus(owner)?.DebugLog(
            $"NightChildFixedDamageApplied skill={skillLabel ?? skillIndex.ToString()} castId={castId} target={target.name} runeCount={currentSkillRuneCount} extraDamage={extraDamage}",
            context);
        return true;
    }

    public static void ReleaseNightChildSkillCast(GameObject owner, int skillIndex, int castId)
    {
        if (owner == null || castId < 0)
        {
            return;
        }

        NightChildHitTargetsByCast.Remove(BuildNightChildCastKey(owner, skillIndex, castId));
    }

    private static TwinFormalStateStatus GetStatus(GameObject owner)
    {
        return owner != null ? EnsureFormalStateStatus(owner) : null;
    }

    private static CombatSkillCaster ResolveSkillCaster(GameObject owner)
    {
        if (owner == null)
        {
            return null;
        }

        CombatSkillCaster caster = owner.GetComponent<CombatSkillCaster>();
        if (caster != null)
        {
            return caster;
        }

        return owner.GetComponentInChildren<CombatSkillCaster>(true);
    }

    private static CombatHealth ResolveCombatHealth(GameObject owner)
    {
        if (owner == null)
        {
            return null;
        }

        CombatHealth combatHealth = owner.GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            return combatHealth;
        }

        return owner.GetComponentInChildren<CombatHealth>(true);
    }

    private static float ResolveCurrentHealth(CombatHealth combatHealth)
    {
        if (combatHealth == null)
        {
            return 0f;
        }

        if (combatHealth.resourceBank != null)
        {
            return Mathf.Max(0f, combatHealth.resourceBank.currentHealth);
        }

        return Mathf.Max(0f, combatHealth.currentHealth);
    }

    private static float ResolveMaxHealth(CombatHealth combatHealth)
    {
        if (combatHealth == null)
        {
            return 0f;
        }

        if (combatHealth.resourceBank != null)
        {
            return Mathf.Max(0f, combatHealth.resourceBank.maxHealth);
        }

        if (combatHealth.stats != null)
        {
            return Mathf.Max(0f, combatHealth.stats.maxHealth);
        }

        return Mathf.Max(0f, combatHealth.MaxHealthValue);
    }

    private static string BuildNightChildCastKey(GameObject owner, int skillIndex, int castId)
    {
        return $"{owner.GetInstanceID()}:{skillIndex}:{castId}";
    }

    private static HashSet<int> GetOrCreateNightChildTargetSet(string castKey)
    {
        if (!NightChildHitTargetsByCast.TryGetValue(castKey, out HashSet<int> targets))
        {
            targets = new HashSet<int>();
            NightChildHitTargetsByCast[castKey] = targets;
        }

        return targets;
    }
}
