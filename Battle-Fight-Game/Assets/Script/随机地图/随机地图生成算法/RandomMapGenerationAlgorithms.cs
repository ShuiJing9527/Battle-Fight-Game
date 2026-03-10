using UnityEngine;

namespace UnderTheStars.GenerationMap
{
    public class RandomMapGenerationAlgorithms
    {
        public static BoundsInt[,] GeneraterRegionPoints(int regionSizeX, int regionSizeY, int regionWidth, int regionHeight)
        {
            BoundsInt[,] regionPoints = new BoundsInt[regionSizeX, regionSizeY];

            for (int i = 0; i < regionPoints.GetLength(0); i++)
            {
                for (int j = 0; j < regionPoints.GetLength(1); j++)
                {
                    var position = new Vector3Int(i * regionWidth, j * regionHeight);
                    var size = new Vector3Int(regionWidth, regionHeight);
                    regionPoints[i, j] = new BoundsInt(position, size);
                }
            }

            return regionPoints;
        }
    }
}