using UnityEngine;

[DisallowMultipleComponent]
public sealed class AmbientDayNightAffinityBonus : MonoBehaviour
{
    private const string RuntimeObjectName = "AmbientDayNightAffinityBonus";

    [System.Serializable]
    public struct AmbientAffinityContext
    {
        public float currentTimeHours;
        public float sunriseHour;
        public float sunsetHour;
        public bool usedFallbackHours;
        public bool isDay;
        public bool isNight;
        public float progress;
        public float affinityStrength;
        public float outgoingMultiplier;
        public float incomingMultiplier;
        public float incomingReduction;
    }

    private static AmbientDayNightAffinityBonus instance;

    [SerializeField, Range(0f, 1f)] private float ambientAffinityMinimum = 0.10f;
    [SerializeField, Range(0f, 1f)] private float ambientAffinityMaximum = 0.50f;
    [SerializeField, Range(0f, 24f)] private float fallbackSunriseHour = 6f;
    [SerializeField, Range(0f, 24f)] private float fallbackSunsetHour = 18f;
    [SerializeField] private bool debugAmbientAffinity;

    public static bool TryGetContext(out AmbientAffinityContext context)
    {
        return ResolveInstance().TryBuildContext(out context);
    }

    public static bool IsDaytime()
    {
        return TryGetContext(out AmbientAffinityContext context) && context.isDay;
    }

    public static bool IsNighttime()
    {
        return TryGetContext(out AmbientAffinityContext context) && context.isNight;
    }

    public static float GetNormalizedDayProgress()
    {
        return TryGetContext(out AmbientAffinityContext context) && context.isDay ? context.progress : 0f;
    }

    public static float GetNormalizedNightProgress()
    {
        return TryGetContext(out AmbientAffinityContext context) && context.isNight ? context.progress : 0f;
    }

    public static float GetDayAffinityStrength()
    {
        return TryGetContext(out AmbientAffinityContext context) && context.isDay ? context.affinityStrength : 0f;
    }

    public static float GetNightAffinityStrength()
    {
        return TryGetContext(out AmbientAffinityContext context) && context.isNight ? context.affinityStrength : 0f;
    }

    public static float GetDayChildAmbientOutgoingMultiplier(GameObject attacker, Object debugContext = null, string debugTag = null)
    {
        if (!IsDayChildAffinityActor(attacker) || !TryGetContext(out AmbientAffinityContext context) || !context.isDay)
        {
            return 1f;
        }

        ResolveInstance().DebugLog(
            $"DayChildAmbientOutgoing source={GetObjectName(attacker)} debugTag={debugTag ?? "<none>"} currentTime={context.currentTimeHours:F2} sunrise={context.sunriseHour:F2} sunset={context.sunsetHour:F2} progress={context.progress:F4} strength={context.affinityStrength:F4} outgoingMultiplier={context.outgoingMultiplier:F4} usedFallback={context.usedFallbackHours}",
            debugContext != null ? debugContext : attacker);
        return context.outgoingMultiplier;
    }

    public static float GetNightChildAmbientIncomingMultiplier(GameObject target, Object debugContext = null, string debugTag = null)
    {
        if (!IsNightChildAffinityActor(target) || !TryGetContext(out AmbientAffinityContext context) || !context.isNight)
        {
            return 1f;
        }

        ResolveInstance().DebugLog(
            $"NightChildAmbientIncoming target={GetObjectName(target)} debugTag={debugTag ?? "<none>"} currentTime={context.currentTimeHours:F2} sunrise={context.sunriseHour:F2} sunset={context.sunsetHour:F2} progress={context.progress:F4} strength={context.affinityStrength:F4} incomingMultiplier={context.incomingMultiplier:F4} incomingReduction={context.incomingReduction:F4} usedFallback={context.usedFallbackHours}",
            debugContext != null ? debugContext : target);
        return context.incomingMultiplier;
    }

    private static AmbientDayNightAffinityBonus ResolveInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<AmbientDayNightAffinityBonus>();
        if (instance != null)
        {
            return instance;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        instance = runtimeObject.AddComponent<AmbientDayNightAffinityBonus>();
        DontDestroyOnLoad(runtimeObject);
        return instance;
    }

