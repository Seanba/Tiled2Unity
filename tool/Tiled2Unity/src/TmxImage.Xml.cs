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
            tmxImage.AbsolutePath = TmxHelper.GetAttributeAsFullPath(elemImage, "source");

            tmxImage.ImageBitmap = (Bitmap)Bitmap.FromFile(tmxImage.AbsolutePath);
            tmxImage.Size = new System.Drawing.Size(tmxImage.ImageBitmap.Width, tmxImage.ImageBitmap.Height);

            // Some images use a transparency color key instead of alpha (blerg)
            tmxImage.TransparentColor = TmxHelper.GetAttributeAsString(elemImage, "trans", "");
            if (!String.IsNullOrEmpty(tmxImage.TransparentColor))
            {
                if (!tmxImage.TransparentColor.StartsWith("#"))
                {
                    // The hash makes it an HTML color
                    tmxImage.TransparentColor = "#" + tmxImage.TransparentColor;
                }

                System.Drawing.Color transColor = System.Drawing.ColorTranslator.FromHtml(tmxImage.TransparentColor);
                tmxImage.ImageBitmap.MakeTransparent(transColor);
            }

            return tmxImage;
        }
    }
}
