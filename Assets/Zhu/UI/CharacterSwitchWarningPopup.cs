using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterSwitchWarningPopup : MonoBehaviour
{
    private const string PopupName = "CharacterSwitchWarningPopup";
    private const string BackgroundName = "Background";
    private const string MessageTextName = "MessageText";
    private const float DefaultDuration = 2f;

    private static CharacterSwitchWarningPopup instance;

    [Header("Text Layout")]
    [SerializeField] private Vector2 messagePaddingMin = new Vector2(32f, 18f);
    [SerializeField] private Vector2 messagePaddingMax = new Vector2(32f, 18f);
    [SerializeField, Min(1f)] private float fontSizeMax = 30f;
    [SerializeField, Min(1f)] private float fontSizeMin = 20f;

    [Header("External References")]
    [SerializeField] private GameObject background;
    [SerializeField] private CanvasGroup popupCanvasGroup;
    [SerializeField] private TextMeshProUGUI externalMessageText;

    private CanvasGroup canvasGroup;
    private TextMeshProUGUI messageText;
    private Coroutine hideRoutine;
    private string activeKey;
    private string activeFallback;
    private bool subscribedToLanguageChanged;

    public static void ShowLocalized(string key, string fallback, float duration = DefaultDuration)
    {
        CharacterSwitchWarningPopup popup = GetOrCreate();
        if (popup == null)
        {
            return;
        }

        popup.Show(key, fallback, duration);
    }

    private static CharacterSwitchWarningPopup GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<CharacterSwitchWarningPopup>(true);
        if (instance != null)
        {
            return instance;
        }

        Canvas parentCanvas = ResolveParentCanvas();
        if (parentCanvas == null)
        {
            GameObject canvasObject = new GameObject("RuntimePopupCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            parentCanvas = canvasObject.GetComponent<Canvas>();
            parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            parentCanvas.sortingOrder = 500;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        GameObject popupObject = new GameObject(PopupName, typeof(RectTransform), typeof(CanvasGroup), typeof(CharacterSwitchWarningPopup));
        popupObject.transform.SetParent(parentCanvas.transform, false);
        popupObject.transform.SetAsLastSibling();

        RectTransform popupRect = popupObject.GetComponent<RectTransform>();
        popupRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupRect.pivot = new Vector2(0.5f, 0.5f);
        popupRect.anchoredPosition = new Vector2(0f, 260f);
        popupRect.sizeDelta = new Vector2(720f, 150f);

        CanvasGroup group = popupObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject backgroundObject = new GameObject(BackgroundName, typeof(RectTransform), typeof(Image), typeof(Outline));
        backgroundObject.transform.SetParent(popupObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0.02f, 0.08f, 0.16f, 0.88f);
        background.raycastTarget = false;

        Outline outline = backgroundObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.75f, 0.95f, 1f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textObject = new GameObject(MessageTextName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(popupObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(32f, 18f);
        textRect.offsetMax = new Vector2(-32f, -18f);

        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.enableAutoSizing = true;
        tmp.fontSize = 30f;
        tmp.fontSizeMax = 30f;
        tmp.fontSizeMin = 20f;
        tmp.margin = new Vector4(8f, 0f, 8f, 0f);
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.text = string.Empty;

        popupObject.SetActive(false);
        instance = popupObject.GetComponent<CharacterSwitchWarningPopup>();
        instance.BindRuntimeReferences(backgroundObject, group, tmp);
        return instance;
    }

    private static Canvas ResolveParentCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        Canvas fallback = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || !canvas.gameObject.scene.IsValid() || canvas.renderMode == RenderMode.WorldSpace)
            {
                continue;
            }

            if (fallback == null || canvas.sortingOrder >= fallback.sortingOrder)
            {
                fallback = canvas;
            }
        }

        return fallback;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        ResolveExternalReferences();
        ApplyExternalTextSettings();
        SubscribeLanguageChanged();
    }

    private void OnValidate()
    {
        ResolveExternalReferences();
        ApplyExternalTextSettings();
    }

    private void ResolveExternalReferences()
    {
        if (popupCanvasGroup != null)
        {
            canvasGroup = popupCanvasGroup;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (externalMessageText != null)
        {
            messageText = externalMessageText;
        }

        if (messageText == null)
        {
            messageText = transform.Find(BackgroundName + "/" + MessageTextName)?.GetComponent<TextMeshProUGUI>();
            if (messageText == null)
            {
                messageText = transform.Find(MessageTextName)?.GetComponent<TextMeshProUGUI>();
            }
        }

        if (background == null)
        {
            Transform backgroundTransform = transform.Find(BackgroundName);
            background = backgroundTransform != null ? backgroundTransform.gameObject : null;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (subscribedToLanguageChanged)
        {
            GameLocalization.LanguageChanged -= OnLanguageChanged;
            subscribedToLanguageChanged = false;
        }
    }

    private void BindRuntimeReferences(GameObject backgroundObject, CanvasGroup group, TextMeshProUGUI text)
    {
        background = backgroundObject;
        popupCanvasGroup = group;
        externalMessageText = text;
        canvasGroup = group;
        messageText = text;
        ApplyExternalTextSettings();
    }

    private void Show(string key, string fallback, float duration)
    {
        ResolveExternalReferences();
        SubscribeLanguageChanged();
        activeKey = key;
        activeFallback = fallback;
        ApplyMessageText();

        gameObject.SetActive(true);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
        }

        hideRoutine = StartCoroutine(HideAfterDelay(Mathf.Max(0.1f, duration)));
    }

    private IEnumerator HideAfterDelay(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        hideRoutine = null;
        HideImmediate();
    }

    private void HideImmediate()
    {
        ApplyCanvasGroupHiddenState();
        gameObject.SetActive(false);
    }

    private void SubscribeLanguageChanged()
    {
        if (subscribedToLanguageChanged)
        {
            return;
        }

        GameLocalization.LanguageChanged += OnLanguageChanged;
        subscribedToLanguageChanged = true;
    }

    private void ApplyCanvasGroupHiddenState()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void OnLanguageChanged(GameLanguage language)
    {
        if (isActiveAndEnabled)
        {
            ApplyMessageText();
        }
    }

    private void ApplyMessageText()
    {
        if (messageText == null)
        {
            return;
        }

        GameLocalization localization = GameLocalization.Instance;
        messageText.text = localization != null
            ? localization.TranslateOrFallback(activeKey, activeFallback)
            : activeFallback;

        if (localization != null)
        {
            localization.ApplyFontForLanguage(messageText);
        }
    }

    private void ApplyExternalTextSettings()
    {
        if (messageText == null)
        {
            return;
        }

        RectTransform textRect = messageText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = messagePaddingMin;
        textRect.offsetMax = new Vector2(-messagePaddingMax.x, -messagePaddingMax.y);

        messageText.alignment = TextAlignmentOptions.Center;
        messageText.enableWordWrapping = true;
        messageText.enableAutoSizing = true;
        messageText.fontSizeMax = fontSizeMax;
        messageText.fontSizeMin = Mathf.Min(fontSizeMin, fontSizeMax);
        messageText.margin = new Vector4(8f, 0f, 8f, 0f);
        messageText.overflowMode = TextOverflowModes.Overflow;
        messageText.raycastTarget = false;
    }
}
