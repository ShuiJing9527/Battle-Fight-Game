using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;
public class Player2Skill_E_CelestialShift : PlayerSkillBase
{
    [Header("E - 天轨换位 / 基础")]
    [SerializeField] private float eRailDuration = 0.6f;

    [Header("E - 天轨换位 / 残影")]
    [InspectorName("E 启用残影")]
    [SerializeField] private bool eEnableAfterimageShader = true;
    [InspectorName("E 残影来源 SpriteRenderer")]
    [SerializeField] private SpriteRenderer eAfterimageSourceRenderer;
    [InspectorName("E 残影材质")]
    [SerializeField] private Material eAfterimageMaterial;
    [InspectorName("E 残影数量")]
    [SerializeField] private int eAfterimageCount = 12;
    [InspectorName("E 残影持续时间")]
    [SerializeField] private float eAfterimageDuration = 0.45f;
    [InspectorName("E 残影透明度")]
    [SerializeField] private float eAfterimageAlpha = 0.35f;
    [InspectorName("E 残影生成间隔")]
    [SerializeField] private float eAfterimageSpawnInterval = 0.03f;
    [InspectorName("E 残影缩放")]
    [SerializeField] private Vector3 eAfterimageScale = Vector3.one;
    [InspectorName("E 残影染色")]
    [SerializeField] private Color eAfterimageTint = new Color(0.6f, 0.85f, 1f, 0.45f);
    [InspectorName("E 残影 SortingOrder 偏移")]
    [SerializeField] private int eAfterimageSortingOrderOffset = 5;
    [InspectorName("E 残影调试日志")]
    [SerializeField] private bool eAfterimageDebugLog = false;
    [InspectorName("E 残影使用彩虹")]
    [SerializeField] private bool eAfterimageUseRainbow = true;
    [InspectorName("E 残影反转颜色顺序")]
    [SerializeField] private bool eAfterimageInvertColorOrder = true;
    [InspectorName("E 残影按序号淡化")]
    [SerializeField] private bool eAfterimageFadeByAgeIndex = true;
    [InspectorName("E 最旧残影透明度倍率")]
    [SerializeField] private float eAfterimageOldestAlphaMultiplier = 0.25f;
    [InspectorName("E 残影按终点距离淡化")]
    [SerializeField] private bool eAfterimageFadeByDistanceToEnd = true;
    [InspectorName("E 远处残影透明度倍率")]
    [SerializeField] private float eAfterimageFarAlphaMultiplier = 0.12f;
    [InspectorName("E 彩虹色相速度")]
    [SerializeField] private float eAfterimageRainbowHueSpeed = 0.04f;
    [InspectorName("E 彩虹饱和度")]
    [SerializeField] private float eAfterimageRainbowSaturation = 0.45f;
    [InspectorName("E 彩虹亮度")]
    [SerializeField] private float eAfterimageRainbowValue = 1f;
    [InspectorName("E 残影使用距离采样")]
    [SerializeField] private bool eAfterimageUseDistanceSampling = true;
    [InspectorName("E 残影使用真实移动方向")]
    [SerializeField] private bool eAfterimageUseActualMoveDirection = true;
    [InspectorName("E 残影反转移动方向")]
    [SerializeField] private bool eAfterimageInvertMoveDirection = false;
    [InspectorName("E 残影反转翻转")]
    [SerializeField] private bool eAfterimageInvertFlip = false;
    [InspectorName("E 残影间距")]
    [SerializeField] private float eAfterimageSpacing = 0.06f;
    [InspectorName("E 每次位移最大残影数")]
    [SerializeField] private int eAfterimageMaxPerDash = 24;

    private bool isDashing;
    private Vector3 lastMoveDir = Vector3.forward;
    [Header("E - Dash Effect")]
    [SerializeField] private GameObject eDashEffectPrefab;
    [SerializeField] private Vector3 eDashEffectLocalOffset = Vector3.zero;
    [SerializeField] private float eDashEffectYawOffset = 0f;
    [SerializeField] private bool eSpawnDashEffect = true;
    [SerializeField] private float eDashEffectLifetime = 0.7f;

    private readonly List<GameObject> activeAfterimageGhosts = new List<GameObject>();

