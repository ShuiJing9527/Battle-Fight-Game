using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;
using UnityEngine.Serialization;
public class Player2Skill_E_CelestialShift : PlayerSkillBase
{
    [Header("E - 星痕瞬移 / 核心参数")]
    [SerializeField, Min(0f)] private float cooldown = 8f;
    [SerializeField, Min(0f)] private float manaCost = 20f;
    [SerializeField, Min(0f)] private float dashDistance = 4f;
    [SerializeField, Min(0.05f)] private float dashDuration = 0.15f;
    [Header("E - 星痕瞬移 / 基础")]
    [SerializeField] private float eRailDuration = 0.6f;
    [Header("E - 星痕瞬移 / 路径伤害")]
    [Tooltip("E 物理段固定基础伤害。")]
    [SerializeField, Min(0f)] private float ePhysicalBaseDamage = 10f;
    [Tooltip("E 物理段从物理攻击获得的倍率。")]
    [FormerlySerializedAs("ePhysicalAttackMultiplier")]
    [SerializeField, Min(0f)] private float ePhysicalFromPhysicalAttackScaling = 0.6f;
    [Tooltip("E 物理段从特殊攻击获得的倍率。")]
    [SerializeField, Min(0f)] private float ePhysicalFromSpecialAttackScaling = 0f;
    [Tooltip("E 特殊段固定基础伤害。")]
    [FormerlySerializedAs("eMagicBaseDamage")]
    [SerializeField, Min(0f)] private float eSpecialBaseDamage = 30f;
    [Tooltip("E 特殊段从物理攻击获得的倍率。")]
    [SerializeField, Min(0f)] private float eSpecialFromPhysicalAttackScaling = 0f;
    [Tooltip("E 特殊段从特殊攻击获得的倍率。")]
    [FormerlySerializedAs("eMagicAttackMultiplier")]
    [SerializeField, Min(0f)] private float eSpecialFromSpecialAttackScaling = 0.3f;
    [Tooltip("E 物理段最终倍率。")]
    [SerializeField, Min(0f)] private float ePhysicalDamageMultiplier = 1f;
    [Tooltip("E 特殊段最终倍率。")]
    [SerializeField, Min(0f)] private float eSpecialDamageMultiplier = 1f;
    [SerializeField, Min(0f)] private float ePathHitRadius = 0.6f;

    [Header("E - 星痕瞬移 / 残影特效")]
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
    [Header("E - 星痕瞬移 / 光迹特效")]
    [SerializeField] private GameObject eDashEffectPrefab;
    [SerializeField] private Vector3 eDashEffectLocalOffset = Vector3.zero;
    [SerializeField] private float eDashEffectYawOffset = 0f;
    [SerializeField] private bool eSpawnDashEffect = true;
    [SerializeField] private float eDashEffectLifetime = 0.7f;

    private readonly List<GameObject> activeAfterimageGhosts = new List<GameObject>();
    private readonly HashSet<int> hitEnemiesThisDash = new HashSet<int>();
    private RuneRuntimeState runeRuntimeState;
    private int currentDashHitCount;
    protected override int SkillIndex => 2;

    public override float CooldownSeconds => cooldown;
    public override float ManaCost => manaCost;

    public override void Initialize(Player2PrototypeController owner)
    {
        base.Initialize(owner);
    }

    public override bool Cast()
    {
        if (Owner == null || isDashing)
        {
            return false;
        }

        runeRuntimeState = ResolveRuneRuntimeState();
        PrepareRuneCastContext();
        StartCoroutine(DashRoutine());
        Owner.GetComponentInChildren<Player2HaloRotateEffect>(true)?.TriggerSkillBoost();
        return true;
    }

