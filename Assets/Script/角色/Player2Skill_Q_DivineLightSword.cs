using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player2Skill_Q_DivineLightSword : PlayerSkillBase
{
    [Header("Q - 神临光剑 / 基础")]
    [InspectorName("Q 蓄力时间")]
    [SerializeField] private float qDelay = 0.35f;
    [InspectorName("Q 光剑速度")]
    [SerializeField] private float qSwordSpeed = 14f;

    [Header("Q - 神临光剑 / 视觉")]
    [InspectorName("Q 技能特效预制体")]
    [SerializeField] private GameObject qSkillEffectPrefab;
    [InspectorName("Q 特效尺寸")]
    [SerializeField] private Vector3 qEffectScale = new Vector3(0.25f, 0.25f, 0.25f);
    [InspectorName("Q 特效旋转 Z")]
    [SerializeField] private float qEffectRotationZ = 0f;
    [InspectorName("Q 特效偏移")]
    [SerializeField] private Vector3 qEffectOffset = Vector3.zero;
    [InspectorName("Q 平面尺寸")]
    [SerializeField] private Vector3 qEffectPlaneScale = new Vector3(0.25f, 0.25f, 1f);
    [InspectorName("Q Yaw 偏移")]
    [SerializeField] private float qEffectYawOffset = 0f;
    [InspectorName("Q 显示 Pitch")]
    [SerializeField] private float qEffectVisualPitch = 0f;
    [InspectorName("Q 显示 Yaw")]
    [SerializeField] private float qEffectVisualYaw = 0f;
    [InspectorName("Q 显示 Roll")]
    [SerializeField] private float qEffectVisualRoll = 0f;
    [InspectorName("Q 反向")]
    [SerializeField] private bool qEffectInvertForward = false;

    [Header("Q Motion")]
    [SerializeField] private bool qAutoTrackEnemy = true;
    [SerializeField] private float qHomingSearchRadius = 18f;
    [SerializeField] private float qHomingTurnSpeed = 540f;
    [SerializeField] private bool qKeepPaperFlat = true;

    [SerializeField] private float qSpreadAngle = 10f;
    [SerializeField] private float qSpawnSideOffsetRandom = 0.35f;
    [SerializeField] private float qWaveAmplitude = 0.22f;
    [SerializeField] private float qWaveFrequency = 1.6f;
    [SerializeField] private float qArcHeight = 0.18f;
    [SerializeField] private float qProjectileLife = 2.2f;
    [SerializeField] private bool qRotateAlongVelocity = true;
    [SerializeField] private float qVisualPitchJitter = 12f;
    [SerializeField] private float qVisualYawJitter = 10f;

    private readonly List<GameObject> activeQVisualRoots = new List<GameObject>();

    public override void Initialize(Player2PrototypeController owner)
    {
        base.Initialize(owner);
        ImportLegacySettingsIfNeeded();
    }

    public override void Cast()
    {
        if (Owner == null)
        {
            return;
        }

        Vector3 baseDir = Owner.GetFacingDirection();
        if (baseDir.sqrMagnitude < 0.0001f)
        {
            baseDir = Vector3.forward;
        }

        baseDir.y = 0f;
        baseDir.Normalize();

        float spread = Random.Range(-Mathf.Abs(qSpreadAngle), Mathf.Abs(qSpreadAngle));
        Vector3 shotDir = Quaternion.Euler(0f, spread, 0f) * baseDir;
        shotDir.y = 0f;
        shotDir = shotDir.sqrMagnitude > 0.0001f ? shotDir.normalized : baseDir;

        Vector3 sideDir = Vector3.Cross(Vector3.up, shotDir);
        if (sideDir.sqrMagnitude < 0.0001f)
        {
            sideDir = Owner.transform.right;
        }
        sideDir.Normalize();

        float randomSideOffset = Random.Range(-Mathf.Abs(qSpawnSideOffsetRandom), Mathf.Abs(qSpawnSideOffsetRandom));
        Vector3 spawnPos = Owner.transform.position + Vector3.up * 1.2f + Owner.transform.right * 0.8f + qEffectOffset + sideDir * randomSideOffset;

        float pitchOffset = qKeepPaperFlat ? 0f : Random.Range(-Mathf.Abs(qVisualPitchJitter), Mathf.Abs(qVisualPitchJitter));
        float yawOffset = qKeepPaperFlat ? 0f : Random.Range(-Mathf.Abs(qVisualYawJitter), Mathf.Abs(qVisualYawJitter));

        Transform homingTarget = qAutoTrackEnemy ? FindNearestEnemyTarget(spawnPos) : null;

        GameObject sword = CreateSkillEffectVisual(
            "Q_Sword",
            ResolveQVisualPrefab(),
            spawnPos,
            shotDir,
            true,
            qEffectInvertForward,
            qEffectYawOffset,
            qEffectVisualPitch + pitchOffset,
            qKeepPaperFlat ? 0f : qEffectVisualYaw + yawOffset,
            qKeepPaperFlat ? 0f : qEffectVisualRoll + ResolveRotation(qEffectRotationZ),
            ResolveVisualScale(qEffectScale, qEffectPlaneScale));

        if (sword != null)
        {
            StartCoroutine(FireAfterDelay(sword, shotDir, sideDir, homingTarget, qDelay, qSwordSpeed));
        }

        Owner.currentSwordEnergy += 1;
    }

    public override void Cleanup()
    {
        StopAllCoroutines();
        for (int i = 0; i < activeQVisualRoots.Count; i++)
        {
            if (activeQVisualRoots[i] != null)
            {
                Destroy(activeQVisualRoots[i]);
            }
        }

        activeQVisualRoots.Clear();
    }

    private void ImportLegacySettingsIfNeeded()
    {
        if (Owner == null || !UsesDefaultSkillSettings())
        {
            return;
        }

        qDelay = Owner.qDelay;
        qSwordSpeed = Owner.qSwordSpeed;
        qSkillEffectPrefab = Owner.qSkillEffectPrefab;
        qEffectScale = Owner.qEffectScale;
        qEffectRotationZ = Owner.qEffectRotationZ;
        qEffectOffset = Owner.qEffectOffset;
        qEffectPlaneScale = Owner.qEffectPlaneScale;
        qEffectYawOffset = Owner.qEffectYawOffset;
        qEffectVisualPitch = Owner.qEffectVisualPitch;
        qEffectVisualYaw = Owner.qEffectVisualYaw;
        qEffectVisualRoll = Owner.qEffectVisualRoll;
        qEffectInvertForward = Owner.qEffectInvertForward;
    }

    private bool UsesDefaultSkillSettings()
    {
        return Mathf.Approximately(qDelay, 0.35f)
               && Mathf.Approximately(qSwordSpeed, 14f)
               && qSkillEffectPrefab == null
               && IsSameVector3(qEffectScale, new Vector3(0.25f, 0.25f, 0.25f))
               && Mathf.Approximately(qEffectRotationZ, 0f)
               && IsSameVector3(qEffectOffset, Vector3.zero)
               && IsSameVector3(qEffectPlaneScale, new Vector3(0.25f, 0.25f, 1f))
               && Mathf.Approximately(qEffectYawOffset, 0f)
               && Mathf.Approximately(qEffectVisualPitch, 0f)
               && Mathf.Approximately(qEffectVisualYaw, 0f)
               && Mathf.Approximately(qEffectVisualRoll, 0f)
               && qEffectInvertForward == false;
    }

    private static bool IsSameVector3(Vector3 left, Vector3 right)
    {
        return (left - right).sqrMagnitude <= 0.000001f;
    }

    private GameObject ResolveQVisualPrefab()
    {
        if (qSkillEffectPrefab != null)
        {
            return qSkillEffectPrefab;
        }

        return Owner != null ? Owner.sharedSkillEffectPrefab : null;
    }

    private IEnumerator FireAfterDelay(GameObject effectRoot, Vector3 dir, Vector3 sideDir, Transform homingTarget, float delay, float speed)
    {
        float waitElapsed = 0f;
        while (waitElapsed < delay)
        {
            if (effectRoot == null)
            {
                yield break;
            }

            waitElapsed += Time.deltaTime;
            yield return null;
        }

        float safeLife = Mathf.Max(0.1f, qProjectileLife);
        float elapsed = 0f;
        Vector3 currentPosition = effectRoot.transform.position;
        Vector3 previousPosition = currentPosition;
        Vector3 currentDirection = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        float wavePhase = Random.Range(0f, Mathf.PI * 2f);
        float waveFrequency = Mathf.Max(0.01f, qWaveFrequency);
        Vector3 previousDecoration = Vector3.zero;

        while (elapsed < safeLife)
        {
            if (effectRoot == null)
            {
                yield break;
            }

            if (homingTarget == null || !homingTarget.gameObject.activeInHierarchy)
            {
                homingTarget = qAutoTrackEnemy ? FindNearestEnemyTarget(currentPosition) : null;
            }

            if (homingTarget != null)
            {
                Vector3 desiredTargetPoint = ResolvePaperFlatTargetPoint(homingTarget, currentPosition.y);
                Vector3 desiredDirection = desiredTargetPoint - currentPosition;
                desiredDirection.y = 0f;
                if (desiredDirection.sqrMagnitude > 0.0001f)
                {
                    float maxTurnRadians = Mathf.Deg2Rad * Mathf.Max(0f, qHomingTurnSpeed) * Time.deltaTime;
                    currentDirection = Vector3.RotateTowards(currentDirection, desiredDirection.normalized, maxTurnRadians, 0f).normalized;
                }
            }

            float progress = Mathf.Clamp01(elapsed / safeLife);
            Vector3 dynamicSideDir = Vector3.Cross(Vector3.up, currentDirection);
            if (dynamicSideDir.sqrMagnitude < 0.0001f)
            {
                dynamicSideDir = sideDir;
            }

            dynamicSideDir.Normalize();

            float waveStrength = qKeepPaperFlat ? 0f : Mathf.Sin(progress * Mathf.PI) * qWaveAmplitude;
            Vector3 waveOffset = dynamicSideDir * Mathf.Sin(progress * Mathf.PI * 2f * waveFrequency + wavePhase) * waveStrength;
            Vector3 arcOffset = qKeepPaperFlat ? Vector3.zero : Vector3.up * Mathf.Sin(progress * Mathf.PI) * qArcHeight;
            Vector3 decoration = waveOffset + arcOffset;
            Vector3 decorationDelta = decoration - previousDecoration;
            Vector3 nextPosition = currentPosition + currentDirection * speed * Time.deltaTime + decorationDelta;

            if (qRotateAlongVelocity)
            {
                Vector3 velocity = nextPosition - previousPosition;
                if (velocity.sqrMagnitude > 0.000001f)
                {
                    if (qKeepPaperFlat)
                    {
                        ApplyPaperFlatDirection(effectRoot.transform, velocity);
                    }
                    else
                    {
                        ApplyRootDirection(effectRoot.transform, velocity, true, qEffectInvertForward, qEffectYawOffset);
                    }
                }
            }

            effectRoot.transform.position = nextPosition;
            previousPosition = currentPosition;
            currentPosition = nextPosition;
            previousDecoration = decoration;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (effectRoot != null)
        {
            activeQVisualRoots.Remove(effectRoot);
            Destroy(effectRoot);
        }
    }

    private Transform FindNearestEnemyTarget(Vector3 origin)
    {
        Collider[] hits = Physics.OverlapSphere(origin, Mathf.Max(0.1f, qHomingSearchRadius));
        Transform bestTarget = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            Transform targetRoot = hit.transform.root;
            if (targetRoot == null || (Owner != null && targetRoot.gameObject == Owner.gameObject))
            {
                continue;
            }

            CombatHealth combatHealth = targetRoot.GetComponentInParent<CombatHealth>();
            EnemyHealth enemyHealth = targetRoot.GetComponentInParent<EnemyHealth>();
            EnemyController enemyController = targetRoot.GetComponentInParent<EnemyController>();
            SlimeAnimationController slimeAnimation = targetRoot.GetComponentInParent<SlimeAnimationController>();
            if (combatHealth == null && enemyHealth == null && enemyController == null && slimeAnimation == null)
            {
                continue;
            }

            Vector3 toTarget = targetRoot.position - origin;
            toTarget.y = 0f;
            float distanceSqr = toTarget.sqrMagnitude;
            if (distanceSqr < 0.0001f || distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestTarget = targetRoot;
        }

        return bestTarget;
    }

    private static Vector3 ResolvePaperFlatTargetPoint(Transform target, float lockedY)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        Collider targetCollider = target.GetComponentInChildren<Collider>();
        Vector3 targetPoint = targetCollider != null ? targetCollider.bounds.center : target.position;
        targetPoint.y = lockedY;
        return targetPoint;
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
        if (qKeepPaperFlat)
        {
            ApplyPaperFlatDirection(root.transform, direction);
        }
        else
        {
            ApplyRootDirection(root.transform, direction, alignToDirection, invertForward, yawOffset);
        }

        GameObject effectVisual = CreateEffectInstance(name, specificPrefab, root.transform.position, root.transform.rotation, Owner != null && Owner.useRawPrefabRotationForSkillEffects);
        if (effectVisual == null)
        {
            Destroy(root);
            return null;
        }

        effectVisual.transform.SetParent(root.transform, true);

        Transform visualTarget = FindEffectVisualTransform(effectVisual);
        if (Owner != null && Owner.useRawPrefabRotationForSkillEffects)
        {
            if (qKeepPaperFlat)
            {
                ApplyPaperFlatDirection(root.transform, direction);
            }
            else
            {
                effectVisual.transform.rotation = root.transform.rotation * BuildQuadOffsetRotation(visualPitch, visualYaw, visualRoll);
            }
            float rawScaleMultiplier = Mathf.Max(0.01f, Owner.skillEffectPrefabScaleMultiplier);
            effectVisual.transform.localScale = effectVisual.transform.localScale * rawScaleMultiplier;
        }
        else
        {
            visualTarget.localRotation = qKeepPaperFlat
                ? Quaternion.identity
                : BuildQuadOffsetRotation(visualPitch, visualYaw, visualRoll);
            visualTarget.localScale = Vector3.Scale(visualTarget.localScale, ClampVisualScale(visualScale));
        }

        effectVisual.SetActive(true);
        activeQVisualRoots.Add(root);
        return root;
    }

    private void ApplyPaperFlatDirection(Transform root, Vector3 direction)
    {
        if (root == null)
        {
            return;
        }

        float lockedYaw = Owner != null ? Owner.transform.eulerAngles.y : root.eulerAngles.y;
        root.rotation = Quaternion.Euler(0f, lockedYaw, 0f);

        if (root.childCount <= 0)
        {
            return;
        }

        Transform visualRoot = FindEffectVisualTransform(root.GetChild(0).gameObject);
        if (visualRoot == null)
        {
            return;
        }

        Vector3 baseFacing = Owner != null ? Owner.GetFacingDirection() : Vector3.forward;
        baseFacing.y = 0f;
        if (baseFacing.sqrMagnitude < 0.0001f)
        {
            baseFacing = Vector3.forward;
        }
        else
        {
            baseFacing.Normalize();
        }

        Vector3 planarDirection = new Vector3(direction.x, 0f, direction.z);
        if (planarDirection.sqrMagnitude < 0.0001f)
        {
            planarDirection = baseFacing;
        }
        else
        {
            planarDirection.Normalize();
        }

        float signedAngle = Vector3.SignedAngle(baseFacing, planarDirection, Vector3.up);
        float baseRoll = (Owner != null ? Owner.transform.eulerAngles.z : 0f) + 90f;
        visualRoot.rotation = Quaternion.Euler(qEffectVisualPitch, lockedYaw, baseRoll + signedAngle);
    }

    private GameObject CreateEffectInstance(string effectName, GameObject specificPrefab, Vector3 position, Quaternion rotation, bool preservePrefabRotation)
    {
        GameObject sourcePrefab = specificPrefab != null ? specificPrefab : (Owner != null ? Owner.sharedSkillEffectPrefab : null);
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

        Debug.LogWarning($"[Player2Skill_Q_DivineLightSword] Missing skill effect prefab for '{effectName}' on {name}. Assign Q Skill Effect Prefab or Shared Skill Effect Prefab.", this);
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

    private static Quaternion BuildQuadOffsetRotation(float pitch, float yaw, float roll)
    {
        return Quaternion.Euler(NormalizeQuadLegacyPitch(pitch), yaw, roll);
    }

    private static float NormalizeQuadLegacyPitch(float pitch)
    {
        return Mathf.Abs(pitch) < 0.0001f ? 0f : pitch;
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

    private float ResolveRotation(float specificRotationZ)
    {
        return (Owner != null ? Owner.sharedEffectRotationZ : 0f) + NormalizeQuadLegacyRoll(specificRotationZ);
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
        if (alignToDirection)
        {
            Vector3 planarDirection = new Vector3(direction.x, 0f, direction.z);
            if (planarDirection.sqrMagnitude > 0.0001f)
            {
                yaw = Mathf.Atan2(planarDirection.x, planarDirection.z) * Mathf.Rad2Deg;
            }
        }

        if (invertForward)
        {
            yaw += 180f;
        }

        root.rotation = Quaternion.Euler(0f, yaw + yawOffset, 0f);
    }
}


