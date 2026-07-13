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
        AppendLine(builder, GetLocalizedBody(entry));
        return builder.ToString().Trim();
    }

    public static string BuildDetailBodyText(SkillUIDefinitionEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        return GetLocalizedBody(entry);
    }

    public static string GetLocalizedTitle(SkillUIDefinitionEntry entry)
    {
        if (entry == null)
        {
            return string.Empty;
        }

        int language = GameLocalization.Instance != null ? (int)GameLocalization.Instance.CurrentLanguage : 0;
        string id = BuildKey(entry.playerIndex, entry.key);
        if (language == 1)
        {
            switch (id)
            {
                case "1:Q": return "Q - 快速剪切";
                case "1:W": return "W - 丝流守势";
                case "1:E": return "E - 断裂冲刺";
                case "1:R": return "R - 飞针射击";
                case "2:Q": return "Q - 神光剑";
                case "2:W": return "W - 圣轮偏转";
                case "2:E": return "E - 天界位移";
                case "2:R": return "R - 神圣星雨";
            }
        }
        else if (language == 2)
        {
            switch (id)
            {
                case "1:Q": return "Q - クイックシアー";
                case "1:W": return "W - スレッドフロー";
                case "1:E": return "E - ブロークンダッシュ";
                case "1:R": return "R - ニードルショット";
                case "2:Q": return "Q - 神光剣";
                case "2:W": return "W - 聖輪防御";
                case "2:E": return "E - 天翔転移";
                case "2:R": return "R - 神聖星雨";
            }
        }

        return string.IsNullOrWhiteSpace(entry.displayName) ? entry.key : entry.displayName;
    }

    private static string GetLocalizedBody(SkillUIDefinitionEntry entry)
    {
        int language = GameLocalization.Instance != null ? (int)GameLocalization.Instance.CurrentLanguage : 0;
        string id = BuildKey(entry.playerIndex, entry.key);
        if (language == 1)
        {
            switch (id)
            {
                case "1:Q": return "冷却：3秒\n消耗：中等魔力\n范围：近战\n伤害：快速物理爆发\n快速斩击近距离敌人，用于施压并触发符文联动。";
                case "1:W": return "冷却：5秒\n消耗：中等魔力\n范围：自身／防御\n伤害：防御辅助\n进入防御状态，减少受到的伤害，并帮助玩家保持安全距离。";
                case "1:E": return "冷却：8秒\n消耗：中等魔力\n范围：位移\n伤害：较低接触伤害\n向前快速冲刺，用于调整位置，并可在冲刺途中擦伤敌人。";
                case "1:R": return "冷却：12秒\n消耗：高魔力\n范围：远程\n伤害：特殊投射物爆发\n发射强力远程终结攻击，从安全距离压制目标。";
                case "2:Q": return "冷却：0.8秒\n消耗：10魔力\n范围：指定落点区域\n伤害：多段混合剑雨\n在选定区域召唤坠落星剑，每把剑命中时分别计算物理与特殊伤害。";
                case "2:W": return "冷却：6秒\n消耗：40魔力\n范围：自身／环绕\n伤害：较低，以防御为主\n生成防御剑轮，提供护盾和伤害减免，而非作为主要输出技能。";
                case "2:E": return "冷却：8秒\n消耗：20魔力\n范围：冲刺\n伤害：位移辅助\n进行短距离天界冲刺，用于重新定位、拉开距离和躲避危险。";
                case "2:R": return "冷却：15秒\n消耗：60魔力\n范围：大范围漩涡区域\n伤害：持续多段特殊伤害\n生成剑刃漩涡与星雨领域，反复伤害控制区域内的敌人。";
            }
        }
        else if (language == 2)
        {
            switch (id)
            {
                case "1:Q": return "クールダウン：3秒\n消費：中MP\n範囲：近接\nダメージ：高速物理バースト\n近くの敵を素早く斬り、圧力をかけながらルーン連携を発動する。";
                case "1:W": return "クールダウン：5秒\n消費：中MP\n範囲：自身／防御\nダメージ：防御支援\n防御態勢に入り、被ダメージを軽減して安全な間合いを保つ。";
                case "1:E": return "クールダウン：8秒\n消費：中MP\n範囲：移動\nダメージ：低い接触ダメージ\n前方へ素早く突進し、位置を調整しながら接触した敵にもダメージを与える。";
                case "1:R": return "クールダウン：12秒\n消費：高MP\n範囲：遠距離\nダメージ：特殊投射バースト\n強力な遠距離フィニッシュ攻撃を放ち、安全な距離から標的を圧迫する。";
                case "2:Q": return "クールダウン：0.8秒\n消費：10MP\n範囲：指定落下区域\nダメージ：多段複合剣雨\n指定範囲に星剣を降らせ、各剣の命中時に物理・特殊ダメージを個別計算する。";
                case "2:W": return "クールダウン：6秒\n消費：40MP\n範囲：自身／周囲\nダメージ：低、守備重視\n防御剣輪を生成し、主力攻撃の代わりにシールドとダメージ軽減を得る。";
                case "2:E": return "クールダウン：8秒\n消費：20MP\n範囲：ダッシュ\nダメージ：移動支援\n短い天翔ダッシュで位置を変え、間合いを調整して危険を回避する。";
                case "2:R": return "クールダウン：15秒\n消費：60MP\n範囲：広域渦ゾーン\nダメージ：反復特殊継続ダメージ\n剣の渦と星雨の領域を生成し、範囲内の敵へ継続的にダメージを与える。";
            }
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        AppendLine(builder, entry.cooldownText);
        AppendLine(builder, entry.costText);
        AppendLine(builder, entry.rangeText);
        AppendLine(builder, entry.damageText);
        AppendLine(builder, entry.descriptionText);
        return builder.ToString().Trim();
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
