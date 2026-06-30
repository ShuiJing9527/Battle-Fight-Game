using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RuneBagUI : MonoBehaviour
{
    private struct RuneStackEntry
    {
        public RuneDefinition rune;
        public int count;
    }

    [System.Serializable]
    public class SkillSlot
    {
        public string skillName;
        public RuneDefinition equippedRune;
    }

    [System.Serializable]
    public class SkillSlotUI
    {
        public Button button;
        public TextMeshProUGUI skillNameText;
        public TextMeshProUGUI equippedRuneText;
    }

    [Header("Rune Data")]
    public RuneInventory runeInventory;

    [Header("UI Root")]
    public GameObject panelRoot;

    [Header("Rune List Content")]
    public Transform runeContent;

    [Header("Rune Button Prefab")]
    public GameObject runeButtonPrefab;

    [Header("Skill Slots")]
    public SkillSlot[] skillSlots;

    [Header("Skill Slot UI")]
    public SkillSlotUI[] skillSlotUIs;

    [Header("Selected Rune Text")]
    public TextMeshProUGUI selectedRuneText;

    [Header("Debug Runes")]
    public RuneDefinition[] testRunes;

    [Header("UI Scale")]
    [SerializeField, Min(1f)] private float panelScaleMultiplier = 1.25f;
    [SerializeField] private Vector2 runeButtonMinSize = new Vector2(72f, 72f);
    [SerializeField] private Vector2 skillSlotButtonSize = new Vector2(86f, 86f);
    [SerializeField, Min(1f)] private float runeTextFontSize = 20f;
    [SerializeField, Min(1f)] private float slotTextFontSize = 20f;
    [SerializeField, Min(1f)] private float selectedRuneFontSize = 22f;

    private RuneDefinition selectedRune;
    private Player2Bootstrap cachedBootstrap;
    private CombatSkillCaster skillCaster;
    private Vector3 panelBaseScale = Vector3.one;
    private bool panelBaseScaleCaptured;
    private bool pauseApplied;
    private CanvasGroup panelCanvasGroup;
    private Canvas panelCanvas;
    private RectTransform panelRectTransform;

    public bool IsPanelOpen => panelRoot != null ? panelRoot.activeSelf : gameObject.activeSelf;

    private void Start()
    {
        AutoBindSceneReferences();
        CachePanelVisuals();
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        CacheBootstrap();
        CapturePanelBaseScale();
        ResolveRuntimeReferences();
        ApplyPanelScale();
        BindSkillSlotButtons();
        RefreshAll();
        ClearSelectedRune();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            AddTestRune();
        }
    }

    private void OnDisable()
    {
        SetPauseState(false);
    }

    private void OnDestroy()
    {
        SetPauseState(false);
    }

    public void OpenPanel()
    {
        ResolveRuntimeReferences();
        CloseCharacterPanelForExclusiveDisplay();
        EnsurePanelVisible(true);
        SetPauseState(true);

        RefreshAll();
        ClearSelectedRune();
    }

    public void ClosePanel()
    {
        EnsurePanelVisible(false);
        SetPauseState(false);
    }

    public void TogglePanel()
    {
        if (panelRoot == null)
        {
            OpenPanel();
            return;
        }

        if (panelRoot.activeSelf)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    public void RefreshAll()
    {
        AutoBindSceneReferences();
        ResolveRuntimeReferences();
        RefreshRuneList();
        RefreshSkillSlots();
    }

    private void RefreshRuneList()
    {
        ResolveRuntimeReferences();

        if (runeInventory == null)
        {
            Debug.LogWarning("[RuneBagUI] Missing RuneInventory.");
            return;
        }

        if (runeContent == null)
        {
            Debug.LogWarning("[RuneBagUI] Missing runeContent.");
            return;
        }

        if (runeButtonPrefab == null)
        {
            Debug.LogWarning("[RuneBagUI] Missing runeButtonPrefab.");
            return;
        }

        for (int i = runeContent.childCount - 1; i >= 0; i--)
        {
            Destroy(runeContent.GetChild(i).gameObject);
        }

        System.Collections.Generic.List<RuneStackEntry> runeStacks = BuildRuneStacks();
        for (int i = 0; i < runeStacks.Count; i++)
        {
            RuneStackEntry stackEntry = runeStacks[i];
            RuneDefinition rune = stackEntry.rune;
            if (rune == null)
            {
                continue;
            }

            GameObject obj = Instantiate(runeButtonPrefab, runeContent);
            Button button = obj.GetComponent<Button>();
            TextMeshProUGUI text = obj.GetComponentInChildren<TextMeshProUGUI>(true);
            ApplyRuneButtonSizing(obj, text);
            RectTransform buttonRect = obj.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                int columns = 4;
                float spacingX = runeButtonMinSize.x + 10f;
                float spacingY = runeButtonMinSize.y + 10f;
                buttonRect.anchorMin = new Vector2(0f, 1f);
                buttonRect.anchorMax = new Vector2(0f, 1f);
                buttonRect.pivot = new Vector2(0f, 1f);
                buttonRect.anchoredPosition = new Vector2((i % columns) * spacingX, -(i / columns) * spacingY);
                buttonRect.localScale = Vector3.one;
            }

            if (text != null)
            {
                text.text = $"{GetRuneName(rune)} x{Mathf.Max(1, stackEntry.count)}";
            }

            if (button != null)
            {
                RuneDefinition tempRune = rune;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectRune(tempRune));
            }
        }
    }

    private void BindSkillSlotButtons()
    {
        if (skillSlotUIs == null)
        {
            return;
        }

        for (int i = 0; i < skillSlotUIs.Length; i++)
        {
            int index = i;
            if (skillSlotUIs[i] != null && skillSlotUIs[i].button != null)
            {
                skillSlotUIs[i].button.onClick.RemoveAllListeners();
                skillSlotUIs[i].button.onClick.AddListener(() => EquipSelectedRuneToSkill(index));
            }
        }
    }

    private void RefreshSkillSlots()
    {
        ResolveRuntimeReferences();

        if (skillSlots == null || skillSlotUIs == null)
        {
            return;
        }

        int count = Mathf.Min(skillSlots.Length, skillSlotUIs.Length);
        for (int i = 0; i < count; i++)
        {
            SkillSlot slot = skillSlots[i];
            SkillSlotUI slotUI = skillSlotUIs[i];

            if (slotUI == null)
            {
                continue;
            }

            if (slotUI.skillNameText != null)
            {
                slotUI.skillNameText.text = slot.skillName;
                slotUI.skillNameText.fontSize = Mathf.Max(slotUI.skillNameText.fontSize, slotTextFontSize);
            }

            if (slotUI.equippedRuneText != null)
            {
                slotUI.equippedRuneText.text = slot.equippedRune == null ? "Empty" : GetRuneName(slot.equippedRune);
                slotUI.equippedRuneText.fontSize = Mathf.Max(slotUI.equippedRuneText.fontSize, slotTextFontSize);
            }

            if (slotUI.button != null)
            {
                ApplySkillSlotButtonSizing(slotUI.button.gameObject);
            }
        }
    }

    private void SelectRune(RuneDefinition rune)
    {
        if (rune == null)
        {
            return;
        }

        selectedRune = rune;
        if (selectedRuneText != null)
        {
            selectedRuneText.text = "Selected: " + GetRuneName(rune);
            selectedRuneText.fontSize = Mathf.Max(selectedRuneText.fontSize, selectedRuneFontSize);
        }

        Debug.Log("[RuneBagUI] Selected rune: " + GetRuneName(rune));
    }

    private void EquipSelectedRuneToSkill(int skillIndex)
    {
        if (selectedRune == null)
        {
            Debug.Log("[RuneBagUI] No rune selected.");
            return;
        }

        ResolveRuntimeReferences();

        if (skillSlots == null)
        {
            Debug.LogWarning("[RuneBagUI] Missing skillSlots.");
            return;
        }

        if (skillIndex < 0 || skillIndex >= skillSlots.Length)
        {
            Debug.LogWarning("[RuneBagUI] Invalid skill index: " + skillIndex);
            return;
        }

        if (skillCaster == null)
        {
            Debug.LogWarning("[RuneBagUI] Missing CombatSkillCaster.");
            return;
        }

        BattleSkill skill = skillCaster.GetSkill(skillIndex);
        if (skill == null)
        {
            Debug.LogWarning("[RuneBagUI] Missing BattleSkill for slot: " + skillIndex);
            return;
        }

        if (skill.equippedRunes == null)
        {
            Debug.LogWarning("[RuneBagUI] Skill rune slots are missing.");
            return;
        }

        SkillSlot slot = skillSlots[skillIndex];
        if (slot == null)
        {
            return;
        }

        int runeSlotIndex = Mathf.Clamp(skillIndex, 0, skill.equippedRunes.Length - 1);
        skill.equippedRunes[runeSlotIndex] = selectedRune;
        slot.equippedRune = selectedRune;
        skillCaster.RefreshRuneState();

        Debug.Log($"[RuneBagUI] Equipped {GetRuneName(selectedRune)} to {slot.skillName} slot {runeSlotIndex}");
        RefreshSkillSlots();
    }

    private void ClearSelectedRune()
    {
        selectedRune = null;
        if (selectedRuneText != null)
        {
            selectedRuneText.text = "Select a rune";
            selectedRuneText.fontSize = Mathf.Max(selectedRuneText.fontSize, selectedRuneFontSize);
        }
    }

    public void AddTestRune()
    {
        ResolveRuntimeReferences();

        if (runeInventory == null)
        {
            Debug.LogWarning("[RuneBagUI] Missing RuneInventory.");
            return;
        }

        if (testRunes == null || testRunes.Length == 0)
        {
            Debug.LogWarning("[RuneBagUI] Missing test runes.");
            return;
        }

        RuneDefinition rune = testRunes[Random.Range(0, testRunes.Length)];
        if (rune == null)
        {
            return;
        }

        runeInventory.AddRune(rune);
        Debug.Log("[RuneBagUI] Added test rune: " + GetRuneName(rune));
        RefreshRuneList();
    }

    public RuneDefinition GetEquippedRune(int skillIndex)
    {
        if (skillSlots == null)
        {
            return null;
        }

        if (skillIndex < 0 || skillIndex >= skillSlots.Length)
        {
            return null;
        }

        return skillSlots[skillIndex].equippedRune;
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

        System.Type type = rune.GetType();
        string[] possibleNames = { "displayName", "Name", "name", "id", "runeId" };

        foreach (string fieldName in possibleNames)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(string))
            {
                string value = field.GetValue(rune) as string;
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
        }

        foreach (string propertyName in possibleNames)
        {
            PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(string))
            {
                string value = property.GetValue(rune) as string;
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
        }

        return "Rune";
    }

    private System.Collections.Generic.List<RuneStackEntry> BuildRuneStacks()
    {
        System.Collections.Generic.List<RuneStackEntry> runeStacks = new System.Collections.Generic.List<RuneStackEntry>();
        if (runeInventory == null || runeInventory.Count <= 0)
        {
            return runeStacks;
        }

        System.Collections.Generic.Dictionary<string, int> stackIndices = new System.Collections.Generic.Dictionary<string, int>();
        for (int i = 0; i < runeInventory.Count; i++)
        {
            RuneDefinition rune = runeInventory.GetRune(i);
            if (rune == null)
            {
                continue;
            }

            string runeKey = GetRuneStackKey(rune);
            int stackIndex;
            if (stackIndices.TryGetValue(runeKey, out stackIndex))
            {
                RuneStackEntry entry = runeStacks[stackIndex];
                entry.count++;
                runeStacks[stackIndex] = entry;
                continue;
            }

            stackIndices[runeKey] = runeStacks.Count;
            runeStacks.Add(new RuneStackEntry
            {
                rune = rune,
                count = 1
            });
        }

        for (int i = runeStacks.Count - 1; i >= 0; i--)
        {
            RuneStackEntry entry = runeStacks[i];
            int availableCount = Mathf.Max(0, entry.count - CountEquippedRuneCopies(entry.rune));
            if (availableCount <= 0)
            {
                runeStacks.RemoveAt(i);
                continue;
            }

            entry.count = availableCount;
            runeStacks[i] = entry;
        }

        return runeStacks;
    }

    private int CountEquippedRuneCopies(RuneDefinition rune)
    {
        if (rune == null || skillCaster == null || skillSlots == null)
        {
            return 0;
        }

        int count = 0;
        for (int skillIndex = 0; skillIndex < skillSlots.Length; skillIndex++)
        {
            BattleSkill skill = skillCaster.GetSkill(skillIndex);
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

    private void CacheBootstrap()
    {
        if (cachedBootstrap == null)
        {
            cachedBootstrap = FindObjectOfType<Player2Bootstrap>(true);
        }
    }

    private void CachePanelVisuals()
    {
        GameObject root = GetPanelRootObject();
        if (root == null)
        {
            return;
        }

        if (panelRectTransform == null)
        {
            panelRectTransform = root.GetComponent<RectTransform>();
        }

        if (panelCanvasGroup == null)
        {
            panelCanvasGroup = root.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
            {
                panelCanvasGroup = root.AddComponent<CanvasGroup>();
            }
        }

        if (panelCanvas == null)
        {
            panelCanvas = root.GetComponentInParent<Canvas>(true);
        }
    }

    private GameObject GetPanelRootObject()
    {
        return panelRoot != null ? panelRoot : gameObject;
    }

    private void EnsurePanelVisible(bool visible)
    {
        GameObject root = GetPanelRootObject();
        if (root == null)
        {
            return;
        }

        CachePanelVisuals();

        EnsureAncestorChainActive(root);
        root.SetActive(true);
        root.transform.SetAsLastSibling();

        if (panelRectTransform != null)
        {
            panelRectTransform.anchoredPosition = Vector2.zero;
            panelRectTransform.localScale = Vector3.one;
        }

        if (visible)
        {
            ApplyPanelScale();
        }

        CanvasGroup[] canvasGroups = root.GetComponentsInParent<CanvasGroup>(true);
        if (canvasGroups != null && canvasGroups.Length > 0)
        {
            foreach (CanvasGroup canvasGroup in canvasGroups)
            {
                if (canvasGroup == null)
                {
                    continue;
                }

                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            panelCanvasGroup = canvasGroups[0];
        }
        else if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = visible ? 1f : 0f;
            panelCanvasGroup.interactable = visible;
            panelCanvasGroup.blocksRaycasts = visible;
        }

        if (panelCanvas != null)
        {
            panelCanvas.enabled = true;
            panelCanvas.overrideSorting = true;
            if (panelCanvas.sortingOrder < 100)
            {
                panelCanvas.sortingOrder = 100;
            }
        }

        if (!visible)
        {
            root.SetActive(false);
        }
    }

    private void EnsureAncestorChainActive(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Transform current = root.transform.parent;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }

            if (current.GetComponent<Canvas>() != null)
            {
                break;
            }

            current = current.parent;
        }
    }

    private void ResolveRuntimeReferences()
    {
        AutoBindSceneReferences();
        CacheBootstrap();

        if (runeInventory == null)
        {
            runeInventory = ResolveSharedRuneInventory();
        }

        if (skillCaster == null)
        {
            skillCaster = ResolveSharedSkillCaster();
        }
    }

    private RuneInventory ResolveSharedRuneInventory()
    {
        if (cachedBootstrap != null)
        {
            GameObject leader = cachedBootstrap.PartyLeader;
            if (leader != null)
            {
                RuneInventory leaderInventory = leader.GetComponent<RuneInventory>();
                if (leaderInventory != null)
                {
                    return leaderInventory;
                }
            }

            GameObject current = cachedBootstrap.CurrentPlayer;
            if (current != null)
            {
                RuneInventory currentInventory = current.GetComponent<RuneInventory>();
                if (currentInventory != null)
                {
                    return currentInventory;
                }
            }
        }

        RuneInventory[] inventories = FindObjectsOfType<RuneInventory>(true);
        if (inventories != null && inventories.Length > 0)
        {
            return inventories[0];
        }

        return null;
    }

    private CombatSkillCaster ResolveSharedSkillCaster()
    {
        if (cachedBootstrap != null)
        {
            GameObject current = cachedBootstrap.CurrentPlayer;
            if (current != null)
            {
                CombatSkillCaster currentCaster = current.GetComponent<CombatSkillCaster>();
                if (currentCaster != null)
                {
                    return currentCaster;
                }
            }

            GameObject leader = cachedBootstrap.PartyLeader;
            if (leader != null)
            {
                CombatSkillCaster leaderCaster = leader.GetComponent<CombatSkillCaster>();
                if (leaderCaster != null)
                {
                    return leaderCaster;
                }
            }
        }

        CombatSkillCaster[] casters = FindObjectsOfType<CombatSkillCaster>(true);
        if (casters != null && casters.Length > 0)
        {
            return casters[0];
        }

        return null;
    }

    private void CapturePanelBaseScale()
    {
        if (panelBaseScaleCaptured || panelRoot == null)
        {
            return;
        }

        panelBaseScale = panelRoot.transform.localScale;
        panelBaseScaleCaptured = true;
    }

    private void ApplyPanelScale()
    {
        if (panelRoot == null)
        {
            return;
        }

        CapturePanelBaseScale();
        panelRoot.transform.localScale = panelBaseScale * panelScaleMultiplier;
    }

    private void ApplyRuneButtonSizing(GameObject buttonObject, TextMeshProUGUI label)
    {
        if (buttonObject == null)
        {
            return;
        }

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = runeButtonMinSize;
        }

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = buttonObject.AddComponent<LayoutElement>();
        }

        layoutElement.minWidth = runeButtonMinSize.x;
        layoutElement.minHeight = runeButtonMinSize.y;
        layoutElement.preferredWidth = runeButtonMinSize.x;
        layoutElement.preferredHeight = runeButtonMinSize.y;

        if (label != null)
        {
            label.fontSize = Mathf.Max(label.fontSize, runeTextFontSize);
        }
    }

    private void ApplySkillSlotButtonSizing(GameObject buttonObject)
    {
        if (buttonObject == null)
        {
            return;
        }

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = skillSlotButtonSize;
        }

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = buttonObject.AddComponent<LayoutElement>();
        }

        layoutElement.minWidth = skillSlotButtonSize.x;
        layoutElement.minHeight = skillSlotButtonSize.y;
        layoutElement.preferredWidth = skillSlotButtonSize.x;
        layoutElement.preferredHeight = skillSlotButtonSize.y;
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

    public void RefreshCurrentPlayerView()
    {
        if (!IsPanelOpen)
        {
            return;
        }

        RefreshAll();
    }

    private void CloseCharacterPanelForExclusiveDisplay()
    {
        PlayerAttributePanelUI attributePanel = FindObjectOfType<PlayerAttributePanelUI>(true);
        if (attributePanel != null && attributePanel.IsPanelOpen)
        {
            attributePanel.ClosePanel();
        }
    }

    private void AutoBindSceneReferences()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (runeContent == null)
        {
            Transform content = panelRoot != null ? panelRoot.transform.Find("RuneContent") : null;
            if (content == null)
            {
                content = transform.Find("RuneContent");
            }

            runeContent = content;
        }

        if (selectedRuneText == null)
        {
            TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name.Contains("SelectedRune"))
                {
                    selectedRuneText = texts[i];
                    break;
                }
            }

            if (selectedRuneText == null && texts.Length > 0)
            {
                selectedRuneText = texts[0];
            }
        }

        if (skillSlots == null || skillSlots.Length == 0)
        {
            skillSlots = new SkillSlot[4];
            string[] labels = { "Q", "W", "E", "R" };
            for (int i = 0; i < skillSlots.Length; i++)
            {
                skillSlots[i] = new SkillSlot
                {
                    skillName = labels[i],
                    equippedRune = null
                };
            }
        }

        if (skillSlotUIs == null || skillSlotUIs.Length == 0)
        {
            Transform slotsRoot = panelRoot != null ? panelRoot.transform.Find("SkillSlotsRoot") : null;
            if (slotsRoot == null)
            {
                slotsRoot = transform.Find("SkillSlotsRoot");
            }

            if (slotsRoot != null)
            {
                skillSlotUIs = new SkillSlotUI[Mathf.Min(4, slotsRoot.childCount)];
                int slotCount = 0;
                for (int i = 0; i < slotsRoot.childCount && slotCount < skillSlotUIs.Length; i++)
                {
                    Transform child = slotsRoot.GetChild(i);
                    if (child == null)
                    {
                        continue;
                    }

                    SkillSlotUI ui = new SkillSlotUI
                    {
                        button = child.GetComponent<Button>(),
                        skillNameText = child.Find("SkillNameText") != null ? child.Find("SkillNameText").GetComponent<TextMeshProUGUI>() : null,
                        equippedRuneText = child.Find("EquippedRuneText") != null ? child.Find("EquippedRuneText").GetComponent<TextMeshProUGUI>() : null
                    };
                    skillSlotUIs[slotCount++] = ui;
                }
            }
        }
    }
}
