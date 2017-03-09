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
    // Handles a Mesh being imported.
    // At this point we should have everything we need to build out any prefabs for the tiled map object
    partial class ImportTiled2Unity
    {
        public void MeshImported(string objPath)
        {
            // Find the import behaviour that was waiting on this mesh to be imported
            string asset = System.IO.Path.GetFileName(objPath);
            ImportBehaviour importComponent = ImportBehaviour.FindImportBehavior_ByWaitingMesh(asset);
            if (importComponent != null)
            {
                // The mesh has finished loading. Keep track of that status.
                if (!importComponent.ImportComplete_Meshes.Contains(asset, StringComparer.OrdinalIgnoreCase))
                {
                    importComponent.ImportComplete_Meshes.Add(asset);
                }

                // Are we done importing all meshes? If so then start importing prefabs.
                if (importComponent.IsMeshImportingCompleted())
                {
                    ImportAllPrefabs(importComponent, objPath);
                }
            }
        }

        private void ImportAllMeshes(Tiled2Unity.ImportBehaviour importComponent)
        {
            foreach (var xmlMesh in importComponent.XmlDocument.Root.Elements("ImportMesh"))
            {
                // We're going to create/write a file that contains our mesh data as a Wavefront Obj file
                // The actual mesh will be imported from this Obj file
                string file = ImportUtils.GetAttributeAsString(xmlMesh, "filename");
                string data = xmlMesh.Value;

                // Keep track of mesh we're going to import
                if (!importComponent.ImportWait_Meshes.Contains(file, StringComparer.OrdinalIgnoreCase))
                {
                    importComponent.ImportWait_Meshes.Add(file);
                }

                // The data is in base64 format. We need it as a raw string.
                string raw = ImportUtils.Base64ToString(data);

                // Save and import the asset
                string pathToMesh = GetMeshAssetPath(file);
                ImportUtils.ReadyToWrite(pathToMesh);
                File.WriteAllText(pathToMesh, raw, Encoding.UTF8);
                importComponent.ImportTiled2UnityAsset(pathToMesh);
            }

            // If we have no meshes to import then go to next stage
            if (importComponent.ImportWait_Meshes.Count() == 0)
            {
                ImportAllPrefabs(importComponent, null);
            }
        }

    } // end class
} // end namespace
#endif