#if !UNITY_WEBPLAYER
// Note: This parital class is not compiled in for WebPlayer builds.
// The Unity Webplayer is deprecated. If you *must* use it then make sure Tiled2Unity assets are imported via another build target first.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using UnityEditor;
using UnityEngine;


namespace Tiled2Unity
{
    // Concentrates on the Xml file being imported
    partial class ImportTiled2Unity
    {
        public static readonly string ThisVersion = "1.0.4.2";

        // Called when Unity detects the *.tiled2unity.xml file needs to be (re)imported
        public void ImportBegin(string xmlPath)
        {
            // Normally, this is where we first create the XmlDocument for the whole import.
            ImportBehaviour importBehaviour = ImportBehaviour.FindOrCreateImportBehaviour(xmlPath);
            XDocument xml = importBehaviour.XmlDocument;
            if (xml == null)
            {
                Debug.LogErrorFormat("GameObject {0} not successfully initialized. Is it left over from a previous import. Try removing from scene are re-importing {1}.", importBehaviour.gameObject.name, xmlPath);
                return;
            }

            CheckVersion(xmlPath, xml);

            // Import asset files.
            // (Note that textures should be imported before meshes)
            ImportTexturesFromXml(xml);
            CreateMaterialsFromInternalTextures(xml);
            ImportMeshesFromXml(xml);
        }

        // Called when the import process has completed and we have a prefab ready to go
        public void ImportFinished(string prefabPath)
        {
            // Get at the import behavour tied to this prefab and remove it from the scene
            string xmlAssetPath = GetXmlImportAssetPath(prefabPath);
            ImportBehaviour importBehaviour = ImportBehaviour.FindOrCreateImportBehaviour(xmlAssetPath);
            importBehaviour.DestroyImportBehaviour();
        }

        private void CheckVersion(string xmlPath, XDocument xml)
        {
            string version = xml.Root.Attribute("version").Value;
            if (version != ThisVersion)
            {
                Debug.LogWarning(string.Format("Imported Tiled2Unity file '{0}' was exported with version {1}. We are expecting version {2}", xmlPath, version, ThisVersion));
            }
        }

        private void ImportTexturesFromXml(XDocument xml)
        {
            var texData = xml.Root.Elements("ImportTexture");
            foreach (var tex in texData)
            {
                string name = tex.Attribute("filename").Value;
                string data = tex.Value;

                // The data is gzip compressed base64 string. We need the raw bytes.
                //byte[] bytes = ImportUtils.GzipBase64ToBytes(data);
                byte[] bytes = ImportUtils.Base64ToBytes(data);

                // Save and import the texture asset
                {
                    string pathToSave = GetTextureAssetPath(name);
                    ImportUtils.ReadyToWrite(pathToSave);
                    File.WriteAllBytes(pathToSave, bytes);
                    AssetDatabase.ImportAsset(pathToSave, ImportAssetOptions.ForceSynchronousImport);
                }

                // Create a material if needed in prepartion for the texture being successfully imported
                {
                    string materialPath = GetMaterialAssetPath(name);
                    Material material = AssetDatabase.LoadAssetAtPath(materialPath, typeof(Material)) as Material;
                    if (material == null)
                    {
                        // We need to create the material afterall
                        // Use our custom shader
                        material = new Material(Shader.Find("Tiled/TextureTintSnap"));
                        ImportUtils.ReadyToWrite(materialPath);
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                }
            }
        }

        private void CreateMaterialsFromInternalTextures(XDocument xml)
        {
            var texData = xml.Root.Elements("InternalTexture");
            foreach (var tex in texData)
            {
                string texAssetPath = tex.Attribute("assetPath").Value;
                string materialPath = GetMaterialAssetPath(texAssetPath);

                Material material = AssetDatabase.LoadAssetAtPath(materialPath, typeof(Material)) as Material;
                if (material == null)
                {
                    // Create our material
                    material = new Material(Shader.Find("Tiled/TextureTintSnap"));

                    // Assign to it the texture that is already internal to our Unity project
                    Texture2D texture2d = AssetDatabase.LoadAssetAtPath(texAssetPath, typeof(Texture2D)) as Texture2D;
                    material.SetTexture("_MainTex", texture2d);

                    // Write the material to our asset database
                    ImportUtils.ReadyToWrite(materialPath);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
            }
        }

        private void ImportMeshesFromXml(XDocument xml)
        {
            var meshData = xml.Root.Elements("ImportMesh");
            foreach (var mesh in meshData)
            {
                // We're going to create/write a file that contains our mesh data as a Wavefront Obj file
                // The actual mesh will be imported from this Obj file

                string fname = mesh.Attribute("filename").Value;
                string data = mesh.Value;

                // The data is in base64 format. We need it as a raw string.
                string raw = ImportUtils.Base64ToString(data);

                // Save and import the asset
                string pathToMesh = GetMeshAssetPath(fname);
                ImportUtils.ReadyToWrite(pathToMesh);
                File.WriteAllText(pathToMesh, raw, Encoding.UTF8);
                AssetDatabase.ImportAsset(pathToMesh, ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
#endif