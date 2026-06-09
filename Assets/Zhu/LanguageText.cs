using UnityEngine;
using TMPro;

public class LanguageText : MonoBehaviour
{
    [Tooltip("对应GameManager里AddText的标识")]
    public string key;
    [Tooltip("手动拖入这个文字专用的TMP字体asset")]
    public TMP_FontAsset customFontCN;
    public TMP_FontAsset customFontEN;
    public TMP_FontAsset customFontJP;

    private TextMeshProUGUI tmp;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        RefreshContentAndFont();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLanguageChanged += RefreshContentAndFont;
        }
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLanguageChanged -= RefreshContentAndFont;
        }
    }

    void RefreshContentAndFont()
    {
        if (tmp == null || GameManager.Instance == null || string.IsNullOrEmpty(key)) return;

        string nowLang = GameManager.Instance.settings.language;
        // 切换对应语种字体
        switch (nowLang)
        {
            case "zh":
                if (customFontCN != null) tmp.font = customFontCN;
                break;
            case "en":
                if (customFontEN != null) tmp.font = customFontEN;
                break;
            case "ja":
                if (customFontJP != null) tmp.font = customFontJP;
                break;
        }
        // 切换文字内容
        tmp.text = GameManager.Instance.GetText(key);
    }
}