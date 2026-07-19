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

    public void PlaySfxForSkillLabel(string skillLabel)
    {
        if (string.IsNullOrWhiteSpace(skillLabel))
        {
            return;
        }

        switch (skillLabel.Trim().ToUpperInvariant())
        {
            case "Q":
                PlayFlashSfx();
                break;
            case "E":
                PlayDashSfx();
                break;
            case "R":
                PlaySkillSfx();
                break;
        }
    }

    public void PlayFlashSfx()
    {
        if (flashSfx != null)
        {
            AudioManager.Instance?.PlaySFX(flashSfx, volume);
        }
    }

    public void PlayDashSfx()
    {
        if (dashSfx != null)
        {
            AudioManager.Instance?.PlaySFX(dashSfx, volume);
        }
    }

    public void PlaySkillSfx()
    {
        if (skillSfx != null)
        {
            AudioManager.Instance?.PlaySFX(skillSfx, volume);
        }
    }
}
