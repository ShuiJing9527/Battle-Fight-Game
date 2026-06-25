using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RunePickup : MonoBehaviour
{
    public RuneDefinition rune;
    public bool destroyAfterPickup = true;

    private static bool warnedMissingSharedInventory;

    public void SetRune(RuneDefinition newRune)
    {
        rune = newRune;
    }

    private void Reset()
    {
        Collider pickupCollider = GetComponent<Collider>();
        pickupCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        RuneInventory inventory = ResolveInventory(other);

        if (inventory == null || rune == null)
        {
            return;
        }

        inventory.AddRune(rune);
        if (destroyAfterPickup)
        {
            Destroy(gameObject);
        }
    }

    private RuneInventory ResolveInventory(Collider other)
    {
        // 先找共享 RuneSystem 背包，是为了让所有角色看到同一份符文状态。
        // 只有共享背包不存在时，才回退到触发碰撞的玩家背包，避免拾取结果分叉。
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

        RuneInventory inventory = other.GetComponentInParent<RuneInventory>();
        if (inventory != null)
        {
            return inventory;
        }

        PlayerMovement playerMovement = other.GetComponentInParent<PlayerMovement>();
        if (playerMovement != null)
        {
            return GetOrAddRuneInventory(playerMovement.gameObject);
        }

        Player01SkillController playerSkillController = other.GetComponentInParent<Player01SkillController>();
        if (playerSkillController != null)
        {
            return GetOrAddRuneInventory(playerSkillController.gameObject);
        }

        Transform root = other.transform.root;
        if (root != null && root.CompareTag("Player"))
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
}
