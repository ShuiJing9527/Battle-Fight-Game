using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Spine.Unity;

public class Player2PrototypeController : MonoBehaviour
{
    private const string LegacyRDisabledWarning = "[Player2PrototypeController] Legacy R fallback is disabled. Please use Player2Skill_R_DivineStarRain.";

    [Header("E - 星痕瞬移 / 基础")]
    [SerializeField] private PlayerSkillBase qSkill;
    [SerializeField] private PlayerSkillBase wSkill;
    [SerializeField] private PlayerSkillBase eSkill;
    [SerializeField] private PlayerSkillBase rSkill;

    [Header("E - 星痕瞬移 / 残影特效")]
    public PlayerSkillCooldownManager cooldownManager;

    [Header("R - 神眷剑涡 / 基础")]
    [HideInInspector] public float moveSpeed = 5f;
    public float dashDistance = 4f;
    public float dashDuration = 0.15f;
    [SerializeField] private bool lockCharacterRotation = true;

    [HideInInspector]
    [SerializeField] private Transform visualRoot;
    [HideInInspector]
    [SerializeField] private float visualFloatHeight = 0.6f;
    [HideInInspector]
    [SerializeField] private bool keepColliderOnGround = true;
    [HideInInspector]
    [SerializeField] private Transform groundAnchor;
    [HideInInspector]
    [SerializeField] private Transform footAnchor;

    [HideInInspector]
    [SerializeField] private bool enableSpawnUnstuck = false;
    [HideInInspector]
    [SerializeField] private float spawnUnstuckDelay = 0.1f;
    [HideInInspector]
    [SerializeField] private float spawnUnstuckSearchStep = 0.5f;
    [HideInInspector]
    [SerializeField] private int spawnUnstuckSearchRings = 5;
    [HideInInspector]
    [SerializeField] private LayerMask spawnBlockerLayers = ~0;

    [HideInInspector]
    [Header("R - 神眷剑涡 / 视觉")]
    public float qDelay = 0.35f;
    [HideInInspector]
    public float qSwordSpeed = 14f;

    [HideInInspector]
    [Header("R - 神眷剑涡 / 万剑漩涡")]
    [InspectorName("W Duration")]
    public float wDuration = 1.5f;
    [HideInInspector]
    [InspectorName("W Damage Reduction")]
    public float wDamageReduction = 0.4f;
    [HideInInspector] public int maxStandbySwords = 3;

    [HideInInspector]
    [Header("R - 神眷剑涡 / 漩涡伤害")]
    [InspectorName("W Damage Reduction Per Sword")]
    public float wDamageReductionPerSword = 0.03f;
    [HideInInspector]
    [InspectorName("W Max Damage Reduction")]
    public float wMaxDamageReduction = 0.8f;
    [HideInInspector]
    [InspectorName("W Counter Damage Ratio")]
    public float wCounterDamageRatio = 0.5f;

    [HideInInspector]
    [Header("R - 神眷剑涡 / 备用星雨")]
    public float eRailDuration = 0.6f;

    [HideInInspector]
    [Header("R - 神眷剑涡 / 漩涡拖尾")]
    [InspectorName("E Enable Afterimage Shader")]
    public bool eEnableAfterimageShader = true;
    [HideInInspector] [SerializeField] private SpriteRenderer eAfterimageSourceSpriteRenderer;
    public Renderer eAfterimageSourceRenderer;
    public Material eAfterimageMaterial;
    [InspectorName("E Afterimage Count")]
    public int eAfterimageCount = 12;
    [HideInInspector]
    [InspectorName("E Afterimage Duration")]
    public float eAfterimageDuration = 0.45f;
    [HideInInspector]
    [InspectorName("E Afterimage Alpha")]
    public float eAfterimageAlpha = 0.35f;
    [HideInInspector]
    [InspectorName("E Afterimage Spawn Interval")]
    public float eAfterimageSpawnInterval = 0.03f;
    [HideInInspector]
    [InspectorName("E Afterimage Scale")]
    public Vector3 eAfterimageScale = Vector3.one;
    [HideInInspector]
    [InspectorName("E Afterimage Tint")]
    public Color eAfterimageTint = new Color(0.6f, 0.85f, 1f, 0.45f);
    [HideInInspector]
    [InspectorName("E Afterimage Sorting Order Offset")]
    public int eAfterimageSortingOrderOffset = 5;
    [HideInInspector]
    [InspectorName("E Afterimage Debug Log")]
    public bool eAfterimageDebugLog = false;
    [HideInInspector]
    [InspectorName("E Afterimage Use Rainbow")]
    [SerializeField] public bool eAfterimageUseRainbow = true;
    [InspectorName("E Afterimage Invert Color Order")]
    [SerializeField] public bool eAfterimageInvertColorOrder = true;
    [InspectorName("E Afterimage Fade By Age Index")]
    [SerializeField] public bool eAfterimageFadeByAgeIndex = true;
    [InspectorName("E Afterimage Oldest Alpha Multiplier")]
    [SerializeField] public float eAfterimageOldestAlphaMultiplier = 0.25f;
    [InspectorName("E Afterimage Fade By Distance To End")]
    [SerializeField] public bool eAfterimageFadeByDistanceToEnd = true;
    [InspectorName("E Afterimage Far Alpha Multiplier")]
    [SerializeField] public float eAfterimageFarAlphaMultiplier = 0.12f;
    [InspectorName("E Afterimage Rainbow Hue Speed")]
    [SerializeField] public float eAfterimageRainbowHueSpeed = 0.04f;
    [InspectorName("E Afterimage Rainbow Saturation")]
    [SerializeField] public float eAfterimageRainbowSaturation = 0.45f;
    [InspectorName("E Afterimage Rainbow Value")]
    [SerializeField] public float eAfterimageRainbowValue = 1f;
    [InspectorName("E Afterimage Use Distance Sampling")]
    [SerializeField] private bool eAfterimageUseDistanceSampling = true;
    [InspectorName("E Afterimage Use Actual Move Direction")]
    [SerializeField] private bool eAfterimageUseActualMoveDirection = true;
    [InspectorName("E Afterimage Invert Move Direction")]
    [SerializeField] private bool eAfterimageInvertMoveDirection = false;
    [InspectorName("E Afterimage Spacing")]
    [SerializeField] private float eAfterimageSpacing = 0.06f;
    [InspectorName("E Afterimage Max Per Dash")]
    [SerializeField] private int eAfterimageMaxPerDash = 24;
    [SerializeField] private bool eAfterimageUseManualTrailLayout = false;
    [SerializeField] private bool eAfterimageInvertTrailDirection = false;
    [SerializeField] private float eAfterimageNearCharacterOffset = 0.03f;
    [SerializeField] private float eAfterimageTrailLength = 2.2f;
    [SerializeField] private float eAfterimageTrailSideOffset = 0f;
    [SerializeField] private bool eAfterimageUsePathSamples = false;
    [SerializeField] private float eAfterimagePathSpawnDelay = 0f;
    [SerializeField] private float eAfterimagePathForwardBias = 0f;
    [SerializeField] private bool eAfterimageSpawnDuringDash = false;
    [SerializeField] private bool eEnableStarTrail = false;
    [SerializeField] private Material eStarTrailMaterial;
    [SerializeField] private Gradient eStarTrailGradient = CreateDefaultStarTrailGradient();
    [SerializeField] private bool eStarTrailReverseGradient = false;
    [SerializeField] private float eStarTrailTime = 0.6f;
    [SerializeField] private float eStarTrailStartWidth = 0.45f;
    [SerializeField] private float eStarTrailEndWidth = 0.02f;
    [SerializeField] private float eStarTrailMinVertexDistance = 0.01f;
    [SerializeField] private Vector3 eStarTrailLocalOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private bool eEnableDashCoreGlow = false;
    [SerializeField] private GameObject eDashCoreGlowPrefab;
    [SerializeField] private Material eDashCoreGlowMaterial;
    [SerializeField] private Color eDashCoreGlowColor = new Color(0.7f, 0.9f, 1f, 0.8f);
    [SerializeField] private float eDashCoreGlowScale = 0.8f;
    [SerializeField] private float eDashCoreGlowDuration = 0.35f;
    [SerializeField] private Vector3 eDashCoreGlowOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private bool eEnableStarTrailParticles = false;
    [SerializeField] private ParticleSystem eStarTrailParticlePrefab;
    private bool eEnableTrailRenderer = false;
    private Material eTrailMaterial;
    private float eTrailTime = 0.35f;
    private float eTrailStartWidth = 0.45f;
    private float eTrailEndWidth = 0f;
    private float eTrailMinVertexDistance = 0.02f;
    private Color eTrailStartColor = new Color(0.6f, 0.85f, 1f, 0.7f);
    private Color eTrailEndColor = new Color(0.6f, 0.85f, 1f, 0f);
    private int eTrailSortingOrderOffset = 5;

    [FormerlySerializedAs("eEnableAfterimage")]
    [HideInInspector] public bool eEnableAfterimageLegacy = false;
    [FormerlySerializedAs("eAfterimageCount")]
    public int eAfterimageCountLegacy = 4;
    [FormerlySerializedAs("eAfterimageDuration")]
    public float eAfterimageDurationLegacy = 0.35f;
    [FormerlySerializedAs("eAfterimageAlpha")]
    public float eAfterimageAlphaLegacy = 0.45f;
    [FormerlySerializedAs("eAfterimageScale")]
    public Vector3 eAfterimageScaleLegacy = new Vector3(1f, 1f, 1f);
    [FormerlySerializedAs("eAfterimageTint")]
    public Color eAfterimageTintLegacy = new Color(0.6f, 0.9f, 1f, 0.45f);
    private TrailRenderer eDashTrailRenderer;
    private GameObject eDashTrailHost;
    private GameObject eDashCoreGlowInstance;
    private Renderer eDashCoreGlowRenderer;
    private ParticleSystem eDashStarTrailParticlesInstance;
    private Material eRuntimeStarTrailMaterial;
    private Material eRuntimeDashCoreGlowMaterial;

    [HideInInspector] public bool eEnableStarFall = false;
    public int eStarFallBladeCount = 7;
    public float eStarFallRadius = 2.5f;
    public float eStarFallSpawnHeight = 4f;
    public float eStarFallFallSpeed = 10f;
    public float eStarFallRandomDelay = 0.08f;
    public float eStarFallDamageRadius = 0.8f;
    public float eStarFallDamageMultiplier = 0.5f;
    public bool eEnableStarFallDamage = false;
    public bool eStarFallUseForcedVisualRotation = true;
    public Vector3 eStarFallForcedVisualEuler = new Vector3(0f, 0f, 90f);
    public Vector3 eStarFallVisualEulerOffset = Vector3.zero;
    public bool eStarFallUseDashPath = true;
    public float eStarFallPathJitter = 0.35f;
    public float eStarFallSequentialDelay = 0.06f;

    [Header("R - 神眷剑涡 / 中心气场")]
    [InspectorName("Current Sword Energy")]
    public int currentSwordEnergy = 0;

    [HideInInspector]
    [Header("R - 神眷剑涡 / 预制体")]
    [FormerlySerializedAs("swordEnergy")]
    [InspectorName("R Base Sword Count")]
    public int rBaseSwordCount = 1;

    [SerializeField] private bool debugSkillCooldownFlow = true;

    [Header("R - 神眷剑涡 / 收场")]
    public GameObject sharedSkillEffectPrefab;
    [HideInInspector]
    public GameObject qSkillEffectPrefab;
    [HideInInspector]
    public GameObject wSkillEffectPrefab;
    public GameObject eSkillEffectPrefab;
    [HideInInspector] public GameObject rSkillEffectPrefab;
    public GameObject standbySkillEffectPrefab;

    [Header("Use Raw Prefab Rotation For Skill Effects")]
    public bool useRawPrefabRotationForSkillEffects = true;
    public Vector3 skillEffectPrefabBaseRotation = Vector3.zero;
    public float skillEffectPrefabScaleMultiplier = 1f;
    public Vector3 sharedEffectScale = new Vector3(1f, 1f, 1f);
    public float sharedEffectRotationZ = 0f;

    [HideInInspector]
    [Header("Q Effect Scale")]
    public Vector3 qEffectScale = new Vector3(0.25f, 0.25f, 0.25f);
    [HideInInspector]
    public float qEffectRotationZ = 0f;
    [HideInInspector]
    public Vector3 qEffectOffset = Vector3.zero;
    [HideInInspector]
    public Vector3 qEffectPlaneScale = new Vector3(0.25f, 0.25f, 1f);
    [HideInInspector]
    public float qEffectYawOffset = 0f;
    [HideInInspector]
    public float qEffectVisualPitch = 0f;
    [HideInInspector]
    public float qEffectVisualYaw = 0f;
    [HideInInspector]
    public float qEffectVisualRoll = 0f;
    [HideInInspector]
    public bool qEffectInvertForward = false;

    [HideInInspector]
    [Header("W Effect Scale")]
    [InspectorName("W Effect Scale")]
    public Vector3 wEffectScale = new Vector3(0.3f, 0.3f, 0.3f);
    [HideInInspector] public float wEffectRotationZ = 0f;
    public Vector3 wEffectOffset = Vector3.zero;
    public Vector3 wEffectPlaneScale = new Vector3(0.25f, 0.25f, 0.25f);

    [HideInInspector]
    [Header("W Effect Scale Multiplier")]
    [InspectorName("W Effect Scale Multiplier")]
    public float wEffectScaleMultiplier = 1f;
    [HideInInspector] public bool wEffectVerticalRotation = true;
    public Vector3 wEffectSpinAxis = Vector3.up;
    public float wEffectVisualPitch = 0f;
    public float wEffectVisualYaw = 0f;
    public float wEffectVisualRoll = 0f;
    public int wSwordCount = 3;

    [HideInInspector]
    [Header("Base W Sword Count")]
    [InspectorName("Base W Sword Count")]
    public int baseWSwordCount = 3;
    [HideInInspector]
    [InspectorName("Use Sword Energy For W")]
    public bool useSwordEnergyForW = true;
    [HideInInspector]
    [InspectorName("Max W Sword Count")]
    public int maxWSwordCount = 15;
    [HideInInspector]
    [InspectorName("W Effect Orbit Radius")]
    public float wEffectOrbitRadius = 1.2f;
    [HideInInspector]
    [InspectorName("W Effect Height")]
    public float wEffectHeight = 1.1f;
    [HideInInspector]
    [InspectorName("W Effect Orbit Speed")]
    public float wEffectOrbitSpeed = 80f;
    [HideInInspector]
    [InspectorName("W Sword Orbit Yaw Offset")]
    public float wSwordOrbitYawOffset = 90f;
    [HideInInspector] public bool wEffectFaceCamera = true;
    [FormerlySerializedAs("wEffectSpinSpeed")]
    public float wEffectSelfSpinSpeed = 0f;

    [HideInInspector]
    [Header("W Duration Per Sword Energy")]
    [InspectorName("W Duration Per Sword Energy")]
    public float wDurationPerSwordEnergy = 0f;
    [HideInInspector]
    [InspectorName("W Max Duration Bonus")]
    public float wMaxDurationBonus = 0f;
    [HideInInspector]
    [InspectorName("W Orbit Speed Per Sword Energy")]
    public float wOrbitSpeedPerSwordEnergy = 0f;
    [HideInInspector]
    [InspectorName("W Max Orbit Speed Bonus")]
    public float wMaxOrbitSpeedBonus = 0f;
    [HideInInspector]
    [InspectorName("W Radius Per Sword Energy")]
    public float wRadiusPerSwordEnergy = 0f;
    [HideInInspector]
    [InspectorName("W Max Radius Bonus")]
    public float wMaxRadiusBonus = 0f;

    [HideInInspector] public float wOrbitRadiusMin = 0.9f;
    public float wOrbitRadiusMax = 1.8f;
    public float wHeightMin = 0.2f;
    public float wHeightMax = 1.2f;
    public float wOrbitSpeedMin = 60f;
    public float wOrbitSpeedMax = 120f;
    public float wBobAmplitudeMin = 0.05f;
    public float wBobAmplitudeMax = 0.25f;
    public float wBobFrequencyMin = 0.8f;
    public float wBobFrequencyMax = 2.0f;
    public float wSwingAngleMin = 3f;
    public float wSwingAngleMax = 12f;
    public float wRadiusJitter = 0.12f;
    public float wAngularJitter = 10f;
    public bool wClockwise = true;
    public bool wFaceOrbitDirection = true;
    public float wOrbitDirectionYawOffset = 0f;
    public float wOrbitDirectionPitchOffset = 0f;
    public float wOrbitDirectionRollOffset = 0f;
    public bool wKeepSwordVisibleToCamera = true;

