using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private string playerTag = "Player";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float stopDistance = 0.8f;
    [SerializeField] private bool faceMoveDirection = false;
    [SerializeField] private bool keepFlatRotation = true;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.35f;
    [SerializeField] private float attackHitRange = 1.6f;
    [SerializeField] private float attackCooldown = 1.1f;
    [SerializeField] private float attackDamage = 3f;

    private Rigidbody rb;
<<<<<<< HEAD
    private Player2Bootstrap playerBootstrap;
    private SlimeAnimationController slimeAnimation;
    private Quaternion initialRotation;
    private float nextAttackTime;
    private Transform pendingAttackTarget;
    private bool attackInProgress;

    private void Start()
=======
    private Transform Player;
    private bool isChasing;
    private CombatHealth playerHealth;

    private float moveSpeed = 1f;

    private float attackRange = 1f;
    private float attackCooldown = 1f;
    private float nextAttackTime = 0f;

    void Start()
>>>>>>> 5e39f29b2b0f9d828bf63fb9bb31e264d53dd8d6
    {
        rb = GetComponent<Rigidbody>();
        slimeAnimation = GetComponent<SlimeAnimationController>();
        initialRotation = transform.rotation;
        ResolvePlayerTarget();

        if (slimeAnimation != null)
        {
<<<<<<< HEAD
            slimeAnimation.OnAttackHit += HandleAttackHit;
        }
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
            return;
        }

        Vector3 toPlayer = playerTarget.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        if (distance <= attackRange && Time.time >= nextAttackTime)
        {
            BeginAttack();
            return;
        }

        if (attackInProgress)
        {
            rb.linearVelocity = Vector3.zero;
            if (keepFlatRotation)
            {
                transform.rotation = initialRotation;
            }
            return;
        }

        if (distance <= stopDistance || distance < 0.001f)
        {
            rb.linearVelocity = Vector3.zero;
            StopMoveAnimation();
            if (keepFlatRotation)
            {
                transform.rotation = initialRotation;
            }
            return;
        }

        Vector3 direction = toPlayer / distance;
        rb.linearVelocity = new Vector3(direction.x * moveSpeed, rb.linearVelocity.y, direction.z * moveSpeed);
        PlayMoveAnimation(direction);

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
        playerTarget = target;
    }

    private void BeginAttack()
    {
        nextAttackTime = Time.time + Mathf.Max(0.1f, attackCooldown);
        pendingAttackTarget = playerTarget;
        attackInProgress = true;
        rb.linearVelocity = Vector3.zero;
        StopMoveAnimation();

        if (slimeAnimation != null)
        {
            slimeAnimation.PlayAttack(pendingAttackTarget);
            CancelInvoke(nameof(FinishAttackRecovery));
            Invoke(nameof(FinishAttackRecovery), 0.7f);
=======
            Player = playerObject.transform;
            playerHealth = playerObject.GetComponent<CombatHealth>();
>>>>>>> 5e39f29b2b0f9d828bf63fb9bb31e264d53dd8d6
        }
        else
        {
            HandleAttackHit(pendingAttackTarget);
            FinishAttackRecovery();
        }
    }

    private void FinishAttackRecovery()
    {
<<<<<<< HEAD
        attackInProgress = false;
        pendingAttackTarget = null;
    }

    private void HandleAttackHit(Transform target)
    {
        Transform hitTarget = target != null ? target : pendingAttackTarget;
        if (hitTarget == null)
        {
            FinishAttackRecovery();
            return;
        }

        Vector3 toTarget = hitTarget.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > attackHitRange * attackHitRange)
        {
            FinishAttackRecovery();
            return;
        }

        CombatHealth combatHealth = hitTarget.GetComponentInParent<CombatHealth>();
        if (combatHealth != null)
        {
            combatHealth.TakeDamage(new BattleDamage(attackDamage, BattleDamageType.Physical, gameObject));
        }

        FinishAttackRecovery();
    }

    private void PlayMoveAnimation(Vector3 direction)
    {
        if (slimeAnimation == null)
        {
            return;
        }

        slimeAnimation.PlayMoveAnimation(new Vector2(direction.x, direction.z), moveSpeed);
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
        if (playerBootstrap == null)
        {
            playerBootstrap = FindObjectOfType<Player2Bootstrap>();
        }

        if (playerBootstrap != null && playerBootstrap.CurrentPlayerTransform != null)
        {
            playerTarget = playerBootstrap.CurrentPlayerTransform;
            return;
        }

        if (playerTarget != null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject playerObject = GameObject.FindWithTag(playerTag);
            if (playerObject != null)
            {
                playerTarget = playerObject.transform;
            }
        }
    }
}
=======
        if (isChasing && Player != null)
        {
            Vector3 direction = (Player.position - transform.position).normalized;
            rb.linearVelocity = direction * moveSpeed;

            float distanceToPlayer = Vector3.Distance(transform.position, Player.position);
            if (distanceToPlayer <= attackRange && Time.time >= nextAttackTime)
            {
                enemyAttack();
                nextAttackTime = Time.time + attackCooldown;
            }
        }
        else
        {
            isChasing = false;
            rb.linearVelocity = Vector3.zero;
        }
    }
    void enemyAttack()
    {
        playerHealth.TakeDamage(1);
    }

    //chase range
    private void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            isChasing = true;   
        }
    }
    private void OnTriggerExit(Collider collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            isChasing = false;   
            rb.linearVelocity = Vector3.zero;
        }
    }
}
>>>>>>> 5e39f29b2b0f9d828bf63fb9bb31e264d53dd8d6
