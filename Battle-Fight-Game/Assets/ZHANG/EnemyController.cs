using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private Rigidbody rb;
    private Transform Player;
    private bool isChasing;

    private float moveSpeed = 10f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            Player = playerObject.transform;
        }
        else
        {
            Debug.LogWarning("Player not found");
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (isChasing)
        {
            Vector3 direction = (Player.position - transform.position).normalized;
            rb.linearVelocity = direction * moveSpeed;            
        }
    }
    private void OnTriggerEnter(Collider collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            isChasing = true;   
        }
    }
    private void OnTriggerExit(Collider collision)
    {
        if (collision.gameObject.tag == "Player")
        {
            isChasing = false;   
            rb.linearVelocity = Vector3.zero;
        }
    }
}