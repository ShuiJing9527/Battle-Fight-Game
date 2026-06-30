using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class RuneUIController : MonoBehaviour
{
    private struct RuneStackEntry
    {
        public RuneDefinition rune;
        public int count;
    }

    private const int SkillCount = 4;
    private const int SlotsPerSkill = 5;
    private const string LabelEmpty = "Empty";
    private const string LabelNoRune = "No rune";
    private const string LabelSelectedRuneNone = "Selected Rune: None";
    private const string LabelRuneNameNone = "Rune Name: None";
    private const string LabelTypePlaceholder = "Type: -";
    private const string LabelDescriptionPlaceholder = "Description: -";
    private const string LabelEffectPlaceholder = "Effect: -";
    private const string LabelRuneFallback = "Rune";
    private const string LogNoRuneSelected = "[RuneUI] Please select a rune first.";
    private const string LogNoAvailableRuneCopy = "[RuneUI] No available copy of this rune.";
    private const string LogMissingRuneInventory = "[RuneUI] Missing RuneInventory on current player. Rune list will show No rune.";
    private const string LogMissingRuneLibrary = "[RuneUI] Missing RuneLibrary in scene. Rune names may use fallback text.";
    private const string LogMissingRuneList = "[RuneUI] Missing runeListContent reference.";
    private const string LogMissingCombatSkillCaster = "[RuneUI] Missing CombatSkillCaster.";
    private const string LogMissingSlotRefs = "[RuneUI] Manual skill slot references are missing. Please assign qSlots / wSlots / eSlots / rSlots in the Inspector.";

    [System.Serializable]
    public class RuneSlotView
    {
        public Button button;
        public TextMeshProUGUI label;
    }

    [Header("Root")]
    public GameObject mainPanel;
    public Button closeButton;
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
    private Transform runeButtonTemplate;

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
    [SerializeField] private Vector2 runeBagItemSpacing = new Vector2(0f, 8f);
    [SerializeField, Min(1)] private int runeBagColumnCount = 1;
    [SerializeField] private Vector2 runeBagItemSize = new Vector2(0f, 40f);
    [SerializeField] private float runeBagContentTopPadding = 0f;
    [SerializeField] private float runeBagContentBottomPadding = 0f;
    [SerializeField] private bool hideLegacyRuneDetailPanel = true;

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
        if (!Application.isPlaying)
        {
            return;
        }

        RestoreState();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RestoreState();
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
            noRuneText.text = hasRuneEntries ? string.Empty : LabelNoRune;
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
                    label.text = LabelEmpty;
                }
            }

            if (button != null)
            {
                RuneDefinition capturedRune = rune;
                button.onClick.RemoveAllListeners();
                if (capturedRune != null)
                {
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
            slotView.label.text = rune != null ? GetRuneName(rune) : LabelEmpty;
            if (slotView.button != null)
            {
                BindEquippedSlotHoverEvents(slotView.button, skillIndex, rune);
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
        SetSelectedRune(null);
        RefreshRuneList();
        RefreshSkillSlots();
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
        if (selectedRuneText != null)
        {
            selectedRuneText.text = selectedRune != null ? $"Selected Rune: {GetRuneName(selectedRune)}" : LabelSelectedRuneNone;
        }

        RefreshSelectedRuneDetails(selectedRune);
    }

    private void EnsureRuneBagLayoutUI()
    {
        if (mainPanel == null)
        {
            return;
        }

        if (runeBagViewportRect == null)
        {
            runeBagViewportRect = FindChildRecursive(mainPanel.transform, "RuneListViewport") as RectTransform;
        }

        if (runeBagContentRoot == null)
        {
            runeBagContentRoot = FindChildRecursive(mainPanel.transform, "RuneListContent") as RectTransform;
        }

        if (detailPanelRoot == null)
        {
            detailPanelRoot = FindChildRecursive(mainPanel.transform, "RuneDetailPanel") as RectTransform;
        }

        if (runeBagScrollbar == null)
        {
            RectTransform bagPanel = FindChildRecursive(mainPanel.transform, "RuneBagPanel") as RectTransform;
            if (bagPanel != null)
            {
                runeBagScrollbar = bagPanel.GetComponentInChildren<Scrollbar>(true);
            }
        }

        if (noRuneText != null && runeBagViewportRect != null && noRuneText.transform.parent != runeBagViewportRect)
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
        RectTransform bagPanelRect = FindChildRecursive(mainPanel.transform, "RuneBagPanel") as RectTransform;
        if (bagPanelRect != null)
        {
            scrollRect = bagPanelRect.GetComponent<ScrollRect>();
        }

        if (scrollRect == null)
        {
            scrollRect = runeBagViewportRect.GetComponentInParent<ScrollRect>();
        }

        if (scrollRect != null)
        {
            scrollRect.viewport = runeBagViewportRect;
            scrollRect.content = runeBagContentRoot;
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
        if (runeBagContentRoot == null)
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
        enterEntry.callback.AddListener(_ => ShowRuneDescription(rune));
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

        RuneDefinition capturedRune = rune;
        int capturedSkillIndex = skillIndex;

        EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener(_ => ShowEquippedSlotDescription(capturedSkillIndex, capturedRune));
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener(_ => RestoreSharedDescription());
        trigger.triggers.Add(exitEntry);
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
            return LabelEmpty;
        }

        if (!string.IsNullOrEmpty(rune.runeName))
        {
            return rune.runeName;
        }

        return LabelRuneFallback;
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
                runeNameText.text = LabelRuneNameNone;
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
            return;
        }

        if (runeNameText != null)
        {
            runeNameText.text = $"Selected Rune: {GetRuneName(rune)}";
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

    private void ShowRuneDescription(RuneDefinition rune)
    {
        if (rune == null)
        {
            RestoreSharedDescription();
            return;
        }

        string rarityText = rune.rarity.ToString();
        string typeText = rune.GetTypeDisplayName();
        string description = string.IsNullOrWhiteSpace(rune.description) ? "-" : rune.description.Trim();
        string effectText = rune.GetFullEffectDescription();
        string body = $"Type: {typeText} / {rarityText}\n\nDescription:\n{description}\n\nEffect:\n{(string.IsNullOrWhiteSpace(effectText) ? $"ID: {rune.runeId}" : effectText)}";
        ShowSharedDescription($"Rune Name: {GetRuneName(rune)}", body, false);
    }

    private void ShowEquippedSlotDescription(int skillIndex, RuneDefinition rune)
    {
        if (rune != null)
        {
            ShowRuneDescription(rune);
            return;
        }

        ShowSkillDescriptionByKey(GetSkillKeyName(skillIndex), ResolveCurrentPlayerIndex());
    }

    private void ShowSharedDescription(string title, string body, bool skillHover)
    {
        EnsureSkillDescriptionPanel();
        if (skillDescriptionPanel == null || skillDescriptionTitleText == null || skillDescriptionBodyText == null)
        {
            return;
        }

        isSkillDescriptionHoverActive = skillHover;
        skillDescriptionTitleText.text = title ?? string.Empty;
        skillDescriptionBodyText.text = body ?? string.Empty;
        RefreshSkillDescriptionPanelHeight();
        skillDescriptionPanel.SetActive(true);
        if (sharedDescriptionScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            sharedDescriptionScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void ShowSkillDescriptionByKey(string key, int playerIndex)
    {
        string normalizedKey = (key ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalizedKey))
        {
            return;
        }

        SkillUIDefinitionEntry entry = SkillUIDefinitionDatabase.Get(playerIndex, normalizedKey);
        if (entry == null)
        {
            return;
        }

        ShowSharedDescription(
            string.IsNullOrWhiteSpace(entry.displayName) ? normalizedKey : entry.displayName,
            SkillUIDefinitionDatabase.BuildDetailBodyText(entry),
            true);
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
        // Keep Player2Bootstrap enabled so T character switching remains available while the rune panel is open.
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

    private void EnsureSkillInfoUI()
    {
        if (mainPanel == null)
        {
            return;
        }

        EnsureSkillDescriptionPanel();
        EnsureSkillRowIcon("Q");
        EnsureSkillRowIcon("W");
        EnsureSkillRowIcon("E");
        EnsureSkillRowIcon("R");
        ApplyRuneSkillPanelTitleLayout();
        ApplySkillRowVerticalLayout();
        ApplySkillRowLayout("Q");
        ApplySkillRowLayout("W");
        ApplySkillRowLayout("E");
        ApplySkillRowLayout("R");
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
            return;
        }

        Transform existing = FindChildRecursive(panelParent, "SkillDescriptionPanel");
        if (existing != null)
        {
            skillDescriptionPanel = existing.gameObject;
            skillDescriptionTitleText = existing.Find("Title")?.GetComponent<TextMeshProUGUI>();
            skillDescriptionBodyText = existing.Find("Body")?.GetComponent<TextMeshProUGUI>();
            if (skillDescriptionBodyText == null)
            {
                skillDescriptionBodyText = existing.Find("BodyViewport/Body")?.GetComponent<TextMeshProUGUI>();
            }
            EnsureSkillDescriptionScrollSetup();
            skillDescriptionPanel.SetActive(false);
            return;
        }

        GameObject panel = new GameObject("SkillDescriptionPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(panelParent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        ApplySkillDescriptionPanelLayout(rect);

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
            ApplyAttributePanelLayout(attributePanel.transform as RectTransform);
            return;
        }

        Transform existing = FindChildRecursive(panelParent, "AttributePanel");
        if (existing != null)
        {
            attributePanel = existing.gameObject;
            attributePanelTitleText = existing.Find("Title")?.GetComponent<TextMeshProUGUI>();
            attributeFooterText = existing.Find("Footer")?.GetComponent<TextMeshProUGUI>();
            CacheAttributeRows(existing);
            ApplyAttributePanelLayout(existing as RectTransform);
            ApplyAttributePanelTextLayout();
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
        attributePanelTitleText.text = "Attributes";

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

    private void EnsureSkillRowIcon(string key)
    {
        if (mainPanel == null || string.IsNullOrWhiteSpace(key) || skillRowIcons.ContainsKey(key))
        {
            return;
        }

        Transform row = FindChildRecursive(mainPanel.transform, $"{key.ToUpperInvariant()}Row");
        if (row == null)
        {
            return;
        }

        TextMeshProUGUI keyLabel = row.GetComponentInChildren<TextMeshProUGUI>(true);
        if (keyLabel == null)
        {
            return;
        }

        GameObject iconObject = new GameObject($"{key.ToUpperInvariant()}SkillIcon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(row, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = skillRowIconSize;
        iconRect.anchoredPosition = new Vector2(skillRowIconX, 0f);

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.preserveAspect = true;

        GameObject highlightObject = new GameObject("HoverHighlight", typeof(RectTransform), typeof(Image));
        highlightObject.transform.SetParent(iconObject.transform, false);
        RectTransform highlightRect = highlightObject.GetComponent<RectTransform>();
        highlightRect.anchorMin = Vector2.zero;
        highlightRect.anchorMax = Vector2.one;
        highlightRect.offsetMin = Vector2.zero;
        highlightRect.offsetMax = Vector2.zero;
        highlightRect.localScale = Vector3.one * 1.2f;
        Image highlightImage = highlightObject.GetComponent<Image>();
        highlightImage.sprite = iconImage.sprite;
        highlightImage.color = skillHoverHighlightColor;
        highlightImage.raycastTarget = false;
        highlightImage.enabled = false;
        highlightObject.SetActive(false);

        SkillHoverTrigger trigger = iconObject.AddComponent<SkillHoverTrigger>();
        trigger.skillKey = key.ToUpperInvariant();
        trigger.entered = HandleRuneSkillHoverEnter;
        trigger.exited = HandleRuneSkillHoverExit;

        skillRowIcons[key.ToUpperInvariant()] = iconImage;
        skillRowHighlights[key.ToUpperInvariant()] = highlightImage;
        skillRowHoverTriggers[key.ToUpperInvariant()] = trigger;
    }

    private void ApplySkillDescriptionPanelLayout(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        RectTransform parentRect = rect.parent as RectTransform;
        float width = skillDescriptionPanelSize.x;
        if (width <= 0f && parentRect != null)
        {
            width = Mathf.Max(0f, parentRect.rect.width - (skillDescriptionPanelOffset.x * 2f));
        }

        if (parentRect != null)
        {
            float maxWidth = Mathf.Max(0f, parentRect.rect.width - skillDescriptionPanelOffset.x);
            width = Mathf.Min(width, maxWidth);
        }

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(width, skillDescriptionPanelSize.y);
        rect.anchoredPosition = new Vector2(
            skillDescriptionPanelOffset.x,
            skillDescriptionPanelOffset.y);
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
        if (skillDescriptionBodyText.transform.parent != viewportTransform)
        {
            skillDescriptionBodyText.transform.SetParent(viewportTransform, false);
        }

        sharedDescriptionText = skillDescriptionBodyText;
        if (sharedDescriptionScrollRect != null)
        {
            sharedDescriptionScrollRect.viewport = skillDescriptionBodyViewportRect;
            sharedDescriptionScrollRect.content = skillDescriptionBodyText.rectTransform;
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

        ApplySkillDescriptionPanelLayout(panelRect);

        float panelWidth = panelRect.sizeDelta.x;
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
        float finalHeight = Mathf.Max(skillDescriptionPanelSize.y, calculatedTextHeight + (skillDescriptionPanelPadding.y * 2f) + 18f);
        finalHeight = Mathf.Min(finalHeight, Mathf.Max(skillDescriptionPanelSize.y, skillDescriptionPanelMaxHeight));
        panelRect.sizeDelta = new Vector2(panelWidth, finalHeight);

        ApplySkillDescriptionTextLayout();
    }

    private void ApplySkillDescriptionTextLayout()
    {
        if (skillDescriptionTitleText != null)
        {
            RectTransform titleRect = skillDescriptionTitleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(skillDescriptionPanelPadding.x, -42f);
            titleRect.offsetMax = new Vector2(-skillDescriptionPanelPadding.x, -10f);
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
            bodyRect.offsetMin = new Vector2(skillDescriptionPanelPadding.x, 0f);
            bodyRect.offsetMax = new Vector2(-skillDescriptionPanelPadding.x, 0f);
            bodyRect.sizeDelta = new Vector2(0f, Mathf.Max(preferredHeight, 10f));
            bodyRect.anchoredPosition = new Vector2(0f, -skillDescriptionPanelPadding.y);
        }

        if (skillDescriptionBodyViewportRect != null)
        {
            skillDescriptionBodyViewportRect.offsetMin = new Vector2(0f, skillDescriptionPanelPadding.y);
            skillDescriptionBodyViewportRect.offsetMax = new Vector2(0f, -46f);
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

        if (skillRowIcons.TryGetValue(upperKey, out Image icon) && icon != null)
        {
            RectTransform iconRect = icon.rectTransform;
            iconRect.sizeDelta = skillRowIconSize;
            iconRect.anchoredPosition = new Vector2(skillRowIconX, 0f);
        }

        if (skillRowHighlights.TryGetValue(upperKey, out Image highlight) && highlight != null)
        {
            RectTransform highlightRect = highlight.rectTransform;
            highlightRect.anchorMin = Vector2.zero;
            highlightRect.anchorMax = Vector2.one;
            highlightRect.offsetMin = Vector2.zero;
            highlightRect.offsetMax = Vector2.zero;
            highlightRect.localScale = Vector3.one * 1.18f;
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

        ApplyAttributePanelLayout(attributePanel.transform as RectTransform);
        ApplyAttributePanelTextLayout();

        CombatStats stats = currentPlayer != null ? BattleStatUtility.GetCombatStats(currentPlayer) : null;
        if (attributePanelTitleText != null)
        {
            attributePanelTitleText.text = "Attributes";
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

        ShowSkillDescriptionByKey(key, trigger.playerIndex);
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
