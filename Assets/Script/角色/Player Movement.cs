using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour, IExternalLaunchReceiver
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
    private CollisionDetectionMode externalLaunchCollisionDetectionModeBefore;
    private RigidbodyInterpolation externalLaunchInterpolationBefore;
    [SerializeField, Min(0.01f)] private float minimumExternalLaunchRetriggerInterval = 0.15f;
    [SerializeField, Min(0.05f)] private float externalLaunchMinimumLockDuration = 0.15f;
    [SerializeField, Min(0.1f)] private float externalLaunchMaximumAirborneDuration = 2.5f;
    [SerializeField, Min(0.01f)] private float externalLaunchGroundProbeDistance = 0.2f;
    [SerializeField, Min(1)] private int externalLaunchLandingConfirmFrames = 2;
    [SerializeField, Min(0f)] private float externalLaunchGroundSkin = 0.02f;
    [SerializeField, Min(0.01f)] private float externalLaunchStartGroundCorrectionMax = 0.15f;
    [SerializeField, Min(0f)] private float externalLaunchStartGroundSkin = 0.03f;
    [SerializeField] private bool enableExternalLaunchRiseSafety = true;
    [SerializeField, Min(0f)] private float externalLaunchRiseSafetyImpulse = 7f;
    [SerializeField, Min(0f)] private float externalLaunchMinimumAcceptedUpwardVelocity = 1f;
    [SerializeField, Min(0f)] private float externalLaunchMinimumAcceptedRiseDistance = 0.02f;
    [SerializeField, Min(1)] private int externalLaunchRiseVerificationSteps = 2;
    [SerializeField, Min(0.01f)] private float externalLaunchMinorGroundPenetrationMaxCorrection = 0.1f;
    [SerializeField, Min(0.1f)] private float externalLaunchFallRecoveryDistance = 6f;
    [SerializeField] private CollisionDetectionMode externalLaunchCollisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
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
    private float externalLaunchStartPositionY;
    private int externalLaunchRiseVerificationStep;
    private bool externalLaunchRiseSafetyApplied;
    private Vector3 lastValidGroundedPosition;
    private bool hasLastValidGroundedPosition;
    private ExternalLaunchPhase externalLaunchPhase = ExternalLaunchPhase.None;

    public float RawResolvedMoveSpeed { get; private set; }
    public float ActualMoveSpeed { get; private set; }
    public float ExcessMoveSpeed { get; private set; }
    public float ExcessMoveSpeedDamageBonus => ExcessMoveSpeed * BattleStatUtility.PlayerExcessMoveSpeedDamageBonusPerPoint;
    public float SpeedStatMoveRatio => Mathf.Max(0f, speedStatMoveRatio);
    public float MaxActualMoveSpeed => Mathf.Max(0f, maxActualMoveSpeed);
    public bool IsExternalLaunchActive => isUnderExternalLaunch;
    public Rigidbody ExternalLaunchBody => rb;
    public ExternalLaunchPhase CurrentExternalLaunchPhase => externalLaunchPhase;
    public Component LaunchOwnerComponent => this;

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

        Player2PrototypeController player2PrototypeController = GetComponent<Player2PrototypeController>();
        if (player2PrototypeController != null &&
            player2PrototypeController.enabled &&
            player2PrototypeController.IsUnderExternalLaunchActive)
        {
            if (debugExternalLaunchMotion)
            {
                Debug.Log(
                    "[PlayerControllerOwnershipTrace] " +
                    "player=" + name +
                    " owner=Player2PrototypeController" +
                    " ignoredController=PlayerMovement" +
                    " reason=SiblingExternalLaunchActive" +
                    " frame=" + Time.frameCount,
                    this);
            }

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
        Player2PrototypeController player2PrototypeController = GetComponent<Player2PrototypeController>();
        if (player2PrototypeController != null && player2PrototypeController.enabled)
        {
            Debug.Log(
                "[PlayerControllerOwnershipTrace] " +
                "player=" + name +
                " owner=Player2PrototypeController" +
                " redirectedFrom=PlayerMovement.ApplyExternalLaunch" +
                " frame=" + Time.frameCount +
                " launchSequenceId=" + launchSequenceId +
                " launchVelocity=" + launchVelocity,
                this);
            player2PrototypeController.ApplyExternalLaunch(launchVelocity, lockDuration, separationOffset, launchSequenceId);
            return;
        }

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

        externalLaunchCollisionDetectionModeBefore = rb.collisionDetectionMode;
        externalLaunchInterpolationBefore = rb.interpolation;

        Vector3 groundedSafeSeparationOffset = ResolveGroundSafeSeparationOffset(separationOffset, launchVelocity);
        if (groundedSafeSeparationOffset.sqrMagnitude > 0.0001f)
        {
            Vector3 newPosition = rb.position + groundedSafeSeparationOffset;
            rb.position = newPosition;
            transform.position = newPosition;
            Physics.SyncTransforms();
        }

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.constraints &= ~RigidbodyConstraints.FreezePositionY;
        movementInputLocked = true;
        isUnderExternalLaunch = true;
        externalLaunchPhase = ExternalLaunchPhase.Rising;
        externalLaunchStartFrame = Time.frameCount;
        externalLaunchReceivedThisFrame = true;
        externalLaunchGroundedFrameCount = 0;
        previousExternalLaunchVelocityY = launchVelocity.y;
        previousExternalLaunchRootY = transform.position.y;
        previousExternalLaunchRbY = rb.position.y;
        externalLaunchStartPositionY = transform.position.y;
        externalLaunchRiseVerificationStep = 0;
        externalLaunchRiseSafetyApplied = false;
        lastExternalLaunchSequenceId = launchSequenceId;
        lastExternalLaunchReceivedTime = Time.time;
        Vector3 velocityBefore = rb.linearVelocity;
        rb.collisionDetectionMode = externalLaunchCollisionDetectionMode;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        bool launchStartGroundCorrectionApplied = TryApplyExternalLaunchStartGroundCorrection(
            out float launchStartGroundY,
            out float launchStartColliderBottomY,
            out float launchStartPenetrationDepth,
            out float launchStartCorrectionApplied,
            out Collider launchStartGroundCollider);
        rb.linearVelocity = launchVelocity;
        rb.angularVelocity = Vector3.zero;
        rb.WakeUp();
        LogPhysicalBodyTrace("LaunchApplied", launchStartGroundY, launchStartColliderBottomY);
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
            " collisionDetectionMode=" + rb.collisionDetectionMode +
            " interpolation=" + rb.interpolation +
            " launchStartGroundCorrectionApplied=" + launchStartGroundCorrectionApplied +
            " launchStartGroundY=" + launchStartGroundY.ToString("F3") +
            " launchStartColliderBottomY=" + launchStartColliderBottomY.ToString("F3") +
            " launchStartPenetrationDepth=" + launchStartPenetrationDepth.ToString("F3") +
            " launchStartCorrectionAppliedDistance=" + launchStartCorrectionApplied.ToString("F3") +
            " launchStartGroundCollider=" + (launchStartGroundCollider != null ? launchStartGroundCollider.name : "None") +
            " sequenceId=" + launchSequenceId,
            this);
        LogGroundFallTrace("LaunchStarted", "ApplyExternalLaunch");
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
        bool confirmedNaturalGroundCollision = false;
        while (Time.time - startTime < Mathf.Max(0.1f, externalLaunchMaximumAirborneDuration))
        {
            bool grounded = IsGroundedForExternalLaunch();
            Vector3 velocity = rb != null ? rb.linearVelocity : Vector3.zero;
            if (externalLaunchPhase == ExternalLaunchPhase.Rising && velocity.y <= 0f)
            {
                externalLaunchPhase = ExternalLaunchPhase.Falling;
            }
            if (grounded && velocity.y <= 0.05f)
            {
                if (externalLaunchPhase != ExternalLaunchPhase.Falling)
                {
                    yield return new WaitForFixedUpdate();
                    continue;
                }

                externalLaunchPhase = ExternalLaunchPhase.LandingConfirm;
                externalLaunchGroundedFrameCount++;
                if (externalLaunchGroundedFrameCount >= Mathf.Max(1, externalLaunchLandingConfirmFrames))
                {
                    confirmedNaturalGroundCollision = true;
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
        if (confirmedNaturalGroundCollision)
        {
            CompleteNaturalExternalLaunchLanding("RoutineComplete");
            yield break;
        }

        if (ShouldTriggerFallRecovery())
        {
            ResolveExternalLaunchFallRecovery("RoutineTimeout");
            yield break;
        }

        FinishExternalLaunchState("RoutineTimeout");
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
        externalLaunchPhase = ExternalLaunchPhase.None;
        externalLaunchRiseVerificationStep = 0;
        externalLaunchRiseSafetyApplied = false;
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
        if (externalLaunchPhase == ExternalLaunchPhase.Rising && currentVelocity.y <= 0f)
        {
            externalLaunchPhase = ExternalLaunchPhase.Falling;
        }

        if (grounded && externalLaunchPhase == ExternalLaunchPhase.Falling)
        {
            externalLaunchPhase = ExternalLaunchPhase.LandingConfirm;
        }

        if (debugPlayerAirborneLanding)
        {
            LogGroundFallTrace("AirborneStep", source);
        }

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

        TraceExternalLaunchRise(currentVelocity, grounded);

        if (TryResolvePassedGround(previousRbPosition, rb.position, currentVelocity, out RaycastHit landingHit, out float bottomOffset))
        {
            LogGroundFallTrace("GroundApproach", source, landingHit.point.y, bottomOffset, landingHit.collider, landingHit.normal);
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
            LogGroundFallTrace("GroundPenetrationDetected", source);
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

    private void TraceExternalLaunchRise(Vector3 currentVelocity, bool grounded)
    {
        if (!isUnderExternalLaunch || rb == null)
        {
            return;
        }

        externalLaunchRiseVerificationStep++;

        bool hasGroundY = TryResolveGroundYAt(rb.position, out float groundY);
        bool hasBottomOffset = TryResolveMainColliderBottomOffset(out float bottomOffset);
        float colliderBottomY = hasBottomOffset ? rb.position.y - bottomOffset : float.NaN;
        float deltaY = transform.position.y - externalLaunchStartPositionY;

        if (debugExternalLaunchMotion)
        {
            Debug.Log(
                "[PlayerLaunchRiseTrace] " +
                "event=RiseTrace" +
                " controller=PlayerMovement" +
                " step=" + externalLaunchRiseVerificationStep +
                " startY=" + externalLaunchStartPositionY.ToString("F3") +
                " currentY=" + transform.position.y.ToString("F3") +
                " deltaY=" + deltaY.ToString("F3") +
                " velocityY=" + currentVelocity.y.ToString("F3") +
                " constraints=" + rb.constraints +
                " useGravity=" + rb.useGravity +
                " isKinematic=" + rb.isKinematic +
                " grounded=" + grounded +
                " solidColliderBottomY=" + (float.IsNaN(colliderBottomY) ? "NaN" : colliderBottomY.ToString("F3")) +
                " groundY=" + (hasGroundY ? groundY.ToString("F3") : "NaN") +
                " riseSafetyApplied=" + externalLaunchRiseSafetyApplied,
                this);
        }

        if (!ExternalLaunchSafetyUtility.ShouldApplyRiseSafety(
                enableExternalLaunchRiseSafety,
                externalLaunchRiseSafetyApplied,
                externalLaunchRiseVerificationStep,
                externalLaunchRiseVerificationSteps,
                deltaY,
                externalLaunchMinimumAcceptedRiseDistance,
                currentVelocity.y,
                externalLaunchMinimumAcceptedUpwardVelocity))
        {
            return;
        }

        Vector3 correctedVelocity = rb.linearVelocity;
        float velocityBeforeY = correctedVelocity.y;
        correctedVelocity.y = Mathf.Max(correctedVelocity.y, Mathf.Max(0f, externalLaunchRiseSafetyImpulse));
        rb.constraints &= ~RigidbodyConstraints.FreezePositionY;
        rb.useGravity = true;
        rb.isKinematic = false;

        if (hasGroundY && hasBottomOffset)
        {
            float penetrationDepth = groundY - colliderBottomY;
            if (ExternalLaunchSafetyUtility.TryComputeLaunchStartGroundCorrection(
                    penetrationDepth,
                    externalLaunchStartGroundCorrectionMax,
                    externalLaunchStartGroundSkin,
                    out float upwardCorrection,
                    out _)
                && upwardCorrection > 0f)
            {
                Vector3 correctedPosition = rb.position + Vector3.up * upwardCorrection;
                rb.position = correctedPosition;
                transform.position = correctedPosition;
                Physics.SyncTransforms();
            }
        }

        rb.linearVelocity = correctedVelocity;
        rb.angularVelocity = Vector3.zero;
        rb.WakeUp();
        externalLaunchRiseSafetyApplied = true;

        if (debugExternalLaunchMotion)
        {
            Debug.Log(
                "[PlayerLaunchRiseTrace] " +
                "event=RiseSafetyApplied" +
                " controller=PlayerMovement" +
                " step=" + externalLaunchRiseVerificationStep +
                " positionY=" + transform.position.y.ToString("F3") +
                " velocityBeforeY=" + velocityBeforeY.ToString("F3") +
                " velocityAfterY=" + correctedVelocity.y.ToString("F3") +
                " constraints=" + rb.constraints +
                " useGravity=" + rb.useGravity +
                " isKinematic=" + rb.isKinematic +
                " reason=NoUpwardDisplacement",
                this);
        }
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
        if (rb == null)
        {
            return false;
        }

        if (!ExternalLaunchSafetyUtility.TryResolvePhysicalBodyCollider(this, rb, out Collider solidCollider))
        {
            return false;
        }

        bottomOffset = ExternalLaunchSafetyUtility.ResolveBottomOffset(rb, solidCollider);
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
            Vector3 safePosition = new Vector3(
                rb.position.x,
                groundY + bottomOffset + Mathf.Max(0f, externalLaunchGroundSkin),
                rb.position.z);
            LogGroundFallTrace("EmergencyFallRecovery", reason, groundY, bottomOffset, null, Vector3.up, safePosition);
            MoveExternalLaunchRoot(safePosition, reason, null, Vector3.up);
            FinishExternalLaunchState(reason);
            return;
        }

        if (hasLastValidGroundedPosition)
        {
            LogGroundFallTrace("EmergencyFallRecovery", reason, float.NaN, float.NaN, null, Vector3.up, lastValidGroundedPosition);
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
        LogGroundFallTrace("LandingResolve", reason, groundY, bottomOffset, groundCollider, groundNormal);
        MoveExternalLaunchRoot(safePosition, reason, groundCollider, groundNormal);
        FinishExternalLaunchState(reason);
    }

    private void CompleteNaturalExternalLaunchLanding(string reason)
    {
        if (rb == null)
        {
            return;
        }

        if (TryResolveMinorGroundPenetration(out float groundY, out float bottomOffset, out Collider groundCollider, out float correctionDistance))
        {
            float safeY = groundY + bottomOffset + Mathf.Max(0f, externalLaunchGroundSkin);
            Vector3 safePosition = new Vector3(rb.position.x, safeY, rb.position.z);
            LogGroundFallTrace("MinorGroundPenetrationCorrection", reason, groundY, bottomOffset, groundCollider, Vector3.up, safePosition, correctionDistance);
            MoveExternalLaunchRoot(safePosition, reason, groundCollider, Vector3.up);
            FinishExternalLaunchState(reason);
            return;
        }

        LogGroundFallTrace("NaturalGroundCollision", reason);
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
        externalLaunchPhase = ExternalLaunchPhase.None;
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

            rb.collisionDetectionMode = externalLaunchCollisionDetectionModeBefore;
            rb.interpolation = externalLaunchInterpolationBefore;
        }

        if (debugPlayerAirborneLanding)
        {
            Debug.Log(
                "[PlayerAirborneTrace] event=FinishAirborneState " +
                "controller=PlayerMovement" +
                "reason=" + reason +
                " position=" + transform.position +
                " velocity=" + (rb != null ? rb.linearVelocity.ToString() : "None") +
                " collisionDetectionMode=" + (rb != null ? rb.collisionDetectionMode.ToString() : "None"),
                this);
        }
    }

    private bool TryResolveMinorGroundPenetration(
        out float groundY,
        out float bottomOffset,
        out Collider groundCollider,
        out float correctionDistance)
    {
        groundY = float.NaN;
        bottomOffset = float.NaN;
        groundCollider = null;
        correctionDistance = 0f;

        if (rb == null ||
            !TryResolveGroundedSupport(out groundY, out groundCollider) ||
            !TryResolveMainColliderBottomOffset(out bottomOffset))
        {
            return false;
        }

        float currentBottomY = rb.position.y - bottomOffset;
        correctionDistance = groundY - currentBottomY;
        float minimumCorrection = Mathf.Max(0.005f, externalLaunchGroundSkin * 0.5f);
        return correctionDistance > minimumCorrection &&
               correctionDistance <= Mathf.Max(minimumCorrection, externalLaunchMinorGroundPenetrationMaxCorrection);
    }

    private bool TryApplyExternalLaunchStartGroundCorrection(
        out float groundY,
        out float colliderBottomY,
        out float penetrationDepth,
        out float correctionApplied,
        out Collider groundCollider)
    {
        groundY = float.NaN;
        colliderBottomY = float.NaN;
        penetrationDepth = 0f;
        correctionApplied = 0f;
        groundCollider = null;

        if (rb == null ||
            !TryResolveGroundedSupport(out groundY, out groundCollider) ||
            !TryResolveMainColliderBottomOffset(out float bottomOffset))
        {
            return false;
        }

        colliderBottomY = rb.position.y - bottomOffset;
        penetrationDepth = groundY - colliderBottomY;
        if (!ExternalLaunchSafetyUtility.TryComputeLaunchStartGroundCorrection(
                penetrationDepth,
                externalLaunchStartGroundCorrectionMax,
                externalLaunchStartGroundSkin,
                out correctionApplied,
                out bool severePenetration))
        {
            if (severePenetration && debugExternalLaunchMotion)
            {
                Debug.LogWarning(
                    "[PlayerLaunchSetupTrace] " +
                    "event=SevereLaunchStartPenetration" +
                    " controller=PlayerMovement" +
                    " playerBottomY=" + colliderBottomY.ToString("F3") +
                    " groundY=" + groundY.ToString("F3") +
                    " penetrationDepth=" + penetrationDepth.ToString("F3") +
                    " correctionSkipped=true" +
                    " maxCorrection=" + Mathf.Max(0f, externalLaunchStartGroundCorrectionMax).ToString("F3") +
                    " solidCollider=" + ResolvePrimarySolidColliderName() +
                    " groundCollider=" + (groundCollider != null ? groundCollider.name : "None"),
                    this);
            }

            return false;
        }

        Vector3 correctedPosition = rb.position + Vector3.up * correctionApplied;
        rb.position = correctedPosition;
        transform.position = correctedPosition;
        Physics.SyncTransforms();
        colliderBottomY += correctionApplied;

        if (debugExternalLaunchMotion)
        {
            Debug.Log(
                "[PlayerLaunchSetupTrace] " +
                "event=LaunchStartGroundOverlap" +
                " controller=PlayerMovement" +
                " playerBottomY=" + (colliderBottomY - correctionApplied).ToString("F3") +
                " groundY=" + groundY.ToString("F3") +
                " penetrationDepth=" + penetrationDepth.ToString("F3") +
                " correctionApplied=" + correctionApplied.ToString("F3") +
                " solidCollider=" + ResolvePrimarySolidColliderName() +
                " groundCollider=" + (groundCollider != null ? groundCollider.name : "None"),
                this);
        }

        return true;
    }

    private string ResolvePrimarySolidColliderName()
    {
        if (!ExternalLaunchSafetyUtility.TryResolvePhysicalBodyCollider(this, rb, out Collider solidCollider))
        {
            return "None";
        }

        return solidCollider.name;
    }

    private void LogPhysicalBodyTrace(string reason, float resolvedGroundY, float colliderBottomY)
    {
        if (!debugExternalLaunchMotion || rb == null)
        {
            return;
        }

        if (!ExternalLaunchSafetyUtility.TryResolvePhysicalBodyCollider(this, rb, out Collider solidCollider))
        {
            Debug.LogError(
                "[PlayerPhysicalBodyTrace] " +
                "controller=PlayerMovement" +
                " player=" + name +
                " body=" + rb.name +
                " solidCollider=None" +
                " reason=" + reason,
                this);
            return;
        }

        Debug.Log(
            "[PlayerPhysicalBodyTrace] " +
            "controller=PlayerMovement" +
            " player=" + name +
            " body=" + rb.name +
            " solidCollider=" + solidCollider.name +
            " colliderType=" + solidCollider.GetType().Name +
            " enabled=" + solidCollider.enabled +
            " isTrigger=" + solidCollider.isTrigger +
            " attachedRigidbody=" + (solidCollider.attachedRigidbody != null ? solidCollider.attachedRigidbody.name : "None") +
            " sameRigidbody=" + (solidCollider.attachedRigidbody == rb) +
            " rootPositionY=" + transform.position.y.ToString("F3") +
            " colliderBottomY=" + (float.IsNaN(colliderBottomY) ? solidCollider.bounds.min.y.ToString("F3") : colliderBottomY.ToString("F3")) +
            " groundY=" + (float.IsNaN(resolvedGroundY) ? "NaN" : resolvedGroundY.ToString("F3")) +
            " reason=" + reason,
            this);
    }

    private void LogGroundFallTrace(
        string eventName,
        string source,
        float resolvedGroundY = float.NaN,
        float bottomOffset = float.NaN,
        Collider groundCollider = null,
        Vector3? groundNormal = null,
        Vector3? safePosition = null,
        float correctionDistance = float.NaN)
    {
        if (!debugPlayerAirborneLanding || rb == null)
        {
            return;
        }

        bool hasGround = TryResolveGroundedSupport(out float probeGroundY, out Collider probeGroundCollider);
        bool hasBottomOffset = TryResolveMainColliderBottomOffset(out float currentBottomOffset);
        float playerBottomY = hasBottomOffset ? rb.position.y - currentBottomOffset : float.NaN;
        string groundName = groundCollider != null
            ? groundCollider.name
            : (probeGroundCollider != null ? probeGroundCollider.name : "None");
        int groundLayer = groundCollider != null
            ? groundCollider.gameObject.layer
            : (probeGroundCollider != null ? probeGroundCollider.gameObject.layer : -1);

        Debug.Log(
            "[PlayerGroundFallTrace] " +
            "event=" + eventName +
            " controller=PlayerMovement" +
            " source=" + source +
            " frame=" + Time.frameCount +
            " position=" + transform.position +
            " rigidbodyPosition=" + rb.position +
            " velocity=" + rb.linearVelocity +
            " isKinematic=" + rb.isKinematic +
            " useGravity=" + rb.useGravity +
            " constraints=" + rb.constraints +
            " collisionDetectionMode=" + rb.collisionDetectionMode +
            " interpolation=" + rb.interpolation +
            " grounded=" + hasGround +
            " playerBottomY=" + (float.IsNaN(playerBottomY) ? "NaN" : playerBottomY.ToString("F3")) +
            " probeGroundY=" + (hasGround ? probeGroundY.ToString("F3") : "NaN") +
            " resolvedGroundY=" + (float.IsNaN(resolvedGroundY) ? "NaN" : resolvedGroundY.ToString("F3")) +
            " bottomOffset=" + (float.IsNaN(bottomOffset) ? (hasBottomOffset ? currentBottomOffset.ToString("F3") : "NaN") : bottomOffset.ToString("F3")) +
            " penetrationDepth=" + ((hasGround && !float.IsNaN(playerBottomY)) ? (probeGroundY - playerBottomY).ToString("F3") : "NaN") +
            " correctionDistance=" + (float.IsNaN(correctionDistance) ? "NaN" : correctionDistance.ToString("F3")) +
            " safePosition=" + (safePosition.HasValue ? safePosition.Value.ToString() : "None") +
            " groundCollider=" + groundName +
            " groundLayer=" + (groundLayer >= 0 ? LayerMask.LayerToName(groundLayer) : "None") +
            " groundNormal=" + (groundNormal.HasValue ? groundNormal.Value.ToString() : Vector3.up.ToString()),
            this);
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
