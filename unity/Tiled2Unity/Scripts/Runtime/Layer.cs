using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Tiled2Unity
{
    // Base class for Layer types (Tile, Object) from Tiled
    public abstract class Layer : MonoBehaviour
    {
        public Vector2 Offset;
    }
}
