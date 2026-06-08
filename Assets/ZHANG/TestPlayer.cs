using UnityEngine;

public class TestPlayer : MonoBehaviour
{
    private float moveSpeed = 20f;
    void Start()
    {
        
    }
    void Update()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        Vector3 movement = new Vector3(moveX, 0, moveZ).normalized;

        transform.Translate(movement * moveSpeed * Time.deltaTime, Space.World);
    }
}