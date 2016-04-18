using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml;

using UnityEditor;
using UnityEngine;

namespace Tiled2Unity
{
    partial class ImportTiled2Unity : IDisposable
    {
        private string fullPathToFile = "";
        private string pathToTiled2UnityRoot = "";
        private string assetPathToTiled2UnityRoot = "";

        public ImportTiled2Unity(string file)
        {
            this.fullPathToFile = Path.GetFullPath(file);

            // Discover the root of the Tiled2Unity scripts and assets
            this.pathToTiled2UnityRoot = Path.GetDirectoryName(this.fullPathToFile);
            int index = this.pathToTiled2UnityRoot.LastIndexOf("Tiled2Unity", StringComparison.InvariantCultureIgnoreCase);
            if (index == -1)
            {
                Debug.LogError(String.Format("There is an error with your Tiled2Unity install. Could not find Tiled2Unity folder in {0}", file));
            }
            else
            {
                this.pathToTiled2UnityRoot = this.pathToTiled2UnityRoot.Remove(index + "Tiled2Unity".Length);
            }

            this.fullPathToFile = this.fullPathToFile.Replace(Path.DirectorySeparatorChar, '/');
            this.pathToTiled2UnityRoot = this.pathToTiled2UnityRoot.Replace(Path.DirectorySeparatorChar, '/');

            // Figure out the path from "Assets" to "Tiled2Unity" root folder
            this.assetPathToTiled2UnityRoot = this.pathToTiled2UnityRoot.Remove(0, Application.dataPath.Count());
            this.assetPathToTiled2UnityRoot = "Assets" + this.assetPathToTiled2UnityRoot;
        }

        public bool IsTiled2UnityFile()
        {
            return this.fullPathToFile.EndsWith(".tiled2unity.xml");
        }

        public bool IsTiled2UnityTexture()
        {
            bool startsWith = this.fullPathToFile.Contains("/Tiled2Unity/Textures/");
            bool endsWithTxt = this.fullPathToFile.EndsWith(".txt");
            return startsWith && !endsWithTxt;
        }

        public bool IsTiled2UnityWavefrontObj()
        {
            bool contains = this.fullPathToFile.Contains("/Tiled2Unity/Meshes/");
            bool endsWith = this.fullPathToFile.EndsWith(".obj");
            return contains && endsWith;
        }

        public bool IsTiled2UnityPrefab()
        {
            bool startsWith = this.fullPathToFile.Contains("/Tiled2Unity/Prefabs/");
            bool endsWith = this.fullPathToFile.EndsWith(".prefab");
            return startsWith && endsWith;
        }

        public string GetMeshAssetPath(string file)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string meshAsset = String.Format("{0}/Meshes/{1}.obj", this.assetPathToTiled2UnityRoot, name);
            return meshAsset;
        }

        public string GetMaterialAssetPath(string file)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string materialAsset = String.Format("{0}/Materials/{1}.mat", this.assetPathToTiled2UnityRoot, name);
            return materialAsset;
        }

        public string GetTextureAssetPath(string filename)
        {
            // Keep the extention given (png, tga, etc.)
            filename = Path.GetFileName(filename);
            string textureAsset = String.Format("{0}/Textures/{1}", this.assetPathToTiled2UnityRoot, filename);
            return textureAsset;
        }

        public string GetXmlImportAssetPath(string name)
        {
#if !UNITY_WEBPLAYER
            name = Tiled2Unity.ImportBehaviour.GetFilenameWithoutTiled2UnityExtension(name);
#endif
            string xmlAsset = String.Format("{0}/Imported/{1}.tiled2unity.xml", this.assetPathToTiled2UnityRoot, name);
            return xmlAsset;
        }

        public string GetPrefabAssetPath(string name, bool isResource, string extraPath)
        {
            string prefabAsset = "";
            if (isResource)
            {
                if (String.IsNullOrEmpty(extraPath))
                {
                    // Put the prefab into a "Resources" folder so it can be instantiated through script
                    prefabAsset = String.Format("{0}/Prefabs/Resources/{1}.prefab", this.assetPathToTiled2UnityRoot, name);
                }
                else
                {
                    // Put the prefab into a "Resources/extraPath" folder so it can be instantiated through script
                    prefabAsset = String.Format("{0}/Prefabs/Resources/{1}/{2}.prefab", this.assetPathToTiled2UnityRoot, extraPath, name);
                }
            }
            else
            {
                prefabAsset = String.Format("{0}/Prefabs/{1}.prefab", this.assetPathToTiled2UnityRoot, name);
            }

            return prefabAsset;
        }

        public void Dispose()
        {
        }
    }
}
