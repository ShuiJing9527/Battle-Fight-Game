using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RuntimeLootDropOnDeath : MonoBehaviour
{
    private const float LuckSoulDropChancePerPoint = 0.025f;
    private const float LuckRuneDropChancePerPoint = 0.03f;

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
    [SerializeField] private bool highRuneDropTestMode = true;
    [SerializeField, Min(0f)] private float normalRuneDropRateMultiplier = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool debugLuckDropLog = false;
    [SerializeField] private bool debugRuneDropDiagLog = true;

    private CombatHealth combatHealth;
    private RuneDropManager runeDropManager;
    private bool hasDropped;
    private bool triedMissingHealthLog;
    private bool deathEventsBound;
    private bool triedMissingRuneDropManager;
    private bool warnedMissingSoulPrefab;
    private bool warnedMissingRunePickupPrefab;
    private static int nextRuneDropCallId;

    private void OnEnable()
    {
        hasDropped = false;
        triedMissingHealthLog = false;
        deathEventsBound = false;
        warnedMissingSoulPrefab = false;
        warnedMissingRunePickupPrefab = false;
        RuntimeLootDropOnDeath[] dropComponents = GetComponents<RuntimeLootDropOnDeath>();
        if (dropComponents != null && dropComponents.Length > 1)
        {
            Debug.LogWarning(
                $"[RuneDropOnDeath] frame={Time.frameCount}, enemy={name}, enemyId={gameObject.GetInstanceID()}, componentId={GetInstanceID()}, rank={(GetComponent<MonsterIdentity>() != null ? GetComponent<MonsterIdentity>().rank.ToString() : "Unknown")}, runeCount=0 warning=MultipleRuntimeLootDropOnDeath count={dropComponents.Length}",
                this);
        }
        UnbindDeathEvents();
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
        UnbindDeathEvents();
        deathEventsBound = false;
    }

    private void DropLoot(GameObject killer)
    {
        if (hasDropped)
        {
            Debug.Log(
                $"[RuneDropOnDeath] frame={Time.frameCount}, enemy={name}, enemyId={GetInstanceID()}, componentId={GetInstanceID()}, rank={(GetComponent<MonsterIdentity>() != null ? GetComponent<MonsterIdentity>().rank.ToString() : "Unknown")}, runeCount=0 skipped=true reason=AlreadyDropped",
                this);
            return;
        }

        hasDropped = true;
        MonsterIdentity identity = GetComponent<MonsterIdentity>();
        MonsterRank rank = identity != null ? identity.rank : MonsterRank.Normal;
        bool suppressRuneDrop = identity != null && identity.suppressRuneDrop;
        CleanupBossPhaseSplit cleanupBossPhaseSplit = GetComponent<CleanupBossPhaseSplit>();
        float cleanupBossRewardMultiplier = cleanupBossPhaseSplit != null
            ? cleanupBossPhaseSplit.CleanupBossRewardMultiplier
            : 1f;
        float killerLuck = ResolveLuck(killer);
        RuneRuntimeState runeRuntimeState = ResolveRuneRuntimeState(killer);
        int dropCallId = ++nextRuneDropCallId;
        int healthInstanceId = combatHealth != null ? combatHealth.GetInstanceID() : 0;
        int dropperInstanceId = GetInstanceID();

        int soulCount = rank == MonsterRank.Boss ? 4 : (rank == MonsterRank.Elite ? 2 : 1);
        if (cleanupBossPhaseSplit != null)
        {
            soulCount = Mathf.Max(0, Mathf.RoundToInt(soulCount * cleanupBossRewardMultiplier));
        }
        for (int i = 0; i < soulCount; i++)
        {
            SoulType soulType = GetRandomSoulTypeByWeight();
            int soulPoint = soulType == SoulType.Growth ? GetRandomGrowthSoulPoint() : GetRandomResourceSoulPoint();
            if (soulType == SoulType.Growth && runeRuntimeState != null)
            {
                soulPoint = runeRuntimeState.ModifyGrowthSoulPointOnDrop(soulPoint);
            }
            CreateSoul(soulType, soulPoint, transform.position + Vector3.up * dropYOffset + RandomOffset());
        }

        bool extraSoulDropped = ShouldDropExtraSoul(killerLuck);
        if (extraSoulDropped)
        {
            SoulType extraSoulType = GetRandomSoulTypeByWeight();
            int extraSoulPoint = extraSoulType == SoulType.Growth ? GetRandomGrowthSoulPoint() : GetRandomResourceSoulPoint();
            if (extraSoulType == SoulType.Growth && runeRuntimeState != null)
            {
                extraSoulPoint = runeRuntimeState.ModifyGrowthSoulPointOnDrop(extraSoulPoint);
            }
            CreateSoul(extraSoulType, extraSoulPoint, transform.position + Vector3.up * dropYOffset + RandomOffset());
        }

        if (runeRuntimeState != null)
        {
            List<RuneRuntimeState.SoulDropRequest> bonusSoulDrops = new List<RuneRuntimeState.SoulDropRequest>();
            runeRuntimeState.AppendKillBonusSoulDrops(rank, bonusSoulDrops);
            for (int i = 0; i < bonusSoulDrops.Count; i++)
            {
                RuneRuntimeState.SoulDropRequest request = bonusSoulDrops[i];
                int requestPoint = request.soulType == SoulType.Growth
                    ? runeRuntimeState.ModifyGrowthSoulPointOnDrop(request.soulPoint)
                    : Mathf.Clamp(request.soulPoint, 1, 5);
                CreateSoul(request.soulType, requestPoint, transform.position + Vector3.up * dropYOffset + RandomOffset());
            }
        }

        float? eliteRuneRoll;
        int runeCount = ResolveRuneDropCount(rank, killerLuck, out eliteRuneRoll);
        if (rank != MonsterRank.Normal)
        {
            runeCount = Mathf.Clamp(runeCount, 1, 3);
        }
        if (cleanupBossPhaseSplit != null)
        {
            runeCount = Mathf.Max(0, Mathf.RoundToInt(runeCount * cleanupBossRewardMultiplier));
        }
        int baseRuneDropCount = GetBaseRuneDropCount(rank);
        int extraRuneCount = Mathf.Max(0, runeCount - baseRuneDropCount);
        float extraRuneChance = GetEffectiveExtraRuneDropChance(rank, killerLuck);
        string runeDropSource = runeDropManager != null ? "RuntimeLootDropOnDeath/RuneDropManager" : "RuntimeLootDropOnDeath/Fallback";

        if (debugRuneDropDiagLog)
        {
            Debug.Log(
                $"[RuneDropDiag] enemy={name} rank={rank} eliteRoll={(eliteRuneRoll.HasValue ? eliteRuneRoll.Value.ToString("F4") : "n/a")} suppressRuneDrop={suppressRuneDrop} luck={killerLuck:F2} finalRuneCount={runeCount} source=RuntimeLootDropOnDeath healthInstanceId={healthInstanceId} dropperInstanceId={dropperInstanceId} hasDropped={hasDropped} path={runeDropSource} baseRuneCount={baseRuneDropCount} extraRuneChance={extraRuneChance:F4} extraRuneCount={extraRuneCount} dropCallId={dropCallId}",
                this);
        }

        Debug.Log(
            $"[RuneDropOnDeath] frame={Time.frameCount}, enemy={name}, enemyId={gameObject.GetInstanceID()}, componentId={GetInstanceID()}, rank={rank}, runeCount={runeCount}",
            this);

        if (runeCount > 0)
        {
            Debug.Log($"Rune drop success: type=RandomPool, count={runeCount}", this);
        }

        for (int i = 0; i < runeCount; i++)
        {
            CreateRune(transform.position + Vector3.up * runeDropYOffset);
        }

        LogDropLuckDiagnostics(killer, rank, killerLuck, baseRuneDropCount, extraSoulDropped, runeCount > baseRuneDropCount, runeCount);
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
        float extraChance = GetExtraRuneDropChanceForLuck(luck) * GetRuneDropRateMultiplier();
        return extraChance > 0f && Random.value < extraChance;
    }

    public float GetExtraSoulDropChanceForLuck(float luck)
    {
        return Mathf.Max(0f, luck - 1f) * LuckSoulDropChancePerPoint;
    }

    public float GetExtraRuneDropChanceForLuck(float luck)
    {
        return Mathf.Max(0f, luck - 1f) * LuckRuneDropChancePerPoint;
    }

    public float GetRuneDropRateMultiplier()
    {
        return highRuneDropTestMode ? 1f : Mathf.Max(0f, normalRuneDropRateMultiplier);
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
        deathEventsBound = false;

        if (combatHealth == null)
        {
            combatHealth = GetComponent<CombatHealth>();
        }

        if (combatHealth != null)
        {
            combatHealth.Died -= DropLoot;
            combatHealth.Died += DropLoot;
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

    private void UnbindDeathEvents()
    {
        if (combatHealth != null)
        {
            combatHealth.Died -= DropLoot;
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
        RuneDefinition rune = library != null ? library.GetRandomRune() : RuneDefinition.CreateDefaultRune(RuneType.Life);
        if (rune == null)
        {
            return;
        }

        if (runePickupPrefab != null)
        {
            RunePickup pickup = Instantiate(runePickupPrefab, position, Quaternion.identity);
            pickup.name = rune.runeType == RuneType.None ? "RuneDrop" : $"RuneDrop_{rune.runeType}";
            pickup.rune = rune;
            pickup.destroyAfterPickup = true;
            pickup.gameObject.SetActive(true);
            return;
        }

        if (TrySpawnRuneFromRuneDropsFolder(rune, position))
        {
            return;
        }

        WarnMissingRunePickupPrefabOnce();
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

    private bool TrySpawnRuneFromRuneDropsFolder(RuneDefinition rune, Vector3 position)
    {
#if UNITY_EDITOR
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/RuneDrops" });
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            GameObject pickupObject = Instantiate(prefab, position, Quaternion.identity);
            pickupObject.name = rune.runeType == RuneType.None ? "RuneDrop" : $"RuneDrop_{rune.runeType}";
            RunePickup pickup = pickupObject.GetComponent<RunePickup>();
            if (pickup == null)
            {
                pickup = pickupObject.AddComponent<RunePickup>();
            }

            pickup.SetRune(rune);
            pickup.destroyAfterPickup = true;
            pickupObject.SetActive(true);
            return true;
        }
#endif

        return false;
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
            Debug.LogWarning($"[RuntimeLootDropOnDeath] RuneDropManager not found in scene. Trying RuneDrops prefab folder fallback on {name}.", this);
        }

        return runeDropManager;
    }

    private int ResolveRuneDropCount(MonsterRank rank, float luck, out float? eliteRoll)
    {
        eliteRoll = null;
        MonsterIdentity identity = GetComponent<MonsterIdentity>();
        if (identity != null && identity.suppressRuneDrop)
        {
            return 0;
        }

        if (rank == MonsterRank.Normal)
        {
            return 0;
        }

        RuneDropManager manager = ResolveRuneDropManager();
        if (manager != null)
        {
            return manager.RollRuneDropCount(rank, luck);
        }

        if (rank == MonsterRank.Elite)
        {
            return RollEliteRuneDropCount(out eliteRoll);
        }

        int baseCount = GetBaseRuneDropCount(rank);
        int maxCount = GetMaxRuneDropCount(rank);
        float extraChance = GetEffectiveExtraRuneDropChance(rank, luck);
        int count = baseCount;
        int extraRollCount = GetExtraRollCount(rank);

        for (int i = 0; i < extraRollCount && count < maxCount; i++)
        {
            float rollChance = extraChance;
            if (highRuneDropTestMode && rank == MonsterRank.Boss && i >= 2)
            {
                rollChance *= 0.25f;
            }

            if (rollChance > 0f && Random.value < rollChance)
            {
                count++;
            }
        }

        return ClampRuneDropCountByRank(rank, count);
    }

    private static int RollEliteRuneDropCount(out float? eliteRoll)
    {
        float roll = Random.value;
        eliteRoll = roll;
        if (roll < 0.05f)
        {
            return 3;
        }

        if (roll < 0.35f)
        {
            return 2;
        }

        return 1;
    }

    private void LogDropLuckDiagnostics(GameObject killer, MonsterRank rank, float luck, int baseRuneDropCount, bool extraSoulDropped, bool extraRuneDropped, int runeCount)
    {
        if (!debugLuckDropLog)
        {
            return;
        }

        float soulLuckMultiplier = 1f + Mathf.Max(0f, luck - 1f) * LuckSoulDropChancePerPoint;
        float runeLuckMultiplier = 1f + Mathf.Max(0f, luck - 1f) * LuckRuneDropChancePerPoint;
        float finalSoulDropChance = GetExtraSoulDropChanceForLuck(luck);
        float finalRuneDropChance = GetExtraRuneDropChanceForLuck(luck);

        Debug.Log(
            $"[DropLuckDiag] killer={(killer != null ? killer.name : "null")} luck={luck:F2} baseSoulDropChance=0.00 luckSoulDropMultiplier={soulLuckMultiplier:F2} finalSoulDropChance={finalSoulDropChance:F4} baseRuneDropChance=0.00 luckRuneDropMultiplier={runeLuckMultiplier:F2} finalRuneDropChance={finalRuneDropChance:F4} resultSoul={(extraSoulDropped ? 1 : 0)} resultRune={(extraRuneDropped ? 1 : 0)} runeCount={runeCount} baseRuneDropCount={baseRuneDropCount}",
            this);
    }

    private static int GetBaseRuneDropCount(MonsterRank rank)
    {
        return rank == MonsterRank.Boss ? 2 : (rank == MonsterRank.Elite ? 1 : 0);
    }

    private static int GetMaxRuneDropCount(MonsterRank rank)
    {
        return rank == MonsterRank.Boss ? 6 : (rank == MonsterRank.Elite ? 3 : 0);
    }

    private static int ClampRuneDropCountByRank(MonsterRank rank, int count)
    {
        return rank switch
        {
            MonsterRank.Boss => Mathf.Clamp(count, 2, 6),
            MonsterRank.Elite => Mathf.Clamp(count, 1, 3),
            _ => 0
        };
    }

    private static int GetExtraRollCount(MonsterRank rank)
    {
        return rank == MonsterRank.Boss ? 4 : (rank == MonsterRank.Elite ? 2 : 0);
    }

    private float GetEffectiveExtraRuneDropChance(MonsterRank rank, float luck)
    {
        float extraChance = GetExtraRuneDropChanceForLuck(luck);
        return highRuneDropTestMode
            ? extraChance
            : Mathf.Clamp01(extraChance * Mathf.Max(0f, normalRuneDropRateMultiplier));
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
        Debug.LogWarning("[RuneDrop] RuneDropManager and RuneDrops prefab fallback are missing. Rune was not spawned to avoid cube placeholder.", this);
    }

    private RuneRuntimeState ResolveRuneRuntimeState(GameObject killer)
    {
        if (killer == null)
        {
            return null;
        }

        RuneRuntimeState runtimeState = killer.GetComponent<RuneRuntimeState>();
        if (runtimeState == null)
        {
            runtimeState = killer.GetComponentInParent<RuneRuntimeState>();
        }

        return runtimeState;
    }

}
