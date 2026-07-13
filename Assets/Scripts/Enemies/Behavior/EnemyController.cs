using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private const float MovementZeroEpsilon = 0.001f;
    private const float AttackRecoveryDurationSeconds = 0.7f;
    private const float ProjectileSpawnHeightOffset = 0.8f;
    private const float ProjectileSpawnForwardOffset = 0.8f;
    private const float BossProjectileScale = 0.45f;
    private const float NormalProjectileScale = 0.28f;
    private const float EdgeDistanceEpsilon = 0.02f;
    private const string DefaultProjectilePrefabResourcePath = "Prefabs/Enemy/BossProjectile";

    [Header("Target")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private string playerTag = "Player";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float stopDistance = 0.8f;
    [SerializeField] private float maxHorizontalMoveSpeed = 3f;
    [SerializeField] private bool faceMoveDirection = false;
    [SerializeField] private bool keepFlatRotation = true;
    [SerializeField] private bool enableEnemySoftAvoidance = true;
    [SerializeField] private float enemySeparationRadius = 1.2f;
    [SerializeField] private float enemySeparationWeight = 0.6f;
    [SerializeField] private int enemySeparationMaxNeighbors = 8;
    [SerializeField] private float maxVerticalTargetDifference = 1.5f;
    [SerializeField] private float maxVerticalVelocity = 4f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.35f;
    [SerializeField] private float attackHitRange = 1.6f;
    [SerializeField] private float attackCooldown = 1.1f;
    [SerializeField] private float attackDamage = 3f;
    [SerializeField] private MonsterAttackStyle attackStyle = MonsterAttackStyle.Melee;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float attackIntervalMultiplier = 1f;
    [SerializeField] private float outgoingDamageMultiplier = 1f;
    [SerializeField] private float meleeHitAngle = 100f;
    [SerializeField] private float meleeHitForwardOffset = 0f;
    [SerializeField] private float closeHitRadius = 0f;
    [SerializeField] private float meleeBodyContactRadius = 0.45f;
    [SerializeField] private bool requireGroundedToAttack = true;
    [SerializeField] private float groundedProbeDistance = 0.2f;
    [SerializeField] private float maxVerticalAttackDifference = 0.75f;
    [SerializeField] private float maxHorizontalAttackDistance = 1.35f;
    [SerializeField, Min(0f)] private float groundedAttackGraceTime = 0f;
    [SerializeField] private float normalMeleeHitRadius = 0.8f;
    [SerializeField] private float eliteMeleeHitRadius = 1.2f;
    [SerializeField] private float normalMeleeHitForwardOffset = 0.35f;
    [SerializeField] private float eliteMeleeHitForwardOffset = 0.5f;
    [SerializeField] private float normalMeleeHitHeight = 0.5f;
    [SerializeField] private float eliteMeleeHitHeight = 0.6f;
    [SerializeField] private float bossMeleeAttackRange = 2.0f;
    [SerializeField] private float bossRangedMinRange = 3.0f;
    [SerializeField] private float bossRangedMaxRange = 10.0f;
    [SerializeField] private float bossRangedAttackCooldown = 3.0f;
    [SerializeField] private float bossRangedCastTime = 0.6f;
    [SerializeField] private float bossMeleeHitRadius = 1.8f;
    [SerializeField] private float bossMeleeHitForwardOffset = 0.8f;
    [SerializeField] private float bossMeleeHitHeight = 0.8f;
    [SerializeField] private bool enableBossRangedCastAnimation = true;
    [SerializeField] private float bossRangedCastWindupTime = 0.25f;
    [SerializeField] private float bossRangedCastReleaseTime = 0.15f;
    [SerializeField] private float bossRangedVisualRecoverTime = 0.25f;
    [SerializeField] private float bossRangedWindupSquashX = 0.85f;
    [SerializeField] private float bossRangedWindupStretchY = 1.1f;
    [SerializeField] private float bossRangedReleaseStretchX = 1.25f;
    [SerializeField] private float bossRangedReleaseSquashY = 0.85f;
    [SerializeField] private float bossRangedVisualLeanDistance = 0.25f;
    [SerializeField] private float bossProjectileSpawnForwardOffset = 0.8f;
    [SerializeField] private float bossProjectileSpawnHeight = 0.6f;
    [SerializeField] private Transform bossProjectileSpawnPoint;
    [SerializeField] private ParticleSystem bossRangedMuzzleParticle;
    [SerializeField] private bool useArcTrajectory = true;
    [SerializeField] private float arcHeight = 2.0f;
    [SerializeField] private float arcTravelTime = 0.9f;
    [SerializeField] private float targetPredictionTime = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool debugMeleeHitCheck = false;
    [SerializeField] private bool debugAttackDiagnostics = false;
    [SerializeField] private bool debugSpeedDiagnostics = false;
    [SerializeField] private bool debugChaseDiagnostics = true;
    [SerializeField] private bool debugAttackStateTransitions = false;
    [SerializeField] private bool debugSlimeAttackLogs = false;
    [SerializeField] private bool debugBossMeleeHit = true;
    [SerializeField, Min(0.1f)] private float debugAttackLogInterval = 0.3f;
    [SerializeField, Min(0.1f)] private float debugSpeedLogInterval = 1f;
    [SerializeField, Min(0.05f)] private float targetResolveRetryInterval = 0.25f;

    private Rigidbody rb;
    private MonsterIdentity monsterIdentity;
    private Player2Bootstrap playerBootstrap;
    private SlimeAnimationController slimeAnimation;
    private EnemyDebuffReceiver debuffReceiver;
    private CombatStats combatStats;
    private Collider meleeEnemyCollider;
    private SpriteRenderer meleeEnemySpriteRenderer;
    private Quaternion initialRotation;
    private float nextAttackTime;
    private Transform pendingAttackTarget;
    private bool attackInProgress;
    private float lastLoggedMoveMultiplier = -1f;
    private float lastLoggedAttackMultiplier = -1f;
    private float nextAttackDiagnosticTime;
    private float nextSpeedDiagnosticTime;
    private float nextChaseDiagnosticTime;
    private float nextTargetResolveTime;
    private float nextBossRangedDecisionLogTime;
    private float nextBossMeleeDecisionLogTime;
    private float nextEnemyMeleeDecisionLogTime;
    private Vector3 lastGroundProbeOrigin;
    private bool lastGroundProbeHit;
    private string lastGroundHitName = "None";
    private int lastGroundHitLayer = -1;
    private float lastGroundProbeCastDistance;
    private Collider[] separationHits;
    private Collider[] bossMeleeHitResults;
    private static GameObject cachedDefaultProjectilePrefab;
    private float lastAttackTime = -1f;
    private float nextBossRangedAttackTime;
    private float lastGroundedTime = float.NegativeInfinity;
    private EnemyAttackRuntimeState lastLoggedAttackState = EnemyAttackRuntimeState.None;
    private Collider lastLoggedMeleeCollider;
    private bool hasLoggedRuntimeAttackConfig;
    private Coroutine bossRangedAttackRoutine;
    private BossAttackKind activeBossAttackKind = BossAttackKind.None;
    private bool attackHitFrameTriggeredThisAttack;
    private string lastMeleeAttackResult = "none";

    private enum EnemyAttackRuntimeState
    {
        None,
        NoTarget,
        Chase,
        HoldPosition,
        AttackReady,
        AttackInProgress,
        AttackRecovery
    }

    private enum BossAttackKind
    {
        None,
        Melee,
        Ranged
    }

    public float BaseMoveSpeed => moveSpeed;
    public Transform CurrentTarget => playerTarget;
    public MonsterAttackStyle CurrentAttackStyle => attackStyle;

    private void Start()
    {
        MonsterCombatAutoSetup.Configure(gameObject);
        rb = GetComponent<Rigidbody>();
        monsterIdentity = GetComponent<MonsterIdentity>();
        combatStats = GetComponent<CombatStats>();
        slimeAnimation = GetComponent<SlimeAnimationController>();
        ResolveMeleeHitSources();
        initialRotation = transform.rotation;
        ResolvePlayerTarget();

        if (slimeAnimation != null)
        {
            slimeAnimation.OnAttackHit += HandleAttackHit;
        }

        ResolveDebuffReceiver();
    }

    private void OnDestroy()
    {
        if (slimeAnimation != null)
        {
            slimeAnimation.OnAttackHit -= HandleAttackHit;
        }
    }

    private void Update()
    {
        ResolvePlayerTarget();
        if (rb == null || playerTarget == null)
        {
            if (debugAttackDiagnostics && playerTarget == null)
            {
                Debug.Log($"[EnemyAttackDiag] name={name} target=null failReason=NoTarget", this);
            }

            LogChaseDiagnostics(
                -1f,
                false,
                false,
                false,
                false,
                false,
                rb == null ? "NoRigidbody" : "NoTarget",
                0f,
                playerTarget != null ? playerTarget.name : "null",
                rb == null ? "NoRigidbody" : "NoTarget");
            return;
        }

        Vector3 toPlayer = playerTarget.position - transform.position;
        toPlayer.y = 0f;
        float centerDistance = Vector3.Distance(playerTarget.position, transform.position);
        float horizontalCenterDistance = new Vector2(toPlayer.x, toPlayer.z).magnitude;
        float verticalDifference = Mathf.Abs(playerTarget.position.y - transform.position.y);
        float horizontalEdgeDistance = ResolveHorizontalEdgeDistance(playerTarget, out Vector3 enemyClosestPoint, out Vector3 playerClosestPoint);
        float attackDistance = ResolveAttackDistance(horizontalCenterDistance, horizontalEdgeDistance);
        float statsSpeed = combatStats != null ? Mathf.Max(0f, combatStats.speed) : 0f;
        bool grounded = IsGroundedForAttack();
        string attackFailReason = EvaluateAttackFailReason(attackDistance, verticalDifference, grounded);
        bool canAttack = string.IsNullOrEmpty(attackFailReason);
        bool isAttackAnimationActive = attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking);
        bool hasPhysicalContact = horizontalEdgeDistance <= EdgeDistanceEpsilon;
        bool insideStopDistance = horizontalEdgeDistance <= Mathf.Max(0f, stopDistance) + EdgeDistanceEpsilon;
        CombatHealth health = GetComponent<CombatHealth>();
        bool isDead = health != null && health.IsDead;

        if (attackStyle == MonsterAttackStyle.ElementalBoss)
        {
            HandleBossElementalCombat(toPlayer, horizontalCenterDistance, centerDistance, verticalDifference, grounded);
            return;
        }

        LogRuntimeAttackConfigOnce();
        LogAttackDiagnostics(
            statsSpeed,
            centerDistance,
            attackDistance,
            verticalDifference,
            grounded,
            canAttack,
            attackFailReason);
        // Enter attack flow as soon as the target is in range so chase does not keep pushing the player.
        if (canAttack)
        {
            LogEnemyMeleeDecision(attackDistance, true, "None", grounded, "Melee", playerTarget != null ? playerTarget.name : "null", Mathf.Max(0f, nextAttackTime - Time.time), isAttackAnimationActive, false, isDead);
            LogChaseDiagnostics(attackDistance, false, true, false, false, false, "Attack", 0f, playerTarget != null ? playerTarget.name : "null", "InAttackRange");
            LogAttackStateChange(
                EnemyAttackRuntimeState.AttackReady,
                centerDistance,
                attackDistance,
                verticalDifference,
                grounded,
                canAttack,
                "InAttackRange",
                enemyClosestPoint,
                playerClosestPoint);
            BeginAttack();
            return;
        }

        // 攻击动作进行中时，原地停住并保持当前朝向，等待攻击回调结算。
        if (isAttackAnimationActive)
        {
            LogEnemyMeleeDecision(attackDistance, false, "AlreadyAttacking", grounded, "None", playerTarget != null ? playerTarget.name : "null", Mathf.Max(0f, nextAttackTime - Time.time), true, false, isDead);
            rb.linearVelocity = Vector3.zero;
            string chaseReason = string.IsNullOrEmpty(attackFailReason) ? "AttackInProgress" : attackFailReason;
            LogChaseDiagnostics(attackDistance, false, false, false, false, false, "AttackRecovery", 0f, playerTarget != null ? playerTarget.name : "null", chaseReason);
            LogAttackStateChange(
                EnemyAttackRuntimeState.AttackInProgress,
                centerDistance,
                attackDistance,
                verticalDifference,
                grounded,
                false,
                chaseReason,
                enemyClosestPoint,
                playerClosestPoint);
            if (keepFlatRotation)
            {
                transform.rotation = initialRotation;
            }
            return;
        }

        // Hold position inside stop distance to avoid face-hug jitter and constant pushing.
        if (hasPhysicalContact || insideStopDistance || centerDistance < MovementZeroEpsilon)
        {
            LogEnemyMeleeDecision(
                attackDistance,
                false,
                string.IsNullOrEmpty(attackFailReason) ? (hasPhysicalContact ? "PhysicalContact" : "StopDistance") : attackFailReason,
                grounded,
                "Chase",
                playerTarget != null ? playerTarget.name : "null",
                Mathf.Max(0f, nextAttackTime - Time.time),
                false,
                false,
                isDead);
            rb.linearVelocity = Vector3.zero;
            StopMoveAnimation();
            string chaseReason = string.IsNullOrEmpty(attackFailReason)
                ? (hasPhysicalContact ? "PhysicalContact" : "StopDistance")
                : attackFailReason;
            LogChaseDiagnostics(attackDistance, false, false, false, false, false, "HoldPosition", 0f, playerTarget != null ? playerTarget.name : "null", chaseReason);
            LogAttackStateChange(
                EnemyAttackRuntimeState.HoldPosition,
                centerDistance,
                attackDistance,
                verticalDifference,
                grounded,
                false,
                chaseReason,
                enemyClosestPoint,
                playerClosestPoint);
            if (keepFlatRotation)
            {
                transform.rotation = initialRotation;
            }
            return;
        }

        float safeHorizontalCenterDistance = Mathf.Max(horizontalCenterDistance, MovementZeroEpsilon);
        Vector3 direction = toPlayer / safeHorizontalCenterDistance;
        if (enableEnemySoftAvoidance)
        {
            Vector3 separationDirection = ResolveEnemySeparationDirection();
            Vector3 combinedDirection = direction + separationDirection * Mathf.Max(0f, enemySeparationWeight);
            combinedDirection.y = 0f;
            if (combinedDirection.sqrMagnitude > MovementZeroEpsilon * MovementZeroEpsilon)
            {
                direction = combinedDirection.normalized;
            }
        }

        float baseMoveSpeed = moveSpeed;
        float externalMoveMultiplier = ResolveExternalMoveMultiplier();
        float rawMoveSpeed = BattleStatUtility.ResolveMoveSpeed(combatStats, baseMoveSpeed, externalMoveMultiplier);
        float currentMoveSpeed = BattleStatUtility.ClampActualMoveSpeed(rawMoveSpeed, out _);
        if (maxHorizontalMoveSpeed > 0f)
        {
            currentMoveSpeed = Mathf.Min(currentMoveSpeed, maxHorizontalMoveSpeed);
        }
        if (debugLog && Mathf.Abs(externalMoveMultiplier - lastLoggedMoveMultiplier) > 0.001f)
        {
            Debug.Log($"[EnemyController] finalMoveSpeed={currentMoveSpeed:F2} multiplier={externalMoveMultiplier:F2}", this);
            lastLoggedMoveMultiplier = externalMoveMultiplier;
        }

        LogSpeedDiagnostics(statsSpeed, centerDistance, canAttack);
        LogEnemyMeleeDecision(
            attackDistance,
            false,
            string.IsNullOrEmpty(attackFailReason) ? "OutOfRange" : attackFailReason,
            grounded,
            UsesProjectileAttack() ? "Ranged" : "Chase",
            playerTarget != null ? playerTarget.name : "null",
            Mathf.Max(0f, nextAttackTime - Time.time),
            false,
            false,
            isDead);
        LogChaseDiagnostics(
            attackDistance,
            currentMoveSpeed > MovementZeroEpsilon,
            false,
            false,
            false,
            externalMoveMultiplier <= 0f,
            "Chase",
            currentMoveSpeed,
            playerTarget != null ? playerTarget.name : "null",
            string.IsNullOrEmpty(attackFailReason) ? "Chase" : attackFailReason);
        LogAttackStateChange(
            EnemyAttackRuntimeState.Chase,
            centerDistance,
            attackDistance,
            verticalDifference,
            grounded,
            canAttack,
            string.IsNullOrEmpty(attackFailReason) ? "Chase" : attackFailReason,
            enemyClosestPoint,
            playerClosestPoint);

        float verticalVelocity = rb.linearVelocity.y;
        if (maxVerticalVelocity > 0f)
        {
            verticalVelocity = Mathf.Clamp(verticalVelocity, -maxVerticalVelocity, maxVerticalVelocity);
        }

        rb.linearVelocity = new Vector3(direction.x * currentMoveSpeed, verticalVelocity, direction.z * currentMoveSpeed);
        PlayMoveAnimation(direction, currentMoveSpeed);

        if (faceMoveDirection)
        {
            transform.forward = direction;
        }
        else if (keepFlatRotation)
        {
            transform.rotation = initialRotation;
        }
    }

    public void SetTarget(Transform target)
    {
        AssignTarget(target, "Manual");
    }

    public void SetTarget(Transform target, string source)
    {
        AssignTarget(target, source);
    }

    public void ConfigureRuntime(float moveSpeed, float stopDistance, float attackRange, float attackHitRange, float attackCooldown, float attackDamage, MonsterAttackStyle attackStyle, float attackIntervalMultiplier = 1f, float outgoingDamageMultiplier = 1f)
    {
        this.moveSpeed = Mathf.Max(0f, moveSpeed);
        this.stopDistance = Mathf.Max(0f, stopDistance);
        this.attackRange = Mathf.Max(0.1f, attackRange);
        this.attackHitRange = Mathf.Max(0.1f, attackHitRange);
        this.attackCooldown = Mathf.Max(0.1f, attackCooldown);
        this.attackDamage = Mathf.Max(0f, attackDamage);
        this.attackStyle = attackStyle;
        this.attackIntervalMultiplier = Mathf.Max(0.1f, attackIntervalMultiplier);
        this.outgoingDamageMultiplier = Mathf.Max(0.01f, outgoingDamageMultiplier);
    }

    private void HandleBossElementalCombat(Vector3 toPlayer, float horizontalCenterDistance, float centerDistance, float verticalDifference, bool grounded)
    {
        float distance = Mathf.Max(0f, horizontalCenterDistance);
        string targetName = playerTarget != null ? playerTarget.name : "null";
        CombatHealth health = GetComponent<CombatHealth>();
        bool isDead = health != null && health.IsDead;
        bool isStunned = false;
        bool isAttacking = attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking);
        float meleeCooldownRemaining = Mathf.Max(0f, nextAttackTime - Time.time);

        if (attackInProgress)
        {
            rb.linearVelocity = Vector3.zero;
            StopMoveAnimation();
            FaceTargetHorizontally(playerTarget);
            LogBossMeleeDecision(distance, false, "AlreadyAttacking", grounded, "Ranged", targetName, meleeCooldownRemaining, isAttacking, isStunned, isDead);
            LogBossRangedDecision(distance, "Ranged", "CastInProgress");
            return;
        }

        bool inMeleeRange = distance <= Mathf.Max(0.1f, bossMeleeAttackRange);
        bool inRangedRange = distance >= Mathf.Max(0.1f, bossRangedMinRange) && distance <= Mathf.Max(bossRangedMinRange, bossRangedMaxRange);

        if (inMeleeRange)
        {
            string meleeFailReason = EvaluateBossMeleeFailReason(distance, verticalDifference, grounded);
            LogBossMeleeDecision(distance, string.IsNullOrEmpty(meleeFailReason), string.IsNullOrEmpty(meleeFailReason) ? "None" : meleeFailReason, grounded, "Melee", targetName, meleeCooldownRemaining, isAttacking, isStunned, isDead);
            LogBossRangedDecision(distance, string.IsNullOrEmpty(meleeFailReason) ? "Melee" : "Melee", string.IsNullOrEmpty(meleeFailReason) ? "WithinMeleeRange" : meleeFailReason);
            if (string.IsNullOrEmpty(meleeFailReason))
            {
                activeBossAttackKind = BossAttackKind.Melee;
                BeginAttack();
                return;
            }

            rb.linearVelocity = Vector3.zero;
            StopMoveAnimation();
            return;
        }

        if (inRangedRange)
        {
            string rangedFailReason = EvaluateBossRangedFailReason(distance, verticalDifference, grounded);
            LogBossMeleeDecision(distance, false, string.IsNullOrEmpty(rangedFailReason) ? "RangedSelected" : rangedFailReason, grounded, "Ranged", targetName, meleeCooldownRemaining, isAttacking, isStunned, isDead);
            LogBossRangedDecision(distance, string.IsNullOrEmpty(rangedFailReason) ? "Ranged" : "Ranged", string.IsNullOrEmpty(rangedFailReason) ? "WithinRangedWindow" : rangedFailReason);
            if (string.IsNullOrEmpty(rangedFailReason))
            {
                BeginBossRangedAttack(playerTarget);
                return;
            }

            if (rangedFailReason == "Cooldown")
            {
                LogBossMeleeDecision(distance, false, "RangedCooldownChaseToMelee", grounded, "Chase", targetName, meleeCooldownRemaining, isAttacking, isStunned, isDead);
                ChaseTarget(toPlayer, currentState: "BossChase", targetName: targetName, reason: "RangedCooldownChaseToMelee");
                return;
            }

            rb.linearVelocity = Vector3.zero;
            StopMoveAnimation();
            FaceTargetHorizontally(playerTarget);
            return;
        }

        if (distance > Mathf.Max(bossRangedMinRange, bossRangedMaxRange))
        {
            LogBossMeleeDecision(distance, false, "TargetOutsideRangedMax", grounded, "Chase", targetName, meleeCooldownRemaining, isAttacking, isStunned, isDead);
            LogBossRangedDecision(distance, "Chase", "TargetOutsideRangedMax");
            ChaseTarget(toPlayer, currentState: "BossChase", targetName: targetName, reason: "TargetOutsideRangedMax");
            return;
        }

        LogBossMeleeDecision(distance, false, "BetweenMeleeAndRangedWindow", grounded, "Chase", targetName, meleeCooldownRemaining, isAttacking, isStunned, isDead);
        LogBossRangedDecision(distance, "Chase", "BetweenMeleeAndRangedWindow");
        ChaseTarget(toPlayer, currentState: "BossChase", targetName: targetName, reason: "BetweenMeleeAndRangedWindow");
    }

    private void ChaseTarget(Vector3 toPlayer, string currentState, string targetName, string reason)
    {
        if (rb == null)
        {
            return;
        }

        float horizontalCenterDistance = new Vector2(toPlayer.x, toPlayer.z).magnitude;
        if (horizontalCenterDistance <= MovementZeroEpsilon)
        {
            rb.linearVelocity = Vector3.zero;
            StopMoveAnimation();
            return;
        }

        Vector3 direction = toPlayer / horizontalCenterDistance;
        if (enableEnemySoftAvoidance)
        {
            Vector3 separationDirection = ResolveEnemySeparationDirection();
            Vector3 combinedDirection = direction + separationDirection * Mathf.Max(0f, enemySeparationWeight);
            combinedDirection.y = 0f;
            if (combinedDirection.sqrMagnitude > MovementZeroEpsilon * MovementZeroEpsilon)
            {
                direction = combinedDirection.normalized;
            }
        }

        float baseMoveSpeed = moveSpeed;
        float externalMoveMultiplier = ResolveExternalMoveMultiplier();
        float rawMoveSpeed = BattleStatUtility.ResolveMoveSpeed(combatStats, baseMoveSpeed, externalMoveMultiplier);
        float currentMoveSpeed = BattleStatUtility.ClampActualMoveSpeed(rawMoveSpeed, out _);
        if (maxHorizontalMoveSpeed > 0f)
        {
            currentMoveSpeed = Mathf.Min(currentMoveSpeed, maxHorizontalMoveSpeed);
        }

        float verticalVelocity = rb.linearVelocity.y;
        if (maxVerticalVelocity > 0f)
        {
            verticalVelocity = Mathf.Clamp(verticalVelocity, -maxVerticalVelocity, maxVerticalVelocity);
        }

        rb.linearVelocity = new Vector3(direction.x * currentMoveSpeed, verticalVelocity, direction.z * currentMoveSpeed);
        PlayMoveAnimation(direction, currentMoveSpeed);
        LogChaseDiagnostics(horizontalCenterDistance, currentMoveSpeed > MovementZeroEpsilon, false, false, false, externalMoveMultiplier <= 0f, currentState, currentMoveSpeed, targetName, reason);

        if (faceMoveDirection)
        {
            transform.forward = direction;
        }
        else if (keepFlatRotation)
        {
            transform.rotation = initialRotation;
        }
    }

    private string EvaluateBossMeleeFailReason(float distance, float verticalDifference, bool grounded)
    {
        if (playerTarget == null)
        {
            return "NoTarget";
        }

        if (attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking))
        {
            return "AlreadyAttacking";
        }

        if (Time.time < nextAttackTime)
        {
            return "Cooldown";
        }

        if (distance > Mathf.Max(0.1f, bossMeleeAttackRange))
        {
            return "OutOfRange";
        }

        if (verticalDifference > Mathf.Max(0f, maxVerticalAttackDifference))
        {
            return "OutOfRange";
        }

        if (requireGroundedToAttack && !grounded)
        {
            return "NotGrounded";
        }

        return string.Empty;
    }

    private string EvaluateBossRangedFailReason(float distance, float verticalDifference, bool grounded)
    {
        if (playerTarget == null)
        {
            return "NoTarget";
        }

        if (attackInProgress)
        {
            return "AlreadyAttacking";
        }

        if (Time.time < nextBossRangedAttackTime)
        {
            return "Cooldown";
        }

        if (distance < Mathf.Max(0.1f, bossRangedMinRange) || distance > Mathf.Max(bossRangedMinRange, bossRangedMaxRange))
        {
            return "OutOfRange";
        }

        if (verticalDifference > Mathf.Max(0f, maxVerticalTargetDifference))
        {
            return "OutOfRange";
        }

        if (requireGroundedToAttack && !grounded)
        {
            return "NotGrounded";
        }

        return string.Empty;
    }

    private void BeginBossRangedAttack(Transform target)
    {
        if (target == null || bossRangedAttackRoutine != null)
        {
            return;
        }

        activeBossAttackKind = BossAttackKind.Ranged;
        attackInProgress = true;
        pendingAttackTarget = target;
        lastAttackTime = Time.time;
        nextBossRangedAttackTime = Time.time + Mathf.Max(0.1f, bossRangedAttackCooldown);
        nextAttackTime = nextBossRangedAttackTime;
        rb.linearVelocity = Vector3.zero;
        StopMoveAnimation();
        CancelInvoke(nameof(FinishAttackRecovery));
        bossRangedAttackRoutine = StartCoroutine(BossRangedAttackRoutine(target));
    }

    private System.Collections.IEnumerator BossRangedAttackRoutine(Transform target)
    {
        float configuredTotal = Mathf.Max(0.05f, bossRangedCastTime);
        float windupDuration = Mathf.Max(0.01f, bossRangedCastWindupTime);
        float releaseDuration = Mathf.Max(0.01f, bossRangedCastReleaseTime);
        float recoverDuration = Mathf.Max(0.01f, bossRangedVisualRecoverTime);
        float durationSum = windupDuration + releaseDuration + recoverDuration;
        if (durationSum > configuredTotal && configuredTotal > 0.01f)
        {
            float scale = configuredTotal / durationSum;
            windupDuration *= scale;
            releaseDuration *= scale;
            recoverDuration *= scale;
        }

        Vector3 baseScale = slimeAnimation != null ? slimeAnimation.BaseVisualLocalScale : Vector3.one;
        Vector3 basePosition = slimeAnimation != null ? slimeAnimation.BaseVisualLocalPosition : Vector3.zero;
        Transform visual = slimeAnimation != null ? slimeAnimation.VisualRoot : null;
        Quaternion rootInitialRotation = transform.rotation;
        Quaternion visualInitialLocalRotation = visual != null ? visual.localRotation : Quaternion.identity;

        Vector3 targetPoint = ResolveBossProjectileTargetPoint(target);
        Vector3 spawnPosition = ResolveProjectileSpawnPosition(target);
        int facingSign = ResolveBossFacingSign(target);
        string targetSide = facingSign >= 0 ? "Right" : "Left";
        if (debugAttackDiagnostics || debugLog)
        {
            Debug.Log($"[BossRangedCast] start cast target position={targetPoint} cast time={configuredTotal:F2} projectile prefab={(ResolveProjectilePrefab() != null ? ResolveProjectilePrefab().name : "runtime sphere")} spawn position={spawnPosition}", this);
            Debug.Log($"[BossRangedCastAnim] start windup visualRoot={(visual != null ? visual.name : "null")} spawnPosition={spawnPosition} targetPosition={targetPoint}", this);
            Debug.Log($"[BossRangedCastAnim] facingSign={facingSign} targetSide={targetSide} rootRotationPreserved=true visualRotationPreserved=true usedFlipOnly=true", this);
        }

        Vector3 windupOffset = new Vector3(-facingSign * bossRangedVisualLeanDistance, 0f, 0f);
        Vector3 releaseOffset = new Vector3(facingSign * bossRangedVisualLeanDistance, 0f, 0f);

        for (float elapsed = 0f; elapsed < windupDuration; elapsed += Time.deltaTime)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }

            FaceTargetHorizontally(target);
            transform.rotation = rootInitialRotation;

            if (enableBossRangedCastAnimation && visual != null)
            {
                float t = Mathf.Clamp01(elapsed / windupDuration);
                visual.localScale = Vector3.Lerp(baseScale, new Vector3(baseScale.x * bossRangedWindupSquashX, baseScale.y * bossRangedWindupStretchY, baseScale.z), t);
                visual.localPosition = Vector3.Lerp(basePosition, basePosition + windupOffset, t);
                visual.localRotation = visualInitialLocalRotation;
            }

            yield return null;
        }

        if (enableBossRangedCastAnimation && visual != null)
        {
            visual.localScale = new Vector3(baseScale.x * bossRangedWindupSquashX, baseScale.y * bossRangedWindupStretchY, baseScale.z);
            visual.localPosition = basePosition + windupOffset;
            visual.localRotation = visualInitialLocalRotation;
        }

        if (debugAttackDiagnostics || debugLog)
        {
            Debug.Log($"[BossRangedCastAnim] release projectile visualRoot={(visual != null ? visual.name : "null")} spawnPosition={spawnPosition} targetPosition={targetPoint}", this);
        }

        if (enableBossRangedCastAnimation && visual != null)
        {
            for (float elapsed = 0f; elapsed < releaseDuration; elapsed += Time.deltaTime)
            {
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                }

                FaceTargetHorizontally(target);
                transform.rotation = rootInitialRotation;
                float t = Mathf.Clamp01(elapsed / releaseDuration);
                visual.localScale = Vector3.Lerp(
                    new Vector3(baseScale.x * bossRangedWindupSquashX, baseScale.y * bossRangedWindupStretchY, baseScale.z),
                    new Vector3(baseScale.x * bossRangedReleaseStretchX, baseScale.y * bossRangedReleaseSquashY, baseScale.z),
                    t);
                visual.localPosition = Vector3.Lerp(basePosition + windupOffset, basePosition + releaseOffset, t);
                visual.localRotation = visualInitialLocalRotation;
                yield return null;
            }
        }

        PlayBossRangedMuzzleParticle();
        ExecuteProjectileAttack(target);

        if (enableBossRangedCastAnimation && visual != null)
        {
            if (debugAttackDiagnostics || debugLog)
            {
                Debug.Log($"[BossRangedCastAnim] recover visualRoot={(visual != null ? visual.name : "null")} spawnPosition={spawnPosition} targetPosition={targetPoint}", this);
            }

            for (float elapsed = 0f; elapsed < recoverDuration; elapsed += Time.deltaTime)
            {
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                }

                FaceTargetHorizontally(target);
                transform.rotation = rootInitialRotation;
                float t = Mathf.Clamp01(elapsed / recoverDuration);
                visual.localScale = Vector3.Lerp(
                    new Vector3(baseScale.x * bossRangedReleaseStretchX, baseScale.y * bossRangedReleaseSquashY, baseScale.z),
                    baseScale,
                    t);
                visual.localPosition = Vector3.Lerp(basePosition + releaseOffset, basePosition, t);
                visual.localRotation = visualInitialLocalRotation;
                yield return null;
            }
        }

        transform.rotation = rootInitialRotation;
        if (visual != null)
        {
            visual.localScale = baseScale;
            visual.localPosition = basePosition;
            visual.localRotation = visualInitialLocalRotation;
        }

        CompleteBossRangedAttack();
    }

    private void CompleteBossRangedAttack()
    {
        attackInProgress = false;
        pendingAttackTarget = null;
        activeBossAttackKind = BossAttackKind.None;
        bossRangedAttackRoutine = null;
    }

    private void FaceTargetHorizontally(Transform target)
    {
        if (target == null)
        {
            return;
        }

        bool facingRight = target.position.x >= transform.position.x;
        if (slimeAnimation != null)
        {
            slimeAnimation.SetFacingRight(facingRight, "BossRangedCast");
            return;
        }

        ResolveMeleeHitSources();
        if (meleeEnemySpriteRenderer != null)
        {
            meleeEnemySpriteRenderer.flipX = facingRight;
        }
    }

    private void LogBossRangedDecision(float distance, string selectedAttack, string reason)
    {
        if (!(debugAttackDiagnostics || debugLog))
        {
            return;
        }

        if (Time.time < nextBossRangedDecisionLogTime)
        {
            return;
        }

        nextBossRangedDecisionLogTime = Time.time + 1f;

        Debug.Log($"[BossRangedDecision] distance={distance:F2} meleeRange={bossMeleeAttackRange:F2} rangedMinRange={bossRangedMinRange:F2} rangedMaxRange={bossRangedMaxRange:F2} selectedAttack={selectedAttack} reason={reason}", this);
    }

    private void LogBossMeleeDecision(
        float distanceToTarget,
        bool canAttack,
        string failReason,
        bool isGrounded,
        string selectedAttack,
        string targetName,
        float meleeCooldownRemaining,
        bool isAttacking,
        bool isStunned,
        bool isDead)
    {
        if (!(debugAttackDiagnostics || debugBossMeleeHit || debugLog))
        {
            return;
        }

        if (Time.time < nextBossMeleeDecisionLogTime)
        {
            return;
        }

        nextBossMeleeDecisionLogTime = Time.time + 1f;
        Debug.Log(
            "[BossMeleeDecision] " +
            "enemy=" + name +
            " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
            " species=" + (monsterIdentity != null ? monsterIdentity.species.ToString() : "Unknown") +
            " attackStyle=" + attackStyle +
            " distanceToTarget=" + distanceToTarget.ToString("F2") +
            " bossMeleeAttackRange=" + bossMeleeAttackRange.ToString("F2") +
            " canAttack=" + canAttack +
            " failReason=" + failReason +
            " isGrounded=" + isGrounded +
            " targetAssigned=" + (playerTarget != null) +
            " targetName=" + targetName +
            " selectedAttack=" + selectedAttack +
            " meleeCooldownRemaining=" + meleeCooldownRemaining.ToString("F2") +
            " isAttacking=" + isAttacking +
            " isStunned=" + isStunned +
            " isDead=" + isDead,
            this);
    }

    private Vector3 ResolveEnemySeparationDirection()
    {
        float separationRadius = Mathf.Max(0f, enemySeparationRadius);
        if (!enableEnemySoftAvoidance || separationRadius <= 0.01f || enemySeparationMaxNeighbors <= 0)
        {
            return Vector3.zero;
        }

        if (separationHits == null || separationHits.Length != Mathf.Max(1, enemySeparationMaxNeighbors))
        {
            separationHits = new Collider[Mathf.Max(1, enemySeparationMaxNeighbors)];
        }

        int enemyLayer = gameObject.layer;
        int layerMask = enemyLayer >= 0 ? (1 << enemyLayer) : ~0;
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            separationRadius,
            separationHits,
            layerMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            return Vector3.zero;
        }

        Vector3 separation = Vector3.zero;
        int neighborCount = 0;
        Vector3 selfPosition = transform.position;
        selfPosition.y = 0f;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = separationHits[i];
            if (hit == null)
            {
                continue;
            }

            Transform root = hit.transform.root;
            if (root == null || root == transform.root)
            {
                continue;
            }

            Vector3 otherPosition = root.position;
            otherPosition.y = 0f;
            Vector3 away = selfPosition - otherPosition;
            float distance = away.magnitude;
            if (distance <= 0.001f || distance > separationRadius)
            {
                continue;
            }

            float weight = 1f - Mathf.Clamp01(distance / separationRadius);
            separation += away.normalized * weight;
            neighborCount++;
        }

        for (int i = 0; i < hitCount; i++)
        {
            separationHits[i] = null;
        }

        if (neighborCount <= 0 || separation.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        separation /= neighborCount;
        separation.y = 0f;
        return separation.normalized;
    }

    private void BeginAttack()
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss && activeBossAttackKind == BossAttackKind.None)
        {
            activeBossAttackKind = BossAttackKind.Melee;
        }

        lastAttackTime = Time.time;
        nextAttackTime = Time.time + ResolveCurrentAttackCooldown();
        pendingAttackTarget = playerTarget;
        attackInProgress = true;
        attackHitFrameTriggeredThisAttack = false;
        lastMeleeAttackResult = "pending";
        CancelInvoke(nameof(FinishAttackRecovery));
        rb.linearVelocity = Vector3.zero;
        StopMoveAnimation();
        if (debugAttackDiagnostics)
        {
            Debug.Log($"[EnemyAttack] StartAttack enemy={name} target={(pendingAttackTarget != null ? pendingAttackTarget.name : "null")}", this);
        }

        if (attackStyle == MonsterAttackStyle.ElementalBoss && activeBossAttackKind == BossAttackKind.Melee)
        {
            Vector3 attackDirection;
            Vector3 attackOrigin = ResolveBossMeleeHitOrigin(pendingAttackTarget, out attackDirection);
            float attackWindup = slimeAnimation != null ? slimeAnimation.AttackWindup : 0f;
            float attackRecover = slimeAnimation != null ? slimeAnimation.AttackRecovery : AttackRecoveryDurationSeconds;
            Debug.Log(
                "[BossMeleeAttack] " +
                "enemy=" + name +
                " target=" + (pendingAttackTarget != null ? pendingAttackTarget.name : "null") +
                " start attack" +
                " attackWindup=" + attackWindup.ToString("F2") +
                " attackHitTime=" + attackWindup.ToString("F2") +
                " attackRecover=" + attackRecover.ToString("F2") +
                " attackDamage=" + ResolveCurrentAttackDamage(ResolvePrimaryDamageType()).ToString("F2") +
                " attackRadius=" + Mathf.Max(0.1f, bossMeleeHitRadius).ToString("F2") +
                " attackOrigin=" + attackOrigin +
                " attackDirection=" + attackDirection,
                this);
        }
        else if (!UsesProjectileAttack())
        {
            Vector3 attackDirection;
            Vector3 attackOrigin = ResolveGenericMeleeHitOrigin(pendingAttackTarget, out attackDirection);
            float attackWindup = slimeAnimation != null ? slimeAnimation.AttackWindup : 0f;
            float attackRecover = slimeAnimation != null ? slimeAnimation.AttackRecovery : AttackRecoveryDurationSeconds;
            Debug.Log(
                "[EnemyMeleeAttack] " +
                "enemy=" + name +
                " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
                " target=" + (pendingAttackTarget != null ? pendingAttackTarget.name : "null") +
                " start attack" +
                " attackWindup=" + attackWindup.ToString("F2") +
                " attackHitTime=" + attackWindup.ToString("F2") +
                " attackRecover=" + attackRecover.ToString("F2") +
                " attackDamage=" + ResolveCurrentAttackDamage(ResolvePrimaryDamageType()).ToString("F2") +
                " attackOrigin=" + attackOrigin +
                " attackRange=" + ResolveCurrentMeleeHitRadius().ToString("F2") +
                " attackDirection=" + attackDirection,
                this);
        }

        LogSlimeAttackLifecycle("BeginAttack", pendingAttackTarget, "Triggered");
        LogAttackStateChange(
            EnemyAttackRuntimeState.AttackInProgress,
            playerTarget != null ? Vector3.Distance(playerTarget.position, transform.position) : -1f,
            playerTarget != null ? ResolveHorizontalEdgeDistance(playerTarget, out _, out _) : -1f,
            playerTarget != null ? Mathf.Abs(playerTarget.position.y - transform.position.y) : 0f,
            IsGroundedForAttack(),
            false,
            "BeginAttack",
            Vector3.zero,
            Vector3.zero);

        // 触发攻击动画后，按动画时序进入冷却恢复阶段。
        if (slimeAnimation != null)
        {
            LogSlimeAttackLifecycle("PlayAttackAnimation", pendingAttackTarget, "SlimeAnimation");
            slimeAnimation.PlayAttack(pendingAttackTarget);
            Invoke(nameof(FinishAttackRecovery), AttackRecoveryDurationSeconds);
        }
        else
        {
            HandleAttackHit(pendingAttackTarget);
            FinishAttackRecovery();
        }
    }

    private void FinishAttackRecovery()
    {
        bool hadPendingAttack = attackInProgress;
        Transform finishedTarget = pendingAttackTarget;
        attackInProgress = false;
        activeBossAttackKind = BossAttackKind.None;
        if (hadPendingAttack && !UsesProjectileAttack() && !attackHitFrameTriggeredThisAttack)
        {
            lastMeleeAttackResult = "no-hit-frame";
            Debug.Log(
                "[EnemyMeleeDamageFlow] " +
                "enemy=" + name +
                " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
                " target=" + (finishedTarget != null ? finishedTarget.name : "null") +
                " source=EnemyMelee damageBeforeModifiers=0.00 damageAfterModifiers=0.00 hitChance=unresolved missRoll=unresolved isMiss=false" +
                " targetInvincible=false targetShield=0.00 targetCombatHealthFound=false TakeDamageCalled=false result=no-hit-frame playerHpBefore=n/a playerHpAfter=n/a",
                this);
        }
        LogSlimeAttackLifecycle("AttackFinished", pendingAttackTarget, "RecoveryComplete");
        pendingAttackTarget = null;
        if (debugAttackDiagnostics)
        {
            Debug.Log($"[EnemyAttack] AttackFinished enemy={name}", this);
        }
        LogAttackStateChange(
            EnemyAttackRuntimeState.AttackRecovery,
            playerTarget != null ? Vector3.Distance(playerTarget.position, transform.position) : -1f,
            playerTarget != null ? ResolveHorizontalEdgeDistance(playerTarget, out _, out _) : -1f,
            playerTarget != null ? Mathf.Abs(playerTarget.position.y - transform.position.y) : 0f,
            IsGroundedForAttack(),
            false,
            "FinishAttackRecovery",
            Vector3.zero,
            Vector3.zero);
    }

    private void HandleAttackHit(Transform target)
    {
        Transform hitTarget = target != null ? target : pendingAttackTarget;
        attackHitFrameTriggeredThisAttack = true;
        LogSlimeAttackLifecycle("AttackHitCallback", hitTarget, hitTarget != null ? "CallbackReceived" : "NoTarget");
        if (hitTarget == null)
        {
            if (debugAttackDiagnostics)
            {
                Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target=null damage=0 reason=NoTarget", this);
            }
            LogAttackAttempt(hitTarget, false, false, false, false, "NoTarget");
            FinishAttackRecovery();
            return;
        }

        Vector3 toTarget = hitTarget.position - transform.position;
        toTarget.y = 0f;
        if (UsesProjectileAttack())
        {
            float projectileCenterDistance = toTarget.magnitude;
            float projectileEdgeDistance = ResolveHorizontalEdgeDistance(hitTarget, out _, out _);
            float projectileAttackDistance = ResolveAttackDistance(projectileCenterDistance, projectileEdgeDistance);
            if (projectileAttackDistance > attackHitRange)
            {
                if (debugAttackDiagnostics)
                {
                    Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target={hitTarget.name} damage=0 reason=TooFarDistance", this);
                }
                LogAttackAttempt(hitTarget, true, false, false, false, "TooFarDistance");
                FinishAttackRecovery();
                return;
            }

            LogAttackAttempt(hitTarget, true, true, true, true, "Projectile");
            ExecuteProjectileAttack(hitTarget);
        }
        else
        {
            bool insideHitRange;
            CombatHealth combatHealth = null;
            string meleeRejectReason = string.Empty;
            Vector3 meleeHitOrigin = Vector3.zero;
            Vector3 meleeAttackDirection = Vector3.zero;
            float resolvedMeleeHitRadius = 0f;
            int meleeHitColliderCount = 0;

            insideHitRange = TryResolveMeleeHitTarget(
                hitTarget,
                out combatHealth,
                out meleeRejectReason,
                out meleeHitOrigin,
                out meleeAttackDirection,
                out resolvedMeleeHitRadius,
                out meleeHitColliderCount);

            if (!insideHitRange)
            {
                if (debugAttackDiagnostics)
                {
                    Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target={hitTarget.name} damage=0 reason=HitCheckFailed", this);
                }
                string failureReason = string.IsNullOrEmpty(meleeRejectReason) ? "no-target-in-hit-range" : meleeRejectReason;
                lastMeleeAttackResult = failureReason;
                Debug.Log(
                    "[EnemyMeleeDamageFlow] " +
                    "enemy=" + name +
                    " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
                    " target=" + hitTarget.name +
                    " source=EnemyMelee damageBeforeModifiers=0.00 damageAfterModifiers=0.00 hitChance=unresolved missRoll=unresolved isMiss=false" +
                    " targetInvincible=false targetShield=0.00 targetCombatHealthFound=false TakeDamageCalled=false result=no-target-in-hit-range playerHpBefore=n/a playerHpAfter=n/a",
                    this);
                LogAttackAttempt(hitTarget, true, false, true, false, failureReason);
                FinishAttackRecovery();
                return;
            }

            if (combatHealth == null)
            {
                if (debugAttackDiagnostics)
                {
                    Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target={hitTarget.name} damage=0 reason=NotPlayer", this);
                }
                lastMeleeAttackResult = "invalid-target";
                Debug.Log(
                    "[EnemyMeleeDamageFlow] " +
                    "enemy=" + name +
                    " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
                    " target=" + hitTarget.name +
                    " source=EnemyMelee damageBeforeModifiers=0.00 damageAfterModifiers=0.00 hitChance=unresolved missRoll=unresolved isMiss=false" +
                    " targetInvincible=false targetShield=0.00 targetCombatHealthFound=false TakeDamageCalled=false result=invalid-target playerHpBefore=n/a playerHpAfter=n/a",
                    this);
                LogAttackAttempt(hitTarget, true, insideHitRange, true, false, "invalid-target");
                FinishAttackRecovery();
                return;
            }

            if (combatHealth != null)
            {
                BattleDamageType damageType = ResolvePrimaryDamageType();
                float currentAttackDamage = ResolveCurrentAttackDamage(damageType);
                LogSlimeAttackLifecycle("ApplyMeleeHit", hitTarget, $"damage={currentAttackDamage:F2}");
                if (debugLog && Mathf.Abs(ResolveAttackMultiplier() - lastLoggedAttackMultiplier) > 0.001f)
                {
                    Debug.Log($"[EnemyController] finalAttackDamage={currentAttackDamage:F2} multiplier={ResolveAttackMultiplier():F2}", this);
                    lastLoggedAttackMultiplier = ResolveAttackMultiplier();
                }

                if (debugAttackDiagnostics)
                {
                    Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target={hitTarget.name} damage={currentAttackDamage:F2}", this);
                    Debug.Log($"[EnemyAttack] ApplyDamage enemy={name} target={hitTarget.name}", this);
                }

                float playerHpBefore = ResolveCombatHealthValue(combatHealth);
                float playerShieldBefore = combatHealth.GetShield();
                Debug.Log(
                    "[EnemyMeleeDamageFlow] " +
                    "enemy=" + name +
                    " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
                    " target=" + hitTarget.name +
                    " source=EnemyMelee" +
                    " damageBeforeModifiers=" + currentAttackDamage.ToString("F2") +
                    " damageAfterModifiers=" + currentAttackDamage.ToString("F2") +
                    " hitChance=see-CombatEvasion" +
                    " missRoll=see-CombatEvasion" +
                    " isMiss=pending" +
                    " targetInvincible=false" +
                    " targetShield=" + playerShieldBefore.ToString("F2") +
                    " targetCombatHealthFound=true" +
                    " TakeDamageCalled=true" +
                    " result=pending" +
                    " playerHpBefore=" + playerHpBefore.ToString("F2") +
                    " playerHpAfter=pending",
                    this);

                combatHealth.TakeDamage(new BattleDamage(currentAttackDamage, damageType, gameObject));
                float playerHpAfter = ResolveCombatHealthValue(combatHealth);
                float playerShieldAfter = combatHealth.GetShield();
                string damageResult = ResolveEnemyMeleeDamageResult(playerHpBefore, playerHpAfter, playerShieldBefore, playerShieldAfter);
                lastMeleeAttackResult = damageResult;
                Debug.Log(
                    "[EnemyMeleeDamageFlow] " +
                    "enemy=" + name +
                    " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
                    " target=" + hitTarget.name +
                    " source=EnemyMelee" +
                    " damageBeforeModifiers=" + currentAttackDamage.ToString("F2") +
                    " damageAfterModifiers=" + currentAttackDamage.ToString("F2") +
                    " hitChance=see-CombatEvasion" +
                    " missRoll=see-CombatEvasion" +
                    " isMiss=" + (damageResult == "miss") +
                    " targetInvincible=" + (damageResult == "invincible") +
                    " targetShield=" + playerShieldAfter.ToString("F2") +
                    " targetCombatHealthFound=true" +
                    " TakeDamageCalled=true" +
                    " result=" + damageResult +
                    " playerHpBefore=" + playerHpBefore.ToString("F2") +
                    " playerHpAfter=" + playerHpAfter.ToString("F2"),
                    this);
                LogAttackAttempt(hitTarget, true, insideHitRange, true, true, "DamageApplied");
            }
            else if (debugAttackDiagnostics)
            {
                Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target={hitTarget.name} damage=0 reason=NoCombatHealth", this);
                lastMeleeAttackResult = "invalid-target";
                LogAttackAttempt(hitTarget, true, insideHitRange, true, false, "NoCombatHealth");
            }
        }

        FinishAttackRecovery();
    }

    private bool CanHitMeleeTarget(Transform hitTarget)
    {
        if (hitTarget == null)
        {
            return false;
        }

        if (requireGroundedToAttack && !IsGroundedForAttack())
        {
            return false;
        }

        float verticalDifference = Mathf.Abs(hitTarget.position.y - transform.position.y);
        if (verticalDifference > Mathf.Max(0f, maxVerticalAttackDifference))
        {
            return false;
        }

        Vector3 playerCenter = ResolvePlayerBodyCenter(hitTarget);
        Vector3 enemyClosest = ResolveEnemyClosestPoint(playerCenter);
        Vector3 playerClosest = ResolvePlayerClosestPoint(hitTarget, enemyClosest);

        Vector3 flatEnemyPoint = enemyClosest;
        flatEnemyPoint.y = 0f;
        Vector3 flatPlayerPoint = playerClosest;
        flatPlayerPoint.y = 0f;
        float distance = Vector3.Distance(flatEnemyPoint, flatPlayerPoint);
        float hitRadius = Mathf.Max(0f, meleeBodyContactRadius);
        float horizontalAttackDistance = Mathf.Max(hitRadius, maxHorizontalAttackDistance > 0f ? maxHorizontalAttackDistance : attackHitRange);

        if (debugMeleeHitCheck)
        {
            Color debugColor = distance <= hitRadius ? Color.red : Color.cyan;
            Debug.DrawLine(enemyClosest, playerClosest, debugColor, 0.4f, false);
            Debug.DrawRay(enemyClosest, Vector3.up * 0.5f, debugColor, 0.4f, false);
            Debug.DrawRay(playerClosest, Vector3.up * 0.5f, debugColor, 0.4f, false);
        }

        if (distance <= hitRadius)
        {
            return true;
        }

        return distance <= horizontalAttackDistance;
    }

    private bool TryResolveMeleeHitTarget(
        Transform hitTarget,
        out CombatHealth targetCombatHealth,
        out string rejectReason,
        out Vector3 hitOrigin,
        out Vector3 attackDirection,
        out float hitRadius,
        out int hitColliderCount)
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss && activeBossAttackKind == BossAttackKind.Melee)
        {
            return TryResolveBossMeleeHitTarget(
                hitTarget,
                out targetCombatHealth,
                out rejectReason,
                out hitOrigin,
                out attackDirection,
                out hitRadius,
                out hitColliderCount);
        }

        targetCombatHealth = null;
        rejectReason = "NoTarget";
        hitOrigin = ResolveGenericMeleeHitOrigin(hitTarget, out attackDirection);
        hitRadius = ResolveCurrentMeleeHitRadius();
        EnsureBossMeleeHitResultsBuffer();
        hitColliderCount = Physics.OverlapSphereNonAlloc(
            hitOrigin,
            hitRadius,
            bossMeleeHitResults,
            ~0,
            QueryTriggerInteraction.Collide);

        Transform targetRoot = hitTarget != null ? hitTarget.root : null;
        System.Collections.Generic.List<string> details = new System.Collections.Generic.List<string>();

        for (int i = 0; i < hitColliderCount; i++)
        {
            Collider collider = bossMeleeHitResults[i];
            if (collider == null)
            {
                details.Add("collider=null acceptedTarget=false rejectReason=null-collider");
                continue;
            }

            Transform colliderRoot = collider.transform.root;
            bool isPlayer = BattleTargetUtility.IsPlayer(collider.gameObject) || (colliderRoot != null && BattleTargetUtility.IsPlayer(colliderRoot.gameObject));
            CombatHealth combatHealth = collider.GetComponentInParent<CombatHealth>();
            bool rootMatchesTarget = targetRoot == null || colliderRoot == targetRoot || (combatHealth != null && combatHealth.transform.root == targetRoot);
            bool targetDead = combatHealth != null && combatHealth.IsDead;
            bool acceptedTarget = isPlayer && combatHealth != null && rootMatchesTarget && !targetDead;
            string currentRejectReason = acceptedTarget
                ? "None"
                : !isPlayer
                    ? "WrongTag"
                    : combatHealth == null
                        ? "CombatHealthNotFound"
                        : !rootMatchesTarget
                            ? "WrongPlayerRoot"
                            : targetDead
                                ? "TargetDead"
                                : "Unknown";

            details.Add(
                "collider name=" + collider.name +
                " collider root=" + (colliderRoot != null ? colliderRoot.name : "null") +
                " collider layer=" + LayerMask.LayerToName(collider.gameObject.layer) +
                " collider tag=" + collider.tag +
                " collider isTrigger=" + collider.isTrigger +
                " has CombatHealth=" + (combatHealth != null) +
                " isPlayer=" + isPlayer +
                " acceptedTarget=" + acceptedTarget +
                " rejectReason=" + currentRejectReason);

            if (!acceptedTarget)
            {
                rejectReason = currentRejectReason;
                continue;
            }

            targetCombatHealth = combatHealth;
            rejectReason = string.Empty;
            break;
        }

        if (hitColliderCount <= 0)
        {
            rejectReason = "NoTargetInHitRange";
        }
        else if (targetCombatHealth == null && string.IsNullOrEmpty(rejectReason))
        {
            rejectReason = "InvalidTarget";
        }

        Debug.Log(
            "[EnemyMeleeHitCheck] " +
            "enemy=" + name +
            " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
            " target=" + (hitTarget != null ? hitTarget.name : "null") +
            " hitFrameReached=true" +
            " hitOrigin=" + hitOrigin +
            " hitRadius=" + hitRadius.ToString("F2") +
            " hitLayerMask=Everything" +
            " hitColliderCount=" + hitColliderCount +
            " attackDirection=" + attackDirection +
            " details=" + (details.Count > 0 ? string.Join(" | ", details) : "none") +
            " acceptedTarget=" + (targetCombatHealth != null) +
            " rejectReason=" + (string.IsNullOrEmpty(rejectReason) ? "None" : rejectReason),
            this);

        return targetCombatHealth != null;
    }

    private bool TryResolveBossMeleeHitTarget(
        Transform hitTarget,
        out CombatHealth targetCombatHealth,
        out string rejectReason,
        out Vector3 hitOrigin,
        out Vector3 attackDirection,
        out float hitRadius,
        out int hitColliderCount)
    {
        targetCombatHealth = null;
        rejectReason = "NoTarget";
        hitOrigin = ResolveBossMeleeHitOrigin(hitTarget, out attackDirection);
        hitRadius = Mathf.Max(0.1f, bossMeleeHitRadius);
        EnsureBossMeleeHitResultsBuffer();
        hitColliderCount = Physics.OverlapSphereNonAlloc(
            hitOrigin,
            hitRadius,
            bossMeleeHitResults,
            ~0,
            QueryTriggerInteraction.Collide);

        Transform targetRoot = hitTarget != null ? hitTarget.root : null;
        System.Collections.Generic.List<string> details = new System.Collections.Generic.List<string>();

        for (int i = 0; i < hitColliderCount; i++)
        {
            Collider collider = bossMeleeHitResults[i];
            if (collider == null)
            {
                details.Add("collider=null acceptedTarget=false rejectReason=null-collider");
                continue;
            }

            Transform colliderRoot = collider.transform.root;
            bool isPlayer = BattleTargetUtility.IsPlayer(collider.gameObject) || (colliderRoot != null && BattleTargetUtility.IsPlayer(colliderRoot.gameObject));
            CombatHealth combatHealth = collider.GetComponentInParent<CombatHealth>();
            bool rootMatchesTarget = targetRoot == null || colliderRoot == targetRoot || (combatHealth != null && combatHealth.transform.root == targetRoot);

            bool acceptedTarget = isPlayer && combatHealth != null && rootMatchesTarget;
            string currentRejectReason = acceptedTarget
                ? "None"
                : !isPlayer
                    ? "NotPlayer"
                    : combatHealth == null
                        ? "NoCombatHealth"
                        : !rootMatchesTarget
                            ? "WrongPlayerRoot"
                            : "Unknown";

            details.Add(
                "collider name=" + collider.name +
                " collider root=" + (colliderRoot != null ? colliderRoot.name : "null") +
                " collider layer=" + LayerMask.LayerToName(collider.gameObject.layer) +
                " collider tag=" + collider.tag +
                " collider isTrigger=" + collider.isTrigger +
                " has CombatHealth=" + (combatHealth != null) +
                " isPlayer=" + isPlayer +
                " acceptedTarget=" + acceptedTarget +
                " rejectReason=" + currentRejectReason);

            if (!acceptedTarget)
            {
                rejectReason = currentRejectReason;
                continue;
            }

            targetCombatHealth = combatHealth;
            rejectReason = string.Empty;
            break;
        }

        if (hitColliderCount <= 0)
        {
            rejectReason = "NoHitCollider";
        }
        else if (targetCombatHealth == null && string.IsNullOrEmpty(rejectReason))
        {
            rejectReason = "NoAcceptedPlayerCollider";
        }

        if (debugBossMeleeHit || debugAttackDiagnostics || debugLog)
        {
            Debug.Log(
                "[BossMeleeHitCheck] " +
                "enemy=" + name +
                " target=" + (hitTarget != null ? hitTarget.name : "null") +
                " hitOrigin=" + hitOrigin +
                " hitRadius=" + hitRadius.ToString("F2") +
                " hitLayerMask=Everything" +
                " hitColliderCount=" + hitColliderCount +
                " attackDirection=" + attackDirection +
                " details=" + (details.Count > 0 ? string.Join(" | ", details) : "none") +
                " finalAcceptedTarget=" + (targetCombatHealth != null) +
                " finalRejectReason=" + (string.IsNullOrEmpty(rejectReason) ? "None" : rejectReason),
                this);
        }

        return targetCombatHealth != null;
    }

    private Vector3 ResolveBossMeleeHitOrigin(Transform hitTarget, out Vector3 attackDirection)
    {
        Vector3 direction = hitTarget != null ? hitTarget.position - transform.position : Vector3.zero;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = playerTarget != null ? playerTarget.position - transform.position : Vector3.right;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.right;
        }

        attackDirection = direction.normalized;
        return transform.position + attackDirection * bossMeleeHitForwardOffset + Vector3.up * bossMeleeHitHeight;
    }

    private Vector3 ResolveGenericMeleeHitOrigin(Transform hitTarget, out Vector3 attackDirection)
    {
        Vector3 direction = hitTarget != null ? hitTarget.position - transform.position : Vector3.zero;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = playerTarget != null ? playerTarget.position - transform.position : Vector3.right;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.right;
        }

        attackDirection = direction.normalized;
        return transform.position + attackDirection * ResolveCurrentMeleeHitForwardOffset() + Vector3.up * ResolveCurrentMeleeHitHeight();
    }

    private void EnsureBossMeleeHitResultsBuffer()
    {
        if (bossMeleeHitResults == null || bossMeleeHitResults.Length < 16)
        {
            bossMeleeHitResults = new Collider[16];
        }
    }

    private static float ResolveCombatHealthValue(CombatHealth combatHealth)
    {
        if (combatHealth == null)
        {
            return 0f;
        }

        return combatHealth.resourceBank != null
            ? Mathf.Max(0f, combatHealth.resourceBank.currentHealth)
            : Mathf.Max(0f, combatHealth.currentHealth);
    }

    private static string ResolveBossMeleeDamageResult(float hpBefore, float hpAfter, float shieldBefore, float shieldAfter)
    {
        if (hpAfter < hpBefore)
        {
            return "applied";
        }

        if (shieldAfter < shieldBefore)
        {
            return "shielded";
        }

        return "no-effective-damage";
    }

    private static string ResolveEnemyMeleeDamageResult(float hpBefore, float hpAfter, float shieldBefore, float shieldAfter)
    {
        if (hpAfter < hpBefore)
        {
            return "applied";
        }

        if (shieldAfter < shieldBefore)
        {
            return "shielded";
        }

        return "no-effective-damage";
    }

    private void ResolveMeleeHitSources()
    {
        if (meleeEnemyCollider == null)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider candidate = colliders[i];
                if (candidate == null || !candidate.enabled || candidate.isTrigger)
                {
                    continue;
                }

                meleeEnemyCollider = candidate;
                break;
            }
        }

        if (debugSlimeAttackLogs && meleeEnemyCollider != null && meleeEnemyCollider != lastLoggedMeleeCollider)
        {
            Bounds bounds = meleeEnemyCollider.bounds;
            float radius = meleeEnemyCollider is SphereCollider sphereCollider ? sphereCollider.radius : -1f;
            Debug.Log(
                $"[SlimeAttackCollider] name={name} collider={meleeEnemyCollider.name} isTrigger={meleeEnemyCollider.isTrigger} radius={(radius >= 0f ? radius.ToString("F2") : "n/a")} boundsSize={bounds.size}",
                this);
            lastLoggedMeleeCollider = meleeEnemyCollider;
        }

        if (meleeEnemySpriteRenderer == null)
        {
            Transform namedVisual = transform.Find("Visual_Slime");
            if (namedVisual != null)
            {
                meleeEnemySpriteRenderer = namedVisual.GetComponent<SpriteRenderer>();
            }
        }

        if (meleeEnemySpriteRenderer == null && slimeAnimation != null)
        {
            SpriteRenderer spriteRenderer = slimeAnimation.GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer != null)
            {
                meleeEnemySpriteRenderer = spriteRenderer;
            }
        }
    }

    private Vector3 ResolveEnemyBodyCenter()
    {
        ResolveMeleeHitSources();

        if (meleeEnemyCollider != null)
        {
            return meleeEnemyCollider.bounds.center;
        }

        if (meleeEnemySpriteRenderer != null)
        {
            return meleeEnemySpriteRenderer.bounds.center;
        }

        return transform.position;
    }

    private Vector3 ResolveEnemyClosestPoint(Vector3 playerCenter)
    {
        ResolveMeleeHitSources();

        if (meleeEnemyCollider != null)
        {
            return meleeEnemyCollider.ClosestPoint(playerCenter);
        }

        if (meleeEnemySpriteRenderer != null)
        {
            Bounds bounds = meleeEnemySpriteRenderer.bounds;
            return bounds.ClosestPoint(playerCenter);
        }

        return transform.position;
    }

    private Vector3 ResolvePlayerBodyCenter(Transform hitTarget)
    {
        Collider playerCollider = ResolvePlayerCollider(hitTarget);
        if (playerCollider != null)
        {
            return playerCollider.bounds.center;
        }

        return hitTarget.position;
    }

    private Vector3 ResolvePlayerClosestPoint(Transform hitTarget, Vector3 enemyPoint)
    {
        Collider playerCollider = ResolvePlayerCollider(hitTarget);
        if (playerCollider != null)
        {
            return playerCollider.ClosestPoint(enemyPoint);
        }

        return hitTarget.position;
    }

    private float ResolveHorizontalEdgeDistance(Transform hitTarget, out Vector3 enemyClosest, out Vector3 playerClosest)
    {
        if (hitTarget == null)
        {
            enemyClosest = transform.position;
            playerClosest = transform.position;
            return float.MaxValue;
        }

        Vector3 playerCenter = ResolvePlayerBodyCenter(hitTarget);
        enemyClosest = ResolveEnemyClosestPoint(playerCenter);
        playerClosest = ResolvePlayerClosestPoint(hitTarget, enemyClosest);
        Vector3 flatEnemyPoint = enemyClosest;
        flatEnemyPoint.y = 0f;
        Vector3 flatPlayerPoint = playerClosest;
        flatPlayerPoint.y = 0f;
        return Vector3.Distance(flatEnemyPoint, flatPlayerPoint);
    }

    private float ResolveAttackDistance(float horizontalCenterDistance, float horizontalEdgeDistance)
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss)
        {
            return horizontalEdgeDistance;
        }

        return UsesProjectileAttack() ? horizontalCenterDistance : horizontalEdgeDistance;
    }

    private float ResolveCurrentMeleeHitRadius()
    {
        MonsterRank rank = monsterIdentity != null ? monsterIdentity.rank : MonsterRank.Normal;
        return rank switch
        {
            MonsterRank.Boss => Mathf.Max(0.1f, bossMeleeHitRadius),
            MonsterRank.Elite => Mathf.Max(0.1f, eliteMeleeHitRadius),
            _ => Mathf.Max(0.1f, normalMeleeHitRadius)
        };
    }

    private float ResolveCurrentMeleeHitForwardOffset()
    {
        MonsterRank rank = monsterIdentity != null ? monsterIdentity.rank : MonsterRank.Normal;
        return rank switch
        {
            MonsterRank.Boss => bossMeleeHitForwardOffset,
            MonsterRank.Elite => eliteMeleeHitForwardOffset,
            _ => normalMeleeHitForwardOffset
        };
    }

    private float ResolveCurrentMeleeHitHeight()
    {
        MonsterRank rank = monsterIdentity != null ? monsterIdentity.rank : MonsterRank.Normal;
        return rank switch
        {
            MonsterRank.Boss => bossMeleeHitHeight,
            MonsterRank.Elite => eliteMeleeHitHeight,
            _ => normalMeleeHitHeight
        };
    }

    private void LogEnemyMeleeDecision(
        float distance,
        bool canAttack,
        string failReason,
        bool isGrounded,
        string selectedAttack,
        string targetName,
        float meleeCooldownRemaining,
        bool isAttacking,
        bool isStunned,
        bool isDead)
    {
        if (!(debugAttackDiagnostics || debugMeleeHitCheck || debugLog))
        {
            return;
        }

        if (Time.time < nextEnemyMeleeDecisionLogTime)
        {
            return;
        }

        nextEnemyMeleeDecisionLogTime = Time.time + 1f;
        Debug.Log(
            "[EnemyMeleeDecision] " +
            "enemy=" + name +
            " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
            " species=" + (monsterIdentity != null ? monsterIdentity.species.ToString() : "Unknown") +
            " attackStyle=" + attackStyle +
            " distanceToTarget=" + distance.ToString("F2") +
            " meleeAttackRange=" + attackRange.ToString("F2") +
            " canAttack=" + canAttack +
            " failReason=" + (string.IsNullOrEmpty(failReason) ? "None" : failReason) +
            " isGrounded=" + isGrounded +
            " targetAssigned=" + (playerTarget != null) +
            " targetName=" + targetName +
            " selectedAttack=" + selectedAttack +
            " meleeCooldownRemaining=" + meleeCooldownRemaining.ToString("F2") +
            " isAttacking=" + isAttacking +
            " isStunned=" + isStunned +
            " isDead=" + isDead,
            this);
    }

    private Collider ResolvePlayerCollider(Transform hitTarget)
    {
        if (hitTarget == null)
        {
            return null;
        }

        CapsuleCollider capsule = hitTarget.GetComponentInParent<CapsuleCollider>();
        if (capsule != null && capsule.enabled)
        {
            return capsule;
        }

        CharacterController characterController = hitTarget.GetComponentInParent<CharacterController>();
        if (characterController != null && characterController.enabled)
        {
            return characterController;
        }

        Collider[] colliders = hitTarget.GetComponentsInParent<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (candidate != null && candidate.enabled && !candidate.isTrigger)
            {
                return candidate;
            }
        }

        return null;
    }

    private bool UsesProjectileAttack()
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss)
        {
            return activeBossAttackKind == BossAttackKind.Ranged;
        }

        return attackStyle == MonsterAttackStyle.Ranged;
    }

    private void ExecuteProjectileAttack(Transform hitTarget)
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss)
        {
            // Boss multi-skill expansion point: keep projectile as the phase-one fallback
            // until fire ring / ground spike attacks are introduced.
            if (debugAttackDiagnostics || debugLog)
            {
                float distanceToTarget = hitTarget != null ? Vector3.Distance(transform.position, hitTarget.position) : -1f;
                Debug.Log($"[BossAcidAttack] enter ranged branch boss={name} target={(hitTarget != null ? hitTarget.name : "null")} distance={distanceToTarget:F2} attackStyle={attackStyle}", this);
            }
            FireProjectileAt(hitTarget);
            return;
        }

        FireProjectileAt(hitTarget);
    }

    private void PlayMoveAnimation(Vector3 direction, float currentMoveSpeed)
    {
        if (slimeAnimation == null)
        {
            return;
        }

        slimeAnimation.PlayMoveAnimation(new Vector2(direction.x, direction.z), currentMoveSpeed);
    }

    private void StopMoveAnimation()
    {
        if (slimeAnimation == null)
        {
            return;
        }

        slimeAnimation.StopMoveAnimation();
    }

    private void ResolvePlayerTarget()
    {
        if (HasUsableTarget(playerTarget))
        {
            return;
        }

        if (Time.time < nextTargetResolveTime)
        {
            return;
        }

        nextTargetResolveTime = Time.time + Mathf.Max(0.05f, targetResolveRetryInterval);
        TryResolveTarget("AutoReacquire");
    }

    private bool TryResolveTarget(string source)
    {
        if (HasUsableTarget(playerTarget))
        {
            return true;
        }

        string oldTargetName = playerTarget != null ? playerTarget.name : "null";
        Transform resolvedTarget = null;

        if (playerBootstrap == null)
        {
            playerBootstrap = FindObjectOfType<Player2Bootstrap>();
        }

        if (playerBootstrap != null)
        {
            playerBootstrap.EnsureInitializedForSpawn();
            Transform currentPlayer = playerBootstrap.CurrentPlayerTransform;
            if (HasUsableTarget(currentPlayer))
            {
                resolvedTarget = currentPlayer;
            }
        }

        if (resolvedTarget == null && !string.IsNullOrEmpty(playerTag))
        {
            GameObject playerObject = GameObject.FindWithTag(playerTag);
            if (playerObject != null && playerObject.activeInHierarchy)
            {
                resolvedTarget = playerObject.transform;
            }
        }

        playerTarget = null;
        if (!HasUsableTarget(resolvedTarget))
        {
            Debug.Log(
                $"[EnemyTargetResolve] enemy={name} oldTarget={oldTargetName} newTarget=null success=False source={source} reason=NoActivePlayerFound",
                this);
            return false;
        }

        AssignTarget(resolvedTarget, source, oldTargetName);
        return true;
    }

    private bool HasUsableTarget(Transform target)
    {
        return target != null && target.gameObject != null && target.gameObject.activeInHierarchy;
    }

    private void AssignTarget(Transform target, string source, string oldTargetNameOverride = null)
    {
        string oldTargetName = oldTargetNameOverride ?? (playerTarget != null ? playerTarget.name : "null");
        playerTarget = HasUsableTarget(target) ? target : null;

        Debug.Log(
            $"[EnemyTargetResolve] enemy={name} oldTarget={oldTargetName} newTarget={(playerTarget != null ? playerTarget.name : "null")} success={(playerTarget != null)} source={source}{(playerTarget == null ? " reason=NoActivePlayerFound" : string.Empty)}",
            this);
    }

    public string BuildRuntimeDebugSummary(Transform fallbackTarget = null)
    {
        Transform target = HasUsableTarget(playerTarget) ? playerTarget : fallbackTarget;
        float centerDistance = target != null ? Vector3.Distance(target.position, transform.position) : -1f;
        float edgeDistance = float.MaxValue;
        Vector3 enemyClosest = transform.position;
        Vector3 playerClosest = target != null ? target.position : transform.position;
        if (target != null)
        {
            edgeDistance = ResolveHorizontalEdgeDistance(target, out enemyClosest, out playerClosest);
        }

        float verticalDifference = target != null ? Mathf.Abs(target.position.y - transform.position.y) : -1f;
        bool grounded = IsGroundedForAttack();
        string failReason = EvaluateAttackFailReason(edgeDistance, verticalDifference, grounded);
        bool canAttack = string.IsNullOrEmpty(failReason);
        CombatHealth health = GetComponent<CombatHealth>();
        EnemyDebuffReceiver receiver = ResolveDebuffReceiver();
        float cooldownRemaining = Mathf.Max(0f, nextAttackTime - Time.time);
        bool isStunned = receiver != null && receiver.GetMoveSpeedMultiplier() <= 0f;
        bool canMove = rb != null && !attackInProgress;
        string state = playerTarget == null
            ? EnemyAttackRuntimeState.NoTarget.ToString()
            : (attackInProgress ? EnemyAttackRuntimeState.AttackInProgress.ToString() : (canAttack ? EnemyAttackRuntimeState.AttackReady.ToString() : EnemyAttackRuntimeState.Chase.ToString()));
        Collider primaryCollider = meleeEnemyCollider != null ? meleeEnemyCollider : GetComponent<Collider>();
        string rigidbodyConstraints = rb != null ? rb.constraints.ToString() : "None";

        return
            "[MonsterDebug] " +
            $"name={name} position={transform.position} rank={(monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown")} " +
            $"species={(monsterIdentity != null ? monsterIdentity.species.ToString() : "Unknown")} attackStyle={attackStyle} " +
            $"enemyControllerExists=true enemyControllerEnabled={enabled} targetExists={(target != null)} targetName={(target != null ? target.name : "null")} " +
            $"distanceToTarget={(centerDistance >= 0f ? centerDistance.ToString("F2") : "n/a")} edgeDistance={(edgeDistance < float.MaxValue ? edgeDistance.ToString("F2") : "n/a")} " +
            $"moveSpeed={moveSpeed:F2} attackRange={attackRange:F2} rangedAttackRange={attackRange:F2} meleeAttackRange={attackHitRange:F2} " +
            $"currentState={state} canMove={canMove} canAttack={canAttack} failReason={(string.IsNullOrEmpty(failReason) ? "None" : failReason)} " +
            $"isDead={(health != null && health.IsDead)} isStunned={isStunned} isAttacking={attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking)} " +
            $"attackCooldownRemaining={cooldownRemaining:F2} lastAttackTime={lastAttackTime:F2} " +
            $"health={(health != null ? health.currentHealth.ToString("F1") : "n/a")}/{(health != null ? health.MaxHealthValue.ToString("F1") : "n/a")} " +
            $"rigidbodyConstraints={rigidbodyConstraints} colliderEnabled={(primaryCollider != null && primaryCollider.enabled)} " +
            $"layer={LayerMask.LayerToName(gameObject.layer)} tag={gameObject.tag}";
    }

    private EnemyDebuffReceiver ResolveDebuffReceiver()
    {
        if (debuffReceiver == null)
        {
            debuffReceiver = GetComponent<EnemyDebuffReceiver>();
        }

        return debuffReceiver;
    }

    private float ResolveExternalMoveMultiplier()
    {
        EnemyDebuffReceiver receiver = ResolveDebuffReceiver();
        return receiver != null ? receiver.GetMoveSpeedMultiplier() : 1f;
    }

    private float ResolveAttackMultiplier()
    {
        EnemyDebuffReceiver receiver = ResolveDebuffReceiver();
        return receiver != null ? receiver.GetAttackMultiplier() : 1f;
    }

    private float ResolveOutgoingDamageMultiplier()
    {
        EnemyDebuffReceiver receiver = ResolveDebuffReceiver();
        return receiver != null ? receiver.GetOutgoingDamageMultiplier() : 1f;
    }

    private float ResolveCurrentAttackCooldown()
    {
        float baseAttackCooldown = Mathf.Max(0.1f, attackCooldown * Mathf.Max(0.1f, attackIntervalMultiplier));
        float attackSpeedMultiplier = BattleStatUtility.GetEnemyAttackSpeedMultiplier(combatStats);
        float externalAttackCooldownMultiplier = Mathf.Max(1f, ResolveAttackMultiplier());
        return Mathf.Max(0.1f, baseAttackCooldown / Mathf.Max(0.1f, attackSpeedMultiplier) * externalAttackCooldownMultiplier);
    }

    private void LogAttackDiagnostics(
        float statsSpeed,
        float distanceToTarget,
        float horizontalDistance,
        float verticalDifference,
        bool isGrounded,
        bool canAttack,
        string failReason)
    {
        if (!debugAttackDiagnostics || Time.time < nextAttackDiagnosticTime)
        {
            return;
        }

        nextAttackDiagnosticTime = Time.time + Mathf.Max(0.1f, debugAttackLogInterval);

        float attackCooldownMultiplier = ResolveAttackMultiplier();
        float baseAttackCooldown = Mathf.Max(0.1f, attackCooldown * Mathf.Max(0.1f, attackIntervalMultiplier));
        float enemyAttackSpeedMultiplier = BattleStatUtility.GetEnemyAttackSpeedMultiplier(combatStats);
        float finalAttackCooldown = Mathf.Max(0.1f, baseAttackCooldown / Mathf.Max(0.1f, enemyAttackSpeedMultiplier) * Mathf.Max(1f, attackCooldownMultiplier));
        float timeSinceLastAttack = lastAttackTime >= 0f ? Mathf.Max(0f, Time.time - lastAttackTime) : -1f;
        float timeUntilNextAttack = Mathf.Max(0f, nextAttackTime - Time.time);
        bool isBlocked = attackCooldownMultiplier <= 0f;
        bool isKnockback = false;
        bool isStunned = false;
        bool isAttacking = attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking);

        Debug.Log(
            $"[EnemyAttackDiag] name={name} target={(playerTarget != null ? playerTarget.name : "null")} speed={statsSpeed:F2} attackCooldown={attackCooldown:F2} attackIntervalMultiplier={attackIntervalMultiplier:F2} baseAttackCooldown={baseAttackCooldown:F2} enemyAttackSpeedMultiplier={enemyAttackSpeedMultiplier:F2} externalAttackCooldownMultiplier={Mathf.Max(1f, attackCooldownMultiplier):F2} finalAttackCooldown={finalAttackCooldown:F2} timeSinceLastAttack={timeSinceLastAttack:F2} timeUntilNextAttack={timeUntilNextAttack:F2} distanceToTarget={distanceToTarget:F2} horizontalDistance={horizontalDistance:F2} verticalDifference={verticalDifference:F2} attackRange={attackRange:F2} maxHorizontalAttackDistance={maxHorizontalAttackDistance:F2} maxVerticalAttackDifference={maxVerticalAttackDifference:F2} requireGroundedToAttack={requireGroundedToAttack} isGrounded={isGrounded} groundedProbeDistance={groundedProbeDistance:F2} isAttacking={isAttacking} isKnockback={isKnockback} isBlocked={isBlocked} isStunned={isStunned} canAttack={canAttack} failReason={(string.IsNullOrEmpty(failReason) ? "None" : failReason)}",
            this);
    }

    private void LogSpeedDiagnostics(
        float statsSpeed,
        float distanceToTarget,
        bool canAttack)
    {
        if (!debugSpeedDiagnostics || Time.time < nextSpeedDiagnosticTime)
        {
            return;
        }

        nextSpeedDiagnosticTime = Time.time + Mathf.Max(0.1f, debugSpeedLogInterval);

        float speedBonus = Mathf.Max(0f, statsSpeed - 1f);
        float baseEnemyAttackSpeedMultiplier = BattleStatUtility.EnemyAttackSpeedBaseMultiplier;
        float extraEnemyAttackSpeedMax = BattleStatUtility.EnemyAttackSpeedExtraMax;
        float enemyAttackSpeedSoftCap = BattleStatUtility.EnemyAttackSpeedSoftCap;
        float baseAttackCooldown = Mathf.Max(0.1f, attackCooldown * Mathf.Max(0.1f, attackIntervalMultiplier));
        float attackSpeedMultiplier = BattleStatUtility.GetEnemyAttackSpeedMultiplier(combatStats);
        float externalAttackCooldownMultiplier = Mathf.Max(1f, ResolveAttackMultiplier());
        float finalAttackCooldown = Mathf.Max(0.1f, baseAttackCooldown / Mathf.Max(0.1f, attackSpeedMultiplier) * externalAttackCooldownMultiplier);
        float timeSinceLastAttack = lastAttackTime >= 0f ? Mathf.Max(0f, Time.time - lastAttackTime) : -1f;
        float timeUntilNextAttack = Mathf.Max(0f, nextAttackTime - Time.time);

        Debug.Log(
            $"[EnemyAttackDiag] name={name} speed={statsSpeed:F2} speedBonus={speedBonus:F2} baseEnemyAttackSpeedMultiplier={baseEnemyAttackSpeedMultiplier:F2} extraEnemyAttackSpeedMax={extraEnemyAttackSpeedMax:F2} enemyAttackSpeedSoftCap={enemyAttackSpeedSoftCap:F2} enemyAttackSpeedMultiplier={attackSpeedMultiplier:F2} attackCooldown={attackCooldown:F2} attackIntervalMultiplier={attackIntervalMultiplier:F2} baseAttackCooldown={baseAttackCooldown:F2} externalAttackCooldownMultiplier={externalAttackCooldownMultiplier:F2} finalAttackCooldown={finalAttackCooldown:F2} timeSinceLastAttack={timeSinceLastAttack:F2} timeUntilNextAttack={timeUntilNextAttack:F2} canAttack={canAttack} distanceToTarget={distanceToTarget:F2} attackRange={attackRange:F2}",
            this);
    }

    private void LogChaseDiagnostics(
        float distanceToTarget,
        bool canMove,
        bool canAttack,
        bool isKnockback,
        bool isStunned,
        bool isBlocked,
        string currentState,
        float moveSpeed,
        string targetName,
        string reason)
    {
        if (!debugChaseDiagnostics || Time.time < nextChaseDiagnosticTime)
        {
            return;
        }

        nextChaseDiagnosticTime = Time.time + Mathf.Max(0.1f, debugSpeedLogInterval);
        CombatHealth health = GetComponent<CombatHealth>();
        bool isDead = health != null && health.IsDead;
        bool isAttacking = attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking);
        float cooldownRemaining = Mathf.Max(0f, nextAttackTime - Time.time);
        bool targetAssigned = playerTarget != null;
        Debug.Log(
            $"[EnemyChaseDiag] enemy={name} rank={(monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown")} species={(monsterIdentity != null ? monsterIdentity.species.ToString() : "Unknown")} attackStyle={attackStyle} " +
            $"distanceToTarget={distanceToTarget:F2} canMove={canMove} canAttack={canAttack} failReason={reason} " +
            $"requireGroundedToAttack={requireGroundedToAttack} isGrounded={lastGroundProbeHit || (Time.time - lastGroundedTime <= Mathf.Max(0f, groundedAttackGraceTime))} groundedProbeDistance={groundedProbeDistance:F2} groundedProbeOrigin={lastGroundProbeOrigin} groundedProbeCastDistance={lastGroundProbeCastDistance:F2} " +
            $"groundedProbeHit={lastGroundProbeHit} groundedHitName={lastGroundHitName} groundedHitLayer={(lastGroundHitLayer >= 0 ? LayerMask.LayerToName(lastGroundHitLayer) : "None")} " +
            $"attackCooldownRemaining={cooldownRemaining:F2} isDead={isDead} isStunned={isStunned} isKnockback={isKnockback} isAttacking={isAttacking} isBlocked={isBlocked} " +
            $"targetAssigned={targetAssigned} targetName={targetName} currentState={currentState} moveSpeed={moveSpeed:F2}",
            this);
    }

    private void LogAttackStateChange(
        EnemyAttackRuntimeState newState,
        float centerDistance,
        float edgeDistance,
        float verticalDifference,
        bool isGrounded,
        bool canBeginAttack,
        string reason,
        Vector3 enemyClosestPoint,
        Vector3 playerClosestPoint)
    {
        if (lastLoggedAttackState == newState)
        {
            return;
        }

        if (!debugAttackStateTransitions)
        {
            LogSlimeAttackCheck(newState, centerDistance, edgeDistance, verticalDifference, isGrounded, canBeginAttack, reason);
            lastLoggedAttackState = newState;
            return;
        }

        Debug.Log(
            $"[EnemyAttackState] name={name} kind={(monsterIdentity != null ? monsterIdentity.species.ToString() : "Unknown")} rank={(monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown")} attackStyle={attackStyle} oldState={lastLoggedAttackState} newState={newState} centerDistance={centerDistance:F2} edgeDistance={edgeDistance:F2} attackRange={attackRange:F2} attackHitRange={attackHitRange:F2} stopDistance={stopDistance:F2} cooldownRemaining={Mathf.Max(0f, nextAttackTime - Time.time):F2} enemyClosest={enemyClosestPoint} playerClosest={playerClosestPoint} reason={reason}",
            this);
        LogSlimeAttackCheck(newState, centerDistance, edgeDistance, verticalDifference, isGrounded, canBeginAttack, reason);
        lastLoggedAttackState = newState;
    }

    private void LogAttackAttempt(
        Transform hitTarget,
        bool insideAttackRange,
        bool insideHitRange,
        bool attackTriggered,
        bool damageApplied,
        string failureReason)
    {
        if (!debugAttackDiagnostics)
        {
            return;
        }

        Debug.Log(
            $"[EnemyAttackAttempt] name={name} kind={(monsterIdentity != null ? monsterIdentity.species.ToString() : "Unknown")} rank={(monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown")} attackStyle={attackStyle} target={(hitTarget != null ? hitTarget.name : "null")} insideAttackRange={insideAttackRange} insideHitRange={insideHitRange} attackTriggered={attackTriggered} hitDetected={insideHitRange} damageApplied={damageApplied} failureReason={failureReason}",
            this);
    }

    private string EvaluateAttackFailReason(float horizontalDistance, float verticalDifference, bool isGrounded)
    {
        if (playerTarget == null)
        {
            return "NoTarget";
        }

        if (attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking))
        {
            return "AnimationBusy";
        }

        if (Time.time < nextAttackTime)
        {
            return "Cooldown";
        }

        float resolvedAttackRange = Mathf.Max(0.1f, attackRange);
        float resolvedMaxHorizontalAttackDistance = Mathf.Max(0.1f, maxHorizontalAttackDistance > 0f ? maxHorizontalAttackDistance : resolvedAttackRange);
        if (horizontalDistance > resolvedMaxHorizontalAttackDistance)
        {
            return "TooFarHorizontal";
        }

        if (horizontalDistance > resolvedAttackRange)
        {
            return "TooFarDistance";
        }

        if (verticalDifference > Mathf.Max(0f, maxVerticalAttackDifference))
        {
            return "TooHighVerticalDifference";
        }

        if (maxVerticalTargetDifference > 0f && verticalDifference > maxVerticalTargetDifference)
        {
            return "TooFarDistance";
        }

        if (requireGroundedToAttack && !isGrounded)
        {
            return "NotGrounded";
        }

        EnemyDebuffReceiver receiver = ResolveDebuffReceiver();
        if (receiver != null && receiver.GetAttackMultiplier() <= 0f)
        {
            return "Blocked";
        }

        return string.Empty;
    }

    private bool IsGroundedForAttack()
    {
        if (ProbeGrounded())
        {
            lastGroundedTime = Time.time;
            return true;
        }

        return Time.time - lastGroundedTime <= Mathf.Max(0f, groundedAttackGraceTime);
    }

    private bool ProbeGrounded()
    {
        float probeDistance = Mathf.Max(0.01f, groundedProbeDistance);
        ResolveMeleeHitSources();
        lastGroundProbeHit = false;
        lastGroundHitName = "None";
        lastGroundHitLayer = -1;
        lastGroundProbeCastDistance = probeDistance;
        lastGroundProbeOrigin = transform.position + Vector3.up * 0.05f;

        Collider sourceCollider = meleeEnemyCollider != null ? meleeEnemyCollider : GetComponent<Collider>();
        if (sourceCollider != null)
        {
            Bounds bounds = sourceCollider.bounds;
            Vector3 origin = bounds.center + Vector3.up * 0.05f;
            float radius = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.45f, 0.05f, 0.45f);
            float castDistance = Mathf.Max(probeDistance + bounds.extents.y + 0.1f, probeDistance + 0.15f);
            lastGroundProbeOrigin = origin;
            lastGroundProbeCastDistance = castDistance;
            if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit sphereHit, castDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                lastGroundProbeHit = true;
                lastGroundHitName = sphereHit.collider != null ? sphereHit.collider.name : "Unknown";
                lastGroundHitLayer = sphereHit.collider != null ? sphereHit.collider.gameObject.layer : -1;
                return true;
            }

            float rayDistance = castDistance + radius;
            lastGroundProbeCastDistance = rayDistance;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit rayHit, rayDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                lastGroundProbeHit = true;
                lastGroundHitName = rayHit.collider != null ? rayHit.collider.name : "Unknown";
                lastGroundHitLayer = rayHit.collider != null ? rayHit.collider.gameObject.layer : -1;
                return true;
            }

            return false;
        }

        Vector3 fallbackOrigin = transform.position + Vector3.up * 0.05f;
        lastGroundProbeOrigin = fallbackOrigin;
        lastGroundProbeCastDistance = probeDistance + 0.05f;
        if (Physics.Raycast(fallbackOrigin, Vector3.down, out RaycastHit fallbackHit, probeDistance + 0.05f, ~0, QueryTriggerInteraction.Ignore))
        {
            lastGroundProbeHit = true;
            lastGroundHitName = fallbackHit.collider != null ? fallbackHit.collider.name : "Unknown";
            lastGroundHitLayer = fallbackHit.collider != null ? fallbackHit.collider.gameObject.layer : -1;
            return true;
        }

        return false;
    }

    private bool IsSlimeIdentity()
    {
        if (monsterIdentity == null)
        {
            return false;
        }

        switch (monsterIdentity.species)
        {
            case MonsterSpecies.BlueSlime:
            case MonsterSpecies.GreenSlime:
            case MonsterSpecies.LavaSlime:
            case MonsterSpecies.PoisonSlime:
            case MonsterSpecies.RainbowSlime:
                return true;
            default:
                return false;
        }
    }

    private void LogRuntimeAttackConfigOnce()
    {
        if (!debugSlimeAttackLogs || hasLoggedRuntimeAttackConfig || !IsSlimeIdentity())
        {
            return;
        }

        ResolveMeleeHitSources();
        Bounds colliderBounds = meleeEnemyCollider != null ? meleeEnemyCollider.bounds : default;
        float bodyRadius = meleeEnemyCollider is SphereCollider sphereCollider ? sphereCollider.radius : -1f;
        float speedValue = combatStats != null ? combatStats.speed : 0f;
        float physicalAttack = combatStats != null ? combatStats.physicalAttack : 0f;
        float specialAttack = combatStats != null ? combatStats.specialAttack : 0f;
        float attackWindup = slimeAnimation != null ? slimeAnimation.AttackWindup : 0f;
        float attackRecovery = slimeAnimation != null ? slimeAnimation.AttackRecovery : 0f;
        float attackAnimationDuration = slimeAnimation != null ? slimeAnimation.AttackAnimationDuration : 0f;
        string attackClipName = slimeAnimation != null ? slimeAnimation.SelectedAttackClipName : "None";
        float attackClipLength = slimeAnimation != null ? slimeAnimation.SelectedAttackClipLength : 0f;
        float animatorSpeed = slimeAnimation != null ? slimeAnimation.AnimatorPlaybackSpeed : 1f;
        string sourceName = name.Replace("(Clone)", string.Empty).Trim();
        Debug.Log(
            $"[SlimeRuntimeComparison] name={name} prefab/source name={sourceName} monsterKind={monsterIdentity.species} rank={monsterIdentity.rank} attackStyle={attackStyle} attackRange={attackRange:F2} attackHitRange={attackHitRange:F2} attackCooldown={attackCooldown:F2} stoppingDistance={stopDistance:F2} verticalTolerance={maxVerticalAttackDifference:F2} requireGrounded={requireGroundedToAttack} groundGrace={groundedAttackGraceTime:F2} groundProbeDistance={groundedProbeDistance:F2} attackWindup={attackWindup:F2} attackRecovery={attackRecovery:F2} animationDuration={attackAnimationDuration:F2} targetRefreshInterval={targetResolveRetryInterval:F2} speed={speedValue:F2} physicalAttack={physicalAttack:F2} specialAttack={specialAttack:F2} bodyColliderRadius={(bodyRadius >= 0f ? bodyRadius.ToString("F2") : "n/a")} bodyColliderBounds={colliderBounds.size} rigidbodyMass={(rb != null ? rb.mass.ToString("F2") : "n/a")} animatorSpeed={animatorSpeed:F2} selectedAttackClipName={attackClipName} selectedAttackClipLength={attackClipLength:F2} allowAttackForwardLeap={(slimeAnimation != null && slimeAnimation.AllowAttackForwardLeap)} allowAttackVerticalLeap={(slimeAnimation != null && slimeAnimation.AllowAttackVerticalLeap)} maxAttackLeapDistance={(slimeAnimation != null ? slimeAnimation.MaxAttackLeapDistance.ToString("F2") : "0.00")}",
            this);
        hasLoggedRuntimeAttackConfig = true;
    }

    private void LogSlimeAttackCheck(
        EnemyAttackRuntimeState newState,
        float centerDistance,
        float edgeDistance,
        float verticalDifference,
        bool isGrounded,
        bool canBeginAttack,
        string failureReason)
    {
        if (!debugSlimeAttackLogs || !IsSlimeIdentity())
        {
            return;
        }

        Debug.Log(
            $"[SlimeAttackCheck] name={name} kind={monsterIdentity.species} rank={monsterIdentity.rank} attackStyle={attackStyle} target={(playerTarget != null ? playerTarget.name : "null")} centerDistance={centerDistance:F2} edgeDistance={edgeDistance:F2} attackRange={attackRange:F2} attackHitRange={attackHitRange:F2} verticalDifference={verticalDifference:F2} isGrounded={isGrounded} isAttacking={attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking)} cooldownRemaining={Mathf.Max(0f, nextAttackTime - Time.time):F2} canBeginAttack={canBeginAttack} failureReason={(string.IsNullOrEmpty(failureReason) ? newState.ToString() : failureReason)}",
            this);
    }

    private void LogSlimeAttackLifecycle(string stage, Transform target, string detail)
    {
        if (!debugSlimeAttackLogs || !IsSlimeIdentity())
        {
            return;
        }

        Debug.Log(
            $"[SlimeAttackLifecycle] stage={stage} name={name} kind={monsterIdentity.species} rank={monsterIdentity.rank} attackStyle={attackStyle} target={(target != null ? target.name : "null")} detail={detail}",
            this);
    }

    private BattleDamageType ResolvePrimaryDamageType()
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss && activeBossAttackKind == BossAttackKind.Melee)
        {
            return BattleDamageType.Physical;
        }

        return attackStyle == MonsterAttackStyle.Melee ? BattleDamageType.Physical : BattleDamageType.Special;
    }

    private float ResolveCurrentAttackDamage(BattleDamageType damageType)
    {
        float attackPower = BattleStatUtility.ResolveAttackPower(gameObject, damageType, attackDamage);
        float damage = attackPower * Mathf.Max(0.01f, outgoingDamageMultiplier) * Mathf.Max(0f, ResolveOutgoingDamageMultiplier());
        return BattleStatUtility.ApplyCriticalDamage(gameObject, damage, out _);
    }

    private void FireProjectileAt(Transform hitTarget)
    {
        if (hitTarget == null)
        {
            return;
        }

        Vector3 spawnPosition = ResolveProjectileSpawnPosition(hitTarget);
        Vector3 targetPoint = ResolveBossProjectileTargetPoint(hitTarget);
        Vector3 direction = targetPoint - spawnPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < MovementZeroEpsilon)
        {
            direction = transform.forward;
        }

        if (debugAttackDiagnostics || debugLog)
        {
            float distanceToTarget = Vector3.Distance(transform.position, hitTarget.position);
            Debug.Log($"[BossAcidAttack] FireProjectileAt boss={name} target={hitTarget.name} distance={distanceToTarget:F2} spawnOffset={ProjectileSpawnForwardOffset:F2}", this);
        }

        BattleDamageType damageType = ResolvePrimaryDamageType();

        // 投射物出生点维持在角色前上方，避免和本体碰撞体重叠。
        GameObject projectile = CreateProjectileObject();
        if (projectile == null)
        {
            Debug.LogWarning($"[BossAcidAttack] Failed to create projectile for boss '{name}'.", this);
            return;
        }

        projectile.name = attackStyle == MonsterAttackStyle.ElementalBoss ? "Boss Acid Projectile" : "Monster Projectile";
        projectile.transform.position = spawnPosition;
        projectile.transform.localScale = Vector3.one * (attackStyle == MonsterAttackStyle.ElementalBoss ? BossProjectileScale : NormalProjectileScale);

        if (debugAttackDiagnostics || debugLog)
        {
            Debug.Log($"[BossAcidAttack] instantiated projectile={projectile.name} prefabSource={(ResolveProjectilePrefab() != null ? ResolveProjectilePrefab().name : "runtime sphere")} position={projectile.transform.position}", this);
        }

        Collider projectileCollider = projectile.GetComponent<Collider>();
        if (projectileCollider == null)
        {
            projectileCollider = projectile.AddComponent<SphereCollider>();
        }
        projectileCollider.isTrigger = true;

        Rigidbody projectileBody = projectile.GetComponent<Rigidbody>();
        if (projectileBody == null)
        {
            projectileBody = projectile.AddComponent<Rigidbody>();
        }
        projectileBody.isKinematic = true;
        projectileBody.useGravity = false;

        MonsterProjectile monsterProjectile = projectile.GetComponent<MonsterProjectile>();
        if (monsterProjectile == null)
        {
            monsterProjectile = projectile.AddComponent<MonsterProjectile>();
        }
        monsterProjectile.Launch(direction, projectileSpeed, ResolveCurrentAttackDamage(damageType), damageType, gameObject);

        if (attackStyle == MonsterAttackStyle.ElementalBoss && useArcTrajectory)
        {
            monsterProjectile.ConfigureArcTrajectory(spawnPosition, targetPoint, Mathf.Max(0.1f, arcHeight), Mathf.Max(0.1f, arcTravelTime));
        }

        Renderer renderer = projectile.GetComponentInChildren<Renderer>();
        if (renderer != null && attackStyle != MonsterAttackStyle.ElementalBoss)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            renderer.material.color = ResolveProjectileColor(damageType);
        }
    }

    private Vector3 ResolveProjectileSpawnPosition(Transform hitTarget)
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss && bossProjectileSpawnPoint != null)
        {
            return bossProjectileSpawnPoint.position;
        }

        Vector3 direction = hitTarget != null ? (hitTarget.position - transform.position) : transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < MovementZeroEpsilon)
        {
            direction = transform.forward;
        }

        if (attackStyle == MonsterAttackStyle.ElementalBoss)
        {
            return transform.position + Vector3.up * bossProjectileSpawnHeight + direction.normalized * bossProjectileSpawnForwardOffset;
        }

        return transform.position + Vector3.up * ProjectileSpawnHeightOffset + direction.normalized * ProjectileSpawnForwardOffset;
    }

    private Vector3 ResolveBossProjectileTargetPoint(Transform hitTarget)
    {
        if (hitTarget == null)
        {
            return transform.position + transform.forward * Mathf.Max(1f, bossRangedMinRange);
        }

        Vector3 predictedPosition = hitTarget.position;
        Rigidbody targetBody = hitTarget.GetComponentInParent<Rigidbody>();
        if (targetBody != null)
        {
            predictedPosition += targetBody.linearVelocity * Mathf.Max(0f, targetPredictionTime);
        }
        else
        {
            CharacterController controller = hitTarget.GetComponentInParent<CharacterController>();
            if (controller != null)
            {
                predictedPosition += controller.velocity * Mathf.Max(0f, targetPredictionTime);
            }
        }

        Collider targetCollider = ResolvePlayerCollider(hitTarget);
        if (targetCollider != null)
        {
            predictedPosition.y = targetCollider.bounds.min.y + Mathf.Min(0.2f, targetCollider.bounds.extents.y);
        }

        return predictedPosition;
    }

    private int ResolveBossFacingSign(Transform target)
    {
        if (target == null)
        {
            return 1;
        }

        return target.position.x >= transform.position.x ? 1 : -1;
    }

    private void PlayBossRangedMuzzleParticle()
    {
        if (bossRangedMuzzleParticle == null)
        {
            return;
        }

        bossRangedMuzzleParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        bossRangedMuzzleParticle.Play(true);
    }

    private GameObject CreateProjectileObject()
    {
        GameObject prefab = ResolveProjectilePrefab();
        if (prefab != null)
        {
            return Instantiate(prefab);
        }

        return GameObject.CreatePrimitive(PrimitiveType.Sphere);
    }

    private GameObject ResolveProjectilePrefab()
    {
        if (projectilePrefab != null)
        {
            return projectilePrefab;
        }

        if (cachedDefaultProjectilePrefab == null)
        {
            cachedDefaultProjectilePrefab = Resources.Load<GameObject>(DefaultProjectilePrefabResourcePath);
            if (cachedDefaultProjectilePrefab == null)
            {
                Debug.LogWarning($"[BossAcidAttack] Missing fallback projectile prefab at Resources/{DefaultProjectilePrefabResourcePath}.", this);
            }
        }

        return cachedDefaultProjectilePrefab;
    }

    private Color ResolveProjectileColor(BattleDamageType damageType)
    {
        if (attackStyle != MonsterAttackStyle.ElementalBoss)
        {
            return new Color(0.25f, 0.6f, 1f, 1f);
        }

        Color[] colors =
        {
            new Color(1f, 0.25f, 0.05f, 1f),
            new Color(0.2f, 0.65f, 1f, 1f),
            new Color(1f, 0.95f, 0.25f, 1f),
            new Color(0.55f, 1f, 0.75f, 1f),
            new Color(0.6f, 0.15f, 0.9f, 1f)
        };

        return damageType == BattleDamageType.Special ? colors[Random.Range(0, colors.Length)] : Color.white;
    }
}
