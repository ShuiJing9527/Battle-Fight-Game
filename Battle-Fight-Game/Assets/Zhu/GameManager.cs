using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏全局管理单例（Unity 3D 适配版）
/// 功能：多语言本地化、设置存档、游戏存档、加密解密
/// </summary>
public class GameManager : MonoBehaviour
{
    // 单例全局引用
    public static GameManager Instance;
    public static GameManager instance => Instance;

    // 语言切换事件（所有UI文本订阅这个事件，自动刷新多语言）
    public event Action OnLanguageChanged;

    // 支持的语言列表（代码 -> 显示名）
    private readonly List<KeyValuePair<string, string>> supportedLanguages = new List<KeyValuePair<string, string>>
    {
        new KeyValuePair<string, string>("en", "English"),
        new KeyValuePair<string, string>("zh", "中文"),
        new KeyValuePair<string, string>("ja", "日本語")
    };

    // 本地化字典：key -> (语言代码 -> 对应文本)
    private Dictionary<string, Dictionary<string, string>> localization = new Dictionary<string, Dictionary<string, string>>();

    // 全局游戏数据容器（设置+存档）
    public GameData gameData = new GameData();
    public SettingsData settings => gameData.settings;
    public SaveData saveData => gameData.saveData;

    // AES加密配置（密钥和向量必须16位，适配Unity全平台）
    private const string ENCRYPTION_KEY = "MySecretKey123456";
    private const string ENCRYPTION_IV = "InitialVector1234";

    // 存档文件路径
    private string settingsPath => Path.Combine(Application.persistentDataPath, "settings.dat");
    private string savePath => Path.Combine(Application.persistentDataPath, "save.dat");
    private string dataPath => Path.Combine(Application.persistentDataPath, "gamedata.dat");

