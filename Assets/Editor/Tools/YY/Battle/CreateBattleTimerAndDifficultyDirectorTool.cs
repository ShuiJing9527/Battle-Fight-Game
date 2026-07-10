using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class CreateBattleTimerAndDifficultyDirectorTool
{
    private const string MenuPath = "Tools/YY/Battle/Create Battle Timer And Difficulty Director";
    private const string DirectorObjectName = "EnemyDifficultyDirector";
    private const string TimerUiObjectName = "BattleTimerUI";
    private const string TimerTextObjectName = "BattleTimerText";

    [MenuItem(MenuPath)]
    public static void CreateBattleTimerAndDifficultyDirector()
    {
        EnemyDifficultyDirector director = Object.FindObjectOfType<EnemyDifficultyDirector>(true);
        if (director == null)
        {
            GameObject directorObject = new GameObject(DirectorObjectName);
            director = directorObject.AddComponent<EnemyDifficultyDirector>();
        }

        BattleTimerUI battleTimerUi = Object.FindObjectOfType<BattleTimerUI>(true);
        if (battleTimerUi == null)
        {
            GameObject timerUiObject = new GameObject(TimerUiObjectName);
            battleTimerUi = timerUiObject.AddComponent<BattleTimerUI>();
        }

        Canvas targetCanvas = ResolveTargetCanvas();
        if (targetCanvas == null)
        {
            Debug.LogWarning("[BattleSetup] No HUDCanvas or Canvas found. BattleTimerUI and EnemyDifficultyDirector were created, but BattleTimerText was not created.");
            Selection.activeGameObject = battleTimerUi.gameObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return;
        }

        TextMeshProUGUI timerText = EnsureBattleTimerText(targetCanvas.transform);
        BindTimerText(battleTimerUi, timerText);

        Selection.activeGameObject = timerText.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[BattleSetup] External BattleTimerText ready");
    }

    private static Canvas ResolveTargetCanvas()
    {
        GameObject hudCanvasObject = GameObject.Find("HUDCanvas");
        if (hudCanvasObject != null)
        {
            Canvas hudCanvas = hudCanvasObject.GetComponent<Canvas>();
            if (hudCanvas != null)
            {
                return hudCanvas;
            }
        }

        PlayerStatusHUD statusHud = Object.FindObjectOfType<PlayerStatusHUD>(true);
        if (statusHud != null)
        {
            Canvas statusCanvas = statusHud.GetComponentInParent<Canvas>();
            if (statusCanvas != null)
            {
                return statusCanvas;
            }
        }

        PlayerSkillHUD skillHud = Object.FindObjectOfType<PlayerSkillHUD>(true);
        if (skillHud != null)
        {
            Canvas skillCanvas = skillHud.GetComponentInParent<Canvas>();
            if (skillCanvas != null)
            {
                return skillCanvas;
            }
        }

        return Object.FindObjectOfType<Canvas>(true);
    }

    private static TextMeshProUGUI EnsureBattleTimerText(Transform parent)
    {
        Transform existing = parent.Find(TimerTextObjectName);
        GameObject textObject;
        if (existing != null)
        {
            textObject = existing.gameObject;
        }
        else
        {
            textObject = new GameObject(TimerTextObjectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            textObject.layer = parent.gameObject.layer;
        }

        RectTransform rect = textObject.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = textObject.AddComponent<RectTransform>();
        }

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -36f);
        rect.sizeDelta = new Vector2(500f, 60f);
        rect.localScale = Vector3.one;

        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
        {
            tmp = textObject.AddComponent<TextMeshProUGUI>();
        }

        tmp.text = "00:00";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 28f;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.color = new Color(0.92f, 0.97f, 1f, 1f);

        Outline outline = textObject.GetComponent<Outline>();
        if (outline == null)
        {
            outline = textObject.AddComponent<Outline>();
        }

        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        return tmp;
    }

    private static void BindTimerText(BattleTimerUI battleTimerUi, TextMeshProUGUI timerText)
    {
        if (battleTimerUi == null || timerText == null)
        {
            return;
        }

        SerializedObject serializedObject = new SerializedObject(battleTimerUi);
        SerializedProperty timerTextProperty = serializedObject.FindProperty("timerText");
        if (timerTextProperty != null)
        {
            timerTextProperty.objectReferenceValue = timerText;
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(battleTimerUi);
        EditorUtility.SetDirty(timerText);
    }
}
