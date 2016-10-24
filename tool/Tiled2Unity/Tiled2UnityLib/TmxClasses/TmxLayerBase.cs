using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    // There are several different "layer" types in Tiled that share some behaviour (tile layer, object layer, image layer)
    // (In Tiled2Unity we treat image layers as a special case of tile layer)
    public class TmxLayerBase : TmxHasProperties
    {
        public enum IgnoreSettings
        {
            False,      // Ingore nothing (layer fully-enabled)
            True,       // Ignore everything (like layer doesn't exist)
            Collision,  // Ignore collision on layer
            Visual,     // Ignore visual on layer
        };

        public TmxMap TmxMap { get; private set; }

        public string Name { get; protected set; }
        public bool Visible { get; protected set; }
        public float Opacity { get; protected set; }
        public PointF Offset { get; protected set; }
        public IgnoreSettings Ignore { get; protected set; }

        public TmxProperties Properties { get; protected set; }

        public int XmlElementIndex { get; protected set; }

        public string SortingLayerName { get; set; }
        public int SortingOrder { get; set; }

        public string UnityLayerOverrideName { get; protected set; }

        public TmxLayerBase(TmxMap tmxMap)
        {
            this.TmxMap = tmxMap;
        }
    }
}
