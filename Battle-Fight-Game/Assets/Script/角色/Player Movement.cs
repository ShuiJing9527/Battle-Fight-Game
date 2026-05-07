using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public Rigidbody rb; // 注意：这里变成了 3D 的 Rigidbody
    public InputAction moveAction;

    void Awake()
    {
        // 自动获取 3D 刚体
        if (rb == null) rb = GetComponent<Rigidbody>();
    }

    void OnEnable()
    {
        moveAction?.Enable();
    }

    void OnDisable()
    {
        moveAction?.Disable();
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // 获取 WASD 的 Vector2 输入 (x, y)
        Vector2 input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        if (input == Vector2.zero && Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            input = Vector2.ClampMagnitude(input, 1f);
        }

        // 关键逻辑：
        // input.x 对应世界的 X（左右）
        // input.y 对应世界的 Z（前后，即深处）
        Vector3 moveDirection = new Vector3(input.x, 0, input.y);

        // 应用速度
        rb.linearVelocity = moveDirection * moveSpeed;
    }
}
