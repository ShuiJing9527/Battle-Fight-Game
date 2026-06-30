using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private const float MovementZeroEpsilon = 0.001f;
    private const float AttackRecoveryDurationSeconds = 0.7f;
    private const float ProjectileSpawnHeightOffset = 0.8f;
    private const float ProjectileSpawnForwardOffset = 0.5f;
    private const float BossProjectileScale = 0.45f;
    private const float NormalProjectileScale = 0.28f;

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

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool debugMeleeHitCheck = false;
    [SerializeField] private bool debugAttackDiagnostics = false;
    [SerializeField] private bool debugSpeedDiagnostics = false;
    [SerializeField] private bool debugChaseDiagnostics = true;
    [SerializeField, Min(0.1f)] private float debugAttackLogInterval = 0.3f;
    [SerializeField, Min(0.1f)] private float debugSpeedLogInterval = 1f;
    [SerializeField, Min(0.05f)] private float targetResolveRetryInterval = 0.25f;

    private Rigidbody rb;
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
    private Collider[] separationHits;
    private float lastAttackTime = -1f;

    public float BaseMoveSpeed => moveSpeed;

    private void Start()
    {
        MonsterCombatAutoSetup.Configure(gameObject);
        rb = GetComponent<Rigidbody>();
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
        float distance = Vector3.Distance(playerTarget.position, transform.position);
        float horizontalDistance = new Vector2(toPlayer.x, toPlayer.z).magnitude;
        float verticalDifference = Mathf.Abs(playerTarget.position.y - transform.position.y);
        float statsSpeed = combatStats != null ? Mathf.Max(0f, combatStats.speed) : 0f;
        bool grounded = IsGrounded();
        string attackFailReason = EvaluateAttackFailReason(horizontalDistance, verticalDifference, grounded);
        bool canAttack = string.IsNullOrEmpty(attackFailReason);
        LogAttackDiagnostics(
            statsSpeed,
            distance,
            horizontalDistance,
            verticalDifference,
            grounded,
            canAttack,
            attackFailReason);

        // 进入攻击范围后，优先切到攻击流程，避免追击和出手同时发生。
        if (canAttack)
        {
            LogChaseDiagnostics(horizontalDistance, false, true, false, false, false, "Attack", 0f, playerTarget != null ? playerTarget.name : "null", "InAttackRange");
            BeginAttack();
            return;
        }

        // 攻击动作进行中时，原地停住并保持当前朝向，等待攻击回调结算。
        if (attackInProgress)
        {
            rb.linearVelocity = Vector3.zero;
            LogChaseDiagnostics(horizontalDistance, false, false, false, false, false, "AttackRecovery", 0f, playerTarget != null ? playerTarget.name : "null", "AttackInProgress");
            if (keepFlatRotation)
            {
                transform.rotation = initialRotation;
            }
            return;
        }

        // 追击到停止距离内就停下，避免贴脸抖动和过冲。
        if (distance <= stopDistance || distance < MovementZeroEpsilon)
        {
            rb.linearVelocity = Vector3.zero;
            StopMoveAnimation();
            LogChaseDiagnostics(horizontalDistance, false, false, false, false, false, "HoldPosition", 0f, playerTarget != null ? playerTarget.name : "null", "StopDistance");
            if (keepFlatRotation)
            {
                transform.rotation = initialRotation;
            }
            return;
        }

        Vector3 direction = toPlayer / distance;
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
        float speedMoveMultiplier = BattleStatUtility.GetSpeedMoveMultiplier(combatStats);
        float externalMoveMultiplier = ResolveExternalMoveMultiplier();
        float currentMoveSpeed = BattleStatUtility.ResolveMoveSpeed(combatStats, baseMoveSpeed, externalMoveMultiplier);
        if (maxHorizontalMoveSpeed > 0f)
        {
            currentMoveSpeed = Mathf.Min(currentMoveSpeed, maxHorizontalMoveSpeed);
        }
        if (debugLog && Mathf.Abs(externalMoveMultiplier - lastLoggedMoveMultiplier) > 0.001f)
        {
            Debug.Log($"[EnemyController] finalMoveSpeed={currentMoveSpeed:F2} multiplier={externalMoveMultiplier:F2}", this);
            lastLoggedMoveMultiplier = externalMoveMultiplier;
        }

        LogSpeedDiagnostics(statsSpeed, distance, canAttack);
        LogChaseDiagnostics(
            horizontalDistance,
            currentMoveSpeed > MovementZeroEpsilon,
            false,
            false,
            false,
            externalMoveMultiplier <= 0f,
            "Chase",
            currentMoveSpeed,
            playerTarget != null ? playerTarget.name : "null",
            string.IsNullOrEmpty(attackFailReason) ? "Chase" : attackFailReason);

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
        lastAttackTime = Time.time;
        nextAttackTime = Time.time + ResolveCurrentAttackCooldown();
        pendingAttackTarget = playerTarget;
        attackInProgress = true;
        rb.linearVelocity = Vector3.zero;
        StopMoveAnimation();
        if (debugAttackDiagnostics)
        {
            Debug.Log($"[EnemyAttack] StartAttack enemy={name} target={(pendingAttackTarget != null ? pendingAttackTarget.name : "null")}", this);
        }

        // 触发攻击动画后，按动画时序进入冷却恢复阶段。
        if (slimeAnimation != null)
        {
            slimeAnimation.PlayAttack(pendingAttackTarget);
            CancelInvoke(nameof(FinishAttackRecovery));
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
        attackInProgress = false;
        pendingAttackTarget = null;
        if (debugAttackDiagnostics)
        {
            Debug.Log($"[EnemyAttack] AttackFinished enemy={name}", this);
        }
    }

    private void HandleAttackHit(Transform target)
    {
        Transform hitTarget = target != null ? target : pendingAttackTarget;
        if (hitTarget == null)
        {
            if (debugAttackDiagnostics)
            {
                Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target=null damage=0 reason=NoTarget", this);
            }
            FinishAttackRecovery();
            return;
        }

        Vector3 toTarget = hitTarget.position - transform.position;
        toTarget.y = 0f;
        if (UsesProjectileAttack())
        {
            if (toTarget.sqrMagnitude > attackHitRange * attackHitRange)
            {
                if (debugAttackDiagnostics)
                {
                    Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target={hitTarget.name} damage=0 reason=TooFarDistance", this);
                }
                FinishAttackRecovery();
                return;
            }

            ExecuteProjectileAttack(hitTarget);
        }
        else
        {
            if (!CanHitMeleeTarget(hitTarget))
            {
                if (debugAttackDiagnostics)
                {
                    Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target={hitTarget.name} damage=0 reason=HitCheckFailed", this);
                }
                FinishAttackRecovery();
                return;
            }

            if (!BattleTargetUtility.IsPlayer(hitTarget.gameObject))
            {
                if (debugAttackDiagnostics)
                {
                    Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target={hitTarget.name} damage=0 reason=NotPlayer", this);
                }
                FinishAttackRecovery();
                return;
            }

            CombatHealth combatHealth = hitTarget.GetComponentInParent<CombatHealth>();
            if (combatHealth != null)
            {
                BattleDamageType damageType = ResolvePrimaryDamageType();
                float currentAttackDamage = ResolveCurrentAttackDamage(damageType);
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
                combatHealth.TakeDamage(new BattleDamage(currentAttackDamage, damageType, gameObject));
            }
            else if (debugAttackDiagnostics)
            {
                Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target={hitTarget.name} damage=0 reason=NoCombatHealth", this);
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

        if (requireGroundedToAttack && !IsGrounded())
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
        return attackStyle == MonsterAttackStyle.Ranged || attackStyle == MonsterAttackStyle.ElementalBoss;
    }

    private void ExecuteProjectileAttack(Transform hitTarget)
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss)
        {
            // Boss multi-skill expansion point: keep projectile as the phase-one fallback
            // until fire ring / ground spike attacks are introduced.
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
        Debug.Log(
            $"[EnemyChaseDiag] enemy={name} distanceToTarget={distanceToTarget:F2} canMove={canMove} canAttack={canAttack} isKnockback={isKnockback} isStunned={isStunned} isBlocked={isBlocked} currentState={currentState} moveSpeed={moveSpeed:F2} target={targetName} reason={reason}",
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

    private bool IsGrounded()
    {
        float probeDistance = Mathf.Max(0.01f, groundedProbeDistance);
        ResolveMeleeHitSources();

        Collider sourceCollider = meleeEnemyCollider != null ? meleeEnemyCollider : GetComponent<Collider>();
        if (sourceCollider != null)
        {
            Bounds bounds = sourceCollider.bounds;
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
            float radius = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.45f, 0.05f, 0.45f);
            float castDistance = probeDistance + 0.15f;
            if (Physics.SphereCast(origin, radius, Vector3.down, out _, castDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return Physics.Raycast(origin + Vector3.up * 0.02f, Vector3.down, castDistance, ~0, QueryTriggerInteraction.Ignore);
        }

        Vector3 fallbackOrigin = transform.position + Vector3.up * 0.05f;
        return Physics.Raycast(fallbackOrigin, Vector3.down, probeDistance + 0.05f, ~0, QueryTriggerInteraction.Ignore);
    }

    private BattleDamageType ResolvePrimaryDamageType()
    {
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

        Vector3 direction = hitTarget.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < MovementZeroEpsilon)
        {
            direction = transform.forward;
        }

        BattleDamageType damageType = ResolvePrimaryDamageType();

        // 投射物出生点维持在角色前上方，避免和本体碰撞体重叠。
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = attackStyle == MonsterAttackStyle.ElementalBoss ? "Boss Element Projectile" : "Monster Projectile";
        projectile.transform.position = transform.position + Vector3.up * ProjectileSpawnHeightOffset + direction.normalized * ProjectileSpawnForwardOffset;
        projectile.transform.localScale = Vector3.one * (attackStyle == MonsterAttackStyle.ElementalBoss ? BossProjectileScale : NormalProjectileScale);

        Collider projectileCollider = projectile.GetComponent<Collider>();
        projectileCollider.isTrigger = true;

        Rigidbody projectileBody = projectile.AddComponent<Rigidbody>();
        projectileBody.isKinematic = true;

        MonsterProjectile monsterProjectile = projectile.AddComponent<MonsterProjectile>();
        monsterProjectile.Launch(direction, projectileSpeed, ResolveCurrentAttackDamage(damageType), damageType, gameObject);

        Renderer renderer = projectile.GetComponent<Renderer>();
        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        renderer.material.color = ResolveProjectileColor(damageType);
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
