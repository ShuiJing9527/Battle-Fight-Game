using UnityEngine;
using Spine.Unity;

public class Player2SpineLightTintController : MonoBehaviour
{
    [Header("Spine Light Tint")]
    [SerializeField] private SkeletonAnimation skeletonAnimation;
    [SerializeField] private Light directionalLight;
    [SerializeField] private Color dayTint = Color.white;
    [SerializeField] private Color nightTint = new Color(0.72f, 0.8f, 1f, 1f);
    [SerializeField] private Color sunsetTint = new Color(1f, 0.78f, 0.62f, 1f);
    [SerializeField] private float minBrightness = 0.45f;
    [SerializeField] private float maxBrightness = 1.15f;
    [Range(0f, 1f)]
    [SerializeField] private float tintStrength = 0.6f;
    [SerializeField] private bool useDirectionalLight = true;
    [Range(0f, 1f)]
    [SerializeField] private float manualNightAmount = 0.5f;

    private void Reset()
    {
        skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
    }

    private void Awake()
    {
        if (skeletonAnimation == null)
        {
            skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
        }

        if (directionalLight == null && useDirectionalLight)
        {
            directionalLight = FindDirectionalLightInScene();
        }
    }

    private void LateUpdate()
    {
        ApplyTint();
    }

    public void ApplyTint()
    {
        if (skeletonAnimation == null)
        {
            return;
        }

        if (skeletonAnimation.Skeleton == null)
        {
            return;
        }

        Color targetColor = ComputeTintColor();
        targetColor.a = 1f;
        skeletonAnimation.Skeleton.SetColor(targetColor);
    }

    private static Light FindDirectionalLightInScene()
    {
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            Light candidate = lights[i];
            if (candidate != null && candidate.type == LightType.Directional)
            {
                return candidate;
            }
        }

        return null;
    }

    private Color ComputeTintColor()
    {
        float brightness = useDirectionalLight && directionalLight != null
            ? directionalLight.intensity * GetLightLuminance(directionalLight.color)
            : Mathf.Lerp(maxBrightness, minBrightness, Mathf.Clamp01(manualNightAmount));

        float normalized = Mathf.InverseLerp(minBrightness, maxBrightness, brightness);
        normalized = Mathf.Clamp01(normalized);

        Color baseTint = SelectBaseTint(normalized);
        Color lightTint = useDirectionalLight && directionalLight != null
            ? directionalLight.color
            : Color.white;

        Color finalColor = Color.Lerp(nightTint, baseTint, normalized);
        finalColor *= lightTint;

        Color dayBlend = Color.Lerp(finalColor, dayTint, normalized);
        return Color.Lerp(finalColor, dayBlend, Mathf.Clamp01(tintStrength));
    }

    private Color SelectBaseTint(float normalizedBrightness)
    {
        if (normalizedBrightness >= 0.66f)
        {
            return dayTint;
        }

        if (normalizedBrightness <= 0.33f)
        {
            return nightTint;
        }

        float sunsetT = Mathf.InverseLerp(0.33f, 0.66f, normalizedBrightness);
        return Color.Lerp(nightTint, sunsetTint, sunsetT);
    }

    private static float GetLightLuminance(Color color)
    {
        return Mathf.Clamp01(color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f);
    }
}
