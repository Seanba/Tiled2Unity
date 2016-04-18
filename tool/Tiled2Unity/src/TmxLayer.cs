using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    public partial class TmxLayer : TmxLayerBase
    {
        public enum IgnoreSettings
        {
            False,      // Ingore nothing (layer fully-enabled)
            True,       // Ignore everything (like layer doesn't exist)
            Collision,  // Ignore collision on layer
            Visual,     // Ignore visual on layer
        };

        public TmxMap TmxMap { get; private set; }
        public string Name { get; private set; }
        public bool Visible { get; private set; }
        public float Opacity { get; private set; }
        public PointF Offset { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public IgnoreSettings Ignore { get; private set; }
        public uint[] TileIds { get; private set; }
        public List<TmxMesh> Meshes { get; private set; }
        public List<TmxLayer> CollisionLayers { get; private set; }

        public TmxLayer(TmxMap map)
        {
            this.TmxMap = map;
            this.Visible = true;
            this.Opacity = 1.0f;
            this.CollisionLayers = new List<TmxLayer>();
        }

        public uint GetTileIdAt(int x, int y)
        {
            uint tileId = GetRawTileIdAt(x, y);
            return TmxMath.GetTileIdWithoutFlags(tileId);
        }

        public uint GetRawTileIdAt(int x, int y)
        {
            Debug.Assert(x < this.Width && y < this.Height);
            Debug.Assert(x >= 0 && y >= 0);
            int index = GetTileIndex(x, y);
            return this.TileIds[index];
        }

        public int GetTileIndex(int x, int y)
        {
            return y * this.Width + x;
        }

        public bool IsExportingConvexPolygons()
        {
            // Always obey layer first
            if (this.Properties.PropertyMap.ContainsKey("unity:convex"))
            {
                return this.Properties.GetPropertyValueAsBoolean("unity:convex", true);
            }

            // Use the map next
            if (this.TmxMap.Properties.PropertyMap.ContainsKey("unity:convex"))
            {
                return this.TmxMap.Properties.GetPropertyValueAsBoolean("unity:convex", true);
            }

            // Use the program setting last
            return Program.PreferConvexPolygons;
        }

    }
}
