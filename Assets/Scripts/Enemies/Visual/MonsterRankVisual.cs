using UnityEngine;

[DisallowMultipleComponent]
public class MonsterRankVisual : MonoBehaviour
{
    [Header("Binding")]
    public Transform visualRoot;
    public Transform effectRoot;

    [Header("Boss Visual Grounding")]
    [SerializeField] private bool enableBossVisualScale = true;
    [SerializeField] private float bossVisualScaleMultiplier = 3.0f;
    [SerializeField] private bool enableBossVisualGroundOffset = true;
    [SerializeField] private float bossVisualGroundOffsetY = -0.25f;
    [SerializeField] private bool debugBossVisualGroundOffset = true;

    [Header("Effect Prefabs")]
    public GameObject normalEffectPrefab;
    public GameObject eliteAuraPrefab;
    public GameObject bossAuraPrefab;

    [Header("Scale Multipliers")]
    public float normalScale = 1f;
    public float eliteScale = 1.45f;
    public float bossScale = 2.4f;

    [Header("Options")]
    public bool applyScale = true;
    public bool createFallbackLight = true;

    private const string RuntimeEffectName = "RuntimeRankVisualEffect";

    private bool baseScaleCaptured;
    private Vector3 baseLocalScale = Vector3.one;
    private bool baseLocalPositionCaptured;
    private Vector3 baseLocalPosition = Vector3.zero;
    private Transform runtimeVisualRoot;
    private bool loggedVisualRootFallback;
    private MonsterRank lastAppliedRank = MonsterRank.Normal;

    public Transform RuntimeVisualRoot => runtimeVisualRoot != null ? runtimeVisualRoot : ResolveScaleTarget();
    public MonsterRank LastAppliedRank => lastAppliedRank;
    public float BossVisualScaleMultiplier => bossVisualScaleMultiplier;

    public void ApplyBossVisualConfig(
        bool enableScale,
        float scaleMultiplier,
        bool enableGroundOffset,
        float groundOffsetY,
        bool debug)
    {
        enableBossVisualScale = enableScale;
        bossVisualScaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);
        enableBossVisualGroundOffset = enableGroundOffset;
        bossVisualGroundOffsetY = groundOffsetY;
        debugBossVisualGroundOffset = debug;

