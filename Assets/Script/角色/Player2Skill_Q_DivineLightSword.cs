using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player2Skill_Q_DivineLightSword : PlayerSkillBase
{
    [Header("Q Delay")]
    [SerializeField] private float qDelay = 0.35f;
    [SerializeField] private float qSwordSpeed = 14f;

    [Header("Q Effect")]
    [SerializeField] private GameObject qSkillEffectPrefab;
    [SerializeField] private Vector3 qEffectScale = new Vector3(0.25f, 0.25f, 0.25f);
    [SerializeField] private float qEffectRotationZ = 0f;
    [SerializeField] private Vector3 qEffectOffset = Vector3.zero;
    [SerializeField] private Vector3 qEffectPlaneScale = new Vector3(0.25f, 0.25f, 1f);
    [SerializeField] private float qEffectYawOffset = 0f;
    [SerializeField] private float qEffectVisualPitch = 0f;
    [SerializeField] private float qEffectVisualYaw = 0f;
    [SerializeField] private float qEffectVisualRoll = 0f;
    [SerializeField] private bool qEffectInvertForward = false;

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

        Vector3 dir = Owner.GetFacingDirection();
        Vector3 spawnPos = Owner.transform.position + Vector3.up * 1.2f + Owner.transform.right * 0.8f + qEffectOffset;
        GameObject sword = CreateSkillEffectVisual(
            "Q_Sword",
            ResolveQVisualPrefab(),
            spawnPos,
            dir,
            true,
            qEffectInvertForward,
            qEffectYawOffset,
            qEffectVisualPitch,
            qEffectVisualYaw,
            qEffectVisualRoll + ResolveRotation(qEffectRotationZ),
            ResolveVisualScale(qEffectScale, qEffectPlaneScale));

        if (sword != null)
        {
            activeQVisualRoots.Add(sword);
            StartCoroutine(FireAfterDelay(sword, dir, qDelay, qSwordSpeed));
        }

        if (Owner != null)
        {
            Owner.currentSwordEnergy += 1;
        }
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

    private IEnumerator FireAfterDelay(GameObject effectRoot, Vector3 dir, float delay, float speed)
    {
        float t = 0f;
        while (t < delay)
        {
            if (effectRoot == null)
            {
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        float life = 2.2f;
        float elapsed = 0f;
        while (elapsed < life)
        {
            if (effectRoot == null)
            {
                yield break;
            }

            effectRoot.transform.position += dir.normalized * speed * Time.deltaTime;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (effectRoot != null)
        {
            activeQVisualRoots.Remove(effectRoot);
            Destroy(effectRoot);
        }
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
            effectVisual.transform.rotation = root.transform.rotation;
            float rawScaleMultiplier = Mathf.Max(0.01f, Owner.skillEffectPrefabScaleMultiplier);
            effectVisual.transform.localScale = effectVisual.transform.localScale * rawScaleMultiplier;
        }
        else
        {
            visualTarget.localRotation = BuildQuadOffsetRotation(visualPitch, visualYaw, visualRoll);
            visualTarget.localScale = Vector3.Scale(visualTarget.localScale, ClampVisualScale(visualScale));
        }

        effectVisual.SetActive(true);
        activeQVisualRoots.Add(root);
        return root;
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
}
