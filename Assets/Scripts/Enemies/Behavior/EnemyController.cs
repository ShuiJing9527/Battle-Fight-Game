using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private const float MovementZeroEpsilon = 0.001f;
    private const float AttackRecoveryDurationSeconds = 0.7f;
    private const float ProjectileSpawnHeightOffset = 0.8f;
    private const float ProjectileSpawnForwardOffset = 0.8f;
    private const float BossProjectileScale = 0.45f;
    private const float NormalProjectileScale = 0.28f;
    private const float EdgeDistanceEpsilon = 0.02f;
    private const float BossDevourUnexpectedVerticalMovementThreshold = 0.02f;
    private const float BossDevourFlightTraceInterval = 0.15f;
    private const string DefaultProjectilePrefabResourcePath = "Prefabs/Enemy/BossProjectile";

    [Header("Target")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private string playerTag = "Player";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float stopDistance = 0.8f;
    [SerializeField] private float maxHorizontalMoveSpeed = 3f;
    [SerializeField] private bool faceMoveDirection = false;
    [SerializeField] private bool keepFlatRotation = true;
    [SerializeField] private bool enableEnemySoftAvoidance = true;
    [SerializeField] private float enemySeparationRadius = 1.2f;
    [SerializeField] private float enemySeparationWeight = 0.6f;
    [SerializeField] private int enemySeparationMaxNeighbors = 8;
    [SerializeField] private float maxVerticalTargetDifference = 1.5f;
    [SerializeField] private float maxVerticalVelocity = 4f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 1.35f;
    [SerializeField] private float attackHitRange = 1.6f;
    [SerializeField] private float attackCooldown = 1.1f;
    [SerializeField] private float attackDamage = 3f;
    [SerializeField] private MonsterAttackStyle attackStyle = MonsterAttackStyle.Melee;
    [SerializeField] private float projectileSpeed = 8f;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float attackIntervalMultiplier = 1f;
    [SerializeField] private float outgoingDamageMultiplier = 1f;
    [SerializeField] private float meleeHitAngle = 100f;
    [SerializeField] private float meleeHitForwardOffset = 0f;
    [SerializeField] private float closeHitRadius = 0f;
    [SerializeField] private float meleeBodyContactRadius = 0.45f;
    [SerializeField] private bool requireGroundedToAttack = true;
    [SerializeField] private float groundedProbeDistance = 0.2f;
    [SerializeField] private float maxVerticalAttackDifference = 0.75f;
    [SerializeField] private float maxHorizontalAttackDistance = 1.35f;
    [SerializeField, Min(0f)] private float groundedAttackGraceTime = 0f;
    [SerializeField] private float normalMeleeHitRadius = 0.8f;
    [SerializeField] private float eliteMeleeHitRadius = 1.2f;
    [SerializeField] private float normalMeleeHitForwardOffset = 0.35f;
    [SerializeField] private float eliteMeleeHitForwardOffset = 0.5f;
    [SerializeField] private float normalMeleeHitHeight = 0.5f;
    [SerializeField] private float eliteMeleeHitHeight = 0.6f;
    [SerializeField] private float bossMeleeAttackRange = 2.0f;
    [SerializeField] private float bossRangedMinRange = 3.0f;
    [SerializeField] private float bossRangedMaxRange = 10.0f;
    [SerializeField] private float bossRangedAttackCooldown = 3.0f;
    [SerializeField] private float bossRangedCastTime = 0.6f;
    [SerializeField] private float bossMeleeHitRadius = 1.8f;
    [SerializeField] private float bossMeleeHitForwardOffset = 0.8f;
    [SerializeField] private float bossMeleeHitHeight = 0.8f;
    [SerializeField] private bool enableBossRangedCastAnimation = true;
    [SerializeField] private float bossRangedCastWindupTime = 0.25f;
    [SerializeField] private float bossRangedCastReleaseTime = 0.15f;
    [SerializeField] private float bossRangedVisualRecoverTime = 0.25f;
    [SerializeField] private float bossRangedWindupSquashX = 0.85f;
    [SerializeField] private float bossRangedWindupStretchY = 1.1f;
    [SerializeField] private float bossRangedReleaseStretchX = 1.25f;
    [SerializeField] private float bossRangedReleaseSquashY = 0.85f;
    [SerializeField] private float bossRangedVisualLeanDistance = 0.25f;
    [SerializeField] private float bossProjectileSpawnForwardOffset = 0.8f;
    [SerializeField] private float bossProjectileSpawnHeight = 0.6f;
    [SerializeField] private Transform bossProjectileSpawnPoint;
    [SerializeField] private ParticleSystem bossRangedMuzzleParticle;
    [SerializeField] private bool useArcTrajectory = true;
    [SerializeField] private float arcHeight = 2.0f;
    [SerializeField] private float arcTravelTime = 0.9f;
    [SerializeField] private float targetPredictionTime = 0.25f;
    [SerializeField, Min(1)] private int minSlimeProjectileCount = 1;
    [SerializeField, Min(1)] private int maxSlimeProjectileCount = 5;
    [SerializeField, Min(0f)] private float slimeProjectileInterval = 0.15f;
    [SerializeField, Range(0f, 15f)] private float slimeProjectileHorizontalScatterAngle = 6f;
    [SerializeField, Range(0f, 8f)] private float slimeProjectileVerticalScatterAngle = 3f;
    [SerializeField] private bool debugBossSlimeProjectile = false;

    [Header("Projectile Attack Audio")]
    [Tooltip("敌人实际发射粘液弹时播放的音效。留空则不播放。")]
    [SerializeField] private AudioClip projectileFireAudioClip;
    [Tooltip("粘液弹发射音效音量。")]
    [SerializeField, Range(0f, 1f)] private float projectileFireAudioVolume = 1f;

    [Header("Boss Skill Components")]
    [SerializeField] private BossSlimeLeapSlamSkill leapSlamSkill;
    [SerializeField] private BossSlimeDevourSkill devourSkill;

    [Header("Boss Skill - Split Merge")]
    [SerializeField] private bool enableBossTimedSplit = true;
    [SerializeField] private float bossSplitInitialDelay = 4.0f;
    [SerializeField] private float bossSplitCooldown = 14.0f;
    [SerializeField, Range(0f, 1f)] private float bossSplitTriggerChance = 0.30f;
    [SerializeField] private int bossSplitChildCount = 2;
    [SerializeField] private float bossSplitScatterRadius = 2.0f;
    [SerializeField] private float bossSplitDuration = 8.0f;
    [SerializeField, Range(0.01f, 1f)] private float bossSplitChildHealthPercentOfBoss = 0.65f;
    [SerializeField] private float bossSplitWindupTime = 0.35f;
    [SerializeField] private float bossSplitMergeRecoverTime = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool debugMeleeHitCheck = false;
    [SerializeField] private bool debugAttackDiagnostics = false;
    [SerializeField] private bool debugSpeedDiagnostics = false;
    [SerializeField] private bool debugChaseDiagnostics = true;
    [SerializeField] private bool debugAttackStateTransitions = false;
    [SerializeField] private bool debugSlimeAttackLogs = false;
    [SerializeField] private bool debugBossMeleeHit = true;
    [SerializeField, Min(0.1f)] private float debugAttackLogInterval = 0.3f;
    [SerializeField, Min(0.1f)] private float debugSpeedLogInterval = 1f;
    [SerializeField, Min(0.05f)] private float targetResolveRetryInterval = 0.25f;

    private Rigidbody rb;
    private MonsterIdentity monsterIdentity;
    private Player2Bootstrap playerBootstrap;
    private SlimeAnimationController slimeAnimation;
    private EnemyDebuffReceiver debuffReceiver;
    private CombatStats combatStats;
    private AudioSource skillAudioSource;
    private Collider meleeEnemyCollider;
    private SpriteRenderer meleeEnemySpriteRenderer;
    private Quaternion initialRotation;
    private float nextAttackTime;
    private Transform pendingAttackTarget;
    private bool attackInProgress;
    private float lastLoggedMoveMultiplier = -1f;
    private float lastLoggedAttackMultiplier = -1f;
    private float nextAttackDiagnosticTime;
    private float nextSpeedDiagnosticTime;
    private float nextChaseDiagnosticTime;
    private float nextTargetResolveTime;
    private float nextBossRangedDecisionLogTime;
    private float nextBossMeleeDecisionLogTime;
    private float nextEnemyMeleeDecisionLogTime;
    private float nextBossLaunchDebugTime;
    private Vector3 lastGroundProbeOrigin;
    private bool lastGroundProbeHit;
    private string lastGroundHitName = "None";
    private int lastGroundHitLayer = -1;
    private float lastGroundHitY;
    private float lastGroundProbeCastDistance;
    private Collider[] separationHits;
    private Collider[] bossMeleeHitResults;
    private static GameObject cachedDefaultProjectilePrefab;
    private float lastAttackTime = -1f;
    private float nextBossRangedAttackTime;
    private float lastGroundedTime = float.NegativeInfinity;
    private EnemyAttackRuntimeState lastLoggedAttackState = EnemyAttackRuntimeState.None;
    private Collider lastLoggedMeleeCollider;
    private bool hasLoggedRuntimeAttackConfig;
    private Coroutine bossRangedAttackRoutine;
    private Coroutine bossSpecialAttackRoutine;
    private Coroutine bossAttackRecoveryRoutine;
    private Coroutine activeBossAttackRoutine;
    private BossAttackKind activeBossAttackKind = BossAttackKind.None;
    private bool bossActionLocked;
    private int bossAttackSequenceId;
    private int activeBossActionSequenceId;
    private bool attackHitFrameTriggeredThisAttack;
    private string lastMeleeAttackResult = "none";
    private float nextBossLeapAttackTime;
    private float nextBossSplitAttackTime;
    private float nextBossDevourAttackTime;
    private readonly List<GameObject> activeBossSplitChildren = new List<GameObject>();
    private Renderer[] bossSplitRenderers;
    private Collider[] bossSplitColliders;
    private bool[] bossSplitRendererEnabledStates;
    private bool[] bossSplitColliderEnabledStates;
    private bool bossBodyHiddenForSplit;
    private bool bossRigidbodyWasKinematicBeforeSplit;
    private bool bossLeapBodyOverrideActive;
    private bool bossLeapBodyUseGravityBefore;
    private bool bossLeapBodyIsKinematicBefore;
    private RigidbodyConstraints bossLeapBodyConstraintsBefore;
    private bool bossDevourBodyOverrideActive;
    private bool bossDevourBodyUseGravityBefore;
    private bool bossDevourBodyIsKinematicBefore;
    private RigidbodyConstraints bossDevourBodyConstraintsBefore;
    private Vector3 bossDevourGroundAnchorPosition;
    private float bossDevourGroundedRootY;
    private float nextBossDevourFlightTraceTime;
    private int bossLandingImpactSequenceId;
    private Collider[] bossLeapLandingHitResults;
    private bool bossWasGroundedLastFixedUpdate;
    private bool bossWasAirborne;
    private bool bossFallingImpactArmed;
    private bool bossFallingImpactTriggered;
    private float bossAirborneStartY;
    private float bossHighestAirborneY;
    private float bossLastGroundedY;
    private int bossCurrentAirborneSequenceId;
    private float nextBossCombatDecisionTraceTime;
    private float nextBossLandingHeightTraceTime;
    private bool forcedAirborneImpactArmed;
    private bool forcedAirborneImpactConsumed;
    private float forcedAirborneHighestHeight;
    private float forcedAirborneStartTime;
    private int forcedAirborneSequenceId;
    private bool bossLandingImpactAwaitGroundReset;
    private readonly HashSet<int> bossLaunchedTargetIdsForImpactSequence = new HashSet<int>();
    private int bossLaunchedTargetSequenceId;
    private bool hasLoggedLeapSlamConfigTrace;
    private bool hasLoggedDevourConfigTrace;
    private BossSlimeDevourSkill.RuntimeConfig activeBossDevourRuntimeConfig;
    private bool leapSlamSkillRuntimeFallbackCreated;
    private bool devourSkillRuntimeFallbackCreated;
    private readonly Dictionary<int, BossPlayerCollisionIsolationState> activeBossPlayerCollisionIsolations = new Dictionary<int, BossPlayerCollisionIsolationState>();

    private enum EnemyAttackRuntimeState
    {
        None,
        NoTarget,
        Chase,
        HoldPosition,
        AttackReady,
        AttackInProgress,
        AttackRecovery
    }

    private enum BossAttackKind
    {
        None,
        Melee,
        Ranged,
        LeapSlam,
        TimedSplit,
        Devour
    }

    private enum BossLandingImpactSource
    {
        LeapSlam,
        AirborneFall,
        AirbornePlayerContact,
        FallingHeightThreshold,
        ForcedAirborneHeight
    }

    private sealed class BossPlayerCollisionIsolationState
    {
        public Collider BossCollider;
        public Collider PlayerCollider;
        public Rigidbody PlayerBody;
        public float StartedAt;
        public int SequenceId;
        public Coroutine RestoreRoutine;
    }

    public float BaseMoveSpeed => moveSpeed;
    public Transform CurrentTarget => playerTarget;
    public MonsterAttackStyle CurrentAttackStyle => attackStyle;
    public string CurrentBossAttackKindName => activeBossAttackKind.ToString();
    private bool enableBossLeapSlam => leapSlamSkill != null && leapSlamSkill.EnableLeapSlam;
    private float bossLeapInitialDelay => leapSlamSkill != null ? leapSlamSkill.InitialDelay : 0f;
    private float bossLeapCooldown => leapSlamSkill != null ? leapSlamSkill.Cooldown : 0f;
    private float bossLeapTriggerChance => leapSlamSkill != null ? leapSlamSkill.TriggerChance : 0f;
    private float bossLeapMinRange => leapSlamSkill != null ? leapSlamSkill.MinimumRange : 0f;
    private float bossLeapMaxRange => leapSlamSkill != null ? leapSlamSkill.MaximumRange : 0f;
    private float bossLeapWindupTime => leapSlamSkill != null ? leapSlamSkill.WindupTime : 0f;
    private float bossLeapTravelTime => leapSlamSkill != null ? leapSlamSkill.TravelTime : 0f;
    private float bossLeapRecoverTime => leapSlamSkill != null ? leapSlamSkill.RecoverTime : 0f;
    private float bossLeapHeight => leapSlamSkill != null ? leapSlamSkill.LeapHeight : 0f;
    private float bossLeapLandingRadius => leapSlamSkill != null ? leapSlamSkill.LandingRadius : 0f;
    private float bossLeapDamageMultiplier => leapSlamSkill != null ? leapSlamSkill.DamageMultiplier : 1f;
    private float BossFallingImpactMinimumHeight => leapSlamSkill != null ? leapSlamSkill.FallingImpactMinimumHeight : 1.5f;
    private float BossFallingImpactMinimumDownwardSpeed => leapSlamSkill != null ? leapSlamSkill.FallingImpactMinimumDownwardSpeed : 2f;
    private bool EnableBossFallingHeightTrigger => leapSlamSkill != null && leapSlamSkill.EnableFallingHeightTrigger;
    private float BossFallingImpactTriggerHeight => leapSlamSkill != null ? leapSlamSkill.FallingImpactTriggerHeight : 1.2f;
    private float BossFallingImpactMaximumTriggerHeight => leapSlamSkill != null ? leapSlamSkill.FallingImpactMaximumTriggerHeight : 2.5f;
    private float BossFallingImpactRequiredDownwardSpeed => leapSlamSkill != null ? leapSlamSkill.FallingImpactRequiredDownwardSpeed : 0.5f;
    private float BossFallingImpactPlayerCheckRadius => leapSlamSkill != null ? leapSlamSkill.FallingImpactPlayerCheckRadius : 4f;
    private LayerMask BossFallingImpactGroundMask => leapSlamSkill != null ? leapSlamSkill.FallingImpactGroundMask : ~0;
    private bool allowLeapSlamInMeleeRange => leapSlamSkill != null && leapSlamSkill.AllowInMeleeRange;
    private float bossLeapSlamMeleeChance => leapSlamSkill != null ? leapSlamSkill.MeleeSelectionChance : 0f;
    private float bossLeapSlamVeryCloseChance => leapSlamSkill != null ? leapSlamSkill.VeryCloseSelectionChance : 0f;
    private float bossLeapSlamVeryCloseDistance => leapSlamSkill != null ? leapSlamSkill.VeryCloseDistance : 0f;
    private float bossLeapSlamMinimumHorizontalTravel => leapSlamSkill != null ? leapSlamSkill.MinimumHorizontalTravel : 0f;
    private float bossLeapSlamCloseTravelDistance => leapSlamSkill != null ? leapSlamSkill.CloseTravelDistance : 0f;
    private float bossLeapSlamMaximumHorizontalDistance => leapSlamSkill != null ? leapSlamSkill.MaximumHorizontalDistance : 0f;
    private bool enableLeapSlamTargetPrediction => leapSlamSkill != null && leapSlamSkill.EnableTargetPrediction;
    private float bossLeapSlamPredictionTime => leapSlamSkill != null ? leapSlamSkill.PredictionTime : 0f;
    private float bossLeapSlamMaximumPredictionDistance => leapSlamSkill != null ? leapSlamSkill.MaximumPredictionDistance : 0f;
    private float bossLeapSlamLandingDamage => leapSlamSkill != null ? leapSlamSkill.LandingDamage : 0f;
    private float bossLeapSlamKnockbackHorizontal => leapSlamSkill != null ? leapSlamSkill.LaunchHorizontalSpeed : 0f;
    private float bossLeapSlamKnockbackVertical => leapSlamSkill != null ? leapSlamSkill.LaunchVerticalSpeed : 0f;
    private float bossLeapSlamMinimumEscapeDistance => leapSlamSkill != null ? leapSlamSkill.MinimumEscapeDistance : 0f;
    private float bossLeapSlamMaximumLaunchSeparation => leapSlamSkill != null ? leapSlamSkill.MaximumSeparationOffset : 0f;
    private float bossLeapSlamLaunchInputLockDuration => leapSlamSkill != null ? leapSlamSkill.LaunchInputLockDuration : 0f;
    private float bossLeapCenterOverlapHorizontalThreshold => leapSlamSkill != null ? leapSlamSkill.CenterOverlapHorizontalThreshold : 0f;
    private float bossLeapCenterFallbackAngle => leapSlamSkill != null ? leapSlamSkill.CenterFallbackAngle : 0f;
    private float bossLeapCenterSeparationHorizontal => leapSlamSkill != null ? leapSlamSkill.CenterSeparationHorizontal : 0f;
    private float bossLeapCenterSeparationUpward => leapSlamSkill != null ? leapSlamSkill.CenterSeparationUpward : 0f;
    private float bossLeapMaximumInitialSeparation => leapSlamSkill != null ? leapSlamSkill.MaximumInitialSeparation : 0f;
    private float bossLeapLaunchCollisionSeparationSkin => leapSlamSkill != null ? leapSlamSkill.LaunchCollisionSeparationSkin : 0.08f;
    private float bossLeapLaunchGroundClearance => leapSlamSkill != null ? leapSlamSkill.LaunchGroundClearance : 0.05f;
    private float bossLeapLaunchMaximumSafeRepositionDistance => leapSlamSkill != null ? leapSlamSkill.LaunchMaximumSafeRepositionDistance : 1.5f;
    private float bossLeapLaunchBossCollisionRestoreDistance => leapSlamSkill != null ? leapSlamSkill.LaunchBossCollisionRestoreDistance : 0.75f;
    private float bossLeapLaunchCollisionIgnoreMaximumDuration => leapSlamSkill != null ? leapSlamSkill.LaunchBossCollisionIgnoreMaximumDuration : 0.5f;
    private LayerMask bossLeapLaunchSafeGroundMask => leapSlamSkill != null ? leapSlamSkill.LaunchSafeGroundMask : BossFallingImpactGroundMask;
    private bool enableForcedAirborneImpact => leapSlamSkill != null && leapSlamSkill.EnableForcedAirborneImpact;
    private float forcedAirborneImpactArmHeight => leapSlamSkill != null ? leapSlamSkill.ForcedAirborneArmHeight : 0f;
    private float forcedAirborneImpactTriggerHeight => leapSlamSkill != null ? leapSlamSkill.ForcedAirborneTriggerHeight : 0f;
    private float forcedAirborneImpactPlayerRadius => leapSlamSkill != null ? leapSlamSkill.ForcedAirbornePlayerCheckRadius : 0f;
    private float forcedAirborneImpactTimeout => leapSlamSkill != null ? leapSlamSkill.ForcedAirborneTimeout : 0f;
    private bool enableLandingVfx => leapSlamSkill != null && leapSlamSkill.EnableLandingVfx;
    private GameObject landingVfxPrefab => leapSlamSkill != null ? leapSlamSkill.LandingVfxPrefab : null;
    private Vector3 landingVfxOffset => leapSlamSkill != null ? leapSlamSkill.LandingVfxOffset : Vector3.zero;
    private float landingVfxLifetime => leapSlamSkill != null ? leapSlamSkill.LandingVfxLifetime : 0f;
    private float landingVfxScaleMultiplier => leapSlamSkill != null ? leapSlamSkill.LandingVfxScaleMultiplier : 1f;
    private float landingVfxVerticalOffset => leapSlamSkill != null ? leapSlamSkill.LandingVfxVerticalOffset : 0f;
    private float landingVfxLifetimeMultiplier => leapSlamSkill != null ? leapSlamSkill.LandingVfxLifetimeMultiplier : 1f;
    private float landingVfxEmissionMultiplier => leapSlamSkill != null ? leapSlamSkill.LandingVfxEmissionMultiplier : 1f;
    private float landingVfxParticleSizeMultiplier => leapSlamSkill != null ? leapSlamSkill.LandingVfxParticleSizeMultiplier : 1f;
    private float landingVfxSpeedMultiplier => leapSlamSkill != null ? leapSlamSkill.LandingVfxSpeedMultiplier : 1f;
    private bool enableLandingShockwave => leapSlamSkill != null && leapSlamSkill.EnableLandingShockwave;
    private float landingShockwaveScaleMultiplier => leapSlamSkill != null ? leapSlamSkill.LandingShockwaveScaleMultiplier : 1f;
    private AudioClip leapSlamActivationAudioClip => leapSlamSkill != null ? leapSlamSkill.ActivationAudioClip : null;
    private float leapSlamActivationAudioVolume => leapSlamSkill != null ? leapSlamSkill.ActivationAudioVolume : 0f;
    private bool enableBossDevour => devourSkill != null && devourSkill.EnableSkill;
    private float bossDevourInitialDelay => devourSkill != null ? devourSkill.InitialDelay : 0f;
    private float bossDevourCooldown => devourSkill != null ? devourSkill.Cooldown : 0f;
    private float bossDevourTriggerChance => devourSkill != null ? devourSkill.SelectionChance : 0f;
    private float bossDevourMinimumRange => devourSkill != null ? devourSkill.MinimumRange : 0f;
    private float bossDevourRange => devourSkill != null ? devourSkill.MaximumRange : 0f;
    private float bossDevourWindupTime => devourSkill != null ? devourSkill.WindupDuration : 0f;
    private float bossDevourPullDuration => devourSkill != null ? devourSkill.PullDuration : 0f;
    private float bossDevourHoldDuration => devourSkill != null ? devourSkill.HoldDuration : 0f;
    private float bossDevourReleaseDuration => devourSkill != null ? devourSkill.ReleaseDuration : 0f;
    private bool bossDevourDealInitialDamage => devourSkill != null && devourSkill.DealInitialDamage;
    private float bossDevourInitialDamage => devourSkill != null ? devourSkill.InitialDamage : 0f;
    private bool bossDevourDealDamageWhileHolding => devourSkill != null && devourSkill.DealDamageWhileHolding;
    private float bossDevourStartingTickDamage => devourSkill != null ? devourSkill.StartingTickDamage : 0f;
    private float bossDevourDamageIncreasePerTick => devourSkill != null ? devourSkill.DamageIncreasePerTick : 0f;
    private float bossDevourTickInterval => devourSkill != null ? devourSkill.DamageTickInterval : 1f;
    private float bossDevourMaximumTickDamage => devourSkill != null ? devourSkill.MaximumTickDamage : 0f;
    private Color bossDevourDarkTint => devourSkill != null ? devourSkill.DarkTint : Color.white;
    private Vector3 bossDevourHoldOffset => devourSkill != null ? devourSkill.HoldOffset : Vector3.zero;
    private bool bossDevourLockBossToGround => devourSkill != null && devourSkill.KeepBossGroundedDuringDevour;
    private bool bossDevourMakeBossKinematicDuringDevour => devourSkill != null && devourSkill.MakeBossKinematicDuringDevour;
    private bool bossDevourDisableBossGravityDuringDevour => devourSkill != null && devourSkill.DisableBossGravityDuringDevour;
    private bool bossDevourIgnoreVerticalPlayerFollow => devourSkill != null && devourSkill.IgnoreVerticalPlayerFollow;
    private float bossDevourMaximumPlayerLift => devourSkill != null ? devourSkill.MaximumPlayerLift : 0f;
    private float bossDevourGroundClearance => devourSkill != null ? devourSkill.GroundClearance : 0f;
    private float bossDevourMaximumAllowedVerticalDrift => devourSkill != null ? devourSkill.MaximumAllowedVerticalDrift : BossDevourUnexpectedVerticalMovementThreshold;

    private BossSlimeLeapSlamSkill ResolveLeapSlamSkill(bool createIfMissing = false)
    {
        if (leapSlamSkill != null)
        {
            return leapSlamSkill;
        }

        leapSlamSkill = GetComponent<BossSlimeLeapSlamSkill>();
        if (leapSlamSkill == null && createIfMissing)
        {
            leapSlamSkill = gameObject.AddComponent<BossSlimeLeapSlamSkill>();
            leapSlamSkillRuntimeFallbackCreated = true;
        }
        else if (leapSlamSkill != null)
        {
            leapSlamSkillRuntimeFallbackCreated = false;
        }

        return leapSlamSkill;
    }

    private BossSlimeDevourSkill ResolveDevourSkill(bool createIfMissing = false)
    {
        if (devourSkill != null)
        {
            return devourSkill;
        }

        devourSkill = GetComponent<BossSlimeDevourSkill>();
        if (devourSkill == null && createIfMissing)
        {
            devourSkill = gameObject.AddComponent<BossSlimeDevourSkill>();
            devourSkillRuntimeFallbackCreated = true;
        }
        else if (devourSkill != null)
        {
            devourSkillRuntimeFallbackCreated = false;
        }

        return devourSkill;
    }

    public void EnsureBossSkillComponentsForRuntime()
    {
        bool shouldCreateBossSkillComponents =
            attackStyle == MonsterAttackStyle.ElementalBoss ||
            (monsterIdentity != null && monsterIdentity.rank == MonsterRank.Boss);

        if (shouldCreateBossSkillComponents)
        {
            ResolveLeapSlamSkill(createIfMissing: true);
            ResolveDevourSkill(createIfMissing: true);
        }
    }

    private void LogBossLeapSlamConfigTraceOnce(string source)
    {
        BossSlimeLeapSlamSkill skill = ResolveLeapSlamSkill();
        if (skill == null || hasLoggedLeapSlamConfigTrace)
        {
            return;
        }

        Debug.Log(
            skill.BuildConfigTrace(source) +
            " configSource=" + (leapSlamSkillRuntimeFallbackCreated ? "RuntimeFallback" : "PrefabAssigned"),
            this);
        hasLoggedLeapSlamConfigTrace = true;
    }

    private void LogBossDevourConfigTraceOnce(string source)
    {
        BossSlimeDevourSkill skill = ResolveDevourSkill();
        if (skill == null || hasLoggedDevourConfigTrace)
        {
            return;
        }

        Debug.Log(
            skill.BuildConfigTrace(source) +
            " configSource=" + (devourSkillRuntimeFallbackCreated ? "RuntimeFallback" : "PrefabAssigned"),
            this);
        hasLoggedDevourConfigTrace = true;
    }

    private void LogBossDevourStartedTrace(int sequenceId, Transform target)
    {
        BossSlimeDevourSkill skill = ResolveDevourSkill();
        if (skill == null)
        {
            return;
        }

        Debug.Log(
            skill.BuildConfigTrace("DevourStarted") +
            " configSource=" + (devourSkillRuntimeFallbackCreated ? "RuntimeFallback" : "PrefabAssigned") +
            " event=DevourStarted" +
            " actionSequenceId=" + sequenceId +
            " target=" + (target != null ? target.name : "null"),
            this);
    }

    private AudioSource EnsureSkillAudioSource()
    {
        if (skillAudioSource != null)
        {
            return skillAudioSource;
        }

        AudioSource[] sources = GetComponents<AudioSource>();
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource candidate = sources[i];
            if (candidate != null && candidate.spatialBlend > 0.5f)
            {
                skillAudioSource = candidate;
                break;
            }
        }

        if (skillAudioSource == null)
        {
            skillAudioSource = gameObject.AddComponent<AudioSource>();
            skillAudioSource.playOnAwake = false;
            skillAudioSource.loop = false;
            skillAudioSource.spatialBlend = 1f;
            skillAudioSource.dopplerLevel = 0f;
        }

        return skillAudioSource;
    }

    private void PlaySkillAudio(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f)
        {
            return;
        }

        AudioSource source = EnsureSkillAudioSource();
        if (source == null)
        {
            return;
        }

        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void Start()
    {
        MonsterCombatAutoSetup.Configure(gameObject);
        rb = GetComponent<Rigidbody>();
        monsterIdentity = GetComponent<MonsterIdentity>();
        combatStats = GetComponent<CombatStats>();
        slimeAnimation = GetComponent<SlimeAnimationController>();
        EnsureSkillAudioSource();
        ResolveMeleeHitSources();
        initialRotation = transform.rotation;
        ResolvePlayerTarget();
        EnsureBossSkillComponentsForRuntime();
        InitializeBossSkillTimers();
        LogBossLeapSlamConfigTraceOnce("EnemyController.Start");
        LogBossDevourConfigTraceOnce("EnemyController.Start");

        if (slimeAnimation != null)
        {
            slimeAnimation.OnAttackHit += HandleAttackHit;
        }

        ResolveDebuffReceiver();
    }

    private void OnDestroy()
    {
        if (slimeAnimation != null)
        {
            slimeAnimation.OnAttackHit -= HandleAttackHit;
        }

        RestoreAllBossPlayerCollisionIsolations("BossDestroyed");
        ReleaseBossActionLock("Destroyed", activeBossActionSequenceId);
        RestoreBossLeapBodyOverride("OnDestroy");
        RestoreBossBodyAfterSplit();
    }

    private void OnDisable()
    {
        RestoreAllBossPlayerCollisionIsolations("BossDisabled");
        ReleaseBossActionLock("Disabled", activeBossActionSequenceId);
        RestoreBossLeapBodyOverride("OnDisable");
    }

    private void Update()
    {
        ResolvePlayerTarget();
        if (rb == null || playerTarget == null)
        {
            if (debugAttackDiagnostics && playerTarget == null)
            {
                Debug.Log($"[EnemyAttackDiag] name={name} target=null failReason=NoTarget", this);
            }

            LogChaseDiagnostics(
                -1f,
                false,
                false,
                false,
                false,
                false,
                rb == null ? "NoRigidbody" : "NoTarget",
                0f,
                playerTarget != null ? playerTarget.name : "null",
                rb == null ? "NoRigidbody" : "NoTarget");
            return;
        }

        Vector3 toPlayer = playerTarget.position - transform.position;
        toPlayer.y = 0f;
        float centerDistance = Vector3.Distance(playerTarget.position, transform.position);
        float horizontalCenterDistance = new Vector2(toPlayer.x, toPlayer.z).magnitude;
        float verticalDifference = ResolveVerticalCombatDifference(playerTarget, out float verticalCenterDifference);
        float horizontalEdgeDistance = ResolveHorizontalEdgeDistance(playerTarget, out Vector3 enemyClosestPoint, out Vector3 playerClosestPoint);
        float attackDistance = ResolveAttackDistance(horizontalCenterDistance, horizontalEdgeDistance);
        float statsSpeed = combatStats != null ? Mathf.Max(0f, combatStats.speed) : 0f;
        bool grounded = IsGroundedForAttack();
        string attackFailReason = EvaluateAttackFailReason(attackDistance, verticalDifference, grounded);
        bool canAttack = string.IsNullOrEmpty(attackFailReason);
        bool isAttackAnimationActive = attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking);
        bool hasPhysicalContact = horizontalEdgeDistance <= EdgeDistanceEpsilon;
        bool insideStopDistance = horizontalEdgeDistance <= Mathf.Max(0f, stopDistance) + EdgeDistanceEpsilon;
        CombatHealth health = GetComponent<CombatHealth>();
        bool isDead = health != null && health.IsDead;
        bool cooldownReady = Time.time >= nextAttackTime;
        bool canMove = rb != null && !attackInProgress;
        string currentState = playerTarget == null
            ? EnemyAttackRuntimeState.NoTarget.ToString()
            : (attackInProgress
                ? EnemyAttackRuntimeState.AttackInProgress.ToString()
                : (canAttack ? EnemyAttackRuntimeState.AttackReady.ToString() : EnemyAttackRuntimeState.Chase.ToString()));

        if (attackStyle == MonsterAttackStyle.ElementalBoss)
        {
            HandleBossElementalCombat(toPlayer, horizontalCenterDistance, horizontalEdgeDistance, centerDistance, verticalDifference, grounded);
            return;
        }

        LogRuntimeAttackConfigOnce();
        LogAttackDiagnostics(
            statsSpeed,
            centerDistance,
            attackDistance,
            verticalDifference,
            grounded,
            canAttack,
            attackFailReason);
        LogEliteAttackDiag(
            horizontalCenterDistance,
            horizontalEdgeDistance,
            verticalDifference,
            verticalCenterDifference,
            grounded,
            canMove,
            canAttack,
            cooldownReady,
            currentState,
            attackFailReason,
            false,
            attackInProgress,
            false);
        // Enter attack flow as soon as the target is in range so chase does not keep pushing the player.
        if (canAttack)
        {
            LogEliteAttackDiag(
                horizontalCenterDistance,
                horizontalEdgeDistance,
                verticalDifference,
                verticalCenterDifference,
                grounded,
                canMove,
                canAttack,
                cooldownReady,
                currentState,
                attackFailReason,
                true,
                false,
                false);
            LogEnemyMeleeDecision(attackDistance, true, "None", grounded, "Melee", playerTarget != null ? playerTarget.name : "null", Mathf.Max(0f, nextAttackTime - Time.time), isAttackAnimationActive, false, isDead);
            LogChaseDiagnostics(attackDistance, false, true, false, false, false, "Attack", 0f, playerTarget != null ? playerTarget.name : "null", "InAttackRange");
            LogAttackStateChange(
                EnemyAttackRuntimeState.AttackReady,
                centerDistance,
                attackDistance,
                verticalDifference,
                grounded,
                canAttack,
                "InAttackRange",
                enemyClosestPoint,
                playerClosestPoint);
            BeginAttack();
            return;
        }

        // 鏀诲嚮鍔ㄤ綔杩涜涓椂锛屽師鍦板仠浣忓苟淇濇寔褰撳墠鏈濆悜锛岀瓑寰呮敾鍑诲洖璋冪粨绠椼€?
        if (isAttackAnimationActive)
        {
            LogEnemyMeleeDecision(attackDistance, false, "AlreadyAttacking", grounded, "None", playerTarget != null ? playerTarget.name : "null", Mathf.Max(0f, nextAttackTime - Time.time), true, false, isDead);
            rb.linearVelocity = Vector3.zero;
            string chaseReason = string.IsNullOrEmpty(attackFailReason) ? "AttackInProgress" : attackFailReason;
            LogChaseDiagnostics(attackDistance, false, false, false, false, false, "AttackRecovery", 0f, playerTarget != null ? playerTarget.name : "null", chaseReason);
            LogAttackStateChange(
                EnemyAttackRuntimeState.AttackInProgress,
                centerDistance,
                attackDistance,
                verticalDifference,
                grounded,
                false,
                chaseReason,
                enemyClosestPoint,
                playerClosestPoint);
            if (keepFlatRotation)
            {
                transform.rotation = initialRotation;
            }
            return;
        }

        // Hold position inside stop distance to avoid face-hug jitter and constant pushing.
        if (hasPhysicalContact || insideStopDistance || centerDistance < MovementZeroEpsilon)
        {
            LogEnemyMeleeDecision(
                attackDistance,
                false,
                string.IsNullOrEmpty(attackFailReason) ? (hasPhysicalContact ? "PhysicalContact" : "StopDistance") : attackFailReason,
                grounded,
                "Chase",
                playerTarget != null ? playerTarget.name : "null",
                Mathf.Max(0f, nextAttackTime - Time.time),
                false,
                false,
                isDead);
            rb.linearVelocity = Vector3.zero;
            StopMoveAnimation();
            string chaseReason = string.IsNullOrEmpty(attackFailReason)
                ? (hasPhysicalContact ? "PhysicalContact" : "StopDistance")
                : attackFailReason;
            LogChaseDiagnostics(attackDistance, false, false, false, false, false, "HoldPosition", 0f, playerTarget != null ? playerTarget.name : "null", chaseReason);
            LogAttackStateChange(
                EnemyAttackRuntimeState.HoldPosition,
                centerDistance,
                attackDistance,
                verticalDifference,
                grounded,
                false,
                chaseReason,
                enemyClosestPoint,
                playerClosestPoint);
            if (keepFlatRotation)
            {
                transform.rotation = initialRotation;
            }
            return;
        }

        float safeHorizontalCenterDistance = Mathf.Max(horizontalCenterDistance, MovementZeroEpsilon);
        Vector3 direction = toPlayer / safeHorizontalCenterDistance;
        if (enableEnemySoftAvoidance)
        {
            Vector3 separationDirection = ResolveEnemySeparationDirection();
            Vector3 combinedDirection = direction + separationDirection * Mathf.Max(0f, enemySeparationWeight);
            combinedDirection.y = 0f;
            if (combinedDirection.sqrMagnitude > MovementZeroEpsilon * MovementZeroEpsilon)
            {
                direction = combinedDirection.normalized;
            }
        }

        float baseMoveSpeed = moveSpeed;
        float externalMoveMultiplier = ResolveExternalMoveMultiplier();
        float rawMoveSpeed = BattleStatUtility.ResolveMoveSpeed(combatStats, baseMoveSpeed, externalMoveMultiplier);
        float currentMoveSpeed = BattleStatUtility.ClampActualMoveSpeed(rawMoveSpeed, out _);
        if (maxHorizontalMoveSpeed > 0f)
        {
            currentMoveSpeed = Mathf.Min(currentMoveSpeed, maxHorizontalMoveSpeed);
        }
        if (debugLog && Mathf.Abs(externalMoveMultiplier - lastLoggedMoveMultiplier) > 0.001f)
        {
            Debug.Log($"[EnemyController] finalMoveSpeed={currentMoveSpeed:F2} multiplier={externalMoveMultiplier:F2}", this);
            lastLoggedMoveMultiplier = externalMoveMultiplier;
        }

        LogSpeedDiagnostics(statsSpeed, centerDistance, canAttack);
        LogEnemyMeleeDecision(
            attackDistance,
            false,
            string.IsNullOrEmpty(attackFailReason) ? "OutOfRange" : attackFailReason,
            grounded,
            UsesProjectileAttack() ? "Ranged" : "Chase",
            playerTarget != null ? playerTarget.name : "null",
            Mathf.Max(0f, nextAttackTime - Time.time),
            false,
            false,
            isDead);
        LogChaseDiagnostics(
            attackDistance,
            currentMoveSpeed > MovementZeroEpsilon,
            false,
            false,
            false,
            externalMoveMultiplier <= 0f,
            "Chase",
            currentMoveSpeed,
            playerTarget != null ? playerTarget.name : "null",
            string.IsNullOrEmpty(attackFailReason) ? "Chase" : attackFailReason);
        LogAttackStateChange(
            EnemyAttackRuntimeState.Chase,
            centerDistance,
            attackDistance,
            verticalDifference,
            grounded,
            canAttack,
            string.IsNullOrEmpty(attackFailReason) ? "Chase" : attackFailReason,
            enemyClosestPoint,
            playerClosestPoint);

        float verticalVelocity = rb.linearVelocity.y;
        if (maxVerticalVelocity > 0f)
        {
            verticalVelocity = Mathf.Clamp(verticalVelocity, -maxVerticalVelocity, maxVerticalVelocity);
        }

        rb.linearVelocity = new Vector3(direction.x * currentMoveSpeed, verticalVelocity, direction.z * currentMoveSpeed);
        PlayMoveAnimation(direction, currentMoveSpeed);

        if (faceMoveDirection)
        {
            transform.forward = direction;
        }
        else if (keepFlatRotation)
        {
            transform.rotation = initialRotation;
        }
    }

    public void SetTarget(Transform target)
    {
        AssignTarget(target, "Manual");
    }

    public void SetTarget(Transform target, string source)
    {
        AssignTarget(target, source);
    }

    public void SetBossTimedSplitEnabled(bool enabled)
    {
        enableBossTimedSplit = enabled;
    }

    public void AbortCombatForExternalPhase(string reason = "ExternalPhase")
    {
        CancelInvoke(nameof(FinishAttackRecovery));

        if (bossRangedAttackRoutine != null)
        {
            StopCoroutine(bossRangedAttackRoutine);
            bossRangedAttackRoutine = null;
        }

        if (bossSpecialAttackRoutine != null)
        {
            StopCoroutine(bossSpecialAttackRoutine);
            bossSpecialAttackRoutine = null;
        }

        if (bossAttackRecoveryRoutine != null)
        {
            StopCoroutine(bossAttackRecoveryRoutine);
            bossAttackRecoveryRoutine = null;
        }

        if (activeBossAttackRoutine != null)
        {
            StopCoroutine(activeBossAttackRoutine);
            activeBossAttackRoutine = null;
        }

        attackInProgress = false;
        attackHitFrameTriggeredThisAttack = false;
        pendingAttackTarget = null;
        lastMeleeAttackResult = reason;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }

        ReleaseBossActionLock(reason, activeBossActionSequenceId);
    }

    public void ConfigureRuntime(float moveSpeed, float stopDistance, float attackRange, float attackHitRange, float attackCooldown, float attackDamage, MonsterAttackStyle attackStyle, float attackIntervalMultiplier = 1f, float outgoingDamageMultiplier = 1f)
    {
        this.moveSpeed = Mathf.Max(0f, moveSpeed);
        this.stopDistance = Mathf.Max(0f, stopDistance);
        this.attackRange = Mathf.Max(0.1f, attackRange);
        this.attackHitRange = Mathf.Max(0.1f, attackHitRange);
        this.attackCooldown = Mathf.Max(0.1f, attackCooldown);
        this.attackDamage = Mathf.Max(0f, attackDamage);
        this.attackStyle = attackStyle;
        this.attackIntervalMultiplier = Mathf.Max(0.1f, attackIntervalMultiplier);
        this.outgoingDamageMultiplier = Mathf.Max(0.01f, outgoingDamageMultiplier);
        EnsureBossSkillComponentsForRuntime();

        // EnemySpawner rewrites rank combat windows at runtime. Keep the horizontal
        // attack gate aligned with the final runtime hit window so template-based
        // Elite/Boss enemies do not keep using stale prefab thresholds.
        maxHorizontalAttackDistance = Mathf.Max(0.1f, this.attackHitRange);
    }

    private void FixedUpdate()
    {
        if (attackStyle != MonsterAttackStyle.ElementalBoss)
        {
            return;
        }

        UpdateBossAirborneLandingImpact();
    }

    private void InitializeBossSkillTimers()
    {
        if (attackStyle != MonsterAttackStyle.ElementalBoss)
        {
            return;
        }

        float now = Time.time;
        nextBossLeapAttackTime = now + Mathf.Max(0f, bossLeapInitialDelay);
        nextBossSplitAttackTime = now + Mathf.Max(0f, bossSplitInitialDelay);
        nextBossDevourAttackTime = now + Mathf.Max(0f, bossDevourInitialDelay);
    }

    private void HandleBossElementalCombat(Vector3 toPlayer, float horizontalCenterDistance, float horizontalEdgeDistance, float centerDistance, float verticalDifference, bool grounded)
    {
        float decisionDistance = horizontalEdgeDistance < float.MaxValue
            ? Mathf.Max(0f, horizontalEdgeDistance)
            : Mathf.Max(0f, horizontalCenterDistance);
        string targetName = playerTarget != null ? playerTarget.name : "null";
        CombatHealth health = GetComponent<CombatHealth>();
        bool isDead = health != null && health.IsDead;
        bool isStunned = false;
        bool isAttacking = attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking);
        float meleeCooldownRemaining = Mathf.Max(0f, nextAttackTime - Time.time);
        float rangedCooldownRemaining = Mathf.Max(0f, nextBossRangedAttackTime - Time.time);
        string currentAttackState = attackInProgress
            ? EnemyAttackRuntimeState.AttackInProgress.ToString()
            : (activeBossAttackKind == BossAttackKind.Ranged
                ? BossAttackKind.Ranged.ToString()
                : (activeBossAttackKind == BossAttackKind.Melee
                    ? BossAttackKind.Melee.ToString()
                    : EnemyAttackRuntimeState.Chase.ToString()));
        Collider physicalBodyCollider = meleeEnemyCollider != null ? meleeEnemyCollider : GetComponent<Collider>();
        Collider combatSurfaceCollider = ResolvePlayerCollider(playerTarget);

        if (bossActionLocked || attackInProgress)
        {
            ZeroBossHorizontalVelocityPreserveVertical(grounded, currentAttackState, "AttackInProgress");
            StopMoveAnimation();
            FaceTargetHorizontally(playerTarget);
            LogBossMeleeDiag(horizontalCenterDistance, decisionDistance, verticalDifference, grounded, false, false, meleeCooldownRemaining <= 0.001f, rangedCooldownRemaining <= 0.001f, currentAttackState, physicalBodyCollider, combatSurfaceCollider, "AlreadyAttacking");
            LogBossMeleeDecision(decisionDistance, false, "AlreadyAttacking", grounded, "Ranged", targetName, meleeCooldownRemaining, isAttacking, isStunned, isDead);
            LogBossRangedDecision(decisionDistance, "Ranged", "CastInProgress");
            LogBossCombatDecisionTrace(decisionDistance, grounded, "ContinueCurrentAttack", "AlreadyAttacking");
            return;
        }

        if (bossBodyHiddenForSplit)
        {
            ZeroBossHorizontalVelocityPreserveVertical(grounded, currentAttackState, "BodyHiddenForSplit");
            StopMoveAnimation();
            LogBossCombatDecisionTrace(decisionDistance, grounded, "InvalidState", "BodyHiddenForSplit");
            return;
        }

        if (IsForcedAirborneImpactPending())
        {
            ZeroBossHorizontalVelocityPreserveVertical(grounded, currentAttackState, "ForcedAirborneImpactPending");
            StopMoveAnimation();
            FaceTargetHorizontally(playerTarget);
            Debug.Log(
                "[BossLeapSlamTrace] event=ForcedAirborneAttackBlocked " +
                "activeKind=" + activeBossAttackKind +
                " forcedSequenceId=" + forcedAirborneSequenceId +
                " forcedArmed=" + forcedAirborneImpactArmed +
                " forcedConsumed=" + forcedAirborneImpactConsumed,
                this);
            LogBossCombatDecisionTrace(decisionDistance, grounded, "AirborneHold", "ForcedAirborneImpactPending");
            return;
        }

        if (TryBeginBossSpecialSkill(decisionDistance, verticalDifference, grounded, out string specialSkillName, out string specialFailReason))
        {
            LogBossCombatDecisionTrace(decisionDistance, grounded, "Start" + specialSkillName, "Started");
            return;
        }

        bool inMeleeRange = decisionDistance <= Mathf.Max(0.1f, bossMeleeAttackRange);
        bool inRangedRange = decisionDistance >= Mathf.Max(0.1f, bossRangedMinRange) && decisionDistance <= Mathf.Max(bossRangedMinRange, bossRangedMaxRange);

        if (inMeleeRange)
        {
            string meleeFailReason = EvaluateBossMeleeFailReason(decisionDistance, verticalDifference, grounded);
            LogBossMeleeDiag(horizontalCenterDistance, decisionDistance, verticalDifference, grounded, true, string.IsNullOrEmpty(meleeFailReason), meleeCooldownRemaining <= 0.001f, rangedCooldownRemaining <= 0.001f, currentAttackState, physicalBodyCollider, combatSurfaceCollider, string.IsNullOrEmpty(meleeFailReason) ? "None" : meleeFailReason);
            LogBossMeleeDecision(decisionDistance, string.IsNullOrEmpty(meleeFailReason), string.IsNullOrEmpty(meleeFailReason) ? "None" : meleeFailReason, grounded, "Melee", targetName, meleeCooldownRemaining, isAttacking, isStunned, isDead);
            LogBossRangedDecision(decisionDistance, "Melee", string.IsNullOrEmpty(meleeFailReason) ? "WithinMeleeRange" : meleeFailReason);
            if (string.IsNullOrEmpty(meleeFailReason))
            {
                if (TryStartBossBasicAttack(playerTarget))
                {
                    LogBossCombatDecisionTrace(decisionDistance, grounded, "StartBasicAttack", "MeleeStarted");
                    return;
                }

                LogBossCombatDecisionTrace(decisionDistance, grounded, "Recovery", "BasicAttackLockFailed");
                return;
            }

            ZeroBossHorizontalVelocityPreserveVertical(grounded, currentAttackState, "MeleeBlocked:" + meleeFailReason);
            StopMoveAnimation();
            LogBossCombatDecisionTrace(decisionDistance, grounded, "Recovery", string.IsNullOrEmpty(specialFailReason) ? meleeFailReason : (specialSkillName + ":" + specialFailReason + "|Melee:" + meleeFailReason));
            return;
        }

        if (inRangedRange)
        {
            string rangedFailReason = EvaluateBossRangedFailReason(decisionDistance, verticalDifference, grounded);
            LogBossMeleeDiag(horizontalCenterDistance, decisionDistance, verticalDifference, grounded, false, false, meleeCooldownRemaining <= 0.001f, rangedCooldownRemaining <= 0.001f, currentAttackState, physicalBodyCollider, combatSurfaceCollider, string.IsNullOrEmpty(rangedFailReason) ? "RangedSelected" : rangedFailReason);
            LogBossMeleeDecision(decisionDistance, false, string.IsNullOrEmpty(rangedFailReason) ? "RangedSelected" : rangedFailReason, grounded, "Ranged", targetName, meleeCooldownRemaining, isAttacking, isStunned, isDead);
            LogBossRangedDecision(decisionDistance, "Ranged", string.IsNullOrEmpty(rangedFailReason) ? "WithinRangedWindow" : rangedFailReason);
            if (string.IsNullOrEmpty(rangedFailReason))
            {
                if (BeginBossRangedAttack(playerTarget))
                {
                    LogBossCombatDecisionTrace(decisionDistance, grounded, "StartRanged", "RangedStarted");
                    return;
                }

                LogBossCombatDecisionTrace(decisionDistance, grounded, "Recovery", "RangedLockFailed");
                return;
            }

            if (rangedFailReason == "Cooldown")
            {
                LogBossMeleeDiag(horizontalCenterDistance, decisionDistance, verticalDifference, grounded, false, false, meleeCooldownRemaining <= 0.001f, false, currentAttackState, physicalBodyCollider, combatSurfaceCollider, "RangedCooldownChaseToMelee");
                LogBossMeleeDecision(decisionDistance, false, "RangedCooldownChaseToMelee", grounded, "Chase", targetName, meleeCooldownRemaining, isAttacking, isStunned, isDead);
                ChaseTarget(toPlayer, currentState: "BossChase", targetName: targetName, reason: "RangedCooldownChaseToMelee");
                LogBossCombatDecisionTrace(decisionDistance, grounded, "ChaseTarget", "RangedCooldown");
                return;
            }

            ZeroBossHorizontalVelocityPreserveVertical(grounded, currentAttackState, "RangedBlocked:" + rangedFailReason);
            StopMoveAnimation();
            FaceTargetHorizontally(playerTarget);
            LogBossCombatDecisionTrace(decisionDistance, grounded, "Recovery", string.IsNullOrEmpty(specialFailReason) ? rangedFailReason : (specialSkillName + ":" + specialFailReason + "|Ranged:" + rangedFailReason));
            return;
        }

        if (decisionDistance > Mathf.Max(bossRangedMinRange, bossRangedMaxRange))
        {
            LogBossMeleeDiag(horizontalCenterDistance, decisionDistance, verticalDifference, grounded, false, false, meleeCooldownRemaining <= 0.001f, rangedCooldownRemaining <= 0.001f, currentAttackState, physicalBodyCollider, combatSurfaceCollider, "TargetOutsideRangedMax");
            LogBossMeleeDecision(decisionDistance, false, "TargetOutsideRangedMax", grounded, "Chase", targetName, meleeCooldownRemaining, isAttacking, isStunned, isDead);
            LogBossRangedDecision(decisionDistance, "Chase", "TargetOutsideRangedMax");
            ChaseTarget(toPlayer, currentState: "BossChase", targetName: targetName, reason: "TargetOutsideRangedMax");
            LogBossCombatDecisionTrace(decisionDistance, grounded, "ChaseTarget", string.IsNullOrEmpty(specialFailReason) ? "TargetOutsideRangedMax" : (specialSkillName + ":" + specialFailReason));
            return;
        }

        LogBossMeleeDiag(horizontalCenterDistance, decisionDistance, verticalDifference, grounded, false, false, meleeCooldownRemaining <= 0.001f, rangedCooldownRemaining <= 0.001f, currentAttackState, physicalBodyCollider, combatSurfaceCollider, "BetweenMeleeAndRangedWindow");
        LogBossMeleeDecision(decisionDistance, false, "BetweenMeleeAndRangedWindow", grounded, "Chase", targetName, meleeCooldownRemaining, isAttacking, isStunned, isDead);
        LogBossRangedDecision(decisionDistance, "Chase", "BetweenMeleeAndRangedWindow");
        ChaseTarget(toPlayer, currentState: "BossChase", targetName: targetName, reason: "BetweenMeleeAndRangedWindow");
        LogBossCombatDecisionTrace(decisionDistance, grounded, "ChaseTarget", string.IsNullOrEmpty(specialFailReason) ? "BetweenMeleeAndRangedWindow" : (specialSkillName + ":" + specialFailReason));
    }

    private bool TryBeginBossSpecialSkill(float distance, float verticalDifference, bool grounded, out string consideredSkill, out string failReason)
    {
        consideredSkill = string.Empty;
        failReason = string.Empty;

        if (playerTarget == null || bossActionLocked || attackInProgress || bossSpecialAttackRoutine != null || bossRangedAttackRoutine != null || activeBossAttackRoutine != null)
        {
            failReason = playerTarget == null
                ? "NoTarget"
                : bossActionLocked
                    ? "AnotherActionActive"
                    : attackInProgress
                        ? "AttackInProgress"
                        : bossSpecialAttackRoutine != null || activeBossAttackRoutine != null
                            ? "SpecialRoutineBusy"
                            : "RangedRoutineBusy";
            return false;
        }

        if (enableBossTimedSplit && Time.time >= nextBossSplitAttackTime)
        {
            consideredSkill = "Split";
            if (CanUseBossSplitSkill() && RollBossSkillChance(bossSplitTriggerChance))
            {
                if (BeginBossSplitSkill())
                {
                    return true;
                }

                failReason = "LockFailed";
            }
            else
            {
                failReason = CanUseBossSplitSkill() ? "ChanceMissed" : "ConditionsFailed";
            }
        }

        bool inBossMeleeRange = distance <= Mathf.Max(0.1f, bossMeleeAttackRange);
        bool leapPreferredInMelee = allowLeapSlamInMeleeRange && inBossMeleeRange;
        bool devourAvailable = enableBossDevour &&
                               Time.time >= nextBossDevourAttackTime &&
                               CanUseBossDevourSkill(distance, verticalDifference, grounded);
        bool leapAvailable = enableBossLeapSlam &&
                             Time.time >= nextBossLeapAttackTime &&
                             CanUseBossLeapSkill(distance, verticalDifference, grounded);

        if (enableBossDevour && Time.time >= nextBossDevourAttackTime)
        {
            consideredSkill = "Devour";
            LogBossDevourConfigTraceOnce("TryBeginBossSpecialSkill");
            float leapChance = distance <= Mathf.Max(0.1f, bossLeapSlamVeryCloseDistance)
                ? bossLeapSlamVeryCloseChance
                : bossLeapSlamMeleeChance;

            if ((debugAttackDiagnostics || debugLog) && inBossMeleeRange)
            {
                Debug.Log(
                    "[BossCombatDecisionTrace] " +
                    "distance=" + distance.ToString("F2") +
                    " veryClose=" + (distance <= Mathf.Max(0.1f, bossLeapSlamVeryCloseDistance)) +
                    " devourAvailable=" + devourAvailable +
                    " leapAvailable=" + (leapPreferredInMelee && leapAvailable) +
                    " basicAttackAvailable=true" +
                    " devourCooldownRemaining=" + Mathf.Max(0f, nextBossDevourAttackTime - Time.time).ToString("F2") +
                    " leapCooldownRemaining=" + Mathf.Max(0f, nextBossLeapAttackTime - Time.time).ToString("F2") +
                    " devourConfiguredChanceOrWeight=" + bossDevourTriggerChance.ToString("F2") +
                    " leapConfiguredChanceOrWeight=" + leapChance.ToString("F2") +
                    " candidateSkills=Devour,LeapSlam,BasicAttack" +
                    " selectionOrder=DevourThenLeapThenBasic" +
                    " effectiveDevourProbability=" + bossDevourTriggerChance.ToString("F2") +
                    " effectiveLeapProbability=" + ((1f - bossDevourTriggerChance) * leapChance).ToString("F2") +
                    " effectiveBasicProbability=" + ((1f - bossDevourTriggerChance) * (1f - leapChance)).ToString("F2"),
                    this);
            }

            float devourRoll = Random.value;
            bool devourPassed = devourAvailable && devourRoll <= Mathf.Clamp01(bossDevourTriggerChance);
            if (debugAttackDiagnostics || debugLog)
            {
                Debug.Log(
                    "[BossCombatDecisionTrace] " +
                    "distance=" + distance.ToString("F2") +
                    " skill=Devour" +
                    " available=" + devourAvailable +
                    " cooldownRemaining=" + Mathf.Max(0f, nextBossDevourAttackTime - Time.time).ToString("F2") +
                    " configuredChance=" + bossDevourTriggerChance.ToString("F2") +
                    " randomRoll=" + devourRoll.ToString("F4") +
                    " passed=" + devourPassed,
                    this);
            }

            if (devourPassed)
            {
                if (BeginBossDevourSkill(playerTarget))
                {
                    if (debugAttackDiagnostics || debugLog)
                    {
                        Debug.Log(
                            "[BossCombatDecisionTrace] selectedAction=Devour selectionReason=PassedRollAndConditions distance=" + distance.ToString("F2"),
                            this);
                    }
                    return true;
                }

                failReason = "LockFailed";
            }
            else
            {
                failReason = devourAvailable ? "ChanceMissed" : "ConditionsFailed";
            }
        }

        if (enableBossLeapSlam && Time.time >= nextBossLeapAttackTime)
        {
            consideredSkill = "LeapSlam";
            float leapRoll = Random.value;
            bool leapPassed = leapAvailable && leapRoll <= Mathf.Clamp01(bossLeapTriggerChance);
            if (debugAttackDiagnostics || debugLog)
            {
                Debug.Log(
                    "[BossCombatDecisionTrace] " +
                    "distance=" + distance.ToString("F2") +
                    " skill=LeapSlam" +
                    " available=" + leapAvailable +
                    " cooldownRemaining=" + Mathf.Max(0f, nextBossLeapAttackTime - Time.time).ToString("F2") +
                    " configuredChance=" + bossLeapTriggerChance.ToString("F2") +
                    " randomRoll=" + leapRoll.ToString("F4") +
                    " passed=" + leapPassed,
                    this);
            }

            if (leapPassed)
            {
                if (BeginBossLeapSlam(playerTarget))
                {
                    if (debugAttackDiagnostics || debugLog)
                    {
                        Debug.Log(
                            "[BossCombatDecisionTrace] selectedAction=LeapSlam selectionReason=PassedRollAndConditions distance=" + distance.ToString("F2"),
                            this);
                    }
                    return true;
                }

                failReason = "LockFailed";
            }
            else
            {
                failReason = leapAvailable ? "ChanceMissed" : "ConditionsFailed";
            }
        }

        return false;
    }

    private bool CanUseBossLeapSkill(float distance, float verticalDifference, bool grounded)
    {
        if (playerTarget == null)
        {
            return false;
        }

        float minimumRange = allowLeapSlamInMeleeRange
            ? Mathf.Max(0f, bossLeapSlamMinimumHorizontalTravel)
            : Mathf.Max(0.1f, bossLeapMinRange);

        float maximumRange = Mathf.Max(
            Mathf.Max(bossLeapMinRange, bossLeapMaxRange),
            Mathf.Max(0f, bossLeapSlamMaximumHorizontalDistance));

        if (distance < minimumRange || distance > maximumRange)
        {
            return false;
        }

        if (verticalDifference > Mathf.Max(0f, maxVerticalTargetDifference))
        {
            return false;
        }

        return !requireGroundedToAttack || grounded;
    }

    private bool RollBossLeapMeleeSelectionChance(float distance)
    {
        float chance = distance <= Mathf.Max(0.1f, bossLeapSlamVeryCloseDistance)
            ? bossLeapSlamVeryCloseChance
            : bossLeapSlamMeleeChance;

        return Random.value <= Mathf.Clamp01(chance);
    }

    private bool CanUseBossSplitSkill()
    {
        MonsterIdentity identity = monsterIdentity != null ? monsterIdentity : GetComponent<MonsterIdentity>();
        if (identity == null || identity.rank != MonsterRank.Boss || !IsSlimeIdentity())
        {
            return false;
        }

        EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
        return spawner != null && Mathf.Max(0, bossSplitChildCount) > 0;
    }

    private bool CanUseBossDevourSkill(float distance, float verticalDifference, bool grounded)
    {
        if (playerTarget == null)
        {
            return false;
        }

        float minimumRange = Mathf.Max(0f, bossDevourMinimumRange);
        float maximumRange = Mathf.Max(minimumRange, bossDevourRange);
        if (distance < minimumRange || distance > Mathf.Max(0.1f, maximumRange))
        {
            return false;
        }

        if (verticalDifference > Mathf.Max(0f, maxVerticalAttackDifference))
        {
            return false;
        }

        return !requireGroundedToAttack || grounded;
    }

    private static bool RollBossSkillChance(float chance)
    {
        return Random.value <= Mathf.Clamp01(chance);
    }

    private bool BeginBossLeapSlam(Transform target)
    {
        if (target == null || bossSpecialAttackRoutine != null)
        {
            return false;
        }

        if (!TryLockBossAction(BossAttackKind.LeapSlam, out int sequenceId))
        {
            return false;
        }

        ResetBossAirborneLandingState(transform.position.y, true);
        pendingAttackTarget = target;
        lastAttackTime = Time.time;
        nextBossLeapAttackTime = Time.time + Mathf.Max(0.1f, bossLeapCooldown);
        rb.linearVelocity = Vector3.zero;
        LogBossLeapSlamConfigTraceOnce("BeginBossLeapSlam");
        BeginBossLeapBodyOverride();
        PlaySkillAudio(leapSlamActivationAudioClip, leapSlamActivationAudioVolume);
        Debug.Log(
            "[BossLeapSlamTrace] event=LeapStarted " +
            "actionSequenceId=" + sequenceId +
            " airborneSequenceId=" + bossCurrentAirborneSequenceId +
            " activeKind=" + activeBossAttackKind +
            " locked=" + bossActionLocked +
            " rbIsKinematic=" + (rb != null && rb.isKinematic) +
            " rbUseGravity=" + (rb != null && rb.useGravity),
            this);
        StopMoveAnimation();
        CancelInvoke(nameof(FinishAttackRecovery));
        bossSpecialAttackRoutine = StartCoroutine(BossLeapSlamRoutine(target, sequenceId));
        activeBossAttackRoutine = bossSpecialAttackRoutine;
        return true;
    }

    private System.Collections.IEnumerator BossLeapSlamRoutine(Transform target, int sequenceId)
    {
        Vector3 startPosition = transform.position;
        bool closeSlam = target != null &&
                         Vector3.ProjectOnPlane(target.position - startPosition, Vector3.up).magnitude <= Mathf.Max(0.1f, bossMeleeAttackRange);
        Vector3 landingPosition = ResolveBossLeapLandingPosition(target, startPosition, closeSlam, sequenceId);
        Vector3 baseScale = slimeAnimation != null ? slimeAnimation.BaseVisualLocalScale : Vector3.one;
        Vector3 basePosition = slimeAnimation != null ? slimeAnimation.BaseVisualLocalPosition : Vector3.zero;
        Transform visual = slimeAnimation != null ? slimeAnimation.VisualRoot : null;

        for (float elapsed = 0f; elapsed < Mathf.Max(0.01f, bossLeapWindupTime); elapsed += Time.deltaTime)
        {
            if (!IsBossActionActive(BossAttackKind.LeapSlam, sequenceId))
            {
                yield break;
            }

            rb.linearVelocity = Vector3.zero;
            FaceTargetHorizontally(target);
            if (visual != null)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, bossLeapWindupTime));
                visual.localScale = Vector3.Lerp(baseScale, new Vector3(baseScale.x * 1.18f, baseScale.y * 0.82f, baseScale.z), t);
                visual.localPosition = basePosition;
            }

            yield return null;
        }

        float travelTime = Mathf.Max(0.01f, bossLeapTravelTime);
        for (float elapsed = 0f; elapsed < travelTime; elapsed += Time.deltaTime)
        {
            if (!IsBossActionActive(BossAttackKind.LeapSlam, sequenceId))
            {
                yield break;
            }

            if (elapsed <= Time.deltaTime)
            {
                landingPosition = ResolveBossLeapLandingPosition(target, startPosition, closeSlam, sequenceId);
                if (debugAttackDiagnostics || debugLog)
                {
                    Debug.Log(
                        "[BossLeapSlamTrace] event=LeapLandingTargetResolved " +
                        "actionSequenceId=" + sequenceId +
                        " closeSlam=" + closeSlam +
                        " landingPosition=" + landingPosition +
                        " startPosition=" + startPosition,
                        this);
                }
            }

            float t = Mathf.Clamp01(elapsed / travelTime);
            float arcY = Mathf.Sin(t * Mathf.PI) * Mathf.Max(0f, bossLeapHeight);
            Vector3 nextPosition = Vector3.Lerp(startPosition, landingPosition, t) + Vector3.up * arcY;
            MoveBossBody(nextPosition);
            if (t >= 0.5f && (debugAttackDiagnostics || debugLog))
            {
                Debug.Log(
                    "[BossLeapSlamTrace] event=LeapDescending " +
                    "actionSequenceId=" + sequenceId +
                    " airborneSequenceId=" + bossCurrentAirborneSequenceId +
                    " positionY=" + transform.position.y.ToString("F3") +
                    " velocityY=" + (rb != null ? rb.linearVelocity.y.ToString("F3") : "0.000") +
                    " grounded=" + IsGroundedForAttack() +
                    " rbIsKinematic=" + (rb != null && rb.isKinematic) +
                    " rbUseGravity=" + (rb != null && rb.useGravity),
                    this);
            }
            FaceTargetHorizontally(target);
            if (visual != null)
            {
                visual.localScale = Vector3.Lerp(new Vector3(baseScale.x * 1.18f, baseScale.y * 0.82f, baseScale.z), baseScale, t);
                visual.localPosition = basePosition;
            }

            yield return null;
        }

        MoveBossBody(landingPosition);
        ApplyBossLeapLandingDamage(target, landingPosition, sequenceId);

        for (float elapsed = 0f; elapsed < Mathf.Max(0.01f, bossLeapRecoverTime); elapsed += Time.deltaTime)
        {
            if (!IsBossActionActive(BossAttackKind.LeapSlam, sequenceId))
            {
                yield break;
            }

            rb.linearVelocity = Vector3.zero;
            if (visual != null)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, bossLeapRecoverTime));
                visual.localScale = Vector3.Lerp(new Vector3(baseScale.x * 1.25f, baseScale.y * 0.75f, baseScale.z), baseScale, t);
                visual.localPosition = basePosition;
            }

            yield return null;
        }

        if (visual != null)
        {
            visual.localScale = baseScale;
            visual.localPosition = basePosition;
        }

        CompleteBossSpecialAttack(sequenceId);
        yield break;
    }

    private void ApplyBossLeapLandingDamage(Transform target, Vector3 landingPosition, int actionSequenceId)
    {
        Debug.Log(
            "[BossLeapSlamTrace] event=LeapLandingDetected " +
            "actionSequenceId=" + actionSequenceId +
            " airborneSequenceId=" + bossCurrentAirborneSequenceId +
            " activeActionSequenceId=" + activeBossActionSequenceId +
            " activeKind=" + activeBossAttackKind +
            " landingPosition=" + landingPosition +
            " grounded=" + IsGroundedForAttack(),
            this);
        TryTriggerBossLandingImpact(landingPosition, BossLandingImpactSource.LeapSlam, actionSequenceId, bossCurrentAirborneSequenceId);
    }

    private Vector3 ResolveBossLeapLandingPosition(Transform target, Vector3 startPosition, bool closeSlam, int sequenceId)
    {
        Vector3 predictedTargetPosition = ResolveBossLeapPredictedTargetPosition(target);
        Vector3 horizontalToTarget = Vector3.ProjectOnPlane(predictedTargetPosition - startPosition, Vector3.up);
        Vector3 horizontalDirection = horizontalToTarget.sqrMagnitude > 0.0001f
            ? horizontalToTarget.normalized
            : ResolveBossLeapLandingFallbackDirection(target, sequenceId, out _, out _);

        float horizontalDistance = horizontalToTarget.magnitude;
        float minimumTravel = closeSlam
            ? Mathf.Max(0f, bossLeapSlamMinimumHorizontalTravel)
            : Mathf.Max(0.1f, bossLeapMinRange);
        float maximumTravel = closeSlam
            ? Mathf.Max(minimumTravel, bossLeapSlamCloseTravelDistance)
            : Mathf.Max(minimumTravel, bossLeapSlamMaximumHorizontalDistance);

        float travelDistance = closeSlam
            ? Mathf.Clamp(horizontalDistance, minimumTravel, maximumTravel)
            : Mathf.Min(horizontalDistance, maximumTravel);

        Vector3 landingPosition = startPosition + horizontalDirection * travelDistance;
        landingPosition.y = startPosition.y;
        return landingPosition;
    }

    private Vector3 ResolveBossLeapPredictedTargetPosition(Transform target)
    {
        if (target == null)
        {
            return transform.position;
        }

        Vector3 predictedPosition = target.position;
        if (!enableLeapSlamTargetPrediction)
        {
            return predictedPosition;
        }

        float predictionTime = Mathf.Clamp(
            Mathf.Max(0f, bossLeapTravelTime + bossLeapWindupTime),
            0.2f,
            Mathf.Max(0.2f, bossLeapSlamPredictionTime));

        Vector3 horizontalVelocity = Vector3.zero;
        Rigidbody targetBody = target.GetComponentInParent<Rigidbody>();
        if (targetBody != null)
        {
            horizontalVelocity = Vector3.ProjectOnPlane(targetBody.linearVelocity, Vector3.up);
        }
        else
        {
            CharacterController controller = target.GetComponentInParent<CharacterController>();
            if (controller != null)
            {
                horizontalVelocity = Vector3.ProjectOnPlane(controller.velocity, Vector3.up);
            }
        }

        Vector3 predictionOffset = Vector3.ClampMagnitude(
            horizontalVelocity * predictionTime,
            Mathf.Max(0f, bossLeapSlamMaximumPredictionDistance));

        predictedPosition += predictionOffset;
        return predictedPosition;
    }

    private bool BeginBossSplitSkill()
    {
        if (bossSpecialAttackRoutine != null)
        {
            return false;
        }

        if (!TryLockBossAction(BossAttackKind.TimedSplit, out int sequenceId))
        {
            return false;
        }

        pendingAttackTarget = playerTarget;
        lastAttackTime = Time.time;
        nextBossSplitAttackTime = Time.time + Mathf.Max(0.1f, bossSplitCooldown);
        rb.linearVelocity = Vector3.zero;
        StopMoveAnimation();
        CancelInvoke(nameof(FinishAttackRecovery));
        bossSpecialAttackRoutine = StartCoroutine(BossTimedSplitRoutine(sequenceId));
        activeBossAttackRoutine = bossSpecialAttackRoutine;
        return true;
    }

    private System.Collections.IEnumerator BossTimedSplitRoutine(int sequenceId)
    {
        Vector3 baseScale = slimeAnimation != null ? slimeAnimation.BaseVisualLocalScale : Vector3.one;
        Vector3 basePosition = slimeAnimation != null ? slimeAnimation.BaseVisualLocalPosition : Vector3.zero;
        Transform visual = slimeAnimation != null ? slimeAnimation.VisualRoot : null;

        for (float elapsed = 0f; elapsed < Mathf.Max(0.01f, bossSplitWindupTime); elapsed += Time.deltaTime)
        {
            if (!IsBossActionActive(BossAttackKind.TimedSplit, sequenceId))
            {
                yield break;
            }

            rb.linearVelocity = Vector3.zero;
            FaceTargetHorizontally(playerTarget);
            if (visual != null)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, bossSplitWindupTime));
                visual.localScale = Vector3.Lerp(baseScale, baseScale * Mathf.Lerp(1f, 0.72f, t), t);
                visual.localPosition = basePosition;
            }

            yield return null;
        }

        HideBossBodyForSplit();
        SpawnBossSplitChildren();

        float elapsedSplit = 0f;
        float splitDuration = Mathf.Max(0.1f, bossSplitDuration);
        while (elapsedSplit < splitDuration)
        {
            if (!IsBossActionActive(BossAttackKind.TimedSplit, sequenceId))
            {
                yield break;
            }

            rb.linearVelocity = Vector3.zero;
            if (CountAliveBossSplitChildren() < Mathf.Max(1, bossSplitChildCount))
            {
                break;
            }

            elapsedSplit += Time.deltaTime;
            yield return null;
        }

        bool fullMerge = CountAliveBossSplitChildren() >= Mathf.Max(1, bossSplitChildCount);
        Vector3 mergePosition = ResolveBossSplitMergePosition();
        DestroyActiveBossSplitChildren();
        MoveBossBody(mergePosition);
        RestoreBossBodyAfterSplit();

        if (fullMerge && visual != null)
        {
            visual.localScale = baseScale * 1.2f;
            for (float elapsed = 0f; elapsed < Mathf.Max(0.01f, bossSplitMergeRecoverTime); elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, bossSplitMergeRecoverTime));
                visual.localScale = Vector3.Lerp(baseScale * 1.2f, baseScale, t);
                visual.localPosition = basePosition;
                yield return null;
            }
        }

        if (visual != null)
        {
            visual.localScale = baseScale;
            visual.localPosition = basePosition;
        }

        CompleteBossSpecialAttack(sequenceId);
        yield break;
    }

    private bool BeginBossDevourSkill(Transform target)
    {
        if (target == null || bossSpecialAttackRoutine != null)
        {
            return false;
        }

        if (!TryLockBossAction(BossAttackKind.Devour, out int sequenceId))
        {
            return false;
        }

        pendingAttackTarget = target;
        lastAttackTime = Time.time;
        nextBossDevourAttackTime = Time.time + Mathf.Max(0.1f, bossDevourCooldown);
        activeBossDevourRuntimeConfig = devourSkill != null ? devourSkill.BuildRuntimeConfig() : default;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }
        LogBossDevourStartedTrace(sequenceId, target);
        BeginBossDevourBodyOverride(sequenceId);
        StopMoveAnimation();
        CancelInvoke(nameof(FinishAttackRecovery));
        bossSpecialAttackRoutine = StartCoroutine(BossDevourRoutine(target, sequenceId));
        activeBossAttackRoutine = bossSpecialAttackRoutine;
        return true;
    }

    private System.Collections.IEnumerator BossDevourRoutine(Transform target, int sequenceId)
    {
        BossSlimeDevourSkill.RuntimeConfig runtimeConfig = activeBossDevourRuntimeConfig;
        Vector3 baseScale = slimeAnimation != null ? slimeAnimation.BaseVisualLocalScale : Vector3.one;
        Vector3 basePosition = slimeAnimation != null ? slimeAnimation.BaseVisualLocalPosition : Vector3.zero;
        Transform visual = slimeAnimation != null ? slimeAnimation.VisualRoot : null;

        for (float elapsed = 0f; elapsed < Mathf.Max(0.01f, bossDevourWindupTime); elapsed += Time.deltaTime)
        {
            if (!IsBossActionActive(BossAttackKind.Devour, sequenceId))
            {
                yield break;
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
            MaintainBossDevourGroundAnchor(sequenceId, "Windup");
            FaceTargetHorizontally(target);
            if (visual != null)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, bossDevourWindupTime));
                visual.localScale = Vector3.Lerp(baseScale, new Vector3(baseScale.x * 1.25f, baseScale.y * 1.12f, baseScale.z), t);
                visual.localPosition = basePosition;
            }

            yield return null;
        }

        CombatHealth targetHealth = target != null ? target.GetComponentInParent<CombatHealth>() : null;
        if (targetHealth != null && !targetHealth.IsDead)
        {
            BossSlimeDevourStatus status = BossSlimeDevourStatus.ResolveOrAdd(targetHealth.gameObject);
            status.Apply(gameObject, transform, this, sequenceId, runtimeConfig);
        }

        float duration = Mathf.Max(0.1f, runtimeConfig.TotalDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (!IsBossActionActive(BossAttackKind.Devour, sequenceId))
            {
                yield break;
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }
            MaintainBossDevourGroundAnchor(sequenceId, "Hold");
            FaceTargetHorizontally(target);
            if (visual != null)
            {
                float pulse = 1f + Mathf.Sin(elapsed * 10f) * 0.04f;
                visual.localScale = new Vector3(baseScale.x * 1.12f * pulse, baseScale.y * 1.08f / pulse, baseScale.z);
                visual.localPosition = basePosition;
            }

            yield return null;
        }

        if (visual != null)
        {
            visual.localScale = baseScale;
            visual.localPosition = basePosition;
        }

        CompleteBossSpecialAttack(sequenceId);
        yield break;
    }

    private void CompleteBossSpecialAttack(int sequenceId)
    {
        RestoreBossLeapBodyOverride("CompleteBossSpecialAttack");
        Coroutine completedRoutine = bossSpecialAttackRoutine;
        bossSpecialAttackRoutine = null;
        activeBossDevourRuntimeConfig = default;
        if (activeBossAttackRoutine == completedRoutine)
        {
            activeBossAttackRoutine = null;
        }
        ReleaseBossActionLock("Completed", sequenceId);
    }

    private void BeginBossDevourBodyOverride(int sequenceId)
    {
        bossDevourGroundAnchorPosition = rb != null ? rb.position : transform.position;
        bossDevourGroundedRootY = transform.position.y;

        if (rb == null || bossDevourBodyOverrideActive || !bossDevourLockBossToGround)
        {
            return;
        }

        bossDevourBodyUseGravityBefore = rb.useGravity;
        bossDevourBodyIsKinematicBefore = rb.isKinematic;
        bossDevourBodyConstraintsBefore = rb.constraints;
        bossDevourBodyOverrideActive = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        if (bossDevourDisableBossGravityDuringDevour)
        {
            rb.useGravity = false;
        }
        if (bossDevourMakeBossKinematicDuringDevour)
        {
            rb.isKinematic = true;
        }
        rb.position = bossDevourGroundAnchorPosition;
        transform.position = bossDevourGroundAnchorPosition;
        Physics.SyncTransforms();

        Debug.Log(
            "[BossDevourFlightTrace] event=DevourOverrideBegin " +
            "boss=" + name +
            " sequenceId=" + sequenceId +
            " bossPositionYBefore=" + bossDevourGroundAnchorPosition.y.ToString("F3") +
            " bossPositionYAfter=" + transform.position.y.ToString("F3") +
            " bossVelocityYBefore=0.000" +
            " bossVelocityYAfter=" + (rb != null ? rb.linearVelocity.y.ToString("F3") : "0.000") +
            " bossGrounded=" + IsGroundedForAttack() +
            " bossIsKinematic=" + (rb != null && rb.isKinematic) +
            " bossUseGravity=" + (rb != null && rb.useGravity) +
            " playerPositionY=" + (playerTarget != null ? playerTarget.position.y.ToString("F3") : "None") +
            " devourAnchorPosition=" + bossDevourGroundAnchorPosition +
            " activeBossAttackKind=" + activeBossAttackKind +
            " bossPositionWrittenThisFrame=true" +
            " writeSource=BeginBossDevourBodyOverride",
            this);
    }

    private void MaintainBossDevourGroundAnchor(int sequenceId, string phase)
    {
        if (!bossDevourBodyOverrideActive || rb == null)
        {
            return;
        }

        float beforeY = transform.position.y;
        float beforeVelocityY = rb.linearVelocity.y;
        Vector3 beforePosition = transform.position;
        bool wrotePosition = false;

        if ((transform.position - bossDevourGroundAnchorPosition).sqrMagnitude > 0.000001f ||
            (rb.position - bossDevourGroundAnchorPosition).sqrMagnitude > 0.000001f)
        {
            rb.position = bossDevourGroundAnchorPosition;
            transform.position = bossDevourGroundAnchorPosition;
            Physics.SyncTransforms();
            wrotePosition = true;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        float afterY = transform.position.y;
        float afterVelocityY = rb.linearVelocity.y;
        bool unexpectedVerticalMovement =
            Mathf.Abs(beforeY - bossDevourGroundAnchorPosition.y) > Mathf.Max(0f, bossDevourMaximumAllowedVerticalDrift);

        if (Time.time >= nextBossDevourFlightTraceTime || wrotePosition || unexpectedVerticalMovement)
        {
            nextBossDevourFlightTraceTime = Time.time + BossDevourFlightTraceInterval;
            Debug.Log(
                "[BossDevourFlightTrace] event=DevourFrame " +
                "boss=" + name +
                " sequenceId=" + sequenceId +
                " phase=" + phase +
                " bossPositionYBefore=" + beforeY.ToString("F3") +
                " bossPositionYAfter=" + afterY.ToString("F3") +
                " bossVelocityYBefore=" + beforeVelocityY.ToString("F3") +
                " bossVelocityYAfter=" + afterVelocityY.ToString("F3") +
                " bossGrounded=" + IsGroundedForAttack() +
                " bossIsKinematic=" + rb.isKinematic +
                " bossUseGravity=" + rb.useGravity +
                " playerPositionY=" + (playerTarget != null ? playerTarget.position.y.ToString("F3") : "None") +
                " devourAnchorPosition=" + bossDevourGroundAnchorPosition +
                " activeBossAttackKind=" + activeBossAttackKind +
                " bossPositionWrittenThisFrame=" + wrotePosition +
                " writeSource=MaintainBossDevourGroundAnchor",
                this);
        }

        if (unexpectedVerticalMovement)
        {
            Debug.LogWarning(
                "[BossDevourFlightTrace] event=UnexpectedBossVerticalMovement " +
                "boss=" + name +
                " sequenceId=" + sequenceId +
                " phase=" + phase +
                " deltaY=" + (beforeY - bossDevourGroundAnchorPosition.y).ToString("F3") +
                " source=PhysicsOrExternalWriteBeforeGroundLock" +
                " stackContext=MaintainBossDevourGroundAnchor" +
                " bossPositionBefore=" + beforePosition +
                " bossPositionAfter=" + transform.position,
                this);
        }
    }

    private void RestoreBossDevourBodyOverride(string reason)
    {
        if (rb == null || !bossDevourBodyOverrideActive)
        {
            return;
        }

        rb.position = bossDevourGroundAnchorPosition;
        transform.position = bossDevourGroundAnchorPosition;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints = bossDevourBodyConstraintsBefore;
        rb.isKinematic = bossDevourBodyIsKinematicBefore;
        rb.useGravity = bossDevourBodyUseGravityBefore;
        bossDevourBodyOverrideActive = false;
        Physics.SyncTransforms();

        Debug.Log(
            "[BossDevourFlightTrace] event=DevourOverrideEnd " +
            "boss=" + name +
            " reason=" + reason +
            " bossPositionYBefore=" + bossDevourGroundAnchorPosition.y.ToString("F3") +
            " bossPositionYAfter=" + transform.position.y.ToString("F3") +
            " bossVelocityYBefore=0.000" +
            " bossVelocityYAfter=" + rb.linearVelocity.y.ToString("F3") +
            " bossGrounded=" + IsGroundedForAttack() +
            " bossIsKinematic=" + rb.isKinematic +
            " bossUseGravity=" + rb.useGravity +
            " devourAnchorPosition=" + bossDevourGroundAnchorPosition +
            " bossPositionWrittenThisFrame=true" +
            " writeSource=RestoreBossDevourBodyOverride",
            this);
    }

    private void MoveBossBody(Vector3 position)
    {
        if (rb != null)
        {
            rb.position = position;
            rb.linearVelocity = Vector3.zero;
        }

        transform.position = position;

        if (bossLeapBodyOverrideActive && (debugAttackDiagnostics || debugLog))
        {
            Debug.Log(
                "[BossLaunchDebug] move body during leap " +
                "boss=" + name +
                " position=" + position +
                " rbVelocity=" + (rb != null ? rb.linearVelocity.ToString() : "None") +
                " useGravity=" + (rb != null && rb.useGravity) +
                " isKinematic=" + (rb != null && rb.isKinematic),
                this);
        }
    }

    private void ZeroBossHorizontalVelocityPreserveVertical(bool grounded, string currentAttackState, string reason)
    {
        if (rb == null)
        {
            return;
        }

        Vector3 before = rb.linearVelocity;
        Vector3 after = grounded
            ? Vector3.zero
            : new Vector3(0f, before.y, 0f);
        rb.linearVelocity = after;

        LogBossLaunchVelocityState(reason, currentAttackState, grounded, before, after, true);
    }

    private void BeginBossLeapBodyOverride()
    {
        if (rb == null || bossLeapBodyOverrideActive)
        {
            return;
        }

        bossLeapBodyUseGravityBefore = rb.useGravity;
        bossLeapBodyIsKinematicBefore = rb.isKinematic;
        bossLeapBodyConstraintsBefore = rb.constraints;
        bossLeapBodyOverrideActive = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        if (debugAttackDiagnostics || debugLog)
        {
            Debug.Log(
                "[BossLaunchDebug] begin leap body override " +
                "boss=" + name +
                " velocityBefore=" + rb.linearVelocity +
                " useGravityBefore=" + bossLeapBodyUseGravityBefore +
                " isKinematicBefore=" + bossLeapBodyIsKinematicBefore +
                " constraintsBefore=" + bossLeapBodyConstraintsBefore,
                this);
        }
    }

    private void RestoreBossLeapBodyOverride(string reason)
    {
        if (rb == null || !bossLeapBodyOverrideActive)
        {
            return;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints = bossLeapBodyConstraintsBefore;
        rb.isKinematic = bossLeapBodyIsKinematicBefore;
        rb.useGravity = bossLeapBodyUseGravityBefore;
        bossLeapBodyOverrideActive = false;

        bool groundedAfterRestore = IsGroundedForAttack();
        if (debugAttackDiagnostics || debugLog || !groundedAfterRestore)
        {
            Debug.Log(
                "[BossLaunchDebug] restore leap body override " +
                "boss=" + name +
                " reason=" + reason +
                " velocityAfter=" + rb.linearVelocity +
                " useGravity=" + rb.useGravity +
                " isKinematic=" + rb.isKinematic +
                " constraints=" + rb.constraints +
                " grounded=" + groundedAfterRestore +
                " groundProbeHit=" + lastGroundProbeHit +
                " groundHitName=" + lastGroundHitName +
                " groundHitLayer=" + (lastGroundHitLayer >= 0 ? LayerMask.LayerToName(lastGroundHitLayer) : "None"),
                this);
        }
    }

    private void LogBossLaunchVelocityState(
        string reason,
        string currentAttackState,
        bool grounded,
        Vector3 velocityBefore,
        Vector3 velocityAfter,
        bool velocityWasOverwritten)
    {
        if (!(debugAttackDiagnostics || debugLog))
        {
            return;
        }

        if (Time.time < nextBossLaunchDebugTime)
        {
            return;
        }

        nextBossLaunchDebugTime = Time.time + 0.08f;

        Debug.Log(
            "[BossLaunchDebug] " +
            "reason=" + reason +
            " boss=" + name +
            " posY=" + transform.position.y.ToString("F3") +
            " rbVelocityYBefore=" + velocityBefore.y.ToString("F3") +
            " rbVelocityYAfter=" + velocityAfter.y.ToString("F3") +
            " rbVelocityBefore=" + velocityBefore +
            " rbVelocityAfter=" + velocityAfter +
            " useGravity=" + (rb != null && rb.useGravity) +
            " isKinematic=" + (rb != null && rb.isKinematic) +
            " constraints=" + (rb != null ? rb.constraints.ToString() : "None") +
            " grounded=" + grounded +
            " groundProbeHit=" + lastGroundProbeHit +
            " groundHitName=" + lastGroundHitName +
            " groundHitLayer=" + (lastGroundHitLayer >= 0 ? LayerMask.LayerToName(lastGroundHitLayer) : "None") +
            " currentState=" + currentAttackState +
            " activeBossAttackKind=" + activeBossAttackKind +
            " velocityWasOverwritten=" + velocityWasOverwritten,
            this);
    }

    private void EnsureBossLeapLandingBuffer()
    {
        if (bossLeapLandingHitResults == null || bossLeapLandingHitResults.Length < 16)
        {
            bossLeapLandingHitResults = new Collider[16];
        }
    }

    private bool TryTriggerBossLandingImpact(
        Vector3 landingPosition,
        BossLandingImpactSource source,
        int actionSequenceId = 0,
        int airborneSequenceId = 0)
    {
        int sequenceId = ResolveLandingImpactSequenceId(source, actionSequenceId, airborneSequenceId);
        LogBossLandingTrace(
            "TryTriggerEntered",
            sequenceId,
            "source", source,
            "alreadyTriggered", bossFallingImpactTriggered,
            "landingPosition", landingPosition);

        if (source == BossLandingImpactSource.LeapSlam)
        {
            bool leapActive = IsBossActionActive(BossAttackKind.LeapSlam, sequenceId);
            Debug.Log(
                "[BossLeapSlamTrace] event=LeapImpactAccepted " +
                "actionSequenceId=" + sequenceId +
                " airborneSequenceId=" + bossCurrentAirborneSequenceId +
                " activeActionSequenceId=" + activeBossActionSequenceId +
                " activeKind=" + activeBossAttackKind +
                " alreadyTriggered=" + bossFallingImpactTriggered +
                " leapActive=" + leapActive +
                " landingPosition=" + landingPosition,
                this);
        }

        CombatHealth selfHealth = GetComponent<CombatHealth>();
        if (selfHealth != null && selfHealth.IsDead)
        {
            LogLandingImpactSkipped(source, "Dead", landingPosition);
            return false;
        }

        if (bossBodyHiddenForSplit)
        {
            LogLandingImpactSkipped(source, "Hidden", landingPosition);
            return false;
        }

        if (bossFallingImpactTriggered)
        {
            LogLandingImpactSkipped(source, "AlreadyTriggeredForSequence", landingPosition);
            return false;
        }

        bossFallingImpactTriggered = true;
        bossFallingImpactArmed = false;
        bossWasAirborne = true;
        bossLandingImpactAwaitGroundReset = true;
        bossAirborneStartY = landingPosition.y;
        bossHighestAirborneY = landingPosition.y;
        bossLastGroundedY = lastGroundProbeHit ? lastGroundHitY : landingPosition.y;
        bossWasGroundedLastFixedUpdate = true;
        forcedAirborneImpactConsumed = true;
        forcedAirborneImpactArmed = false;
        EnsureBossLeapLandingBuffer();
        LogBossLandingTrace(
            "GameplayImpactAccepted",
            sequenceId,
            "source", source,
            "landingPosition", landingPosition);

        int hitCount = Physics.OverlapSphereNonAlloc(
            landingPosition,
            Mathf.Max(0.1f, bossLeapLandingRadius),
            bossLeapLandingHitResults,
            ~0,
            QueryTriggerInteraction.Collide);
        LogBossLandingTrace(
            "OverlapQuery",
            sequenceId,
            "source", source,
            "queryCenter", landingPosition,
            "radius", Mathf.Max(0.1f, bossLeapLandingRadius),
            "hitCount", hitCount);

        HashSet<int> processedTargets = new HashSet<int>();
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = bossLeapLandingHitResults[i];
            if (hitCollider == null)
            {
                continue;
            }

            CombatHealth targetHealth = hitCollider.GetComponentInParent<CombatHealth>();
            Transform attackerRoot = transform.root;
            Transform targetRoot = targetHealth != null ? targetHealth.transform.root : null;

            if (targetHealth == null || targetHealth.IsDead)
            {
                LogBossLandingTrace(
                    "ColliderEvaluated",
                    sequenceId,
                    "source", source,
                    "collider", hitCollider.name,
                    "targetHealth", targetHealth != null ? targetHealth.name : "null",
                    "accepted", false,
                    "rejectReason", targetHealth == null ? "NoCombatHealth" : "TargetDead");
                continue;
            }

            if (!BattleTargetUtility.IsPlayer(targetHealth.gameObject))
            {
                LogBossLandingTrace(
                    "ColliderEvaluated",
                    sequenceId,
                    "source", source,
                    "collider", hitCollider.name,
                    "targetHealth", targetHealth.name,
                    "accepted", false,
                    "rejectReason", "NotPlayer");
                continue;
            }

            if (targetRoot == attackerRoot)
            {
                LogBossLandingTrace(
                    "ColliderEvaluated",
                    sequenceId,
                    "source", source,
                    "collider", hitCollider.name,
                    "targetHealth", targetHealth.name,
                    "accepted", false,
                    "rejectReason", "SameRoot");
                continue;
            }

            int targetKey = targetHealth.GetInstanceID();
            if (!processedTargets.Add(targetKey))
            {
                LogBossLandingTrace(
                    "ColliderEvaluated",
                    sequenceId,
                    "source", source,
                    "collider", hitCollider.name,
                    "targetHealth", targetHealth.name,
                    "accepted", false,
                    "rejectReason", "DuplicateTarget");
                continue;
            }

            Rigidbody targetBody = targetHealth.GetComponentInParent<Rigidbody>();
            if (targetBody != null && targetBody == rb)
            {
                LogBossLandingTrace(
                    "ColliderEvaluated",
                    sequenceId,
                    "source", source,
                    "collider", hitCollider.name,
                    "targetHealth", targetHealth.name,
                    "accepted", false,
                    "rejectReason", "SameRigidbody");
                continue;
            }

            Collider playerCollider = ResolvePlayerCollider(targetHealth.transform);
            Collider bossCollider = meleeEnemyCollider != null ? meleeEnemyCollider : GetComponent<Collider>();
            Vector3 playerCenter = ResolvePlayerBodyCenter(targetHealth.transform);
            bool usedFallbackDirection;
            bool penetrationDetected;
            bool overlapEscapeApplied;
            Vector3 horizontalDirection = ResolveBossLeapLandingHorizontalDirection(
                targetHealth.transform,
                playerCenter,
                bossCollider,
                playerCollider,
                sequenceId,
                out usedFallbackDirection,
                out bool centerOverlapDetected,
                out penetrationDetected,
                out overlapEscapeApplied,
                out string fallbackSource,
                out float fallbackAngle,
                out Vector3 separationOffset);

            float damage = Mathf.Max(0f, bossLeapSlamLandingDamage);
            Vector3 launchVelocity = horizontalDirection * Mathf.Max(0f, bossLeapSlamKnockbackHorizontal) + Vector3.up * Mathf.Max(0f, bossLeapSlamKnockbackVertical);
            float playerHpBefore = ResolveCombatHealthValue(targetHealth);
            float playerShieldBefore = targetHealth.GetShield();
            Debug.Log(
                "[BossLaunchVectorTrace] " +
                "bossPosition=" + transform.position +
                " playerPosition=" + targetHealth.transform.position +
                " rawDirection=" + (targetHealth.transform.position - transform.position) +
                " horizontalDirection=" + horizontalDirection +
                " centerOverlapDetected=" + centerOverlapDetected +
                " fallbackSource=" + fallbackSource +
                " fallbackAngle=" + fallbackAngle.ToString("F2") +
                " verticalLaunchSpeed=" + Mathf.Max(0f, bossLeapSlamKnockbackVertical).ToString("F2") +
                " finalLaunchVelocity=" + launchVelocity +
                " worldUp=" + Vector3.up,
                this);
            Debug.Log(
                "[BossLaunchCenterOverlapTrace] " +
                "sequenceId=" + sequenceId +
                " bossPosition=" + transform.position +
                " playerPosition=" + targetHealth.transform.position +
                " rawHorizontalOffset=" + Vector3.ProjectOnPlane(playerCenter - ResolveEnemyBodyCenter(), Vector3.up) +
                " horizontalSqrMagnitude=" + Vector3.ProjectOnPlane(playerCenter - ResolveEnemyBodyCenter(), Vector3.up).sqrMagnitude.ToString("F4") +
                " centerOverlapDetected=" + centerOverlapDetected +
                " penetrationDetected=" + penetrationDetected +
                " fallbackSource=" + fallbackSource +
                " fallbackAngle=" + fallbackAngle.ToString("F2") +
                " finalHorizontalDirection=" + horizontalDirection +
                " initialSeparationApplied=" + (separationOffset.sqrMagnitude > 0.0001f) +
                " initialSeparationVector=" + separationOffset +
                " finalLaunchVelocity=" + launchVelocity,
                this);
            Debug.Log(
                "[BossLaunchBalanceTrace] " +
                "horizontalKnockback=" + bossLeapSlamKnockbackHorizontal.ToString("F2") +
                " verticalKnockback=" + bossLeapSlamKnockbackVertical.ToString("F2") +
                " launchVelocity=" + launchVelocity +
                " playerStartPosition=" + targetHealth.transform.position,
                this);

            LogBossLandingTrace(
                "ColliderEvaluated",
                sequenceId,
                "source", source,
                "collider", hitCollider.name,
                "targetHealth", targetHealth.name,
                "accepted", true,
                "rejectReason", "None");
            LogBossLandingTrace(
                "PlayerTargetAccepted",
                sequenceId,
                "source", source,
                "player", targetHealth.name,
                "playerRoot", targetRoot != null ? targetRoot.name : "null",
                "playerCollider", playerCollider != null ? playerCollider.name : "null");
            LogBossLandingTrace(
                "DamageApplying",
                sequenceId,
                "source", source,
                "player", targetHealth.name,
                "damage", damage,
                "hpBefore", playerHpBefore,
                "shieldBefore", playerShieldBefore);
            if (source == BossLandingImpactSource.LeapSlam)
            {
                Debug.Log(
                    "[BossLeapSlamTrace] event=LeapDamageApplying " +
                    "actionSequenceId=" + sequenceId +
                    " target=" + targetHealth.name +
                    " damage=" + damage.ToString("F2"),
                    this);
            }

            targetHealth.TakeDamage(new BattleDamage(damage, BattleDamageType.Physical, gameObject));
            float playerHpAfter = ResolveCombatHealthValue(targetHealth);
            float playerShieldAfter = targetHealth.GetShield();
            LogBossLandingTrace(
                "DamageApplied",
                sequenceId,
                "source", source,
                "player", targetHealth.name,
                "hpAfter", playerHpAfter,
                "shieldAfter", playerShieldAfter,
                "result", ResolveBossMeleeDamageResult(playerHpBefore, playerHpAfter, playerShieldBefore, playerShieldAfter));
            if (source == BossLandingImpactSource.LeapSlam)
            {
                Debug.Log(
                    "[BossLeapSlamTrace] event=LeapDamageApplied " +
                    "actionSequenceId=" + sequenceId +
                    " target=" + targetHealth.name +
                    " healthBefore=" + playerHpBefore.ToString("F2") +
                    " healthAfter=" + playerHpAfter.ToString("F2") +
                    " shieldBefore=" + playerShieldBefore.ToString("F2") +
                    " shieldAfter=" + playerShieldAfter.ToString("F2"),
                    this);
            }
            LogBossLandingTrace(
                "ExternalLaunchDispatch",
                sequenceId,
                "source", source,
                "player", targetHealth.name,
                "launchVelocity", launchVelocity,
                "separationOffset", separationOffset);
            if (!TryRegisterBossLaunchTarget(sequenceId, targetHealth, out string launchBlockReason))
            {
                Debug.Log(
                    "[BossLaunchRepeatTrace] event=LaunchDispatchBlocked " +
                    "frame=" + Time.frameCount +
                    " fixedTime=" + Time.fixedTime.ToString("F3") +
                    " landingSource=" + source +
                    " actionSequenceId=" + activeBossActionSequenceId +
                    " airborneSequenceId=" + bossCurrentAirborneSequenceId +
                    " forcedSequenceId=" + forcedAirborneSequenceId +
                    " target=" + targetHealth.name +
                    " launchVelocity=" + launchVelocity +
                    " alreadyLaunchedTargetThisSequence=true" +
                    " blockReason=" + launchBlockReason,
                    this);
                continue;
            }
            ApplyBossLeapLaunchToPlayer(
                targetHealth.transform,
                bossCollider,
                playerCollider,
                centerOverlapDetected,
                penetrationDetected,
                launchVelocity,
                separationOffset,
                sequenceId,
                source);

            if (debugAttackDiagnostics || debugLog)
            {
                Vector3 bossCenter = ResolveEnemyBodyCenter();
                Vector3 flattenedPlayerCenter = playerCenter;
                flattenedPlayerCenter.y = bossCenter.y;
                float distanceAfterImpact = Vector3.Distance(
                    new Vector3(bossCenter.x, 0f, bossCenter.z),
                    new Vector3(flattenedPlayerCenter.x, 0f, flattenedPlayerCenter.z));

                Debug.Log(
                    "[BossLaunchDebug] event=LandingImpactTriggered " +
                    "source=" + source +
                    " sequenceId=" + bossLandingImpactSequenceId +
                    " boss=" + name +
                    " landingPosition=" + landingPosition +
                    " hitCollider=" + hitCollider.name +
                    " playerRoot=" + (targetRoot != null ? targetRoot.name : "null") +
                    " damage=" + damage.ToString("F2") +
                    " horizontalDirection=" + horizontalDirection +
                    " launchVelocity=" + launchVelocity +
                    " usedFallbackDirection=" + usedFallbackDirection +
                    " penetrationDetected=" + penetrationDetected +
                    " overlapEscapeApplied=" + overlapEscapeApplied +
                    " distanceToBossCenterAfterImpact=" + distanceAfterImpact.ToString("F2"),
                    this);
            }
        }

        PlayLandingVfxSafely(landingPosition, sequenceId, source);

        return true;
    }

    private void UpdateBossAirborneLandingImpact()
    {
        if (rb == null)
        {
            return;
        }

        CombatHealth selfHealth = GetComponent<CombatHealth>();
        if ((selfHealth != null && selfHealth.IsDead) || bossBodyHiddenForSplit || rb.isKinematic)
        {
            bool groundedSnapshot = ProbeGrounded();
            float groundedY = lastGroundProbeHit ? lastGroundHitY : transform.position.y;
            ResetBossAirborneLandingState(groundedY, groundedSnapshot);
            return;
        }

        bool isGrounded = ProbeGrounded();
        float currentY = transform.position.y;
        float verticalVelocity = rb.linearVelocity.y;
        float currentGroundY = lastGroundProbeHit ? lastGroundHitY : currentY;
        float heightAboveGround = ResolveBossHeightAboveGround(currentGroundY);

        if (isGrounded)
        {
            bossLastGroundedY = currentGroundY;
        }

        bool shouldTrackAirborne = !isGrounded &&
                                   !bossLandingImpactAwaitGroundReset &&
                                   (bossWasAirborne ||
                                    verticalVelocity > 0.1f ||
                                    Mathf.Abs(currentY - bossLastGroundedY) > 0.15f);

        if (shouldTrackAirborne && !bossWasAirborne)
        {
            bossWasAirborne = true;
            bossFallingImpactArmed = false;
            bossFallingImpactTriggered = false;
            bossAirborneStartY = currentY;
            bossHighestAirborneY = currentY;
            bossCurrentAirborneSequenceId = bossLandingImpactSequenceId + 1;
            LogBossLandingTrace(
                "AirborneStarted",
                bossCurrentAirborneSequenceId,
                "positionY", currentY,
                "groundY", currentGroundY,
                "verticalVelocity", verticalVelocity,
                "activeBossAttackKind", activeBossAttackKind,
                "currentState", ResolveBossLaunchCurrentState());
        }

        if (bossLandingImpactAwaitGroundReset && !isGrounded)
        {
            bossWasGroundedLastFixedUpdate = false;
            return;
        }

        if (bossWasAirborne)
        {
            bossHighestAirborneY = Mathf.Max(bossHighestAirborneY, currentY);
            float airborneHeight = bossHighestAirborneY - bossLastGroundedY;
            if (!bossFallingImpactArmed &&
                airborneHeight >= Mathf.Max(0f, BossFallingImpactMinimumHeight) &&
                verticalVelocity <= -Mathf.Max(0f, BossFallingImpactMinimumDownwardSpeed))
            {
                bossFallingImpactArmed = true;
                LogBossLandingTrace(
                    "LandingImpactArmed",
                    bossCurrentAirborneSequenceId,
                    "airborneHeight", airborneHeight,
                    "highestY", bossHighestAirborneY,
                    "groundY", bossLastGroundedY,
                    "downwardVelocity", verticalVelocity,
                    "source", BossLandingImpactSource.AirborneFall);
            }

            if (bossFallingImpactArmed &&
                !bossFallingImpactTriggered &&
                verticalVelocity < 0f &&
                TryTriggerBossAirbornePlayerContactImpact(currentY, verticalVelocity))
            {
                bossWasGroundedLastFixedUpdate = isGrounded;
                return;
            }

            if (bossFallingImpactArmed &&
                !bossFallingImpactTriggered &&
                TryTriggerBossFallingHeightThresholdImpact(verticalVelocity))
            {
                bossWasGroundedLastFixedUpdate = isGrounded;
                return;
            }
        }

        UpdateForcedBossAirborneImpact(currentY, currentGroundY, heightAboveGround, verticalVelocity, isGrounded);

        if (!bossWasGroundedLastFixedUpdate && isGrounded)
        {
            LogBossLandingTrace(
                "LandingTransitionDetected",
                ResolveActiveBossLandingSequenceId(),
                "wasGrounded", false,
                "isGrounded", true,
                "armed", bossFallingImpactArmed,
                "alreadyTriggered", bossFallingImpactTriggered,
                "activeBossAttackKind", activeBossAttackKind,
                "source", BossLandingImpactSource.AirborneFall,
                "currentState", ResolveBossLaunchCurrentState());

            if (bossWasAirborne && bossFallingImpactArmed && !bossFallingImpactTriggered)
            {
                TryTriggerBossLandingImpact(transform.position, BossLandingImpactSource.AirborneFall, 0, bossCurrentAirborneSequenceId);
            }
            else
            {
                string skipReason = !bossWasAirborne
                    ? "NotAirborne"
                    : bossFallingImpactTriggered
                        ? "AlreadyTriggeredForSequence"
                        : "NotArmed";
                LogLandingImpactSkipped(BossLandingImpactSource.AirborneFall, skipReason, transform.position);
                bossFallingImpactTriggered = false;
            }

            bossWasAirborne = false;
            bossFallingImpactArmed = false;
            bossLandingImpactAwaitGroundReset = false;
            bossLastGroundedY = currentGroundY;
        }

        bossWasGroundedLastFixedUpdate = isGrounded;
    }

    private void ResetBossAirborneLandingState(float groundedY, bool grounded)
    {
        bossWasAirborne = false;
        bossFallingImpactArmed = false;
        bossFallingImpactTriggered = false;
        bossCurrentAirborneSequenceId = 0;
        bossAirborneStartY = transform.position.y;
        bossHighestAirborneY = transform.position.y;
        bossLastGroundedY = groundedY;
        bossWasGroundedLastFixedUpdate = grounded;
        bossLandingImpactAwaitGroundReset = false;
        forcedAirborneImpactArmed = false;
        forcedAirborneImpactConsumed = false;
        forcedAirborneHighestHeight = 0f;
        forcedAirborneStartTime = 0f;
        forcedAirborneSequenceId = 0;
        bossLaunchedTargetIdsForImpactSequence.Clear();
        bossLaunchedTargetSequenceId = 0;
    }

    private void UpdateForcedBossAirborneImpact(float currentY, float currentGroundY, float heightAboveGround, float verticalVelocity, bool isGrounded)
    {
        if (!enableForcedAirborneImpact)
        {
            return;
        }

        if (!forcedAirborneImpactArmed &&
            heightAboveGround >= Mathf.Max(0f, forcedAirborneImpactArmHeight))
        {
            forcedAirborneImpactArmed = true;
            forcedAirborneImpactConsumed = false;
            forcedAirborneHighestHeight = heightAboveGround;
            forcedAirborneStartTime = Time.time;
            forcedAirborneSequenceId = Mathf.Max(bossCurrentAirborneSequenceId, bossLandingImpactSequenceId) + 1;
            Debug.Log(
                "[BossLeapSlamTrace] event=ForcedAirborneImpactArmed " +
                "forcedSequenceId=" + forcedAirborneSequenceId +
                " heightAboveGround=" + heightAboveGround.ToString("F3") +
                " currentY=" + currentY.ToString("F3") +
                " groundY=" + currentGroundY.ToString("F3") +
                " verticalVelocity=" + verticalVelocity.ToString("F3") +
                " activeKind=" + activeBossAttackKind,
                this);
        }

        if (!forcedAirborneImpactArmed || forcedAirborneImpactConsumed)
        {
            return;
        }

        forcedAirborneHighestHeight = Mathf.Max(forcedAirborneHighestHeight, heightAboveGround);
        int playerCount = CountBossLandingPlayersInImpactZone(ResolveEnemyBodyCenter(), Mathf.Max(0.1f, forcedAirborneImpactPlayerRadius));
        bool playerNearby = playerCount > 0;
        bool lowAltitudeDescending = heightAboveGround <= Mathf.Max(0f, forcedAirborneImpactTriggerHeight) && verticalVelocity <= 0f;
        bool stalledAbovePlayer = !isGrounded && Mathf.Abs(verticalVelocity) <= 0.15f && playerNearby;
        bool timedOutAbovePlayer = Time.time - forcedAirborneStartTime >= Mathf.Max(0.1f, forcedAirborneImpactTimeout) && playerNearby;

        if (!(lowAltitudeDescending || stalledAbovePlayer || timedOutAbovePlayer))
        {
            return;
        }

        if (TryTriggerBossLandingImpact(
            ResolveEnemyBodyCenter(),
            BossLandingImpactSource.ForcedAirborneHeight,
            0,
            forcedAirborneSequenceId))
        {
            forcedAirborneImpactConsumed = true;
            forcedAirborneImpactArmed = false;
            Debug.Log(
                "[BossLeapSlamTrace] event=ForcedAirborneImpactTriggered " +
                "forcedSequenceId=" + forcedAirborneSequenceId +
                " heightAboveGround=" + heightAboveGround.ToString("F3") +
                " verticalVelocity=" + verticalVelocity.ToString("F3") +
                " playerCount=" + playerCount +
                " reason=" + (lowAltitudeDescending ? "LowAltitudeDescending" : stalledAbovePlayer ? "StalledAbovePlayer" : "Timeout"),
                this);
        }
    }

    private float ResolveBossHeightAboveGround(float currentGroundY)
    {
        ResolveMeleeHitSources();
        Collider bossCollider = meleeEnemyCollider != null ? meleeEnemyCollider : GetComponent<Collider>();
        if (bossCollider == null)
        {
            return Mathf.Max(0f, transform.position.y - currentGroundY);
        }

        return Mathf.Max(0f, bossCollider.bounds.min.y - currentGroundY);
    }

    private bool IsForcedAirborneImpactPending()
    {
        return enableForcedAirborneImpact &&
               forcedAirborneImpactArmed &&
               !forcedAirborneImpactConsumed;
    }

    private int ResolveLandingImpactSequenceId(BossLandingImpactSource source, int actionSequenceId, int airborneSequenceId)
    {
        if (source == BossLandingImpactSource.LeapSlam)
        {
            return actionSequenceId > 0
                ? actionSequenceId
                : activeBossActionSequenceId;
        }

        if (source == BossLandingImpactSource.ForcedAirborneHeight)
        {
            return airborneSequenceId > 0
                ? airborneSequenceId
                : forcedAirborneSequenceId;
        }

        if (airborneSequenceId > 0)
        {
            bossCurrentAirborneSequenceId = airborneSequenceId;
            bossLandingImpactSequenceId = airborneSequenceId;
            return airborneSequenceId;
        }

        return ResolveActiveBossLandingSequenceId();
    }

    private int ResolveActiveBossLandingSequenceId()
    {
        if (bossCurrentAirborneSequenceId <= 0)
        {
            bossCurrentAirborneSequenceId = bossLandingImpactSequenceId + 1;
        }

        bossLandingImpactSequenceId = bossCurrentAirborneSequenceId;
        return bossCurrentAirborneSequenceId;
    }

    private bool TryTriggerBossAirbornePlayerContactImpact(float currentY, float verticalVelocity)
    {
        if (playerTarget == null)
        {
            LogBossLandingTrace(
                "AirbornePlayerContact",
                ResolveActiveBossLandingSequenceId(),
                "accepted", false,
                "rejectReason", "NoPlayerTarget",
                "bossY", currentY,
                "verticalVelocity", verticalVelocity);
            return false;
        }

        CombatHealth targetHealth = playerTarget.GetComponentInParent<CombatHealth>();
        Collider bossCollider = meleeEnemyCollider != null ? meleeEnemyCollider : GetComponent<Collider>();
        Collider playerCollider = ResolvePlayerCollider(playerTarget);
        if (targetHealth == null || bossCollider == null || playerCollider == null)
        {
            LogBossLandingTrace(
                "AirbornePlayerContact",
                ResolveActiveBossLandingSequenceId(),
                "accepted", false,
                "rejectReason", targetHealth == null ? "NoPlayerCombatHealth" : bossCollider == null ? "NoBossCollider" : "NoPlayerCollider",
                "bossY", currentY,
                "verticalVelocity", verticalVelocity);
            return false;
        }

        Vector3 bossCenter = ResolveEnemyBodyCenter();
        Vector3 playerCenter = ResolvePlayerBodyCenter(targetHealth.transform);
        bool penetrationDetected = Physics.ComputePenetration(
            bossCollider,
            bossCollider.transform.position,
            bossCollider.transform.rotation,
            playerCollider,
            playerCollider.transform.position,
            playerCollider.transform.rotation,
            out Vector3 penetrationDirection,
            out float penetrationDistance);

        bool playerUnderBoss = playerCenter.y <= bossCenter.y + 0.1f;
        LogBossLandingTrace(
            "AirbornePlayerContact",
            ResolveActiveBossLandingSequenceId(),
            "accepted", penetrationDetected && playerUnderBoss,
            "rejectReason", penetrationDetected ? (playerUnderBoss ? "None" : "PlayerAboveBoss") : "NoPenetration",
            "bossY", currentY,
            "verticalVelocity", verticalVelocity,
            "bossCenter", bossCenter,
            "playerCenter", playerCenter,
            "penetrationDetected", penetrationDetected,
            "penetrationDirection", penetrationDirection,
            "penetrationDistance", penetrationDistance);

        if (!penetrationDetected || !playerUnderBoss)
        {
            return false;
        }

        return TryTriggerBossLandingImpact(ResolveEnemyBodyCenter(), BossLandingImpactSource.AirbornePlayerContact, 0, bossCurrentAirborneSequenceId);
    }

    private bool TryTriggerBossFallingHeightThresholdImpact(float verticalVelocity)
    {
        if (!EnableBossFallingHeightTrigger || rb == null || rb.isKinematic || !bossWasAirborne || !bossFallingImpactArmed || bossFallingImpactTriggered)
        {
            return false;
        }

        if (verticalVelocity > -Mathf.Max(0f, BossFallingImpactRequiredDownwardSpeed))
        {
            return false;
        }

        if (!TryResolveBossGroundReference(out Collider groundCollider, out float bossBottomY, out float groundY, out Vector3 gameplayImpactCenter))
        {
            if ((debugAttackDiagnostics || debugLog) && Time.time >= nextBossLandingHeightTraceTime)
            {
                nextBossLandingHeightTraceTime = Time.time + 0.1f;
                LogBossLandingTrace(
                    "FallingHeightCheck",
                    ResolveActiveBossLandingSequenceId(),
                    "bossPivotY", transform.position.y,
                    "bossBottomY", float.NaN,
                    "groundY", float.NaN,
                    "heightAboveGround", float.NaN,
                    "triggerHeight", BossFallingImpactTriggerHeight,
                    "velocityY", verticalVelocity,
                    "armed", bossFallingImpactArmed,
                    "alreadyTriggered", bossFallingImpactTriggered,
                    "playerBelowFound", false,
                    "skippedReason", "GroundNotResolved");
            }

            return false;
        }

        float heightAboveGround = bossBottomY - groundY;
        int playerCount = CountBossLandingPlayersInImpactZone(gameplayImpactCenter, Mathf.Max(0.1f, BossFallingImpactPlayerCheckRadius));
        bool playerBelowFound = playerCount > 0;

        if ((debugAttackDiagnostics || debugLog) && Time.time >= nextBossLandingHeightTraceTime)
        {
            nextBossLandingHeightTraceTime = Time.time + 0.1f;
            LogBossLandingTrace(
                "FallingHeightCheck",
                ResolveActiveBossLandingSequenceId(),
                "bossPivotY", transform.position.y,
                "bossBottomY", bossBottomY,
                "groundY", groundY,
                "heightAboveGround", heightAboveGround,
                "triggerHeight", BossFallingImpactTriggerHeight,
                "velocityY", verticalVelocity,
                "armed", bossFallingImpactArmed,
                "alreadyTriggered", bossFallingImpactTriggered,
                "playerBelowFound", playerBelowFound,
                "groundCollider", groundCollider != null ? groundCollider.name : "null");
        }

        if (heightAboveGround < 0f || heightAboveGround > Mathf.Max(BossFallingImpactTriggerHeight, BossFallingImpactMaximumTriggerHeight))
        {
            return false;
        }

        if (heightAboveGround > Mathf.Max(0f, BossFallingImpactTriggerHeight))
        {
            return false;
        }

        if (!playerBelowFound)
        {
            LogLandingImpactSkipped(BossLandingImpactSource.FallingHeightThreshold, "NoPlayerBelow", gameplayImpactCenter);
            return false;
        }

        LogBossLandingTrace(
            "FallingHeightThresholdReached",
            ResolveActiveBossLandingSequenceId(),
            "heightAboveGround", heightAboveGround,
            "gameplayImpactCenter", gameplayImpactCenter,
            "playerCount", playerCount);

        return TryTriggerBossLandingImpact(gameplayImpactCenter, BossLandingImpactSource.FallingHeightThreshold, 0, bossCurrentAirborneSequenceId);
    }

    private bool TryResolveBossGroundReference(out Collider groundCollider, out float bossBottomY, out float groundY, out Vector3 gameplayImpactCenter)
    {
        ResolveMeleeHitSources();
        Collider bossCollider = meleeEnemyCollider != null ? meleeEnemyCollider : GetComponent<Collider>();
        groundCollider = null;
        bossBottomY = float.NaN;
        groundY = float.NaN;
        gameplayImpactCenter = transform.position;

        if (bossCollider == null)
        {
            return false;
        }

        Bounds bounds = bossCollider.bounds;
        bossBottomY = bounds.min.y;
        Vector3 rayOrigin = new Vector3(bounds.center.x, bounds.max.y + 0.2f, bounds.center.z);
        float rayDistance = Mathf.Max(2f, bounds.size.y + Mathf.Max(0f, BossFallingImpactMaximumTriggerHeight) + 2f);
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, rayDistance, BossFallingImpactGroundMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        Transform bossRoot = transform.root;
        Transform playerRoot = playerTarget != null ? playerTarget.root : null;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || !hitCollider.enabled || hitCollider.isTrigger)
            {
                continue;
            }

            Transform hitRoot = hitCollider.transform.root;
            if (hitRoot == bossRoot)
            {
                continue;
            }

            if (playerRoot != null && hitRoot == playerRoot)
            {
                continue;
            }

            groundCollider = hitCollider;
            groundY = hits[i].point.y;
            gameplayImpactCenter = new Vector3(bounds.center.x, groundY, bounds.center.z);
            return true;
        }

        return false;
    }

    private int CountBossLandingPlayersInImpactZone(Vector3 impactCenter, float radius)
    {
        EnsureBossLeapLandingBuffer();
        int hitCount = Physics.OverlapSphereNonAlloc(
            impactCenter,
            Mathf.Max(0.1f, radius),
            bossLeapLandingHitResults,
            ~0,
            QueryTriggerInteraction.Collide);

        int playerCount = 0;
        HashSet<int> uniquePlayers = new HashSet<int>();
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = bossLeapLandingHitResults[i];
            if (hitCollider == null)
            {
                continue;
            }

            CombatHealth health = hitCollider.GetComponentInParent<CombatHealth>();
            if (health == null || health.IsDead || !BattleTargetUtility.IsPlayer(health.gameObject))
            {
                continue;
            }

            if (uniquePlayers.Add(health.GetInstanceID()))
            {
                playerCount++;
            }
        }

        return playerCount;
    }

    private void LogLandingImpactSkipped(BossLandingImpactSource source, string reason, Vector3 landingPosition)
    {
        LogBossLandingTrace(
            "LandingImpactSkipped",
            ResolveActiveBossLandingSequenceId(),
            "source", source,
            "reason", reason,
            "landingPosition", landingPosition,
            "activeBossAttackKind", activeBossAttackKind,
            "currentState", ResolveBossLaunchCurrentState());
    }

    private void PlayLandingVfx(Vector3 landingPosition)
    {
        if (!enableLandingVfx || landingVfxPrefab == null)
        {
            return;
        }

        EnemyLandingVfxUtility.PlayLandingVfx(
            landingVfxPrefab,
            landingPosition,
            landingVfxOffset,
            landingVfxLifetime,
            Quaternion.identity,
            new EnemyLandingVfxUtility.LandingVfxRuntimeTuning(
                landingVfxScaleMultiplier,
                landingVfxVerticalOffset,
                landingVfxLifetimeMultiplier,
                landingVfxEmissionMultiplier,
                landingVfxParticleSizeMultiplier,
                landingVfxSpeedMultiplier,
                enableLandingShockwave,
                landingShockwaveScaleMultiplier));
    }

    private void PlayLandingVfxSafely(Vector3 landingPosition, int sequenceId, BossLandingImpactSource source)
    {
        LogBossLandingTrace(
            "VfxAttempt",
            sequenceId,
            "source", source,
            "enabled", enableLandingVfx,
            "prefab", landingVfxPrefab != null ? landingVfxPrefab.name : "null",
            "landingPosition", landingPosition);

        if (!enableLandingVfx)
        {
            LogBossLandingTrace("VfxSkipped", sequenceId, "source", source, "reason", "Disabled");
            return;
        }

        if (landingVfxPrefab == null)
        {
            LogBossLandingTrace("VfxSkipped", sequenceId, "source", source, "reason", "NullPrefab");
            return;
        }

        try
        {
            PlayLandingVfx(landingPosition);
            LogBossLandingTrace("VfxPlayed", sequenceId, "source", source, "prefab", landingVfxPrefab.name);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning(
                "[BossLandingImpactTrace] event=VfxSkipped sequenceId=" + sequenceId +
                " boss=" + name +
                " source=" + source +
                " reason=Exception" +
                " message=" + ex.Message,
                this);
        }
    }

    private void LogBossLandingTrace(string eventName, int sequenceId, params object[] fields)
    {
        string message =
            "[BossLandingImpactTrace] event=" + eventName +
            " sequenceId=" + sequenceId +
            " boss=" + name;

        for (int i = 0; i + 1 < fields.Length; i += 2)
        {
            message += " " + fields[i] + "=" + fields[i + 1];
        }

        Debug.Log(message, this);
    }

    private string ResolveBossLaunchCurrentState()
    {
        if (bossBodyHiddenForSplit)
        {
            return "HiddenForSplit";
        }

        if (attackInProgress)
        {
            return EnemyAttackRuntimeState.AttackInProgress.ToString();
        }

        if (activeBossAttackKind != BossAttackKind.None)
        {
            return activeBossAttackKind.ToString();
        }

        return EnemyAttackRuntimeState.Chase.ToString();
    }

    private Vector3 ResolveBossLeapLandingHorizontalDirection(
        Transform target,
        Vector3 playerCenter,
        Collider bossCollider,
        Collider playerCollider,
        int sequenceId,
        out bool usedFallbackDirection,
        out bool centerOverlapDetected,
        out bool penetrationDetected,
        out bool overlapEscapeApplied,
        out string fallbackSource,
        out float fallbackAngle,
        out Vector3 separationOffset)
    {
        Vector3 bossCenter = ResolveEnemyBodyCenter();
        Vector3 rawHorizontalOffset = Vector3.ProjectOnPlane(playerCenter - bossCenter, Vector3.up);
        Vector3 horizontalDirection = rawHorizontalOffset;
        penetrationDetected = false;
        overlapEscapeApplied = false;
        separationOffset = Vector3.zero;
        usedFallbackDirection = false;
        centerOverlapDetected = rawHorizontalOffset.sqrMagnitude <= Mathf.Pow(Mathf.Max(0f, bossLeapCenterOverlapHorizontalThreshold), 2f);
        fallbackSource = "None";
        fallbackAngle = 0f;

        if (bossCollider != null && playerCollider != null &&
            Physics.ComputePenetration(
                bossCollider,
                bossCollider.transform.position,
                bossCollider.transform.rotation,
                playerCollider,
                playerCollider.transform.position,
                playerCollider.transform.rotation,
                out Vector3 penetrationDirection,
                out float penetrationDistance))
        {
            penetrationDetected = true;
            Vector3 penetrationHorizontal = Vector3.ProjectOnPlane(-penetrationDirection, Vector3.up);
            if (penetrationHorizontal.sqrMagnitude > 0.0001f)
            {
                horizontalDirection = penetrationHorizontal.normalized;
                float separationDistance = Mathf.Max(Mathf.Max(0f, penetrationDistance) + 0.05f, Mathf.Max(0f, bossLeapSlamMinimumEscapeDistance));
                separationOffset = horizontalDirection * separationDistance;
                overlapEscapeApplied = separationDistance > 0f;
            }
        }

        if (horizontalDirection.sqrMagnitude < 0.0001f || centerOverlapDetected)
        {
            horizontalDirection = ResolveBossLeapLandingFallbackDirection(target, sequenceId, out fallbackSource, out fallbackAngle);
            usedFallbackDirection = true;
        }
        else
        {
            horizontalDirection.Normalize();
        }

        if (centerOverlapDetected)
        {
            separationOffset = ResolveBossLeapCenterSeparationOffset(horizontalDirection);
            overlapEscapeApplied = separationOffset.sqrMagnitude > 0.0001f;
        }
        else if (separationOffset.sqrMagnitude <= 0.0001f)
        {
            separationOffset = horizontalDirection * Mathf.Max(0f, bossLeapSlamMinimumEscapeDistance);
            overlapEscapeApplied = separationOffset.sqrMagnitude > 0.0001f;
        }

        return horizontalDirection;
    }

    private Vector3 ResolveBossLeapLandingFallbackDirection(Transform target, int sequenceId, out string fallbackSource, out float fallbackAngle)
    {
        fallbackSource = "None";
        fallbackAngle = 0f;

        if (target != null)
        {
            Rigidbody targetBody = target.GetComponentInParent<Rigidbody>();
            if (targetBody != null)
            {
                Vector3 velocityDirection = Vector3.ProjectOnPlane(targetBody.linearVelocity, Vector3.up);
                if (velocityDirection.sqrMagnitude > 0.04f)
                {
                    fallbackSource = "TargetVelocity";
                    return velocityDirection.normalized;
                }
            }
        }

        if (target != null)
        {
            Player01SkillController player1 = target.GetComponentInParent<Player01SkillController>();
            if (player1 != null)
            {
                Vector3 direction = -Vector3.ProjectOnPlane(player1.GetFacingWorldDirection(), Vector3.up);
                if (direction.sqrMagnitude > 0.0001f)
                {
                    fallbackSource = "Player01Facing";
                    return direction.normalized;
                }
            }

            Player2PrototypeController player2 = target.GetComponentInParent<Player2PrototypeController>();
            if (player2 != null)
            {
                Vector3 direction = -Vector3.ProjectOnPlane(player2.GetFacingDirection(), Vector3.up);
                if (direction.sqrMagnitude > 0.0001f)
                {
                    fallbackSource = "Player02Facing";
                    return direction.normalized;
                }
            }
        }

        Vector3 bossForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (bossForward.sqrMagnitude > 0.0001f)
        {
            float signedAngle = sequenceId % 2 == 0
                ? Mathf.Abs(bossLeapCenterFallbackAngle)
                : -Mathf.Abs(bossLeapCenterFallbackAngle);
            fallbackAngle = signedAngle;
            fallbackSource = "BossForwardAngled";
            return (Quaternion.AngleAxis(signedAngle, Vector3.up) * bossForward.normalized).normalized;
        }

        fallbackSource = "WorldLeft";
        return Vector3.left;
    }

    private Vector3 ResolveBossLeapCenterSeparationOffset(Vector3 horizontalDirection)
    {
        Vector3 initialSeparation =
            horizontalDirection * Mathf.Max(0f, bossLeapCenterSeparationHorizontal) +
            Vector3.up * Mathf.Max(0f, bossLeapCenterSeparationUpward);

        float maxDistance = Mathf.Max(0f, bossLeapMaximumInitialSeparation);
        if (maxDistance > 0f && initialSeparation.magnitude > maxDistance)
        {
            initialSeparation = initialSeparation.normalized * maxDistance;
        }

        return initialSeparation;
    }

    private void ApplyBossLeapLaunchToPlayer(
        Transform target,
        Collider bossCollider,
        Collider playerCollider,
        bool centerOverlapDetected,
        bool penetrationDetected,
        Vector3 launchVelocity,
        Vector3 separationOffset,
        int sequenceId,
        BossLandingImpactSource source)
    {
        Vector3 launchSeparationOffset = ResolveBossLaunchSeparationOffset(separationOffset);

        if (source == BossLandingImpactSource.LeapSlam)
        {
            Debug.Log(
                "[BossLaunchTrajectoryTrace] event=LaunchPrepared " +
                "requestedSource=" + source +
                " suppliedActionSequenceId=" + sequenceId +
                " activeActionSequenceId=" + activeBossActionSequenceId +
                " activeKind=" + activeBossAttackKind +
                " bossActionLocked=" + bossActionLocked +
                " launchVelocity=" + launchVelocity +
                " rawSeparationOffset=" + separationOffset +
                " clampedSeparationOffset=" + launchSeparationOffset,
                this);
        }

        if (!CanDispatchBossLaunch(source, sequenceId, out string invalidReason))
        {
            LogBossActionLockTrace("InvalidLaunchBlocked", activeBossAttackKind, false, invalidReason, sequenceId);
            if (source == BossLandingImpactSource.LeapSlam)
            {
                Debug.LogWarning(
                    "[BossLeapSlamTrace] event=LeapLaunchValidation " +
                    "requestedSource=" + source +
                    " suppliedActionSequenceId=" + sequenceId +
                    " activeActionSequenceId=" + activeBossActionSequenceId +
                    " activeKind=" + activeBossAttackKind +
                    " bossActionLocked=" + bossActionLocked +
                    " allowed=false" +
                    " denyReason=" + invalidReason,
                    this);
            }
            return;
        }

        if (target == null)
        {
            LogBossLandingTrace("ExternalLaunchDispatch", sequenceId, "source", source, "accepted", false, "rejectReason", "NullTarget");
            return;
        }

        RuneRuntimeState targetRuneState = target.GetComponentInParent<RuneRuntimeState>();
        if (targetRuneState != null && targetRuneState.IsKnockbackImmuneFromRunes)
        {
            LogBossLandingTrace("ExternalLaunchDispatch", sequenceId, "source", source, "accepted", false, "rejectReason", "RuneKnockbackImmune", "target", target.name);
            return;
        }

        IExternalLaunchReceiver selectedReceiver = ResolveExternalLaunchReceiver(target);
        Player2PrototypeController tracedPlayer2 = target.GetComponentInParent<Player2PrototypeController>();
        PlayerMovement tracedPlayerMovement = target.GetComponentInParent<PlayerMovement>();
        Rigidbody tracedRigidbody = target.GetComponentInParent<Rigidbody>();
        Rigidbody launchBody = selectedReceiver != null && selectedReceiver.ExternalLaunchBody != null ? selectedReceiver.ExternalLaunchBody : tracedRigidbody;
        Collider launchSolidCollider = ResolveLaunchSolidCollider(selectedReceiver, launchBody, target, playerCollider);
        bool shouldIsolateCollision = ShouldIsolateBossPlayerCollision(
            centerOverlapDetected,
            penetrationDetected,
            bossCollider,
            launchSolidCollider);
        Vector3 launchPositionBeforeResolution = launchBody != null ? launchBody.position : target.position;
        Debug.Log(
            "[BossLaunchDispatchTrace] " +
            "event=TargetReceived" +
            " sequenceId=" + sequenceId +
            " inputTransform=" + target.name +
            " inputHierarchyPath=" + BuildHierarchyPath(target) +
            " inputInstanceId=" + target.GetInstanceID() +
            " inputLayer=" + target.gameObject.layer +
            " inputPosition=" + target.position +
            " player2ControllerFound=" + (tracedPlayer2 != null) +
            " player2ControllerObject=" + (tracedPlayer2 != null ? tracedPlayer2.name : "null") +
            " player2ControllerInstanceId=" + (tracedPlayer2 != null ? tracedPlayer2.GetInstanceID().ToString() : "null") +
            " playerMovementFound=" + (tracedPlayerMovement != null) +
            " playerMovementObject=" + (tracedPlayerMovement != null ? tracedPlayerMovement.name : "null") +
            " playerMovementInstanceId=" + (tracedPlayerMovement != null ? tracedPlayerMovement.GetInstanceID().ToString() : "null") +
            " receiverFound=" + (selectedReceiver != null) +
            " receiverType=" + (selectedReceiver != null ? selectedReceiver.GetType().Name : "null") +
            " receiverOwner=" + (selectedReceiver != null && selectedReceiver.LaunchOwnerComponent != null ? selectedReceiver.LaunchOwnerComponent.name : "null") +
            " rigidbodyFound=" + (tracedRigidbody != null) +
            " rigidbodyObject=" + (tracedRigidbody != null ? tracedRigidbody.name : "null") +
            " rigidbodyInstanceId=" + (tracedRigidbody != null ? tracedRigidbody.GetInstanceID().ToString() : "null") +
            " solidCollider=" + (launchSolidCollider != null ? launchSolidCollider.name : "null") +
            " shouldIsolateCollision=" + shouldIsolateCollision,
            this);

        if (launchBody != null && launchSolidCollider != null)
        {
            bool safePositionResolved = false;
            Vector3 safeLaunchPosition = launchBody.position;
            float safeLaunchSeparationDistance = 0f;
            float safeLaunchGroundY = float.NaN;
            bool overlapAfterResolution = false;

            if (shouldIsolateCollision)
            {
                BeginBossPlayerCollisionIsolation(bossCollider, launchSolidCollider, launchBody, sequenceId);
                safePositionResolved = TryResolveBossPlayerSafeLaunchPosition(
                    launchBody,
                    launchSolidCollider,
                    bossCollider,
                    launchVelocity,
                    out safeLaunchPosition,
                    out safeLaunchSeparationDistance,
                    out safeLaunchGroundY,
                    out overlapAfterResolution);
                if (safePositionResolved)
                {
                    launchBody.position = safeLaunchPosition;
                    Physics.SyncTransforms();
                }

                bool playerGroundCollisionStillEnabled = true;
                if (TryResolveGroundBelowPosition(
                        safePositionResolved ? safeLaunchPosition : launchBody.position,
                        launchSolidCollider,
                        out RaycastHit groundHit))
                {
                    playerGroundCollisionStillEnabled = !Physics.GetIgnoreCollision(launchSolidCollider, groundHit.collider);
                    if (!playerGroundCollisionStillEnabled)
                    {
                        Debug.LogError(
                            "[BossPlayerCollisionIsolationTrace] event=InvalidGroundCollisionIgnoreDetected " +
                            "bossCollider=" + (bossCollider != null ? bossCollider.name : "null") +
                            " playerCollider=" + launchSolidCollider.name +
                            " groundCollider=" + groundHit.collider.name,
                            this);
                    }
                }

                Debug.Log(
                    "[BossPlayerCollisionIsolationTrace] event=SafeLaunchPositionResolved " +
                    "bossCollider=" + (bossCollider != null ? bossCollider.name : "null") +
                    " playerCollider=" + launchSolidCollider.name +
                    " positionBefore=" + launchPositionBeforeResolution +
                    " positionAfter=" + (safePositionResolved ? safeLaunchPosition.ToString() : "<unchanged>") +
                    " separationDistance=" + safeLaunchSeparationDistance.ToString("F3") +
                    " bossOverlapAfter=" + overlapAfterResolution +
                    " playerBottomY=" + (safePositionResolved ? (safeLaunchPosition.y - ExternalLaunchSafetyUtility.ResolveBottomOffset(launchBody, launchSolidCollider)).ToString("F3") : "NaN") +
                    " groundY=" + safeLaunchGroundY.ToString("F3") +
                    " playerGroundCollisionStillEnabled=" + playerGroundCollisionStillEnabled,
                    this);
            }

            if (safePositionResolved)
            {
                launchSeparationOffset = Vector3.zero;
            }
        }

        if (selectedReceiver != null)
        {
            string controllerType = selectedReceiver.GetType().Name;
            Component controllerOwner = selectedReceiver.LaunchOwnerComponent;
            Rigidbody controllerBody = selectedReceiver.ExternalLaunchBody;
            Debug.Log(
                "[BossLaunchDispatchTrace] " +
                "event=ControllerSelected" +
                " sequenceId=" + sequenceId +
                " selectedControllerType=" + controllerType +
                " selectedControllerObject=" + (controllerOwner != null ? controllerOwner.name : "null") +
                " selectedControllerInstanceId=" + (controllerOwner != null ? controllerOwner.GetInstanceID().ToString() : "null") +
                " selectedRigidbodyObject=" + (controllerBody != null ? controllerBody.name : "null") +
                " selectedRigidbodyInstanceId=" + (controllerBody != null ? controllerBody.GetInstanceID().ToString() : "null"),
                this);
            LogBossLandingTrace("ExternalLaunchDispatch", sequenceId, "source", source, "accepted", true, "controller", controllerType, "target", controllerOwner != null ? controllerOwner.name : target.name);
            Debug.Log(
                "[BossLaunchRepeatTrace] event=LaunchDispatch " +
                "frame=" + Time.frameCount +
                " fixedTime=" + Time.fixedTime.ToString("F3") +
                " landingSource=" + source +
                " actionSequenceId=" + activeBossActionSequenceId +
                " airborneSequenceId=" + bossCurrentAirborneSequenceId +
                " forcedSequenceId=" + forcedAirborneSequenceId +
                " target=" + (controllerOwner != null ? controllerOwner.name : target.name) +
                " launchVelocity=" + launchVelocity +
                " alreadyLaunchedTargetThisSequence=false",
                this);
            if (source == BossLandingImpactSource.LeapSlam)
            {
                Debug.Log(
                    "[BossLeapSlamTrace] event=LeapLaunchDispatch " +
                    "target=" + (controllerOwner != null ? controllerOwner.name : target.name) +
                    " controllerType=" + controllerType +
                    " launchVelocity=" + launchVelocity +
                    " separationOffset=" + launchSeparationOffset,
                    this);
            }
            selectedReceiver.ApplyExternalLaunch(launchVelocity, Mathf.Max(0f, bossLeapSlamLaunchInputLockDuration), launchSeparationOffset, sequenceId);
            return;
        }

        Rigidbody targetBody = target.GetComponentInParent<Rigidbody>();
        if (targetBody != null && targetBody != rb)
        {
            LogBossLandingTrace("ExternalLaunchDispatch", sequenceId, "source", source, "accepted", true, "controller", "Rigidbody", "target", targetBody.name);
            if (source == BossLandingImpactSource.LeapSlam)
            {
                Debug.Log(
                    "[BossLeapSlamTrace] event=LeapLaunchDispatch " +
                    "target=" + targetBody.name +
                    " controllerType=Rigidbody" +
                    " launchVelocity=" + launchVelocity +
                    " separationOffset=" + launchSeparationOffset,
                    this);
            }
            if (launchSeparationOffset.sqrMagnitude > 0.0001f)
            {
                Vector3 newPosition = targetBody.position + launchSeparationOffset;
                targetBody.position = newPosition;
                target.root.position = newPosition;
                Physics.SyncTransforms();
            }

            targetBody.linearVelocity = launchVelocity;
            targetBody.angularVelocity = Vector3.zero;
            targetBody.WakeUp();
            return;
        }

        LogBossLandingTrace("ExternalLaunchDispatch", sequenceId, "source", source, "accepted", false, "rejectReason", "NoLaunchReceiver");
    }

    private Collider ResolveLaunchSolidCollider(IExternalLaunchReceiver selectedReceiver, Rigidbody launchBody, Transform target, Collider fallbackPlayerCollider)
    {
        if (selectedReceiver != null &&
            selectedReceiver.LaunchOwnerComponent != null &&
            launchBody != null &&
            ExternalLaunchSafetyUtility.TryResolvePhysicalBodyCollider(selectedReceiver.LaunchOwnerComponent, launchBody, out Collider solidCollider))
        {
            return solidCollider;
        }

        if (fallbackPlayerCollider != null && fallbackPlayerCollider.enabled && !fallbackPlayerCollider.isTrigger)
        {
            return fallbackPlayerCollider;
        }

        return ResolvePlayerCollider(target);
    }

    private bool ShouldIsolateBossPlayerCollision(bool centerOverlapDetected, bool penetrationDetected, Collider bossCollider, Collider playerCollider)
    {
        if (bossCollider == null || playerCollider == null)
        {
            return false;
        }

        return centerOverlapDetected ||
               penetrationDetected ||
               IsPlayerWithinBossBottomProjection(bossCollider, playerCollider);
    }

    private bool IsPlayerWithinBossBottomProjection(Collider bossCollider, Collider playerCollider)
    {
        if (bossCollider == null || playerCollider == null)
        {
            return false;
        }

        Vector3 bossCenter = bossCollider.bounds.center;
        Vector3 playerCenter = playerCollider.bounds.center;
        bossCenter.y = 0f;
        playerCenter.y = 0f;
        float horizontalDistance = Vector3.Distance(bossCenter, playerCenter);
        float allowedDistance = ResolveHorizontalBoundsExtent(bossCollider.bounds) + ResolveHorizontalBoundsExtent(playerCollider.bounds);
        return horizontalDistance <= allowedDistance;
    }

    private float ResolveHorizontalBoundsExtent(Bounds bounds)
    {
        return Mathf.Max(bounds.extents.x, bounds.extents.z);
    }

    private bool TryResolveBossPlayerSafeLaunchPosition(
        Rigidbody playerBody,
        Collider playerCollider,
        Collider bossCollider,
        Vector3 launchVelocity,
        out Vector3 safePosition,
        out float appliedDistance,
        out float groundY,
        out bool overlapAfterResolution)
    {
        safePosition = playerBody != null ? playerBody.position : Vector3.zero;
        appliedDistance = 0f;
        groundY = float.NaN;
        overlapAfterResolution = true;

        if (playerBody == null || playerCollider == null || bossCollider == null)
        {
            return false;
        }

        Vector3 launchHorizontalDirection = Vector3.ProjectOnPlane(launchVelocity, Vector3.up);
        if (launchHorizontalDirection.sqrMagnitude <= 0.0001f)
        {
            launchHorizontalDirection = Vector3.ProjectOnPlane(playerCollider.bounds.center - bossCollider.bounds.center, Vector3.up);
        }

        if (launchHorizontalDirection.sqrMagnitude <= 0.0001f)
        {
            launchHorizontalDirection = Vector3.right;
        }

        launchHorizontalDirection.Normalize();

        float playerBottomOffset = ExternalLaunchSafetyUtility.ResolveBottomOffset(playerBody, playerCollider);
        float playerHorizontalExtent = ResolveHorizontalBoundsExtent(playerCollider.bounds);
        float bossHorizontalExtent = ResolveHorizontalBoundsExtent(bossCollider.bounds);
        float requiredDistance = bossHorizontalExtent + playerHorizontalExtent + Mathf.Max(0f, bossLeapLaunchCollisionSeparationSkin);
        float maximumDistance = Mathf.Max(requiredDistance, bossLeapLaunchMaximumSafeRepositionDistance);
        float stepDistance = Mathf.Max(0.05f, playerHorizontalExtent * 0.35f);
        Vector3 bossCenter = bossCollider.bounds.center;

        for (float distance = requiredDistance; distance <= maximumDistance + 0.001f; distance += stepDistance)
        {
            Vector3 candidatePosition = bossCenter + launchHorizontalDirection * distance;
            if (TryResolveGroundedPlayerBodyY(candidatePosition, playerCollider, playerBody, playerBottomOffset, out float adjustedBodyY, out groundY))
            {
                candidatePosition.y = adjustedBodyY;
            }
            else
            {
                candidatePosition.y = playerBody.position.y;
            }

            if (!WouldCollidersOverlapAtBodyPosition(bossCollider, playerCollider, playerBody, candidatePosition))
            {
                safePosition = candidatePosition;
                appliedDistance = distance;
                overlapAfterResolution = false;
                return true;
            }
        }

        Vector3 fallbackPosition = bossCenter + launchHorizontalDirection * maximumDistance;
        if (TryResolveGroundedPlayerBodyY(fallbackPosition, playerCollider, playerBody, playerBottomOffset, out float fallbackBodyY, out groundY))
        {
            fallbackPosition.y = fallbackBodyY;
        }
        else
        {
            fallbackPosition.y = playerBody.position.y;
        }

        safePosition = fallbackPosition;
        appliedDistance = maximumDistance;
        overlapAfterResolution = WouldCollidersOverlapAtBodyPosition(bossCollider, playerCollider, playerBody, fallbackPosition);
        return !overlapAfterResolution;
    }

    private bool TryResolveGroundedPlayerBodyY(
        Vector3 candidateBodyPosition,
        Collider playerCollider,
        Rigidbody playerBody,
        float playerBottomOffset,
        out float adjustedBodyY,
        out float groundY)
    {
        adjustedBodyY = candidateBodyPosition.y;
        groundY = float.NaN;

        if (!TryResolveGroundBelowPosition(candidateBodyPosition, playerCollider, out RaycastHit groundHit))
        {
            return false;
        }

        groundY = groundHit.point.y;
        adjustedBodyY = groundY + playerBottomOffset + Mathf.Max(0f, bossLeapLaunchGroundClearance);
        return true;
    }

    private bool TryResolveGroundBelowPosition(Vector3 candidateBodyPosition, Collider playerCollider, out RaycastHit groundHit)
    {
        Vector3 origin = new Vector3(
            candidateBodyPosition.x,
            Mathf.Max(candidateBodyPosition.y, transform.position.y) + 6f,
            candidateBodyPosition.z);
        float rayDistance = 24f;
        return Physics.Raycast(
            origin,
            Vector3.down,
            out groundHit,
            rayDistance,
            bossLeapLaunchSafeGroundMask,
            QueryTriggerInteraction.Ignore);
    }

    private bool WouldCollidersOverlapAtBodyPosition(Collider bossCollider, Collider playerCollider, Rigidbody playerBody, Vector3 candidateBodyPosition)
    {
        Vector3 bodyDelta = candidateBodyPosition - playerBody.position;
        Vector3 candidateColliderPosition = playerCollider.transform.position + bodyDelta;
        return Physics.ComputePenetration(
            bossCollider,
            bossCollider.transform.position,
            bossCollider.transform.rotation,
            playerCollider,
            candidateColliderPosition,
            playerCollider.transform.rotation,
            out _,
            out _);
    }

    private void BeginBossPlayerCollisionIsolation(Collider bossCollider, Collider playerCollider, Rigidbody playerBody, int sequenceId)
    {
        if (bossCollider == null || playerCollider == null)
        {
            return;
        }

        int key = playerCollider.GetInstanceID();
        if (activeBossPlayerCollisionIsolations.TryGetValue(key, out BossPlayerCollisionIsolationState existingState))
        {
            RestoreBossPlayerCollisionIsolation(existingState, "Replaced");
        }

        Physics.IgnoreCollision(bossCollider, playerCollider, true);
        BossPlayerCollisionIsolationState state = new BossPlayerCollisionIsolationState
        {
            BossCollider = bossCollider,
            PlayerCollider = playerCollider,
            PlayerBody = playerBody,
            StartedAt = Time.time,
            SequenceId = sequenceId
        };
        state.RestoreRoutine = StartCoroutine(RestoreBossPlayerCollisionIsolationRoutine(state));
        activeBossPlayerCollisionIsolations[key] = state;

        bool playerGroundCollisionStillEnabled = true;
        bool playerColliderEnabled = playerCollider.enabled;
        bool playerColliderIsTrigger = playerCollider.isTrigger;
        bool playerDetectCollisions = playerBody == null || playerBody.detectCollisions;
        if (TryResolveGroundBelowPosition(playerBody != null ? playerBody.position : playerCollider.bounds.center, playerCollider, out RaycastHit groundHit))
        {
            playerGroundCollisionStillEnabled = !Physics.GetIgnoreCollision(playerCollider, groundHit.collider);
        }

        Debug.Log(
            "[BossPlayerCollisionIsolationTrace] event=IsolationStarted " +
            "bossCollider=" + bossCollider.name +
            " playerCollider=" + playerCollider.name +
            " playerGroundCollisionStillEnabled=" + playerGroundCollisionStillEnabled +
            " playerDetectCollisions=" + playerDetectCollisions +
            " playerColliderEnabled=" + playerColliderEnabled +
            " playerColliderIsTrigger=" + playerColliderIsTrigger +
            " sequenceId=" + sequenceId,
            this);
    }

    private System.Collections.IEnumerator RestoreBossPlayerCollisionIsolationRoutine(BossPlayerCollisionIsolationState state)
    {
        string restoreReason = "Timeout";
        float restoreDistance = Mathf.Max(0.05f, bossLeapLaunchBossCollisionRestoreDistance);
        float maxDuration = Mathf.Max(0.05f, bossLeapLaunchCollisionIgnoreMaximumDuration);

        while (Time.time - state.StartedAt < maxDuration)
        {
            if (state.BossCollider == null || state.PlayerCollider == null)
            {
                restoreReason = "ColliderMissing";
                break;
            }

            bool overlapping = Physics.ComputePenetration(
                state.BossCollider,
                state.BossCollider.transform.position,
                state.BossCollider.transform.rotation,
                state.PlayerCollider,
                state.PlayerCollider.transform.position,
                state.PlayerCollider.transform.rotation,
                out _,
                out _);

            Vector3 bossCenter = state.BossCollider.bounds.center;
            Vector3 playerCenter = state.PlayerCollider.bounds.center;
            bossCenter.y = 0f;
            playerCenter.y = 0f;
            float horizontalDistance = Vector3.Distance(bossCenter, playerCenter);

            if (!overlapping)
            {
                restoreReason = horizontalDistance >= restoreDistance ? "DistanceReached" : "NoLongerOverlapping";
                break;
            }

            yield return new WaitForFixedUpdate();
        }

        RestoreBossPlayerCollisionIsolation(state, restoreReason);
    }

    private void RestoreBossPlayerCollisionIsolation(BossPlayerCollisionIsolationState state, string reason)
    {
        if (state == null)
        {
            return;
        }

        if (state.RestoreRoutine != null)
        {
            StopCoroutine(state.RestoreRoutine);
            state.RestoreRoutine = null;
        }

        if (state.BossCollider != null && state.PlayerCollider != null)
        {
            Physics.IgnoreCollision(state.BossCollider, state.PlayerCollider, false);
        }

        if (state.PlayerCollider != null)
        {
            activeBossPlayerCollisionIsolations.Remove(state.PlayerCollider.GetInstanceID());
        }

        Debug.Log(
            "[BossPlayerCollisionIsolationTrace] event=IsolationRestored " +
            "bossCollider=" + (state.BossCollider != null ? state.BossCollider.name : "null") +
            " playerCollider=" + (state.PlayerCollider != null ? state.PlayerCollider.name : "null") +
            " reason=" + reason +
            " duration=" + (Time.time - state.StartedAt).ToString("F3"),
            this);
    }

    private void RestoreAllBossPlayerCollisionIsolations(string reason)
    {
        if (activeBossPlayerCollisionIsolations.Count == 0)
        {
            return;
        }

        BossPlayerCollisionIsolationState[] states = new BossPlayerCollisionIsolationState[activeBossPlayerCollisionIsolations.Count];
        activeBossPlayerCollisionIsolations.Values.CopyTo(states, 0);
        activeBossPlayerCollisionIsolations.Clear();
        for (int i = 0; i < states.Length; i++)
        {
            RestoreBossPlayerCollisionIsolation(states[i], reason);
        }
    }

    private static string BuildHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "<null>";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(target.name);
        Transform current = target.parent;
        while (current != null)
        {
            builder.Insert(0, current.name + "/");
            current = current.parent;
        }

        return builder.ToString();
    }

    private static IExternalLaunchReceiver ResolveExternalLaunchReceiver(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = target.GetComponentsInParent<MonoBehaviour>(true);
        IExternalLaunchReceiver fallbackReceiver = null;
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || !behaviour.enabled || behaviour is not IExternalLaunchReceiver receiver)
            {
                continue;
            }

            if (behaviour is Player2PrototypeController)
            {
                return receiver;
            }

            fallbackReceiver ??= receiver;
        }

        return fallbackReceiver;
    }

    private Vector3 ResolveBossLaunchSeparationOffset(Vector3 separationOffset)
    {
        Vector3 horizontalOffset = Vector3.ProjectOnPlane(separationOffset, Vector3.up);
        float maxDistance = Mathf.Max(0f, bossLeapSlamMaximumLaunchSeparation);
        if (maxDistance <= 0f)
        {
            return Vector3.zero;
        }

        return Vector3.ClampMagnitude(horizontalOffset, maxDistance);
    }

    private bool CanDispatchBossLaunch(BossLandingImpactSource source, int sequenceId, out string reason)
    {
        reason = string.Empty;

        if (source == BossLandingImpactSource.LeapSlam)
        {
            if (!IsBossActionActive(BossAttackKind.LeapSlam, sequenceId))
            {
                reason = "SourceKindMismatch";
                return false;
            }

            return true;
        }

        if (bossActionLocked && activeBossAttackKind == BossAttackKind.Devour)
        {
            reason = "DevourActionActive";
            return false;
        }

        return true;
    }

    private bool TryRegisterBossLaunchTarget(int sequenceId, CombatHealth targetHealth, out string reason)
    {
        reason = string.Empty;
        if (targetHealth == null)
        {
            reason = "NullTarget";
            return false;
        }

        if (sequenceId <= 0)
        {
            return true;
        }

        if (bossLaunchedTargetSequenceId != sequenceId)
        {
            bossLaunchedTargetIdsForImpactSequence.Clear();
            bossLaunchedTargetSequenceId = sequenceId;
        }

        int targetId = targetHealth.GetInstanceID();
        if (!bossLaunchedTargetIdsForImpactSequence.Add(targetId))
        {
            reason = "AlreadyLaunchedTargetThisSequence";
            return false;
        }

        return true;
    }

    private void HideBossBodyForSplit()
    {
        if (bossBodyHiddenForSplit)
        {
            return;
        }

        bossSplitRenderers = GetComponentsInChildren<Renderer>(true);
        bossSplitColliders = GetComponentsInChildren<Collider>(true);
        bossSplitRendererEnabledStates = new bool[bossSplitRenderers.Length];
        bossSplitColliderEnabledStates = new bool[bossSplitColliders.Length];

        for (int i = 0; i < bossSplitRenderers.Length; i++)
        {
            Renderer renderer = bossSplitRenderers[i];
            bossSplitRendererEnabledStates[i] = renderer != null && renderer.enabled;
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        for (int i = 0; i < bossSplitColliders.Length; i++)
        {
            Collider collider = bossSplitColliders[i];
            bossSplitColliderEnabledStates[i] = collider != null && collider.enabled;
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        if (rb != null)
        {
            bossRigidbodyWasKinematicBeforeSplit = rb.isKinematic;
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        bossBodyHiddenForSplit = true;
    }

    private void RestoreBossBodyAfterSplit()
    {
        if (!bossBodyHiddenForSplit)
        {
            return;
        }

        if (bossSplitRenderers != null)
        {
            for (int i = 0; i < bossSplitRenderers.Length; i++)
            {
                Renderer renderer = bossSplitRenderers[i];
                if (renderer != null)
                {
                    bool wasEnabled = bossSplitRendererEnabledStates != null && i < bossSplitRendererEnabledStates.Length && bossSplitRendererEnabledStates[i];
                    renderer.enabled = wasEnabled;
                }
            }
        }

        if (bossSplitColliders != null)
        {
            for (int i = 0; i < bossSplitColliders.Length; i++)
            {
                Collider collider = bossSplitColliders[i];
                if (collider != null)
                {
                    bool wasEnabled = bossSplitColliderEnabledStates != null && i < bossSplitColliderEnabledStates.Length && bossSplitColliderEnabledStates[i];
                    collider.enabled = wasEnabled;
                }
            }
        }

        if (rb != null)
        {
            rb.isKinematic = bossRigidbodyWasKinematicBeforeSplit;
            rb.linearVelocity = Vector3.zero;
        }

        bossSplitRenderers = null;
        bossSplitColliders = null;
        bossSplitRendererEnabledStates = null;
        bossSplitColliderEnabledStates = null;
        bossBodyHiddenForSplit = false;
    }

    private void SpawnBossSplitChildren()
    {
        activeBossSplitChildren.Clear();
        EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
        if (spawner == null)
        {
            Debug.LogWarning($"[BossTimedSplit] No EnemySpawner found. Boss '{name}' could not split.", this);
            return;
        }

        List<GameObject> children = spawner.SpawnSplitChildrenAndCollect(
            gameObject,
            Mathf.Max(0, bossSplitChildCount),
            Mathf.Max(0f, bossSplitScatterRadius),
            MonsterRank.Elite,
            1f,
            1f,
            1f,
            1f,
            1f,
            false,
            false,
            debugLog || debugAttackDiagnostics,
            "SkillTimed");

        if (children == null || children.Count == 0)
        {
            return;
        }

        activeBossSplitChildren.AddRange(children);
        ApplyBossRelativeHealthToSplitChildren(children);
    }

    private void ApplyBossRelativeHealthToSplitChildren(List<GameObject> children)
    {
        float childMaxHealth = Mathf.Max(1f, ResolveBossMaxHealthForSplit() * Mathf.Clamp01(bossSplitChildHealthPercentOfBoss));
        for (int i = 0; i < children.Count; i++)
        {
            GameObject child = children[i];
            if (child == null)
            {
                continue;
            }

            CombatStats stats = child.GetComponent<CombatStats>();
            BattleResourceBank bank = child.GetComponent<BattleResourceBank>();
            CombatHealth health = child.GetComponent<CombatHealth>();

            if (stats != null)
            {
                stats.maxHealth = childMaxHealth;
            }

            if (bank != null)
            {
                bank.maxHealth = childMaxHealth;
                bank.currentHealth = childMaxHealth;
            }

            if (health != null)
            {
                health.stats = stats;
                health.resourceBank = bank;
                health.currentHealth = childMaxHealth;
            }
        }
    }

    private float ResolveBossMaxHealthForSplit()
    {
        CombatHealth health = GetComponent<CombatHealth>();
        if (health != null)
        {
            return Mathf.Max(1f, health.MaxHealthValue);
        }

        CombatStats statsSource = combatStats != null ? combatStats : GetComponent<CombatStats>();
        if (statsSource != null)
        {
            return Mathf.Max(1f, statsSource.maxHealth);
        }

        return Mathf.Max(1f, attackDamage * 10f);
    }

    private int CountAliveBossSplitChildren()
    {
        int alive = 0;
        for (int i = activeBossSplitChildren.Count - 1; i >= 0; i--)
        {
            GameObject child = activeBossSplitChildren[i];
            if (child == null)
            {
                activeBossSplitChildren.RemoveAt(i);
                continue;
            }

            CombatHealth childHealth = child.GetComponent<CombatHealth>();
            if (childHealth != null && childHealth.IsDead)
            {
                continue;
            }

            alive++;
        }

        return alive;
    }

    private Vector3 ResolveBossSplitMergePosition()
    {
        Vector3 sum = Vector3.zero;
        int alive = 0;
        for (int i = 0; i < activeBossSplitChildren.Count; i++)
        {
            GameObject child = activeBossSplitChildren[i];
            if (child == null)
            {
                continue;
            }

            CombatHealth childHealth = child.GetComponent<CombatHealth>();
            if (childHealth != null && childHealth.IsDead)
            {
                continue;
            }

            sum += child.transform.position;
            alive++;
        }

        if (alive <= 0)
        {
            return transform.position;
        }

        Vector3 position = sum / alive;
        position.y = transform.position.y;
        return position;
    }

    private void DestroyActiveBossSplitChildren()
    {
        for (int i = 0; i < activeBossSplitChildren.Count; i++)
        {
            GameObject child = activeBossSplitChildren[i];
            if (child != null)
            {
                Destroy(child);
            }
        }

        activeBossSplitChildren.Clear();
    }

    private void ChaseTarget(Vector3 toPlayer, string currentState, string targetName, string reason)
    {
        if (rb == null)
        {
            return;
        }

        float horizontalCenterDistance = new Vector2(toPlayer.x, toPlayer.z).magnitude;
        if (horizontalCenterDistance <= MovementZeroEpsilon)
        {
            rb.linearVelocity = Vector3.zero;
            StopMoveAnimation();
            return;
        }

        Vector3 direction = toPlayer / horizontalCenterDistance;
        if (enableEnemySoftAvoidance)
        {
            Vector3 separationDirection = ResolveEnemySeparationDirection();
            Vector3 combinedDirection = direction + separationDirection * Mathf.Max(0f, enemySeparationWeight);
            combinedDirection.y = 0f;
            if (combinedDirection.sqrMagnitude > MovementZeroEpsilon * MovementZeroEpsilon)
            {
                direction = combinedDirection.normalized;
            }
        }

        float baseMoveSpeed = moveSpeed;
        float externalMoveMultiplier = ResolveExternalMoveMultiplier();
        float rawMoveSpeed = BattleStatUtility.ResolveMoveSpeed(combatStats, baseMoveSpeed, externalMoveMultiplier);
        float currentMoveSpeed = BattleStatUtility.ClampActualMoveSpeed(rawMoveSpeed, out _);
        if (maxHorizontalMoveSpeed > 0f)
        {
            currentMoveSpeed = Mathf.Min(currentMoveSpeed, maxHorizontalMoveSpeed);
        }

        float verticalVelocity = rb.linearVelocity.y;
        if (maxVerticalVelocity > 0f)
        {
            verticalVelocity = Mathf.Clamp(verticalVelocity, -maxVerticalVelocity, maxVerticalVelocity);
        }

        rb.linearVelocity = new Vector3(direction.x * currentMoveSpeed, verticalVelocity, direction.z * currentMoveSpeed);
        PlayMoveAnimation(direction, currentMoveSpeed);
        LogChaseDiagnostics(horizontalCenterDistance, currentMoveSpeed > MovementZeroEpsilon, false, false, false, externalMoveMultiplier <= 0f, currentState, currentMoveSpeed, targetName, reason);

        if (faceMoveDirection)
        {
            transform.forward = direction;
        }
        else if (keepFlatRotation)
        {
            transform.rotation = initialRotation;
        }
    }

    private string EvaluateBossMeleeFailReason(float distance, float verticalDifference, bool grounded)
    {
        if (playerTarget == null)
        {
            return "NoTarget";
        }

        if (attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking))
        {
            return "AlreadyAttacking";
        }

        if (Time.time < nextAttackTime)
        {
            return "Cooldown";
        }

        if (distance > Mathf.Max(0.1f, bossMeleeAttackRange))
        {
            return "OutOfRange";
        }

        if (verticalDifference > Mathf.Max(0f, maxVerticalAttackDifference))
        {
            return "OutOfRange";
        }

        if (requireGroundedToAttack && !grounded)
        {
            return "NotGrounded";
        }

        return string.Empty;
    }

    private string EvaluateBossRangedFailReason(float distance, float verticalDifference, bool grounded)
    {
        if (playerTarget == null)
        {
            return "NoTarget";
        }

        if (attackInProgress)
        {
            return "AlreadyAttacking";
        }

        if (Time.time < nextBossRangedAttackTime)
        {
            return "Cooldown";
        }

        if (distance < Mathf.Max(0.1f, bossRangedMinRange) || distance > Mathf.Max(bossRangedMinRange, bossRangedMaxRange))
        {
            return "OutOfRange";
        }

        if (verticalDifference > Mathf.Max(0f, maxVerticalTargetDifference))
        {
            return "OutOfRange";
        }

        if (requireGroundedToAttack && !grounded)
        {
            return "NotGrounded";
        }

        return string.Empty;
    }

    private bool BeginBossRangedAttack(Transform target)
    {
        if (target == null || bossRangedAttackRoutine != null)
        {
            return false;
        }

        if (!TryLockBossAction(BossAttackKind.Ranged, out int sequenceId))
        {
            return false;
        }

        pendingAttackTarget = target;
        lastAttackTime = Time.time;
        nextBossRangedAttackTime = Time.time + Mathf.Max(0.1f, bossRangedAttackCooldown);
        rb.linearVelocity = Vector3.zero;
        StopMoveAnimation();
        CancelInvoke(nameof(FinishAttackRecovery));
        bossRangedAttackRoutine = StartCoroutine(BossRangedAttackRoutine(target, sequenceId));
        activeBossAttackRoutine = bossRangedAttackRoutine;
        return true;
    }

    private System.Collections.IEnumerator BossRangedAttackRoutine(Transform target, int sequenceId)
    {
        float configuredTotal = Mathf.Max(0.05f, bossRangedCastTime);
        float windupDuration = Mathf.Max(0.01f, bossRangedCastWindupTime);
        float releaseDuration = Mathf.Max(0.01f, bossRangedCastReleaseTime);
        float recoverDuration = Mathf.Max(0.01f, bossRangedVisualRecoverTime);
        float durationSum = windupDuration + releaseDuration + recoverDuration;
        if (durationSum > configuredTotal && configuredTotal > 0.01f)
        {
            float scale = configuredTotal / durationSum;
            windupDuration *= scale;
            releaseDuration *= scale;
            recoverDuration *= scale;
        }

        Vector3 baseScale = slimeAnimation != null ? slimeAnimation.BaseVisualLocalScale : Vector3.one;
        Vector3 basePosition = slimeAnimation != null ? slimeAnimation.BaseVisualLocalPosition : Vector3.zero;
        Transform visual = slimeAnimation != null ? slimeAnimation.VisualRoot : null;
        Quaternion rootInitialRotation = transform.rotation;
        Quaternion visualInitialLocalRotation = visual != null ? visual.localRotation : Quaternion.identity;

        Vector3 targetPoint = ResolveBossProjectileTargetPoint(target);
        Vector3 spawnPosition = ResolveProjectileSpawnPosition(target);
        int facingSign = ResolveBossFacingSign(target);
        string targetSide = facingSign >= 0 ? "Right" : "Left";
        int shotCount = ResolveBossSlimeProjectileCount();
        if (debugAttackDiagnostics || debugLog)
        {
            Debug.Log($"[BossRangedCast] start cast target position={targetPoint} cast time={configuredTotal:F2} projectile prefab={(ResolveProjectilePrefab() != null ? ResolveProjectilePrefab().name : "runtime sphere")} spawn position={spawnPosition}", this);
            Debug.Log($"[BossRangedCastAnim] start windup visualRoot={(visual != null ? visual.name : "null")} spawnPosition={spawnPosition} targetPosition={targetPoint}", this);
            Debug.Log($"[BossRangedCastAnim] facingSign={facingSign} targetSide={targetSide} rootRotationPreserved=true visualRotationPreserved=true usedFlipOnly=true", this);
        }

        Vector3 windupOffset = new Vector3(-facingSign * bossRangedVisualLeanDistance, 0f, 0f);
        Vector3 releaseOffset = new Vector3(facingSign * bossRangedVisualLeanDistance, 0f, 0f);

        for (float elapsed = 0f; elapsed < windupDuration; elapsed += Time.deltaTime)
        {
            if (!IsBossActionActive(BossAttackKind.Ranged, sequenceId))
            {
                yield break;
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }

            FaceTargetHorizontally(target);
            transform.rotation = rootInitialRotation;

            if (enableBossRangedCastAnimation && visual != null)
            {
                float t = Mathf.Clamp01(elapsed / windupDuration);
                visual.localScale = Vector3.Lerp(baseScale, new Vector3(baseScale.x * bossRangedWindupSquashX, baseScale.y * bossRangedWindupStretchY, baseScale.z), t);
                visual.localPosition = Vector3.Lerp(basePosition, basePosition + windupOffset, t);
                visual.localRotation = visualInitialLocalRotation;
            }

            yield return null;
        }

        if (enableBossRangedCastAnimation && visual != null)
        {
            visual.localScale = new Vector3(baseScale.x * bossRangedWindupSquashX, baseScale.y * bossRangedWindupStretchY, baseScale.z);
            visual.localPosition = basePosition + windupOffset;
            visual.localRotation = visualInitialLocalRotation;
        }

        if (debugAttackDiagnostics || debugLog)
        {
            Debug.Log($"[BossRangedCastAnim] release projectile visualRoot={(visual != null ? visual.name : "null")} spawnPosition={spawnPosition} targetPosition={targetPoint}", this);
        }

        if (enableBossRangedCastAnimation && visual != null)
        {
            for (float elapsed = 0f; elapsed < releaseDuration; elapsed += Time.deltaTime)
            {
                if (!IsBossActionActive(BossAttackKind.Ranged, sequenceId))
                {
                    yield break;
                }

                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                }

                FaceTargetHorizontally(target);
                transform.rotation = rootInitialRotation;
                float t = Mathf.Clamp01(elapsed / releaseDuration);
                visual.localScale = Vector3.Lerp(
                    new Vector3(baseScale.x * bossRangedWindupSquashX, baseScale.y * bossRangedWindupStretchY, baseScale.z),
                    new Vector3(baseScale.x * bossRangedReleaseStretchX, baseScale.y * bossRangedReleaseSquashY, baseScale.z),
                    t);
                visual.localPosition = Vector3.Lerp(basePosition + windupOffset, basePosition + releaseOffset, t);
                visual.localRotation = visualInitialLocalRotation;
                yield return null;
            }
        }

        LogBossSlimeProjectileVolleyStarted(shotCount, target);
        for (int i = 0; i < shotCount; i++)
        {
            if (!IsBossActionActive(BossAttackKind.Ranged, sequenceId))
            {
                yield break;
            }

            PlayBossRangedMuzzleParticle();
            ExecuteProjectileAttack(target, i, shotCount);

            if (i < shotCount - 1)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, slimeProjectileInterval));
            }
        }

        if (enableBossRangedCastAnimation && visual != null)
        {
            if (debugAttackDiagnostics || debugLog)
            {
                Debug.Log($"[BossRangedCastAnim] recover visualRoot={(visual != null ? visual.name : "null")} spawnPosition={spawnPosition} targetPosition={targetPoint}", this);
            }

            for (float elapsed = 0f; elapsed < recoverDuration; elapsed += Time.deltaTime)
            {
                if (!IsBossActionActive(BossAttackKind.Ranged, sequenceId))
                {
                    yield break;
                }

                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                }

                FaceTargetHorizontally(target);
                transform.rotation = rootInitialRotation;
                float t = Mathf.Clamp01(elapsed / recoverDuration);
                visual.localScale = Vector3.Lerp(
                    new Vector3(baseScale.x * bossRangedReleaseStretchX, baseScale.y * bossRangedReleaseSquashY, baseScale.z),
                    baseScale,
                    t);
                visual.localPosition = Vector3.Lerp(basePosition + releaseOffset, basePosition, t);
                visual.localRotation = visualInitialLocalRotation;
                yield return null;
            }
        }

        transform.rotation = rootInitialRotation;
        if (visual != null)
        {
            visual.localScale = baseScale;
            visual.localPosition = basePosition;
            visual.localRotation = visualInitialLocalRotation;
        }

        CompleteBossRangedAttack(sequenceId);
        yield break;
    }

    private void CompleteBossRangedAttack(int sequenceId)
    {
        bossRangedAttackRoutine = null;
        activeBossAttackRoutine = null;
        ReleaseBossActionLock("Completed", sequenceId);
    }

    private void FaceTargetHorizontally(Transform target)
    {
        if (target == null)
        {
            return;
        }

        bool facingRight = target.position.x >= transform.position.x;
        if (slimeAnimation != null)
        {
            slimeAnimation.SetFacingRight(facingRight, "BossRangedCast");
            return;
        }

        ResolveMeleeHitSources();
        if (meleeEnemySpriteRenderer != null)
        {
            meleeEnemySpriteRenderer.flipX = facingRight;
        }
    }

    private void LogBossRangedDecision(float distance, string selectedAttack, string reason)
    {
        if (!(debugAttackDiagnostics || debugLog))
        {
            return;
        }

        if (Time.time < nextBossRangedDecisionLogTime)
        {
            return;
        }

        nextBossRangedDecisionLogTime = Time.time + 1f;

        Debug.Log($"[BossRangedDecision] distance={distance:F2} meleeRange={bossMeleeAttackRange:F2} rangedMinRange={bossRangedMinRange:F2} rangedMaxRange={bossRangedMaxRange:F2} selectedAttack={selectedAttack} reason={reason}", this);
    }

    private void LogBossMeleeDecision(
        float distanceToTarget,
        bool canAttack,
        string failReason,
        bool isGrounded,
        string selectedAttack,
        string targetName,
        float meleeCooldownRemaining,
        bool isAttacking,
        bool isStunned,
        bool isDead)
    {
        if (!(debugAttackDiagnostics || debugBossMeleeHit || debugLog))
        {
            return;
        }

        if (Time.time < nextBossMeleeDecisionLogTime)
        {
            return;
        }

        nextBossMeleeDecisionLogTime = Time.time + 1f;
        Debug.Log(
            "[BossMeleeDecision] " +
            "enemy=" + name +
            " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
            " species=" + (monsterIdentity != null ? monsterIdentity.species.ToString() : "Unknown") +
            " attackStyle=" + attackStyle +
            " distanceToTarget=" + distanceToTarget.ToString("F2") +
            " bossMeleeAttackRange=" + bossMeleeAttackRange.ToString("F2") +
            " canAttack=" + canAttack +
            " failReason=" + failReason +
            " isGrounded=" + isGrounded +
            " targetAssigned=" + (playerTarget != null) +
            " targetName=" + targetName +
            " selectedAttack=" + selectedAttack +
            " meleeCooldownRemaining=" + meleeCooldownRemaining.ToString("F2") +
            " isAttacking=" + isAttacking +
            " isStunned=" + isStunned +
            " isDead=" + isDead,
            this);
    }

    private void LogBossMeleeDiag(
        float horizontalCenterDistance,
        float horizontalEdgeDistance,
        float verticalDistance,
        bool isGrounded,
        bool targetInMeleeRange,
        bool canAttack,
        bool meleeCooldownReady,
        bool rangedCooldownReady,
        string currentAttackState,
        Collider physicalBodyCollider,
        Collider combatSurfaceCollider,
        string blockReason)
    {
        if (!(debugAttackDiagnostics || debugBossMeleeHit || debugLog))
        {
            return;
        }

        if (Time.time < nextBossMeleeDecisionLogTime)
        {
            return;
        }

        Debug.Log(
            "[BossMeleeDiag] " +
            "enemy=" + name +
            " target exists=" + (playerTarget != null) +
            " horizontal distance=" + horizontalCenterDistance.ToString("F2") +
            " edge distance=" + horizontalEdgeDistance.ToString("F2") +
            " vertical distance=" + verticalDistance.ToString("F2") +
            " targetInMeleeRange=" + targetInMeleeRange +
            " attackHitRange=" + Mathf.Max(0.1f, bossMeleeHitRadius).ToString("F2") +
            " stopDistance=" + stopDistance.ToString("F2") +
            " isGrounded=" + isGrounded +
            " canMove=" + (rb != null && !attackInProgress) +
            " canAttack=" + canAttack +
            " meleeCooldownReady=" + meleeCooldownReady +
            " rangedCooldownReady=" + rangedCooldownReady +
            " current attack state=" + currentAttackState +
            " physicalBodyCollider=" + (physicalBodyCollider != null ? physicalBodyCollider.name : "null") +
            " combatSurfaceCollider=" + (combatSurfaceCollider != null ? combatSurfaceCollider.name : "null") +
            " blockReason=" + (string.IsNullOrEmpty(blockReason) ? "None" : blockReason),
            this);
    }

    private void LogBossCombatDecisionTrace(float distance, bool grounded, string selectedAction, string failReason)
    {
        if (!(debugAttackDiagnostics || debugBossMeleeHit || debugLog))
        {
            return;
        }

        if (Time.time < nextBossCombatDecisionTraceTime)
        {
            return;
        }

        nextBossCombatDecisionTraceTime = Time.time + 0.5f;
        CombatHealth targetHealth = playerTarget != null ? playerTarget.GetComponentInParent<CombatHealth>() : null;
        Debug.Log(
            "[BossCombatDecisionTrace] " +
            "target=" + (playerTarget != null ? playerTarget.name : "null") +
            " targetAlive=" + (targetHealth != null && !targetHealth.IsDead) +
            " distance=" + distance.ToString("F2") +
            " inMeleeRange=" + (distance <= Mathf.Max(0.1f, bossMeleeAttackRange)) +
            " inRangedRange=" + (distance >= Mathf.Max(0.1f, bossRangedMinRange) && distance <= Mathf.Max(bossRangedMinRange, bossRangedMaxRange)) +
            " grounded=" + grounded +
            " attackInProgress=" + attackInProgress +
            " activeBossAttackKind=" + activeBossAttackKind +
            " recoveryRemaining=" + Mathf.Max(0f, nextAttackTime - Time.time).ToString("F2") +
            " globalCooldownRemaining=" + Mathf.Max(0f, nextAttackTime - Time.time).ToString("F2") +
            " basicAttackCooldownRemaining=" + Mathf.Max(0f, nextAttackTime - Time.time).ToString("F2") +
            " devourCooldownRemaining=" + Mathf.Max(0f, nextBossDevourAttackTime - Time.time).ToString("F2") +
            " leapCooldownRemaining=" + Mathf.Max(0f, nextBossLeapAttackTime - Time.time).ToString("F2") +
            " splitCooldownRemaining=" + Mathf.Max(0f, nextBossSplitAttackTime - Time.time).ToString("F2") +
            " selectedAction=" + selectedAction +
            " failReason=" + (string.IsNullOrEmpty(failReason) ? "None" : failReason),
            this);
    }

    private bool TryStartBossBasicAttack(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        if (!TryLockBossAction(BossAttackKind.Melee, out _))
        {
            return false;
        }

        BeginAttack();
        return true;
    }

    private bool TryLockBossAction(BossAttackKind requestedKind, out int sequenceId)
    {
        sequenceId = 0;
        string denyReason = EvaluateBossActionLockDenyReason(requestedKind);
        bool allowed = string.IsNullOrEmpty(denyReason);
        LogBossActionLockTrace("ActionStartRequested", requestedKind, allowed, allowed ? "None" : denyReason, activeBossActionSequenceId);

        if (!allowed)
        {
            LogBossActionLockTrace("ConcurrentActionBlocked", requestedKind, false, denyReason, activeBossActionSequenceId);
            return false;
        }

        bossAttackSequenceId++;
        activeBossActionSequenceId = bossAttackSequenceId;
        bossActionLocked = true;
        attackInProgress = true;
        activeBossAttackKind = requestedKind;
        sequenceId = activeBossActionSequenceId;
        LogBossActionLockTrace("ActionLockAcquired", requestedKind, true, "None", sequenceId);
        return true;
    }

    private string EvaluateBossActionLockDenyReason(BossAttackKind requestedKind)
    {
        CombatHealth health = GetComponent<CombatHealth>();
        if (health != null && health.IsDead)
        {
            return "Dead";
        }

        if (bossBodyHiddenForSplit)
        {
            return "HiddenForSplit";
        }

        if (bossActionLocked)
        {
            return "AnotherActionActive";
        }

        if (attackInProgress)
        {
            return "AttackInProgress";
        }

        if (activeBossAttackKind != BossAttackKind.None)
        {
            return "ActiveKindNotNone:" + activeBossAttackKind;
        }

        if (activeBossAttackRoutine != null || bossSpecialAttackRoutine != null || bossRangedAttackRoutine != null || bossAttackRecoveryRoutine != null)
        {
            return "CoroutineActive";
        }

        return string.Empty;
    }

    private bool IsBossActionActive(BossAttackKind expectedKind, int sequenceId)
    {
        return attackStyle == MonsterAttackStyle.ElementalBoss &&
               bossActionLocked &&
               attackInProgress &&
               activeBossAttackKind == expectedKind &&
               activeBossActionSequenceId == sequenceId;
    }

    public bool IsBossDevourActionActive(int sequenceId)
    {
        return IsBossActionActive(BossAttackKind.Devour, sequenceId);
    }

    public Vector3 ResolveBossDevourHoldTargetPosition(Transform holdAnchor, Vector3 holdOffset, Transform heldTarget = null)
    {
        Vector3 anchorPosition;
        if (bossDevourBodyOverrideActive)
        {
            anchorPosition = bossDevourGroundAnchorPosition;
        }
        else if (holdAnchor != null)
        {
            anchorPosition = holdAnchor.position;
        }
        else
        {
            anchorPosition = transform.position;
        }

        Vector3 holdTarget = anchorPosition + holdOffset;
        float maximumLiftY = bossDevourGroundedRootY + Mathf.Max(0f, bossDevourMaximumPlayerLift);
        if (bossDevourIgnoreVerticalPlayerFollow)
        {
            holdTarget.y = Mathf.Min(holdTarget.y, maximumLiftY);
        }

        Transform targetTransform = heldTarget != null ? heldTarget : playerTarget;
        Collider playerCollider = ResolvePlayerCollider(targetTransform);
        if (playerCollider != null && TryResolveBossDevourGroundY(anchorPosition, out float groundY))
        {
            float targetRootY = targetTransform != null ? targetTransform.position.y : playerCollider.bounds.center.y;
            float playerBottomOffset = targetRootY - playerCollider.bounds.min.y;
            float minimumSafePlayerRootY = groundY + playerBottomOffset + Mathf.Max(0f, bossDevourGroundClearance);
            if (minimumSafePlayerRootY > maximumLiftY + 0.001f)
            {
                Debug.LogWarning(
                    "[BossDevourConfigTrace] " +
                    "event=InvalidHoldHeightConfig " +
                    "minimumSafeY=" + minimumSafePlayerRootY.ToString("F3") +
                    " maximumAllowedY=" + maximumLiftY.ToString("F3") +
                    " holdOffset=" + holdOffset +
                    " target=" + (targetTransform != null ? targetTransform.name : "null"),
                    this);
            }

            holdTarget.y = Mathf.Max(holdTarget.y, minimumSafePlayerRootY);
        }

        return holdTarget;
    }

    private bool TryResolveBossDevourGroundY(Vector3 anchorPosition, out float groundY)
    {
        groundY = bossDevourGroundedRootY;

        if (lastGroundProbeHit && !float.IsNaN(lastGroundHitY) && !float.IsInfinity(lastGroundHitY))
        {
            groundY = lastGroundHitY;
            return true;
        }

        Vector3 rayOrigin = anchorPosition + Vector3.up * 2f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit rayHit, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform bossRoot = transform.root;
            Transform playerRoot = playerTarget != null ? playerTarget.root : null;
            Transform hitRoot = rayHit.collider != null ? rayHit.collider.transform.root : null;
            if (hitRoot != bossRoot && hitRoot != playerRoot)
            {
                groundY = rayHit.point.y;
                return true;
            }
        }
        return bossDevourBodyOverrideActive;
    }

    private void ReleaseBossActionLock(string endReason, int sequenceId)
    {
        if (sequenceId <= 0 || sequenceId != activeBossActionSequenceId)
        {
            return;
        }

        LogBossActionLockTrace("ActionLockReleased", activeBossAttackKind, true, endReason, sequenceId);
        CancelInvoke(nameof(FinishAttackRecovery));

        if (bossAttackRecoveryRoutine != null)
        {
            StopCoroutine(bossAttackRecoveryRoutine);
            bossAttackRecoveryRoutine = null;
        }

        StopBossDevourAttractionImmediately();
        RestoreBossDevourBodyOverride("ReleaseBossActionLock:" + endReason);
        bool forceStopRoutine = endReason != "Completed";
        if (forceStopRoutine && activeBossAttackRoutine != null)
        {
            StopCoroutine(activeBossAttackRoutine);
        }

        if (forceStopRoutine && bossRangedAttackRoutine != null)
        {
            StopCoroutine(bossRangedAttackRoutine);
        }

        if (forceStopRoutine && bossSpecialAttackRoutine != null)
        {
            StopCoroutine(bossSpecialAttackRoutine);
        }

        bossActionLocked = false;
        attackInProgress = false;
        pendingAttackTarget = null;
        attackHitFrameTriggeredThisAttack = false;
        activeBossAttackRoutine = null;
        bossRangedAttackRoutine = null;
        bossSpecialAttackRoutine = null;
        activeBossAttackKind = BossAttackKind.None;
        activeBossDevourRuntimeConfig = default;
    }

    private void StopBossDevourAttractionImmediately()
    {
        Transform[] candidates = new Transform[]
        {
            pendingAttackTarget,
            playerTarget
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            Transform candidate = candidates[i];
            if (candidate == null)
            {
                continue;
            }

            BossSlimeDevourStatus status = candidate.GetComponentInParent<BossSlimeDevourStatus>();
            if (status != null)
            {
                status.ForceStop("BossActionReleased");
            }
        }
    }

    private void LogBossActionLockTrace(string eventName, BossAttackKind requestedKind, bool allowed, string denyReason, int sequenceId)
    {
        if (!(debugAttackDiagnostics || debugBossMeleeHit || debugLog))
        {
            return;
        }

        Debug.Log(
            "[BossActionLockTrace] " +
            "event=" + eventName +
            " requestedKind=" + requestedKind +
            " activeKind=" + activeBossAttackKind +
            " locked=" + bossActionLocked +
            " attackInProgress=" + attackInProgress +
            " activeSequenceId=" + activeBossActionSequenceId +
            " sequenceId=" + sequenceId +
            " allowed=" + allowed +
            " denyReason=" + (string.IsNullOrEmpty(denyReason) ? "None" : denyReason),
            this);
    }

    private Vector3 ResolveEnemySeparationDirection()
    {
        float separationRadius = Mathf.Max(0f, enemySeparationRadius);
        if (!enableEnemySoftAvoidance || separationRadius <= 0.01f || enemySeparationMaxNeighbors <= 0)
        {
            return Vector3.zero;
        }

        if (separationHits == null || separationHits.Length != Mathf.Max(1, enemySeparationMaxNeighbors))
        {
            separationHits = new Collider[Mathf.Max(1, enemySeparationMaxNeighbors)];
        }

        int enemyLayer = gameObject.layer;
        int layerMask = enemyLayer >= 0 ? (1 << enemyLayer) : ~0;
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            separationRadius,
            separationHits,
            layerMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount <= 0)
        {
            return Vector3.zero;
        }

        Vector3 separation = Vector3.zero;
        int neighborCount = 0;
        Vector3 selfPosition = transform.position;
        selfPosition.y = 0f;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = separationHits[i];
            if (hit == null)
            {
                continue;
            }

            Transform root = hit.transform.root;
            if (root == null || root == transform.root)
            {
                continue;
            }

            Vector3 otherPosition = root.position;
            otherPosition.y = 0f;
            Vector3 away = selfPosition - otherPosition;
            float distance = away.magnitude;
            if (distance <= 0.001f || distance > separationRadius)
            {
                continue;
            }

            float weight = 1f - Mathf.Clamp01(distance / separationRadius);
            separation += away.normalized * weight;
            neighborCount++;
        }

        for (int i = 0; i < hitCount; i++)
        {
            separationHits[i] = null;
        }

        if (neighborCount <= 0 || separation.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        separation /= neighborCount;
        separation.y = 0f;
        return separation.normalized;
    }

    private void BeginAttack()
    {
        int bossSequenceId = attackStyle == MonsterAttackStyle.ElementalBoss ? activeBossActionSequenceId : 0;
        if (attackStyle == MonsterAttackStyle.ElementalBoss && activeBossAttackKind == BossAttackKind.None)
        {
            activeBossAttackKind = BossAttackKind.Melee;
        }

        lastAttackTime = Time.time;
        nextAttackTime = Time.time + ResolveCurrentAttackCooldown();
        pendingAttackTarget = playerTarget;
        attackInProgress = true;
        attackHitFrameTriggeredThisAttack = false;
        lastMeleeAttackResult = "pending";
        CancelInvoke(nameof(FinishAttackRecovery));
        rb.linearVelocity = Vector3.zero;
        StopMoveAnimation();
        if (debugAttackDiagnostics)
        {
            Debug.Log($"[EnemyAttack] StartAttack enemy={name} target={(pendingAttackTarget != null ? pendingAttackTarget.name : "null")}", this);
        }

        if (attackStyle == MonsterAttackStyle.ElementalBoss && activeBossAttackKind == BossAttackKind.Melee)
        {
            Vector3 attackDirection;
            Vector3 attackOrigin = ResolveBossMeleeHitOrigin(pendingAttackTarget, out attackDirection);
            float attackWindup = slimeAnimation != null ? slimeAnimation.AttackWindup : 0f;
            float attackRecover = slimeAnimation != null ? slimeAnimation.AttackRecovery : AttackRecoveryDurationSeconds;
            Debug.Log(
                "[BossMeleeAttack] " +
                "enemy=" + name +
                " target=" + (pendingAttackTarget != null ? pendingAttackTarget.name : "null") +
                " start attack" +
                " attackWindup=" + attackWindup.ToString("F2") +
                " attackHitTime=" + attackWindup.ToString("F2") +
                " attackRecover=" + attackRecover.ToString("F2") +
                " attackDamage=" + ResolveCurrentAttackDamage(ResolvePrimaryDamageType()).ToString("F2") +
                " attackRadius=" + Mathf.Max(0.1f, bossMeleeHitRadius).ToString("F2") +
                " attackOrigin=" + attackOrigin +
                " attackDirection=" + attackDirection,
                this);
        }
        else if (!UsesProjectileAttack())
        {
            Vector3 attackDirection;
            Vector3 attackOrigin = ResolveGenericMeleeHitOrigin(pendingAttackTarget, out attackDirection);
            float attackWindup = slimeAnimation != null ? slimeAnimation.AttackWindup : 0f;
            float attackRecover = slimeAnimation != null ? slimeAnimation.AttackRecovery : AttackRecoveryDurationSeconds;
            Debug.Log(
                "[EnemyMeleeAttack] " +
                "enemy=" + name +
                " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
                " target=" + (pendingAttackTarget != null ? pendingAttackTarget.name : "null") +
                " start attack" +
                " attackWindup=" + attackWindup.ToString("F2") +
                " attackHitTime=" + attackWindup.ToString("F2") +
                " attackRecover=" + attackRecover.ToString("F2") +
                " attackDamage=" + ResolveCurrentAttackDamage(ResolvePrimaryDamageType()).ToString("F2") +
                " attackOrigin=" + attackOrigin +
                " attackRange=" + ResolveCurrentMeleeHitRadius().ToString("F2") +
                " attackDirection=" + attackDirection,
                this);
        }

        LogEliteAttackDiagFromCurrentState(
            failReasonOverride: "None",
            tryStartMeleeCalled: true,
            attackRoutineStarted: true,
            damageApplied: false);

        LogSlimeAttackLifecycle("BeginAttack", pendingAttackTarget, "Triggered");
        LogAttackStateChange(
            EnemyAttackRuntimeState.AttackInProgress,
            playerTarget != null ? Vector3.Distance(playerTarget.position, transform.position) : -1f,
            playerTarget != null ? ResolveHorizontalEdgeDistance(playerTarget, out _, out _) : -1f,
            playerTarget != null ? Mathf.Abs(playerTarget.position.y - transform.position.y) : 0f,
            IsGroundedForAttack(),
            false,
            "BeginAttack",
            Vector3.zero,
            Vector3.zero);

        // 瑙﹀彂鏀诲嚮鍔ㄧ敾鍚庯紝鎸夊姩鐢绘椂搴忚繘鍏ュ喎鍗存仮澶嶉樁娈点€?
        if (slimeAnimation != null)
        {
            LogSlimeAttackLifecycle("PlayAttackAnimation", pendingAttackTarget, "SlimeAnimation");
            slimeAnimation.PlayAttack(pendingAttackTarget);
            if (attackStyle == MonsterAttackStyle.ElementalBoss)
            {
                if (bossAttackRecoveryRoutine != null)
                {
                    StopCoroutine(bossAttackRecoveryRoutine);
                }

                bossAttackRecoveryRoutine = StartCoroutine(BossAttackRecoveryRoutine(bossSequenceId, AttackRecoveryDurationSeconds));
            }
            else
            {
                Invoke(nameof(FinishAttackRecovery), AttackRecoveryDurationSeconds);
            }
        }
        else
        {
            HandleAttackHit(pendingAttackTarget);
            if (attackStyle != MonsterAttackStyle.ElementalBoss)
            {
                FinishAttackRecovery();
            }
        }
    }

    private System.Collections.IEnumerator BossAttackRecoveryRoutine(int sequenceId, float delay)
    {
        yield return new WaitForSeconds(delay);
        FinishBossAttackRecovery(sequenceId);
    }

    private void FinishAttackRecovery()
    {
        RestoreBossLeapBodyOverride("FinishAttackRecovery");
        bool hadPendingAttack = attackInProgress;
        Transform finishedTarget = pendingAttackTarget;
        attackInProgress = false;
        activeBossAttackKind = BossAttackKind.None;
        if (hadPendingAttack && !UsesProjectileAttack() && !attackHitFrameTriggeredThisAttack)
        {
            lastMeleeAttackResult = "no-hit-frame";
            Debug.Log(
                "[EnemyMeleeDamageFlow] " +
                "enemy=" + name +
                " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
                " target=" + (finishedTarget != null ? finishedTarget.name : "null") +
                " source=EnemyMelee damageBeforeModifiers=0.00 damageAfterModifiers=0.00 hitChance=unresolved missRoll=unresolved isMiss=false" +
                " targetInvincible=false targetShield=0.00 targetCombatHealthFound=false TakeDamageCalled=false result=no-hit-frame playerHpBefore=n/a playerHpAfter=n/a",
                this);
        }
        LogSlimeAttackLifecycle("AttackFinished", pendingAttackTarget, "RecoveryComplete");
        pendingAttackTarget = null;
        if (debugAttackDiagnostics)
        {
            Debug.Log($"[EnemyAttack] AttackFinished enemy={name}", this);
        }
        LogAttackStateChange(
            EnemyAttackRuntimeState.AttackRecovery,
            playerTarget != null ? Vector3.Distance(playerTarget.position, transform.position) : -1f,
            playerTarget != null ? ResolveHorizontalEdgeDistance(playerTarget, out _, out _) : -1f,
            playerTarget != null ? Mathf.Abs(playerTarget.position.y - transform.position.y) : 0f,
            IsGroundedForAttack(),
            false,
            "FinishAttackRecovery",
            Vector3.zero,
            Vector3.zero);
    }

    private void FinishBossAttackRecovery(int sequenceId)
    {
        if (!IsBossActionActive(BossAttackKind.Melee, sequenceId))
        {
            return;
        }

        bossAttackRecoveryRoutine = null;
        FinishAttackRecovery();
        ReleaseBossActionLock("Completed", sequenceId);
    }

    private void FinishCurrentBossMeleeAction()
    {
        FinishBossAttackRecovery(activeBossActionSequenceId);
    }

    private void HandleAttackHit(Transform target)
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss && !IsBossActionActive(BossAttackKind.Melee, activeBossActionSequenceId))
        {
            LogBossActionLockTrace("InvalidLaunchBlocked", BossAttackKind.Melee, false, "MeleeHitCallbackWithoutActiveMelee", activeBossActionSequenceId);
            return;
        }

        Transform hitTarget = target != null ? target : pendingAttackTarget;
        attackHitFrameTriggeredThisAttack = true;
        LogSlimeAttackLifecycle("AttackHitCallback", hitTarget, hitTarget != null ? "CallbackReceived" : "NoTarget");
        LogEliteAttackDiagFromCurrentState(
            failReasonOverride: "None",
            tryStartMeleeCalled: true,
            attackRoutineStarted: true,
            damageApplied: false);
        if (hitTarget == null)
        {
            if (debugAttackDiagnostics)
            {
                Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target=null damage=0 reason=NoTarget", this);
            }
            LogAttackAttempt(hitTarget, false, false, false, false, "NoTarget");
            if (attackStyle == MonsterAttackStyle.ElementalBoss)
            {
                FinishCurrentBossMeleeAction();
            }
            else
            {
                FinishAttackRecovery();
            }
            return;
        }

        Vector3 toTarget = hitTarget.position - transform.position;
        toTarget.y = 0f;
        if (UsesProjectileAttack())
        {
            float projectileCenterDistance = toTarget.magnitude;
            float projectileEdgeDistance = ResolveHorizontalEdgeDistance(hitTarget, out _, out _);
            float projectileAttackDistance = ResolveAttackDistance(projectileCenterDistance, projectileEdgeDistance);
            if (projectileAttackDistance > attackHitRange)
            {
                if (debugAttackDiagnostics)
                {
                    Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target={hitTarget.name} damage=0 reason=TooFarDistance", this);
                }
                LogAttackAttempt(hitTarget, true, false, false, false, "TooFarDistance");
                if (attackStyle == MonsterAttackStyle.ElementalBoss)
                {
                    FinishCurrentBossMeleeAction();
                }
                else
                {
                    FinishAttackRecovery();
                }
                return;
            }

            LogAttackAttempt(hitTarget, true, true, true, true, "Projectile");
            ExecuteProjectileAttack(hitTarget);
        }
        else
        {
            bool insideHitRange;
            CombatHealth combatHealth = null;
            string meleeRejectReason = string.Empty;
            Vector3 meleeHitOrigin = Vector3.zero;
            Vector3 meleeAttackDirection = Vector3.zero;
            float resolvedMeleeHitRadius = 0f;
            int meleeHitColliderCount = 0;

            insideHitRange = TryResolveMeleeHitTarget(
                hitTarget,
                out combatHealth,
                out meleeRejectReason,
                out meleeHitOrigin,
                out meleeAttackDirection,
                out resolvedMeleeHitRadius,
                out meleeHitColliderCount);

            if (!insideHitRange)
            {
                if (debugAttackDiagnostics)
                {
                    Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target={hitTarget.name} damage=0 reason=HitCheckFailed", this);
                }
                string failureReason = string.IsNullOrEmpty(meleeRejectReason) ? "no-target-in-hit-range" : meleeRejectReason;
                lastMeleeAttackResult = failureReason;
                Debug.Log(
                    "[EnemyMeleeDamageFlow] " +
                    "enemy=" + name +
                    " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
                    " target=" + hitTarget.name +
                    " source=EnemyMelee damageBeforeModifiers=0.00 damageAfterModifiers=0.00 hitChance=unresolved missRoll=unresolved isMiss=false" +
                    " targetInvincible=false targetShield=0.00 targetCombatHealthFound=false TakeDamageCalled=false result=no-target-in-hit-range playerHpBefore=n/a playerHpAfter=n/a",
                    this);
                LogAttackAttempt(hitTarget, true, false, true, false, failureReason);
                if (attackStyle == MonsterAttackStyle.ElementalBoss)
                {
                    FinishCurrentBossMeleeAction();
                }
                else
                {
                    FinishAttackRecovery();
                }
                return;
            }

            if (combatHealth == null)
            {
                if (debugAttackDiagnostics)
                {
                    Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target={hitTarget.name} damage=0 reason=NotPlayer", this);
                }
                lastMeleeAttackResult = "invalid-target";
                Debug.Log(
                    "[EnemyMeleeDamageFlow] " +
                    "enemy=" + name +
                    " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
                    " target=" + hitTarget.name +
                    " source=EnemyMelee damageBeforeModifiers=0.00 damageAfterModifiers=0.00 hitChance=unresolved missRoll=unresolved isMiss=false" +
                    " targetInvincible=false targetShield=0.00 targetCombatHealthFound=false TakeDamageCalled=false result=invalid-target playerHpBefore=n/a playerHpAfter=n/a",
                    this);
                LogAttackAttempt(hitTarget, true, insideHitRange, true, false, "invalid-target");
                if (attackStyle == MonsterAttackStyle.ElementalBoss)
                {
                    FinishCurrentBossMeleeAction();
                }
                else
                {
                    FinishAttackRecovery();
                }
                return;
            }

            if (combatHealth != null)
            {
                BattleDamageType damageType = ResolvePrimaryDamageType();
                float currentAttackDamage = ResolveCurrentAttackDamage(damageType);
                LogSlimeAttackLifecycle("ApplyMeleeHit", hitTarget, $"damage={currentAttackDamage:F2}");
                if (debugLog && Mathf.Abs(ResolveAttackMultiplier() - lastLoggedAttackMultiplier) > 0.001f)
                {
                    Debug.Log($"[EnemyController] finalAttackDamage={currentAttackDamage:F2} multiplier={ResolveAttackMultiplier():F2}", this);
                    lastLoggedAttackMultiplier = ResolveAttackMultiplier();
                }

                if (debugAttackDiagnostics)
                {
                    Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target={hitTarget.name} damage={currentAttackDamage:F2}", this);
                    Debug.Log($"[EnemyAttack] ApplyDamage enemy={name} target={hitTarget.name}", this);
                }

                float playerHpBefore = ResolveCombatHealthValue(combatHealth);
                float playerShieldBefore = combatHealth.GetShield();
                Debug.Log(
                    "[EnemyMeleeDamageFlow] " +
                    "enemy=" + name +
                    " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
                    " target=" + hitTarget.name +
                    " source=EnemyMelee" +
                    " damageBeforeModifiers=" + currentAttackDamage.ToString("F2") +
                    " damageAfterModifiers=" + currentAttackDamage.ToString("F2") +
                    " hitChance=see-CombatEvasion" +
                    " missRoll=see-CombatEvasion" +
                    " isMiss=pending" +
                    " targetInvincible=false" +
                    " targetShield=" + playerShieldBefore.ToString("F2") +
                    " targetCombatHealthFound=true" +
                    " TakeDamageCalled=true" +
                    " result=pending" +
                    " playerHpBefore=" + playerHpBefore.ToString("F2") +
                    " playerHpAfter=pending",
                    this);

                combatHealth.TakeDamage(new BattleDamage(currentAttackDamage, damageType, gameObject));
                float playerHpAfter = ResolveCombatHealthValue(combatHealth);
                float playerShieldAfter = combatHealth.GetShield();
                string damageResult = ResolveEnemyMeleeDamageResult(playerHpBefore, playerHpAfter, playerShieldBefore, playerShieldAfter);
                lastMeleeAttackResult = damageResult;
                LogEliteAttackDiagFromCurrentState(
                    failReasonOverride: damageResult,
                    tryStartMeleeCalled: true,
                    attackRoutineStarted: true,
                    damageApplied: damageResult == "applied" || damageResult == "shielded");
                Debug.Log(
                    "[EnemyMeleeDamageFlow] " +
                    "enemy=" + name +
                    " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
                    " target=" + hitTarget.name +
                    " source=EnemyMelee" +
                    " damageBeforeModifiers=" + currentAttackDamage.ToString("F2") +
                    " damageAfterModifiers=" + currentAttackDamage.ToString("F2") +
                    " hitChance=see-CombatEvasion" +
                    " missRoll=see-CombatEvasion" +
                    " isMiss=" + (damageResult == "miss") +
                    " targetInvincible=" + (damageResult == "invincible") +
                    " targetShield=" + playerShieldAfter.ToString("F2") +
                    " targetCombatHealthFound=true" +
                    " TakeDamageCalled=true" +
                    " result=" + damageResult +
                    " playerHpBefore=" + playerHpBefore.ToString("F2") +
                    " playerHpAfter=" + playerHpAfter.ToString("F2"),
                    this);
                LogAttackAttempt(hitTarget, true, insideHitRange, true, true, "DamageApplied");
            }
            else if (debugAttackDiagnostics)
            {
                Debug.Log($"[EnemyAttack] DamageFrame enemy={name} target={hitTarget.name} damage=0 reason=NoCombatHealth", this);
                lastMeleeAttackResult = "invalid-target";
                LogAttackAttempt(hitTarget, true, insideHitRange, true, false, "NoCombatHealth");
            }
        }

        if (attackStyle == MonsterAttackStyle.ElementalBoss)
        {
            FinishCurrentBossMeleeAction();
        }
        else
        {
            FinishAttackRecovery();
        }
    }

    private bool CanHitMeleeTarget(Transform hitTarget)
    {
        if (hitTarget == null)
        {
            return false;
        }

        if (requireGroundedToAttack && !IsGroundedForAttack())
        {
            return false;
        }

        float verticalDifference = Mathf.Abs(hitTarget.position.y - transform.position.y);
        if (verticalDifference > Mathf.Max(0f, maxVerticalAttackDifference))
        {
            return false;
        }

        Vector3 playerCenter = ResolvePlayerBodyCenter(hitTarget);
        Vector3 enemyClosest = ResolveEnemyClosestPoint(playerCenter);
        Vector3 playerClosest = ResolvePlayerClosestPoint(hitTarget, enemyClosest);

        Vector3 flatEnemyPoint = enemyClosest;
        flatEnemyPoint.y = 0f;
        Vector3 flatPlayerPoint = playerClosest;
        flatPlayerPoint.y = 0f;
        float distance = Vector3.Distance(flatEnemyPoint, flatPlayerPoint);
        float hitRadius = Mathf.Max(0f, meleeBodyContactRadius);
        float horizontalAttackDistance = Mathf.Max(hitRadius, maxHorizontalAttackDistance > 0f ? maxHorizontalAttackDistance : attackHitRange);

        if (debugMeleeHitCheck)
        {
            Color debugColor = distance <= hitRadius ? Color.red : Color.cyan;
            Debug.DrawLine(enemyClosest, playerClosest, debugColor, 0.4f, false);
            Debug.DrawRay(enemyClosest, Vector3.up * 0.5f, debugColor, 0.4f, false);
            Debug.DrawRay(playerClosest, Vector3.up * 0.5f, debugColor, 0.4f, false);
        }

        if (distance <= hitRadius)
        {
            return true;
        }

        return distance <= horizontalAttackDistance;
    }

    private bool TryResolveMeleeHitTarget(
        Transform hitTarget,
        out CombatHealth targetCombatHealth,
        out string rejectReason,
        out Vector3 hitOrigin,
        out Vector3 attackDirection,
        out float hitRadius,
        out int hitColliderCount)
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss && activeBossAttackKind == BossAttackKind.Melee)
        {
            return TryResolveBossMeleeHitTarget(
                hitTarget,
                out targetCombatHealth,
                out rejectReason,
                out hitOrigin,
                out attackDirection,
                out hitRadius,
                out hitColliderCount);
        }

        targetCombatHealth = null;
        rejectReason = "NoTarget";
        hitOrigin = ResolveGenericMeleeHitOrigin(hitTarget, out attackDirection);
        hitRadius = ResolveCurrentMeleeHitRadius();
        EnsureBossMeleeHitResultsBuffer();
        hitColliderCount = Physics.OverlapSphereNonAlloc(
            hitOrigin,
            hitRadius,
            bossMeleeHitResults,
            ~0,
            QueryTriggerInteraction.Collide);

        Transform targetRoot = hitTarget != null ? hitTarget.root : null;
        System.Collections.Generic.List<string> details = new System.Collections.Generic.List<string>();

        for (int i = 0; i < hitColliderCount; i++)
        {
            Collider collider = bossMeleeHitResults[i];
            if (collider == null)
            {
                details.Add("collider=null acceptedTarget=false rejectReason=null-collider");
                continue;
            }

            Transform colliderRoot = collider.transform.root;
            bool isPlayer = BattleTargetUtility.IsPlayer(collider.gameObject) || (colliderRoot != null && BattleTargetUtility.IsPlayer(colliderRoot.gameObject));
            CombatHealth combatHealth = collider.GetComponentInParent<CombatHealth>();
            bool rootMatchesTarget = targetRoot == null || colliderRoot == targetRoot || (combatHealth != null && combatHealth.transform.root == targetRoot);
            bool targetDead = combatHealth != null && combatHealth.IsDead;
            bool acceptedTarget = isPlayer && combatHealth != null && rootMatchesTarget && !targetDead;
            string currentRejectReason = acceptedTarget
                ? "None"
                : !isPlayer
                    ? "WrongTag"
                    : combatHealth == null
                        ? "CombatHealthNotFound"
                        : !rootMatchesTarget
                            ? "WrongPlayerRoot"
                            : targetDead
                                ? "TargetDead"
                                : "Unknown";

            details.Add(
                "collider name=" + collider.name +
                " collider root=" + (colliderRoot != null ? colliderRoot.name : "null") +
                " collider layer=" + LayerMask.LayerToName(collider.gameObject.layer) +
                " collider tag=" + collider.tag +
                " collider isTrigger=" + collider.isTrigger +
                " has CombatHealth=" + (combatHealth != null) +
                " isPlayer=" + isPlayer +
                " acceptedTarget=" + acceptedTarget +
                " rejectReason=" + currentRejectReason);

            if (!acceptedTarget)
            {
                rejectReason = currentRejectReason;
                continue;
            }

            targetCombatHealth = combatHealth;
            rejectReason = string.Empty;
            break;
        }

        if (hitColliderCount <= 0)
        {
            rejectReason = "NoTargetInHitRange";
        }
        else if (targetCombatHealth == null && string.IsNullOrEmpty(rejectReason))
        {
            rejectReason = "InvalidTarget";
        }

        Debug.Log(
            "[EnemyMeleeHitCheck] " +
            "enemy=" + name +
            " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
            " target=" + (hitTarget != null ? hitTarget.name : "null") +
            " hitFrameReached=true" +
            " hitOrigin=" + hitOrigin +
            " hitRadius=" + hitRadius.ToString("F2") +
            " hitLayerMask=Everything" +
            " hitColliderCount=" + hitColliderCount +
            " attackDirection=" + attackDirection +
            " details=" + (details.Count > 0 ? string.Join(" | ", details) : "none") +
            " acceptedTarget=" + (targetCombatHealth != null) +
            " rejectReason=" + (string.IsNullOrEmpty(rejectReason) ? "None" : rejectReason),
            this);

        return targetCombatHealth != null;
    }

    private bool TryResolveBossMeleeHitTarget(
        Transform hitTarget,
        out CombatHealth targetCombatHealth,
        out string rejectReason,
        out Vector3 hitOrigin,
        out Vector3 attackDirection,
        out float hitRadius,
        out int hitColliderCount)
    {
        targetCombatHealth = null;
        rejectReason = "NoTarget";
        hitOrigin = ResolveBossMeleeHitOrigin(hitTarget, out attackDirection);
        hitRadius = Mathf.Max(0.1f, bossMeleeHitRadius);
        EnsureBossMeleeHitResultsBuffer();
        hitColliderCount = Physics.OverlapSphereNonAlloc(
            hitOrigin,
            hitRadius,
            bossMeleeHitResults,
            ~0,
            QueryTriggerInteraction.Collide);

        Transform targetRoot = hitTarget != null ? hitTarget.root : null;
        System.Collections.Generic.List<string> details = new System.Collections.Generic.List<string>();

        for (int i = 0; i < hitColliderCount; i++)
        {
            Collider collider = bossMeleeHitResults[i];
            if (collider == null)
            {
                details.Add("collider=null acceptedTarget=false rejectReason=null-collider");
                continue;
            }

            Transform colliderRoot = collider.transform.root;
            bool isPlayer = BattleTargetUtility.IsPlayer(collider.gameObject) || (colliderRoot != null && BattleTargetUtility.IsPlayer(colliderRoot.gameObject));
            CombatHealth combatHealth = collider.GetComponentInParent<CombatHealth>();
            bool rootMatchesTarget = targetRoot == null || colliderRoot == targetRoot || (combatHealth != null && combatHealth.transform.root == targetRoot);

            bool acceptedTarget = isPlayer && combatHealth != null && rootMatchesTarget;
            string currentRejectReason = acceptedTarget
                ? "None"
                : !isPlayer
                    ? "NotPlayer"
                    : combatHealth == null
                        ? "NoCombatHealth"
                        : !rootMatchesTarget
                            ? "WrongPlayerRoot"
                            : "Unknown";

            details.Add(
                "collider name=" + collider.name +
                " collider root=" + (colliderRoot != null ? colliderRoot.name : "null") +
                " collider layer=" + LayerMask.LayerToName(collider.gameObject.layer) +
                " collider tag=" + collider.tag +
                " collider isTrigger=" + collider.isTrigger +
                " has CombatHealth=" + (combatHealth != null) +
                " isPlayer=" + isPlayer +
                " acceptedTarget=" + acceptedTarget +
                " rejectReason=" + currentRejectReason);

            if (!acceptedTarget)
            {
                rejectReason = currentRejectReason;
                continue;
            }

            targetCombatHealth = combatHealth;
            rejectReason = string.Empty;
            break;
        }

        if (hitColliderCount <= 0)
        {
            rejectReason = "NoHitCollider";
        }
        else if (targetCombatHealth == null && string.IsNullOrEmpty(rejectReason))
        {
            rejectReason = "NoAcceptedPlayerCollider";
        }

        if (debugBossMeleeHit || debugAttackDiagnostics || debugLog)
        {
            Debug.Log(
                "[BossMeleeHitCheck] " +
                "enemy=" + name +
                " target=" + (hitTarget != null ? hitTarget.name : "null") +
                " hitOrigin=" + hitOrigin +
                " hitRadius=" + hitRadius.ToString("F2") +
                " hitLayerMask=Everything" +
                " hitColliderCount=" + hitColliderCount +
                " attackDirection=" + attackDirection +
                " details=" + (details.Count > 0 ? string.Join(" | ", details) : "none") +
                " finalAcceptedTarget=" + (targetCombatHealth != null) +
                " finalRejectReason=" + (string.IsNullOrEmpty(rejectReason) ? "None" : rejectReason),
                this);
        }

        return targetCombatHealth != null;
    }

    private Vector3 ResolveBossMeleeHitOrigin(Transform hitTarget, out Vector3 attackDirection)
    {
        Vector3 direction = hitTarget != null ? hitTarget.position - transform.position : Vector3.zero;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = playerTarget != null ? playerTarget.position - transform.position : Vector3.right;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.right;
        }

        attackDirection = direction.normalized;
        return transform.position + attackDirection * bossMeleeHitForwardOffset + Vector3.up * bossMeleeHitHeight;
    }

    private Vector3 ResolveGenericMeleeHitOrigin(Transform hitTarget, out Vector3 attackDirection)
    {
        Vector3 direction = hitTarget != null ? hitTarget.position - transform.position : Vector3.zero;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = playerTarget != null ? playerTarget.position - transform.position : Vector3.right;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector3.right;
        }

        attackDirection = direction.normalized;
        return transform.position + attackDirection * ResolveCurrentMeleeHitForwardOffset() + Vector3.up * ResolveCurrentMeleeHitHeight();
    }

    private void EnsureBossMeleeHitResultsBuffer()
    {
        if (bossMeleeHitResults == null || bossMeleeHitResults.Length < 16)
        {
            bossMeleeHitResults = new Collider[16];
        }
    }

    private static float ResolveCombatHealthValue(CombatHealth combatHealth)
    {
        if (combatHealth == null)
        {
            return 0f;
        }

        return combatHealth.resourceBank != null
            ? Mathf.Max(0f, combatHealth.resourceBank.currentHealth)
            : Mathf.Max(0f, combatHealth.currentHealth);
    }

    private static string ResolveBossMeleeDamageResult(float hpBefore, float hpAfter, float shieldBefore, float shieldAfter)
    {
        if (hpAfter < hpBefore)
        {
            return "applied";
        }

        if (shieldAfter < shieldBefore)
        {
            return "shielded";
        }

        return "no-effective-damage";
    }

    private static string ResolveEnemyMeleeDamageResult(float hpBefore, float hpAfter, float shieldBefore, float shieldAfter)
    {
        if (hpAfter < hpBefore)
        {
            return "applied";
        }

        if (shieldAfter < shieldBefore)
        {
            return "shielded";
        }

        return "no-effective-damage";
    }

    private void ResolveMeleeHitSources()
    {
        if (meleeEnemyCollider == null)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider candidate = colliders[i];
                if (candidate == null || !candidate.enabled || candidate.isTrigger)
                {
                    continue;
                }

                meleeEnemyCollider = candidate;
                break;
            }
        }

        if (debugSlimeAttackLogs && meleeEnemyCollider != null && meleeEnemyCollider != lastLoggedMeleeCollider)
        {
            Bounds bounds = meleeEnemyCollider.bounds;
            float radius = meleeEnemyCollider is SphereCollider sphereCollider ? sphereCollider.radius : -1f;
            Debug.Log(
                $"[SlimeAttackCollider] name={name} collider={meleeEnemyCollider.name} isTrigger={meleeEnemyCollider.isTrigger} radius={(radius >= 0f ? radius.ToString("F2") : "n/a")} boundsSize={bounds.size}",
                this);
            lastLoggedMeleeCollider = meleeEnemyCollider;
        }

        if (meleeEnemySpriteRenderer == null)
        {
            Transform namedVisual = transform.Find("Visual_Slime");
            if (namedVisual != null)
            {
                meleeEnemySpriteRenderer = namedVisual.GetComponent<SpriteRenderer>();
            }
        }

        if (meleeEnemySpriteRenderer == null && slimeAnimation != null)
        {
            SpriteRenderer spriteRenderer = slimeAnimation.GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer != null)
            {
                meleeEnemySpriteRenderer = spriteRenderer;
            }
        }
    }

    private Vector3 ResolveEnemyBodyCenter()
    {
        ResolveMeleeHitSources();

        if (meleeEnemyCollider != null)
        {
            return meleeEnemyCollider.bounds.center;
        }

        if (meleeEnemySpriteRenderer != null)
        {
            return meleeEnemySpriteRenderer.bounds.center;
        }

        return transform.position;
    }

    private Vector3 ResolveEnemyClosestPoint(Vector3 playerCenter)
    {
        ResolveMeleeHitSources();

        if (meleeEnemyCollider != null)
        {
            return meleeEnemyCollider.ClosestPoint(playerCenter);
        }

        if (meleeEnemySpriteRenderer != null)
        {
            Bounds bounds = meleeEnemySpriteRenderer.bounds;
            return bounds.ClosestPoint(playerCenter);
        }

        return transform.position;
    }

    private Vector3 ResolvePlayerBodyCenter(Transform hitTarget)
    {
        Collider playerCollider = ResolvePlayerCollider(hitTarget);
        if (playerCollider != null)
        {
            return playerCollider.bounds.center;
        }

        return hitTarget.position;
    }

    private Vector3 ResolvePlayerClosestPoint(Transform hitTarget, Vector3 enemyPoint)
    {
        Collider playerCollider = ResolvePlayerCollider(hitTarget);
        if (playerCollider != null)
        {
            return playerCollider.ClosestPoint(enemyPoint);
        }

        return hitTarget.position;
    }

    private float ResolveHorizontalEdgeDistance(Transform hitTarget, out Vector3 enemyClosest, out Vector3 playerClosest)
    {
        if (hitTarget == null)
        {
            enemyClosest = transform.position;
            playerClosest = transform.position;
            return float.MaxValue;
        }

        Vector3 playerCenter = ResolvePlayerBodyCenter(hitTarget);
        enemyClosest = ResolveEnemyClosestPoint(playerCenter);
        playerClosest = ResolvePlayerClosestPoint(hitTarget, enemyClosest);
        Vector3 flatEnemyPoint = enemyClosest;
        flatEnemyPoint.y = 0f;
        Vector3 flatPlayerPoint = playerClosest;
        flatPlayerPoint.y = 0f;
        return Vector3.Distance(flatEnemyPoint, flatPlayerPoint);
    }

    private float ResolveAttackDistance(float horizontalCenterDistance, float horizontalEdgeDistance)
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss)
        {
            return horizontalEdgeDistance;
        }

        return UsesProjectileAttack() ? horizontalCenterDistance : horizontalEdgeDistance;
    }

    private float ResolveVerticalCombatDifference(Transform hitTarget, out float verticalCenterDifference)
    {
        if (hitTarget == null)
        {
            verticalCenterDifference = 0f;
            return 0f;
        }

        Vector3 enemyBodyCenter = ResolveEnemyBodyCenter();
        Vector3 playerBodyCenter = ResolvePlayerBodyCenter(hitTarget);
        verticalCenterDifference = Mathf.Abs(playerBodyCenter.y - enemyBodyCenter.y);

        Vector3 enemyClosest = ResolveEnemyClosestPoint(playerBodyCenter);
        Vector3 playerClosest = ResolvePlayerClosestPoint(hitTarget, enemyClosest);
        return Mathf.Abs(playerClosest.y - enemyClosest.y);
    }

    private float ResolveCurrentMeleeHitRadius()
    {
        MonsterRank rank = monsterIdentity != null ? monsterIdentity.rank : MonsterRank.Normal;
        return rank switch
        {
            MonsterRank.Boss => Mathf.Max(0.1f, bossMeleeHitRadius),
            MonsterRank.Elite => Mathf.Max(0.1f, eliteMeleeHitRadius),
            _ => Mathf.Max(0.1f, normalMeleeHitRadius)
        };
    }

    private float ResolveCurrentMeleeHitForwardOffset()
    {
        MonsterRank rank = monsterIdentity != null ? monsterIdentity.rank : MonsterRank.Normal;
        return rank switch
        {
            MonsterRank.Boss => bossMeleeHitForwardOffset,
            MonsterRank.Elite => eliteMeleeHitForwardOffset,
            _ => normalMeleeHitForwardOffset
        };
    }

    private float ResolveCurrentMeleeHitHeight()
    {
        MonsterRank rank = monsterIdentity != null ? monsterIdentity.rank : MonsterRank.Normal;
        return rank switch
        {
            MonsterRank.Boss => bossMeleeHitHeight,
            MonsterRank.Elite => eliteMeleeHitHeight,
            _ => normalMeleeHitHeight
        };
    }

    private void LogEnemyMeleeDecision(
        float distance,
        bool canAttack,
        string failReason,
        bool isGrounded,
        string selectedAttack,
        string targetName,
        float meleeCooldownRemaining,
        bool isAttacking,
        bool isStunned,
        bool isDead)
    {
        if (!(debugAttackDiagnostics || debugMeleeHitCheck || debugLog))
        {
            return;
        }

        if (Time.time < nextEnemyMeleeDecisionLogTime)
        {
            return;
        }

        nextEnemyMeleeDecisionLogTime = Time.time + 1f;
        Debug.Log(
            "[EnemyMeleeDecision] " +
            "enemy=" + name +
            " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
            " species=" + (monsterIdentity != null ? monsterIdentity.species.ToString() : "Unknown") +
            " attackStyle=" + attackStyle +
            " distanceToTarget=" + distance.ToString("F2") +
            " meleeAttackRange=" + attackRange.ToString("F2") +
            " canAttack=" + canAttack +
            " failReason=" + (string.IsNullOrEmpty(failReason) ? "None" : failReason) +
            " isGrounded=" + isGrounded +
            " targetAssigned=" + (playerTarget != null) +
            " targetName=" + targetName +
            " selectedAttack=" + selectedAttack +
            " meleeCooldownRemaining=" + meleeCooldownRemaining.ToString("F2") +
            " isAttacking=" + isAttacking +
            " isStunned=" + isStunned +
            " isDead=" + isDead,
            this);
    }

    private void LogEliteAttackDiag(
        float horizontalCenterDistance,
        float horizontalEdgeDistance,
        float verticalDistance,
        float verticalCenterDistance,
        bool isGrounded,
        bool canMove,
        bool canAttack,
        bool cooldownReady,
        string currentState,
        string failReason,
        bool tryStartMeleeCalled,
        bool attackRoutineStarted,
        bool damageApplied)
    {
        if (monsterIdentity == null || monsterIdentity.rank != MonsterRank.Elite)
        {
            return;
        }

        if (!(debugAttackDiagnostics || debugMeleeHitCheck || debugLog))
        {
            return;
        }

        if (Time.time < nextEnemyMeleeDecisionLogTime)
        {
            return;
        }

        Debug.Log(
            "[EliteAttackDiag] " +
            "enemy=" + name +
            " target exists=" + (playerTarget != null) +
            " rank=" + monsterIdentity.rank +
            " horizontalCenterDistance=" + horizontalCenterDistance.ToString("F2") +
            " horizontalEdgeDistance=" + horizontalEdgeDistance.ToString("F2") +
            " verticalDistance=" + verticalDistance.ToString("F2") +
            " verticalCenterDistance=" + verticalCenterDistance.ToString("F2") +
            " attackRange=" + attackRange.ToString("F2") +
            " attackHitRange=" + attackHitRange.ToString("F2") +
            " maxHorizontalAttackDistance=" + maxHorizontalAttackDistance.ToString("F2") +
            " maxVerticalAttackDifference=" + maxVerticalAttackDifference.ToString("F2") +
            " isGrounded=" + isGrounded +
            " canMove=" + canMove +
            " canAttack=" + canAttack +
            " cooldownReady=" + cooldownReady +
            " currentState=" + currentState +
            " failReason=" + (string.IsNullOrEmpty(failReason) ? "None" : failReason) +
            " tryStartMeleeCalled=" + tryStartMeleeCalled +
            " attackRoutineStarted=" + attackRoutineStarted +
            " damageApplied=" + damageApplied,
            this);
    }

    private void LogEliteAttackDiagFromCurrentState(
        string failReasonOverride,
        bool tryStartMeleeCalled,
        bool attackRoutineStarted,
        bool damageApplied)
    {
        if (monsterIdentity == null || monsterIdentity.rank != MonsterRank.Elite)
        {
            return;
        }

        Transform target = playerTarget;
        float horizontalCenterDistance = -1f;
        float horizontalEdgeDistance = -1f;
        float verticalDistance = 0f;
        float verticalCenterDistance = 0f;

        if (target != null)
        {
            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0f;
            horizontalCenterDistance = new Vector2(toPlayer.x, toPlayer.z).magnitude;
            horizontalEdgeDistance = ResolveHorizontalEdgeDistance(target, out _, out _);
            verticalDistance = ResolveVerticalCombatDifference(target, out verticalCenterDistance);
        }

        bool grounded = IsGroundedForAttack();
        string failReason = !string.IsNullOrEmpty(failReasonOverride)
            ? failReasonOverride
            : EvaluateAttackFailReason(horizontalEdgeDistance, verticalDistance, grounded);
        bool canAttack = string.IsNullOrEmpty(failReason);
        bool cooldownReady = Time.time >= nextAttackTime;
        bool canMove = rb != null && !attackInProgress;
        string currentState = target == null
            ? EnemyAttackRuntimeState.NoTarget.ToString()
            : (attackInProgress
                ? EnemyAttackRuntimeState.AttackInProgress.ToString()
                : (canAttack ? EnemyAttackRuntimeState.AttackReady.ToString() : EnemyAttackRuntimeState.Chase.ToString()));

        LogEliteAttackDiag(
            horizontalCenterDistance,
            horizontalEdgeDistance,
            verticalDistance,
            verticalCenterDistance,
            grounded,
            canMove,
            canAttack,
            cooldownReady,
            currentState,
            failReason,
            tryStartMeleeCalled,
            attackRoutineStarted,
            damageApplied);
    }

    private Collider ResolvePlayerCollider(Transform hitTarget)
    {
        if (hitTarget == null)
        {
            return null;
        }

        CapsuleCollider capsule = hitTarget.GetComponentInParent<CapsuleCollider>();
        if (capsule != null && capsule.enabled)
        {
            return capsule;
        }

        CharacterController characterController = hitTarget.GetComponentInParent<CharacterController>();
        if (characterController != null && characterController.enabled)
        {
            return characterController;
        }

        Collider[] colliders = hitTarget.GetComponentsInParent<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider candidate = colliders[i];
            if (candidate != null && candidate.enabled && !candidate.isTrigger)
            {
                return candidate;
            }
        }

        return null;
    }

    private bool UsesProjectileAttack()
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss)
        {
            return activeBossAttackKind == BossAttackKind.Ranged;
        }

        return attackStyle == MonsterAttackStyle.Ranged;
    }

    private void ExecuteProjectileAttack(Transform hitTarget)
    {
        ExecuteProjectileAttack(hitTarget, 0, 1);
    }

    private void ExecuteProjectileAttack(Transform hitTarget, int volleyIndex, int volleyCount)
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss)
        {
            // Boss multi-skill expansion point: keep projectile as the phase-one fallback
            // until fire ring / ground spike attacks are introduced.
            if (debugAttackDiagnostics || debugLog)
            {
                float distanceToTarget = hitTarget != null ? Vector3.Distance(transform.position, hitTarget.position) : -1f;
                Debug.Log($"[BossAcidAttack] enter ranged branch boss={name} target={(hitTarget != null ? hitTarget.name : "null")} distance={distanceToTarget:F2} attackStyle={attackStyle}", this);
            }
            FireProjectileAt(hitTarget, volleyIndex, volleyCount);
            return;
        }

        FireProjectileAt(hitTarget);
    }

    private void PlayMoveAnimation(Vector3 direction, float currentMoveSpeed)
    {
        if (slimeAnimation == null)
        {
            return;
        }

        slimeAnimation.PlayMoveAnimation(new Vector2(direction.x, direction.z), currentMoveSpeed);
    }

    private void StopMoveAnimation()
    {
        if (slimeAnimation == null)
        {
            return;
        }

        slimeAnimation.StopMoveAnimation();
    }

    private void ResolvePlayerTarget()
    {
        if (HasUsableTarget(playerTarget))
        {
            return;
        }

        if (Time.time < nextTargetResolveTime)
        {
            return;
        }

        nextTargetResolveTime = Time.time + Mathf.Max(0.05f, targetResolveRetryInterval);
        TryResolveTarget("AutoReacquire");
    }

    private bool TryResolveTarget(string source)
    {
        if (HasUsableTarget(playerTarget))
        {
            return true;
        }

        string oldTargetName = playerTarget != null ? playerTarget.name : "null";
        Transform resolvedTarget = null;

        if (playerBootstrap == null)
        {
            playerBootstrap = FindObjectOfType<Player2Bootstrap>();
        }

        if (playerBootstrap != null)
        {
            playerBootstrap.EnsureInitializedForSpawn();
            Transform currentPlayer = playerBootstrap.CurrentPlayerTransform;
            if (HasUsableTarget(currentPlayer))
            {
                resolvedTarget = currentPlayer;
            }
        }

        if (resolvedTarget == null && !string.IsNullOrEmpty(playerTag))
        {
            GameObject playerObject = GameObject.FindWithTag(playerTag);
            if (playerObject != null && playerObject.activeInHierarchy)
            {
                resolvedTarget = playerObject.transform;
            }
        }

        playerTarget = null;
        if (!HasUsableTarget(resolvedTarget))
        {
            Debug.Log(
                $"[EnemyTargetResolve] enemy={name} oldTarget={oldTargetName} newTarget=null success=False source={source} reason=NoActivePlayerFound",
                this);
            return false;
        }

        AssignTarget(resolvedTarget, source, oldTargetName);
        return true;
    }

    private bool HasUsableTarget(Transform target)
    {
        return target != null && target.gameObject != null && target.gameObject.activeInHierarchy;
    }

    private void AssignTarget(Transform target, string source, string oldTargetNameOverride = null)
    {
        string oldTargetName = oldTargetNameOverride ?? (playerTarget != null ? playerTarget.name : "null");
        playerTarget = HasUsableTarget(target) ? target : null;

        Debug.Log(
            $"[EnemyTargetResolve] enemy={name} oldTarget={oldTargetName} newTarget={(playerTarget != null ? playerTarget.name : "null")} success={(playerTarget != null)} source={source}{(playerTarget == null ? " reason=NoActivePlayerFound" : string.Empty)}",
            this);
    }

    public string BuildRuntimeDebugSummary(Transform fallbackTarget = null)
    {
        Transform target = HasUsableTarget(playerTarget) ? playerTarget : fallbackTarget;
        float centerDistance = target != null ? Vector3.Distance(target.position, transform.position) : -1f;
        float edgeDistance = float.MaxValue;
        Vector3 enemyClosest = transform.position;
        Vector3 playerClosest = target != null ? target.position : transform.position;
        if (target != null)
        {
            edgeDistance = ResolveHorizontalEdgeDistance(target, out enemyClosest, out playerClosest);
        }

        float verticalDifference = target != null ? Mathf.Abs(target.position.y - transform.position.y) : -1f;
        bool grounded = IsGroundedForAttack();
        string failReason = EvaluateAttackFailReason(edgeDistance, verticalDifference, grounded);
        bool canAttack = string.IsNullOrEmpty(failReason);
        CombatHealth health = GetComponent<CombatHealth>();
        EnemyDebuffReceiver receiver = ResolveDebuffReceiver();
        float cooldownRemaining = Mathf.Max(0f, nextAttackTime - Time.time);
        bool isStunned = receiver != null && receiver.GetMoveSpeedMultiplier() <= 0f;
        bool canMove = rb != null && !attackInProgress;
        string state = playerTarget == null
            ? EnemyAttackRuntimeState.NoTarget.ToString()
            : (attackInProgress ? EnemyAttackRuntimeState.AttackInProgress.ToString() : (canAttack ? EnemyAttackRuntimeState.AttackReady.ToString() : EnemyAttackRuntimeState.Chase.ToString()));
        Collider primaryCollider = meleeEnemyCollider != null ? meleeEnemyCollider : GetComponent<Collider>();
        string rigidbodyConstraints = rb != null ? rb.constraints.ToString() : "None";

        return
            "[MonsterDebug] " +
            $"name={name} position={transform.position} rank={(monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown")} " +
            $"species={(monsterIdentity != null ? monsterIdentity.species.ToString() : "Unknown")} attackStyle={attackStyle} " +
            $"enemyControllerExists=true enemyControllerEnabled={enabled} targetExists={(target != null)} targetName={(target != null ? target.name : "null")} " +
            $"distanceToTarget={(centerDistance >= 0f ? centerDistance.ToString("F2") : "n/a")} edgeDistance={(edgeDistance < float.MaxValue ? edgeDistance.ToString("F2") : "n/a")} " +
            $"moveSpeed={moveSpeed:F2} attackRange={attackRange:F2} rangedAttackRange={attackRange:F2} meleeAttackRange={attackHitRange:F2} " +
            $"currentState={state} canMove={canMove} canAttack={canAttack} failReason={(string.IsNullOrEmpty(failReason) ? "None" : failReason)} " +
            $"isDead={(health != null && health.IsDead)} isStunned={isStunned} isAttacking={attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking)} " +
            $"attackCooldownRemaining={cooldownRemaining:F2} lastAttackTime={lastAttackTime:F2} " +
            $"health={(health != null ? health.currentHealth.ToString("F1") : "n/a")}/{(health != null ? health.MaxHealthValue.ToString("F1") : "n/a")} " +
            $"rigidbodyConstraints={rigidbodyConstraints} colliderEnabled={(primaryCollider != null && primaryCollider.enabled)} " +
            $"layer={LayerMask.LayerToName(gameObject.layer)} tag={gameObject.tag}";
    }

    private EnemyDebuffReceiver ResolveDebuffReceiver()
    {
        if (debuffReceiver == null)
        {
            debuffReceiver = GetComponent<EnemyDebuffReceiver>();
        }

        return debuffReceiver;
    }

    private float ResolveExternalMoveMultiplier()
    {
        EnemyDebuffReceiver receiver = ResolveDebuffReceiver();
        return receiver != null ? receiver.GetMoveSpeedMultiplier() : 1f;
    }

    private float ResolveAttackMultiplier()
    {
        EnemyDebuffReceiver receiver = ResolveDebuffReceiver();
        return receiver != null ? receiver.GetAttackMultiplier() : 1f;
    }

    private float ResolveOutgoingDamageMultiplier()
    {
        EnemyDebuffReceiver receiver = ResolveDebuffReceiver();
        return receiver != null ? receiver.GetOutgoingDamageMultiplier() : 1f;
    }

    private float ResolveCurrentAttackCooldown()
    {
        float baseAttackCooldown = Mathf.Max(0.1f, attackCooldown * Mathf.Max(0.1f, attackIntervalMultiplier));
        float attackSpeedMultiplier = BattleStatUtility.GetEnemyAttackSpeedMultiplier(combatStats);
        float externalAttackCooldownMultiplier = Mathf.Max(1f, ResolveAttackMultiplier());
        return Mathf.Max(0.1f, baseAttackCooldown / Mathf.Max(0.1f, attackSpeedMultiplier) * externalAttackCooldownMultiplier);
    }

    private void LogAttackDiagnostics(
        float statsSpeed,
        float distanceToTarget,
        float horizontalDistance,
        float verticalDifference,
        bool isGrounded,
        bool canAttack,
        string failReason)
    {
        if (!debugAttackDiagnostics || Time.time < nextAttackDiagnosticTime)
        {
            return;
        }

        nextAttackDiagnosticTime = Time.time + Mathf.Max(0.1f, debugAttackLogInterval);

        float attackCooldownMultiplier = ResolveAttackMultiplier();
        float baseAttackCooldown = Mathf.Max(0.1f, attackCooldown * Mathf.Max(0.1f, attackIntervalMultiplier));
        float enemyAttackSpeedMultiplier = BattleStatUtility.GetEnemyAttackSpeedMultiplier(combatStats);
        float finalAttackCooldown = Mathf.Max(0.1f, baseAttackCooldown / Mathf.Max(0.1f, enemyAttackSpeedMultiplier) * Mathf.Max(1f, attackCooldownMultiplier));
        float timeSinceLastAttack = lastAttackTime >= 0f ? Mathf.Max(0f, Time.time - lastAttackTime) : -1f;
        float timeUntilNextAttack = Mathf.Max(0f, nextAttackTime - Time.time);
        bool isBlocked = attackCooldownMultiplier <= 0f;
        bool isKnockback = false;
        bool isStunned = false;
        bool isAttacking = attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking);

        Debug.Log(
            $"[EnemyAttackDiag] name={name} target={(playerTarget != null ? playerTarget.name : "null")} speed={statsSpeed:F2} attackCooldown={attackCooldown:F2} attackIntervalMultiplier={attackIntervalMultiplier:F2} baseAttackCooldown={baseAttackCooldown:F2} enemyAttackSpeedMultiplier={enemyAttackSpeedMultiplier:F2} externalAttackCooldownMultiplier={Mathf.Max(1f, attackCooldownMultiplier):F2} finalAttackCooldown={finalAttackCooldown:F2} timeSinceLastAttack={timeSinceLastAttack:F2} timeUntilNextAttack={timeUntilNextAttack:F2} distanceToTarget={distanceToTarget:F2} horizontalDistance={horizontalDistance:F2} verticalDifference={verticalDifference:F2} attackRange={attackRange:F2} maxHorizontalAttackDistance={maxHorizontalAttackDistance:F2} maxVerticalAttackDifference={maxVerticalAttackDifference:F2} requireGroundedToAttack={requireGroundedToAttack} isGrounded={isGrounded} groundedProbeDistance={groundedProbeDistance:F2} isAttacking={isAttacking} isKnockback={isKnockback} isBlocked={isBlocked} isStunned={isStunned} canAttack={canAttack} failReason={(string.IsNullOrEmpty(failReason) ? "None" : failReason)}",
            this);
    }

    private void LogSpeedDiagnostics(
        float statsSpeed,
        float distanceToTarget,
        bool canAttack)
    {
        if (!debugSpeedDiagnostics || Time.time < nextSpeedDiagnosticTime)
        {
            return;
        }

        nextSpeedDiagnosticTime = Time.time + Mathf.Max(0.1f, debugSpeedLogInterval);

        float speedBonus = Mathf.Max(0f, statsSpeed - 1f);
        float baseEnemyAttackSpeedMultiplier = BattleStatUtility.EnemyAttackSpeedBaseMultiplier;
        float extraEnemyAttackSpeedMax = BattleStatUtility.EnemyAttackSpeedExtraMax;
        float enemyAttackSpeedSoftCap = BattleStatUtility.EnemyAttackSpeedSoftCap;
        float baseAttackCooldown = Mathf.Max(0.1f, attackCooldown * Mathf.Max(0.1f, attackIntervalMultiplier));
        float attackSpeedMultiplier = BattleStatUtility.GetEnemyAttackSpeedMultiplier(combatStats);
        float externalAttackCooldownMultiplier = Mathf.Max(1f, ResolveAttackMultiplier());
        float finalAttackCooldown = Mathf.Max(0.1f, baseAttackCooldown / Mathf.Max(0.1f, attackSpeedMultiplier) * externalAttackCooldownMultiplier);
        float timeSinceLastAttack = lastAttackTime >= 0f ? Mathf.Max(0f, Time.time - lastAttackTime) : -1f;
        float timeUntilNextAttack = Mathf.Max(0f, nextAttackTime - Time.time);

        Debug.Log(
            $"[EnemyAttackDiag] name={name} speed={statsSpeed:F2} speedBonus={speedBonus:F2} baseEnemyAttackSpeedMultiplier={baseEnemyAttackSpeedMultiplier:F2} extraEnemyAttackSpeedMax={extraEnemyAttackSpeedMax:F2} enemyAttackSpeedSoftCap={enemyAttackSpeedSoftCap:F2} enemyAttackSpeedMultiplier={attackSpeedMultiplier:F2} attackCooldown={attackCooldown:F2} attackIntervalMultiplier={attackIntervalMultiplier:F2} baseAttackCooldown={baseAttackCooldown:F2} externalAttackCooldownMultiplier={externalAttackCooldownMultiplier:F2} finalAttackCooldown={finalAttackCooldown:F2} timeSinceLastAttack={timeSinceLastAttack:F2} timeUntilNextAttack={timeUntilNextAttack:F2} canAttack={canAttack} distanceToTarget={distanceToTarget:F2} attackRange={attackRange:F2}",
            this);
    }

    private void LogChaseDiagnostics(
        float distanceToTarget,
        bool canMove,
        bool canAttack,
        bool isKnockback,
        bool isStunned,
        bool isBlocked,
        string currentState,
        float moveSpeed,
        string targetName,
        string reason)
    {
        if (!debugChaseDiagnostics || Time.time < nextChaseDiagnosticTime)
        {
            return;
        }

        nextChaseDiagnosticTime = Time.time + Mathf.Max(0.1f, debugSpeedLogInterval);
        CombatHealth health = GetComponent<CombatHealth>();
        bool isDead = health != null && health.IsDead;
        bool isAttacking = attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking);
        float cooldownRemaining = Mathf.Max(0f, nextAttackTime - Time.time);
        bool targetAssigned = playerTarget != null;
        Debug.Log(
            $"[EnemyChaseDiag] enemy={name} rank={(monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown")} species={(monsterIdentity != null ? monsterIdentity.species.ToString() : "Unknown")} attackStyle={attackStyle} " +
            $"distanceToTarget={distanceToTarget:F2} canMove={canMove} canAttack={canAttack} failReason={reason} " +
            $"requireGroundedToAttack={requireGroundedToAttack} isGrounded={lastGroundProbeHit || (Time.time - lastGroundedTime <= Mathf.Max(0f, groundedAttackGraceTime))} groundedProbeDistance={groundedProbeDistance:F2} groundedProbeOrigin={lastGroundProbeOrigin} groundedProbeCastDistance={lastGroundProbeCastDistance:F2} " +
            $"groundedProbeHit={lastGroundProbeHit} groundedHitName={lastGroundHitName} groundedHitLayer={(lastGroundHitLayer >= 0 ? LayerMask.LayerToName(lastGroundHitLayer) : "None")} " +
            $"attackCooldownRemaining={cooldownRemaining:F2} isDead={isDead} isStunned={isStunned} isKnockback={isKnockback} isAttacking={isAttacking} isBlocked={isBlocked} " +
            $"targetAssigned={targetAssigned} targetName={targetName} currentState={currentState} moveSpeed={moveSpeed:F2}",
            this);
    }

    private void LogAttackStateChange(
        EnemyAttackRuntimeState newState,
        float centerDistance,
        float edgeDistance,
        float verticalDifference,
        bool isGrounded,
        bool canBeginAttack,
        string reason,
        Vector3 enemyClosestPoint,
        Vector3 playerClosestPoint)
    {
        if (lastLoggedAttackState == newState)
        {
            return;
        }

        if (!debugAttackStateTransitions)
        {
            LogSlimeAttackCheck(newState, centerDistance, edgeDistance, verticalDifference, isGrounded, canBeginAttack, reason);
            lastLoggedAttackState = newState;
            return;
        }

        Debug.Log(
            $"[EnemyAttackState] name={name} kind={(monsterIdentity != null ? monsterIdentity.species.ToString() : "Unknown")} rank={(monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown")} attackStyle={attackStyle} oldState={lastLoggedAttackState} newState={newState} centerDistance={centerDistance:F2} edgeDistance={edgeDistance:F2} attackRange={attackRange:F2} attackHitRange={attackHitRange:F2} stopDistance={stopDistance:F2} cooldownRemaining={Mathf.Max(0f, nextAttackTime - Time.time):F2} enemyClosest={enemyClosestPoint} playerClosest={playerClosestPoint} reason={reason}",
            this);
        LogSlimeAttackCheck(newState, centerDistance, edgeDistance, verticalDifference, isGrounded, canBeginAttack, reason);
        lastLoggedAttackState = newState;
    }

    private void LogAttackAttempt(
        Transform hitTarget,
        bool insideAttackRange,
        bool insideHitRange,
        bool attackTriggered,
        bool damageApplied,
        string failureReason)
    {
        if (!debugAttackDiagnostics)
        {
            return;
        }

        Debug.Log(
            $"[EnemyAttackAttempt] name={name} kind={(monsterIdentity != null ? monsterIdentity.species.ToString() : "Unknown")} rank={(monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown")} attackStyle={attackStyle} target={(hitTarget != null ? hitTarget.name : "null")} insideAttackRange={insideAttackRange} insideHitRange={insideHitRange} attackTriggered={attackTriggered} hitDetected={insideHitRange} damageApplied={damageApplied} failureReason={failureReason}",
            this);
    }

    private string EvaluateAttackFailReason(float horizontalDistance, float verticalDifference, bool isGrounded)
    {
        if (playerTarget == null)
        {
            return "NoTarget";
        }

        if (attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking))
        {
            return "AnimationBusy";
        }

        if (Time.time < nextAttackTime)
        {
            return "Cooldown";
        }

        float resolvedAttackRange = Mathf.Max(0.1f, attackRange);
        float resolvedMaxHorizontalAttackDistance = Mathf.Max(0.1f, maxHorizontalAttackDistance > 0f ? maxHorizontalAttackDistance : resolvedAttackRange);
        if (horizontalDistance > resolvedMaxHorizontalAttackDistance)
        {
            return "TooFarHorizontal";
        }

        if (horizontalDistance > resolvedAttackRange)
        {
            return "TooFarDistance";
        }

        if (verticalDifference > Mathf.Max(0f, maxVerticalAttackDifference))
        {
            return "TooHighVerticalDifference";
        }

        if (maxVerticalTargetDifference > 0f && verticalDifference > maxVerticalTargetDifference)
        {
            return "TooFarDistance";
        }

        if (requireGroundedToAttack && !isGrounded)
        {
            return "NotGrounded";
        }

        EnemyDebuffReceiver receiver = ResolveDebuffReceiver();
        if (receiver != null && receiver.GetAttackMultiplier() <= 0f)
        {
            return "Blocked";
        }

        return string.Empty;
    }

    private bool IsGroundedForAttack()
    {
        if (ProbeGrounded())
        {
            lastGroundedTime = Time.time;
            return true;
        }

        return Time.time - lastGroundedTime <= Mathf.Max(0f, groundedAttackGraceTime);
    }

    private bool ProbeGrounded()
    {
        float probeDistance = Mathf.Max(0.01f, groundedProbeDistance);
        ResolveMeleeHitSources();
        lastGroundProbeHit = false;
        lastGroundHitName = "None";
        lastGroundHitLayer = -1;
        lastGroundHitY = float.NegativeInfinity;
        lastGroundProbeCastDistance = probeDistance;
        lastGroundProbeOrigin = transform.position + Vector3.up * 0.05f;

        Collider sourceCollider = meleeEnemyCollider != null ? meleeEnemyCollider : GetComponent<Collider>();
        if (sourceCollider != null)
        {
            Bounds bounds = sourceCollider.bounds;
            Vector3 origin = bounds.center + Vector3.up * 0.05f;
            float radius = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.45f, 0.05f, 0.45f);
            float castDistance = Mathf.Max(probeDistance + bounds.extents.y + 0.1f, probeDistance + 0.15f);
            lastGroundProbeOrigin = origin;
            lastGroundProbeCastDistance = castDistance;
            if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit sphereHit, castDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                lastGroundProbeHit = true;
                lastGroundHitName = sphereHit.collider != null ? sphereHit.collider.name : "Unknown";
                lastGroundHitLayer = sphereHit.collider != null ? sphereHit.collider.gameObject.layer : -1;
                lastGroundHitY = sphereHit.point.y;
                return true;
            }

            float rayDistance = castDistance + radius;
            lastGroundProbeCastDistance = rayDistance;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit rayHit, rayDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                lastGroundProbeHit = true;
                lastGroundHitName = rayHit.collider != null ? rayHit.collider.name : "Unknown";
                lastGroundHitLayer = rayHit.collider != null ? rayHit.collider.gameObject.layer : -1;
                lastGroundHitY = rayHit.point.y;
                return true;
            }

            return false;
        }

        Vector3 fallbackOrigin = transform.position + Vector3.up * 0.05f;
        lastGroundProbeOrigin = fallbackOrigin;
        lastGroundProbeCastDistance = probeDistance + 0.05f;
        if (Physics.Raycast(fallbackOrigin, Vector3.down, out RaycastHit fallbackHit, probeDistance + 0.05f, ~0, QueryTriggerInteraction.Ignore))
        {
            lastGroundProbeHit = true;
            lastGroundHitName = fallbackHit.collider != null ? fallbackHit.collider.name : "Unknown";
            lastGroundHitLayer = fallbackHit.collider != null ? fallbackHit.collider.gameObject.layer : -1;
            lastGroundHitY = fallbackHit.point.y;
            return true;
        }

        return false;
    }

    private bool IsSlimeIdentity()
    {
        if (monsterIdentity == null)
        {
            return false;
        }

        switch (monsterIdentity.species)
        {
            case MonsterSpecies.BlueSlime:
            case MonsterSpecies.GreenSlime:
            case MonsterSpecies.LavaSlime:
            case MonsterSpecies.PoisonSlime:
            case MonsterSpecies.RainbowSlime:
                return true;
            default:
                return false;
        }
    }

    private void LogRuntimeAttackConfigOnce()
    {
        if (!debugSlimeAttackLogs || hasLoggedRuntimeAttackConfig || !IsSlimeIdentity())
        {
            return;
        }

        ResolveMeleeHitSources();
        Bounds colliderBounds = meleeEnemyCollider != null ? meleeEnemyCollider.bounds : default;
        float bodyRadius = meleeEnemyCollider is SphereCollider sphereCollider ? sphereCollider.radius : -1f;
        float speedValue = combatStats != null ? combatStats.speed : 0f;
        float physicalAttack = combatStats != null ? combatStats.physicalAttack : 0f;
        float specialAttack = combatStats != null ? combatStats.specialAttack : 0f;
        float attackWindup = slimeAnimation != null ? slimeAnimation.AttackWindup : 0f;
        float attackRecovery = slimeAnimation != null ? slimeAnimation.AttackRecovery : 0f;
        float attackAnimationDuration = slimeAnimation != null ? slimeAnimation.AttackAnimationDuration : 0f;
        string attackClipName = slimeAnimation != null ? slimeAnimation.SelectedAttackClipName : "None";
        float attackClipLength = slimeAnimation != null ? slimeAnimation.SelectedAttackClipLength : 0f;
        float animatorSpeed = slimeAnimation != null ? slimeAnimation.AnimatorPlaybackSpeed : 1f;
        string sourceName = name.Replace("(Clone)", string.Empty).Trim();
        Debug.Log(
            $"[SlimeRuntimeComparison] name={name} prefab/source name={sourceName} monsterKind={monsterIdentity.species} rank={monsterIdentity.rank} attackStyle={attackStyle} attackRange={attackRange:F2} attackHitRange={attackHitRange:F2} attackCooldown={attackCooldown:F2} stoppingDistance={stopDistance:F2} verticalTolerance={maxVerticalAttackDifference:F2} requireGrounded={requireGroundedToAttack} groundGrace={groundedAttackGraceTime:F2} groundProbeDistance={groundedProbeDistance:F2} attackWindup={attackWindup:F2} attackRecovery={attackRecovery:F2} animationDuration={attackAnimationDuration:F2} targetRefreshInterval={targetResolveRetryInterval:F2} speed={speedValue:F2} physicalAttack={physicalAttack:F2} specialAttack={specialAttack:F2} bodyColliderRadius={(bodyRadius >= 0f ? bodyRadius.ToString("F2") : "n/a")} bodyColliderBounds={colliderBounds.size} rigidbodyMass={(rb != null ? rb.mass.ToString("F2") : "n/a")} animatorSpeed={animatorSpeed:F2} selectedAttackClipName={attackClipName} selectedAttackClipLength={attackClipLength:F2} allowAttackForwardLeap={(slimeAnimation != null && slimeAnimation.AllowAttackForwardLeap)} allowAttackVerticalLeap={(slimeAnimation != null && slimeAnimation.AllowAttackVerticalLeap)} maxAttackLeapDistance={(slimeAnimation != null ? slimeAnimation.MaxAttackLeapDistance.ToString("F2") : "0.00")}",
            this);
        hasLoggedRuntimeAttackConfig = true;
    }

    private void LogSlimeAttackCheck(
        EnemyAttackRuntimeState newState,
        float centerDistance,
        float edgeDistance,
        float verticalDifference,
        bool isGrounded,
        bool canBeginAttack,
        string failureReason)
    {
        if (!debugSlimeAttackLogs || !IsSlimeIdentity())
        {
            return;
        }

        Debug.Log(
            $"[SlimeAttackCheck] name={name} kind={monsterIdentity.species} rank={monsterIdentity.rank} attackStyle={attackStyle} target={(playerTarget != null ? playerTarget.name : "null")} centerDistance={centerDistance:F2} edgeDistance={edgeDistance:F2} attackRange={attackRange:F2} attackHitRange={attackHitRange:F2} verticalDifference={verticalDifference:F2} isGrounded={isGrounded} isAttacking={attackInProgress || (slimeAnimation != null && slimeAnimation.IsAttacking)} cooldownRemaining={Mathf.Max(0f, nextAttackTime - Time.time):F2} canBeginAttack={canBeginAttack} failureReason={(string.IsNullOrEmpty(failureReason) ? newState.ToString() : failureReason)}",
            this);
    }

    private void LogSlimeAttackLifecycle(string stage, Transform target, string detail)
    {
        if (!debugSlimeAttackLogs || !IsSlimeIdentity())
        {
            return;
        }

        Debug.Log(
            $"[SlimeAttackLifecycle] stage={stage} name={name} kind={monsterIdentity.species} rank={monsterIdentity.rank} attackStyle={attackStyle} target={(target != null ? target.name : "null")} detail={detail}",
            this);
    }

    private BattleDamageType ResolvePrimaryDamageType()
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss && activeBossAttackKind == BossAttackKind.Melee)
        {
            return BattleDamageType.Physical;
        }

        return attackStyle == MonsterAttackStyle.Melee ? BattleDamageType.Physical : BattleDamageType.Special;
    }

    private float ResolveCurrentAttackDamage(BattleDamageType damageType)
    {
        float attackPower = BattleStatUtility.ResolveAttackPower(gameObject, damageType, attackDamage);
        float damage = attackPower * Mathf.Max(0.01f, outgoingDamageMultiplier) * Mathf.Max(0f, ResolveOutgoingDamageMultiplier());
        return BattleStatUtility.ApplyCriticalDamage(gameObject, damage, out _);
    }

    private void FireProjectileAt(Transform hitTarget)
    {
        FireProjectileAt(hitTarget, 0, 1);
    }

    private void FireProjectileAt(Transform hitTarget, int volleyIndex, int volleyCount)
    {
        if (hitTarget == null)
        {
            return;
        }

        Vector3 spawnPosition = ResolveProjectileSpawnPosition(hitTarget);
        Vector3 targetPoint = attackStyle == MonsterAttackStyle.ElementalBoss
            ? ResolveBossProjectileVolleyTargetPoint(hitTarget, spawnPosition, volleyIndex, volleyCount)
            : ResolveBossProjectileTargetPoint(hitTarget);
        Vector3 direction = targetPoint - spawnPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < MovementZeroEpsilon)
        {
            direction = transform.forward;
        }

        if (debugAttackDiagnostics || debugLog)
        {
            float distanceToTarget = Vector3.Distance(transform.position, hitTarget.position);
            Debug.Log($"[BossAcidAttack] FireProjectileAt boss={name} target={hitTarget.name} distance={distanceToTarget:F2} spawnOffset={ProjectileSpawnForwardOffset:F2}", this);
        }

        BattleDamageType damageType = ResolvePrimaryDamageType();

        // 鎶曞皠鐗╁嚭鐢熺偣缁存寔鍦ㄨ鑹插墠涓婃柟锛岄伩鍏嶅拰鏈綋纰版挒浣撻噸鍙犮€?
        GameObject projectile = CreateProjectileObject();
        if (projectile == null)
        {
            Debug.LogWarning($"[BossAcidAttack] Failed to create projectile for boss '{name}'.", this);
            return;
        }

        projectile.name = attackStyle == MonsterAttackStyle.ElementalBoss ? "Boss Acid Projectile" : "Monster Projectile";
        projectile.transform.position = spawnPosition;
        projectile.transform.localScale = Vector3.one * (attackStyle == MonsterAttackStyle.ElementalBoss ? BossProjectileScale : NormalProjectileScale);

        if (debugAttackDiagnostics || debugLog)
        {
            Debug.Log($"[BossAcidAttack] instantiated projectile={projectile.name} prefabSource={(ResolveProjectilePrefab() != null ? ResolveProjectilePrefab().name : "runtime sphere")} position={projectile.transform.position}", this);
        }

        Collider projectileCollider = projectile.GetComponent<Collider>();
        if (projectileCollider == null)
        {
            projectileCollider = projectile.AddComponent<SphereCollider>();
        }
        projectileCollider.isTrigger = true;

        Rigidbody projectileBody = projectile.GetComponent<Rigidbody>();
        if (projectileBody == null)
        {
            projectileBody = projectile.AddComponent<Rigidbody>();
        }
        projectileBody.isKinematic = true;
        projectileBody.useGravity = false;

        MonsterProjectile monsterProjectile = projectile.GetComponent<MonsterProjectile>();
        if (monsterProjectile == null)
        {
            monsterProjectile = projectile.AddComponent<MonsterProjectile>();
        }
        monsterProjectile.Launch(direction, projectileSpeed, ResolveCurrentAttackDamage(damageType), damageType, gameObject);
        if (attackStyle == MonsterAttackStyle.ElementalBoss)
        {
            PlaySkillAudio(projectileFireAudioClip, projectileFireAudioVolume);
        }

        if (attackStyle == MonsterAttackStyle.ElementalBoss && useArcTrajectory)
        {
            monsterProjectile.ConfigureArcTrajectory(spawnPosition, targetPoint, Mathf.Max(0.1f, arcHeight), Mathf.Max(0.1f, arcTravelTime));
        }

        Renderer renderer = projectile.GetComponentInChildren<Renderer>();
        if (renderer != null && attackStyle != MonsterAttackStyle.ElementalBoss)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            renderer.material.color = ResolveProjectileColor(damageType);
        }
    }

    private Vector3 ResolveProjectileSpawnPosition(Transform hitTarget)
    {
        if (attackStyle == MonsterAttackStyle.ElementalBoss && bossProjectileSpawnPoint != null)
        {
            return bossProjectileSpawnPoint.position;
        }

        Vector3 direction = hitTarget != null ? (hitTarget.position - transform.position) : transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < MovementZeroEpsilon)
        {
            direction = transform.forward;
        }

        if (attackStyle == MonsterAttackStyle.ElementalBoss)
        {
            return transform.position + Vector3.up * bossProjectileSpawnHeight + direction.normalized * bossProjectileSpawnForwardOffset;
        }

        return transform.position + Vector3.up * ProjectileSpawnHeightOffset + direction.normalized * ProjectileSpawnForwardOffset;
    }

    private Vector3 ResolveBossProjectileTargetPoint(Transform hitTarget)
    {
        if (hitTarget == null)
        {
            return transform.position + transform.forward * Mathf.Max(1f, bossRangedMinRange);
        }

        Vector3 predictedPosition = hitTarget.position;
        Rigidbody targetBody = hitTarget.GetComponentInParent<Rigidbody>();
        if (targetBody != null)
        {
            predictedPosition += targetBody.linearVelocity * Mathf.Max(0f, targetPredictionTime);
        }
        else
        {
            CharacterController controller = hitTarget.GetComponentInParent<CharacterController>();
            if (controller != null)
            {
                predictedPosition += controller.velocity * Mathf.Max(0f, targetPredictionTime);
            }
        }

        Collider targetCollider = ResolvePlayerCollider(hitTarget);
        if (targetCollider != null)
        {
            predictedPosition.y = targetCollider.bounds.min.y + Mathf.Min(0.2f, targetCollider.bounds.extents.y);
        }

        return predictedPosition;
    }

    private int ResolveBossSlimeProjectileCount()
    {
        int safeMin = Mathf.Max(1, minSlimeProjectileCount);
        int safeMax = Mathf.Max(safeMin, maxSlimeProjectileCount);
        return Random.Range(safeMin, safeMax + 1);
    }

    private Vector3 ResolveBossProjectileVolleyTargetPoint(Transform hitTarget, Vector3 spawnPosition, int volleyIndex, int volleyCount)
    {
        Vector3 targetPoint = ResolveBossProjectileTargetPoint(hitTarget);
        if (volleyIndex <= 0 || volleyCount <= 1)
        {
            return targetPoint;
        }

        Vector3 horizontalDirection = targetPoint - spawnPosition;
        horizontalDirection.y = 0f;
        if (horizontalDirection.sqrMagnitude < MovementZeroEpsilon)
        {
            horizontalDirection = transform.forward;
        }

        horizontalDirection.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, horizontalDirection).normalized;
        if (right.sqrMagnitude < MovementZeroEpsilon)
        {
            right = transform.right;
        }

        float distance = Mathf.Max(1f, Vector3.Distance(
            new Vector3(spawnPosition.x, 0f, spawnPosition.z),
            new Vector3(targetPoint.x, 0f, targetPoint.z)));
        float horizontalScatter = Mathf.Tan(Mathf.Deg2Rad * Mathf.Max(0f, slimeProjectileHorizontalScatterAngle)) * distance;
        float verticalScatter = Mathf.Tan(Mathf.Deg2Rad * Mathf.Max(0f, slimeProjectileVerticalScatterAngle)) * distance;
        float horizontalOffset = Random.Range(-horizontalScatter, horizontalScatter);
        float verticalOffset = Random.Range(-verticalScatter, verticalScatter);
        return targetPoint + right * horizontalOffset + Vector3.up * verticalOffset;
    }

    private void LogBossSlimeProjectileVolleyStarted(int shotCount, Transform target)
    {
        if (!debugBossSlimeProjectile)
        {
            return;
        }

        int safeMin = Mathf.Max(1, minSlimeProjectileCount);
        int safeMax = Mathf.Max(safeMin, maxSlimeProjectileCount);
        Debug.Log(
            "[BossSlimeProjectileTrace] " +
            "event=VolleyStarted " +
            "count=" + shotCount +
            " min=" + safeMin +
            " max=" + safeMax +
            " interval=" + Mathf.Max(0f, slimeProjectileInterval).ToString("F2") +
            " target=" + (target != null ? target.name : "null"),
            this);
    }

    private int ResolveBossFacingSign(Transform target)
    {
        if (target == null)
        {
            return 1;
        }

        return target.position.x >= transform.position.x ? 1 : -1;
    }

    private void PlayBossRangedMuzzleParticle()
    {
        if (bossRangedMuzzleParticle == null)
        {
            return;
        }

        bossRangedMuzzleParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        bossRangedMuzzleParticle.Play(true);
    }

    private GameObject CreateProjectileObject()
    {
        GameObject prefab = ResolveProjectilePrefab();
        if (prefab != null)
        {
            return Instantiate(prefab);
        }

        return GameObject.CreatePrimitive(PrimitiveType.Sphere);
    }

    private GameObject ResolveProjectilePrefab()
    {
        if (projectilePrefab != null)
        {
            return projectilePrefab;
        }

        if (cachedDefaultProjectilePrefab == null)
        {
            cachedDefaultProjectilePrefab = Resources.Load<GameObject>(DefaultProjectilePrefabResourcePath);
            if (cachedDefaultProjectilePrefab == null)
            {
                Debug.LogWarning($"[BossAcidAttack] Missing fallback projectile prefab at Resources/{DefaultProjectilePrefabResourcePath}.", this);
            }
        }

        return cachedDefaultProjectilePrefab;
    }

    private Color ResolveProjectileColor(BattleDamageType damageType)
    {
        if (attackStyle != MonsterAttackStyle.ElementalBoss)
        {
            return new Color(0.25f, 0.6f, 1f, 1f);
        }

        Color[] colors =
        {
            new Color(1f, 0.25f, 0.05f, 1f),
            new Color(0.2f, 0.65f, 1f, 1f),
            new Color(1f, 0.95f, 0.25f, 1f),
            new Color(0.55f, 1f, 0.75f, 1f),
            new Color(0.6f, 0.15f, 0.9f, 1f)
        };

        return damageType == BattleDamageType.Special ? colors[Random.Range(0, colors.Length)] : Color.white;
    }
}
