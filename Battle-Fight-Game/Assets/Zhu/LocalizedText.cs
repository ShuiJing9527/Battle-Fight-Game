using UnityEngine;
using TMPro;

/// <summary>
/// 通用多语言文本适配脚本
/// 【挂载位置】：所有需要多语言的 TextMeshProUGUI 组件上
/// 【使用方法】：在 Inspector 面板的 localizationKey 中，填入 GameManager.InitializeLocalization() 里的对应 Key
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizedText : MonoBehaviour
{
    [Header("【必填】GameManager 里的本地化 Key（大小写敏感！）")]
    [Tooltip("比如：button_start、label_music、loadbar_loading")]
    public string localizationKey;

    private TextMeshProUGUI _tmpText;

    void OnEnable()
    {
        // 获取 TMP 组件
        _tmpText = GetComponent<TextMeshProUGUI>();
        if (_tmpText == null)
        {
            Debug.LogError($"【{gameObject.name}】缺少 TextMeshProUGUI 组件！", this);
            enabled = false;
            return;
        }

        // 空值保护：GameManager 未初始化时不订阅
        if (GameManager.Instance == null)
        {
            Debug.LogError($"【{gameObject.name}】GameManager.Instance 为空！请确保 GameManager 先于 UI 初始化。", this);
            enabled = false;
            return;
        }

        // 订阅语言切换事件
        GameManager.Instance.OnLanguageChanged += RefreshLocalizedText;
        // 初始化时刷新一次文本
        RefreshLocalizedText();
    }

    void OnDisable()
    {
        // 取消订阅事件，避免内存泄漏
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLanguageChanged -= RefreshLocalizedText;
        }
    }

    /// <summary>
    /// 刷新多语言文本
    /// </summary>
    private void RefreshLocalizedText()
    {
        if (_tmpText == null || string.IsNullOrEmpty(localizationKey)) return;
        if (GameManager.Instance == null) return;

        // 从 GameManager 获取对应语言的文本
        _tmpText.text = GameManager.Instance.GetLocalizedText(localizationKey);
    }
}