using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Spine.Unity;

public class DissolveOnDeath : MonoBehaviour
{
    [Header("Dissolve")]
    [SerializeField] private DissolveOnDeathProfile dissolveProfile;
    [SerializeField] private Shader dissolveShaderOverride;
    [SerializeField, Min(0.05f)] private float dissolveDuration = 1.1f;
    [SerializeField] private bool destroyAfterDissolve = true;
    [SerializeField] private bool disableCollidersOnDeath = true;
    [SerializeField] private bool disableBehavioursOnDeath = true;
    [SerializeField] private bool hideHealthBarOnDeath = true;
    [SerializeField] private string dissolvePropertyName = "_DissolveAmount";
    [SerializeField] private float dissolveNoise = 12f;
    [SerializeField] private Color edgeColor = new Color(1f, 0.62f, 0.18f, 1f);
    [SerializeField, Range(0.01f, 0.5f)] private float edgeWidth = 0.12f;
    [SerializeField, Min(0f)] private float emissionStrength = 2.8f;
    [SerializeField] private AnimationCurve dissolveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private bool debugLog = false;
    [FormerlySerializedAs("optionalDeathVfxPrefab")]
    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private Vector3 deathVfxOffset = new Vector3(0f, 0.15f, 0f);
    [SerializeField] private bool parentVfxToOwner = false;
    [SerializeField, Min(0.1f)] private float vfxAutoDestroyDelay = 2f;
    [SerializeField] private bool useDifferentVfxForPlayer = false;
    [SerializeField, Min(0f)] private float delayBeforeDestroy = 0f;
    [Header("Renderer Filters")]
    [SerializeField] private Transform[] includeRendererRoots;
    [SerializeField] private Transform[] excludeRendererRoots;

    private readonly List<RendererBinding> rendererBindings = new List<RendererBinding>();
    private readonly List<Behaviour> disabledBehaviours = new List<Behaviour>();
    private readonly List<Collider> disabledColliders3D = new List<Collider>();
    private readonly List<Collider2D> disabledColliders2D = new List<Collider2D>();
    private readonly HashSet<int> warnedMessages = new HashSet<int>();

    private CombatHealth combatHealth;
    private CombatHealth hookedCombatHealth;
    private RuntimeLootDropOnDeath lootDrop;
    private bool deathSequenceStarted;
    private bool healthHooked;
    private Coroutine deathRoutine;
    private Coroutine healthBindingRoutine;
    private Shader dissolveShader;
    private int dissolveAmountId;
    private int dissolveNoiseId;
    private int edgeColorId;
    private int edgeWidthId;
    private int emissionStrengthId;
    private bool loggedRendererDiagnostics;
    private bool deathFinished;
    private bool keepDissolvedOnDestroy;
    private float nextDissolveLogTime;
    private GameObject spawnedDeathVfx;
    private const float dissolveLogInterval = 0.3f;

    public bool IsDeathStarted => deathSequenceStarted;
    public bool IsDeathFinished => deathFinished;

    private struct RendererBinding
    {
        public Renderer renderer;
        public Material[] sourceMaterials;
        public Material[] runtimeMaterials;
        public bool hasSpriteColor;
        public Color sourceSpriteColor;
    }

    private void Awake()
    {
        CacheDependencies();
        CacheShaderIds();
        CacheRenderers();
        CaptureSelfExclusions();
        EnsureHealthBindings();
        SyncDestroyOnDeathState(false);
    }

    private void OnEnable()
    {
        EnsureHealthBindings();
        SyncDestroyOnDeathState(false);
        TryStartIfAlreadyDead();
    }

    private void Start()
    {
        EnsureHealthBindings();
        TryStartIfAlreadyDead();
    }

    private void LateUpdate()
    {
        if (deathFinished && keepDissolvedOnDestroy)
        {
            FinalHideOwnerVisuals();
        }
    }

    private void OnDisable()
    {
        if (debugLog && deathSequenceStarted)
        {
            Debug.Log($"[DissolveOnDeath] OnDisable owner={name} deathFinished={deathFinished} activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy}", this);
        }

        if (deathSequenceStarted && !deathFinished)
        {
            Debug.LogWarning($"[DissolveOnDeath] Interrupted owner={name} before Finish", this);
        }

        UnhookHealthEvents();
    }

    private void OnDestroy()
    {
        if (debugLog && deathSequenceStarted)
        {
            Debug.Log($"[DissolveOnDeath] OnDestroy owner={name} deathFinished={deathFinished}", this);
        }

        if (deathSequenceStarted && !deathFinished)
        {
            Debug.LogWarning($"[DissolveOnDeath] Interrupted owner={name} during OnDestroy before Finish", this);
        }

        UnhookHealthEvents();
        CleanupRuntimeMaterials();
    }

