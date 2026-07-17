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
    private Coroutine externalLaunchRoutine;
    private bool isUnderExternalLaunch;
    [SerializeField, Min(0.01f)] private float minimumExternalLaunchRetriggerInterval = 0.15f;
    [SerializeField, Min(0.05f)] private float externalLaunchMinimumLockDuration = 0.15f;
    [SerializeField, Min(0.1f)] private float externalLaunchMaximumAirborneDuration = 2.5f;
    [SerializeField, Min(0.01f)] private float externalLaunchGroundProbeDistance = 0.2f;
    [SerializeField, Min(1)] private int externalLaunchLandingConfirmFrames = 2;
    [SerializeField, Min(0f)] private float externalLaunchGroundSkin = 0.02f;
    [SerializeField, Min(0.1f)] private float externalLaunchFallRecoveryDistance = 6f;
    [SerializeField] private bool debugExternalLaunchMotion = true;
    [SerializeField] private bool debugPlayerAirborneLanding = false;
    private int lastExternalLaunchSequenceId;
    private float lastExternalLaunchReceivedTime = -999f;
    private int externalLaunchStartFrame = -1;
    private bool externalLaunchReceivedThisFrame;
    private int externalLaunchGroundedFrameCount;
    private float previousExternalLaunchVelocityY;
    private float previousExternalLaunchRootY;
    private float previousExternalLaunchRbY;
    private Vector3 lastValidGroundedPosition;
    private bool hasLastValidGroundedPosition;

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

        if (isUnderExternalLaunch)
        {
            TraceExternalLaunchStep("FixedUpdate");
            return;
        }

        UpdateLastValidGroundedPosition();

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

    public void ApplyExternalLaunch(Vector3 launchVelocity, float lockDuration, Vector3 separationOffset, int launchSequenceId = 0)
    {
        bool duplicateSequence = launchSequenceId > 0 &&
                                 launchSequenceId == lastExternalLaunchSequenceId &&
                                 Time.time - lastExternalLaunchReceivedTime <= Mathf.Max(0.01f, minimumExternalLaunchRetriggerInterval);
        Debug.Log(
            "[BossLaunchRepeatTrace] event=LaunchReceived " +
            "controller=PlayerMovement" +
            " target=" + name +
            " frame=" + Time.frameCount +
            " fixedTime=" + Time.fixedTime.ToString("F3") +
            " launchSequenceId=" + launchSequenceId +
            " previousLaunchSequenceId=" + lastExternalLaunchSequenceId +
            " externalLaunchAlreadyActive=" + isUnderExternalLaunch +
            " currentVelocityBefore=" + (rb != null ? rb.linearVelocity.ToString() : "<no-rigidbody>") +
            " launchVelocity=" + launchVelocity +
            " duplicateSequence=" + duplicateSequence,
            this);

        if (duplicateSequence)
        {
            return;
        }

        Debug.Log(
            "[ExternalLaunchYTrace] event=LaunchStart " +
            "controller=PlayerMovement" +
            " target=" + name +
            " frame=" + Time.frameCount +
            " positionBefore=" + transform.position +
            " launchVelocity=" + launchVelocity +
            " separationOffset=" + separationOffset +
            " lockDuration=" + lockDuration,
            this);

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb == null)
        {
            return;
        }

        if (externalLaunchRoutine != null)
        {
            StopCoroutine(externalLaunchRoutine);
            externalLaunchRoutine = null;
        }

        Vector3 groundedSafeSeparationOffset = ResolveGroundSafeSeparationOffset(separationOffset, launchVelocity);
        if (groundedSafeSeparationOffset.sqrMagnitude > 0.0001f)
        {
            Vector3 newPosition = rb.position + groundedSafeSeparationOffset;
            rb.position = newPosition;
            transform.position = newPosition;
            Physics.SyncTransforms();
        }

        movementInputLocked = true;
        isUnderExternalLaunch = true;
        externalLaunchStartFrame = Time.frameCount;
        externalLaunchReceivedThisFrame = true;
        externalLaunchGroundedFrameCount = 0;
        previousExternalLaunchVelocityY = launchVelocity.y;
        previousExternalLaunchRootY = transform.position.y;
        previousExternalLaunchRbY = rb.position.y;
        lastExternalLaunchSequenceId = launchSequenceId;
        lastExternalLaunchReceivedTime = Time.time;
        Vector3 velocityBefore = rb.linearVelocity;
        rb.linearVelocity = launchVelocity;
        rb.angularVelocity = Vector3.zero;
        rb.WakeUp();
        Debug.Log(
            "[PlayerLaunchMotionTrace] event=LaunchApplied " +
            "controller=PlayerMovement" +
            " frame=" + Time.frameCount +
            " positionAfterSeparation=" + transform.position +
            " velocityBefore=" + velocityBefore +
            " requestedLaunchVelocity=" + launchVelocity +
            " velocityAfter=" + rb.linearVelocity +
            " grounded=" + IsGroundedForExternalLaunch() +
            " useGravity=" + rb.useGravity +
            " isKinematic=" + rb.isKinematic +
            " constraints=" + rb.constraints +
            " sequenceId=" + launchSequenceId,
            this);
        float holdDuration = Mathf.Max(lockDuration, externalLaunchMinimumLockDuration);
        externalLaunchRoutine = StartCoroutine(ReleaseExternalLaunchRoutine(holdDuration));
    }

    private System.Collections.IEnumerator ReleaseExternalLaunchRoutine(float lockDuration)
    {
        if (lockDuration > 0f)
        {
            yield return new WaitForSeconds(lockDuration);
        }
        else
        {
            yield return null;
        }

        float startTime = Time.time;
        externalLaunchGroundedFrameCount = 0;
        while (Time.time - startTime < Mathf.Max(0.1f, externalLaunchMaximumAirborneDuration))
        {
            bool grounded = IsGroundedForExternalLaunch();
            Vector3 velocity = rb != null ? rb.linearVelocity : Vector3.zero;
            if (grounded && velocity.y <= 0.05f)
            {
                externalLaunchGroundedFrameCount++;
                if (externalLaunchGroundedFrameCount >= Mathf.Max(1, externalLaunchLandingConfirmFrames))
                {
                    break;
                }
            }
            else
            {
                externalLaunchGroundedFrameCount = 0;
            }

            yield return new WaitForFixedUpdate();
        }

        externalLaunchRoutine = null;
        ResolveExternalLaunchLanding("RoutineComplete");
    }

    private void OnDisable()
    {
        if (externalLaunchRoutine != null)
        {
            StopCoroutine(externalLaunchRoutine);
            externalLaunchRoutine = null;
        }

        movementInputLocked = false;
        isUnderExternalLaunch = false;
        externalLaunchReceivedThisFrame = false;
        RawResolvedMoveSpeed = 0f;
        ActualMoveSpeed = 0f;
        ExcessMoveSpeed = 0f;
    }

    private void TraceExternalLaunchStep(string source)
    {
        if (rb == null)
        {
            return;
        }

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 previousRbPosition = new Vector3(rb.position.x, previousExternalLaunchRbY, rb.position.z);
        bool grounded = IsGroundedForExternalLaunch();
        if (debugExternalLaunchMotion)
        {
            Debug.Log(
                "[PlayerLaunchMotionTrace] event=LaunchPhysicsStep " +
                "controller=PlayerMovement" +
                " frame=" + Time.frameCount +
                " fixedTime=" + Time.fixedTime.ToString("F3") +
                " rootPositionBefore=" + new Vector3(transform.position.x, previousExternalLaunchRootY, transform.position.z) +
                " rootPositionAfter=" + transform.position +
                " rigidbodyPositionBefore=" + new Vector3(rb.position.x, previousExternalLaunchRbY, rb.position.z) +
                " rigidbodyPositionAfter=" + rb.position +
                " velocityBefore=" + new Vector3(currentVelocity.x, previousExternalLaunchVelocityY, currentVelocity.z) +
                " velocityAfter=" + currentVelocity +
                " groundedAfter=" + grounded +
                " normalMovementExecuted=false" +
                " groundSnapExecuted=false" +
                " positionWriteSource=" + source +
                " velocityWriteSource=Physics",
                this);
        }

        if (TryResolvePassedGround(previousRbPosition, rb.position, currentVelocity, out RaycastHit landingHit, out float bottomOffset))
        {
            ResolveExternalLaunchLanding(
                "PassedGroundWithoutCollision",
                landingHit.point.y,
                bottomOffset,
                landingHit.collider,
                landingHit.normal);
            return;
        }

        if (ShouldTriggerFallRecovery())
        {
            ResolveExternalLaunchFallRecovery("ExceededSafeFallDistance");
            return;
        }

        if (!externalLaunchReceivedThisFrame &&
            currentVelocity.y > previousExternalLaunchVelocityY + 0.5f)
        {
            Debug.LogWarning(
                "[PlayerLaunchMotionTrace] event=IllegalUpwardVelocityIncrease " +
                "controller=PlayerMovement" +
                " frame=" + Time.frameCount +
                " beforeY=" + previousExternalLaunchVelocityY.ToString("F3") +
                " afterY=" + currentVelocity.y.ToString("F3") +
                " source=" + source +
                " launchReceivedThisFrame=false" +
                " jumpExecutedThisFrame=false" +
                " collidedThisFrame=Unknown",
                this);
        }

        previousExternalLaunchVelocityY = currentVelocity.y;
        previousExternalLaunchRootY = transform.position.y;
        previousExternalLaunchRbY = rb.position.y;
        externalLaunchReceivedThisFrame = false;
    }

    private Vector3 ResolveGroundSafeSeparationOffset(Vector3 separationOffset, Vector3 launchVelocity)
    {
        Vector3 horizontalOffset = Vector3.ProjectOnPlane(separationOffset, Vector3.up);
        if (horizontalOffset.sqrMagnitude <= 0.0001f || rb == null)
        {
            return separationOffset;
        }

        if (Mathf.Abs(launchVelocity.y) > 0.05f || !TryResolveGroundedSupport(out _, out _))
        {
            return horizontalOffset;
        }

        if (!TryResolveMainColliderBottomOffset(out float bottomOffset))
        {
            return horizontalOffset;
        }

        Vector3 candidate = rb.position + horizontalOffset;
        if (!TryResolveGroundYAt(candidate, out float groundY))
        {
            return horizontalOffset;
        }

        float safeRootY = groundY + bottomOffset + Mathf.Max(0f, externalLaunchGroundSkin);
        float deltaY = safeRootY - rb.position.y;
        return horizontalOffset + Vector3.up * deltaY;
    }

    private bool IsGroundedForExternalLaunch()
    {
        return TryResolveGroundedSupport(out _, out _);
    }

    private bool TryResolveGroundedSupport(out float groundY, out Collider groundCollider)
    {
        groundY = float.NegativeInfinity;
        groundCollider = null;
        if (!TryResolveMainColliderBottomOffset(out float bottomOffset) || rb == null)
        {
            return false;
        }

        Vector3 origin = rb.position + Vector3.up * Mathf.Max(0.2f, bottomOffset + 0.25f);
        float distance = Mathf.Max(0.2f, bottomOffset + Mathf.Max(0.01f, externalLaunchGroundProbeDistance) + 0.25f);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidGroundHit(hit.collider))
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                groundY = hit.point.y;
                groundCollider = hit.collider;
            }
        }

        return groundCollider != null;
    }

    private bool TryResolveGroundYAt(Vector3 candidatePosition, out float groundY)
    {
        groundY = float.NegativeInfinity;
        if (!TryResolveMainColliderBottomOffset(out float bottomOffset))
        {
            return false;
        }

        Vector3 origin = candidatePosition + Vector3.up * Mathf.Max(0.5f, bottomOffset + 1f);
        float distance = Mathf.Max(2f, bottomOffset + 2f);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidGroundHit(hit.collider))
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                groundY = hit.point.y;
            }
        }

        return !float.IsNegativeInfinity(groundY);
    }

    private bool TryResolveMainColliderBottomOffset(out float bottomOffset)
    {
        bottomOffset = 0f;
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        Bounds? combinedBounds = null;
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            combinedBounds = combinedBounds.HasValue
                ? EncapsulateBounds(combinedBounds.Value, collider.bounds)
                : collider.bounds;
        }

        if (!combinedBounds.HasValue)
        {
            return false;
        }

        bottomOffset = rb.position.y - combinedBounds.Value.min.y;
        return true;
    }

    private void UpdateLastValidGroundedPosition()
    {
        if (rb == null)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        if (velocity.y <= 0.05f && IsGroundedForExternalLaunch())
        {
            lastValidGroundedPosition = rb.position;
            hasLastValidGroundedPosition = true;
        }
    }

    private bool TryResolvePassedGround(
        Vector3 previousRbPosition,
        Vector3 currentRbPosition,
        Vector3 currentVelocity,
        out RaycastHit landingHit,
        out float bottomOffset)
    {
        landingHit = default;
        bottomOffset = 0f;
        if (rb == null || currentVelocity.y > 0.05f || !TryResolveMainColliderBottomOffset(out bottomOffset))
        {
            return false;
        }

        float skin = Mathf.Max(0.001f, externalLaunchGroundSkin);
        float previousFootY = previousRbPosition.y - bottomOffset;
        float currentFootY = currentRbPosition.y - bottomOffset;
        if (currentFootY >= previousFootY - skin)
        {
            return false;
        }

        Vector3 origin = previousRbPosition + Vector3.up * Mathf.Max(0.05f, skin);
        float castDistance = Mathf.Max(0.1f, previousFootY - currentFootY + bottomOffset + skin + 0.1f);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, castDistance, ~0, QueryTriggerInteraction.Ignore);
        float bestY = float.NegativeInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidGroundHit(hit.collider))
            {
                continue;
            }

            float hitY = hit.point.y;
            if (hitY <= previousFootY + skin && hitY >= currentFootY - skin && hitY > bestY)
            {
                bestY = hitY;
                landingHit = hit;
            }
        }

        return !float.IsNegativeInfinity(bestY);
    }

    private bool ShouldTriggerFallRecovery()
    {
        if (rb == null || !hasLastValidGroundedPosition)
        {
            return false;
        }

        return rb.position.y < lastValidGroundedPosition.y - Mathf.Max(0.1f, externalLaunchFallRecoveryDistance);
    }

    private void ResolveExternalLaunchFallRecovery(string reason)
    {
        if (rb == null)
        {
            return;
        }

        if (TryResolveGroundYAt(rb.position, out float groundY) && TryResolveMainColliderBottomOffset(out float bottomOffset))
        {
            ResolveExternalLaunchLanding(reason, groundY, bottomOffset, null, Vector3.up);
            return;
        }

        if (hasLastValidGroundedPosition)
        {
            MoveExternalLaunchRoot(lastValidGroundedPosition, reason, null, Vector3.up);
            FinishExternalLaunchState(reason);
        }
    }

    private void ResolveExternalLaunchLanding(string reason)
    {
        if (rb == null || !TryResolveGroundYAt(rb.position, out float groundY) || !TryResolveMainColliderBottomOffset(out float bottomOffset))
        {
            FinishExternalLaunchState(reason);
            return;
        }

        ResolveExternalLaunchLanding(reason, groundY, bottomOffset, null, Vector3.up);
    }

    private void ResolveExternalLaunchLanding(string reason, float groundY, float bottomOffset, Collider groundCollider, Vector3 groundNormal)
    {
        if (rb == null)
        {
            return;
        }

        float safeY = groundY + bottomOffset + Mathf.Max(0f, externalLaunchGroundSkin);
        Vector3 safePosition = new Vector3(rb.position.x, safeY, rb.position.z);
        MoveExternalLaunchRoot(safePosition, reason, groundCollider, groundNormal);
        FinishExternalLaunchState(reason);
    }

    private void MoveExternalLaunchRoot(Vector3 safePosition, string reason, Collider groundCollider, Vector3 groundNormal)
    {
        Vector3 positionBefore = rb != null ? rb.position : transform.position;
        Vector3 velocityBefore = rb != null ? rb.linearVelocity : Vector3.zero;
        if (rb != null)
        {
            rb.position = safePosition;
            transform.position = safePosition;
            rb.linearVelocity = new Vector3(velocityBefore.x, 0f, velocityBefore.z);
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }
        else
        {
            transform.position = safePosition;
        }

        Physics.SyncTransforms();
        lastValidGroundedPosition = safePosition;
        hasLastValidGroundedPosition = true;

        if (debugPlayerAirborneLanding)
        {
            Debug.Log(
                "[PlayerAirborneTrace] event=LandingResolved " +
                "controller=PlayerMovement" +
                "reason=" + reason +
                " finalPosition=" + safePosition +
                " positionBefore=" + positionBefore +
                " verticalVelocityBefore=" + velocityBefore.y.ToString("F3") +
                " verticalVelocityAfter=0.000" +
                " groundObject=" + (groundCollider != null ? groundCollider.name : "ResolvedGround") +
                " surfaceNormal=" + groundNormal +
                " airborneCleared=true",
                this);
        }
    }

    private void FinishExternalLaunchState(string reason)
    {
        if (externalLaunchRoutine != null)
        {
            StopCoroutine(externalLaunchRoutine);
            externalLaunchRoutine = null;
        }

        movementInputLocked = false;
        isUnderExternalLaunch = false;
        externalLaunchReceivedThisFrame = false;
        externalLaunchGroundedFrameCount = 0;

        if (rb != null)
        {
            Vector3 velocity = rb.linearVelocity;
            if (velocity.y < 0f)
            {
                velocity.y = 0f;
                rb.linearVelocity = velocity;
            }
        }

        if (debugPlayerAirborneLanding)
        {
            Debug.Log(
                "[PlayerAirborneTrace] event=FinishAirborneState " +
                "controller=PlayerMovement" +
                "reason=" + reason +
                " position=" + transform.position +
                " velocity=" + (rb != null ? rb.linearVelocity.ToString() : "None"),
                this);
        }
    }

    private bool IsValidGroundHit(Collider collider)
    {
        if (collider == null || !collider.enabled || collider.isTrigger)
        {
            return false;
        }

        if (collider.transform == transform || collider.transform.IsChildOf(transform))
        {
            return false;
        }

        if (collider.GetComponentInParent<PlayerMovement>() != null ||
            collider.GetComponentInParent<Player2PrototypeController>() != null ||
            collider.GetComponentInParent<EnemyController>() != null ||
            collider.GetComponentInParent<CombatHealth>() != null)
        {
            return false;
        }

        return true;
    }

    private static Bounds EncapsulateBounds(Bounds a, Bounds b)
    {
        a.Encapsulate(b.min);
        a.Encapsulate(b.max);
        return a;
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
