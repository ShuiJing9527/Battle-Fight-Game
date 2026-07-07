using UnityEngine;

public class SkillSFXByKey : MonoBehaviour
{
    [Header("闪现")]
    public KeyCode flashKey = KeyCode.Q;
    public AudioClip flashSfx;

    [Header("急行 / 加速")]
    public KeyCode dashKey = KeyCode.E;
    public AudioClip dashSfx;

    [Header("通用技能")]
    public KeyCode skillKey = KeyCode.None;
    public AudioClip skillSfx;

    [Header("音量")]
    [Range(0f, 1f)] public float volume = 1f;

    private void Update()
    {
        if (flashKey != KeyCode.None && Input.GetKeyDown(flashKey))
        {
            AudioManager.Instance?.PlaySFX(flashSfx, volume);
        }

        if (dashKey != KeyCode.None && Input.GetKeyDown(dashKey))
        {
            AudioManager.Instance?.PlaySFX(dashSfx, volume);
        }

        if (skillKey != KeyCode.None && Input.GetKeyDown(skillKey))
        {
            AudioManager.Instance?.PlaySFX(skillSfx, volume);
        }
    }
}