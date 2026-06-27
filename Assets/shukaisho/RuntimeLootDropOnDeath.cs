using UnityEngine;

public class RuntimeLootDropOnDeath : MonoBehaviour
{
    [Header("Soul Drop")]
    [SerializeField] private SoulPickup soulPrefab;
    [SerializeField, Min(0f)] private float dropYOffset = 1f;
    public float dropScatterRadius = 0.8f;
    [SerializeField, Min(0)] private int lifeSoulWeight = 25;
    [SerializeField, Min(0)] private int energySoulWeight = 20;
    [SerializeField, Min(0)] private int functionSoulWeight = 15;
    [SerializeField, Min(0)] private int growthSoulWeight = 40;
    [SerializeField, Min(0)] private int resourcePoint1Weight = 50;
    [SerializeField, Min(0)] private int resourcePoint2Weight = 25;
    [SerializeField, Min(0)] private int resourcePoint3Weight = 15;
    [SerializeField, Min(0)] private int resourcePoint4Weight = 7;
    [SerializeField, Min(0)] private int resourcePoint5Weight = 3;
    [SerializeField, Min(0)] private int growthPoint1Weight = 70;
    [SerializeField, Min(0)] private int growthPoint2Weight = 18;
    [SerializeField, Min(0)] private int growthPoint3Weight = 8;
    [SerializeField, Min(0)] private int growthPoint4Weight = 3;
    [SerializeField, Min(0)] private int growthPoint5Weight = 1;
    [SerializeField, Min(0f)] private float extraSoulDropChancePerLuck = 0.01f;
    [SerializeField, Min(0f)] private float maxExtraSoulDropChance = 0.5f;

    [Header("Rune Drop")]
    [SerializeField] private RunePickup runePickupPrefab;
    [SerializeField, Min(0f)] private float runeDropYOffset = 0.25f;
    [SerializeField, Min(0f)] private float extraRuneDropChancePerLuck = 0.005f;
    [SerializeField, Min(0f)] private float maxExtraRuneDropChance = 0.3f;

    private CombatHealth combatHealth;
    private EnemyHealth enemyHealth;
    private RuneDropManager runeDropManager;
    private bool dropped;
    private bool triedMissingHealthLog;
    private bool deathEventsBound;
    private bool triedMissingRuneDropManager;
    private bool warnedMissingSoulPrefab;
    private bool warnedMissingRunePickupPrefab;

    private void OnEnable()
    {
        dropped = false;
        triedMissingHealthLog = false;
        deathEventsBound = false;
        warnedMissingSoulPrefab = false;
        warnedMissingRunePickupPrefab = false;
        TryBindDeathEvents(true);
    }

    private void Start()
    {
        TryBindDeathEvents(true);
    }

    private void Update()
    {
        if (!deathEventsBound)
        {
            TryBindDeathEvents(false);
        }
    }

    private void OnDisable()
    {
        if (deathEventsBound && combatHealth != null)
        {
            combatHealth.Died -= DropLoot;
        }

        if (deathEventsBound && enemyHealth != null)
        {
            enemyHealth.Died -= DropLoot;
        }

        deathEventsBound = false;
    }

    private void DropLoot(GameObject killer)
    {
        if (dropped)
        {
            return;
        }

        dropped = true;
        MonsterIdentity identity = GetComponent<MonsterIdentity>();
        MonsterRank rank = identity != null ? identity.rank : MonsterRank.Normal;
        float killerLuck = ResolveLuck(killer);

        int soulCount = rank == MonsterRank.Boss ? 4 : (rank == MonsterRank.Elite ? 2 : 1);
        for (int i = 0; i < soulCount; i++)
        {
            SoulType soulType = GetRandomSoulTypeByWeight();
            int soulPoint = soulType == SoulType.Growth ? GetRandomGrowthSoulPoint() : GetRandomResourceSoulPoint();
            CreateSoul(soulType, soulPoint, transform.position + Vector3.up * dropYOffset + RandomOffset());
        }

        if (ShouldDropExtraSoul(killerLuck))
        {
            SoulType extraSoulType = GetRandomSoulTypeByWeight();
            int extraSoulPoint = extraSoulType == SoulType.Growth ? GetRandomGrowthSoulPoint() : GetRandomResourceSoulPoint();
            CreateSoul(extraSoulType, extraSoulPoint, transform.position + Vector3.up * dropYOffset + RandomOffset());
        }

        int runeCount = rank == MonsterRank.Boss ? 2 : (rank == MonsterRank.Elite ? 1 : 0);
        for (int i = 0; i < runeCount; i++)
        {
            CreateRune(transform.position + Vector3.up * runeDropYOffset);
        }

        if (ShouldDropExtraRune(killerLuck))
        {
            CreateRune(transform.position + Vector3.up * runeDropYOffset + RandomOffset());
        }
    }

