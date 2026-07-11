using System.Collections;
using System.Reflection;
using UnityEngine;
using Spine;
using Spine.Unity;
using UnityEngine.Serialization;

public class Player1Skill_Q_QuickShear : Player01SkillBase
{
    [Header("Q")]
    [SerializeField, Min(1)] private int slashCount = 3;
    [SerializeField, Min(0f)] private float slashInterval = 0.15f;
    [Header("Q - QuickShear / Damage Parameters")]
    [Tooltip("每次斩击使用的物理段基础伤害。")]
    [FormerlySerializedAs("baseDamage")]
    [SerializeField, Min(0f)] private float physicalBaseDamage = 30f;
    [Tooltip("每次斩击使用的特殊段基础伤害。")]
    [SerializeField, Min(0f)] private float specialBaseDamage = 20f;
    [Tooltip("Q 特殊段从物理攻击获得的倍率。")]
    [SerializeField, Min(0f)] private float physicalScaling = 0.2f;
    [Tooltip("Q 物理段从特殊攻击获得的倍率。")]
    [SerializeField, Min(0f)] private float specialScaling = 0.6f;
    [Tooltip("每段斩击的最终伤害倍率。")]
    [SerializeField, Min(0f)] private float quickShearPerSlashDamageMultiplier = 1f;
    [Tooltip("Q 固定追加伤害。")]
    [SerializeField, Min(0f)] private float quickShearBonusDamage = 0f;
    [Tooltip("Q 总伤害最终倍率。")]
    [SerializeField, Min(0f)] private float quickShearFinalDamageMultiplier = 1f;
    [Tooltip("Q 在帷幕类技能状态下的额外伤害修正。默认 1 表示不额外修正。")]
    [SerializeField, Min(0f)] private float quickShearVeilBarrierDamageMultiplier = 1f;
    [SerializeField, Min(0f)] private float qRange = 2f;
    [SerializeField] private LayerMask enemyLayer = ~0;
    [SerializeField] private Transform hitPoint;

    [Header("Cost")]
    [InspectorName("Mana Cost")]
    [SerializeField, Min(0f)] private float manaCost = 10f;
    [SerializeField, Range(0f, 1f)] private float quickShearLifeStealRatio = 0.5f;
    [Header("Night Buff")]
    [SerializeField, Min(1f)] private float nightBuffLifeStealMultiplier = 1.5f;
    [SerializeField, Range(0f, 1f)] private float nightBuffSlowMoveSpeedMultiplier = 0.8f;
    [SerializeField, Min(0f)] private float nightBuffSlowDuration = 1.5f;

    [Header("Q - Scissor Effects")]
    [SerializeField] private GameObject scissorCutEffectPrefab;
    [SerializeField] private GameObject scissorSlashWaveEffectPrefab;
    [SerializeField] private GameObject scissorEndEffectPrefab;
    [SerializeField] private Vector3 scissorCutEffectOffset = new Vector3(0.9f, 0.15f, 0f);
    [SerializeField] private Vector3 scissorSlashWaveEffectOffset = new Vector3(1.35f, 0.15f, 0f);
    [SerializeField] private Vector3 scissorEndEffectOffset = new Vector3(0.95f, 0.15f, 0f);
    [SerializeField] private Vector3 scissorCutEffectScale = new Vector3(1.2f, 1.2f, 1f);
    [SerializeField] private Vector3 scissorSlashWaveEffectScale = new Vector3(1.5f, 1.5f, 1f);
    [SerializeField] private Vector3 scissorEndEffectScale = new Vector3(1.2f, 1.2f, 1f);
    [SerializeField, Min(0.01f)] private float scissorCutEffectLifetime = 0.18f;
    [SerializeField, Min(0.01f)] private float scissorSlashWaveEffectLifetime = 0.20f;
    [SerializeField, Min(0.01f)] private float scissorEndEffectLifetime = 0.18f;
    [SerializeField] private bool useScissorEffectTimeline = true;
    [SerializeField] private bool playScissorEffectsPerSlash = false;
    [SerializeField, Min(0f)] private float scissorCutEffectDelayFrames = 0f;
    [SerializeField, Min(0f)] private float scissorSlashWaveEffectDelayFrames = 12f;
    [SerializeField, Min(0f)] private float scissorEndEffectDelayFrames = 22f;
    [SerializeField, Min(1f)] private float assumedEffectFrameRate = 60f;
    [SerializeField] private int scissorCutEffectSortingOrder = 60;
    [SerializeField] private int scissorSlashWaveEffectSortingOrder = 55;
    [SerializeField] private int scissorEndEffectSortingOrder = 65;
    [SerializeField] private bool playEndEffectPerSlash = false;
    [SerializeField] private bool expandHitboxWithEffect = false;
    [SerializeField, Min(1f)] private float visualAttackRangeMultiplier = 1f;

    [Header("Q - Extra Crit")]
    [SerializeField, Range(0f, 1f)] private float quickShearExtraCritChance = 0.30f;
    [SerializeField, Min(1f)] private float quickShearExtraCritMultiplier = 2.0f;
    [SerializeField, Range(0f, 1f)] private float quickShearSuperCritChance = 0.05f;
    [SerializeField, Min(1f)] private float quickShearSuperCritMultiplier = 5.0f;
    [SerializeField] private bool debugQuickShearCritLog = false;
    [Header("Q - Movement Lock")]
    [SerializeField] private float qMovementLockDuration = -1f;
    [SerializeField, Min(0f)] private float qMovementLockFallbackBuffer = 0.05f;
    [Header("Q - Crit Flash")]
    [SerializeField] private GameObject quickShearCritFlashEffectPrefab;
    [SerializeField] private GameObject quickShearSuperCritFlashEffectPrefab;
    [SerializeField] private Vector3 quickShearCritFlashOffset = new Vector3(0.9f, 0.25f, 0f);
    [SerializeField] private Vector3 quickShearCritFlashScale = Vector3.one;
    [SerializeField, Min(0.01f)] private float quickShearCritFlashLifetime = 0.12f;
    [SerializeField] private int quickShearCritFlashSortingOrder = 80;
    [SerializeField] private Color quickShearCritFlashColor = Color.white;
    [SerializeField] private Color quickShearSuperCritFlashColor = new Color(1f, 0.78f, 0.15f, 1f);