    private bool TryBuildContext(out AmbientAffinityContext context)
    {
        context = default(AmbientAffinityContext);
        if (!TODDayNightAdapter.TryGetCurrentTimeHours(out float currentTimeHours))
        {
            return false;
        }

        bool usedFallbackHours = !TODDayNightAdapter.TryGetSunriseSunsetHours(out float sunriseHour, out float sunsetHour);
        if (usedFallbackHours)
        {
            sunriseHour = fallbackSunriseHour;
            sunsetHour = fallbackSunsetHour;
        }

        sunriseHour = NormalizeHour(sunriseHour);
        sunsetHour = NormalizeHour(sunsetHour);
        if (!TryResolveSafeBoundaries(sunriseHour, sunsetHour, out sunriseHour, out sunsetHour))
        {
            sunriseHour = NormalizeHour(fallbackSunriseHour);
            sunsetHour = NormalizeHour(fallbackSunsetHour);
            usedFallbackHours = true;
        }

        bool isDay = IsTimeWithinRange(currentTimeHours, sunriseHour, sunsetHour);
        bool isNight = !isDay;
        float progress = isDay
            ? ResolveRangeProgress(currentTimeHours, sunriseHour, sunsetHour)
            : ResolveNightProgress(currentTimeHours, sunsetHour, sunriseHour);
        float peakFactor = Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress));
        float affinityStrength = Mathf.Lerp(
            Mathf.Clamp01(ambientAffinityMinimum),
            Mathf.Clamp01(ambientAffinityMaximum),
            peakFactor);
        float outgoingMultiplier = 1f + affinityStrength;
        float incomingMultiplier = 1f / Mathf.Max(1f, outgoingMultiplier);

        context = new AmbientAffinityContext
        {
            currentTimeHours = currentTimeHours,
            sunriseHour = sunriseHour,
            sunsetHour = sunsetHour,
            usedFallbackHours = usedFallbackHours,
            isDay = isDay,
            isNight = isNight,
            progress = progress,
            affinityStrength = affinityStrength,
            outgoingMultiplier = outgoingMultiplier,
            incomingMultiplier = incomingMultiplier,
            incomingReduction = 1f - incomingMultiplier
        };
        return true;
    }

    private void DebugLog(string message, Object context = null)
    {
        if (!debugAmbientAffinity)
        {
            return;
        }

        Debug.Log($"[AmbientAffinity] {message}", context != null ? context : this);
    }

    private static bool TryResolveSafeBoundaries(float sunriseHour, float sunsetHour, out float safeSunriseHour, out float safeSunsetHour)
    {
        safeSunriseHour = sunriseHour;
        safeSunsetHour = sunsetHour;
        float dayDuration = NormalizeDuration(sunsetHour - sunriseHour);
        if (dayDuration <= 0.01f || dayDuration >= 23.99f)
        {
            return false;
        }

        return true;
    }

    private static bool IsTimeWithinRange(float currentTimeHours, float startHour, float endHour)
    {
        if (Mathf.Approximately(startHour, endHour))
        {
            return false;
        }

        if (startHour < endHour)
        {
            return currentTimeHours >= startHour && currentTimeHours < endHour;
        }

        return currentTimeHours >= startHour || currentTimeHours < endHour;
    }

    private static float ResolveRangeProgress(float currentTimeHours, float startHour, float endHour)
    {
        float duration = NormalizeDuration(endHour - startHour);
        if (duration <= 0.01f)
        {
            return 0f;
        }

        float elapsed = NormalizeDuration(currentTimeHours - startHour);
        return Mathf.Clamp01(elapsed / duration);
    }

    private static float ResolveNightProgress(float currentTimeHours, float sunsetHour, float sunriseHour)
    {
        float nightDuration = NormalizeDuration((sunriseHour + 24f) - sunsetHour);
        if (nightDuration <= 0.01f)
        {
            return 0f;
        }

        float nightElapsed = currentTimeHours < sunriseHour
            ? currentTimeHours + 24f - sunsetHour
            : currentTimeHours - sunsetHour;
        return Mathf.Clamp01(nightElapsed / nightDuration);
    }

    private static float NormalizeDuration(float value)
    {
        value %= 24f;
        if (value < 0f)
        {
            value += 24f;
        }

        return value;
    }

    private static float NormalizeHour(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return 0f;
        }

        value %= 24f;
        if (value < 0f)
        {
            value += 24f;
        }

        return value;
    }

    private static bool IsDayChildAffinityActor(GameObject target)
    {
        PlayerDayNightAffinity affinity = ResolveAffinity(target);
        return affinity != null && affinity.IsDayChild;
    }

    private static bool IsNightChildAffinityActor(GameObject target)
    {
        PlayerDayNightAffinity affinity = ResolveAffinity(target);
        return affinity != null && affinity.IsNightChild;
    }

    private static PlayerDayNightAffinity ResolveAffinity(GameObject target)
    {
        GameObject resolvedTarget = ResolvePlayerSource(target) ?? target;
        if (resolvedTarget == null)
        {
            return null;
        }

        PlayerDayNightAffinity affinity = resolvedTarget.GetComponent<PlayerDayNightAffinity>();
        if (affinity != null)
        {
            return affinity;
        }

        affinity = resolvedTarget.GetComponentInParent<PlayerDayNightAffinity>(true);
        if (affinity != null)
        {
            return affinity;
        }

        return resolvedTarget.GetComponentInChildren<PlayerDayNightAffinity>(true);
    }

    private static GameObject ResolvePlayerSource(GameObject source)
    {
        if (source == null)
        {
            return null;
        }

        if (BattleTargetUtility.IsPlayer(source))
        {
            return source;
        }

        CombatHealth combatHealth = source.GetComponentInParent<CombatHealth>();
        if (combatHealth != null && BattleTargetUtility.IsPlayer(combatHealth.gameObject))
        {
            return combatHealth.gameObject;
        }

        PlayerMovement movement = source.GetComponentInParent<PlayerMovement>(true);
        if (movement != null)
        {
            return movement.gameObject;
        }

        Player01SkillController player01 = source.GetComponentInParent<Player01SkillController>(true);
        if (player01 != null)
        {
            return player01.gameObject;
        }

        Player2PrototypeController player02 = source.GetComponentInParent<Player2PrototypeController>(true);
        if (player02 != null)
        {
            return player02.gameObject;
        }

        PlayerDayNightAffinity affinity = source.GetComponentInParent<PlayerDayNightAffinity>(true);
        return affinity != null ? affinity.gameObject : null;
    }

    private static string GetObjectName(GameObject target)
    {
        return target != null ? target.name : "<null>";
    }
}
