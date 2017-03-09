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

            // Get default image size in case we are not opening the file
            {
                int width = TmxHelper.GetAttributeAsInt(elemImage, "width", 0);
                int height = TmxHelper.GetAttributeAsInt(elemImage, "height", 0);
                tmxImage.Size = new System.Drawing.Size(width, height);
            }

            // Do not open the image in Tiled2UnityLite (due to difficulty with GDI+ in some mono installs)
#if !TILED_2_UNITY_LITE
            if (!Tiled2Unity.Settings.IsAutoExporting)
            {
                try
                {
                    tmxImage.ImageBitmap = TmxHelper.FromFileBitmap32bpp(tmxImage.AbsolutePath);
                }
                catch (FileNotFoundException fnf)
                {
                    string msg = String.Format("Image file not found: {0}", tmxImage.AbsolutePath);
                    throw new TmxException(msg, fnf);

                    // Testing for when image files are missing. Just make up an image.
                    //int width = TmxHelper.GetAttributeAsInt(elemImage, "width");
                    //int height = TmxHelper.GetAttributeAsInt(elemImage, "height");
                    //tmxImage.ImageBitmap = new TmxHelper.CreateBitmap32bpp(width, height);
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
            }
#endif

            // Some images use a transparency color key instead of alpha (blerg)
            tmxImage.TransparentColor = TmxHelper.GetAttributeAsString(elemImage, "trans", "");

#if !TILED_2_UNITY_LITE
            if (!String.IsNullOrEmpty(tmxImage.TransparentColor) && tmxImage.ImageBitmap != null)
            {
                System.Drawing.Color transColor = TmxHelper.ColorFromHtml(tmxImage.TransparentColor);
                tmxImage.ImageBitmap.MakeTransparent(transColor);
            }
#endif

            return tmxImage;
        }
    }
}