        MonsterIdentity identity = GetComponent<MonsterIdentity>();
        if (identity != null)
        {
            Apply(identity);
        }
    }

    public void Apply(MonsterIdentity identity)
    {
        Transform scaleTarget = ResolveScaleTarget();
        Transform runtimeEffectParent = ResolveEffectParent();
        SlimeAnimationController slimeAnimationController = GetComponent<SlimeAnimationController>();

        CaptureBaseVisualState(scaleTarget);
        ClearRuntimeEffects();

        if (identity == null)
        {
            lastAppliedRank = MonsterRank.Normal;
            ApplyScaleForRank(scaleTarget, MonsterRank.Normal);
            ApplyBossGroundOffset(scaleTarget, MonsterRank.Normal, identity, slimeAnimationController);
            return;
        }

        lastAppliedRank = identity.rank;
        ApplyScaleForRank(scaleTarget, identity.rank);
        ApplyBossGroundOffset(scaleTarget, identity.rank, identity, slimeAnimationController);

        GameObject effectPrefab = ResolveEffectPrefab(identity.rank);
        if (effectPrefab != null)
        {
            GameObject runtimeEffect = Instantiate(effectPrefab, runtimeEffectParent, false);
            runtimeEffect.name = RuntimeEffectName;
            runtimeEffect.transform.localPosition = Vector3.zero;
            runtimeEffect.transform.localRotation = Quaternion.identity;
            runtimeEffect.transform.localScale = Vector3.one;
            return;
        }

        if (createFallbackLight)
        {
            CreateFallbackLight(runtimeEffectParent, identity.rank);
        }
    }

    public void ClearRuntimeEffects()
    {
        Transform runtimeEffectParent = ResolveEffectParent();
        for (int i = runtimeEffectParent.childCount - 1; i >= 0; i--)
        {
            Transform child = runtimeEffectParent.GetChild(i);
            if (child != null && child.name == RuntimeEffectName)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void ApplyScaleForRank(Transform scaleTarget, MonsterRank rank)
    {
        if (!applyScale || scaleTarget == null)
        {
            return;
        }

        float multiplier = normalScale;
        if (rank == MonsterRank.Elite)
        {
            multiplier = eliteScale;
        }
        else if (rank == MonsterRank.Boss)
        {
            multiplier = enableBossVisualScale ? bossVisualScaleMultiplier : bossScale;
        }

        scaleTarget.localScale = Vector3.Scale(baseLocalScale, Vector3.one * multiplier);
    }

    private void ApplyBossGroundOffset(Transform scaleTarget, MonsterRank rank, MonsterIdentity identity, SlimeAnimationController slimeAnimationController)
    {
        if (scaleTarget == null || !baseLocalPositionCaptured)
        {
            return;
        }

        Vector3 originalLocalScale = baseLocalScale;
        Vector3 adjustedLocalPosition = baseLocalPosition;
        if (rank == MonsterRank.Boss && enableBossVisualGroundOffset)
        {
            adjustedLocalPosition.y += bossVisualGroundOffsetY;
        }

        Vector3 rootPositionBefore = transform.position;
        Vector3 visualLocalBefore = scaleTarget.localPosition;
        Vector3 appliedLocalScale = scaleTarget.localScale;
        scaleTarget.localPosition = adjustedLocalPosition;

        bool updatedAnimationBaseScale = false;
        if (rank == MonsterRank.Boss && slimeAnimationController != null)
        {
            slimeAnimationController.SetVisualBaseScale(scaleTarget.localScale);
            slimeAnimationController.SetVisualBasePosition(adjustedLocalPosition);
            appliedLocalScale = slimeAnimationController.BaseVisualLocalScale;
            updatedAnimationBaseScale = true;
        }

        if (rank == MonsterRank.Boss && debugBossVisualGroundOffset)
        {
            Collider mainCollider = ResolveMainCollider();
            Rigidbody body = GetComponent<Rigidbody>();
            Bounds colliderBounds = mainCollider != null ? mainCollider.bounds : default;
            Debug.Log(
                $"[BossVisualScaleFix] object={name} rank={rank} attackStyle={(identity != null ? identity.attackStyle.ToString() : "Unknown")} runtimeVisualRoot={(scaleTarget != null ? scaleTarget.name : "null")} " +
                $"slimeAnimationController found={(slimeAnimationController != null)} original visual localScale={originalLocalScale} bossVisualScaleMultiplier={bossVisualScaleMultiplier:F2} boss visual localScale applied={scaleTarget.localScale} " +
                $"animation baseScale updated={updatedAnimationBaseScale} visual localScale after one frame={appliedLocalScale} bossVisualGroundOffsetY={bossVisualGroundOffsetY:F2} visual localPosition after={scaleTarget.localPosition} root position before={rootPositionBefore} root position after={transform.position}",
                this);

            Debug.Log(
                $"[BossVisualFix] object={name} prefab/source={gameObject.name} runtime rank={rank} species={(identity != null ? identity.species.ToString() : "Unknown")} attackStyle={(identity != null ? identity.attackStyle.ToString() : "Unknown")} " +
                $"root position before={rootPositionBefore} root position after={transform.position} visualRoot original={(visualRoot != null ? visualRoot.name : "null")} runtimeVisualRoot={(scaleTarget != null ? scaleTarget.name : "null")} visualRoot was root={(scaleTarget == transform)} " +
                $"original visual localScale={originalLocalScale} bossVisualScaleMultiplier={bossVisualScaleMultiplier:F2} visual localScale after={scaleTarget.localScale} " +
                $"original visual localPosition={baseLocalPosition} visual localPosition before={visualLocalBefore} bossVisualGroundOffsetY={bossVisualGroundOffsetY:F2} visual localPosition after={scaleTarget.localPosition} visual worldPosition after={scaleTarget.position} " +
                $"main collider bounds={(mainCollider != null ? colliderBounds.ToString() : "None")} rigidbody position={(body != null ? body.position.ToString() : "None")}",
                this);
        }
    }

    private GameObject ResolveEffectPrefab(MonsterRank rank)
    {
        if (rank == MonsterRank.Boss)
        {
            return bossAuraPrefab;
        }

        if (rank == MonsterRank.Elite)
        {
            return eliteAuraPrefab;
        }

        return normalEffectPrefab;
    }

    private void CreateFallbackLight(Transform parent, MonsterRank rank)
    {
        if (rank == MonsterRank.Normal || parent == null)
        {
            return;
        }

        GameObject runtimeEffect = new GameObject(RuntimeEffectName);
        runtimeEffect.transform.SetParent(parent, false);
        runtimeEffect.transform.localPosition = Vector3.zero;
        runtimeEffect.transform.localRotation = Quaternion.identity;
        runtimeEffect.transform.localScale = Vector3.one;

        Light light = runtimeEffect.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = rank == MonsterRank.Boss
            ? new Color(1f, 0.7f, 0.15f, 0.85f)
            : new Color(0.85f, 0.25f, 1f, 0.75f);
        light.range = rank == MonsterRank.Boss ? 6f : 3f;
        light.intensity = rank == MonsterRank.Boss ? 1.4f : 0.7f;
    }

    private void CaptureBaseVisualState(Transform scaleTarget)
    {
        if (scaleTarget == null)
        {
            return;
        }

        if (!baseScaleCaptured)
        {
            baseLocalScale = scaleTarget.localScale;
            baseScaleCaptured = true;
        }

        if (!baseLocalPositionCaptured)
        {
            baseLocalPosition = scaleTarget.localPosition;
            baseLocalPositionCaptured = true;
        }
    }

    private Transform ResolveScaleTarget()
    {
        runtimeVisualRoot = ResolveRuntimeVisualRoot();
        return runtimeVisualRoot != null ? runtimeVisualRoot : transform;
    }

    private Transform ResolveEffectParent()
    {
        return effectRoot != null ? effectRoot : transform;
    }

    private Transform ResolveRuntimeVisualRoot()
    {
        if (visualRoot != null && visualRoot != transform)
        {
            return visualRoot;
        }

        Transform resolved = null;

        Transform namedVisual = transform.Find("Visual_Slime");
        if (namedVisual != null)
        {
            resolved = namedVisual;
        }

        if (resolved == null)
        {
            SlimeAnimationController slimeAnimationController = GetComponent<SlimeAnimationController>();
            if (slimeAnimationController != null && slimeAnimationController.VisualRoot != null && slimeAnimationController.VisualRoot != transform)
            {
                resolved = slimeAnimationController.VisualRoot;
            }
        }

        if (resolved == null)
        {
            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer != null && spriteRenderer.transform != transform)
            {
                resolved = spriteRenderer.transform;
            }
        }

        if (resolved != null)
        {
            if (!loggedVisualRootFallback)
            {
                Debug.LogWarning($"[MonsterRankVisual] visualRoot was root/null, using actual visual child: {resolved.name}", this);
                loggedVisualRootFallback = true;
            }

            return resolved;
        }

        return visualRoot != null ? visualRoot : transform;
    }

    private Collider ResolveMainCollider()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            return collider;
        }

        return null;
    }
}
