using UnityEngine;

[DisallowMultipleComponent]
public class DeathDissolveVfxController : MonoBehaviour
{
    [Header("Systems")]
    [SerializeField] private ParticleSystem orangeSparkBurst;
    [SerializeField] private ParticleSystem risingAshParticles;
    [SerializeField] private ParticleSystem edgeGlowParticles;

    [Header("Look")]
    [SerializeField] private Material particleMaterial;
    [SerializeField] private Color sparkColor = new Color(1f, 0.62f, 0.2f, 1f);
    [SerializeField] private Color ashColor = new Color(1f, 0.78f, 0.36f, 0.7f);
    [SerializeField] private Color glowColor = new Color(1f, 0.9f, 0.45f, 0.9f);
    [SerializeField] private float effectScale = 1f;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float defaultAutoDestroyDelay = 2f;

    private void Awake()
    {
        EnsureReferences();
        ApplyPreset();
        StopAndClear();
    }

    private void Reset()
    {
        EnsureReferences();
        ApplyPreset();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureReferences();
            ApplyPreset();
        }
    }

    public void Play(float autoDestroyDelay, int sortingOrder)
    {
        EnsureReferences();
        ApplyPreset();
        ApplySortingOrder(sortingOrder);
        StopAndClear();

        PlaySystem(orangeSparkBurst);
        PlaySystem(risingAshParticles);
        PlaySystem(edgeGlowParticles);

        float safeDelay = Mathf.Max(defaultAutoDestroyDelay, autoDestroyDelay);
        Destroy(gameObject, safeDelay);
    }

    private void EnsureReferences()
    {
        if (orangeSparkBurst == null)
        {
            Transform spark = transform.Find("OrangeSparkBurst");
            if (spark != null)
            {
                orangeSparkBurst = spark.GetComponent<ParticleSystem>();
            }
        }

        if (risingAshParticles == null)
        {
            Transform ash = transform.Find("RisingAshParticles");
            if (ash != null)
            {
                risingAshParticles = ash.GetComponent<ParticleSystem>();
            }
        }

        if (edgeGlowParticles == null)
        {
            Transform glow = transform.Find("EdgeGlowParticles");
            if (glow != null)
            {
                edgeGlowParticles = glow.GetComponent<ParticleSystem>();
            }
        }
    }

    private void ApplyPreset()
    {
        ConfigureSparkBurst(orangeSparkBurst);
        ConfigureRisingAsh(risingAshParticles);
        ConfigureEdgeGlow(edgeGlowParticles);
    }

    private void ConfigureSparkBurst(ParticleSystem system)
    {
        if (system == null)
        {
            return;
        }

        ParticleSystem.MainModule main = system.main;
        main.duration = 0.65f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        main.startColor = new ParticleSystem.MinMaxGradient(sparkColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.08f;
        main.maxParticles = 24;
        main.scalingMode = ParticleSystemScalingMode.Local;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)12, (short)16) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f * effectScale;
        shape.arc = 360f;

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(0.4f, 1.15f);
        velocity.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(BuildGradient(
            sparkColor,
            new Color(1f, 0.42f, 0.08f, 0.55f),
            new Color(0.8f, 0.18f, 0.04f, 0f)));

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, BuildCurve(
            new Keyframe(0f, 0.8f),
            new Keyframe(0.45f, 1f),
            new Keyframe(1f, 0f)));

        ApplyRenderer(system, 0);
    }

    private void ConfigureRisingAsh(ParticleSystem system)
    {
        if (system == null)
        {
            return;
        }

        ParticleSystem.MainModule main = system.main;
        main.duration = 1.2f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(ashColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.02f;
        main.maxParticles = 18;
        main.scalingMode = ParticleSystemScalingMode.Local;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0.02f, (short)8, (short)10) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.16f * effectScale;

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(0.55f, 1.1f);
        velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.15f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.2f;
        noise.octaveCount = 1;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(BuildGradient(
            new Color(1f, 0.82f, 0.42f, 0f),
            ashColor,
            new Color(1f, 0.35f, 0.08f, 0f)));

        ApplyRenderer(system, 1);
    }

    private void ConfigureEdgeGlow(ParticleSystem system)
    {
        if (system == null)
        {
            return;
        }

        ParticleSystem.MainModule main = system.main;
        main.duration = 0.9f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(glowColor);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 20;
        main.scalingMode = ParticleSystemScalingMode.Local;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = 10f;
        emission.rateOverDistance = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)4, (short)5) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.14f * effectScale;
        shape.radiusThickness = 0.65f;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, BuildCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, 1f),
            new Keyframe(1f, 0f)));

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(BuildGradient(
            new Color(1f, 0.95f, 0.65f, 0f),
            glowColor,
            new Color(1f, 0.45f, 0.08f, 0f)));

        ApplyRenderer(system, 2);
    }

    private void ApplyRenderer(ParticleSystem system, int sortingOffset)
    {
        if (system == null)
        {
            return;
        }

        ParticleSystem.CollisionModule collision = system.collision;
        collision.enabled = false;

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
        {
            return;
        }

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.normalDirection = 1f;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 0.6f;
        renderer.lengthScale = 1f;
        renderer.velocityScale = 0f;
        renderer.cameraVelocityScale = 0f;
        renderer.sortingOrder = sortingOffset;

        if (particleMaterial != null)
        {
            renderer.sharedMaterial = particleMaterial;
        }
    }

    private void ApplySortingOrder(int baseSortingOrder)
    {
        ApplySortingOrder(orangeSparkBurst, baseSortingOrder + 1);
        ApplySortingOrder(risingAshParticles, baseSortingOrder + 2);
        ApplySortingOrder(edgeGlowParticles, baseSortingOrder + 3);
    }

    private static void ApplySortingOrder(ParticleSystem system, int sortingOrder)
    {
        if (system == null)
        {
            return;
        }

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = sortingOrder;
        }
    }

    private void StopAndClear()
    {
        StopAndClear(orangeSparkBurst);
        StopAndClear(risingAshParticles);
        StopAndClear(edgeGlowParticles);
    }

    private static void StopAndClear(ParticleSystem system)
    {
        if (system != null)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private static void PlaySystem(ParticleSystem system)
    {
        if (system != null)
        {
            system.Play(true);
        }
    }

    private static AnimationCurve BuildCurve(params Keyframe[] keyframes)
    {
        return new AnimationCurve(keyframes);
    }

    private static Gradient BuildGradient(Color start, Color mid, Color end)
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
}