    [Header("R Effect Scale")]
    [HideInInspector] public Vector3 eEffectScale = new Vector3(0.35f, 0.35f, 0.35f);
    public float eEffectRotationZ = -90f;
    public Vector3 eEffectOffset = Vector3.zero;
    public Vector3 eEffectPlaneScale = new Vector3(0.35f, 0.35f, 1f);
    public float eEffectYawOffset = 0f;
    public float eEffectVisualPitch = 0f;
    public float eEffectVisualYaw = 0f;
    public float eEffectVisualRoll = 0f;

    [HideInInspector]
    [Header("R Effect Scale")]
    [InspectorName("R Effect Scale")]
    public Vector3 rEffectScale = new Vector3(0.3f, 0.3f, 0.3f);
    public float rEffectRotationZ = 0f;
    public Vector3 rEffectOffset = Vector3.zero;
    public Vector3 rEffectPlaneScale = new Vector3(0.3f, 0.3f, 1f);
    public float rEffectYawOffset = 0f;
    [InspectorName("R Effect Visual Pitch")]
    public float rEffectVisualPitch = 0f;
    [InspectorName("R Effect Visual Yaw")]
    public float rEffectVisualYaw = 0f;
    [InspectorName("R Effect Visual Roll")]
    public float rEffectVisualRoll = 0f;
    public bool rEffectInvertForward = false;
    [Header("R Swarm Duration")]
    [InspectorName("R Swarm Duration")]
    public float rSwarmDuration = 2.0f;
    [InspectorName("R Swarm Radius Min")]
    public float rSwarmRadiusMin = 0.8f;
    [InspectorName("R Swarm Radius Max")]
    public float rSwarmRadiusMax = 3.2f;
    [InspectorName("R Swarm Height Min")]
    public float rSwarmHeightMin = 0.4f;
    [InspectorName("R Swarm Height Max")]
    public float rSwarmHeightMax = 3.0f;
    [InspectorName("R Swarm Speed Min")]
    public float rSwarmSpeedMin = 120f;
    [InspectorName("R Swarm Speed Max")]
    public float rSwarmSpeedMax = 300f;
    [InspectorName("R Swarm Bob Amplitude Min")]
    public float rSwarmBobAmplitudeMin = 0.05f;
    [InspectorName("R Swarm Bob Amplitude Max")]
    public float rSwarmBobAmplitudeMax = 0.35f;
    [InspectorName("R Swarm Bob Frequency Min")]
    public float rSwarmBobFrequencyMin = 0.8f;
    [InspectorName("R Swarm Bob Frequency Max")]
    public float rSwarmBobFrequencyMax = 2.5f;
    [InspectorName("R Swarm Radius Jitter")]
    public float rSwarmRadiusJitter = 0.25f;
    [InspectorName("R Swarm Clockwise")]
    public bool rSwarmClockwise = true;
    [InspectorName("R Swarm Forward Offset")]
    public float rSwarmForwardOffset = 2.0f;
    [Header("R Swarm Yaw Offset")]
    [InspectorName("R Swarm Yaw Offset")]
    public float rSwarmYawOffset = 0f;
    [InspectorName("R Billboard Like Player")]
    public bool rBillboardLikePlayer = true;
    [InspectorName("R Render Camera")]
    public Camera rRenderCamera;
    [InspectorName("R Auto Resolve Render Camera")]
    public bool rAutoResolveRenderCamera = true;
    [InspectorName("R Swarm Use Camera Forward")]
    public bool rSwarmUseCameraForward = true;
    [InspectorName("R Swarm Center On Player")]
    public bool rSwarmCenterOnPlayer = false;
    [InspectorName("R Apply Effect Offset To Swarm Center")]
    public bool rApplyEffectOffsetToSwarmCenter = false;
    public bool rUseTangentFacing = true;
    [InspectorName("R Plane Upright Euler")]
    public Vector3 rPlaneUprightEuler = Vector3.zero;
    [InspectorName("R Plane Face Camera Euler")]
    public Vector3 rPlaneFaceCameraEuler = Vector3.zero;
    [Header("R Flip Plane Front Back")]
    [InspectorName("R Flip Plane Front Back")]
    public bool rFlipPlaneFrontBack = true;
    [InspectorName("R Plane Front Back Flip Euler")]
    public Vector3 rPlaneFrontBackFlipEuler = new Vector3(0f, 180f, 0f);
    public Vector3 rInPlaneRotationAxis = new Vector3(0f, 0f, 1f);
    [Header("R Use Player Layer For R")]
    public bool rDebugSwordVelocityFacing = false;
    public float rFacingLookAheadTime = 0.05f;
    public bool rDebugFacingScreenAngle = false;
    [InspectorName("R Use Player Layer For R")]
    public bool rUsePlayerLayerForR = true;
    [InspectorName("R Force Double Sided")]
    public bool rForceDoubleSided = true;
    [Header("R Swarm Damage Radius")]
    public bool rEnableSwordSelfSpin = false;
    public float rSwordSelfSpinMin = 30f;
    public float rSwordSelfSpinMax = 120f;
    public Vector3 rSwordLengthLocalAxis = Vector3.up;
    [Header("R Swarm Damage Radius")]
    [InspectorName("R Swarm Damage Radius")]
    public float rSwarmDamageRadius = 3.0f;
    [InspectorName("R Swarm Damage Interval")]
    public float rSwarmDamageInterval = 0.25f;
    [InspectorName("R Swarm Damage Per Tick")]
    public float rSwarmDamagePerTick = 2.0f;
    [HideInInspector]
    [InspectorName("R Swarm Enemy Layer")]
    public LayerMask rSwarmEnemyLayer = ~0;

    [HideInInspector]
    [Header("R 附加坠落 Spawn Height")]
    [InspectorName("R 附加坠落 Spawn Height")]
    public float rRiseDuration = 0.45f;
    [HideInInspector]
    [InspectorName("R 附加坠落 Spawn Height")]
    public float rRiseHeight = 4f;
    [HideInInspector]
    [InspectorName("R 附加坠落 Spawn Height")]
    public float rStarRainDuration = 1.2f;
    [HideInInspector]
    [InspectorName("R 附加坠落 Spawn Height")]
    public float rStarRainSpawnHeight = 5f;
    [HideInInspector]
    [InspectorName("R 附加坠落 Radius")]
    public float rStarRainRadius = 5f;
    [HideInInspector]
    [InspectorName("R 附加坠落 Fall Speed")]
    public float rStarRainFallSpeed = 10f;
    [HideInInspector]
    [InspectorName("R 附加坠落 Random Delay")]
    public float rStarRainRandomDelay = 0.15f;
    [HideInInspector]
    [InspectorName("R 附加坠落 Damage Radius")]
    public float rStarRainDamageRadius = 1.2f;

    [HideInInspector]
    [InspectorName("R 附加坠落 Start Ratio")]
    public float rStarRainStartRatio = 0.5f;
    [HideInInspector]
    [InspectorName("R 附加坠落 Interval")]
    public float rStarRainInterval = 0.12f;
    [HideInInspector]
    [InspectorName("R 附加坠落 Blades Per Wave")]
    public int rStarRainBladesPerWave = 2;
    [HideInInspector]
    [InspectorName("R 附加坠落 Visual Euler Offset")]
    public Vector3 rStarRainVisualEulerOffset = Vector3.zero;
    [HideInInspector]
    [InspectorName("R 附加坠落 Use Forced Visual Rotation")]
    public bool rStarRainUseForcedVisualRotation = true;
    [HideInInspector]
    [InspectorName("R 附加坠落 Forced Visual Euler")]
    public Vector3 rStarRainForcedVisualEuler = new Vector3(0f, 180f, 0f);
    [HideInInspector]
    [InspectorName("R 附加坠落 Damage Multiplier")]
    public float rStarRainDamageMultiplier = 0.6f;
    [HideInInspector]
    [InspectorName("R 附加坠落 Effect Scale")]
    public Vector3 rStarRainEffectScale = new Vector3(0.3f, 0.3f, 0.3f);
    [HideInInspector]
    [InspectorName("R 附加坠落 Continue After Orbit")]
    public bool rStarRainContinueAfterOrbit = true;
    [HideInInspector]
    [InspectorName("R 附加坠落 Extra Duration After Orbit")]
    public float rStarRainExtraDurationAfterOrbit = 0.6f;
    [HideInInspector]
    [InspectorName("R Orbit Clear When Orbit Ends")]
    public bool rOrbitClearWhenOrbitEnds = true;
    [HideInInspector]
    [InspectorName("R Orbit Fade Out Duration")]
    public float rOrbitFadeOutDuration = 0.15f;

    [Header("Standby Sword Scale")]
    public Vector3 standbySwordScale = new Vector3(0.25f, 0.25f, 0.25f);
    public float standbySwordRotationZ = 0f;
    public Vector3 standbySwordPlaneScale = new Vector3(0.25f, 0.25f, 1f);
    public Vector3 standbySwordOffset = Vector3.zero;
    public float standbySwordVisualPitch = 0f;
    public float standbySwordVisualYaw = 0f;
    public float standbySwordVisualRoll = 0f;
    public float standbySwordSpinSpeed = 120f;

    [Header("Rb")]
    public Rigidbody rb;
    private sealed class SkillEffectRuntime : MonoBehaviour
    {
        public Transform visual;
        public Vector3 baseVisualScale;
        public Quaternion baseVisualRotation = Quaternion.identity;
        public Material[] materialTargets;
        public Color[] materialBaseColors;
        public SpriteRenderer[] spriteTargets;
        public Color[] spriteBaseColors;
    }

    private sealed class RSwarmSwordData
    {
        public GameObject sword;
        public float baseAngle;
        public float radius;
        public float height;
        public float orbitSpeed;
        public float bobAmplitude;
        public float bobFrequency;
        public float phase;
        public float layerOffset;
        public SkillEffectRuntime runtime;
        public Transform visualTransform;
        public Quaternion baseVisibleLocalRotation;
        public float selfSpinSpeed;
        public float selfSpinAngle;
        public Vector3 previousPosition;
        public bool hasPreviousPosition;
        public Vector3 orbitEndPosition;
        public Vector3 riseEndPosition;
        public Vector3 rainStartPosition;
        public Vector3 rainTargetPosition;
        public float rainDelay;
        public float rainFallDuration;
        public bool rainImpactApplied;
    }

    private sealed class RStarRainBladeData
    {
        public GameObject sword;
        public SkillEffectRuntime runtime;
        public Transform visualTransform;
        public Quaternion baseVisibleLocalRotation;
        public Vector3 spawnPosition;
        public Vector3 targetPosition;
        public float delay;
        public float fallDuration;
        public float elapsed;
        public bool impactApplied;
    }

    private sealed class EStarFallBladeData
    {
        public GameObject sword;
        public SkillEffectRuntime runtime;
        public Transform visualTransform;
        public Vector3 spawnPosition;
        public Vector3 targetPosition;
        public float delay;
        public float fallDuration;
        public float elapsed;
        public bool impactApplied;
    }

    private Vector3 lastMoveDir = Vector3.forward;
    private SkeletonAnimation cachedSpineAnimation;
    private int cachedSpineFacingScaleX = 1;
    private int standbySwords;
    private bool isDashing;
    private bool isShielding;
    private bool isWGuardActive;
    private float wOrbitAngle;
    private Coroutine wSkillRoutine;
    private Coroutine rSwarmRoutine;
    private GameObject activeWOrbitVisualRoot;
    private readonly List<GameObject> activeWSwords = new List<GameObject>();
    private GameObject activeRSwarmRoot;
    private readonly List<RSwarmSwordData> activeRSwarmSwords = new List<RSwarmSwordData>();
    private readonly List<RStarRainBladeData> activeRStarRainBlades = new List<RStarRainBladeData>();
    private Camera resolvedRRenderCamera;
    private int currentWSwordCount;
    private float currentWFinalDamageReduction;

    private readonly List<GameObject> standbySwordVisuals = new List<GameObject>();
    private readonly List<GameObject> runtimeSkillVisualRoots = new List<GameObject>();
    private readonly List<EStarFallBladeData> activeEStarFallBlades = new List<EStarFallBladeData>();

    private Quaternion initialRotation;
    private Transform cachedVisualRoot;
    private Vector3 cachedVisualRootBaseLocalPosition;
    private bool cachedVisualRootBaseLocalPositionReady;
    private Coroutine spawnUnstuckRoutine;

