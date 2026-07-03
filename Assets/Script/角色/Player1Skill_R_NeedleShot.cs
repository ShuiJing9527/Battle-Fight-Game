using System.Collections;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Serialization;

public class Player1Skill_R_NeedleShot : Player01SkillBase
{
    [Header("R - Resources")]
    [Tooltip("Base mana cost before any future mana cost modifiers are applied.")]
    [SerializeField, Min(0f)] private float baseManaCost = 60f;

    [Header("R - Animation")]
    [SerializeField] private bool useSpineAnimationEvents;
    [SerializeField] private string thrustVfxEventName = "R_ThrustVfx";
    [SerializeField] private string thrustHitEventName = "R_ThrustHit";
    [SerializeField] private string needleStartEventName = "R_Needles";

    [Header("R - Timing")]
    [SerializeField, Min(0f)] private float thrustVfxStartTime = 0.48f;
    [SerializeField, Min(0f)] private float thrustHitTime = 0.58f;
    [SerializeField, Min(0f)] private float needleStartTime = 0.82f;
    [SerializeField, Min(0f)] private float needleInterval = 0.12f;
    [SerializeField, Min(0f)] private float skillEndTime = 1.25f;

    [Header("R - Thrust VFX")]
    [SerializeField] private Player01RThrustVfx thrustVfxPrefab;
    [SerializeField] private Transform thrustVfxAnchor;

    [Header("R - Thrust Damage")]
    [SerializeField, Min(0f)] private float thrustPhysicalBaseDamage = 60f;
    [SerializeField, Min(0f)] private float thrustSpecialToPhysicalScale = 0.80f;
    [SerializeField, Min(0f)] private float thrustSpecialBaseDamage = 40f;
    [SerializeField, Min(0f)] private float thrustPhysicalToSpecialScale = 0.40f;
    [SerializeField, Min(0.1f)] private float thrustRange = 2.4f;
    [SerializeField, Min(0.1f)] private float thrustWidth = 1.2f;
    [SerializeField, Min(0.1f)] private float thrustHeight = 1.4f;
    [SerializeField] private float thrustForwardOffset = 1.2f;
    [FormerlySerializedAs("enemyLayer")]
    [SerializeField] private LayerMask thrustHitLayers = ~0;
    [SerializeField] private bool debugDrawThrustHitbox;

    [Header("R - Needle Setup")]
    [FormerlySerializedAs("needlePrefab")]
    [SerializeField] private GameObject needlePrefab;
    [FormerlySerializedAs("needleCount")]
    [SerializeField, Min(1)] private int minNeedleCount = 4;
    [SerializeField, Min(1)] private int maxNeedleCount = 12;
    [SerializeField, Min(0.1f)] private float spawnRadiusMin = 3.5f;
    [SerializeField, Min(0.1f)] private float spawnRadiusMax = 5.5f;
    [SerializeField] private float heightMin = 0.5f;
    [SerializeField] private float heightMax = 2.2f;
    [SerializeField] private float targetHeightOffset = 0.8f;
    [SerializeField, Range(0f, 60f)] private float horizontalRandomAngle = 18f;
    [SerializeField] private bool enforceNeedleMinVisibleAngle = true;
    [SerializeField, Range(0f, 45f)] private float minVisibleAngle = 12f;
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int randomSeed = 101;

    [Header("R - Needle Damage")]
    [FormerlySerializedAs("baseDamage")]
    [FormerlySerializedAs("needleDamage")]
    [SerializeField, Min(0f)] private float needleDamageMultiplier = 0.50f;
    [FormerlySerializedAs("needleSpeed")]
    [SerializeField, Min(0.01f)] private float travelSpeed = 38f;
    [SerializeField, Min(0f)] private float passThroughDistance = 4.5f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.3f;
    [SerializeField] private LayerMask needleHitLayers = ~0;
    [SerializeField, Min(0.01f)] private float needleHitRadius = 0.3f;

    [Header("R - Recovery")]
    [FormerlySerializedAs("needleHealPercentOfDamage")]
    [SerializeField, Range(0f, 1f)] private float healFromDamagePercent = 0.50f;

    [Header("R - Cooldown On Kill")]
    [SerializeField, Min(0f)] private float cooldownReductionPerKill = 1.0f;

