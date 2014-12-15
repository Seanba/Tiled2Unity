using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    class TmxObjectTile : TmxObject
    {
        public TmxTile Tile { get; private set; }

        public override System.Drawing.RectangleF GetWorldBounds()
        {
            RectangleF myBounds = new RectangleF(this.Position, this.Size);
            RectangleF groupBounds = this.Tile.ObjectGroup.GetWorldBounds(this.Position);
            if (groupBounds.IsEmpty)
            {
                return myBounds;
            }
            RectangleF combinedBounds = RectangleF.Union(myBounds, groupBounds);
            return combinedBounds;
        }

        public override string ToString()
        {
            return String.Format("{{ TmxObjectTile: name={0}, pos={1}, tile={2} }}", GetNonEmptyName(), this.Position, this.Tile);
        }

        protected override void InternalFromXml(System.Xml.Linq.XElement xml, TmxMap tmxMap)
        {
            // Get the tile
            uint gid = TmxHelper.GetAttributeAsUInt(xml, "gid");
            this.Tile = tmxMap.Tiles[gid];

            // Our size is given by the tile
            this.Size = this.Tile.TileSize;
        }

        protected override string InternalGetDefaultName()
        {
            return "TileObject";
        }

    }
}
