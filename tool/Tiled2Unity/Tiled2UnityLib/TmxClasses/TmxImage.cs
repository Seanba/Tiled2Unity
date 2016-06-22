using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    public partial class TmxImage
    {
        public string AbsolutePath { get; private set; }
        public Size Size { get; private set; }
        public String TransparentColor { get; set; }

#if !TILED_2_UNITY_LITE
        public Bitmap ImageBitmap { get; private set; }
#endif
    }
}
