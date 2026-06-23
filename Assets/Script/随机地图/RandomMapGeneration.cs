using Cysharp.Threading.Tasks;
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
        [SerializeField] private List<int> grassRegionIndices = new List<int> { 0 };
        [SerializeField] private List<int> forestRegionIndices = new List<int> { 1 };

        [Header("Player Settings")]
        [SerializeField] private PlayerMovement player; // Drag Player here in Inspector

        [Header("Performance")]
        [SerializeField] private bool collectGarbageAfterReset = false;

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

            if (forestRegionIndices != null && forestRegionIndices.Contains(tileIndex))
            {
                return AreaType.Forest;
            }

            if (grassRegionIndices != null && grassRegionIndices.Contains(tileIndex))
            {
                return AreaType.Grass;
            }

            return AreaType.NoSpawn;
        }

        private void PlacePlayerOnMap()
        {
            if (floorPoints == null) return;

            PlayerSpawnManager spawnManager = FindObjectOfType<PlayerSpawnManager>();
            if (spawnManager != null && spawnManager.SpawnPartyAtRandomSafePoint(this))
            {
                return;
            }

            Player2Bootstrap bootstrap = FindObjectOfType<Player2Bootstrap>();
            if (bootstrap != null)
            {
                bootstrap.EnsureInitializedForSpawn();
            }

            Transform spawnTarget = ResolveSpawnTargetTransform();
            if (spawnTarget == null) return;

            if (!TryGetRandomSafeSpawnWorldPosition(out Vector3 worldSpawnPos, out Vector2Int spawnCoord))
            {
                return;
            }

            Rigidbody targetRb = spawnTarget.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                targetRb.linearVelocity = Vector3.zero;
            }

            PlayerMovement targetMovement = spawnTarget.GetComponent<PlayerMovement>();
            if (targetMovement != null && targetMovement.rb != null)
            {
                targetMovement.rb.linearVelocity = Vector3.zero;
            }

            Vector3 spawnBasePosition = worldSpawnPos;
            if (bootstrap != null)
            {
                spawnBasePosition = bootstrap.ApplyCharacterHeightOffset(spawnTarget.gameObject, worldSpawnPos);
            }

            spawnTarget.position = spawnBasePosition;

            Debug.Log($"Player placed. Cell:{spawnCoord} -> World:{worldSpawnPos}");
            Debug.Log($"[SPAWN] Spawn target = {spawnTarget.name}");
        }

        public void SetPlayer(PlayerMovement playerMovement)
        {
            player = playerMovement;
        }

        public bool TryGetRandomSafeSpawnWorldPosition(out Vector3 worldPosition, out Vector2Int spawnCoord)
        {
            return TryGetRandomGrassSafeSpawnWorldPosition(out worldPosition, out spawnCoord);
        }

        public bool TryGetRandomGrassSafeSpawnWorldPosition(out Vector3 worldPosition, out Vector2Int spawnCoord)
        {
            worldPosition = Vector3.zero;
            spawnCoord = Vector2Int.zero;

            if (floorPoints == null)
            {
                return false;
            }

            Tilemap refTilemap = paintTilemap != null ? paintTilemap.GetFloorTilemap(0) : null;
            if (refTilemap == null)
            {
                return false;
            }

            Dictionary<Vector2Int, AreaType> areaByPoint = BuildPointAreaTypes();
            HashSet<Vector2Int> allFloorPoints = new HashSet<Vector2Int>();
            for (int x = 0; x < floorPoints.GetLength(0); x++)
            {
                for (int y = 0; y < floorPoints.GetLength(1); y++)
                {
                    HashSet<Vector2Int> regionPoints = floorPoints[x, y];
                    if (regionPoints == null)
                    {
                        continue;
                    }

                    allFloorPoints.UnionWith(regionPoints);
                }
            }

            List<Vector2Int> grassCandidates = new List<Vector2Int>();
            foreach (Vector2Int point in allFloorPoints)
            {
                if (!IsGrassSpawnPoint(point, allFloorPoints, areaByPoint))
                {
                    continue;
                }

                grassCandidates.Add(point);
            }

            if (grassCandidates.Count == 0)
            {
                return false;
            }

            spawnCoord = grassCandidates[UnityEngine.Random.Range(0, grassCandidates.Count)];
            Vector3Int cellPos = new Vector3Int(spawnCoord.x, spawnCoord.y, 0);
            worldPosition = refTilemap.GetCellCenterWorld(cellPos);
            return true;
        }

        private static bool IsGrassSpawnPoint(Vector2Int point, HashSet<Vector2Int> allFloorPoints, Dictionary<Vector2Int, AreaType> areaByPoint)
        {
            if (allFloorPoints == null || areaByPoint == null)
            {
                return false;
            }

            if (!areaByPoint.TryGetValue(point, out AreaType areaType) || areaType != AreaType.Grass)
            {
                return false;
            }

            if (!allFloorPoints.Contains(point + Vector2Int.up) ||
                !allFloorPoints.Contains(point + Vector2Int.down) ||
                !allFloorPoints.Contains(point + Vector2Int.left) ||
                !allFloorPoints.Contains(point + Vector2Int.right))
            {
                return false;
            }

            return true;
        }

        private Transform ResolveSpawnTargetTransform()
        {
            Player2Bootstrap bootstrap = FindObjectOfType<Player2Bootstrap>();
            if (bootstrap != null)
            {
                bootstrap.EnsureInitializedForSpawn();
            }
            Transform fromBootstrapCurrent = bootstrap != null ? bootstrap.CurrentPlayerTransform : null;
            Transform fromBootstrapLeader = bootstrap != null && bootstrap.PartyLeader != null ? bootstrap.PartyLeader.transform : null;
            Transform fromFallback = player != null ? player.transform : null;

            Transform spawnTarget = fromBootstrapCurrent ?? fromBootstrapLeader ?? fromFallback;
            if (spawnTarget == null)
            {
                Debug.LogWarning("[SPAWN] Could not resolve spawn target.");
            }

            return spawnTarget;
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

            if (collectGarbageAfterReset)
            {
                System.GC.Collect();
            }
        }

        /// <summary> Clear tile/prop painting. </summary>
        private void InitMapPaint()
        {
            if (paintTilemap != null)
            {
                paintTilemap.InitClearTile();
            }

            if (paintProp != null)
            {
                paintProp.InitClearProp();
            }
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
