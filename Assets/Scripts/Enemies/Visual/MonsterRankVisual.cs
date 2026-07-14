using UnityEngine;

[DisallowMultipleComponent]
public class MonsterRankVisual : MonoBehaviour
{
    [Header("Binding")]
    public Transform visualRoot;
    public Transform effectRoot;

    [Header("Effect Prefabs")]
    public GameObject normalEffectPrefab;
    public GameObject eliteAuraPrefab;
    public GameObject bossAuraPrefab;

    [Header("Options")]
    public bool createFallbackLight = true;

    private const string RuntimeEffectName = "RuntimeRankVisualEffect";

    private Transform runtimeVisualRoot;
    private bool loggedVisualRootFallback;
    private MonsterRank lastAppliedRank = MonsterRank.Normal;
    private string lastConfigSource = "Default";

    public Transform RuntimeVisualRoot => runtimeVisualRoot != null ? runtimeVisualRoot : ResolveScaleTarget();
    public MonsterRank LastAppliedRank => lastAppliedRank;
    public string LastConfigSource => lastConfigSource;

    public void Apply(MonsterIdentity identity)
    {
        Transform runtimeEffectParent = ResolveEffectParent();

        ResolveScaleTarget();
        ClearRuntimeEffects();

        if (identity == null)
        {
            lastAppliedRank = MonsterRank.Normal;
            return;
        }

        lastAppliedRank = identity.rank;

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

}
