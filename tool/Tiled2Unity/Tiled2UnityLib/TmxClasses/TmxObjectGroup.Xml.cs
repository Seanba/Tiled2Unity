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
        public static TmxObjectGroup FromXml(XElement xml, TmxLayerNode parent, TmxMap tmxMap)
        {
            Debug.Assert(xml.Name == "objectgroup");

            TmxObjectGroup tmxObjectGroup = new TmxObjectGroup(parent, tmxMap);
            tmxObjectGroup.FromXmlInternal(xml);

            // Color is specific to object group
            tmxObjectGroup.Color = TmxHelper.GetAttributeAsColor(xml, "color", Color.FromArgb(128, 128, 128));

            // Get all the objects
            Logger.WriteVerbose("Parsing objects in object group '{0}'", tmxObjectGroup.Name);
            var objects = from obj in xml.Elements("object")
                          select TmxObject.FromXml(obj, tmxObjectGroup, tmxMap);

            // The objects are ordered "visually" by Y position
            tmxObjectGroup.Objects = objects.OrderBy(o => TmxMath.ObjectPointFToMapSpace(tmxMap, o.Position).Y).ToList();

            return tmxObjectGroup;
        }

    }
}
