using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy")]
    public GameObject[] enemyPrefabs;

    [Header("Quantity limit")]
    [Tooltip("Maximum concurrent enemies allowed for this spawner in the scene")]
    public int maxEnemyCount = 2;
    private int currentEnemyCount = 0;

    [Header("Spawn time")]
    public float spawnInterval = 3f;
    public float startDelay = 2f;

    [Header("Spawn range")]
    public bool spawnInArea = true;
    public float spawnRadiusX = 10f;
    public float spawnRadiusZ = 10f;

    private void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            if (currentEnemyCount < maxEnemyCount)
            {
                SpawnEnemy();
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return;

        Vector3 spawnPosition = transform.position;
        if (spawnInArea)
        {
            float randomX = Random.Range(-spawnRadiusX, spawnRadiusX);
            float randomZ = Random.Range(-spawnRadiusZ, spawnRadiusZ);
            spawnPosition += new Vector3(randomX, 0, randomZ);
        }

        int randomIndex = Random.Range(0, enemyPrefabs.Length);
        GameObject selectedEnemy = enemyPrefabs[randomIndex];

        GameObject spawnedEnemy = Instantiate(selectedEnemy, spawnPosition, Quaternion.identity);

        currentEnemyCount++;

        EnemyDeathNotifier notifier = spawnedEnemy.AddComponent<EnemyDeathNotifier>();
        notifier.Initialize(this);
    }

    public void OnEnemyDestroyed()
    {
        currentEnemyCount--;
        if (currentEnemyCount < 0) currentEnemyCount = 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnInArea)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3(spawnRadiusX * 2, 1f, spawnRadiusZ * 2));
        }
    }
}