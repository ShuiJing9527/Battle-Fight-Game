using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace UnderTheStars.GenerationMap
{
    public class RandomMapPaintTilemap : MonoBehaviour
    {
        [Header("地图瓷砖")]
        [SerializeField] private TileBase[] floorTile;//地面瓷砖
        [SerializeField] private TileBase wallColliderTile;//墙体碰撞瓷砖

        [Header("地图Tilemap")]
        [SerializeField] private Tilemap[] floorTilemap;//地面Tilemap组件
        [SerializeField] private Tilemap wallColliderTilemap;//墙体碰撞Tilemap组件

        /// <summary> 初始化清除地图瓷砖 </summary>
        internal void InitClearTile()
        {
            foreach (var tile in floorTilemap)
            {
                tile.ClearAllTiles();
            }
            wallColliderTilemap.ClearAllTiles();
        }

        #region 绘制地图
        /// <summary> 绘制地图瓷砖 </summary>
        private async UniTask PaintTile(HashSet<Vector2Int> points, Tilemap tilemap, TileBase tile)
        {
            int count = 0;
            foreach (var point in points)
            {
                var tilePoint = tilemap.WorldToCell((Vector3Int)point);
                tilemap.SetTile(tilePoint, tile);
                if (count >= 500)
                {
                    count = 0;
                    await UniTask.NextFrame();
                }
            }
        }

        /// <summary> 绘制地面地图瓷砖 </summary>
        public UniTask PaintFloorTile(HashSet<Vector2Int> points, int tileIndex)
        {
            return PaintTile(points, floorTilemap[tileIndex], floorTile[tileIndex]);
        }

        /// <summary> 绘制墙体地图瓷砖 </summary>
        public UniTask PaintWallTile(HashSet<Vector2Int> points)
        {
            return PaintTile(points, wallColliderTilemap, wallColliderTile);
        }
        #endregion
    }
}
