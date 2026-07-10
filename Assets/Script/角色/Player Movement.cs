using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement - Base Speed")]
    [Tooltip("隗定牡譎ｮ騾夂ｧｻ蜉ｨ逧・渕遑騾溷ｺｦ縲１layer01 荳・Player02 蜿ｯ蛻・悪蝨ｨ蜷・・螳樔ｾ倶ｸ雁黒迢ｬ隹・紛縲・")]
    [InspectorName("baseMoveSpeed")]
    public float moveSpeed = 5f;

    [Tooltip("蝓ｺ遑遘ｻ蜉ｨ騾溷ｺｦ逧・｢晏､也ｼｩ謾ｾ縲るｻ倩ｮ､ 1 陦ｨ遉ｺ菫晄戟蜴溷ｧ句渕遑騾溷ｺｦ縲・")]
    [SerializeField, Min(0f)] private float playerBaseMoveSpeedScale = 1.0f;

    [Tooltip("SPD 豈剰ｶ・ｿ・1 轤ｹ譌ｶ・悟ｯｹ譎ｮ騾夂ｧｻ蜉ｨ騾溷ｺｦ謠蝉ｾ帷噪豈比ｾ句刈謌舌るｻ倩ｮ､ 0.0075 荳取立蜈ｬ蠑丈ｸ閾ｴ縲・")]
    [SerializeField, Min(0f)] private float speedStatMoveRatio = 0.0075f;

    [Tooltip("譎ｮ騾夂ｧｻ蜉ｨ逧・怙扈磯溷ｺｦ遑ｬ荳企剞縲ょ宵蠖ｱ蜩崎ｵｰ霍ｯ/霍第ｭ･・御ｸ榊ｽｱ蜩肴橿閭ｽ菴咲ｧｻ縲・")]
    [SerializeField, Min(0f)] private float maxActualMoveSpeed = 30f;

    public Rigidbody rb;

    [Header("Debug")]
    [SerializeField] private bool debugSpeedDiagnostics = false;
    [SerializeField, Min(0.1f)] private float debugSpeedLogInterval = 1f;

    private CombatStats combatStats;
    private Player01SkillController player01SkillController;
    private float nextSpeedDiagnosticTime;
    private bool movementInputLocked;

    public float RawResolvedMoveSpeed { get; private set; }
    public float ActualMoveSpeed { get; private set; }
    public float ExcessMoveSpeed { get; private set; }
    public float ExcessMoveSpeedDamageBonus => ExcessMoveSpeed * BattleStatUtility.PlayerExcessMoveSpeedDamageBonusPerPoint;
    public float SpeedStatMoveRatio => Mathf.Max(0f, speedStatMoveRatio);
    public float MaxActualMoveSpeed => Mathf.Max(0f, maxActualMoveSpeed);

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        combatStats = GetComponent<CombatStats>();
        player01SkillController = GetComponent<Player01SkillController>();
    }

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
            if (Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
            if (Keyboard.current.upArrowKey.isPressed) input.y += 1f;
            input = Vector2.ClampMagnitude(input, 1f);
        }

        Vector3 moveDirection = new Vector3(input.x, 0f, input.y);
        float statsSpeed = combatStats != null ? Mathf.Max(0f, combatStats.speed) : 0f;
        float evasionMultiplier = BattleStatUtility.GetEvasionMultiplier(combatStats);
        float finalEvasionChance = BattleStatUtility.GetEvasionChance(combatStats);
        float externalMoveMultiplier = 1f;
        float scaledBaseMoveSpeed = moveSpeed * Mathf.Max(0f, playerBaseMoveSpeedScale);
        float moveMultiplierFromSpeed = 1f + Mathf.Max(0f, statsSpeed - 1f) * Mathf.Max(0f, speedStatMoveRatio);
        float speedStatBonus = Mathf.Max(0f, statsSpeed - 1f) * scaledBaseMoveSpeed * Mathf.Max(0f, speedStatMoveRatio);

        RawResolvedMoveSpeed = (scaledBaseMoveSpeed + speedStatBonus) * Mathf.Max(0f, externalMoveMultiplier);
        float resolvedMoveSpeedCap = Mathf.Max(0f, maxActualMoveSpeed);
        ExcessMoveSpeed = Mathf.Max(0f, RawResolvedMoveSpeed - resolvedMoveSpeedCap);
        ActualMoveSpeed = Mathf.Min(Mathf.Max(0f, RawResolvedMoveSpeed), resolvedMoveSpeedCap);

        bool isLockedByController = player01SkillController != null && player01SkillController.IsMovementInputLocked();
        bool shouldBlockInputMovement = movementInputLocked || isLockedByController;

        if (!shouldBlockInputMovement)
        {
            rb.linearVelocity = new Vector3(
                moveDirection.x * ActualMoveSpeed,
                rb.linearVelocity.y,
                moveDirection.z * ActualMoveSpeed);
        }

        if (debugSpeedDiagnostics && Time.time >= nextSpeedDiagnosticTime)
        {
            nextSpeedDiagnosticTime = Time.time + Mathf.Max(0.1f, debugSpeedLogInterval);
            Debug.Log(
                $"[SpeedDiag] name={name} stats.speed={statsSpeed:F2} stats.luck={(combatStats != null ? Mathf.Max(0f, combatStats.luck) : 0f):F2} baseMoveSpeed={moveSpeed:F2} playerBaseMoveSpeedScale={playerBaseMoveSpeedScale:F2} scaledBaseMoveSpeed={scaledBaseMoveSpeed:F2} speedStatMoveRatio={speedStatMoveRatio:F4} moveMultiplierFromSpeed={moveMultiplierFromSpeed:F2} externalMoveMultiplier={externalMoveMultiplier:F2} moveSpeedCap={resolvedMoveSpeedCap:F2} rawMoveSpeed={RawResolvedMoveSpeed:F2} actualMoveSpeed={ActualMoveSpeed:F2} excessMoveSpeed={ExcessMoveSpeed:F2} excessDamageBonus={ExcessMoveSpeedDamageBonus:P2} evasionMultiplier={evasionMultiplier:F2} finalEvasionChance={finalEvasionChance:P2}",
                this);
        }
    }

    public void SetMovementInputLocked(bool locked)
    {
        movementInputLocked = locked;
        if (locked && rb != null)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }
    }

    public bool IsMovementInputLocked()
    {
        return movementInputLocked;
    }

    private void OnDisable()
    {
        movementInputLocked = false;
        RawResolvedMoveSpeed = 0f;
        ActualMoveSpeed = 0f;
        ExcessMoveSpeed = 0f;
    }

    public static void LogVelocityWrite(
        Component context,
        string writerScript,
        string writerMethod,
        Rigidbody targetRb,
        Vector3 velocityBefore,
        Vector3 velocityAfter,
        string reason,
        string skillState,
        string switchState,
        string spawnState)
    {
    }
}
