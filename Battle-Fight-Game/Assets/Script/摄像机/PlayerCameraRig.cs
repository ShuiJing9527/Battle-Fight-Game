using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCameraRig : MonoBehaviour
{
    [Header("Required")]
    public Transform playerSlot;

    [Header("Camera")]
    [Min(0.1f)] public float distance = 18f;
    public float height = 4f;
    public float yaw = 0f;
    public Vector3 centerOffset = new Vector3(0f, 1f, 0f);
    public bool lockEveryFrame = true;

    private void Reset()
    {
        Camera camera = GetComponent<Camera>();
        if (camera != null)
        {
            camera.fieldOfView = 35f;
        }
    }

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

        Vector3 targetCenter = playerSlot.position + centerOffset;
        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 offset = yawRotation * new Vector3(0f, height, -distance);

        transform.position = targetCenter + offset;
        transform.rotation = Quaternion.LookRotation(targetCenter - transform.position, Vector3.up);
    }
}
