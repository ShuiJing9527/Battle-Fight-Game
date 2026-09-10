using System;
using System.Collections.Generic;
using UnityEngine;

public class CombatHealth : MonoBehaviour
{
    private const float RuneFlatDamageCooldownWindow = 0.15f;
    private const float TwinLifestealCooldownWindow = 0.15f;
    private const float NightChildFavorableLifestealRate = 0.15f;
    private const float NightChildLifestealMaxHealthRatio = 0.08f;
    private const float EliteShieldPressureMultiplier = 1.35f;
    private const float BossShieldPressureMultiplier = 1.50f;
    private static int nextDamagePacketId;
    private static readonly Dictionary<int, float> NextLifestealTimeByAttacker = new Dictionary<int, float>();
    private static int finalRushEnemyHitsOnPlayer;
    private static float finalRushEnemyDamageToShield;
    private static float finalRushEnemyDamageToHp;

    [Header("Combat Balance")]
    [SerializeField] private bool enableCombatBalanceLogs = false;
    [Header("生命")]
    public CombatStats stats;
    public BattleResourceBank resourceBank;
    [Min(0f)] public float currentHealth = 3f;
    public bool destroyOnDeath = true;

    [Header("Animation")]
    public Animator animator;
    public string hitTrigger = "Hit";
    public string deathTrigger = "Die";
    [Min(0f)] public float destroyDelayAfterDeath = 0.65f;

    [Header("Damage Popup")]
    [SerializeField] private bool showDamageNumbers = true;
    [SerializeField] private DamagePopupFloatingText damagePopupPrefab;
    [SerializeField] private Color normalDamageColor = Color.white;
    [SerializeField] private Color physicalDamageColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color specialDamageColor = new Color(0.78f, 0.35f, 1f, 1f);
    [SerializeField] private Color criticalDamageColor = new Color(1f, 0.84f, 0.2f, 1f);
    [SerializeField] private Color missDamageColor = new Color(0.75f, 0.95f, 1f, 1f);
    [SerializeField] private Vector3 damagePopupOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private Vector2 damagePopupRandomOffset = new Vector2(0.3f, 0.15f);
    [SerializeField] private bool debugPlayerDeathTrace = false;

    public event Action<GameObject> Died;
    public event Action<float, GameObject> Damaged;
    public event Action<float, float> OnShieldChanged;

    private bool dead;
    private float localShield;
    private float localMaxShield;
    private readonly Dictionary<string, float> incomingDamageMultipliers = new Dictionary<string, float>();
    private RuneRuntimeState runeRuntimeState;
    private bool warnedMissingDamagePopupPrefab;
    private float lastIncomingDamageFinalHitChance = 1f;
    private float lastIncomingDamageFinalEvasionChance;
    private float lastIncomingDamageMissRoll = -1f;
    private bool lastIncomingDamageWasMiss;
    private static DamagePopupFloatingText defaultDamagePopupPrefab;
    private static bool attemptedLoadDefaultDamagePopupPrefab;
    private float nextPlayerMonsterDamageAllowedTime;
    private float lastCurrentHealthBeforeDeath = -1f;
    private float lastMaxHealthBeforeDeath = -1f;
    private float lastDamageBeforeDeath;
    private string lastDamageSourceMethod = "None";
    private GameObject lastDamageSourceObject;
    private bool battleStartShieldResetPerformed;
    private bool battleStartShieldCarryOverDetected;
    private bool battleStartShieldAuditLogged;
    private bool runtimeShieldSourceAuditLogged;
    private string pendingShieldAuditSource;

    private float MaxHealth => stats != null ? stats.maxHealth : (resourceBank != null ? resourceBank.maxHealth : currentHealth);
    public float MaxHealthValue => MaxHealth;
    public bool IsDead => dead;

    public float ResolveConfiguredMaxHealth(float fallback = 100f)
    {
        if (stats != null && stats.maxHealth > 0f)
        {
            return stats.maxHealth;
        }

        if (resourceBank != null && resourceBank.maxHealth > 0f)
        {
            return resourceBank.maxHealth;
        }

        return currentHealth > 0f ? currentHealth : fallback;
    }

