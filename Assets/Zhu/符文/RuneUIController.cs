using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RuneUIController : MonoBehaviour
{
    private const int SkillCount = 4;
    private const int SlotsPerSkill = 5;

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
        ResolveCurrentPlayerContext();
        SetPauseState(true);
        SetOldHudVisible(false);

        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
        }

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

        int runeCount = GetRuneSourceCount();
        if (runeListContent == null)
        {
            if (!warnedMissingRuneList)
            {
                warnedMissingRuneList = true;
                Debug.LogWarning("[RuneUI] Missing runeListContent reference.", this);
            }
            return;
        }

        int childCount = runeListContent.childCount;
        bool hasRuneEntries = runeCount > 0;

        if (noRuneText != null)
        {
            noRuneText.gameObject.SetActive(!hasRuneEntries);
            noRuneText.text = hasRuneEntries ? string.Empty : "No rune";
        }

        for (int i = 0; i < childCount; i++)
        {
            Transform child = runeListContent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            RuneDefinition rune = i < runeCount ? GetRuneAtIndex(i) : null;
            Button button = child.GetComponent<Button>();
            TextMeshProUGUI label = child.GetComponent<TextMeshProUGUI>();
            if (label == null)
            {
                label = child.GetComponentInChildren<TextMeshProUGUI>(true);
            }

            if (label != null)
            {
                label.text = rune != null ? GetRuneName(rune) : "Empty";
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
            slotView.label.text = rune != null ? GetRuneName(rune) : "Empty";
        }
    }

    private void EquipSelectedRuneToSlot(int skillIndex, int slotIndex)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (selectedRune == null)
        {
            Debug.LogWarning("[RuneUI] No rune selected.");
            return;
        }

        ResolveCurrentPlayerContext();
        if (currentSkillCaster == null)
        {
            if (!warnedMissingSkillCaster)
            {
                warnedMissingSkillCaster = true;
                Debug.LogWarning("[RuneUI] Missing CombatSkillCaster.", this);
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

        skill.equippedRunes[slotIndex] = selectedRune;
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
            selectedRuneText.text = selectedRune != null ? $"Selected Rune: {GetRuneName(selectedRune)}" : "Selected Rune: None";
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
    }

    private int GetRuneSourceCount()
    {
        if (currentRuneInventory != null)
        {
            return currentRuneInventory.Count;
        }

        if (currentRuneLibrary != null && currentRuneLibrary.runes != null)
        {
            return currentRuneLibrary.runes.Length;
        }

        return 0;
    }

    private RuneDefinition GetRuneAtIndex(int index)
    {
        if (index < 0)
        {
            return null;
        }

        if (currentRuneInventory != null && index < currentRuneInventory.Count)
        {
            return currentRuneInventory.GetRune(index);
        }

        if (currentRuneLibrary != null && currentRuneLibrary.runes != null && index < currentRuneLibrary.runes.Length)
        {
            return currentRuneLibrary.runes[index];
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
            return "Empty";
        }

        if (!string.IsNullOrEmpty(rune.runeName))
        {
            return rune.runeName;
        }

        return "Rune";
    }

    private void RefreshSelectedRuneDetails(RuneDefinition rune)
    {
        if (rune == null)
        {
            if (runeNameText != null)
            {
                runeNameText.text = "Rune Name: None";
            }

            if (runeTypeText != null)
            {
                runeTypeText.text = "Type: -";
            }

            if (runeDescriptionText != null)
            {
                runeDescriptionText.text = "Description: -";
            }

            if (runeEffectText != null)
            {
                runeEffectText.text = "Effect: -";
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
        Debug.LogWarning("[RuneUI] Manual skill slot references are missing. Please assign qSlots / wSlots / eSlots / rSlots in the Inspector.", this);
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
            runeLibrary = player.GetComponentInChildren<RuneLibrary>(true) ?? player.GetComponent<RuneLibrary>();
            runeInventory = player.GetComponentInChildren<RuneInventory>(true) ?? player.GetComponent<RuneInventory>();
            if (skillCaster == null)
            {
                skillCaster = player.GetComponentInChildren<CombatSkillCaster>(true) ?? player.GetComponent<CombatSkillCaster>();
            }
        }

        if (runeLibrary == null)
        {
            RuneLibrary[] libraries = Object.FindObjectsOfType<RuneLibrary>(true);
            if (libraries != null && libraries.Length > 0)
            {
                runeLibrary = libraries[0];
            }
        }

        if (runeInventory == null)
        {
            RuneInventory[] inventories = Object.FindObjectsOfType<RuneInventory>(true);
            if (inventories != null && inventories.Length > 0)
            {
                runeInventory = inventories[0];
            }
        }

        if (skillCaster == null && player != null)
        {
            skillCaster = player.GetComponentInChildren<CombatSkillCaster>(true) ?? player.GetComponent<CombatSkillCaster>();
        }

        return player != null || runeLibrary != null || runeInventory != null || skillCaster != null;
    }
}
