using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

using UnityEditor;
using UnityEngine;

namespace Tiled2Unity
{
    class ImportUtils
    {
        public static string GetMaterialPath(string texName)
        {
            return String.Format("Assets/Tiled2Unity/Materials/{0}.mat", Path.GetFileNameWithoutExtension(texName));
        }

        public static string GetTexturePath(string filename)
        {
            return Path.Combine("Assets/Tiled2Unity/Textures", filename);
        }

        public static string GetMeshPath(string filename)
        {
            return String.Format("Assets/Tiled2Unity/Meshes/{0}.obj", Path.GetFileNameWithoutExtension(filename));
        }

        public static string GetXmlPathFromFile(string objFile)
        {
            return String.Format("Assets/Tiled2Unity/Imported/{0}.tiled2unity.xml", Path.GetFileNameWithoutExtension(objFile));
        }

        public static string GetXmlPathFromName(string objName)
        {
            return String.Format("Assets/Tiled2Unity/Imported/{0}.tiled2unity.xml", objName);
        }

        public static string GetPrefabPathFromName(string objName)
        {
            return String.Format("Assets/Tiled2Unity/Prefabs/{0}.prefab", objName);
        }

        public static string GetAttributeAsString(XElement elem, string attrName)
        {
            return elem.Attribute(attrName).Value;
        }

        public static string GetAttributeAsString(XElement elem, string attrName, string defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsString(elem, attrName);
        }

        public static int GetAttributeAsInt(XElement elem, string attrName)
        {
            return Convert.ToInt32(elem.Attribute(attrName).Value);
        }

        public static int GetAttributeAsInt(XElement elem, string attrName, int defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsInt(elem, attrName);
        }

        public static float GetAttributeAsFloat(XElement elem, string attrName)
        {
            return Convert.ToSingle(elem.Attribute(attrName).Value);
        }

        public static float GetAttributeAsFloat(XElement elem, string attrName, float defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsFloat(elem, attrName);
        }

        public static bool GetAttributeAsBoolean(XElement elem, string attrName)
        {
            return Convert.ToBoolean(elem.Attribute(attrName).Value);
        }

        public static bool GetAttributeAsBoolean(XElement elem, string attrName, bool defaultValue)
        {
            XAttribute attr = elem.Attribute(attrName);
            if (attr == null)
            {
                return defaultValue;
            }
            return GetAttributeAsBoolean(elem, attrName);
        }

        public static string GetAttributeAsFullPath(XElement elem, string attrName)
        {
            return Path.GetFullPath(elem.Attribute(attrName).Value);
        }

        public static void ReadyToWrite(string path)
        {
            // Creates directories in path if they don't exist
            FileInfo info = new FileInfo(path);
            info.Directory.Create();

            // Make sure file is not readonly
            if ((info.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                throw new UnityException(String.Format("{0} is read-only", path));
            }
        }

        public static byte[] Base64ToBytes(string base64)
        {
            return Convert.FromBase64String(base64);
        }

        public static string Base64ToString(string base64)
        {
            byte[] bytes = Convert.FromBase64String(base64);
            return Encoding.ASCII.GetString(bytes);
        }

        // Bah! This won't work (at least yet) due to Mono being a bit behind the .Net libraries
        //public static byte[] GzipBase64ToBytes(string gzipBase64)
        //{
        //    byte[] bytesFromBase64 = Convert.FromBase64String(gzipBase64);
        //    MemoryStream streamCompressed = new MemoryStream(bytesFromBase64);

        //    // Now, decompress the bytes
        //    using (MemoryStream streamDecompressed = new MemoryStream())
        //    using (GZipStream deflateStream = new GZipStream(streamCompressed, CompressionMode.Decompress))
        //    {
        //        deflateStream.CopyTo(streamDecompressed);
        //        byte[] bytesDecompressed = streamDecompressed.ToArray();
        //        return bytesDecompressed;
        //    }
        //}

    } // end class

    public static class HelperExtensions
    {
        // Mono does not support GZipStream.CopyTo method yet
        //public static long CopyTo(this Stream source, Stream destination)
        //{
        //    byte[] buffer = new byte[2048];
        //    int bytesRead;
        //    long totalBytes = 0;
        //    while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        //    {
        //        destination.Write(buffer, 0, bytesRead);
        //        totalBytes += bytesRead;
        //    }
        //    return totalBytes;
        //}
    }
}
