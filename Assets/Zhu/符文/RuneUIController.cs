using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RuneUIController : MonoBehaviour
{
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
        RefreshRuneList();
        RefreshSkillSlots();
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
        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
        }

        SetPauseState(true);
        SetOldHudVisible(false);
        ResolveCurrentPlayerContext();
        RefreshRuneList();
        RefreshSkillSlots();
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
        if (runeListContent == null)
        {
            if (!warnedMissingRuneList)
            {
                warnedMissingRuneList = true;
                Debug.LogWarning(LogMissingRuneList, this);
            }
            return;
        }

        List<RuneDefinition> visibleRunes = BuildVisibleRuneList();
        int runeCount = visibleRunes.Count;
        int childCount = runeListContent.childCount;
        bool hasRuneEntries = runeCount > 0;
        Dictionary<string, int> visibleRuneIndices = new Dictionary<string, int>();

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

            RuneDefinition rune = i < runeCount ? visibleRunes[i] : null;
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
                    string runeKey = GetRuneStackKey(rune);
                    int visibleIndex = 0;
                    visibleRuneIndices.TryGetValue(runeKey, out visibleIndex);
                    visibleIndex++;
                    visibleRuneIndices[runeKey] = visibleIndex;
                    label.text = $"{GetRuneName(rune)} x{visibleIndex}";
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
                }
            }
        }
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
            skill.equippedRunes[slotIndex] = null;
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

        if (rune.id != 0)
        {
            return $"id:{rune.id}";
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

        if (a.id != 0 && a.id == b.id)
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
                runeTypeText.text = LabelTypePlaceholder;
            }

            if (runeDescriptionText != null)
            {
                runeDescriptionText.text = LabelDescriptionPlaceholder;
            }

            if (runeEffectText != null)
            {
                runeEffectText.text = LabelEffectPlaceholder;
            }

            return;
        }

        if (runeNameText != null)
        {
            runeNameText.text = $"Rune Name: {GetRuneName(rune)}";
        }

        if (runeTypeText != null)
        {
            string rarityText = rune.rarity.ToString();
            string mechanicText = rune.mechanic.ToString();
            string categoryText = string.IsNullOrEmpty(rune.category) ? "-" : rune.category;
            runeTypeText.text = $"Type: {categoryText} / {rarityText} / {mechanicText}";
        }

        if (runeDescriptionText != null)
        {
            string triggerText = string.IsNullOrEmpty(rune.triggerCondition) ? "-" : rune.triggerCondition;
            string styleText = string.IsNullOrEmpty(rune.playStyle) ? "-" : rune.playStyle;
            runeDescriptionText.text = $"Description: {triggerText}\nPlay: {styleText}";
        }

        if (runeEffectText != null)
        {
            string limitText = string.IsNullOrEmpty(rune.limitOrSideEffect) ? "-" : rune.limitOrSideEffect;
            string effectText = $"ID: {rune.id}\nLimit: {limitText}";
            if (rune.extraHitCount != 0 || rune.extraCastCount != 1 || Mathf.Abs(rune.damageMultiplier - 1f) > 0.001f || Mathf.Abs(rune.cooldownMultiplier - 1f) > 0.001f)
            {
                effectText += $"\nHit+{rune.extraHitCount} Cast+{rune.extraCastCount} Dmgx{rune.damageMultiplier:0.##} CDx{rune.cooldownMultiplier:0.##}";
            }
            if (rune.healAmount > 0f || rune.healthCost > 0f || rune.range > 0f)
            {
                effectText += $"\nHeal:{rune.healAmount:0.##} Cost:{rune.healthCost:0.##} Range:{rune.range:0.##}";
            }

            runeEffectText.text = effectText;
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

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pauseApplied = true;
            return;
        }

        if (!pauseApplied)
        {
            return;
        }

        Time.timeScale = previousTimeScale;
        pauseApplied = false;
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
        CacheBootstrap();
        if (cachedBootstrap == null)
        {
            return;
        }

        if (!hasCachedBootstrapState)
        {
            cachedBootstrapEnabled = cachedBootstrap.enabled;
            hasCachedBootstrapState = true;
        }

        cachedBootstrap.enabled = visible ? cachedBootstrapEnabled : false;
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

    private void WarnMissingSlotRefsOnce()
    {
        if (warnedMissingSlotRefs)
        {
            return;
        }

        warnedMissingSlotRefs = true;
        Debug.LogWarning(LogMissingSlotRefs, this);
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
