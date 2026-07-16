using UnityEngine;

public sealed class BossSlimeDevourSkill : MonoBehaviour
{
    public readonly struct RuntimeConfig
    {
        public readonly float PullDuration;
        public readonly float HoldDuration;
        public readonly float ReleaseDuration;
        public readonly bool DealInitialDamage;
        public readonly float InitialDamage;
        public readonly bool DealDamageWhileHolding;
        public readonly float StartingTickDamage;
        public readonly float DamageIncreasePerTick;
        public readonly float DamageTickInterval;
        public readonly float MaximumTickDamage;
        public readonly float PullStrength;
        public readonly float PullSpeed;
        public readonly float MaximumHorizontalPullSpeed;
        public readonly float MaximumVerticalPullSpeed;
        public readonly float PullStopDistance;
        public readonly float PullAcceleration;
        public readonly Vector3 HoldOffset;
        public readonly float HoldPositionStrength;
        public readonly float MaximumPlayerLift;
        public readonly float GroundClearance;
        public readonly float HoldStopDistance;
        public readonly bool ReleasePlayerOutsideBoss;
        public readonly float ReleaseHorizontalDistance;
        public readonly float ReleaseVerticalSpeed;
        public readonly float ReleaseControlLockDuration;
        public readonly Color DarkTint;

        public RuntimeConfig(
            float pullDuration,
            float holdDuration,
            float releaseDuration,
            bool dealInitialDamage,
            float initialDamage,
            bool dealDamageWhileHolding,
            float startingTickDamage,
            float damageIncreasePerTick,
            float damageTickInterval,
            float maximumTickDamage,
            float pullStrength,
            float pullSpeed,
            float maximumHorizontalPullSpeed,
            float maximumVerticalPullSpeed,
            float pullStopDistance,
            float pullAcceleration,
            Vector3 holdOffset,
            float holdPositionStrength,
            float maximumPlayerLift,
            float groundClearance,
            float holdStopDistance,
            bool releasePlayerOutsideBoss,
            float releaseHorizontalDistance,
            float releaseVerticalSpeed,
            float releaseControlLockDuration,
            Color darkTint)
        {
            PullDuration = pullDuration;
            HoldDuration = holdDuration;
            ReleaseDuration = releaseDuration;
            DealInitialDamage = dealInitialDamage;
            InitialDamage = initialDamage;
            DealDamageWhileHolding = dealDamageWhileHolding;
            StartingTickDamage = startingTickDamage;
            DamageIncreasePerTick = damageIncreasePerTick;
            DamageTickInterval = damageTickInterval;
            MaximumTickDamage = maximumTickDamage;
            PullStrength = pullStrength;
            PullSpeed = pullSpeed;
            MaximumHorizontalPullSpeed = maximumHorizontalPullSpeed;
            MaximumVerticalPullSpeed = maximumVerticalPullSpeed;
            PullStopDistance = pullStopDistance;
            PullAcceleration = pullAcceleration;
            HoldOffset = holdOffset;
            HoldPositionStrength = holdPositionStrength;
            MaximumPlayerLift = maximumPlayerLift;
            GroundClearance = groundClearance;
            HoldStopDistance = holdStopDistance;
            ReleasePlayerOutsideBoss = releasePlayerOutsideBoss;
            ReleaseHorizontalDistance = releaseHorizontalDistance;
            ReleaseVerticalSpeed = releaseVerticalSpeed;
            ReleaseControlLockDuration = releaseControlLockDuration;
            DarkTint = darkTint;
        }

        public float TotalDuration => Mathf.Max(0f, PullDuration) + Mathf.Max(0f, HoldDuration) + Mathf.Max(0f, ReleaseDuration);
    }

    [Header("Selection")]
    [SerializeField] private bool enableSkill = true;
    [SerializeField, Range(0f, 1f)] private float selectionChance = 0.35f;
    [SerializeField] private float minimumRange = 0f;
    [SerializeField] private float maximumRange = 2.2f;

    [Header("Cooldown")]
    [SerializeField] private float initialDelay = 3.0f;
    [SerializeField] private float cooldown = 10.0f;

