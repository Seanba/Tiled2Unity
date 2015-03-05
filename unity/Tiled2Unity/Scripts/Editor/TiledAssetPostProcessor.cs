using System.Collections;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System;

using UnityEditor;
using UnityEngine;

namespace Tiled2Unity
{
    // Assets that are imported to "Assets/Tiled2Unity/..." will use this post processor
    public class TiledAssetPostProcessor : AssetPostprocessor
    {
        private static bool IsTiled2UnityFile(string assetPath)
        {
            return UseThisImporter(assetPath) && assetPath.EndsWith(".tiled2unity.xml");
        }

        private static bool IsTiled2UnityTexture(string assetPath)
        {
            bool startsWith = assetPath.StartsWith("Assets/Tiled2Unity/Textures/");
            bool endsWithTxt = assetPath.EndsWith(".txt");
            return startsWith && !endsWithTxt;
        }

        private static bool IsTiled2UnityWavefrontObj(string assetPath)
        {
            bool startsWith = assetPath.StartsWith("Assets/Tiled2Unity/Meshes/");
            bool endsWith = assetPath.EndsWith(".obj");
            return startsWith && endsWith;
        }

        private static bool IsTiled2UnityPrefab(string assetPath)
        {
            bool startsWith = assetPath.StartsWith("Assets/Tiled2Unity/Prefabs/");
            bool endsWith = assetPath.EndsWith(".prefab");
            return startsWith && endsWith;
        }

        private static bool UseThisImporter(string assetPath)
        {
            return assetPath.StartsWith("Assets/Tiled2Unity");
        }

        private bool UseThisImporter()
        {
            return UseThisImporter(this.assetPath);
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromPath)
        {
            foreach (string imported in importedAssets)
            {
                if (UseThisImporter(imported))
                {
                    //Debug.Log(string.Format("Imported: {0}", imported));
                }
                else
                {
                    continue;
                }

                using (ImportTiled2Unity t2uImporter = new ImportTiled2Unity())
                {
                    if (IsTiled2UnityFile(imported))
                    {
                        // Start the import process by importing our textures and meshes
                        t2uImporter.XmlImported(imported);
                    }
                    else if (IsTiled2UnityTexture(imported))
                    {
                        // A texture was imported and the material assigned to it may need to be fixed
                        t2uImporter.TextureImported(imported);
                    }
                    else if (IsTiled2UnityWavefrontObj(imported))
                    {
                        t2uImporter.MeshImported(imported);
                    }
                    else if (IsTiled2UnityPrefab(imported))
                    {
                        Debug.Log(string.Format("Imported prefab from Tiled map editor: {0}", imported));
                    }
                }
            }
        }

        private void OnPreprocessModel()
        {
            if (!UseThisImporter())
                return;

            ModelImporter modelImporter = this.assetImporter as ModelImporter;

            // Keep normals otherwise Unity will complain about needing them.
            // Normals may not be a bad idea anyhow
            modelImporter.normalImportMode = ModelImporterTangentSpaceMode.Import;

            // Don't need animations or tangents.
            modelImporter.generateAnimations = ModelImporterGenerateAnimations.None;
            modelImporter.animationType = ModelImporterAnimationType.None;
            modelImporter.tangentImportMode = ModelImporterTangentSpaceMode.None;

            // Do not need mesh colliders on import.
            modelImporter.addCollider = false;

            // We will create and assign our own materials.
            // This gives us more control over their construction.
            modelImporter.importMaterials = false;
        }

        private void OnPostprocessModel(GameObject gameObject)
        {
            if (!UseThisImporter())
                return;

            // Each mesh renderer has the ability to set the a sort layer but it takes some work with Unity to expose it.
            foreach (MeshRenderer mr in gameObject.GetComponentsInChildren<MeshRenderer>())
            {
                mr.gameObject.AddComponent<SortingLayerExposed>();

                // Also, no shadows
                mr.receiveShadows = false;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private Material OnAssignMaterialModel(Material defaultMaterial, Renderer renderer)
        {
            if (!UseThisImporter())
                return null;

            // This is the only reliable place to assign materials in the import chain.
            // It kind of sucks because we have to go about making the mesh/material association in a roundabout way.

            // Note: This seems dangerous, but getting to the name of the base gameObject appears to be take some work.
            // The root gameObject, at this point, seems to have "_root" appeneded to it.
            // Once the model if finished being imported it drops this postifx
            // This is something that could change without our knowledge
            string rootName = renderer.transform.root.gameObject.name;
            int rootIndex = rootName.LastIndexOf("_root");
            if (rootIndex != -1)
            {
                rootName = rootName.Remove(rootIndex);
            }

            ImportTiled2Unity importer = new ImportTiled2Unity();
            return importer.FixMaterialForMeshRenderer(rootName, renderer);
        }

        private void OnPreprocessTexture()
        {
            if (!UseThisImporter())
                return;

            if (!string.IsNullOrEmpty(this.assetImporter.userData))
            {
                // The texture has already been exported and we don't want to reset the texture import settings
                // This allows users to change their texture settings and have those changes stick.
                return;
            }

            // Put some dummy UserData on the importer so we know not to apply these settings again.
            this.assetImporter.userData = "tiled2unity";

            TextureImporter textureImporter = this.assetImporter as TextureImporter;
            textureImporter.textureType = TextureImporterType.Advanced;
            textureImporter.npotScale = TextureImporterNPOTScale.None;
            textureImporter.convertToNormalmap = false;
            textureImporter.lightmap = false;
            textureImporter.alphaIsTransparency = true;
            textureImporter.grayscaleToAlpha = false;
            textureImporter.linearTexture = false;
            textureImporter.spriteImportMode = SpriteImportMode.None;
            textureImporter.mipmapEnabled = false;
            textureImporter.generateCubemap = TextureImporterGenerateCubemap.None;
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.textureFormat = TextureImporterFormat.AutomaticTruecolor;
        }

    }
}