    public override void Initialize(Player2PrototypeController owner)
    {
        base.Initialize(owner);
    }

    public override void Cast()
    {
        if (Owner == null || isDashing)
        {
            return;
        }

        StartCoroutine(DashRoutine());
        Owner.GetComponentInChildren<Player2HaloRotateEffect>(true)?.TriggerSkillBoost();
    }

    public override void Cleanup()
    {
        StopAllCoroutines();
        isDashing = false;

        for (int i = 0; i < activeAfterimageGhosts.Count; i++)
        {
            GameObject ghost = activeAfterimageGhosts[i];
            if (ghost != null)
            {
                Destroy(ghost);
            }
        }

        activeAfterimageGhosts.Clear();
    }

    private void OnDisable()
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private IEnumerator DashRoutine()
    {
        isDashing = true;

        float dashDuration = Mathf.Max(0.05f, eRailDuration);
        float dashDistance = Owner != null ? Owner.dashDistance : 4f;
        Vector3 dir = Owner != null ? Owner.FacingDirection : Vector3.forward;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector3.forward;
        }
        Vector3 dashStartPos = Owner != null ? Owner.transform.position : transform.position;
        Vector3 dashEndPos = dashStartPos + dir * dashDistance;
        bool afterimageFlipX = GetCurrentSpineFacingFlipX();
        if (eAfterimageInvertFlip)
        {
            afterimageFlipX = !afterimageFlipX;
        }

        int spawnedAfterimages = 0;
        Vector3 lastAfterimagePos = dashStartPos;
        float afterimageDistanceAccumulator = 0f;
        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            float p = Mathf.Clamp01(elapsed / dashDuration);
            if (Owner != null)
            {
                Owner.transform.position = Vector3.Lerp(dashStartPos, dashEndPos, p);
            }
            else
            {
                transform.position = Vector3.Lerp(dashStartPos, dashEndPos, p);
            }

            if (eEnableAfterimageShader && eAfterimageUseDistanceSampling)
            {
                Vector3 currentPos = Owner != null ? Owner.transform.position : transform.position;
                Vector3 moveDelta = currentPos - lastAfterimagePos;
                Vector3 actualMoveDir = moveDelta.sqrMagnitude > 0.0001f ? moveDelta.normalized : Vector3.zero;
                if (eAfterimageUseActualMoveDirection && actualMoveDir.sqrMagnitude > 0.0001f)
                {
                    lastMoveDir = actualMoveDir;
                }

                float moved = Vector3.Distance(lastAfterimagePos, currentPos);
                afterimageDistanceAccumulator += moved;

                int maxPerDash = Mathf.Max(0, eAfterimageMaxPerDash);
                float spacing = Mathf.Max(0.001f, eAfterimageSpacing);
                Vector3 from = lastAfterimagePos;
                Vector3 to = currentPos;
                if (eAfterimageInvertMoveDirection)
                {
                    from = currentPos;
                    to = lastAfterimagePos;
                }

                while (spawnedAfterimages < maxPerDash && afterimageDistanceAccumulator >= spacing)
                {
                    float over = afterimageDistanceAccumulator - spacing;
                    float spawnDistanceFromStart = moved - over;
                    float t = moved > 0.0001f ? Mathf.Clamp01(spawnDistanceFromStart / moved) : 1f;
                    Vector3 spawnPos = Vector3.Lerp(from, to, t);
                    if (eAfterimageDebugLog)
                    {
                        Debug.Log($"E Afterimage index={spawnedAfterimages}, invert={eAfterimageInvertMoveDirection}, from={from}, to={to}, pos={spawnPos}", this);
                    }

                    TrySpawnEAfterimage(spawnPos, dashStartPos, dashEndPos, afterimageFlipX, ref spawnedAfterimages);
                    afterimageDistanceAccumulator -= spacing;
                }

                lastAfterimagePos = currentPos;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (Owner != null)
        {
            Owner.transform.position = dashEndPos;
        }
        else
        {
            transform.position = dashEndPos;
        }

        if (eSpawnDashEffect)
        {
            SpawnDashEffect(dashStartPos, dashEndPos);
        }

        isDashing = false;
    }

