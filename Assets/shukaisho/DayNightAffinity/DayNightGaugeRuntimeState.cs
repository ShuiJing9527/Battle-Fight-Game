using UnityEngine;

public class DayNightGaugeRuntimeState : MonoBehaviour
{
    private const string RuntimeObjectName = "DayNightGaugeRuntimeState";
    private const string TwinShiftRadianceToTwilightSource = "TwinShiftRadianceToTwilight";
    private const string TwinShiftTwilightToRadianceSource = "TwinShiftTwilightToRadiance";

    private static DayNightGaugeRuntimeState instance;

    [SerializeField, Range(0f, 100f)] private float initialBalanceValue = 50f;
    [SerializeField, Min(0f)] private float gaugeGainPerHit = 3f;
    [SerializeField, Range(0f, 100f)] private float buffActivationThreshold = 100f;
    [SerializeField, Min(0f)] private float activationEpsilon = 0.001f;
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool debugHitFlow = false;
    [SerializeField] private bool debugAffinityDamage = false;

    [field: SerializeField, Range(0f, 100f)]
    public float BalanceValue { get; private set; } = 50f;

    public static DayNightGaugeRuntimeState Instance => ResolveInstance();
    public float RadianceValue => BalanceValue;
    public float TwilightValue => 100f - BalanceValue;
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
        BalanceValue = Mathf.Clamp(initialBalanceValue, 0f, 100f);
        DontDestroyOnLoad(gameObject);
        LogLifecycle($"Awake set-instance balance={BalanceValue:F2} twilight={TwilightValue:F2} radiance={RadianceValue:F2}");
    }

    private void OnEnable()
    {
        LogLifecycle($"OnEnable balance={BalanceValue:F2} twilight={TwilightValue:F2} radiance={RadianceValue:F2}");
    }

    public void AddRadiance(float amount)
    {
        float clampedAmount = Mathf.Max(0f, amount);
        SetBalance(BalanceValue + clampedAmount, "AddRadiance", clampedAmount);
    }

    public void AddTwilight(float amount)
    {
        float clampedAmount = Mathf.Max(0f, amount);
        SetBalance(BalanceValue - clampedAmount, "AddTwilight", clampedAmount);
    }

    public bool TryConsumeRadiance(float amount)
    {
        float clampedAmount = Mathf.Max(0f, amount);
        if (clampedAmount <= 0f)
        {
            return true;
        }

        if (RadianceValue + activationEpsilon < clampedAmount)
        {
            return false;
        }

        SetBalance(BalanceValue - clampedAmount, "ConsumeRadiance", clampedAmount);
        return true;
    }

    public bool TryConsumeTwilight(float amount)
    {
        float clampedAmount = Mathf.Max(0f, amount);
        if (clampedAmount <= 0f)
        {
            return true;
        }

        if (TwilightValue + activationEpsilon < clampedAmount)
        {
            return false;
        }

        SetBalance(BalanceValue + clampedAmount, "ConsumeTwilight", clampedAmount);
        return true;
    }

    public bool TryShiftRadianceToTwilight(float requiredRadiance, float twilightGain)
    {
        float requiredAmount = Mathf.Max(0f, requiredRadiance);
        float gainAmount = Mathf.Max(0f, twilightGain);

        if (requiredAmount > 0f && RadianceValue + activationEpsilon < requiredAmount)
        {
            LogLifecycle(
                $"TryShiftRadianceToTwilight failed requiredRadiance={requiredAmount:F2} currentRadiance={RadianceValue:F2} currentTwilight={TwilightValue:F2}");
            return false;
        }

        SetBalance(BalanceValue - gainAmount, TwinShiftRadianceToTwilightSource, gainAmount);
        return true;
    }

    public bool TryShiftTwilightToRadiance(float requiredTwilight, float radianceGain)
    {
        float requiredAmount = Mathf.Max(0f, requiredTwilight);
        float gainAmount = Mathf.Max(0f, radianceGain);

        if (requiredAmount > 0f && TwilightValue + activationEpsilon < requiredAmount)
        {
            LogLifecycle(
                $"TryShiftTwilightToRadiance failed requiredTwilight={requiredAmount:F2} currentRadiance={RadianceValue:F2} currentTwilight={TwilightValue:F2}");
            return false;
        }

        SetBalance(BalanceValue + gainAmount, TwinShiftTwilightToRadianceSource, gainAmount);
        return true;
    }

    public void ResetGauge()
    {
        SetBalance(initialBalanceValue, "ResetGauge", Mathf.Abs(initialBalanceValue - BalanceValue));
    }

    public void SetDebugLogEnabled(bool enabled)
    {
        debugLog = enabled;
    }

    // Radiance/Twilight state is earned by filling the corresponding gauge to the threshold.
    // Day/night only changes whether that state is favorable or unfavorable.
    public bool HasRadianceState()
    {
        return RadianceValue >= buffActivationThreshold - activationEpsilon;
    }

    public bool HasTwilightState()
    {
        return TwilightValue >= buffActivationThreshold - activationEpsilon;
    }

    public bool IsRadianceBuffActive()
    {
        return HasRadianceState();
    }

    public bool IsTwilightBuffActive()
    {
        return HasTwilightState();
    }

    private void SetBalance(float newValue, string source, float amount)
    {
        float previous = BalanceValue;
        BalanceValue = Mathf.Clamp(newValue, 0f, 100f);

        if (debugLog)
        {
            Debug.Log(
                $"[DayNightGauge] source={source} oldBalance={previous:F2} amount={amount:F2} newBalance={BalanceValue:F2} twilight={TwilightValue:F2} radiance={RadianceValue:F2} instancePath={GetHierarchyPath(gameObject)}",
                this);
        }
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

    private void LogLifecycle(string message)
    {
        if (!debugLog)
        {
            return;
        }

        Debug.Log($"[DayNightGaugeLifecycle] instance={DebugInstanceLabel} path={GetHierarchyPath(gameObject)} {message}", this);
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
