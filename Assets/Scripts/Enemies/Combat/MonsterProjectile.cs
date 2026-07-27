using UnityEngine;
using UnityEngine.Rendering;

public class MonsterProjectile : MonoBehaviour
{
    private const float DirectionEpsilon = 0.001f;
    private const float DefaultSplashLifetime = 0.45f;
    private const float DefaultTrailTime = 0.32f;
    private const float DefaultTrailStartWidth = 0.35f;
    private const float DefaultTrailEndWidth = 0f;
    private const float DefaultTrailMinVertexDistance = 0.025f;
    private const float DefaultDropletRate = 10f;
    private const float DefaultDropletLifetime = 0.22f;
    private const float DefaultDropletSpeed = 0.55f;
    private static Mesh cachedSphereMesh;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Min(0f)] private float speed = 8f;
    [SerializeField, Min(0f)] private float lifeTime = 5f;
    [SerializeField, Min(0f)] private float projectileMaximumTravelDistance = 30f;
    [SerializeField] private BattleDamageType damageType = BattleDamageType.Physical;
    [SerializeField] private LayerMask hitLayerMask = ~0;
    [SerializeField] private bool destroyOnHit = true;
    [SerializeField] private bool enableSweepHitDetection = true;
    [SerializeField, Min(0f)] private float minimumSweepRadius = 0.05f;

    [Header("Impact")]
    [SerializeField] private GameObject hitSplashPrefab;

    [Header("Visual")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Material bodyMaterial;
    [SerializeField] private Material trailMaterial;
    [SerializeField] private Material splashMaterial;
    [SerializeField] private Texture2D bodySurfaceTexture;
    [SerializeField] private Texture2D dropletTexture;
    [SerializeField] private Texture2D trailTexture;
    [SerializeField] private Texture2D splashTexture;
    [SerializeField] private float rotateSpeed = 120f;
    [SerializeField] private float squashStretchAmount = 0.08f;
    [SerializeField] private float squashStretchSpeed = 6f;
    [SerializeField] private Vector3 bodyBaseScale = new Vector3(0.75f, 0.6f, 0.92f);
    [SerializeField] private Color bodyColor = new Color(0.45f, 0.85f, 0.18f, 0.86f);
    [SerializeField] private Color bodyEdgeColor = new Color(0.20f, 0.48f, 0.10f, 0.82f);
    [SerializeField, Min(0f)] private float bodySmoothness = 0.3f;
    [SerializeField, Min(0f)] private float bodyMetallic = 0f;
    [Header("Trail")]
    [SerializeField, Min(0.05f)] private float trailTime = 0.5f;
    [SerializeField, Min(0.05f)] private float trailStartWidth = DefaultTrailStartWidth;
    [SerializeField, Min(0f)] private float trailEndWidth = DefaultTrailEndWidth;
    [SerializeField, Range(0, 12)] private int trailCornerVertices = 6;
    [SerializeField, Range(0, 8)] private int trailCapVertices = 3;
    [SerializeField, Min(0.005f)] private float trailMinVertexDistance = DefaultTrailMinVertexDistance;
    [SerializeField] private Color trailStartColor = new Color(0.6588f, 1f, 0.1569f, 0.95f);
    [SerializeField] private Color trailMidColor = new Color(0.4510f, 0.9098f, 0.0902f, 0.80f);
    [SerializeField] private Color trailLateColor = new Color(0.2275f, 0.6588f, 0.0549f, 0.50f);
    [SerializeField] private Color trailEndColor = new Color(0.1451f, 0.4196f, 0.0314f, 0.10f);
    [SerializeField] private bool debugProjectileLog = false;
    [SerializeField] private bool useArcTrajectory = true;
    [SerializeField] private float arcHeight = 2.0f;
    [SerializeField] private float arcTravelTime = 0.9f;
    [SerializeField] private float targetPredictionTime = 0.25f;

    private Vector3 direction = Vector3.forward;
    private bool useArcMotion;
    private Vector3 arcStartPoint;
    private Vector3 arcTargetPoint;
    private float arcConfiguredHeight;
    private float arcConfiguredTravelTime;
    private GameObject source;
    private float spawnTime;
    private bool hasHit;
    private Transform bodyMeshTransform;
    private MeshRenderer bodyMeshRenderer;
    private TrailRenderer cachedTrailRenderer;
    private ParticleSystem dropletParticle;
    private Collider projectileCollider;
    private Material runtimeBodyMaterial;
    private Material runtimeTrailMaterial;
    private Material runtimeSplashMaterial;
    private Vector3 previousPosition;
    private Vector3 spawnPosition;
    private float traveledDistance;
    private bool damageEnabled;

    private void Awake()
    {
        EnsureRuntimeVisuals();
    }

    private void OnEnable()
    {
        EnsureRuntimeVisuals();
        ResetTrailForLaunch();
    }

    private void OnDisable()
    {
        StopTrailEmissionAndClear();
    }

    public void Launch(Vector3 direction, float speed, float damage, BattleDamageType damageType, GameObject source)
    {
        this.direction = direction.sqrMagnitude > DirectionEpsilon ? direction.normalized : Vector3.forward;
        this.speed = Mathf.Max(0f, speed);
        this.damage = Mathf.Max(0f, damage);
        this.damageType = damageType;
        this.source = source;
        useArcMotion = false;
        hitLayerMask = SanitizeHitLayerMask(hitLayerMask);
        spawnTime = Time.time;
        spawnPosition = transform.position;
        previousPosition = transform.position;
        traveledDistance = 0f;
        hasHit = false;
        damageEnabled = true;

        EnsureRuntimeVisuals();
        AlignVisualRootToDirection();
        ResetTrailForLaunch();

        if (debugProjectileLog)
        {
            Debug.Log(
                "[SlimeProjectileTrace] " +
                "event=Initialized" +
                " projectile=" + name +
                " instanceId=" + GetInstanceID() +
                " damage=" + this.damage.ToString("F2") +
                " owner=" + (source != null ? source.name : "null") +
                " target=null" +
                " spawnPosition=" + transform.position +
                " maxDistance=" + projectileMaximumTravelDistance.ToString("F2") +
                " lifetime=" + lifeTime.ToString("F2") +
                " hasHit=" + hasHit +
                " damageEnabled=" + damageEnabled +
                " colliderEnabled=" + (projectileCollider != null && projectileCollider.enabled) +
                " detectCollisions=" + (projectileCollider != null ? projectileCollider.enabled.ToString() : "false"),
                this);
        }
    }

    public void ConfigureArcTrajectory(Vector3 start, Vector3 target, float configuredArcHeight, float configuredTravelTime)
    {
        useArcMotion = useArcTrajectory;
        arcStartPoint = start;
        arcTargetPoint = target;
        arcConfiguredHeight = Mathf.Max(0f, configuredArcHeight > 0f ? configuredArcHeight : arcHeight);
        arcConfiguredTravelTime = Mathf.Max(0.1f, configuredTravelTime > 0f ? configuredTravelTime : arcTravelTime);
        transform.position = start;
        spawnTime = Time.time;
        spawnPosition = start;
        previousPosition = start;
        traveledDistance = 0f;

        if (debugProjectileLog)
        {
            Debug.Log($"[BossProjectileArc] start={arcStartPoint} target={arcTargetPoint} arcHeight={arcConfiguredHeight:F2} travelTime={arcConfiguredTravelTime:F2} predictionTime={targetPredictionTime:F2} hit=Pending", this);
        }
    }

    private void Update()
    {
        if (hasHit)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 nextPosition;

        if (useArcMotion)
        {
            float elapsed = Mathf.Max(0f, Time.time - spawnTime);
            float normalizedTime = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, arcConfiguredTravelTime));
            nextPosition = Vector3.Lerp(arcStartPoint, arcTargetPoint, normalizedTime)
                + Vector3.up * Mathf.Sin(normalizedTime * Mathf.PI) * arcConfiguredHeight;

            Vector3 movement = nextPosition - currentPosition;
            if (movement.sqrMagnitude > DirectionEpsilon)
            {
                direction = movement.normalized;
            }
        }
        else
        {
            nextPosition = currentPosition + direction * speed * Time.deltaTime;
        }

        if (TrySweepHitDetection(currentPosition, nextPosition))
        {
            return;
        }

        transform.position = nextPosition;
        traveledDistance += Vector3.Distance(currentPosition, nextPosition);
        previousPosition = transform.position;

        if (useArcMotion)
        {
            float elapsed = Mathf.Max(0f, Time.time - spawnTime);
            float normalizedTime = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, arcConfiguredTravelTime));
            if (normalizedTime >= 1f)
            {
                if (TryResolveEndpointHit(transform.position))
                {
                    return;
                }

                hasHit = true;
                damageEnabled = false;
                LogProjectileDespawn("ArcEndpointNoTarget");
                OnHit(arcTargetPoint, Vector3.up);
                return;
            }
        }

        UpdateVisualAnimation();

        if (projectileMaximumTravelDistance > 0f && traveledDistance >= projectileMaximumTravelDistance)
        {
            damageEnabled = false;
            LogProjectileDespawn("ExceededMaxDistance");
            Destroy(gameObject);
            return;
        }

        if (Time.time - spawnTime >= lifeTime)
        {
            damageEnabled = false;
            LogProjectileDespawn("Expired");
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHandleHit(other, default, "Trigger");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
        {
            return;
        }

        TryHandleHit(collision.collider, collision, "Collision");
    }

    private void TryHandleHit(Collider other, Collision collision, string sourceReason)
    {
        if (hasHit)
        {
            LogProjectileReject("AlreadyHit", other, sourceReason);
            return;
        }

        if (!damageEnabled)
        {
            LogProjectileReject("DamageDisabled", other, sourceReason);
            return;
        }

        if (ShouldIgnoreCollision(other))
        {
            LogProjectileReject("IgnoredCollision", other, sourceReason);
            return;
        }

        Vector3 hitPoint = ResolveHitPoint(other, collision);
        Vector3 hitNormal = ResolveHitNormal(other, collision, hitPoint);
        TryApplyHit(other, hitPoint, hitNormal, sourceReason);
    }

    private void OnHit(Vector3 hitPoint, Vector3 hitNormal)
    {
        StopTrailEmission();
        SpawnHitSplash(hitPoint, hitNormal);
        if (destroyOnHit)
        {
            Destroy(gameObject);
        }
    }

    private void EnsureRuntimeVisuals()
    {
        DisableLegacyRootRenderer();
        projectileCollider = GetComponent<Collider>();

        if (visualRoot == null)
        {
            GameObject visualRootObject = new GameObject("VisualRoot");
            visualRootObject.transform.SetParent(transform, false);
            visualRootObject.transform.localPosition = Vector3.zero;
            visualRootObject.transform.localRotation = Quaternion.identity;
            visualRootObject.transform.localScale = Vector3.one;
            visualRoot = visualRootObject.transform;
        }

        if (bodyMeshTransform == null)
        {
            Transform existingBody = visualRoot.Find("BodyMesh");
            if (existingBody == null)
            {
                GameObject bodyObject = new GameObject("BodyMesh");
                bodyObject.transform.SetParent(visualRoot, false);
                existingBody = bodyObject.transform;
            }

            bodyMeshTransform = existingBody;
        }

        MeshFilter meshFilter = bodyMeshTransform.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = bodyMeshTransform.gameObject.AddComponent<MeshFilter>();
        }
        meshFilter.sharedMesh = ResolveSphereMesh();

        bodyMeshRenderer = bodyMeshTransform.GetComponent<MeshRenderer>();
        if (bodyMeshRenderer == null)
        {
            bodyMeshRenderer = bodyMeshTransform.gameObject.AddComponent<MeshRenderer>();
        }
        bodyMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        bodyMeshRenderer.receiveShadows = false;
        Material resolvedBodyMaterial = ResolveBodyMaterial();
        bodyMeshRenderer.sharedMaterial = resolvedBodyMaterial;
        bodyMeshRenderer.enabled = resolvedBodyMaterial != null;

        cachedTrailRenderer = GetComponent<TrailRenderer>();
        if (cachedTrailRenderer == null)
        {
            cachedTrailRenderer = gameObject.AddComponent<TrailRenderer>();
        }
        ConfigureTrailRenderer(cachedTrailRenderer);

        if (dropletParticle == null)
        {
            Transform existingDroplet = visualRoot.Find("DropletParticle");
            if (existingDroplet == null)
            {
                GameObject dropletObject = new GameObject("DropletParticle");
                dropletObject.transform.SetParent(visualRoot, false);
                existingDroplet = dropletObject.transform;
            }

            dropletParticle = existingDroplet.GetComponent<ParticleSystem>();
            if (dropletParticle == null)
            {
                dropletParticle = existingDroplet.gameObject.AddComponent<ParticleSystem>();
            }
        }
        ConfigureDropletParticle(dropletParticle);
    }

    private void UpdateVisualAnimation()
    {
        if (visualRoot == null || bodyMeshTransform == null)
        {
            return;
        }

        AlignVisualRootToDirection();

        float pulse = Mathf.Sin((Time.time - spawnTime) * Mathf.Max(0f, squashStretchSpeed));
        float stretch = 1f + squashStretchAmount + pulse * squashStretchAmount * 0.4f;
        float squash = 1f - squashStretchAmount * 0.65f - pulse * squashStretchAmount * 0.2f;
        bodyMeshTransform.localScale = new Vector3(
            bodyBaseScale.x * squash,
            bodyBaseScale.y * squash,
            bodyBaseScale.z * stretch);

        visualRoot.Rotate(Vector3.forward, rotateSpeed * Time.deltaTime, Space.Self);
    }

    private void AlignVisualRootToDirection()
    {
        if (visualRoot == null || direction.sqrMagnitude <= DirectionEpsilon)
        {
            return;
        }

        visualRoot.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void ClearTrail()
    {
        if (cachedTrailRenderer != null)
        {
            cachedTrailRenderer.Clear();
        }
    }

    private void ResetTrailForLaunch()
    {
        if (cachedTrailRenderer == null)
        {
            return;
        }

        cachedTrailRenderer.emitting = false;
        cachedTrailRenderer.Clear();
        cachedTrailRenderer.emitting = true;
    }

    private void StopTrailEmission()
    {
        if (cachedTrailRenderer != null)
        {
            cachedTrailRenderer.emitting = false;
        }
    }

    private void StopTrailEmissionAndClear()
    {
        if (cachedTrailRenderer == null)
        {
            return;
        }

        cachedTrailRenderer.emitting = false;
        cachedTrailRenderer.Clear();
    }

    private void DisableLegacyRootRenderer()
    {
        MeshRenderer rootRenderer = GetComponent<MeshRenderer>();
        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
        }
    }

    private void ConfigureTrailRenderer(TrailRenderer trail)
    {
        if (trail == null)
        {
            return;
        }

        trail.time = DefaultTrailTime;
        trail.time = Mathf.Max(0.05f, trailTime > 0f ? trailTime : DefaultTrailTime);
        trail.startWidth = Mathf.Max(0.05f, trailStartWidth > 0f ? trailStartWidth : DefaultTrailStartWidth);
        trail.endWidth = Mathf.Max(0f, trailEndWidth);
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.alignment = LineAlignment.View;
        trail.minVertexDistance = Mathf.Max(0.005f, trailMinVertexDistance);
        trail.textureMode = LineTextureMode.Stretch;
        trail.numCornerVertices = Mathf.Clamp(trailCornerVertices, 0, 12);
        trail.numCapVertices = Mathf.Clamp(trailCapVertices, 0, 8);
        Material resolvedTrailMaterial = ResolveTrailMaterial();
        trail.sharedMaterial = resolvedTrailMaterial;
        if (resolvedTrailMaterial == null)
        {
            trail.emitting = false;
            return;
        }

        trail.colorGradient = CreateTrailGradient();
        trail.emitting = true;
    }

    private void ConfigureDropletParticle(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
        {
            return;
        }

        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particleSystem.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = DefaultDropletLifetime;
        main.startSpeed = DefaultDropletSpeed;
        main.startSize = 0.08f;
        main.startColor = new Color(0.76f, 0.97f, 0.32f, 0.45f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;

        var emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = DefaultDropletRate;

        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.04f;
        shape.rotation = new Vector3(180f, 0f, 0f);

        var velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.05f, 0.22f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
            CreateColorGradient(
                new Color(0.76f, 0.97f, 0.32f, 0.45f),
                new Color(0.2f, 0.5f, 0.12f, 0f)));

        var sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.25f));

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        Material resolvedSplashMaterial = ResolveSplashMaterial();
        renderer.sharedMaterial = resolvedSplashMaterial;
        renderer.enabled = resolvedSplashMaterial != null;
        if (resolvedSplashMaterial == null)
        {
            return;
        }

        particleSystem.Play(true);
    }

    private void SpawnHitSplash(Vector3 hitPoint, Vector3 hitNormal)
    {
        Quaternion rotation = Quaternion.LookRotation(hitNormal.sqrMagnitude > DirectionEpsilon ? hitNormal : -direction, Vector3.up);
        GameObject splashInstance = hitSplashPrefab != null
            ? Instantiate(hitSplashPrefab, hitPoint, rotation)
            : new GameObject("SlimeHitSplashRuntime");

        splashInstance.transform.SetPositionAndRotation(hitPoint, rotation);
        EnsureSplashVisuals(splashInstance);
    }

    private void EnsureSplashVisuals(GameObject splashInstance)
    {
        if (splashInstance == null)
        {
            return;
        }

        ParticleSystem[] splashParticles = splashInstance.GetComponentsInChildren<ParticleSystem>(true);
        if (splashParticles == null || splashParticles.Length == 0)
        {
            splashParticles = new[] { splashInstance.AddComponent<ParticleSystem>() };
        }

        for (int i = 0; i < splashParticles.Length; i++)
        {
            ConfigureSplashParticle(splashParticles[i]);
        }

        AutoDestroyParticle autoDestroy = splashInstance.GetComponent<AutoDestroyParticle>();
        if (autoDestroy == null)
        {
            autoDestroy = splashInstance.AddComponent<AutoDestroyParticle>();
        }
    }

    private void ConfigureSplashParticle(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
        {
            return;
        }

        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particleSystem.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = DefaultSplashLifetime;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new Color(0.74f, 0.96f, 0.28f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 24;

        var emission = particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 10, 16)
        });

        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.08f;

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
            CreateColorGradient(
                new Color(0.74f, 0.96f, 0.28f, 0.8f),
                new Color(0.16f, 0.45f, 0.1f, 0f)));

        var sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        Material resolvedSplashMaterial = ResolveSplashMaterial();
        renderer.sharedMaterial = resolvedSplashMaterial;
        renderer.enabled = resolvedSplashMaterial != null;
        if (resolvedSplashMaterial == null)
        {
            return;
        }

        particleSystem.Play(true);
    }

    private Material ResolveBodyMaterial()
    {
        if (bodyMaterial != null)
        {
            return bodyMaterial;
        }

        if (runtimeBodyMaterial != null)
        {
            return runtimeBodyMaterial;
        }

        runtimeBodyMaterial = CreateLitTransparentMaterial(bodyColor);
        ApplyMaterialTexture(runtimeBodyMaterial, bodySurfaceTexture);
        ApplyMaterialColor(runtimeBodyMaterial, "_BaseColor", bodyColor);
        ApplyMaterialColor(runtimeBodyMaterial, "_Color", bodyColor);
        ApplyMaterialColor(runtimeBodyMaterial, "_EmissionColor", bodyEdgeColor * 0.05f);
        SetMaterialFloatIfPresent(runtimeBodyMaterial, "_Smoothness", bodySmoothness);
        SetMaterialFloatIfPresent(runtimeBodyMaterial, "_Metallic", bodyMetallic);
        return runtimeBodyMaterial;
    }

    private Material ResolveTrailMaterial()
    {
        if (trailMaterial != null)
        {
            return trailMaterial;
        }

        if (runtimeTrailMaterial != null)
        {
            return runtimeTrailMaterial;
        }

        runtimeTrailMaterial = CreateParticleTransparentMaterial(new Color(0.4510f, 0.9098f, 0.0902f, 0.95f));
        ApplyMaterialTexture(runtimeTrailMaterial, trailTexture);
        return runtimeTrailMaterial;
    }

    private Material ResolveSplashMaterial()
    {
        if (splashMaterial != null)
        {
            return splashMaterial;
        }

        if (runtimeSplashMaterial != null)
        {
            return runtimeSplashMaterial;
        }

        runtimeSplashMaterial = CreateParticleTransparentMaterial(new Color(0.72f, 0.96f, 0.28f, 0.72f));
        ApplyMaterialTexture(runtimeSplashMaterial, splashTexture != null ? splashTexture : dropletTexture);
        return runtimeSplashMaterial;
    }

    private Material CreateLitTransparentMaterial(Color color)
    {
        Shader shader = ResolveRuntimeShader(
            "BattleFight/BossProjectileSlime",
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
            "Standard");
        if (shader == null)
        {
            Debug.LogWarning("[MonsterProjectile] Could not resolve a runtime body shader. Assign bodyMaterial on the projectile prefab for player builds.", this);
            return null;
        }

        Material material = new Material(shader);
        ApplyMaterialColor(material, "_BaseColor", color);
        ApplyMaterialColor(material, "_Color", color);
        SetMaterialFloatIfPresent(material, "_Surface", 1f);
        SetMaterialFloatIfPresent(material, "_Blend", 0f);
        SetMaterialFloatIfPresent(material, "_AlphaClip", 0f);
        SetMaterialFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetMaterialFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetMaterialFloatIfPresent(material, "_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private Material CreateParticleTransparentMaterial(Color color)
    {
        Shader shader = ResolveRuntimeShader(
            "BattleFight/BossProjectileSlime",
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Universal Render Pipeline/Unlit",
            "Sprites/Default",
            "Standard");
        if (shader == null)
        {
            Debug.LogWarning("[MonsterProjectile] Could not resolve a runtime particle shader. Assign trailMaterial/splashMaterial on the projectile prefab for player builds.", this);
            return null;
        }

        Material material = new Material(shader);
        ApplyMaterialColor(material, "_BaseColor", color);
        ApplyMaterialColor(material, "_Color", color);
        SetMaterialFloatIfPresent(material, "_Surface", 1f);
        SetMaterialFloatIfPresent(material, "_Blend", 0f);
        SetMaterialFloatIfPresent(material, "_AlphaClip", 0f);
        SetMaterialFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
        SetMaterialFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        SetMaterialFloatIfPresent(material, "_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private static Shader ResolveRuntimeShader(params string[] shaderNames)
    {
        for (int i = 0; i < shaderNames.Length; i++)
        {
            Shader shader = Shader.Find(shaderNames[i]);
            if (shader != null)
            {
                return shader;
            }
        }

        return null;
    }

    private static void ApplyMaterialTexture(Material material, Texture texture)
    {
        if (material == null || texture == null)
        {
            return;
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }
    }

    private static void ApplyMaterialColor(Material material, string propertyName, Color color)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetColor(propertyName, color);
        }
    }

    private static void SetMaterialFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static Gradient CreateColorGradient(Color startColor, Color endColor)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(endColor.a, 1f)
            });
        return gradient;
    }

    private Gradient CreateTrailGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(trailStartColor, 0f),
                new GradientColorKey(trailMidColor, 0.35f),
                new GradientColorKey(trailLateColor, 0.7f),
                new GradientColorKey(trailEndColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(trailStartColor.a, 0f),
                new GradientAlphaKey(trailMidColor.a, 0.35f),
                new GradientAlphaKey(trailLateColor.a, 0.7f),
                new GradientAlphaKey(trailEndColor.a, 1f)
            });
        return gradient;
    }

    private static Mesh ResolveSphereMesh()
    {
        if (cachedSphereMesh != null)
        {
            return cachedSphereMesh;
        }

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cachedSphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
        if (Application.isPlaying)
        {
            Destroy(temp);
        }
        else
        {
            DestroyImmediate(temp);
        }

        return cachedSphereMesh;
    }

    private bool ShouldIgnoreCollision(Collider other)
    {
        if (other == null)
        {
            return true;
        }

        if (other.transform == transform || other.transform.IsChildOf(transform))
        {
            return true;
        }

        if (source != null && other.transform.IsChildOf(source.transform))
        {
            if (debugProjectileLog)
            {
                Debug.Log($"[BossAcidProjectile] ignored owner collider={other.name}", this);
            }
            return true;
        }

        if (BattleTargetUtility.IsMonster(other, source != null ? source.transform : null))
        {
            if (debugProjectileLog)
            {
                Debug.Log($"[BossAcidProjectile] ignored monster collider={other.name}", this);
            }

            return true;
        }

        return false;
    }

    private Vector3 ResolveHitPoint(Collider other, Collision collision)
    {
        if (collision != null && collision.contactCount > 0)
        {
            return collision.GetContact(0).point;
        }

        if (other == null)
        {
            return transform.position;
        }

        return other.ClosestPoint(transform.position);
    }

    private Vector3 ResolveHitNormal(Collider other, Collision collision, Vector3 hitPoint)
    {
        if (collision != null && collision.contactCount > 0)
        {
            return collision.GetContact(0).normal;
        }

        Vector3 normal = (transform.position - hitPoint).normalized;
        if (normal.sqrMagnitude <= DirectionEpsilon)
        {
            normal = -direction.normalized;
        }

        return normal;
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private static LayerMask SanitizeHitLayerMask(LayerMask mask)
    {
        int bits = mask.value;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            bits &= ~(1 << enemyLayer);
        }

        return bits;
    }

    private void LogProjectileHit(string category, Collider other, Vector3 hitPoint)
    {
        if (!debugProjectileLog)
        {
            return;
        }

        Debug.Log($"[BossProjectileArc] start={arcStartPoint} target={arcTargetPoint} arcHeight={arcConfiguredHeight:F2} travelTime={arcConfiguredTravelTime:F2} hit={category}", this);
        Debug.Log($"[BossAcidProjectile] hit category={category} collider={(other != null ? other.name : "null")} point={hitPoint} source={(source != null ? source.name : "null")}", this);
    }

    private static CombatHealth ResolvePlayerCombatHealth(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        GameObject target = other.gameObject;
        if (!BattleTargetUtility.IsPlayer(target))
        {
            Transform root = other.transform.root;
            if (root == null || !BattleTargetUtility.IsPlayer(root.gameObject))
            {
                return null;
            }
        }

        return other.GetComponentInParent<CombatHealth>();
    }

    private bool TrySweepHitDetection(Vector3 from, Vector3 to)
    {
        if (!enableSweepHitDetection || hasHit)
        {
            return false;
        }

        Vector3 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= DirectionEpsilon)
        {
            return false;
        }

        Vector3 castDirection = delta / distance;
        float castRadius = ResolveSweepRadius();
        if (Physics.SphereCast(
                from,
                castRadius,
                castDirection,
                out RaycastHit hit,
                distance,
                ~0,
                QueryTriggerInteraction.Collide))
        {
            if (debugProjectileLog)
            {
                Debug.Log(
                    "[SlimeProjectileTrace] " +
                    "event=SweepDetected" +
                    " projectile=" + name +
                    " previousPosition=" + from +
                    " currentPosition=" + to +
                    " travelDistanceThisFrame=" + distance.ToString("F3") +
                    " hitCollider=" + (hit.collider != null ? hit.collider.name : "null"),
                    this);
            }

            TryApplyHit(hit.collider, hit.point, hit.normal, "Sweep");
            return hasHit;
        }

        return false;
    }

    private float ResolveSweepRadius()
    {
        if (projectileCollider is SphereCollider sphereCollider)
        {
            float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
            return Mathf.Max(minimumSweepRadius, sphereCollider.radius * scale);
        }

        if (projectileCollider != null)
        {
            Bounds bounds = projectileCollider.bounds;
            return Mathf.Max(minimumSweepRadius, Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) * 0.5f);
        }

        return Mathf.Max(minimumSweepRadius, 0.1f);
    }

    private bool TryResolveEndpointHit(Vector3 position)
    {
        float radius = ResolveSweepRadius();
        Collider[] overlaps = Physics.OverlapSphere(position, radius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider overlap = overlaps[i];
            if (overlap == null)
            {
                continue;
            }

            TryApplyHit(overlap, overlap.ClosestPoint(position), Vector3.up, "ArcEndpointOverlap");
            if (hasHit)
            {
                return true;
            }
        }

        return false;
    }

    private void TryApplyHit(Collider other, Vector3 hitPoint, Vector3 hitNormal, string sourceReason)
    {
        if (other == null)
        {
            LogProjectileReject("NullCollider", other, sourceReason);
            return;
        }

        CombatHealth playerHealth = ResolvePlayerCombatHealth(other);
        if (playerHealth != null)
        {
            float hpBefore = ResolveCombatHealthValue(playerHealth);
            hasHit = true;
            damageEnabled = false;
            playerHealth.TakeDamage(new BattleDamage(damage, damageType, source));
            float hpAfter = ResolveCombatHealthValue(playerHealth);
            LogProjectileHit("player", other, hitPoint, sourceReason, playerHealth, hpBefore, hpAfter);
            OnHit(hitPoint, hitNormal);
            return;
        }

        if (!IsLayerInMask(other.gameObject.layer, hitLayerMask))
        {
            LogProjectileReject("InvalidLayer", other, sourceReason);
            return;
        }

        hasHit = true;
        damageEnabled = false;
        string category = BattleTargetUtility.IsMonster(other, source != null ? source.transform : null) ? "enemy" : "world";
        LogProjectileHit(category, other, hitPoint, sourceReason, null, float.NaN, float.NaN);
        OnHit(hitPoint, hitNormal);
    }

    private void LogProjectileReject(string reason, Collider other, string sourceReason)
    {
        if (!debugProjectileLog)
        {
            return;
        }

        CombatHealth resolvedHealth = other != null ? ResolvePlayerCombatHealth(other) : null;
        Debug.Log(
            "[SlimeProjectileHitTrace] " +
            "event=Rejected" +
            " projectile=" + name +
            " reason=" + reason +
            " source=" + sourceReason +
            " hitObject=" + (other != null ? other.name : "null") +
            " hitHierarchyPath=" + BuildHierarchyPath(other != null ? other.transform : null) +
            " hitLayer=" + (other != null ? LayerMask.LayerToName(other.gameObject.layer) : "None") +
            " hitIsTrigger=" + (other != null && other.isTrigger) +
            " attachedRigidbody=" + (other != null && other.attachedRigidbody != null ? other.attachedRigidbody.name : "null") +
            " resolvedPlayerRoot=" + (resolvedHealth != null ? resolvedHealth.transform.root.name : "null") +
            " resolvedHealthComponent=" + (resolvedHealth != null ? resolvedHealth.name : "null") +
            " distanceFromSpawn=" + Vector3.Distance(spawnPosition, transform.position).ToString("F3") +
            " flightTime=" + (Time.time - spawnTime).ToString("F3") +
            " damageEnabled=" + damageEnabled +
            " hasHitBefore=" + hasHit,
            this);
    }

    private void LogProjectileHit(string category, Collider other, Vector3 hitPoint, string sourceReason, CombatHealth playerHealth, float hpBefore, float hpAfter)
    {
        if (!debugProjectileLog)
        {
            return;
        }

        Debug.Log(
            "[SlimeProjectileHitTrace] " +
            "event=HitDetected" +
            " projectile=" + name +
            " category=" + category +
            " source=" + sourceReason +
            " hitObject=" + (other != null ? other.name : "null") +
            " hitHierarchyPath=" + BuildHierarchyPath(other != null ? other.transform : null) +
            " hitLayer=" + (other != null ? LayerMask.LayerToName(other.gameObject.layer) : "None") +
            " hitIsTrigger=" + (other != null && other.isTrigger) +
            " attachedRigidbody=" + (other != null && other.attachedRigidbody != null ? other.attachedRigidbody.name : "null") +
            " resolvedPlayerRoot=" + (playerHealth != null ? playerHealth.transform.root.name : "null") +
            " resolvedHealthComponent=" + (playerHealth != null ? playerHealth.name : "null") +
            " distanceFromSpawn=" + Vector3.Distance(spawnPosition, hitPoint).ToString("F3") +
            " flightTime=" + (Time.time - spawnTime).ToString("F3") +
            " damageEnabled=" + damageEnabled +
            " hasHitBefore=" + hasHit,
            this);

        if (playerHealth != null)
        {
            Debug.Log(
                "[SlimeProjectileHitTrace] " +
                "event=DamageApplied" +
                " projectile=" + name +
                " damage=" + damage.ToString("F2") +
                " playerHealthBefore=" + hpBefore.ToString("F2") +
                " playerHealthAfter=" + hpAfter.ToString("F2"),
                this);
        }

        Debug.Log($"[BossProjectileArc] start={arcStartPoint} target={arcTargetPoint} arcHeight={arcConfiguredHeight:F2} travelTime={arcConfiguredTravelTime:F2} hit={category}", this);
        Debug.Log($"[BossAcidProjectile] hit category={category} collider={(other != null ? other.name : "null")} point={hitPoint} source={(source != null ? source.name : "null")}", this);
    }

    private void LogProjectileDespawn(string reason)
    {
        if (!debugProjectileLog)
        {
            return;
        }

        Debug.Log(
            "[SlimeProjectileTrace] " +
            "event=Despawned" +
            " projectile=" + name +
            " reason=" + reason +
            " distanceFromSpawn=" + traveledDistance.ToString("F3") +
            " flightTime=" + (Time.time - spawnTime).ToString("F3") +
            " damageEnabled=" + damageEnabled +
            " hasHit=" + hasHit,
            this);
    }

    private static string BuildHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "<null>";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(target.name);
        Transform current = target.parent;
        while (current != null)
        {
            builder.Insert(0, current.name + "/");
            current = current.parent;
        }

        return builder.ToString();
    }

    private static float ResolveCombatHealthValue(CombatHealth combatHealth)
    {
        if (combatHealth == null)
        {
            return 0f;
        }

        BattleResourceBank resourceBank = combatHealth.GetComponent<BattleResourceBank>();
        if (resourceBank != null)
        {
            return Mathf.Max(0f, resourceBank.currentHealth);
        }

        return Mathf.Max(0f, combatHealth.currentHealth);
    }
}
