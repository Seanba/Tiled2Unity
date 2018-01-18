using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Tiled2Unity 
{
    public interface ICustomAfterSave 
    {
        void HandlePrefabSaved(UnityEngine.Object prefab, string prefabName);
    }
}

// Examples
/*
[Tiled2Unity.CustomTiledImporter]
class CustomImporterSavePrefab : Tiled2Unity.ICustomAfterSave
{
    public void HandlePrefabSaved(UnityEngine.Object prefab, string prefabName) 
    {
        // Do nothing
    }
}
*/