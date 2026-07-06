using UnityEngine;

public class RuneThornCounterEffect : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private SpriteRenderer ringRenderer;
    [SerializeField] private SpriteRenderer outerGlowRenderer;
    [SerializeField] private SpriteRenderer innerBurstRenderer;

    [Header("Sprites")]
    [SerializeField] private Sprite ringSprite;
    [SerializeField] private Sprite outerGlowSprite;
    [SerializeField] private Sprite innerBurstSprite;

    [Header("Timing")]
    [SerializeField, Min(0.05f)] private float effectDuration = 0.24f;

    [Header("Scale")]
    [SerializeField, Min(0.01f)] private float baseDiameter = 2.8f;
    [SerializeField, Min(0f)] private float ringStartScale = 0.35f;
    [SerializeField, Min(0f)] private float ringEndScale = 1.15f;
    [SerializeField, Min(0f)] private float outerStartScale = 0.22f;
    [SerializeField, Min(0f)] private float outerEndScale = 1.32f;
    [SerializeField, Min(0f)] private float innerStartScale = 0.18f;
    [SerializeField, Min(0f)] private float innerEndScale = 0.72f;

    [Header("Color")]
    [SerializeField] private Color ringColor = new Color(0.73f, 1f, 0.22f, 0.95f);
    [SerializeField] private Color outerGlowColor = new Color(0.28f, 1f, 0.72f, 0.55f);
    [SerializeField] private Color innerBurstColor = new Color(0.96f, 1f, 0.62f, 0.85f);

    private float elapsed;
    private float runtimeDuration;
    private float runtimeDiameter;

    private void Awake()
    {
        ResolveBindings();
        ApplySprites();
    }

    private void OnEnable()
    {
        ResolveBindings();
        ApplySprites();
        ResetPlayback();
    }

    private void OnValidate()
    {
        ResolveBindings();
        ApplySprites();
    }

    public void Configure(float radius, float durationSeconds)
    {
        runtimeDiameter = Mathf.Max(0.1f, radius * 2f);
        runtimeDuration = durationSeconds > 0f ? durationSeconds : Mathf.Max(0.05f, effectDuration);
        ResetPlayback();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float duration = runtimeDuration > 0f ? runtimeDuration : Mathf.Max(0.05f, effectDuration);
        float normalized = Mathf.Clamp01(elapsed / duration);

        UpdateRenderer(ringRenderer, ringColor, ringStartScale, ringEndScale, normalized, 1f);
        UpdateRenderer(outerGlowRenderer, outerGlowColor, outerStartScale, outerEndScale, normalized, 0.75f);
        UpdateRenderer(innerBurstRenderer, innerBurstColor, innerStartScale, innerEndScale, normalized, 1.2f);

        if (normalized >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void ResetPlayback()
    {
        elapsed = 0f;
        if (runtimeDuration <= 0f)
        {
            runtimeDuration = Mathf.Max(0.05f, effectDuration);
        }

        if (runtimeDiameter <= 0f)
        {
            runtimeDiameter = Mathf.Max(0.1f, baseDiameter);
        }
    }

    private void UpdateRenderer(SpriteRenderer rendererTarget, Color baseColor, float startScale, float endScale, float normalized, float alphaBias)
    {
        if (rendererTarget == null)
        {
            return;
        }

        float eased = 1f - Mathf.Pow(1f - normalized, 2f);
        float alpha = (1f - normalized) * (1f - normalized) * alphaBias;
        rendererTarget.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * Mathf.Clamp01(alpha));

        float worldScale = runtimeDiameter * Mathf.Lerp(startScale, endScale, eased);
        rendererTarget.transform.localScale = new Vector3(worldScale, worldScale, worldScale);
    }

    private void ResolveBindings()
    {
        if (ringRenderer == null)
        {
            ringRenderer = transform.Find("Ring")?.GetComponent<SpriteRenderer>();
        }

        if (outerGlowRenderer == null)
        {
            outerGlowRenderer = transform.Find("OuterGlow")?.GetComponent<SpriteRenderer>();
        }

        if (innerBurstRenderer == null)
        {
            innerBurstRenderer = transform.Find("InnerBurst")?.GetComponent<SpriteRenderer>();
        }
    }

    private void ApplySprites()
    {
        if (ringRenderer != null && ringSprite != null)
        {
            ringRenderer.sprite = ringSprite;
        }

        if (outerGlowRenderer != null && outerGlowSprite != null)
        {
            outerGlowRenderer.sprite = outerGlowSprite;
        }

        if (innerBurstRenderer != null && innerBurstSprite != null)
        {
            innerBurstRenderer.sprite = innerBurstSprite;
        }
    }
}
