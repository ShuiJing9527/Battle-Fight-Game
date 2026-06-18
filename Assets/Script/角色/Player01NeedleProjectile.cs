using UnityEngine;

public class Player01NeedleProjectile : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float lifeTime = 2f;

    public float Damage { get; private set; }

    private Vector3 moveDirection = Vector3.right;
    private float moveSpeed = 12f;
    private float spawnTime;
    private Rigidbody cachedRigidbody;

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
        moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
        moveSpeed = Mathf.Max(0f, speed);
        Damage = Mathf.Max(0f, damage);
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
}
