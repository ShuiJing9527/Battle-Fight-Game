using UnityEngine;

public class AnimationSFXEvent : MonoBehaviour
{
    [Header("动画音效")]
    public AudioClip attackSfx;
    public AudioClip skillSfx;
    public AudioClip specialSfx;
    public AudioClip footstepSfx;

    [Header("音量")]
    [Range(0f, 1f)] public float volume = 1f;

    public void PlayAttackSFX()
    {
        AudioManager.Instance?.PlaySFX(attackSfx, volume);
    }

    public void PlaySkillSFX()
    {
        AudioManager.Instance?.PlaySkillSFX(skillSfx, volume);
    }

    public void PlaySpecialSFX()
    {
        AudioManager.Instance?.PlaySkillSFX(specialSfx, volume);
    }

    public void PlayFootstepSFX()
    {
        AudioManager.Instance?.PlaySFX(footstepSfx, volume);
    }
}
