using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SkillUIDefinitionEntry
{
    public int playerIndex = 1;
    public string key = "Q";
    public string displayName = string.Empty;
    public string cooldownText = string.Empty;
    public string costText = string.Empty;
    public string rangeText = string.Empty;
    public string damageText = string.Empty;
    public string descriptionText = string.Empty;
}

[Serializable]
public class SkillUIDefinitionCollection
{
    public SkillUIDefinitionEntry[] entries = Array.Empty<SkillUIDefinitionEntry>();
}

public static class SkillUIDefinitionDatabase
{
    private const string ResourcePath = "UI/SkillUIDefinitions";
    private static Dictionary<string, SkillUIDefinitionEntry> cache;

    public static SkillUIDefinitionEntry Get(int playerIndex, string key)
    {
        EnsureLoaded();
        if (cache == null)
        {
            return null;
        }

        cache.TryGetValue(BuildKey(playerIndex, key), out SkillUIDefinitionEntry entry);
        return entry;
    }

    public static string BuildTooltipText(SkillUIDefinitionEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        AppendLine(builder, GetLocalizedTitle(entry));
        AppendLine(builder, BuildDetailBodyText(entry));
        return builder.ToString().Trim();
    }

    public static string BuildDetailBodyText(SkillUIDefinitionEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        AppendLine(builder, GetLocalizedField(entry, "cooldown", entry.cooldownText));
        AppendLine(builder, GetLocalizedField(entry, "cost", entry.costText));
        AppendLine(builder, GetLocalizedField(entry, "range", entry.rangeText));
        AppendLine(builder, GetLocalizedField(entry, "damage", entry.damageText));
        AppendLine(builder, GetLocalizedField(entry, "description", entry.descriptionText));
        return builder.ToString().Trim();
    }

    public static string GetLocalizedTitle(SkillUIDefinitionEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        return GetLocalizedField(entry, "title", string.IsNullOrWhiteSpace(entry.displayName) ? entry.key : entry.displayName);
    }

    private static string GetLocalizedField(SkillUIDefinitionEntry entry, string field, string fallback)
    {
        if (entry == null)
        {
            return fallback ?? string.Empty;
        }

        string key = BuildLocalizationKey(entry, field);
        GameLocalization localization = GameLocalization.Instance;
        if (localization == null || localization.CurrentLanguage == GameLanguage.SimplifiedChinese)
        {
            return fallback ?? string.Empty;
        }

        return localization.TranslateOrFallback(key, fallback ?? string.Empty);
    }

    private static string BuildLocalizationKey(SkillUIDefinitionEntry entry, string field)
    {
        return $"skill.{Mathf.Max(1, entry.playerIndex)}.{(entry.key ?? string.Empty).Trim().ToLowerInvariant()}.{field}";
    }

    private static void EnsureLoaded()
    {
        if (cache != null)
        {
            return;
        }

        cache = new Dictionary<string, SkillUIDefinitionEntry>();
        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null || string.IsNullOrWhiteSpace(asset.text))
        {
            return;
        }

        SkillUIDefinitionCollection collection = JsonUtility.FromJson<SkillUIDefinitionCollection>(asset.text);
        if (collection == null || collection.entries == null)
        {
            return;
        }

        for (int i = 0; i < collection.entries.Length; i++)
        {
            SkillUIDefinitionEntry entry = collection.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            cache[BuildKey(entry.playerIndex, entry.key)] = entry;
        }
    }

    private static string BuildKey(int playerIndex, string key)
    {
        return $"{Mathf.Max(1, playerIndex)}:{(key ?? string.Empty).Trim().ToUpperInvariant()}";
    }

    private static void AppendLine(System.Text.StringBuilder builder, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(value.Trim());
    }
}
