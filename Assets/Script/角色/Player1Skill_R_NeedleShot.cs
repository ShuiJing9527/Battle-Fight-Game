using System.Collections;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Serialization;

public class Player1Skill_R_NeedleShot : Player01SkillBase
{
    [Header("R - Animation")]
    [SerializeField] private bool useSpineAnimationEvents;
    [SerializeField] private string thrustVfxEventName = "R_ThrustVfx";
    [SerializeField] private string thrustHitEventName = "R_ThrustHit";
    [SerializeField] private string needleStartEventName = "R_Needles";

    [Header("R - Timing")]
    [SerializeField, Min(0f)] private float thrustVfxStartTime = 0.15f;
    [SerializeField, Min(0f)] private float thrustHitTime = 0.22f;
    [SerializeField, Min(0f)] private float needleStartTime = 0.45f;
    [SerializeField, Min(0f)] private float needleInterval = 0.12f;
    [SerializeField, Min(0f)] private float skillEndTime = 1.25f;

    [Header("R - Thrust VFX")]
    [SerializeField] private Player01RThrustVfx thrustVfxPrefab;
    [SerializeField] private Transform thrustVfxAnchor;

    [Header("R - Thrust Damage")]
    [SerializeField, Min(0f)] private float thrustDamage = 40f;
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
    [SerializeField, Min(1)] private int needleCount = 4;
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
    [SerializeField, Min(0f)] private float needleDamage = 50f;
    [SerializeField, Min(0f)] private float needlePhysicalScaling = 0.25f;
    [SerializeField, Min(0f)] private float needleSpecialScaling = 1.1f;
    [FormerlySerializedAs("needleSpeed")]
    [SerializeField, Min(0.01f)] private float travelSpeed = 38f;
    [SerializeField, Min(0f)] private float passThroughDistance = 4.5f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.3f;
    [FormerlySerializedAs("healPercentOfDamage")]
    [SerializeField, Range(0f, 1f)] private float needleHealPercentOfDamage = 0.25f;
    [SerializeField] private LayerMask needleHitLayers = ~0;
    [SerializeField, Min(0.01f)] private float needleHitRadius = 0.3f;

    [Header("R - Targeting")]
    [SerializeField, Min(0.5f)] private float targetSearchRadius = 14f;
    [SerializeField] private LayerMask targetSearchLayers = ~0;
    [SerializeField, Min(0.5f)] private float fallbackTargetDistance = 6f;

    private readonly HashSet<CombatHealth> thrustDamagedTargets = new HashSet<CombatHealth>();
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

    private void Reset()
    {
        cooldown = 12f;
        duration = 1.25f;
        effectPower = 50f;
        animationName = "ATK01";
        debugLog = true;
        thrustVfxStartTime = 0.15f;
        thrustHitTime = 0.22f;
        needleStartTime = 0.45f;
        needleInterval = 0.12f;
        skillEndTime = 1.25f;
        thrustDamage = 40f;
        thrustRange = 2.4f;
        thrustWidth = 1.2f;
        thrustHeight = 1.4f;
        thrustForwardOffset = 1.2f;
        needleCount = 4;
        spawnRadiusMin = 3.5f;
        spawnRadiusMax = 5.5f;
        heightMin = 0.5f;
        heightMax = 2.2f;
        targetHeightOffset = 0.8f;
        horizontalRandomAngle = 18f;
        needleDamage = 50f;
        needlePhysicalScaling = 0.25f;
        needleSpecialScaling = 1.1f;
        travelSpeed = 38f;
        passThroughDistance = 4.5f;
        fadeDuration = 0.3f;
        needleHealPercentOfDamage = 0.25f;
        targetSearchRadius = 14f;
        fallbackTargetDistance = 6f;
    }

    private void Awake()
    {
        runeRuntimeState = ResolveRuneRuntimeState();
        SyncLayerMasks();
    }

    private void OnValidate()
    {
        duration = Mathf.Max(duration, skillEndTime);
        SyncLayerMasks();
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

    protected override void OnCastStarted()
    {
        runeRuntimeState = ResolveRuneRuntimeState();
        currentRuneCastId = runeRuntimeState != null ? runeRuntimeState.NotifySkillCastStarted(SkillIndex) : -1;
        thrustDamagedTargets.Clear();
        preferredNeedleTarget = null;
        thrustVfxTriggered = false;
        thrustHitTriggered = false;
        needlePhaseTriggered = false;
        ClearDestroyedNeedles();
        Controller?.SetMovementInputLocked(true, "Player01 R");
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

        CompleteCast();
    }

    protected override void OnCastFinished()
    {
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
        Player01RFourNeedleUtility.SpawnSettings settings = new Player01RFourNeedleUtility.SpawnSettings
        {
            needleCount = Mathf.Max(1, needleCount),
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
            ? new System.Random(System.Environment.TickCount ^ GetInstanceID())
            : new System.Random(randomSeed);

        Vector3[] spawnPositions = Player01RFourNeedleUtility.BuildSpawnPositions(
            targetPoint,
            Controller != null ? Controller.GetFacingWorldDirection() : transform.forward,
            settings,
            NextRange);

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
            gameObject,
            ResolveNeedleDamage(),
            needleHitLayers,
            SkillIndex,
            currentRuneCastId,
            needleHealPercentOfDamage,
            needleHitRadius,
            BattleDamageType.Special);

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
        float resolvedDamage = Mathf.Max(0f, thrustDamage);

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

            float appliedDamage = resolvedDamage + ConsumeRuneFirstHitBonusDamage();
            float beforeHealth = ResolveCurrentHealth(combatHealth);
            combatHealth.TakeDamage(new BattleDamage(appliedDamage, BattleDamageType.Physical, gameObject));
            float actualDamage = Mathf.Max(0f, beforeHealth - ResolveCurrentHealth(combatHealth));
            runeRuntimeState?.NotifyMonsterDamagedBySkill(SkillIndex, combatHealth, actualDamage);

            if (preferredNeedleTarget == null && actualDamage > 0f)
            {
                preferredNeedleTarget = combatHealth;
            }
        }
    }

    private Vector3 ResolveNeedleTargetPoint()
    {
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

    private float ResolveNeedleDamage()
    {
        return PlayerSkillDamageUtility.CalculateHybridSkillDamage(
            this,
            gameObject,
            needleDamage,
            needlePhysicalScaling,
            needleSpecialScaling,
            "Player01 R Needle");
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
        preferredNeedleTarget = null;
        currentRuneCastId = -1;
        thrustVfxTriggered = false;
        thrustHitTriggered = false;
        needlePhaseTriggered = false;
        castRoutine = null;
        Controller?.SetMovementInputLocked(false, "Player01 R");
        Controller?.ClearSkillAnimationLock();
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
