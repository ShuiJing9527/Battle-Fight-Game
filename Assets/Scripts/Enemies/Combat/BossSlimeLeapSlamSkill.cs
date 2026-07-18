using UnityEngine;

public class BossSlimeLeapSlamSkill : MonoBehaviour
{
    [Header("Selection")]
    [SerializeField] private bool enableLeapSlam = true;
    [SerializeField] private float initialDelay = 2.0f;
    [SerializeField] private float cooldown = 6.0f;
    [SerializeField, Range(0f, 1f)] private float triggerChance = 0.45f;
    [SerializeField] private float minimumRange = 2.0f;
    [SerializeField] private float maximumRange = 8.0f;
    [SerializeField] private bool allowInMeleeRange = true;
    [SerializeField, Range(0f, 1f)] private float meleeSelectionChance = 0.3f;
    [SerializeField, Range(0f, 1f)] private float veryCloseSelectionChance = 0.6f;
    [SerializeField] private float veryCloseDistance = 2f;

    [Header("Leap Movement")]
    [SerializeField] private float windupTime = 0.35f;
    [SerializeField] private float travelTime = 0.65f;
    [SerializeField] private float recoverTime = 0.45f;
    [SerializeField] private float leapHeight = 3.0f;
    [SerializeField] private float minimumHorizontalTravel = 0f;
    [SerializeField] private float closeTravelDistance = 1f;
    [SerializeField] private float maximumHorizontalDistance = 12f;

    [Header("Target Prediction")]
    [SerializeField] private bool enableTargetPrediction = true;
    [SerializeField] private float predictionTime = 0.45f;
    [SerializeField] private float maximumPredictionDistance = 4f;

    [Header("Landing Damage")]
    [SerializeField] private float landingRadius = 2.2f;
    [SerializeField] private float landingDamage = 120f;
    [SerializeField] private float damageMultiplier = 1.8f;

    [Header("Falling Landing Impact")]
    [SerializeField] private float fallingImpactMinimumHeight = 1.5f;
    [SerializeField] private float fallingImpactMinimumDownwardSpeed = 2f;

    [Header("Falling Impact Height Trigger")]
    [SerializeField] private bool enableFallingHeightTrigger = true;
    [SerializeField] private float fallingImpactTriggerHeight = 1.2f;
    [SerializeField] private float fallingImpactMaximumTriggerHeight = 2.5f;
    [SerializeField] private float fallingImpactRequiredDownwardSpeed = 0.5f;
    [SerializeField] private float fallingImpactPlayerCheckRadius = 4f;
    [SerializeField] private LayerMask fallingImpactGroundMask = ~0;

    [Header("Player Launch")]
    [SerializeField] private float launchHorizontalSpeed = 10f;
    [SerializeField] private float launchVerticalSpeed = 7f;
    [SerializeField] private float minimumEscapeDistance = 1.5f;
    [SerializeField] private float maximumSeparationOffset = 0.35f;
    [SerializeField, Min(0f)] private float launchInputLockDuration = 0.3f;
    [SerializeField, Min(0f)] private float centerOverlapHorizontalThreshold = 0.25f;
    [SerializeField] private float centerFallbackAngle = 35f;
    [SerializeField, Min(0f)] private float centerSeparationHorizontal = 0.25f;
    [SerializeField, Min(0f)] private float centerSeparationUpward = 0.12f;
    [SerializeField, Min(0f)] private float maximumInitialSeparation = 0.35f;
    [SerializeField, Min(0f)] private float launchCollisionSeparationSkin = 0.08f;
    [SerializeField, Min(0f)] private float launchGroundClearance = 0.05f;
    [SerializeField, Min(0f)] private float launchMaximumSafeRepositionDistance = 1.5f;
    [SerializeField, Min(0f)] private float launchBossCollisionRestoreDistance = 0.75f;
    [SerializeField, Min(0f)] private float launchBossCollisionIgnoreMaximumDuration = 0.5f;
    [SerializeField] private LayerMask launchSafeGroundMask = ~0;

    [Header("Forced Airborne Impact")]
    [SerializeField] private bool enableForcedAirborneImpact = true;
    [SerializeField] private float armHeight = 1.5f;
    [SerializeField] private float triggerHeight = 1.2f;
    [SerializeField] private float playerCheckRadius = 4f;
    [SerializeField] private float timeout = 2f;

    [Header("Landing VFX")]
    [SerializeField] private bool enableLandingVfx = true;
    [SerializeField] private GameObject landingVfxPrefab;
    [SerializeField] private Vector3 landingVfxOffset = Vector3.zero;
    [SerializeField, Min(0f)] private float landingVfxLifetime = 2f;

    [Header("Leap Slam Audio")]
    [Tooltip("Boss 开始发动 LeapSlam 时播放的音效。留空则不播放。")]
    [SerializeField] private AudioClip leapSlamActivationAudioClip;
    [Tooltip("LeapSlam 触发音效音量。")]
    [SerializeField, Range(0f, 1f)] private float leapSlamActivationAudioVolume = 1f;

