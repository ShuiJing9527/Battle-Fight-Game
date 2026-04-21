using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// 极简加载条控制脚本（适配 Unity6 + GameManager + SceneLoader）
/// 【挂载位置】：开始界面的 Canvas 上
/// 【前置依赖】：Canvas 下有 LoadBar_Bg/LoadBar_Progress/LoadText/StartButton
/// </summary>
public class SimpleLoadBar : MonoBehaviour
{
    [Header("【必填】加载条 UI 组件")]
    [Tooltip("进度条 Image（必须是 Filled 模式！）")]
    public Image loadBar_Progress;
    [Tooltip("进度百分比文本（建议挂 LocalizedText 脚本，Key=loadbar_loading）")]
    public TextMeshProUGUI loadText;
    [Tooltip("开始游戏按钮（初始隐藏！）")]
    public GameObject startButton;

    [Header("【必填】加载配置")]
    [Tooltip("模拟加载时长（秒），测试用")]
    public float simulateLoadTime = 3f;
    [Tooltip("游戏主场景名（必须和 Build Settings 里的完全一致！）")]
    public string gameSceneName = "GameScene";

    private GameManager _gameManager;
    private float _loadTimer;
    private bool _isLoading = true;

    void Start()
    {
        // 初始化 GameManager
        _gameManager = GameManager.Instance;
        if (_gameManager == null)
        {
            Debug.LogError("【SimpleLoadBar】GameManager.Instance 为空！请确保 GameManager 先于 UI 初始化。", this);
            enabled = false;
            return;
        }

        // 初始化加载条
        if (loadBar_Progress != null)
        {
            loadBar_Progress.fillAmount = 0;
        }
        if (startButton != null)
        {
            startButton.SetActive(false); // 初始隐藏开始按钮
        }

        // 订阅语言切换事件（加载条文本自动刷新）
        _gameManager.OnLanguageChanged += RefreshLoadText;
        // 初始化文本
        RefreshLoadText();
    }

    void Update()
    {
        if (!_isLoading || _gameManager == null) return;
        if (loadBar_Progress == null || loadText == null) return;

        // 模拟加载进度
        _loadTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(_loadTimer / simulateLoadTime);
        loadBar_Progress.fillAmount = progress;

        // 更新加载文本（多语言 Key 已通过 LocalizedText 脚本处理，这里只加百分比）
        string loadingKey = _gameManager.GetLocalizedText("loadbar_loading");
        loadText.text = $"{loadingKey} {Mathf.Round(progress * 100)}%";

        // 加载完成
        if (progress >= 1)
        {
            _isLoading = false;
            string completeKey = _gameManager.GetLocalizedText("loadbar_complete");
            loadText.text = completeKey;
            if (startButton != null)
            {
                startButton.SetActive(true); // 显示开始按钮
            }
        }
    }

    /// <summary>
    /// 刷新加载条多语言文本（语言切换时调用）
    /// </summary>
    private void RefreshLoadText()
    {
        if (loadText == null || _gameManager == null) return;

        if (_isLoading)
        {
            string loadingKey = _gameManager.GetLocalizedText("loadbar_loading");
            loadText.text = $"{loadingKey} 0%";
        }
        else
        {
            string completeKey = _gameManager.GetLocalizedText("loadbar_complete");
            loadText.text = completeKey;
        }

        // 刷新加载按钮文本（如果按钮文本挂了 LocalizedText 脚本，这里可以省略）
        if (startButton != null)
        {
            TextMeshProUGUI btnText = startButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                // 如果按钮文本没挂 LocalizedText，这里手动获取；挂了的话可以注释掉
                btnText.text = _gameManager.GetLocalizedText("loadbar_start_game");
            }
        }
    }

    /// <summary>
    /// 开始按钮点击事件（供 UI 绑定）
    /// 【绑定方法】：选中 StartButton → On Click () → 拖拽 Canvas → 选择 SimpleLoadBar → OnStartButtonClick()
    /// </summary>
    public void OnStartButtonClick()
    {
        if (_gameManager == null)
        {
            // 兜底：直接加载场景
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        // 优先找挂在 GameManager 物体上的 SceneLoader
        SceneLoader sceneLoader = _gameManager.GetComponent<SceneLoader>();
        if (sceneLoader != null)
        {
            sceneLoader.LoadScene(gameSceneName);
        }
        else
        {
            // 兜底：直接加载场景
            SceneManager.LoadScene(gameSceneName);
        }
    }

    /// <summary>
    /// 取消订阅事件（防止内存泄漏）
    /// </summary>
    private void OnDestroy()
    {
        if (_gameManager != null)
        {
            _gameManager.OnLanguageChanged -= RefreshLoadText;
        }
    }
}