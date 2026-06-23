using UnityEngine;
using UnderTheStars.GenerationMap;

[DisallowMultipleComponent]
public class PlayerSpawnManager : MonoBehaviour
{
    [SerializeField] private GameObject player01Prefab;
    [SerializeField] private GameObject player02Prefab;
    [SerializeField] private bool spawnPlayer02 = true;
    [SerializeField] private float player01YOffset = 0.75f;
    [SerializeField] private float player02YOffset = 1.2f;
    [SerializeField] private float partyMemberSpacing = 1.2f;
    [SerializeField] private LayerMask spawnBlockerLayers = ~0;
    [SerializeField, Min(0f)] private float spawnCheckRadius = 0.45f;
    [SerializeField, Min(0f)] private float spawnCheckHeight = 0.9f;
    [SerializeField, Min(1)] private int spawnSearchAttempts = 32;

    private GameObject player01Instance;
    private GameObject player02Instance;

    public bool SpawnPartyAtRandomSafePoint()
    {
        RandomMapGeneration mapGeneration = FindObjectOfType<RandomMapGeneration>();
        return SpawnPartyAtRandomSafePoint(mapGeneration);
    }

    public bool SpawnPartyAtRandomSafePoint(RandomMapGeneration mapGeneration)
    {
        if (mapGeneration == null)
        {
            Debug.LogWarning("[PlayerSpawnManager] Map generation not found.", this);
            return false;
        }

        for (int attempt = 0; attempt < Mathf.Max(1, spawnSearchAttempts); attempt++)
        {
            if (!mapGeneration.TryGetRandomGrassSafeSpawnWorldPosition(out Vector3 spawnPosition, out Vector2Int spawnCoord))
            {
                break;
            }

            if (IsSpawnBlocked(spawnPosition))
            {
                continue;
            }

            SpawnPartyAtWorldPosition(mapGeneration, spawnPosition, spawnCoord);
            return true;
        }

        Debug.LogWarning("[PlayerSpawnManager] No safe grass spawn position found.", this);
        return false;
    }

    public void SpawnPartyAtWorldPosition(RandomMapGeneration mapGeneration, Vector3 spawnPosition, Vector2Int spawnCoord)
    {
        if (player01Prefab == null)
        {
            Debug.LogWarning("[PlayerSpawnManager] player01Prefab is missing.", this);
            return;
        }

        player01Instance = EnsurePlayerInstance(player01Instance, player01Prefab, "Player01");
        if (player01Instance != null)
        {
            player01Instance.transform.position = spawnPosition + Vector3.up * player01YOffset;
            player01Instance.SetActive(true);
            ResetMotion(player01Instance);
        }

        if (spawnPlayer02 && player02Prefab != null)
        {
            player02Instance = EnsurePlayerInstance(player02Instance, player02Prefab, "Player02");
            if (player02Instance != null)
            {
                player02Instance.transform.position = spawnPosition + Vector3.right * partyMemberSpacing + Vector3.up * player02YOffset;
                player02Instance.SetActive(true);
                ResetMotion(player02Instance);
            }
        }
        else if (player02Instance != null)
        {
            player02Instance.SetActive(false);
        }

        Player2Bootstrap bootstrap = FindObjectOfType<Player2Bootstrap>();
        if (bootstrap != null)
        {
            bootstrap.SetPlayers(player01Instance, spawnPlayer02 ? player02Instance : null, player01Instance);
        }

        if (mapGeneration != null)
        {
            mapGeneration.SetPlayer(player01Instance != null ? player01Instance.GetComponentInChildren<PlayerMovement>() : null);
        }

        PlayerCameraRig cameraRig = FindObjectOfType<PlayerCameraRig>();
        if (cameraRig != null && player01Instance != null)
        {
            cameraRig.playerSlot = player01Instance.transform;
        }

        Debug.Log($"[PlayerSpawnManager] Spawned party at cell={spawnCoord} world={spawnPosition}", this);
    }

    private GameObject EnsurePlayerInstance(GameObject instance, GameObject prefab, string fallbackName)
    {
        if (prefab == null)
        {
            return null;
        }

        if (instance == null)
        {
            instance = Instantiate(prefab);
            instance.name = fallbackName;
            instance.transform.SetParent(transform, true);
        }

        instance.SetActive(true);
        return instance;
    }

    private static void ResetMotion(GameObject player)
    {
        if (player == null)
        {
            return;
        }

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        PlayerMovement movement = player.GetComponentInChildren<PlayerMovement>();
        if (movement != null && movement.rb != null)
        {
            movement.rb.linearVelocity = Vector3.zero;
            movement.rb.angularVelocity = Vector3.zero;
        }
    }

    private bool IsSpawnBlocked(Vector3 spawnPosition)
    {
        Vector3 probeCenter = spawnPosition + Vector3.up * Mathf.Max(0f, spawnCheckHeight);
        Collider[] hits = Physics.OverlapSphere(probeCenter, Mathf.Max(0.01f, spawnCheckRadius), spawnBlockerLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            if (player01Instance != null && hit.transform.IsChildOf(player01Instance.transform))
            {
                continue;
            }

            if (player02Instance != null && hit.transform.IsChildOf(player02Instance.transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
