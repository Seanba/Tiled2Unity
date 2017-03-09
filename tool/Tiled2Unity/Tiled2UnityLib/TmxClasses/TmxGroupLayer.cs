using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Tiled2Unity
{
    // A group layer is a composite of other layer types (i.e. tiled, object, other groups)
    public class TmxGroupLayer : TmxLayerNode
    {
        public TmxGroupLayer(TmxLayerNode parent, TmxMap tmxMap) : base(parent, tmxMap)
        {
        }

        public override void Visit(ITmxVisitor visitor)
        {
            // Visit ourselves
            visitor.VisitGroupLayer(this);

            // Visit our children
            foreach (var node in this.LayerNodes)
            {
                node.Visit(visitor);
            }
        }

        public static TmxGroupLayer FromXml(XElement xml, TmxLayerNode parent, TmxMap tmxMap)
        {
            Debug.Assert(xml.Name == "group");

            TmxGroupLayer tmxGroupLayer = new TmxGroupLayer(parent, tmxMap);
            tmxGroupLayer.FromXmlInternal(xml);
            return tmxGroupLayer;
        }
    }
}
