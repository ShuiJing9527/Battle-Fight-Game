using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RuneRuntimeState : MonoBehaviour
{
    public struct SoulDropRequest
    {
        public SoulType soulType;
        public int soulPoint;

        public SoulDropRequest(SoulType soulType, int soulPoint)
        {
            this.soulType = soulType;
            this.soulPoint = Mathf.Clamp(soulPoint, 1, 5);
        }
    }

    private const int SkillCount = 4;
    private const int SkillSlotCount = 5;
    private const float ShieldNoDamageDelay = 3f;
    private const float ThornCounterExecutionLockSeconds = 1f;
    private const string DefaultThornCounterBurstPrefabResourcePath = "Prefabs/Effects/Runes/RuneThornCounterBurst";
    private const float ShieldEfficiencyCap = 3f;
    private const float ManaConversionEfficiencyCap = 3f;
    private const float ThornEfficiencyCap = 3f;
    private const float LuckEfficiencyCap = 3f;
    private const float ThornBaseMonsterDamageReductionMultiplier = 0.75f;
    private const float ThornBaseCounterCooldownSeconds = 4f;
    private const float ThornSetCounterCooldownSeconds = 2f;
    private const float LifeSetHealingMultiplier = 1.15f;
    private const float LifeResonanceDamageMultiplier = 1.20f;
    private const float LifeResonanceIncomingMonsterMultiplier = 0.75f;
    private const float LifeResonanceDuration = 8f;
    private const float ShieldSetShieldDamageMultiplier = 0.85f;
    private const float BarrierReconstructionIncomingMonsterMultiplier = 0.60f;
    private const float BarrierReconstructionDuration = 3f;
    private const float BarrierReconstructionCooldown = 15f;
    private const float BarrierReconstructionShieldRatio = 0.30f;
    private const float ManaSetRefundRatio = 0.15f;
    private const float ManaResonanceDamageMultiplier = 1.25f;
    private const float ManaResonanceCooldownRecoveryMultiplier = 1.25f;
    private const float ManaResonanceRuneBonusMultiplier = 1.75f;
    private const float ManaResonanceDuration = 8f;
    private const float ThornDrainCooldown = 1f;
    private const float ThornDrainHealRatio = 0.02f;
    private const float ThornSet4IncomingMonsterMultiplier = 0.70f;
    private const float ThornSet4RetaliationMultiplier = 2f;
    private const float ThornSet4Cooldown = 5f;
    private const float LuckSet2LotteryChance = 0.20f;
    private const float LuckSet4LotteryChance = 0.35f;
    private const float LuckSet5JackpotChance = 0.15f;

    [Header("Thorn Counter")]
    [SerializeField, Min(0.1f)] private float thornCounterBurstRadius = 3f;
    [SerializeField, Min(0f)] private float thornCounterCooldown = ThornBaseCounterCooldownSeconds;
    [SerializeField, Min(0f)] private float thornCounterDamageMultiplier = 3f;
    [SerializeField, Min(0.05f)] private float thornCounterEffectDuration = 0.24f;
    [SerializeField] private LayerMask thornCounterTargetLayers = ~0;
    [SerializeField] private GameObject thornCounterBurstPrefab;

    [Header("Mana Rune")]
    [Range(0f, 1f)]
    [SerializeField] private float manaRuneExtraCostMaxManaPercent = 0.20f;

    [Header("Debug")]
    [SerializeField] private bool runeDebugLog = false;
    [SerializeField] private bool debugRuneThornCounter = false;
    [SerializeField] private bool debugRuneSetBonuses = false;

    private CombatSkillCaster skillCaster;
    private BattleResourceBank resourceBank;
    private CombatStats combatStats;
    private CombatHealth combatHealth;
    private PlayerSkillCooldownManager cooldownManager;
    private bool isRebuildingFromEquippedRunes;

    private readonly Dictionary<RuneType, int>[] skillRuneCounts = new Dictionary<RuneType, int>[SkillCount];
    private readonly Dictionary<RuneType, int> globalRuneCounts = new Dictionary<RuneType, int>();
    private readonly List<int>[] pendingSkillFirstHitCastIds = new List<int>[SkillCount];
    private readonly int[] nextSkillCastIds = new int[SkillCount];

    private float lastMonsterDamageTime = float.NegativeInfinity;
    private bool shieldGeneratedSinceLastMonsterDamage;
    private float thornCounterReadyTime;
    private bool thornCounterInProgress;
    private bool suppressReactiveAutoEffects;
    private Coroutine thornCounterReleaseCoroutine;
    private float manaOverflow;
    private float lifeResonanceEndTime;
    private float lastHealthRatio = -1f;
    private float barrierReconstructionEndTime;
    private float nextBarrierReconstructionReadyTime;
    private Coroutine barrierReconstructionCoroutine;
    private float arcaneResonanceEndTime;
    private float arcaneResonanceExtraManaSpent;
    private float nextThornDrainReadyTime;
    private float nextThornBacklashReadyTime;
    private float pendingLuckThornMimicMultiplier;
    private bool isResolvingLuckLottery;

    private float shieldEfficiencyBonus;
    private float manaConversionEfficiencyBonus;
    private float thornEfficiencyBonus;
    private float luckEfficiencyBonus;

    private float appliedHealthBonus;
    private float appliedPhysicalAttackBonus;
    private float appliedPhysicalDefenseBonus;
    private float appliedSpecialAttackBonus;
    private float appliedSpecialDefenseBonus;
    private float appliedSpeedBonus;
    private float appliedLuckRuneBonus;
    private float appliedAllStatsLuckBonus;
    private float appliedOtherLuckBonus;

    private float appliedMaxManaBonus;
    private float appliedManaRegenBonus;
    private int lastPermanentGrowthFrame = -1;

    private float baseMaxManaSnapshot = -1f;
    private float baseManaRegenSnapshot = -1f;

    private bool lifeSet2Active;
    private bool lifeSet4Active;
    private bool lifeSet5Active;
    private bool shieldSet2Active;
    private bool shieldSet4Active;
    private bool shieldSet5Active;
    private bool manaSet2Active;
    private bool manaSet4Active;
    private bool manaSet5Active;
    private bool thornSet2Active;
    private bool thornSet4Active;
    private bool thornSet5Active;
    private bool luckSet2Active;
    private bool luckSet4Active;
    private bool luckSet5Active;

    private void Awake()
    {
        skillCaster = GetComponent<CombatSkillCaster>();
        resourceBank = GetComponent<BattleResourceBank>();
        combatStats = GetComponent<CombatStats>();
        combatHealth = GetComponent<CombatHealth>();
        cooldownManager = GetComponent<PlayerSkillCooldownManager>();

        for (int i = 0; i < SkillCount; i++)
        {
            skillRuneCounts[i] = new Dictionary<RuneType, int>();
            pendingSkillFirstHitCastIds[i] = new List<int>();
        }

        thornCounterReadyTime = 0f;
        lastMonsterDamageTime = Time.time;
        shieldGeneratedSinceLastMonsterDamage = false;
        RuntimeRuneScaling.SetDebugLogging(runeDebugLog);
        RebuildFromEquippedRunes();
    }

    private void OnEnable()
    {
        RuntimeRuneScaling.SetDebugLogging(runeDebugLog);
        RebuildFromEquippedRunes();
    }

    private void Update()
    {
        RefreshTimedRuneSetStates();
        ClampCurrentShieldToRuneCap();

        if (GetGlobalRuneCount(RuneType.Shield) < 1 || resourceBank == null)
        {
            return;
        }

        if (shieldGeneratedSinceLastMonsterDamage)
        {
            return;
        }

        if (Time.time < lastMonsterDamageTime + ShieldNoDamageDelay)
        {
            return;
        }

        float shieldTarget = ResolveOwnerMaxHealth() * 0.50f;
        float currentShield = resourceBank.CurrentShield;
        if (shieldTarget <= 0f || currentShield >= shieldTarget)
        {
            return;
        }

        float shieldAmount = shieldTarget - currentShield;
        shieldGeneratedSinceLastMonsterDamage = true;
        resourceBank.AddShield(shieldAmount);
        ClampCurrentShieldToRuneCap();
        DebugLog($"[RuneRuntimeState] Shield rune passive granted missingShield={shieldAmount:F2}, targetShield={shieldTarget:F2}");
    }

    public void RebuildFromEquippedRunes()
    {
        if (isRebuildingFromEquippedRunes)
        {
            return;
        }

        isRebuildingFromEquippedRunes = true;
        try
        {
        ClearRuneCounts();

        if (skillCaster != null)
        {
            for (int skillIndex = 0; skillIndex < SkillCount; skillIndex++)
            {
                BattleSkill skill = skillCaster.TryGetSkillRaw(skillIndex);
                if (skill == null || skill.equippedRunes == null)
                {
                    continue;
                }

                int slotLimit = Mathf.Min(Mathf.Max(0, skill.runeSlotCount), Mathf.Min(SkillSlotCount, skill.equippedRunes.Length));
                for (int slotIndex = 0; slotIndex < slotLimit; slotIndex++)
                {
                    RuneDefinition rune = skill.equippedRunes[slotIndex];
                    if (rune == null || !rune.IsConfigured() || rune.runeType == RuneType.None)
                    {
                        continue;
                    }

                    int currentCount = 0;
                    skillRuneCounts[skillIndex].TryGetValue(rune.runeType, out currentCount);
                    currentCount++;
                    skillRuneCounts[skillIndex][rune.runeType] = currentCount;

                    int globalCurrent = 0;
                    globalRuneCounts.TryGetValue(rune.runeType, out globalCurrent);
                    globalRuneCounts[rune.runeType] = globalCurrent + 1;
                }
            }
        }

        RebuildRuneSetTierState();
        ApplyGlobalPassiveBonuses();
        }
        finally
        {
            isRebuildingFromEquippedRunes = false;
        }
    }

    public int GetSkillRuneCount(int skillIndex, RuneType runeType)
    {
        if (skillIndex < 0 || skillIndex >= skillRuneCounts.Length || runeType == RuneType.None)
        {
            return 0;
        }

        int count = 0;
        skillRuneCounts[skillIndex].TryGetValue(runeType, out count);
        return count;
    }

    public int GetGlobalRuneCount(RuneType runeType)
    {
        if (runeType == RuneType.None)
        {
            return 0;
        }

        int count = 0;
        globalRuneCounts.TryGetValue(runeType, out count);
        return count;
    }

    public int GetTotalEquippedRuneCount()
    {
        return
            GetGlobalRuneCount(RuneType.Life) +
            GetGlobalRuneCount(RuneType.Shield) +
            GetGlobalRuneCount(RuneType.Mana) +
            GetGlobalRuneCount(RuneType.Thorn) +
            GetGlobalRuneCount(RuneType.Luck);
    }

    public int NotifySkillCastStarted(int skillIndex)
    {
        if (skillIndex < 0 || skillIndex >= pendingSkillFirstHitCastIds.Length)
        {
            return -1;
        }

        int castId = ++nextSkillCastIds[skillIndex];
        pendingSkillFirstHitCastIds[skillIndex].Add(castId);

        int lifeCount = GetGlobalRuneCount(RuneType.Life);
        if (lifeCount >= 2)
        {
            ApplyLifeRuneCastHeal(lifeCount);
        }

        TryResolveLuckLottery();

        return castId;
    }

    public float GetOutgoingDamageMultiplier(int skillIndex)
    {
        float multiplier = 1f;
        int shieldCount = GetGlobalRuneCount(RuneType.Shield);
        if (shieldCount >= 2 && resourceBank != null && resourceBank.CurrentShield > 0f)
        {
            multiplier *= 1.3f;
        }

        if (lifeSet4Active && IsLifeResonanceOffenseActive())
        {
            multiplier *= LifeResonanceDamageMultiplier;
        }

        if (IsArcaneResonanceActive)
        {
            multiplier *= ManaResonanceDamageMultiplier;
        }

        return multiplier;
    }

    public float ConsumeFirstHitBonusDamage(int skillIndex)
    {
        return ConsumeFirstHitBonusDamage(skillIndex, -1);
    }

    public float ConsumeFirstHitBonusDamage(int skillIndex, int castId)
    {
        if (skillIndex < 0 || skillIndex >= pendingSkillFirstHitCastIds.Length)
        {
            return 0f;
        }

        List<int> pendingCastIds = pendingSkillFirstHitCastIds[skillIndex];
        if (pendingCastIds == null || pendingCastIds.Count == 0)
        {
            return 0f;
        }

        if (castId > 0)
        {
            if (!pendingCastIds.Remove(castId))
            {
                return 0f;
            }
        }
        else
        {
            pendingCastIds.RemoveAt(0);
        }

        float bonusDamage = 0f;
        int lifeCount = GetGlobalRuneCount(RuneType.Life);
        if (lifeCount >= 3)
        {
            bonusDamage += ResolveOwnerMaxHealth() * 0.05f;
        }

        int shieldCount = GetGlobalRuneCount(RuneType.Shield);
        if (shieldCount >= 5 && resourceBank != null)
        {
            bonusDamage += resourceBank.CurrentShield * 0.15f * GetShieldEfficiency();
        }

        int thornCount = GetGlobalRuneCount(RuneType.Thorn);
        if (thornCount >= 4)
        {
            float thornDamage = ResolveCurrentThornDamage(thornCount) * 1.5f;
            bonusDamage += thornDamage;
            TryTriggerThornDrain(thornDamage, "ThornSkillFirstHit");
        }

        if (pendingLuckThornMimicMultiplier > 0f)
        {
            float thornDamage = ResolveCurrentThornDamage(thornCount) * pendingLuckThornMimicMultiplier;
            pendingLuckThornMimicMultiplier = 0f;
            bonusDamage += thornDamage;
            TryTriggerThornDrain(thornDamage, "LuckThornMimic");
        }

        DebugLog($"[RuneRuntimeState] First-hit bonus skill={skillIndex} damage={bonusDamage:F2}");
        return bonusDamage;
    }

    public void PrepareManaBurstForSkillCast(int skillIndex, float baseCost)
    {
        // Deprecated: mana rune 4-piece no longer uses first-hit deferred consumption.
    }

    public float TriggerManaRuneCastEffect(int skillIndex)
    {
        int currentSkillManaCount = skillIndex >= 0 && skillIndex < skillRuneCounts.Length
            ? GetSkillRuneCount(skillIndex, RuneType.Mana)
            : 0;
        int globalManaCount = GetGlobalRuneCount(RuneType.Mana);
        ManaRuneLog($"Trigger entered. skill={SkillIndexToName(skillIndex)}, manaRuneCount={currentSkillManaCount}, globalManaRuneCount={globalManaCount}");

        if (skillIndex < 0 || skillIndex >= skillRuneCounts.Length)
        {
            ManaRuneLog($"Skipped. reason=invalid-skill-index-{skillIndex}");
            return 0f;
        }

        int manaCount = globalManaCount;
        if (manaCount < 4)
        {
            ManaRuneLog("Skipped. reason=global-mana-rune-count-below-4");
            return 0f;
        }

        float maxMana = ResolveOwnerMaxMana(out string maxManaSource);
        ManaRuneLog($"maxMana source={maxManaSource}, value={maxMana:F2}");
        float maxExtraCost = Mathf.Max(0f, maxMana * Mathf.Clamp01(manaRuneExtraCostMaxManaPercent));
        ManaRuneLog($"Cast triggered. skill={SkillIndexToName(skillIndex)}, maxMana={maxMana:F2}, cap={maxExtraCost:F2}");
        if (maxExtraCost <= 0f)
        {
            ManaRuneLog("Skipped. reason=non-positive-cap");
            return 0f;
        }

        float beforeOverflow = Mathf.Max(0f, manaOverflow);
        float beforeMana = ResolveCurrentVisibleMana();
        ManaRuneLog($"overflow={beforeOverflow:F2}, currentEnergy={beforeMana:F2}");
        ManaRuneLog($"Before. overflow={beforeOverflow:F2}, mana={beforeMana:F2}");

        float consumedMana = 0f;
        float requestedManaConsume = Mathf.Min(beforeMana, maxExtraCost);
        if (requestedManaConsume > 0f)
        {
            consumedMana = ConsumeVisibleMana(requestedManaConsume);
        }

        float remainingCap = Mathf.Max(0f, maxExtraCost - consumedMana);
        float consumedOverflow = Mathf.Min(beforeOverflow, remainingCap);
        if (consumedOverflow > 0f)
        {
            manaOverflow = Mathf.Max(0f, manaOverflow - consumedOverflow);
        }

        float actualExtraConsumed = consumedOverflow + consumedMana;
        ManaRuneLog($"Consumed overflow={consumedOverflow:F2}, mana={consumedMana:F2}, total={actualExtraConsumed:F2}");
        if (actualExtraConsumed <= 0f)
        {
            ManaRuneLog("Skipped. reason=no-available-extra-resource");
            return 0f;
        }

        ManaRuneLog($"After. overflow={Mathf.Max(0f, manaOverflow):F2}, mana={ResolveCurrentVisibleMana():F2}");

        float extraRatio = maxExtraCost > 0f ? actualExtraConsumed / maxExtraCost : 0f;
        NotifyManaRuneExtraManaSpent(actualExtraConsumed);
        float effectStrength = Mathf.Max(0f, extraRatio * GetManaConversionEfficiency() * GetManaRuneBonusMultiplier());
        ManaRuneLog($"Effect ratio={extraRatio:F2}, strength={effectStrength:F2}, conversionEfficiency={GetManaConversionEfficiency():F2}");
        return effectStrength;
    }

    public void NotifyBaseSkillManaSpent(int skillIndex, float baseManaCost)
    {
        if (!manaSet2Active || resourceBank == null)
        {
            return;
        }

        float refund = Mathf.Max(0f, baseManaCost) * ManaSetRefundRatio;
        if (refund <= 0f)
        {
            return;
        }

        RecoverMana(refund);
    }

    public void NotifyMonsterDamagedBySkill(int skillIndex, CombatHealth target, float actualDamage)
    {
        // Reserved for future per-hit effects that need actual post-defense damage.
    }

    public void NotifyIncomingMonsterDamage(GameObject attacker, float damageAmount)
    {
        DevThornCounterEntryLog(attacker, damageAmount);

        if (suppressReactiveAutoEffects)
        {
            ThornCounterLog("Skipped incoming monster damage auto-effects because suppressReactiveAutoEffects is active.");
            DevThornCounterLog("Trigger rejected. reason=suppressReactiveAutoEffects");
            return;
        }

        if (damageAmount <= 0f)
        {
            DevThornCounterLog($"Trigger rejected. reason=damageAmount<=0 damage={damageAmount:F2}");
            return;
        }

        GameObject resolvedAttacker = ResolveMonsterAttackerObject(attacker);
        DevThornCounterLog($"Incoming monster damage. attacker={(resolvedAttacker != null ? resolvedAttacker.name : "<null>")} rawSource={(attacker != null ? attacker.name : "<null>")} damage={damageAmount:F2}");

        lastMonsterDamageTime = Time.time;
        shieldGeneratedSinceLastMonsterDamage = false;

        int thornCount = GetGlobalRuneCount(RuneType.Thorn);
        DevThornCounterLog($"Active thorn count={thornCount}");
        if (thornCount >= 2)
        {
            ApplyThornRetaliation(resolvedAttacker, thornCount);
        }

        if (thornCount >= 5)
        {
            ThornCounterLog($"Incoming monster damage detected. attacker={(resolvedAttacker != null ? resolvedAttacker.name : "<null>")}, damage={damageAmount:F2}, thornCount={thornCount}");
            TryTriggerThornCounter(resolvedAttacker, thornCount);
        }
        else
        {
            DevThornCounterLog($"Trigger rejected. reason=thornCount<{5}");
        }
    }

    public float GetShieldGainMultiplier()
    {
        int shieldCount = GetGlobalRuneCount(RuneType.Shield);
        if (shieldCount < 4)
        {
            return 1f;
        }

        return 2f * GetShieldEfficiency();
    }

    public float GetShieldEfficiency()
    {
        return shieldSet5Active ? Mathf.Min(1f + Mathf.Max(0f, shieldEfficiencyBonus), ShieldEfficiencyCap) : 1f;
    }

    public float GetManaConversionEfficiency()
    {
        return manaSet5Active ? Mathf.Min(1f + Mathf.Max(0f, manaConversionEfficiencyBonus), ManaConversionEfficiencyCap) : 1f;
    }

    public float GetThornEfficiency()
    {
        return thornSet5Active ? Mathf.Min(1f + Mathf.Max(0f, thornEfficiencyBonus), ThornEfficiencyCap) : 1f;
    }

    public float GetLuckEfficiency()
    {
        return luckSet5Active ? Mathf.Min(1f + Mathf.Max(0f, luckEfficiencyBonus), LuckEfficiencyCap) : 1f;
    }

    public float GetLuckChanceMultiplier()
    {
        return luckSet5Active ? GetLuckEfficiency() : 1f;
    }

    public float GetHealingReceivedMultiplier()
    {
        return lifeSet2Active ? LifeSetHealingMultiplier : 1f;
    }

    public float GetShieldDamageTakenMultiplier()
    {
        return shieldSet2Active && resourceBank != null && resourceBank.CurrentShield > 0f
            ? ShieldSetShieldDamageMultiplier
            : 1f;
    }

    public bool IsBarrierReconstructionActive => shieldSet4Active && Time.time < barrierReconstructionEndTime;
    public bool IsArcaneResonanceActive => manaSet4Active && Time.time < arcaneResonanceEndTime;
    public bool IsKnockbackImmuneFromRunes => IsBarrierReconstructionActive;
    public bool IsHitStunImmuneFromRunes => IsBarrierReconstructionActive;

    public float GetSkillCooldownRecoveryMultiplier()
    {
        return IsArcaneResonanceActive ? ManaResonanceCooldownRecoveryMultiplier : 1f;
    }

    public float GetManaRuneBonusMultiplier()
    {
        return IsArcaneResonanceActive ? ManaResonanceRuneBonusMultiplier : 1f;
    }

    public void AddManaOverflow(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
        {
            return;
        }

        int manaCount = GetGlobalRuneCount(RuneType.Mana);
        if (manaCount < 3 || resourceBank == null)
        {
            return;
        }

        float cap = resourceBank.maxEnergy * (manaCount >= 5 ? 3f : 2f);
        manaOverflow = Mathf.Clamp(manaOverflow + amount, 0f, Mathf.Max(0f, cap));
        DebugLog($"[RuneRuntimeState] Mana overflow={manaOverflow:F2}/{cap:F2}");
    }

    public int ModifyGrowthSoulPointOnDrop(int originalPoint)
    {
        int luckCount = GetGlobalRuneCount(RuneType.Luck);
        if (luckCount < 2)
        {
            return Mathf.Clamp(originalPoint, 1, 5);
        }

        float baseChance = 0.30f;
        int triggers = RollRepeatableChance(baseChance, GetLuckChanceMultiplier());
        if (triggers <= 0)
        {
            return Mathf.Clamp(originalPoint, 1, 5);
        }

        return Mathf.Clamp(originalPoint + triggers, 1, 5);
    }

    public int GetExtraGrowthSoulDropsOnKill()
    {
        int luckCount = GetGlobalRuneCount(RuneType.Luck);
        if (luckCount < 3)
        {
            return 0;
        }

        float baseChance = 0.25f;
        return RollRepeatableChance(baseChance, GetLuckChanceMultiplier());
    }

    public int GetSoulPickupCopyCount()
    {
        int luckCount = GetGlobalRuneCount(RuneType.Luck);
        if (luckCount < 4)
        {
            return 0;
        }

        float baseChance = 0.20f;
        return RollRepeatableChance(baseChance, GetLuckChanceMultiplier());
    }

    public int GetSoulPickupCopyPoint()
    {
        return GetGlobalRuneCount(RuneType.Luck) >= 4 ? 2 : 1;
    }

    public void AppendKillBonusSoulDrops(MonsterRank rank, List<SoulDropRequest> extraDrops)
    {
        if (extraDrops == null)
        {
            return;
        }

        int lifeCount = GetGlobalRuneCount(RuneType.Life);
        if (lifeCount >= 1)
        {
            extraDrops.Add(new SoulDropRequest(SoulType.Growth, 1));
            extraDrops.Add(new SoulDropRequest(SoulType.Life, 1));
        }

        int shieldCount = GetGlobalRuneCount(RuneType.Shield);
        if (shieldCount >= 3)
        {
            extraDrops.Add(new SoulDropRequest(SoulType.Function, 1));
        }

        int manaCount = GetGlobalRuneCount(RuneType.Mana);
        if (manaCount >= 3)
        {
            extraDrops.Add(new SoulDropRequest(SoulType.Energy, 1));
        }

        int extraGrowthDrops = GetExtraGrowthSoulDropsOnKill();
        for (int i = 0; i < extraGrowthDrops; i++)
        {
            extraDrops.Add(new SoulDropRequest(SoulType.Growth, 1));
        }

        if (IsEliteOrBoss(rank))
        {
            int luckCount = GetGlobalRuneCount(RuneType.Luck);
            if (luckCount >= 5)
            {
                for (int i = 0; i < 5; i++)
                {
                    extraDrops.Add(new SoulDropRequest(SoulType.Growth, 1));
                }
            }

            ApplyEliteOrBossKillPermanentGrowth();
        }
    }

    public void NotifyBossKilled()
    {
        ApplyEliteOrBossKillPermanentGrowth();
    }

    public void NotifySoulApplied(SoulType soulType, int soulPoint)
    {
        if (resourceBank == null)
        {
            return;
        }

        if (soulType == SoulType.Energy && GetGlobalRuneCount(RuneType.Mana) >= 5)
        {
            float bonusRecovery = Mathf.Clamp(soulPoint, 1, 5) * 10f * 3f;
            ApplyBonusManaRecovery(bonusRecovery);
        }

        if (soulType == SoulType.Function)
        {
            ClampCurrentShieldToRuneCap();
        }
    }

    private void ApplyEliteOrBossKillPermanentGrowth()
    {
        if (lastPermanentGrowthFrame == Time.frameCount)
        {
            return;
        }

        lastPermanentGrowthFrame = Time.frameCount;

        if (GetGlobalRuneCount(RuneType.Shield) >= 5)
        {
            shieldEfficiencyBonus = Mathf.Min(shieldEfficiencyBonus + 0.10f, ShieldEfficiencyCap - 1f);
        }

        if (GetGlobalRuneCount(RuneType.Mana) >= 5)
        {
            manaConversionEfficiencyBonus = Mathf.Min(manaConversionEfficiencyBonus + 0.10f, ManaConversionEfficiencyCap - 1f);
        }

        if (GetGlobalRuneCount(RuneType.Thorn) >= 5)
        {
            thornEfficiencyBonus = Mathf.Min(thornEfficiencyBonus + 0.10f, ThornEfficiencyCap - 1f);
        }

        if (GetGlobalRuneCount(RuneType.Luck) >= 5)
        {
            luckEfficiencyBonus = Mathf.Min(luckEfficiencyBonus + 0.10f, LuckEfficiencyCap - 1f);
        }

        ApplyGlobalPassiveBonuses();
    }

    private void ApplyLifeRuneCastHeal(int lifeCount)
    {
        if (resourceBank == null || combatHealth == null)
        {
            return;
        }

        float healRatio = 0.05f;
        float maxHealth = ResolveOwnerMaxHealth();
        float healAmount = maxHealth * healRatio;
        if (healAmount <= 0f)
        {
            return;
        }

        float before = combatHealth.currentHealth;
        combatHealth.Heal(healAmount);
        float after = combatHealth.currentHealth;
        float actualHeal = Mathf.Max(0f, after - before);
        float overflow = Mathf.Max(0f, healAmount - actualHeal);

        if (overflow > 0f && lifeCount >= 4)
        {
            float cap = maxHealth;
            float currentShield = resourceBank.CurrentShield;
            float addAmount = Mathf.Max(0f, Mathf.Min(overflow, cap - currentShield));
            if (addAmount > 0f)
            {
                resourceBank.AddShield(addAmount);
                ClampCurrentShieldToRuneCap();
            }
        }
    }

    private void ApplyThornRetaliation(GameObject attacker, int thornCount)
    {
        if (attacker == null)
        {
            return;
        }

        CombatHealth attackerHealth = attacker.GetComponentInParent<CombatHealth>();
        if (attackerHealth == null || attackerHealth == combatHealth)
        {
            return;
        }

        float damage = ResolveCurrentThornDamage(thornCount);
        if (damage <= 0f)
        {
            return;
        }

        BattleDamage retaliationDamage = new BattleDamage(damage, BattleDamageType.Physical, gameObject)
        {
            bypassAmbientAffinity = true,
            debugTag = "BaseThornReflect"
        };
        attackerHealth.ApplyDirectDamage(retaliationDamage, DamagePopupType.Normal);
        TryTriggerThornDrain(damage, "BaseThornReflect");
    }

    private void TryTriggerThornCounter(GameObject attacker, int thornCount)
    {
        if (attacker == null)
        {
            ThornCounterLog("Skipped thorn burst: attacker is null.");
            DevThornCounterLog("Trigger rejected. reason=attacker is null");
            return;
        }

        if (thornCounterInProgress)
        {
            ThornCounterLog("Skipped thorn burst: thornCounterInProgress is true.");
            DevThornCounterLog("Trigger rejected. reason=thornCounterInProgress");
            return;
        }

        if (Time.time < thornCounterReadyTime)
        {
            ThornCounterLog($"Skipped thorn burst: cooldown not ready. now={Time.time:F2}, readyTime={thornCounterReadyTime:F2}");
            DevThornCounterLog($"Trigger rejected. reason=cooldown not ready now={Time.time:F2} readyTime={thornCounterReadyTime:F2}");
            return;
        }

        CombatHealth attackerHealth = attacker.GetComponentInParent<CombatHealth>();
        if (attackerHealth == null)
        {
            ThornCounterLog($"Skipped thorn burst: attacker CombatHealth not found on {attacker.name}.");
            DevThornCounterLog($"Trigger rejected. reason=attacker CombatHealth missing attacker={attacker.name}");
            return;
        }

        BeginThornCounterExecution(thornCount);
        Vector3 burstCenter = ResolveThornCounterBurstCenter();
        float resolvedCooldown = ResolveThornCounterCooldown();
        DevThornCounterLog($"Trigger accepted. attacker={attackerHealth.name} cooldown={resolvedCooldown:F2}");
        ThornCounterLog($"Triggering thorn burst counter. center={burstCenter}, radius={thornCounterBurstRadius:F2}, cooldown={resolvedCooldown:F2}s");
        bool triggered = ExecuteThornCounterBurst(attackerHealth, thornCount);
        if (!triggered)
        {
            ThornCounterLog("Thorn burst counter found no valid monster targets or could not execute.");
            DevThornCounterLog("Trigger rejected after begin. reason=ExecuteThornCounterBurst returned false");
            EndThornCounterExecution();
        }
        else
        {
            ThornCounterLog("Thorn burst counter executed successfully.");
        }
    }

    private bool ExecuteThornCounterBurst(CombatHealth attackerHealth, int thornCount)
    {
        if (attackerHealth == null)
        {
            ThornCounterLog("Skipped thorn burst execution: attackerHealth is null.");
            DevThornCounterLog("Burst rejected. reason=attackerHealth is null");
            return false;
        }

        float radius = Mathf.Max(0.1f, thornCounterBurstRadius);
        float burstMultiplier = Mathf.Max(0f, thornCounterDamageMultiplier);
        float baseThornDamage = ResolveCurrentThornDamage(thornCount);
        float burstDamage = baseThornDamage * burstMultiplier;
        Vector3 burstCenter = ResolveThornCounterBurstCenter();
        DevThornCounterLog($"Burst center={burstCenter}, radius={radius:F2}");
        DevThornCounterLog($"Base thorn damage={baseThornDamage:F2}, multiplier={burstMultiplier:F2}, final damage={burstDamage:F2}");
        if (burstDamage <= 0f)
        {
            ThornCounterLog("Skipped thorn burst execution: burstDamage <= 0.");
            DevThornCounterLog("Burst rejected. reason=burstDamage<=0");
            return false;
        }

        GameObject burstPrefab = ResolveThornCounterBurstPrefab();
        if (burstPrefab != null)
        {
            GameObject burstInstance = Instantiate(burstPrefab, burstCenter, Quaternion.identity);
            RuneThornCounterEffect burstEffect = burstInstance.GetComponent<RuneThornCounterEffect>();
            if (burstEffect != null)
            {
                burstEffect.Configure(radius, thornCounterEffectDuration);
            }
        }

        Collider[] hits = Physics.OverlapSphere(burstCenter, radius, thornCounterTargetLayers, QueryTriggerInteraction.Collide);
        DevThornCounterLog($"Colliders found={hits.Length}");

        HashSet<CombatHealth> damagedCombatTargets = new HashSet<CombatHealth>();
        int hitCount = 0;
        suppressReactiveAutoEffects = true;

        try
        {
            if (!damagedCombatTargets.Contains(attackerHealth) && attackerHealth != combatHealth)
            {
                float beforeHealth = ResolveCurrentHealth(attackerHealth);
                BattleDamage burstRetaliationDamage = new BattleDamage(burstDamage, BattleDamageType.Physical, gameObject)
                {
                    bypassAmbientAffinity = true,
                    debugTag = "ThornCounter"
                };
                attackerHealth.ApplyDirectDamage(burstRetaliationDamage, DamagePopupType.Normal);
                float afterHealth = ResolveCurrentHealth(attackerHealth);
                damagedCombatTargets.Add(attackerHealth);
                hitCount++;
                DevThornCounterLog($"Applied damage to {attackerHealth.name} result={Mathf.Max(0f, beforeHealth - afterHealth):F2}");
            }

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (!BattleTargetUtility.IsMonster(hit, transform))
                {
                    continue;
                }

                CombatHealth targetHealth = BattleTargetUtility.GetMonsterCombatHealth(hit, transform);
                if (targetHealth != null)
                {
                    if (targetHealth == combatHealth || !damagedCombatTargets.Add(targetHealth))
                    {
                        continue;
                    }

                    float beforeHealth = ResolveCurrentHealth(targetHealth);
                    BattleDamage areaRetaliationDamage = new BattleDamage(burstDamage, BattleDamageType.Physical, gameObject)
                    {
                        bypassAmbientAffinity = true,
                        debugTag = "ThornCounter"
                    };
                    targetHealth.ApplyDirectDamage(areaRetaliationDamage, DamagePopupType.Normal);
                    float afterHealth = ResolveCurrentHealth(targetHealth);
                    hitCount++;
                    DevThornCounterLog($"Applied damage to {targetHealth.name} result={Mathf.Max(0f, beforeHealth - afterHealth):F2}");
                    continue;
                }
            }
        }
        finally
        {
            suppressReactiveAutoEffects = false;
        }

        if (hitCount > 0)
        {
            TryTriggerThornDrain(burstDamage, "ThornCounter");
        }

        DevThornCounterLog($"Unique enemies found={damagedCombatTargets.Count}");
        ThornCounterLog($"Thorn burst dealt {burstDamage:F2} damage in radius {radius:F2}, targetsHit={hitCount}.");
        return hitCount > 0;
    }

    private void BeginThornCounterExecution(int thornCount)
    {
        thornCounterInProgress = true;
        thornCounterReadyTime = Time.time + Mathf.Max(0f, ResolveThornCounterCooldown());
        ThornCounterLog($"Counter execution lock started. readyTime={thornCounterReadyTime:F2}");
        if (thornCounterReleaseCoroutine != null)
        {
            StopCoroutine(thornCounterReleaseCoroutine);
        }

        thornCounterReleaseCoroutine = StartCoroutine(ReleaseThornCounterExecutionAfterDelay(ThornCounterExecutionLockSeconds));
    }

    private void EndThornCounterExecution()
    {
        thornCounterInProgress = false;
        ThornCounterLog("Counter execution lock cleared early.");
        if (thornCounterReleaseCoroutine != null)
        {
            StopCoroutine(thornCounterReleaseCoroutine);
            thornCounterReleaseCoroutine = null;
        }
    }

    private IEnumerator ReleaseThornCounterExecutionAfterDelay(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, delay));
        thornCounterInProgress = false;
        thornCounterReleaseCoroutine = null;
        ThornCounterLog("Counter execution lock released after delay.");
    }

    private GameObject ResolveThornCounterBurstPrefab()
    {
        if (thornCounterBurstPrefab != null)
        {
            return thornCounterBurstPrefab;
        }

        thornCounterBurstPrefab = Resources.Load<GameObject>(DefaultThornCounterBurstPrefabResourcePath);
        if (thornCounterBurstPrefab == null)
        {
            ThornCounterLog($"Failed to load thorn counter prefab from Resources/{DefaultThornCounterBurstPrefabResourcePath}.");
        }

        return thornCounterBurstPrefab;
    }

    private Vector3 ResolveThornCounterBurstCenter()
    {
        Transform centerTransform = combatHealth != null ? combatHealth.transform : transform;
        return centerTransform != null ? centerTransform.position : Vector3.zero;
    }

    private int ResolveBestSkillIndexForRune(RuneType runeType)
    {
        int bestIndex = -1;
        int bestCount = 0;
        for (int i = 0; i < SkillCount; i++)
        {
            int count = GetSkillRuneCount(i, runeType);
            if (count > bestCount)
            {
                bestCount = count;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private float ResolveCurrentThornDamage(int thornCount)
    {
        if (combatStats == null)
        {
            DevThornCounterLog("Base thorn damage resolved to 0 because combatStats is null.");
            return 0f;
        }

        float attributeSum =
            Mathf.Max(0f, combatStats.maxHealth) * 0.10f +
            Mathf.Max(0f, combatStats.physicalAttack) +
            Mathf.Max(0f, combatStats.physicalDefense) +
            Mathf.Max(0f, combatStats.specialAttack) +
            Mathf.Max(0f, combatStats.specialDefense) +
            Mathf.Max(0f, combatStats.speed) +
            Mathf.Max(0f, combatStats.luck);

        float thornDamageMultiplier = 1f;
        if (thornCount >= 3)
        {
            thornDamageMultiplier += 1f;
        }

        if (thornCount >= 5)
        {
            thornDamageMultiplier += 1f;
        }

        return attributeSum * 0.30f * thornDamageMultiplier * GetThornEfficiency();
    }

    public float GetIncomingMonsterDamageMultiplier()
    {
        return GetIncomingMonsterDamageMultiplier(null, 0f);
    }

    public float GetIncomingMonsterDamageMultiplier(GameObject attacker, float damageBeforeRune)
    {
        float multiplier = 1f;
        if (GetGlobalRuneCount(RuneType.Thorn) >= 1)
        {
            multiplier *= ThornBaseMonsterDamageReductionMultiplier;
        }

        if (lifeSet4Active && IsLifeResonanceDefenseActive())
        {
            multiplier *= LifeResonanceIncomingMonsterMultiplier;
        }

        if (IsBarrierReconstructionActive)
        {
            multiplier *= BarrierReconstructionIncomingMonsterMultiplier;
        }

        if (thornSet4Active && Time.time >= nextThornBacklashReadyTime)
        {
            nextThornBacklashReadyTime = Time.time + ThornSet4Cooldown;
            multiplier *= ThornSet4IncomingMonsterMultiplier;
            TriggerThornSet4Backlash(attacker);
        }

        return multiplier;
    }

    private void TriggerThornSet4Backlash(GameObject attacker)
    {
        RuneSetBonusLog(
            $"event=ThornBacklashTriggered incomingBefore=unknown incomingAfter=unknown thornDamage={ResolveCurrentThornDamage(GetGlobalRuneCount(RuneType.Thorn)) * ThornSet4RetaliationMultiplier:F2} cooldown={ThornSet4Cooldown:F2}");

        GameObject resolvedAttacker = ResolveMonsterAttackerObject(attacker);
        CombatHealth attackerHealth = resolvedAttacker != null ? resolvedAttacker.GetComponentInParent<CombatHealth>() : null;
        if (attackerHealth == null || attackerHealth == combatHealth)
        {
            return;
        }

        float damage = ResolveCurrentThornDamage(GetGlobalRuneCount(RuneType.Thorn)) * ThornSet4RetaliationMultiplier;
        if (damage <= 0f)
        {
            return;
        }

        suppressReactiveAutoEffects = true;
        try
        {
            BattleDamage backlashDamage = new BattleDamage(damage, BattleDamageType.Physical, gameObject)
            {
                bypassAmbientAffinity = true,
                debugTag = "ThornSet4Retaliation"
            };
            attackerHealth.ApplyDirectDamage(backlashDamage, DamagePopupType.Normal);
        }
        finally
        {
            suppressReactiveAutoEffects = false;
        }

        TryTriggerThornDrain(damage, "ThornSet4Retaliation");
    }

    private void ApplyBonusManaRecovery(float amount)
    {
        if (resourceBank == null)
        {
            return;
        }

        float previousEnergy = resourceBank.currentEnergy;
        resourceBank.currentEnergy = Mathf.Min(resourceBank.maxEnergy, resourceBank.currentEnergy + Mathf.Max(0f, amount));
        float overflow = Mathf.Max(0f, amount - (resourceBank.currentEnergy - previousEnergy));
        if (overflow > 0f)
        {
            AddManaOverflow(overflow);
        }
    }

    public void RecoverMana(float amount)
    {
        ApplyBonusManaRecovery(amount);
    }

    private void NotifyManaRuneExtraManaSpent(float amount)
    {
        if (!manaSet4Active)
        {
            return;
        }

        float spent = Mathf.Max(0f, amount);
        if (spent <= 0f)
        {
            return;
        }

        arcaneResonanceExtraManaSpent += spent;
        float threshold = ResolveOwnerMaxMana(out _) * manaRuneExtraCostMaxManaPercent;
        RuneSetBonusLog($"event=ArcaneResonanceProgress extraManaSpent={spent:F2} accumulated={arcaneResonanceExtraManaSpent:F2} threshold={threshold:F2}");
        if (threshold > 0f && arcaneResonanceExtraManaSpent >= threshold)
        {
            arcaneResonanceExtraManaSpent = 0f;
            arcaneResonanceEndTime = Time.time + ManaResonanceDuration;
            RuneSetBonusLog($"event=ArcaneResonanceStarted duration={ManaResonanceDuration:F2}");
        }
    }

    private void RebuildRuneSetTierState()
    {
        int lifeCount = GetGlobalRuneCount(RuneType.Life);
        int shieldCount = GetGlobalRuneCount(RuneType.Shield);
        int manaCount = GetGlobalRuneCount(RuneType.Mana);
        int thornCount = GetGlobalRuneCount(RuneType.Thorn);
        int luckCount = GetGlobalRuneCount(RuneType.Luck);

        lifeSet2Active = lifeCount >= 2;
        lifeSet4Active = lifeCount >= 4;
        lifeSet5Active = lifeCount >= 5;
        shieldSet2Active = shieldCount >= 2;
        shieldSet4Active = shieldCount >= 4;
        shieldSet5Active = shieldCount >= 5;
        manaSet2Active = manaCount >= 2;
        manaSet4Active = manaCount >= 4;
        manaSet5Active = manaCount >= 5;
        thornSet2Active = thornCount >= 2;
        thornSet4Active = thornCount >= 4;
        thornSet5Active = thornCount >= 5;
        luckSet2Active = luckCount >= 2;
        luckSet4Active = luckCount >= 4;
        luckSet5Active = luckCount >= 5;

        if (!lifeSet4Active)
        {
            EndLifeResonance("Unequipped");
        }

        if (!shieldSet4Active)
        {
            EndBarrierReconstruction("Unequipped");
        }

        if (!manaSet4Active)
        {
            arcaneResonanceExtraManaSpent = 0f;
            arcaneResonanceEndTime = 0f;
        }

        if (!thornSet4Active)
        {
            nextThornBacklashReadyTime = 0f;
        }

        if (!luckSet2Active)
        {
            pendingLuckThornMimicMultiplier = 0f;
        }

        RuneSetBonusLog(
            $"event=RuneSetStateRebuilt lifeCount={lifeCount} shieldCount={shieldCount} manaCount={manaCount} thornCount={thornCount} luckCount={luckCount} " +
            $"lifeTiers={BuildTierLabel(lifeSet2Active, lifeSet4Active, lifeSet5Active)} shieldTiers={BuildTierLabel(shieldSet2Active, shieldSet4Active, shieldSet5Active)} " +
            $"manaTiers={BuildTierLabel(manaSet2Active, manaSet4Active, manaSet5Active)} thornTiers={BuildTierLabel(thornSet2Active, thornSet4Active, thornSet5Active)} luckTiers={BuildTierLabel(luckSet2Active, luckSet4Active, luckSet5Active)}");
    }

    private void RefreshTimedRuneSetStates()
    {
        UpdateLifeResonanceCrossing();

        if (lifeResonanceEndTime > 0f && Time.time >= lifeResonanceEndTime)
        {
            EndLifeResonance("Expired");
        }

        if (barrierReconstructionEndTime > 0f && Time.time >= barrierReconstructionEndTime)
        {
            barrierReconstructionEndTime = 0f;
        }

        if (arcaneResonanceEndTime > 0f && Time.time >= arcaneResonanceEndTime)
        {
            arcaneResonanceEndTime = 0f;
        }
    }

    private void UpdateLifeResonanceCrossing()
    {
        if (!lifeSet4Active)
        {
            lastHealthRatio = ResolveCurrentHealthRatio();
            return;
        }

        float currentRatio = ResolveCurrentHealthRatio();
        if (lastHealthRatio >= 0f && lastHealthRatio < 0.5f && currentRatio >= 0.5f)
        {
            lifeResonanceEndTime = Time.time + LifeResonanceDuration;
            RuneSetBonusLog($"event=LifeResonanceStarted previousHpRatio={lastHealthRatio:F3} currentHpRatio={currentRatio:F3} duration={LifeResonanceDuration:F2}");
        }

        lastHealthRatio = currentRatio;
    }

    private bool IsLifeResonanceOffenseActive()
    {
        return Time.time < lifeResonanceEndTime || ResolveCurrentHealthRatio() >= 0.5f;
    }

    private bool IsLifeResonanceDefenseActive()
    {
        return Time.time < lifeResonanceEndTime || ResolveCurrentHealthRatio() < 0.5f;
    }

    private void EndLifeResonance(string reason)
    {
        if (lifeResonanceEndTime <= 0f)
        {
            return;
        }

        lifeResonanceEndTime = 0f;
        RuneSetBonusLog($"event=LifeResonanceEnded reason={reason}");
    }

    public void NotifyShieldBrokenByMonsterDamage(float shieldBefore)
    {
        if (!shieldSet4Active || shieldBefore <= 0f || Time.time < nextBarrierReconstructionReadyTime)
        {
            return;
        }

        barrierReconstructionEndTime = Time.time + BarrierReconstructionDuration;
        nextBarrierReconstructionReadyTime = Time.time + BarrierReconstructionCooldown;
        RuneSetBonusLog($"event=BarrierReconstructionStarted shieldBefore={shieldBefore:F2} shieldAfter=0 cooldown={BarrierReconstructionCooldown:F2}");

        if (barrierReconstructionCoroutine != null)
        {
            StopCoroutine(barrierReconstructionCoroutine);
        }

        barrierReconstructionCoroutine = StartCoroutine(CompleteBarrierReconstructionAfterDelay());
    }

    private IEnumerator CompleteBarrierReconstructionAfterDelay()
    {
        yield return new WaitForSeconds(BarrierReconstructionDuration);
        barrierReconstructionCoroutine = null;

        if (!shieldSet4Active || combatHealth == null || combatHealth.IsDead || resourceBank == null)
        {
            yield break;
        }

        float baseShield = ResolveOwnerMaxHealth() * BarrierReconstructionShieldRatio;
        float before = resourceBank.CurrentShield;
        resourceBank.AddShield(baseShield);
        ClampCurrentShieldToRuneCap();
        float finalShield = Mathf.Max(0f, resourceBank.CurrentShield - before);
        barrierReconstructionEndTime = 0f;
        RuneSetBonusLog($"event=BarrierReconstructionCompleted baseShield={baseShield:F2} finalShield={finalShield:F2}");
    }

    private void EndBarrierReconstruction(string reason)
    {
        barrierReconstructionEndTime = 0f;
        if (barrierReconstructionCoroutine != null)
        {
            StopCoroutine(barrierReconstructionCoroutine);
            barrierReconstructionCoroutine = null;
        }
    }

    private bool TryTriggerThornDrain(float thornDamage, string source)
    {
        if (!thornSet2Active || thornDamage <= 0f || Time.time < nextThornDrainReadyTime || combatHealth == null)
        {
            return false;
        }

        nextThornDrainReadyTime = Time.time + ThornDrainCooldown;
        combatHealth.Heal(ResolveOwnerMaxHealth() * ThornDrainHealRatio);
        return true;
    }

    private void TryResolveLuckLottery()
    {
        if (!luckSet2Active || isResolvingLuckLottery)
        {
            return;
        }

        float baseChance = luckSet4Active ? LuckSet4LotteryChance : LuckSet2LotteryChance;
        float finalChance = Mathf.Clamp01(baseChance * GetLuckChanceMultiplier());
        bool success = Random.value < finalChance;
        RuneSetBonusLog($"event=LuckLotteryRolled baseChance={baseChance:F2} efficiency={GetLuckChanceMultiplier():F2} finalChance={finalChance:F2} success={success}");
        if (!success)
        {
            return;
        }

        isResolvingLuckLottery = true;
        try
        {
            List<RuneType> pool = BuildLuckLotteryPool();
            bool jackpot = luckSet5Active && Random.value < Mathf.Clamp01(LuckSet5JackpotChance * GetLuckChanceMultiplier());
            if (jackpot)
            {
                if (pool.Count == 0)
                {
                    ApplyLuckFallbackBlessing();
                }
                else
                {
                    for (int i = 0; i < pool.Count; i++)
                    {
                        ApplyLuckBlessing(pool[i], 1f);
                    }
                }

                RuneSetBonusLog($"event=LuckLotteryResult pool={BuildRuneTypeList(pool)} result=Jackpot doubleDraw=false jackpot=true");
                return;
            }

            if (pool.Count == 0)
            {
                ApplyLuckFallbackBlessing();
                RuneSetBonusLog("event=LuckLotteryResult pool=None result=Fallback doubleDraw=false jackpot=false");
                return;
            }

            RuneType first = pool[Random.Range(0, pool.Count)];
            ApplyLuckBlessing(first, 1f);
            string result = first.ToString();
            bool doubleDraw = luckSet4Active;
            if (doubleDraw)
            {
                if (pool.Count >= 2)
                {
                    List<RuneType> secondPool = new List<RuneType>(pool);
                    secondPool.Remove(first);
                    RuneType second = secondPool[Random.Range(0, secondPool.Count)];
                    ApplyLuckBlessing(second, 1f);
                    result += "+" + second;
                }
                else
                {
                    ApplyLuckBlessing(first, 0.5f);
                    result += "+" + first + "(50%)";
                }
            }

            RuneSetBonusLog($"event=LuckLotteryResult pool={BuildRuneTypeList(pool)} result={result} doubleDraw={doubleDraw} jackpot=false");
        }
        finally
        {
            isResolvingLuckLottery = false;
        }
    }

    private List<RuneType> BuildLuckLotteryPool()
    {
        List<RuneType> pool = new List<RuneType>();
        if (GetGlobalRuneCount(RuneType.Life) > 0) pool.Add(RuneType.Life);
        if (GetGlobalRuneCount(RuneType.Shield) > 0) pool.Add(RuneType.Shield);
        if (GetGlobalRuneCount(RuneType.Mana) > 0) pool.Add(RuneType.Mana);
        if (GetGlobalRuneCount(RuneType.Thorn) > 0) pool.Add(RuneType.Thorn);
        return pool;
    }

    private void ApplyLuckBlessing(RuneType runeType, float valueScale)
    {
        float scale = Mathf.Max(0f, valueScale);
        switch (runeType)
        {
            case RuneType.Life:
                ApplyLuckLifeBlessing(scale);
                break;
            case RuneType.Shield:
                resourceBank?.AddShield(ResolveOwnerMaxHealth() * 0.20f * scale);
                ClampCurrentShieldToRuneCap();
                break;
            case RuneType.Mana:
                RecoverMana(ResolveOwnerMaxMana(out _) * 0.15f * scale);
                break;
            case RuneType.Thorn:
                pendingLuckThornMimicMultiplier += 2f * scale;
                break;
        }
    }

    private void ApplyLuckLifeBlessing(float scale)
    {
        if (combatHealth == null)
        {
            return;
        }

        float amount = ResolveOwnerMaxHealth() * 0.10f * Mathf.Max(0f, scale);
        bool wasFull = ResolveCurrentHealthRatio() >= 0.999f;
        if (wasFull)
        {
            resourceBank?.AddShield(amount);
            ClampCurrentShieldToRuneCap();
            return;
        }

        combatHealth.Heal(amount);
    }

    private void ApplyLuckFallbackBlessing()
    {
        combatHealth?.Heal(ResolveOwnerMaxHealth() * 0.05f);
        RecoverMana(ResolveOwnerMaxMana(out _) * 0.05f);
    }

    private float ResolveCurrentHealthRatio()
    {
        float maxHealth = ResolveOwnerMaxHealth();
        if (maxHealth <= 0f)
        {
            return 0f;
        }

        float current = resourceBank != null ? resourceBank.currentHealth : (combatHealth != null ? combatHealth.currentHealth : 0f);
        return Mathf.Clamp01(current / maxHealth);
    }

    private static string BuildTierLabel(bool tier2, bool tier4, bool tier5)
    {
        List<string> active = new List<string>();
        if (tier2) active.Add("2");
        if (tier4) active.Add("4");
        if (tier5) active.Add("5");
        return active.Count > 0 ? string.Join("/", active) : "None";
    }

    private static string BuildRuneTypeList(List<RuneType> pool)
    {
        return pool == null || pool.Count == 0 ? "None" : string.Join("/", pool);
    }

    private void ClampManaOverflowToRuneCap()
    {
        int manaCount = GetGlobalRuneCount(RuneType.Mana);
        if (manaCount < 3 || resourceBank == null)
        {
            manaOverflow = 0f;
            return;
        }

        float cap = resourceBank.maxEnergy * (manaCount >= 5 ? 3f : 2f);
        manaOverflow = Mathf.Clamp(manaOverflow, 0f, Mathf.Max(0f, cap));
    }

    private float ResolveOwnerMaxMana(out string source)
    {
        if (cooldownManager == null)
        {
            cooldownManager = GetComponent<PlayerSkillCooldownManager>();
        }

        if (cooldownManager != null && cooldownManager.maxMana > 0f)
        {
            source = "PlayerSkillCooldownManager.maxMana";
            return cooldownManager.maxMana;
        }

        if (resourceBank == null)
        {
            resourceBank = GetComponent<BattleResourceBank>();
        }

        if (resourceBank != null && resourceBank.maxEnergy > 0f)
        {
            source = "BattleResourceBank.maxEnergy";
            return resourceBank.maxEnergy;
        }

        source = "None";
        return 0f;
    }

    private float ResolveCurrentVisibleMana()
    {
        if (cooldownManager == null)
        {
            cooldownManager = GetComponent<PlayerSkillCooldownManager>();
        }

        if (cooldownManager != null)
        {
            return Mathf.Max(0f, cooldownManager.GetCurrentMana());
        }

        if (resourceBank == null)
        {
            resourceBank = GetComponent<BattleResourceBank>();
        }

        return resourceBank != null ? Mathf.Max(0f, resourceBank.currentEnergy) : 0f;
    }

    private float ConsumeVisibleMana(float requestedAmount)
    {
        float clampedRequest = Mathf.Max(0f, requestedAmount);
        if (clampedRequest <= 0f)
        {
            return 0f;
        }

        if (cooldownManager == null)
        {
            cooldownManager = GetComponent<PlayerSkillCooldownManager>();
        }

        if (cooldownManager != null)
        {
            return cooldownManager.TryConsumeAdditionalMana(clampedRequest);
        }

        if (resourceBank == null)
        {
            resourceBank = GetComponent<BattleResourceBank>();
        }

        if (resourceBank == null)
        {
            return 0f;
        }

        float spendAmount = Mathf.Min(Mathf.Max(0f, resourceBank.currentEnergy), clampedRequest);
        if (spendAmount <= 0f)
        {
            return 0f;
        }

        return resourceBank.TrySpendEnergy(spendAmount) ? spendAmount : 0f;
    }

    private void ClampCurrentShieldToRuneCap()
    {
        if (resourceBank == null || GetGlobalRuneCount(RuneType.Shield) < 5)
        {
            return;
        }

        float cap = ResolveOwnerMaxHealth() * 3f;
        if (resourceBank.CurrentShield > cap)
        {
            resourceBank.SetShield(cap);
        }
    }

    private static bool IsEliteOrBoss(MonsterRank rank)
    {
        return rank == MonsterRank.Elite || rank == MonsterRank.Boss;
    }

    public bool IsThornCounterDebugEnabled()
    {
        return debugRuneThornCounter;
    }

    public bool TryGrantRuneForTesting(RuneType runeType, string source = "RuneTestLoadout")
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (runeType == RuneType.None)
        {
            return false;
        }

        if (skillCaster == null)
        {
            skillCaster = GetComponent<CombatSkillCaster>();
        }

        if (skillCaster == null)
        {
            return false;
        }

        RuneInventory inventory = GetComponent<RuneInventory>();
        if (inventory == null)
        {
            inventory = gameObject.AddComponent<RuneInventory>();
        }

        if (!TryResolveRuneTestTargetSkill(out BattleSkill targetSkill))
        {
            return false;
        }

        RuneDefinition displacedRune = null;
        int targetSlotIndex = -1;
        int slotLength = targetSkill.equippedRunes != null ? targetSkill.equippedRunes.Length : 0;
        for (int slotIndex = 0; slotIndex < slotLength; slotIndex++)
        {
            if (targetSkill.equippedRunes[slotIndex] == null)
            {
                targetSlotIndex = slotIndex;
                break;
            }
        }

        if (targetSlotIndex < 0)
        {
            targetSlotIndex = 0;
            displacedRune = targetSkill.equippedRunes != null && targetSkill.equippedRunes.Length > 0
                ? targetSkill.equippedRunes[0]
                : null;
        }

        if (targetSlotIndex < 0 || targetSkill.equippedRunes == null || targetSlotIndex >= targetSkill.equippedRunes.Length)
        {
            return false;
        }

        if (displacedRune != null)
        {
            inventory.AddRune(displacedRune, $"{source}-Displaced");
        }

        RuneDefinition grantedRune = RuneDefinition.CreateDefaultRune(runeType);
        inventory.AddRune(grantedRune, source);
        targetSkill.equippedRunes[targetSlotIndex] = grantedRune;
        inventory.RemoveRune(grantedRune);
        skillCaster.RefreshRuneState();
        RebuildFromEquippedRunes();
        return true;
#else
        return false;
#endif
    }

    private void ThornCounterLog(string message)
    {
        if (!debugRuneThornCounter)
        {
            return;
        }

        Debug.Log($"[Rune][ThornCounter] {message}", this);
    }

    private bool TryResolveRuneTestTargetSkill(out BattleSkill targetSkill)
    {
        targetSkill = null;

        if (skillCaster == null)
        {
            return false;
        }

        BattleSkill fallbackSkill = null;
        int fallbackSlotCount = -1;
        for (int i = 0; i < SkillCount; i++)
        {
            BattleSkill candidate = skillCaster.TryGetSkillRaw(i);
            if (candidate == null || candidate.equippedRunes == null || candidate.equippedRunes.Length <= 0)
            {
                continue;
            }

            for (int slotIndex = 0; slotIndex < candidate.equippedRunes.Length; slotIndex++)
            {
                if (candidate.equippedRunes[slotIndex] == null)
                {
                    targetSkill = candidate;
                    return true;
                }
            }

            if (candidate.equippedRunes.Length > fallbackSlotCount)
            {
                fallbackSlotCount = candidate.equippedRunes.Length;
                fallbackSkill = candidate;
            }
        }

        targetSkill = fallbackSkill;
        return targetSkill != null;
    }

    private GameObject ResolveMonsterAttackerObject(GameObject attacker)
    {
        if (attacker == null)
        {
            return null;
        }

        if (BattleTargetUtility.IsMonster(attacker))
        {
            CombatHealth attackerHealth = attacker.GetComponentInParent<CombatHealth>();
            return attackerHealth != null ? attackerHealth.gameObject : attacker;
        }

        CombatHealth parentHealth = attacker.GetComponentInParent<CombatHealth>();
        if (parentHealth != null && BattleTargetUtility.IsMonster(parentHealth.gameObject))
        {
            return parentHealth.gameObject;
        }

        EnemyController enemyController = attacker.GetComponentInParent<EnemyController>();
        if (enemyController != null)
        {
            return enemyController.gameObject;
        }

        MonsterIdentity identity = attacker.GetComponentInParent<MonsterIdentity>();
        if (identity != null)
        {
            return identity.gameObject;
        }

        return null;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void DevThornCounterLog(string message)
    {
        if (!debugRuneThornCounter)
        {
            return;
        }

        Debug.Log($"[ThornCounter] {message}", this);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void DevThornCounterEntryLog(GameObject attacker, float damageAmount)
    {
        if (!debugRuneThornCounter)
        {
            return;
        }

        Debug.Log(
            $"[ThornCounter] Notify entered. attacker={(attacker != null ? attacker.name : "<null>")}, damage={damageAmount:F2}",
            this);
    }

    private static string SkillIndexToName(int skillIndex)
    {
        return skillIndex switch
        {
            0 => "Q",
            1 => "W",
            2 => "E",
            3 => "R",
            _ => $"Unknown({skillIndex})"
        };
    }

    private void ClearRuneCounts()
    {
        globalRuneCounts.Clear();
        for (int i = 0; i < skillRuneCounts.Length; i++)
        {
            skillRuneCounts[i].Clear();
            pendingSkillFirstHitCastIds[i].Clear();
            nextSkillCastIds[i] = 0;
        }
    }

    private void ApplyGlobalPassiveBonuses()
    {
        RemoveAppliedStatBonuses();

        if (combatStats == null)
        {
            combatStats = GetComponent<CombatStats>();
        }

        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
        }

        if (resourceBank == null)
        {
            resourceBank = GetComponent<BattleResourceBank>();
        }

        if (cooldownManager == null)
        {
            cooldownManager = GetComponent<PlayerSkillCooldownManager>();
        }

        if (combatStats != null)
        {
            int lifeCount = GetGlobalRuneCount(RuneType.Life);
            if (lifeCount >= 5)
            {
                appliedHealthBonus += combatStats.maxHealth * 0.50f;
            }

            int lifeSetCount = GetGlobalRuneCount(RuneType.Life);
            if (lifeSetCount >= 5)
            {
                int lifeStatBonus = Mathf.FloorToInt((Mathf.Max(0f, combatStats.maxHealth) + appliedHealthBonus) / 100f);
                ApplyFlatCombatStatBonuses(0f, lifeStatBonus, lifeStatBonus, lifeStatBonus, lifeStatBonus, lifeStatBonus, 0f);
                appliedAllStatsLuckBonus += 0f;
            }

            int luckCount = GetGlobalRuneCount(RuneType.Luck);
            if (luckCount >= 1)
            {
                appliedLuckRuneBonus += 5f;
            }
        }

        if (cooldownManager != null)
        {
            float baseMaxMana = ResolveCooldownManagerBaseMaxMana();
            float baseManaRegen = ResolveCooldownManagerBaseManaRegen();
            int manaCount = GetGlobalRuneCount(RuneType.Mana);
            if (manaCount >= 1)
            {
                appliedMaxManaBonus = 200f;
            }

            if (manaCount >= 2)
            {
                appliedManaRegenBonus = baseManaRegen * 1.5f;
            }

            cooldownManager.maxMana = Mathf.Max(0f, baseMaxMana + appliedMaxManaBonus);
            cooldownManager.manaRecoverPerSecond = Mathf.Max(0f, baseManaRegen + appliedManaRegenBonus);

            if (resourceBank != null)
            {
                resourceBank.maxEnergy = Mathf.Max(0f, cooldownManager.maxMana);
                resourceBank.currentEnergy = Mathf.Clamp(resourceBank.currentEnergy, 0f, resourceBank.maxEnergy);
                ClampManaOverflowToRuneCap();
            }

            if (manaCount >= 5 && combatStats != null)
            {
                int manaStatBonus = Mathf.FloorToInt(cooldownManager.maxMana / 100f);
                ApplyFlatCombatStatBonuses(0f, manaStatBonus, manaStatBonus, manaStatBonus, manaStatBonus, manaStatBonus, 0f);
                appliedAllStatsLuckBonus += 0f;
            }
        }

        ApplyStoredStatBonuses();
        LogAttributeDiagnostics();

        ClampCurrentShieldToRuneCap();

        if (resourceBank != null)
        {
            resourceBank.SyncHealthFromCombatStats(refillCurrentHealth: false);
        }

        if (combatHealth != null)
        {
            combatHealth.SyncHealthFromStats(refillCurrentHealth: false);
        }
    }

    private void ApplyFlatCombatStatBonuses(
        float healthBonus,
        float physicalAttackBonus,
        float physicalDefenseBonus,
        float specialAttackBonus,
        float specialDefenseBonus,
        float speedBonus,
        float luckBonus)
    {
        appliedHealthBonus += healthBonus;
        appliedPhysicalAttackBonus += physicalAttackBonus;
        appliedPhysicalDefenseBonus += physicalDefenseBonus;
        appliedSpecialAttackBonus += specialAttackBonus;
        appliedSpecialDefenseBonus += specialDefenseBonus;
        appliedSpeedBonus += speedBonus;
        appliedOtherLuckBonus += luckBonus;
    }

    private void ApplyStoredStatBonuses()
    {
        if (combatStats == null)
        {
            return;
        }

        combatStats.maxHealth += appliedHealthBonus;
        combatStats.physicalAttack += appliedPhysicalAttackBonus;
        combatStats.physicalDefense += appliedPhysicalDefenseBonus;
        combatStats.specialAttack += appliedSpecialAttackBonus;
        combatStats.specialDefense += appliedSpecialDefenseBonus;
        combatStats.speed += appliedSpeedBonus;
        combatStats.luck += appliedLuckRuneBonus + appliedAllStatsLuckBonus + appliedOtherLuckBonus;
    }

    private void RemoveAppliedStatBonuses()
    {
        if (combatStats != null)
        {
            combatStats.maxHealth = Mathf.Max(0f, combatStats.maxHealth - appliedHealthBonus);
            combatStats.physicalAttack = Mathf.Max(0f, combatStats.physicalAttack - appliedPhysicalAttackBonus);
            combatStats.physicalDefense = Mathf.Max(0f, combatStats.physicalDefense - appliedPhysicalDefenseBonus);
            combatStats.specialAttack = Mathf.Max(0f, combatStats.specialAttack - appliedSpecialAttackBonus);
            combatStats.specialDefense = Mathf.Max(0f, combatStats.specialDefense - appliedSpecialDefenseBonus);
            combatStats.speed = Mathf.Max(0f, combatStats.speed - appliedSpeedBonus);
            combatStats.luck = Mathf.Max(0f, combatStats.luck - (appliedLuckRuneBonus + appliedAllStatsLuckBonus + appliedOtherLuckBonus));
        }

        if (cooldownManager != null)
        {
            float baseMaxMana = ResolveCooldownManagerBaseMaxMana();
            float baseManaRegen = ResolveCooldownManagerBaseManaRegen();
            cooldownManager.maxMana = Mathf.Max(0f, baseMaxMana);
            cooldownManager.manaRecoverPerSecond = Mathf.Max(0f, baseManaRegen);
        }

        appliedHealthBonus = 0f;
        appliedPhysicalAttackBonus = 0f;
        appliedPhysicalDefenseBonus = 0f;
        appliedSpecialAttackBonus = 0f;
        appliedSpecialDefenseBonus = 0f;
        appliedSpeedBonus = 0f;
        appliedLuckRuneBonus = 0f;
        appliedAllStatsLuckBonus = 0f;
        appliedOtherLuckBonus = 0f;
        appliedMaxManaBonus = 0f;
        appliedManaRegenBonus = 0f;
    }

    private float ResolveCooldownManagerBaseMaxMana()
    {
        if (cooldownManager == null)
        {
            return 0f;
        }

        if (baseMaxManaSnapshot < 0f)
        {
            baseMaxManaSnapshot = Mathf.Max(0f, cooldownManager.maxMana - appliedMaxManaBonus);
        }

        return Mathf.Max(0f, baseMaxManaSnapshot);
    }

    private float ResolveCooldownManagerBaseManaRegen()
    {
        if (cooldownManager == null)
        {
            return 0f;
        }

        if (baseManaRegenSnapshot < 0f)
        {
            baseManaRegenSnapshot = Mathf.Max(0f, cooldownManager.manaRecoverPerSecond - appliedManaRegenBonus);
        }

        return Mathf.Max(0f, baseManaRegenSnapshot);
    }

    private float ResolveOwnerMaxHealth()
    {
        if (combatStats != null)
        {
            return Mathf.Max(0f, combatStats.maxHealth);
        }

        if (resourceBank != null)
        {
            return Mathf.Max(0f, resourceBank.maxHealth);
        }

        if (combatHealth != null)
        {
            return Mathf.Max(0f, combatHealth.MaxHealthValue);
        }

        return 0f;
    }

    private void LogAttributeDiagnostics()
    {
        if (combatStats == null)
        {
            return;
        }

        float finalLuck = Mathf.Max(0f, combatStats.luck);
        float appliedLuckTotal = appliedLuckRuneBonus + appliedAllStatsLuckBonus + appliedOtherLuckBonus;
        float baseLuck = Mathf.Max(0f, finalLuck - appliedLuckTotal);
        string equippedRunes = BuildEquippedRuneSummary();
        Debug.Log(
            $"[AttributeDiag] character={gameObject.name} baseLuck={baseLuck:F2} luckRuneBonus={appliedLuckRuneBonus:F2} allStatsLuckBonus={appliedAllStatsLuckBonus:F2} otherRuneLuckBonus={appliedOtherLuckBonus:F2} buffLuckBonus=0.00 finalLuck={finalLuck:F2} equippedRunes={equippedRunes}",
            this);
    }

    private string BuildEquippedRuneSummary()
    {
        if (skillCaster == null)
        {
            return "null";
        }

        List<string> entries = new List<string>();
        for (int skillIndex = 0; skillIndex < SkillCount; skillIndex++)
        {
            BattleSkill skill = skillCaster.TryGetSkillRaw(skillIndex);
            if (skill == null || skill.equippedRunes == null)
            {
                continue;
            }

            for (int slotIndex = 0; slotIndex < skill.equippedRunes.Length; slotIndex++)
            {
                RuneDefinition rune = skill.equippedRunes[slotIndex];
                if (rune == null)
                {
                    continue;
                }

                entries.Add($"{GetSkillLabel(skillIndex)}:{GetRuneLabel(rune)}");
            }
        }

        return entries.Count > 0 ? string.Join("|", entries) : "empty";
    }

    private string GetSkillLabel(int skillIndex)
    {
        if (skillIndex == 0)
        {
            return "Q";
        }

        if (skillIndex == 1)
        {
            return "W";
        }

        if (skillIndex == 2)
        {
            return "E";
        }

        if (skillIndex == 3)
        {
            return "R";
        }

        return $"Skill{skillIndex}";
    }

    private string GetRuneLabel(RuneDefinition rune)
    {
        if (rune == null)
        {
            return "Empty";
        }

        if (!string.IsNullOrWhiteSpace(rune.runeName))
        {
            return rune.runeName;
        }

        return rune.runeId != 0 ? $"id:{rune.runeId}" : rune.ToString();
    }

    private int RollRepeatableChance(float baseChance, float efficiencyMultiplier)
    {
        float actualChance = Mathf.Max(0f, baseChance) * Mathf.Max(0f, efficiencyMultiplier);
        if (actualChance <= 0f)
        {
            return 0;
        }

        int guaranteedTriggers = Mathf.FloorToInt(actualChance);
        float remainderChance = actualChance - guaranteedTriggers;
        if (Random.value < remainderChance)
        {
            guaranteedTriggers++;
        }

        return guaranteedTriggers;
    }

    private static float ResolveCurrentHealth(CombatHealth health)
    {
        if (health == null)
        {
            return 0f;
        }

        return health.resourceBank != null
            ? Mathf.Max(0f, health.resourceBank.currentHealth)
            : Mathf.Max(0f, health.currentHealth);
    }

    private float ResolveThornCounterCooldown()
    {
        if (GetGlobalRuneCount(RuneType.Thorn) >= 5)
        {
            return ThornSetCounterCooldownSeconds;
        }

        return Mathf.Max(0f, thornCounterCooldown > 0f ? thornCounterCooldown : ThornBaseCounterCooldownSeconds);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void ManaRuneLog(string message)
    {
        if (!runeDebugLog)
        {
            return;
        }

        Debug.Log($"[ManaRune] {message}", this);
    }

    private void DebugLog(string message)
    {
        if (!runeDebugLog)
        {
            return;
        }

        Debug.Log(message, this);
    }

    private void RuneSetBonusLog(string message)
    {
        if (!debugRuneSetBonuses)
        {
            return;
        }

        Debug.Log($"[RuneSetBonusTrace] {message}", this);
    }

}

public static class RuntimeRuneScaling
{
    private static bool debugLoggingEnabled;

    public readonly struct Snapshot : System.IEquatable<Snapshot>
    {
        public readonly int player01EquippedCount;
        public readonly int player02EquippedCount;
        public readonly int totalEquippedCount;
        public readonly float bonusRate;
        public readonly float multiplier;
        public readonly string player01Source;
        public readonly string player02Source;

        public Snapshot(
            int player01EquippedCount,
            int player02EquippedCount,
            string player01Source,
            string player02Source)
        {
            this.player01EquippedCount = Mathf.Max(0, player01EquippedCount);
            this.player02EquippedCount = Mathf.Max(0, player02EquippedCount);
            totalEquippedCount = this.player01EquippedCount + this.player02EquippedCount;
            bonusRate = totalEquippedCount * 0.05f;
            multiplier = 1f + bonusRate;
            this.player01Source = string.IsNullOrWhiteSpace(player01Source) ? "None" : player01Source;
            this.player02Source = string.IsNullOrWhiteSpace(player02Source) ? "None" : player02Source;
        }

        public bool Equals(Snapshot other)
        {
            return player01EquippedCount == other.player01EquippedCount
                && player02EquippedCount == other.player02EquippedCount
                && totalEquippedCount == other.totalEquippedCount
                && Mathf.Approximately(bonusRate, other.bonusRate)
                && Mathf.Approximately(multiplier, other.multiplier)
                && string.Equals(player01Source, other.player01Source, System.StringComparison.Ordinal)
                && string.Equals(player02Source, other.player02Source, System.StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is Snapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = player01EquippedCount;
                hash = (hash * 397) ^ player02EquippedCount;
                hash = (hash * 397) ^ totalEquippedCount;
                hash = (hash * 397) ^ bonusRate.GetHashCode();
                hash = (hash * 397) ^ multiplier.GetHashCode();
                hash = (hash * 397) ^ (player01Source != null ? player01Source.GetHashCode() : 0);
                hash = (hash * 397) ^ (player02Source != null ? player02Source.GetHashCode() : 0);
                return hash;
            }
        }
    }

    public static event System.Action<Snapshot> ScalingChanged;

    private static Snapshot currentSnapshot;
    private static bool hasSnapshot;
    private static bool warnedNoPlayers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        currentSnapshot = default;
        hasSnapshot = false;
        warnedNoPlayers = false;
        ScalingChanged = null;
    }

    public static Snapshot GetCurrentSnapshot()
    {
        if (!hasSnapshot)
        {
            ForceRefresh("LazyInit");
        }

        return currentSnapshot;
    }

    public static int GetTotalEquippedRuneCount()
    {
        return GetCurrentSnapshot().totalEquippedCount;
    }

    public static float GetTotalEquippedRuneBonusRate()
    {
        return GetCurrentSnapshot().bonusRate;
    }

    public static float GetTotalEquippedRuneMultiplier()
    {
        return GetCurrentSnapshot().multiplier;
    }

    public static void ForceRefresh(string reason)
    {
        Snapshot nextSnapshot = BuildSnapshot();
        bool changed = !hasSnapshot || !currentSnapshot.Equals(nextSnapshot);
        currentSnapshot = nextSnapshot;
        hasSnapshot = true;

        LogSnapshot(reason, nextSnapshot);

        if (changed)
        {
            ScalingChanged?.Invoke(nextSnapshot);
        }
    }

    private static Snapshot BuildSnapshot()
    {
        int player01Count = ResolvePlayerEquippedRuneCount("Player01", typeof(Player01SkillController), out string player01Source);
        int player02Count = ResolvePlayerEquippedRuneCount("Player02", typeof(Player2PrototypeController), out string player02Source);

        if (!warnedNoPlayers && player01Count <= 0 && player02Count <= 0)
        {
            warnedNoPlayers = true;
            Debug.LogWarning("[RuntimeRuneScaling] No valid Player01/Player02 equipped rune data found yet. Using 0 until runtime data becomes available.");
        }
        else if (player01Count > 0 || player02Count > 0)
        {
            warnedNoPlayers = false;
        }

        return new Snapshot(player01Count, player02Count, player01Source, player02Source);
    }

    private static int ResolvePlayerEquippedRuneCount(string explicitName, System.Type controllerType, out string source)
    {
        source = "MissingPlayer";
        CombatSkillCaster caster = FindPlayerCaster(explicitName, controllerType);
        if (caster == null)
        {
            return 0;
        }

        return CountEquippedRunes(caster, out source);
    }

    private static CombatSkillCaster FindPlayerCaster(string explicitName, System.Type controllerType)
    {
        CombatSkillCaster[] casters = Object.FindObjectsOfType<CombatSkillCaster>(true);
        for (int i = 0; i < casters.Length; i++)
        {
            CombatSkillCaster candidate = casters[i];
            if (!IsLoadedSceneCaster(candidate))
            {
                continue;
            }

            if (string.Equals(candidate.gameObject.name, explicitName, System.StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        for (int i = 0; i < casters.Length; i++)
        {
            CombatSkillCaster candidate = casters[i];
            if (!IsLoadedSceneCaster(candidate))
            {
                continue;
            }

            if (controllerType != null && candidate.GetComponent(controllerType) != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsLoadedSceneCaster(CombatSkillCaster caster)
    {
        return caster != null
            && caster.gameObject != null
            && caster.gameObject.scene.IsValid()
            && caster.gameObject.scene.isLoaded;
    }

    private static int CountEquippedRunes(CombatSkillCaster caster, out string source)
    {
        source = "CombatSkillCaster.EquippedRunesFallback";
        if (caster == null)
        {
            return 0;
        }

        RuneRuntimeState runtimeState = caster.GetComponent<RuneRuntimeState>()
            ?? caster.GetComponentInParent<RuneRuntimeState>()
            ?? caster.GetComponentInChildren<RuneRuntimeState>(true);
        if (runtimeState != null)
        {
            source = "RuneRuntimeState.TotalEquippedRuneCount";
            return runtimeState.GetTotalEquippedRuneCount();
        }

        int count = 0;
        for (int skillIndex = 0; skillIndex < 4; skillIndex++)
        {
            BattleSkill skill = caster.TryGetSkillRaw(skillIndex);
            if (skill == null || skill.equippedRunes == null)
            {
                continue;
            }

            int slotLimit = Mathf.Min(Mathf.Max(0, skill.runeSlotCount), skill.equippedRunes.Length);
            for (int slotIndex = 0; slotIndex < slotLimit; slotIndex++)
            {
                RuneDefinition rune = skill.equippedRunes[slotIndex];
                if (rune != null && rune.IsConfigured() && rune.runeType != RuneType.None)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static void LogSnapshot(string reason, Snapshot snapshot)
    {
        if (!debugLoggingEnabled)
        {
            return;
        }

        Debug.Log($"[RuntimeRuneScaling] reason={reason} Player01 equipped={snapshot.player01EquippedCount} source={snapshot.player01Source}");
        Debug.Log($"[RuntimeRuneScaling] reason={reason} Player02 equipped={snapshot.player02EquippedCount} source={snapshot.player02Source}");
        Debug.Log($"[RuntimeRuneScaling] reason={reason} Total equipped={snapshot.totalEquippedCount}");
        Debug.Log($"[RuntimeRuneScaling] reason={reason} Bonus rate={snapshot.bonusRate:P0}");
        Debug.Log($"[RuntimeRuneScaling] reason={reason} Final multiplier={snapshot.multiplier:F2}");
    }

    public static void SetDebugLogging(bool enabled)
    {
        debugLoggingEnabled = enabled;
    }
}
