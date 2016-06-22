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
        public static TmxObject FromXml(XElement xml, TmxObjectGroup tmxObjectGroup, TmxMap tmxMap)
        {
            Logger.WriteLine("Parsing object ...");

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
                uint gid = TmxHelper.GetAttributeAsUInt(xml, "gid");
                gid = TmxMath.GetTileIdWithoutFlags(gid);
                if (tmxMap.Tiles.ContainsKey(gid))
                {
                    tmxObject = new TmxObjectTile();
                }
                else
                {
                    // For some reason, the tile is not in any of our tilesets
                    // Warn the user and use a rectangle
                    Logger.WriteWarning("Tile Id {0} not found in tilesets. Using a rectangle instead.\n{1}", gid, xml.ToString());
                    tmxObject = new TmxObjectRectangle();
                }
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
            tmxObject.ParentObjectGroup = tmxObjectGroup;

            float x = TmxHelper.GetAttributeAsFloat(xml, "x");
            float y = TmxHelper.GetAttributeAsFloat(xml, "y");
            float w = TmxHelper.GetAttributeAsFloat(xml, "width", 0);
            float h = TmxHelper.GetAttributeAsFloat(xml, "height", 0);
            float r = TmxHelper.GetAttributeAsFloat(xml, "rotation", 0);
            tmxObject.Position = new System.Drawing.PointF(x, y);
            tmxObject.Size = new System.Drawing.SizeF(w, h);
            tmxObject.Rotation = r;

            tmxObject.Properties = TmxProperties.FromXml(xml);

            tmxObject.InternalFromXml(xml, tmxMap);

            return tmxObject;
        }
    }
}
