using System.IO;
using UnityEditor;
using UnityEngine;

public static class CreateTwinShiftVfxPrefabs
{
    private const string RootFolder = "Assets/Prefabs/VFX/TwinShift";
    private const string BasicPrefabPath = RootFolder + "/VFX_TwinSwitch_Basic.prefab";
    private const string RadianceToTwilightPrefabPath = RootFolder + "/VFX_TwinShift_RadianceToTwilight.prefab";
    private const string TwilightToRadiancePrefabPath = RootFolder + "/VFX_TwinShift_TwilightToRadiance.prefab";

    private static readonly Color BasicBlue = new Color(0.72f, 0.92f, 1f, 0.9f);
    private static readonly Color BasicWhite = new Color(1f, 1f, 1f, 0.95f);
    private static readonly Color RadianceGold = new Color(1f, 0.9f, 0.35f, 1f);
    private static readonly Color RadianceWhite = new Color(1f, 0.98f, 0.78f, 0.95f);
    private static readonly Color TwilightBlue = new Color(0.45f, 0.25f, 1f, 1f);
    private static readonly Color TwilightSoft = new Color(0.62f, 0.78f, 1f, 0.9f);

    [MenuItem("Tools/YY/Create Twin Shift VFX Prefabs")]
    public static void CreatePrefabs()
    {
        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/VFX");
        EnsureFolder(RootFolder);

        Material particleMaterial = ResolveParticleMaterial();
        CreateOrReplaceBasicPrefab(particleMaterial);
        CreateOrReplaceRadianceToTwilightPrefab(particleMaterial);
        CreateOrReplaceTwilightToRadiancePrefab(particleMaterial);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TwinShift VFX] Prefabs created at " + RootFolder + ".");
    }

    private static void CreateOrReplaceBasicPrefab(Material particleMaterial)
    {
        CreateOrReplacePrefab(BasicPrefabPath, "VFX_TwinSwitch_Basic", root =>
        {
            CreateBurstParticleSystem(
                root.transform,
                "Basic_WhiteBlue_Burst",
                BasicBlue,
                BasicWhite,
                0f,
                22,
                0.9f,
                1.4f,
                0.1f,
                0.2f,
                0.16f,
                particleMaterial);
        });
    }

    private static void CreateOrReplaceRadianceToTwilightPrefab(Material particleMaterial)
    {
        CreateOrReplacePrefab(RadianceToTwilightPrefabPath, "VFX_TwinShift_RadianceToTwilight", root =>
        {
            CreateBurstParticleSystem(
                root.transform,
                "Radiance_Gold_Burst",
                RadianceGold,
                RadianceWhite,
                0f,
                24,
                1.0f,
                1.8f,
                0.16f,
                0.3f,
                0.2f,
                particleMaterial);

            CreateGatherParticleSystem(
                root.transform,
                "Twilight_Blue_Gather",
                TwilightBlue,
                TwilightSoft,
                0.08f,
                30,
                1.25f,
                0.75f,
                0.18f,
                0.34f,
                0.82f,
                particleMaterial);
        });
    }

    private static void CreateOrReplaceTwilightToRadiancePrefab(Material particleMaterial)
    {
        CreateOrReplacePrefab(TwilightToRadiancePrefabPath, "VFX_TwinShift_TwilightToRadiance", root =>
        {
            CreateBurstParticleSystem(
                root.transform,
                "Twilight_Blue_Burst",
                TwilightBlue,
                TwilightSoft,
                0f,
                24,
                1.0f,
                1.7f,
                0.16f,
                0.3f,
                0.24f,
                particleMaterial);

            CreateBurstParticleSystem(
                root.transform,
                "Radiance_Gold_Burst",
                RadianceGold,
                RadianceWhite,
                0.08f,
                26,
                1.2f,
                1.8f,
                0.16f,
                0.34f,
                0.18f,
                particleMaterial);

            CreateRingParticleSystem(
                root.transform,
                "Radiance_Shield_Ring",
                RadianceGold,
                RadianceWhite,
                0.08f,
                28,
                0.75f,
                0.95f,
                0.07f,
                particleMaterial);
        });
    }

    private static void CreateBurstParticleSystem(
        Transform parent,
        string name,
        Color startColor,
        Color endColor,
        float burstDelay,
        int particleCount,
        float lifetime,
        float speed,
        float minSize,
        float maxSize,
        float radius,
        Material particleMaterial)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);

        ParticleSystem system = child.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = true;
        main.duration = Mathf.Max(0.1f, lifetime);
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.45f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(burstDelay, (short)particleCount) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0.01f, radius);
        shape.radiusThickness = 1f;

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(BuildFadeGradient(startColor, endColor));

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, BuildSizeCurve());

        ApplyRendererSetup(system, particleMaterial);
    }

    private static void CreateGatherParticleSystem(
        Transform parent,
        string name,
        Color startColor,
        Color endColor,
        float burstDelay,
        int particleCount,
        float lifetime,
        float inwardSpeed,
        float minSize,
        float maxSize,
        float radius,
        Material particleMaterial)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);

        ParticleSystem system = child.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = true;
        main.duration = Mathf.Max(0.1f, lifetime);
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(-Mathf.Abs(inwardSpeed), -Mathf.Abs(inwardSpeed) * 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(burstDelay, (short)particleCount) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0.01f, radius);
        shape.radiusThickness = 1f;

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(BuildFadeGradient(startColor, endColor));

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, BuildSizeCurve());

        ApplyRendererSetup(system, particleMaterial);
    }

    private static void CreateRingParticleSystem(
        Transform parent,
        string name,
        Color startColor,
        Color endColor,
        float burstDelay,
        int particleCount,
        float lifetime,
        float radius,
        float particleSize,
        Material particleMaterial)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);

        ParticleSystem system = child.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = true;
        main.duration = Mathf.Max(0.1f, lifetime);
        main.startLifetime = lifetime;
        main.startSpeed = 0.05f;
        main.startSize = particleSize;
        main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(burstDelay, (short)particleCount) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0.01f, radius);
        shape.radiusThickness = 0f;

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(BuildFadeGradient(startColor, endColor));

        ApplyRendererSetup(system, particleMaterial);
    }

    private static void CreateOrReplacePrefab(string prefabPath, string rootName, System.Action<GameObject> build)
    {
        if (File.Exists(prefabPath))
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        GameObject root = null;
        try
        {
            root = new GameObject(rootName);
            build(root);
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

    private static void ApplyRendererSetup(ParticleSystem system, Material particleMaterial)
    {
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.sortingOrder = 25;

        if (particleMaterial != null)
        {
            renderer.sharedMaterial = particleMaterial;
        }
    }

    private static Material ResolveParticleMaterial()
    {
        Material material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        if (material != null)
        {
            return material;
        }

        material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        if (material != null)
        {
            return material;
        }

        return AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
    }

    private static Gradient BuildFadeGradient(Color startColor, Color endColor)
    {
        Color transparentEnd = new Color(endColor.r, endColor.g, endColor.b, 0f);
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(endColor, 0.45f),
                new GradientColorKey(transparentEnd, 1f)
            },
            new[]
            {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(endColor.a * 0.75f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static AnimationCurve BuildSizeCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.65f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, 0f));
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
