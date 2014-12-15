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
        public uint UniqueFrameId { get; private set; }
        public uint LocalTileId { get; private set; }
        public uint GlobalTileId { get; private set; }
        public int DurationMs { get; private set; }

        public static TmxFrame FromXml(XElement xml, uint globalStartId)
        {
            TmxFrame tmxFrame = new TmxFrame();

            tmxFrame.UniqueFrameId = TmxMap.GetUniqueId();
            tmxFrame.LocalTileId = TmxHelper.GetAttributeAsUInt(xml, "tileid");
            tmxFrame.GlobalTileId = tmxFrame.LocalTileId + globalStartId;
            tmxFrame.DurationMs = TmxHelper.GetAttributeAsInt(xml, "duration", 100);

            return tmxFrame;
        }
    }
}
