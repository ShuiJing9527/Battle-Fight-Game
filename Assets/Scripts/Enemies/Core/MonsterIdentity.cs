using UnityEngine;

public enum MonsterSpecies
{
    BlueSlime,
    GreenSlime,
    LavaSlime,
    PoisonSlime,
    RainbowSlime,
    Flying,
    Ranged,
    Tank,
    Assassin
}

public enum MonsterRank
{
    Normal,
    Elite,
    Boss
}

public enum MonsterAttackStyle
{
    Melee,
    Ranged,
    ElementalBoss
}

public class MonsterIdentity : MonoBehaviour
{
    public MonsterSpecies species = MonsterSpecies.BlueSlime;
    public MonsterRank rank = MonsterRank.Normal;
    public MonsterAttackStyle attackStyle = MonsterAttackStyle.Melee;
}
