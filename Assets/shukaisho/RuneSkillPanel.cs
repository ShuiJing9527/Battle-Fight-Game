using UnityEngine;

public class RuneSkillPanel : MonoBehaviour
{
    public GameObject panelRoot;
    public RuneInventory inventory;
    public CombatSkillCaster skillCaster;
    public KeyCode[] toggleKeys = { KeyCode.U, KeyCode.I, KeyCode.O };
    public bool visible;

    private int selectedRuneIndex = -1;

    private void Awake()
    {
        if (inventory == null)
        {
            inventory = GetComponent<RuneInventory>();
        }

        if (skillCaster == null)
        {
            skillCaster = GetComponent<CombatSkillCaster>();
        }

        SetPanelVisible(visible);
    }

    private void Update()
    {
        for (int i = 0; i < toggleKeys.Length; i++)
        {
            if (Input.GetKeyDown(toggleKeys[i]))
            {
                TogglePanel();
                break;
            }
        }
    }

    public void TogglePanel()
    {
        SetPanelVisible(!visible);
    }

    public void SetPanelVisible(bool visible)
    {
        this.visible = visible;
        if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
        }
    }

    public bool EquipRuneByIndex(int inventoryIndex, int skillIndex, int slotIndex)
    {
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
        inventory.RemoveRune(rune);
        selectedRuneIndex = -1;
        return true;
    }

    private void OnGUI()
    {
        if (!visible || panelRoot != null)
        {
            return;
        }

        Rect windowRect = new Rect(24f, 90f, 420f, 360f);
        GUI.Window(GetInstanceID(), windowRect, DrawFallbackWindow, "Rune Skill Panel");
    }

    private void DrawFallbackWindow(int windowId)
    {
        GUILayout.Label("Inventory Runes");
        if (inventory == null || inventory.Count == 0)
        {
            GUILayout.Label("No rune");
        }
        else
        {
            for (int i = 0; i < inventory.Count; i++)
            {
                RuneDefinition rune = inventory.GetRune(i);
                string label = rune != null ? rune.runeName : "Empty";
                if (GUILayout.Button(selectedRuneIndex == i ? $"> {label}" : label))
                {
                    selectedRuneIndex = i;
                }
            }
        }

        GUILayout.Space(8f);
        GUILayout.Label("Equip selected rune to skill slot");
        string[] skillLabels = { "Q", "W", "E", "R" };
        for (int skillIndex = 0; skillIndex < skillLabels.Length; skillIndex++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(skillLabels[skillIndex], GUILayout.Width(24f));
            for (int slotIndex = 0; slotIndex < 5; slotIndex++)
            {
                if (GUILayout.Button(slotIndex.ToString(), GUILayout.Width(44f)))
                {
                    EquipRuneByIndex(selectedRuneIndex, skillIndex, slotIndex);
                }
            }
            GUILayout.EndHorizontal();
        }

        GUI.DragWindow();
    }
}
