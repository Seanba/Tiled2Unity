using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Tiled2Unity
{
    public class TiledMap : MonoBehaviour
    {
        public int NumTilesWide = 0;
        public int NumTilesHigh = 0;
        public int TileWidth = 0;
        public int TileHeight = 0;

        public int GetMapWidthInPixels()
        {
            return this.NumTilesWide * this.TileWidth;
        }

        public int GetMapHeightInPixels()
        {
            return this.NumTilesHigh * this.TileHeight;
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 pos_w = this.gameObject.transform.position;
            Vector2 topLeft = Vector2.zero + pos_w;
            Vector2 topRight = new Vector2(GetMapWidthInPixels(), 0) + pos_w;
            Vector2 bottomRight = new Vector2(GetMapWidthInPixels(), -GetMapHeightInPixels()) + pos_w;
            Vector2 bottomLeft = new Vector2(0, -GetMapHeightInPixels()) + pos_w;

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);
        }
    }
}
