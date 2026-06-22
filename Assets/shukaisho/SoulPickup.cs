using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SoulPickup : MonoBehaviour
{
    private const bool EnableHaloRingVisuals = false;

    [Header("Soul")]
    public SoulType soulType = SoulType.Life;
    [Min(0f)] public float amount = 1f;
    public bool destroyAfterPickup = true;

    [Header("Auto Absorb")]
    [SerializeField, Min(0f)] private float absorbDelay = 0.2f;
    [SerializeField, Min(0f)] private float absorbSpeed = 8f;
    [SerializeField, Min(0f)] private float pickupDistance = 0.35f;
    [SerializeField, Min(0f)] private float hoverHeight = 0.6f;
    [SerializeField, Min(0f)] private float hoverAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float hoverSpeed = 3f;
    [SerializeField, Min(0f)] private float targetChestHeight = 1f;

    [Header("Visuals")]
    [SerializeField] private bool useRuntimeGeneratedParticles = false;
    [SerializeField] private ParticleSystem coreParticles;
    [SerializeField] private ParticleSystem trailParticles;
    [SerializeField] private Transform haloRingTransform;
    [SerializeField] private SpriteRenderer haloRingRenderer;
    [SerializeField, Min(0)] private int mainBurstCount = 24;
    [SerializeField, Min(0)] private int trailBurstCount = 12;
    [SerializeField, Min(0f)] private float mainEmissionRate = 80f;
    [SerializeField, Min(0f)] private float trailEmissionRate = 5f;
    [SerializeField, Min(0f)] private float trailAbsorbEmissionRate = 90f;
    [SerializeField] private Vector3 haloRingLocalScale = new Vector3(0.8f, 0.8f, 1f);
    [SerializeField, Min(0f)] private float haloRingRotationSpeed = -60f;
    [SerializeField, Min(0)] private int sortingOrder = 50;
    [SerializeField] private string sortingLayerName = "Default";

    private static Material sharedSoulMaterial;
    private static Texture2D sharedSoulTexture;
    private static Texture2D sharedHaloRingTexture;
    private static Sprite sharedHaloRingSprite;

    private Player2Bootstrap cachedBootstrap;
    private Collider pickupCollider;
    private Renderer[] cachedRenderers;
    private Vector3 spawnPosition;
    private float spawnTime;
    private bool absorbed;
    private bool absorbTargetLogged;
    private bool isAbsorbing;

    private void Awake()
    {
        spawnPosition = transform.position;
        spawnTime = Time.time;

        CacheReferences();
        EnsureTriggerCollider();
        EnsureVisuals();

        Debug.Log($"[SoulOrb] Spawn type={soulType} amount={amount:F2}", this);
    }

    private void OnEnable()
    {
        spawnPosition = transform.position;
        spawnTime = Time.time;
        absorbed = false;
        absorbTargetLogged = false;
        isAbsorbing = false;

        CacheReferences();
        EnsureTriggerCollider();
        EnsureVisuals();
        PlayParticles();
        LogParticleState("spawned");
    }

    private void OnDisable()
    {
        StopParticles(true);
    }

    private void OnValidate()
    {
        CacheReferences();
        BindExistingParticles();
    }

    private void Update()
    {
        if (absorbed)
        {
            return;
        }

        Transform target = ResolveCurrentPlayerTransform();
        Vector3 hoverOffset = GetHoverOffset();

        if (target == null)
        {
            SetAbsorbingVisualState(false);
            transform.position = Vector3.Lerp(transform.position, spawnPosition + hoverOffset, Time.deltaTime * Mathf.Max(1f, absorbSpeed * 0.25f));
            return;
        }

        if (!absorbTargetLogged)
        {
            absorbTargetLogged = true;
            Debug.Log($"[SoulOrb] Start absorb target={target.name}", this);
        }

        if (Time.time < spawnTime + absorbDelay)
        {
            SetAbsorbingVisualState(false);
            transform.position = Vector3.Lerp(transform.position, spawnPosition + hoverOffset, Time.deltaTime * Mathf.Max(1f, absorbSpeed * 0.25f));
            return;
        }

        SetAbsorbingVisualState(true);
        Vector3 targetPoint = target.position + Vector3.up * targetChestHeight;
        Vector3 moveTarget = Vector3.Lerp(targetPoint + hoverOffset * 0.15f, targetPoint, 0.75f);
        transform.position = Vector3.MoveTowards(transform.position, moveTarget, absorbSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPoint) <= pickupDistance)
        {
            ApplySoulToTarget(target);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (absorbed || other == null)
        {
            return;
        }

        Transform target = ResolveCurrentPlayerTransform();
        if (target == null || other.transform.root != target.root)
        {
            return;
        }

        if (Time.time < spawnTime + absorbDelay)
        {
            return;
        }

        ApplySoulToTarget(target);
    }

    public void Configure(SoulType type, float soulAmount)
    {
        soulType = type;
        amount = soulAmount;
        RefreshVisualColors();

        if (Application.isPlaying && isActiveAndEnabled)
        {
            StopParticles(true);
            PlayParticles();
        }
    }

    private void ApplySoulToTarget(Transform target)
    {
        if (absorbed)
        {
            return;
        }

        BattleResourceBank bank = ResolveTargetResourceBank(target);
        if (bank == null)
        {
            return;
        }

        Debug.Log($"[SoulOrb] Apply effect type={soulType} target={bank.name}", this);
        bank.ApplySoul(soulType, amount);
        absorbed = true;
        Debug.Log($"[SoulOrb] Absorbed type={soulType} target={bank.name}", this);

        if (destroyAfterPickup)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private Transform ResolveCurrentPlayerTransform()
    {
        if (cachedBootstrap == null)
        {
            cachedBootstrap = FindObjectOfType<Player2Bootstrap>();
        }

        if (cachedBootstrap != null && cachedBootstrap.CurrentPlayerTransform != null && cachedBootstrap.CurrentPlayerTransform.gameObject.activeInHierarchy)
        {
            return cachedBootstrap.CurrentPlayerTransform;
        }

        GameObject activePlayer = GameObject.FindWithTag("Player");
        if (activePlayer != null && activePlayer.activeInHierarchy)
        {
            return activePlayer.transform;
        }

        GameObject player01 = FindSceneObjectByNameIncludingInactive("Player01");
        if (player01 != null && player01.activeInHierarchy)
        {
            return player01.transform;
        }

        GameObject player02 = FindSceneObjectByNameIncludingInactive("Player02");
        if (player02 != null && player02.activeInHierarchy)
        {
            return player02.transform;
        }

        return null;
    }

    private BattleResourceBank ResolveTargetResourceBank(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        return target.GetComponentInParent<BattleResourceBank>();
    }

    private Vector3 GetHoverOffset()
    {
        float time = Time.time * Mathf.Max(0.01f, hoverSpeed);
        float x = Mathf.Sin(time * 1.07f) * hoverAmplitude;
        float y = hoverHeight + Mathf.Sin(time * 1.33f) * hoverAmplitude * 0.5f;
        float z = Mathf.Cos(time * 0.91f) * hoverAmplitude * 0.35f;
        return new Vector3(x, y, z);
    }

    private void CacheReferences()
    {
        pickupCollider = GetComponent<Collider>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        if (cachedBootstrap == null)
        {
            cachedBootstrap = FindObjectOfType<Player2Bootstrap>();
        }
    }

    private void EnsureTriggerCollider()
    {
        if (pickupCollider == null)
        {
            pickupCollider = GetComponent<Collider>();
        }

        if (pickupCollider != null)
        {
            pickupCollider.isTrigger = true;
        }
    }

    private void EnsureVisuals()
    {
        ConfigureVisuals();
    }

    private void BindExistingParticles()
    {
        if (coreParticles != null && trailParticles != null)
        {
            return;
        }

        Transform core = transform.Find("SoulCoreParticles");
        if (core != null && coreParticles == null)
        {
            coreParticles = core.GetComponent<ParticleSystem>();
        }

        Transform trail = transform.Find("SoulTrailParticles");
        if (trail != null && trailParticles == null)
        {
            trailParticles = trail.GetComponent<ParticleSystem>();
        }

        if (coreParticles != null && trailParticles != null)
        {
            return;
        }

        ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
        if (systems == null || systems.Length == 0)
        {
            return;
        }

        if (coreParticles == null)
        {
            coreParticles = systems[0];
        }

        if (trailParticles == null)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != coreParticles)
                {
                    trailParticles = systems[i];
                    break;
                }
            }
        }
    }

    private ParticleSystem CreateRuntimeParticle(string childName, bool isCore)
    {
        Transform child = transform.Find(childName);
        if (child != null)
        {
            ParticleSystem existing = child.GetComponent<ParticleSystem>();
            if (existing != null)
            {
                return existing;
            }
        }

        GameObject go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psRenderer = go.GetComponent<ParticleSystemRenderer>();
        if (psRenderer != null)
        {
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            psRenderer.sortingLayerName = sortingLayerName;
            psRenderer.sortingOrder = sortingOrder + (isCore ? 2 : 0);
            psRenderer.sharedMaterial = GetSharedSoulMaterial();
        }

        ConfigureParticle(ps, isCore);
        return ps;
    }

    private Transform CreateRuntimeHaloRing()
    {
        if (!EnableHaloRingVisuals)
        {
            return null;
        }

        GameObject go = new GameObject("SoulHaloRing");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = haloRingLocalScale;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = 120;
        renderer.sprite = LoadHaloRingSprite();
        renderer.color = Color.white;
        return go.transform;
    }

    private void ConfigureVisuals()
    {
        BindExistingParticles();

        if (coreParticles == null || trailParticles == null)
        {
            if (useRuntimeGeneratedParticles)
            {
                if (coreParticles == null)
                {
                    coreParticles = CreateRuntimeParticle("SoulCoreParticles", true);
                }

                if (EnableHaloRingVisuals && haloRingRenderer == null)
                {
                    haloRingTransform = CreateRuntimeHaloRing();
                    haloRingRenderer = haloRingTransform != null ? haloRingTransform.GetComponent<SpriteRenderer>() : null;
                }

                if (trailParticles == null)
                {
                    trailParticles = CreateRuntimeParticle("SoulTrailParticles", false);
                }
            }
            else
            {
                if (coreParticles == null)
                {
                    Debug.LogWarning("[SoulOrb] Missing coreParticles reference", this);
                }

                if (trailParticles == null)
                {
                    Debug.LogWarning("[SoulOrb] Missing trailParticles reference", this);
                }

            }
        }

        if (useRuntimeGeneratedParticles)
        {
            if (coreParticles != null || trailParticles != null)
            {
                if (EnableHaloRingVisuals)
                {
                    ApplyHaloRingTransform();
                    ConfigureHaloRing();
                }
            }
        }

        RefreshVisualColors();
    }

    private void ApplyHaloRingTransform()
    {
        if (!EnableHaloRingVisuals)
        {
            return;
        }

        if (haloRingTransform == null)
        {
            return;
        }

        haloRingTransform.localPosition = Vector3.zero;
        haloRingTransform.localScale = haloRingLocalScale;
        haloRingTransform.localRotation = Quaternion.identity;
        haloRingTransform.gameObject.SetActive(true);

        if (haloRingRenderer != null)
        {
            haloRingRenderer.sortingLayerName = sortingLayerName;
            haloRingRenderer.sortingOrder = 120;
            haloRingRenderer.enabled = true;
        }
    }

    private void ConfigureHaloRing()
    {
        if (!EnableHaloRingVisuals)
        {
            return;
        }

        if (haloRingRenderer == null)
        {
            return;
        }

        haloRingRenderer.enabled = true;

        if (haloRingRenderer.sprite == null)
        {
            haloRingRenderer.sprite = LoadHaloRingSprite();
        }

        haloRingRenderer.sortingLayerName = sortingLayerName;
        haloRingRenderer.sortingOrder = 120;
    }

    private void UpdateHaloRingRotation()
    {
        if (!EnableHaloRingVisuals)
        {
            return;
        }

        if (haloRingTransform == null || !haloRingTransform.gameObject.activeSelf)
        {
            return;
        }

        haloRingTransform.Rotate(0f, 0f, haloRingRotationSpeed * Time.deltaTime, Space.Self);
    }

    private void ConfigureParticle(ParticleSystem ps, bool isCore)
    {
        if (ps == null)
        {
            return;
        }

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = isCore ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        main.startDelay = 0f;
        main.startLifetime = isCore ? new ParticleSystem.MinMaxCurve(0.85f) : new ParticleSystem.MinMaxCurve(0.6f);
        main.startSpeed = isCore ? new ParticleSystem.MinMaxCurve(0.01f) : new ParticleSystem.MinMaxCurve(0.02f);
        main.startSize = isCore ? new ParticleSystem.MinMaxCurve(0.075f) : new ParticleSystem.MinMaxCurve(0.055f);
        main.maxParticles = isCore ? 120 : 220;
        main.scalingMode = ParticleSystemScalingMode.Local;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        if (isCore)
        {
            emission.rateOverTime = mainEmissionRate;
            emission.rateOverDistance = 0f;
        }
        else
        {
            emission.rateOverTime = trailEmissionRate;
            emission.rateOverDistance = 0f;
        }

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = isCore ? 0.055f : 0.02f;
        shape.radiusThickness = isCore ? 0.95f : 1f;
        shape.randomDirectionAmount = isCore ? 0.18f : 0.16f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.25f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.8f, 0.3f),
                new GradientAlphaKey(0.7f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(isCore ? 1.05f : 0.88f);

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        if (isCore)
        {
            velocity.x = new ParticleSystem.MinMaxCurve(-0.024f, 0.024f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.012f, 0.04f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.024f, 0.024f);
        }
        else
        {
            velocity.x = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0.02f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.01f, 0.01f);
        }

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = isCore ? 0.01f : 0.004f;
        noise.frequency = isCore ? 0.45f : 0.6f;
        noise.scrollSpeed = isCore ? 0.06f : 0.08f;
        noise.damping = true;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder + (isCore ? 2 : 0);
            // prefabs provide M_SoulParticle; keep existing material binding.
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.lengthScale = 1f;
            renderer.velocityScale = isCore ? 0.2f : 0.18f;
            renderer.cameraVelocityScale = 0f;
            renderer.enableGPUInstancing = true;
        }
    }

    private void SetAbsorbingVisualState(bool absorbing)
    {
        if (isAbsorbing == absorbing)
        {
            return;
        }

        isAbsorbing = absorbing;

        if (trailParticles == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = trailParticles.emission;
        emission.rateOverTime = absorbing ? trailAbsorbEmissionRate : trailEmissionRate;
            emission.rateOverDistance = absorbing ? 35f : 0f;

        ParticleSystemRenderer renderer = trailParticles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = 1f;
            renderer.velocityScale = 0.12f;
            renderer.cameraVelocityScale = 0f;
        }

    }

    private void RefreshVisualColors()
    {
        Color tint = ResolveSoulColor(soulType);
        if (coreParticles != null)
        {
            ParticleSystem.MainModule main = coreParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(tint.r, tint.g, tint.b, 1f));
        }

        if (haloRingRenderer != null)
        {
            if (EnableHaloRingVisuals)
            {
                haloRingRenderer.enabled = true;
                Color ringColor = new Color(tint.r, tint.g, tint.b, 1f);
                haloRingRenderer.color = ringColor;
            }
        }

        if (trailParticles != null)
        {
            ParticleSystem.MainModule trail = trailParticles.main;
            Color trailColor = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(tint.a * 0.7f));
            trail.startColor = new ParticleSystem.MinMaxGradient(trailColor);
        }
    }

    private void LogParticleState(string stage)
    {
        Debug.Log($"[SoulOrb] {stage} prefab={name} coreParticles={(coreParticles != null ? coreParticles.name : "null")} haloRing={(haloRingRenderer != null ? haloRingRenderer.name : "null")} trailParticles={(trailParticles != null ? trailParticles.name : "null")} core playing={(coreParticles != null && coreParticles.isPlaying)} haloRing active={(EnableHaloRingVisuals && haloRingTransform != null && haloRingTransform.gameObject.activeSelf)} trail playing={(trailParticles != null && trailParticles.isPlaying)}", this);
    }

    private void LogHaloState()
    {
        if (!EnableHaloRingVisuals)
        {
            return;
        }

        if (haloRingRenderer == null)
        {
            Debug.Log("[SoulOrb Halo] active=false sprite=null rendererEnabled=false color=null alpha=0 scale=0 sortingOrder=0 cameraFacing=false", this);
            return;
        }
    }

    private void PlayParticles()
    {
        if (coreParticles != null)
        {
            coreParticles.Clear(true);
            coreParticles.Play(true);
            coreParticles.Emit(mainBurstCount);
        }

        if (trailParticles != null)
        {
            trailParticles.Clear(true);
            trailParticles.Play(true);
            trailParticles.Emit(trailBurstCount);
        }
    }

    private void StopParticles(bool clear)
    {
        if (coreParticles != null)
        {
            coreParticles.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            if (clear)
            {
                coreParticles.Clear(true);
            }
        }

        if (trailParticles != null)
        {
            trailParticles.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            if (clear)
            {
                trailParticles.Clear(true);
            }
        }
    }

    private static Sprite LoadHaloRingSprite()
    {
#if UNITY_EDITOR
        Sprite editorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/素材/特效/光圈2.png");
        if (editorSprite != null)
        {
            return editorSprite;
        }
#endif

        if (sharedHaloRingSprite != null)
        {
            return sharedHaloRingSprite;
        }

        Texture2D texture = GetSharedHaloRingTexture();
        sharedHaloRingSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
        sharedHaloRingSprite.name = "RuntimeSoulHaloRing";
        return sharedHaloRingSprite;
    }

    private static Material GetSharedSoulMaterial()
    {
#if UNITY_EDITOR
        Material editorMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Effects/M_SoulParticle.mat");
        if (editorMaterial != null)
        {
            return editorMaterial;
        }
#endif

        if (sharedSoulMaterial != null)
        {
            return sharedSoulMaterial;
        }

        Shader shader = Shader.Find("BattleFight/Player01EGhostParticle")
                     ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Sprites/Default")
                     ?? Shader.Find("Unlit/Transparent")
                     ?? Shader.Find("Standard");

        sharedSoulMaterial = new Material(shader)
        {
            name = "RuntimeSoulParticleMaterial"
        };

        if (sharedSoulMaterial.HasProperty("_BaseMap"))
        {
            sharedSoulMaterial.SetTexture("_BaseMap", GetSharedSoulTexture());
        }

        if (sharedSoulMaterial.HasProperty("_MainTex"))
        {
            sharedSoulMaterial.SetTexture("_MainTex", GetSharedSoulTexture());
        }

        sharedSoulMaterial.renderQueue = 3000;
        return sharedSoulMaterial;
    }

    private static Texture2D GetSharedSoulTexture()
    {
        if (sharedSoulTexture != null)
        {
            return sharedSoulTexture;
        }

        const int size = 64;
        sharedSoulTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        sharedSoulTexture.name = "RuntimeSoulSoftCircle";
        sharedSoulTexture.wrapMode = TextureWrapMode.Clamp;
        sharedSoulTexture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        float half = (size - 1) * 0.5f;
        float maxDist = half;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / maxDist;
                float dy = (y - half) / maxDist;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = alpha * alpha;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        sharedSoulTexture.SetPixels(pixels);
        sharedSoulTexture.Apply(false, false);
        return sharedSoulTexture;
    }

    private static Texture2D GetSharedHaloRingTexture()
    {
        if (sharedHaloRingTexture != null)
        {
            return sharedHaloRingTexture;
        }

        const int size = 256;
        sharedHaloRingTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        sharedHaloRingTexture.name = "RuntimeSoulHaloRing";
        sharedHaloRingTexture.wrapMode = TextureWrapMode.Clamp;
        sharedHaloRingTexture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        float half = (size - 1) * 0.5f;
        float outerRadius = half * 0.94f;
        float innerRadius = half * 0.64f;
        float glowRadius = half * 0.98f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - half;
                float dy = y - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float ring = Mathf.InverseLerp(innerRadius, outerRadius, dist);
                float innerFade = Mathf.Clamp01(1f - Mathf.InverseLerp(innerRadius * 0.82f, innerRadius, dist));
                float outerFade = Mathf.Clamp01(1f - Mathf.InverseLerp(outerRadius, glowRadius, dist));
                float alpha = Mathf.Clamp01(ring * ring * 1.15f + innerFade * 0.35f + outerFade * 0.65f);
                alpha = Mathf.Clamp01(alpha);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        sharedHaloRingTexture.SetPixels(pixels);
        sharedHaloRingTexture.Apply(false, false);
        return sharedHaloRingTexture;
    }

    private Color ResolveSoulColor(SoulType type)
    {
        return type switch
        {
            SoulType.Life => new Color(3.2f, 0.48f, 0.5f, 1f),
            SoulType.Energy => new Color(0.28f, 1.65f, 3.5f, 1f),
            SoulType.Growth => new Color(0.38f, 3.3f, 0.38f, 1f),
            SoulType.Function => new Color(3.3f, 2.65f, 0.38f, 1f),
            _ => Color.white
        };
    }

    private static GameObject FindSceneObjectByNameIncludingInactive(string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i];
            if (go == null || !go.scene.IsValid())
            {
                continue;
            }

            if (go.name == targetName)
            {
                return go;
            }
        }

        return null;
    }
}
