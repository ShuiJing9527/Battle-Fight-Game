using UnityEngine;
using UnderTheStars.GenerationMap;

[DisallowMultipleComponent]
public class PlayerSpawnManager : MonoBehaviour
{
    private enum InitialSpawnPhase
    {
        Day,
        Night
    }

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
    private bool player01InstanceOwned;
    private bool player02InstanceOwned;
    private bool spawnRetryQueued;

    public int ClearSpawnedPlayers()
    {
        int clearedCount = 0;
        clearedCount += ClearSpawnedPlayer(ref player01Instance, ref player01InstanceOwned);
        clearedCount += ClearSpawnedPlayer(ref player02Instance, ref player02InstanceOwned);

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

        if (!TryResolveInitialSpawnPhase(out InitialSpawnPhase spawnPhase))
        {
            if (!spawnRetryQueued && isActiveAndEnabled)
            {
                StartCoroutine(RetrySpawnWhenDayNightReady(mapGeneration));
            }

            return true;
        }

        if (!TryResolveCharacterSelection(
                spawnPhase,
                out GameObject initialActivePrefab,
                out GameObject inactivePrefab,
                out string selectionError))
        {
            Debug.LogError(
                $"[PlayerSpawn] generationId={ResolveGenerationId(mapGeneration)} isPlaying={Application.isPlaying} " +
                $"skipped=True reason={selectionError}",
                this);
            return true;
        }

        for (int attempt = 0; attempt < Mathf.Max(1, spawnSearchAttempts); attempt++)
        {
            if (!TryGetSpawnPositionForPhase(mapGeneration, spawnPhase, out Vector3 spawnPosition, out Vector2Int spawnCoord))
            {
                break;
            }

            if (IsSpawnBlocked(spawnPosition))
            {
                continue;
            }

            SpawnPartyAtWorldPosition(
                mapGeneration,
                spawnPosition,
                spawnCoord,
                initialActivePrefab,
                inactivePrefab,
                spawnPhase);
            return true;
        }

        if (TryGetFallbackSpawnPositionForPhase(mapGeneration, spawnPhase, out Vector3 fallbackPosition, out Vector2Int fallbackCoord)
            && !IsSpawnBlocked(fallbackPosition))
        {
            SpawnPartyAtWorldPosition(
                mapGeneration,
                fallbackPosition,
                fallbackCoord,
                initialActivePrefab,
                inactivePrefab,
                spawnPhase);
            return true;
        }

        Debug.LogError(
            $"[PlayerSpawn] generationId={ResolveGenerationId(mapGeneration)} isPlaying={Application.isPlaying} " +
            $"skipped=True reason=no-safe-{spawnPhase.ToString().ToLowerInvariant()}-spawn",
            this);
        return true;
    }

    public void SpawnPartyAtWorldPosition(RandomMapGeneration mapGeneration, Vector3 spawnPosition, Vector2Int spawnCoord)
    {
        SpawnPartyAtWorldPosition(
            mapGeneration,
            spawnPosition,
            spawnCoord,
            player01Prefab,
            player02Prefab,
            InitialSpawnPhase.Night);
    }

