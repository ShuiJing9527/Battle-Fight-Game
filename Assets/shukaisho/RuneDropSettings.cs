using UnityEngine;

[CreateAssetMenu(fileName = "RuneDropSettings", menuName = "Battle-Fight-Game/Rune Drop Settings")]
public class RuneDropSettings : ScriptableObject
{
    [System.Serializable]
    public struct RuneWeight
    {
        public RuneType runeType;
        [Min(0)] public int weight;
    }

    [Header("Base Drop Count")]
    [SerializeField, Min(0)] private int normalRuneDrops = 0;
    [SerializeField, Min(0)] private int eliteRuneDrops = 1;
    [SerializeField, Min(0)] private int bossRuneDrops = 2;

    [Header("Extra Drop Chance")]
    [SerializeField, Range(0f, 1f)] private float normalExtraRuneChance = 0f;
    [SerializeField, Range(0f, 1f)] private float eliteExtraRuneChance = 0f;
    [SerializeField, Range(0f, 1f)] private float bossExtraRuneChance = 0f;
    [SerializeField, Min(0f)] private float extraRuneDropChancePerLuck = 0.005f;
    [SerializeField, Range(0f, 1f)] private float maxExtraRuneDropChanceFromLuck = 0.3f;

    [Header("Drop Shape")]
    [SerializeField, Min(0f)] private float dropYOffset = 0.3f;
    [SerializeField] private GameObject[] runeDropPrefabs;

    [Header("Rune Type Weight")]
    [SerializeField] private RuneWeight[] runeWeights =
    {
        new RuneWeight { runeType = RuneType.Life, weight = 20 },
        new RuneWeight { runeType = RuneType.Shield, weight = 20 },
        new RuneWeight { runeType = RuneType.Mana, weight = 20 },
        new RuneWeight { runeType = RuneType.Thorn, weight = 20 },
        new RuneWeight { runeType = RuneType.Luck, weight = 20 }
    };

    public float DropYOffset => Mathf.Max(0f, dropYOffset);

    public int RollRuneDropCount(MonsterRank rank, float luck)
    {
        int count = GetBaseRuneDropCount(rank);
        float extraChance = Mathf.Clamp01(GetRankExtraChance(rank) + GetExtraRuneDropChanceForLuck(luck));
        if (extraChance > 0f && Random.value < extraChance)
        {
            count++;
        }

        return Mathf.Max(0, count);
    }

    public RuneDefinition GetRandomRune(RuneLibrary library)
    {
        if (library == null)
        {
            return RuneDefinition.CreateDefaultRune(RuneType.Life);
        }

        RuneType weightedType = RollRuneType();
        if (weightedType != RuneType.None)
        {
            RuneDefinition weightedRune = library.Find(weightedType);
            if (weightedRune != null)
            {
                return weightedRune;
            }
        }

        return library.GetRandomRune();
    }

    public GameObject GetDropPrefabForRune(RuneDefinition rune)
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
            GameObject prefab = runeDropPrefabs[index];
            if (prefab != null)
            {
                return prefab;
            }
        }

        return null;
    }

    public float GetExtraRuneDropChanceForLuck(float luck)
    {
        return Mathf.Clamp(Mathf.Max(0f, luck) * extraRuneDropChancePerLuck, 0f, maxExtraRuneDropChanceFromLuck);
    }

    private int GetBaseRuneDropCount(MonsterRank rank)
    {
        return rank switch
        {
            MonsterRank.Boss => bossRuneDrops,
            MonsterRank.Elite => eliteRuneDrops,
            _ => normalRuneDrops
        };
    }

    private float GetRankExtraChance(MonsterRank rank)
    {
        return rank switch
        {
            MonsterRank.Boss => bossExtraRuneChance,
            MonsterRank.Elite => eliteExtraRuneChance,
            _ => normalExtraRuneChance
        };
    }

    private RuneType RollRuneType()
    {
        if (runeWeights == null || runeWeights.Length == 0)
        {
            return RuneType.None;
        }

        int totalWeight = 0;
        for (int i = 0; i < runeWeights.Length; i++)
        {
            if (runeWeights[i].runeType != RuneType.None)
            {
                totalWeight += Mathf.Max(0, runeWeights[i].weight);
            }
        }

        if (totalWeight <= 0)
        {
            return RuneType.None;
        }

        int roll = Random.Range(0, totalWeight);
        for (int i = 0; i < runeWeights.Length; i++)
        {
            RuneWeight entry = runeWeights[i];
            if (entry.runeType == RuneType.None)
            {
                continue;
            }

            int weight = Mathf.Max(0, entry.weight);
            if (roll < weight)
            {
                return entry.runeType;
            }

            roll -= weight;
        }

        return RuneType.None;
    }
}
