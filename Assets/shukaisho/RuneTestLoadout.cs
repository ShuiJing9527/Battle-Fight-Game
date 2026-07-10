using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public sealed class RuneTestLoadout : MonoBehaviour
{
    [Serializable]
    public sealed class RuneTestEntry
    {
        [Tooltip("要发放的符文类型。")]
        public RuneType runeType;

        [Min(0)]
        [Tooltip("开局要添加的该类型符文数量。0 表示跳过。")]
        public int amount;
    }

    private const float MaxWaitSeconds = 5f;
    private static bool grantedThisPlaySession;

    [Header("测试开关")]
    [Tooltip("开启后，在开发环境开局自动发放下面配置的测试符文。")]
    [SerializeField] private bool grantRunesOnStart = false;

    [Header("开局测试符文")]
    [Tooltip("按列表配置要发放的符文类型与数量。")]
    [SerializeField] private List<RuneTestEntry> testRunes = new List<RuneTestEntry>();

    [Header("初始化设置")]
    [Tooltip("开始查找 RuneRuntimeState 前额外等待的秒数。")]
    [SerializeField, Min(0f)] private float initializationDelay = 0.2f;

    [Tooltip("开启后，同一 Play Session 只执行一次，避免重复发放。")]
    [SerializeField] private bool grantOnlyOncePerPlaySession = true;

    private bool grantAttempted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        grantedThisPlaySession = false;
    }

    private IEnumerator Start()
    {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
        yield break;
#else
        if (!grantRunesOnStart || grantAttempted)
        {
            yield break;
        }

        if (grantOnlyOncePerPlaySession && grantedThisPlaySession)
        {
            yield break;
        }

        grantAttempted = true;

        yield return null;

        if (initializationDelay > 0f)
        {
            yield return new WaitForSeconds(initializationDelay);
        }

        if (testRunes == null || testRunes.Count == 0)
        {
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + MaxWaitSeconds;
        RuneRuntimeState runeRuntimeState = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            runeRuntimeState = ResolveRuneRuntimeState();
            if (runeRuntimeState != null)
            {
                break;
            }

            yield return null;
        }

        if (runeRuntimeState == null)
        {
            Debug.LogWarning("[RuneTestLoadout] RuneRuntimeState was not found within 5 seconds.", this);
            yield break;
        }

        Dictionary<RuneType, int> grantedCounts = new Dictionary<RuneType, int>();
        bool anyGrantAttempted = false;
        bool anyGrantSucceeded = false;

        for (int i = 0; i < testRunes.Count; i++)
        {
            RuneTestEntry entry = testRunes[i];
            if (entry == null || entry.runeType == RuneType.None || entry.amount <= 0)
            {
                continue;
            }

            anyGrantAttempted = true;
            int grantedCount = 0;
            for (int count = 0; count < entry.amount; count++)
            {
                if (!runeRuntimeState.TryGrantRuneForTesting(entry.runeType, "RuneTestLoadout"))
                {
                    break;
                }

                grantedCount++;
            }

            if (grantedCount <= 0)
            {
                continue;
            }

            anyGrantSucceeded = true;
            if (grantedCounts.TryGetValue(entry.runeType, out int existingCount))
            {
                grantedCounts[entry.runeType] = existingCount + grantedCount;
            }
            else
            {
                grantedCounts[entry.runeType] = grantedCount;
            }
        }

        if (!anyGrantAttempted)
        {
            yield break;
        }

        if (!anyGrantSucceeded)
        {
            Debug.LogWarning("[RuneTestLoadout] No test runes were granted. Check skill slots and target RuneRuntimeState initialization.", this);
            yield break;
        }

        if (grantOnlyOncePerPlaySession)
        {
            grantedThisPlaySession = true;
        }

        Debug.Log($"[RuneTestLoadout] Granted test runes: {BuildGrantSummary(grantedCounts)}", this);
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private RuneRuntimeState ResolveRuneRuntimeState()
    {
        RuneRuntimeState[] runtimeStates = FindObjectsOfType<RuneRuntimeState>(true);
        if (runtimeStates == null || runtimeStates.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < runtimeStates.Length; i++)
        {
            RuneRuntimeState candidate = runtimeStates[i];
            if (candidate == null || !candidate.isActiveAndEnabled)
            {
                continue;
            }

            CombatSkillCaster skillCaster = candidate.GetComponent<CombatSkillCaster>();
            if (skillCaster != null)
            {
                return candidate;
            }
        }

        return runtimeStates[0];
    }

    private static string BuildGrantSummary(Dictionary<RuneType, int> grantedCounts)
    {
        if (grantedCounts == null || grantedCounts.Count == 0)
        {
            return "None";
        }

        StringBuilder builder = new StringBuilder();
        bool first = true;
        foreach (KeyValuePair<RuneType, int> pair in grantedCounts)
        {
            if (pair.Value <= 0)
            {
                continue;
            }

            if (!first)
            {
                builder.Append(", ");
            }

            builder.Append(GetRuneTypeLabel(pair.Key)).Append(" x").Append(pair.Value);
            first = false;
        }

        return first ? "None" : builder.ToString();
    }

    private static string GetRuneTypeLabel(RuneType runeType)
    {
        return runeType switch
        {
            RuneType.Life => "Life",
            RuneType.Shield => "Shield",
            RuneType.Mana => "Mana",
            RuneType.Thorn => "Thorn",
            RuneType.Luck => "Lucky",
            _ => runeType.ToString()
        };
    }
#endif
}
