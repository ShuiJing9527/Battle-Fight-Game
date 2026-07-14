using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class RebuildRuneUIPanelLayoutSafely
{
    private const string PrefabPath = "Assets/Prefabs/UI/Rune/RuneUIPanel.prefab";

    [MenuItem("Tools/YY/Rune UI/Rebuild RuneUIPanel Layout Safely")]
    public static void Rebuild()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"[RuneUI] Failed to load prefab: {PrefabPath}");
            return;
        }

        List<string> changes = new List<string>();

        try
        {
            GameObject mainPanel = FindChild(prefabRoot.transform, "MainPanel")?.gameObject;
            GameObject runeBagPanel = FindChild(prefabRoot.transform, "RuneBagPanel")?.gameObject;
            GameObject runeSkillPanel = FindChild(prefabRoot.transform, "RuneSkillPanel")?.gameObject;
            GameObject runeListViewport = FindOrCreateChild(runeBagPanel, "RuneListViewport", changes);
            GameObject runeListContent = FindOrCreateChild(runeListViewport, "RuneListContent", changes);
            GameObject selectedRuneText = FindChild(prefabRoot.transform, "SelectedRuneText")?.gameObject;
            GameObject runeDetailPanel = FindChild(prefabRoot.transform, "RuneDetailPanel")?.gameObject;
            GameObject noRuneText = FindChild(prefabRoot.transform, "NoRuneText")?.gameObject;
            GameObject skillRowsRoot = FindChild(prefabRoot.transform, "SkillSlotRowsRoot")?.gameObject;

            if (mainPanel == null || runeBagPanel == null || runeSkillPanel == null || selectedRuneText == null || noRuneText == null)
            {
                Debug.LogError("[RuneUI] Missing one of the required objects: MainPanel / RuneBagPanel / RuneSkillPanel / SelectedRuneText / NoRuneText");
                return;
            }

            RebuildRuneBagPanel(runeBagPanel, runeListViewport, runeListContent, selectedRuneText, noRuneText, runeDetailPanel, changes);
            RebuildSkillRowIcons(mainPanel, changes);
            GameObject skillDescriptionPanel = RebuildSkillDescriptionPanel(runeSkillPanel, skillRowsRoot, changes);
            BindControllerReferences(prefabRoot, mainPanel, runeListViewport, runeListContent, runeDetailPanel, skillDescriptionPanel, changes);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Object savedPrefab = AssetDatabase.LoadAssetAtPath<Object>(PrefabPath);
            Selection.activeObject = savedPrefab;
            EditorGUIUtility.PingObject(savedPrefab);

            Debug.Log("[RuneUI] Rebuilt RuneUIPanel layout safely:\n- " + string.Join("\n- ", changes));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void RebuildRuneBagPanel(
        GameObject runeBagPanel,
        GameObject runeListViewport,
        GameObject runeListContent,
        GameObject selectedRuneText,
        GameObject noRuneText,
        GameObject runeDetailPanel,
        List<string> changes)
    {
        RectTransform bagRect = runeBagPanel.GetComponent<RectTransform>();
        RectTransform viewportRect = runeListViewport.GetComponent<RectTransform>();
        RectTransform contentRect = runeListContent.GetComponent<RectTransform>();
        RectTransform selectedRect = selectedRuneText.GetComponent<RectTransform>();
        RectTransform noRuneRect = noRuneText.GetComponent<RectTransform>();

        SetParentIfNeeded(runeListViewport.transform, runeBagPanel.transform, changes, "Moved RuneListViewport under RuneBagPanel");
        SetParentIfNeeded(runeListContent.transform, runeListViewport.transform, changes, "Moved RuneListContent under RuneListViewport");
        SetParentIfNeeded(noRuneText.transform, runeListViewport.transform, changes, "Moved NoRuneText into RuneListViewport");

        EnsureImage(runeListViewport, new Color(0.06f, 0.06f, 0.07f, 0.85f));
        EnsureRectMask2D(runeListViewport);
        ConfigureRectStretchArea(viewportRect, new Vector2(0.05f, 0.24f), new Vector2(0.91f, 0.86f));
        changes.Add("Adjusted RuneListViewport to the taller left bag area");

        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        if (contentRect.sizeDelta.y < 450f)
        {
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, 450f);
        }
        changes.Add("Kept RuneListContent under RuneListViewport for scrolling");

        selectedRect.anchorMin = new Vector2(0.05f, 0.14f);
        selectedRect.anchorMax = new Vector2(0.91f, 0.20f);
        selectedRect.offsetMin = Vector2.zero;
        selectedRect.offsetMax = Vector2.zero;
        if (selectedRuneText.TryGetComponent(out TextMeshProUGUI selectedTmp))
        {
            selectedTmp.enableWordWrapping = true;
            selectedTmp.alignment = TextAlignmentOptions.MidlineLeft;
            selectedTmp.text = string.IsNullOrWhiteSpace(selectedTmp.text) ? "Selected Rune: None" : selectedTmp.text;
        }
        changes.Add("Moved SelectedRuneText to the compact lower-left status line");

        noRuneRect.anchorMin = Vector2.zero;
        noRuneRect.anchorMax = Vector2.one;
        noRuneRect.offsetMin = Vector2.zero;
        noRuneRect.offsetMax = Vector2.zero;
        if (noRuneText.TryGetComponent(out TextMeshProUGUI noRuneTmp))
        {
            noRuneTmp.alignment = TextAlignmentOptions.Center;
            noRuneTmp.enableWordWrapping = true;
        }
        changes.Add("Centered NoRuneText inside RuneListViewport");

        GameObject scrollbarObject = FindOrCreateChild(runeBagPanel, "RuneBagScrollbar", changes);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        ConfigureRectStretchArea(scrollbarRect, new Vector2(0.92f, 0.24f), new Vector2(0.97f, 0.86f));
        Scrollbar scrollbar = EnsureScrollbar(scrollbarObject, changes);
        changes.Add("Ensured RuneBagScrollbar exists on the right side of RuneListViewport");

        ScrollRect bagScrollRect = EnsureComponent<ScrollRect>(runeBagPanel);
        bagScrollRect.viewport = viewportRect;
        bagScrollRect.content = contentRect;
        bagScrollRect.horizontal = false;
        bagScrollRect.vertical = true;
        bagScrollRect.movementType = ScrollRect.MovementType.Clamped;
        bagScrollRect.scrollSensitivity = 24f;
        bagScrollRect.verticalScrollbar = scrollbar;
        bagScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        changes.Add("Configured RuneBagPanel ScrollRect with viewport/content/scrollbar");

        if (runeDetailPanel != null && runeDetailPanel.activeSelf)
        {
            runeDetailPanel.SetActive(false);
            changes.Add("Disabled legacy RuneDetailPanel by default");
        }

        if (bagRect != null)
        {
            EditorUtility.SetDirty(bagRect);
        }
    }

    private static GameObject RebuildSkillDescriptionPanel(GameObject runeSkillPanel, GameObject skillRowsRoot, List<string> changes)
    {
        if (skillRowsRoot != null)
        {
            RectTransform rowsRect = skillRowsRoot.GetComponent<RectTransform>();
            ConfigureRectStretchArea(rowsRect, new Vector2(0.03f, 0.40f), new Vector2(0.98f, 0.90f));
            changes.Add("Compressed SkillSlotRowsRoot to the upper-right panel area");
        }

        GameObject panel = FindOrCreateChild(runeSkillPanel, "SkillDescriptionPanel", changes);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        ConfigureRectStretchArea(panelRect, new Vector2(0.03f, 0.05f), new Vector2(0.98f, 0.34f));
        EnsureImage(panel, new Color(0.09f, 0.11f, 0.16f, 0.94f));

        GameObject titleObject = FindOrCreateChild(panel, "Title", changes);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(18f, -42f);
        titleRect.offsetMax = new Vector2(-18f, -10f);
        ConfigureTmp(titleObject, 24f, TextAlignmentOptions.MidlineLeft, "Description", false);

        GameObject bodyViewport = FindOrCreateChild(panel, "BodyViewport", changes);
        RectTransform bodyViewportRect = bodyViewport.GetComponent<RectTransform>();
        bodyViewportRect.anchorMin = new Vector2(0f, 0f);
        bodyViewportRect.anchorMax = new Vector2(0.94f, 1f);
        bodyViewportRect.pivot = new Vector2(0.5f, 0.5f);
        bodyViewportRect.offsetMin = new Vector2(0f, 18f);
        bodyViewportRect.offsetMax = new Vector2(-16f, -46f);
        EnsureImage(bodyViewport, new Color(0f, 0f, 0f, 0.001f));
        EnsureRectMask2D(bodyViewport);

        GameObject body = FindOrCreateChild(bodyViewport, "Body", changes);
        RectTransform bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.offsetMin = new Vector2(18f, 0f);
        bodyRect.offsetMax = new Vector2(-18f, 0f);
        if (bodyRect.sizeDelta.y < 180f)
        {
            bodyRect.sizeDelta = new Vector2(bodyRect.sizeDelta.x, 180f);
        }
        ConfigureTmp(body, 18f, TextAlignmentOptions.TopLeft, "Hover a skill or rune to view details.", true);

        GameObject scrollbarObject = FindOrCreateChild(panel, "DescriptionScrollbar", changes);
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        ConfigureRectStretchArea(scrollbarRect, new Vector2(0.95f, 0.08f), new Vector2(0.99f, 0.90f));
        Scrollbar scrollbar = EnsureScrollbar(scrollbarObject, changes);

        ScrollRect descriptionScrollRect = EnsureComponent<ScrollRect>(panel);
        descriptionScrollRect.viewport = bodyViewportRect;
        descriptionScrollRect.content = bodyRect;
        descriptionScrollRect.horizontal = false;
        descriptionScrollRect.vertical = true;
        descriptionScrollRect.movementType = ScrollRect.MovementType.Clamped;
        descriptionScrollRect.scrollSensitivity = 24f;
        descriptionScrollRect.verticalScrollbar = scrollbar;
        descriptionScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        changes.Add("Configured SkillDescriptionPanel as the shared rune/skill description area");

        return panel;
    }

    private static void RebuildSkillRowIcons(GameObject mainPanel, List<string> changes)
    {
        if (mainPanel == null)
        {
            return;
        }

        string[] keys = { "Q", "W", "E", "R" };
        for (int i = 0; i < keys.Length; i++)
        {
            Transform row = FindChild(mainPanel.transform, $"{keys[i]}Row");
            if (row == null)
            {
                continue;
            }

            GameObject iconRoot = FindOrCreateDirectChild(row.gameObject, $"{keys[i]}SkillIcon", changes);
            RectTransform iconRect = iconRoot.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(60f, 0f);
            iconRect.sizeDelta = new Vector2(64f, 64f);

            Image iconImage = EnsureComponent<Image>(iconRoot);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            GameObject highlight = FindOrCreateDirectChild(iconRoot, "HoverHighlight", changes);
            RectTransform highlightRect = highlight.GetComponent<RectTransform>();
            highlightRect.anchorMin = Vector2.zero;
            highlightRect.anchorMax = Vector2.one;
            highlightRect.offsetMin = Vector2.zero;
            highlightRect.offsetMax = Vector2.zero;
            highlightRect.localScale = Vector3.one * 1.18f;
            Image highlightImage = EnsureComponent<Image>(highlight);
            highlightImage.color = new Color(1f, 0.9f, 0.35f, 0.5f);
            highlightImage.raycastTarget = false;
            highlightImage.enabled = false;
            highlight.SetActive(false);

            EnsureComponent<SkillHoverTrigger>(iconRoot);
            changes.Add($"Ensured external {keys[i]} skill icon view exists");
        }
    }

    private static void BindControllerReferences(
        GameObject prefabRoot,
        GameObject mainPanel,
        GameObject runeListViewport,
        GameObject runeListContent,
        GameObject runeDetailPanel,
        GameObject skillDescriptionPanel,
        List<string> changes)
    {
        RuneUIController controller = prefabRoot.GetComponent<RuneUIController>();
        if (controller == null)
        {
            Debug.LogWarning("[RuneUI] RuneUIController not found on prefab root. Layout rebuilt, but runtime bindings were not updated.");
            return;
        }

        controller.mainPanel = mainPanel;
        controller.runeListContent = runeListContent.transform;
        controller.runeBagViewportRect = runeListViewport.GetComponent<RectTransform>();
        controller.runeBagContentRoot = runeListContent.GetComponent<RectTransform>();
        controller.detailPanelRoot = runeDetailPanel != null ? runeDetailPanel.GetComponent<RectTransform>() : null;
        controller.runeBagScrollbar = FindChild(prefabRoot.transform, "RuneBagScrollbar")?.GetComponent<Scrollbar>();
        controller.sharedDescriptionText = FindChild(skillDescriptionPanel.transform, "Body")?.GetComponent<TextMeshProUGUI>();
        if (controller.sharedDescriptionText == null)
        {
            controller.sharedDescriptionText = FindChild(skillDescriptionPanel.transform, "BodyViewport/Body")?.GetComponent<TextMeshProUGUI>();
        }
        controller.sharedDescriptionScrollRect = skillDescriptionPanel.GetComponent<ScrollRect>();

        SerializedObject serializedObject = new SerializedObject(controller);
        BindSkillIconView(serializedObject.FindProperty("qSkillIcon"), FindChild(prefabRoot.transform, "QSkillIcon"));
        BindSkillIconView(serializedObject.FindProperty("wSkillIcon"), FindChild(prefabRoot.transform, "WSkillIcon"));
        BindSkillIconView(serializedObject.FindProperty("eSkillIcon"), FindChild(prefabRoot.transform, "ESkillIcon"));
        BindSkillIconView(serializedObject.FindProperty("rSkillIcon"), FindChild(prefabRoot.transform, "RSkillIcon"));
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(controller);
        changes.Add("Updated RuneUIController references to viewport/content/scrollbars/shared description");
    }

    private static void BindSkillIconView(SerializedProperty property, Transform root)
    {
        if (property == null)
        {
            return;
        }

        property.FindPropertyRelative("root").objectReferenceValue = root as RectTransform;
        property.FindPropertyRelative("icon").objectReferenceValue = root != null ? root.GetComponent<Image>() : null;
        property.FindPropertyRelative("hoverHighlight").objectReferenceValue = root != null ? root.Find("HoverHighlight")?.GetComponent<Image>() : null;
        property.FindPropertyRelative("hoverTrigger").objectReferenceValue = root != null ? root.GetComponent<SkillHoverTrigger>() : null;
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }

    private static void EnsureImage(GameObject gameObject, Color color)
    {
        Image image = EnsureComponent<Image>(gameObject);
        image.color = color;
        image.raycastTarget = true;
    }

    private static void EnsureRectMask2D(GameObject gameObject)
    {
        EnsureComponent<RectMask2D>(gameObject);
    }

    private static Scrollbar EnsureScrollbar(GameObject gameObject, List<string> changes)
    {
        EnsureImage(gameObject, new Color(0.20f, 0.22f, 0.26f, 0.90f));
        Scrollbar scrollbar = EnsureComponent<Scrollbar>(gameObject);
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.value = 1f;

        GameObject slidingArea = FindOrCreateChild(gameObject, "Sliding Area", changes);
        RectTransform slidingRect = slidingArea.GetComponent<RectTransform>();
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.offsetMin = new Vector2(2f, 2f);
        slidingRect.offsetMax = new Vector2(-2f, -2f);

        GameObject handle = FindOrCreateChild(slidingArea, "Handle", changes);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0f, 1f);
        handleRect.anchorMax = new Vector2(1f, 1f);
        handleRect.pivot = new Vector2(0.5f, 1f);
        if (handleRect.sizeDelta.y <= 0f)
        {
            handleRect.sizeDelta = new Vector2(handleRect.sizeDelta.x, 64f);
        }
        EnsureImage(handle, new Color(0.88f, 0.74f, 0.34f, 0.95f));

        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        return scrollbar;
    }

    private static void ConfigureTmp(GameObject gameObject, float fontSize, TextAlignmentOptions alignment, string fallbackText, bool wordWrap)
    {
        TextMeshProUGUI tmp = EnsureComponent<TextMeshProUGUI>(gameObject);
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = wordWrap;
        tmp.raycastTarget = false;
        if (string.IsNullOrWhiteSpace(tmp.text))
        {
            tmp.text = fallbackText;
        }
    }

    private static void ConfigureRectStretchArea(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void SetParentIfNeeded(Transform child, Transform parent, List<string> changes, string message)
    {
        if (child == null || parent == null || child.parent == parent)
        {
            return;
        }

        child.SetParent(parent, false);
        changes.Add(message);
    }

    private static GameObject FindOrCreateChild(GameObject parent, string name, List<string> changes)
    {
        if (parent == null)
        {
            return null;
        }

        Transform existing = FindChild(parent.transform, name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent.transform, false);
        created.layer = parent.layer;
        changes.Add($"Created {name}");
        return created;
    }

    private static GameObject FindOrCreateDirectChild(GameObject parent, string name, List<string> changes)
    {
        if (parent == null)
        {
            return null;
        }

        Transform existing = parent.transform.Find(name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent.transform, false);
        created.layer = parent.layer;
        changes.Add($"Created {name}");
        return created;
    }

    private static Transform FindChild(Transform parent, string pathOrName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(pathOrName))
        {
            return null;
        }

        Transform byPath = parent.Find(pathOrName);
        if (byPath != null)
        {
            return byPath;
        }

        if (parent.name == pathOrName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChild(parent.GetChild(i), pathOrName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
