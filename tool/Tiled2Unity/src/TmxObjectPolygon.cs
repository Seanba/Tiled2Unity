using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    class TmxObjectPolygon : TmxObject, TmxHasPoints
    {
        public List<PointF> Points { get; protected set; }

        public TmxObjectPolygon()
        {
            this.Points = new List<PointF>();
        }

        public override RectangleF GetWorldBounds()
        {
            float xmin = float.MaxValue;
            float xmax = float.MinValue;
            float ymin = float.MaxValue;
            float ymax = float.MinValue;

            foreach (var p in this.Points)
            {
                xmin = Math.Min(xmin, p.X);
                xmax = Math.Max(xmax, p.X);
                ymin = Math.Min(ymin, p.Y);
                ymax = Math.Max(ymax, p.Y);
            }

            RectangleF bounds = new RectangleF(xmin, ymin, xmax - xmin, ymax - ymin);
            bounds.Offset(this.Position);
            return bounds;
        }

        protected override void InternalFromXml(System.Xml.Linq.XElement xml, TmxMap tmxMap)
        {
            var points = from pt in xml.Element("polygon").Attribute("points").Value.Split(' ')
                         let x = float.Parse(pt.Split(',')[0])
                         let y = float.Parse(pt.Split(',')[1])
                         select new PointF(x, y);

            this.Points = points.ToList();
        }

        protected override string InternalGetDefaultName()
        {
            return "PolygonObject";
        }


        public override string ToString()
        {
            StringBuilder pts = new StringBuilder();
            if (this.Points == null)
            {
                pts.Append("<empty>");
            }
            else
            {
                foreach (var p in this.Points)
                {
                    pts.AppendFormat("({0}, {1})", p.X, p.Y);
                    if (p != this.Points.Last())
                    {
                        pts.AppendFormat(", ");
                    }
                }
            }

            return String.Format("{0} {1} {2} points=({3})", GetType().Name, GetNonEmptyName(), this.Position, pts.ToString());
        }

        public bool ArePointsClosed()
        {
            return true;
        }

    }
}
