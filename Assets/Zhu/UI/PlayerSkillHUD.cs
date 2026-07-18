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

[System.Serializable]
public class SkillSlotView
{
    public RectTransform root;
    public Image iconImage;
    public Image cooldownMaskImage;
    public Text cooldownText;
    public Text keyText;
    public GameObject disabledOverlay;
    public Image hoverHighlight;
    public Image selectionHighlight;
    public SkillHoverTrigger hoverTrigger;
}

[DisallowMultipleComponent]
public class PlayerSkillHUD : MonoBehaviour
{
    private const string LogMissingCanvas = "[SkillUI] Missing target canvas for PlayerSkillHUD.";
    private const string LogMissingRoot = "[SkillUI] Missing external SkillHUDRoot reference.";
    private const string LogMissingSlot = "[SkillUI] Missing external skill slot reference.";
    private const string LogMissingTooltip = "[SkillUI] Missing external tooltip reference. Hover tooltip will be disabled.";

    [Header("Root")]
    [SerializeField] private RectTransform skillHudRoot;
    [SerializeField] private Canvas targetCanvas;

    [Header("External Slots")]
    [SerializeField] private SkillSlotView qSlot = new SkillSlotView();
    [SerializeField] private SkillSlotView wSlot = new SkillSlotView();
    [SerializeField] private SkillSlotView eSlot = new SkillSlotView();
    [SerializeField] private SkillSlotView rSlot = new SkillSlotView();

    [Header("Legacy Prefab (Unused)")]
    [SerializeField] private GameObject skillSlotPrefab;

    [Header("External Tooltip")]
    [SerializeField] private RectTransform externalTooltipRoot;
    [SerializeField] private TextMeshProUGUI externalTooltipText;

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
    [SerializeField] private bool cooldownOverlayDiagnosticMode = false;
    [SerializeField] private bool enableCooldownDebugKeys = false;

    [Header("Hover / Selection")]
    [SerializeField] private Color hoverHighlightColor = new Color(0.55f, 0.88f, 1f, 0.75f);
    [SerializeField] private float hoverHighlightScale = 1.14f;
    [SerializeField] private Color selectionHighlightColor = new Color(1f, 0.88f, 0.28f, 0.95f);
    [SerializeField] private float selectionHighlightScale = 1.2f;
    [SerializeField] private bool useGeneratedHighlightRingSprite = true;
    [SerializeField] private Color tooltipBackgroundColor = new Color(0.08f, 0.1f, 0.14f, 0.96f);
    [SerializeField] private Color tooltipTextColor = Color.white;
    [SerializeField] private int tooltipFontSize = 18;
    [SerializeField] private float tooltipWidth = 340f;
    [SerializeField] private Vector2 tooltipPadding = new Vector2(12f, 10f);
    [SerializeField] private Vector2 tooltipOffset = new Vector2(0f, 26f);
    [SerializeField] private float tooltipScreenMargin = 16f;
    [SerializeField] private Vector2 tooltipPreferredRightOffset = new Vector2(18f, 18f);
    [SerializeField] private Vector2 tooltipPreferredLeftOffset = new Vector2(-18f, 18f);
    [SerializeField] private bool debugTooltipPositioning = true;

    private readonly Image[] slotIconImages = new Image[4];
    private readonly Image[] slotCooldownOverlays = new Image[4];
    private readonly Text[] slotCooldownTexts = new Text[4];
    private readonly Text[] slotKeyLabels = new Text[4];
    private readonly Image[] slotHoverHighlights = new Image[4];
    private readonly Image[] slotSelectionHighlights = new Image[4];
    private readonly SkillHoverTrigger[] slotHoverTriggers = new SkillHoverTrigger[4];
    private readonly float[] cooldownDurations = new float[4];
    private readonly float[] cooldownRemaining = new float[4];
    private readonly bool[] cooldownWasActive = new bool[4];
    private static Sprite sharedCooldownCircleSprite;
    private static Sprite sharedHighlightRingSprite;
    private bool initialized;
    private int currentPlayerIndex;
    private int selectedSlotIndex = -1;
    private RectTransform canvasRectTransform;
    private RectTransform tooltipRoot;
    private TextMeshProUGUI tooltipText;
    private bool warnedMissingCanvas;
    private bool warnedMissingRoot;
    private bool warnedMissingSlot;
    private bool warnedMissingTooltip;

