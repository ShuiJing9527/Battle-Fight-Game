using Spine.Unity;
using UnityEngine;

public class Player01EGhostParticleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform sourceRoot;
    [SerializeField] private PlayerMovement sourceMovement;
    [SerializeField] private SkeletonAnimation sourceSkeleton;

    [Header("Follow")]
    [SerializeField] private Vector3 baseLocalOffset = Vector3.zero;
    [SerializeField, Min(0f)] private float followSmooth = 8f;
    [SerializeField, Min(0f)] private float dragFollowStrength = 0.03f;
    [SerializeField, Min(0f)] private float maxDragOffset = 0.22f;
    [SerializeField] private bool rotateByHorizontalInput = true;
    [SerializeField] private float moveRightZAngle = 90f;
    [SerializeField] private float moveLeftZAngle = -90f;
    [SerializeField, Min(0f)] private float rotateLerpSpeed = 30f;
    [SerializeField, Range(0f, 1f)] private float horizontalThreshold = 0.1f;
    [SerializeField] private bool returnToIdleWhenNoHorizontalInput = true;

    [Header("Sorting")]
    [SerializeField] private int sortingOrderOffset = 2;

    [Header("Material")]
    [SerializeField] private Material ghostParticleMaterial;

    [Header("Particle Tuning")]
    [SerializeField] private Color sparkleColor = new Color(0.74f, 0.97f, 1f, 1f);
    [SerializeField] private Color mistColor = new Color(0.62f, 0.92f, 1f, 0.35f);
    [SerializeField] private Color trailColor = new Color(0.58f, 0.9f, 1f, 0.45f);
    [SerializeField, Min(0f)] private float sparkleEmissionMultiplier = 5f;
    [SerializeField, Min(0f)] private float mistEmissionMultiplier = 5f;
    [SerializeField, Min(0f)] private float trailEmissionMultiplier = 2f;
    [SerializeField, Min(0)] private int sparkleBurstCount = 500;
    [SerializeField, Min(0)] private int mistBurstCount = 180;
    [SerializeField, Min(0)] private int trailBurstCount = 60;
    [SerializeField] private bool useEmissionMultiplier = false;
    [SerializeField] private bool useBurstOnStart = false;

    private Transform sparkleRoot;
    private Transform mistRoot;
    private Transform trailRoot;
    private ParticleSystem sparkleSystem;
    private ParticleSystem mistSystem;
    private ParticleSystem trailSystem;
    private ParticleSystemRenderer sparkleRenderer;
    private ParticleSystemRenderer mistRenderer;
    private ParticleSystemRenderer trailRenderer;
    [SerializeField] private ParticleSystem[] particleSystems;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalScale;
    private bool hasInitialTransform;
    private bool isBuilt;

    private static Material runtimeParticleMaterial;

    private void Awake()
    {
        CacheReferences();
        CaptureInitialTransform();
        EnsureBuilt();
        RestoreInitialTransform();
        ApplySortingAndLayer();
        LogParticleSettings();
    }

    private void OnEnable()
    {
        CacheReferences();
        CaptureInitialTransform();
        EnsureBuilt();
        RestoreInitialTransform();
        ApplySortingAndLayer();
        LogParticleSettings();
        PlayParticles();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        StopParticles();
    }

    private void LateUpdate()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        CacheReferences();
        UpdateFollowOffset();
        UpdateRotationFollow();
        ApplySortingAndLayer();
    }

    public void SetGhostParticlesActive(bool active)
    {
        CacheReferences();
        CaptureInitialTransform();
        EnsureBuilt();

        if (active)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                return;
            }

            RestoreInitialTransform();
            ApplySortingAndLayer();
            LogParticleSettings();
            PlayParticles();
            return;
        }

        StopParticles();
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    public void Reinitialize()
    {
        CacheReferences();
        CaptureInitialTransform();
        EnsureBuilt();
        RestoreInitialTransform();
        ApplySortingAndLayer();
    }

    private void CacheReferences()
    {
        if (sourceRoot == null)
        {
            sourceRoot = transform.parent != null ? transform.parent : transform.root;
        }

        if (sourceMovement == null)
        {
            if (sourceRoot != null)
            {
                sourceMovement = sourceRoot.GetComponent<PlayerMovement>();
            }

            if (sourceMovement == null)
            {
                sourceMovement = GetComponentInParent<PlayerMovement>(true);
            }
        }

        if (sourceSkeleton == null)
        {
            if (sourceRoot != null)
            {
                sourceSkeleton = sourceRoot.GetComponentInChildren<SkeletonAnimation>(true);
            }
            else
            {
                sourceSkeleton = GetComponentInParent<SkeletonAnimation>(true);
            }
        }
    }

    private void CaptureInitialTransform()
    {
        if (hasInitialTransform)
        {
            return;
        }

        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        initialLocalScale = transform.localScale;
        hasInitialTransform = true;
    }

    private void RestoreInitialTransform()
    {
        if (!hasInitialTransform)
        {
            return;
        }

        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
        transform.localScale = initialLocalScale;
    }

    private void EnsureBuilt()
    {
        CacheParticleReferences();
        isBuilt = sparkleSystem != null && mistSystem != null && trailSystem != null;
    }

    private void CacheParticleReferences()
    {
        if (particleSystems == null || particleSystems.Length == 0)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }

        if (sparkleRoot == null)
        {
            sparkleRoot = transform.Find("E_GhostSparkle");
        }

        if (mistRoot == null)
        {
            mistRoot = transform.Find("E_GhostMist");
        }

        if (trailRoot == null)
        {
            trailRoot = transform.Find("E_GhostTrail");
        }

        if (sparkleRoot != null && sparkleSystem == null)
        {
            sparkleSystem = sparkleRoot.GetComponent<ParticleSystem>();
            sparkleRenderer = sparkleRoot.GetComponent<ParticleSystemRenderer>();
        }

        if (mistRoot != null && mistSystem == null)
        {
            mistSystem = mistRoot.GetComponent<ParticleSystem>();
            mistRenderer = mistRoot.GetComponent<ParticleSystemRenderer>();
        }

        if (trailRoot != null && trailSystem == null)
        {
            trailSystem = trailRoot.GetComponent<ParticleSystem>();
            trailRenderer = trailRoot.GetComponent<ParticleSystemRenderer>();
        }

        if ((sparkleSystem == null || mistSystem == null || trailSystem == null) && particleSystems != null)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem system = particleSystems[i];
                if (system == null)
                {
                    continue;
                }

                string systemName = system.gameObject.name;
                if (sparkleSystem == null && systemName.Contains("Sparkle"))
                {
                    sparkleSystem = system;
                    sparkleRenderer = system.GetComponent<ParticleSystemRenderer>();
                }
                else if (mistSystem == null && systemName.Contains("Mist"))
                {
                    mistSystem = system;
                    mistRenderer = system.GetComponent<ParticleSystemRenderer>();
                }
                else if (trailSystem == null && systemName.Contains("Trail"))
                {
                    trailSystem = system;
                    trailRenderer = system.GetComponent<ParticleSystemRenderer>();
                }
            }
        }
    }

    private Transform EnsureChild(string childName, ref ParticleSystem system, ref ParticleSystemRenderer renderer)
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(transform, false);
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
        }

        system = child.GetComponent<ParticleSystem>();
        if (system == null)
        {
            system = child.gameObject.AddComponent<ParticleSystem>();
        }

        renderer = child.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
        {
            renderer = child.gameObject.AddComponent<ParticleSystemRenderer>();
        }

        return child;
    }

    private void ApplyRendererSetup(ParticleSystemRenderer renderer, int orderOffset)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.enableGPUInstancing = true;
        renderer.sortingLayerName = string.Empty;

        if (sourceSkeleton != null)
        {
            Renderer sourceRenderer = sourceSkeleton.GetComponent<Renderer>();
            if (sourceRenderer != null)
            {
                renderer.sortingLayerName = sourceRenderer.sortingLayerName;
                renderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset + orderOffset;
            }
        }

        Material sharedMaterial = ghostParticleMaterial != null ? ghostParticleMaterial : GetRuntimeParticleMaterial();
        if (sharedMaterial != null)
        {
            renderer.sharedMaterial = sharedMaterial;
        }
    }

    private static Material GetRuntimeParticleMaterial()
    {
        if (runtimeParticleMaterial != null)
        {
            return runtimeParticleMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            return null;
        }

        runtimeParticleMaterial = new Material(shader)
        {
            name = "Player01EGhostParticles_RuntimeMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };

        return runtimeParticleMaterial;
    }

    private void UpdateFollowOffset()
    {
        Vector3 targetLocalPosition = baseLocalOffset;

        if (sourceMovement != null && sourceMovement.rb != null)
        {
            Vector3 worldVelocity = sourceMovement.rb.linearVelocity;
            if (sourceRoot != null)
            {
                worldVelocity = sourceRoot.InverseTransformDirection(worldVelocity);
            }

            Vector3 dragOffset = new Vector3(-worldVelocity.x, 0f, -worldVelocity.z) * dragFollowStrength;
            dragOffset = Vector3.ClampMagnitude(dragOffset, maxDragOffset);
            targetLocalPosition += dragOffset;
        }

        float lerpT = Mathf.Clamp01(Time.deltaTime * followSmooth);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, lerpT);
    }

    private void UpdateRotationFollow()
    {
        if (!rotateByHorizontalInput)
        {
            return;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(horizontal) <= horizontalThreshold && sourceMovement != null && sourceMovement.rb != null)
        {
            Vector3 localVelocity = sourceRoot != null
                ? sourceRoot.InverseTransformDirection(sourceMovement.rb.linearVelocity)
                : sourceMovement.rb.linearVelocity;
            horizontal = localVelocity.x;
        }

        Quaternion targetRotation = initialLocalRotation;
        if (horizontal > horizontalThreshold)
        {
            targetRotation = initialLocalRotation * Quaternion.Euler(0f, 0f, moveRightZAngle);
        }
        else if (horizontal < -horizontalThreshold)
        {
            targetRotation = initialLocalRotation * Quaternion.Euler(0f, 0f, moveLeftZAngle);
        }
        else if (!returnToIdleWhenNoHorizontalInput)
        {
            return;
        }

        float lerpT = Mathf.Clamp01(Time.deltaTime * rotateLerpSpeed);
        transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, lerpT);
    }

    private void ApplySortingAndLayer()
    {
        int layer = gameObject.layer;
        if (sparkleRoot != null)
        {
            sparkleRoot.gameObject.layer = layer;
        }

        if (mistRoot != null)
        {
            mistRoot.gameObject.layer = layer;
        }

        if (trailRoot != null)
        {
            trailRoot.gameObject.layer = layer;
        }

        ApplyRendererSetup(sparkleRenderer, 2);
        ApplyRendererSetup(mistRenderer, 0);
        ApplyRendererSetup(trailRenderer, -1);
    }

    private void PlayParticles()
    {
        if (sparkleSystem != null && !sparkleSystem.isPlaying)
        {
            sparkleSystem.Clear(true);
            sparkleSystem.Play(true);
            if (useBurstOnStart)
            {
                sparkleSystem.Emit(sparkleBurstCount);
            }
        }

        if (mistSystem != null && !mistSystem.isPlaying)
        {
            mistSystem.Clear(true);
            mistSystem.Play(true);
            if (useBurstOnStart)
            {
                mistSystem.Emit(mistBurstCount);
            }
        }

        if (trailSystem != null && !trailSystem.isPlaying)
        {
            trailSystem.Clear(true);
            trailSystem.Play(true);
            if (useBurstOnStart)
            {
                trailSystem.Emit(trailBurstCount);
            }
        }
    }

    private void LogParticleSettings()
    {
        if (sparkleSystem != null)
        {
            Debug.Log($"[E Particle] useEmissionMultiplier = {useEmissionMultiplier}, useBurstOnStart = {useBurstOnStart}, Sparkle final rate = {sparkleSystem.emission.rateOverTime.constant}", this);
        }

        if (mistSystem != null)
        {
            Debug.Log($"[E Particle] useEmissionMultiplier = {useEmissionMultiplier}, useBurstOnStart = {useBurstOnStart}, Mist final rate = {mistSystem.emission.rateOverTime.constant}", this);
        }

        if (trailSystem != null)
        {
            Debug.Log($"[E Particle] useEmissionMultiplier = {useEmissionMultiplier}, useBurstOnStart = {useBurstOnStart}, Trail final rate = {trailSystem.emission.rateOverTime.constant}", this);
        }
    }

    private void SetBurst(ParticleSystem.EmissionModule emission, int count)
    {
        if (count <= 0)
        {
            emission.SetBursts(new ParticleSystem.Burst[0]);
            return;
        }

        short clampedCount = (short)Mathf.Clamp(count, 1, short.MaxValue);
        ParticleSystem.Burst burst = new ParticleSystem.Burst(0f, clampedCount, clampedCount);
        emission.SetBursts(new[] { burst });
    }

    private void StopParticles()
    {
        if (sparkleSystem != null)
        {
            sparkleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (mistSystem != null)
        {
            mistSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (trailSystem != null)
        {
            trailSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
