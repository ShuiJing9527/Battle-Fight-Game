using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy")]
    public GameObject[] enemyPrefabs;

    [Header("Quantity limit")]
    [Tooltip("Maximum concurrent enemies allowed for this spawner in the scene")]
    public int maxEnemyCount = 5;
    private int currentEnemyCount = 0;

    [Header("Spawn time")]
    public float spawnInterval = 3f;
    public float startDelay = 2f;

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

    private void Start()
    {
        ResolvePlayerTarget();
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            ResolvePlayerTarget();

            if (currentEnemyCount < maxEnemyCount)
            {
                SpawnEnemy();
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            return;
        }

        Vector3 spawnPosition = ResolveSpawnPosition();
        int randomIndex = Random.Range(0, enemyPrefabs.Length);
        GameObject selectedEnemy = enemyPrefabs[randomIndex];
        GameObject spawnedEnemy = Instantiate(selectedEnemy, spawnPosition, Quaternion.identity);

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

    private Vector3 ResolveSpawnPosition()
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
            spawnPosition.y = transform.position.y;
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