    private bool TrySpawnEAfterimage(Vector3 position, Vector3 dashStartPos, Vector3 dashEndPos, bool afterimageFlipX, ref int spawnedCount)
    {
        if (!eEnableAfterimageShader)
        {
            return false;
        }

        int maxCount = Mathf.Max(0, eAfterimageMaxPerDash);
        if (maxCount > 0 && spawnedCount >= maxCount)
        {
            return false;
        }

        SpriteRenderer sourceSprite = ResolveEAfterimageSourceRenderer();
        if (sourceSprite == null || sourceSprite.sprite == null)
        {
            if (eAfterimageDebugLog)
            {
                Debug.LogWarning("[E Afterimage] source SpriteRenderer is null or has no sprite.", this);
            }

            return false;
        }

        GameObject afterimage = SpawnEAfterimageGhost(sourceSprite, position, dashStartPos, dashEndPos, afterimageFlipX, spawnedCount);
        if (afterimage == null)
        {
            return false;
        }

        spawnedCount += 1;
        return true;
    }

    private SpriteRenderer ResolveEAfterimageSourceRenderer()
    {
        if (eAfterimageSourceRenderer != null && eAfterimageSourceRenderer.sprite != null)
        {
            return eAfterimageSourceRenderer;
        }

        if (Owner != null)
        {
            SpriteRenderer spriteRenderer = Owner.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                return spriteRenderer;
            }

            spriteRenderer = Owner.GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                return spriteRenderer;
            }
        }

        SpriteRenderer localSpriteRenderer = GetComponent<SpriteRenderer>();
        if (localSpriteRenderer != null && localSpriteRenderer.sprite != null)
        {
            return localSpriteRenderer;
        }

        localSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (localSpriteRenderer != null && localSpriteRenderer.sprite != null)
        {
            return localSpriteRenderer;
        }

        return null;
    }

    private bool GetCurrentSpineFacingFlipX()
    {
        SkeletonAnimation spineAnimation = Owner != null ? Owner.GetSpineAnimation() : null;
        if (spineAnimation == null)
        {
            spineAnimation = GetComponentInChildren<SkeletonAnimation>(true);
        }

        if (spineAnimation != null && spineAnimation.Skeleton != null)
        {
            return spineAnimation.Skeleton.ScaleX < 0f;
        }

        return false;
    }

    private GameObject SpawnEAfterimageGhost(SpriteRenderer sourceSprite, Vector3 worldPosition, Vector3 dashStartPos, Vector3 dashEndPos, bool afterimageFlipX, int spawnedIndex)
    {
        if (sourceSprite == null || sourceSprite.sprite == null)
        {
            return null;
        }

        GameObject ghost = new GameObject("E_Afterimage_Ghost");
        SpriteRenderer ghostSprite = ghost.AddComponent<SpriteRenderer>();
        ghostSprite.sprite = sourceSprite.sprite;
        if (eAfterimageMaterial != null)
        {
            ghostSprite.material = new Material(eAfterimageMaterial);
        }
        ghostSprite.flipX = afterimageFlipX;
        ghostSprite.flipY = sourceSprite.flipY;
        ghostSprite.drawMode = sourceSprite.drawMode;
        ghostSprite.size = sourceSprite.size;
        ghostSprite.spriteSortPoint = sourceSprite.spriteSortPoint;
        ghostSprite.maskInteraction = sourceSprite.maskInteraction;
        ghostSprite.sortingLayerID = sourceSprite.sortingLayerID;
        ghostSprite.sortingOrder = sourceSprite.sortingOrder + eAfterimageSortingOrderOffset;

        Color c;
        if (eAfterimageUseRainbow)
        {
            int colorIndex = spawnedIndex;
            if (eAfterimageInvertColorOrder)
            {
                int count = Mathf.Max(1, eAfterimageCount);
                colorIndex = count - 1 - spawnedIndex;
            }

            float hue = Mathf.Repeat(colorIndex * eAfterimageRainbowHueSpeed, 1f);
            c = Color.HSVToRGB(hue, eAfterimageRainbowSaturation, eAfterimageRainbowValue);
        }
        else
        {
            c = eAfterimageTint;
        }

        c *= sourceSprite.color;

        int visibleCount = Mathf.Max(1, Mathf.Min(eAfterimageMaxPerDash, Mathf.Max(1, eAfterimageCount)));
        int denominator = Mathf.Max(1, visibleCount - 1);
        float ageT = Mathf.Clamp01(spawnedIndex / (float)denominator);
        float rampMaxAlpha = Mathf.Clamp01(eAfterimageAlpha);
        float rampAlpha = Mathf.Lerp(0.08f, rampMaxAlpha, ageT);

        if (eAfterimageFadeByDistanceToEnd)
        {
            float totalDistance = Vector3.Distance(dashStartPos, dashEndPos);
            float distanceToEnd = Vector3.Distance(worldPosition, dashEndPos);
            float endT = totalDistance <= 0.0001f
                ? 1f
                : 1f - Mathf.Clamp01(distanceToEnd / totalDistance);
            float distanceAlpha = Mathf.Lerp(0.08f, rampMaxAlpha, endT);
            c.a = Mathf.Min(rampMaxAlpha, Mathf.Max(rampAlpha, distanceAlpha));
        }
        else if (eAfterimageFadeByAgeIndex)
        {
            c.a = Mathf.Min(rampMaxAlpha, rampAlpha);
        }
        else
        {
            c.a = rampMaxAlpha;
        }

        ApplySpriteRendererColor(ghostSprite, c, eAfterimageMaterial != null);
        ghost.transform.position = worldPosition;
        ghost.transform.rotation = sourceSprite.transform.rotation;
        Vector3 sourceScale = sourceSprite.transform.lossyScale;
        sourceScale = new Vector3(Mathf.Abs(sourceScale.x), Mathf.Abs(sourceScale.y), Mathf.Abs(sourceScale.z));
        ghost.transform.localScale = Vector3.Scale(sourceScale, eAfterimageScale);

        if (eAfterimageDebugLog)
        {
            Debug.Log($"Afterimage pos={worldPosition}, end={dashEndPos}, distanceToEnd={Vector3.Distance(worldPosition, dashEndPos)}, alpha={c.a}", this);
        }

        activeAfterimageGhosts.Add(ghost);
        StartCoroutine(FadeAndDestroySpriteGhost(ghost, ghostSprite, eAfterimageDuration));
        return ghost;
    }

    private static void ApplySpriteRendererColor(SpriteRenderer spriteRenderer, Color color, bool syncMaterial)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.color = color;

        if (!syncMaterial)
        {
            return;
        }

        Material material = spriteRenderer.material;
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
    }

    private IEnumerator FadeAndDestroySpriteGhost(GameObject ghost, SpriteRenderer sr, float duration)
    {
        if (ghost == null || sr == null)
        {
            yield break;
        }

        Color baseColor = sr.color;
        float total = Mathf.Max(0.05f, duration);
        float elapsed = 0f;
        while (elapsed < total && ghost != null && sr != null)
        {
            float t = 1f - Mathf.Clamp01(elapsed / total);
            Color c = baseColor;
            c.a *= t;
            ApplySpriteRendererColor(sr, c, eAfterimageMaterial != null);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (ghost != null)
        {
            activeAfterimageGhosts.Remove(ghost);
            Destroy(ghost);
        }
    }

    private void SpawnDashEffect(Vector3 dashStartPos, Vector3 dashEndPos)
    {
        if (eDashEffectPrefab == null)
        {
            return;
        }

        Vector3 dashDirection = dashEndPos - dashStartPos;
        dashDirection.y = 0f;
        Vector3 effectDirection = dashDirection.sqrMagnitude > 0.0001f ? -dashDirection.normalized : Vector3.back;
        Quaternion rotation = Quaternion.LookRotation(effectDirection, Vector3.up);
        rotation *= Quaternion.Euler(0f, eDashEffectYawOffset, 0f);

        Vector3 spawnPosition = dashEndPos + eDashEffectLocalOffset;
        GameObject instance = Instantiate(eDashEffectPrefab, spawnPosition, rotation);
        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem system = systems[i];
            if (system == null)
            {
                continue;
            }

            system.Clear(true);
            system.Play(true);
        }

        Destroy(instance, Mathf.Max(0.05f, eDashEffectLifetime));
    }
}
