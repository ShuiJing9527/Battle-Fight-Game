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

        // 攻击点放到角色面朝方向
        if (attackPoint != null)
        {
            attackPoint.localPosition = lastMoveDir.normalized * attackRange;
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
            attackPoint.position,
            attackRange,
            enemyLayer
        );

        System.Collections.Generic.HashSet<CombatHealth> hitTargets = new System.Collections.Generic.HashSet<CombatHealth>();
        System.Collections.Generic.List<string> debugEntries = new System.Collections.Generic.List<string>();

        foreach (Collider enemy in hitEnemies)
        {
            MonsterIdentity identity = BattleTargetUtility.GetMonsterIdentity(enemy);
            if (!BattleTargetUtility.TryGetMonsterCombatHealth(enemy, transform, out CombatHealth combatHealth, out string rejectReason))
            {
                debugEntries.Add(BuildMeleeHitDebugEntry(enemy, identity, false, rejectReason, false, 0f, 0f, attackDamage, attackDamage));
                continue;
            }

            if (!hitTargets.Add(combatHealth))
            {
                debugEntries.Add(BuildMeleeHitDebugEntry(enemy, identity, false, "duplicate-combat-health", false, 0f, 0f, attackDamage, attackDamage));
                continue;
            }

            float beforeHealth = ResolveTargetCurrentHealth(combatHealth);
            combatHealth.TakeDamage(new BattleDamage(attackDamage, BattleDamageType.Physical, gameObject));
            float afterHealth = ResolveTargetCurrentHealth(combatHealth);
            debugEntries.Add(BuildMeleeHitDebugEntry(enemy, identity, true, "None", true, beforeHealth, afterHealth, attackDamage, attackDamage));
        }

        Debug.Log(
            "[PlayerMeleeHitDebug] " +
            "skill=ATTACK " +
            "attackPosition=" + attackPoint.position +
            " attackRadius=" + attackRange.ToString("F2") +
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
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
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
}
