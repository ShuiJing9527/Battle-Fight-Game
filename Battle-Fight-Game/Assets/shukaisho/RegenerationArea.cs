using UnityEngine;

public class RegenerationArea : MonoBehaviour
{
    private float healAmount;

    public static void Create(Vector3 position, float healAmount)
    {
        GameObject areaObject = new GameObject("RegenerationArea");
        areaObject.transform.position = position;
        SphereCollider collider = areaObject.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 1f;

        RegenerationArea area = areaObject.AddComponent<RegenerationArea>();
        area.healAmount = healAmount;
    }

    private void OnTriggerStay(Collider other)
    {
        BattleResourceBank bank = other.GetComponentInParent<BattleResourceBank>();
        if (bank != null)
        {
            bank.Heal(healAmount * Time.deltaTime);
        }
    }
}
