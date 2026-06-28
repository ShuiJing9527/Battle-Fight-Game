using UnityEngine;

public class Player01NeedleProjectile : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float lifeTime = 2f;

    public float Damage { get; private set; }

    private Vector3 moveDirection = Vector3.right;
    private float moveSpeed = 12f;
    private float spawnTime;
    private Rigidbody cachedRigidbody;
    private GameObject source;
    private float healPercentOfDamage;
    private LayerMask targetLayers = ~0;
    private bool hasHit;
    private int skillSlotIndex = -1;
    private int runeCastId = -1;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        spawnTime = Time.time;
    }

    private void Update()
    {
        if (cachedRigidbody == null)
        {
            transform.position += moveDirection * moveSpeed * Time.deltaTime;
        }

        if (Time.time - spawnTime >= lifeTime)
        {
            Destroy(gameObject);
        }
    }

    public void Launch(Vector3 direction, float speed, float damage)
    {
        Launch(direction, speed, damage, null, 0f, ~0);
    }

    public void Launch(Vector3 direction, float speed, float damage, GameObject source, float healPercentOfDamage, LayerMask targetLayers)
    {
        Launch(direction, speed, damage, source, healPercentOfDamage, targetLayers, -1, -1);
    }

    public void Launch(Vector3 direction, float speed, float damage, GameObject source, float healPercentOfDamage, LayerMask targetLayers, int skillSlotIndex, int runeCastId)
    {
        moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
        moveSpeed = Mathf.Max(0f, speed);
        Damage = Mathf.Max(0f, damage);
        this.source = source;
        this.healPercentOfDamage = Mathf.Clamp01(healPercentOfDamage);
        this.targetLayers = targetLayers;
        this.skillSlotIndex = skillSlotIndex;
        this.runeCastId = runeCastId;
        spawnTime = Time.time;
        transform.rotation = Quaternion.FromToRotation(Vector3.right, moveDirection);

        if (cachedRigidbody == null)
        {
            cachedRigidbody = GetComponent<Rigidbody>();
        }

        if (cachedRigidbody != null)
        {
            cachedRigidbody.linearVelocity = moveDirection * moveSpeed;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryHit(collision.collider);
    }

    private void TryHit(Collider other)
    {
        if (hasHit || other == null)
        {
            return;
        }

        if (((1 << other.gameObject.layer) & targetLayers.value) == 0)
        {
            return;
        }

        Transform sourceTransform = source != null ? source.transform : null;
        if (!BattleTargetUtility.IsMonster(other, sourceTransform))
        {
            return;
        }

        float resolvedDamage = Damage + ConsumeRuneFirstHitBonusDamage();
        float dealtDamage = resolvedDamage;
        CombatHealth combatHealth = BattleTargetUtility.GetMonsterCombatHealth(other, sourceTransform);
        if (combatHealth != null)
        {
            float beforeHealth = ResolveCurrentHealth(combatHealth);
            combatHealth.TakeDamage(new BattleDamage(resolvedDamage, BattleDamageType.Special, source));
            float actualDamage = Mathf.Max(0f, beforeHealth - ResolveCurrentHealth(combatHealth));
            ResolveRuneRuntimeState()?.NotifyMonsterDamagedBySkill(skillSlotIndex, combatHealth, actualDamage);
            dealtDamage = actualDamage > 0f ? actualDamage : resolvedDamage;
        }
        else
        {
            EnemyHealth enemyHealth = BattleTargetUtility.GetMonsterLegacyHealth(other, sourceTransform);
            if (enemyHealth == null)
            {
                return;
            }

            enemyHealth.TakeDamage(Mathf.RoundToInt(resolvedDamage), source);
        }

        hasHit = true;
        HealSource(dealtDamage * healPercentOfDamage);
        Destroy(gameObject);
    }

    private float ConsumeRuneFirstHitBonusDamage()
    {
        RuneRuntimeState runtimeState = ResolveRuneRuntimeState();
        return runtimeState != null ? runtimeState.ConsumeFirstHitBonusDamage(skillSlotIndex, runeCastId) : 0f;
    }

    private RuneRuntimeState ResolveRuneRuntimeState()
    {
        if (source == null)
        {
            return null;
        }

        RuneRuntimeState runtimeState = source.GetComponent<RuneRuntimeState>();
        if (runtimeState != null)
        {
            return runtimeState;
        }

        return source.GetComponentInParent<RuneRuntimeState>();
    }

    private float ResolveCurrentHealth(CombatHealth health)
    {
        if (health == null)
        {
            return 0f;
        }

        return health.resourceBank != null
            ? Mathf.Max(0f, health.resourceBank.currentHealth)
            : Mathf.Max(0f, health.currentHealth);
    }

    private void HealSource(float amount)
    {
        if (source == null || amount <= 0f)
        {
            return;
        }

        CombatHealth sourceHealth = source.GetComponent<CombatHealth>();
        if (sourceHealth != null)
        {
            sourceHealth.Heal(amount);
            return;
        }

        BattleResourceBank bank = source.GetComponent<BattleResourceBank>();
        if (bank != null)
        {
            bank.Heal(amount);
        }
    }
}
