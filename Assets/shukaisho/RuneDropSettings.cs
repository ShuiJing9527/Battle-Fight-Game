using UnityEngine;

[CreateAssetMenu(fileName = "RuneDropSettings", menuName = "Battle-Fight-Game/Rune Drop Settings")]
public class RuneDropSettings : ScriptableObject
{
    private const float LuckRuneDropChancePerPoint = 0.03f;

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

    [Header("Drop Mode")]
    [SerializeField] private bool highRuneDropTestMode = true;
    [SerializeField, Min(0f)] private float normalRuneDropRateMultiplier = 0.25f;

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
    public bool IsHighRuneDropTestMode => highRuneDropTestMode;

    public int RollRuneDropCount(MonsterRank rank, float luck)
    {
        return RollRuneDropCount(rank, luck, 0, out _);
    }

    public int RollRuneDropCount(MonsterRank rank, float luck, out float? eliteRoll)
    {
        return RollRuneDropCount(rank, luck, 0, out eliteRoll);
    }

    public int RollRuneDropCount(MonsterRank rank, float luck, int ownedRuneCount, out float? eliteRoll)
    {
        eliteRoll = null;
        if (rank == MonsterRank.Normal)
        {
            return 0;
        }

        if (rank == MonsterRank.Elite)
        {
            return RollEliteRuneDropCount(ownedRuneCount, out eliteRoll);
        }

        int baseCount = GetBaseRuneDropCount(rank);
        int maxCount = GetMaxRuneDropCount(rank);
        float extraChance = GetEffectiveExtraRuneDropChance(rank, luck, ownedRuneCount);
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
        return Mathf.Max(0f, luck - 1f) * LuckRuneDropChancePerPoint;
    }

    public float GetRuneDropRateMultiplier()
    {
        return highRuneDropTestMode ? 1f : Mathf.Max(0f, normalRuneDropRateMultiplier);
    }

    private float GetEffectiveExtraRuneDropChance(MonsterRank rank, float luck, int ownedRuneCount)
    {
        float rankChance = GetRankExtraChance(rank);
        float luckChance = Mathf.Min(GetExtraRuneDropChanceForLuck(luck), Mathf.Max(0f, maxExtraRuneDropChanceFromLuck));
        float baseChance = Mathf.Clamp01((rankChance + luckChance) * GetRuneDropRateMultiplier());
        return ApplyOwnedRuneDropDecay(baseChance, ownedRuneCount);
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

    private int GetMaxRuneDropCount(MonsterRank rank)
    {
        return rank switch
        {
            MonsterRank.Boss => 6,
            MonsterRank.Elite => 3,
            _ => 0
        };
    }

    private int GetExtraRollCount(MonsterRank rank)
    {
        return rank switch
        {
            MonsterRank.Boss => 4,
            MonsterRank.Elite => 2,
            _ => 0
        };
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

    private float GetRankExtraChance(MonsterRank rank)
    {
        return rank switch
        {
            MonsterRank.Boss => bossExtraRuneChance,
            MonsterRank.Elite => eliteExtraRuneChance,
            _ => normalExtraRuneChance
        };
    }

    private int RollEliteRuneDropCount(int ownedRuneCount, out float? eliteRoll)
    {
        float roll = Random.value;
        eliteRoll = roll;
        float thirdRuneChance = ApplyOwnedRuneDropDecay(0.05f, ownedRuneCount);
        if (roll < thirdRuneChance)
        {
            return 3;
        }

        float secondOrThirdRuneChance = ApplyOwnedRuneDropDecay(0.35f, ownedRuneCount);
        if (roll < secondOrThirdRuneChance)
        {
            return 2;
        }

        return 1;
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

    private static float ApplyOwnedRuneDropDecay(float baseRuneDropChance, int ownedRuneCount)
    {
        float minimumChance = Mathf.Min(baseRuneDropChance, 0.05f);
        float reducedChance = baseRuneDropChance - Mathf.Max(0, ownedRuneCount) * 0.05f;
        return Mathf.Clamp(reducedChance, minimumChance, baseRuneDropChance);
    }
}
