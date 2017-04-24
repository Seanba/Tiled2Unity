using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using SkiaSharp;

namespace Tiled2Unity
{
    public partial class TmxImage
    {
        public string AbsolutePath { get; private set; }
        public Size Size { get; private set; }
        public String TransparentColor { get; set; }
        public string ImageName { get; private set; }

#if !TILED_2_UNITY_LITE
        public SKBitmap ImageBitmap { get; private set; }
#endif
    }
}
