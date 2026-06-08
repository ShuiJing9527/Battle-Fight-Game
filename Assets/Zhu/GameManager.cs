using System;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public event Action OnLanguageChanged;

    public GameObject settingsPanel;

    private List<(string key, string name)> languages = new List<(string, string)>
    {
        ("zh", "中文"),
        ("en", "English"),
        ("ja", "日本語")
    };

    private Dictionary<string, Dictionary<string, string>> texts = new Dictionary<string, Dictionary<string, string>>();
    public SettingsData settings = new SettingsData();

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
        InitLanguage();
    }

    void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMenuBGM();
    }

    void InitLanguage()
    {
        AddText("start", "开始游戏", "Start Game", "ゲーム開始");
        AddText("setting", "设置", "Settings", "設定");
        AddText("exit", "退出", "Exit", "終了");
        AddText("music", "音乐", "Music", "音楽");
        AddText("sfx", "音效", "SFX", "効果音");
        AddText("fullscreen", "全屏", "Fullscreen", "全画面");
        AddText("language", "语言", "Language", "言語");
        AddText("close", "关闭", "Close", "閉じる");
        AddText("save", "保存", "Save", "保存");
        AddText("title", "Battle Fight Game", "Battle Fight Game", "バトルゲーム");
        AddText("loading", "加载中", "Loading...", "読み込み中");
    }

    void AddText(string key, string zh, string en, string ja)
    {
        texts[key] = new Dictionary<string, string>
        {
            { "zh", zh },
            { "en", en },
            { "ja", ja }
        };
    }

    public string GetText(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        if (texts.ContainsKey(key) && texts[key].ContainsKey(settings.language))
            return texts[key][settings.language];
        return key;
    }

    public void SwitchLanguage(string lang)
    {
        settings.language = lang;
        OnLanguageChanged?.Invoke(); // 这里会通知所有文字刷新
    }

    public List<string> GetLangNames()
    {
        var list = new List<string>();
        foreach (var l in languages) list.Add(l.name);
        return list;
    }

    public List<string> GetLangKeys()
    {
        var list = new List<string>();
        foreach (var l in languages) list.Add(l.key);
        return list;
    }

    public void ToggleSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

[Serializable]
public class SettingsData
{
    public float musicVolume = 1;
    public float sfxVolume = 1;
    public bool fullscreen = false;
    public string language = "zh";
}