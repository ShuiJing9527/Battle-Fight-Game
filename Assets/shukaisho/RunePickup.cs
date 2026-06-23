using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RunePickup : MonoBehaviour
{
    public RuneDefinition rune;
    public bool destroyAfterPickup = true;

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
            return playerMovement.GetComponent<RuneInventory>() ?? playerMovement.gameObject.AddComponent<RuneInventory>();
        }

        Player01SkillController playerSkillController = other.GetComponentInParent<Player01SkillController>();
        if (playerSkillController != null)
        {
            return playerSkillController.GetComponent<RuneInventory>() ?? playerSkillController.gameObject.AddComponent<RuneInventory>();
        }

        Transform root = other.transform.root;
        if (root != null && root.CompareTag("Player"))
        {
            return root.GetComponent<RuneInventory>() ?? root.gameObject.AddComponent<RuneInventory>();
        }

        return null;
    }
}
