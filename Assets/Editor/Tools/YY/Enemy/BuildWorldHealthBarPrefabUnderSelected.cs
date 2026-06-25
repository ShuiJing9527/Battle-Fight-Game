using UnityEditor;
using UnityEngine;

public static class BuildWorldHealthBarPrefabUnderSelected
{
    private const string MenuPath = "Tools/YY/Enemy/Build World Health Bar Prefab Under Selected";
    private const int BackgroundSortingOrder = 200;
    private const int FillSortingOrder = 201;

    private static Sprite whiteSprite;

    [MenuItem(MenuPath)]
    private static void Build()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Build World Health Bar", "Select a root object first.", "OK");
            return;
        }

        bool createdAny = false;
        createdAny |= EnsureBarPart(selected.transform, "Background", new Color(0.08f, 0.08f, 0.08f, 0.9f), BackgroundSortingOrder);
        createdAny |= EnsureBarPart(selected.transform, "Fill", new Color(0.85f, 0.15f, 0.12f, 0.95f), FillSortingOrder);

        if (createdAny)
        {
            EditorUtility.DisplayDialog(
                "Build World Health Bar",
                "WorldHealthBar prefab children created under the selected object. You can now save it as a prefab manually.",
                "OK");
            return;
        }

        EditorUtility.DisplayDialog(
            "Build World Health Bar",
            "Background / Fill already exist under the selected object. Nothing was changed.",
            "OK");
    }

    private static bool EnsureBarPart(Transform root, string childName, Color color, int sortingOrder)
    {
        Transform child = root.Find(childName);
        if (child != null)
        {
            return false;
        }

        GameObject childObject = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(childObject, $"Create {childName}");
        childObject.transform.SetParent(root, false);

        SpriteRenderer renderer = childObject.AddComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        if (childName == "Background")
        {
            childObject.transform.localScale = new Vector3(1.4f, 0.12f, 1f);
        }
        else
        {
            childObject.transform.localScale = new Vector3(1.4f, 0.12f, 1f);
            childObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        }

        EditorUtility.SetDirty(childObject);
        return true;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
        {
            return whiteSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "EditorWorldHealthBarWhite";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        whiteSprite.name = "EditorWorldHealthBarWhiteSprite";
        return whiteSprite;
    }
}
