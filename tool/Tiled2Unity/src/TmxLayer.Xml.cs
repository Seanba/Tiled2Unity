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
        public static TmxLayer FromXml(XElement elem, TmxMap tmxMap, int layerIndex)
        {
            Program.WriteVerbose(elem.ToString());
            TmxLayer tmxLayer = new TmxLayer();

            // Have to decorate layer names in order to force them into being unique
            // Also, can't have whitespace in the name because Unity will add underscores
            tmxLayer.DefaultName = TmxHelper.GetAttributeAsString(elem, "name");
            tmxLayer.UniqueName = String.Format("{0}_{1}", tmxLayer.DefaultName, layerIndex.ToString("D2")).Replace(" ", "_");

            tmxLayer.Visible = TmxHelper.GetAttributeAsInt(elem, "visible", 1) == 1;
            tmxLayer.Opacity = TmxHelper.GetAttributeAsFloat(elem, "opacity", 1);

            PointF offset = new PointF(0, 0);
            offset.X = TmxHelper.GetAttributeAsFloat(elem, "offsetx", 0);
            offset.Y = TmxHelper.GetAttributeAsFloat(elem, "offsety", 0);
            tmxLayer.Offset = offset;

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
                    Program.WriteWarning("Image Layer '{0}' is being ignored since it has no image.", tmxLayer.DefaultName);
                    tmxLayer.Ignore = IgnoreSettings.True;
                    return tmxLayer;
                }

                // An image layer is sort of like an tile layer but with just one tile
                tmxLayer.Width = 1;
                tmxLayer.Height = 1;

                // Find the "tile" that matches our image
                string imagePath = TmxHelper.GetAttributeAsFullPath(elem.Element("image"), "source");
                TmxTile tile = tmxMap.Tiles.First(t => t.Value.TmxImage.Path == imagePath).Value;
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

            return tmxLayer;
        }

        private void ParseData(XElement elem)
        {
            Program.WriteLine("Parse {0} layer data ...", this.UniqueName);
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
                TmxException.ThrowFormat("Unsupported schema for {0} layer data", this.UniqueName);
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
            var datum = from val in elem.Value.Split(',')
                        select Convert.ToUInt32(val);
            this.TileIds = datum.ToArray();
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

    }
}
