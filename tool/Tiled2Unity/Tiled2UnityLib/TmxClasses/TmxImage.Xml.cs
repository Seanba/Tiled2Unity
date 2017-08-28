using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;

namespace Tiled2Unity
{
    partial class TmxImage
    {
        public static TmxImage FromXml(XElement elemImage, string prefix, string postfix)
        {
            TmxImage tmxImage = new TmxImage();
            tmxImage.AbsolutePath = TmxHelper.GetAttributeAsFullPath(elemImage, "source");
            tmxImage.ImageName = String.Format("{0}{1}{2}", prefix, Path.GetFileNameWithoutExtension(tmxImage.AbsolutePath), postfix);

            // Get default image size in case we are not opening the file
            {
                int width = TmxHelper.GetAttributeAsInt(elemImage, "width", 0);
                int height = TmxHelper.GetAttributeAsInt(elemImage, "height", 0);
                tmxImage.Size = new System.Drawing.Size(width, height);
            }

            bool canMakeTransparentPixels = true;

            // Do not open the image in Tiled2UnityLite (no SkiaSharp in Tiled2UnityLite)
#if !TILED_2_UNITY_LITE
            if (!Tiled2Unity.Settings.IsAutoExporting)
            {
                try
                {
                    if (!tmxImage.Size.IsEmpty)
                    {
                        // We know our size and can decode the image into our preferred format
                        var info = new SKImageInfo();
                        info.ColorType = SKColorType.Rgba8888;
                        info.AlphaType = SKAlphaType.Unpremul;
                        info.Width = tmxImage.Size.Width;
                        info.Height = tmxImage.Size.Height;
                        tmxImage.ImageBitmap = SKBitmap.Decode(tmxImage.AbsolutePath, info);
                    }
                    else
                    {
                        // Open the image without any helpful information
                        // This stops us from being able to make pixels transparent
                        tmxImage.ImageBitmap = SKBitmap.Decode(tmxImage.AbsolutePath);
                        canMakeTransparentPixels = false;
                    }

                    tmxImage.Size = new System.Drawing.Size(tmxImage.ImageBitmap.Width, tmxImage.ImageBitmap.Height);
                }
                catch (FileNotFoundException fnf)
                {
                    string msg = String.Format("Image file not found: {0}", tmxImage.AbsolutePath);
                    throw new TmxException(msg, fnf);
                }
                catch (Exception e)
                {
                    // Disable previewing. Some users are reporting problems. Perhaps due to older versions of windows.
                    Logger.WriteError("Error creating image with Skia Library.\n\tException: {0}\n\tStack:\n{1}", e.Message, e.StackTrace);
                    Tiled2Unity.Settings.DisablePreviewing();
                }
            }
#endif

            // Some images use a transparency color key instead of alpha (blerg)
            tmxImage.TransparentColor = TmxHelper.GetAttributeAsString(elemImage, "trans", "");

#if !TILED_2_UNITY_LITE
            if (!String.IsNullOrEmpty(tmxImage.TransparentColor) && tmxImage.ImageBitmap != null)
            {
                if (canMakeTransparentPixels)
                {
                    Logger.WriteInfo("Removing alpha from transparent pixels.");
                    System.Drawing.Color systemTransColor = TmxHelper.ColorFromHtml(tmxImage.TransparentColor);

                    // Set the transparent pixels if using color-keying
                    SKColor transColor = new SKColor((uint)systemTransColor.ToArgb()).WithAlpha(0);
                    for (int x = 0; x < tmxImage.ImageBitmap.Width; ++x)
                    {
                        for (int y = 0; y < tmxImage.ImageBitmap.Height; ++y)
                        {
                            SKColor pixel = tmxImage.ImageBitmap.GetPixel(x, y);
                            if (pixel.Red == transColor.Red && pixel.Green == transColor.Green && pixel.Blue == transColor.Blue)
                            {
                                tmxImage.ImageBitmap.SetPixel(x, y, transColor);
                            }
                        }
                    }
                }
                else
                {
                    Logger.WriteWarning("Cannot make transparent pixels for viewing purposes. Save tileset with newer verion of Tiled.");
                }
            }
#endif
            return tmxImage;
        }
    }
}
