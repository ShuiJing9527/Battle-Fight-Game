using System.Collections;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Serialization;

public class Player1Skill_R_NeedleShot : Player01SkillBase
{
    private const float NightBuffDamageMultiplier = 1.15f;
    private const float NightBuffExtraCooldownRefundRatio = 0.15f;

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
    [SerializeField, Min(0f)] private float thrustCloseRangeRadius = 0.8f;
    [SerializeField, Min(0f)] private float thrustCloseRangeForwardOffset = 0.2f;
    [FormerlySerializedAs("enemyLayer")]
    [SerializeField] private LayerMask thrustHitLayers = ~0;
    [SerializeField] private bool debugDrawThrustHitbox;
    [SerializeField] private bool debugRHitDetection;

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
    private bool thrustDamageAppliedThisCast;
    private bool needlePhaseTriggered;
    private Coroutine releaseRoutine;
    private float castFacingSign = 1f;
    private int currentCastId;
    private static int nextCastId = 1;
    private int currentNeedleCount;
    private int currentKillCount;
    private float currentTotalActualDamage;
    private float currentMeleeActualDamageThisCast;
    private float currentTotalHealAmount;
    private float currentTotalCooldownReduction;
    // Night Child state is independent from day/night phase.
    private bool nightChildStateActiveThisCast;
    private bool nightBuffDamageLoggedThisCast;
    private bool nightBuffExtraCooldownRefundTriggered;

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
        runeRuntimeState = ResolvePlayerRuneRuntimeState();
        currentRuneCastId = CurrentRuneCastId;
        thrustDamagedTargets.Clear();
        thrustPhaseTargets.Clear();
        killCreditTargets.Clear();
        preferredNeedleTarget = null;
        thrustVfxTriggered = false;
        thrustHitTriggered = false;
        thrustDamageAppliedThisCast = false;
        needlePhaseTriggered = false;
        currentNeedleCount = 0;
        currentKillCount = 0;
        currentTotalActualDamage = 0f;
        currentMeleeActualDamageThisCast = 0f;
        currentTotalHealAmount = 0f;
        currentTotalCooldownReduction = 0f;
        currentCastId = nextCastId++;
        nightChildStateActiveThisCast = DayNightAffinityDamageModifier.HasNightChildState(Controller != null ? Controller.gameObject : gameObject);
        nightBuffDamageLoggedThisCast = false;
        nightBuffExtraCooldownRefundTriggered = false;
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
                $"cooldown={ResolveRuntimeCooldownSeconds():F2} castFacingSign={castFacingSign:F2} castId={currentCastId}",
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
        Debug.Log(
            $"[Player1R-Melee] TriggerThrustHit CALLED castId={currentCastId} frame={Time.frameCount} " +
            $"skillInstance={GetInstanceID()} thrustHitTriggered={thrustHitTriggered} thrustDamageAppliedThisCast={thrustDamageAppliedThisCast}",
            this);

        if (thrustDamageAppliedThisCast)
        {
            if (debugRHitDetection)
            {
                Debug.Log(
                    $"[Player1R-Melee] skipped reason=AlreadyApplied castId={currentCastId} frame={Time.frameCount}",
                    this);
            }

            thrustHitTriggered = true;
            return;
        }

        thrustHitTriggered = true;
        thrustDamageAppliedThisCast = true;
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
        ResolveNeedleDamageValues(physicalAttack, specialAttack, out float theoreticalNeedlePhysicalDamage, out float theoreticalNeedleSpecialDamage);
        float combatHealthAttackerMultiplier = ResolveNeedleCombatHealthAttackerMultiplier();
        ResolveActualNeedleDamageValues(
            theoreticalNeedlePhysicalDamage,
            theoreticalNeedleSpecialDamage,
            currentMeleeActualDamageThisCast,
            combatHealthAttackerMultiplier,
            out float needlePhysicalDamage,
            out float needleSpecialDamage);
        float finalNeedleDamage = needlePhysicalDamage + needleSpecialDamage;

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

