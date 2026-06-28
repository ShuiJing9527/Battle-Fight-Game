using UnityEngine;

public class RuneDropManager : MonoBehaviour
{
    private const float DefaultDropYOffset = 0.3f;

    public static RuneDropManager Instance { get; private set; }

    [Header("Rune Source")]
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

        RunePickup prefab = GetDropPrefabForRune(rune);
        if (prefab == null)
        {
            WarnMissingPrefabsOnce();
            return null;
        }

        Vector3 spawnPosition = position + Vector3.up * dropYOffset;
        RunePickup pickup = Instantiate(prefab, spawnPosition, Quaternion.identity);
        pickup.SetRune(rune);
        pickup.destroyAfterPickup = true;
        pickup.gameObject.SetActive(true);
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

        return runeLibrary.GetRandomRune();
    }

    private RunePickup GetDropPrefabForRune(RuneDefinition rune)
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
