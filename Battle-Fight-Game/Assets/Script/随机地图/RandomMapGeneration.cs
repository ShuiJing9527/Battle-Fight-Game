using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace UnderTheStars.GenerationMap
{
    public class RandomMapGeneration : MonoBehaviour
    {
        [Header("Map Seed")]
        public int mapSeed;// Map seed
        [Header("Map Size")]
        public int mapSize;// Map size
        [Header("Map Iterations")]
        public int maplterations;// Map iterations

        [Header("Map Painting")]
        [SerializeField] private RandomMapPaintTilemap paintTilemap;// Tilemap painter
        [SerializeField] private RandomMapPainProp paintProp;// Prop painter

        [Header("区域大小与范围")]
        [SerializeField] private Vector2Int regionSize;// Region count (x,y)
        [SerializeField] private Vector2Int regionArea;// Region dimensions (width,height)
        [Header("Area Types (by tileIndex 0..8)")]
        [SerializeField] private bool useRegionAreaTypes = false;
        [SerializeField] private AreaType[] regionAreaTypes = new AreaType[9];
        [SerializeField] private List<int> grassRegionIndices = new List<int> { 0, 1 };

        [Header("Player Settings")]
        [SerializeField] private PlayerMovement player; // Drag Player here in Inspector

        private HashSet<Vector2Int>[,] floorPoints;// Floor points
        private HashSet<Vector2Int>[,] propsPoints;// Prop points
        private HashSet<Vector2Int> wallColliderPoints;// Wall collider points


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

            // Wait until all Tilemaps are painted.
            await UniTask.WhenAll(panintTilemap(0, 0), panintTilemap(0, 1), panintTilemap(1, 0));
            await UniTask.WhenAll(panintTilemap(1, 1), panintTilemap(2, 0), panintTilemap(2, 1));
            await UniTask.WhenAll(panintTilemap(0, 2), panintTilemap(1, 2), panintTilemap(2, 2));

            // Place player
            SpawnPropsOnFloor();
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

        private void SpawnPropsOnFloor()
        {
            if (paintProp == null || floorPoints == null)
            {
                return;
            }

            Tilemap refTilemap = paintTilemap.GetFloorTilemap(0);
            if (refTilemap == null)
            {
                return;
            }

            Dictionary<Vector2Int, AreaType> pointAreaTypes = BuildPointAreaTypes();
            paintProp.SpawnProps(floorPoints, refTilemap, pointAreaTypes);
        }

        private Dictionary<Vector2Int, AreaType> BuildPointAreaTypes()
        {
            Dictionary<Vector2Int, AreaType> result = new Dictionary<Vector2Int, AreaType>();
            if (floorPoints == null)
            {
                return result;
            }

            for (int x = 0; x < floorPoints.GetLength(0); x++)
            {
                for (int y = 0; y < floorPoints.GetLength(1); y++)
                {
                    HashSet<Vector2Int> regionPointSet = floorPoints[x, y];
                    if (regionPointSet == null)
                    {
                        continue;
                    }

                    int tileIndex = x * regionSize.y + y;
                    AreaType areaType = ResolveRegionAreaType(tileIndex);
                    foreach (Vector2Int point in regionPointSet)
                    {
                        result[point] = areaType;
                    }
                }
            }

            return result;
        }

        private AreaType ResolveRegionAreaType(int tileIndex)
        {
            if (useRegionAreaTypes && regionAreaTypes != null && tileIndex >= 0 && tileIndex < regionAreaTypes.Length)
            {
                return regionAreaTypes[tileIndex];
            }

            if (grassRegionIndices != null && grassRegionIndices.Contains(tileIndex))
            {
                return AreaType.Grass;
            }

            return AreaType.NoSpawn;
        }

        private void PlacePlayerOnMap()
        {
            if (player == null || floorPoints == null) return;

            // Get reference Tilemap
            Tilemap refTilemap = paintTilemap.GetFloorTilemap(0);
            if (refTilemap == null) return;

            Vector2Int spawnCoord = Vector2Int.zero;
            bool found = false;

            // Try finding a valid spawn coordinate.
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
                // Convert to cell position.
                Vector3Int cellPos = new Vector3Int(spawnCoord.x, spawnCoord.y, 0);

                // Convert to world position (handles tilemap transform/rotation).
                Vector3 worldSpawnPos = refTilemap.GetCellCenterWorld(cellPos);

                // Teleport player.
                // Use rb.linearVelocity instead of velocity (Unity 6 recommendation).
                player.rb.linearVelocity = Vector3.zero;

                // Lift Y a little to avoid clipping into ground.
                // worldSpawnPos already contains the correct 3D position.
                player.transform.position = worldSpawnPos + Vector3.up * 1.0f;

                Debug.Log($"Player placed. Cell:{cellPos} -> World:{worldSpawnPos}");
            }
        }

        #region Region Generation
        /// <summary> Generate floor points. </summary>
        private HashSet<Vector2Int> GeneraterFloorPoints(BoundsInt[,] regionPoints)
        {
            floorPoints = new HashSet<Vector2Int>[regionSize.x, regionSize.y];
            propsPoints = new HashSet<Vector2Int>[regionSize.x, regionSize.y];

            Vector2Int[,] regionCenters = new Vector2Int[regionSize.x, regionSize.y];

            HashSet<Vector2Int> checkFloor = new HashSet<Vector2Int>();

            GeneraterFloorPoints(regionPoints, regionCenters, checkFloor);

            return checkFloor;
        }

        /// <summary> Generate region points. </summary>
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

        #region Init
        /// <summary> Initialize map regions. </summary>
        private BoundsInt[,] InitMapRegion()
        {
            return RandomMapGenerationAlgorithms.GenraterRegionPoints(regionSize.x, regionSize.y, regionArea.x, regionArea.y);
        }

        /// <summary> Reset map data. </summary>
        public void ResetMapData()
        {
            InitMapSeed();
            InitMapData();
            InitMapPaint();
            GC.Collect();
        }

        /// <summary> Clear tile/prop painting. </summary>
        private void InitMapPaint()
        {
            paintTilemap.InitClearTile();
            paintProp.InitClearProp();
        }

        /// <summary> Clear cached point sets. </summary>
        private void InitMapData()
        {
            floorPoints = null;
            propsPoints = null;
            wallColliderPoints = null;
        }

        /// <summary> Initialize map random seed. </summary>
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
