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
        public string Name { get; private set; }
        public string Type { get; private set; }
        public bool Visible { get; private set; }
        public PointF Position { get; private set; }
        public SizeF Size { get; protected set; }
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

        static protected void CopyBaseProperties(TmxObject from, TmxObject to)
        {
            to.Name = from.Name;
            to.Type = from.Type;
            to.Visible = from.Visible;
            to.Position = from.Position;
            to.Size = from.Size;
            to.Rotation = from.Rotation;
            to.Properties = from.Properties;
        }

        public abstract RectangleF GetWorldBounds();
        protected abstract void InternalFromXml(XElement xml, TmxMap tmxMap);
        protected abstract string InternalGetDefaultName();
    }
}
