using UnityEngine;

public class RuneDropManager : MonoBehaviour
{
    private const float DefaultDropYOffset = 0.3f;
    private const float DefaultMinRuneScatterRadius = 1.25f;
    private const float DefaultMaxRuneScatterRadius = 2.25f;
    private const float RuneDropChanceDecayPerEquippedRune = 0.02f;
    private const float DemoNormalRuneDropChance = 0.12f;
    private const float DemoEliteRuneDropChance = 0.65f;
    private const float DemoBossRuneDropChance = 1f;
    private const float DemoFinalRushRuneDropChanceMultiplier = 1.75f;

    public static RuneDropManager Instance { get; private set; }

    [Header("Rune Source")]
    [SerializeField] private RuneDropSettings dropSettings;
    [SerializeField] private RuneLibrary runeLibrary;

    [Header("Rune Drop Prefabs")]
    [SerializeField] private RunePickup[] runeDropPrefabs;

    [Header("Drop Offset")]
    [SerializeField, Min(0f)] private float dropYOffset = DefaultDropYOffset;

    [Header("Rune Scatter")]
    [SerializeField, Min(0f)] private float minRuneScatterRadius = DefaultMinRuneScatterRadius;
    [SerializeField, Min(0f)] private float maxRuneScatterRadius = DefaultMaxRuneScatterRadius;

    [Header("Debug")]
    [SerializeField] private bool debugRuneDropTraceLog = false;

    [Header("Testing")]
    [Tooltip("勾选后，所有世界符文仍会正常掉落，但不会加入背包或销毁。取消勾选后恢复正常自动拾取。")]
    [SerializeField] private bool pauseWorldRunePickupForTesting;

    private bool warnedMissingLibrary;
    private bool warnedMissingPrefabs;
    private bool lastAppliedPauseWorldRunePickupForTesting;

    private void Awake()
    {
        if (Instance == null || Instance == this)
        {
            Instance = this;
        }

        SyncWorldRunePickupPause(force: true);
    }

    private void OnEnable()
    {
        SyncWorldRunePickupPause(force: true);
    }

    private void Update()
    {
        SyncWorldRunePickupPause(force: false);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            lastAppliedPauseWorldRunePickupForTesting = pauseWorldRunePickupForTesting;
            return;
        }

