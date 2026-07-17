using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameLanguage
{
    English,
    SimplifiedChinese,
    Japanese
}

/// <summary>
/// Persistent localization service shared by every scene.  Text can be
/// translated either by attaching LocalizedText or by using one of the
/// registered UI labels below.
/// </summary>
public class GameLocalization : MonoBehaviour
{
    private const string PreferenceKey = "GameLanguage";
    private const string RuntimeObjectName = "Game Localization";

    public static GameLocalization Instance { get; private set; }
    public static event Action<GameLanguage> LanguageChanged;

    [SerializeField] private TMP_FontAsset cjkFont;
    [SerializeField] private TMP_FontAsset japaneseFont;

    private readonly Dictionary<string, string[]> translations = new Dictionary<string, string[]>
    {
        { "Start", new[] { "Start", "\u5f00\u59cb\u6e38\u620f", "\u30b2\u30fc\u30e0\u958b\u59cb" } },
        { "Setting", new[] { "Settings", "\u8bbe\u7f6e", "\u8a2d\u5b9a" } },
        { "Exit", new[] { "Exit", "\u9000\u51fa\u6e38\u620f", "\u7d42\u4e86" } },
        { "Music", new[] { "Music", "\u97f3\u4e50", "\u97f3\u697d" } },
        { "SFX", new[] { "Sound Effects", "\u97f3\u6548", "\u52b9\u679c\u97f3" } },
        { "FullScreen", new[] { "Full Screen", "\u5168\u5c4f", "\u30d5\u30eb\u30b9\u30af\u30ea\u30fc\u30f3" } },
        { "Save", new[] { "Save", "\u4fdd\u5b58", "\u4fdd\u5b58" } },
        { "T: Switch Player", new[] { "T: Switch Player", "T: \u5207\u6362\u89d2\u8272", "T: \u30ad\u30e3\u30e9\u30af\u30bf\u30fc\u5207\u66ff" } },
        { "K: Rune Panel", new[] { "K: Rune Panel", "K: \u7b26\u6587\u9762\u677f", "K: \u30eb\u30fc\u30f3\u30d1\u30cd\u30eb" } },
        { "I: Character Panel", new[] { "I: Character Panel", "I: \u89d2\u8272\u9762\u677f", "I: \u30ad\u30e3\u30e9\u30af\u30bf\u30fc\u30d1\u30cd\u30eb" } },
        { "Rune Panel", new[] { "Rune Panel", "\u7b26\u6587\u9762\u677f", "\u30eb\u30fc\u30f3\u30d1\u30cd\u30eb" } },
        { "Rune Bag", new[] { "Rune Bag", "\u7b26\u6587\u80cc\u5305", "\u30eb\u30fc\u30f3\u30d0\u30c3\u30b0" } },
        { "Rune Skill Panel", new[] { "Rune Skill Panel", "\u7b26\u6587\u6280\u80fd\u9762\u677f", "\u30eb\u30fc\u30f3\u30b9\u30ad\u30eb\u30d1\u30cd\u30eb" } },
        { "Description", new[] { "Description", "\u8bf4\u660e", "\u8aac\u660e" } },
        { "Description: -", new[] { "Description: -", "\u8bf4\u660e: -", "\u8aac\u660e: -" } },
        { "Effect: -", new[] { "Effect: -", "\u6548\u679c: -", "\u52b9\u679c: -" } },
        { "Type: -", new[] { "Type: -", "\u7c7b\u578b: -", "\u7a2e\u5225: -" } },
        { "Rune Name: None", new[] { "Rune Name: None", "\u7b26\u6587\u540d\u79f0: \u65e0", "\u30eb\u30fc\u30f3\u540d: \u306a\u3057" } },
        { "Selected Rune: None", new[] { "Selected Rune: None", "\u5df2\u9009\u7b26\u6587: \u65e0", "\u9078\u629e\u4e2d\u306e\u30eb\u30fc\u30f3: \u306a\u3057" } },
        { "No rune", new[] { "No rune", "\u65e0\u7b26\u6587", "\u30eb\u30fc\u30f3\u306a\u3057" } },
        { "Empty", new[] { "Empty", "\u7a7a", "\u7a7a\u304d" } },
        { "Hover a skill or rune to view details.", new[] { "Hover a skill or rune to view details.", "\u60ac\u505c\u5728\u6280\u80fd\u6216\u7b26\u6587\u4e0a\u67e5\u770b\u8be6\u60c5\u3002", "\u30b9\u30ad\u30eb\u307e\u305f\u306f\u30eb\u30fc\u30f3\u306b\u30de\u30a6\u30b9\u3092\u5408\u308f\u305b\u3066\u8a73\u7d30\u3092\u8868\u793a\u3002" } },
        { "Player Attributes", new[] { "Player Attributes", "\u89d2\u8272\u5c5e\u6027", "\u30d7\u30ec\u30a4\u30e4\u30fc\u80fd\u529b" } },
        { "Player", new[] { "Player", "\u89d2\u8272", "\u30d7\u30ec\u30a4\u30e4\u30fc" } },
        { "Buff / Rune / Skill Info Reserved", new[] { "Buff / Rune / Skill Info Reserved", "\u589e\u76ca / \u7b26\u6587 / \u6280\u80fd\u4fe1\u606f\u9884\u7559", "\u30d0\u30d5 / \u30eb\u30fc\u30f3 / \u30b9\u30ad\u30eb\u60c5\u5831\u4e88\u7d04" } },
        { "Heal +10", new[] { "Heal +10", "\u6cbb\u7597 +10", "\u56de\u5fa9 +10" } },
        { "Loading 0%", new[] { "Loading 0%", "\u52a0\u8f7d\u4e2d 0%", "\u30ed\u30fc\u30c9\u4e2d 0%" } },
        { "Restart", new[] { "Restart", "\u91cd\u65b0\u5f00\u59cb", "\u30ea\u30b9\u30bf\u30fc\u30c8" } },
        { "Main Menu", new[] { "Main Menu", "\u4e3b\u83dc\u5355", "\u30e1\u30a4\u30f3\u30e1\u30cb\u30e5\u30fc" } },
        { "DEFEAT", new[] { "DEFEAT", "\u5931\u8d25", "\u6557\u5317" } },
        { "DEFEAT!", new[] { "DEFEAT!", "\u5931\u8d25!", "\u6557\u5317!" } },
        { "you win!", new[] { "you win!", "\u4f60\u8d62\u4e86!", "\u52dd\u5229!" } },
        { "Victory", new[] { "Victory", "\u80dc\u5229", "\u52dd\u5229" } }
        ,{ "Life Rune", new[] { "Life Rune", "\u751f\u547d\u7b26\u6587", "\u751f\u547d\u30eb\u30fc\u30f3" } }
        ,{ "Shield Rune", new[] { "Shield Rune", "\u62a4\u76fe\u7b26\u6587", "\u30b7\u30fc\u30eb\u30c9\u30eb\u30fc\u30f3" } }
        ,{ "Mana Rune", new[] { "Mana Rune", "\u9b54\u529b\u7b26\u6587", "\u30de\u30ca\u30eb\u30fc\u30f3" } }
        ,{ "Thorn Rune", new[] { "Thorn Rune", "\u8346\u68d8\u7b26\u6587", "\u30bd\u30fc\u30f3\u30eb\u30fc\u30f3" } }
        ,{ "Luck Rune", new[] { "Luck Rune", "\u5e78\u8fd0\u7b26\u6587", "\u5e78\u904b\u30eb\u30fc\u30f3" } }
        ,{ "Common", new[] { "Common", "\u666e\u901a", "\u30b3\u30e2\u30f3" } }
        ,{ "Selected Rune", new[] { "Selected Rune", "\u5df2\u9009\u7b26\u6587", "\u9078\u629e\u4e2d\u306e\u30eb\u30fc\u30f3" } }
        ,{ "Rune Name", new[] { "Rune Name", "\u7b26\u6587\u540d\u79f0", "\u30eb\u30fc\u30f3\u540d" } }
        ,{ "Type", new[] { "Type", "\u7c7b\u578b", "\u7a2e\u5225" } }
        ,{ "Effect", new[] { "Effect", "\u6548\u679c", "\u52b9\u679c" } }
        ,{ "Attributes", new[] { "Attributes", "\u5c5e\u6027", "\u80fd\u529b" } }
        ,{ "Character Attributes", new[] { "Character Attributes", "\u89d2\u8272\u5c5e\u6027", "\u30ad\u30e3\u30e9\u30af\u30bf\u30fc\u80fd\u529b" } }
        ,{ "character.player01.name", new[] { "Spiritweave Doll", "\u7075\u7f57\u5a03\u5a03", "\u970a\u7f85\u4eba\u5f62" } }
        ,{ "character.player02.name", new[] { "Chosen Child", "\u795e\u7737\u4e4b\u5b50", "\u795e\u7737\u306e\u5b50" } }
        ,{ "character.attributes.title", new[] { "{0} Attributes", "{0}\u5c5e\u6027", "{0}\u306e\u80fd\u529b" } }
        ,{ "Character Preview", new[] { "Character Preview", "\u89d2\u8272\u9884\u89c8", "\u30ad\u30e3\u30e9\u30af\u30bf\u30fc\u30d7\u30ec\u30d3\u30e5\u30fc" } }
        ,{ "LUCK", new[] { "LUCK", "\u5e78\u8fd0", "\u904b" } }
        ,{ "Crit Rate", new[] { "Crit Rate", "\u66b4\u51fb\u7387", "\u30af\u30ea\u30c6\u30a3\u30ab\u30eb\u7387" } }
        ,{ "Extra Soul Drop", new[] { "Extra Soul Drop", "\u989d\u5916\u7075\u9b42\u6389\u843d", "\u8ffd\u52a0\u30bd\u30a6\u30eb\u30c9\u30ed\u30c3\u30d7" } }
        ,{ "Extra Rune Drop", new[] { "Extra Rune Drop", "\u989d\u5916\u7b26\u6587\u6389\u843d", "\u8ffd\u52a0\u30eb\u30fc\u30f3\u30c9\u30ed\u30c3\u30d7" } }
    };

