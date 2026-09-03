using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCameraRig : MonoBehaviour
{
    [Header("Required")]
    [Tooltip("Drag the Player transform (root) here.")]
    public Transform playerSlot;

    [Header("Auto Resolve")]
    [Tooltip("Automatically bind to the current active player when playerSlot is empty.")]
    public bool autoResolvePlayerSlot = true;
    [Tooltip("Fallback tag used when no party bootstrap is found.")]
    public string fallbackPlayerTag = "Player";
    [Tooltip("Fallback scene object names checked when tag lookup fails.")]
    public string[] fallbackPlayerNames = { "Player01", "Player02", "Player" };

    [Header("Camera Distance/Offset (Relative to Player Pivot)")]
    [Tooltip("Back distance from player pivot.")]
    [Min(0.1f)] public float distance = 10f;

    [Tooltip("Height distance from player pivot.")]
    public float height = 2f;

    [Header("Camera Rotation Angle")]
    [Tooltip("Pitch angle. Camera tilting down.")]
    public float pitchRotation = 10f;

    [Header("Behavior")]
    [Tooltip("Horizontal rotation (yaw) of the camera around the player.")]
    public float yaw = 0f;
    [Tooltip("Set to (0, 0, 0) if player slot is base pivot. Use to target head/torso.")]
    public Vector3 targetCenterOffset = new Vector3(0f, 0f, 0f);
    public bool lockEveryFrame = true;

    [Header("Foreground Occlusion")]
    [Tooltip("Fade large generated props when they block the camera's view of the player.")]
    public bool enableForegroundOcclusionFade = true;

    private Player2Bootstrap cachedBootstrap;
    private CameraOcclusionFader cachedOcclusionFader;
    private bool loggedMissingTarget;

    private void Start()
    {
        ResolvePlayerSlotIfNeeded();
        ApplyCameraLock();
    }

    private void LateUpdate()
    {
        if (lockEveryFrame)
        {
            ResolvePlayerSlotIfNeeded();
            ApplyCameraLock();
        }

        UpdateForegroundOcclusionFade();
    }

    private void ApplyCameraLock()
    {
        if (playerSlot == null)
        {
            return;
        }

        loggedMissingTarget = false;

        Vector3 pivotCenter = playerSlot.position + targetCenterOffset;
        Vector3 baseOffsetVector = new Vector3(0f, height, -distance);
        Quaternion horizontalRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 finalPosOffset = horizontalRotation * baseOffsetVector;

        transform.position = pivotCenter + finalPosOffset;
        transform.rotation = Quaternion.Euler(pitchRotation, yaw, 0f);
    }

    private void ResolvePlayerSlotIfNeeded()
    {
        if (!autoResolvePlayerSlot)
        {
            return;
        }

        if (playerSlot != null)
        {
            GameObject slotObject = playerSlot.gameObject;
            if (slotObject != null && slotObject.activeInHierarchy)
            {
                return;
            }

            playerSlot = null;
        }

        if (cachedBootstrap == null)
        {
            cachedBootstrap = FindObjectOfType<Player2Bootstrap>();
        }

        if (cachedBootstrap != null)
        {
            cachedBootstrap.EnsureInitializedForSpawn();
            if (cachedBootstrap.CurrentPlayerTransform != null)
            {
                playerSlot = cachedBootstrap.CurrentPlayerTransform;
                return;
            }

            if (cachedBootstrap.PartyLeader != null)
            {
                playerSlot = cachedBootstrap.PartyLeader.transform;
                return;
            }

            return;
        }

        if (!string.IsNullOrEmpty(fallbackPlayerTag))
        {
            GameObject fallbackPlayer = GameObject.FindWithTag(fallbackPlayerTag);
            if (fallbackPlayer != null)
            {
                playerSlot = fallbackPlayer.transform;
                return;
            }
        }

        for (int i = 0; i < fallbackPlayerNames.Length; i++)
        {
            GameObject namedPlayer = FindSceneObjectByNameIncludingInactive(fallbackPlayerNames[i]);
            if (namedPlayer != null)
            {
                playerSlot = namedPlayer.transform;
                return;
            }
        }

        Player2PrototypeController prototypeController = FindObjectOfType<Player2PrototypeController>(true);
        if (prototypeController != null)
        {
            playerSlot = prototypeController.transform;
        }
    }

    private void Reset()
    {
        distance = 10f;
        height = 2f;
        pitchRotation = 10f;
        targetCenterOffset = Vector3.zero;
        autoResolvePlayerSlot = true;
        fallbackPlayerTag = "Player";
        fallbackPlayerNames = new[] { "Player01", "Player02", "Player" };
        enableForegroundOcclusionFade = true;
    }

    private void UpdateForegroundOcclusionFade()
    {
        if (!enableForegroundOcclusionFade || playerSlot == null)
        {
            if (cachedOcclusionFader != null)
            {
                cachedOcclusionFader.enabled = false;
            }

            return;
        }

        Camera targetCamera = GetComponent<Camera>();
        if (targetCamera == null)
        {
            targetCamera = GetComponentInChildren<Camera>(true);
        }

        if (targetCamera == null)
        {
            return;
        }

        if (cachedOcclusionFader == null || cachedOcclusionFader.gameObject != targetCamera.gameObject)
        {
            cachedOcclusionFader = targetCamera.GetComponent<CameraOcclusionFader>();
            if (cachedOcclusionFader == null)
            {
                cachedOcclusionFader = targetCamera.gameObject.AddComponent<CameraOcclusionFader>();
            }
        }

        cachedOcclusionFader.enabled = true;
        cachedOcclusionFader.SetTarget(playerSlot);
    }

    private static GameObject FindSceneObjectByNameIncludingInactive(string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < all.Length; i++)
        {
            GameObject go = all[i];
            if (go == null || !go.scene.IsValid())
            {
                continue;
            }

            if (go.name == targetName)
            {
                return go;
            }
        }

        return null;
    }
}
