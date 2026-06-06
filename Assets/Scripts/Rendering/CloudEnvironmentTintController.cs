using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Rendering
{
    [DisallowMultipleComponent]
    public class CloudEnvironmentTintController : MonoBehaviour
    {
        [Header("TOD Source")]
        [Tooltip("Assign TODGlobalParameters asset or TODController component.")]
        [SerializeField] private UnityEngine.Object todSource;
        [Tooltip("Direct TODGlobalParameters reference (asset or component). Highest priority.")]
        [SerializeField] private UnityEngine.Object todGlobalParameters;

        [Header("Cloud Renderers")]
        [SerializeField] private List<Renderer> targetRenderers = new List<Renderer>();
        [SerializeField] private bool includeChildrenOnAwake = true;

        [Header("Fallback Colors")]
        [SerializeField] private Color dayTint = Color.white;
        [SerializeField] private Color nightTint = new Color(0.58f, 0.66f, 0.78f, 1f);
        [SerializeField] private bool forceFallbackTint = false;
        [SerializeField] [Range(0f, 1f)] private float debugNightAmount = 0f;

        [Header("Blend")]
        [SerializeField] private bool smoothBlend = true;
        [SerializeField] [Range(0.1f, 20f)] private float blendSpeed = 4f;
        [SerializeField] [Range(0f, 1f)] public float dayNightTintStrength = 0.15f;
        [SerializeField] [Range(0f, 1f)] public float environmentTintStrength = 0.08f;
        [SerializeField] [Range(0f, 2f)] public float cloudBrightnessBoost = 1.25f;
        [SerializeField] [Range(0f, 1f)] public float maxTintSaturation = 0.2f;
        [SerializeField] [Range(0f, 1f)] public float minCloudBrightness = 0.75f;
        [SerializeField] [Range(0f, 1f)] public float minCloudAlpha = 0.9f;

        [Header("Performance")]
        [SerializeField] [Min(0.02f)] private float targetRefreshInterval = 0.1f;
        [SerializeField] [Min(0.02f)] private float applyInterval = 0.05f;
        [SerializeField] [Min(0f)] private float colorChangeThreshold = 0.003f;

        [Header("Debug")]
        [SerializeField] private bool forceVisibleCloudDebug = true;
        [SerializeField] [Min(0.1f)] private float debugLogInterval = 1f;
        [SerializeField] [Range(0.1f, 1f)] private float debugForcedAlpha = 0.85f;
        [SerializeField] [Range(0.1f, 2f)] private float debugForcedOpacity = 0.9f;
        [SerializeField] [Range(0.1f, 2f)] private float debugForcedDensity = 1f;
        [SerializeField] [Range(0.1f, 2f)] private float debugForcedStrength = 1f;
        [SerializeField] private int debugForcedRenderQueue = 3000;
        [SerializeField] private string currentReadMode = "None";
        [SerializeField] private Color currentSourceColor = Color.white;
        [SerializeField] private Color currentAppliedTint = Color.white;

        private Color currentTint = Color.white;
        private Color targetTint = Color.white;
        private Color lastAppliedTint = new Color(-1f, -1f, -1f, -1f);
        private float nextTargetRefreshTime;
        private float nextApplyTime;
        private float nextDebugLogTime;

        private static readonly string[] TintPropertyNames =
        {
            "_EnvironmentTint", "_TintColor", "_Tint", "_BaseColor", "_Color", "_CloudColor"
        };

        private static readonly string[] AlphaPropertyNames =
        {
            "_Alpha", "_Opacity", "_Transparency", "_AlphaStrength", "_CloudOpacity", "_CloudAlpha"
        };

        private static readonly string[] DensityPropertyNames =
        {
            "_Density", "_CloudDensity", "_FogDensity"
        };

        private static readonly string[] StrengthPropertyNames =
        {
            "_Strength", "_CloudStrength", "_LightStrength"
        };

        private const float NormalMinAlphaValue = 0.75f;
        private const float NormalMinOpacityValue = 0.75f;
        private const float NormalMinDensityValue = 0.6f;
        private const float NormalMinStrengthValue = 0.6f;

        private void Awake()
        {
            if (includeChildrenOnAwake)
            {
                CacheChildRenderers();
            }

            if (forceVisibleCloudDebug)
            {
                currentReadMode = "ForceVisibleCloudDebug";
                currentSourceColor = Color.white;
                currentAppliedTint = Color.white;
                ForceVisibleCloudsAndLog("Awake", true);
                return;
            }

            targetTint = ResolveTargetTint();
            currentTint = targetTint;
            currentAppliedTint = BuildSafeCloudTint(currentTint);
            ApplyTint(currentAppliedTint);
            lastAppliedTint = currentAppliedTint;
        }

        private void LateUpdate()
        {
            if (forceVisibleCloudDebug)
            {
                ForceVisibleCloudsAndLog("LateUpdate", false);
                return;
            }

            if (Time.time >= nextTargetRefreshTime)
            {
                targetTint = ResolveTargetTint();
                nextTargetRefreshTime = Time.time + Mathf.Max(0.02f, targetRefreshInterval);
            }

            if (smoothBlend)
            {
                currentTint = Color.Lerp(currentTint, targetTint, 1f - Mathf.Exp(-blendSpeed * Time.deltaTime));
            }
            else
            {
                currentTint = targetTint;
            }

            currentAppliedTint = BuildSafeCloudTint(currentTint);
            if (Time.time >= nextApplyTime && HasMeaningfulColorChange(currentAppliedTint, lastAppliedTint))
            {
                ApplyTint(currentAppliedTint);
                lastAppliedTint = currentAppliedTint;
                nextApplyTime = Time.time + Mathf.Max(0.02f, applyInterval);
            }
        }

        private void CacheChildRenderers()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r != null && !targetRenderers.Contains(r))
                {
                    targetRenderers.Add(r);
                }
            }
        }

        private void ForceVisibleCloudsAndLog(string sourceStage, bool forceLogNow)
        {
            if (targetRenderers.Count == 0 && includeChildrenOnAwake)
            {
                CacheChildRenderers();
            }

            for (int i = 0; i < targetRenderers.Count; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = true;

                Material[] materials = renderer.materials;
                for (int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];
                    if (material == null)
                    {
                        continue;
                    }

                    if (debugForcedRenderQueue >= 0)
                    {
                        material.renderQueue = debugForcedRenderQueue;
                    }

                    SetColorIfPresent(material, Color.white, TintPropertyNames);
                    SetFloatIfPresent(material, debugForcedAlpha, AlphaPropertyNames);
                    SetFloatIfPresent(material, debugForcedOpacity, "_Opacity", "_Transparency");
                    SetFloatIfPresent(material, debugForcedDensity, DensityPropertyNames);
                    SetFloatIfPresent(material, debugForcedStrength, StrengthPropertyNames);
                }
            }

            if (!forceLogNow && Time.unscaledTime < nextDebugLogTime)
            {
                return;
            }

            nextDebugLogTime = Time.unscaledTime + Mathf.Max(0.1f, debugLogInterval);
            LogCloudStatus(sourceStage);
        }

        private void LogCloudStatus(string sourceStage)
        {
            if (targetRenderers.Count == 0)
            {
                Debug.LogWarning($"[CloudEnvironmentTintController] {sourceStage} no cloud renderers bound.", this);
                return;
            }

            for (int i = 0; i < targetRenderers.Count; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer == null)
                {
                    Debug.LogWarning($"[CloudEnvironmentTintController] {sourceStage} targetRenderers[{i}] is null.", this);
                    continue;
                }

                Material[] mats = renderer.materials;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null)
                    {
                        Debug.LogWarning($"[CloudEnvironmentTintController] {sourceStage} renderer={renderer.name} material[{m}] is null.", renderer);
                        continue;
                    }

                    StringBuilder sb = new StringBuilder(512);
                    sb.Append("[CloudEnvironmentTintController] ").Append(sourceStage)
                        .Append(" renderer=").Append(renderer.name)
                        .Append(" renderer.enabled=").Append(renderer.enabled)
                        .Append(" material=").Append(mat.name)
                        .Append(" renderQueue=").Append(mat.renderQueue);

                    AppendColorState(sb, mat, "Tint", "_TintColor");
                    AppendColorState(sb, mat, "BaseColor", "_BaseColor");
                    AppendColorState(sb, mat, "Color", "_Color");
                    AppendFloatState(sb, mat, "Alpha", "_Alpha");
                    AppendFloatState(sb, mat, "Opacity", "_Opacity");
                    AppendFloatState(sb, mat, "Density", "_Density");
                    AppendFloatState(sb, mat, "Strength", "_Strength");
                    AppendFloatState(sb, mat, "Transparency", "_Transparency");
                    AppendFloatState(sb, mat, "AlphaStrength", "_AlphaStrength");
                    AppendColorState(sb, mat, "EnvironmentTint", "_EnvironmentTint");

                    Debug.Log(sb.ToString(), renderer);
                }
            }
        }

        private static void SetColorIfPresent(Material material, Color value, params string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (material.HasProperty(propertyName))
                {
                    Color forced = value;
                    Color current = material.GetColor(propertyName);
                    forced.a = Mathf.Max(current.a, value.a);
                    material.SetColor(propertyName, forced);
                }
            }
        }

        private static void SetFloatIfPresent(Material material, float value, params string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (material.HasProperty(propertyName))
                {
                    material.SetFloat(propertyName, value);
                }
            }
        }

        private static void EnsureMinimumFloat(Material material, float minValue, params string[] propertyNames)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (!material.HasProperty(propertyName))
                {
                    continue;
                }

                float current = material.GetFloat(propertyName);
                if (current < minValue)
                {
                    material.SetFloat(propertyName, minValue);
                }
            }
        }

        private static void AppendColorState(StringBuilder sb, Material mat, string label, string propertyName)
        {
            sb.Append(" | ").Append(label).Append('(').Append(propertyName).Append(")=");
            if (!mat.HasProperty(propertyName))
            {
                sb.Append("N/A");
                return;
            }

            Color color = mat.GetColor(propertyName);
            sb.Append('(')
                .Append(color.r.ToString("0.###")).Append(',')
                .Append(color.g.ToString("0.###")).Append(',')
                .Append(color.b.ToString("0.###")).Append(',')
                .Append(color.a.ToString("0.###")).Append(')');
        }

        private static void AppendFloatState(StringBuilder sb, Material mat, string label, string propertyName)
        {
            sb.Append(" | ").Append(label).Append('(').Append(propertyName).Append(")=");
            if (!mat.HasProperty(propertyName))
            {
                sb.Append("N/A");
                return;
            }

            sb.Append(mat.GetFloat(propertyName).ToString("0.###"));
        }

        private Color ResolveTargetTint()
        {
            float nightAmount = Mathf.Clamp01(debugNightAmount);
            bool hasNightAmountFromTod = false;
            bool hasEnvironmentColor = false;
            Color environmentColor = Color.white;
            object source = null;

            if (forceFallbackTint)
            {
                currentReadMode = "ForceFallback(DayNightOnly)";
            }
            else
            {
                source = ResolveReadSource();
                if (source != null)
                {
                    if (TryReadFloatByNames(source, out float dayNight, "DayOrNight", "dayOrNight", "_dayOrNight"))
                    {
                        nightAmount = Mathf.Clamp01(dayNight);
                        hasNightAmountFromTod = true;
                    }

                    if (TryReadColorByNames(source, out Color fogColor, "FogLightColor", "fogLightColor", "_fogLightColor"))
                    {
                        environmentColor = fogColor;
                        environmentColor.a = 1f;
                        hasEnvironmentColor = true;
                    }
                    else if (TryReadColorByNames(source, out Color mainLightColor, "MainlightColor", "mainlightColor", "_mainlightColor"))
                    {
                        environmentColor = mainLightColor;
                        environmentColor.a = 1f;
                        hasEnvironmentColor = true;
                    }
                }

                if (source == null)
                {
                    currentReadMode = "Fallback(DebugNightOnly)";
                }
            }

            Color cloudWhite = Color.white * Mathf.Max(0f, cloudBrightnessBoost);
            cloudWhite.a = 1f;

            Color baseTint = Color.Lerp(dayTint, nightTint, nightAmount);
            baseTint.a = 1f;
            Color desaturatedDayNight = Color.Lerp(Color.white, baseTint, Mathf.Clamp01(maxTintSaturation));
            desaturatedDayNight.a = 1f;
            Color tintAfterDayNight = Color.Lerp(cloudWhite, desaturatedDayNight, Mathf.Clamp01(dayNightTintStrength));
            tintAfterDayNight.a = 1f;

            if (!forceFallbackTint && hasEnvironmentColor)
            {
                Color desaturatedEnv = Color.Lerp(Color.white, environmentColor, Mathf.Clamp01(maxTintSaturation));
                desaturatedEnv.a = 1f;
                Color finalTint = Color.Lerp(tintAfterDayNight, desaturatedEnv, Mathf.Clamp01(environmentTintStrength));
                finalTint.a = 1f;

                currentReadMode = hasNightAmountFromTod ? "TOD.DayNight+Env(Soft)" : "DebugNight+Env(Soft)";
                currentSourceColor = environmentColor;
                return finalTint;
            }

            if (!forceFallbackTint && hasNightAmountFromTod)
            {
                currentReadMode = "TOD.DayNightOnly(Soft)";
            }

            currentSourceColor = baseTint;
            return tintAfterDayNight;
        }

        private object ResolveReadSource()
        {
            if (todGlobalParameters != null)
            {
                return todGlobalParameters;
            }

            return ResolveParameterSourceFromTodSource();
        }

        private object ResolveParameterSourceFromTodSource()
        {
            if (todSource == null)
            {
                return null;
            }

            if (TryReadMember(todSource, "todGlobalParameters", out object nestedLower) && nestedLower != null)
            {
                return nestedLower;
            }
            if (TryReadMember(todSource, "TodGlobalParameters", out object nestedUpper) && nestedUpper != null)
            {
                return nestedUpper;
            }

            return todSource;
        }

        private Color BuildSafeCloudTint(Color sourceColor)
        {
            Color mixedTint = sourceColor;
            mixedTint.r = Mathf.Max(mixedTint.r, minCloudBrightness);
            mixedTint.g = Mathf.Max(mixedTint.g, minCloudBrightness);
            mixedTint.b = Mathf.Max(mixedTint.b, minCloudBrightness);
            mixedTint.a = Mathf.Max(mixedTint.a, minCloudAlpha);
            return mixedTint;
        }

        private void ApplyTint(Color tint)
        {
            for (int i = 0; i < targetRenderers.Count; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = true;
                Material[] materials = renderer.materials;
                for (int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];
                    if (material == null)
                    {
                        continue;
                    }

                    SetColorIfPresent(material, tint, TintPropertyNames);
                    EnsureMinimumFloat(material, NormalMinAlphaValue, "_Alpha", "_Transparency", "_AlphaStrength", "_CloudAlpha");
                    EnsureMinimumFloat(material, NormalMinOpacityValue, "_Opacity", "_CloudOpacity");
                    EnsureMinimumFloat(material, NormalMinDensityValue, DensityPropertyNames);
                    EnsureMinimumFloat(material, NormalMinStrengthValue, StrengthPropertyNames);
                }
            }
        }

        private bool HasMeaningfulColorChange(Color a, Color b)
        {
            float thresholdSq = colorChangeThreshold * colorChangeThreshold;
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            float da = a.a - b.a;
            return dr * dr + dg * dg + db * db + da * da > thresholdSq;
        }

        private static bool TryReadColorByNames(object source, out Color color, params string[] memberNames)
        {
            for (int i = 0; i < memberNames.Length; i++)
            {
                if (TryReadMember(source, memberNames[i], out object raw) && raw is Color c)
                {
                    color = c;
                    return true;
                }
            }

            color = Color.white;
            return false;
        }

        private static bool TryReadFloatByNames(object source, out float value, params string[] memberNames)
        {
            for (int i = 0; i < memberNames.Length; i++)
            {
                if (!TryReadMember(source, memberNames[i], out object raw))
                {
                    continue;
                }

                if (raw is float f)
                {
                    value = f;
                    return true;
                }
                if (raw is int n)
                {
                    value = n;
                    return true;
                }
            }

            value = 0f;
            return false;
        }

        private static bool TryReadMember(object source, string memberName, out object value)
        {
            if (source == null)
            {
                value = null;
                return false;
            }

            Type type = source.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            FieldInfo field = type.GetField(memberName, flags);
            if (field != null)
            {
                value = field.GetValue(source);
                return true;
            }

            PropertyInfo prop = type.GetProperty(memberName, flags);
            if (prop != null && prop.CanRead)
            {
                value = prop.GetValue(source);
                return true;
            }

            value = null;
            return false;
        }
    }
}