    [Header("R - Targeting")]
    [SerializeField, Min(0.5f)] private float targetSearchRadius = 14f;
    [SerializeField] private LayerMask targetSearchLayers = ~0;
    [SerializeField, Min(0.5f)] private float fallbackTargetDistance = 6f;
    [SerializeField] private Player01EyeFireROffset eyeFireROffset;

    private readonly HashSet<CombatHealth> thrustDamagedTargets = new HashSet<CombatHealth>();
    private readonly HashSet<CombatHealth> killCreditTargets = new HashSet<CombatHealth>();
    private readonly List<CombatHealth> thrustPhaseTargets = new List<CombatHealth>();
    private readonly List<Player01REnergyNeedle> activeNeedles = new List<Player01REnergyNeedle>();
    private readonly List<GameObject> runtimeVfxInstances = new List<GameObject>();

    private RuneRuntimeState runeRuntimeState;
    private int currentRuneCastId = -1;
    private CombatHealth preferredNeedleTarget;
    private TrackEntry activeTrackEntry;
    private System.Random randomGenerator;
    private bool thrustVfxTriggered;
    private bool thrustHitTriggered;
    private bool needlePhaseTriggered;
    private Coroutine releaseRoutine;
    private float castFacingSign = 1f;
    private int currentNeedleCount;
    private int currentKillCount;
    private float currentTotalActualDamage;
    private float currentTotalHealAmount;
    private float currentTotalCooldownReduction;

    private void Reset()
    {
        cooldown = 15f;
        duration = 1.25f;
        effectPower = 50f;
        animationName = "ATK01";
        debugLog = true;
        thrustVfxStartTime = 0.48f;
        thrustHitTime = 0.58f;
        needleStartTime = 0.82f;
        needleInterval = 0.12f;
        skillEndTime = 1.25f;
        baseManaCost = 60f;
        thrustPhysicalBaseDamage = 60f;
        thrustSpecialToPhysicalScale = 0.80f;
        thrustSpecialBaseDamage = 40f;
        thrustPhysicalToSpecialScale = 0.40f;
        thrustRange = 2.4f;
        thrustWidth = 1.2f;
        thrustHeight = 1.4f;
        thrustForwardOffset = 1.2f;
        minNeedleCount = 4;
        maxNeedleCount = 12;
        spawnRadiusMin = 3.5f;
        spawnRadiusMax = 5.5f;
        heightMin = 0.5f;
        heightMax = 2.2f;
        targetHeightOffset = 0.8f;
        horizontalRandomAngle = 18f;
        needleDamageMultiplier = 0.50f;
        travelSpeed = 38f;
        passThroughDistance = 4.5f;
        fadeDuration = 0.3f;
        healFromDamagePercent = 0.50f;
        cooldownReductionPerKill = 1.0f;
        targetSearchRadius = 14f;
        fallbackTargetDistance = 6f;
    }

    private void Awake()
    {
        runeRuntimeState = ResolveRuneRuntimeState();
        if (eyeFireROffset == null)
        {
            eyeFireROffset = GetComponent<Player01EyeFireROffset>();
        }
        SyncLayerMasks();
        SyncSkillConfig();
    }

    private void OnValidate()
    {
        cooldown = Mathf.Max(0f, cooldown);
        duration = Mathf.Max(duration, skillEndTime);
        minNeedleCount = Mathf.Max(1, minNeedleCount);
        maxNeedleCount = Mathf.Max(minNeedleCount, maxNeedleCount);
        SyncLayerMasks();
        SyncSkillConfig();
    }

    private void Update()
    {
        SyncSkillConfig();
    }

    protected override void OnDisable()
    {
        CleanupRuntimeState(destroyActiveNeedles: true);
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        CleanupRuntimeState(destroyActiveNeedles: true);
        base.OnDestroy();
    }

    protected override int SkillIndex => 3;

    protected override string GetSkillLabel()
    {
        return "R - Needle Shot";
    }

    protected override void PrepareCastValidation()
    {
        SyncSkillConfig();
    }

