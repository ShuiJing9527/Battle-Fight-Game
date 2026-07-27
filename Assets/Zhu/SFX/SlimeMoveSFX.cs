using UnityEngine;

public class SlimeMoveSFX : MonoBehaviour
{
    [Header("史莱姆移动音效，可以放多个随机播放")]
    public AudioClip[] moveSfx;

    [Header("检测用 Rigidbody，不填会自动找")]
    public Rigidbody rb;

    [Header("移动速度超过这个值才播放")]
    public float minMoveSpeed = 0.1f;

    [Header("音效间隔")]
    public float soundInterval = 0.45f;

    [Header("音量")]
    [Range(0f, 1f)] public float volume = 0.55f;

    private float timer;

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
        if (moveSfx == null || moveSfx.Length == 0) return;

        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float speed = horizontalVelocity.magnitude;

        if (speed < minMoveSpeed)
        {
            timer = 0f;
            return;
        }

        timer += Time.deltaTime;

        if (timer >= soundInterval)
        {
            PlayMoveSFX();
            timer = 0f;
        }
    }

    private void PlayMoveSFX()
    {
        AudioClip clip = moveSfx[Random.Range(0, moveSfx.Length)];

        if (clip != null)
        {
            AudioManager.Instance?.PlaySlimeSFX(clip, volume);
        }
    }
}
