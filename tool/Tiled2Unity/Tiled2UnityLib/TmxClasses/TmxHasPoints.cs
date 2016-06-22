using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tiled2Unity
{
    public interface TmxHasPoints
    {
        List<PointF> Points { get; set; }
        bool ArePointsClosed();
    }
}
