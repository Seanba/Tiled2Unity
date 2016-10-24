using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Tiled2Unity
{
    public partial class TmxObjectGroup
    {
        public static TmxObjectGroup FromXml(XElement xml, TmxMap tmxMap)
        {
            Debug.Assert(xml.Name == "objectgroup");

            TmxObjectGroup tmxObjectGroup = new TmxObjectGroup(tmxMap);

            // Order within Xml file is import for layer types
            tmxObjectGroup.XmlElementIndex = xml.NodesBeforeSelf().Count();

            tmxObjectGroup.Name = TmxHelper.GetAttributeAsString(xml, "name", "");
            tmxObjectGroup.Visible = TmxHelper.GetAttributeAsInt(xml, "visible", 1) == 1;
            tmxObjectGroup.Opacity = TmxHelper.GetAttributeAsFloat(xml, "opacity", 1);
            tmxObjectGroup.Color = TmxHelper.GetAttributeAsColor(xml, "color", Color.FromArgb(128, 128, 128));
            tmxObjectGroup.Properties = TmxProperties.FromXml(xml);

            // Set the "ignore" setting on this object group
            tmxObjectGroup.Ignore = tmxObjectGroup.Properties.GetPropertyValueAsEnum<IgnoreSettings>("unity:ignore", IgnoreSettings.False);

            PointF offset = new PointF(0, 0);
            offset.X = TmxHelper.GetAttributeAsFloat(xml, "offsetx", 0);
            offset.Y = TmxHelper.GetAttributeAsFloat(xml, "offsety", 0);
            tmxObjectGroup.Offset = offset;

            // Get all the objects
            Logger.WriteLine("Parsing objects in object group '{0}'", tmxObjectGroup.Name);
            var objects = from obj in xml.Elements("object")
                          select TmxObject.FromXml(obj, tmxObjectGroup, tmxMap);

            // The objects are ordered "visually" by Y position
            tmxObjectGroup.Objects = objects.OrderBy(o => TmxMath.ObjectPointFToMapSpace(tmxMap, o.Position).Y).ToList();

            // Are we using a unity:layer override?
            tmxObjectGroup.UnityLayerOverrideName = tmxObjectGroup.Properties.GetPropertyValueAsString("unity:layer", "");

            return tmxObjectGroup;
        }

    }
}
