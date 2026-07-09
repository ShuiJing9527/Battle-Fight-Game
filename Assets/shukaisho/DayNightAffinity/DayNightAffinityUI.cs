using UnityEngine;
using UnityEngine.UI;

public class DayNightAffinityUI : MonoBehaviour
{
    private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int FlowScaleId = Shader.PropertyToID("_FlowScale");
    private static readonly int DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
    private static readonly int HighlightStrengthId = Shader.PropertyToID("_HighlightStrength");
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
    [SerializeField] private Image twilightFill;
    [SerializeField] private Image radianceFill;
    [SerializeField] private Image flowFill;
    [SerializeField] private Image centerMarker;

    [Header("Flow Materials")]
    [SerializeField] private Material twilightFlowMaterial;
    [SerializeField] private Material radianceFlowMaterial;

    [Header("Texts")]
    [SerializeField] private Text twilightText;
    [SerializeField] private Text radianceText;
    [SerializeField] private Text phaseText;
    [SerializeField] private Text statusText;

    [Header("Colors")]
    [SerializeField] private Color gaugeBackgroundColor = new Color(0.05f, 0.08f, 0.12f, 0.88f);
    [SerializeField] private Color twilightColor = new Color(0.56f, 0.8f, 1f, 0.88f);
    [SerializeField] private Color radianceColor = new Color(1f, 0.83f, 0.35f, 0.88f);
    [SerializeField] private Color centerMarkerColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] private Color textColor = new Color(0.96f, 0.97f, 1f, 1f);
    [SerializeField] private Color boostedStatusColor = new Color(1f, 0.96f, 0.78f, 1f);
    [SerializeField] private Color weakenedStatusColor = new Color(0.76f, 0.88f, 1f, 1f);
    [SerializeField] private Color neutralStatusColor = new Color(0.88f, 0.9f, 0.95f, 0.92f);

    [Header("Flow")]
    [SerializeField, Min(0f)] private float flowSpeed = 1.9f;
    [SerializeField, Min(0.1f)] private float flowScale = 5.8f;
    [SerializeField, Range(0f, 0.2f)] private float distortionStrength = 0.12f;
    [SerializeField, Range(0f, 2f)] private float highlightStrength = 1.05f;
    [SerializeField, Range(0f, 1f)] private float activeIconAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float inactiveIconAlpha = 0.45f;

    [Header("Icon Glow")]
    [SerializeField, Min(0f)] private float iconGlowPulseSpeed = 2.2f;
    [SerializeField, Range(0f, 1f)] private float iconGlowMinAlpha = 0.18f;
    [SerializeField, Range(0f, 1f)] private float iconGlowMaxAlpha = 0.52f;
    [SerializeField] private Color moonGlowColor = new Color(0.68f, 0.88f, 1f, 0.4f);
    [SerializeField] private Color sunGlowColor = new Color(1f, 0.88f, 0.5f, 0.42f);

    private Player2Bootstrap cachedBootstrap;
    private Material runtimeTwilightMaterial;
    private Material runtimeRadianceMaterial;
    private bool resolvedBootstrap;

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
        DestroyRuntimeMaterial(ref runtimeTwilightMaterial);
        DestroyRuntimeMaterial(ref runtimeRadianceMaterial);
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

        float radianceValue = Mathf.Clamp(gaugeState.RadianceValue, 0f, 100f);
        float twilightValue = Mathf.Clamp(gaugeState.TwilightValue, 0f, 100f);

        UpdateFill(twilightFill, 0f, Mathf.Clamp01(twilightValue / 100f), twilightColor);
        UpdateFill(radianceFill, 1f - Mathf.Clamp01(radianceValue / 100f), 1f, radianceColor);
        UpdateFlowMaterials();
        UpdateValueTexts(twilightValue, radianceValue);
        UpdateIconState(hasDay, hasNight, isDay, isNight);
        UpdateGlowState(twilightValue, radianceValue);
    }

    private void InitializeVisuals()
    {
        ConfigureImage(gaugeBackground, gaugeBackgroundColor);
        ConfigureImage(twilightFill, Color.white);
        ConfigureImage(radianceFill, Color.white);
        ConfigureImage(centerMarker, centerMarkerColor);
        ConfigureImage(moonGlow, ApplyAlpha(moonGlowColor, 0f));
        ConfigureImage(sunGlow, ApplyAlpha(sunGlowColor, 0f));
        ConfigureText(twilightText, textColor, TextAnchor.MiddleLeft);
        ConfigureText(radianceText, textColor, TextAnchor.MiddleRight);
        ConfigureText(phaseText, textColor, TextAnchor.MiddleCenter);
        ConfigureText(statusText, neutralStatusColor, TextAnchor.MiddleCenter);

        SetupRuntimeMaterial(twilightFill, twilightFlowMaterial, ref runtimeTwilightMaterial);
        SetupRuntimeMaterial(radianceFill, radianceFlowMaterial, ref runtimeRadianceMaterial);

        if (flowFill != null)
        {
            flowFill.gameObject.SetActive(false);
        }

        Transform energyNoise = transform.Find("GaugeRoot/EnergyNoiseMask");
        if (energyNoise != null)
        {
            energyNoise.gameObject.SetActive(false);
        }

        Transform energyOverlay = transform.Find("GaugeRoot/EnergyFlowOverlay");
        if (energyOverlay != null)
        {
            energyOverlay.gameObject.SetActive(false);
        }
    }

    private void UpdateValueTexts(float twilightValue, float radianceValue)
    {
        if (twilightText != null)
        {
            twilightText.text = $"夜暮 {Mathf.RoundToInt(twilightValue)}";
        }

        if (radianceText != null)
        {
            radianceText.text = $"辉光 {Mathf.RoundToInt(radianceValue)}";
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

    private void UpdateGlowState(float twilightValue, float radianceValue)
    {
        bool moonFull = twilightValue >= 100f;
        bool sunFull = radianceValue >= 100f;
        float pulse = Mathf.InverseLerp(-1f, 1f, Mathf.Sin(Time.unscaledTime * Mathf.Max(0f, iconGlowPulseSpeed)));
        float glowAlpha = Mathf.Lerp(iconGlowMinAlpha, iconGlowMaxAlpha, pulse);

        UpdateGlowImage(moonGlow, moonGlowColor, moonFull ? glowAlpha : 0f);
        UpdateGlowImage(sunGlow, sunGlowColor, sunFull ? glowAlpha : 0f);
    }

    private void UpdateFlowMaterials()
    {
        float flowTime = Time.unscaledTime;
        UpdateFlowMaterial(runtimeTwilightMaterial, flowTime);
        UpdateFlowMaterial(runtimeRadianceMaterial, flowTime);
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
        targetMaterial.SetFloat(FlowTimeId, flowTime);
        targetMaterial.SetFloat(AlphaId, 1f);
    }

    private void SetupRuntimeMaterial(Image image, Material template, ref Material runtimeMaterial)
    {
        if (image == null || template == null)
        {
            return;
        }

        if (runtimeMaterial == null)
        {
            runtimeMaterial = new Material(template);
            runtimeMaterial.name = template.name + " (Runtime)";
        }

        image.material = runtimeMaterial;
        image.raycastTarget = false;
    }

    private static void UpdateFill(Image fill, float minX, float maxX, Color color)
    {
        if (fill == null)
        {
            return;
        }

        RectTransform rect = fill.rectTransform;
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(Mathf.Clamp01(minX), 0f);
        rect.anchorMax = new Vector2(Mathf.Clamp01(Mathf.Max(minX, maxX)), 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        fill.color = Color.white;
        fill.raycastTarget = false;
        fill.enabled = maxX - minX > 0.001f;
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

    private PlayerDayNightAffinity ResolveCurrentAffinity()
    {
        if (!resolvedBootstrap || cachedBootstrap == null)
        {
            cachedBootstrap = FindObjectOfType<Player2Bootstrap>();
            resolvedBootstrap = true;
        }

        if (cachedBootstrap != null && cachedBootstrap.CurrentPlayer != null)
        {
            PlayerDayNightAffinity affinity = ResolveAffinityFromObject(cachedBootstrap.CurrentPlayer);
            if (affinity != null)
            {
                return affinity;
            }
        }

        PlayerDayNightAffinity[] affinities = FindObjectsOfType<PlayerDayNightAffinity>(true);
        for (int i = 0; i < affinities.Length; i++)
        {
            PlayerDayNightAffinity affinity = affinities[i];
            if (affinity != null && affinity.gameObject.activeInHierarchy)
            {
                return affinity;
            }
        }

        return null;
    }

    private static PlayerDayNightAffinity ResolveAffinityFromObject(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        PlayerDayNightAffinity affinity = target.GetComponent<PlayerDayNightAffinity>();
        if (affinity != null)
        {
            return affinity;
        }

        affinity = target.GetComponentInChildren<PlayerDayNightAffinity>(true);
        if (affinity != null)
        {
            return affinity;
        }

        return target.GetComponentInParent<PlayerDayNightAffinity>(true);
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
            Transform root = transform.Find("GaugeRoot");
            gaugeRoot = root as RectTransform;
        }

        if (gaugeBackground == null)
        {
            gaugeBackground = FindImage("GaugeRoot/GaugeBackground");
        }

        if (twilightFill == null)
        {
            twilightFill = FindImage("GaugeRoot/TwilightFill") ?? FindImage("GaugeRoot/EnergyGlowOuter");
        }

        if (radianceFill == null)
        {
            radianceFill = FindImage("GaugeRoot/RadianceFill") ?? FindImage("GaugeRoot/EnergyCore");
        }

        if (flowFill == null)
        {
            flowFill = FindImage("GaugeRoot/FlowFill") ?? FindImage("GaugeRoot/EnergyNoiseMask");
        }

        if (centerMarker == null)
        {
            centerMarker = FindImage("GaugeRoot/CenterMarker");
        }

        if (twilightText == null)
        {
            twilightText = FindText("GaugeRoot/TwilightValueText") ?? FindText("GaugeRoot/TwilightText");
        }

        if (radianceText == null)
        {
            radianceText = FindText("GaugeRoot/RadianceValueText") ?? FindText("GaugeRoot/RadianceText");
        }

        if (phaseText == null)
        {
            phaseText = FindText("PhaseText");
        }

        if (statusText == null)
        {
            statusText = FindText("StatusText");
        }
    }

    private Image FindImage(string path)
    {
        Transform target = transform.Find(path);
        return target != null ? target.GetComponent<Image>() : null;
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
