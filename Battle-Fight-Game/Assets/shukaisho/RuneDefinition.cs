using UnityEngine;

[System.Serializable]
public class RuneDefinition
{
    [Header("符文库")]
    public int id;
    public string runeName;
    public string category;
    public RuneRarity rarity;
    public RuneMechanic mechanic;
    public string triggerCondition;
    public string limitOrSideEffect;
    public string playStyle;

    [Header("机制参数")]
    [Min(0)] public int extraHitCount;
    [Min(1)] public int extraCastCount = 1;
    [Min(0f)] public float damageMultiplier = 1f;
    [Min(0f)] public float delaySeconds;
    [Min(0f)] public float cooldownMultiplier = 1f;
    [Min(0f)] public float range;
    [Min(0f)] public float healthCost;
    [Min(0f)] public float healAmount;
    public SoulPickup lifeSoulPrefab;

    public static RuneDefinition CreateTableRune(RuneMechanic mechanic)
    {
        RuneDefinition rune = new RuneDefinition
        {
            mechanic = mechanic,
            extraCastCount = 1,
            damageMultiplier = 1f,
            cooldownMultiplier = 1f
        };

        switch (mechanic)
        {
            case RuneMechanic.Combo:
                rune.id = 1;
                rune.runeName = "连击符文";
                rune.category = "改造类";
                rune.rarity = RuneRarity.Common;
                rune.extraHitCount = 1;
                rune.triggerCondition = "技能释放";
                rune.limitOrSideEffect = "单段伤害略降低";
                rune.playStyle = "连段流";
                break;
            case RuneMechanic.DoubleStar:
                rune.id = 2;
                rune.runeName = "双星符文";
                rune.category = "改造类";
                rune.rarity = RuneRarity.Rare;
                rune.extraCastCount = 2;
                rune.damageMultiplier = 0.5f;
                rune.triggerCondition = "技能释放";
                rune.limitOrSideEffect = "单次伤害降低";
                rune.playStyle = "触发流";
                break;
            case RuneMechanic.Afterimage:
                rune.id = 3;
                rune.runeName = "残影符文";
                rune.category = "改造类";
                rune.rarity = RuneRarity.Epic;
                rune.triggerCondition = "技能结束";
                rune.limitOrSideEffect = "技能CD增加";
                rune.playStyle = "技能流";
                break;
            case RuneMechanic.Split:
                rune.id = 4;
                rune.runeName = "分裂符文";
                rune.category = "改造类";
                rune.rarity = RuneRarity.Rare;
                rune.triggerCondition = "命中敌人";
                rune.limitOrSideEffect = "单体能力下降";
                rune.playStyle = "AOE流";
                break;
            case RuneMechanic.Echo:
                rune.id = 5;
                rune.runeName = "回响符文";
                rune.category = "机制类";
                rune.rarity = RuneRarity.Rare;
                rune.triggerCondition = "命中敌人";
                rune.limitOrSideEffect = "有触发间隔";
                rune.playStyle = "连锁流";
                break;
            case RuneMechanic.BloodExplosion:
                rune.id = 6;
                rune.runeName = "血爆符文";
                rune.category = "机制类";
                rune.rarity = RuneRarity.Rare;
                rune.triggerCondition = "击杀敌人";
                rune.limitOrSideEffect = "必须完成击杀";
                rune.playStyle = "收割流 / 生存";
                break;
            case RuneMechanic.DrainMark:
                rune.id = 7;
                rune.runeName = "汲取印记符文";
                rune.category = "机制类";
                rune.rarity = RuneRarity.Rare;
                rune.triggerCondition = "命中 + 击杀";
                rune.limitOrSideEffect = "需完成两步触发";
                rune.playStyle = "技术流";
                break;
            case RuneMechanic.Regeneration:
                rune.id = 8;
                rune.runeName = "再生符文";
                rune.category = "机制类";
                rune.rarity = RuneRarity.Common;
                rune.triggerCondition = "技能命中";
                rune.limitOrSideEffect = "需站在区域内";
                rune.playStyle = "阵地战";
                break;
            case RuneMechanic.Exchange:
                rune.id = 9;
                rune.runeName = "交换符文";
                rune.category = "高阶机制";
                rune.rarity = RuneRarity.Epic;
                rune.triggerCondition = "技能释放 + 命中";
                rune.limitOrSideEffect = "有生命风险";
                rune.playStyle = "高风险流";
                break;
            case RuneMechanic.SoulLink:
                rune.id = 10;
                rune.runeName = "灵魂链接符文";
                rune.category = "机制类";
                rune.rarity = RuneRarity.Rare;
                rune.triggerCondition = "命中多个敌人";
                rune.limitOrSideEffect = "依赖群体目标";
                rune.playStyle = "群战流";
                break;
        }

        return rune;
    }
}
