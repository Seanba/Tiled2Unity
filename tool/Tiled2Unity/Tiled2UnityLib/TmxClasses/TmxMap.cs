using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;


namespace Tiled2Unity
{
    public partial class TmxMap : TmxHasProperties, ITmxVisit
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

        public Size MapSizeInPixels { get; private set; }

        // Is the prefab created by this map going to be loaded as a resource?
        public bool IsResource { get; private set; }

        public IDictionary<uint, TmxTile> Tiles = new Dictionary<uint, TmxTile>();

        // Our list of layer trees
        public List<TmxLayerNode> LayerNodes { get; private set; }

        // The map may load object type data from another file
        public TmxObjectTypes ObjectTypes = new TmxObjectTypes();

        private uint nextUniqueId = 0;

        public TmxMap()
        {
            this.IsLoaded = false;
            this.Properties = new TmxProperties();
            this.LayerNodes = new List<TmxLayerNode>();
        }

        public string GetExportedFilename()
        {
            return String.Format("{0}.tiled2unity.xml", this.Name);
        }

        public override string ToString()
        {
            return String.Format("{{ \"{5}\" size = {0}x{1}, tile size = {2}x{3}, # tiles = {4} }}",
                this.Width,
                this.Height,
                this.TileWidth,
                this.TileHeight,
                this.Tiles.Count(),
                this.Name);
        }

        public TmxTile GetTileFromTileId(uint tileId)
        {
            if (tileId == 0)
                return null;

            tileId = TmxMath.GetTileIdWithoutFlags(tileId);
            return this.Tiles[tileId];
        }

        public Point GetMapPositionAt(int x, int y)
        {
            return TmxMath.TileCornerInScreenCoordinates(this, x, y);
        }

        public Point GetMapPositionAt(int x, int y, TmxTile tile)
        {
            Point point = GetMapPositionAt(x, y);
            point.X += (int)tile.Offset.X;
            point.Y += (int)tile.Offset.Y;

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

        private Size CalculateMapSizeInPixels()
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
            var tiles = from objectGroup in this.EnumerateObjectLayers()
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
            if (String.IsNullOrEmpty(xmlPath))
            {
                Logger.WriteInfo("Object Type XML file is not being used.");
                return;
            }

            Logger.WriteInfo("Loading Object Type Xml file: '{0}'", xmlPath);

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
                Logger.WriteError("Stack:\n{0}", e.StackTrace);
                this.ObjectTypes = new TmxObjectTypes();
            }

            Logger.WriteInfo("Tiled Object Type count = {0}", this.ObjectTypes.TmxObjectTypeMapping.Count());
        }

        public void ClearObjectTypeXml()
        {
            Logger.WriteInfo("Removing Object Types from map.");
            this.ObjectTypes = new TmxObjectTypes();
        }

        public IEnumerable<TmxLayer> EnumerateTileLayers()
        {
            foreach (var recursive in EnumerateLayersByType<TmxLayer>())
            {
                yield return recursive;
            }
        }

        public IEnumerable<TmxObjectGroup> EnumerateObjectLayers()
        {
            foreach (var recursive in EnumerateLayersByType<TmxObjectGroup>())
            {
                yield return recursive;
            }
        }

        private IEnumerable<T> EnumerateLayersByType<T>() where T : TmxLayerNode
        {
            // Map will have a collection of layer nodes which themselves may also have child layer nodes
            foreach (var node in this.LayerNodes)
            {
                foreach (var recursive in RecursiveEnumerate<T>(node))
                {
                    yield return recursive;
                }
            }
        }

        private IEnumerable<T> RecursiveEnumerate<T>(TmxLayerNode layerNode) where T : TmxLayerNode
        {
            // Is this node the type we're looking for?
            if (layerNode.GetType() == typeof(T))
            {
                yield return (T)layerNode;
            }

            // Go through all children nodes
            foreach (var child in layerNode.LayerNodes)
            {
                foreach (var recursive in RecursiveEnumerate<T>(child))
                {
                    yield return recursive;
                }
            }
        }

        public void Visit(ITmxVisitor visitor)
        {
            // Visit the map
            visitor.VisitMap(this);

            // Visit all the children nodes in order
            foreach (var node in this.LayerNodes)
            {
                node.Visit(visitor);
            }
        }
    }
}
