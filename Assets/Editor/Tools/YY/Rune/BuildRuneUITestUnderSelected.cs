using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BuildRuneUITestUnderSelected
{
    private const int SkillCount = 4;
    private const int SlotsPerSkill = 5;

    private static readonly string[] SkillNames = { "Q", "W", "E", "R" };

    [MenuItem("Tools/YY/Rune/Build Test Rune UI Under Selected")]
    public static void BuildUnderSelected()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Rune UI", "Please select a RuneUIPanel first.", "OK");
            return;
        }

        if (selected.name != "RuneUIPanel")
        {
            EditorUtility.DisplayDialog("Rune UI", "Selected object must be RuneUIPanel.", "OK");
            return;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(selected))
        {
            EditorUtility.DisplayDialog("Rune UI", "Please run this on the scene instance of RuneUIPanel, not on the prefab asset.", "OK");
            return;
        }

        Transform existingMainPanel = selected.transform.Find("MainPanel");
        if (existingMainPanel != null)
        {
            EditorUtility.DisplayDialog(
                "Rune UI",
                "MainPanel already exists. Delete it manually if you want to rebuild.",
                "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Rune UI", "Build test rune UI under selected RuneUIPanel?", "OK", "Cancel"))
        {
            return;
        }

        Transform mainPanelTransform = selected.transform.Find("Panel");
        GameObject mainPanel;
        if (mainPanelTransform != null)
        {
            mainPanel = mainPanelTransform.gameObject;
            mainPanel.name = "MainPanel";
        }
        else
        {
            mainPanel = CreateUIObject("MainPanel", selected.transform);
            SetStretch(mainPanel.GetComponent<RectTransform>(), new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.90f));
            EnsureImage(mainPanel, new Color(0.06f, 0.06f, 0.08f, 0.92f));
        }

        SetupMainPanel(mainPanel);

        RuneUIController controller = selected.GetComponent<RuneUIController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<RuneUIController>(selected);
        }

        BindController(controller, mainPanel);

        Debug.Log("[RuneUI] Built test rune UI under selected RuneUIPanel.", selected);
    }

    private static void SetupMainPanel(GameObject mainPanel)
    {
        RectTransform mainPanelRect = mainPanel.GetComponent<RectTransform>();
        if (mainPanelRect == null)
        {
            mainPanelRect = mainPanel.AddComponent<RectTransform>();
        }

        if (mainPanel.GetComponent<Image>() == null)
        {
            EnsureImage(mainPanel, new Color(0.06f, 0.06f, 0.08f, 0.92f));
        }

        Transform titleRow = EnsureChild(mainPanel.transform, "TitleRow");
        SetupTitleRow(titleRow as RectTransform);

        TextMeshProUGUI titleText = CreateOrReuseText(titleRow, "TitleText", "Rune Panel", 36, TextAlignmentOptions.MidlineLeft);
        RectTransform titleTextRect = titleText.rectTransform;
        titleTextRect.anchorMin = new Vector2(0f, 0f);
        titleTextRect.anchorMax = new Vector2(0f, 1f);
        titleTextRect.offsetMin = new Vector2(24f, 6f);
        titleTextRect.offsetMax = new Vector2(-120f, -6f);

        Button closeButton = CreateOrReuseButton(titleRow, "CloseButton", "X", 28);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.sizeDelta = new Vector2(80f, 36f);
        closeRect.anchoredPosition = new Vector2(-20f, 0f);

        Transform bodyRow = EnsureChild(mainPanel.transform, "BodyRow");
        SetupBodyRow(bodyRow as RectTransform);

        GameObject bagPanel = CreateOrReusePanel(bodyRow, "RuneBagPanel", new Color(0.12f, 0.12f, 0.14f, 0.88f));
        GameObject skillPanel = CreateOrReusePanel(bodyRow, "RuneSkillPanel", new Color(0.10f, 0.10f, 0.12f, 0.88f));

        SetupBagPanel(bagPanel);
        SetupSkillPanel(skillPanel);
    }

    private static void SetupTitleRow(RectTransform row)
    {
        if (row == null)
        {
            return;
        }

        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(0f, 56f);
        row.anchoredPosition = Vector2.zero;
    }

    private static void SetupBodyRow(RectTransform row)
    {
        if (row == null)
        {
            return;
        }

        row.anchorMin = new Vector2(0f, 0f);
        row.anchorMax = new Vector2(1f, 1f);
        row.offsetMin = new Vector2(20f, 20f);
        row.offsetMax = new Vector2(-20f, -64f);
    }

    private static void SetupBagPanel(GameObject bagPanel)
    {
        RectTransform rect = bagPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0.38f, 1f);
        rect.offsetMin = new Vector2(0f, 0f);
        rect.offsetMax = new Vector2(-10f, 0f);

        TextMeshProUGUI title = CreateOrReuseText(bagPanel.transform, "BagTitleText", "Rune Bag", 28, TextAlignmentOptions.MidlineLeft);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(0f, 32f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -20f);

        GameObject viewport = EnsureOrCreatePanel(bagPanel.transform, "RuneListViewport", new Color(0.06f, 0.06f, 0.07f, 0.85f));
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0.05f, 0.40f);
        viewportRect.anchorMax = new Vector2(0.95f, 0.82f);
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        if (viewport.GetComponent<RectMask2D>() == null)
        {
            Undo.AddComponent<RectMask2D>(viewport);
        }

        Transform listContent = EnsureChild(viewport.transform, "RuneListContent");
        RectTransform listRect = listContent as RectTransform;
        listRect.anchorMin = new Vector2(0f, 1f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.pivot = new Vector2(0.5f, 1f);
        listRect.anchoredPosition = new Vector2(0f, 0f);
        listRect.sizeDelta = new Vector2(0f, 10f + 44f * 10f);

        if (listContent.Find("RuneItem0") == null)
        {
            for (int i = 0; i < 10; i++)
            {
                GameObject item = CreateRuneListItem(listContent, $"RuneItem{i}");
                RectTransform itemRect = item.GetComponent<RectTransform>();
                itemRect.anchorMin = new Vector2(0f, 1f);
                itemRect.anchorMax = new Vector2(1f, 1f);
                itemRect.pivot = new Vector2(0.5f, 1f);
                itemRect.sizeDelta = new Vector2(0f, 40f);
                itemRect.anchoredPosition = new Vector2(0f, -i * 44f);
            }
        }

        TextMeshProUGUI noRuneText = CreateOrReuseText(bagPanel.transform, "NoRuneText", "No rune", 24, TextAlignmentOptions.Center);
        noRuneText.rectTransform.anchorMin = new Vector2(0.05f, 0.28f);
        noRuneText.rectTransform.anchorMax = new Vector2(0.95f, 0.38f);
        noRuneText.rectTransform.offsetMin = Vector2.zero;
        noRuneText.rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI selectedRuneText = CreateOrReuseText(bagPanel.transform, "SelectedRuneText", "Selected Rune: None", 24, TextAlignmentOptions.MidlineLeft);
        selectedRuneText.rectTransform.anchorMin = new Vector2(0.05f, 0.18f);
        selectedRuneText.rectTransform.anchorMax = new Vector2(0.95f, 0.26f);
        selectedRuneText.rectTransform.offsetMin = Vector2.zero;
        selectedRuneText.rectTransform.offsetMax = Vector2.zero;

        GameObject detailPanel = EnsureOrCreatePanel(bagPanel.transform, "RuneDetailPanel", new Color(0.08f, 0.08f, 0.10f, 0.92f));
        RectTransform detailRect = detailPanel.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0.05f, 0.02f);
        detailRect.anchorMax = new Vector2(0.95f, 0.16f);
        detailRect.offsetMin = Vector2.zero;
        detailRect.offsetMax = Vector2.zero;

        CreateOrReuseText(detailPanel.transform, "RuneNameText", "Rune Name: None", 22, TextAlignmentOptions.MidlineLeft);
        CreateOrReuseText(detailPanel.transform, "RuneTypeText", "Type: -", 20, TextAlignmentOptions.MidlineLeft);
        CreateOrReuseText(detailPanel.transform, "RuneDescriptionText", "Description: -", 20, TextAlignmentOptions.MidlineLeft);
        CreateOrReuseText(detailPanel.transform, "RuneEffectText", "Effect: -", 20, TextAlignmentOptions.MidlineLeft);

        PositionDetailTexts(detailPanel.transform);
    }

    private static void PositionDetailTexts(Transform detailPanel)
    {
        string[] names = { "RuneNameText", "RuneTypeText", "RuneDescriptionText", "RuneEffectText" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform child = detailPanel.Find(names[i]);
            if (child == null)
            {
                continue;
            }

            RectTransform rect = child as RectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(12f, 0f);
            rect.offsetMax = new Vector2(-12f, -24f);
            rect.anchoredPosition = new Vector2(0f, -i * 22f - 8f);
            rect.sizeDelta = new Vector2(0f, 20f);
        }
    }

    private static void SetupSkillPanel(GameObject skillPanel)
    {
        RectTransform rect = skillPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.40f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(10f, 0f);
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI title = CreateOrReuseText(skillPanel.transform, "SkillTitleText", "Rune Skill Panel", 28, TextAlignmentOptions.MidlineLeft);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(0f, 32f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -20f);

        Transform rowsRoot = EnsureChild(skillPanel.transform, "SkillSlotRowsRoot");
        RectTransform rowsRect = rowsRoot as RectTransform;
        rowsRect.anchorMin = new Vector2(0.03f, 0.04f);
        rowsRect.anchorMax = new Vector2(0.98f, 0.90f);
        rowsRect.offsetMin = Vector2.zero;
        rowsRect.offsetMax = Vector2.zero;

        for (int i = 0; i < SkillCount; i++)
        {
            string rowName = $"{SkillNames[i]}Row";
            Transform row = EnsureChild(rowsRoot, rowName);
            RectTransform rowRect = row as RectTransform;
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, 54f);
            rowRect.anchoredPosition = new Vector2(0f, -i * 68f);

            TextMeshProUGUI skillLabel = CreateOrReuseText(row, "SkillLabel", SkillNames[i], 24, TextAlignmentOptions.MidlineLeft);
            RectTransform labelRect = skillLabel.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.sizeDelta = new Vector2(36f, 32f);
            labelRect.anchoredPosition = new Vector2(0f, 0f);

            for (int slotIndex = 0; slotIndex < SlotsPerSkill; slotIndex++)
            {
                string slotName = $"{SkillNames[i]}Slot{slotIndex}";
                GameObject slot = EnsureOrCreateSlot(row, slotName);
                RectTransform slotRect = slot.GetComponent<RectTransform>();
                slotRect.anchorMin = new Vector2(0f, 0.5f);
                slotRect.anchorMax = new Vector2(0f, 0.5f);
                slotRect.pivot = new Vector2(0f, 0.5f);
                slotRect.sizeDelta = new Vector2(88f, 40f);
                slotRect.anchoredPosition = new Vector2(48f + slotIndex * 96f, 0f);

                TextMeshProUGUI slotLabel = slot.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (slotLabel != null)
                {
                    slotLabel.text = "Empty";
                }
            }
        }
    }

    private static GameObject CreateRuneListItem(Transform parent, string name)
    {
        GameObject item = CreateUIObject(name, parent);
        EnsureCanvasRenderer(item);
        EnsureImage(item, new Color(0.16f, 0.16f, 0.18f, 0.95f));

        Button button = item.AddComponent<Button>();
        ConfigureSelectable(button);

        TextMeshProUGUI label = CreateOrReuseText(item.transform, "Label", "Empty", 20, TextAlignmentOptions.MidlineLeft);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(12f, 4f);
        labelRect.offsetMax = new Vector2(-12f, -4f);

        return item;
    }

    private static GameObject EnsureOrCreateSlot(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            EnsureCanvasRenderer(existing.gameObject);
            if (existing.GetComponent<Button>() == null)
            {
                existing.gameObject.AddComponent<Button>();
            }

            if (existing.GetComponent<Image>() == null)
            {
                EnsureImage(existing.gameObject, new Color(0.18f, 0.18f, 0.20f, 0.95f));
            }

            if (existing.Find("Label") == null)
            {
                CreateOrReuseText(existing, "Label", "Empty", 20, TextAlignmentOptions.Center);
            }

            return existing.gameObject;
        }

        GameObject slot = CreateUIObject(name, parent);
        EnsureCanvasRenderer(slot);
        EnsureImage(slot, new Color(0.18f, 0.18f, 0.20f, 0.95f));

        Button button = slot.AddComponent<Button>();
        ConfigureSelectable(button);

        TextMeshProUGUI label = CreateOrReuseText(slot.transform, "Label", "Empty", 20, TextAlignmentOptions.Center);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(4f, 4f);
        labelRect.offsetMax = new Vector2(-4f, -4f);

        return slot;
    }

    private static GameObject CreateOrReusePanel(Transform parent, string name, Color color)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            EnsureCanvasRenderer(existing.gameObject);
            EnsureImage(existing.gameObject, color);
            return existing.gameObject;
        }

        GameObject panel = CreateUIObject(name, parent);
        EnsureCanvasRenderer(panel);
        EnsureImage(panel, color);
        return panel;
    }

    private static GameObject EnsureOrCreatePanel(Transform parent, string name, Color color)
    {
        return CreateOrReusePanel(parent, name, color);
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build Rune UI");
        Undo.SetTransformParent(go.transform, parent, "Build Rune UI");
        return go;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing;
        }

        return CreateUIObject(name, parent).transform;
    }

    private static TextMeshProUGUI CreateOrReuseText(Transform parent, string name, string value, float fontSize, TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(name);
        TextMeshProUGUI text;
        if (existing != null)
        {
            EnsureCanvasRenderer(existing.gameObject);
            text = existing.GetComponent<TextMeshProUGUI>();
            if (text == null)
            {
                text = existing.gameObject.AddComponent<TextMeshProUGUI>();
            }
        }
        else
        {
            GameObject go = CreateUIObject(name, parent);
            EnsureCanvasRenderer(go);
            text = go.AddComponent<TextMeshProUGUI>();
        }

        if (text.font == null)
        {
            TMP_FontAsset fallbackFont = TMP_Settings.defaultFontAsset;
            if (fallbackFont == null)
            {
                fallbackFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            }

            if (fallbackFont != null)
            {
                text.font = fallbackFont;
            }
        }

        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        if (rect != null)
        {
            rect.localScale = Vector3.one;
        }

        return text;
    }

    private static Button CreateOrReuseButton(Transform parent, string name, string label, float fontSize)
    {
        Transform existing = parent.Find(name);
        GameObject buttonGo;
        Button button;
        if (existing != null)
        {
            buttonGo = existing.gameObject;
            EnsureCanvasRenderer(buttonGo);
            button = buttonGo.GetComponent<Button>();
            if (button == null)
            {
                button = buttonGo.AddComponent<Button>();
            }
        }
        else
        {
            buttonGo = CreateUIObject(name, parent);
            EnsureCanvasRenderer(buttonGo);
            button = buttonGo.AddComponent<Button>();
        }

        EnsureImage(buttonGo, new Color(0.22f, 0.22f, 0.25f, 0.95f));
        ConfigureSelectable(button);

        Transform labelTransform = buttonGo.transform.Find("Label");
        TextMeshProUGUI text = labelTransform != null ? labelTransform.GetComponent<TextMeshProUGUI>() : null;
        if (text == null)
        {
            text = CreateOrReuseText(buttonGo.transform, "Label", label, fontSize, TextAlignmentOptions.Center);
        }
        else
        {
            text.text = label;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return button;
    }

    private static void ConfigureSelectable(Selectable selectable)
    {
        ColorBlock colors = selectable.colors;
        colors.normalColor = new Color(0.25f, 0.25f, 0.28f, 1f);
        colors.highlightedColor = new Color(0.35f, 0.35f, 0.40f, 1f);
        colors.pressedColor = new Color(0.18f, 0.18f, 0.20f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        selectable.colors = colors;
    }

    private static void EnsureImage(GameObject go, Color color)
    {
        EnsureCanvasRenderer(go);
        Image image = go.GetComponent<Image>();
        if (image == null)
        {
            image = Undo.AddComponent<Image>(go);
        }

        image.color = color;
        image.raycastTarget = true;
    }

    private static void EnsureCanvasRenderer(GameObject go)
    {
        if (go != null && go.GetComponent<CanvasRenderer>() == null)
        {
            Undo.AddComponent<CanvasRenderer>(go);
        }
    }

    private static void SetStretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void BindController(RuneUIController controller, GameObject mainPanel)
    {
        if (controller == null || mainPanel == null)
        {
            return;
        }

        controller.mainPanel = mainPanel;
        controller.closeButton = mainPanel.transform.Find("TitleRow/CloseButton")?.GetComponent<Button>();
        controller.runeListContent = mainPanel.transform.Find("BodyRow/RuneBagPanel/RuneListViewport/RuneListContent");
        controller.noRuneText = mainPanel.transform.Find("BodyRow/RuneBagPanel/NoRuneText")?.GetComponent<TextMeshProUGUI>();
        controller.selectedRuneText = mainPanel.transform.Find("BodyRow/RuneBagPanel/SelectedRuneText")?.GetComponent<TextMeshProUGUI>();
        controller.runeNameText = mainPanel.transform.Find("BodyRow/RuneBagPanel/RuneDetailPanel/RuneNameText")?.GetComponent<TextMeshProUGUI>();
        controller.runeTypeText = mainPanel.transform.Find("BodyRow/RuneBagPanel/RuneDetailPanel/RuneTypeText")?.GetComponent<TextMeshProUGUI>();
        controller.runeDescriptionText = mainPanel.transform.Find("BodyRow/RuneBagPanel/RuneDetailPanel/RuneDescriptionText")?.GetComponent<TextMeshProUGUI>();
        controller.runeEffectText = mainPanel.transform.Find("BodyRow/RuneBagPanel/RuneDetailPanel/RuneEffectText")?.GetComponent<TextMeshProUGUI>();

        BindSkillSlotGroup(controller, controller.qSlots, mainPanel.transform.Find("BodyRow/RuneSkillPanel/SkillSlotRowsRoot/QRow"));
        BindSkillSlotGroup(controller, controller.wSlots, mainPanel.transform.Find("BodyRow/RuneSkillPanel/SkillSlotRowsRoot/WRow"));
        BindSkillSlotGroup(controller, controller.eSlots, mainPanel.transform.Find("BodyRow/RuneSkillPanel/SkillSlotRowsRoot/ERow"));
        BindSkillSlotGroup(controller, controller.rSlots, mainPanel.transform.Find("BodyRow/RuneSkillPanel/SkillSlotRowsRoot/RRow"));
    }

    private static void BindSkillSlotGroup(RuneUIController controller, RuneUIController.RuneSlotView[] target, Transform row)
    {
        if (controller == null || target == null || target.Length < SlotsPerSkill || row == null)
        {
            return;
        }

        string skillName = row.name.Substring(0, 1);
        for (int i = 0; i < SlotsPerSkill; i++)
        {
            Transform slot = row.Find($"{skillName}Slot{i}");
            if (slot == null)
            {
                continue;
            }

            if (target[i] == null)
            {
                target[i] = new RuneUIController.RuneSlotView();
            }

            target[i].button = slot.GetComponent<Button>();
            target[i].label = slot.Find("Label")?.GetComponent<TextMeshProUGUI>();
        }
    }
}
