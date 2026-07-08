using Cysharp.Threading.Tasks;
using System;
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
        [SerializeField] private bool enableShoreClassificationDebugLogs = false;
        [SerializeField] private bool enableDirectionalWideBeach = true;
        [SerializeField, Range(0.05f, 0.5f)] private float directionalBeachTargetGrassRatio = 0.30f;
        [SerializeField, Range(0.5f, 1f)] private float directionalMainBeachBudgetRatio = 0.80f;
        [SerializeField, Range(0, 2)] private int directionalSecondaryBeachMaxCount = 2;
        [SerializeField, Min(4)] private int directionalWideBeachMinimumSegmentLength = 10;
        [SerializeField, Min(1)] private int directionalWideBeachMinimumDepth = 5;
        [SerializeField, Min(1)] private int directionalWideBeachMaximumDepth = 14;
        [SerializeField, Min(1)] private int directionalWideBeachFalloffLength = 5;
        [SerializeField, Range(0f, 1f)] private float directionalWideBeachCurvatureTolerance = 0.45f;
        [SerializeField] private bool debugShoreSandDecisionTrace = false;
        [SerializeField] private Vector2Int debugShoreSandGridPoint;
        [System.NonSerialized] private bool randomizeDirectionalWideBeachDirectionCount = false;
        [System.NonSerialized] private int directionalWideBeachDirectionCount = 1;
        [System.NonSerialized] private bool preferAdjacentWideBeachDirections = false;
        [System.NonSerialized] private int directionalWideBeachExtraWidth = 0;
        [System.NonSerialized] private int directionalWideBeachAlongShoreLength = 0;

        [Header("区域大小与范围")]
        [SerializeField] private Vector2Int regionSize;// Legacy serialized data for migration
        [SerializeField] private Vector2Int regionArea;// Base region dimensions (width,height)

        [Header("地图生成开关")]
        [SerializeField] private List<MapRegionGenerateOption> regionGenerateOptions = new List<MapRegionGenerateOption>();
        [System.NonSerialized] private List<MapRegionGenerateOption> runtimeRegionGenerateOptions;
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
        private HashSet<Vector2Int> currentFinalWalkablePoints;
        private BoundsInt? currentShoreLandBounds;
        private Dictionary<Vector2Int, int> currentLocalMaximumDepthByPoint;
        private List<ShoreEdgeDirection> currentDirectionalWideBeachDirections;
        private int currentDirectionalWideBeachBatchId;
        private int currentDirectionalWideBeachCallIndex;
        private int currentEnclosedWaterPointCount;
        private int shoreClassificationDebugBatchCounter;
        private HashSet<Vector2Int> currentBaseShorePoints;
        private Dictionary<Vector2Int, int> currentBaseShoreDepthByPoint;
        private HashSet<Vector2Int> currentMainBeachPoints;
        private HashSet<Vector2Int> currentSecondaryBeachPoints;
        private BeachLayoutDirection currentMainBeachLayoutDirection;
        private List<BeachLayoutDirection> currentSecondaryBeachLayoutDirections;
        private int currentMainBeachPointCount;
        private int currentSecondaryBeachPointCount;
        private int currentSecondaryBeachRegionCount;
        private string currentResolvedMainBeachDirectionLabel;
        private string currentMainBeachFailureReason;
        private bool hasLoggedMissingShoreSandPrefabWarning;
        private ActiveRegionLayout[] activeRegionLayouts;
        private int activeRegionColumns;
        private int activeRegionRows;
        private static int generateMapDebugCallSequence;
        private int currentGenerateMapDebugId;
        private int playerSpawnedGenerationId = -1;
        private bool isGenerateMapDebugInProgress;
        private int shoreGenerationInvocationCount;
        private bool hasCompletedFirstShoreGeneration;
        private readonly HashSet<Vector2Int> diagnosticKnownShorePoints = new HashSet<Vector2Int>();
        private const string GeneratedShoreSandRootName = "Generated Shore Sand";
        private const string GeneratedPropsRootName = "PropsRoot";
        private const string GeneratedWallColliderRootName = "Merged Wall Colliders";

        private enum ShoreEdgeDirection
        {
            Up,
            Down,
            Left,
            Right
        }

        private enum BeachLayoutDirection
        {
            Up,
            Down,
            Left,
            Right,
            UpLeft,
            UpRight,
            DownLeft,
            DownRight
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

        private struct ShoreSandClassificationSnapshot
        {
            public Vector2Int point;
            public int depth;
            public int maxDepth;
            public string prefabType;
            public List<ShoreEdgeDirection> ordinaryGrassDirections;
            public int ordinaryGrassNeighborCount;
            public List<ShoreEdgeDirection> seaDirections;
            public ShoreEdgeDirection direction;
            public bool usesExplicitYaw;
            public float explicitYaw;
            public bool isConnector;
            public bool touchesShoreWater;
        }

        private struct DirectionalWideBeachSegment
        {
            public ShoreEdgeDirection selectedDirection;
            public List<Vector2Int> orderedPoints;
            public HashSet<Vector2Int> pointSet;
            public Vector2Int startPoint;
            public Vector2Int endPoint;
            public Vector2Int centerPoint;
            public int centerIndex;
            public float score;
            public int averageInlandSupport;
            public int nearbyEnclosedWaterCount;
            public float curvatureRatio;
        }

        private struct DirectionalWideBeachBuildResult
        {
            public HashSet<Vector2Int> addedPoints;
            public HashSet<Vector2Int> shorelinePoints;
            public int actualArea;
            public string stoppedReason;
            public int achievedDepth;
        }

        private struct DirectionalWideBeachRejectedSegmentInfo
        {
            public string reason;
            public int length;
            public Vector2Int startPoint;
            public Vector2Int endPoint;
            public float curvatureRatio;
            public int averageInlandSupport;
            public int exteriorOceanContactCount;
            public int branchPointCount;
            public int nearEnclosedWaterCount;
        }

        private struct DirectionalWideBeachCandidateDiagnostics
        {
            public int batchId;
            public string phase;
            public int callIndex;
            public ShoreEdgeDirection selectedDirection;
            public int sourcePointCount;
            public int rejectedDepthNotZero;
            public int rejectedNotOrdinaryShoreCandidate;
            public int rejectedSelectedDirectionPosition;
            public int rejectedLegacyDirectionalFilter;
            public int rejectedExcludedOrUsed;
            public int rejectedMissingAreaEntry;
            public int rejectedMissingImmediateOrdinaryGrassSupport;
            public int rejectedUnhandledBranch;
            public int rawCandidatePointCount;
            public int candidateCountBeforeEnclosedFilter;
            public int connectedComponentCount;
            public int componentCountCardinalOnly;
            public int componentCountWithRestrictedDiagonal;
            public int longestCardinalComponentLength;
            public int longestRestrictedDiagonalComponentLength;
            public int acceptedSegmentCount;
            public int accountedPointCount;
            public int unaccountedPointCount;
            public bool pipelineInvariantValid;
            public int rejectedTooShort;
            public int rejectedBranch;
            public int rejectedClosedLoop;
            public int rejectedPathOrder;
            public int rejectedCurvature;
            public int rejectedInlandSupport;
            public int rejectedDirectCardinalEnclosedWater;
            public int rejectedDiagonalOnlyEnclosedWater;
            public int rejectedExteriorAndEnclosedConflict;
            public int rejectedDirectionalMismatch;
            public int rejectedNoAnyExteriorOceanContact;
            public int rejectedDuplicateOrOverlap;
            public int rejectedUnknown;
            public List<DirectionalWideBeachRejectedSegmentInfo> topRejectedSegments;
            public List<string> unaccountedPointSamples;

            public int TotalRejectedCount =>
                rejectedTooShort +
                rejectedBranch +
                rejectedClosedLoop +
                rejectedPathOrder +
                rejectedCurvature +
                rejectedInlandSupport +
                rejectedDepthNotZero +
                rejectedNotOrdinaryShoreCandidate +
                rejectedDirectCardinalEnclosedWater +
                rejectedDiagonalOnlyEnclosedWater +
                rejectedExteriorAndEnclosedConflict +
                rejectedDirectionalMismatch +
                rejectedNoAnyExteriorOceanContact +
                rejectedDuplicateOrOverlap +
                rejectedSelectedDirectionPosition +
                rejectedLegacyDirectionalFilter +
                rejectedExcludedOrUsed +
                rejectedMissingAreaEntry +
                rejectedMissingImmediateOrdinaryGrassSupport +
                rejectedUnhandledBranch +
                rejectedUnknown;
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

        private struct ShoreGenerationCompleteStats
        {
            public int totalCandidateCount;
            public int totalGeneratedCount;
            public int generatedShoreSandPointsCount;
            public int instantiatedObjectCount;
            public string generationStartTime;
            public string generationEndTime;
            public long elapsedMilliseconds;
            public bool completedBeforePlayerSpawn;
            public bool completedBeforeEnemySpawn;
        }

        private struct PlayerPositionDiagnostic
        {
            public Vector3 worldPosition;
            public Vector2Int gridPoint;
            public bool hasPlayer;
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
            if (isGenerateMapDebugInProgress)
            {
                Debug.LogWarning(
                    $"[RandomMap.GenerateCall] generationId={currentGenerateMapDebugId} isPlaying={Application.isPlaying} " +
                    "accepted=False skipped reason=already-generating",
                    this);
                return;
            }

            currentGenerateMapDebugId = ++generateMapDebugCallSequence;
            int generationId = currentGenerateMapDebugId;
            Debug.Log(
                $"[RandomMap.GenerateCall] generationId={generationId} isPlaying={Application.isPlaying} " +
                $"accepted=True frameCount={Time.frameCount} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}",
                this);

            isGenerateMapDebugInProgress = true;

            try
            {
                ClearGeneratedMap();

                if (!IsGenerationStillCurrent(generationId))
                {
                    Debug.LogWarning($"[RandomMap.GenerateCall] generationId={generationId} aborted reason=stale-after-clear", this);
                    return;
                }

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
                await GeneraterWallPointsAsync(checkAllFloor);

                if (!IsGenerationStillCurrent(generationId))
                {
                    Debug.LogWarning($"[RandomMap.GenerateCall] generationId={generationId} aborted reason=stale-after-wall-points", this);
                    return;
                }

                List<UniTask> paintTasks = new List<UniTask>(activeRegionLayouts.Length);
                for (int i = 0; i < activeRegionLayouts.Length; i++)
                {
                    paintTasks.Add(PaintActiveRegionTilemap(activeRegionLayouts[i]));
                }

                await UniTask.WhenAll(paintTasks);

                if (!IsGenerationStillCurrent(generationId))
                {
                    Debug.LogWarning($"[RandomMap.GenerateCall] generationId={generationId} aborted reason=stale-after-paint", this);
                    return;
                }

                await GenerateShoreSandAsync(generationId);

                if (!IsGenerationStillCurrent(generationId))
                {
                    Debug.LogWarning($"[RandomMap.GenerateCall] generationId={generationId} aborted reason=stale-after-shore", this);
                    return;
                }

                RebuildFinalWaterAndWallState();
                SpawnPropsOnFloor();
                await PanintWallTilemap();

                if (Application.isPlaying)
                {
                    bool alreadySpawned = playerSpawnedGenerationId == generationId;
                    Debug.Log(
                        $"[PlayerSpawn] generationId={generationId} isPlaying={Application.isPlaying} " +
                        $"alreadySpawnedThisGeneration={alreadySpawned}",
                        this);

                    if (!alreadySpawned)
                    {
                        playerSpawnedGenerationId = generationId;
                        PlacePlayerOnMap();
                    }
                }
                else
                {
                    Debug.Log(
                        $"[PlayerSpawn] generationId={generationId} isPlaying={Application.isPlaying} " +
                        "skipped=True reason=not-playing alreadySpawnedThisGeneration=False",
                        this);
                }
            }
            finally
            {
                isGenerateMapDebugInProgress = false;
            }
        }

        private UniTask PanintWallTilemap()
        {
            return paintTilemap.PaintWallTile(
                wallColliderPoints,
                currentFinalWalkablePoints,
                generatedShoreSandPoints);
        }

        private async UniTask GeneraterWallPointsAsync(HashSet<Vector2Int> checkAllFloor)
        {
            int beforeCount = wallColliderPoints != null ? wallColliderPoints.Count : 0;
            wallColliderPoints = new HashSet<Vector2Int>();
            wallColliderPoints = RandomMapGenerationAlgorithms.GenraterWallPoints(checkAllFloor);
            LogMapDataMutation(nameof(wallColliderPoints), beforeCount, wallColliderPoints != null ? wallColliderPoints.Count : 0, nameof(GeneraterWallPointsAsync));
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
            if (!Application.isPlaying)
            {
                Debug.Log(
                    $"[PlayerSpawn] generationId={currentGenerateMapDebugId} isPlaying={Application.isPlaying} " +
                    $"skipped=True reason=not-playing alreadySpawnedThisGeneration={playerSpawnedGenerationId == currentGenerateMapDebugId}",
                    this);
                return;
            }

            if (floorPoints == null)
            {
                Debug.LogWarning(
                    $"[PlayerSpawn] generationId={currentGenerateMapDebugId} isPlaying={Application.isPlaying} " +
                    "skipped=True reason=no-floor-points",
                    this);
                return;
            }

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

            PlayerMovement targetMovement = spawnTarget.GetComponent<PlayerMovement>();

            Rigidbody targetRb = spawnTarget.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                Vector3 velocityBeforeWrite = targetRb.linearVelocity;
                Vector3 velocityAfterWrite = Vector3.zero;
                targetRb.linearVelocity = velocityAfterWrite;
                PlayerMovement.LogVelocityWrite(
                    targetMovement != null ? targetMovement : spawnTarget.GetComponent<PlayerMovement>(),
                    nameof(RandomMapGeneration),
                    nameof(PlacePlayerOnMap),
                    targetRb,
                    velocityBeforeWrite,
                    velocityAfterWrite,
                    "place-player-on-map-root-rigidbody-reset",
                    "none",
                    "none",
                    "map-spawn");
            }

            if (targetMovement != null && targetMovement.rb != null)
            {
                Vector3 velocityBeforeWrite = targetMovement.rb.linearVelocity;
                Vector3 velocityAfterWrite = Vector3.zero;
                targetMovement.rb.linearVelocity = velocityAfterWrite;
                PlayerMovement.LogVelocityWrite(
                    targetMovement,
                    nameof(RandomMapGeneration),
                    nameof(PlacePlayerOnMap),
                    targetMovement.rb,
                    velocityBeforeWrite,
                    velocityAfterWrite,
                    "place-player-on-map-player-movement-rigidbody-reset",
                    "none",
                    "none",
                    "map-spawn");
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

        public int GetCurrentGenerateMapDebugId()
        {
            return currentGenerateMapDebugId;
        }

        public bool TryGetMovementDebugInfo(
            Vector3 worldPosition,
            out Vector2Int gridPoint,
            out AreaType areaType,
            out bool inFloorPoints,
            out bool inGeneratedShoreSandPoints,
            out bool inFinalWalkablePoints)
        {
            gridPoint = Vector2Int.zero;
            areaType = AreaType.NoSpawn;
            inFloorPoints = false;
            inGeneratedShoreSandPoints = false;
            inFinalWalkablePoints = false;

            Tilemap refTilemap = paintTilemap != null ? paintTilemap.GetFloorTilemap(ResolveReferencePaintSlotIndex()) : null;
            if (refTilemap == null)
            {
                return false;
            }

            Vector3Int cell = refTilemap.WorldToCell(worldPosition);
            gridPoint = new Vector2Int(cell.x, cell.y);

            Dictionary<Vector2Int, AreaType> areaByPoint = BuildPointAreaTypes();
            if (areaByPoint.TryGetValue(gridPoint, out AreaType resolvedAreaType))
            {
                areaType = resolvedAreaType;
            }

            if (floorPoints != null)
            {
                for (int x = 0; x < floorPoints.GetLength(0); x++)
                {
                    for (int y = 0; y < floorPoints.GetLength(1); y++)
                    {
                        HashSet<Vector2Int> regionPoints = floorPoints[x, y];
                        if (regionPoints != null && regionPoints.Contains(gridPoint))
                        {
                            inFloorPoints = true;
                            x = floorPoints.GetLength(0);
                            break;
                        }
                    }
                }
            }

            inGeneratedShoreSandPoints = generatedShoreSandPoints != null && generatedShoreSandPoints.Contains(gridPoint);
            inFinalWalkablePoints = currentFinalWalkablePoints != null && currentFinalWalkablePoints.Contains(gridPoint);
            return true;
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
            int previousFloorCount = GetTotalPointCount(floorPoints);
            int previousPropsCount = GetTotalPointCount(propsPoints);
            floorPoints = new HashSet<Vector2Int>[activeRegionColumns, activeRegionRows];
            propsPoints = new HashSet<Vector2Int>[activeRegionColumns, activeRegionRows];
            LogMapDataMutation(nameof(floorPoints), previousFloorCount, GetTotalPointCount(floorPoints), nameof(GeneraterFloorPoints));
            LogMapDataMutation(nameof(propsPoints), previousPropsCount, GetTotalPointCount(propsPoints), nameof(GeneraterFloorPoints));

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

                    int previousRegionFloorCount = floorPoints[i, j].Count;
                    floorPoints[i, j] = RandomMapGenerationAlgorithms.GenraterFloorPoints(regionPoints[i, j], checkFloor, maplterations, mapSize);
                    LogMapDataMutation($"{nameof(floorPoints)}[{i},{j}]", previousRegionFloorCount, floorPoints[i, j] != null ? floorPoints[i, j].Count : 0, nameof(GeneraterFloorPoints));
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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogGrassSideSpurDecision(
            string action,
            Vector2Int tipPoint,
            Vector2Int basePoint,
            Vector2Int axisOffset,
            HashSet<Vector2Int> branchPoints,
            string reason)
        {
            if (!debugShoreSandPlacements)
            {
                return;
            }

            string branchPointText = branchPoints == null || branchPoints.Count == 0
                ? "None"
                : FormatPointCollection(branchPoints);

            Debug.Log(
                $"[ShoreSand.GrassSideSpur] action={action} tip={tipPoint} base={basePoint} axis={axisOffset} branchLength={(branchPoints == null ? 0 : branchPoints.Count)} points={branchPointText} reason={reason}",
                this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogTargetedFixDecision(
            Vector2Int point,
            int depth,
            int maxDepth,
            string action,
            string reason)
        {
            if (!debugShoreSandPlacements)
            {
                return;
            }

            Debug.Log(
                $"[ShoreSand.TargetedFix] point={point} depth={depth} maxDepth={maxDepth} action={action} reason={reason}",
                this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogTargetedFixAction(
            Vector2Int point,
            string action,
            string reason)
        {
            if (!debugShoreSandPlacements)
            {
                return;
            }

            Debug.Log(
                $"[ShoreSand.TargetedFix] point={point} action={action} reason={reason}",
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
            int beforeFloorCount = GetTotalPointCount(floorPoints);
            foreach (Vector2Int point in pointsToFill)
            {
                if (TryResolveOwningRegionForFilledPoint(point, out int ownerGridX, out int ownerGridY))
                {
                    floorPoints[ownerGridX, ownerGridY].Add(point);
                    propsPoints[ownerGridX, ownerGridY]?.Add(point);
                }
            }

            LogMapDataMutation(nameof(floorPoints), beforeFloorCount, GetTotalPointCount(floorPoints), nameof(ApplyFloorPointFills));
        }

        private void ApplyFloorPointRemovals(HashSet<Vector2Int> pointsToRemove)
        {
            if (pointsToRemove.Count == 0)
            {
                return;
            }

            int beforeFloorCount = GetTotalPointCount(floorPoints);
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

            LogMapDataMutation(nameof(floorPoints), beforeFloorCount, GetTotalPointCount(floorPoints), nameof(ApplyFloorPointRemovals));
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
            LogMapDataMutation(nameof(floorPoints), GetTotalPointCount(floorPoints), 0, nameof(InitMapData));
            floorPoints = null;
            LogMapDataMutation(nameof(propsPoints), GetTotalPointCount(propsPoints), 0, nameof(InitMapData));
            propsPoints = null;
            LogMapDataMutation(nameof(wallColliderPoints), wallColliderPoints != null ? wallColliderPoints.Count : 0, 0, nameof(InitMapData));
            wallColliderPoints = null;
            LogMapDataMutation(nameof(generatedShoreSandPoints), generatedShoreSandPoints != null ? generatedShoreSandPoints.Count : 0, 0, nameof(InitMapData));
            generatedShoreSandPoints = null;
            LogMapDataMutation(nameof(connectorFloorPoints), connectorFloorPoints != null ? connectorFloorPoints.Count : 0, 0, nameof(InitMapData));
            connectorFloorPoints = new HashSet<Vector2Int>();
            LogMapDataMutation(nameof(currentExteriorOceanPoints), currentExteriorOceanPoints != null ? currentExteriorOceanPoints.Count : 0, 0, nameof(InitMapData));
            currentExteriorOceanPoints = null;
            LogMapDataMutation(nameof(currentShoreWaterPoints), currentShoreWaterPoints != null ? currentShoreWaterPoints.Count : 0, 0, nameof(InitMapData));
            currentShoreWaterPoints = null;
            LogMapDataMutation(nameof(currentFinalWalkablePoints), currentFinalWalkablePoints != null ? currentFinalWalkablePoints.Count : 0, 0, nameof(InitMapData));
            currentFinalWalkablePoints = null;
            currentShoreLandBounds = null;
            currentLocalMaximumDepthByPoint = null;
            currentDirectionalWideBeachDirections = null;
            currentDirectionalWideBeachBatchId = 0;
            currentDirectionalWideBeachCallIndex = 0;
            currentEnclosedWaterPointCount = 0;
            currentBaseShorePoints = null;
            currentBaseShoreDepthByPoint = null;
            currentMainBeachPoints = null;
            currentSecondaryBeachPoints = null;
            currentMainBeachLayoutDirection = BeachLayoutDirection.Up;
            currentSecondaryBeachLayoutDirections = null;
            currentMainBeachPointCount = 0;
            currentSecondaryBeachPointCount = 0;
            currentSecondaryBeachRegionCount = 0;
            currentResolvedMainBeachDirectionLabel = "None";
            currentMainBeachFailureReason = "not-generated";
            shoreClassificationDebugBatchCounter = 0;
            activeRegionLayouts = null;
            activeRegionColumns = 0;
            activeRegionRows = 0;
            player = null;
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
            if (Application.isPlaying)
            {
                EnsureRuntimeRegionGenerateOptions();
                return;
            }

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

        private void EnsureRuntimeRegionGenerateOptions()
        {
            if (runtimeRegionGenerateOptions != null && runtimeRegionGenerateOptions.Count > 0)
            {
                return;
            }

            runtimeRegionGenerateOptions = new List<MapRegionGenerateOption>();

            if (regionGenerateOptions != null && regionGenerateOptions.Count > 0)
            {
                for (int i = 0; i < regionGenerateOptions.Count; i++)
                {
                    runtimeRegionGenerateOptions.Add(CloneRegionGenerateOption(regionGenerateOptions[i], i));
                }

                return;
            }

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
                runtimeRegionGenerateOptions.Add(new MapRegionGenerateOption
                {
                    displayName = $"Region {i}",
                    generateThisRegion = enabledByDefault,
                    paintSlotIndex = i,
                    areaType = areaType,
                    sizeMultiplier = defaultRegionSizeMultiplier
                });
            }
        }

        private static MapRegionGenerateOption CloneRegionGenerateOption(MapRegionGenerateOption source, int fallbackIndex)
        {
            if (source == null)
            {
                return new MapRegionGenerateOption
                {
                    displayName = $"Region {fallbackIndex}",
                    generateThisRegion = false,
                    paintSlotIndex = fallbackIndex,
                    areaType = AreaType.NoSpawn,
                    sizeMultiplier = Vector2.one
                };
            }

            return new MapRegionGenerateOption
            {
                displayName = source.displayName,
                generateThisRegion = source.generateThisRegion,
                paintSlotIndex = source.paintSlotIndex,
                areaType = source.areaType,
                sizeMultiplier = source.sizeMultiplier
            };
        }

        private ActiveRegionLayout[] BuildActiveRegionLayouts()
        {
            if (Application.isPlaying)
            {
                runtimeRegionGenerateOptions = null;
            }

            EnsureRegionGenerateOptions();

            List<MapRegionGenerateOption> regionOptions = Application.isPlaying
                ? (runtimeRegionGenerateOptions ?? new List<MapRegionGenerateOption>())
                : regionGenerateOptions;
            List<MapRegionGenerateOption> enabledOptions = new List<MapRegionGenerateOption>();
            for (int i = 0; i < regionOptions.Count; i++)
            {
                MapRegionGenerateOption option = regionOptions[i];
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

        private PlayerPositionDiagnostic GetCurrentPlayerPositionDiagnostic()
        {
            PlayerPositionDiagnostic result = new PlayerPositionDiagnostic
            {
                worldPosition = Vector3.zero,
                gridPoint = Vector2Int.zero,
                hasPlayer = false
            };

            Transform playerTransform = null;
            Player2Bootstrap bootstrap = FindObjectOfType<Player2Bootstrap>();
            if (bootstrap != null)
            {
                playerTransform = bootstrap.CurrentPlayerTransform != null
                    ? bootstrap.CurrentPlayerTransform
                    : (bootstrap.PartyLeader != null ? bootstrap.PartyLeader.transform : null);
            }

            if (playerTransform == null && player != null)
            {
                playerTransform = player.transform;
            }

            if (playerTransform == null)
            {
                return result;
            }

            result.hasPlayer = true;
            result.worldPosition = playerTransform.position;

            Tilemap referenceTilemap = paintTilemap != null ? paintTilemap.GetFloorTilemap(ResolveReferencePaintSlotIndex()) : null;
            if (referenceTilemap != null)
            {
                Vector3Int cell = referenceTilemap.WorldToCell(playerTransform.position);
                result.gridPoint = new Vector2Int(cell.x, cell.y);
            }

            return result;
        }

        private int CountCurrentGrassPoints()
        {
            if (floorPoints == null)
            {
                return 0;
            }

            int count = 0;
            for (int x = 0; x < floorPoints.GetLength(0); x++)
            {
                for (int y = 0; y < floorPoints.GetLength(1); y++)
                {
                    HashSet<Vector2Int> regionPointSet = floorPoints[x, y];
                    if (regionPointSet == null || !IsBaseLandAreaType(ResolveRegionAreaType(x, y)))
                    {
                        continue;
                    }

                    foreach (Vector2Int point in regionPointSet)
                    {
                        if (generatedShoreSandPoints == null || !generatedShoreSandPoints.Contains(point))
                        {
                            count++;
                        }
                    }
                }
            }

            return count;
        }

        private int CountCurrentWaterPoints()
        {
            HashSet<Vector2Int> combined = new HashSet<Vector2Int>();
            if (currentExteriorOceanPoints != null)
            {
                combined.UnionWith(currentExteriorOceanPoints);
            }

            if (currentShoreWaterPoints != null)
            {
                combined.UnionWith(currentShoreWaterPoints);
            }

            return combined.Count;
        }

        private static int GetTotalPointCount(HashSet<Vector2Int>[,] pointGrid)
        {
            if (pointGrid == null)
            {
                return 0;
            }

            int count = 0;
            for (int x = 0; x < pointGrid.GetLength(0); x++)
            {
                for (int y = 0; y < pointGrid.GetLength(1); y++)
                {
                    count += pointGrid[x, y] != null ? pointGrid[x, y].Count : 0;
                }
            }

            return count;
        }

        private string ResolvePointClassificationLabel(Vector2Int point, Dictionary<Vector2Int, AreaType> areaByPoint)
        {
            if (generatedShoreSandPoints != null && generatedShoreSandPoints.Contains(point))
            {
                return AreaType.Beach.ToString();
            }

            if (areaByPoint != null && areaByPoint.TryGetValue(point, out AreaType areaType))
            {
                return areaType.ToString();
            }

            if (currentShoreWaterPoints != null && currentShoreWaterPoints.Contains(point))
            {
                return AreaType.Water.ToString();
            }

            if (currentExteriorOceanPoints != null && currentExteriorOceanPoints.Contains(point))
            {
                return "Water";
            }

            return "Other";
        }

        private void LogMapDataMutation(string collectionName, int beforeCount, int afterCount, string currentMethod)
        {
            Debug.Log(
                $"[RandomMap.MapDataMutation] collectionName={collectionName} beforeCount={beforeCount} afterCount={afterCount} " +
                $"currentMethod={currentMethod} frameCount={Time.frameCount} hasCompletedFirstShoreGeneration={hasCompletedFirstShoreGeneration} " +
                $"stackTrace={Environment.StackTrace}",
                this);
        }

        private void LogShoreGenerationBegin(int generationId, int invocationIndex)
        {
            PlayerPositionDiagnostic playerDiagnostic = GetCurrentPlayerPositionDiagnostic();
            Debug.Log(
                $"[RandomMap.ShoreGenerationBegin] frameCount={Time.frameCount} generationId={generationId} callCount={invocationIndex} " +
                $"playerWorldPosition={(playerDiagnostic.hasPlayer ? playerDiagnostic.worldPosition.ToString() : "unavailable")} " +
                $"playerGridPoint={(playerDiagnostic.hasPlayer ? playerDiagnostic.gridPoint.ToString() : "unavailable")} " +
                $"currentGrassCount={CountCurrentGrassPoints()} currentWaterCount={CountCurrentWaterPoints()} " +
                $"currentShoreCount={(generatedShoreSandPoints != null ? generatedShoreSandPoints.Count : 0)} " +
                $"stackTrace={Environment.StackTrace}",
                this);
        }

        private void LogGrassChangedToShore(
            Vector2Int point,
            string beforeClassification,
            string afterClassification,
            string methodName)
        {
            PlayerPositionDiagnostic playerDiagnostic = GetCurrentPlayerPositionDiagnostic();
            Debug.Log(
                $"[RandomMap.GrassChangedToShore] gridPoint={point} beforeClassification={beforeClassification} afterClassification={afterClassification} " +
                $"methodName={methodName} frameCount={Time.frameCount} playerGridPoint={(playerDiagnostic.hasPlayer ? playerDiagnostic.gridPoint.ToString() : "unavailable")} " +
                $"stackTrace={Environment.StackTrace}",
                this);
        }

        private void LogLateShoreCreated(
            int invocationIndex,
            Vector2Int point,
            Vector3 worldPosition,
            string previousClassification)
        {
            PlayerPositionDiagnostic playerDiagnostic = GetCurrentPlayerPositionDiagnostic();
            float distanceInTiles = playerDiagnostic.hasPlayer
                ? Vector2.Distance((Vector2)playerDiagnostic.gridPoint, (Vector2)point)
                : -1f;

            Debug.Log(
                $"[RandomMap.LateShoreCreated] frameCount={Time.frameCount} gridPoint={point} worldPosition={worldPosition} " +
                $"previousClassification={previousClassification} distanceFromPlayerInTiles={distanceInTiles:F2} " +
                $"shoreGenerationCallIndex={invocationIndex} stackTrace={Environment.StackTrace}",
                this);
        }

        private UniTask GenerateShoreSandAsync(int generationId)
        {
            DateTimeOffset generationStartTimeUtc = DateTimeOffset.UtcNow;
            double generationStartRealtime = Time.realtimeSinceStartupAsDouble;
            int currentShoreGenerationInvocationIndex = ++shoreGenerationInvocationCount;
            ShoreGenerationCompleteStats completionStats = new ShoreGenerationCompleteStats
            {
                generationStartTime = generationStartTimeUtc.ToString("O"),
                completedBeforePlayerSpawn = true,
                completedBeforeEnemySpawn = true
            };

            LogShoreGenerationBegin(generationId, currentShoreGenerationInvocationIndex);

            UniTask CompleteAndLog()
            {
                DateTimeOffset generationEndTimeUtc = DateTimeOffset.UtcNow;
                completionStats.generationEndTime = generationEndTimeUtc.ToString("O");
                completionStats.elapsedMilliseconds = Math.Max(
                    0L,
                    (long)Math.Round((Time.realtimeSinceStartupAsDouble - generationStartRealtime) * 1000d));

                Debug.Log(
                    $"[RandomMap.ShoreGenerationComplete] generationId={generationId} " +
                    $"totalCandidateCount={completionStats.totalCandidateCount} totalGeneratedCount={completionStats.totalGeneratedCount} " +
                    $"generatedShoreSandPointsCount={completionStats.generatedShoreSandPointsCount} instantiatedObjectCount={completionStats.instantiatedObjectCount} " +
                    $"generationStartTime={completionStats.generationStartTime} generationEndTime={completionStats.generationEndTime} " +
                    $"elapsedMilliseconds={completionStats.elapsedMilliseconds} completedBeforePlayerSpawn={completionStats.completedBeforePlayerSpawn} " +
                    $"completedBeforeEnemySpawn={completionStats.completedBeforeEnemySpawn}",
                    this);

                if (generatedShoreSandPoints != null)
                {
                    diagnosticKnownShorePoints.UnionWith(generatedShoreSandPoints);
                }

                hasCompletedFirstShoreGeneration = true;

                return UniTask.CompletedTask;
            }

            LogMapDataMutation(nameof(generatedShoreSandPoints), generatedShoreSandPoints != null ? generatedShoreSandPoints.Count : 0, 0, nameof(GenerateShoreSandAsync));
            generatedShoreSandPoints = null;
            ClearGeneratedShoreSandInstances();
            LogMapDataMutation(nameof(currentExteriorOceanPoints), currentExteriorOceanPoints != null ? currentExteriorOceanPoints.Count : 0, 0, nameof(GenerateShoreSandAsync));
            currentExteriorOceanPoints = null;
            LogMapDataMutation(nameof(currentShoreWaterPoints), currentShoreWaterPoints != null ? currentShoreWaterPoints.Count : 0, 0, nameof(GenerateShoreSandAsync));
            currentShoreWaterPoints = null;
            LogMapDataMutation(nameof(currentFinalWalkablePoints), currentFinalWalkablePoints != null ? currentFinalWalkablePoints.Count : 0, 0, nameof(GenerateShoreSandAsync));
            currentFinalWalkablePoints = null;
            currentShoreLandBounds = null;
            currentLocalMaximumDepthByPoint = null;
            currentDirectionalWideBeachDirections = null;
            currentDirectionalWideBeachBatchId = ++shoreClassificationDebugBatchCounter;
            currentDirectionalWideBeachCallIndex = 0;
            currentEnclosedWaterPointCount = 0;
            currentBaseShorePoints = null;
            currentBaseShoreDepthByPoint = null;
            currentMainBeachPoints = null;
            currentSecondaryBeachPoints = null;
            currentMainBeachLayoutDirection = BeachLayoutDirection.Up;
            currentSecondaryBeachLayoutDirections = null;
            currentMainBeachPointCount = 0;
            currentSecondaryBeachPointCount = 0;
            currentSecondaryBeachRegionCount = 0;
            currentResolvedMainBeachDirectionLabel = "None";
            currentMainBeachFailureReason = "not-generated";

            if (!enableShoreSand)
            {
                return CompleteAndLog();
            }

            if (!HasAllShoreSandPrefabsAssigned())
            {
                if (!hasLoggedMissingShoreSandPrefabWarning)
                {
                    Debug.LogWarning("[RandomMapGeneration] Shore Sand is enabled, but one or more Shore Sand prefabs are not assigned. Assign Normal, Ocean Transition, and Grass Transition prefabs in the inspector. Shore sand generation will be skipped.", this);
                    hasLoggedMissingShoreSandPrefabWarning = true;
                }

                return CompleteAndLog();
            }

            if (paintTilemap == null || floorPoints == null)
            {
                return CompleteAndLog();
            }

            Tilemap referenceTilemap = paintTilemap.GetFloorTilemap(ResolveReferencePaintSlotIndex());
            if (referenceTilemap == null)
            {
                return CompleteAndLog();
            }

            Dictionary<Vector2Int, AreaType> areaByPoint = new Dictionary<Vector2Int, AreaType>();
            Dictionary<Vector2Int, int> tilemapIndexByPoint = new Dictionary<Vector2Int, int>();
            HashSet<Vector2Int> allLandPoints = CollectLandPointMetadata(areaByPoint, tilemapIndexByPoint);
            if (allLandPoints.Count == 0)
            {
                return CompleteAndLog();
            }

            currentShoreLandBounds = CalculatePointBounds(allLandPoints);
            currentExteriorOceanPoints = CollectExteriorOceanPoints(allLandPoints);
            if (currentShoreWaterPoints == null || currentShoreWaterPoints.Count == 0)
            {
                return CompleteAndLog();
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
            completionStats.totalCandidateCount = shoreDepthByPoint.Count;
            if (shoreDepthByPoint.Count == 0)
            {
                return CompleteAndLog();
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (debugShoreSandPlacements)
            {
                Debug.Log(
                    $"[ShoreSand.DepthMap] coastalSeedCount={coastalSeedCount} exteriorCoastalSeedCount={exteriorCoastalSeedCount} enclosedWaterCoastalSeedCount={enclosedWaterCoastalSeedCount} shoreDepthPointCount={shoreDepthByPoint.Count} maxDepth={Mathf.Max(0, shoreSandWidth - 1)}",
                    this);
            }
#endif

            if (enableShorelineMicroCleanup)
            {
                RemoveShortSingleWidthGrassSideShoreSpurs(
                    shoreDepthByPoint,
                    allLandPoints,
                    areaByPoint,
                    Mathf.Max(0, shoreSandWidth - 1),
                    null);
            }

            currentBaseShorePoints = new HashSet<Vector2Int>(shoreDepthByPoint.Keys);
            currentBaseShoreDepthByPoint = new Dictionary<Vector2Int, int>(shoreDepthByPoint);

            currentLocalMaximumDepthByPoint = BuildLocalMaximumDepthByPoint(
                allLandPoints,
                areaByPoint,
                shoreDepthByPoint,
                out _);

            List<ShoreSandPlacement> placements = BuildShoreSandPlacementsFromDepthMap(
                shoreDepthByPoint,
                allLandPoints,
                areaByPoint);
            completionStats.totalGeneratedCount = placements.Count;

            if (placements.Count == 0)
            {
                return CompleteAndLog();
            }

            int shoreClassificationDebugBatchId = 0;
            Dictionary<Vector2Int, ShoreSandClassificationSnapshot> previousClassificationSnapshots = null;
            if (enableShoreClassificationDebugLogs)
            {
                shoreClassificationDebugBatchId = ++shoreClassificationDebugBatchCounter;
                previousClassificationSnapshots = LogSuspiciousShoreClassificationPlacements(
                    shoreClassificationDebugBatchId,
                    "BuildShoreSandPlacementsFromDepthMap",
                    placements,
                    shoreDepthByPoint,
                    allLandPoints,
                    areaByPoint,
                    null);
            }

            ApplyShortGrassBoundarySegmentFix(placements, allLandPoints, areaByPoint);
            if (enableShoreClassificationDebugLogs)
            {
                previousClassificationSnapshots = LogSuspiciousShoreClassificationPlacements(
                    shoreClassificationDebugBatchId,
                    "ApplyShortGrassBoundarySegmentFix",
                    placements,
                    shoreDepthByPoint,
                    allLandPoints,
                    areaByPoint,
                    previousClassificationSnapshots);
            }

            ApplyFinalGrassBoundaryCorrection(placements, allLandPoints, areaByPoint);
            if (enableShoreClassificationDebugLogs)
            {
                previousClassificationSnapshots = LogSuspiciousShoreClassificationPlacements(
                    shoreClassificationDebugBatchId,
                    "ApplyFinalGrassBoundaryCorrection",
                    placements,
                    shoreDepthByPoint,
                    allLandPoints,
                    areaByPoint,
                    previousClassificationSnapshots);
            }

            ApplyFinalCornerResolution(placements, allLandPoints, areaByPoint);
            if (enableShoreClassificationDebugLogs)
            {
                previousClassificationSnapshots = LogSuspiciousShoreClassificationPlacements(
                    shoreClassificationDebugBatchId,
                    "ApplyFinalCornerResolution",
                    placements,
                    shoreDepthByPoint,
                    allLandPoints,
                    areaByPoint,
                    previousClassificationSnapshots);
            }

            RestrictPlacementsToAllowedBeachScope(placements);

            EnsureBaseShorePointsIncludedInPlacements(
                placements,
                shoreDepthByPoint,
                allLandPoints,
                areaByPoint,
                Mathf.Max(0, shoreSandWidth - 1));

            HashSet<Vector2Int> finalShorePlacementPoints = BuildPlacementPointSet(placements);
            HashSet<Vector2Int> allowedBeachPoints = BuildAllowedBeachPointSet();
            int ordinaryExpandedPointCount = 0;
            foreach (Vector2Int point in finalShorePlacementPoints)
            {
                if (!allowedBeachPoints.Contains(point))
                {
                    ordinaryExpandedPointCount++;
                }
            }
            string secondaryDirections = currentSecondaryBeachLayoutDirections != null && currentSecondaryBeachLayoutDirections.Count > 0
                ? string.Join(",", currentSecondaryBeachLayoutDirections)
                : "None";
            Debug.Log(
                $"[RandomMap.BeachLayout] generationId={currentGenerateMapDebugId} mainDirection={currentResolvedMainBeachDirectionLabel} " +
                $"mainBeachFailureReason={currentMainBeachFailureReason} baseShoreCount={(currentBaseShorePoints != null ? currentBaseShorePoints.Count : 0)} " +
                $"ordinaryExpandedPointCount={ordinaryExpandedPointCount} mainBeachPointCount={currentMainBeachPointCount} secondaryBeachRegionCount={currentSecondaryBeachRegionCount} " +
                $"secondaryBeachPointCount={currentSecondaryBeachPointCount} secondaryDirections={secondaryDirections} " +
                $"finalShorePointCount={finalShorePlacementPoints.Count}",
                this);

            Transform parent = ResolveGeneratedShoreSandParent();
            int previousGeneratedShoreCount = generatedShoreSandPoints != null ? generatedShoreSandPoints.Count : 0;
            generatedShoreSandPoints = new HashSet<Vector2Int>(placements.Count);
            LogMapDataMutation(nameof(generatedShoreSandPoints), previousGeneratedShoreCount, generatedShoreSandPoints.Count, nameof(GenerateShoreSandAsync));

            for (int i = 0; i < placements.Count; i++)
            {
                Vector2Int point = placements[i].point;
                if (!tilemapIndexByPoint.TryGetValue(point, out int tilemapIndex))
                {
                    continue;
                }

                if (placements[i].replacesGrassTile)
                {
                    string previousClassification = ResolvePointClassificationLabel(point, areaByPoint);
                    if (areaByPoint.TryGetValue(point, out AreaType previousAreaType) &&
                        IsBaseLandAreaType(previousAreaType))
                    {
                        LogGrassChangedToShore(point, previousClassification, AreaType.Beach.ToString(), nameof(GenerateShoreSandAsync));
                    }

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
                string previousClassificationBeforeInstantiation = ResolvePointClassificationLabel(point, areaByPoint);

                GameObject instance = Instantiate(prefab, worldPosition, finalRotation, parent);
                ApplyShoreSandDebugName(instance, placements[i], point);
                TraceFinalShoreSandInstantiation(point, placements[i], prefab, finalYaw, instance);
                if (placements[i].marksAsBeach)
                {
                    if (!placements[i].replacesGrassTile &&
                        areaByPoint.TryGetValue(point, out AreaType previousAreaTypeBeforeInstantiation) &&
                        IsBaseLandAreaType(previousAreaTypeBeforeInstantiation))
                    {
                        LogGrassChangedToShore(point, previousClassificationBeforeInstantiation, AreaType.Beach.ToString(), nameof(GenerateShoreSandAsync));
                    }

                    bool wasAdded = generatedShoreSandPoints.Add(point);
                    if (wasAdded && hasCompletedFirstShoreGeneration && diagnosticKnownShorePoints.Add(point))
                    {
                        LogLateShoreCreated(
                            currentShoreGenerationInvocationIndex,
                            point,
                            worldPosition,
                            previousClassificationBeforeInstantiation);
                    }
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

            completionStats.generatedShoreSandPointsCount = generatedShoreSandPoints.Count;
            completionStats.instantiatedObjectCount = parent != null ? parent.childCount : 0;
            RefreshFinalShoreWaterState(allLandPoints);
            return CompleteAndLog();
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
            int previousShoreWaterCount = currentShoreWaterPoints != null ? currentShoreWaterPoints.Count : 0;
            currentShoreWaterPoints = new HashSet<Vector2Int>();
            LogMapDataMutation(nameof(currentShoreWaterPoints), previousShoreWaterCount, currentShoreWaterPoints.Count, nameof(CollectExteriorOceanPoints));
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
            LogMapDataMutation(nameof(currentExteriorOceanPoints), currentExteriorOceanPoints != null ? currentExteriorOceanPoints.Count : 0, exteriorOceanPoints.Count, nameof(CollectExteriorOceanPoints));
            LogMapDataMutation(nameof(currentShoreWaterPoints), previousShoreWaterCount, currentShoreWaterPoints.Count, nameof(CollectExteriorOceanPoints));
            return exteriorOceanPoints;
        }

        private void RebuildFinalWaterAndWallState()
        {
            Dictionary<Vector2Int, AreaType> areaByPoint = new Dictionary<Vector2Int, AreaType>();
            Dictionary<Vector2Int, int> tilemapIndexByPoint = new Dictionary<Vector2Int, int>();
            HashSet<Vector2Int> allLandPoints = CollectLandPointMetadata(areaByPoint, tilemapIndexByPoint);
            if (allLandPoints.Count == 0)
            {
                return;
            }

            RefreshFinalShoreWaterState(allLandPoints);
            FilterWallColliderPointsUsingFinalShoreState(allLandPoints);
        }

        private void RefreshFinalShoreWaterState(HashSet<Vector2Int> allLandPoints)
        {
            HashSet<Vector2Int> finalWalkablePoints = BuildFinalWalkablePointSet(allLandPoints);
            int previousFinalWalkableCount = currentFinalWalkablePoints != null ? currentFinalWalkablePoints.Count : 0;
            currentFinalWalkablePoints = new HashSet<Vector2Int>(finalWalkablePoints);
            LogMapDataMutation(nameof(currentFinalWalkablePoints), previousFinalWalkableCount, currentFinalWalkablePoints.Count, nameof(RefreshFinalShoreWaterState));
            if (finalWalkablePoints.Count == 0)
            {
                int previousExteriorOceanCount = currentExteriorOceanPoints != null ? currentExteriorOceanPoints.Count : 0;
                int previousShoreWaterCount = currentShoreWaterPoints != null ? currentShoreWaterPoints.Count : 0;
                currentExteriorOceanPoints = new HashSet<Vector2Int>();
                currentShoreWaterPoints = new HashSet<Vector2Int>();
                LogMapDataMutation(nameof(currentExteriorOceanPoints), previousExteriorOceanCount, currentExteriorOceanPoints.Count, nameof(RefreshFinalShoreWaterState));
                LogMapDataMutation(nameof(currentShoreWaterPoints), previousShoreWaterCount, currentShoreWaterPoints.Count, nameof(RefreshFinalShoreWaterState));
                currentShoreLandBounds = null;
                currentEnclosedWaterPointCount = 0;
                return;
            }

            currentShoreLandBounds = CalculatePointBounds(finalWalkablePoints);
            int previousExteriorOceanCountBeforeCollect = currentExteriorOceanPoints != null ? currentExteriorOceanPoints.Count : 0;
            currentExteriorOceanPoints = CollectExteriorOceanPoints(finalWalkablePoints);
            LogMapDataMutation(nameof(currentExteriorOceanPoints), previousExteriorOceanCountBeforeCollect, currentExteriorOceanPoints != null ? currentExteriorOceanPoints.Count : 0, nameof(RefreshFinalShoreWaterState));

            if (generatedShoreSandPoints != null && generatedShoreSandPoints.Count > 0)
            {
                int shoreWaterBeforeExcept = currentShoreWaterPoints != null ? currentShoreWaterPoints.Count : 0;
                int exteriorBeforeExcept = currentExteriorOceanPoints != null ? currentExteriorOceanPoints.Count : 0;
                currentShoreWaterPoints.ExceptWith(generatedShoreSandPoints);
                currentExteriorOceanPoints.ExceptWith(generatedShoreSandPoints);
                LogMapDataMutation(nameof(currentShoreWaterPoints), shoreWaterBeforeExcept, currentShoreWaterPoints != null ? currentShoreWaterPoints.Count : 0, nameof(RefreshFinalShoreWaterState));
                LogMapDataMutation(nameof(currentExteriorOceanPoints), exteriorBeforeExcept, currentExteriorOceanPoints != null ? currentExteriorOceanPoints.Count : 0, nameof(RefreshFinalShoreWaterState));
            }
        }

        private HashSet<Vector2Int> BuildFinalWalkablePointSet(HashSet<Vector2Int> allLandPoints)
        {
            HashSet<Vector2Int> finalWalkablePoints = allLandPoints != null
                ? new HashSet<Vector2Int>(allLandPoints)
                : new HashSet<Vector2Int>();

            if (generatedShoreSandPoints != null && generatedShoreSandPoints.Count > 0)
            {
                finalWalkablePoints.UnionWith(generatedShoreSandPoints);
            }

            return finalWalkablePoints;
        }

        private void FilterWallColliderPointsUsingFinalShoreState(HashSet<Vector2Int> allLandPoints)
        {
            if (wallColliderPoints == null || wallColliderPoints.Count == 0)
            {
                Debug.Log(
                    $"[RandomMap.Clear] generationId={currentGenerateMapDebugId} isPlaying={Application.isPlaying} " +
                    "wallFilterSkipped=True reason=no-wall-candidates",
                    this);
                return;
            }

            HashSet<Vector2Int> finalWalkablePoints = BuildFinalWalkablePointSet(allLandPoints);
            HashSet<Vector2Int> trueBlockedWaterPoints = new HashSet<Vector2Int>();
            if (currentShoreWaterPoints != null)
            {
                trueBlockedWaterPoints.UnionWith(currentShoreWaterPoints);
            }

            if (generatedShoreSandPoints != null && generatedShoreSandPoints.Count > 0)
            {
                trueBlockedWaterPoints.ExceptWith(generatedShoreSandPoints);
            }

            int beforeCount = wallColliderPoints.Count;
            HashSet<Vector2Int> filteredWallPoints = new HashSet<Vector2Int>();
            foreach (Vector2Int candidate in wallColliderPoints)
            {
                if (!trueBlockedWaterPoints.Contains(candidate))
                {
                    continue;
                }

                int adjacentWalkableCount = CountAdjacentPoints(candidate, finalWalkablePoints);
                int adjacentBlockedWaterCount = CountAdjacentPoints(candidate, trueBlockedWaterPoints);
                bool keepWall = adjacentWalkableCount > 0 && adjacentBlockedWaterCount > 0;
                if (keepWall)
                {
                    filteredWallPoints.Add(candidate);
                }
            }

            wallColliderPoints = filteredWallPoints;

            Debug.Log(
                $"[RandomMap.GenerateCall] generationId={currentGenerateMapDebugId} isPlaying={Application.isPlaying} " +
                $"wallPointSemantic=candidate-blocked-cells wallFilterBefore={beforeCount} wallFilterAfter={wallColliderPoints.Count}",
                this);
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

        private Dictionary<Vector2Int, int> BuildLocalMaximumDepthByPoint(
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            Dictionary<Vector2Int, int> shoreDepthByPoint,
            out int affectedPointCount)
        {
            Dictionary<Vector2Int, int> localMaximumDepthByPoint = new Dictionary<Vector2Int, int>();
            affectedPointCount = 0;

            if (allLandPoints == null || allLandPoints.Count == 0)
            {
                return localMaximumDepthByPoint;
            }

            int baseMaxDepth = Mathf.Max(0, shoreSandWidth - 1);
            foreach (Vector2Int point in allLandPoints)
            {
                if (!IsGrassLandPoint(point, allLandPoints, areaByPoint))
                {
                    continue;
                }

                localMaximumDepthByPoint[point] = baseMaxDepth;
            }

            currentMainBeachPointCount = 0;
            currentSecondaryBeachPointCount = 0;
            currentSecondaryBeachRegionCount = 0;
            currentSecondaryBeachLayoutDirections = new List<BeachLayoutDirection>();
            currentMainBeachPoints = new HashSet<Vector2Int>();
            currentSecondaryBeachPoints = new HashSet<Vector2Int>();
            currentResolvedMainBeachDirectionLabel = "None";
            currentMainBeachFailureReason = "not-attempted";

            HashSet<Vector2Int> ordinaryGrassPoints = CollectBudgetDirectionalOrdinaryGrassPoints(
                allLandPoints,
                areaByPoint,
                shoreDepthByPoint);
            int baseOrdinaryGrassPointCount = ordinaryGrassPoints.Count;
            int targetDirectionalBeachArea = Mathf.RoundToInt(baseOrdinaryGrassPointCount * directionalBeachTargetGrassRatio);
            int mainBeachTargetArea = Mathf.RoundToInt(targetDirectionalBeachArea * directionalMainBeachBudgetRatio);
            int secondaryBeachTargetArea = Mathf.Max(0, targetDirectionalBeachArea - mainBeachTargetArea);

            currentDirectionalWideBeachDirections = new List<ShoreEdgeDirection>();
            if (!enableDirectionalWideBeach ||
                directionalWideBeachMaximumDepth <= 0 ||
                targetDirectionalBeachArea <= 0)
            {
                LogDirectionalWideBeachCandidateSummary(new DirectionalWideBeachCandidateDiagnostics
                {
                    batchId = currentDirectionalWideBeachBatchId,
                    phase = "Main",
                    callIndex = 0,
                    selectedDirection = ShoreEdgeDirection.Up,
                    rawCandidatePointCount = 0,
                    connectedComponentCount = 0,
                    acceptedSegmentCount = 0,
                    topRejectedSegments = new List<DirectionalWideBeachRejectedSegmentInfo>(),
                    unaccountedPointSamples = new List<string>()
                });
                LogDirectionalWideBeachBudgetSummary(
                    currentDirectionalWideBeachBatchId,
                    "Overall",
                    0,
                    "None",
                    baseOrdinaryGrassPointCount,
                    targetDirectionalBeachArea,
                    mainBeachTargetArea,
                    0,
                    secondaryBeachTargetArea,
                    0,
                    0,
                    0f,
                    0,
                    0,
                    0,
                    "disabled-or-no-target-area");
                return localMaximumDepthByPoint;
            }

            HashSet<Vector2Int> directionalBeachPoints = new HashSet<Vector2Int>();
            HashSet<Vector2Int> usedShorelinePoints = new HashSet<Vector2Int>();
            int rejectedCandidateCount = 0;
            string stoppedReason = "completed";

            int actualMainBeachArea = 0;
            int selectedMainSegmentLength = 0;
            int mainAchievedDepth = 0;
            List<BeachLayoutDirection> mainDirectionAttemptOrder = BuildShuffledBeachLayoutDirections();
            currentMainBeachFailureReason = "no-safe-main-segment";
            for (int i = 0; i < mainDirectionAttemptOrder.Count; i++)
            {
                BeachLayoutDirection attemptedDirection = mainDirectionAttemptOrder[i];
                int mainRejectedCount;
                DirectionalWideBeachCandidateDiagnostics mainCandidateDiagnostics;
                int candidateSegmentCount;
                int bestSegmentLength;
                if (!TrySelectBudgetDirectionalSegmentForLayout(
                        attemptedDirection,
                        allLandPoints,
                        areaByPoint,
                        shoreDepthByPoint,
                        ordinaryGrassPoints,
                        usedShorelinePoints,
                        baseMaxDepth,
                        out DirectionalWideBeachSegment mainSegment,
                        out ShoreEdgeDirection mainCardinalDirection,
                        out mainRejectedCount,
                        out mainCandidateDiagnostics,
                        out candidateSegmentCount,
                        out bestSegmentLength))
                {
                    rejectedCandidateCount += mainRejectedCount;
                    currentMainBeachFailureReason = "no-safe-main-segment";
                    Debug.Log(
                        $"[RandomMap.MainBeachAttempt] direction={attemptedDirection} candidateSegmentCount={candidateSegmentCount} " +
                        $"bestSegmentLength={bestSegmentLength} budget={mainBeachTargetArea} generatedNewPointCount=0 failureReason={currentMainBeachFailureReason}",
                        this);
                    continue;
                }

                rejectedCandidateCount += mainRejectedCount;
                DirectionalWideBeachBuildResult mainResult = BuildBudgetDirectionalBeachFromSegment(
                    mainSegment,
                    mainBeachTargetArea,
                    directionalWideBeachMinimumDepth,
                    directionalWideBeachMaximumDepth,
                    shoreDepthByPoint,
                    localMaximumDepthByPoint,
                    allLandPoints,
                    areaByPoint,
                    ordinaryGrassPoints,
                    directionalBeachPoints,
                    baseMaxDepth,
                    true);

                int generatedNewPointCount = mainResult.actualArea;
                string failureReason = generatedNewPointCount > 0
                    ? "success"
                    : mainResult.stoppedReason;
                Debug.Log(
                    $"[RandomMap.MainBeachAttempt] direction={attemptedDirection} candidateSegmentCount={candidateSegmentCount} " +
                    $"bestSegmentLength={bestSegmentLength} budget={mainBeachTargetArea} generatedNewPointCount={generatedNewPointCount} failureReason={failureReason}",
                    this);

                if (generatedNewPointCount <= 0)
                {
                    currentMainBeachFailureReason = failureReason;
                    continue;
                }

                currentMainBeachLayoutDirection = attemptedDirection;
                currentResolvedMainBeachDirectionLabel = attemptedDirection.ToString();
                currentMainBeachFailureReason = "success";
                currentDirectionalWideBeachDirections.Add(mainCardinalDirection);
                actualMainBeachArea = mainResult.actualArea;
                currentMainBeachPoints = mainResult.addedPoints != null
                    ? new HashSet<Vector2Int>(mainResult.addedPoints)
                    : new HashSet<Vector2Int>();
                mainAchievedDepth = mainResult.achievedDepth;
                selectedMainSegmentLength = mainSegment.orderedPoints == null ? 0 : mainSegment.orderedPoints.Count;
                usedShorelinePoints.UnionWith(mainResult.shorelinePoints);
                stoppedReason = mainResult.stoppedReason;
                break;
            }

            int actualSecondaryBeachArea = 0;
            int selectedSecondaryBeachCount = 0;
            int remainingBudget = Mathf.Max(0, targetDirectionalBeachArea - actualMainBeachArea);
            int requestedSecondaryCount = directionalSecondaryBeachMaxCount > 0
                ? UnityEngine.Random.Range(0, Mathf.Min(3, directionalSecondaryBeachMaxCount) + 1)
                : 0;
            BeachLayoutDirection secondarySelectionAnchor = actualMainBeachArea > 0
                ? currentMainBeachLayoutDirection
                : mainDirectionAttemptOrder.Count > 0 ? mainDirectionAttemptOrder[0] : BeachLayoutDirection.Up;
            List<BeachLayoutDirection> secondaryDirectionOrder = SelectSecondaryBeachLayoutDirections(secondarySelectionAnchor, requestedSecondaryCount);

            if (actualMainBeachArea > 0 && remainingBudget > 0 && secondaryDirectionOrder.Count > 0)
            {
                for (int i = 0; i < secondaryDirectionOrder.Count && selectedSecondaryBeachCount < requestedSecondaryCount && remainingBudget > 0; i++)
                {
                    int maximumAllowedSecondaryArea = Mathf.Max(0, Mathf.FloorToInt(actualMainBeachArea / 1.5f));
                    int remainingSecondaryAllowance = Mathf.Max(0, maximumAllowedSecondaryArea - actualSecondaryBeachArea);
                    if (remainingSecondaryAllowance <= 0)
                    {
                        break;
                    }

                    int secondaryBudgetCap = Mathf.Max(1, Mathf.FloorToInt(remainingBudget * 0.45f));
                    int secondaryBudget = Mathf.Min(
                        remainingBudget,
                        Mathf.Min(
                            Mathf.Max(1, secondaryBeachTargetArea > 0
                                ? Mathf.CeilToInt((float)secondaryBeachTargetArea / Mathf.Max(1, requestedSecondaryCount))
                                : secondaryBudgetCap),
                            secondaryBudgetCap));
                    secondaryBudget = Mathf.Min(secondaryBudget, Mathf.Max(1, actualMainBeachArea - 1));
                    secondaryBudget = Mathf.Min(secondaryBudget, remainingSecondaryAllowance);
                    if (secondaryBudget <= 0)
                    {
                        break;
                    }

                    int secondaryMinimumDepth = Mathf.Max(1, directionalWideBeachMinimumDepth - 2);
                    int secondaryMaximumDepth = Mathf.Max(
                        secondaryMinimumDepth,
                        Mathf.Min(
                            Mathf.Max(secondaryMinimumDepth, mainAchievedDepth - 1),
                            Mathf.Min(directionalWideBeachMaximumDepth - 2, directionalWideBeachMinimumDepth + 1)));

                    int secondaryRejectedCount;
                    DirectionalWideBeachCandidateDiagnostics secondaryDiagnostics;
                    if (!TrySelectBudgetDirectionalSegmentForLayout(
                            secondaryDirectionOrder[i],
                            allLandPoints,
                            areaByPoint,
                            shoreDepthByPoint,
                            ordinaryGrassPoints,
                            usedShorelinePoints,
                            baseMaxDepth,
                            out DirectionalWideBeachSegment secondarySegment,
                            out ShoreEdgeDirection secondaryCardinalDirection,
                            out secondaryRejectedCount,
                            out secondaryDiagnostics,
                            out _,
                            out _))
                    {
                        rejectedCandidateCount += secondaryRejectedCount;
                        continue;
                    }

                    rejectedCandidateCount += secondaryRejectedCount;
                    if (selectedMainSegmentLength > 0 &&
                        secondarySegment.orderedPoints != null &&
                        secondarySegment.orderedPoints.Count >= selectedMainSegmentLength)
                    {
                        continue;
                    }

                    DirectionalWideBeachBuildResult secondaryResult = BuildBudgetDirectionalBeachFromSegment(
                        secondarySegment,
                        secondaryBudget,
                        secondaryMinimumDepth,
                        secondaryMaximumDepth,
                        shoreDepthByPoint,
                        localMaximumDepthByPoint,
                        allLandPoints,
                        areaByPoint,
                        ordinaryGrassPoints,
                        directionalBeachPoints,
                        baseMaxDepth,
                        false);
                    if (secondaryResult.actualArea <= 0)
                    {
                        continue;
                    }

                    actualSecondaryBeachArea += secondaryResult.actualArea;
                    if (secondaryResult.addedPoints != null)
                    {
                        currentSecondaryBeachPoints.UnionWith(secondaryResult.addedPoints);
                    }
                    currentDirectionalWideBeachDirections.Add(secondaryCardinalDirection);
                    currentSecondaryBeachLayoutDirections.Add(secondaryDirectionOrder[i]);
                    usedShorelinePoints.UnionWith(secondaryResult.shorelinePoints);
                    remainingBudget = Mathf.Max(0, remainingBudget - secondaryResult.actualArea);
                    selectedSecondaryBeachCount++;
                    stoppedReason = secondaryResult.stoppedReason;
                }
            }

            affectedPointCount = directionalBeachPoints.Count;
            currentMainBeachPointCount = actualMainBeachArea;
            currentSecondaryBeachPointCount = actualSecondaryBeachArea;
            currentSecondaryBeachRegionCount = selectedSecondaryBeachCount;
            float achievedGrassRatio = baseOrdinaryGrassPointCount > 0
                ? (float)(actualMainBeachArea + actualSecondaryBeachArea) / baseOrdinaryGrassPointCount
                : 0f;

            LogDirectionalWideBeachBudgetSummary(
                currentDirectionalWideBeachBatchId,
                "Overall",
                currentDirectionalWideBeachCallIndex,
                actualMainBeachArea > 0 ? currentResolvedMainBeachDirectionLabel : "None",
                baseOrdinaryGrassPointCount,
                targetDirectionalBeachArea,
                mainBeachTargetArea,
                actualMainBeachArea,
                secondaryBeachTargetArea,
                actualSecondaryBeachArea,
                actualMainBeachArea + actualSecondaryBeachArea,
                achievedGrassRatio,
                selectedMainSegmentLength,
                selectedSecondaryBeachCount,
                rejectedCandidateCount,
                stoppedReason);

            return localMaximumDepthByPoint;
        }

        private List<ShoreEdgeDirection> SelectDirectionalWideBeachDirections()
        {
            List<ShoreEdgeDirection> directions = new List<ShoreEdgeDirection>();
            int directionCount = randomizeDirectionalWideBeachDirectionCount
                ? UnityEngine.Random.Range(1, 3)
                : Mathf.Clamp(directionalWideBeachDirectionCount, 1, 2);

            if (directionCount <= 0)
            {
                return directions;
            }

            if (directionCount == 1)
            {
                directions.Add((ShoreEdgeDirection)UnityEngine.Random.Range(0, 4));
                return directions;
            }

            if (preferAdjacentWideBeachDirections)
            {
                ShoreEdgeDirection[][] adjacentPairs =
                {
                    new[] { ShoreEdgeDirection.Up, ShoreEdgeDirection.Right },
                    new[] { ShoreEdgeDirection.Right, ShoreEdgeDirection.Down },
                    new[] { ShoreEdgeDirection.Down, ShoreEdgeDirection.Left },
                    new[] { ShoreEdgeDirection.Left, ShoreEdgeDirection.Up }
                };

                ShoreEdgeDirection[] pair = adjacentPairs[UnityEngine.Random.Range(0, adjacentPairs.Length)];
                directions.Add(pair[0]);
                directions.Add(pair[1]);
                return directions;
            }

            List<ShoreEdgeDirection> pool = new List<ShoreEdgeDirection>
            {
                ShoreEdgeDirection.Up,
                ShoreEdgeDirection.Down,
                ShoreEdgeDirection.Left,
                ShoreEdgeDirection.Right
            };

            while (directions.Count < 2 && pool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                directions.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return directions;
        }

        private bool TrySelectDirectionalWideBeachSegment(
            ShoreEdgeDirection selectedDirection,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            Dictionary<Vector2Int, int> shoreDepthByPoint,
            int baseMaxDepth,
            out DirectionalWideBeachSegment bestSegment,
            out int rejectedSegmentCount)
        {
            bestSegment = default;
            rejectedSegmentCount = 0;

            HashSet<Vector2Int> candidatePoints = CollectDirectionalWideBeachCandidateShorelinePoints(
                selectedDirection,
                allLandPoints,
                areaByPoint,
                shoreDepthByPoint);
            if (candidatePoints.Count == 0)
            {
                return false;
            }

            List<List<Vector2Int>> rawSegments = SplitIntoConnectedDirectionalWideBeachSegments(
                candidatePoints,
                allLandPoints,
                areaByPoint,
                shoreDepthByPoint,
                true);
            bool foundSegment = false;
            float bestScore = float.MinValue;

            for (int i = 0; i < rawSegments.Count; i++)
            {
                if (!TryBuildDirectionalWideBeachSegment(
                        selectedDirection,
                        rawSegments[i],
                        allLandPoints,
                        areaByPoint,
                        baseMaxDepth,
                        out DirectionalWideBeachSegment segment,
                        out _,
                        out _))
                {
                    rejectedSegmentCount++;
                    continue;
                }

                if (!foundSegment || segment.score > bestScore)
                {
                    bestSegment = segment;
                    bestScore = segment.score;
                    foundSegment = true;
                }
            }

            if (!foundSegment)
            {
                return false;
            }

            return true;
        }

        private HashSet<Vector2Int> CollectDirectionalWideBeachCandidateShorelinePoints(
            ShoreEdgeDirection selectedDirection,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            Dictionary<Vector2Int, int> shoreDepthByPoint)
        {
            HashSet<Vector2Int> candidatePoints = new HashSet<Vector2Int>();
            if (shoreDepthByPoint == null || currentExteriorOceanPoints == null || currentExteriorOceanPoints.Count == 0)
            {
                return candidatePoints;
            }

            foreach (KeyValuePair<Vector2Int, int> kvp in shoreDepthByPoint)
            {
                if (kvp.Value != 0 ||
                    !IsGrassLandPoint(kvp.Key, allLandPoints, areaByPoint) ||
                    HasDirectCardinalEnclosedWaterNeighbor(kvp.Key) ||
                    !HasAnyCardinalExteriorOceanNeighbor(kvp.Key))
                {
                    continue;
                }

                candidatePoints.Add(kvp.Key);
            }

            return candidatePoints;
        }

        private List<List<Vector2Int>> SplitIntoConnectedDirectionalWideBeachSegments(
            HashSet<Vector2Int> candidatePoints,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            Dictionary<Vector2Int, int> shoreDepthByPoint,
            bool allowRestrictedDiagonal)
        {
            List<List<Vector2Int>> segments = new List<List<Vector2Int>>();
            if (candidatePoints == null || candidatePoints.Count == 0)
            {
                return segments;
            }

            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            List<Vector2Int> orderedCandidates = new List<Vector2Int>(candidatePoints);
            orderedCandidates.Sort(ComparePointOrder);

            for (int i = 0; i < orderedCandidates.Count; i++)
            {
                Vector2Int start = orderedCandidates[i];
                if (!visited.Add(start))
                {
                    continue;
                }

                List<Vector2Int> segment = new List<Vector2Int>();
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    segment.Add(current);

                    for (int candidateIndex = 0; candidateIndex < orderedCandidates.Count; candidateIndex++)
                    {
                        Vector2Int neighbor = orderedCandidates[candidateIndex];
                        if (!candidatePoints.Contains(neighbor) ||
                            !AreDirectionalBeachShorelinePointsConnected(
                                current,
                                neighbor,
                                candidatePoints,
                                allLandPoints,
                                areaByPoint,
                                shoreDepthByPoint,
                                allowRestrictedDiagonal) ||
                            !visited.Add(neighbor))
                        {
                            continue;
                        }

                        queue.Enqueue(neighbor);
                    }
                }

                segment.Sort(ComparePointOrder);
                segments.Add(segment);
            }

            return segments;
        }

        private bool TryBuildDirectionalWideBeachSegment(
            ShoreEdgeDirection selectedDirection,
            List<Vector2Int> rawSegmentPoints,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            int baseMaxDepth,
            out DirectionalWideBeachSegment segment,
            out string rejectedReason,
            out DirectionalWideBeachRejectedSegmentInfo rejectedInfo)
        {
            segment = default;
            rejectedReason = null;
            rejectedInfo = CreateDirectionalWideBeachRejectedSegmentInfo(rawSegmentPoints, selectedDirection, 0f, 0, 0, 0);
            if (rawSegmentPoints == null || rawSegmentPoints.Count < directionalWideBeachMinimumSegmentLength)
            {
                rejectedReason = "too-short";
                rejectedInfo.reason = rejectedReason;
                return false;
            }

            HashSet<Vector2Int> segmentPointSet = new HashSet<Vector2Int>(rawSegmentPoints);
            Dictionary<Vector2Int, List<Vector2Int>> neighborsByPoint =
                BuildDirectionalWideBeachNeighborMap(segmentPointSet, allLandPoints, areaByPoint, true);

            int endpointCount = 0;
            Vector2Int startPoint = rawSegmentPoints[0];
            bool hasBranch = false;
            int branchPointCount = 0;
            foreach (KeyValuePair<Vector2Int, List<Vector2Int>> kvp in neighborsByPoint)
            {
                int degree = kvp.Value.Count;
                if (degree == 1)
                {
                    endpointCount++;
                    if (ComparePointOrder(kvp.Key, startPoint) < 0)
                    {
                        startPoint = kvp.Key;
                    }
                }
                else if (degree > 2)
                {
                    hasBranch = true;
                    branchPointCount++;
                }
            }

            if (hasBranch)
            {
                rejectedReason = "branch-detected";
                rejectedInfo = CreateDirectionalWideBeachRejectedSegmentInfo(rawSegmentPoints, selectedDirection, 0f, 0, 0, branchPointCount);
                rejectedInfo.reason = rejectedReason;
                return false;
            }

            if (endpointCount != 2)
            {
                rejectedReason = endpointCount == 0 ? "closed-loop" : "unknown";
                rejectedInfo = CreateDirectionalWideBeachRejectedSegmentInfo(rawSegmentPoints, selectedDirection, 0f, 0, 0, branchPointCount);
                rejectedInfo.reason = rejectedReason;
                return false;
            }

            List<Vector2Int> orderedPoints = OrderDirectionalWideBeachSegmentPoints(startPoint, neighborsByPoint);
            if (orderedPoints.Count != rawSegmentPoints.Count ||
                orderedPoints.Count < directionalWideBeachMinimumSegmentLength)
            {
                rejectedReason = "path-order-failed";
                rejectedInfo = CreateDirectionalWideBeachRejectedSegmentInfo(orderedPoints.Count > 0 ? orderedPoints : rawSegmentPoints, selectedDirection, 0f, 0, 0, branchPointCount);
                rejectedInfo.reason = rejectedReason;
                return false;
            }

            int turnCount = 0;
            for (int i = 1; i < orderedPoints.Count - 1; i++)
            {
                Vector2Int previousStep = orderedPoints[i] - orderedPoints[i - 1];
                Vector2Int nextStep = orderedPoints[i + 1] - orderedPoints[i];
                if (previousStep != nextStep)
                {
                    turnCount++;
                }
            }

            float curvatureRatio = orderedPoints.Count <= 2
                ? 0f
                : (float)turnCount / (orderedPoints.Count - 2);
            if (curvatureRatio > directionalWideBeachCurvatureTolerance)
            {
                rejectedReason = "curvature-too-high";
                rejectedInfo = CreateDirectionalWideBeachRejectedSegmentInfo(orderedPoints, selectedDirection, curvatureRatio, 0, 0, branchPointCount);
                rejectedInfo.reason = rejectedReason;
                return false;
            }

            int totalSupport = 0;
            int supportedPointCount = 0;
            int nearbyEnclosedWaterCount = 0;
            int minimumDesiredSupport = baseMaxDepth + Mathf.Max(2, Mathf.Min(directionalWideBeachExtraWidth, 3));
            Vector2Int inwardOffset = GetOppositeCardinalOffset(selectedDirection);

            for (int i = 0; i < orderedPoints.Count; i++)
            {
                int support = CountDirectionalWideBeachInlandSupport(
                    orderedPoints[i],
                    inwardOffset,
                    allLandPoints,
                    areaByPoint,
                    baseMaxDepth + directionalWideBeachExtraWidth);
                totalSupport += support;
                if (support >= minimumDesiredSupport)
                {
                    supportedPointCount++;
                }

                if (HasNearbyEnclosedWater(orderedPoints[i], 2))
                {
                    nearbyEnclosedWaterCount++;
                }
            }

            float supportRatio = orderedPoints.Count == 0 ? 0f : (float)supportedPointCount / orderedPoints.Count;
            int averageSupport = orderedPoints.Count == 0 ? 0 : Mathf.RoundToInt((float)totalSupport / orderedPoints.Count);
            if (averageSupport < minimumDesiredSupport || supportRatio < 0.6f)
            {
                rejectedReason = "insufficient-inland-support";
                rejectedInfo = CreateDirectionalWideBeachRejectedSegmentInfo(orderedPoints, selectedDirection, curvatureRatio, averageSupport, nearbyEnclosedWaterCount, branchPointCount);
                rejectedInfo.reason = rejectedReason;
                return false;
            }

            float orientationScore = EvaluateDirectionalWideBeachSegmentOrientationScore(orderedPoints, selectedDirection);
            if (orientationScore < 0.45f)
            {
                rejectedReason = "direction-score-too-low";
                rejectedInfo = CreateDirectionalWideBeachRejectedSegmentInfo(orderedPoints, selectedDirection, curvatureRatio, averageSupport, nearbyEnclosedWaterCount, branchPointCount);
                rejectedInfo.reason = rejectedReason;
                return false;
            }

            int centerIndex = orderedPoints.Count / 2;
            float score =
                (orderedPoints.Count * 3f) +
                (orientationScore * 12f) +
                (averageSupport * 1.2f) +
                (supportRatio * 8f) -
                (curvatureRatio * 14f) -
                (nearbyEnclosedWaterCount * 2f);

            segment = new DirectionalWideBeachSegment
            {
                selectedDirection = selectedDirection,
                orderedPoints = orderedPoints,
                pointSet = segmentPointSet,
                startPoint = orderedPoints[0],
                endPoint = orderedPoints[orderedPoints.Count - 1],
                centerPoint = orderedPoints[centerIndex],
                centerIndex = centerIndex,
                score = score,
                averageInlandSupport = averageSupport,
                nearbyEnclosedWaterCount = nearbyEnclosedWaterCount,
                curvatureRatio = curvatureRatio
            };

            return true;
        }

        private Dictionary<Vector2Int, List<Vector2Int>> BuildDirectionalWideBeachNeighborMap(
            HashSet<Vector2Int> points,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            bool allowRestrictedDiagonal)
        {
            Dictionary<Vector2Int, List<Vector2Int>> neighborsByPoint =
                new Dictionary<Vector2Int, List<Vector2Int>>();
            if (points == null)
            {
                return neighborsByPoint;
            }

            foreach (Vector2Int point in points)
            {
                List<Vector2Int> neighbors = new List<Vector2Int>(4);
                foreach (Vector2Int neighbor in points)
                {
                    if (AreDirectionalBeachShorelinePointsConnected(
                            point,
                            neighbor,
                            points,
                            allLandPoints,
                            areaByPoint,
                            null,
                            allowRestrictedDiagonal))
                    {
                        neighbors.Add(neighbor);
                    }
                }

                neighbors.Sort(ComparePointOrder);
                neighborsByPoint[point] = neighbors;
            }

            return neighborsByPoint;
        }

        private List<Vector2Int> OrderDirectionalWideBeachSegmentPoints(
            Vector2Int startPoint,
            Dictionary<Vector2Int, List<Vector2Int>> neighborsByPoint)
        {
            List<Vector2Int> orderedPoints = new List<Vector2Int>();
            if (neighborsByPoint == null || !neighborsByPoint.ContainsKey(startPoint))
            {
                return orderedPoints;
            }

            Vector2Int previousPoint = new Vector2Int(int.MinValue, int.MinValue);
            Vector2Int currentPoint = startPoint;

            while (true)
            {
                orderedPoints.Add(currentPoint);
                List<Vector2Int> neighbors = neighborsByPoint[currentPoint];
                Vector2Int nextPoint = new Vector2Int(int.MinValue, int.MinValue);
                bool foundNext = false;

                for (int i = 0; i < neighbors.Count; i++)
                {
                    if (neighbors[i] == previousPoint)
                    {
                        continue;
                    }

                    nextPoint = neighbors[i];
                    foundNext = true;
                    break;
                }

                if (!foundNext)
                {
                    break;
                }

                previousPoint = currentPoint;
                currentPoint = nextPoint;
            }

            return orderedPoints;
        }

        private int CountDirectionalWideBeachInlandSupport(
            Vector2Int shorelinePoint,
            Vector2Int inwardOffset,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            int maxProbeDepth)
        {
            int support = 0;
            Vector2Int currentPoint = shorelinePoint;

            for (int step = 0; step <= maxProbeDepth; step++)
            {
                if (!IsGrassLandPoint(currentPoint, allLandPoints, areaByPoint))
                {
                    break;
                }

                support++;
                currentPoint += inwardOffset;
            }

            return support;
        }

        private float EvaluateDirectionalWideBeachSegmentOrientationScore(
            List<Vector2Int> orderedPoints,
            ShoreEdgeDirection selectedDirection)
        {
            if (orderedPoints == null || orderedPoints.Count == 0)
            {
                return 0f;
            }

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;

            for (int i = 0; i < orderedPoints.Count; i++)
            {
                Vector2Int point = orderedPoints[i];
                if (point.x < minX) minX = point.x;
                if (point.x > maxX) maxX = point.x;
                if (point.y < minY) minY = point.y;
                if (point.y > maxY) maxY = point.y;
            }

            int horizontalSpan = maxX - minX;
            int verticalSpan = maxY - minY;
            bool prefersHorizontal = selectedDirection == ShoreEdgeDirection.Up || selectedDirection == ShoreEdgeDirection.Down;
            float tangentSpan = prefersHorizontal ? horizontalSpan : verticalSpan;
            float normalSpan = prefersHorizontal ? verticalSpan : horizontalSpan;
            float shapeScore = tangentSpan / Mathf.Max(1f, tangentSpan + normalSpan);

            float averageX = 0f;
            float averageY = 0f;
            float outwardX = 0f;
            float outwardY = 0f;
            int outwardSamples = 0;

            for (int i = 0; i < orderedPoints.Count; i++)
            {
                Vector2Int point = orderedPoints[i];
                averageX += point.x;
                averageY += point.y;

                if (currentExteriorOceanPoints != null && currentExteriorOceanPoints.Count > 0)
                {
                    if (currentExteriorOceanPoints.Contains(point + Vector2Int.up))
                    {
                        outwardY += 1f;
                        outwardSamples++;
                    }

                    if (currentExteriorOceanPoints.Contains(point + Vector2Int.right))
                    {
                        outwardX += 1f;
                        outwardSamples++;
                    }

                    if (currentExteriorOceanPoints.Contains(point + Vector2Int.down))
                    {
                        outwardY -= 1f;
                        outwardSamples++;
                    }

                    if (currentExteriorOceanPoints.Contains(point + Vector2Int.left))
                    {
                        outwardX -= 1f;
                        outwardSamples++;
                    }
                }
            }

            averageX /= orderedPoints.Count;
            averageY /= orderedPoints.Count;

            float centerScore = 0.5f;
            if (currentShoreLandBounds.HasValue)
            {
                BoundsInt bounds = currentShoreLandBounds.Value;
                float centerX = bounds.xMin + (bounds.size.x - 1) * 0.5f;
                float centerY = bounds.yMin + (bounds.size.y - 1) * 0.5f;
                float extentX = Mathf.Max(1f, (bounds.size.x - 1) * 0.5f);
                float extentY = Mathf.Max(1f, (bounds.size.y - 1) * 0.5f);
                float normalizedX = Mathf.Clamp((averageX - centerX) / extentX, -1f, 1f);
                float normalizedY = Mathf.Clamp((averageY - centerY) / extentY, -1f, 1f);

                switch (selectedDirection)
                {
                    case ShoreEdgeDirection.Up:
                        centerScore = Mathf.InverseLerp(-1f, 1f, normalizedY);
                        break;
                    case ShoreEdgeDirection.Down:
                        centerScore = Mathf.InverseLerp(1f, -1f, normalizedY);
                        break;
                    case ShoreEdgeDirection.Left:
                        centerScore = Mathf.InverseLerp(1f, -1f, normalizedX);
                        break;
                    case ShoreEdgeDirection.Right:
                        centerScore = Mathf.InverseLerp(-1f, 1f, normalizedX);
                        break;
                }
            }

            float normalScore = 0.5f;
            if (outwardSamples > 0)
            {
                float invMagnitude = 1f / Mathf.Max(0.0001f, Mathf.Sqrt((outwardX * outwardX) + (outwardY * outwardY)));
                float normalizedOutwardX = outwardX * invMagnitude;
                float normalizedOutwardY = outwardY * invMagnitude;
                Vector2 targetNormal = GetDirectionalWideBeachNormal(selectedDirection);
                float dot = Mathf.Clamp((normalizedOutwardX * targetNormal.x) + (normalizedOutwardY * targetNormal.y), -1f, 1f);
                normalScore = Mathf.InverseLerp(-1f, 1f, dot);
            }

            return (shapeScore * 0.45f) + (centerScore * 0.2f) + (normalScore * 0.35f);
        }

        private bool HasNearbyEnclosedWater(Vector2Int point, int radius)
        {
            if (currentShoreWaterPoints == null || currentShoreWaterPoints.Count == 0 || currentExteriorOceanPoints == null)
            {
                return false;
            }

            for (int offsetX = -radius; offsetX <= radius; offsetX++)
            {
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    if (Mathf.Abs(offsetX) + Mathf.Abs(offsetY) > radius)
                    {
                        continue;
                    }

                    Vector2Int candidate = point + new Vector2Int(offsetX, offsetY);
                    if (currentShoreWaterPoints.Contains(candidate) && !currentExteriorOceanPoints.Contains(candidate))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private int ApplyDirectionalWideBeachSegment(
            ShoreEdgeDirection selectedDirection,
            DirectionalWideBeachSegment segment,
            Dictionary<Vector2Int, int> localMaximumDepthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            int baseMaxDepth)
        {
            if (segment.orderedPoints == null || segment.orderedPoints.Count == 0)
            {
                return 0;
            }

            Vector2Int inwardOffset = GetOppositeCardinalOffset(selectedDirection);
            float halfCoreLength = Mathf.Max(1f, directionalWideBeachAlongShoreLength * 0.5f);
            int affectedPointCount = 0;
            HashSet<Vector2Int> affectedPoints = new HashSet<Vector2Int>();

            for (int i = 0; i < segment.orderedPoints.Count; i++)
            {
                Vector2Int shorelinePoint = segment.orderedPoints[i];
                float alongDistance = Mathf.Abs(i - segment.centerIndex);
                float alongWeight = 1f - Mathf.SmoothStep(
                    halfCoreLength,
                    halfCoreLength + directionalWideBeachFalloffLength,
                    alongDistance);
                int extraDepth = Mathf.Clamp(
                    Mathf.RoundToInt(alongWeight * directionalWideBeachExtraWidth),
                    0,
                    directionalWideBeachExtraWidth);
                if (extraDepth <= 0)
                {
                    continue;
                }

                int columnMaxDepth = baseMaxDepth + extraDepth;
                Vector2Int currentPoint = shorelinePoint;
                for (int step = 0; step <= columnMaxDepth; step++)
                {
                    if (!IsGrassLandPoint(currentPoint, allLandPoints, areaByPoint))
                    {
                        break;
                    }

                    int existingLocalMaxDepth = GetLocalMaximumDepthForPoint(currentPoint, localMaximumDepthByPoint, baseMaxDepth);
                    if (columnMaxDepth > existingLocalMaxDepth)
                    {
                        localMaximumDepthByPoint[currentPoint] = columnMaxDepth;
                        if (affectedPoints.Add(currentPoint))
                        {
                            affectedPointCount++;
                        }
                    }

                    currentPoint += inwardOffset;
                }
            }

            return affectedPointCount;
        }

        private void ExpandShoreDepthMapToLocalMaximumDepth(
            Dictionary<Vector2Int, int> shoreDepthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            Dictionary<Vector2Int, int> localMaximumDepthByPoint,
            int baseMaxDepth)
        {
            if (shoreDepthByPoint == null ||
                shoreDepthByPoint.Count == 0 ||
                allLandPoints == null ||
                areaByPoint == null ||
                localMaximumDepthByPoint == null ||
                localMaximumDepthByPoint.Count == 0)
            {
                return;
            }

            Queue<Vector2Int> queue = new Queue<Vector2Int>(shoreDepthByPoint.Count);
            List<Vector2Int> orderedPoints = new List<Vector2Int>(shoreDepthByPoint.Keys);
            orderedPoints.Sort((lhs, rhs) =>
            {
                int depthCompare = shoreDepthByPoint[lhs].CompareTo(shoreDepthByPoint[rhs]);
                return depthCompare != 0 ? depthCompare : ComparePointOrder(lhs, rhs);
            });

            for (int i = 0; i < orderedPoints.Count; i++)
            {
                queue.Enqueue(orderedPoints[i]);
            }

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
                int currentDepth = shoreDepthByPoint[current];
                int currentLocalMaxDepth = GetLocalMaximumDepthForPoint(current, localMaximumDepthByPoint, baseMaxDepth);
                if (currentDepth >= currentLocalMaxDepth)
                {
                    continue;
                }

                for (int i = 0; i < inwardDirections.Length; i++)
                {
                    Vector2Int neighbor = current + inwardDirections[i];
                    if (shoreDepthByPoint.ContainsKey(neighbor) ||
                        !IsGrassLandPoint(neighbor, allLandPoints, areaByPoint))
                    {
                        continue;
                    }

                    int nextDepth = currentDepth + 1;
                    if (nextDepth > GetLocalMaximumDepthForPoint(neighbor, localMaximumDepthByPoint, baseMaxDepth))
                    {
                        continue;
                    }

                    shoreDepthByPoint.Add(neighbor, nextDepth);
                    queue.Enqueue(neighbor);
                }
            }
        }

        private HashSet<Vector2Int> CollectBudgetDirectionalOrdinaryGrassPoints(
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            Dictionary<Vector2Int, int> shoreDepthByPoint)
        {
            HashSet<Vector2Int> ordinaryGrassPoints = new HashSet<Vector2Int>();
            if (allLandPoints == null || areaByPoint == null)
            {
                return ordinaryGrassPoints;
            }

            foreach (Vector2Int point in allLandPoints)
            {
                if (IsGrassLandPoint(point, allLandPoints, areaByPoint) &&
                    (shoreDepthByPoint == null || !shoreDepthByPoint.ContainsKey(point)))
                {
                    ordinaryGrassPoints.Add(point);
                }
            }

            return ordinaryGrassPoints;
        }

        private BeachLayoutDirection SelectBudgetDirectionalMainDirection()
        {
            return (BeachLayoutDirection)UnityEngine.Random.Range(0, 8);
        }

        private List<BeachLayoutDirection> BuildShuffledBeachLayoutDirections()
        {
            List<BeachLayoutDirection> directions = new List<BeachLayoutDirection>
            {
                BeachLayoutDirection.Up,
                BeachLayoutDirection.Down,
                BeachLayoutDirection.Left,
                BeachLayoutDirection.Right,
                BeachLayoutDirection.UpLeft,
                BeachLayoutDirection.UpRight,
                BeachLayoutDirection.DownLeft,
                BeachLayoutDirection.DownRight
            };

            for (int i = directions.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                BeachLayoutDirection temp = directions[i];
                directions[i] = directions[swapIndex];
                directions[swapIndex] = temp;
            }

            return directions;
        }

        private List<BeachLayoutDirection> SelectSecondaryBeachLayoutDirections(
            BeachLayoutDirection mainDirection,
            int requestedCount)
        {
            List<BeachLayoutDirection> selectedDirections = new List<BeachLayoutDirection>();
            if (requestedCount <= 0)
            {
                return selectedDirections;
            }

            List<BeachLayoutDirection> candidates = new List<BeachLayoutDirection>
            {
                BeachLayoutDirection.Up,
                BeachLayoutDirection.Down,
                BeachLayoutDirection.Left,
                BeachLayoutDirection.Right,
                BeachLayoutDirection.UpLeft,
                BeachLayoutDirection.UpRight,
                BeachLayoutDirection.DownLeft,
                BeachLayoutDirection.DownRight
            };
            candidates.Remove(mainDirection);

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                BeachLayoutDirection temp = candidates[i];
                candidates[i] = candidates[swapIndex];
                candidates[swapIndex] = temp;
            }

            int count = Mathf.Min(requestedCount, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                selectedDirections.Add(candidates[i]);
            }

            return selectedDirections;
        }

        private bool TrySelectBudgetDirectionalSegmentForLayout(
            BeachLayoutDirection selectedLayoutDirection,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            Dictionary<Vector2Int, int> shoreDepthByPoint,
            HashSet<Vector2Int> ordinaryGrassPoints,
            HashSet<Vector2Int> excludedShorelinePoints,
            int baseMaxDepth,
            out DirectionalWideBeachSegment bestSegment,
            out ShoreEdgeDirection selectedCardinalDirection,
            out int rejectedCandidateCount,
            out DirectionalWideBeachCandidateDiagnostics diagnostics,
            out int candidateSegmentCount,
            out int bestSegmentLength)
        {
            bestSegment = default;
            selectedCardinalDirection = ShoreEdgeDirection.Up;
            rejectedCandidateCount = 0;
            diagnostics = default;
            candidateSegmentCount = 0;
            bestSegmentLength = 0;
            ShoreEdgeDirection[] preferredDirections = GetPreferredCardinalDirections(selectedLayoutDirection);
            float bestScore = float.MinValue;
            bool found = false;

            for (int i = 0; i < preferredDirections.Length; i++)
            {
                List<DirectionalWideBeachSegment> candidates = CollectBudgetDirectionalSegments(
                    preferredDirections[i],
                    allLandPoints,
                    areaByPoint,
                    shoreDepthByPoint,
                    ordinaryGrassPoints,
                    excludedShorelinePoints,
                    baseMaxDepth,
                    out int directionRejectedCount,
                    out DirectionalWideBeachCandidateDiagnostics directionDiagnostics);
                rejectedCandidateCount += directionRejectedCount;
                directionDiagnostics.phase = preferredDirections.Length > 1
                    ? $"Layout-{selectedLayoutDirection}-{preferredDirections[i]}"
                    : "Main";
                LogDirectionalWideBeachCandidateSummary(directionDiagnostics);
                candidateSegmentCount += directionDiagnostics.acceptedSegmentCount;

                if (candidates.Count == 0)
                {
                    if (!found)
                    {
                        diagnostics = directionDiagnostics;
                    }

                    continue;
                }

                DirectionalWideBeachSegment candidate = candidates[0];
                if (!found || candidate.score > bestScore)
                {
                    found = true;
                    bestScore = candidate.score;
                    bestSegment = candidate;
                    bestSegmentLength = candidate.orderedPoints != null ? candidate.orderedPoints.Count : 0;
                    selectedCardinalDirection = preferredDirections[i];
                    diagnostics = directionDiagnostics;
                }
            }

            return found;
        }

        private ShoreEdgeDirection[] GetPreferredCardinalDirections(BeachLayoutDirection direction)
        {
            switch (direction)
            {
                case BeachLayoutDirection.Up:
                    return new[] { ShoreEdgeDirection.Up };
                case BeachLayoutDirection.Down:
                    return new[] { ShoreEdgeDirection.Down };
                case BeachLayoutDirection.Left:
                    return new[] { ShoreEdgeDirection.Left };
                case BeachLayoutDirection.Right:
                    return new[] { ShoreEdgeDirection.Right };
                case BeachLayoutDirection.UpLeft:
                    return new[] { ShoreEdgeDirection.Up, ShoreEdgeDirection.Left };
                case BeachLayoutDirection.UpRight:
                    return new[] { ShoreEdgeDirection.Up, ShoreEdgeDirection.Right };
                case BeachLayoutDirection.DownLeft:
                    return new[] { ShoreEdgeDirection.Down, ShoreEdgeDirection.Left };
                case BeachLayoutDirection.DownRight:
                    return new[] { ShoreEdgeDirection.Down, ShoreEdgeDirection.Right };
                default:
                    return new[] { ShoreEdgeDirection.Up };
            }
        }

        private List<DirectionalWideBeachSegment> CollectBudgetDirectionalSecondarySegments(
            ShoreEdgeDirection mainDirection,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            Dictionary<Vector2Int, int> shoreDepthByPoint,
            HashSet<Vector2Int> ordinaryGrassPoints,
            HashSet<Vector2Int> excludedShorelinePoints,
            int baseMaxDepth,
            out int rejectedCandidateCount)
        {
            rejectedCandidateCount = 0;
            List<DirectionalWideBeachSegment> candidates = new List<DirectionalWideBeachSegment>();
            ShoreEdgeDirection[] directionOrder =
            {
                GetClockwiseDirection(mainDirection),
                GetCounterClockwiseDirection(mainDirection),
                GetOppositeDirection(mainDirection),
                mainDirection
            };

            for (int i = 0; i < directionOrder.Length; i++)
            {
                List<DirectionalWideBeachSegment> directionCandidates = CollectBudgetDirectionalSegments(
                    directionOrder[i],
                    allLandPoints,
                    areaByPoint,
                    shoreDepthByPoint,
                    ordinaryGrassPoints,
                    excludedShorelinePoints,
                    baseMaxDepth,
                    out int directionRejectedCount,
                    out DirectionalWideBeachCandidateDiagnostics directionDiagnostics);
                rejectedCandidateCount += directionRejectedCount;
                directionDiagnostics.phase = $"Secondary{i + 1}";
                LogDirectionalWideBeachCandidateSummary(directionDiagnostics);
                candidates.AddRange(directionCandidates);
            }

            candidates.Sort((lhs, rhs) => rhs.score.CompareTo(lhs.score));
            return candidates;
        }

        private List<DirectionalWideBeachSegment> CollectBudgetDirectionalSegments(
            ShoreEdgeDirection selectedDirection,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            Dictionary<Vector2Int, int> shoreDepthByPoint,
            HashSet<Vector2Int> ordinaryGrassPoints,
            HashSet<Vector2Int> excludedShorelinePoints,
            int baseMaxDepth,
            out int rejectedCandidateCount,
            out DirectionalWideBeachCandidateDiagnostics diagnostics)
        {
            rejectedCandidateCount = 0;
            diagnostics = new DirectionalWideBeachCandidateDiagnostics
            {
                batchId = currentDirectionalWideBeachBatchId,
                phase = "Main",
                callIndex = ++currentDirectionalWideBeachCallIndex,
                selectedDirection = selectedDirection,
                topRejectedSegments = new List<DirectionalWideBeachRejectedSegmentInfo>(),
                unaccountedPointSamples = new List<string>()
            };
            List<DirectionalWideBeachSegment> segments = new List<DirectionalWideBeachSegment>();
            HashSet<Vector2Int> candidatePoints = CollectBudgetDirectionalCandidateShorelinePoints(
                selectedDirection,
                allLandPoints,
                areaByPoint,
                shoreDepthByPoint,
                ordinaryGrassPoints,
                excludedShorelinePoints,
                ref diagnostics);
            diagnostics.rawCandidatePointCount = candidatePoints.Count;
            if (candidatePoints.Count == 0)
            {
                return segments;
            }

            List<List<Vector2Int>> rawSegments = SplitIntoConnectedDirectionalWideBeachSegments(
                candidatePoints,
                allLandPoints,
                areaByPoint,
                shoreDepthByPoint,
                true);
            diagnostics.connectedComponentCount = rawSegments.Count;
            for (int i = 0; i < rawSegments.Count; i++)
            {
                if (!TryBuildBudgetDirectionalSegment(
                        selectedDirection,
                        rawSegments[i],
                        allLandPoints,
                        areaByPoint,
                        ordinaryGrassPoints,
                        baseMaxDepth,
                        out DirectionalWideBeachSegment segment,
                        out string rejectedReason,
                        out DirectionalWideBeachRejectedSegmentInfo rejectedInfo))
                {
                    rejectedCandidateCount++;
                    IncrementDirectionalWideBeachRejectedReason(ref diagnostics, rejectedReason);
                    TrackRejectedDirectionalWideBeachSegment(ref diagnostics, rejectedInfo);
                    continue;
                }

                segments.Add(segment);
            }

            diagnostics.acceptedSegmentCount = segments.Count;
            segments.Sort((lhs, rhs) => rhs.score.CompareTo(lhs.score));
            return segments;
        }

        private HashSet<Vector2Int> CollectBudgetDirectionalCandidateShorelinePoints(
            ShoreEdgeDirection selectedDirection,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            Dictionary<Vector2Int, int> shoreDepthByPoint,
            HashSet<Vector2Int> ordinaryGrassPoints,
            HashSet<Vector2Int> excludedShorelinePoints,
            ref DirectionalWideBeachCandidateDiagnostics diagnostics)
        {
            HashSet<Vector2Int> candidatePoints = new HashSet<Vector2Int>();
            if (shoreDepthByPoint == null || currentExteriorOceanPoints == null || currentExteriorOceanPoints.Count == 0)
            {
                return candidatePoints;
            }

            foreach (KeyValuePair<Vector2Int, int> kvp in shoreDepthByPoint)
            {
                diagnostics.sourcePointCount++;

                if (kvp.Value != 0)
                {
                    diagnostics.rejectedDepthNotZero++;
                    continue;
                }

                if (areaByPoint == null || !areaByPoint.ContainsKey(kvp.Key))
                {
                    diagnostics.rejectedMissingAreaEntry++;
                    continue;
                }

                if (!IsGrassLandPoint(kvp.Key, allLandPoints, areaByPoint))
                {
                    diagnostics.rejectedNotOrdinaryShoreCandidate++;
                    continue;
                }

                if (excludedShorelinePoints != null && excludedShorelinePoints.Contains(kvp.Key))
                {
                    diagnostics.rejectedExcludedOrUsed++;
                    continue;
                }

                bool touchesExteriorOcean = HasAnyCardinalExteriorOceanNeighbor(kvp.Key);
                bool directCardinalEnclosedWater = HasDirectCardinalEnclosedWaterNeighbor(kvp.Key);
                bool diagonalOnlyEnclosedWater = !directCardinalEnclosedWater && HasDiagonalOnlyEnclosedWaterNeighbor(kvp.Key);

                if (directCardinalEnclosedWater && touchesExteriorOcean)
                {
                    diagnostics.rejectedExteriorAndEnclosedConflict++;
                    continue;
                }

                if (directCardinalEnclosedWater)
                {
                    diagnostics.rejectedDirectCardinalEnclosedWater++;
                    continue;
                }

                if (diagonalOnlyEnclosedWater)
                {
                    diagnostics.rejectedDiagonalOnlyEnclosedWater++;
                    continue;
                }

                if (!touchesExteriorOcean)
                {
                    diagnostics.rejectedNoAnyExteriorOceanContact++;
                    continue;
                }

                if (!candidatePoints.Add(kvp.Key))
                {
                    diagnostics.rejectedUnhandledBranch++;
                    if (diagnostics.unaccountedPointSamples != null && diagnostics.unaccountedPointSamples.Count < 10)
                    {
                        diagnostics.unaccountedPointSamples.Add($"point={kvp.Key} branch=hashset-add-failed");
                    }
                    continue;
                }
            }

            List<List<Vector2Int>> cardinalSegments = SplitIntoConnectedDirectionalWideBeachSegments(
                candidatePoints,
                allLandPoints,
                areaByPoint,
                shoreDepthByPoint,
                false);
            diagnostics.componentCountCardinalOnly = cardinalSegments.Count;
            diagnostics.longestCardinalComponentLength = GetLongestDirectionalWideBeachComponentLength(cardinalSegments);

            List<List<Vector2Int>> restrictedDiagonalSegments = SplitIntoConnectedDirectionalWideBeachSegments(
                candidatePoints,
                allLandPoints,
                areaByPoint,
                shoreDepthByPoint,
                true);
            diagnostics.componentCountWithRestrictedDiagonal = restrictedDiagonalSegments.Count;
            diagnostics.longestRestrictedDiagonalComponentLength = GetLongestDirectionalWideBeachComponentLength(restrictedDiagonalSegments);
            diagnostics.rawCandidatePointCount = candidatePoints.Count;
            diagnostics.candidateCountBeforeEnclosedFilter =
                diagnostics.sourcePointCount -
                diagnostics.rejectedDepthNotZero -
                diagnostics.rejectedNotOrdinaryShoreCandidate -
                diagnostics.rejectedExcludedOrUsed -
                diagnostics.rejectedMissingAreaEntry -
                diagnostics.rejectedSelectedDirectionPosition -
                diagnostics.rejectedLegacyDirectionalFilter;
            diagnostics.accountedPointCount = diagnostics.TotalRejectedCount + diagnostics.rawCandidatePointCount;
            diagnostics.unaccountedPointCount = diagnostics.sourcePointCount - diagnostics.accountedPointCount;
            diagnostics.pipelineInvariantValid = diagnostics.unaccountedPointCount == 0;

            return candidatePoints;
        }

        private bool TryBuildBudgetDirectionalSegment(
            ShoreEdgeDirection selectedDirection,
            List<Vector2Int> rawSegmentPoints,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> ordinaryGrassPoints,
            int baseMaxDepth,
            out DirectionalWideBeachSegment segment,
            out string rejectedReason,
            out DirectionalWideBeachRejectedSegmentInfo rejectedInfo)
        {
            segment = default;
            rejectedReason = null;
            rejectedInfo = CreateDirectionalWideBeachRejectedSegmentInfo(rawSegmentPoints, selectedDirection, 0f, 0, 0, 0);
            if (!TryBuildDirectionalWideBeachSegment(
                    selectedDirection,
                    rawSegmentPoints,
                    allLandPoints,
                    areaByPoint,
                    baseMaxDepth,
                    out segment,
                    out rejectedReason,
                    out rejectedInfo))
            {
                return false;
            }

            Vector2Int inwardOffset = GetOppositeCardinalOffset(selectedDirection);
            int minimumDesiredSupport = baseMaxDepth + Mathf.Max(1, directionalWideBeachMinimumDepth);
            int totalSupport = 0;
            int supportedPointCount = 0;

            for (int i = 0; i < segment.orderedPoints.Count; i++)
            {
                int support = CountBudgetDirectionalInlandSupport(
                    segment.orderedPoints[i],
                    inwardOffset,
                    allLandPoints,
                    areaByPoint,
                    ordinaryGrassPoints,
                    baseMaxDepth + directionalWideBeachMaximumDepth);
                totalSupport += support;
                if (support >= minimumDesiredSupport)
                {
                    supportedPointCount++;
                }
            }

            int averageSupport = segment.orderedPoints.Count == 0 ? 0 : Mathf.RoundToInt((float)totalSupport / segment.orderedPoints.Count);
            float supportRatio = segment.orderedPoints.Count == 0 ? 0f : (float)supportedPointCount / segment.orderedPoints.Count;
            if (averageSupport < minimumDesiredSupport || supportRatio < 0.6f)
            {
                rejectedReason = "insufficient-inland-support";
                rejectedInfo = CreateDirectionalWideBeachRejectedSegmentInfo(
                    segment.orderedPoints,
                    selectedDirection,
                    segment.curvatureRatio,
                    averageSupport,
                    0,
                    segment.nearbyEnclosedWaterCount);
                rejectedInfo.reason = rejectedReason;
                return false;
            }

            segment.averageInlandSupport = averageSupport;
            segment.score += (averageSupport * 1.25f) + (supportRatio * 10f);
            return true;
        }

        private void IncrementDirectionalWideBeachRejectedReason(
            ref DirectionalWideBeachCandidateDiagnostics diagnostics,
            string rejectedReason)
        {
            switch (rejectedReason)
            {
                case "too-short":
                    diagnostics.rejectedTooShort++;
                    break;
                case "branch-detected":
                    diagnostics.rejectedBranch++;
                    break;
                case "closed-loop":
                    diagnostics.rejectedClosedLoop++;
                    break;
                case "path-order-failed":
                    diagnostics.rejectedPathOrder++;
                    break;
                case "curvature-too-high":
                    diagnostics.rejectedCurvature++;
                    break;
                case "insufficient-inland-support":
                    diagnostics.rejectedInlandSupport++;
                    break;
                case "near-enclosed-water":
                    diagnostics.rejectedDiagonalOnlyEnclosedWater++;
                    break;
                case "direction-score-too-low":
                    diagnostics.rejectedDirectionalMismatch++;
                    break;
                case "no-exterior-ocean-contact":
                    diagnostics.rejectedNoAnyExteriorOceanContact++;
                    break;
                case "duplicate-or-overlap":
                    diagnostics.rejectedDuplicateOrOverlap++;
                    break;
                default:
                    diagnostics.rejectedUnknown++;
                    break;
            }
        }

        private void TrackRejectedDirectionalWideBeachSegment(
            ref DirectionalWideBeachCandidateDiagnostics diagnostics,
            DirectionalWideBeachRejectedSegmentInfo rejectedInfo)
        {
            if (diagnostics.topRejectedSegments == null)
            {
                diagnostics.topRejectedSegments = new List<DirectionalWideBeachRejectedSegmentInfo>();
            }

            rejectedInfo.reason = string.IsNullOrEmpty(rejectedInfo.reason) ? "unknown" : rejectedInfo.reason;
            diagnostics.topRejectedSegments.Add(rejectedInfo);
            diagnostics.topRejectedSegments.Sort((lhs, rhs) => rhs.length.CompareTo(lhs.length));
            if (diagnostics.topRejectedSegments.Count > 5)
            {
                diagnostics.topRejectedSegments.RemoveRange(5, diagnostics.topRejectedSegments.Count - 5);
            }
        }

        private DirectionalWideBeachRejectedSegmentInfo CreateDirectionalWideBeachRejectedSegmentInfo(
            List<Vector2Int> points,
            ShoreEdgeDirection selectedDirection,
            float curvatureRatio,
            int averageInlandSupport,
            int nearEnclosedWaterCount,
            int branchPointCount)
        {
            return new DirectionalWideBeachRejectedSegmentInfo
            {
                reason = "unknown",
                length = points == null ? 0 : points.Count,
                startPoint = points != null && points.Count > 0 ? points[0] : Vector2Int.zero,
                endPoint = points != null && points.Count > 0 ? points[points.Count - 1] : Vector2Int.zero,
                curvatureRatio = curvatureRatio,
                averageInlandSupport = averageInlandSupport,
                exteriorOceanContactCount = CountDirectionalWideBeachExteriorOceanContacts(points, selectedDirection),
                branchPointCount = branchPointCount,
                nearEnclosedWaterCount = nearEnclosedWaterCount > 0 ? nearEnclosedWaterCount : CountDirectionalWideBeachNearbyEnclosedWater(points)
            };
        }

        private int CountDirectionalWideBeachExteriorOceanContacts(
            List<Vector2Int> points,
            ShoreEdgeDirection selectedDirection)
        {
            if (points == null || points.Count == 0 || currentExteriorOceanPoints == null || currentExteriorOceanPoints.Count == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < points.Count; i++)
            {
                if (IsSpecificWaterAdjacentInDirection(points[i], selectedDirection, currentExteriorOceanPoints))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountDirectionalWideBeachNearbyEnclosedWater(List<Vector2Int> points)
        {
            if (points == null || points.Count == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < points.Count; i++)
            {
                if (HasNearbyEnclosedWater(points[i], 2))
                {
                    count++;
                }
            }

            return count;
        }

        private int GetLongestDirectionalWideBeachComponentLength(List<List<Vector2Int>> components)
        {
            if (components == null || components.Count == 0)
            {
                return 0;
            }

            int longest = 0;
            for (int i = 0; i < components.Count; i++)
            {
                if (components[i] != null && components[i].Count > longest)
                {
                    longest = components[i].Count;
                }
            }

            return longest;
        }

        private bool HasDirectCardinalEnclosedWaterNeighbor(Vector2Int point)
        {
            return currentShoreWaterPoints != null &&
                   currentShoreWaterPoints.Count > 0 &&
                   (IsEnclosedWaterPoint(point + Vector2Int.up) ||
                    IsEnclosedWaterPoint(point + Vector2Int.right) ||
                    IsEnclosedWaterPoint(point + Vector2Int.down) ||
                    IsEnclosedWaterPoint(point + Vector2Int.left));
        }

        private bool HasDiagonalOnlyEnclosedWaterNeighbor(Vector2Int point)
        {
            return currentShoreWaterPoints != null &&
                   currentShoreWaterPoints.Count > 0 &&
                   (IsEnclosedWaterPoint(point + Vector2Int.up + Vector2Int.left) ||
                    IsEnclosedWaterPoint(point + Vector2Int.up + Vector2Int.right) ||
                    IsEnclosedWaterPoint(point + Vector2Int.down + Vector2Int.left) ||
                    IsEnclosedWaterPoint(point + Vector2Int.down + Vector2Int.right));
        }

        private bool IsEnclosedWaterPoint(Vector2Int point)
        {
            return currentShoreWaterPoints != null &&
                   currentShoreWaterPoints.Contains(point) &&
                   (currentExteriorOceanPoints == null || !currentExteriorOceanPoints.Contains(point));
        }

        private bool AreDirectionalBeachShorelinePointsConnected(
            Vector2Int a,
            Vector2Int b,
            HashSet<Vector2Int> candidatePoints,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            Dictionary<Vector2Int, int> shoreDepthByPoint,
            bool allowRestrictedDiagonal)
        {
            if (a == b || candidatePoints == null || !candidatePoints.Contains(a) || !candidatePoints.Contains(b))
            {
                return false;
            }

            int deltaX = Mathf.Abs(a.x - b.x);
            int deltaY = Mathf.Abs(a.y - b.y);
            int chebyshevDistance = Mathf.Max(deltaX, deltaY);
            if (chebyshevDistance != 1)
            {
                return false;
            }

            if ((deltaX + deltaY) == 1)
            {
                return true;
            }

            if (!allowRestrictedDiagonal)
            {
                return false;
            }

            if ((shoreDepthByPoint != null && ((!shoreDepthByPoint.TryGetValue(a, out int aDepth) || aDepth != 0) || (!shoreDepthByPoint.TryGetValue(b, out int bDepth) || bDepth != 0))) ||
                !HasAnyCardinalExteriorOceanNeighbor(a) ||
                !HasAnyCardinalExteriorOceanNeighbor(b))
            {
                return false;
            }

            Vector2Int bridgeA = new Vector2Int(a.x, b.y);
            Vector2Int bridgeB = new Vector2Int(b.x, a.y);
            if (IsEnclosedWaterPoint(bridgeA) || IsEnclosedWaterPoint(bridgeB))
            {
                return false;
            }

            bool bridgeAOrdinaryGrass = IsGrassLandPoint(bridgeA, allLandPoints, areaByPoint) && !candidatePoints.Contains(bridgeA);
            bool bridgeBOrdinaryGrass = IsGrassLandPoint(bridgeB, allLandPoints, areaByPoint) && !candidatePoints.Contains(bridgeB);
            if (bridgeAOrdinaryGrass || bridgeBOrdinaryGrass)
            {
                return false;
            }

            return true;
        }

        private static BoundsInt CalculatePointBounds(HashSet<Vector2Int> points)
        {
            if (points == null || points.Count == 0)
            {
                return new BoundsInt(Vector3Int.zero, Vector3Int.zero);
            }

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;

            foreach (Vector2Int point in points)
            {
                if (point.x < minX) minX = point.x;
                if (point.x > maxX) maxX = point.x;
                if (point.y < minY) minY = point.y;
                if (point.y > maxY) maxY = point.y;
            }

            return new BoundsInt(
                new Vector3Int(minX, minY, 0),
                new Vector3Int((maxX - minX) + 1, (maxY - minY) + 1, 1));
        }

        private bool HasAnyCardinalExteriorOceanNeighbor(Vector2Int point)
        {
            return TouchesSpecificWaterSet(point, currentExteriorOceanPoints);
        }

        private static Vector2 GetDirectionalWideBeachNormal(ShoreEdgeDirection selectedDirection)
        {
            switch (selectedDirection)
            {
                case ShoreEdgeDirection.Up:
                    return Vector2.up;
                case ShoreEdgeDirection.Down:
                    return Vector2.down;
                case ShoreEdgeDirection.Left:
                    return Vector2.left;
                default:
                    return Vector2.right;
            }
        }

        private int CountBudgetDirectionalInlandSupport(
            Vector2Int shorelinePoint,
            Vector2Int inwardOffset,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> ordinaryGrassPoints,
            int maxProbeDepth)
        {
            int support = 0;
            Vector2Int currentPoint = shorelinePoint;
            int baseMaxDepth = Mathf.Max(0, shoreSandWidth - 1);

            for (int step = 0; step <= maxProbeDepth; step++)
            {
                if (step <= baseMaxDepth)
                {
                    if (!IsGrassLandPoint(currentPoint, allLandPoints, areaByPoint))
                    {
                        break;
                    }
                }
                else if (ordinaryGrassPoints == null || !ordinaryGrassPoints.Contains(currentPoint))
                {
                    break;
                }

                support++;
                currentPoint += inwardOffset;
            }

            return support;
        }

        private DirectionalWideBeachBuildResult BuildBudgetDirectionalBeachFromSegment(
            DirectionalWideBeachSegment segment,
            int targetArea,
            int minimumExtraDepth,
            int maximumExtraDepth,
            Dictionary<Vector2Int, int> shoreDepthByPoint,
            Dictionary<Vector2Int, int> localMaximumDepthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> ordinaryGrassPoints,
            HashSet<Vector2Int> directionalBeachPoints,
            int baseMaxDepth,
            bool isMainBeach)
        {
            DirectionalWideBeachBuildResult result = new DirectionalWideBeachBuildResult
            {
                addedPoints = new HashSet<Vector2Int>(),
                shorelinePoints = new HashSet<Vector2Int>(),
                actualArea = 0,
                stoppedReason = "completed",
                achievedDepth = 0
            };

            if (segment.orderedPoints == null ||
                segment.orderedPoints.Count == 0 ||
                targetArea <= 0 ||
                ordinaryGrassPoints == null)
            {
                result.stoppedReason = "invalid-segment-or-zero-budget";
                return result;
            }

            int clampedMaximumExtraDepth = Mathf.Max(1, maximumExtraDepth);
            int clampedMinimumExtraDepth = Mathf.Clamp(minimumExtraDepth, 1, clampedMaximumExtraDepth);
            int depthForLengthEstimation = Mathf.Max(clampedMinimumExtraDepth, Mathf.Min(clampedMaximumExtraDepth, (clampedMinimumExtraDepth + clampedMaximumExtraDepth) / 2));
            int desiredColumnCount = Mathf.Clamp(
                Mathf.CeilToInt((float)targetArea / Mathf.Max(1, depthForLengthEstimation)),
                Mathf.Min(segment.orderedPoints.Count, directionalWideBeachMinimumSegmentLength),
                segment.orderedPoints.Count);
            float halfActiveLength = Mathf.Max(1f, desiredColumnCount * 0.5f);
            Vector2Int inwardOffset = GetOppositeCardinalOffset(segment.selectedDirection);
            List<int> columnOrder = BuildBudgetDirectionalColumnOrder(segment.centerIndex, segment.orderedPoints.Count);

            for (int layer = 1; layer <= clampedMaximumExtraDepth && result.actualArea < targetArea; layer++)
            {
                bool addedAnyThisLayer = false;

                for (int orderIndex = 0; orderIndex < columnOrder.Count && result.actualArea < targetArea; orderIndex++)
                {
                    int columnIndex = columnOrder[orderIndex];
                    float alongDistance = Mathf.Abs(columnIndex - segment.centerIndex);
                    float alongWeight = 1f - Mathf.SmoothStep(
                        halfActiveLength,
                        halfActiveLength + directionalWideBeachFalloffLength,
                        alongDistance);
                    if (alongWeight <= 0f)
                    {
                        continue;
                    }

                    int targetDepthForColumn = Mathf.Clamp(
                        Mathf.RoundToInt(Mathf.Lerp(clampedMinimumExtraDepth, clampedMaximumExtraDepth, alongWeight)),
                        clampedMinimumExtraDepth,
                        clampedMaximumExtraDepth);
                    if (layer > targetDepthForColumn)
                    {
                        continue;
                    }

                    Vector2Int shorelinePoint = segment.orderedPoints[columnIndex];
                    Vector2Int candidatePoint = shorelinePoint + inwardOffset * (baseMaxDepth + layer);
                    if (!TryAddBudgetDirectionalBeachPoint(
                            candidatePoint,
                            shorelinePoint,
                            segment.selectedDirection,
                            layer,
                            targetDepthForColumn,
                            shoreDepthByPoint,
                            localMaximumDepthByPoint,
                            allLandPoints,
                            areaByPoint,
                            ordinaryGrassPoints,
                            directionalBeachPoints,
                            result.addedPoints,
                            result.shorelinePoints,
                            baseMaxDepth,
                            out string rejectionReason))
                    {
                        result.stoppedReason = rejectionReason;
                        continue;
                    }

                    addedAnyThisLayer = true;
                    result.actualArea++;
                    result.achievedDepth = Mathf.Max(result.achievedDepth, layer);
                }

                if (!addedAnyThisLayer)
                {
                    result.stoppedReason = result.actualArea >= targetArea
                        ? "target-area-reached"
                        : "no-more-safe-grass-expansion";
                    break;
                }
            }

            if (result.actualArea >= targetArea)
            {
                result.stoppedReason = "target-area-reached";
            }
            else if (result.achievedDepth >= clampedMaximumExtraDepth)
            {
                result.stoppedReason = "reached-maximum-depth";
            }
            else if (isMainBeach && result.achievedDepth < clampedMinimumExtraDepth)
            {
                result.stoppedReason = "insufficient-safe-depth";
            }

            return result;
        }

        private List<int> BuildBudgetDirectionalColumnOrder(int centerIndex, int count)
        {
            List<int> orderedIndices = new List<int>(count);
            if (count <= 0)
            {
                return orderedIndices;
            }

            orderedIndices.Add(centerIndex);
            for (int offset = 1; orderedIndices.Count < count; offset++)
            {
                int left = centerIndex - offset;
                int right = centerIndex + offset;
                if (left >= 0)
                {
                    orderedIndices.Add(left);
                }

                if (right < count)
                {
                    orderedIndices.Add(right);
                }
            }

            return orderedIndices;
        }

        private bool TryAddBudgetDirectionalBeachPoint(
            Vector2Int candidatePoint,
            Vector2Int shorelinePoint,
            ShoreEdgeDirection selectedDirection,
            int extraLayer,
            int targetDepthForColumn,
            Dictionary<Vector2Int, int> shoreDepthByPoint,
            Dictionary<Vector2Int, int> localMaximumDepthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> ordinaryGrassPoints,
            HashSet<Vector2Int> directionalBeachPoints,
            HashSet<Vector2Int> addedPoints,
            HashSet<Vector2Int> shorelinePoints,
            int baseMaxDepth,
            out string rejectionReason)
        {
            rejectionReason = "no-more-safe-grass-expansion";
            if (!ordinaryGrassPoints.Contains(candidatePoint) ||
                !IsGrassLandPoint(candidatePoint, allLandPoints, areaByPoint))
            {
                return false;
            }

            if (connectorFloorPoints != null && connectorFloorPoints.Contains(candidatePoint))
            {
                rejectionReason = "connector-protected";
                return false;
            }

            if (TouchesEnclosedWater(candidatePoint))
            {
                rejectionReason = "touches-enclosed-water";
                return false;
            }

            Vector2Int inwardOffset = GetOppositeCardinalOffset(selectedDirection);
            Vector2Int seawardPoint = candidatePoint - inwardOffset;
            if (!shoreDepthByPoint.ContainsKey(seawardPoint))
            {
                rejectionReason = "not-orthogonally-connected";
                return false;
            }

            if (WouldDisconnectOrdinaryGrassComponent(candidatePoint, ordinaryGrassPoints))
            {
                rejectionReason = "would-disconnect-grass";
                return false;
            }

            if (WouldCreateUnsupportedGrassAfterRemoval(candidatePoint, ordinaryGrassPoints))
            {
                rejectionReason = "would-create-narrow-grass-corridor";
                return false;
            }

            ordinaryGrassPoints.Remove(candidatePoint);
            directionalBeachPoints.Add(candidatePoint);
            addedPoints.Add(candidatePoint);
            shorelinePoints.Add(shorelinePoint);
            shoreDepthByPoint[candidatePoint] = baseMaxDepth + extraLayer;

            int columnLocalMaximumDepth = baseMaxDepth + extraLayer;
            for (int step = 0; step <= baseMaxDepth + extraLayer; step++)
            {
                Vector2Int columnPoint = shorelinePoint + inwardOffset * step;
                if (!allLandPoints.Contains(columnPoint))
                {
                    break;
                }

                localMaximumDepthByPoint[columnPoint] = Mathf.Max(
                    GetLocalMaximumDepthForPoint(columnPoint, localMaximumDepthByPoint, baseMaxDepth),
                    columnLocalMaximumDepth);
            }

            return true;
        }

        private bool WouldDisconnectOrdinaryGrassComponent(
            Vector2Int pointToRemove,
            HashSet<Vector2Int> ordinaryGrassPoints)
        {
            if (ordinaryGrassPoints == null || !ordinaryGrassPoints.Contains(pointToRemove))
            {
                return false;
            }

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
                Vector2Int neighbor = pointToRemove + offsets[i];
                if (ordinaryGrassPoints.Contains(neighbor))
                {
                    neighbors.Add(neighbor);
                }
            }

            if (neighbors.Count <= 1)
            {
                return false;
            }

            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(neighbors[0]);
            visited.Add(neighbors[0]);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector2Int neighbor = current + offsets[i];
                    if (neighbor == pointToRemove ||
                        !ordinaryGrassPoints.Contains(neighbor) ||
                        !visited.Add(neighbor))
                    {
                        continue;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            for (int i = 1; i < neighbors.Count; i++)
            {
                if (!visited.Contains(neighbors[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool WouldCreateUnsupportedGrassAfterRemoval(
            Vector2Int pointToRemove,
            HashSet<Vector2Int> ordinaryGrassPoints)
        {
            if (ordinaryGrassPoints == null || !ordinaryGrassPoints.Contains(pointToRemove))
            {
                return false;
            }

            HashSet<Vector2Int> simulatedGrass = new HashSet<Vector2Int>(ordinaryGrassPoints);
            simulatedGrass.Remove(pointToRemove);

            Vector2Int[] checks =
            {
                pointToRemove + Vector2Int.up,
                pointToRemove + Vector2Int.right,
                pointToRemove + Vector2Int.down,
                pointToRemove + Vector2Int.left
            };

            for (int i = 0; i < checks.Length; i++)
            {
                if (simulatedGrass.Contains(checks[i]) &&
                    !HasValidTwoDimensionalSupport(checks[i], simulatedGrass, minimumTerrainFeatureWidth))
                {
                    return true;
                }
            }

            return false;
        }

        private void LogDirectionalWideBeachBudgetSummary(
            int batchId,
            string phase,
            int callIndex,
            string selectedMainDirection,
            int baseOrdinaryGrassPointCount,
            int targetBeachArea,
            int mainBeachTargetArea,
            int actualMainBeachArea,
            int secondaryBeachTargetArea,
            int actualSecondaryBeachArea,
            int totalActualDirectionalBeachArea,
            float achievedGrassRatio,
            int selectedMainSegmentLength,
            int selectedSecondaryBeachCount,
            int rejectedCandidateCount,
            string stoppedReason)
        {
            Debug.Log(
                $"[ShoreSand.DirectionalWideBeach] batch={batchId} phase={phase} callIndex={callIndex} selectedMainDirection={selectedMainDirection} baseOrdinaryGrassPointCount={baseOrdinaryGrassPointCount} targetBeachArea={targetBeachArea} mainBeachTargetArea={mainBeachTargetArea} actualMainBeachArea={actualMainBeachArea} secondaryBeachTargetArea={secondaryBeachTargetArea} actualSecondaryBeachArea={actualSecondaryBeachArea} totalActualDirectionalBeachArea={totalActualDirectionalBeachArea} achievedGrassRatio={achievedGrassRatio:F3} selectedMainSegmentLength={selectedMainSegmentLength} selectedSecondaryBeachCount={selectedSecondaryBeachCount} rejectedCandidateCount={rejectedCandidateCount} stoppedReason={stoppedReason}",
                this);
        }

        private void LogDirectionalWideBeachCandidateSummary(DirectionalWideBeachCandidateDiagnostics diagnostics)
        {
            Debug.Log(
                $"[ShoreSand.DirectionalWideBeach.CandidatePipeline] batch={diagnostics.batchId} phase={diagnostics.phase} callIndex={diagnostics.callIndex} sourcePointCount={diagnostics.sourcePointCount} rejectedDepthNotZero={diagnostics.rejectedDepthNotZero} rejectedNotOrdinaryShoreCandidate={diagnostics.rejectedNotOrdinaryShoreCandidate} rejectedNoAnyExteriorOceanContact={diagnostics.rejectedNoAnyExteriorOceanContact} rejectedDirectCardinalEnclosedWater={diagnostics.rejectedDirectCardinalEnclosedWater} rejectedDiagonalOnlyEnclosedWater={diagnostics.rejectedDiagonalOnlyEnclosedWater} rejectedExteriorAndEnclosedConflict={diagnostics.rejectedExteriorAndEnclosedConflict} rejectedSelectedDirectionPosition={diagnostics.rejectedSelectedDirectionPosition} rejectedLegacyDirectionalFilter={diagnostics.rejectedLegacyDirectionalFilter} rejectedExcludedOrUsed={diagnostics.rejectedExcludedOrUsed} rejectedMissingAreaEntry={diagnostics.rejectedMissingAreaEntry} rejectedMissingImmediateOrdinaryGrassSupport={diagnostics.rejectedMissingImmediateOrdinaryGrassSupport} rejectedUnhandledBranch={diagnostics.rejectedUnhandledBranch} acceptedCandidatePointCount={diagnostics.rawCandidatePointCount} accountedPointCount={diagnostics.accountedPointCount} unaccountedPointCount={diagnostics.unaccountedPointCount} pipelineInvariantValid={diagnostics.pipelineInvariantValid}",
                this);

            Debug.Log(
                $"[ShoreSand.DirectionalWideBeach.CandidateSummary] batch={diagnostics.batchId} phase={diagnostics.phase} callIndex={diagnostics.callIndex} selectedDirection={diagnostics.selectedDirection} rawCandidatePointCount={diagnostics.rawCandidatePointCount} connectedComponentCount={diagnostics.connectedComponentCount} acceptedSegmentCount={diagnostics.acceptedSegmentCount} rejectedTooShort={diagnostics.rejectedTooShort} rejectedBranch={diagnostics.rejectedBranch} rejectedClosedLoop={diagnostics.rejectedClosedLoop} rejectedPathOrder={diagnostics.rejectedPathOrder} rejectedCurvature={diagnostics.rejectedCurvature} rejectedInlandSupport={diagnostics.rejectedInlandSupport} rejectedDirectionalMismatch={diagnostics.rejectedDirectionalMismatch} rejectedNoAnyExteriorOceanContact={diagnostics.rejectedNoAnyExteriorOceanContact} rejectedDuplicateOrOverlap={diagnostics.rejectedDuplicateOrOverlap} rejectedOther={diagnostics.rejectedUnknown}",
                this);

            Debug.Log(
                $"[ShoreSand.DirectionalWideBeach.ConnectivitySummary] batch={diagnostics.batchId} phase={diagnostics.phase} callIndex={diagnostics.callIndex} candidateCountBeforeEnclosedFilter={diagnostics.candidateCountBeforeEnclosedFilter} candidateCountBeforeEnclosedFilterMeaning=source-rejectedDepthNotZero-rejectedNotOrdinaryShoreCandidate-rejectedExcludedOrUsed-rejectedMissingAreaEntry-rejectedSelectedDirectionPosition-rejectedLegacyDirectionalFilter rejectedDirectCardinalEnclosedWater={diagnostics.rejectedDirectCardinalEnclosedWater} rejectedDiagonalOnlyEnclosedWater={diagnostics.rejectedDiagonalOnlyEnclosedWater} rejectedExteriorAndEnclosedConflict={diagnostics.rejectedExteriorAndEnclosedConflict} componentCountCardinalOnly={diagnostics.componentCountCardinalOnly} componentCountWithRestrictedDiagonal={diagnostics.componentCountWithRestrictedDiagonal} longestCardinalComponentLength={diagnostics.longestCardinalComponentLength} longestRestrictedDiagonalComponentLength={diagnostics.longestRestrictedDiagonalComponentLength}",
                this);

            if (diagnostics.unaccountedPointSamples != null)
            {
                for (int i = 0; i < diagnostics.unaccountedPointSamples.Count && i < 10; i++)
                {
                    Debug.Log(
                        $"[ShoreSand.DirectionalWideBeach.UnaccountedPoint] batch={diagnostics.batchId} phase={diagnostics.phase} callIndex={diagnostics.callIndex} {diagnostics.unaccountedPointSamples[i]}",
                        this);
                }
            }

            if (diagnostics.topRejectedSegments == null)
            {
                return;
            }

            for (int i = 0; i < diagnostics.topRejectedSegments.Count && i < 5; i++)
            {
                DirectionalWideBeachRejectedSegmentInfo info = diagnostics.topRejectedSegments[i];
                Debug.Log(
                    $"[ShoreSand.DirectionalWideBeach.RejectedSegment] batch={diagnostics.batchId} phase={diagnostics.phase} callIndex={diagnostics.callIndex} reason={info.reason} length={info.length} start={info.startPoint} end={info.endPoint} curvature={info.curvatureRatio:F3} averageInlandSupport={info.averageInlandSupport} exteriorOceanContactCount={info.exteriorOceanContactCount} branchPointCount={info.branchPointCount} nearEnclosedWaterCount={info.nearEnclosedWaterCount}",
                    this);
            }
        }

        private ShoreEdgeDirection GetClockwiseDirection(ShoreEdgeDirection direction)
        {
            switch (direction)
            {
                case ShoreEdgeDirection.Up:
                    return ShoreEdgeDirection.Right;
                case ShoreEdgeDirection.Right:
                    return ShoreEdgeDirection.Down;
                case ShoreEdgeDirection.Down:
                    return ShoreEdgeDirection.Left;
                default:
                    return ShoreEdgeDirection.Up;
            }
        }

        private ShoreEdgeDirection GetCounterClockwiseDirection(ShoreEdgeDirection direction)
        {
            switch (direction)
            {
                case ShoreEdgeDirection.Up:
                    return ShoreEdgeDirection.Left;
                case ShoreEdgeDirection.Left:
                    return ShoreEdgeDirection.Down;
                case ShoreEdgeDirection.Down:
                    return ShoreEdgeDirection.Right;
                default:
                    return ShoreEdgeDirection.Up;
            }
        }

        private int GetLocalMaximumDepthForPoint(
            Vector2Int point,
            Dictionary<Vector2Int, int> localMaximumDepthByPoint,
            int fallbackMaxDepth)
        {
            if (localMaximumDepthByPoint != null &&
                localMaximumDepthByPoint.TryGetValue(point, out int localMaximumDepth))
            {
                return localMaximumDepth;
            }

            return fallbackMaxDepth;
        }

        private void RemoveShortSingleWidthGrassSideShoreSpurs(
            Dictionary<Vector2Int, int> depthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            int maxDepth,
            Dictionary<Vector2Int, int> localMaximumDepthByPoint = null)
        {
            if (depthByPoint == null ||
                depthByPoint.Count == 0 ||
                allLandPoints == null ||
                areaByPoint == null ||
                maxDepth <= 0)
            {
                return;
            }

            List<Vector2Int> orderedMaxDepthPoints = new List<Vector2Int>();
            foreach (KeyValuePair<Vector2Int, int> kvp in depthByPoint)
            {
                int pointLocalMaximumDepth = GetLocalMaximumDepthForPoint(kvp.Key, localMaximumDepthByPoint, maxDepth);
                if (kvp.Value == pointLocalMaximumDepth)
                {
                    orderedMaxDepthPoints.Add(kvp.Key);
                }
            }

            orderedMaxDepthPoints.Sort(ComparePointOrder);

            List<GrassSideSpurCandidate> spurCandidates = new List<GrassSideSpurCandidate>();
            HashSet<Vector2Int> proposedRemovalPoints = new HashSet<Vector2Int>();

            for (int i = 0; i < orderedMaxDepthPoints.Count; i++)
            {
                Vector2Int tipPoint = orderedMaxDepthPoints[i];
                int tipMaxDepth = GetLocalMaximumDepthForPoint(tipPoint, localMaximumDepthByPoint, maxDepth);
                if (proposedRemovalPoints.Contains(tipPoint))
                {
                    continue;
                }

                TryCollectSingleWidthGrassSideSpur(
                    tipPoint,
                    Vector2Int.up,
                    Vector2Int.down,
                    Vector2Int.left,
                    Vector2Int.right,
                    depthByPoint,
                    allLandPoints,
                    areaByPoint,
                    tipMaxDepth,
                    spurCandidates,
                    proposedRemovalPoints);
                TryCollectSingleWidthGrassSideSpur(
                    tipPoint,
                    Vector2Int.down,
                    Vector2Int.up,
                    Vector2Int.left,
                    Vector2Int.right,
                    depthByPoint,
                    allLandPoints,
                    areaByPoint,
                    tipMaxDepth,
                    spurCandidates,
                    proposedRemovalPoints);
                TryCollectSingleWidthGrassSideSpur(
                    tipPoint,
                    Vector2Int.left,
                    Vector2Int.right,
                    Vector2Int.up,
                    Vector2Int.down,
                    depthByPoint,
                    allLandPoints,
                    areaByPoint,
                    tipMaxDepth,
                    spurCandidates,
                    proposedRemovalPoints);
                TryCollectSingleWidthGrassSideSpur(
                    tipPoint,
                    Vector2Int.right,
                    Vector2Int.left,
                    Vector2Int.up,
                    Vector2Int.down,
                    depthByPoint,
                    allLandPoints,
                    areaByPoint,
                    tipMaxDepth,
                    spurCandidates,
                    proposedRemovalPoints);
            }

            HashSet<Vector2Int> removedPoints = new HashSet<Vector2Int>();
            for (int i = 0; i < spurCandidates.Count; i++)
            {
                GrassSideSpurCandidate spurCandidate = spurCandidates[i];
                HashSet<Vector2Int> spurPoints = spurCandidate.branchPoints;
                bool overlapsRemovedPoints = false;
                foreach (Vector2Int spurPoint in spurPoints)
                {
                    if (removedPoints.Contains(spurPoint))
                    {
                        overlapsRemovedPoints = true;
                        break;
                    }
                }

                if (overlapsRemovedPoints)
                {
                    LogGrassSideSpurDecision("Skip", spurCandidate.tipPoint, spurCandidate.basePoint, spurCandidate.axisOffset, spurPoints, "CandidateConflict");
                    continue;
                }

                if (WouldDisconnectMaxDepthSpurBranch(depthByPoint, spurPoints, maxDepth))
                {
                    LogGrassSideSpurDecision("Skip", spurCandidate.tipPoint, spurCandidate.basePoint, spurCandidate.axisOffset, spurPoints, "WouldDisconnectShore");
                    continue;
                }

                foreach (Vector2Int spurPoint in spurPoints)
                {
                    depthByPoint.Remove(spurPoint);
                    removedPoints.Add(spurPoint);
                }

                LogGrassSideSpurDecision("Remove", spurCandidate.tipPoint, spurCandidate.basePoint, spurCandidate.axisOffset, spurPoints, "ShortSingleWidthGrassSideSpur");
            }
        }

        private struct GrassSideSpurCandidate
        {
            public Vector2Int tipPoint;
            public Vector2Int basePoint;
            public Vector2Int axisOffset;
            public HashSet<Vector2Int> branchPoints;

            public GrassSideSpurCandidate(
                Vector2Int tipPoint,
                Vector2Int basePoint,
                Vector2Int axisOffset,
                HashSet<Vector2Int> branchPoints)
            {
                this.tipPoint = tipPoint;
                this.basePoint = basePoint;
                this.axisOffset = axisOffset;
                this.branchPoints = branchPoints;
            }
        }

        private void TryCollectSingleWidthGrassSideSpur(
            Vector2Int tipPoint,
            Vector2Int forwardOffset,
            Vector2Int inwardOffset,
            Vector2Int sideOffsetA,
            Vector2Int sideOffsetB,
            Dictionary<Vector2Int, int> depthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            int maxDepth,
            List<GrassSideSpurCandidate> spurCandidates,
            HashSet<Vector2Int> proposedRemovalPoints)
        {
            if (!TryBuildSingleWidthGrassSideSpur(
                    tipPoint,
                    forwardOffset,
                    inwardOffset,
                    sideOffsetA,
                    sideOffsetB,
                    depthByPoint,
                    allLandPoints,
                    areaByPoint,
                    maxDepth,
                    out HashSet<Vector2Int> spurPoints,
                    out Vector2Int basePoint,
                    out string reason))
            {
                if (!string.IsNullOrEmpty(reason))
                {
                    LogGrassSideSpurDecision("Skip", tipPoint, basePoint, forwardOffset, spurPoints, reason);
                }

                return;
            }

            foreach (Vector2Int spurPoint in spurPoints)
            {
                if (proposedRemovalPoints.Contains(spurPoint))
                {
                    LogGrassSideSpurDecision("Skip", tipPoint, basePoint, forwardOffset, spurPoints, "CandidateConflict");
                    return;
                }
            }

            spurCandidates.Add(new GrassSideSpurCandidate(tipPoint, basePoint, forwardOffset, spurPoints));
            foreach (Vector2Int spurPoint in spurPoints)
            {
                proposedRemovalPoints.Add(spurPoint);
            }
        }

        private bool TryBuildSingleWidthGrassSideSpur(
            Vector2Int tipPoint,
            Vector2Int forwardOffset,
            Vector2Int inwardOffset,
            Vector2Int sideOffsetA,
            Vector2Int sideOffsetB,
            Dictionary<Vector2Int, int> depthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            int maxDepth,
            out HashSet<Vector2Int> spurPoints,
            out Vector2Int basePoint,
            out string reason)
        {
            spurPoints = new HashSet<Vector2Int>();
            basePoint = tipPoint;
            reason = null;

            if (!IsSingleWidthGrassSideSpurPoint(
                    tipPoint,
                    sideOffsetA,
                    sideOffsetB,
                    depthByPoint,
                    allLandPoints,
                    areaByPoint,
                    maxDepth,
                    out string validationReason))
            {
                reason = validationReason;
                return false;
            }

            if (!IsOrdinaryGrassSidePoint(tipPoint + forwardOffset, depthByPoint, allLandPoints, areaByPoint))
            {
                reason = "NotOrdinaryGrassOnBothSides";
                return false;
            }

            Vector2Int currentPoint = tipPoint;
            for (int branchLength = 0; branchLength < 2; branchLength++)
            {
                spurPoints.Add(currentPoint);
                Vector2Int nextPoint = currentPoint + inwardOffset;
                if (IsSingleWidthGrassSideSpurPoint(
                        nextPoint,
                        sideOffsetA,
                        sideOffsetB,
                        depthByPoint,
                        allLandPoints,
                        areaByPoint,
                        maxDepth,
                        out _))
                {
                    currentPoint = nextPoint;
                    continue;
                }

                basePoint = nextPoint;
                break;
            }

            if (spurPoints.Count > 2)
            {
                reason = "LengthTooLong";
                return false;
            }

            if (IsSingleWidthGrassSideSpurPoint(
                    currentPoint + inwardOffset,
                    sideOffsetA,
                    sideOffsetB,
                    depthByPoint,
                    allLandPoints,
                    areaByPoint,
                    maxDepth,
                    out _))
            {
                reason = "LengthTooLong";
                return false;
            }

            if (!IsDepthPoint(basePoint, depthByPoint, maxDepth))
            {
                if (TryBuildSingleMaxDepthTerminalTipWithIntermediateBase(
                        tipPoint,
                        forwardOffset,
                        inwardOffset,
                        sideOffsetA,
                        sideOffsetB,
                        depthByPoint,
                        allLandPoints,
                        areaByPoint,
                        maxDepth,
                        spurPoints,
                        out basePoint))
                {
                    return true;
                }

                if (TryGetDepth(basePoint, depthByPoint, out int baseDepth) && baseDepth != maxDepth)
                {
                    reason = "ContainsIntermediateDepth";
                }
                else
                {
                    reason = "NoBroadBase";
                }

                return false;
            }

            if (!HasBroadMaxDepthBase(basePoint, sideOffsetA, sideOffsetB, depthByPoint, maxDepth))
            {
                reason = "NoBroadBase";
                return false;
            }

            return true;
        }

        private bool TryBuildSingleMaxDepthTerminalTipWithIntermediateBase(
            Vector2Int tipPoint,
            Vector2Int forwardOffset,
            Vector2Int inwardOffset,
            Vector2Int sideOffsetA,
            Vector2Int sideOffsetB,
            Dictionary<Vector2Int, int> depthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            int maxDepth,
            HashSet<Vector2Int> spurPoints,
            out Vector2Int basePoint)
        {
            basePoint = tipPoint + inwardOffset;

            if (spurPoints == null || spurPoints.Count != 1 || maxDepth <= 0)
            {
                return false;
            }

            if (!depthByPoint.TryGetValue(tipPoint, out int tipDepth) ||
                tipDepth != maxDepth ||
                !depthByPoint.TryGetValue(basePoint, out int baseDepth) ||
                baseDepth != maxDepth - 1)
            {
                return false;
            }

            if ((connectorFloorPoints != null && connectorFloorPoints.Contains(tipPoint)) ||
                TouchesSpecificWaterSet(tipPoint, currentShoreWaterPoints))
            {
                return false;
            }

            if (!IsOrdinaryGrassSidePoint(tipPoint + forwardOffset, depthByPoint, allLandPoints, areaByPoint) ||
                !IsOrdinaryGrassSidePoint(tipPoint + sideOffsetA, depthByPoint, allLandPoints, areaByPoint) ||
                !IsOrdinaryGrassSidePoint(tipPoint + sideOffsetB, depthByPoint, allLandPoints, areaByPoint))
            {
                return false;
            }

            int shoreNeighborCount = 0;
            Vector2Int[] cardinalOffsets =
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            for (int i = 0; i < cardinalOffsets.Length; i++)
            {
                Vector2Int neighbor = tipPoint + cardinalOffsets[i];
                if (!depthByPoint.ContainsKey(neighbor))
                {
                    continue;
                }

                shoreNeighborCount++;
                if (neighbor != basePoint)
                {
                    return false;
                }
            }

            if (shoreNeighborCount != 1)
            {
                return false;
            }

            LogTargetedFixDecision(
                tipPoint,
                tipDepth,
                maxDepth,
                "remove-single-max-depth-tip",
                "base-is-maxDepth-minus-one");

            return true;
        }

        private bool IsSingleWidthGrassSideSpurPoint(
            Vector2Int point,
            Vector2Int sideOffsetA,
            Vector2Int sideOffsetB,
            Dictionary<Vector2Int, int> depthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            int maxDepth,
            out string reason)
        {
            reason = null;

            if (!IsDepthPoint(point, depthByPoint, maxDepth))
            {
                if (TryGetDepth(point, depthByPoint, out int depth) && depth != maxDepth)
                {
                    reason = "ContainsIntermediateDepth";
                }

                return false;
            }

            if (TouchesSpecificWaterSet(point, currentShoreWaterPoints))
            {
                reason = "TouchesWater";
                return false;
            }

            if (connectorFloorPoints != null && connectorFloorPoints.Contains(point))
            {
                reason = "WouldDisconnectShore";
                return false;
            }

            if (!IsOrdinaryGrassSidePoint(point + sideOffsetA, depthByPoint, allLandPoints, areaByPoint) ||
                !IsOrdinaryGrassSidePoint(point + sideOffsetB, depthByPoint, allLandPoints, areaByPoint))
            {
                reason = "NotOrdinaryGrassOnBothSides";
                return false;
            }

            return true;
        }

        private bool HasBroadMaxDepthBase(
            Vector2Int basePoint,
            Vector2Int sideOffsetA,
            Vector2Int sideOffsetB,
            Dictionary<Vector2Int, int> depthByPoint,
            int maxDepth)
        {
            if (!IsDepthPoint(basePoint, depthByPoint, maxDepth))
            {
                return false;
            }

            if (IsDepthPoint(basePoint + sideOffsetA, depthByPoint, maxDepth) &&
                IsDepthPoint(basePoint + sideOffsetB, depthByPoint, maxDepth))
            {
                return true;
            }

            return HasStableMaxDepthSquareSupport(basePoint, depthByPoint, maxDepth);
        }

        private static bool HasStableMaxDepthSquareSupport(
            Vector2Int point,
            Dictionary<Vector2Int, int> depthByPoint,
            int maxDepth)
        {
            Vector2Int[] startOffsets =
            {
                Vector2Int.zero,
                Vector2Int.left,
                Vector2Int.down,
                Vector2Int.left + Vector2Int.down
            };

            for (int i = 0; i < startOffsets.Length; i++)
            {
                Vector2Int start = point + startOffsets[i];
                if (IsDepthPoint(start, depthByPoint, maxDepth) &&
                    IsDepthPoint(start + Vector2Int.right, depthByPoint, maxDepth) &&
                    IsDepthPoint(start + Vector2Int.up, depthByPoint, maxDepth) &&
                    IsDepthPoint(start + Vector2Int.right + Vector2Int.up, depthByPoint, maxDepth))
                {
                    return true;
                }
            }

            return false;
        }

        private bool WouldDisconnectMaxDepthSpurBranch(
            Dictionary<Vector2Int, int> depthByPoint,
            HashSet<Vector2Int> branchPoints,
            int maxDepth)
        {
            HashSet<Vector2Int> neighboringMaxDepthPoints = new HashSet<Vector2Int>();
            Vector2Int[] offsets =
            {
                Vector2Int.up,
                Vector2Int.right,
                Vector2Int.down,
                Vector2Int.left
            };

            foreach (Vector2Int branchPoint in branchPoints)
            {
                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector2Int neighbor = branchPoint + offsets[i];
                    if (branchPoints.Contains(neighbor) || !IsDepthPoint(neighbor, depthByPoint, maxDepth))
                    {
                        continue;
                    }

                    neighboringMaxDepthPoints.Add(neighbor);
                }
            }

            if (neighboringMaxDepthPoints.Count <= 1)
            {
                return false;
            }

            List<Vector2Int> orderedNeighboringPoints = new List<Vector2Int>(neighboringMaxDepthPoints);
            orderedNeighboringPoints.Sort(ComparePointOrder);

            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(orderedNeighboringPoints[0]);
            visited.Add(orderedNeighboringPoints[0]);

            while (queue.Count > 0)
            {
                Vector2Int currentPoint = queue.Dequeue();
                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector2Int neighbor = currentPoint + offsets[i];
                    if (visited.Contains(neighbor) ||
                        branchPoints.Contains(neighbor) ||
                        !IsDepthPoint(neighbor, depthByPoint, maxDepth))
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            for (int i = 1; i < orderedNeighboringPoints.Count; i++)
            {
                if (!visited.Contains(orderedNeighboringPoints[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOrdinaryGrassSidePoint(
            Vector2Int point,
            Dictionary<Vector2Int, int> depthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint)
        {
            return !depthByPoint.ContainsKey(point) &&
                   IsGrassLandPoint(point, allLandPoints, areaByPoint);
        }

        private static bool IsDepthPoint(
            Vector2Int point,
            Dictionary<Vector2Int, int> depthByPoint,
            int targetDepth)
        {
            return depthByPoint != null &&
                   depthByPoint.TryGetValue(point, out int depth) &&
                   depth == targetDepth;
        }

        private static bool TryGetDepth(
            Vector2Int point,
            Dictionary<Vector2Int, int> depthByPoint,
            out int depth)
        {
            depth = 0;
            return depthByPoint != null && depthByPoint.TryGetValue(point, out depth);
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

            int baseMaxDepth = Mathf.Max(0, shoreSandWidth - 1);
            for (int i = 0; i < orderedPoints.Count; i++)
            {
                Vector2Int point = orderedPoints[i];
                if (!TryBuildShoreSandPlacementForPoint(
                        point,
                        shoreDepthByPoint,
                        allLandPoints,
                        areaByPoint,
                        finalShoreSandPoints,
                        baseMaxDepth,
                        out ShoreSandPlacement placement))
                {
                    continue;
                }

                placements.Add(placement);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (debugShoreSandPlacements)
                {
                    int placementDepth = shoreDepthByPoint[point];
                    int placementLocalMaximumDepth = GetLocalMaximumDepthForPoint(point, currentLocalMaximumDepthByPoint, baseMaxDepth);
                    Debug.Log(
                        $"[ShoreSand.DepthPlacement] point={point} depth={placementDepth} localMaxDepth={placementLocalMaximumDepth} prefabType={GetShoreSandPlacementDebugType(placement)}",
                        this);
                }
#endif
            }

            return placements;
        }

        private bool TryBuildShoreSandPlacementForPoint(
            Vector2Int point,
            Dictionary<Vector2Int, int> shoreDepthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            HashSet<Vector2Int> finalShoreSandPoints,
            int baseMaxDepth,
            out ShoreSandPlacement placement)
        {
            placement = default;
            if (shoreDepthByPoint == null || !shoreDepthByPoint.TryGetValue(point, out int depth))
            {
                return false;
            }

            int localMaximumDepth = GetLocalMaximumDepthForPoint(point, currentLocalMaximumDepthByPoint, baseMaxDepth);
            if (depth == 0)
            {
                if (!TryGetPreferredCoastalDirection(point, allLandPoints, out ShoreEdgeDirection oceanDirection))
                {
                    return false;
                }

                placement = new ShoreSandPlacement(
                    point,
                    shoreSandOceanTransitionPrefab,
                    oceanDirection,
                    true,
                    true,
                    false);
                return true;
            }

            List<ShoreEdgeDirection> grassNeighborDirections = new List<ShoreEdgeDirection>(4);
            ShoreEdgeDirection singleGrassDirection = ShoreEdgeDirection.Up;
            int grassNeighborCount = 0;
            bool isLocalGrassBoundary = depth >= localMaximumDepth;

            if (isLocalGrassBoundary)
            {
                grassNeighborCount = CountOrdinaryGrassNeighborDirections(
                    point,
                    allLandPoints,
                    areaByPoint,
                    finalShoreSandPoints,
                    out grassNeighborDirections,
                    out singleGrassDirection);
            }

            bool useGrassTransition = isLocalGrassBoundary && grassNeighborCount > 0;
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
                return true;
            }

            placement = new ShoreSandPlacement(
                point,
                shoreSandNormalPrefab,
                ShoreEdgeDirection.Up,
                false,
                false,
                false);
            return true;
        }

        private void EnsureBaseShorePointsIncludedInPlacements(
            List<ShoreSandPlacement> placements,
            Dictionary<Vector2Int, int> shoreDepthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            int baseMaxDepth)
        {
            if (placements == null || currentBaseShorePoints == null || currentBaseShorePoints.Count == 0)
            {
                return;
            }

            HashSet<Vector2Int> finalShorePoints = BuildPlacementPointSet(placements);

            if (currentBaseShoreDepthByPoint != null)
            {
                foreach (KeyValuePair<Vector2Int, int> kvp in currentBaseShoreDepthByPoint)
                {
                    if (!shoreDepthByPoint.ContainsKey(kvp.Key))
                    {
                        shoreDepthByPoint[kvp.Key] = kvp.Value;
                    }
                }
            }

            foreach (Vector2Int basePoint in currentBaseShorePoints)
            {
                if (finalShorePoints.Contains(basePoint))
                {
                    continue;
                }

                if (TryBuildShoreSandPlacementForPoint(
                        basePoint,
                        shoreDepthByPoint,
                        allLandPoints,
                        areaByPoint,
                        finalShorePoints,
                        baseMaxDepth,
                        out ShoreSandPlacement restoredPlacement))
                {
                    placements.Add(restoredPlacement);
                    finalShorePoints.Add(basePoint);
                }
            }

            List<string> missingSamples = new List<string>();
            int missingBaseShoreCount = 0;
            foreach (Vector2Int basePoint in currentBaseShorePoints)
            {
                if (finalShorePoints.Contains(basePoint))
                {
                    continue;
                }

                missingBaseShoreCount++;
                if (missingSamples.Count < 10)
                {
                    missingSamples.Add(basePoint.ToString());
                }
            }

            Debug.Log(
                $"[RandomMap.BaseShoreIntegrity] baseShoreCount={currentBaseShorePoints.Count} finalShoreCount={finalShorePoints.Count} " +
                $"missingBaseShoreCount={missingBaseShoreCount} missingSamplePoints={(missingSamples.Count > 0 ? string.Join(",", missingSamples) : "None")}",
                this);
        }

        private HashSet<Vector2Int> BuildAllowedBeachPointSet()
        {
            HashSet<Vector2Int> allowedPoints = new HashSet<Vector2Int>();
            if (currentBaseShorePoints != null)
            {
                allowedPoints.UnionWith(currentBaseShorePoints);
            }

            if (currentMainBeachPoints != null)
            {
                allowedPoints.UnionWith(currentMainBeachPoints);
            }

            if (currentSecondaryBeachPoints != null)
            {
                allowedPoints.UnionWith(currentSecondaryBeachPoints);
            }

            return allowedPoints;
        }

        private void RestrictPlacementsToAllowedBeachScope(List<ShoreSandPlacement> placements)
        {
            if (placements == null || placements.Count == 0)
            {
                return;
            }

            HashSet<Vector2Int> allowedPoints = BuildAllowedBeachPointSet();
            if (allowedPoints.Count == 0)
            {
                return;
            }

            placements.RemoveAll(placement => !allowedPoints.Contains(placement.point));
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

        private static bool IsSpecificWaterAdjacentInDirection(
            Vector2Int point,
            ShoreEdgeDirection direction,
            HashSet<Vector2Int> waterPoints)
        {
            return waterPoints != null &&
                   waterPoints.Count > 0 &&
                   waterPoints.Contains(point + GetCardinalOffset(direction));
        }

        private static bool IsWithinOceanSearchBounds(Vector2Int point, int minX, int maxX, int minY, int maxY)
        {
            return point.x >= minX && point.x <= maxX &&
                   point.y >= minY && point.y <= maxY;
        }

        private static int CountAdjacentPoints(Vector2Int point, HashSet<Vector2Int> points)
        {
            if (points == null || points.Count == 0)
            {
                return 0;
            }

            int count = 0;
            if (points.Contains(point + Vector2Int.up)) count++;
            if (points.Contains(point + Vector2Int.right)) count++;
            if (points.Contains(point + Vector2Int.down)) count++;
            if (points.Contains(point + Vector2Int.left)) count++;
            return count;
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
                    IsBaseLandAreaType(areaType))
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
                    LogTargetedFixAction(
                        placement.point,
                        "allow-grass-inner-corner",
                        "reverted-overbroad-diagonal-only-guard");
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

        private Dictionary<Vector2Int, ShoreSandClassificationSnapshot> LogSuspiciousShoreClassificationPlacements(
            int batchId,
            string stageName,
            List<ShoreSandPlacement> placements,
            Dictionary<Vector2Int, int> depthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint,
            Dictionary<Vector2Int, ShoreSandClassificationSnapshot> previousSnapshots)
        {
            Dictionary<Vector2Int, ShoreSandClassificationSnapshot> currentSnapshots =
                CaptureShoreSandClassificationSnapshots(placements, depthByPoint, allLandPoints, areaByPoint);

            if (!enableShoreClassificationDebugLogs)
            {
                return currentSnapshots;
            }

            List<Vector2Int> orderedPoints = new List<Vector2Int>(currentSnapshots.Keys);
            orderedPoints.Sort(ComparePointOrder);

            for (int i = 0; i < orderedPoints.Count; i++)
            {
                Vector2Int point = orderedPoints[i];
                ShoreSandClassificationSnapshot currentSnapshot = currentSnapshots[point];
                ShoreSandClassificationSnapshot previousSnapshot = default;

                bool hasPreviousSnapshot =
                    previousSnapshots != null &&
                    previousSnapshots.TryGetValue(point, out previousSnapshot);

                bool changedPrefab = hasPreviousSnapshot &&
                                     currentSnapshot.prefabType != previousSnapshot.prefabType;
                bool changedDirectionOrYaw = hasPreviousSnapshot &&
                                             (currentSnapshot.direction != previousSnapshot.direction ||
                                              currentSnapshot.usesExplicitYaw != previousSnapshot.usesExplicitYaw ||
                                              NormalizeYaw(currentSnapshot.explicitYaw) != NormalizeYaw(previousSnapshot.explicitYaw));

                bool isOppositeGrassPair = IsOppositeGrassPair(currentSnapshot.ordinaryGrassDirections);
                bool currentGrassTransitionSuspicious =
                    currentSnapshot.prefabType == "ShoreSand_GrassTransition" &&
                    currentSnapshot.ordinaryGrassNeighborCount != 1;
                bool hasThreeOrMoreGrassNeighbors = currentSnapshot.ordinaryGrassNeighborCount >= 3;
                bool changedFromNormalToGrassTransition = hasPreviousSnapshot &&
                                                          previousSnapshot.prefabType == "ShoreSand_Normal" &&
                                                          currentSnapshot.prefabType == "ShoreSand_GrassTransition";

                bool shouldLog =
                    currentGrassTransitionSuspicious ||
                    isOppositeGrassPair ||
                    hasThreeOrMoreGrassNeighbors ||
                    changedFromNormalToGrassTransition ||
                    changedDirectionOrYaw;

                if (!shouldLog)
                {
                    continue;
                }

                LogShoreSandClassificationSnapshot(
                    batchId,
                    stageName,
                    currentSnapshot,
                    hasPreviousSnapshot ? (ShoreSandClassificationSnapshot?)previousSnapshot : null,
                    changedPrefab,
                    changedDirectionOrYaw);
            }

            return currentSnapshots;
        }

        private Dictionary<Vector2Int, ShoreSandClassificationSnapshot> CaptureShoreSandClassificationSnapshots(
            List<ShoreSandPlacement> placements,
            Dictionary<Vector2Int, int> depthByPoint,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint)
        {
            Dictionary<Vector2Int, ShoreSandClassificationSnapshot> snapshots =
                new Dictionary<Vector2Int, ShoreSandClassificationSnapshot>();

            if (placements == null || depthByPoint == null || allLandPoints == null || areaByPoint == null)
            {
                return snapshots;
            }

            HashSet<Vector2Int> shorePoints = BuildPlacementPointSet(placements);
            int baseMaxDepth = Mathf.Max(0, shoreSandWidth - 1);

            for (int i = 0; i < placements.Count; i++)
            {
                ShoreSandPlacement placement = placements[i];
                int grassNeighborCount = CountOrdinaryGrassNeighborDirections(
                    placement.point,
                    allLandPoints,
                    areaByPoint,
                    shorePoints,
                    out List<ShoreEdgeDirection> ordinaryGrassDirections,
                    out _);
                List<ShoreEdgeDirection> seaDirections = CollectSeaEdgeDirections(placement.point, allLandPoints);

                snapshots[placement.point] = new ShoreSandClassificationSnapshot
                {
                    point = placement.point,
                    depth = depthByPoint.TryGetValue(placement.point, out int depth) ? depth : -1,
                    maxDepth = GetLocalMaximumDepthForPoint(placement.point, currentLocalMaximumDepthByPoint, baseMaxDepth),
                    prefabType = GetShoreSandPlacementDebugType(placement),
                    ordinaryGrassDirections = ordinaryGrassDirections,
                    ordinaryGrassNeighborCount = grassNeighborCount,
                    seaDirections = seaDirections,
                    direction = placement.direction,
                    usesExplicitYaw = placement.usesExplicitYaw,
                    explicitYaw = placement.usesExplicitYaw ? NormalizeYaw(placement.explicitYaw) : 0f,
                    isConnector = connectorFloorPoints != null && connectorFloorPoints.Contains(placement.point),
                    touchesShoreWater = TouchesSpecificWaterSet(placement.point, currentShoreWaterPoints)
                };
            }

            return snapshots;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogShoreSandClassificationSnapshot(
            int batchId,
            string stageName,
            ShoreSandClassificationSnapshot currentSnapshot,
            ShoreSandClassificationSnapshot? previousSnapshot,
            bool changedPrefab,
            bool changedDirectionOrYaw)
        {
            if (!enableShoreClassificationDebugLogs)
            {
                return;
            }

            string previousPrefabType = previousSnapshot.HasValue
                ? previousSnapshot.Value.prefabType
                : "None";
            string previousDirection = previousSnapshot.HasValue
                ? previousSnapshot.Value.direction.ToString()
                : "None";
            string previousExplicitYaw = previousSnapshot.HasValue && previousSnapshot.Value.usesExplicitYaw
                ? NormalizeYaw(previousSnapshot.Value.explicitYaw).ToString("F1")
                : "N/A";
            string currentExplicitYaw = currentSnapshot.usesExplicitYaw
                ? NormalizeYaw(currentSnapshot.explicitYaw).ToString("F1")
                : "N/A";

            Debug.Log(
                $"[ShoreSand.ClassificationDebug] batch={batchId} stage={stageName} point={currentSnapshot.point} depth={currentSnapshot.depth} maxDepth={currentSnapshot.maxDepth} prefab={currentSnapshot.prefabType} previousPrefab={previousPrefabType} ordinaryGrassDirs={FormatDirectionList(currentSnapshot.ordinaryGrassDirections)} grassNeighborCount={currentSnapshot.ordinaryGrassNeighborCount} seaDirs={FormatDirectionList(currentSnapshot.seaDirections)} direction={currentSnapshot.direction} previousDirection={previousDirection} explicitYaw={currentExplicitYaw} previousExplicitYaw={previousExplicitYaw} isConnector={currentSnapshot.isConnector} touchesShoreWater={currentSnapshot.touchesShoreWater} changedPrefab={changedPrefab} changedDirectionOrYaw={changedDirectionOrYaw}",
                this);
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
                             IsBaseLandAreaType(areaType);
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
                   IsBaseLandAreaType(areaType);
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
                   IsBaseLandAreaType(areaType);
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

        private static bool IsBaseLandAreaType(AreaType areaType)
        {
            return areaType == AreaType.Grass ||
                   areaType == AreaType.Forest ||
                   areaType == AreaType.Rock;
        }

        private static bool IsGrassLandPoint(
            Vector2Int point,
            HashSet<Vector2Int> allLandPoints,
            Dictionary<Vector2Int, AreaType> areaByPoint)
        {
            return allLandPoints.Contains(point) &&
                   areaByPoint.TryGetValue(point, out AreaType areaType) &&
                   IsBaseLandAreaType(areaType);
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

        [ContextMenu("Clear Generated Map")]
        public void ClearGeneratedMap()
        {
            int generationId = currentGenerateMapDebugId;
            int shoreSandChildCount = CountChildren(shoreSandParent != null ? shoreSandParent : transform.Find(GeneratedShoreSandRootName));
            int propsChildCount = CountChildren(FindChildRecursive(paintProp != null ? paintProp.transform : null, GeneratedPropsRootName));
            int wallColliderChildCount = CountChildren(FindChildRecursive(paintTilemap != null ? paintTilemap.transform : null, GeneratedWallColliderRootName));
            int deletedRuntimePlayerCount = 0;
            bool deletedGeneratedShoreSandRoot = false;

            if (Application.isPlaying)
            {
                PlayerSpawnManager playerSpawnManager = FindObjectOfType<PlayerSpawnManager>();
                if (playerSpawnManager != null)
                {
                    deletedRuntimePlayerCount = playerSpawnManager.ClearSpawnedPlayers();
                }
            }

            if (paintTilemap != null)
            {
                paintTilemap.InitClearTile();
            }

            if (paintProp != null)
            {
                paintProp.InitClearProp();
            }

            deletedGeneratedShoreSandRoot = ClearGeneratedShoreSandInstances();

            LogMapDataMutation(nameof(floorPoints), GetTotalPointCount(floorPoints), 0, nameof(ClearGeneratedMap));
            floorPoints = null;
            LogMapDataMutation(nameof(propsPoints), GetTotalPointCount(propsPoints), 0, nameof(ClearGeneratedMap));
            propsPoints = null;
            LogMapDataMutation(nameof(wallColliderPoints), wallColliderPoints != null ? wallColliderPoints.Count : 0, 0, nameof(ClearGeneratedMap));
            wallColliderPoints = null;
            LogMapDataMutation(nameof(generatedShoreSandPoints), generatedShoreSandPoints != null ? generatedShoreSandPoints.Count : 0, 0, nameof(ClearGeneratedMap));
            generatedShoreSandPoints = null;
            LogMapDataMutation(nameof(connectorFloorPoints), connectorFloorPoints != null ? connectorFloorPoints.Count : 0, 0, nameof(ClearGeneratedMap));
            connectorFloorPoints = null;
            LogMapDataMutation(nameof(currentExteriorOceanPoints), currentExteriorOceanPoints != null ? currentExteriorOceanPoints.Count : 0, 0, nameof(ClearGeneratedMap));
            currentExteriorOceanPoints = null;
            LogMapDataMutation(nameof(currentShoreWaterPoints), currentShoreWaterPoints != null ? currentShoreWaterPoints.Count : 0, 0, nameof(ClearGeneratedMap));
            currentShoreWaterPoints = null;
            LogMapDataMutation(nameof(currentFinalWalkablePoints), currentFinalWalkablePoints != null ? currentFinalWalkablePoints.Count : 0, 0, nameof(ClearGeneratedMap));
            currentFinalWalkablePoints = null;
            currentShoreLandBounds = null;
            currentLocalMaximumDepthByPoint = null;
            currentDirectionalWideBeachDirections = null;
            currentDirectionalWideBeachBatchId = 0;
            currentDirectionalWideBeachCallIndex = 0;
            currentEnclosedWaterPointCount = 0;
            shoreClassificationDebugBatchCounter = 0;
            activeRegionLayouts = null;
            activeRegionColumns = 0;
            activeRegionRows = 0;
            player = null;
            playerSpawnedGenerationId = -1;
            shoreGenerationInvocationCount = 0;
            hasCompletedFirstShoreGeneration = false;
            diagnosticKnownShorePoints.Clear();

            int deletedGeneratedObjectCount = shoreSandChildCount + propsChildCount + wallColliderChildCount + (deletedGeneratedShoreSandRoot ? 1 : 0);
            Debug.Log(
                $"[RandomMap.Clear] generationId={generationId} isPlaying={Application.isPlaying} " +
                $"deletedGeneratedObjectCount={deletedGeneratedObjectCount} " +
                $"shoreSandDeleted={shoreSandChildCount} propsDeleted={propsChildCount} wallCollidersDeleted={wallColliderChildCount} " +
                $"deletedGeneratedShoreSandRoot={deletedGeneratedShoreSandRoot} deletedRuntimePlayerCount={deletedRuntimePlayerCount}",
                this);
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

        private bool ClearGeneratedShoreSandInstances()
        {
            Transform parent = shoreSandParent != null ? shoreSandParent : transform.Find(GeneratedShoreSandRootName);
            if (parent == null)
            {
                return false;
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

            bool shouldDestroyParent = parent.name == GeneratedShoreSandRootName && parent.parent == transform;
            if (shouldDestroyParent)
            {
                if (Application.isPlaying)
                {
                    Destroy(parent.gameObject);
                }
                else
                {
                    DestroyImmediate(parent.gameObject);
                }

                if (shoreSandParent == parent)
                {
                    shoreSandParent = null;
                }
            }

            return shouldDestroyParent;
        }

        private bool IsGenerationStillCurrent(int generationId)
        {
            return currentGenerateMapDebugId == generationId;
        }

        private static int CountChildren(Transform parent)
        {
            return parent != null ? parent.childCount : 0;
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName))
            {
                return null;
            }

            if (root.name == targetName)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindChildRecursive(root.GetChild(i), targetName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
        #endregion
    }
}