    private readonly Dictionary<TextMeshProUGUI, TMP_FontAsset> originalFonts = new Dictionary<TextMeshProUGUI, TMP_FontAsset>();

    public GameLanguage CurrentLanguage { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        EnsureInstance();
    }

    public static GameLocalization EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameLocalization existing = FindObjectOfType<GameLocalization>();
        if (existing != null)
        {
            return existing;
        }

        GameObject localizationObject = new GameObject(RuntimeObjectName);
        return localizationObject.AddComponent<GameLocalization>();
    }

    public void SetCjkFont(TMP_FontAsset font)
    {
        if (font != null)
            cjkFont = font;

        ConfigureFallbackFonts();
        PreloadTranslationCharacters();
    }

    public void SetJapaneseFont(TMP_FontAsset font)
    {
        if (font != null)
            japaneseFont = font;

        ConfigureFallbackFonts();
        PreloadTranslationCharacters();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CurrentLanguage = (GameLanguage)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, 0), 0, 2);
        ConfigureFallbackFonts();
        PreloadTranslationCharacters();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ApplyToAllText();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }

    public void CycleLanguage()
    {
        SetLanguage((GameLanguage)(((int)CurrentLanguage + 1) % 3));
    }

    public void SetLanguage(GameLanguage language)
    {
        if (CurrentLanguage == language)
        {
            ApplyToAllText();
            return;
        }

        CurrentLanguage = language;
        PlayerPrefs.SetInt(PreferenceKey, (int)CurrentLanguage);
        PlayerPrefs.Save();
        ApplyToAllText();
        LanguageChanged?.Invoke(CurrentLanguage);
    }

    public string Translate(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        if (TryTranslate(key, out string translated))
        {
            return translated;
        }

        return key;
    }

    public string TranslateOrFallback(string key, string fallback)
    {
        return TryTranslate(key, out string translated) ? translated : fallback;
    }

    public string FormatOrFallback(string key, string fallbackFormat, params object[] args)
    {
        string format = TranslateOrFallback(key, fallbackFormat);
        return args == null || args.Length == 0 ? format : string.Format(format, args);
    }

    public bool TryTranslate(string key, out string translated)
    {
        translated = key;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        foreach (KeyValuePair<string, string[]> entry in translations)
        {
            if (entry.Key == key || Array.IndexOf(entry.Value, key) >= 0)
            {
                translated = entry.Value[(int)CurrentLanguage];
                return true;
            }
        }

        return false;
    }

    public void ApplyToText(TextMeshProUGUI text, string key = null)
    {
        if (text == null)
            return;

        string source = string.IsNullOrEmpty(key) ? text.text : key;
        string translated = Translate(source);
        if (translated == source)
            return;

        text.text = translated;
        ApplyFontForLanguage(text);
    }

    public void ApplyFontForLanguage(TextMeshProUGUI text)
    {
        if (text == null)
            return;

        if (!originalFonts.ContainsKey(text))
            originalFonts.Add(text, text.font);

        if (CurrentLanguage == GameLanguage.English)
        {
            text.font = originalFonts[text];
        }
        else
        {
            TMP_FontAsset primaryFont = CurrentLanguage == GameLanguage.Japanese ? japaneseFont : cjkFont;
            if (primaryFont != null)
                text.font = primaryFont;
        }
    }

    private void ConfigureFallbackFonts()
    {
        AddFallback(cjkFont, japaneseFont);
        AddFallback(japaneseFont, cjkFont);
    }

    private void PreloadTranslationCharacters()
    {
        if (translations == null || translations.Count == 0)
            return;

        StringBuilder characters = new StringBuilder();
        foreach (KeyValuePair<string, string[]> entry in translations)
        {
            foreach (string value in entry.Value)
                characters.Append(value);
        }

        string characterSet = characters.ToString();
        string missingCharacters;
        if (cjkFont != null)
            cjkFont.TryAddCharacters(characterSet, out missingCharacters);

        if (japaneseFont != null)
            japaneseFont.TryAddCharacters(characterSet, out missingCharacters);
    }

    private static void AddFallback(TMP_FontAsset primary, TMP_FontAsset fallback)
    {
        if (primary == null || fallback == null || primary == fallback)
            return;

        if (primary.fallbackFontAssetTable == null)
            primary.fallbackFontAssetTable = new List<TMP_FontAsset>();

        if (!primary.fallbackFontAssetTable.Contains(fallback))
            primary.fallbackFontAssetTable.Add(fallback);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToAllText();
        StartCoroutine(ApplyAfterSceneInitialization());
    }

    private IEnumerator ApplyAfterSceneInitialization()
    {
        yield return null;
        ApplyToAllText();
    }

    private void ApplyToAllText()
    {
        foreach (TextMeshProUGUI text in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
        {
            if (text != null && text.gameObject.scene.IsValid())
                ApplyToText(text);
        }
    }
}
