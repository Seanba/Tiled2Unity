using System;
using System.Collections.Generic;
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
            // This is fixup method due to materials and textures, under some conditions, being imported out of order
            Texture2D texture2d = AssetDatabase.LoadAssetAtPath(texturePath, typeof(Texture2D)) as Texture2D;
            Material material = AssetDatabase.LoadAssetAtPath(ImportUtils.GetMaterialPath(texturePath), typeof(Material)) as Material;
            material.SetTexture("_MainTex", texture2d);
        }
    }
}
