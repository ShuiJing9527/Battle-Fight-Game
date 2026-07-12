using UnityEngine;

public class EnemyDeathNotifier : MonoBehaviour
{
    private EnemySpawner spawner;
    public bool HasSpawner => spawner != null;

    public void Initialize(EnemySpawner creator)
    {
        spawner = creator;
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.OnEnemyDestroyed(gameObject);
        }
    }
}
