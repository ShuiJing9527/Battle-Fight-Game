using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Spine.Unity;
using CameraClearFlags = UnityEngine.CameraClearFlags;

public class PlayerAttributePanelUI : MonoBehaviour
{
    private static PlayerAttributePanelUI primaryInstance;

    [System.Serializable]
    private enum PreviewExtraVisualSortMode
    {
        KeepSource,
        BehindCharacter,
        InFrontOfCharacter
    }

    [System.Serializable]
    private enum PreviewExtraVisualTransformMode
    {
        RelativeToSource,
        ManualLocalToPreviewRoot
    }

    [System.Serializable]
    private class PreviewExtraVisualBinding
    {
        public Transform source;
        public string previewName;
        public PreviewExtraVisualTransformMode transformMode = PreviewExtraVisualTransformMode.RelativeToSource;
        public bool followPosition = true;
        public bool followRotation = true;
        public bool followScale = true;
        public Vector3 localPositionOffset;
        public Vector3 localEulerOffset;
        public Vector3 localScaleMultiplier = Vector3.one;
        public PreviewExtraVisualSortMode sortMode = PreviewExtraVisualSortMode.KeepSource;
        public int sortingOrderOffset;
        public float previewAlphaMultiplier = 1f;
        public float previewColorIntensityMultiplier = 1f;
        public bool overridePreviewColor = false;
        public Color previewColorTint = Color.white;
        public float previewEmissionMultiplier = 1f;
        public bool keepMonoBehaviours = false;
        public string[] keepMonoBehaviourTypeNames;
        public bool usePreviewRotationDriver = false;
        public Vector3 previewRotationSpeedEuler;
    }

    private sealed class PreviewExtraVisualRuntime
    {
        public PreviewExtraVisualBinding binding;
        public Transform sourceCharacterRoot;
        public Transform sourceVisualRoot;
        public Transform previewVisualRoot;
        public Transform previewTransform;
        public int sourceCharacterMinSortingOrder;
        public int sourceCharacterMaxSortingOrder;
        public int previewCharacterMinSortingOrder;
        public int previewCharacterMaxSortingOrder;
        public int previewCharacterSortingLayerId;
        public Vector3 initialLocalPosition;
        public Quaternion initialLocalRotation;
        public Vector3 initialLocalScale;
        public float materialPreviewTime;
        public readonly List<Material> timeDrivenMaterials = new List<Material>();
        public bool warnedMissingRenderer;
        public bool warnedMissingMaterial;
        public bool warnedMissingShaderTimeProperty;
        public bool loggedMaterialDiagnostics;
        public Vector3 previewRotationEuler;
    }

    private struct AttributeBaseSnapshot
    {
        public bool initialized;
        public float maxHealth;
        public float physicalAttack;
        public float physicalDefense;
        public float specialAttack;
        public float specialDefense;
        public float speed;
    }