    private readonly System.Collections.Generic.HashSet<CombatHealth> castDamagedCombatTargets = new System.Collections.Generic.HashSet<CombatHealth>();
    private static readonly System.Collections.Generic.HashSet<string> MissingQuickShearStatsWarnings = new System.Collections.Generic.HashSet<string>();
    private float qLifestealTotalThisCast;
    private Coroutine activeScissorTimelineCoroutine;
    private Coroutine qMovementLockRoutine;
    private RuneRuntimeState runeRuntimeState;
    private int currentRuneCastId = -1;
    private int qMovementLockToken;
    private float qMovementLockEndTime;
    // Night Child state is independent from day/night phase.
    private bool nightChildStateActiveThisCast;

    private void Reset()
    {
        cooldown = 0.7f;
        duration = 0.42f;
        effectPower = 1.2f;
        animationName = "AKT2";
        debugLog = false;
        slashCount = 3;
        slashInterval = 0.15f;
        physicalBaseDamage = 30f;
        specialBaseDamage = 20f;
        physicalScaling = 0.2f;
        specialScaling = 0.6f;
        quickShearPerSlashDamageMultiplier = 1f;
        quickShearBonusDamage = 0f;
        quickShearFinalDamageMultiplier = 1f;
        quickShearVeilBarrierDamageMultiplier = 1f;
        qRange = 2f;
        enemyLayer = ~0;
        manaCost = 10f;
        quickShearLifeStealRatio = 0.5f;
        nightBuffLifeStealMultiplier = 1.5f;
        nightBuffSlowMoveSpeedMultiplier = 0.8f;
        nightBuffSlowDuration = 1.5f;
        scissorCutEffectOffset = new Vector3(0.9f, 0.15f, 0f);
        scissorSlashWaveEffectOffset = new Vector3(1.35f, 0.15f, 0f);
        scissorEndEffectOffset = new Vector3(0.95f, 0.15f, 0f);
        scissorCutEffectScale = new Vector3(1.2f, 1.2f, 1f);
        scissorSlashWaveEffectScale = new Vector3(1.5f, 1.5f, 1f);
        scissorEndEffectScale = new Vector3(1.2f, 1.2f, 1f);
        scissorCutEffectLifetime = 0.18f;
        scissorSlashWaveEffectLifetime = 0.20f;
        scissorEndEffectLifetime = 0.18f;
        useScissorEffectTimeline = true;
        playScissorEffectsPerSlash = false;
        scissorCutEffectDelayFrames = 0f;
        scissorSlashWaveEffectDelayFrames = 12f;
        scissorEndEffectDelayFrames = 22f;
        assumedEffectFrameRate = 60f;
        scissorCutEffectSortingOrder = 60;
        scissorSlashWaveEffectSortingOrder = 55;
        scissorEndEffectSortingOrder = 65;
        playEndEffectPerSlash = false;
        expandHitboxWithEffect = false;
        visualAttackRangeMultiplier = 1f;
        quickShearExtraCritChance = 0.30f;
        quickShearExtraCritMultiplier = 2f;
        quickShearSuperCritChance = 0.05f;
        quickShearSuperCritMultiplier = 5f;
        debugQuickShearCritLog = false;
        qMovementLockDuration = -1f;
        qMovementLockFallbackBuffer = 0.05f;
        quickShearCritFlashOffset = new Vector3(0.9f, 0.25f, 0f);
        quickShearCritFlashScale = Vector3.one;
        quickShearCritFlashLifetime = 0.12f;
        quickShearCritFlashSortingOrder = 80;
        quickShearCritFlashColor = Color.white;
        quickShearSuperCritFlashColor = new Color(1f, 0.78f, 0.15f, 1f);
    }

    private void OnValidate()
    {
        if (animationName == "ATK2")
        {
            animationName = "AKT2";
        }

        SyncQuickShearSkillConfig();
    }

    private void Awake()
    {
        if (animationName == "ATK2")
        {
            animationName = "AKT2";
        }

        runeRuntimeState = ResolveRuneRuntimeState();
        SyncQuickShearSkillConfig();
    }

    private void OnDisable()
    {
        StopActiveScissorTimeline();
        StopQMovementLockRoutine();
    }

    private void OnDestroy()
    {
        StopActiveScissorTimeline();
        StopQMovementLockRoutine();
    }

    public override void Initialize(Player01SkillController controller)
    {
        base.Initialize(controller);
        SyncQuickShearSkillConfig();
    }

    public bool TryCastAsRuneCounter(Transform attacker, bool suppressRuneCounterRecursion = true)
    {
        RuneRuntimeState runeRuntimeState = GetComponent<RuneRuntimeState>();
        bool debugThornCounter = runeRuntimeState != null && runeRuntimeState.IsThornCounterDebugEnabled();

        if (Controller == null || castRoutine != null)
        {
            if (debugThornCounter)
            {
                string reason = Controller == null ? "Controller is null" : "castRoutine already running";
                Debug.Log($"[Rune][ThornCounter] Player01 Q rune counter rejected: {reason}.", this);
            }

            return false;
        }

        if (!Controller.TryBeginSkill(this))
        {
            if (debugThornCounter)
            {
                Debug.Log("[Rune][ThornCounter] Player01 Q rune counter rejected: Controller.TryBeginSkill returned false.", this);
            }

            return false;
        }

        if (attacker != null)
        {
            Controller.FaceTowardsTarget(attacker);
        }

        castFinished = false;
        OnCastStarted();
        StartManagedCast(CastRoutine());
        if (debugThornCounter)
        {
            Debug.Log($"[Rune][ThornCounter] Player01 Q rune counter started successfully. attacker={(attacker != null ? attacker.name : "<null>")}", this);
        }

        return true;
    }