    private Vector3 RandomOffset()
    {
        Vector2 offset = Random.insideUnitCircle * dropScatterRadius;
        return new Vector3(offset.x, 0.15f, offset.y);
    }

    private SoulType GetRandomSoulTypeByWeight()
    {
        int lifeWeight = Mathf.Max(0, lifeSoulWeight);
        int energyWeight = Mathf.Max(0, energySoulWeight);
        int functionWeight = Mathf.Max(0, functionSoulWeight);
        int growthWeight = Mathf.Max(0, growthSoulWeight);

        int totalWeight = lifeWeight + energyWeight + functionWeight + growthWeight;
        if (totalWeight <= 0)
        {
            return SoulType.Life;
        }

        int roll = Random.Range(0, totalWeight);
        if (roll < lifeWeight)
        {
            return SoulType.Life;
        }

        roll -= lifeWeight;
        if (roll < energyWeight)
        {
            return SoulType.Energy;
        }

        roll -= energyWeight;
        if (roll < functionWeight)
        {
            return SoulType.Function;
        }

        return SoulType.Growth;
    }

    private int GetRandomResourceSoulPoint()
    {
        return GetWeightedPoint(
            resourcePoint1Weight,
            resourcePoint2Weight,
            resourcePoint3Weight,
            resourcePoint4Weight,
            resourcePoint5Weight);
    }

    private int GetRandomGrowthSoulPoint()
    {
        return GetWeightedPoint(
            growthPoint1Weight,
            growthPoint2Weight,
            growthPoint3Weight,
            growthPoint4Weight,
            growthPoint5Weight);
    }

    private static int GetWeightedPoint(int point1Weight, int point2Weight, int point3Weight, int point4Weight, int point5Weight)
    {
        int weight1 = Mathf.Max(0, point1Weight);
        int weight2 = Mathf.Max(0, point2Weight);
        int weight3 = Mathf.Max(0, point3Weight);
        int weight4 = Mathf.Max(0, point4Weight);
        int weight5 = Mathf.Max(0, point5Weight);
        int totalWeight = weight1 + weight2 + weight3 + weight4 + weight5;
        if (totalWeight <= 0)
        {
            return 1;
        }

        int roll = Random.Range(0, totalWeight);
        if (roll < weight1)
        {
            return 1;
        }

        roll -= weight1;
        if (roll < weight2)
        {
            return 2;
        }

        roll -= weight2;
        if (roll < weight3)
        {
            return 3;
        }

        roll -= weight3;
        if (roll < weight4)
        {
            return 4;
        }

        return 5;
    }

    private bool ShouldDropExtraSoul(float luck)
    {
        float extraChance = GetExtraSoulDropChanceForLuck(luck);
        return extraChance > 0f && Random.value < extraChance;
    }

    private bool ShouldDropExtraRune(float luck)
    {
        float extraChance = GetExtraRuneDropChanceForLuck(luck);
        return extraChance > 0f && Random.value < extraChance;
    }

    public float GetExtraSoulDropChanceForLuck(float luck)
    {
        return Mathf.Clamp(Mathf.Max(0f, luck) * extraSoulDropChancePerLuck, 0f, maxExtraSoulDropChance);
    }

    public float GetExtraRuneDropChanceForLuck(float luck)
    {
        return Mathf.Clamp(Mathf.Max(0f, luck) * extraRuneDropChancePerLuck, 0f, maxExtraRuneDropChance);
    }

    private float ResolveLuck(GameObject killer)
    {
        CombatStats killerStats = BattleStatUtility.GetCombatStats(killer);
        if (killerStats != null)
        {
            return Mathf.Max(0f, killerStats.luck);
        }

        Player2Bootstrap bootstrap = FindObjectOfType<Player2Bootstrap>();
        if (bootstrap != null && bootstrap.CurrentPlayer != null)
        {
            CombatStats currentPlayerStats = BattleStatUtility.GetCombatStats(bootstrap.CurrentPlayer);
            if (currentPlayerStats != null)
            {
                return Mathf.Max(0f, currentPlayerStats.luck);
            }
        }

        GameObject taggedPlayer = GameObject.FindWithTag("Player");
        CombatStats taggedPlayerStats = BattleStatUtility.GetCombatStats(taggedPlayer);
        return taggedPlayerStats != null ? Mathf.Max(0f, taggedPlayerStats.luck) : 0f;
    }

