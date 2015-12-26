using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Tiled2Unity
{
    // partial class methods that build tile data from xml
    partial class TmxTile
    {
        public void ParseTileXml(XElement elem, TmxMap tmxMap, uint firstId)
        {
            Program.WriteLine("Parse tile data (gid = {0}, id {1}) ...", this.GlobalId, this.LocalId);
            Program.WriteVerbose(elem.ToString());

            this.Properties = TmxProperties.FromXml(elem);

            // Do we have an object group for this tile?
            XElement elemObjectGroup = elem.Element("objectgroup");
            if (elemObjectGroup != null)
            {
                this.ObjectGroup = TmxObjectGroup.FromXml(elemObjectGroup, tmxMap);
            }

            // Is this an animated tile?
            XElement elemAnimation = elem.Element("animation");
            if (elemAnimation != null)
            {
                this.Animation = TmxAnimation.FromXml(elemAnimation, firstId);
            }
        }

    }
}
