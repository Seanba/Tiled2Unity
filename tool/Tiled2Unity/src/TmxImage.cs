using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    public partial class TmxImage
    {
        public string Path { get; private set; }
        public Size Size { get; private set; }
        public String TransparentColor { get; set; }
    }
}
