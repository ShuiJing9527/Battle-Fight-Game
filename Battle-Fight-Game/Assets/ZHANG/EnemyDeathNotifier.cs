using UnityEngine;

public class EnemyDeathNotifier : MonoBehaviour
{
    private EnemySpawner spawner;

    // 初始化，记录是谁生出了自己
    public void Initialize(EnemySpawner creator)
    {
        spawner = creator;
    }

    // 当这个游戏物体被销毁（Destroy）时，Unity 会自动调用这个生命周期函数
    private void OnDestroy()
    {
        if (spawner != null)
        {
            // 通知生成器：我死了，释放一个名额
            spawner.OnEnemyDestroyed();
        }
    }
}