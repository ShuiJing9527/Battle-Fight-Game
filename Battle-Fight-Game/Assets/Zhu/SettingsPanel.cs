using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsPanel : MonoBehaviour
{
    public Slider sliderMusic;
    public Slider sliderSFX;
    public Toggle toggleFullscreen;
    public TMP_Dropdown dropdownLanguage;

    private GameManager gm;

    void Awake()
    {
        gm = GameManager.Instance;
    }

    void OnEnable()
    {
        RefreshUI();
    }

    void RefreshUI()
    {
        sliderMusic.value = gm.settings.musicVolume;
        sliderSFX.value = gm.settings.sfxVolume;
        toggleFullscreen.isOn = gm.settings.fullscreen;
        dropdownLanguage.ClearOptions();
        dropdownLanguage.AddOptions(gm.GetSupportedLanguageDisplayNames());
        dropdownLanguage.value = gm.GetSupportedLanguageCodes().IndexOf(gm.CurrentLanguage);
    }

    public void ApplySettings()
    {
        gm.settings.musicVolume = sliderMusic.value;
        gm.settings.sfxVolume = sliderSFX.value;
        gm.settings.fullscreen = toggleFullscreen.isOn;
        string lang = gm.GetSupportedLanguageCodes()[dropdownLanguage.value];
        gm.SetLanguage(lang);
        gm.SaveSettings();
        gm.ApplySettings();
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
}