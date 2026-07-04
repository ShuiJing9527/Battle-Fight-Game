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
                rune.runeName = "Life Rune";
                rune.description = "A rune born from life force. It strengthens the body and converts vitality into healing, damage, shields, and growth.";
                rune.tier1Effect = "When you kill a monster, gain 1 Life Attribute Soul and 1 Life Recovery Soul.";
                rune.tier2Effect = "When you cast a skill, restore HP equal to 5% of your max HP. This can trigger once per skill cast.";
                rune.tier3Effect = "After casting a skill, the first enemy hit takes bonus damage equal to 5% of your max HP. This can trigger once per skill cast.";
                rune.tier4Effect = "If healing from Life Rune effects overheals you, the overflow is converted into permanent shield. Permanent shield does not decay over time and is capped at 100% of your max HP.";
                rune.tier5Effect = "Max HP increased by 50%.";
                rune.setBonusEffect = "Life Awakening. Gain all-stat bonuses based on max HP. Every 10 max HP counts as 1 Life Attribute. All stats except HP are increased by 10% of that Life Attribute value, rounded down.";
                break;

            case RuneType.Shield:
                rune.runeId = 102;
                rune.runeName = "Shield Rune";
                rune.description = "A rune formed from protective will. It builds shields when you avoid damage and converts shield power into stronger defense and offense.";
                rune.tier1Effect = "After not taking monster damage for 3 seconds, refill your current shield up to 50% of your max HP.";
                rune.tier2Effect = "While you have a shield, your damage dealt is increased by 30%.";
                rune.tier3Effect = "When you kill a monster, gain 1 Shield Soul.";
                rune.tier4Effect = "Shield gained is increased by 100%.";
                rune.tier5Effect = "After casting a skill, the first enemy hit takes bonus damage equal to 15% of your current shield. This can trigger once per skill cast. This damage is affected by Shield Efficiency.";
                rune.setBonusEffect = "Guardian Ascension. Shield cap is increased to 300% of your max HP. Each time you kill an elite monster or Boss, shield efficiency is permanently increased by 10%, up to 300%.";
                break;

            case RuneType.Mana:
                rune.runeId = 103;
                rune.runeName = "Mana Rune";
                rune.description = "A rune condensed from pure mana. It expands mana capacity, strengthens Mana Soul recovery, and converts extra mana consumption into stronger skill effects and growth.";
                rune.tier1Effect = "Max Mana increased by 200.";
                rune.tier2Effect = "Mana regeneration speed increased by 150%.";
                rune.tier3Effect = "When you kill a monster, an additional Mana Soul drops. If Mana Soul recovery overflows your mana, the overflow is converted into Mana Overflow. Mana Overflow is capped at 200% of your max mana.";
                rune.tier4Effect = "When you cast a skill, additionally consume up to 20% of your max mana from Mana Overflow first, then normal mana. Based on the actual extra consumed ratio, that skill's core effect is strengthened up to its own configured cap. This can trigger once per skill cast.";
                rune.tier5Effect = "Gain all-stat bonuses based on max mana. Every 10 max mana counts as 1 Mana Attribute. All stats except HP and MP are increased by 10% of that Mana Attribute value, affected by Mana Conversion Efficiency and rounded down.";
                rune.setBonusEffect = "Mana Ascension. Mana Soul recovery becomes 400% of its original value, Mana Overflow cap is increased to 300% of max mana, and each elite monster or Boss kill permanently increases Mana Conversion Efficiency by 10%, up to 300%.";
                break;

            case RuneType.Thorn:
                rune.runeId = 104;
                rune.runeName = "Thorn Rune";
                rune.description = "A rune born from pain and the will to counterattack. It reduces incoming damage and converts attacks received into retaliatory power.";
                rune.tier1Effect = "Monster damage taken is reduced by 25%.";
                rune.tier2Effect = "When hit by a monster, deal Thorn damage to the attacker. Base Thorn damage is (10% of max HP + Physical Attack + Physical Defense + Special Attack + Special Defense + Speed + Luck) x 30%.";
                rune.tier3Effect = "Thorn damage increased by 100%.";
                rune.tier4Effect = "After casting a skill, the first time that skill deals damage, add bonus damage equal to 150% of your current Thorn damage. This can trigger once per skill cast.";
                rune.tier5Effect = "When hit by a monster, automatically release Thorn Counter. Thorn Counter creates a thorn burst centered on you, damages enemies in range for 300% of your current Thorn damage, always includes the attacker, has its own cooldown, and does not trigger other auto-cast effects.";
                rune.setBonusEffect = "Pain Backlash. Thorn damage is additionally increased by 100%, Thorn Counter cooldown is reduced to 2 seconds, and each elite monster or Boss kill permanently increases Thorn Efficiency by 10%, up to 300%.";
                break;

            case RuneType.Luck:
                rune.runeId = 105;
                rune.runeName = "Luck Rune";
                rune.description = "A rune condensed from the breath of fate. It improves Soul acquisition efficiency and turns chance-based combat rewards into long-term growth.";
                rune.tier1Effect = "Luck increased by 5.";
                rune.tier2Effect = "When a monster drops an Attribute Soul, there is a 30% chance to increase that Soul's point value by 1, up to a maximum of 5.";
                rune.tier3Effect = "When you kill a monster, there is a 25% chance to drop 1 additional random Attribute Soul.";
                rune.tier4Effect = "When you pick up any Soul, there is a 20% chance to copy 1 Soul of the same type. The copied Soul has a fixed value of 2 points.";
                rune.tier5Effect = "When you kill an elite monster or Boss, drop 5 additional random Attribute Souls.";
                rune.setBonusEffect = "Fate's Favor. All Luck Rune probability effects have their base trigger rates multiplied by 1.5. Each elite monster or Boss kill permanently increases Luck Efficiency by 10%, up to 500%.";
                break;
        }

        return rune;
    }
}
