using UnityEngine;

public class RuneDropManager : MonoBehaviour
{
    private const float DefaultDropYOffset = 0.3f;

    public static RuneDropManager Instance { get; private set; }

    [Header("Rune Source")]
    [SerializeField] private RuneDropSettings dropSettings;
    [SerializeField] private RuneLibrary runeLibrary;

    [Header("Rune Drop Prefabs")]
    [SerializeField] private RunePickup[] runeDropPrefabs;

    [Header("Drop Offset")]
    [SerializeField, Min(0f)] private float dropYOffset = DefaultDropYOffset;

    private bool warnedMissingLibrary;
    private bool warnedMissingPrefabs;

    private void Awake()
    {
        if (Instance == null || Instance == this)
        {
            Instance = this;
        }
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

        Vector3 spawnPosition = position + Vector3.up * GetDropYOffset();
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
            Debug.Log(
                $"[RuneDropManagerDiag] rank={rank} luck={luck:F2} ownedRuneCount={ownedRuneCount} finalRuneCount={settingsCount} eliteRoll={(eliteRoll.HasValue ? eliteRoll.Value.ToString("F4") : "n/a")} settings={(dropSettings != null ? dropSettings.name : "null")} highRuneDropTestMode={(dropSettings != null ? dropSettings.IsHighRuneDropTestMode : false)}",
                this);
            return settingsCount;
        }

        if (rank == MonsterRank.Normal)
        {
            Debug.Log($"[RuneDropManagerDiag] rank=Normal luck={luck:F2} ownedRuneCount={ownedRuneCount} finalRuneCount=0 eliteRoll=n/a settings=null highRuneDropTestMode=true", this);
            return 0;
        }

        if (rank == MonsterRank.Elite)
        {
            float eliteRoll = Random.value;
            float thirdRuneChance = ApplyOwnedRuneDropDecay(0.05f, ownedRuneCount);
            float secondOrThirdRuneChance = ApplyOwnedRuneDropDecay(0.35f, ownedRuneCount);
            int eliteCount = NormalizeDropCount(rank, eliteRoll < thirdRuneChance ? 3 : (eliteRoll < secondOrThirdRuneChance ? 2 : 1));
            Debug.Log(
                $"[RuneDropManagerDiag] rank={rank} luck={luck:F2} ownedRuneCount={ownedRuneCount} finalRuneCount={eliteCount} eliteRoll={eliteRoll:F4} settings=null highRuneDropTestMode=true",
                this);
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
        Debug.Log(
            $"[RuneDropManagerDiag] rank={rank} luck={luck:F2} ownedRuneCount={ownedRuneCount} finalRuneCount={finalCount} eliteRoll=n/a settings=null highRuneDropTestMode=true",
            this);
        return finalCount;
    }

    private static int NormalizeDropCount(MonsterRank rank, int count)
    {
        if (rank == MonsterRank.Normal)
        {
            return 0;
        }

        return Mathf.Clamp(count, 1, 3);
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

    private static float ApplyOwnedRuneDropDecay(float baseRuneDropChance, int ownedRuneCount)
    {
        float minimumChance = Mathf.Min(baseRuneDropChance, 0.05f);
        float reducedChance = baseRuneDropChance - Mathf.Max(0, ownedRuneCount) * 0.05f;
        return Mathf.Clamp(reducedChance, minimumChance, baseRuneDropChance);
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
}
