using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class Player01RNeedleDistortionController : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private Renderer targetRenderer;

    [Header("Fade")]
    [SerializeField, Min(0f)] private float maxOpacity = 0.65f;
    [SerializeField, Min(0.01f)] private float fadeInDuration = 0.06f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.16f;
    [SerializeField] private bool playFadeInOnEnable = false;

    [Header("Shader Properties")]
    [SerializeField] private string opacityPropertyName = "_Opacity";

    private MaterialPropertyBlock propertyBlock;
    private int opacityPropertyId = -1;
    private float baseOpacity = 1f;
    private float currentOpacity;
    private float fadeVelocity;
    private float fadeDuration;
    private float fadeStartOpacity;
    private float fadeTargetOpacity;
    private float fadeElapsed;
    private bool fading;

    private void Awake()
    {
        ResolveRenderer();
        EnsurePropertyBlock();
        CacheShaderPropertyIds();
        CacheBaseOpacity();
        SetNormalizedOpacity(0f, true);
    }

    private void OnEnable()
    {
        ResolveRenderer();
        EnsurePropertyBlock();
        CacheShaderPropertyIds();
        CacheBaseOpacity();
        if (playFadeInOnEnable)
        {
            ResetStateInstant(0f);
            PlayFadeIn();
        }
        else
        {
            SetNormalizedOpacity(0f, true);
        }
    }

    private void Update()
    {
        if (!fading)
        {
            return;
        }

        fadeElapsed += Time.deltaTime;
        float normalized = fadeDuration <= 0.0001f ? 1f : Mathf.Clamp01(fadeElapsed / fadeDuration);
        float opacity = Mathf.Lerp(fadeStartOpacity, fadeTargetOpacity, normalized);
        SetNormalizedOpacity(opacity, false);

        if (normalized >= 1f)
        {
            fading = false;
        }
    }

    public void PlayFadeIn()
    {
        StartFade(currentOpacity, 1f, fadeInDuration);
    }

    public void BeginFadeOut()
    {
        StartFade(currentOpacity, 0f, fadeOutDuration);
    }

    public void ResetStateInstant(float normalizedOpacity = 0f)
    {
        fading = false;
        fadeElapsed = 0f;
        fadeStartOpacity = normalizedOpacity;
        fadeTargetOpacity = normalizedOpacity;
        SetNormalizedOpacity(normalizedOpacity, true);
    }

    private void StartFade(float from, float to, float duration)
    {
        fadeStartOpacity = Mathf.Clamp01(from);
        fadeTargetOpacity = Mathf.Clamp01(to);
        fadeDuration = Mathf.Max(0.0001f, duration);
        fadeElapsed = 0f;
        fading = true;
        SetNormalizedOpacity(fadeStartOpacity, false);
    }

    private void SetNormalizedOpacity(float normalizedOpacity, bool force)
    {
        ResolveRenderer();
        if (targetRenderer == null)
        {
            return;
        }

        EnsurePropertyBlock();
        CacheShaderPropertyIds();
        currentOpacity = Mathf.Clamp01(normalizedOpacity);

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(opacityPropertyId, baseOpacity * maxOpacity * currentOpacity);
        targetRenderer.SetPropertyBlock(propertyBlock);

        if (force)
        {
            targetRenderer.enabled = currentOpacity > 0.001f;
        }
        else if (currentOpacity > 0.001f && !targetRenderer.enabled)
        {
            targetRenderer.enabled = true;
        }
        else if (currentOpacity <= 0.001f && targetRenderer.enabled)
        {
            targetRenderer.enabled = false;
        }
    }

    private void ResolveRenderer()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void CacheShaderPropertyIds()
    {
        if (opacityPropertyId < 0)
        {
            opacityPropertyId = Shader.PropertyToID(opacityPropertyName);
        }
    }

    private void CacheBaseOpacity()
    {
        if (targetRenderer == null || targetRenderer.sharedMaterial == null || !targetRenderer.sharedMaterial.HasProperty(opacityPropertyId))
        {
            baseOpacity = 1f;
            return;
        }

        baseOpacity = targetRenderer.sharedMaterial.GetFloat(opacityPropertyId);
    }
}
