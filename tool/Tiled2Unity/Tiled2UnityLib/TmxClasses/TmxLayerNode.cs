using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    // There are several different "layer" types in Tiled that share some behaviour (tile layer, object layer, image layer, group layer)
    // (In Tiled2Unity we treat image layers as a special case of tile layer)
    public abstract partial class TmxLayerNode : TmxHasProperties
    {
        public enum IgnoreSettings
        {
            False,      // Ingore nothing (layer fully-enabled)
            True,       // Ignore everything (like layer doesn't exist)
            Collision,  // Ignore collision on layer
            Visual,     // Ignore visual on layer
        };

        public TmxLayerNode ParentNode { get; private set; }
        public TmxMap TmxMap { get; private set; }

        public string Name { get; protected set; }
        public bool Visible { get; protected set; }
        public float Opacity { get; protected set; }
        public PointF Offset { get; protected set; }
        public IgnoreSettings Ignore { get; protected set; }

        public TmxProperties Properties { get; protected set; }

        // Helps with drawing order. User can be explicit through unity:sortingLayerName and unity:sortingOrder properties.
        public string ExplicitSortingLayerName { get; set; }
        public int? ExplicitSortingOrder { get; set; }

        public int DrawOrderIndex { get; set; }
        public int DepthBufferIndex { get; set; }

        public string UnityLayerOverrideName { get; protected set; }

        // Layer nodes may have a list of other layer nodes
        public List<TmxLayerNode> LayerNodes { get; protected set; }

        public TmxLayerNode(TmxLayerNode parent, TmxMap tmxMap)
        {
            this.DrawOrderIndex = -1;
            this.DepthBufferIndex = -1;
            this.ParentNode = parent;
            this.TmxMap = tmxMap;
            this.LayerNodes = new List<TmxLayerNode>();
        }

        public PointF GetCombinedOffset()
        {
            PointF offset = this.Offset;
            TmxLayerNode parent = this.ParentNode;
            while (parent != null)
            {
                offset = TmxMath.AddPoints(offset, parent.Offset);
                parent = parent.ParentNode;
            }

            return offset;
        }

        public string GetSortingLayerName()
        {
            // Do we have our own sorting layer name?
            if (!String.IsNullOrEmpty(this.ExplicitSortingLayerName))
                return this.ExplicitSortingLayerName;

            // If not then rely on the parent
            if (this.ParentNode != null)
            {
                return this.ParentNode.GetSortingLayerName();
            }

            // Default is an empty string
            return "";
        }

        public int GetSortingOrder()
        {
            // Do we have our own explicit ordering?
            if (this.ExplicitSortingOrder.HasValue)
            {
                return this.ExplicitSortingOrder.Value;
            }

            // Use our draw order index
            return this.DrawOrderIndex;
        }

        // The child class must implement Visit abstraction
        public abstract void Visit(ITmxVisitor visitor);
    }
}
