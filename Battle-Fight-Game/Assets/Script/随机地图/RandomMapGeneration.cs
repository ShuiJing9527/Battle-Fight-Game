using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace UnderTheStars.GenerationMap
{
    public class RandomMapGeneration : MonoBehaviour
    {
        [Header("地图种子")]
        public int mapSeed;//地图种子
        [Header("地图大小")]
        public int mapSize;//地图大小
        [Header("地图迭代次数")]
        public int maplterations;//地图迭代次数

        [Header("地图绘制")]
        [SerializeField] private RandomMapPaintTilemap paintTilemap;//绘制Tilemap组件
        [SerializeField] private RandomMapPainProp paintProp;//绘制道具组件

        [Header("区域大小与范围")]
        [SerializeField] private Vector2Int regionSize;//区域大小
        [SerializeField] private Vector2Int regionArea;//区域范围

        [Header("玩家设置")]
        [SerializeField] private PlayerMovement player; // 在 Inspector 里把你的 Player 拖进来

        private HashSet<Vector2Int>[,] floorPoints;//地面坐标点
        private HashSet<Vector2Int>[,] propsPoints;//道具坐标点
        private HashSet<Vector2Int> wallColliderPoints;//墙体碰撞坐标点


        private void Start()
        {
            GenerateMap();
        }

        public async void GenerateMap()
        {
            ResetMapData();

            var regionPoints = InitMapRegion();
            var checkAllFloor = GeneraterFloorPoints(regionPoints);
            var generateWallPointsTask = GeneraterWallPointsAsync(checkAllFloor);
            await UniTask.WhenAny(generateWallPointsTask);
            PanintWallTilemap().Forget();

            // 等待所有 Tilemap 绘制完成
            await UniTask.WhenAll(panintTilemap(0, 0), panintTilemap(0, 1), panintTilemap(1, 0));
            await UniTask.WhenAll(panintTilemap(1, 1), panintTilemap(2, 0), panintTilemap(2, 1));
            await UniTask.WhenAll(panintTilemap(0, 2), panintTilemap(1, 2), panintTilemap(2, 2));

            // 安置玩家
            PlacePlayerOnMap();
        }

        private UniTask PanintWallTilemap()
        {
            return paintTilemap.PaintWallTile(wallColliderPoints);
        }

        private async UniTask GeneraterWallPointsAsync(HashSet<Vector2Int> checkAllFloor)
        {
            wallColliderPoints = new HashSet<Vector2Int>();
            wallColliderPoints = RandomMapGenerationAlgorithms.GenraterWallPoints(checkAllFloor);
            await UniTask.NextFrame();
        }

        private UniTask panintTilemap(int v1, int v2)
        {
            int index = v1 * regionSize.y + v2;
            return paintTilemap.PaintFloorTile(floorPoints[v1, v2], index);
        }

        private void PlacePlayerOnMap()
        {
            if (player == null || floorPoints == null) return;

            // 获取参考 Tilemap
            Tilemap refTilemap = paintTilemap.GetFloorTilemap(0);
            if (refTilemap == null) return;

            Vector2Int spawnCoord = Vector2Int.zero;
            bool found = false;

            // 尝试找到一个合法的起始坐标
            if (floorPoints[0, 0] != null && floorPoints[0, 0].Count > 0)
            {
                foreach (var point in floorPoints[0, 0])
                {
                    spawnCoord = point;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                // 转换为格子坐标
                Vector3Int cellPos = new Vector3Int(spawnCoord.x, spawnCoord.y, 0);

                // 转换为世界坐标（自动处理 90 度旋转）
                Vector3 worldSpawnPos = refTilemap.GetCellCenterWorld(cellPos);

                // 执行传送
                // 记得使用 rb.linearVelocity 而不是 velocity (Unity 6 推荐)
                player.rb.linearVelocity = Vector3.zero;

                // 抬高 Y 轴以防掉出地图，因为 Tilemap 旋转 90 度后，平铺面在 XZ 平面
                // worldSpawnPos 已经包含了正确的 3D 位置
                player.transform.position = worldSpawnPos + Vector3.up * 1.0f;

                Debug.Log($"玩家已精准安置！格子:{cellPos} -> 世界:{worldSpawnPos}");
            }
        }

        #region 区域生成
        /// <summary> 生成地面坐标点 </summary>
        private HashSet<Vector2Int> GeneraterFloorPoints(BoundsInt[,] regionPoints)
        {
            floorPoints = new HashSet<Vector2Int>[regionSize.x, regionSize.y];
            propsPoints = new HashSet<Vector2Int>[regionSize.x, regionSize.y];

            Vector2Int[,] regionCenters = new Vector2Int[regionSize.x, regionSize.y];

            HashSet<Vector2Int> checkFloor = new HashSet<Vector2Int>();

            GeneraterFloorPoints(regionPoints, regionCenters, checkFloor);

            return checkFloor;
        }

        /// <summary> 生成区域坐标点 </summary>
        private void GeneraterFloorPoints(BoundsInt[,] regionPoints, Vector2Int[,] regionCenters, HashSet<Vector2Int> checkFloor)
        {
            for (int i = 0; i < regionPoints.GetLength(0); i++)
            {
                for (int j = 0; j < regionPoints.GetLength(1); j++)
                {
                    floorPoints[i, j] = new HashSet<Vector2Int>();
                    propsPoints[i, j] = new HashSet<Vector2Int>();

                    var region = regionPoints[i, j];
                    var center = region.center;

                    floorPoints[i, j] = RandomMapGenerationAlgorithms.GenraterFloorPoints(regionPoints[i, j], checkFloor, maplterations, mapSize);
                    propsPoints[i, j].UnionWith(floorPoints[i, j]);
                    regionCenters[i, j] = (Vector2Int)Vector3Int.RoundToInt(center);
                }
            }
        }
        #endregion

        #region 初始化
        /// <summary> 重置地图区域 </summary>
        private BoundsInt[,] InitMapRegion()
        {
            return RandomMapGenerationAlgorithms.GenraterRegionPoints(regionSize.x, regionSize.y, regionArea.x, regionArea.y);
        }

        /// <summary> 重置地图数据 </summary>
        public void ResetMapData()
        {
            InitMapSeed();
            InitMapData();
            InitMapPaint();
            GC.Collect();
        }

        /// <summary> 初始化地图绘制 </summary>
        private void InitMapPaint()
        {
            paintTilemap.InitClearTile();
            paintProp.InitClearProp();
        }

        /// <summary> 初始化地图坐标数据 </summary>
        private void InitMapData()
        {
            floorPoints = null;
            propsPoints = null;
            wallColliderPoints = null;
        }

        /// <summary> 初始化地图随机种 </summary>
        private void InitMapSeed()
        {
            if (mapSeed == 0)
            {
                UnityEngine.Random.InitState(UnityEngine.Random.Range(-100000, 100000));
                return;
            }
            UnityEngine.Random.InitState(mapSeed);
        }
        #endregion
    }
}
