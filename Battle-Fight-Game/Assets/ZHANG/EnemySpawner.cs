using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("敌人配置")]
    public GameObject[] enemyPrefabs;

    [Header("数量限制")]
    [Tooltip("场景中允许该生成器制造的敌人最大同时存在数量")]
    public int maxEnemyCount = 2;
    // 当前存活的敌人数量
    private int currentEnemyCount = 0;

    [Header("生成时间控制")]
    public float spawnInterval = 3f;
    public float startDelay = 2f;

    [Header("生成范围控制")]
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
            // 关键判断：如果当前敌人数量已经达到或超过上限，就跳过本次生成，等待下一次循环检测
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

        // 确定生成位置
        Vector3 spawnPosition = transform.position;
        if (spawnInArea)
        {
            float randomX = Random.Range(-spawnRadiusX, spawnRadiusX);
            float randomZ = Random.Range(-spawnRadiusZ, spawnRadiusZ);
            spawnPosition += new Vector3(randomX, 0, randomZ);
        }

        // 随机选择预制体并生成
        int randomIndex = Random.Range(0, enemyPrefabs.Length);
        GameObject selectedEnemy = enemyPrefabs[randomIndex];

        GameObject spawnedEnemy = Instantiate(selectedEnemy, spawnPosition, Quaternion.identity);

        // 【核心逻辑】
        // 1. 生成数量 +1
        currentEnemyCount++;

        // 2. 给生成的敌人动态挂载一个“死亡监听脚本”，并把当前生成器传给它
        EnemyDeathNotifier notifier = spawnedEnemy.AddComponent<EnemyDeathNotifier>();
        notifier.Initialize(this);
    }

    // 提供给敌人死亡时调用的公共方法
    public void OnEnemyDestroyed()
    {
        currentEnemyCount--;
        // 防止数据意外变成负数
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