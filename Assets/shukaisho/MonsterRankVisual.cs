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

    public void Apply(MonsterIdentity identity)
    {
        Transform scaleTarget = ResolveScaleTarget();
        Transform runtimeEffectParent = ResolveEffectParent();

        CaptureBaseScale(scaleTarget);
        ClearRuntimeEffects();

        if (identity == null)
        {
            ApplyScaleForRank(scaleTarget, MonsterRank.Normal);
            return;
        }

        ApplyScaleForRank(scaleTarget, identity.rank);

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
            multiplier = bossScale;
        }

        scaleTarget.localScale = Vector3.Scale(baseLocalScale, Vector3.one * multiplier);
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

    private void CaptureBaseScale(Transform scaleTarget)
    {
        if (baseScaleCaptured || scaleTarget == null)
        {
            return;
        }

        baseLocalScale = scaleTarget.localScale;
        baseScaleCaptured = true;
    }

    private Transform ResolveScaleTarget()
    {
        return visualRoot != null ? visualRoot : transform;
    }

    private Transform ResolveEffectParent()
    {
        return effectRoot != null ? effectRoot : transform;
    }
}
