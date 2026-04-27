using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
// 新增：场景管理命名空间（必须加）
using UnityEngine.SceneManagement;
using System.Collections;

namespace Game.UI
{
    public class UIManager : MonoBehaviour
    {
        // ========== UI字段定义（序列化+注释，方便手动拖入） ==========
        [Header("标题/版本文本")]
        [SerializeField] private TextMeshProUGUI gameTitleText;
        [SerializeField] private TextMeshProUGUI versionText;

        [Header("设置面板组件")]
        [SerializeField] private TextMeshProUGUI musicLabel;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private TextMeshProUGUI sfxLabel;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private TextMeshProUGUI fullscreenLabel;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private TextMeshProUGUI languageLabel;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private Button closeSettingsBtn;

        [Header("主菜单按钮文本")]
        [SerializeField] private TextMeshProUGUI startBtnText;
        [SerializeField] private TextMeshProUGUI achievementBtnText;
        [SerializeField] private TextMeshProUGUI settingsBtnText;
        [SerializeField] private TextMeshProUGUI exitBtnText;

        // 缓存GameManager引用（加空值保护）
        private GameManager GameMgr => GameManager.Instance != null ? GameManager.Instance : null;

        private void Awake()
        {
            // 关闭动态绑定（改用手动拖入，避免路径错误）
            // BindUIComponents();

            // 跨场景保留Canvas（避免UI销毁）
            if (gameObject.GetComponent<Canvas>() != null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start()
        {
            // 核心：GameManager为空直接终止初始化
            if (GameMgr == null)
            {
                Debug.LogError("[UIManager] GameManager实例不存在！请检查场景中是否有GameManager对象");
                return;
            }

            // 初始化流程（按优先级）
            BindUIEvents();          // 绑定按钮事件
            SubscribeLanguageEvent();// 订阅语言变化
            InitializeUIState();     // 初始化UI状态
            UpdateAllTexts();        // 更新多语言文本
        }

        // ========== 新增：场景加载完成监听（生命周期方法，无private） ==========
        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // ========== 1. 绑定UI交互事件（全空值保护+去重） ==========
        private void BindUIEvents()
        {
            // 开始游戏按钮（核心修改：绑定场景跳转）
            BindButtonEvent(startBtnText, LoadGameScene);

            // 成就按钮
            BindButtonEvent(achievementBtnText, () =>
            {
                Debug.Log("[UIManager] 成就按钮点击");
                // 实现成就逻辑
            });

            // 设置按钮（打开设置面板）
            BindButtonEvent(settingsBtnText, () =>
            {
                Debug.Log("[UIManager] 设置按钮点击");
                if (GameMgr?.settingsPanel != null)
                {
                    GameMgr.settingsPanel.SetActive(true);
                }
                else
                {
                    Debug.LogError("[UIManager] GameManager的settingsPanel未绑定！");
                }
            });

            // 退出游戏按钮
            BindButtonEvent(exitBtnText, QuitGame);

            // 关闭设置面板按钮
            if (closeSettingsBtn != null)
            {
                closeSettingsBtn.onClick.RemoveAllListeners();
                closeSettingsBtn.onClick.AddListener(() =>
                {
                    if (GameMgr?.settingsPanel != null)
                    {
                        GameMgr.settingsPanel.SetActive(false);
                    }
                });
            }

            // 音乐滑块事件
            if (musicSlider != null)
            {
                musicSlider.onValueChanged.RemoveAllListeners();
                musicSlider.onValueChanged.AddListener(value =>
                {
                    if (GameMgr != null)
                    {
                        GameMgr.settings.musicVolume = value;
                        GameMgr.SaveSettings();
                    }
                });
            }

            // 音效滑块事件
            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveAllListeners();
                sfxSlider.onValueChanged.AddListener(value =>
                {
                    if (GameMgr != null)
                    {
                        GameMgr.settings.sfxVolume = value;
                        GameMgr.SaveSettings();
                    }
                });
            }

            // 全屏开关事件
            if (fullscreenToggle != null)
            {
                fullscreenToggle.onValueChanged.RemoveAllListeners();
                fullscreenToggle.onValueChanged.AddListener(value =>
                {
                    if (GameMgr != null)
                    {
                        GameMgr.settings.fullscreen = value;
                        GameMgr.SaveSettings();
                        Screen.fullScreen = value;
                    }
                });
            }

            // 语言下拉框事件
            if (languageDropdown != null)
            {
                languageDropdown.onValueChanged.RemoveAllListeners();
                languageDropdown.onValueChanged.AddListener(OnLanguageSelected);
                InitializeLanguageDropdown(); // 初始化下拉框选项
            }
        }

        // ========== 新增：场景跳转核心方法 ==========
        private void LoadGameScene()
        {
            Debug.Log("[UIManager] 开始游戏按钮点击，跳转至GameScene");
            // 注意：这里的"GameScene"要和你创建的游戏场景名完全一致（区分大小写）
            SceneManager.LoadScene("GameScene");
        }

        // ========== 新增：场景加载完成回调 ==========
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "GameScene")
            {
                Debug.Log("[UIManager] 游戏场景加载完成，隐藏主菜单UI");
                // 隐藏主菜单Canvas（保留对象，返回主菜单时可复用）
                gameObject.SetActive(false);
            }
        }

