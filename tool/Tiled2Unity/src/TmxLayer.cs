using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    public partial class TmxLayer : TmxHasProperties
    {
        public string Name { get; private set; }
        public bool Visible { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public uint[] TileIds { get; private set; }
        public TmxProperties Properties { get; private set; }

        public uint GetTileIdAt(int x, int y)
        {
            uint tileId = GetRawTileIdAt(x, y);
            return TmxMath.GetTileIdWithoutFlags(tileId);
        }

        public uint GetRawTileIdAt(int x, int y)
        {
            Debug.Assert(x < this.Width && y < this.Height);
            Debug.Assert(x >= 0 && y >= 0);
            int index = y * this.Width + x;
            return this.TileIds[index];
        }

    }
}
