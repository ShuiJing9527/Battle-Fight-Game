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

        [Header("Shore Sand")]
        [SerializeField] private bool enableShoreSand = true;
        [SerializeField] private GameObject shoreSandNormalPrefab;
        [SerializeField] private GameObject shoreSandOceanTransitionPrefab;
        [SerializeField] private GameObject shoreSandGrassTransitionPrefab;
        [SerializeField] private GameObject shoreSandOceanOuterCornerPrefab;
        [SerializeField] private GameObject shoreSandOceanInnerCornerPrefab;
        [SerializeField] private GameObject shoreSandGrassOuterCornerPrefab;
        [SerializeField] private GameObject shoreSandGrassInnerCornerPrefab;
        [SerializeField] private Transform shoreSandParent;
        [SerializeField] private float shoreSandHeightOffset = 0.02f;
        [SerializeField] private bool debugShoreSandPlacements = false;

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

        [Header("Land Shape Cleanup")]
        [SerializeField] private bool enableLandShapeCleanup = false;
        [SerializeField] private bool fillTinyWaterPockets = true;
        [SerializeField] private bool removeSingleTileSpikes = true;
        [SerializeField, Min(1)] private int cleanupIterations = 1;

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
        private HashSet<Vector2Int> generatedShoreSandPoints;// Shore sand points
        private HashSet<Vector2Int> connectorFloorPoints;// Protected connector points
        private bool hasLoggedMissingShoreSandPrefabWarning;
        private ActiveRegionLayout[] activeRegionLayouts;
        private int activeRegionColumns;
        private int activeRegionRows;
        private const string GeneratedShoreSandRootName = "Generated Shore Sand";

        private enum ShoreEdgeDirection
        {
            Up,
            Down,
            Left,
            Right
        }

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

        private struct ShoreSandPlacement
        {
            public Vector2Int point;
            public GameObject prefab;
            public ShoreEdgeDirection direction;
            public bool replacesGrassTile;
            public bool marksAsBeach;
            public bool usesGrassTransitionDirectionMapping;
            public int grassNeighborCount;
            public bool usedFixedPrioritySelection;
            public bool fromAdjacentTwoGrass;
            public bool usesExplicitYaw;
            public float explicitYaw;

            public ShoreSandPlacement(
                Vector2Int point,
                GameObject prefab,
                ShoreEdgeDirection direction,
                bool replacesGrassTile,
                bool marksAsBeach,
                bool usesGrassTransitionDirectionMapping,
                int grassNeighborCount = 0,
                bool usedFixedPrioritySelection = false,
                bool fromAdjacentTwoGrass = false,
                bool usesExplicitYaw = false,
                float explicitYaw = 0f)
            {
                this.point = point;
                this.prefab = prefab;
                this.direction = direction;
                this.replacesGrassTile = replacesGrassTile;
                this.marksAsBeach = marksAsBeach;
                this.usesGrassTransitionDirectionMapping = usesGrassTransitionDirectionMapping;
                this.grassNeighborCount = grassNeighborCount;
                this.usedFixedPrioritySelection = usedFixedPrioritySelection;
                this.fromAdjacentTwoGrass = fromAdjacentTwoGrass;
                this.usesExplicitYaw = usesExplicitYaw;
                this.explicitYaw = explicitYaw;
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
            cleanupIterations = Mathf.Max(1, cleanupIterations);
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

            GenerateShoreSand();

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
                        if (generatedShoreSandPoints != null && generatedShoreSandPoints.Contains(point))
                        {
                            result[point] = AreaType.Beach;
                        }
                        else
                        {
                            result[point] = areaType;
                        }
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

            if (enableLandShapeCleanup)
            {
                ApplyLandShapeCleanup();
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
                    connectorFloorPoints?.Add(point);
                }
            }
        }

        private void ApplyLandShapeCleanup()
        {
            for (int iteration = 0; iteration < cleanupIterations; iteration++)
            {
                HashSet<Vector2Int> allFloorPoints = RebuildAllFloorPoints();
                if (allFloorPoints.Count == 0)
                {
                    return;
                }

                HashSet<Vector2Int> pointsToFill = new HashSet<Vector2Int>();
                HashSet<Vector2Int> pointsToRemove = new HashSet<Vector2Int>();

                if (fillTinyWaterPockets)
                {
                    CollectTinyWaterPocketFills(allFloorPoints, pointsToFill);
                }

                if (removeSingleTileSpikes)
                {
                    CollectSingleTileSpikeRemovals(allFloorPoints, pointsToRemove);
                }

                if (pointsToFill.Count == 0 && pointsToRemove.Count == 0)
                {
                    break;
                }

                ApplyFloorPointFills(pointsToFill);
                ApplyFloorPointRemovals(pointsToRemove);
            }
        }

        private void CollectTinyWaterPocketFills(HashSet<Vector2Int> allFloorPoints, HashSet<Vector2Int> pointsToFill)
        {
            foreach (Vector2Int floorPoint in allFloorPoints)
            {
                TryQueueTinyWaterPocketFill(floorPoint + Vector2Int.up, allFloorPoints, pointsToFill);
                TryQueueTinyWaterPocketFill(floorPoint + Vector2Int.down, allFloorPoints, pointsToFill);
                TryQueueTinyWaterPocketFill(floorPoint + Vector2Int.left, allFloorPoints, pointsToFill);
                TryQueueTinyWaterPocketFill(floorPoint + Vector2Int.right, allFloorPoints, pointsToFill);
            }
        }

        private void TryQueueTinyWaterPocketFill(
            Vector2Int candidatePoint,
            HashSet<Vector2Int> allFloorPoints,
            HashSet<Vector2Int> pointsToFill)
        {
            if (allFloorPoints.Contains(candidatePoint) || pointsToFill.Contains(candidatePoint))
            {
                return;
            }

            if (CountOrthogonalLandNeighbors(candidatePoint, allFloorPoints) >= 3)
            {
                pointsToFill.Add(candidatePoint);
            }
        }

        private void CollectSingleTileSpikeRemovals(HashSet<Vector2Int> allFloorPoints, HashSet<Vector2Int> pointsToRemove)
        {
            for (int x = 0; x < floorPoints.GetLength(0); x++)
            {
                for (int y = 0; y < floorPoints.GetLength(1); y++)
                {
                    HashSet<Vector2Int> regionPointSet = floorPoints[x, y];
                    if (regionPointSet == null)
                    {
                        continue;
                    }

                    foreach (Vector2Int point in regionPointSet)
                    {
                        if (connectorFloorPoints != null && connectorFloorPoints.Contains(point))
                        {
                            continue;
                        }

                        if (CountOrthogonalLandNeighbors(point, allFloorPoints) <= 1)
                        {
                            pointsToRemove.Add(point);
                        }
                    }
                }
            }
        }

        private void ApplyFloorPointFills(HashSet<Vector2Int> pointsToFill)
        {
            foreach (Vector2Int point in pointsToFill)
            {
                if (TryResolveOwningRegionForFilledPoint(point, out int ownerGridX, out int ownerGridY))
                {
                    floorPoints[ownerGridX, ownerGridY].Add(point);
                    propsPoints[ownerGridX, ownerGridY]?.Add(point);
                }
            }
        }

        private void ApplyFloorPointRemovals(HashSet<Vector2Int> pointsToRemove)
        {
            if (pointsToRemove.Count == 0)
            {
                return;
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

                    foreach (Vector2Int point in pointsToRemove)
                    {
                        regionPointSet.Remove(point);
                        propsPoints[x, y]?.Remove(point);
                    }
                }
            }
        }

        private bool TryResolveOwningRegionForFilledPoint(Vector2Int point, out int ownerGridX, out int ownerGridY)
        {
            ownerGridX = -1;
            ownerGridY = -1;
            int bestNeighborCount = 0;

            for (int x = 0; x < floorPoints.GetLength(0); x++)
            {
                for (int y = 0; y < floorPoints.GetLength(1); y++)
                {
                    HashSet<Vector2Int> regionPointSet = floorPoints[x, y];
                    if (regionPointSet == null)
                    {
                        continue;
                    }

                    int neighborCount = 0;
                    if (regionPointSet.Contains(point + Vector2Int.up)) neighborCount++;
                    if (regionPointSet.Contains(point + Vector2Int.down)) neighborCount++;
                    if (regionPointSet.Contains(point + Vector2Int.left)) neighborCount++;
                    if (regionPointSet.Contains(point + Vector2Int.right)) neighborCount++;

                    if (neighborCount > bestNeighborCount)
                    {
                        bestNeighborCount = neighborCount;
                        ownerGridX = x;
                        ownerGridY = y;
                    }
                }
            }

            return ownerGridX >= 0 && ownerGridY >= 0 && bestNeighborCount > 0;
        }

        private static int CountOrthogonalLandNeighbors(Vector2Int point, HashSet<Vector2Int> allFloorPoints)
        {
            int count = 0;
            if (allFloorPoints.Contains(point + Vector2Int.up)) count++;
            if (allFloorPoints.Contains(point + Vector2Int.down)) count++;
            if (allFloorPoints.Contains(point + Vector2Int.left)) count++;
            if (allFloorPoints.Contains(point + Vector2Int.right)) count++;
            return count;
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
            generatedShoreSandPoints = null;
            connectorFloorPoints = new HashSet<Vector2Int>();
            hasLoggedMissingShoreSandPrefabWarning = false;
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

        private void GenerateShoreSand()
        {
            generatedShoreSandPoints = null;
            ClearGeneratedShoreSandInstances();

            if (!enableShoreSand)
            {
                return;
            }

            if (!HasAllShoreSandPrefabsAssigned())
            {
                if (!hasLoggedMissingShoreSandPrefabWarning)
                {
                    Debug.LogWarning("[RandomMapGeneration] Shore Sand is enabled, but one or more Shore Sand prefabs are not assigned. Assign Normal, Ocean Transition, and Grass Transition prefabs in the inspector. Shore sand generation will be skipped.", this);
                    hasLoggedMissingShoreSandPrefabWarning = true;
                }

                return;
            }

            if (paintTilemap == null || floorPoints == null)
            {
                return;
            }

            Tilemap referenceTilemap = paintTilemap.GetFloorTilemap(ResolveReferencePaintSlotIndex());
            if (referenceTilemap == null)
            {
                return;
            }

            Dictionary<Vector2Int, AreaType> areaByPoint = new Dictionary<Vector2Int, AreaType>();
            Dictionary<Vector2Int, int> tilemapIndexByPoint = new Dictionary<Vector2Int, int>();
            HashSet<Vector2Int> allLandPoints = CollectLandPointMetadata(areaByPoint, tilemapIndexByPoint);
            if (allLandPoints.Count == 0)
            {
                return;
            }

            Dictionary<Vector2Int, List<ShoreSandPlacement>> candidatePlacementsByPoint = new Dictionary<Vector2Int, List<ShoreSandPlacement>>();
            foreach (Vector2Int point in allLandPoints)
            {
                if (!areaByPoint.TryGetValue(point, out AreaType areaType) || areaType != AreaType.Grass)
                {
                    continue;
                }

                if (TryBuildShoreSandStrip(point, allLandPoints, areaByPoint, out List<ShoreSandPlacement> stripPlacements))
                {
                    for (int i = 0; i < stripPlacements.Count; i++)
                    {
                        Vector2Int placementPoint = stripPlacements[i].point;
                        if (!candidatePlacementsByPoint.TryGetValue(placementPoint, out List<ShoreSandPlacement> candidates))
                        {
                            candidates = new List<ShoreSandPlacement>();
                            candidatePlacementsByPoint.Add(placementPoint, candidates);
                        }

                        candidates.Add(stripPlacements[i]);
                    }
                }
            }

            List<ShoreSandPlacement> placements = new List<ShoreSandPlacement>(candidatePlacementsByPoint.Count);
            foreach (KeyValuePair<Vector2Int, List<ShoreSandPlacement>> kvp in candidatePlacementsByPoint)
            {
                List<ShoreSandPlacement> candidates = kvp.Value;
                if (candidates == null || candidates.Count == 0)
                {
                    continue;
                }

                if (TryResolveOceanOuterCornerPlacement(candidates, out ShoreSandPlacement oceanOuterCornerPlacement))
                {
                    placements.Add(oceanOuterCornerPlacement);
                }
                else if (TryResolveOceanInnerCornerPlacement(candidates, out ShoreSandPlacement oceanInnerCornerPlacement))
                {
                    placements.Add(oceanInnerCornerPlacement);
                }
                else if (TryResolveGrassInnerCornerPlacement(candidates, out ShoreSandPlacement grassInnerCornerPlacement))
                {
                    placements.Add(grassInnerCornerPlacement);
                }
                else if (TryResolvePreferredDirectSeaPlacement(candidates, out ShoreSandPlacement directSeaPlacement))
                {
                    placements.Add(directSeaPlacement);
                }
                else if (ShouldDowngradeToShoreSandNormal(candidates))
                {
                    placements.Add(new ShoreSandPlacement(
                        kvp.Key,
                        shoreSandNormalPrefab,
                        ShoreEdgeDirection.Up,
                        false,
                        false,
                        false));
                }
                else
                {
                    placements.Add(candidates[0]);
                }
            }

            if (placements.Count == 0)
            {
                return;
            }

            ApplyFinalGrassBoundaryCorrection(placements, allLandPoints, areaByPoint);
            ApplyGrassTransitionAdjacencyFix(placements);

            Transform parent = ResolveGeneratedShoreSandParent();
            generatedShoreSandPoints = new HashSet<Vector2Int>(placements.Count);

            for (int i = 0; i < placements.Count; i++)
            {
                Vector2Int point = placements[i].point;
                if (!tilemapIndexByPoint.TryGetValue(point, out int tilemapIndex))
                {
                    continue;
                }

                if (placements[i].replacesGrassTile)
                {
                    paintTilemap.ClearFloorTileCell(tilemapIndex, point);
                }

                Vector3 worldPosition = referenceTilemap.GetCellCenterWorld(new Vector3Int(point.x, point.y, 0));
                worldPosition.y += shoreSandHeightOffset;

                GameObject prefab = placements[i].prefab;
                float finalYaw = placements[i].usesExplicitYaw
                    ? placements[i].explicitYaw
                    : placements[i].usesGrassTransitionDirectionMapping
                    ? ResolveGrassTransitionYaw(placements[i].direction)
                    : ResolveShoreSandYaw(placements[i].direction);
                Quaternion finalRotation = Quaternion.Euler(0f, finalYaw, 0f);

                GameObject instance = Instantiate(prefab, worldPosition, finalRotation, parent);
                ApplyShoreSandDebugName(instance, placements[i], point);
                if (placements[i].marksAsBeach)
                {
                    generatedShoreSandPoints.Add(point);
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (placements[i].usesGrassTransitionDirectionMapping)
                {
                    Debug.Log(
                        $"[ShoreSand.GrassTransitionRotation] point={point} inlandDirection={placements[i].direction} finalWorldY={NormalizeYaw(finalRotation.eulerAngles.y):F1}",
                        this);
                }
#endif
            }
        }

        private bool HasAllShoreSandPrefabsAssigned()
        {
            return shoreSandNormalPrefab != null &&
                   shoreSandOceanTransitionPrefab != null &&
                   shoreSandGrassTransitionPrefab != null;
        }

        private HashSet<Vector2Int> CollectLandPointMetadata(
            Dictionary<Vector2Int, AreaType> areaByPoint,
            Dictionary<Vector2Int, int> tilemapIndexByPoint)
        {
            HashSet<Vector2Int> allLandPoints = new HashSet<Vector2Int>();
            if (floorPoints == null)
            {
                return allLandPoints;
            }

            for (int x = 0; x < floorPoints.GetLength(0); x++)
            {
                for (int y = 0; y < floorPoints.GetLength(1); y++)
                {
                    HashSet<Vector2Int> regionPointSet = floorPoints[x, y];
                    if (regionPointSet == null || regionPointSet.Count == 0)
                    {
                        continue;
                    }

                    AreaType areaType = ResolveRegionAreaType(x, y);
                    int tilemapIndex = ResolveRegionRenderTilemapIndex(x, y);
                    foreach (Vector2Int point in regionPointSet)
                    {
                        allLandPoints.Add(point);
                        areaByPoint[point] = areaType;
                        tilemapIndexByPoint[point] = tilemapIndex;
                    }
                }
            }

            return allLandPoints;
        }

        private int ResolveRegionRenderTilemapIndex(int gridX, int gridY)
        {
            if (activeRegionLayouts != null)
            {
                for (int i = 0; i < activeRegionLayouts.Length; i++)
                {
                    if (activeRegionLayouts[i].gridX == gridX && activeRegionLayouts[i].gridY == gridY)
                    {
                        return Mathf.Max(0, activeRegionLayouts[i].renderTilemapIndex);
                    }
                }
            }

            return 0;
        }

        private bool TryBuildShoreSandStrip(
            Vector2Int outerPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            out List<ShoreSandPlacement> placements)
        {
            placements = null;

            if (!TryGetPreferredSeaEdgeDirection(outerPoint, allLandPoints, out ShoreEdgeDirection oceanDirection))
            {
                return false;
            }

            Vector2Int inwardOffset = GetOppositeCardinalOffset(oceanDirection);
            Vector2Int middlePoint = outerPoint + inwardOffset;
            Vector2Int innerPoint = middlePoint + inwardOffset;

            ShoreEdgeDirection grassDirection = GetOppositeDirection(oceanDirection);
            placements = new List<ShoreSandPlacement>(3);
            placements.Add(new ShoreSandPlacement(
                outerPoint,
                shoreSandOceanTransitionPrefab,
                oceanDirection,
                true,
                true,
                false));

            if (IsGrassLandPoint(middlePoint, allLandPoints, areaByPoint))
            {
                placements.Add(new ShoreSandPlacement(
                    middlePoint,
                    shoreSandNormalPrefab,
                    oceanDirection,
                    false,
                    false,
                    false));

                if (IsGrassLandPoint(innerPoint, allLandPoints, areaByPoint))
                {
                    placements.Add(new ShoreSandPlacement(
                        innerPoint,
                        shoreSandGrassTransitionPrefab,
                        grassDirection,
                        false,
                        false,
                        true));
                }
            }

            return true;
        }

        private static bool TryResolvePreferredDirectSeaPlacement(List<ShoreSandPlacement> candidates, out ShoreSandPlacement directSeaPlacement)
        {
            directSeaPlacement = default;
            bool found = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (!IsOceanTransitionPlacement(candidates[i]))
                {
                    continue;
                }

                if (!found || CompareSeaDirectionPriority(candidates[i].direction, directSeaPlacement.direction) < 0)
                {
                    directSeaPlacement = candidates[i];
                    found = true;
                }
            }

            return found;
        }

        private bool TryResolveOceanOuterCornerPlacement(List<ShoreSandPlacement> candidates, out ShoreSandPlacement cornerPlacement)
        {
            cornerPlacement = default;
            if (shoreSandOceanOuterCornerPrefab == null)
            {
                return false;
            }

            return TryResolveAdjacentCornerPlacementFromCandidates(
                candidates,
                IsOceanTransitionPlacement,
                shoreSandOceanOuterCornerPrefab,
                false,
                true,
                false,
                out cornerPlacement);
        }

        private bool TryResolveOceanInnerCornerPlacement(List<ShoreSandPlacement> candidates, out ShoreSandPlacement cornerPlacement)
        {
            cornerPlacement = default;
            if (shoreSandOceanInnerCornerPrefab == null)
            {
                return false;
            }

            return TryResolveAdjacentCornerPlacementFromCandidates(
                candidates,
                IsNormalPlacement,
                shoreSandOceanInnerCornerPrefab,
                false,
                false,
                false,
                out cornerPlacement);
        }

        private bool TryResolveGrassInnerCornerPlacement(List<ShoreSandPlacement> candidates, out ShoreSandPlacement cornerPlacement)
        {
            cornerPlacement = default;
            if (shoreSandGrassInnerCornerPrefab == null)
            {
                return false;
            }

            return TryResolveAdjacentCornerPlacementFromCandidates(
                candidates,
                IsGrassTransitionPlacement,
                shoreSandGrassInnerCornerPrefab,
                false,
                false,
                true,
                out cornerPlacement);
        }

        private bool TryResolveAdjacentCornerPlacementFromCandidates(
            List<ShoreSandPlacement> candidates,
            System.Func<ShoreSandPlacement, bool> predicate,
            GameObject cornerPrefab,
            bool replacesGrassTile,
            bool marksAsBeach,
            bool usesGrassTransitionDirectionMapping,
            out ShoreSandPlacement cornerPlacement)
        {
            cornerPlacement = default;
            if (candidates == null || candidates.Count == 0 || predicate == null || cornerPrefab == null)
            {
                return false;
            }

            List<ShoreEdgeDirection> uniqueDirections = new List<ShoreEdgeDirection>(2);
            ShoreSandPlacement templatePlacement = default;
            bool hasTemplate = false;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (!predicate(candidates[i]))
                {
                    continue;
                }

                if (!hasTemplate)
                {
                    templatePlacement = candidates[i];
                    hasTemplate = true;
                }

                if (!uniqueDirections.Contains(candidates[i].direction))
                {
                    uniqueDirections.Add(candidates[i].direction);
                }
            }

            if (!hasTemplate || uniqueDirections.Count != 2)
            {
                return false;
            }

            if (!TryResolveAdjacentCornerYaw(uniqueDirections[0], uniqueDirections[1], out float cornerYaw))
            {
                return false;
            }

            cornerPlacement = new ShoreSandPlacement(
                templatePlacement.point,
                cornerPrefab,
                uniqueDirections[0],
                replacesGrassTile,
                marksAsBeach,
                usesGrassTransitionDirectionMapping,
                templatePlacement.grassNeighborCount,
                templatePlacement.usedFixedPrioritySelection,
                templatePlacement.fromAdjacentTwoGrass,
                true,
                cornerYaw);
            return true;
        }

        private void ApplyFinalGrassBoundaryCorrection(
            List<ShoreSandPlacement> placements,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint)
        {
            if (placements == null || placements.Count == 0)
            {
                return;
            }

            HashSet<Vector2Int> finalShoreSandPoints = new HashSet<Vector2Int>(placements.Count);
            for (int i = 0; i < placements.Count; i++)
            {
                finalShoreSandPoints.Add(placements[i].point);
            }

            for (int i = 0; i < placements.Count; i++)
            {
                ShoreSandPlacement placement = placements[i];
                if (IsOceanTransitionPlacement(placement))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LogGrassBoundaryFixDecision(placement, 0, null, false, "direct-sea-ocean-transition-kept");
#endif
                    continue;
                }

                int grassNeighborCount = CountOrdinaryGrassNeighborDirections(
                    placement.point,
                    allLandPoints,
                    areaByPoint,
                    finalShoreSandPoints,
                    out List<ShoreEdgeDirection> grassNeighborDirections,
                    out ShoreEdgeDirection grassNeighborDirection);

                if (grassNeighborCount == 1)
                {
                    ShoreSandPlacement previousPlacement = placement;
                    placements[i] = new ShoreSandPlacement(
                        placement.point,
                        shoreSandGrassTransitionPrefab,
                        grassNeighborDirection,
                        false,
                        false,
                        true,
                        grassNeighborCount);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LogGrassBoundaryFixDecision(previousPlacement, grassNeighborCount, grassNeighborDirections, true, "single-ordinary-grass-neighbor");
#endif
                }
                else if (grassNeighborCount == 2)
                {
                    bool isOppositeTwoGrass = IsOppositeGrassPair(grassNeighborDirections);
                    bool isAdjacentTwoGrass = !isOppositeTwoGrass && IsAdjacentGrassPair(grassNeighborDirections);

                    if (isAdjacentTwoGrass &&
                        shoreSandGrassOuterCornerPrefab != null &&
                        TryResolveAdjacentCornerYaw(grassNeighborDirections[0], grassNeighborDirections[1], out float grassOuterCornerYaw))
                    {
                        ShoreSandPlacement previousPlacement = placement;
                        placements[i] = new ShoreSandPlacement(
                            placement.point,
                            shoreSandGrassOuterCornerPrefab,
                            grassNeighborDirections[0],
                            false,
                            false,
                            false,
                            grassNeighborCount,
                            false,
                            true,
                            true,
                            grassOuterCornerYaw);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        LogGrassBoundaryFixDecision(
                            previousPlacement,
                            grassNeighborCount,
                            grassNeighborDirections,
                            true,
                            "adjacent-two-grass-outer-corner",
                            isAdjacentTwoGrass,
                            isOppositeTwoGrass,
                            null);
#endif
                    }
                    else if (isAdjacentTwoGrass &&
                        TryResolveAdjacentTwoGrassPrimaryDirection(
                            placement.point,
                            allLandPoints,
                            finalShoreSandPoints,
                            grassNeighborDirections,
                            out ShoreEdgeDirection primaryGrassDirection,
                            out string selectionReason))
                    {
                        ShoreSandPlacement previousPlacement = placement;
                        placements[i] = new ShoreSandPlacement(
                            placement.point,
                            shoreSandGrassTransitionPrefab,
                            primaryGrassDirection,
                            false,
                            false,
                            true,
                            grassNeighborCount,
                            selectionReason == "adjacent-two-grass-fixed-priority",
                            true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        LogGrassBoundaryFixDecision(
                            previousPlacement,
                            grassNeighborCount,
                            grassNeighborDirections,
                            true,
                            selectionReason,
                            isAdjacentTwoGrass,
                            isOppositeTwoGrass,
                            primaryGrassDirection);
#endif
                    }
                    else
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        string reason = isOppositeTwoGrass
                            ? "opposite-two-grass-kept-normal"
                            : "adjacent-two-grass-kept-normal";
                        LogGrassBoundaryFixDecision(
                            placement,
                            grassNeighborCount,
                            grassNeighborDirections,
                            false,
                            reason,
                            isAdjacentTwoGrass,
                            isOppositeTwoGrass,
                            null);
#endif
                    }
                }
                else
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    string reason = grassNeighborCount <= 0
                        ? "no-ordinary-grass-neighbor"
                        : "three-or-more-ordinary-grass-neighbors";
                    LogGrassBoundaryFixDecision(placement, grassNeighborCount, grassNeighborDirections, false, reason, false, false, null);
#endif
                }
            }
        }

        private static int CountOrdinaryGrassNeighborDirections(
            Vector2Int point,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> finalShoreSandPoints,
            out List<ShoreEdgeDirection> grassNeighborDirections,
            out ShoreEdgeDirection grassNeighborDirection)
        {
            grassNeighborDirections = new List<ShoreEdgeDirection>(4);
            grassNeighborDirection = ShoreEdgeDirection.Up;

            if (IsOrdinaryGrassNeighbor(point + Vector2Int.up, allLandPoints, areaByPoint, finalShoreSandPoints))
            {
                grassNeighborDirection = ShoreEdgeDirection.Up;
                grassNeighborDirections.Add(ShoreEdgeDirection.Up);
            }

            if (IsOrdinaryGrassNeighbor(point + Vector2Int.down, allLandPoints, areaByPoint, finalShoreSandPoints))
            {
                grassNeighborDirection = ShoreEdgeDirection.Down;
                grassNeighborDirections.Add(ShoreEdgeDirection.Down);
            }

            if (IsOrdinaryGrassNeighbor(point + Vector2Int.left, allLandPoints, areaByPoint, finalShoreSandPoints))
            {
                grassNeighborDirection = ShoreEdgeDirection.Left;
                grassNeighborDirections.Add(ShoreEdgeDirection.Left);
            }

            if (IsOrdinaryGrassNeighbor(point + Vector2Int.right, allLandPoints, areaByPoint, finalShoreSandPoints))
            {
                grassNeighborDirection = ShoreEdgeDirection.Right;
                grassNeighborDirections.Add(ShoreEdgeDirection.Right);
            }

            return grassNeighborDirections.Count;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogGrassBoundaryFixDecision(
            ShoreSandPlacement placement,
            int grassNeighborCount,
            List<ShoreEdgeDirection> grassNeighborDirections,
            bool changedToGrassTransition,
            string reason,
            bool isAdjacentTwoGrass = false,
            bool isOppositeTwoGrass = false,
            ShoreEdgeDirection? selectedPrimaryGrassDirection = null)
        {
            if (!debugShoreSandPlacements)
            {
                return;
            }

            string directionList = grassNeighborDirections == null || grassNeighborDirections.Count == 0
                ? "None"
                : string.Join(",", grassNeighborDirections);
            string selectedDirection = selectedPrimaryGrassDirection.HasValue
                ? selectedPrimaryGrassDirection.Value.ToString()
                : "None";

            Debug.Log(
                $"[ShoreSand.GrassBoundaryFix] point={placement.point} beforeType={GetShoreSandPlacementDebugType(placement)} grassNeighborCount={grassNeighborCount} grassNeighborDirs={directionList} adjacentTwoGrass={isAdjacentTwoGrass} oppositeTwoGrass={isOppositeTwoGrass} changedToGrassTransition={changedToGrassTransition} selectedPrimaryGrassDir={selectedDirection} reason={reason}",
                this);
        }

        private void ApplyShoreSandDebugName(GameObject instance, ShoreSandPlacement placement, Vector2Int point)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!debugShoreSandPlacements || instance == null)
            {
                return;
            }

            instance.name = BuildShoreSandDebugInstanceName(placement, point);
