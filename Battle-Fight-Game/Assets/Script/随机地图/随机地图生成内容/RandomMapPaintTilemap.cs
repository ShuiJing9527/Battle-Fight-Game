using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace UnderTheStars.GenerationMap
{
    public class RandomMapPaintTilemap : MonoBehaviour
    {
        [Header("Map Tiles")]
        [SerializeField] private TileBase[] floorTile;
        [SerializeField] private TileBase wallColliderTile;

        [Header("Tilemaps")]
        [SerializeField] private Tilemap[] floorTilemap;
        [SerializeField] private Tilemap wallColliderTilemap;

        [Header("Wall Collider")]
        [SerializeField] private float wallColliderHeight = 2f;
        [SerializeField] private string wallColliderRootName = "Merged Wall Colliders";

        private Transform wallColliderRoot;

        internal void InitClearTile()
        {
            foreach (var tile in floorTilemap)
            {
                tile.ClearAllTiles();
            }

            wallColliderTilemap.ClearAllTiles();
            ClearWallColliders();
        }

        public Tilemap GetFloorTilemap(int index)
        {
            if (floorTilemap != null && index < floorTilemap.Length)
            {
                return floorTilemap[index];
            }

            return null;
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
            return PaintTile(points, floorTilemap[tileIndex], floorTile[tileIndex]);
        }

        public UniTask PaintWallTile(HashSet<Vector2Int> points)
        {
            return PaintWallTileAsync(points);
        }

        private async UniTask PaintWallTileAsync(HashSet<Vector2Int> points)
        {
            await PaintTile(points, wallColliderTilemap, wallColliderTile);
            RebuildWallColliders(points);
        }

        private void RebuildWallColliders(HashSet<Vector2Int> points)
        {
            ClearWallColliders();

            if (points == null || points.Count == 0)
            {
                return;
            }

            wallColliderRoot = new GameObject(wallColliderRootName).transform;
            wallColliderRoot.SetParent(wallColliderTilemap.transform, false);

            foreach (RectInt rect in MergeCells(points))
            {
                CreateWallCollider(rect);
            }
        }

        private void ClearWallColliders()
        {
            if (wallColliderRoot == null)
            {
                Transform existingRoot = wallColliderTilemap.transform.Find(wallColliderRootName);
                if (existingRoot != null)
                {
                    wallColliderRoot = existingRoot;
                }
            }

            if (wallColliderRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(wallColliderRoot.gameObject);
            }
            else
            {
                DestroyImmediate(wallColliderRoot.gameObject);
            }

            wallColliderRoot = null;
        }

        private IEnumerable<RectInt> MergeCells(HashSet<Vector2Int> points)
        {
            HashSet<Vector2Int> remaining = new HashSet<Vector2Int>(points);

            while (remaining.Count > 0)
            {
                Vector2Int start = remaining.OrderBy(point => point.y).ThenBy(point => point.x).First();

                int width = 1;
                while (remaining.Contains(new Vector2Int(start.x + width, start.y)))
                {
                    width++;
                }

                int height = 1;
                bool canGrow = true;
                while (canGrow)
                {
                    int nextY = start.y + height;
                    for (int x = 0; x < width; x++)
                    {
                        if (!remaining.Contains(new Vector2Int(start.x + x, nextY)))
                        {
                            canGrow = false;
                            break;
                        }
                    }

                    if (canGrow)
                    {
                        height++;
                    }
                }

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        remaining.Remove(new Vector2Int(start.x + x, start.y + y));
                    }
                }

                yield return new RectInt(start.x, start.y, width, height);
            }
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
    }
}
