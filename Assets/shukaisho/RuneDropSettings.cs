using UnityEngine;

[CreateAssetMenu(fileName = "RuneDropSettings", menuName = "Battle-Fight-Game/Rune Drop Settings")]
public class RuneDropSettings : ScriptableObject
{
    private const float LuckRuneDropChancePerPoint = 0.03f;
    private const float RuneDropChanceDecayPerEquippedRune = 0.02f;

    [System.Serializable]
    public struct RuneWeight
    {
        public RuneType runeType;
        [Min(0)] public int weight;
    }

    [Header("Base Drop Count")]
    [SerializeField, Min(0)] private int normalRuneDrops = 1;
    [SerializeField, Min(0)] private int eliteRuneDrops = 1;
    [SerializeField, Min(0)] private int bossRuneDrops = 2;

    [Header("Base Drop Chance")]
    [SerializeField, Range(0f, 1f)] private float normalRuneDropChance = 0.12f;
    [SerializeField, Range(0f, 1f)] private float eliteRuneDropChance = 0.65f;
    [SerializeField, Range(0f, 1f)] private float bossRuneDropChance = 1f;
    [SerializeField, Min(0f)] private float finalRushRuneDropChanceMultiplier = 1.75f;

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
        float dropGateRoll = Random.value;
        float dropGateChance = ResolveRankDropChance(rank, ownedRuneCount);
        eliteRoll = dropGateRoll;
        if (dropGateRoll >= dropGateChance)
        {
            return 0;
        }

        if (rank == MonsterRank.Normal)
        {
            return Mathf.Clamp(Mathf.Max(1, normalRuneDrops), 0, GetMaxRuneDropCount(rank));
        }

        if (rank == MonsterRank.Elite)
        {
            return RollEliteRuneDropCount(ownedRuneCount);
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
            _ => 1
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
            MonsterRank.Boss => Mathf.Clamp(count, 0, 6),
            MonsterRank.Elite => Mathf.Clamp(count, 0, 3),
            _ => Mathf.Clamp(count, 0, 1)
        };
    }

    private float ResolveRankDropChance(MonsterRank rank, int ownedRuneCount)
    {
        float chance = rank switch
        {
            MonsterRank.Boss => bossRuneDropChance,
            MonsterRank.Elite => eliteRuneDropChance,
            _ => normalRuneDropChance
        };

        if (EnemyDifficultyDirector.Instance != null && EnemyDifficultyDirector.Instance.IsFinalRushActive)
        {
            chance *= Mathf.Max(0f, finalRushRuneDropChanceMultiplier);
        }

        if (rank == MonsterRank.Boss)
        {
            return Mathf.Clamp01(chance);
        }

        return ApplyOwnedRuneDropDecay(chance, ownedRuneCount);
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

    private int RollEliteRuneDropCount(int ownedRuneCount)
    {
        float roll = Random.value;
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
        float reducedChance = Mathf.Clamp01(baseRuneDropChance) - Mathf.Max(0, ownedRuneCount) * RuneDropChanceDecayPerEquippedRune;
        return Mathf.Clamp01(reducedChance);
    }
}
