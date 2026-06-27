using System.IO;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

public static class CreatePlayerAttributePreviewPrefabs
{
    private const string Player01SourcePath = "Assets/Prefabs/Player/Player01.prefab";
    private const string Player02SourcePath = "Assets/Prefabs/Player/Player02.prefab";
    private const string OutputFolder = "Assets/Resources/Prefabs/UI/Preview";
    private const string Player01OutputPath = OutputFolder + "/Player01AttributeUIPreview.prefab";
    private const string Player02OutputPath = OutputFolder + "/Player02AttributeUIPreview.prefab";
    private const string SkeletonGraphicMaterialPath = "Assets/Spine/Runtime/spine-unity/Materials/SkeletonGraphicDefault.mat";

    [MenuItem("Tools/YY/UI/Create Player Attribute Preview Prefabs")]
    public static void CreatePreviewPrefabs()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Prefabs");
        EnsureFolder("Assets/Resources/Prefabs/UI");
        EnsureFolder(OutputFolder);

        CreateUiPreviewPrefab(Player01SourcePath, Player01OutputPath, "Player01AttributeUIPreview", "Idle");
        EnsureManualPlayer02PreviewPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[PlayerAttributePreview] Finished creating SkeletonGraphic UI preview prefabs. Drag them onto PlayerAttributePanelUI manually.");
    }

    private static void CreateUiPreviewPrefab(string sourcePath, string outputPath, string rootName, string preferredIdleAnimation)
    {
        GameObject sourceRoot = PrefabUtility.LoadPrefabContents(sourcePath);
        if (sourceRoot == null)
        {
            Debug.LogWarning("[PlayerAttributePreview] Could not load source prefab: " + sourcePath);
            return;
        }

        GameObject previewRoot = null;
        try
        {
            SkeletonAnimation sourceSkeleton = sourceRoot.GetComponentInChildren<SkeletonAnimation>(true);
            if (sourceSkeleton == null || sourceSkeleton.skeletonDataAsset == null)
            {
                Debug.LogWarning("[PlayerAttributePreview] No valid SkeletonAnimation with SkeletonDataAsset found in: " + sourcePath);
                return;
            }

            Transform sourceTransform = sourceSkeleton.transform;

            previewRoot = new GameObject(rootName, typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform rect = previewRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(180f, 260f);
            rect.localScale = Vector3.one;

            Material skeletonGraphicMaterial = AssetDatabase.LoadAssetAtPath<Material>(SkeletonGraphicMaterialPath);
            SkeletonGraphic skeletonGraphic = SkeletonGraphic.AddSkeletonGraphicComponent(
                previewRoot,
                sourceSkeleton.skeletonDataAsset,
                skeletonGraphicMaterial);

            skeletonGraphic.initialSkinName = sourceSkeleton.initialSkinName;
            skeletonGraphic.startingLoop = true;
            skeletonGraphic.startingAnimation = ResolvePreviewAnimationName(sourceSkeleton, preferredIdleAnimation);
            skeletonGraphic.timeScale = sourceSkeleton.timeScale;
            skeletonGraphic.initialFlipX = sourceSkeleton.initialFlipX;
            skeletonGraphic.initialFlipY = sourceSkeleton.initialFlipY;
            skeletonGraphic.raycastTarget = false;
            skeletonGraphic.Initialize(true);

            if (!string.IsNullOrEmpty(skeletonGraphic.initialSkinName) && skeletonGraphic.Skeleton != null)
            {
                skeletonGraphic.Skeleton.SetSkin(skeletonGraphic.initialSkinName);
                skeletonGraphic.Skeleton.SetSlotsToSetupPose();
            }

            if (skeletonGraphic.Skeleton != null && skeletonGraphic.AnimationState != null)
            {
                string animationName = skeletonGraphic.startingAnimation;
                if (!string.IsNullOrEmpty(animationName))
                {
                    skeletonGraphic.AnimationState.SetAnimation(0, animationName, true);
                }

                skeletonGraphic.AnimationState.Apply(skeletonGraphic.Skeleton);
                skeletonGraphic.Skeleton.UpdateWorldTransform();
                skeletonGraphic.UpdateMesh();
            }

            ApplySourceVisualHint(rect, sourceTransform);

            if (File.Exists(outputPath))
            {
                Debug.Log("[PlayerAttributePreview] Overwriting existing UI preview prefab: " + outputPath);
                AssetDatabase.DeleteAsset(outputPath);
            }

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(previewRoot, outputPath);
            if (prefabAsset != null)
            {
                Debug.Log("[PlayerAttributePreview] Created UI preview prefab: " + outputPath);
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

    private static string ResolvePreviewAnimationName(SkeletonAnimation sourceSkeleton, string preferredIdleAnimation)
    {
        if (sourceSkeleton == null || sourceSkeleton.skeletonDataAsset == null)
        {
            return string.Empty;
        }

        Spine.SkeletonData skeletonData = sourceSkeleton.skeletonDataAsset.GetSkeletonData(false);
        if (skeletonData == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(preferredIdleAnimation) && skeletonData.FindAnimation(preferredIdleAnimation) != null)
        {
            return preferredIdleAnimation;
        }

        string sourceAnimation = sourceSkeleton.AnimationName;
        string[] candidates =
        {
            sourceAnimation,
            "Idle",
            "idle",
            "Stand",
            "stand",
            "\u5F85\u673A"
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            if (skeletonData.FindAnimation(candidate) != null)
            {
                return candidate;
            }
        }

        Debug.LogWarning("[PlayerAttributePreview] No suitable idle animation was found on " + sourceSkeleton.name + ". PlayerAttributePanelUI can still override it at runtime.");
        return string.Empty;
    }

    private static void ApplySourceVisualHint(RectTransform previewRect, Transform sourceTransform)
    {
        if (previewRect == null || sourceTransform == null)
        {
            return;
        }

        Vector3 sourceScale = sourceTransform.localScale;
        Vector3 sourcePosition = sourceTransform.localPosition;

        previewRect.anchoredPosition = new Vector2(sourcePosition.x * 5f, sourcePosition.y * 5f);
        previewRect.sizeDelta = new Vector2(
            Mathf.Max(180f, 180f * Mathf.Max(1f, Mathf.Abs(sourceScale.x))),
            Mathf.Max(260f, 260f * Mathf.Max(1f, Mathf.Abs(sourceScale.y))));
        previewRect.localScale = Vector3.one;
    }

    private static void EnsureManualPlayer02PreviewPrefab()
    {
        if (File.Exists(Player02OutputPath))
        {
            Debug.Log("[PlayerAttributePreview] Player02 preview is manual and will not be overwritten: " + Player02OutputPath);
            return;
        }

        GameObject placeholderRoot = null;
        try
        {
            placeholderRoot = new GameObject("Player02AttributeUIPreview", typeof(RectTransform), typeof(CanvasRenderer));
            RectTransform rect = placeholderRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(180f, 260f);
            rect.localScale = Vector3.one;

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(placeholderRoot, Player02OutputPath);
            if (prefabAsset != null)
            {
                Debug.Log("[PlayerAttributePreview] Created Player02 manual preview placeholder at " + Player02OutputPath +
                          ". Replace it with a hand-made prefab before using it in PlayerAttributePanelUI.");
            }
        }
        finally
        {
            if (placeholderRoot != null)
            {
                Object.DestroyImmediate(placeholderRoot);
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