        SyncWorldRunePickupPause(force: false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public RunePickup SpawnRandomRune(Vector3 position)
    {
        RuneDefinition rune = GetRandomRune();
        if (rune == null)
        {
            WarnMissingLibraryOnce();
            return null;
        }

        return SpawnRune(rune, position);
    }

    public RunePickup SpawnRune(RuneDefinition rune, Vector3 position)
    {
        if (rune == null)
        {
            return null;
        }

        GameObject prefab = GetDropPrefabObjectForRune(rune);
        if (prefab == null)
        {
            WarnMissingPrefabsOnce();
            return null;
        }

        Vector3 spawnPosition = ResolveSpawnPosition(position);
        GameObject pickupObject = Instantiate(prefab, spawnPosition, Quaternion.identity);
        pickupObject.name = rune.runeType == RuneType.None ? "RuneDrop" : $"RuneDrop_{rune.runeType}";
        RunePickup pickup = pickupObject.GetComponent<RunePickup>();
        if (pickup == null)
        {
            pickup = pickupObject.AddComponent<RunePickup>();
        }

        pickup.SetRune(rune);
        pickup.destroyAfterPickup = true;
        pickupObject.SetActive(true);

        if (debugRuneDropTraceLog)
        {
            Debug.Log($"[RuneDropTrace] Spawn rune={pickupObject.name}, position={spawnPosition}", pickupObject);
        }

        return pickup;
    }

    public RuneDefinition GetRandomRune()
    {
        if (runeLibrary == null)
        {
            runeLibrary = GetComponent<RuneLibrary>();
        }

        if (runeLibrary == null)
        {
            return null;
        }

        return dropSettings != null ? dropSettings.GetRandomRune(runeLibrary) : runeLibrary.GetRandomRune();
    }

    public int RollRuneDropCount(MonsterRank rank, float luck, int ownedRuneCount)
    {
        if (dropSettings != null)
        {
            float? eliteRoll;
            int settingsCount = NormalizeDropCount(rank, dropSettings.RollRuneDropCount(rank, luck, ownedRuneCount, out eliteRoll));
            LogRuneDropDiagnostic(
                $"rank={rank} luck={luck:F2} ownedRuneCount={ownedRuneCount} finalRuneCount={settingsCount} eliteRoll={(eliteRoll.HasValue ? eliteRoll.Value.ToString("F4") : "n/a")} settings={(dropSettings != null ? dropSettings.name : "null")} highRuneDropTestMode={(dropSettings != null ? dropSettings.IsHighRuneDropTestMode : false)}");
            return settingsCount;
        }

        float dropGateRoll = Random.value;
        float dropGateChance = ResolveDemoRankDropChance(rank, ownedRuneCount);
        if (dropGateRoll >= dropGateChance)
        {
            LogRuneDropDiagnostic(
                $"rank={rank} luck={luck:F2} ownedRuneCount={ownedRuneCount} finalRuneCount=0 dropGateRoll={dropGateRoll:F4} dropGateChance={dropGateChance:F4} settings=null highRuneDropTestMode=true");
            return 0;
        }

        if (rank == MonsterRank.Normal)
        {
            LogRuneDropDiagnostic($"rank=Normal luck={luck:F2} ownedRuneCount={ownedRuneCount} finalRuneCount=1 dropGateRoll={dropGateRoll:F4} dropGateChance={dropGateChance:F4} settings=null highRuneDropTestMode=true");
            return 1;
        }

        if (rank == MonsterRank.Elite)
        {
            float eliteRoll = Random.value;
            float thirdRuneChance = ApplyOwnedRuneDropDecay(0.05f, ownedRuneCount);
            float secondOrThirdRuneChance = ApplyOwnedRuneDropDecay(0.35f, ownedRuneCount);
            int eliteCount = NormalizeDropCount(rank, eliteRoll < thirdRuneChance ? 3 : (eliteRoll < secondOrThirdRuneChance ? 2 : 1));
            LogRuneDropDiagnostic(
                $"rank={rank} luck={luck:F2} ownedRuneCount={ownedRuneCount} finalRuneCount={eliteCount} eliteRoll={eliteRoll:F4} settings=null highRuneDropTestMode=true");
            return eliteCount;
        }

        int baseCount = 2;
        int maxCount = 6;
        int extraRollCount = 4;
        float baseExtraChance = Mathf.Clamp01(Mathf.Max(0f, luck - 1f) * 0.03f);
        float extraChance = ApplyOwnedRuneDropDecay(baseExtraChance, ownedRuneCount);
        int count = baseCount;
        for (int i = 0; i < extraRollCount && count < maxCount; i++)
        {
            float rollChance = extraChance;
            if (i >= 2)
            {
                rollChance *= 0.25f;
            }

            if (rollChance > 0f && Random.value < rollChance)
            {
                count++;
            }
        }

        int finalCount = NormalizeDropCount(rank, ClampRuneDropCountByRank(rank, count));
        LogRuneDropDiagnostic(
            $"rank={rank} luck={luck:F2} ownedRuneCount={ownedRuneCount} finalRuneCount={finalCount} eliteRoll=n/a settings=null highRuneDropTestMode=true");
        return finalCount;
    }

    private static int NormalizeDropCount(MonsterRank rank, int count)
    {
        if (rank == MonsterRank.Normal)
        {
            return Mathf.Clamp(count, 0, 1);
        }

        return Mathf.Clamp(count, 0, 3);
    }

    private static int ClampRuneDropCountByRank(MonsterRank rank, int count)
    {
        return rank switch
        {
            MonsterRank.Boss => Mathf.Clamp(count, 0, 6),
            MonsterRank.Elite => Mathf.Clamp(count, 0, 3),
            _ => Mathf.Clamp(count, 0, 1)
        };
    }

    private static float ResolveDemoRankDropChance(MonsterRank rank, int ownedRuneCount)
    {
        float chance = rank switch
        {
            MonsterRank.Boss => DemoBossRuneDropChance,
            MonsterRank.Elite => DemoEliteRuneDropChance,
            _ => DemoNormalRuneDropChance
        };

        if (EnemyDifficultyDirector.Instance != null && EnemyDifficultyDirector.Instance.IsFinalRushActive)
        {
            chance *= DemoFinalRushRuneDropChanceMultiplier;
        }

        if (rank == MonsterRank.Boss)
        {
            return Mathf.Clamp01(chance);
        }

        return ApplyOwnedRuneDropDecay(chance, ownedRuneCount);
    }

    private static float ApplyOwnedRuneDropDecay(float baseRuneDropChance, int ownedRuneCount)
    {
        float reducedChance = Mathf.Clamp01(baseRuneDropChance) - Mathf.Max(0, ownedRuneCount) * RuneDropChanceDecayPerEquippedRune;
        return Mathf.Clamp01(reducedChance);
    }

    private GameObject GetDropPrefabObjectForRune(RuneDefinition rune)
    {
        if (dropSettings != null)
        {
            GameObject settingsPrefab = dropSettings.GetDropPrefabForRune(rune);
            if (settingsPrefab != null)
            {
                return settingsPrefab;
            }
        }

        RunePickup legacyPrefab = GetLegacyDropPrefabForRune(rune);
        return legacyPrefab != null ? legacyPrefab.gameObject : null;
    }

    private RunePickup GetLegacyDropPrefabForRune(RuneDefinition rune)
    {
        if (runeDropPrefabs == null || runeDropPrefabs.Length == 0)
        {
            return null;
        }

        int startIndex = 0;
        if (rune != null && runeDropPrefabs.Length > 1)
        {
            startIndex = Mathf.Abs(rune.runeId) % runeDropPrefabs.Length;
        }

        for (int offset = 0; offset < runeDropPrefabs.Length; offset++)
        {
            int index = (startIndex + offset) % runeDropPrefabs.Length;
            RunePickup prefab = runeDropPrefabs[index];
            if (prefab != null)
            {
                return prefab;
            }
        }

        return null;
    }

    private float GetDropYOffset()
    {
        return dropSettings != null ? dropSettings.DropYOffset : Mathf.Max(0f, dropYOffset);
    }

    private Vector3 ResolveSpawnPosition(Vector3 basePosition)
    {
        Vector3 spawnPosition = basePosition + Vector3.up * GetDropYOffset();
        Vector3 horizontalOffset = ResolveHorizontalScatterOffset();
        return spawnPosition + horizontalOffset;
    }

    private Vector3 ResolveHorizontalScatterOffset()
    {
        float minRadius = Mathf.Max(0f, minRuneScatterRadius);
        float maxRadius = Mathf.Max(minRadius, maxRuneScatterRadius);
        if (maxRadius <= 0f)
        {
            return Vector3.zero;
        }

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(minRadius, maxRadius);
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
    }

    private void WarnMissingLibraryOnce()
    {
        if (warnedMissingLibrary)
        {
            return;
        }

        warnedMissingLibrary = true;
        Debug.LogWarning("[RuneDropManager] Missing RuneLibrary reference.", this);
    }

    private void WarnMissingPrefabsOnce()
    {
        if (warnedMissingPrefabs)
        {
            return;
        }

        warnedMissingPrefabs = true;
        Debug.LogWarning("[RuneDropManager] Missing rune drop prefabs.", this);
    }

    private void SyncWorldRunePickupPause(bool force)
    {
        if (!force && lastAppliedPauseWorldRunePickupForTesting == pauseWorldRunePickupForTesting)
        {
            return;
        }

        lastAppliedPauseWorldRunePickupForTesting = pauseWorldRunePickupForTesting;
        RunePickup.SetWorldRunePickupPaused(pauseWorldRunePickupForTesting);
    }

    private void LogRuneDropDiagnostic(string message)
    {
        if (!debugRuneDropTraceLog)
        {
            return;
        }

        Debug.Log("[RuneDropManagerDiag] " + message, this);
    }
}
