using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace UnderTheStars.GenerationMap
{
    [System.Serializable]
    public class MapRegionGenerateOption
    {
        public string displayName = "Region";
        public bool generateThisRegion = true;
        public int paintSlotIndex = 0;
        public AreaType areaType = AreaType.Grass;
        public Vector2 sizeMultiplier = Vector2.one;
    }

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
        [SerializeField] private Vector2Int regionSize;// Legacy serialized data for migration
        [SerializeField] private Vector2Int regionArea;// Base region dimensions (width,height)

        [Header("地图生成开关")]
        [SerializeField] private List<MapRegionGenerateOption> regionGenerateOptions = new List<MapRegionGenerateOption>();
        [SerializeField, Min(1)] private int regionsPerRow = 3;
        [SerializeField] private Vector2Int regionSpacing = new Vector2Int(12, 12);
        [SerializeField] private Vector2 defaultRegionSizeMultiplier = Vector2.one;
        [SerializeField] private bool compactByActualFloorBounds = true;
        [SerializeField, Min(0)] private int actualLandSpacing = 3;
        [SerializeField] private bool centerFirstRegionAtWorldOrigin = true;
        [SerializeField] private bool createConnectorBetweenRegions = false;

        [Header("Legacy Area Types (migration only)")]
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
        private ActiveRegionLayout[] activeRegionLayouts;
        private int activeRegionColumns;
        private int activeRegionRows;

        private struct ActiveRegionLayout
        {
            public MapRegionGenerateOption option;
            public BoundsInt bounds;
            public int gridX;
            public int gridY;
            public int layoutIndex;
            public int renderTilemapIndex;
        }

        private struct ActualFloorBoundsInfo
        {
            public bool isValid;
            public Vector2Int min;
            public Vector2Int max;

            public Vector2 Center => new Vector2((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f);

            public BoundsInt ToBoundsInt()
            {
                if (!isValid)
                {
                    return new BoundsInt(Vector3Int.zero, Vector3Int.zero);
                }

                return new BoundsInt(
                    new Vector3Int(min.x, min.y, 0),
                    new Vector3Int((max.x - min.x) + 1, (max.y - min.y) + 1, 1));
            }
        }


        private void Start()
        {
            GenerateMap();
        }

        private void OnValidate()
        {
            EnsureRegionGenerateOptions();
            regionsPerRow = Mathf.Max(1, regionsPerRow);
            regionArea.x = Mathf.Max(1, regionArea.x);
            regionArea.y = Mathf.Max(1, regionArea.y);
        }

        public async void GenerateMap()
        {
            ResetMapData();

            activeRegionLayouts = BuildActiveRegionLayouts();
            if (activeRegionLayouts == null || activeRegionLayouts.Length == 0)
            {
                Debug.LogWarning("[RandomMapGeneration] No enabled regions are configured. Please enable at least one region in regionGenerateOptions.", this);
                return;
            }

            var regionPoints = InitMapRegion();
            var checkAllFloor = GeneraterFloorPoints(regionPoints);
            checkAllFloor = PostProcessActiveRegionFloors(checkAllFloor);
            var generateWallPointsTask = GeneraterWallPointsAsync(checkAllFloor);
            await UniTask.WhenAny(generateWallPointsTask);
            PanintWallTilemap().Forget();

            List<UniTask> paintTasks = new List<UniTask>(activeRegionLayouts.Length);
            for (int i = 0; i < activeRegionLayouts.Length; i++)
            {
                paintTasks.Add(PaintActiveRegionTilemap(activeRegionLayouts[i]));
            }

            await UniTask.WhenAll(paintTasks);

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

        private UniTask PaintActiveRegionTilemap(ActiveRegionLayout layout)
        {
            if (floorPoints == null ||
                layout.gridX < 0 || layout.gridX >= floorPoints.GetLength(0) ||
                layout.gridY < 0 || layout.gridY >= floorPoints.GetLength(1))
            {
                return UniTask.CompletedTask;
            }

            return paintTilemap != null
                ? paintTilemap.PaintFloorTile(
                    floorPoints[layout.gridX, layout.gridY],
                    layout.renderTilemapIndex,
                    layout.option != null ? layout.option.paintSlotIndex : layout.renderTilemapIndex)
                : UniTask.CompletedTask;
        }

        private void SpawnPropsOnFloor()
        {
            if (paintProp == null || floorPoints == null)
            {
                return;
            }

            Tilemap refTilemap = paintTilemap.GetFloorTilemap(ResolveReferencePaintSlotIndex());
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

                    AreaType areaType = ResolveRegionAreaType(x, y);
                    foreach (Vector2Int point in regionPointSet)
                    {
                        result[point] = areaType;
                    }
                }
            }

            return result;
        }

        private AreaType ResolveRegionAreaType(int gridX, int gridY)
        {
            if (activeRegionLayouts != null)
            {
                for (int i = 0; i < activeRegionLayouts.Length; i++)
                {
                    if (activeRegionLayouts[i].gridX == gridX && activeRegionLayouts[i].gridY == gridY)
                    {
                        return activeRegionLayouts[i].option.areaType;
                    }
                }
            }

            int tileIndex = gridX * Mathf.Max(1, regionSize.y) + gridY;
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
            AreaType preferredAreaType = ResolvePreferredSpawnAreaType();
            if (TryGetRandomSafeSpawnWorldPositionForArea(preferredAreaType, out worldPosition, out spawnCoord))
            {
                return true;
            }

            return TryGetRandomSafeSpawnWorldPositionForAnyEnabledRegion(out worldPosition, out spawnCoord);
        }

        public bool TryGetRandomGrassSafeSpawnWorldPosition(out Vector3 worldPosition, out Vector2Int spawnCoord)
        {
            return TryGetRandomSafeSpawnWorldPositionForArea(AreaType.Grass, out worldPosition, out spawnCoord);
        }

        private bool TryGetRandomSafeSpawnWorldPositionForArea(AreaType targetAreaType, out Vector3 worldPosition, out Vector2Int spawnCoord)
        {
            worldPosition = Vector3.zero;
            spawnCoord = Vector2Int.zero;

            if (floorPoints == null)
            {
                return false;
            }

            Tilemap refTilemap = paintTilemap != null ? paintTilemap.GetFloorTilemap(ResolveReferencePaintSlotIndex()) : null;
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
                if (!IsSpawnPointForArea(point, allFloorPoints, areaByPoint, targetAreaType))
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

        private bool TryGetRandomSafeSpawnWorldPositionForAnyEnabledRegion(out Vector3 worldPosition, out Vector2Int spawnCoord)
        {
            worldPosition = Vector3.zero;
            spawnCoord = Vector2Int.zero;

            if (floorPoints == null || activeRegionLayouts == null || activeRegionLayouts.Length == 0)
            {
                return false;
            }

            Tilemap refTilemap = paintTilemap != null ? paintTilemap.GetFloorTilemap(ResolveReferencePaintSlotIndex()) : null;
            if (refTilemap == null)
            {
                return false;
            }

            ActiveRegionLayout firstRegion = activeRegionLayouts[0];
            HashSet<Vector2Int> regionPoints = floorPoints[firstRegion.gridX, firstRegion.gridY];
            if (regionPoints == null || regionPoints.Count == 0)
            {
                return false;
            }

            List<Vector2Int> candidates = new List<Vector2Int>(regionPoints.Count);
            foreach (Vector2Int point in regionPoints)
            {
                candidates.Add(point);
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            spawnCoord = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            worldPosition = refTilemap.GetCellCenterWorld(new Vector3Int(spawnCoord.x, spawnCoord.y, 0));
            return true;
        }

        private static bool IsSpawnPointForArea(Vector2Int point, HashSet<Vector2Int> allFloorPoints, Dictionary<Vector2Int, AreaType> areaByPoint, AreaType targetAreaType)
        {
            if (allFloorPoints == null || areaByPoint == null)
            {
                return false;
            }

            if (!areaByPoint.TryGetValue(point, out AreaType areaType) || areaType != targetAreaType)
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
            floorPoints = new HashSet<Vector2Int>[activeRegionColumns, activeRegionRows];
            propsPoints = new HashSet<Vector2Int>[activeRegionColumns, activeRegionRows];

            Vector2Int[,] regionCenters = new Vector2Int[activeRegionColumns, activeRegionRows];

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
                    if (regionPoints[i, j].size.x <= 0 || regionPoints[i, j].size.y <= 0)
                    {
                        continue;
                    }

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

        private HashSet<Vector2Int> PostProcessActiveRegionFloors(HashSet<Vector2Int> currentAllFloorPoints)
        {
            if (floorPoints == null || activeRegionLayouts == null || activeRegionLayouts.Length == 0)
            {
                return currentAllFloorPoints ?? new HashSet<Vector2Int>();
            }

            Dictionary<int, ActualFloorBoundsInfo> originalBoundsByLayout = new Dictionary<int, ActualFloorBoundsInfo>(activeRegionLayouts.Length);
            Dictionary<int, Vector2Int> finalOffsetByLayout = new Dictionary<int, Vector2Int>(activeRegionLayouts.Length);

            for (int i = 0; i < activeRegionLayouts.Length; i++)
            {
                ActiveRegionLayout layout = activeRegionLayouts[i];
                originalBoundsByLayout[layout.layoutIndex] = CalculateActualFloorBounds(floorPoints[layout.gridX, layout.gridY]);
                finalOffsetByLayout[layout.layoutIndex] = Vector2Int.zero;
            }

            if (compactByActualFloorBounds)
            {
                CompactRowsByActualFloorBounds(originalBoundsByLayout, finalOffsetByLayout);
            }

            Vector2Int anchorOffset = ResolveAnchorOffset(originalBoundsByLayout, finalOffsetByLayout);
            if (anchorOffset != Vector2Int.zero)
            {
                for (int i = 0; i < activeRegionLayouts.Length; i++)
                {
                    ActiveRegionLayout layout = activeRegionLayouts[i];
                    finalOffsetByLayout[layout.layoutIndex] += anchorOffset;
                }
            }

            ApplyOffsetsToRegionFloors(finalOffsetByLayout);

            if (createConnectorBetweenRegions)
            {
                CreateConnectorsBetweenAdjacentRegions();
            }

            HashSet<Vector2Int> rebuiltAllFloorPoints = RebuildAllFloorPoints();
            LogActualFloorBounds(originalBoundsByLayout, finalOffsetByLayout);
            return rebuiltAllFloorPoints;
        }

        private void CompactRowsByActualFloorBounds(
            Dictionary<int, ActualFloorBoundsInfo> actualBoundsByLayout,
            Dictionary<int, Vector2Int> finalOffsetByLayout)
        {
            for (int row = 0; row < activeRegionRows; row++)
            {
                List<ActiveRegionLayout> rowLayouts = new List<ActiveRegionLayout>();
                for (int i = 0; i < activeRegionLayouts.Length; i++)
                {
                    if (activeRegionLayouts[i].gridY == row)
                    {
                        rowLayouts.Add(activeRegionLayouts[i]);
                    }
                }

                rowLayouts.Sort((a, b) => a.gridX.CompareTo(b.gridX));
                int previousShiftedMaxX = 0;
                bool hasPrevious = false;

                for (int i = 0; i < rowLayouts.Count; i++)
                {
                    ActiveRegionLayout layout = rowLayouts[i];
                    if (!actualBoundsByLayout.TryGetValue(layout.layoutIndex, out ActualFloorBoundsInfo actualBounds) || !actualBounds.isValid)
                    {
                        continue;
                    }

                    Vector2Int currentOffset = finalOffsetByLayout[layout.layoutIndex];
                    if (hasPrevious)
                    {
                        int desiredMinX = previousShiftedMaxX + Mathf.Max(0, actualLandSpacing) + 1;
                        int shiftedMinX = actualBounds.min.x + currentOffset.x;
                        currentOffset.x += desiredMinX - shiftedMinX;
                        finalOffsetByLayout[layout.layoutIndex] = currentOffset;
                    }

                    previousShiftedMaxX = actualBounds.max.x + finalOffsetByLayout[layout.layoutIndex].x;
                    hasPrevious = true;
                }
            }
        }

        private Vector2Int ResolveAnchorOffset(
            Dictionary<int, ActualFloorBoundsInfo> actualBoundsByLayout,
            Dictionary<int, Vector2Int> finalOffsetByLayout)
        {
            if (activeRegionLayouts == null || activeRegionLayouts.Length == 0)
            {
                return Vector2Int.zero;
            }

            if (centerFirstRegionAtWorldOrigin)
            {
                ActiveRegionLayout firstLayout = activeRegionLayouts[0];
                if (!actualBoundsByLayout.TryGetValue(firstLayout.layoutIndex, out ActualFloorBoundsInfo firstBounds) || !firstBounds.isValid)
                {
                    return Vector2Int.zero;
                }

                Vector2 shiftedCenter = firstBounds.Center + (Vector2)finalOffsetByLayout[firstLayout.layoutIndex];
                return -Vector2Int.RoundToInt(shiftedCenter);
            }

            ActualFloorBoundsInfo unionBounds = CalculateShiftedUnionBounds(actualBoundsByLayout, finalOffsetByLayout);
            if (!unionBounds.isValid)
            {
                return Vector2Int.zero;
            }

            return -Vector2Int.RoundToInt(unionBounds.Center);
        }

        private void ApplyOffsetsToRegionFloors(Dictionary<int, Vector2Int> finalOffsetByLayout)
        {
            for (int i = 0; i < activeRegionLayouts.Length; i++)
            {
                ActiveRegionLayout layout = activeRegionLayouts[i];
                if (!finalOffsetByLayout.TryGetValue(layout.layoutIndex, out Vector2Int offset) || offset == Vector2Int.zero)
                {
                    continue;
                }

                floorPoints[layout.gridX, layout.gridY] = OffsetPointSet(floorPoints[layout.gridX, layout.gridY], offset);
                propsPoints[layout.gridX, layout.gridY] = OffsetPointSet(propsPoints[layout.gridX, layout.gridY], offset);
            }
        }

        private void CreateConnectorsBetweenAdjacentRegions()
        {
            for (int row = 0; row < activeRegionRows; row++)
            {
                List<ActiveRegionLayout> rowLayouts = new List<ActiveRegionLayout>();
                for (int i = 0; i < activeRegionLayouts.Length; i++)
                {
                    if (activeRegionLayouts[i].gridY == row)
                    {
                        rowLayouts.Add(activeRegionLayouts[i]);
                    }
                }

                rowLayouts.Sort((a, b) => a.gridX.CompareTo(b.gridX));
                for (int i = 0; i < rowLayouts.Count - 1; i++)
                {
                    CreateConnectorBetween(rowLayouts[i], rowLayouts[i + 1]);
                }
            }
        }

        private void CreateConnectorBetween(ActiveRegionLayout leftLayout, ActiveRegionLayout rightLayout)
        {
            HashSet<Vector2Int> leftPoints = floorPoints[leftLayout.gridX, leftLayout.gridY];
            HashSet<Vector2Int> rightPoints = floorPoints[rightLayout.gridX, rightLayout.gridY];
            ActualFloorBoundsInfo leftBounds = CalculateActualFloorBounds(leftPoints);
            ActualFloorBoundsInfo rightBounds = CalculateActualFloorBounds(rightPoints);
            if (!leftBounds.isValid || !rightBounds.isValid)
            {
                return;
            }

            int startX = leftBounds.max.x;
            int endX = rightBounds.min.x;
            if (endX <= startX + 1)
            {
                return;
            }

            int overlapMinY = Mathf.Max(leftBounds.min.y, rightBounds.min.y);
            int overlapMaxY = Mathf.Min(leftBounds.max.y, rightBounds.max.y);
            int corridorCenterY = overlapMinY <= overlapMaxY
                ? Mathf.RoundToInt((overlapMinY + overlapMaxY) * 0.5f)
                : Mathf.RoundToInt((leftBounds.Center.y + rightBounds.Center.y) * 0.5f);

            const int corridorHalfThickness = 1;
            for (int x = startX; x <= endX; x++)
            {
                for (int y = corridorCenterY - corridorHalfThickness; y <= corridorCenterY + corridorHalfThickness; y++)
                {
                    Vector2Int point = new Vector2Int(x, y);
                    leftPoints.Add(point);
                    propsPoints[leftLayout.gridX, leftLayout.gridY]?.Add(point);
                }
            }
        }

        private HashSet<Vector2Int> RebuildAllFloorPoints()
        {
            HashSet<Vector2Int> rebuilt = new HashSet<Vector2Int>();
            if (floorPoints == null)
            {
                return rebuilt;
            }

            for (int x = 0; x < floorPoints.GetLength(0); x++)
            {
                for (int y = 0; y < floorPoints.GetLength(1); y++)
                {
                    if (floorPoints[x, y] == null)
                    {
                        continue;
                    }

                    rebuilt.UnionWith(floorPoints[x, y]);
                }
            }

            return rebuilt;
        }

        private void LogActualFloorBounds(
            Dictionary<int, ActualFloorBoundsInfo> originalBoundsByLayout,
            Dictionary<int, Vector2Int> finalOffsetByLayout)
        {
            for (int i = 0; i < activeRegionLayouts.Length; i++)
            {
                ActiveRegionLayout layout = activeRegionLayouts[i];
                ActualFloorBoundsInfo originalBounds = originalBoundsByLayout.TryGetValue(layout.layoutIndex, out ActualFloorBoundsInfo original) ? original : default(ActualFloorBoundsInfo);
                ActualFloorBoundsInfo finalBounds = CalculateActualFloorBounds(floorPoints[layout.gridX, layout.gridY]);
                Vector2Int finalOffset = finalOffsetByLayout.TryGetValue(layout.layoutIndex, out Vector2Int offset) ? offset : Vector2Int.zero;

                Debug.Log(
                    $"[RandomMapGeneration.ActualBounds] activeIndex={layout.layoutIndex} displayName={(layout.option != null ? layout.option.displayName : "null")} theoreticalBounds={layout.bounds} actualFloorBounds={originalBounds.ToBoundsInt()} finalOffset={finalOffset} finalBounds={finalBounds.ToBoundsInt()}",
                    this);
            }
        }

        private static ActualFloorBoundsInfo CalculateActualFloorBounds(HashSet<Vector2Int> points)
        {
            if (points == null || points.Count == 0)
            {
                return default(ActualFloorBoundsInfo);
            }

            bool initialized = false;
            int minX = 0;
            int maxX = 0;
            int minY = 0;
            int maxY = 0;

            foreach (Vector2Int point in points)
            {
                if (!initialized)
                {
                    minX = maxX = point.x;
                    minY = maxY = point.y;
                    initialized = true;
                    continue;
                }

                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }

            return new ActualFloorBoundsInfo
            {
                isValid = initialized,
                min = new Vector2Int(minX, minY),
                max = new Vector2Int(maxX, maxY)
            };
        }

        private static ActualFloorBoundsInfo CalculateShiftedUnionBounds(
            Dictionary<int, ActualFloorBoundsInfo> actualBoundsByLayout,
            Dictionary<int, Vector2Int> finalOffsetByLayout)
        {
            bool initialized = false;
            int minX = 0;
            int maxX = 0;
            int minY = 0;
            int maxY = 0;

            foreach (KeyValuePair<int, ActualFloorBoundsInfo> kvp in actualBoundsByLayout)
            {
                if (!kvp.Value.isValid)
                {
                    continue;
                }

                Vector2Int offset = finalOffsetByLayout.TryGetValue(kvp.Key, out Vector2Int value) ? value : Vector2Int.zero;
                int shiftedMinX = kvp.Value.min.x + offset.x;
                int shiftedMaxX = kvp.Value.max.x + offset.x;
                int shiftedMinY = kvp.Value.min.y + offset.y;
                int shiftedMaxY = kvp.Value.max.y + offset.y;

                if (!initialized)
                {
                    minX = shiftedMinX;
                    maxX = shiftedMaxX;
                    minY = shiftedMinY;
                    maxY = shiftedMaxY;
                    initialized = true;
                    continue;
                }

                minX = Mathf.Min(minX, shiftedMinX);
                maxX = Mathf.Max(maxX, shiftedMaxX);
                minY = Mathf.Min(minY, shiftedMinY);
                maxY = Mathf.Max(maxY, shiftedMaxY);
            }

            return new ActualFloorBoundsInfo
            {
                isValid = initialized,
                min = new Vector2Int(minX, minY),
                max = new Vector2Int(maxX, maxY)
            };
        }

        private static HashSet<Vector2Int> OffsetPointSet(HashSet<Vector2Int> source, Vector2Int offset)
        {
            if (source == null || offset == Vector2Int.zero)
            {
                return source ?? new HashSet<Vector2Int>();
            }

            HashSet<Vector2Int> result = new HashSet<Vector2Int>(source.Count);
            foreach (Vector2Int point in source)
            {
                result.Add(point + offset);
            }

            return result;
        }
        #endregion

        #region Init
        /// <summary> Initialize map regions. </summary>
        private BoundsInt[,] InitMapRegion()
        {
            BoundsInt[,] regionPoints = new BoundsInt[activeRegionColumns, activeRegionRows];
            for (int i = 0; i < activeRegionLayouts.Length; i++)
            {
                ActiveRegionLayout layout = activeRegionLayouts[i];
                regionPoints[layout.gridX, layout.gridY] = layout.bounds;
            }

            return regionPoints;
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

        private void EnsureRegionGenerateOptions()
        {
            if (regionGenerateOptions != null && regionGenerateOptions.Count > 0)
            {
                return;
            }

            regionGenerateOptions = new List<MapRegionGenerateOption>();
            int legacyCount = Mathf.Max(1, Mathf.Max(regionAreaTypes != null ? regionAreaTypes.Length : 0, Mathf.Max(1, regionSize.x * regionSize.y)));
            for (int i = 0; i < legacyCount; i++)
            {
                AreaType areaType = AreaType.NoSpawn;
                if (useRegionAreaTypes && regionAreaTypes != null && i < regionAreaTypes.Length)
                {
                    areaType = regionAreaTypes[i];
                }
                else if (forestRegionIndices != null && forestRegionIndices.Contains(i))
                {
                    areaType = AreaType.Forest;
                }
                else if (grassRegionIndices != null && grassRegionIndices.Contains(i))
                {
                    areaType = AreaType.Grass;
                }

                bool enabledByDefault = areaType == AreaType.Grass || areaType == AreaType.Forest;
                regionGenerateOptions.Add(new MapRegionGenerateOption
                {
                    displayName = $"Region {i}",
                    generateThisRegion = enabledByDefault,
                    paintSlotIndex = i,
                    areaType = areaType,
                    sizeMultiplier = defaultRegionSizeMultiplier
                });
            }
        }

        private ActiveRegionLayout[] BuildActiveRegionLayouts()
        {
            EnsureRegionGenerateOptions();

            List<MapRegionGenerateOption> enabledOptions = new List<MapRegionGenerateOption>();
            for (int i = 0; i < regionGenerateOptions.Count; i++)
            {
                MapRegionGenerateOption option = regionGenerateOptions[i];
                if (option != null && option.generateThisRegion)
                {
                    enabledOptions.Add(option);
                }
            }

            if (enabledOptions.Count == 0)
            {
                activeRegionColumns = 0;
                activeRegionRows = 0;
                return System.Array.Empty<ActiveRegionLayout>();
            }

            int perRow = Mathf.Max(1, regionsPerRow);
            activeRegionColumns = Mathf.Min(perRow, enabledOptions.Count);
            activeRegionRows = Mathf.CeilToInt(enabledOptions.Count / (float)perRow);
            regionSize = new Vector2Int(activeRegionColumns, activeRegionRows);

            List<ActiveRegionLayout> layouts = new List<ActiveRegionLayout>(enabledOptions.Count);
            int[] rowWidths = new int[activeRegionRows];
            int[] rowHeights = new int[activeRegionRows];

            for (int i = 0; i < enabledOptions.Count; i++)
            {
                MapRegionGenerateOption option = enabledOptions[i];
                int row = i / perRow;
                Vector2Int size = ResolveRegionDimensions(option);
                rowWidths[row] += size.x;
                if (i % perRow > 0)
                {
                    rowWidths[row] += Mathf.Max(0, regionSpacing.x);
                }

                rowHeights[row] = Mathf.Max(rowHeights[row], size.y);
            }

            int totalHeight = 0;
            for (int row = 0; row < activeRegionRows; row++)
            {
                totalHeight += rowHeights[row];
                if (row > 0)
                {
                    totalHeight += Mathf.Max(0, regionSpacing.y);
                }
            }

            int currentY = totalHeight / 2;
            int optionIndex = 0;
            for (int row = 0; row < activeRegionRows; row++)
            {
                int currentX = -rowWidths[row] / 2;
                for (int column = 0; column < perRow && optionIndex < enabledOptions.Count; column++, optionIndex++)
                {
                    MapRegionGenerateOption option = enabledOptions[optionIndex];
                    Vector2Int size = ResolveRegionDimensions(option);
                    BoundsInt bounds = new BoundsInt(
                        new Vector3Int(currentX, currentY - size.y, 0),
                        new Vector3Int(size.x, size.y, 1));

                    layouts.Add(new ActiveRegionLayout
                    {
                        option = option,
                        bounds = bounds,
                        gridX = column,
                        gridY = row,
                        layoutIndex = optionIndex,
                        renderTilemapIndex = optionIndex
                    });

                    Debug.Log(
                        $"[RandomMapGeneration.Layout] activeCount={enabledOptions.Count} activeIndex={optionIndex} displayName={(option != null ? option.displayName : "null")} row={row} column={column} origin={bounds.position} size={bounds.size} boundsMin={bounds.min} boundsMax={bounds.max}",
                        this);

                    currentX += size.x + Mathf.Max(0, regionSpacing.x);
                }

                currentY -= rowHeights[row] + Mathf.Max(0, regionSpacing.y);
            }

            return layouts.ToArray();
        }

        private Vector2Int ResolveRegionDimensions(MapRegionGenerateOption option)
        {
            Vector2 multiplier = option != null ? option.sizeMultiplier : Vector2.one;
            multiplier.x *= Mathf.Max(0.01f, defaultRegionSizeMultiplier.x);
            multiplier.y *= Mathf.Max(0.01f, defaultRegionSizeMultiplier.y);
            int width = Mathf.Max(1, Mathf.RoundToInt(regionArea.x * Mathf.Max(0.01f, multiplier.x)));
            int height = Mathf.Max(1, Mathf.RoundToInt(regionArea.y * Mathf.Max(0.01f, multiplier.y)));
            return new Vector2Int(width, height);
        }

        private AreaType ResolvePreferredSpawnAreaType()
        {
            if (activeRegionLayouts == null || activeRegionLayouts.Length == 0)
            {
                return AreaType.Grass;
            }

            return activeRegionLayouts[0].option != null ? activeRegionLayouts[0].option.areaType : AreaType.Grass;
        }

        private int ResolveReferencePaintSlotIndex()
        {
            if (activeRegionLayouts != null && activeRegionLayouts.Length > 0)
            {
                return Mathf.Max(0, activeRegionLayouts[0].renderTilemapIndex);
            }

            return 0;
        }
        #endregion
    }
}
