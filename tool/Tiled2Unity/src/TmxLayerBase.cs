using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    // There are several different "layer" types in Tiled that share some behaviour (tile layer, object layer, image layer)
    // (In Tiled2Unity we treat image layers as a special case of tile layer)
    public class TmxLayerBase : TmxHasProperties
    {
        public TmxProperties Properties { get; protected set; }

        public int XmlElementIndex { get; protected set; }

        public string SortingLayerName { get; set; }
        public int SortingOrder { get; set; }
    }
}
