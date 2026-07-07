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

    public int ClearSpawnedPlayers()
    {
        int clearedCount = 0;
        clearedCount += ClearSpawnedPlayer(ref player01Instance);
        clearedCount += ClearSpawnedPlayer(ref player02Instance);

        Debug.Log(
            $"[PlayerSpawn] generationId=-1 isPlaying={Application.isPlaying} " +
            $"clearedRuntimePlayers={clearedCount}",
            this);

        return clearedCount;
    }

    public bool SpawnPartyAtRandomSafePoint()
    {
        RandomMapGeneration mapGeneration = FindObjectOfType<RandomMapGeneration>();
        return SpawnPartyAtRandomSafePoint(mapGeneration);
    }

    public bool SpawnPartyAtRandomSafePoint(RandomMapGeneration mapGeneration)
    {
        Debug.Log(
            $"[PlayerSpawn] generationId={ResolveGenerationId(mapGeneration)} isPlaying={Application.isPlaying} " +
            $"action=spawn-party prefab01={(player01Prefab != null ? player01Prefab.name : "null")} " +
            $"prefab02={(player02Prefab != null ? player02Prefab.name : "null")}",
            this);

        if (mapGeneration == null)
        {
            Debug.LogWarning(
                $"[PlayerSpawn] generationId=-1 isPlaying={Application.isPlaying} skipped=True reason=map-generation-not-found",
                this);
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

        Debug.LogWarning(
            $"[PlayerSpawn] generationId={ResolveGenerationId(mapGeneration)} isPlaying={Application.isPlaying} " +
            "skipped=True reason=no-safe-grass-spawn",
            this);
        return false;
    }

    public void SpawnPartyAtWorldPosition(RandomMapGeneration mapGeneration, Vector3 spawnPosition, Vector2Int spawnCoord)
    {
        Debug.Log(
            $"[PlayerSpawn] generationId={ResolveGenerationId(mapGeneration)} isPlaying={Application.isPlaying} " +
            "action=spawn-world-position " +
            $"spawnCoord={spawnCoord} spawnPosition={spawnPosition} " +
            $"preExistingPlayer01Count={CountSceneObjectsNamed("Player01")} " +
            $"preExistingPlayer02Count={CountSceneObjectsNamed("Player02")}",
            this);

        if (player01Prefab == null)
        {
            Debug.LogWarning(
                $"[PlayerSpawn] generationId=-1 isPlaying={Application.isPlaying} skipped=True reason=missing-player01-prefab",
                this);
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

        Debug.Log(
            $"[PlayerSpawn] generationId={ResolveGenerationId(mapGeneration)} isPlaying={Application.isPlaying} " +
            $"spawned=True spawnCoord={spawnCoord} spawnPosition={spawnPosition}",
            this);
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

    private static int CountSceneObjectsNamed(string exactName)
    {
        if (string.IsNullOrEmpty(exactName))
        {
            return 0;
        }

        int count = 0;
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject obj = objects[i];
            if (obj == null || !obj.scene.IsValid() || !obj.scene.isLoaded)
            {
                continue;
            }

            if (obj.name == exactName)
            {
                count++;
            }
        }

        return count;
    }

    private static int ResolveGenerationId(RandomMapGeneration mapGeneration)
    {
        return mapGeneration != null ? mapGeneration.GetCurrentGenerateMapDebugId() : -1;
    }

    private int ClearSpawnedPlayer(ref GameObject instance)
    {
        if (instance == null)
        {
            return 0;
        }

        GameObject target = instance;
        instance = null;

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }

        return 1;
    }
}
