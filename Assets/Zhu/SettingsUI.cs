using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsUI : MonoBehaviour
{
    public Slider musicSlider;
    public Slider sfxSlider;
    public Toggle fullscreenToggle;

    void OnEnable()
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        // 第一层防护：GameManager不存在直接退出
        if (GameManager.Instance == null)
            return;

        // 逐个赋值，空组件跳过不报错
        if (musicSlider != null)
            musicSlider.value = GameManager.Instance.settings.musicVolume;

        if (sfxSlider != null)
            sfxSlider.value = GameManager.Instance.settings.sfxVolume;

        if (fullscreenToggle != null)
            fullscreenToggle.isOn = GameManager.Instance.settings.fullscreen;
    }

    public void SaveSettings()
    {
        if (GameManager.Instance == null) return;

        if (musicSlider != null)
            GameManager.Instance.settings.musicVolume = musicSlider.value;

        if (sfxSlider != null)
            GameManager.Instance.settings.sfxVolume = sfxSlider.value;

        if (fullscreenToggle != null)
        {
            GameManager.Instance.settings.fullscreen = fullscreenToggle.isOn;
            Screen.fullScreen = GameManager.Instance.settings.fullscreen;
        }
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
}