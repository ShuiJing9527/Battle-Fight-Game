using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace UnderTheStars.GenerationMap
{
    public class RandomMapGenerationAlgorithms
    {
        public static BoundsInt[,] GenraterRegionPoints(int regionSizeX, int regionSizeY, int regionWidth, int regionHeight)
        {
            BoundsInt[,] regionPoints = new BoundsInt[regionSizeX, regionSizeY];

            for (int i = 0; i < regionPoints.GetLength(0); i ++)
            {
                for (int j = 0; j < regionPoints.GetLength(1); j++)
                {
                    regionPoints[i, j] = new BoundsInt(new Vector3Int(i * regionWidth, j * regionHeight), new Vector3Int(regionWidth, regionHeight));
                }
            }
            return regionPoints;
        }

        internal static HashSet<Vector2Int> GenraterFloorPoints(BoundsInt regionPoints, HashSet<Vector2Int> checkAllFloor, int maplterations, int mapSize)
        {
            int count = 0;//计数器
            int randomDir = 0;//随机方向
            int maxSize = maplterations * mapSize;//最大尺寸
            HashSet<Vector2Int> points = new HashSet<Vector2Int>();//坐标点集合
            List<Vector2Int> tempPoints = new List<Vector2Int>();

            var center = regionPoints.center;
            Vector2Int currentPoint = new Vector2Int(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y));
            System.Random random = new System.Random();

            for (int i = 0; i < maplterations; i++)
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
            void AddIfNew(Vector2Int newpoint)
            {
                if (!points.Contains(newpoint) && !checkAllFloor.Contains(newpoint))
                {
                    tempPoints.Add(newpoint);
                    checkAllFloor.Add(newpoint);
                    tPoint.Add(newpoint);
                }
            }

            foreach (var point in points)
            {
                AddIfNew(point + GetDir(0));//左
                AddIfNew(point + 2 * GetDir(0));
                AddIfNew(point + GetDir(1));//右
                AddIfNew(point + GetDir(2));//上
                AddIfNew(point + 2 * GetDir(2));
                AddIfNew(point + GetDir(3));//下
            }
        }

        #region 地图生成方向
        private enum EnumDir
        {
            Left,
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
                dir == 0 ? new Vector2Int(-1, 0) :
                dir == 1 ? new Vector2Int(1, 0) :
                dir == 2 ? new Vector2Int(0, 1) :
                dir == 3 ? new Vector2Int(0, -1) :
                dir == 4 ? new Vector2Int(-1, 1) :
                dir == 5 ? new Vector2Int(1, 1) :
                dir == 6 ? new Vector2Int(-1, -1) :
                new Vector2Int(1, -1);
        }

        internal static HashSet<Vector2Int> GenraterWallPoints(HashSet<Vector2Int> checkAllFloor,int wallWidth = 6)
        {
            HashSet<Vector2Int> wallPoints = new HashSet<Vector2Int>();
            foreach (var point in checkAllFloor)
            {
                for (int i = 0; i < 8; i++)
                {
                    for (int j = 0; j < wallWidth; j++)
                    {
                        var newPoint = point + j * GetDir(i);
                        if (!checkAllFloor.Contains(newPoint))
                        {
                            wallPoints.Add(newPoint);
                        }
                    }
                }
            }
            return wallPoints;
        }
        #endregion
    }
}
