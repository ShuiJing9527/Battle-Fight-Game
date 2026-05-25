using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsUI : MonoBehaviour
{
    public Slider musicSlider;
    public Slider sfxSlider;
    public Toggle fullscreenToggle;
    public TMP_Dropdown languageDropdown;

    void OnEnable()
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (GameManager.Instance == null)
            return;

        var gm = GameManager.Instance;

        if (musicSlider != null)
            musicSlider.value = gm.settings.musicVolume;
        if (sfxSlider != null)
            sfxSlider.value = gm.settings.sfxVolume;
        if (fullscreenToggle != null)
            fullscreenToggle.isOn = gm.settings.fullscreen;

        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(gm.GetLangNames());

        int index = gm.GetLangKeys().IndexOf(gm.settings.language);
        if (index >= 0)
            languageDropdown.value = index;
    }

    // 👇 这个就是你要的方法！现在已经写进去了！
    public void OnLanguageChanged(int index)
    {
        if (GameManager.Instance == null)
            return;

        string langKey = GameManager.Instance.GetLangKeys()[index];
        GameManager.Instance.SwitchLanguage(langKey);
    }

    public void Save()
    {
        if (GameManager.Instance == null)
            return;

        var gm = GameManager.Instance;
        gm.settings.musicVolume = musicSlider.value;
        gm.settings.sfxVolume = sfxSlider.value;
        gm.settings.fullscreen = fullscreenToggle.isOn;
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}