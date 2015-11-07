using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Tiled2Unity
{
    // Allows us to set shader properties on the Tiled mesh
    // Note: Keep default shader properties imported from Tiled to avoid breaking batching
    // For example, keeping layer opacity to 1.0 (the default) will keep layers using the same material in the same draw call
    public class TiledInitialShaderProperties : MonoBehaviour
    {
        [Range(0, 1)]
        public float InitialOpacity = 1.0f;

        private void Awake()
        {
            // If supported in the sahder set our opacity
            // (Keep opacity at 1.0 to avoid copying the material)
            MeshRenderer meshRendrer = this.gameObject.GetComponent<MeshRenderer>();
            if (this.InitialOpacity != 1.0f && meshRendrer.material.HasProperty("_Color"))
            {
                meshRendrer.material.SetColor("_Color", new Color(1, 1, 1, this.InitialOpacity));
            }
        }
    }
}
