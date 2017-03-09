using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    public partial class TmxObjectGroup : TmxLayerNode
    {
        public List<TmxObject> Objects { get; private set; }
        public Color Color { get; private set; }

        public TmxObjectGroup(TmxLayerNode parent, TmxMap tmxMap) : base(parent, tmxMap)
        {
            this.Objects = new List<TmxObject>();
        }

        public RectangleF GetWorldBounds(PointF translation)
        {
            RectangleF bounds = new RectangleF();
            foreach (var obj in this.Objects)
            {
                RectangleF objBounds = obj.GetWorldBounds();
                objBounds.Offset(translation);
                bounds = RectangleF.Union(bounds, objBounds);
            }
            return bounds;
        }

        public RectangleF GetWorldBounds()
        {
            return GetWorldBounds(new PointF(0, 0));
        }

        public override string ToString()
        {
            return String.Format("{{ ObjectGroup name={0}, numObjects={1} }}", this.Name, this.Objects.Count());
        }

        public override void Visit(ITmxVisitor visitor)
        {
            // Visit ourselves
            visitor.VisitObjectLayer(this);

            // Visit all our objects
            foreach (var obj in this.Objects)
            {
                visitor.VisitObject(obj);
            }
        }

    }
}
