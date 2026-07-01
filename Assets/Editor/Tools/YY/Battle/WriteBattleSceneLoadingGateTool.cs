using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class WriteBattleSceneLoadingGateTool
{
    private const string MenuPath = "Tools/YY/Battle/Write Battle Scene Loading Gate";
    private const string BattleScenePath = "Assets/Scenes/草原.unity";
    private const string TitleScenePath = "Assets/Zhu/GameScene.unity";
    private const string GameWinScenePath = "Assets/Zhu/gamewin.unity";
    private const string GameOverScenePath = "Assets/Zhu/gameover.unity";
    private const string GateRootName = "BattleSceneLoadingGate";
    private const string PanelName = "BattleLoadingPanel";
    private const string FillName = "ProgressFill";
    private const string TextName = "LoadingText";

    [MenuItem(MenuPath)]
    public static void WriteBattleSceneLoadingGate()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene previousScene = SceneManager.GetActiveScene();
        string previousScenePath = previousScene.path;

        Scene battleScene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
        Canvas canvas = FindSceneObjectByName("HUDCanvas")?.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = Object.FindObjectOfType<Canvas>();
        }

        if (canvas == null)
        {
            Debug.LogWarning("[BattleSceneLoadingGate] No Canvas/HUDCanvas found in battle scene.");
            return;
        }

        GameObject gateObject = FindOrCreateChild(canvas.transform, GateRootName);
        BattleSceneLoadingGate gate = gateObject.GetComponent<BattleSceneLoadingGate>();
        if (gate == null)
        {
            gate = gateObject.AddComponent<BattleSceneLoadingGate>();
        }

        GameObject panelObject = FindOrCreateChild(gateObject.transform, PanelName);
        RectTransform panelRect = EnsureRectTransform(panelObject);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelRect.localScale = Vector3.one;

        Image panelImage = panelObject.GetComponent<Image>();
        if (panelImage == null)
        {
            panelImage = panelObject.AddComponent<Image>();
        }
        panelImage.color = new Color(0.03f, 0.04f, 0.06f, 0.96f);

        CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = panelObject.AddComponent<CanvasGroup>();
        }

        GameObject barRoot = FindOrCreateChild(panelObject.transform, "ProgressBar");
        RectTransform barRect = EnsureRectTransform(barRoot);
        barRect.anchorMin = new Vector2(0.5f, 0.5f);
        barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.pivot = new Vector2(0.5f, 0.5f);
        barRect.anchoredPosition = new Vector2(0f, -24f);
        barRect.sizeDelta = new Vector2(420f, 36f);

        Image barBackground = barRoot.GetComponent<Image>();
        if (barBackground == null)
        {
            barBackground = barRoot.AddComponent<Image>();
        }
        barBackground.color = new Color(1f, 1f, 1f, 0.12f);

        GameObject fillObject = FindOrCreateChild(barRoot.transform, FillName);
        RectTransform fillRect = EnsureRectTransform(fillObject);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.localScale = Vector3.one;

        Image fillImage = fillObject.GetComponent<Image>();
        if (fillImage == null)
        {
            fillImage = fillObject.AddComponent<Image>();
        }
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;
        fillImage.color = new Color(0.54f, 0.87f, 1f, 1f);

        GameObject textObject = FindOrCreateChild(panelObject.transform, TextName);
        RectTransform textRect = EnsureRectTransform(textObject);
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, 28f);
        textRect.sizeDelta = new Vector2(420f, 48f);

        TextMeshProUGUI loadingText = textObject.GetComponent<TextMeshProUGUI>();
        if (loadingText == null)
        {
            loadingText = textObject.AddComponent<TextMeshProUGUI>();
        }
        loadingText.text = "Loading 0%";
        loadingText.alignment = TextAlignmentOptions.Center;
        loadingText.fontSize = 30f;
        loadingText.color = Color.white;
        loadingText.raycastTarget = false;

        SerializedObject serializedGate = new SerializedObject(gate);
        serializedGate.FindProperty("loadingCanvasGroup").objectReferenceValue = canvasGroup;
        serializedGate.FindProperty("progressFillImage").objectReferenceValue = fillImage;
        serializedGate.FindProperty("loadingText").objectReferenceValue = loadingText;
        serializedGate.FindProperty("loadingRoot").objectReferenceValue = panelObject;
        serializedGate.FindProperty("difficultyDirector").objectReferenceValue = Object.FindObjectOfType<EnemyDifficultyDirector>();
        serializedGate.FindProperty("enemySpawner").objectReferenceValue = Object.FindObjectOfType<EnemySpawner>();
        serializedGate.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(gate);
        EditorSceneManager.MarkSceneDirty(battleScene);
        EditorSceneManager.SaveScene(battleScene);
        EnsureBuildSettings();

        if (!string.IsNullOrWhiteSpace(previousScenePath))
        {
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
        }

        Debug.Log("[BattleSceneLoadingGate] Battle loading panel written into 草原 scene hierarchy.");
    }

    private static GameObject FindOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child;
    }

    private static RectTransform EnsureRectTransform(GameObject target)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = target.AddComponent<RectTransform>();
        }

        return rect;
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];
            if (candidate == null || !candidate.scene.IsValid())
            {
                continue;
            }

            if (candidate.name == objectName)
            {
                return candidate;
            }
        }

        return null;
    }

    private static void EnsureBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        EnsureSceneEnabled(scenes, TitleScenePath);
        EnsureSceneEnabled(scenes, BattleScenePath);
        EnsureSceneEnabled(scenes, GameWinScenePath);
        EnsureSceneEnabled(scenes, GameOverScenePath);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void EnsureSceneEnabled(List<EditorBuildSettingsScene> scenes, string path)
    {
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == path)
            {
                scenes[i].enabled = true;
                return;
            }
        }

        scenes.Add(new EditorBuildSettingsScene(path, true));
    }
}
