using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public event Action OnLanguageChanged;

    public GameObject settingsPanel;

    private readonly List<KeyValuePair<string, string>> supportedLanguages = new List<KeyValuePair<string, string>>
    {
        new KeyValuePair<string, string>("en", "English"),
        new KeyValuePair<string, string>("zh", "中文"),
        new KeyValuePair<string, string>("ja", "日本語")
    };

    private Dictionary<string, Dictionary<string, string>> localization = new Dictionary<string, Dictionary<string, string>>();
    public GameData gameData = new GameData();
    public SettingsData settings => gameData.settings;

    private const string ENCRYPTION_KEY = "MySecretKey123456";
    private const string ENCRYPTION_IV = "InitialVector1234";
    private string settingsPath => Path.Combine(Application.persistentDataPath, "settings.dat");

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitData()
    {
        InitializeLocalization();
        LoadSettings();
        OnLanguageChanged?.Invoke();
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    // ======================
    // 按钮绑定用
    // ======================
    public void ToggleSettingsPanel()
    {
        Debug.Log("设置按钮点击");
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void ExitGame()
    {
        Debug.Log("退出按钮点击");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ======================
    // 多语言
    // ======================
    private void InitializeLocalization()
    {
        AddLocalized("label_music", "Music", "音乐", "音楽");
        AddLocalized("label_sfx", "SFX", "音效", "効果音");
        AddLocalized("label_fullscreen", "Fullscreen", "全屏", "全画面");
        AddLocalized("label_language", "Language", "语言", "言語");
        AddLocalized("button_close", "Close", "关闭", "閉じる");
    }

    private void AddLocalized(string key, string en, string zh, string ja)
    {
        localization[key] = new Dictionary<string, string>
        {
            { "en", en }, { "zh", zh }, { "ja", ja }
        };
    }

    public string GetText(string key)
    {
        string lang = settings.language ?? "en";
        if (localization.ContainsKey(key) && localization[key].ContainsKey(lang))
            return localization[key][lang];
        return key;
    }

    public List<string> GetLangNames() => GetSupportedLanguageDisplayNames();
    public List<string> GetLangKeys() => GetSupportedLanguageCodes();

    public List<string> GetSupportedLanguageCodes()
    {
        var list = new List<string>();
        foreach (var kv in supportedLanguages) list.Add(kv.Key);
        return list;
    }

    public List<string> GetSupportedLanguageDisplayNames()
    {
        var list = new List<string>();
        foreach (var kv in supportedLanguages) list.Add(kv.Value);
        return list;
    }

    public void SetLanguage(string languageCode)
    {
        settings.language = languageCode;
        SaveSettings();
        OnLanguageChanged?.Invoke();
    }

    // ======================
    // 设置
    // ======================
    public void LoadSettings()
    {
        if (File.Exists(settingsPath))
        {
            try
            {
                string enc = File.ReadAllText(settingsPath);
                string json = DecryptString(enc);
                var s = JsonUtility.FromJson<SettingsData>(json);
                if (s != null) gameData.settings = s;
            }
            catch { }
        }
        ApplySettings();
    }

    public void SaveSettings()
    {
        try
        {
            string json = JsonUtility.ToJson(gameData.settings);
            string enc = EncryptString(json);
            File.WriteAllText(settingsPath, enc);
        }
        catch { }
        ApplySettings();
    }

    public void ApplySettings()
    {
        AudioListener.volume = gameData.settings.musicVolume;
        Screen.fullScreen = gameData.settings.fullscreen;
    }

    // ======================
    // 加密
    // ======================
    private string EncryptString(string t)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(ENCRYPTION_KEY.PadRight(16)[..16]);
            aes.IV = Encoding.UTF8.GetBytes(ENCRYPTION_IV.PadRight(16)[..16]);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            using var sw = new StreamWriter(cs);
            sw.Write(t);
            return Convert.ToBase64String(ms.ToArray());
        }
    }

    private string DecryptString(string t)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(ENCRYPTION_KEY.PadRight(16)[..16]);
            aes.IV = Encoding.UTF8.GetBytes(ENCRYPTION_IV.PadRight(16)[..16]);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var ms = new MemoryStream(Convert.FromBase64String(t));
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
    }
}

[Serializable]
public class GameData
{
    public SettingsData settings = new SettingsData();
}

[Serializable]
public class SettingsData
{
    public float musicVolume = 1f;
    public float sfxVolume = 1f;
    public bool fullscreen = false;
    public int qualityLevel = 2;
    public string language = "zh";
}

[Serializable]
public class SaveData
{
    public string currentScene = "";
    public Vector3 position;
}