using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Tiled2Unity
{
    public partial class TmxAnimation
    {
        public List<TmxFrame> Frames { get; private set; }
        public int TotalTimeMs { get; private set; }

        public TmxAnimation()
        {
            this.Frames = new List<TmxFrame>();
        }

        public static TmxAnimation FromXml(XElement xml, uint globalStartId)
        {
            TmxAnimation tmxAnimation = new TmxAnimation();

            foreach (var xmlFrame in xml.Elements("frame"))
            {
                TmxFrame tmxFrame = TmxFrame.FromXml(xmlFrame, globalStartId);
                tmxAnimation.Frames.Add(tmxFrame);
                tmxAnimation.TotalTimeMs += tmxFrame.DurationMs;
            }

            return tmxAnimation;
        }

        // Returns an single frame animation
        public static TmxAnimation FromTileId(uint globalTileId)
        {
            TmxAnimation tmxAnimation = new TmxAnimation();

            TmxFrame tmxFrame = TmxFrame.FromTileId(globalTileId);
            tmxAnimation.Frames.Add(tmxFrame);

            return tmxAnimation;
        }

    }
}