    protected override void OnCastStarted()
    {
        castDamagedCombatTargets.Clear();
        qLifestealTotalThisCast = 0f;
        StopActiveScissorTimeline();
        BeginMovementLock(ResolveInitialMovementLockDuration(), ResolveInitialMovementLockSource());
        Debug.Log($"[Player01 Q Lock] Q Start, controller={Controller}, movementInputLocked={(Controller != null && Controller.IsMovementInputLocked())}", this);

        if (debugLog)
        {
            Debug.Log($"[Q - QuickShear] Start. animation={animationName}, slashes={slashCount}, interval={slashInterval:F2}", this);
        }

        SyncQuickShearSkillConfig();
        runeRuntimeState = ResolvePlayerRuneRuntimeState();
        currentRuneCastId = CurrentRuneCastId;
        nightChildStateActiveThisCast = DayNightAffinityDamageModifier.HasNightChildState(Controller != null ? Controller.gameObject : gameObject);

        if (Controller != null && Controller.IsVeilBarrierActive())
        {
            Debug.Log("[Player01 Q] cast while W active", this);
        }

        if (debugQuickShearCritLog)
        {
            Debug.Log(
                $"[QuickShear Config] manaCost={manaCost:F2}, inspectorCooldown={cooldown:F2}, runtimeCooldown={ResolveRuntimeCooldownSeconds():F2}",
                this);
        }
    }

    protected override IEnumerator CastRoutine()
    {
        int count = Mathf.Max(1, slashCount);
        float interval = Mathf.Max(0f, slashInterval);
        float totalDuration = Mathf.Max(0f, duration);
        float lockDuration = Mathf.Max(totalDuration, interval * Mathf.Max(0, count - 1), 0.55f);

        for (int i = 0; i < count; i++)
        {
            PlaySlash(i + 1, count, lockDuration);

            if (i < count - 1)
            {
                if (interval > 0f)
                {
                    yield return new WaitForSeconds(interval);
                }
                else
                {
                    yield return null;
                }
            }
        }

        float remaining = totalDuration - Mathf.Max(0f, interval * (count - 1));
        if (remaining > 0f)
        {
            yield return new WaitForSeconds(remaining);
        }

        CompleteCast();
    }

    private void PlaySlash(int slashIndex, int slashTotal, float lockDuration)
    {
        if (Controller == null)
        {
            if (debugLog)
            {
                Debug.LogWarning($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} skipped because Controller is null.", this);
            }

            return;
        }

        SkeletonAnimation spine = Controller.GetComponentInChildren<SkeletonAnimation>(true);
        if (spine == null)
        {
            if (debugLog)
            {
                Debug.LogWarning($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} skipped because SkeletonAnimation is missing.", this);
            }

            return;
        }

        if (debugLog)
        {
            Debug.Log($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} try play {animationName} on {spine.name}.", this);
            Debug.Log($"[Q - QuickShear] Target: {Controller.GetSkeletonAnimationDebugSummary()}", this);
        }

        if (debugLog)
        {
            Debug.Log($"[Q - QuickShear] TryPlayLockedSkillAnimation -> {animationName}.", this);
        }

        TrackEntry entry = Controller.PlayLockedSkillAnimationEntry(animationName, false, lockDuration, true, "Q");
        if (entry == null)
        {
            if (debugLog)
            {
                Debug.LogWarning($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} failed to play '{animationName}'.", this);
            }

            return;
        }

        if (debugLog)
        {
            Debug.Log($"[Q - QuickShear] Slash {slashIndex}/{slashTotal} requested '{animationName}' via shared controller entry.", this);
            Debug.Log(
                $"[Q - QuickShear] damageFormula=((PBase {physicalBaseDamage:F2} + SATK*{specialScaling:F2}) + (SBase {specialBaseDamage:F2} + PATK*{physicalScaling:F2}) + Bonus {quickShearBonusDamage:F2}) * Slash {quickShearPerSlashDamageMultiplier:F2} * Final {quickShearFinalDamageMultiplier:F2}, range={qRange:F2}",
                this);
        }

        ExtendMovementLock(ResolveMovementLockDuration(entry, slashIndex, slashTotal), ResolveMovementLockSource(entry));

        if (playScissorEffectsPerSlash || slashIndex == 1)
        {
            PlayScissorAttackEffects();
        }

        float dealtDamage = ApplySlashDamage();
        if (dealtDamage > 0f)
        {
            ApplyQLifeSteal(dealtDamage);
        }

        Controller.StartCoroutine(LogTrackNextFrame(slashIndex, slashTotal));
    }

    private float ApplySlashDamage()
    {
        Vector3 origin = transform.position;
        Vector3 facing = Controller != null ? Controller.GetFacingWorldDirection() : transform.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude < 0.0001f)
        {
            facing = transform.forward;
            facing.y = 0f;
        }

        if (facing.sqrMagnitude < 0.0001f)
        {
            facing = Vector3.forward;
        }

        facing.Normalize();
        Vector3 center = hitPoint != null ? hitPoint.position : origin + facing * (Mathf.Max(0.1f, qRange) * 0.5f);
        QuickShearDamageResult damageResult = ResolveDamage();
        float finalDamage = damageResult.finalDamage;
        Collider[] hits = Physics.OverlapSphere(center, Mathf.Max(0.1f, qRange), enemyLayer, QueryTriggerInteraction.Collide);
        float totalDamageDealt = 0f;