    public bool EnableLeapSlam => enableLeapSlam;
    public float InitialDelay => initialDelay;
    public float Cooldown => cooldown;
    public float TriggerChance => triggerChance;
    public float MinimumRange => minimumRange;
    public float MaximumRange => maximumRange;
    public bool AllowInMeleeRange => allowInMeleeRange;
    public float MeleeSelectionChance => meleeSelectionChance;
    public float VeryCloseSelectionChance => veryCloseSelectionChance;
    public float VeryCloseDistance => veryCloseDistance;
    public float WindupTime => windupTime;
    public float TravelTime => travelTime;
    public float RecoverTime => recoverTime;
    public float LeapHeight => leapHeight;
    public float MinimumHorizontalTravel => minimumHorizontalTravel;
    public float CloseTravelDistance => closeTravelDistance;
    public float MaximumHorizontalDistance => maximumHorizontalDistance;
    public bool EnableTargetPrediction => enableTargetPrediction;
    public float PredictionTime => predictionTime;
    public float MaximumPredictionDistance => maximumPredictionDistance;
    public float LandingRadius => landingRadius;
    public float LandingDamage => landingDamage;
    public float DamageMultiplier => damageMultiplier;
    public float FallingImpactMinimumHeight => fallingImpactMinimumHeight;
    public float FallingImpactMinimumDownwardSpeed => fallingImpactMinimumDownwardSpeed;
    public bool EnableFallingHeightTrigger => enableFallingHeightTrigger;
    public float FallingImpactTriggerHeight => fallingImpactTriggerHeight;
    public float FallingImpactMaximumTriggerHeight => fallingImpactMaximumTriggerHeight;
    public float FallingImpactRequiredDownwardSpeed => fallingImpactRequiredDownwardSpeed;
    public float FallingImpactPlayerCheckRadius => fallingImpactPlayerCheckRadius;
    public LayerMask FallingImpactGroundMask => fallingImpactGroundMask;
    public float LaunchHorizontalSpeed => launchHorizontalSpeed;
    public float LaunchVerticalSpeed => launchVerticalSpeed;
    public float MinimumEscapeDistance => minimumEscapeDistance;
    public float MaximumSeparationOffset => maximumSeparationOffset;
    public float LaunchInputLockDuration => launchInputLockDuration;
    public float CenterOverlapHorizontalThreshold => centerOverlapHorizontalThreshold;
    public float CenterFallbackAngle => centerFallbackAngle;
    public float CenterSeparationHorizontal => centerSeparationHorizontal;
    public float CenterSeparationUpward => centerSeparationUpward;
    public float MaximumInitialSeparation => maximumInitialSeparation;
    public float LaunchCollisionSeparationSkin => launchCollisionSeparationSkin;
    public float LaunchGroundClearance => launchGroundClearance;
    public float LaunchMaximumSafeRepositionDistance => launchMaximumSafeRepositionDistance;
    public float LaunchBossCollisionRestoreDistance => launchBossCollisionRestoreDistance;
    public float LaunchBossCollisionIgnoreMaximumDuration => launchBossCollisionIgnoreMaximumDuration;
    public LayerMask LaunchSafeGroundMask => launchSafeGroundMask;
    public bool EnableForcedAirborneImpact => enableForcedAirborneImpact;
    public float ForcedAirborneArmHeight => armHeight;
    public float ForcedAirborneTriggerHeight => triggerHeight;
    public float ForcedAirbornePlayerCheckRadius => playerCheckRadius;
    public float ForcedAirborneTimeout => timeout;
    public bool EnableLandingVfx => enableLandingVfx;
    public GameObject LandingVfxPrefab => landingVfxPrefab;
    public Vector3 LandingVfxOffset => landingVfxOffset;
    public float LandingVfxLifetime => landingVfxLifetime;
    public AudioClip ActivationAudioClip => leapSlamActivationAudioClip;
    public float ActivationAudioVolume => leapSlamActivationAudioVolume;

    public string BuildConfigTrace(string source)
    {
        return "[BossLeapSlamConfigTrace] " +
               "component=" + name +
               " source=" + source +
               " instanceId=" + GetInstanceID() +
               " enabled=" + enableLeapSlam +
               " horizontal=" + launchHorizontalSpeed.ToString("F2") +
               " vertical=" + launchVerticalSpeed.ToString("F2") +
               " landingDamage=" + landingDamage.ToString("F2") +
               " landingRadius=" + landingRadius.ToString("F2") +
               " leapHeight=" + leapHeight.ToString("F2") +
               " windup=" + windupTime.ToString("F2") +
               " travel=" + travelTime.ToString("F2") +
               " recover=" + recoverTime.ToString("F2");
    }
}
