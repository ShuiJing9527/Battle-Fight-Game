using System.Collections.Generic;
using UnityEngine;

public class RuneLibrary : MonoBehaviour
{
    [Header("Rune Library")]
    public RuneDefinition[] runes;

    [Header("Default Text")]
    [SerializeField] private bool keepCustomText = false;

    private static readonly RuneType[] DefaultRuneTypes =
    {
        RuneType.Life,
        RuneType.Shield,
        RuneType.Mana,
        RuneType.Thorn,
        RuneType.Luck
    };

    private void Reset()
    {
        LoadTableDefaults();
    }

    private void Awake()
    {
        EnsureDefaults();
    }

    private void OnValidate()
    {
        EnsureDefaults();
    }

    [ContextMenu("Load Table Defaults")]
    public void LoadTableDefaults()
    {
        runes = new RuneDefinition[DefaultRuneTypes.Length];
        for (int i = 0; i < DefaultRuneTypes.Length; i++)
        {
            runes[i] = RuneDefinition.CreateDefaultRune(DefaultRuneTypes[i]);
        }
    }

    public RuneDefinition Find(RuneType runeType)
    {
        EnsureDefaults();
        for (int i = 0; i < runes.Length; i++)
        {
            RuneDefinition rune = runes[i];
            if (rune != null && rune.runeType == runeType)
            {
                return rune;
            }
        }

        return null;
    }

    public RuneDefinition GetRandomRune()
    {
        EnsureDefaults();
        if (runes == null || runes.Length == 0)
        {
            return null;
        }

        List<RuneDefinition> validRunes = new List<RuneDefinition>();
        for (int i = 0; i < runes.Length; i++)
        {
            if (runes[i] != null && runes[i].IsConfigured())
            {
                validRunes.Add(runes[i]);
            }
        }

        if (validRunes.Count == 0)
        {
            return null;
        }

        return validRunes[Random.Range(0, validRunes.Count)];
    }

    private void EnsureDefaults()
    {
        if (NeedsDefaultRefresh())
        {
            LoadTableDefaults();
        }

        if (!keepCustomText)
        {
            SyncDefaultText();
        }
    }

    [ContextMenu("Sync English Rune Text")]
    public void SyncDefaultText()
    {
        if (runes == null)
        {
            return;
        }

        for (int i = 0; i < runes.Length; i++)
        {
            RuneDefinition rune = runes[i];
            if (rune == null || rune.runeType == RuneType.None)
            {
                continue;
            }

            RuneDefinition defaultRune = RuneDefinition.CreateDefaultRune(rune.runeType);
            rune.runeId = defaultRune.runeId;
            rune.runeName = defaultRune.runeName;
            rune.description = defaultRune.description;
            rune.tier1Effect = defaultRune.tier1Effect;
            rune.tier2Effect = defaultRune.tier2Effect;
            rune.tier3Effect = defaultRune.tier3Effect;
            rune.tier4Effect = defaultRune.tier4Effect;
            rune.tier5Effect = defaultRune.tier5Effect;
            rune.setBonusEffect = defaultRune.setBonusEffect;
        }
    }

    private bool NeedsDefaultRefresh()
    {
        if (runes == null || runes.Length != DefaultRuneTypes.Length)
        {
            return true;
        }

        HashSet<RuneType> seenTypes = new HashSet<RuneType>();
        for (int i = 0; i < runes.Length; i++)
        {
            RuneDefinition rune = runes[i];
            if (rune == null || !rune.IsConfigured())
            {
                return true;
            }

            if (!seenTypes.Add(rune.runeType))
            {
                return true;
            }
        }

        for (int i = 0; i < DefaultRuneTypes.Length; i++)
        {
            if (!seenTypes.Contains(DefaultRuneTypes[i]))
            {
                return true;
            }
        }

        return false;
    }
}