    protected override void OnCastStarted()
    {
        runeRuntimeState = ResolveRuneRuntimeState();
        currentRuneCastId = runeRuntimeState != null ? runeRuntimeState.NotifySkillCastStarted(SkillIndex) : -1;
        thrustDamagedTargets.Clear();
        thrustPhaseTargets.Clear();
        killCreditTargets.Clear();
        preferredNeedleTarget = null;
        thrustVfxTriggered = false;
        thrustHitTriggered = false;
        needlePhaseTriggered = false;
        currentNeedleCount = 0;
        currentKillCount = 0;
        currentTotalActualDamage = 0f;
        currentTotalHealAmount = 0f;
        currentTotalCooldownReduction = 0f;
        ClearDestroyedNeedles();
        castFacingSign = ResolveFacingSign();
        Controller?.SetMovementInputLocked(true, "Player01 R");
        Controller?.SetFacingInputLocked(true, castFacingSign, "Player01 R");
        Player01EyeFireROffset[] eyeFireOffsets = GetComponentsInChildren<Player01EyeFireROffset>(true);
        Debug.Log(
            $"[R EyeFire] Component Count Under Player01 = {eyeFireOffsets.Length}\n" +
            $"[R EyeFire] Component Instance ID = {(eyeFireROffset != null ? eyeFireROffset.GetInstanceID() : 0)}\n" +
            $"[R EyeFire] Component GameObject = {(eyeFireROffset != null ? eyeFireROffset.gameObject.name : "null")}\n" +
            $"[R EyeFire] Offset Root = {(eyeFireROffset != null && eyeFireROffset.OffsetRoot != null ? eyeFireROffset.OffsetRoot.name : "null")}\n" +
            $"[R EyeFire] Offset Root Instance ID = {(eyeFireROffset != null && eyeFireROffset.OffsetRoot != null ? eyeFireROffset.OffsetRoot.GetInstanceID() : 0)}",
            eyeFireROffset != null ? eyeFireROffset : this);
        eyeFireROffset?.BeginROffset(castFacingSign);

        if (debugLog)
        {
            ResolveCurrentStats(out float physicalAttack, out float specialAttack);
            ResolveThrustDamageValues(physicalAttack, specialAttack, out float thrustPhysicalDamage, out float thrustSpecialDamage);
            ResolveNeedleDamageValues(physicalAttack, specialAttack, out float needlePhysicalDamage, out float needleSpecialDamage);
            Debug.Log(
                $"[Player01 R] Cast success MP={ResolveManaCost():F2} PATK={physicalAttack:F2} SATK={specialAttack:F2} " +
                $"thrustPhysical={thrustPhysicalDamage:F2} thrustSpecial={thrustSpecialDamage:F2} " +
                $"needlePhysical={needlePhysicalDamage:F2} needleSpecial={needleSpecialDamage:F2} " +
                $"cooldown={ResolveRuntimeCooldownSeconds():F2} castFacingSign={castFacingSign:F2}",
                this);
        }
    }

    protected override IEnumerator CastRoutine()
    {
        duration = Mathf.Max(duration, skillEndTime);
        float lockDuration = Mathf.Max(duration, skillEndTime);
        TrackEntry entry = Controller != null
            ? Controller.PlayLockedSkillAnimationEntry(animationName, false, lockDuration, true, "R")
            : null;
        AttachAnimationEvents(entry);

        float castStartTime = Time.time;

        while (Time.time - castStartTime < skillEndTime)
        {
            float elapsed = Time.time - castStartTime;

            if (!thrustVfxTriggered && elapsed >= thrustVfxStartTime)
            {
                TriggerThrustVfx();
            }

            if (!thrustHitTriggered && elapsed >= thrustHitTime)
            {
                TriggerThrustHit();
            }

            if (!needlePhaseTriggered && elapsed >= needleStartTime)
            {
                needlePhaseTriggered = true;
                yield return FireNeedlePhase();
            }

            yield return null;
        }

        if (!thrustVfxTriggered)
        {
            TriggerThrustVfx();
        }

        if (!thrustHitTriggered)
        {
            TriggerThrustHit();
        }

        if (!needlePhaseTriggered)
        {
            needlePhaseTriggered = true;
            yield return FireNeedlePhase();
        }

        releaseRoutine = StartCoroutine(FinishCastAndRestoreIdleRoutine());
        yield return releaseRoutine;
        CompleteCast();
    }

