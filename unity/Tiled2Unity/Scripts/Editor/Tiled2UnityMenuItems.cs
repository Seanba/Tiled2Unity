using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using UnityEditor;
using UnityEngine;

namespace Tiled2Unity
{
    class Tiled2UnityMenuItems
    {
        // Convenience function for packaging this library
        [MenuItem("Tiled2Unity/Export Tiled2Unity Library ...")]
        static void ExportLibrary()
        {
            string name = String.Format("Tiled2Unity.{0}.unitypackage", ImportTiled2Unity.ThisVersion);
            var path = EditorUtility.SaveFilePanel("Save texture as PNG", "", name, "unitypackage");
            if (path.Length != 0)
            {
                List<string> packageFiles = new List<string>();
                packageFiles.AddRange(EnumerateAssetFilesAt("Assets/Tiled2Unity", ".cs", ".shader", ".txt"));
                AssetDatabase.ExportPackage(packageFiles.ToArray(), path);
            }
        }

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

        private static IEnumerable<string> EnumerateAssetFilesAt(string dir, params string[] extensions)
        {
            foreach (string f in Directory.GetFiles(dir))
            {
                if (extensions.Any(ext => String.Compare(ext, Path.GetExtension(f), true) == 0))
                {
                    yield return f;
                }
            }

            foreach (string d in Directory.GetDirectories(dir))
            {
                foreach (string f in EnumerateAssetFilesAt(d, extensions))
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

                AssetDatabase.DeleteAsset(f);
            }
        }

    }
}
