using UnityEngine;
using TMPro;

public class LocalizedText : MonoBehaviour
{
    [Tooltip("Translation key. Leave empty to use Show Text as the key.")]
    public string key;
    public string showText;
    public TMP_FontAsset targetFont;
    private TextMeshProUGUI tmp;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        GameLocalization.LanguageChanged += OnLanguageChanged;
        ApplyText();
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= OnLanguageChanged;
    }

    private void Start()
    {
        ApplyText();
    }

    private void OnLanguageChanged(GameLanguage language)
    {
        ApplyText();
    }

    private void ApplyText()
    {
        if (tmp == null) return;
        tmp.text = GameLocalization.Instance != null
            ? GameLocalization.Instance.Translate(string.IsNullOrEmpty(key) ? showText : key)
            : showText;
        if (targetFont != null) tmp.font = targetFont;
        GameLocalization.Instance?.ApplyToText(tmp, string.IsNullOrEmpty(key) ? showText : key);
    }
}
