using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class Player2PrototypeController : MonoBehaviour
{
    [Header("移动")]
    [HideInInspector] public float moveSpeed = 5f;
    public float dashDistance = 4f;
    public float dashDuration = 0.15f;
    [SerializeField] private bool lockCharacterRotation = true;

    [Header("Q - 神临光剑 / 基础")]
    public float qDelay = 0.35f;
    public float qSwordSpeed = 14f;

    [Header("W - 圣轮偏转 / 基础")]
    [InspectorName("W 持续时间")]
    public float wDuration = 1.5f;
    [InspectorName("W 基础减伤")]
    public float wDamageReduction = 0.4f;
    [HideInInspector] public int maxStandbySwords = 3;

    [Header("W - 圣轮偏转 / 防御加成")]
    [InspectorName("W 每把剑减伤加成")]
    public float wDamageReductionPerSword = 0.03f;
    [InspectorName("W 最大减伤")]
    public float wMaxDamageReduction = 0.8f;
    [InspectorName("W 反击伤害比例")]
    public float wCounterDamageRatio = 0.5f;

    [Header("E - 天轨换位 / 基础")]
    public float eRailDuration = 0.6f;

    [Header("E - 天轨换位 / Shader 残影")]
    [InspectorName("E 残影启用")]
    public bool eEnableAfterimageShader = true;
    [InspectorName("E 残影来源 Renderer")]
    public Renderer eAfterimageSourceRenderer;
    [InspectorName("E 残影材质")]
    public Material eAfterimageMaterial;
    [InspectorName("E 残影数量")]
    public int eAfterimageCount = 8;
    [InspectorName("E 残影时长")]
    public float eAfterimageDuration = 0.35f;
    [InspectorName("E 残影透明度")]
    public float eAfterimageAlpha = 0.45f;
    [InspectorName("E 残影生成间隔")]
    public float eAfterimageSpawnInterval = 0.03f;
    [InspectorName("E 残影缩放")]
    public Vector3 eAfterimageScale = Vector3.one;
    [InspectorName("E 残影染色")]
    public Color eAfterimageTint = new Color(0.6f, 0.85f, 1f, 0.45f);
    [InspectorName("E 残影 SortingOrder 偏移")]
    public int eAfterimageSortingOrderOffset = -1;

    [Header("E - 天轨换位 / 残影 Legacy")]
    [FormerlySerializedAs("eEnableAfterimage")]
    [HideInInspector] public bool eEnableAfterimageLegacy = false;
    [FormerlySerializedAs("eAfterimageCount")]
    [HideInInspector] public int eAfterimageCountLegacy = 4;
    [FormerlySerializedAs("eAfterimageDuration")]
    [HideInInspector] public float eAfterimageDurationLegacy = 0.35f;
    [FormerlySerializedAs("eAfterimageAlpha")]
    [HideInInspector] public float eAfterimageAlphaLegacy = 0.45f;
    [FormerlySerializedAs("eAfterimageScale")]
    [HideInInspector] public Vector3 eAfterimageScaleLegacy = new Vector3(1f, 1f, 1f);
    [FormerlySerializedAs("eAfterimageTint")]
    [HideInInspector] public Color eAfterimageTintLegacy = new Color(0.6f, 0.9f, 1f, 0.45f);

    [Header("Legacy - E 星落未使用")]
    [InspectorName("E 星落启用")]
    public bool eEnableStarFall = false;
    [InspectorName("E 星落剑数量")]
    public int eStarFallBladeCount = 7;
    [InspectorName("E 星落半径")]
    public float eStarFallRadius = 2.5f;
    [InspectorName("E 星落生成高度")]
    public float eStarFallSpawnHeight = 4f;
    [InspectorName("E 星落下落速度")]
    public float eStarFallFallSpeed = 10f;
    [InspectorName("E 星落随机延迟")]
    public float eStarFallRandomDelay = 0.08f;
    [InspectorName("E 星落伤害半径")]
    public float eStarFallDamageRadius = 0.8f;
    [InspectorName("E 星落伤害倍率")]
    public float eStarFallDamageMultiplier = 0.5f;
    [InspectorName("E 星落启用伤害")]
    public bool eEnableStarFallDamage = false;
    [InspectorName("E 星落强制视觉旋转")]
    public bool eStarFallUseForcedVisualRotation = true;
    [InspectorName("E 星落强制旋转角度")]
    public Vector3 eStarFallForcedVisualEuler = new Vector3(0f, 0f, 90f);
    [InspectorName("E 星落视觉偏移")]
    public Vector3 eStarFallVisualEulerOffset = Vector3.zero;
    [InspectorName("E 星落使用位移路径")]
    public bool eStarFallUseDashPath = true;
    [InspectorName("E 星落路径扰动")]
    public float eStarFallPathJitter = 0.35f;
    [InspectorName("E 星落顺序延迟")]
    public float eStarFallSequentialDelay = 0.06f;

    [Header("神印点")]
    [InspectorName("Current Divine Mark")]
    public int currentSwordEnergy = 0;

    [Header("R - 神眷星雨 / 基础")]
    [FormerlySerializedAs("swordEnergy")]
    [InspectorName("R 初始剑数量")]
    public int rBaseSwordCount = 1;

    [Header("技能特效预制体")]
    public GameObject sharedSkillEffectPrefab;
    public GameObject qSkillEffectPrefab;
    public GameObject wSkillEffectPrefab;
    public GameObject eSkillEffectPrefab;
    public GameObject rSkillEffectPrefab;
    public GameObject standbySkillEffectPrefab;

    [Header("技能特效通用设置")]
    public bool useRawPrefabRotationForSkillEffects = true;
    public Vector3 skillEffectPrefabBaseRotation = new Vector3(180.618f, 91.603f, -89.927f);
    public float skillEffectPrefabScaleMultiplier = 1f;
    public Vector3 sharedEffectScale = new Vector3(1f, 1f, 1f);
    public float sharedEffectRotationZ = 0f;

    [Header("Q - 神临光剑 / 视觉")]
    public Vector3 qEffectScale = new Vector3(0.25f, 0.25f, 0.25f);
    public float qEffectRotationZ = -90f;
    public Vector3 qEffectOffset = Vector3.zero;
    public Vector3 qEffectPlaneScale = new Vector3(0.25f, 0.25f, 1f);
    public float qEffectYawOffset = 0f;
    public float qEffectVisualPitch = 0f;
    public float qEffectVisualYaw = 0f;
    public float qEffectVisualRoll = 0f;
    public bool qEffectInvertForward = false;

    [Header("W - 圣轮偏转 / 剑阵视觉")]
    [InspectorName("W 尺寸")]
    public Vector3 wEffectScale = new Vector3(0.3f, 0.3f, 0.3f);
    [HideInInspector] public float wEffectRotationZ = 0f;
    [HideInInspector] public Vector3 wEffectOffset = Vector3.zero;
    [HideInInspector] public Vector3 wEffectPlaneScale = new Vector3(0.25f, 0.25f, 0.25f);

    [Header("W - 圣轮偏转 / 尺寸倍率")]
    [InspectorName("W 尺寸倍率")]
    public float wEffectScaleMultiplier = 1f;
    [HideInInspector] public bool wEffectVerticalRotation = true;
    [HideInInspector] public Vector3 wEffectSpinAxis = Vector3.up;
    [HideInInspector] public float wEffectVisualPitch = 0f;
    [HideInInspector] public float wEffectVisualYaw = 0f;
    [HideInInspector] public float wEffectVisualRoll = 0f;
    [HideInInspector] public int wSwordCount = 3;

    [Header("W - 圣轮偏转 / 剑阵设置")]
    [InspectorName("W 初始剑数量")]
    public int baseWSwordCount = 3;
    [InspectorName("W 使用剑气值")]
    public bool useSwordEnergyForW = true;
    [InspectorName("W 最大剑数量")]
    public int maxWSwordCount = 15;
    [InspectorName("W 环绕半径")]
    public float wEffectOrbitRadius = 1.2f;
    [InspectorName("W 高度")]
    public float wEffectHeight = 1.1f;
    [InspectorName("W 环绕速度")]
    public float wEffectOrbitSpeed = 80f;
    [InspectorName("W 切线偏移角")]
    public float wSwordOrbitYawOffset = 90f;
    [HideInInspector] public bool wEffectFaceCamera = true;
    [FormerlySerializedAs("wEffectSpinSpeed")]
    [HideInInspector] public float wEffectSelfSpinSpeed = 0f;

    [Header("W - 圣轮偏转 / 剑气加成")]
    [InspectorName("W 每点剑气增加持续时间")]
    public float wDurationPerSwordEnergy = 0f;
    [InspectorName("W 最大持续时间加成")]
    public float wMaxDurationBonus = 0f;
    [InspectorName("W 每点剑气增加环绕速度")]
    public float wOrbitSpeedPerSwordEnergy = 0f;
    [InspectorName("W 最大环绕速度加成")]
    public float wMaxOrbitSpeedBonus = 0f;
    [InspectorName("W 每点剑气增加半径")]
    public float wRadiusPerSwordEnergy = 0f;
    [InspectorName("W 最大半径加成")]
    public float wMaxRadiusBonus = 0f;

    [HideInInspector] public float wOrbitRadiusMin = 0.9f;
    [HideInInspector] public float wOrbitRadiusMax = 1.8f;
    [HideInInspector] public float wHeightMin = 0.2f;
    [HideInInspector] public float wHeightMax = 1.2f;
    [HideInInspector] public float wOrbitSpeedMin = 60f;
    [HideInInspector] public float wOrbitSpeedMax = 120f;
    [HideInInspector] public float wBobAmplitudeMin = 0.05f;
    [HideInInspector] public float wBobAmplitudeMax = 0.25f;
    [HideInInspector] public float wBobFrequencyMin = 0.8f;
    [HideInInspector] public float wBobFrequencyMax = 2.0f;
    [HideInInspector] public float wSwingAngleMin = 3f;
    [HideInInspector] public float wSwingAngleMax = 12f;
    [HideInInspector] public float wRadiusJitter = 0.12f;
    [HideInInspector] public float wAngularJitter = 10f;
    [HideInInspector] public bool wClockwise = true;
    [HideInInspector] public bool wFaceOrbitDirection = true;
    [HideInInspector] public float wOrbitDirectionYawOffset = 0f;
    [HideInInspector] public float wOrbitDirectionPitchOffset = 0f;
    [HideInInspector] public float wOrbitDirectionRollOffset = 0f;
    [HideInInspector] public bool wKeepSwordVisibleToCamera = true;

    [Header("E - 天轨换位 / Legacy 旧视觉")]
    [HideInInspector] public Vector3 eEffectScale = new Vector3(0.35f, 0.35f, 0.35f);
    [HideInInspector] public float eEffectRotationZ = -90f;
    [HideInInspector] public Vector3 eEffectOffset = Vector3.zero;
    [HideInInspector] public Vector3 eEffectPlaneScale = new Vector3(0.35f, 0.35f, 1f);
    [HideInInspector] public float eEffectYawOffset = 0f;
    [HideInInspector] public float eEffectVisualPitch = 0f;
    [HideInInspector] public float eEffectVisualYaw = 0f;
    [HideInInspector] public float eEffectVisualRoll = 0f;

    [Header("R - 神眷星雨 / 视觉")]
    [InspectorName("R 尺寸")]
    public Vector3 rEffectScale = new Vector3(0.3f, 0.3f, 0.3f);
    [HideInInspector] public float rEffectRotationZ = -90f;
    [HideInInspector] public Vector3 rEffectOffset = Vector3.zero;
    [HideInInspector] public Vector3 rEffectPlaneScale = new Vector3(0.3f, 0.3f, 1f);
    [HideInInspector] public float rEffectYawOffset = 0f;
    [InspectorName("R 显示 Pitch")]
    public float rEffectVisualPitch = 90f;
    [InspectorName("R 显示 Yaw")]
    public float rEffectVisualYaw = 0f;
    [InspectorName("R 显示 Roll")]
    public float rEffectVisualRoll = 0f;
    [HideInInspector] public bool rEffectInvertForward = false;
    [Header("R - 神眷星雨 / 万剑漩涡")]
    [InspectorName("R 持续时间")]
    public float rSwarmDuration = 2.0f;
    [InspectorName("R 最小半径")]
    public float rSwarmRadiusMin = 0.8f;
    [InspectorName("R 最大半径")]
    public float rSwarmRadiusMax = 3.2f;
    [InspectorName("R 最低高度")]
    public float rSwarmHeightMin = 0.4f;
    [InspectorName("R 最高高度")]
    public float rSwarmHeightMax = 3.0f;
    [InspectorName("R 最小旋转速度")]
    public float rSwarmSpeedMin = 120f;
    [InspectorName("R 最大旋转速度")]
    public float rSwarmSpeedMax = 300f;
    [InspectorName("R 最小起伏幅度")]
    public float rSwarmBobAmplitudeMin = 0.05f;
    [InspectorName("R 最大起伏幅度")]
    public float rSwarmBobAmplitudeMax = 0.35f;
    [InspectorName("R 最小起伏频率")]
    public float rSwarmBobFrequencyMin = 0.8f;
    [InspectorName("R 最大起伏频率")]
    public float rSwarmBobFrequencyMax = 2.5f;
    [InspectorName("R 半径扰动")]
    public float rSwarmRadiusJitter = 0.25f;
    [InspectorName("R 顺时针")]
    public bool rSwarmClockwise = true;
    [InspectorName("R 前方偏移")]
    public float rSwarmForwardOffset = 2.0f;
    [Header("R - 神眷星雨 / 漩涡中心")]
    [InspectorName("R 切线偏移角")]
    public float rSwarmYawOffset = 0f;
    [InspectorName("R 像角色一样面向镜头")]
    public bool rBillboardLikePlayer = true;
    [InspectorName("R 渲染相机")]
    public Camera rRenderCamera;
    [InspectorName("R 自动查找相机")]
    public bool rAutoResolveRenderCamera = true;
    [InspectorName("R 使用相机前方")]
    public bool rSwarmUseCameraForward = true;
    [InspectorName("R 围绕角色中心")]
    public bool rSwarmCenterOnPlayer = false;
    [InspectorName("R 应用特效偏移到中心")]
    public bool rApplyEffectOffsetToSwarmCenter = false;
    [HideInInspector] public bool rUseTangentFacing = true;
    [InspectorName("R 平面竖直角度")]
    public Vector3 rPlaneUprightEuler = new Vector3(90f, 0f, 0f);
    [InspectorName("R 面向相机角度")]
    public Vector3 rPlaneFaceCameraEuler = new Vector3(0f, 90f, 0f);
    [Header("R - 神眷星雨 / 显示正反修正")]
    [InspectorName("R 翻转正反面")]
    public bool rFlipPlaneFrontBack = true;
    [InspectorName("R 正反面翻转角度")]
    public Vector3 rPlaneFrontBackFlipEuler = new Vector3(0f, 180f, 0f);
    [HideInInspector] public Vector3 rInPlaneRotationAxis = new Vector3(0f, 0f, 1f);
    [Header("R - 神眷星雨 / 调试")]
    [HideInInspector] public bool rDebugSwordVelocityFacing = false;
    [HideInInspector] public float rFacingLookAheadTime = 0.05f;
    [HideInInspector] public bool rDebugFacingScreenAngle = false;
    [InspectorName("R 使用玩家图层")]
    public bool rUsePlayerLayerForR = true;
    [InspectorName("R 双面显示")]
    public bool rForceDoubleSided = true;
    [Header("R - 神眷星雨 / 自转")]
    [HideInInspector] public bool rEnableSwordSelfSpin = false;
    [HideInInspector] public float rSwordSelfSpinMin = 30f;
    [HideInInspector] public float rSwordSelfSpinMax = 120f;
    [HideInInspector] public Vector3 rSwordLengthLocalAxis = Vector3.up;
    [Header("R - 神眷星雨 / 星雨伤害")]
    [InspectorName("R 伤害半径")]
    public float rSwarmDamageRadius = 3.0f;
    [InspectorName("R 伤害间隔")]
    public float rSwarmDamageInterval = 0.25f;
    [InspectorName("R 每次伤害")]
    public float rSwarmDamagePerTick = 2.0f;
    [InspectorName("R 敌人层")]
    public LayerMask rSwarmEnemyLayer = ~0;

    [Header("R - 神眷星雨 / 星雨")]
    [InspectorName("R 上升时间")]
    [HideInInspector] public float rRiseDuration = 0.45f;
    [InspectorName("R 上升高度")]
    [HideInInspector] public float rRiseHeight = 4f;
    [InspectorName("R 星雨持续时间")]
    [HideInInspector] public float rStarRainDuration = 1.2f;
    [InspectorName("R 星雨生成高度")]
    public float rStarRainSpawnHeight = 5f;
    [InspectorName("R 星雨范围半径")]
    public float rStarRainRadius = 5f;
    [InspectorName("R 星雨落下速度")]
    public float rStarRainFallSpeed = 10f;
    [InspectorName("R 星雨随机延迟")]
    public float rStarRainRandomDelay = 0.15f;
    [InspectorName("R 星雨伤害半径")]
    public float rStarRainDamageRadius = 1.2f;

    [InspectorName("R 星雨开始比例")]
    public float rStarRainStartRatio = 0.5f;
    [InspectorName("R 星雨生成间隔")]
    public float rStarRainInterval = 0.12f;
    [InspectorName("R 每波星雨剑数")]
    public int rStarRainBladesPerWave = 2;
    [InspectorName("R 星雨视觉偏移")]
    public Vector3 rStarRainVisualEulerOffset = new Vector3(90f, 0f, 0f);
    [InspectorName("R 星雨强制视觉旋转")]
    public bool rStarRainUseForcedVisualRotation = true;
    [InspectorName("R 星雨强制旋转角度")]
    public Vector3 rStarRainForcedVisualEuler = new Vector3(90f, 180f, 0f);
    [InspectorName("R 星雨伤害倍率")]
    public float rStarRainDamageMultiplier = 0.6f;
    [InspectorName("R 星雨尺寸")]
    public Vector3 rStarRainEffectScale = new Vector3(0.3f, 0.3f, 0.3f);
    [InspectorName("R 星雨延续到 Orbit 后")]
    public bool rStarRainContinueAfterOrbit = true;
    [InspectorName("R Orbit 后额外星雨时间")]
    public float rStarRainExtraDurationAfterOrbit = 0.6f;
    [InspectorName("R Orbit 结束即清理")]
    public bool rOrbitClearWhenOrbitEnds = true;
    [InspectorName("R Orbit 淡出时间")]
    public float rOrbitFadeOutDuration = 0.15f;

    [Header("待机剑")]
    public Vector3 standbySwordScale = new Vector3(0.25f, 0.25f, 0.25f);
    public float standbySwordRotationZ = -90f;
    public Vector3 standbySwordPlaneScale = new Vector3(0.25f, 0.25f, 1f);
    public Vector3 standbySwordOffset = Vector3.zero;
    public float standbySwordVisualPitch = 90f;
    public float standbySwordVisualYaw = 0f;
    public float standbySwordVisualRoll = 0f;
    public float standbySwordSpinSpeed = 120f;

    [Header("Refs")]
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

            if (HasAliveEStarFallBlades())
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
    }

    public void ClearRuntimeSkillVisualsForSwitch()
    {
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

    private void LateUpdate()
    {
        if (lockCharacterRotation)
        {
            transform.rotation = initialRotation;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.qKey.wasPressedThisFrame) CastQ();
        if (Keyboard.current.wKey.wasPressedThisFrame) CastW();
        if (Keyboard.current.eKey.wasPressedThisFrame) CastE();
        if (Keyboard.current.rKey.wasPressedThisFrame) CastR();
    }

    private void CastQ()
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

    private void CastW()
    {
        if (wSkillRoutine != null)
        {
            StopCoroutine(wSkillRoutine);
            wSkillRoutine = null;
        }

        CleanupWVisuals();
        wSkillRoutine = StartCoroutine(ShieldRoutine());
    }

    private void CastE()
    {
        if (!isDashing) StartCoroutine(DashRoutine());
    }

    private void CastR()
    {
        if (rSwarmRoutine != null)
        {
            StopCoroutine(rSwarmRoutine);
            rSwarmRoutine = null;
        }
        CleanupRSwarmVisuals();

        int energyForR = Mathf.Max(0, currentSwordEnergy);
        int count = Mathf.Max(0, rBaseSwordCount) + energyForR;
        if (count <= 0) return;
        Camera renderCamera = ResolveRRenderCamera();
        Vector3 previewCenter = ResolveRSwarmCenter();
        Debug.Log($"[R Skill] BaseSwordCount={rBaseSwordCount}, CurrentSwordEnergy={energyForR}, Spawned={count}, RenderCamera={(renderCamera != null ? renderCamera.name : "null")}, Center={previewCenter}", this);
        currentSwordEnergy = 0;
        rSwarmRoutine = StartCoroutine(RSwarmRoutine(count));
    }

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
            float radius = Random.Range(Mathf.Min(rSwarmRadiusMin, rSwarmRadiusMax), Mathf.Max(rSwarmRadiusMin, rSwarmRadiusMax));
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

            // W size control: only use W Effect Scale Multiplier on top of the correctly displayed base scale.
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

        // Clean legacy/temporary W visuals that may have been spawned by older logic.
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

    private void ApplyRSwarmTickDamage(Vector3 center)
    {
        ApplyRSwarmAreaDamage(center, rSwarmDamageRadius, rSwarmDamagePerTick);
    }

    private void ApplyRSwarmImpactDamage(Vector3 center)
    {
        ApplyRSwarmAreaDamage(center, Mathf.Max(0.01f, rStarRainDamageRadius), rSwarmDamagePerTick * Mathf.Max(0f, rStarRainDamageMultiplier));
    }

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
        CleanupEStarFallBlades();
        isDashing = true;
        Vector3 dir = ResolveFacingDirection();
        Vector3 start = transform.position;
        Vector3 end = start + dir * dashDistance;
        int spawnedAfterimages = 0;
        float afterimageTimer = 0f;
        float spawnInterval = Mathf.Max(0.01f, eAfterimageSpawnInterval);

        if (eEnableAfterimageShader)
        {
            TrySpawnEAfterimage(start, ref spawnedAfterimages);
        }

        float t = 0f;
        while (t < dashDuration)
        {
            float p = Mathf.Clamp01(t / dashDuration);
            transform.position = Vector3.Lerp(start, end, p);

            if (eEnableAfterimageShader && spawnedAfterimages < Mathf.Max(0, eAfterimageCount))
            {
                afterimageTimer += Time.deltaTime;
                if (afterimageTimer >= spawnInterval)
                {
                    afterimageTimer -= spawnInterval;
                    TrySpawnEAfterimage(transform.position, ref spawnedAfterimages);
                }
            }

            t += Time.deltaTime;
            yield return null;
        }

        transform.position = end;
        if (eEnableAfterimageShader)
        {
            TrySpawnEAfterimage(end, ref spawnedAfterimages);
        }
        isDashing = false;
    }

    private Renderer ResolveEAfterimageSourceRenderer()
    {
        if (eAfterimageSourceRenderer != null)
        {
            return eAfterimageSourceRenderer;
        }

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer != null && spriteRenderer.enabled && spriteRenderer.sprite != null)
            {
                return spriteRenderer;
            }
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer != null)
            {
                return spriteRenderer;
            }
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null)
            {
                return renderer;
            }
        }

        return null;
    }

    private bool TrySpawnEAfterimage(Vector3 position, ref int spawnedCount)
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

        Renderer sourceRenderer = ResolveEAfterimageSourceRenderer();
        if (sourceRenderer == null)
        {
            return false;
        }

        if (eAfterimageMaterial == null)
        {
            Debug.LogWarning("[E Afterimage] Missing eAfterimageMaterial. Assign a Shader Graph / Material for afterimages.", this);
            return false;
        }

        GameObject afterimage = CreateEAfterimageObject(sourceRenderer, position);
        if (afterimage == null)
        {
            return false;
        }

        spawnedCount += 1;
        return true;
    }

    private GameObject CreateEAfterimageObject(Renderer sourceRenderer, Vector3 worldPosition)
    {
        if (sourceRenderer == null)
        {
            return null;
        }

        GameObject afterimage = new GameObject("E_Afterimage");
        afterimage.transform.SetPositionAndRotation(worldPosition, sourceRenderer.transform.rotation);
        afterimage.transform.localScale = Vector3.Scale(sourceRenderer.transform.lossyScale, eAfterimageScale);

        Material runtimeMaterial = new Material(eAfterimageMaterial);
        ApplyAfterimageMaterialTint(runtimeMaterial, eAfterimageTint, eAfterimageAlpha);

        if (sourceRenderer is SpriteRenderer srcSprite)
        {
            SpriteRenderer ghostSprite = afterimage.AddComponent<SpriteRenderer>();
            ghostSprite.sprite = srcSprite.sprite;
            ghostSprite.flipX = srcSprite.flipX;
            ghostSprite.flipY = srcSprite.flipY;
            ghostSprite.drawMode = srcSprite.drawMode;
            ghostSprite.size = srcSprite.size;
            ghostSprite.spriteSortPoint = srcSprite.spriteSortPoint;
            ghostSprite.maskInteraction = srcSprite.maskInteraction;
            ghostSprite.sortingLayerID = srcSprite.sortingLayerID;
            ghostSprite.sortingOrder = srcSprite.sortingOrder + eAfterimageSortingOrderOffset;
            ghostSprite.material = runtimeMaterial;
            ghostSprite.color = new Color(eAfterimageTint.r, eAfterimageTint.g, eAfterimageTint.b, Mathf.Clamp01(eAfterimageAlpha));
        }
        else if (sourceRenderer is SkinnedMeshRenderer skinnedSource)
        {
            Mesh bakedMesh = new Mesh();
            skinnedSource.BakeMesh(bakedMesh);

            MeshFilter meshFilter = afterimage.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = bakedMesh;

            MeshRenderer meshRenderer = afterimage.AddComponent<MeshRenderer>();
            meshRenderer.material = runtimeMaterial;
            meshRenderer.sortingOrder = eAfterimageSortingOrderOffset;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }
        else
        {
            MeshFilter sourceMeshFilter = sourceRenderer.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = afterimage.AddComponent<MeshRenderer>();
            MeshFilter meshFilter = afterimage.AddComponent<MeshFilter>();
            if (sourceMeshFilter != null)
            {
                meshFilter.sharedMesh = sourceMeshFilter.sharedMesh;
            }
            meshRenderer.material = runtimeMaterial;
            meshRenderer.sortingOrder = eAfterimageSortingOrderOffset;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        TrySetDoubleSidedIfSupported(afterimage);
        SkillEffectRuntime runtime = afterimage.AddComponent<SkillEffectRuntime>();
        CacheFadeTargets(afterimage, runtime);
        StartCoroutine(FadeAndDestroy(afterimage, Mathf.Max(0.05f, eAfterimageDuration)));
        return afterimage;
    }

    private static void ApplyAfterimageMaterialTint(Material mat, Color tint, float alpha)
    {
        if (mat == null)
        {
            return;
        }

        Color finalTint = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(Mathf.Max(alpha, tint.a)));
        SetMaterialColor(mat, finalTint);
        TrySetAfterimageMaterialAlpha(mat, finalTint.a);
    }

    private static void TrySetAfterimageMaterialAlpha(Material mat, float alpha)
    {
        if (mat == null)
        {
            return;
        }

        if (mat.HasProperty("_Alpha"))
        {
            mat.SetFloat("_Alpha", alpha);
        }

        if (mat.HasProperty("_Opacity"))
        {
            mat.SetFloat("_Opacity", alpha);
        }
    }

    private static void TrySetAfterimageMaterialTint(Material mat, Color tint, float alpha)
    {
        if (mat == null)
        {
            return;
        }

        Color finalTint = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(Mathf.Max(alpha, tint.a)));
        SetMaterialColor(mat, finalTint);
        TrySetAfterimageMaterialAlpha(mat, finalTint.a);
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
        if (useRawPrefabRotationForSkillEffects)
        {
            root.transform.rotation = Quaternion.Euler(skillEffectPrefabBaseRotation);
        }
        else
        {
            ApplyRootDirection(root.transform, direction, alignToDirection, invertForward, yawOffset);
        }

        GameObject effectVisual = CreateEffectInstance(name, specificPrefab, root.transform.position, root.transform.rotation, useRawPrefabRotationForSkillEffects);
        if (effectVisual == null)
        {
            Destroy(root);
            return null;
        }

        // Keep world transform when parenting so instantiated prefab stays at the root skill position.
        effectVisual.transform.SetParent(root.transform, true);

        Transform visualTarget = FindEffectVisualTransform(effectVisual);
        if (useRawPrefabRotationForSkillEffects)
        {
            effectVisual.transform.rotation = Quaternion.Euler(skillEffectPrefabBaseRotation);
            float rawScaleMultiplier = Mathf.Max(0.01f, skillEffectPrefabScaleMultiplier);
            effectVisual.transform.localScale = effectVisual.transform.localScale * rawScaleMultiplier;
        }
        else
        {
            visualTarget.localRotation = Quaternion.Euler(visualPitch, visualYaw, visualRoll);
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
        return sharedEffectRotationZ + specificRotationZ;
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
            Quaternion.Euler(rPlaneUprightEuler) *
            Quaternion.Euler(rPlaneFaceCameraEuler) *
            (rFlipPlaneFrontBack ? Quaternion.Euler(rPlaneFrontBackFlipEuler) : Quaternion.identity) *
            Quaternion.Euler(rEffectVisualPitch, rEffectVisualYaw, rEffectVisualRoll);
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
            return Quaternion.Euler(rStarRainForcedVisualEuler) * Quaternion.Euler(rStarRainVisualEulerOffset);
        }

        return BuildRVisibleBaseRotation() * Quaternion.Euler(rStarRainVisualEulerOffset);
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
        // StarRain final visual.rotation is intentionally written here last so the fall swords stay vertical.
    }

    private Quaternion BuildEStarFallVisibleRotation(Vector3 fallDir)
    {
        if (eStarFallUseForcedVisualRotation)
        {
            return Quaternion.Euler(eStarFallForcedVisualEuler) * Quaternion.Euler(eStarFallVisualEulerOffset);
        }

        Vector3 dir = fallDir;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = Vector3.down;
        }
        dir.Normalize();

        Quaternion fallRotation = Quaternion.LookRotation(dir, Vector3.up);
        return fallRotation * Quaternion.Euler(eStarFallVisualEulerOffset);
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
}