    public void SyncHealthFromStats(bool refillCurrentHealth)
    {
        float previousCurrentHealth = currentHealth;
        float previousMaxHealth = resourceBank != null ? resourceBank.maxHealth : ResolveConfiguredMaxHealth();
        bool hadMatchingSerializedHealth = Mathf.Approximately(previousCurrentHealth, previousMaxHealth);

        if (resourceBank != null)
        {
            resourceBank.SyncHealthFromCombatStats(refillCurrentHealth);
            currentHealth = Mathf.Clamp(resourceBank.currentHealth, 0f, Mathf.Max(0f, resourceBank.maxHealth));
            return;
        }

        float resolvedMaxHealth = ResolveConfiguredMaxHealth(previousMaxHealth);
        if (refillCurrentHealth || hadMatchingSerializedHealth || currentHealth <= 0f)
        {
            currentHealth = Mathf.Max(0f, resolvedMaxHealth);
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 0f, Mathf.Max(0f, resolvedMaxHealth));
        }
    }

    private void Awake()
    {
        CombatRuntimeAuditLogger.SetEnabled(enableCombatBalanceLogs, name + ".CombatHealthBalanceLogs");
        if (stats == null)
        {
            stats = GetComponent<CombatStats>();
        }

        if (resourceBank == null)
        {
            resourceBank = GetComponent<BattleResourceBank>();
        }

        if (runeRuntimeState == null)
        {
            runeRuntimeState = GetComponent<RuneRuntimeState>();
        }

        ResetPlayerShieldAtBattleStart();

        TwinStateCombatBonus.EnsureFormalStateStatus(gameObject);

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        DissolveOnDeath dissolveOnDeath = GetComponent<DissolveOnDeath>();
        if (dissolveOnDeath == null)
        {
            dissolveOnDeath = gameObject.AddComponent<DissolveOnDeath>();
        }

        dissolveOnDeath.EnsureHealthBindings();

        if (resourceBank != null)
        {
            resourceBank.OnShieldChanged += HandleResourceBankOnShieldChanged;
        }

        SyncHealthFromStats(refillCurrentHealth: false);
        localShield = Mathf.Max(0f, localShield);
        localMaxShield = Mathf.Max(0f, localMaxShield);
        LogNoRuneShieldAudit(
            battleStartShieldCarryOverDetected ? "BattleStartCarryOver" : "BattleStart",
            "InitialState",
            battleStartAudit: true);
    }

    private void OnDestroy()
    {
        if (resourceBank != null)
        {
            resourceBank.OnShieldChanged -= HandleResourceBankOnShieldChanged;
        }
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(new BattleDamage(amount, BattleDamageType.Physical, null));
    }

    public void TakeDamage(BattleDamage damage)
    {
        if (damage.packetId <= 0)
        {
            damage.packetId = ++nextDamagePacketId;
        }
        LogDamageEntry("TakeDamage", damage, defenseHandledBySkill: false);
        bool logBossPlayerDamage = ShouldLogBossPlayerDamageFlow(damage.source);
        float playerHpBefore = ResolveCurrentHealthForDebug();
        float playerShieldBefore = GetShield();
        bool shouldLogDamageApply = CombatRuntimeAuditLogger.IsEnabled && BattleTargetUtility.IsPlayer(gameObject);
        float combatHealthCurrentBefore = currentHealth;
        float resourceBankCurrentBefore = resourceBank != null ? resourceBank.currentHealth : -1f;
        string noEffectiveDamageReason = string.Empty;

        if (dead)
        {
            if (logBossPlayerDamage)
            {
                Debug.Log(
                    "[PlayerDamagePipelineAudit] entry=TakeDamage enemy=" + GetDebugObjectName(damage.source) +
                    " target=" + name +
                    " source=BossMelee damageBeforeModifiers=" + damage.amount.ToString("F2") +
                    " damageAfterModifiers=" + damage.amount.ToString("F2") +
                    " hitChance=" + lastIncomingDamageFinalHitChance.ToString("F4") +
                    " missRoll=" + lastIncomingDamageMissRoll.ToString("F4") +
                    " isMiss=" + lastIncomingDamageWasMiss +
                    " targetInvincible=false targetShield=" + playerShieldBefore.ToString("F2") +
                    " targetCombatHealthFound=true TakeDamageCalled=false result=target-dead" +
                    " playerHpBefore=" + playerHpBefore.ToString("F2") +
                    " playerHpAfter=" + playerHpBefore.ToString("F2"),
                    gameObject);
            }
            DayNightGaugeHitFlowLog("TakeDamage(BattleDamage) skipped reason=target-dead", gameObject);
            return;
        }

        if (ShouldIgnoreDamageFrom(damage.source))
        {
            if (logBossPlayerDamage)
            {
                Debug.Log(
                    "[PlayerDamagePipelineAudit] entry=TakeDamage enemy=" + GetDebugObjectName(damage.source) +
                    " target=" + name +
                    " source=BossMelee damageBeforeModifiers=" + damage.amount.ToString("F2") +
                    " damageAfterModifiers=" + damage.amount.ToString("F2") +
                    " hitChance=" + lastIncomingDamageFinalHitChance.ToString("F4") +
                    " missRoll=" + lastIncomingDamageMissRoll.ToString("F4") +
                    " isMiss=false targetInvincible=false targetShield=" + playerShieldBefore.ToString("F2") +
                    " targetCombatHealthFound=true TakeDamageCalled=true result=ignored-source" +
                    " playerHpBefore=" + playerHpBefore.ToString("F2") +
                    " playerHpAfter=" + playerHpBefore.ToString("F2"),
                    gameObject);
            }
            DayNightGaugeHitFlowLog($"TakeDamage(BattleDamage) skipped reason=ignored-source source={GetDebugObjectName(damage.source)} target={GetDebugObjectName(gameObject)}", gameObject);
            return;
        }

        if (TryEvadeDamage(damage, out _))
        {
            CombatRuntimeAuditLogger.RecordPlayerDamageOutcome(
                gameObject,
                ResolveIncomingMonsterSource(damage.sourceOwner != null ? damage.sourceOwner : damage.source),
                damage.attackKind,
                "Dodge",
                0f,
                0f,
                false);
            if (logBossPlayerDamage)
            {
                Debug.Log(
                    "[PlayerDamagePipelineAudit] entry=TakeDamage enemy=" + GetDebugObjectName(damage.source) +
                    " target=" + name +
                    " source=BossMelee damageBeforeModifiers=" + damage.amount.ToString("F2") +
                    " damageAfterModifiers=" + damage.amount.ToString("F2") +
                    " hitChance=" + lastIncomingDamageFinalHitChance.ToString("F4") +
                    " missRoll=" + lastIncomingDamageMissRoll.ToString("F4") +
                    " isMiss=" + lastIncomingDamageWasMiss +
                    " targetInvincible=false targetShield=" + playerShieldBefore.ToString("F2") +
                    " targetCombatHealthFound=true TakeDamageCalled=true result=miss" +
                    " playerHpBefore=" + playerHpBefore.ToString("F2") +
                    " playerHpAfter=" + playerHpBefore.ToString("F2"),
                    gameObject);
            }
            ShowMissPopup();
            DayNightGaugeHitFlowLog($"TakeDamage(BattleDamage) skipped reason=miss source={GetDebugObjectName(damage.source)} target={GetDebugObjectName(gameObject)}", gameObject);
            return;
        }

        Player01SkillController player1 = GetComponent<Player01SkillController>();
        if (player1 != null && player1.ShouldIgnoreIncomingDamage(damage))
        {
            CombatRuntimeAuditLogger.RecordPlayerDamageOutcome(
                gameObject,
                ResolveIncomingMonsterSource(damage.sourceOwner != null ? damage.sourceOwner : damage.source),
                damage.attackKind,
                "InvinciblePlayer01Skill",
                0f,
                0f,
                false);
            if (logBossPlayerDamage)
            {
                Debug.Log(
                    "[PlayerDamagePipelineAudit] entry=TakeDamage enemy=" + GetDebugObjectName(damage.source) +
                    " target=" + name +
                    " source=BossMelee damageBeforeModifiers=" + damage.amount.ToString("F2") +
                    " damageAfterModifiers=" + damage.amount.ToString("F2") +
                    " hitChance=" + lastIncomingDamageFinalHitChance.ToString("F4") +
                    " missRoll=" + lastIncomingDamageMissRoll.ToString("F4") +
                    " isMiss=false targetInvincible=true targetShield=" + playerShieldBefore.ToString("F2") +
                    " targetCombatHealthFound=true TakeDamageCalled=true result=invincible" +
                    " playerHpBefore=" + playerHpBefore.ToString("F2") +
                    " playerHpAfter=" + playerHpBefore.ToString("F2"),
                    gameObject);
            }
            DayNightGaugeHitFlowLog($"TakeDamage(BattleDamage) skipped reason=player01-ignored-damage source={GetDebugObjectName(damage.source)} target={GetDebugObjectName(gameObject)}", gameObject);
            return;
        }

        GameObject metadataSource = damage.sourceOwner != null ? damage.sourceOwner : damage.source;
        GameObject resolvedMonsterSource = ResolveIncomingMonsterSource(metadataSource);
        GameObject resolvedPlayerSource = BattleTargetUtility.ResolvePlayerSource(metadataSource);
        GameObject resolvedDamageSource = ResolveDamageModifierSource(damage.source, resolvedPlayerSource, resolvedMonsterSource);
        NormalizeDamageMetadata(ref damage, resolvedPlayerSource, resolvedMonsterSource);
        bool isPlayerAttackingMonster = resolvedPlayerSource != null && BattleTargetUtility.IsMonster(gameObject);
        bool isMonsterAttackingPlayer = resolvedMonsterSource != null && BattleTargetUtility.IsPlayer(gameObject);
        if (ShouldBlockPlayerMonsterDamageByInvincibility(resolvedMonsterSource, out float invincibilityRemaining))
        {
            CombatRuntimeAuditLogger.RecordPlayerDamageOutcome(
                gameObject,
                resolvedMonsterSource,
                damage.attackKind,
                "InvincibleGlobalMonsterIFrame",
                0f,
                0f,
                false);
            if (logBossPlayerDamage)
            {
                Debug.Log(
                    "[PlayerDamagePipelineAudit] entry=TakeDamage enemy=" + GetDebugObjectName(damage.source) +
                    " target=" + name +
                    " source=BossMelee damageBeforeModifiers=" + damage.amount.ToString("F2") +
                    " damageAfterModifiers=0.00" +
                    " hitChance=" + lastIncomingDamageFinalHitChance.ToString("F4") +
                    " missRoll=" + lastIncomingDamageMissRoll.ToString("F4") +
                    " isMiss=false targetInvincible=true targetShield=" + playerShieldBefore.ToString("F2") +
                    " targetCombatHealthFound=true TakeDamageCalled=true result=invincible" +
                    " invincibilityRemaining=" + invincibilityRemaining.ToString("F2") +
                    " playerHpBefore=" + playerHpBefore.ToString("F2") +
                    " playerHpAfter=" + playerHpBefore.ToString("F2"),
                    gameObject);
            }

            DayNightGaugeHitFlowLog($"TakeDamage(BattleDamage) skipped reason=player-monster-hit-invincible source={GetDebugObjectName(damage.source)} target={GetDebugObjectName(gameObject)} remaining={invincibilityRemaining:F2}", gameObject);
            return;
        }

        float skillBaseDamage = Mathf.Max(0f, damage.amount);
        float baseDamage = skillBaseDamage;
        int equippedRuneCount = 0;
        float damageBonusPerRune = 0f;
        float runeBaseDamageBonus = 0f;
        string runeCountSource = "Skipped";
        baseDamage = ApplyLimitedRuneFlatDamage(
            damage,
            resolvedPlayerSource,
            isPlayerAttackingMonster,
            baseDamage,
            out equippedRuneCount,
            out damageBonusPerRune,
            out runeBaseDamageBonus,
            out runeCountSource);

        float outgoingDamage = damage.bypassAttackerMultipliers
            ? baseDamage
            : BattleStatUtility.ApplyPlayerMoveSpeedDamageBonus(resolvedDamageSource, baseDamage);
        CombatStats attackerStats = !damage.bypassAttackerMultipliers ? BattleStatUtility.GetCombatStats(resolvedDamageSource) : null;
        float demoPlayerDamageMultiplier = attackerStats != null ? Mathf.Max(0f, attackerStats.outgoingDamageMultiplier) : 1f;
        if (!damage.bypassAttackerMultipliers && attackerStats != null)
        {
            outgoingDamage *= demoPlayerDamageMultiplier;
        }
        outgoingDamage = ApplyRuneEquipResonanceOutgoingMultiplier(resolvedPlayerSource, damage, isPlayerAttackingMonster, outgoingDamage);
        outgoingDamage = ApplyEnemyDebuffOutgoingMultiplier(resolvedMonsterSource, damage, outgoingDamage);
        damage.amount = outgoingDamage;
        float reducedDamage = stats != null ? stats.ReduceDamage(damage) : outgoingDamage;
        float finalDamage = reducedDamage;
        float dayNightDamageMultiplier = 1f;
        if (!damage.bypassAffinityModifier)
        {
            bool includeAmbientAffinity = !damage.bypassAttackerMultipliers && !damage.bypassAmbientAffinity;
            finalDamage = DayNightAffinityDamageModifier.ApplyModifier(resolvedDamageSource, gameObject, finalDamage, out dayNightDamageMultiplier, includeAmbientAffinity);
        }
        float afterAffinityDamage = finalDamage;
        finalDamage = TwinStateCombatBonus.ApplyNightChildIncomingDamageReduction(gameObject, resolvedMonsterSource, finalDamage, this, damage.debugTag);
        float afterTwinReductionDamage = finalDamage;
        runeRuntimeState = ResolveRuneRuntimeState();
        if (resolvedMonsterSource != null && runeRuntimeState != null)
        {
            finalDamage *= runeRuntimeState.GetIncomingMonsterDamageMultiplier(resolvedMonsterSource, finalDamage, damage);
            finalDamage *= runeRuntimeState.GetRuneEquipResonanceIncomingMultiplier();
        }
        float afterRuneDamage = finalDamage;
        finalDamage *= GetIncomingDamageMultiplier();
        finalDamage = ApplyMinimumMonsterHitDamageIfNeeded(baseDamage, resolvedMonsterSource, finalDamage);
        float afterIncomingMultiplierDamage = finalDamage;
        Player2PrototypeController player2 = GetComponent<Player2PrototypeController>();
        if (player2 != null)
        {
            finalDamage = player2.ProcessIncomingDamageWithWGuard(finalDamage, damage);
        }
        float afterGuardDamage = finalDamage;
        float beforeClampDamage = finalDamage;
        finalDamage = ApplyMonsterDamageSafetyClamp(resolvedMonsterSource, damage, finalDamage);
        float afterClampDamage = finalDamage;
        float resolvedDamageBeforeShieldAndGuard = Mathf.Max(0f, finalDamage);
        finalDamage = AbsorbShieldDamage(finalDamage, damage.source, resolvedMonsterSource, out float shieldConsumed);
        RecordFinalRushPressureHit(resolvedMonsterSource, shieldConsumed, finalDamage);
        float afterShieldDamage = finalDamage;
        LogPlayerSkillDamageDebug(
            damage,
            resolvedPlayerSource,
            isPlayerAttackingMonster,
            skillBaseDamage,
            equippedRuneCount,
            damageBonusPerRune,
            runeBaseDamageBonus,
            runeCountSource,
            baseDamage,
            demoPlayerDamageMultiplier,
            dayNightDamageMultiplier,
            finalDamage);

        if (resourceBank != null)
        {
            resourceBank.currentHealth = Mathf.Max(0f, resourceBank.currentHealth - finalDamage);
            currentHealth = resourceBank.currentHealth;
        }
        else
        {
            currentHealth = Mathf.Max(0f, currentHealth - finalDamage);
        }

        float combatHealthCurrentAfter = currentHealth;
        float resourceBankCurrentAfter = resourceBank != null ? resourceBank.currentHealth : -1f;
        bool damageApplied = combatHealthCurrentAfter < combatHealthCurrentBefore || (resourceBank != null && resourceBankCurrentAfter < resourceBankCurrentBefore);
        float actualHpDamage = resourceBank != null
            ? Mathf.Max(0f, resourceBankCurrentBefore - resourceBankCurrentAfter)
            : Mathf.Max(0f, combatHealthCurrentBefore - combatHealthCurrentAfter);
        CombatRuntimeAuditLogger.RecordPlayerDamageOutcome(
            gameObject,
            resolvedMonsterSource,
            damage.attackKind,
            actualHpDamage > 0f ? "AppliedToHP" : (shieldConsumed > 0f ? "ShieldAbsorbed" : "NoEffectiveDamage"),
            shieldConsumed,
            actualHpDamage,
            beforeClampDamage > afterClampDamage + 0.0001f);
        ApplyNightChildFavorableLifesteal(resolvedPlayerSource, damage, Mathf.Max(0f, finalDamage));
        if (resolvedDamageBeforeShieldAndGuard > 0f)
        {
            ArmPlayerMonsterDamageInvincibility(resolvedMonsterSource);
        }

        if (finalDamage <= 0f)
        {
            if (outgoingDamage <= 0f)
            {
                noEffectiveDamageReason = "OutgoingDamageZero";
            }
            else if (reducedDamage <= 0f)
            {
                noEffectiveDamageReason = "DefenseReducedToZero";
            }
            else if (afterAffinityDamage <= 0f)
            {
                noEffectiveDamageReason = "DayNightModifierReducedToZero";
            }
            else if (afterTwinReductionDamage <= 0f)
            {
                noEffectiveDamageReason = "TwinStateReductionReducedToZero";
            }
            else if (afterRuneDamage <= 0f)
            {
                noEffectiveDamageReason = "RuneIncomingMultiplierReducedToZero";
            }
            else if (afterIncomingMultiplierDamage <= 0f)
            {
                noEffectiveDamageReason = "IncomingDamageMultiplierReducedToZero";
            }
            else if (afterShieldDamage <= 0f && playerShieldBefore > 0f)
            {
                noEffectiveDamageReason = "AbsorbedByShield";
            }
            else if (afterGuardDamage <= 0f && player2 != null)
            {
                noEffectiveDamageReason = "ReducedToZeroByWGuard";
            }
            else
            {
                noEffectiveDamageReason = "FinalDamageClampedToZero";
            }
        }
        else if (!damageApplied)
        {
            noEffectiveDamageReason = "HealthWritebackUnchanged";
        }

        if (shouldLogDamageApply)
        {
            bool hasAffinityPhase = DayNightAffinityDamageModifier.TryGetCurrentPhase(out DayNightPhase currentAffinityPhase);
            bool targetHasNightChildState = DayNightAffinityDamageModifier.HasNightChildState(gameObject);
            bool targetHasDayChildState = DayNightAffinityDamageModifier.HasDayChildState(gameObject);
            bool targetNightChildPositivePhase = DayNightAffinityDamageModifier.IsNightChildPositivePhase(gameObject);
            bool targetNightChildNegativePhase = DayNightAffinityDamageModifier.IsNightChildNegativePhase(gameObject);
            float targetIncomingMultiplier = DayNightAffinityDamageModifier.GetTwinIncomingDamageMultiplier(gameObject);
            float targetEvasionMultiplier = DayNightAffinityDamageModifier.GetWrongTimeEvasionMultiplier(gameObject);
            float targetMoveSpeedMultiplier = DayNightAffinityDamageModifier.GetWrongTimeMoveSpeedMultiplier(gameObject);
            float baseEnemyDamage = isMonsterAttackingPlayer ? skillBaseDamage : 0f;
            float enemyDifficultyDamageMultiplier = isMonsterAttackingPlayer
                ? EnemyDifficultyDirector.ResolveEnemyOutgoingDamageMultiplier(resolvedMonsterSource)
                : 1f;
            DayNightGaugeRuntimeState.TryGetExistingInstance(out DayNightGaugeRuntimeState gaugeForDamageApply);
            Debug.Log(
                "[PlayerDamagePipelineAudit] entry=TakeDamage stage=Resolved " +
                "target=" + name +
                " sourceObject=" + GetDebugObjectName(damage.source) +
                " resolvedDamageSource=" + GetDebugObjectName(resolvedDamageSource) +
                " resolvedMonsterSource=" + GetDebugObjectName(resolvedMonsterSource) +
                " currentCharacter=" + name +
                " currentPhase=" + (hasAffinityPhase ? currentAffinityPhase.ToString() : "Unavailable") +
                " radiance=" + (gaugeForDamageApply != null ? gaugeForDamageApply.RadianceValue.ToString("F2") : "Unavailable") +
                " twilight=" + (gaugeForDamageApply != null ? gaugeForDamageApply.TwilightValue.ToString("F2") : "Unavailable") +
                " isInNightChildState=" + targetHasNightChildState +
                " isInDayChildState=" + targetHasDayChildState +
                " isNightChildPositivePhase=" + targetNightChildPositivePhase +
                " isNightChildNegativePhase=" + targetNightChildNegativePhase +
                " dayNightIncomingDamageMultiplier=" + targetIncomingMultiplier.ToString("F2") +
                " evasionMultiplier=" + targetEvasionMultiplier.ToString("F2") +
                " moveSpeedMultiplier=" + targetMoveSpeedMultiplier.ToString("F2") +
                " baseEnemyDamage=" + baseEnemyDamage.ToString("F2") +
                " enemyDifficultyDamageMultiplier=" + enemyDifficultyDamageMultiplier.ToString("F2") +
                " incomingDamage=" + damage.amount.ToString("F2") +
                " outgoingDamage=" + outgoingDamage.ToString("F2") +
                " reducedDamage=" + reducedDamage.ToString("F2") +
                " afterAffinityDamage=" + afterAffinityDamage.ToString("F2") +
                " afterTwinDebuff=" + afterAffinityDamage.ToString("F2") +
                " afterTwinReductionDamage=" + afterTwinReductionDamage.ToString("F2") +
                " afterRuneDamage=" + afterRuneDamage.ToString("F2") +
                " afterIncomingMultiplierDamage=" + afterIncomingMultiplierDamage.ToString("F2") +
                " afterGuardDamage=" + afterGuardDamage.ToString("F2") +
                " beforeClampDamage=" + beforeClampDamage.ToString("F2") +
                " afterClampDamage=" + afterClampDamage.ToString("F2") +
                " shieldBefore=" + playerShieldBefore.ToString("F2") +
                " shieldConsumed=" + shieldConsumed.ToString("F2") +
                " afterShieldDamage=" + afterShieldDamage.ToString("F2") +
                " finalDamage=" + finalDamage.ToString("F2") +
                " finalIncomingDamage=" + (isMonsterAttackingPlayer ? finalDamage.ToString("F2") : "n/a") +
                " resourceBank exists=" + (resourceBank != null) +
                " currentHealth before=" + combatHealthCurrentBefore.ToString("F2") +
                " resourceBank currentHealth before=" + (resourceBank != null ? resourceBankCurrentBefore.ToString("F2") : "n/a") +
                " currentHealth after=" + combatHealthCurrentAfter.ToString("F2") +
                " resourceBank currentHealth after=" + (resourceBank != null ? resourceBankCurrentAfter.ToString("F2") : "n/a") +
                " damageApplied=" + damageApplied +
                " noEffectiveDamageReason=" + (string.IsNullOrEmpty(noEffectiveDamageReason) ? "None" : noEffectiveDamageReason),
                gameObject);
        }

        bool shouldNotifyGaugeHit = ShouldCountAsSuccessfulHit(damage.amount, outgoingDamage, resolvedDamageBeforeShieldAndGuard);
        if (shouldNotifyGaugeHit && !damage.suppressGaugeNotification)
        {
            bool notifiedGauge = DayNightAffinityDamageModifier.NotifySuccessfulPlayerHit(resolvedDamageSource, gameObject);
            if (!notifiedGauge)
            {
                DayNightGaugeHitFlowLog(
                    $"TakeDamage(BattleDamage) gauge-notified=False source={GetDebugObjectName(damage.source)} target={GetDebugObjectName(gameObject)} outgoingDamage={outgoingDamage:F2} preShieldDamage={resolvedDamageBeforeShieldAndGuard:F2} finalDamage={finalDamage:F2}",
                    gameObject);
            }
        }
        else
        {
            if (logBossPlayerDamage)
            {
                Debug.Log(
                    "[PlayerDamagePipelineAudit] entry=TakeDamage enemy=" + GetDebugObjectName(damage.source) +
                    " target=" + name +
                    " source=BossMelee damageBeforeModifiers=" + damage.amount.ToString("F2") +
                    " damageAfterModifiers=" + finalDamage.ToString("F2") +
                    " hitChance=" + lastIncomingDamageFinalHitChance.ToString("F4") +
                    " missRoll=" + lastIncomingDamageMissRoll.ToString("F4") +
                    " isMiss=false targetInvincible=false targetShield=" + GetShield().ToString("F2") +
                    " targetCombatHealthFound=true TakeDamageCalled=true result=no-effective-damage" +
                    " playerHpBefore=" + playerHpBefore.ToString("F2") +
                    " playerHpAfter=" + ResolveCurrentHealthForDebug().ToString("F2") +
                    " noEffectiveDamageReason=" + (string.IsNullOrEmpty(noEffectiveDamageReason) ? "Unknown" : noEffectiveDamageReason),
                    gameObject);
            }
            DayNightGaugeHitFlowLog(
                $"TakeDamage(BattleDamage) skipped reason=no-effective-damage-resolution source={GetDebugObjectName(damage.source)} target={GetDebugObjectName(gameObject)} inputDamage={damage.amount:F2} outgoingDamage={outgoingDamage:F2} preShieldDamage={resolvedDamageBeforeShieldAndGuard:F2} finalDamage={finalDamage:F2}",
                gameObject);
        }

        NotifyRuneIncomingMonsterHit(damage, resolvedMonsterSource, resolvedDamageBeforeShieldAndGuard, "TakeDamage(BattleDamage)");

        if (finalDamage > 0f)
        {
            Damaged?.Invoke(finalDamage, damage.source);
            ShowDamagePopup(finalDamage, ResolvePopupType(damage.damageType), damage.isCritical);
            TriggerAnimation(hitTrigger);

            if (logBossPlayerDamage)
            {
                Debug.Log(
                    "[PlayerDamagePipelineAudit] entry=TakeDamage enemy=" + GetDebugObjectName(damage.source) +
                    " target=" + name +
                    " source=BossMelee damageBeforeModifiers=" + damage.amount.ToString("F2") +
                    " damageAfterModifiers=" + finalDamage.ToString("F2") +
                    " hitChance=" + lastIncomingDamageFinalHitChance.ToString("F4") +
                    " missRoll=" + lastIncomingDamageMissRoll.ToString("F4") +
                    " isMiss=false targetInvincible=false targetShield=" + GetShield().ToString("F2") +
                    " targetCombatHealthFound=true TakeDamageCalled=true result=applied" +
                    " playerHpBefore=" + playerHpBefore.ToString("F2") +
                    " playerHpAfter=" + ResolveCurrentHealthForDebug().ToString("F2"),
                    gameObject);
            }
        }

        if (currentHealth <= 0f)
        {
            lastCurrentHealthBeforeDeath = resourceBank != null ? resourceBankCurrentBefore : combatHealthCurrentBefore;
            lastMaxHealthBeforeDeath = resourceBank != null ? resourceBank.maxHealth : MaxHealthValue;
            lastDamageBeforeDeath = finalDamage;
            lastDamageSourceMethod = "TakeDamage";
            lastDamageSourceObject = damage.source;
            Die(damage.source);
        }
    }

    private bool ShouldLogBossPlayerDamageFlow(GameObject source)
    {
        if (!BattleTargetUtility.IsPlayer(gameObject))
        {
            return false;
        }

        MonsterIdentity sourceIdentity = source != null ? source.GetComponentInParent<MonsterIdentity>() : null;
        return CombatRuntimeAuditLogger.IsEnabled && sourceIdentity != null && sourceIdentity.rank == MonsterRank.Boss;
    }

    public void ApplyDirectDamage(float amount, GameObject source)
    {
        ApplyDirectDamage(amount, source, DamagePopupType.Normal, false);
    }

    public void ApplyDirectDamage(float amount, GameObject source, DamagePopupType popupType, bool isCritical = false)
    {
        ApplyDirectDamage(new BattleDamage(amount, BattleDamageType.Physical, source, isCritical), popupType);
    }

    public void ApplyDirectDamage(BattleDamage damage, DamagePopupType popupType)
    {
        if (damage.packetId <= 0)
        {
            damage.packetId = ++nextDamagePacketId;
        }
        LogDamageEntry("ApplyDirectDamage", damage, defenseHandledBySkill: true);
        float currentHealthBefore = resourceBank != null ? resourceBank.currentHealth : currentHealth;
        float maxHealthBefore = resourceBank != null ? resourceBank.maxHealth : MaxHealthValue;

        if (dead)
        {
            DayNightGaugeHitFlowLog("ApplyDirectDamage skipped reason=target-dead", gameObject);
            return;
        }

        if (ShouldIgnoreDamageFrom(damage.source))
        {
            DayNightGaugeHitFlowLog($"ApplyDirectDamage skipped reason=ignored-source source={GetDebugObjectName(damage.source)} target={GetDebugObjectName(gameObject)}", gameObject);
            return;
        }

        if (TryEvadeDamage(damage, out _))
        {
            CombatRuntimeAuditLogger.RecordPlayerDamageOutcome(
                gameObject,
                ResolveIncomingMonsterSource(damage.sourceOwner != null ? damage.sourceOwner : damage.source),
                damage.attackKind,
                "Dodge",
                0f,
                0f,
                false);
            ShowMissPopup();
            DayNightGaugeHitFlowLog($"ApplyDirectDamage skipped reason=miss source={GetDebugObjectName(damage.source)} target={GetDebugObjectName(gameObject)}", gameObject);
            return;
        }

        GameObject metadataSource = damage.sourceOwner != null ? damage.sourceOwner : damage.source;
        GameObject resolvedMonsterSource = ResolveIncomingMonsterSource(metadataSource);
        GameObject resolvedPlayerSource = BattleTargetUtility.ResolvePlayerSource(metadataSource);
        GameObject resolvedDamageSource = ResolveDamageModifierSource(damage.source, resolvedPlayerSource, resolvedMonsterSource);
        NormalizeDamageMetadata(ref damage, resolvedPlayerSource, resolvedMonsterSource);
        bool isPlayerAttackingMonster = resolvedPlayerSource != null && BattleTargetUtility.IsMonster(gameObject);
        if (ShouldBlockPlayerMonsterDamageByInvincibility(resolvedMonsterSource, out float invincibilityRemaining))
        {
            CombatRuntimeAuditLogger.RecordPlayerDamageOutcome(
                gameObject,
                resolvedMonsterSource,
                damage.attackKind,
                "InvincibleGlobalMonsterIFrame",
                0f,
                0f,
                false);
            DayNightGaugeHitFlowLog($"ApplyDirectDamage skipped reason=player-monster-hit-invincible source={GetDebugObjectName(damage.source)} target={GetDebugObjectName(gameObject)} remaining={invincibilityRemaining:F2}", gameObject);
            return;
        }

        float skillBaseDamage = Mathf.Max(0f, damage.amount);
        float baseDamage = skillBaseDamage;
        int equippedRuneCount = 0;
        float damageBonusPerRune = 0f;
        float runeBaseDamageBonus = 0f;
        string runeCountSource = "Skipped";
        baseDamage = ApplyLimitedRuneFlatDamage(
            damage,
            resolvedPlayerSource,
            isPlayerAttackingMonster,
            baseDamage,
            out equippedRuneCount,
            out damageBonusPerRune,
            out runeBaseDamageBonus,
            out runeCountSource);

        float finalDamage = damage.bypassAttackerMultipliers
            ? baseDamage
            : BattleStatUtility.ApplyPlayerMoveSpeedDamageBonus(resolvedDamageSource, baseDamage);
        CombatStats attackerStats = !damage.bypassAttackerMultipliers ? BattleStatUtility.GetCombatStats(resolvedDamageSource) : null;
        float demoPlayerDamageMultiplier = attackerStats != null ? Mathf.Max(0f, attackerStats.outgoingDamageMultiplier) : 1f;
        if (!damage.bypassAttackerMultipliers && attackerStats != null)
        {
            finalDamage *= demoPlayerDamageMultiplier;
        }
        finalDamage = ApplyRuneEquipResonanceOutgoingMultiplier(resolvedPlayerSource, damage, isPlayerAttackingMonster, finalDamage);
        finalDamage = ApplyEnemyDebuffOutgoingMultiplier(resolvedMonsterSource, damage, finalDamage);

        float dayNightDamageMultiplier = 1f;
        if (!damage.bypassAffinityModifier)
        {
            bool includeAmbientAffinity = !damage.bypassAttackerMultipliers && !damage.bypassAmbientAffinity;
            finalDamage = DayNightAffinityDamageModifier.ApplyModifier(resolvedDamageSource, gameObject, finalDamage, out dayNightDamageMultiplier, includeAmbientAffinity);
        }
        finalDamage = TwinStateCombatBonus.ApplyNightChildIncomingDamageReduction(gameObject, resolvedMonsterSource, finalDamage, this, damage.debugTag);
        runeRuntimeState = ResolveRuneRuntimeState();
        if (resolvedMonsterSource != null && runeRuntimeState != null)
        {
            finalDamage *= runeRuntimeState.GetIncomingMonsterDamageMultiplier(resolvedMonsterSource, finalDamage, damage);
            finalDamage *= runeRuntimeState.GetRuneEquipResonanceIncomingMultiplier();
        }
        finalDamage *= GetIncomingDamageMultiplier();
        finalDamage = ApplyMinimumMonsterHitDamageIfNeeded(baseDamage, resolvedMonsterSource, finalDamage);
        Player2PrototypeController player2 = GetComponent<Player2PrototypeController>();
        if (player2 != null)
        {
            finalDamage = player2.ProcessIncomingDamageWithWGuard(finalDamage, damage);
        }
        float beforeClampDamage = finalDamage;
        finalDamage = ApplyMonsterDamageSafetyClamp(resolvedMonsterSource, damage, finalDamage);
        float afterClampDamage = finalDamage;
        float resolvedDamageBeforeShield = Mathf.Max(0f, finalDamage);
        finalDamage = AbsorbShieldDamage(finalDamage, damage.source, resolvedMonsterSource, out float shieldConsumed);
        RecordFinalRushPressureHit(resolvedMonsterSource, shieldConsumed, finalDamage);
        if (resolvedDamageBeforeShield > 0f)
        {
            ArmPlayerMonsterDamageInvincibility(resolvedMonsterSource);
        }
        LogPlayerSkillDamageDebug(
            damage,
            resolvedPlayerSource,
            isPlayerAttackingMonster,
            skillBaseDamage,
            equippedRuneCount,
            damageBonusPerRune,
            runeBaseDamageBonus,
            runeCountSource,
            baseDamage,
            demoPlayerDamageMultiplier,
            dayNightDamageMultiplier,
            finalDamage);

        if (resourceBank != null)
        {
            resourceBank.currentHealth = Mathf.Max(0f, resourceBank.currentHealth - finalDamage);
            currentHealth = resourceBank.currentHealth;
        }
        else
        {
            currentHealth = Mathf.Max(0f, currentHealth - finalDamage);
        }

        float currentHealthAfter = resourceBank != null ? resourceBank.currentHealth : currentHealth;
        float actualHpDamage = Mathf.Max(0f, currentHealthBefore - currentHealthAfter);
        CombatRuntimeAuditLogger.RecordPlayerDamageOutcome(
            gameObject,
            resolvedMonsterSource,
            damage.attackKind,
            actualHpDamage > 0f ? "AppliedToHP" : (shieldConsumed > 0f ? "ShieldAbsorbed" : "NoEffectiveDamage"),
            shieldConsumed,
            actualHpDamage,
            beforeClampDamage > afterClampDamage + 0.0001f);

        ApplyNightChildFavorableLifesteal(resolvedPlayerSource, damage, Mathf.Max(0f, finalDamage));

        bool shouldNotifyGaugeHit = ShouldCountAsSuccessfulHit(damage.amount, finalDamage, resolvedDamageBeforeShield);
        if (shouldNotifyGaugeHit && !damage.suppressGaugeNotification)
        {
            bool notifiedGauge = DayNightAffinityDamageModifier.NotifySuccessfulPlayerHit(resolvedDamageSource, gameObject);
            if (!notifiedGauge)
            {
                DayNightGaugeHitFlowLog(
                    $"ApplyDirectDamage gauge-notified=False source={GetDebugObjectName(damage.source)} target={GetDebugObjectName(gameObject)} inputDamage={damage.amount:F2} preShieldDamage={resolvedDamageBeforeShield:F2} finalDamage={finalDamage:F2}",
                    gameObject);
            }
        }
        else
        {
            DayNightGaugeHitFlowLog(
                $"ApplyDirectDamage skipped reason=no-effective-damage-resolution source={GetDebugObjectName(damage.source)} target={GetDebugObjectName(gameObject)} inputDamage={damage.amount:F2} preShieldDamage={resolvedDamageBeforeShield:F2} finalDamage={finalDamage:F2}",
                gameObject);
        }

        NotifyRuneIncomingMonsterHit(damage, resolvedMonsterSource, resolvedDamageBeforeShield, "ApplyDirectDamage");

        if (finalDamage > 0f)
        {
            Damaged?.Invoke(finalDamage, damage.source);
            ShowDamagePopup(finalDamage, popupType, damage.isCritical);
            TriggerAnimation(hitTrigger);
        }

        if (currentHealth <= 0f)
        {
            lastCurrentHealthBeforeDeath = currentHealthBefore;
            lastMaxHealthBeforeDeath = maxHealthBefore;
            lastDamageBeforeDeath = finalDamage;
            lastDamageSourceMethod = "ApplyDirectDamage";
            lastDamageSourceObject = damage.source;
            Die(damage.source);
        }
    }

    private float ApplyMinimumMonsterHitDamageIfNeeded(float baseDamage, GameObject resolvedMonsterSource, float finalDamage)
    {
        if (resolvedMonsterSource == null || !BattleTargetUtility.IsPlayer(gameObject) || baseDamage <= 0f)
        {
            return Mathf.Max(0f, finalDamage);
        }

        if (finalDamage > 0f && finalDamage < 1f)
        {
            return 1f;
        }

        return Mathf.Max(0f, finalDamage);
    }

    private void LogPlayerSkillDamageDebug(
        BattleDamage damage,
        GameObject resolvedPlayerSource,
        bool isPlayerAttackingMonster,
        float skillBaseDamage,
        int equippedRuneCount,
        float damageBonusPerRune,
        float runeBaseDamageBonus,
        string runeCountSource,
        float finalBaseDamage,
        float demoPlayerDamageMultiplier,
        float dayNightOutgoingDamageMultiplier,
        float finalDamage)
    {
        if (!enableCombatBalanceLogs || !isPlayerAttackingMonster)
        {
            return;
        }

        string skillName = !string.IsNullOrEmpty(damage.debugTag)
            ? damage.debugTag
            : damage.damageType.ToString();
        bool hasAffinityPhase = DayNightAffinityDamageModifier.TryGetCurrentPhase(out DayNightPhase currentAffinityPhase);
        bool attackerHasNightChildState = DayNightAffinityDamageModifier.HasNightChildState(resolvedPlayerSource);
        bool attackerHasDayChildState = DayNightAffinityDamageModifier.HasDayChildState(resolvedPlayerSource);
        bool attackerNightChildPositivePhase = DayNightAffinityDamageModifier.IsNightChildPositivePhase(resolvedPlayerSource);
        bool attackerNightChildNegativePhase = DayNightAffinityDamageModifier.IsNightChildNegativePhase(resolvedPlayerSource);
        DayNightGaugeRuntimeState.TryGetExistingInstance(out DayNightGaugeRuntimeState gaugeForSkillDamage);
        Debug.Log(
            "[RuneDamageDebug] " +
            "skillName=" + skillName +
            " sourceObject=" + GetDebugObjectName(damage.source) +
            " resolvedPlayerRoot=" + GetDebugObjectName(resolvedPlayerSource) +
            " currentCharacter=" + GetDebugObjectName(resolvedPlayerSource) +
            " target=" + name +
            " currentPhase=" + (hasAffinityPhase ? currentAffinityPhase.ToString() : "Unavailable") +
            " radiance=" + (gaugeForSkillDamage != null ? gaugeForSkillDamage.RadianceValue.ToString("F2") : "Unavailable") +
            " twilight=" + (gaugeForSkillDamage != null ? gaugeForSkillDamage.TwilightValue.ToString("F2") : "Unavailable") +
            " isInNightChildState=" + attackerHasNightChildState +
            " isInDayChildState=" + attackerHasDayChildState +
            " isNightChildPositivePhase=" + attackerNightChildPositivePhase +
            " isNightChildNegativePhase=" + attackerNightChildNegativePhase +
            " skillBaseDamage=" + skillBaseDamage.ToString("F2") +
            " equippedRuneCount=" + equippedRuneCount +
            " damageBonusPerRune=" + damageBonusPerRune.ToString("F2") +
            " runeBaseDamageBonus=" + runeBaseDamageBonus.ToString("F2") +
            " runeCountSource=" + runeCountSource +
            " finalBaseDamage=" + finalBaseDamage.ToString("F2") +
            " demoPlayerDamageMultiplier=" + demoPlayerDamageMultiplier.ToString("F2") +
            " dayNightOutgoingDamageMultiplier=" + dayNightOutgoingDamageMultiplier.ToString("F2") +
            " finalDamage=" + finalDamage.ToString("F2"),
            gameObject);
    }

    private bool ShouldIgnoreDamageFrom(GameObject source)
    {
        if (source == null)
        {
            return false;
        }

        bool sourceIsPlayer = BattleTargetUtility.IsPlayer(source);
        bool sourceIsMonster = BattleTargetUtility.IsMonster(source);
        bool targetIsPlayer = BattleTargetUtility.IsPlayer(gameObject);
        bool targetIsMonster = BattleTargetUtility.IsMonster(gameObject);

        if (sourceIsPlayer)
        {
            return !targetIsMonster;
        }

        if (sourceIsMonster)
        {
            return !targetIsPlayer;
        }

        return false;
    }

    private bool ShouldBlockPlayerMonsterDamageByInvincibility(GameObject resolvedMonsterSource, out float remainingSeconds)
    {
        remainingSeconds = 0f;
        if (resolvedMonsterSource == null || !BattleTargetUtility.IsPlayer(gameObject))
        {
            return false;
        }

        float duration = EnemyDifficultyDirector.ResolvePlayerHitInvincibleDuration();
        if (duration <= 0f)
        {
            return false;
        }

        remainingSeconds = nextPlayerMonsterDamageAllowedTime - Time.time;
        return remainingSeconds > 0f;
    }

    private void ArmPlayerMonsterDamageInvincibility(GameObject resolvedMonsterSource)
    {
        if (resolvedMonsterSource == null || !BattleTargetUtility.IsPlayer(gameObject))
        {
            return;
        }

        float duration = EnemyDifficultyDirector.ResolvePlayerHitInvincibleDuration();
        if (duration <= 0f)
        {
            return;
        }

        nextPlayerMonsterDamageAllowedTime = Mathf.Max(nextPlayerMonsterDamageAllowedTime, Time.time + duration);
    }

    public void Heal(float amount)
    {
        amount = Mathf.Max(0f, amount);
        RuneRuntimeState runtimeState = ResolveRuneRuntimeState();
        if (runtimeState != null)
        {
            amount *= runtimeState.GetHealingReceivedMultiplier();
        }

        if (resourceBank != null)
        {
            resourceBank.Heal(amount, false);
            currentHealth = resourceBank.currentHealth;
        }
        else
        {
            currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
        }
    }

    public void SetShield(float amount, string source = "Unknown")
    {
        amount = Mathf.Max(0f, amount);
        if (resourceBank != null)
        {
            pendingShieldAuditSource = source;
            try
            {
                resourceBank.SetShield(amount);
            }
            finally
            {
                pendingShieldAuditSource = null;
            }
            return;
        }

        float limit = Mathf.Max(0f, MaxHealthValue * BattleResourceBank.ShieldLimitMaxHealthRatio);
        localShield = Mathf.Clamp(amount, 0f, limit);
        localMaxShield = limit;
        NotifyShieldStateChanged();
        LogNoRuneShieldAudit(source, "ShieldSet", battleStartAudit: false);
    }

    public void ClearShield()
    {
        if (resourceBank != null)
        {
            resourceBank.ClearShield();
            return;
        }

        localShield = 0f;
        localMaxShield = 0f;
        NotifyShieldStateChanged();
    }

    public float GetShield()
    {
        float totalShield = GetBaseShield();
        PlayerTimedShieldStatus[] timedShields = GetComponents<PlayerTimedShieldStatus>();
        for (int i = 0; i < timedShields.Length; i++)
        {
            if (timedShields[i] == null)
            {
                continue;
            }

            totalShield += Mathf.Max(0f, timedShields[i].CurrentShield);
        }

        return Mathf.Min(totalShield, Mathf.Max(0f, MaxHealthValue * BattleResourceBank.ShieldLimitMaxHealthRatio));
    }

    public float GetMaxShield()
    {
        float totalMaxShield = GetBaseMaxShield();
        PlayerTimedShieldStatus[] timedShields = GetComponents<PlayerTimedShieldStatus>();
        for (int i = 0; i < timedShields.Length; i++)
        {
            if (timedShields[i] == null)
            {
                continue;
            }

            totalMaxShield += Mathf.Max(0f, timedShields[i].MaxShield);
        }

        return Mathf.Min(totalMaxShield, Mathf.Max(0f, MaxHealthValue * BattleResourceBank.ShieldLimitMaxHealthRatio));
    }

    public bool HasActiveShield()
    {
        return GetShield() > 0f;
    }

    public float CurrentShield => GetShield();
    public float MaxShield => GetMaxShield();
    public bool HasShield => HasActiveShield();

    public float GetCurrentShield()
    {
        return GetShield();
    }

    public void NotifyShieldStateChanged()
    {
        OnShieldChanged?.Invoke(GetShield(), GetMaxShield());
    }

    public void AddDamageReductionModifier(string key, float multiplier)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        incomingDamageMultipliers[key] = Mathf.Max(0f, multiplier);
    }

    public void RemoveDamageReductionModifier(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        incomingDamageMultipliers.Remove(key);
    }

    public void SetIncomingDamageMultiplier(object source, float multiplier)
    {
        AddDamageReductionModifier(GetModifierKey(source), multiplier);
    }

    public void RemoveIncomingDamageMultiplier(object source)
    {
        RemoveDamageReductionModifier(GetModifierKey(source));
    }

    private float AbsorbShieldDamage(float amount, GameObject rawSource, GameObject resolvedMonsterSource, out float totalShieldConsumed)
    {
        amount = Mathf.Max(0f, amount);
        totalShieldConsumed = 0f;
        float baseDamageToShield = amount;
        float shieldBefore = GetShield();
        float rankShieldPressure = ResolveMonsterShieldPressureMultiplier(resolvedMonsterSource);
        float remainingShieldBudget = Mathf.Max(0f, MaxHealthValue * BattleResourceBank.ShieldLimitMaxHealthRatio);
        PlayerTimedShieldStatus[] timedShields = GetComponents<PlayerTimedShieldStatus>();
        for (int i = 0; i < timedShields.Length; i++)
        {
            if (timedShields[i] == null)
            {
                continue;
            }

            timedShields[i].ClampCurrentShield(remainingShieldBudget);
            float timedShieldBefore = timedShields[i].CurrentShield;
            timedShields[i].AbsorbDamage(amount * rankShieldPressure);
            float timedShieldUsed = Mathf.Max(0f, timedShieldBefore - timedShields[i].CurrentShield);
            totalShieldConsumed += timedShieldUsed;
            amount = Mathf.Max(0f, amount - timedShieldUsed / Mathf.Max(0.01f, rankShieldPressure));
            remainingShieldBudget = Mathf.Max(0f, remainingShieldBudget - timedShields[i].CurrentShield);
            if (amount <= 0f)
            {
                LogShieldPressure(resolvedMonsterSource, baseDamageToShield, rankShieldPressure, shieldBefore, totalShieldConsumed, 0f);
                return 0f;
            }
        }

        float baseShield = Mathf.Min(GetBaseShield(), remainingShieldBudget);
        if (resourceBank != null && resourceBank.CurrentShield > baseShield)
        {
            resourceBank.SetShieldCurrent(baseShield);
        }
        float runeShieldDamageMultiplier = runeRuntimeState != null ? runeRuntimeState.GetShieldDamageTakenMultiplier() : 1f;
        float shieldDamageMultiplier = rankShieldPressure * Mathf.Max(0.01f, runeShieldDamageMultiplier);
        float shieldUsed = Mathf.Min(baseShield, amount * shieldDamageMultiplier);
        if (shieldUsed <= 0f)
        {
            LogShieldPressure(resolvedMonsterSource, baseDamageToShield, rankShieldPressure, shieldBefore, totalShieldConsumed, amount);
            return amount;
        }
        totalShieldConsumed += shieldUsed;

        float remainingShield = baseShield - shieldUsed;
        if (resourceBank != null)
        {
            resourceBank.SetShieldCurrent(remainingShield);
        }
        else
        {
            localShield = remainingShield;
            localMaxShield = Mathf.Max(localMaxShield, localShield);
            NotifyShieldStateChanged();
        }

        if (baseShield > 0f && remainingShield <= 0f)
        {
            runeRuntimeState?.NotifyShieldBrokenByMonsterDamage(baseShield, rawSource, resolvedMonsterSource);
        }

        float absorbedIncomingDamage = shieldDamageMultiplier > 0f ? shieldUsed / shieldDamageMultiplier : amount;
        float remainingDamageToHp = Mathf.Max(0f, amount - absorbedIncomingDamage);
        LogShieldPressure(resolvedMonsterSource, baseDamageToShield, rankShieldPressure, shieldBefore, totalShieldConsumed, remainingDamageToHp);
        return remainingDamageToHp;
    }

    private float ResolveMonsterShieldPressureMultiplier(GameObject resolvedMonsterSource)
    {
        if (resolvedMonsterSource == null || !BattleTargetUtility.IsPlayer(gameObject))
        {
            return 1f;
        }

        MonsterIdentity identity = resolvedMonsterSource.GetComponentInParent<MonsterIdentity>();
        if (identity == null)
        {
            return 1f;
        }

        return identity.rank switch
        {
            MonsterRank.Elite => EliteShieldPressureMultiplier,
            MonsterRank.Boss => BossShieldPressureMultiplier,
            _ => 1f
        };
    }

    private void LogShieldPressure(
        GameObject resolvedMonsterSource,
        float baseDamageToShield,
        float shieldDamageMultiplier,
        float shieldBefore,
        float shieldConsumed,
        float remainingDamageToHp)
    {
        if (!CombatRuntimeAuditLogger.IsEnabled || resolvedMonsterSource == null || !BattleTargetUtility.IsPlayer(gameObject))
        {
            return;
        }

        MonsterIdentity identity = resolvedMonsterSource.GetComponentInParent<MonsterIdentity>();
        Debug.Log(
            $"[ShieldPressure] attackerName={GetDebugObjectName(resolvedMonsterSource)} " +
            $"attackerRank={(identity != null ? identity.rank.ToString() : "Unknown")} baseDamageToShield={baseDamageToShield:F2} " +
            $"shieldDamageMultiplier={shieldDamageMultiplier:F2} shieldBefore={shieldBefore:F2} " +
            $"shieldConsumed={shieldConsumed:F2} shieldAfter={GetShield():F2} remainingDamageToHp={remainingDamageToHp:F2}",
            this);
    }

    private void RecordFinalRushPressureHit(GameObject resolvedMonsterSource, float shieldConsumed, float damageToHp)
    {
        if (resolvedMonsterSource == null || !BattleTargetUtility.IsPlayer(gameObject))
        {
            return;
        }

        EnemyDifficultyDirector director = EnemyDifficultyDirector.Instance;
        if (director == null || !director.IsFinalRushActive)
        {
            return;
        }

        finalRushEnemyHitsOnPlayer++;
        finalRushEnemyDamageToShield += Mathf.Max(0f, shieldConsumed);
        finalRushEnemyDamageToHp += Mathf.Max(0f, damageToHp);
    }

    public static void ConsumeFinalRushPressureMetrics(out int hitCount, out float shieldDamage, out float hpDamage)
    {
        hitCount = finalRushEnemyHitsOnPlayer;
        shieldDamage = finalRushEnemyDamageToShield;
        hpDamage = finalRushEnemyDamageToHp;
        finalRushEnemyHitsOnPlayer = 0;
        finalRushEnemyDamageToShield = 0f;
        finalRushEnemyDamageToHp = 0f;
    }

    private float GetBaseShield()
    {
        return resourceBank != null ? resourceBank.CurrentShield : localShield;
    }

    private float GetBaseMaxShield()
    {
        return resourceBank != null ? resourceBank.MaxShield : localMaxShield;
    }

    private float GetIncomingDamageMultiplier()
    {
        float multiplier = 1f;
        foreach (float value in incomingDamageMultipliers.Values)
        {
            multiplier *= Mathf.Max(0f, value);
        }

        return multiplier;
    }

    private float ApplyLimitedRuneFlatDamage(
        BattleDamage damage,
        GameObject playerSource,
        bool isPlayerAttackingMonster,
        float amount,
        out int equippedRuneCount,
        out float damagePerRune,
        out float runeFlatDamage,
        out string resultSource)
    {
        equippedRuneCount = 0;
        damagePerRune = 0f;
        runeFlatDamage = 0f;
        resultSource = "Skipped";
        string skipReason = "NotPlayerAttack";
        string skillLabel = damage.skillName;

        if (!damage.bypassAttackerMultipliers && isPlayerAttackingMonster && playerSource != null)
        {
            RuneRuntimeState attackerRunes = playerSource.GetComponent<RuneRuntimeState>()
                                             ?? playerSource.GetComponentInParent<RuneRuntimeState>();
            if (attackerRunes != null)
            {
                damagePerRune = attackerRunes.GetDemoBaseDamageBonusPerEquippedRune();
                bool triggered = attackerRunes.TryConsumeRuneFlatDamage(
                    damage,
                    out equippedRuneCount,
                    out runeFlatDamage,
                    out skillLabel,
                    out skipReason);
                resultSource = triggered ? "RuneRuntimeState.CastFirstHit" : skipReason;
                if (enableCombatBalanceLogs)
                {
                    Debug.Log(
                        $"[RuneFlatDamage] attacker={GetDebugObjectName(playerSource)} skillName={ResolveDamageLabel(skillLabel)} damageSource={ResolveDamageLabel(damage.damageSource)} " +
                        $"equippedRuneCount={equippedRuneCount} runeFlatDamage={runeFlatDamage:F2} triggered={triggered} skipReason={(triggered ? "None" : skipReason)} " +
                        $"cooldownWindow={RuneFlatDamageCooldownWindow:F2} packetId={damage.packetId}",
                        this);
                }

                return triggered ? amount + runeFlatDamage : amount;
            }

            skipReason = "NoRuneRuntimeState";
        }

        if (enableCombatBalanceLogs)
        {
            Debug.Log(
                $"[RuneFlatDamage] attacker={GetDebugObjectName(playerSource)} skillName={ResolveDamageLabel(skillLabel)} damageSource={ResolveDamageLabel(damage.damageSource)} " +
                $"equippedRuneCount=0 runeFlatDamage=0.00 triggered=false skipReason={skipReason} cooldownWindow={RuneFlatDamageCooldownWindow:F2} packetId={damage.packetId}",
                this);
        }

        return amount;
    }

    private static float ApplyRuneEquipResonanceOutgoingMultiplier(
        GameObject playerSource,
        BattleDamage damage,
        bool isPlayerAttackingMonster,
        float amount)
    {
        if (playerSource == null || !isPlayerAttackingMonster || damage.bypassAttackerMultipliers)
        {
            return amount;
        }

        RuneRuntimeState attackerRunes = playerSource.GetComponent<RuneRuntimeState>()
                                         ?? playerSource.GetComponentInParent<RuneRuntimeState>();
        return attackerRunes != null
            ? amount * attackerRunes.GetRuneEquipResonanceOutgoingMultiplier()
            : amount;
    }

    private float ApplyEnemyDebuffOutgoingMultiplier(GameObject monsterSource, BattleDamage damage, float amount)
    {
        if (monsterSource == null || !BattleTargetUtility.IsPlayer(gameObject))
        {
            return amount;
        }

        EnemyDebuffReceiver receiver = monsterSource.GetComponent<EnemyDebuffReceiver>()
                                       ?? monsterSource.GetComponentInParent<EnemyDebuffReceiver>();
        float multiplier = receiver != null ? Mathf.Max(0f, receiver.GetOutgoingDamageMultiplier()) : 1f;
        float result = amount * multiplier;
        if (enableCombatBalanceLogs)
        {
            Debug.Log(
                $"[EnemyDebuffDamage] enemyName={GetDebugObjectName(monsterSource)} attackPath={ResolveAttackKind(damage)} " +
                $"outgoingDamageMultiplier={multiplier:F2} applied={(receiver != null)} skipReason={(receiver != null ? "None" : "NoEnemyDebuffReceiver")}",
                this);
        }

        return result;
    }

    private float ApplyMonsterDamageSafetyClamp(GameObject monsterSource, BattleDamage damage, float amount)
    {
        if (monsterSource == null || !BattleTargetUtility.IsPlayer(gameObject) || amount <= 0f)
        {
            return Mathf.Max(0f, amount);
        }

        MonsterIdentity identity = monsterSource.GetComponent<MonsterIdentity>()
                                   ?? monsterSource.GetComponentInParent<MonsterIdentity>();
        MonsterRank rank = identity != null ? identity.rank : MonsterRank.Normal;
        string attackKind = ResolveAttackKind(damage);
        bool bossStrongSkill = rank == MonsterRank.Boss && IsBossStrongAttack(attackKind);
        float maxHealth = Mathf.Max(1f, MaxHealthValue);
        float maxRatio = rank switch
        {
            MonsterRank.Elite => 0.32f,
            MonsterRank.Boss => bossStrongSkill ? 0.75f : 0.55f,
            _ => 0.18f
        };
        float maxAllowed = maxHealth * maxRatio;
        float result = Mathf.Min(amount, maxAllowed);
        if (enableCombatBalanceLogs)
        {
            Debug.Log(
                $"[MonsterDamageClamp] enemyName={GetDebugObjectName(monsterSource)} enemyRank={rank} attackKind={attackKind} " +
                $"playerMaxHP={maxHealth:F2} damageBeforeClamp={amount:F2} maxAllowedDamage={maxAllowed:F2} damageAfterClamp={result:F2} clamped={result < amount}",
                this);
        }

        return result;
    }

    private void ApplyNightChildFavorableLifesteal(GameObject playerSource, BattleDamage damage, float actualDamage)
    {
        bool secondaryLifestealExcluded = damage.damageKind == BattleDamageKind.ReflectDamage
                                          || damage.damageKind == BattleDamageKind.ThornRetaliation
                                          || damage.damageKind == BattleDamageKind.ThornExplosion
                                          || damage.damageKind == BattleDamageKind.ShieldBonusDamage;
        if (playerSource == null || actualDamage <= 0f || damage.bypassAttackerMultipliers || damage.bypassLifesteal
            || damage.isReflectDamage || secondaryLifestealExcluded || IsReflectDamageTag(damage.debugTag))
        {
            return;
        }

        TwinStateRuntimeBonus bonus = DayNightAffinityDamageModifier.GetTwinStateRuntimeBonus(playerSource);
        bool eligible = bonus.childType == TwinChildRuntimeType.NightChild
                        && bonus.isInNightChildState
                        && bonus.statusType == TwinStateRuntimeType.Buff;
        int attackerId = playerSource.GetInstanceID();
        bool offCooldown = !NextLifestealTimeByAttacker.TryGetValue(attackerId, out float nextTime) || Time.time >= nextTime;
        float healBeforeClamp = eligible ? actualDamage * NightChildFavorableLifestealRate : 0f;
        CombatHealth attackerHealth = playerSource.GetComponent<CombatHealth>() ?? playerSource.GetComponentInParent<CombatHealth>();
        float healAfterClamp = attackerHealth != null
            ? Mathf.Min(healBeforeClamp, attackerHealth.MaxHealthValue * NightChildLifestealMaxHealthRatio)
            : 0f;
        bool triggered = eligible && offCooldown && attackerHealth != null && !attackerHealth.IsDead && healAfterClamp > 0f;
        if (triggered)
        {
            NextLifestealTimeByAttacker[attackerId] = Time.time + TwinLifestealCooldownWindow;
            attackerHealth.Heal(healAfterClamp);
        }

        if (enableCombatBalanceLogs)
        {
            string skipReason = triggered ? "None" : !eligible ? "TwinBuffInactive" : !offCooldown ? "CooldownWindow" : attackerHealth == null ? "NoCombatHealth" : "NoEffectiveDamage";
            Debug.Log(
                $"[Lifesteal] character={GetDebugObjectName(playerSource)} skillName={ResolveDamageLabel(damage.skillName)} damageDealt={actualDamage:F2} " +
                $"lifestealRate={(eligible ? NightChildFavorableLifestealRate : 0f):F2} healAmountBeforeClamp={healBeforeClamp:F2} " +
                $"healAmountAfterClamp={healAfterClamp:F2} triggered={triggered} skipReason={skipReason}",
                this);
        }
    }

    private static string ResolveAttackKind(BattleDamage damage)
    {
        if (!string.IsNullOrWhiteSpace(damage.attackKind)) return damage.attackKind;
        if (!string.IsNullOrWhiteSpace(damage.debugTag)) return damage.debugTag;
        if (!string.IsNullOrWhiteSpace(damage.damageSource)) return damage.damageSource;
        return "Unknown";
    }

    private static string ResolveDamageLabel(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
    }

    private static bool IsBossStrongAttack(string attackKind)
    {
        return attackKind.IndexOf("Devour", StringComparison.OrdinalIgnoreCase) >= 0
               || attackKind.IndexOf("Leap", StringComparison.OrdinalIgnoreCase) >= 0
               || attackKind.IndexOf("Strong", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsReflectDamageTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return false;
        return tag.IndexOf("Thorn", StringComparison.OrdinalIgnoreCase) >= 0
               || tag.IndexOf("Reflect", StringComparison.OrdinalIgnoreCase) >= 0
               || tag.IndexOf("Retaliation", StringComparison.OrdinalIgnoreCase) >= 0
               || tag.IndexOf("Counter", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void LogDamageEntry(string entry, BattleDamage damage, bool defenseHandledBySkill)
    {
        if (!CombatRuntimeAuditLogger.IsEnabled)
        {
            return;
        }

        Debug.Log(
            $"[PlayerDamagePipelineAudit] entry={entry} attacker={GetDebugObjectName(damage.source)} target={name} damageType={damage.damageType} " +
            $"rawAmount={damage.amount:F2} defenseHandledBySkill={defenseHandledBySkill} usesUnifiedPostProcess=true warning=None",
            this);
    }

    private static string GetModifierKey(object source)
    {
        if (source == null)
        {
            return string.Empty;
        }

        return source is string stringKey ? stringKey : source.GetHashCode().ToString();
    }

    private static bool ShouldCountAsSuccessfulHit(float inputDamage, float outgoingDamage, float preShieldDamage)
    {
        return inputDamage > 0f || outgoingDamage > 0f || preShieldDamage > 0f;
    }

    private void NormalizeDamageMetadata(ref BattleDamage damage, GameObject resolvedPlayerSource, GameObject resolvedMonsterSource)
    {
        if (damage.sourceOwner == null)
        {
            damage.sourceOwner = resolvedPlayerSource != null
                ? resolvedPlayerSource
                : resolvedMonsterSource != null ? resolvedMonsterSource : damage.source;
        }

        if (damage.damageKind == BattleDamageKind.Unknown)
        {
            if (damage.isReflectDamage)
            {
                damage.damageKind = BattleDamageKind.ReflectDamage;
            }
            else if (resolvedPlayerSource != null && BattleTargetUtility.IsMonster(gameObject))
            {
                damage.damageKind = BattleDamageKind.PlayerActiveSkill;
            }
            else if (resolvedMonsterSource != null && BattleTargetUtility.IsPlayer(gameObject))
            {
                damage.damageKind = BattleDamageKind.MonsterDamage;
            }
            else if (damage.source != null && (damage.source == gameObject || damage.source.transform.root == transform.root))
            {
                damage.damageKind = BattleDamageKind.SelfDamage;
            }
            else
            {
                damage.damageKind = BattleDamageKind.EnvironmentDamage;
            }
        }

        if (enableCombatBalanceLogs)
        {
            Debug.Log(
                $"[DamageMetadata] attacker={GetDebugObjectName(damage.source)} target={name} skillName={ResolveDamageLabel(damage.skillName)} " +
                $"castId={damage.castId} damageKind={damage.damageKind} sourceOwner={GetDebugObjectName(damage.sourceOwner)} " +
                $"hasCastId={damage.castId > 0} usesFallbackWindow={damage.castId <= 0}",
                this);
        }
    }

    private static bool ShouldSuppressThornReaction(BattleDamage damage)
    {
        return damage.suppressThornReaction
               || damage.isReflectDamage
               || damage.damageKind == BattleDamageKind.ReflectDamage
               || damage.damageKind == BattleDamageKind.ThornRetaliation
               || damage.damageKind == BattleDamageKind.ThornExplosion
               || damage.damageKind == BattleDamageKind.ShieldBonusDamage
               || damage.damageKind == BattleDamageKind.MarkExplosion;
    }

    private void NotifyRuneIncomingMonsterHit(
        BattleDamage damage,
        GameObject resolvedMonsterSource,
        float effectiveDamageBeforeShield,
        string entry)
    {
        if (effectiveDamageBeforeShield <= 0f)
        {
            return;
        }

        ThornCounterEntryLog(entry, gameObject, damage.source, effectiveDamageBeforeShield);
        ThornCounterEntryLog(
            entry + ":ResolvedSource",
            gameObject,
            resolvedMonsterSource != null ? resolvedMonsterSource : damage.source,
            effectiveDamageBeforeShield);
        bool targetIsPlayer = BattleTargetUtility.IsPlayer(gameObject);
        runeRuntimeState = ResolveRuneRuntimeState();
        ThornCounterNotifyCheckLog(targetIsPlayer, runeRuntimeState, resolvedMonsterSource);
        if (!targetIsPlayer)
        {
            ThornCounterNotifySkippedLog("target-not-player");
        }
        else if (runeRuntimeState == null)
        {
            ThornCounterNotifySkippedLog("rune-runtime-state-null");
        }
        else if (resolvedMonsterSource == null)
        {
            ThornCounterNotifySkippedLog("source-not-monster");
        }
        else if (ShouldSuppressThornReaction(damage))
        {
            ThornCounterNotifySkippedLog("damage-metadata-excluded");
        }
        else
        {
            runeRuntimeState.NotifyIncomingMonsterDamage(resolvedMonsterSource, effectiveDamageBeforeShield);
        }
    }

    private static GameObject ResolveIncomingMonsterSource(GameObject source)
    {
        if (source == null)
        {
            return null;
        }

        if (BattleTargetUtility.IsMonster(source))
        {
            CombatHealth sourceHealth = source.GetComponentInParent<CombatHealth>();
            if (sourceHealth != null)
            {
                return sourceHealth.gameObject;
            }

            return source.transform.root != null ? source.transform.root.gameObject : source;
        }

        CombatHealth parentHealth = source.GetComponentInParent<CombatHealth>();
        if (parentHealth != null && BattleTargetUtility.IsMonster(parentHealth.gameObject))
        {
            return parentHealth.gameObject;
        }

        EnemyController enemyController = source.GetComponentInParent<EnemyController>();
        if (enemyController != null)
        {
            return enemyController.gameObject;
        }

        MonsterIdentity monsterIdentity = source.GetComponentInParent<MonsterIdentity>();
        if (monsterIdentity != null)
        {
            return monsterIdentity.gameObject;
        }

        return null;
    }

    private static GameObject ResolveDamageModifierSource(GameObject originalSource, GameObject resolvedPlayerSource, GameObject resolvedMonsterSource)
    {
        if (resolvedPlayerSource != null)
        {
            return resolvedPlayerSource;
        }

        if (resolvedMonsterSource != null)
        {
            return resolvedMonsterSource;
        }

        return originalSource;
    }

    private RuneRuntimeState ResolveRuneRuntimeState()
    {
        if (runeRuntimeState != null)
        {
            return runeRuntimeState;
        }

        runeRuntimeState = GetComponent<RuneRuntimeState>();
        if (runeRuntimeState != null)
        {
            return runeRuntimeState;
        }

        runeRuntimeState = GetComponentInParent<RuneRuntimeState>();
        if (runeRuntimeState != null)
        {
            return runeRuntimeState;
        }

        runeRuntimeState = GetComponentInChildren<RuneRuntimeState>();
        return runeRuntimeState;
    }

    private void HandleResourceBankOnShieldChanged(float currentShield, float maxShield)
    {
        NotifyShieldStateChanged();
        LogNoRuneShieldAudit(
            string.IsNullOrWhiteSpace(pendingShieldAuditSource) ? "ExternalResourceBankChange" : pendingShieldAuditSource,
            "ShieldChanged",
            battleStartAudit: false);
    }

    private void ResetPlayerShieldAtBattleStart()
    {
        if (!BattleTargetUtility.IsPlayer(gameObject))
        {
            return;
        }

        battleStartShieldResetPerformed = true;
        battleStartShieldCarryOverDetected = GetShield() > 0f;
        if (resourceBank != null)
        {
            resourceBank.ClearShield();
        }

        localShield = 0f;
        localMaxShield = 0f;
        PlayerTimedShieldStatus[] timedShields = GetComponents<PlayerTimedShieldStatus>();
        for (int i = 0; i < timedShields.Length; i++)
        {
            timedShields[i]?.ClearShield();
        }
    }

    private void LogNoRuneShieldAudit(string source, string reason, bool battleStartAudit)
    {
        if (!enableCombatBalanceLogs || !BattleTargetUtility.IsPlayer(gameObject))
        {
            return;
        }

        if (battleStartAudit)
        {
            if (battleStartShieldAuditLogged)
            {
                return;
            }

            battleStartShieldAuditLogged = true;
        }
        else
        {
            if (runtimeShieldSourceAuditLogged || GetShield() <= 0f)
            {
                return;
            }

            runtimeShieldSourceAuditLogged = true;
        }

        int equippedRuneCount = BattleStatUtility.GetEquippedRuneCount(gameObject, out _);
        string safeSource = string.IsNullOrWhiteSpace(source) ? "Unknown" : source;
        bool fromSkill = safeSource.IndexOf("Skill", StringComparison.OrdinalIgnoreCase) >= 0
                         || safeSource.IndexOf("Player02W", StringComparison.OrdinalIgnoreCase) >= 0;
        bool fromRune = safeSource.IndexOf("Rune", StringComparison.OrdinalIgnoreCase) >= 0;
        bool fromPassive = safeSource.IndexOf("Passive", StringComparison.OrdinalIgnoreCase) >= 0
                           || safeSource.IndexOf("Favor", StringComparison.OrdinalIgnoreCase) >= 0
                           || safeSource.IndexOf("Blessing", StringComparison.OrdinalIgnoreCase) >= 0
                           || safeSource.IndexOf("Function", StringComparison.OrdinalIgnoreCase) >= 0;
        bool fromCarryOver = string.Equals(safeSource, "BattleStartCarryOver", StringComparison.OrdinalIgnoreCase);
        Debug.Log(
            $"[NoRuneShieldAudit] equippedRuneCount={equippedRuneCount} currentShield={GetShield():F2} maxShield={GetMaxShield():F2} " +
            $"source={safeSource} fromSkill={fromSkill} fromRune={fromRune} fromPassive={fromPassive} " +
            $"fromCarryOver={fromCarryOver} battleStartCleared={battleStartShieldResetPerformed} reason={reason}",
            this);
    }

    private DamagePopupType ResolvePopupType(BattleDamageType damageType)
    {
        return damageType == BattleDamageType.Special ? DamagePopupType.Special : DamagePopupType.Physical;
    }

    private bool TryEvadeDamage(BattleDamage damage, out float finalEvasionChance)
    {
        GameObject source = damage.source;
        finalEvasionChance = 0f;
        CombatStats defenderStats = stats;
        CombatStats attackerStats = BattleStatUtility.GetCombatStats(source);
        if (damage.bypassEvasion)
        {
            lastIncomingDamageFinalHitChance = 1f;
            lastIncomingDamageFinalEvasionChance = 0f;
            lastIncomingDamageMissRoll = -1f;
            lastIncomingDamageWasMiss = false;
            LogPlayerAccuracyAudit(damage, defenderStats, attackerStats, 0f, 0f, 0f, 1f, -1f, false, "BypassEvasion");
            return false;
        }

        BattleStatUtility.ResolveFinalEvasionAndHitChance(
            gameObject,
            source,
            out float rawEvasionChance,
            out float clampedEvasionChance,
            out finalEvasionChance,
            out float finalHitChance);
        float randomRoll = UnityEngine.Random.value;
        bool evaded = finalEvasionChance > 0f && randomRoll < finalEvasionChance;
        lastIncomingDamageFinalHitChance = finalHitChance;
        lastIncomingDamageFinalEvasionChance = finalEvasionChance;
        lastIncomingDamageMissRoll = randomRoll;
        lastIncomingDamageWasMiss = evaded;
        Debug.Log(
            $"[CombatEvasion] attacker={(source != null ? source.name : "null")} attackerRank={BattleStatUtility.GetAttackerRankLabel(source)} defender={name} defenderSpeed={(defenderStats != null ? defenderStats.speed : 0f):F2} defenderLuck={(defenderStats != null ? defenderStats.luck : 0f):F2} attackerSpeed={(attackerStats != null ? attackerStats.speed : 0f):F2} rawEvasionChance={rawEvasionChance:F4} clampedEvasionChance={clampedEvasionChance:F4} accuracyMultiplier={BattleStatUtility.GetAccuracyMultiplier(attackerStats):F2} finalEvasionChance={finalEvasionChance:F4} finalHitChance={finalHitChance:F4} randomRoll={randomRoll:F4} result={(evaded ? "Miss" : "Hit")}",
            this);
        LogPlayerAccuracyAudit(damage, defenderStats, attackerStats, rawEvasionChance, clampedEvasionChance, finalEvasionChance, finalHitChance, randomRoll, evaded, "Roll");

        if (!evaded)
        {
            return false;
        }

        if (BattleTargetUtility.IsPlayer(gameObject) && BattleTargetUtility.IsMonster(source))
        {
            Debug.Log($"[EnemyAttack] Evaded target={name} attacker={source.name}", this);
        }

        return true;
    }

    private void LogPlayerAccuracyAudit(
        BattleDamage damage,
        CombatStats defenderStats,
        CombatStats attackerStats,
        float rawEvasionChance,
        float clampedEvasionChance,
        float finalEvasionChance,
        float finalHitChance,
        float randomRoll,
        bool evaded,
        string sourceMethod)
    {
        if (!BattleTargetUtility.IsPlayer(damage.source) || !BattleTargetUtility.IsMonster(gameObject))
        {
            return;
        }

        Debug.Log(
            "[PlayerAccuracyAudit] " +
            $"activeCharacter={(damage.source != null ? damage.source.name : "null")} " +
            $"skill={(string.IsNullOrWhiteSpace(damage.debugTag) ? "Unknown" : damage.debugTag)} " +
            "baseAccuracy=1.0000 " +
            $"accuracyMultiplier={BattleStatUtility.GetAccuracyMultiplier(attackerStats):F4} " +
            $"attackerSpeed={(attackerStats != null ? attackerStats.speed : 0f):F2} " +
            $"target={name} " +
            $"targetSpeed={(defenderStats != null ? defenderStats.speed : 0f):F2} " +
            $"targetLuck={(defenderStats != null ? defenderStats.luck : 0f):F2} " +
            $"rawEvasionChance={rawEvasionChance:F4} " +
            $"clampedEvasionChance={clampedEvasionChance:F4} " +
            $"targetEvasion={finalEvasionChance:F4} " +
            $"finalHitChance={finalHitChance:F4} " +
            $"randomRoll={randomRoll:F4} " +
            $"bypassEvasion={damage.bypassEvasion} " +
            $"result={(evaded ? "Miss" : "Hit")} " +
            $"sourceMethod={sourceMethod}",
            this);
    }

    private float ResolveCurrentHealthForDebug()
    {
        return resourceBank != null
            ? Mathf.Max(0f, resourceBank.currentHealth)
            : Mathf.Max(0f, currentHealth);
    }

    private void ShowDamagePopup(float damage, DamagePopupType popupType, bool isCritical)
    {
        if (!showDamageNumbers || damage <= 0f)
        {
            return;
        }

        Vector3 worldPosition = transform.position + damagePopupOffset;
        worldPosition.x += UnityEngine.Random.Range(-damagePopupRandomOffset.x, damagePopupRandomOffset.x);
        worldPosition.y += UnityEngine.Random.Range(-damagePopupRandomOffset.y, damagePopupRandomOffset.y);
        worldPosition.z += UnityEngine.Random.Range(-damagePopupRandomOffset.x, damagePopupRandomOffset.x);

        string message = Mathf.RoundToInt(damage).ToString();

        Color color = ResolveDamagePopupColor(popupType, isCritical);
        DamagePopupFloatingText popupPrefab = ResolveDamagePopupPrefab();
        if (popupPrefab != null)
        {
            DamagePopupFloatingText popup = Instantiate(popupPrefab, worldPosition, Quaternion.identity);
            popup.Show(message, color);
        }
        else
        {
            if (!warnedMissingDamagePopupPrefab)
            {
                warnedMissingDamagePopupPrefab = true;
                Debug.LogWarning("[CombatHealth] damagePopupPrefab is not assigned and no default prefab was found at Resources/Prefabs/UI/DamagePopupFloatingText. Using runtime fallback popup.", this);
            }

            DamagePopupFloatingText.SpawnFallback(message, worldPosition, color);
        }
    }

    private void ShowMissPopup()
    {
        if (!showDamageNumbers)
        {
            return;
        }

        Vector3 worldPosition = transform.position + damagePopupOffset;
        worldPosition.x += UnityEngine.Random.Range(-damagePopupRandomOffset.x, damagePopupRandomOffset.x);
        worldPosition.y += UnityEngine.Random.Range(-damagePopupRandomOffset.y, damagePopupRandomOffset.y);
        worldPosition.z += UnityEngine.Random.Range(-damagePopupRandomOffset.x, damagePopupRandomOffset.x);

        DamagePopupFloatingText popupPrefab = ResolveDamagePopupPrefab();
        if (popupPrefab != null)
        {
            DamagePopupFloatingText popup = Instantiate(popupPrefab, worldPosition, Quaternion.identity);
            popup.Show("miss", missDamageColor);
        }
        else
        {
            DamagePopupFloatingText.SpawnFallback("miss", worldPosition, missDamageColor);
        }
    }

    private DamagePopupFloatingText ResolveDamagePopupPrefab()
    {
        if (damagePopupPrefab != null)
        {
            return damagePopupPrefab;
        }

        if (!attemptedLoadDefaultDamagePopupPrefab)
        {
            attemptedLoadDefaultDamagePopupPrefab = true;
            defaultDamagePopupPrefab = Resources.Load<DamagePopupFloatingText>("Prefabs/UI/DamagePopupFloatingText");
        }

        return defaultDamagePopupPrefab;
    }

    private Color ResolveDamagePopupColor(DamagePopupType popupType, bool isCritical)
    {
        if (isCritical)
        {
            return criticalDamageColor;
        }

        return normalDamageColor;
    }

    private void Die(GameObject killer)
    {
        if (dead)
        {
            return;
        }

        if (BattleTargetUtility.IsPlayer(gameObject))
        {
            LogPlayerDeathTrace($"HP reached zero on {name}");
            LogPlayerDeathAudit(killer);
        }

        dead = true;
        Died?.Invoke(killer);

        if (BattleTargetUtility.IsPlayer(gameObject))
        {
            LogPlayerDeathTrace("Death event invoked");
        }

        TriggerAnimation(deathTrigger);

        if (destroyOnDeath)
        {
            if (GetComponent<DissolveOnDeath>() != null)
            {
                Debug.Log($"[DeathFlow] Skip immediate destroy because DissolveOnDeath exists owner={name}", this);
            }
            else
            {
                Destroy(gameObject, destroyDelayAfterDeath);
            }
        }
    }

    private void TriggerAnimation(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
        {
            return;
        }

        animator.SetTrigger(triggerName);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void ThornCounterEntryLog(string methodName, GameObject target, GameObject source, float finalDamage)
    {
        Debug.Log(
            $"[ThornCounter] Damage entry reached. method={methodName}, target={(target != null ? target.name : "<null>")}, source={(source != null ? source.name : "<null>")}, finalDamage={finalDamage:F2}",
            this);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void ThornCounterNotifyCheckLog(bool targetIsPlayer, RuneRuntimeState runtimeState, GameObject resolvedSource)
    {
        Debug.Log(
            $"[ThornCounter] Notify check. targetIsPlayer={targetIsPlayer}, runtimeState={(runtimeState != null ? runtimeState.GetType().Name : "<null>")}, resolvedSource={(resolvedSource != null ? resolvedSource.name : "<null>")}",
            this);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void ThornCounterNotifySkippedLog(string reason)
    {
        Debug.Log($"[ThornCounter] Notify skipped. reason={reason}", this);
    }

    private void DayNightGaugeHitFlowLog(string message, UnityEngine.Object context)
    {
        if (!IsDayNightGaugeDebugEnabled())
        {
            return;
        }

        Debug.Log($"[DayNightHitFlow] {message}", context);
    }

    private static bool IsDayNightGaugeDebugEnabled()
    {
        return DayNightGaugeRuntimeState.TryGetExistingInstance(out DayNightGaugeRuntimeState gauge) && gauge != null && gauge.DebugHitFlowEnabled;
    }

    private static string GetDebugObjectName(GameObject target)
    {
        if (target == null)
        {
            return "<null>";
        }

        return $"{target.name} ({GetHierarchyPath(target.transform)})";
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "<null>";
        }

        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }

    private void LogPlayerDeathTrace(string message)
    {
        if (!debugPlayerDeathTrace)
        {
            return;
        }

        Debug.Log("[PlayerDeathTrace] " + message, this);
    }

    private void LogPlayerDeathAudit(GameObject killer)
    {
        RuneRuntimeState runtimeState = ResolveRuneRuntimeState();
        string equippedRunes = runtimeState != null
            ? "total=" + runtimeState.GetTotalEquippedRuneCount()
                + " life=" + runtimeState.GetGlobalRuneCount(RuneType.Life)
                + " shield=" + runtimeState.GetGlobalRuneCount(RuneType.Shield)
                + " mana=" + runtimeState.GetGlobalRuneCount(RuneType.Mana)
                + " thorn=" + runtimeState.GetGlobalRuneCount(RuneType.Thorn)
                + " luck=" + runtimeState.GetGlobalRuneCount(RuneType.Luck)
            : "none";
        float currentHp = resourceBank != null ? resourceBank.currentHealth : currentHealth;
        float maxHp = resourceBank != null ? resourceBank.maxHealth : MaxHealthValue;

        Debug.Log(
            "[PlayerDeathAudit] " +
            "currentHPBefore=" + lastCurrentHealthBeforeDeath.ToString("F2") +
            " currentHPAfter=" + currentHp.ToString("F2") +
            " maxHPBefore=" + lastMaxHealthBeforeDeath.ToString("F2") +
            " maxHP=" + maxHp.ToString("F2") +
            " lastDamage=" + lastDamageBeforeDeath.ToString("F2") +
            " deathReason=HealthReachedZero" +
            " sourceMethod=" + lastDamageSourceMethod +
            " sourceObject=" + GetDebugObjectName(lastDamageSourceObject) +
            " killer=" + GetDebugObjectName(killer) +
            " activeCharacter=" + name +
            " equippedRunes=" + equippedRunes +
            " stackTrace=" + Environment.StackTrace,
            this);
    }
}
