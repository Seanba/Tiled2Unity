using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Xml;
using System.Xml.Linq;

namespace Tiled2Unity
{
    partial class TmxObject
    {
        public static TmxObject FromXml(XElement xml, TmxMap tmxMap)
        {
            Program.WriteLine("Parsing object ...");
            Program.WriteVerbose(xml.ToString());

            // What kind of TmxObject are we creating?
            TmxObject tmxObject = null;

            if (xml.Element("ellipse") != null)
            {
                tmxObject = new TmxObjectEllipse();
            }
            else if (xml.Element("polygon") != null)
            {
                tmxObject = new TmxObjectPolygon();
            }
            else if (xml.Element("polyline") != null)
            {
                tmxObject = new TmxObjectPolyline();
            }
            else if (xml.Attribute("gid") != null)
            {
                tmxObject = new TmxObjectTile();
            }
            else
            {
                // Just a rectangle
                tmxObject = new TmxObjectRectangle();
            }

            // Data found on every object type
            tmxObject.Name = TmxHelper.GetAttributeAsString(xml, "name", "");
            tmxObject.Type = TmxHelper.GetAttributeAsString(xml, "type", "");
            tmxObject.Visible = TmxHelper.GetAttributeAsInt(xml, "visible", 1) == 1;

            float x = TmxHelper.GetAttributeAsFloat(xml, "x");
            float y = TmxHelper.GetAttributeAsFloat(xml, "y");
            float r = TmxHelper.GetAttributeAsFloat(xml, "rotation", 0);
            float w = TmxHelper.GetAttributeAsFloat(xml, "width", 0);
            float h = TmxHelper.GetAttributeAsFloat(xml, "height", 0);
            tmxObject.Position = new System.Drawing.PointF(x, y);
            tmxObject.Size = new System.Drawing.SizeF(w, h);
            tmxObject.Rotation = r;

            tmxObject.Properties = TmxProperties.FromXml(xml);

            tmxObject.InternalFromXml(xml, tmxMap);

            return tmxObject;
        }
    }
}
