using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class RuneDefinition
{
    [Header("Identity")]
    [FormerlySerializedAs("id")]
    public int runeId;
    public string runeName;
    public RuneType runeType;
    public RuneRarity rarity = RuneRarity.Common;

    [Header("Display")]
    [TextArea(2, 5)]
    public string description;
    public Sprite icon;
    public GameObject displayPrefab;

    [Header("Tier Effects")]
    [TextArea(2, 4)]
    public string tier1Effect;
    [TextArea(2, 4)]
    public string tier2Effect;
    [TextArea(2, 4)]
    public string tier3Effect;
    [TextArea(2, 4)]
    public string tier4Effect;
    [TextArea(2, 4)]
    public string tier5Effect;
    [TextArea(2, 4)]
    public string setBonusEffect;

    public bool IsConfigured()
    {
        return runeType != RuneType.None && !string.IsNullOrWhiteSpace(runeName);
    }

    public string GetTypeDisplayName()
    {
        return runeType switch
        {
            RuneType.Life => "Life Rune",
            RuneType.Shield => "Shield Rune",
            RuneType.Mana => "Mana Rune",
            RuneType.Thorn => "Thorn Rune",
            RuneType.Luck => "Luck Rune",
            _ => "Rune"
        };
    }

    public string GetTierEffectText(int tier)
    {
        return tier switch
        {
            1 => tier1Effect,
            2 => tier2Effect,
            3 => tier3Effect,
            4 => tier4Effect,
            5 => tier5Effect,
            _ => string.Empty
        };
    }

    public string GetFullEffectDescription()
    {
        StringBuilder builder = new StringBuilder();
        AppendEffectLine(builder, "1", tier1Effect);
        AppendEffectLine(builder, "2", tier2Effect);
        AppendEffectLine(builder, "3", tier3Effect);
        AppendEffectLine(builder, "4", tier4Effect);
        AppendEffectLine(builder, "5", tier5Effect);

        if (!string.IsNullOrWhiteSpace(setBonusEffect))
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append("Set Bonus: ").Append(setBonusEffect.Trim());
        }

        return builder.ToString();
    }

    private static void AppendEffectLine(StringBuilder builder, string tierLabel, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        builder.Append(tierLabel).Append(" Piece: ").Append(text.Trim());
    }

    public static RuneDefinition CreateDefaultRune(RuneType type)
    {
        RuneDefinition rune = new RuneDefinition
        {
            runeType = type,
            rarity = RuneRarity.Common
        };

        switch (type)
        {
            case RuneType.Life:
                rune.runeId = 101;
                rune.runeName = "生命符文";
                rune.description = "以生命为源泉的符文，强化体魄，并将生命力转化为回复、伤害与成长。";
                rune.tier1Effect = "最大生命值提高 10%。";
                rune.tier2Effect = "使用该技能时，回复自身最大生命值 1% 的 HP。";
                rune.tier3Effect = "若该回复产生溢出治疗，则将溢出部分转化为永久护盾，上限为自身最大生命值的 50%。";
                rune.tier4Effect = "该技能首次命中敌人时，额外造成自身最大生命值 1% 的伤害，每次释放最多触发 1 次。";
                rune.tier5Effect = "击杀怪物时，额外获得 1 个生命属性魂和 1 个回复生命魂。";
                rune.setBonusEffect = "1 件生命提高改为 50%；2 件回复改为 5%；3 件永久护盾上限改为最大生命值 100%；4 件首次命中额外伤害改为最大生命值 5%；5 件额外根据最大生命值为 HP 以外属性提供加成。";
                break;

            case RuneType.Shield:
                rune.runeId = 102;
                rune.runeName = "护盾符文";
                rune.description = "守护意志凝结而成的符文，积蓄护盾并将护盾转化为更强的防御与攻击。";
                rune.tier1Effect = "没有受到怪物伤害 3 秒后，获得自身最大生命值 15% 的护盾。";
                rune.tier2Effect = "拥有护盾时，造成的伤害提高 25%。";
                rune.tier3Effect = "获得的护盾值提高 100%。";
                rune.tier4Effect = "该技能首次命中敌人时，额外造成当前护盾值 10% 的伤害，每次释放最多触发 1 次。";
                rune.tier5Effect = "击杀怪物时，额外获得 1 个护盾魂。";
                rune.setBonusEffect = "1 件护盾值改为最大生命值 50%；2 件伤害提高改为 50%；3 件护盾获得提高改为 200%；4 件首次命中额外伤害改为当前护盾值 25%；5 件每次击杀 Boss 使护盾效能永久提高 10%。";
                break;

            case RuneType.Mana:
                rune.runeId = 103;
                rune.runeName = "魔力符文";
                rune.description = "由纯粹魔力凝结而成的符文，扩张法力容量，强化魔力魂恢复，并将溢出的魔力转化为爆发。";
                rune.tier1Effect = "最大法力值增加 150。";
                rune.tier2Effect = "法力恢复速度提高 150%。";
                rune.tier3Effect = "击杀怪物时，额外掉落 1 个魔力魂；魔力魂溢出恢复会转化为魔力充盈，上限为最大法力值的 100%。";
                rune.tier4Effect = "该技能首次命中敌人时，额外消耗最多 100 点法力或魔力充盈，造成额外消耗值 ×3 的伤害。";
                rune.tier5Effect = "最大法力转化为全属性加成 20%。";
                rune.setBonusEffect = "1 件最大法力改为 400；2 件法力恢复改为 300%；3 件魔力魂恢复提高到 300%，魔力充盈上限改为最大法力值 200%；4 件额外消耗上限改为 200 且倍率改为 ×4；5 件每次击杀 Boss 使法力转换效能永久提高 10%。";
                break;

            case RuneType.Thorn:
                rune.runeId = 104;
                rune.runeName = "荆棘符文";
                rune.description = "由痛苦与反击意志凝结而成的符文，降低承伤并把承受的攻击转化为反击力量。";
                rune.tier1Effect = "受到的伤害降低 15%。";
                rune.tier2Effect = "受到怪物攻击时，对攻击者造成自身全属性总和 15% 的荆棘伤害。";
                rune.tier3Effect = "该技能首次造成伤害时，额外附加荆棘伤害 150% 的伤害，每次释放最多触发 1 次。";
                rune.tier4Effect = "受到怪物攻击时，自动触发一次镶嵌技能的荆棘反击，无视冷却和消耗，但有 4 秒触发间隔。";
                rune.tier5Effect = "荆棘伤害提高 100%。";
                rune.setBonusEffect = "1 件减伤改为 35%；2 件荆棘伤害改为全属性总和 40%；3 件附加伤害改为荆棘伤害 300%；4 件触发间隔缩短到 2 秒；5 件每次击杀 Boss 使荆棘效能永久提高 15%。";
                break;

            case RuneType.Luck:
                rune.runeId = 105;
                rune.runeName = "幸运符文";
                rune.description = "由命运气息凝结而成的符文，提高魂的获取效率，并把偶然收益转化为长期成长。";
                rune.tier1Effect = "幸运值提高 1。";
                rune.tier2Effect = "怪物掉落属性魂时，有 20% 概率使该属性魂点数提高 1，最高不超过 5。";
                rune.tier3Effect = "击杀怪物时，有 15% 概率额外掉落 1 个随机属性魂。";
                rune.tier4Effect = "拾取任意魂时，有 10% 概率额外复制 1 个同类型魂，复制魂默认 1 点。";
                rune.tier5Effect = "击杀 Boss 时，额外掉落 2 个随机属性魂。";
                rune.setBonusEffect = "1 件幸运提高改为 3；2 件属性魂提点概率改为 35%；3 件额外属性魂概率改为 25%；4 件复制概率改为 25% 且复制魂点数改为 3；5 件每次击杀 Boss 使幸运效能永久提高 5%。";
                break;
        }

        return rune;
    }
}
