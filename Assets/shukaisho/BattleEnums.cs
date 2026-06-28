using UnityEngine;

public enum BattleDamageType
{
    Physical,
    Special
}

public enum DamagePopupType
{
    Normal,
    Physical,
    Special
}

public enum SoulType
{
    Life = 1,
    Energy = 2,
    Growth = 3,
    Function = 4
}

public enum BattleSkillType
{
    SmallSkill,
    Ultimate
}

public enum RuneRarity
{
    Common,
    Rare,
    Epic
}

[System.Obsolete("Legacy rune mechanic enum. New rune system uses RuneType/count-based effects instead.")]
public enum RuneMechanic
{
    Combo,
    DoubleStar,
    Afterimage,
    Split,
    Echo,
    BloodExplosion,
    DrainMark,
    Regeneration,
    Exchange,
    SoulLink
}

public enum RuneType
{
    None = 0,
    Life = 1,
    Shield = 2,
    Mana = 3,
    Thorn = 4,
    Luck = 5
}

[System.Serializable]
public struct BattleDamage
{
    public float amount;
    public BattleDamageType damageType;
    public GameObject source;
    public bool isCritical;

    public BattleDamage(float amount, BattleDamageType damageType, GameObject source, bool isCritical = false)
    {
        this.amount = Mathf.Max(0f, amount);
        this.damageType = damageType;
        this.source = source;
        this.isCritical = isCritical;
    }
}
