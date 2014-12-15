using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    class TiledMapExpoterUtils
    {
        public static string UnityFriendlyMeshName(TmxMap map, string layerName, string imageName)
        {
            // Trying really hard to come up with a mesh-naming scheme that Unity won't rename on us
            // Using a combination of proper layer and image names won't work so stick with safe ascii and no spaces
            string meshName = map.GetMeshName(layerName, imageName);
            meshName = meshName.Replace(" ", "_");
            return meshName;
        }
    }
}
