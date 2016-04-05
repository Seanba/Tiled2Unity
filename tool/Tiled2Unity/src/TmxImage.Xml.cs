using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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

#if TILED_2_UNITY_LITE
            // Do not open the image in Tiled2UnityLite (due to difficulty with GDI+ in some mono installs)
            int width = TmxHelper.GetAttributeAsInt(elemImage, "width");
            int height = TmxHelper.GetAttributeAsInt(elemImage, "height");
            tmxImage.Size = new System.Drawing.Size(width, height);
#else
            try
            {
                // We use pre-muliplied alpha pixel format (it is 2x faster)
                Bitmap bitmapRaw = (Bitmap)Bitmap.FromFile(tmxImage.AbsolutePath);
                Bitmap bitmapPArgb = new Bitmap(bitmapRaw.Width, bitmapRaw.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

                using (Graphics g = Graphics.FromImage(bitmapPArgb))
                {
                    g.DrawImage(bitmapRaw, 0, 0, bitmapPArgb.Width, bitmapPArgb.Height);
                    tmxImage.ImageBitmap = bitmapPArgb;
                }

                tmxImage.ImageBitmap = bitmapPArgb;
            }
            catch (FileNotFoundException fnf)
            {
                string msg = String.Format("Image file not found: {0}", tmxImage.AbsolutePath);
                throw new TmxException(msg, fnf);

                // Testing for when image files are missing. Just make up an image.
                //int width = TmxHelper.GetAttributeAsInt(elemImage, "width");
                //int height = TmxHelper.GetAttributeAsInt(elemImage, "height");
                //tmxImage.ImageBitmap = new Bitmap(width, height);
                //using (Graphics g = Graphics.FromImage(tmxImage.ImageBitmap))
                //{
                //    int color32 = tmxImage.AbsolutePath.GetHashCode();
                //    Color color = Color.FromArgb(color32);
                //    color = Color.FromArgb(255, color);
                //    using (Brush brush = new SolidBrush(color))
                //    {
                //        g.FillRectangle(brush, new Rectangle(Point.Empty, tmxImage.ImageBitmap.Size));
                //    }
                //}
            }

            tmxImage.Size = new System.Drawing.Size(tmxImage.ImageBitmap.Width, tmxImage.ImageBitmap.Height);
#endif

            // Some images use a transparency color key instead of alpha (blerg)
            tmxImage.TransparentColor = TmxHelper.GetAttributeAsString(elemImage, "trans", "");
            if (!String.IsNullOrEmpty(tmxImage.TransparentColor))
            {
                if (!tmxImage.TransparentColor.StartsWith("#"))
                {
                    // The hash makes it an HTML color
                    tmxImage.TransparentColor = "#" + tmxImage.TransparentColor;
                }

#if !TILED_2_UNITY_LITE
                System.Drawing.Color transColor = System.Drawing.ColorTranslator.FromHtml(tmxImage.TransparentColor);
                tmxImage.ImageBitmap.MakeTransparent(transColor);
#endif
            }

            return tmxImage;
        }
    }
}
