using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

public class RuneBagUI : MonoBehaviour
{
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

    [Header("符文背包数据，拖他们写的 RuneInventory")]
    public RuneInventory runeInventory;

    [Header("面板根物体")]
    public GameObject panelRoot;

    [Header("符文列表 Content")]
    public Transform runeContent;

    [Header("符文按钮预制体")]
    public GameObject runeButtonPrefab;

    [Header("技能槽数据")]
    public SkillSlot[] skillSlots;

    [Header("技能槽 UI")]
    public SkillSlotUI[] skillSlotUIs;

    [Header("当前选择显示")]
    public TextMeshProUGUI selectedRuneText;

    [Header("测试用，可不填")]
    public RuneDefinition[] testRunes;

    private RuneDefinition selectedRune;

    private void Start()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        BindSkillSlotButtons();

        RefreshAll();
        ClearSelectedRune();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            TogglePanel();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            AddTestRune();
        }
    }

    public void OpenPanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        RefreshAll();
        ClearSelectedRune();
    }

    public void ClosePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void TogglePanel()
    {
        if (panelRoot == null) return;

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
        RefreshRuneList();
        RefreshSkillSlots();
    }

    private void RefreshRuneList()
    {
        if (runeInventory == null)
        {
            Debug.LogWarning("没有绑定 RuneInventory");
            return;
        }

        if (runeContent == null)
        {
            Debug.LogWarning("没有绑定 Rune Content");
            return;
        }

        if (runeButtonPrefab == null)
        {
            Debug.LogWarning("没有绑定 Rune Button Prefab");
            return;
        }

        foreach (Transform child in runeContent)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < runeInventory.Count; i++)
        {
            RuneDefinition rune = runeInventory.GetRune(i);

            if (rune == null) continue;

            GameObject obj = Instantiate(runeButtonPrefab, runeContent);

            Button button = obj.GetComponent<Button>();
            TextMeshProUGUI text = obj.GetComponentInChildren<TextMeshProUGUI>();

            if (text != null)
            {
                text.text = GetRuneName(rune);
            }

            if (button != null)
            {
                RuneDefinition tempRune = rune;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    SelectRune(tempRune);
                });
            }
        }
    }

    private void BindSkillSlotButtons()
    {
        if (skillSlotUIs == null) return;

        for (int i = 0; i < skillSlotUIs.Length; i++)
        {
            int index = i;

            if (skillSlotUIs[i] != null && skillSlotUIs[i].button != null)
            {
                skillSlotUIs[i].button.onClick.RemoveAllListeners();
                skillSlotUIs[i].button.onClick.AddListener(() =>
                {
                    EquipSelectedRuneToSkill(index);
                });
            }
        }
    }

    private void RefreshSkillSlots()
    {
        if (skillSlots == null || skillSlotUIs == null) return;

        int count = Mathf.Min(skillSlots.Length, skillSlotUIs.Length);

        for (int i = 0; i < count; i++)
        {
            SkillSlot slot = skillSlots[i];
            SkillSlotUI slotUI = skillSlotUIs[i];

            if (slotUI == null) continue;

            if (slotUI.skillNameText != null)
            {
                slotUI.skillNameText.text = slot.skillName;
            }

            if (slotUI.equippedRuneText != null)
            {
                slotUI.equippedRuneText.text =
                    slot.equippedRune == null ? "未装备" : GetRuneName(slot.equippedRune);
            }
        }
    }

    private void SelectRune(RuneDefinition rune)
    {
        if (rune == null) return;

        selectedRune = rune;

        if (selectedRuneText != null)
        {
            selectedRuneText.text = "已选择：" + GetRuneName(rune);
        }

        Debug.Log("选择符文：" + GetRuneName(rune));
    }

    private void EquipSelectedRuneToSkill(int skillIndex)
    {
        if (selectedRune == null)
        {
            Debug.Log("还没有选择符文");
            return;
        }

        if (skillSlots == null)
        {
            Debug.LogWarning("没有设置技能槽");
            return;
        }

        if (skillIndex < 0 || skillIndex >= skillSlots.Length)
        {
            Debug.LogWarning("技能槽编号错误：" + skillIndex);
            return;
        }

        skillSlots[skillIndex].equippedRune = selectedRune;

        Debug.Log("技能【" + skillSlots[skillIndex].skillName + "】装备符文：" + GetRuneName(selectedRune));

        RefreshSkillSlots();
    }

    private void ClearSelectedRune()
    {
        selectedRune = null;

        if (selectedRuneText != null)
        {
            selectedRuneText.text = "请选择一个符文";
        }
    }

    public void AddTestRune()
    {
        if (runeInventory == null)
        {
            Debug.LogWarning("没有绑定 RuneInventory");
            return;
        }

        if (testRunes == null || testRunes.Length == 0)
        {
            Debug.LogWarning("没有放测试符文");
            return;
        }

        RuneDefinition rune = testRunes[Random.Range(0, testRunes.Length)];

        if (rune == null) return;

        runeInventory.AddRune(rune);

        Debug.Log("测试获得符文：" + GetRuneName(rune));

        RefreshRuneList();
    }

    public RuneDefinition GetEquippedRune(int skillIndex)
    {
        if (skillSlots == null) return null;

        if (skillIndex < 0 || skillIndex >= skillSlots.Length)
        {
            return null;
        }

        return skillSlots[skillIndex].equippedRune;
    }

    private string GetRuneName(RuneDefinition rune)
    {
        if (rune == null) return "空符文";

        System.Type type = rune.GetType();

        string[] possibleNames =
        {
            "runeName",
            "displayName",
            "Name",
            "name",
            "id",
            "runeId"
        };

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

        return "未命名符文";
    }
}