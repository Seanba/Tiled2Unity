#if !UNITY_WEBPLAYER
// Note: This behaviour cannot be used in WebPlayer
using System;

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using UnityEditor;
#endif

using UnityEngine;

namespace Tiled2Unity
{
    // Class to help us manage the import status when a *.tiled2unity.xml file is (re)imported
    // Also holds onto the XML file in memory so that we don't have to keep opening it (an expensive operation) when different parts of the import process needs it.
    // This is a *temporary* behaviour we add to the hierarchy only while importing. It should not be around for runtime.
    public class ImportBehaviour : MonoBehaviour
    {
        public string Tiled2UnityXmlPath = "";
        public string ExporterTiled2UnityVersion = "tiled2unity.version.not.set";

#if UNITY_EDITOR

        // List of asset names we are waiting on to be imported. This helps us keep import process in order, especially if user re-imports their whole project.
        public List<string> ImportWait_Textures = new List<string>();
        public List<string> ImportWait_Materials = new List<string>();
        public List<string> ImportWait_Meshes = new List<string>();
        public List<string> ImportWait_Prefabs = new List<string>();

        // List of asset names that have been imported
        public List<string> ImportComplete_Textures = new List<string>();
        public List<string> ImportComplete_Materials = new List<string>();
        public List<string> ImportComplete_Meshes = new List<string>();
        public List<string> ImportComplete_Prefabs = new List<string>();

        // List of all assets paths we are importing
        public List<string> ImportingAssets = new List<string>();

        public XDocument XmlDocument = null;

        // List of warnings and errors collected over the import process
        private List<string> ImportWarnings = new List<string>();
        private List<string> ImportErrors = new List<string>();

        // The same texture may be imported by multiple import behaviours
        public static IEnumerable<ImportBehaviour> EnumerateImportBehaviors_ByWaitingTexture(string assetName)
        {
            foreach (var component in GameObject.FindObjectsOfType<Tiled2Unity.ImportBehaviour>())
            {
                if (component.ImportWait_Textures.Contains(assetName, StringComparer.OrdinalIgnoreCase))
                {
                    yield return component;
                }
            }
        }

        // The same material may be imported by multiple import behaviours
        public static IEnumerable<ImportBehaviour> EnumerateImportBehaviors_ByWaitingMaterial(string assetName)
        {
            foreach (var component in GameObject.FindObjectsOfType<Tiled2Unity.ImportBehaviour>())
            {
                if (component.ImportWait_Materials.Contains(assetName, StringComparer.OrdinalIgnoreCase))
                {
                    yield return component;
                }
            }
        }

        // Meshes are guarenteed to be unique
        public static ImportBehaviour FindImportBehavior_ByWaitingMesh(string assetName)
        {
            foreach (var component in GameObject.FindObjectsOfType<Tiled2Unity.ImportBehaviour>())
            {
                if (component.ImportWait_Meshes.Contains(assetName, StringComparer.OrdinalIgnoreCase))
                {
                    return component;
                }
            }

            return null;
        }

        // Prefabs are unique
        public static ImportBehaviour FindImportBehavior_ByWaitingPrefab(string assetName)
        {
            foreach (var component in GameObject.FindObjectsOfType<Tiled2Unity.ImportBehaviour>())
            {
                if (component.ImportWait_Prefabs.Contains(assetName, StringComparer.OrdinalIgnoreCase))
                {
                    return component;
                }
            }

            return null;
        }

        public static bool IsAssetBeingImportedByTiled2Unity(string assetPath)
        {
            foreach (var component in GameObject.FindObjectsOfType<Tiled2Unity.ImportBehaviour>())
            {
                if (component.ImportingAssets.Contains(assetPath, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsTextureImportingCompleted()
        {
            return this.ImportComplete_Textures.Count == this.ImportWait_Textures.Count;
        }

        public bool IsMaterialImportingCompleted()
        {
            return this.ImportComplete_Materials.Count == this.ImportWait_Materials.Count;
        }

        public bool IsMeshImportingCompleted()
        {
            return this.ImportComplete_Meshes.Count == this.ImportWait_Meshes.Count;
        }

        public bool IsPrefabImportingCompleted()
        {
            return this.ImportComplete_Prefabs.Count == this.ImportWait_Prefabs.Count;
        }

        public void ImportTiled2UnityAsset(string assetPath)
        {
            if (!this.ImportingAssets.Contains(assetPath, StringComparer.OrdinalIgnoreCase))
            {
                this.ImportingAssets.Add(assetPath);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        public void DestroyImportBehaviour()
        {
            UnityEngine.Object.DestroyImmediate(this.gameObject);
        }

        public void RecordWarning(string fmt, params object[] args)
        {
            string warning = String.Format(fmt, args);
            Debug.LogWarning(warning);
            this.ImportWarnings.Add(warning);
        }

        public void RecordError(string fmt, params object[] args)
        {
            string error = String.Format(fmt, args);
            Debug.LogError(error);
            this.ImportErrors.Add(error);
        }

        public void ReportPrefabImport(string prefabPath)
        {
            // Report any warnings or errors
            Action<object> func = Debug.Log;
            if (this.ImportWarnings.Count > 0)
                func = Debug.LogWarning;
            if (this.ImportErrors.Count > 0)
                func = Debug.LogError;

            StringBuilder msg = new StringBuilder();
            msg.AppendFormat("Imported prefab '{0}' from '{1}' with {2} warnings and {3} errors.\n", prefabPath, this.Tiled2UnityXmlPath, this.ImportWarnings.Count, this.ImportErrors.Count);

            foreach (string error in this.ImportErrors)
            {
                msg.AppendLine(error);
            }

            foreach (string warning in this.ImportWarnings)
            {
                msg.AppendLine(warning);
            }

            func(msg.ToString());
        }

#endif

        // In case this behaviour leaks out of an import and into the runtime, complain.
        private void Update()
        {
            Debug.LogError(String.Format("ImportBehaviour based on '{0}' left in scene after importing. Check if import was successful and remove this object from scene {1}", this.Tiled2UnityXmlPath, this.gameObject.name));
        }

    }
}
#endif // if UNITY_WEBPLAYER