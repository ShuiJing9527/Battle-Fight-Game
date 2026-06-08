using UnityEngine;

public class RuneLibrary : MonoBehaviour
{
    [Header("符文库")]
    public RuneDefinition[] runes;

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

    private void EnsureDefaults()
    {
        if (runes == null || runes.Length == 0)
        {
            LoadTableDefaults();
        }
    }

    [ContextMenu("Load Table Defaults")]
    public void LoadTableDefaults()
    {
        runes = new[]
        {
            RuneDefinition.CreateTableRune(RuneMechanic.Combo),
            RuneDefinition.CreateTableRune(RuneMechanic.DoubleStar),
            RuneDefinition.CreateTableRune(RuneMechanic.Afterimage),
            RuneDefinition.CreateTableRune(RuneMechanic.Split),
            RuneDefinition.CreateTableRune(RuneMechanic.Echo),
            RuneDefinition.CreateTableRune(RuneMechanic.BloodExplosion),
            RuneDefinition.CreateTableRune(RuneMechanic.DrainMark),
            RuneDefinition.CreateTableRune(RuneMechanic.Regeneration),
            RuneDefinition.CreateTableRune(RuneMechanic.Exchange),
            RuneDefinition.CreateTableRune(RuneMechanic.SoulLink)
        };
    }

    public RuneDefinition Find(RuneMechanic mechanic)
    {
        if (runes == null)
        {
            return null;
        }

        foreach (RuneDefinition rune in runes)
        {
            if (rune != null && rune.mechanic == mechanic)
            {
                return rune;
            }
        }

        return null;
    }
}
