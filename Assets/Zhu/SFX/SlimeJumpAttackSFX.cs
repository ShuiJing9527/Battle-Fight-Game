using UnityEngine;

public class SlimeJumpSFX : MonoBehaviour
{
    [Header("检测用 Rigidbody，不填会自动找")]
    public Rigidbody rb;

    [Header("玩家Tag")]
    public string playerTag = "Player";

    [Header("玩家距离小于这个值时，才认为是攻击跳跃")]
    public float detectPlayerDistance = 5f;

    [Header("向上速度超过这个值，认为史莱姆起跳")]
    public float jumpVelocityThreshold = 1.5f;

    [Header("起跳音效，可不填")]
    public AudioClip jumpSfx;

    [Header("落地音效，可不填")]
    public AudioClip landSfx;

    [Header("音量")]
    [Range(0f, 1f)]
    public float volume = 0.3f;

    [Header("起跳音效冷却")]
    public float jumpCooldown = 1.2f;

    [Header("落地检测，可不填")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.25f;
    public LayerMask groundLayer;

    private Transform player;
    private float lastYVelocity;
    private float lastJumpTime = -999f;
    private bool wasGrounded = true;
    private bool hasJumped = false;

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);

        if (playerObj != null)
        {
            player = playerObj.transform;
        }

        lastYVelocity = GetYVelocity();
    }

    private void Update()
    {
        if (rb == null) return;

        DetectJumpStart();
        DetectLanding();

        lastYVelocity = GetYVelocity();
    }

    private float GetYVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity.y;
#else
        return rb.velocity.y;
#endif
    }

    private void DetectJumpStart()
    {
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer > detectPlayerDistance)
        {
            return;
        }

        float yVelocity = GetYVelocity();

        bool isJumpingUp =
            yVelocity > jumpVelocityThreshold &&
            lastYVelocity <= jumpVelocityThreshold;

        if (!isJumpingUp)
        {
            return;
        }

        if (Time.time - lastJumpTime < jumpCooldown)
        {
            return;
        }

        lastJumpTime = Time.time;
        hasJumped = true;

        if (jumpSfx != null)
        {
            AudioManager.Instance?.PlaySlimeSFX(jumpSfx, volume);
        }
    }

    private void DetectLanding()
    {
        if (groundCheck == null) return;

        bool isGrounded = Physics.CheckSphere(
            groundCheck.position,
            groundCheckRadius,
            groundLayer
        );

        if (!wasGrounded && isGrounded && hasJumped)
        {
            hasJumped = false;

            if (landSfx != null)
            {
                AudioManager.Instance?.PlaySlimeSFX(landSfx, volume);
            }
        }

        wasGrounded = isGrounded;
    }
}
