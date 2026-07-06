using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement - Base Speed")]
    [Tooltip("角色普通移动的基础速度。Player01 与 Player02 可分别在各自实例上单独调整。")]
    [InspectorName("baseMoveSpeed")]
    public float moveSpeed = 5f;
    [Tooltip("基础移动速度的额外缩放。默认 1 表示保持原始基础速度。")]
    [SerializeField, Min(0f)] private float playerBaseMoveSpeedScale = 1.0f;
    [Tooltip("SPD 每超过 1 点时，对普通移动速度提供的比例加成。默认 0.0075 与旧公式一致。")]
    [SerializeField, Min(0f)] private float speedStatMoveRatio = 0.0075f;
    [Tooltip("普通移动的最终速度硬上限。只影响走路/跑步，不影响技能位移。")]
    [SerializeField, Min(0f)] private float maxActualMoveSpeed = 30f;
    public Rigidbody rb;
    [Header("Debug")]
    [SerializeField] private bool debugSpeedDiagnostics = false;
    [SerializeField, Min(0.1f)] private float debugSpeedLogInterval = 1f;
    private CombatStats combatStats;
    private Player01SkillController player01SkillController;
    private float nextSpeedDiagnosticTime;
    private bool movementInputLocked;
    private bool loggedMovementBlocked;

    public float RawResolvedMoveSpeed { get; private set; }
    public float ActualMoveSpeed { get; private set; }
    public float ExcessMoveSpeed { get; private set; }
    public float ExcessMoveSpeedDamageBonus => ExcessMoveSpeed * BattleStatUtility.PlayerExcessMoveSpeedDamageBonusPerPoint;
    public float SpeedStatMoveRatio => Mathf.Max(0f, speedStatMoveRatio);
    public float MaxActualMoveSpeed => Mathf.Max(0f, maxActualMoveSpeed);

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        combatStats = GetComponent<CombatStats>();
        player01SkillController = GetComponent<Player01SkillController>();
        Debug.Log($"[PlayerMovement] active on {name}, controller={player01SkillController}", this);
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

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
        if (shouldBlockInputMovement)
        {
            if (!loggedMovementBlocked)
            {
                Debug.Log($"[PlayerMovement] movement input blocked on {name}", this);
                loggedMovementBlocked = true;
            }
        }
        else
        {
            loggedMovementBlocked = false;
        }

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
            loggedMovementBlocked = false;
        }
        else if (!locked)
        {
            loggedMovementBlocked = false;
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
}
