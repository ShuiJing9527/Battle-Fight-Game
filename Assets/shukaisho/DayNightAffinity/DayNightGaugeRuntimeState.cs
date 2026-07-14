using UnityEngine;

public class DayNightGaugeRuntimeState : MonoBehaviour
{
    private const string RuntimeObjectName = "DayNightGaugeRuntimeState";
    private const string TwinShiftRadianceToTwilightSource = "TwinShiftRadianceToTwilight";
    private const string TwinShiftTwilightToRadianceSource = "TwinShiftTwilightToRadiance";
    private const float MaxGaugeValue = 100f;

    private static DayNightGaugeRuntimeState instance;

    [SerializeField, Range(0f, 100f)] private float initialBalanceValue = 50f;
    [SerializeField, Min(0f)] private float gaugeGainPerHit = 3f;
    [SerializeField, Range(0f, 100f)] private float buffActivationThreshold = 100f;
    [SerializeField, Min(0f)] private float activationEpsilon = 0.001f;
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool debugHitFlow = false;
    [SerializeField] private bool debugAffinityDamage = false;

    [SerializeField, Range(0f, 100f)] private float radiance;
    [SerializeField, Range(0f, 100f)] private float twilight;

    public static DayNightGaugeRuntimeState Instance => ResolveInstance();
    public float BalanceValue => Mathf.Clamp(50f + (radiance - twilight) * 0.5f, 0f, MaxGaugeValue);
    public float RadianceValue => radiance;
    public float TwilightValue => twilight;
    public float EmptyValue => Mathf.Clamp(MaxGaugeValue - radiance - twilight, 0f, MaxGaugeValue);
    public float GaugeGainPerHit => gaugeGainPerHit;
    public float BuffActivationThreshold => buffActivationThreshold;
    public float ActivationEpsilon => activationEpsilon;
    public bool DebugLogEnabled => debugLog;
    public bool DebugHitFlowEnabled => debugHitFlow;
    public bool DebugAffinityDamageEnabled => debugAffinityDamage;
    public string DebugInstanceLabel => $"{name}#{GetInstanceID()}";

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            LogLifecycle($"Awake duplicate-destroy existing={instance.DebugInstanceLabel} duplicate={DebugInstanceLabel}");
            Destroy(gameObject);
            return;
        }

        instance = this;
        InitializeGaugeValues();
        DontDestroyOnLoad(gameObject);
        LogLifecycle($"Awake set-instance {GetGaugeSnapshot()}");
    }

    private void OnEnable()
    {
        LogLifecycle($"OnEnable {GetGaugeSnapshot()}");
    }

    public void AddRadiance(float amount)
    {
        float clampedAmount = Mathf.Max(0f, amount);
        if (clampedAmount <= 0f)
        {
            return;
        }

        ApplyRadianceGain(clampedAmount, "AddRadiance");
    }

    public void AddTwilight(float amount)
    {
        float clampedAmount = Mathf.Max(0f, amount);
        if (clampedAmount <= 0f)
        {
            return;
        }

        ApplyTwilightGain(clampedAmount, "AddTwilight");
    }

    public bool TryConsumeRadiance(float amount)
    {
        float clampedAmount = Mathf.Max(0f, amount);
        if (clampedAmount <= 0f)
        {
            return true;
        }

        if (radiance + activationEpsilon < clampedAmount)
        {
            return false;
        }

        float previousRadiance = radiance;
        radiance = Mathf.Max(0f, radiance - clampedAmount);
        LogGaugeChange("ConsumeRadiance", clampedAmount, previousRadiance, twilight);
        return true;
    }

    public bool TryConsumeTwilight(float amount)
    {
        float clampedAmount = Mathf.Max(0f, amount);
        if (clampedAmount <= 0f)
        {
            return true;
        }

        if (twilight + activationEpsilon < clampedAmount)
        {
            return false;
        }

        float previousTwilight = twilight;
        twilight = Mathf.Max(0f, twilight - clampedAmount);
        LogGaugeChange("ConsumeTwilight", clampedAmount, radiance, previousTwilight);
        return true;
    }

    public bool TryShiftRadianceToTwilight(float requiredRadiance, float twilightGain)
    {
        return TryConsumeFullRadianceAndSeedTwilight(twilightGain, out _);
    }

    public bool TryShiftTwilightToRadiance(float requiredTwilight, float radianceGain)
    {
        return TryConsumeFullTwilightAndSeedRadiance(radianceGain, out _);
    }

    public bool TryConsumeFullTwilightAndSeedRadiance(float seedAmount, out float consumedTwilight)
    {
        consumedTwilight = 0f;
        if (!HasTwilightState())
        {
            LogLifecycle($"TryConsumeFullTwilightAndSeedRadiance blocked {GetGaugeSnapshot()}");
            return false;
        }

        consumedTwilight = twilight;
        SetGaugeValues(Mathf.Clamp(seedAmount, 0f, MaxGaugeValue), 0f, TwinShiftTwilightToRadianceSource, consumedTwilight);
        return true;
    }

    public bool TryConsumeFullRadianceAndSeedTwilight(float seedAmount, out float consumedRadiance)
    {
        consumedRadiance = 0f;
        if (!HasRadianceState())
        {
            LogLifecycle($"TryConsumeFullRadianceAndSeedTwilight blocked {GetGaugeSnapshot()}");
            return false;
        }

        consumedRadiance = radiance;
        SetGaugeValues(0f, Mathf.Clamp(seedAmount, 0f, MaxGaugeValue), TwinShiftRadianceToTwilightSource, consumedRadiance);
        return true;
    }

    public void ResetGauge()
    {
        InitializeGaugeValues();
        LogLifecycle($"ResetGauge {GetGaugeSnapshot()}");
    }

    public void SetDebugLogEnabled(bool enabled)
    {
        debugLog = enabled;
    }

    public bool HasRadianceState()
    {
        return radiance >= buffActivationThreshold - activationEpsilon;
    }

    public bool HasTwilightState()
    {
        return twilight >= buffActivationThreshold - activationEpsilon;
    }

    public bool IsRadianceBuffActive()
    {
        return HasRadianceState();
    }

    public bool IsTwilightBuffActive()
    {
        return HasTwilightState();
    }

    public static bool TryGetExistingInstance(out DayNightGaugeRuntimeState gauge)
    {
        if (instance != null)
        {
            gauge = instance;
            gauge.LogLifecycle("TryGetExistingInstance reuse-static-instance");
            return true;
        }

        gauge = Object.FindObjectOfType<DayNightGaugeRuntimeState>();
        if (gauge != null)
        {
            instance = gauge;
            gauge.LogLifecycle("TryGetExistingInstance found-scene-instance");
            return true;
        }

        return false;
    }

    private static DayNightGaugeRuntimeState ResolveInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = Object.FindObjectOfType<DayNightGaugeRuntimeState>();
        if (instance != null)
        {
            instance.LogLifecycle("ResolveInstance found-scene-instance");
            return instance;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        instance = runtimeObject.AddComponent<DayNightGaugeRuntimeState>();
        instance.LogLifecycle("ResolveInstance created-runtime-instance");
        return instance;
    }

    private void InitializeGaugeValues()
    {
        float initialRadiance = Mathf.Clamp(initialBalanceValue, 0f, MaxGaugeValue);
        float initialTwilight = Mathf.Clamp(MaxGaugeValue - initialRadiance, 0f, MaxGaugeValue);
        SetGaugeValuesWithoutLogging(initialRadiance, initialTwilight);
    }

    private void ApplyRadianceGain(float amount, string source)
    {
        float previousRadiance = radiance;
        float previousTwilight = twilight;
        float availableEmpty = EmptyValue;
        float fillAmount = Mathf.Min(amount, availableEmpty);
        float overflow = Mathf.Max(0f, amount - fillAmount);

        radiance += fillAmount;

        if (overflow > 0f)
        {
            float converted = Mathf.Min(overflow, twilight);
            twilight -= converted;
            radiance += converted;
        }

        ClampGaugeValues();
        LogGaugeChange(source, amount, previousRadiance, previousTwilight);
    }

    private void ApplyTwilightGain(float amount, string source)
    {
        float previousRadiance = radiance;
        float previousTwilight = twilight;
        float availableEmpty = EmptyValue;
        float fillAmount = Mathf.Min(amount, availableEmpty);
        float overflow = Mathf.Max(0f, amount - fillAmount);

        twilight += fillAmount;

        if (overflow > 0f)
        {
            float converted = Mathf.Min(overflow, radiance);
            radiance -= converted;
            twilight += converted;
        }

        ClampGaugeValues();
        LogGaugeChange(source, amount, previousRadiance, previousTwilight);
    }

    private void SetGaugeValues(float newRadiance, float newTwilight, string source, float amount)
    {
        float previousRadiance = radiance;
        float previousTwilight = twilight;
        SetGaugeValuesWithoutLogging(newRadiance, newTwilight);
        LogGaugeChange(source, amount, previousRadiance, previousTwilight);
    }

    private void SetGaugeValuesWithoutLogging(float newRadiance, float newTwilight)
    {
        radiance = Mathf.Clamp(newRadiance, 0f, MaxGaugeValue);
        twilight = Mathf.Clamp(newTwilight, 0f, MaxGaugeValue);
        ClampGaugeValues();
    }

    private void ClampGaugeValues()
    {
        radiance = Mathf.Clamp(radiance, 0f, MaxGaugeValue);
        twilight = Mathf.Clamp(twilight, 0f, MaxGaugeValue);

        float total = radiance + twilight;
        if (total <= MaxGaugeValue + activationEpsilon)
        {
            return;
        }

        float overflow = total - MaxGaugeValue;
        if (twilight >= overflow)
        {
            twilight -= overflow;
        }
        else
        {
            overflow -= twilight;
            twilight = 0f;
            radiance = Mathf.Max(0f, radiance - overflow);
        }
    }

    private void LogGaugeChange(string source, float amount, float previousRadiance, float previousTwilight)
    {
        if (!debugLog)
        {
            return;
        }

        Debug.Log(
            $"[DayNightGauge] source={source} amount={amount:F2} oldRadiance={previousRadiance:F2} oldTwilight={previousTwilight:F2} oldEmpty={Mathf.Clamp(MaxGaugeValue - previousRadiance - previousTwilight, 0f, MaxGaugeValue):F2} newRadiance={radiance:F2} newTwilight={twilight:F2} newEmpty={EmptyValue:F2} balance={BalanceValue:F2} instancePath={GetHierarchyPath(gameObject)}",
            this);
    }

    private void LogLifecycle(string message)
    {
        if (!debugLog)
        {
            return;
        }

        Debug.Log($"[DayNightGaugeLifecycle] instance={DebugInstanceLabel} path={GetHierarchyPath(gameObject)} {message}", this);
    }

    private string GetGaugeSnapshot()
    {
        return $"balance={BalanceValue:F2} twilight={twilight:F2} radiance={radiance:F2} empty={EmptyValue:F2}";
    }

    private static string GetHierarchyPath(GameObject target)
    {
        if (target == null)
        {
            return "<null>";
        }

        Transform current = target.transform;
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        return path;
    }
}
