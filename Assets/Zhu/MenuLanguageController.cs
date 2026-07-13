using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Provides the GameScene button for the persistent GameLocalization service.
/// </summary>
public class MenuLanguageController : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset cjkFont;
    [SerializeField] private TMP_FontAsset japaneseFont;

    private TextMeshProUGUI languageButtonLabel;

    private void Start()
    {
        CreateLanguageButton();
        GameLocalization localization = GetOrCreateLocalization();
        RefreshButtonLabel(localization.CurrentLanguage);
        GameLocalization.LanguageChanged += RefreshButtonLabel;
    }

    private void OnDestroy()
    {
        GameLocalization.LanguageChanged -= RefreshButtonLabel;
    }

    private void RefreshButtonLabel(GameLanguage language)
    {
        if (languageButtonLabel != null)
        {
            languageButtonLabel.text = new[] { "EN", "\u4e2d\u6587", "\u65e5\u672c\u8a9e" }[(int)language];
            GameLocalization.Instance?.ApplyFontForLanguage(languageButtonLabel);
        }
    }

    /// <summary>Button callback kept explicit so it can be inspected in play mode.</summary>
    public void CycleLanguage()
    {
        GameLocalization localization = GetOrCreateLocalization();
        localization.CycleLanguage();
        RefreshButtonLabel(localization.CurrentLanguage);
    }

    private GameLocalization GetOrCreateLocalization()
    {
        GameLocalization localization = GameLocalization.Instance;
        if (localization == null)
            localization = FindObjectOfType<GameLocalization>();

        if (localization == null)
        {
            GameObject localizationObject = new GameObject("Game Localization");
            localization = localizationObject.AddComponent<GameLocalization>();
        }

        localization.SetCjkFont(cjkFont);
        localization.SetJapaneseFont(japaneseFont);
        return localization;
    }

    private void CreateLanguageButton()
    {
        GameObject buttonObject = new GameObject("Language Button", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-32f, -32f);
        buttonRect.sizeDelta = new Vector2(128f, 48f);

        Image background = buttonObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.55f);
        background.raycastTarget = true;

        Button languageButton = buttonObject.GetComponent<Button>();
        ColorBlock colors = languageButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        languageButton.colors = colors;
        languageButton.onClick.AddListener(CycleLanguage);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;

        languageButtonLabel = labelObject.GetComponent<TextMeshProUGUI>();
        languageButtonLabel.alignment = TextAlignmentOptions.Center;
        languageButtonLabel.fontSize = 24f;
        languageButtonLabel.color = Color.white;
        languageButtonLabel.raycastTarget = false;
    }
}
