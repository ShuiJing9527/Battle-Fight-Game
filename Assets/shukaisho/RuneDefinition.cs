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
                rune.description = "A rune born from vitality. It strengthens the body and turns life force into healing, damage, and growth.";
                rune.tier1Effect = "Max HP +10%.";
                rune.tier2Effect = "When using the socketed skill, restore HP equal to 1% of max HP.";
                rune.tier3Effect = "Overflow healing from this effect becomes permanent shield, up to 50% of max HP.";
                rune.tier4Effect = "The first enemy hit by the socketed skill takes bonus damage equal to 1% of max HP. Triggers once per cast.";
                rune.tier5Effect = "When killing a monster, gain 1 Life Attribute Soul and 1 Life Recovery Soul.";
                rune.setBonusEffect = "1-piece max HP bonus becomes 50%; 2-piece healing becomes 5%; 3-piece permanent shield cap becomes 100% of max HP; 4-piece bonus damage becomes 5% of max HP; 5-piece also grants all-stat bonuses based on max HP.";
                break;

            case RuneType.Shield:
                rune.runeId = 102;
                rune.runeName = "Shield Rune";
                rune.description = "A rune shaped by protective will. It stores shield power and converts shield strength into defense and offense.";
                rune.tier1Effect = "After taking no monster damage for 3 seconds, gain shield equal to 15% of max HP.";
                rune.tier2Effect = "While shielded, damage dealt +25%.";
                rune.tier3Effect = "Shield gained +100%.";
                rune.tier4Effect = "The first enemy hit by the socketed skill takes bonus damage equal to 10% of current shield. Triggers once per cast.";
                rune.tier5Effect = "When killing a monster, gain 1 Shield Soul.";
                rune.setBonusEffect = "1-piece shield becomes 50% of max HP; 2-piece damage bonus becomes 50%; 3-piece shield gained becomes +200%; 4-piece bonus damage becomes 25% of current shield; 5-piece permanently increases shield efficiency by 10% whenever a Boss is killed.";
                break;

            case RuneType.Mana:
                rune.runeId = 103;
                rune.runeName = "Mana Rune";
                rune.description = "A rune condensed from pure mana. It expands mana capacity, improves mana recovery, and turns overflow mana into burst damage.";
                rune.tier1Effect = "Max Mana +150.";
                rune.tier2Effect = "Mana recovery speed +150%.";
                rune.tier3Effect = "When killing a monster, drop 1 extra Mana Soul. Overflow recovery from Mana Souls becomes Mana Overflow, up to 100% of max Mana.";
                rune.tier4Effect = "On the first enemy hit by the socketed skill, consume up to 100 Mana or Mana Overflow to deal bonus damage equal to consumed value x3.";
                rune.tier5Effect = "Convert max Mana into all-stat bonuses at 20% efficiency.";
                rune.setBonusEffect = "1-piece max Mana bonus becomes 400; 2-piece mana recovery becomes +300%; 3-piece Mana Soul recovery becomes 300% and Mana Overflow cap becomes 200% of max Mana; 4-piece consume cap becomes 200 and damage multiplier becomes x4; 5-piece permanently increases mana conversion efficiency by 10% whenever a Boss is killed.";
                break;

            case RuneType.Thorn:
                rune.runeId = 104;
                rune.runeName = "Thorn Rune";
                rune.description = "A rune formed from pain and retaliation. It reduces incoming damage and converts attacks taken into counter damage.";
                rune.tier1Effect = "Damage taken -15%.";
                rune.tier2Effect = "When hit by a monster, deal thorn damage to the attacker equal to 15% of total attributes.";
                rune.tier3Effect = "The first damage dealt by the socketed skill adds bonus damage equal to 150% of thorn damage. Triggers once per cast.";
                rune.tier4Effect = "When hit by a monster, automatically trigger Thorn Counter from the socketed skill without cooldown or resource cost. Base trigger cooldown: 4 seconds.";
                rune.tier5Effect = "Thorn damage +100%.";
                rune.setBonusEffect = "1-piece damage reduction becomes 35%; 2-piece thorn damage becomes 40% of total attributes; 3-piece added damage becomes 300% of thorn damage; 4-piece trigger cooldown becomes 2 seconds; 5-piece permanently increases thorn efficiency by 15% whenever a Boss is killed.";
                break;

            case RuneType.Luck:
                rune.runeId = 105;
                rune.runeName = "Luck Rune";
                rune.description = "A rune condensed from the breath of fate. It improves Soul gains and turns lucky combat rewards into long-term growth.";
                rune.tier1Effect = "Luck +1.";
                rune.tier2Effect = "When a monster drops an Attribute Soul, it has a 20% chance to increase that Soul's point value by 1, up to 5.";
                rune.tier3Effect = "When killing a monster, there is a 15% chance to drop 1 extra random Attribute Soul.";
                rune.tier4Effect = "When picking up any Soul, there is a 10% chance to copy 1 Soul of the same type. Copied Soul point value is 1.";
                rune.tier5Effect = "When killing a Boss, drop 2 extra random Attribute Souls.";
                rune.setBonusEffect = "1-piece Luck bonus becomes +3; 2-piece point increase chance becomes 35%; 3-piece extra Attribute Soul chance becomes 25%; 4-piece copy chance becomes 25% and copied Soul point value becomes 3; 5-piece permanently increases luck efficiency by 5% whenever a Boss is killed.";
                break;
        }

        return rune;
    }
}
