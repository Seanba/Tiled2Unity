using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Tiled2Unity
{
    partial class TmxImage
    {
        public static TmxImage FromXml(XElement elemImage)
        {
            TmxImage tmxImage = new TmxImage();
            tmxImage.Path = TmxHelper.GetAttributeAsFullPath(elemImage, "source");

            // Width and height are optional.
            int width = TmxHelper.GetAttributeAsInt(elemImage, "width", 0);
            int height = TmxHelper.GetAttributeAsInt(elemImage, "height", 0);

            // Prefer to use the actual width and height anyway so that UVs do not get jacked
            using (Image bitmap = Bitmap.FromFile(tmxImage.Path))
            {
                width = bitmap.Width;
                height = bitmap.Height;
            }

            tmxImage.Size = new System.Drawing.Size(width, height);

            // Some images use a transparency color key instead of alpha (blerg)
            tmxImage.TransparentColor = TmxHelper.GetAttributeAsString(elemImage, "trans", "");
            if (!String.IsNullOrEmpty(tmxImage.TransparentColor) && !tmxImage.TransparentColor.StartsWith("#"))
            {
                // The hash makes it an HTML color
                tmxImage.TransparentColor = "#" + tmxImage.TransparentColor;
            }

            return tmxImage;
        }
    }
}
