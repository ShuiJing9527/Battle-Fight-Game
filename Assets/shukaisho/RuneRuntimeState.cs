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
    private const float ManaSetAllStatPerTenMaxMana = 0.20f;
    private const float ManaSetAttributeUnit = 10f;
    private const float ShieldNoDamageDelay = 3f;
    private const float ThornCounterBaseCooldown = 4f;
    private const float ThornCounterSetCooldown = 2f;
    private const float ThornCounterExecutionLockSeconds = 1f;

    [Header("Thorn Counter")]
    [SerializeField, Min(0.1f)] private float thornCounterBurstRadius = 3f;
    [SerializeField] private GameObject thornCounterBurstPrefab;

    [Header("Debug")]
    [SerializeField] private bool runeDebugLog = false;
    [SerializeField] private bool debugRuneThornCounter = false;

    private CombatSkillCaster skillCaster;
    private BattleResourceBank resourceBank;
    private CombatStats combatStats;
    private CombatHealth combatHealth;
    private PlayerSkillCooldownManager cooldownManager;

    private readonly Dictionary<RuneType, int>[] skillRuneCounts = new Dictionary<RuneType, int>[SkillCount];
    private readonly Dictionary<RuneType, int> globalMaxCounts = new Dictionary<RuneType, int>();
    private readonly List<int>[] pendingSkillFirstHitCastIds = new List<int>[SkillCount];
    private readonly int[] nextSkillCastIds = new int[SkillCount];

    private float lastMonsterDamageTime = float.NegativeInfinity;
    private bool shieldGeneratedSinceLastMonsterDamage;
    private float thornCounterReadyTime;
    private bool thornCounterInProgress;
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
    private float appliedLuckBonus;

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

        float shieldPercent = GetGlobalRuneCount(RuneType.Shield) >= 5 ? 0.50f : 0.15f;
        float shieldAmount = ResolveOwnerMaxHealth() * shieldPercent;
        if (shieldAmount <= 0f)
        {
            return;
        }

        shieldGeneratedSinceLastMonsterDamage = true;
        resourceBank.AddShield(shieldAmount);
        DebugLog($"[RuneRuntimeState] Shield rune passive granted shield={shieldAmount:F2}");
    }

    public void RebuildFromEquippedRunes()
    {
        ClearRuneCounts();

        if (skillCaster != null)
        {
            for (int skillIndex = 0; skillIndex < SkillCount; skillIndex++)
            {
                BattleSkill skill = skillCaster.GetSkill(skillIndex);
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
                    globalMaxCounts.TryGetValue(rune.runeType, out globalCurrent);
                    if (currentCount > globalCurrent)
                    {
                        globalMaxCounts[rune.runeType] = currentCount;
                    }
                }
            }
        }

        ApplyGlobalPassiveBonuses();
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
        globalMaxCounts.TryGetValue(runeType, out count);
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

        int lifeCount = GetSkillRuneCount(skillIndex, RuneType.Life);
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
            multiplier *= shieldCount >= 5 ? 1.5f : 1.25f;
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
        int lifeCount = GetSkillRuneCount(skillIndex, RuneType.Life);
        if (lifeCount >= 4)
        {
            float ratio = lifeCount >= 5 ? 0.05f : 0.01f;
            bonusDamage += ResolveOwnerMaxHealth() * ratio;
        }

        int shieldCount = GetSkillRuneCount(skillIndex, RuneType.Shield);
        if (shieldCount >= 4 && resourceBank != null)
        {
            float ratio = shieldCount >= 5 ? 0.25f : 0.10f;
            bonusDamage += resourceBank.CurrentShield * ratio * GetShieldEfficiency();
        }

        int thornCount = GetSkillRuneCount(skillIndex, RuneType.Thorn);
        if (thornCount >= 3)
        {
            float ratio = thornCount >= 5 ? 3.0f : 1.5f;
            bonusDamage += ResolveBaseThornDamage(thornCount) * ratio;
        }

        int manaCount = GetSkillRuneCount(skillIndex, RuneType.Mana);
        if (manaCount >= 4)
        {
            bonusDamage += ConsumeManaBurstBonus(manaCount);
        }

        DebugLog($"[RuneRuntimeState] First-hit bonus skill={skillIndex} damage={bonusDamage:F2}");
        return bonusDamage;
    }

    public void NotifyMonsterDamagedBySkill(int skillIndex, CombatHealth target, float actualDamage)
    {
        // Reserved for future per-hit effects that need actual post-defense damage.
    }

    public void NotifyIncomingMonsterDamage(GameObject attacker, float damageAmount)
    {
        if (damageAmount <= 0f)
        {
            return;
        }

        lastMonsterDamageTime = Time.time;
        shieldGeneratedSinceLastMonsterDamage = false;

        int thornCount = GetGlobalRuneCount(RuneType.Thorn);
        if (thornCount >= 2)
        {
            ApplyThornRetaliation(attacker, thornCount);
        }

        if (thornCount >= 4)
        {
            ThornCounterLog($"Incoming monster damage detected. attacker={(attacker != null ? attacker.name : "<null>")}, damage={damageAmount:F2}, thornCount={thornCount}");
            TryTriggerThornCounter(attacker, thornCount);
        }
    }

    public float GetShieldGainMultiplier()
    {
        int shieldCount = GetGlobalRuneCount(RuneType.Shield);
        if (shieldCount < 3)
        {
            return 1f;
        }

        float baseMultiplier = shieldCount >= 5 ? 3f : 2f;
        return baseMultiplier * GetShieldEfficiency();
    }

    public float GetShieldEfficiency()
    {
        return 1f + Mathf.Max(0f, shieldEfficiencyBonus);
    }

    public float GetManaConversionEfficiency()
    {
        return 1f + Mathf.Max(0f, manaConversionEfficiencyBonus);
    }

    public float GetThornEfficiency()
    {
        return 1f + Mathf.Max(0f, thornEfficiencyBonus);
    }

    public float GetLuckEfficiency()
    {
        return 1f + Mathf.Max(0f, luckEfficiencyBonus);
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

        float cap = resourceBank.maxEnergy * (manaCount >= 5 ? 2f : 1f);
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

        float baseChance = luckCount >= 5 ? 0.35f : 0.20f;
        int triggers = RollRepeatableChance(baseChance, GetLuckEfficiency());
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

        float baseChance = luckCount >= 5 ? 0.25f : 0.15f;
        return RollRepeatableChance(baseChance, GetLuckEfficiency());
    }

    public int GetSoulPickupCopyCount()
    {
        int luckCount = GetGlobalRuneCount(RuneType.Luck);
        if (luckCount < 4)
        {
            return 0;
        }

        float baseChance = luckCount >= 5 ? 0.25f : 0.10f;
        return RollRepeatableChance(baseChance, GetLuckEfficiency());
    }

    public int GetSoulPickupCopyPoint()
    {
        return GetGlobalRuneCount(RuneType.Luck) >= 5 ? 3 : 1;
    }

    public void AppendKillBonusSoulDrops(MonsterRank rank, List<SoulDropRequest> extraDrops)
    {
        if (extraDrops == null)
        {
            return;
        }

        int lifeCount = GetGlobalRuneCount(RuneType.Life);
        if (lifeCount >= 5)
        {
            extraDrops.Add(new SoulDropRequest(SoulType.Growth, 1));
            extraDrops.Add(new SoulDropRequest(SoulType.Life, 1));
        }

        int shieldCount = GetGlobalRuneCount(RuneType.Shield);
        if (shieldCount >= 5)
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

        if (rank == MonsterRank.Boss)
        {
            int luckCount = GetGlobalRuneCount(RuneType.Luck);
            if (luckCount >= 5)
            {
                extraDrops.Add(new SoulDropRequest(SoulType.Growth, 1));
                extraDrops.Add(new SoulDropRequest(SoulType.Growth, 1));
            }

            ApplyBossKillPermanentGrowth();
        }
    }

    public void NotifyBossKilled()
    {
        ApplyBossKillPermanentGrowth();
    }

    public void NotifySoulApplied(SoulType soulType, int soulPoint)
    {
        // Reserved hook for future UI/debug. Soul copy is handled inside BattleResourceBank.
    }

    private void ApplyBossKillPermanentGrowth()
    {
        if (GetGlobalRuneCount(RuneType.Shield) >= 5)
        {
            shieldEfficiencyBonus += 0.10f;
        }

        if (GetGlobalRuneCount(RuneType.Mana) >= 5)
        {
            manaConversionEfficiencyBonus += 0.10f;
        }

        if (GetGlobalRuneCount(RuneType.Thorn) >= 5)
        {
            thornEfficiencyBonus += 0.15f;
        }

        if (GetGlobalRuneCount(RuneType.Luck) >= 5)
        {
            luckEfficiencyBonus += 0.05f;
        }

        ApplyGlobalPassiveBonuses();
    }

    private void ApplyLifeRuneCastHeal(int lifeCount)
    {
        if (resourceBank == null || combatHealth == null)
        {
            return;
        }

        float healRatio = lifeCount >= 5 ? 0.05f : 0.01f;
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

        if (overflow > 0f && lifeCount >= 3)
        {
            float capRatio = lifeCount >= 5 ? 1f : 0.5f;
            float cap = maxHealth * capRatio;
            float currentShield = resourceBank.CurrentShield;
            float addAmount = Mathf.Max(0f, Mathf.Min(overflow, cap - currentShield));
            if (addAmount > 0f)
            {
                resourceBank.AddShield(addAmount);
            }
        }
    }

    private float ConsumeManaBurstBonus(int manaCount)
    {
        if (resourceBank == null)
        {
            return 0f;
        }

        float consumeCap = manaCount >= 5 ? 200f : 100f;
        float multiplier = manaCount >= 5 ? 4f : 3f;

        float availableOverflow = Mathf.Min(manaOverflow, consumeCap);
        float remainingCap = consumeCap - availableOverflow;
        float availableEnergy = Mathf.Min(resourceBank.currentEnergy, remainingCap);
        float totalConsumed = availableOverflow + availableEnergy;
        if (totalConsumed <= 0f)
        {
            return 0f;
        }

        manaOverflow -= availableOverflow;
        resourceBank.currentEnergy = Mathf.Max(0f, resourceBank.currentEnergy - availableEnergy);
        return totalConsumed * multiplier * GetManaConversionEfficiency();
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

        float ratio = thornCount >= 5 ? 0.40f : 0.15f;
        float damage = ResolveBaseThornDamage(thornCount) * ratio;
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
            return;
        }

        if (thornCounterInProgress)
        {
            ThornCounterLog("Skipped thorn burst: thornCounterInProgress is true.");
            return;
        }

        if (Time.time < thornCounterReadyTime)
        {
            ThornCounterLog($"Skipped thorn burst: cooldown not ready. now={Time.time:F2}, readyTime={thornCounterReadyTime:F2}");
            return;
        }

        CombatHealth attackerHealth = attacker.GetComponentInParent<CombatHealth>();
        if (attackerHealth == null)
        {
            ThornCounterLog($"Skipped thorn burst: attacker CombatHealth not found on {attacker.name}.");
            return;
        }

        BeginThornCounterExecution(thornCount);
        ThornCounterLog($"Triggering thorn burst counter. center={attackerHealth.name}, radius={thornCounterBurstRadius:F2}, cooldown={(thornCount >= 5 ? ThornCounterSetCooldown : ThornCounterBaseCooldown):F2}s");
        bool triggered = ExecuteThornCounterBurst(attackerHealth, thornCount);
        if (!triggered)
        {
            ThornCounterLog("Thorn burst counter found no valid monster targets or could not execute.");
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
            return false;
        }

        float radius = Mathf.Max(0.1f, thornCounterBurstRadius);
        float burstMultiplier = thornCount >= 5 ? 4f : 2f;
        float burstDamage = ResolveBaseThornDamage(thornCount) * burstMultiplier;
        if (burstDamage <= 0f)
        {
            ThornCounterLog("Skipped thorn burst execution: burstDamage <= 0.");
            return false;
        }

        if (thornCounterBurstPrefab != null)
        {
            Instantiate(thornCounterBurstPrefab, attackerHealth.transform.position, Quaternion.identity);
        }

        Collider[] hits = Physics.OverlapSphere(
            attackerHealth.transform.position,
            radius,
            ~0,
            QueryTriggerInteraction.Collide);

        HashSet<CombatHealth> damagedCombatTargets = new HashSet<CombatHealth>();
        HashSet<EnemyHealth> damagedLegacyTargets = new HashSet<EnemyHealth>();
        int hitCount = 0;

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

                targetHealth.ApplyDirectDamage(burstDamage, gameObject, DamagePopupType.Normal, false);
                hitCount++;
                continue;
            }

            EnemyHealth legacyHealth = BattleTargetUtility.GetMonsterLegacyHealth(hit, transform);
            if (legacyHealth != null && damagedLegacyTargets.Add(legacyHealth))
            {
                legacyHealth.TakeDamage(Mathf.RoundToInt(burstDamage), gameObject);
                hitCount++;
            }
        }

        ThornCounterLog($"Thorn burst dealt {burstDamage:F2} damage in radius {radius:F2}, targetsHit={hitCount}.");
        return hitCount > 0;
    }

    private void BeginThornCounterExecution(int thornCount)
    {
        thornCounterInProgress = true;
        thornCounterReadyTime = Time.time + (thornCount >= 5 ? ThornCounterSetCooldown : ThornCounterBaseCooldown);
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
            return 0f;
        }

        float attributeSum =
            Mathf.Max(0f, combatStats.maxHealth) +
            Mathf.Max(0f, combatStats.physicalAttack) +
            Mathf.Max(0f, combatStats.physicalDefense) +
            Mathf.Max(0f, combatStats.specialAttack) +
            Mathf.Max(0f, combatStats.specialDefense) +
            Mathf.Max(0f, combatStats.speed) +
            Mathf.Max(0f, combatStats.luck);

        float thornDamage = attributeSum * GetThornEfficiency();
        if (thornCount >= 5)
        {
            thornDamage *= 2f;
        }

        return thornDamage;
    }

    public bool IsThornCounterDebugEnabled()
    {
        return debugRuneThornCounter;
    }

    private void ThornCounterLog(string message)
    {
        if (!debugRuneThornCounter)
        {
            return;
        }

        Debug.Log($"[Rune][ThornCounter] {message}", this);
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
        globalMaxCounts.Clear();
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
            if (lifeCount >= 1)
            {
                float healthBonusPercent = lifeCount >= 5 ? 0.50f : 0.10f;
                appliedHealthBonus += combatStats.maxHealth * healthBonusPercent;
            }

            int lifeSetCount = GetGlobalRuneCount(RuneType.Life);
            if (lifeSetCount >= 5)
            {
                int lifeAttribute = Mathf.FloorToInt(ResolveOwnerMaxHealth() / 10f);
                int lifeStatBonus = Mathf.FloorToInt(lifeAttribute * LifeSetAllStatPerTenMaxHealth);
                ApplyFlatCombatStatBonuses(0f, lifeStatBonus, lifeStatBonus, lifeStatBonus, lifeStatBonus, lifeStatBonus, lifeStatBonus);
            }

            int luckCount = GetGlobalRuneCount(RuneType.Luck);
            if (luckCount >= 1)
            {
                appliedLuckBonus += luckCount >= 5 ? 3f : 1f;
            }
        }

        if (cooldownManager != null)
        {
            float baseMaxMana = ResolveCooldownManagerBaseMaxMana();
            float baseManaRegen = ResolveCooldownManagerBaseManaRegen();
            int manaCount = GetGlobalRuneCount(RuneType.Mana);
            if (manaCount >= 1)
            {
                appliedMaxManaBonus = manaCount >= 5 ? 400f : 150f;
            }

            if (manaCount >= 2)
            {
                float manaRegenMultiplier = manaCount >= 5 ? 4f : 2.5f;
                appliedManaRegenBonus = baseManaRegen * (manaRegenMultiplier - 1f);
            }

            cooldownManager.maxMana = Mathf.Max(0f, baseMaxMana + appliedMaxManaBonus);
            cooldownManager.manaRecoverPerSecond = Mathf.Max(0f, baseManaRegen + appliedManaRegenBonus);

            if (resourceBank != null)
            {
                resourceBank.maxEnergy = Mathf.Max(resourceBank.maxEnergy, cooldownManager.maxMana);
                resourceBank.currentEnergy = Mathf.Clamp(resourceBank.currentEnergy, 0f, resourceBank.maxEnergy);
            }

            if (manaCount >= 5 && combatStats != null)
            {
                int manaAttribute = Mathf.FloorToInt(cooldownManager.maxMana / ManaSetAttributeUnit);
                int manaStatBonus = Mathf.FloorToInt(manaAttribute * ManaSetAllStatPerTenMaxMana * GetManaConversionEfficiency());
                ApplyFlatCombatStatBonuses(0f, manaStatBonus, manaStatBonus, manaStatBonus, manaStatBonus, manaStatBonus, manaStatBonus);
            }
        }

        ApplyStoredStatBonuses();

        if (combatHealth != null)
        {
            int thornCount = GetGlobalRuneCount(RuneType.Thorn);
            if (thornCount >= 1)
            {
                float incomingMultiplier = thornCount >= 5 ? 0.65f : 0.85f;
                combatHealth.SetIncomingDamageMultiplier("RuneThornReduction", incomingMultiplier);
            }
            else
            {
                combatHealth.RemoveIncomingDamageMultiplier("RuneThornReduction");
            }
        }

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
        appliedLuckBonus += luckBonus;
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
        combatStats.luck += appliedLuckBonus;
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
            combatStats.luck = Mathf.Max(0f, combatStats.luck - appliedLuckBonus);
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
        appliedLuckBonus = 0f;
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

    private void DebugLog(string message)
    {
        if (!runeDebugLog)
        {
            return;
        }

        Debug.Log(message, this);
    }
}
