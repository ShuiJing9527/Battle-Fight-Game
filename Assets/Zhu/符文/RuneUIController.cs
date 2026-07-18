using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class RuneUIController : MonoBehaviour
{
    private enum DescriptionSource
    {
        None,
        Skill,
        Rune,
        EmptySlot
    }

    private enum TooltipPlacementCandidate
    {
        AboveRight,
        AboveLeft,
        BelowRight,
        BelowLeft
    }

    [System.Serializable]
    public class RuneInventoryLayoutSettings
    {
        public Vector2 panelAnchoredPosition = Vector2.zero;
        public Vector2 panelSize = new Vector2(0f, 0f);
        public Vector2 viewportAnchoredPosition = Vector2.zero;
        public Vector2 viewportSize = new Vector2(0f, 0f);
        public Vector2 viewportOffsetMin = Vector2.zero;
        public Vector2 viewportOffsetMax = Vector2.zero;
        public Vector2 contentAnchoredPosition = Vector2.zero;
        public Vector2 contentSize = new Vector2(0f, 0f);
        public Vector2 contentAnchorMin = new Vector2(0f, 1f);
        public Vector2 contentAnchorMax = new Vector2(1f, 1f);
        public Vector2 contentPivot = new Vector2(0.5f, 1f);
        public Vector2 itemSpacing = new Vector2(0f, 8f);
        public Vector2 itemSize = new Vector2(0f, 40f);
        public float contentTopPadding = 0f;
        public float contentBottomPadding = 0f;
        public int columnCount = 1;
    }

    [System.Serializable]
    public class RuneDescriptionLayoutSettings
    {
        public Vector2 panelAnchoredPosition = new Vector2(20f, 60f);
        public Vector2 panelSize = new Vector2(0f, 150f);
        public Vector2 panelAnchorMin = new Vector2(0f, 0f);
        public Vector2 panelAnchorMax = new Vector2(0f, 0f);
        public Vector2 panelPivot = new Vector2(0f, 0f);
        public Vector2 titleOffsetMin = new Vector2(18f, -42f);
        public Vector2 titleOffsetMax = new Vector2(-18f, -10f);
        public Vector2 bodyViewportOffsetMin = new Vector2(0f, 18f);
        public Vector2 bodyViewportOffsetMax = new Vector2(0f, -46f);
        public Vector2 bodyOffsetMin = new Vector2(18f, 0f);
        public Vector2 bodyOffsetMax = new Vector2(-18f, 0f);
        public Vector2 bodyAnchoredPosition = new Vector2(0f, -18f);
        public float maxHeight = 320f;
    }

    private struct RuneStackEntry
    {
        public RuneDefinition rune;
        public int count;
    }

    private const int SkillCount = 4;
    private const int SlotsPerSkill = 5;
    private const string LabelEmpty = "空";
    private const string LabelNoRune = "无符文";
    private const string LabelSelectedRuneNone = "已选符文：无";
    private const string LabelRuneNameNone = "符文名称：无";
    private const string LabelTypePlaceholder = "类型：-";
    private const string LabelDescriptionPlaceholder = "说明：-";
    private const string LabelEffectPlaceholder = "效果：-";
    private const string LabelRuneFallback = "符文";
    private const string LabelEmptyRuneSlot = "空符文槽";
    private const string LogNoRuneSelected = "[RuneUI] Please select a rune first.";
    private const string LogNoAvailableRuneCopy = "[RuneUI] No available copy of this rune.";
    private const string LogMissingRuneInventory = "[RuneUI] Missing RuneInventory on current player. Rune list will show No rune.";
    private const string LogMissingRuneLibrary = "[RuneUI] Missing RuneLibrary in scene. Rune names may use fallback text.";
    private const string LogMissingRuneList = "[RuneUI] Missing runeListContent reference.";
    private const string LogMissingCombatSkillCaster = "[RuneUI] Missing CombatSkillCaster.";
    private const string LogMissingSlotRefs = "[RuneUI] Manual skill slot references are missing. Please assign qSlots / wSlots / eSlots / rSlots in the Inspector.";
    private const string LogMissingSkillIconRefs = "[RuneUI] Missing external skill icon references on rune panel. Please assign qSkillIcon / wSkillIcon / eSkillIcon / rSkillIcon in the Inspector.";
    private const string RunePanelDescriptionTracePrefix = "[RunePanelDescriptionTrace] ";
    private const string RunePanelHoverTracePrefix = "[RunePanelHoverTrace] ";
    private const string TooltipPositionTracePrefix = "[TooltipPositionTrace] ";
    private const string TooltipRuntimeTracePrefix = "[TooltipRuntimeTrace] ";

    [System.Serializable]
    public class RuneSlotView
    {
        public Button button;
        public TextMeshProUGUI label;
    }

    [System.Serializable]
    public class RuneSkillIconView
    {
        public RectTransform root;
        public Image icon;
        public Image hoverHighlight;
        public SkillHoverTrigger hoverTrigger;
    }

    [Header("Root")]
    public GameObject mainPanel;
    public Button closeButton;
    [SerializeField] private RectTransform runeInventoryPanel;
    [SerializeField] private ScrollRect runeInventoryScrollRect;
    [SerializeField] private RectTransform runeInventoryViewport;
    [SerializeField] private RectTransform runeInventoryContent;
    [SerializeField] private Scrollbar runeInventoryScrollbar;
    [SerializeField] private RectTransform runeDescriptionPanel;
    [SerializeField] private RectTransform runeDescriptionViewport;
    [SerializeField] private RectTransform runeDescriptionContent;
    [SerializeField] private RectTransform runeDescriptionBackground;
    public Transform runeListContent;
    public TextMeshProUGUI selectedRuneText;
    public TextMeshProUGUI noRuneText;
    public TextMeshProUGUI runeNameText;
    public TextMeshProUGUI runeTypeText;
    public TextMeshProUGUI runeDescriptionText;
    public TextMeshProUGUI runeEffectText;
    public RectTransform runeBagViewportRect;
    public RectTransform runeBagContentRoot;
    public Scrollbar runeBagScrollbar;
    public RectTransform detailPanelRoot;
    public TextMeshProUGUI sharedDescriptionText;
    public ScrollRect sharedDescriptionScrollRect;

    [Header("Skill Slots")]
    public RuneSlotView[] qSlots = new RuneSlotView[SlotsPerSkill];
    public RuneSlotView[] wSlots = new RuneSlotView[SlotsPerSkill];
    public RuneSlotView[] eSlots = new RuneSlotView[SlotsPerSkill];
    public RuneSlotView[] rSlots = new RuneSlotView[SlotsPerSkill];

    [Header("Skill Icons")]
    [SerializeField] private RuneSkillIconView qSkillIcon = new RuneSkillIconView();
    [SerializeField] private RuneSkillIconView wSkillIcon = new RuneSkillIconView();
    [SerializeField] private RuneSkillIconView eSkillIcon = new RuneSkillIconView();
    [SerializeField] private RuneSkillIconView rSkillIcon = new RuneSkillIconView();

    private RuneDefinition selectedRune;
    private RuneInventory currentRuneInventory;
    private RuneLibrary currentRuneLibrary;
    private CombatSkillCaster currentSkillCaster;
    private GameObject currentPlayer;

    private Player2Bootstrap cachedBootstrap;
    private bool cachedBootstrapEnabled;
    private bool hasCachedBootstrapState;
    private float previousTimeScale = 1f;
    private bool pauseApplied;
    private bool slotsBound;
    private bool warnedMissingSlotRefs;
    private bool warnedMissingSkillCaster;
    private bool warnedMissingRuneList;
    private bool warnedMissingRuneInventory;
    private bool warnedMissingRuneLibrary;
    private bool warnedMissingSelectedRune;
    private bool warnedAlreadyEquippedRune;
    private bool warnedMissingSkillIconRefs;
    private readonly Dictionary<string, Image> skillRowIcons = new Dictionary<string, Image>();
    private readonly Dictionary<string, Image> skillRowHighlights = new Dictionary<string, Image>();
    private readonly Dictionary<string, SkillHoverTrigger> skillRowHoverTriggers = new Dictionary<string, SkillHoverTrigger>();
    private readonly Dictionary<string, Image> attributeBarFills = new Dictionary<string, Image>();
    private readonly Dictionary<string, TextMeshProUGUI> attributeValueTexts = new Dictionary<string, TextMeshProUGUI>();
    private TextMeshProUGUI runeSkillPanelTitleText;
    private Vector2 runeSkillPanelTitleBaseAnchoredPosition;
    private bool hasRuneSkillPanelTitleBasePosition;
    private float skillRowFirstY;
    private bool hasSkillRowFirstY;
    private GameObject skillDescriptionPanel;
    private GameObject attributePanel;
    private TextMeshProUGUI skillDescriptionTitleText;
    private TextMeshProUGUI skillDescriptionBodyText;
    private RectTransform skillDescriptionBodyViewportRect;
    private TextMeshProUGUI attributePanelTitleText;
    private TextMeshProUGUI attributeFooterText;
    private RuntimeLootDropOnDeath lootDropPreview;
    private bool isSkillDescriptionHoverActive;
    private DescriptionSource currentDescriptionSource;
    private Transform runeButtonTemplate;
    private Canvas tooltipCanvas;
    private RectTransform tooltipCanvasRect;
    private Camera tooltipCanvasCamera;
    private Coroutine tooltipNextFrameTraceCoroutine;

    public bool IsPanelOpen => IsMainPanelVisible();

    [Header("Skill UI Info")]
    [SerializeField] private Color skillHoverHighlightColor = new Color(1f, 0.9f, 0.35f, 0.5f);
    [SerializeField] private Color skillDescriptionPanelColor = new Color(0.09f, 0.11f, 0.16f, 0.94f);
    [SerializeField] private Vector2 skillRowIconSize = new Vector2(64f, 64f);
    [SerializeField] private Vector2 runeSkillPanelTitleOffset = new Vector2(20f, 0f);
    [SerializeField] private float skillRowIconX = 58f;
    [SerializeField] private float skillRowSlotsStartX = 160f;
    [SerializeField] private float skillRowSlotSpacing = 96f;
    [SerializeField] private float skillRowVerticalSpacing = 42f;
    [SerializeField] private Vector2 skillDescriptionPanelSize = new Vector2(0f, 150f);
    [SerializeField] private Vector2 skillDescriptionPanelOffset = new Vector2(20f, 60f);
    [SerializeField] private Vector2 skillDescriptionPanelPadding = new Vector2(18f, 18f);
    [SerializeField, Min(120f)] private float skillDescriptionPanelMaxHeight = 320f;
    [SerializeField] private Vector2 tooltipOffset = new Vector2(16f, 16f);
    [SerializeField, Min(0f)] private float tooltipScreenPadding = 12f;
    [SerializeField] private bool debugTooltipPositioning = false;
    [SerializeField] private Vector2 runeBagItemSpacing = new Vector2(0f, 8f);
    [SerializeField, Min(1)] private int runeBagColumnCount = 1;
    [SerializeField] private Vector2 runeBagItemSize = new Vector2(0f, 40f);
    [SerializeField] private float runeBagContentTopPadding = 0f;
    [SerializeField] private float runeBagContentBottomPadding = 0f;
    [SerializeField] private bool hideLegacyRuneDetailPanel = true;
    [SerializeField] private bool buildUiAtRuntime = false;
    [SerializeField] private bool applyInventoryLayoutAtRuntime = false;
    [SerializeField] private bool applyDescriptionLayoutAtRuntime = false;
    [SerializeField] private RuneInventoryLayoutSettings inventoryLayoutSettings = new RuneInventoryLayoutSettings();
    [SerializeField] private RuneDescriptionLayoutSettings descriptionLayoutSettings = new RuneDescriptionLayoutSettings();

    [Header("Attribute Panel")]
    [SerializeField] private Color attributePanelColor = new Color(0.10f, 0.12f, 0.18f, 0.96f);
    [SerializeField] private Color attributeBarBackgroundColor = new Color(0.16f, 0.18f, 0.24f, 1f);
    [SerializeField] private Color attributeBarFillColor = new Color(0.92f, 0.76f, 0.30f, 1f);
    [SerializeField] private Vector2 attributePanelSize = new Vector2(320f, 300f);
    [SerializeField] private Vector2 attributePanelOffset = new Vector2(-24f, -20f);
    [SerializeField] private Vector2 attributePanelPadding = new Vector2(18f, 16f);
    [SerializeField] private float attributeRowHeight = 26f;
    [SerializeField] private float attributeRowSpacing = 12f;
    [SerializeField] private float attributeLabelWidth = 46f;
    [SerializeField] private float attributeValueWidth = 54f;
    [SerializeField] private float attributeTitleHeight = 28f;
    [SerializeField] private float attributeFooterHeight = 90f;
    [SerializeField, Min(1f)] private float attributeHpDisplayMax = 300f;
    [SerializeField, Min(1f)] private float attributeAtkDisplayMax = 40f;
    [SerializeField, Min(1f)] private float attributeDefDisplayMax = 30f;
    [SerializeField, Min(1f)] private float attributeMagDisplayMax = 40f;
    [SerializeField, Min(1f)] private float attributeResDisplayMax = 30f;

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (mainPanel != null)
        {
            mainPanel.SetActive(false);
        }

        BindCloseButton();
        BindSlotButtons();
        ResolveCurrentPlayerContext();
        EnsureRuneBagLayoutUI();
        EnsureSkillInfoUI();
        RefreshRuneList();
        RefreshSkillSlots();
        RefreshSkillInfoVisuals();
        SetSelectedRune(null);
    }

    private void OnEnable()
    {
        GameLocalization.LanguageChanged += OnLanguageChanged;
        if (!Application.isPlaying)
        {
            return;
        }

        if (mainPanel != null)
        {
            mainPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            TogglePanel();
        }

    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= OnLanguageChanged;
        if (!Application.isPlaying)
        {
            return;
        }

        RestoreState();
    }

    private void OnDestroy()
    {
        GameLocalization.LanguageChanged -= OnLanguageChanged;
        if (!Application.isPlaying)
        {
            return;
        }

        RestoreState();
    }

    private void OnLanguageChanged(GameLanguage language)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RefreshRuneList();
        RefreshSkillSlots();
        SetSelectedRune(selectedRune);
        RefreshStaticRunePanelLabels();
        RefreshSkillInfoVisuals();
    }

    private static string Localize(string text)
    {
        return GameLocalization.Instance != null ? GameLocalization.Instance.Translate(text) : text;
    }

    public void TogglePanel()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (IsMainPanelVisible())
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    public void OpenPanel()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        CacheBootstrap();
        CloseCharacterPanelForExclusiveDisplay();
        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
        }

        SetPauseState(true);
        SetOldHudVisible(false);
        ResolveCurrentPlayerContext();
        EnsureRuneBagLayoutUI();
        EnsureSkillInfoUI();
        RefreshStaticRunePanelLabels();
        RefreshRuneList();
        RefreshSkillSlots();
        RefreshSkillInfoVisuals();
        Debug.Log("[RuneUI] Open panel, pause game, hide HUD", this);
    }

    public void ClosePanel()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (mainPanel != null)
        {
            mainPanel.SetActive(false);
        }

        SetOldHudVisible(true);
        SetPauseState(false);
        Debug.Log($"[RuneUI] Close panel, restore timeScale={Time.timeScale}, show HUD", this);
    }

    public void RefreshRuneList()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveCurrentPlayerContext();
        EnsureRuneBagLayoutUI();
        if (runeListContent == null)
        {
            if (!warnedMissingRuneList)
            {
                warnedMissingRuneList = true;
                Debug.LogWarning(LogMissingRuneList, this);
            }
            return;
        }

        List<RuneStackEntry> visibleRuneStacks = BuildVisibleRuneStacks();
        int runeCount = visibleRuneStacks.Count;
        bool hasRuneEntries = runeCount > 0;
        EnsureRuneButtonTemplate();
        EnsureRuneListItemCount(runeCount);
        int childCount = runeListContent.childCount;

        if (noRuneText != null)
        {
            noRuneText.gameObject.SetActive(!hasRuneEntries);
            noRuneText.text = hasRuneEntries ? string.Empty : LocalizeOrFallback("No rune", LabelNoRune);
        }

        for (int i = 0; i < childCount; i++)
        {
            Transform child = runeListContent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            RuneStackEntry runeEntry = i < runeCount ? visibleRuneStacks[i] : default(RuneStackEntry);
            RuneDefinition rune = runeEntry.rune;
            Button button = child.GetComponent<Button>();
            TextMeshProUGUI label = child.GetComponent<TextMeshProUGUI>();
            if (label == null)
            {
                label = child.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            child.gameObject.SetActive(rune != null);
            if (label != null)
            {
                if (rune != null)
                {
                    label.text = $"{GetRuneName(rune)} x{Mathf.Max(1, runeEntry.count)}";
                }
                else
                {
                    label.text = LocalizeOrFallback("Empty", LabelEmpty);
                }
            }

            if (button != null)
            {
                RuneDefinition capturedRune = rune;
                button.onClick.RemoveAllListeners();
                if (capturedRune != null)
                {
                    LogRunePanelDescriptionTrace(
                        "RuneButtonBound",
                        "button=" + child.name +
                        " runeId=" + capturedRune.runeId +
                        " runeName=" + GetRuneName(capturedRune) +
                        " runeDescriptionLength=" + GetRuneDescription(capturedRune).Length +
                        " iconSource=RuneDataButtonLabel iconAssigned=false");
                    button.onClick.AddListener(() => SelectRune(capturedRune));
                    BindRuneHoverEvents(button, capturedRune);
                }
                else
                {
                    RemoveRuneHoverEvents(button);
                }
            }

            RectTransform childRect = child as RectTransform;
            if (childRect != null)
            {
                int columns = Mathf.Max(1, runeBagColumnCount);
                float spacingX = Mathf.Max(0f, runeBagItemSpacing.x);
                float spacingY = Mathf.Max(0f, runeBagItemSpacing.y);
                float itemWidth = runeBagItemSize.x > 0f ? runeBagItemSize.x : childRect.sizeDelta.x;
                float itemHeight = runeBagItemSize.y > 0f ? runeBagItemSize.y : childRect.sizeDelta.y;
                childRect.anchorMin = new Vector2(0f, 1f);
                childRect.anchorMax = new Vector2(columns > 1 ? 0f : 1f, 1f);
                childRect.pivot = new Vector2(0f, 1f);
                if (columns > 1)
                {
                    childRect.sizeDelta = new Vector2(itemWidth, itemHeight);
                }
                else
                {
                    childRect.sizeDelta = new Vector2(0f, itemHeight);
                }

                int column = i % columns;
                int row = i / columns;
                float x = column * (itemWidth + spacingX);
                float y = -runeBagContentTopPadding - row * (itemHeight + spacingY);
                childRect.anchoredPosition = new Vector2(x, y);
            }
        }

        UpdateRuneListContentHeight(runeCount);
    }

    public void RefreshSkillSlots()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveCurrentPlayerContext();
        RefreshSkillGroup(qSlots, 0);
        RefreshSkillGroup(wSlots, 1);
        RefreshSkillGroup(eSlots, 2);
        RefreshSkillGroup(rSlots, 3);
        RefreshSkillInfoVisuals();
    }

    public RuneDefinition GetSelectedRune()
    {
        return selectedRune;
    }

    private void BindCloseButton()
    {
        if (closeButton == null)
        {
            return;
        }

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(ClosePanel);
    }

    private void BindSlotButtons()
    {
        if (!Application.isPlaying || slotsBound)
        {
            return;
        }

        BindSlotGroup(qSlots, 0);
        BindSlotGroup(wSlots, 1);
        BindSlotGroup(eSlots, 2);
        BindSlotGroup(rSlots, 3);
        slotsBound = true;
    }

    private void BindSlotGroup(RuneSlotView[] slots, int skillIndex)
    {
        if (slots == null || slots.Length < SlotsPerSkill)
        {
            WarnMissingSlotRefsOnce();
            return;
        }

        for (int i = 0; i < SlotsPerSkill; i++)
        {
            RuneSlotView slotView = slots[i];
            if (slotView == null || slotView.button == null || slotView.label == null)
            {
                WarnMissingSlotRefsOnce();
                continue;
            }

            int capturedSkillIndex = skillIndex;
            int capturedSlotIndex = i;
            slotView.button.onClick.RemoveAllListeners();
            slotView.button.onClick.AddListener(() => EquipSelectedRuneToSlot(capturedSkillIndex, capturedSlotIndex));
            EnsureRuneSlotHoverTrigger(slotView.button, capturedSkillIndex, capturedSlotIndex, GetEquippedRune(capturedSkillIndex, capturedSlotIndex));
        }
    }

    private void RefreshSkillGroup(RuneSlotView[] slots, int skillIndex)
    {
        if (slots == null || slots.Length < SlotsPerSkill)
        {
            return;
        }

        for (int i = 0; i < SlotsPerSkill; i++)
        {
            RuneSlotView slotView = slots[i];
            if (slotView == null || slotView.label == null)
            {
                continue;
            }

            RuneDefinition rune = GetEquippedRune(skillIndex, i);
            slotView.label.text = rune != null ? GetRuneName(rune) : LocalizeOrFallback("Empty", LabelEmpty);
            if (slotView.button != null)
            {
                EnsureRuneSlotHoverTrigger(slotView.button, skillIndex, i, rune);
            }
        }
    }

    private void EquipSelectedRuneToSlot(int skillIndex, int slotIndex)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveCurrentPlayerContext();
        if (currentSkillCaster == null)
        {
            if (!warnedMissingSkillCaster)
            {
                warnedMissingSkillCaster = true;
                Debug.LogWarning(LogMissingCombatSkillCaster, this);
            }
            return;
        }

        BattleSkill skill = currentSkillCaster.GetSkill(skillIndex);
        if (skill == null || skill.equippedRunes == null)
        {
            return;
        }

        if (slotIndex < 0 || slotIndex >= skill.equippedRunes.Length)
        {
            return;
        }

        // Slot clicks behave as a toggle: occupied slots unequip, empty slots consume one available copy.
        RuneDefinition equippedRune = skill.equippedRunes[slotIndex];
        if (equippedRune != null)
        {
            if (!RuneMatches(selectedRune, equippedRune))
            {
                SetSelectedRune(equippedRune);
                Debug.Log($"[RuneUI] Selected equipped rune from {GetSkillKeyName(skillIndex)} slot {slotIndex}", this);
                return;
            }

            skill.equippedRunes[slotIndex] = null;
            currentSkillCaster.RefreshRuneState();
            RefreshRuneList();
            RefreshSkillSlots();
            Debug.Log($"[RuneUI] Unequipped rune from {GetSkillKeyName(skillIndex)} slot {slotIndex}", this);
            return;
        }

        if (selectedRune == null)
        {
            if (!warnedMissingSelectedRune)
            {
                warnedMissingSelectedRune = true;
                Debug.LogWarning(LogNoRuneSelected, this);
            }
            return;
        }

        int availableCount = GetAvailableRuneCount(selectedRune);
        if (availableCount <= 0)
        {
            if (!warnedAlreadyEquippedRune)
            {
                warnedAlreadyEquippedRune = true;
                Debug.LogWarning(LogNoAvailableRuneCopy, this);
            }
            return;
        }

        skill.equippedRunes[slotIndex] = selectedRune;
        currentSkillCaster.RefreshRuneState();
        RefreshRuneList();
        RefreshSkillSlots();
        RefreshSelectedRuneDetails(selectedRune);
    }

    private void SelectRune(RuneDefinition rune)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        SetSelectedRune(rune);
    }

    private void SetSelectedRune(RuneDefinition rune)
    {
        selectedRune = rune;
        LogRunePanelDescriptionTrace(
            "RuneSelected",
            "runeId=" + (selectedRune != null ? selectedRune.runeId.ToString() : "null") +
            " runeName=" + (selectedRune != null ? GetRuneName(selectedRune) : "null") +
            " runeDataNull=" + (selectedRune == null) +
            " descriptionRaw=" + (selectedRune != null ? GetRuneDescription(selectedRune) : string.Empty));
        if (selectedRuneText != null)
        {
            selectedRuneText.text = selectedRune != null
                ? $"{LocalizeOrFallback("Selected Rune", "已选符文")}：{GetRuneName(selectedRune)}"
                : LocalizeOrFallback("Selected Rune: None", LabelSelectedRuneNone);
        }

        RefreshSelectedRuneDetails(selectedRune);
    }

    private void EnsureRuneBagLayoutUI()
    {
        if (mainPanel == null)
        {
            return;
        }

        if (runeInventoryPanel == null)
        {
            runeInventoryPanel = FindChildRecursive(mainPanel.transform, "RuneBagPanel") as RectTransform;
        }

        if (runeBagViewportRect == null)
        {
            runeBagViewportRect = FindChildRecursive(mainPanel.transform, "RuneListViewport") as RectTransform;
        }
        if (runeInventoryViewport == null)
        {
            runeInventoryViewport = runeBagViewportRect;
        }

        if (runeBagContentRoot == null)
        {
            runeBagContentRoot = FindChildRecursive(mainPanel.transform, "RuneListContent") as RectTransform;
        }
        if (runeInventoryContent == null)
        {
            runeInventoryContent = runeBagContentRoot;
        }

        if (detailPanelRoot == null)
        {
            detailPanelRoot = FindChildRecursive(mainPanel.transform, "RuneDetailPanel") as RectTransform;
        }

        if (runeBagScrollbar == null)
        {
            RectTransform bagPanel = runeInventoryPanel != null ? runeInventoryPanel : FindChildRecursive(mainPanel.transform, "RuneBagPanel") as RectTransform;
            if (bagPanel != null)
            {
                runeBagScrollbar = bagPanel.GetComponentInChildren<Scrollbar>(true);
            }
        }
        if (runeInventoryScrollbar == null)
        {
            runeInventoryScrollbar = runeBagScrollbar;
        }

        if (applyInventoryLayoutAtRuntime && noRuneText != null && runeBagViewportRect != null && noRuneText.transform.parent != runeBagViewportRect)
        {
            noRuneText.transform.SetParent(runeBagViewportRect, false);
        }

        if (hideLegacyRuneDetailPanel && detailPanelRoot != null)
        {
            detailPanelRoot.gameObject.SetActive(false);
        }

        if (runeBagViewportRect == null || runeBagContentRoot == null)
        {
            return;
        }

        ScrollRect scrollRect = null;
        RectTransform bagPanelRect = runeInventoryPanel != null ? runeInventoryPanel : FindChildRecursive(mainPanel.transform, "RuneBagPanel") as RectTransform;
        if (bagPanelRect != null)
        {
            scrollRect = bagPanelRect.GetComponent<ScrollRect>();
        }

        if (scrollRect == null)
        {
            scrollRect = runeBagViewportRect.GetComponentInParent<ScrollRect>();
        }
        if (runeInventoryScrollRect == null)
        {
            runeInventoryScrollRect = scrollRect;
        }

        if (scrollRect != null)
        {
            scrollRect.viewport = runeBagViewportRect;
            scrollRect.content = runeBagContentRoot;
            if (applyInventoryLayoutAtRuntime)
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                if (runeBagScrollbar != null)
                {
                    scrollRect.verticalScrollbar = runeBagScrollbar;
                    scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
                    runeBagScrollbar.direction = Scrollbar.Direction.BottomToTop;
                }
            }
        }

        ApplyInventoryLayoutIfEnabled();
    }

    private void EnsureRuneButtonTemplate()
    {
        if (runeButtonTemplate != null || runeListContent == null || runeListContent.childCount <= 0)
        {
            return;
        }

        runeButtonTemplate = runeListContent.GetChild(0);
    }

    private void EnsureRuneListItemCount(int requiredCount)
    {
        if (runeListContent == null || runeButtonTemplate == null)
        {
            return;
        }

        while (runeListContent.childCount < requiredCount)
        {
            Transform clone = Object.Instantiate(runeButtonTemplate, runeListContent);
            clone.name = $"RuneItem{runeListContent.childCount}";
            clone.gameObject.SetActive(true);
        }
    }

    private void UpdateRuneListContentHeight(int runeCount)
    {
        if (runeBagContentRoot == null || !applyInventoryLayoutAtRuntime)
        {
            return;
        }

        float itemHeight = runeBagItemSize.y > 0f
            ? runeBagItemSize.y
            : ResolveTemplateItemHeight();
        int columns = Mathf.Max(1, runeBagColumnCount);
        int rows = Mathf.Max(1, Mathf.CeilToInt(runeCount / (float)columns));
        float contentHeight = runeBagContentTopPadding
            + rows * itemHeight
            + Mathf.Max(0, rows - 1) * Mathf.Max(0f, runeBagItemSpacing.y)
            + runeBagContentBottomPadding;
        runeBagContentRoot.sizeDelta = new Vector2(runeBagContentRoot.sizeDelta.x, contentHeight);
    }

    private void ApplyInventoryLayoutIfEnabled()
    {
        if (!applyInventoryLayoutAtRuntime)
        {
            return;
        }

        if (runeInventoryPanel != null)
        {
            if (inventoryLayoutSettings.panelSize.x > 0f || inventoryLayoutSettings.panelSize.y > 0f)
            {
                runeInventoryPanel.sizeDelta = inventoryLayoutSettings.panelSize;
            }

            runeInventoryPanel.anchoredPosition = inventoryLayoutSettings.panelAnchoredPosition;
        }

        if (runeInventoryViewport != null)
        {
            if (inventoryLayoutSettings.viewportSize.x > 0f || inventoryLayoutSettings.viewportSize.y > 0f)
            {
                runeInventoryViewport.sizeDelta = inventoryLayoutSettings.viewportSize;
            }

            runeInventoryViewport.anchoredPosition = inventoryLayoutSettings.viewportAnchoredPosition;
            runeInventoryViewport.offsetMin = inventoryLayoutSettings.viewportOffsetMin;
            runeInventoryViewport.offsetMax = inventoryLayoutSettings.viewportOffsetMax;
        }

        if (runeInventoryContent != null)
        {
            runeInventoryContent.anchorMin = inventoryLayoutSettings.contentAnchorMin;
            runeInventoryContent.anchorMax = inventoryLayoutSettings.contentAnchorMax;
            runeInventoryContent.pivot = inventoryLayoutSettings.contentPivot;
            runeInventoryContent.anchoredPosition = inventoryLayoutSettings.contentAnchoredPosition;
            if (inventoryLayoutSettings.contentSize.x > 0f || inventoryLayoutSettings.contentSize.y > 0f)
            {
                runeInventoryContent.sizeDelta = inventoryLayoutSettings.contentSize;
            }
        }

        runeBagItemSpacing = inventoryLayoutSettings.itemSpacing;
        runeBagItemSize = inventoryLayoutSettings.itemSize;
        runeBagContentTopPadding = inventoryLayoutSettings.contentTopPadding;
        runeBagContentBottomPadding = inventoryLayoutSettings.contentBottomPadding;
        runeBagColumnCount = Mathf.Max(1, inventoryLayoutSettings.columnCount);
    }

    private float ResolveTemplateItemHeight()
    {
        if (runeButtonTemplate is RectTransform rectTransform)
        {
            return Mathf.Max(1f, rectTransform.sizeDelta.y);
        }

        return 40f;
    }

    private void BindRuneHoverEvents(Button button, RuneDefinition rune)
    {
        if (button == null)
        {
            return;
        }

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }

        if (trigger.triggers == null)
        {
            trigger.triggers = new List<EventTrigger.Entry>();
        }
        else
        {
            trigger.triggers.Clear();
        }

        EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener(_ => ShowRuneDescription(rune, button.transform as RectTransform, button.gameObject.name));
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener(_ => RestoreSharedDescription());
        trigger.triggers.Add(exitEntry);
    }

    private void RemoveRuneHoverEvents(Button button)
    {
        if (button == null)
        {
            return;
        }

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger != null && trigger.triggers != null)
        {
            trigger.triggers.Clear();
        }
    }

    private void BindEquippedSlotHoverEvents(Button button, int skillIndex, RuneDefinition rune)
    {
        EnsureRuneSlotHoverTrigger(button, skillIndex, 0, rune);
    }

    private void ResolveCurrentPlayerContext()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RuneUIContextResolver.Resolve(
            out currentPlayer,
            out currentRuneLibrary,
            out currentSkillCaster,
            out currentRuneInventory);

        if (currentRuneInventory == null && !warnedMissingRuneInventory)
        {
            warnedMissingRuneInventory = true;
            Debug.LogWarning(LogMissingRuneInventory, this);
        }

        if (currentRuneLibrary == null && !warnedMissingRuneLibrary)
        {
            warnedMissingRuneLibrary = true;
            Debug.LogWarning(LogMissingRuneLibrary, this);
        }
    }

    private RuneDefinition GetRuneAtIndex(int index)
    {
        if (index < 0)
        {
            return null;
        }

        List<RuneDefinition> visibleRunes = BuildVisibleRuneList();
        if (index >= 0 && index < visibleRunes.Count)
        {
            return visibleRunes[index];
        }

        return null;
    }

    private RuneDefinition GetEquippedRune(int skillIndex, int slotIndex)
    {
        if (currentSkillCaster == null)
        {
            return null;
        }

        BattleSkill skill = currentSkillCaster.GetSkill(skillIndex);
        if (skill == null || skill.equippedRunes == null || slotIndex < 0 || slotIndex >= skill.equippedRunes.Length)
        {
            return null;
        }

        return skill.equippedRunes[slotIndex];
    }

    private string GetRuneName(RuneDefinition rune)
    {
        if (rune == null)
        {
            return LocalizeOrFallback("Empty", LabelEmpty);
        }

        if (rune.runeType != RuneType.None)
        {
            return RuneDefinition.GetLocalizedName(rune.runeType);
        }

        if (!string.IsNullOrWhiteSpace(rune.runeName) && !IsKnownEnglishRuneName(rune.runeName))
        {
            return Localize(rune.runeName);
        }

        return LocalizeOrFallback("Rune", LabelRuneFallback);
    }

    private string GetSkillKeyName(int skillIndex)
    {
        switch (skillIndex)
        {
            case 0:
                return "Q";
            case 1:
                return "W";
            case 2:
                return "E";
            case 3:
                return "R";
            default:
                return $"Skill{skillIndex}";
        }
    }

    private List<RuneDefinition> BuildVisibleRuneList()
    {
        // The bag is a filtered view of inventory: equipped copies are hidden, not deleted.
        List<RuneDefinition> visibleRunes = new List<RuneDefinition>();
        if (currentRuneInventory == null || currentRuneInventory.Count <= 0)
        {
            return visibleRunes;
        }

        Dictionary<string, int> hiddenCopiesByKey = new Dictionary<string, int>();
        for (int i = 0; i < currentRuneInventory.Count; i++)
        {
            RuneDefinition rune = currentRuneInventory.GetRune(i);
            if (rune == null)
            {
                continue;
            }

            string runeKey = GetRuneStackKey(rune);
            int equippedCopies = CountEquippedRuneCopies(rune);
            int hiddenCopies = 0;
            hiddenCopiesByKey.TryGetValue(runeKey, out hiddenCopies);
            if (hiddenCopies < equippedCopies)
            {
                hiddenCopiesByKey[runeKey] = hiddenCopies + 1;
                continue;
            }

            visibleRunes.Add(rune);
        }

        return visibleRunes;
    }

    private List<RuneStackEntry> BuildVisibleRuneStacks()
    {
        List<RuneDefinition> visibleRunes = BuildVisibleRuneList();
        List<RuneStackEntry> visibleRuneStacks = new List<RuneStackEntry>();
        if (visibleRunes.Count == 0)
        {
            return visibleRuneStacks;
        }

        Dictionary<string, int> stackIndices = new Dictionary<string, int>();
        for (int i = 0; i < visibleRunes.Count; i++)
        {
            RuneDefinition rune = visibleRunes[i];
            if (rune == null)
            {
                continue;
            }

            string runeKey = GetRuneStackKey(rune);
            int stackIndex;
            if (stackIndices.TryGetValue(runeKey, out stackIndex))
            {
                RuneStackEntry entry = visibleRuneStacks[stackIndex];
                entry.count++;
                visibleRuneStacks[stackIndex] = entry;
                continue;
            }

            stackIndices[runeKey] = visibleRuneStacks.Count;
            visibleRuneStacks.Add(new RuneStackEntry
            {
                rune = rune,
                count = 1
            });
        }

        return visibleRuneStacks;
    }

    private bool IsRuneAlreadyEquipped(RuneDefinition rune)
    {
        return GetAvailableRuneCount(rune) <= 0;
    }

    private int GetAvailableRuneCount(RuneDefinition rune)
    {
        if (rune == null)
        {
            return 0;
        }

        int inventoryCount = CountRuneCopiesInInventory(rune);
        int equippedCount = CountEquippedRuneCopies(rune);
        return Mathf.Max(0, inventoryCount - equippedCount);
    }

    private int CountRuneCopiesInInventory(RuneDefinition rune)
    {
        if (rune == null || currentRuneInventory == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < currentRuneInventory.Count; i++)
        {
            if (RuneMatches(currentRuneInventory.GetRune(i), rune))
            {
                count++;
            }
        }

        return count;
    }

    private int CountEquippedRuneCopies(RuneDefinition rune)
    {
        if (rune == null)
        {
            return 0;
        }

        // Multiple copies can exist, so we count equipped copies instead of using a boolean flag.
        int count = 0;
        CombatSkillCaster[] casters = Object.FindObjectsOfType<CombatSkillCaster>(true);
        for (int casterIndex = 0; casterIndex < casters.Length; casterIndex++)
        {
            count += CountEquippedRuneCopies(casters[casterIndex], rune);
        }

        return count;
    }

    private int CountEquippedRuneCopies(CombatSkillCaster caster, RuneDefinition rune)
    {
        if (caster == null || rune == null)
        {
            return 0;
        }

        int count = 0;
        for (int skillIndex = 0; skillIndex < SkillCount; skillIndex++)
        {
            BattleSkill skill = caster.GetSkill(skillIndex);
            if (skill == null || skill.equippedRunes == null)
            {
                continue;
            }

            for (int i = 0; i < skill.equippedRunes.Length; i++)
            {
                if (RuneMatches(skill.equippedRunes[i], rune))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private string GetRuneStackKey(RuneDefinition rune)
    {
        if (rune == null)
        {
            return "null";
        }

        if (rune.runeId != 0)
        {
            return $"id:{rune.runeId}";
        }

        if (!string.IsNullOrEmpty(rune.runeName))
        {
            return $"name:{rune.runeName}";
        }

        return $"ref:{rune.GetHashCode()}";
    }

    private bool RuneMatches(RuneDefinition a, RuneDefinition b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        if (a.runeId != 0 && a.runeId == b.runeId)
        {
            return true;
        }

        if (!string.IsNullOrEmpty(a.runeName) && !string.IsNullOrEmpty(b.runeName) && a.runeName == b.runeName)
        {
            return true;
        }

        return false;
    }

    private void RefreshSelectedRuneDetails(RuneDefinition rune)
    {
        if (rune == null)
        {
            if (runeNameText != null)
            {
                runeNameText.text = LocalizeOrFallback("Rune Name: None", LabelRuneNameNone);
            }

            if (runeTypeText != null)
            {
                runeTypeText.text = string.Empty;
            }

            if (runeDescriptionText != null)
            {
                runeDescriptionText.text = string.Empty;
            }

            if (runeEffectText != null)
            {
                runeEffectText.text = string.Empty;
            }

            RestoreSharedDescription();
            LogRunePanelDescriptionTrace("DescriptionSkipped", "reason=NoSelectedRune");
            return;
        }

        if (runeNameText != null)
        {
            runeNameText.text = $"{LocalizeOrFallback("Selected Rune", "已选符文")}：{GetRuneName(rune)}";
        }

        if (runeTypeText != null)
        {
            runeTypeText.text = string.Empty;
        }

        if (runeDescriptionText != null)
        {
            runeDescriptionText.text = string.Empty;
        }

        if (runeEffectText != null)
        {
            runeEffectText.text = string.Empty;
        }

        ShowRuneDescription(rune);
    }

    private void ShowRuneDescription(RuneDefinition rune, RectTransform targetRect = null, string sourceObject = null)
    {
        if (rune == null)
        {
            RestoreSharedDescription();
            LogRunePanelDescriptionTrace("DescriptionSkipped", "reason=NoSelectedRune");
            return;
        }

        RuneDefinition displayRune = GetDisplayRuneDefinition(rune) ?? rune;
        int equippedCount = ResolveCurrentRuneTypeEquippedCount(displayRune.runeType);
        string body = displayRune.runeType != RuneType.None
            ? RuneDefinition.GetLocalizedProgressiveDescription(displayRune.runeType, equippedCount)
            : displayRune.GetFullEffectDescription();
        body = string.IsNullOrWhiteSpace(body) ? $"ID: {displayRune.runeId}" : body.Trim();
        ShowSharedDescription(string.Empty, body, false, DescriptionSource.Rune, targetRect, sourceObject);
        LogRunePanelDescriptionTrace(
            "DescriptionUpdated",
            "source=Rune" +
            " runeId=" + rune.runeId +
            " runeType=" + displayRune.runeType +
            " equippedCount=" + equippedCount +
            " bodyLength=" + body.Length +
            " object=" + (string.IsNullOrWhiteSpace(sourceObject) ? GetRuneName(rune) : sourceObject));
    }

    private int ResolveCurrentRuneTypeEquippedCount(RuneType runeType)
    {
        if (runeType == RuneType.None)
        {
            return 0;
        }

        ResolveCurrentPlayerContext();
        RuneRuntimeState runtimeState = currentSkillCaster != null ? currentSkillCaster.GetComponent<RuneRuntimeState>() : null;
        return runtimeState != null ? runtimeState.GetGlobalRuneCount(runeType) : 0;
    }

    private void ShowSharedDescription(
        string title,
        string body,
        bool skillHover,
        DescriptionSource source,
        RectTransform targetRect = null,
        string sourceObject = null)
    {
        EnsureSkillDescriptionPanel();
        if (skillDescriptionPanel == null || skillDescriptionTitleText == null || skillDescriptionBodyText == null)
        {
            LogRunePanelDescriptionTrace("DescriptionSkipped", "reason=MissingDescriptionPanelReference");
            return;
        }

        isSkillDescriptionHoverActive = skillHover;
        currentDescriptionSource = source;
        skillDescriptionTitleText.text = title ?? string.Empty;
        skillDescriptionBodyText.text = body ?? string.Empty;
        skillDescriptionPanel.SetActive(true);
        EnsureTooltipDetachedOverlayParent();
        Canvas.ForceUpdateCanvases();
        RefreshSkillDescriptionPanelHeight();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(skillDescriptionPanel.transform as RectTransform);
        LogTooltipRuntimeTrace(
            "event=TooltipShowRequested" +
            " source=" + source +
            " controllerObject=" + name +
            " controllerInstanceId=" + GetInstanceID() +
            " tooltipObject=" + skillDescriptionPanel.name +
            " tooltipHierarchyPath=" + GetHierarchyPath(skillDescriptionPanel.transform) +
            " tooltipInstanceId=" + skillDescriptionPanel.GetInstanceID() +
            " targetObject=" + (targetRect != null ? targetRect.name : "null") +
            " targetHierarchyPath=" + GetHierarchyPath(targetRect) +
            " targetInstanceId=" + (targetRect != null ? targetRect.GetInstanceID().ToString() : "null"));
        PositionSharedDescriptionPanel(targetRect, sourceObject);
        if (sharedDescriptionScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            sharedDescriptionScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void ShowSkillDescriptionByKey(string key, int playerIndex, RectTransform targetRect = null, string sourceObject = null)
    {
        string normalizedKey = (key ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalizedKey))
        {
            LogRunePanelDescriptionTrace("DescriptionSkipped", "reason=EmptySkillKey");
            return;
        }

        SkillUIDefinitionEntry entry = SkillUIDefinitionDatabase.Get(playerIndex, normalizedKey);
        if (entry == null)
        {
            LogRunePanelDescriptionTrace(
                "DescriptionSkipped",
                "reason=MissingSkillUIDefinition skillKey=" + normalizedKey +
                " playerIndex=" + playerIndex);
            return;
        }

        ShowSharedDescription(
            SkillUIDefinitionDatabase.GetLocalizedTitle(entry),
            SkillUIDefinitionDatabase.BuildDetailBodyText(entry),
            true,
            DescriptionSource.Skill,
            targetRect,
            sourceObject);
        LogRunePanelDescriptionTrace(
            "DescriptionUpdated",
            "source=Skill" +
            " skillKey=" + normalizedKey +
            " playerIndex=" + playerIndex +
            " descriptionTextLength=" + SkillUIDefinitionDatabase.BuildDetailBodyText(entry).Length +
            " object=" + (string.IsNullOrWhiteSpace(sourceObject) ? normalizedKey + "SkillIcon" : sourceObject));
    }

    private void RestoreSharedDescription()
    {
        if (isSkillDescriptionHoverActive)
        {
            isSkillDescriptionHoverActive = false;
        }

        if (selectedRune != null)
        {
            ShowRuneDescription(selectedRune);
            return;
        }

        currentDescriptionSource = DescriptionSource.None;
        if (skillDescriptionPanel != null)
        {
            skillDescriptionPanel.SetActive(false);
        }

        if (skillDescriptionTitleText != null)
        {
            skillDescriptionTitleText.text = string.Empty;
        }

        if (skillDescriptionBodyText != null)
        {
            skillDescriptionBodyText.text = string.Empty;
        }
    }

    private void ShowEmptyRuneSlotDescription(int skillIndex, int slotIndex, Transform slotTransform)
    {
        string skillKey = GetSkillKeyName(skillIndex);
        ShowSharedDescription(
            LocalizeOrFallback("rune.empty_slot", LabelEmptyRuneSlot),
            string.Empty,
            false,
            DescriptionSource.EmptySlot,
            slotTransform as RectTransform,
            slotTransform != null ? slotTransform.name : null);
        LogRunePanelDescriptionTrace(
            "DescriptionUpdated",
            "source=EmptySlot" +
            " skillKey=" + skillKey +
            " slotIndex=" + slotIndex +
            " object=" + (slotTransform != null ? slotTransform.name : "null"));
    }

    private void SetPauseState(bool pause)
    {
        if (pause)
        {
            if (pauseApplied)
            {
                return;
            }

            pauseApplied = true;
            OverlayPanelStateCoordinator.SetRunePanelOpen(true);
            return;
        }

        if (!pauseApplied)
        {
            return;
        }

        pauseApplied = false;
        OverlayPanelStateCoordinator.SetRunePanelOpen(false);
    }

    private void CacheBootstrap()
    {
        if (cachedBootstrap == null)
        {
            cachedBootstrap = Object.FindObjectOfType<Player2Bootstrap>(true);
        }
    }

    private void SetOldHudVisible(bool visible)
    {
        // Keep Player2Bootstrap enabled so input remains available while the rune panel is open,
        // but hide the visible combat HUD to prevent it from overlapping the modal panel.
        PlayerStatusHUD[] statusHuds = FindObjectsOfType<PlayerStatusHUD>();
        foreach (PlayerStatusHUD statusHud in statusHuds)
        {
            if (statusHud != null)
                statusHud.SetDisplayVisible(visible);
        }
    }

    private bool IsMainPanelVisible()
    {
        return mainPanel != null && mainPanel.activeSelf;
    }

    private void RestoreState()
    {
        SetOldHudVisible(true);
        SetPauseState(false);
    }

    public void RefreshCurrentPlayerView()
    {
        if (!Application.isPlaying || !IsMainPanelVisible())
        {
            return;
        }

        ResolveCurrentPlayerContext();
        RefreshRuneList();
        RefreshSkillSlots();
        RefreshSkillInfoVisuals();
    }

    private void CloseCharacterPanelForExclusiveDisplay()
    {
        PlayerAttributePanelUI attributePanel = Object.FindObjectOfType<PlayerAttributePanelUI>(true);
        if (attributePanel != null && attributePanel.IsPanelOpen)
        {
            attributePanel.ClosePanel();
        }
    }

    private void WarnMissingSlotRefsOnce()
    {
        if (warnedMissingSlotRefs)
        {
            return;
        }

        warnedMissingSlotRefs = true;
        Debug.LogWarning(LogMissingSlotRefs, this);
    }

    private void WarnMissingSkillIconRefsOnce()
    {
        if (warnedMissingSkillIconRefs)
        {
            return;
        }

        warnedMissingSkillIconRefs = true;
        Debug.LogWarning(LogMissingSkillIconRefs, this);
    }

    private void EnsureSkillInfoUI()
    {
        if (mainPanel == null)
        {
            return;
        }

        EnsureSkillDescriptionPanel();
        CacheSkillRowIconViews();
        if (applyDescriptionLayoutAtRuntime)
        {
            ApplyRuneSkillPanelTitleLayout();
            ApplySkillRowVerticalLayout();
            ApplySkillRowLayout("Q");
            ApplySkillRowLayout("W");
            ApplySkillRowLayout("E");
            ApplySkillRowLayout("R");
        }
    }

    private void RefreshSkillInfoVisuals()
    {
        int playerIndex = ResolveCurrentPlayerIndex();
        PlayerSkillHUD skillHud = Object.FindObjectOfType<PlayerSkillHUD>(true);
        RefreshSkillRowIcon("Q", playerIndex, skillHud);
        RefreshSkillRowIcon("W", playerIndex, skillHud);
        RefreshSkillRowIcon("E", playerIndex, skillHud);
        RefreshSkillRowIcon("R", playerIndex, skillHud);
    }

    private void CacheSkillRowIconViews()
    {
        skillRowIcons.Clear();
        skillRowHighlights.Clear();
        skillRowHoverTriggers.Clear();

        AutoBindSkillIconView(qSkillIcon, "Q");
        AutoBindSkillIconView(wSkillIcon, "W");
        AutoBindSkillIconView(eSkillIcon, "E");
        AutoBindSkillIconView(rSkillIcon, "R");

        RegisterSkillIconView("Q", qSkillIcon);
        RegisterSkillIconView("W", wSkillIcon);
        RegisterSkillIconView("E", eSkillIcon);
        RegisterSkillIconView("R", rSkillIcon);
    }

    private void AutoBindSkillIconView(RuneSkillIconView view, string key)
    {
        if (view == null || mainPanel == null || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        string upperKey = key.Trim().ToUpperInvariant();
        Transform row = FindChildRecursive(mainPanel.transform, $"{upperKey}Row");
        Transform expectedIconRoot = row != null
            ? row.Find($"{upperKey}SkillIcon")
              ?? row.Find($"SkillIcon_{upperKey}")
              ?? row.Find("SkillIcon")
            : null;

        if (view.root == null || (expectedIconRoot != null && view.root.transform != expectedIconRoot))
        {
            view.root = expectedIconRoot as RectTransform;
        }

        if (view.root == null)
        {
            LogRunePanelHoverTrace(
                "SkillTriggerBindingSkipped",
                "skillKey=" + upperKey +
                " reason=SkillIconRootMissing");
            return;
        }

        if (view.icon == null)
        {
            view.icon = view.root.Find("Icon")?.GetComponent<Image>();
            if (view.icon == null)
            {
                view.icon = view.root.GetComponent<Image>();
            }
        }

        if (view.icon == null)
        {
            LogRunePanelHoverTrace(
                "SkillTriggerBindingSkipped",
                "skillKey=" + upperKey +
                " rootObject=" + view.root.name +
                " rootPath=" + GetHierarchyPath(view.root) +
                " reason=SkillIconImageMissing");
            return;
        }

        RemoveIncorrectSkillTriggersForRow(upperKey, row, view.icon.transform);

        if (view.hoverHighlight == null)
        {
            view.hoverHighlight = view.root.Find("HoverHighlight")?.GetComponent<Image>();
        }

        if (view.hoverHighlight != null)
        {
            view.hoverHighlight.raycastTarget = false;
        }

        view.icon.raycastTarget = true;

        SkillHoverTrigger iconTrigger = view.icon.GetComponent<SkillHoverTrigger>();
        if (iconTrigger == null)
        {
            iconTrigger = view.icon.gameObject.AddComponent<SkillHoverTrigger>();
        }

        if (view.root != null && view.root != view.icon.transform)
        {
            SkillHoverTrigger incorrectRootTrigger = view.root.GetComponent<SkillHoverTrigger>();
            if (incorrectRootTrigger != null && incorrectRootTrigger != iconTrigger)
            {
                LogRunePanelHoverTrace(
                    "IncorrectSkillTriggerRemoved",
                    "skillKey=" + upperKey +
                    " targetObject=" + view.root.name +
                    " targetPath=" + GetHierarchyPath(view.root) +
                    " isRuneSlot=" + IsRuneSlotName(view.root.name) +
                    " reason=RootWasNotIconGraphic");
                Destroy(incorrectRootTrigger);
            }
        }

        view.hoverTrigger = iconTrigger;
    }

    private void RegisterSkillIconView(string key, RuneSkillIconView view)
    {
        if (view == null || view.root == null || view.icon == null || string.IsNullOrWhiteSpace(key))
        {
            WarnMissingSkillIconRefsOnce();
            return;
        }

        string upperKey = key.Trim().ToUpperInvariant();
        skillRowIcons[upperKey] = view.icon;

        if (view.hoverHighlight != null)
        {
            view.hoverHighlight.color = skillHoverHighlightColor;
            view.hoverHighlight.raycastTarget = false;
            view.hoverHighlight.enabled = false;
            view.hoverHighlight.gameObject.SetActive(false);
            skillRowHighlights[upperKey] = view.hoverHighlight;
        }

        if (view.hoverTrigger != null)
        {
            view.hoverTrigger.skillKey = upperKey;
            view.hoverTrigger.entered = HandleRuneSkillHoverEnter;
            view.hoverTrigger.exited = HandleRuneSkillHoverExit;
            view.hoverTrigger.clicked = HandleRuneSkillClick;
            view.root.gameObject.SetActive(true);
            skillRowHoverTriggers[upperKey] = view.hoverTrigger;

            LogRunePanelHoverTrace(
                "SkillTriggerBinding",
                "skillKey=" + upperKey +
                " targetObject=" + view.hoverTrigger.gameObject.name +
                " targetPath=" + GetHierarchyPath(view.hoverTrigger.transform) +
                " hasImage=" + (view.icon != null) +
                " hasButton=" + (view.hoverTrigger.GetComponent<Button>() != null) +
                " isRuneSlot=" + IsRuneSlotName(view.hoverTrigger.gameObject.name) +
                " isSkillIcon=" + IsSkillIconName(view.hoverTrigger.gameObject.name));
        }
    }

    private void EnsureSkillDescriptionPanel()
    {
        Transform panelParent = FindChildRecursive(mainPanel.transform, "RuneSkillPanel");
        if (panelParent == null)
        {
            panelParent = mainPanel.transform;
        }

        if (skillDescriptionPanel != null)
        {
            EnsureSkillDescriptionScrollSetup();
            if (applyDescriptionLayoutAtRuntime)
            {
                ApplySkillDescriptionPanelLayout(skillDescriptionPanel.transform as RectTransform);
                ApplySkillDescriptionTextLayout();
            }
            return;
        }

        Transform existing = FindChildRecursive(panelParent, "SkillDescriptionPanel");
        if (existing != null)
        {
            skillDescriptionPanel = existing.gameObject;
            runeDescriptionPanel = existing as RectTransform;
            runeDescriptionBackground = existing as RectTransform;
            skillDescriptionTitleText = existing.Find("Title")?.GetComponent<TextMeshProUGUI>();
            skillDescriptionBodyText = existing.Find("Body")?.GetComponent<TextMeshProUGUI>();
            if (skillDescriptionBodyText == null)
            {
                skillDescriptionBodyText = existing.Find("BodyViewport/Body")?.GetComponent<TextMeshProUGUI>();
            }
            EnsureSkillDescriptionScrollSetup();
            if (applyDescriptionLayoutAtRuntime)
            {
                ApplySkillDescriptionPanelLayout(existing as RectTransform);
                ApplySkillDescriptionTextLayout();
            }
            skillDescriptionPanel.SetActive(false);
            return;
        }

        if (!buildUiAtRuntime)
        {
            return;
        }

        GameObject panel = new GameObject("SkillDescriptionPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(panelParent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        ApplySkillDescriptionPanelLayout(rect);
        runeDescriptionPanel = rect;
        runeDescriptionBackground = rect;

        Image background = panel.GetComponent<Image>();
        background.color = skillDescriptionPanelColor;
        background.raycastTarget = false;

        GameObject title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(panel.transform, false);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        skillDescriptionTitleText = title.GetComponent<TextMeshProUGUI>();
        skillDescriptionTitleText.fontSize = 24f;
        skillDescriptionTitleText.alignment = TextAlignmentOptions.MidlineLeft;
        skillDescriptionTitleText.enableWordWrapping = true;
        skillDescriptionTitleText.overflowMode = TextOverflowModes.Overflow;
        skillDescriptionTitleText.text = string.Empty;

        GameObject body = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
        body.transform.SetParent(panel.transform, false);
        RectTransform bodyRect = body.GetComponent<RectTransform>();
        skillDescriptionBodyText = body.GetComponent<TextMeshProUGUI>();
        skillDescriptionBodyText.fontSize = 18f;
        skillDescriptionBodyText.alignment = TextAlignmentOptions.TopLeft;
        skillDescriptionBodyText.enableWordWrapping = true;
        skillDescriptionBodyText.overflowMode = TextOverflowModes.Masking;
        skillDescriptionBodyText.text = string.Empty;

        skillDescriptionPanel = panel;
        EnsureSkillDescriptionScrollSetup();
        ApplySkillDescriptionTextLayout();
        skillDescriptionPanel.SetActive(false);
    }

    private void EnsureAttributePanel()
    {
        Transform panelParent = FindChildRecursive(mainPanel.transform, "RuneSkillPanel");
        if (panelParent == null)
        {
            panelParent = mainPanel.transform;
        }

        if (attributePanel != null)
        {
            if (applyDescriptionLayoutAtRuntime)
            {
                ApplyAttributePanelLayout(attributePanel.transform as RectTransform);
            }
            return;
        }

        Transform existing = FindChildRecursive(panelParent, "AttributePanel");
        if (existing != null)
        {
            attributePanel = existing.gameObject;
            attributePanelTitleText = existing.Find("Title")?.GetComponent<TextMeshProUGUI>();
            attributeFooterText = existing.Find("Footer")?.GetComponent<TextMeshProUGUI>();
            CacheAttributeRows(existing);
            if (applyDescriptionLayoutAtRuntime)
            {
                ApplyAttributePanelLayout(existing as RectTransform);
                ApplyAttributePanelTextLayout();
            }
            return;
        }

        if (!buildUiAtRuntime)
        {
            return;
        }

        GameObject panel = new GameObject("AttributePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(panelParent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        ApplyAttributePanelLayout(rect);

        Image background = panel.GetComponent<Image>();
        background.color = attributePanelColor;
        background.raycastTarget = false;

        GameObject title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(panel.transform, false);
        attributePanelTitleText = title.GetComponent<TextMeshProUGUI>();
        attributePanelTitleText.fontSize = 24f;
        attributePanelTitleText.alignment = TextAlignmentOptions.MidlineLeft;
        attributePanelTitleText.enableWordWrapping = false;
        attributePanelTitleText.text = Localize("Attributes");

        CreateAttributeRow(panel.transform, "HP");
        CreateAttributeRow(panel.transform, "ATK");
        CreateAttributeRow(panel.transform, "DEF");
        CreateAttributeRow(panel.transform, "MAG");
        CreateAttributeRow(panel.transform, "RES");

        GameObject footer = new GameObject("Footer", typeof(RectTransform), typeof(TextMeshProUGUI));
        footer.transform.SetParent(panel.transform, false);
        attributeFooterText = footer.GetComponent<TextMeshProUGUI>();
        attributeFooterText.fontSize = 18f;
        attributeFooterText.alignment = TextAlignmentOptions.TopLeft;
        attributeFooterText.enableWordWrapping = true;
        attributeFooterText.overflowMode = TextOverflowModes.Overflow;
        attributeFooterText.text = string.Empty;

        attributePanel = panel;
        ApplyAttributePanelTextLayout();
    }

    private void CreateAttributeRow(Transform parent, string key)
    {
        GameObject row = new GameObject($"{key}Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(row.transform, false);
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.text = key;

        GameObject barBackgroundObject = new GameObject("BarBackground", typeof(RectTransform), typeof(Image));
        barBackgroundObject.transform.SetParent(row.transform, false);
        Image barBackground = barBackgroundObject.GetComponent<Image>();
        barBackground.color = attributeBarBackgroundColor;
        barBackground.raycastTarget = false;

        GameObject barFillObject = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
        barFillObject.transform.SetParent(barBackgroundObject.transform, false);
        Image barFill = barFillObject.GetComponent<Image>();
        barFill.color = attributeBarFillColor;
        barFill.raycastTarget = false;

        GameObject valueObject = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
        valueObject.transform.SetParent(row.transform, false);
        TextMeshProUGUI value = valueObject.GetComponent<TextMeshProUGUI>();
        value.fontSize = 18f;
        value.alignment = TextAlignmentOptions.MidlineRight;
        value.enableWordWrapping = false;
        value.text = "0";

        attributeBarFills[key] = barFill;
        attributeValueTexts[key] = value;
    }

    private void CacheAttributeRows(Transform panelRoot)
    {
        attributeBarFills.Clear();
        attributeValueTexts.Clear();

        string[] keys = { "HP", "ATK", "DEF", "MAG", "RES" };
        for (int i = 0; i < keys.Length; i++)
        {
            Transform row = panelRoot.Find($"{keys[i]}Row");
            if (row == null)
            {
                continue;
            }

            Image fill = row.Find("BarBackground/BarFill")?.GetComponent<Image>();
            TextMeshProUGUI value = row.Find("Value")?.GetComponent<TextMeshProUGUI>();
            if (fill != null)
            {
                attributeBarFills[keys[i]] = fill;
            }

            if (value != null)
            {
                attributeValueTexts[keys[i]] = value;
            }
        }
    }

    private void ApplyRuneSkillPanelTitleLayout()
    {
        if (mainPanel == null)
        {
            return;
        }

        if (runeSkillPanelTitleText == null)
        {
            Transform titleTransform = FindChildRecursive(mainPanel.transform, "SkillTitleText");
            if (titleTransform != null)
            {
                runeSkillPanelTitleText = titleTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        if (runeSkillPanelTitleText == null)
        {
            return;
        }

        RectTransform titleRect = runeSkillPanelTitleText.rectTransform;
        if (!hasRuneSkillPanelTitleBasePosition)
        {
            runeSkillPanelTitleBaseAnchoredPosition = titleRect.anchoredPosition;
            hasRuneSkillPanelTitleBasePosition = true;
        }

        titleRect.anchoredPosition = runeSkillPanelTitleBaseAnchoredPosition + runeSkillPanelTitleOffset;
    }

    private void ApplySkillRowVerticalLayout()
    {
        if (mainPanel == null)
        {
            return;
        }

        RectTransform qRow = FindChildRecursive(mainPanel.transform, "QRow") as RectTransform;
        if (qRow == null)
        {
            return;
        }

        if (!hasSkillRowFirstY)
        {
            skillRowFirstY = qRow.anchoredPosition.y;
            hasSkillRowFirstY = true;
        }

        string[] rowKeys = { "Q", "W", "E", "R" };
        for (int i = 0; i < rowKeys.Length; i++)
        {
            RectTransform rowRect = FindChildRecursive(mainPanel.transform, $"{rowKeys[i]}Row") as RectTransform;
            if (rowRect == null)
            {
                continue;
            }

            Vector2 anchoredPosition = rowRect.anchoredPosition;
            anchoredPosition.y = skillRowFirstY - (skillRowVerticalSpacing * i);
            rowRect.anchoredPosition = anchoredPosition;
        }
    }

    private void ApplySkillDescriptionPanelLayout(RectTransform rect)
    {
        if (rect == null || !applyDescriptionLayoutAtRuntime)
        {
            return;
        }

        RectTransform parentRect = rect.parent as RectTransform;
        float width = descriptionLayoutSettings.panelSize.x > 0f
            ? descriptionLayoutSettings.panelSize.x
            : skillDescriptionPanelSize.x;
        if (width <= 0f && parentRect != null)
        {
            width = Mathf.Max(0f, parentRect.rect.width - (descriptionLayoutSettings.panelAnchoredPosition.x * 2f));
        }

        if (parentRect != null)
        {
            float maxWidth = Mathf.Max(0f, parentRect.rect.width - descriptionLayoutSettings.panelAnchoredPosition.x);
            width = Mathf.Min(width, maxWidth);
        }

        rect.anchorMin = descriptionLayoutSettings.panelAnchorMin;
        rect.anchorMax = descriptionLayoutSettings.panelAnchorMax;
        rect.pivot = descriptionLayoutSettings.panelPivot;
        rect.sizeDelta = new Vector2(width, Mathf.Max(1f, descriptionLayoutSettings.panelSize.y));
        rect.anchoredPosition = descriptionLayoutSettings.panelAnchoredPosition;
    }

    private void EnsureSkillDescriptionScrollSetup()
    {
        if (skillDescriptionPanel == null || skillDescriptionBodyText == null)
        {
            return;
        }

        if (sharedDescriptionScrollRect == null)
        {
            sharedDescriptionScrollRect = skillDescriptionPanel.GetComponent<ScrollRect>();
        }

        Transform viewportTransform = skillDescriptionPanel.transform.Find("BodyViewport");
        if (viewportTransform == null)
        {
            return;
        }

        skillDescriptionBodyViewportRect = viewportTransform as RectTransform;
        if (runeDescriptionViewport == null)
        {
            runeDescriptionViewport = skillDescriptionBodyViewportRect;
        }
        if (skillDescriptionBodyText.transform.parent != viewportTransform)
        {
            skillDescriptionBodyText.transform.SetParent(viewportTransform, false);
        }

        sharedDescriptionText = skillDescriptionBodyText;
        if (runeDescriptionContent == null && skillDescriptionBodyText != null)
        {
            runeDescriptionContent = skillDescriptionBodyText.rectTransform;
        }
        if (sharedDescriptionScrollRect != null)
        {
            sharedDescriptionScrollRect.viewport = skillDescriptionBodyViewportRect;
            sharedDescriptionScrollRect.content = skillDescriptionBodyText.rectTransform;
            if (applyDescriptionLayoutAtRuntime)
            {
                sharedDescriptionScrollRect.horizontal = false;
                sharedDescriptionScrollRect.vertical = true;
                sharedDescriptionScrollRect.movementType = ScrollRect.MovementType.Clamped;
                sharedDescriptionScrollRect.scrollSensitivity = 24f;

                Scrollbar existingScrollbar = skillDescriptionPanel.GetComponentInChildren<Scrollbar>(true);
                if (existingScrollbar != null)
                {
                    sharedDescriptionScrollRect.verticalScrollbar = existingScrollbar;
                    sharedDescriptionScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
                    existingScrollbar.direction = Scrollbar.Direction.BottomToTop;
                }
            }
        }
    }

    private void RefreshSkillDescriptionPanelHeight()
    {
        if (skillDescriptionPanel == null)
        {
            return;
        }

        EnsureSkillDescriptionScrollSetup();

        RectTransform panelRect = skillDescriptionPanel.transform as RectTransform;
        if (panelRect == null)
        {
            return;
        }

        if (applyDescriptionLayoutAtRuntime)
        {
            ApplySkillDescriptionPanelLayout(panelRect);
        }

        float baseHeight = applyDescriptionLayoutAtRuntime
            ? descriptionLayoutSettings.panelSize.y
            : Mathf.Max(1f, panelRect.rect.height > 0f ? panelRect.rect.height : panelRect.sizeDelta.y);
        float maxHeight = applyDescriptionLayoutAtRuntime
            ? Mathf.Max(descriptionLayoutSettings.panelSize.y, descriptionLayoutSettings.maxHeight)
            : Mathf.Max(baseHeight, skillDescriptionPanelMaxHeight);
        float panelWidth = panelRect.rect.width > 0f ? panelRect.rect.width : panelRect.sizeDelta.x;
        float contentWidth = Mathf.Max(40f, panelWidth - (skillDescriptionPanelPadding.x * 2f));
        float titleHeight = 0f;
        float bodyHeight = 0f;

        if (skillDescriptionTitleText != null)
        {
            skillDescriptionTitleText.enableWordWrapping = true;
            skillDescriptionTitleText.overflowMode = TextOverflowModes.Overflow;
            titleHeight = skillDescriptionTitleText.GetPreferredValues(skillDescriptionTitleText.text, contentWidth, Mathf.Infinity).y;
        }

        if (skillDescriptionBodyText != null)
        {
            skillDescriptionBodyText.enableWordWrapping = true;
            skillDescriptionBodyText.overflowMode = TextOverflowModes.Overflow;
            bodyHeight = skillDescriptionBodyText.GetPreferredValues(skillDescriptionBodyText.text, contentWidth, Mathf.Infinity).y;
        }

        float titleBodySpacing = titleHeight > 0f && bodyHeight > 0f ? 14f : 0f;
        float calculatedTextHeight = titleHeight + titleBodySpacing + bodyHeight;
        float finalHeight = Mathf.Max(baseHeight, calculatedTextHeight + (skillDescriptionPanelPadding.y * 2f) + 18f);
        finalHeight = Mathf.Min(finalHeight, maxHeight);
        panelRect.sizeDelta = new Vector2(panelWidth, finalHeight);

        if (applyDescriptionLayoutAtRuntime)
        {
            ApplySkillDescriptionTextLayout();
        }
    }

    private void ApplySkillDescriptionTextLayout()
    {
        if (!applyDescriptionLayoutAtRuntime)
        {
            return;
        }

        if (skillDescriptionTitleText != null)
        {
            RectTransform titleRect = skillDescriptionTitleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = descriptionLayoutSettings.titleOffsetMin;
            titleRect.offsetMax = descriptionLayoutSettings.titleOffsetMax;
        }

        if (skillDescriptionBodyText != null)
        {
            RectTransform bodyRect = skillDescriptionBodyText.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0.5f, 1f);
            float viewportWidth = skillDescriptionBodyViewportRect != null
                ? Mathf.Max(40f, skillDescriptionBodyViewportRect.rect.width - (skillDescriptionPanelPadding.x * 2f))
                : 200f;
            float preferredHeight = skillDescriptionBodyText.GetPreferredValues(skillDescriptionBodyText.text, viewportWidth, Mathf.Infinity).y;
            bodyRect.offsetMin = descriptionLayoutSettings.bodyOffsetMin;
            bodyRect.offsetMax = descriptionLayoutSettings.bodyOffsetMax;
            bodyRect.sizeDelta = new Vector2(0f, Mathf.Max(preferredHeight, 10f));
            bodyRect.anchoredPosition = descriptionLayoutSettings.bodyAnchoredPosition;
        }

        if (skillDescriptionBodyViewportRect != null)
        {
            skillDescriptionBodyViewportRect.offsetMin = descriptionLayoutSettings.bodyViewportOffsetMin;
            skillDescriptionBodyViewportRect.offsetMax = descriptionLayoutSettings.bodyViewportOffsetMax;
        }
    }

    private void ApplyAttributePanelLayout(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = attributePanelSize;
        rect.anchoredPosition = attributePanelOffset;
    }

    private void ApplyAttributePanelTextLayout()
    {
        if (attributePanel == null)
        {
            return;
        }

        RectTransform panelRect = attributePanel.transform as RectTransform;
        if (panelRect == null)
        {
            return;
        }

        float panelWidth = panelRect.sizeDelta.x;

        if (attributePanelTitleText != null)
        {
            RectTransform titleRect = attributePanelTitleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(attributePanelPadding.x, -attributeTitleHeight);
            titleRect.offsetMax = new Vector2(-attributePanelPadding.x, 0f);
        }

        string[] keys = { "HP", "ATK", "DEF", "MAG", "RES" };
        float rowsTopY = -attributePanelPadding.y - attributeTitleHeight - 8f;
        float barStartX = attributePanelPadding.x + attributeLabelWidth + 10f;
        float barEndX = panelWidth - attributePanelPadding.x - attributeValueWidth - 10f;
        float barWidth = Mathf.Max(40f, barEndX - barStartX);

        for (int i = 0; i < keys.Length; i++)
        {
            Transform row = attributePanel.transform.Find($"{keys[i]}Row");
            if (row == null)
            {
                continue;
            }

            RectTransform rowRect = row as RectTransform;
            float top = rowsTopY - i * (attributeRowHeight + attributeRowSpacing);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.offsetMin = new Vector2(0f, top - attributeRowHeight);
            rowRect.offsetMax = new Vector2(0f, top);

            RectTransform labelRect = row.Find("Label") as RectTransform;
            if (labelRect != null)
            {
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(0f, 1f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.anchoredPosition = new Vector2(attributePanelPadding.x, 0f);
                labelRect.sizeDelta = new Vector2(attributeLabelWidth, 0f);
            }

            RectTransform backgroundRect = row.Find("BarBackground") as RectTransform;
            if (backgroundRect != null)
            {
                backgroundRect.anchorMin = new Vector2(0f, 0.5f);
                backgroundRect.anchorMax = new Vector2(0f, 0.5f);
                backgroundRect.pivot = new Vector2(0f, 0.5f);
                backgroundRect.anchoredPosition = new Vector2(barStartX, 0f);
                backgroundRect.sizeDelta = new Vector2(barWidth, attributeRowHeight - 6f);
            }

            RectTransform fillRect = row.Find("BarBackground/BarFill") as RectTransform;
            if (fillRect != null)
            {
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(1f, 1f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
            }

            RectTransform valueRect = row.Find("Value") as RectTransform;
            if (valueRect != null)
            {
                valueRect.anchorMin = new Vector2(1f, 0f);
                valueRect.anchorMax = new Vector2(1f, 1f);
                valueRect.pivot = new Vector2(1f, 0.5f);
                valueRect.anchoredPosition = new Vector2(-attributePanelPadding.x, 0f);
                valueRect.sizeDelta = new Vector2(attributeValueWidth, 0f);
            }
        }

        if (attributeFooterText != null)
        {
            RectTransform footerRect = attributeFooterText.rectTransform;
            footerRect.anchorMin = new Vector2(0f, 0f);
            footerRect.anchorMax = new Vector2(1f, 0f);
            footerRect.pivot = new Vector2(0.5f, 0f);
            footerRect.offsetMin = new Vector2(attributePanelPadding.x, attributePanelPadding.y);
            footerRect.offsetMax = new Vector2(-attributePanelPadding.x, attributePanelPadding.y + attributeFooterHeight);
        }
    }

    private void ApplySkillRowLayout(string key)
    {
        if (mainPanel == null || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        string upperKey = key.Trim().ToUpperInvariant();
        Transform row = FindChildRecursive(mainPanel.transform, $"{upperKey}Row");
        if (row == null)
        {
            return;
        }

        List<RectTransform> slotRects = new List<RectTransform>();
        for (int i = 0; i < row.childCount; i++)
        {
            Transform child = row.GetChild(i);
            if (child == null || !child.name.StartsWith($"{upperKey}Slot"))
            {
                continue;
            }

            if (child is RectTransform childRect)
            {
                slotRects.Add(childRect);
            }
        }

        slotRects.Sort((a, b) => ExtractSlotIndex(a.name).CompareTo(ExtractSlotIndex(b.name)));
        for (int i = 0; i < slotRects.Count; i++)
        {
            RectTransform slotRect = slotRects[i];
            slotRect.anchorMin = new Vector2(0f, 0.5f);
            slotRect.anchorMax = new Vector2(0f, 0.5f);
            slotRect.pivot = new Vector2(0f, 0.5f);
            slotRect.anchoredPosition = new Vector2(skillRowSlotsStartX + (skillRowSlotSpacing * i), 0f);
        }
    }

    private void RefreshAttributePanel()
    {
        if (attributePanel == null)
        {
            return;
        }

        if (applyDescriptionLayoutAtRuntime)
        {
            ApplyAttributePanelLayout(attributePanel.transform as RectTransform);
            ApplyAttributePanelTextLayout();
        }

        CombatStats stats = currentPlayer != null ? BattleStatUtility.GetCombatStats(currentPlayer) : null;
        if (attributePanelTitleText != null)
        {
            attributePanelTitleText.text = Localize("Attributes");
        }

        RefreshAttributeBar("HP", stats != null ? stats.maxHealth : 0f, attributeHpDisplayMax);
        RefreshAttributeBar("ATK", stats != null ? stats.physicalAttack : 0f, attributeAtkDisplayMax);
        RefreshAttributeBar("DEF", stats != null ? stats.physicalDefense : 0f, attributeDefDisplayMax);
        RefreshAttributeBar("MAG", stats != null ? stats.specialAttack : 0f, attributeMagDisplayMax);
        RefreshAttributeBar("RES", stats != null ? stats.specialDefense : 0f, attributeResDisplayMax);

        if (attributeFooterText == null)
        {
            return;
        }

        float speed = stats != null ? Mathf.Max(0f, stats.speed) : 0f;
        float luck = stats != null ? Mathf.Max(0f, stats.luck) : 0f;
        float critRate = BattleStatUtility.GetCritRate(stats) * 100f;
        float extraSoulDrop = ResolveExtraSoulDropChance(luck) * 100f;
        float extraRuneDrop = ResolveExtraRuneDropChance(luck) * 100f;

        attributeFooterText.text =
            $"SPD  {speed:0.0}\n" +
            $"LUCK {luck:0}\n" +
            $"Crit Rate        {critRate:0.#}%\n" +
            $"Extra Soul Drop  {extraSoulDrop:0.#}%\n" +
            $"Extra Rune Drop  {extraRuneDrop:0.#}%";
    }

    private void RefreshAttributeBar(string key, float value, float displayMax)
    {
        if (attributeValueTexts.TryGetValue(key, out TextMeshProUGUI valueText) && valueText != null)
        {
            valueText.text = Mathf.RoundToInt(Mathf.Max(0f, value)).ToString();
        }

        if (!attributeBarFills.TryGetValue(key, out Image fill) || fill == null)
        {
            return;
        }

        float ratio = displayMax > 0f ? Mathf.Clamp01(Mathf.Max(0f, value) / displayMax) : 0f;
        RectTransform rect = fill.rectTransform;
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
            : Mathf.Max(0f, luck - 1f) * 0.025f;
    }

    private float ResolveExtraRuneDropChance(float luck)
    {
        RuntimeLootDropOnDeath preview = ResolveLootDropPreview();
        return preview != null
            ? preview.GetExtraRuneDropChanceForLuck(luck)
            : Mathf.Max(0f, luck - 1f) * 0.03f;
    }

    private RuntimeLootDropOnDeath ResolveLootDropPreview()
    {
        if (lootDropPreview == null)
        {
            lootDropPreview = Object.FindObjectOfType<RuntimeLootDropOnDeath>(true);
        }

        return lootDropPreview;
    }

    private static int ExtractSlotIndex(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        for (int i = name.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(name[i]))
            {
                if (int.TryParse(name.Substring(i + 1), out int value))
                {
                    return value;
                }

                break;
            }
        }

        return 0;
    }

    private void RefreshSkillRowIcon(string key, int playerIndex, PlayerSkillHUD skillHud)
    {
        if (!skillRowIcons.TryGetValue(key, out Image icon))
        {
            return;
        }

        Sprite sprite = skillHud != null ? skillHud.GetConfiguredSkillIcon(playerIndex, key) : null;
        icon.sprite = sprite;
        icon.enabled = sprite != null;

        if (skillRowHighlights.TryGetValue(key, out Image highlight))
        {
            highlight.sprite = sprite;
            highlight.color = skillHoverHighlightColor;
            highlight.enabled = false;
            highlight.gameObject.SetActive(false);
        }

        if (skillRowHoverTriggers.TryGetValue(key, out SkillHoverTrigger trigger))
        {
            trigger.playerIndex = playerIndex;
        }
    }

    private void HandleRuneSkillHoverEnter(SkillHoverTrigger trigger)
    {
        if (trigger == null)
        {
            return;
        }

        string key = (trigger.skillKey ?? string.Empty).Trim().ToUpperInvariant();
        if (skillRowHighlights.TryGetValue(key, out Image highlight))
        {
            highlight.enabled = true;
            highlight.gameObject.SetActive(true);
        }

        LogRunePanelHoverTrace(
            "PointerEnter",
            "skillKey=" + key +
            " targetObject=" + trigger.gameObject.name +
            " targetPath=" + GetHierarchyPath(trigger.transform) +
            " isRuneSlot=" + IsRuneSlotName(trigger.gameObject.name) +
            " isSkillIcon=" + IsSkillIconName(trigger.gameObject.name));
        ShowSkillDescriptionByKey(key, trigger.playerIndex, trigger.transform as RectTransform, trigger.gameObject.name);
    }

    private void HandleRuneSkillHoverExit(SkillHoverTrigger trigger)
    {
        if (trigger == null)
        {
            return;
        }

        string key = (trigger.skillKey ?? string.Empty).Trim().ToUpperInvariant();
        if (skillRowHighlights.TryGetValue(key, out Image highlight))
        {
            highlight.enabled = false;
            highlight.gameObject.SetActive(false);
        }

        isSkillDescriptionHoverActive = false;
        RestoreSharedDescription();
    }

    private void HandleRuneSkillClick(SkillHoverTrigger trigger)
    {
        if (trigger == null)
        {
            return;
        }

        string key = (trigger.skillKey ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(key))
        {
            LogRunePanelDescriptionTrace("DescriptionSkipped", "reason=SkillClickMissingKey");
            return;
        }

        LogRunePanelDescriptionTrace(
            "SkillSelected",
            "skillKey=" + key +
            " playerIndex=" + trigger.playerIndex +
            " iconSelectionSource=ExternalSkillIcon");
        ShowSkillDescriptionByKey(key, trigger.playerIndex, trigger.transform as RectTransform, trigger.gameObject.name);
    }

    public void HandleRuneSlotHoverEnter(int skillIndex, int slotIndex, RuneDefinition rune, Transform slotTransform)
    {
        LogRunePanelHoverTrace(
            "RuneSlotPointerEnter",
            "skillKey=" + GetSkillKeyName(skillIndex) +
            " slotIndex=" + slotIndex +
            " object=" + (slotTransform != null ? slotTransform.name : "null") +
            " hasRune=" + (rune != null));

        if (rune != null)
        {
            ShowRuneDescription(rune, slotTransform as RectTransform, slotTransform != null ? slotTransform.name : null);
            return;
        }

        ShowEmptyRuneSlotDescription(skillIndex, slotIndex, slotTransform);
    }

    public void HandleRuneSlotHoverExit(int skillIndex, int slotIndex, RuneDefinition rune, Transform slotTransform)
    {
        LogRunePanelHoverTrace(
            "RuneSlotPointerExit",
            "skillKey=" + GetSkillKeyName(skillIndex) +
            " slotIndex=" + slotIndex +
            " object=" + (slotTransform != null ? slotTransform.name : "null") +
            " sourceBeforeRestore=" + currentDescriptionSource);
        RestoreSharedDescription();
    }

    private void EnsureRuneSlotHoverTrigger(Button button, int skillIndex, int slotIndex, RuneDefinition rune)
    {
        if (button == null)
        {
            return;
        }

        DisableAndRemoveSkillHoverTrigger(button.transform, "RuneSlotMustNotShowSkillDescription");
        DisableAndRemoveSkillHoverTrigger(button.transform.parent, "RuneSlotMustNotShowSkillDescription");

        EventTrigger oldEventTrigger = button.GetComponent<EventTrigger>();
        if (oldEventTrigger != null)
        {
            oldEventTrigger.enabled = false;
            if (oldEventTrigger.triggers != null)
            {
                oldEventTrigger.triggers.Clear();
            }
        }

        RuneSlotHoverTrigger slotHoverTrigger = button.GetComponent<RuneSlotHoverTrigger>();
        if (slotHoverTrigger == null)
        {
            slotHoverTrigger = button.gameObject.AddComponent<RuneSlotHoverTrigger>();
        }

        slotHoverTrigger.Configure(this, skillIndex, slotIndex, rune);
    }

    private void DisableAndRemoveSkillHoverTrigger(Transform target, string reason)
    {
        if (target == null)
        {
            return;
        }

        SkillHoverTrigger[] triggers = target.GetComponentsInChildren<SkillHoverTrigger>(true);
        for (int i = 0; i < triggers.Length; i++)
        {
            SkillHoverTrigger trigger = triggers[i];
            if (trigger == null)
            {
                continue;
            }

            if (!IsRuneSlotObjectOrChild(trigger.transform))
            {
                continue;
            }

            trigger.enabled = false;
            LogRunePanelHoverTrace(
                "IncorrectSkillTriggerRemoved",
                "object=" + trigger.gameObject.name +
                " reason=" + reason);
            Destroy(trigger);
        }
    }

    private void RemoveIncorrectSkillTriggersForRow(string skillKey, Transform row, Transform validTriggerTransform)
    {
        if (row == null)
        {
            return;
        }

        SkillHoverTrigger[] triggers = row.GetComponentsInChildren<SkillHoverTrigger>(true);
        for (int i = 0; i < triggers.Length; i++)
        {
            SkillHoverTrigger trigger = triggers[i];
            if (trigger == null)
            {
                continue;
            }

            Transform triggerTransform = trigger.transform;
            if (triggerTransform == validTriggerTransform)
            {
                continue;
            }

            bool isRuneSlot = IsRuneSlotName(triggerTransform.name);
            bool isSkillRow = triggerTransform.name.EndsWith("Row");
            bool isInvalidBinding = isRuneSlot || isSkillRow || triggerTransform != row.Find($"{skillKey}SkillIcon");
            if (!isInvalidBinding)
            {
                continue;
            }

            LogRunePanelHoverTrace(
                "IncorrectSkillTriggerRemoved",
                "skillKey=" + skillKey +
                " targetObject=" + triggerTransform.name +
                " targetPath=" + GetHierarchyPath(triggerTransform) +
                " isRuneSlot=" + isRuneSlot +
                " isSkillIcon=" + IsSkillIconName(triggerTransform.name));
            Destroy(trigger);
        }
    }

    private void PositionSharedDescriptionPanel(RectTransform targetRect, string sourceObject)
    {
        if (targetRect == null || skillDescriptionPanel == null)
        {
            LogTooltipRuntimeTrace(
                "event=TooltipPositionSkipped" +
                " reason=" + (targetRect == null ? "TargetRectNull" : "TooltipPanelMissing") +
                " sourceObject=" + (string.IsNullOrWhiteSpace(sourceObject) ? "null" : sourceObject));
            return;
        }

        RectTransform tooltipRect = skillDescriptionPanel.transform as RectTransform;
        if (tooltipRect == null)
        {
            return;
        }

        if (!TryResolveTooltipCanvas(out Canvas canvas, out RectTransform canvasRect, out Camera canvasCamera))
        {
            return;
        }

        EnsureTooltipDetachedOverlayParent();

        Vector2[] targetCorners = GetTargetCornersInCanvasSpace(targetRect, canvasRect, canvasCamera);
        if (targetCorners == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);

        Vector2 tooltipSize = tooltipRect.rect.size;
        Rect canvasBounds = canvasRect.rect;
        TooltipPlacementCandidate[] candidates =
        {
            TooltipPlacementCandidate.AboveRight,
            TooltipPlacementCandidate.AboveLeft,
            TooltipPlacementCandidate.BelowRight,
            TooltipPlacementCandidate.BelowLeft
        };

        LogTooltipRuntimeTrace(
            "event=PositionFunctionEntered" +
            " targetObject=" + targetRect.name +
            " targetHierarchyPath=" + GetHierarchyPath(targetRect) +
            " targetInstanceId=" + targetRect.GetInstanceID() +
            " tooltipObject=" + skillDescriptionPanel.name +
            " tooltipInstanceId=" + skillDescriptionPanel.GetInstanceID() +
            " anchoredPositionBefore=" + tooltipRect.anchoredPosition +
            " tooltipSize=" + tooltipSize +
            " canvasObject=" + canvas.name +
            " canvasInstanceId=" + canvas.GetInstanceID() +
            " canvasRenderMode=" + canvas.renderMode +
            " sameCanvas=" + (targetRect.GetComponentInParent<Canvas>() == canvas));

        TooltipPlacementCandidate selectedCandidate = candidates[0];
        Vector2 selectedPivot = GetTooltipPivot(selectedCandidate);
        Vector2 selectedPosition = GetTooltipCandidatePosition(selectedCandidate, targetCorners);
        bool foundFit = false;
        bool clamped = false;

        for (int i = 0; i < candidates.Length; i++)
        {
            TooltipPlacementCandidate candidate = candidates[i];
            Vector2 pivot = GetTooltipPivot(candidate);
            Vector2 desiredPosition = GetTooltipCandidatePosition(candidate, targetCorners);
            if (TooltipFitsInsideCanvas(desiredPosition, pivot, tooltipSize, canvasBounds, tooltipScreenPadding))
            {
                selectedCandidate = candidate;
                selectedPivot = pivot;
                selectedPosition = desiredPosition;
                foundFit = true;
                break;
            }
        }

        if (!foundFit)
        {
            Vector2 unclampedPosition = selectedPosition;
            selectedPosition = ClampTooltipInsideCanvas(selectedPosition, selectedPivot, tooltipSize, canvasBounds, tooltipScreenPadding);
            clamped = unclampedPosition != selectedPosition;
        }

        tooltipRect.pivot = selectedPivot;
        tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.anchoredPosition = selectedPosition;

        LogTooltipRuntimeTrace(
            "event=PositionFunctionCompleted" +
            " targetObject=" + targetRect.name +
            " selectedCandidate=" + selectedCandidate +
            " anchoredPositionAfter=" + tooltipRect.anchoredPosition +
            " worldPositionAfter=" + tooltipRect.position +
            " clamped=" + clamped +
            " flippedHorizontal=" + (selectedCandidate == TooltipPlacementCandidate.AboveLeft || selectedCandidate == TooltipPlacementCandidate.BelowLeft) +
            " flippedVertical=" + (selectedCandidate == TooltipPlacementCandidate.BelowRight || selectedCandidate == TooltipPlacementCandidate.BelowLeft));

        if (tooltipNextFrameTraceCoroutine != null)
        {
            StopCoroutine(tooltipNextFrameTraceCoroutine);
        }

        tooltipNextFrameTraceCoroutine = StartCoroutine(
            TraceTooltipNextFrame(
                tooltipRect,
                selectedPosition,
                targetRect != null ? targetRect.name : (sourceObject ?? "unknown")));

        if (debugTooltipPositioning)
        {
            LogTooltipPositionTrace(
                "target=" + (string.IsNullOrWhiteSpace(sourceObject) ? targetRect.name : sourceObject) +
                " canvasMode=" + canvas.renderMode +
                " tooltipSize=" + tooltipSize +
                " preferredCandidate=" + candidates[0] +
                " selectedCandidate=" + selectedCandidate +
                " flippedHorizontal=" + (selectedCandidate == TooltipPlacementCandidate.AboveLeft || selectedCandidate == TooltipPlacementCandidate.BelowLeft) +
                " flippedVertical=" + (selectedCandidate == TooltipPlacementCandidate.BelowRight || selectedCandidate == TooltipPlacementCandidate.BelowLeft) +
                " clamped=" + clamped +
                " finalCanvasPosition=" + selectedPosition +
                " canvasBounds=" + canvasBounds);
        }
    }

    private bool TryResolveTooltipCanvas(out Canvas canvas, out RectTransform canvasRect, out Camera canvasCamera)
    {
        if (tooltipCanvas == null)
        {
            tooltipCanvas = mainPanel != null ? mainPanel.GetComponentInParent<Canvas>() : null;
            if (tooltipCanvas != null && tooltipCanvas.rootCanvas != null)
            {
                tooltipCanvas = tooltipCanvas.rootCanvas;
            }

            tooltipCanvasRect = tooltipCanvas != null ? tooltipCanvas.transform as RectTransform : null;
            tooltipCanvasCamera = tooltipCanvas != null && tooltipCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? tooltipCanvas.worldCamera
                : null;
        }

        canvas = tooltipCanvas;
        canvasRect = tooltipCanvasRect;
        canvasCamera = tooltipCanvasCamera;
        return canvas != null && canvasRect != null;
    }

    private Vector2[] GetTargetCornersInCanvasSpace(RectTransform targetRect, RectTransform canvasRect, Camera canvasCamera)
    {
        if (targetRect == null || canvasRect == null)
        {
            return null;
        }

        Vector3[] worldCorners = new Vector3[4];
        Vector2[] localCorners = new Vector2[4];
        targetRect.GetWorldCorners(worldCorners);

        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, worldCorners[i]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvasCamera, out localCorners[i]))
            {
                return null;
            }
        }

        return localCorners;
    }

    private Vector2 GetTooltipPivot(TooltipPlacementCandidate candidate)
    {
        switch (candidate)
        {
            case TooltipPlacementCandidate.AboveLeft:
                return new Vector2(1f, 0f);
            case TooltipPlacementCandidate.BelowRight:
                return new Vector2(0f, 1f);
            case TooltipPlacementCandidate.BelowLeft:
                return new Vector2(1f, 1f);
            default:
                return new Vector2(0f, 0f);
        }
    }

    private Vector2 GetTooltipCandidatePosition(TooltipPlacementCandidate candidate, Vector2[] targetCorners)
    {
        Vector2 bottomLeft = targetCorners[0];
        Vector2 topLeft = targetCorners[1];
        Vector2 topRight = targetCorners[2];
        Vector2 bottomRight = targetCorners[3];

        switch (candidate)
        {
            case TooltipPlacementCandidate.AboveLeft:
                return topLeft + new Vector2(-tooltipOffset.x, tooltipOffset.y);
            case TooltipPlacementCandidate.BelowRight:
                return bottomRight + new Vector2(tooltipOffset.x, -tooltipOffset.y);
            case TooltipPlacementCandidate.BelowLeft:
                return bottomLeft + new Vector2(-tooltipOffset.x, -tooltipOffset.y);
            default:
                return topRight + new Vector2(tooltipOffset.x, tooltipOffset.y);
        }
    }

    private bool TooltipFitsInsideCanvas(Vector2 candidatePosition, Vector2 pivot, Vector2 tooltipSize, Rect canvasBounds, float padding)
    {
        float left = candidatePosition.x - tooltipSize.x * pivot.x;
        float right = candidatePosition.x + tooltipSize.x * (1f - pivot.x);
        float bottom = candidatePosition.y - tooltipSize.y * pivot.y;
        float top = candidatePosition.y + tooltipSize.y * (1f - pivot.y);

        return left >= canvasBounds.xMin + padding
            && right <= canvasBounds.xMax - padding
            && bottom >= canvasBounds.yMin + padding
            && top <= canvasBounds.yMax - padding;
    }

    private Vector2 ClampTooltipInsideCanvas(Vector2 desiredPosition, Vector2 pivot, Vector2 tooltipSize, Rect canvasBounds, float padding)
    {
        float minPivotX = canvasBounds.xMin + padding + tooltipSize.x * pivot.x;
        float maxPivotX = canvasBounds.xMax - padding - tooltipSize.x * (1f - pivot.x);
        float minPivotY = canvasBounds.yMin + padding + tooltipSize.y * pivot.y;
        float maxPivotY = canvasBounds.yMax - padding - tooltipSize.y * (1f - pivot.y);

        return new Vector2(
            Mathf.Clamp(desiredPosition.x, minPivotX, maxPivotX),
            Mathf.Clamp(desiredPosition.y, minPivotY, maxPivotY));
    }

    private void EnsureTooltipDetachedOverlayParent()
    {
        if (skillDescriptionPanel == null)
        {
            return;
        }

        if (!TryResolveTooltipCanvas(out _, out RectTransform canvasRect, out _))
        {
            return;
        }

        RectTransform tooltipRect = skillDescriptionPanel.transform as RectTransform;
        if (tooltipRect == null)
        {
            return;
        }

        if (tooltipRect.parent != canvasRect)
        {
            Vector3 worldPosition = tooltipRect.position;
            Vector3 worldScale = tooltipRect.lossyScale;
            Quaternion worldRotation = tooltipRect.rotation;
            tooltipRect.SetParent(canvasRect, true);
            tooltipRect.position = worldPosition;
            tooltipRect.rotation = worldRotation;
            tooltipRect.localScale = Vector3.one;
            LayoutElement layoutElement = tooltipRect.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = tooltipRect.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.ignoreLayout = true;
            LogTooltipRuntimeTrace(
                "event=TooltipReparentedToCanvas" +
                " tooltipObject=" + skillDescriptionPanel.name +
                " tooltipHierarchyPath=" + GetHierarchyPath(skillDescriptionPanel.transform) +
                " canvasObject=" + canvasRect.name +
                " worldScaleBefore=" + worldScale);
        }

        skillDescriptionPanel.transform.SetAsLastSibling();
    }

    private IEnumerator TraceTooltipNextFrame(RectTransform tooltipRect, Vector2 expectedAnchoredPosition, string targetName)
    {
        yield return null;
        if (tooltipRect == null)
        {
            yield break;
        }

        Vector2 actualAnchoredPosition = tooltipRect.anchoredPosition;
        Vector2 delta = actualAnchoredPosition - expectedAnchoredPosition;
        LogTooltipRuntimeTrace(
            "event=NextFramePositionCheck" +
            " targetObject=" + targetName +
            " expectedAnchoredPosition=" + expectedAnchoredPosition +
            " actualAnchoredPosition=" + actualAnchoredPosition +
            " positionChangedAfterLayout=" + (delta.sqrMagnitude > 0.0001f) +
            " delta=" + delta);
        tooltipNextFrameTraceCoroutine = null;
    }

    private static bool IsRuneSlotName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.StartsWith("QSlot")
            || name.StartsWith("WSlot")
            || name.StartsWith("ESlot")
            || name.StartsWith("RSlot");
    }

    private static bool IsRuneSlotObjectOrChild(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (IsRuneSlotName(current.name))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool IsSkillIconName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.EndsWith("SkillIcon") || name.StartsWith("SkillIcon_");
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "<null>";
        }

        Stack<string> segments = new Stack<string>();
        Transform current = target;
        while (current != null)
        {
            segments.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", segments.ToArray());
    }

    private string GetRuneDescription(RuneDefinition rune)
    {
        RuneDefinition displayRune = GetDisplayRuneDefinition(rune);
        return displayRune != null && displayRune.runeType != RuneType.None
            ? RuneDefinition.GetLocalizedFlavor(displayRune.runeType)
            : string.Empty;
    }

    private static RuneDefinition GetDisplayRuneDefinition(RuneDefinition rune)
    {
        if (rune == null || rune.runeType == RuneType.None)
        {
            return rune;
        }

        RuneDefinition defaultRune = RuneDefinition.CreateDefaultRune(rune.runeType);
        return defaultRune ?? rune;
    }

    private static bool IsKnownEnglishRuneName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string normalized = name.Trim();
        return normalized == "Life Rune" ||
               normalized == "Shield Rune" ||
               normalized == "Mana Rune" ||
               normalized == "Thorn Rune" ||
               normalized == "Luck Rune";
    }

    private void RefreshStaticRunePanelLabels()
    {
        if (mainPanel == null)
        {
            return;
        }

        TextMeshProUGUI[] texts = mainPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null)
            {
                continue;
            }

            string value = (text.text ?? string.Empty).Trim();
            switch (value)
            {
                case "Rune Bag":
                    text.text = LocalizeOrFallback("Rune Bag", "符文背包");
                    break;
                case "Rune Skill Panel":
                    text.text = LocalizeOrFallback("Rune Skill Panel", "符文技能面板");
                    break;
                case "Empty":
                    text.text = LocalizeOrFallback("Empty", LabelEmpty);
                    break;
                case "符文背包":
                case "ルーンバッグ":
                    text.text = LocalizeOrFallback("Rune Bag", "符文背包");
                    break;
                case "符文技能面板":
                case "ルーンスキルパネル":
                    text.text = LocalizeOrFallback("Rune Skill Panel", "符文技能面板");
                    break;
                case "空":
                case "空き":
                    text.text = LocalizeOrFallback("Empty", LabelEmpty);
                    break;
            }
        }
    }

    private static string LocalizeOrFallback(string key, string fallback)
    {
        return GameLocalization.Instance != null
            ? GameLocalization.Instance.TranslateOrFallback(key, fallback)
            : fallback;
    }

    private void LogRunePanelDescriptionTrace(string eventName, string details)
    {
        Debug.Log(RunePanelDescriptionTracePrefix + "event=" + eventName + " " + details, this);
    }

    private void LogRunePanelHoverTrace(string eventName, string details)
    {
        Debug.Log(RunePanelHoverTracePrefix + "event=" + eventName + " " + details, this);
    }

    private void LogTooltipPositionTrace(string details)
    {
        Debug.Log(TooltipPositionTracePrefix + details, this);
    }

    private void LogTooltipRuntimeTrace(string details)
    {
        Debug.Log(TooltipRuntimeTracePrefix + details, this);
    }

    private int ResolveCurrentPlayerIndex()
    {
        if (currentPlayer != null)
        {
            if (currentPlayer.GetComponent<Player2PrototypeController>() != null)
            {
                return 2;
            }

            if (currentPlayer.GetComponent<Player01SkillController>() != null)
            {
                return 1;
            }
        }

        PlayerSkillHUD skillHud = Object.FindObjectOfType<PlayerSkillHUD>(true);
        return skillHud != null ? skillHud.CurrentPlayerIndex : 1;
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

    private void OnValidate()
    {
        inventoryLayoutSettings.columnCount = Mathf.Max(1, inventoryLayoutSettings.columnCount);

        if (!Application.isPlaying && mainPanel != null)
        {
            if (runeInventoryPanel == null)
            {
                runeInventoryPanel = FindChildRecursive(mainPanel.transform, "RuneBagPanel") as RectTransform;
            }

            if (runeInventoryViewport == null)
            {
                runeInventoryViewport = FindChildRecursive(mainPanel.transform, "RuneListViewport") as RectTransform;
            }

            if (runeInventoryContent == null)
            {
                runeInventoryContent = FindChildRecursive(mainPanel.transform, "RuneListContent") as RectTransform;
            }

            if (runeInventoryScrollRect == null && runeInventoryPanel != null)
            {
                runeInventoryScrollRect = runeInventoryPanel.GetComponent<ScrollRect>();
            }

            if (runeInventoryScrollbar == null && runeInventoryPanel != null)
            {
                runeInventoryScrollbar = runeInventoryPanel.GetComponentInChildren<Scrollbar>(true);
            }

            if (runeDescriptionPanel == null)
            {
                runeDescriptionPanel = FindChildRecursive(mainPanel.transform, "SkillDescriptionPanel") as RectTransform;
            }

            if (runeDescriptionViewport == null && runeDescriptionPanel != null)
            {
                runeDescriptionViewport = runeDescriptionPanel.Find("BodyViewport") as RectTransform;
            }

            if (runeDescriptionContent == null && runeDescriptionViewport != null)
            {
                runeDescriptionContent = runeDescriptionViewport.Find("Body") as RectTransform;
            }

            if (runeDescriptionBackground == null)
            {
                runeDescriptionBackground = runeDescriptionPanel;
            }

            AutoBindSkillIconView(qSkillIcon, "Q");
            AutoBindSkillIconView(wSkillIcon, "W");
            AutoBindSkillIconView(eSkillIcon, "E");
            AutoBindSkillIconView(rSkillIcon, "R");
        }
    }
}

public static class RuneUIContextResolver
{
    public static bool Resolve(
        out GameObject player,
        out RuneLibrary runeLibrary,
        out CombatSkillCaster skillCaster,
        out RuneInventory runeInventory)
    {
        player = null;
        runeLibrary = null;
        skillCaster = null;
        runeInventory = null;

        CombatSkillCaster[] casters = Object.FindObjectsOfType<CombatSkillCaster>(true);
        for (int i = 0; i < casters.Length; i++)
        {
            CombatSkillCaster caster = casters[i];
            if (caster != null && caster.isActiveAndEnabled && caster.gameObject.activeInHierarchy)
            {
                skillCaster = caster;
                player = caster.gameObject;
                break;
            }
        }

        if (player == null && casters.Length > 0)
        {
            skillCaster = casters[0];
            if (skillCaster != null)
            {
                player = skillCaster.gameObject;
            }
        }

        if (player != null)
        {
            if (skillCaster == null)
            {
                skillCaster = player.GetComponentInChildren<CombatSkillCaster>(true) ?? player.GetComponent<CombatSkillCaster>();
            }
        }

        runeInventory = FindSharedRuneInventory();
        if (runeInventory == null && player != null)
        {
            runeInventory = player.GetComponentInChildren<RuneInventory>(true) ?? player.GetComponent<RuneInventory>();
        }

        if (runeLibrary == null)
        {
            RuneLibrary[] libraries = Object.FindObjectsOfType<RuneLibrary>(true);
            if (libraries != null && libraries.Length > 0)
            {
                runeLibrary = libraries[0];
            }
        }

        return player != null || runeLibrary != null || runeInventory != null || skillCaster != null;
    }

    private static RuneInventory FindSharedRuneInventory()
    {
        RuneDropManager dropManager = Object.FindObjectOfType<RuneDropManager>(true);
        if (dropManager != null)
        {
            RuneInventory inventory = dropManager.GetComponent<RuneInventory>();
            if (inventory != null)
            {
                return inventory;
            }

            inventory = dropManager.GetComponentInChildren<RuneInventory>(true);
            if (inventory != null)
            {
                return inventory;
            }
        }

        RuneLibrary[] libraries = Object.FindObjectsOfType<RuneLibrary>(true);
        for (int i = 0; i < libraries.Length; i++)
        {
            RuneLibrary library = libraries[i];
            if (library == null)
            {
                continue;
            }

            RuneInventory inventory = library.GetComponent<RuneInventory>();
            if (inventory != null)
            {
                return inventory;
            }

            inventory = library.GetComponentInChildren<RuneInventory>(true);
            if (inventory != null)
            {
                return inventory;
            }
        }

        return null;
    }
}
