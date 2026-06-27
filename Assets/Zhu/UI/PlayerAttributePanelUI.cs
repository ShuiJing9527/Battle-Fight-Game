using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerAttributePanelUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private KeyCode toggleKey = KeyCode.I;

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(760f, 360f);
    [SerializeField] private Vector2 panelAnchoredPosition = new Vector2(0f, 0f);
    [SerializeField] private Vector2 panelPadding = new Vector2(20f, 18f);
    [SerializeField] private float previewWidth = 220f;
    [SerializeField] private float sectionSpacing = 20f;
    [SerializeField] private float statsTitleHeight = 32f;
    [SerializeField] private float attributeRowHeight = 28f;
    [SerializeField] private float attributeRowSpacing = 12f;
    [SerializeField] private float footerHeight = 110f;
    [SerializeField] private float attributeLabelWidth = 48f;
    [SerializeField] private float attributeValueWidth = 72f;

    [Header("Colors")]
    [SerializeField] private Color panelColor = new Color(0.08f, 0.1f, 0.14f, 0.96f);
    [SerializeField] private Color previewColor = new Color(0.12f, 0.14f, 0.2f, 0.95f);
    [SerializeField] private Color barBackgroundColor = new Color(0.16f, 0.18f, 0.24f, 1f);
    [SerializeField] private Color barFillColor = new Color(0.92f, 0.76f, 0.30f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color subTextColor = new Color(0.84f, 0.88f, 0.95f, 1f);

    [Header("Bar Display Max")]
    [SerializeField, Min(1f)] private float hpDisplayMax = 300f;
    [SerializeField, Min(1f)] private float atkDisplayMax = 40f;
    [SerializeField, Min(1f)] private float defDisplayMax = 30f;
    [SerializeField, Min(1f)] private float magDisplayMax = 40f;
    [SerializeField, Min(1f)] private float resDisplayMax = 30f;

    private readonly string[] attributeKeys = { "HP", "ATK", "DEF", "MAG", "RES" };
    private readonly Image[] attributeFills = new Image[5];
    private readonly TextMeshProUGUI[] attributeValues = new TextMeshProUGUI[5];

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI previewText;
    private TextMeshProUGUI footerText;
    private RectTransform previewRect;
    private RectTransform statsRect;
    private bool initialized;
    private Player2Bootstrap cachedBootstrap;
    private GameObject cachedPlayer;
    private CombatStats cachedStats;
    private BattleResourceBank cachedResourceBank;
    private CombatHealth cachedCombatHealth;
    private RuntimeLootDropOnDeath cachedLootDropPreview;
    private float nextBootstrapLookupTime;

    private void Awake()
    {
        Initialize();
        SetVisible(false);
    }

    private void Start()
    {
        Initialize();
        SetVisible(false);
    }

    private void Update()
    {
        if (!initialized)
        {
            Initialize();
            if (!initialized)
            {
                return;
            }
        }

        if (Input.GetKeyDown(toggleKey))
        {
            TogglePanel();
        }

        if (panelRoot != null && panelRoot.gameObject.activeSelf)
        {
            RefreshPlayerCache(force: false);
            RefreshPanel();
        }
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            return;
        }

        if (panelRoot == null)
        {
            Transform existing = canvas.transform.Find("CharacterAttributePanel");
            if (existing != null)
            {
                panelRoot = existing as RectTransform;
            }
        }

        if (panelRoot == null)
        {
            panelRoot = CreateRectTransform("CharacterAttributePanel", canvas.transform);
            panelRoot.gameObject.AddComponent<Image>().color = panelColor;
        }

        BuildPanelIfNeeded();
        ApplyLayout();
        RefreshPlayerCache(force: true);
        RefreshPanel();
        initialized = true;
    }

    private Canvas ResolveCanvas()
    {
        if (targetCanvas != null)
        {
            return targetCanvas;
        }

        targetCanvas = GetComponent<Canvas>();
        if (targetCanvas != null)
        {
            return targetCanvas;
        }

        targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas != null)
        {
            return targetCanvas;
        }

        PlayerStatusHUD statusHud = FindObjectOfType<PlayerStatusHUD>();
        if (statusHud != null)
        {
            targetCanvas = statusHud.GetComponentInParent<Canvas>();
            if (targetCanvas != null)
            {
                return targetCanvas;
            }
        }

        PlayerSkillHUD skillHud = FindObjectOfType<PlayerSkillHUD>();
        if (skillHud != null)
        {
            targetCanvas = skillHud.GetComponentInParent<Canvas>();
            if (targetCanvas != null)
            {
                return targetCanvas;
            }
        }

        targetCanvas = FindObjectOfType<Canvas>();
        if (targetCanvas != null)
        {
            return targetCanvas;
        }

        GameObject canvasObject = new GameObject("HUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas createdCanvas = canvasObject.GetComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        targetCanvas = createdCanvas;
        return targetCanvas;
    }

    private void BuildPanelIfNeeded()
    {
        if (panelRoot == null)
        {
            return;
        }

        Image rootImage = panelRoot.GetComponent<Image>();
        if (rootImage == null)
        {
            rootImage = panelRoot.gameObject.AddComponent<Image>();
        }

        rootImage.color = panelColor;
        rootImage.raycastTarget = false;

        previewRect = FindRect(panelRoot, "PreviewSection") ?? CreateRectTransform("PreviewSection", panelRoot);
        Image previewImage = previewRect.GetComponent<Image>();
        if (previewImage == null)
        {
            previewImage = previewRect.gameObject.AddComponent<Image>();
        }

        previewImage.color = previewColor;
        previewImage.raycastTarget = false;

        previewText = FindOrCreateText(previewRect, "PreviewLabel", 26f, TextAlignmentOptions.Center, textColor);
        previewText.enableWordWrapping = true;

        statsRect = FindRect(panelRoot, "StatsSection") ?? CreateRectTransform("StatsSection", panelRoot);

        titleText = FindOrCreateText(statsRect, "StatsTitle", 26f, TextAlignmentOptions.MidlineLeft, textColor);
        titleText.enableWordWrapping = false;

        for (int i = 0; i < attributeKeys.Length; i++)
        {
            EnsureAttributeRow(i, attributeKeys[i]);
        }

        footerText = FindOrCreateText(statsRect, "FooterText", 18f, TextAlignmentOptions.TopLeft, subTextColor);
        footerText.enableWordWrapping = true;
        footerText.overflowMode = TextOverflowModes.Overflow;
    }

    private void EnsureAttributeRow(int index, string key)
    {
        RectTransform row = FindRect(statsRect, $"{key}Row") ?? CreateRectTransform($"{key}Row", statsRect);

        TextMeshProUGUI label = FindOrCreateText(row, "Label", 20f, TextAlignmentOptions.MidlineLeft, textColor);
        label.text = key;
        label.enableWordWrapping = false;

        RectTransform backgroundRect = FindRect(row, "BarBackground") ?? CreateRectTransform("BarBackground", row);
        Image backgroundImage = backgroundRect.GetComponent<Image>();
        if (backgroundImage == null)
        {
            backgroundImage = backgroundRect.gameObject.AddComponent<Image>();
        }

        backgroundImage.color = barBackgroundColor;
        backgroundImage.raycastTarget = false;

        RectTransform fillRect = FindRect(backgroundRect, "BarFill") ?? CreateRectTransform("BarFill", backgroundRect);
        Image fillImage = fillRect.GetComponent<Image>();
        if (fillImage == null)
        {
            fillImage = fillRect.gameObject.AddComponent<Image>();
        }

        fillImage.color = barFillColor;
        fillImage.raycastTarget = false;

        TextMeshProUGUI value = FindOrCreateText(row, "Value", 20f, TextAlignmentOptions.MidlineRight, textColor);
        value.enableWordWrapping = false;

        attributeFills[index] = fillImage;
        attributeValues[index] = value;
    }

    private void ApplyLayout()
    {
        if (panelRoot == null || previewRect == null || statsRect == null)
        {
            return;
        }

        panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
        panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
        panelRoot.pivot = new Vector2(0.5f, 0.5f);
        panelRoot.anchoredPosition = panelAnchoredPosition;
        panelRoot.sizeDelta = panelSize;
        panelRoot.localScale = Vector3.one;

        previewRect.anchorMin = new Vector2(0f, 0f);
        previewRect.anchorMax = new Vector2(0f, 1f);
        previewRect.pivot = new Vector2(0f, 0.5f);
        previewRect.offsetMin = new Vector2(panelPadding.x, panelPadding.y);
        previewRect.offsetMax = new Vector2(panelPadding.x + previewWidth, -panelPadding.y);

        if (previewText != null)
        {
            RectTransform previewTextRect = previewText.rectTransform;
            previewTextRect.anchorMin = Vector2.zero;
            previewTextRect.anchorMax = Vector2.one;
            previewTextRect.offsetMin = new Vector2(16f, 16f);
            previewTextRect.offsetMax = new Vector2(-16f, -16f);
        }

        statsRect.anchorMin = new Vector2(0f, 0f);
        statsRect.anchorMax = new Vector2(1f, 1f);
        statsRect.offsetMin = new Vector2(panelPadding.x + previewWidth + sectionSpacing, panelPadding.y);
        statsRect.offsetMax = new Vector2(-panelPadding.x, -panelPadding.y);

        if (titleText != null)
        {
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(0f, -statsTitleHeight);
            titleRect.offsetMax = new Vector2(0f, 0f);
        }

        float statsWidth = Mathf.Max(240f, panelSize.x - (panelPadding.x * 2f) - previewWidth - sectionSpacing);
        float barStartX = attributeLabelWidth + 12f;
        float barWidth = Mathf.Max(100f, statsWidth - barStartX - attributeValueWidth - 12f);
        float firstRowTop = -statsTitleHeight - 14f;

        for (int i = 0; i < attributeKeys.Length; i++)
        {
            RectTransform row = FindRect(statsRect, $"{attributeKeys[i]}Row");
            if (row == null)
            {
                continue;
            }

            float top = firstRowTop - i * (attributeRowHeight + attributeRowSpacing);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.offsetMin = new Vector2(0f, top - attributeRowHeight);
            row.offsetMax = new Vector2(0f, top);

            RectTransform labelRect = FindRect(row, "Label");
            if (labelRect != null)
            {
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(0f, 1f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.sizeDelta = new Vector2(attributeLabelWidth, 0f);
            }

            RectTransform backgroundRect = FindRect(row, "BarBackground");
            if (backgroundRect != null)
            {
                backgroundRect.anchorMin = new Vector2(0f, 0.5f);
                backgroundRect.anchorMax = new Vector2(0f, 0.5f);
                backgroundRect.pivot = new Vector2(0f, 0.5f);
                backgroundRect.anchoredPosition = new Vector2(barStartX, 0f);
                backgroundRect.sizeDelta = new Vector2(barWidth, attributeRowHeight - 6f);
            }

            RectTransform fillRect = backgroundRect != null ? FindRect(backgroundRect, "BarFill") : null;
            if (fillRect != null)
            {
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(1f, 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
            }

            RectTransform valueRect = FindRect(row, "Value");
            if (valueRect != null)
            {
                valueRect.anchorMin = new Vector2(1f, 0f);
                valueRect.anchorMax = new Vector2(1f, 1f);
                valueRect.pivot = new Vector2(1f, 0.5f);
                valueRect.anchoredPosition = Vector2.zero;
                valueRect.sizeDelta = new Vector2(attributeValueWidth, 0f);
            }
        }

        if (footerText != null)
        {
            RectTransform footerRect = footerText.rectTransform;
            footerRect.anchorMin = new Vector2(0f, 0f);
            footerRect.anchorMax = new Vector2(1f, 0f);
            footerRect.pivot = new Vector2(0.5f, 0f);
            footerRect.offsetMin = new Vector2(0f, 0f);
            footerRect.offsetMax = new Vector2(0f, footerHeight);
        }
    }

    private void TogglePanel()
    {
        bool nextVisible = panelRoot == null || !panelRoot.gameObject.activeSelf;
        if (nextVisible)
        {
            CloseRunePanelIfOpen();
            RefreshPlayerCache(force: true);
            RefreshPanel();
        }

        SetVisible(nextVisible);
    }

    private void SetVisible(bool visible)
    {
        if (panelRoot != null)
        {
            panelRoot.gameObject.SetActive(visible);
        }
    }

    private void CloseRunePanelIfOpen()
    {
        RuneUIController runeUi = FindObjectOfType<RuneUIController>(true);
        if (runeUi != null && runeUi.mainPanel != null && runeUi.mainPanel.activeSelf)
        {
            runeUi.ClosePanel();
        }
    }

    private void RefreshPlayerCache(bool force)
    {
        if ((force || cachedBootstrap == null) && Time.unscaledTime >= nextBootstrapLookupTime)
        {
            cachedBootstrap = FindObjectOfType<Player2Bootstrap>();
            nextBootstrapLookupTime = Time.unscaledTime + 1f;
        }

        GameObject currentPlayer = cachedBootstrap != null ? cachedBootstrap.CurrentPlayer : null;
        if (currentPlayer == null)
        {
            GameObject taggedPlayer = GameObject.FindWithTag("Player");
            if (taggedPlayer != null && taggedPlayer.activeInHierarchy)
            {
                currentPlayer = taggedPlayer;
            }
        }

        if (!force && currentPlayer == cachedPlayer)
        {
            return;
        }

        cachedPlayer = currentPlayer;
        cachedStats = cachedPlayer != null ? BattleStatUtility.GetCombatStats(cachedPlayer) : null;
        cachedResourceBank = cachedPlayer != null ? cachedPlayer.GetComponent<BattleResourceBank>() : null;
        cachedCombatHealth = cachedPlayer != null ? cachedPlayer.GetComponent<CombatHealth>() : null;
    }

    private void RefreshPanel()
    {
        if (panelRoot == null)
        {
            return;
        }

        ApplyLayout();

        float hpCurrent = ResolveCurrentHealth();
        float hpMax = cachedStats != null ? Mathf.Max(1f, cachedStats.maxHealth) : Mathf.Max(1f, hpCurrent);
        float atk = cachedStats != null ? Mathf.Max(0f, cachedStats.physicalAttack) : 0f;
        float def = cachedStats != null ? Mathf.Max(0f, cachedStats.physicalDefense) : 0f;
        float mag = cachedStats != null ? Mathf.Max(0f, cachedStats.specialAttack) : 0f;
        float res = cachedStats != null ? Mathf.Max(0f, cachedStats.specialDefense) : 0f;
        float speed = cachedStats != null ? Mathf.Max(0f, cachedStats.speed) : 0f;
        float luck = cachedStats != null ? Mathf.Max(0f, cachedStats.luck) : 0f;
        float critRate = BattleStatUtility.GetCritRate(cachedStats) * 100f;
        float extraSoulDrop = ResolveExtraSoulDropChance(luck) * 100f;
        float extraRuneDrop = ResolveExtraRuneDropChance(luck) * 100f;

        if (titleText != null)
        {
            titleText.text = cachedPlayer != null ? $"{cachedPlayer.name} Attributes" : "Character Attributes";
        }

        if (previewText != null)
        {
            previewText.text = cachedPlayer != null
                ? $"{cachedPlayer.name}\n\nCharacter Preview"
                : "Character Preview";
        }

        SetAttributeDisplay(0, $"HP {Mathf.CeilToInt(hpCurrent)}/{Mathf.CeilToInt(hpMax)}", hpMax, hpDisplayMax);
        SetAttributeDisplay(1, $"ATK {Mathf.RoundToInt(atk)}", atk, atkDisplayMax);
        SetAttributeDisplay(2, $"DEF {Mathf.RoundToInt(def)}", def, defDisplayMax);
        SetAttributeDisplay(3, $"MAG {Mathf.RoundToInt(mag)}", mag, magDisplayMax);
        SetAttributeDisplay(4, $"RES {Mathf.RoundToInt(res)}", res, resDisplayMax);

        if (footerText != null)
        {
            footerText.text =
                $"SPD  {speed:0.0}\n" +
                $"LUCK {luck:0}\n" +
                $"Crit Rate        {critRate:0.#}%\n" +
                $"Extra Soul Drop  {extraSoulDrop:0.#}%\n" +
                $"Extra Rune Drop  {extraRuneDrop:0.#}%\n" +
                $"Buff / Rune / Skill Info Reserved";
        }
    }

    private float ResolveCurrentHealth()
    {
        if (cachedResourceBank != null)
        {
            return Mathf.Max(0f, cachedResourceBank.currentHealth);
        }

        if (cachedCombatHealth != null)
        {
            return Mathf.Max(0f, cachedCombatHealth.currentHealth);
        }

        return cachedStats != null ? Mathf.Max(0f, cachedStats.maxHealth) : 0f;
    }

    private void SetAttributeDisplay(int index, string valueLabel, float value, float displayMax)
    {
        if (index < 0 || index >= attributeValues.Length)
        {
            return;
        }

        if (attributeValues[index] != null)
        {
            attributeValues[index].text = valueLabel;
        }

        if (attributeFills[index] == null)
        {
            return;
        }

        float ratio = displayMax > 0f ? Mathf.Clamp01(Mathf.Max(0f, value) / displayMax) : 0f;
        RectTransform rect = attributeFills[index].rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(ratio, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private float ResolveExtraSoulDropChance(float luck)
    {
        RuntimeLootDropOnDeath preview = ResolveLootDropPreview();
        return preview != null
            ? preview.GetExtraSoulDropChanceForLuck(luck)
            : Mathf.Clamp(Mathf.Max(0f, luck) * 0.01f, 0f, 0.5f);
    }

    private float ResolveExtraRuneDropChance(float luck)
    {
        RuntimeLootDropOnDeath preview = ResolveLootDropPreview();
        return preview != null
            ? preview.GetExtraRuneDropChanceForLuck(luck)
            : Mathf.Clamp(Mathf.Max(0f, luck) * 0.005f, 0f, 0.3f);
    }

    private RuntimeLootDropOnDeath ResolveLootDropPreview()
    {
        if (cachedLootDropPreview == null)
        {
            cachedLootDropPreview = FindObjectOfType<RuntimeLootDropOnDeath>(true);
        }

        return cachedLootDropPreview;
    }

    private static RectTransform CreateRectTransform(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static RectTransform FindRect(Transform parent, string name)
    {
        Transform child = parent != null ? parent.Find(name) : null;
        return child as RectTransform;
    }

    private static TextMeshProUGUI FindOrCreateText(Transform parent, string name, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = FindRect(parent, name);
        TextMeshProUGUI text = rect != null ? rect.GetComponent<TextMeshProUGUI>() : null;
        if (text == null)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
        }

        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.text = string.Empty;
        return text;
    }
}
