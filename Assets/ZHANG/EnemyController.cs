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
    private Player2Bootstrap playerBootstrap;
    private SlimeAnimationController slimeAnimation;
    private EnemyDebuffReceiver debuffReceiver;
    private Quaternion initialRotation;
    private float nextAttackTime;
    private Transform pendingAttackTarget;
    private bool attackInProgress;
    private float lastLoggedMoveMultiplier = -1f;
    private float lastLoggedAttackMultiplier = -1f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        slimeAnimation = GetComponent<SlimeAnimationController>();
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
        float moveMultiplier = ResolveMoveSpeedMultiplier();
        float currentMoveSpeed = moveSpeed * moveMultiplier;
        if (Mathf.Abs(moveMultiplier - lastLoggedMoveMultiplier) > 0.001f)
        {
            Debug.Log($"[EnemyController] finalMoveSpeed={currentMoveSpeed:F2} multiplier={moveMultiplier:F2}", this);
            lastLoggedMoveMultiplier = moveMultiplier;
        }

        rb.linearVelocity = new Vector3(direction.x * currentMoveSpeed, rb.linearVelocity.y, direction.z * currentMoveSpeed);
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
            float attackMultiplier = ResolveAttackMultiplier();
            float currentAttackDamage = attackDamage * attackMultiplier;
            if (Mathf.Abs(attackMultiplier - lastLoggedAttackMultiplier) > 0.001f)
            {
                Debug.Log($"[EnemyController] finalAttackDamage={currentAttackDamage:F2} multiplier={attackMultiplier:F2}", this);
                lastLoggedAttackMultiplier = attackMultiplier;
            }

            combatHealth.TakeDamage(new BattleDamage(currentAttackDamage, BattleDamageType.Physical, gameObject));
        }

        FinishAttackRecovery();
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

    private EnemyDebuffReceiver ResolveDebuffReceiver()
    {
        if (debuffReceiver == null)
        {
            debuffReceiver = GetComponent<EnemyDebuffReceiver>();
        }

        return debuffReceiver;
    }

    private float ResolveMoveSpeedMultiplier()
    {
        EnemyDebuffReceiver receiver = ResolveDebuffReceiver();
        return receiver != null ? receiver.GetMoveSpeedMultiplier() : 1f;
    }

    private float ResolveAttackMultiplier()
    {
        EnemyDebuffReceiver receiver = ResolveDebuffReceiver();
        return receiver != null ? receiver.GetAttackMultiplier() : 1f;
    }
}
