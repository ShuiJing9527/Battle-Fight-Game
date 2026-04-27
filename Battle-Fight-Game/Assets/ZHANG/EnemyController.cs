using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private Rigidbody rb;
    public Transform Player;
    private bool isChasing;

    private float moveSpeed = 20f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
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
        }
    }
}