using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderTheStars.GenerationMap//统一化脚本前缀，方便管理和查找
{
    public class RandomMapGenerationAlgorithms
    {
        public static BoundsInt[,] GeneraterRegionPoints(int regionSizeX, int regionSizeY, int regionWidth, int regionHeight)
        {
            BoundsInt[,] regionPoints = new BoundsInt[regionSizeX, regionSizeY];//初始化区域坐标范围二维数组，用于存储每个区域的坐标范围

            for (int i = 0; i < regionPoints.GetLength(0); i ++)
            {
                for (int j = 0; j < regionPoints.GetLength(1); j ++)
                {
                    regionPoints[i, j] = new BoundsInt(new Vector3Int(i * regionWidth, j * regionHeight), new Vector3Int(regionWidth, regionHeight));
                }
            }
            return regionPoints;
        }
        
        internal static HashSet<Vector2Int> GeneraterFloorPoints(BoundsInt regionPoints, HashSet<Vector2Int> checkAllFloor, int maplterations, int mapSize)
        {
            int count = 0;
            int randomDir = 0;
            int maxSize = maplterations * mapSize;
            HashSet<Vector2Int> points = new HashSet<Vector2Int>();
            List<Vector2Int> tempPoints = new List<Vector2Int>();

            var center = regionPoints.center;
            Vector2Int currentPoint = new Vector2Int(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y));
            System.Random random = new System.Random();

            for (int i = 0; i < maplterations; i ++)
            {
                while (count < mapSize)
                {
                    var currentDir = random.Next(0, 4);
                    if (currentDir != randomDir)
                    {
                        randomDir = currentDir;
                        currentPoint += GetDir(randomDir);
                        if (!checkAllFloor.Contains(currentPoint) && !points.Contains(currentPoint))
                        {
                            checkAllFloor.Add(currentPoint);
                            points.Add(currentPoint);
                            tempPoints.Add(currentPoint);
                            if (points.Count >= maxSize)
                            {
                                SpreadFloorPoints(points, checkAllFloor, tempPoints);
                                return points;
                            }
                            count++;
                        }
                    }
                }
                count = 0;
                currentPoint = tempPoints[random.Next(0, points.Count)];
            }
            SpreadFloorPoints(points, checkAllFloor, tempPoints);
            return points;
        }

        /// <summary> 平滑坐标 </summary>
        private static void SpreadFloorPoints(HashSet<Vector2Int> points, HashSet<Vector2Int> checkAllFloor, List<Vector2Int> tempPoints)
        {
            HashSet<Vector2Int> tPoint = new HashSet<Vector2Int>();
            void AddifNew(Vector2Int newPoint)
            {
                if (!points.Contains(newPoint) && !checkAllFloor.Contains(newPoint))
                {
                    tempPoints.Add(newPoint);
                    checkAllFloor.Add(newPoint);
                    tPoint.Add(newPoint);
                }
            }

            foreach (var point in points)
            {
                AddifNew(point + GetDir(0));   //左
                AddifNew(point + 2 * GetDir(0));
                AddifNew(point + GetDir(1));   //右
                AddifNew(point + GetDir(2));   //上
                AddifNew(point + 2 * GetDir(2));
                AddifNew(point + GetDir(3));   //下
            }
        }

        #region 地图生成方向

        private enum EightDir
        {
            left,
            Right,
            Top,
            Bottom,
            LeftTop,
            RightTop,
            LeftBottom,
            RightBottom,
        }

        private static Vector2Int GetDir(int dir)
        {
            return
              dir == 0 ? new Vector2Int(-1, 0):
              dir == 1 ? new Vector2Int(1, 0):
              dir == 2 ? new Vector2Int(0, 1):
              dir == 3 ? new Vector2Int(0, -1):
              dir == 4 ? new Vector2Int(-1, 1):
              dir == 5 ? new Vector2Int(1, 1):
              dir == 6 ? new Vector2Int(-1, -1):
            new Vector2Int(1, -1);
        }
        #endregion


    }
}