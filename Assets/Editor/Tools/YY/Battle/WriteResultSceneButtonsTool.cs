using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class WriteResultSceneButtonsTool
{
    private const string MenuPath = "Tools/YY/Battle/Write Result Scene Buttons";
    private const string GameWinScenePath = "Assets/Zhu/gamewin.unity";
    private const string GameOverScenePath = "Assets/Zhu/gameover.unity";
    private const string TitleScenePath = "Assets/Zhu/GameScene.unity";
    private const string BattleScenePath = "Assets/Scenes/草原.unity";

    private const string LoaderObjectName = "SceneButtonLoader";
    private const string RetryButtonName = "RetryButton";
    private const string BackButtonName = "BackToTitleButton";

    [MenuItem(MenuPath)]
    public static void WriteResultSceneButtons()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene previousScene = SceneManager.GetActiveScene();
        string previousScenePath = previousScene.path;

        SetupResultScene(GameWinScenePath);
        SetupResultScene(GameOverScenePath);
        EnsureBuildSettings();

        if (!string.IsNullOrWhiteSpace(previousScenePath))
        {
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
        }

        Debug.Log("[ResultSceneButtons] gamewin and gameover buttons are written into scene hierarchy.");
    }

    private static void SetupResultScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Canvas canvas = FindSceneObject<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning($"[ResultSceneButtons] No Canvas found in {scenePath}.");
            return;
        }

        SceneButtonLoader loader = EnsureSceneButtonLoader(scene);
        EnsureButton(canvas.transform, loader, RetryButtonName, "Restart", new Vector2(1f, 0f), new Vector2(-260f, 84f), BattleSceneResultRouter.BattleSceneName);
        EnsureButton(canvas.transform, loader, BackButtonName, "Main Menu", new Vector2(1f, 0f), new Vector2(-20f, 84f), BattleSceneResultRouter.TitleSceneName);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static SceneButtonLoader EnsureSceneButtonLoader(Scene scene)
    {
        SceneButtonLoader loader = FindSceneObject<SceneButtonLoader>();
        if (loader != null)
        {
            return loader;
        }

        GameObject loaderObject = new GameObject(LoaderObjectName);
        SceneManager.MoveGameObjectToScene(loaderObject, scene);
        return loaderObject.AddComponent<SceneButtonLoader>();
    }

    private static void EnsureButton(
        Transform parent,
        SceneButtonLoader loader,
        string objectName,
        string labelText,
        Vector2 anchor,
        Vector2 anchoredPosition,
        string targetSceneName)
    {
        Transform existing = parent.Find(objectName);
        GameObject buttonObject = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(220f, 60f);
        rect.localScale = Vector3.one;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.72f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;

        TextMeshProUGUI label = EnsureLabel(buttonObject.transform);
        label.text = labelText;

        ConfigureButtonClick(button, loader, targetSceneName);
    }

    private static TextMeshProUGUI EnsureLabel(Transform parent)
    {
        Transform existing = parent.Find("Label");
        GameObject labelObject = existing != null ? existing.gameObject : new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);

        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 30f;
        text.color = Color.white;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }

    private static void ConfigureButtonClick(Button button, SceneButtonLoader loader, string targetSceneName)
    {
        button.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddStringPersistentListener(button.onClick, loader.LoadSceneByName, targetSceneName);
        EditorUtility.SetDirty(button);
    }

    private static T FindSceneObject<T>() where T : Object
    {
        T[] allObjects = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            T candidate = allObjects[i];
            if (candidate is Component component && component.gameObject.scene.IsValid())
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