        foreach (Collider hit in hits)
        {
            if (!BattleTargetUtility.IsMonster(hit, transform))
            {
                continue;
            }

            if (!IsInFrontSlashArea(hit, origin, facing))
            {
                continue;
            }

            CombatHealth combatHealth = BattleTargetUtility.GetMonsterCombatHealth(hit, transform);
            if (combatHealth != null && castDamagedCombatTargets.Add(combatHealth))
            {
                float resolvedDamage = finalDamage + ConsumeRuneFirstHitBonusDamage();
                resolvedDamage *= PlayerSkillDamageTakenDebuffReceiver.ResolvePlayer01SkillDamageMultiplier(combatHealth.gameObject);
                PlayerTimedSkillDamageBoostStatus timedSkillDamageBoost = PlayerTimedSkillDamageBoostStatus.Resolve(Controller != null ? Controller.gameObject : gameObject);
                if (timedSkillDamageBoost != null)
                {
                    resolvedDamage *= timedSkillDamageBoost.Multiplier;
                }

                float beforeHealth = ResolveCurrentHealth(combatHealth);
                combatHealth.TakeDamage(new BattleDamage(resolvedDamage, BattleDamageType.Physical, gameObject, damageResult.isAnyCritical));
                float afterHealth = ResolveCurrentHealth(combatHealth);
                float actualDamage = Mathf.Max(0f, beforeHealth - afterHealth);
                runeRuntimeState?.NotifyMonsterDamagedBySkill(SkillIndex, combatHealth, actualDamage);
                if (actualDamage > 0f)
                {
                    ApplyNightBuffSlow(combatHealth);
                    TryPlayQuickShearCritFlash(hit, damageResult);
                }
                totalDamageDealt += actualDamage;
                continue;
            }
        }

