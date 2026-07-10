using UnityEngine;

public class LightFlowerHealing : MonoBehaviour
{
    [Header("持续回血")]
    [SerializeField, Min(0.1f)] private float healingRadius = 2.5f;
    [SerializeField, Min(0f)] private float healPerTick = 2f;
    [SerializeField, Min(0.05f)] private float tickInterval = 0.5f;

    private Player2Bootstrap playerBootstrap;
    private float nextHealTime;

    private void OnEnable()
    {
        nextHealTime = Time.time;
    }

    private void Update()
    {
        if (Time.time < nextHealTime)
        {
            return;
        }

        nextHealTime = Time.time + tickInterval;

        Transform player = ResolveCurrentPlayer();
        if (player == null)
        {
            return;
        }

        Vector3 offset = player.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > healingRadius * healingRadius)
        {
            return;
        }

        HealPlayer(player);
    }

    private Transform ResolveCurrentPlayer()
    {
        if (playerBootstrap == null)
        {
            playerBootstrap = FindObjectOfType<Player2Bootstrap>();
        }

        if (playerBootstrap != null && playerBootstrap.CurrentPlayerTransform != null)
        {
            return playerBootstrap.CurrentPlayerTransform;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        return taggedPlayer != null ? taggedPlayer.transform : null;
    }

    private void HealPlayer(Transform player)
    {
        CombatHealth health = player.GetComponentInParent<CombatHealth>();
        if (health == null)
        {
            return;
        }

        float currentHealth;
        float maxHealth;

        if (health.resourceBank != null)
        {
            currentHealth = health.resourceBank.currentHealth;
            maxHealth = health.resourceBank.maxHealth;
        }
        else
        {
            currentHealth = health.currentHealth;
            maxHealth = health.stats != null ? health.stats.maxHealth : health.currentHealth;
        }

        float missingHealth = Mathf.Max(0f, maxHealth - currentHealth);
        if (missingHealth > 0f)
        {
            health.Heal(Mathf.Min(healPerTick, missingHealth));
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.35f, 1f, 0.25f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, healingRadius);
    }
}
