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
    private const float LifeSetAllStatPerTenMaxHealth = 0.10f;
    private const float ManaSetAllStatPerTenMaxMana = 0.10f;
    private const float ManaSetAttributeUnit = 10f;
    private const float ShieldNoDamageDelay = 3f;
    private const float ThornCounterExecutionLockSeconds = 1f;
    private const string DefaultThornCounterBurstPrefabResourcePath = "Prefabs/Effects/Runes/RuneThornCounterBurst";
    private const float ShieldEfficiencyCap = 3f;
    private const float ManaConversionEfficiencyCap = 3f;
    private const float ThornEfficiencyCap = 3f;
    private const float LuckEfficiencyCap = 5f;
    private const float LuckSetChanceMultiplier = 1.5f;
    private const float ThornBaseMonsterDamageReductionMultiplier = 0.75f;
    private const float ThornBaseCounterCooldownSeconds = 4f;
    private const float ThornSetCounterCooldownSeconds = 2f;

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

    private float baseMaxManaSnapshot = -1f;
    private float baseManaRegenSnapshot = -1f;

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
        RebuildFromEquippedRunes();
    }

    private void OnEnable()
    {
        RebuildFromEquippedRunes();
    }

    private void Update()
    {
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
            bonusDamage += ResolveBaseThornDamage(thornCount) * 1.5f;
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

        float availableOverflow = Mathf.Min(beforeOverflow, maxExtraCost);
        float remainingCap = maxExtraCost - availableOverflow;
        float currentMana = beforeMana;
        float availableMana = Mathf.Min(currentMana, remainingCap);

        float consumedOverflow = availableOverflow;
        float requestedManaConsume = availableMana;
        float consumedMana = 0f;

        if (consumedOverflow > 0f)
        {
            manaOverflow = Mathf.Max(0f, manaOverflow - consumedOverflow);
        }

        if (requestedManaConsume > 0f)
        {
            consumedMana = ConsumeVisibleMana(requestedManaConsume);
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
        float effectStrength = Mathf.Clamp01(extraRatio * GetManaConversionEfficiency());
        ManaRuneLog($"Effect ratio={extraRatio:F2}, strength={effectStrength:F2}, conversionEfficiency={GetManaConversionEfficiency():F2}");
        return effectStrength;
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
        return Mathf.Min(1f + Mathf.Max(0f, shieldEfficiencyBonus), ShieldEfficiencyCap);
    }

    public float GetManaConversionEfficiency()
    {
        return Mathf.Min(1f + Mathf.Max(0f, manaConversionEfficiencyBonus), ManaConversionEfficiencyCap);
    }

    public float GetThornEfficiency()
    {
        return Mathf.Min(1f + Mathf.Max(0f, thornEfficiencyBonus), ThornEfficiencyCap);
    }

    public float GetLuckEfficiency()
    {
        return Mathf.Min(1f + Mathf.Max(0f, luckEfficiencyBonus), LuckEfficiencyCap);
    }

    public float GetLuckChanceMultiplier()
    {
        return GetLuckEfficiency() * (GetGlobalRuneCount(RuneType.Luck) >= 5 ? LuckSetChanceMultiplier : 1f);
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

        float damage = ResolveBaseThornDamage(thornCount);
        if (damage <= 0f)
        {
            return;
        }

        attackerHealth.ApplyDirectDamage(damage, gameObject, DamagePopupType.Normal, false);
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
        float baseThornDamage = ResolveBaseThornDamage(thornCount);
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
                attackerHealth.ApplyDirectDamage(burstDamage, gameObject, DamagePopupType.Normal, false);
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
                    targetHealth.ApplyDirectDamage(burstDamage, gameObject, DamagePopupType.Normal, false);
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

    private float ResolveBaseThornDamage(int thornCount)
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
        Debug.Log($"[ThornCounter] {message}", this);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void DevThornCounterEntryLog(GameObject attacker, float damageAmount)
    {
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
                int lifeAttribute = Mathf.FloorToInt((Mathf.Max(0f, combatStats.maxHealth) + appliedHealthBonus) / 10f);
                int lifeStatBonus = Mathf.FloorToInt(lifeAttribute * LifeSetAllStatPerTenMaxHealth);
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
                int manaAttribute = Mathf.FloorToInt(cooldownManager.maxMana / ManaSetAttributeUnit);
                int manaStatBonus = Mathf.FloorToInt(manaAttribute * ManaSetAllStatPerTenMaxMana * GetManaConversionEfficiency());
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

    public float GetIncomingMonsterDamageMultiplier()
    {
        return GetGlobalRuneCount(RuneType.Thorn) >= 1 ? ThornBaseMonsterDamageReductionMultiplier : 1f;
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

}
