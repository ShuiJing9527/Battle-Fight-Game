using UnityEngine;

public static class AirWallScreenDistortion
{
    private static readonly int ScreenDataId = Shader.PropertyToID("_AirWallScreenData");
    private static readonly int TimeDataId = Shader.PropertyToID("_AirWallTimeData");

    private static Vector3 worldPosition;
    private static float startTime;
    private static float duration;
    private static float hueOffset;
    private static bool isActive;

    public static void Trigger(Vector3 hitWorldPosition, float effectDuration, float randomHueOffset)
    {
        worldPosition = hitWorldPosition;
        startTime = Time.time;
        duration = Mathf.Max(0.01f, effectDuration);
        hueOffset = randomHueOffset;
        isActive = true;
    }

    public static void UpdateGlobals(Camera targetCamera)
    {
        if (!isActive || targetCamera == null)
        {
            DisableGlobals();
            return;
        }

        float elapsed = Time.time - startTime;
        float normalizedTime = Mathf.Clamp01(elapsed / duration);

        if (elapsed >= duration)
        {
            isActive = false;
            DisableGlobals();
            return;
        }

        Vector3 viewportPosition = targetCamera.WorldToViewportPoint(worldPosition);
        if (viewportPosition.z <= 0f)
        {
            DisableGlobals();
            return;
        }

        float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedTime / 0.08f));
        float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((normalizedTime - 0.78f) / 0.22f));
        float visibility = fadeIn * fadeOut;

        Shader.SetGlobalVector(ScreenDataId, new Vector4(viewportPosition.x, viewportPosition.y, visibility, hueOffset));
        Shader.SetGlobalVector(TimeDataId, new Vector4(elapsed, duration, normalizedTime, visibility));
    }

    private static void DisableGlobals()
    {
        Shader.SetGlobalVector(ScreenDataId, Vector4.zero);
        Shader.SetGlobalVector(TimeDataId, Vector4.zero);
    }
}
