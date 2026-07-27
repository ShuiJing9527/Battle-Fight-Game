using UnityEngine;

public class SkillSFXByKey : MonoBehaviour
{
    [Header("Flash")]
    public KeyCode flashKey = KeyCode.Q;
    public AudioClip flashSfx;

    [Header("Dash / Movement")]
    public KeyCode dashKey = KeyCode.E;
    public AudioClip dashSfx;

    [Header("Generic Skill")]
    public KeyCode skillKey = KeyCode.None;
    public AudioClip skillSfx;

    [Header("Volume")]
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
            AudioManager.Instance?.PlaySkillSFX(flashSfx, volume);
        }
    }

    public void PlayDashSfx()
    {
        if (dashSfx != null)
        {
            AudioManager.Instance?.PlaySkillSFX(dashSfx, volume);
        }
    }

    public void PlaySkillSfx()
    {
        if (skillSfx != null)
        {
            AudioManager.Instance?.PlaySkillSFX(skillSfx, volume);
        }
    }
}
