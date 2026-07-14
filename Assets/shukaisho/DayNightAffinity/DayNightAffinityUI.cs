using UnityEngine;
using UnityEngine.UI;

public class DayNightAffinityUI : MonoBehaviour
{
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int FlowScaleId = Shader.PropertyToID("_FlowScale");
    private static readonly int DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
    private static readonly int HighlightStrengthId = Shader.PropertyToID("_HighlightStrength");
    private static readonly int HighlightWidthId = Shader.PropertyToID("_HighlightWidth");
    private static readonly int FlowTimeId = Shader.PropertyToID("_FlowTime");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    [Header("Icons")]
    [SerializeField] private Image moonIcon;
    [SerializeField] private Image sunIcon;
    [SerializeField] private Image moonGlow;
    [SerializeField] private Image sunGlow;

    [Header("Gauge")]
    [SerializeField] private RectTransform gaugeRoot;
    [SerializeField] private Image gaugeBackground;
    [SerializeField] private Image twilightBaseAccent;
    [SerializeField] private Image radianceBaseAccent;
    [SerializeField] private RectTransform twilightCoverRoot;
    [SerializeField] private Image twilightCoverSolid;
    [SerializeField] private Image twilightCoverFade;
    [SerializeField] private RectTransform radianceCoverRoot;
    [SerializeField] private Image radianceCoverSolid;
    [SerializeField] private Image radianceCoverFade;
    [SerializeField] private Text twilightText;
    [SerializeField] private Text radianceText;

    [Header("Colors")]
    [SerializeField] private Color gaugeBackgroundColor = new Color(0.05f, 0.08f, 0.12f, 0.42f);
    [SerializeField] private Color textColor = new Color(0.96f, 0.97f, 1f, 1f);
    [SerializeField] private Color moonGlowColor = new Color(0.68f, 0.88f, 1f, 0.4f);
    [SerializeField] private Color sunGlowColor = new Color(1f, 0.88f, 0.5f, 0.42f);
    [SerializeField] private Color twilightDebugColor = new Color(0.32f, 0.76f, 1f, 0.92f);
    [SerializeField] private Color twilightFadeDebugColor = new Color(0.82f, 0.94f, 1f, 0.95f);
    [SerializeField] private Color radianceDebugColor = new Color(1f, 0.82f, 0.32f, 0.92f);
    [SerializeField] private Color radianceFadeDebugColor = new Color(1f, 0.95f, 0.7f, 0.95f);

    [Header("Flow")]
    [SerializeField, Min(0f)] private float flowSpeed = 1.15f;
    [SerializeField, Min(0.1f)] private float flowScale = 2.55f;
    [SerializeField, Range(0f, 0.2f)] private float distortionStrength = 0.016f;
    [SerializeField, Range(0f, 2f)] private float highlightStrength = 0.95f;
    [SerializeField, Range(0.01f, 0.4f)] private float highlightWidth = 0.1f;
    [SerializeField, Range(0f, 1f)] private float activeIconAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float inactiveIconAlpha = 0.45f;
    [SerializeField, Range(0f, 1f)] private float maxCoverRatio = 0.9f;
    [SerializeField, Range(0f, 1f)] private float gaugeFillHeightRatio = 1f;
    [SerializeField, Range(0f, 1f)] private float baseAccentFullAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float baseAccentMinAlpha = 0.45f;
    [SerializeField, Range(0f, 1f)] private float baseAccentFadeStrength = 0.35f;
    [SerializeField, Range(0f, 1f)] private float coverFadeStartDominance = 0.75f;
    [SerializeField, Range(0f, 1f)] private float coverFadeMaxRatio = 0.45f;
    [SerializeField, Range(0f, 1f)] private float coverFadeMinRatio = 0.18f;
    [SerializeField, Min(0f)] private float coverFadeMinPixels = 48f;
    [SerializeField, Min(0f)] private float coverFadeMaxPixels = 180f;
    [SerializeField] private bool debugGaugeVisual;
    [SerializeField] private bool overrideGaugeForPreview;
    [SerializeField, Range(0f, 100f)] private float previewTwilightValue = 50f;
    [SerializeField, Range(0f, 100f)] private float previewRadianceValue = 50f;

    [Header("Icon Glow")]
    [SerializeField, Min(0f)] private float iconGlowPulseSpeed = 2.2f;
    [SerializeField, Range(0f, 1f)] private float iconGlowMinAlpha = 0.18f;
    [SerializeField, Range(0f, 1f)] private float iconGlowMaxAlpha = 0.52f;