    protected override void OnCastFinished()
    {
        if (debugLog)
        {
            PlayerSkillCooldownManager cooldownManager = SkillResource;
            float currentCooldownRemaining = cooldownManager != null ? cooldownManager.GetCurrentSkillCD(SkillIndex) : 0f;
            Debug.Log(
                $"[Player01 R] End needles={currentNeedleCount} actualDamage={currentTotalActualDamage:F2} heal={currentTotalHealAmount:F2} " +
                $"kills={currentKillCount} cooldownReduced={currentTotalCooldownReduction:F2} remainingCD={currentCooldownRemaining:F2}",
                this);
        }

        ReleaseSkillState();
    }

    private void SyncLayerMasks()
    {
        if (needleHitLayers.value == 0)
        {
            needleHitLayers = thrustHitLayers;
        }

        if (targetSearchLayers.value == 0)
        {
            targetSearchLayers = thrustHitLayers;
        }
    }

    private void AttachAnimationEvents(TrackEntry entry)
    {
        DetachAnimationEvents();
        activeTrackEntry = entry;
        if (!useSpineAnimationEvents || activeTrackEntry == null)
        {
            return;
        }

        activeTrackEntry.Event += HandleAnimationEvent;
    }

    private void DetachAnimationEvents()
    {
        if (activeTrackEntry != null)
        {
            activeTrackEntry.Event -= HandleAnimationEvent;
            activeTrackEntry = null;
        }
    }

    private void HandleAnimationEvent(TrackEntry entry, Spine.Event spineEvent)
    {
        if (!useSpineAnimationEvents || spineEvent == null)
        {
            return;
        }

        string eventName = spineEvent.Data != null ? spineEvent.Data.Name : spineEvent.ToString();
        if (!thrustVfxTriggered && !string.IsNullOrWhiteSpace(thrustVfxEventName) && eventName == thrustVfxEventName)
        {
            TriggerThrustVfx();
            return;
        }

        if (!thrustHitTriggered && !string.IsNullOrWhiteSpace(thrustHitEventName) && eventName == thrustHitEventName)
        {
            TriggerThrustHit();
            return;
        }

        if (!needlePhaseTriggered && !string.IsNullOrWhiteSpace(needleStartEventName) && eventName == needleStartEventName)
        {
            needlePhaseTriggered = true;
            StartCoroutine(FireNeedlePhase());
        }
    }

    private void TriggerThrustVfx()
    {
        thrustVfxTriggered = true;
        if (thrustVfxPrefab == null)
        {
            return;
        }

        Transform attachTarget = thrustVfxAnchor != null ? thrustVfxAnchor : transform;
        Player01RThrustVfx instance = Instantiate(thrustVfxPrefab, attachTarget.position, attachTarget.rotation, attachTarget);
        instance.name = thrustVfxPrefab.name + "_Runtime";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        runtimeVfxInstances.Add(instance.gameObject);
        instance.Play(ResolveFacingSign());
    }

    private void TriggerThrustHit()
    {
        thrustHitTriggered = true;
        ApplyThrustDamage();
    }

    private IEnumerator FireNeedlePhase()
    {
        Vector3 targetPoint = ResolveNeedleTargetPoint();
        currentNeedleCount = ResolveNeedleCount();
        Player01RFourNeedleUtility.SpawnSettings settings = new Player01RFourNeedleUtility.SpawnSettings
        {
            needleCount = currentNeedleCount,
            spawnRadiusMin = spawnRadiusMin,
            spawnRadiusMax = spawnRadiusMax,
            heightMin = heightMin,
            heightMax = heightMax,
            horizontalRandomAngle = horizontalRandomAngle,
            enforceMinVisibleAngle = enforceNeedleMinVisibleAngle,
            minVisibleAngle = minVisibleAngle,
            viewCamera = Camera.main
        };

        randomGenerator = useRandomSeed
            ? new System.Random(randomSeed)
            : new System.Random(System.Environment.TickCount ^ GetInstanceID() ^ currentRuneCastId);

        Vector3[] spawnPositions = Player01RFourNeedleUtility.BuildSpawnPositions(
            targetPoint,
            Controller != null ? Controller.GetFacingWorldDirection() : transform.forward,
            settings,
            NextRange);

        if (debugLog)
        {
            Debug.Log($"[Player01 R] needleCount={currentNeedleCount} targetPoint={targetPoint}", this);
        }

        for (int i = 0; i < spawnPositions.Length; i++)
        {
            SpawnNeedle(spawnPositions[i], targetPoint, i);
            if (i < spawnPositions.Length - 1 && needleInterval > 0f)
            {
                yield return new WaitForSeconds(needleInterval);
            }
        }
    }

