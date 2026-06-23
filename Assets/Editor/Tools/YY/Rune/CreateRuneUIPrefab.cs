using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public static class CreateRuneUIPrefab
{
    private const string PrefabPath = "Assets/Prefabs/UI/Rune/RuneUIPanel.prefab";
    private const string RuneButtonPrefabPath = "Assets/Zhu/RuneButtonPrefab.prefab";

    [MenuItem("Tools/YY/Rune/Create Rune UI Prefab")]
    public static void CreatePrefab()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/UI");
        EnsureFolder("Assets/Prefabs/UI/Rune");

        AssetDatabase.DeleteAsset(PrefabPath);

        GameObject root = BuildPrefabRoot();
        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = prefabAsset;
        EditorGUIUtility.PingObject(prefabAsset);

        Debug.Log($"[RuneUI] Created prefab at {PrefabPath}");
    }

    private static GameObject BuildPrefabRoot()
    {
        TMP_FontAsset fontAsset = ResolveFontAsset();
        GameObject runeButtonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuneButtonPrefabPath);

        GameObject root = CreateUIObject("RuneUIPanel");
        RectTransform rootRect = root.GetComponent<RectTransform>();
        StretchFullScreen(rootRect);

        GameObject bagPanel = CreatePanel(root.transform, "RuneBagPanel", new Vector2(980f, 620f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        RuneBagUI bagUI = bagPanel.AddComponent<RuneBagUI>();
        bagPanel.AddComponent<CanvasGroup>();

        GameObject bagBackground = bagPanel;
        Image bagImage = bagBackground.GetComponent<Image>();
        bagImage.color = new Color(0.08f, 0.10f, 0.12f, 0.96f);

        TextMeshProUGUI titleText = CreateText(bagPanel.transform, "TitleText", "Rune Panel", fontAsset, 22f, TextAlignmentOptions.Center);
        ConfigureRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(280f, 40f), new Vector2(0f, -18f));

        GameObject closeButton = CreateButton(bagPanel.transform, "CloseButton", fontAsset, "Close", new Vector2(100f, 34f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -18f));
        Button closeButtonComponent = closeButton.GetComponent<Button>();

        GameObject runeContent = CreatePanel(bagPanel.transform, "RuneContent", new Vector2(420f, 470f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f));
        runeContent.AddComponent<RectMask2D>();
        runeContent.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.17f, 0.9f);

        TextMeshProUGUI selectedRuneText = CreateText(bagPanel.transform, "SelectedRuneText", "Selected Rune: None", fontAsset, 20f, TextAlignmentOptions.Left);
        ConfigureRect(selectedRuneText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(420f, 32f), new Vector2(22f, 28f));

        GameObject skillSlotsRoot = CreateUIObject("SkillSlotsRoot", bagPanel.transform);
        RectTransform skillSlotsRootRect = skillSlotsRoot.GetComponent<RectTransform>();
        ConfigureRect(skillSlotsRootRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(470f, 120f), new Vector2(0f, 24f));

        string[] skillLabels = { "Q", "W", "E", "R" };
        Vector2[] slotPositions = {
            new Vector2(-180f, 0f),
            new Vector2(-60f, 0f),
            new Vector2(60f, 0f),
            new Vector2(180f, 0f)
        };

        RuneBagUI.SkillSlot[] slots = new RuneBagUI.SkillSlot[4];
        RuneBagUI.SkillSlotUI[] slotUIs = new RuneBagUI.SkillSlotUI[4];

        for (int i = 0; i < 4; i++)
        {
            GameObject slot = CreateButton(skillSlotsRoot.transform, $"{skillLabels[i]}Slot", fontAsset, string.Empty, new Vector2(100f, 100f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), slotPositions[i]);
            slot.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.20f, 0.92f);

            TextMeshProUGUI slotNameText = CreateText(slot.transform, "SkillNameText", skillLabels[i], fontAsset, 20f, TextAlignmentOptions.Center);
            ConfigureRect(slotNameText.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(84f, 24f), new Vector2(0f, -6f));

            TextMeshProUGUI equippedRuneText = CreateText(slot.transform, "EquippedRuneText", "Empty", fontAsset, 18f, TextAlignmentOptions.Center);
            equippedRuneText.color = new Color(0.85f, 0.92f, 1f, 1f);
            ConfigureRect(equippedRuneText.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(88f, 24f), new Vector2(0f, 10f));

            slots[i] = new RuneBagUI.SkillSlot
            {
                skillName = skillLabels[i],
                equippedRune = null
            };

            slotUIs[i] = new RuneBagUI.SkillSlotUI
            {
                button = slot.GetComponent<Button>(),
                skillNameText = slotNameText,
                equippedRuneText = equippedRuneText
            };
        }

        GameObject skillPanel = CreatePanel(root.transform, "RuneSkillPanel", new Vector2(540f, 460f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        skillPanel.AddComponent<CanvasGroup>();
        skillPanel.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.12f, 0.94f);
        RuneSkillPanel runeSkillPanel = skillPanel.AddComponent<RuneSkillPanel>();

        TextMeshProUGUI skillTitle = CreateText(skillPanel.transform, "TitleText", "Rune Skill Panel", fontAsset, 22f, TextAlignmentOptions.Center);
        ConfigureRect(skillTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(280f, 40f), new Vector2(0f, -18f));

        bagUI.panelRoot = bagPanel;
        bagUI.runeSkillPanel = runeSkillPanel;
        bagUI.runeContent = runeContent.transform;
        bagUI.runeButtonPrefab = runeButtonPrefab;
        bagUI.skillSlots = slots;
        bagUI.skillSlotUIs = slotUIs;
        bagUI.selectedRuneText = selectedRuneText.GetComponent<TextMeshProUGUI>();

        runeSkillPanel.panelRoot = skillPanel;
        runeSkillPanel.inventory = null;
        runeSkillPanel.skillCaster = null;
        runeSkillPanel.visible = false;

        if (closeButtonComponent != null)
        {
            UnityEventTools.AddPersistentListener(closeButtonComponent.onClick, bagUI.ClosePanel);
        }

        SetLayerRecursively(root, 5);
        return root;
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition)
    {
        GameObject panel = CreateUIObject(name, parent);
        Image image = panel.AddComponent<Image>();
        image.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        ConfigureRect(panel.GetComponent<RectTransform>(), anchorMin, anchorMax, size, anchoredPosition);
        return panel;
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 anchorMin, Vector2 anchorMax)
    {
        return CreatePanel(parent, name, size, anchorMin, anchorMax, Vector2.zero);
    }

    private static GameObject CreateButton(Transform parent, string name, TMP_FontAsset fontAsset, string label, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition)
    {
        GameObject buttonObject = CreateUIObject(name, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.21f, 0.25f, 0.95f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ConfigureRect(buttonObject.GetComponent<RectTransform>(), anchorMin, anchorMax, size, anchoredPosition);

        if (!string.IsNullOrEmpty(label))
        {
            TextMeshProUGUI text = CreateText(buttonObject.transform, "ButtonText", label, fontAsset, 18f, TextAlignmentOptions.Center);
            ConfigureRect(text.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        }

        return buttonObject;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, TMP_FontAsset fontAsset, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUIObject(name, parent);
        TextMeshProUGUI tmp = textObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        if (fontAsset != null)
        {
            tmp.font = fontAsset;
        }

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        return tmp;
    }

    private static GameObject CreateUIObject(string name, Transform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }
        return go;
    }

    private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPosition)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void StretchFullScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static TMP_FontAsset ResolveFontAsset()
    {
        TMP_FontAsset fontAsset = TMP_Settings.defaultFontAsset;
        if (fontAsset != null)
        {
            return fontAsset;
        }

        fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (fontAsset != null)
        {
            return fontAsset;
        }

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
    }
}
