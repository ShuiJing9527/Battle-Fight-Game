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

public enum BattleDamageKind
{
    Unknown = 0,
    PlayerActiveSkill = 1,
    MarkExplosion = 2,
    ReflectDamage = 3,
    ThornRetaliation = 4,
    ThornExplosion = 5,
    ShieldBonusDamage = 6,
    MonsterDamage = 7,
    EnvironmentDamage = 8,
    SelfDamage = 9
}

[System.Serializable]
public struct BattleDamage
{
    public float amount;
    public BattleDamageType damageType;
    public GameObject source;
    public bool isCritical;
    public bool bypassAttackerMultipliers;
    public bool bypassAmbientAffinity;
    public bool bypassAffinityModifier;
    public bool bypassEvasion;
    public bool suppressGaugeNotification;
    public string debugTag;
    public string skillName;
    public string damageSource;
    public string attackKind;
    public int packetId;
    public bool isReflectDamage;
    public int castId;
    public GameObject sourceOwner;
    public BattleDamageKind damageKind;
    public bool bypassRuneFlatDamage;
    public bool bypassLifesteal;
    public bool suppressThornReaction;

    public BattleDamage(float amount, BattleDamageType damageType, GameObject source, bool isCritical = false)
    {
        this.amount = Mathf.Max(0f, amount);
        this.damageType = damageType;
        this.source = source;
        this.isCritical = isCritical;
        bypassAttackerMultipliers = false;
        bypassAmbientAffinity = false;
        bypassAffinityModifier = false;
        bypassEvasion = false;
        suppressGaugeNotification = false;
        debugTag = string.Empty;
        skillName = string.Empty;
        damageSource = string.Empty;
        attackKind = string.Empty;
        packetId = 0;
        isReflectDamage = false;
        castId = -1;
        sourceOwner = null;
        damageKind = BattleDamageKind.Unknown;
        bypassRuneFlatDamage = false;
        bypassLifesteal = false;
        suppressThornReaction = false;
    }
}
