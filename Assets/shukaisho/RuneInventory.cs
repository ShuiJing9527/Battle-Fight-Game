using System.Collections.Generic;
using UnityEngine;

public class RuneInventory : MonoBehaviour
{
    public List<RuneDefinition> runes = new List<RuneDefinition>();

    public int Count => runes.Count;

    public void AddRune(RuneDefinition rune)
    {
        if (rune != null)
        {
            runes.Add(rune);
        }
    }

    public RuneDefinition GetRune(int index)
    {
        return index >= 0 && index < runes.Count ? runes[index] : null;
    }

    public bool RemoveRune(RuneDefinition rune)
    {
        return rune != null && runes.Remove(rune);
    }
}
