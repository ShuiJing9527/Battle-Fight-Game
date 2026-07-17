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
            RuneType.Life => "生命符文",
            RuneType.Shield => "护盾符文",
            RuneType.Mana => "魔力符文",
            RuneType.Thorn => "荆棘符文",
            RuneType.Luck => "幸运符文",
            _ => "符文"
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
        builder.AppendLine("【符文本体效果】");
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

            builder.Append("【套装额外效果】\n").Append(setBonusEffect.Trim());
        }

        return builder.ToString();
    }

    public static string GetLocalizedName(RuneType type)
    {
        RuneDefinition fallbackRune = CreateDefaultRune(type);
        string fallback = fallbackRune != null ? fallbackRune.runeName : "符文";
        return TranslateRuneText(GetLocalizationKey(type, "name"), fallback);
    }

    public static string GetLocalizedFlavor(RuneType type)
    {
        RuneDefinition fallbackRune = CreateDefaultRune(type);
        string fallback = fallbackRune != null ? fallbackRune.description : string.Empty;
        return TranslateRuneText(GetLocalizationKey(type, "flavor"), fallback);
    }

    public static string GetLocalizedFullDescription(RuneType type)
    {
        RuneDefinition fallbackRune = CreateDefaultRune(type);
        string fallback = fallbackRune != null ? fallbackRune.GetFullEffectDescription() : string.Empty;
        return TranslateRuneText(GetLocalizationKey(type, "full_description"), fallback);
    }

    public static string GetLocalizedRarity(RuneRarity rarity)
    {
        string fallback = rarity switch
        {
            RuneRarity.Common => "普通",
            RuneRarity.Rare => "稀有",
            RuneRarity.Epic => "史诗",
            _ => rarity.ToString()
        };

        return TranslateRuneText(rarity.ToString(), fallback);
    }

    private static string TranslateRuneText(string key, string chineseFallback)
    {
        GameLocalization localization = GameLocalization.Instance;
        if (localization == null || localization.CurrentLanguage == GameLanguage.SimplifiedChinese)
        {
            return chineseFallback;
        }

        return localization.TranslateOrFallback(key, chineseFallback);
    }

    private static string GetLocalizationKey(RuneType type, string suffix)
    {
        string typeKey = type switch
        {
            RuneType.Life => "life",
            RuneType.Shield => "shield",
            RuneType.Mana => "mana",
            RuneType.Thorn => "thorn",
            RuneType.Luck => "luck",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(typeKey) ? string.Empty : $"rune.{typeKey}.{suffix}";
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

        builder.Append(tierLabel).Append("件：").Append(text.Trim());
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
                rune.description = "以生命为源泉的符文，能够强化体魄，并将生命力转化为回复、伤害、护盾与成长。";
                rune.tier1Effect = "击杀敌人时，额外掉落1个成长之魂和1个生命之魂。";
                rune.tier2Effect = "每次成功施放技能时，恢复自身5%最大生命值。";
                rune.tier3Effect = "每次成功施放技能后，下一次对敌人造成伤害时，追加相当于自身5%最大生命值的伤害。";
                rune.tier4Effect = "生命符文产生的治疗溢出时，将溢出治疗量转化为护盾，最多至自身100%最大生命值。";
                rune.tier5Effect = "自身最大生命值提高50%。";
                rune.setBonusEffect = "2件套·生命亲和：自身受到的治疗效果提高15%。\n4件套·生命共鸣：当前生命值高于或等于50%时，造成的伤害提高20%；低于50%时，受到怪物造成的伤害降低25%。生命值从50%以下恢复至50%以上时，获得8秒生命共鸣，同时获得上述增伤和减伤，刷新但不叠加。\n5件套·生命统御：每拥有100点最大生命值，物理攻击、物理防御、特殊攻击、特殊防御和速度各提高1点，不提高最大生命值与幸运。";
                break;

            case RuneType.Shield:
                rune.runeId = 102;
                rune.runeName = "护盾符文";
                rune.description = "守护意志凝结而成的符文，能将护盾转化为更强的防御与攻击力量。";
                rune.tier1Effect = "连续3秒未受到怪物伤害后，自动恢复护盾，直到达到50%最大生命值。";
                rune.tier2Effect = "自身拥有护盾时，造成的伤害提高30%。";
                rune.tier3Effect = "击杀敌人时，额外掉落1个功能之魂和1个护盾之魂。";
                rune.tier4Effect = "自身获得的护盾量提高100%，受护盾效率影响。";
                rune.tier5Effect = "每次成功施放技能后，下一次对敌人造成伤害时，追加相当于自身当前护盾值15%的伤害，受护盾效率影响。";
                rune.setBonusEffect = "2件套·坚固屏障：自身拥有护盾时，护盾受到的伤害降低15%，只影响护盾承受的部分。\n4件套·壁垒重构：护盾被怪物击破时，获得3秒壁垒重构：受到怪物伤害降低40%，免疫击退和硬直。结束时获得30%最大生命值护盾。15秒冷却。\n5件套·不落要塞：护盾上限提高至300%最大生命值。击杀精英或Boss时，护盾效率永久+10%，最高300%。";
                break;

            case RuneType.Mana:
                rune.runeId = 103;
                rune.runeName = "魔力符文";
                rune.description = "由纯粹魔力凝结而成的符文，能扩张法力容量，并将溢出的魔力转化为爆发。";
                rune.tier1Effect = "最大魔力值提高200点。";
                rune.tier2Effect = "魔力恢复速度提高150%。";
                rune.tier3Effect = "击杀敌人时，额外掉落1个能量之魂和1个魔力之魂。获得魔力时，超过最大魔力值的部分转化为魔力溢出，最多至200%最大魔力值。";
                rune.tier4Effect = "施放技能时，额外消耗最多20%最大魔力值的魔力，优先使用当前魔力，不足时再使用魔力溢出，强化该技能的符文奖励。";
                rune.tier5Effect = "每拥有100点最大魔力值，物理攻击、物理防御、特殊攻击、特殊防御和速度各+1。";
                rune.setBonusEffect = "2件套·魔力回流：成功施放技能后，返还该技能基础魔力消耗的15%。\n4件套·奥术共鸣：通过魔力符文额外耗蓝累计达到20%最大魔力值时，获得8秒奥术共鸣：造成伤害+25%，技能冷却恢复速度+25%，魔力符文额外耗蓝产生的技能强化效果+75%。触发后累计值清零。\n5件套·奥术超载：魔力之魂恢复量提高至400%，魔力溢出上限提高至300%最大魔力值。击杀精英或Boss时，魔力转化效率永久+10%，最高300%。";
                break;

            case RuneType.Thorn:
                rune.runeId = 104;
                rune.runeName = "荆棘符文";
                rune.description = "由痛苦与反击意志凝结而成的符文，能将承受的攻击转化为反击力量。";
                rune.tier1Effect = "受到怪物造成的伤害降低25%。";
                rune.tier2Effect = "受到怪物攻击时，对攻击者造成荆棘伤害。基础荆棘值为（自身10%最大生命值+主要属性+幸运）x30%。";
                rune.tier3Effect = "荆棘伤害提高100%。";
                rune.tier4Effect = "每次成功施放技能后，下一次对敌人造成伤害时，追加150%当前荆棘值的伤害。";
                rune.tier5Effect = "受到怪物攻击时，自动释放荆棘反击。";
                rune.setBonusEffect = "2件套·倒刺汲取：荆棘伤害成功命中后恢复2%最大生命值，每1秒最多1次。\n4件套·荆棘反噬：受到怪物伤害时，本次伤害额外降低30%，并对攻击者造成200%当前荆棘值的荆棘伤害。5秒冷却。\n5件套·万刺反击：荆棘伤害额外提高100%，荆棘反击冷却缩短至2秒。击杀精英或Boss时，荆棘效率永久+10%，最高300%。";
                break;

            case RuneType.Luck:
                rune.runeId = 105;
                rune.runeName = "幸运符文";
                rune.description = "由命运气息凝结而成的符文，能将战斗中的偶然收益转化为长期成长。";
                rune.tier1Effect = "幸运+5。";
                rune.tier2Effect = "成长之魂生成时，有30%概率使其点数+1，最高5点。";
                rune.tier3Effect = "击杀敌人时，有25%概率额外掉落1个成长之魂。";
                rune.tier4Effect = "拾取任意灵魂时，有20%概率额外复制1个同类灵魂，复制灵魂固定为2点。";
                rune.tier5Effect = "击杀精英敌人或Boss时，额外掉落5个成长之魂。";
                rune.setBonusEffect = "2件套·命运一掷：每次成功施放技能时，有20%概率进行命运抽奖，从当前装备的非幸运符文种类中抽取1种并触发对应拟态效果。若没有其他符文，则恢复5%最大生命值和5%最大魔力值。\n4件套·双重眷顾：命运抽奖基础概率提高至35%。成功时抽取两个不同结果；若只有1种结果，第2次效果降为50%。\n5件套·命运大奖：命运抽奖成功时，有15%概率改为触发当前抽奖池中全部结果。击杀精英或Boss时，幸运效率永久+10%，最高300%。幸运效率影响抽奖和大奖概率，最终概率不超过100%。";
                break;
        }

        return rune;
    }
}
