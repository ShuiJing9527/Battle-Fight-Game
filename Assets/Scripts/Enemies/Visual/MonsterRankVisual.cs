using UnityEngine;

[DisallowMultipleComponent]
public class MonsterRankVisual : MonoBehaviour
{
    [Header("Binding")]
    public Transform visualRoot;
    public Transform effectRoot;

    [Header("Normal Visual Config")]
    [SerializeField] private bool enableNormalVisualGroundOffset = true;
    [SerializeField] private float normalVisualGroundOffsetY = -0.05f;

    [Header("Elite Visual Config")]
    [SerializeField] private bool enableEliteVisualScale = true;
    [SerializeField] private float eliteVisualScaleMultiplier = 1.5f;
    [SerializeField] private bool enableEliteVisualGroundOffset = true;
    [SerializeField] private float eliteVisualGroundOffsetY = 0.25f;
    [SerializeField] private bool debugEliteVisualConfig = true;

    [Header("Boss Visual Grounding")]
    [SerializeField] private bool enableBossVisualScale = true;
    [SerializeField] private float bossVisualScaleMultiplier = 4.0f;
    [SerializeField] private bool enableBossVisualGroundOffset = true;
    [SerializeField] private float bossVisualGroundOffsetY = 0.85f;
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
    private string lastConfigSource = "Default";

    public Transform RuntimeVisualRoot => runtimeVisualRoot != null ? runtimeVisualRoot : ResolveScaleTarget();
    public MonsterRank LastAppliedRank => lastAppliedRank;
    public float BossVisualScaleMultiplier => bossVisualScaleMultiplier;
    public float EliteVisualScaleMultiplier => eliteVisualScaleMultiplier;
    public float NormalVisualGroundOffsetY => normalVisualGroundOffsetY;
    public string LastConfigSource => lastConfigSource;

    public void ApplyNormalVisualConfig(
        bool enableGroundOffset,
        float groundOffsetY,
        string source = "Default")
    {
        enableNormalVisualGroundOffset = enableGroundOffset;
        normalVisualGroundOffsetY = groundOffsetY;
        lastConfigSource = string.IsNullOrWhiteSpace(source) ? "Default" : source;

        MonsterIdentity identity = GetComponent<MonsterIdentity>();
        if (identity != null)
        {
            Apply(identity);
        }
    }

    public void ApplyEliteVisualConfig(
        bool enableScale,
        float scaleMultiplier,
        bool enableGroundOffset,
        float groundOffsetY,
        bool debug,
        string source = "Default")
    {
        enableEliteVisualScale = enableScale;
        eliteVisualScaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);
        enableEliteVisualGroundOffset = enableGroundOffset;
        eliteVisualGroundOffsetY = groundOffsetY;
        debugEliteVisualConfig = debug;
        lastConfigSource = string.IsNullOrWhiteSpace(source) ? "Default" : source;

