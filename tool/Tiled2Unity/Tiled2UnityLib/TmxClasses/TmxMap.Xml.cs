using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Tiled2Unity
{
    // Partial class methods for creating TmxMap data from xml files/data
    partial class TmxMap
    {
        public static TmxMap LoadFromFile(string tmxPath)
        {
            string fullTmxPath = Path.GetFullPath(tmxPath);
            using (ChDir chdir = new ChDir(fullTmxPath))
            {
                TmxMap tmxMap = new TmxMap();
                XDocument doc = tmxMap.LoadDocument(fullTmxPath);

                tmxMap.Name = Path.GetFileNameWithoutExtension(fullTmxPath);
                tmxMap.ParseMapXml(doc);

                // We're done reading and parsing the tmx file
                Logger.WriteLine("Map details: {0}", tmxMap.ToString());
                Logger.WriteSuccess("Parsed: {0} ", fullTmxPath);

                tmxMap.IsLoaded = true;
                return tmxMap;
            }
        }

        private XDocument LoadDocument(string xmlPath)
        {
            XDocument doc = null;
            Logger.WriteLine("Opening {0} ...", xmlPath);
            try
            {
                doc = XDocument.Load(xmlPath);
            }
            catch (FileNotFoundException fnf)
            {
                string msg = String.Format("File not found: {0}", fnf.FileName);
                throw new TmxException(msg, fnf);
            }
            catch (XmlException xml)
            {
                string msg = String.Format("Xml error in {0}\n  {1}", xmlPath, xml.Message);
                throw new TmxException(msg, xml);
            }
            return doc;
        }

        private void ParseMapXml(XDocument doc)
        {
            Logger.WriteLine("Parsing map root ...");

            XElement map = doc.Element("map");
            try
            {
                this.Orientation = TmxHelper.GetAttributeAsEnum<MapOrientation>(map, "orientation");
                this.StaggerAxis = TmxHelper.GetAttributeAsEnum(map, "staggeraxis", MapStaggerAxis.Y);
                this.StaggerIndex = TmxHelper.GetAttributeAsEnum(map, "staggerindex", MapStaggerIndex.Odd);
                this.HexSideLength = TmxHelper.GetAttributeAsInt(map, "hexsidelength", 0);
                this.DrawOrderHorizontal = TmxHelper.GetAttributeAsString(map, "renderorder", "right-down").Contains("right") ? 1 : -1;
                this.DrawOrderVertical = TmxHelper.GetAttributeAsString(map, "renderorder", "right-down").Contains("down") ? 1 : -1;
                this.Width = TmxHelper.GetAttributeAsInt(map, "width");
                this.Height = TmxHelper.GetAttributeAsInt(map, "height");
                this.TileWidth = TmxHelper.GetAttributeAsInt(map, "tilewidth");
                this.TileHeight = TmxHelper.GetAttributeAsInt(map, "tileheight");
                this.BackgroundColor = TmxHelper.GetAttributeAsColor(map, "backgroundcolor", Color.FromArgb(128, 128, 128));
            }
            catch (Exception e)
            {
                TmxException.FromAttributeException(e, map);
            }

            // Collect our map properties
            this.Properties = TmxProperties.FromXml(map);

            // Check properties for us being a resource
            this.IsResource = this.Properties.GetPropertyValueAsBoolean("unity:resource", false);
            this.IsResource = this.IsResource || !String.IsNullOrEmpty(this.Properties.GetPropertyValueAsString("unity:resourcePath", null));

            ParseAllTilesets(doc);

            // Get all our child layer nodes
            this.LayerNodes = TmxLayerNode.ListFromXml(map, null, this);

            // Calcuate the size of the map. Isometric and hex maps make this more complicated than a simple width and height thing.
            this.MapSizeInPixels = CalculateMapSizeInPixels();

            // Visit each node in the map to assign display order
            TmxDisplayOrderVisitor visitor = new TmxDisplayOrderVisitor();
            this.Visit(visitor);
        }

        private void ParseAllTilesets(XDocument doc)
        {
            Logger.WriteLine("Parsing tileset elements ...");
            var tilesets = from item in doc.Descendants("tileset")
                           select item;

            foreach (var ts in tilesets)
            {
                ParseSingleTileset(ts);
            }

            // Treat images in imagelayers as tileset with a single entry
            var imageLayers = from item in doc.Descendants("imagelayer") select item;
            foreach (var il in imageLayers)
            {
                ParseTilesetFromImageLayer(il);
            }
        }

        private void ParseSingleTileset(XElement elem)
        {
            // Parse the tileset data and populate the tiles from it
            uint firstId = TmxHelper.GetAttributeAsUInt(elem, "firstgid");

            // Does the element contain all tileset data or reference an external tileset?
            XAttribute attrSource = elem.Attribute("source");
            if (attrSource == null)
            {
                ParseInternalTileset(elem, firstId);
            }
            else
            {
                // Need to load the tileset data from an external file first
                // Then we'll parse it as if it's internal data
                ParseExternalTileset(attrSource.Value, firstId);
            }
        }

        // This method is called eventually for external tilesets too
        // Only the gid attribute has been consumed at this point for the tileset
        private void ParseInternalTileset(XElement elemTileset, uint firstId)
        {
            string tilesetName = TmxHelper.GetAttributeAsString(elemTileset, "name");

            Logger.WriteLine("Parse internal tileset '{0}' (gid = {1}) ...", tilesetName, firstId);

            int tileWidth = TmxHelper.GetAttributeAsInt(elemTileset, "tilewidth");
            int tileHeight = TmxHelper.GetAttributeAsInt(elemTileset, "tileheight");
            int spacing = TmxHelper.GetAttributeAsInt(elemTileset, "spacing", 0);
            int margin = TmxHelper.GetAttributeAsInt(elemTileset, "margin", 0);

            PointF tileOffset = PointF.Empty;
            XElement xmlTileOffset = elemTileset.Element("tileoffset");
            if (xmlTileOffset != null)
            {
                tileOffset.X = TmxHelper.GetAttributeAsInt(xmlTileOffset, "x");
                tileOffset.Y = TmxHelper.GetAttributeAsInt(xmlTileOffset, "y");
            }

            IList<TmxTile> tilesToAdd = new List<TmxTile>();

            // Tilesets may have an image for all tiles within it, or it may have an image per tile
            if (elemTileset.Element("image") != null)
            {
                TmxImage tmxImage = TmxImage.FromXml(elemTileset.Element("image"));

                // Create all the tiles
                // This is a bit complicated because of spacing and margin
                // (Margin is ignored from Width and Height)
                for (int end_y = margin + tileHeight; end_y <= tmxImage.Size.Height; end_y += spacing + tileHeight)
                {
                    for (int end_x = margin + tileWidth; end_x <= tmxImage.Size.Width; end_x += spacing + tileWidth)
                    {
                        uint localId = (uint) tilesToAdd.Count();
                        uint globalId = firstId + localId;
                        TmxTile tile = new TmxTile(this, globalId, localId, tilesetName, tmxImage);
                        tile.Offset = tileOffset;
                        tile.SetTileSize(tileWidth, tileHeight);
                        tile.SetLocationOnSource(end_x - tileWidth, end_y - tileHeight);
                        tilesToAdd.Add(tile);
                    }
                }
            }
            else
            {
                // Each tile will have it's own image
                foreach (var t in elemTileset.Elements("tile"))
                {
                    TmxImage tmxImage = TmxImage.FromXml(t.Element("image"));

                    uint localId = (uint)tilesToAdd.Count();

                    // Local Id can be overridden by the tile element
                    // This is because tiles can be removed from the tileset, so we won'd always have a zero-based index
                    localId = TmxHelper.GetAttributeAsUInt(t, "id", localId);

                    uint globalId = firstId + localId;
                    TmxTile tile = new TmxTile(this, globalId, localId, tilesetName, tmxImage);
                    tile.Offset = tileOffset;
                    tile.SetTileSize(tmxImage.Size.Width, tmxImage.Size.Height);
                    tile.SetLocationOnSource(0, 0);
                    tilesToAdd.Add(tile);
                }
            }

            StringBuilder builder = new StringBuilder();
            foreach (TmxTile tile in tilesToAdd)
            {
                builder.AppendFormat("{0}", tile.ToString());
                if (tile != tilesToAdd.Last()) builder.Append("\n");
                this.Tiles[tile.GlobalId] = tile;
            }
            Logger.WriteLine("Added {0} tiles", tilesToAdd.Count);

            // Add any extra data to tiles
            foreach (var elemTile in elemTileset.Elements("tile"))
            {
                int localTileId = TmxHelper.GetAttributeAsInt(elemTile, "id");
                var tiles = from t in this.Tiles
                            where t.Value.GlobalId == localTileId + firstId
                            select t.Value;

                // Note that some old tile data may be sticking around
                if (tiles.Count() == 0)
                {
                    Logger.WriteWarning("Tile '{0}' in tileset '{1}' does not exist but there is tile data for it.\n{2}", localTileId, tilesetName, elemTile.ToString());
                }
                else
                {
                    tiles.First().ParseTileXml(elemTile, this, firstId);
                }
            }
        }

        private void ParseExternalTileset(string tsxPath, uint firstId)
        {
            string fullTsxPath = Path.GetFullPath(tsxPath);
            using (ChDir chdir = new ChDir(fullTsxPath))
            {
                XDocument tsx = LoadDocument(fullTsxPath);
                ParseInternalTileset(tsx.Root, firstId);
            }
        }

        private void ParseTilesetFromImageLayer(XElement elemImageLayer)
        {
            string tilesetName = TmxHelper.GetAttributeAsString(elemImageLayer, "name");

            XElement xmlImage = elemImageLayer.Element("image");
            if (xmlImage == null)
            {
                Logger.WriteWarning("Image Layer '{0}' has no image assigned.", tilesetName);
                return;
            }

            TmxImage tmxImage = TmxImage.FromXml(xmlImage);

            // The "firstId" is is always one more than all the tiles that we've already parsed (which may be zero)
            uint firstId = 1;
            if (this.Tiles.Count > 0)
            {
                firstId = this.Tiles.Max(t => t.Key) + 1;
            }
            
            uint localId = 1;
            uint globalId = firstId + localId;

            TmxTile tile = new TmxTile(this, globalId, localId, tilesetName, tmxImage);
            tile.SetTileSize(tmxImage.Size.Width, tmxImage.Size.Height);
            tile.SetLocationOnSource(0, 0);
            this.Tiles[tile.GlobalId] = tile;
        }
    }
}
