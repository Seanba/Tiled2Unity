using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Tiled2Unity
{
    public partial class TmxFrame
    {
        public uint GlobalTileId { get; private set; }
        public int DurationMs { get; private set; }

        public static TmxFrame FromTileId(uint tileId)
        {
            TmxFrame tmxFrame = new TmxFrame();
            tmxFrame.GlobalTileId = tileId;
            tmxFrame.DurationMs = 0;

            return tmxFrame;
        }

        public static TmxFrame FromXml(XElement xml, uint globalStartId)
        {
            TmxFrame tmxFrame = new TmxFrame();

            uint localTileId = TmxHelper.GetAttributeAsUInt(xml, "tileid");
            tmxFrame.GlobalTileId = localTileId + globalStartId;
            tmxFrame.DurationMs = TmxHelper.GetAttributeAsInt(xml, "duration", 100);

            return tmxFrame;
        }
    }
}
