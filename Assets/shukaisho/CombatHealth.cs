using System;
using System.Collections.Generic;
using UnityEngine;

public class CombatHealth : MonoBehaviour
{
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
        bool logBossPlayerDamage = ShouldLogBossPlayerDamageFlow(damage.source);
        float playerHpBefore = ResolveCurrentHealthForDebug();
        float playerShieldBefore = GetShield();
        bool shouldLogDamageApply = BattleTargetUtility.IsPlayer(gameObject) || logBossPlayerDamage;
        float combatHealthCurrentBefore = currentHealth;
        float resourceBankCurrentBefore = resourceBank != null ? resourceBank.currentHealth : -1f;
        string noEffectiveDamageReason = string.Empty;

        if (dead)
        {
            if (logBossPlayerDamage)
            {
                Debug.Log(
                    "[BossMeleeDamageFlow] enemy=" + GetDebugObjectName(damage.source) +
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
                    "[BossMeleeDamageFlow] enemy=" + GetDebugObjectName(damage.source) +
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

        if (TryEvadeDamage(damage.source, out _))
        {
            if (logBossPlayerDamage)
            {
                Debug.Log(
                    "[BossMeleeDamageFlow] enemy=" + GetDebugObjectName(damage.source) +
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
            if (logBossPlayerDamage)
            {
                Debug.Log(
                    "[BossMeleeDamageFlow] enemy=" + GetDebugObjectName(damage.source) +
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

        float outgoingDamage = damage.bypassAttackerMultipliers
            ? Mathf.Max(0f, damage.amount)
            : BattleStatUtility.ApplyPlayerMoveSpeedDamageBonus(damage.source, damage.amount);
        CombatStats attackerStats = !damage.bypassAttackerMultipliers ? BattleStatUtility.GetCombatStats(damage.source) : null;
        if (!damage.bypassAttackerMultipliers && attackerStats != null)
        {
            outgoingDamage *= Mathf.Max(0f, attackerStats.outgoingDamageMultiplier);
        }
        damage.amount = outgoingDamage;
        float reducedDamage = stats != null ? stats.ReduceDamage(damage) : outgoingDamage;
        float finalDamage = reducedDamage;
        if (!damage.bypassAffinityModifier)
        {
            bool includeAmbientAffinity = !damage.bypassAttackerMultipliers && !damage.bypassAmbientAffinity;
            finalDamage = DayNightAffinityDamageModifier.ApplyModifier(damage.source, gameObject, finalDamage, out _, includeAmbientAffinity);
        }
        float afterAffinityDamage = finalDamage;
        GameObject resolvedMonsterSource = ResolveIncomingMonsterSource(damage.source);
        finalDamage = TwinStateCombatBonus.ApplyNightChildIncomingDamageReduction(gameObject, resolvedMonsterSource, finalDamage, this, damage.debugTag);
        float afterTwinReductionDamage = finalDamage;
        runeRuntimeState = ResolveRuneRuntimeState();
        if (resolvedMonsterSource != null && runeRuntimeState != null)
        {
            finalDamage *= runeRuntimeState.GetIncomingMonsterDamageMultiplier(resolvedMonsterSource, finalDamage);
        }
        float afterRuneDamage = finalDamage;
        finalDamage *= GetIncomingDamageMultiplier();
        float afterIncomingMultiplierDamage = finalDamage;
        float resolvedDamageBeforeShieldAndGuard = Mathf.Max(0f, finalDamage);
        finalDamage = AbsorbShieldDamage(finalDamage);
        float afterShieldDamage = finalDamage;
        Player2PrototypeController player2 = GetComponent<Player2PrototypeController>();
        if (player2 != null)
        {
            finalDamage = player2.ProcessIncomingDamageWithWGuard(finalDamage, damage);
        }
        float afterGuardDamage = finalDamage;

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
            Debug.Log(
                "[CombatHealthDamageApply] " +
                "target=" + name +
                " incomingDamage=" + damage.amount.ToString("F2") +
                " outgoingDamage=" + outgoingDamage.ToString("F2") +
                " reducedDamage=" + reducedDamage.ToString("F2") +
                " afterAffinityDamage=" + afterAffinityDamage.ToString("F2") +
                " afterTwinReductionDamage=" + afterTwinReductionDamage.ToString("F2") +
                " afterRuneDamage=" + afterRuneDamage.ToString("F2") +
                " afterIncomingMultiplierDamage=" + afterIncomingMultiplierDamage.ToString("F2") +
                " afterShieldDamage=" + afterShieldDamage.ToString("F2") +
                " finalDamage=" + finalDamage.ToString("F2") +
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
            bool notifiedGauge = DayNightAffinityDamageModifier.NotifySuccessfulPlayerHit(damage.source, gameObject);
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
                    "[BossMeleeDamageFlow] enemy=" + GetDebugObjectName(damage.source) +
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

        if (finalDamage > 0f)
        {
            ThornCounterEntryLog("TakeDamage(BattleDamage)", gameObject, damage.source, finalDamage);
            ThornCounterEntryLog(
                "TakeDamage(BattleDamage):ResolvedSource",
                gameObject,
                resolvedMonsterSource != null ? resolvedMonsterSource : damage.source,
                finalDamage);
            bool targetIsPlayer = BattleTargetUtility.IsPlayer(gameObject);
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
            else
            {
                runeRuntimeState.NotifyIncomingMonsterDamage(resolvedMonsterSource, finalDamage);
            }

            Damaged?.Invoke(finalDamage, damage.source);
            ShowDamagePopup(finalDamage, ResolvePopupType(damage.damageType), damage.isCritical);
            TriggerAnimation(hitTrigger);

            if (logBossPlayerDamage)
            {
                Debug.Log(
                    "[BossMeleeDamageFlow] enemy=" + GetDebugObjectName(damage.source) +
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
        return sourceIdentity != null && sourceIdentity.rank == MonsterRank.Boss;
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

        if (TryEvadeDamage(damage.source, out _))
        {
            ShowMissPopup();
            DayNightGaugeHitFlowLog($"ApplyDirectDamage skipped reason=miss source={GetDebugObjectName(damage.source)} target={GetDebugObjectName(gameObject)}", gameObject);
            return;
        }

        float finalDamage = damage.bypassAttackerMultipliers
            ? Mathf.Max(0f, damage.amount)
            : BattleStatUtility.ApplyPlayerMoveSpeedDamageBonus(damage.source, damage.amount);
        if (!damage.bypassAffinityModifier)
        {
            bool includeAmbientAffinity = !damage.bypassAttackerMultipliers && !damage.bypassAmbientAffinity;
            finalDamage = DayNightAffinityDamageModifier.ApplyModifier(damage.source, gameObject, finalDamage, out _, includeAmbientAffinity);
        }
        GameObject resolvedMonsterSource = ResolveIncomingMonsterSource(damage.source);
        finalDamage = TwinStateCombatBonus.ApplyNightChildIncomingDamageReduction(gameObject, resolvedMonsterSource, finalDamage, this, damage.debugTag);
        runeRuntimeState = ResolveRuneRuntimeState();
        if (resolvedMonsterSource != null && runeRuntimeState != null)
        {
            finalDamage *= runeRuntimeState.GetIncomingMonsterDamageMultiplier(resolvedMonsterSource, finalDamage);
        }
        finalDamage *= GetIncomingDamageMultiplier();
        float resolvedDamageBeforeShield = Mathf.Max(0f, finalDamage);
        finalDamage = AbsorbShieldDamage(finalDamage);

        if (resourceBank != null)
        {
            resourceBank.currentHealth = Mathf.Max(0f, resourceBank.currentHealth - finalDamage);
            currentHealth = resourceBank.currentHealth;
        }
        else
        {
            currentHealth = Mathf.Max(0f, currentHealth - finalDamage);
        }

        bool shouldNotifyGaugeHit = ShouldCountAsSuccessfulHit(damage.amount, finalDamage, resolvedDamageBeforeShield);
        if (shouldNotifyGaugeHit && !damage.suppressGaugeNotification)
        {
            bool notifiedGauge = DayNightAffinityDamageModifier.NotifySuccessfulPlayerHit(damage.source, gameObject);
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

        if (finalDamage > 0f)
        {
            ThornCounterEntryLog("ApplyDirectDamage", gameObject, damage.source, finalDamage);
            ThornCounterEntryLog(
                "ApplyDirectDamage:ResolvedSource",
                gameObject,
                resolvedMonsterSource != null ? resolvedMonsterSource : damage.source,
                finalDamage);
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
            else
            {
                runeRuntimeState.NotifyIncomingMonsterDamage(resolvedMonsterSource, finalDamage);
            }

            Damaged?.Invoke(finalDamage, damage.source);
            ShowDamagePopup(finalDamage, popupType, damage.isCritical);
            TriggerAnimation(hitTrigger);
        }

        if (currentHealth <= 0f)
        {
            Die(damage.source);
        }
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

    public void SetShield(float amount)
    {
        amount = Mathf.Max(0f, amount);
        if (resourceBank != null)
        {
            resourceBank.SetShield(amount);
            return;
        }

        localShield = amount;
        localMaxShield = amount;
        NotifyShieldStateChanged();
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

        return totalShield;
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

        return totalMaxShield;
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

    private float AbsorbShieldDamage(float amount)
    {
        amount = Mathf.Max(0f, amount);
        PlayerTimedShieldStatus[] timedShields = GetComponents<PlayerTimedShieldStatus>();
        for (int i = 0; i < timedShields.Length; i++)
        {
            if (timedShields[i] == null)
            {
                continue;
            }

            amount = timedShields[i].AbsorbDamage(amount);
            if (amount <= 0f)
            {
                return 0f;
            }
        }

        float baseShield = GetBaseShield();
        float shieldDamageMultiplier = runeRuntimeState != null ? runeRuntimeState.GetShieldDamageTakenMultiplier() : 1f;
        float shieldUsed = Mathf.Min(baseShield, amount * shieldDamageMultiplier);
        if (shieldUsed <= 0f)
        {
            return amount;
        }

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
            runeRuntimeState?.NotifyShieldBrokenByMonsterDamage(baseShield);
        }

        float absorbedIncomingDamage = shieldDamageMultiplier > 0f ? shieldUsed / shieldDamageMultiplier : amount;
        return Mathf.Max(0f, amount - absorbedIncomingDamage);
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
    }

    private DamagePopupType ResolvePopupType(BattleDamageType damageType)
    {
        return damageType == BattleDamageType.Special ? DamagePopupType.Special : DamagePopupType.Physical;
    }

    private bool TryEvadeDamage(GameObject source, out float finalEvasionChance)
    {
        finalEvasionChance = 0f;
        CombatStats defenderStats = stats;
        CombatStats attackerStats = BattleStatUtility.GetCombatStats(source);
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
}