    private Material runtimeTwilightSolidMaterial;
    private Material runtimeTwilightFadeMaterial;
    private Material runtimeRadianceSolidMaterial;
    private Material runtimeRadianceFadeMaterial;

    private struct CoverMetrics
    {
        public float TotalWidth;
        public float SolidWidth;
        public float FadeWidth;
    }

    private void Awake()
    {
        AutoBindReferences();
        InitializeVisuals();
    }

    private void OnEnable()
    {
        AutoBindReferences();
        InitializeVisuals();
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterial(ref runtimeTwilightSolidMaterial);
        DestroyRuntimeMaterial(ref runtimeTwilightFadeMaterial);
        DestroyRuntimeMaterial(ref runtimeRadianceSolidMaterial);
        DestroyRuntimeMaterial(ref runtimeRadianceFadeMaterial);
    }

    private void Refresh()
    {
        DayNightGaugeRuntimeState gaugeState = DayNightGaugeRuntimeState.Instance;
        if (gaugeState == null)
        {
            return;
        }

        bool hasDay = TODDayNightAdapter.TryGetIsDay(out bool isDay);
        bool hasNight = TODDayNightAdapter.TryGetIsNight(out bool isNight);

        float twilightValue;
        float radianceValue;
        float emptyValue;
        if (overrideGaugeForPreview)
        {
            twilightValue = Mathf.Clamp(previewTwilightValue, 0f, 100f);
            radianceValue = Mathf.Clamp(previewRadianceValue, 0f, 100f);
            float previewOverflow = Mathf.Max(0f, twilightValue + radianceValue - 100f);
            if (previewOverflow > 0f)
            {
                radianceValue = Mathf.Max(0f, radianceValue - previewOverflow);
            }

            emptyValue = Mathf.Clamp(100f - twilightValue - radianceValue, 0f, 100f);
        }
        else
        {
            twilightValue = Mathf.Clamp(gaugeState.TwilightValue, 0f, 100f);
            radianceValue = Mathf.Clamp(gaugeState.RadianceValue, 0f, 100f);
            emptyValue = Mathf.Clamp(gaugeState.EmptyValue, 0f, 100f);
        }

        float occupiedValue = Mathf.Clamp(100f - emptyValue, 0f, 100f);
        float displayedTotal = twilightValue + radianceValue;
        if (displayedTotal > occupiedValue + 0.001f && displayedTotal > 0.001f)
        {
            float scale = occupiedValue / displayedTotal;
            twilightValue *= scale;
            radianceValue *= scale;
        }

        UpdateBaseAccentState(
            twilightValue / 100f,
            radianceValue / 100f);
        UpdateCover(twilightCoverRoot, twilightCoverSolid, twilightCoverFade, false, twilightValue / 100f);
        UpdateCover(radianceCoverRoot, radianceCoverSolid, radianceCoverFade, true, radianceValue / 100f);
        UpdateFlowMaterials();
        UpdateValueTexts(twilightValue, radianceValue);
        UpdateIconState(hasDay, hasNight, isDay, isNight);
        UpdateGlowState(gaugeState, twilightValue, radianceValue);
    }

    private void InitializeVisuals()
    {
        ConfigureImage(gaugeBackground, gaugeBackgroundColor);
        ConfigureImage(twilightBaseAccent, Color.white);
        ConfigureImage(radianceBaseAccent, Color.white);
        ConfigureImage(twilightCoverSolid, debugGaugeVisual ? twilightDebugColor : Color.white);
        ConfigureImage(twilightCoverFade, debugGaugeVisual ? twilightFadeDebugColor : Color.white);
        ConfigureImage(radianceCoverSolid, debugGaugeVisual ? radianceDebugColor : Color.white);
        ConfigureImage(radianceCoverFade, debugGaugeVisual ? radianceFadeDebugColor : Color.white);
        ConfigureText(twilightText, textColor, TextAnchor.MiddleLeft);
        ConfigureText(radianceText, textColor, TextAnchor.MiddleRight);
        ConfigureImage(moonGlow, ApplyAlpha(moonGlowColor, 0f));
        ConfigureImage(sunGlow, ApplyAlpha(sunGlowColor, 0f));

        SetupRuntimeMaterial(twilightCoverSolid, ref runtimeTwilightSolidMaterial);
        SetupRuntimeMaterial(twilightCoverFade, ref runtimeTwilightFadeMaterial);
        SetupRuntimeMaterial(radianceCoverSolid, ref runtimeRadianceSolidMaterial);
        SetupRuntimeMaterial(radianceCoverFade, ref runtimeRadianceFadeMaterial);

        ConfigureBaseAccentRect(twilightBaseAccent, false);
        ConfigureBaseAccentRect(radianceBaseAccent, true);
        ConfigureCoverRootRect(twilightCoverRoot, false);
        ConfigureCoverRootRect(radianceCoverRoot, true);
        ConfigureCoverSegmentRect(twilightCoverSolid, false);
        ConfigureCoverSegmentRect(twilightCoverFade, false);
        ConfigureCoverSegmentRect(radianceCoverSolid, true);
        ConfigureCoverSegmentRect(radianceCoverFade, true);

        SetCoverVisible(twilightCoverRoot, twilightCoverSolid, twilightCoverFade, false);
        SetCoverVisible(radianceCoverRoot, radianceCoverSolid, radianceCoverFade, false);
    }

