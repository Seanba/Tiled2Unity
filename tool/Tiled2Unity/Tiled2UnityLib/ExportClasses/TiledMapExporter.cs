using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace Tiled2Unity
{
    public partial class TiledMapExporter
    {
        private TmxMap tmxMap = null;

        public TiledMapExporter(TmxMap tmxMap)
        {
            this.tmxMap = tmxMap;
        }

        public void Export(string exportToTiled2UnityPath)
        {
            if (String.IsNullOrEmpty(exportToTiled2UnityPath))
            {
                throw new TmxException("Unity project export path is invalid or not set.");
            }

            // Create an Xml file to be imported by a Unity project
            // The unity project will have code that turns the Xml into Unity objects and prefabs
            string fileToSave = this.tmxMap.GetExportedFilename();
            Logger.WriteInfo("Compiling tiled2unity file: {0}", fileToSave);

            // Need an element for embedded file data that will be imported into Unity
            // These are models and textures
            List<XElement> importFiles = CreateImportFilesElements(exportToTiled2UnityPath);
            List<XElement> assignMaterials = CreateAssignMaterialsElements();

            Logger.WriteVerbose("Gathering prefab data ...");
            XElement prefab = CreatePrefabElement();

            // Create the Xml root and populate it
            Logger.WriteVerbose("Writing as Xml ...");

            string version = Tiled2Unity.Info.GetVersion();
            XElement root = new XElement("Tiled2Unity", new XAttribute("version", version));
            root.Add(assignMaterials);
            root.Add(prefab);
            root.Add(importFiles);

            // Create the XDocument to save
            XDocument doc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XComment("Tiled2Unity generated xml data"),
                new XComment("Do not modify by hand"),
                new XComment(String.Format("Last exported: {0}", DateTime.Now)),
                root);

            // Build the export directory
            string exportDir = Path.Combine(exportToTiled2UnityPath, "Imported");

            if (!Directory.Exists(exportDir))
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendFormat("Could not export '{0}'\n", fileToSave);
                builder.AppendFormat("Tiled2Unity.unitypackage is not installed in unity project: {0}\n", exportToTiled2UnityPath);
                builder.AppendFormat("Select \"Help -> Import Unity Package to Project\" and re-export");
                Logger.WriteError(builder.ToString());
                return;
            }

            // Detect which version of Tiled2Unity is in our project
            // ...\Tiled2Unity\Tiled2Unity.export.txt
            string tiled2unity_export_txt = Path.Combine(exportToTiled2UnityPath, "Tiled2Unity.export.txt");
            if (!File.Exists(tiled2unity_export_txt))
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendFormat("Could not export '{0}'\n", fileToSave);
                builder.AppendFormat("Tiled2Unity.unitypackage is not properly installed in unity project: {0}\n", exportToTiled2UnityPath);
                builder.AppendFormat("Missing file: {0}\n", tiled2unity_export_txt);
                builder.AppendFormat("Select \"Help -> Import Unity Package to Project\" and re-export");
                Logger.WriteError(builder.ToString());
                return;
            }

            // Open the unity-side script file and check its version number
            string projectVersion = GetTiled2UnityVersionInProject(tiled2unity_export_txt);
            if (Tiled2Unity.Info.GetVersion() != projectVersion)
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendFormat("Export/Import Version mismatch\n");
                builder.AppendFormat("  Tiled2Unity version   : {0}\n", Tiled2Unity.Info.GetVersion());
                builder.AppendFormat("  Unity Project version : {0}\n", projectVersion);
                builder.AppendFormat("  (Did you forget to update Tiled2Unity scipts in your Unity project?)");
                Logger.WriteWarning(builder.ToString());
            }

            // Save the file (which is importing it into Unity)
            string pathToSave = Path.Combine(exportDir, fileToSave);
            Logger.WriteInfo("Exporting to: {0}", pathToSave);
            doc.Save(pathToSave);
            Logger.WriteSuccess("Succesfully exported: {0}\n  Vertex Scale = {1}\n  Object Type Xml = {2}",
                pathToSave,
                Tiled2Unity.Settings.Scale,
                String.IsNullOrEmpty(Tiled2Unity.Settings.ObjectTypeXml) ? "<none>" : Tiled2Unity.Settings.ObjectTypeXml);
        }

        private static string GetTiled2UnityVersionInProject(string path)
        {
            try
            {
                XDocument xml = XDocument.Load(path);
                return xml.Element("Tiled2UnityImporter").Element("Header").Attribute("version").Value;
            }
            catch (Exception e)
            {
                Logger.WriteWarning("Couldn't get Tiled2Unity version from '{0}'\n{1}", path, e.Message);
                return "tiled2unity.get.version.fail";
            }
        }

        public static PointF PointFToUnityVector_NoScale(PointF pt)
        {
            // Unity's coordinate sytem has y-up positive, y-down negative
            // Have to watch for negative zero, ffs
            return new PointF(pt.X, pt.Y == 0 ? 0 : -pt.Y);
        }

        public static PointF PointFToUnityVector(float x, float y)
        {
            return PointFToUnityVector(new PointF(x, y));
        }

        public static PointF PointFToUnityVector(PointF pt)
        {
            // Unity's coordinate sytem has y-up positive, y-down negative
            // Apply scaling
            PointF scaled = pt;
            scaled.X *= Tiled2Unity.Settings.Scale;
            scaled.Y *= Tiled2Unity.Settings.Scale;

            // Have to watch for negative zero, ffs
            return new PointF(scaled.X, scaled.Y == 0 ? 0 : -scaled.Y);
        }

        public static PointF PointFToObjVertex(PointF pt)
        {
            // Note, we negate the x and y due to Wavefront's coordinate system
            // Applying scaling
            PointF scaled = pt;
            scaled.X *= Tiled2Unity.Settings.Scale;
            scaled.Y *= Tiled2Unity.Settings.Scale;

            // Watch for negative zero, ffs
            return new PointF(scaled.X == 0 ? 0 : -scaled.X, scaled.Y == 0 ? 0 : -scaled.Y);
        }

        public static PointF PointToTextureCoordinate(PointF pt, Size imageSize)
        {
            float tx = pt.X / (float)imageSize.Width;
            float ty = pt.Y / (float)imageSize.Height;
            return new PointF(tx, 1.0f - ty);
        }

        public static float CalculateFaceDepth(float position_y, float mapHeight)
        {
            float z = position_y / mapHeight * -1.0f;
            return (z == -0.0f) ? 0 : z;
        }

        public static float CalculateLayerDepth(TmxLayerNode layer)
        {
            if (!Tiled2Unity.Settings.DepthBufferEnabled)
                return 0.0f;

            float depthOfOneTile = layer.TmxMap.TileHeight / (float)layer.TmxMap.MapSizeInPixels.Height;
            float z = layer.DepthBufferIndex * depthOfOneTile * -1.0f;

            // How much is our layer offset as a function of tiles?
            float offsetRatio = layer.Offset.Y / layer.TmxMap.TileHeight;
            z -= offsetRatio * depthOfOneTile;

            return (z == -0.0f) ? 0 : z;
        }

        private string StringToBase64String(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            return Convert.ToBase64String(bytes);
        }

        private string FileToBase64String(string path)
        {
            return Convert.ToBase64String(File.ReadAllBytes(path));
        }

        private string FileToCompressedBase64String(string path)
        {
            using (FileStream originalStream = File.OpenRead(path))
            using (MemoryStream byteStream = new MemoryStream())
            using (GZipStream gzipStream = new GZipStream(byteStream, CompressionMode.Compress))
            {
                originalStream.CopyTo(gzipStream);
                byte[] compressedBytes = byteStream.ToArray();
                return Convert.ToBase64String(compressedBytes);
            }

            // Without compression (testing shows it ~300% larger)
            //return Convert.ToBase64String(File.ReadAllBytes(path));
        }

    } // end class
} // end namepsace