    [Header("Timing")]
    [SerializeField] private float windupDuration = 0.25f;
    [SerializeField] private float pullDuration = 0.45f;
    [SerializeField] private float holdDuration = 2.55f;
    [SerializeField] private float releaseDuration = 0f;

    [Header("Damage")]
    [SerializeField] private bool dealInitialDamage = false;
    [SerializeField] private float initialDamage = 0f;
    [SerializeField] private bool dealDamageWhileHolding = true;
    [SerializeField] private float startingTickDamage = 5f;
    [SerializeField] private float damageIncreasePerTick = 3f;
    [SerializeField] private float damageTickInterval = 0.75f;
    [SerializeField] private float maximumTickDamage = 20f;

    [Header("Pull")]
    [SerializeField] private float pullStrength = 12f;
    [SerializeField] private float pullSpeed = 12f;
    [SerializeField] private float maximumHorizontalPullSpeed = 12f;
    [SerializeField] private float maximumVerticalPullSpeed = 8f;
    [SerializeField] private float pullStopDistance = 0.15f;
    [SerializeField] private float pullAcceleration = 0f;

    [Header("Hold")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0f, 1.3f, 0f);
    [SerializeField] private float holdPositionStrength = 12f;
    [SerializeField] private float maximumPlayerLift = 2.5f;
    [SerializeField] private float groundClearance = 0.05f;
    [SerializeField] private float holdStopDistance = 0.08f;

    [Header("Boss Body Override")]
    [SerializeField] private bool keepBossGroundedDuringDevour = true;
    [SerializeField] private bool makeBossKinematicDuringDevour = true;
    [SerializeField] private bool disableBossGravityDuringDevour = true;
    [SerializeField] private bool ignoreVerticalPlayerFollow = true;
    [SerializeField] private float maximumAllowedVerticalDrift = 0.02f;

    [Header("Release")]
    [SerializeField] private bool releasePlayerOutsideBoss = true;
    [SerializeField] private float releaseHorizontalDistance = 0.6f;
    [SerializeField] private float releaseVerticalSpeed = 0f;
    [SerializeField] private float releaseControlLockDuration = 0f;

    [Header("Visual")]
    [SerializeField] private Color darkTint = new Color(0.35f, 0.35f, 0.35f, 1f);

    public bool EnableSkill => enableSkill;
    public float SelectionChance => selectionChance;
    public float MinimumRange => minimumRange;
    public float MaximumRange => maximumRange;
    public float InitialDelay => initialDelay;
    public float Cooldown => cooldown;
    public float WindupDuration => windupDuration;
    public float PullDuration => pullDuration;
    public float HoldDuration => holdDuration;
    public float ReleaseDuration => releaseDuration;
    public bool DealInitialDamage => dealInitialDamage;
    public float InitialDamage => initialDamage;
    public bool DealDamageWhileHolding => dealDamageWhileHolding;
    public float StartingTickDamage => startingTickDamage;
    public float DamageIncreasePerTick => damageIncreasePerTick;
    public float DamageTickInterval => damageTickInterval;
    public float MaximumTickDamage => maximumTickDamage;
    public float PullStrength => pullStrength;
    public float PullSpeed => pullSpeed;
    public float MaximumHorizontalPullSpeed => maximumHorizontalPullSpeed;
    public float MaximumVerticalPullSpeed => maximumVerticalPullSpeed;
    public float PullStopDistance => pullStopDistance;
    public float PullAcceleration => pullAcceleration;
    public Vector3 HoldOffset => holdOffset;
    public float HoldPositionStrength => holdPositionStrength;
    public float MaximumPlayerLift => maximumPlayerLift;
    public float GroundClearance => groundClearance;
    public float HoldStopDistance => holdStopDistance;
    public bool KeepBossGroundedDuringDevour => keepBossGroundedDuringDevour;
    public bool MakeBossKinematicDuringDevour => makeBossKinematicDuringDevour;
    public bool DisableBossGravityDuringDevour => disableBossGravityDuringDevour;
    public bool IgnoreVerticalPlayerFollow => ignoreVerticalPlayerFollow;
    public float MaximumAllowedVerticalDrift => maximumAllowedVerticalDrift;
    public bool ReleasePlayerOutsideBoss => releasePlayerOutsideBoss;
    public float ReleaseHorizontalDistance => releaseHorizontalDistance;
    public float ReleaseVerticalSpeed => releaseVerticalSpeed;
    public float ReleaseControlLockDuration => releaseControlLockDuration;
    public Color DarkTint => darkTint;

