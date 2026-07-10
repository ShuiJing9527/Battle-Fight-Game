using UnityEngine;

public class FootstepAudio : MonoBehaviour
{
    [Header("脚步声音效，可以放多个随机播放")]
    public AudioClip[] footstepSfx;

    [Header("检测用 Rigidbody，不填会自动找")]
    public Rigidbody rb;

    [Header("移动速度超过这个值才播放脚步声")]
    public float minMoveSpeed = 0.1f;

    [Header("脚步间隔，数值越小越频繁")]
    public float stepInterval = 0.35f;

    [Header("音量")]
    [Range(0f, 1f)] public float volume = 0.7f;

    [Header("空中是否播放脚步声")]
    public bool playInAir = false;

    [Header("地面检测，可不填")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    private float stepTimer = 0f;

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
    }

    private void Update()
    {
        if (rb == null) return;
        if (footstepSfx == null || footstepSfx.Length == 0) return;

        // 只检测水平移动速度，不算上下跳跃速度
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float moveSpeed = horizontalVelocity.magnitude;

        if (moveSpeed < minMoveSpeed)
        {
            stepTimer = 0f;
            return;
        }

        if (!playInAir && groundCheck != null)
        {
            bool isGrounded = Physics.CheckSphere(
                groundCheck.position,
                groundCheckRadius,
                groundLayer
            );

            if (!isGrounded)
            {
                stepTimer = 0f;
                return;
            }
        }

        stepTimer += Time.deltaTime;

        if (stepTimer >= stepInterval)
        {
            PlayFootstep();
            stepTimer = 0f;
        }
    }

    private void PlayFootstep()
    {
        if (footstepSfx.Length == 0) return;

        AudioClip clip = footstepSfx[Random.Range(0, footstepSfx.Length)];

        if (clip != null)
        {
            AudioManager.Instance?.PlaySFX(clip, volume);
        }
    }
}