    private void SpawnPartyAtWorldPosition(
        RandomMapGeneration mapGeneration,
        Vector3 spawnPosition,
        Vector2Int spawnCoord,
        GameObject initialActivePrefab,
        GameObject inactivePrefab,
        InitialSpawnPhase spawnPhase)
    {
        Debug.Log(
            $"[PlayerSpawn] generationId={ResolveGenerationId(mapGeneration)} isPlaying={Application.isPlaying} " +
            "action=spawn-world-position " +
            $"spawnCoord={spawnCoord} spawnPosition={spawnPosition} " +
            $"preExistingPlayer01Count={CountSceneObjectsNamed("Player01")} " +
            $"preExistingPlayer02Count={CountSceneObjectsNamed("Player02")}",
            this);

        if (player01Prefab == null || player02Prefab == null)
        {
            Debug.LogError(
                $"[PlayerSpawn] generationId=-1 isPlaying={Application.isPlaying} skipped=True reason=missing-player-prefab " +
                $"player01Prefab={(player01Prefab != null ? player01Prefab.name : "null")} " +
                $"player02Prefab={(player02Prefab != null ? player02Prefab.name : "null")}",
                this);
            return;
        }

        player01Instance = EnsurePlayerInstance(player01Instance, ref player01InstanceOwned, player01Prefab, "Player01");
        if (player01Instance != null)
        {
            player01Instance.transform.position = spawnPosition + Vector3.up * player01YOffset;
            ResetMotion(player01Instance);
        }

        player02Instance = EnsurePlayerInstance(player02Instance, ref player02InstanceOwned, player02Prefab, "Player02");
        if (player02Instance != null)
        {
            player02Instance.transform.position = spawnPosition + Vector3.up * player02YOffset;
            ResetMotion(player02Instance);
        }

        if (!spawnPlayer02)
        {
            Debug.LogWarning(
                "[PlayerSpawn] spawnPlayer02 is disabled, but twin shift gameplay expects both player prefabs to be present. " +
                "The inactive counterpart will still be initialized for switching.",
                this);
        }

        GameObject activeInstance = ResolveInstanceForPrefab(initialActivePrefab);
        GameObject inactiveInstance = ResolveInstanceForPrefab(inactivePrefab);
        if (activeInstance == null || inactiveInstance == null)
        {
            Debug.LogError(
                $"[PlayerSpawn] generationId={ResolveGenerationId(mapGeneration)} isPlaying={Application.isPlaying} " +
                $"skipped=True reason=missing-runtime-instance phase={spawnPhase}",
                this);
            return;
        }

        Player2Bootstrap bootstrap = FindObjectOfType<Player2Bootstrap>();
        if (bootstrap != null)
        {
            bootstrap.SetPlayers(
                player01Instance,
                player02Instance,
                activeInstance,
                activeInstance);
        }
        else
        {
            Debug.LogWarning("[PlayerSpawn] Player2Bootstrap not found. Camera/UI/current-player binding will rely on scene fallbacks.", this);
            activeInstance.SetActive(true);
            inactiveInstance.SetActive(false);
        }

        if (mapGeneration != null)
        {
            mapGeneration.SetPlayer(activeInstance.GetComponentInChildren<PlayerMovement>());
        }

        PlayerCameraRig cameraRig = FindObjectOfType<PlayerCameraRig>();
        if (cameraRig != null)
        {
            cameraRig.playerSlot = activeInstance.transform;
        }

        Debug.Log(
            $"[PlayerSpawn] generationId={ResolveGenerationId(mapGeneration)} isPlaying={Application.isPlaying} " +
            $"spawned=True phase={spawnPhase} activePlayer={activeInstance.name} inactivePlayer={inactiveInstance.name} " +
            $"spawnCoord={spawnCoord} spawnPosition={spawnPosition}",
            this);

        RuntimeRuneScaling.ForceRefresh($"{nameof(PlayerSpawnManager)}.{nameof(SpawnPartyAtWorldPosition)}:{spawnPhase}");
    }

    private GameObject EnsurePlayerInstance(GameObject instance, ref bool isOwnedInstance, GameObject prefab, string fallbackName)
    {
        if (prefab == null)
        {
            return null;
        }

        if (instance == null)
        {
            instance = FindSceneObjectByNameIncludingInactive(fallbackName);
            isOwnedInstance = false;
        }

        if (instance == null)
        {
            instance = Instantiate(prefab);
            instance.name = fallbackName;
            instance.transform.SetParent(transform, true);
            isOwnedInstance = true;
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
            Vector3 velocityBeforeWrite = rb.linearVelocity;
            Vector3 velocityAfterWrite = Vector3.zero;
            rb.linearVelocity = velocityAfterWrite;
            PlayerMovement.LogVelocityWrite(
                player != null ? player.GetComponent<PlayerMovement>() : null,
                nameof(PlayerSpawnManager),
                nameof(ResetMotion),
                rb,
                velocityBeforeWrite,
                velocityAfterWrite,
                "spawn-reset-motion-root-rigidbody",
                "none",
                "none",
                "spawn-reset");
            rb.angularVelocity = Vector3.zero;
        }

        PlayerMovement movement = player.GetComponentInChildren<PlayerMovement>();
        if (movement != null && movement.rb != null)
        {
            Vector3 velocityBeforeWrite = movement.rb.linearVelocity;
            Vector3 velocityAfterWrite = Vector3.zero;
            movement.rb.linearVelocity = velocityAfterWrite;
            PlayerMovement.LogVelocityWrite(
                movement,
                nameof(PlayerSpawnManager),
                nameof(ResetMotion),
                movement.rb,
                velocityBeforeWrite,
                velocityAfterWrite,
                "spawn-reset-motion-player-movement-rigidbody",
                "none",
                "none",
                "spawn-reset");
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

    private bool TryResolveInitialSpawnPhase(out InitialSpawnPhase spawnPhase)
    {
        spawnPhase = InitialSpawnPhase.Night;
        if (TODDayNightAdapter.TryGetIsDay(out bool isDay) && TODDayNightAdapter.TryGetIsNight(out bool isNight))
        {
            if (isDay == isNight)
            {
                Debug.LogError(
                    $"[PlayerSpawn] Invalid day/night state. isDay={isDay} isNight={isNight} phase={TODDayNightAdapter.GetDebugPhaseName()}",
                    this);
                return false;
            }

            spawnPhase = isDay ? InitialSpawnPhase.Day : InitialSpawnPhase.Night;
            return true;
        }

        Debug.LogWarning(
            "[PlayerSpawn] Day/night state is not ready yet. Spawn will wait for the existing TOD system to initialize.",
            this);
        return false;
    }

    private bool TryResolveCharacterSelection(
        InitialSpawnPhase spawnPhase,
        out GameObject initialActivePrefab,
        out GameObject inactivePrefab,
        out string errorReason)
    {
        initialActivePrefab = null;
        inactivePrefab = null;
        errorReason = string.Empty;

        PlayerDayNightAffinityType requiredActiveAffinity =
            spawnPhase == InitialSpawnPhase.Day ? PlayerDayNightAffinityType.DayChild : PlayerDayNightAffinityType.NightChild;
        PlayerDayNightAffinityType requiredInactiveAffinity =
            spawnPhase == InitialSpawnPhase.Day ? PlayerDayNightAffinityType.NightChild : PlayerDayNightAffinityType.DayChild;

        initialActivePrefab = ResolvePrefabByAffinity(requiredActiveAffinity);
        inactivePrefab = ResolvePrefabByAffinity(requiredInactiveAffinity);

        if (initialActivePrefab == null)
        {
            errorReason = $"missing-{requiredActiveAffinity}-prefab";
            return false;
        }

        if (inactivePrefab == null)
        {
            errorReason = $"missing-{requiredInactiveAffinity}-prefab";
            return false;
        }

        return true;
    }

    private GameObject ResolvePrefabByAffinity(PlayerDayNightAffinityType affinityType)
    {
        GameObject[] prefabs = { player01Prefab, player02Prefab };
        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab == null)
            {
                continue;
            }

            PlayerDayNightAffinity affinity = prefab.GetComponent<PlayerDayNightAffinity>();
            if (affinity == null)
            {
                affinity = prefab.GetComponentInChildren<PlayerDayNightAffinity>(true);
            }

            if (affinity != null && affinity.AffinityType == affinityType)
            {
                return prefab;
            }
        }

        return null;
    }

