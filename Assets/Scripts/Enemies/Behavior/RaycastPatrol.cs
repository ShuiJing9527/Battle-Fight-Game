using UnityEngine;
using System.Collections;

public class RandomTranslateNoDownPatrol : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 1f;
    public float directionChangeInterval = 3f; // 每隔几秒随机换个方向

    [Header("碰撞检测设置")]
    public float wallCheckDistance = 1.0f;     // 探测墙壁的距离
    public LayerMask obstacleLayer;            // 障碍物图层

    private Vector3 currentMovementDirection;  // 当前的移动方向向量
    private bool isChangingDirection = false;

    void Start()
    {
        // 游戏开始时先随机选择一个初始方向
        PickRandomDirection();
        // 开启定时随机换方向的协程
        StartCoroutine(DirectionTimer());
    }

    void Update()
    {
        // 1. 沿着当前方向在世界坐标系下平移（完全不改变物体的 rotation）
        transform.Translate(currentMovementDirection * moveSpeed * Time.deltaTime, Space.World);

        // 2. 实时检测当前移动方向上是否撞墙
        CheckWallInDirection();
    }

    // 随机选择一个移动方向（前后左右）
    void PickRandomDirection()
    {
        int randomIndex = Random.Range(0, 4);
        switch (randomIndex)
        {
            case 0: currentMovementDirection = Vector3.forward; break;
            case 1: currentMovementDirection = Vector3.back; break;
            case 2: currentMovementDirection = Vector3.left; break;
            case 3: currentMovementDirection = Vector3.right; break;
        }
    }

    // 仅检测移动方向上的墙壁
    void CheckWallInDirection()
    {
        // 射线起点：敌人中心稍微往上一点（避免贴着地面射出导致误判）
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;

        // 在 Scene 窗口中绘制红色调试射线
        Debug.DrawRay(rayOrigin, currentMovementDirection * wallCheckDistance, Color.red);

        // 发射水平射线检测墙壁
        bool hitWall = Physics.Raycast(rayOrigin, currentMovementDirection, wallCheckDistance, obstacleLayer);

        // 如果撞墙，立刻强行换方向
        if (hitWall)
        {
            if (!isChangingDirection)
            {
                StartCoroutine(ForceChangeDirection());
            }
        }
    }

    // 触发碰撞时立刻换方向的缓冲协程
    IEnumerator ForceChangeDirection()
    {
        isChangingDirection = true;

        Vector3 oldDirection = currentMovementDirection;
        // 确保随机出来的新方向和刚刚撞墙的方向不一样
        while (currentMovementDirection == oldDirection)
        {
            PickRandomDirection();
        }

        // 稍微等待 0.2 秒的冷却时间，防止连续触发
        yield return new WaitForSeconds(0.2f);
        isChangingDirection = false;
    }

    // 定时器：哪怕没撞墙，走一段时间也随机换个方向，让路线更随机
    IEnumerator DirectionTimer()
    {
        while (true)
        {
            yield return new WaitForSeconds(directionChangeInterval);
            if (!isChangingDirection)
            {
                PickRandomDirection();
            }
        }
    }
}