using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    public class TmxObjectTile : TmxObject
    {
        public TmxTile Tile { get; private set; }
        public bool FlippedHorizontal { get; private set; }
        public bool FlippedVertical { get; private set; }

        public string SortingLayerName { get; private set; }
        public int? SortingOrder { get; private set; }

        public TmxObjectTile()
        {
            this.SortingLayerName = null;
        }

        public override System.Drawing.RectangleF GetWorldBounds()
        {
            RectangleF myBounds = new RectangleF(this.Position.X, this.Position.Y - this.Size.Height, this.Size.Width, this.Size.Height);

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

        public SizeF GetTileObjectScale()
        {
            float scaleX = this.Size.Width / this.Tile.TileSize.Width;
            float scaleY = this.Size.Height / this.Tile.TileSize.Height;
            return new SizeF(scaleX, scaleY);
        }

        protected override void InternalFromXml(System.Xml.Linq.XElement xml, TmxMap tmxMap)
        {
            // Get the tile
            uint gid = TmxHelper.GetAttributeAsUInt(xml, "gid");
            this.FlippedHorizontal = TmxMath.IsTileFlippedHorizontally(gid);
            this.FlippedVertical = TmxMath.IsTileFlippedVertically(gid);
            uint rawTileId = TmxMath.GetTileIdWithoutFlags(gid);

            this.Tile = tmxMap.Tiles[rawTileId];

            // The tile needs to have a mesh on it.
            // Note: The tile may already be referenced by another TmxObjectTile instance, and as such will have its mesh data already made
            if (this.Tile.Meshes.Count() == 0)
            {
                this.Tile.Meshes = TmxMesh.FromTmxTile(this.Tile, tmxMap);
            }

            // Check properties for layer placement
            if (this.Properties.PropertyMap.ContainsKey("unity:sortingLayerName"))
            {
                this.SortingLayerName = this.Properties.GetPropertyValueAsString("unity:sortingLayerName");
            }
            if (this.Properties.PropertyMap.ContainsKey("unity:sortingOrder"))
            {
                this.SortingOrder = this.Properties.GetPropertyValueAsInt("unity:sortingOrder");
            }
        }

        protected override string InternalGetDefaultName()
        {
            return "TileObject";
        }

    }
}
