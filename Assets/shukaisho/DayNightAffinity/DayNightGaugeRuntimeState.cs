using UnityEngine;

public class DayNightGaugeRuntimeState : MonoBehaviour
{
    private const string RuntimeObjectName = "DayNightGaugeRuntimeState";

    private static DayNightGaugeRuntimeState instance;

    [SerializeField, Range(0f, 100f)] private float initialBalanceValue = 50f;
    [SerializeField, Min(0f)] private float gaugeGainPerHit = 3f;
    [SerializeField] private bool debugLog = false;

    [field: SerializeField, Range(0f, 100f)]
    public float BalanceValue { get; private set; } = 50f;

    public static DayNightGaugeRuntimeState Instance => ResolveInstance();
    public float RadianceValue => BalanceValue;
    public float TwilightValue => 100f - BalanceValue;
    public float GaugeGainPerHit => gaugeGainPerHit;
    public bool DebugLogEnabled => debugLog;

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
        SetBalance(BalanceValue + Mathf.Max(0f, amount), "AddRadiance");
    }

    public void AddTwilight(float amount)
    {
        SetBalance(BalanceValue - Mathf.Max(0f, amount), "AddTwilight");
    }

    public void ResetGauge()
    {
        SetBalance(initialBalanceValue, "ResetGauge");
    }

    private void SetBalance(float newValue, string source)
    {
        float previous = BalanceValue;
        BalanceValue = Mathf.Clamp(newValue, 0f, 100f);

        if (debugLog)
        {
            Debug.Log(
                $"[DayNightGauge] source={source} previousBalance={previous:F2} currentBalance={BalanceValue:F2} twilight={TwilightValue:F2} radiance={RadianceValue:F2}",
                this);
        }
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
}
