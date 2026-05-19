using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsUI : MonoBehaviour
{
    public Slider musicSlider;
    public Slider sfxSlider;
    public Toggle fullscreenToggle;
    public TMP_Dropdown langDrop;

    void OnEnable()
    {
        // 最安全：延迟2帧刷新，百分百确保GM已初始化
        Invoke(nameof(RefreshUI), 0.02f);
    }

    public void RefreshUI()
    {
        // 双重保险！找不到直接return，绝对不报错
        if (GameManager.Instance == null)
            return;

        if (GameManager.Instance.settings == null)
            return;

        var gm = GameManager.Instance;

        // 音量
        if (musicSlider != null)
            musicSlider.value = gm.settings.musicVolume;
        if (sfxSlider != null)
            sfxSlider.value = gm.settings.sfxVolume;

        // 全屏
        if (fullscreenToggle != null)
            fullscreenToggle.isOn = gm.settings.fullscreen;

        // 语言下拉框
        if (langDrop != null)
        {
            langDrop.ClearOptions();
            langDrop.AddOptions(gm.GetLangNames());

            int index = gm.GetLangKeys().IndexOf(gm.settings.language);
            if (index >= 0)
                langDrop.value = index;
        }
    }

    public void SaveSetting()
    {
        if (GameManager.Instance == null) return;

        var gm = GameManager.Instance;
        gm.settings.musicVolume = musicSlider.value;
        gm.settings.sfxVolume = sfxSlider.value;
        gm.settings.fullscreen = fullscreenToggle.isOn;

        string langKey = gm.GetLangKeys()[langDrop.value];
        gm.SetLanguage(langKey);
        gm.SaveSettings();

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetBgmVolume(musicSlider.value);
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
}