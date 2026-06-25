using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BuildPlayerStatusHUDUnderSelected
{
    [MenuItem("Tools/YY/HUD/Build Player Status HUD Under Selected")]
    public static void BuildUnderSelected()
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

        if (selected.transform.Find("HudRoot") != null)
        {
            EditorUtility.DisplayDialog(
                "Player Status HUD",
                "HudRoot already exists. Delete it manually if you want to rebuild.",
                "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Player Status HUD",
                "Build test PlayerStatusHUD under selected object?",
                "OK",
                "Cancel"))
        {
            return;
        }

        PlayerStatusHUD hud = selected.GetComponent<PlayerStatusHUD>();
        if (hud == null)
        {
            EditorUtility.DisplayDialog("Player Status HUD", "Selected object is missing PlayerStatusHUD component.", "OK");
            return;
        }

        RectTransform selectedRect = EnsureRectTransform(selected);
        GameObject hudRoot = CreateUIObject("HudRoot", selectedRect);
        SetupHudRoot(hudRoot.GetComponent<RectTransform>());

        GameObject hintPanel = CreateUIObject("HintPanel", hudRoot.transform);
        SetupHintPanel(hintPanel.GetComponent<RectTransform>());

        TextMeshProUGUI switchHintText = CreateText(hintPanel.transform, "SwitchHintText", "T: Switch Player", 28f, TextAlignmentOptions.TopLeft);
        ConfigureHintText(switchHintText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, -4f), 34f);

        TextMeshProUGUI runeHintText = CreateText(hintPanel.transform, "RuneHintText", "K: Rune Panel", 28f, TextAlignmentOptions.TopLeft);
        ConfigureHintText(runeHintText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -38f), new Vector2(0f, -42f), 34f);

        GameObject hpBar = CreateStatusBar(hudRoot.transform, "HpBar", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -96f), new Color(0.86f, 0.18f, 0.18f, 1f), "HP 100/100");
        GameObject mpBar = CreateStatusBar(hudRoot.transform, "MpBar", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -144f), new Color(0.24f, 0.55f, 1f, 1f), "MP 100/100");
        GameObject shieldBar = CreateStatusBar(hudRoot.transform, "ShieldBar", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -192f), new Color(0.84f, 0.96f, 1f, 1f), "Shield 0/0");

        BindHud(
            hud,
            hudRoot,
            hpBar.transform.Find("Fill")?.GetComponent<Image>(),
            mpBar.transform.Find("Fill")?.GetComponent<Image>(),
            shieldBar.transform.Find("Fill")?.GetComponent<Image>(),
            hpBar.transform.Find("Label")?.GetComponent<TextMeshProUGUI>(),
            mpBar.transform.Find("Label")?.GetComponent<TextMeshProUGUI>(),
            shieldBar.transform.Find("Label")?.GetComponent<TextMeshProUGUI>(),
            switchHintText,
            runeHintText);

        Selection.activeGameObject = hudRoot;
        Debug.Log("[HUD] Built test PlayerStatusHUD under selected object.", selected);
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
        TextMeshProUGUI runeHintText)
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
        serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(hud);
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
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(360f, 72f);
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