    public RuntimeConfig BuildRuntimeConfig()
    {
        return new RuntimeConfig(
            Mathf.Max(0f, pullDuration),
            Mathf.Max(0f, holdDuration),
            Mathf.Max(0f, releaseDuration),
            dealInitialDamage,
            Mathf.Max(0f, initialDamage),
            dealDamageWhileHolding,
            Mathf.Max(0f, startingTickDamage),
            Mathf.Max(0f, damageIncreasePerTick),
            Mathf.Max(0.05f, damageTickInterval),
            Mathf.Max(Mathf.Max(0f, startingTickDamage), maximumTickDamage),
            Mathf.Max(0f, pullStrength),
            Mathf.Max(0f, pullSpeed),
            Mathf.Max(0f, maximumHorizontalPullSpeed),
            Mathf.Max(0f, maximumVerticalPullSpeed),
            Mathf.Max(0f, pullStopDistance),
            Mathf.Max(0f, pullAcceleration),
            holdOffset,
            Mathf.Max(0f, holdPositionStrength),
            Mathf.Max(0f, maximumPlayerLift),
            Mathf.Max(0f, groundClearance),
            Mathf.Max(0f, holdStopDistance),
            releasePlayerOutsideBoss,
            Mathf.Max(0f, releaseHorizontalDistance),
            releaseVerticalSpeed,
            Mathf.Max(0f, releaseControlLockDuration),
            darkTint);
    }

    public string BuildConfigTrace(string source)
    {
        return "[BossDevourConfigTrace] " +
               "component=" + name +
               " source=" + source +
               " componentInstanceId=" + GetInstanceID() +
               " enabled=" + enableSkill +
               " cooldown=" + cooldown.ToString("F2") +
               " selectionChance=" + selectionChance.ToString("F2") +
               " minRange=" + minimumRange.ToString("F2") +
               " maxRange=" + maximumRange.ToString("F2") +
               " windupDuration=" + windupDuration.ToString("F2") +
               " pullDuration=" + pullDuration.ToString("F2") +
               " holdDuration=" + holdDuration.ToString("F2") +
               " totalEffectiveDuration=" + (Mathf.Max(0f, pullDuration) + Mathf.Max(0f, holdDuration) + Mathf.Max(0f, releaseDuration)).ToString("F2") +
               " pullStrength=" + pullStrength.ToString("F2") +
               " pullSpeed=" + pullSpeed.ToString("F2") +
               " maximumHorizontalPullSpeed=" + maximumHorizontalPullSpeed.ToString("F2") +
               " maximumVerticalPullSpeed=" + maximumVerticalPullSpeed.ToString("F2") +
               " holdOffset=" + holdOffset +
               " maximumPlayerLift=" + maximumPlayerLift.ToString("F2") +
               " groundClearance=" + groundClearance.ToString("F2") +
               " dealInitialDamage=" + dealInitialDamage +
               " initialDamage=" + initialDamage.ToString("F2") +
               " dealDamageWhileHolding=" + dealDamageWhileHolding +
               " startingTickDamage=" + startingTickDamage.ToString("F2") +
               " damageIncreasePerTick=" + damageIncreasePerTick.ToString("F2") +
               " damageTickInterval=" + damageTickInterval.ToString("F2") +
               " maximumTickDamage=" + maximumTickDamage.ToString("F2") +
               " keepBossGroundedDuringDevour=" + keepBossGroundedDuringDevour;
    }

    private void OnValidate()
    {
        initialDamage = Mathf.Max(0f, initialDamage);
        startingTickDamage = Mathf.Max(0f, startingTickDamage);
        damageIncreasePerTick = Mathf.Max(0f, damageIncreasePerTick);
        damageTickInterval = Mathf.Max(0.1f, damageTickInterval);
        maximumTickDamage = Mathf.Max(startingTickDamage, maximumTickDamage);
    }
}
