using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CreatePlayerAttributePanelPrefab
{
    private const string PrefabPath = "Assets/Resources/Prefabs/UI/PlayerAttributePanel.prefab";

    [MenuItem("Tools/YY/UI/Create Player Attribute Panel Prefab")]
    public static void CreatePrefab()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Prefabs");
        EnsureFolder("Assets/Resources/Prefabs/UI");

        if (File.Exists(PrefabPath))
        {
            Debug.Log("[PlayerAttributePanel] Existing prefab found. It will be overwritten: " + PrefabPath);
            AssetDatabase.DeleteAsset(PrefabPath);
        }

        GameObject root = null;
        try
        {
            root = BuildPrefabRoot();
            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = prefabAsset;
            EditorGUIUtility.PingObject(prefabAsset);
            Debug.Log("[PlayerAttributePanel] Created prefab at " + PrefabPath);
        }
        finally
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    private static GameObject BuildPrefabRoot()
    {
        TMP_FontAsset fontAsset = ResolveFontAsset();

        GameObject root = CreateUIObject("PlayerAttributePanel");
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(600f, 320f);
        rootRect.anchoredPosition = Vector2.zero;

        root.AddComponent<CanvasGroup>();

        GameObject background = CreatePanel(root.transform, "Background", new Color(0.08f, 0.10f, 0.14f, 0.75f));
        Stretch(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject previewArea = CreateUIObject("LeftPreviewArea", root.transform);
        Stretch(previewArea.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(20f, 20f), new Vector2(180f, -20f));
        EnsureImage(previewArea, new Color(0.12f, 0.14f, 0.2f, 0.9f));

        TextMeshProUGUI playerNameText = CreateText(previewArea.transform, "PlayerNameText", "Player", fontAsset, 18f, TextAlignmentOptions.Center);
        Stretch(playerNameText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(12f, -44f), new Vector2(-12f, -12f));

        GameObject previewRoot = CreateUIObject("PreviewRoot", previewArea.transform);
        Stretch(previewRoot.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(16f, 44f), new Vector2(-16f, -48f));

        TextMeshProUGUI previewText = CreateText(previewArea.transform, "CharacterPreviewText", "Character Preview", fontAsset, 20f, TextAlignmentOptions.Center);
        previewText.enableWordWrapping = true;
        Stretch(previewText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 12f), new Vector2(-16f, 36f));

        GameObject attributeArea = CreateUIObject("AttributeArea", root.transform);
        Stretch(attributeArea.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(220f, -20f), new Vector2(-20f, -20f));

        TextMeshProUGUI titleText = CreateText(attributeArea.transform, "TitleText", "Player Attributes", fontAsset, 20f, TextAlignmentOptions.MidlineLeft);
        Stretch(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(0f, 0f));

        string[] rowKeys = { "HP", "ATK", "DEF", "MAG", "RES", "SPD" };
        string[] rowValues = { "100/100", "0", "0", "0", "0", "0" };
        float rowTop = 44f;
        float rowHeight = 28f;
        float rowGap = 10f;

        for (int i = 0; i < rowKeys.Length; i++)
        {
            GameObject row = CreateAttributeRow(attributeArea.transform, rowKeys[i], rowValues[i], fontAsset);
            float top = rowTop + (rowHeight + rowGap) * i;
            Stretch(row.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -top - rowHeight), new Vector2(0f, -top));
        }

        GameObject subInfoArea = CreateUIObject("SubInfoArea", root.transform);
        Stretch(subInfoArea.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(220f, 52f), new Vector2(-20f, 142f));

        CreateSubInfoText(subInfoArea.transform, "LUCKText", "LUCK 0", fontAsset, 0);
        CreateSubInfoText(subInfoArea.transform, "CritRateText", "Crit Rate 0%", fontAsset, 1);
        CreateSubInfoText(subInfoArea.transform, "ExtraSoulDropText", "Extra Soul Drop 0%", fontAsset, 2);
        CreateSubInfoText(subInfoArea.transform, "ExtraRuneDropText", "Extra Rune Drop 0%", fontAsset, 3);

        GameObject reserveArea = CreateUIObject("ReserveArea", root.transform);
        Stretch(reserveArea.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(220f, 14f), new Vector2(-20f, 42f));

        TextMeshProUGUI reserveText = CreateText(reserveArea.transform, "ReserveText", "Buff / Rune / Skill Info Reserved", fontAsset, 13f, TextAlignmentOptions.MidlineLeft);
        reserveText.enableWordWrapping = true;
        Stretch(reserveText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

        SetLayerRecursively(root, 5);
        return root;
    }

    private static GameObject CreateAttributeRow(Transform parent, string key, string value, TMP_FontAsset fontAsset)
    {
        GameObject row = CreateUIObject(key + "Row", parent);

        TextMeshProUGUI labelText = CreateText(row.transform, "LabelText", key + ":", fontAsset, 15f, TextAlignmentOptions.MidlineLeft);
        Stretch(labelText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(54f, 0f));

        TextMeshProUGUI valueText = CreateText(row.transform, "ValueText", value, fontAsset, 15f, TextAlignmentOptions.MidlineRight);
        Stretch(valueText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-96f, 0f), new Vector2(0f, 0f));

        GameObject barBackground = CreatePanel(row.transform, "BarBackground", new Color(0.16f, 0.18f, 0.24f, 1f));
        Stretch(barBackground.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(70f, -6f), new Vector2(286f, 6f));

        GameObject baseFill = CreatePanel(row.transform, "BaseFill", ResolveBaseFillColor(key));
        Stretch(baseFill.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(70f, 0f), new Vector2(70f, 0f));

        GameObject bonusFill = CreatePanel(row.transform, "BonusFill", new Color(1.00f, 0.83f, 0.29f, 1.00f));
        Stretch(bonusFill.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(70f, 0f), new Vector2(70f, 0f));

        return row;
    }

    private static Color ResolveBaseFillColor(string key)
    {
        switch (key)
        {
            case "HP":
                return new Color32(0x6C, 0xCB, 0x5F, 0xFF);
            case "ATK":
                return new Color32(0xD9, 0x53, 0x4F, 0xFF);
            case "DEF":
                return new Color32(0xE4, 0x9B, 0x3E, 0xFF);
            case "MAG":
                return new Color32(0x8E, 0x63, 0xD9, 0xFF);
            case "RES":
                return new Color32(0x5B, 0x8C, 0xFF, 0xFF);
            case "SPD":
                return new Color(0.75f, 0.94f, 1.00f, 1.00f);
            default:
                return new Color(0.92f, 0.76f, 0.30f, 1f);
        }
    }

    private static void CreateSubInfoText(Transform parent, string name, string value, TMP_FontAsset fontAsset, int lineIndex)
    {
        TextMeshProUGUI text = CreateText(parent, name, value, fontAsset, 13f, TextAlignmentOptions.MidlineLeft);
        float top = 18f + 22f * lineIndex;
        Stretch(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -top - 18f), new Vector2(0f, -top));
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject panel = CreateUIObject(name, parent);
        EnsureImage(panel, color);
        return panel;
    }

    private static Image EnsureImage(GameObject gameObject, Color color)
    {
        Image image = gameObject.GetComponent<Image>();
        if (image == null)
        {
            image = gameObject.AddComponent<Image>();
        }

        image.color = color;
        return image;
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
        tmp.enableWordWrapping = false;
        if (fontAsset != null)
        {
            tmp.font = fontAsset;
        }

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

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
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

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
    }
}