    private GameObject ResolveInstanceForPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        if (player01Prefab == prefab)
        {
            return player01Instance;
        }

        if (player02Prefab == prefab)
        {
            return player02Instance;
        }

        return null;
    }

    private bool TryGetSpawnPositionForPhase(
        RandomMapGeneration mapGeneration,
        InitialSpawnPhase spawnPhase,
        out Vector3 spawnPosition,
        out Vector2Int spawnCoord)
    {
        if (mapGeneration == null)
        {
            spawnPosition = Vector3.zero;
            spawnCoord = Vector2Int.zero;
            return false;
        }

        return spawnPhase == InitialSpawnPhase.Day
            ? mapGeneration.TryGetRandomSafeSpawnWorldPositionForArea(AreaType.Grass, out spawnPosition, out spawnCoord)
            : mapGeneration.TryGetRandomSafeSpawnWorldPositionForArea(AreaType.Forest, out spawnPosition, out spawnCoord);
    }

    private bool TryGetFallbackSpawnPositionForPhase(
        RandomMapGeneration mapGeneration,
        InitialSpawnPhase spawnPhase,
        out Vector3 spawnPosition,
        out Vector2Int spawnCoord)
    {
        if (mapGeneration == null)
        {
            spawnPosition = Vector3.zero;
            spawnCoord = Vector2Int.zero;
            return false;
        }

        return spawnPhase == InitialSpawnPhase.Day
            ? mapGeneration.TryGetFallbackSafeSpawnWorldPositionForArea(AreaType.Grass, out spawnPosition, out spawnCoord)
            : mapGeneration.TryGetFallbackSafeSpawnWorldPositionForArea(AreaType.Forest, out spawnPosition, out spawnCoord);
    }

    private System.Collections.IEnumerator RetrySpawnWhenDayNightReady(RandomMapGeneration mapGeneration)
    {
        spawnRetryQueued = true;

        const int maxRetryFrames = 30;
        for (int frame = 0; frame < maxRetryFrames; frame++)
        {
            yield return null;
            if (TryResolveInitialSpawnPhase(out _))
            {
                spawnRetryQueued = false;
                SpawnPartyAtRandomSafePoint(mapGeneration);
                yield break;
            }
        }

        spawnRetryQueued = false;
        Debug.LogError(
            "[PlayerSpawn] Timed out waiting for the existing TOD day/night state. Player spawn was cancelled.",
            this);
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

    private int ClearSpawnedPlayer(ref GameObject instance, ref bool isOwnedInstance)
    {
        if (instance == null)
        {
            return 0;
        }

        GameObject target = instance;
        instance = null;
        bool shouldDestroy = isOwnedInstance;
        isOwnedInstance = false;

        if (!shouldDestroy)
        {
            target.SetActive(false);
            return 1;
        }

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
