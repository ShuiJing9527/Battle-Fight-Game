using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;

[System.Serializable]
public class SkillIconSet
{
    public Sprite qIcon;
    public Sprite wIcon;
    public Sprite eIcon;
    public Sprite rIcon;

    public string qKeyText = "Q";
    public string wKeyText = "W";
    public string eKeyText = "E";
    public string rKeyText = "R";
}

public class PlayerSkillHUD : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private RectTransform skillHudRoot;
    [SerializeField] private Canvas targetCanvas;

    [Header("Prefabs")]
    [SerializeField] private GameObject skillSlotPrefab;

    [Header("Default Player")]
    [SerializeField] private int defaultPlayerIndex = 1;

    [Header("Player 01 Icons")]
    [SerializeField] private SkillIconSet player01Icons = new SkillIconSet();

    [Header("Player 02 Icons")]
    [SerializeField] private SkillIconSet player02Icons = new SkillIconSet();

    [Header("Layout")]
    [SerializeField] private Vector2 rootAnchoredPosition = new Vector2(-80f, 70f);
    [SerializeField] private float slotSize = 80f;
    [SerializeField] private float slotSpacing = 16f;
    [SerializeField] private int keyLabelFontSize = 20;

    [Header("Colors")]
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.92f);
    [SerializeField] private Color iconColor = new Color(0.85f, 0.87f, 0.92f, 1f);
    [SerializeField] private Color cooldownIconDimColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private bool useIconDimOnCooldown = false;
    [SerializeField] private Color cooldownOverlayColor = new Color(0f, 0f, 0f, 0.45f);
    [SerializeField] private bool cooldownFillClockwise = true;
    [SerializeField] private int cooldownFillOrigin = 2;
    [SerializeField] private Color keyLabelColor = Color.white;
    [SerializeField] private bool showCooldownText = true;
    [SerializeField] private int cooldownTextFontSize = 36;
    [SerializeField] private Color cooldownTextColor = Color.white;
    [SerializeField] private bool cooldownTextUseOutline = true;
    [SerializeField] private Color cooldownTextOutlineColor = Color.black;
    [SerializeField] private bool cooldownOverlayDiagnosticMode = true;
    [SerializeField] private bool enableCooldownDebugKeys = false;

    [Header("Hover")]
    [SerializeField] private Color hoverHighlightColor = new Color(1f, 0.9f, 0.35f, 0.5f);
    [SerializeField] private float hoverHighlightScale = 1.18f;
    [SerializeField] private Color tooltipBackgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.96f);
    [SerializeField] private Color tooltipTextColor = Color.white;
    [SerializeField] private int tooltipFontSize = 18;
    [SerializeField] private float tooltipWidth = 340f;
    [SerializeField] private Vector2 tooltipPadding = new Vector2(12f, 10f);
    [SerializeField] private Vector2 tooltipOffset = new Vector2(0f, 26f);

    private readonly Image[] slotIconImages = new Image[4];
    private readonly Image[] slotCooldownOverlays = new Image[4];
    private readonly Text[] slotCooldownTexts = new Text[4];
    private readonly Text[] slotKeyLabels = new Text[4];
    private readonly Image[] slotHoverHighlights = new Image[4];
    private readonly SkillHoverTrigger[] slotHoverTriggers = new SkillHoverTrigger[4];
    private readonly float[] cooldownDurations = new float[4];
    private readonly float[] cooldownRemaining = new float[4];
    private readonly bool[] cooldownWasActive = new bool[4];
    private static Sprite sharedCooldownCircleSprite;
    private bool initialized;
    private int currentPlayerIndex;
    private RectTransform canvasRectTransform;
    private RectTransform tooltipRoot;
    private TextMeshProUGUI tooltipText;

    private void Awake()
    {
        Initialize();
    }

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        HandleDebugCooldownKeys();
        UpdateCooldownVisuals(Time.deltaTime);
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

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            return;
        }

        canvasRectTransform = canvasRect;

        if (skillHudRoot == null)
        {
            Transform existing = canvas.transform.Find("SkillHUDRoot");
            if (existing != null)
            {
                skillHudRoot = existing as RectTransform;
            }
        }

        if (skillHudRoot == null)
        {
            skillHudRoot = CreateRectTransform("SkillHUDRoot", canvas.transform);
        }

        SetupRoot(skillHudRoot);
        EnsureSlots(skillHudRoot);
        CacheSlotReferences(skillHudRoot);
        EnsureTooltip();
        SetSkillIconSet(defaultPlayerIndex);
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

    private void SetupRoot(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        root.anchorMin = new Vector2(1f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(1f, 0f);
        root.anchoredPosition = rootAnchoredPosition;
        root.sizeDelta = new Vector2((slotSize * 4f) + (slotSpacing * 3f), slotSize);
        root.localScale = Vector3.one;
    }

    private void EnsureSlots(RectTransform root)
    {
        for (int i = 0; i < 4; i++)
        {
            string key = GetDefaultSlotKey(i);
            string slotName = $"SkillSlot_{key}";
            RectTransform slotRect = root.Find(slotName) as RectTransform;
            if (slotRect == null)
            {
                slotRect = CreateSlot(root, key);
            }

            ConfigureSlotRect(slotRect, i);
            ConfigureSlotVisuals(slotRect, i);
            EnsureCooldownText(slotRect);
            EnsureHoverVisuals(slotRect);
            EnsureHoverTrigger(slotRect, i);
        }
    }

    private void CacheSlotReferences(RectTransform root)
    {
        for (int i = 0; i < 4; i++)
        {
            string key = GetDefaultSlotKey(i);
            Transform slot = root.Find($"SkillSlot_{key}");
            if (slot == null)
            {
                continue;
            }

            Transform icon = slot.Find("Icon");
            if (icon != null)
            {
                slotIconImages[i] = icon.GetComponent<Image>();
            }

            Transform overlay = slot.Find("CooldownOverlay");
            if (overlay != null)
            {
                slotCooldownOverlays[i] = overlay.GetComponent<Image>();
            }

            Transform cooldownText = slot.Find("CooldownText");
            if (cooldownText != null)
            {
                slotCooldownTexts[i] = cooldownText.GetComponent<Text>();
            }

            Transform label = slot.Find("KeyLabel");
            if (label != null)
            {
                slotKeyLabels[i] = label.GetComponent<Text>();
            }

            Transform highlight = slot.Find("HoverHighlight");
            if (highlight != null)
            {
                slotHoverHighlights[i] = highlight.GetComponent<Image>();
            }

            slotHoverTriggers[i] = slot.GetComponent<SkillHoverTrigger>();
        }
    }

    private RectTransform CreateSlot(RectTransform parent, string key)
    {
        GameObject slotObject;
        if (skillSlotPrefab != null)
        {
            slotObject = Instantiate(skillSlotPrefab, parent);
            slotObject.name = $"SkillSlot_{key}";
        }
        else
        {
            slotObject = CreateDefaultSlotObject(parent, key);
        }

        return slotObject.GetComponent<RectTransform>();
    }

    private GameObject CreateDefaultSlotObject(RectTransform parent, string key)
    {
        GameObject slot = new GameObject($"SkillSlot_{key}", typeof(RectTransform));
        slot.transform.SetParent(parent, false);

        GameObject background = CreateUiChild(slot.transform, "Background");
        EnsureImage(background, backgroundColor);

        GameObject icon = CreateUiChild(slot.transform, "Icon");
        EnsureImage(icon, iconColor);

        GameObject overlay = CreateUiChild(slot.transform, "CooldownOverlay");
        EnsureImage(overlay, cooldownOverlayColor);

        GameObject cooldownText = CreateUiChild(slot.transform, "CooldownText");
        EnsureText(cooldownText, string.Empty);

        GameObject label = CreateUiChild(slot.transform, "KeyLabel");
        Text labelText = EnsureText(label, key);
        labelText.color = keyLabelColor;
        labelText.fontSize = keyLabelFontSize;
        labelText.alignment = TextAnchor.UpperLeft;

        return slot;
    }

    private void ConfigureSlotRect(RectTransform slotRect, int index)
    {
        if (slotRect == null)
        {
            return;
        }

        float x = -((slotSize + slotSpacing) * (3 - index));
        slotRect.anchorMin = new Vector2(1f, 0f);
        slotRect.anchorMax = new Vector2(1f, 0f);
        slotRect.pivot = new Vector2(1f, 0f);
        slotRect.anchoredPosition = new Vector2(x, 0f);
        slotRect.sizeDelta = new Vector2(slotSize, slotSize);
        slotRect.localScale = Vector3.one;
    }

    private void ConfigureSlotVisuals(RectTransform slotRect, int index)
    {
        if (slotRect == null)
        {
            return;
        }

        RectTransform background = slotRect.Find("Background") as RectTransform;
        RectTransform icon = slotRect.Find("Icon") as RectTransform;
        RectTransform overlay = slotRect.Find("CooldownOverlay") as RectTransform;
        RectTransform cooldownText = slotRect.Find("CooldownText") as RectTransform;
        RectTransform keyLabel = slotRect.Find("KeyLabel") as RectTransform;

        Stretch(background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(icon, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Vector2.zero, Vector2.zero);
        MatchOverlayToIconOrSlot(overlay, icon, slotRect);
        Stretch(cooldownText, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        if (background != null)
        {
            Image backgroundImage = background.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.color = backgroundColor;
                backgroundImage.type = Image.Type.Simple;
            }
        }

        if (icon != null)
        {
            Image iconImage = icon.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.color = iconColor;
                iconImage.type = Image.Type.Simple;
                iconImage.preserveAspect = true;
            }
        }

        if (overlay != null)
        {
            Image overlayImage = overlay.GetComponent<Image>();
            if (overlayImage != null)
            {
                ConfigureCooldownOverlay(overlayImage, icon);
            }
        }

        if (cooldownText != null)
        {
            Text text = cooldownText.GetComponent<Text>();
            if (text != null)
            {
                text.text = string.Empty;
                text.font = text.font != null ? text.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = cooldownTextFontSize;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = cooldownTextColor;
                text.raycastTarget = false;
                text.enabled = false;

                Outline outline = cooldownText.GetComponent<Outline>();
                if (cooldownTextUseOutline)
                {
                    if (outline == null)
                    {
                        outline = cooldownText.gameObject.AddComponent<Outline>();
                    }

                    outline.effectColor = cooldownTextOutlineColor;
                    outline.effectDistance = new Vector2(1f, -1f);
                }
                else if (outline != null)
                {
                    Object.Destroy(outline);
                }
            }
        }

        if (keyLabel != null)
        {
            keyLabel.anchorMin = new Vector2(0f, 1f);
            keyLabel.anchorMax = new Vector2(0f, 1f);
            keyLabel.pivot = new Vector2(0f, 1f);
            keyLabel.anchoredPosition = new Vector2(8f, -6f);
            keyLabel.sizeDelta = new Vector2(slotSize * 0.45f, slotSize * 0.3f);
            keyLabel.localScale = Vector3.one;

            Text keyText = keyLabel.GetComponent<Text>();
            if (keyText != null)
            {
                keyText.fontSize = keyLabelFontSize;
                keyText.alignment = TextAnchor.UpperLeft;
                keyText.color = keyLabelColor;
                keyText.raycastTarget = false;
                if (keyText.font == null)
                {
                    keyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
            }
        }

        ApplySlotHierarchy(slotRect);
    }

    public int CurrentPlayerIndex => currentPlayerIndex > 0 ? currentPlayerIndex : defaultPlayerIndex;

    public Sprite GetConfiguredSkillIcon(int playerIndex, string key)
    {
        SkillIconSet iconSet = playerIndex == 2 ? player02Icons : player01Icons;
        if (iconSet == null)
        {
            return null;
        }

        switch ((key ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "Q":
                return iconSet.qIcon;
            case "W":
                return iconSet.wIcon;
            case "E":
                return iconSet.eIcon;
            case "R":
                return iconSet.rIcon;
            default:
                return null;
        }
    }

    public void SetSkillIconSet(int playerIndex)
    {
        if (playerIndex != 1 && playerIndex != 2)
        {
            return;
        }

        if (!initialized && skillHudRoot == null)
        {
            return;
        }

        SkillIconSet iconSet = playerIndex == 1 ? player01Icons : player02Icons;
        if (iconSet == null)
        {
            return;
        }

        currentPlayerIndex = playerIndex;
        ApplySlotVisual(0, iconSet.qIcon, iconSet.qKeyText, "Q");
        ApplySlotVisual(1, iconSet.wIcon, iconSet.wKeyText, "W");
        ApplySlotVisual(2, iconSet.eIcon, iconSet.eKeyText, "E");
        ApplySlotVisual(3, iconSet.rIcon, iconSet.rKeyText, "R");
        RefreshAllCooldownVisuals();
        RefreshHoverBindings();
    }

    public void StartSkillCooldown(string key, float duration)
    {
        int index = ResolveSlotIndex(key);
        if (index < 0)
        {
            return;
        }

        if (duration <= 0f)
        {
            cooldownDurations[index] = 0f;
            cooldownRemaining[index] = 0f;
            RefreshCooldownVisual(index);
            return;
        }

        cooldownDurations[index] = duration;
        cooldownRemaining[index] = duration;
        RefreshCooldownVisual(index);
    }

    public void SyncSkillCooldown(string key, float remaining, float duration)
    {
        int index = ResolveSlotIndex(key);
        if (index < 0)
        {
            return;
        }

        cooldownDurations[index] = Mathf.Max(0f, duration);
        cooldownRemaining[index] = Mathf.Clamp(remaining, 0f, cooldownDurations[index] > 0f ? cooldownDurations[index] : 0f);
        RefreshCooldownVisual(index);
    }

    public bool IsSkillOnCooldown(string key)
    {
        int index = ResolveSlotIndex(key);
        if (index < 0)
        {
            return false;
        }

        return cooldownRemaining[index] > 0f;
    }

    public float GetSkillCooldownRemaining(string key)
    {
        int index = ResolveSlotIndex(key);
        if (index < 0)
        {
            return 0f;
        }

        return Mathf.Max(0f, cooldownRemaining[index]);
    }

    public bool TryStartSkillCooldown(string key, float duration)
    {
        if (IsSkillOnCooldown(key))
        {
            return false;
        }

        StartSkillCooldown(key, duration);
        return true;
    }

    private void ApplySlotVisual(int index, Sprite sprite, string keyText, string fallbackKey)
    {
        if (index < 0 || index >= slotIconImages.Length)
        {
            return;
        }

        Image iconImage = slotIconImages[index];
        if (iconImage != null && sprite != null)
        {
            iconImage.sprite = sprite;
        }

        Text label = slotKeyLabels[index];
        if (label != null)
        {
            label.text = string.IsNullOrEmpty(keyText) ? fallbackKey : keyText;
        }
    }

    private void UpdateCooldownVisuals(float deltaTime)
    {
        for (int i = 0; i < cooldownRemaining.Length; i++)
        {
            if (cooldownRemaining[i] <= 0f)
            {
                continue;
            }

            cooldownRemaining[i] = Mathf.Max(0f, cooldownRemaining[i] - Mathf.Max(0f, deltaTime));
            RefreshCooldownVisual(i);
        }
    }

    private void RefreshAllCooldownVisuals()
    {
        for (int i = 0; i < cooldownRemaining.Length; i++)
        {
            RefreshCooldownVisual(i);
        }
    }

    private void RefreshCooldownVisual(int index)
    {
        if (index < 0 || index >= slotIconImages.Length)
        {
            return;
        }

        bool isCoolingDown = cooldownRemaining[index] > 0f && cooldownDurations[index] > 0f;
        bool wasCoolingDown = cooldownWasActive[index];
        cooldownWasActive[index] = isCoolingDown;

        Image iconImage = slotIconImages[index];
        if (iconImage != null)
        {
            iconImage.color = useIconDimOnCooldown && isCoolingDown && !cooldownOverlayDiagnosticMode
                ? cooldownIconDimColor
                : iconColor;
        }

        Image overlayImage = slotCooldownOverlays[index];
        if (overlayImage != null)
        {
            RectTransform slotRect = overlayImage.transform.parent as RectTransform;
            RectTransform iconRect = index >= 0 && index < slotIconImages.Length && slotIconImages[index] != null
                ? slotIconImages[index].rectTransform
                : null;
            MatchOverlayToIconOrSlot(overlayImage.rectTransform, iconRect, slotRect);
            ConfigureCooldownOverlay(overlayImage, iconRect);

            if (isCoolingDown)
            {
                overlayImage.gameObject.SetActive(true);
                overlayImage.enabled = true;
                overlayImage.fillAmount = Mathf.Clamp01(cooldownRemaining[index] / cooldownDurations[index]);
                overlayImage.color = ResolveCooldownOverlayColor();
            }
            else
            {
                overlayImage.fillAmount = 0f;
                overlayImage.gameObject.SetActive(false);
            }
        }

        Text cooldownText = slotCooldownTexts[index];
        if (cooldownText != null)
        {
            cooldownText.fontSize = cooldownTextFontSize;
            cooldownText.color = cooldownTextColor;
            cooldownText.fontStyle = FontStyle.Bold;
            cooldownText.alignment = TextAnchor.MiddleCenter;

            Outline outline = cooldownText.GetComponent<Outline>();
            if (cooldownTextUseOutline)
            {
                if (outline == null)
                {
                    outline = cooldownText.gameObject.AddComponent<Outline>();
                }

                outline.effectColor = cooldownTextOutlineColor;
                outline.effectDistance = new Vector2(1f, -1f);
            }
            else if (outline != null)
            {
                Object.Destroy(outline);
            }

            if (showCooldownText && isCoolingDown)
            {
                int seconds = Mathf.CeilToInt(cooldownRemaining[index]);
                cooldownText.text = seconds > 0 ? seconds.ToString() : string.Empty;
                cooldownText.enabled = seconds > 0;
            }
            else
            {
                cooldownText.text = string.Empty;
                cooldownText.enabled = false;
            }
        }

        if (wasCoolingDown && !isCoolingDown)
        {
            DebugCooldownEvent(GetDefaultSlotKey(index), 0f, false);
        }
    }

    private void HandleDebugCooldownKeys()
    {
        if (!enableCooldownDebugKeys || Keyboard.current == null)
        {
            return;
        }

        bool debugModifierHeld =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;

        if (!debugModifierHeld)
        {
            return;
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            StartSkillCooldown("Q", 3f);
            DebugCooldownEvent("Q", 3f, true);
        }

        if (Keyboard.current.wKey.wasPressedThisFrame)
        {
            StartSkillCooldown("W", 5f);
            DebugCooldownEvent("W", 5f, true);
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            StartSkillCooldown("E", 8f);
            DebugCooldownEvent("E", 8f, true);
        }

        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            StartSkillCooldown("R", 12f);
            DebugCooldownEvent("R", 12f, true);
        }
    }

    private static int ResolveSlotIndex(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return -1;
        }

        switch (key.Trim().ToUpperInvariant())
        {
            case "Q":
                return 0;
            case "W":
                return 1;
            case "E":
                return 2;
            case "R":
                return 3;
            default:
                return -1;
        }
    }

    private void EnsureHoverVisuals(RectTransform slotRect)
    {
        if (slotRect == null)
        {
            return;
        }

        RectTransform highlight = slotRect.Find("HoverHighlight") as RectTransform;
        if (highlight == null)
        {
            GameObject highlightObject = CreateUiChild(slotRect, "HoverHighlight");
            highlight = highlightObject.GetComponent<RectTransform>();
        }

        RectTransform icon = slotRect.Find("Icon") as RectTransform;
        MatchOverlayToIconOrSlot(highlight, icon, slotRect);
        highlight.localScale = Vector3.one * Mathf.Max(1f, hoverHighlightScale);

        Image image = highlight.GetComponent<Image>();
        if (image == null)
        {
            image = highlight.gameObject.AddComponent<Image>();
        }

        image.sprite = GetSharedCooldownCircleSprite();
        image.color = hoverHighlightColor;
        image.raycastTarget = false;
        image.enabled = false;
        highlight.gameObject.SetActive(false);
    }

    private void EnsureHoverTrigger(RectTransform slotRect, int index)
    {
        if (slotRect == null)
        {
            return;
        }

        SkillHoverTrigger trigger = slotRect.GetComponent<SkillHoverTrigger>();
        if (trigger == null)
        {
            trigger = slotRect.gameObject.AddComponent<SkillHoverTrigger>();
        }

        string key = GetDefaultSlotKey(index);
        trigger.skillKey = key;
        trigger.playerIndex = CurrentPlayerIndex;
        trigger.entered = HandleSlotHoverEnter;
        trigger.exited = HandleSlotHoverExit;
        slotHoverTriggers[index] = trigger;
    }

    private void EnsureTooltip()
    {
        if (canvasRectTransform == null)
        {
            return;
        }

        Transform existing = canvasRectTransform.Find("SkillTooltip");
        if (existing == null)
        {
            GameObject tooltipObject = CreateUiChild(canvasRectTransform, "SkillTooltip");
            tooltipRoot = tooltipObject.GetComponent<RectTransform>();

            GameObject background = CreateUiChild(tooltipRoot, "Background");
            Image backgroundImage = EnsureImage(background, tooltipBackgroundColor);
            backgroundImage.raycastTarget = false;

            GameObject textObject = CreateUiChild(tooltipRoot, "Text");
            tooltipText = EnsureTmpText(textObject, string.Empty);
            tooltipText.alignment = TextAlignmentOptions.TopLeft;
            tooltipText.color = tooltipTextColor;
            tooltipText.fontSize = tooltipFontSize;
            tooltipText.enableWordWrapping = true;
            tooltipText.overflowMode = TextOverflowModes.Overflow;
            tooltipText.lineSpacing = 4f;
            tooltipText.raycastTarget = false;

            Stretch(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Stretch(textObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(tooltipPadding.x, tooltipPadding.y), new Vector2(-tooltipPadding.x, -tooltipPadding.y));
        }
        else
        {
            tooltipRoot = existing as RectTransform;
            Transform textTransform = existing.Find("Text");
            if (textTransform != null)
            {
                tooltipText = textTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        if (tooltipRoot != null)
        {
            tooltipRoot.anchorMin = new Vector2(0.5f, 0.5f);
            tooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
            tooltipRoot.pivot = new Vector2(0.5f, 0f);
            tooltipRoot.sizeDelta = new Vector2(tooltipWidth, 64f);
            tooltipRoot.gameObject.SetActive(false);
            tooltipRoot.SetAsLastSibling();
        }
    }

    private void RefreshHoverBindings()
    {
        for (int i = 0; i < slotHoverTriggers.Length; i++)
        {
            SkillHoverTrigger trigger = slotHoverTriggers[i];
            if (trigger == null)
            {
                continue;
            }

            trigger.skillKey = GetDefaultSlotKey(i);
            trigger.playerIndex = CurrentPlayerIndex;
        }
    }

    private void HandleSlotHoverEnter(SkillHoverTrigger trigger)
    {
        if (trigger == null)
        {
            return;
        }

        int index = ResolveSlotIndex(trigger.skillKey);
        if (index >= 0 && index < slotHoverHighlights.Length && slotHoverHighlights[index] != null)
        {
            slotHoverHighlights[index].color = hoverHighlightColor;
            slotHoverHighlights[index].enabled = true;
            slotHoverHighlights[index].gameObject.SetActive(true);
        }

        SkillUIDefinitionEntry entry = SkillUIDefinitionDatabase.Get(CurrentPlayerIndex, trigger.skillKey);
        string tooltip = SkillUIDefinitionDatabase.BuildTooltipText(entry);
        if (tooltipRoot == null || tooltipText == null || string.IsNullOrWhiteSpace(tooltip))
        {
            return;
        }

        tooltipText.text = tooltip;
        tooltipText.color = tooltipTextColor;
        tooltipText.fontSize = tooltipFontSize;
        tooltipText.enableWordWrapping = true;
        tooltipText.overflowMode = TextOverflowModes.Overflow;
        tooltipText.ForceMeshUpdate();

        float contentWidth = Mathf.Max(40f, tooltipWidth - (tooltipPadding.x * 2f));
        Vector2 preferred = tooltipText.GetPreferredValues(tooltip, contentWidth, 0f);
        float tooltipHeight = Mathf.Ceil(preferred.y + (tooltipPadding.y * 2f));
        tooltipRoot.sizeDelta = new Vector2(tooltipWidth, tooltipHeight);

        RectTransform textRect = tooltipText.rectTransform;
        if (textRect != null)
        {
            Stretch(textRect, Vector2.zero, Vector2.one, new Vector2(tooltipPadding.x, tooltipPadding.y), new Vector2(-tooltipPadding.x, -tooltipPadding.y));
        }

        RectTransform slotRect = trigger.transform as RectTransform;
        if (slotRect != null && canvasRectTransform != null)
        {
            Vector3 worldTopCenter = slotRect.TransformPoint(new Vector3(slotRect.rect.center.x, slotRect.rect.yMax, 0f));
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldTopCenter);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, screenPoint, null, out Vector2 localPoint))
            {
                tooltipRoot.anchoredPosition = localPoint + tooltipOffset;
            }
        }

        tooltipRoot.gameObject.SetActive(true);
        tooltipRoot.SetAsLastSibling();
    }

    private void HandleSlotHoverExit(SkillHoverTrigger trigger)
    {
        if (trigger == null)
        {
            return;
        }

        int index = ResolveSlotIndex(trigger.skillKey);
        if (index >= 0 && index < slotHoverHighlights.Length && slotHoverHighlights[index] != null)
        {
            slotHoverHighlights[index].enabled = false;
            slotHoverHighlights[index].gameObject.SetActive(false);
        }

        if (tooltipRoot != null)
        {
            tooltipRoot.gameObject.SetActive(false);
        }
    }

    private void EnsureCooldownText(RectTransform slotRect)
    {
        if (slotRect == null)
        {
            return;
        }

        Transform existing = slotRect.Find("CooldownText");
        if (existing != null)
        {
            return;
        }

        GameObject cooldownText = CreateUiChild(slotRect, "CooldownText");
        Text text = EnsureText(cooldownText, string.Empty);
        text.fontSize = cooldownTextFontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = cooldownTextColor;
        text.raycastTarget = false;
        text.enabled = false;

        RectTransform rect = cooldownText.GetComponent<RectTransform>();
        Stretch(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        Transform keyLabel = slotRect.Find("KeyLabel");
        if (keyLabel != null)
        {
            cooldownText.transform.SetSiblingIndex(Mathf.Max(0, keyLabel.GetSiblingIndex()));
        }

        if (cooldownTextUseOutline)
        {
            Outline outline = cooldownText.GetComponent<Outline>();
            if (outline == null)
            {
                outline = cooldownText.AddComponent<Outline>();
            }

            outline.effectColor = cooldownTextOutlineColor;
            outline.effectDistance = new Vector2(1f, -1f);
        }
    }

    private void ConfigureCooldownOverlay(Image overlayImage)
    {
        if (overlayImage == null)
        {
            return;
        }

        ConfigureCooldownOverlay(overlayImage, null);
    }

    private void ConfigureCooldownOverlay(Image overlayImage, RectTransform iconRect)
    {
        if (overlayImage == null)
        {
            return;
        }

        RectTransform slotRect = overlayImage.transform.parent as RectTransform;
        MatchOverlayToIconOrSlot(overlayImage.rectTransform, iconRect, slotRect);
        overlayImage.sprite = GetSharedCooldownCircleSprite();
        overlayImage.type = Image.Type.Filled;
        overlayImage.fillMethod = Image.FillMethod.Radial360;
        overlayImage.fillOrigin = cooldownFillOrigin;
        overlayImage.fillClockwise = cooldownFillClockwise;
        overlayImage.fillAmount = 0f;
        overlayImage.color = ResolveCooldownOverlayColor();
        overlayImage.raycastTarget = false;
        overlayImage.preserveAspect = false;
        overlayImage.enabled = true;
    }

    private void ApplySlotHierarchy(RectTransform slotRect)
    {
        if (slotRect == null)
        {
            return;
        }

        Transform background = slotRect.Find("Background");
        Transform hoverHighlight = slotRect.Find("HoverHighlight");
        Transform icon = slotRect.Find("Icon");
        Transform overlay = slotRect.Find("CooldownOverlay");
        Transform cooldownText = slotRect.Find("CooldownText");
        Transform keyLabel = slotRect.Find("KeyLabel");

        int siblingIndex = 0;
        if (background != null)
        {
            background.SetSiblingIndex(siblingIndex++);
        }

        if (hoverHighlight != null)
        {
            hoverHighlight.SetSiblingIndex(siblingIndex++);
        }

        if (icon != null)
        {
            icon.SetSiblingIndex(siblingIndex++);
        }

        if (overlay != null)
        {
            overlay.SetSiblingIndex(siblingIndex++);
        }

        if (cooldownText != null)
        {
            cooldownText.SetSiblingIndex(siblingIndex++);
        }

        if (keyLabel != null)
        {
            keyLabel.SetSiblingIndex(siblingIndex);
        }
    }

    private static Sprite GetSharedCooldownCircleSprite()
    {
        if (sharedCooldownCircleSprite != null)
        {
            return sharedCooldownCircleSprite;
        }

        sharedCooldownCircleSprite = CreateCircleSprite(128);
        sharedCooldownCircleSprite.name = "PlayerSkillHUD_CooldownCircleSprite";
        return sharedCooldownCircleSprite;
    }

    private void DebugCooldownEvent(string key, float duration, bool started)
    {
        if (!enableCooldownDebugKeys)
        {
            return;
        }

        int index = ResolveSlotIndex(key);
        if (index < 0 || index >= slotCooldownOverlays.Length)
        {
            return;
        }

        float fillAmount = 0f;
        Image overlay = slotCooldownOverlays[index];
        if (overlay != null)
        {
            fillAmount = overlay.fillAmount;
        }

        if (started)
        {
            string spriteName = overlay != null && overlay.sprite != null ? overlay.sprite.name : "null";
            Debug.Log(
                $"[SkillHUD CD] key={key} duration={duration:F1} overlayExists={(overlay != null)} sprite={spriteName} type={(overlay != null ? overlay.type.ToString() : "null")} fillMethod={(overlay != null ? overlay.fillMethod.ToString() : "null")} fillOrigin={(overlay != null ? overlay.fillOrigin.ToString() : "null")} fillClockwise={(overlay != null && overlay.fillClockwise)} fillAmount={fillAmount:F2} color={(overlay != null ? overlay.color.ToString() : "null")} active={(overlay != null && overlay.gameObject.activeInHierarchy)} enabled={(overlay != null && overlay.enabled)}",
                this);
        }
        else
        {
            Debug.Log($"[SkillHUD CD] {key} finished fill={fillAmount:F2}", this);
        }
    }

    private Color ResolveCooldownOverlayColor()
    {
        return cooldownOverlayDiagnosticMode ? new Color(1f, 0f, 0f, 0.6f) : cooldownOverlayColor;
    }

    private static void MatchOverlayToIconOrSlot(RectTransform overlayRect, RectTransform iconRect, RectTransform slotRect)
    {
        if (overlayRect == null)
        {
            return;
        }

        RectTransform source = iconRect != null ? iconRect : slotRect;
        if (source == null)
        {
            Stretch(overlayRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.localScale = Vector3.one;
            return;
        }

        overlayRect.anchorMin = source.anchorMin;
        overlayRect.anchorMax = source.anchorMax;
        overlayRect.pivot = source.pivot;
        overlayRect.anchoredPosition = source.anchoredPosition;
        overlayRect.sizeDelta = source.sizeDelta;
        overlayRect.offsetMin = source.offsetMin;
        overlayRect.offsetMax = source.offsetMax;
        overlayRect.localScale = Vector3.one;
    }

    private static Sprite CreateCircleSprite(int size)
    {
        int clampedSize = Mathf.Max(16, size);
        Texture2D texture = new Texture2D(clampedSize, clampedSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float radius = (clampedSize - 1) * 0.5f;
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < clampedSize; y++)
        {
            for (int x = 0; x < clampedSize; x++)
            {
                Vector2 point = new Vector2(x, y);
                float distance = Vector2.Distance(point, center);
                texture.SetPixel(x, y, distance <= radius ? Color.white : new Color(1f, 1f, 1f, 0f));
            }
        }

        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, clampedSize, clampedSize),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    private static string GetDefaultSlotKey(int index)
    {
        return index switch
        {
            0 => "Q",
            1 => "W",
            2 => "E",
            3 => "R",
            _ => "Q"
        };
    }

    private static RectTransform CreateRectTransform(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static GameObject CreateUiChild(Transform parent, string name)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Image EnsureImage(GameObject gameObject, Color color)
    {
        Image image = gameObject.GetComponent<Image>();
        if (image == null)
        {
            image = gameObject.AddComponent<Image>();
        }

        image.color = color;
        return image;
    }

    private static Text EnsureText(GameObject gameObject, string value)
    {
        Text text = gameObject.GetComponent<Text>();
        if (text == null)
        {
            text = gameObject.AddComponent<Text>();
        }

        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static TextMeshProUGUI EnsureTmpText(GameObject gameObject, string value)
    {
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        if (text == null)
        {
            text = gameObject.AddComponent<TextMeshProUGUI>();
        }

        text.text = value;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }
}
