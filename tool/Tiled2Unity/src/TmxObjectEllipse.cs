using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    class TmxObjectEllipse : TmxObject
    {
        public bool IsCircle()
        {
            return (this.Size.Width == this.Size.Height);
        }

        public float Radius
        {
            get
            {
                Debug.Assert(IsCircle());
                return this.Size.Width * 0.5f;
            }
        }

        public override System.Drawing.RectangleF GetWorldBounds()
        {
            return new System.Drawing.RectangleF(this.Position, this.Size);
        }

        protected override void InternalFromXml(System.Xml.Linq.XElement xml, TmxMap tmxMap)
        {
            // No extra data for ellipses
        }

        protected override string InternalGetDefaultName()
        {
            if (IsCircle())
                return "CircleObject";
            return "EllipseObject";
        }

    }
}
