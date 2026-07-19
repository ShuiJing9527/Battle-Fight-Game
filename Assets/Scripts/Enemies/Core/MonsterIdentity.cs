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

public enum MonsterBossRole
{
    None,
    FinalBoss,
    SplitBoss
}

public class MonsterIdentity : MonoBehaviour
{
    public MonsterSpecies species = MonsterSpecies.BlueSlime;
    public MonsterRank rank = MonsterRank.Normal;
    public MonsterAttackStyle attackStyle = MonsterAttackStyle.Melee;
    public bool suppressRuneDrop;
    public MonsterBossRole bossRole = MonsterBossRole.None;
    public int splitPhaseIndex;
    public int splitBatchId;
    public int sourceFinalBossInstanceId;
}