    private void SpawnNeedle(Vector3 spawnPosition, Vector3 targetPoint, int index)
    {
        if (needlePrefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(needlePrefab, spawnPosition, Quaternion.identity);
        Player01REnergyNeedle needle = instance.GetComponent<Player01REnergyNeedle>();
        if (needle == null)
        {
            needle = instance.GetComponentInChildren<Player01REnergyNeedle>(true);
        }

        if (needle == null)
        {
            Destroy(instance);
            return;
        }

        ResolveCurrentStats(out float physicalAttack, out float specialAttack);
        ResolveNeedleDamageValues(physicalAttack, specialAttack, out float needlePhysicalDamage, out float needleSpecialDamage);

        needle.name = needle.name + "_R_" + index;
        needle.Launch(
            spawnPosition,
            targetPoint,
            travelSpeed,
            passThroughDistance,
            fadeDuration);

        Player01REnergyNeedleDamageDealer damageDealer = needle.GetComponent<Player01REnergyNeedleDamageDealer>();
        if (damageDealer == null)
        {
            damageDealer = needle.gameObject.AddComponent<Player01REnergyNeedleDamageDealer>();
        }

        damageDealer.Initialize(
            this,
            gameObject,
            needlePhysicalDamage,
            needleSpecialDamage,
            needleHitLayers,
            SkillIndex,
            currentRuneCastId,
            healFromDamagePercent,
            needleHitRadius);

        activeNeedles.Add(needle);
    }

    private void ApplyThrustDamage()
    {
        thrustDamagedTargets.Clear();

        Vector3 facing = ResolveFacingFlatDirection();
        Quaternion orientation = Quaternion.LookRotation(facing, Vector3.up);
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.05f, thrustWidth) * 0.5f,
            Mathf.Max(0.05f, thrustHeight) * 0.5f,
            Mathf.Max(0.05f, thrustRange) * 0.5f);
        Vector3 center = transform.position + facing * (Mathf.Max(0.05f, thrustForwardOffset) + halfExtents.z);

