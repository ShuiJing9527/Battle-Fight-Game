using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SoulPickup : MonoBehaviour
{
    private const bool EnableHaloRingVisuals = true;
    private const float HoverMoveSpeedMultiplier = 0.25f;
    private const float MinHoverSpeed = 0.01f;
    private const float HoverXFrequency = 1.07f;
    private const float HoverYFrequency = 1.33f;
    private const float HoverZFrequency = 0.91f;
    private const float HoverYAmplitudeScale = 0.5f;
    private const float HoverZAmplitudeScale = 0.35f;
    private const float AbsorbHoverBlend = 0.15f;
    private const float AbsorbTargetBlend = 0.75f;
    private const float TrailIdleDistanceRate = 0f;
    private const float TrailAbsorbDistanceRate = 35f;
    private const float TrailLengthScale = 1f;
    private const float TrailVelocityScale = 0.12f;
    private const float TrailCameraVelocityScale = 0f;
    private const float TrailTintAlphaMultiplier = 0.7f;
    private const float HaloTintAlpha = 1f;

    [Header("Soul")]
    public SoulType soulType = SoulType.Life;
    [Min(0f)] public float amount = 1f;
    public bool destroyAfterPickup = true;

    [Header("Auto Absorb")]
    [SerializeField, Min(0f)] private float absorbDelay = 2.0f;
    [SerializeField, Min(0f)] private float absorbSpeed = 8f;
    [SerializeField, Min(0f)] private float pickupDistance = 0.35f;
    [SerializeField, Min(0f)] private float hoverHeight = 0.6f;
    [SerializeField, Min(0f)] private float hoverAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float hoverSpeed = 3f;
    [SerializeField, Min(0f)] private float targetChestHeight = 1f;

    [Header("Visuals")]
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

    private Player2Bootstrap cachedBootstrap;
    private Collider pickupCollider;
    private Renderer[] cachedRenderers;
    private Vector3 spawnPosition;
    private float spawnTime;
    private bool absorbed;
    private bool absorbTargetLogged;
    private bool noTargetLogged;
    private bool isAbsorbing;
    private bool warnedMissingHaloRing;
    private bool warnedMissingCoreParticles;
    private bool warnedMissingTrailParticles;

    private void Awake()
    {
        spawnPosition = transform.position;
        spawnTime = Time.time;

        CacheReferences();
        EnsureTriggerCollider();
        EnsureVisuals();
    }

    private void OnEnable()
    {
        spawnPosition = transform.position;
        spawnTime = Time.time;
        absorbed = false;
        absorbTargetLogged = false;
        noTargetLogged = false;
        isAbsorbing = false;
        warnedMissingHaloRing = false;
        warnedMissingCoreParticles = false;
        warnedMissingTrailParticles = false;

        CacheReferences();
        EnsureTriggerCollider();
        EnsureVisuals();
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

        UpdateHaloRingRotation();

        Transform target = ResolveCurrentPlayerTransform();
        Vector3 hoverOffset = GetHoverOffset();

        if (target == null)
        {
            if (!noTargetLogged)
            {
                noTargetLogged = true;
                Debug.LogWarning("[SoulPickup] no target player found", this);
            }
            SetAbsorbingVisualState(false);
            transform.position = Vector3.Lerp(transform.position, spawnPosition + hoverOffset, Time.deltaTime * Mathf.Max(1f, absorbSpeed * HoverMoveSpeedMultiplier));
            return;
        }

        if (!absorbTargetLogged)
        {
            absorbTargetLogged = true;
        }

        if (Time.time < spawnTime + absorbDelay)
        {
            SetAbsorbingVisualState(false);
            transform.position = Vector3.Lerp(transform.position, spawnPosition + hoverOffset, Time.deltaTime * Mathf.Max(1f, absorbSpeed * HoverMoveSpeedMultiplier));
            return;
        }

        SetAbsorbingVisualState(true);
        Vector3 targetPoint = target.position + Vector3.up * targetChestHeight;
        Vector3 moveTarget = Vector3.Lerp(targetPoint + hoverOffset * AbsorbHoverBlend, targetPoint, AbsorbTargetBlend);
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

        bank.ApplySoul(soulType, amount);
        absorbed = true;

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
        float time = Time.time * Mathf.Max(MinHoverSpeed, hoverSpeed);
        float x = Mathf.Sin(time * HoverXFrequency) * hoverAmplitude;
        float y = hoverHeight + Mathf.Sin(time * HoverYFrequency) * hoverAmplitude * HoverYAmplitudeScale;
        float z = Mathf.Cos(time * HoverZFrequency) * hoverAmplitude * HoverZAmplitudeScale;
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

        if (haloRingTransform == null)
        {
            Transform halo = transform.Find("SoulHaloRing");
            if (halo != null)
            {
                haloRingTransform = halo;
            }
        }

        if (haloRingRenderer == null && haloRingTransform != null)
        {
            haloRingRenderer = haloRingTransform.GetComponent<SpriteRenderer>();
        }
    }

    private void ConfigureVisuals()
    {
        BindExistingParticles();

        if (EnableHaloRingVisuals && haloRingTransform != null && haloRingRenderer != null)
        {
            ApplyHaloRingTransform();
            ConfigureHaloRing();
        }
        else if (EnableHaloRingVisuals && !warnedMissingHaloRing)
        {
            warnedMissingHaloRing = true;
            Debug.LogWarning("[SoulPickup] Missing halo ring reference on prefab", this);
        }

        if (coreParticles == null && !warnedMissingCoreParticles)
        {
            warnedMissingCoreParticles = true;
            Debug.LogWarning("[SoulPickup] Missing coreParticles reference on prefab", this);
        }

        if (trailParticles == null && !warnedMissingTrailParticles)
        {
            warnedMissingTrailParticles = true;
            Debug.LogWarning("[SoulPickup] Missing trailParticles reference on prefab", this);
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

        haloRingTransform.gameObject.SetActive(true);

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
        emission.rateOverDistance = absorbing ? TrailAbsorbDistanceRate : TrailIdleDistanceRate;

        ParticleSystemRenderer renderer = trailParticles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = TrailLengthScale;
            renderer.velocityScale = TrailVelocityScale;
            renderer.cameraVelocityScale = TrailCameraVelocityScale;
        }

    }

    private void RefreshVisualColors()
    {
        Color tint = ResolveSoulColor(soulType);

        if (coreParticles != null)
        {
            ParticleSystem.MainModule main = coreParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(tint.r, tint.g, tint.b, HaloTintAlpha));
        }

        if (haloRingRenderer != null)
        {
            if (EnableHaloRingVisuals)
            {
                if (haloRingTransform != null)
                {
                    haloRingTransform.gameObject.SetActive(true);
                }
                haloRingRenderer.enabled = true;
                Color ringColor = new Color(tint.r, tint.g, tint.b, HaloTintAlpha);
                haloRingRenderer.color = ringColor;
            }
        }

        if (trailParticles != null)
        {
            ParticleSystem.MainModule trail = trailParticles.main;
            Color trailColor = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(tint.a * TrailTintAlphaMultiplier));
            trail.startColor = new ParticleSystem.MinMaxGradient(trailColor);
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
    private Color ResolveSoulColor(SoulType type)
    {
        return type switch
        {
            SoulType.Life => new Color(1f, 0.24f, 0.28f, 1f),
            SoulType.Energy => new Color(0.2f, 0.62f, 1f, 1f),
            SoulType.Growth => new Color(0.28f, 0.92f, 0.38f, 1f),
            SoulType.Function => new Color(1f, 0.76f, 0.22f, 1f),
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
