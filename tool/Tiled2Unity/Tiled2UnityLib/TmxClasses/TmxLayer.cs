using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    public partial class TmxLayer : TmxLayerNode
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public uint[] TileIds { get; private set; }
        public List<TmxMesh> Meshes { get; private set; }
        public List<TmxLayer> CollisionLayers { get; private set; }

        public TmxLayer(TmxLayerNode parent, TmxMap map) : base(parent, map)
        {
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
            return Tiled2Unity.Settings.PreferConvexPolygons;
        }

        public override void Visit(ITmxVisitor visitor)
        {
            visitor.VisitTileLayer(this);
        }

    }
}
