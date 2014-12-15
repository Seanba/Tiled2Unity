using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    public partial class TmxObjectGroup : TmxHasProperties
    {
        public string Name { get; private set; }
        public bool Visible { get; private set; }
        public TmxProperties Properties { get; private set; }
        public List<TmxObject> Objects { get; private set; }
        public Color Color { get; private set; }

        public TmxObjectGroup()
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

    }
}
