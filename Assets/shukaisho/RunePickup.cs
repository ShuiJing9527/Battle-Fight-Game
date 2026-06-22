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
        RuneInventory inventory = other.GetComponentInParent<RuneInventory>();
        if (inventory == null)
        {
            inventory = other.GetComponentInParent<Player01SkillController>()?.gameObject.AddComponent<RuneInventory>();
        }

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
}
