using UnityEngine;

public class MonsterProjectile : MonoBehaviour
{
    public float damage = 8f;
    public float speed = 8f;
    public float lifetime = 4f;
    public BattleDamageType damageType = BattleDamageType.Physical;

    private Vector3 direction;
    private GameObject source;
    private float spawnTime;
    private bool hasHit;

    public void Launch(Vector3 direction, float speed, float damage, BattleDamageType damageType, GameObject source)
    {
        this.direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        this.speed = Mathf.Max(0f, speed);
        this.damage = Mathf.Max(0f, damage);
        this.damageType = damageType;
        this.source = source;
        spawnTime = Time.time;
        hasHit = false;
    }

    private void Update()
    {
        transform.position += direction * speed * Time.deltaTime;
        if (Time.time - spawnTime >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit || ShouldIgnoreCollision(other))
        {
            return;
        }

        CombatHealth playerHealth = ResolvePlayerCombatHealth(other);
        if (playerHealth != null)
        {
            hasHit = true;
            playerHealth.TakeDamage(new BattleDamage(damage, damageType, source));
            Destroy(gameObject);
            return;
        }

        hasHit = true;
        Destroy(gameObject);
    }

    private bool ShouldIgnoreCollision(Collider other)
    {
        if (other == null)
        {
            return true;
        }

        if (other.transform == transform || other.transform.IsChildOf(transform))
        {
            return true;
        }

        if (source != null && other.transform.IsChildOf(source.transform))
        {
            return true;
        }

        return false;
    }

    private static CombatHealth ResolvePlayerCombatHealth(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        GameObject target = other.gameObject;
        if (!BattleTargetUtility.IsPlayer(target))
        {
            Transform root = other.transform.root;
            if (root == null || !BattleTargetUtility.IsPlayer(root.gameObject))
            {
                return null;
            }
        }

        return other.GetComponentInParent<CombatHealth>();
    }
}
