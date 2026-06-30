using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [InspectorName("baseMoveSpeed")]
    public float moveSpeed = 5f;
    public Rigidbody rb;
    [Header("Debug")]
    [SerializeField] private bool debugSpeedDiagnostics = false;
    [SerializeField, Min(0.1f)] private float debugSpeedLogInterval = 1f;
    private CombatStats combatStats;
    private Player01SkillController player01SkillController;
    private float nextSpeedDiagnosticTime;
    private bool movementInputLocked;
    private bool loggedMovementBlocked;

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
        float moveMultiplierFromSpeed = BattleStatUtility.GetSpeedMoveMultiplier(combatStats);
        float evasionMultiplier = BattleStatUtility.GetEvasionMultiplier(combatStats);
        float finalEvasionChance = BattleStatUtility.GetEvasionChance(combatStats);
        float externalMoveMultiplier = 1f;
        float finalMoveSpeed = BattleStatUtility.ResolveMoveSpeed(combatStats, moveSpeed, externalMoveMultiplier);
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
                moveDirection.x * finalMoveSpeed,
                rb.linearVelocity.y,
                moveDirection.z * finalMoveSpeed);
        }

        if (debugSpeedDiagnostics && Time.time >= nextSpeedDiagnosticTime)
        {
            nextSpeedDiagnosticTime = Time.time + Mathf.Max(0.1f, debugSpeedLogInterval);
            Debug.Log(
                $"[SpeedDiag] name={name} stats.speed={statsSpeed:F2} stats.luck={(combatStats != null ? Mathf.Max(0f, combatStats.luck) : 0f):F2} baseMoveSpeed={moveSpeed:F2} moveMultiplierFromSpeed={moveMultiplierFromSpeed:F2} externalMoveMultiplier={externalMoveMultiplier:F2} finalMoveSpeed={finalMoveSpeed:F2} evasionMultiplier={evasionMultiplier:F2} finalEvasionChance={finalEvasionChance:P2}",
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
    }
}
