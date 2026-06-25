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
    [SerializeField] private bool faceMoveDirection = false;
    [SerializeField] private bool keepFlatRotation = true;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.35f;
    [SerializeField] private float attackHitRange = 1.6f;
    [SerializeField] private float attackCooldown = 1.1f;
    [SerializeField] private float attackDamage = 3f;
    [SerializeField] private MonsterAttackStyle attackStyle = MonsterAttackStyle.Melee;
    [SerializeField] private float projectileSpeed = 8f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

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
        MonsterCombatAutoSetup.Configure(gameObject);
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

        // 进入攻击范围后，优先切到攻击流程，避免追击和出手同时发生。
        if (distance <= attackRange && Time.time >= nextAttackTime)
        {
            BeginAttack();
            return;
        }

        // 攻击动作进行中时，原地停住并保持当前朝向，等待攻击回调结算。
        if (attackInProgress)
        {
            rb.linearVelocity = Vector3.zero;
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
            if (keepFlatRotation)
            {
                transform.rotation = initialRotation;
            }
            return;
        }

        Vector3 direction = toPlayer / distance;
        float moveMultiplier = ResolveMoveSpeedMultiplier();
        float currentMoveSpeed = moveSpeed * moveMultiplier;
        if (debugLog && Mathf.Abs(moveMultiplier - lastLoggedMoveMultiplier) > 0.001f)
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

    public void ConfigureRuntime(float moveSpeed, float stopDistance, float attackRange, float attackHitRange, float attackCooldown, float attackDamage, MonsterAttackStyle attackStyle)
    {
        this.moveSpeed = Mathf.Max(0f, moveSpeed);
        this.stopDistance = Mathf.Max(0f, stopDistance);
        this.attackRange = Mathf.Max(0.1f, attackRange);
        this.attackHitRange = Mathf.Max(0.1f, attackHitRange);
        this.attackCooldown = Mathf.Max(0.1f, attackCooldown);
        this.attackDamage = Mathf.Max(0f, attackDamage);
        this.attackStyle = attackStyle;
    }

    private void BeginAttack()
    {
        nextAttackTime = Time.time + Mathf.Max(0.1f, attackCooldown);
        pendingAttackTarget = playerTarget;
        attackInProgress = true;
        rb.linearVelocity = Vector3.zero;
        StopMoveAnimation();

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

        if (attackStyle == MonsterAttackStyle.Ranged || attackStyle == MonsterAttackStyle.ElementalBoss)
        {
            FireProjectileAt(hitTarget);
        }
        else
        {
            if (!BattleTargetUtility.IsPlayer(hitTarget.gameObject))
            {
                FinishAttackRecovery();
                return;
            }

            CombatHealth combatHealth = hitTarget.GetComponentInParent<CombatHealth>();
            if (combatHealth != null)
            {
                float currentAttackDamage = ResolveCurrentAttackDamage();
                if (debugLog && Mathf.Abs(ResolveAttackMultiplier() - lastLoggedAttackMultiplier) > 0.001f)
                {
                    Debug.Log($"[EnemyController] finalAttackDamage={currentAttackDamage:F2} multiplier={ResolveAttackMultiplier():F2}", this);
                    lastLoggedAttackMultiplier = ResolveAttackMultiplier();
                }

                combatHealth.TakeDamage(new BattleDamage(currentAttackDamage, BattleDamageType.Physical, gameObject));
            }
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

    private float ResolveCurrentAttackDamage()
    {
        return attackDamage * ResolveAttackMultiplier();
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

        BattleDamageType damageType = attackStyle == MonsterAttackStyle.ElementalBoss && Random.value > 0.5f
            ? BattleDamageType.Special
            : BattleDamageType.Physical;

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
        monsterProjectile.Launch(direction, projectileSpeed, ResolveCurrentAttackDamage(), damageType, gameObject);

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
