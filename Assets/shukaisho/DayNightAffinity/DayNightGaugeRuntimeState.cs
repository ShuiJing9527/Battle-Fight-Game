using UnityEngine;

public class DayNightGaugeRuntimeState : MonoBehaviour
{
    private const string RuntimeObjectName = "DayNightGaugeRuntimeState";

    private static DayNightGaugeRuntimeState instance;

    [SerializeField, Range(0f, 100f)] private float initialBalanceValue = 50f;
    [SerializeField, Min(0f)] private float gaugeGainPerHit = 3f;
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool debugHitFlow = false;

    [field: SerializeField, Range(0f, 100f)]
    public float BalanceValue { get; private set; } = 50f;

    public static DayNightGaugeRuntimeState Instance => ResolveInstance();
    public float RadianceValue => BalanceValue;
    public float TwilightValue => 100f - BalanceValue;
    public float GaugeGainPerHit => gaugeGainPerHit;
    public bool DebugLogEnabled => debugLog;
    public bool DebugHitFlowEnabled => debugHitFlow;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        BalanceValue = Mathf.Clamp(initialBalanceValue, 0f, 100f);
        DontDestroyOnLoad(gameObject);
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

    public void ResetGauge()
    {
        SetBalance(initialBalanceValue, "ResetGauge", Mathf.Abs(initialBalanceValue - BalanceValue));
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
            return true;
        }

        gauge = Object.FindObjectOfType<DayNightGaugeRuntimeState>();
        if (gauge != null)
        {
            instance = gauge;
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
            return instance;
        }

        GameObject runtimeObject = new GameObject(RuntimeObjectName);
        instance = runtimeObject.AddComponent<DayNightGaugeRuntimeState>();
        return instance;
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
