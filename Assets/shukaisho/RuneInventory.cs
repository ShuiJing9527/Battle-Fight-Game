using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using System;
#endif

public class RuneInventory : MonoBehaviour
{
    public List<RuneDefinition> runes = new List<RuneDefinition>();
    [SerializeField] private bool debugRuneInventoryAddDiag = true;

    public int Count => runes.Count;

    public void AddRune(RuneDefinition rune)
    {
        AddRune(rune, "Unknown");
    }

    public void AddRune(RuneDefinition rune, string source)
    {
        if (rune == null)
        {
            return;
        }

        int beforeCount = runes.Count;
        runes.Add(rune);
        int afterCount = runes.Count;

        if (debugRuneInventoryAddDiag)
        {
#if UNITY_EDITOR
            string stackTrace = Environment.StackTrace;
#else
            string stackTrace = string.Empty;
#endif
            Debug.Log(
                $"[RuneInventory.AddRune] frame={Time.frameCount} rune={(string.IsNullOrEmpty(rune.runeName) ? "Rune" : rune.runeName)} runeId={rune.runeId} before={beforeCount} after={afterCount} added=1 source={source}{(string.IsNullOrEmpty(stackTrace) ? string.Empty : "\n" + stackTrace)}",
                this);
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