        return totalDamageDealt;
    }

    public void PlayScissorAttackEffects()
    {
        float facingSign = ResolveFacingSign();

        if (useScissorEffectTimeline)
        {
            StopActiveScissorTimeline();
            activeScissorTimelineCoroutine = StartCoroutine(PlayScissorEffectTimeline(facingSign));
            return;
        }

        PlayScissorCutEffect(facingSign);
        StartCoroutine(PlayScissorFollowupEffects(facingSign));
    }

    public void PlayScissorCutEffect()
    {
        PlayScissorCutEffect(ResolveFacingSign());
    }

    public void PlayScissorCutEffect(float facingSign)
    {
        SpawnScissorEffect(
            scissorCutEffectPrefab,
            scissorCutEffectOffset,
            scissorCutEffectScale,
            scissorCutEffectLifetime,
            scissorCutEffectSortingOrder,
            facingSign);
    }

    public void PlayScissorSlashWaveEffect()
    {
        PlayScissorSlashWaveEffect(ResolveFacingSign());
    }

    public void PlayScissorSlashWaveEffect(float facingSign)
    {
        SpawnScissorEffect(
            scissorSlashWaveEffectPrefab,
            scissorSlashWaveEffectOffset,
            scissorSlashWaveEffectScale,
            scissorSlashWaveEffectLifetime,
            scissorSlashWaveEffectSortingOrder,
            facingSign);
    }

    public void PlayScissorEndEffect()
    {
        PlayScissorEndEffect(ResolveFacingSign());
    }

    public void PlayScissorEndEffect(float facingSign)
    {
        SpawnScissorEffect(
            scissorEndEffectPrefab,
            scissorEndEffectOffset,
            scissorEndEffectScale,
            scissorEndEffectLifetime,
            scissorEndEffectSortingOrder,
            facingSign);
    }

    private IEnumerator PlayScissorFollowupEffects(float facingSign)
    {
        float secondDelaySeconds = Mathf.Max(0f, scissorSlashWaveEffectDelayFrames) / Mathf.Max(1f, assumedEffectFrameRate);
        if (secondDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(secondDelaySeconds);
        }

        if (this == null || gameObject == null || !isActiveAndEnabled)
        {
            yield break;
        }

        PlayScissorSlashWaveEffect(facingSign);

        if (!playEndEffectPerSlash || scissorEndEffectPrefab == null)
        {
            yield break;
        }

        float endDelaySeconds = Mathf.Max(0f, scissorEndEffectDelayFrames) / Mathf.Max(1f, assumedEffectFrameRate);
        float extraDelaySeconds = Mathf.Max(0f, endDelaySeconds - secondDelaySeconds);
        if (extraDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(extraDelaySeconds);
        }

        if (this == null || gameObject == null || !isActiveAndEnabled)
        {
            yield break;
        }

        PlayScissorEndEffect(facingSign);
    }

    private IEnumerator PlayScissorEffectTimeline(float facingSign)
    {
        float cutDelaySeconds = Mathf.Max(0f, scissorCutEffectDelayFrames) / Mathf.Max(1f, assumedEffectFrameRate);
        float slashDelaySeconds = Mathf.Max(0f, scissorSlashWaveEffectDelayFrames) / Mathf.Max(1f, assumedEffectFrameRate);
        float endDelaySeconds = Mathf.Max(0f, scissorEndEffectDelayFrames) / Mathf.Max(1f, assumedEffectFrameRate);

        if (cutDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(cutDelaySeconds);
        }

        if (!this || !gameObject || !isActiveAndEnabled)
        {
            activeScissorTimelineCoroutine = null;
            yield break;
        }

        PlayScissorCutEffect(facingSign);

        float cutToSlashDelay = Mathf.Max(0f, slashDelaySeconds - cutDelaySeconds);
        if (cutToSlashDelay > 0f)
        {
            yield return new WaitForSeconds(cutToSlashDelay);
        }

        if (!this || !gameObject || !isActiveAndEnabled)
        {
            activeScissorTimelineCoroutine = null;
            yield break;
        }

        PlayScissorSlashWaveEffect(facingSign);

        if (scissorEndEffectPrefab != null)
        {
            float slashToEndDelay = Mathf.Max(0f, endDelaySeconds - slashDelaySeconds);
            if (slashToEndDelay > 0f)
            {
                yield return new WaitForSeconds(slashToEndDelay);
            }

            if (!this || !gameObject || !isActiveAndEnabled)
            {
                activeScissorTimelineCoroutine = null;
                yield break;
            }

            PlayScissorEndEffect(facingSign);
        }

        activeScissorTimelineCoroutine = null;
    }

    private void SpawnScissorEffect(
        GameObject effectPrefab,
        Vector3 localOffset,
        Vector3 localScale,
        float lifetime,
        int sortingOrder,
        float facingSign)
    {
        if (effectPrefab == null)
        {
            return;
        }

        Vector3 spawnOffset = new Vector3(localOffset.x * facingSign, localOffset.y, localOffset.z);
        Vector3 spawnPosition = transform.position + spawnOffset;
        GameObject instance = Instantiate(effectPrefab, spawnPosition, Quaternion.identity);

        Vector3 finalScale = localScale;
        finalScale.x = Mathf.Abs(finalScale.x) * facingSign;
        instance.transform.localScale = finalScale;

        ScissorFrameEffectPlayer effectPlayer = instance.GetComponent<ScissorFrameEffectPlayer>();
        if (effectPlayer != null)
        {
            effectPlayer.SetLifetime(lifetime);
            effectPlayer.SetSortingOrder(sortingOrder);
            effectPlayer.Play();
        }
        else
        {
            SpriteRenderer[] renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingOrder = sortingOrder;
            }
        }
    }

    private float ResolveFacingSign()
    {
        if (Controller == null)
        {
            return transform.localScale.x < 0f ? -1f : 1f;
        }

        Vector3 facing = Controller.GetFacingWorldDirection();
        if (Mathf.Abs(facing.x) > 0.001f)
        {
            return facing.x >= 0f ? 1f : -1f;
        }

        float mirrorScaleX = Controller.GetFacingMirrorScaleX();
        return mirrorScaleX < 0f ? 1f : -1f;
    }

    private void ApplyQLifeSteal(float damageDealt)
    {
        float lifeStealRatio = Mathf.Max(0f, quickShearLifeStealRatio);
        if (nightChildStateActiveThisCast)
        {
            lifeStealRatio *= Mathf.Max(1f, nightBuffLifeStealMultiplier);
        }

        float healAmount = Mathf.Max(0f, damageDealt) * lifeStealRatio;
        if (healAmount <= 0f)
        {
            return;
        }

        float beforeHealth = ResolvePlayerCurrentHealth();
        if (!TryHealPlayer(healAmount))
        {
            return;
        }

        float afterHealth = ResolvePlayerCurrentHealth();
        float actualHeal = Mathf.Max(0f, afterHealth - beforeHealth);
        if (actualHeal <= 0f)
        {
            return;
        }

        qLifestealTotalThisCast += actualHeal;
        if (debugQuickShearCritLog)
        {
            Debug.Log($"[Player01 Q Lifesteal] damage={damageDealt:F2} heal={actualHeal:F2}", this);
        }
    }

    private void ApplyNightBuffSlow(CombatHealth combatHealth)
    {
        if (!nightChildStateActiveThisCast || combatHealth == null)
        {
            return;
        }

        TimedEnemyMoveSpeedDebuff.ApplyOrRefresh(
            ResolveNightBuffSlowTarget(combatHealth),
            $"{nameof(Player1Skill_Q_QuickShear)}_{GetInstanceID()}",
            Mathf.Clamp01(nightBuffSlowMoveSpeedMultiplier),
            Mathf.Max(0f, nightBuffSlowDuration));
    }

    private static GameObject ResolveNightBuffSlowTarget(CombatHealth combatHealth)
    {
        if (combatHealth == null)
        {
            return null;
        }

        EnemyController enemyController = combatHealth.GetComponent<EnemyController>();
        if (enemyController == null)
        {
            enemyController = combatHealth.GetComponentInChildren<EnemyController>(true);
        }

        return enemyController != null ? enemyController.gameObject : combatHealth.gameObject;
    }

    private float ResolveCurrentHealth(CombatHealth combatHealth)
    {
        if (combatHealth == null)
        {
            return 0f;
        }

        if (combatHealth.resourceBank != null)
        {
            return combatHealth.resourceBank.currentHealth;
        }

        return combatHealth.currentHealth;
    }

    private float ResolvePlayerCurrentHealth()
    {
        CombatHealth combatHealth = ResolvePlayerCombatHealth();
        if (combatHealth != null)
        {
            return ResolveCurrentHealth(combatHealth);
        }

        BattleResourceBank bank = ResolvePlayerResourceBank();
        return bank != null ? bank.currentHealth : 0f;
    }

    private bool TryHealPlayer(float amount)
    {
        if (amount <= 0f)
        {
            return false;
        }

        CombatHealth combatHealth = ResolvePlayerCombatHealth();
        if (combatHealth != null)
        {
            float maxHealth = ResolvePlayerMaxHealth(combatHealth);
            if (maxHealth > 0f)
            {
                float before = ResolveCurrentHealth(combatHealth);
                float healed = Mathf.Min(amount, Mathf.Max(0f, maxHealth - before));
                if (healed > 0f)
                {
                    SetPlayerCurrentHealth(combatHealth, before + healed);
                }
            }
            return true;
        }

        BattleResourceBank bank = ResolvePlayerResourceBank();
        if (bank != null)
        {
            float before = Mathf.Max(0f, bank.currentHealth);
            float healed = Mathf.Min(amount, Mathf.Max(0f, bank.maxHealth - before));
            if (healed > 0f)
            {
                bank.currentHealth = before + healed;
            }
            return true;
        }

        return false;
    }

    private CombatHealth ResolvePlayerCombatHealth()
    {
        CombatHealth combatHealth = GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            return combatHealth;
        }

        if (Controller != null)
        {
            combatHealth = Controller.GetComponent<CombatHealth>();
            if (combatHealth != null)
            {
                return combatHealth;
            }
        }

        return GetComponentInParent<CombatHealth>();
    }

    private BattleResourceBank ResolvePlayerResourceBank()
    {
        BattleResourceBank bank = GetComponent<BattleResourceBank>();
        if (bank != null)
        {
            return bank;
        }

        if (Controller != null)
        {
            bank = Controller.GetComponent<BattleResourceBank>();
            if (bank != null)
            {
                return bank;
            }
        }

        return GetComponentInParent<BattleResourceBank>();
    }

    private float ConsumeRuneFirstHitBonusDamage()
    {
        runeRuntimeState = runeRuntimeState != null ? runeRuntimeState : ResolveRuneRuntimeState();
        return runeRuntimeState != null ? runeRuntimeState.ConsumeFirstHitBonusDamage(SkillIndex, currentRuneCastId) : 0f;
    }

    private RuneRuntimeState ResolveRuneRuntimeState()
    {
        RuneRuntimeState runtimeState = GetComponent<RuneRuntimeState>();
        if (runtimeState != null)
        {
            return runtimeState;
        }

        if (Controller != null)
        {
            runtimeState = Controller.GetComponent<RuneRuntimeState>();
            if (runtimeState != null)
            {
                return runtimeState;
            }
        }

        return GetComponentInParent<RuneRuntimeState>();
    }

    private float ResolvePlayerMaxHealth(CombatHealth combatHealth)
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

        return Mathf.Max(0f, combatHealth.currentHealth);
    }

    private void SetPlayerCurrentHealth(CombatHealth combatHealth, float healthValue)
    {
        if (combatHealth == null)
        {
            return;
        }

        if (combatHealth.resourceBank != null)
        {
            float clamped = Mathf.Clamp(healthValue, 0f, combatHealth.resourceBank.maxHealth);
            combatHealth.resourceBank.currentHealth = clamped;
            combatHealth.currentHealth = clamped;
            return;
        }

        combatHealth.currentHealth = Mathf.Clamp(healthValue, 0f, ResolvePlayerMaxHealth(combatHealth));
    }

    private bool IsInFrontSlashArea(Collider hit, Vector3 origin, Vector3 facing)
    {
        Vector3 targetPoint = hit != null ? hit.bounds.center : origin;
        Vector3 toTarget = targetPoint - origin;
        toTarget.y = 0f;

        float range = Mathf.Max(0.1f, qRange);
        if (toTarget.sqrMagnitude > range * range)
        {
            return false;
        }

        if (toTarget.sqrMagnitude < 0.0001f)
        {
            return true;
        }

        return Vector3.Dot(toTarget.normalized, facing) >= 0.15f;
    }

    private QuickShearDamageResult ResolveDamage()
    {
        float rawDamage = Mathf.Max(0f, physicalBaseDamage) + Mathf.Max(0f, specialBaseDamage);
        CombatStats stats = gameObject.GetComponent<CombatStats>();
        if (stats == null)
        {
            stats = gameObject.GetComponentInParent<CombatStats>();
        }

        float physicalAttackValue = 0f;
        float specialAttackValue = 0f;
        if (stats != null)
        {
            physicalAttackValue = Mathf.Max(0f, stats.physicalAttack);
            specialAttackValue = Mathf.Max(0f, stats.specialAttack);
            rawDamage += specialAttackValue * Mathf.Max(0f, specialScaling);
            rawDamage += physicalAttackValue * Mathf.Max(0f, physicalScaling);
        }
        else
        {
            WarnMissingCombatStatsOnce();
        }

        rawDamage += Mathf.Max(0f, quickShearBonusDamage);
        rawDamage *= Mathf.Max(0f, quickShearPerSlashDamageMultiplier);

        if (Controller != null && Controller.IsVeilBarrierActive())
        {
            rawDamage *= Mathf.Max(0f, quickShearVeilBarrierDamageMultiplier);
        }

        BattleResourceBank bank = gameObject.GetComponent<BattleResourceBank>();
        if (bank == null)
        {
            bank = gameObject.GetComponentInParent<BattleResourceBank>();
        }

        if (bank != null)
        {
            rawDamage *= bank.SkillDamageMultiplier;
        }

        rawDamage *= ResolveRuneOutgoingDamageMultiplier();

        rawDamage *= Mathf.Max(0f, quickShearFinalDamageMultiplier);
        float manaRuneMultiplier = ResolveManaRuneScaledMultiplier(0.5f);
        if (manaRuneMultiplier > 1f)
        {
            float beforeManaRune = rawDamage;
            rawDamage *= manaRuneMultiplier;
            LogManaRuneApplied("Q", "Damage", beforeManaRune, rawDamage);
        }

        float afterBaseCrit = BattleStatUtility.ApplyCriticalDamage(gameObject, rawDamage, out bool baseCritTriggered);
        bool extraCritTriggered = Random.value < Mathf.Clamp01(quickShearExtraCritChance);
        bool superCritTriggered = Random.value < Mathf.Clamp01(quickShearSuperCritChance);

        float normalCritDamage = afterBaseCrit;
        float finalDamage = normalCritDamage;
        float qCritMultiplier = 1f;
        string qCritMode = "None";

        if (superCritTriggered)
        {
            qCritMultiplier = Mathf.Max(1f, quickShearSuperCritMultiplier);
            finalDamage = normalCritDamage * qCritMultiplier;
            qCritMode = $"QSuperCrit x{qCritMultiplier:F2}";
        }
        else if (extraCritTriggered)
        {
            qCritMultiplier = Mathf.Max(1f, quickShearExtraCritMultiplier);
            finalDamage = normalCritDamage * qCritMultiplier;
            qCritMode = $"QExtraCrit x{qCritMultiplier:F2}";
        }

        if (debugQuickShearCritLog)
        {
            float baseChainMultiplier = rawDamage > 0f ? normalCritDamage / rawDamage : 1f;
            float totalMultiplier = rawDamage > 0f ? finalDamage / rawDamage : 1f;
            Debug.Log(
                $"[QuickShear Crit] manaCost={manaCost:F2}, cooldown={ResolveRuntimeCooldownSeconds():F2}, PATK={physicalAttackValue:F2}, SATK={specialAttackValue:F2}, " +
                $"Formula=(({physicalBaseDamage:F2} + SATK*{specialScaling:F2}) + ({specialBaseDamage:F2} + PATK*{physicalScaling:F2}) + {quickShearBonusDamage:F2}) * Slash{quickShearPerSlashDamageMultiplier:F2} * Veil{(Controller != null && Controller.IsVeilBarrierActive() ? quickShearVeilBarrierDamageMultiplier : 1f):F2} * Final{quickShearFinalDamageMultiplier:F2} => Raw={rawDamage:F2}, NormalCritDamage={normalCritDamage:F2}, Final={finalDamage:F2}, " +
                $"BaseCrit={baseCritTriggered}, ExtraCrit={extraCritTriggered}, SuperCrit={superCritTriggered}, " +
                $"BaseChainMultiplier={baseChainMultiplier:F2}, QCritMode={qCritMode}, QuickShearMultiplier={qCritMultiplier:F2}, TotalMultiplier={totalMultiplier:F2}",
                this);
        }

        return new QuickShearDamageResult(
            finalDamage,
            baseCritTriggered,
            extraCritTriggered,
            superCritTriggered);
    }

    private readonly struct QuickShearDamageResult
    {
        public readonly float finalDamage;
        public readonly bool baseCritTriggered;
        public readonly bool extraCritTriggered;
        public readonly bool superCritTriggered;

        public bool isAnyCritical => baseCritTriggered || extraCritTriggered || superCritTriggered;

        public QuickShearDamageResult(
            float finalDamage,
            bool baseCritTriggered,
            bool extraCritTriggered,
            bool superCritTriggered)
        {
            this.finalDamage = finalDamage;
            this.baseCritTriggered = baseCritTriggered;
            this.extraCritTriggered = extraCritTriggered;
            this.superCritTriggered = superCritTriggered;
        }
    }

    private void WarnMissingCombatStatsOnce()
    {
        string ownerName = gameObject != null ? gameObject.name : "<null owner>";
        if (!MissingQuickShearStatsWarnings.Add(ownerName))
        {
            return;
        }

        Debug.LogWarning($"[Player01 Q] CombatStats not found on '{ownerName}', using QuickShear base formula fallback.", this);
    }

    private void SyncQuickShearSkillConfig()
    {
        float resolvedCooldown = Mathf.Max(0f, cooldown);
        float resolvedManaCost = Mathf.Max(0f, manaCost);

        if (SkillResource != null && SkillIndex >= 0 && SkillResource.skillDatas != null && SkillResource.skillDatas.Length > SkillIndex)
        {
            SkillCostCDData qData = SkillResource.skillDatas[SkillIndex];
            qData.maxCooldown = resolvedCooldown;
            qData.manaCost = resolvedManaCost;
            SkillResource.skillDatas[SkillIndex] = qData;
        }

        if (Controller != null)
        {
            FieldInfo qCooldownField = typeof(Player01SkillController).GetField("qCooldown", BindingFlags.Instance | BindingFlags.NonPublic);
            if (qCooldownField != null)
            {
                qCooldownField.SetValue(Controller, resolvedCooldown);
            }
        }
    }

    private float ResolveRuntimeCooldownSeconds()
    {
        if (SkillResource != null && SkillIndex >= 0)
        {
            return SkillResource.GetSkillMaxCD(SkillIndex);
        }

        return Mathf.Max(0f, cooldown);
    }

    private void TryPlayQuickShearCritFlash(Collider hit, QuickShearDamageResult damageResult)
    {
        if (!damageResult.extraCritTriggered && !damageResult.superCritTriggered)
        {
            return;
        }

        Vector3 spawnPosition = ResolveQuickShearCritFlashPosition(hit);
        float facingSign = ResolveFacingSign();
        bool useSuperCritPrefab = damageResult.superCritTriggered && quickShearSuperCritFlashEffectPrefab != null;
        GameObject selectedPrefab = useSuperCritPrefab ? quickShearSuperCritFlashEffectPrefab : quickShearCritFlashEffectPrefab;
        if (selectedPrefab == null)
        {
            return;
        }

        Color flashColor = damageResult.superCritTriggered
            ? (useSuperCritPrefab ? Color.white : quickShearSuperCritFlashColor)
            : quickShearCritFlashColor;

        GameObject instance = Instantiate(selectedPrefab, spawnPosition, Quaternion.identity);
        Vector3 finalScale = quickShearCritFlashScale;
        finalScale.x = Mathf.Abs(finalScale.x) * facingSign;
        instance.transform.localScale = finalScale;

        if (instance.TryGetComponent(out ScissorFrameEffectPlayer effectPlayer))
        {
            effectPlayer.SetLifetime(quickShearCritFlashLifetime);
            effectPlayer.SetSortingOrder(quickShearCritFlashSortingOrder);
            effectPlayer.SetDestroyOnComplete(true);
            effectPlayer.SetColor(flashColor);
            effectPlayer.Play();
        }
        else
        {
            ApplyCritFlashVisuals(instance, flashColor);
            Destroy(instance, Mathf.Max(0.01f, quickShearCritFlashLifetime));
        }

        if (debugQuickShearCritLog)
        {
            Debug.Log(
                $"[QuickShear Crit Flash] played={(instance != null)} extra={damageResult.extraCritTriggered} super={damageResult.superCritTriggered} " +
                $"prefab={(selectedPrefab != null ? selectedPrefab.name : "<null>")} " +
                $"colorMode={(damageResult.superCritTriggered ? (useSuperCritPrefab ? "OriginalGoldPrefab" : "GoldTintFallback") : "Original")} " +
                $"lifetime={quickShearCritFlashLifetime:F2} facingSign={facingSign:F0}",
                this);
        }
    }

    private Vector3 ResolveQuickShearCritFlashPosition(Collider hit)
    {
        if (hit != null)
        {
            return hit.bounds.center;
        }

        float facingSign = ResolveFacingSign();
        Vector3 fallback = hitPoint != null ? hitPoint.position : transform.position;
        return fallback + new Vector3(quickShearCritFlashOffset.x * facingSign, quickShearCritFlashOffset.y, quickShearCritFlashOffset.z);
    }

    private void ApplyCritFlashVisuals(GameObject effectInstance, Color color)
    {
        if (effectInstance == null)
        {
            return;
        }

        SpriteRenderer[] renderers = effectInstance.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sortingOrder = quickShearCritFlashSortingOrder;
            renderers[i].color = color;
        }
    }

    private void StopActiveScissorTimeline()
    {
        if (activeScissorTimelineCoroutine == null)
        {
            return;
        }

        StopCoroutine(activeScissorTimelineCoroutine);
        activeScissorTimelineCoroutine = null;
    }

    private IEnumerator LogTrackNextFrame(int slashIndex, int slashTotal)
    {
        yield return null;

        if (Controller == null)
        {
            yield break;
        }

        SkeletonAnimation spine = Controller.GetComponentInChildren<SkeletonAnimation>(true);
        if (spine == null || spine.AnimationState == null)
        {
            Debug.LogWarning($"[Q - QuickShear] Next frame track check failed after slash {slashIndex}/{slashTotal}: SkeletonAnimation missing.", this);
            yield break;
        }

        TrackEntry current = spine.AnimationState.GetCurrent(0);
        string currentName = current != null && current.Animation != null ? current.Animation.Name : "<none>";
        if (currentName == animationName)
        {
            Debug.Log($"[Q - QuickShear] Next frame Track0 is still {currentName}.", this);
        }
        else
        {
            Debug.LogWarning($"[Q - QuickShear] Next frame Track0 changed to {currentName}. currentLocomotion={Controller.GetCurrentLocomotionAnimationName()}, currentSkill={Controller.GetCurrentSkillName()}.", this);
        }
    }

    protected override string GetSkillLabel()
    {
        return "Q - QuickShear";
    }

    protected override void OnCastFinished()
    {
        Debug.Log("[Player01 Q Lock] OnCastFinished requested unlock", this);

        if (qLifestealTotalThisCast > 0f)
        {
            Debug.Log($"[Player01 Q Lifesteal] totalHeal={qLifestealTotalThisCast:F2}", this);
        }
    }

    protected override int SkillIndex => 0;

    private void BeginMovementLock(float duration, string source)
    {
        duration = Mathf.Max(0.01f, duration);
        qMovementLockToken++;
        qMovementLockEndTime = Time.time + duration;
        StopQMovementLockRoutine();
        Controller?.SetMovementInputLocked(true, "Player01 Q");
        Debug.Log($"[Player01 Q Lock] Start movement lock, duration={duration:F3}, source={source}", this);
        qMovementLockRoutine = StartCoroutine(QMovementLockRoutine(qMovementLockToken));
    }

    private void ExtendMovementLock(float duration, string source)
    {
        duration = Mathf.Max(0.01f, duration);
        float proposedEndTime = Time.time + duration;
        if (qMovementLockRoutine == null || Controller == null || !Controller.IsMovementInputLocked())
        {
            BeginMovementLock(duration, source);
            return;
        }

        if (proposedEndTime > qMovementLockEndTime)
        {
            qMovementLockEndTime = proposedEndTime;
            Debug.Log($"[Player01 Q Lock] Start movement lock, duration={duration:F3}, source={source}", this);
        }
    }

    private IEnumerator QMovementLockRoutine(int token)
    {
        while (token == qMovementLockToken && Time.time < qMovementLockEndTime)
        {
            yield return null;
        }

        if (token != qMovementLockToken)
        {
            yield break;
        }

        qMovementLockRoutine = null;
        Controller?.SetMovementInputLocked(false, "Player01 Q");
        Debug.Log($"[Player01 Q Lock] End movement lock, token={token}", this);
        Debug.Log($"[Player01 Q Lock] Q End, movementInputLocked={(Controller != null && Controller.IsMovementInputLocked())}", this);
    }

    private void StopQMovementLockRoutine()
    {
        if (qMovementLockRoutine == null)
        {
            return;
        }

        StopCoroutine(qMovementLockRoutine);
        qMovementLockRoutine = null;
    }

    private float ResolveInitialMovementLockDuration()
    {
        if (qMovementLockDuration > 0f)
        {
            return qMovementLockDuration;
        }

        int count = Mathf.Max(1, slashCount);
        float interval = Mathf.Max(0f, slashInterval);
        float fallbackDuration = Mathf.Max(0f, duration);
        return Mathf.Max(0.01f, fallbackDuration + interval * Mathf.Max(0, count - 1) + qMovementLockFallbackBuffer);
    }

    private string ResolveInitialMovementLockSource()
    {
        return qMovementLockDuration > 0f ? "Inspector" : "Fallback";
    }

    private float ResolveMovementLockDuration(TrackEntry entry, int slashIndex, int slashTotal)
    {
        if (qMovementLockDuration > 0f)
        {
            return qMovementLockDuration;
        }

        float remainingIntervals = Mathf.Max(0f, slashInterval) * Mathf.Max(0, slashTotal - slashIndex);
        float fallbackDuration = Mathf.Max(0f, duration);

        if (entry != null && entry.Animation != null)
        {
            return Mathf.Max(0.01f, entry.Animation.Duration + remainingIntervals + qMovementLockFallbackBuffer);
        }

        return Mathf.Max(0.01f, fallbackDuration + remainingIntervals + qMovementLockFallbackBuffer);
    }

    private string ResolveMovementLockSource(TrackEntry entry)
    {
        if (qMovementLockDuration > 0f)
        {
            return "Inspector";
        }

        return entry != null && entry.Animation != null ? "Animation" : "Fallback";
    }
}
