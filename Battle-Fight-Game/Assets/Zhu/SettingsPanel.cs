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
        RefreshUI();
    }

    void RefreshUI()
    {
        var gm = GameManager.Instance;
        musicSlider.value = gm.settings.musicVolume;
        sfxSlider.value = gm.settings.sfxVolume;
        fullscreenToggle.isOn = gm.settings.fullscreen;

        langDrop.ClearOptions();
        langDrop.AddOptions(gm.GetLangNames());

        int index = gm.GetLangKeys().IndexOf(gm.settings.language);
        langDrop.value = index;
    }

    public void SaveSetting()
    {
        var gm = GameManager.Instance;
        gm.settings.musicVolume = musicSlider.value;
        gm.settings.sfxVolume = sfxSlider.value;
        gm.settings.fullscreen = fullscreenToggle.isOn;

        string langKey = gm.GetLangKeys()[langDrop.value];
        gm.SetLanguage(langKey);

        gm.SaveSettings();
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
}