using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;


namespace Tiled2Unity
{
    public partial class TmxMap : TmxHasProperties
    {
        public delegate void ReadTmxFileCompleted(TmxMap tmxMap);
        public static event ReadTmxFileCompleted OnReadTmxFileCompleted;

        public enum MapOrientation
        {
            Orthogonal,
            Isometric,
            Staggered,
            Hexagonal,
        }

        public enum MapStaggerAxis
        {
            X,
            Y,
        }

        public enum MapStaggerIndex
        {
            Odd,
            Even,
        }

        public string Name { get; private set; }
        public MapOrientation Orientation { get; private set; }
        public MapStaggerAxis StaggerAxis { get; private set; }
        public MapStaggerIndex StaggerIndex { get; private set; }
        public int HexSideLength { get; set; }
        public int DrawOrderHorizontal { get; private set; }
        public int DrawOrderVertical { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int TileWidth { get; private set; }
        public int TileHeight { get; private set; }
        public Color BackgroundColor { get; private set; }
        public TmxProperties Properties { get; private set; }

        public IDictionary<uint, TmxTile> Tiles = new Dictionary<uint, TmxTile>();

        public IList<TmxLayer> Layers = new List<TmxLayer>();
        public IList<TmxObjectGroup> ObjectGroups = new List<TmxObjectGroup>();

        private IList<string> registeredImageNames = new List<string>();

        public override string ToString()
        {
            return String.Format("{{ \"{6}\" size = {0}x{1}, tile size = {2}x{3}, # tiles = {4}, # layers = {5}, # obj groups = {6} }}",
                this.Width,
                this.Height,
                this.TileWidth,
                this.TileHeight,
                this.Tiles.Count(),
                this.Layers.Count(),
                this.ObjectGroups.Count(),
                this.Name);
        }

        public Point GetMapPositionAt(int x, int y)
        {
            return TmxMath.TileCornerInScreenCoordinates(this, x, y);
        }

        public Point GetMapPositionAt(int x, int y, TmxTile tile)
        {
            Point point = GetMapPositionAt(x, y);

            // The tile may have different dimensions than the cells of the map so correct for that
            // In this case, the y-position needs to be adjusted
            point.Y = (point.Y + this.TileHeight) - tile.TileSize.Height;

            return point;
        }

        public void RegisterImagePath(string imagePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(imagePath);

            if (!this.registeredImageNames.Contains(fileName))
            {
                this.registeredImageNames.Add(fileName);
            }
        }

        public string GetMeshName(string layerName, string imageName)
        {
            int layerIndex = -1;
            for (int i = 0; i < this.Layers.Count(); ++i)
            {
                var layer = this.Layers[i];
                if (String.Compare(layer.UniqueName, layerName, true) == 0)
                {
                    layerIndex = i;
                    break;
                }
            }

            int imageIndex = this.registeredImageNames.IndexOf(imageName);

            string meshName = String.Format("mesh_[{0}]_[{1}]",
                layerIndex == -1 ? "bad_layer" : layerIndex.ToString("D2"),
                imageIndex == -1 ? "bad_image" : imageIndex.ToString("D2"));

            return meshName;
        }

        private static uint NextUniqueId = 0;
        private static object NextUniqueIdLock = new Object();
        public static uint GetUniqueId()
        {
            lock (TmxMap.NextUniqueIdLock)
            {
                return ++TmxMap.NextUniqueId;
            }
        }

        public Size MapSizeInPixels()
        {
            // Takes the orientation of the map into account when calculating the size
            if (this.Orientation == MapOrientation.Isometric)
            {
                Size size = Size.Empty;
                size.Width = (this.Width + this.Height) * this.TileWidth / 2;
                size.Height = (this.Width + this.Height) * this.TileHeight / 2;
                return size;
            }
            else if (this.Orientation == MapOrientation.Staggered || this.Orientation == MapOrientation.Hexagonal)
            {
                int tileHeight = this.TileHeight & ~1;
                int tileWidth = this.TileWidth & ~1;

                if (this.StaggerAxis == MapStaggerAxis.Y)
                {
                    int halfHexLeftover = (tileHeight - this.HexSideLength) / 2;

                    Size size = Size.Empty;
                    size.Width = (tileWidth * this.Width) + tileWidth / 2;
                    size.Height = (halfHexLeftover + this.HexSideLength) * this.Height + halfHexLeftover;
                    return size;
                }
                else
                {
                    int halfHexLeftover = (tileWidth - this.HexSideLength) / 2;

                    Size size = Size.Empty;
                    size.Width = (halfHexLeftover + this.HexSideLength) * this.Width + halfHexLeftover;
                    size.Height = (tileHeight * this.Height) + tileHeight / 2;
                    return size;
                }
            }

            // Default orientation (orthongonal)
            return new Size(this.Width * this.TileWidth, this.Height * this.TileHeight);
        }
    }
}