    // 单例初始化
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 场景切换不销毁
            Initialize();
        }
        else
        {
            Destroy(gameObject); // 避免重复创建单例
        }
    }

    // 系统初始化
    private void Initialize()
    {
        InitializeLocalization();

        // 优先加载整体存档，兼容旧版本单独存档
        if (!LoadGameData())
        {
            LoadSettings();
            LoadGame();
        }

        // 触发语言生效
        OnLanguageChanged?.Invoke();
    }

    // 设置面板显示/隐藏切换
    public GameObject settingsPanel;
    public void ToggleSettingsPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    #region 多语言本地化核心
    private void InitializeLocalization()
    {
        localization = new Dictionary<string, Dictionary<string, string>>();

        // ========== 设置面板文本 ==========
        AddLocalized("label_music", "Music", "音乐", "音楽");
        AddLocalized("label_sfx", "SFX", "音效", "効果音");
        AddLocalized("label_fullscreen", "Fullscreen", "全屏", "全画面");
        AddLocalized("label_quality", "Quality", "画质", "画質");
        AddLocalized("label_language", "Language", "语言", "言語");
        AddLocalized("button_save_settings", "Save", "保存", "保存");
        AddLocalized("button_close", "Close", "关闭", "閉じる");

        // ========== 开始界面按钮文本 ==========
        AddLocalized("button_start", "Start Game", "开始游戏", "ゲーム開始");
        AddLocalized("button_load", "Load Game", "读取存档", "セーブを読み込む");
        AddLocalized("button_achievement", "Achievement", "成就", "実績");
        AddLocalized("button_settings", "Settings", "设置", "設定");
        AddLocalized("button_exit", "Exit Game", "退出游戏", "ゲーム終了");

        // ========== 加载条文本 ==========
        AddLocalized("loadbar_loading", "Loading...", "加载中...", "読み込み中...");
        AddLocalized("loadbar_complete", "Load Complete!", "加载完成！", "読み込み完了！");
        AddLocalized("loadbar_start_game", "Start Game", "开始游戏", "ゲーム開始");

        // ========== 场景加载提示 ==========
        AddLocalized("sceneloader_load_next", "Loading next scene: {0}", "加载下一场景：{0}", "次のシーンを読み込み中：{0}");
        AddLocalized("sceneloader_index_out_range", "Next scene index {0} out of range (0..{1})", "下一场景索引 {0} 超出范围（0..{1}）", "次のシーンインデックス {0} が範囲外です（0..{1}）");
        AddLocalized("sceneloader_scene_name_empty", "Scene name is empty, cancel loading", "场景名为空，取消加载", "シーン名が空です、読み込みをキャンセル");
        AddLocalized("sceneloader_load_scene", "Loading scene: {0}", "加载场景：{0}", "シーンを読み込み中：{0}");
        AddLocalized("sceneloader_invalid_save", "Save data invalid, try load again", "存档无效，请重试", "セーブデータが無効です、再試行してください");
        AddLocalized("sceneloader_restore_pos", "Player position restored", "玩家位置已恢复", "プレイヤーの位置を復元しました");
        AddLocalized("sceneloader_no_player", "Player not found", "未找到玩家对象", "プレイヤーオブジェクトが見つかりません");
        AddLocalized("sceneloader_save_quit", "Save completed, ready to quit", "存档已保存，准备退出", "セーブが完了しました、ゲームを終了します");
        AddLocalized("sceneloader_no_gm_quit", "GameManager not found, quit directly", "GameManager未找到，直接退出", "GameManagerが見つかりません、直接終了します");

        // ========== 存档相关文本 ==========
        AddLocalized("no_save_data", "No save data", "无存档", "セーブなし");
        AddLocalized("last_save", "Last save: {0}", "上次保存: {0}", "最終セーブ: {0}");
        AddLocalized("save_success", "Save Success!", "存档成功！", "セーブ成功！");
    }

    // 添加多语言文本（统一入口）
    private void AddLocalized(string key, string en, string zh, string ja)
    {
        localization[key] = new Dictionary<string, string>
        {
            { "en", en },
            { "zh", zh },
            { "ja", ja }
        };
    }

    // 获取对应语言的文本
    public string GetLocalizedText(string key)
    {
        string lang = settings.language ?? "en";
        if (localization.TryGetValue(key, out var langMap) && langMap.TryGetValue(lang, out var text))
        {
            return text;
        }
        return key; // 找不到Key返回Key本身，避免空文本
    }

    // 获取支持的语言代码列表
    public List<string> GetSupportedLanguageCodes()
    {
        var list = new List<string>();
        foreach (var kv in supportedLanguages) list.Add(kv.Key);
        return list;
    }

    // 获取支持的语言显示名列表
    public List<string> GetSupportedLanguageDisplayNames()
    {
        var list = new List<string>();
        foreach (var kv in supportedLanguages) list.Add(kv.Value);
        return list;
    }

    // 切换语言
    public void SetLanguage(string languageCode)
    {
        settings.language = languageCode;
        SaveSettings();
        OnLanguageChanged?.Invoke(); // 通知所有UI刷新文本
    }

    // 当前语言
    public string CurrentLanguage => settings.language ?? "en";
    #endregion

    #region 设置数据加载/保存/生效
    // 加载设置
    public void LoadSettings()
    {
        if (File.Exists(settingsPath))
        {
            try
            {
                string encrypted = File.ReadAllText(settingsPath);
                string json = DecryptString(encrypted);
                var loadedSettings = JsonUtility.FromJson<SettingsData>(json);
                if (loadedSettings != null) gameData.settings = loadedSettings;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"加载设置失败: {ex.Message}");
            }
        }
        ApplySettings();
    }

    // 保存设置
    public void SaveSettings()
    {
        try
        {
            string json = JsonUtility.ToJson(gameData.settings);
            string encrypted = EncryptString(json);
            File.WriteAllText(settingsPath, encrypted);
        }
        catch (Exception ex)
        {
            Debug.LogError($"保存设置失败: {ex.Message}");
        }
        ApplySettings();
    }

    // 应用设置到游戏
    public void ApplySettings()
    {
        AudioListener.volume = gameData.settings.musicVolume;
        Screen.fullScreen = gameData.settings.fullscreen;
        QualitySettings.SetQualityLevel(gameData.settings.qualityLevel);
    }
    #endregion

    #region 游戏存档加载/保存
    // 加载存档
    public bool LoadGame()
    {
        if (File.Exists(savePath))
        {
            try
            {
                string encrypted = File.ReadAllText(savePath);
                string json = DecryptString(encrypted);
                var loadedSave = JsonUtility.FromJson<SaveData>(json);
                if (loadedSave != null)
                {
                    gameData.saveData = loadedSave;
                    // 自动加载存档对应的场景
                    if (!string.IsNullOrEmpty(gameData.saveData.currentScene))
                    {
                        SceneManager.LoadScene(gameData.saveData.currentScene);
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"加载存档失败: {ex.Message}");
            }
        }
        else
        {
            NewGame(); // 无存档创建新档
        }
        return false;
    }

    // 保存存档
    public void SaveGame()
    {
        try
        {
            // 保存玩家位置（3D适配）
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                gameData.saveData.position = player.transform.position;
            }
            else
            {
                Debug.LogWarning("未找到Tag为Player的对象，位置未保存");
            }

            // 保存场景和时间
            gameData.saveData.currentScene = SceneManager.GetActiveScene().name;
            gameData.saveData.saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 加密保存
            string json = JsonUtility.ToJson(gameData.saveData);
            string encrypted = EncryptString(json);
            File.WriteAllText(savePath, encrypted);
            Debug.Log("存档保存成功！");
        }
        catch (Exception ex)
        {
            Debug.LogError($"保存存档失败: {ex.Message}");
        }
    }

    // 创建新存档
    public void NewGame()
    {
        gameData.saveData = new SaveData
        {
            currentScene = "GameScene",
            saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            playerScore = 0,
            playerLevel = 1,
            position = new Vector3(0, 1, 0) // 3D玩家默认出生点
        };
        SaveGame();
    }
    #endregion

    #region 整体游戏数据加载/保存
    public bool LoadGameData()
    {
        if (File.Exists(dataPath))
        {
            try
            {
                string encrypted = File.ReadAllText(dataPath);
                string json = DecryptString(encrypted);
                gameData = JsonUtility.FromJson<GameData>(json) ?? new GameData();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"加载整体数据失败: {ex.Message}");
            }
        }
        return false;
    }

    public void SaveGameData()
    {
        try
        {
            string json = JsonUtility.ToJson(gameData);
            string encrypted = EncryptString(json);
            File.WriteAllText(dataPath, encrypted);
            Debug.Log("整体游戏数据保存成功！");
        }
        catch (Exception ex)
        {
            Debug.LogError($"保存整体数据失败: {ex.Message}");
        }
    }
    #endregion

    #region AES加密解密（全平台兼容）
    private string EncryptString(string plainText)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(ENCRYPTION_KEY.PadRight(16).Substring(0, 16));
        byte[] ivBytes = Encoding.UTF8.GetBytes(ENCRYPTION_IV.PadRight(16).Substring(0, 16));

        using (Aes aes = Aes.Create())
        {
            aes.Key = keyBytes;
            aes.IV = ivBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                }
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }

    private string DecryptString(string cipherText)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(ENCRYPTION_KEY.PadRight(16).Substring(0, 16));
        byte[] ivBytes = Encoding.UTF8.GetBytes(ENCRYPTION_IV.PadRight(16).Substring(0, 16));
        byte[] cipherBytes = Convert.FromBase64String(cipherText);

        using (Aes aes = Aes.Create())
        {
            aes.Key = keyBytes;
            aes.IV = ivBytes;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using (MemoryStream ms = new MemoryStream(cipherBytes))
            {
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader sr = new StreamReader(cs))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
        }
    }
    #endregion
}

#region 数据类定义（全局通用）
[Serializable]
public class GameData
{
    public SettingsData settings = new SettingsData();
    public SaveData saveData = new SaveData();
}

[Serializable]
public class SettingsData
{
    public float musicVolume = 1f;
    public float sfxVolume = 1f;
    public bool fullscreen = true;
    public int qualityLevel = 2;
    public string language = "en";
}

[Serializable]
public class SaveData
{
    public string currentScene = "";
    public string saveTime = "";
    public int playerScore = 0;
    public int playerLevel = 1;
    public Vector3 position; // 3D玩家位置
}
#endregion