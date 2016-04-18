using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Tiled2Unity
{
    // Partial class methods for building layer data from xml strings or files
    partial class TmxLayer
    {
        public static TmxLayer FromXml(XElement elem, TmxMap tmxMap)
        {
            Program.WriteVerbose(elem.ToString());
            TmxLayer tmxLayer = new TmxLayer(tmxMap);

            // Order within Xml file is import for layer types
            tmxLayer.XmlElementIndex = elem.NodesBeforeSelf().Count();

            // Have to decorate layer names in order to force them into being unique
            // Also, can't have whitespace in the name because Unity will add underscores
            tmxLayer.Name = TmxHelper.GetAttributeAsString(elem, "name");

            tmxLayer.Visible = TmxHelper.GetAttributeAsInt(elem, "visible", 1) == 1;
            tmxLayer.Opacity = TmxHelper.GetAttributeAsFloat(elem, "opacity", 1);

            PointF offset = new PointF(0, 0);
            offset.X = TmxHelper.GetAttributeAsFloat(elem, "offsetx", 0);
            offset.Y = TmxHelper.GetAttributeAsFloat(elem, "offsety", 0);
            tmxLayer.Offset = offset;

            // Set our properties
            tmxLayer.Properties = TmxProperties.FromXml(elem);

            // Set the "ignore" setting on this layer
            tmxLayer.Ignore = tmxLayer.Properties.GetPropertyValueAsEnum<IgnoreSettings>("unity:ignore", IgnoreSettings.False);

            // We can build a layer from a "tile layer" (default) or an "image layer"
            if (elem.Name == "layer")
            {
                tmxLayer.Width = TmxHelper.GetAttributeAsInt(elem, "width");
                tmxLayer.Height = TmxHelper.GetAttributeAsInt(elem, "height");
                tmxLayer.ParseData(elem.Element("data"));
            }
            else if (elem.Name == "imagelayer")
            {
                XElement xmlImage = elem.Element("image");
                if (xmlImage == null)
                {
                    Program.WriteWarning("Image Layer '{0}' is being ignored since it has no image.", tmxLayer.Name);
                    tmxLayer.Ignore = IgnoreSettings.True;
                    return tmxLayer;
                }

                // An image layer is sort of like an tile layer but with just one tile
                tmxLayer.Width = 1;
                tmxLayer.Height = 1;

                // Find the "tile" that matches our image
                string imagePath = TmxHelper.GetAttributeAsFullPath(elem.Element("image"), "source");
                TmxTile tile = tmxMap.Tiles.First(t => t.Value.TmxImage.AbsolutePath == imagePath).Value;
                tmxLayer.TileIds = new uint[1] { tile.GlobalId };

                // The image layer needs to be tranlated in an interesting way when expressed as a tile layer
                PointF translated = tmxLayer.Offset;

                // Make up for height of a regular tile in the map
                translated.Y -= (float)tmxMap.TileHeight;

                // Make up for the height of this image
                translated.Y += (float)tile.TmxImage.Size.Height;

                // Correct for any orientation effects on the map (like isometric)
                // (We essentially undo the translation via orientation here)
                PointF orientation = TmxMath.TileCornerInScreenCoordinates(tmxMap, 0, 0);
                translated.X -= orientation.X;
                translated.Y -= orientation.Y;

                // Translate by the x and y coordiantes
                translated.X += TmxHelper.GetAttributeAsFloat(elem, "x", 0);
                translated.Y += TmxHelper.GetAttributeAsFloat(elem, "y", 0);
                tmxLayer.Offset = translated;
            }

            // Sometimes TMX files have "dead" tiles in them (tiles that were removed but are still referenced)
            // Remove these tiles from the layer by replacing them with zero
            for (int t = 0; t < tmxLayer.TileIds.Length; ++t)
            {
                uint tileId = tmxLayer.TileIds[t];
                tileId = TmxMath.GetTileIdWithoutFlags(tileId);
                if (!tmxMap.Tiles.ContainsKey(tileId))
                {
                    tmxLayer.TileIds[t] = 0;
                }
            }

            // Each layer will be broken down into "meshes" which are collections of tiles matching the same texture or animation
            tmxLayer.Meshes = TmxMesh.ListFromTmxLayer(tmxLayer);

            // Each layer may contain different collision types which are themselves put into "Collison Layers" to be processed later
            tmxLayer.UnityLayerOverrideName = tmxLayer.Properties.GetPropertyValueAsString("unity:layer", "");
            tmxLayer.BuildCollisionLayers();

            return tmxLayer;
        }

        private void ParseData(XElement elem)
        {
            Program.WriteLine("Parse {0} layer data ...", this.Name);
            Program.WriteVerbose(elem.ToString());

            string encoding = TmxHelper.GetAttributeAsString(elem, "encoding", "");
            string compression = TmxHelper.GetAttributeAsString(elem, "compression", "");
            if (elem.Element("tile") != null)
            {
                ParseTileDataAsXml(elem);
            }
            else if (encoding == "csv")
            {
                ParseTileDataAsCsv(elem);
            }
            else if (encoding == "base64" && String.IsNullOrEmpty(compression))
            {
                ParseTileDataAsBase64(elem);
            }
            else if (encoding == "base64" && compression == "gzip")
            {
                ParseTileDataAsBase64GZip(elem);
            }
            else if (encoding == "base64" && compression == "zlib")
            {
                ParseTileDataAsBase64Zlib(elem);
            }
            else
            {
                TmxException.ThrowFormat("Unsupported schema for {0} layer data", this.Name);
            }
        }

        private void ParseTileDataAsXml(XElement elemData)
        {
            Program.WriteLine("Parsing layer data as Xml elements ...");
            var tiles = from t in elemData.Elements("tile")
                        select TmxHelper.GetAttributeAsUInt(t, "gid");
            this.TileIds = tiles.ToArray();
        }

        private void ParseTileDataAsCsv(XElement elem)
        {
            Program.WriteLine("Parsing layer data as CSV ...");
            List<uint> tileIds = new List<uint>();

            // Splitting line-by-line reducues out-of-memory exceptions in x86 builds
            string value = elem.Value;
            StringReader reader = new StringReader(value);
            string line = string.Empty;
            do
            {
                line = reader.ReadLine();
                if (!String.IsNullOrEmpty(line))
                {
                    var datum = from val in line.Split(',')
                                where !String.IsNullOrEmpty(val)
                                select Convert.ToUInt32(val);
                    tileIds.AddRange(datum);
                }

            } while (line != null);

            this.TileIds = tileIds.ToArray();
        }

        private void ParseTileDataAsBase64(XElement elem)
        {
            Program.WriteLine("Parsing layer data as base64 string ...");
            byte[] bytes = Convert.FromBase64String(elem.Value);
            BytesToTiles(bytes);
        }

        private void ParseTileDataAsBase64GZip(XElement elem)
        {
            Program.WriteLine("Parsing layer data as base64 gzip-compressed string ...");
            byte[] bytesCompressed = Convert.FromBase64String(elem.Value);

            MemoryStream streamCompressed = new MemoryStream(bytesCompressed);

            // Now, decompress the bytes
            using (MemoryStream streamDecompressed = new MemoryStream())
            using (GZipStream deflateStream = new GZipStream(streamCompressed, CompressionMode.Decompress))
            {
                deflateStream.CopyTo(streamDecompressed);
                byte[] bytesDecompressed = streamDecompressed.ToArray();
                BytesToTiles(bytesDecompressed);
            }
        }

        private void ParseTileDataAsBase64Zlib(XElement elem)
        {
            Program.WriteLine("Parsing layer data as base64 zlib-compressed string ...");
            byte[] bytesCompressed = Convert.FromBase64String(elem.Value);

            MemoryStream streamCompressed = new MemoryStream(bytesCompressed);

            // Nasty trick: Have to read past the zlib stream header
            streamCompressed.ReadByte();
            streamCompressed.ReadByte();

            // Now, decompress the bytes
            using (MemoryStream streamDecompressed = new MemoryStream())
            using (DeflateStream deflateStream = new DeflateStream(streamCompressed, CompressionMode.Decompress))
            {
                deflateStream.CopyTo(streamDecompressed);
                byte[] bytesDecompressed = streamDecompressed.ToArray();
                BytesToTiles(bytesDecompressed);
            }
        }

        private void BytesToTiles(byte[] bytes)
        {
            this.TileIds = new uint[bytes.Length / 4];
            for (int i = 0; i < this.TileIds.Count(); ++i)
            {
                this.TileIds[i] = BitConverter.ToUInt32(bytes, i * 4);
            }
        }

        private void BuildCollisionLayers()
        {
            this.CollisionLayers.Clear();

            // Don't build collision layers if we're invisible
            if (this.Visible == false)
                return;

            // Don't build collision layers if we're ignored
            if (this.Ignore == IgnoreSettings.True)
                return;

            // Don't build collision layers if collision is ignored
            if (this.Ignore == IgnoreSettings.Collision)
                return;

            // Are we using a unity-layer override? If so we have to put everything from this layer into it.
            if (String.IsNullOrEmpty(this.UnityLayerOverrideName))
            {
                BuildBuildCollisionLayers_ByObjectType();
            }
            else
            {
                BuildBuildCollisionLayers_Override();
            }
        }

        private void BuildBuildCollisionLayers_Override()
        {
            // Just make the layer the collision layer
            this.CollisionLayers.Clear();
            this.CollisionLayers.Add(this);
        }

        private void BuildBuildCollisionLayers_ByObjectType()
        {
            // Find all tiles with collisions on them and put them into a "Collision Layer" of the same type
            for (int t = 0; t < this.TileIds.Length; ++t)
            {
                uint rawTileId = this.TileIds[t];
                if (rawTileId == 0)
                    continue;

                uint tileId = TmxMath.GetTileIdWithoutFlags(rawTileId);
                TmxTile tmxTile = this.TmxMap.Tiles[tileId];

                foreach (TmxObject colliderObject in tmxTile.ObjectGroup.Objects)
                {
                    if ((colliderObject is TmxHasPoints) == false)
                        continue;

                    // We have a collider object on the tile
                    // Add the tile to the Collision Layer of the matching type
                    // Or, create a new Collision Layer of this type to add this tile to
                    TmxLayer collisionLayer = this.CollisionLayers.Find(l => String.Compare(l.Name, colliderObject.Type, true) == 0);
                    if (collisionLayer == null)
                    {
                        // Create a new Collision Layer
                        collisionLayer = new TmxLayer(this.TmxMap);
                        this.CollisionLayers.Add(collisionLayer);

                        // The new Collision Layer has the name of the collider object and empty tiles (they will be filled with tiles that have matching collider objects)
                        collisionLayer.Name = colliderObject.Type;
                        collisionLayer.TileIds = new uint[this.TileIds.Length];

                        // Copy over some stuff from parent layer that we need for creating collisions
                        collisionLayer.Offset = this.Offset;
                        collisionLayer.Width = this.Width;
                        collisionLayer.Height = this.Height;
                        collisionLayer.Ignore = this.Ignore;
                        collisionLayer.Properties = this.Properties;
                    }

                    // Add the tile to this collision layer
                    collisionLayer.TileIds[t] = rawTileId;
                }
            }
        }

    }
}
