#if UNITY_4_0 || UNITY_4_0_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2
#define T2U_USE_LEGACY_IMPORTER
#else
#undef T2U_USE_LEGACY_IMPORTER
#endif

#if UNITY_4_0 || UNITY_4_0_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3
#define T2U_USE_LIGHT_PROBES_API
#else
#undef T2U_USE_LIGHT_PROBES_API
#endif

#if UNITY_4_0 || UNITY_4_0_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3 || UNITY_5_4
#define T2U_USE_5_4_API
#else
#endif

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
#if UNITY_WEBPLAYER
            String warning = String.Format("Can not import through Tiled2Unity using the WebPlayer platform. This is depecrated by Unity Technologies and is no longer supported. Go to File -> Build Settings... and switch to another platform. (You can switch back to Web Player after importing.). File: {0}", assetPath);
            Debug.LogWarning(warning);
            return false;
#else
            // Certain file types are ignored by this asset post processor (i.e. scripts)
            // (Note that an empty string as the extension is a folder)
            string[] ignoreThese = { ".cs", ".txt",  ".shader", "", };
            if (ignoreThese.Any(ext => String.Compare(ext, System.IO.Path.GetExtension(assetPath), true) == 0))
            {
                return false;
            }

            // *.tiled2unity.xml files are always supported by this processor
            if (assetPath.EndsWith(".tiled2unity.xml", StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }

            // All other files can only use this post processor if their import was requested by an ImportBehaviour
            return ImportBehaviour.IsAssetBeingImportedByTiled2Unity(assetPath);
#endif
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
                        t2uImporter.ImportBegin(imported, t2uImporter);
                    }
                    else if (t2uImporter.IsTiled2UnityTexture())
                    {
                        // A texture was imported. Once all textures are imported then we'll import materials.
                        t2uImporter.TextureImported(imported);
                    }
                    else if (t2uImporter.IsTiled2UnityMaterial())
                    {
                        // A material was imported. Once all materials are imported then we'll import meshes.
                        t2uImporter.MaterialImported(imported);
                    }
                    else if (t2uImporter.IsTiled2UnityWavefrontObj())
                    {
                        // A mesh was imported. Once all meshes are imported we'll import the prefabs.
                        t2uImporter.MeshImported(imported);
                    }
                    else if (t2uImporter.IsTiled2UnityPrefab())
                    {
                        // A prefab was imported. Once all prefabs are imported then the import is complete.
                        t2uImporter.PrefabImported(imported);
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
#if T2U_USE_LEGACY_IMPORTER
            modelImporter.normalImportMode = ModelImporterTangentSpaceMode.Import;
            modelImporter.tangentImportMode = ModelImporterTangentSpaceMode.None;
#else
            modelImporter.importNormals = ModelImporterNormals.Import;
            modelImporter.importTangents = ModelImporterTangents.None;
#endif

            modelImporter.importBlendShapes = false;

            // Don't need animations or tangents.
            modelImporter.generateAnimations = ModelImporterGenerateAnimations.None;
            modelImporter.animationType = ModelImporterAnimationType.None;

            // Do not need mesh colliders on import.
            modelImporter.addCollider = false;

            // We will create and assign our own materials.
            // This gives us more control over their construction.
            modelImporter.importMaterials = false;

#if UNITY_5_6_OR_NEWER
            modelImporter.keepQuads = true;
#endif
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
#if T2U_USE_LEGACY_IMPORTER
                mr.castShadows = false;
#else
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
#endif

#if !T2U_USE_LEGACY_IMPORTER
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
#endif

#if T2U_USE_LIGHT_PROBES_API
                // No probes
                mr.useLightProbes = false;
#else
                //mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
#endif
            }
        }

        private UnityEngine.Material OnAssignMaterialModel(Material defaultMaterial, Renderer renderer)
        {
            if (!UseThisImporter())
                return null;

            // What is the parent mesh name?
            string rootName = System.IO.Path.GetFileNameWithoutExtension(this.assetPath);

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
            textureImporter.npotScale = TextureImporterNPOTScale.None;
            textureImporter.convertToNormalmap = false;
            textureImporter.alphaIsTransparency = true;
            textureImporter.spriteImportMode = SpriteImportMode.None;
            textureImporter.mipmapEnabled = false;
            textureImporter.filterMode = FilterMode.Point;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
#if T2U_USE_5_4_API
            textureImporter.lightmap = false;
            textureImporter.grayscaleToAlpha = false;
            textureImporter.linearTexture = false;
            textureImporter.generateCubemap = TextureImporterGenerateCubemap.None;
            textureImporter.textureFormat = TextureImporterFormat.AutomaticTruecolor;
#else
            textureImporter.textureType = TextureImporterType.Default;
            textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;
            textureImporter.sRGBTexture = false;
            textureImporter.textureShape = TextureImporterShape.Texture2D;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
#endif
        }

    }
}
