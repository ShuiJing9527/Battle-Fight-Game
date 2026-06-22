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

    public void Launch(Vector3 direction, float speed, float damage, BattleDamageType damageType, GameObject source)
    {
        this.direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        this.speed = Mathf.Max(0f, speed);
        this.damage = Mathf.Max(0f, damage);
        this.damageType = damageType;
        this.source = source;
        spawnTime = Time.time;
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
        if (other == null || (source != null && other.transform.IsChildOf(source.transform)))
        {
            return;
        }

        if (!BattleTargetUtility.IsPlayer(other.gameObject))
        {
            return;
        }

        CombatHealth health = other.GetComponentInParent<CombatHealth>();
        if (health == null)
        {
            return;
        }

        health.TakeDamage(new BattleDamage(damage, damageType, source));
        Destroy(gameObject);
    }
}