        if (debugRHitDetection)
        {
            Debug.Log(
                $"[Player01RHitDetection] castId={currentCastId} event=SpawnNeedle index={index} meleeActualDamageCache={currentMeleeActualDamageThisCast:F2} " +
                $"combatHealthAttackerMultiplier={combatHealthAttackerMultiplier:F2} " +
                $"needleDamageMultiplier={needleDamageMultiplier:F2} theoreticalNeedlePhysical={theoreticalNeedlePhysicalDamage:F2} theoreticalNeedleSpecial={theoreticalNeedleSpecialDamage:F2} " +
                $"finalNeedlePhysical={needlePhysicalDamage:F2} finalNeedleSpecial={needleSpecialDamage:F2} finalNeedleDamage={finalNeedleDamage:F2} " +
                $"zeroBecauseNoMeleeDamage={(currentMeleeActualDamageThisCast <= 0f)}",
                this);
        }
    }

    private void ApplyThrustDamage()
    {
        Debug.Log(
            $"[Player1R-Melee] ApplyThrustDamage CALLED castId={currentCastId} frame={Time.frameCount} " +
            $"skillInstance={GetInstanceID()}",
            this);

        thrustDamagedTargets.Clear();

        Transform thrustOrigin = ResolveThrustOrigin();
        Vector3 originPosition = thrustOrigin != null ? thrustOrigin.position : transform.position;
        Vector3 facing = ResolveFacingFlatDirection();
        Quaternion orientation = Quaternion.LookRotation(facing, Vector3.up);
        float width = Mathf.Max(0.05f, thrustWidth);
        float height = Mathf.Max(0.05f, thrustHeight);
        float originalRange = Mathf.Max(0.05f, thrustRange);
        float forwardOffset = Mathf.Max(0f, thrustForwardOffset);
        float farReach = forwardOffset + originalRange;
        Vector3 halfExtents = new Vector3(
            width * 0.5f,
            height * 0.5f,
            Mathf.Max(originalRange * 0.5f, farReach * 0.5f));
        Vector3 center = originPosition + facing * halfExtents.z;
        float closeRadius = Mathf.Max(0f, thrustCloseRangeRadius);
        Vector3 closeCenter = originPosition + facing * Mathf.Max(0f, thrustCloseRangeForwardOffset);

        Collider[] boxHits = Physics.OverlapBox(center, halfExtents, orientation, thrustHitLayers, QueryTriggerInteraction.Collide);
        Collider[] closeHits = closeRadius > 0f
            ? Physics.OverlapSphere(closeCenter, closeRadius, thrustHitLayers, QueryTriggerInteraction.Collide)
            : System.Array.Empty<Collider>();
        Collider[] closeHitsUnfiltered = debugRHitDetection && closeRadius > 0f
            ? Physics.OverlapSphere(closeCenter, closeRadius, ~0, QueryTriggerInteraction.Collide)
            : System.Array.Empty<Collider>();
        ResolveCurrentStats(out float physicalAttack, out float specialAttack);
        ResolveThrustDamageValues(physicalAttack, specialAttack, out float thrustPhysicalDamage, out float thrustSpecialDamage);

        if (debugRHitDetection)
        {
            Debug.DrawLine(originPosition, originPosition + facing * 0.8f, Color.cyan, 1.5f);
            Debug.DrawLine(originPosition, center - facing * halfExtents.z, Color.green, 1.5f);
            Debug.DrawLine(originPosition, center + facing * halfExtents.z, Color.red, 1.5f);
            Debug.Log(
                $"[Player01RHitDetection] castId={currentCastId} event=ApplyThrustDamage entered scriptInstance={GetInstanceID()} playerPos={transform.position} " +
                $"origin={(thrustOrigin != null ? thrustOrigin.name : "transform")} originPos={originPosition} facing={facing} boxCenter={center} hitHalfExtents={halfExtents} " +
                $"orientationEuler={orientation.eulerAngles} originalRange={originalRange:F2} forwardOffset={forwardOffset:F2} closeCenter={closeCenter} closeRadius={closeRadius:F2} " +
                $"layerMaskValue={thrustHitLayers.value} boxColliderCount={boxHits.Length} closeColliderCount={closeHits.Length} debugAllLayerCloseColliderCount={closeHitsUnfiltered.Length} " +
                $"thrustPhysicalTheoretical={thrustPhysicalDamage:F2} thrustSpecialTheoretical={thrustSpecialDamage:F2}",
                this);
        }

        LogDiagnosticColliders(closeHitsUnfiltered, closeCenter, closeRadius);

        ProcessThrustHitColliders(boxHits, "Box", thrustPhysicalDamage, thrustSpecialDamage);
        ProcessThrustHitColliders(closeHits, "CloseRange", thrustPhysicalDamage, thrustSpecialDamage);

        if (debugRHitDetection && thrustDamagedTargets.Count <= 0)
        {
            Debug.Log(
                $"[Player01RHitDetection] castId={currentCastId} event=ThrustNoDamage cachedMeleeActualDamage={currentMeleeActualDamageThisCast:F2}",
                this);
        }
    }

    private void LogDiagnosticColliders(Collider[] hits, Vector3 closeCenter, float closeRadius)
    {
        if (!debugRHitDetection || hits == null)
        {
            return;
        }

        if (hits.Length <= 0)
        {
            Debug.Log(
                $"[Player01RHitDetection] castId={currentCastId} event=DiagnosticOverlapSphereAll result=NoColliders closeCenter={closeCenter} closeRadius={closeRadius:F2}",
                this);
            return;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                Debug.Log(
                    $"[Player01RHitDetection] castId={currentCastId} event=DiagnosticOverlapSphereAll result=NullCollider index={i}",
                    this);
                continue;
            }

            CombatHealth combatHealth = hit.GetComponentInParent<CombatHealth>();
            EnemyController enemyController = hit.GetComponentInParent<EnemyController>();
            string hierarchyPath = BuildHierarchyPath(hit.transform);
            string layerName = LayerMask.LayerToName(hit.gameObject.layer);
            string combatHealthName = combatHealth != null
                ? combatHealth.name + "#" + combatHealth.GetInstanceID()
                : "null";
            string enemyControllerName = enemyController != null
                ? enemyController.name + "#" + enemyController.GetInstanceID()
                : "null";
            Debug.Log(
                $"[Player01RHitDetection] castId={currentCastId} event=DiagnosticOverlapSphereAll index={i} collider={hit.name} " +
                $"path={hierarchyPath} layer={hit.gameObject.layer} layerName={layerName} " +
                $"isTrigger={hit.isTrigger} enabled={hit.enabled} activeInHierarchy={hit.gameObject.activeInHierarchy} " +
                $"boundsCenter={hit.bounds.center} boundsMin={hit.bounds.min} boundsMax={hit.bounds.max} " +
                $"combatHealth={combatHealthName} enemyController={enemyControllerName}",
                this);
        }
    }

    private static string BuildHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "null";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(target.name);
        Transform current = target.parent;
        while (current != null)
        {
            builder.Insert(0, current.name + "/");
            current = current.parent;
        }

        return builder.ToString();
    }

    private void ProcessThrustHitColliders(Collider[] hits, string sourceLabel, float thrustPhysicalDamage, float thrustSpecialDamage)
    {
        if (hits == null)
        {
            return;
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                if (debugRHitDetection)
                {
                    Debug.Log($"[Player01RHitDetection] castId={currentCastId} event=ColliderSkipped source={sourceLabel} reason=NullCollider index={i}", this);
                }
                continue;
            }

            bool inLayerMask = ((1 << hit.gameObject.layer) & thrustHitLayers.value) != 0;
            CombatHealth parentCombatHealth = hit.GetComponentInParent<CombatHealth>();

            if (!BattleTargetUtility.TryGetMonsterCombatHealth(hit, transform, out CombatHealth combatHealth, out string rejectReason))
            {
                if (debugRHitDetection)
                {
                    Debug.Log(
                        $"[Player01RHitDetection] castId={currentCastId} event=ColliderSkipped source={sourceLabel} reason={rejectReason} collider={hit.name} " +
                        $"root={hit.transform.root.name} layer={LayerMask.LayerToName(hit.gameObject.layer)}({hit.gameObject.layer}) inLayerMask={inLayerMask} isTrigger={hit.isTrigger} " +
                        $"activeSelf={hit.gameObject.activeSelf} activeInHierarchy={hit.gameObject.activeInHierarchy} enabled={hit.enabled} " +
                        $"parentCombatHealth={(parentCombatHealth != null ? parentCombatHealth.name : "null")} boundsCenter={hit.bounds.center} boundsMin={hit.bounds.min} boundsMax={hit.bounds.max}",
                        this);
                }
                continue;
            }

            if (!thrustDamagedTargets.Add(combatHealth))
            {
                if (debugRHitDetection)
                {
                    Debug.Log(
                        $"[Player01RHitDetection] castId={currentCastId} event=ColliderSkipped source={sourceLabel} reason=DuplicateTarget collider={hit.name} target={combatHealth.name}",
                        this);
                }
                continue;
            }

            if (debugRHitDetection)
            {
                Debug.Log(
                    $"[Player01RHitDetection] castId={currentCastId} event=ColliderAccepted source={sourceLabel} collider={hit.name} target={combatHealth.name} " +
                    $"layer={LayerMask.LayerToName(hit.gameObject.layer)}({hit.gameObject.layer}) inLayerMask={inLayerMask} isTrigger={hit.isTrigger} boundsCenter={hit.bounds.center} boundsMin={hit.bounds.min} boundsMax={hit.bounds.max}",
                    this);
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

    private Transform ResolveThrustOrigin()
    {
        ATTACK basicAttack = GetComponent<ATTACK>();
        if (basicAttack != null && basicAttack.attackPoint != null)
        {
            return basicAttack.attackPoint;
        }

        if (thrustVfxAnchor != null)
        {
            return thrustVfxAnchor;
        }

        return transform;
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
        thrustDamageAppliedThisCast = false;
        needlePhaseTriggered = false;
        currentNeedleCount = 0;
        currentKillCount = 0;
        currentTotalActualDamage = 0f;
        currentMeleeActualDamageThisCast = 0f;
        currentTotalHealAmount = 0f;
        currentTotalCooldownReduction = 0f;
        nightChildStateActiveThisCast = false;
        nightBuffDamageLoggedThisCast = false;
        nightBuffExtraCooldownRefundTriggered = false;
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
            if (debugRHitDetection)
            {
                Debug.Log(
                    $"[Player1R-Melee] skipped reason={(combatHealth == null ? "TargetNull" : "TargetDead")} castId={currentCastId}",
                    this);
            }
            return;
        }

        float beforeEffectiveHealth = ResolveCurrentEffectiveHealth(combatHealth);
        float beforeHealth = ResolveCurrentHealth(combatHealth);
        float beforeShield = Mathf.Max(0f, combatHealth.GetCurrentShield());
        float runeBonusDamage = ConsumeRuneFirstHitBonusDamage();
        float resolvedPhysicalDamage = Mathf.Max(0f, physicalDamage + runeBonusDamage);
        float resolvedSpecialDamage = Mathf.Max(0f, specialDamage);
        float skillDamageTakenMultiplier = PlayerSkillDamageTakenDebuffReceiver.ResolvePlayer01SkillDamageMultiplier(combatHealth.gameObject);
        resolvedPhysicalDamage *= skillDamageTakenMultiplier;
        resolvedSpecialDamage *= skillDamageTakenMultiplier;
        float skillDamageMultiplier = ResolveActiveSkillDamageMultiplier();
        resolvedPhysicalDamage *= skillDamageMultiplier;
        resolvedSpecialDamage *= skillDamageMultiplier;

        if (resolvedPhysicalDamage > 0f)
        {
            combatHealth.TakeDamage(new BattleDamage(resolvedPhysicalDamage, BattleDamageType.Physical, gameObject));
        }

        if (!combatHealth.IsDead && resolvedSpecialDamage > 0f)
        {
            combatHealth.TakeDamage(new BattleDamage(resolvedSpecialDamage, BattleDamageType.Special, gameObject));
        }

        float afterEffectiveHealth = ResolveCurrentEffectiveHealth(combatHealth);
        float afterHealth = ResolveCurrentHealth(combatHealth);
        float afterShield = Mathf.Max(0f, combatHealth.GetCurrentShield());
        float actualDamage = Mathf.Max(0f, beforeEffectiveHealth - afterEffectiveHealth);

        if (debugRHitDetection)
        {
            Debug.Log(
                $"[Player1R-Melee] castId={currentCastId} target={combatHealth.name} targetId={combatHealth.GetInstanceID()} " +
                $"physicalDamage={resolvedPhysicalDamage:F2} specialDamage={resolvedSpecialDamage:F2} " +
                $"hpBefore={beforeHealth:F2} shieldBefore={beforeShield:F2} hpAfter={afterHealth:F2} shieldAfter={afterShield:F2} actualDamage={actualDamage:F2}",
                this);
        }

        runeRuntimeState?.NotifyMonsterDamagedBySkill(SkillIndex, combatHealth, actualDamage);
        NotifySkillDamageApplied(actualDamage, combatHealth, "R thrust");
        UpdateMeleeDamageCache(actualDamage, combatHealth, resolvedPhysicalDamage + resolvedSpecialDamage);
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
            TryApplyNightBuffExtraCooldownRefund();
        }
    }

    public float ResolveActiveSkillDamageMultiplier()
    {
        float multiplier = 1f;
        if (nightChildStateActiveThisCast)
        {
            multiplier *= NightBuffDamageMultiplier;
            if (!nightBuffDamageLoggedThisCast)
            {
                nightBuffDamageLoggedThisCast = true;
                Debug.Log($"[SecondBuffDebug] Player01 R night buff damage bonus active x{NightBuffDamageMultiplier:F2}.", this);
            }
        }

        PlayerTimedSkillDamageBoostStatus timedSkillDamageBoost = PlayerTimedSkillDamageBoostStatus.Resolve(Controller != null ? Controller.gameObject : gameObject);
        if (timedSkillDamageBoost != null)
        {
            multiplier *= timedSkillDamageBoost.Multiplier;
        }

        return multiplier;
    }

    public void NotifySkillDamageApplied(float actualDamage, CombatHealth target, string sourceLabel)
    {
        if (actualDamage <= 0f)
        {
            return;
        }
    }

    private void UpdateMeleeDamageCache(float actualDamage, CombatHealth target, float theoreticalDamage)
    {
        float previous = currentMeleeActualDamageThisCast;
        currentMeleeActualDamageThisCast = Mathf.Max(currentMeleeActualDamageThisCast, Mathf.Max(0f, actualDamage));

        if (debugRHitDetection)
        {
            Debug.Log(
                $"[Player01RHitDetection] castId={currentCastId} event=MeleeDamageResolved target={(target != null ? target.name : "null")} " +
                $"theoreticalMeleeDamage={theoreticalDamage:F2} actualMeleeDamage={actualDamage:F2} cachedActualMeleeDamage={currentMeleeActualDamageThisCast:F2} previousCache={previous:F2}",
                this);
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

    private void TryApplyNightBuffExtraCooldownRefund()
    {
        if (!nightChildStateActiveThisCast || nightBuffExtraCooldownRefundTriggered || SkillResource == null)
        {
            return;
        }

        float currentRemainingCooldown = SkillResource.GetCurrentSkillCD(SkillIndex);
        if (currentRemainingCooldown <= 0f)
        {
            return;
        }

        float extraRefund = currentRemainingCooldown * NightBuffExtraCooldownRefundRatio;
        if (extraRefund <= 0f)
        {
            return;
        }

        float remaining = SkillResource.ReduceCurrentSkillCooldown(SkillIndex, extraRefund);
        Controller?.SyncSkillHudCooldown("R");
        currentTotalCooldownReduction += extraRefund;
        nightBuffExtraCooldownRefundTriggered = true;
        Debug.Log($"[SecondBuffDebug] Player01 R night buff extra cooldown refund applied -{extraRefund:F2}s => remaining={remaining:F2}.", this);
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
        float outgoingDamageMultiplier = ResolveRuneOutgoingDamageMultiplier();
        physicalDamage *= outgoingDamageMultiplier;
        specialDamage *= outgoingDamageMultiplier;

        float manaRuneMultiplier = ResolveManaRuneScaledMultiplier(0.5f);
        if (manaRuneMultiplier > 1f)
        {
            float beforePhysical = physicalDamage;
            float beforeSpecial = specialDamage;
            physicalDamage *= manaRuneMultiplier;
            specialDamage *= manaRuneMultiplier;
            LogManaRuneApplied("R", "ThrustPhysicalDamage", beforePhysical, physicalDamage);
            LogManaRuneApplied("R", "ThrustSpecialDamage", beforeSpecial, specialDamage);
        }
    }

    private void ResolveNeedleDamageValues(float physicalAttack, float specialAttack, out float physicalDamage, out float specialDamage)
    {
        ResolveThrustDamageValues(physicalAttack, specialAttack, out float thrustPhysicalDamage, out float thrustSpecialDamage);
        float multiplier = Mathf.Max(0f, needleDamageMultiplier);
        physicalDamage = thrustPhysicalDamage * multiplier;
        specialDamage = thrustSpecialDamage * multiplier;
    }

    private void ResolveActualNeedleDamageValues(
        float theoreticalPhysicalDamage,
        float theoreticalSpecialDamage,
        float actualMeleeDamageThisCast,
        float combatHealthAttackerMultiplier,
        out float physicalDamage,
        out float specialDamage)
    {
        float normalizedActualMeleeDamage = Mathf.Max(0f, actualMeleeDamageThisCast);
        float safeCombatHealthAttackerMultiplier = Mathf.Max(0.0001f, combatHealthAttackerMultiplier);
        normalizedActualMeleeDamage /= safeCombatHealthAttackerMultiplier;
        float projectileTotalDamage = normalizedActualMeleeDamage * Mathf.Max(0f, needleDamageMultiplier);
        float theoreticalTotalDamage = Mathf.Max(0f, theoreticalPhysicalDamage) + Mathf.Max(0f, theoreticalSpecialDamage);
        if (projectileTotalDamage <= 0f || theoreticalTotalDamage <= 0f)
        {
            physicalDamage = 0f;
            specialDamage = 0f;
            return;
        }

        float physicalRatio = Mathf.Max(0f, theoreticalPhysicalDamage) / theoreticalTotalDamage;
        physicalDamage = projectileTotalDamage * physicalRatio;
        specialDamage = projectileTotalDamage - physicalDamage;
    }

    private float ResolveNeedleCombatHealthAttackerMultiplier()
    {
        GameObject attacker = Controller != null ? Controller.gameObject : gameObject;
        float multiplier = BattleStatUtility.GetPlayerExcessMoveSpeedDamageMultiplier(attacker);
        CombatStats attackerStats = BattleStatUtility.GetCombatStats(attacker);
        if (attackerStats != null)
        {
            multiplier *= Mathf.Max(0f, attackerStats.outgoingDamageMultiplier);
        }

        if (DayNightAffinityDamageModifier.IsNightChildFavorableTime(attacker)
            || DayNightAffinityDamageModifier.IsDayChildFavorableTime(attacker))
        {
            multiplier *= 1.5f;
        }

        return Mathf.Max(1f, multiplier);
    }

    private float ResolveCurrentEffectiveHealth(CombatHealth health)
    {
        if (health == null)
        {
            return 0f;
        }

        return ResolveCurrentHealth(health) + Mathf.Max(0f, health.GetCurrentShield());
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