        Collider[] hits = Physics.OverlapBox(center, halfExtents, orientation, thrustHitLayers, QueryTriggerInteraction.Collide);
        ResolveCurrentStats(out float physicalAttack, out float specialAttack);
        ResolveThrustDamageValues(physicalAttack, specialAttack, out float thrustPhysicalDamage, out float thrustSpecialDamage);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || !BattleTargetUtility.IsMonster(hit, transform))
            {
                continue;
            }

            CombatHealth combatHealth = BattleTargetUtility.GetMonsterCombatHealth(hit, transform);
            if (combatHealth == null || combatHealth.IsDead || !thrustDamagedTargets.Add(combatHealth))
            {
                continue;
            }

            thrustPhaseTargets.Add(combatHealth);
            ApplyHybridDamageToTarget(combatHealth, thrustPhysicalDamage, thrustSpecialDamage);

            if (preferredNeedleTarget == null)
            {
                preferredNeedleTarget = combatHealth;
            }
        }
    }

    private Vector3 ResolveNeedleTargetPoint()
    {
        if (TryResolveAverageCenter(thrustPhaseTargets, out Vector3 hitCenter))
        {
            return hitCenter;
        }

        if (TryResolveAverageCenter(FindLivingTargetsInSearchRadius(), out Vector3 searchCenter))
        {
            return searchCenter;
        }

        CombatHealth targetHealth = ResolveNeedleTarget();
        if (targetHealth != null)
        {
            return ResolveTargetCenter(targetHealth.transform);
        }

        Vector3 facing = ResolveFacingFlatDirection();
        return transform.position + facing * Mathf.Max(1f, fallbackTargetDistance) + Vector3.up * targetHeightOffset;
    }

    private CombatHealth ResolveNeedleTarget()
    {
        if (preferredNeedleTarget != null && !preferredNeedleTarget.IsDead)
        {
            return preferredNeedleTarget;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, Mathf.Max(0.5f, targetSearchRadius), targetSearchLayers, QueryTriggerInteraction.Collide);
        CombatHealth nearest = null;
        float nearestSqrDistance = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || !BattleTargetUtility.IsMonster(hit, transform))
            {
                continue;
            }

            CombatHealth combatHealth = BattleTargetUtility.GetMonsterCombatHealth(hit, transform);
            if (combatHealth == null || combatHealth.IsDead)
            {
                continue;
            }

            Vector3 targetCenter = ResolveTargetCenter(combatHealth.transform);
            float sqrDistance = (targetCenter - transform.position).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = combatHealth;
            }
        }

        preferredNeedleTarget = nearest;
        return nearest;
    }

    private Vector3 ResolveTargetCenter(Transform target)
    {
        if (target == null)
        {
            return transform.position + Vector3.up * targetHeightOffset;
        }

        Collider collider = target.GetComponentInChildren<Collider>();
        if (collider == null)
        {
            collider = target.GetComponentInParent<Collider>();
        }

        if (collider != null)
        {
            return collider.bounds.center;
        }

        return target.position + Vector3.up * targetHeightOffset;
    }

    private Vector3 ResolveFacingFlatDirection()
    {
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

        return facing.normalized;
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

    private float NextRange(float min, float max)
    {
        if (randomGenerator == null)
        {
            randomGenerator = useRandomSeed
                ? new System.Random(System.Environment.TickCount ^ GetInstanceID())
                : new System.Random(randomSeed);
        }

        if (Mathf.Approximately(min, max))
        {
            return min;
        }

        return Mathf.Lerp(min, max, (float)randomGenerator.NextDouble());
    }

    public void RegisterNeedleDamageResult(CombatHealth target, float actualDamage, bool killedByThisHit)
    {
        RegisterDamageResult(target, actualDamage, killedByThisHit);
    }

    private float ConsumeRuneFirstHitBonusDamage()
    {
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

    private void CleanupRuntimeState(bool destroyActiveNeedles)
    {
        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }

        DetachAnimationEvents();
        StopAndClearRuntimeVfx();
        if (destroyActiveNeedles)
        {
            DestroyActiveNeedles();
        }
        else
        {
            ClearDestroyedNeedles();
        }

        thrustDamagedTargets.Clear();
        thrustPhaseTargets.Clear();
        killCreditTargets.Clear();
        preferredNeedleTarget = null;
        currentRuneCastId = -1;
        thrustVfxTriggered = false;
        thrustHitTriggered = false;
        needlePhaseTriggered = false;
        currentNeedleCount = 0;
        currentKillCount = 0;
        currentTotalActualDamage = 0f;
        currentTotalHealAmount = 0f;
        currentTotalCooldownReduction = 0f;
        castRoutine = null;
        if (destroyActiveNeedles)
        {
            eyeFireROffset?.ImmediateReset();
            Controller?.SetMovementInputLocked(false, "Player01 R");
            Controller?.SetFacingInputLocked(false, castFacingSign, "Player01 R");
            Controller?.ClearSkillAnimationLock();
        }
        else
        {
            eyeFireROffset?.EndROffset();
        }
    }

    private IEnumerator FinishCastAndRestoreIdleRoutine()
    {
        if (Controller == null)
        {
            yield break;
        }

        Debug.Log("[Player01 R End] R animation finished", this);
        Controller.RestoreLocomotionAnimationIgnoringSkillLock(true);
        Debug.Log("[Player01 R End] Idle restored", this);
        yield return null;
        Controller.SetMovementInputLocked(false, "Player01 R");
        Debug.Log("[Player01 R End] movement unlocked", this);
        Controller.SetFacingInputLocked(false, castFacingSign, "Player01 R");
        Debug.Log("[Player01 R End] facing unlocked", this);
        Controller.ClearSkillAnimationLock();
        releaseRoutine = null;
    }

    private void ApplyHybridDamageToTarget(CombatHealth combatHealth, float physicalDamage, float specialDamage)
    {
        if (combatHealth == null || combatHealth.IsDead)
        {
            return;
        }

        float beforeHealth = ResolveCurrentHealth(combatHealth);
        float runeBonusDamage = ConsumeRuneFirstHitBonusDamage();
        float resolvedPhysicalDamage = Mathf.Max(0f, physicalDamage + runeBonusDamage);
        float resolvedSpecialDamage = Mathf.Max(0f, specialDamage);

        if (resolvedPhysicalDamage > 0f)
        {
            combatHealth.TakeDamage(new BattleDamage(resolvedPhysicalDamage, BattleDamageType.Physical, gameObject));
        }

        if (!combatHealth.IsDead && resolvedSpecialDamage > 0f)
        {
            combatHealth.TakeDamage(new BattleDamage(resolvedSpecialDamage, BattleDamageType.Special, gameObject));
        }

        float actualDamage = Mathf.Max(0f, beforeHealth - ResolveCurrentHealth(combatHealth));
        runeRuntimeState?.NotifyMonsterDamagedBySkill(SkillIndex, combatHealth, actualDamage);
        RegisterDamageResult(combatHealth, actualDamage, actualDamage > 0f && combatHealth.IsDead);
    }

    private void RegisterDamageResult(CombatHealth target, float actualDamage, bool killedByThisHit)
    {
        if (actualDamage <= 0f)
        {
            return;
        }

        currentTotalActualDamage += actualDamage;

        float healAmount = actualDamage * Mathf.Clamp01(healFromDamagePercent);
        if (healAmount > 0f)
        {
            CombatHealth sourceHealth = GetComponent<CombatHealth>();
            if (sourceHealth != null)
            {
                sourceHealth.Heal(healAmount);
            }
            else
            {
                BattleResourceBank bank = GetComponent<BattleResourceBank>();
                bank?.Heal(healAmount);
            }

            currentTotalHealAmount += healAmount;
        }

        if (killedByThisHit && target != null && killCreditTargets.Add(target))
        {
            currentKillCount++;
            currentTotalCooldownReduction += cooldownReductionPerKill;
            ApplyKillCooldownReduction();
        }
    }

    private void ApplyKillCooldownReduction()
    {
        if (SkillResource == null || cooldownReductionPerKill <= 0f)
        {
            return;
        }

        float remaining = SkillResource.ReduceCurrentSkillCooldown(SkillIndex, cooldownReductionPerKill);
        Controller?.SyncSkillHudCooldown("R");

        if (debugLog)
        {
            Debug.Log($"[Player01 R] kill cooldown reduction applied -{cooldownReductionPerKill:F2}s => remaining={remaining:F2}", this);
        }
    }

    private int ResolveNeedleCount()
    {
        int resolvedMin = Mathf.Max(1, minNeedleCount);
        int resolvedMax = Mathf.Max(resolvedMin, maxNeedleCount);
        if (randomGenerator == null)
        {
            randomGenerator = useRandomSeed
                ? new System.Random(randomSeed)
                : new System.Random(System.Environment.TickCount ^ GetInstanceID() ^ currentRuneCastId);
        }

        return randomGenerator.Next(resolvedMin, resolvedMax + 1);
    }

    private void ResolveCurrentStats(out float physicalAttack, out float specialAttack)
    {
        CombatStats combatStats = GetComponent<CombatStats>();
        physicalAttack = combatStats != null ? Mathf.Max(0f, combatStats.physicalAttack) : 0f;
        specialAttack = combatStats != null ? Mathf.Max(0f, combatStats.specialAttack) : 0f;
    }

    private void ResolveThrustDamageValues(float physicalAttack, float specialAttack, out float physicalDamage, out float specialDamage)
    {
        physicalDamage = Mathf.Max(0f, thrustPhysicalBaseDamage + specialAttack * thrustSpecialToPhysicalScale);
        specialDamage = Mathf.Max(0f, thrustSpecialBaseDamage + physicalAttack * thrustPhysicalToSpecialScale);
    }

    private void ResolveNeedleDamageValues(float physicalAttack, float specialAttack, out float physicalDamage, out float specialDamage)
    {
        ResolveThrustDamageValues(physicalAttack, specialAttack, out float thrustPhysicalDamage, out float thrustSpecialDamage);
        float multiplier = Mathf.Max(0f, needleDamageMultiplier);
        physicalDamage = thrustPhysicalDamage * multiplier;
        specialDamage = thrustSpecialDamage * multiplier;
    }

    private List<CombatHealth> FindLivingTargetsInSearchRadius()
    {
        List<CombatHealth> targets = new List<CombatHealth>();
        Collider[] hits = Physics.OverlapSphere(transform.position, Mathf.Max(0.5f, targetSearchRadius), targetSearchLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || !BattleTargetUtility.IsMonster(hit, transform))
            {
                continue;
            }

            CombatHealth combatHealth = BattleTargetUtility.GetMonsterCombatHealth(hit, transform);
            if (combatHealth == null || combatHealth.IsDead || targets.Contains(combatHealth))
            {
                continue;
            }

            targets.Add(combatHealth);
        }

        return targets;
    }

    private bool TryResolveAverageCenter(List<CombatHealth> targets, out Vector3 center)
    {
        center = Vector3.zero;
        if (targets == null || targets.Count == 0)
        {
            return false;
        }

        int validCount = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            CombatHealth target = targets[i];
            if (target == null || target.IsDead)
            {
                continue;
            }

            center += ResolveTargetCenter(target.transform);
            validCount++;
        }

        if (validCount <= 0)
        {
            return false;
        }

        center /= validCount;
        return true;
    }

    private float ResolveManaCost()
    {
        return CalculateFinalManaCost(baseManaCost);
    }

    private float CalculateFinalManaCost(float configuredBaseManaCost)
    {
        float finalManaCost = Mathf.Max(0f, configuredBaseManaCost);

        // Future mana-cost modifiers should be applied here so the skill keeps using
        // the shared SkillResource gate/consume flow without changing cast logic.
        return Mathf.Max(0f, finalManaCost);
    }

    private float ResolveRuntimeCooldownSeconds()
    {
        if (SkillResource != null && SkillIndex >= 0)
        {
            return SkillResource.GetSkillMaxCD(SkillIndex);
        }

        return Mathf.Max(0f, cooldown);
    }

    private void SyncSkillConfig()
    {
        cooldown = Mathf.Max(0f, cooldown);
        duration = Mathf.Max(duration, skillEndTime);

        if (SkillResource != null && SkillIndex >= 0)
        {
            SkillResource.OverrideSkillConfig(SkillIndex, cooldown, ResolveManaCost());
        }

        if (Controller != null)
        {
            var rCooldownField = typeof(Player01SkillController).GetField("rCooldown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (rCooldownField != null)
            {
                rCooldownField.SetValue(Controller, cooldown);
            }
        }
    }

    private void ReleaseSkillState()
    {
        CleanupRuntimeState(destroyActiveNeedles: false);
    }

    private void StopAndClearRuntimeVfx()
    {
        for (int i = runtimeVfxInstances.Count - 1; i >= 0; i--)
        {
            GameObject instance = runtimeVfxInstances[i];
            if (instance != null)
            {
                Destroy(instance);
            }
        }

        runtimeVfxInstances.Clear();
    }

    private void DestroyActiveNeedles()
    {
        for (int i = activeNeedles.Count - 1; i >= 0; i--)
        {
            Player01REnergyNeedle needle = activeNeedles[i];
            if (needle != null)
            {
                Destroy(needle.gameObject);
            }
        }

        activeNeedles.Clear();
    }

    private void ClearDestroyedNeedles()
    {
        for (int i = activeNeedles.Count - 1; i >= 0; i--)
        {
            if (activeNeedles[i] == null)
            {
                activeNeedles.RemoveAt(i);
            }
        }
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

    private void OnDrawGizmosSelected()
    {
        if (!debugDrawThrustHitbox)
        {
            return;
        }

        Vector3 facing = Application.isPlaying ? ResolveFacingFlatDirection() : Vector3.right;
        Quaternion orientation = Quaternion.LookRotation(facing, Vector3.up);
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.05f, thrustWidth) * 0.5f,
            Mathf.Max(0.05f, thrustHeight) * 0.5f,
            Mathf.Max(0.05f, thrustRange) * 0.5f);
        Vector3 center = transform.position + facing * (Mathf.Max(0.05f, thrustForwardOffset) + halfExtents.z);

        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, orientation, Vector3.one);
        Gizmos.color = new Color(0.2f, 1f, 1f, 0.85f);
        Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        Gizmos.matrix = previous;
    }
}
