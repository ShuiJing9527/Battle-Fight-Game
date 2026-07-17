using UnityEngine;

/// <summary>
/// Legacy / deprecated rune panel compatibility script.
/// The current production rune UI flow is RuneUIController + RuneBagUI, opened via K.
/// Keep this script only for backwards compatibility; do not use it as a primary UI entry.
/// </summary>
public class RuneSkillPanel : MonoBehaviour
{
    public GameObject panelRoot;
    public RuneInventory inventory;
    public CombatSkillCaster skillCaster;
    public KeyCode[] toggleKeys = { KeyCode.U, KeyCode.I, KeyCode.O };
    public bool visible;

    [Header("UI Scale")]
    [SerializeField, Min(1f)] private float fallbackWindowScale = 1.2f;

    private Player2Bootstrap cachedBootstrap;
    private Vector3 panelBaseScale = Vector3.one;
    private bool panelBaseScaleCaptured;
    private bool pauseApplied;
    private int selectedRuneIndex = -1;

    private void Awake()
    {
        CacheBootstrap();
        CapturePanelBaseScale();
        ResolveReferences();
        SetPanelVisible(visible, false);
    }

    private void OnDisable()
    {
        SetPauseState(false);
    }

    private void OnDestroy()
    {
        SetPauseState(false);
    }

    public void TogglePanel()
    {
        SetPanelVisible(!visible);
    }

    public void SetPanelVisible(bool visible)
    {
        SetPanelVisible(visible, true);
    }

    private void SetPanelVisible(bool visible, bool pauseGame)
    {
        this.visible = visible;
        ResolveReferences();

        if (panelRoot != null)
        {
            EnsureAncestorChainActive(panelRoot);
            panelRoot.SetActive(visible);
            if (visible)
            {
                CapturePanelBaseScale();
                panelRoot.transform.localScale = Vector3.one;
                panelRoot.transform.localScale = panelBaseScale * fallbackWindowScale;
                panelRoot.transform.SetAsLastSibling();
            }
        }

        if (pauseGame)
        {
            SetPauseState(visible);
        }
    }

    public bool EquipRuneByIndex(int inventoryIndex, int skillIndex, int slotIndex)
    {
        ResolveReferences();

        if (inventory == null || skillCaster == null)
        {
            return false;
        }

        RuneDefinition rune = inventory.GetRune(inventoryIndex);
        BattleSkill skill = skillCaster.GetSkill(skillIndex);
        if (rune == null || skill == null || skill.equippedRunes == null)
        {
            return false;
        }

        if (slotIndex < 0 || slotIndex >= skill.equippedRunes.Length)
        {
            return false;
        }

        skill.equippedRunes[slotIndex] = rune;
        skillCaster.RefreshRuneState();
        inventory.RemoveRune(rune);
        selectedRuneIndex = -1;
        return true;
    }

    private void OnGUI()
    {
        if (!visible || panelRoot == null)
        {
            return;
        }

        Rect windowRect = new Rect(24f, 90f, 540f, 460f);
        GUI.Window(GetInstanceID(), windowRect, DrawFallbackWindow, LocalizeOrFallback("Rune Skill Panel", "符文技能面板"));
    }

    private void DrawFallbackWindow(int windowId)
    {
        GUILayout.Label(LocalizeOrFallback("Rune Bag", "符文背包"));
        if (inventory == null || inventory.Count == 0)
        {
            GUILayout.Label(LocalizeOrFallback("No rune", "无符文"));
        }
        else
        {
            for (int i = 0; i < inventory.Count; i++)
            {
                RuneDefinition rune = inventory.GetRune(i);
                string label = rune != null ? GetRuneDisplayName(rune) : LocalizeOrFallback("Empty", "空");
                if (GUILayout.Button(selectedRuneIndex == i ? $"> {label}" : label, GUILayout.Height(34f)))
                {
                    selectedRuneIndex = i;
                }
            }
        }

        GUILayout.Space(10f);
        GUILayout.Label(LocalizeOrFallback("rune.equip_prompt", "将选中的符文镶嵌到技能槽"));
        string[] skillLabels = { "Q", "W", "E", "R" };
        for (int skillIndex = 0; skillIndex < skillLabels.Length; skillIndex++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(skillLabels[skillIndex], GUILayout.Width(30f));
            for (int slotIndex = 0; slotIndex < 5; slotIndex++)
            {
                if (GUILayout.Button(slotIndex.ToString(), GUILayout.Width(56f), GUILayout.Height(32f)))
                {
                    EquipRuneByIndex(selectedRuneIndex, skillIndex, slotIndex);
                }
            }
            GUILayout.EndHorizontal();
        }

        GUI.DragWindow();
    }

    private static string GetRuneDisplayName(RuneDefinition rune)
    {
        if (rune == null)
        {
            return LocalizeOrFallback("Empty", "空");
        }

        if (rune.runeType != RuneType.None)
        {
            return RuneDefinition.GetLocalizedName(rune.runeType);
        }

        return !string.IsNullOrWhiteSpace(rune.runeName) ? LocalizeOrFallback(rune.runeName, rune.runeName) : "符文";
    }

    private static string LocalizeOrFallback(string key, string fallback)
    {
        return GameLocalization.Instance != null
            ? GameLocalization.Instance.TranslateOrFallback(key, fallback)
            : fallback;
    }

    private void CacheBootstrap()
    {
        if (cachedBootstrap == null)
        {
            cachedBootstrap = FindObjectOfType<Player2Bootstrap>(true);
        }
    }

    private void ResolveReferences()
    {
        CacheBootstrap();

        if (inventory == null)
        {
            inventory = ResolveInventory();
        }

        if (skillCaster == null)
        {
            skillCaster = ResolveSkillCaster();
        }
    }

    private RuneInventory ResolveInventory()
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

    private CombatSkillCaster ResolveSkillCaster()
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

    private void SetPauseState(bool shouldPause)
    {
        if (shouldPause)
        {
            if (pauseApplied)
            {
                return;
            }

            Time.timeScale = 0f;
            pauseApplied = true;
            return;
        }

        if (!pauseApplied && Time.timeScale != 0f)
        {
            return;
        }

        Time.timeScale = 1f;
        pauseApplied = false;
    }
}