    public bool HasActiveRuntimeSkill
    {
        get
        {
            if (wSkillRoutine != null || rSwarmRoutine != null)
            {
                return true;
            }

            if (isDashing || isShielding || isWGuardActive)
            {
                return true;
            }

            if (activeWOrbitVisualRoot != null || activeRSwarmRoot != null)
            {
                return true;
            }

            if (HasAliveObjects(activeWSwords) || HasAliveObjects(standbySwordVisuals) || HasAliveObjects(runtimeSkillVisualRoots))
            {
                return true;
            }

            for (int i = 0; i < activeRSwarmSwords.Count; i++)
            {
                RSwarmSwordData data = activeRSwarmSwords[i];
                if (data != null && data.sword != null)
                {
                    return true;
                }
            }

            for (int i = 0; i < activeRStarRainBlades.Count; i++)
            {
                RStarRainBladeData data = activeRStarRainBlades[i];
                if (data != null && data.sword != null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void Awake()
    {
        initialRotation = transform.rotation;
        EnsureCooldownManager();
        InitializeSkillSlots();
        InitializeEStarTrailDefaults();
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        ResolveRRenderCamera();

        // 鍒濆鍖栧喎鍗翠笌钃濋噺绯荤粺
        // 鍏ㄥ眬鍐峰嵈鍊掕鏃?+ 鑷姩鍥炶摑
        cooldownManager?.TickCooldownAndMana(Time.deltaTime);
    }

    private void Start()
    {
        InitializeSkillSlots();
    }

    public void ClearRuntimeSkillVisualsForSwitch()
    {
        qSkill?.Cleanup();
        wSkill?.Cleanup();
        eSkill?.Cleanup();
        rSkill?.Cleanup();

        if (wSkillRoutine != null)
        {
            StopCoroutine(wSkillRoutine);
            wSkillRoutine = null;
        }

        if (rSwarmRoutine != null)
        {
            StopCoroutine(rSwarmRoutine);
            rSwarmRoutine = null;
        }

        StopAllCoroutines();
        CleanupWVisuals();
        CleanupRSwarmVisuals();
        CleanupEStarTrailVisuals();

        for (int i = 0; i < standbySwordVisuals.Count; i++)
        {
            GameObject standby = standbySwordVisuals[i];
            if (standby != null)
            {
                Destroy(standby);
            }
        }
        standbySwordVisuals.Clear();
        standbySwords = 0;

        for (int i = 0; i < runtimeSkillVisualRoots.Count; i++)
        {
            GameObject root = runtimeSkillVisualRoots[i];
            if (root != null)
            {
                Destroy(root);
            }
        }
        runtimeSkillVisualRoots.Clear();

        isDashing = false;
        isShielding = false;
        isWGuardActive = false;
        currentWSwordCount = 0;
        currentWFinalDamageReduction = 0f;
    }

    private void OnDestroy()
    {
        qSkill?.Cleanup();
        wSkill?.Cleanup();
        eSkill?.Cleanup();
        rSkill?.Cleanup();
        CleanupEStarTrailVisuals();
    }

    private void LateUpdate()
    {
        if (lockCharacterRotation)
        {
            transform.rotation = initialRotation;
        }

        UpdateSpineFacingMirror();
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        // 鍏ㄥ眬鍐峰嵈鍊掕鏃?+ 鑷姩鍥炶摑
        cooldownManager?.TickCooldownAndMana(Time.deltaTime);

        // Q 鎶€鑳?
        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            if (debugSkillCooldownFlow)
            {
                Debug.Log("[SkillCD] Player02 Q pressed", this);
            }

            PlayerSkillHUD skillHud = FindObjectOfType<PlayerSkillHUD>();
            if (skillHud != null && skillHud.IsSkillOnCooldown("Q"))
            {
                if (debugSkillCooldownFlow)
                {
                    Debug.Log("[SkillCD] Player02 Q blocked by HUD cooldown", this);
                }

                return;
            }

            if (!CanCastSkill(0))
            {
                if (debugSkillCooldownFlow)
                {
                    Debug.Log("[SkillCD] Player02 Q blocked by runtime cooldown or MP", this);
                }

                return;
            }

            bool castSucceeded = qSkill != null ? qSkill.Cast() : TryCastQFallback();
            if (debugSkillCooldownFlow)
            {
                Debug.Log($"[SkillCD] Player02 Q cast result = {castSucceeded}", this);
            }

            if (!castSucceeded)
            {
                return;
            }

            bool consumeSucceeded = TryConsumeSkill(0);
            if (debugSkillCooldownFlow)
            {
                Debug.Log($"[SkillCD] Player02 Q consume result = {consumeSucceeded}", this);
            }

            if (!consumeSucceeded)
            {
                return;
            }

            if (skillHud != null)
            {
                if (debugSkillCooldownFlow)
                {
                    Debug.Log("[SkillCD] Player02 Q start HUD cooldown", this);
                }

                skillHud.StartSkillCooldown("Q", ResolveSkillCooldownSeconds(qSkill, "Q"));
            }
        }

        // W 鎶€鑳?
        if (Keyboard.current.wKey.wasPressedThisFrame)
        {
            if (debugSkillCooldownFlow)
            {
                Debug.Log("[SkillCD] Player02 W pressed", this);
            }

            PlayerSkillHUD skillHud = FindObjectOfType<PlayerSkillHUD>();
            if (skillHud != null && skillHud.IsSkillOnCooldown("W"))
            {
                if (debugSkillCooldownFlow)
                {
                    Debug.Log("[SkillCD] Player02 W blocked by HUD cooldown", this);
                }

                return;
            }

            if (!CanCastSkill(1))
            {
                if (debugSkillCooldownFlow)
                {
                    Debug.Log("[SkillCD] Player02 W blocked by runtime cooldown or MP", this);
                }

                return;
            }

            bool castSucceeded = wSkill != null ? wSkill.Cast() : TryCastWFallback();
            if (debugSkillCooldownFlow)
            {
                Debug.Log($"[SkillCD] Player02 W cast result = {castSucceeded}", this);
            }

            if (!castSucceeded)
            {
                return;
            }

            bool consumeSucceeded = TryConsumeSkill(1);
            if (debugSkillCooldownFlow)
            {
                Debug.Log($"[SkillCD] Player02 W consume result = {consumeSucceeded}", this);
            }

            if (!consumeSucceeded)
            {
                return;
            }

            if (skillHud != null)
            {
                if (debugSkillCooldownFlow)
                {
                    Debug.Log("[SkillCD] Player02 W start HUD cooldown", this);
                }

                skillHud.StartSkillCooldown("W", ResolveSkillCooldownSeconds(wSkill, "W"));
            }
        }

        // E 鎶€鑳?
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (debugSkillCooldownFlow)
            {
                Debug.Log("[SkillCD] Player02 E pressed", this);
            }

            PlayerSkillHUD skillHud = FindObjectOfType<PlayerSkillHUD>();
            if (skillHud != null && skillHud.IsSkillOnCooldown("E"))
            {
                if (debugSkillCooldownFlow)
                {
                    Debug.Log("[SkillCD] Player02 E blocked by HUD cooldown", this);
                }

                return;
            }

            if (!CanCastSkill(2))
            {
                if (debugSkillCooldownFlow)
                {
                    Debug.Log("[SkillCD] Player02 E blocked by runtime cooldown or MP", this);
                }

                return;
            }

            bool castSucceeded = eSkill != null ? eSkill.Cast() : TryCastEFallback();
            if (debugSkillCooldownFlow)
            {
                Debug.Log($"[SkillCD] Player02 E cast result = {castSucceeded}", this);
            }

            if (!castSucceeded)
            {
                return;
            }

            bool consumeSucceeded = TryConsumeSkill(2);
            if (debugSkillCooldownFlow)
            {
                Debug.Log($"[SkillCD] Player02 E consume result = {consumeSucceeded}", this);
            }

            if (!consumeSucceeded)
            {
                return;
            }

            if (skillHud != null)
            {
                if (debugSkillCooldownFlow)
                {
                    Debug.Log("[SkillCD] Player02 E start HUD cooldown", this);
                }

                skillHud.StartSkillCooldown("E", ResolveSkillCooldownSeconds(eSkill, "E"));
            }
        }

        // R 鎶€鑳?
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            if (debugSkillCooldownFlow)
            {
                Debug.Log("[SkillCD] Player02 R pressed", this);
            }

            PlayerSkillHUD skillHud = FindObjectOfType<PlayerSkillHUD>();
            if (skillHud != null && skillHud.IsSkillOnCooldown("R"))
            {
                if (debugSkillCooldownFlow)
                {
                    Debug.Log("[SkillCD] Player02 R blocked by HUD cooldown", this);
                }

                return;
            }

            if (!CanCastSkill(3))
            {
                if (debugSkillCooldownFlow)
                {
                    Debug.Log("[SkillCD] Player02 R blocked by runtime cooldown or MP", this);
                }

                return;
            }

            bool castSucceeded = rSkill != null ? rSkill.Cast() : TryCastRFallback();
            if (debugSkillCooldownFlow)
            {
                Debug.Log($"[SkillCD] Player02 R cast result = {castSucceeded}", this);
            }

            if (!castSucceeded)
            {
                return;
            }

            bool consumeSucceeded = TryConsumeSkill(3);
            if (debugSkillCooldownFlow)
            {
                Debug.Log($"[SkillCD] Player02 R consume result = {consumeSucceeded}", this);
            }

            if (!consumeSucceeded)
            {
                return;
            }

            if (skillHud != null)
            {
                if (debugSkillCooldownFlow)
                {
                    Debug.Log("[SkillCD] Player02 R start HUD cooldown", this);
                }

                skillHud.StartSkillCooldown("R", ResolveSkillCooldownSeconds(rSkill, "R"));
            }
        }
    }
    private void InitializeSkillSlots()
    {
        if (qSkill == null) qSkill = GetComponent<Player2Skill_Q_DivineLightSword>();
        if (wSkill == null) wSkill = GetComponent<Player2Skill_W_HolyWheelDeflection>();
        if (eSkill == null) eSkill = GetComponent<Player2Skill_E_CelestialShift>();
        if (rSkill == null) rSkill = GetComponent<Player2Skill_R_DivineStarRain>();

        qSkill?.Initialize(this);
        wSkill?.Initialize(this);
        eSkill?.Initialize(this);
        rSkill?.Initialize(this);
        EnsureCooldownManager();
        SyncPlayer2SkillCooldowns();
        SyncPlayer2SkillManaCosts();
    }

    private void EnsureCooldownManager()
    {
        if (cooldownManager != null)
        {
            return;
        }

        cooldownManager = GetComponent<PlayerSkillCooldownManager>();
        if (cooldownManager == null)
        {
            cooldownManager = GetComponentInChildren<PlayerSkillCooldownManager>(true);
        }

        if (cooldownManager == null)
        {
            cooldownManager = gameObject.AddComponent<PlayerSkillCooldownManager>();
        }
    }

    private void SyncPlayer2SkillManaCosts()
    {
        if (cooldownManager == null || cooldownManager.skillDatas == null || cooldownManager.skillDatas.Length < 4)
        {
            return;
        }

        SkillCostCDData qCost = cooldownManager.skillDatas[0];
        qCost.maxCooldown = ResolveSkillCooldownSeconds(qSkill, "Q");
        qCost.manaCost = ResolveSkillManaCost(qSkill, "Q");
        cooldownManager.skillDatas[0] = qCost;

        SkillCostCDData wCost = cooldownManager.skillDatas[1];
        wCost.maxCooldown = ResolveSkillCooldownSeconds(wSkill, "W");
        wCost.manaCost = ResolveSkillManaCost(wSkill, "W");
        cooldownManager.skillDatas[1] = wCost;

        SkillCostCDData eCost = cooldownManager.skillDatas[2];
        eCost.maxCooldown = ResolveSkillCooldownSeconds(eSkill, "E");
        eCost.manaCost = ResolveSkillManaCost(eSkill, "E");
        cooldownManager.skillDatas[2] = eCost;

        SkillCostCDData rCost = cooldownManager.skillDatas[3];
        rCost.maxCooldown = ResolveSkillCooldownSeconds(rSkill, "R");
        rCost.manaCost = ResolveSkillManaCost(rSkill, "R");
        cooldownManager.skillDatas[3] = rCost;
    }

    private void SyncPlayer2SkillCooldowns()
    {
        if (cooldownManager == null || cooldownManager.skillDatas == null || cooldownManager.skillDatas.Length < 4)
        {
            return;
        }

        SkillCostCDData qData = cooldownManager.skillDatas[0];
        qData.maxCooldown = ResolveSkillCooldownSeconds(qSkill, "Q");
        cooldownManager.skillDatas[0] = qData;

        SkillCostCDData wData = cooldownManager.skillDatas[1];
        wData.maxCooldown = ResolveSkillCooldownSeconds(wSkill, "W");
        cooldownManager.skillDatas[1] = wData;

        SkillCostCDData eData = cooldownManager.skillDatas[2];
        eData.maxCooldown = ResolveSkillCooldownSeconds(eSkill, "E");
        cooldownManager.skillDatas[2] = eData;

        SkillCostCDData rData = cooldownManager.skillDatas[3];
        rData.maxCooldown = ResolveSkillCooldownSeconds(rSkill, "R");
        cooldownManager.skillDatas[3] = rData;
    }

    private static float ResolveSkillCooldownSeconds(PlayerSkillBase skill, string keyLabel)
    {
        if (skill != null && skill.CooldownSeconds > 0f)
        {
            return skill.CooldownSeconds;
        }

        switch (keyLabel)
        {
            case "Q":
                return 0.8f;
            case "W":
                return 6f;
            case "E":
                return 8f;
            case "R":
                return 15f;
            default:
                return 0f;
        }
    }

    private static float ResolveSkillManaCost(PlayerSkillBase skill, string keyLabel)
    {
        if (skill != null && skill.ManaCost >= 0f)
        {
            return skill.ManaCost;
        }

        switch (keyLabel)
        {
            case "Q":
                return 10f;
            case "W":
                return 40f;
            case "E":
                return 20f;
            case "R":
                return 60f;
            default:
                return 0f;
        }
    }

    private void ResolveVisualFloatTargets()
    {
        if (visualRoot == null)
        {
            SkeletonAnimation spineAnimation = ResolveSpineAnimation();
            if (spineAnimation != null)
            {
                visualRoot = spineAnimation.transform;
            }
        }

        if (visualRoot == null)
        {
            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer != null)
            {
                visualRoot = spriteRenderer.transform;
            }
            else
            {
                Renderer renderer = GetComponentInChildren<Renderer>(true);
                if (renderer != null)
                {
                    visualRoot = renderer.transform;
                }
            }
        }

        if (groundAnchor == null)
        {
            groundAnchor = transform;
        }

        if (footAnchor == null)
        {
            footAnchor = visualRoot != null ? visualRoot : transform;
        }

        if (visualRoot != null && (!cachedVisualRootBaseLocalPositionReady || cachedVisualRoot != visualRoot))
        {
            cachedVisualRoot = visualRoot;
            cachedVisualRootBaseLocalPosition = visualRoot.localPosition;
            cachedVisualRootBaseLocalPositionReady = true;
        }
    }

    public SkeletonAnimation GetSpineAnimation()
    {
        return ResolveSpineAnimation();
    }

    private SkeletonAnimation ResolveSpineAnimation()
    {
        if (cachedSpineAnimation != null)
        {
            return cachedSpineAnimation;
        }

        cachedSpineAnimation = GetComponentInChildren<SkeletonAnimation>(true);
        return cachedSpineAnimation;
    }

    private void UpdateSpineFacingMirror()
    {
        SkeletonAnimation spineAnimation = ResolveSpineAnimation();
        if (spineAnimation == null || spineAnimation.Skeleton == null)
        {
            return;
        }

        float horizontalInput = ResolveHorizontalInput();
        if (Mathf.Abs(horizontalInput) > 0.0001f)
        {
            cachedSpineFacingScaleX = horizontalInput > 0f ? -1 : 1;
        }

        spineAnimation.Skeleton.ScaleX = cachedSpineFacingScaleX;
    }

    private float ResolveHorizontalInput()
    {
        if (Keyboard.current == null)
        {
            return 0f;
        }

        float horizontal = 0f;
        if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
        {
            horizontal += 1f;
        }

        return horizontal;
    }

    private void ApplyVisualFloatOffset()
    {
        ResolveVisualFloatTargets();

        if (visualRoot == null || !cachedVisualRootBaseLocalPositionReady)
        {
            return;
        }

        if (cachedVisualRoot != visualRoot)
        {
            cachedVisualRoot = visualRoot;
            cachedVisualRootBaseLocalPosition = visualRoot.localPosition;
            cachedVisualRootBaseLocalPositionReady = true;
        }

        Vector3 floatOffset = Vector3.up * Mathf.Max(0f, visualFloatHeight);
        visualRoot.localPosition = cachedVisualRootBaseLocalPosition + floatOffset;
    }

    private IEnumerator SpawnUnstuckRoutine()
    {
        float delay = Mathf.Max(0f, spawnUnstuckDelay);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        else
        {
            yield return null;
        }

        spawnUnstuckRoutine = null;

        if (!enableSpawnUnstuck || !keepColliderOnGround)
        {
            yield break;
        }

        Vector3 originalPosition = transform.position;
        Vector3 safePosition = ResolveSafeGroundPosition(originalPosition);
        if ((safePosition - originalPosition).sqrMagnitude > 0.0001f)
        {
            MoveRootToGroundPosition(safePosition);
            Debug.Log($"Player spawn unstuck: moved from {originalPosition} to {safePosition}", this);
        }
    }

    public Vector3 ResolveSafeGroundPosition(Vector3 desiredPosition)
    {
        if (!enableSpawnUnstuck || !keepColliderOnGround)
        {
            return desiredPosition;
        }

        if (!IsSpawnPositionBlocked(desiredPosition))
        {
            return desiredPosition;
        }

        float step = Mathf.Max(0.05f, spawnUnstuckSearchStep);
        int rings = Mathf.Max(1, spawnUnstuckSearchRings);

        Vector3 forward = ResolveFacingDirection();
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }
        right.Normalize();

        Vector3[] directions = new Vector3[]
        {
            forward,
            -forward,
            right,
            -right,
            (forward + right).normalized,
            (forward - right).normalized,
            (-forward + right).normalized,
            (-forward - right).normalized
        };

