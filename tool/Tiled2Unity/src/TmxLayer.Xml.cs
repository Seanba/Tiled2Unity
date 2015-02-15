using System;
using System.Collections.Generic;
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
        public static TmxLayer FromXml(XElement elem, int layerIndex)
        {
            Program.WriteVerbose(elem.ToString());
            TmxLayer tmxLayer = new TmxLayer();

            // Have to decorate layer names in order to force them into being unique
            // Also, can't have whitespace in the name because Unity will add underscores
            tmxLayer.Name = String.Format("{0}_{1}", TmxHelper.GetAttributeAsString(elem, "name"), layerIndex.ToString("D2")).Replace(" ", "_");

            tmxLayer.Visible = TmxHelper.GetAttributeAsInt(elem, "visible", 1) == 1;
            tmxLayer.Width = TmxHelper.GetAttributeAsInt(elem, "width");
            tmxLayer.Height = TmxHelper.GetAttributeAsInt(elem, "height");
            tmxLayer.Properties = TmxProperties.FromXml(elem);

            tmxLayer.ParseData(elem.Element("data"));

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

            // Pretty-print the tileIds
            //Program.WriteLine("TileIds for {0} layer:", this.Name);

            //uint largest = this.TileIds.Max();
            //largest = TmxMath.GetTileIdWithoutFlags(largest);

            //int padding = largest.ToString().Length + 2;

            //StringBuilder builder = new StringBuilder();
            //for (int t = 0; t < this.TileIds.Count(); ++t)
            //{
            //    if (t % this.Width == 0)
            //    {
            //        Program.WriteLine(builder.ToString());
            //        builder.Clear();
            //    }

            //    uint tileId = this.TileIds[t];
            //    tileId = TmxMath.GetTileIdWithoutFlags(tileId);
            //    builder.AppendFormat("{0}", tileId.ToString().PadLeft(padding));
            //}

            //// Write the last row
            //Program.WriteLine(builder.ToString());
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
