using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private Rigidbody rb;
    private Transform Player;
    private bool isChasing;
    private CombatHealth playerHealth;

    private float moveSpeed = 1f;

    private float attackRange = 1f;
    private float attackCooldown = 1f;
    private float nextAttackTime = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            Player = playerObject.transform;
            playerHealth = playerObject.GetComponent<CombatHealth>();
        }
        else
        {
            Debug.LogWarning("Player not found");
        }
    }

    // Update is called once per frame
    void Update()
    {
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