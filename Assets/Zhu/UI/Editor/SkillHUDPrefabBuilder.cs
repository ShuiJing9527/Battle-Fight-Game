using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SkillHUDPrefabBuilder
{
    private const string PrefabFolder = "Assets/Zhu/UI/Prefabs";
    private const string SkillSlotPrefabPath = PrefabFolder + "/SkillSlotHUD.prefab";
    private const string SkillRootPrefabPath = PrefabFolder + "/SkillHUDRoot.prefab";
    private static readonly string[] CircleSpriteCandidatePaths =
    {
        "Assets/Textures/Effects/T_SoulSoftCircle.png",
        "Assets/素材/Effects/T_SoftCircleParticle.png"
    };

    [MenuItem("Tools/Zhu UI/Build Skill HUD Prefab")]
    public static void BuildSkillHudPrefab()
    {
        EnsureFolder(PrefabFolder);

        if (!EditorUtility.DisplayDialog(
                "Build Skill HUD Prefab",
                "Create or overwrite SkillSlotHUD.prefab and SkillHUDRoot.prefab?",
                "Build",
                "Cancel"))
        {
            return;
        }

        BuildSkillSlotPrefab();
        BuildSkillRootPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Object createdPrefab = AssetDatabase.LoadAssetAtPath<Object>(SkillSlotPrefabPath);
        Selection.activeObject = createdPrefab;

        EditorUtility.DisplayDialog(
            "Build Skill HUD Prefab",
            "Skill HUD prefabs created. You can now replace the Icon image or save further prefab edits manually.",
            "OK");
    }

    private static void BuildSkillSlotPrefab()
    {
        GameObject root = new GameObject("SkillSlotHUD", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(80f, 80f);
        rootRect.localScale = Vector3.one;

        GameObject background = CreateUiChild(root.transform, "Background");
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        Stretch(backgroundRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.08f, 0.1f, 0.14f, 0.92f);
        backgroundImage.sprite = ResolveCircleSprite();
        backgroundImage.preserveAspect = true;

        GameObject icon = CreateUiChild(root.transform, "Icon");
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        Stretch(iconRect, new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Vector2.zero, Vector2.zero);
        Image iconImage = icon.AddComponent<Image>();
        iconImage.color = new Color(0.85f, 0.87f, 0.92f, 1f);
        iconImage.sprite = ResolveCircleSprite();
        iconImage.preserveAspect = true;

        GameObject cooldown = CreateUiChild(root.transform, "CooldownOverlay");
        RectTransform cooldownRect = cooldown.GetComponent<RectTransform>();
        Stretch(cooldownRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image cooldownImage = cooldown.AddComponent<Image>();
        cooldownImage.color = new Color(0f, 0f, 0f, 0f);
        cooldownImage.sprite = ResolveCircleSprite();
        cooldownImage.type = Image.Type.Filled;
        cooldownImage.fillMethod = Image.FillMethod.Radial360;
        cooldownImage.fillOrigin = (int)Image.Origin360.Top;
        cooldownImage.fillClockwise = false;
        cooldownImage.fillAmount = 0f;
        cooldownImage.raycastTarget = false;

        GameObject keyLabel = CreateUiChild(root.transform, "KeyLabel");
        RectTransform keyLabelRect = keyLabel.GetComponent<RectTransform>();
        keyLabelRect.anchorMin = new Vector2(0f, 1f);
        keyLabelRect.anchorMax = new Vector2(0f, 1f);
        keyLabelRect.pivot = new Vector2(0f, 1f);
        keyLabelRect.anchoredPosition = new Vector2(8f, -6f);
        keyLabelRect.sizeDelta = new Vector2(32f, 20f);
        Text keyText = keyLabel.AddComponent<Text>();
        keyText.text = "Q";
        keyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        keyText.fontSize = 20;
        keyText.color = Color.white;
        keyText.alignment = TextAnchor.UpperLeft;
        keyText.raycastTarget = false;
        keyText.horizontalOverflow = HorizontalWrapMode.Overflow;
        keyText.verticalOverflow = VerticalWrapMode.Overflow;

        SavePrefab(root, SkillSlotPrefabPath);
    }

    private static void BuildSkillRootPrefab()
    {
        GameObject root = new GameObject("SkillHUDRoot", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(1f, 0f);
        rootRect.anchoredPosition = new Vector2(-80f, 70f);
        rootRect.sizeDelta = new Vector2(368f, 80f);
        rootRect.localScale = Vector3.one;

        for (int i = 0; i < 4; i++)
        {
            string key = i switch
            {
                0 => "Q",
                1 => "W",
                2 => "E",
                _ => "R"
            };

            GameObject slot = CreateUiChild(root.transform, $"SkillSlot_{key}");
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(1f, 0f);
            slotRect.anchorMax = new Vector2(1f, 0f);
            slotRect.pivot = new Vector2(1f, 0f);
            slotRect.anchoredPosition = new Vector2(-((80f + 16f) * (3 - i)), 0f);
            slotRect.sizeDelta = new Vector2(80f, 80f);

            Image background = CreateUiChild(slot.transform, "Background").AddComponent<Image>();
            background.color = new Color(0.08f, 0.1f, 0.14f, 0.92f);
            background.sprite = ResolveCircleSprite();
            background.preserveAspect = true;

            Image icon = CreateUiChild(slot.transform, "Icon").AddComponent<Image>();
            icon.color = new Color(0.85f, 0.87f, 0.92f, 1f);
            icon.sprite = ResolveCircleSprite();
            icon.preserveAspect = true;

            Image overlay = CreateUiChild(slot.transform, "CooldownOverlay").AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0f);
            overlay.sprite = ResolveCircleSprite();
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Radial360;
            overlay.fillOrigin = (int)Image.Origin360.Top;
            overlay.fillClockwise = false;
            overlay.fillAmount = 0f;

            GameObject labelObject = CreateUiChild(slot.transform, "KeyLabel");
            Text text = labelObject.AddComponent<Text>();
            text.text = key;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.raycastTarget = false;

            Stretch(slot.transform.Find("Background").GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Stretch(slot.transform.Find("Icon").GetComponent<RectTransform>(), new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Vector2.zero, Vector2.zero);
            Stretch(slot.transform.Find("CooldownOverlay").GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.anchoredPosition = new Vector2(8f, -6f);
            labelRect.sizeDelta = new Vector2(32f, 20f);
        }

        SavePrefab(root, SkillRootPrefabPath);
    }

    private static void SavePrefab(GameObject root, string assetPath)
    {
        try
        {
            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateUiChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static Sprite ResolveCircleSprite()
    {
        for (int i = 0; i < CircleSpriteCandidatePaths.Length; i++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpriteCandidatePaths[i]);
            if (sprite != null)
            {
                return sprite;
            }
        }

        return null;
    }
}
