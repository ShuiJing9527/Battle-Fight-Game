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
        [SerializeField, Min(3)] private int shoreSandWidth = 5;
        [SerializeField, Min(2)] private int minimumShoreSandFootprint = 2;
        [SerializeField] private bool debugShoreSandPlacements = false;
        [SerializeField] private bool debugShoreSandDecisionTrace = false;
        [SerializeField] private Vector2Int debugShoreSandGridPoint;

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
        [SerializeField, Min(1)] private int minimumTerrainFeatureWidth = 4;
        [SerializeField] private bool enableShorelineMicroCleanup = true;
        [SerializeField, Range(1, 2)] private int shorelineMicroCleanupSize = 2;

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
        private HashSet<Vector2Int> currentExteriorOceanPoints;
        private HashSet<Vector2Int> currentShoreWaterPoints;
        private int currentEnclosedWaterPointCount;
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
            public bool hasSecondaryDirection;
            public ShoreEdgeDirection secondaryDirection;

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
                float explicitYaw = 0f,
                bool hasSecondaryDirection = false,
                ShoreEdgeDirection secondaryDirection = ShoreEdgeDirection.Up)
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
                this.hasSecondaryDirection = hasSecondaryDirection;
                this.secondaryDirection = secondaryDirection;
            }
        }

        private struct ShorelineJunctionFillCandidate
        {
            public Vector2Int point;
            public int score;
            public Vector2Int sourceBlockOrigin;
            public string sourceKind;

            public ShorelineJunctionFillCandidate(
                Vector2Int point,
                int score,
                Vector2Int sourceBlockOrigin,
                string sourceKind)
            {
                this.point = point;
                this.score = score;
                this.sourceBlockOrigin = sourceBlockOrigin;
                this.sourceKind = sourceKind;
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

            NormalizeBaseTerrainMinimumWidth();

            if (enableShorelineMicroCleanup)
            {
                ApplyShorelineMicroCleanup();
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

        private void NormalizeBaseTerrainMinimumWidth()
        {
            if (floorPoints == null || minimumTerrainFeatureWidth < 2)
            {
                return;
            }

            int iterationLimit = Mathf.Max(1, cleanupIterations * 3);
            for (int iteration = 0; iteration < iterationLimit; iteration++)
            {
                HashSet<Vector2Int> allFloorPoints = RebuildAllFloorPoints();
                if (allFloorPoints.Count == 0)
                {
                    return;
                }

                ActualFloorBoundsInfo bounds = CalculateActualFloorBounds(allFloorPoints);
                if (!bounds.isValid)
                {
                    return;
                }

                HashSet<Vector2Int> pointsToFill = new HashSet<Vector2Int>();
                HashSet<Vector2Int> pointsToRemove = new HashSet<Vector2Int>();

                CollectNarrowLandFeatureChanges(allFloorPoints, bounds, pointsToFill, pointsToRemove);
                CollectNarrowOceanFeatureFills(allFloorPoints, bounds, pointsToFill);

                if (pointsToFill.Count == 0 && pointsToRemove.Count == 0)
                {
                    break;
                }

                ApplyFloorPointFills(pointsToFill);
                ApplyFloorPointRemovals(pointsToRemove);
            }
        }

        private void ApplyShorelineMicroCleanup()
        {
            if (floorPoints == null)
            {
                return;
            }

            int iterationCount = Mathf.Clamp(shorelineMicroCleanupSize, 1, 2);
            for (int iteration = 0; iteration < iterationCount; iteration++)
            {
                HashSet<Vector2Int> allFloorPoints = RebuildAllFloorPoints();
                if (allFloorPoints.Count == 0)
                {
                    return;
                }

                ActualFloorBoundsInfo bounds = CalculateActualFloorBounds(allFloorPoints);
                if (!bounds.isValid)
                {
                    return;
                }

                HashSet<Vector2Int> shorelineWaterPoints = BuildFiniteOceanPointSet(
                    allFloorPoints,
                    bounds,
                    Mathf.Max(1, shorelineMicroCleanupSize));

                HashSet<Vector2Int> pointsToFill = new HashSet<Vector2Int>();
                CollectShorelineSingleCellPocketFills(allFloorPoints, shorelineWaterPoints, bounds, pointsToFill);
                if (shorelineMicroCleanupSize >= 2)
                {
                    CollectShorelineShortPocketFills(allFloorPoints, shorelineWaterPoints, bounds, pointsToFill);
                }

                if (pointsToFill.Count > 0)
                {
                    ApplyFloorPointFills(pointsToFill);
                    allFloorPoints = RebuildAllFloorPoints();
                    bounds = CalculateActualFloorBounds(allFloorPoints);
                    shorelineWaterPoints = BuildFiniteOceanPointSet(
                        allFloorPoints,
                        bounds,
                        Mathf.Max(1, shorelineMicroCleanupSize));
                }

                HashSet<Vector2Int> junctionPointsToFill = new HashSet<Vector2Int>();
                CollectShorelineJunctionFixes(allFloorPoints, shorelineWaterPoints, bounds, junctionPointsToFill);
                if (junctionPointsToFill.Count > 0)
                {
                    ApplyFloorPointFills(junctionPointsToFill);
                    allFloorPoints = RebuildAllFloorPoints();
                    bounds = CalculateActualFloorBounds(allFloorPoints);
                    shorelineWaterPoints = BuildFiniteOceanPointSet(
                        allFloorPoints,
                        bounds,
                        Mathf.Max(1, shorelineMicroCleanupSize));
                }

                HashSet<Vector2Int> pointsToRemove = new HashSet<Vector2Int>();
                CollectShorelineSingleCellSpikeRemovals(allFloorPoints, shorelineWaterPoints, bounds, pointsToRemove);
                if (shorelineMicroCleanupSize >= 2)
                {
                    CollectShorelineTwoCellSpikeRemovals(allFloorPoints, shorelineWaterPoints, bounds, pointsToRemove);
                }

                if (pointsToRemove.Count > 0)
                {
                    ApplyFloorPointRemovals(pointsToRemove);
                }

                LogShorelineMicroCleanupIteration(iteration, pointsToFill, junctionPointsToFill, pointsToRemove);

                if (pointsToFill.Count == 0 && junctionPointsToFill.Count == 0 && pointsToRemove.Count == 0)
                {
                    break;
                }
            }
        }

        private void CollectShorelineJunctionFixes(
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> shorelineWaterPoints,
            ActualFloorBoundsInfo bounds,
            HashSet<Vector2Int> pointsToFill)
        {
            Dictionary<Vector2Int, ShorelineJunctionFillCandidate> candidateByPoint =
                new Dictionary<Vector2Int, ShorelineJunctionFillCandidate>();

            CollectDiagonalSaddleJunctionCandidates(
                floorPointsSnapshot,
                shorelineWaterPoints,
                bounds,
                candidateByPoint);
            CollectShortStepJunctionCandidates(
                floorPointsSnapshot,
                shorelineWaterPoints,
                bounds,
                candidateByPoint);

            if (candidateByPoint.Count == 0)
            {
                return;
            }

            List<ShorelineJunctionFillCandidate> orderedCandidates =
                new List<ShorelineJunctionFillCandidate>(candidateByPoint.Values);
            orderedCandidates.Sort((lhs, rhs) =>
            {
                int scoreCompare = rhs.score.CompareTo(lhs.score);
                return scoreCompare != 0
                    ? scoreCompare
                    : ComparePointOrder(lhs.point, rhs.point);
            });

            HashSet<Vector2Int> simulatedFloorPoints = new HashSet<Vector2Int>(floorPointsSnapshot);
            HashSet<Vector2Int> simulatedWaterPoints = new HashSet<Vector2Int>(shorelineWaterPoints);

            for (int i = 0; i < orderedCandidates.Count; i++)
            {
                ShorelineJunctionFillCandidate candidate = orderedCandidates[i];
                if (pointsToFill.Contains(candidate.point))
                {
                    continue;
                }

                if (!CanSafelyFillShorelineJunctionPoint(candidate.point, simulatedFloorPoints, simulatedWaterPoints, bounds))
                {
                    LogShorelineJunctionCandidateRejected(candidate, "InsufficientOrthogonalSupport");
                    continue;
                }

                bool matchesDiagonalSaddle = MatchesDiagonalSaddlePatternAtPoint(
                    candidate.point,
                    simulatedFloorPoints,
                    simulatedWaterPoints,
                    out _);
                bool matchesStepJunction = MatchesShortStepJunctionPatternAtPoint(
                    candidate.point,
                    simulatedFloorPoints,
                    simulatedWaterPoints,
                    out _,
                    out _);

                if (!matchesDiagonalSaddle && !matchesStepJunction)
                {
                    LogShorelineJunctionCandidateRejected(candidate, "CandidateConflict");
                    continue;
                }

                pointsToFill.Add(candidate.point);
                simulatedFloorPoints.Add(candidate.point);
                simulatedWaterPoints.Remove(candidate.point);
                LogShorelineJunctionCandidateAccepted(candidate);
            }
        }

        private void CollectDiagonalSaddleJunctionCandidates(
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> shorelineWaterPoints,
            ActualFloorBoundsInfo bounds,
            Dictionary<Vector2Int, ShorelineJunctionFillCandidate> candidateByPoint)
        {
            if (!bounds.isValid)
            {
                return;
            }

            for (int x = bounds.min.x - 1; x <= bounds.max.x; x++)
            {
                for (int y = bounds.min.y - 1; y <= bounds.max.y; y++)
                {
                    Vector2Int origin = new Vector2Int(x, y);
                    Vector2Int bottomLeft = origin;
                    Vector2Int bottomRight = origin + Vector2Int.right;
                    Vector2Int topLeft = origin + Vector2Int.up;
                    Vector2Int topRight = origin + Vector2Int.up + Vector2Int.right;

                    bool bottomLeftLand = floorPointsSnapshot.Contains(bottomLeft);
                    bool bottomRightLand = floorPointsSnapshot.Contains(bottomRight);
                    bool topLeftLand = floorPointsSnapshot.Contains(topLeft);
                    bool topRightLand = floorPointsSnapshot.Contains(topRight);

                    int landCount = (bottomLeftLand ? 1 : 0) +
                                    (bottomRightLand ? 1 : 0) +
                                    (topLeftLand ? 1 : 0) +
                                    (topRightLand ? 1 : 0);
                    if (landCount != 2)
                    {
                        continue;
                    }

                    if (bottomLeftLand && topRightLand && !bottomRightLand && !topLeftLand)
                    {
                        TryRegisterShorelineJunctionCandidate(
                            bottomRight,
                            origin,
                            "DiagonalSaddle",
                            floorPointsSnapshot,
                            shorelineWaterPoints,
                            bounds,
                            candidateByPoint);
                        TryRegisterShorelineJunctionCandidate(
                            topLeft,
                            origin,
                            "DiagonalSaddle",
                            floorPointsSnapshot,
                            shorelineWaterPoints,
                            bounds,
                            candidateByPoint);
                    }
                    else if (!bottomLeftLand && bottomRightLand && topLeftLand && !topRightLand)
                    {
                        TryRegisterShorelineJunctionCandidate(
                            bottomLeft,
                            origin,
                            "DiagonalSaddle",
                            floorPointsSnapshot,
                            shorelineWaterPoints,
                            bounds,
                            candidateByPoint);
                        TryRegisterShorelineJunctionCandidate(
                            topRight,
                            origin,
                            "DiagonalSaddle",
                            floorPointsSnapshot,
                            shorelineWaterPoints,
                            bounds,
                            candidateByPoint);
                    }
                }
            }
        }

        private void CollectShortStepJunctionCandidates(
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> shorelineWaterPoints,
            ActualFloorBoundsInfo bounds,
            Dictionary<Vector2Int, ShorelineJunctionFillCandidate> candidateByPoint)
        {
            foreach (Vector2Int waterPoint in shorelineWaterPoints)
            {
                if (floorPointsSnapshot.Contains(waterPoint) ||
                    !IsPointInsideExpandedBounds(waterPoint, bounds, shorelineMicroCleanupSize))
                {
                    continue;
                }

                if (!MatchesShortStepJunctionPatternAtPoint(
                        waterPoint,
                        floorPointsSnapshot,
                        shorelineWaterPoints,
                        out _,
                        out string rejectionReason))
                {
                    if (rejectionReason == "OpensIntoLargeWater" || rejectionReason == "LongStep")
                    {
                        LogShorelineJunctionCandidateRejected(
                            new ShorelineJunctionFillCandidate(waterPoint, 0, waterPoint, "StepJunction"),
                            rejectionReason);
                    }

                    continue;
                }

                TryRegisterShorelineJunctionCandidate(
                    waterPoint,
                    waterPoint,
                    "StepJunction",
                    floorPointsSnapshot,
                    shorelineWaterPoints,
                    bounds,
                    candidateByPoint);
            }
        }

        private void TryRegisterShorelineJunctionCandidate(
            Vector2Int candidatePoint,
            Vector2Int sourceBlockOrigin,
            string sourceKind,
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> shorelineWaterPoints,
            ActualFloorBoundsInfo bounds,
            Dictionary<Vector2Int, ShorelineJunctionFillCandidate> candidateByPoint)
        {
            if (!CanSafelyFillShorelineJunctionPoint(candidatePoint, floorPointsSnapshot, shorelineWaterPoints, bounds))
            {
                return;
            }

            int candidateScore = ScoreShorelineJunctionFillCandidate(
                candidatePoint,
                floorPointsSnapshot,
                shorelineWaterPoints);
            if (candidateScore <= 0)
            {
                return;
            }

            ShorelineJunctionFillCandidate candidate = new ShorelineJunctionFillCandidate(
                candidatePoint,
                candidateScore,
                sourceBlockOrigin,
                sourceKind);

            if (!candidateByPoint.TryGetValue(candidatePoint, out ShorelineJunctionFillCandidate existingCandidate) ||
                candidateScore > existingCandidate.score ||
                (candidateScore == existingCandidate.score &&
                 ComparePointOrder(candidate.sourceBlockOrigin, existingCandidate.sourceBlockOrigin) < 0))
            {
                candidateByPoint[candidatePoint] = candidate;
            }
        }

        private void CollectShorelineSingleCellPocketFills(
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> shorelineWaterPoints,
            ActualFloorBoundsInfo bounds,
            HashSet<Vector2Int> pointsToFill)
        {
            foreach (Vector2Int waterPoint in shorelineWaterPoints)
            {
                if (floorPointsSnapshot.Contains(waterPoint) ||
                    pointsToFill.Contains(waterPoint) ||
                    !IsPointInsideExpandedBounds(waterPoint, bounds, shorelineMicroCleanupSize))
                {
                    continue;
                }

                int orthogonalLandNeighborCount = CountOrthogonalLandNeighbors(waterPoint, floorPointsSnapshot);
                if (orthogonalLandNeighborCount < 3)
                {
                    continue;
                }

                if (CountOrthogonalWaterNeighbors(waterPoint, shorelineWaterPoints) > 1)
                {
                    continue;
                }

                pointsToFill.Add(waterPoint);
                LogShorelineMicroCleanupDecision("Fill", waterPoint, "OceanSingleStep");
            }
        }

        private void CollectShorelineShortPocketFills(
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> shorelineWaterPoints,
            ActualFloorBoundsInfo bounds,
            HashSet<Vector2Int> pointsToFill)
        {
            Vector2Int[] forwardOffsets =
            {
                Vector2Int.right,
                Vector2Int.up
            };

            foreach (Vector2Int waterPoint in shorelineWaterPoints)
            {
                if (floorPointsSnapshot.Contains(waterPoint) ||
                    !IsPointInsideExpandedBounds(waterPoint, bounds, shorelineMicroCleanupSize))
                {
                    continue;
                }

                for (int i = 0; i < forwardOffsets.Length; i++)
                {
                    Vector2Int secondPoint = waterPoint + forwardOffsets[i];
                    if (floorPointsSnapshot.Contains(secondPoint) ||
                        !shorelineWaterPoints.Contains(secondPoint) ||
                        !IsPointInsideExpandedBounds(secondPoint, bounds, shorelineMicroCleanupSize))
                    {
                        continue;
                    }

                    Vector2Int beforePoint = waterPoint - forwardOffsets[i];
                    Vector2Int afterPoint = secondPoint + forwardOffsets[i];
                    if (shorelineWaterPoints.Contains(beforePoint) || shorelineWaterPoints.Contains(afterPoint))
                    {
                        continue;
                    }

                    HashSet<Vector2Int> component = new HashSet<Vector2Int> { waterPoint, secondPoint };
                    if (!IsShortShorelinePocketComponent(component, floorPointsSnapshot, shorelineWaterPoints))
                    {
                        continue;
                    }

                    foreach (Vector2Int componentPoint in component)
                    {
                        if (!pointsToFill.Contains(componentPoint))
                        {
                            pointsToFill.Add(componentPoint);
                            LogShorelineMicroCleanupDecision("Fill", componentPoint, "ShortCornerRun");
                        }
                    }
                }
            }
        }

        private void CollectShorelineSingleCellSpikeRemovals(
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> shorelineWaterPoints,
            ActualFloorBoundsInfo bounds,
            HashSet<Vector2Int> pointsToRemove)
        {
            foreach (Vector2Int landPoint in floorPointsSnapshot)
            {
                if (!IsPointInsideExpandedBounds(landPoint, bounds, shorelineMicroCleanupSize) ||
                    pointsToRemove.Contains(landPoint) ||
                    (connectorFloorPoints != null && connectorFloorPoints.Contains(landPoint)))
                {
                    continue;
                }

                if (CountOrthogonalWaterNeighbors(landPoint, shorelineWaterPoints) < 3)
                {
                    continue;
                }

                List<Vector2Int> landNeighbors = GetOrthogonalLandNeighbors(landPoint, floorPointsSnapshot, null);
                if (landNeighbors.Count != 1)
                {
                    continue;
                }

                HashSet<Vector2Int> removalCandidate = new HashSet<Vector2Int> { landPoint };
                if (!CanSafelyRemoveLandPoints(floorPointsSnapshot, removalCandidate))
                {
                    LogShorelineMicroCleanupDecision("SkipRemove", landPoint, "possible-mainland-connector");
                    continue;
                }

                pointsToRemove.Add(landPoint);
                LogShorelineMicroCleanupDecision("Remove", landPoint, "GrassSingleStep");
            }
        }

        private void CollectShorelineTwoCellSpikeRemovals(
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> shorelineWaterPoints,
            ActualFloorBoundsInfo bounds,
            HashSet<Vector2Int> pointsToRemove)
        {
            foreach (Vector2Int endPoint in floorPointsSnapshot)
            {
                if (!IsPointInsideExpandedBounds(endPoint, bounds, shorelineMicroCleanupSize) ||
                    pointsToRemove.Contains(endPoint) ||
                    (connectorFloorPoints != null && connectorFloorPoints.Contains(endPoint)))
                {
                    continue;
                }

                if (CountOrthogonalWaterNeighbors(endPoint, shorelineWaterPoints) < 3)
                {
                    continue;
                }

                List<Vector2Int> endPointLandNeighbors = GetOrthogonalLandNeighbors(endPoint, floorPointsSnapshot, null);
                if (endPointLandNeighbors.Count != 1)
                {
                    continue;
                }

                Vector2Int rootPoint = endPointLandNeighbors[0];
                if (pointsToRemove.Contains(rootPoint) ||
                    (connectorFloorPoints != null && connectorFloorPoints.Contains(rootPoint)))
                {
                    continue;
                }

                List<Vector2Int> rootLandNeighbors = GetOrthogonalLandNeighbors(rootPoint, floorPointsSnapshot, null);
                if (rootLandNeighbors.Count != 2 || CountOrthogonalWaterNeighbors(rootPoint, shorelineWaterPoints) < 2)
                {
                    continue;
                }

                Vector2Int directionFromRootToEnd = endPoint - rootPoint;
                Vector2Int inlandPoint = rootPoint - directionFromRootToEnd;
                if (!floorPointsSnapshot.Contains(inlandPoint))
                {
                    continue;
                }

                HashSet<Vector2Int> removalCandidate = new HashSet<Vector2Int> { endPoint, rootPoint };
                if (!CanSafelyRemoveLandPoints(floorPointsSnapshot, removalCandidate))
                {
                    LogShorelineMicroCleanupDecision("SkipRemove", endPoint, "two-cell-spike-may-disconnect");
                    continue;
                }

                pointsToRemove.Add(endPoint);
                pointsToRemove.Add(rootPoint);
                LogShorelineMicroCleanupDecision("Remove", endPoint, "GrassSingleStep");
                LogShorelineMicroCleanupDecision("Remove", rootPoint, "GrassSingleStep");
            }
        }

        private int ScoreShorelineJunctionFillCandidate(
            Vector2Int candidate,
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> shorelineWaterPoints)
        {
            if (floorPointsSnapshot.Contains(candidate))
            {
                return int.MinValue;
            }

            int orthogonalLandNeighborCount = CountOrthogonalLandNeighbors(candidate, floorPointsSnapshot);
            if (orthogonalLandNeighborCount < 2)
            {
                return int.MinValue;
            }

            int score = orthogonalLandNeighborCount * 10;
            score += CountDiagonalLandNeighbors(candidate, floorPointsSnapshot) * 3;

            if (DoesJunctionFillBridgeDiagonalLand(candidate, floorPointsSnapshot))
            {
                score += 18;
            }

            HashSet<Vector2Int> filledFloorPoints = new HashSet<Vector2Int>(floorPointsSnapshot)
            {
                candidate
            };
            if (HasValidTwoDimensionalSupport(candidate, filledFloorPoints, 2))
            {
                score += 20;
            }

            if (CountCoastalOrthogonalLandNeighbors(candidate, floorPointsSnapshot, shorelineWaterPoints) >= 2)
            {
                score += 8;
            }

            int localWaterDensity = CountLocalWaterCells(candidate, shorelineWaterPoints, 1);
            if (localWaterDensity >= 6)
            {
                score -= 6;
            }

            return score;
        }

        private bool CanSafelyFillShorelineJunctionPoint(
            Vector2Int candidate,
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> shorelineWaterPoints,
            ActualFloorBoundsInfo bounds)
        {
            if (floorPointsSnapshot.Contains(candidate) ||
                !shorelineWaterPoints.Contains(candidate) ||
                !IsPointInsideExpandedBounds(candidate, bounds, shorelineMicroCleanupSize))
            {
                return false;
            }

            int orthogonalLandNeighborCount = CountOrthogonalLandNeighbors(candidate, floorPointsSnapshot);
            if (orthogonalLandNeighborCount < 2)
            {
                return false;
            }

            int orthogonalWaterNeighborCount = CountOrthogonalWaterNeighbors(candidate, shorelineWaterPoints);
            if (orthogonalWaterNeighborCount > 2)
            {
                return false;
            }

            if (CountCoastalOrthogonalLandNeighbors(candidate, floorPointsSnapshot, shorelineWaterPoints) < 2)
            {
                return false;
            }

            HashSet<Vector2Int> filledFloorPoints = new HashSet<Vector2Int>(floorPointsSnapshot)
            {
                candidate
            };

            if (HasValidTwoDimensionalSupport(candidate, filledFloorPoints, 2))
            {
                return true;
            }

            List<Vector2Int> orthogonalLandNeighbors = GetOrthogonalLandNeighbors(candidate, floorPointsSnapshot, null);
            for (int i = 0; i < orthogonalLandNeighbors.Count; i++)
            {
                if (HasValidTwoDimensionalSupport(orthogonalLandNeighbors[i], filledFloorPoints, 2))
                {
                    return true;
                }
            }

            return false;
        }

        private bool MatchesDiagonalSaddlePatternAtPoint(
            Vector2Int candidate,
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> shorelineWaterPoints,
            out Vector2Int sourceBlockOrigin)
        {
            Vector2Int[] blockOffsets =
            {
                Vector2Int.zero,
                Vector2Int.left,
                Vector2Int.down,
                Vector2Int.left + Vector2Int.down
            };

            for (int i = 0; i < blockOffsets.Length; i++)
            {
                Vector2Int origin = candidate + blockOffsets[i];
                Vector2Int bottomLeft = origin;
                Vector2Int bottomRight = origin + Vector2Int.right;
                Vector2Int topLeft = origin + Vector2Int.up;
                Vector2Int topRight = origin + Vector2Int.up + Vector2Int.right;

                bool bottomLeftLand = floorPointsSnapshot.Contains(bottomLeft);
                bool bottomRightLand = floorPointsSnapshot.Contains(bottomRight);
                bool topLeftLand = floorPointsSnapshot.Contains(topLeft);
                bool topRightLand = floorPointsSnapshot.Contains(topRight);

                int landCount = (bottomLeftLand ? 1 : 0) +
                                (bottomRightLand ? 1 : 0) +
                                (topLeftLand ? 1 : 0) +
                                (topRightLand ? 1 : 0);
                if (landCount != 2)
                {
                    continue;
                }

                bool matchesPrimaryDiagonal =
                    bottomLeftLand && topRightLand &&
                    shorelineWaterPoints.Contains(bottomRight) &&
                    shorelineWaterPoints.Contains(topLeft) &&
                    (candidate == bottomRight || candidate == topLeft);
                bool matchesSecondaryDiagonal =
                    bottomRightLand && topLeftLand &&
                    shorelineWaterPoints.Contains(bottomLeft) &&
                    shorelineWaterPoints.Contains(topRight) &&
                    (candidate == bottomLeft || candidate == topRight);

                if (matchesPrimaryDiagonal || matchesSecondaryDiagonal)
                {
                    sourceBlockOrigin = origin;
                    return true;
                }
            }

            sourceBlockOrigin = candidate;
            return false;
        }

        private bool MatchesShortStepJunctionPatternAtPoint(
            Vector2Int candidate,
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> shorelineWaterPoints,
            out Vector2Int sourceBlockOrigin,
            out string rejectionReason)
        {
            if (floorPointsSnapshot.Contains(candidate))
            {
                sourceBlockOrigin = candidate;
                rejectionReason = "CandidateConflict";
                return false;
            }

            if (CountOrthogonalLandNeighbors(candidate, floorPointsSnapshot) != 2 ||
                CountOrthogonalWaterNeighbors(candidate, shorelineWaterPoints) != 2)
            {
                sourceBlockOrigin = candidate;
                rejectionReason = "InsufficientOrthogonalSupport";
                return false;
            }

            Vector2Int[] stepOffsets =
            {
                Vector2Int.zero,
                Vector2Int.left,
                Vector2Int.down,
                Vector2Int.left + Vector2Int.down
            };

            for (int i = 0; i < stepOffsets.Length; i++)
            {
                Vector2Int origin = candidate + stepOffsets[i];
                Vector2Int bottomLeft = origin;
                Vector2Int bottomRight = origin + Vector2Int.right;
                Vector2Int topLeft = origin + Vector2Int.up;
                Vector2Int topRight = origin + Vector2Int.up + Vector2Int.right;

                bool bottomLeftLand = floorPointsSnapshot.Contains(bottomLeft);
                bool bottomRightLand = floorPointsSnapshot.Contains(bottomRight);
                bool topLeftLand = floorPointsSnapshot.Contains(topLeft);
                bool topRightLand = floorPointsSnapshot.Contains(topRight);

                int landCount = (bottomLeftLand ? 1 : 0) +
                                (bottomRightLand ? 1 : 0) +
                                (topLeftLand ? 1 : 0) +
                                (topRightLand ? 1 : 0);
                if (landCount != 3)
                {
                    continue;
                }

                if (candidate != bottomLeft &&
                    candidate != bottomRight &&
                    candidate != topLeft &&
                    candidate != topRight)
                {
                    continue;
                }

                if (CountLocalWaterCells(candidate, shorelineWaterPoints, 1) > 6)
                {
                    sourceBlockOrigin = origin;
                    rejectionReason = "OpensIntoLargeWater";
                    return false;
                }

                sourceBlockOrigin = origin;
                rejectionReason = null;
                return true;
            }

            sourceBlockOrigin = candidate;
            rejectionReason = "LongStep";
            return false;
        }

        private static int CountOrthogonalWaterNeighbors(Vector2Int point, HashSet<Vector2Int> shorelineWaterPoints)
        {
            int count = 0;
            if (shorelineWaterPoints.Contains(point + Vector2Int.up)) count++;
            if (shorelineWaterPoints.Contains(point + Vector2Int.down)) count++;
            if (shorelineWaterPoints.Contains(point + Vector2Int.left)) count++;
            if (shorelineWaterPoints.Contains(point + Vector2Int.right)) count++;
            return count;
        }

        private static List<Vector2Int> GetOrthogonalLandNeighbors(
            Vector2Int point,
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> excludedPoints)
        {
            List<Vector2Int> neighbors = new List<Vector2Int>(4);
            Vector2Int[] offsets =
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector2Int neighbor = point + offsets[i];
                if (!floorPointsSnapshot.Contains(neighbor))
                {
                    continue;
                }

                if (excludedPoints != null && excludedPoints.Contains(neighbor))
                {
                    continue;
                }

                neighbors.Add(neighbor);
            }

            return neighbors;
        }

        private static int CountDiagonalLandNeighbors(Vector2Int point, HashSet<Vector2Int> floorPointsSnapshot)
        {
            int count = 0;
            if (floorPointsSnapshot.Contains(point + Vector2Int.up + Vector2Int.left)) count++;
            if (floorPointsSnapshot.Contains(point + Vector2Int.up + Vector2Int.right)) count++;
            if (floorPointsSnapshot.Contains(point + Vector2Int.down + Vector2Int.left)) count++;
            if (floorPointsSnapshot.Contains(point + Vector2Int.down + Vector2Int.right)) count++;
            return count;
        }

        private static int CountCoastalOrthogonalLandNeighbors(
            Vector2Int point,
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> shorelineWaterPoints)
        {
            int count = 0;
            List<Vector2Int> orthogonalLandNeighbors = GetOrthogonalLandNeighbors(point, floorPointsSnapshot, null);
            for (int i = 0; i < orthogonalLandNeighbors.Count; i++)
            {
                if (TouchesSpecificWaterSet(orthogonalLandNeighbors[i], shorelineWaterPoints))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountLocalWaterCells(
            Vector2Int point,
            HashSet<Vector2Int> shorelineWaterPoints,
            int radius)
        {
            int count = 0;
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (shorelineWaterPoints.Contains(new Vector2Int(point.x + x, point.y + y)))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool DoesJunctionFillBridgeDiagonalLand(
            Vector2Int candidate,
            HashSet<Vector2Int> floorPointsSnapshot)
        {
            bool upLeftLand = floorPointsSnapshot.Contains(candidate + Vector2Int.up) &&
                              floorPointsSnapshot.Contains(candidate + Vector2Int.left);
            bool upRightLand = floorPointsSnapshot.Contains(candidate + Vector2Int.up) &&
                               floorPointsSnapshot.Contains(candidate + Vector2Int.right);
            bool downRightLand = floorPointsSnapshot.Contains(candidate + Vector2Int.down) &&
                                 floorPointsSnapshot.Contains(candidate + Vector2Int.right);
            bool downLeftLand = floorPointsSnapshot.Contains(candidate + Vector2Int.down) &&
                                floorPointsSnapshot.Contains(candidate + Vector2Int.left);

            return upLeftLand || upRightLand || downRightLand || downLeftLand;
        }

        private static int CountUniqueOrthogonalLandNeighborsForComponent(
            HashSet<Vector2Int> component,
            HashSet<Vector2Int> floorPointsSnapshot)
        {
            HashSet<Vector2Int> neighbors = new HashSet<Vector2Int>();
            foreach (Vector2Int point in component)
            {
                List<Vector2Int> pointNeighbors = GetOrthogonalLandNeighbors(point, floorPointsSnapshot, component);
                for (int i = 0; i < pointNeighbors.Count; i++)
                {
                    neighbors.Add(pointNeighbors[i]);
                }
            }

            return neighbors.Count;
        }

        private static int CountOrthogonalWaterNeighborsForComponent(
            HashSet<Vector2Int> component,
            HashSet<Vector2Int> shorelineWaterPoints)
        {
            HashSet<Vector2Int> neighboringWaterPoints = new HashSet<Vector2Int>();
            Vector2Int[] offsets =
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            foreach (Vector2Int point in component)
            {
                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector2Int neighbor = point + offsets[i];
                    if (component.Contains(neighbor) || !shorelineWaterPoints.Contains(neighbor))
                    {
                        continue;
                    }

                    neighboringWaterPoints.Add(neighbor);
                }
            }

            return neighboringWaterPoints.Count;
        }

        private static bool IsShortShorelinePocketComponent(
            HashSet<Vector2Int> component,
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> shorelineWaterPoints)
        {
            foreach (Vector2Int point in component)
            {
                if (CountOrthogonalLandNeighbors(point, floorPointsSnapshot) < 2)
                {
                    return false;
                }
            }

            return CountUniqueOrthogonalLandNeighborsForComponent(component, floorPointsSnapshot) >= 4 &&
                   CountOrthogonalWaterNeighborsForComponent(component, shorelineWaterPoints) <= 2;
        }

        private bool CanSafelyRemoveLandPoints(
            HashSet<Vector2Int> floorPointsSnapshot,
            HashSet<Vector2Int> removalPoints)
        {
            if (removalPoints == null || removalPoints.Count == 0)
            {
                return true;
            }

            if (connectorFloorPoints != null)
            {
                foreach (Vector2Int removalPoint in removalPoints)
                {
                    if (connectorFloorPoints.Contains(removalPoint))
                    {
                        return false;
                    }
                }
            }

            HashSet<Vector2Int> neighboringLandPoints = new HashSet<Vector2Int>();
            foreach (Vector2Int removalPoint in removalPoints)
            {
                List<Vector2Int> neighbors = GetOrthogonalLandNeighbors(removalPoint, floorPointsSnapshot, removalPoints);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    neighboringLandPoints.Add(neighbors[i]);
                }
            }

            if (neighboringLandPoints.Count <= 1)
            {
                return true;
            }

            List<Vector2Int> orderedNeighboringLandPoints = new List<Vector2Int>(neighboringLandPoints);
            orderedNeighboringLandPoints.Sort(ComparePointOrder);

            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            Queue<Vector2Int> openSet = new Queue<Vector2Int>();
            Vector2Int startPoint = orderedNeighboringLandPoints[0];
            openSet.Enqueue(startPoint);
            visited.Add(startPoint);

            Vector2Int[] offsets =
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            while (openSet.Count > 0)
            {
                Vector2Int currentPoint = openSet.Dequeue();
                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector2Int neighbor = currentPoint + offsets[i];
                    if (visited.Contains(neighbor) ||
                        removalPoints.Contains(neighbor) ||
                        !floorPointsSnapshot.Contains(neighbor))
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    openSet.Enqueue(neighbor);
                }
            }

            for (int i = 1; i < orderedNeighboringLandPoints.Count; i++)
            {
                if (!visited.Contains(orderedNeighboringLandPoints[i]))
                {
                    return false;
                }
            }

            return true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogShorelineMicroCleanupIteration(
            int iteration,
            HashSet<Vector2Int> pointsToFill,
            HashSet<Vector2Int> junctionPointsToFill,
            HashSet<Vector2Int> pointsToRemove)
        {
            if (!debugShoreSandPlacements)
            {
                return;
            }

            Debug.Log(
                $"[ShoreSand.BoundaryCleanup] iteration={iteration + 1} pocketFillCount={pointsToFill.Count} junctionFillCount={junctionPointsToFill.Count} removeCount={pointsToRemove.Count}",
                this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogShorelineMicroCleanupDecision(string action, Vector2Int point, string reason)
        {
            if (!debugShoreSandPlacements)
            {
                return;
            }

            Debug.Log(
                $"[ShoreSand.BoundaryCleanup] action={action} point={point} reason={reason}",
                this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogShorelineJunctionCandidateAccepted(ShorelineJunctionFillCandidate candidate)
        {
            if (!debugShoreSandPlacements)
            {
                return;
            }

            Debug.Log(
                $"[ShoreSand.BoundaryCleanup] junctionAction=Accept point={candidate.point} sourceKind={candidate.sourceKind} sourceBlockOrigin={candidate.sourceBlockOrigin} score={candidate.score}",
                this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogShorelineJunctionCandidateRejected(ShorelineJunctionFillCandidate candidate, string reason)
        {
            if (!debugShoreSandPlacements)
            {
                return;
            }

            Debug.Log(
                $"[ShoreSand.BoundaryCleanup] junctionAction=Reject point={candidate.point} sourceKind={candidate.sourceKind} sourceBlockOrigin={candidate.sourceBlockOrigin} score={candidate.score} reason={reason}",
                this);
        }

        private void CollectNarrowLandFeatureChanges(
            HashSet<Vector2Int> allFloorPoints,
            ActualFloorBoundsInfo bounds,
            HashSet<Vector2Int> pointsToFill,
            HashSet<Vector2Int> pointsToRemove)
        {
            List<HashSet<Vector2Int>> narrowLandBranches = CollectNarrowTerrainBranches(
                allFloorPoints,
                candidate => IsTerrainCorePoint(candidate, allFloorPoints, minimumTerrainFeatureWidth));

            for (int i = 0; i < narrowLandBranches.Count; i++)
            {
                HashSet<Vector2Int> branch = narrowLandBranches[i];
                if (branch == null || branch.Count == 0 || ContainsProtectedConnectorPoint(branch))
                {
                    continue;
                }

                if (TryWidenBaseTerrainBranch(branch, allFloorPoints, bounds, out HashSet<Vector2Int> branchFills))
                {
                    pointsToFill.UnionWith(branchFills);
                }
                else
                {
                    pointsToRemove.UnionWith(branch);
                }
            }
        }

        private void CollectNarrowOceanFeatureFills(
            HashSet<Vector2Int> allFloorPoints,
            ActualFloorBoundsInfo bounds,
            HashSet<Vector2Int> pointsToFill)
        {
            HashSet<Vector2Int> oceanPoints = BuildFiniteOceanPointSet(allFloorPoints, bounds, minimumTerrainFeatureWidth);
            List<HashSet<Vector2Int>> narrowOceanBranches = CollectNarrowTerrainBranches(
                oceanPoints,
                candidate => IsFiniteOceanCorePoint(candidate, oceanPoints, bounds, minimumTerrainFeatureWidth));

            for (int i = 0; i < narrowOceanBranches.Count; i++)
            {
                pointsToFill.UnionWith(narrowOceanBranches[i]);
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

        private bool TryWidenBaseTerrainBranch(
            HashSet<Vector2Int> branchPoints,
            HashSet<Vector2Int> allFloorPoints,
            ActualFloorBoundsInfo bounds,
            out HashSet<Vector2Int> branchFills)
        {
            branchFills = null;
            Vector2Int[] offsets =
            {
                Vector2Int.left,
                Vector2Int.right,
                Vector2Int.up,
                Vector2Int.down
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                if (!TryBuildBaseTerrainBranchWidening(branchPoints, allFloorPoints, bounds, offsets[i], out HashSet<Vector2Int> candidateFills))
                {
                    continue;
                }

                HashSet<Vector2Int> widened = new HashSet<Vector2Int>(allFloorPoints);
                widened.UnionWith(candidateFills);
                if (!ComponentHasMinimumThickness(branchPoints, candidateFills, widened, minimumTerrainFeatureWidth))
                {
                    continue;
                }

                branchFills = candidateFills;
                return true;
            }

            return false;
        }

        private bool TryBuildBaseTerrainBranchWidening(
            HashSet<Vector2Int> branchPoints,
            HashSet<Vector2Int> allFloorPoints,
            ActualFloorBoundsInfo bounds,
            Vector2Int offset,
            out HashSet<Vector2Int> candidateFills)
        {
            candidateFills = new HashSet<Vector2Int>();
            for (int layer = 1; layer < minimumTerrainFeatureWidth; layer++)
            {
                foreach (Vector2Int branchPoint in branchPoints)
                {
                    Vector2Int candidate = branchPoint + (offset * layer);
                    if (branchPoints.Contains(candidate) || allFloorPoints.Contains(candidate) || candidateFills.Contains(candidate))
                    {
                        continue;
                    }

                    if (!IsPointInsideExpandedBounds(candidate, bounds, minimumTerrainFeatureWidth))
                    {
                        return false;
                    }

                    candidateFills.Add(candidate);
                }
            }

            return candidateFills.Count > 0;
        }

        private bool ContainsProtectedConnectorPoint(HashSet<Vector2Int> points)
        {
            if (connectorFloorPoints == null || points == null)
            {
                return false;
            }

            foreach (Vector2Int point in points)
            {
                if (connectorFloorPoints.Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ComponentHasMinimumThickness(
            HashSet<Vector2Int> branchPoints,
            HashSet<Vector2Int> candidateFills,
            HashSet<Vector2Int> resultPoints,
            int minimumWidth)
        {
            foreach (Vector2Int point in branchPoints)
            {
                if (!HasMinimumTerrainThicknessOnBothAxes(point, resultPoints, minimumWidth))
                {
                    return false;
                }
            }

            if (candidateFills != null)
            {
                foreach (Vector2Int point in candidateFills)
                {
                    if (!HasMinimumTerrainThicknessOnBothAxes(point, resultPoints, minimumWidth))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static HashSet<Vector2Int> BuildFiniteOceanPointSet(
            HashSet<Vector2Int> allFloorPoints,
            ActualFloorBoundsInfo bounds,
            int padding)
        {
            HashSet<Vector2Int> oceanPoints = new HashSet<Vector2Int>();
            if (!bounds.isValid)
            {
                return oceanPoints;
            }

            int minX = bounds.min.x - padding;
            int maxX = bounds.max.x + padding;
            int minY = bounds.min.y - padding;
            int maxY = bounds.max.y + padding;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector2Int point = new Vector2Int(x, y);
                    if (!allFloorPoints.Contains(point))
                    {
                        oceanPoints.Add(point);
                    }
                }
            }

            return oceanPoints;
        }

        private static bool IsFiniteOceanCorePoint(
            Vector2Int point,
            HashSet<Vector2Int> oceanPoints,
            ActualFloorBoundsInfo bounds,
            int padding)
        {
            int minX = bounds.min.x - padding;
            int maxX = bounds.max.x + padding;
            int minY = bounds.min.y - padding;
            int maxY = bounds.max.y + padding;

            if (point.x == minX || point.x == maxX || point.y == minY || point.y == maxY)
            {
                return true;
            }

            return IsTerrainCorePoint(point, oceanPoints, padding);
        }

        private static bool IsTerrainCorePoint(Vector2Int point, HashSet<Vector2Int> points, int minimumWidth)
        {
            return HasMinimumTerrainThicknessOnBothAxes(point, points, minimumWidth) ||
                   HasValidTwoDimensionalSupport(point, points, minimumWidth);
        }

        private static bool HasMinimumTerrainThicknessOnBothAxes(Vector2Int point, HashSet<Vector2Int> points, int minimumWidth)
        {
            if (points == null || !points.Contains(point))
            {
                return false;
            }

            return CountContiguousRunLength(point, Vector2Int.left, Vector2Int.right, points) >= minimumWidth &&
                   CountContiguousRunLength(point, Vector2Int.down, Vector2Int.up, points) >= minimumWidth;
        }

        private static bool IsPointInsideExpandedBounds(Vector2Int point, ActualFloorBoundsInfo bounds, int padding)
        {
            if (!bounds.isValid)
            {
                return false;
            }

            return point.x >= bounds.min.x - padding &&
                   point.x <= bounds.max.x + padding &&
                   point.y >= bounds.min.y - padding &&
                   point.y <= bounds.max.y + padding;
        }

        private static int CountContiguousRunLength(
            Vector2Int origin,
            Vector2Int negativeOffset,
            Vector2Int positiveOffset,
            HashSet<Vector2Int> points)
        {
            if (points == null || !points.Contains(origin))
            {
                return 0;
            }

            int count = 1;
            Vector2Int cursor = origin + negativeOffset;
            while (points.Contains(cursor))
            {
                count++;
                cursor += negativeOffset;
            }

            cursor = origin + positiveOffset;
            while (points.Contains(cursor))
            {
                count++;
                cursor += positiveOffset;
            }

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
            currentExteriorOceanPoints = null;
            currentShoreWaterPoints = null;
            currentEnclosedWaterPointCount = 0;

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

            currentExteriorOceanPoints = CollectExteriorOceanPoints(allLandPoints);
            if (currentShoreWaterPoints == null || currentShoreWaterPoints.Count == 0)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugShoreSandPlacements)
            {
                Debug.Log(
                    $"[ShoreSand.ExteriorOcean] exteriorOceanCount={currentExteriorOceanPoints.Count} enclosedWaterCount={currentEnclosedWaterPointCount} totalShoreWaterCount={currentShoreWaterPoints.Count}",
                    this);
            }
#endif

            Dictionary<Vector2Int, int> shoreDepthByPoint = BuildInwardShoreDepthMap(
                allLandPoints,
                areaByPoint,
                out int coastalSeedCount,
                out int exteriorCoastalSeedCount,
                out int enclosedWaterCoastalSeedCount);
            if (shoreDepthByPoint.Count == 0)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugShoreSandPlacements)
            {
                Debug.Log(
                    $"[ShoreSand.DepthMap] coastalSeedCount={coastalSeedCount} exteriorCoastalSeedCount={exteriorCoastalSeedCount} enclosedWaterCoastalSeedCount={enclosedWaterCoastalSeedCount} shoreDepthPointCount={shoreDepthByPoint.Count} maxDepth={Mathf.Max(0, shoreSandWidth - 1)}",
                    this);
            }
#endif

            List<ShoreSandPlacement> placements = BuildShoreSandPlacementsFromDepthMap(
                shoreDepthByPoint,
                allLandPoints,
                areaByPoint);

            if (placements.Count == 0)
            {
                return;
            }

            ApplyShortGrassBoundarySegmentFix(placements, allLandPoints, areaByPoint);
            ApplyFinalGrassBoundaryCorrection(placements, allLandPoints, areaByPoint);
            ApplyFinalCornerResolution(placements, allLandPoints, areaByPoint);

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
                TraceFinalShoreSandInstantiation(point, placements[i], prefab, finalYaw, instance);
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

        private HashSet<Vector2Int> CollectExteriorOceanPoints(HashSet<Vector2Int> allLandPoints)
        {
            HashSet<Vector2Int> exteriorOceanPoints = new HashSet<Vector2Int>();
            currentShoreWaterPoints = new HashSet<Vector2Int>();
            currentEnclosedWaterPointCount = 0;

            if (allLandPoints == null || allLandPoints.Count == 0)
            {
                return exteriorOceanPoints;
            }

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;

            foreach (Vector2Int point in allLandPoints)
            {
                if (point.x < minX) minX = point.x;
                if (point.x > maxX) maxX = point.x;
                if (point.y < minY) minY = point.y;
                if (point.y > maxY) maxY = point.y;
            }

            const int oceanSearchPadding = 2;
            minX -= oceanSearchPadding;
            maxX += oceanSearchPadding;
            minY -= oceanSearchPadding;
            maxY += oceanSearchPadding;

            Queue<Vector2Int> queue = new Queue<Vector2Int>();

            void TryEnqueueExteriorSeed(Vector2Int candidate)
            {
                if (!IsWithinOceanSearchBounds(candidate, minX, maxX, minY, maxY) ||
                    allLandPoints.Contains(candidate) ||
                    !exteriorOceanPoints.Add(candidate))
                {
                    return;
                }

                queue.Enqueue(candidate);
            }

            for (int x = minX; x <= maxX; x++)
            {
                TryEnqueueExteriorSeed(new Vector2Int(x, minY));
                TryEnqueueExteriorSeed(new Vector2Int(x, maxY));
            }

            for (int y = minY; y <= maxY; y++)
            {
                TryEnqueueExteriorSeed(new Vector2Int(minX, y));
                TryEnqueueExteriorSeed(new Vector2Int(maxX, y));
            }

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                TryEnqueueExteriorSeed(current + Vector2Int.up);
                TryEnqueueExteriorSeed(current + Vector2Int.right);
                TryEnqueueExteriorSeed(current + Vector2Int.down);
                TryEnqueueExteriorSeed(current + Vector2Int.left);
            }

            int enclosedCount = 0;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Vector2Int point = new Vector2Int(x, y);
                    if (!allLandPoints.Contains(point))
                    {
                        currentShoreWaterPoints.Add(point);
                    }

                    if (!allLandPoints.Contains(point) && !exteriorOceanPoints.Contains(point))
                    {
                        enclosedCount++;
                    }
                }
            }

            currentEnclosedWaterPointCount = enclosedCount;
            return exteriorOceanPoints;
        }

        private Dictionary<Vector2Int, int> BuildInwardShoreDepthMap(
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            out int coastalSeedCount,
            out int exteriorCoastalSeedCount,
            out int enclosedWaterCoastalSeedCount)
        {
            Dictionary<Vector2Int, int> depthByPoint = new Dictionary<Vector2Int, int>();
            coastalSeedCount = 0;
            exteriorCoastalSeedCount = 0;
            enclosedWaterCoastalSeedCount = 0;

            if (allLandPoints == null ||
                areaByPoint == null ||
                currentShoreWaterPoints == null ||
                currentShoreWaterPoints.Count == 0)
            {
                return depthByPoint;
            }

            List<Vector2Int> seedPoints = new List<Vector2Int>();
            foreach (Vector2Int point in allLandPoints)
            {
                if (!IsGrassLandPoint(point, allLandPoints, areaByPoint))
                {
                    continue;
                }

                bool touchesExteriorOcean = TouchesSpecificWaterSet(point, currentExteriorOceanPoints);
                bool touchesEnclosedWater = TouchesEnclosedWater(point);
                if (touchesExteriorOcean || touchesEnclosedWater)
                {
                    seedPoints.Add(point);
                    if (touchesExteriorOcean)
                    {
                        exteriorCoastalSeedCount++;
                    }

                    if (touchesEnclosedWater)
                    {
                        enclosedWaterCoastalSeedCount++;
                    }
                }
            }

            seedPoints.Sort(ComparePointOrder);
            coastalSeedCount = seedPoints.Count;

            Queue<Vector2Int> queue = new Queue<Vector2Int>(seedPoints.Count);
            for (int i = 0; i < seedPoints.Count; i++)
            {
                Vector2Int seedPoint = seedPoints[i];
                if (depthByPoint.ContainsKey(seedPoint))
                {
                    continue;
                }

                depthByPoint.Add(seedPoint, 0);
                queue.Enqueue(seedPoint);
            }

            int maxDepth = Mathf.Max(0, shoreSandWidth - 1);
            Vector2Int[] inwardDirections =
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                int currentDepth = depthByPoint[current];
                if (currentDepth >= maxDepth)
                {
                    continue;
                }

                for (int i = 0; i < inwardDirections.Length; i++)
                {
                    Vector2Int neighbor = current + inwardDirections[i];
                    if (depthByPoint.ContainsKey(neighbor) ||
                        !IsGrassLandPoint(neighbor, allLandPoints, areaByPoint))
                    {
                        continue;
                    }

                    depthByPoint.Add(neighbor, currentDepth + 1);
                    queue.Enqueue(neighbor);
                }
            }

            return depthByPoint;
        }

        private List<ShoreSandPlacement> BuildShoreSandPlacementsFromDepthMap(
            Dictionary<Vector2Int, int> shoreDepthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint)
        {
            List<ShoreSandPlacement> placements = new List<ShoreSandPlacement>();
            if (shoreDepthByPoint == null || shoreDepthByPoint.Count == 0)
            {
                return placements;
            }

            HashSet<Vector2Int> finalShoreSandPoints = new HashSet<Vector2Int>(shoreDepthByPoint.Keys);
            List<Vector2Int> orderedPoints = new List<Vector2Int>(shoreDepthByPoint.Keys);
            orderedPoints.Sort((lhs, rhs) =>
            {
                int depthCompare = shoreDepthByPoint[lhs].CompareTo(shoreDepthByPoint[rhs]);
                return depthCompare != 0 ? depthCompare : ComparePointOrder(lhs, rhs);
            });

            int maxDepth = Mathf.Max(0, shoreSandWidth - 1);
            for (int i = 0; i < orderedPoints.Count; i++)
            {
                Vector2Int point = orderedPoints[i];
                int depth = shoreDepthByPoint[point];
                ShoreSandPlacement placement;

                if (depth == 0)
                {
                    if (!TryGetPreferredCoastalDirection(point, allLandPoints, out ShoreEdgeDirection oceanDirection))
                    {
                        continue;
                    }

                    placement = new ShoreSandPlacement(
                        point,
                        shoreSandOceanTransitionPrefab,
                        oceanDirection,
                        true,
                        true,
                        false);
                }
                else
                {
                    List<ShoreEdgeDirection> grassNeighborDirections = new List<ShoreEdgeDirection>(4);
                    ShoreEdgeDirection singleGrassDirection = ShoreEdgeDirection.Up;
                    int grassNeighborCount = 0;

                    if (depth == maxDepth)
                    {
                        grassNeighborCount = CountOrdinaryGrassNeighborDirections(
                            point,
                            allLandPoints,
                            areaByPoint,
                            finalShoreSandPoints,
                            out grassNeighborDirections,
                            out singleGrassDirection);
                    }

                    bool useGrassTransition = depth == maxDepth && grassNeighborCount > 0;

                    if (useGrassTransition)
                    {
                        ShoreEdgeDirection grassDirection = singleGrassDirection;
                        if (IsAdjacentGrassPair(grassNeighborDirections) &&
                            TryResolveAdjacentTwoGrassPrimaryDirection(
                                point,
                                allLandPoints,
                                finalShoreSandPoints,
                                grassNeighborDirections,
                                out ShoreEdgeDirection resolvedGrassDirection,
                                out _))
                        {
                            grassDirection = resolvedGrassDirection;
                        }

                        placement = new ShoreSandPlacement(
                            point,
                            shoreSandGrassTransitionPrefab,
                            grassDirection,
                            false,
                            false,
                            true,
                            grassNeighborCount);
                    }
                    else
                    {
                        placement = new ShoreSandPlacement(
                            point,
                            shoreSandNormalPrefab,
                            ShoreEdgeDirection.Up,
                            false,
                            false,
                            false);
                    }
                }

                placements.Add(placement);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (debugShoreSandPlacements)
                {
                    Debug.Log(
                        $"[ShoreSand.DepthPlacement] point={point} depth={depth} prefabType={GetShoreSandPlacementDebugType(placement)}",
                        this);
                }
#endif
            }

            return placements;
        }

        private bool TouchesExteriorOcean(Vector2Int point)
        {
            return TouchesSpecificWaterSet(point, currentExteriorOceanPoints);
        }

        private bool TouchesEnclosedWater(Vector2Int point)
        {
            if (currentShoreWaterPoints == null || currentShoreWaterPoints.Count == 0)
            {
                return false;
            }

            return TouchesSpecificWaterSet(point, currentShoreWaterPoints) &&
                   !TouchesSpecificWaterSet(point, currentExteriorOceanPoints);
        }

        private static bool TouchesSpecificWaterSet(Vector2Int point, HashSet<Vector2Int> waterPoints)
        {
            if (waterPoints == null || waterPoints.Count == 0)
            {
                return false;
            }

            return waterPoints.Contains(point + Vector2Int.up) ||
                   waterPoints.Contains(point + Vector2Int.right) ||
                   waterPoints.Contains(point + Vector2Int.down) ||
                   waterPoints.Contains(point + Vector2Int.left);
        }

        private static bool IsWithinOceanSearchBounds(Vector2Int point, int minX, int maxX, int minY, int maxY)
        {
            return point.x >= minX && point.x <= maxX &&
                   point.y >= minY && point.y <= maxY;
        }

        private void ApplyShoreSandBoundaryCleanup(
            List<ShoreSandPlacement> placements,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> changedPoints)
        {
            if (placements == null || placements.Count == 0 || allLandPoints == null || areaByPoint == null)
            {
                return;
            }

            HashSet<Vector2Int> sourceShoreSandPoints = new HashSet<Vector2Int>(placements.Count);
            for (int i = 0; i < placements.Count; i++)
            {
                sourceShoreSandPoints.Add(placements[i].point);
            }

            HashSet<Vector2Int> adjustedPoints = new HashSet<Vector2Int>();
            List<ShoreSandPlacement> additions = new List<ShoreSandPlacement>();

            foreach (Vector2Int point in allLandPoints)
            {
                if (sourceShoreSandPoints.Contains(point) ||
                    adjustedPoints.Contains(point) ||
                    !IsGrassLandPoint(point, allLandPoints, areaByPoint))
                {
                    continue;
                }

                if (!TryResolveBoundaryCleanupAddition(
                        point,
                        sourceShoreSandPoints,
                        allLandPoints,
                        areaByPoint,
                        out string reason,
                        out ShoreEdgeDirection primaryDirection,
                        out ShoreEdgeDirection secondaryDirection))
                {
                    continue;
                }

                ShoreSandPlacement addedPlacement;
                if (TryGetPreferredCoastalDirection(point, allLandPoints, out ShoreEdgeDirection seaDirection))
                {
                    addedPlacement = new ShoreSandPlacement(
                        point,
                        shoreSandOceanTransitionPrefab,
                        seaDirection,
                        true,
                        true,
                        false);
                }
                else
                {
                    addedPlacement = new ShoreSandPlacement(
                        point,
                        shoreSandNormalPrefab,
                        ShoreEdgeDirection.Up,
                        false,
                        false,
                        false);
                }

                additions.Add(addedPlacement);
                adjustedPoints.Add(point);
                MarkChangedPoint(changedPoints, point);
                TraceShoreSandPlacement(
                    point,
                    "ApplyShoreSandBoundaryCleanup",
                    "Grass",
                    addedPlacement,
                    $"Boundary cleanup addition. reason={reason} primaryDir={primaryDirection} secondaryDir={secondaryDirection}");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogShoreSandBoundaryCleanup(
                    point,
                    "Grass",
                    GetShoreSandPlacementDebugType(addedPlacement),
                    reason,
                    primaryDirection,
                    secondaryDirection);
#endif
            }

            if (additions.Count > 0)
            {
                placements.AddRange(additions);
            }
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

            if (!TryGetPreferredCoastalDirection(outerPoint, allLandPoints, out ShoreEdgeDirection oceanDirection))
            {
                return false;
            }

            int resolvedShoreSandWidth = Mathf.Max(3, shoreSandWidth);
            Vector2Int inwardOffset = GetOppositeCardinalOffset(oceanDirection);
            ShoreEdgeDirection grassDirection = GetOppositeDirection(oceanDirection);
            placements = new List<ShoreSandPlacement>(resolvedShoreSandWidth);
            placements.Add(new ShoreSandPlacement(
                outerPoint,
                shoreSandOceanTransitionPrefab,
                oceanDirection,
                true,
                true,
                false));
            TraceShoreSandPlacement(
                outerPoint,
                "TryBuildShoreSandStrip",
                "None",
                placements[0],
                $"Initial strip layer=1 oceanDirection={oceanDirection} inwardDirection={grassDirection} shoreWidth={resolvedShoreSandWidth}");

            for (int layerIndex = 2; layerIndex <= resolvedShoreSandWidth; layerIndex++)
            {
                Vector2Int layerPoint = outerPoint + inwardOffset * (layerIndex - 1);
                if (!IsGrassLandPoint(layerPoint, allLandPoints, areaByPoint))
                {
                    break;
                }

                placements.Add(new ShoreSandPlacement(
                    layerPoint,
                    layerIndex == resolvedShoreSandWidth ? shoreSandGrassTransitionPrefab : shoreSandNormalPrefab,
                    layerIndex == resolvedShoreSandWidth ? grassDirection : oceanDirection,
                    false,
                    false,
                    layerIndex == resolvedShoreSandWidth));
                TraceShoreSandPlacement(
                    layerPoint,
                    "TryBuildShoreSandStrip",
                    "None",
                    placements[placements.Count - 1],
                    $"Initial strip layer={layerIndex} oceanDirection={oceanDirection} inwardDirection={grassDirection} shoreWidth={resolvedShoreSandWidth}");
            }

            return true;
        }

        private void ApplyMinimumTwoTileCoastalWidth(
            List<ShoreSandPlacement> placements,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> changedPoints)
        {
            if (placements == null || placements.Count == 0 || allLandPoints == null || areaByPoint == null)
            {
                return;
            }

            EnsureMinimumShoreSandWidth(placements, allLandPoints, areaByPoint, changedPoints, "ApplyMinimumTwoTileCoastalWidth");
            FillSingleTileGrassBottlenecks(placements, allLandPoints, areaByPoint, changedPoints);
        }

        private void NormalizeFinalShoreSandFootprint(
            List<ShoreSandPlacement> placements,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> changedPoints)
        {
            if (placements == null || placements.Count == 0)
            {
                return;
            }

            if (ShouldTraceShoreSandPoint(debugShoreSandGridPoint))
            {
                TraceShoreSandDecision(
                    debugShoreSandGridPoint,
                    "NormalizeFinalShoreSandFootprint",
                    GetPlacementTypeAtPoint(placements, debugShoreSandGridPoint),
                    GetPlacementTypeAtPoint(placements, debugShoreSandGridPoint),
                    $"Begin normalize. existsBefore={ContainsPlacementPoint(placements, debugShoreSandGridPoint)}",
                    null);
            }

            EnsureMinimumShoreSandWidth(placements, allLandPoints, areaByPoint, changedPoints, "NormalizeFinalShoreSandFootprint");

            if (ShouldTraceShoreSandPoint(debugShoreSandGridPoint))
            {
                TraceShoreSandDecision(
                    debugShoreSandGridPoint,
                    "NormalizeFinalShoreSandFootprint",
                    GetPlacementTypeAtPoint(placements, debugShoreSandGridPoint),
                    GetPlacementTypeAtPoint(placements, debugShoreSandGridPoint),
                    $"End normalize. existsAfter={ContainsPlacementPoint(placements, debugShoreSandGridPoint)}",
                    null);
            }
        }

        private void EnsureMinimumShoreSandWidth(
            List<ShoreSandPlacement> placements,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> changedPoints,
            string traceStage = "EnsureMinimumShoreSandWidth")
        {
            bool changed;
            do
            {
                changed = false;

                HashSet<Vector2Int> shorePoints = BuildPlacementPointSet(placements);
                List<HashSet<Vector2Int>> oneTileWideBranches = CollectNarrowShoreBranches(shorePoints);

                if (oneTileWideBranches.Count == 0)
                {
                    break;
                }

                List<ShoreSandPlacement> additions = new List<ShoreSandPlacement>();
                HashSet<Vector2Int> removedPoints = new HashSet<Vector2Int>();
                for (int i = 0; i < oneTileWideBranches.Count; i++)
                {
                    HashSet<Vector2Int> branch = oneTileWideBranches[i];
                    if (branch == null || branch.Count == 0)
                    {
                        continue;
                    }

                    if (TryWidenNarrowShoreBranch(
                            branch,
                            shorePoints,
                            allLandPoints,
                            areaByPoint,
                            out List<ShoreSandPlacement> branchAdditions))
                    {
                        for (int addIndex = 0; addIndex < branchAdditions.Count; addIndex++)
                        {
                            ShoreSandPlacement supportPlacement = branchAdditions[addIndex];
                            if (shorePoints.Add(supportPlacement.point))
                            {
                                additions.Add(supportPlacement);
                                MarkChangedPoint(changedPoints, supportPlacement.point);
                                changed = true;
                                TraceShoreSandPlacement(
                                    supportPlacement.point,
                                    traceStage,
                                    "None",
                                    supportPlacement,
                                    $"Added while widening narrow branch. branchSize={branch.Count} branchPoints={FormatPointCollection(branch)}");
                            }
                        }
                    }
                    else
                    {
                        foreach (Vector2Int removedPoint in branch)
                        {
                            string oldType = GetPlacementTypeAtPoint(placements, removedPoint);
                            removedPoints.Add(removedPoint);
                            MarkChangedPoint(changedPoints, removedPoint);
                            TraceShoreSandDecision(
                                removedPoint,
                                traceStage,
                                oldType,
                                "Removed",
                                $"Removed as narrow shore branch. branchSize={branch.Count} branchPoints={FormatPointCollection(branch)}",
                                null);
                        }
                    }
                }

                if (additions.Count > 0)
                {
                    placements.AddRange(additions);
                }

                if (removedPoints.Count > 0)
                {
                    int removedCount = placements.RemoveAll(placement => removedPoints.Contains(placement.point));
                    changed |= removedCount > 0;
                }
            }
            while (changed);
        }

        private void FillSingleTileGrassBottlenecks(
            List<ShoreSandPlacement> placements,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> changedPoints)
        {
            HashSet<Vector2Int> shorePoints = BuildPlacementPointSet(placements);
            HashSet<Vector2Int> grassPoints = BuildGrassSupportPointSet(allLandPoints, areaByPoint, shorePoints);
            List<Vector2Int> unsupportedGrassPoints = CollectUnsupportedPoints(grassPoints, minimumTerrainFeatureWidth);
            if (unsupportedGrassPoints.Count == 0)
            {
                return;
            }

            HashSet<Vector2Int> addedPoints = new HashSet<Vector2Int>();
            List<ShoreSandPlacement> additions = new List<ShoreSandPlacement>();
            for (int i = 0; i < unsupportedGrassPoints.Count; i++)
            {
                Vector2Int point = unsupportedGrassPoints[i];
                if (!IsSingleTileGrassBottleneck(point, shorePoints) &&
                    HasValidTwoDimensionalSupport(point, grassPoints, minimumTerrainFeatureWidth))
                {
                    continue;
                }

                if (addedPoints.Contains(point))
                {
                    continue;
                }

                ShoreSandPlacement addedPlacement;
                if (TryGetPreferredCoastalDirection(point, allLandPoints, out ShoreEdgeDirection seaDirection))
                {
                    addedPlacement = new ShoreSandPlacement(
                        point,
                        shoreSandOceanTransitionPrefab,
                        seaDirection,
                        true,
                        true,
                        false);
                }
                else
                {
                    addedPlacement = new ShoreSandPlacement(
                        point,
                        shoreSandNormalPrefab,
                        ShoreEdgeDirection.Up,
                        false,
                        false,
                        false);
                }

                additions.Add(addedPlacement);
                addedPoints.Add(point);
                MarkChangedPoint(changedPoints, point);
                TraceShoreSandPlacement(
                    point,
                    "ApplyMinimumTwoTileCoastalWidth",
                    "Grass",
                    addedPlacement,
                    $"Filled unsupported grass bottleneck. singleTileBottleneck={IsSingleTileGrassBottleneck(point, shorePoints)} supports2D={HasValidTwoDimensionalSupport(point, grassPoints, minimumTerrainFeatureWidth)}");
            }

            if (additions.Count > 0)
            {
                placements.AddRange(additions);
            }
        }

        private void ResolveRemainingSingleTileCoastalGaps(
            List<ShoreSandPlacement> placements,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> changedPoints)
        {
            if (placements == null || placements.Count == 0 || allLandPoints == null || areaByPoint == null)
            {
                return;
            }

            HashSet<Vector2Int> shorePoints = BuildPlacementPointSet(placements);
            List<ShoreSandPlacement> additions = new List<ShoreSandPlacement>();
            HashSet<Vector2Int> pendingAdditions = new HashSet<Vector2Int>();

            foreach (Vector2Int point in allLandPoints)
            {
                if (shorePoints.Contains(point) ||
                    !IsGrassLandPoint(point, allLandPoints, areaByPoint))
                {
                    continue;
                }

                if (!HasDirectOceanAndGrassBridgeRisk(point, allLandPoints, shorePoints))
                {
                    continue;
                }

                if (TryCreateMinimumWidthShoreUnitFromGrassPoint(
                        point,
                        shorePoints,
                        allLandPoints,
                        areaByPoint,
                        out List<ShoreSandPlacement> unitAdditions))
                {
                    for (int addIndex = 0; addIndex < unitAdditions.Count; addIndex++)
                    {
                        ShoreSandPlacement addition = unitAdditions[addIndex];
                        if (pendingAdditions.Add(addition.point))
                        {
                            additions.Add(addition);
                            MarkChangedPoint(changedPoints, addition.point);
                            TraceShoreSandPlacement(
                                addition.point,
                                "ResolveRemainingSingleTileCoastalGaps",
                                "Grass",
                                addition,
                                $"Added by coastal gap resolution from sourcePoint={point} bridgeRisk=true");
                        }
                    }
                }
            }

            if (additions.Count > 0)
            {
                placements.AddRange(additions);
            }
        }

        private void ReclassifyAffectedShoreSandPlacements(
            List<ShoreSandPlacement> placements,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> changedPoints)
        {
            if (placements == null || placements.Count == 0)
            {
                return;
            }

            HashSet<Vector2Int> shorePoints = BuildPlacementPointSet(placements);
            HashSet<Vector2Int> expandedAffectedPoints = ExpandChangedPointsToNeighborhood(changedPoints, shorePoints);
            HashSet<Vector2Int> finalBoundaryPoints = CollectFinalShoreBoundaryReclassificationPoints(
                placements,
                allLandPoints,
                areaByPoint,
                shorePoints);
            HashSet<Vector2Int> affectedPoints = new HashSet<Vector2Int>(expandedAffectedPoints);
            affectedPoints.UnionWith(finalBoundaryPoints);
            Dictionary<Vector2Int, int> indexByPoint = BuildPlacementIndexByPoint(placements);

            if (ShouldTraceShoreSandPoint(debugShoreSandGridPoint))
            {
                CountOrdinaryGrassNeighborDirections(
                    debugShoreSandGridPoint,
                    allLandPoints,
                    areaByPoint,
                    shorePoints,
                    out List<ShoreEdgeDirection> traceGrassDirs,
                    out _);
                List<ShoreEdgeDirection> traceSeaDirs = CollectSeaEdgeDirections(debugShoreSandGridPoint, allLandPoints);
                List<ShoreEdgeDirection> traceDiagGrassDirs = CollectDiagonalOrdinaryGrassDirections(debugShoreSandGridPoint, allLandPoints, areaByPoint, shorePoints);
                List<ShoreEdgeDirection> traceDiagSeaDirs = CollectDiagonalSeaDirections(debugShoreSandGridPoint, allLandPoints);
                TraceShoreSandDecision(
                    debugShoreSandGridPoint,
                    "ReclassifyAffectedShoreSandPlacements",
                    GetPlacementTypeAtPoint(placements, debugShoreSandGridPoint),
                    GetPlacementTypeAtPoint(placements, debugShoreSandGridPoint),
                    $"Current Placement Type={GetPlacementTypeAtPoint(placements, debugShoreSandGridPoint)} changedPointsContains={changedPoints != null && changedPoints.Contains(debugShoreSandGridPoint)} expandedAffectedPointsContains={expandedAffectedPoints.Contains(debugShoreSandGridPoint)} finalBoundaryCandidateContains={finalBoundaryPoints.Contains(debugShoreSandGridPoint)} affectedPointsContains={affectedPoints.Contains(debugShoreSandGridPoint)} Skipped Reclassification={!affectedPoints.Contains(debugShoreSandGridPoint)} Skip Reason={(affectedPoints.Contains(debugShoreSandGridPoint) ? "None" : "Not in affected points")} orthogonalOcean={FormatDirectionList(traceSeaDirs)} orthogonalGrass={FormatDirectionList(traceGrassDirs)} diagonalOcean={FormatDirectionList(traceDiagSeaDirs)} diagonalGrass={FormatDirectionList(traceDiagGrassDirs)}",
                    null);
                TraceOrdinaryGrassNeighborStates(debugShoreSandGridPoint, allLandPoints, areaByPoint, shorePoints);
            }

            foreach (Vector2Int point in affectedPoints)
            {
                if (!indexByPoint.TryGetValue(point, out int index))
                {
                    continue;
                }

                ShoreSandPlacement currentPlacement = placements[index];
                List<ShoreEdgeDirection> realSeaDirs = CollectSeaEdgeDirections(point, allLandPoints);
                CountOrdinaryGrassNeighborDirections(
                    point,
                    allLandPoints,
                    areaByPoint,
                    shorePoints,
                    out List<ShoreEdgeDirection> realGrassDirs,
                    out ShoreEdgeDirection singleGrassDirection);

                if (realSeaDirs.Count > 0)
                {
                    ShoreEdgeDirection seaDirection = realSeaDirs[0];
                    ShoreSandPlacement newPlacement = new ShoreSandPlacement(
                        point,
                        shoreSandOceanTransitionPrefab,
                        seaDirection,
                        true,
                        true,
                        false,
                        currentPlacement.grassNeighborCount,
                        currentPlacement.usedFixedPrioritySelection,
                        currentPlacement.fromAdjacentTwoGrass);
                    placements[index] = newPlacement;
                    TraceShoreSandPlacement(
                        point,
                        "ReclassifyAffectedShoreSandPlacements",
                        GetShoreSandPlacementDebugType(currentPlacement),
                        newPlacement,
                        $"Orthogonal ocean found: {FormatDirectionList(realSeaDirs)}");
                    continue;
                }

                if (realGrassDirs.Count > 0)
                {
                    ShoreEdgeDirection grassDirection = singleGrassDirection;
                    if (realGrassDirs.Count >= 2 &&
                        TryResolveAdjacentTwoGrassPrimaryDirection(
                            point,
                            allLandPoints,
                            shorePoints,
                            realGrassDirs,
                            out ShoreEdgeDirection resolvedGrassDirection,
                            out _))
                    {
                        grassDirection = resolvedGrassDirection;
                    }

                    ShoreSandPlacement newPlacement = new ShoreSandPlacement(
                        point,
                        shoreSandGrassTransitionPrefab,
                        grassDirection,
                        false,
                        false,
                        true,
                        realGrassDirs.Count);
                    placements[index] = newPlacement;
                    TraceShoreSandPlacement(
                        point,
                        "ReclassifyAffectedShoreSandPlacements",
                        GetShoreSandPlacementDebugType(currentPlacement),
                        newPlacement,
                        $"Orthogonal grass found: {FormatDirectionList(realGrassDirs)} selectedGrassDirection={grassDirection}");
                    continue;
                }

                ShoreSandPlacement normalPlacement = new ShoreSandPlacement(
                    point,
                    shoreSandNormalPrefab,
                    currentPlacement.direction,
                    false,
                    false,
                    false);
                placements[index] = normalPlacement;
                TraceShoreSandPlacement(
                    point,
                    "ReclassifyAffectedShoreSandPlacements",
                    GetShoreSandPlacementDebugType(currentPlacement),
                    normalPlacement,
                    "No orthogonal ocean or ordinary grass neighbors after final state");
            }
        }

        private HashSet<Vector2Int> CollectFinalShoreBoundaryReclassificationPoints(
            List<ShoreSandPlacement> placements,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> shorePoints)
        {
            HashSet<Vector2Int> points = new HashSet<Vector2Int>();
            if (placements == null || allLandPoints == null || areaByPoint == null || shorePoints == null)
            {
                return points;
            }

            for (int i = 0; i < placements.Count; i++)
            {
                ShoreSandPlacement placement = placements[i];
                Vector2Int point = placement.point;

                List<ShoreEdgeDirection> seaDirs = CollectSeaEdgeDirections(point, allLandPoints);
                CountOrdinaryGrassNeighborDirections(
                    point,
                    allLandPoints,
                    areaByPoint,
                    shorePoints,
                    out List<ShoreEdgeDirection> grassDirs,
                    out _);

                bool isBoundaryCandidate = seaDirs.Count > 0 || grassDirs.Count > 0;
                bool isCornerCandidate =
                    IsOceanOuterCornerPlacement(placement) ||
                    IsOceanInnerCornerPlacement(placement) ||
                    IsGrassOuterCornerPlacement(placement) ||
                    IsGrassInnerCornerPlacement(placement);

                if (isBoundaryCandidate || isCornerCandidate)
                {
                    points.Add(point);
                }
            }

            return points;
        }

        private void ApplyShortGrassBoundarySegmentFix(
            List<ShoreSandPlacement> placements,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint)
        {
            if (placements == null || placements.Count < 2)
            {
                return;
            }

            HashSet<Vector2Int> shorePoints = BuildPlacementPointSet(placements);
            Dictionary<Vector2Int, int> indexByPoint = BuildPlacementIndexByPoint(placements);
            HashSet<Vector2Int> boundaryPoints = new HashSet<Vector2Int>();

            for (int i = 0; i < placements.Count; i++)
            {
                Vector2Int point = placements[i].point;
                List<ShoreEdgeDirection> seaDirs = CollectSeaEdgeDirections(point, allLandPoints);
                CountOrdinaryGrassNeighborDirections(
                    point,
                    allLandPoints,
                    areaByPoint,
                    shorePoints,
                    out List<ShoreEdgeDirection> grassDirs,
                    out _);

                if (seaDirs.Count == 0 && grassDirs.Count > 0)
                {
                    boundaryPoints.Add(point);
                }
            }

            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            foreach (Vector2Int startPoint in boundaryPoints)
            {
                if (visited.Contains(startPoint))
                {
                    continue;
                }

                List<Vector2Int> component = new List<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(startPoint);
                visited.Add(startPoint);

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    component.Add(current);

                    TryEnqueueBoundaryNeighbor(current + Vector2Int.up, boundaryPoints, visited, queue);
                    TryEnqueueBoundaryNeighbor(current + Vector2Int.down, boundaryPoints, visited, queue);
                    TryEnqueueBoundaryNeighbor(current + Vector2Int.left, boundaryPoints, visited, queue);
                    TryEnqueueBoundaryNeighbor(current + Vector2Int.right, boundaryPoints, visited, queue);
                }

                if (component.Count >= minimumShoreSandFootprint)
                {
                    continue;
                }

                if (ShouldTraceShoreSandPoint(debugShoreSandGridPoint) && component.Contains(debugShoreSandGridPoint))
                {
                    TraceShoreSandDecision(
                        debugShoreSandGridPoint,
                        "ApplyShortGrassBoundarySegmentFix",
                        GetPlacementTypeAtPoint(placements, debugShoreSandGridPoint),
                        GetPlacementTypeAtPoint(placements, debugShoreSandGridPoint),
                        $"Boundary segment includes point. segmentLength={component.Count} component={FormatPointCollection(component)}",
                        null);
                }

                for (int componentIndex = 0; componentIndex < component.Count; componentIndex++)
                {
                    Vector2Int point = component[componentIndex];
                    if (!indexByPoint.TryGetValue(point, out int index))
                    {
                        continue;
                    }

                    CountOrdinaryGrassNeighborDirections(
                        point,
                        allLandPoints,
                        areaByPoint,
                        shorePoints,
                        out List<ShoreEdgeDirection> grassDirs,
                        out ShoreEdgeDirection singleGrassDirection);

                    ShoreEdgeDirection grassDirection = singleGrassDirection;
                    if (grassDirs.Count >= 2 &&
                        TryResolveAdjacentTwoGrassPrimaryDirection(
                            point,
                            allLandPoints,
                            shorePoints,
                            grassDirs,
                            out ShoreEdgeDirection resolvedGrassDirection,
                            out _))
                    {
                        grassDirection = resolvedGrassDirection;
                    }

                    string oldType = GetShoreSandPlacementDebugType(placements[index]);
                    ShoreSandPlacement newPlacement = new ShoreSandPlacement(
                        point,
                        shoreSandGrassTransitionPrefab,
                        grassDirection,
                        false,
                        false,
                        true,
                        grassDirs.Count);
                    placements[index] = newPlacement;
                    TraceShoreSandPlacement(
                        point,
                        "ApplyShortGrassBoundarySegmentFix",
                        oldType,
                        newPlacement,
                        $"Forced short grass boundary segment to GrassTransition. segmentLength={component.Count} component={FormatPointCollection(component)} grassDirection={grassDirection} grassDirs={FormatDirectionList(grassDirs)}");
                }
            }
        }

        private static HashSet<Vector2Int> BuildPlacementPointSet(List<ShoreSandPlacement> placements)
        {
            HashSet<Vector2Int> points = new HashSet<Vector2Int>();
            if (placements == null)
            {
                return points;
            }

            for (int i = 0; i < placements.Count; i++)
            {
                points.Add(placements[i].point);
            }

            return points;
        }

        private static Dictionary<Vector2Int, int> BuildPlacementIndexByPoint(List<ShoreSandPlacement> placements)
        {
            Dictionary<Vector2Int, int> indexByPoint = new Dictionary<Vector2Int, int>();
            if (placements == null)
            {
                return indexByPoint;
            }

            for (int i = 0; i < placements.Count; i++)
            {
                indexByPoint[placements[i].point] = i;
            }

            return indexByPoint;
        }

        private static bool IsSingleTileGrassBottleneck(Vector2Int point, HashSet<Vector2Int> shorePoints)
        {
            if (shorePoints == null)
            {
                return false;
            }

            bool blockedLeftRight =
                shorePoints.Contains(point + Vector2Int.left) &&
                shorePoints.Contains(point + Vector2Int.right);
            bool blockedUpDown =
                shorePoints.Contains(point + Vector2Int.up) &&
                shorePoints.Contains(point + Vector2Int.down);

            return blockedLeftRight || blockedUpDown;
        }

        private bool TryWidenNarrowShoreBranch(
            HashSet<Vector2Int> branchPoints,
            HashSet<Vector2Int> shorePoints,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            out List<ShoreSandPlacement> additions)
        {
            additions = null;
            if (branchPoints == null || branchPoints.Count == 0 || shorePoints == null)
            {
                return false;
            }

            Vector2Int[] cardinalOffsets =
            {
                Vector2Int.left,
                Vector2Int.right,
                Vector2Int.up,
                Vector2Int.down
            };

            for (int i = 0; i < cardinalOffsets.Length; i++)
            {
                if (!TryBuildWidenedBranchAdditions(
                        branchPoints,
                        cardinalOffsets[i],
                        shorePoints,
                        allLandPoints,
                        areaByPoint,
                        out List<Vector2Int> candidatePoints))
                {
                    continue;
                }

                HashSet<Vector2Int> augmentedShorePoints = new HashSet<Vector2Int>(shorePoints);
                for (int candidateIndex = 0; candidateIndex < candidatePoints.Count; candidateIndex++)
                {
                    augmentedShorePoints.Add(candidatePoints[candidateIndex]);
                }

                if (!BranchHasMinimumThickness(branchPoints, candidatePoints, augmentedShorePoints, minimumShoreSandFootprint))
                {
                    continue;
                }

                additions = new List<ShoreSandPlacement>(candidatePoints.Count);
                for (int candidateIndex = 0; candidateIndex < candidatePoints.Count; candidateIndex++)
                {
                    additions.Add(CreateIntermediateShorePlacement(candidatePoints[candidateIndex], allLandPoints));
                }

                return true;
            }

            return false;
        }

        private bool TryBuildWidenedBranchAdditions(
            HashSet<Vector2Int> branchPoints,
            Vector2Int offset,
            HashSet<Vector2Int> shorePoints,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            out List<Vector2Int> candidatePoints)
        {
            candidatePoints = new List<Vector2Int>();
            HashSet<Vector2Int> candidateSet = new HashSet<Vector2Int>();

            for (int layer = 1; layer < minimumShoreSandFootprint; layer++)
            {
                foreach (Vector2Int branchPoint in branchPoints)
                {
                    Vector2Int candidatePoint = branchPoint + (offset * layer);
                    if (branchPoints.Contains(candidatePoint) || shorePoints.Contains(candidatePoint) || candidateSet.Contains(candidatePoint))
                    {
                        continue;
                    }

                    if (!CanPromoteGrassPointToShore(candidatePoint, allLandPoints, areaByPoint, shorePoints))
                    {
                        return false;
                    }

                    if (candidateSet.Add(candidatePoint))
                    {
                        candidatePoints.Add(candidatePoint);
                    }
                }
            }

            return candidatePoints.Count > 0;
        }

        private bool TryCreateMinimumWidthShoreUnitFromGrassPoint(
            Vector2Int point,
            HashSet<Vector2Int> shorePoints,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            out List<ShoreSandPlacement> additions)
        {
            additions = null;
            Vector2Int[] horizontalOffsets = { Vector2Int.left, Vector2Int.right };
            Vector2Int[] verticalOffsets = { Vector2Int.up, Vector2Int.down };

            for (int h = 0; h < horizontalOffsets.Length; h++)
            {
                for (int v = 0; v < verticalOffsets.Length; v++)
                {
                    List<Vector2Int> footprint = new List<Vector2Int>(minimumShoreSandFootprint * minimumShoreSandFootprint);
                    for (int x = 0; x < minimumShoreSandFootprint; x++)
                    {
                        for (int y = 0; y < minimumShoreSandFootprint; y++)
                        {
                            footprint.Add(point + (horizontalOffsets[h] * x) + (verticalOffsets[v] * y));
                        }
                    }

                    if (!AllPointsAreLand(footprint, allLandPoints))
                    {
                        continue;
                    }

                    List<Vector2Int> candidatePoints = new List<Vector2Int>();
                    bool canUseFootprint = true;
                    for (int i = 0; i < footprint.Count; i++)
                    {
                        Vector2Int footprintPoint = footprint[i];
                        if (shorePoints.Contains(footprintPoint))
                        {
                            continue;
                        }

                        if (!CanPromoteGrassPointToShore(footprintPoint, allLandPoints, areaByPoint, shorePoints))
                        {
                            canUseFootprint = false;
                            break;
                        }

                        candidatePoints.Add(footprintPoint);
                    }

                    if (!canUseFootprint || candidatePoints.Count == 0)
                    {
                        continue;
                    }

                    HashSet<Vector2Int> augmentedShorePoints = new HashSet<Vector2Int>(shorePoints);
                    for (int i = 0; i < candidatePoints.Count; i++)
                    {
                        augmentedShorePoints.Add(candidatePoints[i]);
                    }

                    if (!BranchHasMinimumThickness(new HashSet<Vector2Int>(footprint), candidatePoints, augmentedShorePoints, minimumShoreSandFootprint))
                    {
                        continue;
                    }

                    additions = new List<ShoreSandPlacement>(candidatePoints.Count);
                    for (int i = 0; i < candidatePoints.Count; i++)
                    {
                        additions.Add(CreateIntermediateShorePlacement(candidatePoints[i], allLandPoints));
                    }

                    return true;
                }
            }

            return false;
        }

        private static bool AllPointsAreLand(IList<Vector2Int> points, HashSet<Vector2Int> allLandPoints)
        {
            if (points == null || allLandPoints == null)
            {
                return false;
            }

            for (int i = 0; i < points.Count; i++)
            {
                if (!allLandPoints.Contains(points[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private ShoreSandPlacement CreateIntermediateShorePlacement(
            Vector2Int point,
            HashSet<Vector2Int> allLandPoints)
        {
            if (TryGetPreferredSeaEdgeDirection(point, allLandPoints, out ShoreEdgeDirection seaDirection))
            {
                return new ShoreSandPlacement(
                    point,
                    shoreSandOceanTransitionPrefab,
                    seaDirection,
                    true,
                    true,
                    false);
            }

            if (TryGetPreferredCoastalDirection(point, allLandPoints, out ShoreEdgeDirection coastalDirection))
            {
                return new ShoreSandPlacement(
                    point,
                    shoreSandGrassTransitionPrefab,
                    GetOppositeDirection(coastalDirection),
                    false,
                    false,
                    true);
            }

            return new ShoreSandPlacement(
                point,
                shoreSandNormalPrefab,
                ShoreEdgeDirection.Up,
                false,
                false,
                false);
        }

        private static bool BranchHasMinimumThickness(
            HashSet<Vector2Int> branchPoints,
            List<Vector2Int> candidatePoints,
            HashSet<Vector2Int> augmentedShorePoints,
            int minimumWidth)
        {
            foreach (Vector2Int point in branchPoints)
            {
                if (!HasMinimumShoreThicknessOnBothAxes(point, augmentedShorePoints, minimumWidth))
                {
                    return false;
                }
            }

            if (candidatePoints != null)
            {
                for (int i = 0; i < candidatePoints.Count; i++)
                {
                    if (!HasMinimumShoreThicknessOnBothAxes(candidatePoints[i], augmentedShorePoints, minimumWidth))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private List<HashSet<Vector2Int>> CollectNarrowShoreBranches(HashSet<Vector2Int> shorePoints)
        {
            List<HashSet<Vector2Int>> branches = new List<HashSet<Vector2Int>>();
            if (shorePoints == null || shorePoints.Count == 0)
            {
                return branches;
            }

            HashSet<Vector2Int> corePoints = BuildShoreCorePointSet(shorePoints);
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

            foreach (Vector2Int point in shorePoints)
            {
                if (corePoints.Contains(point) || visited.Contains(point))
                {
                    continue;
                }

                HashSet<Vector2Int> branch = new HashSet<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(point);
                visited.Add(point);

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    branch.Add(current);

                    TryEnqueueNonCoreNeighbor(current + Vector2Int.up, shorePoints, corePoints, visited, queue);
                    TryEnqueueNonCoreNeighbor(current + Vector2Int.down, shorePoints, corePoints, visited, queue);
                    TryEnqueueNonCoreNeighbor(current + Vector2Int.left, shorePoints, corePoints, visited, queue);
                    TryEnqueueNonCoreNeighbor(current + Vector2Int.right, shorePoints, corePoints, visited, queue);
                }

                if (branch.Count > 0)
                {
                    branches.Add(branch);
                }
            }

            return branches;
        }

        private static List<HashSet<Vector2Int>> CollectNarrowTerrainBranches(
            HashSet<Vector2Int> terrainPoints,
            System.Func<Vector2Int, bool> isCorePoint)
        {
            List<HashSet<Vector2Int>> branches = new List<HashSet<Vector2Int>>();
            if (terrainPoints == null || terrainPoints.Count == 0 || isCorePoint == null)
            {
                return branches;
            }

            HashSet<Vector2Int> corePoints = new HashSet<Vector2Int>();
            foreach (Vector2Int point in terrainPoints)
            {
                if (isCorePoint(point))
                {
                    corePoints.Add(point);
                }
            }

            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            foreach (Vector2Int point in terrainPoints)
            {
                if (corePoints.Contains(point) || visited.Contains(point))
                {
                    continue;
                }

                HashSet<Vector2Int> branch = new HashSet<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(point);
                visited.Add(point);

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    branch.Add(current);

                    TryEnqueueNonCoreNeighbor(current + Vector2Int.up, terrainPoints, corePoints, visited, queue);
                    TryEnqueueNonCoreNeighbor(current + Vector2Int.down, terrainPoints, corePoints, visited, queue);
                    TryEnqueueNonCoreNeighbor(current + Vector2Int.left, terrainPoints, corePoints, visited, queue);
                    TryEnqueueNonCoreNeighbor(current + Vector2Int.right, terrainPoints, corePoints, visited, queue);
                }

                if (branch.Count > 0)
                {
                    branches.Add(branch);
                }
            }

            return branches;
        }

        private HashSet<Vector2Int> BuildShoreCorePointSet(HashSet<Vector2Int> shorePoints)
        {
            HashSet<Vector2Int> corePoints = new HashSet<Vector2Int>();
            if (shorePoints == null)
            {
                return corePoints;
            }

            foreach (Vector2Int point in shorePoints)
            {
                if (HasMinimumShoreThicknessOnBothAxes(point, shorePoints, minimumShoreSandFootprint) ||
                    HasValidTwoDimensionalSupport(point, shorePoints, minimumShoreSandFootprint))
                {
                    corePoints.Add(point);
                }
            }

            return corePoints;
        }

        private static void TryEnqueueNonCoreNeighbor(
            Vector2Int point,
            HashSet<Vector2Int> shorePoints,
            HashSet<Vector2Int> corePoints,
            HashSet<Vector2Int> visited,
            Queue<Vector2Int> queue)
        {
            if (!shorePoints.Contains(point) || corePoints.Contains(point) || visited.Contains(point))
            {
                return;
            }

            visited.Add(point);
            queue.Enqueue(point);
        }

        private static void TryEnqueueBoundaryNeighbor(
            Vector2Int point,
            HashSet<Vector2Int> boundaryPoints,
            HashSet<Vector2Int> visited,
            Queue<Vector2Int> queue)
        {
            if (!boundaryPoints.Contains(point) || visited.Contains(point))
            {
                return;
            }

            visited.Add(point);
            queue.Enqueue(point);
        }

        private static bool CanPromoteGrassPointToShore(
            Vector2Int point,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> shorePoints)
        {
            return !shorePoints.Contains(point) &&
                   IsGrassLandPoint(point, allLandPoints, areaByPoint);
        }

        private static HashSet<Vector2Int> BuildGrassSupportPointSet(
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> shorePoints)
        {
            HashSet<Vector2Int> grassPoints = new HashSet<Vector2Int>();
            if (allLandPoints == null || areaByPoint == null)
            {
                return grassPoints;
            }

            foreach (Vector2Int point in allLandPoints)
            {
                if (!shorePoints.Contains(point) &&
                    areaByPoint.TryGetValue(point, out AreaType areaType) &&
                    areaType == AreaType.Grass)
                {
                    grassPoints.Add(point);
                }
            }

            return grassPoints;
        }

        private List<Vector2Int> CollectUnsupportedPoints(HashSet<Vector2Int> points, int minimumWidth)
        {
            List<Vector2Int> unsupportedPoints = new List<Vector2Int>();
            if (points == null || points.Count == 0)
            {
                return unsupportedPoints;
            }

            foreach (Vector2Int point in points)
            {
                if (!HasValidTwoDimensionalSupport(point, points, minimumWidth))
                {
                    unsupportedPoints.Add(point);
                }
            }

            return unsupportedPoints;
        }

        private static bool HasValidTwoDimensionalSupport(Vector2Int point, HashSet<Vector2Int> supportPoints, int minimumWidth)
        {
            if (supportPoints == null || !supportPoints.Contains(point))
            {
                return false;
            }

            for (int xStart = 0; xStart < minimumWidth; xStart++)
            {
                for (int yStart = 0; yStart < minimumWidth; yStart++)
                {
                    if (FormsSupportedSquareSupport(point, -xStart, -yStart, minimumWidth, supportPoints))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasMinimumShoreThicknessOnBothAxes(Vector2Int point, HashSet<Vector2Int> shorePoints, int minimumWidth)
        {
            if (shorePoints == null || !shorePoints.Contains(point))
            {
                return false;
            }

            return CountContiguousRunLength(point, Vector2Int.left, Vector2Int.right, shorePoints) >= minimumWidth &&
                   CountContiguousRunLength(point, Vector2Int.down, Vector2Int.up, shorePoints) >= minimumWidth;
        }

        private static bool FormsSupportedSquareSupport(
            Vector2Int anchorPoint,
            int xStartOffset,
            int yStartOffset,
            int size,
            HashSet<Vector2Int> supportPoints)
        {
            Vector2Int start = new Vector2Int(anchorPoint.x + xStartOffset, anchorPoint.y + yStartOffset);
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    if (!supportPoints.Contains(new Vector2Int(start.x + x, start.y + y)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void MarkChangedPoint(HashSet<Vector2Int> changedPoints, Vector2Int point)
        {
            changedPoints?.Add(point);
        }

        private static HashSet<Vector2Int> ExpandChangedPointsToNeighborhood(
            HashSet<Vector2Int> changedPoints,
            HashSet<Vector2Int> shorePoints)
        {
            HashSet<Vector2Int> expanded = new HashSet<Vector2Int>();
            if (changedPoints == null || changedPoints.Count == 0)
            {
                return expanded;
            }

            foreach (Vector2Int point in changedPoints)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        Vector2Int neighbor = new Vector2Int(point.x + dx, point.y + dy);
                        if (shorePoints.Contains(neighbor))
                        {
                            expanded.Add(neighbor);
                        }
                    }
                }
            }

            return expanded;
        }

        private static bool HasDirectOceanAndGrassBridgeRisk(
            Vector2Int point,
            HashSet<Vector2Int> allLandPoints,
            HashSet<Vector2Int> shorePoints)
        {
            if (shorePoints == null || shorePoints.Contains(point))
            {
                return false;
            }

            bool touchesOcean = !allLandPoints.Contains(point + Vector2Int.up) ||
                                !allLandPoints.Contains(point + Vector2Int.down) ||
                                !allLandPoints.Contains(point + Vector2Int.left) ||
                                !allLandPoints.Contains(point + Vector2Int.right);

            if (!touchesOcean)
            {
                return false;
            }

            int shoreNeighborCount = 0;
            if (shorePoints.Contains(point + Vector2Int.up))
            {
                shoreNeighborCount++;
            }

            if (shorePoints.Contains(point + Vector2Int.down))
            {
                shoreNeighborCount++;
            }

            if (shorePoints.Contains(point + Vector2Int.left))
            {
                shoreNeighborCount++;
            }

            if (shorePoints.Contains(point + Vector2Int.right))
            {
                shoreNeighborCount++;
            }

            return shoreNeighborCount <= 1;
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

        private bool TryResolveBoundaryCleanupAddition(
            Vector2Int point,
            HashSet<Vector2Int> sourceShoreSandPoints,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            out string reason,
            out ShoreEdgeDirection primaryDirection,
            out ShoreEdgeDirection secondaryDirection)
        {
            reason = null;
            primaryDirection = ShoreEdgeDirection.Up;
            secondaryDirection = ShoreEdgeDirection.Right;

            if (TryMatchBoundaryCleanupPattern(
                    point,
                    ShoreEdgeDirection.Up,
                    ShoreEdgeDirection.Right,
                    sourceShoreSandPoints,
                    allLandPoints,
                    areaByPoint,
                    out reason))
            {
                primaryDirection = ShoreEdgeDirection.Up;
                secondaryDirection = ShoreEdgeDirection.Right;
                return true;
            }

            if (TryMatchBoundaryCleanupPattern(
                    point,
                    ShoreEdgeDirection.Right,
                    ShoreEdgeDirection.Down,
                    sourceShoreSandPoints,
                    allLandPoints,
                    areaByPoint,
                    out reason))
            {
                primaryDirection = ShoreEdgeDirection.Right;
                secondaryDirection = ShoreEdgeDirection.Down;
                return true;
            }

            if (TryMatchBoundaryCleanupPattern(
                    point,
                    ShoreEdgeDirection.Down,
                    ShoreEdgeDirection.Left,
                    sourceShoreSandPoints,
                    allLandPoints,
                    areaByPoint,
                    out reason))
            {
                primaryDirection = ShoreEdgeDirection.Down;
                secondaryDirection = ShoreEdgeDirection.Left;
                return true;
            }

            if (TryMatchBoundaryCleanupPattern(
                    point,
                    ShoreEdgeDirection.Left,
                    ShoreEdgeDirection.Up,
                    sourceShoreSandPoints,
                    allLandPoints,
                    areaByPoint,
                    out reason))
            {
                primaryDirection = ShoreEdgeDirection.Left;
                secondaryDirection = ShoreEdgeDirection.Up;
                return true;
            }

            return false;
        }

        private bool TryMatchBoundaryCleanupPattern(
            Vector2Int point,
            ShoreEdgeDirection directionA,
            ShoreEdgeDirection directionB,
            HashSet<Vector2Int> sourceShoreSandPoints,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            out string reason)
        {
            reason = null;

            Vector2Int offsetA = GetCardinalOffset(directionA);
            Vector2Int offsetB = GetCardinalOffset(directionB);
            Vector2Int neighborA = point + offsetA;
            Vector2Int neighborB = point + offsetB;
            Vector2Int diagonalPoint = point + offsetA + offsetB;

            if (!sourceShoreSandPoints.Contains(neighborA) ||
                !sourceShoreSandPoints.Contains(neighborB))
            {
                return false;
            }

            int runLengthA = CountConsecutiveShoreRun(neighborA, directionA, sourceShoreSandPoints);
            int runLengthB = CountConsecutiveShoreRun(neighborB, directionB, sourceShoreSandPoints);
            bool shortCornerRun = runLengthA < 2 || runLengthB < 2;

            if (!shortCornerRun)
            {
                return false;
            }

            if (!allLandPoints.Contains(diagonalPoint))
            {
                reason = "OceanSingleStep";
                return true;
            }

            if (IsOrdinaryGrassPoint(diagonalPoint, allLandPoints, areaByPoint, sourceShoreSandPoints))
            {
                reason = "GrassSingleStep";
                return true;
            }

            return false;
        }

        private static int CountConsecutiveShoreRun(
            Vector2Int startPoint,
            ShoreEdgeDirection direction,
            HashSet<Vector2Int> shoreSandPoints)
        {
            if (shoreSandPoints == null || !shoreSandPoints.Contains(startPoint))
            {
                return 0;
            }

            Vector2Int offset = GetCardinalOffset(direction);
            int count = 0;
            Vector2Int current = startPoint;

            while (shoreSandPoints.Contains(current))
            {
                count++;
                current += offset;
            }

            return count;
        }

        private void ApplyFinalCornerResolution(
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
                List<ShoreEdgeDirection> realSeaDirs = CollectSeaEdgeDirections(placement.point, allLandPoints);
                CountOrdinaryGrassNeighborDirections(
                    placement.point,
                    allLandPoints,
                    areaByPoint,
                    finalShoreSandPoints,
                    out List<ShoreEdgeDirection> realOrdinaryGrassDirs,
                    out _);

                string selectedCornerType = "None";
                string fallbackType = GetShoreSandPlacementDebugType(placement);
                string reason = "no-corner";
                List<ShoreEdgeDirection> selectedCornerDirs = null;
                float? selectedCornerRotationY = null;

                if (shoreSandOceanOuterCornerPrefab != null &&
                    TryGetAdjacentDirectionPair(realSeaDirs, out ShoreEdgeDirection oceanPrimaryDir, out ShoreEdgeDirection oceanSecondaryDir, out float oceanCornerYaw))
                {
                    placements[i] = new ShoreSandPlacement(
                        placement.point,
                        shoreSandOceanOuterCornerPrefab,
                        oceanPrimaryDir,
                        true,
                        true,
                        false,
                        placement.grassNeighborCount,
                        placement.usedFixedPrioritySelection,
                        placement.fromAdjacentTwoGrass,
                        true,
                        oceanCornerYaw,
                        true,
                        oceanSecondaryDir);

                    selectedCornerType = "ShoreSand_OceanOuterCorner";
                    selectedCornerDirs = new List<ShoreEdgeDirection> { oceanPrimaryDir, oceanSecondaryDir };
                    selectedCornerRotationY = oceanCornerYaw;
                    reason = "real-adjacent-sea-pair";
                }
                else if (realSeaDirs.Count == 0 &&
                         shoreSandOceanInnerCornerPrefab != null &&
                         TryResolveOceanInnerCornerFromGeometry(
                             placement.point,
                             finalShoreSandPoints,
                             allLandPoints,
                             out ShoreEdgeDirection oceanInnerPrimaryDir,
                             out ShoreEdgeDirection oceanInnerSecondaryDir,
                             out float oceanInnerYaw))
                {
                    placements[i] = new ShoreSandPlacement(
                        placement.point,
                        shoreSandOceanInnerCornerPrefab,
                        oceanInnerPrimaryDir,
                        true,
                        true,
                        false,
                        placement.grassNeighborCount,
                        placement.usedFixedPrioritySelection,
                        placement.fromAdjacentTwoGrass,
                        true,
                        oceanInnerYaw,
                        true,
                        oceanInnerSecondaryDir);

                    selectedCornerType = "ShoreSand_OceanInnerCorner";
                    selectedCornerDirs = new List<ShoreEdgeDirection> { oceanInnerPrimaryDir, oceanInnerSecondaryDir };
                    selectedCornerRotationY = oceanInnerYaw;
                    reason = "ocean-inner-diagonal-sea-two-shore-neighbors";
                }
                else if (shoreSandGrassInnerCornerPrefab != null &&
                         TryResolveGrassInnerCornerFromGeometry(
                             placement.point,
                             finalShoreSandPoints,
                             allLandPoints,
                             areaByPoint,
                             out ShoreEdgeDirection grassInnerPrimaryDir,
                             out ShoreEdgeDirection grassInnerSecondaryDir,
                             out float grassInnerYaw))
                {
                    placements[i] = new ShoreSandPlacement(
                        placement.point,
                        shoreSandGrassInnerCornerPrefab,
                        grassInnerPrimaryDir,
                        false,
                        false,
                        false,
                        placement.grassNeighborCount,
                        placement.usedFixedPrioritySelection,
                        placement.fromAdjacentTwoGrass,
                        true,
                        grassInnerYaw,
                        true,
                        grassInnerSecondaryDir);

                    selectedCornerType = "ShoreSand_GrassInnerCorner";
                    selectedCornerDirs = new List<ShoreEdgeDirection> { grassInnerPrimaryDir, grassInnerSecondaryDir };
                    selectedCornerRotationY = grassInnerYaw;
                    reason = "grass-inner-diagonal-grass-two-shore-neighbors";
                }
                else if (realSeaDirs.Count == 0 &&
                         shoreSandGrassOuterCornerPrefab != null &&
                         TryGetAdjacentDirectionPair(realOrdinaryGrassDirs, out ShoreEdgeDirection grassPrimaryDir, out ShoreEdgeDirection grassSecondaryDir, out float grassCornerYaw))
                {
                    placements[i] = new ShoreSandPlacement(
                        placement.point,
                        shoreSandGrassOuterCornerPrefab,
                        grassPrimaryDir,
                        false,
                        false,
                        false,
                        placement.grassNeighborCount,
                        placement.usedFixedPrioritySelection,
                        placement.fromAdjacentTwoGrass,
                        true,
                        grassCornerYaw,
                        true,
                        grassSecondaryDir);

                    selectedCornerType = "ShoreSand_GrassOuterCorner";
                    selectedCornerDirs = new List<ShoreEdgeDirection> { grassPrimaryDir, grassSecondaryDir };
                    selectedCornerRotationY = grassCornerYaw;
                    reason = "real-adjacent-ordinary-grass-pair";
                }
                else if (shoreSandOceanInnerCornerPrefab != null)
                {
                    reason = "ocean-inner-disabled-safe-fallback";
                }

                ShoreSandPlacement finalPlacementForTrace = placements[i];
                TraceShoreSandPlacement(
                    finalPlacementForTrace.point,
                    "ApplyFinalCornerResolution",
                    fallbackType,
                    finalPlacementForTrace,
                    $"selectedCornerType={selectedCornerType} reason={reason} orthogonalOcean={FormatDirectionList(realSeaDirs)} orthogonalGrass={FormatDirectionList(realOrdinaryGrassDirs)} diagonalOcean={FormatDirectionList(CollectDiagonalSeaDirections(finalPlacementForTrace.point, allLandPoints))} diagonalGrass={FormatDirectionList(CollectDiagonalOrdinaryGrassDirections(finalPlacementForTrace.point, allLandPoints, areaByPoint, finalShoreSandPoints))}");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                ShoreSandPlacement finalPlacement = placements[i];
                LogCornerDecision(
                    finalPlacement,
                    realSeaDirs,
                    realOrdinaryGrassDirs,
                    selectedCornerType,
                    selectedCornerDirs,
                    fallbackType,
                    reason,
                    selectedCornerRotationY);
#endif
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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogShoreSandBoundaryCleanup(
            Vector2Int point,
            string beforeType,
            string afterType,
            string reason,
            ShoreEdgeDirection primaryDirection,
            ShoreEdgeDirection secondaryDirection)
        {
            if (!debugShoreSandPlacements)
            {
                return;
            }

            Debug.Log(
                $"[ShoreSand.BoundaryCleanup] point={point} beforeType={beforeType} afterType={afterType} reason={reason} cornerDirs={primaryDirection},{secondaryDirection}",
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
            if (placement.hasSecondaryDirection)
            {
                return $"{typeName}_({point.x},{point.y})_Dirs_{placement.direction}_{placement.secondaryDirection}";
            }

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

        private bool ShouldTraceShoreSandPoint(Vector2Int point)
        {
            return debugShoreSandDecisionTrace && point == debugShoreSandGridPoint;
        }

        private void TraceShoreSandDecision(
            Vector2Int point,
            string stage,
            string oldType,
            string newType,
            string reason,
            float? yaw)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!ShouldTraceShoreSandPoint(point))
            {
                return;
            }

            string yawText = yaw.HasValue ? NormalizeYaw(yaw.Value).ToString("F1") : "N/A";
            Debug.Log(
                $"[ShoreSand Trace]\nPoint: {point}\nStage: {stage}\nOld Type: {oldType}\nNew Type: {newType}\nReason: {reason}\nYaw: {yawText}",
                this);
#endif
        }

        private void TraceShoreSandPlacement(
            Vector2Int point,
            string stage,
            string oldType,
            ShoreSandPlacement placement,
            string reason)
        {
            float yaw = placement.usesExplicitYaw
                ? placement.explicitYaw
                : placement.usesGrassTransitionDirectionMapping
                    ? ResolveGrassTransitionYaw(placement.direction)
                    : ResolveShoreSandYaw(placement.direction);
            TraceShoreSandDecision(
                point,
                stage,
                oldType,
                GetShoreSandPlacementDebugType(placement),
                reason,
                yaw);
        }

        private void TraceFinalShoreSandInstantiation(
            Vector2Int point,
            ShoreSandPlacement placement,
            GameObject prefab,
            float finalYaw,
            GameObject instance)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!ShouldTraceShoreSandPoint(point))
            {
                return;
            }

            Vector3 finalScale = instance != null ? instance.transform.localScale : Vector3.one;
            bool mirrored = finalScale.x < 0f || finalScale.y < 0f || finalScale.z < 0f;
            TraceShoreSandDecision(
                point,
                "InstantiateShoreSandPrefab",
                GetShoreSandPlacementDebugType(placement),
                GetShoreSandPlacementDebugType(placement),
                $"Final prefabField={GetShoreSandPrefabFieldName(prefab)} finalScale={finalScale} mirrored={mirrored}",
                finalYaw);
#endif
        }

        private string GetShoreSandPrefabFieldName(GameObject prefab)
        {
            if (prefab == shoreSandNormalPrefab)
            {
                return nameof(shoreSandNormalPrefab);
            }

            if (prefab == shoreSandOceanTransitionPrefab)
            {
                return nameof(shoreSandOceanTransitionPrefab);
            }

            if (prefab == shoreSandGrassTransitionPrefab)
            {
                return nameof(shoreSandGrassTransitionPrefab);
            }

            if (prefab == shoreSandOceanOuterCornerPrefab)
            {
                return nameof(shoreSandOceanOuterCornerPrefab);
            }

            if (prefab == shoreSandOceanInnerCornerPrefab)
            {
                return nameof(shoreSandOceanInnerCornerPrefab);
            }

            if (prefab == shoreSandGrassOuterCornerPrefab)
            {
                return nameof(shoreSandGrassOuterCornerPrefab);
            }

            if (prefab == shoreSandGrassInnerCornerPrefab)
            {
                return nameof(shoreSandGrassInnerCornerPrefab);
            }

            return prefab != null ? prefab.name : "None";
        }

        private string GetPlacementTypeAtPoint(List<ShoreSandPlacement> placements, Vector2Int point)
        {
            if (placements == null)
            {
                return "None";
            }

            for (int i = 0; i < placements.Count; i++)
            {
                if (placements[i].point == point)
                {
                    return GetShoreSandPlacementDebugType(placements[i]);
                }
            }

            return "None";
        }

        private bool ContainsPlacementPoint(List<ShoreSandPlacement> placements, Vector2Int point)
        {
            if (placements == null)
            {
                return false;
            }

            for (int i = 0; i < placements.Count; i++)
            {
                if (placements[i].point == point)
                {
                    return true;
                }
            }

            return false;
        }

        private string FormatDirectionList(List<ShoreEdgeDirection> directions)
        {
            return directions == null || directions.Count == 0 ? "None" : string.Join(",", directions);
        }

        private string FormatPointCollection(IEnumerable<Vector2Int> points)
        {
            if (points == null)
            {
                return "None";
            }

            List<string> entries = new List<string>();
            foreach (Vector2Int point in points)
            {
                entries.Add(point.ToString());
            }

            return entries.Count == 0 ? "None" : string.Join(",", entries);
        }

        private List<ShoreEdgeDirection> CollectDiagonalSeaDirections(Vector2Int point, HashSet<Vector2Int> allLandPoints)
        {
            List<ShoreEdgeDirection> directions = new List<ShoreEdgeDirection>(4);
            if (currentShoreWaterPoints == null || currentShoreWaterPoints.Count == 0)
            {
                return directions;
            }

            if (currentShoreWaterPoints.Contains(point + Vector2Int.up + Vector2Int.left))
            {
                directions.Add(ShoreEdgeDirection.Left);
            }

            if (currentShoreWaterPoints.Contains(point + Vector2Int.up + Vector2Int.right))
            {
                directions.Add(ShoreEdgeDirection.Right);
            }

            if (currentShoreWaterPoints.Contains(point + Vector2Int.down + Vector2Int.left))
            {
                directions.Add(ShoreEdgeDirection.Down);
            }

            if (currentShoreWaterPoints.Contains(point + Vector2Int.down + Vector2Int.right))
            {
                directions.Add(ShoreEdgeDirection.Up);
            }

            return directions;
        }

        private List<ShoreEdgeDirection> CollectDiagonalOrdinaryGrassDirections(
            Vector2Int point,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> finalShoreSandPoints)
        {
            List<ShoreEdgeDirection> directions = new List<ShoreEdgeDirection>(4);

            if (IsOrdinaryGrassNeighbor(point + Vector2Int.up + Vector2Int.left, allLandPoints, areaByPoint, finalShoreSandPoints))
            {
                directions.Add(ShoreEdgeDirection.Up);
            }

            if (IsOrdinaryGrassNeighbor(point + Vector2Int.up + Vector2Int.right, allLandPoints, areaByPoint, finalShoreSandPoints))
            {
                directions.Add(ShoreEdgeDirection.Right);
            }

            if (IsOrdinaryGrassNeighbor(point + Vector2Int.down + Vector2Int.left, allLandPoints, areaByPoint, finalShoreSandPoints))
            {
                directions.Add(ShoreEdgeDirection.Left);
            }

            if (IsOrdinaryGrassNeighbor(point + Vector2Int.down + Vector2Int.right, allLandPoints, areaByPoint, finalShoreSandPoints))
            {
                directions.Add(ShoreEdgeDirection.Down);
            }

            return directions;
        }

        private void TraceOrdinaryGrassNeighborStates(
            Vector2Int point,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> shorePoints)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!ShouldTraceShoreSandPoint(point))
            {
                return;
            }

            TraceSingleNeighborState("Up", point + Vector2Int.up, allLandPoints, areaByPoint, shorePoints);
            TraceSingleNeighborState("Down", point + Vector2Int.down, allLandPoints, areaByPoint, shorePoints);
            TraceSingleNeighborState("Left", point + Vector2Int.left, allLandPoints, areaByPoint, shorePoints);
            TraceSingleNeighborState("Right", point + Vector2Int.right, allLandPoints, areaByPoint, shorePoints);
#endif
        }

        private void TraceSingleNeighborState(
            string label,
            Vector2Int neighborPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> shorePoints)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool baseGrass = allLandPoints != null &&
                             allLandPoints.Contains(neighborPoint) &&
                             areaByPoint != null &&
                             areaByPoint.TryGetValue(neighborPoint, out AreaType areaType) &&
                             areaType == AreaType.Grass;
            bool occupiedByShore = shorePoints != null && shorePoints.Contains(neighborPoint);
            bool ordinaryGrass = IsOrdinaryGrassNeighbor(neighborPoint, allLandPoints, areaByPoint, shorePoints);
            bool ocean = currentShoreWaterPoints != null && currentShoreWaterPoints.Contains(neighborPoint);

            Debug.Log(
                $"[ShoreSand Trace Neighbor]\nReference Point: {debugShoreSandGridPoint}\nLabel: {label}\nPoint: {neighborPoint}\nBase Grass: {baseGrass}\nOccupied By ShoreSand: {occupiedByShore}\nOrdinary Grass: {ordinaryGrass}\nOcean: {ocean}",
                this);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogCornerDecision(
            ShoreSandPlacement finalPlacement,
            List<ShoreEdgeDirection> realSeaDirs,
            List<ShoreEdgeDirection> realOrdinaryGrassDirs,
            string selectedCornerType,
            List<ShoreEdgeDirection> selectedCornerDirs,
            string fallbackType,
            string reason,
            float? selectedRotationY)
        {
            if (!debugShoreSandPlacements)
            {
                return;
            }

            Vector2Int point = finalPlacement.point;
            string finalType = GetShoreSandPlacementDebugType(finalPlacement);
            string seaDirText = realSeaDirs == null || realSeaDirs.Count == 0 ? "None" : string.Join(",", realSeaDirs);
            string grassDirText = realOrdinaryGrassDirs == null || realOrdinaryGrassDirs.Count == 0 ? "None" : string.Join(",", realOrdinaryGrassDirs);
            string selectedDirText = selectedCornerDirs == null || selectedCornerDirs.Count == 0 ? "None" : string.Join(",", selectedCornerDirs);
            string finalDirectionText = finalPlacement.hasSecondaryDirection
                ? $"{finalPlacement.direction},{finalPlacement.secondaryDirection}"
                : finalPlacement.direction.ToString();
            string rotationText = selectedRotationY.HasValue ? NormalizeYaw(selectedRotationY.Value).ToString("F1") : "None";
            float finalRotationY = finalPlacement.usesExplicitYaw
                ? finalPlacement.explicitYaw
                : finalPlacement.usesGrassTransitionDirectionMapping
                    ? ResolveGrassTransitionYaw(finalPlacement.direction)
                    : ResolveShoreSandYaw(finalPlacement.direction);

            Debug.Log(
                $"[ShoreSand.CornerDecision] point={point} realSeaDirCount={(realSeaDirs == null ? 0 : realSeaDirs.Count)} realSeaDirs={seaDirText} realOrdinaryGrassDirs={grassDirText} selectedCornerType={selectedCornerType} selectedCornerDirs={selectedDirText} rotationY={rotationText} finalType={finalType} finalDirection={finalDirectionText} finalRotationY={NormalizeYaw(finalRotationY):F1} fallbackType={fallbackType} reason={reason}",
                this);
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

        private bool TryResolveOceanInnerCornerFromGeometry(
            Vector2Int point,
            HashSet<Vector2Int> finalShoreSandPoints,
            HashSet<Vector2Int> allLandPoints,
            out ShoreEdgeDirection primaryDirection,
            out ShoreEdgeDirection secondaryDirection,
            out float cornerYaw)
        {
            primaryDirection = ShoreEdgeDirection.Up;
            secondaryDirection = ShoreEdgeDirection.Right;
            cornerYaw = 0f;

            if (finalShoreSandPoints == null ||
                allLandPoints == null ||
                !finalShoreSandPoints.Contains(point))
            {
                return false;
            }

            if (MatchesOceanInnerCornerGeometry(point, ShoreEdgeDirection.Up, ShoreEdgeDirection.Right, finalShoreSandPoints, allLandPoints))
            {
                primaryDirection = ShoreEdgeDirection.Up;
                secondaryDirection = ShoreEdgeDirection.Right;
                cornerYaw = 0f;
                return true;
            }

            if (MatchesOceanInnerCornerGeometry(point, ShoreEdgeDirection.Right, ShoreEdgeDirection.Down, finalShoreSandPoints, allLandPoints))
            {
                primaryDirection = ShoreEdgeDirection.Right;
                secondaryDirection = ShoreEdgeDirection.Down;
                cornerYaw = 90f;
                return true;
            }

            if (MatchesOceanInnerCornerGeometry(point, ShoreEdgeDirection.Down, ShoreEdgeDirection.Left, finalShoreSandPoints, allLandPoints))
            {
                primaryDirection = ShoreEdgeDirection.Down;
                secondaryDirection = ShoreEdgeDirection.Left;
                cornerYaw = 180f;
                return true;
            }

            if (MatchesOceanInnerCornerGeometry(point, ShoreEdgeDirection.Left, ShoreEdgeDirection.Up, finalShoreSandPoints, allLandPoints))
            {
                primaryDirection = ShoreEdgeDirection.Left;
                secondaryDirection = ShoreEdgeDirection.Up;
                cornerYaw = 270f;
                return true;
            }

            return false;
        }

        private bool MatchesOceanInnerCornerGeometry(
            Vector2Int point,
            ShoreEdgeDirection directionA,
            ShoreEdgeDirection directionB,
            HashSet<Vector2Int> finalShoreSandPoints,
            HashSet<Vector2Int> allLandPoints)
        {
            Vector2Int offsetA = GetCardinalOffset(directionA);
            Vector2Int offsetB = GetCardinalOffset(directionB);
            Vector2Int neighborA = point + offsetA;
            Vector2Int neighborB = point + offsetB;
            Vector2Int diagonalPoint = point + offsetA + offsetB;

            if (!finalShoreSandPoints.Contains(neighborA) ||
                !finalShoreSandPoints.Contains(neighborB) ||
                allLandPoints.Contains(diagonalPoint))
            {
                return false;
            }

            return IsSeaAdjacentInDirection(neighborA, directionB, allLandPoints) &&
                   IsSeaAdjacentInDirection(neighborB, directionA, allLandPoints);
        }

        private static bool TryResolveGrassInnerCornerFromGeometry(
            Vector2Int point,
            HashSet<Vector2Int> finalShoreSandPoints,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            out ShoreEdgeDirection primaryDirection,
            out ShoreEdgeDirection secondaryDirection,
            out float cornerYaw)
        {
            primaryDirection = ShoreEdgeDirection.Up;
            secondaryDirection = ShoreEdgeDirection.Right;
            cornerYaw = 0f;

            if (finalShoreSandPoints == null ||
                allLandPoints == null ||
                areaByPoint == null ||
                !finalShoreSandPoints.Contains(point))
            {
                return false;
            }

            if (MatchesGrassInnerCornerGeometry(point, ShoreEdgeDirection.Up, ShoreEdgeDirection.Right, finalShoreSandPoints, allLandPoints, areaByPoint))
            {
                primaryDirection = ShoreEdgeDirection.Up;
                secondaryDirection = ShoreEdgeDirection.Right;
                cornerYaw = 0f;
                return true;
            }

            if (MatchesGrassInnerCornerGeometry(point, ShoreEdgeDirection.Right, ShoreEdgeDirection.Down, finalShoreSandPoints, allLandPoints, areaByPoint))
            {
                primaryDirection = ShoreEdgeDirection.Right;
                secondaryDirection = ShoreEdgeDirection.Down;
                cornerYaw = 90f;
                return true;
            }

            if (MatchesGrassInnerCornerGeometry(point, ShoreEdgeDirection.Down, ShoreEdgeDirection.Left, finalShoreSandPoints, allLandPoints, areaByPoint))
            {
                primaryDirection = ShoreEdgeDirection.Down;
                secondaryDirection = ShoreEdgeDirection.Left;
                cornerYaw = 180f;
                return true;
            }

            if (MatchesGrassInnerCornerGeometry(point, ShoreEdgeDirection.Left, ShoreEdgeDirection.Up, finalShoreSandPoints, allLandPoints, areaByPoint))
            {
                primaryDirection = ShoreEdgeDirection.Left;
                secondaryDirection = ShoreEdgeDirection.Up;
                cornerYaw = 270f;
                return true;
            }

            return false;
        }

        private static bool MatchesGrassInnerCornerGeometry(
            Vector2Int point,
            ShoreEdgeDirection directionA,
            ShoreEdgeDirection directionB,
            HashSet<Vector2Int> finalShoreSandPoints,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint)
        {
            Vector2Int offsetA = GetCardinalOffset(directionA);
            Vector2Int offsetB = GetCardinalOffset(directionB);
            Vector2Int neighborA = point + offsetA;
            Vector2Int neighborB = point + offsetB;
            Vector2Int diagonalPoint = point + offsetA + offsetB;
            Vector2Int oppositeDiagonalPoint = point - offsetA - offsetB;

            if (!finalShoreSandPoints.Contains(neighborA) ||
                !finalShoreSandPoints.Contains(neighborB) ||
                !IsOrdinaryGrassPoint(diagonalPoint, allLandPoints, areaByPoint, finalShoreSandPoints))
            {
                return false;
            }

            CountOrdinaryGrassNeighborDirections(
                point,
                allLandPoints,
                areaByPoint,
                finalShoreSandPoints,
                out List<ShoreEdgeDirection> directGrassDirections,
                out _);

            if (directGrassDirections.Count > 0 ||
                IsOrdinaryGrassPoint(oppositeDiagonalPoint, allLandPoints, areaByPoint, finalShoreSandPoints))
            {
                return false;
            }

            return IsOrdinaryGrassPoint(neighborA + GetCardinalOffset(directionB), allLandPoints, areaByPoint, finalShoreSandPoints) &&
                   IsOrdinaryGrassPoint(neighborB + GetCardinalOffset(directionA), allLandPoints, areaByPoint, finalShoreSandPoints);
        }

        private bool IsSeaAdjacentInDirection(
            Vector2Int point,
            ShoreEdgeDirection direction,
            HashSet<Vector2Int> allLandPoints)
        {
            if (currentShoreWaterPoints == null || currentShoreWaterPoints.Count == 0)
            {
                return false;
            }

            return currentShoreWaterPoints.Contains(point + GetCardinalOffset(direction));
        }

        private static bool IsOrdinaryGrassPoint(
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

        private static bool TryGetAdjacentDirectionPair(
            List<ShoreEdgeDirection> directions,
            out ShoreEdgeDirection primaryDirection,
            out ShoreEdgeDirection secondaryDirection,
            out float cornerYaw)
        {
            primaryDirection = ShoreEdgeDirection.Up;
            secondaryDirection = ShoreEdgeDirection.Up;
            cornerYaw = 0f;

            if (directions == null || directions.Count != 2)
            {
                return false;
            }

            ShoreEdgeDirection first = directions[0];
            ShoreEdgeDirection second = directions[1];
            if (!TryResolveAdjacentCornerYaw(first, second, out cornerYaw))
            {
                return false;
            }

            if (MatchesCornerPair(first, second, ShoreEdgeDirection.Up, ShoreEdgeDirection.Right))
            {
                primaryDirection = ShoreEdgeDirection.Up;
                secondaryDirection = ShoreEdgeDirection.Right;
                return true;
            }

            if (MatchesCornerPair(first, second, ShoreEdgeDirection.Right, ShoreEdgeDirection.Down))
            {
                primaryDirection = ShoreEdgeDirection.Right;
                secondaryDirection = ShoreEdgeDirection.Down;
                return true;
            }

            if (MatchesCornerPair(first, second, ShoreEdgeDirection.Down, ShoreEdgeDirection.Left))
            {
                primaryDirection = ShoreEdgeDirection.Down;
                secondaryDirection = ShoreEdgeDirection.Left;
                return true;
            }

            if (MatchesCornerPair(first, second, ShoreEdgeDirection.Left, ShoreEdgeDirection.Up))
            {
                primaryDirection = ShoreEdgeDirection.Left;
                secondaryDirection = ShoreEdgeDirection.Up;
                return true;
            }

            return false;
        }

        private static bool MatchesCornerPair(
            ShoreEdgeDirection lhs,
            ShoreEdgeDirection rhs,
            ShoreEdgeDirection expectedA,
            ShoreEdgeDirection expectedB)
        {
            return (lhs == expectedA && rhs == expectedB) || (lhs == expectedB && rhs == expectedA);
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

        private bool TryResolveAdjacentTwoGrassPrimaryDirection(
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

        private bool TryGetPreferredSeaEdgeDirection(
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

        private bool TryGetPreferredCoastalDirection(
            Vector2Int point,
            HashSet<Vector2Int> allLandPoints,
            out ShoreEdgeDirection shoreDirection)
        {
            shoreDirection = ShoreEdgeDirection.Up;

            if (TryGetPreferredSeaEdgeDirection(point, allLandPoints, out shoreDirection))
            {
                return true;
            }

            List<ShoreEdgeDirection> diagonalInfluenceDirections = CollectDiagonalSeaInfluenceDirections(point, allLandPoints);
            if (diagonalInfluenceDirections.Count == 0)
            {
                return false;
            }

            shoreDirection = diagonalInfluenceDirections[0];
            return true;
        }

        private List<ShoreEdgeDirection> CollectSeaEdgeDirections(Vector2Int point, HashSet<Vector2Int> allLandPoints)
        {
            List<ShoreEdgeDirection> seaDirections = new List<ShoreEdgeDirection>(4);
            if (currentShoreWaterPoints == null || currentShoreWaterPoints.Count == 0)
            {
                return seaDirections;
            }

            if (currentShoreWaterPoints.Contains(point + Vector2Int.up))
            {
                seaDirections.Add(ShoreEdgeDirection.Up);
            }

            if (currentShoreWaterPoints.Contains(point + Vector2Int.right))
            {
                seaDirections.Add(ShoreEdgeDirection.Right);
            }

            if (currentShoreWaterPoints.Contains(point + Vector2Int.down))
            {
                seaDirections.Add(ShoreEdgeDirection.Down);
            }

            if (currentShoreWaterPoints.Contains(point + Vector2Int.left))
            {
                seaDirections.Add(ShoreEdgeDirection.Left);
            }

            return seaDirections;
        }

        private List<ShoreEdgeDirection> CollectDiagonalSeaInfluenceDirections(
            Vector2Int point,
            HashSet<Vector2Int> allLandPoints)
        {
            List<ShoreEdgeDirection> directions = new List<ShoreEdgeDirection>(4);
            if (currentShoreWaterPoints == null || currentShoreWaterPoints.Count == 0)
            {
                return directions;
            }

            bool oceanUpRight = currentShoreWaterPoints.Contains(point + Vector2Int.up + Vector2Int.right);
            bool oceanRightDown = currentShoreWaterPoints.Contains(point + Vector2Int.right + Vector2Int.down);
            bool oceanDownLeft = currentShoreWaterPoints.Contains(point + Vector2Int.down + Vector2Int.left);
            bool oceanLeftUp = currentShoreWaterPoints.Contains(point + Vector2Int.left + Vector2Int.up);

            if (oceanUpRight || oceanLeftUp)
            {
                directions.Add(ShoreEdgeDirection.Up);
            }

            if (oceanUpRight || oceanRightDown)
            {
                directions.Add(ShoreEdgeDirection.Right);
            }

            if (oceanRightDown || oceanDownLeft)
            {
                directions.Add(ShoreEdgeDirection.Down);
            }

            if (oceanDownLeft || oceanLeftUp)
            {
                directions.Add(ShoreEdgeDirection.Left);
            }

            return directions;
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

        private static Vector2Int GetCardinalOffset(ShoreEdgeDirection direction)
        {
            switch (direction)
            {
                case ShoreEdgeDirection.Up:
                    return Vector2Int.up;
                case ShoreEdgeDirection.Right:
                    return Vector2Int.right;
                case ShoreEdgeDirection.Down:
                    return Vector2Int.down;
                case ShoreEdgeDirection.Left:
                    return Vector2Int.left;
                default:
                    return Vector2Int.zero;
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
