using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnderTheStars.GenerationMap
{
    public class RandomMapGeneration : MonoBehaviour
    {
        public int mapSeed;
        public int mapSize;
        public int maplterations;

        [SerializeField] private RandomMapPaintTilemap paintTilemap;
        [SerializeField] private RandomMapPainProp paintProp;

        [SerializeField] private Vector2Int regionSize;
        [SerializeField] private Vector2Int regionArea;

        private HashSet<Vector2Int>[,] floorPoints;
        private HashSet<Vector2Int>[,] propsPoints;
        private HashSet<Vector2Int>[,] farthestCorridor;
        private HashSet<Vector2Int>[,] wallColliderPoints;
        private HashSet<Vector2Int>[,] wallDecorationPoints;

        private void Start()
        {
            GenerationMap();
        }

        private void GenerationMap()
        {
            ResetMapData();

            var regionPoints = InitMapRegion();
        }

        /// <summary> 重置地图数据 </summary>
        private void ResetMapData()
        {
            InitMapSeed();
            InitMapData();
            InitMapPaint();
            GC.Collect();
        }

        #region 初始化
        /// <summary> 重置地图区域 </summary>
        private BoundsInt[,] InitMapRegion()
        {
            return RandomMapGenerationAlgorithms.GeneraterRegionPoints(regionSize.x, regionSize.y, regionArea.x, regionArea.y);
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
