using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

#if !UNITY_WEBPLAYER
using System.Xml.Linq;
#endif

using UnityEditor;
using UnityEngine;

namespace Tiled2Unity
{
    class Tiled2UnityMenuItems
    {
#if !UNITY_WEBPLAYER
        // Convenience function for packaging this library
        [MenuItem("Tiled2Unity/Export Tiled2Unity Library ...")]
        static void ExportLibrary()
        {
            // Get the version from our Tiled2Unity.export.txt library data file
            TextAsset textAsset = AssetDatabase.LoadAssetAtPath("Assets/Tiled2Unity/Tiled2Unity.export.txt", typeof(TextAsset)) as TextAsset;
            XDocument xml = XDocument.Parse(textAsset.text);
            string version = xml.Element("Tiled2UnityImporter").Element("Header").Attribute("version").Value;

            // Export the package
            string name = String.Format("Tiled2Unity.{0}.unitypackage", version);
            var path = EditorUtility.SaveFilePanel("Save Tiled2Unity library as unity package.", "", name, "unitypackage");
            if (path.Length != 0)
            {
                List<string> packageFiles = new List<string>();

                // Export all C# files, shaders, text files, and some select materials
                packageFiles.AddRange(EnumerateAssetFilesAt("Assets/Tiled2Unity",".cs", ".shader", ".cginc", ".txt", "t2uSprite-Depth.mat", "t2uSprite-DiffuseDepth.mat"));
                AssetDatabase.ExportPackage(packageFiles.ToArray(), path);
            }
        }
#endif

        // Not ready for public consumption yet. (But handy to have for development)
        //[MenuItem("Tiled2Unity/Clean Tiled2Unity Files")]
        //static void CleanFiles()
        //{
        //    Debug.LogWarning("Cleaning out Tiled2Unity files that were automatically created. Re-import your *.tiled2unity.xml files to re-create them.");
        //    DeleteAssetsAt("Assets/Tiled2Unity/Materials");
        //    DeleteAssetsAt("Assets/Tiled2Unity/Meshes");
        //    DeleteAssetsAt("Assets/Tiled2Unity/Prefabs");
        //    DeleteAssetsAt("Assets/Tiled2Unity/Textures");
        //}

        private static IEnumerable<string> EnumerateAssetFilesAt(string dir, params string[] endPatterns)
        {
            foreach (string f in Directory.GetFiles(dir))
            {
                if (endPatterns.Any(pat => f.EndsWith(pat, true, null)))
                {
                    yield return f;
                }
            }

            foreach (string d in Directory.GetDirectories(dir))
            {
                foreach (string f in EnumerateAssetFilesAt(d, endPatterns))
                {
                    yield return f;
                }
            }
        }

        private static void DeleteAssetsAt(string dir)
        {
            // Note: Does not remove any text files.
            foreach (string f in Directory.GetFiles(dir))
            {
                if (f.EndsWith(".txt", true, null))
                    continue;

                if (f.EndsWith(".meta", true, null))
                    continue;

                // Just to be safe. Do not remove scripts.
                if (f.EndsWith(".cs", true, null))
                    continue;

                // Do not remove special materials
                if (f.EndsWith("t2uSprite-Depth.mat", true, null))
                    continue;
                if (f.EndsWith("t2uSprite-DiffuseDepth.mat", true, null))
                    continue;

                AssetDatabase.DeleteAsset(f);
            }
        }

    }
}
