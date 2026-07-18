using UnityEngine;

public enum ExternalLaunchPhase
{
    None,
    Rising,
    Falling,
    LandingConfirm
}

[System.Serializable]
public class ExternalLaunchSafetySettings
{
    [Min(0.01f)] public float minimumRetriggerInterval = 0.15f;
    [Min(0.05f)] public float minimumLockDuration = 0.15f;
    [Min(0.1f)] public float maximumAirborneDuration = 2.5f;
    [Min(0.01f)] public float groundProbeDistance = 0.2f;
    [Min(1)] public int landingConfirmFrames = 2;
    [Min(0f)] public float groundSkin = 0.02f;
    [Min(0.01f)] public float minorGroundPenetrationMaxCorrection = 0.1f;
    [Min(0.1f)] public float fallRecoveryDistance = 6f;
    [Min(0.01f)] public float startGroundCorrectionMax = 0.15f;
    [Min(0f)] public float startGroundSkin = 0.03f;
    public bool enableRiseSafety = true;
    [Min(0f)] public float riseSafetyVelocity = 7f;
    [Min(0f)] public float minimumAcceptedUpwardVelocity = 1f;
    [Min(0f)] public float minimumRiseDistance = 0.02f;
    [Min(1)] public int riseVerificationSteps = 2;
    public CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
}

public interface IExternalLaunchReceiver
{
    bool IsExternalLaunchActive { get; }
    Rigidbody ExternalLaunchBody { get; }
    ExternalLaunchPhase CurrentExternalLaunchPhase { get; }
    Component LaunchOwnerComponent { get; }
    void ApplyExternalLaunch(Vector3 launchVelocity, float duration, Vector3 separationOffset, int sequenceId = 0);
}
