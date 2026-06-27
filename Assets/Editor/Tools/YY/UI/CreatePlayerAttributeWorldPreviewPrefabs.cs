using System.IO;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CreatePlayerAttributeWorldPreviewPrefabs
{
    private const string Player01SourcePath = "Assets/Prefabs/Player/Player01.prefab";
    private const string Player02SourcePath = "Assets/Prefabs/Player/Player02.prefab";
    private const string OutputFolder = "Assets/Resources/Prefabs/UI/Preview";
    private const string Player01OutputPath = OutputFolder + "/Player01AttributeWorldPreview.prefab";
    private const string Player02OutputPath = OutputFolder + "/Player02AttributeWorldPreview.prefab";

    [MenuItem("Tools/YY/UI/Create Player Attribute World Preview Prefabs")]
    public static void CreateWorldPreviewPrefabs()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Prefabs");
        EnsureFolder("Assets/Resources/Prefabs/UI");
        EnsureFolder(OutputFolder);

        CreateWorldPreviewPrefab(Player01SourcePath, Player01OutputPath, "Player01AttributeWorldPreview");
        CreateWorldPreviewPrefab(Player02SourcePath, Player02OutputPath, "Player02AttributeWorldPreview");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PlayerAttributeWorldPreview] Finished creating world preview prefabs.");
    }

    private static void CreateWorldPreviewPrefab(string sourcePath, string outputPath, string rootName)
    {
        if (File.Exists(outputPath))
        {
            Debug.Log("[PlayerAttributeWorldPreview] Preview prefab already exists and will not be overwritten: " + outputPath);
            return;
        }

        GameObject sourceRoot = PrefabUtility.LoadPrefabContents(sourcePath);
        if (sourceRoot == null)
        {
            Debug.LogWarning("[PlayerAttributeWorldPreview] Could not load source prefab: " + sourcePath);
            return;
        }

        GameObject previewRoot = null;
        try
        {
            SkeletonAnimation sourceSkeleton = sourceRoot.GetComponentInChildren<SkeletonAnimation>(true);
            if (sourceSkeleton == null || sourceSkeleton.skeletonDataAsset == null)
            {
                Debug.LogWarning("[PlayerAttributeWorldPreview] No valid SkeletonAnimation found in: " + sourcePath);
                return;
            }

            previewRoot = new GameObject(rootName);
            previewRoot.transform.position = Vector3.zero;
            previewRoot.transform.rotation = Quaternion.identity;
            previewRoot.transform.localScale = Vector3.one;

            GameObject visualClone = Object.Instantiate(sourceSkeleton.gameObject);
            visualClone.name = sourceSkeleton.gameObject.name;
            visualClone.transform.SetParent(previewRoot.transform, false);
            visualClone.transform.localPosition = sourceSkeleton.transform.localPosition;
            visualClone.transform.localRotation = sourceSkeleton.transform.localRotation;
            visualClone.transform.localScale = sourceSkeleton.transform.localScale;

            StripNonVisualComponents(previewRoot.transform);

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(previewRoot, outputPath);
            if (prefabAsset != null)
            {
                Debug.Log("[PlayerAttributeWorldPreview] Created world preview prefab: " + outputPath);
            }
        }
        finally
        {
            if (previewRoot != null)
            {
                Object.DestroyImmediate(previewRoot);
            }

            PrefabUtility.UnloadPrefabContents(sourceRoot);
        }
    }

    private static void StripNonVisualComponents(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Component[] components = root.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || component is Transform)
            {
                continue;
            }

            if (IsAllowedVisualComponent(component))
            {
                continue;
            }

            Object.DestroyImmediate(component);
        }

        for (int i = 0; i < root.childCount; i++)
        {
            StripNonVisualComponents(root.GetChild(i));
        }
    }

    private static bool IsAllowedVisualComponent(Component component)
    {
        return component is SkeletonAnimation ||
               component is SkeletonRenderer ||
               component is MeshRenderer ||
               component is MeshFilter ||
               component is SortingGroup;
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
