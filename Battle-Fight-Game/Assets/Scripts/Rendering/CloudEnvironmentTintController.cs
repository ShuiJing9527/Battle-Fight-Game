using System;
using System.Collections.Generic;
using System.Reflection;
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
        [Header("Debug")]
        [SerializeField] private string currentReadMode = "None";
        [SerializeField] private Color currentSourceColor = Color.white;
        [SerializeField] private Color currentAppliedTint = Color.white;

        private static readonly int EnvironmentTintId = Shader.PropertyToID("_EnvironmentTint");

        private MaterialPropertyBlock mpb;
        private Color currentTint = Color.white;

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();

            if (includeChildrenOnAwake)
            {
                CacheChildRenderers();
            }

            currentTint = ResolveTargetTint();
            currentAppliedTint = currentTint;
            ApplyTint(currentAppliedTint);
        }

        private void LateUpdate()
        {
            Color targetTint = ResolveTargetTint();
            if (smoothBlend)
            {
                currentTint = Color.Lerp(currentTint, targetTint, 1f - Mathf.Exp(-blendSpeed * Time.deltaTime));
            }
            else
            {
                currentTint = targetTint;
            }

            currentAppliedTint = currentTint;
            ApplyTint(currentAppliedTint);
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

        private Color ResolveTargetTint()
        {
            if (forceFallbackTint)
            {
                Color fallback = Color.Lerp(dayTint, nightTint, Mathf.Clamp01(debugNightAmount));
                fallback.a = 1f;
                currentReadMode = "ForceFallback";
                currentSourceColor = fallback;
                return fallback;
            }

            object source = ResolveReadSource();
            if (source == null)
            {
                currentReadMode = "Fallback(No Source)";
                currentSourceColor = dayTint;
                return dayTint;
            }

            if (TryReadColorByNames(source, out Color fogColor, "FogLightColor", "fogLightColor", "_fogLightColor"))
            {
                fogColor.a = 1f;
                currentReadMode = "TOD.FogLightColor";
                currentSourceColor = fogColor;
                return fogColor;
            }

            if (TryReadColorByNames(source, out Color mainLightColor, "MainlightColor", "mainlightColor", "_mainlightColor"))
            {
                mainLightColor.a = 1f;
                currentReadMode = "TOD.MainlightColor";
                currentSourceColor = mainLightColor;
                return mainLightColor;
            }

            if (TryReadFloatByNames(source, out float dayNight, "DayOrNight", "dayOrNight", "_dayOrNight"))
            {
                float t = Mathf.Clamp01(dayNight);
                Color fallback = Color.Lerp(dayTint, nightTint, t);
                fallback.a = 1f;
                currentReadMode = "TOD._dayOrNight Fallback";
                currentSourceColor = fallback;
                return fallback;
            }

            currentReadMode = "Fallback(Default DayTint)";
            currentSourceColor = dayTint;
            return dayTint;
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

        private void ApplyTint(Color tint)
        {
            for (int i = 0; i < targetRenderers.Count; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Material shared = renderer.sharedMaterial;
                if (shared == null || !shared.HasProperty(EnvironmentTintId))
                {
                    continue;
                }

                renderer.GetPropertyBlock(mpb);
                mpb.SetColor(EnvironmentTintId, tint);
                renderer.SetPropertyBlock(mpb);
            }
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
