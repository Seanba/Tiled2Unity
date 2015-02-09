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
        public float ExportScale = 1.0f;

        public float GetMapWidthInPixels()
        {
            return this.NumTilesWide * this.TileWidth;
        }

        public float GetMapHeightInPixels()
        {
            return this.NumTilesHigh * this.TileHeight;
        }

        public float GetMapWidthInPixelsScaled()
        {
            return GetMapWidthInPixels() * this.transform.lossyScale.x * this.ExportScale;
        }

        public float GetMapHeightInPixelsScaled()
        {
            return GetMapHeightInPixels() * this.transform.lossyScale.y * this.ExportScale;
        }


        private void OnDrawGizmosSelected()
        {
            Vector2 pos_w = this.gameObject.transform.position;
            Vector2 topLeft = Vector2.zero + pos_w;
            Vector2 topRight = new Vector2(GetMapWidthInPixelsScaled(), 0) + pos_w;
            Vector2 bottomRight = new Vector2(GetMapWidthInPixelsScaled(), -GetMapHeightInPixelsScaled()) + pos_w;
            Vector2 bottomLeft = new Vector2(0, -GetMapHeightInPixelsScaled()) + pos_w;

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);
        }
    }
}
