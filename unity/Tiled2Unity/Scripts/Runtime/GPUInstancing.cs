using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Tiled2Unity
{
    [ExecuteInEditMode]
    public class GPUInstancing : MonoBehaviour
    {
        [Range(0, 1)]
        public float Opacity = 1.0f;

        private void Awake()
        {
            SetPropertyBlock();
        }

        private void OnValidate()
        {
            SetPropertyBlock();
        }

        private void SetPropertyBlock()
        {
            // Allows us to share a material with different opacity settings
#if UNITY_5_6_OR_NEWER
            MeshRenderer meshRenderer = this.gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                MaterialPropertyBlock props = new MaterialPropertyBlock();
                props.SetColor("_Color", new Color(1, 1, 1, this.Opacity));
                meshRenderer.SetPropertyBlock(props);
            }
#endif
        }

    }
}
