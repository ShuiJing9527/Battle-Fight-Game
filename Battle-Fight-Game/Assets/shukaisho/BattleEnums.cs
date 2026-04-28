using UnityEngine;

public enum BattleDamageType
{
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

[System.Serializable]
public struct BattleDamage
{
    public float amount;
    public BattleDamageType damageType;
    public GameObject source;

    public BattleDamage(float amount, BattleDamageType damageType, GameObject source)
    {
        this.amount = Mathf.Max(0f, amount);
        this.damageType = damageType;
        this.source = source;
    }
}
