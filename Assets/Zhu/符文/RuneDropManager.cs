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

    public int RollRuneDropCount(MonsterRank rank, float luck)
    {
        if (dropSettings != null)
        {
            return dropSettings.RollRuneDropCount(rank, luck);
        }

        int count = rank == MonsterRank.Boss ? 2 : (rank == MonsterRank.Elite ? 1 : 0);
        float extraChance = Mathf.Clamp(Mathf.Max(0f, luck) * 0.005f, 0f, 0.3f);
        if (extraChance > 0f && Random.value < extraChance)
        {
            count++;
        }

        return count;
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