        // 按钮事件绑定工具方法（简化重复代码）
        private void BindButtonEvent(TextMeshProUGUI btnText, System.Action onClickAction)
        {
            if (btnText == null) return;

            Button btn = btnText.transform.parent.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogError($"[UIManager] {btnText.name}的父对象没有Button组件！");
                return;
            }

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onClickAction?.Invoke());
        }

        // ========== 2. 语言相关初始化/回调 ==========
        private void SubscribeLanguageEvent()
        {
            if (GameMgr == null) return;

            // 先移除再添加，避免重复订阅
            GameMgr.OnLanguageChanged -= UpdateAllTexts;
            GameMgr.OnLanguageChanged += UpdateAllTexts;
        }

        private void InitializeLanguageDropdown()
        {
            if (languageDropdown == null || GameMgr == null) return;

            languageDropdown.ClearOptions();
            List<string> displayNames = GameMgr.GetSupportedLanguageDisplayNames();
            if (displayNames.Count > 0)
            {
                languageDropdown.AddOptions(displayNames);

                // 设置当前选中语言
                int currentIndex = GameMgr.GetSupportedLanguageCodes().IndexOf(GameMgr.CurrentLanguage);
                if (currentIndex >= 0)
                {
                    languageDropdown.value = currentIndex;
                }
            }
        }

        private void OnLanguageSelected(int index)
        {
            if (GameMgr == null || languageDropdown == null) return;

            List<string> langCodes = GameMgr.GetSupportedLanguageCodes();
            if (index >= 0 && index < langCodes.Count)
            {
                GameMgr.SetLanguage(langCodes[index]);
            }
        }

        // ========== 3. UI状态初始化（全空值保护） ==========
        private void InitializeUIState()
        {
            if (GameMgr == null) return;

            // 音量初始化
            if (musicSlider != null) musicSlider.value = GameMgr.settings.musicVolume;
            if (sfxSlider != null) sfxSlider.value = GameMgr.settings.sfxVolume;

            // 全屏状态初始化
            if (fullscreenToggle != null) fullscreenToggle.isOn = GameMgr.settings.fullscreen;
        }

        // ========== 4. 多语言文本更新（核心） ==========
        private void UpdateAllTexts()
        {
            if (GameMgr == null) return;

            // 标题文本
            UpdateGameTitle();

            // 设置面板文本
            if (musicLabel != null) musicLabel.text = GameMgr.GetLocalizedText("label_music");
            if (sfxLabel != null) sfxLabel.text = GameMgr.GetLocalizedText("label_sfx");
            if (fullscreenLabel != null) fullscreenLabel.text = GameMgr.GetLocalizedText("label_fullscreen");
            if (languageLabel != null) languageLabel.text = GameMgr.GetLocalizedText("label_language");

            // 按钮文本
            if (startBtnText != null) startBtnText.text = GameMgr.GetLocalizedText("button_start");
            if (achievementBtnText != null) achievementBtnText.text = GameMgr.GetLocalizedText("button_achievement");
            if (settingsBtnText != null) settingsBtnText.text = GameMgr.GetLocalizedText("button_settings");
            if (exitBtnText != null) exitBtnText.text = GameMgr.GetLocalizedText("button_exit");

            // 版本号（固定值）
            if (versionText != null) versionText.text = "DEMO v1.0";
        }

        private void UpdateGameTitle()
        {
            if (gameTitleText == null || GameMgr == null) return;

            switch (GameMgr.CurrentLanguage)
            {
                case "zh": gameTitleText.text = "阿黄阿黄"; break;
                case "en": gameTitleText.text = "Ahuang Ahuang"; break;
                case "ja": gameTitleText.text = "小麦"; break;
                default: gameTitleText.text = "阿黄阿黄"; break;
            }
        }

        // ========== 6. 退出游戏逻辑（唯一版本，避免重复） ==========
        public void QuitGame()
        {
            Debug.Log("[UIManager] 执行退出游戏逻辑");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ========== 工具方法：判断对象是否存活 ==========
        private bool IsNotNullAndAlive(Object obj)
        {
            return obj != null && !obj.Equals(null);
        }

        // ========== 生命周期：取消事件订阅（防止内存泄漏） ==========
        private void OnDestroy()
        {
            if (GameMgr != null)
            {
                GameMgr.OnLanguageChanged -= UpdateAllTexts;
            }
            // 取消场景加载监听
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // ========== 废弃：动态绑定UI（保留但禁用，避免干扰） ==========
        private void BindUIComponents()
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null) return;

            // 原动态绑定逻辑（已禁用，建议完全删除或保留备份）
        }
    }
}