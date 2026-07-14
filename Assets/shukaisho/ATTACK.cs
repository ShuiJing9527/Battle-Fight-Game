using UnityEngine;

public class ATTACK : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 5f;

    [Header("攻击设置")]
    public float attackRange = 1.5f;
    public int attackDamage = 1;
    public float attackCooldown = 0.5f;
    public LayerMask enemyLayer;
    [SerializeField] private bool debugPlayerHit = false;
    [SerializeField] private Vector3 attackHitboxOffset = Vector3.zero;
    [SerializeField] private float attackForwardDistance = -1f;
    [SerializeField] private float attackRadius = -1f;

    [Header("攻击点")]
    public Transform attackPoint;

    private Rigidbody rb;
    private Animator animator;
    private CombatSkillCaster skillCaster;

    private Vector3 moveInput;
    private Vector3 lastMoveDir = Vector3.forward;

    private float nextAttackTime = 0f;
    private bool warnedMissingAttackPoint;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        skillCaster = GetComponent<CombatSkillCaster>();
    }

    void Update()
    {
        HandleInput();
        HandleAttack();
        HandleAnimation();
    }

    void FixedUpdate()
    {
        Move();
    }

    void HandleInput()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        moveInput = new Vector3(x, 0f, z).normalized;

        if (moveInput != Vector3.zero)
        {
            lastMoveDir = moveInput;
        }
    }

    void Move()
    {
        Vector3 targetPos = rb.position + moveInput * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(targetPos);
    }

    void HandleAttack()
    {
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
        {
            Attack();
            nextAttackTime = Time.time + attackCooldown;
        }
    }

    void Attack()
    {
        if (skillCaster != null && skillCaster.CastSkill(0))
        {
            return;
        }

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        Vector3 attackDirection = lastMoveDir.sqrMagnitude > 0.0001f ? lastMoveDir.normalized : Vector3.forward;
        float resolvedForwardDistance = ResolveAttackForwardDistance();
        float resolvedAttackRadius = ResolveAttackRadius();
        Vector3 attackOrigin = transform.position;
        Vector3 attackCenter = attackOrigin + attackDirection * resolvedForwardDistance + transform.TransformVector(attackHitboxOffset);

        // 攻击点放到角色面朝方向
        if (attackPoint != null)
        {
            attackPoint.position = attackCenter;
        }
        else
        {
            if (!warnedMissingAttackPoint)
            {
                warnedMissingAttackPoint = true;
                Debug.LogWarning("ATTACK requires attackPoint for normal attacks.");
            }
            return;
        }

        Collider[] hitEnemies = Physics.OverlapSphere(
            attackCenter,
            resolvedAttackRadius,
            enemyLayer,
            QueryTriggerInteraction.Collide
        );

        System.Collections.Generic.HashSet<CombatHealth> hitTargets = new System.Collections.Generic.HashSet<CombatHealth>();
        System.Collections.Generic.List<string> debugEntries = new System.Collections.Generic.List<string>();

        foreach (Collider enemy in hitEnemies)
        {
            MonsterIdentity identity = BattleTargetUtility.GetMonsterIdentity(enemy);
            if (!BattleTargetUtility.TryGetMonsterCombatHealth(enemy, transform, out CombatHealth combatHealth, out string rejectReason))
            {
                LogPlayerHitDebug(enemy, null, false, false, rejectReason, attackOrigin, attackCenter, resolvedAttackRadius, attackDirection);
                debugEntries.Add(BuildMeleeHitDebugEntry(enemy, identity, false, rejectReason, false, 0f, 0f, attackDamage, attackDamage));
                continue;
            }

            if (!hitTargets.Add(combatHealth))
            {
                LogPlayerHitDebug(enemy, combatHealth, true, false, "duplicate-combat-health", attackOrigin, attackCenter, resolvedAttackRadius, attackDirection);
                debugEntries.Add(BuildMeleeHitDebugEntry(enemy, identity, false, "duplicate-combat-health", false, 0f, 0f, attackDamage, attackDamage));
                continue;
            }

            float beforeHealth = ResolveTargetCurrentHealth(combatHealth);
            combatHealth.TakeDamage(new BattleDamage(attackDamage, BattleDamageType.Physical, gameObject));
            float afterHealth = ResolveTargetCurrentHealth(combatHealth);
            LogPlayerHitDebug(enemy, combatHealth, true, true, "None", attackOrigin, attackCenter, resolvedAttackRadius, attackDirection);
            debugEntries.Add(BuildMeleeHitDebugEntry(enemy, identity, true, "None", true, beforeHealth, afterHealth, attackDamage, attackDamage));
        }

        if (debugPlayerHit && hitEnemies.Length <= 0)
        {
            Debug.Log(
                "[PlayerHitDebug] " +
                "attack=NormalAttack " +
                "result=NoColliders " +
                "attackOrigin=" + FormatVector(attackOrigin) +
                " attackCenter=" + FormatVector(attackCenter) +
                " attackRadius=" + resolvedAttackRadius.ToString("F3") +
                " attackForwardDistance=" + resolvedForwardDistance.ToString("F3") +
                " attackHitboxOffset=" + FormatVector(attackHitboxOffset) +
                " attackBoundsMinZ=" + (attackCenter.z - resolvedAttackRadius).ToString("F3") +
                " attackBoundsMaxZ=" + (attackCenter.z + resolvedAttackRadius).ToString("F3") +
                " layerMask=" + enemyLayer.value +
                " queryTriggers=Collide " +
                "reason=no-collider-overlap",
                this);
        }

        Debug.Log(
            "[PlayerMeleeHitDebug] " +
            "skill=ATTACK " +
            "attackPosition=" + attackCenter +
            " attackRadius=" + resolvedAttackRadius.ToString("F2") +
            " attackForwardDistance=" + resolvedForwardDistance.ToString("F2") +
            " hitColliderCount=" + hitEnemies.Length +
            " details=" + (debugEntries.Count > 0 ? string.Join(" | ", debugEntries) : "none"),
            this);
    }

    void HandleAnimation()
    {
        if (animator == null) return;

        bool isMoving = moveInput.magnitude > 0.1f;

        animator.SetBool("IsMoving", isMoving);
        animator.SetFloat("MoveX", lastMoveDir.x);
        animator.SetFloat("MoveZ", lastMoveDir.z);
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, ResolveAttackRadius());
    }

    private float ResolveAttackForwardDistance()
    {
        return attackForwardDistance >= 0f ? attackForwardDistance : attackRange;
    }

    private float ResolveAttackRadius()
    {
        return attackRadius > 0f ? attackRadius : attackRange;
    }

    private static float ResolveTargetCurrentHealth(CombatHealth health)
    {
        if (health == null)
        {
            return 0f;
        }

        return health.resourceBank != null
            ? Mathf.Max(0f, health.resourceBank.currentHealth)
            : Mathf.Max(0f, health.currentHealth);
    }

    private static string BuildMeleeHitDebugEntry(
        Collider collider,
        MonsterIdentity identity,
        bool acceptedTarget,
        string rejectReason,
        bool takeDamageCalled,
        float beforeHealth,
        float afterHealth,
        float damageBeforeModifiers,
        float damageAfterModifiers)
    {
        Transform root = collider != null ? collider.transform.root : null;
        float actualDamage = Mathf.Max(0f, beforeHealth - afterHealth);

        return
            "collider=" + (collider != null ? collider.name : "null") +
            " root=" + (root != null ? root.name : "null") +
            " layer=" + (collider != null ? LayerMask.LayerToName(collider.gameObject.layer) : "null") +
            " tag=" + (collider != null ? collider.tag : "null") +
            " hasCombatHealth=" + takeDamageCalled +
            " hasMonsterIdentity=" + (identity != null) +
            " rank=" + (identity != null ? identity.rank.ToString() : "Unknown") +
            " isBoss=" + (identity != null && identity.rank == MonsterRank.Boss) +
            " acceptedTarget=" + acceptedTarget +
            " rejectReason=" + rejectReason +
            " damageBeforeModifiers=" + damageBeforeModifiers.ToString("F2") +
            " damageAfterModifiers=" + damageAfterModifiers.ToString("F2") +
            " TakeDamageCalled=" + takeDamageCalled +
            " actualDamage=" + actualDamage.ToString("F2");
    }

    private void LogPlayerHitDebug(
        Collider hitCollider,
        CombatHealth combatHealth,
        bool resolvedHealth,
        bool accepted,
        string reason,
        Vector3 attackOrigin,
        Vector3 attackCenter,
        float resolvedAttackRadius,
        Vector3 attackDirection)
    {
        if (!debugPlayerHit || hitCollider == null)
        {
            return;
        }

        Vector3 colliderClosestPoint = hitCollider.ClosestPoint(attackOrigin);
        Vector3 targetRootPosition = combatHealth != null
            ? combatHealth.transform.position
            : (hitCollider.transform.root != null ? hitCollider.transform.root.position : hitCollider.transform.position);
        float distanceToCollider = Vector3.Distance(attackOrigin, colliderClosestPoint);
        float distanceToTargetCenter = Vector3.Distance(attackOrigin, targetRootPosition);
        float angleToCollider = ResolveAngle(attackDirection, colliderClosestPoint - attackOrigin);
        float angleToTargetCenter = ResolveAngle(attackDirection, targetRootPosition - attackOrigin);
        Bounds colliderBounds = hitCollider.bounds;

        Debug.Log(
            "[PlayerHitDebug] " +
            "attack=NormalAttack " +
            "colliderPath=" + GetHierarchyPath(hitCollider.transform) +
            " isTrigger=" + hitCollider.isTrigger +
            " colliderClosestPoint=" + FormatVector(colliderClosestPoint) +
            " attackOrigin=" + FormatVector(attackOrigin) +
            " attackCenter=" + FormatVector(attackCenter) +
            " targetRootPosition=" + FormatVector(targetRootPosition) +
            " distanceToCollider=" + distanceToCollider.ToString("F3") +
            " distanceToTargetCenter=" + distanceToTargetCenter.ToString("F3") +
            " angleToCollider=" + angleToCollider.ToString("F2") +
            " angleToTargetCenter=" + angleToTargetCenter.ToString("F2") +
            " zDifference=" + Mathf.Abs(attackOrigin.z - colliderClosestPoint.z).ToString("F3") +
            " attackRadius=" + resolvedAttackRadius.ToString("F3") +
            " attackBoundsMinZ=" + (attackCenter.z - resolvedAttackRadius).ToString("F3") +
            " attackBoundsMaxZ=" + (attackCenter.z + resolvedAttackRadius).ToString("F3") +
            " colliderBoundsMinZ=" + colliderBounds.min.z.ToString("F3") +
            " colliderBoundsMaxZ=" + colliderBounds.max.z.ToString("F3") +
            " layerMask=" + enemyLayer.value +
            " resolvedHealth=" + resolvedHealth +
            " accepted=" + accepted +
            " reason=" + reason,
            this);
    }

    private static float ResolveAngle(Vector3 from, Vector3 to)
    {
        Vector3 flatFrom = new Vector3(from.x, 0f, from.z);
        Vector3 flatTo = new Vector3(to.x, 0f, to.z);
        if (flatFrom.sqrMagnitude <= 0.0001f || flatTo.sqrMagnitude <= 0.0001f)
        {
            return 0f;
        }

        return Vector3.Angle(flatFrom, flatTo);
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "null";
        }

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static string FormatVector(Vector3 value)
    {
        return "(" + value.x.ToString("F3") + "," + value.y.ToString("F3") + "," + value.z.ToString("F3") + ")";
    }
}
