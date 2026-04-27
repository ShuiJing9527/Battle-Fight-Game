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

    private Vector3 moveInput;
    private Vector3 lastMoveDir = Vector3.forward;

    private float nextAttackTime = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
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
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        // 攻击点放到角色面朝方向
        if (attackPoint != null)
        {
            attackPoint.localPosition = lastMoveDir.normalized * attackRange;
        }

        Collider[] hitEnemies = Physics.OverlapSphere(
            attackPoint.position,
            attackRange,
            enemyLayer
        );

        foreach (Collider enemy in hitEnemies)
        {
            EnemyHealth hp = enemy.GetComponent<EnemyHealth>();

            if (hp != null)
            {
                hp.TakeDamage(attackDamage);
            }
        }
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
}