    private void UpdateValueTexts(float twilightValue, float radianceValue)
    {
        if (twilightText != null)
        {
            twilightText.text = $"螟懈坩 {Mathf.RoundToInt(twilightValue)}";
        }

        if (radianceText != null)
        {
            radianceText.text = $"霎牙・ {Mathf.RoundToInt(radianceValue)}";
        }
    }

    private void UpdateIconState(bool hasDay, bool hasNight, bool isDay, bool isNight)
    {
        if (moonIcon != null)
        {
            moonIcon.color = ApplyAlpha(moonIcon.color, hasNight && isNight ? activeIconAlpha : inactiveIconAlpha);
        }

        if (sunIcon != null)
        {
            sunIcon.color = ApplyAlpha(sunIcon.color, hasDay && isDay ? activeIconAlpha : inactiveIconAlpha);
        }
    }

    private void UpdateGlowState(DayNightGaugeRuntimeState gaugeState, float twilightValue, float radianceValue)
    {
        bool moonFull = overrideGaugeForPreview ? twilightValue >= 100f : gaugeState != null && gaugeState.HasTwilightState();
        bool sunFull = overrideGaugeForPreview ? radianceValue >= 100f : gaugeState != null && gaugeState.HasRadianceState();
        float pulse = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(Time.unscaledTime * Mathf.Max(0f, iconGlowPulseSpeed)));
        float glowAlpha = Mathf.Lerp(iconGlowMinAlpha, iconGlowMaxAlpha, pulse);

