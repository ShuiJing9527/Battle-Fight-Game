using UnityEngine;

public class AutoPlaySFX : MonoBehaviour
{
    [Header("物体出现时播放，比如火球生成、斩击出现")]
    public AudioClip appearSfx;

    [Header("碰撞 / 命中时播放")]
    public AudioClip hitSfx;

    [Header("只命中这些Tag才播放，留空则碰到任何东西都播放")]
    public string[] hitTags;

    [Header("音量")]
    [Range(0f, 1f)] public float volume = 1f;

    private bool hasPlayedHit = false;

    private void OnEnable()
    {
        hasPlayedHit = false;

        if (appearSfx != null)
        {
            AudioManager.Instance?.PlaySFX(appearSfx, volume);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryPlayHitSound(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryPlayHitSound(collision.gameObject);
    }

    private void TryPlayHitSound(GameObject target)
    {
        if (hasPlayedHit) return;
        if (hitSfx == null) return;

        if (hitTags != null && hitTags.Length > 0)
        {
            bool tagMatched = false;

            foreach (string tag in hitTags)
            {
                if (!string.IsNullOrEmpty(tag) && target.CompareTag(tag))
                {
                    tagMatched = true;
                    break;
                }
            }

            if (!tagMatched) return;
        }

        hasPlayedHit = true;
        AudioManager.Instance?.PlaySFX(hitSfx, volume);
    }
}