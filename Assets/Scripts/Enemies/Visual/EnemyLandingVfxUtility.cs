using UnityEngine;

public static class EnemyLandingVfxUtility
{
    public readonly struct LandingVfxRuntimeTuning
    {
        public LandingVfxRuntimeTuning(
            float scaleMultiplier,
            float verticalOffset,
            float lifetimeMultiplier,
            float emissionMultiplier,
            float particleSizeMultiplier,
            float speedMultiplier,
            bool enableShockwave,
            float shockwaveScaleMultiplier)
        {
            ScaleMultiplier = scaleMultiplier;
            VerticalOffset = verticalOffset;
            LifetimeMultiplier = lifetimeMultiplier;
            EmissionMultiplier = emissionMultiplier;
            ParticleSizeMultiplier = particleSizeMultiplier;
            SpeedMultiplier = speedMultiplier;
            EnableShockwave = enableShockwave;
            ShockwaveScaleMultiplier = shockwaveScaleMultiplier;
        }

        public float ScaleMultiplier { get; }
        public float VerticalOffset { get; }
        public float LifetimeMultiplier { get; }
        public float EmissionMultiplier { get; }
        public float ParticleSizeMultiplier { get; }
        public float SpeedMultiplier { get; }
        public bool EnableShockwave { get; }
        public float ShockwaveScaleMultiplier { get; }

        public static LandingVfxRuntimeTuning Default =>
            new LandingVfxRuntimeTuning(1f, 0f, 1f, 1f, 1f, 1f, true, 1f);
    }

    public static GameObject PlayLandingVfx(
        GameObject prefab,
        Vector3 landingPosition,
        Vector3 offset,
        float lifetime,
        Quaternion rotation)
    {
        return PlayLandingVfx(
            prefab,
            landingPosition,
            offset,
            lifetime,
            rotation,
            LandingVfxRuntimeTuning.Default);
    }

    public static GameObject PlayLandingVfx(
        GameObject prefab,
        Vector3 landingPosition,
        Vector3 offset,
        float lifetime,
        Quaternion rotation,
        LandingVfxRuntimeTuning tuning)
    {
        if (prefab == null)
        {
            return null;
        }

        Vector3 groundedPosition = ResolveGroundedPosition(landingPosition);
        Vector3 spawnPosition = groundedPosition + offset + Vector3.up * tuning.VerticalOffset;
        GameObject instance = Object.Instantiate(prefab, spawnPosition, rotation);
        if (instance == null)
        {
            return null;
        }

        if (!Mathf.Approximately(tuning.ScaleMultiplier, 1f))
        {
            instance.transform.localScale *= tuning.ScaleMultiplier;
        }

        ApplyParticleSystemTuning(instance, tuning);
        ConfigureShockwave(instance.transform, tuning);

        float adjustedLifetime = lifetime * Mathf.Max(0.1f, tuning.LifetimeMultiplier);
        if (adjustedLifetime > 0f)
        {
            Object.Destroy(instance, adjustedLifetime);
        }

        return instance;
    }

    private static void ApplyParticleSystemTuning(GameObject instance, LandingVfxRuntimeTuning tuning)
    {
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            main.startLifetime = MultiplyCurve(main.startLifetime, tuning.LifetimeMultiplier);

            if (main.startSize3D)
            {
                main.startSizeX = MultiplyCurve(main.startSizeX, tuning.ParticleSizeMultiplier);
                main.startSizeY = MultiplyCurve(main.startSizeY, tuning.ParticleSizeMultiplier);
                main.startSizeZ = MultiplyCurve(main.startSizeZ, tuning.ParticleSizeMultiplier);
            }
            else
            {
                main.startSize = MultiplyCurve(main.startSize, tuning.ParticleSizeMultiplier);
            }

            main.startSpeed = MultiplyCurve(main.startSpeed, tuning.SpeedMultiplier);

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = MultiplyCurve(emission.rateOverTime, tuning.EmissionMultiplier);
            emission.rateOverDistance = MultiplyCurve(emission.rateOverDistance, tuning.EmissionMultiplier);

            int burstCount = emission.burstCount;
            if (burstCount > 0)
            {
                ParticleSystem.Burst[] bursts = new ParticleSystem.Burst[burstCount];
                emission.GetBursts(bursts);
                for (int i = 0; i < bursts.Length; i++)
                {
                    bursts[i].count = MultiplyCurve(bursts[i].count, tuning.EmissionMultiplier);
                }

                emission.SetBursts(bursts, bursts.Length);
            }
        }
    }

    private static void ConfigureShockwave(Transform root, LandingVfxRuntimeTuning tuning)
    {
        if (root == null)
        {
            return;
        }

        Transform shockwave = root.Find("GroundShockwave");
        if (shockwave == null)
        {
            return;
        }

        shockwave.gameObject.SetActive(tuning.EnableShockwave);
        if (!tuning.EnableShockwave)
        {
            return;
        }

        if (!Mathf.Approximately(tuning.ShockwaveScaleMultiplier, 1f))
        {
            shockwave.localScale *= tuning.ShockwaveScaleMultiplier;
        }

        ParticleSystem particleSystem = shockwave.GetComponent<ParticleSystem>();
        if (particleSystem == null)
        {
            return;
        }

        ParticleSystem.MainModule main = particleSystem.main;
        main.startLifetime = MultiplyCurve(main.startLifetime, tuning.LifetimeMultiplier * 0.75f);
        if (main.startSize3D)
        {
            float shockwaveSizeMultiplier = tuning.ParticleSizeMultiplier * 1.35f;
            main.startSizeX = MultiplyCurve(main.startSizeX, shockwaveSizeMultiplier);
            main.startSizeY = MultiplyCurve(main.startSizeY, shockwaveSizeMultiplier);
            main.startSizeZ = MultiplyCurve(main.startSizeZ, shockwaveSizeMultiplier);
        }
        else
        {
            main.startSize = MultiplyCurve(main.startSize, tuning.ParticleSizeMultiplier * 1.35f);
        }

        main.startSpeed = MultiplyCurve(main.startSpeed, Mathf.Max(0.25f, tuning.SpeedMultiplier * 0.6f));

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = MultiplyCurve(emission.rateOverTime, tuning.EmissionMultiplier);
        emission.rateOverDistance = MultiplyCurve(emission.rateOverDistance, tuning.EmissionMultiplier);
    }

    private static ParticleSystem.MinMaxCurve MultiplyCurve(ParticleSystem.MinMaxCurve curve, float multiplier)
    {
        multiplier = Mathf.Max(0f, multiplier);
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return new ParticleSystem.MinMaxCurve(curve.constant * multiplier);
            case ParticleSystemCurveMode.TwoConstants:
                return new ParticleSystem.MinMaxCurve(curve.constantMin * multiplier, curve.constantMax * multiplier);
            case ParticleSystemCurveMode.Curve:
                return new ParticleSystem.MinMaxCurve(curve.curveMultiplier * multiplier, curve.curve);
            case ParticleSystemCurveMode.TwoCurves:
                return new ParticleSystem.MinMaxCurve(curve.curveMultiplier * multiplier, curve.curveMin, curve.curveMax);
            default:
                return curve;
        }
    }

    private static Vector3 ResolveGroundedPosition(Vector3 landingPosition)
    {
        Vector3 rayOrigin = landingPosition + Vector3.up * 1.5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return landingPosition;
    }
}
