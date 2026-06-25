using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AHD2TimeOfDay;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy")]
    public GameObject[] enemyPrefabs;
    public GameObject[] normalEnemyPrefabs;
    public GameObject[] eliteEnemyPrefabs;
    public GameObject[] bossEnemyPrefabs;
    public bool useRuntimeRankOverride = true;

    [Header("Quantity limit")]
    [Tooltip("Maximum concurrent enemies allowed for this spawner in the scene")]
    public int maxEnemyCount = 5;
    private int currentEnemyCount = 0;

    [Header("Spawn time")]
    public float spawnInterval = 3f;
    public float startDelay = 2f;
    public float eliteSpawnInterval = 30f;

    [Header("Spawn Around Player")]
    public bool spawnAroundPlayer = true;
    public float spawnMinDistance = 6f;
    public float spawnMaxDistance = 12f;
    public float fallbackSpawnRadiusX = 10f;
    public float fallbackSpawnRadiusZ = 10f;

    [Header("Target")]
    public Transform playerTarget;
    public string playerTag = "Player";

    private Player2Bootstrap playerBootstrap;
    private TODController todController;
    private float previousTodTime;
    private bool todTimeInitialized;
    private readonly List<GameObject> fallbackNormalEnemyPrefabs = new List<GameObject>();
    private readonly List<GameObject> fallbackEliteEnemyPrefabs = new List<GameObject>();
    private readonly List<GameObject> fallbackBossEnemyPrefabs = new List<GameObject>();

    private void Start()
    {
        CachePrefabPools();
        ResolvePlayerTarget();
        InitializeTodTracking();
        StartCoroutine(NormalSpawnRoutine());
        StartCoroutine(EliteSpawnRoutine());
    }

    private void Update()
    {
        ResolvePlayerTarget();
        CheckBossSpawnAtMidnight();
    }

    private IEnumerator NormalSpawnRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            ResolvePlayerTarget();

            if (currentEnemyCount < maxEnemyCount)
            {
                SpawnNormalEnemy();
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private IEnumerator EliteSpawnRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, eliteSpawnInterval));

        while (true)
        {
            ResolvePlayerTarget();

            if (currentEnemyCount < maxEnemyCount)
            {
                SpawnEliteEnemy();
            }

            yield return new WaitForSeconds(Mathf.Max(0.1f, eliteSpawnInterval));
        }
    }

    private void SpawnNormalEnemy()
    {
        SpawnFromPool(ResolvePool(normalEnemyPrefabs, fallbackNormalEnemyPrefabs), MonsterRank.Normal);
    }

    private void SpawnEliteEnemy()
    {
        SpawnFromPool(ResolvePool(eliteEnemyPrefabs, fallbackEliteEnemyPrefabs), MonsterRank.Elite);
    }

    private void SpawnBossEnemy()
    {
        SpawnFromPool(ResolvePool(bossEnemyPrefabs, fallbackBossEnemyPrefabs), MonsterRank.Boss);
    }

    private void SpawnFromPool(List<GameObject> sourcePool, MonsterRank forcedRank)
    {
        if (sourcePool == null || sourcePool.Count == 0)
        {
            return;
        }

        int randomIndex = Random.Range(0, sourcePool.Count);
        GameObject selectedEnemy = sourcePool[randomIndex];
        if (selectedEnemy == null)
        {
            return;
        }

        MonsterIdentity prefabIdentity = selectedEnemy.GetComponent<MonsterIdentity>();
        MonsterSpecies? runtimeSpecies = prefabIdentity != null ? prefabIdentity.species : (MonsterSpecies?)null;
        MonsterRank runtimeRank = forcedRank;

        Vector3 spawnPosition = ResolveSpawnPosition(selectedEnemy);
        GameObject spawnedEnemy = Instantiate(selectedEnemy, spawnPosition, Quaternion.identity);
        MonsterIdentity cloneIdentity = spawnedEnemy.GetComponent<MonsterIdentity>();
        if (cloneIdentity == null)
        {
            cloneIdentity = spawnedEnemy.AddComponent<MonsterIdentity>();
        }

        if (runtimeSpecies.HasValue)
        {
            cloneIdentity.species = runtimeSpecies.Value;
        }

        cloneIdentity.rank = runtimeRank;

        MonsterCombatAutoSetup.Configure(spawnedEnemy, runtimeSpecies, runtimeRank);

        currentEnemyCount++;

        EnemyDeathNotifier notifier = spawnedEnemy.GetComponent<EnemyDeathNotifier>();
        if (notifier == null)
        {
            notifier = spawnedEnemy.AddComponent<EnemyDeathNotifier>();
        }
        notifier.Initialize(this);

        EnemyController enemyController = spawnedEnemy.GetComponent<EnemyController>();
        if (enemyController != null)
        {
            enemyController.SetTarget(ResolveActivePlayerTarget());
        }
    }

    private Vector3 ResolveSpawnPosition(GameObject selectedEnemyPrefab)
    {
        Transform activePlayer = ResolveActivePlayerTarget();
        if (spawnAroundPlayer && activePlayer != null)
        {
            float minDistance = Mathf.Max(0f, Mathf.Min(spawnMinDistance, spawnMaxDistance));
            float maxDistance = Mathf.Max(minDistance + 0.1f, Mathf.Max(spawnMinDistance, spawnMaxDistance));
            Vector2 offset2D = Random.insideUnitCircle.normalized * Random.Range(minDistance, maxDistance);
            if (offset2D.sqrMagnitude < 0.0001f)
            {
                offset2D = Vector2.right * minDistance;
            }

            Vector3 spawnPosition = activePlayer.position + new Vector3(offset2D.x, 0f, offset2D.y);
            if (selectedEnemyPrefab != null)
            {
                spawnPosition.y = selectedEnemyPrefab.transform.position.y;
            }
            return spawnPosition;
        }

        Vector3 fallbackPosition = transform.position;
        float randomX = Random.Range(-fallbackSpawnRadiusX, fallbackSpawnRadiusX);
        float randomZ = Random.Range(-fallbackSpawnRadiusZ, fallbackSpawnRadiusZ);
        fallbackPosition += new Vector3(randomX, 0f, randomZ);
        return fallbackPosition;
    }

    private void ResolvePlayerTarget()
    {
        if (playerBootstrap == null)
        {
            playerBootstrap = FindObjectOfType<Player2Bootstrap>();
            if (playerBootstrap != null)
            {
                playerBootstrap.EnsureInitializedForSpawn();
            }
        }

        Transform activePlayer = ResolveActivePlayerTarget();
        if (activePlayer != null)
        {
            playerTarget = activePlayer;
            return;
        }

        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject playerObject = GameObject.FindWithTag(playerTag);
            if (playerObject != null)
            {
                playerTarget = playerObject.transform;
            }
        }
    }

    private Transform ResolveActivePlayerTarget()
    {
        if (playerBootstrap != null && playerBootstrap.CurrentPlayerTransform != null)
        {
            return playerBootstrap.CurrentPlayerTransform;
        }

        return playerTarget;
    }

    private void CachePrefabPools()
    {
        fallbackNormalEnemyPrefabs.Clear();
        fallbackEliteEnemyPrefabs.Clear();
        fallbackBossEnemyPrefabs.Clear();

        if (enemyPrefabs == null)
        {
            return;
        }

        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            GameObject prefab = enemyPrefabs[i];
            if (prefab == null)
            {
                continue;
            }

            MonsterIdentity identity = prefab.GetComponent<MonsterIdentity>();
            MonsterRank rank = identity != null ? identity.rank : MonsterRank.Normal;
            switch (rank)
            {
                case MonsterRank.Boss:
                    fallbackBossEnemyPrefabs.Add(prefab);
                    break;
                case MonsterRank.Elite:
                    fallbackEliteEnemyPrefabs.Add(prefab);
                    break;
                default:
                    fallbackNormalEnemyPrefabs.Add(prefab);
                    break;
            }
        }
    }

    private static List<GameObject> ResolvePool(GameObject[] primaryPool, List<GameObject> fallbackPool)
    {
        List<GameObject> resolved = new List<GameObject>();
        if (primaryPool != null)
        {
            for (int i = 0; i < primaryPool.Length; i++)
            {
                if (primaryPool[i] != null)
                {
                    resolved.Add(primaryPool[i]);
                }
            }
        }

        return resolved.Count > 0 ? resolved : fallbackPool;
    }

    private void InitializeTodTracking()
    {
        todController = FindObjectOfType<TODController>();
        if (todController != null && todController.todGlobalParameters != null)
        {
            previousTodTime = todController.todGlobalParameters.CurrentTime;
            todTimeInitialized = true;
        }
    }

    private void CheckBossSpawnAtMidnight()
    {
        if (todController == null)
        {
            todController = FindObjectOfType<TODController>();
        }

        if (todController == null || todController.todGlobalParameters == null)
        {
            return;
        }

        float currentTodTime = todController.todGlobalParameters.CurrentTime;
        if (!todTimeInitialized)
        {
            previousTodTime = currentTodTime;
            todTimeInitialized = true;
            return;
        }

        bool crossedMidnight = currentTodTime < previousTodTime;
        previousTodTime = currentTodTime;

        if (!crossedMidnight || currentEnemyCount >= maxEnemyCount)
        {
            return;
        }

        SpawnBossEnemy();
    }

    public void OnEnemyDestroyed()
    {
        currentEnemyCount--;
        if (currentEnemyCount < 0)
        {
            currentEnemyCount = 0;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        if (spawnAroundPlayer && playerTarget != null)
        {
            Gizmos.DrawWireSphere(playerTarget.position, spawnMinDistance);
            Gizmos.DrawWireSphere(playerTarget.position, spawnMaxDistance);
        }
        else
        {
            Gizmos.DrawWireCube(transform.position, new Vector3(fallbackSpawnRadiusX * 2f, 1f, fallbackSpawnRadiusZ * 2f));
        }
    }
}
