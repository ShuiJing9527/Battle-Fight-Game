using System.Reflection;
using AHD2TimeOfDay;
using UnityEngine;

public enum DayNightPhase
{
    Dawn,
    Day,
    Dusk,
    Night
}

public static class TODDayNightAdapter
{
    public const float DefaultDawnStartHour = 5f;
    public const float DefaultDayStartHour = 7f;
    public const float DefaultDuskStartHour = 17f;
    public const float DefaultNightStartHour = 19f;

    public static float DawnStartHour => ResolvePhaseBoundary(gauge => gauge.DawnStartHour, DefaultDawnStartHour);
    public static float DayStartHour => ResolvePhaseBoundary(gauge => gauge.DayStartHour, DefaultDayStartHour);
    public static float DuskStartHour => ResolvePhaseBoundary(gauge => gauge.DuskStartHour, DefaultDuskStartHour);
    public static float NightStartHour => ResolvePhaseBoundary(gauge => gauge.NightStartHour, DefaultNightStartHour);

    private static TODController cachedController;
    private static object cachedParameters;
    private static bool warnedMissingController;

    public static bool TryGetIsDay(out bool isDay)
    {
        isDay = false;
        if (!TryGetCurrentPhase(out DayNightPhase phase))
        {
            return false;
        }

        isDay = phase == DayNightPhase.Dawn || phase == DayNightPhase.Day;
        return true;
    }

    public static bool TryGetIsNight(out bool isNight)
    {
        isNight = false;
        if (!TryGetCurrentPhase(out DayNightPhase phase))
        {
            return false;
        }

        isNight = phase == DayNightPhase.Dusk || phase == DayNightPhase.Night;
        return true;
    }

    public static string GetDebugPhaseName()
    {
        if (TryGetCurrentPhase(out DayNightPhase phase))
        {
            return phase.ToString();
        }

        object parameters = ResolveParameters();
        if (parameters != null && TryReadMember(parameters, "currentTimeOfDay", out object currentTimeOfDay) && currentTimeOfDay != null)
        {
            if (currentTimeOfDay is Object unityObject)
            {
                return unityObject.name;
            }

            return currentTimeOfDay.ToString();
        }

        TODController controller = ResolveController();
        return controller != null ? controller.name : "Unavailable";
    }

    public static bool TryGetCurrentTimeHours(out float currentTimeHours)
    {
        currentTimeHours = 0f;
        object parameters = ResolveParameters();
        if (parameters == null)
        {
            return false;
        }

        if (TryReadFloat(parameters, out currentTimeHours, "CurrentTime", "_currentTime", "currentTime"))
        {
            currentTimeHours = NormalizeHour(currentTimeHours);
            return true;
        }

        return false;
    }

    public static bool TryGetCurrentPhase(out DayNightPhase phase)
    {
        phase = DayNightPhase.Night;
        if (!TryGetCurrentTimeHours(out float currentTimeHours))
        {
            return false;
        }

        phase = GetPhaseForHour(currentTimeHours);
        return true;
    }

    public static DayNightPhase GetPhaseForHour(float currentTimeHours)
    {
        float normalizedHour = NormalizeHour(currentTimeHours);
        float dawnStart = DawnStartHour;
        float dayStart = DayStartHour;
        float duskStart = DuskStartHour;
        float nightStart = NightStartHour;

        if (IsHourInRange(normalizedHour, dawnStart, dayStart))
        {
            return DayNightPhase.Dawn;
        }

        if (IsHourInRange(normalizedHour, dayStart, duskStart))
        {
            return DayNightPhase.Day;
        }

        if (IsHourInRange(normalizedHour, duskStart, nightStart))
        {
            return DayNightPhase.Dusk;
        }

        return DayNightPhase.Night;
    }

    public static void GetPhaseBoundaries(out float dawnStartHour, out float dayStartHour, out float duskStartHour, out float nightStartHour)
    {
        dawnStartHour = DawnStartHour;
        dayStartHour = DayStartHour;
        duskStartHour = DuskStartHour;
        nightStartHour = NightStartHour;
    }

