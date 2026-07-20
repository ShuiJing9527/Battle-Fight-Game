using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class RunePickup : MonoBehaviour
{
    public static bool IsWorldRunePickupPaused { get; private set; }

    public RuneDefinition rune;
    public bool destroyAfterPickup = true;

    [SerializeField, Min(0f)] private float pickupDelay = 0.6f;
    [SerializeField] private bool debugPickupTraceLog = false;

    private static bool warnedMissingSharedInventory;

    private bool collected;
    private bool pickupEnabledLogged;
    private bool loggedPausedTriggerSource;
    private bool loggedSpawnTrace;
    private float pickupEnabledAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticTestingState()
    {
        IsWorldRunePickupPaused = false;
        warnedMissingSharedInventory = false;
    }

    public static void SetWorldRunePickupPaused(bool paused)
    {
        if (IsWorldRunePickupPaused == paused)
        {
            return;
        }

        IsWorldRunePickupPaused = paused;
        Debug.Log(paused
            ? "[RunePickupTest] World rune pickup PAUSED"
            : "[RunePickupTest] World rune pickup RESUMED");
    }

    public static bool ToggleWorldRunePickupPaused()
    {
        bool next = !IsWorldRunePickupPaused;
        SetWorldRunePickupPaused(next);
        return next;
    }

    public void SetRune(RuneDefinition newRune)
    {
        rune = newRune;
    }

    private void Awake()
    {
        LogSpawnTraceIfNeeded();
    }

    private void OnEnable()
    {
        collected = false;
        pickupEnabledLogged = false;
        loggedPausedTriggerSource = false;
        loggedSpawnTrace = false;
        pickupEnabledAt = Time.time + Mathf.Max(0f, pickupDelay);
        LogSpawnTraceIfNeeded();
    }

    private void Reset()
    {
        Collider pickupCollider = GetComponent<Collider>();
        pickupCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider other)
    {
        string runeName = GetRuneDebugName();

        if (collected)
        {
            return;
        }

        if (IsWorldRunePickupPaused)
        {
            LogTriggerSourceForTesting(other);
            return;
        }

        if (Time.time < pickupEnabledAt)
        {
            return;
        }

        if (!pickupEnabledLogged)
        {
            pickupEnabledLogged = true;
            if (debugPickupTraceLog)
            {
                Debug.Log($"[RunePickupTrace] Pickup enabled rune={name}", this);
            }
        }

        RuneInventory inventory = ResolveInventory(other);

        if (inventory == null || rune == null)
        {
            return;
        }

        collected = true;
        Collider[] pickupColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < pickupColliders.Length; i++)
        {
            if (pickupColliders[i] != null)
            {
                pickupColliders[i].enabled = false;
            }
        }

        inventory.AddRune(rune, "Pickup");
        if (debugPickupTraceLog)
        {
            Debug.Log($"[RunePickupTrace] Picked rune={runeName}, player={other.transform.root.name}", this);
        }

        if (destroyAfterPickup)
        {
            Destroy(gameObject);
        }
    }

    private RuneInventory ResolveInventory(Collider other)
    {
        // Prefer the shared RuneSystem inventory so both characters observe the same rune state.
        RuneInventory sharedInventory = ResolveSharedInventory();
        if (sharedInventory != null)
        {
            return sharedInventory;
        }

        WarnMissingSharedInventoryOnce();

        if (other == null)
        {
            return null;
        }

        Transform root = other.transform.root;
        if (root == null || other.transform != root || !root.CompareTag("Player"))
        {
            return null;
        }

        RuneInventory inventory = root.GetComponent<RuneInventory>();
        if (inventory != null)
        {
            return inventory;
        }

        inventory = root.GetComponentInChildren<RuneInventory>(true);
        if (inventory != null)
        {
            return inventory;
        }

        PlayerMovement playerMovement = root.GetComponent<PlayerMovement>() ?? root.GetComponentInChildren<PlayerMovement>(true);
        if (playerMovement != null)
        {
            return GetOrAddRuneInventory(root.gameObject);
        }

        return null;
    }

    private RuneInventory GetOrAddRuneInventory(GameObject owner)
    {
        return owner.GetComponent<RuneInventory>() ?? owner.AddComponent<RuneInventory>();
    }

    private RuneInventory ResolveSharedInventory()
    {
        RuneDropManager dropManager = RuneDropManager.Instance;
        if (dropManager != null)
        {
            RuneInventory inventory = dropManager.GetComponent<RuneInventory>();
            if (inventory != null)
            {
                return inventory;
            }

            inventory = dropManager.GetComponentInChildren<RuneInventory>(true);
            if (inventory != null)
            {
                return inventory;
            }
        }

        RuneLibrary[] libraries = Object.FindObjectsOfType<RuneLibrary>(true);
        for (int i = 0; i < libraries.Length; i++)
        {
            RuneLibrary library = libraries[i];
            if (library == null)
            {
                continue;
            }

            RuneInventory inventory = library.GetComponent<RuneInventory>();
            if (inventory != null)
            {
                return inventory;
            }

            inventory = library.GetComponentInChildren<RuneInventory>(true);
            if (inventory != null)
            {
                return inventory;
            }
        }

        return null;
    }

    private void WarnMissingSharedInventoryOnce()
    {
        if (warnedMissingSharedInventory)
        {
            return;
        }

        warnedMissingSharedInventory = true;
        Debug.LogWarning("[RunePickup] Missing shared RuneInventory on RuneSystem. Falling back to player RuneInventory.", this);
    }

    private void LogSpawnTraceIfNeeded()
    {
        if (loggedSpawnTrace || !debugPickupTraceLog)
        {
            return;
        }

        loggedSpawnTrace = true;
        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        Scene scene = gameObject.scene;
        string spriteName = spriteRenderer != null && spriteRenderer.sprite != null ? spriteRenderer.sprite.name : "null";
        string colliderRadius = sphereCollider != null ? sphereCollider.radius.ToString("F3") : "n/a";

        Debug.Log(
            "[RunePickupTest] Spawned world rune " +
            "Object=" + name +
            " InstanceID=" + GetInstanceID() +
            " Rune=" + GetRuneDebugName() +
            " Position=" + transform.position +
            " Scene=" + (scene.IsValid() ? scene.name : "InvalidScene") +
            " ColliderRadius=" + colliderRadius +
            " Sprite=" + spriteName,
            this);
    }

    private void LogTriggerSourceForTesting(Collider other)
    {
        if (loggedPausedTriggerSource || other == null)
        {
            return;
        }

        loggedPausedTriggerSource = true;

        Transform otherRoot = other.transform.root;
        RuneInventory resolvedInventory = ResolveInventory(other);
        string layerName = LayerMask.LayerToName(other.gameObject.layer);
        string layerText = string.IsNullOrEmpty(layerName) ? other.gameObject.layer.ToString() : layerName + "/" + other.gameObject.layer;
        float distanceToRune = Vector3.Distance(transform.position, other.bounds.ClosestPoint(transform.position));

        Debug.Log(
            "[RunePickupTest] " +
            "Rune=" + GetRuneDebugName() +
            " PickupObject=" + name +
            " TriggerObject=" + other.gameObject.name +
            " TriggerPath=" + BuildHierarchyPath(other.transform) +
            " TriggerRoot=" + (otherRoot != null ? otherRoot.name : "null") +
            " RootTag=" + (otherRoot != null ? otherRoot.tag : "null") +
            " Layer=" + layerText +
            " ColliderType=" + other.GetType().Name +
            " IsTrigger=" + other.isTrigger +
            " SameAsRoot=" + (otherRoot != null && other.transform == otherRoot) +
            " ResolvedInventory=" + (resolvedInventory != null ? resolvedInventory.name : "null") +
            " DistanceToRune=" + distanceToRune.ToString("F3") +
            " RunePosition=" + transform.position +
            " ColliderPosition=" + other.bounds.center,
            this);
    }

    private string GetRuneDebugName()
    {
        return rune != null && !string.IsNullOrEmpty(rune.runeName)
            ? rune.runeName
            : (rune != null ? rune.runeType.ToString() : "null");
    }

    private static string BuildHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "null";
        }

        string path = target.name;
        Transform current = target.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
