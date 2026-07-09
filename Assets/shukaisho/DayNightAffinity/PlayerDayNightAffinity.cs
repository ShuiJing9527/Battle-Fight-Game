using UnityEngine;

public enum PlayerDayNightAffinityType
{
    None,
    NightChild,
    DayChild
}

public class PlayerDayNightAffinity : MonoBehaviour
{
    [SerializeField] private PlayerDayNightAffinityType affinityType = PlayerDayNightAffinityType.None;

    public PlayerDayNightAffinityType AffinityType => affinityType;
    public bool IsNightChild => affinityType == PlayerDayNightAffinityType.NightChild;
    public bool IsDayChild => affinityType == PlayerDayNightAffinityType.DayChild;
}
