using System.IO;
using UnityEditor;
using UnityEngine;

public static class CreatePlayer01ScissorEffectPrefabs
{
    private const string RootFolder = "Assets/Resources/Prefabs/Effects/Player01";
    private const string CutPrefabPath = RootFolder + "/Player01ScissorCutEffect.prefab";
    private const string SlashWavePrefabPath = RootFolder + "/Player01ScissorSlashWaveEffect.prefab";

    [MenuItem("Tools/YY/Effects/Create Player01 Scissor Effect Prefabs")]
    public static void CreatePrefabs()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Prefabs");
        EnsureFolder("Assets/Resources/Prefabs/Effects");
        EnsureFolder(RootFolder);

        CreateOrReplacePrefab(CutPrefabPath, "Player01ScissorCutEffect", 0.14f, 60);
        CreateOrReplacePrefab(SlashWavePrefabPath, "Player01ScissorSlashWaveEffect", 0.16f, 55);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Player01 Scissor Effects] Prefabs created. Assign Sprite frames manually in Inspector.");
    }

    private static void CreateOrReplacePrefab(string prefabPath, string rootName, float lifetime, int sortingOrder)
    {
        if (File.Exists(prefabPath))
        {
            Debug.Log("[Player01 Scissor Effects] Existing prefab found. It will be overwritten: " + prefabPath);
            AssetDatabase.DeleteAsset(prefabPath);
        }

        GameObject root = null;
        try
        {
            root = new GameObject(rootName);
            SpriteRenderer spriteRenderer = root.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = sortingOrder;

            ScissorFrameEffectPlayer effectPlayer = root.AddComponent<ScissorFrameEffectPlayer>();
            effectPlayer.SetLifetime(lifetime);
            effectPlayer.SetSortingOrder(sortingOrder);

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            EditorGUIUtility.PingObject(prefabAsset);
        }
        finally
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
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
}
