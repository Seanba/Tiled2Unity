using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using UnityEditor;
using UnityEngine;

namespace Tiled2Unity
{
    // Handled a texture being imported
    partial class ImportTiled2Unity
    {
        public void TextureImported(string texturePath)
        {
            // Find the import behaviour that was waiting on this texture to be imported
            string asset = System.IO.Path.GetFileName(texturePath);
            foreach (var importComponent in ImportBehaviour.EnumerateImportBehaviors_ByWaitingTexture(asset))
            {
                // The texture has finished loading. Keep track of that status.
                if (!importComponent.ImportComplete_Textures.Contains(asset, StringComparer.OrdinalIgnoreCase))
                {
                    importComponent.ImportComplete_Textures.Add(asset);
                }

                // Are we done importing all textures? If so then start importing materials.
                if (importComponent.IsTextureImportingCompleted())
                {
                    ImportAllMaterials(importComponent);
                }
            }
        }

        private void ImportAllTextures(Tiled2Unity.ImportBehaviour importComponent)
        {
            // Textures need to be imported before we can create or import materials
            foreach (var xmlImportTexture in importComponent.XmlDocument.Root.Elements("ImportTexture"))
            {
                string filename = ImportUtils.GetAttributeAsString(xmlImportTexture, "filename");
                string data = xmlImportTexture.Value;
                byte[] bytes = ImportUtils.Base64ToBytes(data);

                // Keep track that we are importing this texture
                if (!importComponent.ImportWait_Textures.Contains(filename, StringComparer.OrdinalIgnoreCase))
                {
                    importComponent.ImportWait_Textures.Add(filename);
                }

                // Start the import process for this texture
                string pathToSave = GetTextureAssetPath(filename);
                ImportUtils.ReadyToWrite(pathToSave);
                File.WriteAllBytes(pathToSave, bytes);
                importComponent.ImportTiled2UnityAsset(pathToSave);
            }

            // If we have no textures too import then go to next stage (materials)
            if (importComponent.ImportWait_Textures.Count() == 0)
            {
                ImportAllMaterials(importComponent);
            }
        }
    }
}