    public void EnsureHealthBindings()
    {
        HookHealthEvents();

        if (combatHealth == null && isActiveAndEnabled && healthBindingRoutine == null)
        {
            healthBindingRoutine = StartCoroutine(EnsureHealthBindingsRoutine());
        }
    }

    private void CacheDependencies()
    {
        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
            if (combatHealth == null)
            {
                combatHealth = GetComponentInParent<CombatHealth>();
            }
        }

        if (lootDrop == null)
        {
            lootDrop = GetComponent<RuntimeLootDropOnDeath>();
            if (lootDrop == null)
            {
                lootDrop = GetComponentInParent<RuntimeLootDropOnDeath>();
            }
        }
    }

    private void CacheShaderIds()
    {
        dissolveAmountId = Shader.PropertyToID(dissolvePropertyName);
        dissolveNoiseId = Shader.PropertyToID("_DissolveNoise");
        edgeColorId = Shader.PropertyToID("_EdgeColor");
        edgeWidthId = Shader.PropertyToID("_EdgeWidth");
        emissionStrengthId = Shader.PropertyToID("_EmissionStrength");
    }

    private void CacheRenderers()
    {
        rendererBindings.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            WarnOnce(1, $"[DissolveOnDeath] No Renderer found on {name}. The object will still be destroyed after death.");
            return;
        }

        dissolveShader = ResolveDissolveShader();
        if (dissolveShader == null)
        {
            WarnOnce(2, "[DissolveOnDeath] Shader 'BattleFight/DeathDissolveURP' was not found. Dissolve visuals will not render.");
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (ShouldSkipRenderer(renderer, out string skipReason))
            {
                if (!string.IsNullOrEmpty(skipReason) && skipReason.IndexOf("HealthBar", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    DebugLog($"[DissolveOnDeath] skippedHealthBar owner={name} renderer={renderer.name} reason={skipReason}");
                }

                continue;
            }

            Material[] sourceMaterials = renderer.sharedMaterials;
            if (sourceMaterials == null || sourceMaterials.Length == 0)
            {
                continue;
            }

            rendererBindings.Add(new RendererBinding
            {
                renderer = renderer,
                sourceMaterials = sourceMaterials,
                runtimeMaterials = null,
                hasSpriteColor = renderer is SpriteRenderer,
                sourceSpriteColor = renderer is SpriteRenderer spriteRenderer ? spriteRenderer.color : Color.white
            });
        }
    }

    private bool ShouldSkipRenderer(Renderer renderer, out string reason)
    {
        reason = null;
        if (renderer == null)
        {
            reason = "NullRenderer";
            return true;
        }

        Transform rendererTransform = renderer.transform;
        if (renderer is ParticleSystemRenderer)
        {
            reason = "ParticleSystemRenderer";
            return true;
        }

        if (IsHealthBarRenderer(rendererTransform))
        {
            reason = "HealthBarRenderer";
            return true;
        }

        if (IsUnderExcludedRendererRoot(rendererTransform))
        {
            reason = "ExcludedRendererRoot";
            return true;
        }

        if (!IsUnderIncludedRendererRoot(rendererTransform))
        {
            reason = "OutsideIncludedRendererRoots";
            return true;
        }

        return false;
    }

    private Shader ResolveDissolveShader()
    {
        if (dissolveProfile != null && dissolveProfile.dissolveShader != null)
        {
            return dissolveProfile.dissolveShader;
        }

        if (dissolveShaderOverride != null)
        {
            return dissolveShaderOverride;
        }

        return Shader.Find("BattleFight/DeathDissolveURP");
    }

    private bool IsUnderIncludedRendererRoot(Transform rendererTransform)
    {
        if (!HasConfiguredRendererRoots(includeRendererRoots))
        {
            return true;
        }

        return IsUnderAnyRendererRoot(rendererTransform, includeRendererRoots);
    }

    private bool IsUnderExcludedRendererRoot(Transform rendererTransform)
    {
        return HasConfiguredRendererRoots(excludeRendererRoots) &&
               IsUnderAnyRendererRoot(rendererTransform, excludeRendererRoots);
    }

    private static bool HasConfiguredRendererRoots(Transform[] rendererRoots)
    {
        if (rendererRoots == null)
        {
            return false;
        }

        for (int i = 0; i < rendererRoots.Length; i++)
        {
            if (rendererRoots[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUnderAnyRendererRoot(Transform rendererTransform, Transform[] rendererRoots)
    {
        if (rendererTransform == null || rendererRoots == null)
        {
            return false;
        }

        for (int i = 0; i < rendererRoots.Length; i++)
        {
            Transform rendererRoot = rendererRoots[i];
            if (rendererRoot != null && rendererTransform.IsChildOf(rendererRoot))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHealthBarRenderer(Transform rendererTransform)
    {
        if (rendererTransform == null)
        {
            return false;
        }

        WorldHealthBar[] parentHealthBars = rendererTransform.GetComponentsInParent<WorldHealthBar>(true);
        for (int i = 0; i < parentHealthBars.Length; i++)
        {
            WorldHealthBar worldHealthBar = parentHealthBars[i];
            if (worldHealthBar == null)
            {
                continue;
            }

            if (worldHealthBar.barInstanceRoot != null && rendererTransform.IsChildOf(worldHealthBar.barInstanceRoot))
            {
                return true;
            }
        }

        Transform current = rendererTransform;
        while (current != null)
        {
            if (string.Equals(current.name, "WorldHealthBar", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.parent;
        }

        bool isHealthBarPartName =
            string.Equals(rendererTransform.name, "Background", StringComparison.Ordinal) ||
            string.Equals(rendererTransform.name, "Fill", StringComparison.Ordinal);
        if (!isHealthBarPartName)
        {
            return false;
        }

        Transform parent = rendererTransform.parent;
        return parent != null && string.Equals(parent.name, "WorldHealthBar", StringComparison.Ordinal);
    }

    private void CopySourceMaterialProperties(Renderer renderer, Material sourceMaterial, Material runtimeMaterial)
    {
        if (sourceMaterial == null || runtimeMaterial == null)
        {
            return;
        }

        string copiedTexturePropertyName;
        Texture mainTexture = ResolveSourceMainTexture(renderer, sourceMaterial, out copiedTexturePropertyName);
        if (mainTexture != null)
        {
            runtimeMaterial.mainTexture = mainTexture;
            runtimeMaterial.SetTexture("_MainTex", mainTexture);
            runtimeMaterial.SetTexture("_BaseMap", mainTexture);
        }

        Vector2 mainScale = sourceMaterial.mainTextureScale;
        Vector2 mainOffset = sourceMaterial.mainTextureOffset;
        runtimeMaterial.mainTextureScale = mainScale;
        runtimeMaterial.mainTextureOffset = mainOffset;

        if (sourceMaterial.HasProperty("_MainTex"))
        {
            runtimeMaterial.SetTexture("_MainTex", sourceMaterial.GetTexture("_MainTex") ?? mainTexture);
            runtimeMaterial.SetTextureScale("_MainTex", sourceMaterial.GetTextureScale("_MainTex"));
            runtimeMaterial.SetTextureOffset("_MainTex", sourceMaterial.GetTextureOffset("_MainTex"));
        }

        if (sourceMaterial.HasProperty("_BaseMap"))
        {
            runtimeMaterial.SetTexture("_BaseMap", sourceMaterial.GetTexture("_BaseMap") ?? mainTexture);
            runtimeMaterial.SetTextureScale("_BaseMap", sourceMaterial.GetTextureScale("_BaseMap"));
            runtimeMaterial.SetTextureOffset("_BaseMap", sourceMaterial.GetTextureOffset("_BaseMap"));
        }

        if (sourceMaterial.HasProperty("_Color"))
        {
            runtimeMaterial.SetColor("_Color", sourceMaterial.GetColor("_Color"));
        }
        else
        {
            runtimeMaterial.SetColor("_Color", Color.white);
        }

        if (sourceMaterial.HasProperty("_BaseColor"))
        {
            runtimeMaterial.SetColor("_BaseColor", sourceMaterial.GetColor("_BaseColor"));
        }
        else
        {
            runtimeMaterial.SetColor("_BaseColor", Color.white);
        }

        if (sourceMaterial.HasProperty("_RendererColor"))
        {
            runtimeMaterial.SetColor("_RendererColor", sourceMaterial.GetColor("_RendererColor"));
        }
        else if (renderer is SpriteRenderer rendererSprite)
        {
            runtimeMaterial.SetColor("_RendererColor", rendererSprite.color);
        }
        else
        {
            runtimeMaterial.SetColor("_RendererColor", Color.white);
        }

        if (runtimeMaterial.HasProperty("_AlphaClip"))
        {
            runtimeMaterial.SetFloat("_AlphaClip", 1f);
        }

        if (debugLog)
        {
            Texture runtimeMainTexture = runtimeMaterial.HasProperty("_MainTex")
                ? runtimeMaterial.GetTexture("_MainTex")
                : runtimeMaterial.mainTexture;
            string copiedTextureName = mainTexture != null ? mainTexture.name : "<null>";
            string runtimeTextureName = runtimeMainTexture != null ? runtimeMainTexture.name : "<null>";
            DebugLog(
                $"[DissolveOnDeath] MaterialCopy owner={name} renderer={renderer.name} originalMaterial={sourceMaterial.name} originalShader={(sourceMaterial.shader != null ? sourceMaterial.shader.name : "<null>")} copiedTexturePropertyName={copiedTexturePropertyName} copiedTextureName={copiedTextureName} runtimeMainTex={runtimeTextureName} runtimeMainTexAssigned={(runtimeMainTexture != null)}");
        }
    }

    private static readonly string[] MainTexturePropertyCandidates =
    {
        "_MainTex",
        "_BaseMap",
        "_Texture",
        "_AtlasTex",
        "_PageTex",
        "_Diffuse"
    };

    private Texture ResolveSourceMainTexture(Renderer renderer, Material sourceMaterial, out string copiedTexturePropertyName)
    {
        copiedTexturePropertyName = "<none>";
        if (sourceMaterial == null)
        {
            return null;
        }

        for (int i = 0; i < MainTexturePropertyCandidates.Length; i++)
        {
            string propertyName = MainTexturePropertyCandidates[i];
            if (!sourceMaterial.HasProperty(propertyName))
            {
                continue;
            }

            Texture propertyTexture = sourceMaterial.GetTexture(propertyName);
            if (propertyTexture != null)
            {
                copiedTexturePropertyName = propertyName;
                return propertyTexture;
            }
        }

        string[] texturePropertyNames = sourceMaterial.GetTexturePropertyNames();
        for (int i = 0; i < texturePropertyNames.Length; i++)
        {
            string propertyName = texturePropertyNames[i];
            Texture propertyTexture = sourceMaterial.GetTexture(propertyName);
            if (propertyTexture != null)
            {
                copiedTexturePropertyName = propertyName;
                return propertyTexture;
            }
        }

        if (renderer is SpriteRenderer spriteRenderer &&
            spriteRenderer.sprite != null &&
            spriteRenderer.sprite.texture != null)
        {
            copiedTexturePropertyName = "SpriteRenderer.sprite.texture";
            return spriteRenderer.sprite.texture;
        }

        if (sourceMaterial.mainTexture != null)
        {
            copiedTexturePropertyName = "mainTexture";
            return sourceMaterial.mainTexture;
        }

        return null;
    }

    private void CaptureSelfExclusions()
    {
        disabledBehaviours.Clear();
        disabledColliders3D.Clear();
        disabledColliders2D.Clear();

        if (disableBehavioursOnDeath)
        {
            Behaviour[] behaviours = GetComponentsInChildren<Behaviour>(true);
            foreach (Behaviour behaviour in behaviours)
            {
                if (ShouldKeepBehaviour(behaviour))
                {
                    continue;
                }

                if (behaviour != null)
                {
                    disabledBehaviours.Add(behaviour);
                }
            }
        }

        if (disableCollidersOnDeath)
        {
            disabledColliders3D.AddRange(GetComponentsInChildren<Collider>(true));
            disabledColliders2D.AddRange(GetComponentsInChildren<Collider2D>(true));
        }
    }

    private static bool ShouldKeepBehaviour(Behaviour behaviour)
    {
        if (behaviour == null)
        {
            return true;
        }

        if (behaviour is DissolveOnDeath)
        {
            return true;
        }

        if (behaviour is CombatHealth || behaviour is RuntimeLootDropOnDeath || behaviour is WorldHealthBar)
        {
            return true;
        }

        if (behaviour is Animator || behaviour is SkeletonAnimation || behaviour is SkeletonMecanim || behaviour is SkeletonRenderer || behaviour is SkeletonGraphic)
        {
            return true;
        }

        return false;
    }

    private void HookHealthEvents()
    {
        CacheDependencies();
        bool hasAnyBinding = false;

        if (hookedCombatHealth != null && hookedCombatHealth != combatHealth)
        {
            hookedCombatHealth.Died -= HandleDied;
            hookedCombatHealth = null;
        }

        if (combatHealth != null)
        {
            combatHealth.Died -= HandleDied;
            combatHealth.Died += HandleDied;
            hookedCombatHealth = combatHealth;
            hasAnyBinding = true;
        }
        else if (hookedCombatHealth != null)
        {
            hookedCombatHealth.Died -= HandleDied;
            hookedCombatHealth = null;
        }

        healthHooked = hasAnyBinding;
        DebugLog($"[DissolveOnDeath] Bind owner={name} combatHealth={(combatHealth != null)}");
    }

    private void UnhookHealthEvents()
    {
        if (hookedCombatHealth != null)
        {
            hookedCombatHealth.Died -= HandleDied;
            hookedCombatHealth = null;
        }

        healthHooked = false;

        if (healthBindingRoutine != null)
        {
            StopCoroutine(healthBindingRoutine);
            healthBindingRoutine = null;
        }
    }

    private void SyncDestroyOnDeathState(bool value)
    {
        if (combatHealth != null)
        {
            combatHealth.destroyOnDeath = value;
        }
    }

    private void TryStartIfAlreadyDead()
    {
        EnsureHealthBindings();

        if (deathSequenceStarted)
        {
            return;
        }

        bool combatDead = combatHealth != null && combatHealth.IsDead;
        if (combatDead)
        {
            BeginDeathSequence();
        }
    }

    private void HandleDied(GameObject killer)
    {
        BeginDeathSequence();
    }

    private IEnumerator EnsureHealthBindingsRoutine()
    {
        int attempts = 0;
        while (!deathSequenceStarted && isActiveAndEnabled && attempts < 30)
        {
            if (healthHooked)
            {
                break;
            }

            HookHealthEvents();
            if (healthHooked)
            {
                break;
            }

            attempts++;
            yield return null;
        }

        healthBindingRoutine = null;
    }

    private void BeginDeathSequence()
    {
        if (deathSequenceStarted)
        {
            return;
        }

        deathSequenceStarted = true;
        deathFinished = false;
        keepDissolvedOnDestroy = false;
        loggedRendererDiagnostics = false;
        nextDissolveLogTime = 0f;
        DebugLog($"[DissolveOnDeath] Begin owner={name} hasCombatHealth={(combatHealth != null)}");
        SyncDestroyOnDeathState(false);
        PrepareRuntimeMaterials();
        LogRendererDiagnostics();
        ApplyDissolveProperties(0f);

        if (deathRoutine != null)
        {
            StopCoroutine(deathRoutine);
        }

        deathRoutine = StartCoroutine(DeathRoutine());
        DebugLog($"[DissolveOnDeath] CoroutineStarted owner={name} routineActive={(deathRoutine != null)}");
    }

    private IEnumerator DeathRoutine()
    {
        SpawnDeathVfx();

        DisableGameplaySystems();

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, ResolveDissolveDuration());
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            ApplyDissolveProperties(EvaluateDissolveAmount(t));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        ApplyDissolveProperties(1f);
        FinalHideOwnerVisuals();

        if (delayBeforeDestroy > 0f)
        {
            yield return new WaitForSecondsRealtime(delayBeforeDestroy);
        }

        DebugLog($"[DissolveOnDeath] Finish owner={name} destroyAfterDissolve={destroyAfterDissolve}");
        deathFinished = true;
        keepDissolvedOnDestroy = true;
        DetachSpawnedDeathVfx();
        if (destroyAfterDissolve)
        {
            FinalHideOwnerVisuals();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void DisableGameplaySystems()
    {
        FreezePhysicsBodies();

        if (hideHealthBarOnDeath)
        {
            WorldHealthBar[] worldHealthBars = GetComponentsInChildren<WorldHealthBar>(true);
            for (int i = 0; i < worldHealthBars.Length; i++)
            {
                WorldHealthBar worldHealthBar = worldHealthBars[i];
                if (worldHealthBar == null)
                {
                    continue;
                }

                if (worldHealthBar.barInstanceRoot != null)
                {
                    worldHealthBar.barInstanceRoot.gameObject.SetActive(false);
                    continue;
                }

                if (worldHealthBar.backgroundRenderer != null)
                {
                    worldHealthBar.backgroundRenderer.enabled = false;
                }

                if (worldHealthBar.fillRenderer != null)
                {
                    worldHealthBar.fillRenderer.enabled = false;
                }
            }
        }

        if (disableCollidersOnDeath)
        {
            for (int i = 0; i < disabledColliders3D.Count; i++)
            {
                Collider collider = disabledColliders3D[i];
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }

            for (int i = 0; i < disabledColliders2D.Count; i++)
            {
                Collider2D collider2D = disabledColliders2D[i];
                if (collider2D != null)
                {
                    collider2D.enabled = false;
                }
            }
        }

        if (!disableBehavioursOnDeath)
        {
            return;
        }

        for (int i = 0; i < disabledBehaviours.Count; i++)
        {
            Behaviour behaviour = disabledBehaviours[i];
            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }
    }

    private void FreezePhysicsBodies()
    {
        Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody rigidbody = rigidbodies[i];
            if (rigidbody == null)
            {
                continue;
            }

            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
        }

        Rigidbody2D[] rigidbodies2D = GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < rigidbodies2D.Length; i++)
        {
            Rigidbody2D rigidbody2D = rigidbodies2D[i];
            if (rigidbody2D == null)
            {
                continue;
            }

            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.angularVelocity = 0f;
            rigidbody2D.gravityScale = 0f;
            rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private void SpawnDeathVfx()
    {
        GameObject resolvedDeathVfxPrefab = ResolveDeathVfxPrefab();
        if (resolvedDeathVfxPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = ResolveDeathVfxSpawnPosition() + ResolveDeathVfxOffset();
        Quaternion spawnRotation = Quaternion.identity;
        if (ResolveParentVfxToOwner())
        {
            spawnedDeathVfx = Instantiate(resolvedDeathVfxPrefab, transform);
            spawnedDeathVfx.transform.localPosition = ResolveDeathVfxOffset();
            spawnedDeathVfx.transform.localRotation = Quaternion.identity;
            spawnedDeathVfx.transform.localScale = Vector3.one;
        }
        else
        {
            spawnedDeathVfx = Instantiate(resolvedDeathVfxPrefab, spawnPosition, spawnRotation);
        }

        if (spawnedDeathVfx == null)
        {
            return;
        }

        DeathDissolveVfxController vfxController = spawnedDeathVfx.GetComponent<DeathDissolveVfxController>();
        if (vfxController != null)
        {
            vfxController.Play(ResolveVfxAutoDestroyDelay(), ResolveDeathVfxSortingOrder());
        }
        else
        {
            float safeDestroyDelay = Mathf.Max(0.1f, ResolveVfxAutoDestroyDelay());
            Destroy(spawnedDeathVfx, safeDestroyDelay);
        }
    }

    private Vector3 ResolveDeathVfxSpawnPosition()
    {
        Bounds bounds = default;
        bool hasBounds = false;
        for (int i = 0; i < rendererBindings.Count; i++)
        {
            Renderer renderer = rendererBindings[i].renderer;
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds ? bounds.center : transform.position;
    }

    private int ResolveDeathVfxSortingOrder()
    {
        int maxSortingOrder = 0;
        bool foundRenderer = false;
        for (int i = 0; i < rendererBindings.Count; i++)
        {
            Renderer renderer = rendererBindings[i].renderer;
            if (renderer == null)
            {
                continue;
            }

            maxSortingOrder = foundRenderer ? Mathf.Max(maxSortingOrder, renderer.sortingOrder) : renderer.sortingOrder;
            foundRenderer = true;
        }

        return foundRenderer ? maxSortingOrder + 1 : 1;
    }

    private void DetachSpawnedDeathVfx()
    {
        if (!ResolveParentVfxToOwner() || spawnedDeathVfx == null)
        {
            return;
        }

        spawnedDeathVfx.transform.SetParent(null, true);
        spawnedDeathVfx = null;
    }

    private void FinalHideOwnerVisuals()
    {
        ApplyDissolveProperties(1f);

        for (int bindingIndex = 0; bindingIndex < rendererBindings.Count; bindingIndex++)
        {
            RendererBinding binding = rendererBindings[bindingIndex];
            Renderer renderer = binding.renderer;
            if (renderer != null)
            {
                renderer.enabled = false;

                if (binding.hasSpriteColor && renderer is SpriteRenderer spriteRenderer)
                {
                    Color hiddenColor = spriteRenderer.color;
                    hiddenColor.a = 0f;
                    spriteRenderer.color = hiddenColor;
                }

                GameObject rendererObject = renderer.gameObject;
                if (rendererObject != null && rendererObject != gameObject)
                {
                    rendererObject.SetActive(false);
                }
            }
        }
    }

    private void ApplyDissolveProperties(float amount)
    {
        if (rendererBindings.Count == 0)
        {
            return;
        }

        float clampedAmount = Mathf.Clamp01(amount);
        if (Time.unscaledTime >= nextDissolveLogTime || clampedAmount >= 1f)
        {
            DebugLog($"[DissolveOnDeath] Progress owner={name} dissolveAmount={clampedAmount:0.00} rendererCount={rendererBindings.Count}");
            nextDissolveLogTime = Time.unscaledTime + dissolveLogInterval;
        }

        for (int bindingIndex = 0; bindingIndex < rendererBindings.Count; bindingIndex++)
        {
            RendererBinding binding = rendererBindings[bindingIndex];
            if (binding.runtimeMaterials == null)
            {
                continue;
            }

            EnsureRuntimeMaterialsApplied(ref binding);
            rendererBindings[bindingIndex] = binding;

            if (binding.hasSpriteColor && binding.renderer is SpriteRenderer spriteRenderer)
            {
                spriteRenderer.color = binding.sourceSpriteColor;
            }

            for (int materialIndex = 0; materialIndex < binding.runtimeMaterials.Length; materialIndex++)
            {
                Material material = binding.runtimeMaterials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                material.SetFloat(dissolveAmountId, clampedAmount);
                material.SetFloat(dissolveNoiseId, Mathf.Max(0.01f, ResolveDissolveNoise()));
                material.SetColor(edgeColorId, ResolveEdgeColor());
                material.SetFloat(edgeWidthId, Mathf.Max(0.001f, ResolveEdgeWidth()));
                material.SetFloat(emissionStrengthId, Mathf.Max(0f, ResolveEmissionStrength()));
            }
        }
    }

    private void EnsureRuntimeMaterialsApplied(ref RendererBinding binding)
    {
        Renderer renderer = binding.renderer;
        if (!deathSequenceStarted || renderer == null || binding.runtimeMaterials == null || binding.runtimeMaterials.Length == 0)
        {
            return;
        }

        Material[] currentMaterials = renderer.sharedMaterials;
        bool materialsRestored = AreRuntimeMaterialsMissing(currentMaterials, binding.runtimeMaterials);

        if (debugLog)
        {
            string currentShaderName = ResolveFirstShaderName(currentMaterials);
            DebugLog(
                $"[DissolveOnDeath] Apply owner={name} renderer={renderer.name} currentMaterialShader={currentShaderName} materialsRestored={materialsRestored}");
        }

        if (!materialsRestored)
        {
            return;
        }

        if (renderer is SpriteRenderer spriteRenderer && binding.runtimeMaterials[0] != null)
        {
            spriteRenderer.material = binding.runtimeMaterials[0];
        }
        else
        {
            renderer.materials = binding.runtimeMaterials;
        }

        if (debugLog)
        {
            string reappliedShaderName = ResolveFirstShaderName(renderer.sharedMaterials);
            DebugLog(
                $"[DissolveOnDeath] ReapplyRuntimeMaterials owner={name} renderer={renderer.name} reappliedShader={reappliedShaderName}");
        }
    }

    private static bool AreRuntimeMaterialsMissing(Material[] currentMaterials, Material[] runtimeMaterials)
    {
        if (runtimeMaterials == null || runtimeMaterials.Length == 0)
        {
            return false;
        }

        if (currentMaterials == null || currentMaterials.Length != runtimeMaterials.Length)
        {
            return true;
        }

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            Material runtimeMaterial = runtimeMaterials[i];
            Material currentMaterial = currentMaterials[i];
            if (runtimeMaterial == null)
            {
                continue;
            }

            if (!ReferenceEquals(currentMaterial, runtimeMaterial))
            {
                return true;
            }
        }

        return false;
    }

    private static string ResolveFirstShaderName(Material[] materials)
    {
        if (materials == null || materials.Length == 0)
        {
            return "<none>";
        }

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null)
            {
                return material.shader != null ? material.shader.name : "<null shader>";
            }
        }

        return "<null material>";
    }

    private float ResolveDissolveDuration()
    {
        return dissolveProfile != null ? dissolveProfile.dissolveDuration : dissolveDuration;
    }

    private float ResolveDissolveNoise()
    {
        return dissolveProfile != null ? dissolveProfile.dissolveNoise : dissolveNoise;
    }

    private Color ResolveEdgeColor()
    {
        return dissolveProfile != null ? dissolveProfile.edgeColor : edgeColor;
    }

    private float ResolveEdgeWidth()
    {
        return dissolveProfile != null ? dissolveProfile.edgeWidth : edgeWidth;
    }

    private float ResolveEmissionStrength()
    {
        return dissolveProfile != null ? dissolveProfile.emissionStrength : emissionStrength;
    }

    private GameObject ResolveDeathVfxPrefab()
    {
        if (deathVfxPrefab != null)
        {
            return deathVfxPrefab;
        }

        return dissolveProfile != null ? dissolveProfile.deathVfxPrefab : null;
    }

    private Vector3 ResolveDeathVfxOffset()
    {
        return dissolveProfile != null ? dissolveProfile.deathVfxOffset : deathVfxOffset;
    }

    private bool ResolveParentVfxToOwner()
    {
        return dissolveProfile != null ? dissolveProfile.parentVfxToOwner : parentVfxToOwner;
    }

    private float ResolveVfxAutoDestroyDelay()
    {
        return dissolveProfile != null ? dissolveProfile.vfxAutoDestroyDelay : vfxAutoDestroyDelay;
    }

    private float EvaluateDissolveAmount(float normalizedTime)
    {
        float clampedTime = Mathf.Clamp01(normalizedTime);
        AnimationCurve curve = dissolveProfile != null ? dissolveProfile.dissolveCurve : dissolveCurve;
        if (curve == null || curve.length == 0)
        {
            return clampedTime;
        }

        return Mathf.Clamp01(curve.Evaluate(clampedTime));
    }

    private void LogRendererDiagnostics()
    {
        if (loggedRendererDiagnostics)
        {
            return;
        }

        loggedRendererDiagnostics = true;
        DebugLog($"[DissolveOnDeath] Renderers owner={name} rendererCount={rendererBindings.Count}");
        for (int i = 0; i < rendererBindings.Count; i++)
        {
            RendererBinding binding = rendererBindings[i];
            Renderer renderer = binding.renderer;
            if (renderer == null)
            {
                continue;
            }

            string rendererType = renderer.GetType().Name;
            string sourceShaderName = "<none>";
            string runtimeShaderName = "<none>";
            bool hasDissolveProperty = false;
            bool createdInstance = binding.runtimeMaterials != null;

            if (binding.sourceMaterials != null && binding.sourceMaterials.Length > 0 && binding.sourceMaterials[0] != null)
            {
                sourceShaderName = binding.sourceMaterials[0].shader != null ? binding.sourceMaterials[0].shader.name : "<null shader>";
            }

            if (binding.runtimeMaterials != null && binding.runtimeMaterials.Length > 0 && binding.runtimeMaterials[0] != null)
            {
                runtimeShaderName = binding.runtimeMaterials[0].shader != null ? binding.runtimeMaterials[0].shader.name : "<null shader>";
                hasDissolveProperty = binding.runtimeMaterials[0].HasProperty(dissolveAmountId);
            }

            DebugLog(
                $"[DissolveOnDeath] Renderer owner={name} renderer={renderer.name} type={rendererType} enabled={renderer.enabled} active={renderer.gameObject.activeInHierarchy} sourceShader={sourceShaderName} runtimeShader={runtimeShaderName} hasDissolveProperty={hasDissolveProperty} createdInstance={createdInstance}");
        }
    }

    private void PrepareRuntimeMaterials()
    {
        if (dissolveShader == null || rendererBindings.Count == 0)
        {
            return;
        }

        for (int bindingIndex = 0; bindingIndex < rendererBindings.Count; bindingIndex++)
        {
            RendererBinding binding = rendererBindings[bindingIndex];
            if (binding.sourceMaterials == null || binding.sourceMaterials.Length == 0)
            {
                continue;
            }

            Material[] clonedMaterials = new Material[binding.sourceMaterials.Length];
            bool hasAnyMaterial = false;
            for (int materialIndex = 0; materialIndex < binding.sourceMaterials.Length; materialIndex++)
            {
                Material sourceMaterial = binding.sourceMaterials[materialIndex];
                if (sourceMaterial == null)
                {
                    continue;
                }

                Material runtimeMaterial = new Material(dissolveShader)
                {
                    name = $"{sourceMaterial.name} (Dissolve Instance)",
                    renderQueue = sourceMaterial.renderQueue >= 0 ? sourceMaterial.renderQueue : dissolveShader.renderQueue
                };

                CopySourceMaterialProperties(binding.renderer, sourceMaterial, runtimeMaterial);
                clonedMaterials[materialIndex] = runtimeMaterial;
                hasAnyMaterial = true;
            }

            if (!hasAnyMaterial)
            {
                continue;
            }

            binding.runtimeMaterials = clonedMaterials;
            rendererBindings[bindingIndex] = binding;

            if (binding.renderer is SpriteRenderer spriteRenderer && clonedMaterials.Length > 0 && clonedMaterials[0] != null)
            {
                spriteRenderer.material = clonedMaterials[0];
            }
            else
            {
                binding.renderer.materials = clonedMaterials;
            }
        }
    }

    private void CleanupRuntimeMaterials()
    {
        if (deathSequenceStarted && deathFinished && keepDissolvedOnDestroy)
        {
            return;
        }

        for (int bindingIndex = 0; bindingIndex < rendererBindings.Count; bindingIndex++)
        {
            RendererBinding binding = rendererBindings[bindingIndex];
            if (binding.runtimeMaterials == null)
            {
                continue;
            }

            for (int materialIndex = 0; materialIndex < binding.runtimeMaterials.Length; materialIndex++)
            {
                Material material = binding.runtimeMaterials[materialIndex];
                if (material != null)
                {
                    Destroy(material);
                }
            }
        }
    }

    private void WarnOnce(int key, string message)
    {
        if (warnedMessages.Contains(key))
        {
            return;
        }

        warnedMessages.Add(key);
        Debug.LogWarning(message, this);
    }

    private void DebugLog(string message)
    {
        if (!debugLog)
        {
            return;
        }

        Debug.Log(message, this);
    }
}
