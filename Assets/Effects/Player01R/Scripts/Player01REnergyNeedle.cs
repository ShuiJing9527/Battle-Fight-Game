using UnityEngine;

public class Player01REnergyNeedle : MonoBehaviour
{
    [Header("Motion")]
    [Tooltip("Forward travel speed in world units per second.")]
    [SerializeField, Min(0.01f)] private float travelSpeed = 42f;
    [Tooltip("How far the needle continues forward after passing through the target point.")]
    [SerializeField, Min(0f)] private float passThroughDistance = 4.5f;
    [Tooltip("How long the needle takes to fully fade once travel completes.")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.28f;

    [Header("Render Bindings")]
    [SerializeField] private Renderer[] fadeRenderers;
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private ParticleSystem[] tailParticles;

    [Header("Shader Fade")]
    [Tooltip("Shader property used to control per-instance opacity during fade.")]
    [SerializeField] private string opacityPropertyName = "_Opacity";
    [Tooltip("Shader property used to control per-instance emission intensity during fade.")]
    [SerializeField] private string emissionPropertyName = "_EmissionIntensity";
    [SerializeField, Min(0f)] private float fadeEmissionMultiplier = 1f;

    private MaterialPropertyBlock propertyBlock;
    private int opacityPropertyId = -1;
    private int emissionPropertyId = -1;
    private float[] baseOpacityValues;
    private float[] baseEmissionValues;
    private Gradient trailBaseGradient;
    private float trailBaseTime;
    private float trailBaseWidthMultiplier = 1f;
    private bool isFlying;
    private bool isFading;
    private float remainingTravelDistance;
    private float fadeTimer;
    private Vector3 travelDirection = Vector3.forward;

    public void Launch(Vector3 startPosition, Vector3 targetPosition, float speed, float extraDistance, float fadeTime)
    {
        transform.position = startPosition;
        travelDirection = (targetPosition - startPosition).sqrMagnitude > 0.0001f
            ? (targetPosition - startPosition).normalized
            : transform.forward;
        transform.rotation = Quaternion.LookRotation(travelDirection, ResolveUpAxis(travelDirection));

        travelSpeed = Mathf.Max(0.01f, speed);
        passThroughDistance = Mathf.Max(0f, extraDistance);
        fadeDuration = Mathf.Max(0.01f, fadeTime);
        remainingTravelDistance = Vector3.Distance(startPosition, targetPosition) + passThroughDistance;

        EnsureBindings();
        ResetVisualState();
        SetParticleEmissionEnabled(true);
        isFading = false;
        isFlying = true;
        fadeTimer = 0f;
    }

    private void Awake()
    {
        EnsurePropertyBlock();
        EnsureBindings();
        ResetVisualState();
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        if (isFlying)
        {
            float step = travelSpeed * deltaTime;
            if (step >= remainingTravelDistance)
            {
                transform.position += travelDirection * remainingTravelDistance;
                remainingTravelDistance = 0f;
                BeginFade();
            }
            else
            {
                transform.position += travelDirection * step;
                remainingTravelDistance -= step;
            }
        }

        if (isFading)
        {
            fadeTimer += deltaTime;
            float normalized = Mathf.Clamp01(fadeTimer / fadeDuration);
            ApplyFade(1f - normalized);
            if (normalized >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }

    private void BeginFade()
    {
        if (isFading)
        {
            return;
        }

        isFlying = false;
        isFading = true;
        fadeTimer = 0f;
        SetParticleEmissionEnabled(false);
    }

    private void EnsureBindings()
    {
        if (fadeRenderers == null || fadeRenderers.Length == 0)
        {
            fadeRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (trailRenderer == null)
        {
            trailRenderer = GetComponentInChildren<TrailRenderer>(true);
        }

        if (tailParticles == null || tailParticles.Length == 0)
        {
            tailParticles = GetComponentsInChildren<ParticleSystem>(true);
        }

        opacityPropertyId = Shader.PropertyToID(opacityPropertyName);
        emissionPropertyId = Shader.PropertyToID(emissionPropertyName);
        CacheBaseMaterialValues();
        CacheTrailState();
    }

    private void CacheBaseMaterialValues()
    {
        if (fadeRenderers == null)
        {
            baseOpacityValues = System.Array.Empty<float>();
            baseEmissionValues = System.Array.Empty<float>();
            return;
        }

        baseOpacityValues = new float[fadeRenderers.Length];
        baseEmissionValues = new float[fadeRenderers.Length];
        for (int i = 0; i < fadeRenderers.Length; i++)
        {
            Renderer rendererTarget = fadeRenderers[i];
            Material sharedMaterial = rendererTarget != null ? rendererTarget.sharedMaterial : null;
            baseOpacityValues[i] = sharedMaterial != null && sharedMaterial.HasProperty(opacityPropertyId)
                ? sharedMaterial.GetFloat(opacityPropertyId)
                : 1f;
            baseEmissionValues[i] = sharedMaterial != null && sharedMaterial.HasProperty(emissionPropertyId)
                ? sharedMaterial.GetFloat(emissionPropertyId)
                : 1f;
        }
    }

    private void CacheTrailState()
    {
        if (trailRenderer == null)
        {
            return;
        }

        trailBaseGradient = trailRenderer.colorGradient;
        trailBaseTime = trailRenderer.time;
        trailBaseWidthMultiplier = trailRenderer.widthMultiplier;
    }

    private void ResetVisualState()
    {
        ApplyFade(1f);

        if (trailRenderer != null)
        {
            trailRenderer.Clear();
            trailRenderer.emitting = true;
            trailRenderer.time = trailBaseTime > 0f ? trailBaseTime : trailRenderer.time;
            trailRenderer.widthMultiplier = trailBaseWidthMultiplier > 0f ? trailBaseWidthMultiplier : trailRenderer.widthMultiplier;
        }
    }

    private void ApplyFade(float factor)
    {
        EnsurePropertyBlock();
        float clampedFactor = Mathf.Clamp01(factor);
        if (fadeRenderers != null)
        {
            for (int i = 0; i < fadeRenderers.Length; i++)
            {
                Renderer rendererTarget = fadeRenderers[i];
                if (rendererTarget == null)
                {
                    continue;
                }

                rendererTarget.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(opacityPropertyId, baseOpacityValues[i] * clampedFactor);
                propertyBlock.SetFloat(emissionPropertyId, baseEmissionValues[i] * Mathf.Lerp(0f, 1f, clampedFactor) * fadeEmissionMultiplier);
                rendererTarget.SetPropertyBlock(propertyBlock);
            }
        }

        if (trailRenderer != null)
        {
            trailRenderer.widthMultiplier = trailBaseWidthMultiplier * Mathf.Lerp(0.35f, 1f, clampedFactor);
            trailRenderer.colorGradient = BuildFadedGradient(trailBaseGradient, clampedFactor);
            if (isFading)
            {
                trailRenderer.time = trailBaseTime * Mathf.Lerp(0.2f, 1f, clampedFactor);
            }
        }
    }

    private void SetParticleEmissionEnabled(bool enabled)
    {
        if (tailParticles == null)
        {
            return;
        }

        for (int i = 0; i < tailParticles.Length; i++)
        {
            ParticleSystem system = tailParticles[i];
            if (system == null)
            {
                continue;
            }

            var emission = system.emission;
            emission.enabled = enabled;
            if (enabled)
            {
                if (!system.isPlaying)
                {
                    system.Play(true);
                }
            }
            else
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        if (trailRenderer != null && !enabled)
        {
            trailRenderer.emitting = false;
        }
    }

    private static Gradient BuildFadedGradient(Gradient source, float factor)
    {
        Gradient result = new Gradient();
        if (source == null)
        {
            result.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(factor, 0f), new GradientAlphaKey(0f, 1f) });
            return result;
        }

        GradientColorKey[] colorKeys = source.colorKeys;
        GradientAlphaKey[] alphaKeys = source.alphaKeys;
        GradientAlphaKey[] fadedAlphaKeys = new GradientAlphaKey[alphaKeys.Length];
        for (int i = 0; i < alphaKeys.Length; i++)
        {
            fadedAlphaKeys[i] = new GradientAlphaKey(alphaKeys[i].alpha * factor, alphaKeys[i].time);
        }

        result.SetKeys(colorKeys, fadedAlphaKeys);
        return result;
    }

    private static Vector3 ResolveUpAxis(Vector3 direction)
    {
        Vector3 fallbackUp = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.98f
            ? Vector3.right
            : Vector3.up;
        return fallbackUp;
    }
}
