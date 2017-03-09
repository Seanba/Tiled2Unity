using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Tiled2Unity
{
    partial class TmxLayerNode : ITmxVisit
    {
        public static List<TmxLayerNode> ListFromXml(XElement xmlRoot, TmxLayerNode parent, TmxMap tmxMap)
        {
            List<TmxLayerNode> nodes = new List<TmxLayerNode>();

            foreach (var xmlNode in xmlRoot.Elements())
            {
                TmxLayerNode layerNode = null;

                if (xmlNode.Name == "layer" || xmlNode.Name == "imagelayer")
                {
                    layerNode = TmxLayer.FromXml(xmlNode, parent, tmxMap);
                }
                else if (xmlNode.Name == "objectgroup")
                {
                    layerNode = TmxObjectGroup.FromXml(xmlNode, parent, tmxMap);
                }
                else if (xmlNode.Name == "group")
                {
                    layerNode = TmxGroupLayer.FromXml(xmlNode, parent, tmxMap);
                }

                // If the layer is visible then add it to our list
                if (layerNode != null && layerNode.Visible)
                {
                    nodes.Add(layerNode);
                }
            }

            return nodes;
        }

        protected void FromXmlInternal(XElement xml)
        {
            // Get common elements amoung layer nodes from xml
            this.Name = TmxHelper.GetAttributeAsString(xml, "name", "");
            this.Visible = TmxHelper.GetAttributeAsInt(xml, "visible", 1) == 1;
            this.Opacity = TmxHelper.GetAttributeAsFloat(xml, "opacity", 1);

            // Get the offset
            PointF offset = new PointF(0, 0);
            offset.X = TmxHelper.GetAttributeAsFloat(xml, "offsetx", 0);
            offset.Y = TmxHelper.GetAttributeAsFloat(xml, "offsety", 0);
            this.Offset = offset;

            // Get all the properties
            this.Properties = TmxProperties.FromXml(xml);

            // Set the "ignore" setting on this object group
            this.Ignore = this.Properties.GetPropertyValueAsEnum<IgnoreSettings>("unity:ignore", IgnoreSettings.False);

            // Explicit sorting properties
            this.ExplicitSortingLayerName = this.Properties.GetPropertyValueAsString("unity:sortingLayerName", "");
            if (this.Properties.PropertyMap.ContainsKey("unity:sortingOrder"))
            {
                this.ExplicitSortingOrder = this.Properties.GetPropertyValueAsInt("unity:sortingOrder");
            }

            // Are we using a unity:layer override?
            this.UnityLayerOverrideName = this.Properties.GetPropertyValueAsString("unity:layer", "");

            // Add all our children
            this.LayerNodes = TmxLayerNode.ListFromXml(xml, this, this.TmxMap);
        }
    }
}
