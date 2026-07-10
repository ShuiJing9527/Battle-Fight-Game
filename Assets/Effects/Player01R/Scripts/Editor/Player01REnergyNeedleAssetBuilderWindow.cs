using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class Player01REnergyNeedleAssetBuilderWindow : EditorWindow
{
    private const string RootFolder = "Assets/Effects/Player01R";
    private const string ScriptFolder = RootFolder + "/Scripts";
    private const string ShaderFolder = RootFolder + "/Shaders";
    private const string MaterialFolder = RootFolder + "/Materials";
    private const string MeshFolder = RootFolder + "/Meshes";
    private const string PrefabFolder = RootFolder + "/Prefabs";
    private const string ShaderPath = ShaderFolder + "/Player01REnergyNeedleURP.shader";
    private const string MeshPath = MeshFolder + "/Player01R_EnergyNeedle.asset";
    private const string CoreMaterialPath = MaterialFolder + "/Player01R_NeedleCore.mat";
    private const string OuterMaterialPath = MaterialFolder + "/Player01R_NeedleOuterGlow.mat";
    private const string TipMaterialPath = MaterialFolder + "/Player01R_NeedleTipGlow.mat";
    private const string TrailMaterialPath = MaterialFolder + "/Player01R_NeedleTrail.mat";
    private const string ParticleMaterialPath = MaterialFolder + "/Player01R_NeedleTailParticles.mat";
    private const string PrefabPath = PrefabFolder + "/Player01R_EnergyNeedle.prefab";

    private float totalLength = 3.25f;
    private float bodyRadius = 0.055f;
    private float tipLength = 0.72f;
    private int radialSegments = 12;
    private int tipSegments = 5;

    [MenuItem("Tools/YY/Effects/Player01/R Energy Needle Builder")]
    public static void Open()
    {
        Player01REnergyNeedleAssetBuilderWindow window = GetWindow<Player01REnergyNeedleAssetBuilderWindow>();
        window.titleContent = new GUIContent("P1 R Needle");
        window.minSize = new Vector2(420f, 360f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Player01 R Energy Needle", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Manual builder for the first-stage single-needle visual test. Generates mesh, materials, and prefab under Assets/Effects/Player01R.", MessageType.Info);

        GUILayout.Space(8f);
        EditorGUILayout.LabelField("Mesh Parameters", EditorStyles.boldLabel);
        totalLength = EditorGUILayout.FloatField("Total Length", totalLength);
        bodyRadius = EditorGUILayout.FloatField("Body Radius", bodyRadius);
        tipLength = EditorGUILayout.FloatField("Tip Length", tipLength);
        radialSegments = EditorGUILayout.IntSlider("Radial Segments", radialSegments, 6, 32);
        tipSegments = EditorGUILayout.IntSlider("Tip Segments", tipSegments, 2, 10);

        GUILayout.Space(10f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Generate Mesh Only", GUILayout.Height(28f)))
            {
                GenerateMeshAsset();
            }

            if (GUILayout.Button("Generate Full Needle Assets", GUILayout.Height(28f)))
            {
                GenerateFullAssetSet();
            }
        }

        GUILayout.Space(8f);
        EditorGUILayout.LabelField("Output Paths", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(MeshPath, EditorStyles.textField, GUILayout.Height(18f));
        EditorGUILayout.SelectableLabel(PrefabPath, EditorStyles.textField, GUILayout.Height(18f));
    }

    private void GenerateMeshAsset()
    {
        EnsureFolders();
        Mesh mesh = BuildNeedleMesh(
            Mathf.Max(0.25f, totalLength),
            Mathf.Max(0.001f, bodyRadius),
            Mathf.Clamp(tipLength, 0.05f, Mathf.Max(0.1f, totalLength - 0.05f)),
            Mathf.Clamp(radialSegments, 6, 64),
            Mathf.Clamp(tipSegments, 2, 16));

        CreateOrReplaceAsset(mesh, MeshPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath));
    }

    private void GenerateFullAssetSet()
    {
        EnsureFolders();
        GenerateMeshAsset();

        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            Debug.LogError("[Player01 R Needle] Shader asset not found. Ensure the shader file exists and Unity has compiled it.");
            return;
        }

        Mesh needleMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (needleMesh == null)
        {
            Debug.LogError("[Player01 R Needle] Needle mesh asset was not generated.");
            return;
        }

        Material coreMaterial = CreateNeedleMaterial(CoreMaterialPath, shader, new Color(0.1f, 1f, 1f, 1f), new Color(0.62f, 1f, 1f, 1f), 0.92f, 4.4f, 2.6f, 1.45f, 8.2f, 2.35f, 1.08f, 0.14f, 0.68f, 1.12f);
        Material outerMaterial = CreateNeedleMaterial(OuterMaterialPath, shader, new Color(0.08f, 0.9f, 1f, 1f), new Color(0.48f, 0.9f, 1f, 1f), 0.24f, 2.7f, 2.05f, 2.2f, 5.6f, 1.85f, 1.04f, 0.08f, 0.76f, 1.1f);
        Material tipMaterial = CreateNeedleMaterial(TipMaterialPath, shader, new Color(0.95f, 1f, 1f, 1f), new Color(0.25f, 0.96f, 1f, 1f), 0.68f, 5.1f, 2.2f, 1.9f, 4.8f, 1.35f, 0.92f, 0.3f, 0.56f, 0.95f);
        Material trailMaterial = CreateNeedleMaterial(TrailMaterialPath, shader, new Color(0.22f, 0.94f, 1f, 1f), new Color(0.82f, 1f, 1f, 1f), 0.34f, 2.05f, 1.95f, 1.8f, 4.2f, 1.5f, 0.96f, 0.02f, 0.96f, 1f);
        Material particleMaterial = CreateNeedleMaterial(ParticleMaterialPath, shader, new Color(0.25f, 0.93f, 1f, 1f), new Color(0.82f, 1f, 1f, 1f), 0.28f, 2.25f, 1.5f, 1.2f, 3.2f, 1.25f, 0.9f, 0.02f, 0.98f, 1f);

        GameObject prefabRoot = null;
        try
        {
            prefabRoot = BuildNeedlePrefab(needleMesh, coreMaterial, outerMaterial, tipMaterial, trailMaterial, particleMaterial);
            if (File.Exists(PrefabPath))
            {
                AssetDatabase.DeleteAsset(PrefabPath);
            }

            GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(prefabAsset);
        }
        finally
        {
            if (prefabRoot != null)
            {
                DestroyImmediate(prefabRoot);
            }
        }
    }

    private static GameObject BuildNeedlePrefab(
        Mesh needleMesh,
        Material coreMaterial,
        Material outerMaterial,
        Material tipMaterial,
        Material trailMaterial,
        Material particleMaterial)
    {
        float needleLength = needleMesh != null ? needleMesh.bounds.max.z : 3.25f;
        GameObject root = new GameObject("Player01R_EnergyNeedle");
        Player01REnergyNeedle needle = root.AddComponent<Player01REnergyNeedle>();

        GameObject core = CreateMeshChild(root.transform, "Core", needleMesh, coreMaterial, Vector3.zero, Vector3.one);
        GameObject outer = CreateMeshChild(root.transform, "OuterGlow", needleMesh, outerMaterial, Vector3.zero, new Vector3(1.18f, 1.18f, 1.01f));
        GameObject tip = CreateMeshChild(
            root.transform,
            "ForwardSpikeGlow",
            needleMesh,
            tipMaterial,
            new Vector3(0f, 0f, needleLength * 0.78f),
            new Vector3(0.6f, 0.6f, 0.22f));
        MeshRenderer tipRenderer = tip.GetComponent<MeshRenderer>();

        TrailRenderer trail = root.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.widthMultiplier = 0.055f;
        trail.minVertexDistance = 0.01f;
        trail.sharedMaterial = trailMaterial;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        trail.numCapVertices = 4;
        trail.numCornerVertices = 4;
        trail.textureMode = LineTextureMode.Stretch;
        trail.widthCurve = AnimationCurve.EaseInOut(0f, 0.82f, 1f, 0f);
        trail.colorGradient = CreateThreePointGradient(
            new Color(0.95f, 1f, 1f, 0.65f),
            new Color(0.18f, 0.9f, 1f, 0.3f),
            new Color(0.08f, 0.55f, 1f, 0f));

        GameObject tailParticles = new GameObject("TailParticles");
        tailParticles.transform.SetParent(root.transform, false);
        tailParticles.transform.localPosition = new Vector3(0f, 0f, 0.08f);
        ParticleSystem particleSystem = tailParticles.AddComponent<ParticleSystem>();
        ConfigureTailParticles(particleSystem);
        ParticleSystemRenderer particleRenderer = tailParticles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = particleMaterial;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.lengthScale = 0.45f;
        particleRenderer.velocityScale = 0.08f;
        particleRenderer.cameraVelocityScale = 0f;

        SerializedObject serializedNeedle = new SerializedObject(needle);
        serializedNeedle.FindProperty("fadeRenderers").arraySize = 5;
        serializedNeedle.FindProperty("fadeRenderers").GetArrayElementAtIndex(0).objectReferenceValue = core.GetComponent<MeshRenderer>();
        serializedNeedle.FindProperty("fadeRenderers").GetArrayElementAtIndex(1).objectReferenceValue = outer.GetComponent<MeshRenderer>();
        serializedNeedle.FindProperty("fadeRenderers").GetArrayElementAtIndex(2).objectReferenceValue = tipRenderer;
        serializedNeedle.FindProperty("fadeRenderers").GetArrayElementAtIndex(3).objectReferenceValue = trail;
        serializedNeedle.FindProperty("fadeRenderers").GetArrayElementAtIndex(4).objectReferenceValue = particleRenderer;
        serializedNeedle.FindProperty("trailRenderer").objectReferenceValue = trail;
        serializedNeedle.FindProperty("tailParticles").arraySize = 1;
        serializedNeedle.FindProperty("tailParticles").GetArrayElementAtIndex(0).objectReferenceValue = particleSystem;
        serializedNeedle.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    private static GameObject CreateMeshChild(Transform parent, string childName, Mesh mesh, Material material, Vector3 localPosition, Vector3 localScale)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localScale = localScale;
        MeshFilter filter = child.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return child;
    }

    private static void ConfigureTailParticles(ParticleSystem system)
    {
        var main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.36f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startSize3D = false;
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.08f);
        main.startRotation3D = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 72;
        main.scalingMode = ParticleSystemScalingMode.Local;

        var emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 46f;

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.04f;
        shape.angle = 10f;
        shape.rotation = new Vector3(0f, 180f, 0f);

        var colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(CreateThreePointGradient(
            new Color(0.92f, 1f, 1f, 0.35f),
            new Color(0.22f, 0.92f, 1f, 0.2f),
            new Color(0.08f, 0.55f, 1f, 0f)));

        var sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.18f));

        var velocityOverLifetime = system.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-1.9f, -3.1f);

        var noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.2f;
        noise.frequency = 0.55f;
        noise.separateAxes = true;
        noise.scrollSpeed = 0.45f;

        var trails = system.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 1f;
        trails.lifetime = 0.45f;
        trails.dieWithParticles = true;
        trails.sizeAffectsWidth = true;
        trails.sizeAffectsLifetime = true;
        trails.inheritParticleColor = true;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.75f, 1f, 0f));
    }

    private static Material CreateNeedleMaterial(
        string assetPath,
        Shader shader,
        Color coreColor,
        Color edgeColor,
        float opacity,
        float emissionIntensity,
        float fresnelPower,
        float fresnelIntensity,
        float noiseScale,
        float noiseSpeed,
        float noiseContrast,
        float tailFadeStart,
        float tailFadeLength,
        float tailFadePower)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, assetPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetColor("_CoreColor", coreColor);
        material.SetColor("_EdgeColor", edgeColor);
        material.SetFloat("_Opacity", opacity);
        material.SetFloat("_EmissionIntensity", emissionIntensity);
        material.SetFloat("_FresnelPower", fresnelPower);
        material.SetFloat("_FresnelIntensity", fresnelIntensity);
        material.SetFloat("_NoiseScale", noiseScale);
        material.SetFloat("_NoiseSpeed", noiseSpeed);
        material.SetFloat("_NoiseContrast", noiseContrast);
        material.SetFloat("_TailFadeStart", tailFadeStart);
        material.SetFloat("_TailFadeLength", tailFadeLength);
        material.SetFloat("_TailFadePower", tailFadePower);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Gradient CreateGradient(Color start, Color end)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(end.a, 1f) });
        return gradient;
    }

    private static Gradient CreateThreePointGradient(Color start, Color mid, Color end)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(mid, 0.45f),
                new GradientColorKey(end, 1f)
            },
            new[]
            {
                new GradientAlphaKey(start.a, 0f),
                new GradientAlphaKey(mid.a, 0.45f),
                new GradientAlphaKey(end.a, 1f)
            });
        return gradient;
    }

    private static void EnsureFolders()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(ScriptFolder);
        EnsureFolder(ScriptFolder + "/Editor");
        EnsureFolder(ShaderFolder);
        EnsureFolder(MaterialFolder);
        EnsureFolder(MeshFolder);
        EnsureFolder(PrefabFolder);
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

    private static void CreateOrReplaceAsset<T>(T asset, string assetPath) where T : Object
    {
        T existingAsset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (existingAsset != null)
        {
            AssetDatabase.DeleteAsset(assetPath);
        }

        AssetDatabase.CreateAsset(asset, assetPath);
    }

    private static Mesh BuildNeedleMesh(float totalLength, float bodyRadius, float tipLength, int radialSegments, int tipSegments)
    {
        Mesh mesh = new Mesh
        {
            name = "Player01R_EnergyNeedle"
        };

        float clampedTipLength = Mathf.Clamp(tipLength, 0.05f, totalLength - 0.05f);
        float bodyLength = Mathf.Max(0.05f, totalLength - clampedTipLength);

        System.Collections.Generic.List<Vector3> vertices = new System.Collections.Generic.List<Vector3>();
        System.Collections.Generic.List<Vector3> normals = new System.Collections.Generic.List<Vector3>();
        System.Collections.Generic.List<Vector2> uvs = new System.Collections.Generic.List<Vector2>();
        System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();

        int AddRing(float z, float radius)
        {
            int startIndex = vertices.Count;
            for (int i = 0; i < radialSegments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / radialSegments;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                vertices.Add(new Vector3(x, y, z));
                normals.Add(new Vector3(x, y, 0f).normalized);
                uvs.Add(new Vector2(i / (float)radialSegments, z / totalLength));
            }

            return startIndex;
        }

        int bodyStart = AddRing(0f, bodyRadius);
        int bodyEnd = AddRing(bodyLength, bodyRadius);

        int previousRing = bodyEnd;
        for (int tipIndex = 1; tipIndex < tipSegments; tipIndex++)
        {
            float t = tipIndex / (float)tipSegments;
            float z = Mathf.Lerp(bodyLength, totalLength, t);
            float radius = Mathf.Lerp(bodyRadius, 0.0025f, t);
            int ring = AddRing(z, radius);
            AppendBridgeTriangles(triangles, previousRing, ring, radialSegments);
            previousRing = ring;
        }

        AppendBridgeTriangles(triangles, bodyStart, bodyEnd, radialSegments);

        int tipVertexIndex = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, totalLength));
        normals.Add(Vector3.forward);
        uvs.Add(new Vector2(0.5f, 1f));

        for (int i = 0; i < radialSegments; i++)
        {
            int current = previousRing + i;
            int next = previousRing + ((i + 1) % radialSegments);
            triangles.Add(current);
            triangles.Add(next);
            triangles.Add(tipVertexIndex);
        }

        int tailCenterIndex = vertices.Count;
        vertices.Add(Vector3.zero);
        normals.Add(Vector3.back);
        uvs.Add(new Vector2(0.5f, 0f));
        for (int i = 0; i < radialSegments; i++)
        {
            int current = bodyStart + i;
            int next = bodyStart + ((i + 1) % radialSegments);
            triangles.Add(tailCenterIndex);
            triangles.Add(next);
            triangles.Add(current);
        }

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    private static void AppendBridgeTriangles(System.Collections.Generic.List<int> triangles, int ringAStart, int ringBStart, int radialSegments)
    {
        for (int i = 0; i < radialSegments; i++)
        {
            int a0 = ringAStart + i;
            int a1 = ringAStart + ((i + 1) % radialSegments);
            int b0 = ringBStart + i;
            int b1 = ringBStart + ((i + 1) % radialSegments);

            triangles.Add(a0);
            triangles.Add(b0);
            triangles.Add(a1);

            triangles.Add(a1);
            triangles.Add(b0);
            triangles.Add(b1);
        }
    }
}
