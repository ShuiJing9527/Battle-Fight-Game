using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player2Skill_W_HolyWheelDeflection : PlayerSkillBase
{
    [Header("W - 神圣护轮 / 核心参数")]
    [SerializeField, Min(0f)] private float cooldown = 6f;
    [SerializeField, Min(0f)] private float manaCost = 40f;
    [InspectorName("W 持续时间")]
    [SerializeField] private float wDuration = 1.5f;
    [InspectorName("W 基础减伤")]
    [SerializeField] private float wDamageReduction = 0.4f;

    [Header("W - 星环剑轮 / 护盾")]
    [InspectorName("W 护盾倍率")]
    [SerializeField, Min(0f)] private float wShieldMaxHpMultiplier = 2f;
    [InspectorName("W 每个额外星刃的护盾加成")]
    [SerializeField, Min(0f)] private float wShieldBonusPerExtraSword = 0.1f;
    [InspectorName("W 结束时清空护盾")]
    [SerializeField] private bool wClearShieldOnEnd = true;

    [Header("W - 星环剑轮 / 神印加成")]
    [InspectorName("W 每把剑减伤加成")]
    [SerializeField] private float wDamageReductionPerSword = 0.03f;
    [InspectorName("W 最大减伤")]
    [SerializeField] private float wMaxDamageReduction = 0.8f;
    [InspectorName("W 反击伤害比例")]
    [SerializeField] private float wCounterDamageRatio = 0.5f;

    [Header("W - 星环剑轮 / 视觉")]
    [InspectorName("W 特效尺寸")]
    [SerializeField] private Vector3 wEffectScale = new Vector3(0.3f, 0.3f, 0.3f);
    [InspectorName("W 尺寸倍率")]
    [SerializeField] private float wEffectScaleMultiplier = 1f;

    [Header("W - 星环剑轮 / 剑轮")]
    [InspectorName("W 初始剑数量")]
    [SerializeField] private int baseWSwordCount = 3;
    [InspectorName("W 使用剑气值")]
    [SerializeField] private bool useSwordEnergyForW = true;
    [InspectorName("W 最大剑数量")]
    [SerializeField] private int maxWSwordCount = 15;
    [InspectorName("W 环绕半径")]
    [SerializeField] private float wEffectOrbitRadius = 1.2f;
    [InspectorName("W 高度")]
    [SerializeField] private float wEffectHeight = 1.1f;
    [InspectorName("W 环绕速度")]
    [SerializeField] private float wEffectOrbitSpeed = 80f;
    [InspectorName("W 圆周切线 Yaw 偏移")]
    [SerializeField] private float wSwordOrbitYawOffset = 90f;

    [Header("W - 星环剑轮 / 神印加成")]
    [InspectorName("W 每点剑气增加持续时间")]
    [SerializeField] private float wDurationPerSwordEnergy = 0f;
    [InspectorName("W 最大持续时间加成")]
    [SerializeField] private float wMaxDurationBonus = 0f;
    [InspectorName("W 每点剑气增加环绕速度")]
    [SerializeField] private float wOrbitSpeedPerSwordEnergy = 0f;
    [InspectorName("W 最大环绕速度加成")]
    [SerializeField] private float wMaxOrbitSpeedBonus = 0f;
    [InspectorName("W 每点剑气增加半径")]
    [SerializeField] private float wRadiusPerSwordEnergy = 0f;
    [InspectorName("W 最大半径加成")]
    [SerializeField] private float wMaxRadiusBonus = 0f;

    [Header("W - 星环剑轮 / 预制体")]
    [InspectorName("W 通用特效预制体")]
    [SerializeField] private GameObject sharedSkillEffectPrefab;
    [InspectorName("W 专属特效预制体")]
    [SerializeField] private GameObject wSkillEffectPrefab;
    [InspectorName("W 待机特效预制体")]
    [SerializeField] private GameObject standbySkillEffectPrefab;

    [Header("W - 星环剑轮 / 护盾球体")]
    [InspectorName("W Spawn Shield Bubble")]
    [SerializeField] private bool wSpawnShieldBubble = true;
    [InspectorName("W Shield Bubble Prefab")]
    [SerializeField] private GameObject wShieldBubblePrefab;
    [InspectorName("W Shield Bubble Local Offset")]
    [SerializeField] private Vector3 wShieldBubbleLocalOffset = new Vector3(0f, 0.2f, 0f);
    [InspectorName("W Shield Bubble Scale")]
    [SerializeField] private Vector3 wShieldBubbleScale = new Vector3(1.2f, 1.5f, 1f);
    [InspectorName("W Shield Bubble Fade In Duration")]
    [SerializeField] private float wShieldBubbleFadeInDuration = 0.15f;
    [InspectorName("W Shield Bubble Fade Out Duration")]
    [SerializeField] private float wShieldBubbleFadeOutDuration = 0.15f;
    [InspectorName("W Shield Bubble Pulse Amount")]
    [SerializeField] private float wShieldBubblePulseAmount = 0.03f;
    [InspectorName("W Shield Bubble Pulse Speed")]
    [SerializeField] private float wShieldBubblePulseSpeed = 2f;

    private bool isShielding;
    private bool isWGuardActive;
    private float wOrbitAngle;
    private Coroutine wSkillRoutine;
    private float wAppliedShieldValue;
    private GameObject activeWOrbitVisualRoot;
    private GameObject activeWShieldBubble;
    private readonly List<SpriteRenderer> activeWShieldBubbleSpriteRenderers = new List<SpriteRenderer>();
    private readonly List<Color> activeWShieldBubbleSpriteBaseColors = new List<Color>();
    private readonly List<Renderer> activeWShieldBubbleMeshRenderers = new List<Renderer>();
    private readonly List<Color> activeWShieldBubbleMeshBaseColors = new List<Color>();
    private readonly List<GameObject> activeWSwords = new List<GameObject>();
    private int currentWSwordCount;
    private float currentWFinalDamageReduction;
    private RuneRuntimeState runeRuntimeState;

    public override float CooldownSeconds => cooldown;
    public override float ManaCost => manaCost;

    private sealed class WSkillEffectRuntime : MonoBehaviour
    {
        public Transform visual;
        public Quaternion baseVisualRotation = Quaternion.identity;
    }

    public override void Initialize(Player2PrototypeController owner)
    {
        base.Initialize(owner);
        SyncLegacyOwnerValuesIfNeeded();
    }

    public override bool Cast()
    {
        if (Owner == null)
        {
            return false;
        }

        if (wSkillRoutine != null)
        {
            StopCoroutine(wSkillRoutine);
            wSkillRoutine = null;
        }

        Cleanup();
        runeRuntimeState = ResolveRuneRuntimeState();
        runeRuntimeState?.NotifySkillCastStarted(1);
        wSkillRoutine = StartCoroutine(ShieldRoutine());
        Owner.GetComponentInChildren<Player2HaloRotateEffect>(true)?.TriggerSkillBoost();
        return true;
    }

    public override void Cleanup()
    {
        if (wSkillRoutine != null)
        {
            StopCoroutine(wSkillRoutine);
            wSkillRoutine = null;
        }

        for (int i = 0; i < activeWSwords.Count; i++)
        {
            GameObject sword = activeWSwords[i];
            if (sword != null)
            {
                Destroy(sword);
            }
        }
        activeWSwords.Clear();

        if (activeWOrbitVisualRoot != null)
        {
            Destroy(activeWOrbitVisualRoot);
            activeWOrbitVisualRoot = null;
        }

        DestroyWShieldBubbleImmediate();

        Transform[] allTransforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform tr = allTransforms[i];
            if (tr == null) continue;
            string n = tr.name;
            bool isLegacyW =
                n == "W_OrbitRoot" ||
                n == "W_OrbitVisualRoot" ||
                n == "W_Sword" ||
                n == "W_SwordInstance" ||
                n.StartsWith("W_Sword_") ||
                n.StartsWith("W_SwordPivot_");

            if (isLegacyW)
            {
                Destroy(tr.gameObject);
            }
        }

        isShielding = false;
        isWGuardActive = false;
        currentWSwordCount = 0;
        currentWFinalDamageReduction = 0f;
        ClearWShield();
    }

    private void OnDisable()
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    public override float ProcessIncomingDamageWithWGuard(float rawDamage, BattleDamage incomingDamage)
    {
        float clampedRaw = Mathf.Max(0f, rawDamage);
        if (!isWGuardActive)
        {
            return clampedRaw;
        }

        float blockedDamage = clampedRaw * currentWFinalDamageReduction;
        float damageAfterReduction = clampedRaw - blockedDamage;
        float counterDamage = blockedDamage * wCounterDamageRatio;

        Debug.Log($"[W Guard] Raw={clampedRaw:F2}, Blocked={blockedDamage:F2}, Taken={damageAfterReduction:F2}, Counter={counterDamage:F2}", this);
        ApplyWCounterDamage(incomingDamage, counterDamage);
        return Mathf.Max(0f, damageAfterReduction);
    }

    private RuneRuntimeState ResolveRuneRuntimeState()
    {
        if (Owner == null)
        {
            return GetComponent<RuneRuntimeState>() ?? GetComponentInParent<RuneRuntimeState>();
        }

        return Owner.GetComponent<RuneRuntimeState>() ?? Owner.GetComponentInParent<RuneRuntimeState>();
    }

    private IEnumerator ShieldRoutine()
    {
        isShielding = true;

        GameObject orbitRoot = new GameObject("W_OrbitVisualRoot");
        orbitRoot.transform.position = Owner != null ? Owner.transform.position : transform.position;
        orbitRoot.transform.rotation = Quaternion.identity;
        activeWOrbitVisualRoot = orbitRoot;
        GameObject shieldBubble = SpawnWShieldBubble(orbitRoot.transform);

        int energyForW = useSwordEnergyForW ? Mathf.Max(0, Owner != null ? Owner.currentSwordEnergy : 0) : 0;
        int swordCount = baseWSwordCount;
        if (useSwordEnergyForW)
        {
            swordCount += energyForW;
        }
        swordCount = Mathf.Clamp(swordCount, baseWSwordCount, maxWSwordCount);

        float finalDuration = wDuration + Mathf.Min(energyForW * wDurationPerSwordEnergy, wMaxDurationBonus);
        float finalOrbitSpeed = wEffectOrbitSpeed + Mathf.Min(energyForW * wOrbitSpeedPerSwordEnergy, wMaxOrbitSpeedBonus);
        float finalRadius = wEffectOrbitRadius + Mathf.Min(energyForW * wRadiusPerSwordEnergy, wMaxRadiusBonus);

        activeWSwords.Clear();
        for (int i = 0; i < swordCount; i++)
        {
            float angle = i * (360f / swordCount);
            Vector3 offset = GetOrbitPositionXZ(angle, finalRadius, wEffectHeight);
            Vector3 spawnBase = Owner != null ? Owner.transform.position : transform.position;
            Vector3 spawnPos = spawnBase + offset + ResolveWEffectOffset();

            GameObject sword = CreateSkillEffectVisual(
                $"W_Sword_{i}",
                ResolveWVisualPrefab(),
                spawnPos,
                offset,
                false,
                false,
                0f,
                ResolveStandbySwordVisualPitch(),
                ResolveStandbySwordVisualYaw(),
                ResolveStandbySwordVisualRoll() + ResolveSharedRotationZ(),
                ResolveWEffectScale());

            if (sword == null)
            {
                continue;
            }

            Quaternion extraRot = Quaternion.Euler(
                ResolveWEffectVisualPitch(),
                ResolveWEffectVisualYaw(),
                ResolveWEffectVisualRoll());
            sword.transform.rotation = sword.transform.rotation * extraRot;

            sword.transform.SetParent(orbitRoot.transform, true);

            WSkillEffectRuntime runtime = sword.GetComponent<WSkillEffectRuntime>();
            if (runtime != null && runtime.visual != null)
            {
                runtime.baseVisualRotation = runtime.visual.rotation;
                if (!ResolveUseRawPrefabRotationForSkillEffects())
                {
                    runtime.visual.localScale = Vector3.one;
                }
            }

            float sizeMul = Mathf.Max(0.01f, wEffectScaleMultiplier);
            Vector3 finalScale = Vector3.Scale(wEffectScale, Vector3.one * sizeMul);
            sword.transform.localScale = finalScale;
            Debug.Log($"[W Skill Scale] sword={sword.name}, baseScale={wEffectScale}, multiplier={sizeMul:F2}, finalScale={finalScale}", this);

            activeWSwords.Add(sword);
        }

        currentWSwordCount = activeWSwords.Count;
        currentWFinalDamageReduction = ComputeWFinalDamageReduction(currentWSwordCount);
        isWGuardActive = true;
        ApplyWShield(currentWSwordCount);

        Debug.Log($"[W Skill] Base={baseWSwordCount}, CurrentSwordEnergy={energyForW}, Spawned={activeWSwords.Count}, Duration={finalDuration:F2}, OrbitSpeed={finalOrbitSpeed:F2}, Radius={finalRadius:F2}, DamageReduction={currentWFinalDamageReduction:F2}", this);
        if (activeWSwords.Count > swordCount)
        {
            Debug.LogWarning($"[W Skill] Spawned sword count exceeded expected {swordCount}: {activeWSwords.Count}", this);
        }

        if (activeWSwords.Count == 0)
        {
            Cleanup();
            isShielding = false;
            wSkillRoutine = null;
            yield break;
        }

        wOrbitAngle = 0f;
        float t = 0f;
        while (t < finalDuration)
        {
            if (orbitRoot == null)
            {
                break;
            }

            orbitRoot.transform.position = Owner != null ? Owner.transform.position : transform.position;
            orbitRoot.transform.rotation = Quaternion.identity;

            wOrbitAngle += finalOrbitSpeed * Time.deltaTime;
            for (int i = 0; i < activeWSwords.Count; i++)
            {
                GameObject sword = activeWSwords[i];
                if (sword == null)
                {
                    continue;
                }

                float baseAngle = wOrbitAngle + i * (360f / swordCount);
                Vector3 offset = GetOrbitPositionXZ(baseAngle, finalRadius, wEffectHeight);
                Vector3 orbitBase = Owner != null ? Owner.transform.position : transform.position;
                sword.transform.position = orbitBase + offset + ResolveWEffectOffset();

                WSkillEffectRuntime runtime = sword.GetComponent<WSkillEffectRuntime>();
                if (runtime != null && runtime.visual != null)
                {
                    float yaw = GetOrbitTangentYawXZ(baseAngle) + wSwordOrbitYawOffset;
                    runtime.visual.rotation = Quaternion.Euler(0f, yaw, 0f) * runtime.baseVisualRotation;
                }
            }

            if (shieldBubble != null)
            {
                UpdateWShieldBubble(shieldBubble.transform, t);
            }

            t += Time.deltaTime;
            yield return null;
        }

        if (shieldBubble != null)
        {
            yield return FadeOutWShieldBubble(shieldBubble, orbitRoot != null ? orbitRoot.transform : null, wShieldBubbleFadeOutDuration);
        }

        Cleanup();
        isShielding = false;
        wSkillRoutine = null;
    }

    private GameObject SpawnWShieldBubble(Transform parent)
    {
        if (!wSpawnShieldBubble || wShieldBubblePrefab == null || parent == null)
        {
            activeWShieldBubble = null;
            activeWShieldBubbleSpriteRenderers.Clear();
            activeWShieldBubbleSpriteBaseColors.Clear();
            activeWShieldBubbleMeshRenderers.Clear();
            activeWShieldBubbleMeshBaseColors.Clear();
            return null;
        }

        GameObject bubble = Instantiate(wShieldBubblePrefab, parent);
        bubble.name = "W_ShieldBubble";
        bubble.transform.localPosition = wShieldBubbleLocalOffset;
        bubble.transform.localRotation = Quaternion.identity;
        bubble.transform.localScale = ClampVisualScale(wShieldBubbleScale);

        Collider[] colliders = bubble.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        activeWShieldBubbleSpriteRenderers.Clear();
        activeWShieldBubbleSpriteBaseColors.Clear();
        activeWShieldBubbleMeshRenderers.Clear();
        activeWShieldBubbleMeshBaseColors.Clear();

        SpriteRenderer[] spriteRenderers = bubble.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            spriteRenderer.sortingOrder = 10;
            Material spriteMaterial = spriteRenderer.material;
            if (spriteMaterial != null)
            {
                ConfigureWShieldBubbleMaterial(spriteMaterial);
            }
            activeWShieldBubbleSpriteRenderers.Add(spriteRenderer);
            activeWShieldBubbleSpriteBaseColors.Add(spriteRenderer.color);
            SetSpriteRendererAlpha(spriteRenderer, 0f);
        }

        Renderer[] renderers = bubble.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is SpriteRenderer)
            {
                continue;
            }

            renderer.sortingOrder = 10;
            ConfigureWShieldBubbleRenderer(renderer);
            activeWShieldBubbleMeshRenderers.Add(renderer);
            activeWShieldBubbleMeshBaseColors.Add(ResolveRendererBaseColor(renderer));
            SetRendererAlpha(renderer, 0f);
        }

        if (activeWShieldBubbleSpriteRenderers.Count == 0 && activeWShieldBubbleMeshRenderers.Count == 0)
        {
            Destroy(bubble);
            activeWShieldBubble = null;
            return null;
        }

        activeWShieldBubble = bubble;
        return bubble;
    }

    private static void ConfigureWShieldBubbleRenderer(Renderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sortingOrder = 10;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        Material[] materials = renderer.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            ConfigureWShieldBubbleMaterial(materials[i]);
        }
    }

    private static void ConfigureWShieldBubbleMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 0f);
        }

        if (material.HasProperty("_ZTest"))
        {
            material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        }
    }

    private void UpdateWShieldBubble(Transform bubbleTransform, float elapsed)
    {
        if (bubbleTransform == null || activeWShieldBubble == null)
        {
            return;
        }

        bubbleTransform.localPosition = wShieldBubbleLocalOffset;

        float pulse = 1f;
        if (wShieldBubblePulseAmount > 0f)
        {
            pulse = 1f + Mathf.Sin(elapsed * wShieldBubblePulseSpeed) * wShieldBubblePulseAmount;
        }

        bubbleTransform.localScale = Vector3.Scale(ClampVisualScale(wShieldBubbleScale), Vector3.one * pulse);

        if (activeWShieldBubbleSpriteRenderers.Count > 0 || activeWShieldBubbleMeshRenderers.Count > 0)
        {
            float fadeIn = wShieldBubbleFadeInDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / wShieldBubbleFadeInDuration);
            float alphaPulse = 1f;
            if (wShieldBubblePulseAmount > 0f)
            {
                alphaPulse = 1f + Mathf.Sin(elapsed * wShieldBubblePulseSpeed) * (wShieldBubblePulseAmount * 0.5f);
            }

            for (int i = 0; i < activeWShieldBubbleSpriteRenderers.Count; i++)
            {
                SpriteRenderer spriteRenderer = activeWShieldBubbleSpriteRenderers[i];
                if (spriteRenderer == null)
                {
                    continue;
                }

                Color baseColor = i < activeWShieldBubbleSpriteBaseColors.Count ? activeWShieldBubbleSpriteBaseColors[i] : spriteRenderer.color;
                float alpha = Mathf.Clamp01(baseColor.a * fadeIn * alphaPulse);
                SetSpriteRendererColor(spriteRenderer, baseColor, alpha);
            }

            for (int i = 0; i < activeWShieldBubbleMeshRenderers.Count; i++)
            {
                Renderer renderer = activeWShieldBubbleMeshRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Color baseColor = i < activeWShieldBubbleMeshBaseColors.Count ? activeWShieldBubbleMeshBaseColors[i] : ResolveRendererBaseColor(renderer);
                float alpha = Mathf.Clamp01(baseColor.a * fadeIn * alphaPulse);
                SetRendererColor(renderer, baseColor, alpha);
            }
        }
    }

    private IEnumerator FadeOutWShieldBubble(GameObject bubble, Transform followRoot, float duration)
    {
        if (bubble == null)
        {
            yield break;
        }

        if ((activeWShieldBubbleSpriteRenderers.Count == 0 && activeWShieldBubbleMeshRenderers.Count == 0) || duration <= 0f)
        {
            DestroyWShieldBubbleImmediate();
            yield break;
        }

        float startAlpha = 0f;
        for (int i = 0; i < activeWShieldBubbleSpriteRenderers.Count; i++)
        {
            SpriteRenderer spriteRenderer = activeWShieldBubbleSpriteRenderers[i];
            if (spriteRenderer != null)
            {
                startAlpha = Mathf.Max(startAlpha, spriteRenderer.color.a);
            }
        }
        for (int i = 0; i < activeWShieldBubbleMeshRenderers.Count; i++)
        {
            Renderer renderer = activeWShieldBubbleMeshRenderers[i];
            if (renderer != null)
            {
                startAlpha = Mathf.Max(startAlpha, ResolveRendererAlpha(renderer));
            }
        }
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            if (bubble == null)
            {
                yield break;
            }

            if (followRoot != null)
            {
                Vector3 orbitBase = Owner != null ? Owner.transform.position : transform.position;
                followRoot.position = orbitBase;
                followRoot.rotation = Quaternion.identity;
            }

            if (bubble.transform != null)
            {
                bubble.transform.localPosition = wShieldBubbleLocalOffset;
                float pulse = 1f;
                if (wShieldBubblePulseAmount > 0f)
                {
                    pulse = 1f + Mathf.Sin(elapsed * wShieldBubblePulseSpeed) * wShieldBubblePulseAmount;
                }

                bubble.transform.localScale = Vector3.Scale(ClampVisualScale(wShieldBubbleScale), Vector3.one * pulse);
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(startAlpha, 0f, t);
            for (int i = 0; i < activeWShieldBubbleSpriteRenderers.Count; i++)
            {
                SpriteRenderer spriteRenderer = activeWShieldBubbleSpriteRenderers[i];
                if (spriteRenderer == null)
                {
                    continue;
                }

                Color baseColor = i < activeWShieldBubbleSpriteBaseColors.Count ? activeWShieldBubbleSpriteBaseColors[i] : spriteRenderer.color;
                SetSpriteRendererColor(spriteRenderer, baseColor, alpha);
            }
            for (int i = 0; i < activeWShieldBubbleMeshRenderers.Count; i++)
            {
                Renderer renderer = activeWShieldBubbleMeshRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Color baseColor = i < activeWShieldBubbleMeshBaseColors.Count ? activeWShieldBubbleMeshBaseColors[i] : ResolveRendererBaseColor(renderer);
                SetRendererColor(renderer, baseColor, alpha);
            }
            yield return null;
        }

        DestroyWShieldBubbleImmediate();
    }

    private void DestroyWShieldBubbleImmediate()
    {
        if (activeWShieldBubble != null)
        {
            Destroy(activeWShieldBubble);
        }

        activeWShieldBubble = null;
        activeWShieldBubbleSpriteRenderers.Clear();
        activeWShieldBubbleSpriteBaseColors.Clear();
        activeWShieldBubbleMeshRenderers.Clear();
        activeWShieldBubbleMeshBaseColors.Clear();
    }

    private static void SetSpriteRendererColor(SpriteRenderer spriteRenderer, Color baseColor, float alpha)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Color color = baseColor;
        color.a = Mathf.Clamp01(alpha);
        spriteRenderer.color = color;
    }

    private static void SetSpriteRendererAlpha(SpriteRenderer spriteRenderer, float alpha)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Color color = spriteRenderer.color;
        color.a = Mathf.Clamp01(alpha);
        spriteRenderer.color = color;
    }

    private static void SetRendererAlpha(Renderer renderer, float alpha)
    {
        if (renderer == null)
        {
            return;
        }

        SetRendererColor(renderer, ResolveRendererBaseColor(renderer), alpha);
    }

    private static Color ResolveRendererBaseColor(Renderer renderer)
    {
        if (renderer == null)
        {
            return Color.white;
        }

        Material[] materials = renderer.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (TryGetMaterialColor(material, out Color color))
            {
                return color;
            }
        }

        return Color.white;
    }

    private static float ResolveRendererAlpha(Renderer renderer)
    {
        return ResolveRendererBaseColor(renderer).a;
    }

    private static void SetRendererColor(Renderer renderer, Color baseColor, float alpha)
    {
        if (renderer == null)
        {
            return;
        }

        Material[] materials = renderer.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            SetMaterialColor(materials[i], baseColor, alpha);
        }
    }

    private static bool TryGetMaterialColor(Material material, out Color color)
    {
        if (material == null)
        {
            color = Color.white;
            return false;
        }

        if (material.HasProperty("_BaseColor"))
        {
            color = material.GetColor("_BaseColor");
            return true;
        }

        if (material.HasProperty("_Color"))
        {
            color = material.GetColor("_Color");
            return true;
        }

        if (material.HasProperty("_TintColor"))
        {
            color = material.GetColor("_TintColor");
            return true;
        }

        color = Color.white;
        return false;
    }

    private static void SetMaterialColor(Material material, Color baseColor, float alpha)
    {
        if (material == null)
        {
            return;
        }

        Color color = baseColor;
        color.a = Mathf.Clamp01(alpha);

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_TintColor"))
        {
            material.SetColor("_TintColor", color);
        }

        if (material.HasProperty("_Alpha"))
        {
            material.SetFloat("_Alpha", color.a);
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
            Vector3 combinedScale = Vector3.Scale(ClampVisualScale(ResolveSharedEffectScale()), ClampVisualScale(visualScale));
            visualTarget.localScale = Vector3.Scale(visualTarget.localScale, combinedScale);
        }
        EnsureEffectVisible(effectVisual);

        WSkillEffectRuntime runtime = root.AddComponent<WSkillEffectRuntime>();
        runtime.visual = visualTarget;
        runtime.baseVisualRotation = visualTarget.rotation;

        return root;
    }

    private GameObject CreateEffectInstance(string effectName, GameObject specificPrefab, Vector3 position, Quaternion rotation, bool preservePrefabRotation)
    {
        GameObject sourcePrefab = ResolvePrefabCandidate(specificPrefab);
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

        Debug.LogWarning($"[Player2Skill_W_HolyWheelDeflection] Missing skill effect prefab for '{effectName}' on {name}. Assign a specific prefab, or set it on the owner controller.", this);
        return null;
    }

    private GameObject ResolvePrefabCandidate(GameObject localPrefab)
    {
        if (localPrefab != null)
        {
            return localPrefab;
        }

        if (Owner != null)
        {
            if (ResolveStandbySkillEffectPrefab() != null)
            {
                return ResolveStandbySkillEffectPrefab();
            }

            if (ResolveWSkillEffectPrefab() != null)
            {
                return ResolveWSkillEffectPrefab();
            }

            return Owner.sharedSkillEffectPrefab;
        }

        return null;
    }

    private GameObject ResolveWVisualPrefab()
    {
        GameObject local = ResolveStandbySkillEffectPrefab();
        if (local != null)
        {
            return local;
        }

        local = ResolveWSkillEffectPrefab();
        if (local != null)
        {
            return local;
        }

        if (Owner != null)
        {
            if (Owner.standbySkillEffectPrefab != null)
            {
                return Owner.standbySkillEffectPrefab;
            }

            if (Owner.wSkillEffectPrefab != null)
            {
                return Owner.wSkillEffectPrefab;
            }

            return Owner.sharedSkillEffectPrefab;
        }

        return sharedSkillEffectPrefab;
    }

    private GameObject ResolveSharedSkillEffectPrefab()
    {
        if (sharedSkillEffectPrefab != null)
        {
            return sharedSkillEffectPrefab;
        }

        return Owner != null ? Owner.sharedSkillEffectPrefab : null;
    }

    private GameObject ResolveWSkillEffectPrefab()
    {
        if (wSkillEffectPrefab != null)
        {
            return wSkillEffectPrefab;
        }

        return Owner != null ? Owner.wSkillEffectPrefab : null;
    }

    private GameObject ResolveStandbySkillEffectPrefab()
    {
        if (standbySkillEffectPrefab != null)
        {
            return standbySkillEffectPrefab;
        }

        return Owner != null ? Owner.standbySkillEffectPrefab : null;
    }

    private Vector3 ResolveWEffectScale()
    {
        return ResolveOwnerVector3(Owner != null ? Owner.wEffectScale : Vector3.one, wEffectScale);
    }

    private Vector3 ResolveWEffectOffset()
    {
        return Owner != null ? Owner.wEffectOffset : Vector3.zero;
    }

    private Vector3 ResolveSharedEffectScale()
    {
        return Owner != null ? Owner.sharedEffectScale : Vector3.one;
    }

    private float ResolveSharedRotationZ()
    {
        return Owner != null ? Owner.sharedEffectRotationZ : 0f;
    }

    private bool ResolveUseRawPrefabRotationForSkillEffects()
    {
        return Owner == null || Owner.useRawPrefabRotationForSkillEffects;
    }

    private float ResolveSkillEffectPrefabScaleMultiplier()
    {
        return Owner != null ? Owner.skillEffectPrefabScaleMultiplier : 1f;
    }

    private float ResolveStandbySwordVisualPitch()
    {
        return Owner != null ? Owner.standbySwordVisualPitch : 0f;
    }

    private float ResolveStandbySwordVisualYaw()
    {
        return Owner != null ? Owner.standbySwordVisualYaw : 0f;
    }

    private float ResolveStandbySwordVisualRoll()
    {
        return Owner != null ? Owner.standbySwordVisualRoll : 0f;
    }

    private float ResolveWEffectVisualPitch()
    {
        return Owner != null ? Owner.wEffectVisualPitch : 0f;
    }

    private float ResolveWEffectVisualYaw()
    {
        return Owner != null ? Owner.wEffectVisualYaw : 0f;
    }

    private float ResolveWEffectVisualRoll()
    {
        return Owner != null ? Owner.wEffectVisualRoll : 0f;
    }

    private static Vector3 ResolveOwnerVector3(Vector3 ownerValue, Vector3 localValue)
    {
        if (localValue.sqrMagnitude > 0.0001f)
        {
            return localValue;
        }

        if (ownerValue.sqrMagnitude > 0.0001f)
        {
            return ownerValue;
        }

        return Vector3.one;
    }

    private void SyncLegacyOwnerValuesIfNeeded()
    {
        if (Owner == null)
        {
            return;
        }

        if (Approximately(wDuration, 1.5f)) wDuration = Owner.wDuration;
        if (Approximately(wDamageReduction, 0.4f)) wDamageReduction = Owner.wDamageReduction;
        if (Approximately(wDamageReductionPerSword, 0.03f)) wDamageReductionPerSword = Owner.wDamageReductionPerSword;
        if (Approximately(wMaxDamageReduction, 0.8f)) wMaxDamageReduction = Owner.wMaxDamageReduction;
        if (Approximately(wCounterDamageRatio, 0.5f)) wCounterDamageRatio = Owner.wCounterDamageRatio;
        if (Approximately(wEffectScale, new Vector3(0.3f, 0.3f, 0.3f))) wEffectScale = Owner.wEffectScale;
        if (Approximately(wEffectScaleMultiplier, 1f)) wEffectScaleMultiplier = Owner.wEffectScaleMultiplier;
        if (baseWSwordCount == 3) baseWSwordCount = Owner.baseWSwordCount;
        if (useSwordEnergyForW) useSwordEnergyForW = Owner.useSwordEnergyForW;
        if (maxWSwordCount == 15) maxWSwordCount = Owner.maxWSwordCount;
        if (Approximately(wEffectOrbitRadius, 1.2f)) wEffectOrbitRadius = Owner.wEffectOrbitRadius;
        if (Approximately(wEffectHeight, 1.1f)) wEffectHeight = Owner.wEffectHeight;
        if (Approximately(wEffectOrbitSpeed, 80f)) wEffectOrbitSpeed = Owner.wEffectOrbitSpeed;
        if (Approximately(wSwordOrbitYawOffset, 90f)) wSwordOrbitYawOffset = Owner.wSwordOrbitYawOffset;
        if (Approximately(wDurationPerSwordEnergy, 0f)) wDurationPerSwordEnergy = Owner.wDurationPerSwordEnergy;
        if (Approximately(wMaxDurationBonus, 0f)) wMaxDurationBonus = Owner.wMaxDurationBonus;
        if (Approximately(wOrbitSpeedPerSwordEnergy, 0f)) wOrbitSpeedPerSwordEnergy = Owner.wOrbitSpeedPerSwordEnergy;
        if (Approximately(wMaxOrbitSpeedBonus, 0f)) wMaxOrbitSpeedBonus = Owner.wMaxOrbitSpeedBonus;
        if (Approximately(wRadiusPerSwordEnergy, 0f)) wRadiusPerSwordEnergy = Owner.wRadiusPerSwordEnergy;
        if (Approximately(wMaxRadiusBonus, 0f)) wMaxRadiusBonus = Owner.wMaxRadiusBonus;
    }

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.0001f;
    }

    private static bool Approximately(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude <= 0.0001f;
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

    private float ComputeWFinalDamageReduction(int wSwordCountAtCast)
    {
        float reduction = wDamageReduction + Mathf.Max(0, wSwordCountAtCast) * wDamageReductionPerSword;
        return Mathf.Clamp(reduction, 0f, wMaxDamageReduction);
    }

    private void ApplyWCounterDamage(BattleDamage incomingDamage, float counterDamage)
    {
        if (counterDamage <= 0f)
        {
            return;
        }

        GameObject attacker = incomingDamage.source;
        if (attacker == null)
        {
            Debug.LogWarning("[W Guard] Counter requires attacker/source reference in BattleDamage.", this);
            return;
        }

        if (attacker == gameObject)
        {
            return;
        }

        CombatHealth attackerCombatHealth = attacker.GetComponentInParent<CombatHealth>();
        if (attackerCombatHealth != null && attackerCombatHealth.gameObject != gameObject)
        {
            attackerCombatHealth.TakeDamage(new BattleDamage(counterDamage, incomingDamage.damageType, gameObject));
            return;
        }

        EnemyHealth attackerEnemyHealth = attacker.GetComponentInParent<EnemyHealth>();
        if (attackerEnemyHealth != null && attackerEnemyHealth.gameObject != gameObject)
        {
            int roundedDamage = Mathf.Max(1, Mathf.RoundToInt(counterDamage));
            attackerEnemyHealth.TakeDamage(roundedDamage, gameObject);
            return;
        }

        Debug.LogWarning($"[W Guard] Attacker '{attacker.name}' has no CombatHealth/EnemyHealth for counter damage.", this);
    }

    private void ApplyWShield(int currentSwordCount)
    {
        if (Owner == null)
        {
            return;
        }

        CombatHealth combatHealth = Owner.GetComponent<CombatHealth>();
        if (combatHealth == null)
        {
            Debug.LogWarning("[W Shield] Owner has no CombatHealth shield receiver.", this);
            return;
        }

        float maxHp = ResolveOwnerMaxHp();
        int extraSwordCount = Mathf.Max(0, currentSwordCount - baseWSwordCount);
        float baseShield = Mathf.Max(0f, maxHp * wShieldMaxHpMultiplier);
        wAppliedShieldValue = Mathf.Max(0f, baseShield * (1f + extraSwordCount * wShieldBonusPerExtraSword));
        combatHealth.SetShield(wAppliedShieldValue);
        Debug.Log($"[W Shield] Applied shield={wAppliedShieldValue:F2}, baseShield={baseShield:F2}, maxHp={maxHp:F2}, extraSwordCount={extraSwordCount}, bonusPerSword={wShieldBonusPerExtraSword:F2}", this);
    }

    private void ClearWShield()
    {
        if (!wClearShieldOnEnd)
        {
            return;
        }

        if (Owner == null)
        {
            wAppliedShieldValue = 0f;
            return;
        }

        CombatHealth combatHealth = Owner.GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            combatHealth.ClearShield();
            Debug.Log($"[W Shield] Cleared shield, previousApplied={wAppliedShieldValue:F2}", this);
        }

        wAppliedShieldValue = 0f;
    }

    private float ResolveOwnerMaxHp()
    {
        if (Owner == null)
        {
            return 0f;
        }

        CombatHealth combatHealth = Owner.GetComponent<CombatHealth>();
        if (combatHealth != null)
        {
            if (combatHealth.resourceBank != null)
            {
                return Mathf.Max(0f, combatHealth.resourceBank.maxHealth);
            }

            if (combatHealth.stats != null)
            {
                return Mathf.Max(0f, combatHealth.stats.maxHealth);
            }

            return Mathf.Max(0f, combatHealth.currentHealth);
        }

        BattleResourceBank resourceBank = Owner.GetComponent<BattleResourceBank>();
        if (resourceBank != null)
        {
            return Mathf.Max(0f, resourceBank.maxHealth);
        }

        CombatStats stats = Owner.GetComponent<CombatStats>();
        if (stats != null)
        {
            return Mathf.Max(0f, stats.maxHealth);
        }

        return 0f;
    }
}
