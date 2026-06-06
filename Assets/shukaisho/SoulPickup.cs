using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SoulPickup : MonoBehaviour
{
    [Header("魂力系统表")]
    public SoulType soulType = SoulType.Life;
    [Min(0f)] public float amount = 1f;
    public bool destroyAfterPickup = true;

    private void Reset()
    {
        Collider pickupCollider = GetComponent<Collider>();
        pickupCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        BattleResourceBank bank = other.GetComponentInParent<BattleResourceBank>();
        if (bank == null)
        {
            return;
        }

        bank.ApplySoul(soulType, amount);

        if (destroyAfterPickup)
        {
            Destroy(gameObject);
        }
    }
}