    public override void Cleanup()
    {
        StopAllCoroutines();
        isDashing = false;
        currentDashHitCount = 0;
        hitEnemiesThisDash.Clear();
        ResetRuneCastContext();

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

    private RuneRuntimeState ResolveRuneRuntimeState()
    {
        if (Owner == null)
        {
            return GetComponent<RuneRuntimeState>() ?? GetComponentInParent<RuneRuntimeState>();
        }

        return Owner.GetComponent<RuneRuntimeState>() ?? Owner.GetComponentInParent<RuneRuntimeState>();
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
        currentDashHitCount = 0;
        hitEnemiesThisDash.Clear();

        float dashDurationSeconds = Mathf.Max(0.05f, dashDuration > 0f ? dashDuration : eRailDuration);
        float manaMultiplier = ResolveManaRuneScaledMultiplier(0.30f);
        float dashDistanceValue = Mathf.Max(0f, dashDistance * manaMultiplier);
        if (manaMultiplier > 1f)
        {
            LogManaRuneApplied("Player02 E", "DashDistance", dashDistance, dashDistanceValue);
        }
        Vector3 dir = Owner != null ? Owner.FacingDirection : Vector3.forward;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector3.forward;
        }
        Vector3 dashStartPos = Owner != null ? Owner.transform.position : transform.position;
        Vector3 dashEndPos = dashStartPos + dir * dashDistanceValue;
        Vector3 previousPos = dashStartPos;
        bool afterimageFlipX = GetCurrentSpineFacingFlipX();
        if (eAfterimageInvertFlip)
        {
            afterimageFlipX = !afterimageFlipX;
        }

        Debug.Log(
            $"Player02 E 星痕瞬移：距离={dashDistanceValue:F2}，持续={dashDurationSeconds:F2}，路径伤害半径={Mathf.Max(0f, ePathHitRadius):F2}，物理伤害={ePhysicalBaseDamage:F0}+PATK*{ePhysicalFromPhysicalAttackScaling:F2}+SATK*{ePhysicalFromSpecialAttackScaling:F2}，特殊伤害={eSpecialBaseDamage:F0}+PATK*{eSpecialFromPhysicalAttackScaling:F2}+SATK*{eSpecialFromSpecialAttackScaling:F2}，CD={cooldown:F2}，蓝耗={manaCost:F2}",
            this);

        int spawnedAfterimages = 0;
        Vector3 lastAfterimagePos = dashStartPos;
        float afterimageDistanceAccumulator = 0f;
        float elapsed = 0f;

        while (elapsed < dashDurationSeconds)
        {
            float p = Mathf.Clamp01(elapsed / dashDurationSeconds);
            if (Owner != null)
            {
                Owner.transform.position = Vector3.Lerp(dashStartPos, dashEndPos, p);
            }
            else
            {
                transform.position = Vector3.Lerp(dashStartPos, dashEndPos, p);
            }

            Vector3 currentPosForDamage = Owner != null ? Owner.transform.position : transform.position;
            TryApplyDashPathDamage(previousPos, currentPosForDamage);
            previousPos = currentPosForDamage;

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

        TryApplyDashPathDamage(previousPos, dashEndPos);

        if (eSpawnDashEffect)
        {
            SpawnDashEffect(dashStartPos, dashEndPos);
        }

        isDashing = false;
        Debug.Log($"Player02 E 星痕瞬移结束：命中敌人数量={currentDashHitCount}", this);
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

    private void TryApplyDashPathDamage(Vector3 from, Vector3 to)
    {
        float hitRadius = Mathf.Max(0f, ePathHitRadius);
        if (hitRadius <= 0f)
        {
            return;
        }

        Collider[] hits = Physics.OverlapCapsule(from, to, hitRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || !BattleTargetUtility.IsMonster(hit, transform))
            {
                continue;
            }

            Transform targetRoot = hit.transform.root;
            if (targetRoot == null || !hitEnemiesThisDash.Add(targetRoot.gameObject.GetInstanceID()))
            {
                continue;
            }

            ApplyDashPathDamage(targetRoot.gameObject);
        }
    }

    private void ApplyDashPathDamage(GameObject targetRoot)
    {
        if (targetRoot == null)
        {
            return;
        }

        CombatHealth combatHealth = targetRoot.GetComponentInParent<CombatHealth>();
        EnemyHealth enemyHealth = targetRoot.GetComponentInParent<EnemyHealth>();
        CombatStats targetStats = targetRoot.GetComponentInParent<CombatStats>();
        if (combatHealth == null && enemyHealth == null)
        {
            return;
        }

        CombatStats attackerStats = Owner != null ? Owner.GetComponent<CombatStats>() : GetComponent<CombatStats>();
        float attackerPhysicalAttack = attackerStats != null ? Mathf.Max(0f, attackerStats.physicalAttack) : 0f;
        float attackerSpecialAttack = attackerStats != null ? Mathf.Max(0f, attackerStats.specialAttack) : 0f;
        float targetPhysicalDefense = targetStats != null ? Mathf.Max(0f, targetStats.physicalDefense) : 0f;
        float targetSpecialDefense = targetStats != null ? Mathf.Max(0f, targetStats.specialDefense) : 0f;

        float physicalRaw =
            Mathf.Max(0f, ePhysicalBaseDamage)
            + attackerPhysicalAttack * Mathf.Max(0f, ePhysicalFromPhysicalAttackScaling)
            + attackerSpecialAttack * Mathf.Max(0f, ePhysicalFromSpecialAttackScaling);
        float specialRaw =
            Mathf.Max(0f, eSpecialBaseDamage)
            + attackerPhysicalAttack * Mathf.Max(0f, eSpecialFromPhysicalAttackScaling)
            + attackerSpecialAttack * Mathf.Max(0f, eSpecialFromSpecialAttackScaling);
        float outgoingDamageMultiplier = Mathf.Max(0f, ResolveRuneOutgoingDamageMultiplier());
        float physicalFinal = Mathf.Max(1f, (physicalRaw - targetPhysicalDefense) * Mathf.Max(0f, ePhysicalDamageMultiplier) * outgoingDamageMultiplier);
        float specialFinal = Mathf.Max(1f, (specialRaw - targetSpecialDefense) * Mathf.Max(0f, eSpecialDamageMultiplier) * outgoingDamageMultiplier);

        GameObject source = Owner != null ? Owner.gameObject : gameObject;
        float beforeHealth = ResolveCurrentHealth(combatHealth);

        if (combatHealth != null && combatHealth.gameObject != source)
        {
            combatHealth.ApplyDirectDamage(physicalFinal, source, DamagePopupType.Physical);
            combatHealth.ApplyDirectDamage(specialFinal, source, DamagePopupType.Special);
        }
        else if (enemyHealth != null && enemyHealth.gameObject != source)
        {
            int totalDamage = Mathf.Max(2, Mathf.RoundToInt(physicalFinal) + Mathf.RoundToInt(specialFinal));
            enemyHealth.TakeDamage(totalDamage, source);
        }

        if (combatHealth != null)
        {
            float actualDamage = Mathf.Max(0f, beforeHealth - ResolveCurrentHealth(combatHealth));
            runeRuntimeState?.NotifyMonsterDamagedBySkill(2, combatHealth, actualDamage);
        }

        currentDashHitCount++;
    }

    private float ResolveCurrentHealth(CombatHealth health)
    {
        if (health == null)
        {
            return 0f;
        }

        return health.resourceBank != null
            ? Mathf.Max(0f, health.resourceBank.currentHealth)
            : Mathf.Max(0f, health.currentHealth);
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