    private void CreateSoul(SoulType type, int soulPoint, Vector3 position)
    {
        if (soulPrefab != null)
        {
            SoulPickup pickup = Instantiate(soulPrefab, position, Quaternion.identity);
            pickup.soulType = type;
            pickup.soulPoint = soulPoint;
            pickup.destroyAfterPickup = true;
            pickup.gameObject.SetActive(true);
            pickup.Configure(type, soulPoint);
            EnsureDebugVisible(pickup.gameObject);
            return;
        }

        WarnMissingSoulPrefabOnce();
        GameObject soulObject = new GameObject($"{type} Soul");
        soulObject.transform.position = position;
        soulObject.transform.localScale = Vector3.one * 0.35f;

        SphereCollider collider = soulObject.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.55f;

        Rigidbody rb = soulObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        SoulPickup fallbackPickup = soulObject.AddComponent<SoulPickup>();
        fallbackPickup.Configure(type, soulPoint);
        soulObject.SetActive(true);
    }

    private void TryBindDeathEvents(bool logMissing)
    {
        if (deathEventsBound)
        {
            return;
        }

        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
        }

        if (enemyHealth == null)
        {
            enemyHealth = GetComponent<EnemyHealth>();
        }

        if (combatHealth != null)
        {
            combatHealth.Died += DropLoot;
            deathEventsBound = true;
        }

        if (enemyHealth != null)
        {
            enemyHealth.Died += DropLoot;
            deathEventsBound = true;
        }

        if (deathEventsBound)
        {
            return;
        }

        if (logMissing && !triedMissingHealthLog)
        {
            triedMissingHealthLog = true;
            Debug.LogWarning($"[RuntimeLootDropOnDeath] no health component found enemy={name}", this);
        }
    }

    private void EnsureDebugVisible(GameObject soulRoot)
    {
        if (soulRoot == null)
        {
            return;
        }

        Renderer[] renderers = soulRoot.GetComponentsInChildren<Renderer>(true);
        bool hasVisibleRenderer = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
            {
                hasVisibleRenderer = true;
                break;
            }
        }

        if (hasVisibleRenderer)
        {
            return;
        }

        GameObject debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        debugSphere.name = "SoulDropDebugVisible";
        debugSphere.transform.SetParent(soulRoot.transform, false);
        debugSphere.transform.localPosition = Vector3.zero;
        debugSphere.transform.localScale = Vector3.one * 0.25f;

        Collider collider = debugSphere.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        Renderer renderer = debugSphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard"));
            material.color = new Color(1f, 0.95f, 0.7f, 1f);
            renderer.material = material;
        }
    }

    private void CreateRune(Vector3 position)
    {
        if (TrySpawnRuneFromManager(position))
        {
            return;
        }

        RuneLibrary library = FindObjectOfType<RuneLibrary>();
        RuneDefinition rune = library != null ? library.GetRandomRune() : RuneDefinition.CreateTableRune(RuneMechanic.Combo);
        if (rune == null)
        {
            return;
        }

        if (runePickupPrefab != null)
        {
            RunePickup pickup = Instantiate(runePickupPrefab, position, Quaternion.identity);
            pickup.rune = rune;
            pickup.destroyAfterPickup = true;
            pickup.gameObject.SetActive(true);
            return;
        }

        WarnMissingRunePickupPrefabOnce();
        GameObject runeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        runeObject.name = $"Rune - {rune.runeName}";
        runeObject.transform.position = position;
        runeObject.transform.localScale = new Vector3(0.35f, 0.12f, 0.35f);

        Collider collider = runeObject.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        Rigidbody rb = runeObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = runeObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        RunePickup fallbackPickup = runeObject.GetComponent<RunePickup>();
        if (fallbackPickup == null)
        {
            fallbackPickup = runeObject.AddComponent<RunePickup>();
        }
        fallbackPickup.rune = rune;
        fallbackPickup.destroyAfterPickup = true;
    }

    private bool TrySpawnRuneFromManager(Vector3 position)
    {
        RuneDropManager manager = ResolveRuneDropManager();
        if (manager == null)
        {
            return false;
        }

        RunePickup pickup = manager.SpawnRandomRune(position);
        return pickup != null;
    }

    private RuneDropManager ResolveRuneDropManager()
    {
        if (runeDropManager != null)
        {
            return runeDropManager;
        }

        runeDropManager = Object.FindFirstObjectByType<RuneDropManager>();
        if (runeDropManager == null && !triedMissingRuneDropManager)
        {
            triedMissingRuneDropManager = true;
            Debug.LogWarning($"[RuntimeLootDropOnDeath] RuneDropManager not found in scene. Falling back to local runePrefab on {name}.", this);
        }

        return runeDropManager;
    }

    private void WarnMissingSoulPrefabOnce()
    {
        if (warnedMissingSoulPrefab)
        {
            return;
        }

        warnedMissingSoulPrefab = true;
        Debug.LogWarning("[SoulDrop] soulPrefab missing, fallback simple soul created.", this);
    }

    private void WarnMissingRunePickupPrefabOnce()
    {
        if (warnedMissingRunePickupPrefab)
        {
            return;
        }

        warnedMissingRunePickupPrefab = true;
        Debug.LogWarning("[RuneDrop] runePickupPrefab missing, fallback simple rune created.", this);
    }

}
