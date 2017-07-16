using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    // Setttings for compiling and export Tiled maps into Unity
    public class Settings
    {
        public static string ObjectTypeXml = "";

        public static float Scale = 1.0f;
        public static bool PreferConvexPolygons = false;
        public static bool DepthBufferEnabled = false;

        public static readonly float DefaultTexelBias = 0.0f;
        public static float TexelBias = DefaultTexelBias;

        // If we're automatically opening, exporting, and closing then there are some code paths we don't want to take
        public static bool IsAutoExporting = false;

        // Some old operating systems (like Windows 7) are incompatible with the Skia library and throw exceptions
        // We want to try to handle those execptions and disable previewing when that happens
        public static event EventHandler PreviewingDisabled;

        public static void DisablePreviewing()
        {
            if (PreviewingDisabled != null)
            {
                PreviewingDisabled.Invoke(null, EventArgs.Empty);
            }
        }
    }
}