    [Header("Root")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private GameObject panelPrefab;
    [SerializeField] private KeyCode toggleKey = KeyCode.I;
    [SerializeField] private bool debugToggleLog = false;
    [SerializeField] private bool usePanelOverrideSorting = true;
    [SerializeField] private int panelSortingOrder = 500;

    [Header("Refresh")]
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;

    [Header("Layout Control")]
    [SerializeField] private bool preserveManualLayout = true;

    [Header("Preview")]
    [SerializeField] private GameObject player01WorldPreviewPrefab;
    [SerializeField] private GameObject player02WorldPreviewPrefab;
    [SerializeField] private RawImage previewRawImage;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private RenderTexture previewRenderTexture;
    [SerializeField] private Transform worldPreviewRoot;
    [SerializeField] private Vector2 previewPanelSize = new Vector2(520f, 420f);
    [SerializeField] private Vector2 previewPanelOffset = Vector2.zero;
    [SerializeField] private Vector2Int previewTextureSize = new Vector2Int(1024, 1024);
    [SerializeField, Min(0.1f)] private float previewCameraOrthographicSize = 3f;
    [SerializeField] private string previewLayerName = "UI";
    [SerializeField] private Vector3 player01WorldPreviewPosition = Vector3.zero;
    [SerializeField] private Vector3 player01WorldPreviewScale = Vector3.one;
    [SerializeField] private Vector3 player01WorldPreviewEuler = Vector3.zero;
    [SerializeField] private Vector3 player02WorldPreviewPosition = Vector3.zero;
    [SerializeField] private Vector3 player02WorldPreviewScale = Vector3.one;
    [SerializeField] private Vector3 player02WorldPreviewEuler = Vector3.zero;
    [SerializeField] private string player01PreviewIdleAnimationName = "Idle";
    [SerializeField] private string player02PreviewIdleAnimationName = "idle";
    [Header("Extra Preview Visuals")]
    [SerializeField] private List<PreviewExtraVisualBinding> player01ExtraPreviewVisuals = new List<PreviewExtraVisualBinding>();
    [SerializeField] private List<PreviewExtraVisualBinding> player02ExtraPreviewVisuals = new List<PreviewExtraVisualBinding>();
    [Header("Legacy UI Preview Fallback")]
    [SerializeField] private GameObject player01PreviewPrefab;
    [SerializeField] private GameObject player02PreviewPrefab;
    [SerializeField] private Vector2 previewUiAnchoredPosition = new Vector2(25f, 10f);
    [SerializeField] private Vector2 previewUiSize = new Vector2(260f, 340f);
    [SerializeField] private float previewUiScale = 0.12f;
    [SerializeField] private Vector2 player01PreviewUiAnchoredPosition = new Vector2(25f, 10f);
    [SerializeField] private Vector2 player01PreviewUiSize = new Vector2(260f, 340f);
    [SerializeField] private float player01PreviewUiScale = 0.12f;
    [SerializeField] private Vector2 player02PreviewUiAnchoredPosition = new Vector2(35f, 35f);
    [SerializeField] private Vector2 player02PreviewUiSize = new Vector2(260f, 340f);
    [SerializeField] private float player02PreviewUiScale = 0.07f;
    [SerializeField] private Vector3 previewLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 previewLocalScale = new Vector3(80f, 80f, 80f);
    [SerializeField] private Vector3 previewLocalEuler = Vector3.zero;

    [Header("Fallback Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(760f, 360f);
    [SerializeField] private Vector2 panelAnchoredPosition = new Vector2(0f, 0f);
    [SerializeField] private Vector2 panelPadding = new Vector2(20f, 18f);
    [SerializeField] private float sectionSpacing = 20f;
    [SerializeField] private float statsTitleHeight = 32f;
    [SerializeField] private float attributeRowHeight = 28f;
    [SerializeField] private float attributeRowSpacing = 12f;
    [SerializeField] private float footerHeight = 110f;
    [SerializeField] private float attributeLabelWidth = 48f;
    [SerializeField] private float attributeValueWidth = 88f;

    [Header("Fallback Colors")]
    [SerializeField] private Color panelColor = new Color(0.08f, 0.1f, 0.14f, 0.96f);
    [SerializeField] private Color previewColor = new Color(0.12f, 0.14f, 0.2f, 0.95f);
    [SerializeField] private Color barBackgroundColor = new Color(0.16f, 0.18f, 0.24f, 1f);
    [SerializeField] private Color barFillColor = new Color(0.92f, 0.76f, 0.30f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color subTextColor = new Color(0.84f, 0.88f, 0.95f, 1f);

    [Header("Attribute Bar Colors")]
    [SerializeField] private Color hpBaseColor = new Color32(0x6C, 0xCB, 0x5F, 0xFF);
    [SerializeField] private Color atkBaseColor = new Color32(0xD9, 0x53, 0x4F, 0xFF);
    [SerializeField] private Color defBaseColor = new Color32(0xE4, 0x9B, 0x3E, 0xFF);
    [SerializeField] private Color magBaseColor = new Color32(0x8E, 0x63, 0xD9, 0xFF);
    [SerializeField] private Color resBaseColor = new Color32(0x5B, 0x8C, 0xFF, 0xFF);
    [SerializeField] private Color spdBaseColor = new Color(0.75f, 0.94f, 1.00f, 1.00f);
    [SerializeField] private Color bonusBarColor = new Color32(0xF2, 0xC9, 0x4C, 0xFF);
    [SerializeField] private Color compositeBarBackgroundColor = new Color32(0x2F, 0x35, 0x50, 0xFF);

    [Header("Bar Display Max")]
    [SerializeField, Min(1f)] private float hpChartDisplayMax = 50f;
    [SerializeField, Min(1f)] private float atkDisplayMax = 100f;
    [SerializeField, Min(1f)] private float defDisplayMax = 100f;
    [SerializeField, Min(1f)] private float magDisplayMax = 100f;
    [SerializeField, Min(1f)] private float resDisplayMax = 100f;
    [SerializeField, Min(1f)] private float spdDisplayMax = 100f;

    private readonly string[] attributeKeys = { "HP", "ATK", "DEF", "MAG", "RES", "SPD" };
    private readonly Image[] attributeBarBackgrounds = new Image[6];
    private readonly Image[] attributeBaseFills = new Image[6];
    private readonly Image[] attributeBonusFills = new Image[6];
    private readonly TextMeshProUGUI[] attributeValues = new TextMeshProUGUI[6];

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI previewText;
    private TextMeshProUGUI playerNameText;
    private TextMeshProUGUI characterPreviewText;
    private TextMeshProUGUI footerText;
    private TextMeshProUGUI spdText;
    private TextMeshProUGUI luckText;
    private TextMeshProUGUI critRateText;
    private TextMeshProUGUI extraSoulDropText;
    private TextMeshProUGUI extraRuneDropText;
    private TextMeshProUGUI reserveText;
    private Canvas panelCanvas;
    private RectTransform previewRect;
    private RectTransform previewRootRect;
    private RectTransform statsRect;
    private RectTransform subInfoRect;
    private RectTransform reserveRect;
    private bool initialized;
    private bool isVisible;
    private bool usingFallbackLayout;
    private bool usingPrefabLayout;
    private bool panelRootWasCreatedAtRuntime;
    private bool previewRectWasCreatedAtRuntime;
    private bool previewRootRectWasCreatedAtRuntime;
    private bool previewCameraWasCreatedAtRuntime;
    private bool pausedByAttributePanel;
    private bool warnedShowPanelFailed;
    private float nextRefreshTime;
    private float nextBootstrapLookupTime;
    private float nextBaseSnapshotWarmupTime;
    private float previousTimeScale = 1f;
    private static bool warnedMissingPanelPrefab;

    private Player2Bootstrap cachedBootstrap;
    private GameObject cachedPlayer;
    private CombatStats cachedStats;
    private BattleResourceBank cachedResourceBank;
    private CombatHealth cachedCombatHealth;
    private RuntimeLootDropOnDeath cachedLootDropPreview;
    private GameObject previewInstance;
    private SkeletonAnimation previewSkeletonAnimation;
    private GameObject currentPreviewPrefab;
    private int currentPreviewPlayerIndex;
    private string currentPreviewAnimationKey;
    private bool warnedMissingPreviewIdleAnimation;
    private int previewLayerIndex = -1;
    private Transform previewExtraVisualRoot;
    private readonly List<PreviewExtraVisualRuntime> previewExtraVisuals = new List<PreviewExtraVisualRuntime>();
    private readonly Dictionary<int, AttributeBaseSnapshot> attributeBaseSnapshots = new Dictionary<int, AttributeBaseSnapshot>();

    public bool IsPanelOpen => isVisible;

    private void Awake()
    {
        if (!AcquirePrimaryInstance())
        {
            return;
        }

        Initialize();
        ForceHiddenInitializedState();
        TryWarmupAttributeBaseSnapshot(forceBootstrapRefresh: true);
    }

    private void Start()
    {
        if (primaryInstance != this)
        {
            return;
        }

        Initialize();
        ForceHiddenInitializedState();
        TryWarmupAttributeBaseSnapshot(forceBootstrapRefresh: true);
    }

    private void OnEnable()
    {
        if (primaryInstance != this)
        {
            return;
        }

        Initialize();
        TryWarmupAttributeBaseSnapshot(forceBootstrapRefresh: true);
    }

    private void OnDisable()
    {
        if (primaryInstance != this)
        {
            return;
        }

        RestoreTimeScaleIfNeeded();
    }

    private void OnDestroy()
    {
        bool isPrimary = primaryInstance == this;
        if (!isPrimary)
        {
            return;
        }

        RestoreTimeScaleIfNeeded();
        if (previewRenderTexture != null)
        {
            previewRenderTexture.Release();
            Destroy(previewRenderTexture);
            previewRenderTexture = null;
        }

        primaryInstance = null;
    }

    private void Update()
    {
        if (!initialized)
        {
            if (primaryInstance != null && primaryInstance != this)
            {
                return;
            }

            Initialize();
            if (!initialized)
            {
                return;
            }
        }

        if (Time.unscaledTime >= nextBaseSnapshotWarmupTime)
        {
            TryWarmupAttributeBaseSnapshot(forceBootstrapRefresh: false);
        }

        if (Input.GetKeyDown(toggleKey))
        {
            TogglePanel();
        }

        if (isVisible && panelRoot != null && panelRoot.gameObject.activeSelf && Time.unscaledTime >= nextRefreshTime)
        {
            RefreshPlayerCache(force: false);
            RefreshPanel();
            nextRefreshTime = Time.unscaledTime + refreshInterval;
        }

        UpdatePreviewExtraVisualsUnscaled(Time.unscaledDeltaTime);
        UpdatePreviewAnimationUnscaled(Time.unscaledDeltaTime);
    }

    private void LateUpdate()
    {
        if (primaryInstance != this)
        {
            return;
        }

        SyncPreviewExtraVisualTransforms();
        RenderPreviewCameraIfNeeded();
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        panelRootWasCreatedAtRuntime = false;
        previewRectWasCreatedAtRuntime = false;
        previewRootRectWasCreatedAtRuntime = false;
        previewCameraWasCreatedAtRuntime = false;

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            return;
        }

        if (panelRoot == null)
        {
            panelRoot = FindExistingPanel(canvas.transform);
        }

        if (panelRoot == null)
        {
            RectTransform prefabInstance = InstantiatePrefabPanel(canvas.transform);
            if (prefabInstance != null)
            {
                panelRoot = prefabInstance;
                panelRootWasCreatedAtRuntime = true;
                usingFallbackLayout = ShouldUseFallbackLayout(prefabInstance);
            }
        }

        if (panelRoot == null)
        {
            panelRoot = CreateRectTransform("PlayerAttributePanel", canvas.transform);
            panelRootWasCreatedAtRuntime = true;
            usingFallbackLayout = true;
        }

        if (!usingFallbackLayout)
        {
            usingFallbackLayout = ShouldUseFallbackLayout(panelRoot);
        }

        usingPrefabLayout = !usingFallbackLayout;

        BuildPanelIfNeeded();
        EnsurePanelDisplayHierarchy();
        if (ShouldApplyFallbackLayout())
        {
            ApplyFallbackLayout();
        }

        RefreshPlayerCache(force: true);
        TryWarmupAttributeBaseSnapshot(forceBootstrapRefresh: true);
        RefreshPanel();
        initialized = true;
        ForceHiddenInitializedState();
    }

    private Canvas ResolveCanvas()
    {
        if (targetCanvas != null)
        {
            return targetCanvas;
        }

        targetCanvas = GetComponent<Canvas>();
        if (targetCanvas != null)
        {
            return targetCanvas;
        }

        targetCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas != null)
        {
            return targetCanvas;
        }

        PlayerStatusHUD statusHud = FindObjectOfType<PlayerStatusHUD>();
        if (statusHud != null)
        {
            targetCanvas = statusHud.GetComponentInParent<Canvas>();
            if (targetCanvas != null)
            {
                return targetCanvas;
            }
        }

        PlayerSkillHUD skillHud = FindObjectOfType<PlayerSkillHUD>();
        if (skillHud != null)
        {
            targetCanvas = skillHud.GetComponentInParent<Canvas>();
            if (targetCanvas != null)
            {
                return targetCanvas;
            }
        }

        targetCanvas = FindObjectOfType<Canvas>();
        if (targetCanvas != null)
        {
            return targetCanvas;
        }

        GameObject canvasObject = new GameObject(
            "HUDCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas createdCanvas = canvasObject.GetComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        targetCanvas = createdCanvas;
        return targetCanvas;
    }

    private RectTransform FindExistingPanel(Transform canvasTransform)
    {
        if (canvasTransform == null)
        {
            return null;
        }

        Transform existing = canvasTransform.Find("PlayerAttributePanel");
        if (existing != null)
        {
            return existing as RectTransform;
        }

        existing = canvasTransform.Find("CharacterAttributePanel");
        return existing as RectTransform;
    }

    private RectTransform InstantiatePrefabPanel(Transform parent)
    {
        GameObject resolvedPrefab = panelPrefab;
        if (resolvedPrefab == null)
        {
            resolvedPrefab = Resources.Load<GameObject>("Prefabs/UI/PlayerAttributePanel");
        }

        if (resolvedPrefab == null)
        {
            if (!warnedMissingPanelPrefab)
            {
                Debug.LogWarning("[PlayerAttributePanelUI] Missing PlayerAttributePanel prefab. Falling back to runtime-built panel.");
                warnedMissingPanelPrefab = true;
            }

            return null;
        }

        GameObject instance = Instantiate(resolvedPrefab, parent, false);
        instance.name = resolvedPrefab.name;
        return instance.GetComponent<RectTransform>();
    }

    private bool ShouldUseFallbackLayout(RectTransform root)
    {
        if (root == null)
        {
            return true;
        }

        RectTransform resolvedPreviewRect = FindNamedRect(root, "PreviewSection", "LeftPreviewArea");
        RectTransform resolvedStatsRect = FindNamedRect(root, "StatsSection", "AttributeArea");
        if (resolvedPreviewRect == null || resolvedStatsRect == null)
        {
            return true;
        }

        for (int i = 0; i < attributeKeys.Length; i++)
        {
            if ((resolvedStatsRect.Find(attributeKeys[i] + "Row") as RectTransform) == null)
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldApplyFallbackLayout()
    {
        if (!usingFallbackLayout)
        {
            return false;
        }

        if (!preserveManualLayout)
        {
            return true;
        }

        return panelRootWasCreatedAtRuntime;
    }

    private void BuildPanelIfNeeded()
    {
        if (panelRoot == null)
        {
            return;
        }

        RectTransform backgroundRect = FindRect(panelRoot, "Background");
        Image backgroundImage = backgroundRect != null
            ? EnsureImage(backgroundRect, panelColor)
            : EnsureImage(panelRoot, panelColor);
        backgroundImage.raycastTarget = false;

        previewRect = FindNamedRect(panelRoot, "PreviewSection", "LeftPreviewArea");
        if (previewRect == null)
        {
            previewRect = CreateRectTransform("PreviewSection", panelRoot);
            usingFallbackLayout = true;
            usingPrefabLayout = false;
            previewRectWasCreatedAtRuntime = true;
        }

        Image previewImage = EnsureImage(previewRect, previewColor);
        previewImage.raycastTarget = false;

        playerNameText = FindExistingText(previewRect, "PlayerNameText");
        characterPreviewText = FindExistingText(previewRect, "CharacterPreviewText");
        previewText = FindExistingText(previewRect, "PreviewLabel");
        previewRootRect = FindNamedRect(previewRect, "PreviewRoot");
        if (previewRootRect == null && !usingPrefabLayout)
        {
            previewRootRect = CreateRectTransform("PreviewRoot", previewRect);
            previewRootRectWasCreatedAtRuntime = true;
        }

        if (previewRawImage == null)
        {
            previewRawImage = FindExistingRawImage(previewRootRect != null ? previewRootRect : previewRect, "PreviewRawImage");
        }

        if (previewRawImage == null)
        {
            previewRawImage = CreatePreviewRawImage(previewRootRect != null ? previewRootRect : previewRect);
        }

        if (previewRawImage != null)
        {
            previewRawImage.raycastTarget = false;
            StretchPreviewGraphic(previewRawImage.rectTransform);
        }

        if (previewText == null && !usingPrefabLayout)
        {
            previewText = FindOrCreateText(previewRect, "PreviewLabel", 26f, TextAlignmentOptions.Center, textColor);
        }

        if (previewText != null)
        {
            previewText.enableWordWrapping = true;
        }

        statsRect = FindNamedRect(panelRoot, "StatsSection", "AttributeArea");
        if (statsRect == null)
        {
            statsRect = CreateRectTransform("StatsSection", panelRoot);
            usingFallbackLayout = true;
            usingPrefabLayout = false;
        }

        titleText = FindExistingText(statsRect, "StatsTitle");
        if (titleText == null)
        {
            titleText = FindExistingText(statsRect, "TitleText");
        }

        if (titleText == null && !usingPrefabLayout)
        {
            titleText = FindOrCreateText(statsRect, "StatsTitle", 26f, TextAlignmentOptions.MidlineLeft, textColor);
        }

        if (titleText != null)
        {
            titleText.enableWordWrapping = false;
        }

        for (int i = 0; i < attributeKeys.Length; i++)
        {
            EnsureAttributeRow(i, attributeKeys[i]);
        }

        subInfoRect = FindNamedRect(panelRoot, "SubInfoArea");
        if (subInfoRect != null)
        {
            spdText = FindExistingText(subInfoRect, "SPDText");
            luckText = FindExistingText(subInfoRect, "LUCKText");
            critRateText = FindExistingText(subInfoRect, "CritRateText");
            extraSoulDropText = FindExistingText(subInfoRect, "ExtraSoulDropText");
            extraRuneDropText = FindExistingText(subInfoRect, "ExtraRuneDropText");
        }

        reserveRect = FindNamedRect(panelRoot, "ReserveArea");
        if (reserveRect != null)
        {
            reserveText = FindExistingText(reserveRect, "ReserveText");
        }

        footerText = FindExistingText(statsRect, "FooterText");
        if (footerText == null && !usingPrefabLayout && subInfoRect == null && reserveRect == null)
        {
            footerText = FindOrCreateText(statsRect, "FooterText", 18f, TextAlignmentOptions.TopLeft, subTextColor);
        }

        if (footerText != null)
        {
            footerText.enableWordWrapping = true;
            footerText.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private void EnsurePanelDisplayHierarchy()
    {
        if (panelRoot == null)
        {
            return;
        }

        panelCanvas = panelRoot.GetComponent<Canvas>();
        if (panelCanvas == null)
        {
            panelCanvas = panelRoot.gameObject.AddComponent<Canvas>();
        }

        panelCanvas.overrideSorting = usePanelOverrideSorting;
        if (usePanelOverrideSorting)
        {
            panelCanvas.sortingOrder = panelSortingOrder;
        }

        if (panelRoot.GetComponent<GraphicRaycaster>() == null)
        {
            panelRoot.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private void EnsureAttributeRow(int index, string key)
    {
        RectTransform row = FindRect(statsRect, key + "Row");
        if (row == null)
        {
            row = CreateRectTransform(key + "Row", statsRect);
            usingFallbackLayout = true;
            usingPrefabLayout = false;
        }

        TextMeshProUGUI label = FindExistingText(row, "Label");
        if (label == null)
        {
            label = FindExistingText(row, "LabelText");
        }

        if (label == null && !usingPrefabLayout)
        {
            label = FindOrCreateText(row, "Label", 20f, TextAlignmentOptions.MidlineLeft, textColor);
        }

        if (label != null)
        {
            label.text = key + ":";
            label.enableWordWrapping = false;
        }

        RectTransform barRootRect = FindRect(row, "BarRoot");
        RectTransform legacyBackgroundRect = FindRect(row, "BarBackground");
        if (barRootRect == null && legacyBackgroundRect != null)
        {
            barRootRect = legacyBackgroundRect;
        }

        bool createdBarRoot = false;
        if (barRootRect == null)
        {
            barRootRect = CreateRectTransform("BarRoot", row);
            createdBarRoot = true;
            usingFallbackLayout = true;
            usingPrefabLayout = false;
        }

        RectTransform backgroundRect = FindRect(barRootRect, "BarBg");
        if (backgroundRect == null)
        {
            backgroundRect = FindRect(barRootRect, "BarBackground");
        }

        bool backgroundRectIsBarRoot = backgroundRect == null && barRootRect == legacyBackgroundRect;
        bool createdBackgroundRect = false;
        if (backgroundRect == null)
        {
            if (backgroundRectIsBarRoot)
            {
                backgroundRect = barRootRect;
            }
            else
            {
                backgroundRect = CreateRectTransform("BarBg", barRootRect);
                createdBackgroundRect = true;
                usingFallbackLayout = true;
                usingPrefabLayout = false;
            }
        }

        Image backgroundImage = EnsureImage(backgroundRect, compositeBarBackgroundColor);
        backgroundImage.color = compositeBarBackgroundColor;
        backgroundImage.raycastTarget = false;

        if (createdBarRoot)
        {
            ConfigureBarContainerRect(barRootRect);
        }

        if (createdBackgroundRect)
        {
            ConfigureBarContainerRect(backgroundRect);
        }

        RectTransform fillParentRect = backgroundRect != null ? backgroundRect : barRootRect;
        RectTransform baseFillRect = FindRect(fillParentRect, "BaseFill");
        if (baseFillRect == null)
        {
            RectTransform legacyFillRect = FindRect(backgroundRect, "BarFill");
            if (legacyFillRect != null)
            {
                baseFillRect = legacyFillRect;
                baseFillRect.SetParent(fillParentRect, false);
                baseFillRect.name = "BaseFill";
            }
        }

        if (baseFillRect == null)
        {
            baseFillRect = CreateRectTransform("BaseFill", fillParentRect);
            usingFallbackLayout = true;
            usingPrefabLayout = false;
        }

        Image baseFillImage = EnsureImage(baseFillRect, ResolveAttributeBaseColor(index));
        baseFillImage.color = ResolveAttributeBaseColor(index);
        baseFillImage.raycastTarget = false;

        RectTransform bonusFillRect = FindRect(fillParentRect, "BonusFill");
        if (bonusFillRect == null)
        {
            bonusFillRect = CreateRectTransform("BonusFill", fillParentRect);
            usingFallbackLayout = true;
            usingPrefabLayout = false;
        }

        Image bonusFillImage = EnsureImage(bonusFillRect, bonusBarColor);
        bonusFillImage.color = bonusBarColor;
        bonusFillImage.raycastTarget = false;

        TextMeshProUGUI value = FindExistingText(row, "Value");
        if (value == null)
        {
            value = FindExistingText(row, "ValueText");
        }

        if (value == null && !usingPrefabLayout)
        {
            value = FindOrCreateText(row, "Value", 20f, TextAlignmentOptions.MidlineRight, textColor);
        }

        if (value != null)
        {
            value.enableWordWrapping = false;
            value.alignment = TextAlignmentOptions.MidlineRight;
        }

        attributeBarBackgrounds[index] = backgroundImage;
        attributeBaseFills[index] = baseFillImage;
        attributeBonusFills[index] = bonusFillImage;
        attributeValues[index] = value;
    }

    private void ApplyFallbackLayout()
    {
        if (panelRoot == null || previewRect == null || statsRect == null)
        {
            return;
        }

        panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
        panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
        panelRoot.pivot = new Vector2(0.5f, 0.5f);
        panelRoot.anchoredPosition = panelAnchoredPosition;
        panelRoot.sizeDelta = panelSize;
        panelRoot.localScale = Vector3.one;

        float resolvedPreviewWidth = ResolvePreviewPanelSize().x;
        float resolvedPreviewHeight = ResolvePreviewPanelSize().y;
        float previewLeft = panelPadding.x + previewPanelOffset.x;
        float previewRight = previewLeft + resolvedPreviewWidth;

        if (previewRectWasCreatedAtRuntime || !preserveManualLayout)
        {
            previewRect.anchorMin = new Vector2(0f, 0.5f);
            previewRect.anchorMax = new Vector2(0f, 0.5f);
            previewRect.pivot = new Vector2(0f, 0.5f);
            previewRect.anchoredPosition = new Vector2(previewLeft, previewPanelOffset.y);
            previewRect.sizeDelta = new Vector2(resolvedPreviewWidth, resolvedPreviewHeight);
        }

        if (previewRootRect != null && (previewRootRectWasCreatedAtRuntime || !preserveManualLayout))
        {
            StretchPreviewGraphic(previewRootRect);
        }

        if (previewText != null)
        {
            RectTransform previewTextRect = previewText.rectTransform;
            previewTextRect.anchorMin = Vector2.zero;
            previewTextRect.anchorMax = Vector2.one;
            previewTextRect.offsetMin = new Vector2(16f, 16f);
            previewTextRect.offsetMax = new Vector2(-16f, -16f);
        }

        statsRect.anchorMin = new Vector2(0f, 0f);
        statsRect.anchorMax = new Vector2(1f, 1f);
        statsRect.offsetMin = new Vector2(previewRight + sectionSpacing, panelPadding.y);
        statsRect.offsetMax = new Vector2(-panelPadding.x, -panelPadding.y);

        if (titleText != null)
        {
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(0f, -statsTitleHeight);
            titleRect.offsetMax = new Vector2(0f, 0f);
        }

        float statsWidth = Mathf.Max(240f, panelSize.x - panelPadding.x - previewRight - sectionSpacing - panelPadding.x);
        float barStartX = attributeLabelWidth + 12f;
        float barWidth = Mathf.Max(100f, statsWidth - barStartX - attributeValueWidth - 12f);
        float firstRowTop = -statsTitleHeight - 14f;

        for (int i = 0; i < attributeKeys.Length; i++)
        {
            RectTransform row = FindRect(statsRect, attributeKeys[i] + "Row");
            if (row == null)
            {
                continue;
            }

            float top = firstRowTop - i * (attributeRowHeight + attributeRowSpacing);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.offsetMin = new Vector2(0f, top - attributeRowHeight);
            row.offsetMax = new Vector2(0f, top);

            RectTransform labelRect = FindRect(row, "Label");
            if (labelRect == null)
            {
                labelRect = FindRect(row, "LabelText");
            }
            if (labelRect != null)
            {
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(0f, 1f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.sizeDelta = new Vector2(attributeLabelWidth, 0f);
            }

            RectTransform barRootRect = FindRect(row, "BarRoot");
            if (barRootRect == null)
            {
                barRootRect = FindRect(row, "BarBackground");
            }

            if (barRootRect != null)
            {
                barRootRect.anchorMin = new Vector2(0f, 0.5f);
                barRootRect.anchorMax = new Vector2(0f, 0.5f);
                barRootRect.pivot = new Vector2(0f, 0.5f);
                barRootRect.anchoredPosition = new Vector2(barStartX, 0f);
                barRootRect.sizeDelta = new Vector2(barWidth, attributeRowHeight - 6f);

                RectTransform backgroundRect = FindRect(barRootRect, "BarBg");
                if (backgroundRect == null)
                {
                    backgroundRect = FindRect(barRootRect, "BarBackground");
                }

                if (backgroundRect != null)
                {
                    backgroundRect.anchorMin = new Vector2(0f, 0f);
                    backgroundRect.anchorMax = new Vector2(1f, 1f);
                    backgroundRect.pivot = new Vector2(0f, 0.5f);
                    backgroundRect.offsetMin = Vector2.zero;
                    backgroundRect.offsetMax = Vector2.zero;
                }

                RectTransform baseFillRect = FindRect(barRootRect, "BaseFill");
                ConfigureCompositeFillRect(baseFillRect);

                RectTransform bonusFillRect = FindRect(barRootRect, "BonusFill");
                ConfigureCompositeFillRect(bonusFillRect);
            }

            RectTransform valueRect = FindRect(row, "Value");
            if (valueRect == null)
            {
                valueRect = FindRect(row, "ValueText");
            }
            if (valueRect != null)
            {
                valueRect.anchorMin = new Vector2(1f, 0f);
                valueRect.anchorMax = new Vector2(1f, 1f);
                valueRect.pivot = new Vector2(1f, 0.5f);
                valueRect.anchoredPosition = Vector2.zero;
                valueRect.sizeDelta = new Vector2(attributeValueWidth, 0f);
            }
        }

        if (footerText != null)
        {
            RectTransform footerRect = footerText.rectTransform;
            footerRect.anchorMin = new Vector2(0f, 0f);
            footerRect.anchorMax = new Vector2(1f, 0f);
            footerRect.pivot = new Vector2(0.5f, 0f);
            footerRect.offsetMin = new Vector2(0f, 0f);
            footerRect.offsetMax = new Vector2(0f, footerHeight);
        }
    }

    private void TogglePanel()
    {
        LogToggleState("TogglePanel pressed");
        if (isVisible)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    private bool EnsurePanelReady()
    {
        if (!initialized || panelRoot == null)
        {
            Initialize();
        }

        if (panelRoot != null && panelRoot.gameObject.activeSelf != isVisible)
        {
            panelRoot.gameObject.SetActive(isVisible);
        }

        return initialized && panelRoot != null;
    }

    public void OpenPanel()
    {
        if (!EnsurePanelReady())
        {
            if (!warnedShowPanelFailed)
            {
                Debug.LogWarning("[PlayerAttributePanelUI] Failed to show panel because panel root could not be created or bound.");
                warnedShowPanelFailed = true;
            }
            return;
        }

        warnedShowPanelFailed = false;
        CloseRunePanelsForExclusiveDisplay();

        if (panelRoot == null)
        {
            return;
        }

        EnsurePanelDisplayHierarchy();
        panelRoot.gameObject.SetActive(true);
        panelRoot.SetAsLastSibling();
        isVisible = true;
        LogToggleState("ShowPanel active");

        RefreshPlayerCache(force: true);
        RefreshPanel();
        ForceRefreshPreview();
        nextRefreshTime = Time.unscaledTime + refreshInterval;

        PauseGameForPanel();
        LogToggleState("ShowPanel success");
    }

    public void ClosePanel()
    {
        if (panelRoot != null)
        {
            panelRoot.gameObject.SetActive(false);
        }

        ClearPreviewInstance();
        SetPreviewVisible(false);
        isVisible = false;
        RestoreTimeScaleIfNeeded();
        LogToggleState("HidePanel");
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;
        if (panelRoot != null)
        {
            panelRoot.gameObject.SetActive(visible);
        }
    }

    private void ForceHiddenInitializedState()
    {
        isVisible = false;
        pausedByAttributePanel = false;
        OverlayPanelStateCoordinator.SetCharacterPanelOpen(false);

        EnsurePanelDisplayHierarchy();

        if (panelRoot != null)
        {
            panelRoot.gameObject.SetActive(false);
        }

        ClearPreviewInstance();
        SetPreviewVisible(false);
    }

    private void RefreshPlayerCache(bool force)
    {
        if ((force || cachedBootstrap == null) && Time.unscaledTime >= nextBootstrapLookupTime)
        {
            cachedBootstrap = FindObjectOfType<Player2Bootstrap>();
            nextBootstrapLookupTime = Time.unscaledTime + 1f;
        }

        GameObject currentPlayer = cachedBootstrap != null ? cachedBootstrap.CurrentPlayer : null;
        if (currentPlayer == null)
        {
            GameObject taggedPlayer = GameObject.FindWithTag("Player");
            if (taggedPlayer != null && taggedPlayer.activeInHierarchy)
            {
                currentPlayer = taggedPlayer;
            }
        }

        if (!force && currentPlayer == cachedPlayer)
        {
            return;
        }

        cachedPlayer = currentPlayer;
        cachedStats = cachedPlayer != null ? BattleStatUtility.GetCombatStats(cachedPlayer) : null;
        cachedResourceBank = cachedPlayer != null ? cachedPlayer.GetComponent<BattleResourceBank>() : null;
        cachedCombatHealth = cachedPlayer != null ? cachedPlayer.GetComponent<CombatHealth>() : null;
        CacheAttributeBaseSnapshot(currentPlayer, cachedStats);
    }

    private void RefreshPanel()
    {
        if (panelRoot == null)
        {
            return;
        }

        float hpCurrent = ResolveCurrentHealth();
        float hpTotal = ResolveMaxHealth();
        AttributeBaseSnapshot baseSnapshot = ResolveAttributeBaseSnapshot();
        float hpBase = Mathf.Max(0f, baseSnapshot.maxHealth);
        float hpBonus = Mathf.Max(0f, hpTotal - hpBase);
        float atkTotal = cachedStats != null ? Mathf.Max(0f, cachedStats.physicalAttack) : 0f;
        float atkBase = Mathf.Max(0f, baseSnapshot.physicalAttack);
        float atkBonus = Mathf.Max(0f, atkTotal - atkBase);
        float defTotal = cachedStats != null ? Mathf.Max(0f, cachedStats.physicalDefense) : 0f;
        float defBase = Mathf.Max(0f, baseSnapshot.physicalDefense);
        float defBonus = Mathf.Max(0f, defTotal - defBase);
        float magTotal = cachedStats != null ? Mathf.Max(0f, cachedStats.specialAttack) : 0f;
        float magBase = Mathf.Max(0f, baseSnapshot.specialAttack);
        float magBonus = Mathf.Max(0f, magTotal - magBase);
        float resTotal = cachedStats != null ? Mathf.Max(0f, cachedStats.specialDefense) : 0f;
        float resBase = Mathf.Max(0f, baseSnapshot.specialDefense);
        float resBonus = Mathf.Max(0f, resTotal - resBase);
        float speedTotal = cachedStats != null ? Mathf.Max(0f, cachedStats.speed) : 0f;
        float speedBase = Mathf.Max(0f, baseSnapshot.speed);
        float speedBonus = Mathf.Max(0f, speedTotal - speedBase);
        float luck = cachedStats != null ? Mathf.Max(0f, cachedStats.luck) : 0f;
        float critRate = BattleStatUtility.GetCritRate(cachedStats) * 100f;
        float extraSoulDrop = ResolveExtraSoulDropChance(luck) * 100f;
        float extraRuneDrop = ResolveExtraRuneDropChance(luck) * 100f;

        if (titleText != null)
        {
            titleText.text = cachedPlayer != null ? cachedPlayer.name + " Attributes" : "Character Attributes";
        }

        if (previewText != null)
        {
            previewText.text = "Character Preview";
        }

        if (playerNameText != null)
        {
            playerNameText.text = cachedPlayer != null ? cachedPlayer.name : "Player";
        }

        if (characterPreviewText != null)
        {
            characterPreviewText.text = "Character Preview";
        }

        RefreshPreview(force: false);

        SetHealthDisplay(hpCurrent, hpBase, hpTotal, hpBonus);
        SetAttributeDisplay(1, atkBase, atkTotal, atkBonus, atkDisplayMax);
        SetAttributeDisplay(2, defBase, defTotal, defBonus, defDisplayMax);
        SetAttributeDisplay(3, magBase, magTotal, magBonus, magDisplayMax);
        SetAttributeDisplay(4, resBase, resTotal, resBonus, resDisplayMax);
        SetAttributeDisplay(5, speedBase, speedTotal, speedBonus, spdDisplayMax);

        if (spdText != null)
        {
            spdText.gameObject.SetActive(false);
        }

        if (luckText != null)
        {
            luckText.text = "LUCK " + luck.ToString("0");
        }

        if (critRateText != null)
        {
            critRateText.text = "Crit Rate " + critRate.ToString("0.#") + "%";
        }

        if (extraSoulDropText != null)
        {
            extraSoulDropText.text = "Extra Soul Drop " + extraSoulDrop.ToString("0.#") + "%";
        }

        if (extraRuneDropText != null)
        {
            extraRuneDropText.text = "Extra Rune Drop " + extraRuneDrop.ToString("0.#") + "%";
        }

        if (reserveText != null)
        {
            reserveText.text = "Buff / Rune / Skill Info Reserved";
        }

        if (footerText != null && spdText == null && luckText == null && critRateText == null && extraSoulDropText == null && extraRuneDropText == null)
        {
            footerText.text =
                "LUCK " + luck.ToString("0") + "\n" +
                "Crit Rate        " + critRate.ToString("0.#") + "%\n" +
                "Extra Soul Drop  " + extraSoulDrop.ToString("0.#") + "%\n" +
                "Extra Rune Drop  " + extraRuneDrop.ToString("0.#") + "%\n" +
                "Buff / Rune / Skill Info Reserved";
        }
    }

    private float ResolveCurrentHealth()
    {
        if (cachedResourceBank != null)
        {
            return Mathf.Max(0f, cachedResourceBank.currentHealth);
        }

        if (cachedCombatHealth != null)
        {
            return Mathf.Max(0f, cachedCombatHealth.currentHealth);
        }

        return cachedStats != null ? Mathf.Max(0f, cachedStats.maxHealth) : 0f;
    }

    private float ResolveMaxHealth()
    {
        if (cachedResourceBank != null)
        {
            return Mathf.Max(1f, cachedResourceBank.maxHealth);
        }

        if (cachedStats != null)
        {
            return Mathf.Max(1f, cachedStats.maxHealth);
        }

        if (cachedCombatHealth != null)
        {
            return Mathf.Max(1f, cachedCombatHealth.currentHealth);
        }

        return 1f;
    }

    private void SetHealthDisplay(float current, float baseMax, float totalMax, float bonusMax)
    {
        float safeBaseMax = Mathf.Max(0f, baseMax);
        float safeTotalMax = Mathf.Max(0f, totalMax);
        float safeBonusMax = Mathf.Max(0f, bonusMax);
        float hpChartBaseValue = safeBaseMax / 10f;
        float hpChartBonusValue = safeBonusMax / 10f;
        SetCompositeAttributeDisplay(0, safeBaseMax, safeTotalMax, safeBonusMax, hpChartBaseValue, hpChartBonusValue, hpChartDisplayMax);
    }

    private void SetAttributeDisplay(int index, float baseValue, float totalValue, float bonusValue, float displayMax)
    {
        float safeBaseValue = Mathf.Max(0f, baseValue);
        float safeTotalValue = Mathf.Max(0f, totalValue);
        float safeBonusValue = Mathf.Max(0f, bonusValue);
        SetCompositeAttributeDisplay(index, safeBaseValue, safeTotalValue, safeBonusValue, safeBaseValue, safeBonusValue, displayMax);
    }

    private void SetCompositeAttributeDisplay(int index, float baseValue, float totalValue, float bonusValue, float barBaseValue, float barBonusValue, float displayMax)
    {
        if (index < 0 || index >= attributeValues.Length)
        {
            return;
        }

        if (attributeValues[index] != null)
        {
            attributeValues[index].text = Mathf.RoundToInt(baseValue) + " + " + Mathf.RoundToInt(bonusValue);
        }

        float totalDisplayValue = Mathf.Max(0f, barBaseValue + barBonusValue);
        if (displayMax <= 0f)
        {
            displayMax = totalDisplayValue;
        }

        if (displayMax <= 0f)
        {
            ApplyCompositeFillWidths(index, 0f, 0f);
            return;
        }

        float safeBaseValue = Mathf.Max(0f, baseValue);
        float safeTotalValue = Mathf.Max(0f, totalValue);
        float safeBonusValue = Mathf.Max(0f, bonusValue);
        float finalValue = Mathf.Max(0f, safeTotalValue);
        float finalBarValue = Mathf.Clamp(Mathf.Max(0f, barBaseValue + barBonusValue), 0f, displayMax);

        if (finalBarValue <= 0f || finalValue <= 0f)
        {
            ApplyCompositeFillWidths(index, 0f, 0f);
            return;
        }

        float finalRatio = Mathf.Clamp01(finalBarValue / displayMax);
        float baseRatio = finalRatio * (safeBaseValue / finalValue);
        float bonusRatio = safeBonusValue > 0f
            ? finalRatio * (safeBonusValue / finalValue)
            : 0f;

        ApplyCompositeFillWidths(index, baseRatio, bonusRatio);
    }

    private void CacheAttributeBaseSnapshot(GameObject player, CombatStats stats)
    {
        if (player == null || stats == null)
        {
            return;
        }

        int key = player.GetInstanceID();
        if (attributeBaseSnapshots.ContainsKey(key))
        {
            return;
        }

        AttributeBaseSnapshot snapshot = new AttributeBaseSnapshot
        {
            initialized = true,
            maxHealth = Mathf.Max(0f, stats.maxHealth),
            physicalAttack = Mathf.Max(0f, stats.physicalAttack),
            physicalDefense = Mathf.Max(0f, stats.physicalDefense),
            specialAttack = Mathf.Max(0f, stats.specialAttack),
            specialDefense = Mathf.Max(0f, stats.specialDefense),
            speed = Mathf.Max(0f, stats.speed)
        };

        attributeBaseSnapshots[key] = snapshot;
    }

    private void TryWarmupAttributeBaseSnapshot(bool forceBootstrapRefresh)
    {
        if (!forceBootstrapRefresh && Time.unscaledTime < nextBaseSnapshotWarmupTime)
        {
            return;
        }

        GameObject player = ResolveCurrentPlayerForBaseSnapshot(forceBootstrapRefresh);
        CombatStats stats = player != null ? BattleStatUtility.GetCombatStats(player) : null;

        if (player != null && stats != null)
        {
            CacheAttributeBaseSnapshot(player, stats);
            nextBaseSnapshotWarmupTime = HasAttributeBaseSnapshot(player)
                ? Time.unscaledTime + 1f
                : Time.unscaledTime + 0.25f;
            return;
        }

        nextBaseSnapshotWarmupTime = Time.unscaledTime + 0.25f;
    }

    private GameObject ResolveCurrentPlayerForBaseSnapshot(bool forceBootstrapRefresh)
    {
        if ((forceBootstrapRefresh || cachedBootstrap == null) && Time.unscaledTime >= nextBootstrapLookupTime)
        {
            cachedBootstrap = FindObjectOfType<Player2Bootstrap>();
            nextBootstrapLookupTime = Time.unscaledTime + 1f;
        }

        GameObject player = cachedBootstrap != null ? cachedBootstrap.CurrentPlayer : null;
        if (player != null && player.activeInHierarchy)
        {
            return player;
        }

        GameObject taggedPlayer = GameObject.FindWithTag("Player");
        if (taggedPlayer != null && taggedPlayer.activeInHierarchy)
        {
            return taggedPlayer;
        }

        return null;
    }

    private bool HasAttributeBaseSnapshot(GameObject player)
    {
        if (player == null)
        {
            return false;
        }

        AttributeBaseSnapshot snapshot;
        return attributeBaseSnapshots.TryGetValue(player.GetInstanceID(), out snapshot) && snapshot.initialized;
    }

    private AttributeBaseSnapshot ResolveAttributeBaseSnapshot()
    {
        if (cachedPlayer == null)
        {
            return default;
        }

        AttributeBaseSnapshot snapshot;
        if (attributeBaseSnapshots.TryGetValue(cachedPlayer.GetInstanceID(), out snapshot) && snapshot.initialized)
        {
            return snapshot;
        }

        if (cachedStats == null)
        {
            return default;
        }

        snapshot = new AttributeBaseSnapshot
        {
            initialized = true,
            maxHealth = Mathf.Max(0f, cachedStats.maxHealth),
            physicalAttack = Mathf.Max(0f, cachedStats.physicalAttack),
            physicalDefense = Mathf.Max(0f, cachedStats.physicalDefense),
            specialAttack = Mathf.Max(0f, cachedStats.specialAttack),
            specialDefense = Mathf.Max(0f, cachedStats.specialDefense),
            speed = Mathf.Max(0f, cachedStats.speed)
        };

        attributeBaseSnapshots[cachedPlayer.GetInstanceID()] = snapshot;
        return snapshot;
    }

    private void ApplyCompositeFillWidths(int index, float baseRatio, float bonusRatio)
    {
        if (index < 0 || index >= attributeBaseFills.Length)
        {
            return;
        }

        RectTransform backgroundRect = attributeBarBackgrounds[index] != null
            ? attributeBarBackgrounds[index].rectTransform
            : null;
        RectTransform baseRect = attributeBaseFills[index] != null
            ? attributeBaseFills[index].rectTransform
            : null;
        RectTransform bonusRect = attributeBonusFills[index] != null
            ? attributeBonusFills[index].rectTransform
            : null;

        if (backgroundRect == null || baseRect == null || bonusRect == null)
        {
            return;
        }

        baseRatio = Mathf.Clamp01(baseRatio);
        bonusRatio = bonusRatio <= 0f ? 0f : Mathf.Clamp01(bonusRatio);

        float totalWidth = ResolveBarTotalWidth(backgroundRect);
        if (totalWidth <= 0f)
        {
            SetCompositeFillRect(baseRect, 0f, 0f);
            SetCompositeFillRect(bonusRect, 0f, 0f);
            if (bonusRect != null)
            {
                bonusRect.gameObject.SetActive(false);
            }

            return;
        }

        float totalRatio = Mathf.Clamp01(baseRatio + bonusRatio);
        baseRatio = Mathf.Clamp01(baseRatio);
        if (totalRatio < baseRatio)
        {
            totalRatio = baseRatio;
        }

        SetCompositeFillRect(baseRect, 0f, baseRatio);
        SetCompositeFillRect(bonusRect, baseRatio, totalRatio);

        if (baseRect != null)
        {
            baseRect.gameObject.SetActive(baseRatio > 0f);
        }

        if (bonusRect != null)
        {
            bool showBonus = bonusRatio > 0f && totalRatio > baseRatio;
            bonusRect.gameObject.SetActive(showBonus);
        }
    }

    private float ResolveExtraSoulDropChance(float luck)
    {
        RuntimeLootDropOnDeath preview = ResolveLootDropPreview();
        return preview != null
            ? preview.GetExtraSoulDropChanceForLuck(luck)
            : Mathf.Max(0f, luck - 1f) * 0.025f;
    }

    private float ResolveExtraRuneDropChance(float luck)
    {
        RuntimeLootDropOnDeath preview = ResolveLootDropPreview();
        return preview != null
            ? preview.GetExtraRuneDropChanceForLuck(luck)
            : Mathf.Max(0f, luck - 1f) * 0.03f;
    }

    private RuntimeLootDropOnDeath ResolveLootDropPreview()
    {
        if (cachedLootDropPreview == null)
        {
            cachedLootDropPreview = FindObjectOfType<RuntimeLootDropOnDeath>(true);
        }

        return cachedLootDropPreview;
    }

    private static RectTransform CreateRectTransform(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject.GetComponent<RectTransform>();
    }

    private static RectTransform FindRect(Transform parent, string name)
    {
        Transform child = parent != null ? parent.Find(name) : null;
        return child as RectTransform;
    }

    private static RectTransform FindNamedRect(Transform parent, params string[] names)
    {
        if (parent == null || names == null)
        {
            return null;
        }

        for (int i = 0; i < names.Length; i++)
        {
            RectTransform found = FindRect(parent, names[i]);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TextMeshProUGUI FindExistingText(Transform parent, string name)
    {
        RectTransform rect = FindRect(parent, name);
        return rect != null ? rect.GetComponent<TextMeshProUGUI>() : null;
    }

    private static Image EnsureImage(RectTransform rect, Color fallbackColor)
    {
        Image image = rect.GetComponent<Image>();
        if (image != null)
        {
            return image;
        }

        image = rect.gameObject.AddComponent<Image>();
        image.color = fallbackColor;
        return image;
    }

    private static RawImage FindExistingRawImage(Transform parent, string name)
    {
        RectTransform rect = FindRect(parent, name);
        return rect != null ? rect.GetComponent<RawImage>() : null;
    }

    private Color ResolveAttributeBaseColor(int index)
    {
        switch (index)
        {
            case 0:
                return hpBaseColor;
            case 1:
                return atkBaseColor;
            case 2:
                return defBaseColor;
            case 3:
                return magBaseColor;
            case 4:
                return resBaseColor;
            case 5:
                return spdBaseColor;
            default:
                return barFillColor;
        }
    }

    private static void ConfigureBarContainerRect(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void ConfigureCompositeFillRect(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = new Vector2(0f, 0f);
        rect.offsetMax = new Vector2(0f, 0f);
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void SetCompositeFillRect(RectTransform rect, float startRatio, float endRatio)
    {
        if (rect == null)
        {
            return;
        }

        startRatio = Mathf.Clamp01(startRatio);
        endRatio = Mathf.Clamp01(endRatio);
        if (endRatio < startRatio)
        {
            endRatio = startRatio;
        }

        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.anchorMin = new Vector2(startRatio, 0f);
        rect.anchorMax = new Vector2(endRatio, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static float ResolveBarTotalWidth(RectTransform barRect)
    {
        if (barRect == null)
        {
            return 0f;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(barRect);
        Canvas.ForceUpdateCanvases();

        float totalWidth = barRect.rect.width;
        if (totalWidth > 0f)
        {
            return totalWidth;
        }

        totalWidth = barRect.sizeDelta.x;
        return totalWidth > 0f ? totalWidth : 0f;
    }

    private RawImage CreatePreviewRawImage(Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        GameObject rawImageObject = new GameObject("PreviewRawImage", typeof(RectTransform), typeof(RawImage));
        rawImageObject.transform.SetParent(parent, false);

        RectTransform rect = rawImageObject.GetComponent<RectTransform>();
        StretchPreviewGraphic(rect);

        RawImage rawImage = rawImageObject.GetComponent<RawImage>();
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;
        return rawImage;
    }

    private static TextMeshProUGUI FindOrCreateText(Transform parent, string name, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = FindRect(parent, name);
        bool createdObject = false;
        if (rect == null)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            rect = textObject.GetComponent<RectTransform>();
            createdObject = true;
        }

        TextMeshProUGUI text = rect.GetComponent<TextMeshProUGUI>();
        bool createdComponent = false;
        if (text == null)
        {
            text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            createdComponent = true;
        }

        if (createdObject || createdComponent)
        {
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.text = string.Empty;
        }

        text.raycastTarget = false;
        return text;
    }

    private void PauseGameForPanel()
    {
        if (pausedByAttributePanel)
        {
            return;
        }

        pausedByAttributePanel = true;
        OverlayPanelStateCoordinator.SetCharacterPanelOpen(true);
    }

    private void RestoreTimeScaleIfNeeded()
    {
        if (!pausedByAttributePanel)
        {
            return;
        }

        pausedByAttributePanel = false;
        OverlayPanelStateCoordinator.SetCharacterPanelOpen(false);
    }

    public void RefreshCurrentPlayerView()
    {
        if (!isVisible)
        {
            return;
        }

        if (!EnsurePanelReady())
        {
            return;
        }

        RefreshPlayerCache(force: true);
        RefreshPanel();
        ForceRefreshPreview();
        nextRefreshTime = Time.unscaledTime + refreshInterval;
    }

    private void CloseRunePanelsForExclusiveDisplay()
    {
        RuneUIController runeController = FindObjectOfType<RuneUIController>(true);
        if (runeController != null && runeController.IsPanelOpen)
        {
            runeController.ClosePanel();
        }

        RuneBagUI runeBag = FindObjectOfType<RuneBagUI>(true);
        if (runeBag != null && runeBag.IsPanelOpen)
        {
            runeBag.ClosePanel();
        }
    }

    private void ForceRefreshPreview()
    {
        if (previewRect == null)
        {
            return;
        }

        LogPreviewState("ForceRefreshPreview before");
        RefreshPreview(force: true);
        LogPreviewState("ForceRefreshPreview after");
    }

    private void RefreshPreview(bool force)
    {
        if (previewRect == null)
        {
            return;
        }

        int playerIndex = ResolveCurrentPreviewPlayerIndex();
        bool useWorldPreview;
        GameObject targetPreviewPrefab = ResolvePreviewPrefab(playerIndex, out useWorldPreview);

        if (targetPreviewPrefab == null)
        {
            currentPreviewPlayerIndex = 0;
            currentPreviewPrefab = null;
            currentPreviewAnimationKey = null;
            ClearPreviewInstance();

            SetPreviewPlaceholderVisible(true);
            return;
        }

        if (!force && previewInstance != null && currentPreviewPlayerIndex == playerIndex && currentPreviewPrefab == targetPreviewPrefab)
        {
            ApplyPreviewTransform(previewInstance.transform, useWorldPreview);
            if (useWorldPreview)
            {
                RefreshPreviewExtraVisuals(forceRebuild: false);
            }
            else
            {
                ClearPreviewExtraVisualClones();
            }
            if (useWorldPreview)
            {
                PrepareWorldPreviewRenderChain(previewInstance);
            }

            SetPreviewVisible(true);
            SetPreviewPlaceholderVisible(false);
            return;
        }

        ClearPreviewInstance();

        currentPreviewAnimationKey = null;
        warnedMissingPreviewIdleAnimation = false;
        currentPreviewPlayerIndex = playerIndex;
        currentPreviewPrefab = targetPreviewPrefab;
        Transform parent = useWorldPreview
            ? EnsureWorldPreviewRoot()
            : (previewRootRect != null ? previewRootRect : previewRect);

        if (parent == null)
        {
            SetPreviewPlaceholderVisible(true);
            return;
        }

        previewInstance = Instantiate(targetPreviewPrefab, parent, false);
        previewInstance.name = targetPreviewPrefab.name + "_Preview";
        previewSkeletonAnimation = previewInstance.GetComponentInChildren<SkeletonAnimation>(true);
        ApplyPreviewTransform(previewInstance.transform, useWorldPreview);
        if (useWorldPreview)
        {
            RefreshPreviewExtraVisuals(forceRebuild: true);
        }
        else
        {
            ClearPreviewExtraVisualClones();
        }
        if (useWorldPreview)
        {
            PrepareWorldPreviewRenderChain(previewInstance);
        }

        PlayPreviewIdleAnimation(previewInstance);
        SetPreviewVisible(true);
        SetPreviewPlaceholderVisible(false);
    }

    private int ResolveCurrentPreviewPlayerIndex()
    {
        if (cachedPlayer == null)
        {
            return 0;
        }

        if (cachedPlayer.GetComponent<Player2PrototypeController>() != null || cachedPlayer.name.Contains("Player02"))
        {
            return 2;
        }

        if (cachedPlayer.GetComponent<Player01SkillController>() != null || cachedPlayer.name.Contains("Player01"))
        {
            return 1;
        }

        return 0;
    }

    private GameObject ResolvePreviewPrefab(int playerIndex, out bool useWorldPreview)
    {
        useWorldPreview = false;

        if (player01WorldPreviewPrefab == null)
        {
            player01WorldPreviewPrefab = Resources.Load<GameObject>("Prefabs/UI/Preview/Player01AttributeWorldPreview");
        }

        if (player02WorldPreviewPrefab == null)
        {
            player02WorldPreviewPrefab = Resources.Load<GameObject>("Prefabs/UI/Preview/Player02AttributeWorldPreview");
        }

        GameObject worldPreviewPrefab = null;
        switch (playerIndex)
        {
            case 1:
                worldPreviewPrefab = player01WorldPreviewPrefab;
                break;
            case 2:
                worldPreviewPrefab = player02WorldPreviewPrefab;
                break;
        }

        if (worldPreviewPrefab != null)
        {
            useWorldPreview = true;
            return worldPreviewPrefab;
        }

        switch (playerIndex)
        {
            case 1:
                return player01PreviewPrefab;
            case 2:
                return player02PreviewPrefab;
            default:
                return null;
        }
    }

    private void ApplyPreviewTransform(Transform previewTransform, bool useWorldPreview)
    {
        if (previewTransform == null)
        {
            return;
        }

        if (useWorldPreview)
        {
            previewTransform.localPosition = ResolveWorldPreviewPosition();
            previewTransform.localEulerAngles = ResolveWorldPreviewEuler();
            previewTransform.localScale = ResolveWorldPreviewScale();
            return;
        }

        SkeletonGraphic skeletonGraphic = previewTransform.GetComponent<SkeletonGraphic>();
        if (skeletonGraphic == null)
        {
            skeletonGraphic = previewTransform.GetComponentInChildren<SkeletonGraphic>(true);
        }

        if (skeletonGraphic != null)
        {
            RectTransform rect = skeletonGraphic.rectTransform;
            if (rect != null)
            {
                Vector2 anchoredPosition = ResolvePreviewUiAnchoredPosition();
                Vector2 size = ResolvePreviewUiSize();
                float scale = ResolvePreviewUiScale();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = size;
                rect.localScale = Vector3.one * scale;
                rect.localEulerAngles = Vector3.zero;
            }

            return;
        }

        previewTransform.localPosition = previewLocalPosition;
        previewTransform.localEulerAngles = previewLocalEuler;
        previewTransform.localScale = previewLocalScale;
    }

    private Vector3 ResolveWorldPreviewPosition()
    {
        switch (currentPreviewPlayerIndex)
        {
            case 1:
                return player01WorldPreviewPosition;
            case 2:
                return player02WorldPreviewPosition;
            default:
                return Vector3.zero;
        }
    }

    private Vector3 ResolveWorldPreviewScale()
    {
        switch (currentPreviewPlayerIndex)
        {
            case 1:
                return player01WorldPreviewScale;
            case 2:
                return player02WorldPreviewScale;
            default:
                return Vector3.one;
        }
    }

    private Vector3 ResolveWorldPreviewEuler()
    {
        switch (currentPreviewPlayerIndex)
        {
            case 1:
                return player01WorldPreviewEuler;
            case 2:
                return player02WorldPreviewEuler;
            default:
                return Vector3.zero;
        }
    }

    private void ClearPreviewInstance()
    {
        ClearPreviewExtraVisualClones();

        if (previewInstance != null)
        {
            Destroy(previewInstance);
            previewInstance = null;
        }

        previewSkeletonAnimation = null;
        currentPreviewAnimationKey = null;
        previewExtraVisualRoot = null;
        SetPreviewVisible(false);
    }

    private void RefreshPreviewExtraVisuals(bool forceRebuild)
    {
        if (previewInstance == null)
        {
            ClearPreviewExtraVisualClones();
            return;
        }

        List<PreviewExtraVisualBinding> bindings = ResolveCurrentExtraPreviewVisualBindings();
        if (bindings == null || bindings.Count == 0)
        {
            ClearPreviewExtraVisualClones();
            return;
        }

        bool needsRebuild = forceRebuild ||
                            previewExtraVisualRoot == null ||
                            previewExtraVisualRoot.parent != previewInstance.transform ||
                            previewExtraVisuals.Count != bindings.Count;

        if (!needsRebuild)
        {
            for (int i = 0; i < previewExtraVisuals.Count; i++)
            {
                PreviewExtraVisualRuntime runtime = previewExtraVisuals[i];
                if (runtime == null ||
                    runtime.binding != bindings[i] ||
                    runtime.previewTransform == null)
                {
                    needsRebuild = true;
                    break;
                }
            }
        }

        if (needsRebuild)
        {
            RebuildPreviewExtraVisuals(bindings);
        }

        SyncPreviewExtraVisualTransforms();
    }

    private List<PreviewExtraVisualBinding> ResolveCurrentExtraPreviewVisualBindings()
    {
        switch (currentPreviewPlayerIndex)
        {
            case 1:
                return player01ExtraPreviewVisuals;
            case 2:
                return player02ExtraPreviewVisuals;
            default:
                return null;
        }
    }

    private void RebuildPreviewExtraVisuals(List<PreviewExtraVisualBinding> bindings)
    {
        ClearPreviewExtraVisualClones();
        if (previewInstance == null || bindings == null || bindings.Count == 0)
        {
            return;
        }

        previewExtraVisualRoot = new GameObject("ExtraPreviewVisuals").transform;
        previewExtraVisualRoot.SetParent(previewInstance.transform, false);
        previewExtraVisualRoot.localPosition = Vector3.zero;
        previewExtraVisualRoot.localRotation = Quaternion.identity;
        previewExtraVisualRoot.localScale = Vector3.one;

        for (int i = 0; i < bindings.Count; i++)
        {
            PreviewExtraVisualBinding binding = bindings[i];
            if (binding == null || binding.source == null)
            {
                continue;
            }

            GameObject clone = Instantiate(binding.source.gameObject, previewExtraVisualRoot, false);
            clone.name = string.IsNullOrWhiteSpace(binding.previewName) ? binding.source.name : binding.previewName;
            ApplyPreviewLayer(clone);

            previewExtraVisuals.Add(new PreviewExtraVisualRuntime
            {
                binding = binding,
                sourceCharacterRoot = ResolvePreviewExtraSourceCharacterRoot(binding.source),
                sourceVisualRoot = ResolvePreviewExtraSourceVisualRoot(binding.source),
                previewVisualRoot = ResolvePreviewVisualRoot(),
                previewTransform = clone.transform,
                initialLocalPosition = clone.transform.localPosition,
                initialLocalRotation = clone.transform.localRotation,
                initialLocalScale = clone.transform.localScale
            });

            PreviewExtraVisualRuntime runtime = previewExtraVisuals[previewExtraVisuals.Count - 1];
            CachePreviewExtraVisualSorting(runtime);
            StripNonPreviewVisualComponents(clone.transform, runtime);
            EnsurePreviewExtraVisualBehaviours(runtime);
            RemapPreviewExtraVisualReferences(runtime);
            ApplyPreviewExtraVisualSorting(runtime);
        }
    }

    private void SyncPreviewExtraVisualTransforms()
    {
        for (int i = 0; i < previewExtraVisuals.Count; i++)
        {
            PreviewExtraVisualRuntime runtime = previewExtraVisuals[i];
            if (runtime == null || runtime.binding == null || runtime.previewTransform == null || runtime.binding.source == null)
            {
                continue;
            }

            ApplyPreviewExtraVisualTransform(runtime);
            ApplyPreviewLayer(runtime.previewTransform.gameObject);
            ApplyPreviewExtraVisualSorting(runtime);
        }
    }

    private void ApplyPreviewExtraVisualTransform(PreviewExtraVisualRuntime runtime)
    {
        PreviewExtraVisualBinding binding = runtime.binding;
        Transform source = binding.source;
        Transform sourceVisualRoot = runtime.sourceVisualRoot;
        Transform previewVisualRoot = runtime.previewVisualRoot;
        Transform previewTransform = runtime.previewTransform;
        Transform previewParent = previewTransform.parent;
        if (source == null || previewTransform == null || previewParent == null)
        {
            return;
        }

        Vector3 localPosition = runtime.initialLocalPosition;
        Quaternion localRotation = runtime.initialLocalRotation;
        Vector3 localScale = runtime.initialLocalScale;

        if (binding.transformMode == PreviewExtraVisualTransformMode.RelativeToSource &&
            sourceVisualRoot != null &&
            previewVisualRoot != null)
        {
            Matrix4x4 sourceRelativeMatrix = sourceVisualRoot.worldToLocalMatrix * source.localToWorldMatrix;
            Matrix4x4 previewWorldMatrix = previewVisualRoot.localToWorldMatrix * sourceRelativeMatrix;
            Matrix4x4 previewLocalMatrix = previewParent.worldToLocalMatrix * previewWorldMatrix;
            DecomposeMatrix(previewLocalMatrix, out localPosition, out localRotation, out localScale);
        }
        else if (binding.transformMode == PreviewExtraVisualTransformMode.RelativeToSource)
        {
            localPosition = source.localPosition;
            localRotation = source.localRotation;
            localScale = source.localScale;
        }

        if (binding.transformMode == PreviewExtraVisualTransformMode.ManualLocalToPreviewRoot)
        {
            previewTransform.localPosition = binding.localPositionOffset;
            Quaternion manualRotation = Quaternion.Euler(binding.localEulerOffset);
            if (binding.usePreviewRotationDriver)
            {
                manualRotation *= Quaternion.Euler(runtime.previewRotationEuler);
            }

            previewTransform.localRotation = manualRotation;
            previewTransform.localScale = Vector3.Scale(runtime.initialLocalScale, binding.localScaleMultiplier);
            return;
        }

        Vector3 positionBase = binding.followPosition ? localPosition : runtime.initialLocalPosition;
        Quaternion rotationBase = binding.followRotation ? localRotation : runtime.initialLocalRotation;
        Vector3 scaleBase = binding.followScale ? localScale : runtime.initialLocalScale;

        previewTransform.localPosition = positionBase + binding.localPositionOffset;
        previewTransform.localRotation = rotationBase * Quaternion.Euler(binding.localEulerOffset);
        previewTransform.localScale = Vector3.Scale(scaleBase, binding.localScaleMultiplier);
    }

    private Transform ResolvePreviewExtraSourceCharacterRoot(Transform source)
    {
        if (source == null)
        {
            return null;
        }

        if (cachedPlayer != null)
        {
            Transform playerRoot = cachedPlayer.transform;
            if (source == playerRoot || source.IsChildOf(playerRoot))
            {
                return playerRoot;
            }
        }

        return source.root;
    }

    private Transform ResolvePreviewExtraSourceVisualRoot(Transform source)
    {
        Transform sourceCharacterRoot = ResolvePreviewExtraSourceCharacterRoot(source);
        if (sourceCharacterRoot == null)
        {
            return source != null ? source.root : null;
        }

        SkeletonAnimation sourceSkeleton = sourceCharacterRoot.GetComponentInChildren<SkeletonAnimation>(true);
        if (sourceSkeleton != null)
        {
            return sourceSkeleton.transform;
        }

        Renderer renderer = sourceCharacterRoot.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            return renderer.transform;
        }

        return sourceCharacterRoot;
    }

    private Transform ResolvePreviewVisualRoot()
    {
        if (previewSkeletonAnimation != null)
        {
            return previewSkeletonAnimation.transform;
        }

        if (previewInstance == null)
        {
            return null;
        }

        SkeletonAnimation skeletonAnimation = previewInstance.GetComponentInChildren<SkeletonAnimation>(true);
        if (skeletonAnimation != null)
        {
            return skeletonAnimation.transform;
        }

        Renderer renderer = previewInstance.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            return renderer.transform;
        }

        return previewInstance.transform;
    }

    private void ClearPreviewExtraVisualClones()
    {
        for (int i = 0; i < previewExtraVisuals.Count; i++)
        {
            PreviewExtraVisualRuntime runtime = previewExtraVisuals[i];
            if (runtime != null && runtime.previewTransform != null)
            {
                Destroy(runtime.previewTransform.gameObject);
            }
        }

        previewExtraVisuals.Clear();

        if (previewExtraVisualRoot != null)
        {
            Destroy(previewExtraVisualRoot.gameObject);
            previewExtraVisualRoot = null;
        }
    }

    private void StripNonPreviewVisualComponents(Transform root, PreviewExtraVisualRuntime runtime)
    {
        if (root == null)
        {
            return;
        }

        Component[] components = root.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || component is Transform)
            {
                continue;
            }

            if (IsAllowedPreviewVisualComponent(component, runtime))
            {
                continue;
            }

            Object.Destroy(component);
        }

        for (int i = 0; i < root.childCount; i++)
        {
            StripNonPreviewVisualComponents(root.GetChild(i), runtime);
        }
    }

    private bool IsAllowedPreviewVisualComponent(Component component, PreviewExtraVisualRuntime runtime)
    {
        if (component is MonoBehaviour monoBehaviour)
        {
            return ShouldKeepPreviewExtraMonoBehaviour(monoBehaviour, runtime != null ? runtime.binding : null);
        }

        return component is SkeletonAnimation ||
               component is SkeletonRenderer ||
               component is SkeletonMecanim ||
               component is Renderer ||
               component is MeshFilter ||
               component is ParticleSystem ||
               component is ParticleSystemRenderer ||
               component is TrailRenderer ||
               component is LineRenderer ||
               component is Animator ||
               component is Animation;
    }

    private bool ShouldKeepPreviewExtraMonoBehaviour(MonoBehaviour monoBehaviour, PreviewExtraVisualBinding binding)
    {
        if (monoBehaviour == null)
        {
            return false;
        }

        if (binding != null &&
            binding.usePreviewRotationDriver &&
            monoBehaviour is Player2HaloRotateEffect)
        {
            return false;
        }

        if (binding != null && binding.keepMonoBehaviours)
        {
            return true;
        }

        string typeName = monoBehaviour.GetType().Name;
        if (IsKnownPreviewSafeMonoBehaviourType(typeName))
        {
            return true;
        }

        if (binding == null || binding.keepMonoBehaviourTypeNames == null)
        {
            return false;
        }

        for (int i = 0; i < binding.keepMonoBehaviourTypeNames.Length; i++)
        {
            string keepTypeName = binding.keepMonoBehaviourTypeNames[i];
            if (string.IsNullOrWhiteSpace(keepTypeName))
            {
                continue;
            }

            if (string.Equals(typeName, keepTypeName, System.StringComparison.Ordinal) ||
                string.Equals(monoBehaviour.GetType().FullName, keepTypeName, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsKnownPreviewSafeMonoBehaviourType(string typeName)
    {
        return typeName == nameof(Player2HaloRotateEffect) ||
               typeName == nameof(EyeFireHorizontalRotationController) ||
               typeName == nameof(ForceRendererSortingOrder);
    }

    private void CachePreviewExtraVisualSorting(PreviewExtraVisualRuntime runtime)
    {
        if (runtime == null)
        {
            return;
        }

        Transform sourceCharacterRoot = runtime.sourceCharacterRoot;
        Transform previewRoot = previewInstance != null ? previewInstance.transform : null;

        GetSortingOrderRange(sourceCharacterRoot, null, out runtime.sourceCharacterMinSortingOrder, out runtime.sourceCharacterMaxSortingOrder, out _);
        GetSortingOrderRange(previewRoot, runtime.previewTransform, out runtime.previewCharacterMinSortingOrder, out runtime.previewCharacterMaxSortingOrder, out runtime.previewCharacterSortingLayerId);
    }

    private static void GetSortingOrderRange(Transform root, Transform excludedSubtree, out int minOrder, out int maxOrder, out int primarySortingLayerId)
    {
        minOrder = 0;
        maxOrder = 0;
        primarySortingLayerId = 0;
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (excludedSubtree != null && renderer.transform.IsChildOf(excludedSubtree))
            {
                continue;
            }

            if (!found)
            {
                minOrder = renderer.sortingOrder;
                maxOrder = renderer.sortingOrder;
                primarySortingLayerId = renderer.sortingLayerID;
                found = true;
                continue;
            }

            minOrder = Mathf.Min(minOrder, renderer.sortingOrder);
            maxOrder = Mathf.Max(maxOrder, renderer.sortingOrder);
        }
    }

    private void ApplyPreviewExtraVisualSorting(PreviewExtraVisualRuntime runtime)
    {
        if (runtime == null || runtime.binding == null || runtime.previewTransform == null)
        {
            return;
        }

        Renderer[] renderers = runtime.previewTransform.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            int targetSortingLayerId = renderer.sortingLayerID;
            int targetSortingOrder = renderer.sortingOrder;
            switch (runtime.binding.sortMode)
            {
                case PreviewExtraVisualSortMode.BehindCharacter:
                    targetSortingLayerId = runtime.previewCharacterSortingLayerId;
                    targetSortingOrder = runtime.previewCharacterMinSortingOrder + runtime.binding.sortingOrderOffset;
                    break;
                case PreviewExtraVisualSortMode.InFrontOfCharacter:
                    targetSortingLayerId = runtime.previewCharacterSortingLayerId;
                    targetSortingOrder = runtime.previewCharacterMaxSortingOrder + runtime.binding.sortingOrderOffset;
                    break;
                default:
                    ResolveSourceRendererSorting(runtime, renderer, out targetSortingLayerId, out targetSortingOrder);
                    targetSortingOrder += runtime.binding.sortingOrderOffset;
                    break;
            }

            renderer.sortingLayerID = targetSortingLayerId;
            renderer.sortingOrder = targetSortingOrder;
        }
    }

    private void ResolveSourceRendererSorting(PreviewExtraVisualRuntime runtime, Renderer previewRenderer, out int sortingLayerId, out int sortingOrder)
    {
        sortingLayerId = previewRenderer != null ? previewRenderer.sortingLayerID : 0;
        sortingOrder = previewRenderer != null ? previewRenderer.sortingOrder : 0;
        if (runtime == null || runtime.binding == null || runtime.binding.source == null || previewRenderer == null)
        {
            return;
        }

        string relativePath = GetRelativeTransformPath(runtime.previewTransform, previewRenderer.transform);
        Transform sourceTransform = string.IsNullOrEmpty(relativePath)
            ? runtime.binding.source
            : runtime.binding.source.Find(relativePath);
        if (sourceTransform == null)
        {
            return;
        }

        Renderer sourceRenderer = sourceTransform.GetComponent(previewRenderer.GetType()) as Renderer;
        if (sourceRenderer == null)
        {
            sourceRenderer = sourceTransform.GetComponent<Renderer>();
        }

        if (sourceRenderer == null)
        {
            return;
        }

        sortingLayerId = sourceRenderer.sortingLayerID;
        sortingOrder = sourceRenderer.sortingOrder;
    }

    private void RemapPreviewExtraVisualReferences(PreviewExtraVisualRuntime runtime)
    {
        if (runtime == null || runtime.previewTransform == null)
        {
            return;
        }

        runtime.timeDrivenMaterials.Clear();
        ParticleSystem[] particleSystems = runtime.previewTransform.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            main.useUnscaledTime = true;
            particleSystem.Play(true);
        }

        Renderer[] renderers = runtime.previewTransform.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            if (!runtime.warnedMissingRenderer)
            {
                Debug.LogWarning("[PlayerAttributePanelUI] Extra preview visual has no renderer: " +
                                 (runtime.binding != null && runtime.binding.source != null ? runtime.binding.source.name : "null"));
                runtime.warnedMissingRenderer = true;
            }
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] rendererMaterials = renderer.materials;
            if (rendererMaterials == null || rendererMaterials.Length == 0)
            {
                if (!runtime.warnedMissingMaterial)
                {
                    Debug.LogWarning("[PlayerAttributePanelUI] Extra preview visual renderer is missing material: " + renderer.name, renderer);
                    runtime.warnedMissingMaterial = true;
                }

                continue;
            }

            bool foundTimePropertyOnRenderer = false;
            for (int m = 0; m < rendererMaterials.Length; m++)
            {
                Material material = rendererMaterials[m];
                if (material == null)
                {
                    continue;
                }

                if (!runtime.loggedMaterialDiagnostics && ShouldLogPreviewMaterialDiagnostics(runtime, renderer))
                {
                    LogPreviewMaterialDiagnostics(runtime, renderer, material, m);
                }

                ApplyPreviewMaterialEnhancement(material, runtime.binding);

                if (HasPreviewTimeProperty(material))
                {
                    runtime.timeDrivenMaterials.Add(material);
                    foundTimePropertyOnRenderer = true;
                }
            }

            if (!foundTimePropertyOnRenderer &&
                currentPreviewPlayerIndex == 1 &&
                !runtime.warnedMissingShaderTimeProperty)
            {
                Debug.LogWarning("[PlayerAttributePanelUI] Extra preview visual material has no exposed preview time property. " +
                                 "If Player01 flame uses Shader Graph time animation, expose a float like _PreviewTime on the material and drive that in the graph.",
                                 renderer);
                runtime.warnedMissingShaderTimeProperty = true;
            }
        }

        if (!runtime.loggedMaterialDiagnostics && ShouldLogPreviewMaterialDiagnostics(runtime, null))
        {
            runtime.loggedMaterialDiagnostics = true;
        }

        Animator[] animators = runtime.previewTransform.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }

        MonoBehaviour[] behaviours = runtime.previewTransform.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
            {
                continue;
            }

            if (behaviour is Player2HaloRotateEffect haloRotateEffect)
            {
                RemapHaloRotateEffect(haloRotateEffect, runtime);
                continue;
            }

            if (behaviour is EyeFireHorizontalRotationController eyeFireController)
            {
                eyeFireController.Reinitialize();
                continue;
            }

            if (behaviour is ForceRendererSortingOrder forceRendererSortingOrder)
            {
                InvokeIfExists(forceRendererSortingOrder, "OnEnable");
            }
        }
    }

    private void EnsurePreviewExtraVisualBehaviours(PreviewExtraVisualRuntime runtime)
    {
        if (runtime == null || runtime.binding == null || runtime.previewTransform == null)
        {
            return;
        }

        ApplyPreviewLayer(runtime.previewTransform.gameObject);
    }

    private void RemapHaloRotateEffect(Player2HaloRotateEffect haloRotateEffect, PreviewExtraVisualRuntime runtime)
    {
        if (haloRotateEffect == null)
        {
            return;
        }

        SetFieldValue(haloRotateEffect, "spineTarget", previewSkeletonAnimation);
        SetFieldValue(haloRotateEffect, "unscaledTime", true);
        SetFieldValue(haloRotateEffect, "followSpineFacingOffset", false);
    }

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        if (target == null || string.IsNullOrEmpty(fieldName))
        {
            return;
        }

        System.Reflection.FieldInfo field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (field == null)
        {
            return;
        }

        field.SetValue(target, value);
    }

    private static void InvokeIfExists(object target, string methodName)
    {
        if (target == null || string.IsNullOrEmpty(methodName))
        {
            return;
        }

        System.Reflection.MethodInfo method = target.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (method == null)
        {
            return;
        }

        method.Invoke(target, null);
    }

    private static void DecomposeMatrix(Matrix4x4 matrix, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        position = matrix.GetColumn(3);

        Vector3 x = matrix.GetColumn(0);
        Vector3 y = matrix.GetColumn(1);
        Vector3 z = matrix.GetColumn(2);

        scale = new Vector3(x.magnitude, y.magnitude, z.magnitude);

        if (scale.x > 0f)
        {
            x /= scale.x;
        }

        if (scale.y > 0f)
        {
            y /= scale.y;
        }

        if (scale.z > 0f)
        {
            z /= scale.z;
        }

        rotation = Quaternion.LookRotation(z, y);
    }

    private static string GetRelativeTransformPath(Transform root, Transform target)
    {
        if (root == null || target == null)
        {
            return null;
        }

        if (root == target)
        {
            return string.Empty;
        }

        List<string> segments = new List<string>();
        Transform current = target;
        while (current != null && current != root)
        {
            segments.Add(current.name);
            current = current.parent;
        }

        if (current != root)
        {
            return null;
        }

        segments.Reverse();
        return string.Join("/", segments.ToArray());
    }

    private static readonly string[] PreviewTimePropertyCandidates =
    {
        "_PreviewTime",
        "_UnscaledTime",
        "_ManualTime",
        "_CustomTime",
        "_TimeValue",
        "_TimeOffset"
    };

    private static readonly string[] PreviewAlphaPropertyCandidates =
    {
        "_Alpha",
        "_BodyAlpha",
        "_Opacity",
        "_OpacityIntensity",
        "_TintAlpha"
    };

    private static readonly string[] PreviewColorPropertyCandidates =
    {
        "_BaseColor",
        "_Color",
        "_EmissionColor",
        "_TintColor",
        "_MainColor",
        "_GrayColor",
        "_GreyColor",
        "_BrightColor",
        "_LightColor"
    };

    private static readonly string[] PreviewAlphaKeywordCandidates =
    {
        "alpha",
        "opacity",
        "bodyalpha",
        "tintalpha"
    };

    private static readonly string[] PreviewIntensityKeywordCandidates =
    {
        "intensity",
        "power",
        "strength",
        "emission"
    };

    private static readonly string[] PreviewBrightColorKeywordCandidates =
    {
        "bright",
        "emission",
        "light",
        "highlight",
        "亮色",
        "亮"
    };

    private static readonly string[] PreviewGrayColorKeywordCandidates =
    {
        "gray",
        "grey",
        "灰色",
        "灰"
    };

    private static readonly string[] PreviewGeneralColorKeywordCandidates =
    {
        "color",
        "hdr",
        "emission",
        "bright",
        "亮色",
        "灰色"
    };

    private static bool HasPreviewTimeProperty(Material material)
    {
        if (material == null)
        {
            return false;
        }

        for (int i = 0; i < PreviewTimePropertyCandidates.Length; i++)
        {
            if (material.HasProperty(PreviewTimePropertyCandidates[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyPreviewTimeToMaterial(Material material, float previewTime)
    {
        if (material == null)
        {
            return;
        }

        for (int i = 0; i < PreviewTimePropertyCandidates.Length; i++)
        {
            string propertyName = PreviewTimePropertyCandidates[i];
            if (!material.HasProperty(propertyName))
            {
                continue;
            }

            material.SetFloat(propertyName, previewTime);
        }
    }

    private static void ApplyPreviewMaterialEnhancement(Material material, PreviewExtraVisualBinding binding)
    {
        if (material == null || binding == null)
        {
            return;
        }

        float alphaMultiplier = Mathf.Max(0f, binding.previewAlphaMultiplier);
        float colorIntensityMultiplier = Mathf.Max(0f, binding.previewColorIntensityMultiplier);
        float emissionMultiplier = Mathf.Max(0f, binding.previewEmissionMultiplier);

        Shader shader = material.shader;
        if (shader == null)
        {
            return;
        }

        bool handledPrimaryBrightColor = false;
        bool handledPrimaryGrayColor = false;
        bool handledPrimaryAlpha = false;

        handledPrimaryBrightColor |= TryApplyPreviewColorToProperty(material, binding, "_Color", alphaMultiplier, colorIntensityMultiplier, emissionMultiplier, true, false, true);
        handledPrimaryBrightColor |= TryApplyPreviewColorToProperty(material, binding, "_BrightColor", alphaMultiplier, colorIntensityMultiplier, emissionMultiplier, true, false, false);
        handledPrimaryBrightColor |= TryApplyPreviewColorToProperty(material, binding, "_LightColor", alphaMultiplier, colorIntensityMultiplier, emissionMultiplier, true, false, false);
        handledPrimaryBrightColor |= TryApplyPreviewColorToProperty(material, binding, "_EmissionColor", alphaMultiplier, colorIntensityMultiplier, emissionMultiplier, true, false, false);

        handledPrimaryGrayColor |= TryApplyPreviewColorToProperty(material, binding, "_Color_1", alphaMultiplier, colorIntensityMultiplier, emissionMultiplier, false, true, true);
        handledPrimaryGrayColor |= TryApplyPreviewColorToProperty(material, binding, "_GrayColor", alphaMultiplier, colorIntensityMultiplier, emissionMultiplier, false, true, false);
        handledPrimaryGrayColor |= TryApplyPreviewColorToProperty(material, binding, "_GreyColor", alphaMultiplier, colorIntensityMultiplier, emissionMultiplier, false, true, false);

        handledPrimaryAlpha |= TryApplyPreviewFloatMultiplier(material, "_Alpha", alphaMultiplier);
        handledPrimaryAlpha |= TryApplyPreviewFloatMultiplier(material, "_BodyAlpha", alphaMultiplier);
        handledPrimaryAlpha |= TryApplyPreviewFloatMultiplier(material, "_Opacity", alphaMultiplier);
        handledPrimaryAlpha |= TryApplyPreviewFloatMultiplier(material, "_TintAlpha", alphaMultiplier);

        TryApplyPreviewFloatMultiplier(material, "_Intensity", colorIntensityMultiplier);
        TryApplyPreviewFloatMultiplier(material, "_EmissionIntensity", colorIntensityMultiplier * emissionMultiplier);
        TryApplyPreviewFloatMultiplier(material, "_Power", colorIntensityMultiplier);
        TryApplyPreviewFloatMultiplier(material, "_Strength", colorIntensityMultiplier);
    }

    private static bool TryApplyPreviewColorToProperty(
        Material material,
        PreviewExtraVisualBinding binding,
        string propertyName,
        float alphaMultiplier,
        float colorIntensityMultiplier,
        float emissionMultiplier,
        bool isBrightColor,
        bool isGrayColor,
        bool requireNonDefaultValue)
    {
        if (material == null || string.IsNullOrEmpty(propertyName) || !material.HasProperty(propertyName))
        {
            return false;
        }

        Color color = material.GetColor(propertyName);
        if (requireNonDefaultValue && IsDefaultLikeColor(color))
        {
            return false;
        }

        material.SetColor(propertyName, BuildPreviewEnhancedColor(color, binding, alphaMultiplier, colorIntensityMultiplier, emissionMultiplier, isBrightColor, isGrayColor));
        return true;
    }

    private static bool TryApplyPreviewFloatMultiplier(Material material, string propertyName, float multiplier)
    {
        if (material == null || string.IsNullOrEmpty(propertyName) || !material.HasProperty(propertyName))
        {
            return false;
        }

        material.SetFloat(propertyName, material.GetFloat(propertyName) * multiplier);
        return true;
    }

    private static Color BuildPreviewEnhancedColor(
        Color color,
        PreviewExtraVisualBinding binding,
        float alphaMultiplier,
        float colorIntensityMultiplier,
        float emissionMultiplier,
        bool isBrightColor,
        bool isGrayColor)
    {
        float rgbMultiplier = colorIntensityMultiplier;
        if (isGrayColor)
        {
            rgbMultiplier = Mathf.Lerp(1f, colorIntensityMultiplier, 0.25f);
        }

        if (isBrightColor)
        {
            rgbMultiplier *= emissionMultiplier;
        }

        color.r *= rgbMultiplier;
        color.g *= rgbMultiplier;
        color.b *= rgbMultiplier;
        color.a *= alphaMultiplier;

        if (binding.overridePreviewColor)
        {
            Color targetTint = binding.previewColorTint;
            float tintStrength = isBrightColor ? 0.95f : (isGrayColor ? 0.2f : 0.45f);
            Color tinted = new Color(
                color.r * targetTint.r,
                color.g * targetTint.g,
                color.b * targetTint.b,
                color.a);
            color = Color.Lerp(color, tinted, tintStrength);
        }

        return color;
    }

    private static bool IsDefaultLikeColor(Color color)
    {
        return Mathf.Approximately(color.r, 0f) &&
               Mathf.Approximately(color.g, 0f) &&
               Mathf.Approximately(color.b, 0f) &&
               Mathf.Approximately(color.a, 0f);
    }

    private bool ShouldLogPreviewMaterialDiagnostics(PreviewExtraVisualRuntime runtime, Renderer renderer)
    {
        if (runtime == null || runtime.loggedMaterialDiagnostics || runtime.binding == null || runtime.binding.source == null)
        {
            return false;
        }

        if (currentPreviewPlayerIndex != 1)
        {
            return false;
        }

        string sourcePath = GetTransformPath(runtime.binding.source) ?? string.Empty;
        if (sourcePath.Contains("\u706b\u7130"))
        {
            return renderer == null ||
                   renderer.name.Contains("\u706b\u7130") ||
                   renderer.name.Contains("Flame") ||
                   renderer.name.Contains("Fire");
        }

        string sourceName = runtime.binding.source.name;
        if (!sourceName.Contains("火焰") && !sourceName.Contains("Flame") && !sourceName.Contains("Fire"))
        {
            return false;
        }

        return renderer == null || renderer.name.Contains("火焰") || renderer.name.Contains("Flame") || renderer.name.Contains("Fire");
    }

    private void LogPreviewMaterialDiagnostics(PreviewExtraVisualRuntime runtime, Renderer renderer, Material material, int materialIndex)
    {
        if (runtime == null || material == null)
        {
            return;
        }

        Shader shader = material.shader;
        if (shader == null)
        {
            return;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(512);
        builder.AppendLine("[PlayerAttributePanelUI] Preview flame material diagnostics");
        builder.AppendLine("source=" + (runtime.binding != null && runtime.binding.source != null ? GetTransformPath(runtime.binding.source) : "null"));
        builder.AppendLine("renderer=" + (renderer != null ? renderer.name : "null"));
        builder.AppendLine("materialIndex=" + materialIndex);
        builder.AppendLine("material=" + material.name);
        builder.AppendLine("shader=" + shader.name);

        AppendMaterialFloatIfPresent(builder, material, "_Alpha", "Alpha");
        AppendMaterialFloatIfPresent(builder, material, "_BodyAlpha", "BodyAlpha");
        AppendMaterialFloatIfPresent(builder, material, "_Opacity", "Opacity");
        AppendMaterialFloatIfPresent(builder, material, "_TintAlpha", "TintAlpha");
        AppendMaterialFloatIfPresent(builder, material, "_PreviewTime", "PreviewTime");
        AppendMaterialFloatIfPresent(builder, material, "_UnscaledTime", "UnscaledTime");
        AppendMaterialFloatIfPresent(builder, material, "_ManualTime", "ManualTime");
        AppendMaterialFloatIfPresent(builder, material, "_CustomTime", "CustomTime");
        AppendMaterialFloatIfPresent(builder, material, "_Intensity", "Intensity");
        AppendMaterialFloatIfPresent(builder, material, "_EmissionIntensity", "EmissionIntensity");
        AppendMaterialFloatIfPresent(builder, material, "_Power", "Power");
        AppendMaterialFloatIfPresent(builder, material, "_Strength", "Strength");

        AppendMaterialColorIfPresent(builder, material, "_Color", "亮色Color/_Color");
        AppendMaterialColorIfPresent(builder, material, "_Color_1", "灰色Color/_Color_1");
        AppendMaterialColorIfPresent(builder, material, "_BaseColor", "BaseColor");
        AppendMaterialColorIfPresent(builder, material, "_EmissionColor", "EmissionColor");
        AppendMaterialColorIfPresent(builder, material, "_BrightColor", "BrightColor");
        AppendMaterialColorIfPresent(builder, material, "_GrayColor", "GrayColor");
        AppendMaterialColorIfPresent(builder, material, "_GreyColor", "GreyColor");

        Debug.Log(builder.ToString(), renderer != null ? renderer : runtime.previewTransform);
        runtime.loggedMaterialDiagnostics = true;
    }

    private static void AppendMaterialFloatIfPresent(System.Text.StringBuilder builder, Material material, string propertyName, string label)
    {
        if (builder == null || material == null || string.IsNullOrEmpty(propertyName) || !material.HasProperty(propertyName))
        {
            return;
        }

        builder.AppendLine("[Float] " + propertyName + " (" + label + ") = " + material.GetFloat(propertyName));
    }

    private static void AppendMaterialColorIfPresent(System.Text.StringBuilder builder, Material material, string propertyName, string label)
    {
        if (builder == null || material == null || string.IsNullOrEmpty(propertyName) || !material.HasProperty(propertyName))
        {
            return;
        }

        builder.AppendLine("[Color] " + propertyName + " (" + label + ") = " + material.GetColor(propertyName));
    }

    private static bool ShouldLogShaderProperty(string propertyName, string propertyDescription)
    {
        return MatchesAnyKeyword(propertyName, propertyDescription, PreviewAlphaKeywordCandidates) ||
               MatchesAnyKeyword(propertyName, propertyDescription, PreviewIntensityKeywordCandidates) ||
               MatchesAnyKeyword(propertyName, propertyDescription, PreviewGeneralColorKeywordCandidates);
    }

    private static bool MatchesAnyKeyword(string propertyName, string propertyDescription, string[] keywords)
    {
        if (keywords == null)
        {
            return false;
        }

        string name = propertyName ?? string.Empty;
        string description = propertyDescription ?? string.Empty;
        string loweredName = name.ToLowerInvariant();
        string loweredDescription = description.ToLowerInvariant();
        for (int i = 0; i < keywords.Length; i++)
        {
            string keyword = keywords[i];
            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            string loweredKeyword = keyword.ToLowerInvariant();
            if (loweredName.Contains(loweredKeyword) || loweredDescription.Contains(loweredKeyword) ||
                name.Contains(keyword) || description.Contains(keyword))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetShaderPropertyDescription(Shader shader, int propertyIndex)
    {
        if (shader == null)
        {
            return string.Empty;
        }

        try
        {
            return shader.GetPropertyDescription(propertyIndex);
        }
        catch
        {
            return string.Empty;
        }
    }

    private Transform EnsureWorldPreviewRoot()
    {
        if (worldPreviewRoot != null)
        {
            return worldPreviewRoot;
        }

        GameObject root = new GameObject("AttributeWorldPreviewRoot");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(0f, -1000f, 0f);
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        worldPreviewRoot = root.transform;
        return worldPreviewRoot;
    }

    private void PrepareWorldPreviewRenderChain(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        EnsurePreviewRawImage();
        EnsurePreviewRenderTexture();
        EnsurePreviewCamera();
        ApplyPreviewLayer(instance);
        WarnIfPreviewLayerCannotRender(instance);
        ConfigurePreviewCamera();

        if (previewRawImage != null)
        {
            previewRawImage.texture = previewRenderTexture;
            previewRawImage.gameObject.SetActive(true);
        }
    }

    private void EnsurePreviewRawImage()
    {
        if (previewRawImage != null)
        {
            StretchPreviewGraphic(previewRawImage.rectTransform);
            return;
        }

        Transform parent = previewRootRect != null ? previewRootRect : previewRect;
        if (parent == null)
        {
            return;
        }

        previewRawImage = FindExistingRawImage(parent, "PreviewRawImage");
        if (previewRawImage == null)
        {
            previewRawImage = CreatePreviewRawImage(parent);
        }

        if (previewRawImage != null)
        {
            StretchPreviewGraphic(previewRawImage.rectTransform);
        }
    }

    private void EnsurePreviewRenderTexture()
    {
        Vector2Int resolvedSize = ResolvePreviewTextureSize();
        if (previewRenderTexture != null &&
            previewRenderTexture.width == resolvedSize.x &&
            previewRenderTexture.height == resolvedSize.y)
        {
            return;
        }

        if (previewRenderTexture != null)
        {
            previewRenderTexture.Release();
            Destroy(previewRenderTexture);
        }

        RenderTextureFormat preferredFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)
            ? RenderTextureFormat.ARGBHalf
            : RenderTextureFormat.ARGB32;
        previewRenderTexture = new RenderTexture(resolvedSize.x, resolvedSize.y, 16, preferredFormat);
        previewRenderTexture.name = "PlayerAttributePreviewRT";
        previewRenderTexture.useMipMap = false;
        previewRenderTexture.autoGenerateMips = false;
        previewRenderTexture.Create();
    }

    private void EnsurePreviewCamera()
    {
        if (previewCamera == null)
        {
            Transform existing = transform.Find("AttributePreviewCamera");
            if (existing != null)
            {
                previewCamera = existing.GetComponent<Camera>();
            }
        }

        if (previewCamera == null)
        {
            GameObject cameraObject = new GameObject("AttributePreviewCamera");
            cameraObject.transform.SetParent(transform, false);
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCameraWasCreatedAtRuntime = true;
        }

        previewCamera.enabled = false;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 100f;
        previewCamera.allowHDR = true;
        previewCamera.targetTexture = previewRenderTexture;
        if (previewCameraWasCreatedAtRuntime)
        {
            previewCamera.orthographic = true;
        }

        if (previewCamera.orthographic)
        {
            previewCamera.orthographicSize = previewCameraOrthographicSize;
        }

        previewLayerIndex = ResolvePreviewLayerIndex();
        previewCamera.cullingMask = 1 << previewLayerIndex;
    }

    private void WarnIfPreviewLayerCannotRender(GameObject instance)
    {
        if (instance == null || previewCamera == null)
        {
            return;
        }

        int layerIndex = ResolvePreviewLayerIndex();
        if ((previewCamera.cullingMask & (1 << layerIndex)) == 0)
        {
            Debug.LogWarning("[PlayerAttributePanelUI] AttributePreviewCamera culling mask does not include preview layer '" +
                             LayerMask.LayerToName(layerIndex) + "'.", previewCamera);
        }
    }

    private Vector2 ResolvePreviewPanelSize()
    {
        float width = Mathf.Max(1f, previewPanelSize.x);
        float height = Mathf.Max(1f, previewPanelSize.y);
        return new Vector2(width, height);
    }

    private Vector2Int ResolvePreviewTextureSize()
    {
        int width = Mathf.Max(128, previewTextureSize.x);
        int height = Mathf.Max(128, previewTextureSize.y);
        return new Vector2Int(width, height);
    }

    private static void StretchPreviewGraphic(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void ConfigurePreviewCamera()
    {
        if (previewCamera == null)
        {
            return;
        }

        Vector3 focusPoint = previewInstance != null ? previewInstance.transform.position : Vector3.zero;
        previewCamera.transform.position = new Vector3(focusPoint.x, focusPoint.y, focusPoint.z - 10f);
        previewCamera.transform.rotation = Quaternion.identity;
    }

    private int ResolvePreviewLayerIndex()
    {
        int layerIndex = LayerMask.NameToLayer(previewLayerName);
        if (layerIndex < 0)
        {
            layerIndex = 0;
        }

        return layerIndex;
    }

    private void ApplyPreviewLayer(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        int layerIndex = ResolvePreviewLayerIndex();
        SetLayerRecursively(root.transform, layerIndex);
    }

    private static void SetLayerRecursively(Transform root, int layerIndex)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layerIndex;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layerIndex);
        }
    }

    private Vector2 ResolvePreviewUiAnchoredPosition()
    {
        switch (currentPreviewPlayerIndex)
        {
            case 1:
                return player01PreviewUiAnchoredPosition;
            case 2:
                return player02PreviewUiAnchoredPosition;
            default:
                return previewUiAnchoredPosition;
        }
    }

    private Vector2 ResolvePreviewUiSize()
    {
        switch (currentPreviewPlayerIndex)
        {
            case 1:
                return player01PreviewUiSize;
            case 2:
                return player02PreviewUiSize;
            default:
                return previewUiSize;
        }
    }

    private float ResolvePreviewUiScale()
    {
        switch (currentPreviewPlayerIndex)
        {
            case 1:
                return player01PreviewUiScale;
            case 2:
                return player02PreviewUiScale;
            default:
                return previewUiScale;
        }
    }

    private void PlayPreviewIdleAnimation(GameObject previewObject)
    {
        if (previewObject == null)
        {
            return;
        }

        SkeletonGraphic skeletonGraphic = previewObject.GetComponentInChildren<SkeletonGraphic>(true);
        if (skeletonGraphic != null)
        {
            skeletonGraphic.Initialize(true);
            if (TrySetIdleAnimation(skeletonGraphic))
            {
                return;
            }
        }

        SkeletonAnimation skeletonAnimation = previewObject.GetComponentInChildren<SkeletonAnimation>(true);
        if (skeletonAnimation != null)
        {
            skeletonAnimation.Initialize(true);
            TrySetIdleAnimation(skeletonAnimation);
        }
    }

    private bool TrySetIdleAnimation(SkeletonGraphic skeletonGraphic)
    {
        if (skeletonGraphic == null || skeletonGraphic.Skeleton == null || skeletonGraphic.AnimationState == null)
        {
            return false;
        }

        string animationName = ResolvePreviewIdleAnimationName(skeletonGraphic.Skeleton.Data);
        if (string.IsNullOrEmpty(animationName))
        {
            WarnMissingPreviewIdleAnimation(skeletonGraphic.name);
            return false;
        }

        if (currentPreviewAnimationKey == animationName)
        {
            return true;
        }

        skeletonGraphic.AnimationState.SetAnimation(0, animationName, true);
        skeletonGraphic.AnimationState.Apply(skeletonGraphic.Skeleton);
        skeletonGraphic.Skeleton.UpdateWorldTransform();
        skeletonGraphic.UpdateMesh();
        currentPreviewAnimationKey = animationName;
        return true;
    }

    private bool TrySetIdleAnimation(SkeletonAnimation skeletonAnimation)
    {
        if (skeletonAnimation == null || skeletonAnimation.Skeleton == null || skeletonAnimation.AnimationState == null)
        {
            return false;
        }

        string animationName = ResolvePreviewIdleAnimationName(skeletonAnimation.Skeleton.Data);
        if (string.IsNullOrEmpty(animationName))
        {
            WarnMissingPreviewIdleAnimation(skeletonAnimation.name);
            return false;
        }

        if (currentPreviewAnimationKey == animationName)
        {
            return true;
        }

        skeletonAnimation.AnimationState.SetAnimation(0, animationName, true);
        currentPreviewAnimationKey = animationName;
        return true;
    }

    private string ResolvePreviewIdleAnimationName(Spine.SkeletonData skeletonData)
    {
        if (skeletonData == null)
        {
            return null;
        }

        string preferredAnimation = ResolvePreferredPreviewIdleAnimationName();
        if (!string.IsNullOrEmpty(preferredAnimation) && skeletonData.FindAnimation(preferredAnimation) != null)
        {
            return preferredAnimation;
        }

        string[] candidates =
        {
            "Idle",
            "idle",
            "stand",
            "Stand",
            "待机"
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrEmpty(candidate))
            {
                continue;
            }

            if (skeletonData.FindAnimation(candidate) != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private string ResolvePreferredPreviewIdleAnimationName()
    {
        switch (currentPreviewPlayerIndex)
        {
            case 1:
                return player01PreviewIdleAnimationName;
            case 2:
                return player02PreviewIdleAnimationName;
            default:
                return null;
        }
    }

    private void WarnMissingPreviewIdleAnimation(string sourceName)
    {
        if (warnedMissingPreviewIdleAnimation)
        {
            return;
        }

        Debug.LogWarning("[PlayerAttributePanelUI] Missing preview idle animation on " + sourceName +
                         ". Preferred animation was '" + ResolvePreferredPreviewIdleAnimationName() + "'.");
        warnedMissingPreviewIdleAnimation = true;
    }

    private void SetPreviewVisible(bool visible)
    {
        if (previewInstance != null)
        {
            previewInstance.SetActive(visible);
        }

        if (previewRawImage != null)
        {
            previewRawImage.texture = visible ? previewRenderTexture : null;
            previewRawImage.gameObject.SetActive(visible);
        }
    }

    private void UpdatePreviewAnimationUnscaled(float deltaTime)
    {
        if (deltaTime <= 0f || previewInstance == null || panelRoot == null || !panelRoot.gameObject.activeSelf)
        {
            return;
        }

        if (!Mathf.Approximately(Time.timeScale, 0f))
        {
            return;
        }

        if (previewSkeletonAnimation == null)
        {
            previewSkeletonAnimation = previewInstance.GetComponentInChildren<SkeletonAnimation>(true);
        }

        if (previewSkeletonAnimation == null ||
            previewSkeletonAnimation.AnimationState == null ||
            previewSkeletonAnimation.Skeleton == null)
        {
            return;
        }

        previewSkeletonAnimation.AnimationState.Update(deltaTime);
        previewSkeletonAnimation.AnimationState.Apply(previewSkeletonAnimation.Skeleton);
        previewSkeletonAnimation.Skeleton.UpdateWorldTransform();
    }

    private void UpdatePreviewExtraVisualsUnscaled(float deltaTime)
    {
        if (deltaTime <= 0f || !isVisible || previewExtraVisuals.Count == 0)
        {
            return;
        }

        for (int i = 0; i < previewExtraVisuals.Count; i++)
        {
            PreviewExtraVisualRuntime runtime = previewExtraVisuals[i];
            if (runtime == null)
            {
                continue;
            }

            if (runtime.binding != null && runtime.binding.usePreviewRotationDriver)
            {
                runtime.previewRotationEuler += runtime.binding.previewRotationSpeedEuler * deltaTime;
            }

            runtime.materialPreviewTime += deltaTime;
            for (int m = 0; m < runtime.timeDrivenMaterials.Count; m++)
            {
                ApplyPreviewTimeToMaterial(runtime.timeDrivenMaterials[m], runtime.materialPreviewTime);
            }
        }
    }

    private void RenderPreviewCameraIfNeeded()
    {
        if (!isVisible || panelRoot == null || !panelRoot.gameObject.activeSelf)
        {
            return;
        }

        if (previewCamera == null || previewRenderTexture == null || previewInstance == null)
        {
            return;
        }

        ConfigurePreviewCamera();
        previewCamera.targetTexture = previewRenderTexture;
        previewCamera.Render();
    }

    private void SetPreviewPlaceholderVisible(bool visible)
    {
        if (previewText != null)
        {
            previewText.gameObject.SetActive(visible);
        }

        if (characterPreviewText != null)
        {
            characterPreviewText.gameObject.SetActive(visible);
        }

        if (previewRect != null)
        {
            TextMeshProUGUI[] placeholders = previewRect.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < placeholders.Length; i++)
            {
                TextMeshProUGUI placeholder = placeholders[i];
                if (placeholder == null)
                {
                    continue;
                }

                if (placeholder == previewText || placeholder == characterPreviewText)
                {
                    continue;
                }

                bool isPlaceholderByName =
                    string.Equals(placeholder.gameObject.name, "PreviewLabel") ||
                    string.Equals(placeholder.gameObject.name, "CharacterPreviewText");

                bool isPlaceholderByText = !string.IsNullOrEmpty(placeholder.text) &&
                                           placeholder.text.Contains("Character Preview");

                if (isPlaceholderByName || isPlaceholderByText)
                {
                    placeholder.gameObject.SetActive(visible);
                }
            }
        }
    }

    private void LogToggleState(string context)
    {
        if (!debugToggleLog)
        {
            return;
        }

        bool panelActive = panelRoot != null && panelRoot.gameObject.activeSelf;
        Debug.Log("[PlayerAttributePanelUI] " + context +
                  " | controller=" + GetTransformPath(transform) +
                  " | isVisible=" + isVisible +
                  " panelActive=" + panelActive +
                  " pausedByPanel=" + pausedByAttributePanel +
                  " timeScale=" + Time.timeScale.ToString("0.###"));
    }

    private void LogPreviewState(string context)
    {
        if (!debugToggleLog)
        {
            return;
        }

        int playerIndex = ResolveCurrentPreviewPlayerIndex();
        bool useWorldPreview;
        GameObject targetPreviewPrefab = ResolvePreviewPrefab(playerIndex, out useWorldPreview);
        Debug.Log("[PlayerAttributePanelUI] " + context +
                  " | controller=" + GetTransformPath(transform) +
                  " | playerIndex=" + playerIndex +
                  " | useWorldPreview=" + useWorldPreview +
                  " previewPrefab=" + (targetPreviewPrefab != null ? targetPreviewPrefab.name : "null") +
                  " previewInstance=" + (previewInstance != null ? previewInstance.name : "null"));
    }

    private bool AcquirePrimaryInstance()
    {
        if (primaryInstance == null)
        {
            primaryInstance = this;
            return true;
        }

        if (primaryInstance == this)
        {
            return true;
        }

        if (debugToggleLog)
        {
            Debug.LogWarning("[PlayerAttributePanelUI] Duplicate controller disabled: " + GetTransformPath(transform), this);
        }

        enabled = false;
        return false;
    }

    private static string GetTransformPath(Transform current)
    {
        if (current == null)
        {
            return "<null>";
        }

        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        return path;
    }
}
