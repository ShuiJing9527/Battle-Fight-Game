using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCameraRig : MonoBehaviour
{
    [Header("Required")]
    [Tooltip("Drag the Player transform (root) here.")]
    public Transform playerSlot;

    [Header("Camera Distance/Offset (Relative to Player Pivot)")]
    // 对应图1你的 Z 位置参数 (-10)
    [Tooltip("Back distance from player pivot.")]
    [Min(0.1f)] public float distance = 10f;

    // 对应图1你的 Y 位置参数 (2)
    [Tooltip("Height distance from player pivot.")]
    public float height = 2f;

    // 对应图1你的 X 旋转参数 (10)
    [Header("Camera Rotation Angle")]
    [Tooltip("Pitch angle. Camera tilting down.")]
    public float pitchRotation = 10f;

    [Header("Behavior")]
    [Tooltip("Horizontal rotation (yaw) of the camera around the player.")]
    public float yaw = 0f;
    [Tooltip("Set to (0, 0, 0) if player slot is base pivot. Use to target head/torso.")]
    public Vector3 targetCenterOffset = new Vector3(0f, 0f, 0f); // 通常保持0,除非Player pivot在脚底想看头
    public bool lockEveryFrame = true;

    private void Start()
    {
        ApplyCameraLock();
    }

    private void LateUpdate()
    {
        if (lockEveryFrame)
        {
            ApplyCameraLock();
        }
    }

    private void ApplyCameraLock()
    {
        if (playerSlot == null)
        {
            Debug.LogError("PlayerCameraRig.playerSlot is empty. Drag the Player transform into this slot.", this);
            return;
        }

        // 1. 计算基准中心位置（通常就是玩家位置）
        Vector3 pivotCenter = playerSlot.position + targetCenterOffset;

        // 2. 计算摄像机的平移偏移 (不考虑旋转时的默认偏移)
        // 放在玩家后下方 (-distance), 高度 (height), X偏置 (0)
        Vector3 baseOffsetVector = new Vector3(0f, height, -distance);

        // 3. 应用水平旋转 (Yaw - 摄像机绕玩家转)
        Quaternion horizontalRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 finalPosOffset = horizontalRotation * baseOffsetVector;

        // 设置最终位置
        transform.position = pivotCenter + finalPosOffset;

        // 4. 设置最终旋转角度 (强制使用图1你想要的X角度 10 度)
        // 旋转 = (固定的向下倾斜 10度, 摄像机当前的水平旋转角度, 0不翻滚)
        transform.rotation = Quaternion.Euler(pitchRotation, yaw, 0f);
    }

    // 可选：方便在 Inspector 快速重置参数
    private void Reset()
    {
        distance = 10f;
        height = 2f;
        pitchRotation = 10f;
        targetCenterOffset = Vector3.zero;
    }
}
