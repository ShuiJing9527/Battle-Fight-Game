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
    [SerializeField] private BattleDamageType damageType = BattleDamageType.Physical;
    [SerializeField] private LayerMask hitLayerMask = ~0;
    [SerializeField] private bool destroyOnHit = true;

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
    private Material runtimeBodyMaterial;
    private Material runtimeTrailMaterial;
    private Material runtimeSplashMaterial;

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
        hasHit = false;

        EnsureRuntimeVisuals();
        AlignVisualRootToDirection();
        ResetTrailForLaunch();

        if (debugProjectileLog)
        {
            Debug.Log($"[BossAcidProjectile] launch projectile={name} source={(source != null ? source.name : "null")} speed={this.speed:F2} damage={this.damage:F2} hitMask={hitLayerMask.value} position={transform.position} direction={this.direction}", this);
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

        if (debugProjectileLog)
        {
            Debug.Log($"[BossProjectileArc] start={arcStartPoint} target={arcTargetPoint} arcHeight={arcConfiguredHeight:F2} travelTime={arcConfiguredTravelTime:F2} predictionTime={targetPredictionTime:F2} hit=Pending", this);
        }
    }

    private void Update()
    {
        if (useArcMotion)
        {
            float elapsed = Mathf.Max(0f, Time.time - spawnTime);
            float normalizedTime = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, arcConfiguredTravelTime));
            Vector3 nextPosition = Vector3.Lerp(arcStartPoint, arcTargetPoint, normalizedTime)
                + Vector3.up * Mathf.Sin(normalizedTime * Mathf.PI) * arcConfiguredHeight;

            Vector3 movement = nextPosition - transform.position;
            if (movement.sqrMagnitude > DirectionEpsilon)
            {
                direction = movement.normalized;
            }

            transform.position = nextPosition;
            if (normalizedTime >= 1f && !hasHit)
            {
                hasHit = true;
                OnHit(arcTargetPoint, Vector3.up);
                return;
            }
        }
        else
        {
            transform.position += direction * speed * Time.deltaTime;
        }

        UpdateVisualAnimation();

        if (Time.time - spawnTime >= lifeTime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHandleHit(other, default);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
        {
            return;
        }

        TryHandleHit(collision.collider, collision);
    }

    private void TryHandleHit(Collider other, Collision collision)
    {
        if (hasHit || ShouldIgnoreCollision(other))
        {
            return;
        }

        CombatHealth playerHealth = ResolvePlayerCombatHealth(other);
        Vector3 hitPoint = ResolveHitPoint(other, collision);
        Vector3 hitNormal = ResolveHitNormal(other, collision, hitPoint);
        if (playerHealth != null)
        {
            hasHit = true;
            playerHealth.TakeDamage(new BattleDamage(damage, damageType, source));
            LogProjectileHit("player", other, hitPoint);
            OnHit(hitPoint, hitNormal);
            return;
        }

        if (!IsLayerInMask(other.gameObject.layer, hitLayerMask))
        {
            if (debugProjectileLog)
            {
                Debug.Log($"[BossAcidProjectile] ignored non-hit layer collider={other.name} layer={other.gameObject.layer}", this);
            }
            return;
        }

        hasHit = true;
        LogProjectileHit(BattleTargetUtility.IsMonster(other, source != null ? source.transform : null) ? "enemy" : "world", other, hitPoint);
        OnHit(hitPoint, hitNormal);
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
        bodyMeshRenderer.sharedMaterial = ResolveBodyMaterial();

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
        trail.sharedMaterial = ResolveTrailMaterial();
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
        renderer.sharedMaterial = ResolveSplashMaterial();
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
        renderer.sharedMaterial = ResolveSplashMaterial();
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
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
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
        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Standard");
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
}
