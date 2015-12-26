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
        public Bitmap ImageBitmap { get; private set; }
    }
}