        MonsterIdentity identity = GetComponent<MonsterIdentity>();
        if (identity != null)
        {
            Apply(identity);
        }
    }

    public void ApplyBossVisualConfig(
        bool enableScale,
        float scaleMultiplier,
        bool enableGroundOffset,
        float groundOffsetY,
        bool debug,
        string source = "Default")
    {
        enableBossVisualScale = enableScale;
        bossVisualScaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);
        enableBossVisualGroundOffset = enableGroundOffset;
        bossVisualGroundOffsetY = groundOffsetY;
        debugBossVisualGroundOffset = debug;
        lastConfigSource = string.IsNullOrWhiteSpace(source) ? "Default" : source;

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
            multiplier = enableEliteVisualScale ? eliteVisualScaleMultiplier : eliteScale;
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

        Vector3 adjustedLocalPosition = baseLocalPosition;

        if (rank == MonsterRank.Elite && enableEliteVisualGroundOffset)
        {
            adjustedLocalPosition.y += eliteVisualGroundOffsetY;
        }

        if (rank == MonsterRank.Normal && enableNormalVisualGroundOffset)
        {
            adjustedLocalPosition.y += normalVisualGroundOffsetY;
        }

        if (rank == MonsterRank.Boss && enableBossVisualGroundOffset)
        {
            adjustedLocalPosition.y += bossVisualGroundOffsetY;
        }

        Vector3 rootPositionBefore = transform.position;
        Vector3 visualLocalBefore = scaleTarget.localPosition;
        Vector3 appliedLocalScale = scaleTarget.localScale;
        scaleTarget.localPosition = adjustedLocalPosition;

        bool updatedAnimationBaseScale = false;
        if (slimeAnimationController != null)
        {
            slimeAnimationController.SetVisualBaseScale(scaleTarget.localScale);
            slimeAnimationController.SetVisualBasePosition(adjustedLocalPosition);
            appliedLocalScale = slimeAnimationController.BaseVisualLocalScale;
            updatedAnimationBaseScale = true;
        }

        float visualScaleMultiplier = rank switch
        {
            MonsterRank.Boss => enableBossVisualScale ? bossVisualScaleMultiplier : bossScale,
            MonsterRank.Elite => enableEliteVisualScale ? eliteVisualScaleMultiplier : eliteScale,
            _ => normalScale
        };

        float visualGroundOffsetY = rank switch
        {
            MonsterRank.Boss => enableBossVisualGroundOffset ? bossVisualGroundOffsetY : 0f,
            MonsterRank.Elite => enableEliteVisualGroundOffset ? eliteVisualGroundOffsetY : 0f,
            _ => enableNormalVisualGroundOffset ? normalVisualGroundOffsetY : 0f
        };

        Debug.Log(
            "[RankVisualConfigApply] " +
            "object=" + name +
            " rank=" + rank +
            " source=" + lastConfigSource +
            " visualScaleMultiplier=" + visualScaleMultiplier.ToString("F2") +
            " visualGroundOffsetY=" + visualGroundOffsetY.ToString("F2") +
            " final visual localScale=" + scaleTarget.localScale +
            " final visual localPosition=" + scaleTarget.localPosition,
            this);

        if (rank == MonsterRank.Elite && debugEliteVisualConfig)
        {
            Debug.Log(
                "[EliteVisualConfig] " +
                "object=" + name +
                " rank=" + rank +
                " runtimeVisualRoot=" + (scaleTarget != null ? scaleTarget.name : "null") +
                " eliteVisualScaleMultiplier=" + eliteVisualScaleMultiplier.ToString("F2") +
                " eliteVisualGroundOffsetY=" + eliteVisualGroundOffsetY.ToString("F2") +
                " visual localScale after=" + scaleTarget.localScale +
                " visual localPosition after=" + scaleTarget.localPosition +
                " animation baseScale updated=" + updatedAnimationBaseScale,
                this);
        }

        if (rank == MonsterRank.Boss && debugBossVisualGroundOffset)
        {
            Collider mainCollider = ResolveMainCollider();
            Rigidbody body = GetComponent<Rigidbody>();
            Bounds colliderBounds = mainCollider != null ? mainCollider.bounds : default;
            Debug.Log(
                $"[BossVisualScaleFix] object={name} rank={rank} attackStyle={(identity != null ? identity.attackStyle.ToString() : "Unknown")} runtimeVisualRoot={(scaleTarget != null ? scaleTarget.name : "null")} " +
                $"slimeAnimationController found={(slimeAnimationController != null)} original visual localScale={baseLocalScale} bossVisualScaleMultiplier={bossVisualScaleMultiplier:F2} boss visual localScale applied={scaleTarget.localScale} " +
                $"animation baseScale updated={updatedAnimationBaseScale} visual localScale after one frame={appliedLocalScale} bossVisualGroundOffsetY={bossVisualGroundOffsetY:F2} visual localPosition after={scaleTarget.localPosition} root position before={rootPositionBefore} root position after={transform.position}",
                this);

            Debug.Log(
                $"[BossVisualFix] object={name} prefab/source={gameObject.name} runtime rank={rank} species={(identity != null ? identity.species.ToString() : "Unknown")} attackStyle={(identity != null ? identity.attackStyle.ToString() : "Unknown")} " +
                $"root position before={rootPositionBefore} root position after={transform.position} visualRoot original={(visualRoot != null ? visualRoot.name : "null")} runtimeVisualRoot={(scaleTarget != null ? scaleTarget.name : "null")} visualRoot was root={(scaleTarget == transform)} " +
                $"original visual localScale={baseLocalScale} bossVisualScaleMultiplier={bossVisualScaleMultiplier:F2} visual localScale after={scaleTarget.localScale} " +
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
