using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsUI : MonoBehaviour
{
    public Slider musicSlider;
    public Slider sfxSlider;
    public Toggle fullscreenToggle;

    private void OnEnable()
    {
        RefreshUI();
        BindEvents();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    public void RefreshUI()
    {
        if (GameManager.Instance == null || GameManager.Instance.settings == null)
            return;

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(GameManager.Instance.settings.musicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(GameManager.Instance.settings.sfxVolume);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(GameManager.Instance.settings.fullscreen);
        }
    }

    private void BindEvents()
    {
        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }
    }

    private void UnbindEvents()
    {
        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
        }
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (GameManager.Instance != null && GameManager.Instance.settings != null)
        {
            GameManager.Instance.settings.musicVolume = value;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBgmVolume(value);
        }
    }

    private void OnSfxVolumeChanged(float value)
    {
        if (GameManager.Instance != null && GameManager.Instance.settings != null)
        {
            GameManager.Instance.settings.sfxVolume = value;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSfxVolume(value);
        }
    }

    private void OnFullscreenChanged(bool value)
    {
        if (GameManager.Instance != null && GameManager.Instance.settings != null)
        {
            GameManager.Instance.settings.fullscreen = value;
        }

        Screen.fullScreen = value;
    }

    public void SaveSettings()
    {
        if (GameManager.Instance == null || GameManager.Instance.settings == null)
            return;

        if (musicSlider != null)
        {
            GameManager.Instance.settings.musicVolume = musicSlider.value;
            AudioManager.Instance?.SetBgmVolume(musicSlider.value);
        }

        if (sfxSlider != null)
        {
            GameManager.Instance.settings.sfxVolume = sfxSlider.value;
            AudioManager.Instance?.SetSfxVolume(sfxSlider.value);
        }

        if (fullscreenToggle != null)
        {
            GameManager.Instance.settings.fullscreen = fullscreenToggle.isOn;
            Screen.fullScreen = GameManager.Instance.settings.fullscreen;
        }
    }

    public void ClosePanel()
    {
        SaveSettings();
        gameObject.SetActive(false);
    }
}