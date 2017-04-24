#if !UNITY_WEBPLAYER
// Note: This parital class is not compiled in for WebPlayer builds.
// The Unity Webplayer is deprecated. If you *must* use it then make sure Tiled2Unity assets are imported via another build target first.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using UnityEditor;
using UnityEngine;


namespace Tiled2Unity
{
    // Partial class for the importer that deals with Materials
    partial class ImportTiled2Unity
    {
        public void MaterialImported(string materialPath)
        {
            // Find the import behaviour that was waiting on this material to be imported
            string asset = System.IO.Path.GetFileName(materialPath);
            foreach (var importComponent in ImportBehaviour.EnumerateImportBehaviors_ByWaitingMaterial(asset))
            {
                // The material has finished loading. Keep track of that status.
                if (!importComponent.ImportComplete_Materials.Contains(asset, StringComparer.OrdinalIgnoreCase))
                {
                    importComponent.ImportComplete_Materials.Add(asset);
                }

                // Are we done importing all materials? If so then start importing meshes.
                if (importComponent.IsMaterialImportingCompleted())
                {
                    ImportAllMeshes(importComponent);
                }
            }
        }

        // We need to call this while the renderers on the model is having its material assigned to it
        // This is invoked for every submesh in the .obj wavefront mesh
        public UnityEngine.Material FixMaterialForMeshRenderer(string objName, Renderer renderer)
        {
            // Find the import behaviour that is waiting for the mesh to be imported.
            string assetName = objName + ".obj";
            ImportBehaviour importBehavior = ImportBehaviour.FindImportBehavior_ByWaitingMesh(assetName);

            // The mesh to match
            string meshName = renderer.name;

            // Find an assignment that matches the mesh renderer
            var assignMaterials = importBehavior.XmlDocument.Root.Elements("AssignMaterial");
            XElement match = assignMaterials.FirstOrDefault(el => el.Attribute("mesh").Value == meshName);

            if (match == null)
            {
                // The names of our meshes in the AssignMaterials elements may be wrong
                // This happened before when Unity replaced whitespace with underscore in our named meshes
                // That case is handled now, but there may be others
                StringBuilder builder = new StringBuilder();
                builder.AppendFormat("Could not find mesh named '{0}' for material matching\n", renderer.name);
                string choices = String.Join("\n  ", assignMaterials.Select(m => m.Attribute("mesh").Value).ToArray());
                builder.AppendFormat("Choices are:\n  {0}", choices);

                importBehavior.RecordError(builder.ToString());
                return null;
            }

            string materialName = match.Attribute("material").Value + ".mat";
            string materialPath = GetExistingMaterialAssetPath(materialName);

            // Assign the material
            UnityEngine.Material material = AssetDatabase.LoadAssetAtPath(materialPath, typeof(UnityEngine.Material)) as UnityEngine.Material;
            if (material == null)
            {
                importBehavior.RecordError("Could not find material: {0}", materialName);
            }

            return material;
        }

        private void ImportAllMaterials(Tiled2Unity.ImportBehaviour importComponent)
        {
            // Create a material for each texture that has been imported
            foreach (var xmlTexture in importComponent.XmlDocument.Root.Elements("ImportTexture"))
            {
                bool isResource = ImportUtils.GetAttributeAsBoolean(xmlTexture, "isResource", false);

                string textureFile = ImportUtils.GetAttributeAsString(xmlTexture, "filename");
                string materialPath = MakeMaterialAssetPath(textureFile, isResource);
                string materialFile = System.IO.Path.GetFileName(materialPath);

                // Keep track that we importing this material
                if (!importComponent.ImportWait_Materials.Contains(materialFile, StringComparer.OrdinalIgnoreCase))
                {
                    importComponent.ImportWait_Materials.Add(materialFile);
                }

                // Create the material
                UnityEngine.Material material = CreateMaterialFromXml(xmlTexture, importComponent);

                // Assign the texture to the material
                {
                    string textureAsset = GetTextureAssetPath(textureFile);
                    AssignTextureAssetToMaterial(material, materialFile, textureAsset, importComponent);
                }

                ImportUtils.ReadyToWrite(materialPath);
                ImportUtils.CreateOrReplaceAsset(material, materialPath);
                importComponent.ImportTiled2UnityAsset(materialPath);
            }

            // Create a material for each internal texture
            foreach (var xmlInternal in importComponent.XmlDocument.Root.Elements("InternalTexture"))
            {
                bool isResource = ImportUtils.GetAttributeAsBoolean(xmlInternal, "isResource", false);

                string textureAsset = ImportUtils.GetAttributeAsString(xmlInternal, "assetPath");
                string textureFile = System.IO.Path.GetFileName(textureAsset);
                string materialPath = MakeMaterialAssetPath(textureFile, isResource);

                // "Internal textures" may have a unique material name that goes with it
                string uniqueMaterialName = ImportUtils.GetAttributeAsString(xmlInternal, "materialName", "");
                if (!String.IsNullOrEmpty(uniqueMaterialName))
                {
                    materialPath = String.Format("{0}/{1}{2}", Path.GetDirectoryName(materialPath), uniqueMaterialName, Path.GetExtension(materialPath));
                }

                string materialFile = System.IO.Path.GetFileName(materialPath);

                // Keep track that we are importing this material
                if (!importComponent.ImportWait_Materials.Contains(materialFile, StringComparer.OrdinalIgnoreCase))
                {
                    importComponent.ImportWait_Materials.Add(materialFile);
                }

                // Create the material and assign the texture
                UnityEngine.Material material = CreateMaterialFromXml(xmlInternal, importComponent);
                AssignTextureAssetToMaterial(material, materialFile, textureAsset, importComponent);

                ImportUtils.ReadyToWrite(materialPath);
                ImportUtils.CreateOrReplaceAsset(material, materialPath);
                importComponent.ImportTiled2UnityAsset(materialPath);
            }

            // If we have no materials to import then go to next stage (meshes)
            if (importComponent.ImportWait_Materials.Count() == 0)
            {
                ImportAllMeshes(importComponent);
            }
        }

        private void AssignTextureAssetToMaterial(Material material, string materialFile, string textureAsset, ImportBehaviour importComponent)
        {
            Texture2D texture2d = AssetDatabase.LoadAssetAtPath(textureAsset, typeof(Texture2D)) as Texture2D;
            if (texture2d == null)
            {
                importComponent.RecordError("Error creating material '{0}'. Texture was not found: {1}", materialFile, textureAsset);
            }
            material.SetTexture("_MainTex", texture2d);
        }
    }
}
#endif