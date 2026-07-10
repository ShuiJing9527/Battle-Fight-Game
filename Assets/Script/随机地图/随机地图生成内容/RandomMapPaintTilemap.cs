using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace UnderTheStars.GenerationMap
{
    public class RandomMapPaintTilemap : MonoBehaviour
    {
        private const int MaxLoggedOverlapSamples = 10;

        [Header("Map Tiles")]
        [SerializeField] private TileBase[] floorTile;
        [SerializeField] private TileBase wallColliderTile;

        [Header("Tilemaps")]
        [SerializeField] private Tilemap[] floorTilemap;
        [SerializeField] private Tilemap wallColliderTilemap;

        [Header("Wall Collider")]
        [SerializeField] private float wallColliderHeight = 2f;
        [SerializeField] private string wallColliderRootName = "Merged Wall Colliders";
        [SerializeField] private bool debugOceanWallColliders = false;

        private Transform wallColliderRoot;
        private readonly List<RectInt> builtWallColliderRects = new List<RectInt>();
        private readonly List<RectInt> builtHorizontalSegments = new List<RectInt>();
        private readonly List<RectInt> builtVerticalSegments = new List<RectInt>();
        private readonly List<string> overlapLogSamples = new List<string>();
        private HashSet<Vector2Int> debugWallPoints = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> debugFinalWalkablePoints = new HashSet<Vector2Int>();
        private HashSet<Vector2Int> debugGeneratedShorePoints = new HashSet<Vector2Int>();

        private struct WallColliderClearStats
        {
            public int oldColliderCount;
            public int deletedColliderCount;
        }

        private struct WallColliderBuildStats
        {
            public int newColliderCount;
            public int mergedHorizontalSegmentCount;
            public int mergedVerticalSegmentCount;
            public int rejectedOversizedColliderCount;
            public int colliderOverlapWithWalkableCount;
            public Vector2 maximumColliderSize;
        }

        internal void InitClearTile()
        {
            foreach (var tile in floorTilemap)
            {
                tile.ClearAllTiles();
            }

            wallColliderTilemap.ClearAllTiles();
            ClearWallCollidersComplete();
        }

        public Tilemap GetFloorTilemap(int index)
        {
            if (floorTilemap != null && index < floorTilemap.Length)
            {
                return floorTilemap[index];
            }

            return null;
        }

        public void ClearFloorTileCell(int tilemapIndex, Vector2Int point)
        {
            if (floorTilemap == null || tilemapIndex < 0 || tilemapIndex >= floorTilemap.Length)
            {
                return;
            }

            Tilemap tilemap = floorTilemap[tilemapIndex];
            if (tilemap == null)
            {
                return;
            }

            tilemap.SetTile(new Vector3Int(point.x, point.y, 0), null);
        }

        private async UniTask PaintTile(HashSet<Vector2Int> points, Tilemap tilemap, TileBase tile)
        {
            int count = 0;
            foreach (var point in points)
            {
                Vector3Int tilePoint = new Vector3Int(point.x, point.y, 0);
                tilemap.SetTile(tilePoint, tile);

                count++;
                if (count >= 500)
                {
                    count = 0;
                    await UniTask.NextFrame();
                }
            }
        }

        public UniTask PaintFloorTile(HashSet<Vector2Int> points, int tileIndex)
        {
            if (points == null || floorTilemap == null || floorTile == null)
            {
                return UniTask.CompletedTask;
            }

            if (tileIndex < 0 || tileIndex >= floorTilemap.Length || tileIndex >= floorTile.Length)
            {
                Debug.LogWarning($"[RandomMapPaintTilemap] Invalid tileIndex={tileIndex}. floorTilemapLength={(floorTilemap != null ? floorTilemap.Length : 0)} floorTileLength={(floorTile != null ? floorTile.Length : 0)}", this);
                return UniTask.CompletedTask;
            }

            if (floorTilemap[tileIndex] == null || floorTile[tileIndex] == null)
            {
                Debug.LogWarning($"[RandomMapPaintTilemap] Missing tilemap or tile asset at index {tileIndex}.", this);
                return UniTask.CompletedTask;
            }

            return PaintTile(points, floorTilemap[tileIndex], floorTile[tileIndex]);
        }

        public UniTask PaintFloorTile(HashSet<Vector2Int> points, int tilemapIndex, int tileAssetIndex)
        {
            if (points == null || floorTilemap == null || floorTile == null)
            {
                return UniTask.CompletedTask;
            }

            if (tilemapIndex < 0 || tilemapIndex >= floorTilemap.Length)
            {
                Debug.LogWarning($"[RandomMapPaintTilemap] Invalid tilemapIndex={tilemapIndex}. floorTilemapLength={(floorTilemap != null ? floorTilemap.Length : 0)}", this);
                return UniTask.CompletedTask;
            }

            if (tileAssetIndex < 0 || tileAssetIndex >= floorTile.Length)
            {
                Debug.LogWarning($"[RandomMapPaintTilemap] Invalid tileAssetIndex={tileAssetIndex}. floorTileLength={(floorTile != null ? floorTile.Length : 0)}", this);
                return UniTask.CompletedTask;
            }

            if (floorTilemap[tilemapIndex] == null || floorTile[tileAssetIndex] == null)
            {
                Debug.LogWarning($"[RandomMapPaintTilemap] Missing tilemap or tile asset at tilemapIndex={tilemapIndex}, tileAssetIndex={tileAssetIndex}.", this);
                return UniTask.CompletedTask;
            }

            return PaintTile(points, floorTilemap[tilemapIndex], floorTile[tileAssetIndex]);
        }

        public UniTask PaintWallTile(
            HashSet<Vector2Int> points,
            HashSet<Vector2Int> finalWalkablePoints = null,
            HashSet<Vector2Int> generatedShoreSandPoints = null)
        {
            return PaintWallTileAsync(points, finalWalkablePoints, generatedShoreSandPoints);
        }

        private async UniTask PaintWallTileAsync(
            HashSet<Vector2Int> points,
            HashSet<Vector2Int> finalWalkablePoints,
            HashSet<Vector2Int> generatedShoreSandPoints)
        {
            WallColliderClearStats clearStats = ClearWallCollidersComplete();

            debugWallPoints = points != null ? new HashSet<Vector2Int>(points) : new HashSet<Vector2Int>();
            debugFinalWalkablePoints = finalWalkablePoints != null ? new HashSet<Vector2Int>(finalWalkablePoints) : new HashSet<Vector2Int>();
            debugGeneratedShorePoints = generatedShoreSandPoints != null ? new HashSet<Vector2Int>(generatedShoreSandPoints) : new HashSet<Vector2Int>();

            if (points == null || points.Count == 0)
            {
                Debug.Log(
                    $"[RandomMap.OceanWallCollider] wallPointCount=0 oldColliderCount={clearStats.oldColliderCount} " +
                    $"deletedColliderCount={clearStats.deletedColliderCount} newColliderCount=0 mergedHorizontalSegmentCount=0 " +
                    $"mergedVerticalSegmentCount=0 rejectedOversizedColliderCount=0 colliderOverlapWithWalkableCount=0 maximumColliderSize=(0.0,0.0)",
                    this);
                return;
            }

            await PaintTile(points, wallColliderTilemap, wallColliderTile);
            WallColliderBuildStats buildStats = RebuildWallColliders(points, finalWalkablePoints, generatedShoreSandPoints);

            Debug.Log(
                $"[RandomMap.OceanWallCollider] wallPointCount={points.Count} oldColliderCount={clearStats.oldColliderCount} " +
                $"deletedColliderCount={clearStats.deletedColliderCount} newColliderCount={buildStats.newColliderCount} " +
                $"mergedHorizontalSegmentCount={buildStats.mergedHorizontalSegmentCount} " +
                $"mergedVerticalSegmentCount={buildStats.mergedVerticalSegmentCount} " +
                $"rejectedOversizedColliderCount={buildStats.rejectedOversizedColliderCount} " +
                $"colliderOverlapWithWalkableCount={buildStats.colliderOverlapWithWalkableCount} " +
                $"maximumColliderSize=({buildStats.maximumColliderSize.x:F1},{buildStats.maximumColliderSize.y:F1})",
                this);
        }

        private WallColliderBuildStats RebuildWallColliders(
            HashSet<Vector2Int> points,
            HashSet<Vector2Int> finalWalkablePoints,
            HashSet<Vector2Int> generatedShoreSandPoints)
        {
            WallColliderBuildStats stats = new WallColliderBuildStats
            {
                maximumColliderSize = Vector2.zero
            };

            if (points == null || points.Count == 0)
            {
                return stats;
            }

            wallColliderRoot = new GameObject(wallColliderRootName).transform;
            wallColliderRoot.SetParent(wallColliderTilemap.transform, false);

            List<RectInt> colliderRects = BuildWallColliderRects(points, ref stats);

            for (int i = 0; i < colliderRects.Count; i++)
            {
                RectInt rect = colliderRects[i];
                if (TryFindOverlapPoint(rect, finalWalkablePoints, generatedShoreSandPoints, out Vector2Int overlapPoint))
                {
                    stats.rejectedOversizedColliderCount++;
                    stats.colliderOverlapWithWalkableCount++;
                    LogColliderOverlap("split-or-reject", rect, overlapPoint);

                    for (int x = rect.xMin; x < rect.xMax; x++)
                    {
                        for (int y = rect.yMin; y < rect.yMax; y++)
                        {
                            RectInt cellRect = new RectInt(x, y, 1, 1);
                            if (TryFindOverlapPoint(cellRect, finalWalkablePoints, generatedShoreSandPoints, out Vector2Int cellOverlapPoint))
                            {
                                stats.colliderOverlapWithWalkableCount++;
                                LogColliderOverlap("split-or-reject", cellRect, cellOverlapPoint);
                                continue;
                            }

                            CreateWallCollider(cellRect);
                            stats.newColliderCount++;
                            UpdateMaximumColliderSize(ref stats.maximumColliderSize, cellRect);
                        }
                    }

                    continue;
                }

                CreateWallCollider(rect);
                stats.newColliderCount++;
                UpdateMaximumColliderSize(ref stats.maximumColliderSize, rect);
            }

            return stats;
        }

        private WallColliderClearStats ClearWallCollidersComplete()
        {
            WallColliderClearStats stats = new WallColliderClearStats();
            overlapLogSamples.Clear();
            builtWallColliderRects.Clear();
            builtHorizontalSegments.Clear();
            builtVerticalSegments.Clear();
            debugWallPoints.Clear();
            debugFinalWalkablePoints.Clear();
            debugGeneratedShorePoints.Clear();

            if (wallColliderRoot == null)
            {
                Transform existingRoot = wallColliderTilemap.transform.Find(wallColliderRootName);
                if (existingRoot != null)
                {
                    wallColliderRoot = existingRoot;
                }
            }

            if (wallColliderTilemap != null)
            {
                wallColliderTilemap.ClearAllTiles();
            }

            HashSet<Component> colliderComponents = new HashSet<Component>();
            if (wallColliderTilemap != null)
            {
                AddComponents(colliderComponents, wallColliderTilemap.GetComponentsInChildren<Collider>(true));
                AddComponents(colliderComponents, wallColliderTilemap.GetComponentsInChildren<Collider2D>(true));
                AddComponents(colliderComponents, wallColliderTilemap.GetComponentsInChildren<TilemapCollider2D>(true));
                AddComponents(colliderComponents, wallColliderTilemap.GetComponentsInChildren<CompositeCollider2D>(true));
            }

            HashSet<GameObject> colliderObjects = new HashSet<GameObject>();
            foreach (Component component in colliderComponents)
            {
                if (component == null)
                {
                    continue;
                }

                colliderObjects.Add(component.gameObject);
            }

            stats.oldColliderCount = colliderObjects.Count;

            foreach (Component component in colliderComponents)
            {
                DestroyObject(component);
            }

            if (wallColliderRoot == null)
            {
                return stats;
            }

            for (int i = wallColliderRoot.childCount - 1; i >= 0; i--)
            {
                DestroyObject(wallColliderRoot.GetChild(i).gameObject);
            }

            DestroyObject(wallColliderRoot.gameObject);
            wallColliderRoot = null;
            stats.deletedColliderCount = stats.oldColliderCount;
            return stats;
        }

        private List<RectInt> BuildWallColliderRects(HashSet<Vector2Int> points, ref WallColliderBuildStats stats)
        {
            List<RectInt> rects = new List<RectInt>();
            HashSet<Vector2Int> used = new HashSet<Vector2Int>();

            Dictionary<int, List<int>> xsByRow = BuildSortedCoordinateMap(points, groupByRow: true);
            foreach (KeyValuePair<int, List<int>> entry in xsByRow)
            {
                int y = entry.Key;
                List<int> xs = entry.Value;
                int runStart = xs[0];
                int runEnd = xs[0];
                for (int i = 1; i <= xs.Count; i++)
                {
                    bool extendRun = i < xs.Count && xs[i] == runEnd + 1;
                    if (extendRun)
                    {
                        runEnd = xs[i];
                        continue;
                    }

                    int width = (runEnd - runStart) + 1;
                    if (width > 1)
                    {
                        RectInt rect = new RectInt(runStart, y, width, 1);
                        rects.Add(rect);
                        builtWallColliderRects.Add(rect);
                        builtHorizontalSegments.Add(rect);
                        stats.mergedHorizontalSegmentCount++;
                        MarkRectPointsUsed(rect, used);
                    }

                    if (i < xs.Count)
                    {
                        runStart = xs[i];
                        runEnd = xs[i];
                    }
                }
            }

            Dictionary<int, List<int>> ysByColumn = BuildSortedCoordinateMap(points, groupByRow: false);
            foreach (KeyValuePair<int, List<int>> entry in ysByColumn)
            {
                int x = entry.Key;
                List<int> ys = entry.Value;
                int index = 0;
                while (index < ys.Count)
                {
                    Vector2Int startPoint = new Vector2Int(x, ys[index]);
                    if (used.Contains(startPoint))
                    {
                        index++;
                        continue;
                    }

                    int runStart = ys[index];
                    int runEnd = ys[index];
                    index++;

                    while (index < ys.Count)
                    {
                        Vector2Int nextPoint = new Vector2Int(x, ys[index]);
                        if (used.Contains(nextPoint) || ys[index] != runEnd + 1)
                        {
                            break;
                        }

                        runEnd = ys[index];
                        index++;
                    }

                    int height = (runEnd - runStart) + 1;
                    if (height > 1)
                    {
                        RectInt rect = new RectInt(x, runStart, 1, height);
                        rects.Add(rect);
                        builtWallColliderRects.Add(rect);
                        builtVerticalSegments.Add(rect);
                        stats.mergedVerticalSegmentCount++;
                        MarkRectPointsUsed(rect, used);
                    }
                }
            }

            foreach (Vector2Int point in points)
            {
                if (used.Contains(point))
                {
                    continue;
                }

                RectInt rect = new RectInt(point.x, point.y, 1, 1);
                rects.Add(rect);
                builtWallColliderRects.Add(rect);
            }

            return rects;
        }

        private static Dictionary<int, List<int>> BuildSortedCoordinateMap(HashSet<Vector2Int> points, bool groupByRow)
        {
            Dictionary<int, List<int>> map = new Dictionary<int, List<int>>();
            foreach (Vector2Int point in points)
            {
                int key = groupByRow ? point.y : point.x;
                int value = groupByRow ? point.x : point.y;
                if (!map.TryGetValue(key, out List<int> values))
                {
                    values = new List<int>();
                    map.Add(key, values);
                }

                values.Add(value);
            }

            foreach (List<int> values in map.Values)
            {
                values.Sort();
            }

            return map;
        }

        private void CreateWallCollider(RectInt rect)
        {
            GameObject colliderObject = new GameObject($"Wall Collider {rect.x},{rect.y} {rect.width}x{rect.height}");
            colliderObject.layer = wallColliderTilemap.gameObject.layer;
            colliderObject.transform.SetParent(wallColliderRoot, false);

            colliderObject.transform.localPosition = wallColliderTilemap.CellToLocalInterpolated(
                new Vector3(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f, 0f));

            Vector3 cellSize = wallColliderTilemap.layoutGrid != null
                ? wallColliderTilemap.layoutGrid.cellSize
                : Vector3.one;

            BoxCollider boxCollider = colliderObject.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(
                Mathf.Abs(cellSize.x) * rect.width,
                Mathf.Abs(cellSize.y) * rect.height,
                wallColliderHeight);
            boxCollider.center = Vector3.zero;
        }

        private static void MarkRectPointsUsed(RectInt rect, HashSet<Vector2Int> used)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                for (int y = rect.yMin; y < rect.yMax; y++)
                {
                    used.Add(new Vector2Int(x, y));
                }
            }
        }

        private bool TryFindOverlapPoint(
            RectInt rect,
            HashSet<Vector2Int> finalWalkablePoints,
            HashSet<Vector2Int> generatedShoreSandPoints,
            out Vector2Int overlapPoint)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                for (int y = rect.yMin; y < rect.yMax; y++)
                {
                    Vector2Int point = new Vector2Int(x, y);
                    if ((finalWalkablePoints != null && finalWalkablePoints.Contains(point)) ||
                        (generatedShoreSandPoints != null && generatedShoreSandPoints.Contains(point)))
                    {
                        overlapPoint = point;
                        return true;
                    }
                }
            }

            overlapPoint = Vector2Int.zero;
            return false;
        }

        private void LogColliderOverlap(string action, RectInt rect, Vector2Int overlapPoint)
        {
            if (overlapLogSamples.Count >= MaxLoggedOverlapSamples)
            {
                return;
            }

            string sample =
                $"[RandomMap.OceanWallCollider] action={action} reason=collider-overlaps-final-walkable " +
                $"bounds=({rect.xMin},{rect.yMin},{rect.width},{rect.height}) overlapPoint={overlapPoint}";
            overlapLogSamples.Add(sample);
            Debug.LogWarning(sample, this);
        }

        private static void UpdateMaximumColliderSize(ref Vector2 maximumColliderSize, RectInt rect)
        {
            maximumColliderSize.x = Mathf.Max(maximumColliderSize.x, rect.width);
            maximumColliderSize.y = Mathf.Max(maximumColliderSize.y, rect.height);
        }

        private void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!debugOceanWallColliders || wallColliderTilemap == null)
            {
                return;
            }

            DrawPointSet(debugWallPoints, new Color(1f, 0.35f, 0.35f, 0.6f));
            DrawPointSet(debugFinalWalkablePoints, new Color(0.35f, 1f, 0.4f, 0.25f));
            DrawPointSet(debugGeneratedShorePoints, new Color(1f, 0.85f, 0.2f, 0.6f));
            DrawColliderRects(builtWallColliderRects, new Color(0.2f, 0.85f, 1f, 0.9f));
        }

        private static void AddComponents<T>(HashSet<Component> target, T[] components) where T : Component
        {
            if (components == null)
            {
                return;
            }

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null)
                {
                    target.Add(components[i]);
                }
            }
        }

        private void DrawPointSet(HashSet<Vector2Int> points, Color color)
        {
            if (points == null || points.Count == 0)
            {
                return;
            }

            Grid grid = wallColliderTilemap.layoutGrid;
            Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;
            Gizmos.color = color;

            foreach (Vector2Int point in points)
            {
                Vector3 center = wallColliderTilemap.GetCellCenterWorld(new Vector3Int(point.x, point.y, 0));
                Gizmos.DrawWireCube(center, new Vector3(Mathf.Abs(cellSize.x), 0.05f, Mathf.Abs(cellSize.y)));
            }
        }

        private void DrawColliderRects(List<RectInt> rects, Color color)
        {
            if (rects == null || rects.Count == 0)
            {
                return;
            }

            Grid grid = wallColliderTilemap.layoutGrid;
            Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;
            Gizmos.color = color;

            for (int i = 0; i < rects.Count; i++)
            {
                RectInt rect = rects[i];
                Vector3 minCenter = wallColliderTilemap.GetCellCenterWorld(new Vector3Int(rect.xMin, rect.yMin, 0));
                Vector3 maxCenter = wallColliderTilemap.GetCellCenterWorld(new Vector3Int(rect.xMax - 1, rect.yMax - 1, 0));
                Vector3 center = (minCenter + maxCenter) * 0.5f;
                Vector3 size = new Vector3(
                    Mathf.Abs(cellSize.x) * rect.width,
                    wallColliderHeight,
                    Mathf.Abs(cellSize.y) * rect.height);
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}
