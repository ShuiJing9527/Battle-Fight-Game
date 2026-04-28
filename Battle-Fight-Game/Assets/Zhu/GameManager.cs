using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class SettingsData
{
    public float musicVolume = 1f;
    public float sfxVolume = 1f;
    public bool fullscreen = false;
    public string language = "zh";
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public event Action OnLanguageChanged;

    public SettingsData settings = new SettingsData();
    public GameObject settingsPanel;

    private Dictionary<string, Dictionary<string, string>> loc = new Dictionary<string, Dictionary<string, string>>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        InitLocalization();
        LoadSettings();
    }

    void InitLocalization()
    {
        Add("button_start", "Start", "开始游戏", "ゲーム開始");
        Add("button_settings", "Settings", "设置", "設定");
        Add("button_exit", "Exit", "退出游戏", "終了");
        Add("button_close", "Close", "关闭", "閉じる");

        Add("label_music", "Music", "音乐", "音楽");
        Add("label_sfx", "SFX", "音效", "効果音");
        Add("label_fullscreen", "Fullscreen", "全屏", "全画面");
        Add("label_language", "Language", "语言", "言語");

        Add("loadbar_loading", "Loading...", "加载中...", "読み込み中...");
        Add("loadbar_complete", "Complete", "加载完成", "完了");
    }

    void Add(string key, string en, string zh, string ja)
    {
        loc[key] = new Dictionary<string, string>()
        {
            {"en",en},{"zh",zh},{"ja",ja}
        };
    }

    public string GetText(string key)
    {
        string lang = settings.language;
        if (loc.ContainsKey(key) && loc[key].ContainsKey(lang))
            return loc[key][lang];
        return key;
    }

    public List<string> GetLangNames()
    {
        return new List<string> { "English", "中文", "日本語" };
    }

    public List<string> GetLangKeys()
    {
        return new List<string> { "en", "zh", "ja" };
    }

    public void SetLanguage(string key)
    {
        settings.language = key;
        OnLanguageChanged?.Invoke();
        SaveSettings();
    }

    public void ToggleSettingsPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat("MusicVol", settings.musicVolume);
        PlayerPrefs.SetFloat("SfxVol", settings.sfxVolume);
        PlayerPrefs.SetInt("Fullscreen", settings.fullscreen ? 1 : 0);
        PlayerPrefs.SetString("Lang", settings.language);
        PlayerPrefs.Save();
        ApplySetting();
    }

    public void LoadSettings()
    {
        settings.musicVolume = PlayerPrefs.GetFloat("MusicVol", 1f);
        settings.sfxVolume = PlayerPrefs.GetFloat("SfxVol", 1f);
        settings.fullscreen = PlayerPrefs.GetInt("Fullscreen", 0) == 1;
        settings.language = PlayerPrefs.GetString("Lang", "zh");
        ApplySetting();
    }

    void ApplySetting()
    {
        AudioListener.volume = settings.musicVolume;
        Screen.fullScreen = settings.fullscreen;
    }
}