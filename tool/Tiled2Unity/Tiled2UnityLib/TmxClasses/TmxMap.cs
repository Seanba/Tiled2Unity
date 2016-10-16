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

        static public readonly uint FLIPPED_HORIZONTALLY_FLAG = 0x80000000;
        static public readonly uint FLIPPED_VERTICALLY_FLAG = 0x40000000;
        static public readonly uint FLIPPED_DIAGONALLY_FLAG = 0x20000000;

        public bool IsLoaded { get; private set; }

        public string Name { get; private set; }
        public MapOrientation Orientation { get; set; }
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

        // The map may load object type data from another file
        public TmxObjectTypes ObjectTypes = new TmxObjectTypes();

        private uint nextUniqueId = 0;

        public TmxMap()
        {
            this.IsLoaded = false;
            this.Properties = new TmxProperties();
        }

        public string GetExportedFilename()
        {
            return String.Format("{0}.tiled2unity.xml", this.Name);
        }

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

        public TmxTile GetTileFromTileId(uint tileId)
        {
            if (tileId == 0)
            {
                return null;
            }

            tileId = tileId & ~(FLIPPED_HORIZONTALLY_FLAG | FLIPPED_VERTICALLY_FLAG | FLIPPED_DIAGONALLY_FLAG);
            TmxTile tile;
            Tiles.TryGetValue(tileId, out tile);
            return tile;
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

        // Get a unique Id tied to this map instance.
        public uint GetUniqueId()
        {
            return ++this.nextUniqueId;
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

        // Get a unique list of all the tiles that are used as tile objects
        public List<TmxMesh> GetUniqueListOfVisibleObjectTileMeshes()
        {
            var tiles = from objectGroup in this.ObjectGroups
                        where objectGroup.Visible == true
                        from tmxObject in objectGroup.Objects
                        where tmxObject.Visible == true
                        let tmxObjectTile = tmxObject as TmxObjectTile
                        where tmxObjectTile != null
                        from tmxMesh in tmxObjectTile.Tile.Meshes
                        select tmxMesh;

            // Make list unique based on mesh name
            return tiles.GroupBy(m => m.UniqueMeshName).Select(g => g.First()).ToList();
        }

        // Load an Object Type Xml file for this map's objects to reference
        public void LoadObjectTypeXml(string xmlPath)
        {
            Logger.WriteLine("Loading Object Type Xml file: '{0}'", xmlPath);

            try
            {
                this.ObjectTypes = TmxObjectTypes.FromXmlFile(xmlPath);
            }
            catch (FileNotFoundException)
            {
                Logger.WriteError("Object Type Xml file was not found: {0}", xmlPath);
                this.ObjectTypes = new TmxObjectTypes();
            }
            catch (Exception e)
            {
                Logger.WriteError("Error parsing Object Type Xml file: {0}\n{1}", xmlPath, e.Message);
                this.ObjectTypes = new TmxObjectTypes();
            }

            Logger.WriteLine("Tiled Object Type count = {0}", this.ObjectTypes.TmxObjectTypeMapping.Count());
        }

        public void ClearObjectTypeXml()
        {
            Logger.WriteLine("Removing Object Types from map.");
            this.ObjectTypes = new TmxObjectTypes();
        }

    }
}