        for (int ring = 1; ring <= rings; ring++)
        {
            float radius = step * ring;
            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 candidate = desiredPosition + directions[i] * radius;
                candidate.y = desiredPosition.y;
                if (!IsSpawnPositionBlocked(candidate))
                {
                    return candidate;
                }
            }
        }

        return desiredPosition;
    }

    private bool IsSpawnPositionBlocked(Vector3 candidatePosition)
    {
        if (!TryGetOwnColliderBounds(candidatePosition, out Bounds combinedBounds))
        {
            return false;
        }

        Vector3 halfExtents = combinedBounds.extents;
        halfExtents.x = Mathf.Max(0.05f, halfExtents.x * 0.95f);
        halfExtents.y = Mathf.Max(0.05f, halfExtents.y * 0.95f);
        halfExtents.z = Mathf.Max(0.05f, halfExtents.z * 0.95f);

        Collider[] hits = Physics.OverlapBox(
            combinedBounds.center,
            halfExtents,
            transform.rotation,
            spawnBlockerLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || IsOwnCollider(hit))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool TryGetOwnColliderBounds(Vector3 candidatePosition, out Bounds bounds)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        bool found = false;
        Vector3 delta = candidatePosition - transform.position;
        Bounds combined = new Bounds();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger || !IsOwnCollider(collider))
            {
                continue;
            }

            Bounds colliderBounds = collider.bounds;
            colliderBounds.center += delta;

            if (!found)
            {
                combined = colliderBounds;
                found = true;
            }
            else
            {
                combined.Encapsulate(colliderBounds);
            }
        }

        if (!found)
        {
            bounds = new Bounds(candidatePosition, new Vector3(0.5f, 1f, 0.5f));
            return false;
        }

        bounds = combined;
        return true;
    }

    private bool IsOwnCollider(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        Transform current = collider.transform;
        return current == transform || current.IsChildOf(transform);
    }

    private void MoveRootToGroundPosition(Vector3 worldPosition)
    {
        if (rb != null)
        {
            rb.position = worldPosition;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }

        transform.position = worldPosition;
        Physics.SyncTransforms();
    }

    public Rigidbody Body => rb;

    public Vector3 FacingDirection => ResolveFacingDirection();
    public Vector3 GetFacingDirection() => FacingDirection;
    public bool TryTriggerRuneCounterQ(CombatHealth attacker, bool suppressRuneCounterRecursion = true)
    {
        RuneRuntimeState runeRuntimeState = GetComponent<RuneRuntimeState>();
        bool debugThornCounter = runeRuntimeState != null && runeRuntimeState.IsThornCounterDebugEnabled();

        if (attacker == null)
        {
            if (debugThornCounter)
            {
                Debug.Log("[Rune][ThornCounter] Player02 controller rejected Q counter: attacker is null.", this);
            }

            return false;
        }

        if (qSkill is not Player2Skill_Q_DivineLightSword qDivineLightSword)
        {
            if (debugThornCounter)
            {
                Debug.Log("[Rune][ThornCounter] Player02 controller rejected Q counter: qSkill is null or not Player2Skill_Q_DivineLightSword.", this);
            }

            return false;
        }

        FaceTowardsTarget(attacker.transform);
        bool started = qDivineLightSword.TryCastAsRuneCounter(attacker.transform, suppressRuneCounterRecursion);
        if (debugThornCounter)
        {
            Debug.Log($"[Rune][ThornCounter] Player02 controller Q counter request result={started}.", this);
        }

        return started;
    }

    public void FaceTowardsTarget(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
        {
            return;
        }

        lastMoveDir = dir.normalized;
        cachedSpineFacingScaleX = lastMoveDir.x >= 0f ? -1 : 1;
        if (!lockCharacterRotation)
        {
            transform.rotation = Quaternion.LookRotation(lastMoveDir, Vector3.up);
        }

        UpdateSpineFacingMirror();
    }
    public int CurrentDivineMark => currentSwordEnergy;
    public Camera GetRenderCamera() => ResolveRRenderCamera();
    public GameObject GetSharedSkillEffectPrefab() => sharedSkillEffectPrefab;
    public Transform GroundAnchor => groundAnchor != null ? groundAnchor : transform;
    public Transform FootAnchor => footAnchor != null ? footAnchor : (visualRoot != null ? visualRoot : transform);

    public float LegacyERailDuration => eRailDuration;
    public bool LegacyEEnableAfterimageShader => eEnableAfterimageShader;
    public SpriteRenderer LegacyEAfterimageSourceSpriteRenderer => eAfterimageSourceSpriteRenderer;
    public bool LegacyEAfterimageUseDistanceSampling => eAfterimageUseDistanceSampling;
    public bool LegacyEAfterimageUseActualMoveDirection => eAfterimageUseActualMoveDirection;
    public bool LegacyEAfterimageInvertMoveDirection => eAfterimageInvertMoveDirection;
    public float LegacyEAfterimageSpacing => eAfterimageSpacing;
    public int LegacyEAfterimageMaxPerDash => eAfterimageMaxPerDash;

    public void CastQ()
    {
        if (qSkill != null)
        {
            qSkill.Cast();
            return;
        }

        CastQLegacy();
    }

    private void CastQLegacy()
    {
        Vector3 dir = ResolveFacingDirection();
        Vector3 spawnPos = transform.position + Vector3.up * 1.2f + transform.right * 0.8f + qEffectOffset;
        GameObject sword = CreateSkillEffectVisual(
            "Q_Sword",
            qSkillEffectPrefab,
            spawnPos,
            dir,
            true,
            qEffectInvertForward,
            qEffectYawOffset,
            qEffectVisualPitch,
            qEffectVisualYaw,
            qEffectVisualRoll + ResolveRotation(qEffectRotationZ),
            ResolveVisualScale(qEffectScale, qEffectPlaneScale));
        StartCoroutine(FireAfterDelay(sword, dir, qDelay, qSwordSpeed));
        currentSwordEnergy += 1;
    }

    private bool TryCastQFallback()
    {
        CastQ();
        return true;
    }

    public void CastW()
    {
        if (wSkill != null)
        {
            wSkill.Cast();
            return;
        }

        if (wSkillRoutine != null)
        {
            StopCoroutine(wSkillRoutine);
            wSkillRoutine = null;
        }

        CleanupWVisuals();
        wSkillRoutine = StartCoroutine(ShieldRoutine());
    }

    private bool TryCastWFallback()
    {
        CastW();
        return true;
    }

    public void CastE()
    {
        if (eSkill != null)
        {
            eSkill.Cast();
            return;
        }

        if (!isDashing)
        {
            StartCoroutine(DashRoutine());
        }
    }

    private bool TryCastEFallback()
    {
        if (isDashing)
        {
            return false;
        }

        CastE();
        return true;
    }

    public void CastR()
    {
        if (rSkill != null)
        {
            rSkill.Cast();
            return;
        }

        Debug.LogWarning(LegacyRDisabledWarning, this);
    }

    private bool TryCastRFallback()
    {
        Debug.LogWarning(LegacyRDisabledWarning, this);
        return false;
    }

    // Legacy R fallback disabled. Current R is Player2Skill_R_DivineStarRain.
    private IEnumerator RSwarmRoutine(int count)
    {
        Vector3 center = ResolveRSwarmCenter();
        GameObject swarmRoot = new GameObject("R_SwarmVisualRoot");
        swarmRoot.transform.position = center;
        swarmRoot.transform.rotation = Quaternion.identity;
        activeRSwarmRoot = swarmRoot;
        activeRSwarmSwords.Clear();

        int swordCount = Mathf.Max(0, count);
        for (int i = 0; i < swordCount; i++)
        {
            float baseAngle = i * (360f / Mathf.Max(1, swordCount)) + Random.Range(-30f, 30f);
            float radiusMin = Mathf.Min(rSwarmRadiusMin, rSwarmRadiusMax);
            float radiusMax = Mathf.Max(rSwarmRadiusMin, rSwarmRadiusMax);
            float radiusT = swordCount <= 1 ? 0.5f : i / (float)(swordCount - 1);
            float radius = Mathf.Lerp(radiusMin, radiusMax, radiusT);
            radius += Random.Range(-rSwarmRadiusJitter, rSwarmRadiusJitter);
            radius = Mathf.Max(0.01f, radius);
            float height = Random.Range(Mathf.Min(rSwarmHeightMin, rSwarmHeightMax), Mathf.Max(rSwarmHeightMin, rSwarmHeightMax));
            float orbitSpeed = Random.Range(Mathf.Min(rSwarmSpeedMin, rSwarmSpeedMax), Mathf.Max(rSwarmSpeedMin, rSwarmSpeedMax));
            float bobAmplitude = Random.Range(Mathf.Min(rSwarmBobAmplitudeMin, rSwarmBobAmplitudeMax), Mathf.Max(rSwarmBobAmplitudeMin, rSwarmBobAmplitudeMax));
            float bobFrequency = Random.Range(Mathf.Min(rSwarmBobFrequencyMin, rSwarmBobFrequencyMax), Mathf.Max(rSwarmBobFrequencyMin, rSwarmBobFrequencyMax));
            float phase = Random.Range(0f, Mathf.PI * 2f);

            Vector3 spawnOffset = GetOrbitPositionXZ(baseAngle, radius, height);
            Vector3 spawnPosition = center + spawnOffset;

            GameObject sword = CreateSkillEffectVisual(
                $"R_SwarmSword_{i}",
                rSkillEffectPrefab,
                spawnPosition,
                spawnOffset,
                false,
                false,
                0f,
                rEffectVisualPitch,
                rEffectVisualYaw,
                rEffectVisualRoll + ResolveRotation(rEffectRotationZ),
                Vector3.one);

            if (sword == null)
            {
                continue;
            }

            if (rUsePlayerLayerForR)
            {
                SetLayerRecursively(sword, gameObject.layer);
            }

            if (rForceDoubleSided)
            {
                TrySetDoubleSidedIfSupported(sword);
            }

            EnsureEffectVisible(sword);
            sword.transform.SetParent(swarmRoot.transform, true);
            ApplyRootVisualScale(sword, ResolveVisualScaleWithoutShared(rEffectScale));
            SkillEffectRuntime runtime = sword.GetComponent<SkillEffectRuntime>();
            Transform visualTransform = runtime != null && runtime.visual != null ? runtime.visual : null;
            Quaternion baseVisualLocalRotation = visualTransform != null ? visualTransform.localRotation : Quaternion.identity;
            activeRSwarmSwords.Add(new RSwarmSwordData
            {
                sword = sword,
                baseAngle = baseAngle,
                radius = radius,
                height = height,
                orbitSpeed = orbitSpeed,
                bobAmplitude = bobAmplitude,
                bobFrequency = bobFrequency,
                phase = phase,
                layerOffset = Random.Range(-0.25f, 0.25f),
                runtime = runtime,
                visualTransform = visualTransform,
                baseVisibleLocalRotation = baseVisualLocalRotation,
                selfSpinSpeed = 0f,
                selfSpinAngle = 0f,
                previousPosition = spawnPosition,
                hasPreviousPosition = true
            });
        }

        if (eAfterimageDebugLog)
        {
            Debug.Log($"R Swarm radius range: min={rSwarmRadiusMin}, max={rSwarmRadiusMax}, actualRadiusCount={activeRSwarmSwords.Count}", this);
            for (int i = 0; i < activeRSwarmSwords.Count; i++)
            {
                RSwarmSwordData data = activeRSwarmSwords[i];
                if (data != null)
                {
                    Debug.Log($"R Swarm sword[{i}] radius={data.radius}", this);
                }
            }
        }

        float orbitElapsed = 0f;
        float damageTickTimer = 0f;
        float orbitDuration = Mathf.Max(0.05f, rSwarmDuration);
        float safeDamageInterval = Mathf.Max(0.05f, rSwarmDamageInterval);
        float rainStartTime = Mathf.Clamp01(rStarRainStartRatio) * orbitDuration;
        float rainSpawnEndTime = orbitDuration;
        if (rStarRainContinueAfterOrbit)
        {
            rainSpawnEndTime += Mathf.Max(0f, rStarRainExtraDurationAfterOrbit);
        }
        float rainSpawnInterval = Mathf.Max(0.01f, rStarRainInterval);
        float rainSpawnAccumulator = 0f;
        float totalElapsed = 0f;
        bool orbitCleared = false;

        while (totalElapsed < rainSpawnEndTime || HasAliveRStarRainBlades())
        {
            center = ResolveRSwarmCenter();
            if (!orbitCleared && swarmRoot != null)
            {
                swarmRoot.transform.position = center;
            }

            if (orbitElapsed < orbitDuration)
            {
                float dirSign = rSwarmClockwise ? -1f : 1f;
                for (int i = 0; i < activeRSwarmSwords.Count; i++)
                {
                    RSwarmSwordData data = activeRSwarmSwords[i];
                    if (data == null || data.sword == null)
                    {
                        continue;
                    }

                    float angle = data.baseAngle + dirSign * data.orbitSpeed * orbitElapsed;
                    float rad = angle * Mathf.Deg2Rad;
                    float dynamicRadius = data.radius + Mathf.Sin(orbitElapsed * 1.7f + data.phase) * rSwarmRadiusJitter;
                    float dynamicHeight = data.height + data.layerOffset + Mathf.Sin(orbitElapsed * data.bobFrequency + data.phase) * data.bobAmplitude;

                    Vector3 offset = new Vector3(
                        Mathf.Cos(rad) * dynamicRadius,
                        dynamicHeight,
                        Mathf.Sin(rad) * dynamicRadius);
                    Vector3 currentPosition = center + offset;
                    data.sword.transform.position = currentPosition;
                    data.orbitEndPosition = currentPosition;

                    Vector3 tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                    if (rSwarmClockwise)
                    {
                        tangent = -tangent;
                    }

                    if (data.visualTransform != null)
                    {
                        float yaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg + rSwarmYawOffset;
                        ApplyRSwarmVisualRotation(data, yaw);
                    }
                }

                damageTickTimer -= Time.deltaTime;
                if (damageTickTimer <= 0f)
                {
                    ApplyRSwarmTickDamage(center);
                    damageTickTimer += safeDamageInterval;
                }
                orbitElapsed = Mathf.Min(orbitElapsed + Time.deltaTime, orbitDuration);
            }

            if (!orbitCleared && orbitElapsed >= orbitDuration)
            {
                orbitCleared = true;
                if (rOrbitClearWhenOrbitEnds)
                {
                    CleanupRSwarmOrbitVisuals();
                }
            }

            if (totalElapsed >= rainStartTime && totalElapsed <= rainSpawnEndTime)
            {
                rainSpawnAccumulator += Time.deltaTime;
                while (rainSpawnAccumulator >= rainSpawnInterval)
                {
                    SpawnRStarRainWave(center);
                    rainSpawnAccumulator -= rainSpawnInterval;
                }
            }

            UpdateRStarRainBlades(Time.deltaTime);

            totalElapsed += Time.deltaTime;

            if (totalElapsed >= rainSpawnEndTime && !HasAliveRStarRainBlades())
            {
                break;
            }

            yield return null;
        }

        CleanupRSwarmVisuals();
        rSwarmRoutine = null;
    }

    private IEnumerator FireAfterDelay(GameObject effectRoot, Vector3 dir, float delay, float speed)
    {
        float t = 0f;
        while (t < delay)
        {
            if (effectRoot == null) yield break;
            t += Time.deltaTime;
            yield return null;
        }

        float life = 2.2f;
        float elapsed = 0f;
        while (elapsed < life)
        {
            if (effectRoot == null) yield break;
            effectRoot.transform.position += dir.normalized * speed * Time.deltaTime;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (effectRoot != null) Destroy(effectRoot);
    }

    private IEnumerator ShieldRoutine()
    {
        isShielding = true;
        GameObject orbitRoot = new GameObject("W_OrbitVisualRoot");
        orbitRoot.transform.position = transform.position;
        orbitRoot.transform.rotation = Quaternion.identity;
        activeWOrbitVisualRoot = orbitRoot;

        int energyForW = useSwordEnergyForW ? Mathf.Max(0, currentSwordEnergy) : 0;
        int swordCount = baseWSwordCount;
        if (useSwordEnergyForW)
        {
            swordCount += energyForW;
        }
        swordCount = Mathf.Clamp(swordCount, baseWSwordCount, maxWSwordCount);
        wSwordCount = swordCount;

        float finalDuration = wDuration + Mathf.Min(energyForW * wDurationPerSwordEnergy, wMaxDurationBonus);
        float finalOrbitSpeed = wEffectOrbitSpeed + Mathf.Min(energyForW * wOrbitSpeedPerSwordEnergy, wMaxOrbitSpeedBonus);
        float finalRadius = wEffectOrbitRadius + Mathf.Min(energyForW * wRadiusPerSwordEnergy, wMaxRadiusBonus);

        activeWSwords.Clear();
        for (int i = 0; i < swordCount; i++)
        {
            float angle = i * (360f / swordCount);
            Vector3 offset = GetOrbitPositionXZ(angle, finalRadius, wEffectHeight);
            Vector3 spawnPos = transform.position + offset + wEffectOffset;

            GameObject sword = CreateSkillEffectVisual(
                $"W_Sword_{i}",
                ResolveWVisualPrefab(),
                spawnPos,
                offset,
                false,
                false,
                0f,
                standbySwordVisualPitch,
                standbySwordVisualYaw,
                standbySwordVisualRoll + ResolveRotation(standbySwordRotationZ),
                Vector3.one);

            if (sword == null)
            {
                continue;
            }

            Quaternion extraRot = Quaternion.Euler(
                wEffectVisualPitch,
                wEffectVisualYaw,
                wEffectVisualRoll);
            sword.transform.rotation = sword.transform.rotation * extraRot;

            sword.transform.SetParent(orbitRoot.transform, true);

            SkillEffectRuntime runtime = sword.GetComponent<SkillEffectRuntime>();
            if (runtime != null && runtime.visual != null)
            {
                runtime.baseVisualRotation = runtime.visual.rotation;
                if (!useRawPrefabRotationForSkillEffects)
                {
                    runtime.visual.localScale = Vector3.one;
                }
            }

            float sizeMul = Mathf.Max(0.01f, wEffectScaleMultiplier);
            Vector3 finalScale = Vector3.Scale(wEffectScale, Vector3.one * sizeMul);
            sword.transform.localScale = finalScale;
            Debug.Log($"[W Skill Scale] sword={sword.name}, baseScale={wEffectScale}, multiplier={sizeMul:F2}, finalScale={finalScale}", this);

            activeWSwords.Add(sword);
        }

        currentWSwordCount = activeWSwords.Count;
        currentWFinalDamageReduction = ComputeWFinalDamageReduction(currentWSwordCount);
        isWGuardActive = true;

        Debug.Log($"[W Skill] Base={baseWSwordCount}, CurrentSwordEnergy={energyForW}, Spawned={activeWSwords.Count}, Duration={finalDuration:F2}, OrbitSpeed={finalOrbitSpeed:F2}, Radius={finalRadius:F2}, DamageReduction={currentWFinalDamageReduction:F2}", this);
        if (activeWSwords.Count > swordCount)
        {
            Debug.LogWarning($"[W Skill] Spawned sword count exceeded expected {swordCount}: {activeWSwords.Count}", this);
        }

        if (activeWSwords.Count == 0)
        {
            CleanupWVisuals();
            isShielding = false;
            yield break;
        }

        wOrbitAngle = 0f;
        float t = 0f;
        while (t < finalDuration)
        {
            orbitRoot.transform.position = transform.position;
            orbitRoot.transform.rotation = Quaternion.identity;

            wOrbitAngle += finalOrbitSpeed * Time.deltaTime;
            for (int i = 0; i < activeWSwords.Count; i++)
            {
                GameObject sword = activeWSwords[i];
                if (sword == null)
                {
                    continue;
                }

                float baseAngle = wOrbitAngle + i * (360f / swordCount);
                Vector3 offset = GetOrbitPositionXZ(baseAngle, finalRadius, wEffectHeight);
                sword.transform.position = transform.position + offset + wEffectOffset;

                SkillEffectRuntime runtime = sword.GetComponent<SkillEffectRuntime>();
                if (runtime != null && runtime.visual != null)
                {
                    float yaw = GetOrbitTangentYawXZ(baseAngle) + wSwordOrbitYawOffset;
                    runtime.visual.rotation = Quaternion.Euler(0f, yaw, 0f) * runtime.baseVisualRotation;
                }
            }

            t += Time.deltaTime;
            yield return null;
        }

        CleanupWVisuals();
        isShielding = false;
        wSkillRoutine = null;
    }

    private void CleanupWVisuals()
    {
        for (int i = 0; i < activeWSwords.Count; i++)
        {
            GameObject sword = activeWSwords[i];
            if (sword != null)
            {
                Destroy(sword);
            }
        }
        activeWSwords.Clear();

        if (activeWOrbitVisualRoot != null)
        {
            Destroy(activeWOrbitVisualRoot);
            activeWOrbitVisualRoot = null;
        }

        Transform[] allTransforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform tr = allTransforms[i];
            if (tr == null) continue;
            string n = tr.name;
            bool isLegacyW =
                n == "W_OrbitRoot" ||
                n == "W_OrbitVisualRoot" ||
                n == "W_Sword" ||
                n == "W_SwordInstance" ||
                n.StartsWith("W_Sword_") ||
                n.StartsWith("W_SwordPivot_");

            if (isLegacyW)
            {
                Destroy(tr.gameObject);
            }
        }

        standbySwords = 0;
        isShielding = false;
        isWGuardActive = false;
        currentWSwordCount = 0;
        currentWFinalDamageReduction = 0f;
    }

    private bool HasAliveRStarRainBlades()
    {
        for (int i = 0; i < activeRStarRainBlades.Count; i++)
        {
            RStarRainBladeData data = activeRStarRainBlades[i];
            if (data != null && data.sword != null)
            {
                return true;
            }
        }

        return false;
    }

    private void SpawnRStarRainWave(Vector3 center)
    {
        int waveCount = Mathf.Max(1, rStarRainBladesPerWave);
        float rainSpawnHeight = Mathf.Max(0f, rStarRainSpawnHeight);
        float rainRadius = Mathf.Max(0f, rStarRainRadius);
        float rainRandomDelay = Mathf.Max(0f, rStarRainRandomDelay);
        float rainFallSpeed = Mathf.Max(0.1f, rStarRainFallSpeed);

        for (int i = 0; i < waveCount; i++)
        {
            Vector2 randomOffset2D = Random.insideUnitCircle * rainRadius;
            Vector3 target = center + new Vector3(randomOffset2D.x, 0f, randomOffset2D.y);
            Vector3 spawn = target + Vector3.up * rainSpawnHeight;

            GameObject sword = CreateSkillEffectVisual(
                $"R_StarRain_{activeRStarRainBlades.Count}",
                rSkillEffectPrefab,
                spawn,
                Vector3.down,
                false,
                false,
                0f,
                0f,
                0f,
                0f,
                Vector3.one);

            if (sword == null)
            {
                continue;
            }

            if (rUsePlayerLayerForR)
            {
                SetLayerRecursively(sword, gameObject.layer);
            }

            if (rForceDoubleSided)
            {
                TrySetDoubleSidedIfSupported(sword);
            }

            EnsureEffectVisible(sword);
            ApplyRootVisualScale(sword, ResolveVisualScaleWithoutShared(rStarRainEffectScale));
            SkillEffectRuntime runtime = sword.GetComponent<SkillEffectRuntime>();
            Transform visualTransform = runtime != null && runtime.visual != null ? runtime.visual : FindEffectVisualTransform(sword);

            RStarRainBladeData data = new RStarRainBladeData
            {
                sword = sword,
                runtime = runtime,
                visualTransform = visualTransform,
                baseVisibleLocalRotation = visualTransform != null ? visualTransform.localRotation : Quaternion.identity,
                spawnPosition = spawn,
                targetPosition = target,
                delay = Random.Range(0f, rainRandomDelay),
                fallDuration = Mathf.Max(0.05f, Vector3.Distance(spawn, target) / rainFallSpeed),
                elapsed = 0f,
                impactApplied = false
            };

            activeRStarRainBlades.Add(data);
            ApplyRStarRainVisualRotation(data);
        }
    }

    private void UpdateRStarRainBlades(float deltaTime)
    {
        for (int i = activeRStarRainBlades.Count - 1; i >= 0; i--)
        {
            RStarRainBladeData data = activeRStarRainBlades[i];
            if (data == null || data.sword == null)
            {
                activeRStarRainBlades.RemoveAt(i);
                continue;
            }

            data.elapsed += deltaTime;
            if (data.elapsed < data.delay)
            {
                data.sword.transform.position = data.spawnPosition;
                continue;
            }

            float fallT = Mathf.Clamp01((data.elapsed - data.delay) / Mathf.Max(0.05f, data.fallDuration));
            float smoothFallT = Mathf.SmoothStep(0f, 1f, fallT);
            data.sword.transform.position = Vector3.Lerp(data.spawnPosition, data.targetPosition, smoothFallT);
            ApplyRStarRainVisualRotation(data);

            if (fallT >= 1f && !data.impactApplied)
            {
                data.impactApplied = true;
                ApplyRSwarmImpactDamage(data.targetPosition);
                Destroy(data.sword);
                activeRStarRainBlades.RemoveAt(i);
            }
        }
    }

    private void CleanupRSwarmVisuals()
    {
        for (int i = 0; i < activeRSwarmSwords.Count; i++)
        {
            RSwarmSwordData data = activeRSwarmSwords[i];
            if (data != null && data.sword != null)
            {
                Destroy(data.sword);
            }
        }
        activeRSwarmSwords.Clear();

        for (int i = 0; i < activeRStarRainBlades.Count; i++)
        {
            RStarRainBladeData data = activeRStarRainBlades[i];
            if (data != null && data.sword != null)
            {
                Destroy(data.sword);
            }
        }
        activeRStarRainBlades.Clear();

        if (activeRSwarmRoot != null)
        {
            Destroy(activeRSwarmRoot);
            activeRSwarmRoot = null;
        }
    }

    private void CleanupRSwarmOrbitVisuals()
    {
        GameObject orbitRoot = activeRSwarmRoot;
        for (int i = 0; i < activeRSwarmSwords.Count; i++)
        {
            RSwarmSwordData data = activeRSwarmSwords[i];
            if (data != null && data.sword != null)
            {
                if (rOrbitFadeOutDuration > 0f)
                {
                    StartCoroutine(FadeAndDestroy(data.sword, rOrbitFadeOutDuration));
                }
                else
                {
                    Destroy(data.sword);
                }
            }
        }

        activeRSwarmSwords.Clear();

        activeRSwarmRoot = null;

        if (orbitRoot != null)
        {
            if (rOrbitFadeOutDuration > 0f)
            {
                StartCoroutine(DestroyAfterDelay(orbitRoot, rOrbitFadeOutDuration));
            }
            else
            {
                Destroy(orbitRoot);
            }
        }
    }

    private IEnumerator DestroyAfterDelay(GameObject target, float delay)
    {
        if (target == null) yield break;
        yield return new WaitForSeconds(Mathf.Max(0f, delay));
        if (target != null)
        {
            Destroy(target);
        }
    }

    private IEnumerator FadeAndDestroy(GameObject target, float duration)
    {
        if (target == null)
        {
            yield break;
        }

        duration = Mathf.Max(0.01f, duration);
        SkillEffectRuntime runtime = target.GetComponent<SkillEffectRuntime>();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (target == null)
            {
                yield break;
            }

            float alpha = 1f - (elapsed / duration);
            ApplyFadeAlpha(runtime, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (target != null)
        {
            ApplyFadeAlpha(runtime, 0f);
            Destroy(target);
        }
    }

    private IEnumerator FadeAndDestroySpriteGhost(GameObject ghost, SpriteRenderer sr, float duration)
    {
        if (ghost == null || sr == null)
        {
            yield break;
        }

        duration = Mathf.Max(0.05f, duration);
        Color startColor = sr.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (ghost == null || sr == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0f, t);
            sr.color = c;

            yield return null;
        }

        if (ghost != null)
        {
            Destroy(ghost);
        }
    }

    // Legacy R fallback disabled. Current R is Player2Skill_R_DivineStarRain.
    private void ApplyRSwarmTickDamage(Vector3 center)
    {
        ApplyRSwarmAreaDamage(center, rSwarmDamageRadius, rSwarmDamagePerTick);
    }

    // Legacy R fallback disabled. Current R is Player2Skill_R_DivineStarRain.
    private void ApplyRSwarmImpactDamage(Vector3 center)
    {
        ApplyRSwarmAreaDamage(center, Mathf.Max(0.01f, rStarRainDamageRadius), rSwarmDamagePerTick * Mathf.Max(0f, rStarRainDamageMultiplier));
    }

    // Legacy R fallback disabled. Current R is Player2Skill_R_DivineStarRain.
    private void ApplyRSwarmAreaDamage(Vector3 center, float radius, float damageAmount)
    {
        if (damageAmount <= 0f || radius <= 0f)
        {
            return;
        }

        Collider[] hits = Physics.OverlapSphere(center, radius, rSwarmEnemyLayer);
        HashSet<GameObject> damagedRoots = new HashSet<GameObject>();
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            Transform targetRoot = hit.transform.root;
            if (targetRoot == null || targetRoot.gameObject == gameObject || !damagedRoots.Add(targetRoot.gameObject))
            {
                continue;
            }

            CombatHealth combatHealth = targetRoot.GetComponentInParent<CombatHealth>();
            if (combatHealth != null && combatHealth.gameObject != gameObject)
            {
                combatHealth.TakeDamage(new BattleDamage(damageAmount, BattleDamageType.Physical, gameObject));
                continue;
            }

            EnemyHealth enemyHealth = targetRoot.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null && enemyHealth.gameObject != gameObject)
            {
                int damageInt = Mathf.Max(1, Mathf.RoundToInt(damageAmount));
                enemyHealth.TakeDamage(damageInt, gameObject);
            }
        }
    }

    private GameObject ResolveWVisualPrefab()
    {
        if (standbySkillEffectPrefab != null)
        {
            return standbySkillEffectPrefab;
        }

        if (wSkillEffectPrefab != null)
        {
            return wSkillEffectPrefab;
        }

        return sharedSkillEffectPrefab;
    }

    private float ComputeWFinalDamageReduction(int wSwordCountAtCast)
    {
        float reduction = wDamageReduction + Mathf.Max(0, wSwordCountAtCast) * wDamageReductionPerSword;
        return Mathf.Clamp(reduction, 0f, wMaxDamageReduction);
    }

    public float ProcessIncomingDamageWithWGuard(float rawDamage, BattleDamage incomingDamage)
    {
        if (wSkill != null)
        {
            return wSkill.ProcessIncomingDamageWithWGuard(rawDamage, incomingDamage);
        }

        float clampedRaw = Mathf.Max(0f, rawDamage);
        if (!isWGuardActive)
        {
            return clampedRaw;
        }

        float blockedDamage = clampedRaw * currentWFinalDamageReduction;
        float damageAfterReduction = clampedRaw - blockedDamage;
        float counterDamage = blockedDamage * wCounterDamageRatio;

        Debug.Log($"[W Guard] Raw={clampedRaw:F2}, Blocked={blockedDamage:F2}, Taken={damageAfterReduction:F2}, Counter={counterDamage:F2}", this);
        ApplyWCounterDamage(incomingDamage, counterDamage);
        return Mathf.Max(0f, damageAfterReduction);
    }

    private void ApplyWCounterDamage(BattleDamage incomingDamage, float counterDamage)
    {
        if (counterDamage <= 0f)
        {
            return;
        }

        GameObject attacker = incomingDamage.source;
        if (attacker == null)
        {
            Debug.LogWarning("[W Guard] Counter requires attacker/source reference in BattleDamage.", this);
            return;
        }

        if (attacker == gameObject)
        {
            return;
        }

        CombatHealth attackerCombatHealth = attacker.GetComponentInParent<CombatHealth>();
        if (attackerCombatHealth != null && attackerCombatHealth.gameObject != gameObject)
        {
            attackerCombatHealth.TakeDamage(new BattleDamage(counterDamage, incomingDamage.damageType, gameObject));
            return;
        }

        EnemyHealth attackerEnemyHealth = attacker.GetComponentInParent<EnemyHealth>();
        if (attackerEnemyHealth != null && attackerEnemyHealth.gameObject != gameObject)
        {
            int roundedDamage = Mathf.Max(1, Mathf.RoundToInt(counterDamage));
            attackerEnemyHealth.TakeDamage(roundedDamage, gameObject);
            return;
        }

        Debug.LogWarning($"[W Guard] Attacker '{attacker.name}' has no CombatHealth/EnemyHealth for counter damage.", this);
    }

    private IEnumerator DashRoutine()
    {
        isDashing = true;
        Vector3 dir = ResolveFacingDirection();
        Vector3 dashStartPos = transform.position;
        Vector3 dashEndPos = dashStartPos + dir * dashDistance;

        if (eEnableAfterimageShader)
        {
            int spawnedAfterimages = 0;
            Vector3 lastAfterimagePos = dashStartPos;
            float afterimageDistanceAccumulator = 0f;
            float nextAfterimageTime = 0f;
            float spawnInterval = Mathf.Max(0.005f, eAfterimageSpawnInterval);
            float elapsed = 0f;

            while (elapsed < dashDuration)
            {
                float p = Mathf.Clamp01(elapsed / dashDuration);
                transform.position = Vector3.Lerp(dashStartPos, dashEndPos, p);

                if (!eAfterimageUseManualTrailLayout && eAfterimageUseDistanceSampling)
                {
                    Vector3 currentPos = transform.position;
                    Vector3 moveDelta = currentPos - lastAfterimagePos;
                    Vector3 actualMoveDir = moveDelta.sqrMagnitude > 0.0001f ? moveDelta.normalized : Vector3.zero;
                    if (eAfterimageUseActualMoveDirection && actualMoveDir.sqrMagnitude > 0.0001f)
                    {
                        lastMoveDir = actualMoveDir;
                    }

                    float moved = Vector3.Distance(lastAfterimagePos, currentPos);
                    afterimageDistanceAccumulator += moved;

                    int maxPerDash = Mathf.Max(0, eAfterimageMaxPerDash);
                    float spacing = Mathf.Max(0.001f, eAfterimageSpacing);
                    Vector3 from = lastAfterimagePos;
                    Vector3 to = currentPos;
                    if (eAfterimageInvertMoveDirection)
                    {
                        from = currentPos;
                        to = lastAfterimagePos;
                    }

                    while (spawnedAfterimages < maxPerDash && afterimageDistanceAccumulator >= spacing)
                    {
                        float over = afterimageDistanceAccumulator - spacing;
                        float spawnDistanceFromStart = moved - over;
                        float t = moved > 0.0001f ? Mathf.Clamp01(spawnDistanceFromStart / moved) : 1f;
                        Vector3 spawnPos = Vector3.Lerp(from, to, t);
                        if (eAfterimageDebugLog)
                        {
                            Debug.Log($"E Afterimage index={spawnedAfterimages}, invert={eAfterimageInvertMoveDirection}, from={from}, to={to}, pos={spawnPos}", this);
                        }
                        TrySpawnEAfterimage(spawnPos, dashStartPos, dashEndPos, ref spawnedAfterimages);
                        afterimageDistanceAccumulator -= spacing;
                    }

                    lastAfterimagePos = currentPos;
                }
                else if (!eAfterimageUseManualTrailLayout && eAfterimageSpawnDuringDash)
                {
                    while (spawnedAfterimages < Mathf.Max(0, eAfterimageCount) && elapsed >= nextAfterimageTime)
                    {
                        TrySpawnEAfterimage(transform.position, dashStartPos, dashEndPos, ref spawnedAfterimages);
                        nextAfterimageTime += spawnInterval;
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.position = dashEndPos;
            if (eAfterimageUseManualTrailLayout)
            {
                StartCoroutine(SpawnEAfterimagesManualTrail(dashStartPos, dashEndPos));
            }
            else if (eAfterimageUseDistanceSampling)
            {
                // Distance sampling already handled during the dash.
            }
            else if (eAfterimageSpawnDuringDash)
            {
                // Do not spawn a second path-sampled set when real-time dash spawning is enabled.
            }
            else if (eAfterimageUsePathSamples)
            {
                StartCoroutine(SpawnEAfterimagesAlongPath(dashStartPos, dashEndPos));
            }

            isDashing = false;
            yield break;
        }

        isDashing = false;
    }

    private TrailRenderer BeginETrailRenderer()
    {
        SpriteRenderer sourceSprite = ResolveEAfterimageSourceSpriteRenderer();
        GameObject trailHost = sourceSprite != null ? sourceSprite.gameObject : gameObject;

        if (eDashTrailRenderer == null || eDashTrailRenderer.gameObject != trailHost)
        {
            eDashTrailRenderer = trailHost.GetComponent<TrailRenderer>();
            if (eDashTrailRenderer == null)
            {
                eDashTrailRenderer = trailHost.AddComponent<TrailRenderer>();
            }
        }

        eDashTrailRenderer.Clear();
        eDashTrailRenderer.emitting = true;
        eDashTrailRenderer.time = Mathf.Max(0.05f, eTrailTime);
        eDashTrailRenderer.startWidth = eTrailStartWidth;
        eDashTrailRenderer.endWidth = eTrailEndWidth;
        eDashTrailRenderer.minVertexDistance = Mathf.Max(0.001f, eTrailMinVertexDistance);
        eDashTrailRenderer.startColor = eTrailStartColor;
        eDashTrailRenderer.endColor = eTrailEndColor;
        eDashTrailRenderer.alignment = LineAlignment.View;
        eDashTrailRenderer.textureMode = LineTextureMode.Stretch;
        eDashTrailRenderer.generateLightingData = false;
        eDashTrailRenderer.autodestruct = false;
        eDashTrailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        eDashTrailRenderer.receiveShadows = false;
        eDashTrailRenderer.sortingLayerID = sourceSprite != null ? sourceSprite.sortingLayerID : 0;
        eDashTrailRenderer.sortingOrder = (sourceSprite != null ? sourceSprite.sortingOrder : 0) + eTrailSortingOrderOffset;

        if (eTrailMaterial != null)
        {
            eDashTrailRenderer.material = eTrailMaterial;
        }
        else if (sourceSprite != null && sourceSprite.sharedMaterial != null)
        {
            eDashTrailRenderer.material = sourceSprite.sharedMaterial;
        }
        else if (eDashTrailRenderer.material == null)
        {
            Shader fallbackShader = Shader.Find("Sprites/Default");
            if (fallbackShader != null)
            {
                eDashTrailRenderer.material = new Material(fallbackShader);
            }
        }

        return eDashTrailRenderer;
    }

    private void EndETrailRenderer(TrailRenderer trail)
    {
        if (trail == null)
        {
            return;
        }

        trail.emitting = false;
    }

    private TrailRenderer BeginEStarTrail()
    {
        SpriteRenderer sourceSprite = ResolveEAfterimageSourceSpriteRenderer();
        CleanupEStarTrailVisuals();
        InitializeEStarTrailDefaults();

        GameObject trailHost = new GameObject("E_StarTrail");
        trailHost.transform.SetParent(transform, false);
        trailHost.transform.localPosition = eStarTrailLocalOffset;
        trailHost.transform.localRotation = Quaternion.identity;
        trailHost.transform.localScale = Vector3.one;
        eDashTrailHost = trailHost;

        TrailRenderer trail = trailHost.AddComponent<TrailRenderer>();
        eDashTrailRenderer = trail;
        trail.Clear();
        trail.emitting = true;
        trail.time = Mathf.Max(0.05f, eStarTrailTime);
        trail.startWidth = eStarTrailStartWidth;
        trail.endWidth = eStarTrailEndWidth;
        trail.minVertexDistance = Mathf.Max(0.001f, eStarTrailMinVertexDistance);
        trail.colorGradient = GetResolvedStarTrailGradient();
        trail.numCornerVertices = 6;
        trail.numCapVertices = 8;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.generateLightingData = false;
        trail.autodestruct = false;
        trail.sortingLayerID = sourceSprite != null ? sourceSprite.sortingLayerID : 0;
        trail.sortingOrder = (sourceSprite != null ? sourceSprite.sortingOrder : 0) + eTrailSortingOrderOffset;
        trail.material = GetOrCreateStarTrailMaterial();
        return trail;
    }

    private void EndEStarTrail(TrailRenderer trail)
    {
        if (trail == null)
        {
            return;
        }

        trail.emitting = false;
        StartCoroutine(DestroyAfterSeconds(trail.gameObject, Mathf.Max(0.05f, eStarTrailTime)));
    }

    private GameObject BeginEDashCoreGlow()
    {
        if (!eEnableDashCoreGlow)
        {
            return null;
        }

        SpriteRenderer sourceSprite = ResolveEAfterimageSourceSpriteRenderer();
        CleanupEDashCoreGlow();

        GameObject glowObject;
        if (eDashCoreGlowPrefab != null)
        {
            glowObject = Instantiate(eDashCoreGlowPrefab, transform);
        }
        else
        {
            glowObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glowObject.name = "E_DashCoreGlow";
            glowObject.transform.SetParent(transform, false);
            Collider collider = glowObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        glowObject.transform.localPosition = eDashCoreGlowOffset;
        glowObject.transform.localRotation = Quaternion.identity;
        glowObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, eDashCoreGlowScale);
        eDashCoreGlowInstance = glowObject;

        Renderer renderer = glowObject.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            eDashCoreGlowRenderer = renderer;
            renderer.material = GetOrCreateDashCoreGlowMaterial();
            ApplyRendererColor(renderer, eDashCoreGlowColor, 1f);
            renderer.sortingLayerID = sourceSprite != null ? sourceSprite.sortingLayerID : 0;
            renderer.sortingOrder = (sourceSprite != null ? sourceSprite.sortingOrder : 0) + eTrailSortingOrderOffset;
        }
        else if (eAfterimageDebugLog)
        {
            Debug.LogWarning("[E DashCoreGlow] Prefab has no Renderer.", this);
        }

        return glowObject;
    }

    private void FadeAndDestroyEDashCoreGlow(GameObject glowObject, float duration)
    {
        if (glowObject == null)
        {
            return;
        }

        Renderer renderer = glowObject.GetComponentInChildren<Renderer>(true);
        if (renderer == null)
        {
            Destroy(glowObject);
            return;
        }

        StartCoroutine(FadeAndDestroyRendererRoutine(glowObject, renderer, duration));
    }

    private ParticleSystem BeginEStarTrailParticles()
    {
        if (!eEnableStarTrailParticles || eStarTrailParticlePrefab == null)
        {
            return null;
        }

        CleanupEStarTrailParticles();
        ParticleSystem instance = Instantiate(eStarTrailParticlePrefab, transform);
        instance.transform.localPosition = eStarTrailLocalOffset;
        instance.transform.localRotation = Quaternion.identity;
        instance.gameObject.name = "E_StarTrailParticles";
        var main = instance.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        instance.Play(true);
        eDashStarTrailParticlesInstance = instance;
        return instance;
    }

    private void EndEStarTrailParticles(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
        {
            return;
        }

        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        StartCoroutine(DestroyParticleSystemWhenDead(particleSystem));
    }

    private void CleanupEStarTrailVisuals()
    {
        CleanupEStarTrailParticles();
        CleanupEDashCoreGlow();

        if (eDashTrailHost != null)
        {
            Destroy(eDashTrailHost);
            eDashTrailHost = null;
            eDashTrailRenderer = null;
        }

        if (eRuntimeStarTrailMaterial != null)
        {
            Destroy(eRuntimeStarTrailMaterial);
            eRuntimeStarTrailMaterial = null;
        }

        if (eRuntimeDashCoreGlowMaterial != null)
        {
            Destroy(eRuntimeDashCoreGlowMaterial);
            eRuntimeDashCoreGlowMaterial = null;
        }
    }

    private void CleanupEDashCoreGlow()
    {
        if (eDashCoreGlowInstance != null)
        {
            Destroy(eDashCoreGlowInstance);
            eDashCoreGlowInstance = null;
            eDashCoreGlowRenderer = null;
        }
    }

    private void CleanupEStarTrailParticles()
    {
        if (eDashStarTrailParticlesInstance != null)
        {
            Destroy(eDashStarTrailParticlesInstance.gameObject);
            eDashStarTrailParticlesInstance = null;
        }
    }

    private IEnumerator DestroyAfterSeconds(GameObject target, float seconds)
    {
        if (target == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(Mathf.Max(0.05f, seconds));
        if (target != null)
        {
            Destroy(target);
        }
    }

    private IEnumerator DestroyParticleSystemWhenDead(ParticleSystem particleSystem)
    {
        if (particleSystem == null)
        {
            yield break;
        }

        while (particleSystem != null && particleSystem.IsAlive(true))
        {
            yield return null;
        }

        if (particleSystem != null)
        {
            Destroy(particleSystem.gameObject);
        }
    }

    private IEnumerator FadeAndDestroyRendererRoutine(GameObject target, Renderer renderer, float duration)
    {
        if (target == null || renderer == null)
        {
            yield break;
        }

        float total = Mathf.Max(0.05f, duration);
        float elapsed = 0f;
        Color baseColor = eDashCoreGlowColor;

        while (elapsed < total && target != null && renderer != null)
        {
            float t = 1f - Mathf.Clamp01(elapsed / total);
            Color faded = baseColor;
            faded.a *= t;
            ApplyRendererColor(renderer, faded, Mathf.Lerp(1f, 0.2f, 1f - t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (target != null)
        {
            Destroy(target);
        }
    }

    private Material GetOrCreateStarTrailMaterial()
    {
        if (eStarTrailMaterial != null)
        {
            return eStarTrailMaterial;
        }

        if (eRuntimeStarTrailMaterial == null)
        {
            Shader shader = Shader.Find("AHD2TODSystem/S_E_StarTrail_Additive");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                return null;
            }

            eRuntimeStarTrailMaterial = new Material(shader);
            if (eRuntimeStarTrailMaterial.HasProperty("_MainTex"))
            {
                eRuntimeStarTrailMaterial.mainTexture = Texture2D.whiteTexture;
            }

            if (eRuntimeStarTrailMaterial.HasProperty("_Color"))
            {
                eRuntimeStarTrailMaterial.SetColor("_Color", Color.white);
            }

            if (eRuntimeStarTrailMaterial.HasProperty("_Intensity"))
            {
                eRuntimeStarTrailMaterial.SetFloat("_Intensity", 1.25f);
            }
        }

        return eRuntimeStarTrailMaterial;
    }

    private Material GetOrCreateDashCoreGlowMaterial()
    {
        if (eDashCoreGlowMaterial != null)
        {
            return eDashCoreGlowMaterial;
        }

        if (eRuntimeDashCoreGlowMaterial == null)
        {
            Shader shader = Shader.Find("AHD2TODSystem/S_E_StarTrail_Additive");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                return null;
            }

            eRuntimeDashCoreGlowMaterial = new Material(shader);
            if (eRuntimeDashCoreGlowMaterial.HasProperty("_MainTex"))
            {
                eRuntimeDashCoreGlowMaterial.mainTexture = Texture2D.whiteTexture;
            }

            if (eRuntimeDashCoreGlowMaterial.HasProperty("_Intensity"))
            {
                eRuntimeDashCoreGlowMaterial.SetFloat("_Intensity", 2.2f);
            }
        }

        return eRuntimeDashCoreGlowMaterial;
    }

    private void ApplyRendererColor(Renderer renderer, Color color, float intensity)
    {
        if (renderer == null)
        {
            return;
        }

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor("_Color", color);
        block.SetColor("_BaseColor", color);
        if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Intensity"))
        {
            block.SetFloat("_Intensity", intensity);
        }
        renderer.SetPropertyBlock(block);
    }

    private Gradient GetResolvedStarTrailGradient()
    {
        Gradient gradient = eStarTrailGradient != null ? eStarTrailGradient : CreateDefaultStarTrailGradient();
        if (!eStarTrailReverseGradient)
        {
            return gradient;
        }

        return ReverseGradient(gradient);
    }

    private static Gradient ReverseGradient(Gradient source)
    {
        if (source == null)
        {
            return CreateDefaultStarTrailGradient();
        }

        Gradient reversed = new Gradient();
        GradientColorKey[] sourceColorKeys = source.colorKeys;
        GradientAlphaKey[] sourceAlphaKeys = source.alphaKeys;
        GradientColorKey[] reversedColorKeys = new GradientColorKey[sourceColorKeys.Length];
        GradientAlphaKey[] reversedAlphaKeys = new GradientAlphaKey[sourceAlphaKeys.Length];

        for (int i = 0; i < sourceColorKeys.Length; i++)
        {
            GradientColorKey key = sourceColorKeys[i];
            reversedColorKeys[i] = new GradientColorKey(key.color, 1f - key.time);
        }

        for (int i = 0; i < sourceAlphaKeys.Length; i++)
        {
            GradientAlphaKey key = sourceAlphaKeys[i];
            reversedAlphaKeys[i] = new GradientAlphaKey(key.alpha, 1f - key.time);
        }

        reversed.SetKeys(reversedColorKeys, reversedAlphaKeys);
        return reversed;
    }

    private void InitializeEStarTrailDefaults()
    {
        if (eStarTrailGradient == null)
        {
            eStarTrailGradient = CreateDefaultStarTrailGradient();
        }
    }

    private static Gradient CreateDefaultStarTrailGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.55f, 0.85f, 1f), 0.25f),
                new GradientColorKey(new Color(0.42f, 0.55f, 1f), 0.65f),
                new GradientColorKey(new Color(0.25f, 0.15f, 0.55f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.85f, 0.18f),
                new GradientAlphaKey(0.4f, 0.7f),
                new GradientAlphaKey(0f, 1f),
            });
        return gradient;
    }

    private SpriteRenderer ResolveEAfterimageSourceSpriteRenderer()
    {
        if (eAfterimageSourceSpriteRenderer != null && eAfterimageSourceSpriteRenderer.sprite != null)
        {
            return eAfterimageSourceSpriteRenderer;
        }

        if (eAfterimageSourceRenderer is SpriteRenderer legacySprite && legacySprite.sprite != null)
        {
            return legacySprite;
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            return spriteRenderer;
        }

        spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            return spriteRenderer;
        }

        return null;
    }

    private bool TrySpawnEAfterimage(Vector3 position, Vector3 dashStartPos, Vector3 dashEndPos, ref int spawnedCount)
    {
        if (!eEnableAfterimageShader)
        {
            return false;
        }

        int maxCount = Mathf.Max(0, eAfterimageCount);
        if (maxCount > 0 && spawnedCount >= maxCount)
        {
            return false;
        }

        SpriteRenderer sourceSprite = ResolveEAfterimageSourceSpriteRenderer();
        if (sourceSprite == null || sourceSprite.sprite == null)
        {
            if (eAfterimageDebugLog)
            {
                Debug.LogWarning("[E Afterimage] source SpriteRenderer is null or has no sprite.", this);
            }
            return false;
        }

        GameObject afterimage = SpawnEAfterimageGhost(sourceSprite, position, dashStartPos, dashEndPos, spawnedCount);
        if (afterimage == null)
        {
            return false;
        }

        spawnedCount += 1;
        return true;
    }

    private IEnumerator SpawnEAfterimagesAlongPath(Vector3 startPos, Vector3 endPos)
    {
        if (!eEnableAfterimageShader)
        {
            yield break;
        }

        SpriteRenderer sourceSprite = ResolveEAfterimageSourceSpriteRenderer();
        if (sourceSprite == null || sourceSprite.sprite == null)
        {
            if (eAfterimageDebugLog)
            {
                Debug.LogWarning("[E Afterimage] source SpriteRenderer is null or has no sprite.", this);
            }
            yield break;
        }

        Vector3 visualOffset = sourceSprite.transform.position - transform.position;
        if (eAfterimageDebugLog)
        {
            Debug.Log($"E Afterimage Path start={startPos}, end={endPos}, distance={Vector3.Distance(startPos, endPos)}", this);
        }

        int count = Mathf.Max(1, eAfterimageCount);
        bool sampleForward = eAfterimageUseActualMoveDirection;
        if (eAfterimageInvertMoveDirection)
        {
            sampleForward = !sampleForward;
        }

        for (int i = 0; i < count; i++)
        {
            float rawT = count <= 1 ? 1f : i / (float)(count - 1);
            float biasedT = Mathf.Clamp01(rawT + eAfterimagePathForwardBias);
            Vector3 pos = Vector3.Lerp(sampleForward ? startPos : endPos, sampleForward ? endPos : startPos, biasedT) + visualOffset;
            SpawnEAfterimageGhost(sourceSprite, pos, startPos, endPos, i);

            if (eAfterimagePathSpawnDelay > 0f)
            {
                yield return new WaitForSeconds(eAfterimagePathSpawnDelay);
            }
            else
            {
                yield return null;
            }
        }
    }

    private IEnumerator SpawnEAfterimagesManualTrail(Vector3 dashStartPos, Vector3 dashEndPos)
    {
        if (!eEnableAfterimageShader)
        {
            yield break;
        }

        SpriteRenderer sourceSprite = ResolveEAfterimageSourceSpriteRenderer();
        if (sourceSprite == null || sourceSprite.sprite == null)
        {
            if (eAfterimageDebugLog)
            {
                Debug.LogWarning("[E Afterimage] source SpriteRenderer is null or has no sprite.", this);
            }
            yield break;
        }

        Vector3 dashDir = dashEndPos - dashStartPos;
        float totalDistance = dashDir.magnitude;
        if (totalDistance < 0.0001f)
        {
            dashDir = transform.forward;
            totalDistance = 0f;
        }

        if (dashDir.sqrMagnitude < 0.0001f)
        {
            yield break;
        }

        dashDir.Normalize();
        Vector3 trailDir = eAfterimageInvertTrailDirection ? -dashDir : dashDir;
        Vector3 side = Vector3.Cross(Vector3.up, trailDir);
        if (side.sqrMagnitude > 0.0001f)
        {
            side.Normalize();
        }

        int count = Mathf.Max(1, eAfterimageCount);
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0f : i / (float)(count - 1);
            float distance = Mathf.Min(totalDistance, Mathf.Max(0f, eAfterimageNearCharacterOffset) + t * Mathf.Max(0f, eAfterimageTrailLength));
            float distanceFromStart = eAfterimageInvertTrailDirection ? distance : Mathf.Max(0f, totalDistance - distance);
            float sampleT = totalDistance > 0.0001f ? Mathf.Clamp01(distanceFromStart / totalDistance) : 0f;
            Vector3 pos = Vector3.Lerp(dashStartPos, dashEndPos, sampleT);
            if (Mathf.Abs(eAfterimageTrailSideOffset) > 0.0001f)
            {
                pos += side * eAfterimageTrailSideOffset;
            }
            SpawnEAfterimageGhost(sourceSprite, pos, dashStartPos, dashEndPos, i);

            if (eAfterimagePathSpawnDelay > 0f)
            {
                yield return new WaitForSeconds(eAfterimagePathSpawnDelay);
            }
            else
            {
                yield return null;
            }
        }
    }

    private GameObject SpawnEAfterimageGhost(SpriteRenderer sourceSprite, Vector3 worldPosition, Vector3 dashStartPos, Vector3 dashEndPos, int spawnedIndex)
    {
        if (sourceSprite == null || sourceSprite.sprite == null)
        {
            return null;
        }

        GameObject ghost = new GameObject("E_Afterimage_Ghost");
        SpriteRenderer ghostSprite = ghost.AddComponent<SpriteRenderer>();
        ghostSprite.sprite = sourceSprite.sprite;
        ghostSprite.flipX = sourceSprite.flipX;
        ghostSprite.flipY = sourceSprite.flipY;
        ghostSprite.drawMode = sourceSprite.drawMode;
        ghostSprite.size = sourceSprite.size;
        ghostSprite.spriteSortPoint = sourceSprite.spriteSortPoint;
        ghostSprite.maskInteraction = sourceSprite.maskInteraction;
        ghostSprite.sortingLayerID = sourceSprite.sortingLayerID;
        ghostSprite.sortingOrder = sourceSprite.sortingOrder + eAfterimageSortingOrderOffset;

        Color c;
        if (eAfterimageUseRainbow)
        {
            int colorIndex = spawnedIndex;
            if (eAfterimageInvertColorOrder)
            {
                int count = Mathf.Max(1, eAfterimageCount);
                colorIndex = count - 1 - spawnedIndex;
            }

            float hue = Mathf.Repeat(colorIndex * eAfterimageRainbowHueSpeed, 1f);
            c = Color.HSVToRGB(hue, eAfterimageRainbowSaturation, eAfterimageRainbowValue);
        }
        else
        {
            c = eAfterimageTint;
        }
        if (eAfterimageFadeByDistanceToEnd)
        {
            float totalDistance = Vector3.Distance(dashStartPos, dashEndPos);
            float distanceToEnd = Vector3.Distance(worldPosition, dashEndPos);
            float endT = totalDistance <= 0.0001f
                ? 1f
                : 1f - Mathf.Clamp01(distanceToEnd / totalDistance);
            float alphaScale = Mathf.Lerp(Mathf.Clamp01(eAfterimageFarAlphaMultiplier), 1f, endT);
            c.a = Mathf.Max(0.01f, eAfterimageAlpha * alphaScale);
        }
        else if (eAfterimageFadeByAgeIndex)
        {
            int visibleCount = Mathf.Max(1, Mathf.Min(eAfterimageMaxPerDash, Mathf.Max(1, eAfterimageCount)));
            int denominator = Mathf.Max(1, visibleCount - 1);
            float ageT = Mathf.Clamp01(spawnedIndex / (float)denominator);
            float alphaScale = Mathf.Lerp(Mathf.Clamp01(eAfterimageOldestAlphaMultiplier), 1f, ageT);
            c.a = Mathf.Max(0.01f, eAfterimageAlpha * alphaScale);
        }
        else
        {
            c.a = Mathf.Max(0.2f, eAfterimageAlpha);
        }
        ghostSprite.color = c;

        ghost.transform.position = worldPosition;
        ghost.transform.rotation = sourceSprite.transform.rotation;
        ghost.transform.localScale = Vector3.Scale(sourceSprite.transform.lossyScale, eAfterimageScale);

        if (eAfterimageDebugLog)
        {
            Debug.Log($"Afterimage pos={worldPosition}, end={dashEndPos}, distanceToEnd={Vector3.Distance(worldPosition, dashEndPos)}, alpha={c.a}", this);
        }

        StartCoroutine(FadeAndDestroySpriteGhost(ghost, ghostSprite, eAfterimageDuration));
        return ghost;
    }

    private bool HasAliveEStarFallBlades()
    {
        for (int i = 0; i < activeEStarFallBlades.Count; i++)
        {
            EStarFallBladeData data = activeEStarFallBlades[i];
            if (data != null && data.sword != null)
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator EStarFallRoutine(Vector3 startPos, Vector3 endPos)
    {
        CleanupEStarFallBlades();

        if (eStarFallBladeCount <= 0)
        {
            yield break;
        }

        int bladeCount = Mathf.Max(1, eStarFallBladeCount);
        float spawnHeight = Mathf.Max(0f, eStarFallSpawnHeight);
        float fallSpeed = Mathf.Max(0.1f, eStarFallFallSpeed);
        float randomDelay = Mathf.Max(0f, eStarFallRandomDelay);
        float sequentialDelay = Mathf.Max(0f, eStarFallSequentialDelay);

        for (int i = 0; i < bladeCount; i++)
        {
            float t = bladeCount <= 1 ? 1f : i / (float)(bladeCount - 1);
            Vector3 target = eStarFallUseDashPath
                ? Vector3.Lerp(startPos, endPos, t)
                : endPos;

            if (eStarFallUseDashPath)
            {
                Vector2 jitter2D = Random.insideUnitCircle * Mathf.Max(0f, eStarFallPathJitter);
                target += new Vector3(jitter2D.x, 0f, jitter2D.y);
            }
            else
            {
                Vector2 randomOffset2D = Random.insideUnitCircle * Mathf.Max(0f, eStarFallRadius);
                target += new Vector3(randomOffset2D.x, 0f, randomOffset2D.y);
            }

            Vector3 spawn = target + Vector3.up * spawnHeight;

            GameObject blade = CreateSkillEffectVisual(
                $"E_StarFall_{i}",
                eSkillEffectPrefab,
                spawn,
                Vector3.down,
                false,
                false,
                0f,
                0f,
                0f,
                0f,
                ResolveVisualScale(eEffectScale, eEffectPlaneScale));

            if (blade == null)
            {
                continue;
            }

            EnsureEffectVisible(blade);

            SkillEffectRuntime runtime = blade.GetComponent<SkillEffectRuntime>();
            Transform visualTransform = runtime != null && runtime.visual != null ? runtime.visual : FindEffectVisualTransform(blade);
            EStarFallBladeData data = new EStarFallBladeData
            {
                sword = blade,
                runtime = runtime,
                visualTransform = visualTransform,
                spawnPosition = spawn,
                targetPosition = target,
                delay = (eStarFallUseDashPath ? i * sequentialDelay : 0f) + Random.Range(0f, randomDelay),
                fallDuration = Mathf.Max(0.05f, Vector3.Distance(spawn, target) / fallSpeed),
                elapsed = 0f,
                impactApplied = false
            };

            activeEStarFallBlades.Add(data);
            ApplyEStarFallVisualRotation(data, Vector3.down);
        }

        float maxLife = 0f;
        for (int i = 0; i < activeEStarFallBlades.Count; i++)
        {
            EStarFallBladeData data = activeEStarFallBlades[i];
            if (data != null)
            {
                maxLife = Mathf.Max(maxLife, data.delay + data.fallDuration);
            }
        }

        float elapsed = 0f;
        while (elapsed < maxLife || HasAliveEStarFallBlades())
        {
            for (int i = activeEStarFallBlades.Count - 1; i >= 0; i--)
            {
                EStarFallBladeData data = activeEStarFallBlades[i];
                if (data == null || data.sword == null)
                {
                    activeEStarFallBlades.RemoveAt(i);
                    continue;
                }

                data.elapsed += Time.deltaTime;
                if (data.elapsed < data.delay)
                {
                    data.sword.transform.position = data.spawnPosition;
                    ApplyEStarFallVisualRotation(data, Vector3.down);
                    continue;
                }

                float fallT = Mathf.Clamp01((data.elapsed - data.delay) / Mathf.Max(0.05f, data.fallDuration));
                float smoothFallT = Mathf.SmoothStep(0f, 1f, fallT);
                Vector3 pos = Vector3.Lerp(data.spawnPosition, data.targetPosition, smoothFallT);
                data.sword.transform.position = pos;
                ApplyEStarFallVisualRotation(data, data.targetPosition - pos);

                if (fallT >= 1f && !data.impactApplied)
                {
                    data.impactApplied = true;
                    if (eEnableStarFallDamage)
                    {
                        float damage = Mathf.Max(0.01f, rSwarmDamagePerTick * Mathf.Max(0f, eStarFallDamageMultiplier));
                        ApplyRSwarmAreaDamage(data.targetPosition, Mathf.Max(0.01f, eStarFallDamageRadius), damage);
                    }

                    Destroy(data.sword);
                    activeEStarFallBlades.RemoveAt(i);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        CleanupEStarFallBlades();
    }

    private void CleanupEStarFallBlades()
    {
        for (int i = 0; i < activeEStarFallBlades.Count; i++)
        {
            EStarFallBladeData data = activeEStarFallBlades[i];
            if (data != null && data.sword != null)
            {
                Destroy(data.sword);
            }
        }

        activeEStarFallBlades.Clear();
    }

    private void AddStandbySword()
    {
        if (standbySwords >= maxStandbySwords) return;

        standbySwords += 1;
        Vector3 orbitOffset = Quaternion.Euler(0f, standbySwords * 360f / maxStandbySwords, 0f) * Vector3.forward * 1.1f;
        GameObject standby = CreateSkillEffectVisual(
            "StandbySword",
            standbySkillEffectPrefab,
            transform.position + Vector3.up + orbitOffset + standbySwordOffset,
            orbitOffset,
            false,
            false,
            0f,
            standbySwordVisualPitch,
            standbySwordVisualYaw,
            standbySwordVisualRoll + ResolveRotation(standbySwordRotationZ),
            ResolveVisualScale(standbySwordScale, standbySwordPlaneScale));
        standbySwordVisuals.Add(standby);
        StartCoroutine(OrbitStandbySword(standby, standbySwords - 1));
    }

    private IEnumerator OrbitStandbySword(GameObject standby, int index)
    {
        SkillEffectRuntime runtime = standby != null ? standby.GetComponent<SkillEffectRuntime>() : null;
        while (standby != null && standbySwords > 0)
        {
            float angle = Time.time * 120f + index * 120f;
            Vector3 orbitOffset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 1.1f;
            standby.transform.position = transform.position + Vector3.up + orbitOffset + standbySwordOffset;

            if (runtime != null && runtime.visual != null && standbySwordSpinSpeed != 0f)
            {
                runtime.visual.Rotate(Vector3.up, standbySwordSpinSpeed * Time.deltaTime, Space.Self);
            }

            yield return null;
        }
    }

    private void LaunchStandbySwords(Vector3 dir, float speed)
    {
        foreach (GameObject standby in standbySwordVisuals)
        {
            if (standby == null) continue;
            if (!useRawPrefabRotationForSkillEffects)
            {
                ApplyRootDirection(standby.transform, dir, true, false, 0f);
            }
            StartCoroutine(FireAfterDelay(standby, dir, 0f, speed));
        }

        standbySwordVisuals.Clear();
        standbySwords = 0;
    }

    private Vector3 ResolveFacingDirection()
    {
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            if (v.sqrMagnitude > 0.0001f)
            {
                lastMoveDir = v.normalized;
                return lastMoveDir;
            }
        }

        if (lastMoveDir.sqrMagnitude > 0.0001f)
        {
            return lastMoveDir.normalized;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0.0001f)
        {
            return forward.normalized;
        }

        return Vector3.forward;
    }

    private Vector3 ResolveRSwarmForward()
    {
        Vector3 forward = Vector3.forward;
        Camera renderCamera = ResolveRRenderCamera();
        if (rSwarmUseCameraForward && renderCamera != null)
        {
            forward = renderCamera.transform.forward;
        }
        else
        {
            forward = transform.forward;
        }

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = ResolveFacingDirection();
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        return forward.normalized;
    }

    private Vector3 ResolveRSwarmCenter()
    {
        Vector3 center = transform.position;
        if (rApplyEffectOffsetToSwarmCenter)
        {
            center += rEffectOffset;
        }

        if (!rSwarmCenterOnPlayer)
        {
            center += ResolveRSwarmForward() * rSwarmForwardOffset;
        }

        return center;
    }

    private Camera ResolveRRenderCamera()
    {
        if (rRenderCamera != null && rRenderCamera.isActiveAndEnabled)
        {
            resolvedRRenderCamera = rRenderCamera;
            return resolvedRRenderCamera;
        }

        if (!rAutoResolveRenderCamera)
        {
            return resolvedRRenderCamera;
        }

        if (resolvedRRenderCamera != null && resolvedRRenderCamera.isActiveAndEnabled)
        {
            return resolvedRRenderCamera;
        }

        PlayerCameraRig cameraRig = FindObjectOfType<PlayerCameraRig>();
        if (cameraRig != null)
        {
            Camera rigCamera = cameraRig.GetComponent<Camera>();
            if (rigCamera == null)
            {
                rigCamera = cameraRig.GetComponentInChildren<Camera>(true);
            }

            if (rigCamera != null && rigCamera.isActiveAndEnabled)
            {
                resolvedRRenderCamera = rigCamera;
                return resolvedRRenderCamera;
            }
        }

        if (Camera.main != null && Camera.main.isActiveAndEnabled)
        {
            resolvedRRenderCamera = Camera.main;
            return resolvedRRenderCamera;
        }

        Camera[] allCameras = Camera.allCameras;
        for (int i = 0; i < allCameras.Length; i++)
        {
            Camera cam = allCameras[i];
            if (cam != null && cam.isActiveAndEnabled && cam.cameraType == CameraType.Game)
            {
                resolvedRRenderCamera = cam;
                return resolvedRRenderCamera;
            }
        }

        return resolvedRRenderCamera;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
        {
            return;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            all[i].gameObject.layer = layer;
        }
    }

    private GameObject CreateSkillEffectVisual(
        string name,
        GameObject specificPrefab,
        Vector3 worldPosition,
        Vector3 direction,
        bool alignToDirection,
        bool invertForward,
        float yawOffset,
        float visualPitch,
        float visualYaw,
        float visualRoll,
        Vector3 visualScale)
    {
        GameObject root = new GameObject(name);
        root.transform.position = worldPosition;
        ApplyRootDirection(root.transform, direction, alignToDirection, invertForward, yawOffset);

        GameObject effectVisual = CreateEffectInstance(name, specificPrefab, root.transform.position, root.transform.rotation, useRawPrefabRotationForSkillEffects);
        if (effectVisual == null)
        {
            Destroy(root);
            return null;
        }

        effectVisual.transform.SetParent(root.transform, true);

        Transform visualTarget = FindEffectVisualTransform(effectVisual);
        if (useRawPrefabRotationForSkillEffects)
        {
            effectVisual.transform.rotation = root.transform.rotation;
            float rawScaleMultiplier = Mathf.Max(0.01f, skillEffectPrefabScaleMultiplier);
            effectVisual.transform.localScale = effectVisual.transform.localScale * rawScaleMultiplier;
        }
        else
        {
            visualTarget.localRotation = BuildQuadOffsetRotation(visualPitch, visualYaw, visualRoll);
            visualTarget.localScale = Vector3.Scale(visualTarget.localScale, ClampVisualScale(visualScale));
        }
        EnsureEffectVisible(effectVisual);

        SkillEffectRuntime runtime = root.AddComponent<SkillEffectRuntime>();
        runtime.visual = visualTarget;
        runtime.baseVisualScale = visualTarget.localScale;
        CacheFadeTargets(effectVisual, runtime);
        runtimeSkillVisualRoots.Add(root);

        return root;
    }

    private static Vector3 ClampVisualScale(Vector3 scale)
    {
        return new Vector3(
            ClampScaleAxis(scale.x),
            ClampScaleAxis(scale.y),
            ClampScaleAxis(scale.z));
    }

    private static float ClampScaleAxis(float value)
    {
        const float minAbs = 0.01f;
        if (Mathf.Abs(value) >= minAbs)
        {
            return value;
        }

        return value < 0f ? -minAbs : minAbs;
    }

    private static bool HasAliveObjects(List<GameObject> objects)
    {
        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureEffectVisible(GameObject effectRoot)
    {
        if (effectRoot == null)
        {
            return;
        }

        effectRoot.SetActive(true);
        Renderer[] renderers = effectRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
        }
    }

    private static void TrySetDoubleSidedIfSupported(GameObject effectRoot)
    {
        if (effectRoot == null)
        {
            return;
        }

        Renderer[] renderers = effectRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null || !mat.HasProperty("_Cull"))
                {
                    continue;
                }

                mat.SetFloat("_Cull", 0f);
            }
        }
    }

    private GameObject CreateEffectInstance(string effectName, GameObject specificPrefab, Vector3 position, Quaternion rotation, bool preservePrefabRotation)
    {
        GameObject sourcePrefab = specificPrefab != null ? specificPrefab : sharedSkillEffectPrefab;
        if (sourcePrefab != null)
        {
            GameObject instance;
            if (preservePrefabRotation)
            {
                instance = Instantiate(sourcePrefab);
                instance.transform.position = position;
            }
            else
            {
                instance = Instantiate(sourcePrefab, position, rotation);
            }

            return instance;
        }

        Debug.LogWarning($"[Player2PrototypeController] Missing skill effect prefab for '{effectName}' on {name}. Assign specific prefab or Shared Skill Effect Prefab.", this);
        return null;
    }

    private static Transform FindEffectVisualTransform(GameObject root)
    {
        MeshRenderer rootMesh = root.GetComponent<MeshRenderer>();
        if (rootMesh != null) return root.transform;

        MeshRenderer childMesh = root.GetComponentInChildren<MeshRenderer>(true);
        if (childMesh != null) return childMesh.transform;

        SpriteRenderer rootSprite = root.GetComponent<SpriteRenderer>();
        if (rootSprite != null) return root.transform;

        SpriteRenderer childSprite = root.GetComponentInChildren<SpriteRenderer>(true);
        if (childSprite != null) return childSprite.transform;

        return root.transform;
    }

    private void CacheFadeTargets(GameObject effectVisualRoot, SkillEffectRuntime runtime)
    {
        List<Material> mats = new List<Material>();
        List<Color> matColors = new List<Color>();

        Renderer[] renderers = effectVisualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] instanceMats = renderers[i].materials;
            for (int m = 0; m < instanceMats.Length; m++)
            {
                Material mat = instanceMats[m];
                if (mat == null) continue;
                mats.Add(mat);
                matColors.Add(GetMaterialColor(mat));
            }
        }

        runtime.materialTargets = mats.ToArray();
        runtime.materialBaseColors = matColors.ToArray();

        runtime.spriteTargets = effectVisualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        runtime.spriteBaseColors = new Color[runtime.spriteTargets.Length];
        for (int i = 0; i < runtime.spriteTargets.Length; i++)
        {
            runtime.spriteBaseColors[i] = runtime.spriteTargets[i].color;
        }
    }

    private static void ApplyFadeAlpha(SkillEffectRuntime runtime, float alpha)
    {
        if (runtime == null)
        {
            return;
        }

        if (runtime.materialTargets != null)
        {
            for (int i = 0; i < runtime.materialTargets.Length; i++)
            {
                Material mat = runtime.materialTargets[i];
                if (mat == null) continue;
                Color baseColor = i < runtime.materialBaseColors.Length ? runtime.materialBaseColors[i] : Color.white;
                SetMaterialColor(mat, new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha));
            }
        }

        if (runtime.spriteTargets != null)
        {
            for (int i = 0; i < runtime.spriteTargets.Length; i++)
            {
                SpriteRenderer sr = runtime.spriteTargets[i];
                if (sr == null) continue;
                Color baseColor = i < runtime.spriteBaseColors.Length ? runtime.spriteBaseColors[i] : Color.white;
                sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
            }
        }
    }

    private static Quaternion BuildQuadOffsetRotation(float pitch, float yaw, float roll)
    {
        return Quaternion.Euler(NormalizeQuadLegacyPitch(pitch), yaw, roll);
    }

    private static float NormalizeQuadLegacyPitch(float pitch)
    {
        float absPitch = Mathf.Abs(pitch);
        if (absPitch < 0.0001f)
        {
            return 0f;
        }

        if (Mathf.Abs(absPitch - 90f) <= 0.01f || Mathf.Abs(absPitch - 180f) <= 0.01f)
        {
            return 0f;
        }

        return pitch;
    }

    private static float NormalizeQuadLegacyRoll(float roll)
    {
        float absRoll = Mathf.Abs(roll);
        if (absRoll < 0.0001f)
        {
            return 0f;
        }

        if (Mathf.Abs(absRoll - 90f) <= 0.01f)
        {
            return 0f;
        }

        return roll;
    }

    private static void ApplyRootDirection(Transform root, Vector3 direction, bool alignToDirection, bool invertForward, float yawOffset)
    {
        float yaw = 0f;
        if (alignToDirection && direction.sqrMagnitude > 0.0001f)
        {
            yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        if (invertForward)
        {
            yaw += 180f;
        }

        root.rotation = Quaternion.Euler(0f, yaw + yawOffset, 0f);
    }

    private Vector3 ResolveVisualScale(Vector3 specificScale, Vector3 planeScale)
    {
        Vector3 baseScale = sharedEffectScale;
        Vector3 roleScale = specificScale.sqrMagnitude > 0.0001f ? specificScale : Vector3.one;
        Vector3 quadScale = planeScale.sqrMagnitude > 0.0001f ? planeScale : Vector3.one;
        return new Vector3(
            baseScale.x * roleScale.x * quadScale.x,
            baseScale.y * roleScale.y * quadScale.y,
            baseScale.z * roleScale.z * quadScale.z);
    }

    private Vector3 ResolveVisualScaleWithoutShared(Vector3 specificScale)
    {
        Vector3 roleScale = specificScale.sqrMagnitude > 0.0001f ? specificScale : Vector3.one;
        return ClampVisualScale(roleScale);
    }

    private static void ApplyRootVisualScale(GameObject effectRoot, Vector3 visualScale)
    {
        if (effectRoot == null)
        {
            return;
        }

        effectRoot.transform.localScale = ClampVisualScale(visualScale);
    }

    private float ResolveRotation(float specificRotationZ)
    {
        return sharedEffectRotationZ + NormalizeQuadLegacyRoll(specificRotationZ);
    }

    private static Vector3 GetOrbitPositionXZ(float angleDegrees, float radius, float height)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        return new Vector3(
            Mathf.Cos(rad) * radius,
            height,
            Mathf.Sin(rad) * radius);
    }

    private static float GetOrbitTangentYawXZ(float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        Vector3 tangent = new Vector3(
            -Mathf.Sin(rad),
            0f,
            Mathf.Cos(rad));
        return Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg;
    }

    private static float GetYawFromDirectionXZ(Vector3 direction, float fallbackYaw)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return fallbackYaw;
        }

        direction.Normalize();
        return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    }

    private Quaternion BuildRVisibleBaseRotation()
    {
        return
            (rFlipPlaneFrontBack ? Quaternion.Euler(0f, rPlaneFrontBackFlipEuler.y, 0f) : Quaternion.identity) *
            BuildQuadOffsetRotation(rEffectVisualPitch, rEffectVisualYaw, rEffectVisualRoll);
    }

    private void ApplyRSwarmVisualRotation(RSwarmSwordData data, float yaw)
    {
        if (data == null || data.visualTransform == null)
        {
            return;
        }

        data.visualTransform.rotation = Quaternion.Euler(0f, yaw, 0f) * BuildRVisibleBaseRotation();
    }

    private Quaternion BuildRStarRainVisibleRotation()
    {
        if (rStarRainUseForcedVisualRotation)
        {
            return BuildQuadOffsetRotation(rStarRainForcedVisualEuler.x, rStarRainForcedVisualEuler.y, rStarRainForcedVisualEuler.z) *
                   BuildQuadOffsetRotation(rStarRainVisualEulerOffset.x, rStarRainVisualEulerOffset.y, rStarRainVisualEulerOffset.z);
        }

        return BuildRVisibleBaseRotation() * BuildQuadOffsetRotation(rStarRainVisualEulerOffset.x, rStarRainVisualEulerOffset.y, rStarRainVisualEulerOffset.z);
    }

    private void ApplyRStarRainVisualRotation(RStarRainBladeData data)
    {
        if (data == null || data.visualTransform == null)
        {
            return;
        }

        Quaternion finalRotation = BuildRStarRainVisibleRotation();
        data.sword.transform.rotation = finalRotation;
        data.visualTransform.rotation = finalRotation;
    }

    private Quaternion BuildEStarFallVisibleRotation(Vector3 fallDir)
    {
        if (eStarFallUseForcedVisualRotation)
        {
            return BuildQuadOffsetRotation(eStarFallForcedVisualEuler.x, eStarFallForcedVisualEuler.y, eStarFallForcedVisualEuler.z) *
                   BuildQuadOffsetRotation(eStarFallVisualEulerOffset.x, eStarFallVisualEulerOffset.y, eStarFallVisualEulerOffset.z);
        }

        Vector3 dir = fallDir;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector3.down;
        }
        dir.Normalize();

        Quaternion fallRotation = Quaternion.LookRotation(dir, Vector3.up);
        return fallRotation * BuildQuadOffsetRotation(eStarFallVisualEulerOffset.x, eStarFallVisualEulerOffset.y, eStarFallVisualEulerOffset.z);
    }

    private void ApplyEStarFallVisualRotation(EStarFallBladeData data, Vector3 fallDir)
    {
        if (data == null || data.visualTransform == null)
        {
            return;
        }

        Quaternion finalRotation = BuildEStarFallVisibleRotation(fallDir);
        data.sword.transform.rotation = finalRotation;
        data.visualTransform.rotation = finalRotation;
    }

    private static Color GetMaterialColor(Material mat)
    {
        if (mat == null) return Color.white;
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
        if (mat.HasProperty("_TintColor")) return mat.GetColor("_TintColor");
        return Color.white;
    }

    private static void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", color);
    }

    // 鍐呴儴鍒ゆ柇锛氭妧鑳芥槸鍚︽弧瓒抽噴鏀炬潯浠?
    // 鍐呴儴鍒ゆ柇锛氭妧鑳芥槸鍚︽弧瓒抽噴鏀炬潯浠?
    private bool CanCastSkill(int index)
    {
        if (cooldownManager == null) return true;
        return cooldownManager.IsSkillCastable(index);
    }

    // 鍐呴儴娑堣€楋細鎵ｈ摑 + 杩涘叆鍐峰嵈
    private bool TryConsumeSkill(int index)
    {
        if (cooldownManager == null)
        {
            return true;
        }

        return cooldownManager.TryConsumeSkillResource(index);
    }

    // ========== UI 鍙鎺ュ彛锛岀粰鎶€鑳芥爮UI璋冪敤 ==========
    public float GetSkillCurrentCD(int index)
    {
        if (cooldownManager == null) return 0f;
        return cooldownManager.GetCurrentSkillCD(index);
    }

    public float GetSkillMaxCD(int index)
    {
        if (cooldownManager == null) return 0f;
        return cooldownManager.GetSkillMaxCD(index);
    }

    public float GetSkillManaCost(int index)
    {
        if (cooldownManager == null) return 0f;
        return cooldownManager.GetSkillManaCost(index);
    }

    public float GetCurrentMana()
    {
        if (cooldownManager == null) return 0f;
        return cooldownManager.GetCurrentMana();
    }

    public bool IsSkillReady(int index)
    {
        if (cooldownManager == null) return true;
        return cooldownManager.IsSkillReady(index);
    }
}
