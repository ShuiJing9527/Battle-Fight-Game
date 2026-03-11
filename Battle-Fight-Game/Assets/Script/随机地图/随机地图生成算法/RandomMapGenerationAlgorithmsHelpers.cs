using UnityEngine;

internal static class RandomMapGenerationAlgorithmsHelpers
{
    public static BoundsInt[,] GeneraterRegionPoints(int regionSizeX, int regionSizeY, int regionWidth, int regionHeight)
    {
        BoundsInt[,] regionPoints = new BoundsInt[regionSizeX, regionSizeY];//初始化区域坐标范围二维数组，用于存储每个区域的坐标范围

        for (int i = 0; i < regionPoints.GetLength(0); i++)
        {
            for (int j = 0; j < regionPoints.GetLength(1); j++)
            {
                regionPoints[i, j] = new BoundsInt(new Vector3Int(i * regionWidth, j * regionHeight), new Vector3Int(regionWidth, regionHeight));
            }
        }
        return regionPoints;
    }
}