using System.Collections;
using System.IO;
using System.Linq;
using System.Xml;
using System;

using UnityEditor;
using UnityEngine;

namespace Tiled2Unity
{
    // Assets that are imported to "Tiled2Unity/..." will use this post processor
    public class TiledAssetPostProcessor : AssetPostprocessor
    {
        private static bool UseThisImporter(string assetPath)
        {
            // Certain file types are ignored by this asset post processor (i.e. scripts)
            string[] ignoreThese = { ".cs", ".txt", };
            if (ignoreThese.Any(ext => String.Compare(ext, Path.GetExtension(assetPath), true) == 0))
            {
                return false;
            }

            // Note: This importer can never be used if UNITY_WEBPLAYER is the configuration
            bool useThisImporter = false;

            // Is this file relative to our Tiled2Unity export marker file?
            // If so, then we want to use this asset postprocessor
            string path = assetPath;
            while (!String.IsNullOrEmpty(path))
            {
                path = Path.GetDirectoryName(path);
                string exportMarkerPath = Path.Combine(path, "Tiled2Unity.export.txt");
                if (File.Exists(exportMarkerPath))
                {
                    // This is a file under the Tiled2Unity root.
                    useThisImporter = true;
                    break;
                }
            }

            if (useThisImporter == true)
            {
#if UNITY_WEBPLAYER
                String warning = String.Format("Importing '{0}' but Tiled2Unity files cannot be imported with the WebPlayer[deprecated] platform.\nHowever, You can use Tiled2Unity prefabs imported by another platform.", assetPath);
                Debug.LogWarning(warning);
                return false;
#else
                return true;
#endif
            }

            return false;
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

#if !UNITY_WEBPLAYER
                using (ImportTiled2Unity t2uImporter = new ImportTiled2Unity(imported))
                {
                    if (t2uImporter.IsTiled2UnityFile())
                    {
                        // Start the import process. This will trigger textures and meshes to be imported as well.
                        t2uImporter.ImportBegin(imported);
                    }
                    else if (t2uImporter.IsTiled2UnityTexture())
                    {
                        // A texture was imported and the material assigned to it may need to be fixed
                        t2uImporter.TextureImported(imported);
                    }
                    else if (t2uImporter.IsTiled2UnityWavefrontObj())
                    {
                        // Now that the mesh has been imported we will build the prefab
                        t2uImporter.MeshImported(imported);
                    }
                    else if (t2uImporter.IsTiled2UnityPrefab())
                    {
                        // Now the the prefab is built and imported we are done
                        t2uImporter.ImportFinished(imported);
                        Debug.Log(string.Format("Imported prefab from Tiled map editor: {0}", imported));
                    }
                }
#endif
            }
        }

        private void OnPreprocessModel()
        {
            if (!UseThisImporter())
                return;

            ModelImporter modelImporter = this.assetImporter as ModelImporter;

            // Keep normals otherwise Unity will complain about needing them.
            // Normals may not be a bad idea anyhow
#if UNITY_5_0 || UNITY_5_1 || UNITY_5_2
            modelImporter.normalImportMode = ModelImporterTangentSpaceMode.Import;
#else
            modelImporter.importNormals = ModelImporterNormals.Import;
#endif

            // Don't need animations or tangents.
            modelImporter.generateAnimations = ModelImporterGenerateAnimations.None;
            modelImporter.animationType = ModelImporterAnimationType.None;
#if UNITY_5_0 || UNITY_5_1 || UNITY_5_2
            modelImporter.tangentImportMode = ModelImporterTangentSpaceMode.None;
#else
            modelImporter.importTangents = ModelImporterTangents.None;
#endif

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

                // No shadows
                mr.receiveShadows = false;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // No probes
                mr.useLightProbes = false;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
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

#if !UNITY_WEBPLAYER
            ImportTiled2Unity importer = new ImportTiled2Unity(this.assetPath);
            return importer.FixMaterialForMeshRenderer(rootName, renderer);
#else
            return null;
#endif
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
