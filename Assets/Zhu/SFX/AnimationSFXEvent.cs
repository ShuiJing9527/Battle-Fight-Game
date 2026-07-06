using UnityEngine;

public class AnimationSFXEvent : MonoBehaviour
{
    [Header("动画音效")]
    public AudioClip attackSfx;
    public AudioClip footstepSfx;
    public AudioClip specialSfx;

    [Header("音量")]
    [Range(0f, 1f)] public float volume = 1f;

    public void PlayAttackSFX()
    {
        AudioManager.Instance?.PlaySFX(attackSfx, volume);
    }

    public void PlayFootstepSFX()
    {
        AudioManager.Instance?.PlaySFX(footstepSfx, volume);
    }

    public void PlaySpecialSFX()
    {
        AudioManager.Instance?.PlaySFX(specialSfx, volume);
    }
}