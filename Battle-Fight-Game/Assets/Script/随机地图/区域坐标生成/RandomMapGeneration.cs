using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnderTheStars.GenerationMap//统一化脚本前缀，方便管理和查找
{
    public class RandomMapGeneration : MonoBehaviour
    {
        [Header("地图种子")]
        public int mapSeed;
        [Header("地图大小")]
        public int mapSize;
        [Header("地图迭代次数")]
        public int maplterations;

        [Header("地图绘制")]
        [SerializeField] private RandomMapPaintTilemap paintTilemap;
        [SerializeField] private RandomMapPainProp paintProp;

        [Header("区域大小与范围")]
        [SerializeField] private Vector2Int regionSize;
        [SerializeField] private Vector2Int regionArea;

        private HashSet<Vector2Int>[,] floorPoints;
        private HashSet<Vector2Int>[,] propsPoints;
        private HashSet<Vector2Int>[,] farthestCorridor;
        private HashSet<Vector2Int>[,] wallColliderPoints;
        private HashSet<Vector2Int>[,] wallDecorationPoints;

        private void Start()
        {
            GenerationMap();//生成地图
        }

        public void GenerationMap()
        {
            ResetMapData();

            var regionPoints = InitMapRegion();
            var checkAllFloor = GeneraterFloorPoints(regionPoints);
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

                    floorPoints[i, j] = RandomMapGenerationAlgorithms.GeneraterFloorPoints(regionPoints[i, j], checkFloor, maplterations, mapSize);
                }
            }
        }
        #endregion

       

        #region 初始化
        /// <summary> 重置地图区域 </summary>
        private BoundsInt[,] InitMapRegion()
        {
            return RandomMapGenerationAlgorithms.GeneraterRegionPoints(regionSize.x, regionSize.y, regionArea.x, regionArea.y);
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
            farthestCorridor = null;
            wallColliderPoints = null;
            wallDecorationPoints = null;
            propsPoints = null;
        }

        /// <summary> 初始化地图随机种子 </summary>
        private void InitMapSeed()
        {
            if (mapSeed == 0)
            {
                UnityEngine.Random.InitState(UnityEngine.Random.Range(-100000,100000));
                return;
            }
            UnityEngine.Random.InitState(mapSeed);
        }
        #endregion
    }
}
