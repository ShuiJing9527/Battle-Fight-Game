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
        moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
        moveSpeed = Mathf.Max(0f, speed);
        Damage = Mathf.Max(0f, damage);
        this.source = source;
        this.healPercentOfDamage = Mathf.Clamp01(healPercentOfDamage);
        this.targetLayers = targetLayers;
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

        float dealtDamage = Damage;
        CombatHealth combatHealth = BattleTargetUtility.GetMonsterCombatHealth(other, sourceTransform);
        if (combatHealth != null)
        {
            combatHealth.TakeDamage(new BattleDamage(Damage, BattleDamageType.Special, source));
        }
        else
        {
            EnemyHealth enemyHealth = BattleTargetUtility.GetMonsterLegacyHealth(other, sourceTransform);
            if (enemyHealth == null)
            {
                return;
            }

            enemyHealth.TakeDamage(Mathf.RoundToInt(Damage), source);
        }

        hasHit = true;
        HealSource(dealtDamage * healPercentOfDamage);
        Destroy(gameObject);
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
