using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Tiled2Unity
{
    public abstract partial class TmxObject : TmxHasProperties
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public string Type { get; private set; }
        public bool Visible { get; private set; }
        public PointF Position { get; private set; }
        public SizeF Size { get; private set; }
        public float Rotation { get; private set; }
        public TmxProperties Properties { get; private set; }
        public TmxObjectGroup ParentObjectGroup { get; private set; }

        public string GetNonEmptyName()
        {
            if (String.IsNullOrEmpty(this.Name))
                return InternalGetDefaultName();
            return this.Name;
        }

        public override string ToString()
        {
            return String.Format("{0} {1} pos={2}, size={3} rot = {4}", GetType().Name, GetNonEmptyName(), this.Position, this.Size, this.Rotation);
        }

        public void BakeRotation()
        {
            // Rotate (0, 0)
            PointF[] pointfs = new PointF[1] { PointF.Empty };
            TmxMath.RotatePoints(pointfs, this);

            // Bake that rotation into our position, sanitizing the result
            float x = this.Position.X - pointfs[0].X;
            float y = this.Position.Y - pointfs[0].Y;
            this.Position = new PointF(x, y);
            this.Position = TmxMath.Sanitize(this.Position);

            // Null out our rotation
            this.Rotation = 0;
        }

        static protected void CopyBaseProperties(TmxObject from, TmxObject to)
        {
            to.Id = from.Id;
            to.Name = from.Name;
            to.Type = from.Type;
            to.Visible = from.Visible;
            to.Position = from.Position;
            to.Size = from.Size;
            to.Rotation = from.Rotation;
            to.Properties = from.Properties;
            to.ParentObjectGroup = from.ParentObjectGroup;
        }

        // Get the world boundary taking into account that the parent object group (and/or one of its ancestors) is offset
        public RectangleF GetOffsetWorldBounds()
        {
            RectangleF bounds = GetWorldBounds();
            PointF combinedOffset = this.ParentObjectGroup.GetCombinedOffset();
            bounds.Offset(combinedOffset);
            return bounds;
        }

        public abstract RectangleF GetWorldBounds();
        protected abstract void InternalFromXml(XElement xml, TmxMap tmxMap);
        protected abstract string InternalGetDefaultName();
    }
}