#endif
        }

        private string BuildShoreSandDebugInstanceName(ShoreSandPlacement placement, Vector2Int point)
        {
            string typeName = GetShoreSandPlacementDebugType(placement);
            if (placement.usesExplicitYaw)
            {
                return $"{typeName}_({point.x},{point.y})_Yaw_{NormalizeYaw(placement.explicitYaw):F0}";
            }

            if (placement.usesGrassTransitionDirectionMapping)
            {
                return $"{typeName}_({point.x},{point.y})_GrassDir_{placement.direction}";
            }

            if (IsOceanTransitionPlacement(placement))
            {
                return $"{typeName}_({point.x},{point.y})_Dir_{placement.direction}";
            }

            return $"{typeName}_({point.x},{point.y})";
        }

        private string GetShoreSandPlacementDebugType(ShoreSandPlacement placement)
        {
            if (IsOceanOuterCornerPlacement(placement))
            {
                return "ShoreSand_OceanOuterCorner";
            }

            if (IsOceanInnerCornerPlacement(placement))
            {
                return "ShoreSand_OceanInnerCorner";
            }

            if (IsGrassOuterCornerPlacement(placement))
            {
                return "ShoreSand_GrassOuterCorner";
            }

            if (IsGrassInnerCornerPlacement(placement))
            {
                return "ShoreSand_GrassInnerCorner";
            }

            if (placement.usesGrassTransitionDirectionMapping)
            {
                return "ShoreSand_GrassTransition";
            }

            if (IsOceanTransitionPlacement(placement))
            {
                return "ShoreSand_OceanTransition";
            }

            return "ShoreSand_Normal";
        }

        private static bool IsOrdinaryGrassNeighbor(
            Vector2Int point,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> finalShoreSandPoints)
        {
            return allLandPoints.Contains(point) &&
                   !finalShoreSandPoints.Contains(point) &&
                   areaByPoint.TryGetValue(point, out AreaType areaType) &&
                   areaType == AreaType.Grass;
        }

        private void ApplyGrassTransitionAdjacencyFix(List<ShoreSandPlacement> placements)
        {
            if (placements == null || placements.Count <= 1)
            {
                return;
            }

            Dictionary<Vector2Int, int> indexByPoint = new Dictionary<Vector2Int, int>(placements.Count);
            for (int i = 0; i < placements.Count; i++)
            {
                indexByPoint[placements[i].point] = i;
            }

            HashSet<Vector2Int> pointsToDowngrade = new HashSet<Vector2Int>();

            for (int i = 0; i < placements.Count; i++)
            {
                ShoreSandPlacement placement = placements[i];
                if (!placement.usesGrassTransitionDirectionMapping)
                {
                    continue;
                }

                TryEvaluateGrassTransitionAdjacencyPair(placement, Vector2Int.right, placements, indexByPoint, pointsToDowngrade);
                TryEvaluateGrassTransitionAdjacencyPair(placement, Vector2Int.up, placements, indexByPoint, pointsToDowngrade);
            }

            foreach (Vector2Int point in pointsToDowngrade)
            {
                if (!indexByPoint.TryGetValue(point, out int index))
                {
                    continue;
                }

                placements[index] = new ShoreSandPlacement(
                    point,
                    shoreSandNormalPrefab,
                    ShoreEdgeDirection.Up,
                    false,
                    false,
                    false);
            }
        }

        private void TryEvaluateGrassTransitionAdjacencyPair(
            ShoreSandPlacement placementA,
            Vector2Int neighborOffset,
            List<ShoreSandPlacement> placements,
            Dictionary<Vector2Int, int> indexByPoint,
            HashSet<Vector2Int> pointsToDowngrade)
        {
            Vector2Int neighborPoint = placementA.point + neighborOffset;
            if (!indexByPoint.TryGetValue(neighborPoint, out int neighborIndex))
            {
                return;
            }

            ShoreSandPlacement placementB = placements[neighborIndex];
            if (!placementB.usesGrassTransitionDirectionMapping)
            {
                return;
            }

            string conflictType;
            if (placementA.direction == placementB.direction)
            {
                conflictType = "same";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogGrassTransitionAdjacencyFix(placementA, placementB, conflictType, null, "same-direction-kept");
#endif
                return;
            }

            if (GetOppositeDirection(placementA.direction) == placementB.direction)
            {
                conflictType = "opposite";
            }
            else
            {
                conflictType = "perpendicular";
            }

            ShoreSandPlacement downgradedPlacement;
            string downgradeReason;
            SelectLessStableGrassTransitionPlacement(placementA, placementB, out downgradedPlacement, out downgradeReason);
            pointsToDowngrade.Add(downgradedPlacement.point);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogGrassTransitionAdjacencyFix(placementA, placementB, conflictType, downgradedPlacement.point, downgradeReason);
#endif
        }

        private static void SelectLessStableGrassTransitionPlacement(
            ShoreSandPlacement placementA,
            ShoreSandPlacement placementB,
            out ShoreSandPlacement downgradedPlacement,
            out string downgradeReason)
        {
            if (placementA.grassNeighborCount != placementB.grassNeighborCount)
            {
                downgradedPlacement = placementA.grassNeighborCount > placementB.grassNeighborCount ? placementA : placementB;
                downgradeReason = "higher-grass-neighbor-count";
                return;
            }

            if (placementA.usedFixedPrioritySelection != placementB.usedFixedPrioritySelection)
            {
                downgradedPlacement = placementA.usedFixedPrioritySelection ? placementA : placementB;
                downgradeReason = "fixed-priority-selection";
                return;
            }

            if (placementA.fromAdjacentTwoGrass != placementB.fromAdjacentTwoGrass)
            {
                downgradedPlacement = placementA.fromAdjacentTwoGrass ? placementA : placementB;
                downgradeReason = "adjacent-two-grass-less-stable";
                return;
            }

            downgradedPlacement = ComparePointOrder(placementA.point, placementB.point) >= 0 ? placementA : placementB;
            downgradeReason = "stable-coordinate-order";
        }

        private static int ComparePointOrder(Vector2Int lhs, Vector2Int rhs)
        {
            int compareX = lhs.x.CompareTo(rhs.x);
            if (compareX != 0)
            {
                return compareX;
            }

            return lhs.y.CompareTo(rhs.y);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogGrassTransitionAdjacencyFix(
            ShoreSandPlacement placementA,
            ShoreSandPlacement placementB,
            string conflictType,
            Vector2Int? downgradedPoint,
            string downgradeReason)
        {
            if (!debugShoreSandPlacements)
            {
                return;
            }

            string downgradedLabel = downgradedPoint.HasValue
                ? $"({downgradedPoint.Value.x},{downgradedPoint.Value.y})"
                : "None";

            Debug.Log(
                $"[ShoreSand.GrassTransitionAdjacencyFix] pointA={placementA.point} pointB={placementB.point} dirA={placementA.direction} dirB={placementB.direction} conflict={conflictType} downgraded={downgradedLabel} reason={downgradeReason}",
                this);
        }

        private static bool IsOppositeGrassPair(List<ShoreEdgeDirection> grassNeighborDirections)
        {
            if (grassNeighborDirections == null || grassNeighborDirections.Count != 2)
            {
                return false;
            }

            ShoreEdgeDirection first = grassNeighborDirections[0];
            ShoreEdgeDirection second = grassNeighborDirections[1];
            return GetOppositeDirection(first) == second;
        }

        private static bool IsAdjacentGrassPair(List<ShoreEdgeDirection> grassNeighborDirections)
        {
            return grassNeighborDirections != null &&
                   grassNeighborDirections.Count == 2 &&
                   !IsOppositeGrassPair(grassNeighborDirections);
        }

        private static bool TryResolveAdjacentTwoGrassPrimaryDirection(
            Vector2Int point,
            HashSet<Vector2Int> allLandPoints,
            HashSet<Vector2Int> finalShoreSandPoints,
            List<ShoreEdgeDirection> grassNeighborDirections,
            out ShoreEdgeDirection primaryGrassDirection,
            out string selectionReason)
        {
            primaryGrassDirection = ShoreEdgeDirection.Up;
            selectionReason = "adjacent-two-grass-fixed-priority";

            if (!IsAdjacentGrassPair(grassNeighborDirections))
            {
                return false;
            }

            List<ShoreEdgeDirection> seaDirections = CollectSeaEdgeDirections(point, allLandPoints);
            int bestScore = int.MinValue;
            bool hasBest = false;

            for (int i = 0; i < grassNeighborDirections.Count; i++)
            {
                ShoreEdgeDirection candidate = grassNeighborDirections[i];
                int score = ScoreAdjacentGrassPrimaryDirection(point, candidate, seaDirections, finalShoreSandPoints);
                if (!hasBest || score > bestScore)
                {
                    primaryGrassDirection = candidate;
                    bestScore = score;
                    hasBest = true;
                    continue;
                }

                if (score == bestScore && CompareSeaDirectionPriority(candidate, primaryGrassDirection) < 0)
                {
                    primaryGrassDirection = candidate;
                }
            }

            bool tieOnScore = false;
            if (hasBest)
            {
                int matchCount = 0;
                for (int i = 0; i < grassNeighborDirections.Count; i++)
                {
                    if (ScoreAdjacentGrassPrimaryDirection(point, grassNeighborDirections[i], seaDirections, finalShoreSandPoints) == bestScore)
                    {
                        matchCount++;
                    }
                }

                tieOnScore = matchCount > 1;
            }

            selectionReason = tieOnScore
                ? "adjacent-two-grass-fixed-priority"
                : "adjacent-two-grass-sea-shore-informed";

            return hasBest;
        }

        private static int ScoreAdjacentGrassPrimaryDirection(
            Vector2Int point,
            ShoreEdgeDirection candidate,
            List<ShoreEdgeDirection> seaDirections,
            HashSet<Vector2Int> finalShoreSandPoints)
        {
            int score = 0;
            ShoreEdgeDirection oppositeDirection = GetOppositeDirection(candidate);

            for (int i = 0; i < seaDirections.Count; i++)
            {
                if (seaDirections[i] == oppositeDirection)
                {
                    score += 4;
                }
            }

            Vector2Int oppositePoint = point + GetOppositeCardinalOffset(candidate);
            if (finalShoreSandPoints.Contains(oppositePoint))
            {
                score += 2;
            }

            return score;
        }

        private static bool ShouldDowngradeToShoreSandNormal(List<ShoreSandPlacement> candidates)
        {
            if (candidates == null || candidates.Count <= 1)
            {
                return false;
            }

            ShoreSandPlacement first = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                ShoreSandPlacement other = candidates[i];
                if (other.direction != first.direction ||
                    other.replacesGrassTile != first.replacesGrassTile ||
                    other.marksAsBeach != first.marksAsBeach ||
                    other.usesGrassTransitionDirectionMapping != first.usesGrassTransitionDirectionMapping ||
                    other.prefab != first.prefab)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOceanTransitionPlacement(ShoreSandPlacement placement)
        {
            return placement.replacesGrassTile && placement.marksAsBeach && !placement.usesGrassTransitionDirectionMapping;
        }

        private bool IsOceanOuterCornerPlacement(ShoreSandPlacement placement)
        {
            return shoreSandOceanOuterCornerPrefab != null && placement.prefab == shoreSandOceanOuterCornerPrefab;
        }

        private bool IsOceanInnerCornerPlacement(ShoreSandPlacement placement)
        {
            return shoreSandOceanInnerCornerPrefab != null && placement.prefab == shoreSandOceanInnerCornerPrefab;
        }

        private bool IsGrassOuterCornerPlacement(ShoreSandPlacement placement)
        {
            return shoreSandGrassOuterCornerPrefab != null && placement.prefab == shoreSandGrassOuterCornerPrefab;
        }

        private bool IsGrassInnerCornerPlacement(ShoreSandPlacement placement)
        {
            return shoreSandGrassInnerCornerPrefab != null && placement.prefab == shoreSandGrassInnerCornerPrefab;
        }

        private static bool IsNormalPlacement(ShoreSandPlacement placement)
        {
            return !placement.replacesGrassTile &&
                   !placement.marksAsBeach &&
                   !placement.usesGrassTransitionDirectionMapping &&
                   !placement.usesExplicitYaw;
        }

        private static bool IsGrassTransitionPlacement(ShoreSandPlacement placement)
        {
            return placement.usesGrassTransitionDirectionMapping;
        }

        private static bool IsGrassLandPoint(
            Vector2Int point,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint)
        {
            return allLandPoints.Contains(point) &&
                   areaByPoint.TryGetValue(point, out AreaType areaType) &&
                   areaType == AreaType.Grass;
        }

        private static bool TryGetSingleSeaEdgeDirection(
            Vector2Int point,
            HashSet<Vector2Int> allLandPoints,
            out ShoreEdgeDirection shoreDirection)
        {
            shoreDirection = ShoreEdgeDirection.Up;
            int seaSideCount = 0;

            if (!allLandPoints.Contains(point + Vector2Int.up))
            {
                shoreDirection = ShoreEdgeDirection.Up;
                seaSideCount++;
            }

            if (!allLandPoints.Contains(point + Vector2Int.down))
            {
                shoreDirection = ShoreEdgeDirection.Down;
                seaSideCount++;
            }

            if (!allLandPoints.Contains(point + Vector2Int.left))
            {
                shoreDirection = ShoreEdgeDirection.Left;
                seaSideCount++;
            }

            if (!allLandPoints.Contains(point + Vector2Int.right))
            {
                shoreDirection = ShoreEdgeDirection.Right;
                seaSideCount++;
            }

            return seaSideCount == 1;
        }

        private static bool TryGetPreferredSeaEdgeDirection(
            Vector2Int point,
            HashSet<Vector2Int> allLandPoints,
            out ShoreEdgeDirection shoreDirection)
        {
            shoreDirection = ShoreEdgeDirection.Up;
            List<ShoreEdgeDirection> seaDirections = CollectSeaEdgeDirections(point, allLandPoints);
            if (seaDirections.Count == 0)
            {
                return false;
            }

            shoreDirection = seaDirections[0];
            return true;
        }

        private static List<ShoreEdgeDirection> CollectSeaEdgeDirections(Vector2Int point, HashSet<Vector2Int> allLandPoints)
        {
            List<ShoreEdgeDirection> seaDirections = new List<ShoreEdgeDirection>(4);

            if (!allLandPoints.Contains(point + Vector2Int.up))
            {
                seaDirections.Add(ShoreEdgeDirection.Up);
            }

            if (!allLandPoints.Contains(point + Vector2Int.right))
            {
                seaDirections.Add(ShoreEdgeDirection.Right);
            }

            if (!allLandPoints.Contains(point + Vector2Int.down))
            {
                seaDirections.Add(ShoreEdgeDirection.Down);
            }

            if (!allLandPoints.Contains(point + Vector2Int.left))
            {
                seaDirections.Add(ShoreEdgeDirection.Left);
            }

            return seaDirections;
        }

        private static bool TryResolveAdjacentCornerYaw(
            ShoreEdgeDirection directionA,
            ShoreEdgeDirection directionB,
            out float cornerYaw)
        {
            cornerYaw = 0f;

            bool hasUp = directionA == ShoreEdgeDirection.Up || directionB == ShoreEdgeDirection.Up;
            bool hasRight = directionA == ShoreEdgeDirection.Right || directionB == ShoreEdgeDirection.Right;
            bool hasDown = directionA == ShoreEdgeDirection.Down || directionB == ShoreEdgeDirection.Down;
            bool hasLeft = directionA == ShoreEdgeDirection.Left || directionB == ShoreEdgeDirection.Left;

            if (hasUp && hasRight)
            {
                cornerYaw = 0f;
                return true;
            }

            if (hasRight && hasDown)
            {
                cornerYaw = 90f;
                return true;
            }

            if (hasDown && hasLeft)
            {
                cornerYaw = 180f;
                return true;
            }

            if (hasLeft && hasUp)
            {
                cornerYaw = 270f;
                return true;
            }

            return false;
        }

        private static int CompareSeaDirectionPriority(ShoreEdgeDirection lhs, ShoreEdgeDirection rhs)
        {
            return GetSeaDirectionPriority(lhs).CompareTo(GetSeaDirectionPriority(rhs));
        }

        private static int GetSeaDirectionPriority(ShoreEdgeDirection direction)
        {
            switch (direction)
            {
                case ShoreEdgeDirection.Up:
                    return 0;
                case ShoreEdgeDirection.Right:
                    return 1;
                case ShoreEdgeDirection.Down:
                    return 2;
                case ShoreEdgeDirection.Left:
                    return 3;
                default:
                    return int.MaxValue;
            }
        }

        private static ShoreEdgeDirection GetOppositeDirection(ShoreEdgeDirection direction)
        {
            switch (direction)
            {
                case ShoreEdgeDirection.Up:
                    return ShoreEdgeDirection.Down;
                case ShoreEdgeDirection.Down:
                    return ShoreEdgeDirection.Up;
                case ShoreEdgeDirection.Left:
                    return ShoreEdgeDirection.Right;
                case ShoreEdgeDirection.Right:
                    return ShoreEdgeDirection.Left;
                default:
                    return ShoreEdgeDirection.Up;
            }
        }

        private static Vector2Int GetOppositeCardinalOffset(ShoreEdgeDirection direction)
        {
            switch (direction)
            {
                case ShoreEdgeDirection.Up:
                    return Vector2Int.down;
                case ShoreEdgeDirection.Down:
                    return Vector2Int.up;
                case ShoreEdgeDirection.Left:
                    return Vector2Int.right;
                case ShoreEdgeDirection.Right:
                    return Vector2Int.left;
                default:
                    return Vector2Int.zero;
            }
        }

        private static float ResolveShoreSandYaw(ShoreEdgeDirection shoreDirection)
        {
            switch (shoreDirection)
            {
                case ShoreEdgeDirection.Up:
                    return 0f;
                case ShoreEdgeDirection.Right:
                    return 90f;
                case ShoreEdgeDirection.Down:
                    return 180f;
                case ShoreEdgeDirection.Left:
                    return 270f;
                default:
                    return 0f;
            }
        }

        private static float ResolveGrassTransitionYaw(ShoreEdgeDirection inlandGrassDirection)
        {
            switch (inlandGrassDirection)
            {
                case ShoreEdgeDirection.Down:
                    return 180f;
                case ShoreEdgeDirection.Left:
                    return 270f;
                case ShoreEdgeDirection.Up:
                    return 0f;
                case ShoreEdgeDirection.Right:
                    return 90f;
                default:
                    return 0f;
            }
        }

        private static float NormalizeYaw(float yaw)
        {
            yaw %= 360f;
            if (yaw < 0f)
            {
                yaw += 360f;
            }

            return yaw;
        }

        private Transform ResolveGeneratedShoreSandParent()
        {
            if (shoreSandParent != null)
            {
                return shoreSandParent;
            }

            Transform existing = transform.Find(GeneratedShoreSandRootName);
            if (existing != null)
            {
                shoreSandParent = existing;
                return shoreSandParent;
            }

            GameObject parentObject = new GameObject(GeneratedShoreSandRootName);
            parentObject.transform.SetParent(transform, false);
            shoreSandParent = parentObject.transform;
            return shoreSandParent;
        }

        private void ClearGeneratedShoreSandInstances()
        {
            Transform parent = shoreSandParent != null ? shoreSandParent : transform.Find(GeneratedShoreSandRootName);
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
        #endregion
    }
}