    public static bool TryGetSunriseSunsetHours(out float sunriseHour, out float sunsetHour)
    {
        sunriseHour = DawnStartHour;
        sunsetHour = DuskStartHour;
        return true;
    }

    private static bool IsDayHour(float currentTimeHours)
    {
        DayNightPhase phase = GetPhaseForHour(currentTimeHours);
        return phase == DayNightPhase.Dawn || phase == DayNightPhase.Day;
    }

    private static bool IsHourInRange(float hour, float startHour, float endHour)
    {
        float normalizedHour = NormalizeHour(hour);
        float normalizedStart = NormalizeHour(startHour);
        float normalizedEnd = NormalizeHour(endHour);

        if (Mathf.Approximately(normalizedStart, normalizedEnd))
        {
            return false;
        }

        if (normalizedStart < normalizedEnd)
        {
            return normalizedHour >= normalizedStart && normalizedHour < normalizedEnd;
        }

        return normalizedHour >= normalizedStart || normalizedHour < normalizedEnd;
    }

    private static float ResolvePhaseBoundary(System.Func<DayNightGaugeRuntimeState, float> selector, float fallback)
    {
        if (selector != null && DayNightGaugeRuntimeState.TryGetExistingInstance(out DayNightGaugeRuntimeState gauge) && gauge != null)
        {
            return NormalizeHour(selector(gauge));
        }

        return NormalizeHour(fallback);
    }

    private static object ResolveParameters()
    {
        if (cachedParameters != null)
        {
            return cachedParameters;
        }

        TODController controller = ResolveController();
        if (controller == null)
        {
            return null;
        }

        if (TryReadMember(controller, "todGlobalParameters", out object parameters) && parameters != null)
        {
            cachedParameters = parameters;
        }

        return cachedParameters;
    }

    private static TODController ResolveController()
    {
        if (cachedController != null)
        {
            return cachedController;
        }

        TryResolveStaticInstance(out cachedController);
        if (cachedController == null)
        {
            cachedController = Object.FindObjectOfType<TODController>();
        }

        if (cachedController == null && !warnedMissingController)
        {
            warnedMissingController = true;
            Debug.LogWarning("[TODDayNightAdapter] TODController not found. Day/night affinity bonus will be skipped.");
        }

        return cachedController;
    }

    private static bool TryReadFloat(object source, out float value, params string[] memberNames)
    {
        value = 0f;
        for (int i = 0; i < memberNames.Length; i++)
        {
            if (!TryReadMember(source, memberNames[i], out object raw) || raw == null)
            {
                continue;
            }

            switch (raw)
            {
                case float f:
                    value = f;
                    return true;
                case int intValue:
                    value = intValue;
                    return true;
            }
        }

        return false;
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

    private static bool TryReadMember(object source, string memberName, out object value)
    {
        value = null;
        if (source == null || string.IsNullOrEmpty(memberName))
        {
            return false;
        }

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        System.Type type = source.GetType();
        FieldInfo field = type.GetField(memberName, flags);
        if (field != null)
        {
            value = field.GetValue(source);
            return true;
        }

        PropertyInfo property = type.GetProperty(memberName, flags);
        if (property != null)
        {
            value = property.GetValue(source);
            return true;
        }

        return false;
    }

    private static bool TryResolveStaticInstance(out TODController controller)
    {
        controller = null;
        BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        System.Type type = typeof(TODController);

        PropertyInfo property = type.GetProperty("Instance", flags);
        if (property != null && typeof(TODController).IsAssignableFrom(property.PropertyType))
        {
            controller = property.GetValue(null) as TODController;
            if (controller != null)
            {
                return true;
            }
        }

        FieldInfo field = type.GetField("Instance", flags);
        if (field != null && typeof(TODController).IsAssignableFrom(field.FieldType))
        {
            controller = field.GetValue(null) as TODController;
            if (controller != null)
            {
                return true;
            }
        }

        return false;
    }
}
