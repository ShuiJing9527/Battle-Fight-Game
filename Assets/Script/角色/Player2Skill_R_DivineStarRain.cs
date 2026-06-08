using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player2Skill_R_DivineStarRain : PlayerSkillBase
{
    [Header("R Base Sword Count")]
    [SerializeField] private int rBaseSwordCount = 1;

    [Header("R Effect Scale")]
    [SerializeField] private Vector3 rEffectScale = new Vector3(0.3f, 0.3f, 0.3f);
    [SerializeField] private float rEffectRotationZ = 0f;
    [SerializeField] private Vector3 rEffectOffset = Vector3.zero;
    [SerializeField] private Vector3 rEffectPlaneScale = new Vector3(0.3f, 0.3f, 1f);
    [SerializeField] private float rEffectYawOffset = 0f;
    [SerializeField] private float rEffectVisualPitch = 0f;
    [SerializeField] private float rEffectVisualYaw = 0f;
    [SerializeField] private float rEffectVisualRoll = 0f;

    [Header("R Swarm")]
    [SerializeField] private float rSwarmDuration = 2.0f;
    [SerializeField] private float rSwarmRadiusMin = 0.8f;
    [SerializeField] private float rSwarmRadiusMax = 3.2f;
    [SerializeField] private float rSwarmHeightMin = 0.4f;
    [SerializeField] private float rSwarmHeightMax = 3.0f;
    [SerializeField] private float rSwarmSpeedMin = 120f;
    [SerializeField] private float rSwarmSpeedMax = 300f;
    [SerializeField] private float rSwarmBobAmplitudeMin = 0.05f;
    [SerializeField] private float rSwarmBobAmplitudeMax = 0.35f;
    [SerializeField] private float rSwarmBobFrequencyMin = 0.8f;
    [SerializeField] private float rSwarmBobFrequencyMax = 2.5f;
    [SerializeField] private float rSwarmRadiusJitter = 0.25f;
    [SerializeField] private bool rSwarmClockwise = true;
    [SerializeField] private float rSwarmForwardOffset = 2.0f;
    [SerializeField] private float rSwarmYawOffset = 0f;
    [SerializeField] private Camera rRenderCamera;
    [SerializeField] private bool rAutoResolveRenderCamera = true;
    [SerializeField] private bool rSwarmUseCameraForward = true;
    [SerializeField] private bool rSwarmCenterOnPlayer = false;
    [SerializeField] private bool rApplyEffectOffsetToSwarmCenter = false;
    [SerializeField] private bool rUseTangentFacing = true;
    [SerializeField] private Vector3 rPlaneUprightEuler = Vector3.zero;
    [SerializeField] private Vector3 rPlaneFaceCameraEuler = Vector3.zero;
    [SerializeField] private bool rFlipPlaneFrontBack = true;
    [SerializeField] private Vector3 rPlaneFrontBackFlipEuler = new Vector3(0f, 180f, 0f);
    [SerializeField] private bool rUsePlayerLayerForR = true;
    [SerializeField] private bool rForceDoubleSided = true;
    [SerializeField] private bool rDebugSwordVelocityFacing = false;
    [SerializeField] private float rFacingLookAheadTime = 0.05f;
    [SerializeField] private bool rDebugFacingScreenAngle = false;
    [SerializeField] private bool rEnableSwordSelfSpin = false;
    [SerializeField] private float rSwordSelfSpinMin = 30f;
    [SerializeField] private float rSwordSelfSpinMax = 120f;
    [SerializeField] private Vector3 rSwordLengthLocalAxis = Vector3.up;

    [Header("R Swarm Damage")]
    [SerializeField] private float rSwarmDamageRadius = 3.0f;
    [SerializeField] private float rSwarmDamageInterval = 0.25f;
    [SerializeField] private float rSwarmDamagePerTick = 2.0f;
    [SerializeField] private LayerMask rSwarmEnemyLayer = ~0;

    [Header("R Star Rain")]
    [SerializeField] private float rStarRainStartRatio = 0.5f;
    [SerializeField] private float rStarRainInterval = 0.12f;
    [SerializeField] private int rStarRainBladesPerWave = 2;
    [SerializeField] private float rStarRainSpawnHeight = 5f;
    [SerializeField] private float rStarRainRadius = 5f;
    [SerializeField] private float rStarRainFallSpeed = 10f;
    [SerializeField] private float rStarRainRandomDelay = 0.15f;
    [SerializeField] private float rStarRainDamageRadius = 1.2f;
    [SerializeField] private float rStarRainDamageMultiplier = 0.6f;
    [SerializeField] private bool rStarRainContinueAfterOrbit = true;
    [SerializeField] private float rStarRainExtraDurationAfterOrbit = 0.6f;
    [SerializeField] private Vector3 rStarRainEffectScale = new Vector3(0.3f, 0.3f, 0.3f);
    [SerializeField] private bool rStarRainUseForcedVisualRotation = true;
    [SerializeField] private Vector3 rStarRainForcedVisualEuler = new Vector3(0f, 180f, 0f);
    [SerializeField] private Vector3 rStarRainVisualEulerOffset = Vector3.zero;

    [Header("R Orbit Cleanup")]
    [SerializeField] private bool rOrbitClearWhenOrbitEnds = true;
    [SerializeField] private float rOrbitFadeOutDuration = 0.15f;

    [Header("R Effect Prefabs")]
    [SerializeField] private GameObject sharedSkillEffectPrefab;
    [SerializeField] private GameObject rSkillEffectPrefab;

    private sealed class SkillEffectRuntime : MonoBehaviour
    {
        public Transform visual;
        public Vector3 baseVisualScale;
        public Quaternion baseVisualRotation = Quaternion.identity;
        public Material[] materialTargets;
        public Color[] materialBaseColors;
        public SpriteRenderer[] spriteTargets;
        public Color[] spriteBaseColors;
    }

    private sealed class RSwarmSwordData
    {
        public GameObject sword;
        public float baseAngle;
        public float radius;
        public float height;
        public float orbitSpeed;
        public float bobAmplitude;
        public float bobFrequency;
        public float phase;
        public float layerOffset;
        public SkillEffectRuntime runtime;
        public Transform visualTransform;
        public Quaternion baseVisibleLocalRotation;
        public float selfSpinSpeed;
        public float selfSpinAngle;
        public Vector3 previousPosition;
        public bool hasPreviousPosition;
        public Vector3 orbitEndPosition;
        public Vector3 riseEndPosition;
        public Vector3 rainStartPosition;
        public Vector3 rainTargetPosition;
        public float rainDelay;
        public float rainFallDuration;
        public bool rainImpactApplied;
    }

    private sealed class RStarRainBladeData
    {
        public GameObject sword;
        public SkillEffectRuntime runtime;
        public Transform visualTransform;
        public Quaternion baseVisibleLocalRotation;
        public Vector3 spawnPosition;
        public Vector3 targetPosition;
        public float delay;
        public float fallDuration;
        public float elapsed;
        public bool impactApplied;
    }

    private float lastMoveDirYawFallback = 0f;
    private Coroutine rSwarmRoutine;
    private GameObject activeRSwarmRoot;
    private readonly List<RSwarmSwordData> activeRSwarmSwords = new List<RSwarmSwordData>();
    private readonly List<RStarRainBladeData> activeRStarRainBlades = new List<RStarRainBladeData>();
    private Camera resolvedRRenderCamera;

    public override void Initialize(Player2PrototypeController owner)
    {
        base.Initialize(owner);
        SyncLegacyOwnerValuesIfNeeded();
    }

    public override void Cast()
    {
        if (Owner == null)
        {
            return;
        }

        if (rSwarmRoutine != null)
        {
            StopCoroutine(rSwarmRoutine);
            rSwarmRoutine = null;
        }

        Cleanup();
        CastInternal();
    }

    public override void Cleanup()
    {
        if (rSwarmRoutine != null)
        {
            StopCoroutine(rSwarmRoutine);
            rSwarmRoutine = null;
        }

        CleanupRSwarmVisuals();
        CleanupRStarRainVisuals();
        if (activeRSwarmRoot != null)
        {
            Destroy(activeRSwarmRoot);
            activeRSwarmRoot = null;
        }
    }

    private void OnDisable()
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void CastInternal()
    {
        int energyForR = Mathf.Max(0, Owner != null ? Owner.currentSwordEnergy : 0);
        int count = Mathf.Max(0, rBaseSwordCount) + energyForR;
        if (count <= 0)
        {
            return;
        }

        Camera renderCamera = ResolveRRenderCamera();
        Vector3 previewCenter = ResolveRSwarmCenter();
        Debug.Log($"[R Skill] BaseSwordCount={rBaseSwordCount}, CurrentSwordEnergy={energyForR}, Spawned={count}, RenderCamera={(renderCamera != null ? renderCamera.name : "null")}, Center={previewCenter}", this);
        if (Owner != null)
        {
            Owner.currentSwordEnergy = 0;
        }

        rSwarmRoutine = StartCoroutine(RSwarmRoutine(count));
    }

    private IEnumerator RSwarmRoutine(int count)
    {
        Vector3 center = ResolveRSwarmCenter();
        GameObject swarmRoot = new GameObject("R_SwarmVisualRoot");
        swarmRoot.transform.position = center;
        swarmRoot.transform.rotation = Quaternion.identity;
        activeRSwarmRoot = swarmRoot;
        activeRSwarmSwords.Clear();

        int swordCount = Mathf.Max(0, count);
        for (int i = 0; i < swordCount; i++)
        {
            float baseAngle = i * (360f / Mathf.Max(1, swordCount)) + Random.Range(-30f, 30f);
            float radiusMin = Mathf.Min(rSwarmRadiusMin, rSwarmRadiusMax);
            float radiusMax = Mathf.Max(rSwarmRadiusMin, rSwarmRadiusMax);
            float radiusT = swordCount <= 1 ? 0.5f : i / (float)(swordCount - 1);
            float radius = Mathf.Lerp(radiusMin, radiusMax, radiusT);
            radius += Random.Range(-rSwarmRadiusJitter, rSwarmRadiusJitter);
            radius = Mathf.Max(0.01f, radius);
            float height = Random.Range(Mathf.Min(rSwarmHeightMin, rSwarmHeightMax), Mathf.Max(rSwarmHeightMin, rSwarmHeightMax));
            float orbitSpeed = Random.Range(Mathf.Min(rSwarmSpeedMin, rSwarmSpeedMax), Mathf.Max(rSwarmSpeedMin, rSwarmSpeedMax));
            float bobAmplitude = Random.Range(Mathf.Min(rSwarmBobAmplitudeMin, rSwarmBobAmplitudeMax), Mathf.Max(rSwarmBobAmplitudeMin, rSwarmBobAmplitudeMax));
            float bobFrequency = Random.Range(Mathf.Min(rSwarmBobFrequencyMin, rSwarmBobFrequencyMax), Mathf.Max(rSwarmBobFrequencyMin, rSwarmBobFrequencyMax));
            float phase = Random.Range(0f, Mathf.PI * 2f);

            Vector3 spawnOffset = GetOrbitPositionXZ(baseAngle, radius, height);
            Vector3 spawnPosition = center + spawnOffset;

            GameObject sword = CreateSkillEffectVisual(
                $"R_SwarmSword_{i}",
                ResolveRSkillEffectPrefab(),
                spawnPosition,
                spawnOffset,
                false,
                false,
                0f,
                rEffectVisualPitch,
                rEffectVisualYaw,
                rEffectVisualRoll + ResolveRotation(rEffectRotationZ),
                Vector3.one);

            if (sword == null)
            {
                continue;
            }

            if (rUsePlayerLayerForR)
            {
                SetLayerRecursively(sword, Owner != null ? Owner.gameObject.layer : gameObject.layer);
            }

            if (rForceDoubleSided)
            {
                TrySetDoubleSidedIfSupported(sword);
            }

            EnsureEffectVisible(sword);
            sword.transform.SetParent(swarmRoot.transform, true);
            ApplyRootVisualScale(sword, ResolveVisualScaleWithoutShared(rEffectScale));
            SkillEffectRuntime runtime = sword.GetComponent<SkillEffectRuntime>();
            Transform visualTransform = runtime != null && runtime.visual != null ? runtime.visual : null;
            Quaternion baseVisualLocalRotation = visualTransform != null ? visualTransform.localRotation : Quaternion.identity;

            activeRSwarmSwords.Add(new RSwarmSwordData
            {
                sword = sword,
                baseAngle = baseAngle,
                radius = radius,
                height = height,
                orbitSpeed = orbitSpeed,
                bobAmplitude = bobAmplitude,
                bobFrequency = bobFrequency,
                phase = phase,
                layerOffset = Random.Range(-0.25f, 0.25f),
                runtime = runtime,
                visualTransform = visualTransform,
                baseVisibleLocalRotation = baseVisualLocalRotation,
                selfSpinSpeed = 0f,
                selfSpinAngle = 0f,
                previousPosition = spawnPosition,
                hasPreviousPosition = true
            });
        }

        if (rDebugFacingScreenAngle)
        {
            Debug.Log($"R Swarm radius range: min={rSwarmRadiusMin}, max={rSwarmRadiusMax}, actualRadiusCount={activeRSwarmSwords.Count}", this);
            for (int i = 0; i < activeRSwarmSwords.Count; i++)
            {
                RSwarmSwordData data = activeRSwarmSwords[i];
                if (data != null)
                {
                    Debug.Log($"R Swarm sword[{i}] radius={data.radius}", this);
                }
            }
        }

        float orbitElapsed = 0f;
        float damageTickTimer = 0f;
        float orbitDuration = Mathf.Max(0.05f, rSwarmDuration);
        float safeDamageInterval = Mathf.Max(0.05f, rSwarmDamageInterval);
        float rainStartTime = Mathf.Clamp01(rStarRainStartRatio) * orbitDuration;
        float rainSpawnEndTime = orbitDuration;
        if (rStarRainContinueAfterOrbit)
        {
            rainSpawnEndTime += Mathf.Max(0f, rStarRainExtraDurationAfterOrbit);
        }
        float rainSpawnInterval = Mathf.Max(0.01f, rStarRainInterval);
        float rainSpawnAccumulator = 0f;
        float totalElapsed = 0f;
        bool orbitCleared = false;

        while (totalElapsed < rainSpawnEndTime || HasAliveRStarRainBlades())
        {
            center = ResolveRSwarmCenter();
            if (!orbitCleared && swarmRoot != null)
            {
                swarmRoot.transform.position = center;
            }

            if (orbitElapsed < orbitDuration)
            {
                float dirSign = rSwarmClockwise ? -1f : 1f;
                for (int i = 0; i < activeRSwarmSwords.Count; i++)
                {
                    RSwarmSwordData data = activeRSwarmSwords[i];
                    if (data == null || data.sword == null)
                    {
                        continue;
                    }

                    float angle = data.baseAngle + dirSign * data.orbitSpeed * orbitElapsed;
                    float rad = angle * Mathf.Deg2Rad;
                    float dynamicRadius = data.radius + Mathf.Sin(orbitElapsed * 1.7f + data.phase) * rSwarmRadiusJitter;
                    float dynamicHeight = data.height + data.layerOffset + Mathf.Sin(orbitElapsed * data.bobFrequency + data.phase) * data.bobAmplitude;

                    Vector3 offset = new Vector3(
                        Mathf.Cos(rad) * dynamicRadius,
                        dynamicHeight,
                        Mathf.Sin(rad) * dynamicRadius);
                    Vector3 currentPosition = center + offset;
                    data.sword.transform.position = currentPosition;
                    data.orbitEndPosition = currentPosition;

                    Vector3 tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                    if (rSwarmClockwise)
                    {
                        tangent = -tangent;
                    }

                    if (data.visualTransform != null)
                    {
                        float yaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg + rSwarmYawOffset;
                        ApplyRSwarmVisualRotation(data, yaw);
                    }
                }

                damageTickTimer -= Time.deltaTime;
                if (damageTickTimer <= 0f)
                {
                    ApplyRSwarmTickDamage(center);
                    damageTickTimer += safeDamageInterval;
                }
                orbitElapsed = Mathf.Min(orbitElapsed + Time.deltaTime, orbitDuration);
            }

            if (!orbitCleared && orbitElapsed >= orbitDuration)
            {
                orbitCleared = true;
                if (rOrbitClearWhenOrbitEnds)
                {
                    CleanupRSwarmOrbitVisuals();
                }
            }

            if (totalElapsed >= rainStartTime && totalElapsed <= rainSpawnEndTime)
            {
                rainSpawnAccumulator += Time.deltaTime;
                while (rainSpawnAccumulator >= rainSpawnInterval)
                {
                    SpawnRStarRainWave(center);
                    rainSpawnAccumulator -= rainSpawnInterval;
                }
            }

            UpdateRStarRainBlades(Time.deltaTime);

            totalElapsed += Time.deltaTime;

            if (totalElapsed >= rainSpawnEndTime && !HasAliveRStarRainBlades())
            {
                break;
            }

            yield return null;
        }

        CleanupRSwarmVisuals();
        rSwarmRoutine = null;
    }

    private bool HasAliveRStarRainBlades()
    {
        for (int i = 0; i < activeRStarRainBlades.Count; i++)
        {
            RStarRainBladeData data = activeRStarRainBlades[i];
            if (data != null && data.sword != null)
            {
                return true;
            }
        }

        return false;
    }

    private void SpawnRStarRainWave(Vector3 center)
    {
        int waveCount = Mathf.Max(1, rStarRainBladesPerWave);
        float rainSpawnHeight = Mathf.Max(0f, rStarRainSpawnHeight);
        float rainRadius = Mathf.Max(0f, rStarRainRadius);
        float rainRandomDelay = Mathf.Max(0f, rStarRainRandomDelay);
        float rainFallSpeed = Mathf.Max(0.1f, rStarRainFallSpeed);

        for (int i = 0; i < waveCount; i++)
        {
            Vector2 randomOffset2D = Random.insideUnitCircle * rainRadius;
            Vector3 target = center + new Vector3(randomOffset2D.x, 0f, randomOffset2D.y);
            Vector3 spawn = target + Vector3.up * rainSpawnHeight;

            GameObject sword = CreateSkillEffectVisual(
                $"R_StarRain_{activeRStarRainBlades.Count}",
                ResolveRSkillEffectPrefab(),
                spawn,
                Vector3.down,
                false,
                false,
                0f,
                0f,
                0f,
                0f,
                Vector3.one);

            if (sword == null)
            {
                continue;
            }

            if (rUsePlayerLayerForR)
            {
                SetLayerRecursively(sword, Owner != null ? Owner.gameObject.layer : gameObject.layer);
            }

            if (rForceDoubleSided)
            {
                TrySetDoubleSidedIfSupported(sword);
            }

            EnsureEffectVisible(sword);
            ApplyRootVisualScale(sword, ResolveVisualScaleWithoutShared(rStarRainEffectScale));
            SkillEffectRuntime runtime = sword.GetComponent<SkillEffectRuntime>();
            Transform visualTransform = runtime != null && runtime.visual != null ? runtime.visual : FindEffectVisualTransform(sword);

            RStarRainBladeData data = new RStarRainBladeData
            {
                sword = sword,
                runtime = runtime,
                visualTransform = visualTransform,
                baseVisibleLocalRotation = visualTransform != null ? visualTransform.localRotation : Quaternion.identity,
                spawnPosition = spawn,
                targetPosition = target,
                delay = Random.Range(0f, rainRandomDelay),
                fallDuration = Mathf.Max(0.05f, Vector3.Distance(spawn, target) / rainFallSpeed),
                elapsed = 0f,
                impactApplied = false
            };

            activeRStarRainBlades.Add(data);
            ApplyRStarRainVisualRotation(data);
        }
    }

    private void UpdateRStarRainBlades(float deltaTime)
    {
        for (int i = activeRStarRainBlades.Count - 1; i >= 0; i--)
        {
            RStarRainBladeData data = activeRStarRainBlades[i];
            if (data == null || data.sword == null)
            {
                activeRStarRainBlades.RemoveAt(i);
                continue;
            }

            data.elapsed += deltaTime;
            if (data.elapsed < data.delay)
            {
                data.sword.transform.position = data.spawnPosition;
                continue;
            }

            float fallT = Mathf.Clamp01((data.elapsed - data.delay) / Mathf.Max(0.05f, data.fallDuration));
            float smoothFallT = Mathf.SmoothStep(0f, 1f, fallT);
            data.sword.transform.position = Vector3.Lerp(data.spawnPosition, data.targetPosition, smoothFallT);
            ApplyRStarRainVisualRotation(data);

            if (fallT >= 1f && !data.impactApplied)
            {
                data.impactApplied = true;
                ApplyRSwarmImpactDamage(data.targetPosition);
                Destroy(data.sword);
                activeRStarRainBlades.RemoveAt(i);
            }
        }
    }

    private void CleanupRSwarmVisuals()
    {
        for (int i = 0; i < activeRSwarmSwords.Count; i++)
        {
            RSwarmSwordData data = activeRSwarmSwords[i];
            if (data != null && data.sword != null)
            {
                Destroy(data.sword);
            }
        }
        activeRSwarmSwords.Clear();

        if (activeRSwarmRoot != null)
        {
            Destroy(activeRSwarmRoot);
            activeRSwarmRoot = null;
        }
    }

    private void CleanupRSwarmOrbitVisuals()
    {
        GameObject orbitRoot = activeRSwarmRoot;
        for (int i = 0; i < activeRSwarmSwords.Count; i++)
        {
            RSwarmSwordData data = activeRSwarmSwords[i];
            if (data != null && data.sword != null)
            {
                if (rOrbitFadeOutDuration > 0f)
                {
                    StartCoroutine(FadeAndDestroy(data.sword, rOrbitFadeOutDuration));
                }
                else
                {
                    Destroy(data.sword);
                }
            }
        }

        activeRSwarmSwords.Clear();
        activeRSwarmRoot = null;

        if (orbitRoot != null)
        {
            if (rOrbitFadeOutDuration > 0f)
            {
                StartCoroutine(DestroyAfterDelay(orbitRoot, rOrbitFadeOutDuration));
            }
            else
            {
                Destroy(orbitRoot);
            }
        }
    }

    private void CleanupRStarRainVisuals()
    {
        for (int i = 0; i < activeRStarRainBlades.Count; i++)
        {
            RStarRainBladeData data = activeRStarRainBlades[i];
            if (data != null && data.sword != null)
            {
                Destroy(data.sword);
            }
        }
        activeRStarRainBlades.Clear();
    }

    private IEnumerator DestroyAfterDelay(GameObject target, float delay)
    {
        if (target == null) yield break;
        yield return new WaitForSeconds(Mathf.Max(0f, delay));
        if (target != null) Destroy(target);
    }

    private IEnumerator FadeAndDestroy(GameObject target, float duration)
    {
        if (target == null) yield break;

        duration = Mathf.Max(0.01f, duration);
        SkillEffectRuntime runtime = target.GetComponent<SkillEffectRuntime>();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (target == null) yield break;
            float alpha = 1f - (elapsed / duration);
            ApplyFadeAlpha(runtime, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (target != null)
        {
            ApplyFadeAlpha(runtime, 0f);
            Destroy(target);
        }
    }

    private void ApplyRSwarmTickDamage(Vector3 center)
    {
        ApplyRSwarmAreaDamage(center, rSwarmDamageRadius, rSwarmDamagePerTick);
    }

    private void ApplyRSwarmImpactDamage(Vector3 center)
    {
        ApplyRSwarmAreaDamage(center, Mathf.Max(0.01f, rStarRainDamageRadius), rSwarmDamagePerTick * Mathf.Max(0f, rStarRainDamageMultiplier));
    }

    private void ApplyRSwarmAreaDamage(Vector3 center, float radius, float damageAmount)
    {
        if (damageAmount <= 0f || radius <= 0f)
        {
            return;
        }

        Collider[] hits = Physics.OverlapSphere(center, radius, rSwarmEnemyLayer);
        HashSet<GameObject> damagedRoots = new HashSet<GameObject>();
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            Transform targetRoot = hit.transform.root;
            if (targetRoot == null || (Owner != null && targetRoot.gameObject == Owner.gameObject) || !damagedRoots.Add(targetRoot.gameObject))
            {
                continue;
            }

            CombatHealth combatHealth = targetRoot.GetComponentInParent<CombatHealth>();
            if (combatHealth != null && (Owner == null || combatHealth.gameObject != Owner.gameObject))
            {
                combatHealth.TakeDamage(new BattleDamage(damageAmount, BattleDamageType.Physical, Owner != null ? Owner.gameObject : gameObject));
                continue;
            }

            EnemyHealth enemyHealth = targetRoot.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null && (Owner == null || enemyHealth.gameObject != Owner.gameObject))
            {
                int damageInt = Mathf.Max(1, Mathf.RoundToInt(damageAmount));
                enemyHealth.TakeDamage(damageInt, Owner != null ? Owner.gameObject : gameObject);
            }
        }
    }

    private GameObject ResolveRSkillEffectPrefab()
    {
        if (rSkillEffectPrefab != null)
        {
            return rSkillEffectPrefab;
        }

        if (Owner != null && Owner.rSkillEffectPrefab != null)
        {
            return Owner.rSkillEffectPrefab;
        }

        if (sharedSkillEffectPrefab != null)
        {
            return sharedSkillEffectPrefab;
        }

        return Owner != null ? Owner.sharedSkillEffectPrefab : null;
    }

    private bool ResolveUseRawPrefabRotationForSkillEffects()
    {
        return Owner == null || Owner.useRawPrefabRotationForSkillEffects;
    }

    private float ResolveSkillEffectPrefabScaleMultiplier()
    {
        return Owner != null ? Owner.skillEffectPrefabScaleMultiplier : 1f;
    }

    private static Vector3 ClampVisualScale(Vector3 scale)
    {
        return new Vector3(
            ClampScaleAxis(scale.x),
            ClampScaleAxis(scale.y),
            ClampScaleAxis(scale.z));
    }

    private static float ClampScaleAxis(float value)
    {
        const float minAbs = 0.01f;
        if (Mathf.Abs(value) >= minAbs)
        {
            return value;
        }

        return value < 0f ? -minAbs : minAbs;
    }

    private GameObject CreateSkillEffectVisual(
        string name,
        GameObject specificPrefab,
        Vector3 worldPosition,
        Vector3 direction,
        bool alignToDirection,
        bool invertForward,
        float yawOffset,
        float visualPitch,
        float visualYaw,
        float visualRoll,
        Vector3 visualScale)
    {
        GameObject root = new GameObject(name);
        root.transform.position = worldPosition;
        ApplyRootDirection(root.transform, direction, alignToDirection, invertForward, yawOffset);

        GameObject effectVisual = CreateEffectInstance(name, specificPrefab, root.transform.position, root.transform.rotation, ResolveUseRawPrefabRotationForSkillEffects());
        if (effectVisual == null)
        {
            Destroy(root);
            return null;
        }

        effectVisual.transform.SetParent(root.transform, true);

        Transform visualTarget = FindEffectVisualTransform(effectVisual);
        if (ResolveUseRawPrefabRotationForSkillEffects())
        {
            effectVisual.transform.rotation = root.transform.rotation;
            float rawScaleMultiplier = Mathf.Max(0.01f, ResolveSkillEffectPrefabScaleMultiplier());
            effectVisual.transform.localScale = effectVisual.transform.localScale * rawScaleMultiplier;
        }
        else
        {
            visualTarget.localRotation = BuildQuadOffsetRotation(visualPitch, visualYaw, visualRoll);
            visualTarget.localScale = Vector3.Scale(visualTarget.localScale, ClampVisualScale(visualScale));
        }
        EnsureEffectVisible(effectVisual);

        SkillEffectRuntime runtime = root.AddComponent<SkillEffectRuntime>();
        runtime.visual = visualTarget;
        runtime.baseVisualScale = visualTarget.localScale;
        CacheFadeTargets(effectVisual, runtime);

        return root;
    }

    private GameObject CreateEffectInstance(string effectName, GameObject specificPrefab, Vector3 position, Quaternion rotation, bool preservePrefabRotation)
    {
        GameObject sourcePrefab = specificPrefab != null ? specificPrefab : ResolveRSkillEffectPrefab();
        if (sourcePrefab != null)
        {
            GameObject instance;
            if (preservePrefabRotation)
            {
                instance = Instantiate(sourcePrefab);
                instance.transform.position = position;
            }
            else
            {
                instance = Instantiate(sourcePrefab, position, rotation);
            }

            return instance;
        }

        Debug.LogWarning($"[Player2Skill_R_DivineStarRain] Missing skill effect prefab for '{effectName}' on {name}. Assign specific prefab or Shared Skill Effect Prefab.", this);
        return null;
    }

    private static Transform FindEffectVisualTransform(GameObject root)
    {
        MeshRenderer rootMesh = root.GetComponent<MeshRenderer>();
        if (rootMesh != null) return root.transform;

        MeshRenderer childMesh = root.GetComponentInChildren<MeshRenderer>(true);
        if (childMesh != null) return childMesh.transform;

        SpriteRenderer rootSprite = root.GetComponent<SpriteRenderer>();
        if (rootSprite != null) return root.transform;

        SpriteRenderer childSprite = root.GetComponentInChildren<SpriteRenderer>(true);
        if (childSprite != null) return childSprite.transform;

        return root.transform;
    }

    private void CacheFadeTargets(GameObject effectVisualRoot, SkillEffectRuntime runtime)
    {
        List<Material> mats = new List<Material>();
        List<Color> matColors = new List<Color>();

        Renderer[] renderers = effectVisualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] instanceMats = renderers[i].materials;
            for (int m = 0; m < instanceMats.Length; m++)
            {
                Material mat = instanceMats[m];
                if (mat == null) continue;
                mats.Add(mat);
                matColors.Add(GetMaterialColor(mat));
            }
        }

        runtime.materialTargets = mats.ToArray();
        runtime.materialBaseColors = matColors.ToArray();

        runtime.spriteTargets = effectVisualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        runtime.spriteBaseColors = new Color[runtime.spriteTargets.Length];
        for (int i = 0; i < runtime.spriteTargets.Length; i++)
        {
            runtime.spriteBaseColors[i] = runtime.spriteTargets[i].color;
        }
    }

    private static void ApplyFadeAlpha(SkillEffectRuntime runtime, float alpha)
    {
        if (runtime == null)
        {
            return;
        }

        if (runtime.materialTargets != null)
        {
            for (int i = 0; i < runtime.materialTargets.Length; i++)
            {
                Material mat = runtime.materialTargets[i];
                if (mat == null) continue;
                Color baseColor = i < runtime.materialBaseColors.Length ? runtime.materialBaseColors[i] : Color.white;
                SetMaterialColor(mat, new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha));
            }
        }

        if (runtime.spriteTargets != null)
        {
            for (int i = 0; i < runtime.spriteTargets.Length; i++)
            {
                SpriteRenderer sr = runtime.spriteTargets[i];
                if (sr == null) continue;
                Color baseColor = i < runtime.spriteBaseColors.Length ? runtime.spriteBaseColors[i] : Color.white;
                sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
            }
        }
    }

    private static Color GetMaterialColor(Material mat)
    {
        if (mat == null) return Color.white;
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
        if (mat.HasProperty("_TintColor")) return mat.GetColor("_TintColor");
        return Color.white;
    }

    private static void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", color);
    }

    private static Quaternion BuildQuadOffsetRotation(float pitch, float yaw, float roll)
    {
        return Quaternion.Euler(NormalizeQuadLegacyPitch(pitch), yaw, roll);
    }

    private static float NormalizeQuadLegacyPitch(float pitch)
    {
        float absPitch = Mathf.Abs(pitch);
        if (absPitch < 0.0001f)
        {
            return 0f;
        }

        if (Mathf.Abs(absPitch - 90f) <= 0.01f || Mathf.Abs(absPitch - 180f) <= 0.01f)
        {
            return 0f;
        }

        return pitch;
    }

    private static float NormalizeQuadLegacyRoll(float roll)
    {
        float absRoll = Mathf.Abs(roll);
        if (absRoll < 0.0001f)
        {
            return 0f;
        }

        if (Mathf.Abs(absRoll - 90f) <= 0.01f)
        {
            return 0f;
        }

        return roll;
    }

    private static void ApplyRootDirection(Transform root, Vector3 direction, bool alignToDirection, bool invertForward, float yawOffset)
    {
        float yaw = 0f;
        if (alignToDirection && direction.sqrMagnitude > 0.0001f)
        {
            yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        if (invertForward)
        {
            yaw += 180f;
        }

        root.rotation = Quaternion.Euler(0f, yaw + yawOffset, 0f);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
        {
            return;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            all[i].gameObject.layer = layer;
        }
    }

    private static void EnsureEffectVisible(GameObject effectRoot)
    {
        if (effectRoot == null)
        {
            return;
        }

        effectRoot.SetActive(true);
        Renderer[] renderers = effectRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
        }
    }

    private static void TrySetDoubleSidedIfSupported(GameObject effectRoot)
    {
        if (effectRoot == null)
        {
            return;
        }

        Renderer[] renderers = effectRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null || !mat.HasProperty("_Cull"))
                {
                    continue;
                }

                mat.SetFloat("_Cull", 0f);
            }
        }
    }

    private Vector3 ResolveRSwarmForward()
    {
        Vector3 forward = Vector3.forward;
        Camera renderCamera = ResolveRRenderCamera();
        if (rSwarmUseCameraForward && renderCamera != null)
        {
            forward = renderCamera.transform.forward;
        }
        else if (Owner != null)
        {
            forward = Owner.transform.forward;
        }
        else
        {
            forward = transform.forward;
        }

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Owner != null ? Owner.FacingDirection : transform.forward;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        return forward.normalized;
    }

    private Vector3 ResolveRSwarmCenter()
    {
        Vector3 center = Owner != null ? Owner.transform.position : transform.position;
        if (rApplyEffectOffsetToSwarmCenter)
        {
            center += rEffectOffset;
        }

        if (!rSwarmCenterOnPlayer)
        {
            center += ResolveRSwarmForward() * rSwarmForwardOffset;
        }

        return center;
    }

    private Camera ResolveRRenderCamera()
    {
        if (rRenderCamera != null && rRenderCamera.isActiveAndEnabled)
        {
            resolvedRRenderCamera = rRenderCamera;
            return resolvedRRenderCamera;
        }

        if (!rAutoResolveRenderCamera)
        {
            return resolvedRRenderCamera;
        }

        if (resolvedRRenderCamera != null && resolvedRRenderCamera.isActiveAndEnabled)
        {
            return resolvedRRenderCamera;
        }

        PlayerCameraRig cameraRig = FindObjectOfType<PlayerCameraRig>();
        if (cameraRig != null)
        {
            Camera rigCamera = cameraRig.GetComponent<Camera>();
            if (rigCamera == null)
            {
                rigCamera = cameraRig.GetComponentInChildren<Camera>(true);
            }

            if (rigCamera != null && rigCamera.isActiveAndEnabled)
            {
                resolvedRRenderCamera = rigCamera;
                return resolvedRRenderCamera;
            }
        }

        if (Camera.main != null && Camera.main.isActiveAndEnabled)
        {
            resolvedRRenderCamera = Camera.main;
            return resolvedRRenderCamera;
        }

        Camera[] allCameras = Camera.allCameras;
        for (int i = 0; i < allCameras.Length; i++)
        {
            Camera cam = allCameras[i];
            if (cam != null && cam.isActiveAndEnabled && cam.cameraType == CameraType.Game)
            {
                resolvedRRenderCamera = cam;
                return resolvedRRenderCamera;
            }
        }

        return resolvedRRenderCamera;
    }

    private Vector3 ResolveVisualScale(Vector3 specificScale, Vector3 planeScale)
    {
        Vector3 baseScale = Owner != null ? Owner.sharedEffectScale : Vector3.one;
        Vector3 roleScale = specificScale.sqrMagnitude > 0.0001f ? specificScale : Vector3.one;
        Vector3 quadScale = planeScale.sqrMagnitude > 0.0001f ? planeScale : Vector3.one;
        return new Vector3(
            baseScale.x * roleScale.x * quadScale.x,
            baseScale.y * roleScale.y * quadScale.y,
            baseScale.z * roleScale.z * quadScale.z);
    }

    private Vector3 ResolveVisualScaleWithoutShared(Vector3 specificScale)
    {
        Vector3 roleScale = specificScale.sqrMagnitude > 0.0001f ? specificScale : Vector3.one;
        return ClampVisualScale(roleScale);
    }

    private static void ApplyRootVisualScale(GameObject effectRoot, Vector3 visualScale)
    {
        if (effectRoot == null)
        {
            return;
        }

        effectRoot.transform.localScale = ClampVisualScale(visualScale);
    }

    private float ResolveRotation(float specificRotationZ)
    {
        float sharedEffectRotationZ = Owner != null ? Owner.sharedEffectRotationZ : 0f;
        return sharedEffectRotationZ + NormalizeQuadLegacyRoll(specificRotationZ);
    }

    private static Vector3 GetOrbitPositionXZ(float angleDegrees, float radius, float height)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(
            Mathf.Cos(rad) * radius,
            height,
            Mathf.Sin(rad) * radius);
    }

    private static float GetOrbitTangentYawXZ(float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        Vector3 tangent = new Vector3(
            -Mathf.Sin(rad),
            0f,
            Mathf.Cos(rad));
        return Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
    }

    private void ApplyRSwarmVisualRotation(RSwarmSwordData data, float yaw)
    {
        if (data == null || data.visualTransform == null)
        {
            return;
        }

        data.visualTransform.rotation = Quaternion.Euler(0f, yaw, 0f) * BuildRVisibleBaseRotation();
    }

    private Quaternion BuildRVisibleBaseRotation()
    {
        Quaternion frontBackFlip = rFlipPlaneFrontBack ? Quaternion.Euler(0f, rPlaneFrontBackFlipEuler.y, 0f) : Quaternion.identity;
        return frontBackFlip * BuildQuadOffsetRotation(rEffectVisualPitch, rEffectVisualYaw, rEffectVisualRoll);
    }

    private Quaternion BuildRStarRainVisibleRotation()
    {
        if (rStarRainUseForcedVisualRotation)
        {
            return BuildQuadOffsetRotation(rStarRainForcedVisualEuler.x, rStarRainForcedVisualEuler.y, rStarRainForcedVisualEuler.z) *
                   BuildQuadOffsetRotation(rStarRainVisualEulerOffset.x, rStarRainVisualEulerOffset.y, rStarRainVisualEulerOffset.z);
        }

        return BuildRVisibleBaseRotation() * BuildQuadOffsetRotation(rStarRainVisualEulerOffset.x, rStarRainVisualEulerOffset.y, rStarRainVisualEulerOffset.z);
    }

    private void ApplyRStarRainVisualRotation(RStarRainBladeData data)
    {
        if (data == null || data.visualTransform == null)
        {
            return;
        }

        Quaternion finalRotation = BuildRStarRainVisibleRotation();
        data.sword.transform.rotation = finalRotation;
        data.visualTransform.rotation = finalRotation;
    }

    private void SyncLegacyOwnerValuesIfNeeded()
    {
        if (Owner == null)
        {
            return;
        }

        if (Approximately(rBaseSwordCount, 1)) rBaseSwordCount = Owner.rBaseSwordCount;
        if (Approximately(rEffectScale, new Vector3(0.3f, 0.3f, 0.3f))) rEffectScale = Owner.rEffectScale;
        if (Approximately(rEffectRotationZ, 0f)) rEffectRotationZ = Owner.rEffectRotationZ;
        if (Approximately(rEffectOffset, Vector3.zero)) rEffectOffset = Owner.rEffectOffset;
        if (Approximately(rEffectPlaneScale, new Vector3(0.3f, 0.3f, 1f))) rEffectPlaneScale = Owner.rEffectPlaneScale;
        if (Approximately(rEffectYawOffset, 0f)) rEffectYawOffset = Owner.rEffectYawOffset;
        if (Approximately(rEffectVisualPitch, 0f)) rEffectVisualPitch = Owner.rEffectVisualPitch;
        if (Approximately(rEffectVisualYaw, 0f)) rEffectVisualYaw = Owner.rEffectVisualYaw;
        if (Approximately(rEffectVisualRoll, 0f)) rEffectVisualRoll = Owner.rEffectVisualRoll;
        if (Approximately(rSwarmDuration, 2.0f)) rSwarmDuration = Owner.rSwarmDuration;
        if (Approximately(rSwarmRadiusMin, 0.8f)) rSwarmRadiusMin = Owner.rSwarmRadiusMin;
        if (Approximately(rSwarmRadiusMax, 3.2f)) rSwarmRadiusMax = Owner.rSwarmRadiusMax;
        if (Approximately(rSwarmHeightMin, 0.4f)) rSwarmHeightMin = Owner.rSwarmHeightMin;
        if (Approximately(rSwarmHeightMax, 3.0f)) rSwarmHeightMax = Owner.rSwarmHeightMax;
        if (Approximately(rSwarmSpeedMin, 120f)) rSwarmSpeedMin = Owner.rSwarmSpeedMin;
        if (Approximately(rSwarmSpeedMax, 300f)) rSwarmSpeedMax = Owner.rSwarmSpeedMax;
        if (Approximately(rSwarmBobAmplitudeMin, 0.05f)) rSwarmBobAmplitudeMin = Owner.rSwarmBobAmplitudeMin;
        if (Approximately(rSwarmBobAmplitudeMax, 0.35f)) rSwarmBobAmplitudeMax = Owner.rSwarmBobAmplitudeMax;
        if (Approximately(rSwarmBobFrequencyMin, 0.8f)) rSwarmBobFrequencyMin = Owner.rSwarmBobFrequencyMin;
        if (Approximately(rSwarmBobFrequencyMax, 2.5f)) rSwarmBobFrequencyMax = Owner.rSwarmBobFrequencyMax;
        if (Approximately(rSwarmRadiusJitter, 0.25f)) rSwarmRadiusJitter = Owner.rSwarmRadiusJitter;
        if (rSwarmClockwise) rSwarmClockwise = Owner.rSwarmClockwise;
        if (Approximately(rSwarmForwardOffset, 2.0f)) rSwarmForwardOffset = Owner.rSwarmForwardOffset;
        if (Approximately(rSwarmYawOffset, 0f)) rSwarmYawOffset = Owner.rSwarmYawOffset;
        if (rSwarmUseCameraForward) rSwarmUseCameraForward = Owner.rSwarmUseCameraForward;
        if (!rSwarmCenterOnPlayer) rSwarmCenterOnPlayer = Owner.rSwarmCenterOnPlayer;
        if (!rApplyEffectOffsetToSwarmCenter) rApplyEffectOffsetToSwarmCenter = Owner.rApplyEffectOffsetToSwarmCenter;
        if (rUseTangentFacing) rUseTangentFacing = Owner.rUseTangentFacing;
        if (Approximately(rPlaneUprightEuler, Vector3.zero)) rPlaneUprightEuler = Owner.rPlaneUprightEuler;
        if (Approximately(rPlaneFaceCameraEuler, Vector3.zero)) rPlaneFaceCameraEuler = Owner.rPlaneFaceCameraEuler;
        if (rFlipPlaneFrontBack) rFlipPlaneFrontBack = Owner.rFlipPlaneFrontBack;
        if (Approximately(rPlaneFrontBackFlipEuler, new Vector3(0f, 180f, 0f))) rPlaneFrontBackFlipEuler = Owner.rPlaneFrontBackFlipEuler;
        if (!rUsePlayerLayerForR) rUsePlayerLayerForR = Owner.rUsePlayerLayerForR;
        if (rForceDoubleSided) rForceDoubleSided = Owner.rForceDoubleSided;
        if (!rDebugSwordVelocityFacing) rDebugSwordVelocityFacing = Owner.rDebugSwordVelocityFacing;
        if (Approximately(rFacingLookAheadTime, 0.05f)) rFacingLookAheadTime = Owner.rFacingLookAheadTime;
        if (!rDebugFacingScreenAngle) rDebugFacingScreenAngle = Owner.rDebugFacingScreenAngle;
        if (!rEnableSwordSelfSpin) rEnableSwordSelfSpin = Owner.rEnableSwordSelfSpin;
        if (Approximately(rSwordSelfSpinMin, 30f)) rSwordSelfSpinMin = Owner.rSwordSelfSpinMin;
        if (Approximately(rSwordSelfSpinMax, 120f)) rSwordSelfSpinMax = Owner.rSwordSelfSpinMax;
        if (Approximately(rSwordLengthLocalAxis, Vector3.up)) rSwordLengthLocalAxis = Owner.rSwordLengthLocalAxis;
        if (Approximately(rSwarmDamageRadius, 3.0f)) rSwarmDamageRadius = Owner.rSwarmDamageRadius;
        if (Approximately(rSwarmDamageInterval, 0.25f)) rSwarmDamageInterval = Owner.rSwarmDamageInterval;
        if (Approximately(rSwarmDamagePerTick, 2.0f)) rSwarmDamagePerTick = Owner.rSwarmDamagePerTick;
        if (rSwarmEnemyLayer == ~0) rSwarmEnemyLayer = Owner.rSwarmEnemyLayer;
        if (Approximately(rStarRainStartRatio, 0.5f)) rStarRainStartRatio = Owner.rStarRainStartRatio;
        if (Approximately(rStarRainInterval, 0.12f)) rStarRainInterval = Owner.rStarRainInterval;
        if (rStarRainBladesPerWave == 2) rStarRainBladesPerWave = Owner.rStarRainBladesPerWave;
        if (Approximately(rStarRainSpawnHeight, 5f)) rStarRainSpawnHeight = Owner.rStarRainSpawnHeight;
        if (Approximately(rStarRainRadius, 5f)) rStarRainRadius = Owner.rStarRainRadius;
        if (Approximately(rStarRainFallSpeed, 10f)) rStarRainFallSpeed = Owner.rStarRainFallSpeed;
        if (Approximately(rStarRainRandomDelay, 0.15f)) rStarRainRandomDelay = Owner.rStarRainRandomDelay;
        if (Approximately(rStarRainDamageRadius, 1.2f)) rStarRainDamageRadius = Owner.rStarRainDamageRadius;
        if (Approximately(rStarRainDamageMultiplier, 0.6f)) rStarRainDamageMultiplier = Owner.rStarRainDamageMultiplier;
        if (rStarRainContinueAfterOrbit) rStarRainContinueAfterOrbit = Owner.rStarRainContinueAfterOrbit;
        if (Approximately(rStarRainExtraDurationAfterOrbit, 0.6f)) rStarRainExtraDurationAfterOrbit = Owner.rStarRainExtraDurationAfterOrbit;
        if (Approximately(rStarRainEffectScale, new Vector3(0.3f, 0.3f, 0.3f))) rStarRainEffectScale = Owner.rStarRainEffectScale;
        if (rStarRainUseForcedVisualRotation) rStarRainUseForcedVisualRotation = Owner.rStarRainUseForcedVisualRotation;
        if (Approximately(rStarRainForcedVisualEuler, new Vector3(0f, 180f, 0f))) rStarRainForcedVisualEuler = Owner.rStarRainForcedVisualEuler;
        if (Approximately(rStarRainVisualEulerOffset, Vector3.zero)) rStarRainVisualEulerOffset = Owner.rStarRainVisualEulerOffset;
        if (rOrbitClearWhenOrbitEnds) rOrbitClearWhenOrbitEnds = Owner.rOrbitClearWhenOrbitEnds;
        if (Approximately(rOrbitFadeOutDuration, 0.15f)) rOrbitFadeOutDuration = Owner.rOrbitFadeOutDuration;
    }

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.0001f;
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude <= 0.0001f;
    }

}
