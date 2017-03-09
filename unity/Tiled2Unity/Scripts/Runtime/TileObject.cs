using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Tiled2Unity
{
    public class TileObject : Tiled2Unity.TmxObject
    {
        [Header("Tmx Tile Object Properties (Raw Data)")]
        public bool TmxFlippingHorizontal;
        public bool TmxFlippingVertical;

        [Header("Tile Object Properties")]
        [Tooltip("Imported Tile Width (after scaling and transforms applied)")]
        public float TileWidth = 0.0f;

        [Tooltip("Imported Tile Height (after scaling and transforms applied)")]
        public float TileHeight = 0.0f;
    }
}
