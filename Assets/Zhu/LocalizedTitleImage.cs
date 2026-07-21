using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LocalizedTitleImage : MonoBehaviour
{
    [System.Serializable]
    public struct TitleImageLayout
    {
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector3 localScale;
        public float rotationZ;
        public Color color;

        public static TitleImageLayout Default(Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            return new TitleImageLayout
            {
                anchoredPosition = anchoredPosition,
                sizeDelta = sizeDelta,
                localScale = Vector3.one,
                rotationZ = 0f,
                color = Color.white
            };
        }
    }

    [SerializeField] private Image targetImage;

    [Header("Localized Sprites")]
    [SerializeField] private Sprite chineseSprite;
    [SerializeField] private Sprite japaneseSprite;
    [SerializeField] private Sprite englishSprite;

    [Header("Per-Language Layout")]
    [SerializeField] private TitleImageLayout chineseLayout = TitleImageLayout.Default(new Vector2(0f, 345f), new Vector2(1100f, 280f));
    [SerializeField] private TitleImageLayout japaneseLayout = TitleImageLayout.Default(new Vector2(0f, 345f), new Vector2(1200f, 300f));
    [SerializeField] private TitleImageLayout englishLayout = TitleImageLayout.Default(new Vector2(0f, 345f), new Vector2(1500f, 320f));

    [Header("Rendering")]
    [SerializeField] private Material titleMaterial;
    [SerializeField] private bool preserveAspect = true;
    [SerializeField] private bool setNativeSizeOnRefresh = false;
    [SerializeField] private bool raycastTarget = false;
    [SerializeField] private bool debugLog = false;

    private void Reset()
    {
        targetImage = GetComponent<Image>();
    }

    private void Awake()
    {
        ResolveImage();
    }

    private void OnEnable()
    {
        GameLocalization.LanguageChanged += HandleLanguageChanged;
        Refresh();
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= HandleLanguageChanged;
    }

    public void Refresh()
    {
        Image image = ResolveImage();
        if (image == null)
            return;

        GameLanguage language = ResolveLanguage();
        Sprite sprite = ResolveSprite(language);
        TitleImageLayout layout = ResolveLayout(language);

        if (sprite != null)
            image.sprite = sprite;
        else if (debugLog)
            Debug.LogWarning($"[LocalizedTitleImage] Missing title sprite for language={language}.", this);

        image.material = titleMaterial;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = raycastTarget;
        image.color = layout.color;

        RectTransform rect = image.rectTransform;
        rect.anchoredPosition = layout.anchoredPosition;
        rect.sizeDelta = layout.sizeDelta;
        rect.localScale = layout.localScale;
        rect.localRotation = Quaternion.Euler(0f, 0f, layout.rotationZ);

        if (setNativeSizeOnRefresh && image.sprite != null)
            image.SetNativeSize();

        if (debugLog)
            Debug.Log($"[LocalizedTitleImage] language={language} sprite={(image.sprite != null ? image.sprite.name : "None")} size={rect.sizeDelta} position={rect.anchoredPosition}", this);
    }

    private void HandleLanguageChanged(GameLanguage language)
    {
        Refresh();
    }

    private Image ResolveImage()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        return targetImage;
    }

    private static GameLanguage ResolveLanguage()
    {
        GameLocalization localization = GameLocalization.Instance != null
            ? GameLocalization.Instance
            : GameLocalization.EnsureInstance();

        return localization != null ? localization.CurrentLanguage : GameLanguage.SimplifiedChinese;
    }

    private Sprite ResolveSprite(GameLanguage language)
    {
        switch (language)
        {
            case GameLanguage.English:
                return englishSprite != null ? englishSprite : chineseSprite;
            case GameLanguage.Japanese:
                return japaneseSprite != null ? japaneseSprite : chineseSprite;
            default:
                return chineseSprite;
        }
    }

    private TitleImageLayout ResolveLayout(GameLanguage language)
    {
        switch (language)
        {
            case GameLanguage.English:
                return englishLayout;
            case GameLanguage.Japanese:
                return japaneseLayout;
            default:
                return chineseLayout;
        }
    }
}
