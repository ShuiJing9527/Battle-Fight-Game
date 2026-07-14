using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class BuildPlayerStatusHUDUnderSelected
{
    [MenuItem("Tools/YY/HUD/Build Player Status HUD Under Selected")]
    public static void BuildUnderSelected()
    {
        AddOrRepairExternalSkillHud();
    }

    [MenuItem("Tools/YY/HUD/Add Or Repair External Skill HUD")]
    public static void AddOrRepairExternalSkillHud()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Player Status HUD", "Please select PlayerStatusHUD first.", "OK");
            return;
        }

        if (selected.name != "PlayerStatusHUD")
        {
            EditorUtility.DisplayDialog("Player Status HUD", "Selected object must be PlayerStatusHUD.", "OK");
            return;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(selected))
        {
            EditorUtility.DisplayDialog("Player Status HUD", "Please run this on the scene instance, not on the prefab asset.", "OK");
            return;
        }

        PlayerStatusHUD hud = selected.GetComponent<PlayerStatusHUD>();
        if (hud == null)
        {
            EditorUtility.DisplayDialog("Player Status HUD", "Selected object is missing PlayerStatusHUD component.", "OK");
            return;
        }

        RectTransform selectedRect = EnsureRectTransform(selected);
        Transform existingHudRoot = selected.transform.Find("HudRoot");
        GameObject hudRoot;
        bool reusedHudRoot = existingHudRoot != null;
        if (reusedHudRoot)
        {
            hudRoot = existingHudRoot.gameObject;
        }
        else
        {
            hudRoot = CreateUIObject("HudRoot", selectedRect);
            SetupHudRoot(hudRoot.GetComponent<RectTransform>());
        }

        RectTransform hudRootRect = EnsureRectTransform(hudRoot);
        GameObject skillHudRoot = BuildSkillHudRoot(hudRootRect);
        GameObject skillTooltip = BuildSkillTooltip(hudRootRect);

        int repairedReferenceCount = RepairExistingHudReferences(hud, hudRoot, out string missingHudObjects);
        BindSkillHud(selected, skillHudRoot, skillTooltip);

        Selection.activeGameObject = hudRoot;
        Debug.Log(reusedHudRoot ? "[SkillHUDBuilder] Reused existing HudRoot." : "[SkillHUDBuilder] Created HudRoot.", selected);
        Debug.Log("[SkillHUDBuilder] Created/Reused SkillHUDRoot.", selected);
        Debug.Log("[SkillHUDBuilder] Created/Reused Q/W/E/R slots.", selected);
        Debug.Log("[SkillHUDBuilder] Bound PlayerSkillHUD external references.", selected);
        Debug.Log($"[SkillHUDBuilder] Repaired existing PlayerStatusHUD references: {repairedReferenceCount}.", selected);
        Debug.Log($"[SkillHUDBuilder] Missing existing PlayerStatusHUD objects: {missingHudObjects}.", selected);
        EditorSceneManager.MarkSceneDirty(selected.scene);
    }

    private static void BindHud(
        PlayerStatusHUD hud,
        GameObject root,
        Image hpFill,
        Image mpFill,
        Image shieldFill,
        TextMeshProUGUI hpText,
        TextMeshProUGUI mpText,
        TextMeshProUGUI shieldText,
        TextMeshProUGUI switchHintText,
        TextMeshProUGUI runeHintText,
        TextMeshProUGUI characterPanelHintText)
    {
        Undo.RecordObject(hud, "Bind Player Status HUD");

        SerializedObject serializedObject = new SerializedObject(hud);
        serializedObject.FindProperty("root").objectReferenceValue = root;
        serializedObject.FindProperty("hpFill").objectReferenceValue = hpFill;
        serializedObject.FindProperty("mpFill").objectReferenceValue = mpFill;
        serializedObject.FindProperty("shieldFill").objectReferenceValue = shieldFill;
        serializedObject.FindProperty("hpText").objectReferenceValue = hpText;
        serializedObject.FindProperty("mpText").objectReferenceValue = mpText;
        serializedObject.FindProperty("shieldText").objectReferenceValue = shieldText;
        serializedObject.FindProperty("switchHintText").objectReferenceValue = switchHintText;
        serializedObject.FindProperty("runeHintText").objectReferenceValue = runeHintText;
        serializedObject.FindProperty("characterPanelHintText").objectReferenceValue = characterPanelHintText;
        serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(hud);
    }

    private static void BindSkillHud(GameObject selected, GameObject skillHudRoot, GameObject skillTooltip)
    {
        if (selected == null)
        {
            return;
        }

        PlayerSkillHUD skillHud = selected.GetComponent<PlayerSkillHUD>();
        if (skillHud == null)
        {
            return;
        }

        Undo.RecordObject(skillHud, "Bind Player Skill HUD");
        SerializedObject serializedObject = new SerializedObject(skillHud);
        serializedObject.FindProperty("skillHudRoot").objectReferenceValue = skillHudRoot != null ? skillHudRoot.GetComponent<RectTransform>() : null;
        serializedObject.FindProperty("targetCanvas").objectReferenceValue = selected.GetComponentInParent<Canvas>();
        serializedObject.FindProperty("externalTooltipRoot").objectReferenceValue = skillTooltip != null ? skillTooltip.GetComponent<RectTransform>() : null;
        serializedObject.FindProperty("externalTooltipText").objectReferenceValue = skillTooltip != null ? skillTooltip.transform.Find("Text")?.GetComponent<TextMeshProUGUI>() : null;
        BindSlotView(serializedObject.FindProperty("qSlot"), skillHudRoot != null ? skillHudRoot.transform.Find("SkillSlot_Q") : null);
        BindSlotView(serializedObject.FindProperty("wSlot"), skillHudRoot != null ? skillHudRoot.transform.Find("SkillSlot_W") : null);
        BindSlotView(serializedObject.FindProperty("eSlot"), skillHudRoot != null ? skillHudRoot.transform.Find("SkillSlot_E") : null);
        BindSlotView(serializedObject.FindProperty("rSlot"), skillHudRoot != null ? skillHudRoot.transform.Find("SkillSlot_R") : null);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(skillHud);
    }

    private static int RepairExistingHudReferences(PlayerStatusHUD hud, GameObject hudRoot, out string missingHudObjects)
    {
        int repairedCount = 0;
        System.Collections.Generic.List<string> missing = new System.Collections.Generic.List<string>();

        Undo.RecordObject(hud, "Repair Player Status HUD References");
        SerializedObject serializedObject = new SerializedObject(hud);
        repairedCount += BindObjectReference(serializedObject, "root", hudRoot, "HudRoot", missing);
        repairedCount += BindComponentReference<Image>(serializedObject, "hpFill", hudRoot.transform, "HpBar/Fill", "HpBar/Fill", missing);
        repairedCount += BindComponentReference<Image>(serializedObject, "mpFill", hudRoot.transform, "MpBar/Fill", "MpBar/Fill", missing);
        repairedCount += BindComponentReference<Image>(serializedObject, "shieldFill", hudRoot.transform, "ShieldBar/Fill", "ShieldBar/Fill", missing);
        repairedCount += BindComponentReference<TextMeshProUGUI>(serializedObject, "hpText", hudRoot.transform, "HpBar/Label", "HpBar/Label", missing);
        repairedCount += BindComponentReference<TextMeshProUGUI>(serializedObject, "mpText", hudRoot.transform, "MpBar/Label", "MpBar/Label", missing);
        repairedCount += BindComponentReference<TextMeshProUGUI>(serializedObject, "shieldText", hudRoot.transform, "ShieldBar/Label", "ShieldBar/Label", missing);
        repairedCount += BindComponentReference<TextMeshProUGUI>(serializedObject, "switchHintText", hudRoot.transform, "HintPanel/SwitchHintText", "HintPanel/SwitchHintText", missing);
        repairedCount += BindComponentReference<TextMeshProUGUI>(serializedObject, "runeHintText", hudRoot.transform, "HintPanel/RuneHintText", "HintPanel/RuneHintText", missing);
        repairedCount += BindComponentReference<TextMeshProUGUI>(serializedObject, "characterPanelHintText", hudRoot.transform, "HintPanel/CharacterPanelHintText", "HintPanel/CharacterPanelHintText", missing);
        serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(hud);
        missingHudObjects = missing.Count > 0 ? string.Join(", ", missing) : "None";
        if (missing.Count > 0)
        {
            Debug.LogWarning($"[SkillHUDBuilder] Missing existing PlayerStatusHUD objects: {missingHudObjects}.", hud);
        }

        return repairedCount;
    }

    private static int BindObjectReference(SerializedObject serializedObject, string propertyName, Object value, string missingName, System.Collections.Generic.List<string> missing)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            missing.Add(propertyName);
            return 0;
        }

        if (value == null)
        {
            missing.Add(missingName);
            return 0;
        }

        property.objectReferenceValue = value;
        return 1;
    }

    private static int BindComponentReference<T>(
        SerializedObject serializedObject,
        string propertyName,
        Transform parent,
        string relativePath,
        string missingName,
        System.Collections.Generic.List<string> missing)
        where T : Component
    {
        Transform child = parent != null ? parent.Find(relativePath) : null;
        T component = child != null ? child.GetComponent<T>() : null;
        return BindObjectReference(serializedObject, propertyName, component, missingName, missing);
    }

    private static void BindSlotView(SerializedProperty property, Transform root)
    {
        if (property == null)
        {
            return;
        }

        property.FindPropertyRelative("root").objectReferenceValue = root as RectTransform;
        property.FindPropertyRelative("iconImage").objectReferenceValue = root != null ? root.Find("Icon")?.GetComponent<Image>() : null;
        property.FindPropertyRelative("cooldownMaskImage").objectReferenceValue = root != null ? root.Find("CooldownOverlay")?.GetComponent<Image>() : null;
        property.FindPropertyRelative("cooldownText").objectReferenceValue = root != null ? root.Find("CooldownText")?.GetComponent<Text>() : null;
        property.FindPropertyRelative("keyText").objectReferenceValue = root != null ? root.Find("KeyLabel")?.GetComponent<Text>() : null;
        property.FindPropertyRelative("disabledOverlay").objectReferenceValue = root != null ? root.Find("DisabledOverlay")?.gameObject : null;
        property.FindPropertyRelative("hoverHighlight").objectReferenceValue = root != null ? root.Find("HoverHighlight")?.GetComponent<Image>() : null;
        property.FindPropertyRelative("hoverTrigger").objectReferenceValue = root != null ? root.GetComponent<SkillHoverTrigger>() : null;
    }

    private static GameObject CreateStatusBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Color fillColor, string labelText)
    {
        GameObject bar = CreateUIObject(name, parent);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = anchorMin;
        barRect.anchorMax = anchorMax;
        barRect.pivot = new Vector2(0f, 1f);
        barRect.anchoredPosition = anchoredPosition;
        barRect.sizeDelta = new Vector2(360f, 32f);

        GameObject background = CreateUIObject("Background", bar.transform);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        Stretch(backgroundRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
        Image backgroundImage = EnsureImage(background, new Color(0f, 0f, 0f, 0.65f));
        backgroundImage.type = Image.Type.Simple;

        GameObject fill = CreateUIObject("Fill", bar.transform);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        Stretch(fillRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(3f, 3f), new Vector2(-3f, -3f));
        Image fillImage = EnsureImage(fill, fillColor);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;

        TextMeshProUGUI label = CreateText(bar.transform, "Label", labelText, 22f, TextAlignmentOptions.Center);
        RectTransform labelRect = label.rectTransform;
        Stretch(labelRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-8f, 0f));

        return bar;
    }

    private static GameObject BuildSkillHudRoot(RectTransform parent)
    {
        GameObject existing = parent.Find("SkillHUDRoot")?.gameObject;
        GameObject root = existing;
        if (root == null)
        {
            root = CreateUIObject("SkillHUDRoot", parent);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.anchoredPosition = new Vector2(-80f, 70f);
            rootRect.sizeDelta = new Vector2(368f, 80f);
            rootRect.localScale = Vector3.one;
        }

        string[] keys = { "Q", "W", "E", "R" };
        for (int i = 0; i < keys.Length; i++)
        {
            CreateOrRepairSkillSlot(root.transform, keys[i], i);
        }

        return root;
    }

    private static GameObject CreateOrRepairSkillSlot(Transform parent, string key, int index)
    {
        Transform existing = parent.Find($"SkillSlot_{key}");
        GameObject slot = existing != null ? existing.gameObject : CreateSkillSlot(parent, key, index);
        EnsureComponent<SkillHoverTrigger>(slot);
        EnsureSkillSlotChild(slot.transform, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, image =>
        {
            image.color = new Color(0.08f, 0.1f, 0.14f, 0.92f);
            image.type = Image.Type.Simple;
        });
        EnsureSkillSlotChild(slot.transform, "Icon", new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Vector2.zero, Vector2.zero, image =>
        {
            image.color = new Color(0.85f, 0.87f, 0.92f, 1f);
            image.preserveAspect = true;
        });
        EnsureSkillSlotChild(slot.transform, "CooldownOverlay", new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Vector2.zero, Vector2.zero, image =>
        {
            image.color = new Color(0f, 0f, 0f, 0.45f);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = 2;
            image.fillClockwise = true;
        });
        EnsureLegacyTextChild(slot.transform, "CooldownText", string.Empty, 36, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        EnsureKeyLabel(slot.transform, key);
        bool hadHoverHighlight = slot.transform.Find("HoverHighlight") != null;
        GameObject hoverHighlightObject = EnsureSkillSlotChild(slot.transform, "HoverHighlight", new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Vector2.zero, Vector2.zero, image =>
        {
            image.color = new Color(1f, 0.9f, 0.35f, 0.5f);
            image.raycastTarget = false;
            image.enabled = false;
        });
        if (!hadHoverHighlight)
        {
            hoverHighlightObject.transform.localScale = Vector3.one * 1.18f;
            hoverHighlightObject.SetActive(false);
        }

        return slot;
    }

    private static GameObject CreateSkillSlot(Transform parent, string key, int index)
    {
        GameObject slot = CreateUIObject($"SkillSlot_{key}", parent);
        RectTransform slotRect = slot.GetComponent<RectTransform>();
        slotRect.anchorMin = new Vector2(1f, 0f);
        slotRect.anchorMax = new Vector2(1f, 0f);
        slotRect.pivot = new Vector2(1f, 0f);
        slotRect.anchoredPosition = new Vector2(-((80f + 16f) * (3 - index)), 0f);
        slotRect.sizeDelta = new Vector2(80f, 80f);

        GameObject background = CreateUIObject("Background", slot.transform);
        Stretch(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        EnsureImage(background, new Color(0.08f, 0.1f, 0.14f, 0.92f));

        GameObject icon = CreateUIObject("Icon", slot.transform);
        Stretch(icon.GetComponent<RectTransform>(), new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Vector2.zero, Vector2.zero);
        Image iconImage = EnsureImage(icon, new Color(0.85f, 0.87f, 0.92f, 1f));
        iconImage.preserveAspect = true;

        GameObject overlay = CreateUIObject("CooldownOverlay", slot.transform);
        Stretch(overlay.GetComponent<RectTransform>(), new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Vector2.zero, Vector2.zero);
        Image overlayImage = EnsureImage(overlay, new Color(0f, 0f, 0f, 0.45f));
        overlayImage.type = Image.Type.Filled;
        overlayImage.fillMethod = Image.FillMethod.Radial360;
        overlayImage.fillOrigin = 2;
        overlayImage.fillClockwise = true;
        overlayImage.fillAmount = 0f;

        GameObject cooldownText = CreateUIObject("CooldownText", slot.transform);
        Stretch(cooldownText.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Text cooldownTextLabel = Undo.AddComponent<Text>(cooldownText);
        cooldownTextLabel.text = string.Empty;
        cooldownTextLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cooldownTextLabel.fontSize = 36;
        cooldownTextLabel.fontStyle = FontStyle.Bold;
        cooldownTextLabel.alignment = TextAnchor.MiddleCenter;
        cooldownTextLabel.color = Color.white;
        cooldownTextLabel.raycastTarget = false;

        GameObject keyLabel = CreateUIObject("KeyLabel", slot.transform);
        RectTransform keyLabelRect = keyLabel.GetComponent<RectTransform>();
        keyLabelRect.anchorMin = new Vector2(0f, 1f);
        keyLabelRect.anchorMax = new Vector2(0f, 1f);
        keyLabelRect.pivot = new Vector2(0f, 1f);
        keyLabelRect.anchoredPosition = new Vector2(8f, -6f);
        keyLabelRect.sizeDelta = new Vector2(32f, 20f);
        Text keyLabelText = Undo.AddComponent<Text>(keyLabel);
        keyLabelText.text = key;
        keyLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        keyLabelText.fontSize = 20;
        keyLabelText.color = Color.white;
        keyLabelText.alignment = TextAnchor.UpperLeft;
        keyLabelText.raycastTarget = false;

        GameObject hoverHighlight = CreateUIObject("HoverHighlight", slot.transform);
        RectTransform hoverRect = hoverHighlight.GetComponent<RectTransform>();
        Stretch(hoverRect, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Vector2.zero, Vector2.zero);
        hoverRect.localScale = Vector3.one * 1.18f;
        Image hoverImage = EnsureImage(hoverHighlight, new Color(1f, 0.9f, 0.35f, 0.5f));
        hoverImage.raycastTarget = false;
        hoverImage.enabled = false;
        hoverHighlight.SetActive(false);

        return slot;
    }

    private static GameObject BuildSkillTooltip(RectTransform parent)
    {
        GameObject existing = parent.Find("SkillTooltip")?.gameObject;
        GameObject tooltip = existing;
        if (tooltip == null)
        {
            tooltip = CreateUIObject("SkillTooltip", parent);
            RectTransform tooltipRect = tooltip.GetComponent<RectTransform>();
            tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
            tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
            tooltipRect.pivot = new Vector2(0.5f, 0f);
            tooltipRect.sizeDelta = new Vector2(340f, 64f);
            tooltip.SetActive(false);
        }

        EnsureSkillSlotChild(tooltip.transform, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, image =>
        {
            image.color = new Color(0.08f, 0.1f, 0.14f, 0.96f);
        });
        TextMeshProUGUI text = EnsureTextMeshProChild(tooltip.transform, "Text", string.Empty, 18f, TextAlignmentOptions.TopLeft, Vector2.zero, Vector2.one, new Vector2(12f, 10f), new Vector2(-12f, -10f));
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.lineSpacing = 4f;
        text.raycastTarget = false;

        return tooltip;
    }

    private static void SetupHudRoot(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        rect.sizeDelta = new Vector2(400f, 260f);
        rect.localScale = Vector3.one;
    }

    private static void SetupHintPanel(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(0f, -152f);
        rect.sizeDelta = new Vector2(360f, 108f);
    }

    private static void ConfigureHintText(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        float height)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(offsetMin.x, offsetMax.y - height);
        rect.offsetMax = new Vector2(offsetMax.x, offsetMax.y);
        rect.anchoredPosition = new Vector2(0f, offsetMin.y);
        rect.sizeDelta = new Vector2(0f, height);
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static RectTransform EnsureRectTransform(GameObject gameObject)
    {
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        if (rect != null)
        {
            return rect;
        }

        return Undo.AddComponent<RectTransform>(gameObject);
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(gameObject);
    }

    private static GameObject EnsureSkillSlotChild(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        System.Action<Image> configureNewImage)
    {
        Transform existing = parent.Find(name);
        bool created = existing == null;
        GameObject child = created ? CreateUIObject(name, parent) : existing.gameObject;
        if (created)
        {
            Stretch(child.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
        }

        Image image = child.GetComponent<Image>();
        bool addedImage = image == null;
        if (addedImage)
        {
            image = Undo.AddComponent<Image>(child);
        }

        if ((created || addedImage) && configureNewImage != null)
        {
            configureNewImage(image);
        }

        return child;
    }

    private static Text EnsureLegacyTextChild(
        Transform parent,
        string name,
        string defaultText,
        int fontSize,
        TextAnchor alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        Transform existing = parent.Find(name);
        bool created = existing == null;
        GameObject child = created ? CreateUIObject(name, parent) : existing.gameObject;
        if (created)
        {
            Stretch(child.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
        }

        Text text = child.GetComponent<Text>();
        bool addedText = text == null;
        if (addedText)
        {
            text = Undo.AddComponent<Text>(child);
        }

        if (created || addedText)
        {
            text.text = defaultText;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        return text;
    }

    private static Text EnsureKeyLabel(Transform parent, string key)
    {
        Transform existing = parent.Find("KeyLabel");
        bool created = existing == null;
        GameObject child = created ? CreateUIObject("KeyLabel", parent) : existing.gameObject;
        if (created)
        {
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(8f, -6f);
            rect.sizeDelta = new Vector2(32f, 20f);
        }

        Text text = child.GetComponent<Text>();
        bool addedText = text == null;
        if (addedText)
        {
            text = Undo.AddComponent<Text>(child);
        }

        if (created || addedText)
        {
            text.text = key;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.raycastTarget = false;
        }

        return text;
    }

    private static TextMeshProUGUI EnsureTextMeshProChild(
        Transform parent,
        string name,
        string defaultText,
        float fontSize,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        Transform existing = parent.Find(name);
        bool created = existing == null;
        GameObject child = created ? CreateUIObject(name, parent) : existing.gameObject;
        if (created)
        {
            Stretch(child.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
        }

        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        bool addedText = text == null;
        if (addedText)
        {
            text = Undo.AddComponent<TextMeshProUGUI>(child);
        }

        if (created || addedText)
        {
            text.text = defaultText;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.font = ResolveFontAsset();
        }

        return text;
    }

    private static Image EnsureImage(GameObject gameObject, Color color)
    {
        Image image = gameObject.GetComponent<Image>();
        if (image == null)
        {
            image = Undo.AddComponent<Image>(gameObject);
        }

        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float fontSize, TextAlignmentOptions alignment)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            TextMeshProUGUI existingText = existing.GetComponent<TextMeshProUGUI>();
            if (existingText != null)
            {
                existingText.text = text;
                existingText.fontSize = fontSize;
                existingText.alignment = alignment;
                return existingText;
            }
        }

        GameObject gameObject = CreateUIObject(name, parent);
        TextMeshProUGUI label = Undo.AddComponent<TextMeshProUGUI>(gameObject);
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        label.font = ResolveFontAsset();
        return label;
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

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }
}
