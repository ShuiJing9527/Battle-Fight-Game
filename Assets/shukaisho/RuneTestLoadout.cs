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
        public RuneType runeType;
        [Min(0)] public int amount;
    }

    private const float MaxResolveSeconds = 3f;
    private static bool grantedThisPlaySession;

    [Header("Test Toggle")]
    [SerializeField] private bool grantRunesOnStart = false;

    [Header("Test Rune Entries")]
    [SerializeField] private List<RuneTestEntry> testRunes = new List<RuneTestEntry>();

    [Header("Initialization")]
    [SerializeField, Min(0f)] private float initializationDelay = 0.2f;
    [SerializeField] private bool grantOnlyOncePerPlaySession = true;

    private bool grantAttempted;
    private Coroutine startupGrantRoutine;

    private sealed class RuneGrantContext
    {
        public GameObject player;
        public RuneLibrary runeLibrary;
        public CombatSkillCaster skillCaster;
        public RuneInventory runeInventory;
        public RuneRuntimeState runeRuntimeState;
        public RuneUIController runeUIController;
        public RuneBagUI runeBagUI;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        grantedThisPlaySession = false;
    }

    private void Awake()
    {
        Debug.Log("[RuneTestLoadout] Awake", this);
    }

    private void OnEnable()
    {
        Debug.Log("[RuneTestLoadout] OnEnable", this);
    }

    private void Start()
    {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
        return;
#else
        Debug.Log("[RuneTestLoadout] Start", this);
        Debug.Log("[RuneTestLoadout] Grant Runes On Start = " + grantRunesOnStart, this);
        Debug.Log("[RuneTestLoadout] Grant Only Once = " + grantOnlyOncePerPlaySession, this);
        Debug.Log("[RuneTestLoadout] Initialization Delay = " + initializationDelay.ToString("F2"), this);
        Debug.Log("[RuneTestLoadout] Test Runes Count = " + (testRunes != null ? testRunes.Count : 0), this);

        if (!grantRunesOnStart || grantAttempted)
        {
            return;
        }

        if (grantOnlyOncePerPlaySession && grantedThisPlaySession)
        {
            Debug.Log("[RuneTestLoadout] Skip auto grant because play-session one-shot already used.", this);
            return;
        }

        startupGrantRoutine = StartCoroutine(GrantRunesOnStartRoutine());
#endif
    }

    [ContextMenu("TEST/Grant Test Runes Now")]
    public void GrantTestRunesNow()
    {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
        return;
#else
        if (startupGrantRoutine != null)
        {
            StopCoroutine(startupGrantRoutine);
            startupGrantRoutine = null;
        }

        StartCoroutine(GrantTestRunesRoutine("ContextMenu"));
#endif
    }

    [ContextMenu("TEST/Print Rune Test State")]
    public void PrintRuneTestState()
    {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
        return;
#else
        RuneGrantContext context = ResolveContext(logResolution: true);
        int bagCount = context.runeInventory != null ? context.runeInventory.Count : -1;
        int runtimeLuck = context.runeRuntimeState != null ? context.runeRuntimeState.GetGlobalRuneCount(RuneType.Luck) : -1;

        Debug.Log(
            "[RuneTestLoadout] Print State " +
            "player=" + (context.player != null ? context.player.name : "null") +
            " runeLibrary=" + (context.runeLibrary != null ? context.runeLibrary.name : "null") +
            " runeInventory=" + (context.runeInventory != null ? context.runeInventory.name : "null") +
            " bagCount=" + bagCount +
            " runeRuntimeState=" + (context.runeRuntimeState != null ? context.runeRuntimeState.name : "null") +
            " luckCount=" + runtimeLuck +
            " runeUIController=" + (context.runeUIController != null ? context.runeUIController.name : "null") +
            " runeBagUI=" + (context.runeBagUI != null ? context.runeBagUI.name : "null"),
            this);
#endif
    }

    [ContextMenu("TEST/Refresh Rune UI")]
    public void RefreshRuneUIForTest()
    {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
        return;
#else
        RuneGrantContext context = ResolveContext(logResolution: true);
        RefreshRuneUI(context);
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private IEnumerator GrantRunesOnStartRoutine()
    {
        grantAttempted = true;
        yield return GrantTestRunesRoutine("Start");
        startupGrantRoutine = null;
    }

    private IEnumerator GrantTestRunesRoutine(string source)
    {
        if (initializationDelay > 0f)
        {
            yield return new WaitForSeconds(initializationDelay);
        }

        if (testRunes == null || testRunes.Count == 0)
        {
            Debug.LogWarning("[RuneTestLoadout] Test Runes Count = 0, nothing to grant.", this);
            yield break;
        }

        float deadline = Time.realtimeSinceStartup + MaxResolveSeconds;
        RuneGrantContext context = null;

        while (Time.realtimeSinceStartup < deadline)
        {
            context = ResolveContext(logResolution: false);
            if (context.runeInventory != null)
            {
                break;
            }

            yield return null;
        }

        if (context == null || context.runeInventory == null)
        {
            Debug.LogWarning("[RuneTestLoadout] Failed to resolve RuneInventory within 3 seconds.", this);
            PrintRuneTestState();
            yield break;
        }

        GrantResolvedRunes(context, source);
    }

    private void GrantResolvedRunes(RuneGrantContext context, string source)
    {
        if (context == null || context.runeInventory == null)
        {
            Debug.LogWarning("[RuneTestLoadout] GrantResolvedRunes aborted because RuneInventory is null.", this);
            return;
        }

        LogResolution(context);

        int beforeCount = context.runeInventory.Count;
        Debug.Log("[RuneTestLoadout] before grant bag count = " + beforeCount, this);

        Dictionary<RuneType, int> grantedCounts = new Dictionary<RuneType, int>();
        bool anyGrantSucceeded = false;

        for (int i = 0; i < testRunes.Count; i++)
        {
            RuneTestEntry entry = testRunes[i];
            if (entry == null || entry.runeType == RuneType.None || entry.amount <= 0)
            {
                continue;
            }

            for (int count = 0; count < entry.amount; count++)
            {
                string reason;
                bool success = TryGrantRuneToInventory(context, entry.runeType, source, out reason);
                Debug.Log(
                    "[RuneTestLoadout] grant rune type=" + entry.runeType +
                    " amount=1 success=" + success +
                    " reason=" + reason,
                    this);

                if (!success)
                {
                    continue;
                }

                anyGrantSucceeded = true;
                if (grantedCounts.TryGetValue(entry.runeType, out int currentCount))
                {
                    grantedCounts[entry.runeType] = currentCount + 1;
                }
                else
                {
                    grantedCounts[entry.runeType] = 1;
                }
            }
        }

        int afterCount = context.runeInventory.Count;
        Debug.Log("[RuneTestLoadout] after grant bag count = " + afterCount, this);

        if (!anyGrantSucceeded)
        {
            Debug.LogWarning("[RuneTestLoadout] No test runes were granted into the active RuneInventory.", this);
            return;
        }

        if (grantOnlyOncePerPlaySession)
        {
            grantedThisPlaySession = true;
        }

        Debug.Log("[RuneTestLoadout] Granted test runes: " + BuildGrantSummary(grantedCounts), this);
        RefreshRuneUI(context);
    }

    private bool TryGrantRuneToInventory(RuneGrantContext context, RuneType runeType, string source, out string reason)
    {
        reason = "Unknown";
        if (context == null)
        {
            reason = "ContextNull";
            return false;
        }

        if (context.runeInventory == null)
        {
            reason = "RuneInventoryNull";
            return false;
        }

        if (runeType == RuneType.None)
        {
            reason = "RuneTypeNone";
            return false;
        }

        RuneDefinition runeTemplate = context.runeLibrary != null ? context.runeLibrary.Find(runeType) : null;
        if (runeTemplate == null)
        {
            Debug.LogWarning("[RuneTestLoadout] RuneType " + runeType + " exists but RuneData not found.", this);
        }

        RuneDefinition runtimeRune = CloneRuneDefinition(runeTemplate) ?? RuneDefinition.CreateDefaultRune(runeType);
        if (runtimeRune == null || runtimeRune.runeType == RuneType.None)
        {
            reason = "RuneDefinitionCreateFailed";
            return false;
        }

        NormalizeDefaultRuneText(runtimeRune, runeType);
        context.runeInventory.AddRune(runtimeRune, "RuneTestLoadout-" + source);
        reason = "AddedToRuneInventory";
        return true;
    }

    private RuneGrantContext ResolveContext(bool logResolution)
    {
        RuneGrantContext context = new RuneGrantContext();

        RuneUIContextResolver.Resolve(
            out context.player,
            out context.runeLibrary,
            out context.skillCaster,
            out context.runeInventory);

        context.runeRuntimeState = ResolveRuneRuntimeState(context.player, context.skillCaster);
        context.runeUIController = FindObjectOfType<RuneUIController>(true);
        context.runeBagUI = FindObjectOfType<RuneBagUI>(true);

        if (context.runeInventory == null && context.runeBagUI != null)
        {
            context.runeBagUI.RefreshAll();
            context.runeInventory = context.runeBagUI.runeInventory;
        }

        if (logResolution)
        {
            LogResolution(context);
        }

        return context;
    }

    private void LogResolution(RuneGrantContext context)
    {
        Debug.Log(
            "[RuneTestLoadout] Resolve Target " +
            "found RuneSystem=" + (context.runeRuntimeState != null) +
            " found RuneInventory=" + (context.runeInventory != null) +
            " found RuneBag=" + (context.runeBagUI != null) +
            " found RuneBagUI=" + (context.runeBagUI != null) +
            " found RuneUIController=" + (context.runeUIController != null) +
            " player=" + (context.player != null ? context.player.name : "null") +
            " runeInventoryName=" + (context.runeInventory != null ? context.runeInventory.name : "null") +
            " runeRuntimeStateName=" + (context.runeRuntimeState != null ? context.runeRuntimeState.name : "null"),
            this);
    }

    private void RefreshRuneUI(RuneGrantContext context)
    {
        bool refreshRequested = false;

        if (context != null && context.runeUIController != null)
        {
            context.runeUIController.RefreshRuneList();
            refreshRequested = true;
        }

        if (context != null && context.runeBagUI != null)
        {
            context.runeBagUI.RefreshAll();
            refreshRequested = true;
        }

        if (refreshRequested)
        {
            Debug.Log("[RuneTestLoadout] UI refresh requested.", this);
        }
        else
        {
            Debug.LogWarning("[RuneTestLoadout] UI refresh skipped because RuneUIController / RuneBagUI was not found.", this);
        }
    }

    private static RuneRuntimeState ResolveRuneRuntimeState(GameObject player, CombatSkillCaster skillCaster)
    {
        if (player != null)
        {
            RuneRuntimeState stateOnPlayer = player.GetComponent<RuneRuntimeState>() ?? player.GetComponentInChildren<RuneRuntimeState>(true);
            if (stateOnPlayer != null)
            {
                return stateOnPlayer;
            }
        }

        if (skillCaster != null)
        {
            RuneRuntimeState stateOnCaster = skillCaster.GetComponent<RuneRuntimeState>();
            if (stateOnCaster != null)
            {
                return stateOnCaster;
            }
        }

        RuneRuntimeState[] runtimeStates = FindObjectsOfType<RuneRuntimeState>(true);
        for (int i = 0; i < runtimeStates.Length; i++)
        {
            RuneRuntimeState candidate = runtimeStates[i];
            if (candidate != null && candidate.isActiveAndEnabled)
            {
                return candidate;
            }
        }

        return runtimeStates != null && runtimeStates.Length > 0 ? runtimeStates[0] : null;
    }

    private static RuneDefinition CloneRuneDefinition(RuneDefinition source)
    {
        if (source == null)
        {
            return null;
        }

        return new RuneDefinition
        {
            runeId = source.runeId,
            runeName = source.runeName,
            runeType = source.runeType,
            rarity = source.rarity,
            description = source.description,
            icon = source.icon,
            displayPrefab = source.displayPrefab,
            tier1Effect = source.tier1Effect,
            tier2Effect = source.tier2Effect,
            tier3Effect = source.tier3Effect,
            tier4Effect = source.tier4Effect,
            tier5Effect = source.tier5Effect,
            setBonusEffect = source.setBonusEffect
        };
    }

    private static void NormalizeDefaultRuneText(RuneDefinition rune, RuneType requestedType)
    {
        if (rune == null)
        {
            return;
        }

        RuneDefinition defaultRune = RuneDefinition.CreateDefaultRune(requestedType != RuneType.None ? requestedType : rune.runeType);
        if (defaultRune == null)
        {
            return;
        }

        rune.runeId = defaultRune.runeId;
        rune.runeName = defaultRune.runeName;
        rune.runeType = defaultRune.runeType;
        rune.description = defaultRune.description;
        rune.tier1Effect = defaultRune.tier1Effect;
        rune.tier2Effect = defaultRune.tier2Effect;
        rune.tier3Effect = defaultRune.tier3Effect;
        rune.tier4Effect = defaultRune.tier4Effect;
        rune.tier5Effect = defaultRune.tier5Effect;
        rune.setBonusEffect = defaultRune.setBonusEffect;
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

            builder.Append(pair.Key).Append(" x").Append(pair.Value);
            first = false;
        }

        return first ? "None" : builder.ToString();
    }
#endif
}
