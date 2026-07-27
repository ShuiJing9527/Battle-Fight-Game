#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class ShaderBuildCollector
{
    private const string ReportPath = "Assets/EditorReports/CollectedEffectShaders.txt";
    private const string VariantCollectionPath = "Assets/ShaderVariants/RuntimeEffectVariants.shadervariants";
    private const string AlwaysIncludedShadersProperty = "m_AlwaysIncludedShaders";
    private const string PreloadedShadersProperty = "m_PreloadedShaders";
    private const string VisualEffectTypeName = "UnityEngine.VFX.VisualEffect";

    private static readonly Type[] RendererTypes =
    {
        typeof(ParticleSystemRenderer),
        typeof(TrailRenderer),
        typeof(LineRenderer),
        typeof(MeshRenderer),
        typeof(SkinnedMeshRenderer),
        typeof(SpriteRenderer)
    };

    private static readonly string[] EffectAssetKeywords =
    {
        "Effect",
        "Effects",
        "VFX",
        "FX",
        "Skill",
        "Attack",
        "Bullet",
        "Projectile",
        "Hit",
        "Slash",
        "Trail",
        "Particle",
        "Spell",
        "Ability",
        "Magic",
        "\u7279\u6548",
        "\u6280\u80fd",
        "\u653b\u51fb",
        "\u5b50\u5f39",
        "\u547d\u4e2d"
    };

    private static readonly string[] EffectShaderKeywords =
    {
        "Particle",
        "Particles",
        "Trail",
        "Effect",
        "VFX",
        "FX",
        "Additive",
        "Dissolve",
        "Distortion",
        "Glow",
        "Slash",
        "Hit",
        "Spine",
        "Shader Graphs/",
        "Universal Render Pipeline/Particles/"
    };

    private static readonly string[] NonEffectShaderKeywords =
    {
        "Sky",
        "Skybox",
        "AHD2TODSystem/Sky",
        "Cloud",
        "Terrain",
        "Universal Render Pipeline/Terrain",
        "Spine/PlayerLit",
        "UI/",
        "TextMeshPro/",
        "Sprites/Default",
        "Universal Render Pipeline/2D/Sprite",
        "Universal Render Pipeline/Sprite"
    };

    private static readonly string[] LargeGeneralPurposeShaderExactNames =
    {
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Simple Lit",
        "Universal Render Pipeline/Complex Lit",
        "Standard",
        "Standard (Specular setup)"
    };

    private static readonly string[] LargeGeneralPurposeShaderPrefixes =
    {
        "Universal Render Pipeline/Nature/",
        "Universal Render Pipeline/Terrain/"
    };

    [MenuItem("Tools/Build/Preview Effect Shaders")]
    public static void PreviewEffectShaders()
    {
        Run(dryRun: true);
    }

    [MenuItem("Tools/Build/Collect Effect Shaders")]
    public static void CollectEffectShaders()
    {
        Run(dryRun: false);
    }

    private static void Run(bool dryRun)
    {
        try
        {
            CollectResult result = CollectShaders();
            GraphicsSettingsAccess graphics = ReadGraphicsSettings();
            if (!graphics.IsValid)
            {
                WriteReport(result, graphics, dryRun, Array.Empty<Shader>(), Array.Empty<Shader>());
                Debug.LogError("[ShaderBuildCollector] Failed to read GraphicsSettings.asset. See report for details.");
                return;
            }

            List<Shader> existingAlwaysIncluded = ReadShaderList(graphics.AlwaysIncludedShaders);
            List<Shader> existingLargeGeneralPurpose = existingAlwaysIncluded
                .Where(IsLargeGeneralPurposeShader)
                .OrderBy(shader => shader.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!dryRun && existingLargeGeneralPurpose.Count > 0)
            {
                string message = "Always Included Shaders already contains large general-purpose shaders:\n\n" +
                                 string.Join("\n", existingLargeGeneralPurpose.Select(shader => shader.name)) +
                                 "\n\nThis tool will not add them again and will not delete existing user settings. " +
                                 "Unity may require removing them manually to avoid variant explosion.";
                if (Application.isBatchMode)
                {
                    Debug.LogWarning($"[ShaderBuildCollector] {message}");
                }
                else
                {
                    EditorUtility.DisplayDialog("Large Always Included Shaders Detected", message, "OK");
                }
            }

            HashSet<Shader> existingSet = new HashSet<Shader>(existingAlwaysIncluded);
            List<Shader> shadersToAdd = result.GetHighConfidenceShaders()
                .Where(shader => shader != null && !IsLargeGeneralPurposeShader(shader) && !existingSet.Contains(shader))
                .OrderBy(shader => shader.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!dryRun)
            {
                Undo.RecordObject(graphics.Asset, "Collect Effect Shaders");
                AppendShaders(graphics.AlwaysIncludedShaders, shadersToAdd);
                graphics.SerializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(graphics.Asset);

                ShaderVariantCollection variantCollection = CreateOrUpdateVariantCollection(result);
                if (variantCollection != null)
                {
                    Undo.RecordObject(graphics.Asset, "Preload Runtime Effect Shader Variants");
                    AddPreloadedShaderCollection(graphics.PreloadedShaders, variantCollection);
                    graphics.SerializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(graphics.Asset);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            WriteReport(result, graphics, dryRun, existingAlwaysIncluded, shadersToAdd);

            bool aContainsLit = result.GetHighConfidenceShaders()
                .Any(shader => shader != null && shader.name == "Universal Render Pipeline/Lit");
            Debug.Log(
                $"[ShaderBuildCollector] {(dryRun ? "Preview" : "Collect")} complete. " +
                $"High={result.GetShaderCount(ShaderConfidence.High)}, " +
                $"Medium={result.GetShaderCount(ShaderConfidence.Medium)}, " +
                $"LargeExcluded={result.LargeGeneralPurposeReferences.Count}, " +
                $"AContainsURPLit={aContainsLit}, NewA={shadersToAdd.Count}. Report: {ReportPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ShaderBuildCollector] Failed: {ex}");
        }
    }

    private static CollectResult CollectShaders()
    {
        CollectResult result = new CollectResult();
        string[] roots = GetSearchRoots(result);
        HashSet<string> runtimePrefabPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        CollectEnabledSceneDependencies(result, runtimePrefabPaths);
        CollectResourcesPrefabs(result, roots, runtimePrefabPaths);
        CollectEffectNamedPrefabs(result, roots, runtimePrefabPaths);

        foreach (string prefabPath in runtimePrefabPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            CollectPrefabRendererMaterials(result, prefabPath);
            CollectPrefabVisualEffectMaterials(result, prefabPath);
        }

        CollectDynamicShaderFinds(result, roots);
        return result;
    }

    private static string[] GetSearchRoots(CollectResult result)
    {
        List<string> roots = new List<string> { "Assets" };
        string packagesRoot = Path.Combine(Directory.GetCurrentDirectory(), "Packages");
        if (Directory.Exists(packagesRoot))
        {
            foreach (string packageDir in Directory.GetDirectories(packagesRoot))
            {
                string packageJson = Path.Combine(packageDir, "package.json");
                if (!File.Exists(packageJson))
                {
                    continue;
                }

                string relativePath = ToUnityPath(Path.GetRelativePath(Directory.GetCurrentDirectory(), packageDir));
                if (!IsExcludedPath(relativePath, result))
                {
                    roots.Add(relativePath);
                }
            }
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void CollectEnabledSceneDependencies(CollectResult result, HashSet<string> runtimePrefabPaths)
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled || string.IsNullOrEmpty(scene.path) || IsExcludedPath(scene.path, result))
            {
                continue;
            }

            result.EnabledSceneCount++;
            foreach (string dependency in AssetDatabase.GetDependencies(scene.path, true))
            {
                if (IsExcludedPath(dependency, result))
                {
                    continue;
                }

                if (dependency.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    runtimePrefabPaths.Add(dependency);
                    continue;
                }

                if (!dependency.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Material material = AssetDatabase.LoadAssetAtPath<Material>(dependency);
                if (material == null)
                {
                    continue;
                }

                ShaderConfidence confidence = IsEffectLikeAsset(dependency) || IsEffectLikeShader(material.shader)
                    ? ShaderConfidence.Medium
                    : ShaderConfidence.NonEffect;
                string reason = confidence == ShaderConfidence.Medium
                    ? "Enabled Build Settings scene dependency has effect-like path or shader name."
                    : "Enabled Build Settings scene dependency material is not effect-like.";
                result.RegisterMaterialReference(material, dependency, scene.path, "SceneDependency", confidence, reason);
            }
        }
    }

    private static void CollectResourcesPrefabs(CollectResult result, string[] roots, HashSet<string> runtimePrefabPaths)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", roots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsExcludedPath(path, result) && IndexOfOrdinalIgnoreCase(path, "/Resources/") >= 0)
            {
                runtimePrefabPaths.Add(path);
            }
        }
    }

    private static void CollectEffectNamedPrefabs(CollectResult result, string[] roots, HashSet<string> runtimePrefabPaths)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", roots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsExcludedPath(path, result) && IsEffectLikeAsset(path))
            {
                runtimePrefabPaths.Add(path);
            }
        }
    }

    private static void CollectPrefabRendererMaterials(CollectResult result, string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return;
        }

        foreach (Type rendererType in RendererTypes)
        {
            Component[] renderers = prefab.GetComponentsInChildren(rendererType, true);
            foreach (Component component in renderers)
            {
                Renderer renderer = component as Renderer;
                if (renderer == null)
                {
                    continue;
                }

                foreach (Material material in renderer.sharedMaterials)
                {
                    RegisterRendererMaterial(result, material, prefabPath, renderer);
                }
            }
        }
    }

    private static void RegisterRendererMaterial(CollectResult result, Material material, string prefabPath, Renderer renderer)
    {
        string rendererTypeName = renderer.GetType().Name;
        string rendererPath = $"{rendererTypeName}:{GetHierarchyPath(renderer.transform)}";
        if (material == null)
        {
            result.RegisterMissingMaterial(prefabPath, rendererPath, "material=null");
            return;
        }

        bool effectPrefab = IsEffectLikeAsset(prefabPath);
        bool effectRenderer = IsEffectRenderer(renderer);
        bool effectShader = IsEffectLikeShader(material.shader);
        bool largeGeneralPurpose = IsLargeGeneralPurposeShader(material.shader);

        ShaderConfidence confidence;
        string reason;
        if (largeGeneralPurpose)
        {
            confidence = ShaderConfidence.Medium;
            reason = "Large general-purpose shader. Variant count can be too high for Always Included; use material references or ShaderVariantCollection.";
        }
        else if (effectRenderer)
        {
            confidence = ShaderConfidence.High;
            reason = $"Renderer type {rendererTypeName} is a runtime effect renderer.";
        }
        else if (effectPrefab)
        {
            confidence = ShaderConfidence.High;
            reason = "Prefab path/name matches runtime effect keywords.";
        }
        else if (effectShader)
        {
            confidence = ShaderConfidence.Medium;
            reason = "Shader name matches effect keywords, but renderer/prefab is not clearly an effect.";
        }
        else
        {
            confidence = ShaderConfidence.NonEffect;
            reason = "Ordinary renderer/material; not collected automatically.";
        }

        result.RegisterMaterialReference(
            material,
            AssetDatabase.GetAssetPath(material),
            prefabPath,
            rendererPath,
            confidence,
            reason);
    }

    private static void CollectPrefabVisualEffectMaterials(CollectResult result, string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return;
        }

        foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component.GetType().FullName != VisualEffectTypeName)
            {
                continue;
            }

            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                Material material = iterator.objectReferenceValue as Material;
                if (material == null)
                {
                    continue;
                }

                ShaderConfidence confidence = IsLargeGeneralPurposeShader(material.shader)
                    ? ShaderConfidence.Medium
                    : ShaderConfidence.High;
                string reason = confidence == ShaderConfidence.Medium
                    ? "VisualEffect references a large general-purpose shader. Kept out of Always Included."
                    : "VisualEffect component references this material.";
                result.RegisterMaterialReference(
                    material,
                    AssetDatabase.GetAssetPath(material),
                    prefabPath,
                    $"VisualEffect:{GetHierarchyPath(component.transform)}",
                    confidence,
                    reason);
            }
        }
    }

    private static void CollectDynamicShaderFinds(CollectResult result, string[] roots)
    {
        Regex shaderFindRegex = new Regex("Shader\\.Find\\s*\\(\\s*\"([^\"]+)\"\\s*\\)", RegexOptions.Compiled);

        foreach (string guid in AssetDatabase.FindAssets("t:Script", roots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (IsExcludedPath(path, result) || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                result.CodeScanWarnings.Add($"{path}: {ex.Message}");
                continue;
            }

            bool editorOnly = IsEditorOnlyPath(path);
            foreach (Match match in shaderFindRegex.Matches(text))
            {
                string shaderName = match.Groups[1].Value;
                Shader shader = Shader.Find(shaderName);
                result.DynamicShaderFinds.Add(new DynamicShaderFind(shaderName, path, shader, editorOnly));

                if (editorOnly || shader == null || !ShouldConsiderShader(shader))
                {
                    continue;
                }

                ShaderConfidence confidence;
                string reason;
                if (IsLargeGeneralPurposeShader(shader))
                {
                    confidence = ShaderConfidence.Medium;
                    reason = "Runtime Shader.Find uses a large general-purpose shader. Suggested only; not Always Included.";
                }
                else if (IsEffectLikeShader(shader) || IsEffectLikeAsset(path))
                {
                    confidence = ShaderConfidence.High;
                    reason = "Runtime code uses constant Shader.Find for an effect-like shader.";
                }
                else
                {
                    confidence = ShaderConfidence.Medium;
                    reason = "Runtime code uses constant Shader.Find.";
                }

                result.RegisterDynamicShader(shader, shaderName, path, confidence, reason);
            }

            if (IndexOfOrdinal(text, "EnableKeyword(") >= 0 ||
                IndexOfOrdinal(text, "DisableKeyword(") >= 0 ||
                IndexOfOrdinal(text, "Resources.Load(") >= 0 ||
                IndexOfOrdinal(text, "Addressables.LoadAssetAsync(") >= 0 ||
                IndexOfOrdinal(text, ".shader =") >= 0)
            {
                result.DynamicCodeReferences.Add(path);
            }
        }
    }

    private static GraphicsSettingsAccess ReadGraphicsSettings()
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        UnityEngine.Object asset = assets.FirstOrDefault();
        if (asset == null)
        {
            return GraphicsSettingsAccess.Invalid("Could not load ProjectSettings/GraphicsSettings.asset.");
        }

        SerializedObject serializedObject = new SerializedObject(asset);
        SerializedProperty alwaysIncludedShaders = serializedObject.FindProperty(AlwaysIncludedShadersProperty);
        SerializedProperty preloadedShaders = serializedObject.FindProperty(PreloadedShadersProperty);
        if (alwaysIncludedShaders == null)
        {
            return GraphicsSettingsAccess.Invalid($"Serialized property '{AlwaysIncludedShadersProperty}' was not found.");
        }

        if (preloadedShaders == null)
        {
            return GraphicsSettingsAccess.Invalid($"Serialized property '{PreloadedShadersProperty}' was not found.");
        }

        return new GraphicsSettingsAccess(asset, serializedObject, alwaysIncludedShaders, preloadedShaders, null);
    }

    private static List<Shader> ReadShaderList(SerializedProperty property)
    {
        List<Shader> shaders = new List<Shader>();
        for (int i = 0; i < property.arraySize; i++)
        {
            Shader shader = property.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
            if (shader != null)
            {
                shaders.Add(shader);
            }
        }

        return shaders;
    }

    private static void AppendShaders(SerializedProperty property, IReadOnlyList<Shader> shaders)
    {
        foreach (Shader shader in shaders)
        {
            if (shader == null || IsLargeGeneralPurposeShader(shader))
            {
                continue;
            }

            int index = property.arraySize;
            property.InsertArrayElementAtIndex(index);
            property.GetArrayElementAtIndex(index).objectReferenceValue = shader;
        }
    }

    private static ShaderVariantCollection CreateOrUpdateVariantCollection(CollectResult result)
    {
        EnsureDirectory("Assets/ShaderVariants");

        ShaderVariantCollection collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(VariantCollectionPath);
        if (collection == null)
        {
            collection = new ShaderVariantCollection();
            AssetDatabase.CreateAsset(collection, VariantCollectionPath);
            Undo.RegisterCreatedObjectUndo(collection, "Create Runtime Effect Shader Variants");
        }
        else
        {
            Undo.RecordObject(collection, "Update Runtime Effect Shader Variants");
            collection.Clear();
        }

        foreach (Material material in result.GetVariantMaterials())
        {
            if (material == null || !ShouldConsiderShader(material.shader))
            {
                continue;
            }

            try
            {
                ShaderVariantCollection.ShaderVariant variant =
                    new ShaderVariantCollection.ShaderVariant(
                        material.shader,
                        PassType.Normal,
                        material.shaderKeywords ?? Array.Empty<string>());
                collection.Add(variant);
            }
            catch (Exception ex)
            {
                result.VariantWarnings.Add($"{material.name} / {material.shader.name}: {ex.Message}");
            }
        }

        EditorUtility.SetDirty(collection);
        return collection;
    }

    private static void AddPreloadedShaderCollection(SerializedProperty property, ShaderVariantCollection collection)
    {
        if (collection == null)
        {
            return;
        }

        for (int i = 0; i < property.arraySize; i++)
        {
            if (property.GetArrayElementAtIndex(i).objectReferenceValue == collection)
            {
                return;
            }
        }

        int index = property.arraySize;
        property.InsertArrayElementAtIndex(index);
        property.GetArrayElementAtIndex(index).objectReferenceValue = collection;
    }

    private static void WriteReport(
        CollectResult result,
        GraphicsSettingsAccess graphics,
        bool dryRun,
        IReadOnlyList<Shader> existingAlwaysIncluded,
        IReadOnlyList<Shader> shadersToAdd)
    {
        EnsureDirectory("Assets/EditorReports");

        HashSet<Shader> existingSet = new HashSet<Shader>(existingAlwaysIncluded.Where(shader => shader != null));
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Shader Build Collector Report");
        builder.AppendLine($"Generated At: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Mode: {(dryRun ? "Dry Run / Preview" : "Collect / Apply")}");
        builder.AppendLine($"GraphicsSettings valid: {graphics.IsValid}");
        if (!graphics.IsValid)
        {
            builder.AppendLine($"GraphicsSettings error: {graphics.Error}");
        }

        builder.AppendLine();
        builder.AppendLine("Summary");
        builder.AppendLine($"Enabled Build Settings scenes scanned: {result.EnabledSceneCount}");
        builder.AppendLine($"Prefab/material references scanned: {result.MaterialReferenceCount}");
        builder.AppendLine($"High confidence effect shaders: {result.GetShaderCount(ShaderConfidence.High)}");
        builder.AppendLine($"Medium confidence suggested shaders: {result.GetShaderCount(ShaderConfidence.Medium)}");
        builder.AppendLine($"Non-effect shaders excluded from auto add: {result.GetShaderCount(ShaderConfidence.NonEffect)}");
        builder.AppendLine($"Large general-purpose shader references excluded from Always Included: {result.LargeGeneralPurposeReferences.Count}");
        builder.AppendLine($"Dynamic Shader.Find constants: {result.DynamicShaderFinds.Count}");
        builder.AppendLine($"Existing Always Included Shaders: {existingAlwaysIncluded.Count}");
        builder.AppendLine($"New A-class shaders {(dryRun ? "that would be added" : "added")}: {shadersToAdd.Count}");
        builder.AppendLine($"A class contains Universal Render Pipeline/Lit: {result.GetHighConfidenceShaders().Any(shader => shader.name == "Universal Render Pipeline/Lit")}");
        builder.AppendLine($"Excluded test/recovery/editor/temp resources: {result.ExcludedAssetCount}");
        builder.AppendLine($"Excluded path hit Assets/_Recovery: {result.HasExcludedRecoveryPath}");
        builder.AppendLine($"Excluded path hit Packages/*/Tests or Test: {result.HasExcludedPackageTestPath}");
        builder.AppendLine($"Materials missing shader: {result.MissingShaderMaterials.Count}");

        builder.AppendLine();
        builder.AppendLine("Large General-Purpose Shaders Excluded From Always Included");
        AppendReferenceList(builder, result.LargeGeneralPurposeReferences);

        builder.AppendLine();
        builder.AppendLine("A. High Confidence Effect Shaders - Auto Added By Collect");
        AppendShaderSection(builder, result, ShaderConfidence.High, existingSet, shadersToAdd);

        builder.AppendLine();
        builder.AppendLine("B. Medium Confidence Shaders - Suggestions Only");
        AppendShaderSection(builder, result, ShaderConfidence.Medium, existingSet, Array.Empty<Shader>());

        builder.AppendLine();
        builder.AppendLine("C. Non-Effect Shaders - Not Added");
        AppendShaderSection(builder, result, ShaderConfidence.NonEffect, existingSet, Array.Empty<Shader>());

        builder.AppendLine();
        builder.AppendLine("D. Shader.Find Dynamic Loaded Shaders");
        foreach (DynamicShaderFind shaderFind in result.DynamicShaderFinds.OrderBy(item => item.ShaderName, StringComparer.OrdinalIgnoreCase))
        {
            string resolved = shaderFind.Shader != null ? "resolved" : "missing";
            string scope = shaderFind.IsEditorOnly ? "editor-only" : "runtime";
            builder.AppendLine($"- {shaderFind.ShaderName} [{resolved}, {scope}]");
            builder.AppendLine($"  Source: {shaderFind.SourcePath}");
        }

        builder.AppendLine();
        builder.AppendLine("Missing Shader Materials");
        foreach (string material in result.MissingShaderMaterials.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {material}");
        }

        builder.AppendLine();
        builder.AppendLine("Excluded Path Samples");
        foreach (string path in result.ExcludedPathSamples.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {path}");
        }

        builder.AppendLine();
        builder.AppendLine("Code Paths With Runtime Shader/Keyword/Resource References");
        foreach (string path in result.DynamicCodeReferences.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {path}");
        }

        builder.AppendLine();
        builder.AppendLine("Variant Collection");
        builder.AppendLine($"Path: {VariantCollectionPath}");
        builder.AppendLine("Collect mode generates variants for A-class materials and large general-purpose materials currently referenced by runtime effects.");
        foreach (string warning in result.VariantWarnings.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- Variant warning: {warning}");
        }

        builder.AppendLine();
        builder.AppendLine("Code Scan Warnings");
        foreach (string warning in result.CodeScanWarnings.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {warning}");
        }

        File.WriteAllText(ReportPath, builder.ToString(), Encoding.UTF8);
        AssetDatabase.ImportAsset(ReportPath);
    }

    private static void AppendShaderSection(
        StringBuilder builder,
        CollectResult result,
        ShaderConfidence confidence,
        HashSet<Shader> existingAlwaysIncluded,
        IReadOnlyList<Shader> shadersToAdd)
    {
        HashSet<Shader> addedSet = new HashSet<Shader>(shadersToAdd.Where(shader => shader != null));
        foreach (Shader shader in result.GetShadersByConfidence(confidence).OrderBy(shader => shader.name, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- Shader: {shader.name}");
            builder.AppendLine($"  Already Included: {existingAlwaysIncluded.Contains(shader)}");
            builder.AppendLine($"  {(confidence == ShaderConfidence.High ? "Added/Will Add" : "Auto Add")}: {addedSet.Contains(shader)}");
            AppendReferenceList(builder, result.GetReferences(shader, confidence));
        }
    }

    private static void AppendReferenceList(StringBuilder builder, IEnumerable<ShaderReference> references)
    {
        foreach (ShaderReference reference in references
                     .OrderBy(item => item.ShaderName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.PrefabOrSourcePath, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.MaterialName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- Shader: {reference.ShaderName}");
            builder.AppendLine($"  Material: {reference.MaterialName}");
            builder.AppendLine($"  Material Path: {reference.MaterialPath}");
            builder.AppendLine($"  Prefab/Source: {reference.PrefabOrSourcePath}");
            builder.AppendLine($"  Renderer Type: {reference.RendererType}");
            builder.AppendLine($"  Reason: {reference.Reason}");
        }
    }

    private static bool ShouldConsiderShader(Shader shader)
    {
        if (shader == null)
        {
            return false;
        }

        string shaderName = shader.name ?? string.Empty;
        return !string.Equals(shaderName, "Hidden/InternalErrorShader", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEffectRenderer(Renderer renderer)
    {
        return renderer is ParticleSystemRenderer ||
               renderer is TrailRenderer ||
               renderer is LineRenderer;
    }

    private static bool IsEffectLikeAsset(string pathOrName)
    {
        return ContainsAny(pathOrName, EffectAssetKeywords);
    }

    private static bool IsEffectLikeShader(Shader shader)
    {
        return shader != null && ContainsAny(shader.name, EffectShaderKeywords);
    }

    private static bool IsNonEffectShader(Shader shader)
    {
        return shader != null && ContainsAny(shader.name, NonEffectShaderKeywords);
    }

    private static bool IsLargeGeneralPurposeShader(Shader shader)
    {
        if (shader == null)
        {
            return false;
        }

        string shaderName = shader.name ?? string.Empty;
        return LargeGeneralPurposeShaderExactNames.Any(name => string.Equals(shaderName, name, StringComparison.OrdinalIgnoreCase)) ||
               LargeGeneralPurposeShaderPrefixes.Any(prefix => shaderName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(string value, IEnumerable<string> keywords)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return keywords.Any(keyword => IndexOfOrdinalIgnoreCase(value, keyword) >= 0);
    }

    private static bool IsExcludedPath(string path, CollectResult result = null)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        string normalized = ToUnityPath(path);
        bool excluded =
            normalized.StartsWith("Assets/_Recovery/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("Assets/Editor/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("Assets/EditorReports/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("Assets/ShaderVariants/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("Library/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("Temp/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("Logs/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/Library/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/PackageCache/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/Temp/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/Logs/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/Tests/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/Test/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/Samples/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/Samples~/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("/Editor/", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith(".Editor.cs", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(".Editor.", StringComparison.OrdinalIgnoreCase);

        if (excluded)
        {
            result?.RegisterExcludedPath(normalized);
        }

        return excluded;
    }

    private static bool IsEditorOnlyPath(string path)
    {
        string normalized = ToUnityPath(path);
        return normalized.StartsWith("Assets/Editor/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("/Editor/", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(".Editor.cs", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains(".Editor.", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    private static string ToUnityPath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        Stack<string> names = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static int IndexOfOrdinal(string value, string pattern)
    {
        return value?.IndexOf(pattern, StringComparison.Ordinal) ?? -1;
    }

    private static int IndexOfOrdinalIgnoreCase(string value, string pattern)
    {
        return value?.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) ?? -1;
    }

    private enum ShaderConfidence
    {
        High,
        Medium,
        NonEffect
    }

    private sealed class CollectResult
    {
        private readonly Dictionary<Shader, List<ShaderReference>> highConfidence = new Dictionary<Shader, List<ShaderReference>>();
        private readonly Dictionary<Shader, List<ShaderReference>> mediumConfidence = new Dictionary<Shader, List<ShaderReference>>();
        private readonly Dictionary<Shader, List<ShaderReference>> nonEffect = new Dictionary<Shader, List<ShaderReference>>();
        private readonly Dictionary<string, ShaderReference> dedupe = new Dictionary<string, ShaderReference>(StringComparer.OrdinalIgnoreCase);

        public readonly SortedSet<string> MissingShaderMaterials = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<DynamicShaderFind> DynamicShaderFinds = new List<DynamicShaderFind>();
        public readonly SortedSet<string> DynamicCodeReferences = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> VariantWarnings = new List<string>();
        public readonly List<string> CodeScanWarnings = new List<string>();
        public readonly SortedSet<string> ExcludedPathSamples = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<ShaderReference> LargeGeneralPurposeReferences = new List<ShaderReference>();

        public int MaterialReferenceCount { get; private set; }
        public int EnabledSceneCount { get; set; }
        public int ExcludedAssetCount { get; private set; }
        public bool HasExcludedRecoveryPath { get; private set; }
        public bool HasExcludedPackageTestPath { get; private set; }

        public void RegisterMaterialReference(
            Material material,
            string materialPath,
            string prefabOrSourcePath,
            string rendererType,
            ShaderConfidence confidence,
            string reason)
        {
            MaterialReferenceCount++;
            if (material == null)
            {
                RegisterMissingMaterial(prefabOrSourcePath, rendererType, "material=null");
                return;
            }

            if (!ShouldConsiderShader(material.shader))
            {
                RegisterMissingMaterial(prefabOrSourcePath, rendererType, $"{material.name} shader=null or Hidden/InternalErrorShader");
                return;
            }

            if (IsLargeGeneralPurposeShader(material.shader))
            {
                confidence = ShaderConfidence.Medium;
                reason = "Variant count too large for Always Included; use material references or ShaderVariantCollection.";
            }

            ShaderReference reference = new ShaderReference(
                material.shader,
                material.name,
                string.IsNullOrEmpty(materialPath) ? "(embedded or generated material)" : materialPath,
                prefabOrSourcePath,
                rendererType,
                reason);

            Register(reference, confidence);
            if (IsLargeGeneralPurposeShader(material.shader))
            {
                LargeGeneralPurposeReferences.Add(reference);
            }
        }

        public void RegisterDynamicShader(
            Shader shader,
            string shaderName,
            string sourcePath,
            ShaderConfidence confidence,
            string reason)
        {
            if (!ShouldConsiderShader(shader))
            {
                return;
            }

            if (IsLargeGeneralPurposeShader(shader))
            {
                confidence = ShaderConfidence.Medium;
                reason = "Variant count too large for Always Included; use material references or ShaderVariantCollection.";
            }

            Register(
                new ShaderReference(
                    shader,
                    $"Shader.Find(\"{shaderName}\")",
                    "(dynamic)",
                    sourcePath,
                    "Code",
                    reason),
                confidence);
        }

        public void RegisterMissingMaterial(string prefabOrSourcePath, string rendererType, string reason)
        {
            MissingShaderMaterials.Add($"{prefabOrSourcePath} [{rendererType}] {reason}");
        }

        public void RegisterExcludedPath(string path)
        {
            ExcludedAssetCount++;
            if (ExcludedPathSamples.Count < 200)
            {
                ExcludedPathSamples.Add(path);
            }

            string normalized = ToUnityPath(path);
            if (normalized.StartsWith("Assets/_Recovery/", StringComparison.OrdinalIgnoreCase))
            {
                HasExcludedRecoveryPath = true;
            }

            if (normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) &&
                (normalized.Contains("/Tests/", StringComparison.OrdinalIgnoreCase) ||
                 normalized.Contains("/Test/", StringComparison.OrdinalIgnoreCase)))
            {
                HasExcludedPackageTestPath = true;
            }
        }

        public int GetShaderCount(ShaderConfidence confidence)
        {
            return GetMap(confidence).Count;
        }

        public IEnumerable<Shader> GetHighConfidenceShaders()
        {
            return highConfidence.Keys.Where(shader => !IsLargeGeneralPurposeShader(shader));
        }

        public IEnumerable<Material> GetVariantMaterials()
        {
            return highConfidence.Values
                .Concat(new[] { LargeGeneralPurposeReferences })
                .SelectMany(references => references)
                .Where(reference => !string.Equals(reference.MaterialPath, "(dynamic)", StringComparison.OrdinalIgnoreCase))
                .Where(reference => !string.IsNullOrEmpty(reference.MaterialPath))
                .Select(reference => AssetDatabase.LoadAssetAtPath<Material>(reference.MaterialPath))
                .Where(material => material != null)
                .Distinct();
        }

        public IEnumerable<Shader> GetShadersByConfidence(ShaderConfidence confidence)
        {
            return GetMap(confidence).Keys;
        }

        public IEnumerable<ShaderReference> GetReferences(Shader shader, ShaderConfidence confidence)
        {
            return GetMap(confidence).TryGetValue(shader, out List<ShaderReference> references)
                ? references
                : Enumerable.Empty<ShaderReference>();
        }

        private void Register(ShaderReference reference, ShaderConfidence confidence)
        {
            string key = $"{reference.Shader.GetInstanceID()}|{confidence}|{reference.MaterialPath}|{reference.PrefabOrSourcePath}|{reference.RendererType}";
            if (dedupe.ContainsKey(key))
            {
                return;
            }

            dedupe.Add(key, reference);
            Dictionary<Shader, List<ShaderReference>> map = GetMap(confidence);
            if (!map.TryGetValue(reference.Shader, out List<ShaderReference> references))
            {
                references = new List<ShaderReference>();
                map.Add(reference.Shader, references);
            }

            references.Add(reference);
        }

        private Dictionary<Shader, List<ShaderReference>> GetMap(ShaderConfidence confidence)
        {
            switch (confidence)
            {
                case ShaderConfidence.High:
                    return highConfidence;
                case ShaderConfidence.Medium:
                    return mediumConfidence;
                default:
                    return nonEffect;
            }
        }
    }

    private sealed class ShaderReference
    {
        public ShaderReference(
            Shader shader,
            string materialName,
            string materialPath,
            string prefabOrSourcePath,
            string rendererType,
            string reason)
        {
            Shader = shader;
            ShaderName = shader != null ? shader.name : "(missing shader)";
            MaterialName = materialName;
            MaterialPath = materialPath;
            PrefabOrSourcePath = prefabOrSourcePath;
            RendererType = rendererType;
            Reason = reason;
        }

        public Shader Shader { get; }
        public string ShaderName { get; }
        public string MaterialName { get; }
        public string MaterialPath { get; }
        public string PrefabOrSourcePath { get; }
        public string RendererType { get; }
        public string Reason { get; }
    }

    private sealed class DynamicShaderFind
    {
        public DynamicShaderFind(string shaderName, string sourcePath, Shader shader, bool isEditorOnly)
        {
            ShaderName = shaderName;
            SourcePath = sourcePath;
            Shader = shader;
            IsEditorOnly = isEditorOnly;
        }

        public string ShaderName { get; }
        public string SourcePath { get; }
        public Shader Shader { get; }
        public bool IsEditorOnly { get; }
    }

    private sealed class GraphicsSettingsAccess
    {
        public GraphicsSettingsAccess(
            UnityEngine.Object asset,
            SerializedObject serializedObject,
            SerializedProperty alwaysIncludedShaders,
            SerializedProperty preloadedShaders,
            string error)
        {
            Asset = asset;
            SerializedObject = serializedObject;
            AlwaysIncludedShaders = alwaysIncludedShaders;
            PreloadedShaders = preloadedShaders;
            Error = error;
        }

        public UnityEngine.Object Asset { get; }
        public SerializedObject SerializedObject { get; }
        public SerializedProperty AlwaysIncludedShaders { get; }
        public SerializedProperty PreloadedShaders { get; }
        public string Error { get; }
        public bool IsValid => Asset != null && SerializedObject != null && AlwaysIncludedShaders != null && PreloadedShaders != null;

        public static GraphicsSettingsAccess Invalid(string error)
        {
            return new GraphicsSettingsAccess(null, null, null, null, error);
        }
    }
}
#endif
