using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    // Setttings for compiling and export Tiled maps into Unity
    public class Settings
    {
        static public string ObjectTypeXml = "";

        public static float Scale = 1.0f;
        public static bool PreferConvexPolygons = false;
        public static bool DepthBufferEnabled = false;

        public static readonly float DefaultTexelBias = 8192.0f;
        public static float TexelBias = DefaultTexelBias;
    }
}
