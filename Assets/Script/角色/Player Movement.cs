using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public Rigidbody rb;
    [Header("Debug")]
    [SerializeField] private bool debugSpeedDiagnostics = false;
    [SerializeField, Min(0.1f)] private float debugSpeedLogInterval = 1f;
    private CombatStats combatStats;
    private float nextSpeedDiagnosticTime;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        combatStats = GetComponent<CombatStats>();
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
        float baseMoveSpeed = BattleStatUtility.ResolveBaseMoveSpeed(combatStats, moveSpeed);
        float speedMoveMultiplier = BattleStatUtility.GetSpeedMoveMultiplier(combatStats);
        float externalMoveMultiplier = 1f;
        float finalMoveSpeed = baseMoveSpeed * speedMoveMultiplier * externalMoveMultiplier;

        rb.linearVelocity = moveDirection * finalMoveSpeed;

        if (debugSpeedDiagnostics && Time.time >= nextSpeedDiagnosticTime)
        {
            nextSpeedDiagnosticTime = Time.time + Mathf.Max(0.1f, debugSpeedLogInterval);
            Debug.Log(
                $"[SpeedDiag] name={name} stats.speed={statsSpeed:F2} baseMoveSpeed={baseMoveSpeed:F2} speedMoveMultiplier={speedMoveMultiplier:F2} externalMoveMultiplier={externalMoveMultiplier:F2} finalMoveSpeed={finalMoveSpeed:F2} baseAttackCooldown=n/a speedCooldownMultiplier=n/a finalAttackCooldown=n/a",
                this);
        }
    }
}
