using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AddOrRepairCharacterSwitchWarningPopup
{
    private const string PopupName = "CharacterSwitchWarningPopup";
    private const string BackgroundName = "Background";
    private const string MessageTextName = "MessageText";

    [MenuItem("Tools/YY/HUD/Add Or Repair Character Switch Warning Popup")]
    public static void AddOrRepair()
    {
        GameObject selected = Selection.activeGameObject;
        Canvas canvas = ResolveCanvas(selected);
        if (canvas == null)
        {
            EditorUtility.DisplayDialog(
                "Character Switch Warning Popup",
                "Please select a Canvas, PlayerStatusHUD, or a child object under the target HUD Canvas.",
                "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        GameObject popup = FindOrCreateChild(canvas.transform, PopupName, typeof(RectTransform), typeof(CanvasGroup), typeof(CharacterSwitchWarningPopup));
        RectTransform popupRect = popup.GetComponent<RectTransform>();
        if (popupRect != null && popup.transform.parent == canvas.transform)
        {
            Undo.RecordObject(popupRect, "Configure Character Switch Warning Popup");
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.pivot = new Vector2(0.5f, 0.5f);
            if (popupRect.sizeDelta == Vector2.zero)
            {
                popupRect.sizeDelta = new Vector2(720f, 150f);
            }
            if (popupRect.anchoredPosition == Vector2.zero)
            {
                popupRect.anchoredPosition = new Vector2(0f, 260f);
            }
        }

        CanvasGroup group = popup.GetComponent<CanvasGroup>();
        Undo.RecordObject(group, "Configure Character Switch Warning Popup");
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject background = FindOrCreateChild(popup.transform, BackgroundName, typeof(RectTransform), typeof(Image), typeof(Outline));
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        Undo.RecordObject(backgroundRect, "Configure Character Switch Warning Background");
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image backgroundImage = background.GetComponent<Image>();
        Undo.RecordObject(backgroundImage, "Configure Character Switch Warning Background");
        backgroundImage.color = new Color(0.02f, 0.08f, 0.16f, 0.88f);
        backgroundImage.raycastTarget = false;

        Outline outline = background.GetComponent<Outline>();
        Undo.RecordObject(outline, "Configure Character Switch Warning Outline");
        outline.effectColor = new Color(0.75f, 0.95f, 1f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject message = FindOrCreateChild(popup.transform, MessageTextName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform messageRect = message.GetComponent<RectTransform>();
        Undo.RecordObject(messageRect, "Configure Character Switch Warning Text");
        messageRect.anchorMin = Vector2.zero;
        messageRect.anchorMax = Vector2.one;
        messageRect.offsetMin = new Vector2(32f, 18f);
        messageRect.offsetMax = new Vector2(-32f, -18f);

        TextMeshProUGUI text = message.GetComponent<TextMeshProUGUI>();
        Undo.RecordObject(text, "Configure Character Switch Warning Text");
        text.text = "能量槽未满，无法切换角色。";
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.enableAutoSizing = true;
        text.fontSize = 30f;
        text.fontSizeMax = 30f;
        text.fontSizeMin = 20f;
        text.margin = new Vector4(8f, 0f, 8f, 0f);
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        CharacterSwitchWarningPopup popupComponent = popup.GetComponent<CharacterSwitchWarningPopup>();
        EditorUtility.SetDirty(popupComponent);
        EditorUtility.SetDirty(popup);
        EditorUtility.SetDirty(canvas);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);

        Selection.activeGameObject = popup;
        Debug.Log("[CharacterSwitchWarningPopup] Created/Reused external popup. You can now adjust its RectTransform position and size in the Inspector.", popup);
    }

    private static Canvas ResolveCanvas(GameObject selected)
    {
        if (selected == null)
        {
            return Object.FindObjectOfType<Canvas>();
        }

        Canvas canvas = selected.GetComponent<Canvas>();
        if (canvas != null)
        {
            return canvas;
        }

        canvas = selected.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            return canvas;
        }

        PlayerStatusHUD hud = selected.GetComponent<PlayerStatusHUD>();
        if (hud != null)
        {
            return selected.GetComponentInParent<Canvas>();
        }

        return Object.FindObjectOfType<Canvas>();
    }

    private static GameObject FindOrCreateChild(Transform parent, string name, params System.Type[] componentTypes)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject child = new GameObject(name, componentTypes);
        Undo.RegisterCreatedObjectUndo(child, "Create " + name);
        child.transform.SetParent(parent, false);
        return child;
    }
}