    private void Awake()
    {
        if (!IsPrimaryComponent())
        {
            enabled = false;
            return;
        }

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
        AutoBindExternalReferences(canvas.transform);
        if (!HasValidExternalReferences())
        {
            initialized = false;
            return;
        }

        ApplyConfiguredLayout();
        EnsureSlots(skillHudRoot);
        CacheSlotReferences(skillHudRoot);
        EnsureTooltip();
        SetSkillIconSet(defaultPlayerIndex);
        initialized = true;
    }

    private bool IsPrimaryComponent()
    {
        PlayerSkillHUD[] components = GetComponents<PlayerSkillHUD>();
        return components.Length == 0 || components[components.Length - 1] == this;
    }

    private void ApplyConfiguredLayout()
    {
        slotSize = Mathf.Max(1f, slotSize);
        slotSpacing = Mathf.Max(0f, slotSpacing);

        SetupRoot(skillHudRoot);

        SkillSlotView[] slots = { qSlot, wSlot, eSlot, rSlot };
        for (int i = 0; i < slots.Length; i++)
        {
            RectTransform slotRect = slots[i] != null ? slots[i].root : null;
            if (slotRect == null && skillHudRoot != null)
            {
                slotRect = skillHudRoot.Find($"SkillSlot_{GetDefaultSlotKey(i)}") as RectTransform;
            }

            ConfigureSlotRect(slotRect, i);
            ConfigureSlotVisuals(slotRect, i);
        }
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

        if (!warnedMissingCanvas)
        {
            warnedMissingCanvas = true;
            Debug.LogWarning(LogMissingCanvas, this);
        }

        return null;
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

    private void AutoBindExternalReferences(Transform canvasTransform)
    {
        if (canvasTransform == null)
        {
            return;
        }

        if (skillHudRoot == null)
        {
            Transform existing = canvasTransform.Find("SkillHUDRoot");
            if (existing == null)
            {
                existing = FindChildRecursive(canvasTransform, "SkillHUDRoot");
            }

            skillHudRoot = existing as RectTransform;
        }

        AutoBindSlotView(qSlot, "Q");
        AutoBindSlotView(wSlot, "W");
        AutoBindSlotView(eSlot, "E");
        AutoBindSlotView(rSlot, "R");

        if (externalTooltipRoot == null)
        {
            Transform tooltip = canvasTransform.Find("SkillTooltip");
            if (tooltip == null)
            {
                tooltip = FindChildRecursive(canvasTransform, "SkillTooltip");
            }

            externalTooltipRoot = tooltip as RectTransform;
        }

        if (externalTooltipText == null && externalTooltipRoot != null)
        {
            Transform textTransform = externalTooltipRoot.Find("Text");
            if (textTransform == null)
            {
                textTransform = FindChildRecursive(externalTooltipRoot, "Text");
            }

            if (textTransform != null)
            {
                externalTooltipText = textTransform.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    private void AutoBindSlotView(SkillSlotView slotView, string key)
    {
        if (slotView == null || skillHudRoot == null)
        {
            return;
        }

        if (slotView.root == null)
        {
            slotView.root = skillHudRoot.Find($"SkillSlot_{key}") as RectTransform;
        }

        if (slotView.root == null)
        {
            return;
        }

        if (slotView.iconImage == null)
        {
            slotView.iconImage = slotView.root.Find("Icon")?.GetComponent<Image>();
        }

        if (slotView.cooldownMaskImage == null)
        {
            slotView.cooldownMaskImage = slotView.root.Find("CooldownOverlay")?.GetComponent<Image>();
        }

        if (slotView.cooldownText == null)
        {
            slotView.cooldownText = slotView.root.Find("CooldownText")?.GetComponent<Text>();
        }

        if (slotView.keyText == null)
        {
            slotView.keyText = slotView.root.Find("KeyLabel")?.GetComponent<Text>();
        }

        if (slotView.disabledOverlay == null)
        {
            Transform disabledOverlay = slotView.root.Find("DisabledOverlay");
            slotView.disabledOverlay = disabledOverlay != null ? disabledOverlay.gameObject : null;
        }

        if (slotView.hoverHighlight == null)
        {
            slotView.hoverHighlight = slotView.root.Find("HoverHighlight")?.GetComponent<Image>();
        }

        if (slotView.selectionHighlight == null)
        {
            Transform selectionHighlight = slotView.root.Find("SelectionHighlight");
            if (selectionHighlight == null)
            {
                selectionHighlight = slotView.root.Find("SelectedHighlight");
            }

            slotView.selectionHighlight = selectionHighlight?.GetComponent<Image>();
        }

        if (slotView.hoverTrigger == null)
        {
            slotView.hoverTrigger = slotView.root.GetComponent<SkillHoverTrigger>();
        }
    }

    private bool HasValidExternalReferences()
    {
        if (skillHudRoot == null)
        {
            if (!warnedMissingRoot)
            {
                warnedMissingRoot = true;
                Debug.LogWarning(LogMissingRoot, this);
            }

            return false;
        }

        bool hasAllSlots =
            HasValidSlotView(qSlot) &&
            HasValidSlotView(wSlot) &&
            HasValidSlotView(eSlot) &&
            HasValidSlotView(rSlot);

        if (!hasAllSlots && !warnedMissingSlot)
        {
            warnedMissingSlot = true;
            Debug.LogWarning(LogMissingSlot, this);
        }

        return hasAllSlots;
    }

    private static bool HasValidSlotView(SkillSlotView slotView)
    {
        return slotView != null
            && slotView.root != null
            && slotView.iconImage != null
            && slotView.cooldownMaskImage != null
            && slotView.cooldownText != null
            && slotView.keyText != null;
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
                continue;
            }

            EnsureHighlightChild(slotRect, "HoverHighlight");
            EnsureHighlightChild(slotRect, "SelectionHighlight");
            EnsureHighlightVisuals(slotRect);
            EnsureHoverTrigger(slotRect, i);
        }
    }

    private void CacheSlotReferences(RectTransform root)
    {
        SkillSlotView[] configuredViews = { qSlot, wSlot, eSlot, rSlot };
        for (int i = 0; i < 4; i++)
        {
            SkillSlotView configuredView = configuredViews[i];
            if (configuredView != null)
            {
                if (configuredView.iconImage != null)
                {
                    slotIconImages[i] = configuredView.iconImage;
                }

                if (configuredView.cooldownMaskImage != null)
                {
                    slotCooldownOverlays[i] = configuredView.cooldownMaskImage;
                }

                if (configuredView.cooldownText != null)
                {
                    slotCooldownTexts[i] = configuredView.cooldownText;
                }

                if (configuredView.keyText != null)
                {
                    slotKeyLabels[i] = configuredView.keyText;
                }

                if (configuredView.hoverHighlight != null)
                {
                    slotHoverHighlights[i] = configuredView.hoverHighlight;
                }

                if (configuredView.selectionHighlight != null)
                {
                    slotSelectionHighlights[i] = configuredView.selectionHighlight;
                }

                if (configuredView.hoverTrigger != null)
                {
                    slotHoverTriggers[i] = configuredView.hoverTrigger;
                }
            }

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

            Transform selectionHighlight = slot.Find("SelectionHighlight");
            if (selectionHighlight == null)
            {
                selectionHighlight = slot.Find("SelectedHighlight");
            }

            if (selectionHighlight != null)
            {
                slotSelectionHighlights[i] = selectionHighlight.GetComponent<Image>();
            }

            slotHoverTriggers[i] = slot.GetComponent<SkillHoverTrigger>();
        }
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (parent.name == name)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
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
        RectTransform hoverHighlight = slotRect.Find("HoverHighlight") as RectTransform;
        RectTransform selectionHighlight = slotRect.Find("SelectionHighlight") as RectTransform;
        RectTransform cooldownText = slotRect.Find("CooldownText") as RectTransform;
        RectTransform keyLabel = slotRect.Find("KeyLabel") as RectTransform;

        Stretch(background, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Stretch(icon, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Vector2.zero, Vector2.zero);
        MatchOverlayToIconOrSlot(overlay, icon, slotRect);
        MatchOverlayToIconOrSlot(hoverHighlight, icon, slotRect);
        MatchOverlayToIconOrSlot(selectionHighlight, icon, slotRect);
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

        if (hoverHighlight != null)
        {
            ConfigureHighlightImage(hoverHighlight.GetComponent<Image>(), hoverHighlightColor, hoverHighlightScale, false);
        }

        if (selectionHighlight != null)
        {
            ConfigureHighlightImage(selectionHighlight.GetComponent<Image>(), selectionHighlightColor, selectionHighlightScale, false);
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
        RefreshSelectionHighlights();
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

    private void EnsureHighlightVisuals(RectTransform slotRect)
    {
        if (slotRect == null)
        {
            return;
        }

        ConfigureNamedHighlight(slotRect, "HoverHighlight", hoverHighlightColor, hoverHighlightScale, false);
        ConfigureNamedHighlight(slotRect, "SelectionHighlight", selectionHighlightColor, selectionHighlightScale, false);
    }

    private void ConfigureNamedHighlight(RectTransform slotRect, string childName, Color color, float scale, bool active)
    {
        RectTransform highlight = slotRect != null ? slotRect.Find(childName) as RectTransform : null;
        if (highlight == null)
        {
            return;
        }

        ConfigureHighlightImage(highlight.GetComponent<Image>(), color, scale, active);
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
            return;
        }

        string key = GetDefaultSlotKey(index);
        trigger.skillKey = key;
        trigger.playerIndex = CurrentPlayerIndex;
        trigger.entered = HandleSlotHoverEnter;
        trigger.exited = HandleSlotHoverExit;
        trigger.clicked = HandleSlotClick;
        slotHoverTriggers[index] = trigger;
    }

    private void EnsureTooltip()
    {
        if (canvasRectTransform == null)
        {
            return;
        }

        tooltipRoot = externalTooltipRoot;
        tooltipText = externalTooltipText;

        if ((tooltipRoot == null || tooltipText == null) && !warnedMissingTooltip)
        {
            warnedMissingTooltip = true;
            Debug.LogWarning(LogMissingTooltip, this);
        }

        if (tooltipRoot != null)
        {
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
            trigger.clicked = HandleSlotClick;
        }
    }

    private void HandleSlotHoverEnter(SkillHoverTrigger trigger)
    {
        if (trigger == null)
        {
            return;
        }

        Debug.Log(
            "[TooltipEntryTrace] " +
            "entry=PlayerSkillHUD.HandleSlotHoverEnter" +
            " skillKey=" + trigger.skillKey +
            " callerObject=" + trigger.gameObject.name +
            " callerInstanceId=" + trigger.gameObject.GetInstanceID() +
            " tooltipObject=" + (tooltipRoot != null ? tooltipRoot.name : "null") +
            " tooltipInstanceId=" + (tooltipRoot != null ? tooltipRoot.gameObject.GetInstanceID().ToString() : "null") +
            " frame=" + Time.frameCount,
            this);

        int index = ResolveSlotIndex(trigger.skillKey);
        if (index >= 0 && index < slotHoverHighlights.Length && slotHoverHighlights[index] != null)
        {
            ConfigureHighlightImage(slotHoverHighlights[index], hoverHighlightColor, hoverHighlightScale, true);
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
            PositionTooltipForSlot(slotRect, tooltipRoot.sizeDelta);
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

    private void HandleSlotClick(SkillHoverTrigger trigger)
    {
        if (trigger == null)
        {
            return;
        }

        selectedSlotIndex = ResolveSlotIndex(trigger.skillKey);
        RefreshSelectionHighlights();
    }

    private void PositionTooltipForSlot(RectTransform slotRect, Vector2 tooltipSize)
    {
        if (slotRect == null || canvasRectTransform == null || tooltipRoot == null)
        {
            return;
        }

        Camera uiCamera = ResolveUiCamera();
        Vector3[] worldCorners = new Vector3[4];
        slotRect.GetWorldCorners(worldCorners);

        Vector2 slotBottomLeftScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, worldCorners[0]);
        Vector2 slotTopRightScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, worldCorners[2]);
        Vector2 slotTopLeftScreen = RectTransformUtility.WorldToScreenPoint(uiCamera, worldCorners[1]);

        Rect canvasScreenRect = GetCanvasScreenRect(uiCamera);
        float margin = Mathf.Max(0f, tooltipScreenMargin);

        bool canPlaceRight = slotTopRightScreen.x + tooltipPreferredRightOffset.x + tooltipSize.x <= canvasScreenRect.xMax - margin;
        bool placeRight = canPlaceRight;
        Vector2 chosenOffset = placeRight ? tooltipPreferredRightOffset : tooltipPreferredLeftOffset;
        Vector2 pivot = placeRight ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
        Vector2 anchorScreenPoint = placeRight ? slotTopRightScreen : slotTopLeftScreen;

        tooltipRoot.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRoot.pivot = pivot;

        Vector2 screenTarget = anchorScreenPoint + chosenOffset;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, screenTarget, uiCamera, out Vector2 localPoint))
        {
            tooltipRoot.anchoredPosition = ClampTooltipAnchoredPosition(localPoint, tooltipSize, pivot, margin);
        }

        if (debugTooltipPositioning)
        {
            Debug.Log(
                "[TooltipRuntimeTrace] " +
                "event=TooltipResolved" +
                " tooltipObject=" + tooltipRoot.name +
                " tooltipInstanceId=" + tooltipRoot.gameObject.GetInstanceID() +
                " targetObject=" + slotRect.name +
                " targetInstanceId=" + slotRect.gameObject.GetInstanceID() +
                " placement=" + (placeRight ? "RightOfSlot" : "LeftOfSlot") +
                " slotTopRightScreen=" + slotTopRightScreen +
                " slotTopLeftScreen=" + slotTopLeftScreen +
                " canvasScreenRect=" + canvasScreenRect +
                " tooltipSize=" + tooltipSize +
                " pivot=" + pivot +
                " anchoredPosition=" + tooltipRoot.anchoredPosition +
                " frame=" + Time.frameCount,
                this);
        }
    }

    private Vector2 ClampTooltipAnchoredPosition(Vector2 desiredLocalPoint, Vector2 tooltipSize, Vector2 pivot, float margin)
    {
        Rect canvasRect = canvasRectTransform.rect;
        float left = desiredLocalPoint.x - (tooltipSize.x * pivot.x);
        float right = left + tooltipSize.x;
        float bottom = desiredLocalPoint.y - (tooltipSize.y * pivot.y);
        float top = bottom + tooltipSize.y;

        float minX = canvasRect.xMin + margin;
        float maxX = canvasRect.xMax - margin;
        float minY = canvasRect.yMin + margin;
        float maxY = canvasRect.yMax - margin;

        if (left < minX)
        {
            desiredLocalPoint.x += minX - left;
            right += minX - left;
            left = minX;
        }

        if (right > maxX)
        {
            desiredLocalPoint.x -= right - maxX;
        }

        if (bottom < minY)
        {
            desiredLocalPoint.y += minY - bottom;
            top += minY - bottom;
            bottom = minY;
        }

        if (top > maxY)
        {
            desiredLocalPoint.y -= top - maxY;
        }

        return desiredLocalPoint;
    }

    private Camera ResolveUiCamera()
    {
        if (targetCanvas == null)
        {
            return null;
        }

        if (targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return targetCanvas.worldCamera != null ? targetCanvas.worldCamera : Camera.main;
    }

    private Rect GetCanvasScreenRect(Camera uiCamera)
    {
        Vector3[] corners = new Vector3[4];
        canvasRectTransform.GetWorldCorners(corners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
        return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
    }

    private void RefreshSelectionHighlights()
    {
        for (int i = 0; i < slotSelectionHighlights.Length; i++)
        {
            Image highlight = slotSelectionHighlights[i];
            if (highlight == null)
            {
                continue;
            }

            bool active = i == selectedSlotIndex;
            ConfigureHighlightImage(highlight, selectionHighlightColor, selectionHighlightScale, active);
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

        overlayImage.type = Image.Type.Filled;
        overlayImage.fillMethod = Image.FillMethod.Radial360;
        overlayImage.fillOrigin = cooldownFillOrigin;
        overlayImage.fillClockwise = cooldownFillClockwise;
        if (overlayImage.sprite == null)
        {
            overlayImage.sprite = GetSharedCooldownCircleSprite();
        }

        overlayImage.fillAmount = 0f;
        overlayImage.color = ResolveCooldownOverlayColor();
        overlayImage.raycastTarget = false;
        overlayImage.preserveAspect = true;
        overlayImage.enabled = true;
    }

    private void ConfigureHighlightImage(Image image, Color color, float scale, bool active)
    {
        if (image == null)
        {
            return;
        }

        if (useGeneratedHighlightRingSprite || image.sprite == null)
        {
            image.sprite = GetSharedHighlightRingSprite();
        }

        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = color;
        image.rectTransform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
        image.enabled = active;
        image.gameObject.SetActive(active);
    }

    private void ApplySlotHierarchy(RectTransform slotRect)
    {
        if (slotRect == null)
        {
            return;
        }

        Transform background = slotRect.Find("Background");
        Transform icon = slotRect.Find("Icon");
        Transform overlay = slotRect.Find("CooldownOverlay");
        Transform hoverHighlight = slotRect.Find("HoverHighlight");
        Transform selectionHighlight = slotRect.Find("SelectionHighlight");
        Transform cooldownText = slotRect.Find("CooldownText");
        Transform keyLabel = slotRect.Find("KeyLabel");

        int siblingIndex = 0;
        if (background != null)
        {
            background.SetSiblingIndex(siblingIndex++);
        }

        if (icon != null)
        {
            icon.SetSiblingIndex(siblingIndex++);
        }

        if (overlay != null)
        {
            overlay.SetSiblingIndex(siblingIndex++);
        }

        if (hoverHighlight != null)
        {
            hoverHighlight.SetSiblingIndex(siblingIndex++);
        }

        if (selectionHighlight != null)
        {
            selectionHighlight.SetSiblingIndex(siblingIndex++);
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

    private static Sprite GetSharedHighlightRingSprite()
    {
        if (sharedHighlightRingSprite != null)
        {
            return sharedHighlightRingSprite;
        }

        sharedHighlightRingSprite = CreateRingSprite(128);
        sharedHighlightRingSprite.name = "PlayerSkillHUD_HighlightRingSprite";
        return sharedHighlightRingSprite;
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
        return cooldownOverlayDiagnosticMode && enableCooldownDebugKeys
            ? new Color(1f, 0f, 0f, 0.6f)
            : cooldownOverlayColor;
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

    private static Sprite CreateRingSprite(int size)
    {
        int clampedSize = Mathf.Max(16, size);
        Texture2D texture = new Texture2D(clampedSize, clampedSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float radius = (clampedSize - 1) * 0.5f;
        float outerRadius = radius * 0.94f;
        float ringRadius = radius * 0.77f;
        float innerRadius = radius * 0.58f;
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < clampedSize; y++)
        {
            for (int x = 0; x < clampedSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float ringAlpha = Mathf.InverseLerp(innerRadius, ringRadius, distance) * (1f - Mathf.InverseLerp(ringRadius, outerRadius, distance));
                float glowAlpha = distance > ringRadius ? 1f - Mathf.InverseLerp(ringRadius, radius, distance) : 0f;
                float alpha = Mathf.Clamp01((ringAlpha * 0.95f) + (glowAlpha * 0.35f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, clampedSize, clampedSize),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    private static RectTransform EnsureHighlightChild(RectTransform slotRect, string childName)
    {
        if (slotRect == null)
        {
            return null;
        }

        RectTransform highlight = slotRect.Find(childName) as RectTransform;
        if (highlight != null)
        {
            return highlight;
        }

        highlight = CreateRectTransform(childName, slotRect);
        Stretch(highlight, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Vector2.zero, Vector2.zero);
        Image image = highlight.gameObject.AddComponent<Image>();
        image.sprite = GetSharedHighlightRingSprite();
        image.raycastTarget = false;
        highlight.gameObject.SetActive(false);
        return highlight;
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

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        Canvas canvas = targetCanvas != null
            ? targetCanvas
            : GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return;
        }

        AutoBindExternalReferences(canvas.transform);
    }
}