        UpdateGlowImage(moonGlow, moonGlowColor, moonFull ? glowAlpha : 0f);
        UpdateGlowImage(sunGlow, sunGlowColor, sunFull ? glowAlpha : 0f);
    }

    private void UpdateFlowMaterials()
    {
        float flowTime = Time.unscaledTime;
        UpdateFlowMaterial(runtimeTwilightSolidMaterial, flowTime);
        UpdateFlowMaterial(runtimeTwilightFadeMaterial, flowTime);
        UpdateFlowMaterial(runtimeRadianceSolidMaterial, flowTime);
        UpdateFlowMaterial(runtimeRadianceFadeMaterial, flowTime);
    }

    private void UpdateFlowMaterial(Material targetMaterial, float flowTime)
    {
        if (targetMaterial == null)
        {
            return;
        }

        targetMaterial.SetFloat(FlowSpeedId, flowSpeed);
        targetMaterial.SetFloat(FlowScaleId, flowScale);
        targetMaterial.SetFloat(DistortionStrengthId, distortionStrength);
        targetMaterial.SetFloat(HighlightStrengthId, highlightStrength);
        targetMaterial.SetFloat(HighlightWidthId, highlightWidth);
        targetMaterial.SetFloat(FlowTimeId, flowTime);
        targetMaterial.SetFloat(AlphaId, 1f);
    }

    private void UpdateBaseAccentState(float twilightDominance, float radianceDominance)
    {
        float twilightBaseAlpha = Mathf.Lerp(
            baseAccentFullAlpha,
            baseAccentMinAlpha,
            radianceDominance * baseAccentFadeStrength);
        float radianceBaseAlpha = Mathf.Lerp(
            baseAccentFullAlpha,
            baseAccentMinAlpha,
            twilightDominance * baseAccentFadeStrength);

        UpdateAccentAlpha(twilightBaseAccent, Mathf.Max(baseAccentMinAlpha, twilightBaseAlpha));
        UpdateAccentAlpha(radianceBaseAccent, Mathf.Max(baseAccentMinAlpha, radianceBaseAlpha));
    }

    private void UpdateCover(RectTransform coverRoot, Image solid, Image fade, bool anchorRight, float fillRatio)
    {
        if (coverRoot == null || solid == null || fade == null)
        {
            return;
        }

        CoverMetrics metrics = CalculateCoverMetrics(Mathf.Clamp01(fillRatio));
        bool visible = metrics.TotalWidth > 0.001f;
        SetCoverVisible(coverRoot, solid, fade, visible);

        ConfigureCoverRootRect(coverRoot, anchorRight);
        coverRoot.sizeDelta = new Vector2(visible ? metrics.TotalWidth : 0f, coverRoot.sizeDelta.y);

        UpdateCoverSegment(solid.rectTransform, anchorRight, 0f, visible ? metrics.SolidWidth : 0f);
        float fadeOffset = anchorRight ? -metrics.SolidWidth : metrics.SolidWidth;
        UpdateCoverSegment(fade.rectTransform, anchorRight, visible ? fadeOffset : 0f, visible ? metrics.FadeWidth : 0f);

        solid.enabled = visible && metrics.SolidWidth > 0.001f;
        fade.enabled = visible && metrics.FadeWidth > 0.001f;
    }

    private CoverMetrics CalculateCoverMetrics(float fillRatio)
    {
        float coverTotalWidth = ResolveGaugeWidth() * Mathf.Clamp01(fillRatio);
        if (coverTotalWidth <= 0.001f)
        {
            return default;
        }

        float fadeRatio = fillRatio <= coverFadeStartDominance
            ? coverFadeMaxRatio
            : Mathf.Lerp(
                coverFadeMaxRatio,
                coverFadeMinRatio,
                Mathf.InverseLerp(coverFadeStartDominance, 1f, fillRatio));

        fadeRatio = Mathf.Clamp01(fadeRatio);

        float fadeWidth = coverTotalWidth * fadeRatio;
        float minFadePixels = Mathf.Max(0f, coverFadeMinPixels);
        float maxFadePixels = Mathf.Max(minFadePixels, coverFadeMaxPixels);
        fadeWidth = Mathf.Clamp(fadeWidth, minFadePixels, maxFadePixels);
        fadeWidth = Mathf.Min(fadeWidth, coverTotalWidth);

        return new CoverMetrics
        {
            TotalWidth = coverTotalWidth,
            FadeWidth = fadeWidth,
            SolidWidth = Mathf.Max(0f, coverTotalWidth - fadeWidth)
        };
    }

    private float ResolveGaugeWidth()
    {
        return gaugeRoot != null ? Mathf.Max(0f, gaugeRoot.rect.width) : 0f;
    }

    private void ConfigureBaseAccentRect(Image image, bool anchorRight)
    {
        if (image == null)
        {
            return;
        }

        RectTransform rect = image.rectTransform;
        float minX = anchorRight ? 0.76f : 0f;
        float maxX = anchorRight ? 1f : 0.24f;
        float verticalInset = (1f - Mathf.Clamp01(gaugeFillHeightRatio)) * 0.5f;

        rect.anchorMin = new Vector2(minX, verticalInset);
        rect.anchorMax = new Vector2(maxX, 1f - verticalInset);
        rect.pivot = new Vector2(anchorRight ? 1f : 0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private void ConfigureCoverRootRect(RectTransform coverRoot, bool anchorRight)
    {
        if (coverRoot == null)
        {
            return;
        }

        float anchorX = anchorRight ? 1f : 0f;
        float verticalInset = (1f - Mathf.Clamp01(gaugeFillHeightRatio)) * 0.5f;
        coverRoot.anchorMin = new Vector2(anchorX, verticalInset);
        coverRoot.anchorMax = new Vector2(anchorX, 1f - verticalInset);
        coverRoot.pivot = new Vector2(anchorX, 0.5f);
        coverRoot.anchoredPosition = Vector2.zero;
        coverRoot.sizeDelta = new Vector2(coverRoot.sizeDelta.x, 0f);
    }

    private static void ConfigureCoverSegmentRect(Image image, bool anchorRight)
    {
        if (image == null)
        {
            return;
        }

        RectTransform rect = image.rectTransform;
        float anchorX = anchorRight ? 1f : 0f;
        rect.anchorMin = new Vector2(anchorX, 0f);
        rect.anchorMax = new Vector2(anchorX, 1f);
        rect.pivot = new Vector2(anchorX, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, rect.sizeDelta.y);
    }

    private static void UpdateCoverSegment(RectTransform segment, bool anchorRight, float offsetX, float width)
    {
        if (segment == null)
        {
            return;
        }

        float anchorX = anchorRight ? 1f : 0f;
        segment.anchorMin = new Vector2(anchorX, 0f);
        segment.anchorMax = new Vector2(anchorX, 1f);
        segment.pivot = new Vector2(anchorX, 0.5f);
        segment.anchoredPosition = new Vector2(offsetX, 0f);
        segment.sizeDelta = new Vector2(width, segment.sizeDelta.y);
    }

    private static void SetCoverVisible(RectTransform coverRoot, Image solid, Image fade, bool visible)
    {
        if (coverRoot != null && coverRoot.gameObject.activeSelf != visible)
        {
            coverRoot.gameObject.SetActive(visible);
        }

        if (solid != null)
        {
            solid.enabled = visible;
            solid.raycastTarget = false;
        }

        if (fade != null)
        {
            fade.enabled = visible;
            fade.raycastTarget = false;
        }
    }

    private static void SetupRuntimeMaterial(Image image, ref Material runtimeMaterial)
    {
        if (image == null || image.material == null)
        {
            return;
        }

        if (runtimeMaterial == null)
        {
            runtimeMaterial = new Material(image.material);
            runtimeMaterial.name = image.material.name + " (Runtime)";
        }

        image.material = runtimeMaterial;
        image.raycastTarget = false;
    }

    private void AutoBindReferences()
    {
        if (moonIcon == null)
        {
            moonIcon = FindImage("MoonIcon");
        }

        if (moonGlow == null)
        {
            moonGlow = FindImage("MoonIcon/MoonGlow");
        }

        if (sunIcon == null)
        {
            sunIcon = FindImage("SunIcon");
        }

        if (sunGlow == null)
        {
            sunGlow = FindImage("SunIcon/SunGlow");
        }

        if (gaugeRoot == null)
        {
            gaugeRoot = FindRect("GaugeRoot");
        }

        if (gaugeBackground == null)
        {
            gaugeBackground = FindImage("GaugeRoot/GaugeBackground");
        }

        if (twilightBaseAccent == null)
        {
            twilightBaseAccent = FindImage("GaugeRoot/TwilightBaseAccent");
        }

        if (radianceBaseAccent == null)
        {
            radianceBaseAccent = FindImage("GaugeRoot/RadianceBaseAccent");
        }

        if (twilightCoverRoot == null)
        {
            twilightCoverRoot = FindRect("GaugeRoot/TwilightCover");
        }

        if (twilightCoverSolid == null)
        {
            twilightCoverSolid = FindImage("GaugeRoot/TwilightCover/TwilightCoverSolid");
        }

        if (twilightCoverFade == null)
        {
            twilightCoverFade = FindImage("GaugeRoot/TwilightCover/TwilightCoverFade");
        }

        if (radianceCoverRoot == null)
        {
            radianceCoverRoot = FindRect("GaugeRoot/RadianceCover");
        }

        if (radianceCoverSolid == null)
        {
            radianceCoverSolid = FindImage("GaugeRoot/RadianceCover/RadianceCoverSolid");
        }

        if (radianceCoverFade == null)
        {
            radianceCoverFade = FindImage("GaugeRoot/RadianceCover/RadianceCoverFade");
        }

        if (twilightText == null)
        {
            twilightText = FindText("GaugeRoot/TwilightText");
        }

        if (radianceText == null)
        {
            radianceText = FindText("GaugeRoot/RadianceText");
        }
    }

    private Image FindImage(string path)
    {
        Transform target = transform.Find(path);
        return target != null ? target.GetComponent<Image>() : null;
    }

    private RectTransform FindRect(string path)
    {
        Transform target = transform.Find(path);
        return target as RectTransform;
    }

    private Text FindText(string path)
    {
        Transform target = transform.Find(path);
        return target != null ? target.GetComponent<Text>() : null;
    }

    private static void ConfigureImage(Image image, Color color)
    {
        if (image == null)
        {
            return;
        }

        image.color = color;
        image.raycastTarget = false;
    }

    private static void ConfigureText(Text text, Color color, TextAnchor alignment)
    {
        if (text == null)
        {
            return;
        }

        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
    }

    private static void DestroyRuntimeMaterial(ref Material runtimeMaterial)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        Object.Destroy(runtimeMaterial);
        runtimeMaterial = null;
    }

    private static void UpdateAccentAlpha(Image image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        image.color = ApplyAlpha(image.color, alpha);
        image.enabled = alpha > 0.001f;
        image.raycastTarget = false;
    }

    private static Color ApplyAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private static void UpdateGlowImage(Image image, Color color, float alpha)
    {
        if (image == null)
        {
            return;
        }

        image.color = ApplyAlpha(color, alpha);
        image.enabled = alpha > 0.001f;
        image.raycastTarget = false;
    }
}
