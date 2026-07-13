using System.Reflection;
using AHD2TimeOfDay;
using UnityEngine;

public static class TODDayNightAdapter
{
    private static TODController cachedController;
    private static object cachedParameters;
    private static bool warnedMissingController;

    public static bool TryGetIsDay(out bool isDay)
    {
        isDay = false;
        if (!TryReadDayNightValue(out float dayNightValue))
        {
            return false;
        }

        isDay = dayNightValue < 0.5f;
        return true;
    }

    public static bool TryGetIsNight(out bool isNight)
    {
        isNight = false;
        if (!TryReadDayNightValue(out float dayNightValue))
        {
            return false;
        }

        isNight = dayNightValue >= 0.5f;
        return true;
    }

    public static string GetDebugPhaseName()
    {
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

    private static bool TryReadDayNightValue(out float value)
    {
        value = 0f;
        object parameters = ResolveParameters();
        if (parameters == null)
        {
            return false;
        }

        if (TryReadFloat(parameters, out value, "DayOrNight", "dayOrNight", "_dayOrNight"))
        {
            value = Mathf.Clamp01(value);
            return true;
        }

        return false;
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
