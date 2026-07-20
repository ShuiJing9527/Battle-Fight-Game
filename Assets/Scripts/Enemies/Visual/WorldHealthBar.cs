using UnityEngine;

public class WorldHealthBar : MonoBehaviour
{
    public Vector3 offset = new Vector3(0f, 1.4f, 0f);
    public Vector2 size = new Vector2(1.4f, 0.12f);
    [Header("Rank Offsets")]
    [SerializeField] private float normalHealthBarOffsetY = 0.25f;
    [SerializeField] private float eliteHealthBarOffsetY = 0.3f;
    [SerializeField] private float bossHealthBarOffsetY = 0.45f;
    [SerializeField] private bool debugHealthBarAnchor = false;
    public Color backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.85f);
    public Color fillColor = new Color(0.9f, 0.15f, 0.12f, 0.95f);
    public int sortingOrder = 200;
    public bool usePrefabBar = true;
    public bool createFallbackBarIfMissing = true;
    public GameObject healthBarPrefab;
    public Transform barInstanceRoot;
    public SpriteRenderer backgroundRenderer;
    public SpriteRenderer fillRenderer;

    private static Sprite whiteSprite;

    private CombatHealth combatHealth;
    private MonsterIdentity monsterIdentity;
    private MonsterRankVisual rankVisual;
    private Transform cameraTransform;
    private Renderer visualRenderer;
    private bool initialized;
    private bool usingFallbackBar;
    private bool healthVisualDirty = true;
    private bool barVisible = true;
    private float backgroundLocalZ;
    private float fillLocalZ;
    private float nextCameraResolveTime;
    private float lastKnownCurrentHealth = float.NaN;
    private float lastKnownMaxHealth = float.NaN;
    private float lastKnownShield = float.NaN;
    private float lastKnownMaxShield = float.NaN;
    private string configSource = "Default";
    private void Awake()
    {
        RefreshHealthBindings();
        RefreshVisualBindings();
        EnsureBarInitialized();
    }

    private void OnEnable()
    {
        RefreshHealthBindings();
        SubscribeHealthEvents();
        healthVisualDirty = true;
    }

    private void OnDisable()
    {
        UnsubscribeHealthEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeHealthEvents();
    }

    private void LateUpdate()
    {
        EnsureBarInitialized();
        if (barInstanceRoot == null)
        {
            return;
        }

        RefreshHealthBindings();
        RefreshVisualBindings();
        EnsureCameraCached();

        bool isVisible = IsBarRelevantForCamera();
        SetBarVisible(isVisible);
        if (!isVisible)
        {
            return;
        }

        Vector3 anchorPosition = ResolveHealthBarWorldPosition(forceLog: false);
        if (barInstanceRoot.position != anchorPosition)
        {
            barInstanceRoot.position = anchorPosition;
        }
        if (cameraTransform != null && barInstanceRoot.rotation != cameraTransform.rotation)
        {
            barInstanceRoot.rotation = cameraTransform.rotation;
        }

        RefreshBarVisualsIfNeeded(force: false);
    }

    public void RefreshWorldPositionForDebug()
    {
        EnsureBarInitialized();
        if (barInstanceRoot == null)
        {
            return;
        }

        Vector3 anchorPosition = ResolveHealthBarWorldPosition(forceLog: true);
        barInstanceRoot.position = anchorPosition;
    }

    public void ApplyHealthBarConfig(
        float normalOffsetY,
        float eliteOffsetY,
        float bossOffsetY,
        bool debug,
        string source = "Default")
    {
        normalHealthBarOffsetY = normalOffsetY;
        eliteHealthBarOffsetY = eliteOffsetY;
        bossHealthBarOffsetY = bossOffsetY;
        debugHealthBarAnchor = debug;
        configSource = string.IsNullOrWhiteSpace(source) ? "Default" : source;

        if (barInstanceRoot != null)
        {
            barInstanceRoot.position = ResolveHealthBarWorldPosition(forceLog: debug);
        }

        healthVisualDirty = true;
    }

    private void EnsureBarInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        if (usePrefabBar && healthBarPrefab != null)
        {
            CreateBarFromPrefab();
        }

        if (barInstanceRoot == null && createFallbackBarIfMissing)
        {
            CreateFallbackBar();
        }

        CacheRendererReferencesFromChildren();
        EnsureRendererSprites();
        CacheDepthOffsets();
        RefreshVisualBindings();
        SubscribeHealthEvents();
        RefreshBarVisualsIfNeeded(force: true);
    }

    private float ResolveHealthRatio()
    {
        return TryGetCombatHealthRatio(out float ratio) ? Mathf.Clamp01(ratio) : -1f;
    }

    private bool TryGetCombatHealthRatio(out float ratio)
    {
        ratio = 1f;
        if (combatHealth == null)
        {
            return false;
        }

        float currentHealth = combatHealth.currentHealth;
        float maxHealth = combatHealth.MaxHealthValue;

        if (combatHealth.resourceBank != null)
        {
            currentHealth = combatHealth.resourceBank.currentHealth;
            maxHealth = combatHealth.resourceBank.maxHealth;
        }

        if (maxHealth <= 0f)
        {
            ratio = 0f;
            return true;
        }

        ratio = Mathf.Clamp01(currentHealth / maxHealth);
        return true;
    }

    private void CreateBarFromPrefab()
    {
        GameObject instance = Instantiate(healthBarPrefab, transform);
        instance.name = "WorldHealthBar";
        barInstanceRoot = instance.transform;
    }

    private void CreateFallbackBar()
    {
        usingFallbackBar = true;

        GameObject root = new GameObject("WorldHealthBar");
        root.transform.SetParent(transform, false);
        barInstanceRoot = root.transform;

        GameObject background = CreateFallbackSpritePart("Background", backgroundColor, sortingOrder);
        background.transform.localScale = new Vector3(size.x, size.y, 1f);
        backgroundRenderer = background.GetComponent<SpriteRenderer>();

        GameObject fillObject = CreateFallbackSpritePart("Fill", fillColor, sortingOrder + 1);
        fillObject.transform.localScale = new Vector3(size.x, size.y, 1f);
        fillObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        fillRenderer = fillObject.GetComponent<SpriteRenderer>();
    }

    private GameObject CreateFallbackSpritePart(string partName, Color color, int order)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(barInstanceRoot, false);

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = GetWhiteSprite();
        renderer.color = color;
        renderer.sortingOrder = order;
        return part;
    }

    private void CacheRendererReferencesFromChildren()
    {
        if (barInstanceRoot == null)
        {
            return;
        }

        if (backgroundRenderer == null)
        {
            Transform background = barInstanceRoot.Find("Background");
            if (background != null)
            {
                backgroundRenderer = background.GetComponent<SpriteRenderer>();
            }
        }

        if (fillRenderer == null)
        {
            Transform fill = barInstanceRoot.Find("Fill");
            if (fill != null)
            {
                fillRenderer = fill.GetComponent<SpriteRenderer>();
            }
        }
    }

    private void EnsureRendererSprites()
    {
        if (backgroundRenderer != null && backgroundRenderer.sprite == null)
        {
            backgroundRenderer.sprite = GetWhiteSprite();
        }

        if (fillRenderer != null && fillRenderer.sprite == null)
        {
            fillRenderer.sprite = GetWhiteSprite();
        }
    }

    private void CacheDepthOffsets()
    {
        if (backgroundRenderer != null)
        {
            backgroundLocalZ = backgroundRenderer.transform.localPosition.z;
        }

        if (fillRenderer != null)
        {
            fillLocalZ = fillRenderer.transform.localPosition.z;
        }
    }

    private void ApplyBarSize(float ratio)
    {
        if (backgroundRenderer == null || fillRenderer == null)
        {
            return;
        }

        bool hasHealthSource = ratio >= 0f;
        ratio = hasHealthSource ? Mathf.Clamp01(ratio) : 0f;
        EnsureRendererSprites();

        backgroundRenderer.sortingOrder = sortingOrder;
        fillRenderer.sortingOrder = sortingOrder + 1;
        backgroundRenderer.enabled = hasHealthSource;
        fillRenderer.enabled = hasHealthSource && ratio > 0f;

        if (!hasHealthSource)
        {
            return;
        }

        ApplyRendererDimensions(backgroundRenderer, size.x, size.y);
        backgroundRenderer.transform.localPosition = new Vector3(0f, 0f, backgroundLocalZ);

        ApplyRendererDimensions(fillRenderer, size.x * ratio, size.y);
        float fillX = -(size.x * (1f - ratio)) * 0.5f;
        float fillZ = usingFallbackBar ? -0.01f : fillLocalZ;
        fillRenderer.transform.localPosition = new Vector3(fillX, 0f, fillZ);
    }

    private static void ApplyRendererDimensions(SpriteRenderer renderer, float width, float height)
    {
        if (renderer == null)
        {
            return;
        }

        Sprite sprite = renderer.sprite;
        float spriteWidth = 1f;
        float spriteHeight = 1f;
        if (sprite != null)
        {
            Vector2 spriteSize = sprite.bounds.size;
            spriteWidth = Mathf.Max(0.0001f, spriteSize.x);
            spriteHeight = Mathf.Max(0.0001f, spriteSize.y);
        }

        renderer.transform.localScale = new Vector3(
            Mathf.Max(0f, width) / spriteWidth,
            Mathf.Max(0f, height) / spriteHeight,
            1f);
    }

    private void RefreshHealthBindings()
    {
        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
            if (combatHealth == null)
            {
                combatHealth = GetComponentInParent<CombatHealth>();
            }
            if (combatHealth == null)
            {
                combatHealth = GetComponentInChildren<CombatHealth>(true);
            }

            if (combatHealth != null)
            {
                SubscribeHealthEvents();
                healthVisualDirty = true;
            }
        }
    }

    private void RefreshVisualBindings()
    {
        if (monsterIdentity == null)
        {
            monsterIdentity = GetComponent<MonsterIdentity>();
            if (monsterIdentity == null)
            {
                monsterIdentity = GetComponentInParent<MonsterIdentity>();
            }
        }

        if (rankVisual == null)
        {
            rankVisual = GetComponent<MonsterRankVisual>();
            if (rankVisual == null)
            {
                rankVisual = GetComponentInParent<MonsterRankVisual>();
            }
        }

        if (visualRenderer == null)
        {
            visualRenderer = ResolveVisualRenderer();
        }
    }

    private Vector3 ResolveHealthBarWorldPosition(bool forceLog)
    {
        RefreshVisualBindings();

        Renderer visualRenderer = ResolveVisualRenderer();
        float healthBarOffsetY = ResolveRankHealthBarOffsetY();
        Vector3 positionBefore = barInstanceRoot != null ? barInstanceRoot.position : transform.position + offset;
        Vector3 resolvedPosition = transform.position + offset;

        if (visualRenderer != null)
        {
            Bounds bounds = visualRenderer.bounds;
            resolvedPosition = new Vector3(
                bounds.center.x,
                bounds.max.y + healthBarOffsetY,
                bounds.center.z);

            if (debugHealthBarAnchor || forceLog)
            {
                Debug.Log(
                    "[MonsterHealthBarDebug] " +
                    "object=" + name +
                    " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
                    " source=" + configSource +
                    " visualRoot=" + (rankVisual != null && rankVisual.RuntimeVisualRoot != null ? rankVisual.RuntimeVisualRoot.name : "null") +
                    " renderer bounds maxY=" + bounds.max.y.ToString("F2") +
                    " healthBar=" + (barInstanceRoot != null ? barInstanceRoot.name : "null") +
                    " healthBar position before=" + positionBefore +
                    " healthBar position after=" + resolvedPosition +
                    " healthBarOffsetY=" + healthBarOffsetY.ToString("F2") +
                    " healthBar active=" + (barInstanceRoot != null && barInstanceRoot.gameObject.activeInHierarchy),
                    this);
            }
        }
        else if (debugHealthBarAnchor || forceLog)
        {
            Debug.Log(
                "[MonsterHealthBarDebug] " +
                "object=" + name +
                " rank=" + (monsterIdentity != null ? monsterIdentity.rank.ToString() : "Unknown") +
                " source=" + configSource +
                " visualRoot=" + (rankVisual != null && rankVisual.RuntimeVisualRoot != null ? rankVisual.RuntimeVisualRoot.name : "null") +
                " renderer bounds maxY=n/a" +
                " healthBar=" + (barInstanceRoot != null ? barInstanceRoot.name : "null") +
                " healthBar position before=" + positionBefore +
                " healthBar position after=" + resolvedPosition +
                " healthBarOffsetY=" + healthBarOffsetY.ToString("F2") +
                " healthBar active=" + (barInstanceRoot != null && barInstanceRoot.gameObject.activeInHierarchy),
                this);
        }

        return resolvedPosition;
    }

    private float ResolveRankHealthBarOffsetY()
    {
        MonsterRank rank = monsterIdentity != null ? monsterIdentity.rank : MonsterRank.Normal;
        return rank switch
        {
            MonsterRank.Boss => bossHealthBarOffsetY,
            MonsterRank.Elite => eliteHealthBarOffsetY,
            _ => normalHealthBarOffsetY
        };
    }

    private Renderer ResolveVisualRenderer()
    {
        Transform visualRoot = rankVisual != null ? rankVisual.RuntimeVisualRoot : null;
        if (visualRoot == null)
        {
            visualRoot = transform.Find("Visual_Slime");
        }

        if (visualRoot != null)
        {
            Renderer renderer = visualRoot.GetComponentInChildren<Renderer>(true);
            if (renderer != null)
            {
                return renderer;
            }
        }

        return GetComponentInChildren<Renderer>(true);
    }

    private void EnsureCameraCached()
    {
        if (cameraTransform != null)
        {
            return;
        }

        if (Time.unscaledTime < nextCameraResolveTime)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        cameraTransform = mainCamera != null ? mainCamera.transform : null;
        nextCameraResolveTime = Time.unscaledTime + 0.5f;
    }

    private bool IsBarRelevantForCamera()
    {
        if (visualRenderer == null)
        {
            visualRenderer = ResolveVisualRenderer();
        }

        return visualRenderer == null || visualRenderer.isVisible;
    }

    private void SetBarVisible(bool visible)
    {
        if (barInstanceRoot == null || barVisible == visible)
        {
            return;
        }

        barVisible = visible;
        barInstanceRoot.gameObject.SetActive(visible);
        if (visible)
        {
            healthVisualDirty = true;
        }
    }

    private void RefreshBarVisualsIfNeeded(bool force)
    {
        if (!TryReadHealthSnapshot(out float currentHealth, out float maxHealth, out float currentShield, out float maxShield))
        {
            if (force || healthVisualDirty)
            {
                ApplyBarSize(-1f);
                healthVisualDirty = false;
            }
            return;
        }

        bool valuesChanged =
            force ||
            healthVisualDirty ||
            !Mathf.Approximately(lastKnownCurrentHealth, currentHealth) ||
            !Mathf.Approximately(lastKnownMaxHealth, maxHealth) ||
            !Mathf.Approximately(lastKnownShield, currentShield) ||
            !Mathf.Approximately(lastKnownMaxShield, maxShield);

        if (!valuesChanged)
        {
            return;
        }

        lastKnownCurrentHealth = currentHealth;
        lastKnownMaxHealth = maxHealth;
        lastKnownShield = currentShield;
        lastKnownMaxShield = maxShield;
        float ratio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        ApplyBarSize(ratio);
        healthVisualDirty = false;
    }

    private bool TryReadHealthSnapshot(out float currentHealth, out float maxHealth, out float currentShield, out float maxShield)
    {
        currentHealth = 0f;
        maxHealth = 0f;
        currentShield = 0f;
        maxShield = 0f;
        if (combatHealth == null)
        {
            return false;
        }

        currentHealth = combatHealth.currentHealth;
        maxHealth = combatHealth.MaxHealthValue;
        currentShield = combatHealth.GetShield();
        maxShield = combatHealth.GetMaxShield();

        if (combatHealth.resourceBank != null)
        {
            currentHealth = combatHealth.resourceBank.currentHealth;
            maxHealth = combatHealth.resourceBank.maxHealth;
        }

        return true;
    }

    private void SubscribeHealthEvents()
    {
        if (combatHealth == null)
        {
            return;
        }

        combatHealth.Damaged -= HandleHealthChanged;
        combatHealth.OnShieldChanged -= HandleShieldChanged;
        combatHealth.Died -= HandleDeath;
        combatHealth.Damaged += HandleHealthChanged;
        combatHealth.OnShieldChanged += HandleShieldChanged;
        combatHealth.Died += HandleDeath;
    }

    private void UnsubscribeHealthEvents()
    {
        if (combatHealth == null)
        {
            return;
        }

        combatHealth.Damaged -= HandleHealthChanged;
        combatHealth.OnShieldChanged -= HandleShieldChanged;
        combatHealth.Died -= HandleDeath;
    }

    private void HandleHealthChanged(float _, GameObject __)
    {
        healthVisualDirty = true;
    }

    private void HandleShieldChanged(float _, float __)
    {
        healthVisualDirty = true;
    }

    private void HandleDeath(GameObject _)
    {
        healthVisualDirty = true;
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
        {
            return whiteSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "RuntimeHealthBarWhite";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        whiteSprite.name = "RuntimeHealthBarWhiteSprite";
        return whiteSprite;
    }

}
