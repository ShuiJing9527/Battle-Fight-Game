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
        RuneDisplayText displayText = GetRuneDisplayText(type);
        string fallback = displayText != null ? displayText.Name.GetCurrentLanguageText() : "符文";
        return TranslateRuneText(GetLocalizationKey(type, "name"), fallback);
    }

    public static string GetLocalizedFlavor(RuneType type)
    {
        RuneDisplayText displayText = GetRuneDisplayText(type);
        string fallback = displayText != null ? displayText.Flavor.GetCurrentLanguageText() : string.Empty;
        return TranslateRuneText(GetLocalizationKey(type, "flavor"), fallback);
    }

    public static string GetLocalizedFullDescription(RuneType type)
    {
        RuneDefinition fallbackRune = CreateDefaultRune(type);
        string fallback = fallbackRune != null ? fallbackRune.GetFullEffectDescription() : string.Empty;
        return TranslateRuneText(GetLocalizationKey(type, "full_description"), fallback);
    }

    public static string GetLocalizedProgressiveDescription(RuneType type, int equippedCount)
    {
        RuneDisplayText displayText = GetRuneDisplayText(type);
        if (displayText == null)
        {
            return GetLocalizedFullDescription(type);
        }

        int displayCount = Mathf.Clamp(equippedCount, 1, 5);
        StringBuilder builder = new StringBuilder();
        AppendBlock(builder, TranslateRuneText(GetLocalizationKey(type, "name"), displayText.Name));
        AppendBlock(builder, TranslateRuneText(GetLocalizationKey(type, "flavor"), displayText.Flavor));

        for (int i = 0; i < displayCount && i < displayText.BaseEffects.Length; i++)
        {
            string effect = TranslateRuneText(GetLocalizationKey(type, $"base.{i + 1}"), displayText.BaseEffects[i]);
            if (!string.IsNullOrWhiteSpace(effect))
            {
                builder.Append("●").Append(effect.Trim()).Append('\n');
            }
        }

        AppendUnlockedSetEffect(builder, type, equippedCount, displayText.Set2);
        AppendUnlockedSetEffect(builder, type, equippedCount, displayText.Set4);
        AppendUnlockedSetEffect(builder, type, equippedCount, displayText.Set5);

        return builder.ToString().Trim();
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

    private static string TranslateRuneText(string key, RuneLocalizedText fallback)
    {
        string localizedFallback = fallback.GetCurrentLanguageText();
        GameLocalization localization = GameLocalization.Instance;
        if (localization == null)
        {
            return localizedFallback;
        }

        return localization.TranslateOrFallback(key, localizedFallback);
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

    private static void AppendBlock(StringBuilder builder, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append("\n\n");
        }

        builder.Append(text.Trim()).Append('\n');
    }

    private static void AppendUnlockedSetEffect(StringBuilder builder, RuneType type, int equippedCount, RuneSetDisplayText setText)
    {
        if (setText == null || equippedCount < setText.RequiredCount)
        {
            return;
        }

        string setName = TranslateRuneText(GetLocalizationKey(type, $"set.{setText.RequiredCount}.name"), setText.Name);
        string description = TranslateRuneText(GetLocalizationKey(type, $"set.{setText.RequiredCount}.description"), setText.Description);
        if (string.IsNullOrWhiteSpace(setName) && string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.Append('\n');
        }

        if (string.IsNullOrWhiteSpace(setName))
        {
            builder.Append(description.Trim()).Append('\n');
            return;
        }

        builder.Append(setName.Trim());
        if (!string.IsNullOrWhiteSpace(description))
        {
            builder.Append("：").Append(description.Trim());
        }

        builder.Append('\n');
    }

    private static RuneDisplayText GetRuneDisplayText(RuneType type)
    {
        switch (type)
        {
            case RuneType.Life:
                return new RuneDisplayText(
                    L("Life Rune", "生命符文", "生命ルーン"),
                    L("A rune born from life force. It strengthens the body and turns vitality into healing, damage, and growth.", "以生命为源泉的符文，能够强化体魄，并将生命力转化为回复、伤害与成长。", "生命力を源とするルーン。体を強化し、生命力を回復・ダメージ・成長へ変える。"),
                    new[]
                    {
                        L("Kills drop 1 Growth Soul and 1 Life Soul.", "击杀敌人时，额外掉落1个成长之魂和1个生命之魂。", "敵を倒すと成長ソウルと生命ソウルを1個ずつ追加ドロップ。"),
                        L("Each successful skill cast restores 5% max HP.", "每次施放技能时，恢复自身5%最大生命值。", "スキル発動時、最大HPの5%を回復。"),
                        L("After each skill cast, the next enemy hit deals bonus damage equal to 5% max HP. Each skill cast can trigger this once.", "每次施放技能后，下一次对敌人造成伤害时，追加相当于自身5%最大生命值的伤害。每次技能施放只能触发1次。", "スキル後、次の対敵ヒットに最大HP5%分の追加ダメージ。スキル1回につき1回のみ。"),
                        L("Life Rune overheal becomes shield, capped at 100% max HP.", "生命符文产生的治疗溢出时，将溢出的治疗量转化为护盾，最多累积至自身100%最大生命值。", "生命ルーンの過剰回復をシールドに変換。上限は最大HPの100%。"),
                        L("Max HP +50%.", "自身最大生命值提高50%。", "最大HP+50%。")
                    },
                    S(2, "Life Affinity", "生命亲和", "生命親和", "Healing received +15%.", "自身受到的治疗效果提高15%。", "受ける回復量+15%。"),
                    S(4, "Life Resonance", "生命共鸣", "生命共鳴", "At 50% HP or higher, damage dealt +20%. Below 50% HP, monster damage taken -25%. Healing from below 50% to 50% or higher grants both effects for 8s; refreshing does not stack.", "生命值高于或等于50%时，造成的伤害提高20%；生命值低于50%时，受到怪物造成的伤害降低25%。每次生命值从50%以下恢复至50%以上时，获得持续8秒的生命共鸣，生命共鸣期间同时获得上述增伤与减伤效果，效果不会叠加。", "HP50%以上で与ダメージ+20%、50%未満でモンスターからのダメージ-25%。50%未満から50%以上へ回復すると8秒間両方を得る。重複しない。"),
                    S(5, "Life Dominion", "生命统御", "生命統御", "Every 100 max HP grants +1 Physical Attack, Physical Defense, Special Attack, Special Defense, and Speed. Does not increase Max HP or Luck.", "根据自身最大生命值，提高物理攻击、物理防御、特殊攻击、特殊防御和速度。每拥有100点最大生命值，上述属性各提高1点，不提高最大生命值与幸运。", "最大HP100ごとに物理攻撃、物理防御、特殊攻撃、特殊防御、速度+1。最大HPと運は増えない。"));

            case RuneType.Shield:
                return new RuneDisplayText(
                    L("Shield Rune", "护盾符文", "シールドルーン"),
                    L("A rune of protection that turns shield power into defense and offense.", "守护意志凝结而成的符文，能将护盾转化为更强的防御与攻击力量。", "守りの意志が結晶化したルーン。シールドを防御と攻撃の力に変える。"),
                    new[]
                    {
                        L("After 3s without monster damage, refill shield up to 50% max HP.", "连续3秒未受到怪物伤害后，自动恢复护盾，直到达到50%最大生命值。", "3秒間モンスターダメージを受けないと、最大HP50%までシールド回復。"),
                        L("While shielded, damage dealt +30%.", "自身拥有护盾时，造成的伤害提高30%。", "シールド中、与ダメージ+30%。"),
                        L("Kills drop 1 Function Soul and 1 Shield Soul.", "击杀敌人时，额外掉落1个功能之魂和1个护盾之魂。", "敵を倒すと機能ソウルとシールドソウルを1個ずつ追加ドロップ。"),
                        L("Shield gained +100%, affected by Shield Efficiency.", "自身获得的护盾量提高100%，受护盾效率影响。", "獲得シールド+100%。シールド効率の影響を受ける。"),
                        L("After casting, your next enemy hit deals bonus damage equal to 15% current shield, affected by Shield Efficiency.", "每次施放技能后，下一次对敌人造成伤害时，追加相当于自身当前护盾值15%的伤害，受护盾效率影响。", "スキル後の次の対敵ヒットに現在シールド15%分の追加ダメージ。シールド効率の影響を受ける。")
                    },
                    S(2, "Solid Barrier", "坚固屏障", "堅固な結界", "While shielded, shield damage taken -15%. This only affects the shield portion.", "自身拥有护盾时，护盾受到的伤害降低15%，只影响护盾承受的部分。", "シールドが受けるダメージ-15%。シールド部分のみ。"),
                    S(4, "Barrier Reconstruction", "壁垒重构", "壁の再構築", "When shield breaks, gain 3s of 40% monster damage reduction and immunity to knockback/hit-stun. When it ends, gain shield equal to 30% max HP. 15s cooldown.", "护盾被怪物击破时，获得3秒壁垒重构：受到怪物伤害降低40%，免疫击退和硬直。结束时获得30%最大生命值护盾。15秒冷却。", "シールド破壊時、3秒間モンスターダメージ-40%、ノックバックとひるみ無効。終了時に最大HP30%のシールド。15秒CD。"),
                    S(5, "Unfallen Fortress", "不落要塞", "不落の要塞", "Shield cap becomes 300% max HP. Elite/Boss kills grant permanent Shield Efficiency +10%, up to 300%.", "护盾上限提高至300%最大生命值。击杀精英或Boss时，护盾效率永久+10%，最高300%。", "シールド上限が最大HP300%になる。エリート/Boss撃破でシールド効率+10%（最大300%）。"));

            case RuneType.Mana:
                return new RuneDisplayText(
                    L("Mana Rune", "魔力符文", "マナルーン"),
                    L("A pure mana rune that expands mana and converts overflow into power.", "由纯粹魔力凝结而成的符文，能扩张法力容量，并将溢出的魔力转化为爆发。", "純粋なマナから生まれたルーン。マナと溢れた力を爆発力に変える。"),
                    new[]
                    {
                        L("Max Mana +200.", "最大魔力值提高200点。", "最大MP+200。"),
                        L("Mana regeneration +150%.", "魔力恢复速度提高150%。", "MP回復速度+150%。"),
                        L("Kills drop 1 Energy Soul and 1 Mana Soul. Mana recovery overflow becomes Mana Overflow, capped at 200% max mana.", "击杀敌人时，额外掉落1个能量之魂和1个魔力之魂。获得魔力时，超过最大魔力值的部分转化为Mana Overflow，最多至200%最大魔力值。", "敵を倒すとエネルギーソウルとマナソウルを1個ずつ追加ドロップ。過剰MP回復はMana Overflowになり、最大MP200%まで蓄積。"),
                        L("Casting a skill consumes up to 20% max mana as extra mana, current mana first then Mana Overflow, to strengthen that skill's configured Mana Rune bonus.", "施放技能时，额外消耗最多20%最大魔力值的魔力，优先使用当前魔力，不足时再使用Mana Overflow，强化该技能的符文奖励。", "スキル時、最大MP20%まで追加消費し、マナルーン強化を増幅。現在MPを先に使い、不足分をMana Overflowから使う。"),
                        L("Every 100 max mana grants +1 Physical Attack, Physical Defense, Special Attack, Special Defense, and Speed.", "每拥有100点最大魔力值，物理攻击、物理防御、特殊攻击、特殊防御和速度各+1。", "最大MP100ごとに物理攻撃、物理防御、特殊攻撃、特殊防御、速度+1。")
                    },
                    S(2, "Mana Flow", "魔力回流", "マナ還流", "After a successful cast, refund 15% of the skill's base mana cost.", "成功施放技能后，返还该技能基础魔力消耗的15%。", "スキル成功後、基本MP消費の15%を返還。"),
                    S(4, "Arcane Resonance", "奥术共鸣", "奥術共鳴", "When Mana Rune extra spending totals 20% max mana, gain 8s of +25% damage, +25% cooldown recovery speed, and +75% Mana Rune bonus contribution. The counter resets on trigger.", "通过魔力符文额外耗蓝累计达到20%最大魔力值时，获得8秒奥术共鸣：造成伤害+25%，技能冷却恢复速度+25%，魔力符文额外耗蓝产生的技能强化效果+75%。触发后累计值清零。", "追加MP消費が最大MP20%に達すると8秒間、与ダメージ+25%、CD回復速度+25%、マナルーン強化貢献+75%。発動後カウントはリセット。"),
                    S(5, "Arcane Overload", "奥术超载", "奥術過負荷", "Mana Soul recovery becomes 400%, Mana Overflow cap becomes 300% max mana, and Elite/Boss kills grant permanent Mana Conversion Efficiency +10%, up to 300%.", "魔力之魂恢复量提高至400%，Mana Overflow上限提高至300%最大魔力值。击杀精英或Boss时，魔力转化效率永久+10%，最高300%。", "マナソウル回復量400%、Mana Overflow上限最大MP300%。エリート/Boss撃破でマナ変換効率+10%（最大300%）。"));

            case RuneType.Thorn:
                return new RuneDisplayText(
                    L("Thorn Rune", "荆棘符文", "ソーンルーン"),
                    L("A rune of pain and retaliation that turns incoming attacks into thorn damage.", "由痛苦与反击意志凝结而成的符文，能将承受的攻击转化为反击力量。", "痛みと反撃の意志が結晶化したルーン。受けた攻撃を棘の反撃に変える。"),
                    new[]
                    {
                        L("Monster damage taken -25%.", "受到怪物造成的伤害降低25%。", "モンスターからのダメージ-25%。"),
                        L("When hit by a monster, deal Thorn damage to the attacker. Base Thorn value is (10% max HP + main attributes + Luck) x 30%.", "受到怪物攻击时，对攻击者造成荆棘伤害。基础荆棘值为（自身10%最大生命值+主要属性+幸运）×30%。", "モンスターに攻撃されると攻撃者に棘ダメージ。基礎値は（最大HP10%+主要属性+運）×30%。"),
                        L("Thorn damage +100%.", "荆棘伤害提高100%。", "棘ダメージ+100%。"),
                        L("After casting, your next enemy hit adds Thorn damage equal to 150% current Thorn value.", "每次施放技能后，下一次对敌人造成伤害时，追加150%当前荆棘值的伤害。", "スキル後の次の対敵ヒットに現在棘値150%分を追加。"),
                        L("When hit by a monster, auto-release Thorn Counter around you.", "受到怪物攻击时，自动释放荆棘反击。", "被弾時にThorn Counterを自動発動。")
                    },
                    S(2, "Thorn Drain", "倒刺汲取", "棘の吸収", "Thorn damage that successfully hits restores 2% max HP, at most once per second.", "荆棘伤害成功命中后恢复2%最大生命值，每1秒最多1次。", "棘ダメージ命中時、最大HP2%回復。1秒に1回まで。"),
                    S(4, "Thorn Backlash", "荆棘反噬", "棘の反噬", "When taking monster damage, this hit is reduced by an extra 30% and the attacker takes Thorn damage equal to 200% current Thorn value. 5s cooldown.", "受到怪物伤害时，本次伤害额外降低30%，并对攻击者造成200%当前荆棘值的荆棘伤害。5秒冷却。", "モンスターダメージをさらに30%軽減し、攻撃者に現在棘値200%の棘ダメージ。5秒CD。"),
                    S(5, "Thousand-Thorn Counter", "万刺反击", "千の棘", "Thorn damage +100%, Thorn Counter cooldown becomes 2s, and Elite/Boss kills grant permanent Thorn Efficiency +10%, up to 300%.", "荆棘伤害额外提高100%，荆棘反击冷却缩短至2秒。击杀精英或Boss时，荆棘效率永久+10%，最高300%。", "棘ダメージ+100%、Thorn Counter CD2秒。エリート/Boss撃破で棘効率+10%（最大300%）。"));

            case RuneType.Luck:
                return new RuneDisplayText(
                    L("Luck Rune", "幸运符文", "幸運ルーン"),
                    L("A fate rune that turns chance into combat rewards and long-term growth.", "由命运气息凝结而成的符文，能将战斗中的偶然收益转化为长期成长。", "運命の気配から生まれたルーン。偶然の恩恵を成長に変える。"),
                    new[]
                    {
                        L("Luck +5.", "幸运+5。", "運+5。"),
                        L("When a Growth Soul is generated, 30% chance to increase its point by 1, up to 5.", "成长之魂生成时，有30%概率使其点数+1，最高5点。", "成長ソウル生成時、30%の確率で値+1（最大5）。"),
                        L("Kills have a 25% chance to drop 1 extra Growth Soul.", "击杀敌人时，有25%概率额外掉落1个成长之魂。", "敵を倒すと25%の確率で成長ソウルを1個追加ドロップ。"),
                        L("Picking up any Soul has a 20% chance to copy 1 Soul of the same type. The copy is fixed at 2 points.", "拾取任意灵魂时，有20%概率额外复制1个同类灵魂，复制灵魂固定为2点。", "ソウル取得時、20%の確率で同種ソウルを1個コピー。値は2固定。"),
                        L("Elite/Boss kills drop 5 extra Growth Souls.", "击杀精英敌人或Boss时，额外掉落5个成长之魂。", "エリート/Boss撃破時、成長ソウルを5個追加ドロップ。")
                    },
                    S(2, "Fate Roll", "命运一掷", "運命の一投", "Each successful skill cast has a 20% chance to trigger a fate lottery from equipped non-Luck rune types. If no other rune type is equipped, restore 5% max HP and 5% max Mana.", "每次成功施放技能时，有20%概率进行命运抽奖，从当前装备的非幸运符文种类中抽取1种并触发对应拟态效果。若没有其他符文，则恢复5%最大生命值和5%最大魔力值。", "スキル成功時、20%の確率で幸運以外の装備ルーンから1種類を抽選し、対応する恩恵を発動。他のルーンがなければ最大HP5%と最大MP5%を回復。"),
                    S(4, "Double Grace", "双重眷顾", "二重の加護", "Lottery chance becomes 35%. On success, draw two different results; if only one result exists, the second triggers at 50% value.", "命运抽奖基础概率提高至35%。成功时抽取两个不同结果；若只有1种结果，第2次效果降为50%。", "抽選確率が35%になり、成功時に異なる2結果を発動。1種類だけなら同じ結果をもう一度発動し、2回目は50%効果。"),
                    S(5, "Fate Jackpot", "命运大奖", "運命の大当たり", "On a successful lottery, 15% chance to trigger all current lottery results instead of normal draws. Elite/Boss kills grant permanent Luck Efficiency +10%, up to 300%. Luck Efficiency affects lottery and jackpot chance, clamped to 100%.", "命运抽奖成功时，有15%概率改为触发当前抽奖池中全部结果。击杀精英或Boss时，幸运效率永久+10%，最高300%。幸运效率影响抽奖和大奖概率，最终概率不超过100%。", "抽選成功時、15%の確率で抽選池の全結果を発動。エリート/Boss撃破で幸運効率+10%（最大300%）。確率は最大100%。"));

            default:
                return null;
        }
    }

    private static RuneLocalizedText L(string english, string chinese, string japanese)
    {
        return new RuneLocalizedText(english, chinese, japanese);
    }

    private static RuneSetDisplayText S(int requiredCount, string englishName, string chineseName, string japaneseName, string englishDescription, string chineseDescription, string japaneseDescription)
    {
        return new RuneSetDisplayText(requiredCount, L(englishName, chineseName, japaneseName), L(englishDescription, chineseDescription, japaneseDescription));
    }

    private sealed class RuneDisplayText
    {
        public readonly RuneLocalizedText Name;
        public readonly RuneLocalizedText Flavor;
        public readonly RuneLocalizedText[] BaseEffects;
        public readonly RuneSetDisplayText Set2;
        public readonly RuneSetDisplayText Set4;
        public readonly RuneSetDisplayText Set5;

        public RuneDisplayText(RuneLocalizedText name, RuneLocalizedText flavor, RuneLocalizedText[] baseEffects, RuneSetDisplayText set2, RuneSetDisplayText set4, RuneSetDisplayText set5)
        {
            Name = name;
            Flavor = flavor;
            BaseEffects = baseEffects ?? new RuneLocalizedText[0];
            Set2 = set2;
            Set4 = set4;
            Set5 = set5;
        }
    }

    private sealed class RuneSetDisplayText
    {
        public readonly int RequiredCount;
        public readonly RuneLocalizedText Name;
        public readonly RuneLocalizedText Description;

        public RuneSetDisplayText(int requiredCount, RuneLocalizedText name, RuneLocalizedText description)
        {
            RequiredCount = requiredCount;
            Name = name;
            Description = description;
        }
    }

    private struct RuneLocalizedText
    {
        private readonly string english;
        private readonly string chinese;
        private readonly string japanese;

        public RuneLocalizedText(string english, string chinese, string japanese)
        {
            this.english = english;
            this.chinese = chinese;
            this.japanese = japanese;
        }

        public string GetCurrentLanguageText()
        {
            GameLocalization localization = GameLocalization.Instance;
            GameLanguage language = localization != null ? localization.CurrentLanguage : GameLanguage.SimplifiedChinese;
            switch (language)
            {
                case GameLanguage.English:
                    return string.IsNullOrWhiteSpace(english) ? chinese : english;
                case GameLanguage.Japanese:
                    return string.IsNullOrWhiteSpace(japanese) ? chinese : japanese;
                default:
                    return chinese;
            }
        }
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
